using UnityEngine;

/// <summary>
/// Creates a subtle alternating multicolored string lights effect for pixel-art Christmas trees.
/// Uses MaterialPropertyBlock for emission control - no material instantiation, mobile-friendly.
/// 
/// Setup:
/// 1. Attach to the root tree GameObject
/// 2. Assign target renderers (or leave empty for auto-find)
/// 3. Configure palette colors and timing in inspector
/// 4. Optionally assign a point light for subtle real light contribution
/// </summary>
[ExecuteAlways]
public class XmasTreeStringLights : MonoBehaviour
{
    // ============================================
    // REFERENCES
    // ============================================
    
    [Header("References")]
    [Tooltip("Renderers to apply emission effect. Auto-populates from children if empty.")]
    [SerializeField] private Renderer[] targetRenderers;
    
    [Tooltip("Optional point light for tiny real light contribution (disabled by default)")]
    [SerializeField] private Light optionalPointLight;
    
    [Tooltip("Use the optional point light")]
    [SerializeField] private bool usePointLight = false;
    
    // ============================================
    // EMISSION SETTINGS
    // ============================================
    
    [Header("Emission Settings")]
    [Tooltip("Shader property name for emission color")]
    [SerializeField] private string emissionColorProperty = "_EmissionColor";
    
    [Tooltip("Enable emission keyword on materials at start")]
    [SerializeField] private bool enableEmissionKeyword = true;
    
    [Tooltip("Base emission intensity (subtle default)")]
    [Range(0f, 2f)]
    [SerializeField] private float emissionIntensity = 0.35f;
    
    [Tooltip("Peak intensity multiplier during pulse")]
    [Range(1f, 3f)]
    [SerializeField] private float maxIntensityMultiplier = 1.15f;
    
    [Tooltip("Low intensity multiplier during pulse")]
    [Range(0f, 1f)]
    [SerializeField] private float minIntensityMultiplier = 0.85f;
    
    // ============================================
    // COLOR CYCLE SETTINGS
    // ============================================
    
    [Header("Color Cycle")]
    [Tooltip("Palette of colors to cycle through")]
    [SerializeField] private Color[] palette = new Color[]
    {
        new Color(1f, 0.2f, 0.2f),    // Red
        new Color(0.2f, 1f, 0.3f),    // Green
        new Color(0.3f, 0.5f, 1f),    // Blue
        new Color(1f, 0.9f, 0.2f),    // Yellow
        new Color(1f, 0.4f, 0.7f)     // Pink
    };
    
    [Tooltip("Colors per second (phase speed)")]
    [Range(0.1f, 5f)]
    [SerializeField] private float cycleSpeed = 1.0f;
    
    [Tooltip("Smoothly blend between colors vs hard step")]
    [SerializeField] private bool smoothColorBlend = true;
    
    [Tooltip("Optional curve for color blend easing")]
    [SerializeField] private AnimationCurve blendCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    
    // ============================================
    // ALTERNATION / TIMING
    // ============================================
    
    [Header("Alternation / Timing")]
    [Tooltip("Pulse/alternation speed")]
    [Range(0.1f, 5f)]
    [SerializeField] private float alternationSpeed = 1.0f;
    
    [Tooltip("Phase offset per renderer in the list (creates wave effect)")]
    [Range(0f, 1f)]
    [SerializeField] private float alternationOffsetPerRenderer = 0.12f;
    
    [Tooltip("Use unscaled time (ignores Time.timeScale)")]
    [SerializeField] private bool useUnscaledTime = false;
    
    // ============================================
    // RANDOMIZATION (OPTIONAL)
    // ============================================
    
    [Header("Randomization")]
    [Tooltip("Seed for deterministic random variations")]
    [SerializeField] private int randomSeed = 12345;
    
    [Tooltip("Random phase jitter per renderer")]
    [Range(0f, 0.5f)]
    [SerializeField] private float randomPhaseJitter = 0.08f;
    
    [Tooltip("Tiny noise amount for natural variation")]
    [Range(0f, 0.3f)]
    [SerializeField] private float noiseAmount = 0.05f;
    
    [Tooltip("Speed of noise variation")]
    [Range(0.1f, 3f)]
    [SerializeField] private float noiseSpeed = 0.8f;
    
    // ============================================
    // OPTIONAL POINT LIGHT SETTINGS
    // ============================================
    
    [Header("Point Light (Optional)")]
    [Tooltip("Point light intensity when enabled")]
    [Range(0f, 1f)]
    [SerializeField] private float pointLightIntensity = 0.2f;
    
    [Tooltip("Point light range")]
    [Range(0.5f, 5f)]
    [SerializeField] private float pointLightRange = 1.5f;
    
    // ============================================
    // RUNTIME CONTROLS
    // ============================================
    
    [Header("Runtime")]
    [Tooltip("Enable/disable the effect")]
    [SerializeField] private bool isActive = true;
    
    [Tooltip("Preview in editor (may impact editor performance)")]
    [SerializeField] private bool enableEditorPreview = false;
    
    // ============================================
    // CACHED STATE (No allocations in Update)
    // ============================================
    
    // Reusable MaterialPropertyBlock (single instance, reused for all renderers)
    private MaterialPropertyBlock mpb;
    
    // Cached property ID
    private int emissionPropertyId;
    
    // Pre-computed per-renderer jitter offsets
    private float[] rendererJitterOffsets;
    
    // Fallback palette if user provides invalid one
    private static readonly Color[] FallbackPalette = new Color[]
    {
        new Color(1f, 0.3f, 0.3f),
        new Color(0.3f, 1f, 0.4f)
    };
    
    // Constants
    private const float TwoPi = Mathf.PI * 2f;
    
    // Cached color for point light (avoids allocation)
    private Color cachedLightColor;
    
    // ============================================
    // PROPERTIES
    // ============================================
    
    /// <summary>Is the effect currently active?</summary>
    public bool IsActive => isActive;
    
    /// <summary>Current emission intensity setting.</summary>
    public float EmissionIntensity => emissionIntensity;
    
    /// <summary>Number of target renderers.</summary>
    public int RendererCount => targetRenderers?.Length ?? 0;
    
    // ============================================
    // LIFECYCLE
    // ============================================
    
    private void Awake()
    {
        Initialize();
    }
    
    private void OnEnable()
    {
        Initialize();
        
        // Enable emission keyword on materials if needed
        if (enableEmissionKeyword && Application.isPlaying)
        {
            EnableEmissionKeywords();
        }
    }
    
    private void OnDisable()
    {
        // Reset emission to black when disabled
        if (mpb != null && targetRenderers != null)
        {
            mpb.SetColor(emissionPropertyId, Color.black);
            foreach (var renderer in targetRenderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(mpb);
                }
            }
        }
        
        // Disable point light
        if (optionalPointLight != null)
        {
            optionalPointLight.enabled = false;
        }
    }
    
    private void Initialize()
    {
        // Create MaterialPropertyBlock once
        if (mpb == null)
        {
            mpb = new MaterialPropertyBlock();
        }
        
        // Cache property ID
        emissionPropertyId = Shader.PropertyToID(emissionColorProperty);
        
        // Auto-find renderers if not assigned
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            FindTargetRenderers();
        }
        
        // Validate palette
        ValidatePalette();
        
        // Pre-compute jitter offsets
        ComputeJitterOffsets();
        
        // Configure point light
        ConfigurePointLight();
    }
    
    private void FindTargetRenderers()
    {
        // Get all renderers in children, excluding particle systems
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        
        // Count valid renderers (exclude ParticleSystemRenderer)
        int validCount = 0;
        foreach (var r in allRenderers)
        {
            if (r != null && !(r is ParticleSystemRenderer))
            {
                validCount++;
            }
        }
        
        // Allocate array (only happens in Initialize, not Update)
        targetRenderers = new Renderer[validCount];
        
        int index = 0;
        foreach (var r in allRenderers)
        {
            if (r != null && !(r is ParticleSystemRenderer))
            {
                targetRenderers[index++] = r;
            }
        }
    }
    
    private void ValidatePalette()
    {
        if (palette == null || palette.Length < 2)
        {
            palette = FallbackPalette;
            Debug.LogWarning($"[XmasTreeStringLights] Palette must have at least 2 colors. Using fallback palette on {gameObject.name}");
        }
    }
    
    private void ComputeJitterOffsets()
    {
        if (targetRenderers == null) return;
        
        // Pre-allocate jitter array
        if (rendererJitterOffsets == null || rendererJitterOffsets.Length != targetRenderers.Length)
        {
            rendererJitterOffsets = new float[targetRenderers.Length];
        }
        
        // Use deterministic random based on seed
        System.Random rng = new System.Random(randomSeed);
        
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            // Generate jitter in range [-jitter, +jitter]
            float jitter = ((float)rng.NextDouble() * 2f - 1f) * randomPhaseJitter;
            rendererJitterOffsets[i] = jitter;
        }
    }
    
    private void EnableEmissionKeywords()
    {
        if (targetRenderers == null) return;
        
        foreach (var renderer in targetRenderers)
        {
            if (renderer == null) continue;
            
            // Enable emission keyword on shared materials (safe, doesn't instantiate)
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.HasProperty(emissionColorProperty))
                {
                    mat.EnableKeyword("_EMISSION");
                }
            }
        }
    }
    
    private void ConfigurePointLight()
    {
        if (optionalPointLight == null) return;
        
        // Configure point light settings
        optionalPointLight.type = LightType.Point;
        optionalPointLight.range = pointLightRange;
        optionalPointLight.shadows = LightShadows.None; // Performance
        optionalPointLight.enabled = usePointLight && isActive;
    }
    
    // ============================================
    // UPDATE LOOP
    // ============================================
    
    private void Update()
    {
        // Skip if not active
        if (!isActive) return;
        
        // In editor, only update if preview is enabled
        if (!Application.isPlaying && !enableEditorPreview) return;
        
        // Skip if no renderers
        if (targetRenderers == null || targetRenderers.Length == 0) return;
        
        UpdateEmission();
    }
    
    private void UpdateEmission()
    {
        // Get time value
        float t = useUnscaledTime ? Time.unscaledTime : Time.time;
        
        // Calculate color phase
        float colorPhase = t * cycleSpeed;
        
        // Determine palette indices
        int paletteLength = palette.Length;
        int indexA = Mathf.FloorToInt(colorPhase) % paletteLength;
        if (indexA < 0) indexA += paletteLength; // Handle negative modulo
        int indexB = (indexA + 1) % paletteLength;
        
        // Calculate blend factor
        float blendRaw = colorPhase - Mathf.Floor(colorPhase); // frac(colorPhase)
        float blend = smoothColorBlend ? (blendCurve != null ? blendCurve.Evaluate(blendRaw) : blendRaw) : 0f;
        
        // Get base color (lerp or hard step)
        Color baseColor;
        if (smoothColorBlend)
        {
            baseColor = Color.Lerp(palette[indexA], palette[indexB], blend);
        }
        else
        {
            baseColor = palette[indexA];
        }
        
        // Store for point light
        cachedLightColor = baseColor;
        
        // Calculate base pulse
        float basePulse = 0.5f + 0.5f * Mathf.Sin(t * alternationSpeed * TwoPi);
        
        // Apply to each renderer
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer renderer = targetRenderers[i];
            if (renderer == null) continue;
            
            // Calculate per-renderer local time with offset and jitter
            float localT = t + (i * alternationOffsetPerRenderer);
            if (rendererJitterOffsets != null && i < rendererJitterOffsets.Length)
            {
                localT += rendererJitterOffsets[i];
            }
            
            // Calculate local pulse
            float localPulse = 0.5f + 0.5f * Mathf.Sin(localT * alternationSpeed * TwoPi);
            
            // Apply blend curve if present
            if (blendCurve != null)
            {
                localPulse = blendCurve.Evaluate(localPulse);
            }
            
            // Calculate intensity multiplier
            float intensityMult = Mathf.Lerp(minIntensityMultiplier, maxIntensityMultiplier, localPulse);
            
            // Add noise variation
            if (noiseAmount > 0f)
            {
                // Perlin noise for smooth variation (no allocation)
                float noise = Mathf.PerlinNoise(localT * noiseSpeed, randomSeed + i * 0.1f);
                intensityMult += (noise - 0.5f) * noiseAmount * 2f;
            }
            
            // Calculate final emission color
            float finalIntensity = emissionIntensity * intensityMult;
            Color finalColor = baseColor * finalIntensity;
            
            // Get existing property block (preserves other properties)
            renderer.GetPropertyBlock(mpb);
            
            // Set emission color
            mpb.SetColor(emissionPropertyId, finalColor);
            
            // Apply property block
            renderer.SetPropertyBlock(mpb);
        }
        
        // Update optional point light
        UpdatePointLight();
    }
    
    private void UpdatePointLight()
    {
        if (optionalPointLight == null) return;
        
        if (usePointLight && isActive)
        {
            if (!optionalPointLight.enabled)
            {
                optionalPointLight.enabled = true;
            }
            
            optionalPointLight.color = cachedLightColor;
            optionalPointLight.intensity = pointLightIntensity;
            optionalPointLight.range = pointLightRange;
        }
        else
        {
            if (optionalPointLight.enabled)
            {
                optionalPointLight.enabled = false;
            }
        }
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Enable or disable the string lights effect.
    /// </summary>
    public void SetActive(bool active)
    {
        isActive = active;
        
        if (!active)
        {
            // Reset emission when deactivated
            if (mpb != null && targetRenderers != null)
            {
                mpb.SetColor(emissionPropertyId, Color.black);
                foreach (var renderer in targetRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.SetPropertyBlock(mpb);
                    }
                }
            }
            
            // Disable point light
            if (optionalPointLight != null)
            {
                optionalPointLight.enabled = false;
            }
        }
    }
    
    /// <summary>
    /// Set base emission intensity.
    /// </summary>
    /// <param name="intensity">Intensity value (0-2 typical range)</param>
    public void SetIntensity(float intensity)
    {
        emissionIntensity = Mathf.Max(0f, intensity);
    }
    
    /// <summary>
    /// Set cycle and alternation speeds.
    /// </summary>
    /// <param name="cycle">Color cycle speed (colors per second)</param>
    /// <param name="alternation">Pulse alternation speed</param>
    public void SetSpeed(float cycle, float alternation)
    {
        cycleSpeed = Mathf.Max(0.01f, cycle);
        alternationSpeed = Mathf.Max(0.01f, alternation);
    }
    
    /// <summary>
    /// Set a new color palette (defensive copy).
    /// </summary>
    /// <param name="newPalette">Array of colors (minimum 2)</param>
    public void SetPalette(Color[] newPalette)
    {
        if (newPalette == null || newPalette.Length < 2)
        {
            Debug.LogWarning("[XmasTreeStringLights] Palette must have at least 2 colors!");
            return;
        }
        
        // Defensive copy
        if (palette == null || palette.Length != newPalette.Length)
        {
            palette = new Color[newPalette.Length];
        }
        
        for (int i = 0; i < newPalette.Length; i++)
        {
            palette[i] = newPalette[i];
        }
    }
    
    /// <summary>
    /// Set individual palette color at index.
    /// </summary>
    /// <param name="index">Palette index</param>
    /// <param name="color">New color</param>
    public void SetPaletteColor(int index, Color color)
    {
        if (palette != null && index >= 0 && index < palette.Length)
        {
            palette[index] = color;
        }
    }
    
    /// <summary>
    /// Enable or disable the optional point light.
    /// </summary>
    public void SetPointLightEnabled(bool enabled)
    {
        usePointLight = enabled;
        if (optionalPointLight != null)
        {
            optionalPointLight.enabled = enabled && isActive;
        }
    }
    
    /// <summary>
    /// Force re-initialization (useful after runtime renderer changes).
    /// </summary>
    public void Reinitialize()
    {
        targetRenderers = null;
        rendererJitterOffsets = null;
        Initialize();
    }
    
    // ============================================
    // EDITOR SUPPORT
    // ============================================
    
    private void OnValidate()
    {
        // Clamp values
        emissionIntensity = Mathf.Max(0f, emissionIntensity);
        cycleSpeed = Mathf.Max(0.01f, cycleSpeed);
        alternationSpeed = Mathf.Max(0.01f, alternationSpeed);
        minIntensityMultiplier = Mathf.Clamp(minIntensityMultiplier, 0f, maxIntensityMultiplier);
        maxIntensityMultiplier = Mathf.Max(minIntensityMultiplier, maxIntensityMultiplier);
        noiseAmount = Mathf.Max(0f, noiseAmount);
        noiseSpeed = Mathf.Max(0.01f, noiseSpeed);
        randomPhaseJitter = Mathf.Max(0f, randomPhaseJitter);
        pointLightIntensity = Mathf.Max(0f, pointLightIntensity);
        pointLightRange = Mathf.Max(0.1f, pointLightRange);
        
        // Re-validate palette
        if (palette != null && palette.Length < 2)
        {
            Debug.LogWarning("[XmasTreeStringLights] Palette should have at least 2 colors!");
        }
        
        // Recompute jitter if seed changed
        if (Application.isPlaying)
        {
            ComputeJitterOffsets();
        }
        
        // Update point light config
        ConfigurePointLight();
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (targetRenderers == null) return;
        
        // Draw spheres at renderer positions to visualize targets
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.5f);
        foreach (var renderer in targetRenderers)
        {
            if (renderer != null)
            {
                Gizmos.DrawWireSphere(renderer.bounds.center, 0.15f);
            }
        }
        
        // Draw point light range
        if (optionalPointLight != null && usePointLight)
        {
            Gizmos.color = new Color(1f, 1f, 0.5f, 0.3f);
            Gizmos.DrawWireSphere(optionalPointLight.transform.position, pointLightRange);
        }
    }
#endif
}

