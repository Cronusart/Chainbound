using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Marks a directional light as the indoor light for this scene.
/// Caches scene-authored rotation IMMEDIATELY in Awake (before any time-of-day systems can modify it).
/// TimeOfDayVisualApplier will find and use this component for indoor lighting.
/// 
/// Setup:
/// 1. Add this component to your indoor directional light GameObject
/// 2. Assign the IndoorVisualProfileSO
/// 3. The visual applier will automatically find this and use its cached rotation
/// </summary>
[RequireComponent(typeof(Light))]
[ExecuteAlways]
public class IndoorDirectionalLight : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Indoor visual profile for this light")]
    [SerializeField] private IndoorVisualProfileSO profile;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Cached data
    private Light directionalLight;
    private Quaternion sceneAuthoredRotation;
    private Vector3 sceneAuthoredPosition;
    private bool dataCached = false;
    
    // Static registry for quick lookup
    private static IndoorDirectionalLight activeIndoorLight;
    
    // ============================================
    // PUBLIC ACCESSORS
    // ============================================
    
    /// <summary>The directional light component.</summary>
    public Light Light => directionalLight;
    
    /// <summary>The scene-authored rotation (cached at Awake, before any modifications).</summary>
    public Quaternion SceneAuthoredRotation => sceneAuthoredRotation;
    
    /// <summary>The scene-authored position (cached at Awake).</summary>
    public Vector3 SceneAuthoredPosition => sceneAuthoredPosition;
    
    /// <summary>Whether the scene-authored data has been cached.</summary>
    public bool IsDataCached => dataCached;
    
    /// <summary>The indoor visual profile for this light.</summary>
    public IndoorVisualProfileSO Profile => profile;
    
    /// <summary>Get the currently active indoor directional light (if any).</summary>
    public static IndoorDirectionalLight ActiveIndoorLight => activeIndoorLight;

    /// <summary>
    /// Find the indoor directional light that belongs to a specific scene.
    /// Prefers the registered active light when it lives in that scene.
    /// </summary>
    public static IndoorDirectionalLight FindForScene(Scene scene)
    {
        if (!scene.IsValid())
            return activeIndoorLight;

        if (activeIndoorLight != null && activeIndoorLight.gameObject.scene == scene)
            return activeIndoorLight;

        IndoorDirectionalLight[] lights = FindObjectsByType<IndoorDirectionalLight>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            IndoorDirectionalLight light = lights[i];
            if (light != null && light.gameObject.scene == scene)
                return light;
        }

        return null;
    }
    
    // ============================================
    // LIFECYCLE
    // ============================================
    
    private void Awake()
    {
        directionalLight = GetComponent<Light>();
        
        if (directionalLight == null)
        {
            Debug.LogError($"[IndoorDirectionalLight] No Light component found on {gameObject.name}!");
            return;
        }
        
        if (directionalLight.type != LightType.Directional)
        {
            Debug.LogError($"[IndoorDirectionalLight] Light on {gameObject.name} is not a Directional light!");
            return;
        }
        
        // ============================================
        // CRITICAL: Cache scene-authored values IMMEDIATELY
        // This happens before ANY other system (TimeOfDayManager, etc.) can modify it
        // ============================================
        CacheSceneAuthoredValues();
    }
    
    private void OnEnable()
    {
        // Register as active indoor light
        if (activeIndoorLight != null && activeIndoorLight != this)
        {
            Debug.LogWarning($"[IndoorDirectionalLight] Multiple indoor lights active! " +
                           $"Existing: {activeIndoorLight.gameObject.name} (scene: {activeIndoorLight.gameObject.scene.name}), " +
                           $"New: {gameObject.name} (scene: {gameObject.scene.name}). Using new one.");
        }
        
        activeIndoorLight = this;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[IndoorDirectionalLight] {gameObject.name} registered as active indoor light. " +
                     $"Cached rotation: {sceneAuthoredRotation.eulerAngles}");
        }
    }
    
    private void OnDisable()
    {
        if (activeIndoorLight == this)
        {
            activeIndoorLight = null;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[IndoorDirectionalLight] {gameObject.name} unregistered as active indoor light.");
            }
        }
    }
    
    // ============================================
    // CACHING
    // ============================================
    
    /// <summary>
    /// Cache the scene-authored transform values.
    /// Called in Awake BEFORE any other system can modify them.
    /// </summary>
    private void CacheSceneAuthoredValues()
    {
        sceneAuthoredRotation = transform.rotation;
        sceneAuthoredPosition = transform.position;
        dataCached = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[IndoorDirectionalLight] {gameObject.name}: Cached scene-authored values:" +
                     $"\n  Rotation: {sceneAuthoredRotation.eulerAngles}" +
                     $"\n  Position: {sceneAuthoredPosition}");
        }
    }
    
    /// <summary>
    /// Apply the scene-authored rotation to the light.
    /// Call this to restore the original rotation if it was modified.
    /// </summary>
    public void ApplySceneAuthoredRotation()
    {
        if (!dataCached)
        {
            Debug.LogWarning($"[IndoorDirectionalLight] {gameObject.name}: Cannot apply scene-authored rotation - not cached!");
            return;
        }
        
        transform.rotation = sceneAuthoredRotation;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[IndoorDirectionalLight] {gameObject.name}: Applied scene-authored rotation: {sceneAuthoredRotation.eulerAngles}");
        }
    }
    
    /// <summary>
    /// Apply all indoor profile settings to this light.
    /// </summary>
    public void ApplyProfileSettings()
    {
        if (directionalLight == null || profile == null) return;
        
        directionalLight.intensity = profile.IndoorDirectionalIntensity;
        directionalLight.color = profile.IndoorDirectionalColor;
        directionalLight.shadowStrength = profile.IndoorShadowStrength;
        
        // Apply rotation based on profile setting
        if (profile.UseSceneAuthoredRotation && dataCached)
        {
            transform.rotation = sceneAuthoredRotation;
        }
        else if (!profile.UseSceneAuthoredRotation)
        {
            transform.rotation = Quaternion.Euler(profile.IndoorDirectionalRotation);
        }
        
        if (enableDebugLogs)
        {
            string rotationSource = profile.UseSceneAuthoredRotation ? "scene-authored" : "profile";
            Debug.Log($"[IndoorDirectionalLight] Applied profile settings: intensity={profile.IndoorDirectionalIntensity}, rotation source={rotationSource}");
        }
    }
    
    // ============================================
    // EDITOR
    // ============================================
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f); // Warm yellow for indoor light
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        
        // Draw light direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);
    }
#endif
}
