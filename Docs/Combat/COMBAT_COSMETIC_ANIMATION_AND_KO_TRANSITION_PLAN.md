# Combat Cosmetic Animation & KO Transition Plan

## Summary

V1 turns combat animation cosmetics into reusable identity-driven body clip overrides. Species variant profiles remain the baseline, while `ChainlingVisualIdentity` can override idle loop, idle flourish, melee, ranged, hit, swap-in, voluntary swap-out, KO, and attacker-KO taunt clips.

KO and voluntary swap-out use separate transition entry points. Both play a body clip first, freeze the final frame, run the signal glitch shader, then hide the slot. KO resolves `koFxId` first, then profile `koSequence`, then the profile `swapOutSequence` when stock profiles do not author a KO clip. Attacker KO taunts are separate from the KO'd chainling's animation; only battle-end victory still uses `victoryPoseId`.

## Data Model

- `ChainlingVisualIdentity` uses `visualVariantId` for body appearance. Legacy `skinId` is removed.
- `ChainlingVisualIdentity` owns optional animation IDs: `idleStyleId`, `idleFlourishId`, `meleeAttackStyleId`, `rangedAttackStyleId`, `hitReactionStyleId`, `swapInFxId`, `swapOutFxId`, `koFxId`, and `attackerKoStyleId`.
- `ChainlingVisualIdentityDatabaseSO` resolves those IDs to variant-scoped assets alongside aura, HUD card, and victory pose assets.
- Cosmetic definitions can scope to `speciesScope` and `visualVariantScope`; scoped mismatches are filtered in UI and rejected on save when local catalog context is available.
- Swap-in, swap-out, and KO FX assets use the shared FX base with optional `bodySequence` and `bodyFps` fields.
- Attack style assets can supply an optional `markerFrame`. `-1` disables marker override and lets the presenter use the existing midpoint fallback.
- Variant-specific body sprite strips should be authored as `CombatVisualProfileSO.animationAlternates` keyed by animation slot and equipped style ID.

## Runtime Flow

- `Combat2ChainlingView.AssignChainling` applies the species variant profile first, then cosmetic overrides through `ChainlingVisualSequenceResolver`.
- `CombatChainlingVisual` resolves sequence, FPS, duration, and marker timing from profile alternates first, embedded cosmetic clips second, species variant profile defaults third, and idle fallback last.
- `PlaySwapOutTransition` handles voluntary swap/flee and resolves `swapOutFxId` before profile swap-out.
- `PlayKoTransition` handles KO and resolves `koFxId → profile.koSequence → profile.swapOutSequence`.
- `PlayFaintAnimation` remains only as a compatibility wrapper around `PlayKoTransition`.
- Swap-in keeps the existing signal shader feel, with equipped swap-in body clips or profile alternates overriding the profile `swapInSequence`.
- Idle flourish is loop-count driven: every 2 completed idle loops, an equipped `idleFlourishId` one-shot can play, then idle resumes.

## KO Presentation

- `CombatEventLog.LogKO(turn, targetId, sourceId = "", moveId = "")` is the single KO event entry point.
- Direct attack damage passes the attacker and move IDs. Status, chemistry chip, escape/chip, recoil, and other non-direct KOs leave attribution empty.
- `HandleDamage` peeks ahead for a direct `ChainlingKO` from the same source/move/target. Lethal direct hits use `PlayHitImpactOnly` so damage popups and hit flash still happen without a full recoil settle.
- `HandleChainlingKO` plays the KO'd chainling's transition-out and, only for directly attributed KOs, plays the attacker's equipped attacker-KO taunt in parallel. The presenter waits for the longer of the two.
- `victoryPoseId` remains battle-end only.

## Cleanup Notes

- The old sprint-offscreen faint fields/routine and parallel body-plus-shader swap-out behavior are deprecated by the shared transition-out path.
- Existing FX-only swap-in/swap-out/KO assets remain valid. They become body clip overrides only when `bodySequence` is authored.
- V1 intentionally excludes post-KO relunge and per-hit projectile re-emission.

## Verification

- Edit-mode tests cover identity normalization/resolution, variant-scoped resolution, pose-capable FX definitions, body override duration/marker behavior, clearing overrides, and KO fallback.
- Manual QA should cover direct melee KO, direct ranged KO, status KO, chemistry KO, voluntary swap-out, auto swap-in, battle-end victory, equipped/unequipped idle loops, equipped/unequipped attack styles, and equipped/unequipped swap-in/swap-out/KO body clips.
- Build verification: `dotnet build "Signal Jam 3D.sln" -v minimal`.
