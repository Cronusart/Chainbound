# Mobile Controls Setup Guide

This guide explains how to set up mobile controls (virtual D-pad/joystick + Game Boy-style buttons) for Signal Jam, with automatic PlaySolana PSG1 device detection.

## Overview

The mobile control system consists of four main components:
1. **VirtualDPad** - D-pad style virtual control (PlaySolana-inspired, recommended)
2. **VirtualJoystick** - Circular touch-based joystick (legacy option)
3. **GameboyButton** - Game Boy-style buttons for actions (Interact, Attack, Menu)
4. **MobileInputManager** - Manages mobile input, detects PlaySolana device, and integrates with Unity's Input System

## PlaySolana PSG1 Device Support

The system automatically detects when a PlaySolana PSG1 device is connected and **disables virtual controls** since the device has physical D-pad and buttons. Virtual controls are hidden when:
- PSG1 device is detected via `PSG1.current` (PlaySolana SDK)
- Device product name contains "psg1" or "playsolana"

When PlaySolana device is connected, the game uses the physical controls directly through Unity's Input System.

## Setup Instructions

### 1. Create the UI Canvas

1. Create a new Canvas in your scene (GameObject > UI > Canvas)
2. Set Canvas Scaler to "Scale With Screen Size"
3. Set Reference Resolution (e.g., 1920x1080 for mobile)

### 2. Create Virtual D-Pad (Recommended - PlaySolana Style)

**Option A: D-Pad Style (Recommended)**

1. Create an empty GameObject under Canvas: `DPad`
2. Add `RectTransform` component
3. Position it in bottom-left corner (Anchor: Bottom-Left)
4. Add `VirtualDPad` script component

5. Create 4 child Button GameObjects:
   - `Up` - Position above center
   - `Down` - Position below center
   - `Left` - Position left of center
   - `Right` - Position right of center

6. For each button:
   - Add Image component with D-pad button sprite (if not already present)
   - Ensure Image component has "Raycast Target" enabled
   - Assign GameObject to corresponding field in VirtualDPad script
   - Button component is optional - VirtualDPad works with Image components directly

**Layout Example:**
```
    [Up]
[Left] [Right]
   [Down]
```

**Option B: Circular Joystick (Legacy)**

1. Create an empty GameObject under Canvas: `Joystick`
2. Add `RectTransform` component
3. Position it in bottom-left corner (Anchor: Bottom-Left)
4. Add an Image component for the background (circular sprite)
5. Add `VirtualJoystick` script component
6. Create a child GameObject: `Handle`
   - Add `RectTransform` component
   - Add an Image component (smaller circle for the handle)
   - Assign to `joystickHandle` field in VirtualJoystick script

### 3. Create Game Boy-Style Buttons

**Action Buttons (Bottom Right Panel - PlaySolana 4-Button Layout):**
Create 4 buttons: X, Y, A, B

1. Create 4 Button GameObjects under Canvas (GameObject > UI > Button)
2. Position in bottom-right corner in PlaySolana layout:
   ```
   [Y] [X]
   [A] [B]
   ```
3. Remove the default Button component (we use GameboyButton instead)
4. Add `GameboyButton` script component to each
5. Assign the Image component to `buttonImage` field
6. Customize colors:
   - Normal Color: White or light gray
   - Pressed Color: Darker gray (0.8, 0.8, 0.8, 1.0)
7. Name them appropriately: `ButtonX`, `ButtonY`, `ButtonA`, `ButtonB`

**System Buttons (Bottom Middle Panel):**
For Start and Select buttons:

1. Create Button GameObjects under Canvas
2. Position in bottom-middle area
3. Remove default Button component
4. Add `GameboyButton` script component
5. Assign Image component to `buttonImage` field

**Sprint Button:**
For sprint functionality:

1. Create Button GameObject under Canvas
2. Position near D-pad or action buttons (recommended: bottom-left area)
3. Remove default Button component
4. Add `GameboyButton` script component
5. Assign Image component to `buttonImage` field
6. This button will be held to sprint (like a run button)

**Wallet Button (Top Middle):**
For Solana wallet button:

1. Create Button GameObject under Canvas
2. Position in top-middle area
3. Remove default Button component
4. Add `GameboyButton` script component
5. Assign Image component to `buttonImage` field

**Recommended Layout:**
```
        [Solana]
        
[Start] [Select]

[Y] [X]
[A] [B]
```

### 4. Setup MobileInputManager

1. Create an empty GameObject: `MobileInputManager`
2. Add `MobileInputManager` script component
3. Assign references:
   - `movementDPad`: Your VirtualDPad GameObject (recommended)
   - `movementJoystick`: Your VirtualJoystick GameObject (optional, fallback)
   
   **Action Buttons (Bottom Right - PlaySolana 4-Button Layout):**
   - `buttonX`: Your X button (Back)
   - `buttonY`: Your Y button (In-game menu)
   - `buttonA`: Your A button (Interact 2)
   - `buttonB`: Your B button (Interact - primary)
   
   **System Buttons (Bottom Middle):**
   - `startButton`: Your Start GameboyButton (main menu)
   - `selectButton`: Your Select GameboyButton (quick menu/inventory)
   
   **Movement Button:**
   - `sprintButton`: Your Sprint GameboyButton (hold to sprint)
   
   **Wallet Button (Top Middle):**
   - `solanaButton`: Your Solana wallet GameboyButton
   
   - `preferDPadOverJoystick`: Check this to use D-pad when both are assigned

### 5. Connect to PlayerMovement

1. Select your Player GameObject
2. In `PlayerMovement` component, assign:
   - `inputActions`: Your InputSystem_Actions asset (from Assets folder)
   - `mobileInputManager`: Your MobileInputManager GameObject

### 6. Configure Input Actions Asset

1. Select `InputSystem_Actions` asset in Project window
2. In Inspector, ensure:
   - Action Map is named "Player" (matches code)
   - "Move" action exists and is set to Vector2
   - "Interact" and "Attack" actions exist

## Platform Detection

The system automatically detects:
- **PlaySolana PSG1 Device**: Automatically disables virtual controls
- **Mobile Platforms**: Android, iOS, Mobile builds
- **Desktop**: Virtual controls disabled by default

You can also manually enable/disable mobile controls via `MobileInputManager.SetMobileControlsEnabled()`, but this will be overridden if a PlaySolana device is detected.

## Screen Orientation

The game automatically locks to **landscape orientation** when running on mobile devices:
- Landscape Left orientation is enforced
- Portrait rotations are disabled
- This ensures proper mobile control layout and PlaySolana device compatibility

The orientation lock is applied automatically when `MobileInputManager` initializes on mobile platforms.

## Button Functions

### Action Buttons (Bottom Right - PlaySolana 4-Button Layout)
- **X Button**: Back - Triggers "Cancel" Input Action or returns to previous screen
- **Y Button**: In-game menu - Opens quick menu / creature inventory (Roster scene)
- **A Button**: Interact 2 - Triggers "Attack" Input Action (secondary interact)
- **B Button**: Interact - Triggers "Interact" Input Action (primary interact for objects/NPCs)

### System Buttons (Bottom Middle)
- **Start**: Opens the main menu (loads MainMenu scene)
- **Select**: Opens quick menu / creature inventory (loads Roster scene)

### Movement Button
- **Sprint**: Hold to sprint (increases movement speed and zooms camera out)
  - Works with mobile button, PSG1 device button, or keyboard (Left Shift)
  - Automatically synchronizes with camera zoom

### Wallet Button (Top Middle)
- **Solana**: Opens wallet connection UI
  - If wallet not connected: Triggers wallet connection (Phantom, etc.)
  - If wallet connected: Shows wallet info (can be extended to show wallet panel)

## Customization

### D-Pad Settings
In VirtualDPad:
- `allowDiagonalInput`: Set to false for Game Boy-style (no diagonals)
- `inputDeadzone`: Minimum input threshold
- `pressScale`: Scale when button is pressed (for tactile feedback)
- `normalColor` / `pressedColor`: Button colors

### Joystick Range
Adjust `joystickRange` in VirtualJoystick to change the joystick movement area.

### Button Visuals
Customize button colors and press animation in GameboyButton:
- `normalColor`: Default button color
- `pressedColor`: Color when pressed
- `pressedScale`: Scale when pressed (for tactile feedback)

### Audio Feedback
Add AudioSource to GameboyButton and assign:
- `pressSound`: Sound when button is pressed
- `releaseSound`: Sound when button is released

## Testing

1. **In Editor**: Mobile controls are disabled by default. Enable them manually:
   - Set `autoDetectPlatform` to false
   - Set `enableMobileControls` to true

2. **On Mobile Device**: Controls automatically activate on mobile platforms.

3. **With PlaySolana PSG1 Device**:
   - Connect your PSG1 device
   - Virtual controls will automatically hide
   - Physical D-pad and buttons work directly through Input System
   - Check console for "[MobileInputManager] PlaySolana PSG1 device detected" message

## Troubleshooting

**Controls not working:**
- Check that MobileInputManager is assigned to PlayerMovement
- Verify Input Actions asset is assigned
- Ensure action map is named "Player"

**Joystick not responding:**
- Check that Canvas has GraphicRaycaster component
- Verify EventSystem exists in scene
- Ensure joystick has Image component with Raycast Target enabled

**Buttons not working:**
- Check that buttons have Image component
- Verify EventSystem exists in scene
- Ensure buttons are children of Canvas

## Alternative: Using Generated C# Classes

If you prefer type-safe generated classes:

1. Select `InputSystem_Actions` asset
2. In Inspector, check "Generate C# Class"
3. Click "Apply"
4. Update PlayerMovement to use generated class (see Unity Input System docs)

The current implementation uses the InputActionAsset directly, which works without code generation.

