using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Diagnostic tool to check why sprite depth sorting isn't working.
/// Attach to SpriteChild GameObject and enable debug logs.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteDepthDiagnostic : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool checkEveryFrame = false;

    private SpriteRenderer spriteRenderer;
    private Material material;
    private Camera mainCamera;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;
    }

    private void Start()
    {
        Diagnose();
    }

    private void Update()
    {
        if (checkEveryFrame)
        {
            Diagnose();
        }
    }

    [ContextMenu("Diagnose Sprite Depth Issues")]
    public void Diagnose()
    {
        if (spriteRenderer == null)
        {
            Debug.LogError("[SpriteDepthDiagnostic] SpriteRenderer not found!");
            return;
        }

        material = spriteRenderer.sharedMaterial;
        if (material == null)
        {
            Debug.LogError("[SpriteDepthDiagnostic] No material assigned to SpriteRenderer!");
            return;
        }

        Debug.Log("=== SPRITE DEPTH DIAGNOSTIC ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Material: {material.name}");
        Debug.Log($"Shader: {material.shader.name}");
        
        // Check render queue
        int queue = material.renderQueue;
        Debug.Log($"Render Queue: {queue}");
        if (queue < 2500)
            Debug.LogWarning("Queue is in Opaque/AlphaTest range - GOOD for depth testing");
        else if (queue < 3000)
            Debug.LogWarning("Queue is in TransparentCutout range - may work");
        else
            Debug.LogError("Queue is in Transparent range - BAD for depth testing! Use AlphaTest queue (2450)");

        // Check shader keywords
        Debug.Log($"Shader Keywords: {string.Join(", ", material.shaderKeywords)}");

        // Check if ZWrite is enabled (via shader properties)
        if (material.HasProperty("_ZWrite"))
        {
            float zWrite = material.GetFloat("_ZWrite");
            Debug.Log($"ZWrite (from material): {zWrite}");
            if (zWrite > 0.5f)
                Debug.Log("ZWrite appears enabled");
            else
                Debug.LogError("ZWrite appears disabled!");
        }

        // Check sprite renderer settings
        Debug.Log($"SpriteRenderer.sortingOrder: {spriteRenderer.sortingOrder}");
        Debug.Log($"SpriteRenderer.sortingLayerName: {spriteRenderer.sortingLayerName}");
        Debug.Log($"SpriteRenderer.sortingLayerID: {spriteRenderer.sortingLayerID}");

        // Check camera settings
        if (mainCamera != null)
        {
            Debug.Log($"Camera: {mainCamera.name}");
            Debug.Log($"Camera Depth: {mainCamera.depth}");
            Debug.Log($"Camera Culling Mask: {mainCamera.cullingMask}");
            
            // Check if sprite layer is visible
            int spriteLayer = gameObject.layer;
            int layerMask = 1 << spriteLayer;
            bool visible = (mainCamera.cullingMask & layerMask) != 0;
            Debug.Log($"Sprite Layer ({LayerMask.LayerToName(spriteLayer)}): {(visible ? "Visible" : "NOT VISIBLE")}");
        }

        // Check transparency sort settings
        Debug.Log($"Graphics.transparencySortMode: {GraphicsSettings.transparencySortMode}");
        Debug.Log($"Graphics.transparencySortAxis: {GraphicsSettings.transparencySortAxis}");

        Debug.Log("=== END DIAGNOSTIC ===");
        Debug.Log("RECOMMENDATION: If queue is Transparent (>3000), sprite cannot depth-test against opaque objects.");
        Debug.Log("SOLUTION: Use MeshRenderer with flat quad + depth-writing material instead.");
    }
}
