using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public enum MenuConnectivityState
    {
        Unknown,
        Offline,
        Pending,
        Ready,
        Error,
    }

    [Serializable]
    public class MenuMutationResult
    {
        public bool success;
        public string error;

        public static MenuMutationResult Ok()
        {
            return new MenuMutationResult { success = true, error = "" };
        }

        public static MenuMutationResult Failed(string message)
        {
            return new MenuMutationResult { success = false, error = message ?? "" };
        }
    }

    [Serializable]
    public class PlayerRosterSnapshot
    {
        public string playerProfileId;
        public List<PlayerRosterEntry> chainlings = new List<PlayerRosterEntry>();
        public int ownedCount = -1;
        public int ownedCapacity;
        public int partyCount = -1;
        public int partyCapacity;
        public int storageCount = -1;
        public int storageCapacity;
        public bool canCapture;

        public int Count => chainlings != null ? chainlings.Count : 0;
        public int OwnedCount => ownedCount >= 0 ? ownedCount : Count;
        public int OwnedCapacity => ownedCapacity > 0 ? ownedCapacity : CombatConstants.MAX_CHAINLINGS;
        public int PartyCount => partyCount >= 0 ? partyCount : 0;
        public int PartyCapacity => partyCapacity > 0 ? partyCapacity : CombatConstants.ROSTER_SIZE;
        public int StorageCount => storageCount >= 0 ? storageCount : Math.Max(0, OwnedCount - PartyCount);
        public int StorageCapacity => storageCapacity > 0 ? storageCapacity : Math.Max(0, OwnedCapacity - PartyCapacity);
        public bool IsAtCapacity => OwnedCount >= OwnedCapacity;
    }

    [Serializable]
    public class PlayerRosterEntry
    {
        public ChainlingSnapshot snapshot = new ChainlingSnapshot();
        public bool isPartyMember;
        public int partySlot = -1;
        public int masteryXp;
        public ChainlingPveServiceRecord pveServiceRecord = new ChainlingPveServiceRecord();
        public ChainlingBattleStats battleStats = new ChainlingBattleStats();
    }

    [Serializable]
    public class ChainlingBattleStats
    {
        public int battlesWon;
        public int battlesLost;
        public int kosScored;
        public int timesKod;
        public int timesCaptured;
    }

    [Serializable]
    public class MoveInventorySnapshot
    {
        public string playerProfileId;
        public List<MenuMoveItem> moves = new List<MenuMoveItem>();
        public List<MenuMoveLoadoutSlot> loadouts = new List<MenuMoveLoadoutSlot>();
    }

    [Serializable]
    public class MenuMoveItem
    {
        public string moveItemId;
        public string moveId;
        public MoveTier tier;
        public string modifier1SectionId;
        public float modifier1EffectValue;
        public bool modifier1EffectValueLocked;
        public string modifier2SectionId;
        public float modifier2EffectValue;
        public bool modifier2EffectValueLocked;
        public bool locked;
        public string exportedAssetId;
        public string equippedChainlingId;
        public int equippedSlot = -1;

        public MoveInstance ToMoveInstance(MoveDatabaseSO moveDatabase)
        {
            MoveInstance instance = null;
            MoveDefinitionSO definition = moveDatabase != null ? moveDatabase.GetById(moveId) : null;
            if (definition != null)
                instance = CombatFactory.CreateMove(definition);
            else
                instance = CombatFactory.CreateTestMove(string.IsNullOrWhiteSpace(moveId) ? "Unknown Move" : moveId, MoveCategory.StatusUtility, ChainlingType.None, 0, 0, 100);

            instance.instanceId = string.IsNullOrWhiteSpace(moveItemId) ? instance.instanceId : moveItemId;
            instance.definitionId = moveId;
            instance.tier = tier;
            instance.modifier1SectionId = modifier1SectionId;
            instance.modifier2SectionId = modifier2SectionId;
            AddLockedModifier(instance, modifier1SectionId, modifier1EffectValue, modifier1EffectValueLocked);
            AddLockedModifier(instance, modifier2SectionId, modifier2EffectValue, modifier2EffectValueLocked);
            instance.mintAddress = exportedAssetId;
            instance.lockState = locked ? MoveLockState.Escrowed : MoveLockState.Unlocked;
            instance.modifierIds = BuildModifierIds(modifier1SectionId, modifier2SectionId);
            return instance;
        }

        private static void AddLockedModifier(
            MoveInstance instance,
            string sectionId,
            float effectValue,
            bool effectValueLocked)
        {
            if (instance == null || !effectValueLocked || string.IsNullOrWhiteSpace(sectionId))
                return;
            instance.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = sectionId.Trim(),
                effectValue = effectValue,
                effectValueMax = effectValue,
                effectValueLocked = true,
            });
        }

        private static string[] BuildModifierIds(string modifier1, string modifier2)
        {
            bool has1 = !string.IsNullOrWhiteSpace(modifier1);
            bool has2 = !string.IsNullOrWhiteSpace(modifier2);
            if (has1 && has2) return new[] { modifier1.Trim(), modifier2.Trim() };
            if (has1) return new[] { modifier1.Trim() };
            if (has2) return new[] { modifier2.Trim() };
            return Array.Empty<string>();
        }
    }

    [Serializable]
    public class MenuMoveLoadoutSlot
    {
        public string chainlingId;
        public int slot;
        public string moveItemId;
    }

    [Serializable]
    public class InventorySnapshot
    {
        public MenuConnectivityState connectivity = MenuConnectivityState.Unknown;
        public long ramBalance;
        public long ramBalanceVersion;
        /// <summary>Legacy flat id list; prefer <see cref="stacks"/>.</summary>
        public List<string> consumableIds = new List<string>();
        /// <summary>Legacy cosmetic asset pubkeys; prefer provenance on Style / Core cosmetic services.</summary>
        public List<string> cosmeticAssetIds = new List<string>();
        /// <summary>Canonical bag stacks for Inventory tab (profile + Core-backed).</summary>
        public List<MenuInventoryItemStack> stacks = new List<MenuInventoryItemStack>();
        public string cluster = "devnet";
        public string walletPublicKey = "";
        /// <summary>Empty when the fetch succeeded. Set when the bag read failed.</summary>
        public string lastError = "";
    }

    [Serializable]
    public class PlayerItemUseRequest
    {
        public string itemId;
        public string coreAssetId;
        public string targetChainlingId;
        public int targetSlot = -1;
        public string targetMoveItemId;
        public Dictionary<string, string> context;
    }

    [Serializable]
    public class PlayerItemUseResult : MenuMutationResult
    {
        public InventorySnapshot inventory;
        public int favorScore;
        public string preparedTxIdempotencyKey;
        public string consumeReceiptId;
        public List<FieldItemHpPatch> hpPatches = new List<FieldItemHpPatch>();

        public static PlayerItemUseResult Ok(
            InventorySnapshot inventory = null,
            int favorScore = 0,
            string consumeReceiptId = "",
            List<FieldItemHpPatch> hpPatches = null)
        {
            return new PlayerItemUseResult
            {
                success = true,
                error = "",
                inventory = inventory,
                favorScore = favorScore,
                consumeReceiptId = consumeReceiptId ?? "",
                hpPatches = hpPatches ?? new List<FieldItemHpPatch>(),
            };
        }

        public new static PlayerItemUseResult Failed(string message)
        {
            return new PlayerItemUseResult
            {
                success = false,
                error = message ?? "",
                hpPatches = new List<FieldItemHpPatch>(),
            };
        }
    }

    [Serializable]
    public class CoreItemConsumePrepareResult : MenuMutationResult
    {
        public string consumeIntentId;
        public string itemId;
        public string assetId;
        public string unsignedTxBase64;
        public string cluster;
        public long estimatedFeeLamports;
        public string actionSummary;

        public static CoreItemConsumePrepareResult Ok(
            string consumeIntentId,
            string itemId,
            string assetId,
            string unsignedTxBase64,
            string cluster,
            string actionSummary,
            long estimatedFeeLamports = 0)
        {
            return new CoreItemConsumePrepareResult
            {
                success = true,
                error = "",
                consumeIntentId = consumeIntentId ?? "",
                itemId = itemId ?? "",
                assetId = assetId ?? "",
                unsignedTxBase64 = unsignedTxBase64 ?? "",
                cluster = cluster ?? "devnet",
                actionSummary = actionSummary ?? "",
                estimatedFeeLamports = estimatedFeeLamports,
            };
        }

        public new static CoreItemConsumePrepareResult Failed(string message)
        {
            return new CoreItemConsumePrepareResult
            {
                success = false,
                error = message ?? "",
            };
        }
    }

    [Serializable]
    public class CoreItemConsumeFinalizeResult : MenuMutationResult
    {
        public string consumeIntentId;
        public string txSignature;
        public InventorySnapshot inventory;

        public static CoreItemConsumeFinalizeResult Ok(
            string consumeIntentId,
            string txSignature,
            InventorySnapshot inventory = null)
        {
            return new CoreItemConsumeFinalizeResult
            {
                success = true,
                error = "",
                consumeIntentId = consumeIntentId ?? "",
                txSignature = txSignature ?? "",
                inventory = inventory,
            };
        }

        public new static CoreItemConsumeFinalizeResult Failed(string message)
        {
            return new CoreItemConsumeFinalizeResult
            {
                success = false,
                error = message ?? "",
            };
        }
    }

    [Serializable]
    public class PlayerStatsSnapshot
    {
        public string playerProfileId;
        public string displayName;
        public string walletAddress;
        public long ramBalance;
        public bool handbookUnlocked;
        public int battlesWon;
        public int battlesLost;
        public int battlesDrawn;
        public int battlesFled;
        public int totalKosScored;
        public int totalKosTaken;
        public int totalCaptures;
        public int entangledCaptures;
        public int captureAttempts;
        public int captureOrbsUsed;
        public int captureTimeouts;
        public int captureTargetsEscaped;
        public int capturePerfectLocks;
        public int capturesAboveLowHp;
        public string capturesByOrbJson;
        public int fastestCaptureMilliseconds;
        public int extremeProfileCaptures;
        public int entangleOrbCaptures;
        public List<string> captureAchievementIds = new List<string>();
        public int totalRebirths;
        public int totalStarUps;
        public int totalMasteryCycles;
        public long totalRamEarned;
        public long totalRamSpent;
        public int totalKnownMovesAcquired;
        public int totalStampedMovesCreated;
        public int uniqueSpeciesCaptured;
    }

    [Serializable]
    public class TrenchesLeaderboardSnapshot
    {
        public List<TrenchesLeaderboardEntry> entries = new List<TrenchesLeaderboardEntry>();
    }

    [Serializable]
    public class TrenchesLeaderboardEntry
    {
        public string playerProfileId;
        public string displayName;
        public int rank;
        public int score;
    }

    [Serializable]
    public class ChainlingDexSnapshot
    {
        public List<string> seenSpeciesIds = new List<string>();
        public List<string> caughtSpeciesIds = new List<string>();
    }

    [Serializable]
    public class ChainlingRebirthResult : MenuMutationResult
    {
        public ChainlingSnapshot updatedChainling;
        public long ramBalance;
    }

    [Serializable]
    public class VisualIdentityMutationResult : MenuMutationResult
    {
        public ChainlingVisualIdentity visualIdentity;

        public static VisualIdentityMutationResult Ok(ChainlingVisualIdentity identity)
        {
            return new VisualIdentityMutationResult
            {
                success = true,
                error = "",
                visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(identity),
            };
        }

        public new static VisualIdentityMutationResult Failed(string message)
        {
            return new VisualIdentityMutationResult
            {
                success = false,
                error = message ?? "",
                visualIdentity = null,
            };
        }
    }

    public interface IPlayerRosterProvider
    {
        Task<PlayerRosterSnapshot> FetchRosterAsync(string playerProfileId, string walletAddress);
    }

    public interface IMoveInventoryProvider
    {
        Task<MoveInventorySnapshot> FetchMoveInventoryAsync(string playerProfileId, string walletAddress);
    }

    public interface IMenuMoveEquipService
    {
        Task<MenuMutationResult> EquipMoveAsync(string playerProfileId, string walletAddress, string chainlingId, int slot, string moveItemId);
    }

    public interface IMenuPartyLineupService
    {
        Task<MenuMutationResult> SavePartyLineupAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string[] orderedPartyChainlingBackendIds);

        Task<MenuMutationResult> ReleaseChainlingAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string chainlingBackendId);
    }

    public interface IMenuVisualIdentityService
    {
        Task<VisualIdentityMutationResult> SaveVisualIdentityAsync(
            string playerProfileId,
            string walletAddress,
            string authUserId,
            string chainlingBackendId,
            ChainlingVisualIdentity visualIdentity,
            IReadOnlyCollection<string> ownedCosmeticIds,
            PlayerRosterSnapshot rosterCacheToPatch,
            ChainlingVisualIdentityDatabaseSO identityDatabase = null,
            string speciesId = null,
            IReadOnlyDictionary<string, string> cosmeticAssetIdsByCosmeticId = null);
    }

    public interface IChainlingsMenuTestHarness
    {
        string HarnessLabel { get; }

        Task<PlayerRosterSnapshot> RegenerateRosterAsync(string playerProfileId, string walletAddress);

        Task<MenuMutationResult> RandomizeLineupAsync(string playerProfileId, string walletAddress);
    }

    public interface ICosmeticInventoryService
    {
        Task<IReadOnlyList<string>> FetchOwnedCosmeticIdsAsync(string playerProfileId, string walletAddress);
    }

    public interface ICosmeticInventoryProvenanceService
    {
        IReadOnlyDictionary<string, string> GetAssetIdsByCosmeticId();

        bool TryGetAssetIdForCosmeticId(string cosmeticId, out string assetId);

        void ClearCosmeticCache();
    }

    public interface IInventoryService
    {
        Task<InventorySnapshot> FetchInventoryAsync(string playerProfileId, string walletAddress);
    }

    /// <summary>
    /// Bag grant / consume seam named in the catalog spec. World pickups send
    /// <c>spawn_id</c> only; the server looks up the reward.
    /// </summary>
    public interface IPlayerItemInventory
    {
        Task<WorldPickupGrantResult> GrantWorldPickupAsync(string spawnId);
    }

    [Serializable]
    public class WorldPickupGrantResult
    {
        public bool ok;
        public bool alreadyClaimed;
        public string itemId;
        public int quantity;
        public int remainingQuantity;
        public string error;

        public static WorldPickupGrantResult Ok(
            string itemId,
            int quantity,
            int remainingQuantity,
            bool alreadyClaimed = false)
        {
            return new WorldPickupGrantResult
            {
                ok = true,
                alreadyClaimed = alreadyClaimed,
                itemId = itemId ?? "",
                quantity = quantity,
                remainingQuantity = remainingQuantity,
                error = "",
            };
        }

        public static WorldPickupGrantResult Failed(string message)
        {
            return new WorldPickupGrantResult
            {
                ok = false,
                alreadyClaimed = false,
                itemId = "",
                quantity = 0,
                remainingQuantity = 0,
                error = message ?? "",
            };
        }
    }

    /// <summary>
    /// Optional provenance for Core-backed bag items (asset_id ↔ item_id).
    /// Mirrors <see cref="ICosmeticInventoryProvenanceService"/>.
    /// </summary>
    public interface IInventoryItemProvenanceService
    {
        IReadOnlyDictionary<string, string> GetAssetIdsByItemId();

        bool TryGetAssetIdForItemId(string itemId, out string assetId);

        void ClearItemInventoryCache();
    }

    public interface IPlayerItemUseService
    {
        Task<PlayerItemUseResult> UseItemAsync(
            string playerProfileId,
            string walletAddress,
            PlayerItemUseRequest request);
    }

    public interface IFavorService
    {
        int CurrentFavorScore { get; }

        int AmpouleFavorPoints { get; }

        int PartyFavorPoints { get; }

        int AmpouleBattlesRemaining { get; }

        void SetAmpouleFavor(int points, int durationBattles);

        void SetPartyFavorGrants(IReadOnlyList<int> grantPoints);

        void ClearAmpoule();

        void NotifyBattleCompleted();

        void SetRankedLock(bool ranked);
    }

    /// <summary>
    /// Metaplex Core collectible consume pipeline (prepare → wallet sign → finalize).
    /// Unity never treats client-side ownership lists as authority.
    /// </summary>
    public interface ICoreAssetConsumeService
    {
        Task<CoreItemConsumePrepareResult> PrepareConsumeAsync(
            string playerProfileId,
            string walletAddress,
            string itemId,
            string assetId,
            string reason);

        Task<CoreItemConsumeFinalizeResult> FinalizeConsumeAsync(
            string playerProfileId,
            string walletAddress,
            string consumeIntentId,
            string txSignature);
    }

    public interface IPlayerStatsProvider
    {
        Task<PlayerStatsSnapshot> FetchStatsAsync(string playerProfileId, string walletAddress);
    }

    public interface ITrenchesLeaderboardService
    {
        Task<TrenchesLeaderboardSnapshot> FetchLeaderboardAsync(string playerProfileId, string walletAddress);
    }

    public interface IChainlingDexService
    {
        Task<ChainlingDexSnapshot> FetchDexAsync(string playerProfileId, string walletAddress);
    }

    public interface IChainlingRebirthService
    {
        Task<ChainlingRebirthResult> RebirthAsync(
            string playerProfileId,
            string walletAddress,
            string chainlingId,
            CoreStat chosenStat);
    }

    public interface IFeatureUnlockService
    {
        bool IsHandbookUnlocked { get; }
        Task<bool> RefreshHandbookUnlockedAsync(string playerProfileId, string walletAddress);
    }
}
