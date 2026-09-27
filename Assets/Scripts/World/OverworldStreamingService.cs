using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent coordinator for silent additive overworld chunk IO. Hard scene
/// swaps stay owned by <see cref="SceneLoader"/>; this service only manages
/// residency after the player is inside a configured overworld set.
/// </summary>
public sealed class OverworldStreamingService : MonoBehaviour
{
    private static OverworldStreamingService instance;

    [Header("Configuration")]
    [Tooltip("Optional explicit config. When empty, uses SceneLoadingConfigSO's linked overworld config.")]
    [SerializeField] private OverworldStreamingConfigSO streamingConfig;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private readonly HashSet<string> desiredChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> loadingChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> unloadingChunkIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> pendingReturnChunkIds = new List<string>();

    private string pendingReturnActiveSceneId;
    private string deferredActiveChunkId;
    private int deferredActiveChunkAttempts;
    private Camera persistentOverworldCamera;
    private GameObject persistentOverworldCameraRig;
    private ISceneOperationBackend fallbackBackend;

    public static OverworldStreamingService Instance
    {
        get
        {
            if (instance == null)
                instance = FindFirstObjectByType<OverworldStreamingService>(FindObjectsInactive.Include);
            return instance;
        }
    }

    public OverworldStreamingConfigSO Config => streamingConfig != null
        ? streamingConfig
        : SceneLoadingConfigRuntime.Resolve()?.OverworldStreamingConfig;

    public Camera PersistentOverworldCamera => persistentOverworldCamera;

    private ISceneOperationBackend OperationBackend
    {
        get
        {
            SceneLoader loader = SceneLoader.Instance;
            if (loader != null)
                return loader.OperationBackendForStreaming;

            return fallbackBackend ??= new UnitySceneOperationBackend();
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[{nameof(OverworldStreamingService)}] Duplicate instance found. Destroying {gameObject.name}.");
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        SceneManager.sceneUnloaded += HandleSceneUnloaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        deferredActiveChunkId = null;
        deferredActiveChunkAttempts = 0;
    }

    private void Update()
    {
        FlushDeferredActiveChunk();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    /// <summary>
    /// True when a hard load should hide the DontDestroyOnLoad overworld camera
    /// before the destination scene activates. Combat and interiors bind a
    /// scene-local rig; leaving the exclusive persistent presenter live lets
    /// their OnEnable steal the overworld camera.
    /// </summary>
    public static bool ShouldReleasePersistentCamera(OverworldStreamingConfigSO config, string sceneId)
    {
        string normalized = sceneId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
            return false;

        return config == null || !config.ContainsScene(normalized);
    }

    /// <summary>
    /// Called by <see cref="SceneLoader"/> while the cover is up, before the
    /// destination scene is allowed to activate.
    /// </summary>
    public void ReleasePersistentCameraForDestination(string sceneId)
    {
        if (!ShouldReleasePersistentCamera(Config, sceneId))
            return;

        ReleasePersistentOverworldCamera();
    }

    /// <summary>
    /// Called by <see cref="SceneLoader"/> behind its existing cover after a
    /// hard entry into an overworld scene. This is intentionally an IEnumerator
    /// so initial residency is complete before the cover reveals.
    /// </summary>
    public IEnumerator EnsureSetForEntry(
        string entrySceneId,
        string spawnId,
        string[] preferredChunkSceneIds = null)
    {
        OverworldStreamingConfigSO config = Config;
        if (config == null || !config.ContainsScene(entrySceneId))
            yield break;

        string[] resolvedPreferred = SanctuaryLatchZone.ResolvePreferredResidentChunkSceneIds(
            entrySceneId,
            spawnId,
            preferredChunkSceneIds);

        bool usePreferredChunks = HasPreferredResidency(config, resolvedPreferred);
        List<string> residentIds = usePreferredChunks
            ? config.GetResidentSceneIdsForPreferredChunks(resolvedPreferred)
            : config.GetEntryResidentSceneIds();

        if (!usePreferredChunks)
        {
            for (int i = 0; i < pendingReturnChunkIds.Count; i++)
                AddUnique(residentIds, pendingReturnChunkIds[i]);
        }
        else if (enableDebugLogs)
        {
            Debug.Log(
                $"[{nameof(OverworldStreamingService)}] Sanctuary entry using authored chunks " +
                $"'{string.Join(", ", residentIds)}' instead of the full preload set.");
        }

        AddUnique(residentIds, entrySceneId);

        for (int i = 0; i < residentIds.Count; i++)
        {
            string sceneId = residentIds[i];
            desiredChunkIds.Add(sceneId);
            yield return EnsureChunkRoutine(sceneId, warmupAfterLoad: true);
        }

        EnsurePersistentCameraRig(config);

        string activeSceneId = usePreferredChunks
            ? entrySceneId
            : (!string.IsNullOrWhiteSpace(pendingReturnActiveSceneId)
                ? pendingReturnActiveSceneId
                : entrySceneId);
        pendingReturnChunkIds.Clear();
        pendingReturnActiveSceneId = string.Empty;
        SetActiveChunk(activeSceneId);
    }

    /// <summary>Begins silent additive loading for one configured chunk.</summary>
    public bool EnsureChunk(string sceneId)
    {
        if (!CanStream(sceneId, out string error))
        {
            LogWarning(error);
            return false;
        }

        desiredChunkIds.Add(sceneId.Trim());
        StartCoroutine(EnsureChunkRoutine(sceneId, warmupAfterLoad: true));
        return true;
    }

    /// <summary>
    /// Begins silent unloading for one configured chunk. The active chunk is
    /// never unloaded; a content volume must set a safe active neighbor first.
    /// </summary>
    public bool ReleaseChunk(string sceneId)
    {
        if (!CanStream(sceneId, out string error))
        {
            LogWarning(error);
            return false;
        }

        string normalized = sceneId.Trim();
        if (string.Equals(SceneManager.GetActiveScene().name, normalized, StringComparison.OrdinalIgnoreCase))
        {
            LogWarning($"Cannot release active overworld chunk '{normalized}'. Set an adjacent loaded chunk active first.");
            return false;
        }

        desiredChunkIds.Remove(normalized);
        StartCoroutine(ReleaseChunkRoutine(normalized));
        return true;
    }

    /// <summary>
    /// Sets Unity's active scene for spawn lookup, encounter snapshots, and
    /// active-chunk soundtrack ownership. Additive loading itself stays silent.
    /// Unity rejects SetActiveScene from physics callbacks (OnTriggerEnter,
    /// WaitForFixedUpdate). Those calls are retried on the next Update.
    /// </summary>
    public bool SetActiveChunk(string sceneId)
    {
        return SetActiveChunk(sceneId, allowDefer: true);
    }

    private bool SetActiveChunk(string sceneId, bool allowDefer)
    {
        if (!CanStream(sceneId, out string error))
        {
            LogWarning(error);
            return false;
        }

        string normalized = sceneId.Trim();
        Scene scene = SceneManager.GetSceneByName(normalized);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            LogWarning($"Cannot set unloaded overworld chunk '{sceneId}' active.");
            return false;
        }

        Scene current = SceneManager.GetActiveScene();
        if (current.IsValid() && current.handle == scene.handle)
        {
            deferredActiveChunkId = null;
            deferredActiveChunkAttempts = 0;
            return true;
        }

        if (!OperationBackend.SetActiveScene(scene, out string activeError))
        {
            if (allowDefer)
            {
                deferredActiveChunkId = normalized;
                if (enableDebugLogs)
                    Debug.Log($"[{nameof(OverworldStreamingService)}] {activeError} Retrying on the next Update.");
                return false;
            }

            return false;
        }

        deferredActiveChunkId = null;
        deferredActiveChunkAttempts = 0;
        MusicConductor.Instance?.RequestActiveOverworldChunkSoundtrack(normalized);
        return true;
    }

    private void FlushDeferredActiveChunk()
    {
        if (string.IsNullOrEmpty(deferredActiveChunkId))
            return;

        const int maxAttempts = 8;
        string sceneId = deferredActiveChunkId;
        if (SetActiveChunk(sceneId, allowDefer: false))
            return;

        deferredActiveChunkAttempts++;
        if (deferredActiveChunkAttempts < maxAttempts)
        {
            deferredActiveChunkId = sceneId;
            return;
        }

        deferredActiveChunkId = null;
        deferredActiveChunkAttempts = 0;
        LogWarning($"Unity rejected '{sceneId}' as the active scene.");
    }

    /// <summary>
    /// Arms the next hard overworld entry to restore this exact set before its
    /// existing cover reveals. Used by combat return; it does not initiate IO.
    /// </summary>
    public bool PrepareReturnToSet(string activeSceneId, IEnumerable<string> loadedChunkIds)
    {
        OverworldStreamingConfigSO config = Config;
        if (config == null || !config.ContainsScene(activeSceneId))
            return false;

        pendingReturnChunkIds.Clear();
        if (loadedChunkIds != null)
        {
            foreach (string sceneId in loadedChunkIds)
            {
                if (config.ContainsScene(sceneId))
                    AddUnique(pendingReturnChunkIds, sceneId);
            }
        }

        AddUnique(pendingReturnChunkIds, config.HubSceneId);
        AddUnique(pendingReturnChunkIds, activeSceneId);
        pendingReturnActiveSceneId = activeSceneId.Trim();
        return true;
    }

    public string ResolveHubFor(string sceneId)
    {
        OverworldStreamingConfigSO config = Config;
        return config != null && config.ContainsScene(sceneId)
            ? config.HubSceneId
            : sceneId?.Trim() ?? string.Empty;
    }

    public List<string> CaptureLoadedChunkIds()
    {
        List<string> loaded = new List<string>();
        OverworldStreamingConfigSO config = Config;
        if (config == null)
            return loaded;

        List<string> candidates = config.GetAllSceneIds();
        for (int i = 0; i < candidates.Count; i++)
        {
            Scene scene = SceneManager.GetSceneByName(candidates[i]);
            if (scene.IsValid() && scene.isLoaded)
                loaded.Add(scene.name);
        }

        return loaded;
    }

    private IEnumerator EnsureChunkRoutine(string sceneId, bool warmupAfterLoad)
    {
        string normalized = sceneId?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
            yield break;

        Scene loadedScene = SceneManager.GetSceneByName(normalized);
        if (loadedScene.IsValid() && loadedScene.isLoaded)
            yield break;

        if (loadingChunkIds.Contains(normalized))
        {
            while (loadingChunkIds.Contains(normalized))
                yield return null;
            yield break;
        }

        if (!CanStream(normalized, out string error))
        {
            LogWarning(error);
            yield break;
        }

        loadingChunkIds.Add(normalized);
        ThreadPriority previousPriority = Application.backgroundLoadingPriority;
        bool changedPriority = false;
        try
        {
            Application.backgroundLoadingPriority = Config.SilentLoadingPriority;
            changedPriority = true;

            if (!OperationBackend.TryBeginLoadAdditive(normalized, out ISceneLoadOperation operation, out error))
            {
                LogWarning(error);
                yield break;
            }

            while (!operation.IsDone)
                yield return null;

            loadedScene = SceneManager.GetSceneByName(normalized);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                LogWarning($"Additive load completed but '{normalized}' is not available.");
                yield break;
            }

            // Additive sceneLoaded also strips, but Destroy is deferred. Neutralize
            // again here so silent warmup never binds leftover hub cameras.
            EnsurePersistentCameraRig(Config);

            if (warmupAfterLoad)
                yield return RunSilentWarmups(loadedScene);
        }
        finally
        {
            if (changedPriority)
                Application.backgroundLoadingPriority = previousPriority;
            loadingChunkIds.Remove(normalized);
        }

        if (!desiredChunkIds.Contains(normalized))
            yield return ReleaseChunkRoutine(normalized);
    }

    private IEnumerator ReleaseChunkRoutine(string sceneId)
    {
        if (loadingChunkIds.Contains(sceneId))
        {
            while (loadingChunkIds.Contains(sceneId))
                yield return null;
        }

        if (desiredChunkIds.Contains(sceneId) || unloadingChunkIds.Contains(sceneId))
            yield break;

        Scene scene = SceneManager.GetSceneByName(sceneId);
        if (!scene.IsValid() || !scene.isLoaded)
            yield break;

        if (SceneManager.GetActiveScene() == scene)
        {
            LogWarning($"Refusing to unload active overworld chunk '{sceneId}'.");
            yield break;
        }

        unloadingChunkIds.Add(sceneId);
        try
        {
            if (!OperationBackend.TryBeginUnload(scene, out ISceneOperation operation, out string error))
            {
                LogWarning(error);
                yield break;
            }

            while (!operation.IsDone)
                yield return null;
        }
        finally
        {
            unloadingChunkIds.Remove(sceneId);
        }
    }

    private IEnumerator RunSilentWarmups(Scene scene)
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<ISceneWarmupTask> tasks = new List<ISceneWarmupTask>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].gameObject.scene == scene && behaviours[i] is ISceneWarmupTask task)
                tasks.Add(task);
        }

        tasks.Sort((left, right) =>
        {
            int order = left.Order.CompareTo(right.Order);
            return order != 0 ? order : string.Compare(left.WarmupId, right.WarmupId, StringComparison.Ordinal);
        });

        for (int i = 0; i < tasks.Count; i++)
        {
            ISceneWarmupTask task = tasks[i];
            SceneTransitionContext transition = new SceneTransitionContext(
                scene.name,
                string.Empty,
                TransitionStyle.QuickFade,
                scene);
            SceneWarmupContext context = new SceneWarmupContext(
                transition,
                SceneLoadingConfigRuntime.Resolve(),
                null);

            float startedAt = Time.unscaledTime;
            IEnumerator routine;
            try
            {
                routine = task.Warmup(context);
            }
            catch (Exception exception)
            {
                LogWarning($"Silent warmup '{task.WarmupId}' could not start: {exception.Message}");
                continue;
            }

            while (routine != null && Time.unscaledTime - startedAt < Config.SilentWarmupTimeoutSeconds)
            {
                bool moved;
                object yielded;
                try
                {
                    moved = routine.MoveNext();
                    yielded = moved ? routine.Current : null;
                }
                catch (Exception exception)
                {
                    LogWarning($"Silent warmup '{task.WarmupId}' failed: {exception.Message}");
                    break;
                }

                if (!moved)
                    break;
                yield return yielded;
            }

            if (context.Failed)
                LogWarning($"Silent warmup '{task.WarmupId}' reported: {context.FailureMessage}");
            else if (Time.unscaledTime - startedAt >= Config.SilentWarmupTimeoutSeconds)
                LogWarning($"Silent warmup '{task.WarmupId}' timed out after {Config.SilentWarmupTimeoutSeconds:0.#} seconds.");
        }
    }

    private void EnsurePersistentCameraRig(OverworldStreamingConfigSO config)
    {
        if (config == null)
            return;

        if (persistentOverworldCamera == null)
            AdoptOverworldCameraRig(config);

        if (persistentOverworldCamera == null)
        {
            LogWarning($"No '{config.CameraRigRootName}' camera rig was found in the overworld set.");
            return;
        }

        if (persistentOverworldCameraRig != null && !persistentOverworldCameraRig.activeSelf)
            persistentOverworldCameraRig.SetActive(true);

        SceneCameraResolver.RegisterPersistentCamera(persistentOverworldCamera);
        StripDuplicateOverworldCameraRigs(config);
    }

    private void AdoptOverworldCameraRig(OverworldStreamingConfigSO config)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            Transform root = FindCameraRigRoot(camera, config);
            if (root == null)
                continue;

            GameServicesContainer services = GameServicesContainer.Instance;
            if (services != null)
                root.SetParent(services.transform, true);
            else
                DontDestroyOnLoad(root.gameObject);

            persistentOverworldCamera = camera;
            persistentOverworldCameraRig = root.gameObject;
            if (enableDebugLogs)
                Debug.Log($"[{nameof(OverworldStreamingService)}] Persisted camera rig '{root.name}'.");
            return;
        }
    }

    private void StripDuplicateOverworldCameraRigs(OverworldStreamingConfigSO config)
    {
        NeutralizeDuplicateOverworldCameraRigs(
            persistentOverworldCamera,
            persistentOverworldCameraRig,
            config,
            enableDebugLogs);
    }

    /// <summary>
    /// Immediately disables leftover overworld cameras, then destroys named
    /// CameraRigRoots. Additive hub reloads must not present at authored default
    /// yaw while the GameServices rig still holds the live camera-zone yaw.
    /// </summary>
    public static void NeutralizeDuplicateOverworldCameraRigs(
        Camera persistentCamera,
        GameObject persistentRig,
        OverworldStreamingConfigSO config,
        bool enableDebugLogs = false)
    {
        if (config == null)
            return;

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || camera == persistentCamera)
                continue;

            if (!config.ContainsScene(camera.gameObject.scene.name))
                continue;

            Transform root = FindCameraRigRoot(camera, config);
            if (root != null && root.gameObject == persistentRig)
                continue;

            bool isSeedPresenter = root != null || camera.CompareTag("MainCamera");
            if (!isSeedPresenter)
                continue;

            NeutralizeLeftoverOverworldCamera(camera, root, enableDebugLogs);
        }
    }

    private static void NeutralizeLeftoverOverworldCamera(
        Camera camera,
        Transform rigRoot,
        bool enableDebugLogs)
    {
        if (camera == null)
            return;

        camera.enabled = false;
        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener != null)
            listener.enabled = false;

        Transform toDestroy = rigRoot != null ? rigRoot : null;
        if (toDestroy != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[{nameof(OverworldStreamingService)}] Removing duplicate camera rig " +
                    $"'{toDestroy.name}' from '{toDestroy.gameObject.scene.name}'.");
            }

            toDestroy.gameObject.SetActive(false);
            DestroyOverworldObject(toDestroy.gameObject);
            return;
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[{nameof(OverworldStreamingService)}] Disabled leftover overworld camera " +
                $"'{camera.name}' in '{camera.gameObject.scene.name}'.");
        }
    }

    private static void DestroyOverworldObject(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static Transform FindCameraRigRoot(Camera camera, OverworldStreamingConfigSO config)
    {
        if (camera == null || config == null || !config.ContainsScene(camera.gameObject.scene.name))
            return null;

        Transform current = camera.transform;
        while (current != null)
        {
            if (string.Equals(current.name, config.CameraRigRootName, StringComparison.Ordinal))
                return current;

            Transform parent = current.parent;
            if (parent == null || parent.gameObject.scene != camera.gameObject.scene)
                break;
            current = parent;
        }

        return null;
    }

    private bool CanStream(string sceneId, out string error)
    {
        OverworldStreamingConfigSO config = Config;
        if (config == null)
        {
            error = "No OverworldStreamingConfigSO is assigned to SceneLoadingConfigSO.";
            return false;
        }

        string normalized = sceneId?.Trim() ?? string.Empty;
        if (!config.ContainsScene(normalized))
        {
            error = $"Scene '{normalized}' is not in the configured overworld set.";
            return false;
        }

        if (!OperationBackend.CanLoad(normalized))
        {
            error = $"Scene '{normalized}' is not available in Build Settings.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private Coroutine cameraZoneResyncRoutine;

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        OverworldStreamingConfigSO config = Config;
        bool overworldScene = config != null && config.ContainsScene(scene.name);

        // Additive chunk IO (Loomville reload after buffer-exit) used to skip
        // here, leaving the hub's seed CameraRigRoot live at authored default yaw.
        if (mode == LoadSceneMode.Additive)
        {
            if (overworldScene)
            {
                EnsurePersistentCameraRig(config);
                ScheduleCameraZoneResync();
            }
            return;
        }

        if (!overworldScene)
        {
            desiredChunkIds.Clear();
            pendingReturnChunkIds.Clear();
            pendingReturnActiveSceneId = string.Empty;
            ReleasePersistentOverworldCamera();
            return;
        }

        // Re-enable the adopted rig before warmup so returning to the set does
        // not depend on the newly loaded hub still containing a seed camera.
        EnsurePersistentCameraRig(config);
        ScheduleCameraZoneResync();
    }

    private void HandleSceneUnloaded(Scene scene)
    {
        desiredChunkIds.Remove(scene.name);
        loadingChunkIds.Remove(scene.name);
        unloadingChunkIds.Remove(scene.name);

        OverworldStreamingConfigSO config = Config;
        if (config != null && config.ContainsScene(scene.name))
            ScheduleCameraZoneResync();
    }

    /// <summary>
    /// Physics often emits false camera-zone enter/exit while a chunk loads.
    /// Wait until a covered scene transition has placed the player, then one
    /// render + physics step, and sample live zones from player position.
    /// </summary>
    private void ScheduleCameraZoneResync()
    {
        if (!isActiveAndEnabled)
        {
            CameraZoneOccupancy.ResyncAllLiveZones();
            return;
        }

        if (cameraZoneResyncRoutine != null)
            StopCoroutine(cameraZoneResyncRoutine);

        cameraZoneResyncRoutine = StartCoroutine(CameraZoneResyncRoutine());
    }

    private IEnumerator CameraZoneResyncRoutine()
    {
        while (CameraZoneOccupancy.IsSceneTransitionOccupancyDeferred())
            yield return null;

        yield return null;
        yield return new WaitForFixedUpdate();
        yield return null;
        CameraZoneOccupancy.ResyncAllLiveZones();
        cameraZoneResyncRoutine = null;
    }

    private void LogWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            Debug.LogWarning($"[{nameof(OverworldStreamingService)}] {message}");
    }

    private void ReleasePersistentOverworldCamera()
    {
        SetOverworldCameraPresentation(false);
        SceneCameraResolver.ClearPersistentCamera(persistentOverworldCamera);
    }

    private void SetOverworldCameraPresentation(bool active)
    {
        if (persistentOverworldCameraRig != null && persistentOverworldCameraRig.activeSelf != active)
            persistentOverworldCameraRig.SetActive(active);
    }

    private static bool HasPreferredResidency(
        OverworldStreamingConfigSO config,
        IEnumerable<string> preferredChunkSceneIds)
    {
        if (config == null || preferredChunkSceneIds == null)
            return false;

        foreach (string sceneId in preferredChunkSceneIds)
        {
            if (config.ContainsScene(sceneId))
                return true;
        }

        return false;
    }

    private static void AddUnique(ICollection<string> destination, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (string existing in destination)
        {
            if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                return;
        }

        destination.Add(value.Trim());
    }
}
