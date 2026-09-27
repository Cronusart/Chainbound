# WaterPixelArt Shader - Stylized Pixel-Art Water for Signal Jam

## Overview

A complete stylized water shader solution for Signal Jam's 2.5D pixel-art world. The shader preserves pixel-art aesthetics while adding translucency, caustics, shoreline gradients, and subtle motion effects.

## Approach: URP Shader Graph + Custom HLSL Functions

**Rationale:**
- **Shader Graph** provides artist-friendly visual editing and iteration
- **Custom HLSL functions** handle pixel snapping and complex calculations not natively supported in Shader Graph
- Balances ease of use with precision needed for pixel-art preservation
- Transparent rendering mode for translucency effects

## Files

### Core Implementation

- **`WaterPixelArtFunctions.hlsl`** - Custom HLSL functions for pixel snapping, caustics, shoreline gradients, foam, and dithering
- **`SHADER_GRAPH_CREATION_NOTE.md`** - Instructions for creating the Shader Graph in Unity

### Documentation

- **`WATER_SHADER_SETUP.md`** - Complete step-by-step setup guide for creating and configuring the shader graph
- **`WATER_SHADER_AUTHORING.md`** - Guide for authoring shoreline masks (vertex colors, texture masks, procedural)
- **`WATER_SHADER_TROUBLESHOOTING.md`** - Common issues and solutions

## Quick Start

1. **Read `SHADER_GRAPH_CREATION_NOTE.md`** - Understand how to create the shader graph
2. **Follow `WATER_SHADER_SETUP.md`** - Step-by-step instructions to build the shader
3. **Author shoreline mask** - See `WATER_SHADER_AUTHORING.md` for vertex color painting or texture mask creation
4. **Create material and assign** - Use the shader on your water meshes

## Features

### ✅ Core Features

- **Pixel-art base texture** with UV scrolling
- **Pixel snapping** to preserve pixel grid
- **Subtle wave distortion** (preserves pixel identity)
- **Stylized caustics** overlay with shallow water masking
- **Shoreline/depth gradient** (vertex color or texture driven)
- **Optional shore foam** band with animation
- **Stylized highlights** (non-PBR sparkles)
- **Optional dithering** for pixel-art friendly gradients

### ✅ Performance Features

- **Quality toggles** via shader keywords (caustics, distortion, foam, highlights, dither)
- **Minimal texture samples** (2-4 samples depending on enabled features)
- **Mobile-friendly** (PSG1 class hardware compatible)
- **No screen-space effects** (no expensive depth sampling)

### ✅ Authoring Features

- **Vertex color support** for precise shoreline painting
- **Texture mask fallback** for large water areas
- **Procedural option** for dynamic/scripted water
- **Comprehensive property groups** for easy material setup

## Material Properties

All properties are organized into logical groups:

- **Base Water**: Texture, tiling, color tint, scroll speed, pixel snap
- **Distortion**: Noise texture, strength, speed, scale
- **Caustics**: Texture, strength, scale, speed, tint, shallow masking
- **Shoreline/Depth**: Mask texture, shallow/deep colors, blend power
- **Shore Foam**: Strength, width, animation speed
- **Highlights**: Strength, color, scale, speed

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

## Requirements

- Unity 2021.2+ with URP
- Shader Graph package
- Pixel-art water texture tiles
- Optional: Caustics texture, noise texture

## Next Steps

1. **Create Shader Graph**: Follow `WATER_SHADER_SETUP.md`
2. **Author Shoreline**: See `WATER_SHADER_AUTHORING.md`
3. **Troubleshoot**: Check `WATER_SHADER_TROUBLESHOOTING.md` if issues arise

## Support

For detailed instructions, see:
- **Setup**: `WATER_SHADER_SETUP.md`
- **Authoring**: `WATER_SHADER_AUTHORING.md`
- **Troubleshooting**: `WATER_SHADER_TROUBLESHOOTING.md`

---

**Note**: The Shader Graph file (`WaterPixelArt.shadergraph`) must be created manually in Unity following the setup guide. This is due to the complexity of Shader Graph JSON structure. All necessary functions and documentation are provided to make this process straightforward.

