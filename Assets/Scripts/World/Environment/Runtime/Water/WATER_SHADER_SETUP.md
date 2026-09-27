# Water Shader Setup Guide

## Overview

This guide explains how to set up the **WaterPixelArt** shader for stylized pixel-art water in Signal Jam. The shader uses URP Shader Graph with custom HLSL functions for pixel snapping and advanced features.

---

## Prerequisites

- Unity 2021.2+ with URP installed
- Shader Graph package installed
- Pixel-art water texture tiles
- Optional: Caustics texture, noise texture for distortion

---

## Step 1: Create Shader Graph

### 1.1 Create New Shader Graph

1. In Unity, right-click in `Assets/Scripts/Environment/Water/`
2. Select **Create > Shader Graph > URP > Lit Shader Graph** (or **Unlit Shader Graph**)
3. Rename to `WaterPixelArt.shadergraph`

### 1.2 Configure Graph Settings

1. Open the Shader Graph window
2. Click the **Graph Settings** button (gear icon)
3. Configure:
   - **Surface Type**: `Transparent`
   - **Blend Mode**: `Alpha`
   - **Render Face**: `Both` (or `Front` if single-sided)
   - **Depth Write**: `Off` (for proper transparency)
   - **Depth Test**: `LEqual`
   - **Render Queue**: `Transparent` (3000)

---

## Step 2: Add Custom Function Nodes

### 2.1 Import HLSL Functions

1. Ensure `WaterPixelArtFunctions.hlsl` is in the same directory as the shader graph
2. The shader graph will automatically find it when you create Custom Function nodes

### 2.2 Create Custom Function Nodes

For each function, create a **Custom Function** node:

1. Right-click in graph → **Create Node**
2. Search for **Custom Function**
3. Configure each node:

#### Pixel Snap UV Function
- **Name**: `PixelSnapUV`
- **Source**: `File`
- **File**: `WaterPixelArtFunctions.hlsl`
- **Inputs**:
  - `uv` (Vector2)
  - `snapScale` (Float)
  - `textureSize` (Vector2) - Optional, can use constant
- **Output**: `Vector2`

#### Shoreline Gradient Function
- **Name**: `ShorelineGradient`
- **Source**: `File`
- **File**: `WaterPixelArtFunctions.hlsl`
- **Inputs**:
  - `vertexColorMask` (Float) - From Vertex Color node (Red channel)
  - `textureMask` (Float) - From ShoreMask texture sample
  - `useVertexColor` (Float) - Toggle (0 or 1)
- **Output**: `Float`

#### Blend Shoreline Color Function
- **Name**: `BlendShorelineColor`
- **Source**: `File`
- **File**: `WaterPixelArtFunctions.hlsl`
- **Inputs**:
  - `shallowColor` (Vector3)
  - `deepColor` (Vector3)
  - `mask` (Float)
  - `blendPower` (Float)
- **Output**: `Vector3`

#### Stylized Caustics Function
- **Name**: `StylizedCaustics`
- **Source**: `File`
- **File**: `WaterPixelArtFunctions.hlsl`
- **Inputs**:
  - `causticsSample` (Vector3) - From caustics texture
  - `strength` (Float)
  - `maskToShallow` (Float)
  - `shorelineMask` (Float)
- **Output**: `Vector3`

#### Shore Foam Function
- **Name**: `ShoreFoam`
- **Source**: `File`
- **File**: `WaterPixelArtFunctions.hlsl`
- **Inputs**:
  - `shorelineMask` (Float)
  - `foamWidth` (Float)
  - `foamSpeed` (Float)
  - `time` (Float) - Use `Time` node
- **Output**: `Float`

#### Ordered Dither Function
- **Name**: `OrderedDither`
- **Source**: `File`
- **File**: `WaterPixelArtFunctions.hlsl`
- **Inputs**:
  - `position` (Vector2) - Use Screen Position node
- **Output**: `Float`

---

## Step 3: Add Properties

Add all properties to the **Blackboard** panel:

### Base Water Properties

1. **`_MainTex`** (Texture2D)
   - Display Name: "Water Texture"
   - Default: White texture
   - Reference: `_MainTex`

2. **`_MainTex_Tiling`** (Vector2)
   - Display Name: "UV Tiling"
   - Default: (1, 1)
   - Reference: `_MainTex_Tiling`

3. **`_ColorTint`** (Color)
   - Display Name: "Color Tint"
   - Default: White (1, 1, 1, 1)
   - Reference: `_ColorTint`

4. **`_UVScrollSpeed`** (Vector2)
   - Display Name: "UV Scroll Speed"
   - Default: (0.1, 0.1)
   - Reference: `_UVScrollSpeed`

5. **`_PixelSnapScale`** (Float)
   - Display Name: "Pixel Snap Scale"
   - Default: 0.5
   - Range: 0-1
   - Reference: `_PixelSnapScale`

### Distortion Properties

6. **`_NoiseTex`** (Texture2D)
   - Display Name: "Noise Texture"
   - Default: None
   - Reference: `_NoiseTex`

7. **`_DistortionStrength`** (Float)
   - Display Name: "Distortion Strength"
   - Default: 0.02
   - Range: 0-0.1
   - Reference: `_DistortionStrength`

8. **`_DistortionSpeed`** (Vector2)
   - Display Name: "Distortion Speed"
   - Default: (0.05, 0.05)
   - Reference: `_DistortionSpeed`

9. **`_DistortionScale`** (Float)
   - Display Name: "Distortion Scale"
   - Default: 1.0
   - Reference: `_DistortionScale`

### Caustics Properties

10. **`_CausticsTex`** (Texture2D)
    - Display Name: "Caustics Texture"
    - Default: None
    - Reference: `_CausticsTex`

11. **`_CausticsStrength`** (Float)
    - Display Name: "Caustics Strength"
    - Default: 0.3
    - Range: 0-1
    - Reference: `_CausticsStrength`

12. **`_CausticsScale`** (Float)
    - Display Name: "Caustics Scale"
    - Default: 1.0
    - Reference: `_CausticsScale`

13. **`_CausticsSpeed`** (Vector2)
    - Display Name: "Caustics Speed"
    - Default: (0.08, 0.08)
    - Reference: `_CausticsSpeed`

14. **`_CausticsTint`** (Color)
    - Display Name: "Caustics Tint"
    - Default: Light blue/white
    - Reference: `_CausticsTint`

15. **`_CausticsMaskToShallow`** (Float)
    - Display Name: "Caustics Mask To Shallow"
    - Default: 0.5
    - Range: 0-1
    - Reference: `_CausticsMaskToShallow`

### Shoreline/Depth Properties

16. **`_ShoreMaskTex`** (Texture2D)
    - Display Name: "Shoreline Mask Texture"
    - Default: None
    - Reference: `_ShoreMaskTex`

17. **`_ShallowColor`** (Color)
    - Display Name: "Shallow Color"
    - Default: Light blue/cyan
    - Reference: `_ShallowColor`

18. **`_DeepColor`** (Color)
    - Display Name: "Deep Color"
    - Default: Dark blue
    - Reference: `_DeepColor`

19. **`_DepthBlendPower`** (Float)
    - Display Name: "Depth Blend Power"
    - Default: 1.5
    - Range: 0.5-5
    - Reference: `_DepthBlendPower`

20. **`_UseVertexColorMask`** (Boolean)
    - Display Name: "Use Vertex Color Mask"
    - Default: True
    - Reference: `_UseVertexColorMask`

### Shore Foam Properties

21. **`_ShoreFoamStrength`** (Float)
    - Display Name: "Shore Foam Strength"
    - Default: 0.5
    - Range: 0-1
    - Reference: `_ShoreFoamStrength`

22. **`_ShoreFoamWidth`** (Float)
    - Display Name: "Shore Foam Width"
    - Default: 0.2
    - Range: 0-1
    - Reference: `_ShoreFoamWidth`

23. **`_ShoreFoamSpeed`** (Float)
    - Display Name: "Shore Foam Speed"
    - Default: 2.0
    - Reference: `_ShoreFoamSpeed`

### Highlight Properties

24. **`_HighlightStrength`** (Float)
    - Display Name: "Highlight Strength"
    - Default: 0.2
    - Range: 0-1
    - Reference: `_HighlightStrength`

25. **`_HighlightColor`** (Color)
    - Display Name: "Highlight Color"
    - Default: White
    - Reference: `_HighlightColor`

26. **`_HighlightScale`** (Float)
    - Display Name: "Highlight Scale"
    - Default: 10.0
    - Reference: `_HighlightScale`

27. **`_HighlightSpeed`** (Float)
    - Display Name: "Highlight Speed"
    - Default: 1.0
    - Reference: `_HighlightSpeed`

---

## Step 4: Add Shader Keywords

Add keywords for quality toggles:

1. In **Graph Settings**, go to **Keywords** section
2. Add each keyword:

- **`_ENABLE_CAUSTICS`** (Boolean, Default: True)
- **`_ENABLE_DISTORTION`** (Boolean, Default: True)
- **`_ENABLE_FOAM`** (Boolean, Default: False)
- **`_ENABLE_HIGHLIGHTS`** (Boolean, Default: False)
- **`_ENABLE_DITHER`** (Boolean, Default: False)

---

## Step 5: Build Shader Graph Logic

### 5.1 Base Water Texture

1. Create **Sample Texture 2D** node for `_MainTex`
2. Create **UV** node
3. Create **Tiling And Offset** node:
   - Connect UV → Tiling input
   - Connect `_MainTex_Tiling` → Tiling property
   - Connect `_UVScrollSpeed * Time` → Offset
4. Apply distortion (if enabled):
   - Sample `_NoiseTex` with scaled/offset UVs
   - Use **Wave Distortion** custom function or add noise to UV
   - Connect to Tiling And Offset UV input
5. Apply pixel snapping:
   - Use **PixelSnapUV** custom function
   - Connect distorted UV → `uv` input
   - Connect `_PixelSnapScale` → `snapScale`
   - Connect texture size (e.g., Vector2(64, 64)) → `textureSize`
6. Connect snapped UV to Sample Texture 2D
7. Multiply texture sample by `_ColorTint`

### 5.2 Shoreline Gradient

1. Get vertex color:
   - Create **Vertex Color** node
   - Split to get Red channel (shoreline mask)
2. Sample shore mask texture (if not using vertex colors):
   - Create **Sample Texture 2D** for `_ShoreMaskTex`
   - Use Red channel as mask
3. Use **ShorelineGradient** function:
   - Connect vertex color red → `vertexColorMask`
   - Connect texture mask red → `textureMask`
   - Connect `_UseVertexColorMask` → `useVertexColor`
4. Use **BlendShorelineColor** function:
   - Connect `_ShallowColor` → `shallowColor`
   - Connect `_DeepColor` → `deepColor`
   - Connect gradient mask → `mask`
   - Connect `_DepthBlendPower` → `blendPower`
5. Multiply base water color by shoreline blend result

### 5.3 Caustics (If Enabled)

1. Create **Branch** node (controlled by `_ENABLE_CAUSTICS` keyword)
2. Sample `_CausticsTex`:
   - Create **Sample Texture 2D** for `_CausticsTex`
   - Use UV with `_CausticsScale` tiling
   - Add `_CausticsSpeed * Time` for scrolling
3. Use **StylizedCaustics** function:
   - Connect caustics sample → `causticsSample`
   - Connect `_CausticsStrength` → `strength`
   - Connect `_CausticsMaskToShallow` → `maskToShallow`
   - Connect shoreline mask → `shorelineMask`
4. Multiply caustics result by `_CausticsTint`
5. Add to water color (use **Add** node)

### 5.4 Shore Foam (If Enabled)

1. Create **Branch** node (controlled by `_ENABLE_FOAM` keyword)
2. Use **ShoreFoam** function:
   - Connect shoreline mask → `shorelineMask`
   - Connect `_ShoreFoamWidth` → `foamWidth`
   - Connect `_ShoreFoamSpeed` → `foamSpeed`
   - Connect **Time** node Y → `time`
3. Multiply foam result by `_ShoreFoamStrength`
4. Add white foam color to water (use **Lerp** to blend)

### 5.5 Highlights (If Enabled)

1. Create **Branch** node (controlled by `_ENABLE_HIGHLIGHTS` keyword)
2. Generate noise pattern:
   - Use **Simple Noise** node or sample noise texture
   - Scale by `_HighlightScale`
   - Animate with `_HighlightSpeed * Time`
3. Use **StylizedHighlights** function or threshold noise
4. Multiply by `_HighlightColor` and `_HighlightStrength`
5. Add to water color

### 5.6 Dithering (If Enabled)

1. Create **Branch** node (controlled by `_ENABLE_DITHER` keyword)
2. Use **OrderedDither** function:
   - Connect **Screen Position** node → `position`
3. Add dither offset to final color (subtle, ~0.01-0.02 strength)

### 5.7 Final Output

1. Combine all effects:
   - Base water color
   - Shoreline gradient blend
   - Caustics (if enabled)
   - Foam (if enabled)
   - Highlights (if enabled)
   - Dithering (if enabled)
2. Connect final color to **Base Color** output
3. Set **Alpha** output:
   - Use base texture alpha
   - Or constant alpha (e.g., 0.8 for translucency)

---

## Step 6: Create Material

1. Right-click in project → **Create > Material**
2. Name: `WaterPixelArt`
3. Assign shader: `WaterPixelArt`
4. Configure properties:
   - Assign water texture to `_MainTex`
   - Set UV tiling
   - Adjust colors (shallow/deep)
   - Enable/disable quality features via keywords

---

## Step 7: Setup GameObject

### 7.1 Create Water Mesh

1. Create **Plane** or **Quad** GameObject
2. Scale to desired water area size
3. Position at water level

### 7.2 Assign Material

1. Select water GameObject
2. In **Mesh Renderer** component:
   - Assign `WaterPixelArt` material
   - Set **Sorting Layer** if needed (for 2.5D layering)
   - Set **Order in Layer** (e.g., 0 for base, higher for foreground water)

### 7.3 Configure Render Settings

- **Cast Shadows**: Off (optional, depends on art style)
- **Receive Shadows**: On (optional)
- **Light Probes**: Use if needed for lighting

---

## Step 8: Author Shoreline Mask

See `WATER_SHADER_AUTHORING.md` for detailed instructions on:
- Painting vertex colors
- Creating texture masks
- Procedural shoreline gradients

---

## Recommended Texture Sizes

- **Water Base Texture**: 64x64 to 128x128 (pixel-art tiles)
- **Caustics Texture**: 256x256 (can be grayscale)
- **Noise Texture**: 64x64 to 128x128 (small, tileable)
- **Shoreline Mask**: Match mesh UV layout

---

## Quality Presets

### High Quality (Desktop)
- All features enabled
- Pixel snap scale: 0.5-1.0
- Full texture resolution

### Medium Quality (Mobile)
- Caustics: On
- Distortion: On
- Foam: Off
- Highlights: Off
- Pixel snap scale: 0.3-0.5

### Low Quality (Low-end Mobile)
- Caustics: Off
- Distortion: Off
- Foam: Off
- Highlights: Off
- Pixel snap scale: 0.0-0.3

---

## Next Steps

- See `WATER_SHADER_AUTHORING.md` for shoreline mask authoring
- See `WATER_SHADER_TROUBLESHOOTING.md` for common issues

