using System.Collections;
using Chainbound.Combat;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Plays sanctuary arrival presentation after SpawnService places the player.
/// Portal continue/wipe: freeze, portal drops in and hovers, then player
/// GlitchIn after that hold starts. Departure overlaps the catalog wake line.
/// Hub continue/wipe (bedroom): wake line only — no outdoor portal mesh.
/// </summary>
public sealed class SanctuarySpawnDirector : MonoBehaviour
{
    private const string PlayerGlitchChildName = "SanctuaryGlitchOverlay";
    private const string PortalGlitchChildName = "GlitchOverlay";
    private const string GlitchMaterialResourcePath = "Materials/Signal_Glitch";

    private const float PresentationGlitchTimeoutSeconds = 8f;
    private const float WakeLineWaitTimeoutSeconds = 12f;
    // Match the authored Chainling battle spawn-in timing.
    private const float PlayerGlitchInDuration = 0.8f;
    private const float PresentationArrivalMaxDistance = 16f;
    private const float PendingArrivalWaitSeconds = 8f;

    private static SanctuarySpawnDirector instance;
    private static string pendingSceneId;
    private static string pendingSpawnId;
    private static SanctuaryWakeKind pendingWakeKind;
    private static bool presenting;
    private static float pendingArmedAt;

    [Header("Presentation")]
    [SerializeField] private SanctuaryWakeCatalogSO wakeCatalog;
    [SerializeField] private Material glitchMaterial;
    [SerializeField] private GameObject portalPrefab;
    [SerializeField] private float portalRiseHeight = 2.4f;
    [SerializeField] private float portalRiseDuration = 0.45f;
    [SerializeField] private string playerTag = "Player";

    [Header("Diagnostics")]
    [SerializeField] private bool enableDebugLogs;

    private Coroutine presentationRoutine;
    private Coroutine wakeRoutine;
    private bool wakeLineFinished;
    private InputLockHandle inputLockHandle;
    private bool hasInputLock;
    private PlayerMovement3D frozenMovement;
    private bool appliedFreeze;
    private MeshRendererPlayerVisual hiddenPlayerVisual;
    private bool holdsPlayerVisualHidden;
    private OcclusionSilhouette.SuppressionHandle silhouetteSuppression;
    private bool hasSilhouetteSuppression;
    private GameObject runtimePortalInstance;
    private bool sceneLoaderBound;

    public static SanctuarySpawnDirector Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SanctuarySpawnDirector>();
                if (instance == null)
                {
                    GameServicesContainer services = GameServicesContainer.Instance;
                    var host = new GameObject("SanctuarySpawnDirector");
                    if (services != null)
                        host.transform.SetParent(services.transform, false);
                    instance = host.AddComponent<SanctuarySpawnDirector>();
                    if (services == null && Application.isPlaying)
                        DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    public static bool IsPresenting => presenting;

    public static bool ShouldSuppressLatchPing
    {
        get
        {
            if (presenting || HasPendingPresentation)
                return true;
            if (EncounterReturnPlacement.SuppressWorldVolumeEnter)
                return true;
            SceneLoader loader = SceneLoader.Instance;
            return loader != null && loader.IsTransitioning;
        }
    }

    public static bool HasPendingPresentation =>
        !string.IsNullOrWhiteSpace(pendingSceneId) && !string.IsNullOrWhiteSpace(pendingSpawnId);

    public static void ArmPendingPresentation(
        string sceneId,
        string spawnId,
        SanctuaryWakeKind wakeKind = SanctuaryWakeKind.Continue)
    {
        pendingSceneId = sceneId != null ? sceneId.Trim() : "";
        pendingSpawnId = spawnId != null ? spawnId.Trim() : "";
        pendingWakeKind = wakeKind;
        pendingArmedAt = Time.unscaledTime;
        _ = Instance;
        instance?.BindSceneLoader();
        if (instance != null && instance.enableDebugLogs)
        {
            Debug.Log(
                $"[SanctuarySpawnDirector] Armed presentation scene='{pendingSceneId}' spawn='{pendingSpawnId}' wake='{pendingWakeKind}'.");
        }
    }

    public static void ClearPendingPresentation()
    {
        pendingSceneId = "";
        pendingSpawnId = "";
        pendingWakeKind = SanctuaryWakeKind.Continue;
        pendingArmedAt = 0f;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null && Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        if (wakeCatalog == null)
            wakeCatalog = Resources.Load<SanctuaryWakeCatalogSO>(SanctuaryWakeCatalogSO.DefaultResourcePath);
        if (glitchMaterial == null)
            glitchMaterial = Resources.Load<Material>(GlitchMaterialResourcePath);
    }

    private void OnEnable()
    {
        BindSceneLoader();
    }

    private void OnDisable()
    {
        UnbindSceneLoader();
        AbortPresentation();
    }

    private void OnDestroy()
    {
        AbortPresentation();
        if (instance == this)
            instance = null;
    }

    private void BindSceneLoader()
    {
        if (sceneLoaderBound)
            return;

        SceneLoader loader = SceneLoader.Instance;
        if (loader == null)
            return;

        loader.BeforeReveal += HandleBeforeReveal;
        loader.TransitionCompleted += HandleTransitionCompleted;
        sceneLoaderBound = true;
    }

    private void UnbindSceneLoader()
    {
        if (!sceneLoaderBound)
            return;

        SceneLoader loader = SceneLoader.Instance;
        if (loader != null)
        {
            loader.BeforeReveal -= HandleBeforeReveal;
            loader.TransitionCompleted -= HandleTransitionCompleted;
        }

        sceneLoaderBound = false;
    }

    /// <summary>
    /// Hide the already-spawned player before the loading cover lifts so
    /// GlitchIn can materialize them instead of swapping a visible body out.
    /// </summary>
    private void HandleBeforeReveal(SceneTransitionContext context)
    {
        if (!HasPendingPresentation)
            return;
        if (EncounterReturnPlacement.SuppressWorldVolumeEnter)
            return;

        bool sceneMatch = string.Equals(
            context.SceneId,
            pendingSceneId,
            System.StringComparison.OrdinalIgnoreCase);
        bool spawnMatch = string.IsNullOrWhiteSpace(context.SpawnId) ||
                          string.Equals(context.SpawnId, pendingSpawnId, System.StringComparison.OrdinalIgnoreCase);
        if (!sceneMatch || !spawnMatch)
            return;

        SanctuaryLatchZone zone = SanctuaryLatchZone.FindArrivalZone(pendingSceneId, pendingSpawnId);
        if (zone == null || zone.Kind != SanctuaryLatchKind.Portal)
            return;

        HidePlayerUntilGlitchIn();
    }

    private void HandleTransitionCompleted(SceneTransitionResult result)
    {
        if (!result.Succeeded)
            return;
        if (EncounterReturnPlacement.SuppressWorldVolumeEnter)
        {
            ClearPendingPresentation();
            return;
        }

        string arrivalScene = result.SceneId;
        string arrivalSpawn = result.SpawnId;
        if (string.IsNullOrWhiteSpace(arrivalSpawn) &&
            HasPendingPresentation &&
            string.Equals(arrivalScene, pendingSceneId, System.StringComparison.OrdinalIgnoreCase))
        {
            arrivalSpawn = pendingSpawnId;
        }

        SanctuaryLatchZone arrivalZone = SanctuaryLatchZone.FindArrivalZone(arrivalScene, arrivalSpawn);
        if (HasPendingPresentation)
        {
            bool sceneMatch = string.Equals(arrivalScene, pendingSceneId, System.StringComparison.OrdinalIgnoreCase);
            bool spawnMatch = string.IsNullOrWhiteSpace(result.SpawnId) ||
                              string.Equals(result.SpawnId, pendingSpawnId, System.StringComparison.OrdinalIgnoreCase);
            if (sceneMatch && spawnMatch)
            {
                TryBeginPendingPresentation();
                return;
            }

            ClearPendingPresentation();
        }

        if (arrivalZone == null || arrivalZone.Kind != SanctuaryLatchKind.Portal)
            return;

        ArmPendingPresentation(arrivalZone.SceneId, string.IsNullOrWhiteSpace(arrivalSpawn) ? arrivalZone.SpawnId : arrivalSpawn);
        TryBeginPendingPresentation();
    }

    private void Update()
    {
        if (!HasPendingPresentation || presenting)
            return;
        if (EncounterReturnPlacement.SuppressWorldVolumeEnter)
            return;

        SceneLoader loader = SceneLoader.Instance;
        if (loader != null && loader.IsTransitioning)
            return;

        TryBeginPendingPresentation();
    }

    private void TryBeginPendingPresentation()
    {
        if (!HasPendingPresentation || presenting || presentationRoutine != null)
            return;
        if (EncounterReturnPlacement.SuppressWorldVolumeEnter)
            return;

        string sceneId = pendingSceneId;
        string spawnId = pendingSpawnId;
        SanctuaryWakeKind wakeKind = pendingWakeKind;

        Scene named = SceneManager.GetSceneByName(sceneId);
        bool sceneReady = named.IsValid() && named.isLoaded;
        if (!sceneReady)
            return;

        SpawnService spawnService = SpawnService.Instance;
        SpawnPoint spawnPoint = spawnService != null ? spawnService.GetSpawnPoint(spawnId) : null;
        if (spawnPoint == null)
        {
            if (pendingArmedAt > 0f && Time.unscaledTime - pendingArmedAt > PendingArrivalWaitSeconds)
                ClearPendingPresentation();
            return;
        }

        Transform playerRoot = ResolvePlayerRoot();
        if (playerRoot != null &&
            Vector3.Distance(playerRoot.position, spawnPoint.Position) > PresentationArrivalMaxDistance)
        {
            if (pendingArmedAt > 0f && Time.unscaledTime - pendingArmedAt > PendingArrivalWaitSeconds)
            {
                if (enableDebugLogs)
                {
                    Debug.Log(
                        $"[SanctuarySpawnDirector] Timed out waiting for spawn arrival '{spawnId}'; dropping presentation arm.");
                }
                ClearPendingPresentation();
            }
            return;
        }

        presenting = true;
        ClearPendingPresentation();
        presentationRoutine = StartCoroutine(PresentRoutine(sceneId, spawnId, spawnPoint, wakeKind));
    }

    private IEnumerator PresentRoutine(string sceneId, string spawnId, SpawnPoint spawnPoint, SanctuaryWakeKind wakeKind)
    {
        presenting = true;
        wakeRoutine = null;
        wakeLineFinished = false;
        try
        {
            SanctuaryLatchZone zone = SanctuaryLatchZone.FindArrivalZone(sceneId, spawnId);
            bool playPortalVisual = zone != null && zone.Kind == SanctuaryLatchKind.Portal;
            Transform playerRoot = ResolvePlayerRoot();
            GameObject portalRoot = playPortalVisual ? ResolvePortalRoot(zone, spawnPoint) : null;
            SanctuaryPortalPresentation presentation = portalRoot != null
                ? portalRoot.GetComponent<SanctuaryPortalPresentation>()
                : null;

            AcquireHold(playerRoot);

            if (playPortalVisual)
            {
                HidePlayerUntilGlitchIn();
                if (zone != null)
                    zone.ShowPortalVisual();
                else if (portalRoot != null)
                    portalRoot.SetActive(true);

                Vector3 portalLocalPos = Vector3.zero;
                Quaternion portalLocalRot = Quaternion.identity;
                bool restorePortalPose = portalRoot != null;
                if (restorePortalPose)
                {
                    portalLocalPos = portalRoot.transform.localPosition;
                    portalLocalRot = portalRoot.transform.localRotation;
                    if (presentation != null)
                        presentation.PlaceBehindPlayer(playerRoot);
                    else
                        SanctuaryPortalPresentation.AlignTransformBehindPlayer(
                            portalRoot.transform,
                            playerRoot,
                            1.75f);
                }

                if (presentation != null && presentation.isActiveAndEnabled)
                    yield return RunBounded(presentation.PlayArrival(), PresentationGlitchTimeoutSeconds);
                else
                    yield return PlayFallbackPortalDrop(portalRoot);

                if (presentation != null && !presentation.isActiveAndEnabled)
                    yield break;

                SignalGlitchEffect playerGlitch = EnsurePlayerGlitch(playerRoot);
                bool playerGlitchDone = playerGlitch == null;
                if (playerGlitch != null)
                {
                    playerGlitch.PlayGlitchIn(
                        PlayerGlitchInDuration,
                        unscaledTime: true,
                        onComplete: () =>
                        {
                            ReleasePlayerVisualHide();
                            playerGlitchDone = true;
                        });
                }
                else
                {
                    ReleasePlayerVisualHide();
                }

                float holdSeconds = presentation != null ? presentation.ArrivalHoldDuration : 0f;
                float elapsed = 0f;
                while ((!playerGlitchDone || elapsed < holdSeconds) && elapsed < PresentationGlitchTimeoutSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (presentation != null)
                {
                    if (!presentation.isActiveAndEnabled)
                        yield break;
                    StartOverlappingWake(wakeKind, sceneId, spawnId);
                    yield return RunBounded(presentation.PlayDeparture(), PresentationGlitchTimeoutSeconds);
                }
                else
                {
                    StartOverlappingWake(wakeKind, sceneId, spawnId);
                    SignalGlitchEffect portalGlitch = EnsurePortalGlitch(portalRoot);
                    bool portalGlitchDone = portalGlitch == null;
                    Tween riseTween = null;
                    if (portalRoot != null)
                    {
                        Vector3 start = portalRoot.transform.position;
                        riseTween = portalRoot.transform
                            .DOMove(start + Vector3.up * portalRiseHeight, portalRiseDuration)
                            .SetEase(Ease.OutCubic)
                            .SetUpdate(true);
                    }

                    if (portalGlitch != null)
                        portalGlitch.PlayGlitchOut(-1f, unscaledTime: true, onComplete: () => portalGlitchDone = true);

                    yield return WaitUntilOrTimeout(() => portalGlitchDone, PresentationGlitchTimeoutSeconds);

                    if (riseTween != null && riseTween.IsActive() && !riseTween.IsComplete())
                        yield return WaitUntilOrTimeout(
                            () => riseTween == null || !riseTween.IsActive() || riseTween.IsComplete(),
                            PresentationGlitchTimeoutSeconds);
                }

                if (restorePortalPose && portalRoot != null)
                {
                    portalRoot.transform.localPosition = portalLocalPos;
                    portalRoot.transform.localRotation = portalLocalRot;
                }

                if (zone != null)
                    zone.HidePortalVisual();
                CleanupRuntimePortal();
            }

            yield return WaitForWakeLine(wakeKind, sceneId, spawnId);
        }
        finally
        {
            StopOverlappingWake(stopDialogue: true);
            ReleaseHoldEffects();
            presenting = false;
            presentationRoutine = null;
            SanctuaryLatchZone arrivalZone = SanctuaryLatchZone.FindArrivalZone(sceneId, spawnId);
            arrivalZone?.CommitLatch(playPing: false);
        }
    }

    private void StartOverlappingWake(SanctuaryWakeKind wakeKind, string sceneId, string spawnId)
    {
        wakeLineFinished = false;
        wakeRoutine = StartCoroutine(PlayOverlappingWake(wakeKind, sceneId, spawnId));
    }

    private IEnumerator PlayOverlappingWake(SanctuaryWakeKind wakeKind, string sceneId, string spawnId)
    {
        try
        {
            yield return PlayWakeLine(wakeKind, sceneId, spawnId);
        }
        finally
        {
            wakeLineFinished = true;
            wakeRoutine = null;
        }
    }

    /// <summary>
    /// Waits for an overlapping wake, or plays it inline for hub arrivals.
    /// Never yields a finished <see cref="Coroutine"/> handle — Unity can hang
    /// forever on that, which leaves the SanctuarySpawn input lock held.
    /// </summary>
    private IEnumerator WaitForWakeLine(SanctuaryWakeKind wakeKind, string sceneId, string spawnId)
    {
        if (wakeRoutine == null && !wakeLineFinished)
        {
            yield return PlayWakeLine(wakeKind, sceneId, spawnId);
            yield break;
        }

        float elapsed = 0f;
        while (!wakeLineFinished && elapsed < WakeLineWaitTimeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (wakeLineFinished)
            yield break;

        Debug.LogWarning(
            "[SanctuarySpawnDirector] Wake line timed out; releasing sanctuary hold.");
        StopOverlappingWake(stopDialogue: true);
    }

    private IEnumerator PlayWakeLine(SanctuaryWakeKind wakeKind, string sceneId, string spawnId)
    {
        SanctuaryWakeCatalogSO catalog = wakeCatalog != null
            ? wakeCatalog
            : Resources.Load<SanctuaryWakeCatalogSO>(SanctuaryWakeCatalogSO.DefaultResourcePath);
        SanctuaryLatchZone zone = SanctuaryLatchZone.FindArrivalZone(sceneId, spawnId);
        bool useDogFallback = zone != null && zone.UseDogWakeFallback;
        DialogueSequenceSO wakeLine = null;
        if (catalog != null)
        {
            wakeLine = useDogFallback
                ? catalog.PickDogFallback()
                : catalog.PickNext(wakeKind);
            if (wakeLine == null && useDogFallback)
                wakeLine = catalog.PickNext(wakeKind);
        }
        if (wakeLine == null)
            yield break;

        const float dialogueWaitSeconds = 2f;
        float elapsed = 0f;
        DialogueService dialogue = DialogueService.Instance;
        while (dialogue == null && elapsed < dialogueWaitSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
            dialogue = DialogueService.Instance;
        }

        if (dialogue == null)
        {
            Debug.LogWarning("[SanctuarySpawnDirector] DialogueService was not ready; skipped sanctuary wake line.");
            yield break;
        }

        // Sanctuary already holds InputLockChannel.All. Nested Dialogue lock is
        // redundant, and yielding PlayDialogue's Coroutine can hang if that
        // playback finished before the yield (Unity waits forever on a completed
        // handle). Wait on IsPlaying with unscaled timeout instead.
        dialogue.PlayDialogue(wakeLine, lockInputOverride: false);
        float playbackElapsed = 0f;
        while (dialogue.IsPlaying && playbackElapsed < WakeLineWaitTimeoutSeconds)
        {
            playbackElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (dialogue.IsPlaying)
        {
            Debug.LogWarning(
                "[SanctuarySpawnDirector] Wake dialogue did not finish; stopping it so input can unlock.");
            dialogue.StopDialogue();
        }
    }

    private static IEnumerator RunBounded(IEnumerator routine, float timeoutSeconds)
    {
        if (routine == null)
            yield break;

        float elapsed = 0f;
        timeoutSeconds = Mathf.Max(0.05f, timeoutSeconds);
        while (elapsed < timeoutSeconds)
        {
            bool moved;
            try
            {
                moved = routine.MoveNext();
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning($"[SanctuarySpawnDirector] Presentation enumerator failed: {exception.Message}");
                yield break;
            }

            if (!moved)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return routine.Current;
        }

        Debug.LogWarning("[SanctuarySpawnDirector] Presentation phase timed out; releasing sanctuary hold.");
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator PlayFallbackPortalDrop(GameObject portalRoot)
    {
        if (portalRoot == null)
            yield break;

        Transform portal = portalRoot.transform;
        Vector3 rest = portal.position;
        float height = Mathf.Max(0.01f, portalRiseHeight);
        float duration = Mathf.Max(0.05f, portalRiseDuration);
        float overshoot = 0.22f;
        portal.position = rest + Vector3.up * height;

        Tween drop = DOTween.Sequence()
            .Append(portal.DOMove(rest - Vector3.up * overshoot, duration * 0.82f).SetEase(Ease.InCubic))
            .Append(portal.DOMove(rest, duration * 0.18f).SetEase(Ease.OutQuad))
            .SetUpdate(true);
        yield return WaitUntilOrTimeout(
            () => drop == null || !drop.IsActive() || drop.IsComplete(),
            duration + 2f);
        portal.position = rest;
    }

    private GameObject ResolvePortalRoot(SanctuaryLatchZone zone, SpawnPoint spawnPoint)
    {
        if (zone != null && zone.PortalVisual != null)
            return zone.PortalVisual;

        if (portalPrefab == null || spawnPoint == null)
            return null;

        runtimePortalInstance = Instantiate(portalPrefab, spawnPoint.Position, spawnPoint.Rotation);
        runtimePortalInstance.SetActive(true);
        return runtimePortalInstance;
    }

    private SignalGlitchEffect EnsurePlayerGlitch(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;

        Transform visual = FindPlayerVisual(playerRoot);
        if (visual == null)
            visual = playerRoot;

        MeshRenderer source = visual.GetComponent<MeshRenderer>();
        if (source == null)
            source = visual.GetComponentInChildren<MeshRenderer>(true);
        if (source == null)
            return null;

        return EnsureOverlay(visual, PlayerGlitchChildName, source);
    }

    private SignalGlitchEffect EnsurePortalGlitch(GameObject portalRoot)
    {
        if (portalRoot == null)
            return null;

        Transform overlayParent = portalRoot.transform;
        MeshRenderer source = null;
        Transform namedPortal = FindNamedChild(portalRoot.transform, "PortalMesh");
        if (namedPortal != null)
            source = namedPortal.GetComponent<MeshRenderer>();

        Transform existingOverlay = FindNamedChild(portalRoot.transform, PortalGlitchChildName);
        if (source == null)
        {
            MeshRenderer[] renderers = portalRoot.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                MeshRenderer candidate = renderers[i];
                if (candidate == null)
                    continue;
                if (existingOverlay != null && candidate.transform == existingOverlay)
                    continue;
                if (candidate.gameObject.name == PortalGlitchChildName)
                    continue;
                if (candidate.gameObject.name == "Structure" || candidate.gameObject.name == "StructureGlitchOverlay")
                    continue;
                source = candidate;
                overlayParent = candidate.transform.parent != null ? candidate.transform.parent : portalRoot.transform;
                break;
            }
        }
        else
        {
            overlayParent = source.transform.parent != null ? source.transform.parent : portalRoot.transform;
        }

        if (source == null)
            return null;

        return EnsureOverlay(overlayParent, PortalGlitchChildName, source);
    }

    private SignalGlitchEffect EnsureOverlay(Transform parent, string childName, MeshRenderer source)
    {
        Transform overlay = parent.Find(childName);
        GameObject overlayGo = overlay != null ? overlay.gameObject : null;
        if (overlayGo == null)
        {
            overlayGo = new GameObject(childName);
            overlayGo.transform.SetParent(parent, false);
            overlayGo.AddComponent<MeshFilter>();
            MeshRenderer overlayRenderer = overlayGo.AddComponent<MeshRenderer>();
            overlayRenderer.enabled = false;
            overlayRenderer.sharedMaterial = ResolveGlitchMaterial();
        }

        SignalGlitchEffect effect = overlayGo.GetComponent<SignalGlitchEffect>();
        if (effect == null)
            effect = overlayGo.AddComponent<SignalGlitchEffect>();

        effect.Configure(source, ResolveGlitchMaterial());
        return effect;
    }

    private static Transform FindNamedChild(Transform root, string childName)
    {
        if (root == null)
            return null;
        Transform direct = root.Find(childName);
        if (direct != null)
            return direct;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
                return children[i];
        }

        return null;
    }

    private Material ResolveGlitchMaterial()
    {
        if (glitchMaterial != null)
            return glitchMaterial;

        glitchMaterial = Resources.Load<Material>(GlitchMaterialResourcePath);
        if (glitchMaterial != null)
            return glitchMaterial;

        Shader shader = Shader.Find("SignalJam/Sprites/SignalGlitch");
        if (shader != null)
            glitchMaterial = new Material(shader);

        return glitchMaterial;
    }

    private void AcquireHold(Transform playerRoot)
    {
        PlayerMovement3D movement = ResolveMovement(playerRoot);
        InputLockService lockService = InputLockService.Instance;
        if (lockService != null)
        {
            inputLockHandle = lockService.AcquireLock("SanctuarySpawn", InputLockChannel.All, this);
            hasInputLock = inputLockHandle.IsValid;
        }

        if (movement != null)
        {
            frozenMovement = movement;
            movement.SetFrozen(true, preservePhysics: false);
            appliedFreeze = true;
        }

        SuppressSilhouette(playerRoot);
    }

    private void AbortPresentation()
    {
        if (presentationRoutine != null)
        {
            StopCoroutine(presentationRoutine);
            presentationRoutine = null;
        }

        StopOverlappingWake(stopDialogue: true);
        ReleaseHoldEffects();
        presenting = false;
    }

    private void StopOverlappingWake(bool stopDialogue)
    {
        if (wakeRoutine != null)
        {
            StopCoroutine(wakeRoutine);
            wakeRoutine = null;
        }

        wakeLineFinished = true;

        if (!stopDialogue)
            return;

        DialogueService dialogue = FindFirstObjectByType<DialogueService>();
        if (dialogue != null && dialogue.IsPlaying)
            dialogue.StopDialogue();
    }

    private void HidePlayerUntilGlitchIn()
    {
        Transform playerRoot = ResolvePlayerRoot();
        MeshRendererPlayerVisual playerVisual = playerRoot != null
            ? playerRoot.GetComponentInChildren<MeshRendererPlayerVisual>(true)
            : null;
        if (playerVisual != null && !holdsPlayerVisualHidden)
        {
            hiddenPlayerVisual = playerVisual;
            hiddenPlayerVisual.SetTemporarilyHidden(true);
            holdsPlayerVisualHidden = true;
        }

        SuppressSilhouette(playerRoot);
        SignalGlitchEffect playerGlitch = EnsurePlayerGlitch(playerRoot);
        playerGlitch?.HideSourceUntilGlitchIn();
    }

    private void ReleasePlayerVisualHide()
    {
        if (!holdsPlayerVisualHidden)
            return;

        if (hiddenPlayerVisual != null)
            hiddenPlayerVisual.SetTemporarilyHidden(false);
        hiddenPlayerVisual = null;
        holdsPlayerVisualHidden = false;
    }

    private void ReleaseHoldEffects()
    {
        ReleasePlayerVisualHide();
        StopPlayerGlitchIfPresent(ResolvePlayerRoot());
        ReleaseSilhouetteSuppression();

        if (appliedFreeze && frozenMovement != null)
        {
            frozenMovement.SetFrozen(false, preservePhysics: false);
            appliedFreeze = false;
        }

        frozenMovement = null;

        if (hasInputLock)
        {
            inputLockHandle.Release();
            hasInputLock = false;
        }

        CleanupRuntimePortal();
    }

    private void CleanupRuntimePortal()
    {
        if (runtimePortalInstance == null)
            return;

        Destroy(runtimePortalInstance);
        runtimePortalInstance = null;
    }

    private Transform ResolvePlayerRoot()
    {
        GameObject tagged = GameObject.FindGameObjectWithTag(playerTag);
        if (tagged != null)
            return tagged.transform;
        PlayerMovement3D movement = FindFirstObjectByType<PlayerMovement3D>();
        return movement != null ? movement.transform : null;
    }

    private static Transform FindPlayerVisual(Transform playerRoot)
    {
        MeshRendererPlayerVisual visual = playerRoot.GetComponentInChildren<MeshRendererPlayerVisual>(true);
        if (visual != null)
            return visual.transform;

        Transform named = playerRoot.Find("Visual");
        return named != null ? named : playerRoot;
    }

    private static PlayerMovement3D ResolveMovement(Transform playerRoot)
    {
        if (playerRoot == null)
            return FindFirstObjectByType<PlayerMovement3D>();

        PlayerMovement3D movement = playerRoot.GetComponent<PlayerMovement3D>();
        if (movement == null)
            movement = playerRoot.GetComponentInParent<PlayerMovement3D>();
        if (movement == null)
            movement = playerRoot.GetComponentInChildren<PlayerMovement3D>(true);
        return movement;
    }

    private void SuppressSilhouette(Transform playerRoot)
    {
        if (hasSilhouetteSuppression)
            return;

        OcclusionSilhouette silhouette = ResolveSilhouette(playerRoot);
        if (silhouette == null)
            return;

        silhouetteSuppression = silhouette.AcquireSuppression("sanctuary_presentation");
        hasSilhouetteSuppression = silhouetteSuppression.IsValid;
    }

    private void ReleaseSilhouetteSuppression()
    {
        if (!hasSilhouetteSuppression)
            return;

        silhouetteSuppression.Dispose();
        hasSilhouetteSuppression = false;
    }

    private void StopPlayerGlitchIfPresent(Transform playerRoot)
    {
        if (playerRoot == null)
            return;

        Transform visual = FindPlayerVisual(playerRoot);
        Transform searchRoot = visual != null ? visual : playerRoot;
        Transform overlay = searchRoot.Find(PlayerGlitchChildName);
        if (overlay == null)
            overlay = FindNamedChild(playerRoot, PlayerGlitchChildName);
        if (overlay == null)
            return;

        SignalGlitchEffect effect = overlay.GetComponent<SignalGlitchEffect>();
        effect?.StopGlitch();
    }

    private static OcclusionSilhouette ResolveSilhouette(Transform playerRoot)
    {
        if (playerRoot == null)
            return FindFirstObjectByType<OcclusionSilhouette>();

        OcclusionSilhouette silhouette = playerRoot.GetComponent<OcclusionSilhouette>();
        if (silhouette == null)
            silhouette = playerRoot.GetComponentInParent<OcclusionSilhouette>();
        if (silhouette == null)
            silhouette = playerRoot.GetComponentInChildren<OcclusionSilhouette>(true);
        return silhouette;
    }
}
