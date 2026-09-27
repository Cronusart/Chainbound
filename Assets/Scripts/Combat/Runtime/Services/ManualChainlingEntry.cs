using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared inspector entry for manual party/encounter test providers.
    /// Mirrors production <see cref="ChainlingSnapshot"/> combat-relevant fields
    /// (stars, Cycle count, IVs, Cycle picks) so test battles match live progression.
    /// </summary>
    [Serializable]
    public class ManualChainlingEntry
    {
        [Tooltip("Species ID to look up in SpeciesDatabaseSO (e.g. 'validape').")]
        public string speciesId;

        [Tooltip("Display name override. If empty, species default is used.")]
        public string displayName;

        [Tooltip("Star level (0-5). Primary progression UX.")]
        [Range(0, CombatConstants.STAR_MAX)]
        public int stars;

        [Tooltip("Cycle / rebirth count (0-5). Applies Cycle auto flats/percents.")]
        [Range(0, CombatConstants.REBIRTH_MAX)]
        public int rebirthCount;

        [Tooltip("Optional mastery XP for reward/UI parity. Combat stats ignore this.")]
        [Min(0)]
        public int masteryXp;

        [Tooltip("Evolution stage (1-based). 0 defaults to 1.")]
        public int evolutionStage = 1;

        [Header("Visual Identity")]
        public ChainlingVisualIdentity visualIdentity = ChainlingVisualIdentity.CreateDefault();

        [Tooltip("Move definition IDs to equip. If empty, species defaults are used.")]
        public List<string> moveIds = new List<string>();

        [Tooltip("Optional modifier_1 section IDs aligned to moveIds (e.g. '6.4-a').")]
        public List<string> modifier1SectionIds = new List<string>();

        [Tooltip("Optional modifier_2 section IDs aligned to moveIds (e.g. '6.4-g').")]
        public List<string> modifier2SectionIds = new List<string>();

        [Header("Base IVs (-5 to 5)")]
        public int ivMaxHP;
        public int ivMeleeAtk;
        public int ivMeleeDef;
        public int ivRangedAtk;
        public int ivRangedDef;
        public int ivSpeed;

        [Header("Cycle Picks (allocation counts; sum should be <= rebirthCount)")]
        [Min(0)] public int pickMaxHP;
        [Min(0)] public int pickMeleeAtk;
        [Min(0)] public int pickMeleeDef;
        [Min(0)] public int pickRangedAtk;
        [Min(0)] public int pickRangedDef;
        [Min(0)] public int pickSpeed;

        [Header("Optional Wild / Vitals")]
        public bool isMutated;
        [Tooltip("Mutation FX / aura id when mutated.")]
        public string mutationId = ChainlingVisualIdentity.NoneId;
        public bool isEntangled;
        [Tooltip("Entangled type when isEntangled is set.")]
        public ChainlingType entangledType = ChainlingType.None;

        [Tooltip("Unsynced (-1) means start at full EffectiveMaxHP.")]
        public int currentHp = ChainlingVitalsConstants.UnsyncedCurrentHp;

        public int[] BuildIvArray()
        {
            return new[]
            {
                ivMaxHP,
                ivMeleeAtk,
                ivMeleeDef,
                ivRangedAtk,
                ivRangedDef,
                ivSpeed,
            };
        }

        public int[] BuildCyclePickArray()
        {
            return new[]
            {
                pickMaxHP,
                pickMeleeAtk,
                pickMeleeDef,
                pickRangedAtk,
                pickRangedDef,
                pickSpeed,
            };
        }

        public int SumCyclePicks()
        {
            return pickMaxHP + pickMeleeAtk + pickMeleeDef + pickRangedAtk + pickRangedDef + pickSpeed;
        }
    }
}
