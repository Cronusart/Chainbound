# Combat Animation Previewer

## Purpose

`Tools/Chainbound/Combat/Animation Previewer` is an editor-only preview surface for combat body animation, authored presentation events, and paired move VFX timing.

Use it when you want to inspect:

- `CombatVisualProfileSO` idle, melee, ranged, and hit flipbooks
- melee contact and ranged release marker timing
- `Combat2ChainlingView` anchor placement
- `CombatVisualProfileSO.anchorOffsets`
- profile-level and move-level `CombatPresentationTrackSO` events
- temporary preview-only VFX/SFX/debug events before promoting them into saved tracks
- `CombatVFXLibrary` cast/strike/projectile/impact prefab phases in a paired attacker-vs-defender preview

It is intentionally lighter than `Tools/Chainbound/Combat/Presentation Authoring Tool`. The previewer is for timing and feel; the authoring tool is for direct saved-track editing.

## Quick Start

1. Open `Tools/Chainbound/Combat/Animation Previewer`.
2. Assign `Attacker Profile`.
   - Good starter choices:
     - `Assets/Data/Combat/ChainlingSpecies/Visual/Gilarime/Visual_Gilarime.asset`
     - `Assets/Data/Combat/ChainlingSpecies/Visual/Validape/Visual_Validape.asset`
     - `Assets/Data/Combat/ChainlingSpecies/Visual/Cinislug/Visual_Cinislug.asset`
3. Pick `Idle`, `Melee`, `Ranged`, or `Hit`.
4. Use `Play`, `Pause`, `Stop`, `Prev`, `Next`, or the frame slider.
5. Use mouse wheel over the viewport, `+`, `-`, or the zoom slider to frame the subject.
6. Toggle `Show Anchor Overlay` and click anchor buttons in the right panel to inspect placement.
7. Toggle `Replay Authored Tracks` to include or ignore saved profile/move presentation events.

For a paired move preview:

1. Assign `Attacker Profile`.
2. Assign `Defender Profile`.
3. Assign `Move`.
   - `Assets/Data/Combat/Moves/Move_baseline_striker.asset`
   - `Assets/Data/Combat/Moves/Move_cipher_shot.asset`
4. Assign `VFX Library`.
   - `Assets/Data/Combat/VFX/Library/CombatVFXLibrary.asset`
5. Press `Play Attack Pair`.

`baseline_striker` previews the pixel cast pulse, strike slash, and impact burst. `cipher_shot` previews the pixel cast pulse, projectile bolt, and impact burst.

Important: the regular `Play` button only plays the selected body animation state. It does not run a combat move or spawn `CombatVFXLibrary` prefabs. To preview a move, use `Play Attack Pair` in the left panel's `Attack Pair` section.

## Window Layout

Left panel:

- `Attacker Profile`: main `CombatVisualProfileSO` to preview.
- `Defender Profile`: optional second profile used by `Play Attack Pair`.
- `Move`: optional `MoveDefinitionSO`; sets move category automatically and enables move-track replay.
- `Preview Template`: optional `Combat2ChainlingView` prefab/template. If unset, the previewer creates a procedural preview subject.
- `VFX Library`: `CombatVFXLibrary` used only by paired attack preview.
- `Move Category`: used for attack-state selection and category fallback VFX lookup.
- `Loop Preview`, `Flip X`, `Replay Authored Tracks`, `Show Anchor Overlay`, `Deterministic Audio`.
- info readout for frame count, FPS, duration, sprite size, profile track, move track, and pair timing.

Center panel:

- IMGUI preview viewport rendered through `PreviewRenderUtility`.
- transport controls.
- `Replay Frame` to re-fire frame events on the current frame.
- `Clear Spawned` to remove temporary preview VFX/audio markers.

Right panel:

- current-frame events grouped as `[Profile]`, `[Move]`, or `[Preview]`.
- editable inspector for preview-only events.
- read-only inspector for authored events.
- `Promote to Profile Track` / `Promote to Move Track` for temporary events.
- `Edit in Authoring Tool` to jump to the full presentation-track editor.
- anchor position and profile-offset readout.
- debug log.

Timeline:

- one cell per animation frame.
- blue highlight for the current frame.
- red and blue marker badges for melee contact and ranged release frames.
- event bars for profile, move, and preview events.

## Script Map

Editor previewer:

- `Assets/Editor/CombatPreview/CombatAnimationPreviewerWindow.cs`
  - IMGUI window, fields, controls, promotion, tool handoff.
- `Assets/Editor/CombatPreview/CombatAnimationPreviewSession.cs`
  - owns preview state, preview rendering, frame stepping, event dispatch, spawned preview objects.
- `Assets/Editor/CombatPreview/CombatAnimationPreviewSubject.cs`
  - creates the hidden preview subject, ensures anchors, applies profiles, spawns preview prefabs/flipbooks/graph VFX.
- `Assets/Editor/CombatPreview/CombatPreviewEventSimulator.cs`
  - merges profile track, move track, and session-only temporary events for the current frame.
- `Assets/Editor/CombatPreview/CombatPreviewPairOrchestrator.cs`
  - paired attacker/defender sequence, `CombatVFXLibrary` lookup, phase prefab spawning, and shared travel-path setup.
- `Assets/Editor/CombatPreview/CombatPreviewAudioPlayer.cs`
  - editor-safe audio preview.
- `Assets/Editor/CombatPreview/CombatPreviewGizmos.cs`
  - anchor and marker overlay drawing.

Runtime systems being previewed:

- `Assets/Scripts/Combat/Runtime/Presentation/CombatChainlingVisual.cs`
  - animation timing source.
- `Assets/Scripts/Combat/Runtime/Presentation/Combat2ChainlingView.cs`
  - combat view wrapper and base anchors.
- `Assets/Scripts/Combat/Runtime/Presentation/CombatPresentationPoseUtility.cs`
  - resolves event anchor + offset positions.
- `Assets/Scripts/Combat/Runtime/Presentation/SpriteFlipbookVfxPlayer.cs`
  - runtime/editor sprite VFX player for `VFX_Sprite` events.
- `Assets/Scripts/Combat/Runtime/Presentation/CombatMoveVFX.cs`
  - prefab VFX lifecycle, sprite playback, billboarding, and phase travel for `CombatVFXLibrary` pair preview.
- `Assets/Scripts/Combat/Runtime/Presentation/CombatVFXLibrary.cs`
  - move/category lookup for cast/strike/projectile/impact phase prefabs.
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatVisualProfileSO.cs`
  - body animation profiles, anchor offsets, optional profile presentation track.
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/MoveDefinitionSO.cs`
  - move ID/category and optional move presentation track.
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatPresentationTrackSO.cs`
  - saved frame event lists.
- `Assets/Scripts/Combat/Runtime/Data/CombatPresentationEvent.cs`
  - per-frame VFX/SFX/gameplay/debug event payload.

## Event Previewing

The frame-event path previews saved profile/move tracks and temporary preview-only events.

Supported frame-event families:

- `VFX_Sprite`
  - payload must be `SpriteFlipbookVfxDefinitionSO`.
  - preview uses `SpriteFlipbookVfxPlayer`.
- `VFX_Graph`
  - payload must be `GraphVfxDefinitionSO`.
  - preview instantiates the graph prefab.
  - battle runtime graph dispatch is still not finished.
- `Audio`
  - payload must be `SoundEventSO`.
  - one-shot preview is the most reliable editor path.
- `GameplayMarker`
  - logged only.
- `DebugNote`
  - logged only.

Temporary preview events are not saved until promoted. Promotion can create and assign a missing presentation track.

## Previewing Combat Moves

The previewer has two different playback modes:

- Solo animation playback
  - driven by the `Idle`, `Melee`, `Ranged`, and `Hit` state buttons plus the normal `Play` transport.
  - useful for checking a single `CombatVisualProfileSO` and authored frame events.
  - assigning `Move` here only adds the move's `presentationTrack` to frame-event replay and auto-selects `Move Category`.
- Paired move playback
  - driven by `Play Attack Pair`.
  - useful for checking a move's attacker animation, defender hit reaction, cast/strike/projectile/impact VFX, and move presentation events together.
  - this is the path that spawns `CombatVFXLibrary` prefabs.

To preview a combat move:

1. Open `Tools/Chainbound/Combat/Animation Previewer`.
2. In the left panel, assign `Attacker Profile`.
3. Assign `Defender Profile`.
4. Assign `Move`.
5. Assign `VFX Library`.
6. Confirm the `Attack Pair` section shows timing values.
7. Press `Play Attack Pair`.

If `Play Attack Pair` is disabled, one of these is missing:

- `Attacker Profile`
- `Defender Profile`
- `Move`
- `VFX Library`

For the Pixel Battle smoke test:

- `Attacker Profile`: `Assets/Data/Combat/ChainlingSpecies/Visual/Gilarime/Visual_Gilarime.asset`
- `Defender Profile`: `Assets/Data/Combat/ChainlingSpecies/Visual/Validape/Visual_Validape.asset`
- melee move: `Assets/Data/Combat/Moves/Move_baseline_striker.asset`
- ranged move: `Assets/Data/Combat/Moves/Move_cipher_shot.asset`
- `VFX Library`: `Assets/Data/Combat/VFX/Library/CombatVFXLibrary.asset`

Expected results:

- `Move_baseline_striker`
  - attacker plays `MeleeAttack`
  - cast pulse appears near the attacker at move start
  - strike slash starts at attacker camera focus on the melee contact marker and its final frame lands at defender impact
  - impact burst appears at the defender impact timing
  - no projectile appears
- `Move_cipher_shot`
  - attacker plays `RangedAttack`
  - cast pulse appears near the attacker at move start
  - projectile bolt travels from attacker projectile origin to defender impact anchor
  - impact burst appears at the defender

The paired VFX anchors come from the preview template's `Combat2ChainlingView`. `CameraFocus` and `ImpactAnchor` must point into the Chainling's `VisualRoot`; the `Targeter` child is reserved for ground selection and must not be used for VFX placement.

For travel timing experiments, temporarily set a phase to `Constant Speed` and author a speed such as `10` world units per second. The previewer resolves the start/end anchors once, so increasing the slot distance lengthens the preview while keeping the same average speed. Use `Fit Frame Span To Travel` for a projectile or slash whose final unique frame should reach the destination, or `Fit Effect Lifetime To Travel` when the final frame's full display interval belongs inside the travel.

Combat move positions are authored from the player's non-mirrored perspective. During pair preview, the VFX source uses the attacker subject's `CombatChainlingVisual.FlipX` value, so the previewer mirrors the VFX root and authored X offsets when `Flip X` is active in solo mode. Y and depth corrections remain unchanged. This is the same facing rule used by live combat, allowing one move entry and one prefab orientation to preview correctly for either side.

If the body animation plays but VFX do not appear:

- Make sure you pressed `Play Attack Pair`, not the regular transport `Play`.
- Make sure `VFX Library` is assigned; it is not inferred from the move asset.
- Make sure the move's `moveId` has an exact entry in `CombatVFXLibrary.asset`, or a category fallback entry exists.
- Make sure the relevant phase config has `enabled` checked and a `vfxPrefab` assigned.
- Check `Move Category`; assigning a move auto-fills it, but manual changes can change melee/ranged behavior.
- Turn on `Show Anchor Overlay` and adjust `Zoom`; some effects may spawn at attacker/target anchors outside the current framing.
- Use `Clear Spawned` between repeated tests if persistent or long-lived effects are cluttering the preview.

## Diagnosing Move VFX Scale

If a move preview plays but the cast/strike/projectile/impact VFX look too small, check scale before assuming the previewer axis is wrong.

The Chainling body sprites are usually authored as large world-space quads. For example, current starter profiles such as Gilarime and Validape use `spriteSize: 5 x 5`. The Pixel Battle starter VFX prefabs are authored closer to one world unit:

- `PF_PixelBattle_StrikeSlash`
  - `StrikeSlash` uses `Sprite Size (World Units)` `5 x 5` as its base renderer envelope.
  - Its `Sprite Canvas Size (Pixels)` is explicitly `96 x 69`, and its `Sprite Aspect Mode` is `Preserve Authored Height`. The canvas ratio is shared by every frame; transparent padding and opaque pixel bounds are not resized independently.
  - `baseline_striker` strike phase applies `scaleMultiplier: 1.15`; phase scale is separate from the child renderer's canvas sizing.
- `PF_PixelBattle_ImpactBurst`
  - `ImpactPixels` local scale is roughly `1.65`.
  - `baseline_striker` and `cipher_shot` impact phases apply their own `scaleMultiplier`.
- `PF_PixelBattle_CastPulse`
  - particle child scale is roughly `1.55`.
  - `baseline_striker` cast phase applies `scaleMultiplier: 1.15`.
  - `cipher_shot` cast phase applies `scaleMultiplier: 1.05`.
- `PF_PixelBattle_ProjectileBolt`
  - bolt core is roughly `1.75 x 0.45`.
  - trail particle child scale is roughly `1.35`.
  - `cipher_shot` projectile phase applies `scaleMultiplier: 1.2`, so the bolt core becomes roughly `2.1 x 0.54` world units before the texture mask shape is considered.

Against a `5 x 5` Chainling body, those values are intentionally small starter marks, not full-body-sized attacks. The preview camera frames both attacker and defender based on their profile `spriteSize`, so the camera can make that mismatch easier to notice, but it does not change the relative size of the VFX versus the Chainling.

The assigned `CombatVFXLibrary.asset` also applies a shared **World Scale Multiplier** of `1.2`, matching the scaled Chainling `Billboard` visual in `Combat2Battlefield`. This multiplies each phase root in both pair preview and live combat, including sprite sequences, particles, and travel. The phase `scaleMultiplier` remains the additional per-phase adjustment; neither setting changes the Pixel Battle canvas dimensions or shader grid density.

Use this order when diagnosing:

1. Check the Chainling profile `spriteSize`.
2. Check the prefab child `Transform.localScale`.
3. Check the `CombatVFXLibrary.VFXPhaseConfig.scaleMultiplier` and edit its **Phase Scale Curve** when the effect should grow, shrink, or overshoot during playback. A constant curve value of `1` preserves the configured phase size.
4. Check the phase `spawnOffset` when the VFX is anchored correctly but sits too high or low relative to the billboarded Chainling sprite. Positive Y raises the effect in world space.
5. Check the phase `cameraDepthOffset` and `sortingOrderOffset` if the VFX appears behind the Chainling body.
6. For Pixel Battle shader materials, check material `Z Test`. Starter battle VFX use `Always` so they draw above depth-writing Chainling body sprites; use `LessEqual` only for effects that should be hidden by scene depth.
7. For moving phases, check Travel start/end anchors, endpoint offsets, and start/end scale multipliers. The phase-wide `spawnOffset` applies to both endpoints.
8. Check the phase `anchor` and `attachPolicy` for static placement.
9. Use `Show Anchor Overlay` to confirm whether the VFX is spawning at the expected anchor.
10. Adjust preview `Zoom` only after the relative object sizes look correct.

For a frame-authored `CombatMoveVFX` sprite that looks stretched, inspect **Sprite Canvas Size (Pixels)** and **Sprite Aspect Mode** before changing phase or prefab scale. Canvas Size should match the authored PNG canvas, while Sprite Size remains world-space sizing. `Preserve Authored Height` is usually the right choice for hand-drawn slash frames; `Fit Inside Authored Bounds` is useful where frames vary in aspect but must stay within a consistent visual envelope. This sizing is local to the child mesh renderer and does not alter the phase's Travel path or billboard behavior.

Axis or positioning issues usually look different:

- wrong anchor: the VFX appears at the attacker, defender, midpoint, or barrier when it should be elsewhere.
- wrong depth: the VFX appears hidden behind a body sprite or flickers against it. For Pixel Battle materials, confirm `Z Test` is `Always`; then adjust `cameraDepthOffset` and `sortingOrderOffset` if needed.
- wrong rotation/plane: the VFX appears edge-on, invisible from the camera, or angled incorrectly.
- wrong scale: the VFX is at the right place but visibly too small or too large.

For the current Pixel Battle starter VFX, the first tuning pass should usually adjust prefab child scale and/or phase `scaleMultiplier` for size, phase `spawnOffset` for vertical/horizontal placement, the shared Pixel Battle style plus preset `pixelCellMultiplier` for chunkiness, and material `Z Test` plus phase `cameraDepthOffset`/`sortingOrderOffset` for front/back layering. Use anchor changes only if the VFX is landing on the wrong combat object.

## CombatVFXLibrary Phase Configs

Each `CombatVFXLibrary.VFXEntry` is phase-only:

- `castVfx`
- `strikeVfx`
- `projectileVfx`
- `impactVfx`

Cast fires at move start. Melee Strike fires at the melee contact marker. Ranged Projectile fires at the ranged release marker. Impact remains tied to move resolution. A disabled or unassigned phase simply does not spawn.

### Travel In Pair Preview

Each phase can enable Travel independently. Pair preview resolves the exact same start/end anchors, phase-wide `spawnOffset`, endpoint offsets, depth correction, duration source, speed, Travel Progress / Velocity Profile, world-Y arc, sprite timing mode, phase scale curve, and Travel scale multipliers as live combat.

- Default endpoint behavior is **snapshot**: moving the preview subjects after spawn does not move the visual path.
- Enable an endpoint's **Follow Anchor** option to test homing or tether behavior against a moving subject.
- **Sprite Frame Span** makes the final sprite frame reach the end anchor. **Effect Lifetime** uses the whole prefab lifetime. **Explicit Seconds** is best for particle-only effects. **Constant Speed** derives the duration from the resolved path length.
- **Fit Frame Span To Travel** makes the final unique sprite frame arrive at the endpoint. **Fit Effect Lifetime To Travel** includes the final-frame display interval within the travel.
- Travel is world-space and does not attach the spawned VFX to a Chainling. Local sprite animation within the prefab remains independent and can add slash smear or shape motion.

Use `Fire Cast`, `Fire Strike`, `Fire Projectile`, and `Fire Impact` to isolate a phase's path. Temporary phase prefab overrides preserve the selected entry's Travel settings, so they are useful for comparing alternate art without modifying the library asset.

## Pair Previewing

`Play Attack Pair` is separate from frame-event previewing.

It requires:

- attacker profile
- defender profile
- move definition
- VFX library

The pair preview:

1. Chooses `MeleeAttack` for melee categories and `RangedAttack` for `DamagingRanged`.
2. Uses the attacker marker time as the strike/release time.
3. Looks up `CombatVFXLibrary.GetEntryWithFallback(move.moveId, move.category)`.
4. Spawns explicit `Cast` at move start.
5. Spawns `Strike` at the melee contact marker for melee moves.
6. Spawns `Projectile` at the ranged release marker for ranged moves.
7. Uses explicit `impactDelay`, or the configured Strike/Projectile travel duration when one is available.
8. Spawns `Impact` and plays the defender `Hit` animation.

This is the best current path for previewing `CombatMoveVFX` prefabs, including the new Pixel Battle VFX prefabs.

Phase spawns honor `CombatVFXLibrary.VFXPhaseConfig.spawnDelay`. Delays are measured from the pair move-start, strike/release, or impact trigger time, matching the runtime presenter behavior.

The `Move VFX Phases` section can also fire individual phases:

- `Fire Cast`
- `Fire Strike`
- `Fire Projectile`
- `Fire Impact`

Use the phase override slots to temporarily test another prefab for a phase. Null override slots use `CombatVFXLibrary.asset`. Override slots are preview-only and do not modify the library.

`DamagingMelee` remains the move category. `Strike` is a VFX phase that describes melee contact timing; it is not a new move category.

## Pixel Battle VFX Integration

The new pixel shader VFX assets are already wired into `CombatVFXLibrary.asset`:

- `baseline_striker`
  - `PF_PixelBattle_CastPulse`
  - `PF_PixelBattle_StrikeSlash`
  - `PF_PixelBattle_ImpactBurst`
  - no projectile
- `cipher_shot`
  - `PF_PixelBattle_CastPulse`
  - `PF_PixelBattle_ProjectileBolt`
  - `PF_PixelBattle_ImpactBurst`

Preview them through the pair-preview path:

1. Open `Tools/Chainbound/Combat/Animation Previewer`.
2. Assign an attacker visual profile.
3. Assign a defender visual profile.
4. Assign `Move_baseline_striker` or `Move_cipher_shot`.
5. Assign `CombatVFXLibrary.asset`.
6. Press `Play Attack Pair`.

Why this works:

- pair preview uses `CombatVFXLibrary` phase configs, which point at prefab assets.
- the Pixel Battle prefabs are `CombatMoveVFX` prefabs.
- the previewer manually initializes `CombatMoveVFX` and calls `StepPlayback`.
- the previewer applies any `PixelBattleVfxApplicator` components after prefab instantiation, so preset-driven material/property-block overrides show in the preview window.
- the previewer manually simulates child `ParticleSystem` components on active `CombatMoveVFX` prefabs, so particle bursts and trails can be judged in editor preview.
- pair preview resolves the same Travel config as live combat and calls `ConfigureTravel(...)`, so any phase can move between independent endpoints.

## Pixel VFX Constraints

These are the current constraints to keep in mind while using the previewer for shader VFX work:

- Frame-authored `VFX_Sprite` events cannot directly use `CombatMoveVFX` prefabs. They require `SpriteFlipbookVfxDefinitionSO`.
- Pair preview can use `CombatMoveVFX` prefabs because it comes from `CombatVFXLibrary`, not a frame-event payload.
- Child `ParticleSystem` components are simulated manually while their parent `CombatMoveVFX` remains active. This is intended for play-on-awake combat bursts/trails; custom script-driven particle triggers still need their own preview support.
- `PixelBattleVfxApplicator` is applied once after prefab instantiation. Runtime changes made later by a custom VFX script would need an explicit preview hook.
- `SpriteFlipbookVfxDefinitionSO.pixelBattlePreset` can use the same Pixel Battle material and preset contract as phase prefabs. `SpriteFlipbookVfxPlayer` preserves the source frame, atlas transform, and preset tint unless the preset explicitly opts into a source-texture override.
- The preview viewport uses editor preview rendering. If a URP-only shader appears blank in this window but works in Game view, check render-pipeline preview compatibility before debugging the VFX data.
- Anchor placement is approximate. It uses combat view anchors plus profile-local offsets, not true frame-by-frame sockets attached to limbs or weapon tips.

## Recommended Pixel VFX Workflow

For shader, style, source-texture, and material authoring details, see [Pixel Battle VFX System](Pixel_Battle_VFX_System.md).

Use two loops:

1. Library/prefab loop
   - Use `CombatVFXLibrary` + `Play Attack Pair`.
   - Best for cast, projectile, and impact prefab composition.
   - Best for the new pixel shader prefab family.

2. Frame-event loop
   - Use temporary preview events.
   - Payload with `SpriteFlipbookVfxDefinitionSO`, `GraphVfxDefinitionSO`, or `SoundEventSO`.
   - Promote good timing into profile or move presentation tracks.
   - Best for precise per-frame sparkle, slash frame, sound, marker, or graph-timing work.

If a move needs both:

- put broad combat-phase identity in `CombatVFXLibrary` as cast/strike/projectile/impact prefabs.
- put exact frame accents in the move `CombatPresentationTrackSO`.
- use profile tracks only for species-flavor events that should happen regardless of move.

## Best Practices

- Start with a known profile and known move before tuning anchors.
- Use `Move_baseline_striker` for melee timing and impact readability.
- Use `Move_cipher_shot` for projectile travel and ranged release timing.
- Keep `Replay Authored Tracks` on when validating final behavior.
- Turn it off when isolating temporary preview events.
- Use `Replay Frame` after editing a preview event payload or offset.
- Use `Clear Spawned` frequently when testing persistent or long-lived effects.
- Tune profile `anchorOffsets` for species-level correction.
- Tune event `offset` for move-specific correction.
- Use `Edit in Authoring Tool` once timing is right and you need durable track edits.

## Useful Next Improvements

The previewer can already preview the Pixel Battle VFX set through pair preview. The most valuable future improvements would be:

- add a frame-event family or payload route for prefab VFX if we want per-frame `CombatMoveVFX` prefab dispatch outside `CombatVFXLibrary`
- optionally expose a one-click `Load Pixel Battle Demo` preset that fills attacker, defender, move, and VFX library fields
