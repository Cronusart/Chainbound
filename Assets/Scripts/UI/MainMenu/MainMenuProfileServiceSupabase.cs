using System;
using System.Threading.Tasks;
using UnityEngine;
using Chainbound.Combat;

/// <summary>
/// Supabase-backed profile adapter for the main menu.
/// Reads the currently authenticated Supabase profile and maps it to
/// MainMenuProfileSnapshot.
/// </summary>
public class MainMenuProfileServiceSupabase : MainMenuProfileServiceBase
{
    [Header("Supabase")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;
    [SerializeField] private SupabasePlayerProfileGateway profileGateway;

    [Header("Local Cache")]
    [Tooltip("Global PlayerPrefs key for intro-quest complete. Per-wallet key is written automatically.")]
    [SerializeField] private string introCompletePlayerPrefsKey = MainMenuProfileCache.IntroCompleteGlobal;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Self-Test (Editor/Dev)")]
    [Tooltip("Wallet used by the Supabase endpoint self-test in editor/dev builds.")]
    [SerializeField] private string selfTestWalletAddress = "11111111111111111111111111111111";
#endif

    public override void FetchProfile(string walletAddress, Action<MainMenuProfileSnapshot> onComplete)
    {
        if (string.IsNullOrEmpty(walletAddress))
        {
            if (enableDebugLogs)
                Debug.LogWarning("[ProfileSupabase] FetchProfile called with empty wallet address.");
            onComplete?.Invoke(DefaultSnapshot());
            return;
        }

        RunFetchAsync(walletAddress, onComplete);
    }

    private async void RunFetchAsync(string walletAddress, Action<MainMenuProfileSnapshot> onComplete)
    {
        MainMenuProfileSnapshot snapshot = DefaultSnapshot();

        try
        {
            SupabasePlayerProfileGateway gateway = ResolveGateway();
            if (gateway == null)
            {
                snapshot.fetchError = true;
                snapshot.activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform();
                Debug.LogError("[ProfileSupabase] SupabasePlayerProfileGateway could not be resolved.");
                onComplete?.Invoke(snapshot);
                return;
            }

            string playerProfileId = GameSessionContext.Instance != null
                ? GameSessionContext.Instance.PlayerProfileId
                : "";
            PlayerAccountSession session = await gateway.GetSessionAsync(new AccountGetSessionRequest
            {
                playerProfileId = playerProfileId,
                walletPublicKey = walletAddress,
            });

            if (session == null || !session.hasSession || session.fetchError)
            {
                snapshot.fetchError = true;
                snapshot.activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform();
                if (enableDebugLogs)
                    Debug.LogWarning($"[ProfileSupabase] Compatibility profile fetch failed: {session?.errorMessage}");
                onComplete?.Invoke(snapshot);
                return;
            }

            snapshot = MapSession(session, walletAddress);

            // Global key (legacy fallback) + per-wallet key (offline fallback for this device)
            PlayerPrefs.SetInt(introCompletePlayerPrefsKey, snapshot.introQuestComplete ? 1 : 0);
            PlayerPrefs.SetInt(MainMenuProfileCache.GetWalletIntroKey(walletAddress), snapshot.introQuestComplete ? 1 : 0);
            PlayerPrefs.Save();

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[ProfileSupabase] profile={snapshot.playerProfileId} wallet={walletAddress} " +
                    $"intro_complete={snapshot.introQuestComplete} platform={snapshot.activePlatformBuild} " +
                    $"-> hasStartedGame={snapshot.hasStartedGame} introQuestComplete={snapshot.introQuestComplete}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileSupabase] FetchProfile error: {ex.Message}");
            snapshot.fetchError = true;
        }

        onComplete?.Invoke(snapshot);
    }

    private SupabasePlayerProfileGateway ResolveGateway()
    {
        if (profileGateway != null)
        {
            profileGateway.Configure(supabaseConfig);
            return profileGateway;
        }

        profileGateway = GetComponent<SupabasePlayerProfileGateway>();
        if (profileGateway == null)
            profileGateway = FindObjectOfType<SupabasePlayerProfileGateway>();
        if (profileGateway == null)
            profileGateway = gameObject.AddComponent<SupabasePlayerProfileGateway>();

        profileGateway.Configure(supabaseConfig);
        return profileGateway;
    }

    public static MainMenuProfileSnapshot MapSession(PlayerAccountSession session, string fallbackWalletAddress = "")
    {
        PlayerProfileRef profile = session != null ? session.profile : null;
        PlatformIdentityRef platformIdentity = session != null ? session.platformIdentity : null;
        WalletIdentityRef walletIdentity = session != null ? session.walletIdentity : null;

        return new MainMenuProfileSnapshot
        {
            hasProfile = session != null && session.hasSession,
            hasStartedGame = profile != null && profile.hasStartedGame,
            introCinematicComplete = profile != null && profile.introCinematicComplete,
            introQuestComplete = profile != null && profile.introComplete,
            displayName = profile != null ? profile.displayName ?? "" : "",
            playerProfileId = profile != null ? profile.playerProfileId ?? "" : "",
            authUserId = profile != null ? profile.authUserId ?? "" : "",
            platformIdentityType = platformIdentity != null ? platformIdentity.platformIdentityType : PlatformIdentityType.None,
            platformIdentitySubject = platformIdentity != null ? platformIdentity.platformIdentitySubject ?? "" : "",
            walletProviderType = walletIdentity != null ? walletIdentity.walletProviderType : WalletProviderType.None,
            walletPublicKey = walletIdentity != null && !string.IsNullOrWhiteSpace(walletIdentity.walletPublicKey)
                ? walletIdentity.walletPublicKey
                : fallbackWalletAddress ?? "",
            accountLinkState = session != null ? session.accountLinkState : AccountLinkState.None,
            lastSignInMethod = session != null ? session.lastSignInMethod : LastSignInMethod.None,
            activePlatformBuild = session != null ? session.activePlatformBuild : PlatformContext.ResolveActiveBuildPlatform(),
            isGuestSession = session != null && session.isGuestSession,
            isRecoverable = session != null && session.isRecoverable,
            walletLinkedToProfile = walletIdentity != null && walletIdentity.isActive,
            fetchError = session != null && session.fetchError,
        };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Run Supabase Endpoint Self-Test")]
    private void RunSupabaseEndpointSelfTestContextMenu()
    {
        _ = RunSupabaseEndpointSelfTestAsync(selfTestWalletAddress);
    }

    /// <summary>
    /// Runs the profile fetch flow and asserts outbound Supabase calls are /functions/v1 only.
    /// </summary>
    public async Task<bool> RunSupabaseEndpointSelfTestAsync(string walletAddress)
    {
        if (supabaseConfig == null)
        {
            Debug.LogError("[ProfileSupabase][SelfTest] Missing SupabaseConfigSO reference.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            Debug.LogError("[ProfileSupabase][SelfTest] walletAddress is required.");
            return false;
        }

        SupabaseClient.ClearRequestAudit();

        bool callbackInvoked = false;
        FetchProfile(walletAddress, _ => callbackInvoked = true);

        float timeoutSeconds = Mathf.Max(5f, supabaseConfig.requestTimeoutSeconds + 2f);
        float deadline = Time.realtimeSinceStartup + timeoutSeconds;

        while (!callbackInvoked && Time.realtimeSinceStartup < deadline)
            await Task.Yield();

        if (!callbackInvoked)
        {
            Debug.LogError("[ProfileSupabase][SelfTest] Timed out waiting for profile fetch.");
            return false;
        }

        var urls = SupabaseClient.GetRequestAuditSnapshot();
        if (urls.Count == 0)
        {
            Debug.LogError("[ProfileSupabase][SelfTest] No outbound Supabase URLs were captured.");
            return false;
        }

        bool passed = true;

        foreach (string url in urls)
        {
            Debug.Log($"[ProfileSupabase][SelfTest] outbound URL: {url}");
            if (!SupabaseClient.IsAllowedEdgeFunctionUrl(url))
            {
                passed = false;
                Debug.LogError($"[ProfileSupabase][SelfTest] Disallowed Supabase endpoint: {url}");
            }
        }

        Debug.Assert(passed, "[ProfileSupabase][SelfTest] Expected only /functions/v1 endpoints.");
        if (passed)
            Debug.Log("[ProfileSupabase][SelfTest] PASS: all Supabase calls used /functions/v1.");

        return passed;
    }
#endif

    private static MainMenuProfileSnapshot DefaultSnapshot()
    {
        return new MainMenuProfileSnapshot
        {
            hasProfile = false,
            hasStartedGame = false,
            introCinematicComplete = false,
            introQuestComplete = false,
            displayName = "",
            playerProfileId = "",
            authUserId = "",
            platformIdentityType = PlatformIdentityType.None,
            platformIdentitySubject = "",
            walletProviderType = WalletProviderType.None,
            walletPublicKey = "",
            accountLinkState = AccountLinkState.None,
            lastSignInMethod = LastSignInMethod.None,
            activePlatformBuild = PlatformContext.ResolveActiveBuildPlatform(),
            isGuestSession = false,
            isRecoverable = false,
            walletLinkedToProfile = false,
        };
    }
}
