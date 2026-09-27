# Library card mod effect text (stamp-exact)

## Source of truth
Stamped inventory (`StampedMoveRecord` / `MenuMoveItem`) already persists:
- `modifier1Id` / `modifier2Id`
- `modifier1EffectValue` / `modifier2EffectValue`
- `modifier1EffectValueLocked` / `modifier2EffectValueLocked`

When locked, combat uses the fixed value — not a re-roll of catalog `effectMin`..`effectMax`.

## Card bake input (per mod in fixture / metadata)
```json
{
  "slot": 1,
  "id": "2.1-a",
  "displayName": "Overclock",
  "effectDescription": "+5-8% base power",
  "effectValue": 7,
  "effectValueLocked": true,
  "effectUnit": "%",
  "effectNoun": "base power",
  "effectLine": "optional preformatted override"
}
```

## Display priority in `render.py`
1. `effectLine` if non-empty (authoritative bake override)
2. else if `effectValueLocked` + numeric `effectValue` → `+{value}{unit} {noun}` (unit defaults `%`)
3. else catalog `effectDescription` / `shortEffect` range text
4. else name only

Final mod box label: `{DISPLAYNAME}  ·  {EFFECT}` when an effect string exists.

## Live wiring (Cursor follow-up)
Map stamp inventory → card JSON:
- `id` ← `modifierNId`
- `displayName` ← ModifierDefinitionSO.GetDisplayName()
- `effectValue` ← `modifierNEffectValue`
- `effectValueLocked` ← `modifierNEffectValueLocked`
- `effectUnit` / `effectNoun` ← derive from definition `effectDescription` or a future structured field
- optional: server formats `effectLine` once at stamp URI bake so the PNG renderer stays dumb

Do not print catalog ranges on stamped cards when `effectValueLocked` is true.
