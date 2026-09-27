using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public class StandardBattleRewardCalculator : IBattleRewardCalculator
    {
        private readonly PostBattleRewardsConfigSO _rewardsConfig;
        private readonly ProgressionConfigSO _progressionConfig;
        private readonly FavorConfigSO _favorConfig;
        private readonly System.Random _rng;

        public StandardBattleRewardCalculator(
            PostBattleRewardsConfigSO rewardsConfig,
            ProgressionConfigSO progressionConfig = null,
            FavorConfigSO favorConfig = null,
            System.Random rng = null)
        {
            _rewardsConfig = rewardsConfig != null
                ? rewardsConfig
                : BalanceConfigRuntime.PostBattleRewards;
            _progressionConfig = progressionConfig != null
                ? progressionConfig
                : BalanceConfigRuntime.Progression;
            _favorConfig = favorConfig != null ? favorConfig : FavorConfigSO.CreateRuntimeDefault();
            _rng = rng ?? new System.Random();
        }

        public BattleRewardSummary Calculate(BattleResult result)
        {
            var summary = new BattleRewardSummary
            {
                playerWon = result != null && result.PlayerWon,
                difficulty = result != null ? result.encounterDifficulty : EncounterDifficulty.Standard,
            };

            if (result == null)
                return summary;

            bool playerLostPve = result.battleType == BattleType.PvE
                && !result.PlayerWon
                && !result.IsDraw
                && !result.wasFled
                && !result.EndedByCapture;
            if (playerLostPve)
                summary.ramPenalty = _rewardsConfig.GetDefeatRamPenalty(result.playerLevel);

            bool captureRewards = result.DidCapture && !result.wasFled;
            if (!result.PlayerWon && !captureRewards && !_rewardsConfig.defeatGrantsRewards)
                return summary;

            ResolveRewardModifiers(
                result,
                out float xpMult,
                out float ramMult,
                out float rareDropChanceBonus,
                out List<string> bonusItems);

            summary.xpMultiplier = xpMult;
            summary.ramMultiplier = ramMult;
            if (bonusItems != null)
                summary.bonusItemIds.AddRange(bonusItems);

            int baseXpPool = (result.totalEnemyKOs * _rewardsConfig.GetKoXp(result.encounterDifficulty))
                + _rewardsConfig.GetCompletionBonus(result.encounterDifficulty);
            baseXpPool = Mathf.Max(0, Mathf.RoundToInt(baseXpPool * xpMult));

            summary.ramEarned = Mathf.Max(0, Mathf.RoundToInt(_rewardsConfig.GetRamReward(result.encounterDifficulty) * ramMult));

            if (result.playerParticipants == null)
                return summary;

            for (int i = 0; i < result.playerParticipants.Count; i++)
            {
                var participant = result.playerParticipants[i];
                if (participant == null)
                    continue;

                float shareMultiplier = participant.wasBenched
                    ? _rewardsConfig.benchXpShare
                    : _rewardsConfig.activeXpShare;

                if (participant.wasKOd && !_rewardsConfig.kodReceivesXp)
                    shareMultiplier = 0f;

                if (IsCapturedParticipant(result, participant) && !_rewardsConfig.capturedReceivesXp)
                    shareMultiplier = 0f;

                int xpEarned = Mathf.Max(0, Mathf.RoundToInt(baseXpPool * shareMultiplier));
                int previousMasteryXp = Mathf.Max(0, participant.masteryXp);
                int newMasteryXp = Mathf.Max(0, previousMasteryXp + xpEarned);
                int rebirthCount = ChainlingProgressionUtility.NormalizeRebirthCount(participant.rebirthCount);
                int previousStars = ChainlingProgressionUtility.NormalizeStars(
                    StarProgressionUtility.GetStarCount(previousMasteryXp, rebirthCount, _progressionConfig));
                int newStars = previousStars;
                var starsCrossed = StarProgressionUtility.DetectStarUps(previousMasteryXp, xpEarned, rebirthCount, _progressionConfig);
                var masteryCyclesCompleted = StarProgressionUtility.DetectMasteryCycles(previousMasteryXp, xpEarned, rebirthCount, _progressionConfig);
                if (xpEarned > 0)
                {
                    int projectedStars = StarProgressionUtility.GetStarCount(newMasteryXp, rebirthCount, _progressionConfig);
                    newStars = Mathf.Max(previousStars, ChainlingProgressionUtility.NormalizeStars(projectedStars));
                }

                summary.chainlingRewards.Add(new ChainlingXpReward
                {
                    instanceId = participant.instanceId,
                    backendChainlingId = participant.backendChainlingId,
                    speciesId = participant.speciesId,
                    displayName = participant.displayName,
                    xpEarned = xpEarned,
                    shareMultiplier = shareMultiplier,
                    wasBenchOnly = participant.wasBenched,
                    wasKOd = participant.wasKOd,
                    isEntangled = participant.isEntangled,
                    rebirthCount = rebirthCount,
                    previousMasteryXp = previousMasteryXp,
                    newMasteryXp = newMasteryXp,
                    previousStars = previousStars,
                    newStars = newStars,
                    gainedStar = newStars > previousStars,
                    starsCrossed = starsCrossed,
                    masteryCyclesCompleted = masteryCyclesCompleted,
                    knownMoveIds = participant.knownMoveIds != null
                        ? new List<string>(participant.knownMoveIds)
                        : new List<string>(),
                });
            }

            TryGrantRareConsumable(result, summary, rareDropChanceBonus);
            return summary;
        }

        private static bool IsCapturedParticipant(BattleResult result, BattleParticipant participant)
        {
            if (result == null || participant == null || !result.DidCapture)
                return false;

            if (!string.IsNullOrWhiteSpace(result.capturedBackendChainlingId)
                && string.Equals(result.capturedBackendChainlingId, participant.backendChainlingId, StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(result.capturedChainlingPda)
                && (string.Equals(result.capturedChainlingPda, participant.instanceId, StringComparison.Ordinal)
                    || string.Equals(result.capturedChainlingPda, participant.backendChainlingId, StringComparison.Ordinal)))
            {
                return true;
            }

            return false;
        }

        private void ResolveRewardModifiers(
            BattleResult result,
            out float xpMult,
            out float ramMult,
            out float rareDropChanceBonus,
            out List<string> bonusItems)
        {
            xpMult = 1f;
            ramMult = 1f;
            rareDropChanceBonus = 0f;
            bonusItems = new List<string>();

            var service = EncounterModifierService.Instance;
            if (service == null || result == null)
                return;

            string areaId = result.areaId ?? "";
            string zoneId = result.zoneId ?? "";
            TimeSegment segment = result.timeSegment;
            EncounterDifficulty difficulty = result.encounterDifficulty;

            var xpOps = service.GetMatchingOps(areaId, zoneId, null, segment, difficulty, EncounterModifierOpType.RewardXpMultiplier);
            var ramOps = service.GetMatchingOps(areaId, zoneId, null, segment, difficulty, EncounterModifierOpType.RewardRamMultiplier);
            var rareOps = service.GetMatchingOps(areaId, zoneId, null, segment, difficulty, EncounterModifierOpType.RewardRareDropChance);
            var itemOps = service.GetMatchingOps(areaId, zoneId, null, segment, difficulty, EncounterModifierOpType.RewardBonusItem);

            xpMult = Mathf.Max(0f, service.ApplyNumericOps(1f, xpOps));
            ramMult = Mathf.Max(0f, service.ApplyNumericOps(1f, ramOps));
            rareDropChanceBonus = Mathf.Max(0f, service.ApplyNumericOps(0f, rareOps));

            for (int i = 0; i < itemOps.Count; i++)
            {
                var op = itemOps[i];
                if (op == null || string.IsNullOrWhiteSpace(op.targetId))
                    continue;
                // value acts as drop chance when mode is Multiply/Add on a roll; Set/Add with value>=1 grants.
                bool grant = op.mode == EncounterModifierOpMode.Set
                    ? op.value >= 1f
                    : _rng.NextDouble() < Mathf.Clamp01(op.mode == EncounterModifierOpMode.Add ? op.value : op.value);
                if (grant)
                    bonusItems.Add(op.targetId.Trim());
            }
        }

        private void TryGrantRareConsumable(BattleResult result, BattleRewardSummary summary, float rareDropChanceBonus)
        {
            if (result == null || !result.PlayerWon || result.battleType != BattleType.PvE)
                return;

            int favorScore = FavorRuntime.CurrentScore;
            // Include server FavorBonus ops stamped onto the encounter via area/zone scope.
            var favorOps = EncounterModifierService.Instance != null
                ? EncounterModifierService.Instance.GetMatchingOps(
                    result.areaId, result.zoneId, null, result.timeSegment, result.encounterDifficulty,
                    EncounterModifierOpType.FavorBonus)
                : null;
            if (favorOps != null)
            {
                for (int i = 0; i < favorOps.Count; i++)
                {
                    if (favorOps[i] != null)
                        favorScore += Mathf.RoundToInt(favorOps[i].value);
                }
            }

            float chance = _favorConfig.RareConsumableChance(favorScore) + rareDropChanceBonus;
            chance = Mathf.Clamp01(chance);
            if (chance <= 0f || _rng.NextDouble() >= chance)
                return;

            string[] ids = _favorConfig.rareConsumableItemIds;
            if (ids == null || ids.Length == 0)
                return;

            string itemId = ids[_rng.Next(ids.Length)];
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            summary.grantedRareConsumable = true;
            summary.rareConsumableItemId = itemId.Trim();
        }
    }
}
