# Combat Presentation Hybrid Guide

## Purpose

The combat presentation system now supports two authored layers on top of `CombatChainlingVisual` playback:

- species-level baseline presentation on `CombatVisualProfileSO`
- move-specific overlays on `MoveDefinitionSO`

Use the profile track for beats that should always happen for that species animation, and the move track for effects that should only happen when a specific move resolves.

Examples:
- a species-specific body flash on melee frame 1
- a move-specific sword slash on melee frame 2
- a move-specific cast audio cue on ranged frame 0
- a gameplay marker that should line up with hit or release timing

Keep using `CombatVFXLibrary` and the move presenter for larger move-phase choreography like travel paths, barrier phases, and multi-stage impact logic.

## Post-FX Ownership (Combat Priority Volume)

Temporary bloom / vignette / chromatic (etc.) uses a **combat-owned** high-priority URP Volume (`CombatPostFxVolumeController`), not the GameServices / scene Global Volume driven by TimeOfDay.

Isolation rules:

1. The controller only touches its own serialized/child `Volume` (`CombatPostFxVolume`).
2. It never finds “any global Volume”, never references TimeOfDay profiles, and never calls `VolumeProfile.Override`.
3. Playback assigns `sharedProfile = cue.profile` and animates **`weight` only** (0 → peak → 0). Idle weight is 0.
4. Combat volume priority defaults to **100**; harness baseline Global Volume stays at **0**.
5. `ForceClear()` runs on battle end / presenter disable so cues cannot stick after combat.

Authoring:

- **Moves:** optional `postFxCue` on each `CombatVFXLibrary.VFXPhaseConfig` (Cast / Strike / Projectile / Impact). A phase may be sound-only, VFX-only, post-FX-only, or any combination.
- **Species tracks:** presentation event `Family = PostFx` with payload `CombatPostFxCueSO` (Create → Chainbound/Combat/Presentation/PostFX/Post FX Cue). Assign a `VolumeProfile` with the desired overrides, then tune fade in / hold / fade out.

Scaffold in a scene: select the battlefield presenter → `Tools > Chainbound > Combat > PostFX > Scaffold Combat Post FX Volume On Selection`. Cue assets live under `Assets/Data/Combat/PostFX/`.

**Camera sampling:** combat cameras use Volume Layer Mask = **`Volume`** (layer 20). The combat-owned Volume GameObject must be on that layer (not Default), or cues will weight-fade with no visible change while GameServices Global Volume still looks “in charge.” The controller forces the owned Volume onto the `Volume` layer at runtime.

## Audio Ownership And Precedence

Combat audio has four intentional ownership layers:

1. `CombatPresentationTrackSO` owns frame-locked species/move cues. These remain independent of outcome resolution.
2. `CombatVFXLibrary.VFXPhaseConfig.phaseStartSound` owns lifecycle cues at Cast, Strike, Projectile, or Impact phase start. A phase is valid with a prefab, a phase sound, or both; `spawnDelay` applies to either.
3. `CombatVFXLibrary.VFXEntry.impactSound` and `missSound` own move outcome cues. The presenter copies them into `CombatSfxOutcomeContext`.
4. `CombatBattleSfxCatalogSO` and its linked `CombatWallSfxCatalogSO` own universal fallbacks, crit windup, material wall cues, and legacy compatibility behavior.

`CombatSfxOrchestrator` is the sole resolver for outcome audio. Authored impact/miss sounds beat catalog hit/crit/miss fallbacks, including when the root catalog is absent. A material-specific wall block beats the generic block sound. `impactAccentSound` is a temporary damage-only fallback and is suppressed when an authored impact or frame-track audio exists; do not author new content there.

**Impact pairing rules**

- Outcome `impactSound` is the hit confirmation. It never plays on miss/dodge.
- Impact `phaseStartSound` only fires when Impact VFX is spawned on connect paths (`DamageDealt`, `BarrierBlocked`). Miss/dodge never spawn Impact, so cast → projectile/strike → miss is the miss chain.
- When both outcome `impactSound` and Impact `phaseStartSound` are authored, the phase cue is ducked by `CombatBattleSfxCatalogSO.pairedImpactAccentVolumeScale` (default **0.6**). Outcome impact stays at full volume. Tune that lever for mobile speaker headroom.

Frame-track audio and lifecycle/outcome audio may intentionally overlap. The authoring inspectors warn about these combinations; they are not silently deduplicated. Keep frame cues for exact animation beats and phase/outcome cues for combat lifecycle timing.

The presentation director, battlefield presenter, and SFX orchestrator all resolve the same `ICombatAudioBridge` policy. Assign the bridge explicitly on the combat SFX prefab/scene when possible; auto-discovery is only a fallback. Presentation code never calls `AudioService` directly.

Crit timing is controlled by `CombatBattleSfxCatalogSO.critWindupSound` and `critWindupLeadSeconds`. The windup is scheduled at `max(0, impactDelay - leadSeconds)` and is only emitted for damage crits—not misses, dodges, or blocks.

## Wall SFX Authoring

`MoveDefinitionSO.wallPresentation.wallSfxId` is an asset-authored catalog ID; it does not belong in `Moves.csv`. The runtime resolves a wall cue by:

1. the wall source move's `wallPresentation.wallSfxId`;
2. the legacy `wallSourceTag`;
3. the `default` entry in the linked `CombatWallSfxCatalogSO`.

The seeded catalog includes `default`, `wall_guard_posture`, `wall_barrier`, `wall_vapor`, `wall_ash`, `wall_ember`, `wall_ice`, `wall_compound`, `wall_lattice`, `firewall_passive`, `defensive_reflex`, and `wall_ice_ap`. Add any new wall source ID to both the source move's presentation data (when the source has a persistent wall visual) and the catalog. The catalog validates duplicate IDs and requires a default entry; the move inspector reports missing IDs and orphan wall presentation data.

Wall cues are edge/event driven: Raise only on inactive-to-active transitions; Absorb on mitigation or surviving no-damage hits; Reflect only when reflected damage or AP drain feedback exists; Break at the start of the break beat; and Block as the primary `BarrierBlocked` outcome. `CombatWallVisual` remains visual-only. `CombatSfxOrchestrator.PlayWallCue` deduplicates timestamp/target/source/cue requests.

## Legacy Migration

`MoveDefinitionSO.castSound`, `releaseSound`, and `travelSound` are hidden legacy fields and warn when assigned. Migrate them to phase-start sounds, then remove them after content migration. Keep `impactAccentSound` only for temporary compatibility while authored outcome/track audio is moved to the new layers. `PlayOutcomeBatch` is the authoritative orchestrator entry point; convenience wrappers are compatibility APIs and should not receive new callers.

## Runtime Architecture

`CombatChainlingVisual`
- Owns flipbook playback.
- Emits frame advancement markers for combat presentation timing.
- Stores the currently applied `CombatVisualProfileSO`.

`CombatVisualProfileSO`
- Has an optional `presentationTrack` field for species-level presentation.
- Has optional `anchorOffsets` for per-species anchor refinement.

`MoveDefinitionSO`
- Has an optional `presentationTrack` field for move-specific frame presentation.
- The move track is only active while that move is resolving.

`CombatPresentationTrackSO`
- Holds a flat list of `CombatPresentationEvent` entries.
- Events are keyed by `animationState + frameIndex`.

`CombatPresentationDirector`
- Subscribes to combat slot visuals.
- Layers `profile.presentationTrack` with the active move's `presentationTrack`.
- Dispatches sprite VFX, audio bridge calls, gameplay markers, and debug notes.

`CombatPresentationPoseUtility`
- Resolves anchors from `Combat2ChainlingView`.
- Applies `CombatVisualProfileSO.anchorOffsets` before the event-local `offset`.
- Keeps preview/runtime pose resolution aligned.

`ICombatAudioBridge` + `CombatAudioBridge`
- Keeps runtime presentation decoupled from `SoundEventSO` and the audio service.
- Supports one-shots plus loop start/stop tokens.

`SpriteFlipbookVfxPlayer`
- Spawns lightweight sprite flipbook VFX using the same material-property-block pattern used by combat visuals.

## Authoring Workflow

1. Open `Chainbound/Combat/Presentation Authoring Tool`.
2. Select a `CombatVisualProfileSO`.
3. If you are authoring a move-specific overlay, also select a `MoveDefinitionSO`.
4. Set `Track Target` to `Profile` or `Move`.
5. Assign an existing `CombatPresentationTrackSO`, or click `Create Track`.
6. Choose the animation state you want to author.
7. Scrub or play through the frame strip.
8. Select a frame and click `Add Event`.
9. Set the event family, anchor, playback space, payload, offset, and note.
10. Save the track asset.
11. Use `Preview in Previewer` to jump straight into the lightweight animation previewer at the current state/frame.
12. In the previewer, use `Edit in Authoring Tool` to come back when you need full asset editing.

Recommended split:
- `Profile` track: baseline species flavor that should happen for every use of that animation.
- `Move` track: move-identity presentation such as unique slash bursts, cast cues, muzzle flashes, or move-only impact notes.

## Event Rules

`PresentationEventFamily.VFX_Sprite`
- Payload must be a `SpriteFlipbookVfxDefinitionSO`.
- Works in preview and battle runtime.

`PresentationEventFamily.VFX_Graph`
- Payload must be a `GraphVfxDefinitionSO`.
- Works in the previewer.
- Battle runtime dispatch is still a stub until `CombatPresentationDirector.DispatchGraphVfx(...)` is finished.

`PresentationEventFamily.Audio`
- Payload must be a `SoundEventSO`.
- `PlayOneShot` is fire-and-forget.
- `StartLoop` and `StopLoop` must share the same `eventId`.

`PresentationEventFamily.GameplayMarker`
- No payload required.
- Use this for timing markers that higher-level combat systems may consume.

`PresentationEventFamily.DebugNote`
- No payload required.
- Useful during tuning and instrumentation.

`PresentationEventFamily.PostFx`
- Payload must be a `CombatPostFxCueSO`.
- Plays through `CombatPostFxVolumeController` (combat-owned Volume weight blend).
- Does not affect TimeOfDay / GameServices Global Volume.

## Anchors And Playback

`SelfRoot`
- Uses the visual transform itself.

`CameraFocus`, `MeleeStop`, `Impact`, `ProjectileOrigin`, `Barrier`
- Resolve through the existing `Combat2ChainlingView` anchor accessors.

`anchorOffsets` on `CombatVisualProfileSO`
- Apply per-species local adjustments to those anchors.
- Mirror automatically on X when the visual flips.
- Are resolved before the event-local `offset`.

`offset` on `CombatPresentationEvent`
- Use this for per-event fine tuning after the species anchor offset is applied.

`AtSelf` / `AtAnchor`
- Follows the resolved transform.

`World`
- Spawns at the resolved position but does not follow after spawn.

`Global2D`
- Intended for non-positional audio or HUD-adjacent calls.

## Conventions

- Keep `eventId` stable for any audio loop start/stop pair.
- Use profile tracks for baseline species presentation.
- Use move tracks for move identity and interchangeability across species.
- Use the Animation Previewer pair mode to validate attacker/defender timing against `CombatVFXLibrary` before doing full battle tests.
- Promote temporary previewer events into profile or move tracks once the frame timing is proven.
- Use `anchorOffsets` for per-species anchor correction.
- Use event `offset` for one-off placement refinements.
- Leave tracks null on profiles or moves that do not need custom presentation.

## Current Limits

- Frame-authored dispatch still depends on the flipbook frame events emitted by `CombatChainlingVisual`.
- Graph VFX runtime playback is still not finished.
- Sprite VFX uses simple instantiate-and-destroy playback rather than pooling.
- The system gives you strong per-species and per-move control, but not true per-frame limb sockets inside the flipbook.
- Combat Post FX is weight-blend only (no UI fullscreen flipbook overlay path).
