using UnityEngine;

/// <summary>
/// Runtime owner for normal NPC interaction presentation sessions.
/// Starts from <see cref="NPCInteractableBinding"/>, can be claimed by downstream dialogue,
/// and restores NPC autonomy immediately when the session ends.
/// </summary>
public class NPCInteractionSessionService : MonoBehaviour
{
    private enum SessionPhase
    {
        PendingDialogueClaim,
        ClaimedByDialogue
    }

    private sealed class SessionState
    {
        public int SessionId;
        public NPCController Npc;
        public NPCInteractionBehaviorSO Behavior;
        public RouteOverrideHandle RouteOverrideHandle;
        public Transform PlayerTransform;
        public float PendingExpiryTime;
        public int ClaimedPlaybackId;
        public SessionPhase Phase;
        public bool TrackingPlayer;
        public bool RestoreIdleFacingOnRelease;
        public ExplorationSpritePresentationPresenter OwnedPresentationPresenter;
        public int OwnedPresentationPlaybackId;
    }

    private static NPCInteractionSessionService instance;

    [Header("Behavior")]
    [Tooltip("How long a normal interaction session stays pending while waiting for downstream dialogue to claim it.")]
    [SerializeField] private float downstreamClaimGraceSeconds = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private DialogueService dialogueService;
    private SessionState activeSession;
    private int nextSessionId = 1;

    public static NPCInteractionSessionService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<NPCInteractionSessionService>();
                if (instance != null)
                    return instance;

                // Edit Mode callers (including authoring validation and tests) must
                // never manufacture a persistent runtime service as a side effect.
                if (!Application.isPlaying)
                    return null;

                GameServicesContainer servicesContainer = GameServicesContainer.Instance != null
                    ? GameServicesContainer.Instance
                    : FindFirstObjectByType<GameServicesContainer>(FindObjectsInactive.Include);

                GameObject go = new GameObject("NPCInteractionSessionService");
                if (servicesContainer != null)
                    go.transform.SetParent(servicesContainer.transform, false);

                instance = go.AddComponent<NPCInteractionSessionService>();
            }

            return instance;
        }
    }

    public bool HasActiveSession => activeSession != null;

    public bool TryGetActiveSessionId(NPCController npc, out int sessionId)
    {
        if (activeSession != null && activeSession.Npc == npc)
        {
            sessionId = activeSession.SessionId;
            return true;
        }

        sessionId = 0;
        return false;
    }

    public bool IsSessionActive(int sessionId)
    {
        return activeSession != null &&
               sessionId > 0 &&
               activeSession.SessionId == sessionId;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning($"[NPCInteractionSessionService] Duplicate instance found. Destroying {gameObject.name}");
                Destroy(gameObject);
                return;
            }
        }

        instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        AttachDialogueService(FindFirstObjectByType<DialogueService>());
    }

    private void OnDisable()
    {
        DetachDialogueService();
        CancelActiveSession("service disabled");
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (dialogueService == null)
            AttachDialogueService(FindFirstObjectByType<DialogueService>());

        if (activeSession == null)
            return;

        if (activeSession.Phase == SessionPhase.PendingDialogueClaim)
        {
            if (Time.unscaledTime >= activeSession.PendingExpiryTime)
                ReleaseSession("no downstream dialogue claimed the interaction session");

            return;
        }

        if (dialogueService == null || !dialogueService.IsPlaying)
        {
            ReleaseSession("claimed dialogue ended unexpectedly");
            return;
        }

        if (activeSession.TrackingPlayer)
        {
            Transform playerTransform = ResolvePlayerTransform();
            if (playerTransform != activeSession.PlayerTransform)
            {
                activeSession.PlayerTransform = playerTransform;
                activeSession.Npc?.SetFacingTargetOverride(playerTransform);
            }
        }
    }

    public void BeginNormalInteraction(NPCController npc)
    {
        BeginInteraction(npc, npc != null ? npc.InteractionBehavior : null, "normal interaction");
    }

    public void BeginInteraction(
        NPCController npc,
        NPCInteractionBehaviorSO behavior,
        string contextLabel = "interaction")
    {
        CancelActiveSession($"replaced by a newer {contextLabel}");

        if (npc == null || behavior == null)
            return;

        Transform playerTransform = ResolvePlayerTransform();
        SessionState session = new SessionState
        {
            SessionId = nextSessionId++,
            Npc = npc,
            Behavior = behavior,
            PlayerTransform = playerTransform,
            PendingExpiryTime = Time.unscaledTime + Mathf.Max(0f, downstreamClaimGraceSeconds),
            ClaimedPlaybackId = 0,
            Phase = SessionPhase.PendingDialogueClaim,
            TrackingPlayer = false
        };

        if (session.Behavior.PauseRouteWhileInteracting)
        {
            session.RouteOverrideHandle = npc.AcquireRouteOverride(
                ToRouteOverrideMode(session.Behavior.RoutePauseMode),
                $"{contextLabel} session");
        }

        bool restoreIdleFacing = session.Behavior.FacePlayerOnStart ||
                                 session.Behavior.TrackPlayerWhileUnlockedDialogue;
        if (restoreIdleFacing)
        {
            npc.CaptureIdleFacing();
            session.RestoreIdleFacingOnRelease = true;
        }

        if (session.Behavior.FacePlayerOnStart && playerTransform != null)
            npc.SnapFaceTarget(playerTransform);

        activeSession = session;
        LogDebug($"Started {contextLabel} session {session.SessionId} for NPC '{npc.NPCId}'.");
    }

    public void CancelActiveSession(string reason = "cancelled")
    {
        ReleaseSession(reason);
    }

    public void TryBindPresentationOwnership(
        NPCController npc,
        ExplorationSpritePresentationPresenter presenter,
        int playbackId)
    {
        if (activeSession == null || npc == null || presenter == null || playbackId <= 0)
            return;

        if (activeSession.Npc != npc)
            return;

        activeSession.OwnedPresentationPresenter = presenter;
        activeSession.OwnedPresentationPlaybackId = playbackId;

        LogDebug(
            $"Bound presentation playback {playbackId} on '{npc.NPCId}' to interaction session {activeSession.SessionId}.");
    }

    private void HandleDialoguePlaybackStarted(DialoguePlaybackContext playback)
    {
        if (activeSession == null)
            return;

        if (activeSession.Phase == SessionPhase.PendingDialogueClaim)
        {
            ClaimPendingSession(playback);
            return;
        }

        if (activeSession.Phase == SessionPhase.ClaimedByDialogue)
        {
            // Keep the same interaction session alive across immediate dialogue handoffs
            // while the current normal interaction session is already active.
            activeSession.ClaimedPlaybackId = playback.PlaybackId;
            ApplyTrackingForPlayback(activeSession, playback);
            LogDebug($"Transferred interaction session {activeSession.SessionId} to dialogue playback {playback.PlaybackId}.");
        }
    }

    private void HandleDialoguePlaybackComplete(DialoguePlaybackContext playback)
    {
        if (activeSession == null || activeSession.Phase != SessionPhase.ClaimedByDialogue)
            return;

        if (activeSession.ClaimedPlaybackId != playback.PlaybackId)
            return;

        ReleaseSession($"dialogue playback {playback.PlaybackId} completed");
    }

    private void ClaimPendingSession(DialoguePlaybackContext playback)
    {
        if (activeSession == null)
            return;

        activeSession.Phase = SessionPhase.ClaimedByDialogue;
        activeSession.ClaimedPlaybackId = playback.PlaybackId;
        ApplyTrackingForPlayback(activeSession, playback);

        LogDebug($"Claimed interaction session {activeSession.SessionId} with dialogue playback {playback.PlaybackId}.");
    }

    private void ApplyTrackingForPlayback(SessionState session, DialoguePlaybackContext playback)
    {
        if (session == null || session.Npc == null)
            return;

        bool shouldTrack = !playback.LocksInput &&
                           session.Behavior != null &&
                           session.Behavior.TrackPlayerWhileUnlockedDialogue;

        if (!shouldTrack)
        {
            session.Npc.ClearFacingTargetOverride();
            session.TrackingPlayer = false;
            return;
        }

        session.PlayerTransform = ResolvePlayerTransform();
        session.Npc.SetFacingTargetOverride(session.PlayerTransform, true);
        session.TrackingPlayer = session.PlayerTransform != null;
    }

    private void ReleaseSession(string reason)
    {
        if (activeSession == null)
            return;

        SessionState session = activeSession;
        activeSession = null;

        if (session.OwnedPresentationPresenter != null &&
            session.OwnedPresentationPlaybackId > 0 &&
            session.OwnedPresentationPresenter.CurrentPlaybackId == session.OwnedPresentationPlaybackId)
        {
            session.OwnedPresentationPresenter.StopActiveClip();
        }

        if (session.Npc != null)
        {
            session.Npc.ClearFacingTargetOverride();
            if (session.RestoreIdleFacingOnRelease)
                session.Npc.RestoreCapturedIdleFacing();
        }

        if (session.RouteOverrideHandle != null && !session.RouteOverrideHandle.IsReleased)
            session.RouteOverrideHandle.Release();

        LogDebug($"Released interaction session {session.SessionId}: {reason}.");
    }

    private void AttachDialogueService(DialogueService service)
    {
        if (service == null || dialogueService == service)
            return;

        DetachDialogueService();

        dialogueService = service;
        dialogueService.OnDialoguePlaybackStarted += HandleDialoguePlaybackStarted;
        dialogueService.OnDialoguePlaybackComplete += HandleDialoguePlaybackComplete;
    }

    private void DetachDialogueService()
    {
        if (dialogueService == null)
            return;

        dialogueService.OnDialoguePlaybackStarted -= HandleDialoguePlaybackStarted;
        dialogueService.OnDialoguePlaybackComplete -= HandleDialoguePlaybackComplete;
        dialogueService = null;
    }

    private static RouteOverrideMode ToRouteOverrideMode(NPCInteractionRoutePauseMode pauseMode)
    {
        switch (pauseMode)
        {
            case NPCInteractionRoutePauseMode.DisableSchedule:
                return RouteOverrideMode.DisableSchedule;
            case NPCInteractionRoutePauseMode.DisableAutonomy:
                return RouteOverrideMode.DisableAutonomy;
            case NPCInteractionRoutePauseMode.ForceHoldPosition:
            default:
                return RouteOverrideMode.ForceHoldPosition;
        }
    }

    private static Transform ResolvePlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[NPCInteractionSessionService] {message}", this);
    }
}
