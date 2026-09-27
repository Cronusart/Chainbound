# Mobile HUD Scene Policy Setup

Use `MobileHudScenePolicyController` when an entire scene should force the mobile HUD into a specific context. Use narrative sequence steps when only part of a scene or cutscene needs temporary HUD control.

## Scene-Level Rules

Attach `MobileHudScenePolicyController` to the persistent `MobileServices` object and configure `Scene Rules` in the inspector.

Recommended examples:

- `main_menu` -> `Menu`
- full-screen video or narrative scenes -> `Hidden`
- battle scenes -> `Battle`
- normal overworld scenes usually need no rule and fall back to `Overworld`

The controller acquires a `MobileHudService` token when the active scene matches a rule, then releases that token when the scene no longer matches. Do not disable `MobileServices` or `MobileHudCanvas` directly for scene flow.

## Narrative Sequence Rules

Use existing sequence steps for temporary HUD changes:

1. Add `Step_SetMobileHudContext`.
2. Set `hudContext`:
   - `Hidden` for full-screen narrative/video with no controls.
   - `Cutscene` when a cutscene-specific affordance may remain visible.
   - `Dialogue` for dialogue-only control visibility.
3. Set `tokenKey` to a stable value, such as `fullscreen_narrative`.
4. Later in the same sequence, add `Step_ReleaseMobileHudContext` with the same `tokenKey`.

If a sequence stops early, `SequenceContext` releases stored mobile HUD tokens during cleanup, so the HUD should not stay stuck hidden.

## Context Guide

- `Hidden`: no mobile HUD.
- `Menu`: D-pad, action buttons, and system buttons for menu navigation.
- `Cutscene`: gameplay controls hidden; optional cutscene affordances depend on HUD group masks.
- `Dialogue`: dialogue/advance affordances only.
- `Battle`: battle/menu-style controller mode.
- `Overworld`: normal mobile gameplay controller.

## Validation Checklist

- Enter a configured scene and confirm the HUD context changes.
- Leave that scene and confirm the scene-policy token releases.
- Run a sequence that hides the HUD and confirm the paired release restores the previous context.
- Test overlapping scene and sequence tokens; `MobileHudService` should resolve the highest-priority context.
