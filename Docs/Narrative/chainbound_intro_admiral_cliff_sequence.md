# Chainbound intro — Admiral at cliff (after `jump_fall`)

This doc describes how to wire **NPC control**, **movement to talking distance**, **camera**, and **dialogue** using the existing narrative + NPC toolbox. Admiral’s registry id in Loomville is **`admiral`** (`NPCController` on `Admiral - NPCRoot`).

---

## Design overview

| Phase | Goal | Mechanism |
|--------|------|-----------|
| **A — Wait position** | Admiral stands near the landing / trigger while the player approaches the ledge | Move to a **waypoint**, then **disable autonomy** so schedule/routes stop updating |
| **B — Player lands** | Sequence continues from `Step_WaitForTrigger` (`jump_fall`) | Already your last step today |
| **C — Cinematic beat** | Zoom on player | `Step_PlayCameraShot` with `fallbackTargetId = Player` (or assigned transform) |
| **D — Approach** | Walk toward player but not into their model | `Step_NPC_Follow` **or** `Step_NPC_MoveTo` a **waypoint** placed at “talking distance” |
| **E — Dialogue** | Back-and-forth lines | One `DialogueSequenceSO` (e.g. `Dial_AdmiralConversation`) with **distinct `speakerName`** per line |
| **F — Camera on speakers** | Framing swaps with who’s speaking | **`DialogueSpeakerCameraBridge`** (scene component) **or** short alternating `Step_PlayCameraShot` blocks (no new code) |
| **G — Resume sandbox** | Admiral returns to `NOM_ROUTE` / `NOM_ROUTE_SCHED` | **`Step_NPC_EnableAutonomy`** = true (schedule was never removed, only paused) |

---

## A — Put Admiral on station (before or after barriers, before `jump_fall`)

Use **registered waypoints** (`NPCWaypointRegistry`) so `Step_NPC_MoveTo` can resolve positions.

1. **Place** an empty or marker at the cliff wait spot (NavMesh-valid).
2. **Register** it with id e.g. `admiral_cliff_wait` (same pattern as other intro waypoints).
3. In sequence **before** the player is expected to jump (timeline is up to you):

   - **`Step_NPC_MoveTo`**  
     - `npcId`: `admiral`  
     - `useWaypointId`: true  
     - `waypointId`: `admiral_cliff_wait`  
     - Timeout as needed (e.g. 60s).

   - **`Step_NPC_EnableAutonomy`**  
     - `npcId`: `admiral`  
     - `enable`: **false**  

`SetAutonomyEnabled(false)` stops the route runner from **re-applying** the schedule; Admiral stays put until you turn autonomy back on.

**Optional:** `Step_NPC_ClaimControl` with **Hard** freezes the NPC immediately (same as interrupt **Frozen**). You **must** `Step_NPC_ReleaseControl` before any scripted walk (`MoveTo` / `Follow`). Prefer **autonomy off** without Hard claim for this beat unless you need a hard freeze mid-route.

---

## B — `jump_fall`

Keep your existing **`Step_WaitForTrigger`** (`jump_fall`) as the gate after the player leaves the ledge.

---

## C — Player zoom

**`Step_PlayCameraShot`**

- Assign a **zoom / close** `CameraShotPresetSO`.
- Target: `fallbackTargetId` = `Player`, or drag **Player** transform if the scene instance is valid.

Then **`Step_RestoreCameraFollow`** if the next beat needs follow cam before NPC walks (or let the bridge restore after dialogue — see below).

---

## D — Walk to “talking distance”

### Option 1 — `Step_NPC_Follow` (good default)

- `npcId`: `admiral`  
- `minFollowDistance`: ~**2.0–2.8** (stops closer than this)  
- `maxFollowDistance`: ~**3.5–6** (starts moving if farther)  
- `maxDuration`: **0** (until in band) or a cap if you want a timeout  
- `stopOnArrival`: **true**  
- `targetId`: leave empty for **Player**

While the player is in the band between min and max, the NPC **idles** (no path spam). When closer than `minFollowDistance`, follow ends — **natural talking distance**.

**Requires:** autonomy enabled for movement. Before this step:

- **`Step_NPC_EnableAutonomy`** `enable`: **true**  
- Do **not** leave a **Hard** `ClaimControl` active.

### Option 2 — `Step_NPC_MoveTo` to a fixed waypoint

Place **`admiral_talk_spot`** at a comfortable standoff from the landing collider; use **world position** or waypoint id. Less dynamic than Follow, but very predictable.

---

## E — Dialogue

- **`Step_PlayDialogue`** → your `DialogueSequenceSO` (e.g. **`Dial_AdmiralConversation`**).
- **Lock input** during dialogue if you want a classic Cinematic feel (`lockInputDuringDialogue` = true).
- Give each line a **`speakerName`** that matches your camera rules (e.g. `"Admiral"`, `"You"`, or player name).

---

## F — Camera follows the speaker

### F1 — `DialogueSpeakerCameraBridge` (speaker-based, one dialogue step)

- **`GameServices` prefab:** keep **`DialogueSpeakerCameraBridge` disabled** by default. Enable it only for specific dialogues by checking **`Use Speaker Camera Bridge`** on the **`Step_PlayDialogue`** asset (enables the component for the duration of that dialogue).
- Add **`DialogueSpeakerCameraBridge`** to a persistent object (e.g. same as `DialogueService` / `GameServices`).
- Add **rules**: e.g. speaker contains `"Admiral"` → shot preset + target `Admiral - NPCRoot` (drag scene reference) or `fallbackTargetId` by name.
- **First match wins**; optional **default** shot if no rule matches.
- Shots are **non-blocking** for dialogue; short presets or `skipIfShotAlreadyPlaying` avoid overlap flicker.
- On **dialogue complete**, the bridge calls **`CameraShotController.EndDialogueCameraSession()`** (or the legacy path: stop shot + **`RestoreCameraFollow`**).

#### Smoother line-to-line framing (anchored session)

Enable on the bridge:

- **Use Anchored Dialogue Session** — On **`DialogueService.OnDialogueStarted`**, calls **`CameraShotController.BeginDialogueCameraSession`**: saves **profile / zoom / pitch** (`CameraStateSnapshot`) as the exit target, optionally **pauses follow** so scripted rig moves are not fighting live follow.
- Each line uses **`PlayDialogueLineShot`** instead of **`PlayShot`**: it does **not** re-save state per line and does **not** restore after every line (that was causing snap-back jank).
- **Neutralize Shot Profile Follow Offset** — optional: each line blends using a **runtime clone** of the shot’s `CameraProfile` with **`followOffset = Vector3.zero`**, so framing comes more from **rig position + look-at** and less from large profile offsets (still uses the profile’s zoom, pitch, etc.).

You can turn **anchored session** off to revert to the older per-line `PlayShot` behavior.

### F2 — No code — split cameras in the sequence

Alternate **`Step_PlayCameraShot`** (short presets) with separate **`DialogueSequenceSO`** chunks per line. More steps, more maintenance, but zero extra components.

---

## G — Resume route / schedule

1. **`Step_NPC_EnableAutonomy`**  
   - `npcId`: `admiral`  
   - `enable`: **true**  

`NPCController` still references **`NOM_ROUTE_SCHED`**; disabling autonomy only **paused** application. Re-enabling runs **`ApplyRouteFromSchedule()`** for the current time segment.

2. **If** you ever used **`Step_NPC_SetRouteSchedule(null)`** or swapped schedule assets during intro, set the schedule back explicitly:

   - **`Step_NPC_SetRouteSchedule`** → assign **`NOM_ROUTE_SCHED`** again.

3. **Route overrides:** If you later use **`AcquireRouteOverride`** from code, call **`ReleaseAllRouteOverrides()`** on the controller (no dedicated sequence step yet). For this intro, **autonomy off/on** is enough.

---

## Recommended step order (skeleton)

1. *(Earlier in Loomville)* `MoveTo` `admiral_cliff_wait` → `EnableAutonomy` false  
2. … player reaches ledge, plays your ledge prompts …  
3. `WaitForTrigger` `jump_fall`  
4. `LockInput` *(optional)*  
5. `PlayCameraShot` (player)  
6. `RestoreCameraFollow` *(optional)*  
7. `EnableAutonomy` true  
8. `NPC_Follow` (or `MoveTo` talk waypoint)  
9. `PlayDialogue` (Admiral conversation)  
10. `RestoreCameraFollow` *(if not using bridge or if you didn’t restore after shot)*  
11. `UnlockInput`  
12. `EnableAutonomy` true *(if not already true after follow)*  

Adjust locks/unlocks to match your input policy.

---

## Files

| Asset / type | Role |
|--------------|------|
| `Step_NPC_MoveTo`, `Step_NPC_EnableAutonomy`, `Step_NPC_Follow`, `Step_PlayCameraShot`, `Step_PlayDialogue`, `Step_RestoreCameraFollow`, `Step_LockInput`, `Step_UnlockInput` | Sequence |
| `NPCWaypointRegistry` + waypoint markers | `admiral_cliff_wait`, optional `admiral_talk_spot` |
| `DialogueSpeakerCameraBridge` | `Assets/Scripts/Camera/Shots/DialogueSpeakerCameraBridge.cs` |
| `NOM_ROUTE.asset`, `NOM_ROUTE_SCHED.asset` | Default Admiral schedule (leave assigned on prefab; autonomy toggle pauses application) |

---

## Troubleshooting

- **Admiral walks off during wait:** Confirm **`EnableAutonomy` false** after **MoveTo** completes; check no other system is calling `SetAutonomyEnabled(true)`.
- **Admiral doesn’t move during Follow:** Hard **`ClaimControl`** still active — **`ReleaseControl`**. Or **`EnableAutonomy` false** — turn **true** before Follow.
- **Follow overshoots:** Lower **`minFollowDistance`** slightly or use a fixed **MoveTo** waypoint instead.
- **Schedule doesn’t resume:** Confirm **`routeSchedule`** on the NPC still references **`NOM_ROUTE_SCHED`**; reassign with **`Step_NPC_SetRouteSchedule`** if you cleared it elsewhere.
