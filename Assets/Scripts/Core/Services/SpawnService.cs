using System;
using System.Collections;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Service for managing player spawn points.
/// Maintains registry of SpawnPoint components and spawns player at requested ID.
/// </summary>
public class SpawnService : MonoBehaviour
{
    private static SpawnService instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static SpawnService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SpawnService>();
                if (instance == null)
                {
                    GameObject go = new GameObject("SpawnService");
                    instance = go.AddComponent<SpawnService>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Player Reference")]
    [Tooltip("Player GameObject. Auto-finds by tag if null.")]
    [SerializeField] private GameObject playerObject;
    
    [Tooltip("Player prefab to instantiate if player not found in scene (optional)")]
    [SerializeField] private GameObject playerPrefab;
    
    [Tooltip("Player tag for auto-find")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Make player persist across scenes (DontDestroyOnLoad)")]
    [SerializeField] private bool persistPlayerAcrossScenes = true;
    
    [Header("Auto Spawn Settings")]
    [Tooltip("Automatically spawn player when scene loads")]
    [SerializeField] private bool autoSpawnOnSceneLoad = true;
    
    [Tooltip("Default spawn point ID to use if no spawn ID is stored (leave empty to use first spawn point)")]
    [SerializeField] private string defaultSpawnId = "";

    [Header("Combat Scene Restrictions")]
    [Tooltip("Disable player control components while in combat scenes to prevent overworld input from competing with combat selection.")]
    [SerializeField] private bool applyCombatScenePlayerRestrictions = true;

    [Tooltip("Skip SpawnService auto-spawn when a combat scene is loaded.")]
    [SerializeField] private bool skipAutoSpawnInCombatScenes = true;

    [Tooltip("Exact combat scene names (case-insensitive).")]
    [SerializeField] private string[] combatSceneNames = { "Combat2_TestHarness" };

    [Tooltip("Scene-name keywords treated as combat scenes when exact-name list does not match.")]
    [SerializeField] private string[] combatSceneNameKeywords = { "Combat", "Battle" };

    [Tooltip("Disable PlayerMovement3D while in combat scenes.")]
    [SerializeField] private bool disablePlayerMovementInCombatScenes = true;

    [Tooltip("Disable PlayerInteractor while in combat scenes.")]
    [SerializeField] private bool disablePlayerInteractorInCombatScenes = true;

    [Tooltip("Deactivate the persisted PlayerRoot for the full combat scene so its visuals, tags, and colliders cannot participate in combat presentation.")]
    [SerializeField] private bool deactivatePlayerRootInCombatScenes = true;

    [Header("Scene Filters")]
    [Tooltip("Do not create or auto-spawn a Player in these scenes (Bootstrap / main menu have no spawn points, but RefreshPlayerReference used to instantiate one anyway).")]
    [SerializeField] private string[] skipPlayerCreateSceneNames = { "Bootstrap", "main_menu" };
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [Tooltip("Logs auto-spawn pipeline decisions (skip flag, lastSpawnId) even when enableDebugLogs is off. Use to trace post-battle exact return vs nearest-spawn races.")]
    [SerializeField] private bool logAutoSpawnPipelineDiagnostics = false;
    
    // Registry of spawn points by ID
    private readonly Dictionary<string, SpawnPoint> spawnPoints = new Dictionary<string, SpawnPoint>();
    
    // Last spawn point ID used (persists across scenes)
    private string lastSpawnId = "";

    /// <summary>
    /// When true, <see cref="AutoSpawnCoroutine"/> will not run on the next scene load.
    /// Used when another system (e.g. post-battle exact return) will position the player instead.
    /// </summary>
    private bool skipAutoSpawnOnce;

    // Combat scene state
    private bool isCombatSceneActive;
    private bool movementDisabledForCombat;
    private bool interactorDisabledForCombat;
    private PlayerMovement3D movementDisabledComponent;
    private PlayerInteractor interactorDisabledComponent;
    private bool playerRootDeactivatedForCombat;
    private GameObject playerRootDeactivatedObject;

    public bool IsCombatSceneActive => isCombatSceneActive;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[SpawnService] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
        // GameServicesContainer persists the whole authored hierarchy. Only a
        // standalone fallback SpawnService needs to persist itself.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
        
        // Subscribe to scene loaded events
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        
        // Auto-find an existing player only. Instantiating here would spawn a
        // PlayerRoot during Bootstrap / main_menu and leave it fighting the UI
        // EventSystem until player_room loads.
        RefreshPlayerReference(allowInstantiate: false);
        RefreshCombatSceneRestrictions(SceneManager.GetActiveScene());
    }
    
    private void OnDestroy()
    {
        ClearCombatPlayerRestrictions();

        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            instance = null;
        }
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Loading an additive scene must not change the gameplay mode until it
        // becomes active. Single-scene loads are authoritative immediately,
        // including the brief window before Unity reports the active scene.
        bool sceneControlsGameplay = mode == LoadSceneMode.Single || SceneManager.GetActiveScene() == scene;
        bool loadedSceneIsCombat = IsCombatScene(scene);
        if (sceneControlsGameplay)
            RefreshCombatSceneRestrictions(scene);

        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] OnSceneLoaded: {scene.name} (mode: {mode})");
            if (isCombatSceneActive && applyCombatScenePlayerRestrictions)
            {
                Debug.Log($"[SpawnService] Combat scene restrictions are active for scene '{scene.name}'.");
            }
        }

        // Scene-owned SpawnPoint.OnEnable has already registered by this point.
        // Keep additive chunk registrations intact; only stale/destroyed entries
        // from a prior Single scene need pruning here.
        PruneInvalidSpawnPoints();
        if (mode == LoadSceneMode.Single)
            UnregisterSpawnPointsOutsideScene(scene);
        
        // Check for duplicate players BEFORE refreshing reference
        // This catches scenarios where scene has a player AND we have a persisted player
        StartCoroutine(CheckForDuplicatePlayersAndCleanup(scene));
        
        // Auto-spawn if enabled
        if (sceneControlsGameplay &&
            autoSpawnOnSceneLoad &&
            !(skipAutoSpawnInCombatScenes && loadedSceneIsCombat) &&
            ShouldCreatePlayerInScene(scene.name))
        {
            if (logAutoSpawnPipelineDiagnostics || enableDebugLogs)
            {
                Debug.Log(
                    $"[SpawnService] AutoSpawnCoroutine scheduled: scene='{scene.name}', " +
                    $"skipAutoSpawnOnce={skipAutoSpawnOnce}, lastSpawnId='{lastSpawnId}', " +
                    $"defaultSpawnId='{defaultSpawnId}'");
            }
            // Wait for spawn points to register
            StartCoroutine(AutoSpawnCoroutine());
        }
        else if (sceneControlsGameplay &&
                 enableDebugLogs &&
                 autoSpawnOnSceneLoad &&
                 skipAutoSpawnInCombatScenes &&
                 loadedSceneIsCombat)
        {
            Debug.Log($"[SpawnService] Auto-spawn skipped in combat scene '{scene.name}'.");
        }

        // Apply restrictions immediately when entering combat if the player is already available.
        if (sceneControlsGameplay)
            ApplyCombatPlayerRestrictions();
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        RefreshCombatSceneRestrictions(next);
    }

    private void OnSceneUnloaded(Scene scene)
    {
        UnregisterSpawnPointsInScene(scene);
    }
    
    /// <summary>
    /// Check for and handle duplicate player objects.
    /// This can happen when returning to a scene that has a player placed in the editor
    /// while also having a persisted player from DontDestroyOnLoad.
    /// </summary>
    private IEnumerator CheckForDuplicatePlayersAndCleanup(Scene loadedScene)
    {
        // Wait a frame for all objects to initialize
        yield return null;

        // Find player roots via PlayerMovement3D so inactive duplicates in DontDestroyOnLoad
        // are still detected and cleaned up.
        PlayerMovement3D[] playerMovements = FindObjectsOfType<PlayerMovement3D>(true);
        HashSet<GameObject> uniqueRootPlayers = new HashSet<GameObject>();

        foreach (var movement in playerMovements)
        {
            if (movement == null)
                continue;

            GameObject rootPlayer = movement.gameObject;
            if (rootPlayer.scene.IsValid())
            {
                uniqueRootPlayers.Add(rootPlayer);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Found {playerMovements.Length} PlayerMovement3D instance(s), {uniqueRootPlayers.Count} unique player root(s)");
            foreach (var p in uniqueRootPlayers)
            {
                string sceneInfo = p.scene.name == "DontDestroyOnLoad" ? "DontDestroyOnLoad" : p.scene.name;
                Debug.Log($"[SpawnService]   - Root: {p.name} in scene: {sceneInfo}, InstanceID: {p.GetEntityId()}");
            }
        }
        
        if (uniqueRootPlayers.Count > 1 && persistPlayerAcrossScenes)
        {
            // We have actual duplicate root players! Keep the persisted one, destroy scene ones
            Debug.LogWarning($"[SpawnService] DUPLICATE PLAYER ROOTS DETECTED! Found {uniqueRootPlayers.Count} player root objects.");
            
            GameObject persistedPlayer = null;
            List<GameObject> scenePlayersToDestroy = new List<GameObject>();
            
            foreach (var p in uniqueRootPlayers)
            {
                if (p.scene.name == "DontDestroyOnLoad")
                {
                    // This is our persisted player
                    if (persistedPlayer == null)
                    {
                        persistedPlayer = p;
                        Debug.Log($"[SpawnService] Keeping persisted player root: {p.name} (InstanceID: {p.GetEntityId()})");
                    }
                    else
                    {
                        // Multiple persisted players?! Keep the first one
                        Debug.LogWarning($"[SpawnService] Multiple persisted player roots! Destroying extra: {p.name}");
                        scenePlayersToDestroy.Add(p);
                    }
                }
                else
                {
                    // This player is in the scene - should be destroyed
                    scenePlayersToDestroy.Add(p);
                    Debug.LogWarning($"[SpawnService] Found scene player root to destroy: {p.name} in {p.scene.name}");
                }
            }
            
            // Destroy duplicates
            foreach (var toDestroy in scenePlayersToDestroy)
            {
                Debug.Log($"[SpawnService] Destroying duplicate player root: {toDestroy.name}");
                Destroy(toDestroy);
            }
            
            // Ensure our reference is the persisted player
            if (persistedPlayer != null)
            {
                playerObject = persistedPlayer;
            }
        }
        else if (uniqueRootPlayers.Count == 1)
        {
            // Single player root - make sure it's our reference
            foreach (var p in uniqueRootPlayers)
            {
                playerObject = p;
                
                if (persistPlayerAcrossScenes && p.scene.name != "DontDestroyOnLoad")
                {
                    DontDestroyOnLoad(playerObject);
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SpawnService] Applied DontDestroyOnLoad to: {playerObject.name}");
                    }
                }
                break;
            }
        }
        else if (uniqueRootPlayers.Count == 0 && playerPrefab != null)
        {
            // No player found - will be handled by RefreshPlayerReference
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] No player found, will instantiate from prefab");
            }
        }
        
        // Now refresh the reference without creating a player for menu/bootstrap.
        RefreshPlayerReference(allowInstantiate: false);
        
        // Combat scenes own their camera and presentation. Do not refresh
        // overworld visual bindings while the persisted root is suppressed.
        if (playerObject != null && !isCombatSceneActive)
        {
            ValidatePlayerPhysicsState();
             
            // Restore prefab defaults for visual components
            // This ensures shadow/silhouette settings match prefab after scene transitions
            RestorePlayerVisualComponentDefaults();
            
            // Also refresh occlusion silhouette references after delay (cameras need time to initialize)
            StartCoroutine(RefreshOcclusionSilhouetteReferencesDelayed());
        }

        ApplyCombatPlayerRestrictions();
    }

    public bool IsCombatScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        if (IsCombatSceneName(scene.name))
            return true;

        return CombatSceneUtility.IsCombatScene(scene);
    }

    public void RefreshCombatSceneRestrictions(Scene scene)
    {
        bool nextIsCombatScene = IsCombatScene(scene);
        if (!nextIsCombatScene)
            ClearCombatPlayerRestrictions();

        isCombatSceneActive = nextIsCombatScene;
        if (isCombatSceneActive)
            ApplyCombatPlayerRestrictions();
    }

    private bool IsCombatSceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        if (combatSceneNames != null)
        {
            for (int i = 0; i < combatSceneNames.Length; i++)
            {
                string candidate = combatSceneNames[i];
                if (string.IsNullOrEmpty(candidate))
                    continue;

                if (string.Equals(sceneName, candidate, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        if (combatSceneNameKeywords != null)
        {
            for (int i = 0; i < combatSceneNameKeywords.Length; i++)
            {
                string keyword = combatSceneNameKeywords[i];
                if (string.IsNullOrEmpty(keyword))
                    continue;

                if (sceneName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        return false;
    }

    private void ApplyCombatPlayerRestrictions()
    {
        if (!applyCombatScenePlayerRestrictions || !isCombatSceneActive || playerObject == null)
            return;

        if (disablePlayerMovementInCombatScenes)
        {
            PlayerMovement3D movement = playerObject.GetComponent<PlayerMovement3D>();
            if (movement != null && movement.enabled)
            {
                movement.enabled = false;
                movementDisabledForCombat = true;
                movementDisabledComponent = movement;

                if (enableDebugLogs)
                {
                    Debug.Log("[SpawnService] Disabled PlayerMovement3D for combat scene.");
                }
            }
        }

        if (disablePlayerInteractorInCombatScenes)
        {
            PlayerInteractor interactor = playerObject.GetComponent<PlayerInteractor>();
            if (interactor == null)
                interactor = playerObject.GetComponentInChildren<PlayerInteractor>(true);

            if (interactor != null && interactor.enabled)
            {
                interactor.enabled = false;
                interactorDisabledForCombat = true;
                interactorDisabledComponent = interactor;

                if (enableDebugLogs)
                {
                    Debug.Log("[SpawnService] Disabled PlayerInteractor for combat scene.");
                }
            }
        }

        if (deactivatePlayerRootInCombatScenes && playerObject.activeSelf)
        {
            playerRootDeactivatedObject = playerObject;
            playerRootDeactivatedForCombat = true;
            playerObject.SetActive(false);

            if (enableDebugLogs)
                Debug.Log("[SpawnService] Deactivated PlayerRoot for combat scene presentation.");
        }
    }

    private void ClearCombatPlayerRestrictions()
    {
        if (playerRootDeactivatedForCombat)
        {
            if (playerRootDeactivatedObject != null)
                playerRootDeactivatedObject.SetActive(true);

            playerRootDeactivatedForCombat = false;
            playerRootDeactivatedObject = null;

            if (enableDebugLogs)
                Debug.Log("[SpawnService] Reactivated PlayerRoot after combat scene.");
        }

        if (movementDisabledForCombat)
        {
            if (movementDisabledComponent != null)
                movementDisabledComponent.enabled = true;

            movementDisabledForCombat = false;
            movementDisabledComponent = null;

            if (enableDebugLogs)
            {
                Debug.Log("[SpawnService] Restored PlayerMovement3D after combat scene.");
            }
        }

        if (interactorDisabledForCombat)
        {
            if (interactorDisabledComponent != null)
                interactorDisabledComponent.enabled = true;

            interactorDisabledForCombat = false;
            interactorDisabledComponent = null;

            if (enableDebugLogs)
            {
                Debug.Log("[SpawnService] Restored PlayerInteractor after combat scene.");
            }
        }
    }
    
    /// <summary>
    /// Get the root player object from any tagged object (handles child objects like Visual, Silhouette, etc.)
    /// </summary>
    private GameObject GetPlayerRoot(GameObject taggedObject)
    {
        return PlayerRootResolver.ResolveCandidate(taggedObject, playerTag);
    }
    
    /// <summary>
    /// Validate and fix player physics state after scene transition.
    /// </summary>
    private void ValidatePlayerPhysicsState()
    {
        if (playerObject == null) return;
        
        Rigidbody rb = playerObject.GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogWarning("[SpawnService] Player has no Rigidbody!");
            return;
        }
        
        // Log current physics state
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Player physics state:" +
                     $"\n  - isKinematic: {rb.isKinematic}" +
                     $"\n  - useGravity: {rb.useGravity}" +
                     $"\n  - interpolation: {rb.interpolation}" +
                     $"\n  - collisionDetection: {rb.collisionDetectionMode}" +
                     $"\n  - constraints: {rb.constraints}" +
                     $"\n  - velocity: {rb.linearVelocity}" +
                     $"\n  - angularVelocity: {rb.angularVelocity}");
        }
        
        // Check for issues and fix them
        bool hadIssues = false;
        
        if (rb.isKinematic)
        {
            Debug.LogWarning("[SpawnService] Player Rigidbody was kinematic! Fixing...");
            rb.isKinematic = false;
            hadIssues = true;
        }
        
        if (!rb.useGravity)
        {
            Debug.LogWarning("[SpawnService] Player Rigidbody had no gravity! Fixing...");
            rb.useGravity = true;
            hadIssues = true;
        }
        
        if (rb.collisionDetectionMode != CollisionDetectionMode.Continuous)
        {
            Debug.LogWarning($"[SpawnService] Player collision detection was {rb.collisionDetectionMode}! Fixing to Continuous...");
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            hadIssues = true;
        }
        
        if (rb.interpolation != RigidbodyInterpolation.Interpolate)
        {
            Debug.LogWarning($"[SpawnService] Player interpolation was {rb.interpolation}! Fixing to Interpolate...");
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            hadIssues = true;
        }
        
        // Check for correct constraints (FreezeRotation = 112)
        if (rb.constraints != RigidbodyConstraints.FreezeRotation)
        {
            Debug.LogWarning($"[SpawnService] Player constraints were {rb.constraints}! Fixing to FreezeRotation...");
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            hadIssues = true;
        }
        
        // Reset velocity
        if (rb.linearVelocity.magnitude > 0.01f || rb.angularVelocity.magnitude > 0.01f)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[SpawnService] Resetting player velocity");
            }
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // CRITICAL: Sync player colliders to match current scene's camera yaw
        // When persisting player across scenes, collider offsets may be configured for
        // a different camera orientation (e.g., from a camera zone in the previous scene)
        SyncPlayerCollidersToCurrentCamera();
        
        // Check colliders
        Collider[] colliders = playerObject.GetComponentsInChildren<Collider>();
        int enabledColliders = 0;
        int triggerColliders = 0;
        
        foreach (var col in colliders)
        {
            if (col.enabled)
            {
                enabledColliders++;
                if (col.isTrigger) triggerColliders++;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Player colliders: {colliders.Length} total, {enabledColliders} enabled, {triggerColliders} triggers");
            
            // Log each collider
            foreach (var col in colliders)
            {
                Debug.Log($"[SpawnService]   - {col.gameObject.name}: {col.GetType().Name}, enabled={col.enabled}, trigger={col.isTrigger}, layer={LayerMask.LayerToName(col.gameObject.layer)}");
            }
        }
        
        // Check layer
        int playerLayer = playerObject.layer;
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Player root layer: {LayerMask.LayerToName(playerLayer)} ({playerLayer})");
        }
        
        if (hadIssues)
        {
            Debug.LogWarning("[SpawnService] Fixed player physics issues - this may indicate a problem in your scene setup!");
        }
    }
    
    /// <summary>
    /// Sync player colliders and ground check to match the current scene's camera orientation.
    /// This is critical when the player persists across scenes and was previously in a camera zone
    /// with a different yaw - the collider offsets would be wrong for the new scene's camera.
    /// </summary>
    private void SyncPlayerCollidersToCurrentCamera()
    {
        TopDownCameraController3D cameraController = SceneCameraResolver.ResolveGameplayCameraController();
        SynchronizePlayerToCameraYaw(cameraController);
    }

    /// <summary>
    /// Authoritatively align all yaw-dependent player state with a scene camera.
    /// Safe to call after a camera-zone transition and before revealing a scene.
    /// </summary>
    public bool SynchronizePlayerToCameraYaw(TopDownCameraController3D cameraController)
    {
        playerObject = GetResolvedPlayerRoot();
        if (playerObject == null)
            return false;

        int cameraYawIndex = cameraController != null ? cameraController.YawIndex90 : 0;
        PlayerMovement3D playerMovement = playerObject.GetComponent<PlayerMovement3D>();
        if (playerMovement != null)
        {
            if (cameraController != null)
                playerMovement.SetCameraController(cameraController);

            playerMovement.UpdateColliderForCameraYaw(cameraYawIndex);
            playerMovement.UpdateGroundCheckForCameraYaw(cameraYawIndex);
        }

        MeshRendererPlayerVisual playerVisual = playerObject.GetComponentInChildren<MeshRendererPlayerVisual>(true);
        playerVisual?.UpdateVisualForCameraYaw(cameraYawIndex);

        PlayerShadowCaster playerShadow = playerObject.GetComponentInChildren<PlayerShadowCaster>(true);
        if (playerShadow != null)
        {
            if (cameraController != null)
                playerShadow.SetCameraController(cameraController);
            playerShadow.UpdateShadowForCameraYaw(cameraYawIndex);
            playerShadow.SynchronizeWithCameraYaw();
        }

        OcclusionSilhouette silhouette = playerObject.GetComponent<OcclusionSilhouette>();
        if (silhouette != null)
        {
            Camera presentationCamera = cameraController != null
                ? SceneCameraResolver.GetCamera(cameraController)
                : SceneCameraResolver.ResolveFor(playerObject);
            silhouette.SynchronizeWithCamera(presentationCamera, cameraController);
            silhouette.UpdateSilhouetteForCameraYaw(cameraYawIndex);
        }

        Camera billboardCamera = cameraController != null
            ? SceneCameraResolver.GetCamera(cameraController)
            : SceneCameraResolver.ResolveFor(playerObject);
        if (billboardCamera != null)
        {
            SpriteBillboard[] playerBillboards =
                playerObject.GetComponentsInChildren<SpriteBillboard>(true);
            for (int i = 0; i < playerBillboards.Length; i++)
                playerBillboards[i].SetTargetCamera(billboardCamera);
        }

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SpawnService] Synchronized player root '{playerObject.name}' to camera yaw index {cameraYawIndex}.");
        }

        return true;
    }
    
    private IEnumerator AutoSpawnCoroutine()
    {
        // Wait multiple frames for spawn points to register via OnEnable
        // OnEnable() is called before OnSceneLoaded, but we clear the dictionary in OnSceneLoaded,
        // so we need to wait for spawn points to re-register
        yield return null;
        yield return null; // Extra frame to ensure registration completes

        if (!ShouldCreatePlayerInScene(SceneManager.GetActiveScene().name))
        {
            if (enableDebugLogs || logAutoSpawnPipelineDiagnostics)
            {
                Debug.Log(
                    $"[SpawnService] Auto-spawn skipped for scene '{SceneManager.GetActiveScene().name}' (menu/bootstrap filter).");
            }
            yield break;
        }

        if (skipAutoSpawnOnce)
        {
            skipAutoSpawnOnce = false;
            if (enableDebugLogs || logAutoSpawnPipelineDiagnostics)
            {
                Debug.Log(
                    $"[SpawnService] Auto-spawn skipped (SkipAutoSpawnForNextSceneLoad). " +
                    $"Scene='{SceneManager.GetActiveScene().name}', lastSpawnId='{lastSpawnId}' (not applied).");
            }
            yield break;
        }
        
        // Determine which spawn ID to use
        string spawnIdToUse = "";
        
        if (!string.IsNullOrEmpty(lastSpawnId) && spawnPoints.ContainsKey(lastSpawnId))
        {
            // Use the last spawn ID if it exists in this scene
            spawnIdToUse = lastSpawnId;
        }
        else if (!string.IsNullOrEmpty(defaultSpawnId) && spawnPoints.ContainsKey(defaultSpawnId))
        {
            // Use default spawn ID if specified and exists
            spawnIdToUse = defaultSpawnId;
        }
        else if (spawnPoints.Count > 0)
        {
            // Use the first spawn point found
            foreach (var kvp in spawnPoints)
            {
                spawnIdToUse = kvp.Key;
                break;
            }
        }
        
        // Spawn player if we have a valid spawn ID
        if (!string.IsNullOrEmpty(spawnIdToUse))
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Auto-spawning player at '{spawnIdToUse}' on scene load");
            }
            SpawnPlayerAt(spawnIdToUse);
            
            // Apply facing direction after a short delay to allow camera zones to activate first
            // This ensures facing direction is relative to final camera orientation
            StartCoroutine(ApplyFacingDirectionAfterDelay(spawnIdToUse));
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"[SpawnService] No spawn points found in scene '{SceneManager.GetActiveScene().name}'. Player will remain at default position.");
        }
    }
    
    /// <summary>
    /// Returns the player root used by spawn/teleport logic, refreshing the reference if needed.
    /// When multiple objects share the player tag during a scene transition (e.g. scene instance + DontDestroyOnLoad),
    /// prefers the persisted <c>DontDestroyOnLoad</c> root so positioning matches the runtime player.
    /// </summary>
    public GameObject GetResolvedPlayerRoot()
    {
        RefreshPlayerReference();

        GameObject[] tagged = GameObject.FindGameObjectsWithTag(playerTag);
        if (tagged.Length > 1)
        {
            for (int i = 0; i < tagged.Length; i++)
            {
                GameObject candidate = GetPlayerRoot(tagged[i]);
                if (candidate == null)
                    continue;
                if (candidate.scene.name == "DontDestroyOnLoad")
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[SpawnService] Multiple '{playerTag}' roots ({tagged.Length}); using DontDestroyOnLoad instance '{candidate.name}'.");
                    }
                    playerObject = candidate;
                    return playerObject;
                }
            }
        }

        playerObject = GetPlayerRoot(playerObject);
        return playerObject;
    }

    private void RefreshPlayerReference(bool allowInstantiate = false)
    {
        // Ignore prefab-asset or unloaded-scene references serialized into the service prefab.
        if (playerObject != null && !playerObject.scene.IsValid())
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[SpawnService] Ignoring non-scene player reference '{playerObject.name}' and re-resolving at runtime.");
            }
            playerObject = null;
        }

        // Auto-find player if not set or if current reference is null/destroyed
        if (playerObject == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found == null)
            {
                PlayerMovement3D[] movements = FindObjectsByType<PlayerMovement3D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < movements.Length; i++)
                {
                    if (movements[i] == null)
                        continue;
                    found = GetPlayerRoot(movements[i].gameObject);
                    if (found != null)
                        break;
                }
            }

            if (found != null)
            {
                playerObject = GetPlayerRoot(found);
                
                // Make player persist across scenes if enabled
                if (persistPlayerAcrossScenes)
                {
                    DontDestroyOnLoad(playerObject);
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[SpawnService] Found player object: {playerObject.name}");
                }
            }
            else if (allowInstantiate && ShouldCreatePlayerInScene(SceneManager.GetActiveScene().name) && playerPrefab != null)
            {
                // Instantiate player from prefab if not found
                playerObject = Instantiate(playerPrefab);
                playerObject.name = playerPrefab.name; // Remove "(Clone)" suffix
                
                // Make player persist across scenes if enabled
                if (persistPlayerAcrossScenes)
                {
                    DontDestroyOnLoad(playerObject);
                }
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[SpawnService] Instantiated player from prefab: {playerObject.name}");
                }
            }
            else if (enableDebugLogs && allowInstantiate)
            {
                Debug.LogWarning($"[SpawnService] Player object with tag '{playerTag}' not found in scene and no player prefab assigned.");
            }
        }
        else if (playerObject != null && persistPlayerAcrossScenes)
        {
            // Ensure existing player persists across scenes
            DontDestroyOnLoad(playerObject);
        }
    }

    private bool ShouldCreatePlayerInScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return false;

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() &&
            string.Equals(activeScene.name, sceneName, StringComparison.Ordinal) &&
            IsCombatScene(activeScene))
        {
            return false;
        }

        if (IsCombatSceneName(sceneName))
            return false;

        if (skipPlayerCreateSceneNames == null)
            return true;

        for (int i = 0; i < skipPlayerCreateSceneNames.Length; i++)
        {
            string skipName = skipPlayerCreateSceneNames[i];
            if (string.IsNullOrEmpty(skipName))
                continue;

            if (string.Equals(sceneName, skipName, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
    
    /// <summary>
    /// Register a spawn point.
    /// </summary>
    public void RegisterSpawnPoint(SpawnPoint spawnPoint)
    {
        if (spawnPoint == null) return;
        
        string id = spawnPoint.SpawnId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning($"[SpawnService] SpawnPoint on {spawnPoint.gameObject.name} has empty spawnId!");
            return;
        }
        
        // Warn if duplicate ID is being registered
        if (spawnPoints.ContainsKey(id))
        {
            SpawnPoint existing = spawnPoints[id];
            Debug.LogWarning($"[SpawnService] Duplicate spawn point ID '{id}' detected! " +
                           $"Existing: {existing.gameObject.name} at {existing.Position}, " +
                           $"New: {spawnPoint.gameObject.name} at {spawnPoint.Position}. " +
                           $"The new one will replace the existing one.");
        }
        
        spawnPoints[id] = spawnPoint;
        
        if (enableDebugLogs)
        {
            Vector3 worldPos = spawnPoint.Position;
            Vector3 localPos = spawnPoint.transform.localPosition;
            string parentInfo = spawnPoint.transform.parent != null 
                ? $" (Parent: {spawnPoint.transform.parent.name}, Local: {localPos})" 
                : " (No parent)";
            
            Debug.Log($"[SpawnService] Registered spawn point: '{id}' at World: {worldPos}{parentInfo} (GameObject: {spawnPoint.gameObject.name}). Total registered: {spawnPoints.Count}");
        }
    }
    
    /// <summary>
    /// Unregister a spawn point.
    /// </summary>
    public void UnregisterSpawnPoint(SpawnPoint spawnPoint)
    {
        if (spawnPoint == null) return;
        
        string id = spawnPoint.SpawnId;
        if (spawnPoints.TryGetValue(id, out SpawnPoint registered) && registered == spawnPoint)
        {
            spawnPoints.Remove(id);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Unregistered spawn point: {id}");
            }
        }
    }

    /// <summary>
    /// Removes registrations owned by one unloaded scene without disturbing
    /// spawn points in adjacent additive overworld chunks.
    /// </summary>
    private void UnregisterSpawnPointsInScene(Scene scene)
    {
        if (!scene.IsValid())
            return;

        List<string> idsToRemove = new List<string>();
        foreach (KeyValuePair<string, SpawnPoint> pair in spawnPoints)
        {
            SpawnPoint spawnPoint = pair.Value;
            if (spawnPoint == null || spawnPoint.gameObject == null || spawnPoint.gameObject.scene == scene)
                idsToRemove.Add(pair.Key);
        }

        for (int i = 0; i < idsToRemove.Count; i++)
            spawnPoints.Remove(idsToRemove[i]);
    }

    private void UnregisterSpawnPointsOutsideScene(Scene keptScene)
    {
        List<string> idsToRemove = new List<string>();
        foreach (KeyValuePair<string, SpawnPoint> pair in spawnPoints)
        {
            SpawnPoint spawnPoint = pair.Value;
            if (spawnPoint == null || spawnPoint.gameObject == null ||
                (spawnPoint.gameObject.scene != keptScene && !IsPersistentScene(spawnPoint.gameObject.scene)))
                idsToRemove.Add(pair.Key);
        }

        for (int i = 0; i < idsToRemove.Count; i++)
            spawnPoints.Remove(idsToRemove[i]);
    }

    private void PruneInvalidSpawnPoints()
    {
        List<string> idsToRemove = new List<string>();
        foreach (KeyValuePair<string, SpawnPoint> pair in spawnPoints)
        {
            if (pair.Value == null || pair.Value.gameObject == null)
                idsToRemove.Add(pair.Key);
        }

        for (int i = 0; i < idsToRemove.Count; i++)
            spawnPoints.Remove(idsToRemove[i]);
    }

    private static bool IsPersistentScene(Scene scene)
    {
        return scene.IsValid() && string.Equals(scene.name, "DontDestroyOnLoad", StringComparison.Ordinal);
    }
    
    /// <summary>
    /// Get the spawn point nearest to a position. Optional prefix filter (e.g. "harbor_")
    /// limits to water-respawn points only.
    /// </summary>
    /// <param name="position">Position to measure distance from</param>
    /// <param name="idPrefix">If set, only consider spawn points whose ID starts with this prefix</param>
    /// <returns>Nearest SpawnPoint, or null if none match</returns>
    public SpawnPoint GetNearestSpawnPoint(Vector3 position, string idPrefix = null)
    {
        SpawnPoint nearest = null;
        float nearestSqDist = float.MaxValue;
        
        foreach (var kvp in spawnPoints)
        {
            if (kvp.Value == null || kvp.Value.gameObject == null) continue;
            if (!string.IsNullOrEmpty(idPrefix) && !kvp.Key.StartsWith(idPrefix)) continue;
            
            float sqDist = (kvp.Value.Position - position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest = kvp.Value;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get the closest authored spawn point in the active scene. Unlike
    /// <see cref="GetNearestSpawnPoint"/>, this never returns a persistent
    /// runtime point from DontDestroyOnLoad, which is important when restoring
    /// a player after an interrupted encounter.
    /// </summary>
    public SpawnPoint GetNearestSpawnPointInActiveScene(Vector3 position, string idPrefix = null)
    {
        return GetNearestSpawnPointInScene(position, SceneManager.GetActiveScene(), idPrefix);
    }

    /// <summary>
    /// Get the closest authored spawn point in a specific loaded scene.
    /// </summary>
    public SpawnPoint GetNearestSpawnPointInScene(Vector3 position, Scene scene, string idPrefix = null)
    {
        if (!scene.IsValid() || !scene.isLoaded || IsPersistentScene(scene))
            return null;

        SpawnPoint nearest = null;
        float nearestSqDist = float.MaxValue;

        foreach (var kvp in spawnPoints)
        {
            SpawnPoint candidate = kvp.Value;
            if (candidate == null || candidate.gameObject == null || candidate.gameObject.scene != scene)
                continue;
            if (!string.IsNullOrEmpty(idPrefix) && !kvp.Key.StartsWith(idPrefix))
                continue;

            float sqDist = (candidate.Position - position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest = candidate;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Get the closest authored spawn among every loaded gameplay scene. This
    /// includes restored additive overworld chunks, but excludes persistent
    /// runtime points such as <c>encounter_return</c>.
    /// </summary>
    public SpawnPoint GetNearestSpawnPointInLoadedScenes(Vector3 position, string idPrefix = null)
    {
        SpawnPoint nearest = null;
        float nearestSqDist = float.MaxValue;

        foreach (var kvp in spawnPoints)
        {
            SpawnPoint candidate = kvp.Value;
            if (candidate == null || candidate.gameObject == null)
                continue;

            Scene scene = candidate.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded || IsPersistentScene(scene))
                continue;
            if (!string.IsNullOrEmpty(idPrefix) && !kvp.Key.StartsWith(idPrefix))
                continue;

            float sqDist = (candidate.Position - position).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                nearest = candidate;
            }
        }

        return nearest;
    }
    
    /// <summary>
    /// Check if a position is inside a camera zone trigger and return the trigger.
    /// </summary>
    /// <param name="position">Position to check</param>
    /// <returns>CameraZoneTrigger if position is inside one, null otherwise</returns>
    public CameraZoneTrigger GetCameraZoneAtPosition(Vector3 position)
    {
        CameraZoneTrigger zone = CameraZoneOccupancy.FindTriggerContaining(position);
        if (zone != null && enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Found camera zone '{zone.gameObject.name}' at spawn position {position}");
        }

        return zone;
    }
    
    /// <summary>
    /// Spawn player at the specified spawn point ID.
    /// </summary>
    /// <param name="spawnId">ID of spawn point</param>
    /// <returns>True if spawn was successful</returns>
    public bool SpawnPlayerAt(string spawnId)
    {
        // Refresh player reference in case it was lost
        RefreshPlayerReference(allowInstantiate: true);
        
        if (playerObject == null)
        {
            Debug.LogError("[SpawnService] Player object not found! Cannot spawn.");
            return false;
        }
        
        if (string.IsNullOrEmpty(spawnId))
        {
            Debug.LogWarning("[SpawnService] Spawn ID is empty!");
            return false;
        }
        
        if (!spawnPoints.TryGetValue(spawnId, out SpawnPoint spawnPoint))
        {
            Debug.LogWarning($"[SpawnService] Spawn point '{spawnId}' not found! Registered spawn points: {string.Join(", ", spawnPoints.Keys)}");
            return false;
        }
        
        // Validate spawn point is still valid
        if (spawnPoint == null || spawnPoint.gameObject == null)
        {
            Debug.LogError($"[SpawnService] Spawn point '{spawnId}' GameObject is null or destroyed!");
            spawnPoints.Remove(spawnId); // Clean up invalid entry
            return false;
        }
        
        // Ensure spawn point is in the active scene
        if (spawnPoint.gameObject.scene != SceneManager.GetActiveScene())
        {
            Debug.LogWarning($"[SpawnService] Spawn point '{spawnId}' is not in the active scene! " +
                           $"Active scene: {SceneManager.GetActiveScene().name}, " +
                           $"Spawn point scene: {spawnPoint.gameObject.scene.name}");
        }
        
        // Get spawn position and rotation (read fresh at spawn time, not cached)
        // Read directly from transform to ensure we get the current position
        Vector3 spawnPosition = spawnPoint.transform.position;
        Quaternion spawnRotation = spawnPoint.Rotation;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Spawning player at '{spawnId}'. " +
                     $"Spawn point GameObject: {spawnPoint.gameObject.name}, " +
                     $"Spawn point position: {spawnPosition}, " +
                     $"Spawn point scene: {spawnPoint.gameObject.scene.name}, " +
                     $"Player current position: {playerObject.transform.position}, " +
                     $"Player scene: {playerObject.scene.name}, " +
                     $"Spawn point parent: {(spawnPoint.transform.parent != null ? spawnPoint.transform.parent.name : "None")}");
        }
        
        // IMPORTANT: Set transform.position DIRECTLY first for immediate effect
        // Rigidbody.MovePosition() only schedules the move for the next physics update,
        // so transform.position won't be updated until FixedUpdate runs.
        // We set transform directly to ensure immediate positioning.
        playerObject.transform.position = spawnPosition;
        playerObject.transform.rotation = spawnRotation;
        
        // Reset rigidbody velocity and sync physics
        Rigidbody rb = playerObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // Also call MovePosition to ensure physics engine is aware of the new position
            // This prevents physics from "snapping back" to the old position
            rb.MovePosition(spawnPosition);
            rb.MoveRotation(spawnRotation);
        }
        
        // Verify position after physics update to catch any issues
        StartCoroutine(VerifySpawnPosition(spawnPosition));
        
        // Refresh all scene transition triggers to recognize player's new position
        // This is critical when teleporting - trigger events may not fire properly
        StartCoroutine(RefreshSceneTransitionTriggersDelayed());
        StartCoroutine(RefreshCameraZoneOccupancyDelayed());
        
        // Refresh player's camera controller reference for camera-relative movement
        // This is critical when player persists across scenes (DontDestroyOnLoad)
        PlayerMovement3D playerMovement = playerObject.GetComponent<PlayerMovement3D>();
        if (playerMovement != null)
        {
            // Refresh all components after scene transition (including camera controller and landing dust spawner)
            playerMovement.RefreshAfterSceneTransition();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Refreshed player components after scene transition");
            }
        }
        
        // Restore prefab defaults for shadow and silhouette components
        // This ensures settings match the prefab configuration after scene transitions.
        // Camera zones can still override these values after restoration.
        RestorePlayerVisualComponentDefaults();
        
        // Refresh occlusion silhouette references after a small delay to ensure cameras are initialized
        // This is critical - the camera from the old scene is destroyed, so we need to find the new one
        StartCoroutine(RefreshOcclusionSilhouetteReferencesDelayed());
        
        // Store the spawn ID for next scene load
        lastSpawnId = spawnId;
        
        if (enableDebugLogs)
        {
            // Position should now be correct immediately since we set transform.position directly
            Vector3 actualPosition = playerObject.transform.position;
            float distance = Vector3.Distance(spawnPosition, actualPosition);
            Debug.Log($"[SpawnService] Spawned player at '{spawnId}'. Target: {spawnPosition}, Actual: {actualPosition}, Distance: {distance:F4}");
            
            if (distance > 0.01f)
            {
                Debug.LogWarning($"[SpawnService] WARNING: Player position differs from spawn point by {distance:F4} units immediately after spawn!");
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Verify and correct spawn position after physics update.
    /// </summary>
    private IEnumerator VerifySpawnPosition(Vector3 targetPosition)
    {
        // Wait for physics to update
        yield return new WaitForFixedUpdate();
        yield return null; // Wait one more frame
        
        if (playerObject != null)
        {
            Vector3 currentPosition = playerObject.transform.position;
            float distance = Vector3.Distance(currentPosition, targetPosition);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Post-physics verification: Player at {currentPosition}, Target: {targetPosition}, Distance: {distance:F4}");
            }
            
            // If player has moved significantly, correct it
            if (distance > 0.5f)
            {
                Debug.LogWarning(
                    $"[SpawnService] VerifySpawnPosition: correcting drift of {distance:F2}m toward spawn target. " +
                    $"This can override a post-battle teleport if SpawnPlayerAt ran after exact return. " +
                    $"From {currentPosition} to {targetPosition}");
                
                // Set transform.position directly first for immediate effect
                playerObject.transform.position = targetPosition;
                
                // Also update rigidbody if present
                Rigidbody rb = playerObject.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.MovePosition(targetPosition);
                }
            }
            else if (enableDebugLogs && distance > 0.01f)
            {
                Debug.Log($"[SpawnService] Minor position variance of {distance:F4} units (within tolerance)");
            }
        }
    }
    
    /// <summary>
    /// Refresh all scene transition triggers after a delay.
    /// This ensures triggers properly recognize the player's new position after teleporting.
    /// </summary>
    private IEnumerator RefreshSceneTransitionTriggersDelayed()
    {
        // Wait for physics to settle
        yield return new WaitForFixedUpdate();
        yield return null;
        
        // Find all scene transition triggers in the scene
        SceneTransitionTrigger[] allTriggers = FindObjectsOfType<SceneTransitionTrigger>();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Refreshing {allTriggers.Length} scene transition trigger(s)");
        }
        
        foreach (var trigger in allTriggers)
        {
            if (trigger != null)
            {
                trigger.ForceRefreshState();
            }
        }
    }

    /// <summary>
    /// Teleport does not fire camera-zone trigger enter. Sample live volumes
    /// from the player's new position after physics has caught up.
    /// </summary>
    private IEnumerator RefreshCameraZoneOccupancyDelayed()
    {
        yield return new WaitForFixedUpdate();
        yield return null;
        CameraZoneOccupancy.ResyncAllLiveZones();
    }
    
    /// <summary>
    /// Spawn player at the specified spawn point ID and return any camera zone at that location.
    /// </summary>
    /// <param name="spawnId">ID of spawn point</param>
    /// <param name="cameraZone">Output: CameraZoneTrigger if spawn point is inside one</param>
    /// <returns>True if spawn was successful</returns>
    public bool SpawnPlayerAt(string spawnId, out CameraZoneTrigger cameraZone)
    {
        cameraZone = null;
        
        bool success = SpawnPlayerAt(spawnId);
        
        if (success && playerObject != null)
        {
            // Check if spawn position is inside a camera zone
            cameraZone = GetCameraZoneAtPosition(playerObject.transform.position);
        }
        
        return success;
    }
    
    /// <summary>
    /// Wait for a spawn point to be registered, then spawn player there.
    /// Use this when spawning immediately after scene load to ensure spawn points are registered.
    /// </summary>
    /// <param name="spawnId">ID of spawn point</param>
    /// <param name="maxWaitFrames">Maximum frames to wait (default 10)</param>
    /// <returns>Coroutine</returns>
    public IEnumerator SpawnPlayerAtWhenReady(string spawnId, int maxWaitFrames = 10)
    {
        int framesWaited = 0;
        
        while (!spawnPoints.ContainsKey(spawnId) && framesWaited < maxWaitFrames)
        {
            yield return null;
            framesWaited++;
        }
        
        if (spawnPoints.ContainsKey(spawnId))
        {
            SpawnPlayerAt(spawnId);
        }
        else
        {
            Debug.LogError($"[SpawnService] Spawn point '{spawnId}' not registered after {maxWaitFrames} frames! Registered spawn points: {string.Join(", ", spawnPoints.Keys)}");
        }
    }
    
    /// <summary>
    /// Get spawn point by ID (without spawning).
    /// </summary>
    public SpawnPoint GetSpawnPoint(string spawnId)
    {
        spawnPoints.TryGetValue(spawnId, out SpawnPoint spawnPoint);
        return spawnPoint;
    }
    
    /// <summary>
    /// Check if a spawn point exists.
    /// </summary>
    public bool HasSpawnPoint(string spawnId)
    {
        return spawnPoints.ContainsKey(spawnId);
    }
    
    /// <summary>
    /// Set the player object reference.
    /// </summary>
    public void SetPlayerObject(GameObject player)
    {
        playerObject = player;
    }
    
    /// <summary>
    /// Set the spawn ID to use on next scene load (overrides auto-spawn logic).
    /// </summary>
    public void SetNextSpawnId(string spawnId)
    {
        lastSpawnId = spawnId;
    }
    
    /// <summary>
    /// Clear the stored spawn ID (will use default or first spawn point on next load).
    /// </summary>
    public void ClearStoredSpawnId()
    {
        lastSpawnId = "";
    }

    /// <summary>
    /// Call before loading a scene when player position will be set by another system
    /// (e.g. <see cref="PostBattleSceneReturnHandler"/> exact encounter return) instead of auto-spawn.
    /// Prevents <see cref="AutoSpawnCoroutine"/> from calling <see cref="SpawnPlayerAt"/> and starting
    /// <c>VerifySpawnPosition</c>, which can overwrite a later teleport back to <see cref="lastSpawnId"/>.
    /// </summary>
    public void SkipAutoSpawnForNextSceneLoad()
    {
        skipAutoSpawnOnce = true;
    }
    
    /// <summary>
    /// Coroutine to apply facing direction after a delay (for auto-spawn case).
    /// Waits for camera zones to potentially activate, then applies facing direction.
    /// </summary>
    private IEnumerator ApplyFacingDirectionAfterDelay(string spawnId)
    {
        // Wait a few frames to allow camera zones to activate if they're going to
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f); // Small delay to ensure camera zones have started their transitions
        
        // Apply facing direction
        ApplySpawnFacingDirection(spawnId);
    }
    
    /// <summary>
    /// Apply the facing direction from a spawn point to the player.
    /// Call this AFTER camera zone transitions complete (if any) to ensure facing direction
    /// is relative to the final camera orientation.
    /// </summary>
    /// <param name="spawnId">Spawn point ID</param>
    public void ApplySpawnFacingDirection(string spawnId)
    {
        if (string.IsNullOrEmpty(spawnId))
            return;
        
        if (!spawnPoints.TryGetValue(spawnId, out SpawnPoint spawnPoint))
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[SpawnService] Cannot apply facing direction - spawn point '{spawnId}' not found");
            }
            return;
        }
        
        if (spawnPoint.FacingDirection == SpawnPoint.SpawnFacingDirection.None)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Spawn point '{spawnId}' has no facing direction set (None), keeping current direction");
            }
            return;
        }
        
        if (playerObject == null)
        {
            RefreshPlayerReference(allowInstantiate: false);
            if (playerObject == null)
            {
                Debug.LogWarning("[SpawnService] Cannot apply facing direction - player object not found");
                return;
            }
        }
        
        // Get player animation controller
        var animController = playerObject.GetComponentInChildren<PlayerAnimationController3D_Mesh>();
        if (animController == null)
        {
            Debug.LogWarning("[SpawnService] Cannot apply facing direction - PlayerAnimationController3D_Mesh not found");
            return;
        }
        
        // Convert SpawnFacingDirection to MeshRendererPlayerVisual.Direction
        MeshRendererPlayerVisual.Direction direction;
        switch (spawnPoint.FacingDirection)
        {
            case SpawnPoint.SpawnFacingDirection.Up:
                direction = MeshRendererPlayerVisual.Direction.Up;
                break;
            case SpawnPoint.SpawnFacingDirection.Down:
                direction = MeshRendererPlayerVisual.Direction.Down;
                break;
            case SpawnPoint.SpawnFacingDirection.Left:
                direction = MeshRendererPlayerVisual.Direction.Left;
                break;
            case SpawnPoint.SpawnFacingDirection.Right:
                direction = MeshRendererPlayerVisual.Direction.Right;
                break;
            default:
                return; // None - already handled above
        }
        
        // Apply facing direction
        animController.ForceFaceDirection(direction);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SpawnService] Applied facing direction '{spawnPoint.FacingDirection}' from spawn point '{spawnId}'");
        }
    }
    
    /// <summary>
    /// Restore prefab default values for player visual components (shadow, silhouette).
    /// This ensures settings match the prefab configuration after scene transitions.
    /// Camera zones can still override these values after restoration.
    /// </summary>
    private void RestorePlayerVisualComponentDefaults()
    {
        if (playerObject == null)
            return;
        
        // Restore shadow caster defaults
        PlayerShadowCaster shadowCaster = playerObject.GetComponentInChildren<PlayerShadowCaster>();
        if (shadowCaster != null)
        {
            shadowCaster.RestorePrefabDefaults();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Restored shadow caster prefab defaults");
            }
        }
        
        // Restore occlusion silhouette defaults
        OcclusionSilhouette occlusionSilhouette = playerObject.GetComponent<OcclusionSilhouette>();
        if (occlusionSilhouette != null)
        {
            occlusionSilhouette.RestorePrefabDefaults();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Restored occlusion silhouette prefab defaults");
            }
        }
    }
    
    /// <summary>
    /// Refresh occlusion silhouette references after a delay to ensure cameras are initialized.
    /// This is critical when transitioning scenes - the old scene's camera is destroyed,
    /// so we need to find the new scene's camera.
    /// </summary>
    private IEnumerator RefreshOcclusionSilhouetteReferencesDelayed()
    {
        // Wait a few frames for cameras to initialize
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f); // Small delay to ensure cameras are ready
        
        if (playerObject == null)
            yield break;
        
        OcclusionSilhouette occlusionSilhouette = playerObject.GetComponent<OcclusionSilhouette>();
        if (occlusionSilhouette != null)
        {
            occlusionSilhouette.RefreshReferencesAfterSceneTransition();
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SpawnService] Refreshed occlusion silhouette references after delay");
            }
        }
    }
}
