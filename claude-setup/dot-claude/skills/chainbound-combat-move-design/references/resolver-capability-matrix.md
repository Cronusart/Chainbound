# Resolver Capability Matrix

Use this matrix before authoring move claims. `Moves.csv` is the source of truth; imported assets and the resolver must carry the same contract.

## Data-only supported effects

- Identity, classification, targeting, contact, power, AP, accuracy, hit count, recharge, tags, status application, stage changes, and percent-MaxHP healing.
- Priority through `PRIOR`; active selection through `SelectionPolicy`, `ActiveConditionType`, and `ActiveConditionThreshold`.
- HP sacrifice for tagged full-absorb barriers through `HpCostPercent`.
- Resolve-profile timing and gating through `ResolvePrimaryTiming`, `ResolveUtilityTiming`, `ResolveConditionalPolicy`, `ResolveConditions`, and `ResolveRequireAll`.
- Damage formula overrides through `DamagePowerMultiplier`, `DamagePreMitigationMultiplier`, `DamageFinalMultiplier`, `DamageOverrideAttackStat`, and `DamageAttackStat`.
- Conditional damage through fixed conditions, count scaling, caps, and seeded discrete random multipliers. It is evaluated per hit against the final defender after redirect and immediately before `DamageCalculator.CalculateDamageDetailed`.

Supported resolve conditions:

- HP below/at-or-below/above percentage; AP at least/at most.
- Has/missing status; status count; debuff count; no buffs.
- Stage at least/at most; one stage at least another; Speed higher than the comparison target.
- Barrier present/absent; buffed active-partner count.
- Hit/damaged/not-damaged last turn; did not use a damaging move this turn; any active foe has more AP.

## UtilityEffects opcodes

The importer, round-trip exporter, runtime clone, and executor support:

- Walls: `MitigationWindow`, `FullAbsorbBarrier`, `DamageThresholdBarrier`, `WallHitRetaliation`, `GainApOnDamageIfSpeedHigher`.
- AP: `GainAP`, `DrainAP`, `StealAP`, `TransferAP`, `RefundAP`, `NextMoveAPSurcharge`, `BankAPUntilNextTurn`, `GainApIfDistinctTargetsHitAtLeast`.
- Status/heal: `TurnStartPendingStatus`, `Cleanse`, `BonusHealPercentIfSpeedHigher`, `LifestealPercent`, `HealPercentIfStageAtLeast`.
- Stages/passives: `IncomingDamageMitigation`, `ChangeStage`, `RandomStageChange`, `MoveAPDiscountWhileHPAbove`.
- Delayed/field: `ArmUtilityWindow`, `FieldEntryStageChange`.

`ArmUtilityWindow` currently supports `FoeKO`, `EnemyMiss`, `IncomingDebuff`, `IncomingHit`, and `OwnerTurnStart`, with AP delta, team/self healing, one stage delta, and MaxHP-percent counter damage. Windows clear on swap-out.

`FieldEntryStageChange` stores a team-owned field entry effect and applies it after normal swap-in modifiers on voluntary and automatic swaps.

## Passive pipeline

Generic passive payload execution supports `HPBelowThreshold`, `OnDamageTaken`, `OnMiss`, `EndOfTurn`, `TurnStart`, `OnHit`, `OnCrit`, `OnAllyHit`, `OnBuffExpired`, `OnAction`, `OnKO`, and `OnAllyKO`.

When a passive fires, standard stages, statuses, healing, and typed utility payloads execute without a new move-ID branch. KO/ally-KO hooks cover direct-hit and end-of-turn status KOs. Existing once-per-battle and HP-threshold metadata remain authoritative.

## Supported special families

These older families intentionally retain explicit resolver routing:

- `position_swap`.
- `ap_donation`, `ap_redistribution`, `ap_router`.
- `damage_intercept`, `signal_redirect`.
- `effect_relay`, `relay_packet`.
- Broadcast/Bridged target expansion and consumption.
- Barrier bypass from Relay, Unsealed, and modifier `4.3-b`.

Do not add a new ID to a family unless it truly shares that family contract.

## Known limits / resolver work

- Refunds based on the exact amount a wall blocked are not represented; descriptions must omit that rider until a typed wall-block payoff exists.
- Armed windows carry the documented compact payload, not arbitrary nested utility lists.
- Field support currently covers stage changes on entry, not general auras, periodic field damage, or status-wide rule replacement.
- Random support covers seeded random stage/stat changes and discrete damage multipliers. Arbitrary random effect branches or random self/enemy action targeting need a new typed representation.
- Buff negation, future-buff prevention, broad status stripping, and general status-duration editing remain unsupported beyond existing status-processor behavior.
- Legacy `hardcoded_defId` passives listed in the species-pool audit remain compatibility routes and should be migrated before expanding their designs.

## Extension requirements

For an unsupported effect, add a reusable data representation, execute it in the correct resolver subsystem, log why it fired or skipped, and test targeting, AP clamps, status origin, stage caps, KO timing, swap behavior, redirect, walls, and multi-hit behavior. Avoid isolated move-ID branches.
