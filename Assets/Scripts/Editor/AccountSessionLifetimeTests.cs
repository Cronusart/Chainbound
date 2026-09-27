using System;
using System.Reflection;
using System.Threading.Tasks;
using Chainbound.Combat;
using NUnit.Framework;
using UnityEngine;

public sealed class AccountSessionLifetimeTests
{
    private GameObject authorityRoot;
    private GameObject duplicateRoot;
    private GameObject guardRoot;
    private AccountSessionService authorityService;

    [TearDown]
    public void TearDown()
    {
        if (authorityService != null)
            InvokeLifecycle(authorityService, "OnDestroy");
        DestroyIfPresent(guardRoot);
        DestroyIfPresent(duplicateRoot);
        DestroyIfPresent(authorityRoot);
        authorityService = null;
    }

    [Test]
    public void DuplicateService_DoesNotReplaceProfileAuthority()
    {
        AccountSessionService authority = CreateAuthority();

        duplicateRoot = new GameObject("DuplicateSceneAccountSession");
        AccountSessionService duplicate = duplicateRoot.AddComponent<AccountSessionService>();
        InvokeLifecycle(duplicate, "Awake");

        Assert.AreSame(authority, AccountSessionService.Instance);
    }

    [Test]
    public async Task DetachedOwnershipGuard_UsesVerifiedAuthoritativeSession()
    {
        AccountSessionService authority = CreateAuthority();
        FakeWalletProvider wallet = authorityRoot.AddComponent<FakeWalletProvider>();
        wallet.SetConnected(true);
        authority.Configure(accountWalletProvider: wallet);
        authority.SetSession(CreateVerifiedSession(wallet.PublicKey));

        guardRoot = new GameObject("PersistentOwnershipGuard");
        WalletOperationGuard guard = guardRoot.AddComponent<WalletOperationGuard>();
        WalletOperationGuardResult result = await guard.EnsureVerifiedWalletAsync(new WalletOperationRequest
        {
            operationKind = WalletOperationKind.OnChainTransaction,
            actionName = "initialize the Ownership Spine",
            requireFreshProof = true,
        });

        Assert.IsTrue(result.success, result.message);
        Assert.AreEqual("profile-persistent-session", result.session.PlayerProfileId);
    }

    private AccountSessionService CreateAuthority()
    {
        Assert.IsTrue(AccountSessionService.Instance == null, "A previous test left an account authority alive.");
        authorityRoot = new GameObject("AuthoritativeAccountSession");
        authorityService = authorityRoot.AddComponent<AccountSessionService>();
        InvokeLifecycle(authorityService, "Awake");
        Assert.AreSame(authorityService, AccountSessionService.Instance);
        return authorityService;
    }

    private static PlayerAccountSession CreateVerifiedSession(string walletPublicKey)
    {
        PlayerAccountSession session = PlayerAccountSession.FromGuest(
            "profile-persistent-session",
            BuildPlatform.Editor);
        session.accountLinkState = AccountLinkState.Linked;
        session.walletIdentity = new WalletIdentityRef
        {
            isActive = true,
            walletProviderType = WalletProviderType.SeedVault,
            walletPublicKey = walletPublicKey,
        };
        session.isWalletConnected = true;
        session.walletProofVerifiedAt = DateTime.UtcNow.ToString("O");
        session.walletProofExpiresAt = DateTime.UtcNow.AddMinutes(10).ToString("O");
        return session;
    }

    private static void DestroyIfPresent(GameObject target)
    {
        if (target != null)
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static void InvokeLifecycle(AccountSessionService service, string methodName)
    {
        MethodInfo method = typeof(AccountSessionService).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method, $"AccountSessionService.{methodName} was not found.");
        method.Invoke(service, null);
    }

    private sealed class FakeWalletProvider : MonoBehaviour, IWalletProvider
    {
        public WalletProviderType ProviderType => WalletProviderType.SeedVault;
        public bool IsAvailable => true;
        public bool IsConnected { get; private set; }
        public string PublicKey => "11111111111111111111111111111111";
        public event Action<WalletConnectResult> OnWalletConnected
        {
            add { }
            remove { }
        }
        public event Action OnWalletDisconnected;

        public void SetConnected(bool value)
        {
            IsConnected = value;
        }

        public Task<WalletConnectResult> ConnectAsync()
        {
            return Task.FromResult(WalletConnectResult.Failed("unused"));
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            OnWalletDisconnected?.Invoke();
            return Task.CompletedTask;
        }

        public Task<byte[]> SignMessageAsync(byte[] messageBytes)
        {
            return Task.FromResult(new byte[64]);
        }
    }
}
