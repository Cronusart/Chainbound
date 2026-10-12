using Chainbound.Combat.Core;

namespace Chainbound.BattleService;

/// <summary>
/// Phase-1 Devnet exploit filter. This rejects internally inconsistent client
/// claims; it is deliberately not described as combat verification.
/// </summary>
public sealed class BattlePlausibilityValidator
{
    public bool TryClassifyTerminal(
        TeamCommandApiRequest command,
        AuthoritativeBattleState nextState,
        out string outcome,
        out string reason,
        out string error)
    {
        outcome = "";
        reason = "";
        error = "";
        if (command.BattleActive) return true;

        reason = string.IsNullOrWhiteSpace(command.EndReason)
            ? (command.WinnerTeamIndex == 0 ? "Victory" : "Defeat")
            : command.EndReason.Trim();
        if (reason.Equals("Draw", StringComparison.OrdinalIgnoreCase))
        {
            outcome = BattleTerminalOutcome.Draw;
            return true;
        }
        if (reason.Equals("Flee", StringComparison.OrdinalIgnoreCase) ||
            reason.Equals("Fled", StringComparison.OrdinalIgnoreCase))
        {
            outcome = BattleTerminalOutcome.Flee;
            return true;
        }
        if (command.WinnerTeamIndex != 0)
        {
            outcome = BattleTerminalOutcome.Defeat;
            return true;
        }

        bool hasTargets = nextState.Targets.Count > 0;
        bool allTargetsDefeated = hasTargets && nextState.Targets.All(target =>
            target.IsRemoved || target.CaptureContext.CurrentHp <= 0);
        bool livingPartyMember = nextState.Vitals.Any(vital =>
            vital.TeamIndex == 0 && vital.CurrentHp > 0);
        if (command.TurnNumber < 1 || !allTargetsDefeated || !livingPartyMember)
        {
            error = "A victory claim requires every hostile target at zero HP and at least one living party member.";
            return false;
        }

        outcome = BattleTerminalOutcome.Victory;
        return true;
    }
}
