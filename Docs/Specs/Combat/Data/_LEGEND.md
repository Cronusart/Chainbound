# Combat Data CSV Legend

> **This folder is the source of truth for all combat data.**
> The importer (`Tools > Combat > Data Importer`) reads these CSVs and generates ScriptableObject assets.
> One row = one SO asset. Edit here, re-import, done.
>
> `Modifiers.csv` includes `DisplayName` immediately after `SectionId`. `sectionId` stays the stable key; player-facing UI should call `ModifierDefinitionSO.GetDisplayName()`.

---

## Editing Rules

1. Use any text editor or spreadsheet app. Save as CSV (comma-delimited, UTF-8).
2. **Header-based import** — `Import Moves (CSV)` maps fields by column **name** (`MoveId`, `Power`, …), not index. You may reorder columns; keep the header row as the first non-comment line starting with `MoveId`. Unknown headers are ignored with a warning. Required headers are listed in `CombatMovesCsvSchema.RequiredColumns`.
3. Enum values must match **exact C# names** (case-sensitive). See tables below.
4. Boolean values: `true` or `false` (lowercase).
5. Empty fields = default/zero. Leave blank, not `null`.
6. Lines starting with `#` are treated as comments and skipped by the importer.
7. Pipe `|` separates multiple values within a single field (e.g., stage effects, tags).
8. **Validate before trusting assets** — `Tools > Chainbound > Combat > Validate Moves CSV` (also on the Data Importer window) writes `Docs/Combat/Audits/moves_import_validation.md` (unknown opcodes, bad enums, ChemistryOnly status applications, bad resolve conditions, etc.). Import also regenerates this report.
9. **CSV ↔ asset parity** — after import (or before commit), run `Tools > Chainbound > Combat > Audit Moves CSV vs Assets` → `Docs/Combat/Audits/moves_csv_asset_parity.md`. Presentation / VFX / SFX fields on `MoveDefinitionSO` are intentionally not in Moves.csv.

---

## Import Order

Import must follow dependency order. Use **"Import All From Data"** to run them automatically.

```
1. Statuses   (no dependencies)
2. Moves      (references StatusId for status effects)
3. Modifiers  (references MoveCategory for restrictions)
4. Species Pools (references MoveId for learnable/exclusive pools)
```

---

## Enum Reference Tables

### StatusId

See `Assets/Scripts/Combat/Runtime/Data/StatusId.cs` for the full list (54 values).
Ranges: ChemistryOnly = 100–130, General = 200–219, v1.2 General = 300–306, Engine = 900.
Use the enum name exactly as written (e.g., `Pumped`, `FUD`, `GlitchedMind`, `FiftyOnePercentAttacked`).

### StatusClass

| Value | Meaning |
|-------|---------|
| Duration | Persists N turns; reapply refreshes up to max |
| Instant | Resolves immediately on application |
| Counter | Increments counter; triggers at threshold |
| Consumed | Persists until consumed OR expires |

### StatusOrigin

| Value | Meaning |
|-------|---------|
| ChemistryOnly | Only produced by chemistry resolver on damaging hit |
| General | Can be applied by moves/passives |

### MoveCategory

| Value | Meaning |
|-------|---------|
| DamagingMelee | Physical melee damage move |
| DamagingRanged | Ranged/projectile damage move |
| StatusUtility | Status application or utility effect |
| Support | Ally-targeting buff/heal/barrier |
| Passive | Always-active or triggered passive |

### MoveTargeting

| Value | Meaning |
|-------|---------|
| SingleEnemy | One opponent |
| AllEnemies | All opponents |
| SingleAlly | One ally |
| AllAllies | All allies |
| Self | User only |
| Field | Both sides of the field |
| RandomEnemy | Random opponent |

### ContactType

| Value | Meaning |
|-------|---------|
| Contact | Physical contact (triggers Backlash) |
| Projectile | Ranged projectile (softened by Firewall) |
| None | Non-damaging or special |

### ChainlingType

| Value | ID | Meaning |
|-------|----|---------|
| None | 0 | Typeless / unresolved |
| Node | 1 | Support/coordination |
| Gas | 2 | AP manipulation/tempo |
| Block | 3 | Defense/sustain |
| Encrypted | 4 | Protection/clarity |
| Burned | 5 | Attrition/AP denial |
| Frozen | 6 | Speed control/stall |
| Forked | 7 | Speed/burst |
| Mined | 8 | Scaling/momentum |
| Staked | 9 | Growth/patience |
| Compressed | 10 | Efficiency/precision |
| Bot | 11 | Automation/attrition |
| Glitched | 12 | Chaos/exploit |
| Shard | 13 | Team scaling/partition |
| Meme | 14 | High-risk/high-reward |

### RarityTier

| Value | Meaning |
|-------|---------|
| Common | Baseline availability |
| Uncommon | Slightly restricted |
| Rare | Notable power/complexity |
| Epic | Significant power |
| Legendary | Top-tier |
| Mythic | Ultra-rare |

### ModifierCategory

| Value | Code | Meaning |
|-------|------|---------|
| POT | POT | Potency (power, accuracy, crit) |
| EFF | EFF | Efficiency (AP refund, bonus action) |
| INIT | INIT | Initiative / turn-order modifiers |
| AUG | AUG | Augmentation (extra hit, AoE, priority) |
| TRX | TRX | Transmutation (type infusion, stat substitution) |
| TRG | TRG | Trigger (on-hit, on-crit, conditional, swap) |
| SYN | SYN | Synergy (team/field effects) |
| DUR | DUR | Durability (passive MaxHP%, defensive sustain) |

### ModifierScope

| Value | Meaning |
|-------|---------|
| Active | Applies to the specific move it's attached to |
| Passive | Applies globally while the modified move is equipped |

### ModifierTriggerCondition

| Value | Meaning |
|-------|---------|
| None | No trigger |
| OnHit | Triggers when the move hits |
| OnCrit | Triggers on critical hit |
| OnMiss | Triggers on miss |
| OnKO | Triggers when target is KO'd |
| OnAllyKO | Triggers when an ally is KO'd |
| OnLowHP | Triggers when HP drops below threshold (use `lowHpThresholdPercent`) |
| OnEscape | Triggers when opponent attempts swap/escape |
| OnHPBelow30 | Triggers when HP drops below 30% |
| OnStatusApplied | Triggers when a status is applied |
| EndOfTurn | Triggers at end of turn |
| OnSwapIn | Triggers when swapping in |
| OnSwapOut | Triggers when swapping out |
| OnBattleStart | Triggers once when battle initializes |
| OnIncomingHit | Triggers when the owner is hit |

### ModifierSlotType

| Value | Meaning |
|-------|---------|
| None | No slot-type cap applies |
| INIT | Initiative slot-type (max 1 per inscription) |
| ActionEconomy | Action-economy slot-type (max 1 per inscription) |
| Targeting | Targeting slot-type (max 1 per inscription) |
| Barrier | Post-cast barrier-grant slot-type (max 1 per inscription) |
| Durability | Passive MaxHP% durability slot-type (max 1 per inscription) |

### PassiveTriggerCondition

| Value | Meaning |
|-------|---------|
| None | No trigger (always active) |
| HPBelowThreshold | HP drops below threshold % |
| OnDamageTaken | When taking damage |
| OnKO | When enemy is KO'd |
| OnAllyKO | When ally is KO'd |
| OnMiss | When enemy misses |
| EndOfTurn | At end of turn |
| TurnStart | At the owner's turn-start hook |
| OnHit | After the owner confirms a hit |
| OnCrit | After the owner confirms a critical hit |
| OnAllyHit | After another active ally confirms a hit |
| OnBuffExpired | When one or more active buff statuses expire |
| OnAction | After the owner resolves an action |

### CoreStat

| Value | Meaning |
|-------|---------|
| MaxHP | Maximum hit points |
| MeleeAtk | Melee attack power |
| MeleeDef | Melee defense |
| RangedAtk | Ranged attack power |
| RangedDef | Ranged defense |
| Speed | Speed (turn order) |

---

## Ally Terminology

| Term | Definition |
|------|-----------|
| **Partner** | The other active ally (the Chainling in the other active slot when self is excluded) |
| **Team** | Self + partner (both active allies) |
| **Active ally** | Synonym for partner when self is excluded |
| **Bench** | Chainlings not currently in an active slot |

> In a 2v2 battle, each side has 2 active slots and 1 bench slot. "Per partner" means the single other active Chainling on your side. "Per team" includes self. Avoid "per ally" — use "per partner" or "per team member" for clarity.

---

## Complex Field Formats

### StageEffects (Statuses.csv)

Format: `Stat:Stages` or `Stat:Stages:ApplyOnce`

- `Stat` = CoreStat enum value
- `Stages` = integer (positive = buff, negative = debuff)
- `ApplyOnce` = `true` (default, applied on status application) or `false` (applied per turn end)
- Multiple effects separated by `|`

Examples:
- `MeleeDef:3` — +3 MeleeDef applied once
- `MeleeAtk:-1|MeleeDef:-1|Speed:-1` — all three applied once
- `MeleeDef:1:false` — +1 MeleeDef applied each turn end (ramp)
- *(empty)* — no stage effects

### StatusEffects (Moves.csv)

Format: `StatusId:Duration:Chance:TargetOverride`

- `StatusId` = StatusId enum value
- `Duration` = integer turns
- `Chance` = float 0.0–1.0
- `TargetOverride` = MoveTargeting enum (optional, empty = use move targeting)
- Multiple separated by `|`

Examples:
- `Rekt:2:0.25:SingleEnemy` — 25% chance to apply Rekt for 2 turns
- `FUD:2:1.0:` — 100% chance to apply FUD for 2 turns (no target override)

### StageChanges (Moves.csv)

Format: `Stat:Stages:Chance:TargetsSelf`

- `Stat` = CoreStat enum value
- `Stages` = integer
- `Chance` = float 0.0–1.0
- `TargetsSelf` = `true` or `false`
- Multiple separated by `|`

Examples:
- `Speed:-2:0.25:false` — 25% chance to apply -2 Speed to target
- `MeleeAtk:1:1.0:false|Speed:1:1.0:false` — +1 Atk and +1 Speed to target (100%)
- `MeleeAtk:2:1.0:true|MeleeDef:-1:1.0:true` — self +2 Atk, self -1 Def

### Tags (Moves.csv)

Pipe-separated tag strings: `DMG|MULTI|AOE|PRIO|PRIOR|TRIG|APMAN|STAT|UTIL|SUP|PAS|HEAL`

| Tag | Meaning |
|------|---------|
| DMG | Damaging move |
| MULTI | Multi-hit move |
| AOE | Area of effect (hits multiple targets) |
| PRIO | Legacy priority flag (see PRIOR) |
| PRIOR | Priority move — resolves in the priority block before normal combat moves |
| TRIG | Conditional/triggered effect |
| APMAN | AP manipulation (gain, drain, transfer) |
| STAT | Stat-scaling or stat-dependent effect |
| UTIL | Utility effect (buffs, debuffs, field) |
| SUP | Support move (ally heal, barrier, transfer) |
| PAS | Passive ability |
| HEAL | Healing effect |

### HealPercentMaxHP (Moves.csv, optional)

Integer 0–100. When non-zero, the move heals the target for this percentage of their `EffectiveMaxHP` instead of using flat `basePower`. Missing/empty column defaults to `0`.

Examples:
- `25` — heal 25% of target's MaxHP
- `0` — no percentage-based heal (use flat power or no heal)

### Resolve profile columns (Moves.csv, optional)

Named resolve-profile columns (see header row / export). Leave an optional profile field empty to keep its legacy/default behavior. Export writes the full header set used by round-trip.

| Column | Meaning |
|---|---|
| `HpCostPercent` | MaxHP percentage paid by supported HP-cost moves |
| `ResolvePrimaryTiming` | Primary authored `MoveEffectTimingBucket` |
| `ResolveUtilityTiming` | `UtilityOnly`, `ResolveBeforeFirstHit`, `AfterEachConfirmedHit`, or `AfterMoveComplete` |
| `ResolveConditionalPolicy` | How legacy active gating combines with resolve conditions |
| `ResolveConditions` | Pipe-separated live-state predicates used to gate riders/effects |
| `ResolveRequireAll` | `true` = all predicates; `false` = any predicate |
| `DamagePowerMultiplier` | Move power multiplier before modifier power multipliers |
| `DamagePreMitigationMultiplier` | Damage multiplier before mitigation |
| `DamageFinalMultiplier` | Damage multiplier after mitigation |
| `DamageOverrideAttackStat` | Whether `DamageAttackStat` replaces the normal attack stat |
| `DamageAttackStat` | `CoreStat` used when the override is enabled |
| `ConditionalDamageMultiplier` | Fixed multiplier when conditional-damage predicates pass |
| `ConditionalDamageConditions` | Pipe-separated per-hit conditional-damage predicates |
| `ConditionalDamageRequireAll` | All/any policy for conditional damage |
| `ConditionalDamageScalingCondition` | One count-producing predicate used for scaling |
| `ConditionalDamageBonusPerCount` | Multiplier added per resolved count |
| `ConditionalDamageMaxMultiplier` | Cap after count scaling (`0` = uncapped) |
| `RandomDamageMultipliers` | Pipe-separated seeded choices, e.g. `1.5|0.75` |

Condition token format:

`Subject:ConditionType:Threshold:StatusId:Stat:ComparisonStat`

- `Subject` is `Self` or `Target`.
- Multiple conditions are separated by `|`.
- Count-producing scaling predicates include `BuffedPartnerCountAtLeast`, `DebuffCountAtLeast`, and `StatusCountAtLeast`.
- Other supported predicates include live HP/AP, status/stage/barrier checks, no-buff checks, prior-turn hit/damage memory, and same-turn action memory.

Examples:

- `Target:HPBelowPercent:40:None:MeleeAtk:MeleeAtk`
- `Target:DebuffCountAtLeast:2:None:MeleeAtk:MeleeAtk`
- `Self:BuffedPartnerCountAtLeast:0:None:MeleeAtk:MeleeAtk`
- `Self:WasNotDamagedLastTurn:0:None:MeleeAtk:MeleeAtk`

### UtilityEffects (Moves.csv)

Separate payloads with `|`. Percent tokens accept `25%` or a fraction such as `0.25`.

| Opcode | Parameter format |
|---|---|
| `MitigationWindow` | `percent:turns:hits:stackKey` |
| `FullAbsorbBarrier` | `turns:hits:stackKey` |
| `DamageThresholdBarrier` | `capacity:turns:hits:reflectPercent:stackKey` |
| `WallHitRetaliation` | `attackerApDrain:stackKey` |
| `GainApOnDamageIfSpeedHigher` | `apGain:turns:hits:stackKey` |
| AP mutation opcodes | `amount:chance:target:stackKey` |
| `TurnStartPendingStatus` | `statusId:duration:target:stackKey` |
| `Cleanse` | `maxCount:chance:target:stackKey` |
| `BonusHealPercentIfSpeedHigher` | `percent:target:stackKey` |
| `IncomingDamageMitigation` | `percent:contactFilter:stackKey` |
| `ChangeStage` | `stageStat:amount:chance:target:cap:allAllies:stackKey` |
| `RandomStageChange` | `minAmount:maxAmount:chance:target:cap:stackKey` |
| `ArmUtilityWindow` | `trigger:turns:hits:apDelta:healPercent:stageStat:stageAmount:target:counterPercent:allAllies:cap:stackKey` |
| `BankAPUntilNextTurn` | `bankCap:bonus:stackKey` |
| `GainApIfDistinctTargetsHitAtLeast` | `targetCount:apGain:stackKey` |
| `FieldEntryStageChange` | `stageStat:amount:turns:chance:stackKey` |
| `MoveAPDiscountWhileHPAbove` | `discount:hpPercent:stackKey` |
| `LifestealPercent` | `damagePercent:stackKey` |
| `HealPercentIfStageAtLeast` | `healPercent:stageStat:threshold:target:stackKey` |

AP mutation opcodes are `GainAP`, `DrainAP`, `StealAP`, `TransferAP`, `RefundAP`, `NextMoveAPSurcharge`.

`CombatStageStat` supports `MeleeAtk`, `MeleeDef`, `RangedAtk`, `RangedDef`, `Speed`, `Accuracy`, `Evasion`, and `CritRate`. Contact filters are `Any`, `Contact`, `Projectile`, and `None`.

Armed triggers are `FoeKO`, `EnemyMiss`, `IncomingDebuff`, `IncomingHit`, and `OwnerTurnStart`. Armed windows clear on swap-out.

### AllowedCategories / ForbiddenCombos (Modifiers.csv)

- `AllowedCategories`: pipe-separated MoveCategory values (e.g., `DamagingMelee|DamagingRanged`)
- `ForbiddenCombos`: pipe-separated modifier section IDs (e.g., `2.1-a|2.1-b`)

### PowerCap / APCostCap / ForbiddenTags / SlotType / Weight (Modifiers.csv)

- `PowerCap`: integer cap on move power (`0` = no cap)
- `APCostCap`: integer cap on move AP cost (`0` = no cap)
- `ForbiddenTags`: pipe-separated move tags that disallow the modifier (e.g., `MULTI|PRIOR`)
- `SlotType`: `None`, `INIT`, `ActionEconomy`, or `Targeting` for one-per-inscription caps
- `LowHpThreshold` (optional): HP % threshold for `OnLowHP` triggers. Defaults to `30` when omitted.
- `Weight` (optional, trailing): within-tier relative drop weight for vending candidate pools (`ModifierDefinitionSO.weight`). Defaults to `1` when omitted. Integer ≥ 0. Export writes `LowHpThreshold,Weight` after `SlotType`; older rows without `Weight` remain valid.

---

## Common Editing Scenarios

### Adding a new move

1. Open `Moves.csv` in a text editor or spreadsheet.
2. Add a new row with a unique `MoveId` (snake_case).
3. Fill gameplay columns using enum values from this legend (column order may follow the export header).
4. Optionally run `Validate Moves CSV`, then in Unity: `Tools > Chainbound > Combat > Data Importer > Import Moves (CSV)`.
5. Run `Audit Moves CSV vs Assets` if you need to confirm SO parity.

### Rebalancing a status

1. Open `Statuses.csv`.
2. Find the row by `StatusId`.
3. Change `Duration`, `EoTDamagePercent`, `StageEffects`, etc.
4. Re-import: `Import Statuses (CSV)`.

### Adding a modifier

1. Open `Modifiers.csv`.
2. Add a row with a unique `SectionId` (e.g., `2.1-f`) and a short `DisplayName`.
3. Fill category, scope, rarity, effect values, and restrictions.
4. Re-import: `Import Modifiers (CSV)`.

### Assigning moves to a species

1. Open `SpeciesMovePools.csv`.
2. Find or add the species row.
3. List the 4-5 progression moves in `LearnableMoves` (pipe-separated). Put species-locked moves in `ExclusiveMoves` instead.
4. Set `FlagshipMove` to the signature. Import gives it weight 8, and weight 24 at 5 stars. Every other pool move gets weight 23.
5. List `InitialMoves`. Every initial move must also exist in that species' learnable or exclusive pool.
6. List 0-2 typed theme exceptions in `OffTypeMoves`. These equip even when the type does not match. They may also sit in the progression pool.
7. Re-import: `Import Species Pools (CSV)`.
