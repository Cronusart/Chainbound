using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Area-level encounter defaults and base species table.
    /// Zones reference an area and inherit or override its fields.
    /// </summary>
    [CreateAssetMenu(fileName = "EncounterArea_", menuName = "Chainbound/Combat/Encounters/Encounter Area")]
    public class EncounterAreaSO : ScriptableObject
    {
        [Header("Identity")]
        public string areaId;

        [Header("Encounter Chance")]
        [Range(0f, 1f)]
        public float encounterChance = 0.15f;

        [Header("Reward Difficulty")]
        public EncounterDifficulty difficulty = EncounterDifficulty.Standard;

        [Header("Party Size")]
        [Range(1, 2)]
        public int minEncounterPartySize = 1;

        [Range(1, 2)]
        public int maxEncounterPartySize = 1;

        [Header("Extra Wild From Player Party")]
        [Tooltip("Chance of one extra wild when the player has 1 Chainling equipped. Boss encounters ignore this.")]
        [Range(0f, 1f)]
        public float extraWildChanceAtOne = WildPartyExtraRoll.DefaultChanceAtOne;

        [Tooltip("Chance of one extra wild when the player has 2 Chainlings equipped.")]
        [Range(0f, 1f)]
        public float extraWildChanceAtTwo = WildPartyExtraRoll.DefaultChanceAtTwo;

        [Tooltip("Chance of one extra wild when the player has 3 Chainlings equipped. This is the cap.")]
        [Range(0f, 1f)]
        public float extraWildChanceAtThree = WildPartyExtraRoll.DefaultChanceAtThree;

        [Header("Wild Traits")]
        public WildRollConfig wildTraits = new WildRollConfig();

        [Header("Rarity Tables")]
        public WildVariantRarityTableSO defaultVariantRarityTable;
        public EncounterRarityTierMultipliers variantTierMultipliers = new EncounterRarityTierMultipliers();
        public WildMutationRarityTableSO mutationRarityTable;
        public EncounterRarityTierMultipliers mutationTierMultipliers = new EncounterRarityTierMultipliers();

        [Header("Species Table")]
        public List<ZoneEncounterEntry> entries = new List<ZoneEncounterEntry>();

        [Header("Default Pools")]
        public WildMovePoolSO defaultMovePool;
        public WildVariantPoolSO defaultVariantPool;

        private void OnValidate()
        {
            if (maxEncounterPartySize < minEncounterPartySize)
                maxEncounterPartySize = minEncounterPartySize;
        }
    }
}
