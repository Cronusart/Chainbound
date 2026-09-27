using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Prepares / finalizes Metaplex Core bag-item burns via Supabase.
    /// Wallet signing uses <see cref="IChainTxOrchestrator"/> when unsigned bytes are returned.
    /// </summary>
    public class CoreAssetConsumeService : MonoBehaviour, ICoreAssetConsumeService
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool allowLocalStubWhenOffline = true;

        public void Configure(SupabaseConfigSO config)
        {
            supabaseConfig = config;
        }

        public async Task<CoreItemConsumePrepareResult> PrepareConsumeAsync(
            string playerProfileId,
            string walletAddress,
            string itemId,
            string assetId,
            string reason)
        {
            string profileId = (playerProfileId ?? "").Trim();
            string wallet = (walletAddress ?? "").Trim();
            string normalizedItemId = (itemId ?? "").Trim();
            string normalizedAssetId = (assetId ?? "").Trim();

            if (string.IsNullOrEmpty(normalizedItemId))
                return CoreItemConsumePrepareResult.Failed("item_id_required");
            if (string.IsNullOrEmpty(normalizedAssetId))
                return CoreItemConsumePrepareResult.Failed("core_asset_id_required");

            if (supabaseConfig == null)
            {
                if (!allowLocalStubWhenOffline)
                    return CoreItemConsumePrepareResult.Failed("supabase_config_missing");

                Log($"Stub prepare consume item={normalizedItemId} asset={normalizedAssetId}");
                return CoreItemConsumePrepareResult.Ok(
                    consumeIntentId: $"stub-intent-{Guid.NewGuid():N}",
                    itemId: normalizedItemId,
                    assetId: normalizedAssetId,
                    unsignedTxBase64: "",
                    cluster: "devnet",
                    actionSummary: $"Stub consume {normalizedItemId} (no chain tx).");
            }

            try
            {
                string json = JsonUtility.ToJson(new PrepareRequest
                {
                    player_profile_id = profileId,
                    wallet_address = wallet,
                    item_id = normalizedItemId,
                    asset_id = normalizedAssetId,
                    reason = reason ?? "",
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_prepare_core_item_consume",
                    json);

                if (!response.IsSuccess)
                {
                    LogWarning($"prepare returned {response.statusCode}: {response.body}");
                    return CoreItemConsumePrepareResult.Failed($"prepare_http_{response.statusCode}");
                }

                PrepareResponse payload = JsonUtility.FromJson<PrepareResponse>(response.body);
                if (payload == null || !payload.success)
                    return CoreItemConsumePrepareResult.Failed(payload != null ? payload.error : "prepare_invalid_payload");

                return CoreItemConsumePrepareResult.Ok(
                    payload.consume_intent_id,
                    payload.item_id,
                    payload.asset_id,
                    payload.unsigned_tx_base64,
                    payload.cluster,
                    payload.action_summary,
                    payload.estimated_fee_lamports);
            }
            catch (Exception ex)
            {
                LogWarning($"PrepareConsumeAsync failed: {ex.Message}");
                return CoreItemConsumePrepareResult.Failed("prepare_exception");
            }
        }

        public async Task<CoreItemConsumeFinalizeResult> FinalizeConsumeAsync(
            string playerProfileId,
            string walletAddress,
            string consumeIntentId,
            string txSignature)
        {
            string intentId = (consumeIntentId ?? "").Trim();
            if (string.IsNullOrEmpty(intentId))
                return CoreItemConsumeFinalizeResult.Failed("consume_intent_id_required");

            if (supabaseConfig == null)
            {
                if (!allowLocalStubWhenOffline)
                    return CoreItemConsumeFinalizeResult.Failed("supabase_config_missing");

                Log($"Stub finalize consume intent={intentId} sig={txSignature}");
                return CoreItemConsumeFinalizeResult.Ok(intentId, txSignature ?? "stub-signature");
            }

            try
            {
                string json = JsonUtility.ToJson(new FinalizeRequest
                {
                    player_profile_id = playerProfileId ?? "",
                    wallet_address = walletAddress ?? "",
                    consume_intent_id = intentId,
                    tx_signature = txSignature ?? "",
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "profile_finalize_core_item_consume",
                    json);

                if (!response.IsSuccess)
                    return CoreItemConsumeFinalizeResult.Failed($"finalize_http_{response.statusCode}");

                FinalizeResponse payload = JsonUtility.FromJson<FinalizeResponse>(response.body);
                if (payload == null || !payload.success)
                    return CoreItemConsumeFinalizeResult.Failed(payload != null ? payload.error : "finalize_invalid_payload");

                return CoreItemConsumeFinalizeResult.Ok(payload.consume_intent_id, payload.tx_signature);
            }
            catch (Exception ex)
            {
                LogWarning($"FinalizeConsumeAsync failed: {ex.Message}");
                return CoreItemConsumeFinalizeResult.Failed("finalize_exception");
            }
        }

        /// <summary>
        /// Full stub/local path: prepare → optional orchestrator submit → finalize.
        /// When unsigned bytes are empty (stub), skips chain submit.
        /// </summary>
        public async Task<CoreItemConsumeFinalizeResult> ConsumeAsync(
            string playerProfileId,
            string walletAddress,
            string itemId,
            string assetId,
            string reason)
        {
            CoreItemConsumePrepareResult prepared = await PrepareConsumeAsync(
                playerProfileId,
                walletAddress,
                itemId,
                assetId,
                reason);
            if (!prepared.success)
                return CoreItemConsumeFinalizeResult.Failed(prepared.error);

            string signature = "";
            if (!string.IsNullOrWhiteSpace(prepared.unsignedTxBase64))
            {
                IChainTxOrchestrator orch = SolanaServiceBridge.Instance != null
                    ? SolanaServiceBridge.Instance.TxOrchestrator
                    : null;
                if (orch == null)
                    return CoreItemConsumeFinalizeResult.Failed("tx_orchestrator_unavailable");

                byte[] txBytes;
                try
                {
                    txBytes = Convert.FromBase64String(prepared.unsignedTxBase64);
                }
                catch
                {
                    return CoreItemConsumeFinalizeResult.Failed("unsigned_tx_invalid_base64");
                }

                string idempotency = $"core_consume_{prepared.consumeIntentId}";
                TxResult txResult = await orch.SubmitAsync(idempotency, txBytes);
                if (txResult == null ||
                    (txResult.status != TxStatus.Confirmed && txResult.status != TxStatus.Finalized))
                {
                    return CoreItemConsumeFinalizeResult.Failed(
                        txResult != null ? $"tx_{txResult.status}" : "tx_submit_failed");
                }

                signature = txResult.txSignature ?? "";
            }

            return await FinalizeConsumeAsync(
                playerProfileId,
                walletAddress,
                prepared.consumeIntentId,
                signature);
        }

        private void Log(string message)
        {
            if (enableDebugLogs)
                Debug.Log($"[CoreAssetConsumeService] {message}", this);
        }

        private void LogWarning(string message)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[CoreAssetConsumeService] {message}", this);
        }

        [Serializable]
        private class PrepareRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string item_id;
            public string asset_id;
            public string reason;
        }

        [Serializable]
        private class PrepareResponse
        {
            public bool success;
            public string error;
            public string consume_intent_id;
            public string item_id;
            public string asset_id;
            public string unsigned_tx_base64;
            public string cluster;
            public string action_summary;
            public long estimated_fee_lamports;
        }

        [Serializable]
        private class FinalizeRequest
        {
            public string player_profile_id;
            public string wallet_address;
            public string consume_intent_id;
            public string tx_signature;
        }

        [Serializable]
        private class FinalizeResponse
        {
            public bool success;
            public string error;
            public string consume_intent_id;
            public string tx_signature;
        }
    }
}
