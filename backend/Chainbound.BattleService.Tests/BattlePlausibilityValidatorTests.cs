using Chainbound.BattleService;
using Chainbound.Combat.Core;
using Xunit;

namespace Chainbound.BattleService.Tests;

public sealed class BattlePlausibilityValidatorTests
{
    private readonly BattlePlausibilityValidator _validator = new();

    [Fact]
    public void Victory_requires_all_targets_defeated_and_a_living_party_member()
    {
        AuthoritativeBattleState state = State(targetHp: 0, partyHp: 12);
        TeamCommandApiRequest command = Terminal(winner: 0, reason: "Victory");

        bool accepted = _validator.TryClassifyTerminal(
            command, state, out string outcome, out string reason, out string error);

        Assert.True(accepted, error);
        Assert.Equal(BattleTerminalOutcome.Victory, outcome);
        Assert.Equal("Victory", reason);
    }

    [Fact]
    public void Forged_victory_with_a_living_target_is_rejected()
    {
        bool accepted = _validator.TryClassifyTerminal(
            Terminal(winner: 0, reason: "Victory"),
            State(targetHp: 1, partyHp: 12),
            out _, out _, out string error);

        Assert.False(accepted);
        Assert.Contains("zero HP", error);
    }

    [Fact]
    public void Defeat_and_flee_never_classify_as_victory()
    {
        Assert.True(_validator.TryClassifyTerminal(
            Terminal(winner: 1, reason: "Defeat"), State(20, 0),
            out string defeat, out _, out _));
        Assert.Equal(BattleTerminalOutcome.Defeat, defeat);

        Assert.True(_validator.TryClassifyTerminal(
            Terminal(winner: -1, reason: "Flee"), State(20, 10),
            out string flee, out _, out _));
        Assert.Equal(BattleTerminalOutcome.Flee, flee);
    }

    private static TeamCommandApiRequest Terminal(int winner, string reason) => new()
    {
        BattleActive = false,
        WinnerTeamIndex = winner,
        EndReason = reason,
        TurnNumber = 1,
    };

    private static AuthoritativeBattleState State(int targetHp, int partyHp) => new()
    {
        Targets = new List<AuthoritativeBattleTarget>
        {
            new()
            {
                TargetId = "enemy-0",
                IsRemoved = targetHp <= 0,
                CaptureContext = new CaptureTargetContext { CurrentHp = targetHp, MaxHp = 20 },
            },
        },
        Vitals = new List<CombatVitalSnapshot>
        {
            new() { TeamIndex = 0, RosterIndex = 0, CurrentHp = partyHp },
            new() { TeamIndex = 1, RosterIndex = 0, CurrentHp = targetHp },
        },
    };
}
