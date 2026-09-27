# Encounter return: bridge-based overworld snapshot and dynamic spawn

Overworld encounters now flow through **`EncounterTransitionBridge`** before the battle scene loads.
The bridge centralizes the pre-battle handoff for:

- wild encounters (`EncounterTriggerZone`)
- NPC encounters (`NpcEncounterTriggerZone`)
- narrative encounters (`NarrativeEncounterService`)

That bridge is responsible for:

- acquiring an `InputLockService` lock on `InputLockChannel.All`
- hard-freezing the current `PlayerMovement3D`
- updating the runtime return spawn when using the dynamic `encounter_return` flow
- saving `EncounterReturnContextService` data
- playing a short chromatic aberration pulse
- handing off to `SceneLoader` or falling back to `SceneManager.LoadScene`

## Dynamic `encounter_return` SpawnPoint

Each overworld scene that can launch combat should contain **one** `SpawnPoint` with:

- `spawnId = "encounter_return"`

This spawn point can live anywhere in the scene at author time. The bridge repositions it at runtime to the player's current location and facing before battle loads.

What gets updated at runtime:

- `transform.position` — player root position plus **`SpawnPoint.runtimeVerticalOffsetFromPlayer`** (default small +Y so the return point matches authored spawn safety, not raw feet-in-mesh)
- `spawnRotationY`
- `lightingType` (`Indoor` or `Outdoor` based on `TimeOfDayVisualApplier.IsIndoors`)

After that, the bridge force re-registers the spawn with `SpawnService`, so post-battle return can use the moved spawn point immediately.

**Chromatic aberration:** On a successful battle load, the bridge does **not** reset chroma intensity in `finally` (the overworld volume unloads with the scene). If the transition aborts before load (e.g. `SceneLoader` busy), intensity is restored to the previous value.

## Authored return SpawnPoint ids still work

Narrative encounters can still provide an explicit authored return spawn id.
When a non-empty return spawn id is supplied, the bridge:

- saves that id into `EncounterReturnContextService`
- does **not** reposition the authored spawn
- lets `PostBattleSceneReturnHandler` use `SpawnService.SpawnPlayerAt(returnSpawnId)` on return

Return spawn id precedence remains:

1. `NarrativeEncounterService` registry entry `Return Spawn Point Id`
2. `Step_StartEncounter` / `EncounterConfig.returnSpawnPointId`
3. bridge default `encounter_return`

## `skipSaveReturnContext` still matters

If a prior sequence step already saved the exact return snapshot, you can still prevent the encounter launch from overwriting it.

Use:

- `Step_WaitForTrigger` with **Save Return Snapshot On Trigger Complete**
- `Step_StartEncounter` with **Skip Save Return Context**

When `EncounterConfig.skipSaveReturnContext` is true, `EncounterTransitionBridge` will:

- skip return-context saving
- skip dynamic `encounter_return` repositioning
- preserve the previously saved snapshot exactly as-is

Use this when you want the post-battle return position to match the original trigger instant rather than the later pre-battle handoff point.

## Post-battle placement behavior

`PostBattleSceneReturnHandler` now prefers placement in this order:

1. `returnSpawnId` via `SpawnService.SpawnPlayerAt`
2. raw snapshot fallback with ground snap + temporary rigidbody safety hold

That means:

- dynamic `encounter_return` gives you exact return placement with authored spawn semantics
- authored return spawn ids still work for custom narrative placements
- missing spawn points safely fall back to raw snapshot placement

## Scene setup checklist

For every overworld scene that can start a battle:

1. Add a `SpawnPoint` prefab instance.
2. Set `spawnId` to `encounter_return`.
3. Leave it somewhere safe like `(0, 1, 0)` if you do not care about its authored position.
4. Ensure the scene has the usual lighting setup so `TimeOfDayVisualApplier` can report indoor/outdoor state.

## Troubleshooting

### Player returns to the wrong named spawn

If the console shows the correct encounter return context but the player still appears at an old named spawn, check whether another system forced a scene-load spawn id or whether the return scene is missing the `encounter_return` spawn point.

`PostBattleSceneReturnHandler` still calls `SpawnService.SkipAutoSpawnForNextSceneLoad()` for exact returns so `AutoSpawnCoroutine` does not fight the battle return placement.

### `encounter_return` is not used

If `PostBattleSceneReturnHandler` logs that `returnSpawnId` was not registered, confirm the return scene contains a `SpawnPoint` with:

- `spawnId = "encounter_return"`

If the spawn is missing, the handler falls back to snapshot placement.

### I need the exact trigger spot, not the later handoff point

Use the narrative snapshot path:

- save the snapshot at trigger time
- set `skipSaveReturnContext = true` when launching the encounter

That prevents the bridge from overwriting the earlier saved position.

## Code references

- `EncounterTransitionBridge`
- `EncounterReturnContextService`
- `PostBattleSceneReturnHandler`
- `EncounterTriggerZone`
- `NpcEncounterTriggerZone`
- `NarrativeEncounterService`
