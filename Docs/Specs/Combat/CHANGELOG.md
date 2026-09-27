# Combat Changelog

## 2026-09-16 — Balance hotfix

- Locked new IV rolls and authored encounter ranges to `-5..+5`; existing saved IVs are unchanged.
- Chemistry advantage remains a damage multiplier but no longer bypasses walls.
- Recharging now ticks once per active end of turn, and benched durations remain paused.
- `RandomEnemy` now uses deterministic uniform selection across living opposing actives.
- Reflected barrier KOs stop remaining multi-hit strikes after the current hit resolves.
- Encrypted Seal now blocks harmful chemistry reactions through the same debuff rule.
