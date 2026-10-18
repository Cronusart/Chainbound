# Combat issues master

Date: 2026-10-16. Index of the combat audit and design notes on this branch. No gameplay, CSV, or test change is in this file. Detail stays in the linked section. This list does not close any open item.

## How to use

Pick up a row. Read the linked section before editing code or data. This page is the list. The source section is the spec.

**Status**

| Status | What an implementing agent should do |
|---|---|
| fixed in PR | Already landed on this branch. Do not redo it. |
| open | The defect is identified and the source states the expected behavior. It is not landed. |
| awaiting decision | Change, remove, or implement is still a human call, or a design option is unchosen. Do not start the work. |
| approved proposal | The developer approved this design. It is not in the game yet. |

**Work** is what a landing needs. `none` means nothing left to build. `data` means CSV, catalog, spec, or comment text. `engine` means resolver, status processor, AI, or a new status id. `both` means the landing needs engine and data. For an awaiting-decision move, Work is the cost of **Implement**. Change and Remove are data. Do not treat that column as permission to implement.

**Rules that stay in force**

- Do not invent a hit count for Ash Guard, Compound Guard, or Ice Wall (E-17). `hitsRemaining == 0` is inactive, not unlimited.
- Do not retune powers while `all_in`, `recoil_strike`, `volatile_shot`, and `crystal_bash` are still awaiting a change / remove / implement call (BAL-COST).
- Stamp modifiers `3.2-b`, `3.2-c`, `7.2-c`, `10.1-d`, `10.2-b`, and `10.2-c` are another agent's work (E-23). Do not edit them.
- Do not edit `Docs/GameTheory/`.
- Group Hug modifier `4.5-a` is not Relay. Relay pays +1 AP to an ally this Chainling just buffed. Group Hug pays +1 AP to each living active ally after any Support.
- When a row lands, change its status here to `fixed in PR` and leave the row so the next agent does not redo it.

**Later notes override earlier proposals**

- Node passive is Relay (APR-RELAY). Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, and Open Line are withdrawn. Do not build them.
- Burned passive is Kindling at 6% (APR-KIND). Cinder Idle and Scorch Echo are withdrawn. Sear Echo is the unchosen alternative, not an approval.
- Compressed C1–C7 and Bot D1–D7 were drafted before the typed-pool rule and were not re-cut. They are not approved (DEC-CD).
- The twenty design-idea moves and mechanics M1–M6 are not approved (DEC-PACK). `node_beacon` and `cinder_idle` in that list are the withdrawn passives above.

**Stale text an agent can misread**

- `Audits/combat_species_pool_routing_inventory.md` marks empty-payload rows `GapType none` because the asset matches the CSV. Both sides are empty. See DEC-STALE.
- `Validape.asset` design notes still name Extend Protocol as an exclusive. The species pool does not.
- `UNIMPLEMENTED_MOVE_EFFECTS.md` says `system_reset` spends 2 AP. `Moves.csv` `APCost` is 3 (MV-system_reset).
- `COMBAT_SYSTEMS_ANALYSIS.md` still names Node Beacon and Cinder Idle. Type-gap proposals replaced both.
- `Audits/moves_import_validation.md` records the Ice Wall comma errors as resolved. The hit budget is still E-17. `Audits/resolver_seven_buckets_playtest.md` is a playtest checklist, not an open-issue list.

**Named decisions**

| Topic | Row |
|---|---|
| 6 AP Bean Beam | DEC-BEAM |
| Hit budgets of 0 | E-17 |
| HODLed double Def | E-11 |
| System Reset AP mismatch | MV-system_reset |
| Relay | APR-RELAY |
| Kindling | APR-KIND |

## Counts

| | Rows |
|---|---:|
| All rows | 115 |
| fixed in PR | 7 |
| open | 7 |
| awaiting decision | 99 |
| approved proposal | 2 |
| Work `none` | 9 |
| Work `data` | 43 |
| Work `engine` | 44 |
| Work `both` | 19 |

| Group | Rows |
|---|---:|
| Fixed in this PR | 7 |
| Correctness still open | 10 |
| Empty payloads and half cards | 26 |
| Hits whose rider or cost does not run | 31 |
| Status bodies that do not match | 7 |
| Statuses with no producer | 13 |
| Balance flags | 6 |
| Open design decisions | 13 |
| Approved proposals | 2 |

E-18 is the empty Accuracy / Evasion bucket (7 moves). E-19 is the missing-payload bucket (the 13 whole cards, plus the rider rows). Those finding ids are the group headers below, not extra rows. E-27 is the producer list, not an extra row.

## Fixed in this PR

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-01 | Bean Sip stays `rechargeTurns` 2, the card says 1 skipped turn, and the move grants +1 AP. No 6 AP beam was added. | correctness | fixed in PR | none | [E-01](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-01--bean-sip-recharge-sentence) |
| E-02 | AP Boost and Extend Protocol stay at 3 and say 2 skipped turns. Fox Feint stays at 999 and the log says once per battle. Gecko Guard's unused 999 was left. | correctness | fixed in PR | none | [E-02](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-02--same-recharge-sentence-on-the-other-recharge-moves) |
| E-15 | Apply-once and repeating stages are owned by the status and revert when it expires. Refresh does not reapply apply-once stages. | correctness | fixed in PR | none | [E-15](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-15--for-n-turns-stages-that-never-revert) |
| E-16 | Ice Wall's description comma is quoted, so both opcodes import. Mitigation `hitBudget` is still 0 (E-17). | correctness | fixed in PR | none | [E-16](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-16--ice-wall-csv-comma-breaks-the-import) |
| E-22 | Group Hug no longer copies a Support. Each living active ally gains +1 AP. The host move stays single-target. | correctness | fixed in PR | none | [E-22](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-22--group-hug-does-not-reduce-potency) |
| E-26 | FUD's stages revert with the status. Reinforced still has no producer (NP-reinforced); if applied, Def +3 reverts. | correctness | fixed in PR | none | [E-26](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-26--fud-and-reinforced-duration-versus-a-permanent-stage) |
| E-29 | Bench recharge ticks. Other statuses still pause. Spec §1.2 matches. | correctness | fixed in PR | none | [E-29](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-29--bench-pause-spec-line-versus-the-clock-the-tests-lock) |

## Correctness still open

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-03 | Slowed's compact label says a 25% action loss. The spec and the resolver are Speed −2 only. The unused 25% constant was left so it is not wired back in. | correctness | open | data | [E-03](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-03--slowed-label-says-a-25-action-loss) |
| E-04 | Drip heals 6% MaxHP on every end step for 3 ticks, including the granting turn. Spec §7.6 still says "once". | correctness | open | data | [E-04](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-04--drip-once-versus-every-end-of-turn) |
| E-05 | Cascade Failure's code comment says KO. The third apply deals 25% MaxHP and can KO only if HP hits 0. | correctness | open | data | [E-05](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-05--cascade-failure-comment-says-ko) |
| E-06 | The FUD comment lists Accuracy. The catalog stages are Atk, Def, and Speed. Accuracy is not a `CoreStat`. | correctness | awaiting decision | both | [E-06](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-06--fud-comment-lists-accuracy) |
| E-20 | `streamlined_flow` drops Def −1 twice, from the stage column and from `ChangeStage`. The pass did not pick which write to keep. | correctness | awaiting decision | data | [E-20](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-20--streamlined-flow-drops-defense-twice) |
| E-21 | `compression_burst` says the first hit lowers Def. `ApplyMoveStageChanges` runs per hit, so both hits drop Def. | correctness | awaiting decision | both | [E-21](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-21--compression-burst-lowers-defense-on-every-hit) |
| E-23 | Six stamp modifiers have no effect body: Side Quest, After Hours, Setup Feed, All-Stat Burn, Anniversary Mark, Tournament Badge. Another agent owns them. | correctness | open | engine | [E-23](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-23--six-stampable-modifiers-have-no-effect-body) |
| E-24 | Tripwire applies a permanent Speed −1 by searching the description for "slow". It does not apply Slowed. | correctness | open | engine | [E-24](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-24--tripwire-is-a-permanent-speed--1) |
| E-25 | `charged_relay` reads the hit ledger when the utility resolves, so a turn whose only action is Charged Relay has not recorded those hits yet. | correctness | awaiting decision | both | [E-25](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-25--charged-relay-checks-the-hit-ledger-at-cast-time) |
| E-28 | A passive status with a non-self `targetOverride` still applies to the owner. No live passive authors that override. | correctness | awaiting decision | engine | [E-28](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-28--passive-status-target-override-is-ignored) |

## Empty payloads and half cards

E-18: the stage column cannot name Accuracy or Evasion. `ChangeStage` already accepts both, so an Implement of a plain Acc/Eva stage is data. E-19: blank `StatusEffects`, `StageChanges`, and `UtilityEffects` are copied through as empty lists. The asset matches the CSV. The payload was never authored. Each row is still change, remove, or implement.

### Accuracy and Evasion only (E-18)

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-accuracy_focus | Common, 1 AP. Tooltip is own Acc +2. Spends AP and applies nothing. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-evasion_maneuver | Common, 1 AP. Tooltip is own Eva +2. Empty payload. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-accuracy_disrupt | Common, 1 AP. Tooltip is target Acc −1. Empty payload. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-evasion_pin | Common, 1 AP. Tooltip is target Eva −1. Empty payload. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-gas_cloud | Common, 2 AP, AllEnemies. Tooltip is Acc −1 and Eva −1 on all foes. Empty payload. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-dust_fees | Common, 1 AP. Tooltip is target Acc −1 and Eva −1. Empty payload. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-optimize_aim | Common, 1 AP. Tooltip is Acc +2. Empty payload. | empty payload | awaiting decision | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |

### Half the card (E-18 riders)

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-chain_seal | Speed −1 is real. The Eva −1 sentence does not run. | unimplemented move | awaiting decision | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-key_rotation | Def +1 is real. Eva +1 does not run. | unimplemented move | awaiting decision | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-zip_step | Only Def −1 runs, so the card's downside is the whole move. Eva +2 does not. | unimplemented move | awaiting decision | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-pivot_line | Only Speed +1 runs. Acc +1 and Eva +1 do not. | unimplemented move | awaiting decision | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-precision_strike | Power 64, accuracy 95, 2 AP. The extra +20 accuracy does not run. No accuracy-bonus opcode exists. | unimplemented move | awaiting decision | engine | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-shard_scatter | Power 18, 2–3 hits, AllEnemies. The per-hit Eva drop on a different foe does not run. | unimplemented move | awaiting decision | engine | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |

### Whole card does not run (E-19)

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-ap_drain | Common, 1 AP. No drain and no 25% steal. `DrainAP` and `StealAP` already exist. | empty payload | awaiting decision | data | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-transaction_tax | Common, 1 AP. Target does not lose 1 AP, and the user does not gain it. | empty payload | awaiting decision | data | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-extend_protocol | Recharge 3 runs and the card says 2 skipped turns. No ally buff is extended. There is no extend opcode. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-blockade | Common, 3 AP. No mitigation window. A hit budget of 0 would still leave it inactive (E-17). | empty payload | awaiting decision | data | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-script_loop | Common, 1 AP. Does not extend debuffs when the target has 2 or more. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-network_sync | Common, 2 AP, AllAllies. Does not raise each ally's lowest stage. There is no lowest-stage opcode. Blank in the CSV and the asset. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-system_reset | Strip and self-skip do not run. The unimplemented doc says it spends 2 AP. `Moves.csv` line 94 has `APCost` 3. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run), [AP mismatch](Audits/EMPTY_PAYLOAD_INVESTIGATION.md#same-failure-other-moves) |
| MV-status_resistance | Passive. General debuffs do not expire 1 turn faster. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-isolation_tool | Common, 1 AP. Does not force the target's next move to be single-target. Isolated is a different status. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-viral_surge | Common, 1 AP. No AP coin flip and no random stage. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-wildcard_swap | Common, 1 AP. Does not swap a stage with the target. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-hype_burst | Common, 2 AP. Does not strip buffs or gain Atk per buff removed. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-extraction_hit | Passive. Hits do not raise Atk, and Atk +3 does not also grant Speed. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |

## Hits whose rider or cost does not run

The hit itself resolves. The extra sentence does not. Missing costs on `all_in`, `recoil_strike`, and `volatile_shot` are also BAL-COST. Do not retune other powers off these indices.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-all_in | Power 98, accuracy 95, AP column 0, field index 93.10. No all-AP spend, no recoil, no KO refund. Largest index in the file, and it is free. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-recoil_strike | Power 76, AP 2, index 72.20. The 15% MaxHP recoil does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-volatile_shot | Power 72, AP 2, index 68.40. A miss does not cost 15% MaxHP. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-jackpot_shot | Power 64, AP 2. Damage is the normal roll. The double / normal / half branch does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-hype_train | Rare, power 52, index 49.40. The consecutive-hit ramp does not run, so it loses to Common `solid_strike` (60.80). | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-lucky_scatter | Rare, power 18, 1–4 hits, index 42.75. The 25% random buff per hit does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-crystal_bash | Rare, power 0, index 0. Def is not the attack stat, and the below-30% HP double does not run. `DamageOverrideAttackStat` already exists on `bulwark_blow`. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-glitch_barrage | Rare, power 16, 2–4 hits. Every hit uses the chosen target. The random self-hit does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-cipher_shot | Power 64, AP 2. The 25% chance to block the target's next buff does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-cipher_burst | Rare, power 22, 2–3 hits, index 52.25. The per-hit buff erase does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-ap_siphon | Power 56, AP 3. The 50% drain of 1 AP does not run. `DrainAP` exists. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-deep_dig | Power 72, AP 3, index 68.40. Defense stages apply in full. The 50% Def-stage ignore does not run. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-momentum_drill | Power 64, AP 2. No +25% versus a debuffed target. `DebuffCountAtLeast` already gates `bug_payoff`. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-ore_strike | Power 64, AP 2. No +10% per own buff, cap +30%. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-stake_strike | Power 64, AP 2. No +10% per own Def stage, cap +30%. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-yield_spike | Power 64, AP 3. At 4+ AP there is no +25% and no extra AP spend. | unimplemented move | awaiting decision | both | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-interest_strike | Rare, power 18, 2–3 hits, index 42.75. No AP grant when Def stage exceeds Atk. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-rallying_slam | Rare, power 64, AP 2. No +1 AP when Def stage exceeds Atk. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-pickaxe_flurry | Power 16, 2–4 hits. Hits do not raise Atk, and there is no +2 cap. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-combustive_shift | Rare, power 22, 2 hits. No bonus versus debuffed targets who are missing AP. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-crash_routine | Always applies Atk −1. The below-50% target HP damage branch does not run. | unimplemented move | awaiting decision | both | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-glitch_feedback | The +1 AP surcharge runs. The cleanse of 1 debuff does not. `Cleanse` exists. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-bug_fix | Heal 10% runs. The cleanse and the −20% damage next turn do not. | unimplemented move | awaiting decision | both | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-dice_roll | Heal 20% runs. The random heal / debuff / +2 AP branch does not. The tooltip's heal number and the 20% also disagree. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-recursive_backup | The immediate 20% heal under 50% HP runs. There is no end-of-turn heal and no duration reset. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-data_scramble | Self Atk −1, self Speed −1, and +2 AP run. The target's stages do not. | unimplemented move | awaiting decision | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-risk_spike | Atk +2 and Def −1 run. The next-hit +25% damage and +10% recoil do not. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-guard_stance | Def +2 runs and stays, because it is a move-column stage. The 3-turn contact-recoil window does not. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-rally_cry | Allies' Speed +1 runs. The user's end-of-turn +1 AP does not. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-refinery | Def +1 runs and stays. The +1 AP at end of turn for 2 turns does not. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-double_or_nothing | The 60/40 AP roll runs. The 40% branch does not grant AP-drain immunity. | unimplemented move | awaiting decision | engine | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |

## Status bodies that do not match

The move or the chemistry cell applies the status. The extra sentence on the status does not run. Shared bodies are one row, not one row per move.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-07 | Jammed is 8 v1 cells and lasts 2 turns. Acc −2 is not applied. The stage column cannot store Accuracy. | status body | awaiting decision | engine | [Jammed](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#chemistry-outputs-whose-card-does-not-match-the-body) |
| E-08 | Leaked's summary says 1/10 HP to all on action. The catalog is a 10% MaxHP self DoT at end of turn. Two v1 cells. | status body | awaiting decision | both | [Leaked](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#chemistry-outputs-whose-card-does-not-match-the-body) |
| E-09 | Backlash (`BacklashType`) says contact adds a typed DoT. Duration 3, no contact DoT body. Two v1 cells. | status body | awaiting decision | engine | [Backlash](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#chemistry-outputs-whose-card-does-not-match-the-body) |
| E-10 | Dusted Speed −3 now reverts with the status. Eva −3 is still not applied. Four v1 cells. | status body | open | engine | [Dusted](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-10--dusted-is-a-permanent-speed--3-and-evasion-is-missing) |
| E-12 | `mev_extract` applies MEV Extracted for 2 turns. The catalog drains 1 AP per turn. It does not steal. | status body | awaiting decision | engine | [MEV](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#status-text-the-move-applies-and-the-status-body-does-not-match) |
| E-13 | `deflation_mark` applies Burned Tokens and a permanent Atk −1. Enemy DoT does not heal the user. The status has no resolver body. | status body | awaiting decision | engine | [Burned Tokens](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#status-text-the-move-applies-and-the-status-body-does-not-match) |
| E-14 | Overleveraged is applied by `voltage_rush` and `risk_marker` and then does nothing. Spec §7.5 says 25% of damage dealt. Consumed duration 2 still needs a call. | status body | awaiting decision | engine | [Overleveraged](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#status-text-the-move-applies-and-the-status-body-does-not-match) |

## Statuses with no producer

E-27. No producer was added and no row was deprecated. The call is author a producer or move the row onto the deprecated list. Deprecated chemistry ids in `COMBAT_Chemistry.txt` §7 are a different list. HODLed's double ramp is E-11 in this table.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| NP-rekt | Action-loss 25% is implemented and unused. No move, not deprecated. Unfinished authoring. | no producer | awaiting decision | data | [Rekt](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#rekt) |
| NP-offline | Wakes on any damage. The wake is implemented. No move, not deprecated. | no producer | awaiting decision | data | [Offline](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#offline) |
| NP-glitchedmind | Mis-act at 50% is implemented. The Glitched profile still names it. No move. | no producer | awaiting decision | data | [GlitchedMind](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#glitchedmind) |
| E-11 | No move and no v1 cell applies HODLed. No stat-drop immunity. Repeating Def +1 plus an extra +1 while Def is below +3, so the first end tick can reach +2. Both writes revert if it expires. The ramp was not retuned. | design decision | awaiting decision | both | [E-11](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-11--hodled-ramps-twice-and-does-not-stop-at-3), [HODLed](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#hodled) |
| NP-duststorm | Catalog says field Evasion down on apply. No resolver read, no move, not deprecated. | no producer | awaiting decision | both | [DustStorm](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#duststorm) |
| NP-forkedchain | Forked profile names it beside Splinter. Splinter is live. ForkedChain has no body and no move. | no producer | awaiting decision | both | [ForkedChain](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#forkedchain) |
| NP-compresseddata | Compressed profile names it. Catalog stage is Speed −1 only. The summary also says Eva +2, which the stage column cannot store. | no producer | awaiting decision | both | [CompressedData](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#compresseddata) |
| NP-memesurge | No resolver body for the random-stat summary. Left with the Meme wildcard, not deleted from a working move. | no producer | awaiting decision | both | [MemeSurge](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#memesurge) |
| NP-noderelay | Node profile names it. "Redirect healing" was never wired. `effect_relay` and `relay_packet` move a buff. They do not apply this status. Distinct from passive `relay`. | no producer | awaiting decision | both | [NodeRelay](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#noderelay) |
| NP-unsealed | Resolver consumes it for a 50% barrier bypass. Spec §10.1 says support moves apply it. No move does. | no producer | awaiting decision | data | [Unsealed](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#unsealed) |
| NP-relay-status | Resolver consumes `StatusId.Relay` for a full barrier bypass. No move applies it. Not the Node passive `relay`, and not Effect Relay or Relay Packet. | no producer | awaiting decision | data | [Relay](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#relay) |
| NP-bridgedout | Catalog says swap with ally. No resolver body and no move. Bridged, a different id, is the deprecated one. | no producer | awaiting decision | both | [BridgedOut](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#bridgedout) |
| NP-reinforced | v0 had one cell. v1 cut it and kept the catalog row. If applied, Def +3 now reverts (E-26). | no producer | awaiting decision | data | [Reinforced](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#reinforced) |

## Balance flags

Value model and the out-of-order tables are in the balance audit. No power was edited. These flags wait on the move calls above.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| BAL-RARE | Typeless Rare singles sit under the typed Common band: Impact Knuckle 37.80, Vector Shot 34.20, and Rupture Point 30.60 versus power-64 index 60.80 at 2 AP. Do not move numbers yet. | balance | awaiting decision | data | [Spread ladder](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#spread-ladder) |
| BAL-SPREAD | At 2 AP, Common `baseline_barrage` (47.50) is above Rare `refined_volley` (39.60). At 3 AP, Common `fork_split` (87.40) is above Rare `refined_barrage` (54.00) and `refined_blast` (50.40). | balance | awaiting decision | data | [Spread ladder](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#spread-ladder) |
| BAL-EPIC | `bulwark_blow` is the only Epic damage move (index 45.00 at 3 AP, attacks with MeleeDef). The Def column is real. On an attacker the Atk index stays under several AP-3 Commons. Leave it until a retune. | balance | awaiting decision | data | [Strictly worse](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#strictly-worse-as-shipped) |
| BAL-COST | Do not use `all_in`, `recoil_strike`, or `volatile_shot` as the Common template, and do not retune until those three plus `crystal_bash` have a call. | balance | awaiting decision | none | [Suggestions](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#suggestions) |
| BAL-EMPTY | `hype_train`, `glitch_barrage`, `cipher_burst`, `lucky_scatter`, and `interest_strike` are Rare in the table and Common in the resolver. Implement the rider or drop the rarity. | balance | awaiting decision | data | [Suggestions](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#suggestions) |
| BAL-61 | `COMBAT_Moves.txt` §6.1 still says rarity is complexity and availability, and that a Common stays competitive with Epic. The balance screen follows the developer rule instead, and it does not edit §6.1. | balance | awaiting decision | data | [Suggestions](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#suggestions) |

## Open design decisions

Proposals only, except where a row above is already the defect. Do not ship a shortlist item, a design-idea card, or a Compressed / Bot concept from this table.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-17 | Ash Guard, Compound Guard, and Ice Wall author `MitigationWindow` with `hitBudget` 0, so they do not mitigate. Ice Wall's speed-based +1 AP can still run. No hit count was chosen. | design decision | awaiting decision | data | [E-17](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-17--hit-budget-0-turns-mitigation-off), [Dead walls](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#dead-walls) |
| DEC-BEAM | Cofi's kit is `bean_sip` plus `civet_beam` (power 12, 2 AP). Utility AI has no 6 AP check. Options A Espresso Shot, B Double Bean, and C Cream Top are unchosen. Any of them needs an AI gate or Cofi keeps sipping. | design decision | awaiting decision | both | [Bean Beam](Design/COMBAT_DESIGN_IDEAS.md#4-boss-design-cofi-lootwaks-stronger-bean-beam) |
| DEC-S1 | Shortlist 1, not approved. Data-only opcode pack: Fee Tap, Cipher Skim, Relay Cut, Brace Yield, Mempool Mark, Aim Lock. | design decision | awaiting decision | data | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S2 | Shortlist 2, not approved. Utility AI subtracts for recharge and can bonus an AP threshold. This does not add the 6 AP beam. | design decision | awaiting decision | engine | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S3 | Shortlist 3, not approved. Two 1 AP chips, Gas Vent and Anchor Tap. Rapid Strike's number stays until the cost rows are decided. | design decision | awaiting decision | data | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S4 | Shortlist 4, not approved. One above-band utility priced with `rechargeTurns` 2. Depends on DEC-S2. Does not change Fox Feint's 999. | design decision | awaiting decision | data | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S5 | Shortlist 5, not approved. A 1 AP toll on a voluntary swap. KO swaps and forced swaps stay free. | design decision | awaiting decision | engine | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-PACK | The other design-idea moves (20) and mechanics M1–M6 are not approved. `node_beacon` and `cinder_idle` in that list are withdrawn. Finality and Reorg stay held back. | design decision | awaiting decision | both | [New moves](Design/COMBAT_DESIGN_IDEAS.md#2-new-move-ideas), [Mechanics](Design/COMBAT_DESIGN_IDEAS.md#3-new-mechanics) |
| DEC-CD | Compressed C1–C7 and Bot D1–D7 are not approved. They were drafted before the typed-pool rule and were not re-cut. | design decision | awaiting decision | both | [Recommended picks](Design/TYPE_GAP_PROPOSALS.md#recommended-picks) |
| DEC-AI | Systems proposals A–E are not approved: write the target slot, a Utility lethal check, defense and chemistry in `EstimateDamage`, recharge and hurt-ally weights, and Defensive personality on walls. | design decision | awaiting decision | engine | [AI proposals](Design/COMBAT_SYSTEMS_ANALYSIS.md#proposals) |
| DEC-CHEM | Leave ×1.2 / ×0.8. A later chart edit could cap one output at 4 cells and move extra Rugged and Jammed onto thin families, only after Jammed's accuracy body (E-07) is decided. Meme stays out. | design decision | awaiting decision | data | [Chemistry](Design/COMBAT_SYSTEMS_ANALYSIS.md#what-is-dominant-thin-or-dead) |
| DEC-PACE | The pacing note treats 2–3 hits as the intended KO for a neutral power-64 exchange. Bean Beam is the drag. The fix is DEC-BEAM, still unchosen. | design decision | awaiting decision | none | [Pacing](Design/COMBAT_SYSTEMS_ANALYSIS.md#do-regen-recharge-and-healing-drag-or-snowball) |
| DEC-STALE | The species-pool inventory's `GapType none` on empty payloads is stale, and Validape's design note still calls Extend Protocol an exclusive. The empty-payload investigation is the current finding. | design decision | open | data | [Other sources](Audits/EMPTY_PAYLOAD_INVESTIGATION.md#other-sources) |

## Approved proposals

These two are approved and not implemented. Everything else in the design docs is a proposal or a withdrawal.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| APR-RELAY | Uncommon Node passive `relay`. When this Chainling applies a buff to an ally, that ally gains +1 AP, once per ally per turn. Turn 1 does not pay with working cards (`pack_rally` is 3 AP). Not Group Hug. Not data-only. | approved proposal | approved proposal | both | [Relay](Design/TYPE_GAP_PROPOSALS.md#relay-recommended) |
| APR-KIND | Uncommon Burned passive `kindling`, approved at 6% MaxHP. A damaging hit that leaves a living foe applies Kindled for one end step. Keep the stack with Rugged. The optional shared-end-step trim was not adopted. Sear Echo was not chosen. | approved proposal | approved proposal | both | [Kindling](Design/TYPE_GAP_PROPOSALS.md#kindling-recommended), [Approval](Design/TYPE_GAP_PROPOSALS.md#guardrails-and-the-recommendation) |
