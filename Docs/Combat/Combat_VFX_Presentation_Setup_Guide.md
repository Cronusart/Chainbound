# Combat VFX Presentation Setup Guide

This guide describes what the current combat presentation stack can do today, what is only partially supported, and what is still missing.

## Short answer

### 1. Assign specific VFX to a move
Yes, with one important runtime caveat.

What works now:
- You can assign a `CombatPresentationTrackSO` directly on `MoveDefinitionSO.presentationTrack`.
- That move track layers on top of `CombatVisualProfileSO.presentationTrack` while that move is resolving.
- Sprite VFX and audio frame events work in battle runtime.
- Graph VFX can be previewed in the Combat Animation Previewer.

Current limitation:
- Graph VFX battle runtime dispatch is still a stub until `CombatPresentationDirector.DispatchGraphVfx(...)` is finished.

Practical conclusion:
- `Per move sprite VFX`: yes.
- `Per move SFX`: yes.
- `Per move graph VFX in previewer`: yes.
- `Per move graph VFX in battle runtime`: not yet.

### 2. Fire VFX, sprite effects, and SFX from a specific animation frame
Yes.

What works now:
- `CombatChainlingVisual` remains the timing source.
- `CombatPresentationDirector` listens to frame advancement.
- It merges the acting profile track with the active move track for the current frame.
- Matching events dispatch from that exact frame.

Practical conclusion:
- `Sprite flipbook VFX on frame`: yes.
- `SoundEventSO on frame`: yes.
- `Move-specific frame triggers`: yes.
- `Graph VFX on frame in previewer`: yes.
- `Graph VFX on frame in battle runtime`: not yet.

### 3. Set an anchor point on the Chainling visual so effects originate from a precise place
Partially yes, and the answer is clearer now than before.

What anchors mean today:
- Base anchors still come from `Combat2ChainlingView` transforms.
- `CombatVisualProfileSO.anchorOffsets` now let you refine those anchors per species profile.
- Event-local `offset` still gives you one-off placement on top of the profile anchor offset.

What this means in practice:
- You can set a species-specific slash, muzzle, impact, or wall contact origin by adjusting the relevant profile anchor offset.
- You can then fine-tune a specific move event with its own offset.
- This is strong enough for "spawn the slash from the weapon side of this species".

What it does not mean:
- It is still not a true animated socket on the flipbook mesh.
- It does not track a sword tip frame-by-frame through the sprite sequence.
- It is not yet a per-frame socket authoring system.

Practical conclusion:
- `Approximate slash origin from a chosen point on the character`: yes.
- `Species-specific anchor correction`: yes.
- `True sword tip socket that tracks the weapon through the 2D flipbook`: not with the current model.

## Current architecture

### Runtime timing source
`CombatChainlingVisual` is the source of truth for frame playback and animation timing.

Relevant files:
- `Assets/Scripts/Combat/Runtime/Presentation/CombatChainlingVisual.cs`
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatVisualProfileSO.cs`

### Frame-authored event dispatch
`CombatPresentationDirector` subscribes to `CombatChainlingVisual`, merges the current profile track with the active move track, and dispatches matching events.

Relevant files:
- `Assets/Scripts/Combat/Runtime/Presentation/CombatPresentationDirector.cs`
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatPresentationTrackSO.cs`
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/MoveDefinitionSO.cs`
- `Assets/Scripts/Combat/Runtime/Data/CombatPresentationEvent.cs`
- `Assets/Scripts/Combat/Runtime/Data/CombatPresentationEnums.cs`

### Anchor source and refinement
Base anchors come from `Combat2ChainlingView`. Species-specific anchor correction comes from `CombatVisualProfileSO.anchorOffsets`, resolved through `CombatPresentationPoseUtility`.

Relevant files:
- `Assets/Scripts/Combat/Runtime/Presentation/Combat2ChainlingView.cs`
- `Assets/Scripts/Combat/Runtime/Presentation/CombatPresentationPoseUtility.cs`
- `Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatVisualProfileSO.cs`

### Sprite VFX runtime
Sprite VFX are spawned by `SpriteFlipbookVfxPlayer`.

Relevant file:
- `Assets/Scripts/Combat/Runtime/Presentation/SpriteFlipbookVfxPlayer.cs`

### Audio runtime
Audio payloads are existing `SoundEventSO` assets played through `CombatAudioBridge`.

Relevant file:
- `Assets/Scripts/Audio/Components/CombatAudioBridge.cs`

### Graph VFX preview support
The Combat Animation Previewer can instantiate graph VFX prefabs for timing checks.

Relevant files:
- `Assets/Editor/CombatPreview/CombatAnimationPreviewSession.cs`
- `Assets/Editor/CombatPreview/CombatAnimationPreviewSubject.cs`

## Recommended setup workflow today

### A. Create the combat visual profile
1. Create or open a `CombatVisualProfileSO`.
2. Assign the sprite sequences for:
   - `Idle`
   - `MeleeAttack`
   - `RangedAttack`
   - `Hit`
3. Tune FPS for each state.
4. Add any needed `anchorOffsets` for species-specific placement correction.
5. Optionally assign `presentationTrack` for baseline species events.

Use the profile track for flavor that should always accompany that species animation.

### B. Validate the move in the Animation Previewer
1. Open `Chainbound/Combat/Animation Previewer`.
2. Assign the attacker profile, defender profile, move, and `CombatVFXLibrary`.
3. Set the move category if you want to test a category fallback entry.
4. Use `Play Attack Pair` to validate cast timing, projectile travel, impact delay, and defender hit timing.
5. Add temporary preview events if you want to experiment before saving anything.
6. Promote successful preview events into the profile or move track from the previewer.

### C. Create the move-specific track when needed
1. Create or open a `MoveDefinitionSO`.
2. Create a `CombatPresentationTrackSO`.
3. Assign it to `MoveDefinitionSO.presentationTrack`.
4. Open `Chainbound/Combat/Presentation Authoring Tool`.
5. Load the profile used for the animation frames.
6. Load the move definition.
7. Set `Track Target = Move`.

### D. Author wall presentation when needed
Wall visuals now live outside the move VFX library phases. Use them this way:
1. Create or open a `CombatWallVisualProfileSO` for shared default wall sprite/material/tint/animation timing.
2. Assign the profile on `Combat2BattlefieldPresenter.wallVisualProfile`.
3. For a move that places a wall (`MitigationWindow`, `FullAbsorbBarrier`, `WALL`, or `FULL_ABSORB_WALL`), set `MoveDefinitionSO.wallPresentation` if it needs custom wall art.
4. Leave `wallPresentation.wallSprite` empty when the move should use the shared profile default.

Normal move identity still comes from cast/projectile/impact VFX. When a hit resolves against a wall, the presenter routes the regular `Impact` phase to either `BarrierAnchor` or the body anchor from the resolver payload. The retired wall-only VFX phases are `PersistentBarrier`, `BarrierImpact`, and `BarrierBreak`.

### E. Author Chainling cosmetic animation slots
Combat body animation cosmetics are resolved through `ChainlingVisualIdentity` and `ChainlingVisualIdentityDatabaseSO`, not the move VFX library.

Use identity slots this way:
1. Create a melee, ranged, hit reaction, swap-in, swap-out, KO, or attacker-KO taunt asset under the Chainling cosmetics menus.
2. Add the asset to the matching list on `ChainlingVisualIdentityDatabaseSO`.
3. For variant-specific body sprite strips, author a `CombatVisualProfileSO.animationAlternates` row keyed by the equipped style ID and animation slot.
4. For melee/ranged alternates or embedded style clips, set `markerFrame` only when the authored clip has a known contact/release frame; leave it at `-1` for midpoint fallback.
5. Equip the matching identity ID on `meleeAttackStyleId`, `rangedAttackStyleId`, `hitReactionStyleId`, `swapInFxId`, `swapOutFxId`, `koFxId`, or `attackerKoStyleId`.

Swap-in, swap-out, and KO assets can still carry additive prefab/flipbook effects. Their optional `bodySequence`/`bodyFps` fields remain a migration fallback; new variant-correct body strips should live on the active `CombatVisualProfileSO` alternate table.

Attacker KO taunt uses `attackerKoStyleId` only when the KO event has direct attacker attribution. Battle-end victory still uses `victoryPoseId`.

### F. Create baseline species events when needed
1. Open `Chainbound/Combat/Presentation Authoring Tool`.
2. Load the profile.
3. Set `Track Target = Profile`.
4. Create or assign the profile track.
5. Author events on the desired frames.

Use this for baseline species flavor that should happen regardless of which move is mapped onto that animation.

### G. Create sprite VFX
1. Create a `SpriteFlipbookVfxDefinitionSO`.
2. Assign the sprite flipbook frames.
3. Set VFX FPS.
4. Set sprite size.
5. Assign a material if needed.
6. In the relevant profile or move presentation event:
   - set `Family = VFX_Sprite`
   - set `Payload = SpriteFlipbookVfxDefinitionSO`
   - choose anchor + playback space
   - set offset if the effect needs final fine positioning

This path works in battle runtime today.

### H. Create sound triggers
1. Create or reuse a `SoundEventSO`.
2. In the relevant profile or move presentation event:
   - set `Family = Audio`
   - set `Payload = SoundEventSO`
   - set `Audio Action = PlayOneShot` for the common case
   - choose anchor + playback space
   - set offset if needed

This path works in battle runtime today.

### I. Create graph VFX
1. Create a `GraphVfxDefinitionSO`.
2. Assign the VFX Graph prefab.
3. In the relevant profile or move presentation event:
   - set `Family = VFX_Graph`
   - set `Payload = GraphVfxDefinitionSO`
   - choose anchor + playback space
   - set offset if needed

Current status:
- Works for preview iteration in the Combat Animation Previewer.
- Does not yet spawn in actual battle runtime without finishing `CombatPresentationDirector.DispatchGraphVfx(...)`.

## Anchor clarification

### What exists today
The view exposes these base anchors:
- `CameraFocus`
- `MeleeStopAnchor`
- `ImpactAnchor`
- `ProjectileOriginAnchor`
- `BarrierAnchor`
- `SelfRoot` via the visual transform

The profile can then add species-specific local corrections through `anchorOffsets`.

If the view anchors are missing, `Combat2ChainlingView` creates fallback transforms under `VisualRoot` during `Awake()`.

### Best current use of each anchor
- `SelfRoot`: full-body aura, charge-up, status effect, ambient hum
- `ProjectileOrigin`: muzzle flash, slash start, cast emission, thrown object spawn
- `Impact`: hit spark, impact burst, contact sound
- `MeleeStop`: where the attacker visually closes distance
- `Barrier`: wall contact point for routed impact VFX and `CombatWallVisual`
- `CameraFocus`: camera-centric emphasis or elevated overhead effect

### Recommended placement strategy
1. Choose the closest base anchor.
2. Adjust `CombatVisualProfileSO.anchorOffsets` so that anchor is correct for the species.
3. Use event `offset` only for move-specific fine tuning.
4. Verify the result in the Combat Animation Previewer with both the profile and move selected.

For a sword slash example:
- use `ProjectileOrigin` or `Impact` as the base anchor
- push that anchor with a profile-local offset toward the hand or weapon side
- put the slash VFX on the move track frame where contact should happen
- add a small per-event offset only if the move-specific effect still needs final tuning

## What is still missing for true socket-style control

If you want the spawn point to visually follow a sword or limb through the attack frames, you still need one of these:
- per-frame authored anchor offsets
- event payloads with frame-by-frame offset curves
- separate attachment-track data for each animation state

That is a larger feature because the underlying visual is a 2D flipbook on a quad, not a skeletal mesh.

## Recommended decision

If the immediate goal is production-ready iteration, the current stack is now good enough for:
- move-specific sprite VFX timing
- move-specific sound timing
- impact timing
- projectile spawn timing
- species-specific anchor correction
- approximate slash origin placement
- preview validation without entering battle

The next useful improvement is no longer move tracks or profile-local offsets. Those are in place.

The next real gap is:
1. battle-runtime graph VFX dispatch
2. per-frame or socket-style anchor authoring if you need weapon-tip precision
