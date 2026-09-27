using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Diagnostic tool to check why mesh depth occlusion isn't working.
/// Attach to Visual GameObject and enable debug logs.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class MeshDepthDiagnostic : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool checkEveryFrame = false;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Material material;
    private Camera mainCamera;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
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

    [ContextMenu("Diagnose Mesh Depth Issues")]
    public void Diagnose()
    {
        if (meshRenderer == null)
        {
            Debug.LogError("[MeshDepthDiagnostic] MeshRenderer not found!");
            return;
        }

        material = meshRenderer.sharedMaterial;
        if (material == null)
        {
            Debug.LogError("[MeshDepthDiagnostic] No material assigned to MeshRenderer!");
            return;
        }

        Debug.Log("=== MESH DEPTH DIAGNOSTIC ===");
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

        // Check mesh
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Mesh mesh = meshFilter.sharedMesh;
            Debug.Log($"Mesh: {mesh.name}");
            Debug.Log($"Mesh Bounds: {mesh.bounds}");
            Debug.Log($"Mesh Vertex Count: {mesh.vertexCount}");
            Debug.Log($"Mesh Triangle Count: {mesh.triangles.Length / 3}");
            
            // Check mesh orientation
            Bounds bounds = mesh.bounds;
            Debug.Log($"Mesh Center: {bounds.center}");
            Debug.Log($"Mesh Size: {bounds.size}");
        }
        else
        {
            Debug.LogError("No mesh assigned to MeshFilter!");
        }

        // Check transform
        Debug.Log($"Transform Position: {transform.position}");
        Debug.Log($"Transform Rotation: {transform.rotation.eulerAngles}");
        Debug.Log($"Transform Scale: {transform.lossyScale}");
        
        // Check world bounds
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds worldBounds = meshRenderer.bounds;
            Debug.Log($"World Bounds Center: {worldBounds.center}");
            Debug.Log($"World Bounds Size: {worldBounds.size}");
        }

        // Check camera settings
        if (mainCamera != null)
        {
            Debug.Log($"Camera: {mainCamera.name}");
            Debug.Log($"Camera Position: {mainCamera.transform.position}");
            Debug.Log($"Camera Rotation: {mainCamera.transform.rotation.eulerAngles}");
            Debug.Log($"Camera Depth: {mainCamera.depth}");
            Debug.Log($"Camera Near Plane: {mainCamera.nearClipPlane}");
            Debug.Log($"Camera Far Plane: {mainCamera.farClipPlane}");
            Debug.Log($"Camera Culling Mask: {mainCamera.cullingMask}");
            
            // Check if mesh layer is visible
            int meshLayer = gameObject.layer;
            int layerMask = 1 << meshLayer;
            bool visible = (mainCamera.cullingMask & layerMask) != 0;
            Debug.Log($"Mesh Layer ({LayerMask.LayerToName(meshLayer)}): {(visible ? "Visible" : "NOT VISIBLE")}");
            
            // Calculate distance from camera
            float distance = Vector3.Distance(transform.position, mainCamera.transform.position);
            Debug.Log($"Distance from Camera: {distance}");
            
            // Check if mesh is in camera frustum
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            bool inFrustum = GeometryUtility.TestPlanesAABB(planes, meshRenderer.bounds);
            Debug.Log($"In Camera Frustum: {inFrustum}");
        }

        // Check shader properties
        if (material.HasProperty("_ZWrite"))
        {
            float zWrite = material.GetFloat("_ZWrite");
            Debug.Log($"ZWrite (from material): {zWrite}");
            if (zWrite > 0.5f)
                Debug.Log("ZWrite appears enabled");
            else
                Debug.LogError("ZWrite appears disabled!");
        }

        // Check if material is using instanced material (might override shader settings)
        if (meshRenderer.material != meshRenderer.sharedMaterial)
        {
            Debug.LogWarning("MeshRenderer is using INSTANCED material - shader changes may not apply!");
            Debug.LogWarning("Use sharedMaterial or ensure material is not being modified at runtime.");
        }

        Debug.Log("=== END DIAGNOSTIC ===");
        Debug.Log("CHECKLIST:");
        Debug.Log("1. Material render queue should be ~2450 (AlphaTest)");
        Debug.Log("2. Shader should have ZWrite On");
        Debug.Log("3. Building materials should be opaque (queue ~2000)");
        Debug.Log("4. Camera should have proper depth settings");
        Debug.Log("5. Mesh should be facing correct direction");
    }
}

