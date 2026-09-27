# Server LED Flicker System - Setup Guide

Pixel-perfect LED flicker shader for server rack overlays. Creates sporadic, unpredictable blinking across server rooms while maintaining pixel-art crispness.

## Overview

The system consists of:
1. **ServerLedFlicker.shader** - URP-compatible HLSL shader handling per-LED random flickering
2. **ServerLedFlicker.mat** - Shared material for all server overlay quads
3. **ServerLightFlicker.cs** - MonoBehaviour for per-instance seed/variation via MaterialPropertyBlock

## Quick Setup

### 1. Texture Import Settings

For your LED overlay texture (the "lights ON" sprite with transparent background):

1. Select texture in Project window
2. In Inspector, set:
   - **Texture Type**: Default (or Sprite if needed for other uses)
   - **Filter Mode**: Point (no filter) ⚠️ CRITICAL for pixel-perfect
   - **Compression**: None (or RGBA 32 bit for quality)
   - **Generate Mip Maps**: OFF ⚠️ CRITICAL
   - **Alpha Source**: From Gray Scale or Input Texture Alpha (depending on your source)
   - **Alpha Is Transparency**: ON
3. Click **Apply**

### 2. Material Setup

1. Locate `Assets/Scripts/Environment/ServerLedFlicker.mat`
2. If shader isn't assigned:
   - Find shader: `Environment/ServerLedFlicker_AlphaClip`
   - Drag shader to material or select from dropdown
3. Assign your LED overlay texture to `_MaskTex` slot
4. Adjust parameters as needed (see Parameter Reference below)

### 3. Scene Setup

For each server rack:

**Base Mesh (Lights OFF):**
- Uses your "lights off" texture/material
- Standard placement

**Overlay Quad (Lights ON):**
1. Create a Quad: `GameObject > 3D Object > Quad`
2. Position slightly in front of base mesh (Z offset ~0.001 to 0.01 units)
3. Scale to match base texture dimensions
4. Assign `ServerLedFlicker.mat` material
5. Add `ServerLightFlicker` component

### 4. ServerLightFlicker Component

Add to overlay quad and configure:

```
[Inspector Settings]
├── References
│   └── Overlay Renderer: (auto-finds if empty)
│
├── Flicker Settings
│   ├── Speed: 4.0 (blinks per second)
│   ├── Density: 0.45 (% LEDs on)
│   ├── Emission: 1.5 (glow intensity)
│   ├── Tint: Cyan (0, 1, 0.85)
│   └── Clip Threshold: 0.5
│
├── Seed & Variation
│   ├── Deterministic Seed From Position: ✓
│   ├── Include Pos Y: ✓ (or OFF for stacked racks)
│   └── Seed Override: 0 (if not using position seed)
│
├── Grid Layout
│   ├── Tex Width: 23
│   ├── Tex Height: 27
│   ├── Columns: 17
│   ├── Rows: 15
│   ├── Led Start X: 4
│   ├── Led Size Px: 1
│   └── Led Gap Px: 1
│
└── Fake Glow (optional)
    ├── Use Fake Glow: OFF
    ├── Glow Strength: 0.3
    └── Glow Spread: 1.0
```

## Parameter Reference

### Shader Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `_MaskTex` | Texture2D | - | LED overlay texture (transparent bg, LEDs visible) |
| `_Tint` | Color | Cyan | LED color multiplier |
| `_Emission` | Float | 1.5 | Emission/glow intensity (1-3 typical) |
| `_Clip` | Float | 0.5 | Alpha clip threshold for crisp edges |
| `_Speed` | Float | 4.0 | Blink speed (higher = faster) |
| `_Density` | Float | 0.45 | Probability LED is ON (0-1) |
| `_Seed` | Float | 0 | Per-instance random seed |
| `_TexSize` | Vector2 | (23,27) | Texture dimensions in pixels |
| `_Cols` | Float | 17 | Number of LED columns |
| `_Rows` | Float | 15 | Number of logical LED rows |
| `_LedStartX` | Float | 4 | First LED X position (pixels from left) |
| `_LedSizePx` | Float | 1 | LED width in pixels |
| `_LedGapPx` | Float | 1 | Gap between LEDs in pixels |
| `_UseFakeGlow` | Float | 0 | Enable fake glow (0/1) |
| `_GlowStrength` | Float | 0.3 | Glow intensity |
| `_GlowSpread` | Float | 1.0 | Glow spread in pixels |

### Grid Layout Explanation

Your 23x27 texture with 17x15 LED grid:

```
┌─────────────────────────────────────┐
│  ···· Band 4 (top rows)             │  Y = varies
│  ···································│
│        (5px gap)                    │
│  ···· Band 3                        │
│  ···································│
│        (5px gap)                    │
│  ···· Band 2                        │
│  ···································│
│        (1px gap)                    │
│  ···· Band 1 (bottom rows)          │  Y = 4
│                                     │
└─────────────────────────────────────┘
     X = 4 (first LED column)
```

The shader handles irregular vertical spacing via hardcoded row Y positions. If your texture differs, modify `ROW_Y_POSITIONS` array in shader.

## Performance Notes

### What Makes It Fast
- **No texture swapping** - single material, shader-based randomization
- **No per-LED GameObjects** - pure GPU computation
- **MaterialPropertyBlock** - no material instantiation, SRP Batcher friendly
- **No Update() allocations** - all state cached at initialization
- **GPU hash function** - deterministic random per-cell in fragment shader

### Memory
- One shared material for all racks
- One MaterialPropertyBlock per ServerLightFlicker instance (~200 bytes)
- No runtime allocations after Start()

### GPU Cost
- Simple math ops per fragment (hash, comparison)
- Optional fake glow adds 4 texture samples
- Recommend disabling glow on mobile/low-end

## Profiling Checklist

1. **Verify no GC allocations:**
   - Open Profiler > CPU Usage > Hierarchy
   - Search for `ServerLightFlicker`
   - Confirm 0 B GC Alloc in steady state

2. **Verify SRP Batcher:**
   - Frame Debugger > check batching
   - All server overlays should batch together

3. **Verify no material instances:**
   - In Hierarchy, select multiple server overlays
   - Inspector should show same shared material

## Troubleshooting

### LEDs not flickering
- Check `_Speed` > 0
- Check `_Density` between 0.1 and 0.9
- Verify overlay is visible (Z offset correct)

### Blurry/soft edges
- Texture Filter Mode must be **Point (no filter)**
- Mip Maps must be **OFF**
- `_Clip` threshold around 0.5

### All racks flicker identically
- Enable `Deterministic Seed From Position`
- Or set unique `Seed Override` per rack
- Check racks aren't at exact same position

### Flickering too fast/slow
- Adjust `_Speed` (2-6 is typical range)
- Lower = calmer, higher = frantic

### Too many/few LEDs on
- Adjust `_Density` (0.3-0.6 typical)
- 0.3 = sparse blinking, 0.6 = mostly on

### Grid alignment wrong
- Verify `_TexSize` matches actual texture dimensions
- Adjust `_LedStartX` to match first LED column
- Adjust `_LedSizePx` and `_LedGapPx` to match your pixel grid

## Runtime API

```csharp
// Get component
var flicker = serverOverlay.GetComponent<ServerLightFlicker>();

// Adjust parameters
flicker.SetSpeed(6f);           // Faster blinking
flicker.SetDensity(0.3f);       // Fewer LEDs on
flicker.SetEmission(2f);        // Brighter
flicker.SetTint(Color.red);     // Change color

// Batch parameter change (more efficient)
flicker.SetParameters(
    newSpeed: 5f,
    newDensity: 0.4f,
    newEmission: 2f,
    newTint: new Color(0f, 1f, 0.85f)
);

// Seed control
flicker.SetSeed(42f);           // Manual seed
flicker.RecomputeSeed();        // Re-derive from position

// Glow control
flicker.SetFakeGlow(true, strength: 0.4f, spread: 1.5f);

// Force refresh
flicker.Reinitialize();
```

## Advanced: Custom Row Positions

If your texture has different vertical LED band spacing, modify the shader constant array:

```hlsl
// In ServerLedFlicker.shader, find ROW_Y_POSITIONS:
static const float ROW_Y_POSITIONS[15] = {
    // Band 1 (bottom) - rows 0,1,2,3
    4.0, 6.0, 8.0, 10.0,
    // Band 2 - rows 4,5,6,7
    16.0, 18.0, 20.0, 22.0,
    // Band 3 - rows 8,9,10,11 (adjust or set to 0 if unused)
    0.0, 0.0, 0.0, 0.0,
    // Band 4 - rows 12,13,14
    0.0, 0.0, 0.0
};
```

Set values to actual pixel Y positions (from bottom) for each logical row.
Set unused rows to 0.0 to disable them.

## Files

- `Assets/Scripts/Environment/ServerLedFlicker.shader` - The shader
- `Assets/Scripts/Environment/ServerLedFlicker.mat` - Shared material
- `Assets/Scripts/Environment/ServerLightFlicker.cs` - Per-instance component
- `Assets/Scripts/Environment/SERVER_LED_SETUP.md` - This guide
