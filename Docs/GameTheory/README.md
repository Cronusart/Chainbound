# Game Theory — Exploratory Reports

**Status:** Exploratory only. **Not canonical. Not committed. Not a spec.**

This folder holds game-theory writeups: revenue, ownership, gacha vs market, capture psychology, and similar strategy notes. They exist so we can come back later and *optionally* source a path from a named report.

## Hard rule for this folder

Nothing in `Docs/GameTheory/**` is a product decision, economy rule, architecture, or implementation requirement.

- Do **not** treat these reports as source of truth.
- Do **not** include them in plans, builds, migrations, UI copy, or feature work by default.
- Do **not** “align” code or specs to suggestions in this folder unless the user **explicitly** asks to source from a **named file** in this folder (for example: “source from `GT_01_REVENUE_OWNERSHIP_AND_VENDING.md`”).
- Canonical economy, move tiers, and ownership rules remain in `Docs/Specs/**`, especially:
  - `Docs/Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md`
  - `Docs/Specs/Combat/MOVE_TIER_PLAYER_FACING_v1.md`
  - `Docs/Specs/Economy/BINDERY_FOLIO_AND_PRESS_CHARGE_v1.md`

If a report conflicts with a spec, **the spec wins**.

## Reports

| File | Topic |
|------|--------|
| [GT_01_REVENUE_OWNERSHIP_AND_VENDING.md](GT_01_REVENUE_OWNERSHIP_AND_VENDING.md) | Why marketplace / OTC can look like it kills vending; ownership as moat vs revenue; pack-rip analog; candidate revenue surfaces |

## How to promote a idea out of here

A report becomes real only after a separate, explicit request to:

1. Name the source file in this folder.
2. Write or update a spec under `Docs/Specs/**` (or another canonical doc).
3. Then plan / build from that spec — not from the Game Theory report.
