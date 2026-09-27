using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Serializable DTO capturing everything needed to replay a battle deterministically.
    /// Pure C# â€” no MonoBehaviour dependency.
    /// </summary>
    [Serializable]
    public class CombatReplayData
    {
        /// <summary>RNG seed used to initialize the battle.</summary>
        public int seed;

        /// <summary>Active combat ruleset id stamped at battle start.</summary>
        public string rulesetId;

        /// <summary>Config hash of the active ruleset (+ resolver) stamped at battle start.</summary>
        public string configHash;

        /// <summary>Whether strict chemistry mode was enabled.</summary>
        public bool chemistryStrictMode;

        /// <summary>Snapshot of the player party at battle start.</summary>
        public PartySnapshot playerParty;

        /// <summary>Snapshot of the encounter/opponent party at battle start.</summary>
        public EncounterSnapshot encounter;

        /// <summary>Per-turn action records for deterministic replay.</summary>
        public List<TurnActionRecord> turnActions;

        /// <summary>Final result of the battle (populated after battle ends).</summary>
        public BattleResult result;

        /// <summary>Player-readable narration grouped by turn and action.</summary>
        public List<CombatNarrationTranscriptEntry> narrationTranscript;

        /// <summary>Schema version for future migration support.</summary>
        public string schemaVersion;

        /// <summary>Unix epoch timestamp (UTC) when the battle was recorded.</summary>
        public long timestampUtc;

        public CombatReplayData()
        {
            turnActions = new List<TurnActionRecord>();
            narrationTranscript = new List<CombatNarrationTranscriptEntry>();
            schemaVersion = "1.1";
        }

        public void AppendNarration(IEnumerable<CombatNarrationTranscriptEntry> entries)
        {
            if (entries == null)
                return;

            narrationTranscript ??= new List<CombatNarrationTranscriptEntry>();
            foreach (CombatNarrationTranscriptEntry entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;

                entry.sequenceIndex = narrationTranscript.Count;
                narrationTranscript.Add(entry);
            }
        }
    }

    /// <summary>
    /// Records the actions chosen by both teams for a single turn.
    /// When replayed with the same seed + party data, the combat engine
    /// produces identical outcomes.
    /// </summary>
    [Serializable]
    public class TurnActionRecord
    {
        /// <summary>1-based turn number.</summary>
        public int turnNumber;

        /// <summary>Actions submitted by team 0 (player) this turn.</summary>
        public List<CombatAction> team0Actions;

        /// <summary>Actions submitted by team 1 (opponent) this turn.</summary>
        public List<CombatAction> team1Actions;

        public TurnActionRecord()
        {
            team0Actions = new List<CombatAction>();
            team1Actions = new List<CombatAction>();
        }

        public TurnActionRecord(int turnNumber, List<CombatAction> team0, List<CombatAction> team1)
        {
            this.turnNumber = turnNumber;
            this.team0Actions = team0 != null ? new List<CombatAction>(team0) : new List<CombatAction>();
            this.team1Actions = team1 != null ? new List<CombatAction>(team1) : new List<CombatAction>();
        }
    }
}
