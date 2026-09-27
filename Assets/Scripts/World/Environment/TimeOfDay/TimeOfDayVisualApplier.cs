using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Unified component that applies time-of-day visuals (directional light, ambient, fog, post-processing)
/// with support for indoor/outdoor switching.
/// 
/// Setup:
/// 1. Add one instance per scene (or persistent services scene)
/// 2. Assign TimeOfDayManager reference (or uses singleton)
/// 3. Assign TimeOfDayConfigSO
/// 4. Optionally assign IndoorVisualProfileSO for indoor lighting
/// 5. Use IndoorZoneTrigger components to mark indoor areas
/// </summary>
[ExecuteAlways]
public class TimeOfDayVisualApplier : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Time source override (optional). Leave empty to use TimeOfDayManager singleton. " +
             "NOTE: Cross-scene references will be lost on scene reload - prefer using singleton.")]
    [SerializeField] private MonoBehaviour timeSourceOverride;
    
    [Tooltip("Time-of-day configuration (required)")]
    [SerializeField] private TimeOfDayConfigSO config;
    
    [Tooltip("OUTDOOR directional light (sun). This is the light driven by time-of-day.")]
    [SerializeField] private Light directionalLight;
    
    [Header("Indoor Directional Light")]
    [Tooltip("INDOOR directional light. If null, uses directionalLight field for both (not recommended).")]
    [SerializeField] private Light indoorDirectionalLight;
    
    [Tooltip("If true, directional lights MUST be explicitly assigned (no auto-find). Recommended for indoor scenes.")]
    [SerializeField] private bool requireExplicitDirectionalLight = false;
    
    [Header("Apply Toggles")]
    [Tooltip("Apply fog settings")]
    [SerializeField] private bool applyFog = true;
    
    [Tooltip("Apply ambient light settings")]
    [SerializeField] private bool applyAmbient = true;
    
    [Tooltip("Apply directional light settings")]
    [SerializeField] private bool applyDirectional = true;
    
    [Header("URP Volume")]
    [Tooltip("Global volume for post-processing. Auto-finds if null.")]
    [SerializeField] private Volume globalVolume;
    
    [Tooltip("Blend between day/night volume profiles if config has them")]
    [SerializeField] private bool blendDayNightVolumes = true;
    
    [Header("Indoor Control")]
    [Tooltip("Start indoors (for testing or initial state)")]
    [SerializeField] private bool startIndoors = false;
    
    [Tooltip("Indoor visual profile (optional - if null, just disables outdoor effects)")]
    [SerializeField] private IndoorVisualProfileSO indoorProfile;
    
    [Header("Directional Light Settings")]
    [Tooltip("Base intensity multiplier")]
    [SerializeField] private float intensityMultiplier = 1f;
    
    [Tooltip("Minimum intensity (prevents complete darkness)")]
    [SerializeField] private float minIntensity = 0.05f;
    
    [Tooltip("Base Y rotation (azimuth) of the sun")]
    [SerializeField] private float baseYawAngle = -30f;
    
    [Tooltip("Base Z rotation (roll) of the sun")]
    [SerializeField] private float baseRollAngle = 0f;
    
    [Header("Fog Settings")]
    [Tooltip("Fog mode")]
    [SerializeField] private FogMode fogMode = FogMode.ExponentialSquared;
    
    [Tooltip("Linear fog start distance (if using Linear mode)")]
    [SerializeField] private float linearFogStart = 10f;
    
    [Tooltip("Linear fog end distance (if using Linear mode)")]
    [SerializeField] private float linearFogEnd = 100f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    [Tooltip("Enable detailed lighting ownership debug logs")]
    [SerializeField] private bool debugLighting = false;
    
    // Runtime state
    private ITimeOfDayService timeService;
    private bool isIndoors;
    private bool isSubscribed = false;
    private float lastAppliedTime = -1f;
    private bool stateChanged = false;

    private readonly Dictionary<int, RegionFogOverlay> regionFogOverlays = new Dictionary<int, RegionFogOverlay>();

    private readonly struct RegionFogOverlay
    {
        public readonly float DensityMultiplier;
        public readonly Color Tint;
        public readonly int Priority;

        public RegionFogOverlay(float densityMultiplier, Color tint, int priority)
        {
            DensityMultiplier = Mathf.Max(0f, densityMultiplier);
            Tint = tint;
            Priority = priority;
        }
    }
    
    // ============================================
    // SINGLE WRITER ENFORCEMENT (A)
    // ============================================
    // Only ONE applier should actively mutate lighting at runtime.
    // This prevents conflicts when multiple appliers exist across scenes.
    private static TimeOfDayVisualApplier activeWriter;
    private static bool multipleAppliersWarningLogged = false;
    
    // Static tracking for old drivers to check indoor state (legacy)
    private static TimeOfDayVisualApplier activeInstance;
    
    // Cached outdoor values (for restoration when exiting indoors)
    private bool cachedFogEnabled;
    private float cachedFogDensity;
    private Color cachedFogColor;
    private Color cachedAmbientLight;
    private float cachedAmbientIntensity;
    private Color cachedDirectionalColor;
    private float cachedDirectionalIntensity;
    private RuntimeVolumeProfileHandle volumeProfileHandle;
    
    // ============================================
    // OUTDOOR SUN CACHING (C)
    // ============================================
    // Deterministic outdoor sun restoration - don't rely on RenderSettings.sun for lookup
    private Light cachedOutdoorSun;
    private bool cachedOutdoorSunEnabled;
    private Quaternion cachedOutdoorSunRotation;
    private bool hasOutdoorSunCached = false;
    
    // ============================================
    // INDOOR ROTATION CACHING (B)
    // ============================================
    // Cache scene-authored rotation on FIRST indoor entry, not in Awake
    private Quaternion cachedIndoorSceneAuthoredRotation;
    private bool hasIndoorRotationCached = false;
    private int cachedIndoorLightInstanceID = -1; // Track which light we cached from
    
    // ============================================
    // PROPERTIES
    // ============================================
    
    /// <summary>Is player currently indoors?</summary>
    public bool IsIndoors => isIndoors;
    
    /// <summary>Get the active visual applier instance (if any).</summary>
    public static TimeOfDayVisualApplier ActiveInstance => activeInstance;
    
    /// <summary>Check if any visual applier reports we're indoors.</summary>
    public static bool IsAnyIndoors => activeInstance != null && activeInstance.IsIndoors;
    
    // ============================================
    // LIFECYCLE
    // ============================================
    
    private void Awake()
    {
        // Resolve time source with robust null/zombie checking
        ResolveTimeService();
        
        // Handle directional light assignment
        if (applyDirectional)
        {
            if (requireExplicitDirectionalLight)
            {
                // Explicit mode: warn if lights are not assigned
                if (directionalLight == null)
                {
                    Debug.LogWarning($"[TimeOfDayVisualApplier] {gameObject.name}: requireExplicitDirectionalLight is true but directionalLight is not assigned! Outdoor visuals will not affect directional lighting.");
                }
                if (indoorDirectionalLight == null && indoorProfile != null)
                {
                    Debug.LogWarning($"[TimeOfDayVisualApplier] {gameObject.name}: requireExplicitDirectionalLight is true but indoorDirectionalLight is not assigned! Indoor scenes may have incorrect shadows.");
                }
            }
            else
            {
                // Auto-find mode (legacy behavior, not recommended for indoor scenes)
                if (directionalLight == null)
                {
                    directionalLight = GetComponent<Light>();
                    if (directionalLight == null || directionalLight.type != LightType.Directional)
                    {
                        Light[] lights = FindObjectsOfType<Light>();
                        foreach (var light in lights)
                        {
                            if (light.type == LightType.Directional)
                            {
                                directionalLight = light;
                                break;
                            }
                        }
                    }
                }
                
                // If no indoor directional assigned, fall back to main directional (not ideal)
                if (indoorDirectionalLight == null)
                {
                    indoorDirectionalLight = directionalLight;
                }
            }
        }
        
        // Auto-find global volume
        if (globalVolume == null)
        {
            globalVolume = GetComponent<Volume>();
            if (globalVolume == null || !globalVolume.isGlobal)
            {
                Volume[] volumes = FindObjectsOfType<Volume>();
                foreach (var vol in volumes)
                {
                    if (vol.isGlobal)
                    {
                        globalVolume = vol;
                        break;
                    }
                }
            }
        }
        
        // Cache initial outdoor values (fog, ambient, etc.)
        CacheOutdoorValues();
        
        // NOTE: Do NOT cache indoor rotation here - it's too early.
        // We cache it on FIRST indoor entry to ensure we get the correct light reference.
        
        // Set initial indoor state
        isIndoors = startIndoors;
        
        // Register as active instance (legacy for old drivers)
        activeInstance = this;
    }
    
    private void OnEnable()
    {
        // ============================================
        // SINGLE WRITER ENFORCEMENT (A)
        // ============================================
        if (Application.isPlaying)
        {
            if (!TryBecomeActiveWriter())
            {
                // Another applier is already the active writer
                return; // Don't subscribe or apply - we're a secondary applier
            }
        }
        
        // Subscribe to scene loaded events to refresh connections
        SceneManager.sceneLoaded += OnSceneLoaded;
        
        // Subscribe to TimeOfDayManager availability event (for reconnection after scene loads)
        TimeOfDayManager.OnInstanceAvailable += OnTimeManagerAvailable;
        
        // ============================================
        // RETRY TIME SERVICE RESOLUTION
        // ============================================
        // If timeService is null (failed in Awake or scene reload), try again
        if (timeService == null)
        {
            ResolveTimeService();
            
            if (enableDebugLogs)
            {
                if (timeService != null)
                {
                    Debug.Log($"[TimeOfDayVisualApplier] Time service resolved in OnEnable: {(timeService as MonoBehaviour)?.name}");
                }
                else
                {
                    Debug.LogWarning($"[TimeOfDayVisualApplier] Time service still null in OnEnable. Starting delayed retry...");
                }
            }
        }
        
        Subscribe();
        
        // Apply initial state (only if we're the active writer or in edit mode)
        if (timeService != null)
        {
            ApplyVisuals(timeService.NormalizedTime);
        }
        else
        {
            // Start delayed retry coroutine
            if (Application.isPlaying)
            {
                StartCoroutine(DelayedTimeServiceRetry());
            }
            
            if (enableDebugLogs)
            {
                Debug.LogWarning("[TimeOfDayVisualApplier] No timeService found on enable! Will retry after delay.");
            }
        }
    }
    
    /// <summary>
    /// Called when TimeOfDayManager broadcasts its availability.
    /// </summary>
    private void OnTimeManagerAvailable(TimeOfDayManager manager)
    {
        if (manager == null) return;
        
        // Check if we need to reconnect
        bool needsReconnect = timeService == null || (timeService is MonoBehaviour mb && mb == null);
        
        if (needsReconnect)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TimeOfDayVisualApplier] Received OnInstanceAvailable from {manager.gameObject.name}. Reconnecting...");
            }
            
            timeService = manager;
            Subscribe();
            
            if (IsActiveWriter())
            {
                ApplyVisuals(timeService.NormalizedTime);
            }
        }
    }
    
    /// <summary>
    /// Delayed retry for time service resolution.
    /// Waits a few frames then tries again, handling DontDestroyOnLoad timing issues.
    /// </summary>
    private System.Collections.IEnumerator DelayedTimeServiceRetry()
    {
        // Wait a few frames for DontDestroyOnLoad objects to be fully registered
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();
        
        if (timeService == null)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[TimeOfDayVisualApplier] Attempting delayed time service resolution...");
            }
            
            ResolveTimeService();
            
            if (timeService != null)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[TimeOfDayVisualApplier] Delayed retry SUCCEEDED: {(timeService as MonoBehaviour)?.name}");
                }
                
                Subscribe();
                
                if (IsActiveWriter())
                {
                    ApplyVisuals(timeService.NormalizedTime);
                }
            }
            else
            {
                Debug.LogError("[TimeOfDayVisualApplier] Delayed retry FAILED - TimeOfDayManager not found! " +
                              "Ensure TimeOfDayManager exists in GameServicesContainer and is marked DontDestroyOnLoad.");
            }
        }
    }
    
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        Unsubscribe();
        ReleaseVolumeProfileHandle();
        
        // Release active writer status
        if (activeWriter == this)
        {
            activeWriter = null;
            multipleAppliersWarningLogged = false;
        }
    }
    
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        TimeOfDayManager.OnInstanceAvailable -= OnTimeManagerAvailable;
        Unsubscribe();
        
        // Unregister if we're the active instance
        if (activeInstance == this)
        {
            activeInstance = null;
        }
        
        // Release active writer status
        if (activeWriter == this)
        {
            activeWriter = null;
            multipleAppliersWarningLogged = false;
        }

        ReleaseVolumeProfileHandle();
        
        // Safety: If we were indoors and getting destroyed, ensure we restore outdoor state
        // This handles cases where scene unloads while indoors
        if (isIndoors && Application.isPlaying)
        {
            // Restore fog state to outdoor default
            RenderSettings.fog = cachedFogEnabled;
            
            // Restore outdoor sun if it was disabled
            RestoreOutdoorSunDeterministic();
        }
    }
    
    // ============================================
    // SINGLE WRITER ENFORCEMENT (A)
    // ============================================
    
    /// <summary>
    /// Try to become the active writer for lighting state.
    /// Only one applier should mutate RenderSettings/lights at runtime.
    /// </summary>
    private bool TryBecomeActiveWriter()
    {
        // Check if current active writer is still valid
        bool currentWriterValid = activeWriter != null && activeWriter.enabled && activeWriter.gameObject.activeInHierarchy;
        
        if (!currentWriterValid)
        {
            // No valid active writer - claim it
            activeWriter = this;
            activeInstance = this; // Also set as active instance for IsAnyIndoors checks
            multipleAppliersWarningLogged = false;
            
            if (debugLighting)
            {
                Debug.Log($"[TimeOfDayVisualApplier] {gameObject.name} (scene: {gameObject.scene.name}) is now the ACTIVE WRITER for lighting.");
            }
            return true;
        }
        
        if (activeWriter == this)
        {
            return true; // We're already the active writer
        }
        
        // Another applier is active - log warning once
        if (!multipleAppliersWarningLogged)
        {
            Debug.LogWarning($"[TimeOfDayVisualApplier] Multiple appliers detected! " +
                           $"Active: {activeWriter.gameObject.name} (scene: {activeWriter.gameObject.scene.name}), " +
                           $"Ignored: {gameObject.name} (scene: {gameObject.scene.name}). " +
                           $"Only the active writer will mutate lighting state.");
            multipleAppliersWarningLogged = true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check if this applier is allowed to write lighting state.
    /// </summary>
    private bool IsActiveWriter()
    {
        if (!Application.isPlaying) return true; // Edit mode: always allow
        return activeWriter == this;
    }
    
    /// <summary>
    /// Called when a new scene loads. Refreshes time service connection.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-resolve time service in case it was lost or recreated
        RefreshTimeService();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayVisualApplier] Scene loaded: {scene.name}, timeService valid: {timeService != null}");
        }
    }
    
    /// <summary>
    /// Resolve the time service. ALWAYS prefers singleton for cross-scene reliability.
    /// Only uses serialized override if it's valid AND in the same scene.
    /// </summary>
    private void ResolveTimeService()
    {
        // ALWAYS try singleton first - it's the most reliable for cross-scene scenarios
        timeService = TimeOfDayManager.Instance;
        
        if (timeService != null)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[TimeOfDayVisualApplier] Using TimeOfDayManager singleton: {(timeService as MonoBehaviour)?.name}");
            }
            return;
        }
        
        // Fallback: Check if serialized override is valid (same-scene reference only)
        bool hasValidOverride = timeSourceOverride != null && 
                                (!(timeSourceOverride is MonoBehaviour) || (MonoBehaviour)timeSourceOverride != null);
        
        if (hasValidOverride && timeSourceOverride is ITimeOfDayService service)
        {
            timeService = service;
            if (enableDebugLogs)
            {
                Debug.Log($"[TimeOfDayVisualApplier] Using serialized timeSourceOverride: {timeSourceOverride.name}");
            }
            return;
        }
        
        if (enableDebugLogs)
        {
            Debug.LogWarning("[TimeOfDayVisualApplier] No TimeOfDayManager instance found! Time-based visuals will not work. " +
                            "Ensure TimeOfDayManager exists in GameServicesContainer.");
        }
    }
    
    /// <summary>
    /// Refresh the time service connection. Call if time service was lost.
    /// </summary>
    public void RefreshTimeService()
    {
        // Check if current service is still valid
        bool needsRefresh = timeService == null;
        
        // Also check for Unity "zombie" objects
        if (!needsRefresh && timeService is MonoBehaviour mb)
        {
            needsRefresh = mb == null; // Unity's == will return true if destroyed
        }
        
        if (needsRefresh)
        {
            Unsubscribe();
            
            // Use the robust resolution method
            ResolveTimeService();
            
            if (timeService != null)
            {
                Subscribe();
                ApplyVisuals(timeService.NormalizedTime);
                
                if (enableDebugLogs)
                {
                    Debug.Log($"[TimeOfDayVisualApplier] Time service refreshed, t={timeService.NormalizedTime:F3}");
                }
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning("[TimeOfDayVisualApplier] Failed to refresh time service - none found!");
            }
        }
    }
    
    // ============================================
    // SUBSCRIPTION
    // ============================================
    
    private void Subscribe()
    {
        if (isSubscribed || timeService == null) return;
        
        timeService.OnTimeNormalizedChanged += ApplyVisuals;
        isSubscribed = true;
    }
    
    private void Unsubscribe()
    {
        if (!isSubscribed || timeService == null) return;
        
        timeService.OnTimeNormalizedChanged -= ApplyVisuals;
        isSubscribed = false;
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Set indoor/outdoor state (idempotent).
    /// </summary>
    public void SetIndoors(bool indoors)
    {
        if (isIndoors == indoors) return;
        
        bool wasIndoors = isIndoors;
        isIndoors = indoors;
        stateChanged = true;
        
        if (debugLighting)
        {
            Debug.Log($"[TimeOfDayVisualApplier] SetIndoors({indoors}) - was: {wasIndoors}");
        }
        
        // If transitioning TO outdoors, reset indoor rotation cache
        // (so we can re-cache fresh rotation on next indoor entry)
        if (!indoors && wasIndoors)
        {
            hasIndoorRotationCached = false;
            cachedIndoorLightInstanceID = -1;
            
            if (debugLighting)
            {
                Debug.Log("[TimeOfDayVisualApplier] Exiting indoor mode - reset indoor rotation cache");
            }
        }
        
        // Immediately apply visuals
        if (timeService != null)
        {
            ApplyVisuals(timeService.NormalizedTime);
        }
    }
    
    /// <summary>
    /// Configure this applier to use an IndoorDirectionalLight.
    /// Called by SceneLoader after scene transitions to indoor scenes.
    /// </summary>
    public void ConfigureIndoorLight(IndoorDirectionalLight indoorLight)
    {
        if (indoorLight == null)
        {
            Debug.LogError("[TimeOfDayVisualApplier] ConfigureIndoorLight called with null light!");
            return;
        }
        
        // Set the indoor directional light reference
        indoorDirectionalLight = indoorLight.Light;

        // Always adopt this room's profile. A sticky first-room profile would keep
        // player_room lighting/fog rules after entering a later indoor scene.
        if (indoorLight.Profile != null)
            indoorProfile = indoorLight.Profile;

        hasIndoorRotationCached = false;
        cachedIndoorLightInstanceID = -1;
        stateChanged = true;
        
        // Pre-cache the scene-authored rotation from the IndoorDirectionalLight component
        // This is the authoritative source since it caches in Awake before anything else
        if (indoorLight.IsDataCached && indoorLight.Profile != null && indoorLight.Profile.UseSceneAuthoredRotation)
        {
            cachedIndoorSceneAuthoredRotation = indoorLight.SceneAuthoredRotation;
            cachedIndoorLightInstanceID = indoorLight.Light.GetInstanceID();
            hasIndoorRotationCached = true;
            
            if (debugLighting)
            {
                Debug.Log($"[TimeOfDayVisualApplier] ConfigureIndoorLight: Using pre-cached rotation from IndoorDirectionalLight:" +
                         $"\n  Light: {indoorLight.Light.name}" +
                         $"\n  Cached rotation: {cachedIndoorSceneAuthoredRotation.eulerAngles}");
            }
        }
        
        if (debugLighting)
        {
            Debug.Log($"[TimeOfDayVisualApplier] Configured indoor light: {indoorLight.Light.name}");
        }

        if (isIndoors && timeService != null)
            ApplyVisuals(timeService.NormalizedTime);
    }
    
    /// <summary>
    /// Configure this applier to use an outdoor sun (directional light).
    /// Called by SceneLoader after scene transitions to outdoor scenes.
    /// </summary>
    public void ConfigureOutdoorSun(Light outdoorSun)
    {
        if (outdoorSun == null)
        {
            Debug.LogError("[TimeOfDayVisualApplier] ConfigureOutdoorSun called with null light!");
            return;
        }
        
        // Set the outdoor directional light reference
        directionalLight = outdoorSun;
        
        // Ensure it's enabled
        directionalLight.enabled = true;
        
        // Set as RenderSettings.sun
        RenderSettings.sun = directionalLight;
        
        // Clear any cached outdoor sun state (we have a fresh reference now)
        hasOutdoorSunCached = false;
        cachedOutdoorSun = null;
        
        // Reset indoor rotation cache since we're configuring for outdoor
        hasIndoorRotationCached = false;
        cachedIndoorLightInstanceID = -1;
        
        if (debugLighting)
        {
            Debug.Log($"[TimeOfDayVisualApplier] Configured outdoor sun: {outdoorSun.name}, RenderSettings.sun set");
        }
    }

    /// <summary>
    /// Applies a chunk-local fog overlay without creating another time-of-day
    /// config or lighting writer. Overlapping region volumes resolve by their
    /// explicit priority, then instance id for deterministic ties.
    /// </summary>
    public void SetRegionFogOverlay(Component owner, float fogDensityMultiplier, Color fogTint, int priority)
    {
        if (owner == null)
            return;

        regionFogOverlays[owner.GetInstanceID()] = new RegionFogOverlay(fogDensityMultiplier, fogTint, priority);
        stateChanged = true;
        if (!isIndoors && timeService != null)
            ApplyVisuals(timeService.NormalizedTime);
    }

    /// <summary>Removes one region overlay. Indoor presentation always remains authoritative.</summary>
    public void ClearRegionFogOverlay(Component owner)
    {
        if (owner == null || !regionFogOverlays.Remove(owner.GetInstanceID()))
            return;

        stateChanged = true;
        if (!isIndoors && timeService != null)
            ApplyVisuals(timeService.NormalizedTime);
    }
    
    // ============================================
    // VISUAL APPLICATION
    // ============================================
    
    private void ApplyVisuals(float normalizedTime)
    {
        if (config == null) return;
        
        // ============================================
        // SINGLE WRITER ENFORCEMENT (A)
        // ============================================
        if (!IsActiveWriter())
        {
            return; // Another applier is the active writer - don't mutate state
        }
        
        // Skip if time hasn't changed significantly (unless indoor state changed)
        if (Mathf.Approximately(normalizedTime, lastAppliedTime) && !stateChanged)
        {
            return;
        }
        
        lastAppliedTime = normalizedTime;
        stateChanged = false;
        
        if (isIndoors)
        {
            ApplyIndoorVisuals(normalizedTime);
        }
        else
        {
            ApplyOutdoorVisuals(normalizedTime);
        }
    }
    
    private void ApplyOutdoorVisuals(float normalizedTime)
    {
        // ============================================
        // OUTDOOR SUN RESTORATION (C)
        // ============================================
        // Restore outdoor sun deterministically (don't rely on RenderSettings.sun for lookup)
        RestoreOutdoorSunDeterministic();
        
        // Try to find directional light if missing (handles scene transitions)
        if (directionalLight == null && applyDirectional && !requireExplicitDirectionalLight)
        {
            TryFindDirectionalLight();
        }
        
        // Restore cached values if we were indoors
        if (applyFog)
        {
            RenderSettings.fog = cachedFogEnabled;
            RenderSettings.fogMode = fogMode;
            if (fogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = linearFogStart;
                RenderSettings.fogEndDistance = linearFogEnd;
            }
            
            RegionFogOverlay overlay = ResolveRegionFogOverlay();
            RenderSettings.fogDensity = config.EvaluateFogDensity(normalizedTime) * overlay.DensityMultiplier;
            RenderSettings.fogColor = Color.Lerp(config.EvaluateFogColor(normalizedTime), overlay.Tint, overlay.Tint.a);
        }
        
        if (applyAmbient)
        {
            float intensity = config.EvaluateAmbientIntensity(normalizedTime);
            RenderSettings.ambientIntensity = intensity;
            
            Color ambientColor = config.EvaluateAmbientColor(normalizedTime);
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    RenderSettings.ambientLight = ambientColor;
                    break;
                case AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor = ambientColor;
                    RenderSettings.ambientEquatorColor = ambientColor * 0.8f;
                    RenderSettings.ambientGroundColor = ambientColor * 0.5f;
                    break;
                case AmbientMode.Skybox:
                    RenderSettings.ambientLight = ambientColor;
                    break;
            }
        }
        
        if (applyDirectional && directionalLight != null)
        {
            // Ensure outdoor sun is enabled and set as RenderSettings.sun
            directionalLight.enabled = true;
            RenderSettings.sun = directionalLight;
            
            float intensity = config.EvaluateLightIntensity(normalizedTime) * intensityMultiplier;
            directionalLight.intensity = Mathf.Max(intensity, minIntensity);
            directionalLight.color = config.EvaluateLightColor(normalizedTime);
            directionalLight.shadowStrength = config.EvaluateShadowStrength(normalizedTime);
            
            float sunAngle = config.EvaluateSunAngle(normalizedTime);
            directionalLight.transform.rotation = Quaternion.Euler(sunAngle, baseYawAngle, baseRollAngle);
            
            // Disable indoor directional if it's a separate light
            if (indoorDirectionalLight != null && indoorDirectionalLight != directionalLight)
            {
                indoorDirectionalLight.enabled = false;
            }
        }
        
        ApplyOutdoorVolume(normalizedTime);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayVisualApplier] Applied outdoor visuals at t={normalizedTime:F3}");
        }
    }

    private RegionFogOverlay ResolveRegionFogOverlay()
    {
        RegionFogOverlay resolved = new RegionFogOverlay(1f, new Color(1f, 1f, 1f, 0f), int.MinValue);
        int resolvedOwnerId = int.MaxValue;
        foreach (KeyValuePair<int, RegionFogOverlay> pair in regionFogOverlays)
        {
            RegionFogOverlay candidate = pair.Value;
            if (candidate.Priority > resolved.Priority ||
                (candidate.Priority == resolved.Priority && pair.Key < resolvedOwnerId))
            {
                resolved = candidate;
                resolvedOwnerId = pair.Key;
            }
        }

        return resolved;
    }
    
    // ============================================
    // OUTDOOR SUN RESTORATION (C) - Deterministic
    // ============================================
    
    /// <summary>
    /// Restore outdoor sun deterministically after exiting indoor mode.
    /// Does NOT rely on RenderSettings.sun for lookup.
    /// </summary>
    private void RestoreOutdoorSunDeterministic()
    {
        if (!hasOutdoorSunCached || cachedOutdoorSun == null)
        {
            return; // Nothing to restore
        }
        
        Light sunBefore = RenderSettings.sun;
        
        // Re-enable the cached outdoor sun
        cachedOutdoorSun.enabled = cachedOutdoorSunEnabled;
        
        // Set it as the active sun in RenderSettings
        RenderSettings.sun = cachedOutdoorSun;
        
        // Also restore it to our directionalLight reference for time-of-day driving
        directionalLight = cachedOutdoorSun;
        
        // Disable indoor directional if it's a separate light
        if (indoorDirectionalLight != null && indoorDirectionalLight != cachedOutdoorSun)
        {
            indoorDirectionalLight.enabled = false;
        }
        
        if (debugLighting)
        {
            Debug.Log($"[TimeOfDayVisualApplier] RESTORE OUTDOOR SUN:" +
                     $"\n  RenderSettings.sun before: {(sunBefore != null ? sunBefore.name : "null")}" +
                     $"\n  RenderSettings.sun after: {cachedOutdoorSun.name}" +
                     $"\n  directionalLight set to: {cachedOutdoorSun.name}");
        }
        
        // Clear cache
        cachedOutdoorSun = null;
        hasOutdoorSunCached = false;
    }
    
    /// <summary>
    /// Cache outdoor sun state when entering indoor mode.
    /// </summary>
    private void CacheOutdoorSunState()
    {
        if (hasOutdoorSunCached) return; // Already cached
        
        // Use our explicit directionalLight reference (the outdoor sun)
        if (directionalLight != null && directionalLight != indoorDirectionalLight)
        {
            cachedOutdoorSun = directionalLight;
            cachedOutdoorSunEnabled = directionalLight.enabled;
            cachedOutdoorSunRotation = directionalLight.transform.rotation;
            hasOutdoorSunCached = true;
            
            if (debugLighting)
            {
                Debug.Log($"[TimeOfDayVisualApplier] CACHED OUTDOOR SUN:" +
                         $"\n  Light: {cachedOutdoorSun.name} (ID: {cachedOutdoorSun.GetInstanceID()})" +
                         $"\n  Enabled: {cachedOutdoorSunEnabled}" +
                         $"\n  Rotation: {cachedOutdoorSunRotation.eulerAngles}");
            }
        }
    }
    
    // ============================================
    // INDOOR VISUALS APPLICATION
    // ============================================
    
    private void ApplyIndoorVisuals(float normalizedTime)
    {
        // ============================================
        // EDIT MODE SAFETY (D)
        // ============================================
        // Don't cache/restore in edit mode to avoid polluting scene
        bool isRuntime = Application.isPlaying;
        
        // Disable fog if profile says so
        if (applyFog)
        {
            if (indoorProfile != null && indoorProfile.DisableFogIndoors)
            {
                RenderSettings.fog = false;
            }
            else
            {
                RenderSettings.fog = cachedFogEnabled;
            }
        }
        
        // Apply indoor ambient if profile provided
        // Uses time-based evaluation if enabled in profile
        if (applyAmbient && indoorProfile != null)
        {
            RenderSettings.ambientIntensity = indoorProfile.EvaluateIndoorAmbientIntensity(normalizedTime);
            
            Color ambientColor = indoorProfile.EvaluateIndoorAmbientColor(normalizedTime);
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    RenderSettings.ambientLight = ambientColor;
                    break;
                case AmbientMode.Trilight:
                    RenderSettings.ambientSkyColor = ambientColor;
                    RenderSettings.ambientEquatorColor = ambientColor * 0.8f;
                    RenderSettings.ambientGroundColor = ambientColor * 0.5f;
                    break;
                case AmbientMode.Skybox:
                    RenderSettings.ambientLight = ambientColor;
                    break;
            }
        }
        
        // ============================================
        // INDOOR DIRECTIONAL LIGHTING (B) + (C)
        // ============================================
        if (applyDirectional && indoorProfile != null)
        {
            // Validate indoor directional light reference
            if (indoorDirectionalLight == null)
            {
                Debug.LogError($"[TimeOfDayVisualApplier] {gameObject.name}: indoorDirectionalLight is null! " +
                              "For indoor scenes, you MUST explicitly assign the indoor directional light. " +
                              "Skipping directional light changes in indoor mode.");
                return;
            }
            
            // ============================================
            // CACHE OUTDOOR SUN (C) - Before disabling it
            // ============================================
            if (isRuntime && indoorProfile.DisableOutdoorSunIndoors)
            {
                CacheOutdoorSunState();
                
                // Disable outdoor sun
                if (directionalLight != null && directionalLight != indoorDirectionalLight)
                {
                    directionalLight.enabled = false;
                }
            }
            
            // ============================================
            // CACHE SCENE-AUTHORED ROTATION (B) - Use IndoorDirectionalLight component if available
            // ============================================
            if (isRuntime && indoorProfile.UseSceneAuthoredRotation && !hasIndoorRotationCached)
            {
                // PRIORITY: Use IndoorDirectionalLight component's pre-cached rotation (most reliable)
                IndoorDirectionalLight indoorLightComponent = indoorDirectionalLight.GetComponent<IndoorDirectionalLight>();
                
                if (indoorLightComponent != null && indoorLightComponent.IsDataCached)
                {
                    // Use the rotation cached by IndoorDirectionalLight in its Awake (before anything else)
                    cachedIndoorSceneAuthoredRotation = indoorLightComponent.SceneAuthoredRotation;
                    cachedIndoorLightInstanceID = indoorDirectionalLight.GetInstanceID();
                    hasIndoorRotationCached = true;
                    
                    if (debugLighting)
                    {
                        Debug.Log($"[TimeOfDayVisualApplier] USING IndoorDirectionalLight COMPONENT'S CACHED ROTATION:" +
                                 $"\n  Light: {indoorDirectionalLight.name} (ID: {cachedIndoorLightInstanceID})" +
                                 $"\n  Scene-authored rotation: {cachedIndoorSceneAuthoredRotation.eulerAngles}");
                    }
                }
                else
                {
                    // Fallback: cache current rotation (may already be modified - less reliable)
                    cachedIndoorSceneAuthoredRotation = indoorDirectionalLight.transform.rotation;
                    cachedIndoorLightInstanceID = indoorDirectionalLight.GetInstanceID();
                    hasIndoorRotationCached = true;
                    
                    if (debugLighting)
                    {
                        Debug.LogWarning($"[TimeOfDayVisualApplier] FALLBACK: Caching current rotation (may not be scene-authored):" +
                                        $"\n  Light: {indoorDirectionalLight.name} (ID: {cachedIndoorLightInstanceID})" +
                                        $"\n  Current rotation: {cachedIndoorSceneAuthoredRotation.eulerAngles}" +
                                        $"\n  TIP: Add IndoorDirectionalLight component for reliable scene-authored rotation.");
                    }
                }
            }
            
            // ============================================
            // APPLY INDOOR DIRECTIONAL SETTINGS
            // Uses time-based evaluation if enabled in profile
            // ============================================
            indoorDirectionalLight.enabled = true;
            indoorDirectionalLight.intensity = indoorProfile.EvaluateIndoorDirectionalIntensity(normalizedTime);
            indoorDirectionalLight.color = indoorProfile.EvaluateIndoorDirectionalColor(normalizedTime);
            indoorDirectionalLight.shadowStrength = indoorProfile.IndoorShadowStrength;
            
            // Apply rotation (ENFORCE every frame to prevent other scripts from overwriting)
            if (indoorProfile.UseSceneAuthoredRotation)
            {
                if (hasIndoorRotationCached)
                {
                    // Verify we're applying to the same light we cached from
                    if (indoorDirectionalLight.GetInstanceID() != cachedIndoorLightInstanceID)
                    {
                        Debug.LogWarning($"[TimeOfDayVisualApplier] Indoor light instance changed! " +
                                        $"Cached from ID {cachedIndoorLightInstanceID}, now applying to {indoorDirectionalLight.GetInstanceID()}. " +
                                        "Re-caching rotation.");
                        cachedIndoorSceneAuthoredRotation = indoorDirectionalLight.transform.rotation;
                        cachedIndoorLightInstanceID = indoorDirectionalLight.GetInstanceID();
                    }
                    
                    // ENFORCE the cached rotation (idempotent - prevents drift from other scripts)
                    indoorDirectionalLight.transform.rotation = cachedIndoorSceneAuthoredRotation;
                }
                else if (!isRuntime)
                {
                    // Edit mode: don't modify rotation
                }
                else
                {
                    // Runtime but failed to cache - this shouldn't happen, log error
                    Debug.LogError($"[TimeOfDayVisualApplier] UseSceneAuthoredRotation is true but rotation was not cached!");
                }
            }
            else
            {
                // Use profile-defined rotation
                indoorDirectionalLight.transform.rotation = Quaternion.Euler(indoorProfile.IndoorDirectionalRotation);
            }
            
            // Set as the active sun for RenderSettings
            RenderSettings.sun = indoorDirectionalLight;
            
            if (debugLighting)
            {
                string rotationInfo = indoorProfile.UseSceneAuthoredRotation 
                    ? $"scene-authored/cached ({cachedIndoorSceneAuthoredRotation.eulerAngles})" 
                    : $"profile ({indoorProfile.IndoorDirectionalRotation})";
                Debug.Log($"[TimeOfDayVisualApplier] APPLIED INDOOR DIRECTIONAL:" +
                         $"\n  Light: {indoorDirectionalLight.name} (ID: {indoorDirectionalLight.GetInstanceID()})" +
                         $"\n  Rotation mode: {rotationInfo}" +
                         $"\n  Actual rotation: {indoorDirectionalLight.transform.rotation.eulerAngles}" +
                         $"\n  RenderSettings.sun: {(RenderSettings.sun != null ? RenderSettings.sun.name : "null")}");
            }
        }
        
        ApplyIndoorVolume();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TimeOfDayVisualApplier] Applied indoor visuals");
        }
    }
    
    private void ApplyOutdoorVolume(float normalizedTime)
    {
        if (!TryRetainVolumeProfileHandle())
            return;

        VolumeProfile source;
        if (blendDayNightVolumes && config.DayVolumeProfile != null && config.NightVolumeProfile != null)
        {
            float nightFactor = GetNightFactor(normalizedTime);
            source = nightFactor > 0.5f ? config.NightVolumeProfile : config.DayVolumeProfile;
            volumeProfileHandle.AcquireWorkingProfile(source);
            return;
        }

        VolumeProfile profile = volumeProfileHandle.AcquireWorkingProfile(globalVolume.sharedProfile);
        if (profile == null || !profile.TryGet(out ColorAdjustments colorAdj))
            return;

        colorAdj.colorFilter.Override(config.EvaluateColorFilter(normalizedTime));
        colorAdj.postExposure.Override(config.EvaluateExposure(normalizedTime));
    }

    private void ApplyIndoorVolume()
    {
        if (indoorProfile == null || !indoorProfile.UseIndoorURPVolume || indoorProfile.IndoorVolumeProfile == null)
            return;
        if (!TryRetainVolumeProfileHandle())
            return;

        volumeProfileHandle.AcquireWorkingProfile(indoorProfile.IndoorVolumeProfile);
        volumeProfileHandle.SetWeight(indoorProfile.IndoorVolumeWeight);
    }

    private bool TryRetainVolumeProfileHandle()
    {
        if (!Application.isPlaying || globalVolume == null)
            return false;

        if (volumeProfileHandle == null)
            volumeProfileHandle = RuntimeVolumeProfileHandle.Retain(globalVolume);

        return volumeProfileHandle != null;
    }

    private void ReleaseVolumeProfileHandle()
    {
        if (volumeProfileHandle == null)
            return;

        volumeProfileHandle.Release();
        volumeProfileHandle = null;
    }

    private float GetNightFactor(float normalizedTime)
    {
        if (config == null) return 0f;
        
        float lightIntensity = config.EvaluateLightIntensity(normalizedTime);
        float nightFactor = 1f - Mathf.InverseLerp(0.1f, 0.8f, lightIntensity);
        return Mathf.Clamp01(nightFactor);
    }
    
    // ============================================
    // CACHING
    // ============================================
    
    private void CacheOutdoorValues()
    {
        cachedFogEnabled = RenderSettings.fog;
        cachedFogDensity = RenderSettings.fogDensity;
        cachedFogColor = RenderSettings.fogColor;
        cachedAmbientLight = RenderSettings.ambientLight;
        cachedAmbientIntensity = RenderSettings.ambientIntensity;
        
        if (directionalLight != null)
        {
            cachedDirectionalColor = directionalLight.color;
            cachedDirectionalIntensity = directionalLight.intensity;
        }
        
    }
    
    /// <summary>
    /// Try to find a directional light in the scene for outdoor mode.
    /// Does NOT use RenderSettings.sun (which may point to indoor light).
    /// Uses deterministic strategy: prefer cached outdoor sun, then search.
    /// </summary>
    private void TryFindDirectionalLight()
    {
        // ============================================
        // DETERMINISTIC FIND STRATEGY (C)
        // ============================================
        
        // Strategy 1: Use cached outdoor sun if available
        if (hasOutdoorSunCached && cachedOutdoorSun != null)
        {
            directionalLight = cachedOutdoorSun;
            if (debugLighting)
            {
                Debug.Log($"[TimeOfDayVisualApplier] Using cached outdoor sun: {directionalLight.name}");
            }
            return;
        }
        
        // Strategy 2: If requireExplicitDirectionalLight is true, don't auto-find
        if (requireExplicitDirectionalLight)
        {
            Debug.LogError($"[TimeOfDayVisualApplier] {gameObject.name}: directionalLight is null and requireExplicitDirectionalLight is true. " +
                          "Cannot auto-find. Assign the outdoor sun explicitly.");
            return;
        }
        
        // Strategy 3: Search for directional lights in scene (exclude indoor light)
        Light[] lights = FindObjectsOfType<Light>();
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional && light != indoorDirectionalLight)
            {
                directionalLight = light;
                RenderSettings.sun = light; // Also set as sun
                
                if (debugLighting)
                {
                    Debug.Log($"[TimeOfDayVisualApplier] Found outdoor directional light via search: {directionalLight.name}");
                }
                return;
            }
        }
        
        Debug.LogWarning($"[TimeOfDayVisualApplier] {gameObject.name}: Could not find outdoor directional light in scene!");
    }
    
    // ============================================
    // EDITOR SUPPORT
    // ============================================
    
#if UNITY_EDITOR
    private void Update()
    {
        // In edit mode, continuously update
        if (!Application.isPlaying && timeService != null)
        {
            ApplyVisuals(timeService.NormalizedTime);
        }
    }
    
    private void OnValidate()
    {
        intensityMultiplier = Mathf.Max(0f, intensityMultiplier);
        minIntensity = Mathf.Max(0f, minIntensity);
        linearFogStart = Mathf.Max(0f, linearFogStart);
        linearFogEnd = Mathf.Max(linearFogStart + 1f, linearFogEnd);
        
        // Apply immediately in editor
        if (timeService != null)
        {
            ApplyVisuals(timeService.NormalizedTime);
        }
    }
#endif
}
