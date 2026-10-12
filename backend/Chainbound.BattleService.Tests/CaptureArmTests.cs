using Chainbound.Combat.Core;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class CaptureArmTests
{
    [Fact]
    public async Task Deadline_starts_when_the_attempt_is_armed()
    {
        long now = 1_000_000;
        CaptureChallengeSnapshot challenge = Challenge();
        var runtime = new CaptureAttemptRuntime(
            challenge,
            null,
            new ServiceOptions { CaptureArmGrace = TimeSpan.FromSeconds(5) },
            () => now,
            (_, _) => Task.FromResult(new CaptureOutcome { AttemptId = challenge.AttemptId }),
            (_, _) => Task.CompletedTask);

        await Task.Delay(80);
        Assert.False(runtime.IsArmed);
        Assert.Equal(0, runtime.Latest.Tick);
        Assert.Equal(0, challenge.DeadlineUnixMilliseconds);

        Assert.True(await runtime.ArmAsync());
        Assert.Equal(now + 20_000, challenge.DeadlineUnixMilliseconds);

        await Task.Delay(120);
        Assert.True(runtime.Latest.Tick > 0);
        Assert.True(runtime.Latest.Tick < challenge.Parameters.DurationTicks);
        runtime.Dispose();
    }

    [Fact]
    public async Task Unarmed_attempt_times_out_without_starting_the_clock()
    {
        CaptureChallengeSnapshot challenge = Challenge();
        CaptureSimulationState? finalized = null;
        var runtime = new CaptureAttemptRuntime(
            challenge,
            null,
            new ServiceOptions { CaptureArmGrace = TimeSpan.FromMilliseconds(40) },
            () => 1_000_000,
            (_, state) =>
            {
                finalized = state;
                return Task.FromResult(new CaptureOutcome
                {
                    AttemptId = challenge.AttemptId,
                    Kind = state.Outcome,
                });
            },
            (_, _) => Task.CompletedTask);

        CaptureOutcome outcome = await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(CaptureOutcomeKind.TimedOut, outcome.Kind);
        Assert.NotNull(finalized);
        Assert.Equal(0, finalized!.Tick);
        Assert.Equal(0, challenge.DeadlineUnixMilliseconds);
        runtime.Dispose();
    }

    [Fact]
    public async Task Expired_armed_attempt_catches_up_and_finalizes_without_a_client_connection()
    {
        long now = 2_000_000;
        CaptureChallengeSnapshot challenge = Challenge();
        challenge.Parameters.DurationTicks = 3;
        challenge.Parameters.LockRateBasisPointsPerSecond = 0;
        challenge.Parameters.EscapeRateBasisPointsPerSecond = 0;
        challenge.DeadlineUnixMilliseconds = now - 1;

        CaptureSimulationState? finalized = null;
        var runtime = new CaptureAttemptRuntime(
            challenge,
            CaptureSimulation.CreateInitial(challenge),
            new ServiceOptions { CaptureArmGrace = TimeSpan.FromSeconds(5) },
            () => now,
            (_, state) =>
            {
                finalized = state;
                return Task.FromResult(new CaptureOutcome
                {
                    AttemptId = challenge.AttemptId,
                    Kind = state.Outcome,
                });
            },
            (_, _) => Task.CompletedTask);

        runtime.Start();

        CaptureOutcome outcome = await runtime.Completion.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(CaptureOutcomeKind.TimedOut, outcome.Kind);
        Assert.NotNull(finalized);
        Assert.Equal(challenge.Parameters.DurationTicks, finalized!.Tick);
        runtime.Dispose();
    }

    [Fact]
    public async Task Capture_input_ignores_stale_release_and_reports_the_accepted_sequence()
    {
        long now = 3_000_000;
        CaptureChallengeSnapshot challenge = Challenge();
        var runtime = new CaptureAttemptRuntime(
            challenge,
            null,
            new ServiceOptions { CaptureArmGrace = TimeSpan.FromSeconds(5) },
            () => now,
            (_, state) => Task.FromResult(new CaptureOutcome { AttemptId = challenge.AttemptId, Kind = state.Outcome }),
            (_, _) => Task.CompletedTask);

        runtime.SetInput(8, true);
        runtime.SetInput(7, false);
        await runtime.ArmAsync();
        await Task.Delay(120);

        CaptureAuthoritativeState snapshot = runtime.Latest;
        Assert.Equal(8, snapshot.LastInputSequence);
        Assert.True(snapshot.IsHeld);
        runtime.Dispose();
    }

    private static CaptureChallengeSnapshot Challenge()
    {
        return new CaptureChallengeSnapshot
        {
            AttemptId = Guid.NewGuid().ToString("D"),
            BattleId = Guid.NewGuid().ToString("D"),
            DeadlineUnixMilliseconds = 0,
            Parameters = new CaptureSimulationParameters
            {
                TickRate = 30,
                DurationTicks = 600,
                GraphUnits = 1000,
                BandHeightUnits = 240,
            },
        };
    }
}
