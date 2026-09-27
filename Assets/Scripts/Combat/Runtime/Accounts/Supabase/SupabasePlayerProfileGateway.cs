using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class SupabasePlayerProfileGateway : MonoBehaviour, IPlayerProfileGateway
    {
        private const string DefaultSupabaseConfigResourcePath = "SupabaseConfig";
#if UNITY_EDITOR
        private const string DefaultSupabaseConfigAssetPath = "Assets/Data/Supabase/SupabaseConfig.asset";
#endif

        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;

        private bool attemptedDefaultConfigResolve;

        public SupabaseConfigSO Config => supabaseConfig;

        public void Configure(SupabaseConfigSO config)
        {
            if (config != null)
                supabaseConfig = config;
            else
                ResolveDefaultConfigIfNeeded();
        }

        public async Task<PlayerAccountSession> StartGuestAsync(AccountStartGuestRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountStartGuestDto
            {
                guest_install_id = request != null ? request.guestInstallId : "",
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
                client_version = request != null ? request.clientVersion : "",
            });

            var response = await AuthenticatedPostAsync("account_start_guest", json);
            if (!response.IsSuccess)
                return ErrorSession($"account_start_guest returned {response.statusCode}: {response.body}");

            return ParseSession(response.body, BuildPlatform.Editor);
        }

        public async Task<PlayerAccountSession> BeginGameAsync(AccountBeginGameRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountBeginGameDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
                client_version = request != null ? request.clientVersion : "",
            });
            var response = await AuthenticatedPostAsync("account_begin_game", json);
            return response.IsSuccess
                ? ParseSession(response.body, request != null ? request.activePlatformBuild : BuildPlatform.Editor)
                : ErrorSession($"account_begin_game returned {response.statusCode}: {response.body}");
        }

        public async Task<PlayerAccountSession> SetOnboardingStateAsync(AccountOnboardingStateRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountOnboardingStateDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                intro_cinematic_complete = request != null && request.introCinematicComplete,
            });
            var response = await AuthenticatedPostAsync("account_set_onboarding_state", json);
            return response.IsSuccess
                ? ParseSession(response.body, BuildPlatform.Editor)
                : ErrorSession($"account_set_onboarding_state returned {response.statusCode}: {response.body}");
        }

        public async Task<PlayerAccountSession> RestoreIdentityAsync(AccountRestoreIdentityRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountRestoreIdentityDto
            {
                identity_type = ToBackendIdentity(request != null ? request.authIdentityType : AuthIdentityType.None),
                platform_identity_type = ToBackendPlatformIdentity(request != null ? request.platformIdentityType : PlatformIdentityType.None),
                provider = request != null ? request.provider : "",
                provider_subject = request != null ? request.providerSubject : "",
                auth_user_id = request != null ? request.authUserId : "",
                verification_token = request != null ? request.verificationToken : "",
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
            });

            var response = await SupabaseClient.PostJsonAsync(supabaseConfig, "account_restore_identity", json);
            if (!response.IsSuccess)
                return ErrorSession($"account_restore_identity returned {response.statusCode}: {response.body}");

            return ParseSession(response.body, request != null ? request.activePlatformBuild : BuildPlatform.Editor);
        }

        public async Task<PlayerAccountSession> GetSessionAsync(AccountGetSessionRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountGetSessionDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                auth_user_id = request != null ? request.authUserId : "",
                wallet_public_key = request != null ? request.walletPublicKey : "",
            });

            var response = await AuthenticatedPostAsync("account_get_session", json);
            if (!response.IsSuccess)
                return ErrorSession($"account_get_session returned {response.statusCode}: {response.body}");

            return ParseSession(response.body, BuildPlatform.Editor);
        }

        public async Task<AccountLinkResult> LinkIdentityAsync(AccountLinkIdentityRequest request)
        {
            if (!HasConfig())
                return AccountLinkResult.Failed(AccountLinkError.BackendRejected, "Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountLinkIdentityDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                identity_type = ToBackendIdentity(request != null ? request.authIdentityType : AuthIdentityType.None),
                platform_identity_type = ToBackendPlatformIdentity(request != null ? request.platformIdentityType : PlatformIdentityType.None),
                provider = request != null ? request.provider : "",
                provider_subject = request != null ? request.providerSubject : "",
                auth_user_id = request != null ? request.authUserId : "",
                verification_token = request != null ? request.verificationToken : "",
                explicit_consent = request != null && request.explicitConsent,
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
            });

            var response = await AuthenticatedPostAsync("account_link_identity", json);
            return ParseLinkResult(response, "account_link_identity");
        }

        public async Task<AccountLinkResult> LinkWalletAsync(AccountLinkWalletRequest request)
        {
            if (!HasConfig())
                return AccountLinkResult.Failed(AccountLinkError.BackendRejected, "Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountLinkWalletDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                wallet_provider = ToBackendWalletProvider(request != null ? request.walletProviderType : WalletProviderType.None),
                wallet_public_key = request != null ? request.walletPublicKey : "",
                signed_message = request != null ? request.signedMessage : "",
                signature = request != null ? request.signature : "",
                explicit_consent = request != null && request.explicitConsent,
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
            });

            var response = await AuthenticatedPostAsync("account_link_wallet", json);
            return ParseLinkResult(response, "account_link_wallet");
        }

        public async Task<AccountLinkResult> UnlinkWalletAsync(AccountUnlinkWalletRequest request)
        {
            if (!HasConfig())
                return AccountLinkResult.Failed(AccountLinkError.BackendRejected, "Supabase config missing.");

            string json = JsonUtility.ToJson(new AccountUnlinkWalletDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                wallet_public_key = request != null ? request.walletPublicKey : "",
                explicit_consent = request != null && request.explicitConsent,
            });

            var response = await AuthenticatedPostAsync("account_unlink_wallet", json);
            return ParseLinkResult(response, "account_unlink_wallet");
        }

        public async Task<WalletAuthChallengeResult> RequestWalletAuthChallengeAsync(AccountWalletAuthChallengeRequest request)
        {
            if (!HasConfig())
                return WalletAuthChallengeResult.Failed("Supabase config missing.");

            string json = JsonUtility.ToJson(new WalletAuthChallengeRequestDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                wallet_provider = ToBackendWalletProvider(request != null ? request.walletProviderType : WalletProviderType.None),
                wallet_public_key = request != null ? request.walletPublicKey : "",
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
                client_version = request != null ? request.clientVersion : "",
                device_id = request != null ? request.deviceId : "",
                cluster = request != null ? request.cluster : "",
            });

            var response = await AuthenticatedPostAsync("wallet_auth_challenge", json);
            if (!response.IsSuccess)
                return WalletAuthChallengeResult.Failed($"wallet_auth_challenge returned {response.statusCode}: {response.body}");

            var payload = JsonUtility.FromJson<WalletAuthChallengeResponseDto>(response.body);
            if (payload == null || !payload.success)
                return WalletAuthChallengeResult.Failed(payload != null ? payload.error : "wallet_auth_challenge returned an invalid payload.");

            return new WalletAuthChallengeResult
            {
                success = true,
                nonceId = payload.nonce_id ?? "",
                walletPublicKey = payload.wallet_public_key ?? "",
                walletProvider = payload.wallet_provider ?? "",
                cluster = payload.cluster ?? "",
                message = payload.message ?? "",
                issuedAt = payload.issued_at ?? "",
                expiresAt = payload.expires_at ?? "",
            };
        }

        public async Task<PlayerAccountSession> ConnectVerifiedWalletAsync(AccountWalletVerifiedConnectRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new WalletConnectVerifiedRequestDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                wallet_provider = ToBackendWalletProvider(request != null ? request.walletProviderType : WalletProviderType.None),
                wallet_public_key = request != null ? request.walletPublicKey : "",
                nonce_id = request != null ? request.nonceId : "",
                signed_message = request != null ? request.signedMessage : "",
                signature = request != null ? request.signature : "",
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
                client_version = request != null ? request.clientVersion : "",
                device_id = request != null ? request.deviceId : "",
            });

            var response = await AuthenticatedPostAsync("wallet_connect_verified", json);
            if (!response.IsSuccess)
                return ErrorSession($"wallet_connect_verified returned {response.statusCode}: {response.body}");

            return ParseSession(response.body, request != null ? request.activePlatformBuild : BuildPlatform.Editor);
        }

        public async Task<PlayerAccountSession> RestoreWalletProfileAsync(AccountWalletVerifiedConnectRequest request)
        {
            if (!HasConfig())
                return ErrorSession("Supabase config missing.");

            string json = JsonUtility.ToJson(new WalletConnectVerifiedRequestDto
            {
                player_profile_id = request != null ? request.playerProfileId : "",
                wallet_provider = ToBackendWalletProvider(request != null ? request.walletProviderType : WalletProviderType.None),
                wallet_public_key = request != null ? request.walletPublicKey : "",
                nonce_id = request != null ? request.nonceId : "",
                signed_message = request != null ? request.signedMessage : "",
                signature = request != null ? request.signature : "",
                active_platform_build = ToBackendPlatform(request != null ? request.activePlatformBuild : BuildPlatform.Editor),
                client_version = request != null ? request.clientVersion : "",
                device_id = request != null ? request.deviceId : "",
            });

            var response = await AuthenticatedPostAsync("wallet_restore_profile", json);
            return response.IsSuccess
                ? ParseSession(response.body, request != null ? request.activePlatformBuild : BuildPlatform.Editor)
                : ErrorSession($"wallet_restore_profile returned {response.statusCode}: {response.body}");
        }

        private bool HasConfig()
        {
            ResolveDefaultConfigIfNeeded();
            bool hasConfig = supabaseConfig != null;
            if (!hasConfig && enableDebugLogs)
                Debug.LogWarning("[SupabasePlayerProfileGateway] SupabaseConfigSO is not assigned.");
            return hasConfig;
        }

        private Task<SupabaseClient.Response> AuthenticatedPostAsync(string functionName, string json)
        {
            string accessToken = SupabaseAuthSessionContext.AccessToken;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return Task.FromResult(new SupabaseClient.Response
                {
                    statusCode = 0,
                    body = "Authenticated Supabase session is unavailable."
                });
            }

            return SupabaseClient.PostJsonAsync(supabaseConfig, functionName, json, accessToken);
        }

        private void ResolveDefaultConfigIfNeeded()
        {
            if (supabaseConfig != null || attemptedDefaultConfigResolve)
                return;

            attemptedDefaultConfigResolve = true;
            supabaseConfig = Resources.Load<SupabaseConfigSO>(DefaultSupabaseConfigResourcePath);

#if UNITY_EDITOR
            if (supabaseConfig == null)
                supabaseConfig = UnityEditor.AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(DefaultSupabaseConfigAssetPath);
#endif

            if (supabaseConfig != null && enableDebugLogs)
                Debug.Log($"[SupabasePlayerProfileGateway] Loaded SupabaseConfigSO '{supabaseConfig.name}'.", this);
        }

        private static PlayerAccountSession ErrorSession(string message)
        {
            var session = PlayerAccountSession.Empty();
            session.fetchError = true;
            session.errorMessage = message ?? "Profile gateway error.";
            return session;
        }

        private static AccountLinkResult ParseLinkResult(SupabaseClient.Response response, string functionName)
        {
            if (!response.IsSuccess)
                return AccountLinkResult.Failed(AccountLinkError.BackendRejected, $"{functionName} returned {response.statusCode}: {response.body}");

            var payload = JsonUtility.FromJson<AccountLinkResponseDto>(response.body);
            if (payload == null || !payload.success)
                return AccountLinkResult.Failed(ToLinkError(payload != null ? payload.error : ""), payload != null ? payload.message : "Link failed.");

            return AccountLinkResult.Success(payload.player_profile_id, ToLinkState(payload.account_link_state), payload.message);
        }

        private static PlayerAccountSession ParseSession(string body, BuildPlatform fallbackPlatform)
        {
            var payload = JsonUtility.FromJson<AccountSessionResponseDto>(body);
            if (payload == null || !payload.success)
                return ErrorSession(payload != null ? payload.error : "Session response was invalid.");

            string proofExpiresAt = payload.wallet_proof_expires_at ?? "";
            bool hasFreshProof = DateTime.TryParse(proofExpiresAt, out DateTime proofExpiry) &&
                                 proofExpiry.ToUniversalTime() > DateTime.UtcNow;

            return new PlayerAccountSession
            {
                hasSession = true,
                isGuestSession = payload.is_guest,
                isRecoverable = payload.is_recoverable,
                isWalletConnected = !string.IsNullOrWhiteSpace(payload.wallet_public_key) && hasFreshProof,
                accountLinkState = ToLinkState(payload.account_link_state),
                lastSignInMethod = ToLastSignInMethod(payload.last_sign_in_method),
                activePlatformBuild = ToBuildPlatform(payload.active_platform_build, fallbackPlatform),
                walletProofVerifiedAt = payload.wallet_proof_verified_at ?? "",
                walletProofExpiresAt = proofExpiresAt,
                profile = new PlayerProfileRef
                {
                    playerProfileId = payload.player_profile_id ?? "",
                    authUserId = payload.auth_user_id ?? "",
                    displayName = payload.display_name ?? "",
                    platformOrigin = payload.platform_origin ?? "",
                    lastPlatformSeen = payload.last_platform_seen ?? "",
                    dataResidency = payload.data_residency ?? "",
                    hasStartedGame = payload.has_started_game,
                    introCinematicComplete = payload.intro_cinematic_complete,
                    introComplete = payload.intro_complete,
                    isNewProfile = payload.created,
                },
                authIdentity = new AuthIdentityRef
                {
                    authUserId = payload.auth_user_id ?? "",
                    identityType = ToAuthIdentityType(payload.identity_type),
                    provider = payload.identity_provider ?? "",
                    providerSubject = payload.identity_subject ?? "",
                    isPrimary = true,
                },
                platformIdentity = new PlatformIdentityRef
                {
                    platformIdentityType = ToPlatformIdentityType(payload.platform_identity_type),
                    platformIdentitySubject = payload.platform_identity_subject ?? "",
                    provider = payload.platform_identity_provider ?? "",
                    isPrimary = true,
                },
                walletIdentity = new WalletIdentityRef
                {
                    walletProviderType = ToWalletProvider(payload.wallet_provider),
                    walletPublicKey = payload.wallet_public_key ?? "",
                    isActive = !string.IsNullOrWhiteSpace(payload.wallet_public_key),
                },
            };
        }

        private static string ToBackendPlatform(BuildPlatform platform)
        {
            switch (platform)
            {
                case BuildPlatform.GooglePlay: return "google_play";
                case BuildPlatform.IOSAppStore: return "ios_app_store";
                case BuildPlatform.SolanaDappStore: return "solana_dapp_store";
                default: return "editor";
            }
        }

        private static string ToBackendIdentity(AuthIdentityType identityType)
        {
            switch (identityType)
            {
                case AuthIdentityType.Guest: return "guest";
                case AuthIdentityType.SupabaseAuth: return "supabase_auth";
                case AuthIdentityType.GooglePlay: return "google_play";
                case AuthIdentityType.Apple: return "apple";
                case AuthIdentityType.Seeker: return "seeker";
                default: return "none";
            }
        }

        private static string ToBackendPlatformIdentity(PlatformIdentityType identityType)
        {
            switch (identityType)
            {
                case PlatformIdentityType.Guest: return "guest";
                case PlatformIdentityType.GooglePlay: return "google_play";
                case PlatformIdentityType.Apple: return "apple";
                case PlatformIdentityType.Seeker: return "seeker";
                default: return "none";
            }
        }

        private static string ToBackendWalletProvider(WalletProviderType provider)
        {
            switch (provider)
            {
                case WalletProviderType.Phantom: return "phantom";
                case WalletProviderType.Solflare: return "solflare";
                case WalletProviderType.SeedVault: return "seed_vault";
                case WalletProviderType.Other: return "other";
                default: return "none";
            }
        }

        private static BuildPlatform ToBuildPlatform(string value, BuildPlatform fallback)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "google_play": return BuildPlatform.GooglePlay;
                case "ios_app_store": return BuildPlatform.IOSAppStore;
                case "solana_dapp_store": return BuildPlatform.SolanaDappStore;
                case "editor": return BuildPlatform.Editor;
                default: return fallback;
            }
        }

        private static AccountLinkState ToLinkState(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "unlinked": return AccountLinkState.Unlinked;
                case "link_pending": return AccountLinkState.LinkPending;
                case "linked": return AccountLinkState.Linked;
                case "revoked": return AccountLinkState.Revoked;
                case "conflict": return AccountLinkState.Conflict;
                default: return AccountLinkState.None;
            }
        }

        private static LastSignInMethod ToLastSignInMethod(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "guest": return LastSignInMethod.Guest;
                case "google_play": return LastSignInMethod.GooglePlay;
                case "apple": return LastSignInMethod.Apple;
                case "seeker": return LastSignInMethod.Seeker;
                case "wallet": return LastSignInMethod.Wallet;
                case "editor_mock": return LastSignInMethod.EditorMock;
                default: return LastSignInMethod.None;
            }
        }

        private static AuthIdentityType ToAuthIdentityType(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "guest": return AuthIdentityType.Guest;
                case "supabase_auth": return AuthIdentityType.SupabaseAuth;
                case "google_play": return AuthIdentityType.GooglePlay;
                case "apple": return AuthIdentityType.Apple;
                case "seeker": return AuthIdentityType.Seeker;
                default: return AuthIdentityType.None;
            }
        }

        private static PlatformIdentityType ToPlatformIdentityType(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "guest": return PlatformIdentityType.Guest;
                case "google_play": return PlatformIdentityType.GooglePlay;
                case "apple": return PlatformIdentityType.Apple;
                case "seeker": return PlatformIdentityType.Seeker;
                default: return PlatformIdentityType.None;
            }
        }

        private static WalletProviderType ToWalletProvider(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "phantom": return WalletProviderType.Phantom;
                case "solflare": return WalletProviderType.Solflare;
                case "seed_vault": return WalletProviderType.SeedVault;
                case "other": return WalletProviderType.Other;
                default: return WalletProviderType.None;
            }
        }

        private static AccountLinkError ToLinkError(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            {
                case "missing_current_profile": return AccountLinkError.MissingCurrentProfile;
                case "missing_candidate": return AccountLinkError.MissingCandidate;
                case "explicit_consent_required": return AccountLinkError.ExplicitConsentRequired;
                case "identity_linked_to_different_profile": return AccountLinkError.IdentityLinkedToDifferentProfile;
                case "wallet_linked_to_different_profile": return AccountLinkError.WalletLinkedToDifferentProfile;
                case "provider_unsupported": return AccountLinkError.ProviderUnsupported;
                case "invalid_proof": return AccountLinkError.InvalidProof;
                case "backend_rejected": return AccountLinkError.BackendRejected;
                default: return AccountLinkError.Unknown;
            }
        }

        [Serializable]
        private class AccountSessionResponseDto
        {
            public bool success;
            public string error;
            public string player_profile_id;
            public string auth_user_id;
            public string display_name;
            public string platform_origin;
            public string last_platform_seen;
            public string data_residency;
            public bool has_started_game;
            public bool intro_cinematic_complete;
            public bool intro_complete;
            public bool created;
            public bool is_guest;
            public bool is_recoverable;
            public string identity_type;
            public string identity_provider;
            public string identity_subject;
            public string platform_identity_type;
            public string platform_identity_provider;
            public string platform_identity_subject;
            public string wallet_provider;
            public string wallet_public_key;
            public string account_link_state;
            public string last_sign_in_method;
            public string active_platform_build;
            public string wallet_proof_verified_at;
            public string wallet_proof_expires_at;
        }

        [Serializable]
        private class AccountLinkResponseDto
        {
            public bool success;
            public string error;
            public string message;
            public string player_profile_id;
            public string account_link_state;
        }

        [Serializable] private class AccountStartGuestDto { public string guest_install_id; public string active_platform_build; public string client_version; }
        [Serializable] private class AccountBeginGameDto { public string player_profile_id; public string active_platform_build; public string client_version; }
        [Serializable] private class AccountOnboardingStateDto { public string player_profile_id; public bool intro_cinematic_complete; }
        [Serializable] private class AccountGetSessionDto { public string player_profile_id; public string auth_user_id; public string wallet_public_key; }
        [Serializable] private class AccountRestoreIdentityDto { public string identity_type; public string platform_identity_type; public string provider; public string provider_subject; public string auth_user_id; public string verification_token; public string active_platform_build; }
        [Serializable] private class AccountLinkIdentityDto : AccountRestoreIdentityDto { public string player_profile_id; public bool explicit_consent; }
        [Serializable] private class AccountLinkWalletDto { public string player_profile_id; public string wallet_provider; public string wallet_public_key; public string signed_message; public string signature; public bool explicit_consent; public string active_platform_build; }
        [Serializable] private class AccountUnlinkWalletDto { public string player_profile_id; public string wallet_public_key; public bool explicit_consent; }
        [Serializable] private class WalletAuthChallengeRequestDto { public string player_profile_id; public string wallet_provider; public string wallet_public_key; public string active_platform_build; public string client_version; public string device_id; public string cluster; }
        [Serializable] private class WalletConnectVerifiedRequestDto { public string player_profile_id; public string wallet_provider; public string wallet_public_key; public string nonce_id; public string signed_message; public string signature; public string active_platform_build; public string client_version; public string device_id; }

        [Serializable]
        private class WalletAuthChallengeResponseDto
        {
            public bool success;
            public string error;
            public string nonce_id;
            public string wallet_public_key;
            public string wallet_provider;
            public string cluster;
            public string message;
            public string issued_at;
            public string expires_at;
        }

    }
}
