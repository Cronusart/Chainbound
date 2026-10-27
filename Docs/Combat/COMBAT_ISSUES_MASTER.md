# Combat issues master

Date: 2026-10-19. Index of the combat audit and design notes on this branch. Detail stays in the linked section; implemented rows remain unverified until the manual battle smoke is recorded. Reconciliation plan: `C:\Users\mando\.cursor\plans\audit_reconciliation_v1.plan.md`; automated evidence: [audit_reconciliation_v1_tests.md](Audits/audit_reconciliation_v1_tests.md).

> **2026-10-22 update:** see [Moves with no working effect: implement or rewrite (2026-10-22)](#moves-with-no-working-effect-implement-or-rewrite-2026-10-22) at the end of this page. It holds the decided All-In spec, PROPOSAL verdicts for every move with no working effect, and the stale rows to refresh. Checker: `Docs/Combat/no_effect_moves_check.py`.

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

## Moves with no working effect: implement or rewrite (2026-10-22)

Snapshot: local tree, 2026-10-22 ~09:30 CDT. Every move fact below (rarity, type, AP, power, payload, pool membership, species tier) is asserted by `Docs/Combat/no_effect_moves_check.py`, which reads `Docs/Specs/Combat/Data/Moves.csv`, `SpeciesMovePools.csv`, `Statuses.csv`, the species assets, and greps `Assets/Scripts/Combat/Runtime` for id literals. Run it from the repo root with `python Docs/Combat/no_effect_moves_check.py`.

**Status.** All-In is **decided** (Cronus, 2026-10-22) and is written as an implementation spec. Every other recommendation in this section is a **PROPOSAL**. It is not approved and nothing here is implemented. None of the rows above were changed. They are listed for refresh in [Stale master items (to refresh)](#stale-master-items-to-refresh).

**Hard constraints used.**
- Learn pools follow `C:\Users\mando\.cursor\plans\learn_pool_rarity_6d7ff36f.plan.md`:
  - A wild pool is five moves: three at the body tier or lower, one at that tier or one step higher, and a signature that is strictly the highest rarity.
  - Starters only need a unique top-rarity signature. Ghost and Cofi Lootwak are outside the rule.
  - Moves stay inside the type profile and species role. The plan uses existing moves only.
- No self-damage. Typed moves beat typeless. Build out empty moves rather than pulling them.
- Rarity tracks power and availability (BAL-61). A lower-rarity move must not rival a higher-rarity one.
- Type identities are from `Docs/Specs/Combat/COMBAT_TypeProfiles.md` (summary table).

### Sweep: what does nothing or only part of its card

Method:
- A non-damaging row counts as blank when `StatusEffects`, `StageChanges`, `UtilityEffects` and `HealPercentMaxHP` are all empty.
- It also needs no `"move_id"` string literal anywhere under `Assets/Scripts/Combat/Runtime`. Passives and the 2v2 supports are id-driven, so the literal is how they run.
- Damaging rows with no payload were read by hand.

| Result | Moves |
|---|---|
| Card does nothing | `script_loop`, `viral_surge`, `wildcard_swap`, `extraction_hit` (passive) |
| Already retired, no action | `status_resistance` (MV-status_resistance, deprecated; text says "Retired. No effect.") |
| Half the card runs | `deflation_mark`. Atk −1 runs. `BurnedTokens` has no body: its `Statuses.csv` row has no stage effects, 0 end-of-turn damage and AP, and is neither buff nor debuff. `StatusId.BurnedTokens` appears nowhere in Runtime code. |
| Works today, in scope by request | `all_in` (decided change below), `recoil_strike` / Hard Fork |
| Blank payload but the card is a plain hit, so it works | `quick_lunge` (PRIOR), `chain_fork`, `shard_scatter`, `hype_train`, `combustive_shift` and other riderless hits |

Also seen, not in scope here:
- `hype_train` and `combustive_shift` are riderless Legendaries (BAL-EMPTY, `rarity_rivals.md`).
- `hype_train` and `ore_strike` carry a `TRIG` tag with no trigger.

**Meme reaches no one.** Four of the seven moves below are Meme. No Meme move is in any learn pool. The only Meme species is Ghost, which is outside the pool rule and has no `SpeciesMovePools.csv` row. Meme is also outside the chemistry pass (`COMBAT_TypeProfiles.md`). Under the "stay inside the type profile" rule, every Meme verdict below only matters once a Meme body exists, or a rule exception is granted. That is an open question, not a proposal.

### Verdict table

| Move (id) | Rarity · type · AP · power | Learned by | Verdict | Proposed effect (one line) | Engine cost |
|---|---|---|---|---|---|
| All-In (`all_in`) | Epic · Meme · 4 (min) · 98 · recharge 2 | No pool | **DECIDED: change** | On cast, drain all AP from the caster and its active ally; enemy AP untouched | Small (one branch) |
| Script Loop (`script_loop`) | Common · Bot · 1 · 0 | No pool | PROPOSAL: implement, trimmed | If the target has 2+ General debuffs, add 1 turn to its shortest General debuff; RechargeTurns 2 (1-turn recharge) | Small (new opcode) |
| Viral Surge (`viral_surge`) | Mythic · Meme · 1 · 0 | No pool | PROPOSAL: rewrite | Gain Surging (next hit +25%) and +1 to a random stat; 50% chance to gain +2 AP | None (data) |
| Wildcard Swap (`wildcard_swap`) | Common · Meme · 2 · 0 | No pool | PROPOSAL: rewrite | Lower one random foe stat by 1 and raise one random own stat by 1 | None (data) |
| Extraction Hit (`extraction_hit`) | Rare · Mined · passive | No pool (left Kodiax in the pool plan) | PROPOSAL: implement as written | First landed damaging hit each turn: Atk +1, passive cap +3; the first time Atk reaches +3, Speed +1 | Small (passive-by-id pattern) |
| Deflation Mark (`deflation_mark`) | Rare · Burned · 1 · 0 | No pool (left Cinislug in the pool plan) | PROPOSAL: implement as written | Burned Tokens body: end-of-turn DoT on the marked foe heals the marker for 50% of the tick; keep Atk −1 | New hook |
| Hard Fork (`recoil_strike`) | Uncommon · Forked · 4 · 72 | Balloom, off-type only | PROPOSAL: keep as is | No recoil. The Forked profile asks for cleanup "without recoil stacking" | None |

### All-In: DECIDED, implementation spec (Codex)

> **Implemented 2026-10-25.** `DrainApEffect` has the `AllAllies` branch, the `all_in` row carries `DrainAP:10:100%:AllAllies:all_in` with `ResolveBeforeFirstHit`, and the asset, gameplay manifest, and server move tables are regenerated (CSV/asset parity: 0 diffs). Tests: four `AllIn_*` 2v2 tests in `ApCostLadderTests.cs` (slower ally fails for no AP and foes keep AP; faster and PRIOR allies resolve; a 0-AP ally move resolves; a miss still wipes). The optional AI penalty is not done.
>
> **Rarity and Candy (decided 2026-10-26, Cronus, later the same day).** All-In is now **Mythic** (it's the highest-damage move in the pool). It joins the rarity-weighted Candy Machine Known pool under the rule in [KNOWN_RARITY_WEIGHTS_v1.md](../Specs/Economy/KNOWN_RARITY_WEIGHTS_v1.md). It still stays out of species pools. This supersedes the Candy part of the note below.
>
> **Availability (decided 2026-10-26, Cronus).** All-In stays out of every species pool until a Meme species exists, and out of the Candy Machine Known pool (`VendingConfigSO.knownPoolMoveIds`, today ten typeless baselines rolled 1-in-10) until rarity-weighted Known rolls are designed. Until then no player can obtain it.

**Decision.** On cast, All-In drains **all** AP from the caster **and** its active ally. Enemy AP is untouched. This is the full wipe, not the 2 AP partial.

**Today.**
- `Moves.csv` row `all_in`: Epic, Meme, DamagingMelee, power 98, APCost 4, RechargeTurns 2.
- Tags `DMG|APMAN|SpendRemainingAp`. `UtilityEffects` and `ResolveUtilityTiming` are blank.
- `CombatResolver.cs:1740` pays the cost. Lines 1757–1761 then spend the remainder, so the caster is already at 0.
- RechargeTurns 2 is the one-turn lock. Test: `ApCostLadderTests.AllIn_SpendsRemainingApAndSkipsTheNextTurn` (`Assets/Scripts/Combat/Tests/ApCostLadderTests.cs:133`).

**Why it cancels a slower ally's move.** AP is checked again and paid when an action executes, not when it is queued.
- `ResolveMove` calls `EvaluateMoveSelection` (`CombatResolver.cs:1636`).
- That returns `InsufficientAP` when cost > current AP (`Assets/Scripts/Combat/Runtime/Engine/CombatActionEligibilityService.cs:58-60`).
- The resolver then logs `LogNoApActionFailed` (`CombatResolver.cs:1641`, body at `:1907`), sets `hasActedThisTurn`, and returns before paying (`:1740`).

So a slower ally at 0 AP fails **any** move that costs 1 AP or more. Only 0-AP moves survive: `ap_boost`, `fuel_surge`, `crystal_focus`, `ap_reserve`. AP Reserve then banks 0. Swaps resolve earlier in the turn, so they are unaffected.

**Engine change.** In `Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs`, `DrainApEffect` (lines 1041–1078), add a branch after the `AllEnemies` block (1053–1073) and before `ResolveUtilityTarget` (1075):

```csharp
if (effect != null && effect.targetOverride == MoveTargeting.AllAllies)
{
    TeamState allies = context?.sourceTeam;
    if (allies == null)
        return;
    List<ChainlingInstance> actives = allies.GetActiveChainlings();
    for (int i = 0; i < actives.Count; i++)
    {
        ChainlingInstance ally = actives[i];
        if (ally == null || !ally.isAlive)
            continue;
        int drained = _apService.Drain(ally, amount, turn, move.definitionId);
        LogDrain(source, ally, move, drained, turn);
    }
    return;
}
```

- **Return early, or the drain hits the enemy.** `ResolveUtilityTarget` (1214–1232) has no ally case and falls back to the move's target, which is the enemy. Authoring `DrainAP:…:AllAllies` without this branch would wipe the foe.
- **The caster is included on purpose.** It is already 0 after `SpendRemainingAp`, so the drain is a no-op that keeps the log honest.
- `context.sourceTeam` is filled by `ApplyDamagingMoveUtilityEffects` through `CreateUtilityContext` (`CombatResolver.cs:3593-3606`).
- `Drain` floors at 0 (`Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs:101-115`). The amount 10 equals `apCap: 10` in `Assets/Data/Combat/Config/CombatResolverConfig_Default.asset`, so it wipes any bank.
- **Timing.** `ResolveBeforeFirstHit` runs at `CombatResolver.cs:2103-2104`, before the hit-chance roll at 2112, so the wipe fires even on a miss. Bounce targets are excluded at 2084–2088. All-In is single-target, so the effect runs once.
- No new effect id. `DrainAP` and `MoveTargeting.AllAllies` both exist. The importer already parses `AllAllies` for `DrainAP` (`Assets/Scripts/Combat/Editor/Importers/Combat2DataImporter.cs:1744-1756`).

**Data change.** Edit the `all_in` row in `Docs/Specs/Combat/Data/Moves.csv`:
- `UtilityEffects` = `DrainAP:10:100%:AllAllies:all_in`
- `ResolveUtilityTiming` = `ResolveBeforeFirstHit`
- `Description` = `Spends all your AP (min 4). On cast, your ally's AP also drops to 0. Can't use All-In again next turn.`
- Power, AP, recharge, rarity, type and tags stay as they are. `move_ap.ts` is unchanged at 4.

**Then:**
1. Run the Combat Data Importer.
2. Run the CSV/asset parity audit (`Assets/Scripts/Combat/Editor/Importers/CombatMovesCsvParityAuditor.cs` → `Docs/Combat/Audits/moves_csv_asset_parity.md`; expect 0 field diffs).
3. Re-export the gameplay manifest. `AuthoritativeGameplayManifestExporter.cs:373` writes the utility string.
4. Run `ManifestParityTests` (`Assets/Scripts/Editor/Tests/Combat/ManifestParityTests.cs`) and the Deno `move_ap_parity_test.ts`.

**Optional AI penalty.**
- `Assets/Scripts/Combat/Runtime/AI/UtilityStatusAI.cs` already prices `SpendRemainingAp` at the caster's current AP (`ResolveScoringCost`, line 154; used at 84).
- Add a score penalty for living active allies' `currentAP`, and a larger one when `queuedBySlot` (lines 23–62) holds an ally action that costs 1+ AP and that ally is slower.
- Leave the other AIs alone. Low priority while All-In is in no pool.

**Tests (6).** Add these to `ApCostLadderTests.cs`, next to the existing All-In tests at :133, :530 and :690.
1. A slower ally's queued 1+ AP move fails with the no-AP failure after All-In. The ally ends at 0 AP and is marked acted.
2. A faster ally in the normal block, and a PRIOR ally, both resolve normally. Only their leftover AP is wiped.
3. A slower ally's 0-AP move (`ap_boost`) still resolves after the wipe.
4. A forced miss on All-In still wipes the ally's AP (`ResolveBeforeFirstHit`).
5. Both foes' AP is unchanged.
6. With no ally (1v1), behavior is unchanged and the existing `AllIn_SpendsRemainingApAndSkipsTheNextTurn` still passes. The imported `all_in` asset carries `DrainAP`, `AllAllies`, amount 10, timing `ResolveBeforeFirstHit`.

**Interactions.**
- **Turn order** is swaps, then the PRIOR block, then normal, then capture (`CombatResolver.cs:984-1005`).
  - Ally PRIOR moves resolve before All-In and keep their effect. That covers the whole wall/barrier family, Rapid Strike, Blink Lunge, Bean Sip and Position Swap.
  - A PRIOR wall or barrier on the All-In user is the natural pairing.
- **A faster ally** has already paid and only loses its leftover bank.
- **Swaps.** A voluntary swap copies the outgoing AP to the incoming Chainling, and swaps resolve first. The incoming active is what gets wiped. Benched Chainlings are untouched.
- **Next turn.** Regen is +2 (`StatusProcessor.cs:457`), so the ally restarts at 2 and any saved bank is lost. AP gained after the wipe in the same turn still lands, for example Group Hug or Uplink.
- **Not a lock.** It only touches your own team, and RechargeTurns 2 blocks a back-to-back cast. It is an AP drawback, not HP, so it complies with SD-SHELF.
- **1v1** is unchanged. The drawback only exists in 2v2.
- **Rarity flag.** Epic All-In at 98 power is above Legendary Hammer Sync at 80 (4 AP, ×1.2 with a buffed partner, so 96). The whole-bank spend, the ally wipe and the recharge are the price. Keep that in mind in the next rarity-rivals review.
- **Narration.** The ally's failure uses the generic no-AP message. The `LogDrain` line is the only All-In attribution. A clearer line is optional.
- **Open question: pool.** `all_in` is in no species pool. It is referenced only by `MoveDatabase.asset`, so the change reaches no one until it is pooled. Under the pool plan it could only join a Meme body (see "Meme reaches no one" above). No placement is proposed here.

### Script Loop: PROPOSAL, implement (trimmed)

- **Card.** "If target has 2+ debuffs extend all by 1 turn." Common, Bot, StatusUtility, 1 AP, SingleEnemy. Payload is blank.
- **Bot profile.** Automated pressure: AP denial, multi-hit attrition. Keywords: automation, AP denial, multi-thread, attrition. Statuses: BotSwarm, MEV Extracted, ThreadLock.
- **Bot pool today:**
  - Debuff Script (Common, 1 AP, Atk −1 and Def −1 as stages)
  - Bot Swarm (Epic, 2 AP, BotSwarm)
  - MEV Extract (Rare, 2 AP, MEVExtracted)
  - Automated Pressure (Rare passive, random foe stat −1 each end of turn)
  - Multi-Thread Shot (Uncommon, 2 AP, power 12 × 2–3, 10% +1 AP)
  - Automation Burst (Uncommon, 4 AP, power 38 AOE, 25% random stat)
  - Recursive Backup (Common, heal 20% below 50% HP)
  - Thread Lock (Legendary, 2 AP)
  - Self-Repair Protocol (Uncommon, heal 15%)
  - Blade Flurry (Common, power 12 × 3)
  - Fox Feint (Epic, dodge and counter once)
  - Packet Skim (Common, 1 AP, drain 1)
- **What's missing.** Bot applies debuffs but has nothing that amplifies or extends them. Script Loop would be distinct. It is the only debuff extender in the game; Node's Extend Protocol extends ally buffs.
- **Learned by.** No pool. The only Bot body is Kitsauto, a starter with a full five-move pool and Fox Feint (Epic) as its unique top. A Common is legal there as a non-signature slot, but placement is not proposed.
- **Rarity conflict as written.** Extending **all** debuffs at Common, recharge 0, would beat Epic Extend Protocol, which adds 1 turn to one ally buff at 1 AP with recharge 2. Trim it to one debuff.
- **Proposed effect.** Gate on `ResolveConditions` = `Target:DebuffCountAtLeast:2:None:MeleeAtk:MeleeAtk` (the same predicate as `bug_payoff` / `momentum_drill`). Add 1 turn to the target's shortest General **debuff**. `RechargeTurns` 2.
  - Common gated ≤ Epic ungated, so no conflict.
  - It only counts and extends debuff *statuses*. Confirm in a test that stage-only debuffs (Debuff Script) are not counted.
- **Engine (small):**
  1. Append `ExtendDebuff` at the **end** of `UtilityEffectType` (`Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs:263-319`, after `BlockReflect`) so serialized ints stay stable.
  2. Add `StatusProcessor.TryExtendShortestDebuff`, mirroring `TryExtendShortestBuff` (`Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs:711-758`) with `def.isDebuff` and General origin, and no Cipher Lock gate.
  3. Add an executor case beside `ExtendBuff` (`UtilityEffectExecutor.cs:180-185`).
  4. Add the parse case in `Combat2DataImporter.cs` and the format case in `Assets/Scripts/Combat/Editor/Importers/CombatCsvRoundTrip.cs` (near 504). The manifest carries the string.
  5. Data: `UtilityEffects` `ExtendDebuff:script_loop`, plus `ResolveConditions` and RechargeTurns as above.
  6. Tests: gate fails at 1 debuff; extends the shortest; logs when there is none; recharge.

### Viral Surge: PROPOSAL, rewrite

- **Card.** "Randomly gain +2 AP or lose 1 AP; always gain +1 random stat." Mythic, Meme, StatusUtility, Self, 1 AP. Payload is blank.
- **Meme profile.** Universal wildcard: random, hype effects, all-in plays. Statuses: Staggered, Surging. Outside the chemistry pass.
- **Meme pool today:**
  - Jackpot Shot (Mythic, 2 AP, power 58, ×2 / ×1 / ×0.5)
  - All-In (Epic, 98)
  - Lucky Scatter (Common, 3 AP, power 17 × 1–4, 25% random self buff per hit)
  - Double-or-Nothing (Mythic passive, end of turn 60% +1 AP / 40% −1 AP)
  - Meme Momentum (Mythic passive)
  - Hype Train (Legendary, power 52, riderless)
  - Dice Roll (Common, ally heal 20%)
  - Meme Magic (Uncommon, ally heal 20%)
  - Wildcard Swap (blank)
- **As written it duplicates other Meme moves.** The AP coin flip is Double-or-Nothing on an active. The random +1 is Lucky Scatter's rider. It also needs an exclusive either/or AP branch, which no opcode does; independent rolls give four outcomes.
- **Rarity conflict as written.** Net expected value is +0.5 AP plus one random stat for 1 AP. That is below Epic AP Boost (0 AP, +2 AP, recharge 3), so a Mythic would sit under an Epic.
- **Proposed effect.**
  - `StatusEffects` = `Surging:1:1.0:Self`
  - `UtilityEffects` = `RandomStageChange:1:1:100%:Self:6:viral_surge|GainAP:2:50%:Self:viral_surge_ap`
  - Card: "Gain Surging (next hit +25%) and +1 to a random stat; 50% chance to gain +2 AP."
- **Why this version.** It uses Meme's own Surging status, which no Meme move produces today. It sits next to Mythic Leverage Up (Atk +2, Def −1, Surging, 1 AP) rather than under an Epic. No self-damage.
- **Engine.** None. Every piece is authored elsewhere: Surging on Self (`risk_spike`), `RandomStageChange` on Self (`lucky_scatter`), chance-gated `GainAP` (`multi_thread_shot`).
- **Pool.** None. See "Meme reaches no one."

### Wildcard Swap: PROPOSAL, rewrite

- **Card.** "Randomly swap one of your stage changes with target." Common, Meme, StatusUtility, SingleEnemy, 2 AP. Payload is blank.
- **As written it is distinct** (nothing swaps stages), but it is a new hook. Stages owned by statuses revert on expiry (E-15). Swapping an owned stage between two Chainlings breaks that bookkeeping, so it needs ownership transfer rules, not just a new opcode.
- **Proposed effect.** `UtilityEffects` = `RandomStageChange:-1:-1:100%:SingleEnemy:-6:wildcard_swap_foe|RandomStageChange:1:1:100%:Self:6:wildcard_swap_self`. Card: "Lower one random foe stat by 1 and raise one random own stat by 1."
  - It reads as a stat "swap", keeps Meme's randomness, and Meme has no foe-facing debuff today.
  - Bot (Automated Pressure, Automation Burst) and Glitched (Bit Rot) already roll random foe stats. This version is two-sided, which keeps it Meme.
- **Rarity check.** Common, 2 AP, two random ±1 stages. That is below Common Debuff Script (1 AP, two chosen −1s) per AP, and far below Epic Pivot Line or Mythic Data Scramble. No conflict.
- **Engine.** None.
- **Pool.** None. See "Meme reaches no one."

### Extraction Hit: PROPOSAL, implement as written

- **Card.** "Passive - hits can raise Atk; at Atk +3 also gain Speed." Rare, Mined, Passive. Payload is blank, and there is no `"extraction_hit"` literal in Runtime.
- **Mined profile.** Ramp attacker; crashes tanks. Keywords: physical ramp, extraction, scaling. Status: FOMO.
- **Mined pool today:**
  - Pickaxe Flurry (Common, 3 AP, power 14 × 2–4, Atk +1 per hit, cap +2)
  - Deep Dig (Uncommon, 4 AP, power 72)
  - Ore Strike (Rare, 2 AP, power 52)
  - Exploit Window (Rare, ExploitMark)
  - Refinery (Rare, Def +1, Yield)
  - Momentum Drill (Epic, 2 AP, power 54, ×1.25 vs debuffed)
  - Majority Pressure (Epic, AOE Speed −1)
  - Deep Reap (Legendary, 2 AP, power 58, 20% lifesteal)
  - Crystal Extraction (Common)
  - Ore Restoration (Uncommon)
- **Fit.** Ramp is the type's core, but only one Common move ramps today, and Mined has no working passive. A ramp passive is on-profile and distinct, because it rewards any hit, not one multi-hit move.
- **Proposed effect.** The first landed damaging hit each turn gives Atk +1, up to a passive cap of +3. The first time Atk reaches +3, Speed +1, once per battle. Numbers are tunable. "Can raise" did not name a chance, so once per turn keeps multi-hits in check.
- **Rarity check.** Rare passive ramp vs Uncommon Kindling (6% DoT mark) and Legendary Immutable Ledger or Conductive Counter. No conflict.
- **Engine (small, existing pattern).**
  - Add a `TryApplyExtractionHit` beside `TryApplyKindling` (`CombatResolver.cs:5550`, called at 2380). Use `HasEquippedPassive`, `MarkOncePerTurn` (as `shattered_armor` does at `CombatResolver.cs:4519`), and the normal stage write so Cipher Lock still blocks it.
  - Card text only in the CSV.
  - Tests: one raise per turn, cap +3, Speed once, Cipher Lock blocks.
- **Learn-pool check (hard rule).**
  - Kodiax (Uncommon body) **cannot** take it as-is. Its signature is Ore Strike (Rare), so a second Rare breaks "strictly highest". That is why the plan removed it.
  - Minefolk (Rare body, signature Deep Reap, Legendary) could legally hold it in a ≤Rare slot or the Rare/Epic slot, but the pool is full. No placement is proposed.

### Deflation Mark (Burned Tokens): PROPOSAL, implement as written

- **Card.** "Apply Burned Tokens so enemy DoT heals you; lower Atk -1." Rare, Burned, StatusUtility, SingleEnemy, 1 AP. `StatusEffects` `BurnedTokens:2:1.0:` and `StageChanges` `MeleeAtk:-1:1.0:false`. Only the Atk −1 runs (E-13).
- **Burned profile.** DoT pressure; wins long attrition lanes. Keywords: DoT, burn, coating setups. Key statuses: **BurnedTokens**, FUD.
- **Burned pool today:**
  - Cinder Burst (Common, 3 AP, power 15 × 2–4, 20% Atk −1)
  - Hype Burst (Common)
  - Kindling (Uncommon passive, Kindled 6%)
  - Ember Shower (Uncommon, 4 AP, power 38 AOE)
  - Ash Barrier (Uncommon, Mid Barrier, heals 5%)
  - Scorch Shot (Rare, power 52)
  - Sear Field (Epic)
  - Flame Charge (Epic, power 52)
  - Finish Burn (Legendary, power 60, ×1.5 below 40%)
  - Ember Wall (Legendary)
- **Fit.** Burned Tokens is a named profile status with no producer that works. Burned has burn pressure but almost no payoff for its own DoT. Healing off the burn is a distinct attrition-sustain role and pairs with Kindling.
- **Proposed effect.** While a foe holds Burned Tokens (2 turns), each end-of-turn DoT tick on it heals the status source for 50% of the damage dealt. Keep Atk −1.
  - Set `BurnedTokens` `IsDebuff` = true in `Statuses.csv`, so cleanses and debuff counts see it.
  - Card: "Mark a foe for 2 turns: DoT it takes heals you for half. Lower its Atk −1."
- **Rarity check.** Kindled 6% gives a 3% MaxHP heal per tick, under Legendary Deep Reap's 20% lifesteal. No conflict.
- **Engine (new hook).**
  - Heal the source in `StatusProcessor.ApplyEndOfTurnDamage` (`Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs:34`; Kindled's tick calls it at 93).
  - `StatusInstance.sourceId` exists (`Assets/Scripts/Combat/Runtime/Data/StatusInstance.cs:33`). The processor needs a way to resolve that id to a living Chainling.
  - Tests: heal equals half the tick, no heal after expiry or source KO, cleanse removes it.
- **Cheaper fallback (PROPOSAL, data only).** Replace `BurnedTokens` with `HealSelfPerTargetDebuff:5:100%:deflation_mark`. It is less distinct, because it is Crystal Extraction's opcode without the cleanse.
- **Learn-pool check (hard rule).**
  - The only Burned body is Cinislug (Common). Its non-signature slots allow Common, plus one Common or Uncommon (Kindling). A Rare does not fit, which is why the plan removed it.
  - No Burned body can hold it today. Placement is an open question.

### Hard Fork (formerly Recoil Strike): PROPOSAL, keep as is

- **Card.** "Heavy contact hit; then remove one debuff from yourself." Uncommon, Forked, DamagingMelee, 4 AP, power 72. `Cleanse:1:100%:Self:hard_fork`, `AfterMoveComplete`. It works. HP recoil was shelved (SD-SHELF).
- **Forked profile.** "Infrastructure spreader. Chain splits propagate pressure or route a defensive cleanup **without recoil stacking**."
- **Forked pool today:**
  - Rapid Strike (Rare, 1 AP, power 26, PRIOR)
  - Fork Split (Legendary, 4 AP, power 56 AOE)
  - Chain Fork (Common, 2 AP, power 12 × 2–4)
  - Voltage Rush (Mythic)
  - Leverage Up (Mythic)
  - Pivot Line (Epic)
  - Conductive Counter (Legendary passive)
  - Charged Relay (Epic)
  - Surge Recovery (Uncommon)
- **Fit.** Hard Fork is Forked's only single-target heavy, and its self-cleanse is the profile's "defensive cleanup". It is distinct.
- **Optional recoil flavor (not recommended).** Add a self Def −1 stage, `MeleeDef:-1:1.0:true` as Key Rotation authors it. That is not HP and not a debuff status, so the Cleanse won't undo it. It would match Gas War's drawback, but the type profile explicitly avoids recoil.
- **Rarity.** All three Uncommon 4 AP power-72 heavies share a rarity: Hard Fork (cleanse), Deep Dig (none) and Gas War (+1 surcharge). Hard Fork is the best of the three, but that is not a cross-rarity conflict.
- **Learn-pool.** Off-type exception on Balloom only. The plan says "Hard Fork stays off-type only". No Forked body learns it.

### Rarity conflicts found

- Viral Surge **as written**: a Mythic under Epic AP Boost. The rewrite fixes it.
- Script Loop **as written**: a Common that would beat Epic Extend Protocol. The one-debuff trim with RechargeTurns 2 fixes it.
- All-In **today and after the change**: Epic power 98 is above Legendary Hammer Sync at 80. This is priced by the drawback, not fixed here.
- None of the proposals creates a new conflict.

### Stale master items (to refresh)

The rows above were not rewritten. Line numbers are current, after the two-line pointer insert; the pre-insert number is in brackets. Values are from `Moves.csv` on 2026-10-22.

| Where | Master says | Current value |
|---|---|---|
| L25 [L23] rule; L272 [L270] E-17 | Ash Guard, Compound Guard, Ice Wall and Blockade use `hitsRemaining == -1` (unlimited) | Eight-move `BlockWindow` family. Walls block melee, barriers block ranged. 1 hit, 2 turns, PRIOR, RechargeTurns 2 (1-turn recharge), one block per Chainling. Renamed Ash Barrier, Compound Barrier, Lattice Barrier, Buffer Barrier (`barrier_wall`). |
| L96 [L94] E-16 | Ice Wall uses the unlimited budget | Mid Wall `BlockWindow:40%:Melee`, 2 AP, +1 AP to the holder if faster (once) |
| L168 [L166] MV-blockade | Common, 3 AP, 25% for two turns, unlimited hits | Uncommon, 3 AP, Greater Wall `BlockWindow:60%:Melee`, 1 hit, PRIOR |
| Missing | No Armor row | `guard_posture` tag `ARMOR`, key `armor_guard_posture`, 25% for 1 hit or 2 turns, stacks with a block. Flat Armor 12/16 is only a proposal (`Design/ARMOR_MECHANIC_V1_ANALYSIS.md`). |
| Missing | No AP-denial rows | Chained Memory Leak is a true lock. V3 proposals: Memory Leak recharge, Transaction Tax to `DrainAP:1`, no drain cap (`Design/AP_DENIAL_ENDGAME_ANALYSIS.md`, `Design/COMBAT_ISSUES_DECISION_BRIEF_V3.md`) |
| Missing | No typeless row | Typeless rule decided but not built; V3 reports `ResolveAttackType` still uses the attacker's type |
| L165 [L163] | ap_drain Common | Uncommon |
| L170 [L168] | network_sync Common | Epic (Node) |
| L174 [L172] | viral_surge Common | Mythic |
| L175 [L173] | wildcard_swap 1 AP | 2 AP |
| L190 [L188] | jackpot_shot power 64 | Mythic, power 58 |
| L192 [L190] | lucky_scatter Rare, power 18 | Common, power 17, 3 AP |
| L159 [L157] | shard_scatter power 18 | Legendary, power 22, 4 AP |
| L197 [L195] | ap_siphon power 56, drain doesn't run | Rare, power 54, `DrainAP:1:50%` authored |
| L199 [L197] | momentum_drill power 64, no +25% | Epic, power 54, ×1.25 vs debuffed authored |
| L203 [L201] | interest_strike Rare, no AP grant | Epic, conditional `GainAP` authored |
| L204 [L202] | rallying_slam Rare, power 64, no AP grant | Epic, power 52, conditional `GainAP` authored |
| L205 [L203] | pickaxe_flurry power 16, no Atk raise | Common, power 14, 3 AP, Atk +1 per hit, cap +2 |
| L208 [L206] | glitch_feedback cleanse doesn't run | `Cleanse:1` authored |
| L154–156 [L152–154] | chain_seal, key_rotation, zip_step: Evasion half doesn't run | `ChangeStage:Evasion` authored on all three |
| L142–148 [L140–146] | Accuracy/Evasion rows "empty payload", Work = data | `ChangeStage:Accuracy` / `Evasion` authored; Work should be `none` |
| L261 [L259] BAL-EPIC | bulwark_blow is the only Epic damage move (index 45) | bulwark_blow power 50, 3 AP. Nine Epic damaging moves. |
| L3 header; Counts block L58–85 [L56–83] | Dated 2026-10-19 | Counts change once the rows above are refreshed |
