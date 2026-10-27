using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Defensive AI: prioritizes survival and team sustain.
    /// - Always prefers a lethal blow (KO an enemy = permanent damage reduction)
    /// - Swaps out critically-low-HP Chainlings to preserve them
    /// - Heals/supports allies when they're in real danger (below 40% HP)
    /// - Buffs team only when the situation is stable (no ally in danger)
    /// - Falls back to AP-efficient attacking with highest-threat targeting
    /// Good for tanky species like Gilarime and wall-builders.
    /// </summary>
    public class DefensiveAI : ICombatAI
    {
        public string PersonalityName => "Defensive";

        private const float SWAP_HP_THRESHOLD = 0.20f;   // Swap out when below 20% HP (was 25% â€” swapping wastes a turn)
        private const float HEAL_HP_THRESHOLD = 0.40f;    // Prioritize heals when ally below 40% HP (was 60% â€” triggered too early)
        private const float BUFF_HP_THRESHOLD = 0.55f;    // Only buff if ALL allies are above 55% HP (stable situation)

        public List<CombatAction> GenerateActions(CombatState state, int teamIndex, CombatRng rng)
        {
            var myTeam = state.GetTeam(teamIndex);
            var enemyTeam = state.GetOpposingTeam(teamIndex);
            var actions = new List<CombatAction>();
            var queuedBySlot = new Dictionary<int, CombatAction>();

            for (int slot = 0; slot < myTeam.activeIndices.Count; slot++)
            {
                var c = myTeam.GetActiveInSlot(slot);
                if (c == null || !c.isAlive)
                {
                    actions.Add(CombatAction.Pass(teamIndex));
                    continue;
                }

                float myHpFrac = (float)c.currentHP / Math.Max(1, c.EffectiveMaxHP);
                var legalActions = AIHelpers.GetLegalActions(
                    c, myTeam, enemyTeam, teamIndex, state, queuedBySlot: queuedBySlot, actorSlot: slot);

                // Priority 1: If we can KO an enemy this turn, always take the kill.
                // Removing an enemy's damage output is the best form of defense.
                CombatAction lethalAction = FindLethalBlow(c, legalActions, enemyTeam, teamIndex);
                if (lethalAction.actionType != CombatActionType.Pass)
                {
                    actions.Add(lethalAction);
                    queuedBySlot[slot] = lethalAction;
                    goto nextSlot;
                }

                if (AIHelpers.ShouldBankForGreaterBlock(
                        c, myTeam, enemyTeam, state, queuedBySlot, slot))
                {
                    CombatAction bank = CombatAction.Pass(teamIndex);
                    actions.Add(bank);
                    queuedBySlot[slot] = bank;
                    goto nextSlot;
                }

                // Priority 2: Swap out if critically low HP and bench available
                if (myHpFrac < SWAP_HP_THRESHOLD)
                {
                    for (int i = 0; i < legalActions.Count; i++)
                    {
                        if (legalActions[i].action.actionType == CombatActionType.Swap)
                        {
                            actions.Add(legalActions[i].action);
                            queuedBySlot[slot] = legalActions[i].action;
                            goto nextSlot;
                        }
                    }
                }

                // Priority 3: Heal/support ally if any ally is in real danger
                bool allyInDanger = false;
                var actives = myTeam.GetActiveChainlings();
                for (int a = 0; a < actives.Count; a++)
                {
                    float allyHp = (float)actives[a].currentHP / Math.Max(1, actives[a].EffectiveMaxHP);
                    if (allyHp < HEAL_HP_THRESHOLD)
                    {
                        allyInDanger = true;
                        break;
                    }
                }

                if (allyInDanger)
                {
                    CombatAction supportAction = FindBestSupport(c, legalActions, teamIndex);
                    if (supportAction.actionType != CombatActionType.Pass)
                    {
                        actions.Add(supportAction);
                        queuedBySlot[slot] = supportAction;
                        goto nextSlot;
                    }
                }

                // Priority 4: Buff team only when situation is stable (all allies healthy)
                bool teamStable = true;
                for (int a = 0; a < actives.Count; a++)
                {
                    float allyHp = (float)actives[a].currentHP / Math.Max(1, actives[a].EffectiveMaxHP);
                    if (allyHp < BUFF_HP_THRESHOLD)
                    {
                        teamStable = false;
                        break;
                    }
                }

                if (teamStable)
                {
                    CombatAction buffAction = FindBestBuff(c, legalActions, teamIndex);
                    if (buffAction.actionType != CombatActionType.Pass)
                    {
                        actions.Add(buffAction);
                        queuedBySlot[slot] = buffAction;
                        goto nextSlot;
                    }
                }

                // Priority 5: Attack the highest-threat enemy
                CombatAction attackAction = FindBestAttack(c, legalActions, enemyTeam, teamIndex);
                actions.Add(attackAction);
                queuedBySlot[slot] = attackAction;

                nextSlot:;
            }

            return actions;
        }

        /// <summary>
        /// Check if any damaging move can KO an active enemy.
        /// Killing an enemy removes their damage output permanently â€”
        /// this is the most impactful "defensive" play possible.
        /// </summary>
        private CombatAction FindLethalBlow(ChainlingInstance actor,
            List<ScoredAction> legalActions, TeamState enemyTeam, int teamIndex)
        {
            var enemies = enemyTeam.GetActiveChainlings();
            if (enemies.Count == 0)
                return CombatAction.Pass(teamIndex);

            // Find the lowest-HP enemy that we might be able to KO
            ChainlingInstance weakestEnemy = null;
            for (int e = 0; e < enemies.Count; e++)
            {
                if (!enemies[e].isAlive) continue;
                if (weakestEnemy == null || enemies[e].currentHP < weakestEnemy.currentHP)
                    weakestEnemy = enemies[e];
            }
            if (weakestEnemy == null)
                return CombatAction.Pass(teamIndex);

            // Check each damaging move for a potential KO
            float bestLethalScore = -1f;
            CombatAction bestLethal = CombatAction.Pass(teamIndex);

            for (int i = 0; i < legalActions.Count; i++)
            {
                if (legalActions[i].action.actionType != CombatActionType.UseMove) continue;
                var move = actor.moves[legalActions[i].action.moveIndex];
                if (!AIHelpers.IsDamaging(move)) continue;

                float estDmg = AIHelpers.EstimateDamage(actor, move);
                if (estDmg >= weakestEnemy.currentHP)
                {
                    // Prefer the most AP-efficient lethal option
                    float score = AIHelpers.DamagePerAP(actor, move);
                    if (score > bestLethalScore)
                    {
                        bestLethalScore = score;
                        bestLethal = legalActions[i].action;
                    }
                }
            }

            return bestLethal;
        }

        private CombatAction FindBestSupport(ChainlingInstance actor,
            List<ScoredAction> legalActions, int teamIndex)
        {
            float bestScore = float.MinValue;
            CombatAction best = CombatAction.Pass(teamIndex);
            for (int i = 0; i < legalActions.Count; i++)
            {
                if (legalActions[i].action.actionType != CombatActionType.UseMove) continue;
                var move = actor.moves[legalActions[i].action.moveIndex];
                if (AIHelpers.IsSupport(move) || move.tags.Contains("HEAL"))
                {
                    float score = CombatWallStateUtility.MovePlacesBlock(move)
                        ? legalActions[i].score
                        : 0f;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = legalActions[i].action;
                    }
                }
            }
            return bestScore >= 0f ? best : CombatAction.Pass(teamIndex);
        }

        private CombatAction FindBestBuff(ChainlingInstance actor,
            List<ScoredAction> legalActions, int teamIndex)
        {
            for (int i = 0; i < legalActions.Count; i++)
            {
                if (legalActions[i].action.actionType != CombatActionType.UseMove) continue;
                var move = actor.moves[legalActions[i].action.moveIndex];

                // Look for self-targeting utility with positive stage changes
                if (AIHelpers.IsUtility(move) && move.targeting == MoveTargeting.Self)
                {
                    for (int s = 0; s < move.stageChanges.Count; s++)
                    {
                        if (move.stageChanges[s].stages > 0 && move.stageChanges[s].targetsSelf)
                            return legalActions[i].action;
                    }
                }
            }
            return CombatAction.Pass(teamIndex);
        }

        private CombatAction FindBestAttack(ChainlingInstance actor,
            List<ScoredAction> legalActions, TeamState enemyTeam, int teamIndex)
        {
            float bestScore = -1f;
            CombatAction best = CombatAction.Pass(teamIndex);

            for (int i = 0; i < legalActions.Count; i++)
            {
                if (legalActions[i].action.actionType != CombatActionType.UseMove) continue;
                var move = actor.moves[legalActions[i].action.moveIndex];

                if (!AIHelpers.IsDamaging(move)) continue;

                // Prefer AP-efficient moves (conserve resources)
                float score = AIHelpers.DamagePerAP(actor, move);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = legalActions[i].action;
                }
            }

            return best;
        }
    }
}
