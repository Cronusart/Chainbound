using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabaseBattleOutcomePersistence : MonoBehaviour, IBattleRewardPersistence
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        public bool IsAvailable => supabaseConfig != null;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public Task<bool> PersistRewardsAsync(BattleRewardSummary summary, string walletAddress)
        {
            if (enableDebugLogs)
                Debug.Log("[SupabaseBattleOutcomePersistence] Reward-only persistence skipped; battle outcome bundle is required for PvE stats.");
            return Task.FromResult(true);
        }

        public async Task<bool> PersistOutcomeAsync(BattleOutcomeBundle outcome, string walletAddress)
        {
            if (supabaseConfig == null)
            {
                Debug.LogWarning("[SupabaseBattleOutcomePersistence] SupabaseConfigSO was not assigned.", this);
                return false;
            }

            if (outcome == null || outcome.result == null)
                return true;

            if (outcome.result.battleType != BattleType.PvE)
                return true;

            OutcomeRequest request = BuildRequest(outcome, walletAddress);
            if (request.chainlings == null || request.chainlings.Count == 0)
            {
                if (enableDebugLogs)
                    Debug.Log("[SupabaseBattleOutcomePersistence] No Chainling PvE contributions to persist.");
                return true;
            }

            string json = JsonUtility.ToJson(request);
            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                supabaseConfig,
                "profile_apply_pve_battle_outcome",
                json);

            if (!response.IsSuccess)
            {
                Debug.LogWarning($"[SupabaseBattleOutcomePersistence] profile_apply_pve_battle_outcome returned {response.statusCode}: {response.body}", this);
                return false;
            }

            OutcomeResponse envelope = JsonUtility.FromJson<OutcomeResponse>(response.body);
            if (envelope == null || !envelope.success)
            {
                string error = envelope != null ? envelope.error : "invalid_payload";
                Debug.LogWarning($"[SupabaseBattleOutcomePersistence] outcome persistence failed: {error}", this);
                return false;
            }

            if (enableDebugLogs)
                Debug.Log($"[SupabaseBattleOutcomePersistence] Persisted {request.chainlings.Count} Chainling PvE contribution(s).", this);

            return true;
        }

        public static OutcomeRequest BuildRequest(BattleOutcomeBundle outcome, string walletAddress)
        {
            GameSessionContext session = GameSessionContext.Instance;
            BattleResult result = outcome.result;
            BattleRewardSummary rewards = outcome.rewardSummary;

            var request = new OutcomeRequest
            {
                player_profile_id = session != null ? session.PlayerProfileId : "",
                wallet_address = !string.IsNullOrWhiteSpace(walletAddress)
                    ? walletAddress.Trim()
                    : session != null ? session.WalletAddress : "",
                auth_user_id = session != null ? session.AuthUserId : "",
                battle_seed = result.seed,
                battle_type = result.battleType.ToString(),
                player_won = result.PlayerWon,
                is_draw = result.IsDraw,
                was_fled = result.wasFled,
                // PvE is client-resolved, so RAM remains preview-only until an
                // authoritative battle verifier exists.
                ram_earned = 0,
                ram_penalty = 0,
                forensic_hash = !string.IsNullOrWhiteSpace(outcome.forensicHash)
                    ? outcome.forensicHash
                    : result.forensicHash ?? string.Empty,
                config_hash = !string.IsNullOrWhiteSpace(outcome.configHash)
                    ? outcome.configHash
                    : result.configHash ?? string.Empty,
                chainlings = new List<ChainlingOutcomePayload>(),
            };

            Dictionary<string, ChainlingXpReward> rewardsByBackendId = BuildRewardsByBackendId(rewards);
            var payloadsByBackendId = new Dictionary<string, ChainlingOutcomePayload>(StringComparer.Ordinal);
            List<ChainlingBattleStatContribution> contributions = outcome.chainlingContributions ?? new List<ChainlingBattleStatContribution>();

            for (int i = 0; i < contributions.Count; i++)
            {
                ChainlingBattleStatContribution contribution = contributions[i];
                if (contribution == null || string.IsNullOrWhiteSpace(contribution.chainlingBackendId))
                    continue;

                ChainlingPveStatDelta delta = contribution.delta ?? new ChainlingPveStatDelta();
                rewardsByBackendId.TryGetValue(contribution.chainlingBackendId, out ChainlingXpReward reward);

                payloadsByBackendId[contribution.chainlingBackendId] = new ChainlingOutcomePayload
                {
                    chainling_id = contribution.chainlingBackendId,
                    instance_id = contribution.instanceId,
                    display_name = contribution.displayName,
                    total_victories = Math.Max(0, delta.totalVictories),
                    xp_earned_lifetime = Math.Max(0, delta.xpEarnedLifetime),
                    progression_moves_granted = 0,
                    damage_dealt = Math.Max(0, delta.damageDealt),
                    damage_taken = Math.Max(0, delta.damageTaken),
                    damage_mitigated = Math.Max(0, delta.damageMitigated),
                    healing_total = Math.Max(0, delta.healingTotal),
                    enemy_kos = Math.Max(0, delta.enemyKos),
                    legacy_times_kod = Math.Max(0, contribution.legacyTimesKod),
                    previous_stars = reward != null ? Math.Max(0, reward.previousStars) : 0,
                    new_stars = reward != null ? Math.Max(0, reward.newStars) : 0,
                    gained_star = reward != null && reward.gainedStar,
                };
            }

            MergeParticipantHp(result?.playerParticipants, payloadsByBackendId);

            request.chainlings.AddRange(payloadsByBackendId.Values);
            return request;
        }

        private static void MergeParticipantHp(
            List<BattleParticipant> participants,
            Dictionary<string, ChainlingOutcomePayload> payloadsByBackendId)
        {
            if (participants == null || payloadsByBackendId == null)
                return;

            for (int i = 0; i < participants.Count; i++)
            {
                BattleParticipant participant = participants[i];
                if (participant == null || string.IsNullOrWhiteSpace(participant.backendChainlingId))
                    continue;

                if (!payloadsByBackendId.TryGetValue(participant.backendChainlingId, out ChainlingOutcomePayload payload))
                {
                    payload = new ChainlingOutcomePayload
                    {
                        chainling_id = participant.backendChainlingId,
                        instance_id = participant.instanceId,
                        display_name = participant.displayName,
                    };
                    payloadsByBackendId[participant.backendChainlingId] = payload;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(payload.instance_id))
                        payload.instance_id = participant.instanceId;
                    if (string.IsNullOrWhiteSpace(payload.display_name))
                        payload.display_name = participant.displayName;
                }

                payload.sync_current_hp = true;
                payload.current_hp = Math.Max(0, participant.endCurrentHp);
            }
        }

        private static Dictionary<string, ChainlingXpReward> BuildRewardsByBackendId(BattleRewardSummary rewards)
        {
            var map = new Dictionary<string, ChainlingXpReward>(StringComparer.Ordinal);
            if (rewards?.chainlingRewards == null)
                return map;

            for (int i = 0; i < rewards.chainlingRewards.Count; i++)
            {
                ChainlingXpReward reward = rewards.chainlingRewards[i];
                if (reward != null && !string.IsNullOrWhiteSpace(reward.backendChainlingId))
                    map[reward.backendChainlingId] = reward;
            }

            return map;
        }

        [Serializable]
        public class OutcomeRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string auth_user_id;
            public int battle_seed;
            public string battle_type;
            public bool player_won;
            public bool is_draw;
            public bool was_fled;
            public int ram_earned;
            public int ram_penalty;
            public string forensic_hash;
            public string config_hash;
            public List<ChainlingOutcomePayload> chainlings = new List<ChainlingOutcomePayload>();
        }

        [Serializable]
        public class ChainlingOutcomePayload
        {
            public string chainling_id;
            public string instance_id;
            public string display_name;
            public int total_victories;
            public long xp_earned_lifetime;
            public int progression_moves_granted;
            public long damage_dealt;
            public long damage_taken;
            public long damage_mitigated;
            public long healing_total;
            public int enemy_kos;
            public int legacy_times_kod;
            public int previous_stars;
            public int new_stars;
            public bool gained_star;
            public bool sync_current_hp;
            public int current_hp;
        }

        [Serializable]
        private class OutcomeResponse
        {
            public bool success;
            public string error;
        }
    }
}
