using UnityEngine;

/// <summary>
/// Updates SpriteRenderer.sortingOrder based on world or camera-space position for proper depth sorting.
/// Attach to the sprite child object in the player hierarchy (not the root with physics).
/// 
/// IMPORTANT: This is for sprite-vs-sprite ordering only (transparent queue renderers).
/// For sprite-vs-opaque MeshRenderer occlusion, use a depth-writing shader (e.g., Sprites/DepthWrite).
/// 
/// How it works:
/// - Calculates depth value based on selected sort mode
/// - Objects further from camera get lower sorting orders
/// - This ensures sprites are drawn in correct order relative to each other
/// 
/// Setup:
/// 1. Configure Project Settings → Graphics → Transparency Sort Mode to "Custom Axis"
/// 2. Set Transparency Sort Axis to (0, 0, 1) for Z-based sorting
/// 3. Attach this script to the SpriteChild GameObject containing the SpriteRenderer
/// 4. Select appropriate Sort Mode (CameraSpaceZ recommended for pitched cameras)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DynamicSpriteSorting : MonoBehaviour
{
    [Header("Sorting Configuration")]
    [Tooltip("Multiplier for position-to-order conversion. Higher = more granular sorting.")]
    [SerializeField] private float orderMultiplier = 100f;
    
    [Tooltip("Base sorting order offset. Useful for ensuring player is above ground sprites.")]
    [SerializeField] private int baseSortingOrder = 0;
    
    [Tooltip("Use parent's position for sorting (recommended for split hierarchy).")]
    [SerializeField] private bool useParentPosition = true;
    
    [Tooltip("Which method to use for depth sorting. CameraSpaceZ recommended for pitched cameras.")]
    [SerializeField] private SortMode sortMode = SortMode.CameraSpaceZ;
    
    [Tooltip("Camera reference for camera-space modes. Auto-finds Camera.main if null.")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Update Settings")]
    [Tooltip("Update sorting every frame. Disable for static sprites.")]
    [SerializeField] private bool updateEveryFrame = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public enum SortMode
    {
        WorldY,            // Use world Y position (traditional 2D top-down)
        WorldZ,            // Use world Z position (3D top-down with Z as depth)
        CameraSpaceZ,      // Use camera-space Z (cam.InverseTransformPoint). Recommended for pitched cameras.
        CameraForwardDot   // Use dot product with camera forward (Vector3.Dot(pos - camPos, cam.forward))
    }

    private SpriteRenderer spriteRenderer;
    private Transform sortingTransform;
    private Camera cachedCamera;
    private int lastSortingOrder = int.MinValue;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Use parent transform for sorting if specified (for split hierarchy)
        if (useParentPosition && transform.parent != null)
        {
            sortingTransform = transform.parent;
        }
        else
        {
            sortingTransform = transform;
        }
        
        // Cache camera reference
        FindCamera();
    }
    
    private void FindCamera()
    {
        cachedCamera = SceneCameraResolver.ResolveOrKeep(targetCamera, gameObject);
    }

    private bool TryGetCachedCamera(out Camera camera)
    {
        if (!SceneCameraResolver.IsUsableFor(cachedCamera, gameObject))
            FindCamera();

        camera = cachedCamera;
        return camera != null;
    }

    private void Start()
    {
        // Ensure camera is found
        if (cachedCamera == null)
        {
            FindCamera();
        }
        
        // Initial sorting update
        UpdateSortingOrder();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame)
        {
            UpdateSortingOrder();
        }
    }

    /// <summary>
    /// Calculate and apply sorting order based on selected sort mode.
    /// </summary>
    public void UpdateSortingOrder()
    {
        if (spriteRenderer == null || sortingTransform == null)
            return;

        float depthValue = CalculateDepthValue();

        // Calculate sorting order (flip sign so further from camera = lower order = drawn behind)
        int newOrder = baseSortingOrder + Mathf.RoundToInt(-depthValue * orderMultiplier);
        
        // Only update if changed (optimization)
        if (newOrder != lastSortingOrder)
        {
            spriteRenderer.sortingOrder = newOrder;
            lastSortingOrder = newOrder;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[DynamicSpriteSorting] {gameObject.name}: Depth={depthValue:F2}, Order={newOrder}, Mode={sortMode}");
            }
        }
    }
    
    /// <summary>
    /// Calculate depth value based on selected sort mode.
    /// </summary>
    private float CalculateDepthValue()
    {
        Vector3 worldPos = sortingTransform.position;
        
        switch (sortMode)
        {
            case SortMode.WorldY:
                return worldPos.y;
                
            case SortMode.WorldZ:
                return worldPos.z;
                
            case SortMode.CameraSpaceZ:
                if (!TryGetCachedCamera(out Camera spaceCamera))
                    return worldPos.z;
                Vector3 camSpacePos = spaceCamera.transform.InverseTransformPoint(worldPos);
                return camSpacePos.z;
                
            case SortMode.CameraForwardDot:
                if (!TryGetCachedCamera(out Camera forwardCamera))
                    return worldPos.z;
                Vector3 toObject = worldPos - forwardCamera.transform.position;
                return Vector3.Dot(toObject, forwardCamera.transform.forward);
                
            default:
                return worldPos.z;
        }
    }

    /// <summary>
    /// Force an immediate sorting update.
    /// </summary>
    public void ForceUpdate()
    {
        lastSortingOrder = int.MinValue; // Reset to force update
        UpdateSortingOrder();
    }

    /// <summary>
    /// Set the sort mode at runtime.
    /// </summary>
    public void SetSortMode(SortMode mode)
    {
        sortMode = mode;
        ForceUpdate();
    }
    
    /// <summary>
    /// Set the target camera at runtime.
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        cachedCamera = camera;
        ForceUpdate();
    }

    /// <summary>
    /// Set the order multiplier at runtime.
    /// </summary>
    public void SetOrderMultiplier(float multiplier)
    {
        orderMultiplier = multiplier;
        ForceUpdate();
    }

    /// <summary>
    /// Get current sorting order.
    /// </summary>
    public int CurrentSortingOrder => spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
}

