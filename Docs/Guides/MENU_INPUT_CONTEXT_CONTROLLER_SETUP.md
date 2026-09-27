# Menu Input Context Controller Setup

Use `MenuInputContextController` on UGUI menus that should support mobile HUD controls, keyboard, and gamepad navigation through the shared Unity Input System.

## Recommended Hierarchy

- Add `MenuInputContextController` to the menu root or a dedicated `MenuInput` child.
- Keep menu behavior on the menu's existing controller script.
- Use `onCancel` to call the menu controller's close/back method.

## Inspector Fields

- `Input Actions`: assign `InputSystem_Actions`.
- `UI Action Map Name`: keep `UI`.
- `Cancel Action Name`: keep `Cancel`.
- `Default Selection`: first button that should be focused when the menu opens.
- `Fallback Selection`: safe button to focus if the default is hidden or disabled.
- `Navigation Order`: vertical D-pad order for selectable controls.
- `Configure Explicit Navigation`: enable for most menus.
- `Wrap Navigation`: enable only for menus that should loop from bottom to top.
- `Restore Selection When Lost`: keep enabled for mobile/controller support.
- `Acquire Mobile Hud Menu Context`: enable when the menu should show menu controls and suppress overworld HUD behavior.
- `On Cancel`: wire to the menu's close/back method.

## Main Menu Pattern

The main menu uses `MenuInputContextController` to own:

- `D-pad` / left stick navigation.
- `A` / submit through `UI/Submit`.
- `B` / cancel through `UI/Cancel`.
- Selection recovery after touch/background deselection.
- `MobileHudContext.Menu` token ownership while the menu is active.

`MainMenuSequenceController2` still owns wallet/profile/routing behavior. Its public `HandleMenuCancelRequested()` method is the reusable cancel target for the controller.

## Future Menu Checklist

1. Add `MenuInputContextController` to the menu root.
2. Assign `InputSystem_Actions`.
3. Fill `Navigation Order` from top to bottom.
4. Set `Default Selection` and `Fallback Selection`.
5. Wire `On Cancel` to the menu close/back method.
6. Enable `Acquire Mobile Hud Menu Context` for full-screen menus like pause, settings, inventory, roster, or wallet.
7. Test D-pad movement, `A` submit, `B` cancel, and selection recovery after tapping empty space.
