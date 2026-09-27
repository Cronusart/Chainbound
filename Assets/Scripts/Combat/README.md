# Combat System Overview

This folder contains the Unity combat stack used by the playable battle scenes and the combat harness.

## Layout
- `Runtime/Engine/`: battle resolution, turn flow, action execution, and core combat state
- `Runtime/Presentation/`: battlefield presenter, chainling views, VFX hooks, and combat camera behavior
- `Runtime/UI/`: action selection, target selection, and HUD-facing coordination
- `Runtime/AI/`: combat decision-making and legal action generation
- `Runtime/Data/` and `Runtime/ScriptableObjects/`: move data, battle data, and authoring assets
- `Tests/`: combat-focused tests and regression coverage

## Recommended Entry Points
- `Assets/Scenes/Combat2_TestHarness.unity`
  - Fastest scene for validating combat behavior in isolation.
- `Runtime/Engine/CombatBattleController.cs`
  - Central turn/battle orchestration.
- `Runtime/Presentation/Combat2BattlefieldPresenter.cs`
  - Scene-side presentation bridge for combat events.

## Notes
- The combat harness is the best place to review ongoing gameplay and presentation work.
- Some legacy or experimental code is intentionally separated inside `Runtime/Quarantine/`.
