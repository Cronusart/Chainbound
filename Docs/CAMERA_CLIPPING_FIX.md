# Camera Clipping Fix - Top-Down View

## Problem

Near-side objects (like building fronts) are being clipped by the camera, making them invisible or partially cut off. This breaks the classic Game Boy top-down aesthetic where all objects should be visible.

## Solution

The camera's **near clipping plane** was too far from the camera (Unity default is 0.3), causing objects close to the camera to be clipped.

## What Was Fixed

1. **Added clipping plane settings to CameraProfile**:
   - `nearClipPlane`: Set to 0.01 (very close to camera)
   - `farClipPlane`: Set to 1000 (sufficient for most scenes)

2. **Applied clipping planes automatically**:
   - Clipping planes are set when profile is applied
   - Default values are set immediately in `Awake()` to prevent clipping before profile loads

## How to Use

### Option 1: Use Default Values (Recommended)

The code now sets default clipping planes automatically:
- Near: 0.01
- Far: 1000

These should work for most top-down scenes. **No action needed** - just test your scene!

### Option 2: Adjust in Camera Profile

If you need to fine-tune:

1. Select your `CameraProfile` ScriptableObject
2. In Inspector, find **Projection** section
3. Adjust:
   - **Near Clip Plane**: Lower values (0.001-0.1) = see objects closer to camera
   - **Far Clip Plane**: Higher values (500-2000) = see objects farther away

### Option 3: Adjust in Camera Component (Manual Override)

If you need to override per-camera:

1. Select Camera GameObject
2. In Camera component, find **Clipping Planes**
3. Set:
   - **Near**: 0.01 (or lower)
   - **Far**: 1000 (or higher)

**Note**: The script will override these values when a profile is applied, so adjust the profile instead.

## Recommended Settings

For classic Game Boy top-down view:

```
Near Clip Plane: 0.01
Far Clip Plane: 1000
Pitch Angle: 45 degrees
Orthographic: Yes
```

## Troubleshooting

### Still seeing clipping?

1. **Check camera position**: Make sure camera offset isn't too close to objects
   - Default offset: (0, 10, -10) should work well
   - If objects are very close to camera, increase Y offset

2. **Check object positions**: Make sure objects aren't positioned at Y=0 or below camera
   - Camera rig root should be at ground level or slightly above
   - Objects should be positioned above ground

3. **Check pitch angle**: 45 degrees is optimal for top-down
   - Too steep (>60) can cause clipping
   - Too shallow (<30) loses top-down feel

4. **Verify profile is applied**: Check console for errors
   - Profile should load in `Awake()`
   - Clipping planes are set immediately

### Objects still invisible?

- Check if objects are on a layer that's excluded from camera culling
- Verify objects have renderers and materials
- Check if occluder fading is interfering (should only fade objects behind player)

## Technical Details

**Why 0.01 for near plane?**
- Unity's default is 0.3, which clips objects within 0.3 units of camera
- For top-down views, we want to see objects very close to camera
- 0.01 is safe and prevents clipping while maintaining depth precision

**Why 1000 for far plane?**
- Sufficient for most game scenes
- Can be increased if you have very large levels
- Too high (>5000) can cause precision issues

