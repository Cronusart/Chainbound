using UnityEngine;

/// <summary>
/// Makes a sprite/quad always face the camera (full billboard).
/// Useful for 2D pixel characters in a 3D world (Cassette Beasts style).
/// 
/// This script should be placed on a CHILD object of the physics root.
/// The parent's Rigidbody constraints will NOT prevent this child from rotating.
/// </summary>
[DefaultExecutionOrder(50)]
[RequireComponent(typeof(Transform))]
public class SpriteBillboard : MonoBehaviour
{
    [Header("Camera Reference")]
    [Tooltip("Target camera to face. If null, uses the GameServices persistent rig when live, otherwise the scene camera.")]
    [SerializeField] private Camera targetCamera;
    
    [Header("Billboard Mode")]
    [Tooltip("How to orient the billboard toward the camera.")]
    [SerializeField] private BillboardMode mode = BillboardMode.FaceCamera;
    
    [Tooltip("Keep horizontal facing aligned to the camera plane, suppressing per-object yaw/parallax while still following camera-zone yaw. Disable for the selected mode's full yaw behavior.")]
    [SerializeField] private bool lockYRotation = true;
    
    [Header("Rotation Settings")]
    [Tooltip("Smooth rotation instead of instant.")]
    [SerializeField] private bool smoothRotation = false;
    
    [Tooltip("Rotation speed for smooth rotation.")]
    [SerializeField] private float rotationSpeed = 10f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public enum BillboardMode
    {
        FaceCamera,         // Face the camera position directly
        FaceCameraForward,  // Face the camera's forward direction (all sprites face same way)
        FaceCameraPlane     // Face parallel to camera's near plane
    }

    private bool isInitialized = false;

    private void Awake()
    {
        FindCamera();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (!IsTargetCameraUsable())
            FindCamera(forceRefresh: true);

        if (targetCamera != null)
            ApplyBillboardRotation(false);
    }

    private void Start()
    {
        // Try again in Start if Awake failed (camera might not exist yet)
        if (targetCamera == null)
        {
            FindCamera();
        }
        
        isInitialized = true;
        
        // Apply rotation immediately on start
        if (targetCamera != null)
        {
            ApplyBillboardRotation(false); // Instant on first frame
        }
    }

    private void FindCamera(bool forceRefresh = false)
    {
        if (!forceRefresh && IsTargetCameraUsable())
            return;

        targetCamera = SceneCameraResolver.ResolveFor(gameObject);
        
        if (targetCamera == null && enableDebugLogs)
        {
            Debug.LogWarning($"[SpriteBillboard] No camera found for {gameObject.name}! Billboard will not work until camera is assigned.");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[SpriteBillboard] Found camera: {targetCamera.name} for {gameObject.name}");
        }
    }

    private void LateUpdate()
    {
        // Persistent billboards must replace the destroyed camera from the
        // previous scene, and disabled cinematic cameras must not remain bound.
        if (!IsTargetCameraUsable())
        {
            FindCamera(forceRefresh: true);
            if (targetCamera == null) return;
        }

        ApplyBillboardRotation(smoothRotation);
    }

    private void ApplyBillboardRotation(bool smooth)
    {
        if (targetCamera == null) return;

        Quaternion targetWorldRotation = CalculateTargetRotation();
        Quaternion targetLocalRotation = transform.parent != null
            ? Quaternion.Inverse(transform.parent.rotation) * targetWorldRotation
            : targetWorldRotation;

        if (smooth && isInitialized)
        {
            // Smoothly rotate towards camera
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation,
                targetLocalRotation,
                rotationSpeed * Time.deltaTime);
        }
        else
        {
            // Instant rotation
            transform.localRotation = targetLocalRotation;
        }

        if (enableDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"[SpriteBillboard] {gameObject.name} rotation: {transform.rotation.eulerAngles}, target: {targetWorldRotation.eulerAngles}");
        }
    }

    private Quaternion CalculateTargetRotation()
    {
        Vector3 forward;
        Vector3 up = Vector3.up;

        switch (mode)
        {
            case BillboardMode.FaceCamera:
                // Face the camera's position directly
                Vector3 directionToCamera = targetCamera.transform.position - transform.position;
                
                forward = -directionToCamera.normalized;
                break;
                
            case BillboardMode.FaceCameraForward:
                // Face opposite to camera's forward direction
                forward = targetCamera.transform.forward;
                
                up = targetCamera.transform.up;
                break;
                
            case BillboardMode.FaceCameraPlane:
            default:
                // Face parallel to camera's view plane
                forward = targetCamera.transform.forward;
                up = targetCamera.transform.up;
                break;
        }

        if (lockYRotation)
        {
            // A world/local Euler-Y freeze makes a billboard turn edge-on when
            // a camera zone rotates the rig. The useful lock for exploration
            // sprites is camera-relative: remove per-object horizontal parallax
            // while retaining the active camera plane's cardinal yaw.
            Vector3 cameraPlanarForward = Vector3.ProjectOnPlane(
                targetCamera.transform.forward,
                Vector3.up);
            if (cameraPlanarForward.sqrMagnitude > 0.001f)
            {
                cameraPlanarForward.Normalize();
                float vertical = Mathf.Clamp(forward.normalized.y, -0.999f, 0.999f);
                float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - vertical * vertical));
                forward = cameraPlanarForward * horizontal + Vector3.up * vertical;
            }
        }

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward, up);
    }

    private bool IsTargetCameraUsable()
    {
        return SceneCameraResolver.IsUsableFor(targetCamera, gameObject);
    }

    /// <summary>
    /// Set the target camera for billboarding.
    /// </summary>
    public void SetTargetCamera(Camera camera)
    {
        targetCamera = camera;
        if (targetCamera != null && isActiveAndEnabled)
            ApplyBillboardRotation(false);
        if (enableDebugLogs && camera != null)
        {
            Debug.Log($"[SpriteBillboard] Camera set to: {camera.name}");
        }
    }

    /// <summary>
    /// Get the current target camera.
    /// </summary>
    public Camera GetTargetCamera()
    {
        return targetCamera;
    }

    public bool HasTargetCamera => targetCamera != null;

    /// <summary>
    /// Force refresh camera reference.
    /// </summary>
    public void RefreshCamera()
    {
        targetCamera = null;
        FindCamera(forceRefresh: true);
        if (targetCamera != null && isActiveAndEnabled)
            ApplyBillboardRotation(false);
    }

    /// <summary>Enable or disable preservation of local Y rotation.</summary>
    public void SetYRotationLocked(bool locked)
    {
        lockYRotation = locked;
        if (targetCamera != null && isActiveAndEnabled)
            ApplyBillboardRotation(false);
    }

    public bool IsYRotationLocked => lockYRotation;

    /// <summary>
    /// Set the billboard mode at runtime.
    /// </summary>
    public void SetMode(BillboardMode newMode)
    {
        mode = newMode;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Immediately apply rotation in editor when settings change
        if (!Application.isPlaying && targetCamera != null)
        {
            ApplyBillboardRotation(false);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (targetCamera == null) return;
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, targetCamera.transform.position);
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }
#endif
}
