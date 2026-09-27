using UnityEngine;

/// <summary>
/// Reusable quad mesh that displays a Sprite or Texture2D.
/// Use for world-space images that need MeshRenderer (e.g. interactable indicators,
/// billboards, HUD elements). Supports sprite atlas via texture rect.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[AddComponentMenu("Rendering/Mesh Quad Image")]
public class MeshQuadImage : MonoBehaviour
{
    [Header("Source")]
    [Tooltip("Sprite to display. Supports atlas rect. If set, overrides texture.")]
    [SerializeField] private Sprite sprite;

    [Tooltip("Texture to display when sprite is null. Uses full UVs.")]
    [SerializeField] private Texture2D texture;

    [Header("Size")]
    [Tooltip("Quad size in local units (before parent scale)")]
    [SerializeField] private Vector2 size = new Vector2(1f, 1f);

    [Header("Material")]
    [Tooltip("Material for rendering. Uses Sprites/Default if null.")]
    [SerializeField] private Material material;

    [Header("Color")]
    [Tooltip("Tint color (alpha used for fade)")]
    [SerializeField] private Color color = Color.white;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh quadMesh;
    private MaterialPropertyBlock propertyBlock;
    private Material instanceMaterial;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        CreateQuadMesh();

        if (material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Unlit/Transparent");
            material = shader != null ? new Material(shader) : meshRenderer.sharedMaterial;
        }

        instanceMaterial = new Material(material);
        meshRenderer.material = instanceMaterial;
        meshRenderer.enabled = true;

        ApplyTexture();
        ApplyColor();
    }

    private void OnDestroy()
    {
        if (quadMesh != null)
            Destroy(quadMesh);
        if (instanceMaterial != null)
            Destroy(instanceMaterial);
    }

    private void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        quadMesh.name = "MeshQuadImage";

        float w = size.x * 0.5f;
        float h = size.y * 0.5f;

        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-w, -h, 0f),
            new Vector3(w, -h, 0f),
            new Vector3(-w, h, 0f),
            new Vector3(w, h, 0f)
        };

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        quadMesh.vertices = vertices;
        quadMesh.uv = uvs;
        quadMesh.triangles = triangles;
        quadMesh.RecalculateNormals();
        quadMesh.RecalculateBounds();

        meshFilter.mesh = quadMesh;
    }

    private void ApplyTexture()
    {
        if (meshRenderer == null || propertyBlock == null) return;

        meshRenderer.GetPropertyBlock(propertyBlock);

        if (sprite != null && sprite.texture != null)
        {
            propertyBlock.SetTexture("_MainTex", sprite.texture);

            Rect rect = sprite.textureRect;
            Vector2 texSize = new Vector2(sprite.texture.width, sprite.texture.height);
            Vector2 offset = new Vector2(rect.x / texSize.x, rect.y / texSize.y);
            Vector2 scale = new Vector2(rect.width / texSize.x, rect.height / texSize.y);
            propertyBlock.SetVector("_MainTex_ST", new Vector4(scale.x, scale.y, offset.x, offset.y));
        }
        else if (texture != null)
        {
            propertyBlock.SetTexture("_MainTex", texture);
            propertyBlock.SetVector("_MainTex_ST", new Vector4(1f, 1f, 0f, 0f));
        }

        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyColor()
    {
        if (meshRenderer == null || propertyBlock == null) return;

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_Color", color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    /// <summary>Set the sprite to display. Supports atlas rect.</summary>
    public void SetSprite(Sprite newSprite)
    {
        sprite = newSprite;
        texture = null;
        ApplyTexture();
    }

    /// <summary>Set the texture to display. Uses full UVs.</summary>
    public void SetTexture(Texture2D newTexture)
    {
        texture = newTexture;
        sprite = null;
        ApplyTexture();
    }

    /// <summary>Set the quad size (recreates mesh).</summary>
    public void SetSize(Vector2 newSize)
    {
        size = newSize;
        CreateQuadMesh();
    }

    /// <summary>Set color (including alpha for fade).</summary>
    public void SetColor(Color newColor)
    {
        color = newColor;
        ApplyColor();
    }

    /// <summary>Set alpha for fade (0–1).</summary>
    public void SetAlpha(float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        ApplyColor();
    }

    /// <summary>Refresh mesh and texture after inspector changes.</summary>
    public void Refresh()
    {
        CreateQuadMesh();
        ApplyTexture();
        ApplyColor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Awake owns runtime mesh construction. Assigning MeshFilter.mesh from
        // OnValidate during scene activation invokes Unity callbacks from a
        // forbidden validation phase and can also run before the runtime cache
        // is initialized.
        size.x = Mathf.Max(0.001f, size.x);
        size.y = Mathf.Max(0.001f, size.y);
    }
#endif
}
