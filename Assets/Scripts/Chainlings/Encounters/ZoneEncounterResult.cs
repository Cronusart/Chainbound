using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Single slot in a zone encounter result (one wild chainling).
    /// </summary>
    [Serializable]
    public class ZoneEncounterSlot
    {
        public string speciesId;
        public WildTraits traits;
        public int stars;
        public int level;
        public int baseCaptureRate;

        /// <summary>Battle kit move ids (may be larger than the single capture Known).</summary>
        public List<string> moveIds = new List<string>();

        /// <summary>Exactly one move persisted on capture.</summary>
        public string knownMoveId;

        /// <summary>Rolled visual identity (variant / cosmetics).</summary>
        public ChainlingVisualIdentity visualIdentity;
    }

    /// <summary>
    /// Result of a successful zone-based encounter roll.
    /// Produced by <see cref="EncounterRollService"/> and consumed by the trigger
    /// to start a battle. May contain 1 or 2 chainlings depending on zone config.
    /// </summary>
    [Serializable]
    public class ZoneEncounterResult
    {
        /// <summary>One or two wild chainlings. Never empty when result is valid.</summary>
        public List<ZoneEncounterSlot> slots = new List<ZoneEncounterSlot>();

        public string zoneId;
        public string areaId;
        public TimeSegment segment;
        public int seed;
        public EncounterDifficulty difficulty = EncounterDifficulty.Standard;
        public UnityEngine.Object bossIntroDialogueAsset;
        public string debugLogLine;

        /// <summary>Modifier ids that shaped this roll (for QA).</summary>
        public List<string> activeModifierIds = new List<string>();

        /// <summary>
        /// First slot's species ID. Convenience for single-slot consumers.
        /// </summary>
        public string speciesId => slots.Count > 0 ? slots[0].speciesId : null;
    }
}
