# Combat Runtime Follow-Ups

This note tracks review items intentionally left out of the AP/modifier/passive/lockdown runtime pass.

## Status And Stage Review

- Audit derived Accuracy, Evasion, and Crit stage behavior across `StatusProcessor`, `DamageCalculator`, move data, and UI text.
- Confirm stage caps by source: direct move stages, status-applied stages, chemistry stages, and modifier-applied stages.
- Reconcile status duration timing across immediate application, pending turn-start application, consumed statuses, and end-of-turn expiry.
- Review status descriptions for player-facing clarity once timing and stage behavior are final.

## Remaining Resolver Extensions

- Bonus action modifiers need a reusable queued-action resolver path before rows like `3.2-a`, `3.2-b`, `3.2-c`, and `3.3-a` should become fully active.
- Targeting expansion modifiers such as bounce, reduced-power spread, ally-wide support, and partner relay should use generic targeting/potency payloads rather than move-id branches.
- Synergy rows that depend on "ally attacked this turn", "target was hit this turn", or "next ally move" need turn-memory state before they can be deterministic.
- Cosmetic/ranked-only modifier rows should remain metadata-only unless a numeric combat rule is explicitly specified.
