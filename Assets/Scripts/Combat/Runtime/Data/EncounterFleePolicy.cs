namespace Chainbound.Combat
{
    /// <summary>
    /// Encounter-authored flee behavior. UseDifficultyDefault keeps standard and elite encounters escapable
    /// while making Boss encounters consequential by default.
    /// </summary>
    public enum EncounterFleePolicy
    {
        UseDifficultyDefault = 0,
        Allowed = 1,
        Locked = 2,
    }

    /// <summary>Identifies the caller for diagnostics without changing the player-flee rules.</summary>
    public enum FleeRequestSource
    {
        ActionSelector,
        AppLifecycle,
        Legacy,
    }

    public enum FleeRequestStatus
    {
        Accepted,
        BattleInactive,
        AlreadyResolving,
        LockedByEncounter,
        UnavailableInPvP,
    }

    /// <summary>Inspectable result of a flee request for UI, lifecycle, and analytics callers.</summary>
    public readonly struct FleeRequestResult
    {
        public FleeRequestStatus Status { get; }
        public EncounterFleePolicy Policy { get; }
        public string Message { get; }
        public bool Accepted => Status == FleeRequestStatus.Accepted;

        public FleeRequestResult(FleeRequestStatus status, EncounterFleePolicy policy, string message)
        {
            Status = status;
            Policy = policy;
            Message = message ?? string.Empty;
        }
    }

    /// <summary>Pure encounter-flee policy resolution. Keep UI callers out of the battle-rule decision.</summary>
    public static class EncounterFleePolicyResolver
    {
        public static EncounterFleePolicy Resolve(
            EncounterSnapshot encounter,
            EncounterFleePolicy runtimeOverride = EncounterFleePolicy.UseDifficultyDefault)
        {
            if (runtimeOverride != EncounterFleePolicy.UseDifficultyDefault)
                return runtimeOverride;

            if (encounter != null && encounter.fleePolicy != EncounterFleePolicy.UseDifficultyDefault)
                return encounter.fleePolicy;

            return encounter != null && encounter.difficulty == EncounterDifficulty.Boss
                ? EncounterFleePolicy.Locked
                : EncounterFleePolicy.Allowed;
        }

        public static FleeRequestResult Evaluate(
            bool battleIsActive,
            bool fleeIsResolving,
            bool isPvP,
            EncounterSnapshot encounter,
            EncounterFleePolicy runtimeOverride = EncounterFleePolicy.UseDifficultyDefault)
        {
            EncounterFleePolicy policy = Resolve(encounter, runtimeOverride);
            if (!battleIsActive)
                return new FleeRequestResult(FleeRequestStatus.BattleInactive, policy, "There is no active battle to flee.");
            if (fleeIsResolving)
                return new FleeRequestResult(FleeRequestStatus.AlreadyResolving, policy, "Your team is already withdrawing.");
            if (isPvP)
                return new FleeRequestResult(FleeRequestStatus.UnavailableInPvP, policy, "Flee is unavailable in PvP.");
            if (policy == EncounterFleePolicy.Locked)
                return new FleeRequestResult(FleeRequestStatus.LockedByEncounter, policy, "This encounter cannot be fled.");

            return new FleeRequestResult(FleeRequestStatus.Accepted, policy, "Your team is withdrawing.");
        }
    }
}
