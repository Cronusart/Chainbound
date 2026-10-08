namespace Chainbound.Combat
{
    /// <summary>
    /// Process-local handoff from the casual queue to the combat scene.
    /// Player A is always team 0 and player B is always team 1.
    /// </summary>
    public static class CasualPvpLaunchContext
    {
        public static CasualPvpLaunchPayload Current { get; private set; }

        public static bool HasPending =>
            Current != null && !string.IsNullOrWhiteSpace(Current.MatchId);

        public static void Publish(CasualPvpLaunchPayload payload)
        {
            Current = payload;
        }

        public static CasualPvpLaunchPayload Consume()
        {
            CasualPvpLaunchPayload payload = Current;
            Current = null;
            return payload;
        }

        public static void Clear()
        {
            Current = null;
        }
    }

    public sealed class CasualPvpLaunchPayload
    {
        public string MatchId;
        public int Seed;
        public int LocalTeamIndex;
        public string Seat;
        public string RulesetId;
        public CombatRulesetSO Ruleset;
        public PartySnapshot PartyA;
        public PartySnapshot PartyB;
        public IPvPTurnBridge TurnBridge;
        public System.Collections.Generic.List<CasualPvpCommittedTurn> History;
        public bool FullHeal = true;
        public int PendingTurnIndex;
        public System.Collections.Generic.List<CombatAction> PendingLocalActions;
    }

    public sealed class SnapshotPartyProvider : IPlayerPartyProvider
    {
        private readonly PartySnapshot party;

        public SnapshotPartyProvider(PartySnapshot party)
        {
            this.party = party ?? new PartySnapshot();
        }

        public PartySnapshot GetCurrentParty() => party;
    }

    public sealed class SnapshotEncounterProvider : IEncounterProvider
    {
        private readonly EncounterSnapshot encounter;

        public SnapshotEncounterProvider(EncounterSnapshot encounter)
        {
            this.encounter = encounter ?? new EncounterSnapshot();
        }

        public EncounterSnapshot GenerateEncounter(int seed) => encounter;
    }
}
