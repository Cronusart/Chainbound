using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public interface IPlatformBuildProfile
    {
        BuildPlatform BuildPlatform { get; }
        PlatformCapabilities Capabilities { get; }
        string BackendEnvironment { get; }
        bool WalletForwardStart { get; }
    }

    /// <summary>
    /// Marks an account provider that fabricates identities for Editor testing and
    /// must never satisfy a real flow. These types still compile into player builds,
    /// so a scene that wires one for Play Mode convenience would otherwise ship a
    /// fake wallet/identity to devices. Resolvers must reject marked providers
    /// outside the Editor and fall through to the real platform provider.
    /// </summary>
    public interface IEditorOnlyAccountProvider
    {
    }

    public interface IIdentityProvider
    {
        AuthIdentityType AuthIdentityType { get; }
        PlatformIdentityType PlatformIdentityType { get; }
        bool IsAvailable { get; }
        Task<IdentitySignInResult> SignInAsync(AccountSignInRequest request);
        Task SignOutAsync();
    }

    public interface IRecoverableSignInProvider : IIdentityProvider
    {
        bool CanRestore { get; }
        Task<IdentitySignInResult> RestoreAsync(AccountSignInRequest request);
    }

    public interface IWalletProvider
    {
        WalletProviderType ProviderType { get; }
        bool IsAvailable { get; }
        bool IsConnected { get; }
        string PublicKey { get; }
        event Action<WalletConnectResult> OnWalletConnected;
        event Action OnWalletDisconnected;
        Task<WalletConnectResult> ConnectAsync();
        Task DisconnectAsync();
        Task<byte[]> SignMessageAsync(byte[] messageBytes);
    }

    /// <summary>
    /// Owns the Supabase Auth credential. Profile authority always comes from this
    /// JWT; installation and wallet values are never accepted as profile authority.
    /// </summary>
    public interface IAccountAuthSessionProvider
    {
        bool HasCachedSession { get; }
        string AccessToken { get; }
        string AuthUserId { get; }
        Task<AccountAuthSessionResult> BootstrapAsync(SupabaseConfigSO config);
        Task<AccountAuthSessionResult> RefreshSessionAsync(SupabaseConfigSO config, bool forceRefresh = false);
        void ClearSession();
    }

    public interface IPlayerProfileGateway
    {
        Task<PlayerAccountSession> StartGuestAsync(AccountStartGuestRequest request);
        Task<PlayerAccountSession> BeginGameAsync(AccountBeginGameRequest request);
        Task<PlayerAccountSession> SetOnboardingStateAsync(AccountOnboardingStateRequest request);
        Task<PlayerAccountSession> RestoreIdentityAsync(AccountRestoreIdentityRequest request);
        Task<PlayerAccountSession> GetSessionAsync(AccountGetSessionRequest request);
        Task<AccountLinkResult> LinkIdentityAsync(AccountLinkIdentityRequest request);
        Task<AccountLinkResult> LinkWalletAsync(AccountLinkWalletRequest request);
        Task<AccountLinkResult> UnlinkWalletAsync(AccountUnlinkWalletRequest request);
        Task<WalletAuthChallengeResult> RequestWalletAuthChallengeAsync(AccountWalletAuthChallengeRequest request);
        Task<PlayerAccountSession> ConnectVerifiedWalletAsync(AccountWalletVerifiedConnectRequest request);
        Task<PlayerAccountSession> RestoreWalletProfileAsync(AccountWalletVerifiedConnectRequest request);
    }
}
