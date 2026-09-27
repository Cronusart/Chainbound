# 3D Camera Setup Guide

This guide explains how to set up the 3D top-down camera system with rig hierarchy, dynamic zoom, and camera moments.

## Overview

The 3D camera system provides:
- Rig hierarchy for clean separation of follow/rotation/zoom
- 45° pitch top-down view (configurable)
- Dynamic zoom based on player movement state
- Trigger-driven camera moments (zones and rotations)
- Camera profiles for easy switching (Explore, Battle, etc.)
- Mobile-optimized (no allocations, cached references)

## Step 1: Create Camera Profile Asset

1. In Unity, go to `Assets > Create > SignalJam > Camera > Camera Profile`
2. Name it `ExploreProfile` (or `DefaultProfile`)
3. Configure settings:
   - **Is Orthographic**: `true` (for pixel-art look) or `false` (for perspective)
   - **Default Zoom**: `5` (orthographic size or FOV baseline)
   - **Min Zoom**: `1`
   - **Max Zoom**: `10`
   - **Pitch Angle**: `45` (top-down angle)
   - **Follow Offset**: `(0, 10, -10)` (adjust for your scene)
   - **Follow Smooth Time**: `0.3`
   - **Use Smooth Damp**: `true`
   - **Yaw Rotate Time**: `0.2`
   - **Zoom Smooth Time**: `0.5`
   - **Walk Zoom**: `5` (baseline)
   - **Sprint Zoom**: `7` (zoomed out)
   - **Idle Zoom**: `4.5` (slightly zoomed in)
   - **Idle Threshold**: `0.1` (speed for idle state)
   - **Sprint Threshold**: `2.5` (speed for sprint state)

4. Save the asset

## Step 2: Create Camera Rig Hierarchy

The camera system automatically creates the rig hierarchy, but you can also set it up manually:

### Manual Setup (Optional)

1. **Create CameraRigRoot**:
   - Right-click in Hierarchy → `Create Empty` → Rename to `CameraRigRoot`
   - Position: `(0, 10, 0)` (adjust height as needed)

2. **Create CameraYawPivot**:
   - Right-click CameraRigRoot → `Create Empty` → Rename to `CameraYawPivot`
   - Local Position: `(0, 0, 0)`
   - Local Rotation: `(0, 0, 0)`

3. **Create CameraPitchPivot**:
   - Right-click CameraYawPivot → `Create Empty` → Rename to `CameraPitchPivot`
   - Local Position: `(0, 0, 0)`
   - Local Rotation: `(45, 0, 0)` (45° pitch)

4. **Add Main Camera**:
   - Right-click CameraPitchPivot → `Camera`
   - Rename to `Main Camera`
   - Local Position: `(0, 10, -10)` (adjust offset as needed)
   - Local Rotation: `(0, 0, 0)`

### Automatic Setup (Recommended)

1. Create a Camera GameObject in scene
2. Add `TopDownCameraController3D` component
3. The script will automatically create the rig hierarchy on `Awake()`

## Step 3: Configure Camera Component

### Camera Settings

1. **Projection**:
   - **Orthographic** (for pixel-art): Set `Is Orthographic` in profile
   - **Perspective** (for 3D look): Uncheck `Is Orthographic` in profile

2. **Clipping Planes** (mobile optimization):
   - **Near**: `0.1`
   - **Far**: `50` (reduce if possible for better performance)

3. **Culling Mask**:
   - Set to exclude layers you don't want to render
   - Example: Exclude UI layer if using separate UI camera

4. **Clear Flags**:
   - **Skybox** (for perspective) or **Solid Color** (for orthographic)

5. **Background Color**:
   - Set to match your game's background

## Step 4: Configure TopDownCameraController3D

### Component Setup

1. **Target**: Drag player GameObject (or leave null to auto-find by "Player" tag)
2. **Target Rb**: Drag player's Rigidbody component (optional, for velocity-based zoom)
3. **Default Profile**: Assign `ExploreProfile` asset
4. **Current Profile**: Will be set automatically from default

### Boundaries (Optional)

1. **Use Boundaries**: Check if you want camera limits
2. **Min XZ**: `(-50, -50)` (minimum X and Z positions)
3. **Max XZ**: `(50, 50)` (maximum X and Z positions)

### Pixel Perfect (Optional)

1. **Use Pixel Snap**: Check for retro-style snapping
2. **Pixels Per Unit**: `16` (match your sprite/texture PPU)

### Manual Override (Optional)

- **Use Manual Movement Mode**: Check to override velocity detection
- **Manual Movement Mode**: Select `Idle`, `Walk`, or `Sprint`

## Step 5: Player Setup

### Player GameObject Requirements

1. **Tag**: Must be `Player` (for auto-detection)
2. **Rigidbody** (3D): Required for velocity-based zoom
   - Assign to `Target Rb` field in camera controller
   - Camera reads `linearVelocity.magnitude` to determine movement state

### Movement Speed Thresholds

Configure in Camera Profile:
- **Idle Threshold**: Speed below this = idle zoom
- **Sprint Threshold**: Speed above this = sprint zoom
- Between thresholds = walk zoom

### Manual Control (Alternative)

If you prefer event-driven zoom:

```csharp
// In PlayerMovement3D or similar
TopDownCameraController3D cameraController;

void OnSprintStart()
{
    cameraController?.SetMovementMode(CameraMovementMode.Sprint);
}

void OnSprintEnd()
{
    cameraController?.ClearMovementMode(); // Re-enable velocity detection
}
```

## Step 6: Create Camera Zone Triggers

### Zone Setup

1. **Create Trigger Volume**:
   - Right-click in Hierarchy → `3D Object > Cube` (or other shape)
   - Rename to `CameraZone_Example`
   - Scale to desired trigger size

2. **Configure Collider**:
   - **Is Trigger**: `true` (required)
   - **Size**: Adjust to cover desired area

3. **Add CameraZoneTrigger Component**:
   - Add Component: `CameraZoneTrigger`
   - **Camera Controller**: Drag `TopDownCameraController3D` (or leave null to auto-find)

### Zone Configuration Options

1. **Zone Profile**:
   - Assign a different `CameraProfile` asset for this zone
   - Camera will blend to this profile when player enters

2. **Zoom Override**:
   - Set `Zoom Override` > 0 to override profile zoom
   - Example: `8` for wide view in open areas

3. **Yaw Rotation Steps**:
   - Set to rotate camera horizontally
   - `1` = 45° clockwise, `-1` = 45° counterclockwise
   - `2` = 90° rotation, etc.

4. **Offset Adjustment**:
   - Adjust camera offset in this zone
   - Example: `(0, 5, -5)` for closer framing

5. **Boundaries Override**:
   - Check `Override Boundaries` to set zone-specific limits
   - Set `Zone Min XZ` and `Zone Max XZ`

6. **Exit Behavior**:
   - **Restore Previous**: Revert to profile before entering zone
   - **Restore Profile**: Revert to specific profile
   - **Do Nothing**: Keep zone settings

### Example Zone Use Cases

**Wide View Zone** (open area):
- Zoom Override: `8`
- Yaw Rotation: `0`
- Exit: Restore Previous

**Corner Turn Zone** (rotate camera):
- Zone Profile: (same as default)
- Yaw Rotation Steps: `1` (rotate 45°)
- Exit: Do Nothing (keep rotation)

**Boss Arena Zone**:
- Zone Profile: `BossArenaProfile` (different angle/zoom)
- Exit: Restore Profile → `ExploreProfile`

## Step 7: Battle Camera Profile (Future)

### Create Battle Profile

1. Create new Camera Profile: `BattleProfile`
2. Configure for battle view:
   - **Pitch Angle**: `60` (more angled)
   - **Default Zoom**: `3` (closer)
   - **Follow Offset**: `(0, 5, -5)` (closer framing)
   - **Follow Smooth Time**: `0.2` (faster)

### Switching to Battle Camera

In your Battle scene or BattleManager:

```csharp
TopDownCameraController3D cameraController;
[SerializeField] private CameraProfile battleProfile;

void StartBattle()
{
    cameraController.ApplyProfile(battleProfile, 0.5f); // Blend over 0.5 seconds
}

void EndBattle()
{
    cameraController.ResetToDefaultProfile(0.5f);
}
```

### Multiple Camera Setup (Advanced)

For completely separate cameras:

1. Create second camera GameObject with `TopDownCameraController3D`
2. Assign `BattleProfile` as default
3. Enable/disable cameras as needed:

```csharp
exploreCamera.gameObject.SetActive(false);
battleCamera.gameObject.SetActive(true);
```

## Step 8: Testing Checklist

### Basic Functionality

- [ ] Camera follows player smoothly on XZ plane
- [ ] Camera maintains stable height (Y position)
- [ ] Default view is ~45° pitch top-down
- [ ] Zoom works (orthographic or perspective)

### Dynamic Zoom

- [ ] Sprinting zooms out smoothly
- [ ] Walking returns to baseline zoom
- [ ] Idle zooms in slightly (if configured)
- [ ] Zoom transitions are smooth (no popping)

### Camera Moments

- [ ] Trigger zones apply profile/zoom correctly
- [ ] Yaw rotations are smooth and snap to 45° increments
- [ ] Exit behavior works (restore previous/do nothing)
- [ ] Multiple zones can be chained

### Boundaries

- [ ] Camera respects XZ boundaries
- [ ] Boundaries work with pixel snap
- [ ] Zone-specific boundaries override global

### Performance

- [ ] No GC allocations per frame (check Profiler)
- [ ] Smooth 60fps on target device
- [ ] No jitter or stuttering

## Troubleshooting

### Camera Not Following

1. Check `Target` is assigned or player has "Player" tag
2. Check `Default Profile` is assigned
3. Check camera rig hierarchy was created (should see CameraRigRoot in Hierarchy)
4. Enable debug logs to see follow updates

### Zoom Not Changing

1. Check `Target Rb` is assigned (for velocity-based zoom)
2. Check movement thresholds in profile (idle/sprint)
3. Check player speed is above thresholds
4. Try manual override: `Use Manual Movement Mode` = true

### Yaw Rotation Not Working

1. Check `Yaw Pivot` exists in rig hierarchy
2. Check rotation steps are non-zero
3. Check `Yaw Rotate Time` in profile is reasonable (0.15-0.25s)
4. Enable debug logs to see rotation updates

### Trigger Zones Not Activating

1. Check collider `Is Trigger` = true
2. Check player has "Player" tag
3. Check `Camera Controller` is assigned (or auto-find works)
4. Check zone is on correct layer (not excluded by camera culling)

### Pixel Snap Causing Jitter

1. Reduce `Pixels Per Unit` (larger snap increments)
2. Increase `Follow Smooth Time` in profile
3. Disable pixel snap if not needed (`Use Pixel Snap` = false)

### Material/Shader Issues

- Camera system doesn't modify materials
- For occlusion fading, see `FadeOccludersBetweenCameraAndPlayer` setup guide

## Performance Tips

### Mobile Optimization

1. **Update Frequency**:
   - Camera updates in `LateUpdate` (after player movement)
   - No per-frame allocations

2. **Zoom Smoothing**:
   - Default `Zoom Smooth Time` = 0.5s
   - Reduce to 0.3s for snappier feel
   - Increase to 0.8s for smoother transitions

3. **Follow Smoothing**:
   - Default `Follow Smooth Time` = 0.3s
   - Reduce for tighter follow
   - Increase for smoother, more cinematic feel

4. **Boundaries**:
   - Only enable if needed
   - Reduces calculations per frame

5. **Pixel Snap**:
   - Disable if not needed (saves calculations)
   - Use larger `Pixels Per Unit` for less frequent snapping

### Profile Switching

- Use `blendTime` > 0 for smooth transitions
- Use `blendTime` = 0 for instant switches (battle transitions)
- Limit profile switches (don't switch every frame)

## Advanced Usage

### Custom Camera Moments

Create custom camera behaviors via code:

```csharp
TopDownCameraController3D camera;

// Quick 90° rotation
camera.RotateYawSteps(2, false);

// Instant zoom to specific level
camera.SetZoomTarget(8f);

// Manual movement mode override
camera.SetMovementMode(CameraMovementMode.Sprint);
camera.ClearMovementMode(); // Re-enable auto-detection
```

### Scripted Camera Sequences

```csharp
IEnumerator CameraSequence()
{
    // Rotate 45°
    camera.RotateYawSteps(1, false);
    yield return new WaitForSeconds(0.3f);
    
    // Zoom out
    camera.SetZoomTarget(8f);
    yield return new WaitForSeconds(1f);
    
    // Restore
    camera.SetZoomTarget(5f);
    camera.RotateYawSteps(-1, false);
}
```

## Migration Notes

### From 2D CameraFollow

- **Follow**: Now uses XZ plane instead of XY
- **Zoom**: Works for both orthographic and perspective
- **Boundaries**: Now XZ instead of XY
- **Pixel Perfect**: Now XZ snapping (retro-style, not true pixel-perfect 3D)
- **Offset**: Now 3D Vector3 instead of 2D Vector3

### Removed Features

- 2D-specific pixel perfect (replaced with retro snap)
- Direct orthographic size control (now via profile)

### New Features

- Rig hierarchy for clean rotation/zoom separation
- Dynamic zoom based on movement state
- Camera profiles for easy switching
- Trigger zones for camera moments
- 45° yaw rotation system

## Next Steps

After setup:

1. Test all camera behaviors
2. Create additional profiles for different areas
3. Set up trigger zones for key moments
4. Fine-tune zoom thresholds for your movement speeds
5. Prepare Battle profile for combat scenes
6. Test on target mobile device for performance

