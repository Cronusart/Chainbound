using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class AccountSessionService : MonoBehaviour
    {
        private const string LegacyOfflineProfileSnapshotStorageKey = "signaljam.profile.snapshot.v1";
        private const string ScopedOfflineProfileSnapshotPrefix = "signaljam.profile.snapshot.v2";

        private static AccountSessionService instance;

        [Header("Providers")]
        [SerializeField] private MonoBehaviour guestIdentityProvider;
        [SerializeField] private MonoBehaviour recoverableSignInProvider;
        [SerializeField] private MonoBehaviour walletProvider;
        [SerializeField] private MonoBehaviour profileGateway;
        [SerializeField] private MonoBehaviour authSessionProvider;
        [SerializeField] private SupabaseConfigSO supabaseConfig;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs;

        private readonly AccountAssociationService associationService = new AccountAssociationService();

        /// <summary>
        /// Process-wide profile authority. The verified wallet proof and its linked
        /// profile must survive the main-menu scene transition so ownership guards
        /// never fall back to an empty scene-local session.
        /// </summary>
        public static AccountSessionService Instance => instance;

        public PlayerAccountSession CurrentSession { get; private set; } = PlayerAccountSession.Empty();
        public WalletConnectResult LastWalletConnection { get; private set; } = WalletConnectResult.Failed("Wallet not connected.");

        public bool HasLinkedWallet => CurrentSession != null && CurrentSession.HasLinkedWallet;
        public bool HasVerifiedWalletConnection
        {
            get
            {
                IWalletProvider provider = walletProvider as IWalletProvider;
                return CurrentSession != null &&
                       CurrentSession.HasVerifiedWalletConnection &&
                       provider != null &&
                       provider.IsConnected &&
                       !string.IsNullOrWhiteSpace(provider.PublicKey) &&
                       string.Equals(provider.PublicKey, CurrentSession.WalletPublicKey, StringComparison.Ordinal);
            }
        }

        public event Action<PlayerAccountSession> OnSessionChanged;
        public event Action<WalletConnectResult> OnWalletConnected;

        private IWalletProvider boundWalletProvider;
        private Task resumeAuthRefreshTask;
        private bool applicationWasInterrupted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning(
                    $"[AccountSessionService] Duplicate profile authority on '{gameObject.name}' was discarded; " +
                    $"keeping '{instance.gameObject.name}'.",
                    this);

                if (Application.isPlaying)
                    Destroy(gameObject);
                else
                    DestroyImmediate(gameObject);
                return;
            }

            instance = this;
            supabaseConfig = PlatformContext.ResolveSupabaseConfig(supabaseConfig);
            if (Application.isPlaying)
            {
                if (transform.parent != null)
                {
                    Debug.LogWarning(
                        "[AccountSessionService] Persistent account authority should be placed on a root GameObject. " +
                        $"Persisting root '{transform.root.name}'.",
                        this);
                    DontDestroyOnLoad(transform.root.gameObject);
                }
                else
                {
                    DontDestroyOnLoad(gameObject);
                }
            }
        }

        private void OnEnable()
        {
            BindWalletProviderEvents();
        }

        private void OnDisable()
        {
            UnbindWalletProviderEvents();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                applicationWasInterrupted = true;
                return;
            }

            RefreshAuthenticationAfterInterruption();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                applicationWasInterrupted = true;
                return;
            }

            RefreshAuthenticationAfterInterruption();
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (CurrentSession == null || !CurrentSession.isWalletConnected)
                return;

            IWalletProvider provider = walletProvider as IWalletProvider;
            bool adapterMismatch = provider == null ||
                                   !provider.IsConnected ||
                                   !string.Equals(provider.PublicKey, CurrentSession.WalletPublicKey, StringComparison.Ordinal);
            if (!CurrentSession.HasFreshWalletProof || adapterMismatch)
                ClearRuntimeWalletAuthorization();
        }

        public void Configure(
            MonoBehaviour guestProvider = null,
            MonoBehaviour recoverableProvider = null,
            MonoBehaviour accountWalletProvider = null,
            MonoBehaviour gateway = null,
            MonoBehaviour authenticatedSessionProvider = null,
            SupabaseConfigSO config = null)
        {
            if (guestProvider != null)
                guestIdentityProvider = guestProvider;
            if (recoverableProvider != null)
                recoverableSignInProvider = recoverableProvider;
            if (accountWalletProvider != null)
                walletProvider = accountWalletProvider;
            if (gateway != null)
                profileGateway = gateway;
            if (authenticatedSessionProvider != null)
                authSessionProvider = authenticatedSessionProvider;
            if (config != null)
                supabaseConfig = config;

            BindWalletProviderEvents();
        }

        /// <summary>
        /// Restores a Supabase anonymous session or creates one while online, then
        /// resolves its JWT-owned profile. No installation identifier is used as
        /// profile authority.
        /// </summary>
        public async Task<PlayerAccountSession> BootstrapSessionAsync(string clientVersion = "")
        {
            IAccountAuthSessionProvider auth = authSessionProvider as IAccountAuthSessionProvider;
            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;
            if (auth == null)
                return SetError("Authenticated profile session provider is not available.");
            if (gateway == null)
                return SetError("Profile gateway is not available.");

            PlayerAccountSession offlineSession;

            AccountAuthSessionResult authResult = await auth.BootstrapAsync(supabaseConfig);
            if (authResult == null || !authResult.success || string.IsNullOrWhiteSpace(authResult.accessToken))
            {
                if (auth.HasCachedSession && TryUseCachedOfflineSession(authResult != null ? authResult.errorMessage : "Anonymous profile authentication failed.", out offlineSession))
                    return offlineSession;
                return SetError(authResult != null ? authResult.errorMessage : "Anonymous profile authentication failed.");
            }

            PlayerAccountSession session = await gateway.StartGuestAsync(new AccountStartGuestRequest
            {
                guestInstallId = GetOrCreateInstallId(), // idempotency/telemetry only; server derives auth identity from JWT.
                activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
                clientVersion = clientVersion ?? "",
            });

            if (session == null || !session.hasSession || session.fetchError)
            {
                if (auth.HasCachedSession && TryUseCachedOfflineSession(session != null ? session.errorMessage : "Profile bootstrap failed.", out offlineSession))
                    return offlineSession;
                return SetError(session != null ? session.errorMessage : "Profile bootstrap failed.");
            }

            SetSession(session);
            return CurrentSession;
        }

        /// <summary>
        /// Returns a current Supabase access token, refreshing the rotating session
        /// credential when it is near expiry or when a caller explicitly recovered
        /// from an authentication rejection.
        /// </summary>
        public Task<AccountAuthSessionResult> EnsureFreshAuthSessionAsync(bool forceRefresh = false)
        {
            IAccountAuthSessionProvider auth = authSessionProvider as IAccountAuthSessionProvider;
            if (auth == null)
                return Task.FromResult(AccountAuthSessionResult.Failed("Authenticated profile session provider is not available."));

            SupabaseConfigSO config = PlatformContext.ResolveSupabaseConfig(supabaseConfig);
            return auth.RefreshSessionAsync(config, forceRefresh);
        }

        private void RefreshAuthenticationAfterInterruption()
        {
            if (!applicationWasInterrupted || CurrentSession == null || !CurrentSession.hasSession)
                return;

            applicationWasInterrupted = false;
            if (resumeAuthRefreshTask != null && !resumeAuthRefreshTask.IsCompleted)
                return;

            resumeAuthRefreshTask = RefreshAuthenticationAfterInterruptionAsync();
        }

        private async Task RefreshAuthenticationAfterInterruptionAsync()
        {
            string previousToken = SupabaseAuthSessionContext.AccessToken;
            AccountAuthSessionResult result = await EnsureFreshAuthSessionAsync();
            if (result == null || !result.success)
            {
                if (enableDebugLogs)
                    Debug.LogWarning(
                        $"[AccountSessionService] Profile session refresh after resume failed: {result?.errorMessage ?? "Unknown authentication error."}",
                        this);
                return;
            }

            if (!string.Equals(previousToken, result.accessToken, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(CurrentSession.PlayerProfileId))
            {
                PlayerRosterRefreshSignal.Publish(CurrentSession.PlayerProfileId, string.Empty);
            }
        }

        /// <summary>Idempotently marks onboarding started after the player accepts New Game.</summary>
        public async Task<PlayerAccountSession> BeginNewGameAsync(string clientVersion = "")
        {
            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;
            if (gateway == null || CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.PlayerProfileId))
                return SetError("No authenticated profile is available to begin a game.");

            PlayerAccountSession session = await gateway.BeginGameAsync(new AccountBeginGameRequest
            {
                playerProfileId = CurrentSession.PlayerProfileId,
                activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
                clientVersion = clientVersion ?? "",
            });

            if (session == null || !session.hasSession || session.fetchError)
                return SetError(session != null ? session.errorMessage : "Could not begin the game.");

            SetSession(session);
            return CurrentSession;
        }

        public async Task<PlayerAccountSession> SetIntroCinematicCompleteAsync()
        {
            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;
            if (gateway == null || CurrentSession == null || string.IsNullOrWhiteSpace(CurrentSession.PlayerProfileId))
                return SetError("No authenticated profile is available to update onboarding.");

            PlayerAccountSession session = await gateway.SetOnboardingStateAsync(new AccountOnboardingStateRequest
            {
                playerProfileId = CurrentSession.PlayerProfileId,
                introCinematicComplete = true,
            });

            if (session == null || !session.hasSession || session.fetchError)
                return SetError(session != null ? session.errorMessage : "Could not persist cinematic completion.");

            SetSession(session);
            return CurrentSession;
        }

        public async Task<PlayerAccountSession> StartGuestAsync(string clientVersion = "")
        {
            // Kept as a compatibility entry point for existing callers. Guest
            // profiles now always originate from verified Supabase anonymous auth.
            return await BootstrapSessionAsync(clientVersion);
        }

        public async Task<PlayerAccountSession> RestoreAccountAsync(string clientVersion = "")
        {
            IRecoverableSignInProvider provider = recoverableSignInProvider as IRecoverableSignInProvider;
            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;

            if (provider == null || !provider.IsAvailable || !provider.CanRestore)
                return SetError("Recoverable sign-in provider is not available.");

            IdentitySignInResult identity = await provider.RestoreAsync(new AccountSignInRequest
            {
                activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
                capabilities = PlatformContext.ResolveCapabilities(),
                clientVersion = clientVersion ?? "",
                deviceInstallId = GetOrCreateInstallId(),
                explicitConsent = true,
            });

            if (!identity.success)
                return SetError(identity.errorMessage);

            if (gateway == null)
                return SetError("Profile gateway is not available.");

            PlayerAccountSession session = await gateway.RestoreIdentityAsync(new AccountRestoreIdentityRequest
            {
                activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
                authIdentityType = identity.authIdentityType,
                platformIdentityType = identity.platformIdentityType,
                provider = identity.provider,
                providerSubject = identity.providerSubject,
                authUserId = identity.authUserId,
            });

            SetSession(session);
            return CurrentSession;
        }

        public async Task<WalletConnectResult> ConnectWalletAsync()
        {
            IWalletProvider provider = walletProvider as IWalletProvider;
            if (provider == null || !provider.IsAvailable)
            {
                LastWalletConnection = WalletConnectResult.Failed("Wallet provider is not available for this build.");
                return LastWalletConnection;
            }

            try
            {
                LastWalletConnection = await provider.ConnectAsync();
            }
            catch (WalletUserCancelledException ex)
            {
                LastWalletConnection = new WalletConnectResult { success = false, userCancelled = true, errorMessage = ex.Message };
            }
            catch (TimeoutException ex)
            {
                LastWalletConnection = new WalletConnectResult { success = false, userCancelled = true, errorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                LastWalletConnection = WalletConnectResult.Failed(ex.Message);
            }
            if (LastWalletConnection.success)
                OnWalletConnected?.Invoke(LastWalletConnection);

            if (enableDebugLogs)
                Debug.Log($"[AccountSessionService] Wallet connect success={LastWalletConnection.success}");

            return LastWalletConnection;
        }

        public async Task<PlayerAccountSession> ConnectWalletAndResolveSessionAsync(
            string clientVersion = "",
            WalletConnectMode mode = WalletConnectMode.Link)
        {
            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;
            if (gateway == null)
                return FailedWalletLinkAttempt("Profile gateway is not available.");

            IWalletProvider provider = walletProvider as IWalletProvider;
            if (provider == null || !provider.IsAvailable)
                return FailedWalletLinkAttempt("Wallet provider is not available for this build.");

            string linkedWallet = CurrentSession != null && CurrentSession.HasLinkedWallet
                ? CurrentSession.WalletPublicKey
                : "";

            WalletConnectResult wallet = await ConnectWalletAsync();
            if (wallet == null || !wallet.success)
                return FailedWalletLinkAttempt(wallet != null ? wallet.errorMessage : "Wallet connection failed.");

            if (mode == WalletConnectMode.Link &&
                !string.IsNullOrWhiteSpace(linkedWallet) &&
                !string.Equals(linkedWallet, wallet.walletPublicKey, StringComparison.Ordinal))
            {
                await ClearCandidateAuthorizationAsync();
                return FailedWalletLinkAttempt("wallet_mismatch_for_profile: Connect the wallet already linked to this profile.");
            }

            string installId = GetOrCreateInstallId();
            BuildPlatform platform = PlatformContext.ResolveActiveBuildPlatform();
            string currentProfileId = CurrentSession != null ? CurrentSession.PlayerProfileId : "";

            WalletAuthChallengeResult challenge = await gateway.RequestWalletAuthChallengeAsync(new AccountWalletAuthChallengeRequest
            {
                playerProfileId = currentProfileId,
                walletProviderType = wallet.providerType,
                walletPublicKey = wallet.walletPublicKey,
                activePlatformBuild = platform,
                clientVersion = clientVersion ?? "",
                deviceId = installId,
            });

            if (challenge == null || !challenge.success)
                return FailedWalletLinkAttempt(challenge != null ? challenge.errorMessage : "Wallet auth challenge failed.");

            if (!string.IsNullOrWhiteSpace(challenge.walletPublicKey) &&
                !string.Equals(challenge.walletPublicKey, wallet.walletPublicKey, StringComparison.Ordinal))
            {
                return FailedWalletLinkAttempt("Wallet auth challenge returned a different wallet.");
            }

            string signature;
            try
            {
                byte[] signedBytes = await provider.SignMessageAsync(Encoding.UTF8.GetBytes(challenge.message));
                if (signedBytes == null || signedBytes.Length != 64)
                    return FailedWalletLinkAttempt("Wallet returned an invalid message signature.");

                signature = Convert.ToBase64String(signedBytes);
            }
            catch (Exception ex)
            {
                return FailedWalletLinkAttempt($"Wallet signature failed: {ex.Message}");
            }

            PlayerAccountSession session = await gateway.ConnectVerifiedWalletAsync(new AccountWalletVerifiedConnectRequest
            {
                mode = mode,
                playerProfileId = currentProfileId,
                walletProviderType = wallet.providerType,
                walletPublicKey = wallet.walletPublicKey,
                nonceId = challenge.nonceId,
                signedMessage = challenge.message,
                signature = signature,
                activePlatformBuild = platform,
                clientVersion = clientVersion ?? "",
                deviceId = installId,
            });

            if (mode == WalletConnectMode.Resolve)
            {
                if (!IsVerifiedWalletResolution(session, wallet.walletPublicKey))
                {
                    string resolutionError = session != null && !string.IsNullOrWhiteSpace(session.errorMessage)
                        ? session.errorMessage
                        : "Verified wallet resolution was invalid.";
                    return FailedWalletLinkAttempt(resolutionError);
                }

                return session;
            }

            if (!IsVerifiedWalletSession(session, wallet.walletPublicKey))
            {
                string error = session != null && !string.IsNullOrWhiteSpace(session.errorMessage)
                    ? session.errorMessage
                    : "Verified wallet session did not match the connected wallet.";
                return FailedWalletLinkAttempt(error);
            }

            SetSession(session);
            return CurrentSession;
        }

        public Task<PlayerAccountSession> CreateWalletProfileAsync(string clientVersion = "")
        {
            return ConnectWalletAndResolveSessionAsync(clientVersion, WalletConnectMode.Create);
        }

        /// <summary>
        /// Explicitly replaces the active anonymous profile identity with the
        /// profile already bound to the proven wallet. This is recovery, never an
        /// implicit merge; callers only invoke it after the conflict prompt.
        /// </summary>
        public async Task<PlayerAccountSession> RestoreWalletProfileAsync(string clientVersion = "")
        {
            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;
            IWalletProvider provider = walletProvider as IWalletProvider;
            if (gateway == null || provider == null || !provider.IsAvailable)
                return FailedWalletLinkAttempt("Wallet recovery is unavailable.");

            WalletConnectResult wallet = LastWalletConnection;
            if (wallet == null || !wallet.success)
                wallet = await ConnectWalletAsync();
            if (wallet == null || !wallet.success)
                return FailedWalletLinkAttempt(wallet != null ? wallet.errorMessage : "Wallet connection failed.");

            string installId = GetOrCreateInstallId();
            BuildPlatform platform = PlatformContext.ResolveActiveBuildPlatform();
            WalletAuthChallengeResult challenge = await gateway.RequestWalletAuthChallengeAsync(new AccountWalletAuthChallengeRequest
            {
                playerProfileId = CurrentSession != null ? CurrentSession.PlayerProfileId : "",
                walletProviderType = wallet.providerType,
                walletPublicKey = wallet.walletPublicKey,
                activePlatformBuild = platform,
                clientVersion = clientVersion ?? "",
                deviceId = installId,
            });
            if (challenge == null || !challenge.success)
                return FailedWalletLinkAttempt(challenge != null ? challenge.errorMessage : "Wallet recovery challenge failed.");

            try
            {
                byte[] signatureBytes = await provider.SignMessageAsync(Encoding.UTF8.GetBytes(challenge.message));
                if (signatureBytes == null || signatureBytes.Length == 0)
                    return FailedWalletLinkAttempt("Wallet signature was rejected or empty.");

                PlayerAccountSession session = await gateway.RestoreWalletProfileAsync(new AccountWalletVerifiedConnectRequest
                {
                    playerProfileId = CurrentSession != null ? CurrentSession.PlayerProfileId : "",
                    walletProviderType = wallet.providerType,
                    walletPublicKey = wallet.walletPublicKey,
                    nonceId = challenge.nonceId,
                    signedMessage = challenge.message,
                    signature = Convert.ToBase64String(signatureBytes),
                    activePlatformBuild = platform,
                    clientVersion = clientVersion ?? "",
                    deviceId = installId,
                });
                if (session == null || !session.hasSession || session.fetchError)
                    return FailedWalletLinkAttempt(session != null ? session.errorMessage : "Wallet profile recovery failed.");

                SetSession(session);
                return CurrentSession;
            }
            catch (WalletUserCancelledException exception)
            {
                return FailedWalletLinkAttempt(exception.Message);
            }
            catch (TimeoutException exception)
            {
                return FailedWalletLinkAttempt(exception.Message);
            }
            catch (Exception exception)
            {
                return FailedWalletLinkAttempt($"Wallet recovery signature failed: {exception.Message}");
            }
        }

        public async Task<AccountLinkResult> LinkConnectedWalletAsync(bool explicitConsent)
        {
            if (LastWalletConnection == null || !LastWalletConnection.success)
                return AccountLinkResult.Failed(AccountLinkError.MissingCandidate, "Connect a wallet before linking it.");

            AccountLinkResult localPolicy = associationService.CanLinkWallet(CurrentSession, "", false, explicitConsent);
            if (!localPolicy.success)
                return localPolicy;

            IWalletProvider provider = walletProvider as IWalletProvider;
            if (provider == null || !provider.IsAvailable)
                return AccountLinkResult.Failed(AccountLinkError.ProviderUnsupported, "Wallet provider is not available.");

            IPlayerProfileGateway gateway = profileGateway as IPlayerProfileGateway;
            if (gateway == null)
                return AccountLinkResult.Failed(AccountLinkError.BackendRejected, "Profile gateway is not available.");

            BuildPlatform platform = PlatformContext.ResolveActiveBuildPlatform();
            WalletAuthChallengeResult challenge = await gateway.RequestWalletAuthChallengeAsync(new AccountWalletAuthChallengeRequest
            {
                playerProfileId = CurrentSession.PlayerProfileId,
                walletProviderType = LastWalletConnection.providerType,
                walletPublicKey = LastWalletConnection.walletPublicKey,
                activePlatformBuild = platform,
                clientVersion = Application.version,
                deviceId = GetOrCreateInstallId(),
            });
            if (challenge == null || !challenge.success)
            {
                return AccountLinkResult.Failed(
                    AccountLinkError.BackendRejected,
                    challenge != null ? challenge.errorMessage : "Wallet auth challenge failed.");
            }

            if (!string.Equals(challenge.walletPublicKey, LastWalletConnection.walletPublicKey, StringComparison.Ordinal))
                return AccountLinkResult.Failed(AccountLinkError.InvalidProof, "Wallet auth challenge returned a different wallet.");

            string signature;
            try
            {
                byte[] signedBytes = await provider.SignMessageAsync(Encoding.UTF8.GetBytes(challenge.message));
                if (signedBytes == null || signedBytes.Length != 64)
                    return AccountLinkResult.Failed(AccountLinkError.InvalidProof, "Wallet returned an invalid message signature.");
                signature = signedBytes != null && signedBytes.Length > 0 ? Convert.ToBase64String(signedBytes) : "";
            }
            catch (WalletUserCancelledException ex)
            {
                return AccountLinkResult.Failed(AccountLinkError.UserCancelled, ex.Message);
            }
            catch (TimeoutException ex)
            {
                return AccountLinkResult.Failed(AccountLinkError.UserCancelled, ex.Message);
            }
            catch (Exception ex)
            {
                return AccountLinkResult.Failed(AccountLinkError.InvalidProof, $"Wallet signature failed: {ex.Message}");
            }

            PlayerAccountSession verifiedSession = await gateway.ConnectVerifiedWalletAsync(new AccountWalletVerifiedConnectRequest
            {
                mode = WalletConnectMode.Link,
                playerProfileId = CurrentSession.PlayerProfileId,
                walletProviderType = LastWalletConnection.providerType,
                walletPublicKey = LastWalletConnection.walletPublicKey,
                nonceId = challenge.nonceId,
                signedMessage = challenge.message,
                signature = signature,
                activePlatformBuild = platform,
                clientVersion = Application.version,
                deviceId = GetOrCreateInstallId(),
            });

            if (verifiedSession == null || !verifiedSession.hasSession || verifiedSession.fetchError)
            {
                string error = verifiedSession != null ? verifiedSession.errorMessage : "Verified wallet link failed.";
                AccountLinkError linkError = error != null &&
                    (error.Contains("wallet_linked_to_different_profile", StringComparison.OrdinalIgnoreCase) ||
                     error.Contains("wallet_bound_to_other_profile", StringComparison.OrdinalIgnoreCase) ||
                     error.Contains("wallet_already_bound_to_profile", StringComparison.OrdinalIgnoreCase) ||
                     error.Contains("wallet_profile_mismatch", StringComparison.OrdinalIgnoreCase))
                    ? AccountLinkError.WalletLinkedToDifferentProfile
                    : AccountLinkError.InvalidProof;
                return AccountLinkResult.Failed(linkError, error);
            }

            SetSession(verifiedSession);
            return new AccountLinkResult
            {
                success = true,
                state = AccountLinkState.Linked,
                playerProfileId = verifiedSession.PlayerProfileId,
                message = "Wallet linked.",
            };
        }

        public async Task DisconnectWalletAsync()
        {
            IWalletProvider provider = walletProvider as IWalletProvider;
            if (provider != null && provider.IsConnected)
                await provider.DisconnectAsync();

            LastWalletConnection = WalletConnectResult.Failed("Wallet disconnected.");
            ClearRuntimeWalletAuthorization();
        }

        public void SetSession(PlayerAccountSession session)
        {
            CurrentSession = session ?? PlayerAccountSession.Empty();
            if (!CurrentSession.HasFreshWalletProof)
                ResetRuntimeWalletState(CurrentSession);
            if (CurrentSession.hasSession && !CurrentSession.fetchError)
                SecureCredentialStore.Set(OfflineProfileSnapshotStorageKey, JsonUtility.ToJson(CurrentSession));
            OnSessionChanged?.Invoke(CurrentSession);
        }

        /// <summary>
        /// Clears only runtime authorization. The backend wallet link remains active
        /// and will be required on the next ownership operation.
        /// </summary>
        public void ClearRuntimeWalletAuthorization()
        {
            if (CurrentSession == null)
                return;

            ResetRuntimeWalletState(CurrentSession);
            if (CurrentSession.hasSession && !CurrentSession.fetchError)
                SecureCredentialStore.Set(OfflineProfileSnapshotStorageKey, JsonUtility.ToJson(CurrentSession));
            OnSessionChanged?.Invoke(CurrentSession);
        }

        private bool TryUseCachedOfflineSession(string onlineFailure, out PlayerAccountSession session)
        {
            session = null;
            string json = SecureCredentialStore.Get(OfflineProfileSnapshotStorageKey);
            if (string.IsNullOrWhiteSpace(json) &&
                PlatformContext.ResolveOwnershipEnvironment() == OwnershipDeploymentEnvironment.DevnetOwnership)
            {
                json = SecureCredentialStore.Get(LegacyOfflineProfileSnapshotStorageKey);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    SecureCredentialStore.Set(OfflineProfileSnapshotStorageKey, json);
                    SecureCredentialStore.Delete(LegacyOfflineProfileSnapshotStorageKey);
                }
            }
            if (string.IsNullOrWhiteSpace(json))
                return false;

            PlayerAccountSession cached = JsonUtility.FromJson<PlayerAccountSession>(json);
            if (cached == null || !cached.hasSession || cached.profile == null || string.IsNullOrWhiteSpace(cached.profile.playerProfileId))
                return false;

            ResetRuntimeWalletState(cached);
            cached.fetchError = true;
            cached.errorMessage = string.IsNullOrWhiteSpace(onlineFailure)
                ? "Using the cached profile while the profile service is unavailable."
                : onlineFailure;
            CurrentSession = cached;
            OnSessionChanged?.Invoke(CurrentSession);
            session = CurrentSession;
            return true;
        }

        private PlayerAccountSession SetError(string message)
        {
            CurrentSession = PlayerAccountSession.Empty();
            CurrentSession.fetchError = true;
            CurrentSession.errorMessage = message ?? "Account session error.";
            OnSessionChanged?.Invoke(CurrentSession);
            return CurrentSession;
        }

        private static PlayerAccountSession FailedWalletLinkAttempt(string message)
        {
            PlayerAccountSession failed = PlayerAccountSession.Empty();
            failed.fetchError = true;
            failed.errorMessage = message ?? "Wallet link failed.";
            return failed;
        }

        private static bool IsVerifiedWalletSession(PlayerAccountSession session, string expectedWalletPublicKey)
        {
            return session != null &&
                   session.hasSession &&
                   !session.fetchError &&
                   session.walletIdentity != null &&
                   session.walletIdentity.isActive &&
                   session.isWalletConnected &&
                   session.HasFreshWalletProof &&
                   !string.IsNullOrWhiteSpace(session.PlayerProfileId) &&
                   !string.IsNullOrWhiteSpace(session.WalletPublicKey) &&
                   string.Equals(session.WalletPublicKey, expectedWalletPublicKey, StringComparison.Ordinal);
        }

        private static bool IsVerifiedWalletResolution(PlayerAccountSession session, string expectedWalletPublicKey)
        {
            if (session == null || !session.hasSession || session.fetchError ||
                session.walletIdentity == null || !session.walletIdentity.isActive ||
                !session.isWalletConnected || !session.HasFreshWalletProof ||
                !string.Equals(session.WalletPublicKey, expectedWalletPublicKey, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(session.walletResolutionStatus, "no_profile", StringComparison.Ordinal))
                return string.IsNullOrWhiteSpace(session.PlayerProfileId);

            return string.Equals(session.walletResolutionStatus, "profile_found", StringComparison.Ordinal) &&
                   !string.IsNullOrWhiteSpace(session.PlayerProfileId);
        }

        private async Task ClearCandidateAuthorizationAsync()
        {
            IWalletProvider provider = walletProvider as IWalletProvider;
            if (provider != null && provider.IsConnected)
            {
                try
                {
                    await provider.DisconnectAsync();
                }
                catch (Exception exception)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[AccountSessionService] Could not clear mismatched wallet authorization: {exception.Message}", this);
                }
            }

            LastWalletConnection = WalletConnectResult.Failed("wallet_mismatch_for_profile");
            ClearRuntimeWalletAuthorization();
        }

        private void BindWalletProviderEvents()
        {
            IWalletProvider provider = walletProvider as IWalletProvider;
            if (ReferenceEquals(provider, boundWalletProvider))
                return;

            UnbindWalletProviderEvents();
            boundWalletProvider = provider;
            if (boundWalletProvider == null)
                return;

            boundWalletProvider.OnWalletDisconnected += HandleProviderDisconnected;
            boundWalletProvider.OnWalletConnected += HandleProviderConnected;
        }

        private void UnbindWalletProviderEvents()
        {
            if (boundWalletProvider == null)
                return;

            boundWalletProvider.OnWalletDisconnected -= HandleProviderDisconnected;
            boundWalletProvider.OnWalletConnected -= HandleProviderConnected;
            boundWalletProvider = null;
        }

        private void HandleProviderDisconnected()
        {
            LastWalletConnection = WalletConnectResult.Failed("Wallet disconnected.");
            ClearRuntimeWalletAuthorization();
        }

        private async void HandleProviderConnected(WalletConnectResult candidate)
        {
            if (candidate == null || !candidate.success || CurrentSession == null || !CurrentSession.HasVerifiedWalletConnection)
                return;

            if (!string.Equals(candidate.walletPublicKey, CurrentSession.WalletPublicKey, StringComparison.Ordinal))
            {
                ClearRuntimeWalletAuthorization();
                if (boundWalletProvider != null && boundWalletProvider.IsConnected)
                {
                    try
                    {
                        await boundWalletProvider.DisconnectAsync();
                    }
                    catch (Exception exception)
                    {
                        if (enableDebugLogs)
                            Debug.LogWarning($"[AccountSessionService] Could not clear changed wallet authorization: {exception.Message}", this);
                    }
                }
            }
        }

        private static void ResetRuntimeWalletState(PlayerAccountSession session)
        {
            if (session == null)
                return;

            session.isWalletConnected = false;
            session.walletProofVerifiedAt = "";
            session.walletProofExpiresAt = "";
        }

        private static string OfflineProfileSnapshotStorageKey =>
            PlatformContext.BuildScopedStorageKey(ScopedOfflineProfileSnapshotPrefix);

        private static string GetOrCreateInstallId()
        {
            const string key = "SignalJam_AccountInstallId";
            string id = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(key, id);
                PlayerPrefs.Save();
            }

            return id;
        }
    }
}
