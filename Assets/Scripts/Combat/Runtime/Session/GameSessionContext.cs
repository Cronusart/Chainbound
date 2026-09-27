using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Cross-scene context carrier.
///
/// Populated by MainMenuSequenceController2 immediately before a scene load.
/// Consumed by the intro / game scene to determine wallet identity, spawn point,
/// and which quest to activate on entry.
///
/// Setup: Add [GameSessionContext] as a child of GameServices in Bootstrap so it
/// persists automatically. If not found there, MainMenuSequenceController2 will
/// create it lazily before the first scene load.
/// </summary>
public class GameSessionContext : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────

    private static GameSessionContext _instance;

    public static GameSessionContext Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<GameSessionContext>();
            return _instance;
        }
    }

    // ── Session Data (read-only from outside) ─────────────────────────────────

    /// <summary>Solana wallet public key for the active session.</summary>
    public string WalletAddress { get; private set; } = "";

    /// <summary>Canonical server-issued profile id. Prefer this over WalletAddress for backend profile operations.</summary>
    public string PlayerProfileId { get; private set; } = "";

    /// <summary>Supabase auth user id when a recoverable auth session is present.</summary>
    public string AuthUserId { get; private set; } = "";

    public BuildPlatform ActivePlatformBuild { get; private set; } = BuildPlatform.Editor;
    public LastSignInMethod LastSignInMethod { get; private set; } = LastSignInMethod.None;
    public PlatformIdentityType PlatformIdentityType { get; private set; } = PlatformIdentityType.None;
    public string PlatformIdentitySubject { get; private set; } = "";
    public WalletProviderType WalletProviderType { get; private set; } = WalletProviderType.None;
    public AccountLinkState AccountLinkState { get; private set; } = AccountLinkState.None;
    public bool IsGuestSession { get; private set; }

    /// <summary>True when the authenticated profile was created during this account bootstrap.</summary>
    public bool IsNewPlayer { get; private set; }

    /// <summary>Persistent intro quest completion value from the authenticated profile.</summary>
    public bool IntroComplete { get; private set; }

    /// <summary>Remote onboarding checkpoint for the menu sprite cinematic.</summary>
    public bool IntroCinematicComplete { get; private set; }

    /// <summary>Resolved display name for the active session, if any.</summary>
    public string DisplayName { get; private set; } = "";

    /// <summary>Committed starter species id for the active session, if any.</summary>
    public string StarterSpeciesId { get; private set; } = "";

    /// <summary>Backend chainling id returned when the starter is committed.</summary>
    public string StarterBackendChainlingId { get; private set; } = "";

    /// <summary>
    /// Spawn tag to resolve in the destination scene (e.g. "IntroSpawn", "DefaultSpawn").
    /// Consuming code: GameObject.FindGameObjectWithTag(DesiredSpawnTag).
    /// </summary>
    public string DesiredSpawnTag { get; private set; } = "DefaultSpawn";

    /// <summary>
    /// Quest ID / asset name to activate on scene entry. Empty = no pending quest.
    /// Consuming code: QuestService.TryStartQuestById(PendingQuestId).
    /// </summary>
    public string PendingQuestId { get; private set; } = "";

    /// <summary>
    /// Sequence to play on scene entry (e.g. intro sequence). Set by main menu when routing to intro path.
    /// Cast to SequenceAsset when consuming. PendingSequenceStarter starts it on scene load, then clears.
    /// </summary>
    public ScriptableObject PendingSequence { get; private set; }

    /// <summary>
    /// True when this session's profile data came from a PlayerPrefs offline cache
    /// rather than a live Supabase response.
    /// </summary>
    public bool IsOfflineSession { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        // Nested prefab instances persist with the authoritative GameServices
        // root. Standalone fallback instances still own their persistence.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticInstance()
    {
        _instance = null;
    }

    // ── Population ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populate from the resolved profile snapshot before a scene load.
    /// Called by MainMenuSequenceController2.PopulateGameSessionContext().
    /// </summary>
    public void SetFromProfile(
        string walletAddress,
        bool   isNewPlayer,
        bool   introComplete,
        string desiredSpawnTag,
        string pendingQuestId,
        bool   isOfflineSession = false,
        string displayName = "",
        string starterSpeciesId = "",
        string starterBackendChainlingId = "",
        string playerProfileId = "",
        string authUserId = "",
        BuildPlatform activePlatformBuild = BuildPlatform.Editor,
        LastSignInMethod lastSignInMethod = LastSignInMethod.None,
        PlatformIdentityType platformIdentityType = PlatformIdentityType.None,
        string platformIdentitySubject = "",
        WalletProviderType walletProviderType = WalletProviderType.None,
        AccountLinkState accountLinkState = AccountLinkState.None,
        bool isGuestSession = false,
        bool introCinematicComplete = false)
    {
        WalletAddress = walletAddress ?? "";
        PlayerProfileId = playerProfileId ?? "";
        AuthUserId = authUserId ?? "";
        ActivePlatformBuild = activePlatformBuild;
        LastSignInMethod = lastSignInMethod;
        PlatformIdentityType = platformIdentityType;
        PlatformIdentitySubject = platformIdentitySubject ?? "";
        WalletProviderType = walletProviderType;
        AccountLinkState = accountLinkState;
        IsGuestSession = isGuestSession;
        IsNewPlayer = isNewPlayer;
        IntroComplete = introComplete;
        IntroCinematicComplete = introCinematicComplete;
        DesiredSpawnTag = desiredSpawnTag ?? "DefaultSpawn";
        PendingQuestId = pendingQuestId ?? "";
        IsOfflineSession = isOfflineSession;
        DisplayName = displayName ?? "";
        StarterSpeciesId = starterSpeciesId ?? "";
        StarterBackendChainlingId = starterBackendChainlingId ?? "";
    }

    public void SetFromAccountSession(
        PlayerAccountSession session,
        string desiredSpawnTag,
        string pendingQuestId,
        bool isOfflineSession = false,
        string starterSpeciesId = "",
        string starterBackendChainlingId = "")
    {
        PlayerAccountSession resolved = session ?? PlayerAccountSession.Empty();
        SetFromProfile(
            walletAddress: resolved.WalletPublicKey,
            isNewPlayer: resolved.profile != null && resolved.profile.isNewProfile,
            introComplete: resolved.profile != null && resolved.profile.introComplete,
            introCinematicComplete: resolved.profile != null && resolved.profile.introCinematicComplete,
            desiredSpawnTag: desiredSpawnTag,
            pendingQuestId: pendingQuestId,
            isOfflineSession: isOfflineSession || resolved.fetchError,
            displayName: resolved.profile != null ? resolved.profile.displayName : "",
            starterSpeciesId: starterSpeciesId,
            starterBackendChainlingId: starterBackendChainlingId,
            playerProfileId: resolved.PlayerProfileId,
            authUserId: resolved.profile != null ? resolved.profile.authUserId : "",
            activePlatformBuild: resolved.activePlatformBuild,
            lastSignInMethod: resolved.lastSignInMethod,
            platformIdentityType: resolved.platformIdentity != null ? resolved.platformIdentity.platformIdentityType : PlatformIdentityType.None,
            platformIdentitySubject: resolved.platformIdentity != null ? resolved.platformIdentity.platformIdentitySubject : "",
            walletProviderType: resolved.walletIdentity != null ? resolved.walletIdentity.walletProviderType : WalletProviderType.None,
            accountLinkState: resolved.accountLinkState,
            isGuestSession: resolved.isGuestSession);
    }

    /// <summary>
    /// Set the sequence to play on next scene load. Called by MainMenuSequenceController2 when routing to intro path.
    /// </summary>
    public void SetPendingSequence(ScriptableObject sequence)
    {
        PendingSequence = sequence;
    }

    /// <summary>
    /// Clear the pending sequence after it has been started. Called by PendingSequenceStarter.
    /// </summary>
    public void ClearPendingSequence()
    {
        PendingSequence = null;
    }

    /// <summary>
    /// Call from the end of the intro sequence to flip the local flag
    /// (the remote update is handled separately by SupabaseIntroCompleteHelper).
    /// </summary>
    public void MarkIntroComplete()
    {
        IntroComplete = true;
    }

    public void MarkIntroCinematicComplete()
    {
        IntroCinematicComplete = true;
    }

    public void SetDisplayName(string displayName)
    {
        DisplayName = displayName != null ? displayName.Trim() : "";
    }

    public void SetStarterResult(string speciesId, string backendChainlingId)
    {
        StarterSpeciesId = speciesId != null ? speciesId.Trim() : "";
        StarterBackendChainlingId = backendChainlingId != null ? backendChainlingId.Trim() : "";
    }

    // ── Debug ─────────────────────────────────────────────────────────────────

    public override string ToString() =>
        $"[GameSessionContext] profile={PlayerProfileId} wallet={WalletAddress} platform={ActivePlatformBuild} " +
        $"signIn={LastSignInMethod} linkState={AccountLinkState} guest={IsGuestSession} newPlayer={IsNewPlayer} " +
        $"cinematicComplete={IntroCinematicComplete} introComplete={IntroComplete} spawn={DesiredSpawnTag} quest={PendingQuestId} " +
        $"pendingSeq={(PendingSequence != null ? PendingSequence.name : "null")} offline={IsOfflineSession} " +
        $"displayName={DisplayName} starterSpecies={StarterSpeciesId} starterChainling={StarterBackendChainlingId}";
}
