using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Devnet transport for the ownership spine. Supabase remains the profile/DAS authority;
    /// Unity only signs a server-prepared Stamp transaction after local RPC simulation.
    /// </summary>
    public sealed class SupabaseOwnershipSpineGateway : MonoBehaviour, IOwnershipSpineGateway
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private SolanaConfigSO solanaConfig;
        [SerializeField] private bool enableDevnetOwnershipSpine;

        private IWalletService wallet;
        private IRpcService rpc;
        private IChainTxOrchestrator txOrchestrator;
        private readonly Dictionary<string, PreparedStampPayload> prepared = new Dictionary<string, PreparedStampPayload>(StringComparer.Ordinal);
        private readonly Dictionary<string, PreparedRamGrantPayload> preparedRamGrants = new Dictionary<string, PreparedRamGrantPayload>(StringComparer.Ordinal);

        public bool IsWalletConnected
        {
            get
            {
                ResolveChainDependencies();
                return wallet != null && wallet.IsConnected;
            }
        }

        public void Configure(
            SupabaseConfigSO backend,
            SolanaConfigSO chain,
            IWalletService walletService,
            IRpcService rpcService,
            IChainTxOrchestrator orchestrator = null)
        {
            supabaseConfig = PlatformContext.ResolveSupabaseConfig(backend);
            solanaConfig = PlatformContext.ResolveSolanaConfig(chain);
            wallet = walletService;
            rpc = rpcService;
            txOrchestrator = orchestrator;
        }

        public async Task<OwnershipKnownRollResult> RollKnownAsync(string idempotencyKey)
        {
            RequireEnabled();
            string profileId = RequireProfileId();
            return await PostAsync<OwnershipKnownRollResult>("ownership_roll_known", new KnownRequest
            {
                player_profile_id = profileId,
                idempotency_key = idempotencyKey,
            });
        }

        public async Task<OwnershipSpineSnapshot> FetchStateAsync()
        {
            RequireEnabled();
            ResolveChainDependencies();
            // Hydration is a read. Reconciliation is deliberately initiated by the
            // ownership service/UI so opening a screen cannot silently start a pull
            // animation or leave presentation state locked behind a network request.
            return await FetchInventoryAsync() ?? new OwnershipSpineSnapshot();
        }

        public async Task<StampMoveResult> ReconcileStampAsync(string idempotencyKey = null)
        {
            RequireStampDependencies();
            ReconcileResponse response = await PostAsync<ReconcileResponse>("ownership_reconcile_stamp", new ReconcileRequest
            {
                player_profile_id = RequireProfileId(),
                wallet_address = wallet.PublicKey,
                idempotency_key = idempotencyKey,
            });
            return FromReconciliation(response);
        }

        public async Task<IReadOnlyList<KnownMoveRecord>> FetchKnownAsync()
        {
            OwnershipSpineSnapshot response = await FetchStateAsync();
            return response?.known ?? new List<KnownMoveRecord>();
        }

        public async Task<StampTransactionReview> SimulateStampAsync(StampMoveRequest request)
        {
            RequireStampDependencies();
            PreparedStampPayload payload = await PrepareAsync(request);
            byte[] transaction = Convert.FromBase64String(payload.transaction_base64);
            ChainTxScope scope = StampScope(payload, request.idempotencyKey);
            RpcSimulationResult simulation = await txOrchestrator.SimulateAsync(
                new ChainTxRequest(scope, transaction, wallet.SupportsSignAndSend));
            payload.review.simulationSucceeded = simulation != null && simulation.success;
            payload.review.simulationMessage = simulation != null ? simulation.errorMessage : "RPC simulation returned no result.";
            if (simulation != null && simulation.unitsConsumed > 0)
                payload.review.simulationMessage = payload.review.simulationSucceeded
                    ? $"Simulation passed ({simulation.unitsConsumed} compute units)."
                    : payload.review.simulationMessage;
            prepared[request.idempotencyKey] = payload;
            return payload.review;
        }

        public async Task<StampMoveResult> SubmitStampAsync(StampMoveRequest request)
        {
            RequireStampDependencies();
            if (!prepared.TryGetValue(request.idempotencyKey, out PreparedStampPayload payload)
                || payload.review == null || !payload.review.simulationSucceeded)
                return new StampMoveResult { error = "Stamp must pass simulation before signing." };

            byte[] transaction = Convert.FromBase64String(payload.transaction_base64);
            if (!Application.isEditor && !wallet.SupportsSignAndSend)
                return new StampMoveResult { error = "Device Stamp requires wallet SignAndSend support." };

            TxResult submission;
            try
            {
                submission = await txOrchestrator.SubmitAsync(new ChainTxRequest(
                    StampScope(payload, request.idempotencyKey),
                    transaction,
                    wallet.SupportsSignAndSend));
            }
            catch (Exception exception)
            {
                return new StampMoveResult { error = exception.Message };
            }

            string signature = submission?.txSignature;

            if (string.IsNullOrWhiteSpace(signature))
                return new StampMoveResult
                {
                    pending = submission != null && submission.status == TxStatus.UnknownOutcome,
                    operationId = payload.operation_id,
                    operationStatus = submission?.status.ToString(),
                    idempotencyKey = request.idempotencyKey,
                    error = submission?.errorMessage ?? "Wallet rejected or did not return a transaction signature.",
                };

            // Once the wallet returns a signature, never offer this prepared transaction for
            // signing again. The authority persists it immediately and reconciliation handles
            // RPC/DAS delay or process interruption.
            prepared.Remove(request.idempotencyKey);
            ConfirmationResponse confirmation = await PostAsync<ConfirmationResponse>("ownership_confirm_stamp", new ConfirmRequest
            {
                player_profile_id = RequireProfileId(),
                wallet_address = wallet.PublicKey,
                known_instance_id = request.knownInstanceId,
                idempotency_key = request.idempotencyKey,
                signature = signature,
            });
            return new StampMoveResult
            {
                success = confirmation != null && confirmation.success,
                confirmed = confirmation != null && confirmation.confirmed,
                pending = confirmation != null && confirmation.pending,
                operationId = confirmation?.operation_id ?? payload.operation_id,
                operationStatus = confirmation?.operation_status,
                knownInstanceId = confirmation?.known_instance_id ?? request.knownInstanceId,
                idempotencyKey = confirmation?.idempotency_key ?? request.idempotencyKey,
                acquisition = confirmation?.acquisition ?? payload.acquisition,
                assetAddress = confirmation?.stamped_asset_address ?? payload.stamped_asset_address,
                message = confirmation?.message,
                signature = signature,
                ramBalance = confirmation != null ? confirmation.ram_balance : 0L,
                verifiedAsset = confirmation?.asset,
                error = confirmation != null && confirmation.success && confirmation.confirmed
                    ? null
                    : confirmation != null && confirmation.pending
                        ? null
                        : "Stamp confirmation was not accepted by the authority.",
            };
        }

        public async Task<IReadOnlyList<StampedMoveRecord>> FetchVerifiedStampedAsync()
        {
            OwnershipSpineSnapshot response = await FetchStateAsync();
            return response?.stamped ?? new List<StampedMoveRecord>();
        }

        public async Task<DevnetRamGrantReview> SimulateDevnetRamGrantAsync(string idempotencyKey)
        {
            RequireTransactionDependencies();
            PreparedRamGrantPayload payload = await PrepareRamGrantAsync(idempotencyKey);
            byte[] transaction = Convert.FromBase64String(payload.transaction_base64);
            RpcSimulationResult simulation = await rpc.SimulateTransactionAsync(transaction);
            payload.review.simulationSucceeded = simulation != null && simulation.success;
            payload.review.simulationMessage = simulation != null ? simulation.errorMessage : "RPC simulation returned no result.";
            if (simulation != null && simulation.unitsConsumed > 0)
            {
                payload.review.simulationMessage = payload.review.simulationSucceeded
                    ? $"Simulation passed ({simulation.unitsConsumed} compute units)."
                    : payload.review.simulationMessage;
            }
            preparedRamGrants[idempotencyKey] = payload;
            return payload.review;
        }

        public async Task<DevnetRamGrantResult> SubmitDevnetRamGrantAsync(string idempotencyKey)
        {
            RequireTransactionDependencies();
            if (!preparedRamGrants.TryGetValue(idempotencyKey, out PreparedRamGrantPayload payload)
                || payload.review == null || !payload.review.simulationSucceeded)
            {
                return new DevnetRamGrantResult { error = "RAM grant must pass simulation before signing." };
            }

            byte[] transaction = Convert.FromBase64String(payload.transaction_base64);
            string signature;
            try
            {
                signature = wallet.SupportsSignAndSend
                    ? await wallet.SignAndSendTransactionAsync(transaction)
                    : await SendSignedAsync(transaction);
            }
            catch (Exception exception)
            {
                return new DevnetRamGrantResult { error = exception.Message };
            }

            if (string.IsNullOrWhiteSpace(signature))
                return new DevnetRamGrantResult { error = "Wallet rejected or did not return a transaction signature." };

            RpcConfirmationStatus status = await rpc.ConfirmTransactionAsync(
                signature, solanaConfig != null ? solanaConfig.confirmationTimeoutMs : 45000);
            bool confirmed = status == RpcConfirmationStatus.Confirmed || status == RpcConfirmationStatus.Finalized;
            if (!confirmed)
                return new DevnetRamGrantResult { signature = signature, error = "RAM grant confirmation timed out." };

            RamGrantConfirmationResponse response = await PostAsync<RamGrantConfirmationResponse>(
                "ownership_confirm_devnet_smoke_grant",
                new RamGrantConfirmRequest
                {
                    player_profile_id = RequireProfileId(),
                    wallet_address = wallet.PublicKey,
                    idempotency_key = idempotencyKey,
                    signature = signature,
                });
            preparedRamGrants.Remove(idempotencyKey);
            return new DevnetRamGrantResult
            {
                success = response != null && response.success,
                confirmed = response != null && response.confirmed,
                signature = signature,
                amount = response != null ? response.amount : 0,
                balanceAfter = response != null ? response.balance_after : 0L,
                error = response != null && response.success ? null : "RAM grant confirmation was not accepted by the authority.",
            };
        }

        private async Task<PreparedStampPayload> PrepareAsync(StampMoveRequest request)
        {
            if (prepared.TryGetValue(request.idempotencyKey, out PreparedStampPayload cached))
                return cached;
            PreparedStampPayload payload = await PostAsync<PreparedStampPayload>("ownership_prepare_stamp", new PrepareRequest
            {
                player_profile_id = RequireProfileId(),
                wallet_address = wallet.PublicKey,
                known_instance_id = request.knownInstanceId,
                modifier_count = request.requestedModifierCount,
                idempotency_key = request.idempotencyKey,
            });
            if (payload == null || string.IsNullOrWhiteSpace(payload.transaction_base64) || payload.review == null)
                throw new InvalidOperationException("Stamp authority returned an invalid prepared transaction.");
            string requestedKey = request.idempotencyKey;
            if (!string.IsNullOrWhiteSpace(payload.idempotency_key))
                request.idempotencyKey = payload.idempotency_key;
            if (!string.Equals(requestedKey, request.idempotencyKey, StringComparison.Ordinal))
                prepared.Remove(requestedKey);
            prepared[request.idempotencyKey] = payload;
            return payload;
        }

        private async Task<PreparedRamGrantPayload> PrepareRamGrantAsync(string idempotencyKey)
        {
            if (preparedRamGrants.TryGetValue(idempotencyKey, out PreparedRamGrantPayload cached))
                return cached;

            PreparedRamGrantPayload payload = await PostAsync<PreparedRamGrantPayload>(
                "ownership_prepare_devnet_smoke_grant",
                new RamGrantPrepareRequest
                {
                    player_profile_id = RequireProfileId(),
                    wallet_address = wallet.PublicKey,
                    idempotency_key = idempotencyKey,
                });
            if (payload == null || string.IsNullOrWhiteSpace(payload.transaction_base64) || payload.review == null)
                throw new InvalidOperationException("RAM grant authority returned an invalid prepared transaction.");
            preparedRamGrants[idempotencyKey] = payload;
            return payload;
        }

        private Task<OwnershipSpineSnapshot> FetchInventoryAsync() => PostAsync<OwnershipSpineSnapshot>(
            "ownership_get_inventory", new InventoryRequest
            {
                player_profile_id = RequireProfileId(),
                wallet_address = wallet != null ? wallet.PublicKey : string.Empty,
            });

        private static StampMoveResult FromReconciliation(ReconcileResponse response)
        {
            if (response == null)
                return new StampMoveResult { error = "Stamp reconciliation returned no response." };
            bool none = string.Equals(response.operation_status, "none", StringComparison.OrdinalIgnoreCase);
            return new StampMoveResult
            {
                success = response.success,
                confirmed = response.confirmed,
                pending = response.pending,
                operationId = response.operation_id,
                operationStatus = response.operation_status,
                knownInstanceId = response.known_instance_id,
                idempotencyKey = response.idempotency_key,
                acquisition = response.acquisition,
                assetAddress = response.stamped_asset_address,
                signature = response.signature,
                message = response.message,
                ramBalance = response.ram_balance,
                verifiedAsset = response.asset,
                error = response.success || none ? null : "Stamp reconciliation was not accepted by the authority.",
            };
        }

        private async Task<T> PostAsync<T>(string function, object request) where T : class
        {
            if (supabaseConfig == null) throw new InvalidOperationException("Supabase ownership configuration is missing.");
            SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, function, JsonUtility.ToJson(request));
            if (!response.IsSuccess)
                throw new InvalidOperationException($"{function} returned {response.statusCode}: {response.body}");
            return JsonUtility.FromJson<T>(response.body);
        }

        private async Task<string> SendSignedAsync(byte[] transaction)
        {
            byte[] signed = await wallet.SignTransactionAsync(transaction);
            return signed != null && signed.Length > 0 ? await rpc.SendTransactionAsync(signed) : null;
        }

        private ChainTxScope StampScope(PreparedStampPayload payload, string idempotencyKey)
        {
            string operationId = !string.IsNullOrWhiteSpace(payload?.operation_id)
                ? payload.operation_id
                : idempotencyKey;
            string cluster = solanaConfig != null ? solanaConfig.ownershipCluster : string.Empty;
            return new ChainTxScope(cluster, wallet.PublicKey, operationId);
        }

        private string RequireProfileId()
        {
            string value = GameSessionContext.Instance != null ? GameSessionContext.Instance.PlayerProfileId : string.Empty;
            return !string.IsNullOrWhiteSpace(value) ? value : throw new InvalidOperationException("Player profile is not available.");
        }

        private void RequireEnabled()
        {
            if (!IsRuntimeEnabled()) throw new InvalidOperationException("Ownership Spine is disabled outside devnet validation.");
        }

        private void RequireStampDependencies()
        {
            RequireTransactionDependencies();
            if (txOrchestrator == null) throw new InvalidOperationException("Solana transaction recovery is not configured.");
        }

        private void RequireTransactionDependencies()
        {
            RequireEnabled();
            ResolveChainDependencies();
            if (!IsWalletConnected) throw new InvalidOperationException("Connect a wallet to authorize this ownership transaction.");
            if (rpc == null) throw new InvalidOperationException("Solana RPC service is not configured.");
        }

        private bool IsRuntimeEnabled()
        {
            if (!PlatformContext.ResolveOwnershipRuntimeEnabled())
                return false;
            if (Application.isEditor)
                return enableDevnetOwnershipSpine;
            return PlatformContext.HasExplicitDeploymentSelection;
        }

        private void ResolveChainDependencies()
        {
            SolanaServiceBridge bridge = SolanaServiceBridge.Instance;
            if (bridge == null) return;
            if (wallet == null) wallet = bridge.WalletService;
            if (rpc == null) rpc = bridge.RpcService;
            if (txOrchestrator == null) txOrchestrator = bridge.TxOrchestrator;
            if (solanaConfig == null) solanaConfig = bridge.Config;
        }

        [Serializable] private sealed class KnownRequest { public string player_profile_id; public string idempotency_key; }
        [Serializable] private sealed class InventoryRequest { public string player_profile_id; public string wallet_address; }
        [Serializable] private sealed class ReconcileRequest { public string player_profile_id; public string wallet_address; public string idempotency_key; }
        [Serializable] private sealed class PrepareRequest { public string player_profile_id; public string wallet_address; public string known_instance_id; public int modifier_count; public string idempotency_key; }
        [Serializable] private sealed class ConfirmRequest { public string player_profile_id; public string wallet_address; public string known_instance_id; public string idempotency_key; public string signature; }
        [Serializable] private sealed class ConfirmationResponse { public bool success; public bool confirmed; public bool pending; public string operation_id; public string operation_status; public string known_instance_id; public string idempotency_key; public string acquisition; public string stamped_asset_address; public string signature; public string message; public long ram_balance; public StampedMoveRecord asset; }
        [Serializable] private sealed class ReconcileResponse { public bool success; public bool confirmed; public bool pending; public string operation_id; public string operation_status; public string known_instance_id; public string idempotency_key; public string acquisition; public string stamped_asset_address; public string signature; public string message; public long ram_balance; public StampedMoveRecord asset; }
        [Serializable] private sealed class PreparedStampPayload { public string operation_id; public string operation_status; public string idempotency_key; public string known_instance_id; public string acquisition; public string stamped_asset_address; public string transaction_base64; public StampTransactionReview review; }
        [Serializable] private sealed class RamGrantPrepareRequest { public string player_profile_id; public string wallet_address; public string idempotency_key; }
        [Serializable] private sealed class RamGrantConfirmRequest { public string player_profile_id; public string wallet_address; public string idempotency_key; public string signature; }
        [Serializable] private sealed class PreparedRamGrantPayload { public string transaction_base64; public DevnetRamGrantReview review; }
        [Serializable] private sealed class RamGrantConfirmationResponse { public bool success; public bool confirmed; public int amount; public long balance_after; }
    }
}
