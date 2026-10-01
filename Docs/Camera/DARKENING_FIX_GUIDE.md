# Fixing Remaining Darkening Issues

## Quick Diagnostic

1. **Add `DarkeningDiagnostic.cs` to any GameObject**
2. **Right-click component → "Diagnose Darkening Issues"**
3. **Check console output** - it will tell you exactly what's causing darkening

## Common Causes & Fixes

### 1. Materials Using Standard/URP Shaders

**Problem:** Standard shaders respond to lighting, creating darkening.

**Fix:**
- Select material in Project
- Change Shader dropdown to:
  - `Unlit/Texture` (for textured objects)
  - `Unlit/Color` (for solid colors)
  - `Sprites/Default` (for sprites)

**Quick Fix Script:**
- Use `DarkeningDiagnostic` → Right-click → "Fix Common Issues"
- Then manually change materials to Unlit shaders

### 2. Vertex Colors or Baked Lighting

**Problem:** Objects have dark vertex colors or baked lighting data.

**Fix:**
- Select object → MeshRenderer → Materials
- Check if material has vertex color multiplier
- Re-import mesh with "Generate Lightmap UVs" disabled if not needed

### 3. Renderer Settings

**Problem:** Renderer still casting/receiving shadows.

**Fix:**
- Select objects showing darkening
- MeshRenderer component:
  - **Cast Shadows**: Off
  - **Receive Shadows**: Off

### 4. Lighting Settings

**Problem:** Ambient lighting not set to Flat mode.

**Fix:**
- Window → Rendering → Lighting
- Environment tab:
  - **Ambient Mode**: Flat
  - **Ambient Intensity**: 1.0
  - **Ambient Color**: White

## Automated Fix

Run the diagnostic tool:
1. Add `DarkeningDiagnostic.cs` to scene
2. Right-click component → "Fix Common Issues"
3. This will:
   - Set ambient to Flat
   - Disable shadows on lights
   - Disable shadows on renderers
4. Then manually change materials to Unlit shaders

## Testing

After fixes:
1. Enter Play Mode
2. Check if darkening is gone
3. If still present, check diagnostic output for specific issues

