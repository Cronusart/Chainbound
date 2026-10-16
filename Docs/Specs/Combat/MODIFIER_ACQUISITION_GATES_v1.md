# Modifier Acquisition Gates v1

Status: specified, not wired. Devnet only.

This note is the routing contract for modifiers that must not enter the open vending roll. Section ids stay stable. Combat behavior for a row is separate from how a player is allowed to receive it.

## Current catalog

`Docs/Specs/Combat/Data/Modifiers.csv` remains the combat-data source of truth.

| Section | Name | Combat | Grant route reserved |
|---|---|---|---|
| `3.2-b` | Side Quest | Retired. No effect. | None. Weight 0. |
| `3.2-c` | After Hours | Retired. No effect. | None. Weight 0. |
| `7.2-c` | Setup Feed | Live. Next ally action gains 12% power. | Standard vending. |
| `10.1-a` | Equalizer | Live power modifier. | Standard until the 10.1 family moves. |
| `10.1-b` | Glass Overclock | Live power modifier. | Standard until the 10.1 family moves. |
| `10.1-c` | Volatile | Live power modifier. | Standard until the 10.1 family moves. |
| `10.1-d` | All-Stat Burn | Retired combat effect. | Entangled-only. |
| `10.2-a` | Seasonal Skin | Live 3–5% power modifier. | Standard until the event family moves. |
| `10.2-b` | Anniversary Mark | Retired combat effect. | Server-wide event. |
| `10.2-c` | Tournament Badge | Retired combat effect. | PvP tournament win. |

`7.2-c` is locked at EffectMin 12 and EffectMax 12. The narration panel states that exact percent when the modifier arms, and states the same percent when a non-power action spends it. Turn boundaries do not clear the arm. `COMBAT_RuntimeFollowups.md` records the resolver rule.

## Client off switch

Retired rows use Weight `0` and `MoveModifierGenerator.IsRetiredFromVending`. `BuildCandidatePool` skips those section ids even if a balance overlay raises their weight. The legacy fallback pool also omits `3.2-b` and `3.2-c`.

That switch covers Unity catalog rolls only. It does not remove the ids from the server stamp pool.

## Pinned stamp pool

`deriveStampResult` in `Supabase/functions/_shared/ownership_spine.ts` picks section ids by modulo over `MODIFIER_POOL` (104 ids) and locks the effect from `MODIFIER_EFFECT_BOUNDS`. The pinned pool hash stays:

`6cea5ce56944a88486cafbb568a628b5aa5664c3db652a79cd90016427aaa4b4`

`generate-modifier-pool-artifact.cjs` may refresh effect bounds and `catalogMechanicalHash` when Weight, EffectMin, or EffectMax change. It must keep the pinned id list and the pinned hash. A section-id reorder or deletion is a pool cut: the generator exits without writing, and a new hash requires a reviewed generator-version bump. Historical snapshots in older ownership notes stay historical.

Until that cut, a new stamp can still land on a retired id. The locked effect is 0 and the resolver has no combat branch for it. Filtering those ids inside `deriveStampResult` would change outcomes for the same pool hash, so it is out of scope for this pass.

## Gate values

A later CSV column, `AcquisitionGate`, is the reusable switch. Importer and `ModifierDefinitionSO` carry the same field. `MoveModifierGenerator` admits only `Standard` into candy and other local rolls.

| Value | Who may receive it | Example ids |
|---|---|---|
| `Standard` | Open vending and the current stamp roll. | Everything still live, including `7.2-c` and `10.2-a`. |
| `EntangledExclusive` | The chainling that owns the stamped move has `ChainlingInstance.isEntangled` (the same flag already copied onto battle snapshots). | Family `10.1`. Only `10.1-d` is reserved today. `10.1-a`, `10.1-b`, and `10.1-c` stay `Standard` until that family moves together. |
| `ServerEvent` | A server receipt for one profile and one event id. | Family `10.2` except `10.2-c`. Only `10.2-b` is reserved today. `10.2-a` stays a live `Standard` power modifier until that family moves. |
| `TournamentReward` | A server receipt for one recorded tournament win. | `10.2-c`. |

There is no event-window service and no tournament receipt yet. Those grants stay unwired. Local clients do not roll them.

## Future grant path

When a gate is ready to wire:

1. Add `AcquisitionGate` to the CSV and the importer. Default existing rows to `Standard`. Set `10.1-d`, `10.2-b`, and `10.2-c` to the values above.
2. Keep `IsRetiredFromVending` until the generator itself excludes every non-`Standard` id. Weight `0` stays on rows with no combat effect.
3. Entangled equip and stamp checks read `isEntangled` on the owning chainling. A standard chainling cannot hold an `EntangledExclusive` modifier on a move.
4. Event and tournament grants are server receipts, one per profile per event id and one per recorded win. The receipt names the section id. It does not call the open stamp modulo.
5. A pool cut that drops retired ids from `MODIFIER_POOL` is a separate reviewed change. It rotates `EXPECTED_MODIFIER_POOL_HASH` and the generator version. It is not part of turning a gate on.

No ranked-bonus formula is defined for `10.2-c`. The first tournament wiring grants the section id only.
