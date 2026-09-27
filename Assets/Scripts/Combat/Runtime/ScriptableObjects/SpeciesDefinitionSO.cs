using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum ProgressionPoolTag
    {
        Learnable,
        Exclusive,
        EventOnly,
    }

    [Serializable]
    public class SpeciesProgressionMoveEntry
    {
        public MoveDefinitionSO move;
        [Min(0)] public int weight = 10;
        public ProgressionPoolTag poolTag = ProgressionPoolTag.Learnable;
        [Range(0, 5)] public int minStar;
        [Range(0, 5)] public int maxStar = CombatConstants.STAR_MAX;
        [Tooltip("One-time reward override. Leave disabled for normal repeatable star/mastery folio farming.")]
        public bool excludeIfOwned;
    }

    /// <summary>
    /// ScriptableObject defining a Chainling species.
    /// New species can be added by creating new SO assets Ã¢â‚¬â€ no code edits needed.
    /// Matches COMBAT_06_SpeciesMovePools_v1.2.
    /// </summary>
    [CreateAssetMenu(fileName = "Species_", menuName = "Chainbound/Combat/Species/Species Definition")]
    public class SpeciesDefinitionSO : ScriptableObject
    {
        [Header("Identity")]
        public string speciesId;
        public string displayName;

        [TextArea(2, 4)]
        public string description;

        [Header("Types")]
        public ChainlingType primaryType;
        public ChainlingType secondaryType;  // None if mono-type

        [Header("Base Stats")]
        public int baseMaxHP;
        public int baseMeleeAtk;
        public int baseMeleeDef;
        public int baseRangedAtk;
        public int baseRangedDef;
        public int baseSpeed;

        /// <summary>
        /// Sum of combat base stats excluding MaxHP (MATK+MDEF+RATK+RDEF+SPD).
        /// Inspector balance glimpse; not used by combat resolution.
        /// </summary>
        public int BaseCombatStatTotal =>
            baseMeleeAtk + baseMeleeDef + baseRangedAtk + baseRangedDef + baseSpeed;

        [Header("Evolution")]
        [Tooltip("How many evolution stages this species has (1 = no evolution).")]
        public int maxEvolutionStages = 1;

        [Header("Move Pool")]
        [Tooltip("Curated learnable Stamped moves (granted via leveling up). Subset of equippable pool.")]
        public List<MoveDefinitionSO> learnableMoves = new List<MoveDefinitionSO>();

        [Tooltip("Level thresholds at which each learnable move is granted.")]
        public List<int> learnLevels = new List<int>();

        [Tooltip("Exclusive signature Stamped moves (starters only: Validape, Gilarime, Kitsauto).")]
        public List<MoveDefinitionSO> exclusiveMoves = new List<MoveDefinitionSO>();

        [Tooltip("Level 5 guaranteed reward. Must exist in learnable or exclusive pool.")]
        public MoveDefinitionSO flagshipMove;

        [Header("Initial Loadout")]
        [Tooltip("Moves equipped when a new Chainling of this species is granted. Keep these in the learnable or exclusive pool.")]
        public List<MoveDefinitionSO> initialMoves = new List<MoveDefinitionSO>();

        [Header("Progression Reward Pool")]
        [Tooltip("Weighted move pool used by star/mastery progression grants. Auto-seeded from legacy move lists when empty.")]
        public List<SpeciesProgressionMoveEntry> progressionMovePool = new List<SpeciesProgressionMoveEntry>();

        [Header("Flags")]
        [Tooltip("Is this a starter species with exclusive moves?")]
        public bool isStarter;

        [Header("Capture")]
        [Tooltip("Default trading-trace volatility kit for this species. Traits, HP, and IVs stack on top. Not a catch %.")]
        public CaptureVolatilityProfile defaultCaptureProfile = CaptureVolatilityProfile.Standard;

        [Header("Catalog / Entry")]
        [Tooltip("When set, Party Entry combat-role chip uses this instead of base-stat / pool derivation.")]
        public SpeciesCombatRole combatRoleOverride = SpeciesCombatRole.Unspecified;

        [Header("Metadata")]
        [TextArea(2, 4)]
        public string designNotes;

        private void OnValidate()
        {
            if (progressionMovePool == null)
                progressionMovePool = new List<SpeciesProgressionMoveEntry>();

            if (initialMoves == null)
                initialMoves = new List<MoveDefinitionSO>();

            if (progressionMovePool.Count == 0)
                SeedProgressionPoolFromLegacyLists();

            for (int i = 0; i < progressionMovePool.Count; i++)
            {
                SpeciesProgressionMoveEntry entry = progressionMovePool[i];
                if (entry == null)
                    continue;

                if (entry.weight < 0)
                    entry.weight = 0;

                if (entry.maxStar == 0)
                    entry.maxStar = CombatConstants.STAR_MAX;

                if (entry.maxStar < entry.minStar)
                    entry.maxStar = entry.minStar;
            }
        }

        private void SeedProgressionPoolFromLegacyLists()
        {
            AppendLegacyMoves(learnableMoves, ProgressionPoolTag.Learnable, 10);
            AppendLegacyMoves(exclusiveMoves, ProgressionPoolTag.Exclusive, 5);
        }

        private void AppendLegacyMoves(
            IReadOnlyList<MoveDefinitionSO> moves,
            ProgressionPoolTag tag,
            int weight)
        {
            if (moves == null)
                return;

            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinitionSO move = moves[i];
                if (move == null || ContainsProgressionMove(move))
                    continue;

                progressionMovePool.Add(new SpeciesProgressionMoveEntry
                {
                    move = move,
                    weight = weight,
                    poolTag = tag,
                    minStar = 0,
                    maxStar = CombatConstants.STAR_MAX,
                    excludeIfOwned = false,
                });
            }
        }

        private bool ContainsProgressionMove(MoveDefinitionSO move)
        {
            if (move == null || progressionMovePool == null)
                return false;

            for (int i = 0; i < progressionMovePool.Count; i++)
            {
                SpeciesProgressionMoveEntry entry = progressionMovePool[i];
                if (entry != null && entry.move == move)
                    return true;
            }

            return false;
        }
    }
}
