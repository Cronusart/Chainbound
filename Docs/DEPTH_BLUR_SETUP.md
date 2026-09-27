# Depth Blur Setup Guide

## Problem

Far-side objects should be blurred to create depth perception in top-down view, similar to classic Game Boy games.

## Solution Options

### Option 1: URP Post-Processing Depth of Field (Recommended)

**Best quality, requires URP Post-Processing package:**

1. **Install Post-Processing:**
   - Window → Package Manager
   - Install "Universal RP" (if not already)
   - Post-processing is included in URP

2. **Create Post-Process Volume:**
   - Right-click Hierarchy → Volume → Global Volume
   - Or create GameObject → Add Component → Volume → Is Global = true

3. **Add Depth of Field Effect:**
   - In Volume component, click "Add Override"
   - Select "Depth of Field"
   - Configure:
     - **Mode**: Gaussian
     - **Start**: 5 (focus distance - objects closer are sharp)
     - **End**: 20 (distance where blur reaches max)
     - **Max Radius**: 2-4 (blur intensity)

4. **Assign to Camera:**
   - Select Camera
   - Add Component → Volume (if using local volume)
   - Or ensure Global Volume is in scene

**Pros:** Best quality, smooth blur, depth-aware  
**Cons:** Requires URP Post-Processing setup

---

### Option 2: Simple Depth Blur Script (Quick Fix)

**Uses provided `TopDownDepthBlur.cs`:**

1. **Add Component:**
   - Select Camera GameObject
   - Add Component → `TopDownDepthBlur`

2. **Configure Settings:**
   - **Enable Blur**: ✓
   - **Focus Distance**: 5 (objects closer are sharp)
   - **Blur Intensity**: 2-4
   - **Max Blur Distance**: 20
   - **Blur Iterations**: 2-3
   - **Downsample**: 2 (performance)

3. **Test:**
   - Enter Play Mode
   - Far objects should blur

**Pros:** Quick setup, no post-processing needed  
**Cons:** Lower quality than post-processing

---

### Option 3: Camera Controller Integration

**Automatic setup via camera controller:**

1. **Select Camera:**
   - Find `TopDownCameraController3D` component

2. **Enable Depth Blur:**
   - Check **Enable Depth Blur**
   - Set **Focus Distance**: 5
   - Set **Blur Intensity**: 2-4

3. **Script auto-adds `TopDownDepthBlur` component**

**Pros:** Integrated with camera system  
**Cons:** Same quality as Option 2

---

## Recommended Settings

**For classic Game Boy top-down:**

```
Focus Distance: 5 units
Blur Intensity: 2-3
Max Blur Distance: 15-20 units
Blur Iterations: 2
Downsample: 2
```

**Adjust based on:**
- **Camera zoom level** - Higher zoom = increase focus distance
- **Scene scale** - Larger scenes = increase max blur distance
- **Performance** - Lower iterations/downsample if needed

---

## Troubleshooting

### Blur not working?

1. **Check camera depth texture:**
   - Camera → Depth Texture Mode = Depth
   - Or add `TopDownDepthBlur` (sets automatically)

2. **Verify URP setup:**
   - Project Settings → Graphics → Scriptable Render Pipeline Settings
   - Should have URP Asset assigned

3. **Check post-processing:**
   - If using Option 1, verify Volume is Global or assigned to camera
   - Check Volume Profile has Depth of Field effect

### Performance issues?

- Reduce **Blur Iterations** (2 is usually enough)
- Increase **Downsample** (3-4 for better performance)
- Disable blur on low-end devices

### Blur too strong/weak?

- Adjust **Blur Intensity** (lower = subtle, higher = strong)
- Adjust **Focus Distance** (closer = more objects blurred)
- Adjust **Max Blur Distance** (farther = blur extends more)

---

## Alternative: Distance-Based Material Blur

For pixel art, you might prefer:
- Different materials for near/far objects
- Far objects use slightly blurred textures
- More control, but requires manual setup per object

