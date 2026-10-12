using System;
using System.Collections;
using System.Threading.Tasks;
using Chainbound.Combat;
using Chainbound.Combat.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Centralized pre-battle transition flow for overworld encounters.
/// Freezes input, updates the dynamic encounter return spawn, saves return context,
/// plays a short lead-in presentation, then hands off to SceneLoader.
/// Chromatic lead-in uses an owned overlay Volume (weight only) so aborted
/// transitions and Play Mode stops cannot leak into the authored global profile.
/// </summary>
public class EncounterTransitionBridge : MonoBehaviour
{
    /// <summary>
    /// Identifies which runtime service must provide the encounter payload in
    /// the destination battle scene. This prevents gameplay transitions from
    /// silently borrowing an unrelated manual or combat-test provider.
    /// </summary>
    public enum EncounterLaunchSource
    {
        Unspecified = 0,
        Narrative = 1,
        Zone = 2,
        ManualEncounter = 3,
        AuthoritativeResume = 4,
    }

    [Serializable]
    public sealed class EncounterTransitionRequest
    {
        public Transform playerTransform;
        public string battleSceneName = "Combat2_TestHarness";
        [Tooltip("Combat stage id for the shared battle scene. Empty resolves to loomville at load.")]
        public string battleSpawnId = "";
        public string returnSpawnId = "";
        public SoundtrackCueSO soundtrackOverride;
        public bool forceRestartSoundtrack;
        public TransitionStyle transitionStyle = TransitionStyle.QuickFade;
        public bool useSceneLoader = true;
        public bool skipSaveReturnContext = false;
        public float preEncounterDelay = -1f;
        public EncounterLaunchSource launchSource = EncounterLaunchSource.Unspecified;
        public string authoritativeZoneId = "";
        public string authoritativeNarrativeEncounterId = "";
        public bool skipAuthoritativeSession;
        public Action onReadyToLoad;
    }

    private static EncounterTransitionBridge instance;

    public static EncounterTransitionBridge Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<EncounterTransitionBridge>(true);
                if (instance == null)
                {
                    GameServicesContainer servicesContainer = GameServicesContainer.Instance != null
                        ? GameServicesContainer.Instance
                        : FindObjectOfType<GameServicesContainer>(true);

                    GameObject go = new GameObject("EncounterTransitionBridge");
                    if (servicesContainer != null)
                        go.transform.SetParent(servicesContainer.transform, false);

                    instance = go.AddComponent<EncounterTransitionBridge>();

                    if (servicesContainer == null)
                        DontDestroyOnLoad(go);
                }
            }

            return instance;
        }
    }

    [Header("Return Spawn")]
    [SerializeField] private string encounterReturnSpawnId = "encounter_return";

    [Header("Timing")]
    [SerializeField] private float defaultPreEncounterDelay = 1.5f;
    [SerializeField] private float chromaticAberrationPeak = 0.8f;
    [SerializeField] private AnimationCurve chromaticCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Visuals")]
    [Tooltip("Owned overlay Volume for lead-in chromatic. Auto-added when omitted.")]
    [SerializeField] private EncounterTransitionPostFxVolume leadInVolume;
    [Tooltip("Optional explicit persistent transition overlay. When omitted the bridge resolves the GameServices overlay.")]
    [SerializeField] private EncounterTransitionGlitchOverlay transitionGlitchOverlay;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private bool isTransitionActive;
    private bool lastTransitionIssuedLoad;
    private string pendingBattleSceneLaunchTicket = string.Empty;
    private EncounterLaunchSource pendingBattleSceneLaunchSource = EncounterLaunchSource.Unspecified;
    private string authorityGateMessage = string.Empty;

    public string AuthorityGateMessage => authorityGateMessage;
    public static event Action<string> AuthorityGateMessageChanged;

    /// <summary>True when the most recent request successfully invoked scene loading.</summary>
    public bool LastTransitionIssuedLoad => lastTransitionIssuedLoad;

    /// <summary>
    /// Consumes the one-shot provenance ticket for a battle scene. A ticket is
    /// issued only when this bridge accepts a gameplay scene transition. Direct
    /// harness launches therefore have no ticket and may use explicit test data.
    /// </summary>
    public bool TryConsumeBattleSceneLaunchTicket(
        string battleSceneName,
        out EncounterLaunchSource launchSource)
    {
        launchSource = EncounterLaunchSource.Unspecified;

        if (string.IsNullOrWhiteSpace(pendingBattleSceneLaunchTicket))
            return false;

        string expectedScene = pendingBattleSceneLaunchTicket;
        EncounterLaunchSource expectedSource = pendingBattleSceneLaunchSource;

        // A battle scene arrival consumes any outstanding ticket. If the scene
        // does not match, the ticket was stale and must not affect a later test.
        pendingBattleSceneLaunchTicket = string.Empty;
        pendingBattleSceneLaunchSource = EncounterLaunchSource.Unspecified;

        if (!string.Equals(expectedScene, battleSceneName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning(
                $"[EncounterTransitionBridge] Discarded battle launch ticket for '{expectedScene}' " +
                $"because the active battle scene is '{battleSceneName}'.");
            return false;
        }

        launchSource = expectedSource;
        return true;
    }

    public void ArmBattleSceneLaunchTicket(string battleSceneName, EncounterLaunchSource launchSource)
    {
        pendingBattleSceneLaunchTicket = battleSceneName?.Trim() ?? string.Empty;
        pendingBattleSceneLaunchSource = launchSource;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[EncounterTransitionBridge] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);

        ResolveLeadInVolume();
    }

    private void OnDisable()
    {
        if (leadInVolume != null)
            leadInVolume.ForceClear();
    }

    private void OnDestroy()
    {
        if (leadInVolume != null)
            leadInVolume.ForceClear();

        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// Clears the encounter lead-in overlay Volume. Safe after returning to the
    /// overworld if the persistent bridge still has a leftover weight.
    /// </summary>
    public void ResetChromaticAberration()
    {
        ResolveLeadInVolume().ForceClear();
        if (enableDebugLogs)
            Debug.Log("[EncounterTransitionBridge] Encounter lead-in overlay volume cleared.");
    }

    public IEnumerator BeginEncounterTransition(EncounterTransitionRequest request)
    {
        if (request == null)
        {
            Debug.LogError("[EncounterTransitionBridge] Cannot begin encounter transition with a null request.");
            yield break;
        }

        if (isTransitionActive)
        {
            Debug.LogWarning("[EncounterTransitionBridge] Encounter transition already in progress. Ignoring duplicate request.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(request.battleSceneName))
        {
            Debug.LogError("[EncounterTransitionBridge] Battle scene name is empty. Aborting encounter transition.");
            yield break;
        }

        bool authorityReady = false;
        yield return EnsureAuthoritativeBattleSession(request, ready => authorityReady = ready);
        if (!authorityReady)
            yield break;

        isTransitionActive = true;
        lastTransitionIssuedLoad = false;
        pendingBattleSceneLaunchTicket = string.Empty;
        pendingBattleSceneLaunchSource = EncounterLaunchSource.Unspecified;

        InputLockHandle inputLockHandle = default;
        PlayerMovement3D playerMovement = null;
        PlayerAnimationController3D_Mesh playerAnimationController = null;
        EncounterTransitionGlitchOverlay glitchOverlay = null;
        EncounterLeadInCue encounterCue = null;
        bool startedExternalAnimationLock = false;

        try
        {
            GameObject playerRoot = ResolvePlayerRoot(request.playerTransform);
            Transform playerTransform = playerRoot != null ? playerRoot.transform : request.playerTransform;
            playerAnimationController = ResolvePlayerAnimationController(playerRoot);
            if (playerTransform != null)
                SanctuaryLatchZone.TryLatchNearestPortal(playerTransform.position);

            InputLockService inputLockService = InputLockService.Instance;
            if (inputLockService != null)
            {
                inputLockHandle = inputLockService.AcquireLock("EncounterTransition", InputLockChannel.All, this);
            }

            if (playerRoot != null)
            {
                playerMovement = playerRoot.GetComponent<PlayerMovement3D>();
                if (playerMovement != null)
                    playerMovement.SetFrozen(true, preservePhysics: false);
            }

            string returnSpawnId = ResolveReturnSpawnId(request.returnSpawnId);
            // Always move dynamic encounter_return spawn before battle; snapshot position may come from an earlier step.
            ConfigureReturnSpawnIfNeeded(returnSpawnId, playerRoot);

            if (!request.skipSaveReturnContext)
            {
                SaveReturnContext(playerTransform, returnSpawnId);
            }
            else
            {
                MergeReturnSpawnIdOnly(returnSpawnId);
                if (enableDebugLogs)
                {
                    Debug.Log(
                        "[EncounterTransitionBridge] skipSaveReturnContext: merged returnSpawnId only; " +
                        "position/rotation left from prior snapshot.");
                }
            }

            float delaySeconds = request.preEncounterDelay >= 0f
                ? request.preEncounterDelay
                : defaultPreEncounterDelay;

            PublishBattleSoundtrack(request);

            if (delaySeconds > 0f)
            {
                if (playerAnimationController != null)
                {
                    playerAnimationController.BeginExternalAnimationLock(MeshRendererPlayerVisual.AnimationState.Alert);
                    startedExternalAnimationLock = true;
                }

                encounterCue = EncounterLeadInCue.FindOnPlayer(playerRoot);
                if (encounterCue != null)
                    encounterCue.Show();

                glitchOverlay = ResolveGlitchOverlay();
                if (glitchOverlay != null)
                {
                    glitchOverlay.PlayForDuration(delaySeconds);
                }

                yield return ResolveLeadInVolume().AnimateChromaticWeight(
                    chromaticAberrationPeak,
                    delaySeconds,
                    chromaticCurve);
            }

            if (!CanStartSceneLoad(request))
                yield break;

            if (request.launchSource != EncounterLaunchSource.Narrative)
                SignalJam.Narrative.Combat.NarrativeCombatLaunchBridge.ClearPendingPayload();

            request.onReadyToLoad?.Invoke();

            // Publish provenance before asking SceneLoader to activate the
            // destination so its Awake methods can consume it immediately.
            pendingBattleSceneLaunchTicket = request.battleSceneName.Trim();
            pendingBattleSceneLaunchSource = request.launchSource;
            SanctuarySpawnDirector.ClearPendingPresentation();
            lastTransitionIssuedLoad = StartSceneLoad(request);

            if (!lastTransitionIssuedLoad)
            {
                pendingBattleSceneLaunchTicket = string.Empty;
                pendingBattleSceneLaunchSource = EncounterLaunchSource.Unspecified;
            }
        }
        finally
        {
            if (glitchOverlay != null)
                glitchOverlay.HideImmediate();

            ResolveLeadInVolume().ForceClear();

            if (startedExternalAnimationLock && playerAnimationController != null)
                playerAnimationController.EndExternalAnimationLock();

            if (encounterCue != null)
                encounterCue.HideImmediate();

            if (playerMovement != null)
                playerMovement.SetFrozen(false, preservePhysics: false);

            if (inputLockHandle.IsValid && !inputLockHandle.IsReleased)
                inputLockHandle.Release();

            isTransitionActive = false;
        }
    }

    private bool CanStartSceneLoad(EncounterTransitionRequest request)
    {
        if (!request.useSceneLoader)
            return true;

        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
            return true;

        if (!sceneLoader.IsTransitioning)
            return true;

        Debug.LogWarning("[EncounterTransitionBridge] SceneLoader is already transitioning. Encounter battle load was aborted.");
        return false;
    }

    private IEnumerator EnsureAuthoritativeBattleSession(EncounterTransitionRequest request, Action<bool> completed)
    {
        if (request.skipAuthoritativeSession)
        {
            AuthoritativeBattleLaunchContext.Clear();
            SetAuthorityGateMessage(string.Empty);
            completed?.Invoke(true);
            yield break;
        }

        AuthoritativeCombatSessionService service = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
        if (service == null && SolanaServiceBridge.Instance != null)
            service = SolanaServiceBridge.Instance.CombatSessionService as AuthoritativeCombatSessionService;

        if (service == null || !service.IsRolloutEnabled)
        {
            AuthoritativeBattleLaunchContext.Clear();
            SetAuthorityGateMessage(string.Empty);
            completed?.Invoke(true);
            yield break;
        }

        Task<bool> availability = service.RefreshAvailabilityAsync();
        while (!availability.IsCompleted)
            yield return null;
        if (availability.IsFaulted || availability.IsCanceled || !availability.Result)
        {
            Exception availabilityError = availability.Exception != null ? availability.Exception.GetBaseException() : null;
            string detail = availabilityError != null ? availabilityError.Message : service.LastAuthorityError;
            if (BeginLocalPractice(service, null))
            {
                completed?.Invoke(true);
                yield break;
            }

            SetAuthorityGateMessage("SIGNAL SYNC UNAVAILABLE — RETRYING");
            if (!string.IsNullOrWhiteSpace(detail))
                Debug.LogWarning($"[EncounterTransitionBridge] Battle service is not ready: {detail}", this);
            completed?.Invoke(false);
            yield break;
        }

        var startRequest = new StartCombatSessionRequest
        {
            CommandId = Guid.NewGuid().ToString("D"),
            ZoneId = request.authoritativeZoneId ?? string.Empty,
            NarrativeEncounterId = request.authoritativeNarrativeEncounterId ?? string.Empty,
        };
        Task<CombatSessionEnvelope> start = service.StartEncounterAsync(startRequest);
        while (!start.IsCompleted)
            yield return null;
        if (start.IsFaulted || start.IsCanceled || start.Result == null)
        {
            Exception error = start.Exception != null ? start.Exception.GetBaseException() : null;
            string failureCode = error is AuthoritativeCombatServiceException authorityError ? authorityError.Code : null;
            if (error != null)
                Debug.LogWarning($"[EncounterTransitionBridge] Authoritative battle start failed: {error.Message}", this);
            if (BeginLocalPractice(service, failureCode))
            {
                completed?.Invoke(true);
                yield break;
            }

            SetAuthorityGateMessage(string.Equals(failureCode, "upgrade_required", StringComparison.Ordinal)
                ? "GAME DATA UPDATE REQUIRED"
                : "SIGNAL SYNC UNAVAILABLE — RETRYING");
            completed?.Invoke(false);
            yield break;
        }

        if (start.Result.RequiresResumeChoice || start.Result.RequiresWipe)
        {
            PendingBattleResumeCoordinator coordinator = PendingBattleResumeCoordinator.Instance;
            if (coordinator == null)
            {
                Debug.LogError("[EncounterTransitionBridge] Pending battle prompt is unavailable; refusing to choose for the player.", this);
                completed?.Invoke(false);
                yield break;
            }

            Task<PendingBattleResumeResult> decision = coordinator.ResolveEnvelopeAsync(start.Result);
            while (!decision.IsCompleted)
                yield return null;
            if (decision.IsFaulted || decision.IsCanceled)
            {
                Debug.LogWarning("[EncounterTransitionBridge] Pending battle decision did not complete.", this);
                completed?.Invoke(false);
                yield break;
            }

            if (decision.Result.Outcome == PendingBattleResumeOutcome.Wipe)
            {
                PendingBattleRouteUtility.RequestWipe(request.transitionStyle, "Encounter.PendingBattleWipe");
                completed?.Invoke(false);
                yield break;
            }
            if (decision.Result.Outcome != PendingBattleResumeOutcome.Rejoin)
            {
                completed?.Invoke(false);
                yield break;
            }
            request.launchSource = EncounterLaunchSource.AuthoritativeResume;
        }
        else
        {
            AuthoritativeBattleLaunchContext.Publish(start.Result);
        }
        SetAuthorityGateMessage(string.Empty);
        completed?.Invoke(true);
    }

    /// <summary>
    /// Editor and test runs may start the existing local encounter when the battle
    /// service does not answer. A manifest mismatch still refuses the battle.
    /// </summary>
    public static bool ShouldUseLocalPracticeBattle(bool allowsLocalPractice, string failureCode)
    {
        if (!allowsLocalPractice)
            return false;
        if (string.Equals(failureCode, "upgrade_required", StringComparison.Ordinal))
            return false;
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        return true;
#else
        return false;
#endif
    }

    private bool BeginLocalPractice(AuthoritativeCombatSessionService service, string failureCode)
    {
        bool allowsLocalPractice = service == null || service.AllowsLocalPractice;
        if (!ShouldUseLocalPracticeBattle(allowsLocalPractice, failureCode))
            return false;

        AuthoritativeBattleLaunchContext.Clear();
        SetAuthorityGateMessage(string.Empty);
        Debug.LogWarning("[EncounterTransitionBridge] Battle service did not answer. Starting the local encounter.");
        return true;
    }

    private void SetAuthorityGateMessage(string message)
    {
        string value = message ?? string.Empty;
        if (string.Equals(authorityGateMessage, value, StringComparison.Ordinal))
            return;
        authorityGateMessage = value;
        AuthoritativeBattleLaunchContext.SetGateMessage(value);
        AuthorityGateMessageChanged?.Invoke(value);
        if (!string.IsNullOrWhiteSpace(value))
            Debug.LogWarning($"[EncounterTransitionBridge] {value}", this);
    }

    private void PublishBattleSoundtrack(EncounterTransitionRequest request)
    {
        MusicConductor conductor = MusicConductor.Instance;
        if (conductor == null || string.IsNullOrWhiteSpace(request.battleSceneName))
            return;

        conductor.RequestSceneSoundtrack(
            request.battleSceneName,
            SoundtrackTriggerMoment.TransitionStart,
            request.soundtrackOverride,
            request.forceRestartSoundtrack);

        if (enableDebugLogs)
        {
            string cueId = request.soundtrackOverride != null ? request.soundtrackOverride.Id : "<scene-catalog>";
            Debug.Log(
                $"[EncounterTransitionBridge] Published battle soundtrack request for '{request.battleSceneName}' " +
                $"(override={cueId}, forceRestart={request.forceRestartSoundtrack}).");
        }
    }

    private bool StartSceneLoad(EncounterTransitionRequest request)
    {
        string battleSpawnId = CombatStageIds.Normalize(request.battleSpawnId);
        request.battleSpawnId = battleSpawnId;
        CombatStageRequest.SetPending(battleSpawnId);

        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
        {
            Debug.LogError("[EncounterTransitionBridge] SceneLoader is unavailable; encounter transition was cancelled safely.");
            return false;
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[EncounterTransitionBridge] Loading battle scene '{request.battleSceneName}' via SceneLoader (style: {request.transitionStyle}, spawn: '{battleSpawnId}').");
        }

        if (!request.useSceneLoader)
            Debug.LogWarning("[EncounterTransitionBridge] Direct scene loading is retired; routing this request through SceneLoader.");

        return sceneLoader.RequestTransition(new SceneTransitionRequest(
            request.battleSceneName,
            battleSpawnId,
            request.transitionStyle,
            sourceId: "EncounterTransitionBridge"));
    }

    private void ConfigureReturnSpawnIfNeeded(string returnSpawnId, GameObject playerRoot)
    {
        if (string.IsNullOrWhiteSpace(returnSpawnId))
            return;

        SpawnService spawnService = SpawnService.Instance;
        if (spawnService == null)
        {
            Debug.LogWarning("[EncounterTransitionBridge] SpawnService not found. Encounter return spawn could not be configured.");
            return;
        }

        SpawnPoint spawnPoint = spawnService.GetSpawnPoint(returnSpawnId);
        if (spawnPoint == null &&
            string.Equals(returnSpawnId, encounterReturnSpawnId, StringComparison.OrdinalIgnoreCase))
        {
            spawnPoint = EnsurePersistentEncounterReturnSpawn(returnSpawnId);
        }
        if (spawnPoint == null)
        {
            Debug.LogWarning($"[EncounterTransitionBridge] SpawnPoint '{returnSpawnId}' not found in scene '{SceneManager.GetActiveScene().name}'. Post-battle return will fall back to snapshot placement.");
            return;
        }

        if (!string.Equals(returnSpawnId, encounterReturnSpawnId, StringComparison.OrdinalIgnoreCase))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[EncounterTransitionBridge] Using authored return spawn '{returnSpawnId}' without dynamic repositioning.");
            }
            return;
        }

        if (playerRoot == null)
        {
            Debug.LogWarning("[EncounterTransitionBridge] Player root not found. Dynamic encounter return spawn could not be aligned.");
            return;
        }

        bool isIndoor = ResolveIsIndoor();
        spawnPoint.ConfigureFromPlayer(playerRoot, isIndoor);
        spawnPoint.ForceReRegister();

        if (enableDebugLogs)
        {
            Debug.Log($"[EncounterTransitionBridge] Configured dynamic return spawn '{returnSpawnId}' at {playerRoot.transform.position} (indoor: {isIndoor}).");
        }
    }

    /// <summary>
    /// Provides a sticky fallback for streamed corridors that do not author a
    /// local <c>encounter_return</c> point yet. The exact snapshot remains the
    /// return source of truth; this point only preserves existing bridge
    /// behavior and is parented under persistent GameServices.
    /// </summary>
    private SpawnPoint EnsurePersistentEncounterReturnSpawn(string returnSpawnId)
    {
        SpawnPoint[] candidates = FindObjectsByType<SpawnPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            SpawnPoint candidate = candidates[i];
            if (candidate != null &&
                string.Equals(candidate.SpawnId, returnSpawnId, StringComparison.OrdinalIgnoreCase) &&
                candidate.gameObject.scene.name == "DontDestroyOnLoad")
            {
                return candidate;
            }
        }

        GameServicesContainer services = GameServicesContainer.Instance != null
            ? GameServicesContainer.Instance
            : FindFirstObjectByType<GameServicesContainer>(FindObjectsInactive.Include);
        GameObject host = new GameObject("Persistent Encounter Return Spawn");
        if (services != null)
            host.transform.SetParent(services.transform, false);
        else
            DontDestroyOnLoad(host);

        SpawnPoint spawnPoint = host.AddComponent<SpawnPoint>();
        spawnPoint.ConfigureRuntimeSpawnId(returnSpawnId, isIndoor: false);
        spawnPoint.ForceReRegister();
        return spawnPoint;
    }

    private void SaveReturnContext(Transform playerTransform, string returnSpawnId)
    {
        IEncounterReturnContextService returnContextService = ResolveReturnContextService();
        Vector3 position = playerTransform != null ? playerTransform.position : Vector3.zero;
        Quaternion rotation = playerTransform != null ? playerTransform.rotation : Quaternion.identity;

        returnContextService.SaveReturnContext(
            SceneManager.GetActiveScene().name,
            position,
            rotation,
            string.IsNullOrWhiteSpace(returnSpawnId) ? null : returnSpawnId,
            EncounterReturnSnapshotUtility.CaptureLoadedBuildSceneIds());

        if (enableDebugLogs)
        {
            Debug.Log($"[EncounterTransitionBridge] Saved return context: scene='{SceneManager.GetActiveScene().name}', position={position}, rotation={rotation.eulerAngles}, returnSpawnId='{returnSpawnId}'.");
        }
    }

    private void MergeReturnSpawnIdOnly(string returnSpawnId)
    {
        IEncounterReturnContextService svc = ResolveReturnContextService();
        svc.SetReturnSpawnIdOnly(string.IsNullOrWhiteSpace(returnSpawnId) ? null : returnSpawnId);
    }

    private IEncounterReturnContextService ResolveReturnContextService()
    {
        SolanaServiceBridge solanaBridge = SolanaServiceBridge.Instance != null
            ? SolanaServiceBridge.Instance
            : FindObjectOfType<SolanaServiceBridge>(true);

        return solanaBridge != null && solanaBridge.EncounterReturnContextService != null
            ? solanaBridge.EncounterReturnContextService
            : new EncounterReturnContextService();
    }

    private GameObject ResolvePlayerRoot(Transform requestTransform)
    {
        if (requestTransform != null)
        {
            PlayerMovement3D movement = requestTransform.GetComponent<PlayerMovement3D>()
                ?? requestTransform.GetComponentInParent<PlayerMovement3D>();
            if (movement != null)
                return movement.gameObject;

            return requestTransform.gameObject;
        }

        SpawnService spawnService = SpawnService.Instance;
        if (spawnService != null)
        {
            GameObject resolved = spawnService.GetResolvedPlayerRoot();
            if (resolved != null)
                return resolved;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer == null)
            return null;

        PlayerMovement3D taggedMovement = taggedPlayer.GetComponent<PlayerMovement3D>()
            ?? taggedPlayer.GetComponentInParent<PlayerMovement3D>();
        return taggedMovement != null ? taggedMovement.gameObject : taggedPlayer;
    }

    private PlayerAnimationController3D_Mesh ResolvePlayerAnimationController(GameObject playerRoot)
    {
        if (playerRoot == null)
            return null;

        return playerRoot.GetComponent<PlayerAnimationController3D_Mesh>()
            ?? playerRoot.GetComponentInChildren<PlayerAnimationController3D_Mesh>(true);
    }

    private EncounterTransitionPostFxVolume ResolveLeadInVolume()
    {
        if (leadInVolume != null)
            return leadInVolume;

        leadInVolume = GetComponent<EncounterTransitionPostFxVolume>();
        if (leadInVolume == null)
            leadInVolume = gameObject.AddComponent<EncounterTransitionPostFxVolume>();

        return leadInVolume;
    }

    private EncounterTransitionGlitchOverlay ResolveGlitchOverlay()
    {
        if (transitionGlitchOverlay != null)
            return transitionGlitchOverlay;

        transitionGlitchOverlay = EncounterTransitionGlitchOverlay.Instance;
        if (transitionGlitchOverlay != null)
            return transitionGlitchOverlay;

        GameServicesContainer servicesContainer = GameServicesContainer.Instance != null
            ? GameServicesContainer.Instance
            : FindObjectOfType<GameServicesContainer>(true);

        if (servicesContainer == null)
            return FindObjectOfType<EncounterTransitionGlitchOverlay>(true);

        transitionGlitchOverlay = servicesContainer.GetComponentInChildren<EncounterTransitionGlitchOverlay>(true);
        return transitionGlitchOverlay;
    }

    private string ResolveReturnSpawnId(string requestReturnSpawnId)
    {
        if (!string.IsNullOrWhiteSpace(requestReturnSpawnId))
            return requestReturnSpawnId.Trim();

        return encounterReturnSpawnId;
    }

    private bool ResolveIsIndoor()
    {
        TimeOfDayVisualApplier visualApplier = TimeOfDayVisualApplier.ActiveInstance;
        if (visualApplier == null)
            visualApplier = FindObjectOfType<TimeOfDayVisualApplier>();

        return visualApplier != null && visualApplier.IsIndoors;
    }
}
