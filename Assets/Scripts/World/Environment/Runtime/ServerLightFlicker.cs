using UnityEngine;

/// <summary>
/// Controls per-instance LED flicker parameters for server rack overlay quads.
/// Uses MaterialPropertyBlock to avoid material instantiation - SRP Batcher friendly.
/// 
/// Setup:
/// 1. Attach to the overlay quad GameObject (the "lights ON" layer)
/// 2. Assign the MeshRenderer (or leave empty for auto-find)
/// 3. Configure flicker parameters in inspector
/// 4. Use deterministic seed from position for consistent per-rack variation
/// 
/// Performance:
/// - No Update() allocations
/// - Single MaterialPropertyBlock per instance
/// - All instances can share one material
/// </summary>
[ExecuteAlways]
public class ServerLightFlicker : MonoBehaviour
{
    // ============================================
    // REFERENCES
    // ============================================
    
    [Header("References")]
    [Tooltip("Overlay quad renderer. Auto-finds if not assigned.")]
    [SerializeField] private MeshRenderer overlayRenderer;
    
    [Tooltip("LED mask texture (optional - reads from material if not set)")]
    [SerializeField] private Texture2D maskTex;
    
    // ============================================
    // FLICKER SETTINGS
    // ============================================
    
    [Header("Flicker Settings")]
    [Tooltip("Blink speed - higher = faster flickering")]
    [Range(0.1f, 20f)]
    [SerializeField] private float speed = 4f;
    
    [Tooltip("Probability that an LED is ON per time step (0 = all off, 1 = all on)")]
    [Range(0f, 1f)]
    [SerializeField] private float density = 0.45f;
    
    [Tooltip("Emission intensity multiplier")]
    [Range(0f, 5f)]
    [SerializeField] private float emission = 1.5f;
    
    [Tooltip("LED tint color")]
    [SerializeField] private Color tint = new Color(0f, 1f, 0.85f, 1f);
    
    [Tooltip("Alpha clip threshold for crisp pixel edges")]
    [Range(0f, 1f)]
    [SerializeField] private float clipThreshold = 0.5f;
    
    // ============================================
    // SEED / VARIATION
    // ============================================
    
    [Header("Seed & Variation")]
    [Tooltip("Generate seed from world position for deterministic per-rack variation")]
    [SerializeField] private bool deterministicSeedFromPosition = true;
    
    [Tooltip("Manual seed override (used if deterministicSeedFromPosition is false)")]
    [SerializeField] private float seedOverride = 0f;
    
    [Tooltip("Include Y position in seed hash (disable for same pattern on stacked racks)")]
    [SerializeField] private bool includePosY = true;
    
    // ============================================
    // GRID LAYOUT (match your texture)
    // ============================================
    
    [Header("Grid Layout")]
    [Tooltip("Texture width in pixels")]
    [SerializeField] private int texWidth = 23;
    
    [Tooltip("Texture height in pixels")]
    [SerializeField] private int texHeight = 27;
    
    [Tooltip("Number of LED columns")]
    [SerializeField] private int columns = 17;
    
    [Tooltip("Number of logical LED rows")]
    [SerializeField] private int rows = 15;
    
    [Tooltip("X pixel position of first LED (from left)")]
    [SerializeField] private float ledStartX = 4f;
    
    [Tooltip("LED size in pixels")]
    [SerializeField] private float ledSizePx = 1f;
    
    [Tooltip("Gap between LEDs in pixels")]
    [SerializeField] private float ledGapPx = 1f;
    
    // ============================================
    // FAKE GLOW (optional)
    // ============================================
    
    [Header("Fake Glow")]
    [Tooltip("Enable soft glow around LEDs")]
    [SerializeField] private bool useFakeGlow = false;
    
    [Tooltip("Glow intensity")]
    [Range(0f, 1f)]
    [SerializeField] private float glowStrength = 0.3f;
    
    [Tooltip("Glow spread in pixels")]
    [Range(0.5f, 3f)]
    [SerializeField] private float glowSpread = 1f;
    
    // ============================================
    // ROW POSITIONS (for irregular spacing)
    // ============================================
    
    [Header("Row Y Positions (Advanced)")]
    [Tooltip("Override default row Y positions. Leave empty to use shader defaults.")]
    [SerializeField] private float[] rowYPositions;
    
    // ============================================
    // DEBUG
    // ============================================
    
    [Header("Debug")]
    [Tooltip("Log initialization details")]
    [SerializeField] private bool debugLog = false;
    
    // ============================================
    // CACHED STATE
    // ============================================
    
    // Single MaterialPropertyBlock instance - reused, no allocations
    private MaterialPropertyBlock mpb;
    
    // Cached property IDs (computed once)
    private static readonly int PropTint = Shader.PropertyToID("_Tint");
    private static readonly int PropEmission = Shader.PropertyToID("_Emission");
    private static readonly int PropClip = Shader.PropertyToID("_Clip");
    private static readonly int PropSpeed = Shader.PropertyToID("_Speed");
    private static readonly int PropDensity = Shader.PropertyToID("_Density");
    private static readonly int PropSeed = Shader.PropertyToID("_Seed");
    private static readonly int PropTexSize = Shader.PropertyToID("_TexSize");
    private static readonly int PropCols = Shader.PropertyToID("_Cols");
    private static readonly int PropRows = Shader.PropertyToID("_Rows");
    private static readonly int PropLedStartX = Shader.PropertyToID("_LedStartX");
    private static readonly int PropLedSizePx = Shader.PropertyToID("_LedSizePx");
    private static readonly int PropLedGapPx = Shader.PropertyToID("_LedGapPx");
    private static readonly int PropUseFakeGlow = Shader.PropertyToID("_UseFakeGlow");
    private static readonly int PropGlowStrength = Shader.PropertyToID("_GlowStrength");
    private static readonly int PropGlowSpread = Shader.PropertyToID("_GlowSpread");
    
    // Computed seed value
    private float computedSeed;
    
    // Track if initialized
    private bool isInitialized;
    
    // ============================================
    // PROPERTIES
    // ============================================
    
    /// <summary>Current flicker speed.</summary>
    public float Speed => speed;
    
    /// <summary>Current LED on probability.</summary>
    public float Density => density;
    
    /// <summary>Current emission intensity.</summary>
    public float Emission => emission;
    
    /// <summary>Current tint color.</summary>
    public Color Tint => tint;
    
    /// <summary>Computed seed value for this instance.</summary>
    public float ComputedSeed => computedSeed;
    
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
        ApplyPropertyBlock();
    }
    
    private void Start()
    {
        // Ensure properties are applied on start
        if (Application.isPlaying)
        {
            ApplyPropertyBlock();
        }
    }
    
    private void Initialize()
    {
        if (isInitialized) return;
        
        // Create MaterialPropertyBlock
        if (mpb == null)
        {
            mpb = new MaterialPropertyBlock();
        }
        
        // Auto-find renderer if not assigned
        if (overlayRenderer == null)
        {
            overlayRenderer = GetComponent<MeshRenderer>();
            if (overlayRenderer == null)
            {
                overlayRenderer = GetComponentInChildren<MeshRenderer>();
            }
        }
        
        if (overlayRenderer == null)
        {
            Debug.LogWarning($"[ServerLightFlicker] No MeshRenderer found on {gameObject.name}");
            return;
        }
        
        // Try to get texture size from material if not using override texture
        if (maskTex == null && overlayRenderer.sharedMaterial != null)
        {
            var mainTex = overlayRenderer.sharedMaterial.GetTexture("_MaskTex");
            if (mainTex != null)
            {
                maskTex = mainTex as Texture2D;
            }
        }
        
        // Update texture dimensions if we have a texture
        if (maskTex != null)
        {
            texWidth = maskTex.width;
            texHeight = maskTex.height;
        }
        
        // Compute seed
        ComputeSeed();
        
        isInitialized = true;
        
        if (debugLog)
        {
            Debug.Log($"[ServerLightFlicker] Initialized on {gameObject.name} - " +
                      $"Seed: {computedSeed:F2}, TexSize: {texWidth}x{texHeight}");
        }
    }
    
    private void ComputeSeed()
    {
        if (deterministicSeedFromPosition)
        {
            // Generate deterministic seed from world position
            Vector3 pos = transform.position;
            
            // Hash position to get consistent seed per rack
            float hashInput = pos.x * 73.137f + pos.z * 179.213f;
            if (includePosY)
            {
                hashInput += pos.y * 311.77f;
            }
            
            // Create seed in reasonable range
            computedSeed = Mathf.Abs(hashInput % 10000f);
        }
        else
        {
            computedSeed = seedOverride;
        }
    }
    
    // ============================================
    // PROPERTY BLOCK APPLICATION
    // ============================================
    
    /// <summary>
    /// Apply all properties to the MaterialPropertyBlock.
    /// Call this after changing any parameters at runtime.
    /// </summary>
    public void ApplyPropertyBlock()
    {
        if (overlayRenderer == null || mpb == null) return;
        
        // Get existing property block (preserves other properties)
        overlayRenderer.GetPropertyBlock(mpb);
        
        // Set all shader properties
        mpb.SetColor(PropTint, tint);
        mpb.SetFloat(PropEmission, emission);
        mpb.SetFloat(PropClip, clipThreshold);
        mpb.SetFloat(PropSpeed, speed);
        mpb.SetFloat(PropDensity, density);
        mpb.SetFloat(PropSeed, computedSeed);
        mpb.SetVector(PropTexSize, new Vector4(texWidth, texHeight, 0, 0));
        mpb.SetFloat(PropCols, columns);
        mpb.SetFloat(PropRows, rows);
        mpb.SetFloat(PropLedStartX, ledStartX);
        mpb.SetFloat(PropLedSizePx, ledSizePx);
        mpb.SetFloat(PropLedGapPx, ledGapPx);
        mpb.SetFloat(PropUseFakeGlow, useFakeGlow ? 1f : 0f);
        mpb.SetFloat(PropGlowStrength, glowStrength);
        mpb.SetFloat(PropGlowSpread, glowSpread);
        
        // Apply to renderer
        overlayRenderer.SetPropertyBlock(mpb);
    }
    
    // ============================================
    // PUBLIC API
    // ============================================
    
    /// <summary>
    /// Set flicker speed at runtime.
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Max(0.1f, newSpeed);
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Set LED on probability at runtime.
    /// </summary>
    public void SetDensity(float newDensity)
    {
        density = Mathf.Clamp01(newDensity);
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Set emission intensity at runtime.
    /// </summary>
    public void SetEmission(float newEmission)
    {
        emission = Mathf.Max(0f, newEmission);
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Set tint color at runtime.
    /// </summary>
    public void SetTint(Color newTint)
    {
        tint = newTint;
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Set multiple parameters at once (more efficient than individual calls).
    /// </summary>
    public void SetParameters(float newSpeed, float newDensity, float newEmission, Color newTint)
    {
        speed = Mathf.Max(0.1f, newSpeed);
        density = Mathf.Clamp01(newDensity);
        emission = Mathf.Max(0f, newEmission);
        tint = newTint;
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Override the seed value directly.
    /// </summary>
    public void SetSeed(float newSeed)
    {
        deterministicSeedFromPosition = false;
        computedSeed = newSeed;
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Re-compute seed from current position (useful after moving the object).
    /// </summary>
    public void RecomputeSeed()
    {
        deterministicSeedFromPosition = true;
        ComputeSeed();
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Enable or disable fake glow effect.
    /// </summary>
    public void SetFakeGlow(bool enabled, float strength = 0.3f, float spread = 1f)
    {
        useFakeGlow = enabled;
        glowStrength = Mathf.Clamp01(strength);
        glowSpread = Mathf.Clamp(spread, 0.5f, 3f);
        ApplyPropertyBlock();
    }
    
    /// <summary>
    /// Force re-initialization and property application.
    /// </summary>
    public void Reinitialize()
    {
        isInitialized = false;
        Initialize();
        ApplyPropertyBlock();
    }
    
    // ============================================
    // EDITOR SUPPORT
    // ============================================
    
    private void OnValidate()
    {
        // Clamp values
        speed = Mathf.Max(0.1f, speed);
        density = Mathf.Clamp01(density);
        emission = Mathf.Max(0f, emission);
        clipThreshold = Mathf.Clamp01(clipThreshold);
        texWidth = Mathf.Max(1, texWidth);
        texHeight = Mathf.Max(1, texHeight);
        columns = Mathf.Max(1, columns);
        rows = Mathf.Max(1, rows);
        ledStartX = Mathf.Max(0f, ledStartX);
        ledSizePx = Mathf.Max(1f, ledSizePx);
        ledGapPx = Mathf.Max(0f, ledGapPx);
        glowStrength = Mathf.Clamp01(glowStrength);
        glowSpread = Mathf.Clamp(glowSpread, 0.5f, 3f);
        
        // Recompute seed if position-based
        if (deterministicSeedFromPosition)
        {
            ComputeSeed();
        }
        else
        {
            computedSeed = seedOverride;
        }
        
        // Apply changes in editor
        if (overlayRenderer != null)
        {
            if (mpb == null)
            {
                mpb = new MaterialPropertyBlock();
            }
            ApplyPropertyBlock();
        }
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw indicator for seed value (color-coded)
        if (overlayRenderer != null)
        {
            Gizmos.color = tint;
            Gizmos.DrawWireCube(overlayRenderer.bounds.center, overlayRenderer.bounds.size * 1.05f);
        }
    }
#endif
}
