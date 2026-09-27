# Combat2 UI Setup Guide

This guide covers the current Combat2 HUD architecture after migrating the battle HUD from UI Toolkit to uGUI.

## 1) What This UI System Is

Combat2 now uses a uGUI HUD for combat readouts and indicators:

- Player and enemy HUD cards (A/B slots per side)
- Name, level, type icon
- HP/AP display
- Active-turn highlight
- Target highlight
- Turn order ribbon placeholder

Primary files:

- `Assets/Game/Combat2/UI/Prefabs/CombatBattleCanvas.prefab`
- `Assets/Game/Combat2/UI/Prefabs/HudCard.prefab`
- `Assets/Game/Combat2/UI/Runtime/CombatBattleUGUIController.cs`
- `Assets/Game/Combat2/UI/Runtime/HudCardView.cs`
- `Assets/Game/Combat2/UI/Runtime/CombatBattleUGUIInstaller.cs`
- `Assets/Game/Combat2/UI/Runtime/CombatActionSelectorBridge.cs`

## 2) What It Is NOT

- It is not the action-selection menu.
- It does not replace the 3D selector (`CombatActionSelectorController`).
- It does not implement full Fight/Switch/Item/Flee/Record content panels.

Current split:

- 3D selector: input + action choice entry point
- uGUI HUD: display + combat indicators only

## 3) Prefabs & Scene Hierarchy

Drop this prefab into the combat scene (or let installer spawn it):

- `Assets/Game/Combat2/UI/Prefabs/CombatBattleCanvas.prefab`

Expected structure:

- `Canvas` (Screen Space Overlay)
- `CanvasScaler`
- `GraphicRaycaster` (disabled to avoid selector input blocking)
- `SafeAreaRoot` (`SafeAreaAdapter`)
- `HudRoot`
  - `PlayerHudPanel`
    - `PlayerSlotA_HudCard`
    - `PlayerSlotB_HudCard`
  - `EnemyHudPanel`
    - `EnemySlotA_HudCard`
    - `EnemySlotB_HudCard`
  - `TurnOrderRibbon` (placeholder)
  - `TargetIndicatorsRoot` (placeholder)
  - `BottomActionPanel` (placeholder only)

Canvas Scaler settings:

- UI Scale Mode: `Scale With Screen Size`
- Reference Resolution: `1920 x 1080`
- Screen Match Mode: `Match Width Or Height`
- Match: `0.5`

Safe area:

- `SafeAreaRoot` must include `SafeAreaAdapter`.

## 4) Wiring to Combat Runtime

### HUD binding path

`CombatBattleUGUIInstaller` auto-attaches to every `CombatBattleController` on scene load, then:

1. Finds or spawns `CombatBattleUGUIController`
2. Calls `Initialize(battleController)`
3. HUD controller subscribes to battle events

`CombatBattleUGUIController` listens to:

- `OnBattleStarted`
- `OnTurnBegan`
- `OnActionStepExecuted`
- `OnTurnEnded`
- `OnChainlingFainted`
- `OnBattleEnded`
- `OnTurnActorSelected`
- `OnActionTargetsResolved`
- `OnVitalsChanged`

Core public API remains available:

- `BindCombat(CombatResolver resolver, CombatEventLog log)` (compatibility stub)
- `RefreshAllHud()`
- `SetActiveTurnSlot(int teamIndex, int slotIndex)`
- `SetTargetSlots(...)`

### Selector action submission path

`CombatActionSelectorBridge` handles `CombatBattleController.OnPlayerTurnWaiting` and:

1. Enables `CombatUI` map (`Navigate/Confirm/Cancel`)
2. Routes input to `CombatActionSelectorController`
3. Builds `CombatAction` selections for active player slots
4. Calls `SubmitPlayerActions(...)`

This keeps 3D selector as primary action input without reviving UITK battle HUD scripts.

## 5) Team Size Behavior

`CombatBattleUGUIController.UpdateTeamHud(...)` behavior:

- 2 active chainlings: show both cards split left/right
- 1 active chainling: show one card centered, hide the other
- 0 active chainlings: hide both cards

Supported shapes include mixed active counts across sides (2v1, 2v2, etc.) based on current active slots.

KO + autoswap:

- HUD refreshes from combat events (`ChainlingKO`, `AutoSwapIn`, `SwapPerformed`, vitals events)
- Card bindings update to the newly active chainling automatically

## 6) Keeping 3D Selector Primary

Input priority safeguards:

- HUD root raycaster is disabled (`GraphicRaycaster.enabled = false`)
- HUD graphics are non-interactive (`raycastTarget = false` on generated images/text)
- No uGUI action buttons are used for combat choice

If input conflicts happen:

- Ensure only one active `CombatActionSelectorController` exists in scene
- Ensure `CombatActionSelectorBridge` is present on the `CombatBattleController` object
- Verify `CombatUI` action map contains `Navigate`, `Confirm`, `Cancel`
- If needed, set `CombatActionSelectorBridge.disablePlayerMapWhileSelecting = true`

## 7) Customizing HUD Layout (Prefab Edits Persisting)

To edit the HUD layout (panel positions, anchors, etc.) and have changes persist:

1. **Open the prefab** `Assets/Prefabs/Combat/CombatBattleCanvas.prefab` in prefab mode.
2. **Ensure layout enforcement is OFF** on `CombatBattleUGUIController`:
   - **Enforce Hud Panel Layout** = unchecked
   - **Enforce Card Layout** = unchecked
3. Make your layout changes (anchors, positions, sizes).
4. Save the prefab (Ctrl+S or Apply in prefab mode).

If these flags are unchecked, the controller preserves your prefab/scene layout instead of overwriting it with hardcoded values at runtime. The installer (`CombatBattleUGUIInstaller`) also sets `preserveSceneHudLayout = true` by default, which calls `SetLayoutEnforcement(false)` when it finds the canvas.

## 8) Debug Checklist

### If HUD is not updating

- Confirm `CombatBattleUGUIController` exists (scene or spawned).
- Confirm `CombatBattleUGUIController` has a `CombatBattleController` reference (auto-find is fallback).
- Confirm `OnTurnBegan` / `OnActionStepExecuted` are firing in logs.
- Call `RefreshAllHud()` manually in play mode to validate card references.

### If selector input stops working

- Confirm `CombatActionSelectorBridge` exists and is enabled.
- Confirm `CombatActionSelectorController` exists and is enabled.
- Confirm `CombatBattleController.usePlayerInput` is enabled for that scene.
- Confirm input asset has `CombatUI/Navigate`, `CombatUI/Confirm`, `CombatUI/Cancel`.

### If layout changes revert

- Ensure `CombatBattleUGUIController` has **Enforce Hud Panel Layout** and **Enforce Card Layout** unchecked.
- Ensure `CombatBattleUGUIInstaller` has **Preserve Scene Hud Layout** checked.

### If target/active highlight looks wrong

- Verify slot mapping:
  - Team 0: Player A/B
  - Team 1: Enemy A/B
- Verify `OnTurnActorSelected` and `OnActionTargetsResolved` fire from `CombatBattleController`.
- Verify hidden cards are not being highlighted (only active objects should show outlines).
