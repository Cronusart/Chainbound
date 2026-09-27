# Defense Balance Systems Notes

Defense-balance rules landed first; **lever boards now own the config surface** — see `Docs/Specs/Combat/LEVER_BOARDS_v1.md`.

## Rules landed

- **Mitigation uncapped:** `CombatConstants.MITIGATION_SINGLE_SOURCE_CAP = 1.0` (also `CombatResolverConfigSO.mitigationSingleSourceCap`). Sources still stack multiplicatively; `MITIGATION_FLOOR` (10%) remains.
- **Exclusive walls/shields:** `AddBarrier` clears prior barriers (`EXCLUSIVE_BARRIERS_AND_WALLS` / resolver config).
- **Cycle HP %:** `ProgressionConfigSO.cycleAutoHpPercent` / `cyclePickHpPercent` = `0.05` of species `baseMaxHP` (constants remain defaults).
- **Typed sustain moves:** `surge_recovery`, `ore_restoration`, `self_repair_protocol`, `error_correction`, `meme_magic` are resolver-honest in `Moves.csv` (plus `Cleanse` / `BonusHealPercentIfSpeedHigher` opcodes).
- **Defensive modifiers:** lifesteal ladder `6.1-d/e/a/f`, post-cast barriers `6.7-*` (`ModifierSlotType.Barrier`), passive MaxHP% `8.1-*` (`DUR` / `Durability`).
- **Rarity weights:** owned by `VendingConfigSO` (ECONOMY_01 curve) with server override via `get_vending_config`.

## Follow-up

- Re-import Moves/Modifiers/SpeciesMovePools CSV in Unity (`Combat2DataImporter`) so assets/catalog match CSV.
- Assign default Config assets on `BalanceConfigService` in the GameServices prefab (Inspector).
- On-chain vending re-derivation against stamped config version (documented trust gap in LEVER_BOARDS_v1).
