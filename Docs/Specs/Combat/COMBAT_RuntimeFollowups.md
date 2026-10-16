# Combat Runtime Follow-Ups

This note tracks review items intentionally left out of the AP/modifier/passive/lockdown runtime pass.

## Status And Stage Review

- Audit derived Accuracy, Evasion, and Crit stage behavior across `StatusProcessor`, `DamageCalculator`, move data, and UI text.
- Confirm stage caps by source: direct move stages, status-applied stages, chemistry stages, and modifier-applied stages.
- Reconcile status duration timing across immediate application, pending turn-start application, consumed statuses, and end-of-turn expiry.
- Review status descriptions for player-facing clarity once timing and stage behavior are final.

## Remaining Resolver Extensions

- `3.2-a` Echo and `3.3-a` Bonus Strike are the live bonus-action rows. `3.2-b` Side Quest and `3.2-c` After Hours are decommissioned.
- Targeting expansion modifiers such as bounce, reduced-power spread, ally-wide support, and partner relay should use generic targeting/potency payloads rather than move-id branches.
- `7.2-a` Tag Team and `7.2-b` Pile-On read `TurnActionLedger` for this turn. `7.2-c` Setup Feed is the next ally action, including across the turn boundary, at a fixed 12% power.
- `10.1-d`, `10.2-b`, and `10.2-c` keep their section ids as grant-route placeholders. The gates are specified in `MODIFIER_ACQUISITION_GATES_v1.md` and are not wired.
- `10.2-a` Seasonal Skin remains a live power modifier. Other cosmetic or ranked rows stay metadata-only unless a numeric combat rule is explicitly specified.
