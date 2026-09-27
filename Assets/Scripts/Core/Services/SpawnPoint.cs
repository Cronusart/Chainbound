using UnityEngine;

/// <summary>
/// Scene object that registers itself as a spawn point.
/// Player spawns here when scene loads with matching spawnId.
/// 
/// Setup:
/// 1. Place in scene at desired spawn location
/// 2. Set spawnId (e.g., "bedroom_start", "loomville_from_bedroom")
/// 3. Set lighting type (Indoor/Outdoor) for automatic lighting configuration
/// </summary>
public class SpawnPoint : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Unique spawn ID for this point")]
    [SerializeField] private string spawnId = "";
    
    [Tooltip("Spawn rotation (Y axis)")]
    [SerializeField] private float spawnRotationY = 0f;

    [Tooltip("When using ConfigureFromPlayer, place the spawn point this far above the player's root (reduces fall-through / mesh intersection).")]
    [SerializeField] private float runtimeVerticalOffsetFromPlayer = 0.08f;
    
    [Header("Player Facing Direction")]
    [Tooltip("Direction player should face when spawning (relative to camera orientation). " +
             "None = keep current facing direction. " +
             "Up/Down/Left/Right = face that direction relative to camera (WASD/joystick directions).")]
    [SerializeField] private SpawnFacingDirection facingDirection = SpawnFacingDirection.None;
    
    public enum SpawnFacingDirection
    {
        [Tooltip("Keep current facing direction (don't change)")]
        None,
        [Tooltip("Face Up (W key / joystick up)")]
        Up,
        [Tooltip("Face Down (S key / joystick down)")]
        Down,
        [Tooltip("Face Left (A key / joystick left)")]
        Left,
        [Tooltip("Face Right (D key / joystick right)")]
        Right
    }
    
    [Header("Lighting Configuration")]
    [Tooltip("Lighting type for this spawn point. Determines how time-of-day visuals are applied.")]
    [SerializeField] private SceneLightingType lightingType = SceneLightingType.Outdoor;
    
    /// <summary>Lighting type for scene transitions.</summary>
    public enum SceneLightingType
    {
        [Tooltip("Outdoor: Time-of-day drives the directional sun light")]
        Outdoor,
        [Tooltip("Indoor: Use indoor directional light with fixed/scene-authored rotation")]
        Indoor
    }
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmo = true;
    
    /// <summary>Spawn ID for this point.</summary>
    public string SpawnId => spawnId;
    
    /// <summary>World position of this spawn point.</summary>
    public Vector3 Position => transform.position;
    
    /// <summary>Rotation of this spawn point.</summary>
    public Quaternion Rotation => Quaternion.Euler(0f, spawnRotationY, 0f);
    
    /// <summary>Facing direction for player when spawning.</summary>
    public SpawnFacingDirection FacingDirection => facingDirection;
    
    /// <summary>Lighting type for this spawn point (Indoor/Outdoor).</summary>
    public SceneLightingType LightingType => lightingType;
    
    /// <summary>Whether this is an indoor spawn point.</summary>
    public bool IsIndoor => lightingType == SceneLightingType.Indoor;

    /// <summary>
    /// Reconfigure this spawn point from the current player transform for dynamic return placement.
    /// </summary>
    public void ConfigureFromPlayer(GameObject player, bool isIndoor)
    {
        if (player == null)
        {
            Debug.LogWarning($"[SpawnPoint] Cannot configure '{spawnId}' from a null player.");
            return;
        }

        float offsetY = Mathf.Max(0f, runtimeVerticalOffsetFromPlayer);
        transform.position = player.transform.position + Vector3.up * offsetY;
        spawnRotationY = player.transform.eulerAngles.y;
        facingDirection = SpawnFacingDirection.None;
        lightingType = isIndoor ? SceneLightingType.Indoor : SceneLightingType.Outdoor;
    }

    /// <summary>
    /// Configures a persistent, runtime-created spawn point without exposing
    /// scene serialization details to services that need a safe fallback.
    /// </summary>
    public void ConfigureRuntimeSpawnId(string id, bool isIndoor = false)
    {
        spawnId = id?.Trim() ?? string.Empty;
        spawnRotationY = transform.eulerAngles.y;
        facingDirection = SpawnFacingDirection.None;
        lightingType = isIndoor ? SceneLightingType.Indoor : SceneLightingType.Outdoor;
    }

    /// <summary>
    /// Force this spawn point to refresh its SpawnService registration after a runtime move.
    /// </summary>
    public void ForceReRegister()
    {
        SpawnService spawnService = SpawnService.Instance;
        if (spawnService == null)
            return;

        spawnService.UnregisterSpawnPoint(this);
        spawnService.RegisterSpawnPoint(this);
    }
    
    private void OnEnable()
    {
        // Register with spawn service
        // Use StartCoroutine to ensure SpawnService is ready
        StartCoroutine(RegisterWhenReady());
    }
    
    private System.Collections.IEnumerator RegisterWhenReady()
    {
        // Wait a frame to ensure SpawnService is initialized
        yield return null;
        
        // Warn if spawn point is a child of another GameObject
        // Spawn points should ideally be at root level to avoid confusion between local and world positions
        if (transform.parent != null)
        {
            Debug.LogWarning($"[SpawnPoint] Spawn point '{spawnId}' on {gameObject.name} is a child of '{transform.parent.name}'. " +
                           $"World position ({transform.position}) will be used for spawning, not local position ({transform.localPosition}). " +
                           $"Consider moving spawn point to root level if this causes issues.");
        }
        
        SpawnService spawnService = SpawnService.Instance;
        if (spawnService != null)
        {
            spawnService.RegisterSpawnPoint(this);
        }
        else
        {
            Debug.LogWarning($"[SpawnPoint] SpawnService.Instance is null when trying to register '{spawnId}' on {gameObject.name}. Spawn point may not work correctly.");
        }
    }
    
    private void OnDisable()
    {
        // Unregister from spawn service
        SpawnService spawnService = SpawnService.Instance;
        if (spawnService != null)
        {
            spawnService.UnregisterSpawnPoint(this);
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmo) return;
        
        // Draw spawn point indicator
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw forward direction
        Gizmos.color = Color.cyan;
        Vector3 forward = Rotation * Vector3.forward;
        Gizmos.DrawRay(transform.position, forward * 1f);
        
        // Draw label
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.7f, spawnId);
        #endif
    }
#endif
}
