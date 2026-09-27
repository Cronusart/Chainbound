using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ScriptableObject containing all time-of-day configuration.
/// Includes cycle timing, segment thresholds, and visual curves/gradients.
/// Curve X and segment thresholds are a 24h CST day fraction when the manager
/// uses SolanaCstRealTime (0 = midnight, 0.5 = noon).
/// </summary>
[CreateAssetMenu(fileName = "TimeOfDayConfig", menuName = "Chainbound/World/Time of Day/Config")]
public class TimeOfDayConfigSO : ScriptableObject
{
    [Header("Cycle Timing")]
    [Tooltip("LocalSimulated only. Ignored when TimeOfDayManager uses SolanaCstRealTime (that path is always a 24h CST day).")]
    [SerializeField] private float fullDaySeconds = 600f;
    
    [Tooltip("Time update broadcast rate in Hz. Higher = more responsive but more events.")]
    [Range(1f, 30f)]
    [SerializeField] private float updateRateHz = 4f;
    
    [Header("Segment Thresholds")]
    [Tooltip("Boundaries as fractions of a 24h CST day (0 = midnight, 0.5 = noon). Default 0.20/0.30/0.70/0.80 = 04:48 / 07:12 / 16:48 / 19:12.")]
    [SerializeField] private TimeSegmentThresholds segmentThresholds = TimeSegmentThresholds.Default;
    
    [Header("Directional Light")]
    [Tooltip("Light intensity over time (X = normalized time, Y = intensity)")]
    [SerializeField] private AnimationCurve lightIntensityCurve = CreateDefaultLightIntensityCurve();
    
    [Tooltip("Light color over time")]
    [SerializeField] private Gradient lightColorGradient = CreateDefaultLightColorGradient();
    
    [Tooltip("Shadow strength over time (X = normalized time, Y = strength 0-1)")]
    [SerializeField] private AnimationCurve shadowStrengthCurve = CreateDefaultShadowStrengthCurve();
    
    [Tooltip("Sun angle offset over time (affects directional light rotation)")]
    [SerializeField] private AnimationCurve sunAngleCurve = CreateDefaultSunAngleCurve();
    
    [Header("Ambient Light")]
    [Tooltip("Ambient light intensity multiplier over time")]
    [SerializeField] private AnimationCurve ambientIntensityCurve = CreateDefaultAmbientIntensityCurve();
    
    [Tooltip("Ambient light color over time")]
    [SerializeField] private Gradient ambientColorGradient = CreateDefaultAmbientColorGradient();
    
    [Header("Fog")]
    [Tooltip("Fog density over time")]
    [SerializeField] private AnimationCurve fogDensityCurve = CreateDefaultFogDensityCurve();
    
    [Tooltip("Fog color over time")]
    [SerializeField] private Gradient fogColorGradient = CreateDefaultFogColorGradient();
    
    [Header("Post Processing (Optional)")]
    [Tooltip("Exposure compensation over time (-2 to +2 typical)")]
    [SerializeField] private AnimationCurve exposureCurve = CreateDefaultExposureCurve();
    
    [Tooltip("Color filter/tint over time (multiplied with scene)")]
    [SerializeField] private Gradient colorFilterGradient = CreateDefaultColorFilterGradient();
    
    [Tooltip("Optional: Day volume profile for URP")]
    [SerializeField] private VolumeProfile dayVolumeProfile;
    
    [Tooltip("Optional: Night volume profile for URP (will blend between day/night)")]
    [SerializeField] private VolumeProfile nightVolumeProfile;
    
    [Header("Skybox (Optional)")]
    [Tooltip("Skybox tint color over time")]
    [SerializeField] private Gradient skyboxTintGradient = CreateDefaultSkyboxTintGradient();
    
    [Tooltip("Skybox exposure/intensity over time")]
    [SerializeField] private AnimationCurve skyboxExposureCurve = CreateDefaultSkyboxExposureCurve();
    
    // ============================================
    // PUBLIC ACCESSORS
    // ============================================
    
    public float FullDaySeconds => fullDaySeconds;
    public float UpdateRateHz => updateRateHz;
    public float UpdateInterval => 1f / updateRateHz;
    public TimeSegmentThresholds SegmentThresholds => segmentThresholds;
    
    // Directional Light
    public float EvaluateLightIntensity(float t) => lightIntensityCurve.Evaluate(t);
    public Color EvaluateLightColor(float t) => lightColorGradient.Evaluate(t);
    public float EvaluateShadowStrength(float t) => shadowStrengthCurve.Evaluate(t);
    public float EvaluateSunAngle(float t) => sunAngleCurve.Evaluate(t);
    
    // Ambient
    public float EvaluateAmbientIntensity(float t) => ambientIntensityCurve.Evaluate(t);
    public Color EvaluateAmbientColor(float t) => ambientColorGradient.Evaluate(t);
    
    // Fog
    public float EvaluateFogDensity(float t) => fogDensityCurve.Evaluate(t);
    public Color EvaluateFogColor(float t) => fogColorGradient.Evaluate(t);
    
    // Post Processing
    public float EvaluateExposure(float t) => exposureCurve.Evaluate(t);
    public Color EvaluateColorFilter(float t) => colorFilterGradient.Evaluate(t);
    public VolumeProfile DayVolumeProfile => dayVolumeProfile;
    public VolumeProfile NightVolumeProfile => nightVolumeProfile;
    
    // Skybox
    public Color EvaluateSkyboxTint(float t) => skyboxTintGradient.Evaluate(t);
    public float EvaluateSkyboxExposure(float t) => skyboxExposureCurve.Evaluate(t);
    
    /// <summary>
    /// Get segment for a given normalized time using configured thresholds.
    /// </summary>
    public TimeSegment GetSegment(float normalizedTime) => segmentThresholds.GetSegment(normalizedTime);

    /// <summary>Convert a 24h CST clock hour into a curve/segment sample (0–1).</summary>
    public static float HoursToNormalized(float hours) => TimeOfDayClock.HoursToNormalized(hours);
    
    /// <summary>
    /// Get a midpoint time value for a given segment (useful for holds).
    /// </summary>
    public float GetSegmentMidpoint(TimeSegment segment)
    {
        switch (segment)
        {
            case TimeSegment.Night:
                // Night spans [nightStart..1.0) and [0..dawnStart)
                // Return a value in the first half (late night)
                return 0.1f; // 10% through cycle (deep night)
            case TimeSegment.Dawn:
                return (segmentThresholds.dawnStart + segmentThresholds.dayStart) * 0.5f;
            case TimeSegment.Day:
                return (segmentThresholds.dayStart + segmentThresholds.duskStart) * 0.5f;
            case TimeSegment.Dusk:
                return (segmentThresholds.duskStart + segmentThresholds.nightStart) * 0.5f;
            default:
                return 0.5f;
        }
    }
    
    // ============================================
    // DEFAULT CURVE/GRADIENT CREATION
    // ============================================
    
    private static AnimationCurve CreateDefaultLightIntensityCurve()
    {
        var curve = new AnimationCurve();
        // Night (0.0 - 0.2): very low
        curve.AddKey(0f, 0.1f);
        // Dawn rise (0.2 - 0.3)
        curve.AddKey(0.2f, 0.2f);
        curve.AddKey(0.3f, 0.8f);
        // Day peak (0.5)
        curve.AddKey(0.5f, 1.0f);
        // Dusk fall (0.7 - 0.8)
        curve.AddKey(0.7f, 0.8f);
        curve.AddKey(0.8f, 0.2f);
        // Night again
        curve.AddKey(1f, 0.1f);
        return curve;
    }
    
    private static Gradient CreateDefaultLightColorGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f),      // Night: deep blue
                new GradientColorKey(new Color(1f, 0.6f, 0.4f), 0.25f),     // Sunrise: warm orange
                new GradientColorKey(new Color(1f, 0.98f, 0.9f), 0.5f),     // Noon: warm white
                new GradientColorKey(new Color(1f, 0.5f, 0.3f), 0.75f),     // Sunset: deep orange
                new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1f)       // Night: deep blue
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static AnimationCurve CreateDefaultShadowStrengthCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.3f);    // Night: soft shadows
        curve.AddKey(0.25f, 0.6f); // Dawn
        curve.AddKey(0.5f, 1f);    // Noon: full shadows
        curve.AddKey(0.75f, 0.6f); // Dusk
        curve.AddKey(1f, 0.3f);    // Night
        return curve;
    }
    
    private static AnimationCurve CreateDefaultSunAngleCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, -50f);
        curve.AddKey(0.2f, -8f);
        curve.AddKey(0.25f, 0f);
        curve.AddKey(0.5f, 60f);
        curve.AddKey(0.75f, 0f);
        curve.AddKey(0.8f, -8f);
        curve.AddKey(1f, -50f);
        return curve;
    }
    
    private static AnimationCurve CreateDefaultAmbientIntensityCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.2f);
        curve.AddKey(0.25f, 0.5f);
        curve.AddKey(0.5f, 1f);
        curve.AddKey(0.75f, 0.5f);
        curve.AddKey(1f, 0.2f);
        return curve;
    }
    
    private static Gradient CreateDefaultAmbientColorGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 0f),      // Night
                new GradientColorKey(new Color(0.5f, 0.4f, 0.5f), 0.25f),   // Dawn
                new GradientColorKey(new Color(0.6f, 0.7f, 0.8f), 0.5f),    // Day
                new GradientColorKey(new Color(0.5f, 0.3f, 0.4f), 0.75f),   // Dusk
                new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 1f)       // Night
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static AnimationCurve CreateDefaultFogDensityCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.02f);   // Night: moderate fog
        curve.AddKey(0.25f, 0.03f); // Dawn: morning mist
        curve.AddKey(0.5f, 0.01f); // Day: clear
        curve.AddKey(0.75f, 0.015f); // Dusk: slight haze
        curve.AddKey(1f, 0.02f);   // Night
        return curve;
    }
    
    private static Gradient CreateDefaultFogColorGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 0f),     // Night
                new GradientColorKey(new Color(0.7f, 0.5f, 0.5f), 0.25f),   // Dawn
                new GradientColorKey(new Color(0.7f, 0.8f, 0.9f), 0.5f),    // Day
                new GradientColorKey(new Color(0.6f, 0.4f, 0.4f), 0.75f),   // Dusk
                new GradientColorKey(new Color(0.1f, 0.1f, 0.15f), 1f)      // Night
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static AnimationCurve CreateDefaultExposureCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, -0.5f);   // Night: slightly darker
        curve.AddKey(0.25f, 0.2f); // Dawn
        curve.AddKey(0.5f, 0f);    // Day: neutral
        curve.AddKey(0.75f, 0.1f); // Dusk
        curve.AddKey(1f, -0.5f);   // Night
        return curve;
    }
    
    private static Gradient CreateDefaultColorFilterGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.7f, 0.7f, 0.9f), 0f),      // Night: blue tint
                new GradientColorKey(new Color(1f, 0.95f, 0.9f), 0.25f),    // Dawn: warm
                new GradientColorKey(Color.white, 0.5f),                    // Day: neutral
                new GradientColorKey(new Color(1f, 0.9f, 0.85f), 0.75f),    // Dusk: warm
                new GradientColorKey(new Color(0.7f, 0.7f, 0.9f), 1f)       // Night: blue tint
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static Gradient CreateDefaultSkyboxTintGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 0f),      // Night
                new GradientColorKey(new Color(0.9f, 0.6f, 0.5f), 0.25f),   // Dawn
                new GradientColorKey(new Color(0.5f, 0.7f, 1f), 0.5f),      // Day
                new GradientColorKey(new Color(0.9f, 0.5f, 0.4f), 0.75f),   // Dusk
                new GradientColorKey(new Color(0.1f, 0.1f, 0.2f), 1f)       // Night
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    private static AnimationCurve CreateDefaultSkyboxExposureCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.3f);
        curve.AddKey(0.25f, 0.8f);
        curve.AddKey(0.5f, 1.2f);
        curve.AddKey(0.75f, 0.8f);
        curve.AddKey(1f, 0.3f);
        return curve;
    }
    
    private void OnValidate()
    {
        fullDaySeconds = Mathf.Max(1f, fullDaySeconds);
        
        if (!segmentThresholds.IsValid())
        {
            Debug.LogWarning($"[TimeOfDayConfigSO] Invalid segment thresholds in {name}. Using defaults.");
            segmentThresholds = TimeSegmentThresholds.Default;
        }
    }
}

