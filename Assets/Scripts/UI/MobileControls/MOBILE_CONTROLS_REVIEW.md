# Mobile Controls Review & Implementation Guide

Complete review of mobile controls system with Game Boy-style D-pad + buttons, PlaySolana PSG1 device prioritization, and sprint support.

---

## Overview

The mobile control system provides:
1. **Game Boy-style D-pad** (VirtualDPad) - Discrete directional input
2. **Game Boy-style buttons** (GameboyButton) - Tactile button feedback
3. **PlaySolana PSG1 device support** - Automatic detection and prioritization
4. **Sprint functionality** - Integrated with mobile controls and camera zoom
5. **Input priority system** - PSG1 Device > Mobile Virtual Controls > Keyboard/Gamepad

---

## Input Priority System

The system uses a **three-tier priority** for all inputs:

### Priority 1: PlaySolana PSG1 Device (Highest)
- **Detection**: Automatic via `PSG1.current` (PlaySolana SDK) or device name matching
- **Behavior**: Virtual controls are **automatically hidden** when PSG1 is detected
- **Input Source**: Unity Input System directly (physical D-pad and buttons)
- **Sprint**: Uses Input System "Sprint" action (physical button)

### Priority 2: Mobile Virtual Controls
- **Activation**: When `IsMobileControlsActive` is true and PSG1 is not connected
- **Components**: 
  - VirtualDPad or VirtualJoystick for movement
  - GameboyButton for actions (X, Y, A, B, Start, Select, Sprint, Solana)
- **Input Source**: Mobile UI touch input
- **Sprint**: Dedicated sprint button (GameboyButton)

### Priority 3: Keyboard/Gamepad (Fallback)
- **Activation**: When mobile controls are inactive
- **Input Source**: Unity Input System (keyboard/gamepad bindings)
- **Sprint**: Keyboard (Left Shift) or Gamepad button

---

## Component Architecture

### MobileInputManager
**Location**: `Assets/Scripts/UI/MobileControls/MobileInputManager.cs`

**Responsibilities**:
- Detects PlaySolana PSG1 device
- Manages mobile control visibility
- Provides unified input interface (`GetMovementInput()`, `IsSprinting()`)
- Handles button events (X, Y, A, B, Start, Select, Sprint, Solana)
- Locks screen orientation to landscape

**Key Methods**:
- `GetMovementInput()` - Returns Vector2 movement input (prioritized)
- `IsSprinting()` - Returns bool sprint state (prioritized)
- `SetMobileControlsEnabled(bool)` - Enable/disable mobile controls
- `IsMobileControlsActive` - Property indicating if mobile controls are active
- `IsPlaySolanaDevice` - Property indicating if PSG1 is connected

**Settings**:
- `enableMobileControls` - Master toggle
- `autoDetectPlatform` - Auto-detect mobile platforms
- `preferDPadOverJoystick` - Use D-pad when both are assigned
- `enableDebugLogs` - Debug logging toggle

### VirtualDPad
**Location**: `Assets/Scripts/UI/MobileControls/VirtualDPad.cs`

**Features**:
- Discrete directional buttons (Up, Down, Left, Right)
- Game Boy-style input (no diagonals by default)
- Visual feedback (color change + scale animation)
- Auto-finds child buttons by name

**Settings**:
- `allowDiagonalInput` - Enable/disable diagonal movement (default: false)
- `inputDeadzone` - Minimum input threshold
- `pressScale` - Scale when button is pressed
- `normalColor` / `pressedColor` - Button colors

### VirtualJoystick
**Location**: `Assets/Scripts/UI/MobileControls/VirtualJoystick.cs`

**Features**:
- Circular touch-based joystick
- Smooth analog input
- Visual handle movement
- Snap-to-center option

**Settings**:
- `joystickRange` - Movement range in pixels
- `snapToCenter` - Snap handle to center on release
- `normalizeInput` - Normalize input vector

### GameboyButton
**Location**: `Assets/Scripts/UI/MobileControls/GameboyButton.cs`

**Features**:
- Tactile button press/release feedback
- Color and scale animations
- Audio feedback support
- Pointer exit handling (releases if finger moves outside)

**Events**:
- `OnButtonDown` - Fired when button is pressed
- `OnButtonUp` - Fired when button is released

**Settings**:
- `normalColor` / `pressedColor` - Button colors
- `pressedScale` - Scale when pressed
- `pressAnimationDuration` - Animation duration
- `pressSound` / `releaseSound` - Audio clips

---

## Sprint Integration

### Mobile Sprint Button
1. **Create Sprint Button**:
   - Add GameboyButton component to a UI Button
   - Position in mobile control layout (recommended: near D-pad or action buttons)
   - Assign to `MobileInputManager.sprintButton` field

2. **Button Behavior**:
   - **Press**: Starts sprint (hold to maintain)
   - **Release**: Stops sprint
   - Automatically hidden when PSG1 device is connected

### PlayerMovement3D Integration
The `PlayerMovement3D` script automatically checks sprint state from:
1. Mobile sprint button (if mobile controls active)
2. Input System sprint action (keyboard/gamepad/PSG1)

**Sprint Effects**:
- Movement speed multiplied by `sprintSpeedMultiplier` (default: 1.8x)
- Camera zooms out via `SetMovementMode(CameraMovementMode.Sprint)`
- Camera zooms back when sprint ends

### Camera Synchronization
- Sprint triggers `TopDownCameraController3D.SetMovementMode(CameraMovementMode.Sprint)`
- Uses `sprintZoom` value from Camera Profile
- Smooth zoom transition via `zoomSmoothTime`

---

## Button Layout (PlaySolana Style)

### Recommended Layout

```
        [Solana Wallet]
        
[Start] [Select]

[Y] [X]        [Sprint]
[A] [B]

    [D-Pad]
```

### Button Functions

**Action Buttons (Bottom Right)**:
- **X**: Back / Cancel
- **Y**: In-game menu / Roster
- **A**: Interact 2 / Attack
- **B**: Interact (primary)

**System Buttons (Bottom Middle)**:
- **Start**: Main menu
- **Select**: Quick menu / Roster

**Movement Button**:
- **Sprint**: Hold to sprint (near D-pad or action buttons)

**Wallet Button (Top Middle)**:
- **Solana**: Wallet connection / info

**Movement Control (Bottom Left)**:
- **D-Pad**: Movement (Up, Down, Left, Right)

---

## Setup Checklist

### 1. Input Actions Asset
- [ ] Create `InputSystem_Actions` asset
- [ ] Create "Player" action map
- [ ] Add "Move" action (Vector2)
- [ ] Add "Sprint" action (Button)
- [ ] Add "Interact", "Attack", "Cancel" actions
- [ ] Place in `Assets/Resources/` folder (optional)

### 2. UI Canvas Setup
- [ ] Create Canvas (UI > Canvas)
- [ ] Set Canvas Scaler to "Scale With Screen Size"
- [ ] Set Reference Resolution (e.g., 1920x1080)
- [ ] Ensure EventSystem exists in scene

### 3. Virtual D-Pad Setup
- [ ] Create D-Pad GameObject under Canvas
- [ ] Add VirtualDPad component
- [ ] Create 4 child buttons: Up, Down, Left, Right
- [ ] Add Image components to buttons
- [ ] Enable "Raycast Target" on images
- [ ] Assign buttons to VirtualDPad fields

### 4. Game Boy-Style Buttons
- [ ] Create X, Y, A, B buttons (bottom right)
- [ ] Create Start, Select buttons (bottom middle)
- [ ] Create Sprint button (near D-pad)
- [ ] Create Solana button (top middle)
- [ ] Remove default Button components
- [ ] Add GameboyButton component to each
- [ ] Assign Image components
- [ ] Customize colors and scale

### 5. MobileInputManager Setup
- [ ] Create MobileInputManager GameObject
- [ ] Add MobileInputManager component
- [ ] Assign VirtualDPad reference
- [ ] Assign all GameboyButton references
- [ ] Set `preferDPadOverJoystick` to true
- [ ] Configure settings

### 6. PlayerMovement3D Integration
- [ ] Select Player GameObject
- [ ] Assign MobileInputManager to `mobileInputManager` field
- [ ] Assign Input Actions asset to `inputActions` field
- [ ] Verify sprint works (mobile button + keyboard)

### 7. Camera Setup
- [ ] Ensure Camera Profile has `sprintZoom` configured
- [ ] Verify `sprintThreshold` is appropriate
- [ ] Test camera zoom on sprint

---

## Testing Guide

### Test Priority System

1. **PSG1 Device Priority**:
   - [ ] Connect PSG1 device
   - [ ] Verify virtual controls are hidden
   - [ ] Test movement with physical D-pad
   - [ ] Test sprint with physical button
   - [ ] Check console for "[MobileInputManager] PlaySolana PSG1 device detected"

2. **Mobile Controls Priority**:
   - [ ] Disconnect PSG1 device
   - [ ] Enable mobile controls (`enableMobileControls = true`)
   - [ ] Verify virtual controls are visible
   - [ ] Test movement with D-pad
   - [ ] Test sprint with sprint button
   - [ ] Verify buttons work (X, Y, A, B, Start, Select, Solana)

3. **Keyboard/Gamepad Fallback**:
   - [ ] Disable mobile controls
   - [ ] Test movement with WASD/Arrow keys
   - [ ] Test sprint with Left Shift
   - [ ] Verify Input System actions work

### Test Sprint Integration

1. **Mobile Sprint**:
   - [ ] Press sprint button
   - [ ] Verify player moves faster
   - [ ] Verify camera zooms out
   - [ ] Release sprint button
   - [ ] Verify player returns to normal speed
   - [ ] Verify camera zooms back

2. **PSG1 Sprint**:
   - [ ] Connect PSG1 device
   - [ ] Press physical sprint button
   - [ ] Verify sprint works correctly

3. **Keyboard Sprint**:
   - [ ] Press Left Shift
   - [ ] Verify sprint works correctly

### Test Button Functions

- [ ] **X Button**: Triggers Cancel action or returns to previous screen
- [ ] **Y Button**: Opens Roster scene
- [ ] **A Button**: Triggers Attack action
- [ ] **B Button**: Triggers Interact action
- [ ] **Start Button**: Opens Main Menu scene
- [ ] **Select Button**: Opens Roster scene
- [ ] **Solana Button**: Connects/displays wallet

---

## Troubleshooting

### Virtual Controls Not Showing

**Problem**: Controls are hidden when they should be visible.

**Solutions**:
1. Check `enableMobileControls` is true
2. Check `autoDetectPlatform` is true (or manually enable)
3. Verify PSG1 device is not connected (disconnects virtual controls)
4. Check `IsMobileControlsActive` property in code
5. Verify platform detection (mobile platforms only)

### Sprint Not Working

**Problem**: Sprint doesn't activate.

**Solutions**:
1. Verify sprint button is assigned in MobileInputManager
2. Check sprint button has GameboyButton component
3. Verify Input Actions has "Sprint" action
4. Check PlayerMovement3D has MobileInputManager assigned
5. Test with keyboard (Left Shift) to verify Input System works
6. Check console for errors

### PSG1 Device Not Detected

**Problem**: PSG1 device connected but not detected.

**Solutions**:
1. Verify PlaySolana SDK is imported
2. Check `PSG1.current` is not null
3. Verify device name contains "psg1" or "playsolana"
4. Check Input System device list
5. Enable debug logs to see detection messages

### Input Priority Issues

**Problem**: Wrong input source is being used.

**Solutions**:
1. Check `IsMobileControlsActive` property
2. Verify `IsPlaySolanaDevice` property
3. Review input priority logic in `GetMovementInput()` and `IsSprinting()`
4. Ensure MobileInputManager is assigned to PlayerMovement3D
5. Check Input Actions asset is assigned

### Button Not Responding

**Problem**: Button press doesn't trigger action.

**Solutions**:
1. Verify button has Image component
2. Check "Raycast Target" is enabled on Image
3. Ensure EventSystem exists in scene
4. Verify button is child of Canvas
5. Check GameboyButton component is attached
6. Verify button is assigned in MobileInputManager
7. Check button event handlers are subscribed

---

## Code Examples

### Checking Sprint State

```csharp
// In any script
MobileInputManager mobileInput = FindObjectOfType<MobileInputManager>();
if (mobileInput != null)
{
    bool isSprinting = mobileInput.IsSprinting();
    // Use sprint state...
}
```

### Programmatically Triggering Sprint

```csharp
// Get sprint button
GameboyButton sprintButton = FindObjectOfType<GameboyButton>();
if (sprintButton != null)
{
    sprintButton.Press(); // Start sprint
    // Later...
    sprintButton.Release(); // Stop sprint
}
```

### Checking Input Priority

```csharp
MobileInputManager mobileInput = FindObjectOfType<MobileInputManager>();
if (mobileInput != null)
{
    if (mobileInput.IsPlaySolanaDevice)
    {
        Debug.Log("Using PSG1 device input");
    }
    else if (mobileInput.IsMobileControlsActive)
    {
        Debug.Log("Using mobile virtual controls");
    }
    else
    {
        Debug.Log("Using keyboard/gamepad input");
    }
}
```

---

## Performance Considerations

### Mobile Optimization

1. **Update Frequency**: Mobile controls update every frame, but occlusion fading uses intervals
2. **Raycast Optimization**: Buttons use UI raycasting (handled by Unity efficiently)
3. **Material Instancing**: GameboyButton uses MaterialPropertyBlock to avoid material instancing
4. **Device Detection**: PSG1 detection runs every 60 frames (~1 second) to reduce overhead

### Best Practices

1. **Disable Unused Controls**: Hide controls when not needed (PSG1 connected)
2. **Pool Buttons**: Reuse button GameObjects if creating dynamically
3. **Limit Debug Logs**: Disable `enableDebugLogs` in production
4. **Optimize Canvas**: Use separate Canvas for mobile controls if needed

---

## Future Enhancements

Potential improvements:
1. **Haptic Feedback**: Add vibration on button press (mobile)
2. **Customizable Layout**: Allow players to reposition buttons
3. **Button Themes**: Multiple visual styles (Game Boy, Game Boy Color, etc.)
4. **Accessibility**: Support for larger buttons, high contrast mode
5. **Multi-touch Gestures**: Swipe gestures for actions
6. **Button Remapping**: Allow players to reassign button functions

---

## Summary

The mobile control system provides a complete solution for:
- ✅ Game Boy-style D-pad and buttons
- ✅ PlaySolana PSG1 device prioritization
- ✅ Sprint functionality with camera synchronization
- ✅ Input priority system (PSG1 > Mobile > Keyboard)
- ✅ Automatic device detection
- ✅ Landscape orientation lock
- ✅ Comprehensive button layout

All components work together seamlessly, with automatic fallbacks and priority handling to ensure the best input experience across all platforms and devices.

