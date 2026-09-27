using UnityEngine;

/// <summary>
/// Separate shadow GameObject that syncs mesh and texture from Visual GameObject.
/// Stays flat (no billboard rotation) and casts sprite-shaped shadows.
/// 
/// IMPORTANT: Unity's shadow system automatically projects shadows based on directional light angle.
/// This GameObject stays flat, but Unity projects the shadow onto surfaces based on light direction.
/// The shadow will naturally align with your directional light - no manual rotation needed!
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class PlayerShadowCaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Visual GameObject's MeshRenderer to sync from")]
    [SerializeField] private MeshRenderer visualMeshRenderer;
    
    [Tooltip("The Visual GameObject's MeshRendererPlayerVisual component (optional, for better syncing)")]
    [SerializeField] private MeshRendererPlayerVisual visualPlayerVisual;
    
    [Tooltip("Camera controller for syncing shadow rotation with camera yaw")]
    [SerializeField] private TopDownCameraController3D cameraController;
    
    [Header("Shadow Position Offsets")]
    [Tooltip("Y position offset (height above ground). Set to 0.01-0.05 to prevent Z-fighting.")]
    [SerializeField] private float yPositionOffset = 0.01f;
    
    [Tooltip("If true, uses per-camera-yaw XZ offsets. If false, uses single XZ offset.")]
    [SerializeField] private bool usePerYawXZOffsets = false;
    
    [Tooltip("Single XZ offset for shadow position (used when usePerYawXZOffsets is false)")]
    [SerializeField] private Vector2 xzPositionOffset = new Vector2(0f, -0.75f);
    
    [Tooltip("XZ offsets for shadow position per camera yaw. Index: 0=0°, 1=90°, 2=180°, 3=270°")]
    [SerializeField] private Vector2[] xzPositionOffsetsPerYaw = new Vector2[]
    {
        new Vector2(0f, -0.75f),   // 0°
        new Vector2(-0.75f, 0f),   // 90°
        new Vector2(0f, 0.75f),    // 180°
        new Vector2(0.75f, 0f)     // 270°
    };
    
    [Header("Dynamic Ground Alignment (Slopes)")]
    [Tooltip("If true, shadow Y position is determined by raycasting to find the actual ground surface (works correctly on slopes)")]
    [SerializeField] private bool useGroundRaycast = true;
    
    [Tooltip("Layers considered as ground for shadow placement raycast")]
    [SerializeField] private LayerMask shadowGroundLayers = ~0; // Default: everything
    
    [Tooltip("Maximum distance to raycast downward for ground detection")]
    [SerializeField] private float groundRaycastDistance = 5f;
    
    [Tooltip("Height above player to start the raycast (ensures we're above any geometry)")]
    [SerializeField] private float raycastStartHeight = 2f;
    
    [Tooltip("Small offset above detected ground surface to prevent z-fighting (0.01-0.05 recommended)")]
    [SerializeField] private float groundSurfaceOffset = 0.02f;
    
    [Tooltip("If true, smoothly interpolates Y position to avoid jitter on uneven surfaces")]
    [SerializeField] private bool smoothYPosition = true;
    
    [Tooltip("Speed of Y position smoothing (higher = faster, more responsive)")]
    [SerializeField] private float yPositionSmoothSpeed = 15f;
    
    // Legacy field names (for backwards compatibility in inspector)
    [HideInInspector] [SerializeField] private float groundOffset = 0.01f;
    [HideInInspector] [SerializeField] private float zOffset = -0.75f;
    
    // Current camera yaw index for per-yaw offsets
    private int currentCameraYawIndex = 0;
    
    [Tooltip("Material for shadow casting (should use Sprites/SpriteShadowCaster shader - dedicated shadow-only shader)")]
    [SerializeField] private Material shadowMaterial;
    
    [Tooltip("Alpha cutoff for shadow shape (0.5 = standard, lower = more pixels cast shadow, higher = fewer pixels).")]
    [SerializeField] private float alphaCutoff = 0.5f;
    
    [Tooltip("Scale factor for shadow size (1.0 = same size as visual, 1.2 = 20% larger, etc.)")]
    [SerializeField] private float shadowScale = 1.0f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private MeshRenderer shadowMeshRenderer;
    private MeshFilter shadowMeshFilter;
    private MaterialPropertyBlock visualPropertyBlock;
    private Vector3 originalLocalScale;
    
    // Cached material instance - created ONCE in Awake, NEVER recreated
    private Material cachedMaterialInstance;
    private bool isInitialized = false;
    
    // Camera yaw tracking
    private float currentCameraYaw = 0f;
    
    // Ground raycast Y position tracking (for smoothing)
    private float currentShadowY = 0f;
    private bool hasInitializedShadowY = false;
    
    // Prefab default values (stored on first initialization for restoration)
    private bool hasStoredPrefabDefaults = false;
    private bool prefabDefault_useGroundRaycast;
    private LayerMask prefabDefault_shadowGroundLayers;
    private float prefabDefault_groundRaycastDistance;
    private float prefabDefault_raycastStartHeight;
    private float prefabDefault_groundSurfaceOffset;
    private bool prefabDefault_smoothYPosition;
    private float prefabDefault_yPositionSmoothSpeed;
    private float prefabDefault_yPositionOffset;
    private bool prefabDefault_usePerYawXZOffsets;
    private Vector2[] prefabDefault_xzPositionOffsetsPerYaw;
    
    // Flag to check if we've migrated from legacy offsets
    private bool hasAppliedLegacyMigration = false;
    
    // Cached property existence flags (set once in Awake)
    private bool hasMainTex;
    private bool hasBaseMap;
    private bool hasMainTex_ST;
    private bool hasBaseMap_ST;
    private bool hasCutoff;
    private bool hasAlphaClipThreshold;

    private void Awake()
    {
        shadowMeshRenderer = GetComponent<MeshRenderer>();
        shadowMeshFilter = GetComponent<MeshFilter>();
        visualPropertyBlock = new MaterialPropertyBlock();
        originalLocalScale = transform.localScale;
        
        // Auto-find visual renderer if not assigned
        if (visualMeshRenderer == null)
        {
            Transform parent = transform.parent;
            if (parent != null)
            {
                Transform visualTransform = parent.Find("Visual");
                if (visualTransform != null)
                {
                    visualMeshRenderer = visualTransform.GetComponent<MeshRenderer>();
                    visualPlayerVisual = visualTransform.GetComponent<MeshRendererPlayerVisual>();
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[PlayerShadowCaster] Auto-found Visual GameObject: {visualTransform.name}");
                    }
                }
            }
        }
        
        // Auto-find camera controller if not assigned. The player can persist
        // across scenes, so this reference is also refreshed by PlayerMovement3D
        // whenever the destination scene camera becomes authoritative.
        if (cameraController == null)
            RefreshCameraController();
        
        // Migrate from legacy offset fields if needed
        MigrateLegacyOffsets();
        
        // Ensure offset arrays are initialized
        if (xzPositionOffsetsPerYaw == null || xzPositionOffsetsPerYaw.Length < 4)
        {
            xzPositionOffsetsPerYaw = new Vector2[]
            {
                new Vector2(0f, -0.75f),
                new Vector2(-0.75f, 0f),
                new Vector2(0f, 0.75f),
                new Vector2(0.75f, 0f)
            };
        }
        
        // Create shadow material with correct shader if needed
        CreateShadowMaterial();
        
        // Configure shadow renderer
        // Use ShadowCastingMode.On (not ShadowsOnly) because URP needs a forward pass
        // to properly recognize the shader. Our shader's forward pass renders fully transparent.
        shadowMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        shadowMeshRenderer.receiveShadows = false;
        
        // Position and rotation - use new offset system
        transform.localRotation = Quaternion.identity;
        UpdateShadowPosition();
        
        if (shadowScale != 1.0f)
        {
            transform.localScale = originalLocalScale * shadowScale;
        }
        
        // Store prefab defaults for restoration after scene transitions
        StorePrefabDefaults();
        
        isInitialized = true;
    }
    
    /// <summary>
    /// Store current values as prefab defaults (called once in Awake).
    /// These values will be restored when transitioning between scenes.
    /// </summary>
    private void StorePrefabDefaults()
    {
        if (hasStoredPrefabDefaults)
            return; // Only store once
        
        prefabDefault_useGroundRaycast = useGroundRaycast;
        prefabDefault_shadowGroundLayers = shadowGroundLayers;
        prefabDefault_groundRaycastDistance = groundRaycastDistance;
        prefabDefault_raycastStartHeight = raycastStartHeight;
        prefabDefault_groundSurfaceOffset = groundSurfaceOffset;
        prefabDefault_smoothYPosition = smoothYPosition;
        prefabDefault_yPositionSmoothSpeed = yPositionSmoothSpeed;
        prefabDefault_yPositionOffset = yPositionOffset;
        prefabDefault_usePerYawXZOffsets = usePerYawXZOffsets;
        
        // Deep copy the array
        if (xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > 0)
        {
            prefabDefault_xzPositionOffsetsPerYaw = new Vector2[xzPositionOffsetsPerYaw.Length];
            System.Array.Copy(xzPositionOffsetsPerYaw, prefabDefault_xzPositionOffsetsPerYaw, xzPositionOffsetsPerYaw.Length);
        }
        else
        {
            prefabDefault_xzPositionOffsetsPerYaw = new Vector2[]
            {
                new Vector2(0f, -0.75f),
                new Vector2(-0.75f, 0f),
                new Vector2(0f, 0.75f),
                new Vector2(0.75f, 0f)
            };
        }
        
        hasStoredPrefabDefaults = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerShadowCaster] Stored prefab defaults for restoration");
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
        
        useGroundRaycast = prefabDefault_useGroundRaycast;
        shadowGroundLayers = prefabDefault_shadowGroundLayers;
        groundRaycastDistance = prefabDefault_groundRaycastDistance;
        raycastStartHeight = prefabDefault_raycastStartHeight;
        groundSurfaceOffset = prefabDefault_groundSurfaceOffset;
        smoothYPosition = prefabDefault_smoothYPosition;
        yPositionSmoothSpeed = prefabDefault_yPositionSmoothSpeed;
        yPositionOffset = prefabDefault_yPositionOffset;
        usePerYawXZOffsets = prefabDefault_usePerYawXZOffsets;
        
        // Deep copy the array
        if (prefabDefault_xzPositionOffsetsPerYaw != null && prefabDefault_xzPositionOffsetsPerYaw.Length > 0)
        {
            xzPositionOffsetsPerYaw = new Vector2[prefabDefault_xzPositionOffsetsPerYaw.Length];
            System.Array.Copy(prefabDefault_xzPositionOffsetsPerYaw, xzPositionOffsetsPerYaw, prefabDefault_xzPositionOffsetsPerYaw.Length);
        }
        
        // Reset shadow Y tracking
        hasInitializedShadowY = false;
        currentShadowY = 0f;
        
        // Update position immediately
        UpdateShadowPosition();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerShadowCaster] Restored prefab defaults after scene transition");
        }
    }
    
    /// <summary>
    /// Migrate from legacy groundOffset/zOffset to new offset system.
    /// </summary>
    private void MigrateLegacyOffsets()
    {
        if (hasAppliedLegacyMigration)
            return;
        
        // Check if legacy values are non-default and new values are default
        bool legacyHasCustomValues = groundOffset != 0.01f || zOffset != -0.75f;
        bool newHasDefaultValues = yPositionOffset == 0.01f && xzPositionOffset == new Vector2(0f, -0.75f);
        
        if (legacyHasCustomValues && newHasDefaultValues)
        {
            // Migrate legacy values to new system
            yPositionOffset = groundOffset;
            xzPositionOffset = new Vector2(0f, zOffset);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerShadowCaster] Migrated legacy offsets: groundOffset={groundOffset} -> yPositionOffset, zOffset={zOffset} -> xzPositionOffset.y");
            }
        }
        
        hasAppliedLegacyMigration = true;
    }
    
    /// <summary>
    /// Create and cache the shadow material instance ONCE.
    /// </summary>
    private void CreateShadowMaterial()
    {
        // Find or create material with correct shader
        Shader shadowShader = Shader.Find("Sprites/SpriteShadowCaster");
        if (shadowShader == null)
        {
            Debug.LogError("[PlayerShadowCaster] Sprites/SpriteShadowCaster shader not found!");
            return;
        }
        
        // Check if assigned material has wrong shader
        if (shadowMaterial == null || shadowMaterial.shader.name != "Sprites/SpriteShadowCaster")
        {
            if (shadowMaterial != null && enableDebugLogs)
            {
                Debug.LogWarning($"[PlayerShadowCaster] Assigned material uses shader '{shadowMaterial.shader.name}', creating new material with SpriteShadowCaster.");
            }
            
            // Create new material with correct shader
            shadowMaterial = new Material(shadowShader);
            shadowMaterial.name = "PlayerShadow (Auto-Created)";
        }
        
        // Create the cached instance ONCE by assigning to .material (creates instance)
        // Then store the reference and NEVER access .material again
        shadowMeshRenderer.sharedMaterial = shadowMaterial;
        cachedMaterialInstance = new Material(shadowMaterial); // Create our own instance
        cachedMaterialInstance.name = "PlayerShadow (Runtime Instance)";
        shadowMeshRenderer.sharedMaterial = cachedMaterialInstance; // Assign our instance
        
        // CRITICAL: Enable alpha clipping keyword (some URP versions require this)
        cachedMaterialInstance.EnableKeyword("_ALPHATEST_ON");
        cachedMaterialInstance.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        
        // Cache property existence
        CacheMaterialProperties();
        
        // Set initial cutoff on BOTH property names
        SetCutoffOnInstance(alphaCutoff);
        
        // Also set directly with both names to be extra sure
        if (cachedMaterialInstance.HasProperty("_Cutoff"))
        {
            cachedMaterialInstance.SetFloat("_Cutoff", alphaCutoff);
        }
        if (cachedMaterialInstance.HasProperty("_AlphaClipThreshold"))
        {
            cachedMaterialInstance.SetFloat("_AlphaClipThreshold", alphaCutoff);
        }
        
        // Log material properties once
        LogMaterialProperties();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[PlayerShadowCaster] Created cached material instance. Shader: {cachedMaterialInstance.shader.name}");
        }
    }
    
    private void Start()
    {
        // Sync mesh after Visual's mesh is created
        SyncMeshFromVisual();
        
        // Sync texture immediately
        SyncTextureFromVisual();
    }
    
    private void LateUpdate()
    {
        if (!isInitialized || cachedMaterialInstance == null)
            return;

        // A persistent player outlives each scene-local camera. Recover here as
        // a safety net if a transition path did not explicitly propagate the
        // replacement camera reference.
        if (cameraController == null)
            RefreshCameraController();
        
        // Keep both continuous rotation and the settled per-yaw offset synchronized.
        // Camera shots can rotate yaw without going through PlayerMovement3D.
        SynchronizeWithCameraYaw();
        
        // Sync mesh (in case Visual's mesh changes)
        SyncMeshFromVisual();
        
        // Sync texture (so shadow matches current sprite frame)
        SyncTextureFromVisual();
        
        // Maintain scale
        if (shadowScale != 1.0f && transform.localScale != originalLocalScale * shadowScale)
        {
            transform.localScale = originalLocalScale * shadowScale;
        }
    }
    
    /// <summary>
    /// Update shadow position based on current offsets and camera yaw.
    /// When useGroundRaycast is enabled, shadow Y is determined by raycasting to find
    /// the actual ground surface, which works correctly on slopes.
    /// </summary>
    private void UpdateShadowPosition()
    {
        if (transform.parent == null)
            return;
        
        // Get current XZ offset
        Vector2 xzOffset = GetCurrentXZOffset();
        
        // If not using per-yaw offsets, rotate the offset by camera yaw
        if (!usePerYawXZOffsets && currentCameraYaw != 0f)
        {
            float rad = currentCameraYaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            
            // Rotate XZ offset
            float rotatedX = xzOffset.x * cos - xzOffset.y * sin;
            float rotatedZ = xzOffset.x * sin + xzOffset.y * cos;
            xzOffset = new Vector2(rotatedX, rotatedZ);
        }
        
        // Calculate world XZ position for shadow
        Vector3 parentWorldPos = transform.parent.position;
        Vector3 shadowWorldXZ = new Vector3(
            parentWorldPos.x + xzOffset.x,
            parentWorldPos.y,
            parentWorldPos.z + xzOffset.y
        );
        
        // Determine Y position
        float targetShadowY;
        
        if (useGroundRaycast)
        {
            // Raycast downward to find actual ground surface
            Vector3 rayOrigin = new Vector3(shadowWorldXZ.x, parentWorldPos.y + raycastStartHeight, shadowWorldXZ.z);
            
            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                groundRaycastDistance + raycastStartHeight,
                shadowGroundLayers,
                QueryTriggerInteraction.Ignore
            ))
            {
                // Position shadow at ground surface + small offset
                targetShadowY = hit.point.y + groundSurfaceOffset;
                
                if (enableDebugLogs && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[PlayerShadowCaster] Ground raycast hit at Y={hit.point.y:F2}, target shadow Y={targetShadowY:F2}");
                }
            }
            else
            {
                // Fallback to fixed offset if no ground found (e.g., over void)
                targetShadowY = parentWorldPos.y + yPositionOffset;
                
                if (enableDebugLogs && Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning($"[PlayerShadowCaster] Ground raycast missed, using fixed offset Y={targetShadowY:F2}");
                }
            }
            
            // Initialize shadow Y on first frame
            if (!hasInitializedShadowY)
            {
                currentShadowY = targetShadowY;
                hasInitializedShadowY = true;
            }
            
            // Apply smoothing to avoid jitter on uneven surfaces
            if (smoothYPosition)
            {
                currentShadowY = Mathf.Lerp(currentShadowY, targetShadowY, yPositionSmoothSpeed * Time.deltaTime);
            }
            else
            {
                currentShadowY = targetShadowY;
            }
            
            // Set world position (shadow is child of player, but we set world position for accuracy)
            transform.position = new Vector3(shadowWorldXZ.x, currentShadowY, shadowWorldXZ.z);
        }
        else
        {
            // Fixed offset mode (original behavior)
            transform.localPosition = new Vector3(xzOffset.x, yPositionOffset, xzOffset.y);
        }
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
    /// Update shadow position for a specific camera yaw index.
    /// Called by PlayerMovement3D when camera rotates.
    /// </summary>
    /// <param name="yawIndex90">Camera yaw index (0-3)</param>
    public void UpdateShadowForCameraYaw(int yawIndex90)
    {
        // Normalize yaw index to 0-3
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        currentCameraYawIndex = yawIndex90;

        if (isInitialized)
            UpdateShadowPosition();
        
        if (enableDebugLogs)
        {
            Vector2 xzOffset = GetCurrentXZOffset();
            Debug.Log($"[PlayerShadowCaster] Updated shadow for yaw index {yawIndex90}: XZ=({xzOffset.x}, {xzOffset.y}), Y={yPositionOffset}");
        }
    }

    /// <summary>
    /// Bind the scene-authoritative camera and immediately align this persistent
    /// shadow with its current yaw.
    /// </summary>
    public void SetCameraController(TopDownCameraController3D newCameraController)
    {
        cameraController = newCameraController;
        if (cameraController == null)
            return;

        currentCameraYaw = cameraController.CurrentYaw;
        currentCameraYawIndex = cameraController.YawIndex90;
        transform.localRotation = Quaternion.Euler(0f, currentCameraYaw, 0f);
        UpdateShadowPosition();
    }

    /// <summary>
    /// Synchronize against the live camera. The continuous angle follows during a
    /// rotation; authored per-yaw offsets switch once the camera reaches its final
    /// cardinal yaw so they do not jump halfway through a cinematic move.
    /// </summary>
    public void SynchronizeWithCameraYaw()
    {
        UpdateShadowRotation();
        if (isInitialized)
            UpdateShadowPosition();
    }

    /// <summary>Re-resolve the active scene camera after a scene transition.</summary>
    public void RefreshCameraController()
    {
        SetCameraController(SceneCameraResolver.ResolveGameplayCameraController());
    }
    
    /// <summary>
    /// Update shadow rotation to match camera yaw.
    /// This keeps the shadow oriented correctly as the camera rotates around the player.
    /// </summary>
    private void UpdateShadowRotation()
    {
        if (cameraController != null)
        {
            currentCameraYaw = cameraController.CurrentYaw;
            if (!cameraController.IsRotatingYaw)
            {
                int yawIndex = cameraController.YawIndex90 % 4;
                if (yawIndex < 0) yawIndex += 4;
                currentCameraYawIndex = yawIndex;
            }
        }
        
        // Shadow rotates with camera yaw (Y-axis rotation only, stays flat on XZ plane)
        transform.localRotation = Quaternion.Euler(0f, currentCameraYaw, 0f);
    }
    
    /// <summary>
    /// Sync mesh from Visual GameObject.
    /// </summary>
    private void SyncMeshFromVisual()
    {
        if (visualMeshRenderer == null)
            return;
        
        MeshFilter visualFilter = visualMeshRenderer.GetComponent<MeshFilter>();
        if (visualFilter == null || visualFilter.sharedMesh == null)
            return;
        
        if (shadowMeshFilter.sharedMesh != visualFilter.sharedMesh)
        {
            shadowMeshFilter.sharedMesh = visualFilter.sharedMesh;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[PlayerShadowCaster] Synced mesh '{visualFilter.sharedMesh.name}' from Visual");
            }
        }
    }
    
    /// <summary>
    /// Sync texture from Visual GameObject.
    /// Writes ONLY to cachedMaterialInstance, never recreates it.
    /// </summary>
    private void SyncTextureFromVisual()
    {
        if (visualMeshRenderer == null || cachedMaterialInstance == null)
            return;
        
        // Get texture from Visual's MaterialPropertyBlock
        visualMeshRenderer.GetPropertyBlock(visualPropertyBlock);
        
        Texture2D visualTexture = null;
        Vector4 visualUV_ST = new Vector4(1f, 1f, 0f, 0f);
        float cutoffToUse = alphaCutoff;
        
        // Try _MainTex first
        if (visualPropertyBlock.HasTexture("_MainTex"))
        {
            visualTexture = visualPropertyBlock.GetTexture("_MainTex") as Texture2D;
            if (visualPropertyBlock.HasVector("_MainTex_ST"))
            {
                visualUV_ST = visualPropertyBlock.GetVector("_MainTex_ST");
            }
            if (visualPropertyBlock.HasFloat("_Cutoff"))
            {
                cutoffToUse = visualPropertyBlock.GetFloat("_Cutoff");
            }
        }
        // Try _BaseMap (URP)
        else if (visualPropertyBlock.HasTexture("_BaseMap"))
        {
            visualTexture = visualPropertyBlock.GetTexture("_BaseMap") as Texture2D;
            if (visualPropertyBlock.HasVector("_BaseMap_ST"))
            {
                visualUV_ST = visualPropertyBlock.GetVector("_BaseMap_ST");
            }
        }
        
        // Fallback: get from material directly
        if (visualTexture == null && visualMeshRenderer.sharedMaterial != null)
        {
            Material visMat = visualMeshRenderer.sharedMaterial;
            if (visMat.HasProperty("_MainTex"))
            {
                visualTexture = visMat.GetTexture("_MainTex") as Texture2D;
            }
            else if (visMat.HasProperty("_BaseMap"))
            {
                visualTexture = visMat.GetTexture("_BaseMap") as Texture2D;
            }
        }
        
        if (visualTexture != null)
        {
            // Set texture (supports both property names)
            if (hasMainTex)
            {
                cachedMaterialInstance.SetTexture("_MainTex", visualTexture);
            }
            if (hasBaseMap)
            {
                cachedMaterialInstance.SetTexture("_BaseMap", visualTexture);
            }
            
            // Set UV transform
            if (hasMainTex_ST)
            {
                cachedMaterialInstance.SetVector("_MainTex_ST", visualUV_ST);
            }
            if (hasBaseMap_ST)
            {
                cachedMaterialInstance.SetVector("_BaseMap_ST", visualUV_ST);
            }
            
            // Set cutoff
            SetCutoffOnInstance(cutoffToUse);
            
            // Debug logging (throttled)
            if (enableDebugLogs && Time.frameCount % 60 == 0)
            {
                Texture2D verifyTex = hasMainTex 
                    ? cachedMaterialInstance.GetTexture("_MainTex") as Texture2D 
                    : (hasBaseMap ? cachedMaterialInstance.GetTexture("_BaseMap") as Texture2D : null);
                float verifyCutoff = hasCutoff 
                    ? cachedMaterialInstance.GetFloat("_Cutoff") 
                    : (hasAlphaClipThreshold ? cachedMaterialInstance.GetFloat("_AlphaClipThreshold") : -1f);
                Vector4 verifyUV_ST = hasMainTex_ST 
                    ? cachedMaterialInstance.GetVector("_MainTex_ST") 
                    : (hasBaseMap_ST ? cachedMaterialInstance.GetVector("_BaseMap_ST") : Vector4.zero);
                
                Debug.Log($"[PlayerShadowCaster] === SYNC DEBUG ===");
                Debug.Log($"[PlayerShadowCaster] Input UV_ST from Visual: ({visualUV_ST.x:F3}, {visualUV_ST.y:F3}, {visualUV_ST.z:F3}, {visualUV_ST.w:F3})");
                Debug.Log($"[PlayerShadowCaster] Verified Material UV_ST: ({verifyUV_ST.x:F3}, {verifyUV_ST.y:F3}, {verifyUV_ST.z:F3}, {verifyUV_ST.w:F3})");
                Debug.Log($"[PlayerShadowCaster] Texture: {visualTexture.name} -> {verifyTex?.name ?? "NULL"}");
                Debug.Log($"[PlayerShadowCaster] Cutoff: {cutoffToUse} -> {verifyCutoff}");
                
                if (verifyTex != null)
                {
                    bool hasAlpha = verifyTex.format.ToString().Contains("RGBA") || 
                                   verifyTex.format.ToString().Contains("Alpha");
                    Debug.Log($"[PlayerShadowCaster] Texture Size: {verifyTex.width}x{verifyTex.height}, Format: {verifyTex.format}, HasAlpha: {hasAlpha}");
                    
                    // Check if texture is readable and sample a corner pixel
                    try
                    {
                        if (verifyTex.isReadable)
                        {
                            Color cornerPixel = verifyTex.GetPixel(0, 0);
                            Color centerPixel = verifyTex.GetPixel(verifyTex.width / 2, verifyTex.height / 2);
                            Debug.Log($"[PlayerShadowCaster] Corner pixel alpha: {cornerPixel.a:F3}, Center pixel alpha: {centerPixel.a:F3}");
                        }
                        else
                        {
                            Debug.Log($"[PlayerShadowCaster] Texture is NOT readable - cannot sample pixels");
                        }
                    }
                    catch (System.Exception e)
                    {
                        Debug.Log($"[PlayerShadowCaster] Cannot sample texture: {e.Message}");
                    }
                }
                
                // Log material keywords
                string[] keywords = cachedMaterialInstance.shaderKeywords;
                Debug.Log($"[PlayerShadowCaster] Material keywords: [{string.Join(", ", keywords)}]");
                Debug.Log($"[PlayerShadowCaster] ==================");
            }
        }
        else if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.LogWarning("[PlayerShadowCaster] Visual texture is NULL!");
        }
    }
    
    private void CacheMaterialProperties()
    {
        if (cachedMaterialInstance == null)
            return;
            
        hasMainTex = cachedMaterialInstance.HasProperty("_MainTex");
        hasBaseMap = cachedMaterialInstance.HasProperty("_BaseMap");
        hasMainTex_ST = cachedMaterialInstance.HasProperty("_MainTex_ST");
        hasBaseMap_ST = cachedMaterialInstance.HasProperty("_BaseMap_ST");
        hasCutoff = cachedMaterialInstance.HasProperty("_Cutoff");
        hasAlphaClipThreshold = cachedMaterialInstance.HasProperty("_AlphaClipThreshold");
    }
    
    private void SetCutoffOnInstance(float cutoff)
    {
        if (cachedMaterialInstance == null)
            return;
            
        if (hasCutoff)
        {
            cachedMaterialInstance.SetFloat("_Cutoff", cutoff);
        }
        if (hasAlphaClipThreshold)
        {
            cachedMaterialInstance.SetFloat("_AlphaClipThreshold", cutoff);
        }
    }
    
    private void LogMaterialProperties()
    {
        if (cachedMaterialInstance == null)
            return;
            
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[PlayerShadowCaster] === Shadow Material Properties ===");
        sb.AppendLine($"Shader: {cachedMaterialInstance.shader.name}");
        sb.AppendLine($"Material: {cachedMaterialInstance.name}");
        sb.AppendLine($"Properties:");
        sb.AppendLine($"  _MainTex: {(hasMainTex ? "YES" : "NO")}");
        sb.AppendLine($"  _BaseMap: {(hasBaseMap ? "YES" : "NO")}");
        sb.AppendLine($"  _MainTex_ST: {(hasMainTex_ST ? "YES" : "NO")}");
        sb.AppendLine($"  _BaseMap_ST: {(hasBaseMap_ST ? "YES" : "NO")}");
        sb.AppendLine($"  _Cutoff: {(hasCutoff ? "YES" : "NO")}");
        sb.AppendLine($"  _AlphaClipThreshold: {(hasAlphaClipThreshold ? "YES" : "NO")}");
        sb.AppendLine($"==========================================");
        
        Debug.Log(sb.ToString());
    }
    
    public void SetVisualRenderer(MeshRenderer renderer)
    {
        visualMeshRenderer = renderer;
        if (renderer != null)
        {
            visualPlayerVisual = renderer.GetComponent<MeshRendererPlayerVisual>();
        }
    }
    
    public void SetShadowScale(float scale)
    {
        shadowScale = scale;
        if (transform != null)
        {
            transform.localScale = originalLocalScale * shadowScale;
        }
    }
    
    public void SetGroundOffset(float offset)
    {
        yPositionOffset = offset;
        groundOffset = offset; // Keep legacy in sync
        UpdateShadowPosition();
    }
    
    public void SetZOffset(float offset)
    {
        xzPositionOffset = new Vector2(xzPositionOffset.x, offset);
        zOffset = offset; // Keep legacy in sync
        UpdateShadowPosition();
    }
    
    /// <summary>
    /// Set the Y position offset.
    /// </summary>
    public void SetYPositionOffset(float offset)
    {
        yPositionOffset = offset;
        groundOffset = offset;
        UpdateShadowPosition();
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
        zOffset = offset.y; // Keep legacy in sync
        UpdateShadowPosition();
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
    /// Get current camera yaw index.
    /// </summary>
    public int CurrentCameraYawIndex => currentCameraYawIndex;
    
    public void SetAlphaCutoff(float cutoff)
    {
        alphaCutoff = Mathf.Clamp01(cutoff);
        SetCutoffOnInstance(alphaCutoff);
    }
    
    #region Ground Raycast API
    
    /// <summary>
    /// Enable or disable ground raycast for dynamic Y positioning on slopes.
    /// </summary>
    public void SetUseGroundRaycast(bool enable)
    {
        useGroundRaycast = enable;
        if (!enable)
        {
            hasInitializedShadowY = false; // Reset for next enable
        }
    }
    
    /// <summary>
    /// Check if using ground raycast for Y positioning.
    /// </summary>
    public bool IsUsingGroundRaycast => useGroundRaycast;
    
    /// <summary>
    /// Set the ground surface offset (distance above detected ground).
    /// </summary>
    public void SetGroundSurfaceOffset(float offset)
    {
        groundSurfaceOffset = Mathf.Max(0f, offset);
    }
    
    /// <summary>
    /// Get the ground surface offset.
    /// </summary>
    public float GetGroundSurfaceOffset() => groundSurfaceOffset;
    
    /// <summary>
    /// Set the layers used for ground raycast detection.
    /// </summary>
    public void SetShadowGroundLayers(LayerMask layers)
    {
        shadowGroundLayers = layers;
    }
    
    /// <summary>
    /// Get the layers used for ground raycast detection.
    /// </summary>
    public LayerMask GetShadowGroundLayers() => shadowGroundLayers;
    
    /// <summary>
    /// Set the raycast start height above player.
    /// </summary>
    public void SetRaycastStartHeight(float height)
    {
        raycastStartHeight = Mathf.Max(0f, height);
    }
    
    /// <summary>
    /// Get the raycast start height.
    /// </summary>
    public float GetRaycastStartHeight() => raycastStartHeight;
    
    /// <summary>
    /// Set the maximum raycast distance.
    /// </summary>
    public void SetGroundRaycastDistance(float distance)
    {
        groundRaycastDistance = Mathf.Max(0f, distance);
    }
    
    /// <summary>
    /// Get the maximum raycast distance.
    /// </summary>
    public float GetGroundRaycastDistance() => groundRaycastDistance;
    
    /// <summary>
    /// Enable or disable Y position smoothing.
    /// </summary>
    public void SetSmoothYPosition(bool enable)
    {
        smoothYPosition = enable;
    }
    
    /// <summary>
    /// Check if Y position smoothing is enabled.
    /// </summary>
    public bool IsSmoothYPositionEnabled => smoothYPosition;
    
    /// <summary>
    /// Set the Y position smoothing speed.
    /// </summary>
    public void SetYPositionSmoothSpeed(float speed)
    {
        yPositionSmoothSpeed = Mathf.Max(0f, speed);
    }
    
    /// <summary>
    /// Get the Y position smoothing speed.
    /// </summary>
    public float GetYPositionSmoothSpeed() => yPositionSmoothSpeed;
    
    #endregion
    
    #region Debug Gizmos
    
    /// <summary>
    /// Draw debug visualization for ground raycast in Scene view.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!useGroundRaycast || transform.parent == null)
            return;
        
        // Get current XZ offset (simplified, doesn't account for rotation in editor)
        Vector2 xzOffset = xzPositionOffset;
        if (usePerYawXZOffsets && xzPositionOffsetsPerYaw != null && xzPositionOffsetsPerYaw.Length > 0)
        {
            xzOffset = xzPositionOffsetsPerYaw[0];
        }
        
        Vector3 parentPos = transform.parent.position;
        Vector3 rayOrigin = new Vector3(
            parentPos.x + xzOffset.x,
            parentPos.y + raycastStartHeight,
            parentPos.z + xzOffset.y
        );
        
        Vector3 rayEnd = rayOrigin + Vector3.down * (groundRaycastDistance + raycastStartHeight);
        
        // Draw raycast line
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(rayOrigin, rayEnd);
        
        // Draw ray origin
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(rayOrigin, 0.05f);
        
        // Draw ray end
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(rayEnd, 0.05f);
        
        // Draw current shadow position
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
        
        // Draw ground surface offset indicator
        if (Application.isPlaying && hasInitializedShadowY)
        {
            Gizmos.color = Color.magenta;
            Vector3 groundPoint = new Vector3(transform.position.x, currentShadowY - groundSurfaceOffset, transform.position.z);
            Gizmos.DrawWireCube(groundPoint, new Vector3(0.3f, 0.01f, 0.3f));
        }
    }
    
    #endregion
}
