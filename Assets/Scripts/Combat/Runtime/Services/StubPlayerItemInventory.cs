using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Editor / sandbox world-pickup grant. Records spawn ids in memory and never
    /// writes SQL. Seeded with Link 1 / Jungle backpacks and Mart leftover crates.
    /// </summary>
    public sealed class StubPlayerItemInventory : MonoBehaviour, IPlayerItemInventory, IInventoryService
    {
        private readonly HashSet<string> claimedSpawns = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpawnReward> spawnCatalog =
            new Dictionary<string, SpawnReward>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> stacks = new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyCollection<string> ClaimedSpawnIds => claimedSpawns;

        public static StubPlayerItemInventory EnsureInstance()
        {
            StubPlayerItemInventory existing = FindFirstObjectByType<StubPlayerItemInventory>(FindObjectsInactive.Include);
            if (existing != null)
                return existing;

            GameObject go = new GameObject("StubPlayerItemInventory");
            if (Application.isPlaying)
                DontDestroyOnLoad(go);
            return go.AddComponent<StubPlayerItemInventory>();
        }

        private void Awake()
        {
            SeedDefaultSpawns();
        }

        public void RegisterSpawn(string spawnId, string itemId, int quantity)
        {
            string spawn = (spawnId ?? "").Trim();
            string item = (itemId ?? "").Trim();
            if (spawn.Length == 0 || item.Length == 0)
                return;

            spawnCatalog[spawn] = new SpawnReward
            {
                itemId = item,
                quantity = Mathf.Max(1, quantity),
            };
        }

        public static bool TryResolveDefaultSpawn(string spawnId, out string itemId, out int quantity)
        {
            switch ((spawnId ?? string.Empty).Trim())
            {
                case "world.pickup.link1.backpack_01":
                case "world.pickup.jungle.backpack_01":
                case "world.pickup.loomville_mart.open_crate_patch":
                    itemId = "chainling_patch";
                    quantity = 1;
                    return true;
                case "world.pickup.loomville_mart.open_crate_orb":
                    itemId = "signal_orb";
                    quantity = 1;
                    return true;
                default:
                    itemId = string.Empty;
                    quantity = 0;
                    return false;
            }
        }

        public Task<WorldPickupGrantResult> GrantWorldPickupAsync(string spawnId)
        {
            SeedDefaultSpawns();

            string normalized = (spawnId ?? "").Trim();
            if (normalized.Length == 0)
                return Task.FromResult(WorldPickupGrantResult.Failed("spawn_id_required"));

            if (!spawnCatalog.TryGetValue(normalized, out SpawnReward reward))
                return Task.FromResult(WorldPickupGrantResult.Failed("unknown_spawn"));

            if (!PlayerItemCatalogSeed.TryGet(reward.itemId, out PlayerItemDefinition def) || def == null)
                return Task.FromResult(WorldPickupGrantResult.Failed("unknown_item"));

            if (def.ownershipKind != PlayerItemOwnershipKind.ProfileStack)
                return Task.FromResult(WorldPickupGrantResult.Failed("core_item_not_grantable"));

            stacks.TryGetValue(reward.itemId, out int remaining);
            if (claimedSpawns.Contains(normalized))
            {
                return Task.FromResult(WorldPickupGrantResult.Ok(
                    reward.itemId,
                    reward.quantity,
                    remaining,
                    alreadyClaimed: true));
            }

            claimedSpawns.Add(normalized);
            int next = remaining + reward.quantity;
            if (def.maxStack > 0)
                next = Mathf.Min(def.maxStack, next);
            stacks[reward.itemId] = next;

            return Task.FromResult(WorldPickupGrantResult.Ok(
                reward.itemId,
                reward.quantity,
                next,
                alreadyClaimed: false));
        }

        public Task<InventorySnapshot> FetchInventoryAsync(string playerProfileId, string walletAddress)
        {
            SeedDefaultSpawns();
            var snapshot = new InventorySnapshot
            {
                connectivity = MenuConnectivityState.Ready,
                ramBalance = 0,
                stacks = new List<MenuInventoryItemStack>(),
                consumableIds = new List<string>(),
                cosmeticAssetIds = new List<string>(),
                walletPublicKey = (walletAddress ?? string.Empty).Trim(),
                cluster = "editor",
                lastError = string.Empty,
            };

            foreach (KeyValuePair<string, int> pair in stacks)
            {
                if (pair.Value <= 0)
                    continue;

                snapshot.stacks.Add(PlayerItemCatalogSeed.CreateStack(pair.Key, pair.Value));
                for (int i = 0; i < pair.Value; i++)
                    snapshot.consumableIds.Add(pair.Key);
            }

            return Task.FromResult(snapshot);
        }

        private void SeedDefaultSpawns()
        {
            if (spawnCatalog.Count > 0)
                return;

            RegisterSpawn("world.pickup.link1.backpack_01", "chainling_patch", 1);
            RegisterSpawn("world.pickup.jungle.backpack_01", "chainling_patch", 1);
            RegisterSpawn("world.pickup.loomville_mart.open_crate_patch", "chainling_patch", 1);
            RegisterSpawn("world.pickup.loomville_mart.open_crate_orb", "signal_orb", 1);
        }

        private struct SpawnReward
        {
            public string itemId;
            public int quantity;
        }
    }
}
