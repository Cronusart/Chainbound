using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent, transactional scene-transition service. The source is covered
/// before scene IO begins and remains covered through activation, readiness,
/// spawn, camera, and lighting finalization.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    private sealed class WarmupRunState
    {
        public Exception Exception;
        public bool TimedOut;
    }

    private static SceneLoader instance;

    public static SceneLoader Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SceneLoader>();
                if (instance == null)
                    Debug.LogWarning("[SceneLoader] Instance not found. Start through Bootstrap.");
            }
            return instance;
        }
    }

    [Header("Loading System")]
    [SerializeField] private LoadingScreenUI loadingScreenUI;
    [SerializeField] private SceneLoadingConfigSO loadingConfig;

    [Header("Transition Hold")]
    [SerializeField] private bool lockInputUntilFadeComplete = true;
    [SerializeField] private bool freezePlayerUntilFadeComplete = true;
    [SerializeField, Min(0f)] private float fallbackFadeOutWaitSeconds = 0.7f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private bool isTransitioning;
    private string activeSceneId = string.Empty;
    private float displayedProgress;
    private bool transitionDegraded;
    private InputLockHandle sceneTransitionLockHandle;
    private PlayerMovement3D frozenPlayer;
    private bool frozenPlayerHeld;
    private ThreadPriority previousLoadingPriority;
    private bool loadingPriorityChanged;
    private Coroutine activeTransitionCoroutine;
    private ISceneOperationBackend operationBackend;
    private SceneTransitionStatus currentStatus = new SceneTransitionStatus(
        SceneTransitionPhase.Idle, 0f, string.Empty, string.Empty, false);

    public event Action<SceneTransitionContext> BeforeReveal;
    public event Action<SceneTransitionStatus> TransitionChanged;
    public event Action<SceneTransitionResult> TransitionCompleted;
    public event Action<SceneTransitionResult> TransitionFailed;

    public bool IsTransitioning => isTransitioning;
    public SceneTransitionStatus CurrentStatus => currentStatus;
    public SceneLoadingConfigSO Config => SceneLoadingConfigRuntime.Resolve(loadingConfig);
    private ISceneOperationBackend OperationBackend => operationBackend ??= new UnitySceneOperationBackend();

    // The additive coordinator shares this deliberately narrow IO seam so tests
    // and a future Addressables backend can exercise both paths consistently.
    internal ISceneOperationBackend OperationBackendForStreaming => OperationBackend;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[SceneLoader] Duplicate instance found. Destroying {gameObject.name}.");
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (loadingScreenUI == null)
            loadingScreenUI = GetComponentInChildren<LoadingScreenUI>(true);
        if (loadingScreenUI == null)
            loadingScreenUI = FindObjectOfType<LoadingScreenUI>();

        SceneLoadingConfigRuntime.Register(loadingConfig);
        ApplyPresentationConfig(Config);
    }

    private void OnDisable()
    {
        if (instance == this && isTransitioning)
            EmergencyCleanup();
    }

    private void OnDestroy()
    {
        SceneLoadingConfigRuntime.Unregister(loadingConfig);
        if (instance != this)
            return;

        EmergencyCleanup();
        instance = null;
    }

    public bool RequestTransition(SceneTransitionRequest request)
    {
        if (request == null)
            return RejectRequest(null, SceneTransitionOutcome.Invalid, "Scene transition request was null.");
        if (isTransitioning)
            return RejectRequest(request, SceneTransitionOutcome.Busy, "Another scene transition is already in progress.");
        if (string.IsNullOrWhiteSpace(request.SceneId))
            return RejectRequest(request, SceneTransitionOutcome.Invalid, "Destination scene id is empty.");
        if (!OperationBackend.CanLoad(request.SceneId))
            return RejectRequest(request, SceneTransitionOutcome.Invalid,
                $"Scene '{request.SceneId}' is not available in Build Settings.");

        SceneLoadingConfigSO config = Config;
        TransitionStyle style = config.ResolveStyle(request.SceneId, request.StyleOverride);
        isTransitioning = true;
        activeSceneId = request.SceneId;
        displayedProgress = 0f;
        transitionDegraded = false;
        activeTransitionCoroutine = StartCoroutine(LoadSceneCoroutine(request, style, config));
        return true;
    }

    public void LoadScene(
        string sceneName,
        TransitionStyle style,
        string spawnId = "",
        string preferredOverworldChunkSceneId = "",
        string[] preferredOverworldChunkSceneIds = null)
    {
        RequestTransition(new SceneTransitionRequest(
            sceneName,
            spawnId,
            style,
            sourceId: "Legacy.LoadScene",
            preferredOverworldChunkSceneId: preferredOverworldChunkSceneId,
            preferredOverworldChunkSceneIds: preferredOverworldChunkSceneIds));
    }

    public void LoadSceneAsync(
        string sceneName,
        TransitionStyle style,
        string spawnId = "",
        Action onComplete = null,
        Action onBeforeReveal = null)
    {
        RequestTransition(new SceneTransitionRequest(
            sceneName,
            spawnId,
            style,
            sourceId: "Legacy.LoadSceneAsync",
            beforeReveal: _ => onBeforeReveal?.Invoke(),
            completed: result =>
            {
                if (result.Succeeded)
                    onComplete?.Invoke();
            }));
    }

    public void SetLoadingScreenUI(LoadingScreenUI ui)
    {
        loadingScreenUI = ui;
        ApplyPresentationConfig(Config);
    }

    public void SetOperationBackend(ISceneOperationBackend backend)
    {
        if (isTransitioning)
            throw new InvalidOperationException("Cannot replace the scene operation backend during a transition.");
        operationBackend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    private IEnumerator LoadSceneCoroutine(
        SceneTransitionRequest request,
        TransitionStyle style,
        SceneLoadingConfigSO config)
    {
        bool sceneActivated = false;
        bool succeeded = false;
        string failureMessage = string.Empty;
        Scene destinationScene = default;

        BeginSceneTransitionHold();
        previousLoadingPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = config.GetBackgroundPriority(style);
        loadingPriorityChanged = true;

        try
        {
            SetStatus(SceneTransitionPhase.Covering, 0f, "Covering scene");
            ApplyPresentationConfig(config);
            if (loadingScreenUI != null)
                yield return loadingScreenUI.ShowAndWaitForCover(style);
            else
                yield return null;

            // ForceUpdateCanvases prepares geometry; EndOfFrame guarantees a rendered cover.
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            NotifySceneSoundtrack(request.SceneId, SoundtrackTriggerMoment.TransitionStart);
            SetStatus(SceneTransitionPhase.Loading, 0.01f, "Loading scene");

            if (!OperationBackend.TryBeginLoad(request.SceneId, out ISceneLoadOperation loadOperation, out failureMessage))
                yield break;

            loadOperation.AllowSceneActivation = false;
            float loadingStartedAt = Time.unscaledTime;
            float minimumDisplay = config.GetMinimumDisplaySeconds(style);
            bool stillLoadingMessageShown = false;

            while (loadOperation.Progress < 0.9f || Time.unscaledTime - loadingStartedAt < minimumDisplay)
            {
                float operationProgress = Mathf.Clamp01(loadOperation.Progress / 0.9f);
                SetStatus(SceneTransitionPhase.Loading, operationProgress * 0.8f, "Loading scene");

                if (!stillLoadingMessageShown &&
                    Time.unscaledTime - loadingStartedAt >= config.StillLoadingMessageSeconds)
                {
                    stillLoadingMessageShown = true;
                    loadingScreenUI?.SetStatus("Still loading…");
                }

                yield return null;
            }

            loadingScreenUI?.ClearStatus();
            SetStatus(SceneTransitionPhase.Activating, 0.8f, "Activating scene");
            OverworldStreamingService streaming = OverworldStreamingService.Instance;
            streaming?.ReleasePersistentCameraForDestination(request.SceneId);
            loadOperation.AllowSceneActivation = true;
            while (!loadOperation.IsDone)
            {
                SetStatus(SceneTransitionPhase.Activating, 0.85f, "Activating scene");
                yield return null;
            }

            sceneActivated = true;
            destinationScene = SceneManager.GetSceneByName(request.SceneId);
            if (!destinationScene.IsValid() || !destinationScene.isLoaded)
            {
                failureMessage = $"Scene '{request.SceneId}' activated but could not be resolved.";
                yield break;
            }

            yield return null;
            SceneTransitionContext transitionContext = new SceneTransitionContext(
                request.SceneId,
                request.SpawnId,
                style,
                destinationScene);

            OverworldStreamingService streamingService = OverworldStreamingService.Instance;
            if (streamingService != null &&
                config.OverworldStreamingConfig != null &&
                config.OverworldStreamingConfig.ContainsScene(request.SceneId))
            {
                // Persistent overworld camera and link chunks must exist before
                // readiness checks that require a gameplay camera and player.
                yield return streamingService.EnsureSetForEntry(
                    request.SceneId,
                    request.SpawnId,
                    request.PreferredOverworldChunkSceneIds);
            }

            yield return EnsurePlayerForDestination(request, destinationScene);

            SetStatus(SceneTransitionPhase.Warming, 0.9f, "Preparing scene");
            List<ISceneWarmupTask> warmupTasks = FindSceneWarmupTasks(destinationScene);
            if (warmupTasks.Count > 0)
            {
                bool warmupSucceeded = true;
                yield return RunWarmupTasks(
                    warmupTasks,
                    transitionContext,
                    config,
                    result =>
                    {
                        warmupSucceeded = result.Succeeded;
                        if (!result.Succeeded)
                            failureMessage = result.Message;
                    });
                if (!warmupSucceeded)
                    yield break;
            }

            SetStatus(SceneTransitionPhase.Finalizing, 0.97f, "Finalizing scene");
            yield return FinalizeDestination(request, destinationScene);
            NotifySceneSoundtrack(request.SceneId, SoundtrackTriggerMoment.BeforeReveal);

            // Give destination Awake/Start subscribers a deterministic chance to attach.
            yield return null;
            InvokeBeforeReveal(transitionContext, request);

            // Keep the cover rendered for one fully prepared destination frame.
            // Coroutines resumed from `yield null` run before LateUpdate, so hiding
            // here used to expose the authored camera rig before follow settled.
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();

            SetStatus(SceneTransitionPhase.Revealing, 1f, "Ready");
            if (loadingScreenUI != null)
            {
                loadingScreenUI.SetProgress(1f);
                loadingScreenUI.Hide();
            }

            float fadeWait = loadingScreenUI != null
                ? loadingScreenUI.TotalFadeOutDurationSeconds
                : fallbackFadeOutWaitSeconds;
            if (fadeWait > 0f)
                yield return new WaitForSecondsRealtime(fadeWait);

            succeeded = true;
        }
        finally
        {
            RestoreLoadingPriority();
            EndSceneTransitionHold();
            isTransitioning = false;
            activeTransitionCoroutine = null;

            if (succeeded)
            {
                SceneTransitionOutcome outcome = transitionDegraded
                    ? SceneTransitionOutcome.SucceededDegraded
                    : SceneTransitionOutcome.Succeeded;
                SceneTransitionResult result = new SceneTransitionResult(
                    outcome,
                    request.SceneId,
                    transitionDegraded ? "Scene loaded with non-critical readiness warnings." : string.Empty,
                    sceneActivated: true,
                    spawnId: request.SpawnId);
                SetStatus(SceneTransitionPhase.Completed, 1f, result.Message);
                SafeInvoke(() => TransitionCompleted?.Invoke(result), "TransitionCompleted");
                SafeInvoke(() => request.Completed?.Invoke(result), "request completion");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(failureMessage))
                    failureMessage = $"Scene transition to '{request.SceneId}' failed unexpectedly.";
                SceneTransitionResult result = new SceneTransitionResult(
                    SceneTransitionOutcome.Failed,
                    request.SceneId,
                    failureMessage,
                    sceneActivated,
                    spawnId: request.SpawnId);
                SetStatus(SceneTransitionPhase.Failed, displayedProgress, failureMessage);
                SafeInvoke(() => TransitionFailed?.Invoke(result), "TransitionFailed");
                SafeInvoke(() => request.Completed?.Invoke(result), "request failure completion");
                ShowRecovery(request, result, config);
            }
        }
    }

    private IEnumerator RunWarmupTasks(
        List<ISceneWarmupTask> tasks,
        SceneTransitionContext transition,
        SceneLoadingConfigSO config,
        Action<SceneTransitionResult> completed)
    {
        float totalWeight = 0f;
        for (int i = 0; i < tasks.Count; i++)
            totalWeight += Mathf.Max(0.01f, tasks[i].Weight);

        float completedWeight = 0f;
        for (int i = 0; i < tasks.Count; i++)
        {
            ISceneWarmupTask task = tasks[i];
            float taskWeight = Mathf.Max(0.01f, task.Weight);
            float baseWeight = completedWeight;
            SceneWarmupContext context = new SceneWarmupContext(
                transition,
                config,
                (progress, message) =>
                {
                    float weighted = (baseWeight + taskWeight * progress) / totalWeight;
                    SetStatus(
                        SceneTransitionPhase.Warming,
                        0.9f + weighted * 0.07f,
                        string.IsNullOrEmpty(message) ? $"Preparing {task.WarmupId}" : message);
                });

            IEnumerator routine = null;
            Exception creationException = null;
            try
            {
                routine = task.Warmup(context);
            }
            catch (Exception exception)
            {
                creationException = exception;
            }

            WarmupRunState runState = new WarmupRunState { Exception = creationException };
            float timeout = task.TimeoutSeconds > 0f
                ? task.TimeoutSeconds
                : config.WarmupTaskTimeoutSeconds;
            if (routine != null && runState.Exception == null)
                yield return RunRoutineSafely(routine, timeout, runState);

            string problem = context.Failed
                ? context.FailureMessage
                : runState.TimedOut
                    ? $"Warmup '{task.WarmupId}' timed out after {timeout:0.#} seconds."
                    : runState.Exception != null
                        ? $"Warmup '{task.WarmupId}' threw: {runState.Exception.Message}"
                        : string.Empty;

            if (!string.IsNullOrEmpty(problem))
            {
                if (task.IsCritical)
                {
                    completed?.Invoke(new SceneTransitionResult(
                        SceneTransitionOutcome.Failed,
                        transition.SceneId,
                        problem,
                        sceneActivated: true));
                    yield break;
                }

                transitionDegraded = true;
                Debug.LogWarning($"[SceneLoader] Non-critical {problem}");
            }

            if (context.HasWarnings)
            {
                transitionDegraded = true;
                for (int warningIndex = 0; warningIndex < context.Warnings.Count; warningIndex++)
                    Debug.LogWarning($"[SceneLoader] {task.WarmupId}: {context.Warnings[warningIndex]}");
            }

            completedWeight += taskWeight;
            SetStatus(
                SceneTransitionPhase.Warming,
                0.9f + completedWeight / totalWeight * 0.07f,
                $"Prepared {task.WarmupId}");
        }

        completed?.Invoke(new SceneTransitionResult(
            transitionDegraded ? SceneTransitionOutcome.SucceededDegraded : SceneTransitionOutcome.Succeeded,
            transition.SceneId,
            sceneActivated: true));
    }

    private static IEnumerator RunRoutineSafely(
        IEnumerator root,
        float timeoutSeconds,
        WarmupRunState state)
    {
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(root);
        float startedAt = Time.unscaledTime;

        while (stack.Count > 0)
        {
            if (Time.unscaledTime - startedAt >= timeoutSeconds)
            {
                state.TimedOut = true;
                break;
            }

            IEnumerator currentRoutine = stack.Peek();
            bool moved;
            object yielded = null;
            try
            {
                moved = currentRoutine.MoveNext();
                if (moved)
                    yielded = currentRoutine.Current;
            }
            catch (Exception exception)
            {
                state.Exception = exception;
                break;
            }

            if (!moved)
            {
                (currentRoutine as IDisposable)?.Dispose();
                stack.Pop();
                continue;
            }

            if (yielded is IEnumerator nested)
            {
                stack.Push(nested);
                continue;
            }

            yield return yielded;
        }

        while (stack.Count > 0)
            (stack.Pop() as IDisposable)?.Dispose();
    }

    private IEnumerator FinalizeDestination(SceneTransitionRequest request, Scene destinationScene)
    {
        SpawnService spawnService = SpawnService.Instance;
        bool allowOverworldPlayerPresentation = spawnService == null || !spawnService.IsCombatScene(destinationScene);
        CameraZoneTrigger cameraZoneAtSpawn = null;
        if (allowOverworldPlayerPresentation && !string.IsNullOrEmpty(request.SpawnId))
        {
            if (spawnService != null)
            {
                yield return spawnService.SpawnPlayerAtWhenReady(request.SpawnId, maxWaitFrames: 10);
                GameObject player = spawnService.GetResolvedPlayerRoot();
                if (player != null)
                {
                    yield return CameraZoneOccupancy.WaitThenResyncOccupancy(
                        () =>
                        {
                            GameObject spawned = spawnService.GetResolvedPlayerRoot();
                            return spawned != null ? spawned.transform.position : player.transform.position;
                        });
                    player = spawnService.GetResolvedPlayerRoot() ?? player;
                    cameraZoneAtSpawn = spawnService.GetCameraZoneAtPosition(player.transform.position);
                    if (cameraZoneAtSpawn == null && enableDebugLogs)
                    {
                        Debug.LogWarning(
                            $"[SceneLoader] No camera zone contained spawn '{request.SpawnId}' at {player.transform.position}.");
                    }
                }
            }
        }

        if (cameraZoneAtSpawn != null && cameraZoneAtSpawn.ZoneBehavior != null)
        {
            GameObject spawnedPlayer = spawnService.GetResolvedPlayerRoot();
            if (spawnedPlayer != null)
                cameraZoneAtSpawn.ForceActivateAtPosition(spawnedPlayer.transform.position, reassertPresentation: true);
            else
                cameraZoneAtSpawn.ForceActivateIfPlayerInside();

            if (!cameraZoneAtSpawn.ZoneBehavior.IsActive)
                cameraZoneAtSpawn.ZoneBehavior.Activate();

            yield return WaitForCameraZoneTransition(cameraZoneAtSpawn);
        }

        if (allowOverworldPlayerPresentation && !string.IsNullOrEmpty(request.SpawnId))
            spawnService?.ApplySpawnFacingDirection(request.SpawnId);

        ConfigureSceneLighting(request.SpawnId, destinationScene);
        GameObject presentationPlayer = allowOverworldPlayerPresentation
            ? spawnService?.GetResolvedPlayerRoot()
            : null;
        FinalizeGameplayPresentation(destinationScene, presentationPlayer);
    }

    private IEnumerator EnsurePlayerForDestination(SceneTransitionRequest request, Scene destinationScene)
    {
        SpawnService spawnService = SpawnService.Instance;
        if (spawnService == null || spawnService.IsCombatScene(destinationScene))
            yield break;

        spawnService.RefreshCombatSceneRestrictions(destinationScene);
        if (string.IsNullOrEmpty(request.SpawnId))
            yield break;

        yield return spawnService.SpawnPlayerAtWhenReady(request.SpawnId, maxWaitFrames: 10);
    }

    private static void FinalizeGameplayPresentation(Scene destinationScene, GameObject playerRoot)
    {
        Camera sceneCamera = SceneCameraResolver.ResolveGameplayCamera();
        if (sceneCamera == null)
            sceneCamera = SceneCameraResolver.Resolve(destinationScene);
        TopDownCameraController3D cameraController = SceneCameraResolver.ResolveGameplayCameraController();
        if (cameraController == null && sceneCamera != null)
            cameraController = SceneCameraResolver.GetController(sceneCamera);

        if (cameraController == null && !SceneCameraResolver.HasLivePersistentCamera)
        {
            TopDownCameraController3D[] controllers = FindObjectsByType<TopDownCameraController3D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].gameObject.scene == destinationScene)
                {
                    cameraController = controllers[i];
                    break;
                }
            }
        }

        if (cameraController != null && playerRoot != null)
        {
            SpawnService.Instance?.SynchronizePlayerToCameraYaw(cameraController);
            cameraController.SetTarget(playerRoot.transform);
            cameraController.SetPauseFollowPosition(false);
            cameraController.SnapToTarget();
        }

        if (sceneCamera != null)
        {
            SpriteBillboard[] allBillboards = FindObjectsByType<SpriteBillboard>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < allBillboards.Length; i++)
            {
                SpriteBillboard billboard = allBillboards[i];
                if (billboard != null && billboard.gameObject.scene == destinationScene)
                    billboard.SetTargetCamera(sceneCamera);
            }

            if (playerRoot != null)
            {
                SpriteBillboard[] playerBillboards = playerRoot.GetComponentsInChildren<SpriteBillboard>(true);
                for (int i = 0; i < playerBillboards.Length; i++)
                    playerBillboards[i].SetTargetCamera(sceneCamera);
            }
        }

        Physics.SyncTransforms();
    }

    private List<ISceneWarmupTask> FindSceneWarmupTasks(Scene scene)
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<ISceneWarmupTask> tasks = new List<ISceneWarmupTask>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.gameObject.scene == scene && behaviour is ISceneWarmupTask task)
                tasks.Add(task);
        }

        tasks.Sort((a, b) =>
        {
            int order = a.Order.CompareTo(b.Order);
            return order != 0
                ? order
                : string.Compare(a.WarmupId, b.WarmupId, StringComparison.Ordinal);
        });
        return tasks;
    }

    private bool RejectRequest(
        SceneTransitionRequest request,
        SceneTransitionOutcome outcome,
        string message)
    {
        SceneTransitionResult result = new SceneTransitionResult(
            outcome,
            request?.SceneId ?? string.Empty,
            message,
            sceneActivated: false,
            spawnId: request?.SpawnId ?? string.Empty);
        SafeInvoke(() => request?.Completed?.Invoke(result), "rejected request completion");
        if (outcome != SceneTransitionOutcome.Busy)
            SafeInvoke(() => TransitionFailed?.Invoke(result), "rejected TransitionFailed");
        Debug.LogWarning($"[SceneLoader] {message} (destination='{request?.SceneId}', current='{activeSceneId}')");
        return false;
    }

    private void ShowRecovery(
        SceneTransitionRequest failedRequest,
        SceneTransitionResult result,
        SceneLoadingConfigSO config)
    {
        if (loadingScreenUI == null)
            return;

        Action retry = () =>
        {
            loadingScreenUI.HideRecoveryImmediate();
            RequestTransition(failedRequest);
        };

        if (!result.SceneActivated)
        {
            loadingScreenUI.ShowRecovery(
                result.Message,
                retry,
                "Cancel",
                () =>
                {
                    loadingScreenUI.HideRecoveryImmediate();
                    loadingScreenUI.Hide();
                });
            return;
        }

        bool failedOnRecoveryScene = string.Equals(
            failedRequest.SceneId,
            config.RecoverySceneId,
            StringComparison.OrdinalIgnoreCase);
        loadingScreenUI.ShowRecovery(
            result.Message,
            retry,
            failedOnRecoveryScene ? "Retry Main Menu" : "Main Menu",
            failedOnRecoveryScene
                ? retry
                : () =>
                {
                    loadingScreenUI.HideRecoveryImmediate();
                    RequestTransition(new SceneTransitionRequest(
                        config.RecoverySceneId,
                        styleOverride: TransitionStyle.FullLoading,
                        sourceId: "SceneLoader.Recovery"));
                });
    }

    private void InvokeBeforeReveal(
        SceneTransitionContext context,
        SceneTransitionRequest request)
    {
        SafeInvoke(() => BeforeReveal?.Invoke(context), "BeforeReveal");
        SafeInvoke(() => request.BeforeReveal?.Invoke(context), "request BeforeReveal");
    }

    private static void SafeInvoke(Action action, string label)
    {
        if (action == null)
            return;
        try
        {
            action.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(new Exception($"Scene transition callback '{label}' failed.", exception));
        }
    }

    private void SetStatus(
        SceneTransitionPhase phase,
        float progress,
        string message)
    {
        displayedProgress = Mathf.Max(displayedProgress, Mathf.Clamp01(progress));
        currentStatus = new SceneTransitionStatus(
            phase,
            displayedProgress,
            activeSceneId,
            message,
            transitionDegraded);

        if (loadingScreenUI != null)
            loadingScreenUI.SetProgress(displayedProgress);
        SafeInvoke(() => TransitionChanged?.Invoke(currentStatus), "TransitionChanged");
    }

    private void ApplyPresentationConfig(SceneLoadingConfigSO config)
    {
        if (loadingScreenUI == null || config == null)
            return;
        loadingScreenUI.ConfigureTimings(
            config.FadeInSeconds,
            config.FadeOutSeconds,
            config.PostLoadFadeOutDelaySeconds);
    }

    private void BeginSceneTransitionHold()
    {
        if (lockInputUntilFadeComplete)
        {
            InputLockService service = InputLockService.Instance;
            if (service != null)
                sceneTransitionLockHandle = service.AcquireLock(
                    "SceneLoader.Transition",
                    InputLockChannel.All,
                    this);
        }

        if (!freezePlayerUntilFadeComplete)
            return;
        GameObject player = ResolvePlayerForTransitionHold();
        frozenPlayer = player != null ? player.GetComponent<PlayerMovement3D>() : null;
        if (frozenPlayer == null)
            return;
        frozenPlayer.SetFrozen(true, preservePhysics: false);
        frozenPlayerHeld = true;
    }

    private void EndSceneTransitionHold()
    {
        if (sceneTransitionLockHandle.IsValid && !sceneTransitionLockHandle.IsReleased)
            sceneTransitionLockHandle.Release();
        sceneTransitionLockHandle = default;

        if (frozenPlayerHeld && frozenPlayer != null)
            frozenPlayer.SetFrozen(false, preservePhysics: false);
        frozenPlayer = null;
        frozenPlayerHeld = false;
    }

    private static GameObject ResolvePlayerForTransitionHold()
    {
        return PlayerRootResolver.ResolveActivePlayer();
    }

    private void RestoreLoadingPriority()
    {
        if (!loadingPriorityChanged)
            return;
        Application.backgroundLoadingPriority = previousLoadingPriority;
        loadingPriorityChanged = false;
    }

    /// <summary>
    /// Combat already activated but the loader coroutine never cleared busy.
    /// Stop it so a post-battle overworld return can start.
    /// </summary>
    public bool AbortStalledTransitionWhenDestinationAlreadyActive(string expectedActiveSceneId)
    {
        if (!isTransitioning)
            return false;
        if (string.IsNullOrWhiteSpace(expectedActiveSceneId))
            return false;

        string expected = expectedActiveSceneId.Trim();
        if (!string.Equals(activeSceneId, expected, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(SceneManager.GetActiveScene().name, expected, StringComparison.Ordinal))
            return false;

        Debug.LogWarning(
            $"[SceneLoader] Aborting stalled transition (was targeting '{activeSceneId}') because '{expectedActiveSceneId}' is already active.");
        if (activeTransitionCoroutine != null)
            StopCoroutine(activeTransitionCoroutine);
        EmergencyCleanup();
        return true;
    }

    private void EmergencyCleanup()
    {
        RestoreLoadingPriority();
        EndSceneTransitionHold();
        isTransitioning = false;
        activeTransitionCoroutine = null;
    }

    private void ConfigureSceneLighting(string spawnId, Scene destinationScene)
    {
        bool isIndoor = false;
        if (!string.IsNullOrEmpty(spawnId))
        {
            SpawnPoint spawnPoint = SpawnService.Instance?.GetSpawnPoint(spawnId);
            if (spawnPoint != null)
                isIndoor = spawnPoint.IsIndoor;
        }

        TimeOfDayVisualApplier visualApplier = TimeOfDayVisualApplier.ActiveInstance;
        if (visualApplier == null)
            visualApplier = FindObjectOfType<TimeOfDayVisualApplier>();
        if (visualApplier == null)
            return;

        if (isIndoor)
            ConfigureIndoorLighting(visualApplier, destinationScene);
        else
            ConfigureOutdoorLighting(visualApplier);
    }

    private void ConfigureIndoorLighting(TimeOfDayVisualApplier visualApplier, Scene destinationScene)
    {
        IndoorDirectionalLight indoorLight = IndoorDirectionalLight.FindForScene(destinationScene);
        if (indoorLight == null)
            indoorLight = IndoorDirectionalLight.ActiveIndoorLight;
        if (indoorLight == null)
            indoorLight = FindObjectOfType<IndoorDirectionalLight>();

        if (indoorLight != null)
            visualApplier.ConfigureIndoorLight(indoorLight);
        else
            Debug.LogWarning("[SceneLoader] Indoor spawn has no IndoorDirectionalLight.");
        visualApplier.SetIndoors(true);
    }

    private void ConfigureOutdoorLighting(TimeOfDayVisualApplier visualApplier)
    {
        Light outdoorSun = null;
        if (RenderSettings.sun != null &&
            RenderSettings.sun.type == LightType.Directional &&
            RenderSettings.sun.GetComponent<IndoorDirectionalLight>() == null)
        {
            outdoorSun = RenderSettings.sun;
        }

        if (outdoorSun != null)
        {
            visualApplier.ConfigureOutdoorSun(outdoorSun);
            RenderSettings.sun = outdoorSun;
            DirectionalLightTimeDriver driver = FindObjectOfType<DirectionalLightTimeDriver>();
            driver?.SetLight(outdoorSun);
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[SceneLoader] Outdoor scene has no directional sun.");
        }
        visualApplier.SetIndoors(false);
    }

    private void NotifySceneSoundtrack(string sceneName, SoundtrackTriggerMoment moment)
    {
        MusicConductor.Instance?.RequestSceneSoundtrack(sceneName, moment);
    }

    private IEnumerator WaitForCameraZoneTransition(CameraZoneTrigger trigger)
    {
        CameraZoneBehavior zoneBehavior = trigger != null ? trigger.ZoneBehavior : null;
        if (zoneBehavior == null)
            yield break;

        const float timeoutSeconds = 5f;
        float elapsed = 0f;
        while ((!zoneBehavior.IsActive || !zoneBehavior.IsTransitionSettled) && elapsed < timeoutSeconds)
        {
            GameObject player = SpawnService.Instance != null
                ? SpawnService.Instance.GetResolvedPlayerRoot()
                : null;
            if (player != null)
                trigger.ForceActivateAtPosition(player.transform.position, reassertPresentation: true);
            else
                trigger.ForceActivateIfPlayerInside();

            if (!zoneBehavior.IsActive)
                zoneBehavior.Activate();

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!zoneBehavior.IsActive)
        {
            Debug.LogWarning(
                $"[SceneLoader] Camera zone '{trigger.gameObject.name}' did not activate before destination finalization timed out.");
            yield break;
        }

        if (!zoneBehavior.IsTransitionSettled)
        {
            Debug.LogWarning(
                $"[SceneLoader] Camera zone '{trigger.gameObject.name}' did not settle before destination finalization timed out.");
            yield break;
        }

        // Let LateUpdate consumers (billboards and shadow casters) observe the final yaw.
        yield return null;
    }
}
