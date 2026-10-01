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
        [Tooltip("Relative odds inside the pool. 0 excludes the move.")]
        [Min(0)] public int weight = 10;
        [Tooltip("When enabled, star-5 folio rolls and star-5 captures use weightAtStarFive instead of weight.")]
        public bool useWeightAtStarFive;
        [Tooltip("Relative odds at 5 stars. Only used when useWeightAtStarFive is enabled.")]
        [Min(0)] public int weightAtStarFive = 10;
        [Tooltip("Marks this entry as the species signature. Odds still come from the weights above.")]
        public bool isSignature;
        public ProgressionPoolTag poolTag = ProgressionPoolTag.Learnable;
        [Range(0, 5)] public int minStar;
        [Range(0, 5)] public int maxStar = CombatConstants.STAR_MAX;
        [Tooltip("One-time reward override. Leave disabled for normal repeatable star/mastery folio farming.")]
        public bool excludeIfOwned;
    }

    public static class SpeciesProgressionPoolWeights
    {
        public const int Signature = 8;
        public const int SignatureAtStarFive = 24;
        public const int Standard = 23;
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

        /// <summary>
        /// Full base budget: MaxHP plus the five combat stats.
        /// Inspector balance glimpse; not used by combat resolution.
        /// </summary>
        public int BaseNetStatTotal => baseMaxHP + BaseCombatStatTotal;

        [Header("Evolution")]
        [Tooltip("How many evolution stages this species has (1 = no evolution).")]
        public int maxEvolutionStages = 1;

        [Header("Move Pool")]
        [Tooltip("Theme exceptions this species can equip even when the move type does not match. Keep this to one or two moves.")]
        public List<MoveDefinitionSO> offTypeMoveExceptions = new List<MoveDefinitionSO>();

        [Tooltip("Curated learnable moves. Subset of the equippable space. Not the capture or folio roll.")]
        public List<MoveDefinitionSO> learnableMoves = new List<MoveDefinitionSO>();

        [Tooltip("Level thresholds at which each learnable move is granted.")]
        public List<int> learnLevels = new List<int>();

        [Tooltip("Starter exclusives. Learned only from this species' progression pool, and equippable only on this species.")]
        public List<MoveDefinitionSO> exclusiveMoves = new List<MoveDefinitionSO>();

        [Tooltip("Level 5 guaranteed reward. Must exist in learnable or exclusive pool.")]
        public MoveDefinitionSO flagshipMove;

        [Header("Initial Loadout")]
        [Tooltip("Fallback battle kit when a Chainling has no saved moves. Capture Known grants roll progressionMovePool instead.")]
        public List<MoveDefinitionSO> initialMoves = new List<MoveDefinitionSO>();

        [Header("Progression Reward Pool")]
        [Tooltip("Weighted pool for star/mastery folios and for the Known move a capture can already have equipped. Auto-seeded from legacy lists when empty. Set each entry's weight, and enable useWeightAtStarFive for a different 5-star odd.")]
        public List<SpeciesProgressionMoveEntry> progressionMovePool = new List<SpeciesProgressionMoveEntry>();

        [Header("Flags")]
        [Tooltip("Is this a starter species with exclusive moves?")]
        public bool isStarter;

        [Header("Capture")]
        [Tooltip("Default trading-trace volatility kit for this species. Traits, HP, and IVs stack on top. Not a catch %.")]
        public CaptureVolatilityProfile defaultCaptureProfile = CaptureVolatilityProfile.Standard;

        [Header("Catalog / Entry")]
        [Tooltip("Wild rarity band. Sets the net point budget and, later, encounter weight. Starters and bosses sit outside this ladder.")]
        public RarityTier rarity = RarityTier.Common;

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

            if (offTypeMoveExceptions == null)
                offTypeMoveExceptions = new List<MoveDefinitionSO>();

            if (offTypeMoveExceptions.Count > 2)
            {
                Debug.LogWarning(
                    $"{name}: offTypeMoveExceptions has {offTypeMoveExceptions.Count} moves. Species identity keeps this list to 1-2 theme exceptions.",
                    this);
            }

            if (progressionMovePool.Count == 0)
                SeedProgressionPoolFromLegacyLists();

            for (int i = 0; i < progressionMovePool.Count; i++)
            {
                SpeciesProgressionMoveEntry entry = progressionMovePool[i];
                if (entry == null)
                    continue;

                if (entry.weight < 0)
                    entry.weight = 0;

                if (entry.weightAtStarFive < 0)
                    entry.weightAtStarFive = 0;

                if (entry.maxStar == 0)
                    entry.maxStar = CombatConstants.STAR_MAX;

                if (entry.maxStar < entry.minStar)
                    entry.maxStar = entry.minStar;
            }
        }

        private void SeedProgressionPoolFromLegacyLists()
        {
            AppendLegacyMoves(learnableMoves, ProgressionPoolTag.Learnable);
            AppendLegacyMoves(exclusiveMoves, ProgressionPoolTag.Exclusive);
        }

        private void AppendLegacyMoves(
            IReadOnlyList<MoveDefinitionSO> moves,
            ProgressionPoolTag tag)
        {
            if (moves == null)
                return;

            for (int i = 0; i < moves.Count; i++)
            {
                MoveDefinitionSO move = moves[i];
                if (move == null || ContainsProgressionMove(move))
                    continue;

                bool signature = flagshipMove != null && move == flagshipMove;
                progressionMovePool.Add(new SpeciesProgressionMoveEntry
                {
                    move = move,
                    weight = signature ? SpeciesProgressionPoolWeights.Signature : SpeciesProgressionPoolWeights.Standard,
                    useWeightAtStarFive = signature,
                    weightAtStarFive = signature
                        ? SpeciesProgressionPoolWeights.SignatureAtStarFive
                        : SpeciesProgressionPoolWeights.Standard,
                    isSignature = signature,
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
