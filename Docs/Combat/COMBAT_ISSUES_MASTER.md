# Combat issues master

Date: 2026-10-19. Index of the combat audit and design notes on this branch. Detail stays in the linked section; implemented rows remain unverified until the manual battle smoke is recorded. Reconciliation plan: `C:\Users\mando\.cursor\plans\audit_reconciliation_v1.plan.md`; automated evidence: [audit_reconciliation_v1_tests.md](Audits/audit_reconciliation_v1_tests.md).

## How to use

Pick up a row. Read the linked section before editing code or data. This page is the list. The source section is the spec.

**Status**

| Status | What an implementing agent should do |
|---|---|
| fixed in PR | Already landed on this branch. Do not redo it. |
| open | The defect is identified and the source states the expected behavior. It is not landed. |
| awaiting decision | Change, remove, or implement is still a human call, or a design option is unchosen. Do not start the work. |
| approved proposal | The developer approved this design. It is not in the game yet. |
| implemented, unverified | EditMode tests passed locally. A manual battle smoke test is still required before `fixed in PR`. |

**Work** is what a landing needs. `none` means nothing left to build. `data` means CSV, catalog, spec, or comment text. `engine` means resolver, status processor, AI, or a new status id. `both` means the landing needs engine and data. For an awaiting-decision move, Work is the cost of **Implement**. Change and Remove are data. Do not treat that column as permission to implement.

**Rules that stay in force**

- Ash Guard, Compound Guard, Ice Wall, and Blockade use `hitsRemaining == -1` for unlimited hits during their authored turn window (E-17). `0` remains inactive.
- Self-damage is shelved. The affected cards now use non-HP tradeoffs recorded in [SELF_DAMAGE_SHELF.md](Design/SELF_DAMAGE_SHELF.md); future self-damage work must retain the boss mutual-wipe rule.
- Stamp modifiers `3.2-b`, `3.2-c`, `7.2-c`, `10.1-d`, `10.2-b`, and `10.2-c` are another agent's work (E-23). Do not edit them.
- Do not edit `Docs/GameTheory/`.
- Group Hug modifier `4.5-a` is not Uplink. Uplink pays +1 AP to an ally this Chainling just buffed. Group Hug pays +1 AP to each living active ally after any Support.
- When a row lands, leave the row. Use `implemented, unverified` until a manual battle smoke test is noted, then `fixed in PR`.

**Later notes override earlier proposals**

- Node passive is Uplink (`uplink`, APR-RELAY). Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, and Open Line are withdrawn. Do not build them. Catalog status Relay stays.
- Burned passive is Kindling at 6% (APR-KIND). Cinder Idle and Scorch Echo are withdrawn. Sear Echo is the unchosen alternative, not an approval.
- Compressed C1–C7 and Bot D1–D7 were drafted before the typed-pool rule and were not re-cut. They are not approved (DEC-CD).
- The twenty design-idea moves and mechanics M1–M6 are not approved (DEC-PACK). `node_beacon` and `cinder_idle` in that list are the withdrawn passives above.

**Stale text an agent can misread**

- `Audits/combat_species_pool_routing_inventory.md` marks empty-payload rows `GapType none` because the asset matches the CSV. Both sides are empty. See DEC-STALE.
- `Validape.asset` design notes no longer call Extend Protocol an exclusive. Extend Protocol recharge is 2. The E-02 sentence that leaves it at 3 is historical.
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
| All rows | 128 |
| fixed in PR | 42 |
| implemented, unverified | 58 |
| awaiting decision | 18 |
| superseded | 4 |
| deprecated | 4 |
| partial | 2 |
| Work `none` | 92 |
| Work `data` | 23 |
| Work `engine` | 9 |
| Work `both` | 4 |

| Group | Rows |
|---|---:|
| Fixed in this PR | 7 |
| Combat balance follow-up v1 | 10 |
| Correctness still open | 11 |
| Empty payloads and half cards | 28 |
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
| E-02 | AP Boost stays at 3. Extend Protocol is recharge 2 (one skipped turn). Fox Feint stays at 999 and the log says once per battle. Gecko Guard's unused 999 was left. | correctness | fixed in PR | none | [E-02](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-02--same-recharge-sentence-on-the-other-recharge-moves) |
| E-15 | Apply-once and repeating stages are owned by the status and revert when it expires. Refresh does not reapply apply-once stages. | correctness | fixed in PR | none | [E-15](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-15--for-n-turns-stages-that-never-revert) |
| E-16 | Ice Wall's description comma is quoted, so both opcodes import. Its mitigation now uses the E-17 unlimited hit budget. | correctness | fixed in PR | none | [E-16](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-16--ice-wall-csv-comma-breaks-the-import) |
| E-22 | Group Hug no longer copies a Support. Each living active ally gains +1 AP. The host move stays single-target. | correctness | fixed in PR | none | [E-22](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-22--group-hug-does-not-reduce-potency) |
| E-26 | FUD's stages revert with the status. | correctness | fixed in PR | none | [E-26](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-26--fud-and-reinforced-duration-versus-a-permanent-stage) |
| E-29 | Bench recharge ticks. Other statuses still pause. Spec §1.2 matches. | correctness | fixed in PR | none | [E-29](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-29--bench-pause-spec-line-versus-the-clock-the-tests-lock) |

## Combat balance follow-up v1

These changes are implemented and covered by focused automated tests. They stay unverified until the manual battle smoke is recorded.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| SD-SHELF | Self-damage remains shelved. Hard Fork, Gas War, Bit Rot, and Leverage Up keep their old ids but use non-HP costs/riders; Guard Stance's recoil clause was removed. | design decision | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| CL-312 | `CipherLocked = 312` is a two-turn General debuff that blocks new buff statuses, buff extension, passive/chemistry ramps, and positive stages while preserving existing buffs and negative/reversion deltas. | correctness | implemented, unverified | none | [Baseline and verification](Audits/followups_v1_baseline_tests.md) |
| PR9-INTEG | PR #9 behavior is integrated once: Bean Sip +1 AP, Group Hug active-ally AP, bench recharge ticking, stage ownership/reversion, recharge copy, Sear Field 310, and Kindled 311. | integration | implemented, unverified | none | [Baseline and verification](Audits/followups_v1_baseline_tests.md) |
| ACC-TUNE | Resolve profiles carry reusable accuracy tuning. Precision Strike authors `SureHit`, ignores Evasion, and clamps to at least 95% hit chance. | engine/data | implemented, unverified | none | [CSV/asset parity](Audits/moves_csv_asset_parity.md) |
| BOSS-DRAW | Boss mutual wipes use KO order; a missing/tied order is a player loss. Non-boss mutual wipes remain draws. | correctness | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| POOL-COMMON | Every authored species pool contains 4–5 moves, includes a Common damaging move, and retains every InitialMove; Balloom opens with Gas Bump and no longer rolls All-In. | balance/data | implemented, unverified | none | [Baseline and verification](Audits/followups_v1_baseline_tests.md) |
| SUPA-AP | Supabase AP, boss-only flags, Known pool, and event grant tables are generated from the imported Unity data/manifest instead of hand-copied values. Both shared-table suites pass 36/36 against manifest SHA-256 `92D83D7D37CA6614ACF4170869B645BB9B94F57F1C30AC5FDF563E4C41CB6F02`. | server parity | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| BOSS-ONLY | `BOSS_ONLY` moves are excluded from pools, progression, candy/Known rolls, captures, PvP parties, and the ownership-card catalog. | correctness | implemented, unverified | none | [Baseline and verification](Audits/followups_v1_baseline_tests.md) |
| GRIND-TAX | Cofi's authored encounter kit includes the 6 AP, boss-only Grind Tax: three team hits and Speed -1 once per foe per cast, with payoff AI priority and Charging/Ready tells. | boss balance | implemented, unverified | none | [CSV/asset parity](Audits/moves_csv_asset_parity.md) |
| SRV-PARITY | Gameplay manifest v3 exports move resolve/accuracy/effect flags, the reconciliation opcodes, and every `StatusId`; `ManifestParityTests`, the .NET build/verification tool, and both 36/36 Deno suites pass. | server parity | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |

## Correctness still open

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-03 | Slowed's compact label matches the Speed −2-only body and does not promise an action skip (`SlowedLabel_DoesNotPromiseAnActionSkip`). | correctness | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-04 | Drip heals 6% MaxHP on every end step for 3 ticks, including the granting turn. Spec §7.6 still says "once". | correctness | implemented, unverified | data | [E-04](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-04--drip-once-versus-every-end-of-turn) |
| E-05 | Cascade Failure's code comment says KO. The third apply deals 25% MaxHP and can KO only if HP hits 0. | correctness | implemented, unverified | data | [E-05](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-05--cascade-failure-comment-says-ko) |
| E-06 | FUD's comment now matches its Atk/Def/Spd −1 payload. | correctness | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-20 | Streamlined Flow has one conditional Def −1 write plus its AP discount above 70% HP. | correctness | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-21 | Compression Burst applies move effects once per target, so its two hits lower Def once. | correctness | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-23 | Five empty stamp modifiers were retired. Setup Feed (`7.2-c`) is authored and remains unverified. | correctness | partial | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| EOT-ORDER | End-of-turn passives resolve before status durations tick; Kindled remains first inside the status pass. | correctness | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-24 | Tripwire applies owned Slowed for one turn, refreshes without stacking, and unrelated Frozen assets do not trigger it (`Tripwire_AppliesSlowedForOneTurnAndRefreshDoesNotStack`, `Tripwire_RefreshDoesNotStackSpeed`, `Tripwire_FrozenAssetsAndOtherSectionsDoNotSlow`). | correctness | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-25 | Charged Relay reads team-wide distinct hits, so an ally's spread can satisfy its two-target payoff. | correctness | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-28 | A passive status with a non-self `targetOverride` still applies to the owner. No live passive authors that override. | correctness | awaiting decision | engine | [E-28](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-28--passive-status-target-override-is-ignored) |

## Empty payloads and half cards

E-18: the stage column cannot name Accuracy or Evasion. `ChangeStage` already accepts both, so an Implement of a plain Acc/Eva stage is data. E-19: blank `StatusEffects`, `StageChanges`, and `UtilityEffects` are copied through as empty lists. The asset matches the CSV. The payload was never authored. Each row is still change, remove, or implement.

### Accuracy and Evasion only (E-18)

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-accuracy_focus | Common, 1 AP. Tooltip is own Acc +2. Spends AP and applies nothing. | empty payload | implemented, unverified | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-evasion_maneuver | Common, 1 AP. Tooltip is own Eva +2. Empty payload. | empty payload | implemented, unverified | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-accuracy_disrupt | Common, 1 AP. Tooltip is target Acc −1. Empty payload. | empty payload | implemented, unverified | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-evasion_pin | Common, 1 AP. Tooltip is target Eva −1. Empty payload. | empty payload | implemented, unverified | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |
| MV-gas_cloud | Epic, 2 AP, AllEnemies. Acc −1 and Eva −1 payload is implemented. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-dust_fees | Epic, 1 AP. Target Acc −1 and Eva −1 payload is implemented. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-optimize_aim | Common, 1 AP. Tooltip is Acc +2. Empty payload. | empty payload | implemented, unverified | data | [Empty Acc/Eva](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#empty-accuracy-and-evasion-moves) |

### Half the card (E-18 riders)

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-chain_seal | Speed −1 is real. The Eva −1 sentence does not run. | unimplemented move | implemented, unverified | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-key_rotation | Def +1 is real. Eva +1 does not run. | unimplemented move | implemented, unverified | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-zip_step | Only Def −1 runs, so the card's downside is the whole move. Eva +2 does not. | unimplemented move | implemented, unverified | data | [Half card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#accuracy-or-evasion-is-only-half-of-the-card) |
| MV-pivot_line | Epic, 1 AP. Acc +1, Eva +1, and Speed +1 are implemented. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-precision_strike | Power 34, accuracy 100, 2 AP. Reusable `SureHit` tuning ignores Evasion and enforces a 95% hit floor (`SureHitTuningIgnoresEvasionAndKeepsNinetyFivePercentFloor`). | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-shard_scatter | Power 18, 2–3 hits, AllEnemies. The unsupported per-hit Eva sentence was removed; the approved 4 AP decision is tested. | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |

### Whole card does not run (E-19)

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-ap_drain | Common, 2 AP, power 16. Drains 1 AP and the 25% self gain only occurs on a landed hit (`ApDrain_HitsForSixteenCutsOneAndGainsOnlyOnTheHit`). | empty payload | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-transaction_tax | Epic, 1 AP. Steals 1 AP from the target. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-extend_protocol | Extends the shortest eligible ally buff without paying Uplink; logs when no buff qualifies. Recharge 2 means one skipped turn (`ExtendProtocol_ExtendsTheShortestBuffAndDoesNotPayUplink`, `ExtendProtocol_LogsWhenNothingQualifies`). | empty payload | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-blockade | Common, 3 AP. Grants 25% mitigation for two turns with unlimited hits. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-script_loop | Common, 1 AP. Does not extend debuffs when the target has 2 or more. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-network_sync | Common, 2 AP, AllAllies. `RaiseLowestStage` raises each ally's lowest stage with deterministic tie and +6-cap behavior (`RaiseLowestStage_RespectsTiesAndCap`, `Uplink_PaysEachChangedAllyOnceAndSkipsExtendAndCap`). | empty payload | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-system_reset | Common, 3 AP. Strips every General buff/debuff from one foe and uses recharge 2; live in Keyper. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-status_resistance | Retired. No effect; no pool references. | empty payload | deprecated | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-isolation_tool | Common, 1 AP. Applies Anchored for one turn through the chemistry-status opcode. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-viral_surge | Common, 1 AP. No AP coin flip and no random stage. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-wildcard_swap | Common, 1 AP. Does not swap a stage with the target. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |
| MV-hype_burst | Common, 2 AP. Strips up to two foe buffs and grants Atk +1 per buff removed; live in Cinislug. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-crystal_extraction | Common, 2 AP. Heals the user 5% MaxHP per General debuff on the foe, then cleanses the foe; live in Kodiax. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-purge_protocol | Common, 2 AP. Cleanses all General debuffs from one ally and grants Def +1; ungranted. | empty payload | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-extraction_hit | Passive. Hits do not raise Atk, and Atk +3 does not also grant Speed. | empty payload | awaiting decision | engine | [Whole card](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#whole-card-does-not-run) |

## Hits whose rider or cost does not run

The hit itself resolves. The extra sentence does not. Missing costs on `all_in`, `recoil_strike`, and `volatile_shot` are also BAL-COST. Do not retune other powers off these indices.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| MV-all_in | Power 98, minimum 4 AP. `SpendRemainingAp` spends the whole bank and recharge text states the one-turn lock; recoil and KO refund are shelved (`AllIn_SpendsRemainingApAndSkipsTheNextTurn`). | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-recoil_strike | Renamed Hard Fork: 72-power contact hit, then cleanse one General debuff from self. HP recoil is shelved. | unimplemented move | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| MV-volatile_shot | Renamed Gas War: 72-power ranged hit, then the user's next move costs +1 AP. HP loss is shelved. | unimplemented move | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| MV-jackpot_shot | Power 64, AP 2. Seeded `RandomDamageMultipliers` runs the double / normal / half branch. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-hype_train | Text cut to its implemented 52-power ranged hit. Riderless Legendary remains tracked in `rarity_rivals.md`. | unimplemented move | fixed in PR | none | [Rarity rivals](Audits/rarity_rivals.md) |
| MV-lucky_scatter | Rare, power 18, 1–4 hits. Its 25% per-hit `RandomStageChange` is implemented. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-crystal_bash | Epic, Power 44, Def-scaled, ×2 below 30% target HP; ungranted. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-glitch_barrage | Renamed Bit Rot: Mythic, power 18, 2–4 hits; each hit has a 25% chance to lower one random foe stat. The self-hit concept is shelved. | unimplemented move | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| MV-cipher_shot | Uncommon, power 40, AP 2. The first landed hit per target applies guaranteed Cipher Lock for 2 turns (`CipherLockBlocksNewBuffsAndPositiveStagesWithoutRemovingExistingBuffs`, plus the imported-row parity test). | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-cipher_burst | Text cut to its implemented 2–3-hit body; rarity comparison stays in `rarity_rivals.md`. | unimplemented move | fixed in PR | none | [Rarity rivals](Audits/rarity_rivals.md) |
| MV-ap_siphon | Power 56, AP 3. The 50% drain of 1 AP does not run. `DrainAP` exists. | unimplemented move | implemented, unverified | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-deep_dig | Text cut to its implemented 72-power contact hit. | unimplemented move | fixed in PR | none | [Rarity rivals](Audits/rarity_rivals.md) |
| MV-momentum_drill | Power 64, AP 2. No +25% versus a debuffed target. `DebuffCountAtLeast` already gates `bug_payoff`. | unimplemented move | implemented, unverified | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-ore_strike | Text cut to its implemented contact hit. | unimplemented move | fixed in PR | none | [Rarity rivals](Audits/rarity_rivals.md) |
| MV-stake_strike | Text cut to its implemented contact hit. | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-yield_spike | Text cut to its implemented strong contact hit; tags are now only `DMG`. | unimplemented move | fixed in PR | none | [Rarity rivals](Audits/rarity_rivals.md) |
| MV-interest_strike | Rare, power 18, 2–3 hits, index 42.75. No AP grant when Def stage exceeds Atk. | unimplemented move | implemented, unverified | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-rallying_slam | Rare, power 64, AP 2. No +1 AP when Def stage exceeds Atk. | unimplemented move | implemented, unverified | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-pickaxe_flurry | Power 16, 2–4 hits. Hits do not raise Atk, and there is no +2 cap. | unimplemented move | implemented, unverified | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-combustive_shift | Text cut to its implemented two-hit body. Riderless Legendary remains tracked in `rarity_rivals.md`. | unimplemented move | fixed in PR | none | [Rarity rivals](Audits/rarity_rivals.md) |
| MV-crash_routine | Text cut to its implemented body: lower target Atk by 1. | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-glitch_feedback | The +1 AP surcharge runs. The cleanse of 1 debuff does not. `Cleanse` exists. | unimplemented move | implemented, unverified | data | [Riders](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#a-real-hit-with-a-cost-or-rider-that-does-not-run) |
| MV-bug_fix | Heal 10%, cleanse one General debuff, then apply self Staggered for the next damaging move. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-dice_roll | Text cut to its implemented body: heal an ally for 20% MaxHP. | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-recursive_backup | Text cut to its implemented body: below 50% HP, heal 20% now. | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-data_scramble | Costs 1 AP; self Atk −1, self Speed −1, and +2 AP run. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-risk_spike | Renamed Leverage Up: Atk +2, Def −1, and Surging grants +25% power to the next hit. HP recoil is shelved. | unimplemented move | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| MV-guard_stance | Def +2 runs. The unimplemented contact-recoil sentence was removed while self-damage remains shelved. | unimplemented move | implemented, unverified | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |
| MV-rally_cry | Allies gain Speed +1; the user gains one Yield end-step AP tick. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-refinery | Def +1 remains; the user gains Yield for two end steps. | unimplemented move | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| MV-double_or_nothing | Text cut to the implemented passive: end turn 60% +1 AP, 40% −1 AP. | unimplemented move | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |

## Status bodies that do not match

The move or the chemistry cell applies the status. The extra sentence on the status does not run. Shared bodies are one row, not one row per move.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-07 | Jammed applies an owned Accuracy −1 for two turns; refresh does not stack and expiry reverts it. | status body | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-08 | Leaked taxes only the acting Chainling on that side for 10% MaxHP and does not tick the bench (`Leaked_TaxesOnlyActorsOnThatSide`). | status body | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-09 | Backlash text now matches its token-only body: no contact damage. | status body | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-10 | Dusted owns clamped Evasion −3 and Speed −3 deltas, does not stack them on refresh, and reverts both at expiry (`Dusted_OwnsClampedEvasionAndDoesNotStackOnRefresh`). | status body | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-12 | MEV Extracted text now says drain 1 AP per turn, matching the body. | status body | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| E-13 | `deflation_mark` applies Burned Tokens and a permanent Atk −1. Enemy DoT does not heal the user. The status has no resolver body. | status body | awaiting decision | engine | [Burned Tokens](Audits/UNIMPLEMENTED_MOVE_EFFECTS.md#status-text-the-move-applies-and-the-status-body-does-not-match) |
| E-14 | Superseded by SD-SHELF. Overleveraged is shelved and status id 208 remains reserved. | status body | superseded | none | [Self-damage shelf](Design/SELF_DAMAGE_SHELF.md) |

## Statuses with no producer

E-27. Rekt, Offline, and Relay are explicitly deprecated; seven unused catalog-only definitions were removed while their enum ids remain reserved. The remaining awaiting-decision entries still need a producer or an explicit retirement call. Deprecated chemistry ids in `COMBAT_Chemistry.txt` §7 are a different list. HODLed's double ramp is E-11 in this table.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| NP-rekt | Retained enum/catalog id; display row is deprecated and has no producer. | no producer | deprecated | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-offline | Retained enum/catalog id; display row is deprecated and has no producer. | no producer | deprecated | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-glitchedmind | Mis-act at 50% is implemented. The Glitched profile still names it. No move. | no producer | awaiting decision | data | [GlitchedMind](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#glitchedmind) |
| E-11 | No move and no v1 cell applies HODLed. No stat-drop immunity. Repeating Def +1 plus an extra +1 while Def is below +3, so the first end tick can reach +2. Both writes revert if it expires. No new body this round (2026-10-19). | design decision | awaiting decision | both | [E-11](Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md#e-11--hodled-ramps-twice-and-does-not-stop-at-3), [HODLed](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#hodled) |
| NP-duststorm | Removed from the live status catalog; the enum id remains reserved. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-forkedchain | Removed from the live status catalog; the enum id remains reserved. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-compresseddata | Removed from the live status catalog; the enum id remains reserved. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-memesurge | Removed from the live status catalog; the enum id remains reserved. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-noderelay | Removed from the live status catalog; the enum id remains reserved. This remains distinct from passive `uplink`. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-unsealed | Resolver consumes it for a 50% barrier bypass. Spec §10.1 says support moves apply it. No move does. | no producer | awaiting decision | data | [Unsealed](Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md#unsealed) |
| NP-relay-status | Retained enum/catalog id; display row is deprecated and has no producer. This is not passive `uplink`. | no producer | deprecated | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-bridgedout | Removed from the live status catalog; the enum id remains reserved. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| NP-reinforced | Removed from the live status catalog; the enum id remains reserved. | no producer | fixed in PR | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |

## Balance flags

Value model and the historical out-of-order tables are in the balance audit. The AP ladder and this reconciliation supersede the broad rarity flags; remaining rivals are tracked in `rarity_rivals.md`.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| BAL-RARE | Superseded by the AP ladder and the written typeless/typed band rule. | balance | superseded | none | [Rarity rivals](Audits/rarity_rivals.md) |
| BAL-SPREAD | Superseded by the AP ladder and current rarity-rival review. | balance | superseded | none | [Rarity rivals](Audits/rarity_rivals.md) |
| BAL-EPIC | `bulwark_blow` is the only Epic damage move (index 45.00 at 3 AP, attacks with MeleeDef). The Def column is real. On an attacker the Atk index stays under several AP-3 Commons. Leave it until a retune. | balance | awaiting decision | data | [Strictly worse](Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md#strictly-worse-as-shipped) |
| BAL-COST | Cost ladder retuned case by case. Typed Commons now occupy the 30–40 power spine; Hard Fork, Gas War, All-In, Fork Split, Shard Scatter, Hammer Sync, and Coordinated Shot have explicit impact-based costs. | balance | implemented, unverified | none | [AP ladder follow-up](Audits/AP_COST_LADDER_IMPLEMENTATION_AUDIT.md#8-decisions--follow-ups-2026-10-18) |
| BAL-EMPTY | Partial: Hype Train remains riderless; the other reviewed cards are implemented, retired, or tracked in `rarity_rivals.md`. | balance | partial | none | [Rarity rivals](Audits/rarity_rivals.md) |
| BAL-61 | Spec §6.1 now states that rarity tracks power and availability, typed moves beat typeless, and Rare typeless aligns with typed Common. | balance | fixed in PR | none | [COMBAT_Moves.txt](../Specs/Combat/COMBAT_Moves.txt) |

## Open design decisions

Proposals only, except where a row above is already the defect. Do not ship a shortlist item, a design-idea card, or a Compressed / Bot concept from this table.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| E-17 | Ash Guard, Compound Guard, Ice Wall, and Blockade use unlimited hit budget −1 for their authored turn windows. | design decision | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| DEC-BEAM | Resolved by keeping the longer Cofi fight and adding boss-only Grind Tax at 6 AP instead of a stronger beam. Bean Sip supplies +1 AP; boss AI prioritizes the payoff when legal and emits Charging/Ready tells. | design decision | implemented, unverified | none | [GRIND-TAX](#combat-balance-follow-up-v1) |
| DEC-S1 | Shortlist 1, not approved. Data-only opcode pack: Fee Tap, Cipher Skim, Relay Cut, Brace Yield, Mempool Mark, Aim Lock. | design decision | awaiting decision | data | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S2 | Shortlist 2, not approved. Utility AI subtracts for recharge and can bonus an AP threshold. This does not add the 6 AP beam. | design decision | awaiting decision | engine | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S3 | Shortlist 3, not approved. Two 1 AP chips, Gas Vent and Anchor Tap. Rapid Strike's number stays until the cost rows are decided. | design decision | awaiting decision | data | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S4 | Shortlist 4, not approved. One above-band utility priced with `rechargeTurns` 2. Depends on DEC-S2. Does not change Fox Feint's 999. | design decision | awaiting decision | data | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-S5 | Shortlist 5, not approved. A 1 AP toll on a voluntary swap. KO swaps and forced swaps stay free. | design decision | awaiting decision | engine | [Shortlist](Design/COMBAT_DESIGN_IDEAS.md#5-shortlist) |
| DEC-PACK | The other design-idea moves (20) and mechanics M1–M6 are not approved. `node_beacon` and `cinder_idle` in that list are withdrawn. Finality and Reorg stay held back. | design decision | awaiting decision | both | [New moves](Design/COMBAT_DESIGN_IDEAS.md#2-new-move-ideas), [Mechanics](Design/COMBAT_DESIGN_IDEAS.md#3-new-mechanics) |
| DEC-CD | Compressed C1–C7 and Bot D1–D7 are not approved. They were drafted before the typed-pool rule and were not re-cut. | design decision | awaiting decision | both | [Recommended picks](Design/TYPE_GAP_PROPOSALS.md#recommended-picks) |
| DEC-AI | Systems proposals A–E are not approved: write the target slot, a Utility lethal check, defense and chemistry in `EstimateDamage`, recharge and hurt-ally weights, and Defensive personality on walls. | design decision | awaiting decision | engine | [AI proposals](Design/COMBAT_SYSTEMS_ANALYSIS.md#proposals) |
| DEC-CHEM | Superseded: ×1.2 / ×0.8 stays; Jammed's Accuracy −1 body is implemented. Meme stays out. | design decision | superseded | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| DEC-PACE | The longer Cofi fight was intentionally kept; Grind Tax is its 6 AP pressure payoff. Other pacing proposals remain unchosen. | design decision | implemented, unverified | none | [Pacing](Design/COMBAT_SYSTEMS_ANALYSIS.md#do-regen-recharge-and-healing-drag-or-snowball) |
| DEC-STALE | The species-pool inventory's `GapType none` on empty payloads is stale, and Validape's design note still calls Extend Protocol an exclusive. The empty-payload investigation is the current finding. | design decision | implemented, unverified | data | [Other sources](Audits/EMPTY_PAYLOAD_INVESTIGATION.md#other-sources) |

## Approved proposals

Uplink and Kindling are in the resolver and the pools. Their rows stay here until a manual battle smoke test. Everything else in the design docs is a proposal or a withdrawal.

| ID | Summary | Category | Status | Work | Source |
|---|---|---|---|---|---|
| APR-RELAY | Uncommon Node passive `uplink`. When this Chainling applies a buff to an ally, that ally gains +1 AP once per ally per turn. It is granted on Validape in place of `ap_router`; it is not Group Hug. | approved proposal | implemented, unverified | none | [Reconciliation tests](Audits/audit_reconciliation_v1_tests.md) |
| APR-KIND | Uncommon Burned passive `kindling`, approved at 6% MaxHP. A damaging hit that leaves a living foe applies Kindled for one end step. Keep the stack with Rugged. The optional shared-end-step trim was not adopted. Sear Echo was not chosen. | approved proposal | implemented, unverified | both | [Kindling](Design/TYPE_GAP_PROPOSALS.md#kindling-recommended), [Approval](Design/TYPE_GAP_PROPOSALS.md#guardrails-and-the-recommendation) |
