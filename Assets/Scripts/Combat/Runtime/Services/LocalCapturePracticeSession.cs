using System;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat.Core;

namespace Chainbound.Combat
{
    /// <summary>
    /// Editor/test capture session driven by the same Update clock as the overlay.
    /// A second timer would let a lagged "outside band" snapshot overwrite a recapture.
    /// </summary>
    public interface IClientSteppedCaptureSession
    {
        CaptureSimulationState CurrentState { get; }
        CaptureTickResult Step(bool held);
    }

    /// <summary>
    /// Explicit Editor/test harness for capture rendering and golden-state tests.
    /// It does not consume inventory, grant a Chainling, or represent production authority.
    /// </summary>
    public sealed class LocalCapturePracticeSession : ICaptureSession, IClientSteppedCaptureSession
    {
        private readonly object gate = new object();
        private readonly TaskCompletionSource<CaptureOutcome> completion = new TaskCompletionSource<CaptureOutcome>();
        private CaptureSimulationState state;
        private CaptureAuthoritativeState latest;
        private long lastInputSequence = -1;
        private bool held;
        private bool finished;

        public LocalCapturePracticeSession(CaptureChallengeSnapshot challenge)
        {
            Challenge = challenge ?? throw new ArgumentNullException(nameof(challenge));
            state = CaptureSimulation.CreateInitial(challenge);
            latest = CaptureSimulation.ToSnapshot(challenge.AttemptId, 0, state, challenge.Parameters, false);
        }

        public CaptureChallengeSnapshot Challenge { get; }
        public CaptureAuthoritativeState LatestState { get { lock (gate) return latest; } }
        public Task<CaptureOutcome> Completion => completion.Task;
        public event Action<CaptureAuthoritativeState> SnapshotReceived;

        public CaptureSimulationState CurrentState
        {
            get
            {
                lock (gate)
                    return state.Clone();
            }
        }

        public Task SetHeldAsync(bool value, long inputSequence, CancellationToken cancellationToken = default(CancellationToken))
        {
            lock (gate)
            {
                if (inputSequence > lastInputSequence)
                {
                    lastInputSequence = inputSequence;
                    held = value;
                }
            }
            return Task.CompletedTask;
        }

        public void ClearHeldInput()
        {
            lock (gate)
                held = false;
        }

        public CaptureTickResult Step(bool heldOverride)
        {
            CaptureTickResult result;
            CaptureAuthoritativeState snapshot;
            lock (gate)
            {
                held = heldOverride;
                if (state.Outcome != CaptureOutcomeKind.None)
                    return new CaptureTickResult { State = state.Clone() };

                result = CaptureSimulation.Step(state, Challenge.Parameters, held);
                state = result.State;
                latest = CaptureSimulation.ToSnapshot(
                    Challenge.AttemptId,
                    state.Tick,
                    state,
                    Challenge.Parameters,
                    result.SpikeOccurred);
                snapshot = latest;
            }

            SnapshotReceived?.Invoke(snapshot);
            if (result.State.Outcome != CaptureOutcomeKind.None)
                CompleteFromState(result.State);
            return result;
        }

        public void Dispose()
        {
            ClearHeldInput();
            CompleteInterrupted();
        }

        private void CompleteFromState(CaptureSimulationState finalState)
        {
            if (finalState == null)
                return;
            TryComplete(new CaptureOutcome
            {
                AttemptId = Challenge.AttemptId,
                Kind = finalState.Outcome,
                BattleContinues = true,
                FinalMeterBasisPoints = finalState.MeterBasisPoints,
                ElapsedTicks = finalState.Tick,
                Message = finalState.Outcome switch
                {
                    CaptureOutcomeKind.Locked => "PRACTICE SIGNAL LOCKED",
                    CaptureOutcomeKind.Escaped => "PRACTICE TARGET FLED",
                    CaptureOutcomeKind.TimedOut => "PRACTICE ORB TIMED OUT",
                    _ => "PRACTICE CAPTURE ENDED",
                },
            });
        }

        private void CompleteInterrupted()
        {
            TryComplete(new CaptureOutcome
            {
                AttemptId = Challenge.AttemptId,
                Kind = CaptureOutcomeKind.Interrupted,
                ErrorCode = "practice_session_closed",
                BattleContinues = true,
            });
        }

        private void TryComplete(CaptureOutcome outcome)
        {
            lock (gate)
            {
                if (finished)
                    return;
                finished = true;
            }
            completion.TrySetResult(outcome);
        }
    }
}
