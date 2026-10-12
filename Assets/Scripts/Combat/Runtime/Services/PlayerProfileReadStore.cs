using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Persistent, profile-scoped read-through cache for the menu's roster and bag data.
    /// It deliberately owns no presentation state: callers can render its last successful
    /// snapshot immediately while all readers join one in-flight Supabase request.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerProfileReadStore : MonoBehaviour
    {
        private sealed class Entry
        {
            public PlayerRosterSnapshot roster;
            public InventorySnapshot inventory;
            public Task<PlayerRosterSnapshot> rosterTask;
            public Task<InventorySnapshot> inventoryTask;
            public IPlayerRosterProvider rosterProvider;
            public IInventoryService inventoryProvider;
            public string profileId;
            public string walletAddress;
        }

        private static PlayerProfileReadStore instance;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private SupabasePlayerRosterProvider defaultRosterProvider;
        private CoreBagItemInventoryService defaultInventoryProvider;

        public static PlayerProfileReadStore ResolveOrCreate()
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<PlayerProfileReadStore>(FindObjectsInactive.Include);
            if (instance != null)
                return instance;

            Transform host = GameSessionContext.Instance != null ? GameSessionContext.Instance.transform : null;
            GameObject serviceObject = host != null ? host.gameObject : new GameObject("[PlayerProfileReadStore]");
            if (host == null && Application.isPlaying)
                DontDestroyOnLoad(serviceObject);
            instance = serviceObject.GetComponent<PlayerProfileReadStore>() ?? serviceObject.AddComponent<PlayerProfileReadStore>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
        }

        private void OnEnable()
        {
            PlayerRosterRefreshSignal.ResolveOrCreate().Changed += HandleRosterChanged;
        }

        private void OnDisable()
        {
            if (instance != null)
                PlayerRosterRefreshSignal.ResolveOrCreate().Changed -= HandleRosterChanged;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public Task HydrateCurrentProfileAsync()
        {
            if (!TryResolveCurrentIdentity(out string profileId, out string walletAddress))
                return Task.CompletedTask;

            EnsureProductionProviders();
            return HydrateAsync(defaultRosterProvider, defaultInventoryProvider, profileId, walletAddress);
        }

        public async Task HydrateAsync(
            IPlayerRosterProvider rosterProvider,
            IInventoryService inventoryProvider,
            string playerProfileId,
            string walletAddress)
        {
            Task<PlayerRosterSnapshot> rosterTask = RefreshRosterAsync(rosterProvider, playerProfileId, walletAddress);
            Task<InventorySnapshot> inventoryTask = RefreshInventoryAsync(inventoryProvider, playerProfileId, walletAddress);
            await Task.WhenAll(rosterTask, inventoryTask);
        }

        public bool TryGetRoster(string playerProfileId, string walletAddress, out PlayerRosterSnapshot snapshot)
        {
            snapshot = null;
            return TryGetEntry(playerProfileId, walletAddress, out Entry entry) && (snapshot = entry.roster) != null;
        }

        public bool TryGetInventory(string playerProfileId, string walletAddress, out InventorySnapshot snapshot)
        {
            snapshot = null;
            return TryGetEntry(playerProfileId, walletAddress, out Entry entry) && (snapshot = entry.inventory) != null;
        }

        public Task<PlayerRosterSnapshot> RefreshRosterAsync(
            IPlayerRosterProvider provider,
            string playerProfileId,
            string walletAddress)
        {
            if (provider == null || !TryGetOrCreateEntry(playerProfileId, walletAddress, out Entry entry))
                return Task.FromResult<PlayerRosterSnapshot>(null);

            entry.rosterProvider = provider;
            if (entry.rosterTask != null && !entry.rosterTask.IsCompleted)
                return entry.rosterTask;

            entry.rosterTask = RefreshRosterCoreAsync(entry, provider);
            return entry.rosterTask;
        }

        public Task<InventorySnapshot> RefreshInventoryAsync(
            IInventoryService provider,
            string playerProfileId,
            string walletAddress)
        {
            if (provider == null || !TryGetOrCreateEntry(playerProfileId, walletAddress, out Entry entry))
                return Task.FromResult<InventorySnapshot>(null);

            entry.inventoryProvider = provider;
            if (entry.inventoryTask != null && !entry.inventoryTask.IsCompleted)
                return entry.inventoryTask;

            entry.inventoryTask = RefreshInventoryCoreAsync(entry, provider);
            return entry.inventoryTask;
        }

        public void PublishInventory(string playerProfileId, string walletAddress, InventorySnapshot snapshot)
        {
            if (snapshot != null && TryGetOrCreateEntry(playerProfileId, walletAddress, out Entry entry))
                entry.inventory = snapshot;
        }

        public void MarkInventoryStale(string playerProfileId, string walletAddress)
        {
            if (!TryGetEntry(playerProfileId, walletAddress, out Entry entry))
                return;
            if (entry.inventoryProvider is CoreBagItemInventoryService core)
                core.MarkItemInventoryCacheStale();
            else if (entry.inventoryProvider is IInventoryItemProvenanceService cache)
                cache.ClearItemInventoryCache();
            if (entry.inventoryProvider != null)
                _ = RefreshInventoryAsync(entry.inventoryProvider, entry.profileId, entry.walletAddress);
        }

        private async Task<PlayerRosterSnapshot> RefreshRosterCoreAsync(Entry entry, IPlayerRosterProvider provider)
        {
            PlayerRosterSnapshot loaded = await provider.FetchRosterAsync(entry.profileId, entry.walletAddress);
            if (loaded != null)
                entry.roster = loaded;
            return loaded ?? entry.roster;
        }

        private async Task<InventorySnapshot> RefreshInventoryCoreAsync(Entry entry, IInventoryService provider)
        {
            // Force the raw provider past its TTL while retaining this store's last good snapshot.
            if (provider is CoreBagItemInventoryService core)
                core.MarkItemInventoryCacheStale();
            else if (provider is IInventoryItemProvenanceService cache)
                cache.ClearItemInventoryCache();
            InventorySnapshot loaded = await provider.FetchInventoryAsync(entry.profileId, entry.walletAddress);
            if (loaded != null && string.IsNullOrWhiteSpace(loaded.lastError))
                entry.inventory = loaded;
            return loaded ?? entry.inventory;
        }

        private void HandleRosterChanged(PlayerRosterRefreshSignal.Notice notice)
        {
            if (notice.PlayerProfileId == null || !entries.TryGetValue(ProfileKey(notice.PlayerProfileId, string.Empty), out Entry entry))
                return;
            if (entry.rosterProvider != null)
                _ = RefreshRosterAsync(entry.rosterProvider, entry.profileId, entry.walletAddress);
        }

        private void EnsureProductionProviders()
        {
            if (defaultRosterProvider == null)
                defaultRosterProvider = GetComponent<SupabasePlayerRosterProvider>() ?? gameObject.AddComponent<SupabasePlayerRosterProvider>();
            if (defaultInventoryProvider == null)
                defaultInventoryProvider = GetComponent<CoreBagItemInventoryService>() ?? gameObject.AddComponent<CoreBagItemInventoryService>();

            SupabaseConfigSO config = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
            if (config != null)
            {
                defaultRosterProvider.Configure(config);
                defaultInventoryProvider.Configure(config);
            }
        }

        private static bool TryResolveCurrentIdentity(out string profileId, out string walletAddress)
        {
            GameSessionContext session = GameSessionContext.Instance;
            profileId = session != null ? (session.PlayerProfileId ?? string.Empty).Trim() : string.Empty;
            walletAddress = session != null ? (session.WalletAddress ?? string.Empty).Trim() : string.Empty;
            return profileId.Length > 0 || (session != null && session.IsGuestSession && walletAddress.Length > 0);
        }

        private bool TryGetEntry(string profileId, string walletAddress, out Entry entry)
        {
            return entries.TryGetValue(ProfileKey(profileId, walletAddress), out entry);
        }

        private bool TryGetOrCreateEntry(string profileId, string walletAddress, out Entry entry)
        {
            string key = ProfileKey(profileId, walletAddress);
            if (key.Length == 0)
            {
                entry = null;
                return false;
            }
            if (!entries.TryGetValue(key, out entry))
            {
                entry = new Entry
                {
                    profileId = (profileId ?? string.Empty).Trim(),
                    walletAddress = (walletAddress ?? string.Empty).Trim(),
                };
                entries.Add(key, entry);
            }
            return true;
        }

        private static string ProfileKey(string profileId, string walletAddress)
        {
            string profile = (profileId ?? string.Empty).Trim();
            if (profile.Length > 0)
                return "profile:" + profile;
            string wallet = (walletAddress ?? string.Empty).Trim();
            return wallet.Length > 0 ? "wallet:" + wallet : string.Empty;
        }
    }
}
