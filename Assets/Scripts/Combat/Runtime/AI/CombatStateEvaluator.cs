using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Pure-function utility that scores a combat state from a team's perspective.
    /// Used by AI implementations to evaluate moves and by balance tools to summarize outcomes.
    ///
    /// Score range: [-1.0, +1.0]
    ///   +1.0 = total domination (all enemies KO, full HP)
    ///   0.0  = even
    ///   -1.0 = total defeat
    /// </summary>
    public static class CombatStateEvaluator
    {
        /// <summary>
        /// Evaluate the state from a team's perspective.
        /// </summary>
        public static float Evaluate(CombatState state, int teamIndex)
        {
            var myTeam = state.GetTeam(teamIndex);
            var theirTeam = state.GetOpposingTeam(teamIndex);

            // HP advantage (weighted 50%)
            float myHpRatio = TeamHPRatio(myTeam);
            float theirHpRatio = TeamHPRatio(theirTeam);
            float hpScore = myHpRatio - theirHpRatio;

            // Alive count advantage (weighted 30%)
            float maxAlive = Math.Max(myTeam.chainlings.Count, theirTeam.chainlings.Count);
            float aliveScore = maxAlive > 0
                ? (myTeam.AliveCount - theirTeam.AliveCount) / maxAlive
                : 0f;

            // AP advantage (weighted 10%)
            float myAP = TeamTotalAP(myTeam);
            float theirAP = TeamTotalAP(theirTeam);
            float maxAP = Math.Max(myAP + theirAP, 1f);
            float apScore = (myAP - theirAP) / maxAP;

            // Stage advantage (weighted 10%)
            float myStages = TeamTotalPositiveStages(myTeam) - TeamTotalNegativeStages(myTeam);
            float theirStages = TeamTotalPositiveStages(theirTeam) - TeamTotalNegativeStages(theirTeam);
            float maxStages = Math.Max(Math.Abs(myStages) + Math.Abs(theirStages), 1f);
            float stageScore = (myStages - theirStages) / maxStages;

            return Math.Max(-1f, Math.Min(1f,
                hpScore * 0.50f +
                aliveScore * 0.30f +
                apScore * 0.10f +
                stageScore * 0.10f));
        }

        /// <summary>Sum of currentHP / EffectiveMaxHP across all alive Chainlings.</summary>
        public static float TeamHPRatio(TeamState team)
        {
            float total = 0f, current = 0f;
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                total += team.chainlings[i].EffectiveMaxHP;
                current += Math.Max(0, team.chainlings[i].currentHP);
            }
            return total > 0 ? current / total : 0f;
        }

        /// <summary>Sum of AP across all alive Chainlings on the team.</summary>
        public static float TeamTotalAP(TeamState team)
        {
            float total = 0;
            for (int i = 0; i < team.chainlings.Count; i++)
            {
                if (team.chainlings[i].isAlive)
                    total += team.chainlings[i].currentAP;
            }
            return total;
        }

        /// <summary>Sum of all positive stages across active Chainlings.</summary>
        public static float TeamTotalPositiveStages(TeamState team)
        {
            float total = 0;
            var actives = team.GetActiveChainlings();
            for (int i = 0; i < actives.Count; i++)
            {
                var c = actives[i];
                total += Math.Max(0, c.stageMeleeAtk) + Math.Max(0, c.stageMeleeDef) +
                         Math.Max(0, c.stageRangedAtk) + Math.Max(0, c.stageRangedDef) +
                         Math.Max(0, c.stageSpeed) + Math.Max(0, c.stageAccuracy) +
                         Math.Max(0, c.stageEvasion);
            }
            return total;
        }

        /// <summary>Sum of all negative stages (as positive number) across active Chainlings.</summary>
        public static float TeamTotalNegativeStages(TeamState team)
        {
            float total = 0;
            var actives = team.GetActiveChainlings();
            for (int i = 0; i < actives.Count; i++)
            {
                var c = actives[i];
                total += Math.Max(0, -c.stageMeleeAtk) + Math.Max(0, -c.stageMeleeDef) +
                         Math.Max(0, -c.stageRangedAtk) + Math.Max(0, -c.stageRangedDef) +
                         Math.Max(0, -c.stageSpeed) + Math.Max(0, -c.stageAccuracy) +
                         Math.Max(0, -c.stageEvasion);
            }
            return total;
        }

        /// <summary>
        /// Score a single Chainling's threat level (0-1).
        /// Used by AIs to pick targets.
        /// </summary>
        public static float ThreatScore(ChainlingInstance c)
        {
            if (c == null || !c.isAlive) return 0f;

            float hpFrac = (float)c.currentHP / Math.Max(1, c.EffectiveMaxHP);
            float atkPressure = (c.GetEffectiveStat(CoreStat.MeleeAtk) + c.GetEffectiveStat(CoreStat.RangedAtk)) / 2f;

            // Normalize attack to ~0-1 range (rough heuristic)
            float atkNorm = Math.Min(1f, atkPressure / 300f);

            // High threat = high atk AND still healthy
            return atkNorm * 0.6f + hpFrac * 0.4f;
        }

        /// <summary>
        /// Score how vulnerable a Chainling is (0-1, higher = easier to KO).
        /// Used by AIs to pick targets.
        /// </summary>
        public static float VulnerabilityScore(ChainlingInstance c)
        {
            if (c == null || !c.isAlive) return 0f;
            float hpFrac = (float)c.currentHP / Math.Max(1, c.EffectiveMaxHP);
            return 1f - hpFrac; // Lower HP = higher vulnerability
        }
    }
}
