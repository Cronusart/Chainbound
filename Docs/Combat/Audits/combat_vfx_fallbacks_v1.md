# Combat VFX Fallbacks v1 — implementation audit

Date: 2026-10-22

Scope: plan phases P0-P8. P7-P8 were implemented after explicit approval on 2026-10-22.

## Result

- The move VFX library was moved through the live Unity Editor from `Assets/Data/Combat/VFX/Library/` to `Assets/Data/Combat/VFX/Registry/`.
- The library asset GUID remains `fcc3c49dcbbcfe34eb3d12deede80b15`; `Combat2_TestHarness.unity` still references that GUID.
- `CombatVFXFallbacks.asset` is stored beside the library and referenced by it.
- Runtime and both combat preview paths now use the same pure resolver.
- Resolver order is explicit move → legacy category → typed/typeless damage or utility role/parent → global → none.
- Empty entries do not win resolution; at least one usable phase is required.
- Damage classification wins over status/debuff riders, and damage typing uses the authored `baseType`.
- The fallback inspector exposes the 15 × 2 damage grid, role slots, globals, filled markers, and catalog usage counts.
- `Tools/Chainbound/Combat/VFX/Coverage Report` exposes move-level resolution, tier counts, empty preferred slots, filters, and Markdown export.
- No combat resolver, AI, server, PvP, move data, or manifest-authoritative behavior was changed.

## Seeded asset coverage

The approved seed copies `baseline_jab` into typeless melee and `baseline_shot` into typeless ranged. Both copies retain phase authoring, use `impactDelay = -1`, and leave all remaining slots empty for art authoring.

Catalog summary for the seeded asset:

- Total move definitions: 211
- Passive definitions excluded from `MoveUsed` VFX: 31
- Usable moves evaluated: 180
- Resolved now: 75
  - Explicit: 5
  - Typeless damage fallback: 70
- Intentionally unresolved until role art is assigned: 105 utility moves
- Proof of structural coverage: all 180 usable moves resolve in the automated catalog test when the approved beta slots are populated.

Authoritative classifier buckets from the imported move assets:

- Damage 75; Heal 18; Buff 40; Debuff 29
- Wall 3; Barrier 5; Armor 1
- AP Gain 3; AP Drain 5; Swap 1
- Passive 31

The unresolved count is expected for the seed pass: the plan explicitly leaves heal, buff, debuff, optional specific roles, typed damage rows, and global defaults empty for the VFX author to fill. The coverage window identifies each empty-but-used slot.

## Per-phase fill

Added 2026-10-23. `CombatVfxResolver` still walks explicit, legacy, typed/typeless or role/parent, then global. It no longer stops at the first entry that has any usable phase. Each of Cast, Strike, Projectile, and Impact is chosen from the first candidate in that same order that has a usable config for that phase.

The base tier is still the first usable entry, so existing logs and `MovePresentationContext.vfxResolveTier` keep their meaning. When every chosen phase already comes from that base entry, the resolver returns the entry itself. Otherwise it returns a new entry whose phases are clones. Source entries and fallback slots are not rewritten.

`suppressPhaseFill` on the base entry keeps today's unmerged result, for a move that intentionally omits a phase. `impactDelay` uses the base value when it is above zero, otherwise the Impact supplier's value, otherwise -1. `impactSound` and `missSound` use the base cue when one is assigned, otherwise the Impact supplier.

`CombatVFXLibrary.Resolve` caches the result by move key. The cache clears when the library validates, when `CombatVFXFallbacks.Version` changes, and on domain reload. The coverage report adds a source column per phase, a count of moves with at least one phase filled from a later slot, and a "Relevant phase missing" filter for ranged moves without Projectile and damage moves without Impact.

A ranged fallback row that already has another usable phase, such as Impact, no longer raises the "no usable Projectile phase" warning. Impact-only typed rows stay valid suppliers. A ranged row with no usable phase at all still warns.

Phase-fill verification on Unity 6000.6.6f1:

- `CombatVfxPhaseFillTests`: 12 passed, 0 failed
- `CombatVfxFallbackTests`: 18 passed, 0 failed
- `CombatVFXLibraryPhaseTests`: 22 passed, 0 failed
- `CombatPresentationAudioTests`: 4 passed, 1 failed — the same `SoundOnlyPhaseHonorsSpawnDelay` baseline (expected play count 1, observed 0)
- `CombatPostFxVolumeControllerTests`: 3 passed, 3 failed — the same baseline. `IdleWeightIsZeroAndDoesNotTouchBaselineVolume` still sees owned weight 1. `PlayRaisesWeightThenReturnsToZero` and `ReplaceLatestCueEndsAtZeroWeight` each hit the 180-second EditMode coroutine timeout.

## EditMode verification

Pre-change baseline:

- `CombatVFXLibraryPhaseTests`: 22 passed, 0 failed
- `CombatPresentationAudioTests`: 4 passed, 1 failed
  - Existing failure: `SoundOnlyPhaseHonorsSpawnDelay` (expected play count 1, observed 0)
- `CombatPostFxVolumeControllerTests`: 3 passed, 3 failed
  - Existing failures: `IdleWeightIsZeroAndDoesNotTouchBaselineVolume`, `PlayRaisesWeightThenReturnsToZero`, `ReplaceLatestCueEndsAtZeroWeight`

Post-change verification:

- `CombatVfxFallbackTests`: 18 passed, 0 failed
- `CombatVFXLibraryPhaseTests`: 22 passed, 0 failed
- `CombatPresentationAudioTests`: 4 passed, 1 failed — identical baseline failure
- `CombatPostFxVolumeControllerTests`: the rerun timed out after 300 seconds in the EditMode coroutine runner. The same suite had already completed at baseline with the three failures listed above; no VFX fallback code touches the post-FX controller or these test timings.
- Unity compilation completed without compiler errors before the post-FX runner hang.

Post-restart confirmation:

- Unity reported compilation completed with no errors.
- `CombatVfxFallbackTests`: 18 passed, 0 failed, including authoritative catalog bucket counts.
- `CombatVFXLibraryPhaseTests`: 22 passed, 0 failed.
- `CombatPresentationAudioTests`: 4 passed, 1 failed — the same `SoundOnlyPhaseHonorsSpawnDelay` baseline failure.
- Non-coroutine post-FX checks were run separately to avoid the known zero-`deltaTime` EditMode loop: owned-volume layer, force-clear behavior, and post-FX-only phase usability passed; the idle-weight check reproduced its baseline failure (owned volume weight 1 instead of 0).
- The two coroutine post-FX checks were not repeated as a combined suite because their `Time.deltaTime`-bounded loops previously occupied the test runner until its 300-second guard.

## Source-control precondition

The Registry folder and both VFX assets are no longer hidden by the `[Ll]ibrary/` ignore rule. The folder rename, preserved-GUID library, and new fallback asset are staged in Git; unrelated dirty files remain untouched. The move intentionally preserved the original library GUID rather than recreating the asset.

## P7-P8 wall timeline and preview result

- `CombatVFXFallbacks` now carries seven normalized wall-state rows: Wall default/Lower/Mid/Greater and Barrier default/Lower/Mid. Each row supports art overrides plus raise, persistent loop, hit, break, and expire phase configs. Greater Barrier is rejected by normalization and produces a validation warning if authored.
- Resolution is per-move wall art, exact kind/tier, kind default, then the existing global `CombatWallVisualProfileSO`. Wall audio remains in `CombatWallSfxCatalogSO`.
- Move-sourced walls raise after the utility Cast hold. Polling suppresses first raise while a matching `MoveUsed` is queued or presenting, preserving turn-start ordering for faster priority hits. Polling remains the fallback for passive/modifier/restore state.
- `CombatWallPresentationPlanner` owns ordered wall/body beats and emits exactly one attacker impact per hit. Break-through order is wall impact, wall hit, break, then body reaction.
- Active source-tag replacement reapplies art and plays a short refresh. Persistent loop VFX is replaced and cleaned up with wall state.
- Timer expiry uses the distinct expire phase and `FADED` popup; duplicate expiry events are presentation-gated.
- The previewer now exposes Defender Has Wall plus kind/tier controls, routes travel/impact anchors to the barrier, suppresses the body hit reaction for a blocked preview, and plays the selected wall row's loop/hit VFX through the same planner policy.

P7-P8 verification after the final compile:

- `CombatWallPresentationPlannerTests`: 10 passed, 0 failed.
- All `CombatWall*` tests: 25 passed, 0 failed.
- `WallsBarriersTieredV1Tests`: 29 passed, 0 failed.
- `CombatVfxFallbackTests`: 18 passed, 0 failed.
- `CombatVFXLibraryPhaseTests`: 22 passed, 0 failed.
- `CombatDecisionRoundTests`: 10 passed, 0 failed, including bench AP, Group Hug AP, and stage-expiry behavior.
- Unity compilation completed with no errors.

The known pre-existing audio/post-FX baseline failures and the zero-`deltaTime` coroutine runner hang documented above are unchanged; those suites were not used as a new P7-P8 gate.
