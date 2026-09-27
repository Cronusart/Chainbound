using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class StubCosmeticInventoryService : MonoBehaviour, ICosmeticInventoryService
    {
        [SerializeField] private List<string> devOwnedCosmeticIds = new List<string>();

        public Task<IReadOnlyList<string>> FetchOwnedCosmeticIdsAsync(string playerProfileId, string walletAddress)
        {
            return Task.FromResult((IReadOnlyList<string>)devOwnedCosmeticIds);
        }
    }

    public class StubInventoryService : MonoBehaviour, IInventoryService
    {
        [SerializeField] private long devRamBalance = 9999;
        [SerializeField] private List<string> devConsumableIds = new List<string>();
        [SerializeField] private List<string> devCosmeticAssetIds = new List<string>();
        [SerializeField] private bool seedCatalogDefaults = true;

        public Task<InventorySnapshot> FetchInventoryAsync(string playerProfileId, string walletAddress)
        {
            var stacks = new List<MenuInventoryItemStack>();
            if (seedCatalogDefaults && (devConsumableIds == null || devConsumableIds.Count == 0))
            {
                SeedDefaultStacks(stacks);
            }
            else if (devConsumableIds != null)
            {
                var counts = new Dictionary<string, int>(System.StringComparer.Ordinal);
                for (int i = 0; i < devConsumableIds.Count; i++)
                {
                    string id = devConsumableIds[i];
                    if (string.IsNullOrWhiteSpace(id))
                        continue;
                    counts.TryGetValue(id, out int n);
                    counts[id] = n + 1;
                }

                foreach (KeyValuePair<string, int> pair in counts)
                    stacks.Add(PlayerItemCatalogSeed.CreateStack(pair.Key, pair.Value));
            }

            return Task.FromResult(new InventorySnapshot
            {
                connectivity = string.IsNullOrWhiteSpace(walletAddress) ? MenuConnectivityState.Offline : MenuConnectivityState.Ready,
                ramBalance = devRamBalance,
                consumableIds = FlattenIds(stacks),
                cosmeticAssetIds = new List<string>(devCosmeticAssetIds ?? new List<string>()),
                stacks = stacks,
                walletPublicKey = walletAddress ?? "",
            });
        }

        private static void SeedDefaultStacks(List<MenuInventoryItemStack> stacks)
        {
            stacks.Add(PlayerItemCatalogSeed.CreateStack("chainling_update", 5));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("chainling_reboot", 2));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("signal_orb", 8));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("lattice_orb", 3));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("favor_boost_s", 2));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("party_patch", 2));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("system_flush", 1));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("stamp_solvent", 1));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("archive_key", 1, "DevArchiveKey111111111111111111111111111"));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("case_binding_standard", 1, "DevCaseBinding1111111111111111111111111"));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("signature_extract", 1, "DevSigExtract11111111111111111111111111"));
            stacks.Add(PlayerItemCatalogSeed.CreateStack("archive_severance", 1, "DevSeverance111111111111111111111111111"));
        }

        private static List<string> FlattenIds(List<MenuInventoryItemStack> stacks)
        {
            var ids = new List<string>();
            for (int i = 0; i < stacks.Count; i++)
            {
                MenuInventoryItemStack stack = stacks[i];
                if (stack == null || string.IsNullOrWhiteSpace(stack.itemId))
                    continue;
                for (int q = 0; q < stack.quantity; q++)
                    ids.Add(stack.itemId);
            }

            return ids;
        }
    }

    public class StubMenuMoveEquipService : MonoBehaviour, IMenuMoveEquipService
    {
        [SerializeField] private bool succeedInDev = true;

        public Task<MenuMutationResult> EquipMoveAsync(string playerProfileId, string walletAddress, string chainlingId, int slot, string moveItemId)
        {
            if (!succeedInDev)
                return Task.FromResult(MenuMutationResult.Failed("move_equip_stub_rejected"));

            Debug.Log(
                $"[StubMoveEquipService] EquipMove profile={playerProfileId} chainling={chainlingId} slot={slot} moveItem={moveItemId}",
                this);
            return Task.FromResult(MenuMutationResult.Ok());
        }
    }

    public class StubTrenchesLeaderboardService : MonoBehaviour, ITrenchesLeaderboardService
    {
        [SerializeField] private List<TrenchesLeaderboardEntry> devEntries = new List<TrenchesLeaderboardEntry>();

        public Task<TrenchesLeaderboardSnapshot> FetchLeaderboardAsync(string playerProfileId, string walletAddress)
        {
            return Task.FromResult(new TrenchesLeaderboardSnapshot
            {
                entries = new List<TrenchesLeaderboardEntry>(devEntries),
            });
        }
    }

    public class StubChainlingDexService : MonoBehaviour, IChainlingDexService
    {
        [SerializeField] private List<string> devSeenSpeciesIds = new List<string>();
        [SerializeField] private List<string> devCaughtSpeciesIds = new List<string>();

        public Task<ChainlingDexSnapshot> FetchDexAsync(string playerProfileId, string walletAddress)
        {
            return Task.FromResult(new ChainlingDexSnapshot
            {
                seenSpeciesIds = new List<string>(devSeenSpeciesIds),
                caughtSpeciesIds = new List<string>(devCaughtSpeciesIds),
            });
        }
    }
}
