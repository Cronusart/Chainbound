using UnityEngine;

/// <summary>
/// Shadow caster for NPCs. Wraps PlayerShadowCaster pattern.
/// Syncs mesh and texture from Visual GameObject, stays flat (no billboard rotation).
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class NPCShadowCaster : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Visual GameObject's MeshRenderer to sync from")]
    [SerializeField] private MeshRenderer visualMeshRenderer;
    
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
    
    [Tooltip("Material for shadow casting (should use Sprites/SpriteShadowCaster shader)")]
    [SerializeField] private Material shadowMaterial;
    
    [Tooltip("Alpha cutoff for shadow shape (0.5 = standard)")]
    [SerializeField] private float alphaCutoff = 0.5f;
    
    [Tooltip("Scale factor for shadow size (1.0 = same size as visual)")]
    [SerializeField] private float shadowScale = 1.0f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private MeshRenderer shadowMeshRenderer;
    private MeshFilter shadowMeshFilter;
    private MaterialPropertyBlock visualPropertyBlock;
    private Vector3 originalLocalScale;
    
    // Cached material instance
    private Material cachedMaterialInstance;
    private bool isInitialized = false;
    
    // Camera yaw tracking
    private float currentCameraYaw = 0f;
    private int currentCameraYawIndex = 0;
    
    // Cached property existence flags
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
                }
            }
        }
        
        // Auto-find camera controller if not assigned. Scene-local NPCs usually
        // resolve this in Awake; the LateUpdate recovery also supports spawned
        // or persistent NPCs whose camera arrives later.
        if (cameraController == null)
            RefreshCameraController();
        
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
        
        // Create shadow material
        CreateShadowMaterial();
        
        // Configure shadow renderer
        shadowMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        shadowMeshRenderer.receiveShadows = false;
        
        // Position and rotation
        transform.localRotation = Quaternion.identity;
        UpdateShadowPosition();
        
        if (shadowScale != 1.0f)
        {
            transform.localScale = originalLocalScale * shadowScale;
        }
        
        isInitialized = true;
    }
    
    private void CreateShadowMaterial()
    {
        Shader shadowShader = Shader.Find("Sprites/SpriteShadowCaster");
        if (shadowShader == null)
        {
            Debug.LogError("[NPCShadowCaster] Sprites/SpriteShadowCaster shader not found!");
            return;
        }
        
        if (shadowMaterial == null || shadowMaterial.shader.name != "Sprites/SpriteShadowCaster")
        {
            if (shadowMaterial != null && enableDebugLogs)
            {
                Debug.LogWarning($"[NPCShadowCaster] Assigned material uses shader '{shadowMaterial.shader.name}', creating new material.");
            }
            
            shadowMaterial = new Material(shadowShader);
            shadowMaterial.name = "NPCShadow (Auto-Created)";
        }
        
        shadowMeshRenderer.sharedMaterial = shadowMaterial;
        cachedMaterialInstance = new Material(shadowMaterial);
        cachedMaterialInstance.name = "NPCShadow (Runtime Instance)";
        shadowMeshRenderer.sharedMaterial = cachedMaterialInstance;
        
        cachedMaterialInstance.EnableKeyword("_ALPHATEST_ON");
        cachedMaterialInstance.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        
        CacheMaterialProperties();
        SetCutoffOnInstance(alphaCutoff);
        
        if (cachedMaterialInstance.HasProperty("_Cutoff"))
        {
            cachedMaterialInstance.SetFloat("_Cutoff", alphaCutoff);
        }
        if (cachedMaterialInstance.HasProperty("_AlphaClipThreshold"))
        {
            cachedMaterialInstance.SetFloat("_AlphaClipThreshold", alphaCutoff);
        }
    }
    
    private void Start()
    {
        SyncMeshFromVisual();
        SyncTextureFromVisual();
    }
    
    private void LateUpdate()
    {
        if (!isInitialized || cachedMaterialInstance == null)
            return;

        if (cameraController == null)
            RefreshCameraController();
        
        SynchronizeWithCameraYaw();
        SyncMeshFromVisual();
        SyncTextureFromVisual();
        
        if (shadowScale != 1.0f && transform.localScale != originalLocalScale * shadowScale)
        {
            transform.localScale = originalLocalScale * shadowScale;
        }
    }
    
    private void UpdateShadowPosition()
    {
        if (transform.parent == null)
            return;
        
        Vector2 xzOffset = GetCurrentXZOffset();
        
        if (!usePerYawXZOffsets && currentCameraYaw != 0f)
        {
            float rad = currentCameraYaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            
            float rotatedX = xzOffset.x * cos - xzOffset.y * sin;
            float rotatedZ = xzOffset.x * sin + xzOffset.y * cos;
            xzOffset = new Vector2(rotatedX, rotatedZ);
        }
        
        transform.localPosition = new Vector3(xzOffset.x, yPositionOffset, xzOffset.y);
    }
    
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
        
        transform.localRotation = Quaternion.Euler(0f, currentCameraYaw, 0f);
    }
    
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
        }
    }
    
    private void SyncTextureFromVisual()
    {
        if (visualMeshRenderer == null || cachedMaterialInstance == null)
            return;
        
        visualMeshRenderer.GetPropertyBlock(visualPropertyBlock);
        
        Texture2D visualTexture = null;
        Vector4 visualUV_ST = new Vector4(1f, 1f, 0f, 0f);
        float cutoffToUse = alphaCutoff;
        
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
        else if (visualPropertyBlock.HasTexture("_BaseMap"))
        {
            visualTexture = visualPropertyBlock.GetTexture("_BaseMap") as Texture2D;
            if (visualPropertyBlock.HasVector("_BaseMap_ST"))
            {
                visualUV_ST = visualPropertyBlock.GetVector("_BaseMap_ST");
            }
        }
        
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
            if (hasMainTex)
            {
                cachedMaterialInstance.SetTexture("_MainTex", visualTexture);
            }
            if (hasBaseMap)
            {
                cachedMaterialInstance.SetTexture("_BaseMap", visualTexture);
            }
            
            if (hasMainTex_ST)
            {
                cachedMaterialInstance.SetVector("_MainTex_ST", visualUV_ST);
            }
            if (hasBaseMap_ST)
            {
                cachedMaterialInstance.SetVector("_BaseMap_ST", visualUV_ST);
            }
            
            SetCutoffOnInstance(cutoffToUse);
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
    
    public void UpdateShadowForCameraYaw(int yawIndex90)
    {
        yawIndex90 = yawIndex90 % 4;
        if (yawIndex90 < 0) yawIndex90 += 4;
        
        currentCameraYawIndex = yawIndex90;

        if (isInitialized)
            UpdateShadowPosition();
    }

    /// <summary>Bind a camera and immediately align the NPC shadow to its yaw.</summary>
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
    /// Synchronize continuous camera rotation and settled cardinal offset state.
    /// This also covers camera shots that do not route through NPCMovementAgent.
    /// </summary>
    public void SynchronizeWithCameraYaw()
    {
        UpdateShadowRotation();
        if (isInitialized)
            UpdateShadowPosition();
    }

    public int CurrentCameraYawIndex => currentCameraYawIndex;

    /// <summary>Re-resolve the active scene camera.</summary>
    public void RefreshCameraController()
    {
        SetCameraController(SceneCameraResolver.ResolveGameplayCameraController());
    }
}

