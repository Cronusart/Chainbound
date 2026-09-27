# Shadows with Unlit Materials Guide

## Problem

You want shadows cast by directional light, but Unlit shaders don't receive shadows, and Standard shaders cause unwanted darkening.

## Solution: Custom Unlit Shadow Receiver Shader

I've created `UnlitShadowReceiver.shader` that:
- Uses flat, unlit colors (no darkening from lighting)
- Receives shadows from directional lights
- Can cast shadows
- Fully customizable shadow strength

## Setup

### Step 1: Create Material with Custom Shader

1. **Right-click in Project → Create → Material**
2. **Name it** (e.g., "UnlitShadowMaterial")
3. **Select the material**
4. **In Inspector, change Shader dropdown to:**
   - **For URP projects**: `Unlit/ShadowReceiverURP` (use this one!)
   - **For Built-in RP**: `Unlit/ShadowReceiver`
   
   **Note:** Since you're using URP, use `Unlit/ShadowReceiverURP`

### Step 2: Configure Material

1. **Assign texture** (if using textured objects)
2. **Set Color** (tint color, white = no tint)
3. **Adjust Shadow Strength**:
   - `0` = No shadows (pure unlit)
   - `0.5` = Moderate shadows (recommended)
   - `1.0` = Full shadow strength

### Step 3: Apply to Objects

1. **Select objects** showing darkening
2. **Assign the new material**
3. **Ensure objects can cast shadows:**
   - MeshRenderer → Cast Shadows = On
   - Receive Shadows = On (for shadow receiving)

### Step 4: Enable Shadows on Light

1. **Select Directional Light**
2. **Set Shadows** to `Soft Shadows` or `Hard Shadows`
3. **Adjust Shadow Strength** (0.5-0.8 recommended for subtle shadows)

## Alternative: Use Built-in Shader with Modifications

If the custom shader doesn't work, try:

1. **Use `Sprites/Default` shader** (if using sprites)
   - Supports shadow receiving
   - Flat colors

2. **Use `Unlit/Texture` with shadow receiving enabled**
   - May require shader modifications
   - Less reliable than custom shader

## Testing

1. **Enter Play Mode**
2. **Check shadows appear** on objects
3. **Verify no darkening** from lighting (only shadows)
4. **Adjust Shadow Strength** in material if needed

## Troubleshooting

### Shadows not appearing?

- Check Directional Light has Shadows enabled
- Verify objects have Cast Shadows = On
- Ensure objects have Receive Shadows = On
- Check material uses UnlitShadowReceiver shader

### Still seeing darkening?

- Verify material uses UnlitShadowReceiver shader (not Standard)
- Check Shadow Strength is set correctly
- Ensure no additional lights causing darkening
- Verify ambient lighting is set to Flat mode

### Shadows too strong/weak?

- Adjust **Shadow Strength** in material (0-1 range)
- Adjust **Shadow Strength** on Directional Light
- Adjust **Shadow Type** (Hard vs Soft)

## Recommended Settings

**For classic Game Boy with subtle shadows:**

```
Material:
- Shader: Unlit/ShadowReceiver
- Shadow Strength: 0.3-0.5
- Color: White (or desired tint)

Directional Light:
- Shadows: Soft Shadows
- Shadow Strength: 0.5-0.7
- Shadow Resolution: 2048
```

