using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    [Serializable]
    public sealed class KnownMoveRecord
    {
        public string instanceId;
        public string moveId;
        public int modifierCount;
        public int generatorVersion;
        public string poolHash;
        public string acquisition;
    }

    [Serializable]
    public sealed class StampedMoveRecord
    {
        public string assetAddress;
        public string ownerAddress;
        public string collectionAddress;
        public string updateAuthority;
        public string moveId;
        public string knownInstanceId;
        public string acquisition;
        public string rarity;
        public string modifier1Id;
        public float modifier1EffectValue;
        public bool modifier1EffectValueLocked;
        public string modifier2Id;
        public float modifier2EffectValue;
        public bool modifier2EffectValueLocked;
        public int modifierCount;
        public int schemaVersion;
        public int generatorVersion;
        public string poolHash;
        public string entropyTrace;
        public bool isCoreAsset;
    }

    [Serializable]
    public sealed class StampMoveRequest
    {
        public string knownInstanceId;
        public int requestedModifierCount;
        public string idempotencyKey;
    }

    [Serializable]
    public sealed class StampTransactionReview
    {
        public string cluster;
        public string feePayer;
        public string chainboundProgram;
        public string coreProgram;
        public string knownInstanceId;
        public int ramCost;
        public string outputCollection;
        public ulong estimatedNetworkFeeLamports;
        public bool simulationSucceeded;
        public string simulationMessage;
        public bool metadataPublished;
        public string metadataUri;
        public string imageUri;
    }

    public sealed class StampMoveResult
    {
        public bool success;
        public bool confirmed;
        public bool pending;
        public string operationId;
        public string operationStatus;
        public string knownInstanceId;
        public string idempotencyKey;
        public string acquisition;
        public string assetAddress;
        public string message;
        public string signature;
        public string error;
        public long ramBalance;
        public long ramBalanceVersion;
        public StampedMoveRecord verifiedAsset;
    }

    [Serializable]
    public sealed class PendingStampRecord
    {
        public string operationId;
        public string operationStatus;
        public string knownInstanceId;
        public string idempotencyKey;
        public string acquisition;
        public string assetAddress;
        public string signature;
        public string message;
    }

    [Serializable]
    public sealed class OwnershipKnownRollResult
    {
        public KnownMoveRecord known;
        public int ramCost;
        public long ramBalance;
        public long ramBalanceVersion;
    }

    [Serializable]
    public sealed class OwnershipSpineSnapshot
    {
        public long ramBalance;
        public long ramBalanceVersion;
        public int stampRamCost;
        public List<KnownMoveRecord> known = new List<KnownMoveRecord>();
        public List<StampedMoveRecord> stamped = new List<StampedMoveRecord>();
        public PendingStampRecord pendingStamp;
    }

    [Serializable]
    public sealed class DevnetRamGrantReview
    {
        public string cluster;
        public string feePayer;
        public string chainboundProgram;
        public int amount;
        public int dailyCap;
        public int startingBalanceVerified;
        public ulong estimatedNetworkFeeLamports;
        public bool simulationSucceeded;
        public string simulationMessage;
    }

    public sealed class DevnetRamGrantResult
    {
        public bool success;
        public bool confirmed;
        public string signature;
        public int amount;
        public long balanceAfter;
        public string error;
    }

    public interface IKnownMoveService
    {
        IReadOnlyList<KnownMoveRecord> CachedKnown { get; }
        Task<KnownMoveRecord> GetKnownAsync(string idempotencyKey);
        Task RefreshKnownAsync();
    }

    public interface IStampedMoveService
    {
        IReadOnlyList<StampedMoveRecord> CachedStamped { get; }
        Task<StampTransactionReview> ReviewStampAsync(StampMoveRequest request);
        Task<StampMoveResult> StampMoveAsync(StampMoveRequest request);
        Task RefreshStampedAsync();
    }

    public interface IOwnershipSpineRuntimeService : IKnownMoveService, IStampedMoveService
    {
        long RamBalance { get; }
        int StampRamCost { get; }
        PendingStampRecord PendingStamp { get; }
        Task RefreshAsync();
        Task<StampMoveResult> ReconcilePendingStampAsync(string idempotencyKey = null);
        Task<DevnetRamGrantReview> ReviewDevnetRamGrantAsync(string idempotencyKey);
        Task<DevnetRamGrantResult> GrantDevnetRamAsync(string idempotencyKey);
    }

    public interface IOwnershipSpineGateway
    {
        bool IsWalletConnected { get; }
        Task<OwnershipKnownRollResult> RollKnownAsync(string idempotencyKey);
        Task<OwnershipSpineSnapshot> FetchStateAsync();
        Task<IReadOnlyList<KnownMoveRecord>> FetchKnownAsync();
        Task<StampTransactionReview> SimulateStampAsync(StampMoveRequest request);
        Task<StampMoveResult> SubmitStampAsync(StampMoveRequest request);
        Task<StampMoveResult> ReconcileStampAsync(string idempotencyKey = null);
        Task<IReadOnlyList<StampedMoveRecord>> FetchVerifiedStampedAsync();
        Task<DevnetRamGrantReview> SimulateDevnetRamGrantAsync(string idempotencyKey);
        Task<DevnetRamGrantResult> SubmitDevnetRamGrantAsync(string idempotencyKey);
    }

    /// <summary>
    /// Client orchestration only. The gateway owns authoritative backend/chain operations.
    /// A successful wallet response is not enough: Stamped inventory is accepted only after
    /// the authority verifies the consumed on-chain Stamp Ticket and Core-owned asset account.
    /// DAS enrichment is asynchronous and cannot hold gameplay settlement open.
    /// </summary>
    public sealed class OwnershipSpineService : IOwnershipSpineRuntimeService
    {
        public const int DefaultFirstStampRamCost = 75;

        private readonly IOwnershipSpineGateway gateway;
        private readonly string trustedCollection;
        private readonly string trustedUpdateAuthority;
        private readonly int supportedSchemaVersion;
        private readonly MoveDatabaseSO moveDatabase;
        private readonly ModifierCatalogSO modifierCatalog;
        private readonly List<KnownMoveRecord> known = new List<KnownMoveRecord>();
        private readonly List<StampedMoveRecord> stamped = new List<StampedMoveRecord>();

        public OwnershipSpineService(
            IOwnershipSpineGateway gateway,
            string trustedCollection,
            string trustedUpdateAuthority,
            int supportedSchemaVersion,
            MoveDatabaseSO moveDatabase = null,
            ModifierCatalogSO modifierCatalog = null)
        {
            this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            this.trustedCollection = RequireValue(trustedCollection, nameof(trustedCollection));
            this.trustedUpdateAuthority = RequireValue(trustedUpdateAuthority, nameof(trustedUpdateAuthority));
            this.supportedSchemaVersion = supportedSchemaVersion;
            this.moveDatabase = moveDatabase;
            this.modifierCatalog = modifierCatalog;
        }

        public IReadOnlyList<KnownMoveRecord> CachedKnown => known;
        public IReadOnlyList<StampedMoveRecord> CachedStamped => stamped;
        public long RamBalance { get; private set; }
        public int StampRamCost { get; private set; } = DefaultFirstStampRamCost;
        public PendingStampRecord PendingStamp { get; private set; }

        public async Task<KnownMoveRecord> GetKnownAsync(string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));

            OwnershipKnownRollResult rolled = await gateway.RollKnownAsync(idempotencyKey);
            KnownMoveRecord result = rolled?.known;
            if (!IsValidKnown(result))
                throw new InvalidOperationException("Authority returned an invalid Known record.");
            ApplyAuthoritativeBalance(rolled.ramBalance, rolled.ramBalanceVersion);
            UpsertKnown(result);
            await RefreshAsync();
            for (int i = 0; i < known.Count; i++)
            {
                if (string.Equals(known[i].instanceId, result.instanceId, StringComparison.Ordinal))
                    return known[i];
            }
            throw new InvalidOperationException("Confirmed Known roll was not returned by authoritative inventory.");
        }

        public async Task RefreshAsync()
        {
            OwnershipSpineSnapshot snapshot = await gateway.FetchStateAsync();
            if (snapshot == null)
                throw new InvalidOperationException("Ownership authority returned no inventory state.");

            ApplySnapshot(snapshot);
        }

        public Task RefreshKnownAsync() => RefreshAsync();

        public async Task<StampTransactionReview> ReviewStampAsync(StampMoveRequest request)
        {
            ValidateStampRequest(request);
            await RefreshAsync();
            EnsureSufficientRamForStamp();
            WalletAccessResult access = await EnsureWalletAccessAsync("review this Stamp");
            if (access.kind != WalletAccessResultKind.Ready)
                throw new InvalidOperationException(access.message ?? "Wallet access was not authorized.");
            if (!gateway.IsWalletConnected)
                throw new InvalidOperationException("Connect a wallet to Stamp a move.");
            return await gateway.SimulateStampAsync(request);
        }

        public async Task<StampMoveResult> StampMoveAsync(StampMoveRequest request)
        {
            ValidateStampRequest(request);
            await RefreshAsync();
            if (RamBalance < StampRamCost)
            {
                return new StampMoveResult
                {
                    error = $"{StampRamCost} RAM is required to Stamp a move. Current balance: {RamBalance}.",
                    ramBalance = RamBalance,
                };
            }
            WalletAccessResult access = await EnsureWalletAccessAsync("confirm this Stamp transaction");
            if (access.kind != WalletAccessResultKind.Ready)
                return new StampMoveResult { error = access.message ?? "Wallet access was not authorized." };
            if (!gateway.IsWalletConnected)
                return new StampMoveResult { error = "Connect a wallet to Stamp a move." };

            StampMoveResult submitted = await gateway.SubmitStampAsync(request);
            if (submitted != null && submitted.success && submitted.pending)
            {
                PendingStamp = ToPendingRecord(submitted);
                return submitted;
            }
            if (submitted == null || !submitted.success || !submitted.confirmed)
                return submitted ?? new StampMoveResult { error = "Stamp transaction failed." };

            return await HydrateConfirmedStampAsync(request.knownInstanceId, submitted);
        }

        public async Task<StampMoveResult> ReconcilePendingStampAsync(string idempotencyKey = null)
        {
            PendingStampRecord pendingBeforeReconcile = PendingStamp;
            StampMoveResult reconciled = await gateway.ReconcileStampAsync(idempotencyKey);
            if (reconciled != null && reconciled.success && reconciled.pending)
            {
                PendingStamp = ToPendingRecord(reconciled);
                return reconciled;
            }
            if (reconciled == null || !reconciled.success || !reconciled.confirmed)
            {
                if (reconciled != null && reconciled.success && !reconciled.pending
                    && (string.Equals(reconciled.operationStatus, "none", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(reconciled.operationStatus, "failed", StringComparison.OrdinalIgnoreCase)))
                {
                    return await RecoverSettledStampFromInventoryAsync(pendingBeforeReconcile, reconciled);
                }

                return reconciled ?? new StampMoveResult { error = "Stamp reconciliation failed." };
            }
            string knownInstanceId = !string.IsNullOrWhiteSpace(reconciled.knownInstanceId)
                ? reconciled.knownInstanceId
                : pendingBeforeReconcile?.knownInstanceId;
            return await HydrateConfirmedStampAsync(knownInstanceId, reconciled);
        }

        private async Task<StampMoveResult> RecoverSettledStampFromInventoryAsync(
            PendingStampRecord previousPending,
            StampMoveResult reconciled)
        {
            OwnershipSpineSnapshot snapshot = await gateway.FetchStateAsync();
            if (snapshot == null)
                return new StampMoveResult { error = "Ownership authority returned no inventory state." };

            StampedMoveRecord match = FindTrustedStamped(
                snapshot.stamped,
                previousPending?.knownInstanceId,
                previousPending?.assetAddress);
            ApplySnapshot(snapshot);
            if (match == null)
            {
                // There is no durable open intent anymore. Clear stale client state
                // and return a terminal, non-error result so the machine unlocks.
                PendingStamp = null;
                reconciled.pending = false;
                reconciled.confirmed = false;
                if (string.IsNullOrWhiteSpace(reconciled.message))
                    reconciled.message = "No pending Stamp remains. Authoritative inventory was refreshed.";
                return reconciled;
            }

            reconciled.confirmed = true;
            reconciled.pending = false;
            reconciled.operationStatus = "confirmed";
            reconciled.knownInstanceId = match.knownInstanceId;
            reconciled.assetAddress = match.assetAddress;
            reconciled.acquisition = match.acquisition;
            reconciled.verifiedAsset = match;
            reconciled.ramBalance = RamBalance;
            reconciled.message = "Recovered confirmed Stamp from authoritative inventory.";
            PendingStamp = null;
            return reconciled;
        }

        private async Task<StampMoveResult> HydrateConfirmedStampAsync(string knownInstanceId, StampMoveResult submitted)
        {
            if (string.IsNullOrWhiteSpace(knownInstanceId))
                return new StampMoveResult { error = "Confirmed Stamp did not identify its Known move." };

            // The confirmation response is backed by the consumed on-chain
            // Stamp Ticket. Accept it immediately so DAS presentation indexing
            // cannot hold the machine in SETTLING or expose the consumed Known
            // move for another attempt.
            if (IsTrustedStamped(submitted.verifiedAsset))
            {
                submitted.acquisition = submitted.verifiedAsset.acquisition;
                known.RemoveAll(item => string.Equals(item.instanceId, knownInstanceId, StringComparison.Ordinal));
                stamped.RemoveAll(item => string.Equals(
                    item.assetAddress,
                    submitted.verifiedAsset.assetAddress,
                    StringComparison.Ordinal));
                stamped.Add(submitted.verifiedAsset);
                ApplyAuthoritativeBalance(submitted.ramBalance, submitted.ramBalanceVersion);
                PendingStamp = null;
                return submitted;
            }

            OwnershipSpineSnapshot snapshot = await gateway.FetchStateAsync();
            IReadOnlyList<StampedMoveRecord> verified = snapshot?.stamped;
            StampedMoveRecord match = null;
            if (verified != null)
            {
                for (int i = 0; i < verified.Count; i++)
                {
                    StampedMoveRecord candidate = verified[i];
                    if (IsTrustedStamped(candidate)
                        && string.Equals(candidate.knownInstanceId, knownInstanceId, StringComparison.Ordinal))
                    {
                        match = candidate;
                        break;
                    }
                }
            }

            if (match == null)
                return new StampMoveResult { error = "Confirmed transaction was not returned by verified Core inventory." };

            ApplySnapshot(snapshot);
            submitted.ramBalance = RamBalance;
            submitted.acquisition = match.acquisition;
            submitted.verifiedAsset = match;
            return submitted;
        }

        private StampedMoveRecord FindTrustedStamped(
            IReadOnlyList<StampedMoveRecord> records,
            string knownInstanceId,
            string assetAddress)
        {
            if (records == null)
                return null;

            for (int i = 0; i < records.Count; i++)
            {
                StampedMoveRecord candidate = records[i];
                if (!IsTrustedStamped(candidate))
                    continue;
                if (!string.IsNullOrWhiteSpace(knownInstanceId)
                    && string.Equals(candidate.knownInstanceId, knownInstanceId, StringComparison.Ordinal))
                {
                    return candidate;
                }
                if (!string.IsNullOrWhiteSpace(assetAddress)
                    && string.Equals(candidate.assetAddress, assetAddress, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void ApplySnapshot(OwnershipSpineSnapshot snapshot)
        {
            ApplyAuthoritativeBalance(snapshot.ramBalance, snapshot.ramBalanceVersion);
            StampRamCost = snapshot.stampRamCost > 0
                ? snapshot.stampRamCost
                : DefaultFirstStampRamCost;
            PendingStamp = snapshot.pendingStamp;
            known.Clear();
            if (snapshot.known != null)
            {
                for (int i = 0; i < snapshot.known.Count; i++)
                    if (IsValidKnown(snapshot.known[i])) UpsertKnown(snapshot.known[i]);
            }
            ReplaceStamped(snapshot.stamped);
        }

        private void ApplyAuthoritativeBalance(long balance, long balanceVersion)
        {
            string profileId = GameSessionContext.Instance != null
                ? GameSessionContext.Instance.PlayerProfileId
                : "";
            PlayerRamBalanceStore.TryApplyAuthoritative(profileId, balance, balanceVersion);
            RamBalance = PlayerRamBalanceStore.TryGet(profileId, out long current)
                ? current
                : Math.Max(0L, balance);
        }

        private void EnsureSufficientRamForStamp()
        {
            if (RamBalance < StampRamCost)
            {
                throw new InvalidOperationException(
                    $"{StampRamCost} RAM is required to Stamp a move. Current balance: {RamBalance}.");
            }
        }

        private static PendingStampRecord ToPendingRecord(StampMoveResult result) => new PendingStampRecord
        {
            operationId = result.operationId,
            operationStatus = result.operationStatus,
            knownInstanceId = result.knownInstanceId,
            idempotencyKey = result.idempotencyKey,
            acquisition = result.acquisition ?? result.verifiedAsset?.acquisition,
            assetAddress = result.assetAddress,
            signature = result.signature,
            message = result.message,
        };

        public Task RefreshStampedAsync() => RefreshAsync();

        public async Task<DevnetRamGrantReview> ReviewDevnetRamGrantAsync(string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new ArgumentException("An idempotency key is required.", nameof(idempotencyKey));

            WalletAccessResult access = await EnsureWalletAccessAsync("review the devnet RAM grant");
            if (access.kind != WalletAccessResultKind.Ready)
                throw new InvalidOperationException(access.message ?? "Wallet access was not authorized.");
            if (!gateway.IsWalletConnected)
                throw new InvalidOperationException("Connect a wallet to grant devnet RAM.");
            return await gateway.SimulateDevnetRamGrantAsync(idempotencyKey);
        }

        public async Task<DevnetRamGrantResult> GrantDevnetRamAsync(string idempotencyKey)
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey))
                return new DevnetRamGrantResult { error = "An idempotency key is required." };

            WalletAccessResult access = await EnsureWalletAccessAsync("confirm the devnet RAM grant");
            if (access.kind != WalletAccessResultKind.Ready)
                return new DevnetRamGrantResult { error = access.message ?? "Wallet access was not authorized." };
            if (!gateway.IsWalletConnected)
                return new DevnetRamGrantResult { error = "Connect a wallet to grant devnet RAM." };

            DevnetRamGrantResult result = await gateway.SubmitDevnetRamGrantAsync(idempotencyKey);
            if (result == null || !result.success || !result.confirmed)
                return result ?? new DevnetRamGrantResult { error = "Devnet RAM grant failed." };

            RamBalance = Math.Max(0L, result.balanceAfter);
            await RefreshAsync();
            result.balanceAfter = RamBalance;
            return result;
        }

        private static async Task<WalletAccessResult> EnsureWalletAccessAsync(string actionName)
        {
            IProfileWalletAccessCoordinator coordinator = ProfileWalletAccessRegistry.Current;
            if (coordinator == null)
            {
                return WalletAccessResult.From(
                    WalletAccessResultKind.Failed,
                    "Wallet access service is unavailable.");
            }

            return await coordinator.EnsureAccessAsync(new WalletAccessRequest
            {
                reason = WalletAccessReason.OwnershipAction,
                actionName = actionName,
                requireFreshProof = true,
                allowOfflineContinuation = false,
            });
        }

        private void ReplaceStamped(IReadOnlyList<StampedMoveRecord> records)
        {
            stamped.Clear();
            if (records == null) return;
            for (int i = 0; i < records.Count; i++)
                if (IsTrustedStamped(records[i])) stamped.Add(records[i]);
        }

        private bool IsTrustedStamped(StampedMoveRecord value)
        {
            if (value == null || !value.isCoreAsset || value.schemaVersion != supportedSchemaVersion)
                return false;
            if (!string.Equals(value.collectionAddress, trustedCollection, StringComparison.Ordinal)
                || !string.Equals(value.updateAuthority, trustedUpdateAuthority, StringComparison.Ordinal))
                return false;
            if (value.modifierCount < 1 || value.modifierCount > 2 || string.IsNullOrWhiteSpace(value.modifier1Id))
                return false;
            bool structurallyValid = value.modifierCount != 2
                || (!string.IsNullOrWhiteSpace(value.modifier2Id)
                    && !string.Equals(value.modifier1Id, value.modifier2Id, StringComparison.Ordinal));
            if (!structurallyValid || moveDatabase == null || modifierCatalog == null)
                return structurallyValid;

            MoveDefinitionSO hostMove = moveDatabase.GetById(value.moveId);
            if (hostMove == null)
                return false;

            var assigned = new List<string>(2);
            if (!IsTrustedModifier(
                    value.modifier1Id,
                    value.modifier1EffectValue,
                    value.modifier1EffectValueLocked,
                    hostMove,
                    assigned))
            {
                return false;
            }
            assigned.Add(value.modifier1Id);

            return value.modifierCount != 2 || IsTrustedModifier(
                value.modifier2Id,
                value.modifier2EffectValue,
                value.modifier2EffectValueLocked,
                hostMove,
                assigned);
        }

        private bool IsTrustedModifier(
            string sectionId,
            float effectValue,
            bool effectValueLocked,
            MoveDefinitionSO hostMove,
            IReadOnlyCollection<string> assigned)
        {
            ModifierDefinitionSO definition = modifierCatalog.GetBySectionId(sectionId);
            if (definition == null
                || !ModifierValidationService.CanApply(definition, hostMove, modifierCatalog, assigned).isValid)
            {
                return false;
            }

            if (!effectValueLocked)
                return true; // Backward-compatible read of pre-lock schema records.

            ModifierBalanceOverlaySO overlay = BalanceConfigRuntime.ActiveModifierOverlay;
            float min = overlay != null ? overlay.ResolveEffectMin(definition) : definition.effectMin;
            float max = overlay != null ? overlay.ResolveEffectMax(definition) : definition.effectMax;
            if (max < min)
            {
                float swap = min;
                min = max;
                max = swap;
            }
            return effectValue >= min && effectValue <= max;
        }

        private static bool IsValidKnown(KnownMoveRecord value) =>
            value != null && !string.IsNullOrWhiteSpace(value.instanceId)
            && !string.IsNullOrWhiteSpace(value.moveId) && value.modifierCount == 0;

        private void UpsertKnown(KnownMoveRecord value)
        {
            known.RemoveAll(item => string.Equals(item.instanceId, value.instanceId, StringComparison.Ordinal));
            known.Add(value);
        }

        private static void ValidateStampRequest(StampMoveRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.knownInstanceId)
                || string.IsNullOrWhiteSpace(request.idempotencyKey))
                throw new ArgumentException("Stamp requires an exact Known instance and idempotency key.");
            if (request.requestedModifierCount < 1 || request.requestedModifierCount > 2)
                throw new ArgumentOutOfRangeException(nameof(request.requestedModifierCount));
        }

        private static string RequireValue(string value, string name) =>
            !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("A trusted value is required.", name);
    }
}
