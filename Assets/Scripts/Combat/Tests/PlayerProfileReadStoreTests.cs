using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class PlayerProfileReadStoreTests
    {
        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            created.Clear();
        }

        [Test]
        public async Task RefreshRosterAsync_JoinsConcurrentReadersForOneProfile()
        {
            PlayerProfileReadStore store = CreateStore();
            var provider = new ControlledRosterProvider();

            Task<PlayerRosterSnapshot> first = store.RefreshRosterAsync(provider, "profile-a", "wallet-a");
            Task<PlayerRosterSnapshot> second = store.RefreshRosterAsync(provider, "profile-a", "wallet-a");

            Assert.AreEqual(1, provider.CallCount);
            var snapshot = new PlayerRosterSnapshot { playerProfileId = "profile-a" };
            provider.Complete(snapshot);

            Assert.AreSame(snapshot, await first);
            Assert.AreSame(snapshot, await second);
            Assert.IsTrue(store.TryGetRoster("profile-a", "wallet-a", out PlayerRosterSnapshot cached));
            Assert.AreSame(snapshot, cached);
        }

        [Test]
        public async Task RefreshInventoryAsync_PreservesLastSuccessfulSnapshotWhenRefreshFails()
        {
            PlayerProfileReadStore store = CreateStore();
            var provider = new SequencedInventoryProvider(
                new InventorySnapshot { stacks = new List<MenuInventoryItemStack>() },
                null);

            InventorySnapshot first = await store.RefreshInventoryAsync(provider, "profile-a", "wallet-a");
            InventorySnapshot second = await store.RefreshInventoryAsync(provider, "profile-a", "wallet-a");

            Assert.AreSame(first, second);
            Assert.IsTrue(store.TryGetInventory("profile-a", "wallet-a", out InventorySnapshot cached));
            Assert.AreSame(first, cached);
        }

        [Test]
        public async Task RefreshRosterAsync_KeepsProfileSnapshotsIsolated()
        {
            PlayerProfileReadStore store = CreateStore();
            var provider = new SequencedRosterProvider(
                new PlayerRosterSnapshot { playerProfileId = "profile-a" },
                new PlayerRosterSnapshot { playerProfileId = "profile-b" });

            await store.RefreshRosterAsync(provider, "profile-a", "wallet-a");
            await store.RefreshRosterAsync(provider, "profile-b", "wallet-b");

            Assert.IsTrue(store.TryGetRoster("profile-a", "wallet-a", out PlayerRosterSnapshot first));
            Assert.IsTrue(store.TryGetRoster("profile-b", "wallet-b", out PlayerRosterSnapshot second));
            Assert.AreEqual("profile-a", first.playerProfileId);
            Assert.AreEqual("profile-b", second.playerProfileId);
        }

        private PlayerProfileReadStore CreateStore()
        {
            var gameObject = new GameObject("PlayerProfileReadStoreTests");
            created.Add(gameObject);
            return gameObject.AddComponent<PlayerProfileReadStore>();
        }

        private sealed class ControlledRosterProvider : IPlayerRosterProvider
        {
            private readonly TaskCompletionSource<PlayerRosterSnapshot> completion = new TaskCompletionSource<PlayerRosterSnapshot>();
            public int CallCount { get; private set; }

            public Task<PlayerRosterSnapshot> FetchRosterAsync(string playerProfileId, string walletAddress)
            {
                CallCount++;
                return completion.Task;
            }

            public void Complete(PlayerRosterSnapshot snapshot) => completion.SetResult(snapshot);
        }

        private sealed class SequencedRosterProvider : IPlayerRosterProvider
        {
            private readonly Queue<PlayerRosterSnapshot> results;
            public SequencedRosterProvider(params PlayerRosterSnapshot[] results) => this.results = new Queue<PlayerRosterSnapshot>(results);
            public Task<PlayerRosterSnapshot> FetchRosterAsync(string playerProfileId, string walletAddress) => Task.FromResult(results.Dequeue());
        }

        private sealed class SequencedInventoryProvider : IInventoryService
        {
            private readonly Queue<InventorySnapshot> results;
            public SequencedInventoryProvider(params InventorySnapshot[] results) => this.results = new Queue<InventorySnapshot>(results);
            public Task<InventorySnapshot> FetchInventoryAsync(string playerProfileId, string walletAddress) => Task.FromResult(results.Dequeue());
        }
    }
}
