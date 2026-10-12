using System.Diagnostics;
using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

public sealed class CaptureHistoryPoint
{
    public int Tick { get; set; }
    public int MeterBasisPoints { get; set; }
    public bool InsideBand { get; set; }
    public int TargetPositionUnits { get; set; }
    public int BandPositionUnits { get; set; }
}

public sealed class CaptureAttemptRuntime : IDisposable
{
    private readonly object _gate = new();
    private readonly ServiceOptions _options;
    private readonly Func<long> _authorityNow;
    private readonly Func<CaptureAttemptRuntime, CaptureSimulationState, Task<CaptureOutcome>> _finalize;
    private readonly Func<CaptureAttemptRuntime, CaptureSimulationState, Task> _checkpoint;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TaskCompletionSource<CaptureOutcome> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<CaptureHistoryPoint> _history = new();
    private CaptureSimulationState _state;
    private CaptureAuthoritativeState _latest;
    private long _lastInputSequence = -1;
    private bool _held;
    private int _connections;
    private long _lastDisconnectStopwatchTicks;
    private bool _perfectLock = true;
    private Task? _loop;
    private int _phase;

    public CaptureAttemptRuntime(
        CaptureChallengeSnapshot challenge,
        CaptureSimulationState? resumeState,
        ServiceOptions options,
        Func<long> authorityNow,
        Func<CaptureAttemptRuntime, CaptureSimulationState, Task<CaptureOutcome>> finalize,
        Func<CaptureAttemptRuntime, CaptureSimulationState, Task> checkpoint)
    {
        Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
        _options = options;
        _authorityNow = authorityNow;
        _finalize = finalize;
        _checkpoint = checkpoint;
        _state = resumeState?.Clone() ?? CaptureSimulation.CreateInitial(challenge);
        _held = _state.IsHeld;
        _latest = CreateSnapshot(_state, false);
        _lastDisconnectStopwatchTicks = Stopwatch.GetTimestamp();
        if (Challenge.DeadlineUnixMilliseconds > 0)
            _phase = Armed;
        else
            _ = WatchArmGraceAsync();
    }

    private const int Unarmed = 0;
    private const int Armed = 1;
    private const int Expired = 2;

    public CaptureChallengeSnapshot Challenge { get; }
    public Guid AttemptId => Guid.Parse(Challenge.AttemptId);
    public Guid BattleId => Guid.Parse(Challenge.BattleId);
    public Task<CaptureOutcome> Completion => _completion.Task;
    public bool PerfectLock { get { lock (_gate) return _perfectLock; } }
    public IReadOnlyList<CaptureHistoryPoint> History { get { lock (_gate) return _history.ToArray(); } }
    public CaptureAuthoritativeState Latest { get { lock (_gate) return CloneSnapshot(_latest); } }
    public bool IsArmed { get { lock (_gate) return _phase == Armed; } }

    public async Task<bool> ArmAsync()
    {
        bool start;
        lock (_gate)
        {
            if (_phase == Expired)
                return false;
            if (_phase == Armed)
                return true;
            _phase = Armed;
            long now = _authorityNow();
            long durationMilliseconds = (long)Math.Ceiling(
                Challenge.Parameters.DurationTicks * 1000d / Math.Max(1, Challenge.Parameters.TickRate));
            Challenge.DeadlineUnixMilliseconds = now + Math.Max(1, durationMilliseconds);
            start = true;
        }

        if (start)
        {
            Start();
            await _checkpoint(this, _state.Clone());
        }
        return true;
    }

    public void Start()
    {
        lock (_gate)
            _loop ??= RunAsync(_lifetime.Token);
    }

    public void Connect()
    {
        lock (_gate)
            _connections++;
    }

    public void Disconnect()
    {
        lock (_gate)
        {
            _connections = Math.Max(0, _connections - 1);
            if (_connections == 0)
                _lastDisconnectStopwatchTicks = Stopwatch.GetTimestamp();
        }
    }

    public void SetInput(long sequence, bool held)
    {
        lock (_gate)
        {
            if (sequence <= _lastInputSequence)
                return;
            _lastInputSequence = sequence;
            _held = held;
        }
    }

    public void ClearHeldInput()
    {
        lock (_gate)
            _held = false;
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private async Task WatchArmGraceAsync()
    {
        try
        {
            await Task.Delay(_options.CaptureArmGrace, _lifetime.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        CaptureSimulationState expired;
        lock (_gate)
        {
            if (_phase != Unarmed)
                return;
            _phase = Expired;
            _state.Outcome = CaptureOutcomeKind.TimedOut;
            expired = _state.Clone();
        }

        try
        {
            CaptureOutcome outcome = await _finalize(this, expired);
            _completion.TrySetResult(outcome);
        }
        catch (Exception exception)
        {
            _completion.TrySetResult(new CaptureOutcome
            {
                AttemptId = Challenge.AttemptId,
                Kind = CaptureOutcomeKind.Interrupted,
                ErrorCode = "service_interrupted",
                Message = exception.Message,
                BattleContinues = true,
            });
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            CatchUpReleasedTime();
            TimeSpan tickPeriod = TimeSpan.FromSeconds(1d / Math.Max(1, Challenge.Parameters.TickRate));
            using var timer = new PeriodicTimer(tickPeriod);
            while (_state.Outcome == CaptureOutcomeKind.None && await timer.WaitForNextTickAsync(cancellationToken))
            {
                bool held = ResolveHeld();
                CaptureTickResult tick = CaptureSimulation.Step(_state, Challenge.Parameters, held);
                lock (_gate)
                {
                    _state = tick.State;
                    if (tick.LeftBand)
                        _perfectLock = false;
                    if (_state.Tick % Math.Max(1, Challenge.Parameters.TickRate / _options.CorrectionRateHz) == 0 ||
                        _state.Outcome != CaptureOutcomeKind.None || tick.SpikeOccurred)
                    {
                        _latest = CreateSnapshot(_state, tick.SpikeOccurred);
                        _history.Add(new CaptureHistoryPoint
                        {
                            Tick = _state.Tick,
                            MeterBasisPoints = _state.MeterBasisPoints,
                            InsideBand = _state.IsInsideBand,
                            TargetPositionUnits = _state.TargetPositionUnits,
                            BandPositionUnits = _state.BandPositionUnits,
                        });
                    }
                }

                if (_state.Tick > 0 && _state.Tick % (Challenge.Parameters.TickRate * 3) == 0)
                    await _checkpoint(this, _state.Clone());
            }

            if (_state.Outcome != CaptureOutcomeKind.None)
            {
                CaptureOutcome outcome = await _finalize(this, _state.Clone());
                _completion.TrySetResult(outcome);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _completion.TrySetResult(new CaptureOutcome
            {
                AttemptId = Challenge.AttemptId,
                Kind = CaptureOutcomeKind.Interrupted,
                ErrorCode = "service_interrupted",
                Message = "Capture authority stopped before finalization.",
                BattleContinues = true,
            });
        }
        catch (Exception exception)
        {
            _completion.TrySetResult(new CaptureOutcome
            {
                AttemptId = Challenge.AttemptId,
                Kind = CaptureOutcomeKind.Interrupted,
                ErrorCode = "service_interrupted",
                Message = exception.Message,
                BattleContinues = true,
            });
        }
    }

    private void CatchUpReleasedTime()
    {
        long now = _authorityNow();
        long remainingMilliseconds = Math.Max(0, Challenge.DeadlineUnixMilliseconds - now);
        int remainingTicks = (int)Math.Min(
            Challenge.Parameters.DurationTicks,
            (remainingMilliseconds * Challenge.Parameters.TickRate + 999) / 1000);
        int expectedTick = Math.Max(_state.Tick, Challenge.Parameters.DurationTicks - remainingTicks);
        while (_state.Tick < expectedTick && _state.Outcome == CaptureOutcomeKind.None)
            _state = CaptureSimulation.Step(_state, Challenge.Parameters, false).State;
        lock (_gate)
            _latest = CreateSnapshot(_state, false);
    }

    private bool ResolveHeld()
    {
        lock (_gate)
        {
            if (_connections > 0)
                return _held;
            double disconnectedMilliseconds =
                (Stopwatch.GetTimestamp() - _lastDisconnectStopwatchTicks) * 1000d / Stopwatch.Frequency;
            if (disconnectedMilliseconds > _options.DisconnectedHoldRetention.TotalMilliseconds)
                _held = false;
            return _held;
        }
    }

    private static CaptureAuthoritativeState CloneSnapshot(CaptureAuthoritativeState source)
    {
        return new CaptureAuthoritativeState
        {
            AttemptId = source.AttemptId,
            Version = source.Version,
            Tick = source.Tick,
            TargetPositionUnits = source.TargetPositionUnits,
            BandPositionUnits = source.BandPositionUnits,
            MeterBasisPoints = source.MeterBasisPoints,
            RemainingTicks = source.RemainingTicks,
            LastInputSequence = source.LastInputSequence,
            IsHeld = source.IsHeld,
            IsInsideBand = source.IsInsideBand,
            SpikeOccurred = source.SpikeOccurred,
            Outcome = source.Outcome,
        };
    }

    private CaptureAuthoritativeState CreateSnapshot(CaptureSimulationState state, bool spikeOccurred)
    {
        CaptureAuthoritativeState snapshot = CaptureSimulation.ToSnapshot(
            Challenge.AttemptId,
            state.Tick,
            state,
            Challenge.Parameters,
            spikeOccurred);
        snapshot.LastInputSequence = _lastInputSequence;
        return snapshot;
    }
}
