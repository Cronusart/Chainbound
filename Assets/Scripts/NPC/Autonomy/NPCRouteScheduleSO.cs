using System;
using UnityEngine;

/// <summary>
/// ScriptableObject mapping time segments to route assets with multipliers for
/// wait times and micro-wander settings. Content authors can tune behavior
/// per time segment without duplicating route assets.
/// </summary>
[CreateAssetMenu(fileName = "RouteSchedule_", menuName = "Chainbound/NPC/Routes/Route Schedule")]
public class NPCRouteScheduleSO : ScriptableObject
{
    [Serializable]
    public class RouteConfig
    {
        [Header("Route")]
        [Tooltip("Route asset for this time segment")]
        public NPCRouteAsset routeAsset;
        
        [Header("Visual")]
        [Tooltip("Blink rate multiplier (0 = no blink, 1 = normal, 2 = double)")]
        [Range(0f, 2f)]
        public float blinkRateMultiplier = 1f;
        
        [Header("Wait Time Modifiers")]
        [Tooltip("Multiplier applied to route's wait time range")]
        [Range(0.1f, 3f)]
        public float waitTimeMultiplier = 1f;
        
        [Header("Micro-Wander Modifiers")]
        [Tooltip("Enable micro-wander for this time segment (overrides route setting if false)")]
        public bool microWanderEnabled = true;
        
        [Tooltip("Multiplier applied to micro-wander radius")]
        [Range(0.1f, 3f)]
        public float wanderRadiusMultiplier = 1f;
        
        [Tooltip("Multiplier applied to micro-wander interval range")]
        [Range(0.1f, 3f)]
        public float wanderIntervalMultiplier = 1f;
        
        [Tooltip("Multiplier applied to micro-wander dwell range")]
        [Range(0.1f, 3f)]
        public float wanderDwellMultiplier = 1f;
        
        // Legacy fields - kept for backward compatibility
        [HideInInspector]
        public float microWanderChance = 0.3f;
        [HideInInspector]
        public float microWanderRadius = 1.5f;
        [HideInInspector]
        public float microWanderCooldown = 10f;
    }
    
    [Header("Time-Gated Routes")]
    [Tooltip("Route configuration for Night segment")]
    [SerializeField] private RouteConfig nightRoute = new RouteConfig();
    
    [Tooltip("Route configuration for Dawn segment")]
    [SerializeField] private RouteConfig dawnRoute = new RouteConfig();
    
    [Tooltip("Route configuration for Day segment")]
    [SerializeField] private RouteConfig dayRoute = new RouteConfig();
    
    [Tooltip("Route configuration for Dusk segment")]
    [SerializeField] private RouteConfig duskRoute = new RouteConfig();
    
    /// <summary>
    /// Get route configuration for a time segment.
    /// </summary>
    public RouteConfig GetRouteForSegment(TimeSegment segment)
    {
        switch (segment)
        {
            case TimeSegment.Night:
                return nightRoute;
            case TimeSegment.Dawn:
                return dawnRoute;
            case TimeSegment.Day:
                return dayRoute;
            case TimeSegment.Dusk:
                return duskRoute;
            default:
                return dayRoute;
        }
    }
    
    /// <summary>
    /// Apply schedule multipliers to a wait time range.
    /// </summary>
    public Vector2 ApplyWaitTimeMultiplier(Vector2 baseRange, RouteConfig config)
    {
        if (config == null) return baseRange;
        return baseRange * config.waitTimeMultiplier;
    }
    
    /// <summary>
    /// Apply schedule multipliers to micro-wander settings.
    /// Returns null if micro-wander is disabled for this config.
    /// </summary>
    public MicroWanderSettings ApplyMicroWanderMultipliers(MicroWanderSettings baseSettings, RouteConfig config)
    {
        if (config == null || baseSettings == null)
            return baseSettings;
        
        // If schedule disables micro-wander, return disabled settings
        if (!config.microWanderEnabled)
        {
            return new MicroWanderSettings
            {
                enabled = false,
                radius = baseSettings.radius,
                intervalRange = baseSettings.intervalRange,
                dwellRange = baseSettings.dwellRange,
                maxStepsPerWait = baseSettings.maxStepsPerWait
            };
        }
        
        // Apply multipliers
        return new MicroWanderSettings
        {
            enabled = baseSettings.enabled,
            radius = baseSettings.radius * config.wanderRadiusMultiplier,
            intervalRange = baseSettings.intervalRange * config.wanderIntervalMultiplier,
            dwellRange = baseSettings.dwellRange * config.wanderDwellMultiplier,
            maxStepsPerWait = baseSettings.maxStepsPerWait
        };
    }
}
