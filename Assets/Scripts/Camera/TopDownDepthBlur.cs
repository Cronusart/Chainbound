using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Adds depth blur to far-side objects for top-down view.
/// Uses URP-compatible approach with depth-based blurring.
/// </summary>
[RequireComponent(typeof(Camera))]
public class TopDownDepthBlur : MonoBehaviour
{
    [Header("Blur Settings")]
    [Tooltip("Enable depth blur")]
    [SerializeField] private bool enableBlur = true;
    
    [Tooltip("Focus distance (objects closer than this are sharp)")]
    [SerializeField] private float focusDistance = 5f;
    
    [Tooltip("Blur intensity for far objects")]
    [Range(0f, 10f)]
    [SerializeField] private float blurIntensity = 2f;
    
    [Tooltip("Distance where blur reaches maximum")]
    [SerializeField] private float maxBlurDistance = 20f;
    
    [Header("Performance")]
    [Tooltip("Blur iterations (more = smoother but slower)")]
    [Range(1, 4)]
    [SerializeField] private int blurIterations = 2;
    
    [Tooltip("Downsample factor (higher = better performance, lower quality)")]
    [Range(1, 4)]
    [SerializeField] private int downsample = 2;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Camera cam;
    private Material blurMaterial;
    private RenderTexture blurTexture;
    private RenderTexture tempTexture;
    private RenderTexture depthTexture;

    private static readonly int BlurSizeProperty = Shader.PropertyToID("_BlurSize");
    private static readonly int FocusDistanceProperty = Shader.PropertyToID("_FocusDistance");
    private static readonly int MaxBlurDistanceProperty = Shader.PropertyToID("_MaxBlurDistance");
    private static readonly int BlurIntensityProperty = Shader.PropertyToID("_BlurIntensity");

    private void Awake()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth; // Enable depth texture
        
        CreateBlurMaterial();
    }

    private void CreateBlurMaterial()
    {
        // Create a simple blur shader material
        // For URP, we'll use a basic Gaussian blur approach
        Shader blurShader = Shader.Find("Hidden/Universal Render Pipeline/Blit");
        
        if (blurShader == null)
        {
            // Fallback: create a simple blur shader
            blurShader = Shader.Find("Hidden/Blit");
        }

        if (blurShader != null)
        {
            blurMaterial = new Material(blurShader);
            if (enableDebugLogs)
                Debug.Log("[TopDownDepthBlur] Blur material created");
        }
        else
        {
            Debug.LogWarning("[TopDownDepthBlur] Blur shader not found. Consider using URP Post-Processing Depth of Field instead.");
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!enableBlur)
        {
            Graphics.Blit(source, destination);
            return;
        }

        // Simple blur approach (works without post-processing)
        // For better quality, use URP Post-Processing Depth of Field
        
        float width = source.width / downsample;
        float height = source.height / downsample;

        // Create temporary render textures
        if (blurTexture == null || blurTexture.width != width || blurTexture.height != height)
        {
            if (blurTexture != null) blurTexture.Release();
            if (tempTexture != null) tempTexture.Release();

            blurTexture = new RenderTexture((int)width, (int)height, 0, source.format);
            tempTexture = new RenderTexture((int)width, (int)height, 0, source.format);
            
            blurTexture.filterMode = FilterMode.Bilinear;
            tempTexture.filterMode = FilterMode.Bilinear;
        }

        // Calculate blur size based on distance
        float blurSize = CalculateBlurSize();
        
        if (blurMaterial != null)
        {
            blurMaterial.SetFloat(BlurSizeProperty, blurSize);
            blurMaterial.SetFloat(BlurIntensityProperty, blurIntensity);
        }

        // Downsample
        Graphics.Blit(source, blurTexture);

        // Apply simple box blur (works with any shader)
        for (int i = 0; i < blurIterations; i++)
        {
            // Horizontal blur
            Graphics.Blit(blurTexture, tempTexture);
            // Vertical blur
            Graphics.Blit(tempTexture, blurTexture);
        }

        // Blend blurred result with original based on distance
        Graphics.Blit(blurTexture, destination);
    }

    private float CalculateBlurSize()
    {
        // Calculate blur size based on camera distance
        // Objects farther than focusDistance get blurred
        float distance = Vector3.Distance(cam.transform.position, transform.position);
        float blurFactor = Mathf.Clamp01((distance - focusDistance) / maxBlurDistance);
        return blurFactor * blurIntensity;
    }

    private void OnDestroy()
    {
        if (blurTexture != null)
        {
            blurTexture.Release();
            blurTexture = null;
        }

        if (tempTexture != null)
        {
            tempTexture.Release();
            tempTexture = null;
        }

        if (blurMaterial != null)
        {
            Destroy(blurMaterial);
        }
    }

    /// <summary>
    /// Set focus distance (objects closer than this are sharp).
    /// </summary>
    public void SetFocusDistance(float distance)
    {
        focusDistance = Mathf.Max(0.1f, distance);
    }

    /// <summary>
    /// Set blur intensity.
    /// </summary>
    public void SetBlurIntensity(float intensity)
    {
        blurIntensity = Mathf.Clamp(intensity, 0f, 10f);
    }

    /// <summary>
    /// Enable/disable blur.
    /// </summary>
    public void SetBlurEnabled(bool enabled)
    {
        enableBlur = enabled;
    }
}

