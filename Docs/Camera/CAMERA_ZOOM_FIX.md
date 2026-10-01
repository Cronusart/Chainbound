# Camera Zoom and FOV Fix

## Issues Fixed

1. **Black Screen on Startup**: Camera FOV was not initialized immediately, causing black screen until FOV slider was moved
2. **FOV Set to 90**: Updated default FOV from 30 to 90 for wider field of view
3. **Inverted Zoom Behavior**: Fixed zoom logic so idle = zoomed IN, sprint = zoomed OUT

## Changes Made

### CameraProfile.cs
- **FOV Range**: Changed from `[Range(10f, 60f)]` to `[Range(30f, 120f)]` with default `90f`
- **Zoom Values**: Updated defaults to match desired behavior:
  - `idleZoom = 1f` (zoomed IN - closer view)
  - `walkZoom = 5f` (baseline - medium view)
  - `sprintZoom = 10f` (zoomed OUT - wider view)

### TopDownCameraController3D.cs

#### 1. Immediate FOV Initialization (Awake)
- Set FOV immediately in `Awake()` before profile is applied
- Prevents black screen on startup
- Fallback to 90 FOV if no profile exists

#### 2. FOV Initialization (Start)
- Ensure FOV is set in `Start()` as well
- Added fallback check for invalid FOV values

#### 3. Fixed Zoom Logic (ApplyZoom)
- **Previous (WRONG)**: `Mathf.Lerp(maxFOV, minFOV, normalizedZoom)` 
  - This made idle (low zoom) = high FOV (zoomed out) ❌
- **Fixed (CORRECT)**: `Mathf.Lerp(minFOV, maxFOV, normalizedZoom)`
  - Now idle (low zoom) = low FOV (zoomed in) ✅
  - Sprint (high zoom) = high FOV (zoomed out) ✅

#### 4. FOV Range Calculation
- Base FOV: 90
- Idle FOV: 90 * 0.6 = 54 (zoomed in)
- Sprint FOV: 90 * 1.4 = 126 (zoomed out, clamped to 120)
- Walk FOV: Interpolated between idle and sprint based on zoom value

## How It Works Now

### Zoom Behavior
- **Idle** (speed < 0.1): 
  - Zoom value: 1
  - FOV: ~54 (zoomed IN - closer view)
  
- **Walk** (speed 0.1-2.5):
  - Zoom value: 5
  - FOV: ~90 (baseline)
  
- **Sprint** (speed >= 2.5):
  - Zoom value: 10
  - FOV: ~120 (zoomed OUT - wider view)

### Player Movement Speeds
Your current speeds work well with these zoom levels:
- Move Speed: 7.8
- Sprint Speed Multiplier: 1.8
- Sprint Speed: 7.8 * 1.8 = 14.04

The camera will automatically detect sprint when speed exceeds `sprintThreshold` (default 2.5).

## Testing

1. **Enter Play Mode** - should see scene immediately (no black screen)
2. **Stand Still** - camera should zoom IN (closer view)
3. **Walk** - camera should zoom to medium level
4. **Sprint** - camera should zoom OUT (wider view)

## Adjusting Zoom Levels

If you want to fine-tune the zoom behavior, edit your `CameraProfile` asset:

1. Select your Camera Profile asset in Project window
2. Adjust these values:
   - `idleZoom`: Lower = more zoomed in when idle
   - `walkZoom`: Baseline zoom level
   - `sprintZoom`: Higher = more zoomed out when sprinting
3. Adjust `sprintThreshold` if sprint detection is too sensitive/insensitive

## Troubleshooting

### Still seeing black screen?
- Check that your Camera Profile has `isOrthographic = false`
- Verify `fieldOfView = 90` in the profile
- Check Console for any errors

### Zoom not working?
- Verify player movement speeds match thresholds
- Check `sprintThreshold` in Camera Profile (default 2.5)
- Ensure `TopDownCameraController3D` component is on Main Camera

### Zoom feels wrong?
- Adjust `idleZoom`, `walkZoom`, `sprintZoom` values in Camera Profile
- Lower values = more zoomed in
- Higher values = more zoomed out

