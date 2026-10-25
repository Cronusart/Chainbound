# Move Authoring Checklist

Use this checklist before and after any move edit.

## Before Editing

- Identify the source row in `Docs/Specs/Combat/Data/Moves.csv`.
- Read `_LEGEND.md` for column formats; do not reorder columns.
- Check `Statuses.csv` before adding `StatusEffects`; moves may only directly apply General statuses.
- Check `Modifiers.csv` for modifier compatibility and forbidden tags.
- Check `SpeciesMovePools.csv` only for acquisition/equip pool impact, not for species-specific descriptions.
- Classify the move:
  - `Data-only`: fields are enough.
  - `Resolver-supported special family`: existing explicit family is enough.
  - `Requires resolver extension`: plan code/importer/logging/tests first.

## Design Fit

- Does the move reinforce the type's role without redefining the type?
- Is any off-theme behavior limited, intentional, and useful for variety?
- Does the rarity reflect complexity or conditionality rather than raw output?
- If this is a tiered sibling, what keeps the lower-tier move tactically valid?
- Does the higher-tier sibling pay for extra impact through AP, accuracy, recharge, risk, status chance, or targeting limits?
- Does AP cost match tactical impact and category?
- Does targeting match counterplay expectations?
- Does a multi-hit or AoE rider multiply status chances too aggressively?
- Does the move remain understandable in one short MoveSelectPanel description?

## Resolver Fit

- Map each claimed effect to a concrete field, status, tag, utility opcode, or resolver family.
- Reject description-only mechanics; player-facing copy must not promise unsupported effects.
- If adding resolver support, prefer reusable enums/opcodes over move-specific branches.
- Include importer changes whenever a new CSV field/opcode is introduced.
- Include deterministic combat log output for every new timing, AP, HP, status, stage, barrier, or targeting effect.

## After Editing

- Validate CSV row width and enum spelling.
- Confirm description is species-neutral and concise.
- Re-import or deliberately sync generated move assets when needed.
- Run focused tests or smoke checks for:
  - AP spend/gain/drain and AP cap.
  - Hit/miss and multi-hit resolution.
  - Status origin and duration.
  - Stage caps and self/target routing.
  - Barrier mitigation/absorption/reflection.
  - Targeting and priority order.
  - Passive or delayed trigger timing.

## Sample Classifications

- Simple Atk debuff: `Data-only`; use `StageChanges` such as `MeleeAtk:-1:1.0:false`.
- Barrier support: `Data-only` or `UtilityEffectPayload`; use `MitigationWindow` or `FullAbsorbBarrier`.
- Higher-tier sibling with a modest power bump and AP/accuracy/status tradeoff: `Data-only`; allowed if the lower-tier move remains efficient or reliable.
- "Target next move costs +1 AP": `Requires resolver extension`; needs a represented temporary AP tax.
- Passive `OnMiss`: `Requires resolver extension`; currently unsupported as generic passive execution.
- Rare move that simply outclasses its lower-tier sibling: reject or revise; rarity alone cannot justify raw power creep.
