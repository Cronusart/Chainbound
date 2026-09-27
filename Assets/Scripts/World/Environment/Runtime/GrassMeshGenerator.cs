using UnityEngine;

/// <summary>
/// Utility class for generating grass patch meshes (single quad or cross-quad).
/// Used by GrassPatchController and editor tools.
/// </summary>
public static class GrassMeshGenerator
{
    /// <summary>
    /// Create a single quad mesh for grass.
    /// </summary>
    /// <param name="size">Size of the quad (width, height)</param>
    /// <param name="pivot">Pivot point (0 = bottom, 0.5 = center, 1 = top)</param>
    /// <param name="name">Name for the mesh</param>
    /// <returns>Generated mesh</returns>
    public static Mesh CreateQuadMesh(Vector2 size, float pivot = 0f, string name = "GrassQuad")
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        
        float halfWidth = size.x * 0.5f;
        float height = size.y;
        float pivotOffset = -height * pivot; // Negative because we want pivot at bottom by default
        
        // Vertices: bottom-left, bottom-right, top-left, top-right
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-halfWidth, pivotOffset, 0f),           // Bottom-left
            new Vector3(halfWidth, pivotOffset, 0f),              // Bottom-right
            new Vector3(-halfWidth, height + pivotOffset, 0f),    // Top-left
            new Vector3(halfWidth, height + pivotOffset, 0f)     // Top-right
        };
        
        // UVs: standard 0-1 mapping
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0f, 0f),  // Bottom-left
            new Vector2(1f, 0f),   // Bottom-right
            new Vector2(0f, 1f),  // Top-left
            new Vector2(1f, 1f)   // Top-right
        };
        
        // Triangles: two triangles forming a quad
        int[] triangles = new int[6]
        {
            0, 2, 1,  // First triangle: bottom-left, top-left, bottom-right
            2, 3, 1   // Second triangle: top-left, top-right, bottom-right
        };
        
        // Normals: face forward (Z+)
        Vector3[] normals = new Vector3[4]
        {
            Vector3.forward,
            Vector3.forward,
            Vector3.forward,
            Vector3.forward
        };
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Create a cross-quad mesh (two quads rotated 45° relative to each other, forming an X shape).
    /// This provides better visual coverage from all angles.
    /// </summary>
    /// <param name="size">Size of each quad (width, height)</param>
    /// <param name="pivot">Pivot point (0 = bottom, 0.5 = center, 1 = top)</param>
    /// <param name="name">Name for the mesh</param>
    /// <returns>Generated mesh</returns>
    public static Mesh CreateCrossQuadMesh(Vector2 size, float pivot = 0f, string name = "GrassCrossQuad")
    {
        Mesh mesh = new Mesh();
        mesh.name = name;
        
        float halfWidth = size.x * 0.5f;
        float height = size.y;
        float pivotOffset = -height * pivot;
        
        // First quad: standard orientation (facing forward)
        // Second quad: rotated 45° around Y axis
        
        // Vertices for both quads (8 total)
        Vector3[] vertices = new Vector3[8];
        Vector2[] uvs = new Vector2[8];
        int[] triangles = new int[12];
        
        // First quad (standard orientation)
        vertices[0] = new Vector3(-halfWidth, pivotOffset, 0f);
        vertices[1] = new Vector3(halfWidth, pivotOffset, 0f);
        vertices[2] = new Vector3(-halfWidth, height + pivotOffset, 0f);
        vertices[3] = new Vector3(halfWidth, height + pivotOffset, 0f);
        
        uvs[0] = new Vector2(0f, 0f);
        uvs[1] = new Vector2(1f, 0f);
        uvs[2] = new Vector2(0f, 1f);
        uvs[3] = new Vector2(1f, 1f);
        
        // First quad triangles
        triangles[0] = 0;
        triangles[1] = 2;
        triangles[2] = 1;
        triangles[3] = 2;
        triangles[4] = 3;
        triangles[5] = 1;
        
        // Second quad (rotated 45° around Y axis)
        // Rotate vertices by 45° around Y axis
        Quaternion rotation = Quaternion.Euler(0f, 45f, 0f);
        
        vertices[4] = rotation * new Vector3(-halfWidth, pivotOffset, 0f);
        vertices[5] = rotation * new Vector3(halfWidth, pivotOffset, 0f);
        vertices[6] = rotation * new Vector3(-halfWidth, height + pivotOffset, 0f);
        vertices[7] = rotation * new Vector3(halfWidth, height + pivotOffset, 0f);
        
        uvs[4] = new Vector2(0f, 0f);
        uvs[5] = new Vector2(1f, 0f);
        uvs[6] = new Vector2(0f, 1f);
        uvs[7] = new Vector2(1f, 1f);
        
        // Second quad triangles
        triangles[6] = 4;
        triangles[7] = 6;
        triangles[8] = 5;
        triangles[9] = 6;
        triangles[10] = 7;
        triangles[11] = 5;
        
        // Normals: face forward for first quad, rotated for second quad
        Vector3[] normals = new Vector3[8];
        for (int i = 0; i < 4; i++)
        {
            normals[i] = Vector3.forward;
        }
        for (int i = 4; i < 8; i++)
        {
            normals[i] = rotation * Vector3.forward;
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Create a quad mesh with default size (1x1 unit).
    /// </summary>
    public static Mesh CreateQuadMesh(string name = "GrassQuad")
    {
        return CreateQuadMesh(Vector2.one, 0f, name);
    }
    
    /// <summary>
    /// Create a cross-quad mesh with default size (1x1 unit).
    /// </summary>
    public static Mesh CreateCrossQuadMesh(string name = "GrassCrossQuad")
    {
        return CreateCrossQuadMesh(Vector2.one, 0f, name);
    }
}

