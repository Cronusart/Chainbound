using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Night accent object that can drive emissive materials and an optional point light during night.
/// Revised: Point light can retain authored editor settings (default behavior).
/// </summary>
public class NightAccentObject : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private NightAccentConfigSO config;

    [Header("Group Selection")]
    [SerializeField] private NightAccentType groupType = NightAccentType.Custom;
    [SerializeField] private string groupName = "";

    [Header("Rendering")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private bool autoFindRenderer = true;
    [SerializeField] private string emissionPropertyName = "_EmissionColor";

    [Header("Point Light (Optional - Reference)")]
    [Tooltip("Pre-placed point light (recommended: child of the fixture).")]
    [SerializeField] private Light referencedPointLight;

    [Tooltip("If true and no referencedPointLight is assigned, attempts to find a Light in children on Awake.")]
    [SerializeField] private bool autoFindPointLightInChildren = false;

    [Header("Point Light Behavior")]
    [Tooltip("If ON, the script will use the Light's authored Color/Range/Intensity as the baseline and only multiply intensity by night fade.\n" +
             "If OFF, the script will override the Light using the NightAccentGroup PointLight settings (legacy behavior).")]
    [SerializeField] private bool retainAuthoredPointLightSettings = true;

    [Tooltip("When retaining authored settings, multiply authored intensity by this group scalar (optional).")]
    [SerializeField] private bool multiplyAuthoredIntensityByGroupScalar = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    [Header("Conflict Prevention")]
    [Tooltip("If true, disables any EmissiveGroup on this object/children to prevent both systems from controlling the same light.")]
    [SerializeField] private bool disableConflictingEmissiveGroups = true;

    private NightAccentGroup currentGroup;
    private MaterialPropertyBlock mpb;
    private float objectSeed;
    private bool isActive;
    private float currentFade;

    private int emissionColorId;

    // Cached authored point light settings (to prevent runtime "reset" issues)
    private bool hasCachedLightSettings;
    private Color authoredColor;
    private float authoredRange;
    private float authoredIntensity;
    private LightShadows authoredShadows;
    private LightRenderMode authoredRenderMode;
    
    // Subscription tracking
    private bool isSubscribed;
    private ITimeOfDayService subscribedService;

    private void Awake()
    {
        emissionColorId = Shader.PropertyToID(emissionPropertyName);

        objectSeed = transform.position.x * 100f + transform.position.z * 10f + transform.position.y;

        if (targetRenderer == null && autoFindRenderer)
        {
            targetRenderer = GetComponent<Renderer>();
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            mpb = new MaterialPropertyBlock();
        }

        RefreshGroup();

        if (referencedPointLight == null && autoFindPointLightInChildren)
        {
            referencedPointLight = GetComponentInChildren<Light>(includeInactive: true);
        }

        CacheAuthoredPointLightSettings();
        
        // Detect and handle conflicts with EmissiveGroup system
        HandleEmissiveGroupConflicts();

        // Start OFF until first time update
        if (referencedPointLight != null)
        {
            referencedPointLight.enabled = false;
            referencedPointLight.intensity = 0f;
        }
    }
    
    /// <summary>
    /// Detects if an EmissiveGroup component is also trying to control our referenced point light
    /// and optionally disables it to prevent flashing conflicts.
    /// </summary>
    private void HandleEmissiveGroupConflicts()
    {
        if (referencedPointLight == null) return;
        
        bool foundConflict = false;
        
        // Check for EmissiveGroup on this object or parents
        EmissiveGroup[] emissiveGroups = GetComponentsInChildren<EmissiveGroup>(true);
        EmissiveGroup parentGroup = GetComponentInParent<EmissiveGroup>();
        
        if (parentGroup != null)
        {
            foundConflict |= CheckEmissiveGroupConflict(parentGroup);
        }
        
        foreach (var group in emissiveGroups)
        {
            foundConflict |= CheckEmissiveGroupConflict(group);
        }
        
        // Also check if a NightAccentLightingDriver exists and might be controlling lights
        if (foundConflict)
        {
            NightAccentLightingDriver driver = FindObjectOfType<NightAccentLightingDriver>();
            if (driver != null && driver.enabled)
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: A NightAccentLightingDriver is active in the scene. " +
                                $"If the light still flashes, either:\n" +
                                $"  1. Remove the EmissiveGroup component from '{gameObject.name}' or its parents\n" +
                                $"  2. Clear the 'Attached Point Light' field on the EmissiveGroup\n" +
                                $"  3. Remove this object from the NightAccentLightingDriver's emissiveGroups list\n" +
                                $"  4. Disable the NightAccentLightingDriver if using NightAccentObject for all lights");
            }
        }
    }
    
    private bool CheckEmissiveGroupConflict(EmissiveGroup group)
    {
        if (group == null) return false;
        
        // Check if this EmissiveGroup has the same light attached
        Light groupLight = group.AttachedLight;
        
        if (groupLight != null && groupLight == referencedPointLight)
        {
            Debug.LogWarning($"[NightAccentObject] {gameObject.name}: CONFLICT DETECTED! " +
                            $"EmissiveGroup on '{group.gameObject.name}' also controls our point light!");
            
            if (disableConflictingEmissiveGroups)
            {
                // Disable the EmissiveGroup to prevent fighting over the light
                group.enabled = false;
                
                Debug.Log($"[NightAccentObject] {gameObject.name}: Disabled conflicting EmissiveGroup on '{group.gameObject.name}'");
            }
            else
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: Light is controlled by BOTH NightAccentObject AND EmissiveGroup! " +
                                $"This will cause flashing. Either set 'disableConflictingEmissiveGroups=true' or remove the EmissiveGroup reference to this light.");
            }
            
            return true;
        }
        
        return false;
    }

    private void Start()
    {
        SubscribeToTimeService();
    }
    
    private void OnEnable()
    {
        // Subscribe to scene loaded events for reconnection after scene transitions
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Subscribe to TimeOfDayManager availability event
        TimeOfDayManager.OnInstanceAvailable += OnTimeManagerAvailable;
        
        // Try to subscribe if not already
        if (!isSubscribed)
        {
            SubscribeToTimeService();
        }
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        UnsubscribeFromTimeService();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        UnsubscribeFromTimeService();
    }
    
    /// <summary>
    /// Subscribe to the TimeOfDayManager time change event.
    /// </summary>
    private void SubscribeToTimeService()
    {
        if (isSubscribed) return;
        
        var manager = TimeOfDayManager.Instance;
        if (manager != null)
        {
            manager.OnTimeNormalizedChanged += OnTimeChanged;
            subscribedService = manager;
            isSubscribed = true;
            
            // Apply current time immediately
            OnTimeChanged(manager.NormalizedTime);
            
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name} subscribed to TimeOfDayManager");
            }
        }
        else
        {
            if (showDebug)
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: TimeOfDayManager.Instance is null, will retry on availability event");
            }
            
            // Start delayed retry coroutine
            if (Application.isPlaying)
            {
                StartCoroutine(DelayedSubscriptionRetry());
            }
        }
    }
    
    /// <summary>
    /// Unsubscribe from the time service.
    /// </summary>
    private void UnsubscribeFromTimeService()
    {
        if (!isSubscribed) return;
        
        if (subscribedService != null)
        {
            subscribedService.OnTimeNormalizedChanged -= OnTimeChanged;
        }
        
        subscribedService = null;
        isSubscribed = false;
    }
    
    /// <summary>
    /// Called when TimeOfDayManager broadcasts its availability.
    /// </summary>
    private void OnTimeManagerAvailable(TimeOfDayManager manager)
    {
        if (manager == null) return;
        
        if (!isSubscribed)
        {
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name} received OnInstanceAvailable, subscribing...");
            }
            
            SubscribeToTimeService();
        }
    }
    
    /// <summary>
    /// Called when a scene loads. Ensures we're connected to the time service.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Check if our subscription is still valid
        bool needsResubscribe = !isSubscribed || subscribedService == null;
        
        if (!needsResubscribe && subscribedService is MonoBehaviour mb)
        {
            needsResubscribe = mb == null; // Check for zombie
        }
        
        if (needsResubscribe)
        {
            isSubscribed = false;
            subscribedService = null;
            SubscribeToTimeService();
        }
    }
    
    /// <summary>
    /// Delayed retry for subscription (handles DontDestroyOnLoad timing issues).
    /// </summary>
    private System.Collections.IEnumerator DelayedSubscriptionRetry()
    {
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        
        if (!isSubscribed)
        {
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name} attempting delayed subscription...");
            }
            
            SubscribeToTimeService();
        }
    }

    private void CacheAuthoredPointLightSettings()
    {
        if (referencedPointLight == null) return;

        authoredColor = referencedPointLight.color;
        authoredRange = referencedPointLight.range;
        authoredIntensity = referencedPointLight.intensity;
        authoredShadows = referencedPointLight.shadows;
        authoredRenderMode = referencedPointLight.renderMode;

        hasCachedLightSettings = true;
        
        if (showDebug)
        {
            Debug.Log($"[NightAccentObject] {gameObject.name} CACHED light settings:" +
                     $"\n  Color: {authoredColor}" +
                     $"\n  Range: {authoredRange}" +
                     $"\n  Intensity: {authoredIntensity}" +
                     $"\n  Shadows: {authoredShadows}");
        }
    }

    private void OnTimeChanged(float normalizedTime)
    {
        if (config == null)
        {
            if (showDebug)
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: config is NULL! Cannot process time change.");
            }
            return;
        }
        
        if (currentGroup == null)
        {
            if (showDebug)
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: currentGroup is NULL! Trying RefreshGroup...");
            }
            RefreshGroup();
            if (currentGroup == null)
            {
                Debug.LogError($"[NightAccentObject] {gameObject.name}: Failed to get a group from config! " +
                              $"groupType={groupType}, groupName='{groupName}'");
                return;
            }
        }

        isActive = config.ShouldBeActive(normalizedTime, currentGroup);
        currentFade = config.GetFadeProgress(normalizedTime, currentGroup);

        float flicker = currentGroup.EnableFlicker ? currentGroup.GetFlickerMultiplier(objectSeed) : 1f;
        float finalIntensity01 = currentFade * flicker;

        if (showDebug)
        {
            Debug.Log($"[NightAccentObject] {gameObject.name} TIME UPDATE:" +
                     $"\n  Time: {normalizedTime:F3}" +
                     $"\n  Group: {currentGroup.GroupName}" +
                     $"\n  EnablePointLights: {currentGroup.EnablePointLights}" +
                     $"\n  isActive: {isActive}" +
                     $"\n  currentFade: {currentFade:F3}" +
                     $"\n  finalIntensity01: {finalIntensity01:F3}");
        }

        UpdateEmission(finalIntensity01);
        UpdatePointLight(finalIntensity01);
    }

    private void UpdateEmission(float intensity01)
    {
        if (targetRenderer == null || mpb == null || currentGroup == null) return;

        Color emission = currentGroup.EmissionColor * currentGroup.EmissionIntensity * intensity01;

        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(emissionColorId, emission);
        targetRenderer.SetPropertyBlock(mpb);
    }

    private void UpdatePointLight(float intensity01)
    {
        if (referencedPointLight == null)
        {
            if (showDebug)
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: referencedPointLight is NULL!");
            }
            return;
        }
        
        if (currentGroup == null)
        {
            if (showDebug)
            {
                Debug.LogWarning($"[NightAccentObject] {gameObject.name}: currentGroup is NULL in UpdatePointLight!");
            }
            return;
        }

        // If group doesn't use point lights, keep it off.
        if (!currentGroup.EnablePointLights)
        {
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: Group '{currentGroup.GroupName}' has EnablePointLights=FALSE, keeping light off.");
            }
            if (referencedPointLight.enabled) referencedPointLight.enabled = false;
            if (referencedPointLight.intensity != 0f) referencedPointLight.intensity = 0f;
            return;
        }

        // Ensure we have authored settings cached
        if (!hasCachedLightSettings)
        {
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: Caching light settings (wasn't cached before)...");
            }
            CacheAuthoredPointLightSettings();
        }

        bool shouldEnable = intensity01 > 0.01f;
        
        // Only change enabled state if it differs - avoids potential Unity update issues
        if (referencedPointLight.enabled != shouldEnable)
        {
            referencedPointLight.enabled = shouldEnable;
        }

        if (!shouldEnable)
        {
            if (referencedPointLight.intensity != 0f)
            {
                referencedPointLight.intensity = 0f;
            }
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: intensity01={intensity01:F3} < 0.01, light DISABLED");
            }
            return;
        }

        if (retainAuthoredPointLightSettings)
        {
            // Keep what you authored in-editor.
            referencedPointLight.color = authoredColor;
            referencedPointLight.range = authoredRange;
            referencedPointLight.shadows = authoredShadows;
            referencedPointLight.renderMode = authoredRenderMode;

            float scalar = 1f;
            if (multiplyAuthoredIntensityByGroupScalar)
                scalar = currentGroup.PointLightIntensity; // treat as a multiplier, not an override

            float finalIntensity = authoredIntensity * scalar * intensity01;
            referencedPointLight.intensity = finalIntensity;
            
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: LIGHT ENABLED (retain authored)" +
                         $"\n  authoredIntensity: {authoredIntensity}" +
                         $"\n  scalar (group intensity): {scalar}" +
                         $"\n  intensity01: {intensity01:F3}" +
                         $"\n  FINAL intensity: {finalIntensity:F3}" +
                         $"\n  authoredRange: {authoredRange}" +
                         $"\n  authoredColor: {authoredColor}");
            }
        }
        else
        {
            // Legacy behavior: config overrides the light.
            referencedPointLight.color = currentGroup.PointLightColor;
            referencedPointLight.range = currentGroup.PointLightRange;
            referencedPointLight.intensity = currentGroup.PointLightIntensity * intensity01;

            // Keep shadows off unless you explicitly want them.
            if (referencedPointLight.shadows != LightShadows.None)
                referencedPointLight.shadows = LightShadows.None;
                
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: LIGHT ENABLED (legacy override)" +
                         $"\n  group intensity: {currentGroup.PointLightIntensity}" +
                         $"\n  group range: {currentGroup.PointLightRange}" +
                         $"\n  group color: {currentGroup.PointLightColor}");
            }
        }
    }

    public void RefreshGroup()
    {
        if (config == null)
        {
            if (showDebug)
            {
                Debug.LogError($"[NightAccentObject] {gameObject.name}: RefreshGroup called but config is NULL!");
            }
            return;
        }

        if (!string.IsNullOrEmpty(groupName))
        {
            currentGroup = config.GetGroup(groupName);
            if (showDebug && currentGroup != null)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: Found group by name '{groupName}'");
            }
        }

        if (currentGroup == null && groupType != NightAccentType.Custom)
        {
            currentGroup = config.GetGroup(groupType);
            if (showDebug && currentGroup != null)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: Found group by type '{groupType}'");
            }
        }

        if (currentGroup == null)
        {
            currentGroup = config.GetDefaultGroup();
            if (showDebug)
            {
                if (currentGroup != null)
                {
                    Debug.Log($"[NightAccentObject] {gameObject.name}: Using default group '{currentGroup.GroupName}'");
                }
                else
                {
                    Debug.LogError($"[NightAccentObject] {gameObject.name}: No group found! Config has no groups!");
                }
            }
        }

        if (currentGroup != null)
        {
            currentGroup.InitializeFlicker();
            
            if (showDebug)
            {
                Debug.Log($"[NightAccentObject] {gameObject.name}: Group settings:" +
                         $"\n  GroupName: {currentGroup.GroupName}" +
                         $"\n  Enabled: {currentGroup.Enabled}" +
                         $"\n  EnablePointLights: {currentGroup.EnablePointLights}" +
                         $"\n  PointLightIntensity: {currentGroup.PointLightIntensity}" +
                         $"\n  PointLightRange: {currentGroup.PointLightRange}");
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // During edit time, keep the reference populated if requested
        if (!Application.isPlaying && autoFindPointLightInChildren && referencedPointLight == null)
        {
            referencedPointLight = GetComponentInChildren<Light>(includeInactive: true);
        }

        // If user changes the referenced light in editor, refresh cached authored settings.
        if (!Application.isPlaying && referencedPointLight != null)
        {
            // Re-cache so playmode uses the correct authored baseline.
            authoredColor = referencedPointLight.color;
            authoredRange = referencedPointLight.range;
            authoredIntensity = referencedPointLight.intensity;
            authoredShadows = referencedPointLight.shadows;
            authoredRenderMode = referencedPointLight.renderMode;
            hasCachedLightSettings = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showDebug) return;

        if (referencedPointLight != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(referencedPointLight.transform.position, 0.2f);
        }
    }
#endif
}
