# Top-Down Lighting Guide - Classic Game Boy Aesthetic

## Problem

Near-side objects show darkened portions due to lighting/shadows, breaking the classic flat top-down aesthetic.

## Solutions (Choose One or Combine)

### Solution 1: Disable Shadows (Quickest Fix)

**In Unity Editor:**

1. **Disable Directional Light Shadows:**
   - Select your Directional Light in scene
   - In Inspector, set **Shadows** to `None`
   - This removes all shadow casting

2. **Disable Object Shadows:**
   - Select objects showing darkening
   - In MeshRenderer component, set **Cast Shadows** to `Off`
   - Set **Receive Shadows** to `Off`

**Pros:** Instant fix, no code changes  
**Cons:** No shadows at all (might want some for depth)

---

### Solution 2: Use Unlit Shaders (Best for Pixel Art)

**For each material:**

1. Select material in Project
2. In Inspector, click shader dropdown
3. Change to:
   - `Unlit/Texture` (simple flat colors)
   - `Unlit/Color` (pure color, no texture)
   - `Sprites/Default` (if using sprites)

**Or create unlit materials:**

1. Right-click in Project → `Create > Material`
2. Name it (e.g., "UnlitBuilding")
3. Set Shader to `Unlit/Texture` or `Unlit/Color`
4. Assign texture/color
5. Apply to objects

**Pros:** True flat colors, classic Game Boy look  
**Cons:** Need to update all materials

---

### Solution 3: Adjust Lighting Settings (Balanced)

**In Unity Editor:**

1. **Window → Rendering → Lighting**
2. **Environment** tab:
   - Set **Ambient Intensity** to `1.0` (full brightness)
   - Set **Ambient Mode** to `Flat` (no gradient)
   - Adjust **Ambient Color** to white or slightly warm

3. **Directional Light:**
   - Set **Intensity** to `1.0` or higher
   - Set **Color** to white
   - Set **Shadows** to `Soft Shadows` with low strength, or `None`

**Pros:** Keeps some lighting flexibility  
**Cons:** May still have subtle darkening

---

### Solution 4: Add Flat Lighting Script (Recommended)

Use the provided `FlatTopDownLighting.cs` script to automatically configure lighting for top-down view.

**Setup:**
1. Add script to any GameObject in scene (or Camera)
2. Configure settings in Inspector
3. Script auto-adjusts lighting on Start

**Pros:** Automatic, configurable, can be toggled  
**Cons:** Requires script component

---

### Solution 5: Post-Processing (Optional Enhancement)

Add subtle post-processing for retro look:

1. **Install Post-Processing Stack v2** (if not already)
2. Add **Post-Process Volume** to scene
3. Add effects:
   - **Color Grading**: Increase saturation slightly
   - **Bloom**: Subtle glow (optional)
   - **Vignette**: Slight darkening at edges (optional)

**Pros:** Can enhance retro aesthetic  
**Cons:** Additional setup, performance cost

---

## Recommended Approach

**For classic Game Boy top-down:**

1. **Use Unlit Shaders** (Solution 2) - Best for pixel art
2. **Disable Shadows** (Solution 1) - Quick fix
3. **Add Flat Lighting Script** (Solution 4) - Automatic setup

Combine Solutions 1 + 2 + 4 for best results.

---

## Quick Test

To quickly test if lighting is the issue:

1. **Temporarily disable all lights:**
   - Select all Directional Lights
   - Uncheck `Enabled` in Inspector
   - If darkening disappears → lighting is the cause

2. **Test with unlit shader:**
   - Change one material to `Unlit/Texture`
   - If it looks better → use unlit shaders

---

## Troubleshooting

### Still seeing darkening?

- Check if objects have **MeshRenderer** with shadows enabled
- Verify **Lighting Settings** → **Ambient** is set to `Flat` mode
- Ensure no **Post-Processing** effects are darkening the image
- Check if materials are using **Standard** shader (switch to Unlit)

### Want subtle depth but flat colors?

- Use **Unlit shaders** for flat colors
- Add **very subtle** directional light (Intensity 0.3-0.5)
- Keep shadows **disabled**
- This gives slight depth without darkening

