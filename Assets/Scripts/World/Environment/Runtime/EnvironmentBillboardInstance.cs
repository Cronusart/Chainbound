using UnityEngine;

/// <summary>
/// Environment billboard instance component.
/// Holds sprite sequence, animation settings, and handles per-instance state.
/// No per-frame Update - managed by EnvironmentBillboardAnimator.
/// 
/// Optimized for Solana PSG1: uses MaterialPropertyBlock with shared materials.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class EnvironmentBillboardInstance : MonoBehaviour
{
    [Header("Animation Sequence")]
    [Tooltip("Sprite frames for animation loop. Leave empty for static billboard.")]
    [SerializeField] private Sprite[] spriteFrames = new Sprite[0];
    
    [Header("Animation Settings")]
    [Tooltip("Frames per second for animation (only used if spriteFrames.Length > 1)")]
    [SerializeField] private float animationFPS = 4f;
    
    [Tooltip("Loop animation (true) or play once and freeze (false)")]
    [SerializeField] private bool loop = true;
    
    [Tooltip("Random phase offset (0-1) to avoid all instances animating in sync")]
    [SerializeField] private bool useRandomPhaseOffset = true;
    
    [Header("Rendering")]
    [Tooltip("Shared material for all environment billboards (alpha-cutout / depth-write). Set in manager or inspector.")]
    [SerializeField] private Material sharedMaterial;
    
    [Tooltip("Size of the quad mesh (sprite size in world units)")]
    [SerializeField] private Vector2 spriteSize = new Vector2(1f, 1f);
    
    [Header("LOD")]
    [Tooltip("Freeze animation at frame 0 when beyond LOD distance (set by manager)")]
    [SerializeField] private bool useLOD = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    // Internal state (managed by EnvironmentBillboardAnimator)
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Mesh quadMesh;
    private MaterialPropertyBlock propertyBlock;
    
    // Animation state
    private int currentFrame = 0;
    private float animationTimer = 0f;
    private float phaseOffset = 0f;
    private bool isInitialized = false;
    
    // LOD state
    private bool isWithinLODDistance = true;
    
    // Cached sprite data
    private bool hasAnimation => spriteFrames != null && spriteFrames.Length > 1;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;
        
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        propertyBlock = new MaterialPropertyBlock();
        
        // Create quad mesh
        CreateQuadMesh();
        
        // Setup shared material (must be set externally or in inspector)
        if (sharedMaterial != null)
        {
            meshRenderer.sharedMaterial = sharedMaterial;
        }
        else
        {
            Debug.LogWarning($"[EnvironmentBillboardInstance] No shared material assigned on {gameObject.name}! Billboard will not render correctly.");
        }
        
        // Random phase offset for animation variety
        if (useRandomPhaseOffset && hasAnimation)
        {
            phaseOffset = Random.Range(0f, 1f);
        }
        
        // Initialize animation timer with phase offset
        if (hasAnimation)
        {
            animationTimer = phaseOffset;
        }
        
        // Update initial frame
        UpdateFrame();
        
        isInitialized = true;
    }

    private void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        quadMesh.name = $"EnvBillboardQuad_{gameObject.name}";
        
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-spriteSize.x * 0.5f, -spriteSize.y * 0.5f, 0f),
            new Vector3(spriteSize.x * 0.5f, -spriteSize.y * 0.5f, 0f),
            new Vector3(-spriteSize.x * 0.5f, spriteSize.y * 0.5f, 0f),
            new Vector3(spriteSize.x * 0.5f, spriteSize.y * 0.5f, 0f)
        };
        
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        
        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };
        
        quadMesh.vertices = vertices;
        quadMesh.uv = uvs;
        quadMesh.triangles = triangles;
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();
        
        meshFilter.mesh = quadMesh;
    }

    /// <summary>
    /// Update animation frame. Called by EnvironmentBillboardAnimator at fixed intervals.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update (from manager's tick interval)</param>
    public void UpdateAnimation(float deltaTime)
    {
        if (!hasAnimation || !isInitialized)
            return;
        
        // LOD: freeze at frame 0 if beyond distance
        if (useLOD && !isWithinLODDistance)
        {
            if (currentFrame != 0)
            {
                currentFrame = 0;
                UpdateFrame();
            }
            return;
        }
        
        // Advance animation timer
        animationTimer += deltaTime * animationFPS;
        
        // Calculate frame
        if (loop)
        {
            currentFrame = Mathf.FloorToInt(animationTimer) % spriteFrames.Length;
        }
        else
        {
            // Play once and freeze at last frame
            int maxFrame = spriteFrames.Length - 1;
            currentFrame = Mathf.Min(Mathf.FloorToInt(animationTimer), maxFrame);
        }
        
        UpdateFrame();
    }
    
    /// <summary>
    /// Update the rendered sprite frame using MaterialPropertyBlock.
    /// </summary>
    private void UpdateFrame()
    {
        if (!isInitialized || spriteFrames == null || spriteFrames.Length == 0)
            return;
        
        Sprite currentSprite = spriteFrames[Mathf.Clamp(currentFrame, 0, spriteFrames.Length - 1)];
        if (currentSprite == null)
            return;
        
        // Get existing property block (preserves other properties)
        meshRenderer.GetPropertyBlock(propertyBlock);
        
        if (currentSprite.texture != null)
        {
            propertyBlock.SetTexture("_MainTex", currentSprite.texture);
            
            // Set UV offset/scale for sprite atlas support
            Rect spriteRect = currentSprite.textureRect;
            Vector2 textureSize = new Vector2(currentSprite.texture.width, currentSprite.texture.height);
            
            Vector2 offset = new Vector2(spriteRect.x / textureSize.x, spriteRect.y / textureSize.y);
            Vector2 scale = new Vector2(spriteRect.width / textureSize.x, spriteRect.height / textureSize.y);
            
            propertyBlock.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
            
            // Preserve material properties from shared material
            if (sharedMaterial != null)
            {
                if (sharedMaterial.HasProperty("_Color"))
                {
                    propertyBlock.SetColor("_Color", sharedMaterial.GetColor("_Color"));
                }
                if (sharedMaterial.HasProperty("_Cutoff"))
                {
                    propertyBlock.SetFloat("_Cutoff", sharedMaterial.GetFloat("_Cutoff"));
                }
            }
            
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
    }
    
    /// <summary>
    /// Set LOD distance state (called by manager).
    /// </summary>
    public void SetLODState(bool withinDistance)
    {
        isWithinLODDistance = withinDistance;
    }
    
    /// <summary>
    /// Set shared material (called by manager or inspector).
    /// </summary>
    public void SetSharedMaterial(Material material)
    {
        sharedMaterial = material;
        if (meshRenderer != null)
        {
            meshRenderer.sharedMaterial = material;
        }
    }
    
    /// <summary>
    /// Get current frame index.
    /// </summary>
    public int GetCurrentFrame() => currentFrame;
    
    /// <summary>
    /// Get animation frame count.
    /// </summary>
    public int GetFrameCount() => spriteFrames != null ? spriteFrames.Length : 0;
    
    /// <summary>
    /// Check if this instance has animation.
    /// </summary>
    public bool HasAnimation() => hasAnimation;
    
    /// <summary>
    /// Get distance from camera (for LOD culling).
    /// </summary>
    public float GetDistanceFromCamera(Camera camera)
    {
        if (camera == null) return float.MaxValue;
        return Vector3.Distance(transform.position, camera.transform.position);
    }
    
    /// <summary>
    /// Check if renderer is visible to camera (for frustum culling).
    /// </summary>
    public bool IsVisibleToCamera(Camera camera)
    {
        if (camera == null || meshRenderer == null) return true;
        
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
        return GeometryUtility.TestPlanesAABB(planes, meshRenderer.bounds);
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Reinitialize if material changed in editor
        if (Application.isPlaying && isInitialized && sharedMaterial != null && meshRenderer != null)
        {
            meshRenderer.sharedMaterial = sharedMaterial;
        }
    }
#endif
}

