using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Water Patch Grid System for Signal Jam
/// 
/// USAGE:
/// 1. Add this component to a GameObject in your scene
/// 2. Assign a shared Material reference (e.g., your Water material)
/// 3. Configure grid dimensions (widthTiles, heightTiles)
/// 4. Set tileWorldSize and pixelScale (default 5 for pixel art)
/// 5. Click "Regenerate Grid" in the Inspector or enable generateInEditMode
/// 
/// PERFORMANCE:
/// - All tiles share the same material reference (sharedMaterial) for optimal batching
/// - No per-frame updates or allocations
/// - Generation happens at edit-time or on-demand
/// </summary>
[ExecuteInEditMode]
public class WaterPatchGrid : MonoBehaviour
{
    [Header("Grid Configuration")]
    [SerializeField] private int widthTiles = 8;
    [SerializeField] private int heightTiles = 8;
    [SerializeField] private float tileWorldSize = 1f;
    [SerializeField] private float pixelScale = 5f;
    [Min(1)]
    [Tooltip("Number of quads per tile edge. Keep ocean grids at 1; use higher values for local vertex effects.")]
    [SerializeField] private int tileSubdivisions = 1;
    
    [Header("Material")]
    [SerializeField] private Material sharedMaterial;
    
    [Header("Generation Settings")]
    [SerializeField] private bool generateOnAwake = false;
    [SerializeField] private bool generateInEditMode = true;
    
    [Header("Orientation")]
    [SerializeField] private PlaneAxis planeAxis = PlaneAxis.XZ;
    [SerializeField] private Anchor anchor = Anchor.BottomLeft;
    [SerializeField] private bool twoSided = false;
    
    [Header("Offset")]
    [SerializeField] private Vector3 originOffset = Vector3.zero;
    
    [Header("Sprite Animation (Optional)")]
    [Tooltip("If true, uses sprite animation instead of shader-based scrolling. Less performant but allows frame-by-frame animation.")]
    [SerializeField] private bool useSpriteAnimation = false;
    
    [Tooltip("Sprite frames for water animation (8-12 frames recommended). Only used if useSpriteAnimation is true.")]
    [SerializeField] private SpriteAnimationSequence spriteAnimationSequence = new SpriteAnimationSequence();
    
    [Tooltip("Frames per second for sprite animation. Only used if useSpriteAnimation is true.")]
    [SerializeField] private float spriteAnimationSpeed = 8f;
    
    // Internal state for change detection
    private int lastWidthTiles = -1;
    private int lastHeightTiles = -1;
    private float lastTileWorldSize = -1f;
    private float lastPixelScale = -1f;
    private int lastTileSubdivisions = -1;
    private Vector3 lastOriginOffset = Vector3.zero;
    private PlaneAxis lastPlaneAxis = PlaneAxis.XZ;
    private Anchor lastAnchor = Anchor.BottomLeft;
    private bool lastTwoSided = false;
    private bool lastUseSpriteAnimation = false;
    
    // Sprite animation state
    private float spriteAnimationTimer = 0f;
    private int currentSpriteFrame = 0;
    private MaterialPropertyBlock propertyBlock;
    
    private const string TILES_ROOT_NAME = "TilesRoot";
    private const string PARTIAL_OCCLUDERS_LAYER = "PartialOccluders";

    private static readonly Dictionary<MeshCacheKey, Mesh> SharedMeshes = new Dictionary<MeshCacheKey, Mesh>();

    private readonly struct MeshCacheKey : IEquatable<MeshCacheKey>
    {
        private readonly int quantizedSize;
        private readonly int subdivisions;
        private readonly PlaneAxis axis;

        public MeshCacheKey(float size, int subdivisions, PlaneAxis axis)
        {
            quantizedSize = Mathf.RoundToInt(size * 10000f);
            this.subdivisions = subdivisions;
            this.axis = axis;
        }

        public bool Equals(MeshCacheKey other)
        {
            return quantizedSize == other.quantizedSize &&
                   subdivisions == other.subdivisions &&
                   axis == other.axis;
        }

        public override bool Equals(object obj) => obj is MeshCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = quantizedSize;
                hash = (hash * 397) ^ subdivisions;
                hash = (hash * 397) ^ (int)axis;
                return hash;
            }
        }
    }
    
    #if UNITY_EDITOR
    private bool isRegenerateQueued = false;
    #endif
    
    public enum PlaneAxis
    {
        XZ,  // Horizontal plane (default for top-down)
        XY   // Vertical plane
    }
    
    public enum Anchor
    {
        Center,     // Grid centered around parent transform
        BottomLeft  // Grid starts at parent transform position
    }
    
    private void Awake()
    {
        if (generateOnAwake && Application.isPlaying)
        {
            RegenerateGrid();
        }
        
        // Initialize property block for sprite animation
        if (useSpriteAnimation)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }
    
    private void Update()
    {
        // Only update sprite animation if enabled
        if (useSpriteAnimation && Application.isPlaying)
        {
            UpdateSpriteAnimation();
        }
    }
    
    private void OnValidate()
    {
        tileSubdivisions = Mathf.Max(1, tileSubdivisions);

        // Only auto-regenerate in edit mode if enabled
        if (generateInEditMode && !Application.isPlaying)
        {
            // Check if any relevant field changed
            if (HasGridParametersChanged())
            {
                // Delay regeneration slightly to avoid constant rebuilding while typing
                #if UNITY_EDITOR
                QueueDelayedRegenerate();
                #endif
            }
        }
    }
    
    private void OnDisable()
    {
        #if UNITY_EDITOR
        CancelDelayedRegenerate();
        #endif
    }
    
    private void OnDestroy()
    {
        #if UNITY_EDITOR
        CancelDelayedRegenerate();
        #endif
    }
    
    #if UNITY_EDITOR
    private void QueueDelayedRegenerate()
    {
        if (isRegenerateQueued)
        {
            return;
        }
        
        isRegenerateQueued = true;
        UnityEditor.EditorApplication.delayCall += HandleDelayedRegenerate;
    }
    
    private void HandleDelayedRegenerate()
    {
        UnityEditor.EditorApplication.delayCall -= HandleDelayedRegenerate;
        isRegenerateQueued = false;
        
        // Callback can outlive the component if it's deleted in edit mode.
        if (this == null)
        {
            return;
        }
        
        RegenerateGrid();
    }
    
    private void CancelDelayedRegenerate()
    {
        if (!isRegenerateQueued)
        {
            return;
        }
        
        UnityEditor.EditorApplication.delayCall -= HandleDelayedRegenerate;
        isRegenerateQueued = false;
    }
    #endif
    
    private bool HasGridParametersChanged()
    {
        bool changed = 
            widthTiles != lastWidthTiles ||
            heightTiles != lastHeightTiles ||
            Mathf.Abs(tileWorldSize - lastTileWorldSize) > 0.001f ||
            Mathf.Abs(pixelScale - lastPixelScale) > 0.001f ||
            tileSubdivisions != lastTileSubdivisions ||
            originOffset != lastOriginOffset ||
            planeAxis != lastPlaneAxis ||
            anchor != lastAnchor ||
            twoSided != lastTwoSided ||
            useSpriteAnimation != lastUseSpriteAnimation;
        
        if (changed)
        {
            lastWidthTiles = widthTiles;
            lastHeightTiles = heightTiles;
            lastTileWorldSize = tileWorldSize;
            lastPixelScale = pixelScale;
            lastTileSubdivisions = tileSubdivisions;
            lastOriginOffset = originOffset;
            lastPlaneAxis = planeAxis;
            lastAnchor = anchor;
            lastTwoSided = twoSided;
            lastUseSpriteAnimation = useSpriteAnimation;
        }
        
        return changed;
    }
    
    /// <summary>
    /// Regenerates the entire water grid. Cleans up old tiles first.
    /// </summary>
    [ContextMenu("Regenerate Grid")]
    public void RegenerateGrid()
    {
        if (this == null)
        {
            return;
        }
        
        // Clean up existing tiles
        ClearGrid();
        
        // Validate inputs
        if (widthTiles <= 0 || heightTiles <= 0)
        {
            Debug.LogWarning($"[WaterPatchGrid] Invalid grid dimensions: {widthTiles}x{heightTiles}. Grid not generated.", this);
            return;
        }
        
        if (sharedMaterial == null)
        {
            Debug.LogWarning("[WaterPatchGrid] No shared material assigned. Grid not generated.", this);
            return;
        }
        
        // Calculate final tile size
        float finalTileSize = tileWorldSize * pixelScale;
        
        // Create root container for tiles
        Transform tilesRoot = new GameObject(TILES_ROOT_NAME).transform;
        tilesRoot.SetParent(transform);
        tilesRoot.localPosition = Vector3.zero;
        tilesRoot.localRotation = Quaternion.identity;
        tilesRoot.localScale = Vector3.one;
        
        // Calculate grid bounds for anchor positioning
        float gridWidth = widthTiles * finalTileSize;
        float gridHeight = heightTiles * finalTileSize;
        
        Vector3 gridStartPos = Vector3.zero;
        if (anchor == Anchor.Center)
        {
            // Center the grid around origin
            if (planeAxis == PlaneAxis.XZ)
            {
                gridStartPos = new Vector3(-gridWidth * 0.5f, 0f, -gridHeight * 0.5f);
            }
            else // XY
            {
                gridStartPos = new Vector3(-gridWidth * 0.5f, -gridHeight * 0.5f, 0f);
            }
        }
        // BottomLeft anchor starts at (0,0) in local space
        
        // Apply origin offset
        gridStartPos += originOffset;
        
        // Generate tiles
        for (int row = 0; row < heightTiles; row++)
        {
            for (int col = 0; col < widthTiles; col++)
            {
                CreateTile(col, row, finalTileSize, gridStartPos, tilesRoot);
            }
        }
        
        // Update change tracking
        HasGridParametersChanged();
        
        Debug.Log($"[WaterPatchGrid] Generated {widthTiles * heightTiles} water tiles. Final tile size: {finalTileSize}", this);
    }
    
    private void CreateTile(int col, int row, float tileSize, Vector3 gridStartPos, Transform parent)
    {
        // Create tile GameObject
        GameObject tileObj = new GameObject($"Tile_x{col}_y{row}");
        tileObj.transform.SetParent(parent);
        
        // Calculate position based on plane axis
        Vector3 localPos = gridStartPos;
        if (planeAxis == PlaneAxis.XZ)
        {
            localPos += new Vector3(col * tileSize + tileSize * 0.5f, 0f, row * tileSize + tileSize * 0.5f);
        }
        else // XY
        {
            localPos += new Vector3(col * tileSize + tileSize * 0.5f, row * tileSize + tileSize * 0.5f, 0f);
        }
        
        tileObj.transform.localPosition = localPos;
        tileObj.transform.localRotation = GetTileRotation();
        tileObj.transform.localScale = Vector3.one;
        
        // Add MeshFilter and assign quad
        MeshFilter meshFilter = tileObj.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = GetOrCreateGridMesh(tileSize, tileSubdivisions, planeAxis);
        
        // Add MeshRenderer and assign shared material
        MeshRenderer renderer = tileObj.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = sharedMaterial;
        
        // Set layer to PartialOccluders
        int partialOccludersLayer = LayerMask.NameToLayer(PARTIAL_OCCLUDERS_LAYER);
        if (partialOccludersLayer >= 0)
        {
            tileObj.layer = partialOccludersLayer;
        }
        else
        {
            Debug.LogWarning($"[WaterPatchGrid] Layer '{PARTIAL_OCCLUDERS_LAYER}' not found. Using default layer.", this);
        }
        
        // Handle two-sided rendering
        if (twoSided)
        {
            // For two-sided, we could duplicate the mesh with reversed normals
            // Or use a shader that renders both sides. For now, we'll create a double-sided mesh.
            // Actually, Unity's default quad is single-sided. We'll need to handle this in the material/shader.
            // For simplicity, we'll just note it - the material should handle two-sided rendering.
        }
    }
    
    private Quaternion GetTileRotation()
    {
        if (planeAxis == PlaneAxis.XZ)
        {
            return Quaternion.identity; // Flat on XZ plane
        }
        else // XY
        {
            return Quaternion.Euler(0f, 0f, 0f); // Flat on XY plane (facing camera in 2D)
        }
    }
    
    private static Mesh GetOrCreateGridMesh(float size, int subdivisions, PlaneAxis axis)
    {
        subdivisions = Mathf.Max(1, subdivisions);
        var key = new MeshCacheKey(size, subdivisions, axis);
        if (SharedMeshes.TryGetValue(key, out Mesh cachedMesh) && cachedMesh != null)
            return cachedMesh;

        Mesh mesh = CreateGridMesh(size, subdivisions, axis);
        SharedMeshes[key] = mesh;
        return mesh;
    }

    private static Mesh CreateGridMesh(float size, int subdivisions, PlaneAxis axis)
    {
        Mesh mesh = new Mesh
        {
            name = $"WaterGrid_{axis}_{subdivisions}x{subdivisions}_{size:0.###}"
        };

        int verticesPerEdge = subdivisions + 1;
        int vertexCount = verticesPerEdge * verticesPerEdge;
        if (vertexCount > ushort.MaxValue)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        var vertices = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var uvs = new Vector2[vertexCount];
        var triangles = new int[subdivisions * subdivisions * 6];
        float halfSize = size * 0.5f;

        int vertexIndex = 0;
        for (int row = 0; row <= subdivisions; row++)
        {
            float v = row / (float)subdivisions;
            float vertical = Mathf.Lerp(-halfSize, halfSize, v);
            for (int col = 0; col <= subdivisions; col++)
            {
                float u = col / (float)subdivisions;
                float horizontal = Mathf.Lerp(-halfSize, halfSize, u);
                vertices[vertexIndex] = axis == PlaneAxis.XZ
                    ? new Vector3(horizontal, 0f, vertical)
                    : new Vector3(horizontal, vertical, 0f);
                normals[vertexIndex] = axis == PlaneAxis.XZ ? Vector3.up : Vector3.forward;
                uvs[vertexIndex] = new Vector2(u, v);
                vertexIndex++;
            }
        }

        int triangleIndex = 0;
        for (int row = 0; row < subdivisions; row++)
        {
            for (int col = 0; col < subdivisions; col++)
            {
                int bottomLeft = row * verticesPerEdge + col;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + verticesPerEdge;
                int topRight = topLeft + 1;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomRight;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        return mesh;
    }
    
    /// <summary>
    /// Clears all generated tiles from the grid.
    /// </summary>
    [ContextMenu("Clear Grid")]
    public void ClearGrid()
    {
        if (this == null)
        {
            return;
        }
        
        Transform selfTransform = transform;
        if (selfTransform == null)
        {
            return;
        }
        
        // Find and destroy the tiles root
        Transform tilesRoot = selfTransform.Find(TILES_ROOT_NAME);
        if (tilesRoot != null)
        {
            if (Application.isPlaying)
            {
                Destroy(tilesRoot.gameObject);
            }
            else
            {
                #if UNITY_EDITOR
                DestroyImmediate(tilesRoot.gameObject);
                #else
                DestroyImmediate(tilesRoot.gameObject);
                #endif
            }
        }
    }
    
    /// <summary>
    /// Gets the total number of tiles in the grid.
    /// </summary>
    public int GetTileCount()
    {
        if (this == null)
        {
            return 0;
        }
        
        Transform selfTransform = transform;
        if (selfTransform == null)
        {
            return 0;
        }
        
        Transform tilesRoot = selfTransform.Find(TILES_ROOT_NAME);
        if (tilesRoot == null) return 0;
        return tilesRoot.childCount;
    }
    
    /// <summary>
    /// Gets the final tile size (tileWorldSize * pixelScale).
    /// </summary>
    public float GetFinalTileSize()
    {
        return tileWorldSize * pixelScale;
    }

    public int GetTileSubdivisions()
    {
        return Mathf.Max(1, tileSubdivisions);
    }
    
    /// <summary>
    /// Updates sprite animation for all tiles. Only called if useSpriteAnimation is true.
    /// </summary>
    private void UpdateSpriteAnimation()
    {
        if (this == null)
            return;
        
        if (spriteAnimationSequence == null || spriteAnimationSequence.Length == 0)
            return;
        
        // Update animation timer
        int sequenceLength = spriteAnimationSequence.Length;
        if (sequenceLength > 1)
        {
            spriteAnimationTimer += Time.deltaTime * spriteAnimationSpeed;
            currentSpriteFrame = Mathf.FloorToInt(spriteAnimationTimer) % sequenceLength;
        }
        else
        {
            currentSpriteFrame = 0;
        }
        
        // Update all tile renderers with current sprite frame
        Transform selfTransform = transform;
        if (selfTransform == null)
            return;
        
        Transform tilesRoot = selfTransform.Find(TILES_ROOT_NAME);
        if (tilesRoot == null) return;
        
        Sprite currentSprite = spriteAnimationSequence[currentSpriteFrame];
        if (currentSprite == null || currentSprite.texture == null) return;
        
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
        
        // Update all tiles with the same sprite frame (keeps batching)
        foreach (Transform tileTransform in tilesRoot)
        {
            MeshRenderer renderer = tileTransform.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.GetPropertyBlock(propertyBlock);
                
                // Set texture
                propertyBlock.SetTexture("_MainTex", currentSprite.texture);
                
                // Set UV offset/scale for sprite atlas support
                Rect spriteRect = currentSprite.textureRect;
                Vector2 textureSize = new Vector2(currentSprite.texture.width, currentSprite.texture.height);
                
                Vector2 offset = new Vector2(spriteRect.x / textureSize.x, spriteRect.y / textureSize.y);
                Vector2 scale = new Vector2(spriteRect.width / textureSize.x, spriteRect.height / textureSize.y);
                
                propertyBlock.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
                
                // Preserve material properties
                if (sharedMaterial != null)
                {
                    if (sharedMaterial.HasProperty("_Color"))
                    {
                        propertyBlock.SetColor("_Color", sharedMaterial.GetColor("_Color"));
                    }
                }
                
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}

