using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public sealed class StarProgressionGrantService
    {
        private readonly SpeciesDatabaseSO _speciesDatabase;
        private readonly ChainlingProgressionRewardTablesSO _rewardTables;
        private readonly ChainlingProgressionEventCatalogSO _eventCatalog;
        private readonly IProgressionGrantPersistence _persistence;

        public StarProgressionGrantService(
            SpeciesDatabaseSO speciesDatabase,
            ChainlingProgressionRewardTablesSO rewardTables,
            ChainlingProgressionEventCatalogSO eventCatalog,
            IProgressionGrantPersistence persistence)
        {
            _speciesDatabase = speciesDatabase;
            _rewardTables = rewardTables != null
                ? rewardTables
                : ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            _eventCatalog = eventCatalog;
            _persistence = persistence;
        }

        public async Task<List<ProgressionGrantResult>> ApplyGrantsAsync(
            BattleResult result,
            BattleRewardSummary rewardSummary,
            string playerProfileId,
            string walletAddress,
            string authUserId)
        {
            var grants = new List<ProgressionGrantResult>();
            if (result == null ||
                rewardSummary?.chainlingRewards == null ||
                result.battleType != BattleType.PvE)
            {
                return grants;
            }

            for (int i = 0; i < rewardSummary.chainlingRewards.Count; i++)
            {
                ChainlingXpReward reward = rewardSummary.chainlingRewards[i];
                if (reward == null || reward.xpEarned <= 0)
                    continue;

                if (reward.progressionGrants == null)
                    reward.progressionGrants = new List<ProgressionGrantResult>();

                SpeciesDefinitionSO species = _speciesDatabase != null
                    ? _speciesDatabase.GetById(reward.speciesId)
                    : null;

                if (reward.starsCrossed != null)
                {
                    for (int j = 0; j < reward.starsCrossed.Count; j++)
                    {
                        ProgressionGrantResult grant = await RollAndPersistAsync(
                            result,
                            reward,
                            species,
                            ProgressionGrantEventType.StarUp,
                            reward.starsCrossed[j],
                            0,
                            playerProfileId,
                            walletAddress,
                            authUserId);
                        reward.progressionGrants.Add(grant);
                        grants.Add(grant);
                    }
                }

                if (reward.masteryCyclesCompleted != null)
                {
                    for (int j = 0; j < reward.masteryCyclesCompleted.Count; j++)
                    {
                        ProgressionGrantResult grant = await RollAndPersistAsync(
                            result,
                            reward,
                            species,
                            ProgressionGrantEventType.MasteryCycle,
                            CombatConstants.STAR_MAX,
                            reward.masteryCyclesCompleted[j],
                            playerProfileId,
                            walletAddress,
                            authUserId);
                        reward.progressionGrants.Add(grant);
                        grants.Add(grant);
                    }
                }
            }

            return grants;
        }

        private async Task<ProgressionGrantResult> RollAndPersistAsync(
            BattleResult result,
            ChainlingXpReward reward,
            SpeciesDefinitionSO species,
            ProgressionGrantEventType eventType,
            int starIndex,
            int masteryCycleIndex,
            string playerProfileId,
            string walletAddress,
            string authUserId)
        {
            int rollKey = eventType == ProgressionGrantEventType.StarUp
                ? starIndex
                : 1000 + masteryCycleIndex;
            string chainlingId = reward.backendChainlingId ?? "";

            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                starIndex,
                reward.isEntangled,
                reward.knownMoveIds,
                _eventCatalog);

            int gatePercent = eventType == ProgressionGrantEventType.StarUp
                ? ChainlingProgressionRollUtility.GetStarGatePercent(
                    starIndex,
                    reward.isEntangled,
                    _rewardTables,
                    pool.starGateBonusPercent)
                : ChainlingProgressionRollUtility.GetMasteryGatePercent(
                    reward.isEntangled,
                    _rewardTables,
                    pool.starGateBonusPercent);

            var grant = new ProgressionGrantResult
            {
                chainlingBackendId = chainlingId,
                instanceId = reward.instanceId,
                speciesId = reward.speciesId,
                chainlingDisplayName = reward.displayName,
                eventType = eventType,
                starIndex = starIndex,
                masteryCycleIndex = masteryCycleIndex,
                gatePercent = gatePercent,
                poolHadEligibleMoves = pool.TotalWeight > 0,
                outcome = ProgressionGrantOutcome.IssuancePending,
            };

            if (species == null)
            {
                grant.error = "species_not_found";
                grant.outcome = ProgressionGrantOutcome.IssuanceError;
                return grant;
            }

            if (!grant.poolHadEligibleMoves)
            {
                grant.error = "empty_progression_pool";
                grant.outcome = ProgressionGrantOutcome.IssuanceError;
                return grant;
            }

            var gateRng = ChainlingProgressionRollUtility.BuildDeterministicRng(result.seed, chainlingId, rollKey, 0);
            grant.gatePassed = eventType == ProgressionGrantEventType.StarUp
                ? ChainlingProgressionRollUtility.TryRollStarGate(starIndex, reward.isEntangled, _rewardTables, pool.starGateBonusPercent, gateRng)
                : ChainlingProgressionRollUtility.TryRollMasteryGate(reward.isEntangled, _rewardTables, pool.starGateBonusPercent, gateRng);

            if (!grant.gatePassed)
            {
                grant.outcome = ProgressionGrantOutcome.NoFolioIssued;
                return grant;
            }

            var poolRng = ChainlingProgressionRollUtility.BuildDeterministicRng(result.seed, chainlingId, rollKey, 1);
            ResolvedProgressionMoveEntry move = ChainlingProgressionRollUtility.RollMoveFromPool(pool, poolRng);
            if (move == null || string.IsNullOrWhiteSpace(move.moveId))
            {
                grant.error = "pool_roll_failed";
                grant.outcome = ProgressionGrantOutcome.IssuanceError;
                return grant;
            }

            grant.moveId = move.moveId;
            grant.moveName = move.displayName;
            grant.sourceEventId = move.sourceEventId;
            grant.idempotencyKey = BuildIdempotencyKey(result, chainlingId, eventType, starIndex, masteryCycleIndex);

            if (_persistence == null || !_persistence.IsAvailable)
            {
                grant.error = "progression_grant_persistence_unavailable";
                grant.outcome = ProgressionGrantOutcome.IssuanceError;
                return grant;
            }

            ProgressionGrantPersistenceResult persisted;
            try
            {
                persisted = await _persistence.GrantProgressionKnownMoveAsync(
                    new ProgressionGrantRequest
                    {
                        playerProfileId = playerProfileId,
                        walletAddress = walletAddress,
                        authUserId = authUserId,
                        chainlingBackendId = chainlingId,
                        moveId = grant.moveId,
                        sourceEventId = grant.sourceEventId,
                        idempotencyKey = grant.idempotencyKey,
                        eventType = eventType,
                        starIndex = starIndex,
                        masteryCycleIndex = masteryCycleIndex,
                        battleSeed = result.seed,
                    });
            }
            catch (System.Exception ex)
            {
                grant.error = string.IsNullOrWhiteSpace(ex.Message) ? "progression_grant_exception" : ex.Message;
                grant.outcome = ProgressionGrantOutcome.IssuanceError;
                return grant;
            }

            grant.grantSucceeded = persisted != null && persisted.success;
            grant.alreadyApplied = persisted != null && persisted.alreadyApplied;
            grant.error = persisted != null ? persisted.error : "invalid_progression_grant_response";
            if (persisted != null)
            {
                grant.moveItemId = persisted.moveItemId;
                grant.ownedCopyCount = Mathf.Max(0, persisted.ownedCopyCount);
                if (!string.IsNullOrWhiteSpace(persisted.moveId))
                    grant.moveId = persisted.moveId;
            }

            grant.outcome = grant.grantSucceeded
                ? ProgressionGrantOutcome.FolioIssued
                : ProgressionGrantOutcome.IssuanceError;

            return grant;
        }

        private static string BuildIdempotencyKey(
            BattleResult result,
            string chainlingId,
            ProgressionGrantEventType eventType,
            int starIndex,
            int masteryCycleIndex)
        {
            string battlePart = result != null && !string.IsNullOrWhiteSpace(result.forensicHash)
                ? result.forensicHash
                : $"seed_{(result != null ? result.seed : 0)}";
            string eventPart = eventType == ProgressionGrantEventType.StarUp
                ? $"star_{starIndex}"
                : $"mastery_{masteryCycleIndex}";
            return $"progression_grant:{battlePart}:{chainlingId}:{eventPart}";
        }
    }
}
