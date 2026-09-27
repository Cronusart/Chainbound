# NPC narrative movement and autonomy

## Problem

`NPCController` reapplies the route schedule whenever `ApplyEffectiveRoute()` runs (e.g. **`OnTimeSegmentChanged`**). If schedule autonomy is **on** while a sequence step drives the NPC via `NPCMovementAgent` (`MoveToPosition`, follow, etc.), the **route runner coroutine** and the **narrative movement** can both fight over the same `NavMeshAgent`.

## Built-in fix (Move To / Follow steps)

**`Step_NPC_MoveTo`** and **`Step_NPC_Follow`** include an inspector flag (default **on**):

- **Suspend autopilot during step** (`suspendAutonomyDuringStep`)

When enabled, the step:

1. Records whether autonomy was enabled (`NPCController.AutonomyEnabled`).
2. Calls **`SetAutonomyEnabled(false)`** so the route runner stops and time-of-day cannot restart it mid-step.
3. Runs the movement/follow logic.
4. In a **`finally`** block, restores the **previous** autonomy state.

You can turn off **Suspend autopilot during step** on an asset if you need legacy behavior (e.g. you manually control autonomy in the sequence).

## Pause for player (optional, synchronous steps only)

**`Step_NPC_MoveTo`** and **`Step_NPC_Follow`** also expose **Suppress Pause For Player During Step** (`suppressPauseForPlayerDuringStep`, default off).

When enabled **and** the step is **not** fire-and-forget, the step wraps movement in **`NPCNarrativePlayerBlockScope`**: the NPC’s scripted **pause-for-player** behavior (see `NPCMovementAgent.UpdatePlayerBlocking`) is suppressed so the NavMesh agent keeps steering toward the destination instead of stopping on overlap. Unity avoidance and collision layers still apply; this does **not** knock the player aside. Scope ends in the step’s **`finally`** block so autonomous routes afterward use the NPC prefab default again.

## Manual steps (optional)

You no longer need a separate **`Step_NPC_EnableAutonomy`** before/after every Move To or Follow **unless** you want explicit control or a different pattern. If you still use manual disable/enable, leave **suspend autopilot** off on that step to avoid redundant toggles.

## Implementation

- Helper: `NPCNarrativeAutonomyScope` (`Enter` / `Exit`) in `Assets/Scripts/NPC/NPCNarrativeAutonomyScope.cs`  
- Pause-for-player scope: `NPCNarrativePlayerBlockScope` in `Assets/Scripts/NPC/NPCNarrativePlayerBlockScope.cs`  
- Steps: `Step_NPC_MoveTo.cs`, `Step_NPC_Follow.cs`
