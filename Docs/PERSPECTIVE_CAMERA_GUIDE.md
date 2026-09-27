# Perspective Camera Guide - Cassette Beasts Style

## Overview

Switch from orthographic to perspective camera for a hybrid top-down/3D feel, similar to Cassette Beasts.

## Key Differences

**Orthographic (Current):**
- No depth perspective
- Objects same size regardless of distance
- Classic Game Boy feel

**Perspective (New Option):**
- Depth perspective (objects farther = smaller)
- More 3D feel
- Hybrid classic/modern aesthetic

## Setup

### Option 1: Via Camera Profile (Recommended)

1. **Select your CameraProfile ScriptableObject**
2. **In Inspector:**
   - **Is Orthographic**: Uncheck (switches to perspective)
   - **Field Of View**: 25-35 (lower = more zoomed, classic feel)
     - `25` = Very zoomed in, classic top-down
     - `30` = Balanced (recommended)
     - `35` = More 3D feel
3. **Save the profile**

### Option 2: Via Camera Controller Inspector

1. **Select Camera GameObject**
2. **Find `TopDownCameraController3D` component**
3. **Select Camera Profile**
4. **Edit profile** (see Option 1)

## Field of View Explained

**FOV (Field of View)** controls how "wide" the camera sees:

- **Lower FOV (20-30)**: 
  - More zoomed in
  - Narrower view
  - Classic top-down feel
  - Objects appear larger

- **Higher FOV (40-60)**:
  - More zoomed out
  - Wider view
  - More 3D feel
  - Objects appear smaller

**For Cassette Beasts style:** Use 25-30 FOV

## Zoom Behavior

In perspective mode, zoom works differently:

- **Zoom In**: Lowers FOV (narrows view)
- **Zoom Out**: Raises FOV (widens view)
- **Sprint Zoom**: Automatically widens FOV when sprinting

## Recommended Settings

**For Cassette Beasts hybrid feel:**

```
Camera Profile:
- Is Orthographic: false
- Field Of View: 28-30
- Pitch Angle: 45 degrees
- Follow Offset: (0, 10, -10)

Zoom Settings:
- Walk Zoom: 5 (maps to ~28 FOV)
- Sprint Zoom: 7 (maps to ~35 FOV)
- Idle Zoom: 4.5 (maps to ~25 FOV)
```

## Testing

1. **Switch profile to perspective**
2. **Enter Play Mode**
3. **Observe depth perception:**
   - Far objects should appear smaller
   - Near objects should appear larger
   - Movement should feel more 3D

4. **Adjust FOV** if needed:
   - Too zoomed in → Increase FOV
   - Too zoomed out → Decrease FOV
   - Want more classic feel → Lower FOV (25-28)
   - Want more 3D feel → Higher FOV (32-35)

## Comparison

| Setting | Orthographic | Perspective (FOV 30) |
|---------|-------------|---------------------|
| Depth Perception | None | Subtle |
| Object Scaling | Same size | Distance-based |
| Classic Feel | High | Medium |
| Modern Feel | Low | High |
| Best For | Pure pixel art | Hybrid aesthetic |

## Tips

1. **Start with FOV 30** - Good balance
2. **Adjust based on scene scale** - Larger scenes may need higher FOV
3. **Test with player movement** - Ensure it feels right
4. **Consider camera height** - May need adjustment for perspective
5. **Keep pitch at 45 degrees** - Maintains top-down feel

## Troubleshooting

### Perspective looks too flat?

- Lower FOV (25-28)
- Adjust camera height/offset
- Check pitch angle (should be 45)

### Perspective too 3D/distorted?

- Lower FOV (20-25)
- Or switch back to orthographic

### Objects clipping in perspective?

- Check near clip plane (should be 0.01)
- Adjust camera offset if needed

### Zoom not working correctly?

- Verify zoom settings in profile
- Check that FOV range is appropriate (10-60)

