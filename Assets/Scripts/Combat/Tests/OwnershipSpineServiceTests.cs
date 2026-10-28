using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class OwnershipSpineServiceTests
    {
        private ReadyWalletCoordinator walletCoordinator;

        [SetUp]
        public void SetUp()
        {
            walletCoordinator = new ReadyWalletCoordinator();
            ProfileWalletAccessRegistry.Register(walletCoordinator);
        }

        [TearDown]
        public void TearDown() => ProfileWalletAccessRegistry.Unregister(walletCoordinator);

        [Test]
        public void StoredLegacyValuesMapOnlyToKnownOrStamped()
        {
            Assert.AreEqual(MoveTier.Known, MoveTierMigrationAdapter.FromStoredValue("ordinal", null, null));
            Assert.AreEqual(MoveTier.Stamped, MoveTierMigrationAdapter.FromStoredValue("inscription", "m1", null));
            Assert.AreEqual(MoveTier.Stamped, MoveTierMigrationAdapter.FromStoredValue("rune", "m1", "m2"));
        }

        [Test]
        public async Task GetKnownDoesNotRequireWallet()
        {
            var gateway = new FakeGateway { IsWalletConnected = false };
            var service = Create(gateway);
            KnownMoveRecord result = await service.GetKnownAsync("roll-1");
            Assert.AreEqual(0, result.modifierCount);
            Assert.AreEqual(1, service.CachedKnown.Count);
            Assert.AreEqual(95, service.RamBalance);
        }

        [Test]
        public async Task KnownBundleReturnsEveryPullInOrderAndSpendsOnce()
        {
            var gateway = new FakeGateway { IsWalletConnected = false };
            var service = Create(gateway);

            IReadOnlyList<KnownMoveRecord> results = await service.GetKnownBundleAsync("bundle-1", 5);

            Assert.AreEqual(5, results.Count);
            for (int i = 0; i < results.Count; i++)
                Assert.AreEqual($"bundle-1#{i}", results[i].instanceId);
            Assert.AreEqual(1, gateway.BundleCalls);
            Assert.AreEqual(0, gateway.SingleCalls);
            Assert.AreEqual(75, service.RamBalance);
            Assert.AreEqual(5, service.CachedKnown.Count);
        }

        [Test]
        public async Task KnownBundleOfOneUsesTheSingleRoll()
        {
            var gateway = new FakeGateway { IsWalletConnected = false };
            var service = Create(gateway);

            IReadOnlyList<KnownMoveRecord> results = await service.GetKnownBundleAsync("bundle-1", 1);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(1, gateway.SingleCalls);
            Assert.AreEqual(0, gateway.BundleCalls);
        }

        [Test]
        public void KnownBundleRejectsAShortAuthorityResponse()
        {
            var gateway = new FakeGateway { IsWalletConnected = false, BundleDropsLastPull = true };
            var service = Create(gateway);

            Assert.ThrowsAsync<InvalidOperationException>(() => service.GetKnownBundleAsync("bundle-1", 5));
            Assert.AreEqual(0, service.CachedKnown.Count);
        }

        [Test]
        public void KnownBundleRejectsOutOfRangePullCounts()
        {
            var service = Create(new FakeGateway());

            Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.GetKnownBundleAsync("bundle-1", 0));
            Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => service.GetKnownBundleAsync("bundle-1", OwnershipSpineService.MaxKnownBundlePulls + 1));
        }

        [Test]
        public async Task RefreshHydratesKnownStampedAndRamTogether()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, RamBalance = 42 };
            gateway.Known.Add(new KnownMoveRecord
            {
                instanceId = "known-2", moveId = "jab", modifierCount = 0, generatorVersion = 1, poolHash = "hash",
            });
            gateway.Verified.Add(TrustedStamped());
            var service = Create(gateway);

            await service.RefreshAsync();

            Assert.AreEqual(42, service.RamBalance);
            Assert.AreEqual(1, service.CachedKnown.Count);
            Assert.AreEqual(1, service.CachedStamped.Count);
        }

        [Test]
        public void DevicePlayersNeverResolveToLocalPreview()
        {
            Assert.AreEqual(
                CandyMachineRuntimeMode.LocalPreview,
                CandyMachineScreen.ResolveRuntimeMode(CandyMachineRuntimeMode.LocalPreview, true, true));
            Assert.AreEqual(
                CandyMachineRuntimeMode.DevnetOwnership,
                CandyMachineScreen.ResolveRuntimeMode(CandyMachineRuntimeMode.LocalPreview, false, true));
            Assert.AreEqual(
                CandyMachineRuntimeMode.DevnetOwnership,
                CandyMachineScreen.ResolveRuntimeMode(CandyMachineRuntimeMode.LocalPreview, false, false));
            Assert.AreEqual(1, CandyMachineScreen.StampSlotsPerAction);
        }

        [Test]
        public async Task FailedStampKeepsKnown()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitSuccess = false };
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");
            StampMoveResult result = await service.StampMoveAsync(Request());
            Assert.IsFalse(result.success);
            Assert.AreEqual(1, service.CachedKnown.Count);
        }

        [Test]
        public async Task ConfirmedStampReceiptRemovesKnownWithoutWaitingForInventoryRefresh()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitSuccess = true };
            gateway.Verified.Add(TrustedStamped());
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");
            int refreshesBeforeStamp = gateway.FetchStateCalls;
            StampMoveResult result = await service.StampMoveAsync(Request());
            Assert.IsTrue(result.success);
            Assert.NotNull(result.verifiedAsset);
            Assert.AreEqual(0, service.CachedKnown.Count);
            Assert.AreEqual(1, service.CachedStamped.Count);
            Assert.AreEqual(
                refreshesBeforeStamp + 1,
                gateway.FetchStateCalls,
                "Stamp performs one canonical preflight refresh but must not wait on a post-confirm inventory refresh.");
        }

        [Test]
        public async Task PendingStampKeepsKnownAndCanSettleWithoutAnotherSignature()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitPending = true };
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");

            StampMoveResult pending = await service.StampMoveAsync(Request());

            Assert.IsTrue(pending.success);
            Assert.IsTrue(pending.pending);
            Assert.AreEqual(1, service.CachedKnown.Count);
            Assert.AreEqual(1, gateway.SubmitCalls);

            gateway.SubmitPending = false;
            gateway.Verified.Add(TrustedStamped());
            StampMoveResult settled = await service.ReconcilePendingStampAsync("stamp-1");

            Assert.IsTrue(settled.confirmed);
            Assert.NotNull(settled.verifiedAsset);
            Assert.AreEqual(LibraryCardFaces.ProgressionLearn, settled.acquisition);
            Assert.AreEqual(LibraryCardFaces.ProgressionLearn, settled.verifiedAsset.acquisition);
            Assert.AreEqual(0, service.CachedKnown.Count);
            Assert.AreEqual(1, gateway.SubmitCalls, "reconciliation must not ask the wallet to sign again");
            Assert.AreEqual(1, gateway.ReconcileCalls);
        }

        [Test]
        public async Task ReconcileNoneRecoversConfirmedAssetAndClearsStalePendingState()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitPending = true };
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");
            await service.StampMoveAsync(Request());
            Assert.NotNull(service.PendingStamp);

            gateway.SubmitPending = false;
            gateway.ReconcileReturnsNone = true;
            gateway.Known.Clear();
            gateway.RamBalance -= 75;
            gateway.Verified.Add(TrustedStamped());

            StampMoveResult settled = await service.ReconcilePendingStampAsync("stamp-1");

            Assert.IsTrue(settled.success);
            Assert.IsTrue(settled.confirmed);
            Assert.IsFalse(settled.pending);
            Assert.NotNull(settled.verifiedAsset);
            Assert.AreEqual(LibraryCardFaces.ProgressionLearn, settled.acquisition);
            Assert.AreEqual(LibraryCardFaces.ProgressionLearn, settled.verifiedAsset.acquisition);
            Assert.IsNull(service.PendingStamp);
            Assert.AreEqual(0, service.CachedKnown.Count);
            Assert.AreEqual(1, service.CachedStamped.Count);
        }

        [Test]
        public async Task ReconcileNoneWithoutAssetClearsStalePendingAndKeepsKnown()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitPending = true };
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");
            await service.StampMoveAsync(Request());
            Assert.NotNull(service.PendingStamp);

            gateway.SubmitPending = false;
            gateway.ReconcileReturnsNone = true;

            StampMoveResult settled = await service.ReconcilePendingStampAsync("stamp-1");

            Assert.IsTrue(settled.success);
            Assert.IsFalse(settled.confirmed);
            Assert.IsFalse(settled.pending);
            Assert.IsNull(service.PendingStamp);
            Assert.AreEqual(1, service.CachedKnown.Count);
            Assert.AreEqual(0, service.CachedStamped.Count);
        }

        [Test]
        public async Task ReconcileFailedIntentClearsPendingAndKeepsKnown()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitPending = true };
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");
            await service.StampMoveAsync(Request());
            Assert.NotNull(service.PendingStamp);

            gateway.SubmitPending = false;
            gateway.ReconcileReturnsFailed = true;
            StampMoveResult settled = await service.ReconcilePendingStampAsync("stamp-1");

            Assert.IsTrue(settled.success);
            Assert.AreEqual("failed", settled.operationStatus);
            Assert.IsNull(service.PendingStamp);
            Assert.AreEqual(1, service.CachedKnown.Count);
        }

        [Test]
        public void PreparedStampRequiresWalletSignatureButSubmittedStampDoesNot()
        {
            Assert.IsTrue(CandyMachineScreen.PendingStampRequiresWalletSignature(new PendingStampRecord
            {
                operationStatus = "prepared",
            }));
            Assert.IsFalse(CandyMachineScreen.PendingStampRequiresWalletSignature(new PendingStampRecord
            {
                operationStatus = "prepared",
                signature = "wallet-signature",
            }));
            Assert.IsFalse(CandyMachineScreen.PendingStampRequiresWalletSignature(new PendingStampRecord
            {
                operationStatus = "submitted",
            }));
            Assert.IsFalse(CandyMachineScreen.PendingStampIsFinalizing(new PendingStampRecord
            {
                operationStatus = "prepared",
            }));
            Assert.IsTrue(CandyMachineScreen.PendingStampIsFinalizing(new PendingStampRecord
            {
                operationStatus = "submitted",
                signature = "wallet-signature",
            }));
        }

        [Test]
        public async Task InsufficientRamBlocksReviewAndSubmitBeforeWalletOrGateway()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, RamBalance = 60 };
            var service = Create(gateway);
            await service.GetKnownAsync("roll-low-ram");
            Assert.AreEqual(55, service.RamBalance);
            Assert.IsFalse(CandyMachineScreen.HasSufficientRam(service.RamBalance, service.StampRamCost));

            InvalidOperationException reviewError = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await service.ReviewStampAsync(Request()));
            StringAssert.Contains("75 RAM is required", reviewError.Message);
            Assert.AreEqual(0, gateway.SimulateCalls);
            Assert.AreEqual(0, walletCoordinator.Calls);

            StampMoveResult submit = await service.StampMoveAsync(Request());
            Assert.IsFalse(submit.success);
            StringAssert.Contains("75 RAM is required", submit.error);
            Assert.AreEqual(0, gateway.SubmitCalls);
            Assert.AreEqual(0, walletCoordinator.Calls);
        }

        [Test]
        public async Task UntrustedPayloadIsDroppedAndDoesNotConsumeKnownCache()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitSuccess = true };
            StampedMoveRecord forged = TrustedStamped();
            forged.collectionAddress = "attacker";
            gateway.Verified.Add(forged);
            var service = Create(gateway);
            await service.GetKnownAsync("roll-1");
            StampMoveResult result = await service.StampMoveAsync(Request());
            Assert.IsFalse(result.success);
            Assert.AreEqual(1, service.CachedKnown.Count);
            Assert.AreEqual(0, service.CachedStamped.Count);
        }

        [Test]
        public async Task VerifiedCoreRecord_WithCategoryIllegalModifier_IsDropped()
        {
            var gateway = new FakeGateway { IsWalletConnected = true, SubmitSuccess = true };
            StampedMoveRecord illegal = TrustedStamped();
            illegal.moveId = "spite_counter";
            illegal.modifier1Id = "10.1-c";
            illegal.modifier1EffectValue = 25f;
            illegal.modifier1EffectValueLocked = true;
            gateway.Verified.Add(illegal);

            MoveDefinitionSO host = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            host.moveId = "spite_counter";
            host.displayName = "Spite Counter";
            host.category = MoveCategory.Passive;
            MoveDatabaseSO database = ScriptableObject.CreateInstance<MoveDatabaseSO>();
            database.moves = new List<MoveDefinitionSO> { host };
            database.BuildLookup();

            ModifierDefinitionSO modifier = ScriptableObject.CreateInstance<ModifierDefinitionSO>();
            modifier.sectionId = "10.1-c";
            modifier.effectMin = 20f;
            modifier.effectMax = 30f;
            modifier.allowedCategories = new[] { MoveCategory.DamagingMelee, MoveCategory.DamagingRanged };
            ModifierCatalogSO catalog = ScriptableObject.CreateInstance<ModifierCatalogSO>();
            catalog.modifiers = new List<ModifierDefinitionSO> { modifier };
            catalog.BuildLookup();

            var service = new OwnershipSpineService(gateway, "moves", "authority", 1, database, catalog);
            await service.GetKnownAsync("roll-1");
            StampMoveResult result = await service.StampMoveAsync(Request());

            Assert.IsFalse(result.success);
            Assert.AreEqual(1, service.CachedKnown.Count);
            Assert.AreEqual(0, service.CachedStamped.Count);
        }

        private static OwnershipSpineService Create(FakeGateway gateway) =>
            new OwnershipSpineService(gateway, "moves", "authority", 1);

        private static StampMoveRequest Request() => new StampMoveRequest
        {
            knownInstanceId = "known-1",
            requestedModifierCount = 1,
            idempotencyKey = "stamp-1",
        };

        private static StampedMoveRecord TrustedStamped() => new StampedMoveRecord
        {
            assetAddress = "asset",
            ownerAddress = "owner",
            collectionAddress = "moves",
            updateAuthority = "authority",
            moveId = "jab",
            knownInstanceId = "known-1",
            modifierCount = 1,
            modifier1Id = "power-up",
            schemaVersion = 1,
            generatorVersion = 1,
            poolHash = "hash",
            acquisition = LibraryCardFaces.ProgressionLearn,
            isCoreAsset = true,
        };

        private sealed class FakeGateway : IOwnershipSpineGateway
        {
            public bool IsWalletConnected { get; set; }
            public bool SubmitSuccess { get; set; }
            public bool SubmitPending { get; set; }
            public bool ReconcileReturnsNone { get; set; }
            public bool ReconcileReturnsFailed { get; set; }
            public int SubmitCalls { get; private set; }
            public int SimulateCalls { get; private set; }
            public int ReconcileCalls { get; private set; }
            public int FetchStateCalls { get; private set; }
            public int SingleCalls { get; private set; }
            public int BundleCalls { get; private set; }
            public bool BundleDropsLastPull { get; set; }
            public long RamBalance { get; set; } = 100;
            public readonly List<KnownMoveRecord> Known = new List<KnownMoveRecord>();
            public readonly List<StampedMoveRecord> Verified = new List<StampedMoveRecord>();

            public Task<OwnershipKnownRollResult> RollKnownAsync(string idempotencyKey)
            {
                SingleCalls++;
                var record = new KnownMoveRecord
                {
                    instanceId = "known-1", moveId = "jab", modifierCount = 0, generatorVersion = 1, poolHash = "hash",
                    acquisition = LibraryCardFaces.CandyRoll,
                };
                if (Known.Find(item => item.instanceId == record.instanceId) == null)
                    Known.Add(record);
                RamBalance -= 5;
                return Task.FromResult(new OwnershipKnownRollResult
                {
                    known = record,
                    ramCost = 5,
                    ramBalance = RamBalance,
                });
            }

            public Task<OwnershipKnownRollResult> RollKnownBundleAsync(string idempotencyKey, int pullCount)
            {
                BundleCalls++;
                var records = new List<KnownMoveRecord>(pullCount);
                for (int i = 0; i < pullCount; i++)
                {
                    records.Add(new KnownMoveRecord
                    {
                        instanceId = $"{idempotencyKey}#{i}", moveId = "jab", modifierCount = 0, generatorVersion = 1,
                        poolHash = "hash", acquisition = LibraryCardFaces.CandyRoll,
                    });
                }
                if (BundleDropsLastPull)
                    records.RemoveAt(records.Count - 1);
                else
                {
                    Known.AddRange(records);
                    RamBalance -= 5 * pullCount;
                }
                return Task.FromResult(new OwnershipKnownRollResult
                {
                    known = records.Count > 0 ? records[0] : null,
                    knowns = records,
                    pullCount = pullCount,
                    ramCost = 5 * pullCount,
                    ramBalance = RamBalance,
                });
            }

            public Task<OwnershipSpineSnapshot> FetchStateAsync()
            {
                FetchStateCalls++;
                return Task.FromResult(new OwnershipSpineSnapshot
                {
                    ramBalance = RamBalance,
                    known = new List<KnownMoveRecord>(Known),
                    stamped = new List<StampedMoveRecord>(Verified),
                });
            }

            public Task<IReadOnlyList<KnownMoveRecord>> FetchKnownAsync() =>
                Task.FromResult<IReadOnlyList<KnownMoveRecord>>(Known);

            public Task<StampTransactionReview> SimulateStampAsync(StampMoveRequest request)
            {
                SimulateCalls++;
                return Task.FromResult(new StampTransactionReview { simulationSucceeded = true });
            }

            public Task<StampMoveResult> SubmitStampAsync(StampMoveRequest request)
            {
                SubmitCalls++;
                if (SubmitPending)
                {
                    return Task.FromResult(new StampMoveResult
                    {
                        success = true,
                        pending = true,
                        operationStatus = "das_pending",
                        knownInstanceId = request.knownInstanceId,
                        idempotencyKey = request.idempotencyKey,
                    });
                }
                if (SubmitSuccess)
                {
                    Known.Clear();
                    RamBalance -= 75;
                }
                return Task.FromResult(new StampMoveResult
                {
                    success = SubmitSuccess,
                    confirmed = SubmitSuccess,
                    knownInstanceId = request.knownInstanceId,
                    signature = SubmitSuccess ? "sig" : null,
                    ramBalance = RamBalance,
                    verifiedAsset = SubmitSuccess && Verified.Count > 0 ? Verified[0] : null,
                    error = SubmitSuccess ? null : "rejected",
                });
            }

            public Task<StampMoveResult> ReconcileStampAsync(string idempotencyKey = null)
            {
                ReconcileCalls++;
                if (ReconcileReturnsNone)
                {
                    return Task.FromResult(new StampMoveResult
                    {
                        success = true,
                        pending = false,
                        confirmed = false,
                        operationStatus = "none",
                    });
                }
                if (ReconcileReturnsFailed)
                {
                    return Task.FromResult(new StampMoveResult
                    {
                        success = true,
                        pending = false,
                        confirmed = false,
                        operationStatus = "failed",
                        message = "The Stamp transaction failed. No RAM or Known move was consumed.",
                    });
                }
                if (SubmitPending)
                {
                    return Task.FromResult(new StampMoveResult
                    {
                        success = true,
                        pending = true,
                        operationStatus = "das_pending",
                        knownInstanceId = "known-1",
                        idempotencyKey = idempotencyKey,
                    });
                }

                Known.Clear();
                RamBalance -= 75;
                return Task.FromResult(new StampMoveResult
                {
                    success = true,
                    confirmed = true,
                    operationStatus = "confirmed",
                    knownInstanceId = "known-1",
                    idempotencyKey = idempotencyKey,
                    ramBalance = RamBalance,
                    verifiedAsset = Verified.Count > 0 ? Verified[0] : null,
                });
            }

            public Task<IReadOnlyList<StampedMoveRecord>> FetchVerifiedStampedAsync() =>
                Task.FromResult<IReadOnlyList<StampedMoveRecord>>(Verified);

            public Task<DevnetRamGrantReview> SimulateDevnetRamGrantAsync(string idempotencyKey) =>
                Task.FromResult(new DevnetRamGrantReview
                {
                    cluster = "devnet", amount = 100, simulationSucceeded = true,
                });

            public Task<DevnetRamGrantResult> SubmitDevnetRamGrantAsync(string idempotencyKey)
            {
                RamBalance = 100;
                return Task.FromResult(new DevnetRamGrantResult
                {
                    success = true, confirmed = true, amount = 100, balanceAfter = RamBalance,
                });
            }
        }

        private sealed class ReadyWalletCoordinator : IProfileWalletAccessCoordinator
        {
            public int Calls { get; private set; }

            public Task<WalletAccessResult> EnsureAccessAsync(WalletAccessRequest request)
            {
                Calls++;
                return Task.FromResult(WalletAccessResult.From(WalletAccessResultKind.Ready));
            }
        }
    }
}
