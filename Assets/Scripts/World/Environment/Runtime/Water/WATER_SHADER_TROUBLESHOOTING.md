# Water Shader Troubleshooting Guide

## Common Issues and Solutions

This guide covers common problems when using the WaterPixelArt shader and how to fix them.

---

## Sorting and Transparency Issues

### Problem: Water Renders Behind/In Front of Other Objects Incorrectly

**Symptoms:**
- Water appears behind objects that should be in front
- Objects disappear when behind water
- Z-fighting or flickering

**Solutions:**

1. **Check Render Queue**:
   - Ensure shader uses **Transparent** queue (3000)
   - In Shader Graph: Graph Settings → Render Queue → Transparent

2. **Adjust Sorting Order**:
   - Select water GameObject
   - In **Mesh Renderer** component:
     - Set **Sorting Layer** (create layers if needed)
     - Set **Order in Layer**:
       - Lower values render first (behind)
       - Higher values render last (in front)
   - Example: Background water = 0, Foreground water = 10

3. **Check Depth Write**:
   - Ensure **Depth Write** is **Off** in shader
   - Depth Write On can cause sorting issues with transparent objects

4. **Use Separate Render Queues**:
   - Background water: Queue 3000 (Transparent)
   - Foreground water: Queue 3001 (Transparent+1)
   - Or use custom queue values

5. **2.5D Layering**:
   - For 2.5D games, use **Sorting Layers**:
     - Create layers: "Background", "Water", "Foreground"
     - Assign objects to appropriate layers
     - Adjust **Order in Layer** within each sorting layer

---

## Transparency Problems

### Problem: Water is Not Transparent / Too Opaque

**Symptoms:**
- Water appears solid
- Can't see objects behind water
- Alpha doesn't affect transparency

**Solutions:**

1. **Check Surface Type**:
   - Shader must be set to **Transparent**
   - In Shader Graph: Graph Settings → Surface Type → Transparent

2. **Check Blend Mode**:
   - Use **Alpha** blend mode
   - Graph Settings → Blend Mode → Alpha

3. **Check Alpha Output**:
   - Ensure **Alpha** output is connected in shader graph
   - Use base texture alpha or set constant alpha (0.5-0.9)
   - Lower alpha = more transparent

4. **Check Material Alpha**:
   - In material inspector, ensure alpha channel is not 1.0
   - Try setting color alpha to 0.8 for translucency

5. **Check Render Pipeline**:
   - Ensure URP is active (not Built-in)
   - Check Graphics Settings → Scriptable Render Pipeline Settings

---

### Problem: Water is Too Transparent / Invisible

**Symptoms:**
- Water barely visible
- Can't see water effect

**Solutions:**

1. **Increase Alpha**:
   - Set alpha output to higher value (0.7-0.9)
   - Or increase material color alpha

2. **Check Base Color**:
   - Ensure base texture has color
   - Check `_ColorTint` is not too dark/transparent

3. **Check Shallow/Deep Colors**:
   - Ensure `_ShallowColor` and `_DeepColor` are visible
   - Adjust colors to be more saturated

---

## Pixel Shimmering / Flickering

### Problem: Water Texture Shimmers or Flickers During Movement

**Symptoms:**
- Texture appears to "swim" or shimmer
- Pixels appear unstable
- Moiré patterns or aliasing

**Solutions:**

1. **Enable Pixel Snapping**:
   - Increase `_PixelSnapScale` (0.5-1.0)
   - This snaps UVs to pixel grid, reducing shimmer

2. **Check Texture Filtering**:
   - In texture import settings:
     - Set **Filter Mode** to **Point (no filter)**
     - This preserves pixel-art crispness
   - Or use **Bilinear** if pixel snapping handles it

3. **Disable Mipmaps**:
   - In texture import settings:
     - Uncheck **Generate Mip Maps**
     - Mipmaps can cause pixel-art to blur

4. **Check UV Tiling**:
   - Ensure UV tiling values are whole numbers (1, 2, 4, etc.)
   - Fractional tiling can cause shimmer

5. **Reduce Distortion**:
   - Lower `_DistortionStrength` (0.01-0.02)
   - Too much distortion breaks pixel grid

6. **Check Anisotropic Filtering**:
   - Disable anisotropic filtering for pixel-art textures
   - Project Settings → Quality → Anisotropic Textures → Disabled

---

## Caustics Not Visible

### Problem: Caustics Don't Appear or Are Too Subtle

**Symptoms:**
- No caustic pattern visible
- Caustics too faint to see

**Solutions:**

1. **Check Keyword**:
   - Ensure `_ENABLE_CAUSTICS` keyword is enabled
   - In material, check keyword is active

2. **Increase Strength**:
   - Increase `_CausticsStrength` (0.3-0.8)
   - Default may be too subtle

3. **Check Texture**:
   - Ensure `_CausticsTex` is assigned
   - Texture should be visible (not black/transparent)
   - Try white texture to test

4. **Check Tint**:
   - Ensure `_CausticsTint` is visible (not black)
   - Use light blue/white for visibility

5. **Check Masking**:
   - If `_CausticsMaskToShallow` is high, caustics only appear in shallow water
   - Lower this value (0.0-0.3) to see caustics everywhere

6. **Check UV Scrolling**:
   - Ensure `_CausticsSpeed` is not zero
   - Try (0.1, 0.1) for visible movement

---

## Shoreline Gradient Not Working

### Problem: No Shallow/Deep Color Transition

**Symptoms:**
- Water is single color
- No gradient from shore to center

**Solutions:**

1. **Check Vertex Colors**:
   - Ensure mesh has vertex colors painted
   - Red channel should vary (0 = deep, 1 = shallow)
   - See `WATER_SHADER_AUTHORING.md` for painting guide

2. **Check Texture Mask**:
   - If using texture mask, ensure `_ShoreMaskTex` is assigned
   - Set `_UseVertexColorMask` to False

3. **Check Colors**:
   - Ensure `_ShallowColor` and `_DeepColor` are different
   - Try extreme values (white vs black) to test

4. **Check Blend Power**:
   - `_DepthBlendPower` controls gradient sharpness
   - Try 1.0 (linear) or 2.0 (sharper) to see effect

5. **Check Mask Value**:
   - Create debug material that outputs mask as color
   - Verify mask values are correct (0-1 range)

6. **Check Shader Graph**:
   - Ensure shoreline gradient nodes are connected
   - Verify **ShorelineGradient** function is called
   - Check **BlendShorelineColor** output is used

---

## Foam Not Appearing

### Problem: Shore Foam Doesn't Show

**Symptoms:**
- No foam band at shoreline
- Foam too subtle

**Solutions:**

1. **Check Keyword**:
   - Ensure `_ENABLE_FOAM` keyword is enabled

2. **Increase Strength**:
   - Increase `_ShoreFoamStrength` (0.5-1.0)

3. **Check Width**:
   - Increase `_ShoreFoamWidth` (0.2-0.4)
   - Too narrow = invisible

4. **Check Shoreline Mask**:
   - Foam appears where shoreline mask transitions
   - Ensure mask has gradient (not flat 0 or 1)

5. **Check Animation**:
   - Ensure `_ShoreFoamSpeed` is not zero
   - Try 2.0-5.0 for visible animation

---

## Performance Issues

### Problem: Water Shader Causes Frame Drops

**Symptoms:**
- FPS drops when water is visible
- Game stutters

**Solutions:**

1. **Disable Optional Features**:
   - Turn off caustics (`_ENABLE_CAUSTICS` = False)
   - Turn off foam (`_ENABLE_FOAM` = False)
   - Turn off highlights (`_ENABLE_HIGHLIGHTS` = False)
   - Turn off dithering (`_ENABLE_DITHER` = False)

2. **Reduce Texture Samples**:
   - Use vertex colors instead of texture mask (saves 1 sample)
   - Disable distortion (saves 1 sample)
   - Disable caustics (saves 1 sample)

3. **Reduce Pixel Snapping**:
   - Lower `_PixelSnapScale` (0.0-0.3)
   - Pixel snapping adds computation

4. **Use LOD**:
   - Create lower-quality version for distant water
   - Use Unity LOD Groups

5. **Reduce Mesh Complexity**:
   - Use fewer vertices for water mesh
   - Simple plane instead of detailed mesh

6. **Check Draw Calls**:
   - Ensure water uses shared material (not material instances)
   - Batch multiple water objects if possible

---

## Shader Graph Compilation Errors

### Problem: Shader Graph Won't Compile

**Symptoms:**
- Red error messages in Shader Graph
- Material shows pink/magenta

**Solutions:**

1. **Check Custom Function Path**:
   - Ensure `WaterPixelArtFunctions.hlsl` is in same directory
   - Or use absolute path in Custom Function node

2. **Check Function Names**:
   - Function names must match exactly (case-sensitive)
   - Verify function signatures match

3. **Check Includes**:
   - Ensure URP includes are available
   - Check URP package is installed

4. **Check Node Connections**:
   - Ensure all required inputs are connected
   - Check for type mismatches (Vector2 vs Float, etc.)

5. **Check Keywords**:
   - Ensure keyword names don't conflict
   - Use `_ENABLE_` prefix for consistency

6. **Check Outputs**:
   - Ensure Base Color output is connected
   - Ensure Alpha output is connected (for transparent)

7. **Reimport Shader**:
   - Right-click shader graph → Reimport
   - Check Console for detailed errors

---

## Material Not Updating

### Problem: Changes to Material Don't Appear

**Symptoms:**
- Property changes don't affect rendering
- Material looks the same

**Solutions:**

1. **Check Material Instance**:
   - Ensure you're editing the material asset, not an instance
   - Or apply changes to all instances

2. **Check Shader Assignment**:
   - Ensure material uses `WaterPixelArt` shader
   - Not a different shader

3. **Check Keyword States**:
   - Keywords must be enabled in material
   - Check material inspector for keyword toggles

4. **Recompile Shader**:
   - Make a small change to shader graph
   - Save to force recompilation

5. **Check Renderer**:
   - Ensure Mesh Renderer has material assigned
   - Check material slot is not empty

---

## UV/Tiling Issues

### Problem: Texture Tiles Incorrectly

**Symptoms:**
- Texture repeats oddly
- Seams visible
- Wrong scale

**Solutions:**

1. **Check UV Tiling**:
   - Adjust `_MainTex_Tiling` values
   - Use whole numbers for pixel-art (1, 2, 4, etc.)

2. **Check Mesh UVs**:
   - Ensure mesh has proper UV mapping
   - Check UV layout in 3D software

3. **Check Texture Import**:
   - Ensure texture is set to **Repeat** wrap mode
   - Not **Clamp** (causes seams)

4. **Check Tiling Node**:
   - Verify **Tiling And Offset** node is connected correctly
   - Check offset values aren't causing issues

---

## Quick Reference: Common Settings

### Good Default Values

```
_ColorTint: (1, 1, 1, 0.8)
_PixelSnapScale: 0.5
_UVScrollSpeed: (0.1, 0.1)
_DistortionStrength: 0.02
_CausticsStrength: 0.3
_CausticsSpeed: (0.08, 0.08)
_ShallowColor: Light blue (0.4, 0.7, 1.0)
_DeepColor: Dark blue (0.1, 0.2, 0.5)
_DepthBlendPower: 1.5
_ShoreFoamStrength: 0.5
_ShoreFoamWidth: 0.2
```

### Performance Presets

**Low-End Mobile:**
- All optional features: Off
- Pixel snap: 0.0-0.3
- Texture size: 64x64

**Mid-Range Mobile:**
- Caustics: On
- Distortion: On
- Foam/Highlights: Off
- Pixel snap: 0.3-0.5

**Desktop:**
- All features: On
- Pixel snap: 0.5-1.0
- Full texture resolution

---

## Still Having Issues?

1. **Check Console**:
   - Look for shader compilation errors
   - Check for missing texture warnings

2. **Test with Simple Setup**:
   - Create minimal shader graph (just base texture)
   - Add features one at a time

3. **Compare with Working Example**:
   - Check Unity's Shader Graph water examples
   - Compare node setups

4. **Check Documentation**:
   - See `WATER_SHADER_SETUP.md` for setup
   - See `WATER_SHADER_AUTHORING.md` for authoring

---

## Next Steps

- Review `WATER_SHADER_SETUP.md` for complete setup
- Review `WATER_SHADER_AUTHORING.md` for shoreline authoring

