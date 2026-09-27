using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages fade state for a single renderer using MaterialPropertyBlock to avoid material instancing.
/// Supports MeshRenderer, SkinnedMeshRenderer, and SpriteRenderer.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class FadeableRenderer : MonoBehaviour
{
    private Renderer cachedRenderer;
    private MaterialPropertyBlock propertyBlock;
    private float currentAlpha = 1f;
    private bool isInitialized = false;
    
    // Cache original material properties
    private Material originalMaterial;
    private bool originalMaterialSupportsAlpha;
    private Material fadeCapableMaterial; // Fallback material if original doesn't support alpha
    private bool usingInstancedMaterial = false; // Track if we're using an instanced material
    
    [Header("Fade Settings")]
    [SerializeField] private Material fadeCapableMaterialOverride; // Optional: assign a fade-capable material variant
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (isInitialized) return;

        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null)
        {
            Debug.LogError($"[FadeableRenderer] No Renderer component found on {gameObject.name}");
            return;
        }

        propertyBlock = new MaterialPropertyBlock();
        originalMaterial = cachedRenderer.sharedMaterial;
        
        // Check if material supports alpha/_Color property
        originalMaterialSupportsAlpha = originalMaterial != null && 
            (originalMaterial.HasProperty("_Color") || originalMaterial.HasProperty("_BaseColor") || 
             originalMaterial.HasProperty("_TintColor"));

        // Use override material if provided, otherwise try to find a fade-capable variant
        if (fadeCapableMaterialOverride != null)
        {
            fadeCapableMaterial = fadeCapableMaterialOverride;
        }
        else if (originalMaterial != null && !originalMaterialSupportsAlpha)
        {
            // Try to find a fade-capable variant by appending "_Fade" to material name
            Material fadeVariant = Resources.Load<Material>($"Materials/{originalMaterial.name}_Fade");
            if (fadeVariant != null)
            {
                fadeCapableMaterial = fadeVariant;
            }
        }

        isInitialized = true;
    }

    /// <summary>
    /// Set the alpha/transparency of this renderer.
    /// Uses MaterialPropertyBlock to avoid material instancing.
    /// </summary>
    /// <param name="alpha">Target alpha (0 = transparent, 1 = opaque)</param>
    public void SetAlpha(float alpha)
    {
        if (!isInitialized)
            Initialize();

        if (cachedRenderer == null)
            return;

        currentAlpha = Mathf.Clamp01(alpha);

        // Use MaterialPropertyBlock to modify alpha without instancing material
        cachedRenderer.GetPropertyBlock(propertyBlock);
        
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        // Try different color property names (varies by shader)
        if (originalMaterialSupportsAlpha && originalMaterial != null)
        {
            Color currentColor = originalMaterial.GetColor("_Color");
            if (currentColor.a == 0f) // Try _BaseColor (URP) or _TintColor
            {
                if (originalMaterial.HasProperty("_BaseColor"))
                    currentColor = originalMaterial.GetColor("_BaseColor");
                else if (originalMaterial.HasProperty("_TintColor"))
                    currentColor = originalMaterial.GetColor("_TintColor");
                else
                    currentColor = Color.white;
            }

            currentColor.a = currentAlpha;
            
            // Set color property (try common shader property names)
            if (originalMaterial.HasProperty("_Color"))
                propertyBlock.SetColor("_Color", currentColor);
            else if (originalMaterial.HasProperty("_BaseColor"))
                propertyBlock.SetColor("_BaseColor", currentColor);
            else if (originalMaterial.HasProperty("_TintColor"))
                propertyBlock.SetColor("_TintColor", currentColor);
        }
        else if (fadeCapableMaterial != null)
        {
            // Only instance material as last resort (when original doesn't support alpha)
            if (!usingInstancedMaterial)
            {
                cachedRenderer.material = fadeCapableMaterial; // Creates instance, doesn't affect sharedMaterial
                usingInstancedMaterial = true;
            }
            
            // Apply alpha via property block on instanced material
            Color fadeColor = fadeCapableMaterial.GetColor("_Color");
            fadeColor.a = currentAlpha;
            propertyBlock.SetColor("_Color", fadeColor);
        }
        else
        {
            // Fallback: try to set alpha anyway (may not work)
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[FadeableRenderer] Material on {gameObject.name} doesn't support alpha fading. Consider assigning a fade-capable material.");
            }
        }

        cachedRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>
    /// Get current alpha value.
    /// </summary>
    public float GetAlpha()
    {
        return currentAlpha;
    }

    /// <summary>
    /// Reset alpha to 1.0 (fully opaque).
    /// </summary>
    public void ResetAlpha()
    {
        SetAlpha(1f);
        
        // Restore original material if we switched to instanced fade-capable variant
        if (usingInstancedMaterial && cachedRenderer != null && originalMaterial != null)
        {
            cachedRenderer.material = originalMaterial; // Restore original material instance
            usingInstancedMaterial = false;
        }
    }

    /// <summary>
    /// Check if this renderer supports alpha fading.
    /// </summary>
    public bool SupportsAlpha()
    {
        if (!isInitialized)
            Initialize();
        
        return originalMaterialSupportsAlpha || fadeCapableMaterial != null;
    }
}

