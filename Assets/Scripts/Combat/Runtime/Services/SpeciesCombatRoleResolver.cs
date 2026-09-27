using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Derives a single combat-role chip from species override, base-stat shape, and optional pool bias.
    /// </summary>
    public static class SpeciesCombatRoleResolver
    {
        private const float DominanceMargin = 1.12f;
        private const float PoolBiasRatio = 0.45f;

        public static SpeciesCombatRole Resolve(SpeciesDefinitionSO species)
        {
            if (species == null)
                return SpeciesCombatRole.Balanced;

            if (species.combatRoleOverride != SpeciesCombatRole.Unspecified)
                return species.combatRoleOverride;

            SpeciesCombatRole poolBias = ResolvePoolBias(species);
            SpeciesCombatRole statRole = ResolveFromBaseStats(species);

            if (poolBias != SpeciesCombatRole.Unspecified &&
                (statRole == SpeciesCombatRole.Balanced ||
                 poolBias == SpeciesCombatRole.Support ||
                 poolBias == SpeciesCombatRole.Disrupt))
            {
                // Prefer Support/Disrupt when the kit is clearly utility-heavy and stats are not a sharp striker/guard.
                if (poolBias == SpeciesCombatRole.Support || poolBias == SpeciesCombatRole.Disrupt)
                {
                    if (statRole == SpeciesCombatRole.Balanced ||
                        statRole == SpeciesCombatRole.Speed ||
                        IsSoftOffense(statRole))
                        return poolBias;
                }
            }

            return statRole;
        }

        public static string FormatLabel(SpeciesCombatRole role)
        {
            switch (role)
            {
                case SpeciesCombatRole.MeleeStriker:
                    return "Melee Striker";
                case SpeciesCombatRole.MeleeGuard:
                    return "Melee Guardian";
                case SpeciesCombatRole.RangedStriker:
                    return "Ranged Striker";
                case SpeciesCombatRole.RangedGuard:
                    return "Ranged Guardian";
                case SpeciesCombatRole.Balanced:
                    return "All-Rounder";
                case SpeciesCombatRole.Speed:
                    return "Speedy Striker";
                case SpeciesCombatRole.Support:
                    return "Support Warrior";
                case SpeciesCombatRole.Disrupt:
                    return "Utility Disruptor";
                default:
                    return "All-Rounder";
            }
        }

        private static bool IsSoftOffense(SpeciesCombatRole role)
        {
            return role == SpeciesCombatRole.MeleeGuard || role == SpeciesCombatRole.RangedGuard;
        }

        private static SpeciesCombatRole ResolveFromBaseStats(SpeciesDefinitionSO species)
        {
            float matk = Math.Max(1f, species.baseMeleeAtk);
            float mdef = Math.Max(1f, species.baseMeleeDef);
            float ratk = Math.Max(1f, species.baseRangedAtk);
            float rdef = Math.Max(1f, species.baseRangedDef);
            float spd = Math.Max(1f, species.baseSpeed);
            float mean = (matk + mdef + ratk + rdef + spd) / 5f;
            if (mean <= 0f)
                return SpeciesCombatRole.Balanced;

            float matkR = matk / mean;
            float mdefR = mdef / mean;
            float ratkR = ratk / mean;
            float rdefR = rdef / mean;
            float spdR = spd / mean;

            float peak = matkR;
            SpeciesCombatRole peakRole = SpeciesCombatRole.MeleeStriker;

            if (mdefR > peak)
            {
                peak = mdefR;
                peakRole = SpeciesCombatRole.MeleeGuard;
            }

            if (ratkR > peak)
            {
                peak = ratkR;
                peakRole = SpeciesCombatRole.RangedStriker;
            }

            if (rdefR > peak)
            {
                peak = rdefR;
                peakRole = SpeciesCombatRole.RangedGuard;
            }

            if (spdR > peak)
            {
                peak = spdR;
                peakRole = SpeciesCombatRole.Speed;
            }

            // Require a clear lead over the second-highest axis.
            float second = 0f;
            AccumulateSecond(matkR, peakRole != SpeciesCombatRole.MeleeStriker, ref second);
            AccumulateSecond(mdefR, peakRole != SpeciesCombatRole.MeleeGuard, ref second);
            AccumulateSecond(ratkR, peakRole != SpeciesCombatRole.RangedStriker, ref second);
            AccumulateSecond(rdefR, peakRole != SpeciesCombatRole.RangedGuard, ref second);
            AccumulateSecond(spdR, peakRole != SpeciesCombatRole.Speed, ref second);

            if (peak < DominanceMargin || peak < second * DominanceMargin)
                return SpeciesCombatRole.Balanced;

            return peakRole;
        }

        private static void AccumulateSecond(float value, bool eligible, ref float second)
        {
            if (eligible && value > second)
                second = value;
        }

        private static SpeciesCombatRole ResolvePoolBias(SpeciesDefinitionSO species)
        {
            int support = 0;
            int disrupt = 0;
            int total = 0;

            CountMoves(species.learnableMoves, ref support, ref disrupt, ref total);
            CountMoves(species.exclusiveMoves, ref support, ref disrupt, ref total);
            CountProgression(species.progressionMovePool, ref support, ref disrupt, ref total);

            if (total <= 0)
                return SpeciesCombatRole.Unspecified;

            float supportRatio = support / (float)total;
            float disruptRatio = disrupt / (float)total;

            if (supportRatio >= PoolBiasRatio && supportRatio >= disruptRatio)
                return SpeciesCombatRole.Support;
            if (disruptRatio >= PoolBiasRatio)
                return SpeciesCombatRole.Disrupt;

            return SpeciesCombatRole.Unspecified;
        }

        private static void CountMoves(
            IList<MoveDefinitionSO> moves,
            ref int support,
            ref int disrupt,
            ref int total)
        {
            if (moves == null)
                return;

            for (int i = 0; i < moves.Count; i++)
                ClassifyMove(moves[i], ref support, ref disrupt, ref total);
        }

        private static void CountProgression(
            IList<SpeciesProgressionMoveEntry> entries,
            ref int support,
            ref int disrupt,
            ref int total)
        {
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                SpeciesProgressionMoveEntry entry = entries[i];
                if (entry == null)
                    continue;
                ClassifyMove(entry.move, ref support, ref disrupt, ref total);
            }
        }

        private static void ClassifyMove(
            MoveDefinitionSO move,
            ref int support,
            ref int disrupt,
            ref int total)
        {
            if (move == null)
                return;

            total++;
            if (move.category == MoveCategory.Support || move.healPercentMaxHP > 0)
                support++;
            else if (move.category == MoveCategory.StatusUtility)
                disrupt++;
        }

    }
}
