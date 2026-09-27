using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class WalletOperationGuardTests
    {
        private GameObject root;
        private GameObject duplicateRoot;
        private GameObject detachedGuardRoot;
        private FakeCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            coordinator = new FakeCoordinator();
            ProfileWalletAccessRegistry.Register(coordinator);
        }

        [TearDown]
        public void TearDown()
        {
            ProfileWalletAccessRegistry.Unregister(coordinator);
            if (detachedGuardRoot != null)
                UnityEngine.Object.DestroyImmediate(detachedGuardRoot);
            if (duplicateRoot != null)
                UnityEngine.Object.DestroyImmediate(duplicateRoot);
            if (root != null)
            {
                AccountSessionService service = root.GetComponent<AccountSessionService>();
                if (service != null && ReferenceEquals(AccountSessionService.Instance, service))
                    InvokeLifecycle(service, "OnDestroy");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public async Task DuplicateSessionService_DoesNotReplaceVerifiedProfileAuthority()
        {
            root = new GameObject("AuthoritativeAccountSession");
            AccountSessionService sessions = root.AddComponent<AccountSessionService>();
            InvokeLifecycle(sessions, "Awake");
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            wallet.SetConnected(true);
            sessions.Configure(accountWalletProvider: wallet);
            sessions.SetSession(VerifiedSession(wallet.PublicKey));

            duplicateRoot = new GameObject("DuplicateSceneAccountSession");
            AccountSessionService duplicate = duplicateRoot.AddComponent<AccountSessionService>();
            InvokeLifecycle(duplicate, "Awake");

            detachedGuardRoot = new GameObject("PersistentOwnershipGuard");
            WalletOperationGuard guard = detachedGuardRoot.AddComponent<WalletOperationGuard>();
            WalletOperationGuardResult result = await guard.EnsureVerifiedWalletAsync(Request());

            Assert.AreSame(sessions, AccountSessionService.Instance);
            Assert.IsTrue(result.success, result.message);
            Assert.AreEqual("profile-guard", result.session.PlayerProfileId);
            Assert.AreEqual(0, coordinator.Calls);
        }

        [Test]
        public async Task FreshVerifiedSession_SuppressesCoordinatorPrompt()
        {
            CreateServices(out AccountSessionService sessions, out WalletOperationGuard guard, out FakeWalletProvider wallet);
            wallet.SetConnected(true);
            sessions.SetSession(VerifiedSession(wallet.PublicKey));

            WalletOperationGuardResult result = await guard.EnsureVerifiedWalletAsync(Request());

            Assert.IsTrue(result.success, result.message);
            Assert.AreEqual(0, coordinator.Calls);
        }

        [Test]
        public async Task OwnershipCancellation_BlocksOnlyRequestedOperation()
        {
            CreateServices(out AccountSessionService sessions, out WalletOperationGuard guard, out FakeWalletProvider wallet);
            sessions.SetSession(LinkedDisconnectedSession(wallet.PublicKey));
            coordinator.Next = WalletAccessResult.From(WalletAccessResultKind.Cancelled, "Not now.");

            WalletOperationGuardResult result = await guard.EnsureVerifiedWalletAsync(Request());

            Assert.IsFalse(result.success);
            Assert.AreEqual(WalletOperationGuardStatus.UserCancelled, result.status);
            Assert.IsTrue(sessions.CurrentSession.HasLinkedWallet);
            Assert.IsFalse(sessions.CurrentSession.isWalletConnected);
        }

        [Test]
        public void MarkDisconnected_PreservesPersistentWalletLink()
        {
            CreateServices(out AccountSessionService sessions, out WalletOperationGuard guard, out FakeWalletProvider wallet);
            sessions.SetSession(VerifiedSession(wallet.PublicKey));

            guard.MarkWalletDisconnected();

            Assert.IsTrue(sessions.CurrentSession.HasLinkedWallet);
            Assert.IsFalse(sessions.CurrentSession.isWalletConnected);
            Assert.IsFalse(sessions.CurrentSession.HasFreshWalletProof);
        }

        private void CreateServices(
            out AccountSessionService sessions,
            out WalletOperationGuard guard,
            out FakeWalletProvider wallet)
        {
            root = new GameObject("WalletOperationGuardTests");
            sessions = root.AddComponent<AccountSessionService>();
            wallet = root.AddComponent<FakeWalletProvider>();
            sessions.Configure(accountWalletProvider: wallet);
            guard = root.AddComponent<WalletOperationGuard>();
        }

        private static WalletOperationRequest Request() => new WalletOperationRequest
        {
            operationKind = WalletOperationKind.OnChainTransaction,
            actionName = "test ownership action",
            requireFreshProof = true,
        };

        private static PlayerAccountSession LinkedDisconnectedSession(string wallet)
        {
            PlayerAccountSession session = PlayerAccountSession.FromGuest("profile-guard", BuildPlatform.Editor);
            session.accountLinkState = AccountLinkState.Linked;
            session.walletIdentity = new WalletIdentityRef
            {
                isActive = true,
                walletProviderType = WalletProviderType.SeedVault,
                walletPublicKey = wallet,
            };
            return session;
        }

        private static PlayerAccountSession VerifiedSession(string wallet)
        {
            PlayerAccountSession session = LinkedDisconnectedSession(wallet);
            session.isWalletConnected = true;
            session.walletProofVerifiedAt = DateTime.UtcNow.ToString("O");
            session.walletProofExpiresAt = DateTime.UtcNow.AddMinutes(10).ToString("O");
            return session;
        }

        private static void InvokeLifecycle(AccountSessionService service, string methodName)
        {
            MethodInfo method = typeof(AccountSessionService).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, $"AccountSessionService.{methodName} was not found.");
            method.Invoke(service, null);
        }

        private sealed class FakeCoordinator : IProfileWalletAccessCoordinator
        {
            public int Calls { get; private set; }
            public WalletAccessResult Next = WalletAccessResult.From(WalletAccessResultKind.Ready);

            public Task<WalletAccessResult> EnsureAccessAsync(WalletAccessRequest request)
            {
                Calls++;
                return Task.FromResult(Next);
            }
        }

        private sealed class FakeWalletProvider : MonoBehaviour, IWalletProvider
        {
            public WalletProviderType ProviderType => WalletProviderType.SeedVault;
            public bool IsAvailable => true;
            public bool IsConnected { get; private set; }
            public string PublicKey => "11111111111111111111111111111111";
            public event Action<WalletConnectResult> OnWalletConnected;
            public event Action OnWalletDisconnected;

            public void SetConnected(bool value) => IsConnected = value;
            public Task<WalletConnectResult> ConnectAsync() => Task.FromResult(WalletConnectResult.Failed("unused"));
            public Task DisconnectAsync()
            {
                IsConnected = false;
                OnWalletDisconnected?.Invoke();
                return Task.CompletedTask;
            }
            public Task<byte[]> SignMessageAsync(byte[] messageBytes) => Task.FromResult(new byte[64]);
        }
    }
}
