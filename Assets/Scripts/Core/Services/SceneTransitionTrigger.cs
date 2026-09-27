using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Scene transition trigger for collider-based transitions.
/// Supports auto-transition on enter or interact-based transitions.
/// Includes buffer zone system to prevent immediate re-triggering after spawn.
/// 
/// Setup:
/// 1. Add this component to a trigger zone
/// 2. (Optional) Create child with SceneTransitionBuffer component for spawn safety
/// 3. Configure target scene and spawn ID
/// 
/// Buffer Zone Logic:
/// - Player spawns inside trigger → transition is NOT armed
/// - Player exits buffer zone → transition becomes armed
/// - Player enters trigger again → transition fires
/// This prevents immediate re-triggering when spawning inside the trigger area.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SceneTransitionTrigger : MonoBehaviour
{
    public enum TransitionMode
    {
        AutoOnEnter,    // Transition immediately when entering (doors, portals)
        RequireInteract // Require interact input (elevators, special doors)
    }
    
    [Header("Transition Settings")]
    [Tooltip("Target scene name (must match Build Settings)")]
    [SerializeField] private string targetSceneName = "";
    
    [Tooltip("Spawn point ID in target scene")]
    [SerializeField] private string spawnId = "";
    
    [Tooltip("How the transition should be triggered")]
    [SerializeField] private TransitionMode transitionMode = TransitionMode.AutoOnEnter;
    
    [Tooltip("Loading transition style")]
    [SerializeField] private TransitionStyle transitionStyle = TransitionStyle.QuickFade;

    [Tooltip("Use the style above instead of the destination rule from SceneLoadingConfigSO.")]
    [SerializeField] private bool overrideConfiguredStyle = false;
    
    [Header("Buffer Zone")]
    [Tooltip("Reference to buffer zone (auto-finds child with SceneTransitionBuffer if null)")]
    [SerializeField] private SceneTransitionBuffer bufferZone;
    
    [Tooltip("If true, player must exit buffer zone before transition is armed")]
    [SerializeField] private bool useBufferZone = true;
    
    [Header("Player Detection")]
    [Tooltip("Player tag to detect")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("Input (for RequireInteract mode)")]
    [Tooltip("Input Action Asset (auto-loads from Resources if null)")]
    [SerializeField] private InputActionAsset inputActions;
    
    [Tooltip("Action map name")]
    [SerializeField] private string actionMapName = "Player";
    
    [Tooltip("Interact action name")]
    [SerializeField] private string interactActionName = "Interact";
    
    [Header("Quest Gating")]
    [Tooltip("Quest IDs that block this trigger when active. If any are active, this component disables itself. Use for doors/transitions controlled by sequences (e.g. chainbound_intro).")]
    [SerializeField] private string[] blockingQuestIds = System.Array.Empty<string>();
    
    [Header("Safety Net")]
    [Tooltip("Enable periodic safety check for jump edge cases")]
    [SerializeField] private bool enableSafetyNet = true;
    
    [Tooltip("How often to run safety check (seconds)")]
    [SerializeField] private float safetyCheckInterval = 0.15f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // State
    private Collider triggerCollider;
    private bool playerInTrigger = false;
    private bool playerInBuffer = false;
    private bool isArmed = false; // True when player has exited buffer (safe to transition)
    private bool isTransitioning = false;
    
    // Input
    private InputActionMap gameplayMap;
    private InputAction interactAction;
    
    // Safety net
    private float lastSafetyCheckTime = 0f;
    
    /// <summary>Is the trigger currently armed (ready to transition)?</summary>
    public bool IsArmed => isArmed;
    
    /// <summary>Is the player currently in the trigger zone?</summary>
    public bool IsPlayerInTrigger => playerInTrigger;
    
    /// <summary>Is the player currently in the buffer zone?</summary>
    public bool IsPlayerInBuffer => playerInBuffer;
    
    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
        else
        {
            Debug.LogError($"[SceneTransitionTrigger] No Collider found on {gameObject.name}!");
        }
        
        // Auto-find buffer zone
        if (bufferZone == null && useBufferZone)
        {
            bufferZone = GetComponentInChildren<SceneTransitionBuffer>();
        }
        
        // Initialize buffer zone if found
        if (bufferZone != null)
        {
            bufferZone.Initialize(this, playerTag);
        }
        
        // Setup input if using interact mode
        if (transitionMode == TransitionMode.RequireInteract)
        {
            SetupInput();
        }
        
        // Start disarmed if using buffer zone (player may spawn inside)
        if (useBufferZone && bufferZone != null)
        {
            isArmed = false;
        }
        else
        {
            // No buffer zone - always armed
            isArmed = true;
        }
    }
    
    private void SetupInput()
    {
        if (inputActions == null)
        {
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (inputActions == null)
            {
                Debug.LogWarning($"[SceneTransitionTrigger] InputActionAsset not found! Interact mode will not work. Assign InputActions or place InputSystem_Actions in Resources.");
                return;
            }
        }
        
        gameplayMap = inputActions.FindActionMap(actionMapName);
        if (gameplayMap == null)
        {
            Debug.LogWarning($"[SceneTransitionTrigger] Action map '{actionMapName}' not found!");
            return;
        }
        
        interactAction = gameplayMap.FindAction(interactActionName);
        if (interactAction == null)
        {
            Debug.LogWarning($"[SceneTransitionTrigger] Action '{interactActionName}' not found!");
            return;
        }
    }
    
    private void Start()
    {
        // Quest gating: disable if any blocking quest is active (e.g. sequence-controlled transitions)
        if (blockingQuestIds != null && blockingQuestIds.Length > 0)
        {
            QuestService questService = QuestService.Instance;
            if (questService != null)
            {
                foreach (string questId in blockingQuestIds)
                {
                    if (!string.IsNullOrEmpty(questId) && questService.IsQuestActive(questId))
                    {
                        enabled = false;
                        if (triggerCollider != null)
                        {
                            triggerCollider.enabled = false;
                        }
                        if (enableDebugLogs)
                        {
                            Debug.Log($"[SceneTransitionTrigger] Disabled (blocking quest active): {questId} on {gameObject.name} — component and collider disabled");
                        }
                        return;
                    }
                }
            }
        }
        
        // Reset transition state on scene start (fresh scene load)
        isTransitioning = false;
        
        // Check for persisted player that may already be inside trigger/buffer
        StartCoroutine(CheckInitialPlayerPosition());
    }
    
    /// <summary>
    /// Check if player is already inside trigger/buffer on scene load.
    /// This handles persisted players from DontDestroyOnLoad.
    /// </summary>
    private IEnumerator CheckInitialPlayerPosition()
    {
        // Wait for physics to settle and player to be positioned
        yield return new WaitForFixedUpdate();
        yield return null;
        yield return null; // Extra frame for spawn positioning
        
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] No player found during initial check: {gameObject.name}");
            }
            yield break;
        }
        
        if (enableDebugLogs)
        {
            string sceneInfo = playerObj.scene.name == "DontDestroyOnLoad" ? "DontDestroyOnLoad (persisted)" : playerObj.scene.name;
            Debug.Log($"[SceneTransitionTrigger] Initial player check - Player: {playerObj.name} in {sceneInfo}");
        }
        
        // Check if player is in trigger zone
        bool inTrigger = IsAnyPlayerColliderInZone(triggerCollider);
        
        // Check if player is in buffer zone
        bool inBuffer = false;
        if (useBufferZone && bufferZone != null)
        {
            Collider bufferCol = bufferZone.GetComponent<Collider>();
            if (bufferCol != null)
            {
                inBuffer = IsAnyPlayerColliderInZone(bufferCol);
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Initial position check on {gameObject.name}:" +
                     $"\n  - Player in trigger: {inTrigger}" +
                     $"\n  - Player in buffer: {inBuffer}" +
                     $"\n  - Is armed: {isArmed}" +
                     $"\n  - Use buffer zone: {useBufferZone}");
        }
        
        // Update state based on player position
        playerInTrigger = inTrigger;
        playerInBuffer = inBuffer;
        
        // Spawned inside the door volume (or its buffer) must stay disarmed until
        // the player leaves. Do not treat "in trigger but not in buffer" as armed —
        // CheckInitial often runs before SceneLoader teleports the player, then a
        // later teleport would AutoOnEnter the reverse scene.
        if (inTrigger || (inBuffer && useBufferZone))
        {
            isArmed = false;
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] Player spawned in door volume - trigger disarmed: {gameObject.name}");
            }
        }
        else
        {
            isArmed = true;
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] Player outside all zones - trigger armed: {gameObject.name}");
            }
        }
        
        // Log detailed collider state for debugging
        if (enableDebugLogs)
        {
            LogDetailedColliderState(playerObj);
        }
    }
    
    /// <summary>
    /// Log detailed collider state for debugging physics issues.
    /// </summary>
    private void LogDetailedColliderState(GameObject playerObj)
    {
        Rigidbody rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Debug.Log($"[SceneTransitionTrigger] Player Rigidbody state:" +
                     $"\n  - isKinematic: {rb.isKinematic}" +
                     $"\n  - detectCollisions: {rb.detectCollisions}" +
                     $"\n  - velocity: {rb.linearVelocity}" +
                     $"\n  - position: {rb.position}");
        }
        
        Collider[] playerColliders = playerObj.GetComponentsInChildren<Collider>();
        Debug.Log($"[SceneTransitionTrigger] Player has {playerColliders.Length} colliders:");
        foreach (var col in playerColliders)
        {
            Debug.Log($"  - {col.gameObject.name}: {col.GetType().Name}, enabled={col.enabled}, trigger={col.isTrigger}, layer={LayerMask.LayerToName(col.gameObject.layer)}");
        }
        
        // Check our trigger collider
        Debug.Log($"[SceneTransitionTrigger] Our trigger collider: {triggerCollider.GetType().Name}, enabled={triggerCollider.enabled}, layer={LayerMask.LayerToName(gameObject.layer)}");
    }
    
    private void OnEnable()
    {
        if (transitionMode == TransitionMode.RequireInteract && interactAction != null)
        {
            interactAction.Enable();
            interactAction.performed += OnInteractPressed;
        }
    }
    
    private void OnDisable()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteractPressed;
            interactAction.Disable();
        }
    }
    
    private void Update()
    {
        // Safety net for jump edge cases
        if (enableSafetyNet && useBufferZone && bufferZone != null)
        {
            if (Time.time - lastSafetyCheckTime >= safetyCheckInterval)
            {
                lastSafetyCheckTime = Time.time;
                PerformSafetyCheck();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        
        playerInTrigger = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Player entered trigger: {gameObject.name} (armed={isArmed}, collider={other.name})");
        }
        
        // Inbound loads spawn the player inside the opposite door while SceneLoader
        // is still covering/warming. Requesting the reverse load then logs Busy and
        // can leave the SceneLoader hold (All + freeze) on the player.
        if (IsSceneLoaderBusy())
        {
            isArmed = false;
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] Ignoring enter during inbound load: {gameObject.name}");
            }
            return;
        }

        // Check if we should transition
        if (transitionMode == TransitionMode.AutoOnEnter && isArmed && !isTransitioning)
        {
            TriggerTransition();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other)) return;
        
        // Don't immediately set playerInTrigger to false
        // Let safety check handle it (for jump edge cases)
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Player exited trigger: {gameObject.name} (collider={other.name})");
        }
        
        // Delayed check to confirm player has truly exited
        StartCoroutine(DelayedExitCheck());
    }
    
    private IEnumerator DelayedExitCheck()
    {
        yield return new WaitForSeconds(0.05f);
        
        // Verify player is truly outside trigger
        if (!IsAnyPlayerColliderInZone(triggerCollider))
        {
            playerInTrigger = false;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] Confirmed player exited trigger: {gameObject.name}");
            }
        }
    }
    
    /// <summary>
    /// Called by SceneTransitionBuffer when player enters buffer zone.
    /// </summary>
    public void OnBufferZoneEnter()
    {
        playerInBuffer = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Player entered buffer zone: {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Called by SceneTransitionBuffer when player exits buffer zone.
    /// This arms the transition trigger.
    /// </summary>
    public void OnBufferZoneExit()
    {
        playerInBuffer = false;
        
        // Arm the trigger - player has left the safe zone
        if (!isArmed)
        {
            isArmed = true;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] Trigger armed! Player exited buffer zone: {gameObject.name}");
            }
        }
    }
    
    private void OnInteractPressed(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (!playerInTrigger) return;
        if (!isArmed) return;
        if (isTransitioning) return;
        if (IsSceneLoaderBusy()) return;
        
        // Check InputLockService
        InputLockService inputLock = InputLockService.Instance;
        if (inputLock != null && inputLock.IsInteractionLocked)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[SceneTransitionTrigger] Interact blocked by InputLockService");
            }
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Interact pressed - transitioning: {gameObject.name}");
        }
        
        TriggerTransition();
    }
    
    /// <summary>
    /// Trigger the scene transition.
    /// </summary>
    private void TriggerTransition()
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning($"[SceneTransitionTrigger] Target scene name is empty on {gameObject.name}!");
            return;
        }

        if (IsSceneLoaderBusy())
        {
            isArmed = false;
            isTransitioning = false;
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Transitioning to: {targetSceneName} (spawn: {spawnId})");
        }
        
        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader != null)
        {
            isTransitioning = sceneLoader.RequestTransition(new SceneTransitionRequest(
                targetSceneName,
                spawnId,
                overrideConfiguredStyle ? transitionStyle : (TransitionStyle?)null,
                sourceId: $"SceneTransitionTrigger.{gameObject.scene.name}.{gameObject.name}",
                completed: result =>
                {
                    if (!result.Succeeded)
                        isTransitioning = false;
                }));
        }
        else
        {
            Debug.LogError("[SceneTransitionTrigger] SceneLoader not found!");
            isTransitioning = false;
        }
    }

    private static bool IsSceneLoaderBusy()
    {
        SceneLoader loader = SceneLoader.Instance;
        return loader != null && loader.IsTransitioning;
    }
    
    /// <summary>
    /// Force disarm the trigger (for testing or special cases).
    /// </summary>
    public void Disarm()
    {
        isArmed = false;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Trigger disarmed: {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Force arm the trigger (for testing or special cases).
    /// </summary>
    public void Arm()
    {
        isArmed = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] Trigger armed: {gameObject.name}");
        }
    }
    
    /// <summary>
    /// Force refresh trigger state based on current player position.
    /// Call this after player teleports/spawns to ensure trigger state is correct.
    /// </summary>
    public void ForceRefreshState()
    {
        // Check actual player position
        bool actuallyInTrigger = IsAnyPlayerColliderInZone(triggerCollider);
        
        bool actuallyInBuffer = false;
        if (useBufferZone && bufferZone != null)
        {
            Collider bufferCol = bufferZone.GetComponent<Collider>();
            if (bufferCol != null)
            {
                actuallyInBuffer = IsAnyPlayerColliderInZone(bufferCol);
            }
        }
        
        // Update state
        playerInTrigger = actuallyInTrigger;
        playerInBuffer = actuallyInBuffer;
        
        // Determine armed state
        if (actuallyInBuffer)
        {
            // Player is in buffer zone - disarm to prevent immediate re-trigger
            isArmed = false;
        }
        else if (!actuallyInTrigger)
        {
            // Player is completely outside - arm the trigger
            isArmed = true;
        }
        // If player is in trigger but not buffer, keep current armed state
        
        // Reset transitioning flag
        isTransitioning = false;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[SceneTransitionTrigger] ForceRefreshState on {gameObject.name}:" +
                     $"\n  - In trigger: {playerInTrigger}" +
                     $"\n  - In buffer: {playerInBuffer}" +
                     $"\n  - Is armed: {isArmed}");
        }
    }
    
    /// <summary>
    /// Check if a collider belongs to the player (handles both capsule and falling colliders).
    /// </summary>
    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        
        // Direct tag check
        if (col.CompareTag(playerTag))
            return true;
        
        // Check parent GameObjects (for falling collider which is a child)
        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(playerTag))
                return true;
            parent = parent.parent;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if any player collider is inside a zone collider.
    /// </summary>
    private bool IsAnyPlayerColliderInZone(Collider zoneCol)
    {
        if (zoneCol == null) return false;
        
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj == null) return false;
        
        Collider[] playerColliders = playerObj.GetComponentsInChildren<Collider>();
        if (playerColliders == null || playerColliders.Length == 0) return false;
        
        foreach (Collider playerCol in playerColliders)
        {
            if (playerCol == null || !playerCol.enabled) continue;
            if (playerCol.isTrigger) continue; // Skip trigger colliders
            
            // Bounds intersection check
            if (zoneCol.bounds.Intersects(playerCol.bounds))
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Safety net: Verify player state matches actual position.
    /// Catches edge cases where trigger events might be missed during jumps.
    /// </summary>
    private void PerformSafetyCheck()
    {
        if (bufferZone == null) return;
        
        bool actuallyInTrigger = IsAnyPlayerColliderInZone(triggerCollider);
        Collider bufferCol = bufferZone.GetComponent<Collider>();
        bool actuallyInBuffer = bufferCol != null && IsAnyPlayerColliderInZone(bufferCol);
        
        // If player is truly outside both zones but we think they're inside
        if (!actuallyInBuffer && !actuallyInTrigger)
        {
            if (playerInTrigger || playerInBuffer)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[SceneTransitionTrigger] Safety net: Correcting state (was: trigger={playerInTrigger}, buffer={playerInBuffer})");
                }
                
                playerInTrigger = false;
                playerInBuffer = false;
                
                // Arm the trigger since player has exited
                if (!isArmed)
                {
                    isArmed = true;
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[SceneTransitionTrigger] Safety net: Trigger armed after exit correction");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Get the trigger collider (for buffer zone reference).
    /// </summary>
    public Collider GetTriggerCollider()
    {
        return triggerCollider;
    }
    
    #region Gizmos
    
    private void OnDrawGizmos()
    {
        if (triggerCollider == null)
            triggerCollider = GetComponent<Collider>();
        
        if (triggerCollider != null)
        {
            // Green for transition trigger
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
            
            if (triggerCollider is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (triggerCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius);
                Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
                Gizmos.DrawWireSphere(transform.position + sphere.center, sphere.radius);
            }
            else if (triggerCollider is CapsuleCollider capsule)
            {
                // Approximate capsule gizmo
                Vector3 center = transform.position + capsule.center;
                float halfHeight = Mathf.Max(0, capsule.height * 0.5f - capsule.radius);
                Vector3 top = center + Vector3.up * halfHeight;
                Vector3 bottom = center - Vector3.up * halfHeight;
                Gizmos.DrawWireSphere(top, capsule.radius);
                Gizmos.DrawWireSphere(bottom, capsule.radius);
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw arrow pointing to target scene
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            Gizmos.color = Color.cyan;
            Vector3 start = transform.position + Vector3.up * 2f;
            Vector3 direction = transform.forward * 2f;
            Gizmos.DrawLine(start, start + direction);
            Gizmos.DrawSphere(start + direction, 0.15f);
        }
    }
    
    #endregion
}

