# Moves.csv Import Validation

- Source: `Docs/Specs/Combat/Data/Moves.csv`
- Generated: 2026-10-01 07:47:55
- Errors: 2 at generation time. Both were the `ice_wall` comma shift.
- Warnings: 0
- Infos: 0

## Follow-up 2026-10-16

The `ice_wall` description is quoted, so SelectionPolicy is `EffectOnly` and ActiveConditionType is `None`. The two errors below are cleared in the CSV. The restored mitigation hit budget is still 0, so that window does not mitigate. The speed-based +1 AP opcode imports.

| Severity | Row | MoveId | Code | Message |
|---|---:|---|---|---|
| Error | 111 | ice_wall | BadEnum | SelectionPolicy value 'gain +1 AP when hit.' is not a valid MoveSelectionPolicy. Resolved 2026-10-16 by quoting the description comma. |
| Error | 111 | ice_wall | BadEnum | ActiveConditionType value 'EffectOnly' is not a valid ActiveConditionType. Resolved 2026-10-16 by the same quote. |
