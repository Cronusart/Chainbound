using System.Collections.Generic;
using UnityEngine;
using System.Linq;

/// <summary>
/// Controls night accent lighting for windows, street lamps, and other emissive elements.
/// Uses EmissiveGroup components and optional point light pooling for performance.
/// 
/// Setup:
/// 1. Attach to a manager object in your scene
/// 2. Create EmissiveGroup components on window/lamp parent objects
/// 3. Assign groups manually or use auto-find
/// 4. Configure NightAccentConfigSO for appearance settings
/// </summary>
public class NightAccentLightingDriver : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TimeOfDayManager reference. Uses singleton if null.")]
    [SerializeField] private TimeOfDayManager timeManager;
    
    [Tooltip("Night accent configuration")]
    [SerializeField] private NightAccentConfigSO config;
    
    [Header("Emissive Groups")]
    [Tooltip("Emissive groups to control. Leave empty to auto-find in scene.")]
    [SerializeField] private List<EmissiveGroup> emissiveGroups = new List<EmissiveGroup>();
    
    [Tooltip("Auto-find EmissiveGroup components in scene on Start")]
    [SerializeField] private bool autoFindGroups = true;
    
    [Header("Player Reference (for Distance Culling)")]
    [Tooltip("Player transform for distance-based point light culling")]
    [SerializeField] private Transform playerTransform;
    
    [Tooltip("Auto-find player by tag")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("Performance")]
    [Tooltip("Update rate for point light culling (Hz)")]
    [Range(1f, 10f)]
    [SerializeField] private float cullUpdateRateHz = 2f;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool showDebugUI = false;
    
    // State
    private bool isNightActive = false;
    private float currentNormalizedTime = 0f;
    private float lastCullUpdateTime = 0f;
    private int activePointLightCount = 0;
    
    // Per-group fade state
    private Dictionary<EmissiveGroup, float> groupFadeProgress = new Dictionary<EmissiveGroup, float>();
    
    // Cached
    private bool isSubscribed = false;
    private List<EmissiveGroup> sortedGroups = new List<EmissiveGroup>();
    
    private void OnEnable()
    {
        // Auto-find time manager
        if (timeManager == null)
        {
            timeManager = TimeOfDayManager.Instance;
        }
        
        // Auto-find player
        if (playerTransform == null && !string.IsNullOrEmpty(playerTag))
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        Subscribe();
    }
    
    private void Start()
    {
        // Auto-find emissive groups
        if (autoFindGroups && (emissiveGroups == null || emissiveGroups.Count == 0))
        {
            FindEmissiveGroups();
        }
        
        // Remove null entries
        emissiveGroups.RemoveAll(g => g == null);
        
        // Initialize sorted list and fade dictionary
        sortedGroups = new List<EmissiveGroup>(emissiveGroups);
        groupFadeProgress.Clear();
        foreach (var group in emissiveGroups)
        {
            if (group != null)
            {
                groupFadeProgress[group] = 0f;
            }
        }
        
        // Apply initial state
        if (timeManager != null)
        {
            currentNormalizedTime = timeManager.NormalizedTime;
            UpdateNightState(currentNormalizedTime);
            ApplyEmissionPerGroup();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NightAccentLightingDriver] Initialized with {emissiveGroups.Count} groups");
            if (config != null)
            {
                Debug.Log($"[NightAccentLightingDriver] Config has {config.GroupCount} accent group definitions");
            }
        }
    }
    
    private void OnDisable()
    {
        Unsubscribe();
        
        // Turn off all emissions
        foreach (var group in emissiveGroups)
        {
            if (group != null)
            {
                group.TurnOff();
            }
        }
    }
    
    private void Subscribe()
    {
        if (isSubscribed || timeManager == null) return;
        
        timeManager.OnTimeNormalizedChanged += OnTimeChanged;
        timeManager.OnDayNightChanged += OnDayNightChanged;
        isSubscribed = true;
    }
    
    private void Unsubscribe()
    {
        if (!isSubscribed || timeManager == null) return;
        
        timeManager.OnTimeNormalizedChanged -= OnTimeChanged;
        timeManager.OnDayNightChanged -= OnDayNightChanged;
        isSubscribed = false;
    }
    
    private void FindEmissiveGroups()
    {
        emissiveGroups.Clear();
        emissiveGroups.AddRange(FindObjectsOfType<EmissiveGroup>());
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NightAccentLightingDriver] Auto-found {emissiveGroups.Count} emissive groups");
        }
    }
    
    private void OnTimeChanged(float normalizedTime)
    {
        currentNormalizedTime = normalizedTime;
        UpdateNightState(normalizedTime);
    }
    
    private void OnDayNightChanged(bool isNight)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[NightAccentLightingDriver] Day/night changed: isNight={isNight}");
        }
    }
    
    private void UpdateNightState(float normalizedTime)
    {
        if (config == null) return;
        
        bool shouldBeActive = config.ShouldBeActive(normalizedTime);
        
        if (shouldBeActive != isNightActive)
        {
            isNightActive = shouldBeActive;
            
            if (enableDebugLogs)
            {
                Debug.Log($"[NightAccentLightingDriver] Night active state: {isNightActive}");
            }
        }
    }
    
    private void Update()
    {
        if (config == null) return;
        
        // Update per-group fade and apply emission
        ApplyEmissionPerGroup();
        
        // Point light distance culling
        if (config.EnablePointLights && playerTransform != null)
        {
            float cullInterval = 1f / cullUpdateRateHz;
            if (Time.time - lastCullUpdateTime >= cullInterval)
            {
                lastCullUpdateTime = Time.time;
                UpdatePointLightCulling();
            }
        }
    }
    
    /// <summary>
    /// Apply emission to each group based on its specific accent group settings.
    /// </summary>
    private void ApplyEmissionPerGroup()
    {
        if (config == null) return;
        
        foreach (var group in emissiveGroups)
        {
            if (group == null) continue;
            
            // Get the accent group for this emissive group
            NightAccentGroup accentGroup = group.GetAccentGroup(config);
            
            // Calculate target fade for this specific group
            float targetFade = 0f;
            float fadeDuration = config.FadeDuration;
            
            if (accentGroup != null && accentGroup.Enabled)
            {
                // Check if this group should be active (handles custom timing)
                bool groupShouldBeActive = config.ShouldBeActive(currentNormalizedTime, accentGroup);
                targetFade = groupShouldBeActive ? 1f : 0f;
                fadeDuration = accentGroup.FadeDuration;
            }
            
            // Get current fade progress for this group
            if (!groupFadeProgress.TryGetValue(group, out float currentFade))
            {
                currentFade = 0f;
                groupFadeProgress[group] = currentFade;
            }
            
            // Smooth fade
            if (!Mathf.Approximately(currentFade, targetFade))
            {
                float fadeSpeed = 1f / Mathf.Max(0.01f, fadeDuration);
                currentFade = Mathf.MoveTowards(currentFade, targetFade, fadeSpeed * Time.deltaTime);
                groupFadeProgress[group] = currentFade;
            }
            
            // Apply emission using the group's settings
            group.SetEmissionFromGroup(currentFade, config);
        }
    }
    
    /// <summary>
    /// Legacy method - applies same emission to all groups.
    /// </summary>
    private void ApplyEmission(float intensity)
    {
        if (config == null) return;
        
        Color emissionColor = config.FinalEmissionColor;
        
        // Apply flicker if enabled
        if (config.EnableFlicker && intensity > 0f)
        {
            float flicker = config.GetFlickerMultiplier();
            intensity *= flicker;
        }
        
        foreach (var group in emissiveGroups)
        {
            if (group != null)
            {
                group.SetEmission(intensity, emissionColor);
            }
        }
    }
    
    private void UpdatePointLightCulling()
    {
        if (playerTransform == null || config == null) return;
        
        Vector3 playerPos = playerTransform.position;
        float cullDistSq = config.PointLightCullDistance * config.PointLightCullDistance;
        int maxLights = config.MaxActivePointLights;
        
        // Sort groups by distance to player
        sortedGroups.Clear();
        foreach (var group in emissiveGroups)
        {
            if (group != null && group.AttachedLight != null)
            {
                sortedGroups.Add(group);
            }
        }
        
        sortedGroups.Sort((a, b) => 
        {
            float distA = (a.Position - playerPos).sqrMagnitude;
            float distB = (b.Position - playerPos).sqrMagnitude;
            return distA.CompareTo(distB);
        });
        
        // Enable closest lights, disable others
        activePointLightCount = 0;
        for (int i = 0; i < sortedGroups.Count; i++)
        {
            var group = sortedGroups[i];
            var light = group.AttachedLight;
            if (light == null) continue;
            
            // Get the accent group for this emissive group
            NightAccentGroup accentGroup = group.GetAccentGroup(config);
            
            // Check if this group should be active (handles custom timing)
            bool groupShouldBeActive = accentGroup != null && accentGroup.Enabled && 
                                       config.ShouldBeActive(currentNormalizedTime, accentGroup);
            
            float distSq = (group.Position - playerPos).sqrMagnitude;
            bool shouldBeActive = groupShouldBeActive && 
                                  distSq <= cullDistSq && 
                                  activePointLightCount < maxLights;
            
            if (shouldBeActive)
            {
                // Get fade progress for this group
                float groupFade = groupFadeProgress.TryGetValue(group, out float fade) ? fade : 0f;
                
                if (!light.enabled)
                {
                    light.enabled = true;
                }
                
                // Update light properties from accent group settings
                if (accentGroup != null && accentGroup.EnablePointLights)
                {
                    light.range = accentGroup.PointLightRange;
                    light.intensity = accentGroup.PointLightIntensity * groupFade;
                    light.color = accentGroup.PointLightColor;
                }
                else
                {
                    // Fallback to config defaults
                    light.range = config.PointLightRange;
                    light.intensity = config.PointLightIntensity * groupFade;
                    light.color = config.PointLightColor;
                }
                
                activePointLightCount++;
            }
            else
            {
                if (light.enabled)
                {
                    light.enabled = false;
                }
            }
        }
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Register a new emissive group at runtime.
    /// </summary>
    public void RegisterGroup(EmissiveGroup group)
    {
        if (group == null || emissiveGroups.Contains(group)) return;
        
        emissiveGroups.Add(group);
        groupFadeProgress[group] = 0f;
        
        // Apply current state
        if (config != null)
        {
            group.SetEmissionFromGroup(0f, config);
        }
    }
    
    /// <summary>
    /// Unregister an emissive group.
    /// </summary>
    public void UnregisterGroup(EmissiveGroup group)
    {
        if (group == null) return;
        
        group.TurnOff();
        emissiveGroups.Remove(group);
        groupFadeProgress.Remove(group);
    }
    
    /// <summary>
    /// Force night accents on/off (for testing or cutscenes).
    /// </summary>
    public void ForceState(bool on, bool immediate = false)
    {
        isNightActive = on;
        float targetFade = on ? 1f : 0f;
        
        if (immediate)
        {
            // Set all groups to target immediately
            var keys = groupFadeProgress.Keys.ToList();
            foreach (var key in keys)
            {
                groupFadeProgress[key] = targetFade;
            }
            ApplyEmissionPerGroup();
        }
    }
    
    /// <summary>
    /// Set player transform for distance culling.
    /// </summary>
    public void SetPlayerTransform(Transform player)
    {
        playerTransform = player;
    }
    
    // ============================================
    // DEBUG UI
    // ============================================
    
    private void OnGUI()
    {
        if (!showDebugUI) return;
        
        GUILayout.BeginArea(new Rect(10, 220, 350, 200));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Night Accent Lighting");
        GUILayout.Label($"Time: {currentNormalizedTime:F3}");
        GUILayout.Label($"Night Active: {isNightActive}");
        GUILayout.Label($"Emissive Groups: {emissiveGroups.Count}");
        GUILayout.Label($"Accent Group Definitions: {config?.GroupCount ?? 0}");
        GUILayout.Label($"Point Lights Active: {activePointLightCount}/{config?.MaxActivePointLights ?? 0}");
        
        // Show active groups by type
        if (config != null)
        {
            var activeGroups = emissiveGroups
                .Where(g => g != null && groupFadeProgress.TryGetValue(g, out float f) && f > 0.01f)
                .GroupBy(g => g.AccentGroupType)
                .Select(g => $"{g.Key}: {g.Count()}");
            GUILayout.Label($"Active: {string.Join(", ", activeGroups)}");
        }
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (config == null) return;
        
        // Draw cull distance sphere around player
        if (playerTransform != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(playerTransform.position, config.PointLightCullDistance);
        }
    }
#endif
}

