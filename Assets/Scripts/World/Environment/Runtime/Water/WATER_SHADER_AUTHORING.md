# Water Shader Authoring Guide

## Overview

This guide explains how to author shoreline masks for the WaterPixelArt shader. The shader supports three methods for defining shallow-to-deep water gradients:

1. **Vertex Colors** (Recommended) - Paint directly on mesh vertices
2. **Texture Mask** - Use a grayscale texture
3. **Procedural** - Generate from world position (advanced)

---

## Method 1: Vertex Color Painting (Recommended)

### Why Vertex Colors?

- **Precise Control**: Paint exactly where shallow/deep water should be
- **No UV Mapping Issues**: Works regardless of UV layout
- **Performance**: No extra texture sample needed
- **Artistic Flexibility**: Easy to iterate and adjust

### Setup in Unity

#### Option A: Using ProBuilder (Recommended)

1. **Install ProBuilder** (if not already installed):
   - Window → Package Manager
   - Search "ProBuilder"
   - Install

2. **Open ProBuilder**:
   - Tools → ProBuilder → ProBuilder Window

3. **Select Water Mesh**:
   - Select your water plane/quad GameObject

4. **Enter Vertex Editing Mode**:
   - In ProBuilder window, click **Vertex** mode
   - Or press `V` key

5. **Paint Vertex Colors**:
   - Select vertices near the shore (shallow water)
   - In ProBuilder, use **Vertex Colors** tool
   - Set color to **Red = 1.0** (white/red) for shallow areas
   - Set color to **Red = 0.0** (black) for deep areas
   - Use gradient painting for smooth transitions

6. **Apply Colors**:
   - Click **Apply** in ProBuilder
   - Colors are saved to mesh

#### Option B: Using Blender

1. **Export Mesh**:
   - Export water mesh from Unity (or create in Blender)

2. **Import to Blender**:
   - File → Import → FBX/OBJ

3. **Enter Vertex Paint Mode**:
   - Select mesh
   - Switch to **Vertex Paint** mode (top menu)

4. **Paint Vertex Colors**:
   - Use **Draw** brush
   - Set color to **Red = 1.0** for shallow (near shore)
   - Set color to **Red = 0.0** for deep (center/far from shore)
   - Use **Blur** brush for smooth gradients

5. **Export**:
   - File → Export → FBX
   - Ensure **Vertex Colors** is enabled in export settings

6. **Reimport in Unity**:
   - Replace mesh in Unity
   - Ensure **Read/Write Enabled** is checked in mesh import settings

#### Option C: Using Script (Runtime/Editor)

Create an editor script to paint vertex colors programmatically:

```csharp
using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(MeshFilter))]
public class WaterShorelinePainter : MonoBehaviour
{
    [ContextMenu("Paint Shoreline Gradient")]
    void PaintShorelineGradient()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        Mesh mesh = mf.sharedMesh;
        
        Vector3[] vertices = mesh.vertices;
        Color[] colors = new Color[vertices.Length];
        
        // Example: Paint based on distance from center
        Vector3 center = Vector3.zero;
        float maxDistance = 5f; // Adjust based on mesh size
        
        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = Vector3.Distance(vertices[i], center);
            float shallowMask = 1f - Mathf.Clamp01(distance / maxDistance);
            
            // Red channel = shallow mask (0 = deep, 1 = shallow)
            colors[i] = new Color(shallowMask, 0f, 0f, 1f);
        }
        
        mesh.colors = colors;
        
        // Save mesh
        #if UNITY_EDITOR
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssets();
        #endif
    }
}
```

### Vertex Color Channel Mapping

- **Red Channel**: Shoreline mask (0 = deep, 1 = shallow)
- **Green Channel**: Reserved for future use
- **Blue Channel**: Reserved for future use
- **Alpha Channel**: Not used (can be 1.0)

### Tips for Vertex Color Painting

1. **Start with Gradient**:
   - Paint a smooth gradient from shore (red=1) to center (red=0)
   - Use blur/smooth tools to avoid harsh transitions

2. **Test in Play Mode**:
   - Adjust colors and test in real-time
   - Use material's `_DepthBlendPower` to sharpen/soften gradient

3. **Multiple Shorelines**:
   - If water has multiple shores (island in center), paint accordingly
   - Red=1 at all shore edges, Red=0 in deepest areas

4. **Performance**:
   - Vertex colors are stored per-vertex, so dense meshes use more memory
   - For large water areas, consider texture mask instead

---

## Method 2: Texture Mask

### When to Use Texture Mask

- **Large Water Areas**: More memory-efficient than dense vertex colors
- **Complex Patterns**: Easier to paint detailed patterns in image editor
- **Reusable**: Same mask can be used on multiple meshes
- **UV-Based**: Requires proper UV mapping

### Creating the Mask Texture

1. **Create Texture**:
   - Size: Match your mesh UV layout (typically 512x512 or 1024x1024)
   - Format: Grayscale (R8) or RGBA
   - Import as: **Default** texture type

2. **Paint Mask**:
   - **White (1.0)**: Shallow water (near shore)
   - **Black (0.0)**: Deep water (center/far from shore)
   - **Gray**: Transition zones

3. **Tips**:
   - Use soft brushes for smooth gradients
   - Ensure seamless tiling if water repeats
   - Test in Unity to verify UV mapping

### UV Mapping Considerations

- **Planar Mapping**: Standard for flat water planes
- **World-Space UV**: Can use world position for procedural mapping
- **Custom UV**: Paint UVs to match shoreline layout

### Setup in Shader

1. **Assign Texture**:
   - In material, assign mask texture to `_ShoreMaskTex`

2. **Disable Vertex Colors**:
   - Set `_UseVertexColorMask` to `False` (0)

3. **Adjust Sampling**:
   - Ensure UV coordinates match your mesh layout
   - Use **Tiling And Offset** node if needed

---

## Method 3: Procedural Shoreline (Advanced)

### World Position-Based Gradient

Create a custom function or use Shader Graph nodes to generate shoreline mask from world position:

1. **Get World Position**:
   - Use **Position** node (World Space)

2. **Calculate Distance**:
   - Calculate distance from shore points
   - Use **Distance** node or custom calculation

3. **Generate Mask**:
   - Use **Smoothstep** or **Remap** to create gradient
   - Output: 0 (deep) to 1 (shallow)

### Example: Radial Gradient from Center

```hlsl
// In Shader Graph or Custom Function
float2 worldPosXZ = worldPosition.xz;
float2 center = float2(0, 0); // Adjust to water center
float distance = length(worldPosXZ - center);
float maxDistance = 10.0; // Adjust to water radius
float shorelineMask = 1.0 - saturate(distance / maxDistance);
```

### Example: Distance from Multiple Points

For complex shorelines (islands, multiple edges):

```hlsl
// Calculate distance to nearest shore point
float minDistance = 999.0;
for (int i = 0; i < shorePointCount; i++)
{
    float dist = distance(worldPosXZ, shorePoints[i]);
    minDistance = min(minDistance, dist);
}
float shorelineMask = 1.0 - saturate(minDistance / maxShoreDistance);
```

---

## Testing and Iteration

### Visual Debugging

1. **Temporary Debug Material**:
   - Create material that outputs shoreline mask as color
   - Red = shallow, Black = deep
   - Verify mask looks correct

2. **Adjust Blend Power**:
   - Increase `_DepthBlendPower` for sharper transitions
   - Decrease for softer gradients

3. **Test at Different Angles**:
   - View water from different camera angles
   - Ensure gradient looks good from all perspectives

### Common Issues

1. **Harsh Transitions**:
   - **Solution**: Smooth vertex colors or use blur in texture mask
   - Adjust `_DepthBlendPower` to 1.0-2.0 for softer look

2. **Inverted Gradient**:
   - **Solution**: Invert mask (1.0 - mask) or swap shallow/deep colors

3. **UV Mismatch** (Texture Mask):
   - **Solution**: Check UV mapping, adjust tiling/offset
   - Consider using vertex colors instead

4. **Performance** (Dense Vertex Colors):
   - **Solution**: Reduce vertex count or use texture mask
   - Use LOD meshes for distant water

---

## Best Practices

1. **Start Simple**:
   - Begin with basic gradient (shore to center)
   - Add complexity (multiple shores, islands) later

2. **Iterate Quickly**:
   - Use vertex colors for fast iteration
   - Convert to texture mask once finalized

3. **Match Art Style**:
   - Pixel-art games: Sharp transitions (high `_DepthBlendPower`)
   - Stylized games: Soft gradients (low `_DepthBlendPower`)

4. **Consider Performance**:
   - Vertex colors: Good for small-medium water areas
   - Texture mask: Better for large areas or multiple instances
   - Procedural: Good for dynamic/scripted water

5. **Document Your Setup**:
   - Note which method you used
   - Save vertex color meshes or mask textures
   - Document any custom UV layouts

---

## Workflow Summary

### Quick Start (Vertex Colors)

1. Select water mesh
2. Enter vertex paint mode (ProBuilder/Blender)
3. Paint red channel: 1.0 at shore, 0.0 at center
4. Smooth/blur for gradients
5. Test in Unity
6. Adjust `_DepthBlendPower` for sharpness

### Production (Texture Mask)

1. Create grayscale texture (512x512 or larger)
2. Paint white at shore, black at center
3. Ensure proper UV mapping
4. Assign to `_ShoreMaskTex`
5. Set `_UseVertexColorMask` to False
6. Test and iterate

---

## Next Steps

- See `WATER_SHADER_SETUP.md` for shader setup
- See `WATER_SHADER_TROUBLESHOOTING.md` for common issues

