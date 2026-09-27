using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ScriptableObject defining indoor lighting settings, independent of outdoor time-of-day curves.
/// When player is indoors, these values override outdoor visuals.
/// Optionally supports time-based color/intensity changes to give players a sense of the day/night cycle.
/// </summary>
[CreateAssetMenu(fileName = "IndoorVisualProfile", menuName = "Chainbound/World/Time of Day/Indoor Visual Profile")]
public class IndoorVisualProfileSO : ScriptableObject
{
    [Header("Fog")]
    [Tooltip("Disable fog entirely when indoors (recommended)")]
    [SerializeField] private bool disableFogIndoors = true;
    
    [Header("Time-Based Indoor Lighting")]
    [Tooltip("Enable time-based color/intensity changes indoors (gives players a subtle sense of day/night cycle)")]
    [SerializeField] private bool useTimeBasedIndoorLighting = false;
    
    [Header("Ambient Light")]
    [Tooltip("Indoor ambient light color (used when time-based lighting is disabled)")]
    [SerializeField] private Color indoorAmbientColor = new Color(0.4f, 0.4f, 0.45f);
    
    [Tooltip("Indoor ambient light intensity multiplier (used when time-based lighting is disabled)")]
    [Range(0f, 2f)]
    [SerializeField] private float indoorAmbientIntensity = 0.8f;
    
    [Tooltip("Indoor ambient light color over time (used when time-based lighting is enabled)")]
    [SerializeField] private Gradient indoorAmbientColorOverTime = CreateDefaultIndoorAmbientGradient();
    
    [Tooltip("Indoor ambient intensity over time (used when time-based lighting is enabled)")]
    [SerializeField] private AnimationCurve indoorAmbientIntensityOverTime = CreateDefaultIndoorAmbientIntensityCurve();
    
    [Header("Directional Light")]
    [Tooltip("Indoor directional light color (used when time-based lighting is disabled)")]
    [SerializeField] private Color indoorDirectionalColor = new Color(1f, 0.95f, 0.9f);
    
    [Tooltip("Indoor directional light intensity (used when time-based lighting is disabled)")]
    [Range(0f, 2f)]
    [SerializeField] private float indoorDirectionalIntensity = 0.5f;
    
    [Tooltip("Indoor directional light color over time (used when time-based lighting is enabled). Subtle variations simulate light through windows.")]
    [SerializeField] private Gradient indoorDirectionalColorOverTime = CreateDefaultIndoorDirectionalGradient();
    
    [Tooltip("Indoor directional intensity over time (used when time-based lighting is enabled). Brighter during day, dimmer at night.")]
    [SerializeField] private AnimationCurve indoorDirectionalIntensityOverTime = CreateDefaultIndoorDirectionalIntensityCurve();
    
    [Tooltip("Use the rotation set in the scene editor instead of overriding with indoorDirectionalRotation")]
    [SerializeField] private bool useSceneAuthoredRotation = false;
    
    [Tooltip("Fixed indoor directional rotation (Euler angles). Only used if useSceneAuthoredRotation is false.")]
    [SerializeField] private Vector3 indoorDirectionalRotation = new Vector3(50f, -30f, 0f);
    
    [Tooltip("Shadow strength for indoor directional light")]
    [Range(0f, 1f)]
    [SerializeField] private float indoorShadowStrength = 0.5f;
    
    [Tooltip("Disable outdoor sun entirely when indoors (prevents any outdoor shadow leaks)")]
    [SerializeField] private bool disableOutdoorSunIndoors = true;
    
    [Header("URP Volume (Optional)")]
    [Tooltip("Use a separate URP volume profile for indoor scenes")]
    [SerializeField] private bool useIndoorURPVolume = false;
    
    [Tooltip("Indoor volume profile (applied when indoors)")]
    [SerializeField] private VolumeProfile indoorVolumeProfile;
    
    [Tooltip("Volume weight when indoors (0 = no effect, 1 = full effect)")]
    [Range(0f, 1f)]
    [SerializeField] private float indoorVolumeWeight = 1f;
    
    // ============================================
    // PUBLIC ACCESSORS
    // ============================================
    
    public bool DisableFogIndoors => disableFogIndoors;
    public bool UseTimeBasedIndoorLighting => useTimeBasedIndoorLighting;
    
    // Static values (used when time-based is disabled)
    public Color IndoorAmbientColor => indoorAmbientColor;
    public float IndoorAmbientIntensity => indoorAmbientIntensity;
    public Color IndoorDirectionalColor => indoorDirectionalColor;
    public float IndoorDirectionalIntensity => indoorDirectionalIntensity;
    
    // Time-based evaluation methods
    public Color EvaluateIndoorAmbientColor(float normalizedTime) => 
        useTimeBasedIndoorLighting ? indoorAmbientColorOverTime.Evaluate(normalizedTime) : indoorAmbientColor;
    
    public float EvaluateIndoorAmbientIntensity(float normalizedTime) => 
        useTimeBasedIndoorLighting ? indoorAmbientIntensityOverTime.Evaluate(normalizedTime) : indoorAmbientIntensity;
    
    public Color EvaluateIndoorDirectionalColor(float normalizedTime) => 
        useTimeBasedIndoorLighting ? indoorDirectionalColorOverTime.Evaluate(normalizedTime) : indoorDirectionalColor;
    
    public float EvaluateIndoorDirectionalIntensity(float normalizedTime) => 
        useTimeBasedIndoorLighting ? indoorDirectionalIntensityOverTime.Evaluate(normalizedTime) : indoorDirectionalIntensity;
    
    // Rotation & shadows
    public bool UseSceneAuthoredRotation => useSceneAuthoredRotation;
    public Vector3 IndoorDirectionalRotation => indoorDirectionalRotation;
    public float IndoorShadowStrength => indoorShadowStrength;
    public bool DisableOutdoorSunIndoors => disableOutdoorSunIndoors;
    
    // Volume
    public bool UseIndoorURPVolume => useIndoorURPVolume;
    public VolumeProfile IndoorVolumeProfile => indoorVolumeProfile;
    public float IndoorVolumeWeight => indoorVolumeWeight;
    
    // ============================================
    // DEFAULT CURVE/GRADIENT CREATION
    // ============================================
    
    /// <summary>
    /// Default indoor ambient color gradient - subtle warm/cool shift through the day.
    /// More subdued than outdoor to feel like indoor lighting.
    /// </summary>
    private static Gradient CreateDefaultIndoorAmbientGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.3f, 0.3f, 0.4f), 0f),       // Night: cool dim
                new GradientColorKey(new Color(0.4f, 0.38f, 0.42f), 0.25f),  // Dawn: slightly warm
                new GradientColorKey(new Color(0.45f, 0.45f, 0.48f), 0.5f),  // Day: neutral bright
                new GradientColorKey(new Color(0.42f, 0.38f, 0.4f), 0.75f),  // Dusk: warm tint
                new GradientColorKey(new Color(0.3f, 0.3f, 0.4f), 1f)        // Night: cool dim
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    /// <summary>
    /// Default indoor ambient intensity curve - lower at night, higher during day.
    /// </summary>
    private static AnimationCurve CreateDefaultIndoorAmbientIntensityCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.5f);     // Night: dim
        curve.AddKey(0.25f, 0.65f); // Dawn
        curve.AddKey(0.5f, 0.8f);   // Day: brightest (but still indoor-level)
        curve.AddKey(0.75f, 0.65f); // Dusk
        curve.AddKey(1f, 0.5f);     // Night
        return curve;
    }
    
    /// <summary>
    /// Default indoor directional color gradient - simulates light coming through windows.
    /// Warm golden during day, cool blue-ish at night (simulating moonlight/artificial light).
    /// </summary>
    private static Gradient CreateDefaultIndoorDirectionalGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.6f, 0.65f, 0.8f), 0f),      // Night: cool moonlight
                new GradientColorKey(new Color(1f, 0.85f, 0.7f), 0.25f),     // Dawn: warm orange
                new GradientColorKey(new Color(1f, 0.98f, 0.92f), 0.5f),     // Day: warm white
                new GradientColorKey(new Color(1f, 0.8f, 0.65f), 0.75f),     // Dusk: golden
                new GradientColorKey(new Color(0.6f, 0.65f, 0.8f), 1f)       // Night: cool moonlight
            },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
        );
        return gradient;
    }
    
    /// <summary>
    /// Default indoor directional intensity curve - brighter during day (simulating sunlight through windows).
    /// Much lower at night since there's no direct sunlight.
    /// </summary>
    private static AnimationCurve CreateDefaultIndoorDirectionalIntensityCurve()
    {
        var curve = new AnimationCurve();
        curve.AddKey(0f, 0.15f);    // Night: very dim (moonlight/ambient)
        curve.AddKey(0.2f, 0.2f);   // Late night
        curve.AddKey(0.3f, 0.45f);  // Dawn: rising
        curve.AddKey(0.5f, 0.6f);   // Day: brightest (still subdued vs outdoor)
        curve.AddKey(0.7f, 0.45f);  // Evening
        curve.AddKey(0.8f, 0.2f);   // Dusk: fading
        curve.AddKey(1f, 0.15f);    // Night
        return curve;
    }
}
