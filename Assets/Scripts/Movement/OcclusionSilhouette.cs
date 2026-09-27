using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows a silhouette of the player when significantly occluded by 3D objects.
/// Uses multi-point sampling to determine occlusion percentage, avoiding false
/// positives from partial occluders like fences.
/// 
/// Setup:
/// 1. Create a child GameObject under PlayerRoot called "Silhouette"
/// 2. Add a SpriteRenderer with the same sprite as the main character
/// 3. Apply a SilhouetteOccluded material (ZTest Greater, tinted color)
/// 4. Disable the Silhouette GameObject by default
/// 5. Attach this script to PlayerRoot
/// </summary>
[DefaultExecutionOrder(-40)]
public class OcclusionSilhouette : MonoBehaviour
{
    public readonly struct SuppressionHandle : IDisposable
    {
        private readonly OcclusionSilhouette owner;
        private readonly int tokenId;

        internal SuppressionHandle(OcclusionSilhouette owner, int tokenId)
        {
            this.owner = owner;
            this.tokenId = tokenId;
        }

        public bool IsValid => owner != null && tokenId > 0;

        public void Dispose()
        {
            if (owner == null || tokenId <= 0)
                return;

            owner.ReleaseSuppression(tokenId);
        }
    }

    [Header("References")]
    [Tooltip("The MeshRenderer used for the silhouette effect.")]
    [SerializeField] private MeshRenderer silhouetteRenderer;
    
    [Tooltip("The main mesh renderer to sync with (optional, for texture syncing).")]
    [SerializeField] private MeshRenderer mainMeshRenderer;
    
    [Tooltip("Camera to use for occlusion raycast. Auto-finds main camera if null.")]
    [SerializeField] private Camera targetCamera;
    
    [Tooltip("Camera controller for syncing position with camera yaw")]
    [SerializeField] private TopDownCameraController3D cameraController;
    
    [Header("Silhouette Position Offsets")]
    [Tooltip("Y position offset for the silhouette mesh. Used only when no player visual is assigned.")]
    [SerializeField] private float yPositionOffset = 0f;
    
    [Tooltip("If true, uses per-camera-yaw XZ offsets. If false, uses single XZ offset.")]
    [SerializeField] private bool usePerYawXZOffsets = false;
    
    [Tooltip("Single XZ offset for silhouette position (used when usePerYawXZOffsets is false)")]
    [SerializeField] private Vector2 xzPositionOffset = Vector2.zero;
    
    [Tooltip("XZ offsets for silhouette position per camera yaw. Index: 0=0°, 1=90°, 2=180°, 3=270°")]
    [SerializeField] private Vector2[] xzPositionOffsetsPerYaw = new Vector2[]
    {
        new Vector2(0f, 0f),   // 0°
        new Vector2(0f, 0f),   // 90°
        new Vector2(0f, 0f),   // 180°
        new Vector2(0f, 0f)    // 270°
    };
    
    [Tooltip("Use visual offset for raycast origin (matches visual sprite position)")]
    [SerializeField] private bool useVisualOffsetForRaycast = true;

    [Header("Pitch Y Offset")]
    [Tooltip("Fallback only: additive Y lift when no player visual is assigned. When a visual exists, the silhouette copies that posed transform instead.")]
    [SerializeField] private ExplorationVisualPitchYOffset.PitchYOffsetSettings pitchYOffsetSettings =
        ExplorationVisualPitchYOffset.PitchYOffsetSettings.CreateDefault(0.5f);
    
    // Current camera yaw index for per-yaw offsets
    private int currentCameraYawIndex = 0;
    
    [Header("Occlusion Detection")]
    [Tooltip("Layers that trigger full silhouette (buildings, walls, large objects).")]
    [SerializeField] private LayerMask occlusionLayers = -1;
    
    [Tooltip("Layers that should NOT trigger silhouette (fences, small props, foliage).")]
    [SerializeField] private LayerMask ignoreLayers;
    
    [Header("Multi-Point Sampling")]
    [Tooltip("Use multiple sample points for more accurate occlusion detection.")]
    [SerializeField] private bool useMultiPointSampling = true;
    
    [Tooltip("Sample points relative to player (0,0 = center). Spread across sprite height.")]
    [SerializeField] private Vector3[] sampleOffsets = new Vector3[]
    {
        new Vector3(0f, 0.0f, 0f),  // Feet
        new Vector3(0f, 0.3f, 0f),  // Lower body
        new Vector3(0f, 0.6f, 0f),  // Torso
        new Vector3(0f, 0.9f, 0f),  // Head
    };
    
    [Tooltip("Minimum percentage of sample points that must be occluded (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float minOcclusionThreshold = 0.5f;
    
    [Header("Simple Mode (if multi-point disabled)")]
    [Tooltip("Single raycast offset from player position.")]
    [SerializeField] private Vector3 raycastOffset = new Vector3(0f, 0.5f, 0f);
    
    [Tooltip("Additional margin to push the ray start back from camera.")]
    [SerializeField] private float raycastMargin = 0.1f;
    
    [Header("Silhouette Appearance")]
    [Tooltip("Color tint for the silhouette (applies if material supports it).")]
    [SerializeField] private Color silhouetteColor = new Color(0.2f, 0.1f, 0.3f, 0.5f);
    
    [Tooltip("Sync silhouette texture with main visual (for animated characters). REQUIRED for MeshRenderer setup.")]
    [SerializeField] private bool syncTextureWithMain = true; // Enabled by default for MeshRenderer
    
    [Header("Performance")]
    [Tooltip("How often to check for occlusion (seconds). Lower = more responsive but higher cost.")]
    [SerializeField] private float updateInterval = 0.033f; // ~30 Hz
    
    [Tooltip("Radius for ray thickness (0 = thin ray, >0 = sphere cast).")]
    [SerializeField] private float raycastRadius = 0.05f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool drawDebugRays = false;

    private float lastCheckTime = 0f;
    private bool wasOccluded = false;
    private MaterialPropertyBlock propertyBlock;
    private float currentOcclusionPercent = 0f;
    private int nextSuppressionTokenId = 1;
    private readonly HashSet<int> suppressionTokens = new HashSet<int>();
    private readonly Dictionary<int, string> suppressionReasons = new Dictionary<int, string>();
    
    // Prefab default values (stored on first initialization for restoration)
    private bool hasStoredPrefabDefaults = false;
    private float prefabDefault_yPositionOffset;
    private bool prefabDefault_usePerYawXZOffsets;
    private Vector2 prefabDefault_xzPositionOffset;
    private Vector2[] prefabDefault_xzPositionOffsetsPerYaw;
    private bool prefabDefault_useVisualOffsetForRaycast;
    private LayerMask prefabDefault_occlusionLayers;
    private LayerMask prefabDefault_ignoreLayers;
    private bool prefabDefault_useMultiPointSampling;
    private Vector3[] prefabDefault_sampleOffsets;
    private float prefabDefault_minOcclusionThreshold;
    private Vector3 prefabDefault_raycastOffset;
    private float prefabDefault_raycastMargin;
    private Color prefabDefault_silhouetteColor;
    private bool prefabDefault_syncTextureWithMain;
    private float prefabDefault_updateInterval;
    private float prefabDefault_raycastRadius;

    private void Awake()
    {
        RefreshCameraReferences(forceRefresh: false);
        
        // Auto-find main mesh renderer if not assigned (for texture syncing)
        if (mainMeshRenderer == null && syncTextureWithMain)
        {
            // Find mesh renderer in children, excluding the silhouette
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                if (mr != silhouetteRenderer)
                {
                    mainMeshRenderer = mr;
                    break;
                }
            }
        }
        
        // Ensure offset arrays are initialized
        if (xzPositionOffsetsPerYaw == null || xzPositionOffsetsPerYaw.Length < 4)
        {
            xzPositionOffsetsPerYaw = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            };
        }
        
        // Sync mesh from main renderer to silhouette renderer
        SyncMeshFromMain();
        
        // Initialize property block for color tinting
        propertyBlock = new MaterialPropertyBlock();
        
        // Apply initial color to silhouette
        if (silhouetteRenderer != null)
        {
            ApplySilhouetteColor();
            // Don't disable MeshRenderer here - SetOccluded will handle it
            // GameObject should stay active, but MeshRenderer component will be enabled/disabled
        }
        
        if (silhouetteRenderer == null)
        {
            Debug.LogWarning($"[OcclusionSilhouette] No silhouette renderer assigned on {gameObject.name}. Silhouette effect will not work.");
        }
        
        // Ensure we have valid sample offsets
        if (sampleOffsets == null || sampleOffsets.Length == 0)
        {
            sampleOffsets = new Vector3[] { new Vector3(0f, 0.5f, 0f) };
        }
        
        // Store prefab defaults for restoration after scene transitions
        StorePrefabDefaults();
    }
    
    /// <summary>
    /// Store current values as prefab defaults (called once in Awake).
    /// These values will be restored when transitioning between scenes.
    /// </summary>
    private void StorePrefabDefaults()
    {
        if (hasStoredPrefabDefaults)
            return; // Only store once
        
        prefabDefault_yPositionOffset = yPositionOffset;
        prefabDefault_usePerYawXZOffsets = usePerYawXZOffsets;
        prefabDefault_xzPositionOffset = xzPositionOffset;
        prefabDefault_useVisualOffsetForRaycast = useVisualOffsetForRaycast;
        prefabDefault_occlusionLayers = occlusionLayers;
        prefabDefault_ignoreLayers = ignoreLayers;
        prefabDefault_useMultiPointSampling = useMultiPointSampling;
        prefabDefault_minOcclusionThreshold = minOcclusionThreshold;
        prefabDefault_raycastOffset = raycastOffset;
        prefabDefault_raycastMargin = raycastMargin;
        prefabDefault_silhouetteColor = silhouetteColor;
        prefabDefault_syncTextureWithMain = syncTextureWithMain;
        prefabDefault_updateInterval = updateInterval;
        prefabDefault_raycastRadius = raycastRadius;
        
        // Deep copy the arrays
        if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > 0)
        {
            prefabDefault_xzPositionOffsetsPerYaw = new Vector2[xzPositionOffsetsPerYaw.Length];
            System.Array.Copy(xzPositionOffsetsPerYaw, prefabDefault_xzPositionOffsetsPerYaw, xzPositionOffsetsPerYaw.Length);
        }
        else
        {
            prefabDefault_xzPositionOffsetsPerYaw = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f),
                new Vector2(0f, 0f)
            };
        }
        
        if (sampleOffsets != null && sampleOffsets.Length > 0)
        {
            prefabDefault_sampleOffsets = new Vector3[sampleOffsets.Length];
            System.Array.Copy(sampleOffsets, prefabDefault_sampleOffsets, sampleOffsets.Length);
        }
        else
        {
            prefabDefault_sampleOffsets = new Vector3[] { new Vector3(0f, 0.5f, 0f) };
        }
        
        hasStoredPrefabDefaults = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[OcclusionSilhouette] Stored prefab defaults for restoration");
        }
    }
    
    /// <summary>
    /// Restore prefab default values. Called after scene transitions to ensure
    /// settings match the prefab configuration. Camera zones can still override
    /// these values after restoration.
    /// </summary>
    public void RestorePrefabDefaults()
    {
        if (!hasStoredPrefabDefaults)
        {
            // If defaults weren't stored (shouldn't happen, but safety check),
            // store them now from current values
            StorePrefabDefaults();
        }
        
        yPositionOffset = prefabDefault_yPositionOffset;
        usePerYawXZOffsets = prefabDefault_usePerYawXZOffsets;
        xzPositionOffset = prefabDefault_xzPositionOffset;
        useVisualOffsetForRaycast = prefabDefault_useVisualOffsetForRaycast;
        occlusionLayers = prefabDefault_occlusionLayers;
        ignoreLayers = prefabDefault_ignoreLayers;
        useMultiPointSampling = prefabDefault_useMultiPointSampling;
        minOcclusionThreshold = prefabDefault_minOcclusionThreshold;
        raycastOffset = prefabDefault_raycastOffset;
        raycastMargin = prefabDefault_raycastMargin;
        silhouetteColor = prefabDefault_silhouetteColor;
        syncTextureWithMain = prefabDefault_syncTextureWithMain;
        updateInterval = prefabDefault_updateInterval;
        raycastRadius = prefabDefault_raycastRadius;
        
        // Deep copy the arrays
        if (prefabDefault_xzPositionOffsetsPerYaw != null && prefabDefault_xzPositionOffsetsPerYaw.Length > 0)
        {
            xzPositionOffsetsPerYaw = new Vector2[prefabDefault_xzPositionOffsetsPerYaw.Length];
            System.Array.Copy(prefabDefault_xzPositionOffsetsPerYaw, xzPositionOffsetsPerYaw, prefabDefault_xzPositionOffsetsPerYaw.Length);
        }
        
        if (prefabDefault_sampleOffsets != null && prefabDefault_sampleOffsets.Length > 0)
        {
            sampleOffsets = new Vector3[prefabDefault_sampleOffsets.Length];
            System.Array.Copy(prefabDefault_sampleOffsets, sampleOffsets, prefabDefault_sampleOffsets.Length);
        }
        
        // Reapply color
        ApplySilhouetteColor();
        
        // Update position immediately
        UpdateSilhouettePosition();
        
        // CRITICAL: Refresh references after scene transition
        // Camera and renderer references may be invalid after scene change
        RefreshReferencesAfterSceneTransition();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[OcclusionSilhouette] Restored prefab defaults after scene transition");
        }
    }
    
    /// <summary>
    /// Refresh camera and renderer references after scene transition.
    /// This is critical when the player persists across scenes - old scene's
    /// camera is destroyed, so we need to find the new scene's camera.
    /// </summary>
    public void RefreshReferencesAfterSceneTransition()
    {
        RefreshCameraReferences(forceRefresh: true);
        
        // Validate silhouette renderer (should persist, but verify it's still valid)
        if (silhouetteRenderer == null)
        {
            // Try to find it in children
            silhouetteRenderer = GetComponentInChildren<MeshRenderer>();
            
            if (enableDebugLogs)
            {
                if (silhouetteRenderer != null)
                {
                    Debug.Log($"[OcclusionSilhouette] Re-found silhouette renderer: {silhouetteRenderer.name}");
                }
                else
                {
                    Debug.LogWarning($"[OcclusionSilhouette] Silhouette renderer not found! Occlusion effect will not work.");
                }
            }
        }
        
        // Refresh main mesh renderer reference (for texture syncing)
        if (mainMeshRenderer == null && syncTextureWithMain)
        {
            // Find mesh renderer in children, excluding the silhouette
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                if (mr != silhouetteRenderer && mr.gameObject.name.Contains("Visual"))
                {
                    mainMeshRenderer = mr;
                    break;
                }
            }
            
            if (enableDebugLogs)
            {
                if (mainMeshRenderer != null)
                {
                    Debug.Log($"[OcclusionSilhouette] Re-found main mesh renderer: {mainMeshRenderer.name}");
                }
            }
        }
        
        // Re-initialize property block if needed
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
        
        // Re-apply color and sync mesh
        if (silhouetteRenderer != null)
        {
            ApplySilhouetteColor();
            SyncMeshFromMain();
        }
        
        // Reset occlusion state to force a fresh check
        wasOccluded = false;
        currentOcclusionPercent = 0f;
        lastCheckTime = 0f; // Force immediate check on next LateUpdate
        
        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.enabled = false; // Start hidden
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[OcclusionSilhouette] References refreshed. Camera: {(targetCamera != null ? targetCamera.name : "NULL")}, " +
                     $"Silhouette: {(silhouetteRenderer != null ? silhouetteRenderer.name : "NULL")}, " +
                     $"Main: {(mainMeshRenderer != null ? mainMeshRenderer.name : "NULL")}");
        }
    }

    private void RefreshCameraReferences(bool forceRefresh)
    {
        if (forceRefresh || !SceneCameraResolver.IsUsableFor(targetCamera, gameObject))
            targetCamera = SceneCameraResolver.ResolveFor(gameObject);

        TopDownCameraController3D resolvedController = SceneCameraResolver.GetController(targetCamera);

        if (forceRefresh || cameraController == null ||
            !cameraController.gameObject.scene.isLoaded ||
            (resolvedController != null && cameraController != resolvedController))
        {
            cameraController = resolvedController;
        }

        if (!enableDebugLogs)
            return;

        if (targetCamera != null)
        {
            Debug.Log(
                $"[OcclusionSilhouette] Bound camera '{targetCamera.name}' and controller " +
                $"'{(cameraController != null ? cameraController.name : "NULL")}'.");
        }
        else
        {
            Debug.LogWarning(
                $"[OcclusionSilhouette] No authoritative scene camera found for '{gameObject.name}'.");
        }
    }
    
    private void Start()
    {
        // Sync mesh after all Awake() calls complete (Visual's mesh should be created by now)
        SyncMeshFromMain();
    }

    private void LateUpdate()
    {
        // Safety check: Ensure we have valid references (critical after scene transitions)
        // If camera or renderer is null/invalid, try to refresh references
        if (!SceneCameraResolver.IsUsableFor(targetCamera, gameObject) || silhouetteRenderer == null)
        {
            // Only refresh occasionally to avoid performance hit
            if (Time.frameCount % 60 == 0) // Check every 60 frames (~1 second at 60fps)
            {
                RefreshReferencesAfterSceneTransition();
            }
            
            // Can't check occlusion without valid references
            return;
        }
        
        // Update silhouette position based on offsets
        UpdateSilhouettePosition();

        if (IsSuppressed)
        {
            ForceSilhouetteOff();
            return;
        }
        
        // Throttle updates for performance
        if (Time.time - lastCheckTime < updateInterval)
            return;
        
        lastCheckTime = Time.time;
        
        CheckOcclusion();
        
        // Sync mesh from main renderer (in case it's created at runtime)
        SyncMeshFromMain();
        
        // Sync texture if enabled (mesh renderer version)
        if (syncTextureWithMain && silhouetteRenderer != null && mainMeshRenderer != null)
        {
            // Copy material property block from main to silhouette
            MaterialPropertyBlock mainBlock = new MaterialPropertyBlock();
            mainMeshRenderer.GetPropertyBlock(mainBlock);
            
            MaterialPropertyBlock silhouetteBlock = new MaterialPropertyBlock();
            silhouetteRenderer.GetPropertyBlock(silhouetteBlock);
            
            // Copy main texture and UV transform
            if (mainBlock.HasTexture("_MainTex"))
            {
                silhouetteBlock.SetTexture("_MainTex", mainBlock.GetTexture("_MainTex"));
                
                // Copy UV transform (_MainTex_ST) for sprite atlas support
                if (mainBlock.HasVector("_MainTex_ST"))
                {
                    silhouetteBlock.SetVector("_MainTex_ST", mainBlock.GetVector("_MainTex_ST"));
                }
                
                // Apply color tint
                silhouetteBlock.SetColor("_Color", silhouetteColor);
                
                silhouetteRenderer.SetPropertyBlock(silhouetteBlock);
            }
        }
        else if (syncTextureWithMain && silhouetteRenderer != null && mainMeshRenderer == null)
        {
            // Try to auto-find main mesh renderer
            if (mainMeshRenderer == null)
            {
                MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
                foreach (var mr in renderers)
                {
                    if (mr != silhouetteRenderer && mr.gameObject.name.Contains("Visual"))
                    {
                        mainMeshRenderer = mr;
                        break;
                    }
                }
            }
        }
    }

    private void CheckOcclusion()
    {
        if (targetCamera == null || silhouetteRenderer == null)
            return;
        
        if (useMultiPointSampling)
        {
            CheckOcclusionMultiPoint();
        }
        else
        {
            CheckOcclusionSinglePoint();
        }
    }

    /// <summary>
    /// Check occlusion using multiple sample points across the sprite.
    /// Only shows silhouette if enough points are occluded.
    /// </summary>
    private void CheckOcclusionMultiPoint()
    {
        Vector3 cameraPosition = targetCamera.transform.position;
        int occludedCount = 0;
        int validSamples = 0;
        
        // Combined layer mask: occlusion layers minus ignore layers
        LayerMask effectiveMask = occlusionLayers & ~ignoreLayers;
        
        // Get visual offset for raycast origin (to match where the sprite actually is)
        Vector3 visualOffset = GetVisualOffsetForRaycast();
        
        foreach (Vector3 offset in sampleOffsets)
        {
            Vector3 samplePoint = transform.position + offset + visualOffset;
            Vector3 direction = samplePoint - cameraPosition;
            float distance = direction.magnitude - raycastMargin;
            
            if (distance <= 0f)
                continue;
            
            validSamples++;
            direction.Normalize();
            
            bool hitOccluder = false;
            
            if (raycastRadius > 0f)
            {
                // SphereCast for more stable detection
                if (Physics.SphereCast(
                    cameraPosition + direction * raycastMargin,
                    raycastRadius,
                    direction,
                    out RaycastHit hit,
                    distance,
                    effectiveMask,
                    QueryTriggerInteraction.Ignore))
                {
                    // Make sure we're not hitting the player itself
                    if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                    {
                        hitOccluder = true;
                    }
                }
            }
            else
            {
                // Thin raycast
                if (Physics.Raycast(
                    cameraPosition + direction * raycastMargin,
                    direction,
                    out RaycastHit hit,
                    distance,
                    effectiveMask,
                    QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                    {
                        hitOccluder = true;
                    }
                }
            }
            
            if (hitOccluder)
            {
                occludedCount++;
            }
            
            // Debug visualization
            if (drawDebugRays)
            {
                Debug.DrawLine(cameraPosition, samplePoint, hitOccluder ? Color.red : Color.green);
            }
        }
        
        // Calculate occlusion percentage
        if (validSamples > 0)
        {
            currentOcclusionPercent = (float)occludedCount / validSamples;
        }
        else
        {
            currentOcclusionPercent = 0f;
        }
        
        // Only show silhouette if occlusion exceeds threshold
        bool isOccluded = currentOcclusionPercent >= minOcclusionThreshold;
        
        SetOccluded(isOccluded);
        
        if (enableDebugLogs && Time.frameCount % 30 == 0)
        {
            Debug.Log($"[OcclusionSilhouette] Occlusion: {currentOcclusionPercent:P0} ({occludedCount}/{validSamples}), Threshold: {minOcclusionThreshold:P0}, Showing: {isOccluded}");
        }
    }

    /// <summary>
    /// Check occlusion using a single sample point (legacy mode).
    /// </summary>
    private void CheckOcclusionSinglePoint()
    {
        // Get visual offset for raycast origin (to match where the sprite actually is)
        Vector3 visualOffset = GetVisualOffsetForRaycast();
        Vector3 playerPoint = transform.position + raycastOffset + visualOffset;
        Vector3 cameraPosition = targetCamera.transform.position;
        Vector3 direction = playerPoint - cameraPosition;
        float distance = direction.magnitude - raycastMargin;
        
        if (distance <= 0f)
        {
            SetOccluded(false);
            return;
        }
        
        direction.Normalize();
        bool isOccluded = false;
        
        // Combined layer mask
        LayerMask effectiveMask = occlusionLayers & ~ignoreLayers;
        
        if (raycastRadius > 0f)
        {
            if (Physics.SphereCast(
                cameraPosition + direction * raycastMargin,
                raycastRadius,
                direction,
                out RaycastHit hit,
                distance,
                effectiveMask,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    isOccluded = true;
                }
            }
        }
        else
        {
            if (Physics.Raycast(
                cameraPosition + direction * raycastMargin,
                direction,
                out RaycastHit hit,
                distance,
                effectiveMask,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.transform != transform && !hit.transform.IsChildOf(transform))
                {
                    isOccluded = true;
                }
            }
        }
        
        SetOccluded(isOccluded);
        
        if (drawDebugRays)
        {
            Debug.DrawLine(cameraPosition, playerPoint, isOccluded ? Color.red : Color.green);
        }
    }

    private void SetOccluded(bool occluded)
    {
        if (IsSuppressed)
        {
            ForceSilhouetteOff();
            return;
        }

        if (occluded == wasOccluded)
            return;
        
        wasOccluded = occluded;
        
        if (silhouetteRenderer != null)
        {
            // Enable/disable the MeshRenderer component (not the GameObject)
            // GameObject stays active so mesh syncing and other operations can continue
            silhouetteRenderer.enabled = occluded;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[OcclusionSilhouette] {gameObject.name} is {(occluded ? "OCCLUDED - showing silhouette" : "VISIBLE - hiding silhouette")}");
            }
        }
    }

    private void ApplySilhouetteColor()
    {
        if (silhouetteRenderer == null)
            return;
        
        silhouetteRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", silhouetteColor);
        silhouetteRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// Set the silhouette color at runtime.
    /// </summary>
    public void SetSilhouetteColor(Color color)
    {
        silhouetteColor = color;
        ApplySilhouetteColor();
    }

    /// <summary>
    /// Set the occlusion layers at runtime.
    /// </summary>
    public void SetOcclusionLayers(LayerMask layers)
    {
        occlusionLayers = layers;
    }

    /// <summary>
    /// Set the ignore layers at runtime.
    /// </summary>
    public void SetIgnoreLayers(LayerMask layers)
    {
        ignoreLayers = layers;
    }

    /// <summary>
    /// Set the minimum occlusion threshold (0-1).
    /// </summary>
    public void SetOcclusionThreshold(float threshold)
    {
        minOcclusionThreshold = Mathf.Clamp01(threshold);
    }

    /// <summary>
    /// Check if player is currently occluded.
    /// </summary>
    public bool IsOccluded => wasOccluded;

    /// <summary>
    /// Get current occlusion percentage (0-1).
    /// </summary>
    public float OcclusionPercent => currentOcclusionPercent;

    /// <summary>
    /// Temporarily suppress silhouette rendering. Dispose the returned handle to resume normal behavior.
    /// </summary>
    public SuppressionHandle AcquireSuppression(string reason = null)
    {
        int tokenId = nextSuppressionTokenId++;
        suppressionTokens.Add(tokenId);
        suppressionReasons[tokenId] = reason ?? string.Empty;
        ForceSilhouetteOff();

        if (enableDebugLogs)
        {
            Debug.Log($"[OcclusionSilhouette] Suppression acquired ({reason ?? "no reason"}). Active={suppressionTokens.Count}");
        }

        return new SuppressionHandle(this, tokenId);
    }

    private void ReleaseSuppression(int tokenId)
    {
        if (!suppressionTokens.Remove(tokenId))
            return;

        suppressionReasons.Remove(tokenId);

        if (suppressionTokens.Count == 0)
        {
            lastCheckTime = 0f;
            wasOccluded = false;
            currentOcclusionPercent = 0f;
            ForceSilhouetteOff();
        }

        if (enableDebugLogs)
        {
            Debug.Log($"[OcclusionSilhouette] Suppression released. Active={suppressionTokens.Count}");
        }
    }

    private void ForceSilhouetteOff()
    {
        wasOccluded = false;
        currentOcclusionPercent = 0f;

        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.enabled = false;
        }
    }

    public bool IsSuppressed => suppressionTokens.Count > 0;
    public int ActiveSuppressionCount => suppressionTokens.Count;
    
    // ============================================
    // POSITION OFFSET SYSTEM
    // ============================================
    
    /// <summary>
    /// Match the posed player visual when one exists. Offset/pitch fields are a
    /// fallback for setups with no visual (tests, incomplete prefabs).
    /// </summary>
    private void UpdateSilhouettePosition()
    {
        if (silhouetteRenderer == null)
            return;

        Transform visual = ResolveVisualPoseTransform();
        if (visual != null)
        {
            silhouetteRenderer.transform.position = visual.position;
            return;
        }
        
        Vector2 xzOffset = GetCurrentXZOffset();
        float effectiveY = yPositionOffset + GetPitchYOffsetLift();
        silhouetteRenderer.transform.localPosition = new Vector3(xzOffset.x, effectiveY, xzOffset.y);
    }

    private Transform ResolveVisualPoseTransform()
    {
        if (mainMeshRenderer != null)
            return mainMeshRenderer.transform;

        MeshRendererPlayerVisual visual = GetComponentInChildren<MeshRendererPlayerVisual>(true);
        if (visual == null)
            return null;

        mainMeshRenderer = visual.GetComponent<MeshRenderer>();
        return visual.transform;
    }
    
    /// <summary>
    /// Get the current XZ offset based on settings.
    /// </summary>
    private Vector2 GetCurrentXZOffset()
    {
        if (usePerYawXZOffsets)
        {
            if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > currentCameraYawIndex)
            {
                return xzPositionOffsetsPerYaw[currentCameraYawIndex];
            }
        }
        
        return xzPositionOffset;
    }
    
    /// <summary>
    /// Get the visual offset for raycast origin.
    /// This ensures raycasts match where the sprite actually is.
    /// </summary>
    private Vector3 GetVisualOffsetForRaycast()
    {
        if (!useVisualOffsetForRaycast)
            return Vector3.zero;

        Transform visual = ResolveVisualPoseTransform();
        if (visual != null)
            return visual.position - transform.position;
        
        Vector2 xzOffset = GetCurrentXZOffset();
        float effectiveY = yPositionOffset + GetPitchYOffsetLift();
        return new Vector3(xzOffset.x, effectiveY, xzOffset.y);
    }

    private float GetPitchYOffsetLift()
    {
        return ExplorationVisualPitchYOffset.EvaluateLiftForSubject(
            pitchYOffsetSettings,
            transform.position,
            enableDebugLogs,
            nameof(OcclusionSilhouette));
    }
    
    /// <summary>
    /// Update silhouette position for a specific camera yaw index.
    /// Called by PlayerMovement3D when camera rotates.
    /// </summary>
    /// <param name="yawIndex90">Camera yaw index (0-3)</param>
    public void UpdateSilhouetteForCameraYaw(int yawIndex90)
    {
        // Normalize yaw index to 0-3
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        currentCameraYawIndex = yawIndex90;
        UpdateSilhouettePosition();
        
        if (enableDebugLogs)
        {
            Vector2 xzOffset = GetCurrentXZOffset();
            Debug.Log($"[OcclusionSilhouette] Updated silhouette for yaw index {yawIndex90} (visual follow={(ResolveVisualPoseTransform() != null)})");
        }
    }
    
    /// <summary>
    /// Get current camera yaw index.
    /// </summary>
    public int CurrentCameraYawIndex => currentCameraYawIndex;

    /// <summary>
    /// Bind the silhouette's raycasts, yaw-dependent offset, and child
    /// billboard to one authoritative scene camera.
    /// </summary>
    public void SynchronizeWithCamera(Camera camera, TopDownCameraController3D controller = null)
    {
        targetCamera = camera != null ? camera : SceneCameraResolver.ResolveFor(gameObject);
        cameraController = controller != null
            ? controller
            : targetCamera != null
                ? targetCamera.GetComponent<TopDownCameraController3D>()
                : null;

        if (cameraController != null)
            UpdateSilhouetteForCameraYaw(cameraController.YawIndex90);
        else
            UpdateSilhouettePosition();

        SpriteBillboard billboard = silhouetteRenderer != null
            ? silhouetteRenderer.GetComponent<SpriteBillboard>()
            : null;
        if (billboard != null && targetCamera != null)
            billboard.SetTargetCamera(targetCamera);
    }
    
    /// <summary>
    /// Set the Y position offset.
    /// </summary>
    public void SetYPositionOffset(float offset)
    {
        yPositionOffset = offset;
    }
    
    /// <summary>
    /// Get the Y position offset.
    /// </summary>
    public float GetYPositionOffset() => yPositionOffset;
    
    /// <summary>
    /// Set the single XZ position offset.
    /// </summary>
    public void SetXZPositionOffset(Vector2 offset)
    {
        xzPositionOffset = offset;
    }
    
    /// <summary>
    /// Get the single XZ position offset.
    /// </summary>
    public Vector2 GetXZPositionOffset() => xzPositionOffset;
    
    /// <summary>
    /// Set XZ offset for a specific camera yaw index.
    /// </summary>
    public void SetXZOffsetForYaw(int yawIndex, Vector2 offset)
    {
        yawIndex = yawIndex % 4;
        if (yawIndex < 0) yawIndex += 4;
        
        if (xzPositionOffsetsPerYaw == null || xzPositionOffsetsPerYaw.Length < 4)
        {
            xzPositionOffsetsPerYaw = new Vector2[4];
        }
        
        xzPositionOffsetsPerYaw[yawIndex] = offset;
    }
    
    /// <summary>
    /// Get XZ offset for a specific camera yaw index.
    /// </summary>
    public Vector2 GetXZOffsetForYaw(int yawIndex)
    {
        yawIndex = yawIndex % 4;
        if (yawIndex < 0) yawIndex += 4;
        
        if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > yawIndex)
        {
            return xzPositionOffsetsPerYaw[yawIndex];
        }
        
        return Vector2.zero;
    }
    
    /// <summary>
    /// Enable or disable per-yaw XZ offsets.
    /// </summary>
    public void SetUsePerYawXZOffsets(bool enable)
    {
        usePerYawXZOffsets = enable;
    }
    
    /// <summary>
    /// Check if using per-yaw XZ offsets.
    /// </summary>
    public bool IsUsingPerYawXZOffsets => usePerYawXZOffsets;
    
    /// <summary>
    /// Enable or disable visual offset for raycast.
    /// </summary>
    public void SetUseVisualOffsetForRaycast(bool enable)
    {
        useVisualOffsetForRaycast = enable;
    }
    
    /// <summary>
    /// Check if using visual offset for raycast.
    /// </summary>
    public bool IsUsingVisualOffsetForRaycast => useVisualOffsetForRaycast;

    /// <summary>
    /// Sync mesh from main MeshRenderer to silhouette MeshRenderer.
    /// This ensures the silhouette uses the same quad mesh as the visual.
    /// </summary>
    private void SyncMeshFromMain()
    {
        if (silhouetteRenderer == null)
            return;
        
        MeshFilter silhouetteFilter = silhouetteRenderer.GetComponent<MeshFilter>();
        if (silhouetteFilter == null)
            return;
        
        // If silhouette already has a mesh, don't override (user may have set it manually)
        if (silhouetteFilter.sharedMesh != null)
            return;
        
        // Try to get mesh from main renderer
        if (mainMeshRenderer != null)
        {
            MeshFilter mainFilter = mainMeshRenderer.GetComponent<MeshFilter>();
            if (mainFilter != null && mainFilter.sharedMesh != null)
            {
                silhouetteFilter.sharedMesh = mainFilter.sharedMesh;
                if (enableDebugLogs)
                {
                    Debug.Log($"[OcclusionSilhouette] Synced mesh '{mainFilter.sharedMesh.name}' from Visual to Silhouette");
                }
                return;
            }
        }
        
        // Auto-find main mesh renderer if not assigned
        if (mainMeshRenderer == null)
        {
            MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                if (mr != silhouetteRenderer)
                {
                    MeshFilter filter = mr.GetComponent<MeshFilter>();
                    if (filter != null && filter.sharedMesh != null)
                    {
                        silhouetteFilter.sharedMesh = filter.sharedMesh;
                        mainMeshRenderer = mr; // Cache for future use
                        if (enableDebugLogs)
                        {
                            Debug.Log($"[OcclusionSilhouette] Auto-synced mesh '{filter.sharedMesh.name}' from Visual to Silhouette");
                        }
                        return;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Manually set silhouette renderer reference.
    /// </summary>
    public void SetSilhouetteRenderer(MeshRenderer renderer)
    {
        silhouetteRenderer = renderer;
        if (silhouetteRenderer != null)
        {
            SyncMeshFromMain(); // Sync mesh when setting renderer
            ApplySilhouetteColor();
            silhouetteRenderer.enabled = wasOccluded;
        }
    }

    private void OnDisable()
    {
        // Hide silhouette when script is disabled
        ForceSilhouetteOff();
    }
    
    private void OnEnable()
    {
        // Restore silhouette state when script is re-enabled
        if (silhouetteRenderer != null)
        {
            silhouetteRenderer.enabled = !IsSuppressed && wasOccluded;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!useMultiPointSampling || sampleOffsets == null) return;
        
        Gizmos.color = Color.yellow;
        foreach (Vector3 offset in sampleOffsets)
        {
            Gizmos.DrawWireSphere(transform.position + offset, 0.05f);
        }
    }
#endif
}
