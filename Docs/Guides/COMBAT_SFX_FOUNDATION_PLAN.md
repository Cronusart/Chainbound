# Combat SFX Foundation Plan

## Purpose

This plan establishes the first shippable layer of battle scene sound effects without trying to solve every sequencing edge case. The goal is to route all combat audio through the existing audio service, keep sounds data-driven, and create stable entry points for universal battle SFX, UI SFX, species SFX, and move SFX.

## Principles

- Route combat sounds through `ICombatAudioBridge` and `AudioService`, not direct `AudioSource` calls.
- Keep authoring data in ScriptableObjects and presentation tracks.
- Treat code as orchestration: it chooses the right sound moment, but assets own the clips, bus, volume, pitch, 3D settings, cooldown, and polyphony.
- Ship a small useful layer first: UI, universal outcomes, turn-start AP, basic spawn/swap/KO, and existing frame-synced presentation tracks.

## Existing Systems To Reuse

- `AudioService` handles playback, mixer routing, source pooling, cooldown, and polyphony.
- `SoundEventSO` defines clips, bus, 3D settings, volume and pitch variance, cooldown, and max instances.
- `ICombatAudioBridge` / `CombatAudioBridge` is the combat-facing bridge into `AudioService`.
- `CombatPresentationDirector` already dispatches frame-synced audio from `CombatPresentationTrackSO`.
- `Combat2BattlefieldPresenter` already owns event-synced combat beats like AP, hit, miss, heal, status, swap, and KO presentation.

## SFX Categories

### UI SFX

Use `AudioBus.UI`.

Examples:
- Move panel navigation
- Confirm
- Cancel / back
- Invalid move
- Target selection change
- Tab / panel change

Foundation target:
- Create a small UI SFX catalog or reuse `AudioLibrarySO` IDs consistently.
- Replace reflection-based ad hoc calls in combat UI with a thin combat UI audio helper that uses the shared bridge/service path.

### Universal Battle SFX

Use `AudioBus.SFX`.

Examples:
- Turn-start AP grant
- AP drain / blocked
- Universal hit
- Universal block
- Universal miss
- Universal heal
- Buff applied
- Debuff applied
- Status immune / cleanse
- Glitch in / glitch out
- Swap in / swap out
- KO impact tail if needed

Foundation target:
- Add a `CombatBattleSfxCatalogSO` for reusable battle outcome sounds.
- Wire presenter handlers to request sounds from the catalog.
- Keep this separate from `CombatBattlePacingProfileSO`; pacing controls timing/color/holds, while the SFX catalog controls sound assets.

### Species / Chainling SFX

Use `AudioBus.SFX`, usually 3D at the acting or affected chainling.

Examples:
- Species spawn voice / chirp
- Melee attack grunt
- Ranged attack charge/grunt
- Hit reaction voice
- KO cry

Foundation target:
- Use `CombatVisualProfileSO.presentationTrack` for frame-synced species audio.
- Prefer frame events for attack sounds because they should match the sprite animation.
- Later, optional fallback fields can be added for spawn/KO if a profile has no presentation track.

### Move-Specific SFX

Use `AudioBus.SFX`, usually 3D at source, target, or an anchor based on the move.

Examples:
- Fire projectile cast
- Electric strike
- Ice wall raise
- Poison pulse
- Cleanse shimmer

Foundation target:
- Use `MoveDefinitionSO.presentationTrack` for move-specific cast and travel sounds.
- Let the move track fire before the universal outcome sound.
- Keep move sound identity separate from species attack identity.

## First-Pass Runtime Ownership

### `CombatPresentationDirector`

Owns animation/frame-synced sounds:
- Species melee/ranged attack sounds
- Move cast sounds
- Move charge/release sounds
- Glitch-in/out events tied to frames

### `Combat2BattlefieldPresenter`

Owns event/outcome-synced sounds:
- Turn-start AP batch
- AP gain/drain/block feedback
- Hit / block / miss
- Heal
- Status applied / immune / cleanse
- Swap and KO outcome beats

### Combat UI Controllers

Own immediate UI response sounds:
- Navigation
- Confirm
- Cancel
- Invalid selection

These should not wait on battle presentation queues.

## Foundation Sequencing Rules

### Standard Damaging Move

1. Species attack SFX from presentation track.
2. Move-specific SFX from move presentation track.
3. Universal outcome SFX from presenter when the resolver event arrives.

Outcome priority:
- Block beats hit.
- Miss beats hit.
- Crit can layer a crit accent with hit, or replace the normal hit if the mix becomes too busy.

### Turn-Start AP Grant

Use the existing batched turn-start AP presentation beat.

Foundation sound:
- One universal AP refresh stinger.
- Optional quiet per-chainling AP tick only if it does not muddy the mix.

Do not play four full-volume AP grant sounds at once.

### Heal

1. Universal heal sound at the target.
2. Optional move-specific heal sound from the move track if the heal came from a move.

### Status

1. Universal buff/debuff/cleanse/immune sound from presenter.
2. Optional move-specific status flavor from the move track.

### Spawn / Swap / KO

Foundation sequence:
- Spawn/swap in: universal glitch-in plus optional species spawn sound.
- Swap out/faint: universal glitch-out plus optional species KO sound.
- KO after damage: universal hit first, then species KO sound after a short delay.

## Suggested ScriptableObject Additions

### `CombatBattleSfxCatalogSO`

Recommended fields:
- `turnStartApGrant`
- `apGain`
- `apDrain`
- `apBlocked`
- `hit`
- `critHit`
- `block`
- `miss`
- `heal`
- `buffApplied`
- `debuffApplied`
- `statusImmune`
- `statusCleanse`
- `glitchIn`
- `glitchOut`
- `swapIn`
- `swapOut`
- `ko`

Recommended tuning:
- `defaultImpactStaggerSeconds`
- `koDelayAfterImpactSeconds`
- `turnStartApVolumeScale`
- `multiTargetVolumeScale`

## First Implementation Milestones

1. Add `CombatBattleSfxCatalogSO` under `Assets/Scripts/Combat/Runtime/ScriptableObjects`.
2. Add a serialized catalog reference to `Combat2BattlefieldPresenter`.
3. Add helper methods to resolve and play universal SFX through `ICombatAudioBridge`.
4. Wire event-synced universal sounds into AP, damage, miss, heal, status, swap, and KO handlers.
5. Keep species and move sounds on existing `CombatPresentationTrackSO` paths.
6. Create placeholder `SoundEventSO` assets with sensible cooldown/polyphony settings.
7. Replace combat UI reflection calls with a small helper that uses the same service path.

## Test Checklist

- UI confirm/cancel/invalid sounds route to `AudioBus.UI`.
- Turn-start AP grant plays one centered SFX beat, not one loud sound per chainling.
- Damage plays attack/move sounds before universal hit.
- Miss plays miss instead of hit.
- Block plays block instead of hit.
- Heal plays heal sound and does not also play hit.
- Status buff/debuff colors and sounds align with the visual feedback.
- KO plays after impact, not before.
- `SoundEventSO` cooldown and max instances prevent audio spam.

## Out Of Scope For Foundation

- Full multi-target outcome choreography.
- Priority ducking between cast, impact, and KO layers.
- Per-status custom sound mapping.
- Timeline-level authoring UI for whole move sequences.
- Dynamic music reactions.
- Camera pitch/audio synchronization.

