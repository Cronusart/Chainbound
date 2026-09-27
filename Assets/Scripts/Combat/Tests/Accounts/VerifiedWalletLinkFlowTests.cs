using System;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class VerifiedWalletLinkFlowTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }

        [Test]
        public async Task LinkConnectedWallet_UsesServerNonceAndVerifiedEndpoint()
        {
            root = new GameObject("VerifiedWalletLinkFlowTests");
            AccountSessionService service = root.AddComponent<AccountSessionService>();
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            FakeProfileGateway gateway = root.AddComponent<FakeProfileGateway>();
            service.Configure(accountWalletProvider: wallet, gateway: gateway);
            service.SetSession(PlayerAccountSession.FromGuest("profile-1", BuildPlatform.Editor));

            WalletConnectResult connected = await service.ConnectWalletAsync();
            Assert.IsTrue(connected.success);
            Assert.IsFalse(service.CurrentSession.isWalletConnected, "A candidate connection must not authorize or mutate the profile session.");
            Assert.IsFalse(service.CurrentSession.HasLinkedWallet);

            AccountLinkResult result = await service.LinkConnectedWalletAsync(explicitConsent: true);

            Assert.IsTrue(result.success, result.message);
            Assert.IsFalse(gateway.LegacyLinkEndpointCalled);
            Assert.NotNull(gateway.ChallengeRequest);
            Assert.AreEqual(wallet.PublicKey, gateway.ChallengeRequest.walletPublicKey);
            Assert.NotNull(gateway.VerifiedRequest);
            Assert.AreEqual("nonce-1", gateway.VerifiedRequest.nonceId);
            Assert.AreEqual(gateway.ChallengeMessage, gateway.VerifiedRequest.signedMessage);
            Assert.AreEqual(Convert.ToBase64String(wallet.Signature), gateway.VerifiedRequest.signature);
        }

        [Test]
        public async Task ConnectWalletAndResolveSession_ConnectsSignsAndPersistsVerifiedWallet()
        {
            root = new GameObject("VerifiedWalletSessionFlowTests");
            AccountSessionService service = root.AddComponent<AccountSessionService>();
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            FakeProfileGateway gateway = root.AddComponent<FakeProfileGateway>();
            service.Configure(accountWalletProvider: wallet, gateway: gateway);
            service.SetSession(PlayerAccountSession.FromGuest("profile-1", BuildPlatform.Editor));

            PlayerAccountSession session = await service.ConnectWalletAndResolveSessionAsync("milestone-1-test");

            Assert.IsTrue(session.hasSession, session.errorMessage);
            Assert.IsFalse(session.fetchError, session.errorMessage);
            Assert.IsTrue(session.walletIdentity.isActive);
            Assert.IsTrue(session.isWalletConnected);
            Assert.IsTrue(service.HasVerifiedWalletConnection);
            Assert.AreEqual(wallet.PublicKey, session.WalletPublicKey);
            Assert.AreEqual("profile-1", session.PlayerProfileId);
            Assert.AreSame(session, service.CurrentSession);
            Assert.NotNull(gateway.ChallengeRequest);
            Assert.AreEqual("milestone-1-test", gateway.ChallengeRequest.clientVersion);
            Assert.NotNull(gateway.VerifiedRequest);
            Assert.AreEqual(Convert.ToBase64String(wallet.Signature), gateway.VerifiedRequest.signature);
            Assert.IsFalse(gateway.LegacyLinkEndpointCalled);
        }

        [Test]
        public async Task ConnectWalletAndResolveSession_AcceptsWalletOwnedRecoveredProfile()
        {
            root = new GameObject("RecoveredWalletSessionFlowTests");
            AccountSessionService service = root.AddComponent<AccountSessionService>();
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            FakeProfileGateway gateway = root.AddComponent<FakeProfileGateway>();
            gateway.VerifiedProfileId = "profile-restored-from-wallet";
            service.Configure(accountWalletProvider: wallet, gateway: gateway);
            service.SetSession(PlayerAccountSession.FromGuest("fresh-install-profile", BuildPlatform.Editor));

            PlayerAccountSession session = await service.ConnectWalletAndResolveSessionAsync("wallet-recovery-test");

            Assert.IsTrue(session.hasSession, session.errorMessage);
            Assert.IsFalse(session.fetchError, session.errorMessage);
            Assert.AreEqual("profile-restored-from-wallet", session.PlayerProfileId);
            Assert.AreEqual(wallet.PublicKey, session.WalletPublicKey);
            Assert.IsTrue(service.HasVerifiedWalletConnection);
            Assert.AreSame(session, service.CurrentSession);
        }

        [Test]
        public async Task ConnectWalletAndResolveSession_RejectsNonEd25519SignatureLength()
        {
            root = new GameObject("InvalidWalletSignatureFlowTests");
            AccountSessionService service = root.AddComponent<AccountSessionService>();
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            FakeProfileGateway gateway = root.AddComponent<FakeProfileGateway>();
            wallet.Signature = new byte[63];
            service.Configure(accountWalletProvider: wallet, gateway: gateway);
            service.SetSession(PlayerAccountSession.FromGuest("profile-1", BuildPlatform.Editor));

            PlayerAccountSession session = await service.ConnectWalletAndResolveSessionAsync("milestone-1-test");

            Assert.IsFalse(session.hasSession);
            Assert.IsTrue(session.fetchError);
            StringAssert.Contains("invalid message signature", session.errorMessage);
            Assert.IsNull(gateway.VerifiedRequest);
        }

        [Test]
        public async Task ConnectWalletAndResolveSession_WrongWalletPreservesLinkedIdentityAndClearsAdapter()
        {
            root = new GameObject("WrongWalletFlowTests");
            AccountSessionService service = root.AddComponent<AccountSessionService>();
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            FakeProfileGateway gateway = root.AddComponent<FakeProfileGateway>();
            wallet.WalletPublicKey = "22222222222222222222222222222222";
            service.Configure(accountWalletProvider: wallet, gateway: gateway);

            PlayerAccountSession linked = PlayerAccountSession.FromGuest("profile-1", BuildPlatform.Editor);
            linked.walletIdentity = new WalletIdentityRef
            {
                isActive = true,
                walletProviderType = WalletProviderType.SeedVault,
                walletPublicKey = "11111111111111111111111111111111",
            };
            linked.accountLinkState = AccountLinkState.Linked;
            service.SetSession(linked);

            PlayerAccountSession result = await service.ConnectWalletAndResolveSessionAsync("wrong-wallet-test");

            Assert.IsTrue(result.fetchError);
            StringAssert.Contains("wallet_mismatch_for_profile", result.errorMessage);
            Assert.AreEqual("11111111111111111111111111111111", service.CurrentSession.WalletPublicKey);
            Assert.IsTrue(service.CurrentSession.HasLinkedWallet);
            Assert.IsFalse(service.CurrentSession.isWalletConnected);
            Assert.IsFalse(wallet.IsConnected);
            Assert.AreEqual(1, wallet.DisconnectCalls);
            Assert.IsNull(gateway.ChallengeRequest);
        }

        [Test]
        public async Task Disconnect_PreservesBackendLinkAndClearsProof()
        {
            root = new GameObject("DisconnectWalletFlowTests");
            AccountSessionService service = root.AddComponent<AccountSessionService>();
            FakeWalletProvider wallet = root.AddComponent<FakeWalletProvider>();
            FakeProfileGateway gateway = root.AddComponent<FakeProfileGateway>();
            service.Configure(accountWalletProvider: wallet, gateway: gateway);
            await service.ConnectWalletAndResolveSessionAsync("disconnect-test");

            await service.DisconnectWalletAsync();

            Assert.IsTrue(service.CurrentSession.HasLinkedWallet);
            Assert.AreEqual(wallet.PublicKey, service.CurrentSession.WalletPublicKey);
            Assert.IsFalse(service.CurrentSession.isWalletConnected);
            Assert.IsFalse(service.CurrentSession.HasFreshWalletProof);
        }

        private sealed class FakeWalletProvider : MonoBehaviour, IWalletProvider
        {
            public byte[] Signature = new byte[64];
            public string WalletPublicKey = "11111111111111111111111111111111";
            public int DisconnectCalls { get; private set; }
            public WalletProviderType ProviderType => WalletProviderType.SeedVault;
            public bool IsAvailable => true;
            public bool IsConnected { get; private set; }
            public string PublicKey => WalletPublicKey;
            public event Action<WalletConnectResult> OnWalletConnected;
            public event Action OnWalletDisconnected;

            public Task<WalletConnectResult> ConnectAsync()
            {
                IsConnected = true;
                var result = new WalletConnectResult
                {
                    success = true,
                    providerType = ProviderType,
                    walletPublicKey = PublicKey,
                };
                OnWalletConnected?.Invoke(result);
                return Task.FromResult(result);
            }

            public Task DisconnectAsync()
            {
                DisconnectCalls++;
                IsConnected = false;
                OnWalletDisconnected?.Invoke();
                return Task.CompletedTask;
            }

            public Task<byte[]> SignMessageAsync(byte[] messageBytes)
            {
                Assert.AreEqual(FakeProfileGateway.StaticChallengeMessage, Encoding.UTF8.GetString(messageBytes));
                return Task.FromResult(Signature);
            }
        }

        private sealed class FakeProfileGateway : MonoBehaviour, IPlayerProfileGateway
        {
            public const string StaticChallengeMessage = "Chainbound wallet login\nNonce: test";
            public string ChallengeMessage => StaticChallengeMessage;
            public string VerifiedProfileId { get; set; } = "profile-1";
            public bool LegacyLinkEndpointCalled { get; private set; }
            public AccountWalletAuthChallengeRequest ChallengeRequest { get; private set; }
            public AccountWalletVerifiedConnectRequest VerifiedRequest { get; private set; }

            public Task<WalletAuthChallengeResult> RequestWalletAuthChallengeAsync(AccountWalletAuthChallengeRequest request)
            {
                ChallengeRequest = request;
                return Task.FromResult(new WalletAuthChallengeResult
                {
                    success = true,
                    nonceId = "nonce-1",
                    walletPublicKey = request.walletPublicKey,
                    message = StaticChallengeMessage,
                });
            }

            public Task<PlayerAccountSession> ConnectVerifiedWalletAsync(AccountWalletVerifiedConnectRequest request)
            {
                VerifiedRequest = request;
                PlayerAccountSession session = PlayerAccountSession.FromGuest(VerifiedProfileId, BuildPlatform.Editor);
                session.isGuestSession = false;
                session.isWalletConnected = true;
                session.accountLinkState = AccountLinkState.Linked;
                session.walletProofVerifiedAt = DateTime.UtcNow.ToString("O");
                session.walletProofExpiresAt = DateTime.UtcNow.AddMinutes(15).ToString("O");
                session.walletIdentity = new WalletIdentityRef
                {
                    isActive = true,
                    walletProviderType = WalletProviderType.SeedVault,
                    walletPublicKey = request.walletPublicKey,
                };
                return Task.FromResult(session);
            }

            public Task<AccountLinkResult> LinkWalletAsync(AccountLinkWalletRequest request)
            {
                LegacyLinkEndpointCalled = true;
                return Task.FromResult(AccountLinkResult.Failed(AccountLinkError.InvalidProof, "Legacy endpoint must not be called."));
            }

            public Task<PlayerAccountSession> StartGuestAsync(AccountStartGuestRequest request) => Task.FromResult(PlayerAccountSession.Empty());
            public Task<PlayerAccountSession> BeginGameAsync(AccountBeginGameRequest request) => Task.FromResult(PlayerAccountSession.Empty());
            public Task<PlayerAccountSession> SetOnboardingStateAsync(AccountOnboardingStateRequest request) => Task.FromResult(PlayerAccountSession.Empty());
            public Task<PlayerAccountSession> RestoreIdentityAsync(AccountRestoreIdentityRequest request) => Task.FromResult(PlayerAccountSession.Empty());
            public Task<PlayerAccountSession> GetSessionAsync(AccountGetSessionRequest request) => Task.FromResult(PlayerAccountSession.Empty());
            public Task<AccountLinkResult> LinkIdentityAsync(AccountLinkIdentityRequest request) => Task.FromResult(AccountLinkResult.Failed(AccountLinkError.Unknown, "unused"));
            public Task<AccountLinkResult> UnlinkWalletAsync(AccountUnlinkWalletRequest request) => Task.FromResult(AccountLinkResult.Failed(AccountLinkError.Unknown, "unused"));
            public Task<PlayerAccountSession> RestoreWalletProfileAsync(AccountWalletVerifiedConnectRequest request) => Task.FromResult(PlayerAccountSession.Empty());
        }
    }
}
