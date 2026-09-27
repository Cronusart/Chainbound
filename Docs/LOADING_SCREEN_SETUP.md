# Production Bootstrap and Scene Loading

## Runtime architecture

`Bootstrap` is the only production entry scene and must remain at build index 0. It owns the one production `GameServices` prefab instance. `GameServicesContainer` moves that root to `DontDestroyOnLoad`; gameplay scenes and `main_menu` must not contain another copy.

Cold start is:

1. Unity's native splash covers engine and first-scene startup.
2. `Bootstrap` renders the custom loading cover fully opaque.
3. `BootstrapStartupController` waits for persistent services and requests `main_menu`.
4. `SceneLoader` loads, activates, warms, and finalizes `main_menu` behind the cover.
5. `BeforeReveal` starts the authored main-menu splash clock, and the cover fades into it.

The profile bootstrap still starts as soon as `main_menu` activates. Network/profile readiness is intentionally owned by the menu splash rather than the global scene loading boundary.

## Loading lever board

The single project config is `Assets/Data/Loading/SceneLoadingConfig_Default.asset`. The `SceneLoader` resolves configuration in this order: explicit injection, registered project asset, runtime defaults.

Initial production values are:

| Lever | Value |
| --- | --- |
| Startup destination | `main_menu`, FullLoading |
| Default destination style | QuickFade |
| Loomville | FullLoading |
| Full/Quick minimum display | 2.75s / 0.8s |
| Fade in/out/post-delay | 0.2s / 0.4s / 0.3s |
| Full/Quick loading priority | Normal / BelowNormal |
| Warmup task timeout | 10s |
| Recovery destination | `main_menu` |

Use `Tools > Signal Jam > Loading > Validate Loading Config` after changing it. Validation checks scene IDs, duplicate rules, enabled Build Settings membership, timing, startup/recovery destinations, the overworld hub FullLoading rule, streamed-chunk content rules, build order, and production `GameServices` placement.

Destination rules select the normal style. `SceneTransitionTrigger` uses the lever board unless `Override Configured Style` is enabled. Narrative, encounter, or exceptional requests may supply an explicit override.

## Additive overworld streaming

The overworld uses a deliberately separate, silent IO path. `SceneLoader.RequestTransition` remains the hard-swap API and still uses `LoadSceneMode.Single` with the loading cover for interiors, combat, menus, and entry to or exit from an overworld set.

`Assets/Data/Loading/OverworldStreamingSet_Loomville.asset` is the source of truth for this overworld set (one asset per hub). It owns set membership, ordered chunks, entry residency, neighbors, and volume IDs. Create another `Overworld Streaming Set` asset when a later hub needs its own chunk list.

Current Loomville set:

- `Loomville` — hub, FullLoading destination, preloaded on set entry.
- `Link1` — preloaded resident. Always loaded with Loomville when exiting an interior into town. Not a covered destination.
- `Jungle` — future chunk, **not** preloaded. Added additively from a Link1 stream volume near the jungle end.

`OverworldStreamingService`, hosted next to `SceneLoader` on `GameServices`, exposes `EnsureChunk`, `ReleaseChunk`, and `SetActiveChunk`. Scene-authored `OverworldStreamVolume` components issue only those generic directives. Do not use `SceneTransitionTrigger` at a chunk seam.

Hysteresis is a parent/child collider pair, same pattern as camera and interior transition buffers:

1. Inner `OverworldStreamVolume` (the TransitionZone box on Link1). Player **enter** loads `Jungle`, sets `Link1` active, then unloads `Loomville`. Inner **exit** does nothing.
2. Child `OverworldStreamBuffer` (`BufferZone`). Place it on the **Loomville / town side** of the inner box. Do not let it stick past the jungle face. After the player has visited the inner volume, walking back and **leaving the buffer** reloads `Loomville` and unloads `Jungle`.

Select the TransitionZone in Link1 and use `GameObject > Signal Jam > Configure Overworld Stream Volume (Loomville-Jungle)`, or the buttons on the volume inspector. Stretch the child toward town after it is created. `Jungle` can sit in the set before it is in Build Settings; add it to Build Settings before that volume should actually load.

On entry to any set scene, `SceneLoader` waits for the initial resident chunks while its existing cover remains opaque. Later chunk loads run additively at below-normal priority and warm only the newly loaded scene. The active chunk drives spawn lookup and encounter snapshots. Soundtrack follows mapped scene loads: covered `SceneLoader` entry, and additive reload of a catalog scene such as Loomville after it was unloaded. Camera-zone and stream-volume `SetActiveChunk` flips do not restart the town bed. Unmapped additive chunks (Jungle) must not stop that bed. After a chunk load or unload, live `CameraZoneTrigger` occupancy is re-sampled from the player position so PhysX false enter/exit cannot leave seam yaw stuck, and a reloaded hub zone cannot restore or steal yaw while another loaded zone still contains the player. A single `CameraRigRoot` is moved under persistent `GameServices` before a source chunk can unload. Additive reloads of a previously unloaded hub must immediately disable and strip that scene's seed `CameraRigRoot` so it cannot present at authored default yaw while the GameServices rig still holds the live camera-zone yaw.

Combat still hard-loads and therefore unloads the entire overworld set. Encounter return context stores the active scene plus all loaded build scenes; returning through the hub restores configured chunk members before exact placement. A persistent dynamic `encounter_return` fallback covers streamed corridors until a local authored point is added.

Authoring rules for every streamed chunk:

- No Main Camera, AudioListener, EventSystem, player, outdoor sun, or active `TimeOfDayVisualApplier`.
- Use one persistent GameServices time-of-day applier and outdoor sun. `TimeOfDayRegionVolume` may layer fog density/tint through that writer; it does not create a second clock or config.
- Bake each chunk's NavMesh separately and overlap traversable ground by 2–4m at boundaries. Do not unload a chunk while a sequence still needs one of its waypoints.
- Runtime-only clutter/NPC poses reset when a chunk reloads. Persistent quest state remains profile-owned.

## Transaction semantics

All production loads go through:

```csharp
SceneLoader.Instance.RequestTransition(new SceneTransitionRequest(
    "Loomville",
    spawnId: "town_entrance",
    sourceId: "PlayerRoom.Exit"));
```

The loader phases are:

`Idle -> Covering -> Loading -> Activating -> Warming -> Finalizing -> Revealing -> Completed/Failed`

The cover becomes opaque, blocks raycasts, and remains rendered for one end-of-frame before `LoadSceneAsync` begins. Scene activation is held at Unity's 0.9 ready point. After activation, overworld streaming and player spawn run before explicit warmups. The cover then remains through camera-zone settle, lighting finalization, and one prepared destination frame.

Displayed progress is monotonic:

- Scene read: 0-80%
- Activation: 80-90%
- Warmup/finalization: 90-100%

The UI never reports 100% before readiness completes. There is no artificial timeout for Unity's non-cancellable scene operation; long loads change the status to "Still loading...". Background loading priority is restored on every exit path.

`SceneTransitionRequest` supports destination, spawn, optional style override, source/reason ID, before-reveal callback, and completion callback. Rejected requests complete immediately with a structured Busy or Invalid result, allowing caller-local in-flight flags to reset safely.

Legacy `LoadScene` and `LoadSceneAsync` wrappers remain temporarily, but new code should submit a request.

## Scene warmup tasks

Scene-scoped components may implement `ISceneWarmupTask`. Every task in the destination scene runs in deterministic `Order`, then `WarmupId`, order. Task weights feed the 90-97% progress range. Each task can report progress, fail critically, or add degraded-mode warnings.

Unity scripts are compiled before runtime. Generic scene objects do not exist before activation, so their `Awake` and `OnEnable` methods cannot be "preloaded." The production guarantee is that activation and explicit readiness work remain visually and interactively covered.

Loomville contains a configured `SceneReadinessWarmup` that:

- runs after overworld streaming entry and player spawn so Continue/wipe into Loomville does not fail closed before `FinalizeDestination`;
- waits briefly for spawn, gameplay camera (persistent overworld rig included), and player instead of failing on the first frame;
- validates the requested spawn, gameplay camera, player, and configured required services;
- uses `SceneCameraResolver` once and assigns unresolved `SpriteBillboard` components in bounded batches;
- initializes deferred `GrassPatchManager` components one manager per frame;
- treats spawn/camera/player failures as critical and lighting/ambience helpers as degraded-mode warnings.

Keep heavy hierarchy scans out of `Awake` when adding more large-scene systems. Add reusable, weighted warmup tasks instead.

Covered overworld spawn does not rely on physics `OnTriggerEnter`. After the player is placed, `FinalizeDestination` waits briefly for a live camera zone to contain that position (compound child volumes and additive chunks can exist a frame later), then `ForceActivateAtPosition` retries `Activate` until the zone is actually live (a failed camera bind must not leave the trigger marked occupied). Streaming chunk resync waits until `SceneLoader` is no longer covering so a hub reload cannot sample the player before spawn.

## Editor parity

`Tools > Signal Jam > Loading > Always Start Through Bootstrap` is enabled by default. On Play, Unity launches `Bootstrap`, then the startup controller loads the build scene that was active in the editor. Unsaved, disabled, invalid, or Bootstrap targets fall back to `main_menu` with a warning.

This makes scene testing use the same persistent services, cover, and transition lifecycle as a build. Disable the toggle only for isolated component/UI preview work.

## Loading UI and recovery

The persistent loading canvas is Screen Space Overlay at the authored landscape reference resolution. Its fade cover is full bleed; loading content is moved under a safe-area wrapper at runtime. The prefab starts at scale one and is normalized defensively.

Failure policy:

- Before activation: Retry or Cancel to the still-loaded source.
- After activation: Retry or Return to Main Menu.
- Recovery-scene failure: keep the recovery panel covered and retryable.

Recovery buttons are gamepad/EventSystem navigable and use at least 220x64 authored-pixel targets. The transition cleanup releases only the loader's own input token, unfreezes the exact player it froze, restores loading priority, and resets state. Scene-owned input locks expire when their owner scene unloads; persistent menu/sequence locks are not globally cleared.

## Verification

Run:

- `Tools > Signal Jam > Loading > Validate Loading Config`
- Edit Mode tests matching `SceneLoadingPipelineTests`
- Play Mode smoke checks from Bootstrap through `main_menu` and `player_room` to Loomville

For release acceptance, profile Windows and Android development builds. There must be no uncovered source-scene freeze before loading, no input window during transition, exactly one `GameServices`, and all activation/warmup stalls must remain behind the opaque cover.

Addressables, multiplayer loading, academy wiring, and broader `GameServices` reorganization remain intentionally out of scope. The scene-IO seam supports a future Addressables backend without changing scene callers.
