# AP cost ladder: implementation audit

> **Status (2026-10-18): superseded by the implemented combat balance follow-up v1.** The ladder/data defects and approved decisions identified here were addressed, imported, and covered by focused tests. Current evidence is recorded in [followups_v1_baseline_tests.md](followups_v1_baseline_tests.md), with CSV-to-asset results in [moves_csv_asset_parity.md](moves_csv_asset_parity.md). Manual battle smoke remains before the issues master can mark the follow-up fixed.

- Plan: `C:\Users\mando\.cursor\plans\ap_cost_ladder_9eb41b13.plan.md` (all 7 todos marked completed)
- Audited: 2026-10-18, uncommitted working tree on top of `03641b3b`
- Scope: audit only. No code, data, or tests were changed. This file is the only new file.
- Tests: **not run.** Unity was open (`Signal Jam 3D - Combat2_TestHarness`), so EditMode tests could not run headless. Every "test asserts X" below comes from reading the code.
- Line numbers point at the current working-tree files. CSV rows are file line numbers in `Docs/Specs/Combat/Data/Moves.csv`. The header is line 1.

> The historical findings below describe the pre-follow-up tree. Use the status note above for the current result.

## 1. Summary

The engine side of the plan is mostly in place and reads correctly:

- `SpendRemainingAp`
- Fee Collector's first-actor drain
- the menu counting a faster ally's committed gift
- the Thread Lock reapply gap
- the Gas Spike and FUD stage revert
- bench recharge
- Packet Skim and the Kitsauto pool swap

The 21 cost changes match the plan row for row. All 209 `Move_*.asset` files agree with the CSV on apCost, power, accuracy, hits, rarity, recharge, tags, utility opcodes, timing, and description. The one exception is Ice Wall, and the reason is that the CSV row itself is broken.

The problems are in the data around the engine work:

1. **Ice Wall regression (High).** Row 111 has an unquoted comma again. A trailing comma was also dropped, so the row's column count looks valid. The importer shifted the columns, and `Move_ice_wall.asset` now has a truncated description and a zero-magnitude `MitigationWindow`. The `GainApOnDamageIfSpeedHigher` opcode is gone. `moves_import_validation.md` reports 2 errors, while `COMBAT_ISSUES_MASTER.md` still says E-16 is "fixed in PR".
2. **Interest Strike gains AP (High).** The cost cut to 2 AP collides with the newly authored `>=` per-hit gate. On a fresh Chainling it refunds about 2.4 AP on average for a 2 AP cast. That is free damage that also builds a bank, which undercuts the whole ladder.
3. **Species pools (High).** 12 of 18 pools have no Common damaging move. Balloom's whole damaging kit now costs 4 AP, so a fresh Balloom cannot attack on turn 1. In Validape's pool the only damaging move is the 4 AP Hammer Sync, and its CSV starter `baseline_striker` was dropped from the asset on re-import.
4. **All-In card text (Medium).** "Skip your next action" is not what the engine does. `rechargeTurns` 2 locks only All-In. The Chainling acts normally on the next turn.
5. **Retier shape (Medium).** The retier hits the global counts. The total is 209/63 instead of the plan's 208/62, because Packet Skim was added. The per-type shape the plan asked for is not met: typeless is 26/14/14/4/1/0 against a target of 34/12/8/4/1/0. Several typed kits have no Common or no Rare.

Plan-item tally (38 items): **28 Implemented, 6 Partial, 0 Missing, 4 Diverged.**

**Scope note.** The working tree also holds a large concurrent "issues remediation v2" pass. It touches Uplink, Kindling, Setup Feed, Leaked, the Acc/Eva opcodes, `ExtendBuff`, `RaiseLowestStage`, Statuses.csv deletions, and Supabase and backend files. This audit separates the two passes where it can. Findings caused by that pass, such as Validape losing Coordinated Shot or the Glitch Feedback cleanse, are labeled as such.

## 2. Per-plan-item table

| # | Plan item | Status | Evidence |
|---:|---|---|---|
| 1 | All-In minimum cost 4 | Implemented | CSV row 191 `APCost` 0→4. `Move_all_in.asset` apCost 4. |
| 2 | `SpendRemainingAp`: spend excess after the normal cost; eligibility uses printed 4; damage flat | Partial | `CombatResolver.cs:1706-1710` spends `currentAP` after `TrySpendCost`. Damage does not scale. Gaps: Streamlined's discount lowers the minimum to 3 (`CombatActionEligibilityService.cs:197`). `CheckAPRefund` (`CombatResolver.cs:1793`) can still refund AP after the spend-all. |
| 3 | `rechargeTurns` 2 = "one skipped turn"; text says the turn is skipped | Diverged | Row 191 rc 2. `CombatResolver.cs:1779-1788` adds `Recharging` for **that move only**. The description says "Skip your next action", but the Chainling can use any other move next turn. The narration line reads "All-In now recharging (2 turns)". |
| 4 | All-In → Epic; Balloom still learns it | Implemented | Row 191 Common→Epic. `SpeciesMovePools.csv:22` keeps `all_in` (also initial). |
| 5 | Fork Split 3→5, Common→Legendary | Implemented | Row 118. Asset matches. |
| 6 | Shard Scatter 3→5, Uncommon→Legendary, power stays 18 | Implemented | Row 187. Eva clause removed. |
| 7 | Recoil Strike, Ember Shower, Automation Burst, Volatile Shot, Deep Dig → 4 AP, Uncommon | Implemented | Rows 121, 104, 162, 72, 133. |
| 8 | Hammer Sync, Cryo Ray → 4 AP, Rare→Legendary | Implemented | Rows 198, 200. |
| 9 | Body Check, Rapid Pummel, Focus Shot, Breach Rush, Frost Shards, Chain Fork, Stone Barrage 3→2 | Implemented | Rows 3, 11, 6, 16, 114, 123, 85. |
| 10 | Interest Strike 3→2; "do not retune that gate" | Diverged | Row 147: cost 2, but the same edit also authors `ResolveConditions Self:StageAtLeastOtherStage:0:...MeleeDef:MeleeAtk`, adds `GainAP:1` `AfterEachConfirmedHit`, changes the text from "exceeds" to "at least", and moves the card Rare→Epic. Before this tree the rider did not run (`MV-interest_strike`). See issue H2. |
| 11 | AP Drain: DamagingRanged / None / 16 / 95 / projectile, `DrainAP:1` 100% + `GainAP:1` 25% self, `AfterEachConfirmedHit`, cost 2 | Implemented | Row 33 and `Move_ap_drain.asset` (chances 1 and 0.25, timing 2). |
| 12 | Leave-alone costs (spine, Rapid Strike, Lean Hit, Blink Lunge, Finish Burn, Multi-Thread Shot, Crystal Bash, Wide Burst, Compression Burst, Bean Beam) | Implemented | Only the 21 listed rows changed `APCost` (CSV diff vs HEAD). Bean Beam row 205 stays 2. |
| 13 | Delete dead clauses (All-In, AP Drain, Recoil, Volatile, Deep Dig, Shard Scatter); keep live riders | Implemented | Rows 191, 33, 121, 72, 133, 187 rewritten. Ember, Automation, Hammer, and Cryo riders are live (StageChanges, `RandomStageChange`, `ConditionalDamage` columns). The All-In wording problem is in item 3. |
| 14 | `AP_COST_DAMAGING_MAX` 3→5 | Implemented | `CombatConstants.cs:13`. |
| 15 | Re-import with `Combat2DataImporter` | Partial | 209/209 assets match the CSV on every checked field. Ice Wall imported from a column-shifted row (issue H1). |
| 16 | Locked rarity rows | Implemented | All-In Epic. Cryo, Hammer, Fork, Shard Legendary. Recoil, Ember, Automation, Volatile, Deep Dig, Bean Beam Uncommon. Fee Collector Rare. |
| 17 | Catalog spread 62/42/42/31/21/10 (208 moves) | Diverged | CSV and assets: 63/42/42/31/21/10 = 209. Packet Skim was added as a Common. This is documented in `COMBAT_Rarity.txt` §1.2 and asserted in `ApCostLadderTests.cs:362-368, 449-454` (63, not 62). |
| 18 | `COMBAT_Rarity.txt` §1.2 updated; §3 weights untouched | Implemented | The diff only touches §1.2 lines. |
| 19 | Category floors (passives ≥ Rare, support ≥ Uncommon, field ≥ Rare) | Partial | Support and Field are clean. `uplink` (row 208) and `kindling` (row 209) are Uncommon passives. Their Uncommon tier was approved in `COMBAT_ISSUES_MASTER` (APR-RELAY, APR-KIND), so the two rules conflict. |
| 20 | Power ceiling (no Common at printed power ≥72; Common ≤ same-type Rare of the same job) | Partial | No Common ≥72. Flags: Compressed `precision_strike` (row 148, Common, 60.8 expected) > Rare `lean_hit` (43.7, but 1 AP). Typeless `refined_blast` (Common, 3 AP AoE) > best typeless Rare when AoE is counted. |
| 21 | Per-type shape: typeless 34/12/8/4/1/0; typed kits ~2C/2U/2R/1–2E | Diverged | Typeless is 26/14/14/4/1/0. Staked has 0 Common. Node has 0 Rare (1C/6U). Compressed has 0 Uncommon and 0 Epic. Frozen has 0 Epic. Meme has 0 Rare. Gas, Glitched, Shard, and Staked have no Common damaging move. |
| 22 | Mythic 10, clustered on Meme, Glitched, Gas, Forked plus signature passives | Implemented | Meme 4 (two passives), Glitched 3, Forked 2, Gas 1. |
| 23 | Legendary ≈ one signature per type (20 typed + 1 typeless) | Implemented | 20 typed + `defensive_reflex`. |
| 24 | Stat substitution Epic+ (Crystal Bash moves up with Bulwark Blow) | Implemented | Rows 185 and Bulwark Blow are both Epic. |
| 25 | 3-AP commons scored by the rubric, no exemption | Partial | Still Common with clear riders: `lucky_scatter` (row 196, random buff, Rare→Common), `pickaxe_flurry` (row 136, Atk-on-hit rider now live), `compression_burst` (Def drop), `cipher_burst` (buff erase). By the rubric these are Uncommon or higher. |
| 26 | Fee Collector: drain 1 from the first living opposing actor in the built queue, only if that foe's AP > holder's AP; priority and swaps count; one per holder per turn | Implemented | `CombatResolver.cs:972` (after the queue is built, before resolution) and `:6670-6727` (`firstFoe.currentAP <= holder.currentAP` skip at `:6694`). `feeCollectorAppliedTurn` guard in `CombatState.cs`. |
| 27 | Remove the TurnStart `AllEnemies` drain | Implemented | Row 75: `TriggerCondition` None, `UtilityEffects` empty, `TRIG` tag removed. |
| 28 | Menu counts a faster ally's committed AP Donation or AP Router | Implemented | `CombatActionEligibilityService.cs:44, 112-188`. `CombatActionSelectorBridge.cs:578, 997`. `CombatSelectionCoordinatorUGUI.cs:497, 514`. Caveats in issue L2. |
| 29 | Thread Lock held through the grant turn so the next regen is the blocked gain | Implemented | Uses the existing turn-start pending path (`CombatResolver.cs:3764, 4003`). Asserted at `ApCostLadderTests.cs:243-283`. |
| 30 | Thread Lock reapply gap (cannot reapply until two turns after expiry) | Implemented | `StatusProcessor.cs:269` (`threadLockBlockedUntilTurn = turn + 2`), `:580-598`. `CombatResolver.cs:3950-4000` (pending queue). Caveats in issue L1. |
| 31 | Gas Spike and FUD stages revert on expiry | Implemented | `StatusProcessor.cs:639-645` owned list. The revert runs through `RevertChemistryStageEffects`. Caveat in issue M4 (cleanse). |
| 32 | Bench ticks `Recharging` only | Implemented | `CombatResolver.cs:475-514`. Other benched statuses stay paused. |
| 33 | Update the bench-pause test, add a bench-recharge test | Implemented | `CombatRuntimeFixTests.cs:246` (Slowed pauses), `:275` (Recharging 2→1). |
| 34 | New 1 AP Common Bot drain, `DrainAP:1`, species-neutral text | Implemented | Row 210 `packet_skim`: "Cut the target's AP by 1." Registered in `MoveDatabase.asset`. |
| 35 | Kitsauto: Packet Skim replaces `automated_pressure`; keep the others; no `mev_extract` or `thread_lock` | Implemented | `SpeciesMovePools.csv:7` and `Kitsauto.asset`. Note: the plan's reason ("so Kitsauto still has a common damaging move") no longer holds for `multi_thread_shot`, which the retier made Uncommon (row 161). `blade_flurry` (Common) covers the rule. |
| 36 | EditMode tests for the plan's list | Partial | All 12 plan bullets have a test (`ApCostLadderTests.cs`, `CombatRuntimeFixTests.cs`). Gaps: All-In is never shown castable again on the turn after the lock. Gas and FUD use synthetic `StatusDefinitionSO`s, not the real assets. The counts assert 63, not 62. The catalog test checks only cost and rarity parity (plus All-In's tag and recharge). Not run in this audit. |
| 37 | Out of scope respected: no power retune, no recoil, miss cost, or Crystal Bash power, no shielded AP or MEV steal | Implemented | The only `Power` change in the CSV diff is AP Drain 0→16. |
| 38 | Do not regenerate the battle-service manifest | Implemented | `backend/Chainbound.BattleService/manifests/gameplay-manifest.json` was last written 2026-10-15 05:22 CT. It still has `all_in` apCost 0 and rarity 0, and has no `packet_skim`. That follows the plan but leaves a drift risk (open question Q6). |

## 3. Broken or suspicious logic, ranked by severity

### High

**H1. Ice Wall CSV row is column-shifted, and the asset was imported broken.**
- Evidence: `Moves.csv:111` has the description `...for the rest of this turn. If you are faster, gain +1 AP when hit.` unquoted. HEAD had the same comma with 44 fields. The working copy has 43, because one trailing comma was removed, so a column-count check passes.
- Parsed result: `SelectionPolicy=" gain +1 AP when hit."`, `ActiveConditionType=EffectOnly`, `UtilityEffects="0"`.
- `Move_ice_wall.asset`:
  - description truncated to "...If you are faster"
  - a single `utilityEffects` entry, effectType 0 (`MitigationWindow`) with magnitude 0
  - `GainApOnDamageIfSpeedHigher` gone
- `Docs/Combat/Audits/moves_import_validation.md` (generated 2026-10-18 03:11) lists both BadEnum errors and dropped the 2026-10-16 "resolved" note. `COMBAT_ISSUES_MASTER.md` E-16 still says "fixed in PR".
- Learners: Gilarime, Humpacked, Blockwyrm.
- Recommendation: quote the description, re-import, and set E-16 back to open until then. Add the CSV-integrity test in §6. It should check each row's field count against the header and enum validity for every enum column.

**H2. Interest Strike (and Rallying Slam) now gain AP on net.**
- Evidence: row 147 is 2 AP, 2–3 hits at 95%, with `GainAP:1` per confirmed hit gated by `StageAtLeastOtherStage:0` (MeleeDef stage ≥ MeleeAtk stage). A fresh Chainling is 0 ≥ 0, so each hit refunds 1. Expected refund ≈ 2.5 × 0.95 ≈ 2.4 AP against a 2 AP cost.
- Every cast is free damage (index 42.75) plus about +0.4 AP. That banks a 4–5 AP hit with no boost, which is against the plan's premise and Cronus's "high damage needs setup" rule.
- `rallying_slam` (row 87, Block Epic, 64 power, 2 AP, +1 AP on the same gate) is a 60.8 hit that nets 1 AP.
- Recommendation: make the grant once per cast (`AfterMoveComplete` or a once-per-move key) or use the card's original strict "exceeds" gate. Or keep cost 3 until the gate is decided. The plan said "do not retune that gate", but the gate was newly authored in this same tree, so this needs a decision (Q2).

**H3. Pools lose their turn-1 attack, and the importer drops starters.**
- 12 of 18 pools (learnable + exclusive) have **no Common damaging move**: validape, gilarime, kreechank, cinislug, rattlestake, humpacked, deghoul, cofi_lootwak, blockwyrm, hopoch, balloom, plus kitsauto's `multi_thread_shot` now Uncommon. Rattlestake has no damaging move at all.
- **Balloom** (`SpeciesMovePools.csv:22`): every damaging move is 4 AP (`all_in`, `volatile_shot`, `recoil_strike`). Its initial loadout is `all_in|recoil_strike`. A fresh Balloom has 2 AP on turn 1 and no damaging option. Its only plays are Fuel Surge (if equipped) or pass.
- **Validape**: the concurrent pass swapped `coordinated_shot` for `uplink`, so the only damaging move in its pool is the 4 AP Hammer Sync. Its CSV `InitialMoves=baseline_striker` is not in the pool, and `Combat2DataImporter.cs:2578-2582` skips initial moves that are not in the pool. `Validape.asset`, `Gilarime.asset`, and `Kitsauto.asset` now have `initialMoves: []` (they had baselines at HEAD).
- Recommendation: either guarantee a ≤2 AP damaging Common per pool, or add each species' baseline starter to its pool. Fix or remove CSV `InitialMoves` values that the importer will drop, and make the importer warning fail the import. Decide Balloom's turn 1 explicitly (Q3).

### Medium

**M1. All-In's "Skip your next action" is false.** The recharge is a per-move lock (`CombatResolver.cs:1779-1788`, eligibility reason `MoveRecharging`). After All-In, the Chainling regens 2 and can cast any 1–2 AP move next turn. The narration says "(2 turns)" while it locks one turn.
- Recommendation: either reword the card ("All-In can't be used next turn") or add a real self-skip, for example a one-turn status that forces Pass. Pick one before the card ships (Q1).

**M2. Streamlined discount is not floored.** `GetEffectiveApCost` returns `Math.Max(0, apCost + surcharge - discount)` (`CombatActionEligibilityService.cs:197`). With Streamlined Flow (row 156, Humpacked's flagship), every 1 AP move becomes free. Humpacked's own `lean_hit` (1 AP, 43.7 index) is then free every turn while the bar grows. All-In's minimum drops to 3, against the plan's "printed 4".
- Recommendation: floor the discounted cost at 1 for printed costs ≥1, and use the printed cost as `SpendRemainingAp`'s minimum.

**M3. New AP events are not narrated.**
- `CombatApService.Drain` and `TrySpendCost` log `APSpent`. Its default delivery is `PopupOrHudOnly` (`CombatNarrationProfileSO.ResolveDefaultDelivery`), and `CombatNarrationComposer.cs` has no `APSpent` case.
- Fee Collector's drain fires before the first action and logs no `PassiveTriggered`. A foe whose move then fails reads only "{actor} could not use {move}." The AP Drain and Packet Skim cuts and All-In's excess spend are also silent.
- The Thread Lock gap message is a `SystemMessage` (also `PopupOrHudOnly` unless it matches the interruption phrases).
- Recommendation: log `PassiveTriggered` (or a dedicated drain event) for Fee Collector, with an "X lost 1 AP to Fee Collector" template. Add a drain sentence for `DrainAP` on hit. Make the no-AP failure name the cause when a drain happened this turn.

**M4. Cleanse does not revert owned stages.**
- `StatusProcessor.Cleanse` (`:737-750`) removes FUD and Bot Swarm (now owned-stage statuses) without calling `RevertChemistryStageEffects`, so their stat drops become permanent. That breaks "timed-status stat changes end with the status".
- Related, from the concurrent pass: `glitch_feedback` (row 173) now runs `Cleanse:1` on **SingleEnemy**, which removes a debuff from the foe.
- Recommendation: revert owned and temporary deltas in `Cleanse` (and in any other status removal path). Confirm Glitch Feedback should strip a foe *buff*, not a debuff.

**M5. The AI does not see the new costs.**
- `AIHelpers.DamagePerAP` (`AIHelpers.cs:145-149`) divides by printed `apCost`. It ignores surcharge, discount, and `SpendRemainingAp`, and still returns 0 at cost 0. No damaging move costs 0 now, but a Streamlined discount creates effective-0 moves.
- `AggressiveAI.cs:50-58` adds +50 for lethal, so it will fire All-In at 10 AP and throw away 6.
- `UtilityStatusAI` gives every StatusUtility a base 30, so Packet Skim scores about 39 even against a 0 AP foe it cannot drain.
- Affordability is fine: `AIHelpers.GetLegalActions` (`:36`) filters with `EvaluateMoveSelection`, so the AI never selects a move it cannot pay for at selection time. It can still be drained into the no-AP path by Fee Collector.
- Recommendation: score with `GetEffectiveApCost`. Treat `SpendRemainingAp` as costing `currentAP`. Value drains by the foe's AP versus their cheapest uncastable move (AP_ECONOMY §6.2).

**M6. 4 AP heavy hits are dominated by 2 AP typed hits.**
- Legendary Hammer Sync (4 AP, 72, +10% per buffed partner, cap +30%) against Common Coordinated Shot (2 AP, 64, +15% per buffed partner, cap +30%, row 66). With one partner, the Common hit is 73.6 for 2 AP and the Legendary is 79.2 for 4 AP.
- Two typed spine casts (121.6) beat any 4 AP hit (68.4–72.2). The heavy hits now mainly buy finishing burst.
- This is a design call, not a bug (Q4).

**M7. Battle-service manifest drift.** The client catalog changed. The server manifest still has the old All-In (0 AP, Common), no Packet Skim, the old Fee Collector, and old rarities. Any server-authoritative battle will resolve different costs from the client. This follows the plan, but it is a release blocker (Q6).

### Low

- **L1.** `threadLockBlockedUntilTurn` (`ChainlingInstance.cs:99`) is never reset in `InitializeBattle`. If a `ChainlingInstance` is reused across battles, a stale value can block Thread Lock in the next battle's early turns. It is also only set on natural expiry (`StatusProcessor.cs:269`), so a cleanse, swap, or KO removal skips the gap. Recommendation: reset it at battle start. Set it in a shared "status removed" path.
- **L2.** `ResolveCommittedFasterGiftAp` ignores several things that make the gift fail at resolution:
  - Thread Lock on the recipient (the gain is blocked)
  - Isolated (the transfer is refused)
  - `apDonationUsedThisTurn`
  - a recipient move with `PRIOR`, which resolves before the gift
  - Fee Collector draining the giver first
  
  The menu shows the move as affordable, and the resolver then fails it through the no-AP path. The plan accepts that failure, but the UI misleads. Recommendation: mirror the Thread Lock, Isolated, and PRIOR checks in the projection.
- **L3.** The `SpendRemainingAp` tag is not documented in `_LEGEND.md` "Tags". It is also the only mixed-case tag. Recommendation: document it, or rename it to `SPENDALL` for consistency.
- **L4.** `COMBAT_ISSUES_MASTER.md` is stale. `MV-all_in` still says "AP column 0 ... it is free", E-16 says Ice Wall is fixed, and BAL-COST still lists `all_in` as undecided.
- **L5.** Fee Collector drains the first actor even when that action is a Pass or a swap-out (`ApCostLadderTests.cs:208-212` asserts the swapper is drained). This is plan-faithful, but the drain often lands on a Chainling that is leaving.
- **L6.** `CheckAPRefund` (`CombatResolver.cs:1793`) and `ProcessActionEconomyModifiers` run after `SpendRemainingAp`, so a 3.1 EFF modifier can refund AP into All-In's "spend everything" turn.

### Engine edge cases checked

| Case | Result |
|---|---|
| AP checked and spent at execution, not reserved | Yes. `EvaluateMoveSelection` runs again at resolve (`CombatResolver.cs:1595`), then `TrySpendCost` (`:1689`). Failure goes to `LogNoApActionFailed`. |
| AP 0 / 0-cost moves | `TrySpendCost` with cost 0 succeeds without logging. `SpendRemainingAp` is skipped at 0 AP. |
| Cap 10 | `Gain` clamps to `apCap`. All-In at 10 spends 10. The menu gift projection does not clamp (harmless, since no move costs more than 5). |
| Drain before acting | Fee Collector runs after the queue is built. A foe locked into an exact-cost move fails through the no-AP path with no damage. That is the plan's intent, but it is unnarrated (M3). |
| `nextMoveApSurcharge` | Included in the effective cost and consumed only after a successful spend (`:1688-1699`). The All-In minimum becomes 5 under a surcharge. |
| Recharge timing | rc 2 ticks to 1 at the end of the cast turn and expires at the end of the next turn: one locked turn. It ticks on the bench (`:475-514`). |
| New opcodes or statuses | No new opcode for this plan. `SpendRemainingAp` is a tag. `ExtendBuff`, `RaiseLowestStage`, and `Kindled` come from the concurrent pass and were appended at the enum ends, so serialized ints are unchanged. |

## 4. Data gaps

1. Ice Wall row 111 is shifted and its asset is broken (H1).
2. CSV `InitialMoves` entries that the importer drops because they are not in the pool: `validape:baseline_striker`, `gilarime:baseline_shot`, `kitsauto:baseline_jab`. The assets now have `initialMoves: []`.
3. 129 of 209 moves are in no species pool (learnable, exclusive, initial, or off-type). They are candy-only. This is not new, but the retier changes their drop odds.
4. There are no orphan move ids. Every pool, initial, and off-type id resolves to an asset. `MoveDatabase.asset` has 209 entries. There are no duplicate `moveId`s. The `ghost` species asset has no CSV row.
5. Description vs payload: the plan rows are consistent apart from All-In (M1). A heuristic scan found ~30 older rows whose printed numbers are not in any CSV column, for example `precision_focus` 75%, `immutable_ledger` 20%, `yield_spike` 4+/25%, and `double_or_nothing` 60/40. These are likely hardcoded by move id in the resolver and were not re-verified here.
6. `multi_thread_shot` went Common→Uncommon, which removes the plan's stated reason for keeping it on Kitsauto.
7. The server manifest is stale (M7).
8. `_LEGEND.md` does not document `SpendRemainingAp`.

## 5. Design-rule check

| Rule | Verdict | Notes |
|---|---|---|
| Typed moves beat typeless equivalents | Holds | At each cost, typed beats typeless: 1 AP 49.4/43.7 vs 15.2, 2 AP 60.8 vs 37.8, 3 AP ~51 vs 39.1. Packet Skim (Bot, 1 AP, drain 1) against AP Drain (typeless, 2 AP, drain 1 + chip) is a fair sibling pair. |
| Rare typeless ≈ typed common | Weak | The typeless Rare top is `overdraw_snipe` at 39.1 for 3 AP, against a typed 60.8 spine at 2 AP. Unchanged by this plan, which left those powers alone on purpose. Note that most typed 60.8 spines are no longer Common (Block, Burned, Frozen, Mined, Shard, Staked are Rare; Glitched and Meme are Mythic). |
| High-damage moves need AP-boost setup to fire early | Partial | 4–5 AP hits now need a bank or a gift. Broken by Interest Strike and Rallying Slam AP-positive loops (H2) and Streamlined free 1-AP moves (M2). All-In at 4 fires on turn 2 from regen alone, and its excess spend costs nothing on that earliest turn. §6.1 proposed 6. |
| AP denial works; boost and denial offset | Partial | Fee Collector, Packet Skim, AP Drain, and Thread Lock each slip a bank by one turn. There is no shielded AP or 2-AP cap (out of scope), so the offset table in AP_ECONOMY §6.2 is not reachable yet. `data_scramble` (row 174, Mythic, 0 AP, +2 AP, no recharge) is an uncapped boost. |
| Bot is the AP-denial type | Partial | Bot has Packet Skim (C), MEV Extract (R), Thread Lock (L). Gas has Fee Collector (R), Transaction Tax (E), AP Siphon (R), the stronger set. Kitsauto, the only Bot species, carries just Packet Skim. |
| Meme has no chemistry | Not changed | No diff touches Meme chemistry. The matrix asset was not re-verified. |
| Recharge ticks on the bench | Holds | `CombatResolver.cs:475-514`, test `CombatRuntimeFixTests.cs:275`. |
| Timed-status stat changes end with the status | Partial | Gas Spike and FUD revert on expiry. Cleanse removal does not revert (M4). |
| Simple effects that match the name | Partial | All-In's text promises a skipped action it does not deliver (M1). "Recoil Strike" and "Volatile Shot" now have no recoil or volatility. That was the plan's call, but the names now overpromise (Q5). Packet Skim, AP Drain, and Fee Collector text match their behavior. |

## 6. Tests to add (verifiable)

1. **CSV integrity.** Parse `Moves.csv` with the importer's own `ParseCsvLinePublic`. Assert every row has exactly the header's field count, and every enum column (`SelectionPolicy`, `ActiveConditionType`, `Category`, `Targeting`, `ContactType`, `Rarity`, `BaseType`) parses. Ice Wall should fail today.
2. **Ice Wall asset.** `Move_ice_wall.asset` has `MitigationWindow` plus `GainApOnDamageIfSpeedHigher`, and its description ends "gain +1 AP when hit."
3. **All-In lock length.** Cast on turn N: not selectable on N+1, selectable on N+2 with ≥4 AP. Also assert another move *is* selectable on N+1. That pins the card text decision.
4. **All-In under Streamlined.** With Streamlined active and 3 AP, All-In is not selectable (if the printed-4 rule stands).
5. **Interest Strike economy.** On a fresh Chainling at 2 AP, record net AP after one cast across seeds. Assert the decided ceiling, for example net ≤ 0.
6. **Fee Collector failure path.** A foe at exactly the move's cost who acts first is drained, logs `ActionFailed` (no AP), and deals no damage. The slower foe still acts.
7. **Fee Collector narration.** One drain produces one player-visible sentence that names Fee Collector.
8. **Real status assets.** Load `StatusDef_GasSpike.asset` and `StatusDef_FUD.asset` (not synthetic definitions), cast Fuel Surge from `Move_fuel_surge.asset`, and assert the stages revert on expiry.
9. **Cleanse reverts.** Apply FUD, cleanse it, and assert the stages return.
10. **Thread Lock across battles.** After a battle that ended with `threadLockBlockedUntilTurn` > 0, a new `InitializeBattle` allows Thread Lock on turn 1.
11. **Pool rule.** For every species, learnable ∪ exclusive (∪ initial, if that is the rule) contains a damaging move with AP ≤ 2, and one Common damaging move, if Cronus keeps that rule. Pool size is 4–5.
12. **Initial moves.** Every CSV `InitialMoves` id is in that species' pool, so the importer never drops one silently.
13. **AI cost.** `AggressiveAI` with 10 AP and a lethal 2 AP option does not pick All-In. `DamagePerAP` uses the effective cost.
14. **Gift projection.** The projected gift is 0 when the recipient is Thread Locked or Isolated, or when the recipient's chosen move is `PRIOR`.
15. **Manifest parity (when export is allowed).** Exported manifest costs and rarities equal the assets.

## 7. Open questions for Cronus

1. **All-In:** should the card really skip the Chainling's next action, or only lock All-In for a turn? The engine does the second today.
2. **Interest Strike gate:** should it be "Def stage exceeds Atk" (the old text, so 0 v 0 does not pay) or "at least" (the current gate, about +2.4 AP per 2 AP cast)? Should the grant be once per cast instead of per hit? The same question applies to Rallying Slam.
3. **Balloom:** a fresh Balloom cannot attack on turn 1 (all damage is 4 AP). Is that intended, or should it get a 2 AP Meme or Gas chip? Validape: keep Hammer Sync as its only damaging pool move after Coordinated Shot left?
4. Is a 4 AP Legendary hit supposed to lose to two casts of a 2 AP typed Common (Hammer Sync vs Coordinated Shot)?
5. With their riders deleted, should "Recoil Strike" and "Volatile Shot" be renamed?
6. **Manifest:** when should the battle-service manifest be re-exported? Until then the server resolves the old costs.
7. **Rarity shape:** accept 209/63 and the current per-type spread, or rebalance toward typeless 34/12/8/4/1/0 and about 2 Commons per typed kit? Should a typed 64-power spine stay Common in each type? Today 8 types have their spine at Rare or Mythic.
8. Uplink and Kindling are approved Uncommon passives, but the plan floors passives at Rare. Which rule wins?
9. Should Transaction Tax (1 AP steal of 1) be Epic? AP_ECONOMY §6.2 calls it "the right typed common".
10. Glitch Feedback now cleanses a debuff from the *foe*. Was a buff strip intended?

## 8. Decisions & follow-ups (2026-10-18)

Cronus answered open questions 1, 2, 4, 5 and 6 from section 7. This section turns those answers into concrete changes. Everything here is still a **recommendation**: no code, data, or test was edited, and nothing was committed. Source references are to the working tree as of 2026-10-18.

### A. All-In and Interest Strike: wording and payload

**Description length.** Neither `_LEGEND.md` nor `Combat2DataImporter` sets a limit, and no UI truncation of move descriptions turned up in the UI or test code. Current descriptions run from a median of 48 characters to a p90 of 61 and a maximum of 95 (Kindling). The wording below stays at 66 characters or fewer. In Moves.csv style, "Atk/Def" with no qualifier means the melee stat.

**Recharge wording convention (watch out).** Both existing rows write "N-turn recharge" with N equal to the `RechargeTurns` value: AP Boost (rc 3) says "3-turn recharge" and Extend Protocol (rc 2) says "2-turn recharge". The engine, though, locks the move for N−1 turns, because the status ticks at the end of the cast turn. If All-In followed that convention it would read "2-turn recharge" and mislead players, so the proposal states the effect in plain words.

**A1. All-In: lock itself, not the whole action.**
- **Engine.** Already correct. `RechargeTurns` 2 adds a move-scoped `Recharging` status (`CombatResolver.cs:1779-1788`). Eligibility reason `MoveRecharging` blocks All-In on the next turn only, and every other move stays usable. The status also ticks on the bench (`CombatResolver.cs:475-514`).
- **Payload change.** Description only. Keep `APCost` 4, tag `SpendRemainingAp`, and `RechargeTurns` 2.
- **Description, recommended (61 characters):** `Spends all your AP (min 4). Can't use All-In again next turn.`
- **Alternate (66 characters), if the damage cap should be explicit:** `Spends all your AP (min 4); power stays fixed. Locked next turn.`
- **Related fix (engine text, recommendation).** The log line `"{move} now recharging ({rechargeTurns} turns)"` (`CombatResolver.cs:1787`) prints "(2 turns)" for a one-turn lock. Print `rechargeTurns - 1`, or "until your next-next turn". The plan's own wording, "rechargeTurns becomes 2, which is one skipped turn" (plan line 53), is where "Skip your next action" came from. Plan line 152 already describes a self-lock: "cannot be selected again on the next turn".

**A2. Interest Strike: refund only when MELEE Def stage > MELEE Atk stage; +1 AP per hit; accuracy 90.**
- **Strict "exceeds" is not expressible today.**
  - `MoveResolveConditionType` (`MoveEnums.cs:197-221`) has `StageAtLeastOtherStage`, which evaluates `left >= right` and ignores `threshold` (`MoveResolveProfile.cs:645-651`).
  - The only strict comparison is `SpeedHigherThanOther`, and it works on Speed only.
  - So `Self:StageAtLeastOtherStage:1:...` would silently still mean `>=`.
- **Minimal engine change (recommended).**
  - Append `StageHigherThanOtherStage` at the **end** of `MoveResolveConditionType`. Appending keeps the serialized int values of existing assets stable.
  - Add one evaluator case beside line 645: `left > right`, with the same failure reason format.
  - **No importer change.** `TryParseResolveCondition` parses the type with `Enum.TryParse(..., ignoreCase: true)`, and `CombatCsvRoundTrip` writes the enum name.
  - Add the new token to the `ResolveConditions` vocabulary in `_LEGEND.md`.
  - Size: about 10 lines plus 2 tests (0 v 0 fails, Def +1 v Atk 0 passes).
- **Alternative (not recommended).** Make `StageAtLeastOtherStage` honor `threshold` as an offset (`left >= right + threshold`). Rallying Slam and Interest Strike both author 0, so nothing changes today, but it repurposes a field that today reads as "ignored". That is a silent-semantics trap.
- **Per-hit AP gain is already supported.**
  - `GainAP:1:100%:Self:interest_strike` with `ResolveUtilityTiming` = `AfterEachConfirmedHit` runs once per landed hit.
  - The resolve conditions are re-evaluated live for each hit (`ShouldApplyMoveEffectsAtResolve`).
  - A miss `continue`s the hit loop, so each hit rolls accuracy independently (`CombatResolver.cs:1983-2066`). Accuracy 90 therefore means 90% *per strike*.
- **Exact payload (row `interest_strike`):**

| Column | Now | Proposed |
|---|---|---|
| Accuracy | 95 | **90** |
| ResolveConditions | `Self:StageAtLeastOtherStage:0:None:MeleeDef:MeleeAtk` | **`Self:StageHigherThanOtherStage:0:None:MeleeDef:MeleeAtk`** (needs the enum above) |
| UtilityEffects / ResolveUtilityTiming / ResolveRequireAll | `GainAP:1:100%:Self:interest_strike` / `AfterEachConfirmedHit` / `true` | unchanged |
| Description | `2-3 hits; each can grant AP if Def stage is at least Atk.` | **`2-3 hits; each hit gives +1 AP if your Def stage exceeds your Atk.`** (66 characters) |

- **Effect on the numbers.**
  - Field index goes from 42.75 to 40.50 (18 × 0.90 × 2.5).
  - On a fresh 0/0 Chainling the refund is 0, down from about +2.4 AP today.
  - With Def +1 over Atk the expected refund is 2.25 AP per 2 AP cast, which is net positive only after setup. That matches "reward the Def build".
- **Caveat to decide.** A hit fully absorbed by a barrier (0 damage) still counts as a confirmed hit and pays the AP (`CombatResolver.cs` ~2192). See question D2.

### B. Every 4 to 6 AP damaging move compared with 2 AP typed Commons

**Index method.** Field index = power × accuracy × average hits. Spread moves show per target and ×2 when both foes are up. The cast count is matched to equal AP: two 2 AP casts against 4 AP, and 2.5 casts against 5 AP. There are no 6 AP damaging rows; All-In reaches 6+ only through `SpendRemainingAp`.

**The 2 AP typed Commons that exist:**

| Move | Index | Rider |
|---|---|---|
| Coordinated Shot (Node) | 60.80 | +15% per buffed partner, **live**; ≤69.92 in 2v2 |
| Cipher Shot (Encrypted) | 60.80 | 25% buff-block **does not run** (empty payload) |
| Precision Strike (Compressed) | 60.80 | +20% accuracy **does not run** |
| Blade Flurry (Bot) | 51.30 | none |
| Zipped Strikes (Compressed) | 49.40 | 25% self Speed +1, live |
| Stone Barrage (Block) | 38.00 | live |
| Chain Fork (Forked) | 34.20 | none |
| Frost Shards (Frozen) | 34.20 | live |

Gas, Mined, Meme, Shard and Burned have no 2 AP Common hit. "Two typed Commons" therefore means 121.6 for the three spine Commons, and 102.6 to 68.4 for the multi-hit Commons.

**Reference targets.** `MOVE_COST_CURVE_ANALYSIS.md` uses a deliberately *concave* curve: typed Common single-target targets of 60.8 at 2 AP, 72 at 3, 82 at 4, 95 at 5 and 108 at 6. Rarity multipliers are Uncommon ×1.05 and Rare ×1.12, and spread is ×1.15. On that curve a 4 AP single is *meant* to deal less than two 2 AP spines (82 vs 121.6). The surplus pays for doing it in one action.

**Rider status.**
- All riders listed as live run in the engine: Ember `StageChanges`, Automation `RandomStageChange` per hit, Hammer `ConditionalDamage`, and Cryo `StageChanges`.
- The recoil, miss-cost, Defense-ignore and Shard Evasion clauses were deleted from the descriptions by this plan and never ran.

**Classes.** (a) the 2 AP move is misplaced; (b) the heavy move loses only because of uncounted or unimplemented effects; (c) both; **(d) neither**, meaning it loses because the plan raised AP and kept power ("Power on the heavy hits stays", plan line 3; "Do not apply the power retune in MOVE_COST_CURVE_ANALYSIS.md", line 31).

| Move (type, rarity) | AP | Power / Acc / Hits / Target | Index | Riders (runs?) | Loses to (2 AP Common ×2) | Curve target | Class | Recommendation |
|---|---:|---|---:|---|---|---:|---|---|
| All-In (Meme, Epic) | 4+ (spends all) | 98 / 95 / 1 / single | 93.10 | SpendRemainingAp ✔, self-lock rc 2 ✔; recoil and KO +1 AP deleted, never ran | No Meme Common. Cross-type spine ×2 = 121.6 | 82 at 4 AP; 108 at 6 AP | **d** (+ spend-all is an uncounted *cost*) | Either drop `SpendRemainingAp` (93.1 is +14% over the 4 AP center, which is fine for a one-action burst), or keep spend-all and pay it: about power 110 (index 104.5) |
| Fork Split (Forked, Legendary) | 5 | 46 / 95 / 1 / AoE | 43.70 ×2 = 87.40 | none | Chain Fork 34.2 ×2.5 = 85.5, so Fork *wins* in 2v2 and loses 1v1 (43.7). Spine ×2.5 = 152 | 3 AP spread 82.8; 5 AP Legendary spread ≈ 122 | **d** | Return to 3 AP (the cost-curve note's call), or power ≈ 64 at 5 AP (121.6 total) |
| Shard Scatter (Shard, Legendary) | 5 | 18 / 95 / 2–3 / AoE | 42.75 ×2 = 85.50 | Evasion rider deleted, never ran | No Shard Common. Spine ×2.5 = 152 | as above | **b + d** | 3 AP, or power ≈ 26 at 5 AP (123.5). If Evasion is implemented later, trim power then |
| Recoil Strike (Forked, Uncommon) | 4 | 76 / 95 / 1 / single | 72.20 | recoil deleted, never ran. It was a *drawback*, so implementing it widens the gap | Chain Fork ×2 = 68.4, so Recoil *wins*. Spine ×2 = 121.6 | 86.1 | **d** | Pick one tax: 2 AP + 15% recoil at power 76 (cost-curve note), or 4 AP with no recoil at power ≈ 91 |
| Volatile Shot (Gas, Uncommon) | 4 | 72 / 95 / 1 / single | 68.40 | miss-cost deleted, never ran (drawback, ≈1 index point at 95%) | No Gas Common. Spine ×2 = 121.6 | 86.1 | **d** | Same choice: 2 AP + miss cost (power 66–72), or 4 AP at power ≈ 91 |
| Deep Dig (Mined, Uncommon) | 4 | 72 / 95 / 1 / single | 68.40 | ignore 50% Def stages deleted, never ran (upside only against +Def) | No Mined Common. Spine ×2 = 121.6 | 86.1 | **b (small) + d** | 3 AP (index ≈ 72 center), or power ≈ 91 at 4 AP. The Defense-ignore can come later as a real flag |
| Ember Shower (Burned, Uncommon) | 4 | 38 / 95 / 1 / AoE | 36.10 ×2 = 72.20 | 25% Atk −1 each ✔ | No Burned Common. Spine ×2 = 121.6 | 4 AP Uncommon spread ≈ 99 | **d** | 3 AP, or power ≈ 52 at 4 AP (98.8 total) |
| Automation Burst (Bot, Uncommon) | 4 | 38 / 95 / 1 / AoE | 36.10 ×2 = 72.20 | 25% random stat −1 per target ✔ | **Blade Flurry 51.3 ×2 = 102.6** | ≈ 99 | **a + d** | 3 AP or power ≈ 52. Blade Flurry (3 guaranteed hits, 51.3) is the strongest 2 AP Common multi-hit (Zipped Strikes 49.4, the rest 34–38) and is a same-type rival: trim to 16×3 (45.6) or make it Uncommon |
| Hammer Sync (Node, Legendary) | 4 | 72 / 95 / 1 / single | 68.40, max 75.24 | +10% per buffed partner ✔, but 2v2 has only 1 partner, so the **+30% cap is unreachable** | **Coordinated Shot 60.8–69.9 ×2 = 121.6–139.8** | ≈ 92 | **c** | Coordinated Shot is a Common whose live rider (+15%) beats its Legendary sibling's (+10%). Move it to Uncommon, or cut its rider to +10%. For Hammer: 2 AP (the cost-curve note's typed-rare center), or power ≈ 82 with +25% per partner, and fix the "cap +30%" text |
| Cryo Ray (Frozen, Legendary) | 4 | 72 / 95 / 1 / single | 68.40 | 25% Speed −2 ✔ | Frost Shards ×2 = 68.4 (tie). Spine ×2 = 121.6 | ≈ 92 | **d** | 2 AP (it was the typed-rare premium there), or power ≈ 97 at 4 AP |

**Bottom line for point 3.**
- The 2 AP Commons are mostly *not* misplaced. The exceptions are Coordinated Shot (a live rider at Common that is stronger than its Legendary's) and arguably Blade Flurry.
- Cipher Shot and Precision Strike are honest bare spines whose descriptions lie. Fix the text, not the tier.
- The heavy moves also do not lose because of uncounted effects. Only Shard Scatter and Deep Dig have small unimplemented upsides.
- The deleted self-damage clauses were drawbacks, so they cannot explain the loss.
- The real cause is (d): eight of ten rows had AP raised by 1–3 with power frozen. They now sit 15–40% under even the concave cost curve.
- If "a 4 AP Legendary must not lose to two 2 AP Commons" is the rule, it needs a *linear* curve (4 AP single ≈ 121.6, so power ≈ 128). That contradicts the cost-curve note. See D1.

### C. Self-damage: why it is gone, and what it would take

**History.**
- The clauses ("take 15% MaxHP recoil", "miss costs you 15% MaxHP", All-In's "recoil on hit or miss; +1 AP on KO") date from the first commit, `3a56ccc5` (2026-09-27 06:12 CT). `git log -S` finds no later commit touching them.
- They had **never been implemented**: no payload column carries them, and no utility opcode exists.
- The removal is in the uncommitted working tree, made by the AP cost ladder plan:
  - Plan line 70: "When a row's description still promises an effect the resolver does not run, delete that clause in the same edit. **Do not add the missing opcode.**"
  - Plan line 164 (out of scope): "**Do not implement Recoil Strike's recoil, Volatile Shot's miss cost**, or a nonzero Crystal Bash power."
  - Plan line 59 (All-In): "Recoil and the KO refund stay unimplemented."
- So this was a truth-in-labeling cleanup, not a design decision against self-damage.
- **Contradiction worth fixing.** Plan line 116 still justifies Uncommon with "Their cost, and **the drawback on the ones that have one**, is the sibling tradeoff". That drawback no longer exists on any of them.

**Earlier design intent (plans folder).**
- `inscriptions_v1.2_balance_pass`: "Recoil Strike: Lower recoil to 15% (from 20%)". All-In had "on hit: 25% recoil; on miss: 50% recoil; **non-lethal (floor 1 HP)**; +1 AP refund on KO".
- `balance_check_&_spec_04`: All-In "ALL AP, 25-50% recoil … non-lethal".
- `type_profiles_matrix_and_move_rebalance`: "Gas – APMAN; **volatile self-trade**", "Forked – AoE DMG; chain split; speed burst; **recoil tradeoff**", "Meme All-In (all-AP cost + recoil)".
- So the archetypes Cronus means are already on paper: **Forked (recoil), Gas (volatile / miss cost), Meme (All-In)**.
- Docs that still say the riders are owed: `AP_ECONOMY_ANALYSIS.md` §6.1 ("Recoil Strike and Volatile Shot are still missing their HP costs … the moves stay undercosted until the riders exist") and `COMBAT_ISSUES_MASTER.md` (`MV-all_in`, `BAL-COST`).
- `MOVE_COST_CURVE_ANALYSIS.md` warns that recoil plus a higher AP cost is a "double tax".

**Engine support today.**
- `HpCostPercent` column: parsed (`Combat2DataImporter.cs:1545`), round-tripped, and parity-audited. The resolver pays it **only** for `FULL_ABSORB_WALL` support moves: it floors at 1 HP and logs a `SystemMessage` (`CombatResolver.cs:2625-2636`). Eligibility needs enough HP for that case (`CombatActionEligibilityService.cs:290-296`).
- `CombatEventType.HPRecoilDamage` already has consumers: narration composer and profile, presenter, SFX orchestrator, stats aggregator. It has **no producer** in the engine.
- `LifestealPercent` (`UtilityEffectExecutor.cs:137` → `ApplyLifesteal`) already reads `context.damageDealt` per hit. It is the exact template for "% of damage dealt".
- Self-KO precedent: `HandleReflectedAttackerKo` (`CombatResolver.cs:2474`) records the KO, arms FoeKO windows, and fires OnKO and AllyKO passives.
- No recoil or self-damage `UtilityEffectType` exists.

**Estimates (recommendations; sizes assume one engineer who knows the resolver):**

| Piece | (i) Recoil % of damage dealt | (ii) Flat %MaxHP on use (and Volatile's on-miss variant) |
|---|---|---|
| Data | New `UtilityEffectType.RecoilPercent` (append), authored `RecoilPercent:15%:recoil_strike` with `AfterEachConfirmedHit` | Reuse `HpCostPercent`. Add a tag, e.g. `HP_COST_ON_USE` / `HP_COST_ON_MISS`, instead of relaxing the `FULL_ABSORB_WALL` gate |
| Engine | Executor case + `ApplyRecoil`, a mirror of `ApplyLifesteal` (≈25 lines). Barrier-blocked or missed hits deal 0, so no recoil, which is natural | Pay on use in the damaging path before the hit loop. For on-miss, pay in the miss branch (`CombatResolver.cs` ~2040). ≈30 lines |
| KO-on-self rule | **Decide:** floor at 1 HP (the old All-In design and the barrier precedent; trivial) or lethal. Lethal means generalizing `HandleReflectedAttackerKo` into a self-KO helper plus a mutual-KO / last-mon tiebreak rule (≈0.5 day plus tests) | Same decision, plus eligibility: block the move when HP ≤ cost (barrier rule) or allow it with the floor |
| Narration | Emit `HPRecoilDamage` (OutcomeSummary by default, already wired to popups, SFX and stats): "X took N recoil." | Same event instead of today's popup-only `SystemMessage` |
| AI | `AIHelpers.EstimateDamage` ignores costs. Subtract the expected self-damage × weight in `AggressiveAI` scoring, and skip the move if the recoil would KO (when lethal) or drop below ~25% HP. ≈20 lines | Same hook, using cost × (1 − acc) for the on-miss variant |
| Tests (EditMode) | recoil = floor(dmg × pct) per hit, summed across multi-hit; miss and barrier → 0; floor or KO path; event emitted; importer + round-trip parse | pay on hit and on miss; on-miss pays only on a miss; floor/eligibility; event; round-trip |
| Total | **≈1 day** (floor-at-1) / ≈1.5 days (lethal) | **≈0.5–1 day**; shares the KO rule and AI hook with (i) |

The pair together is about 1.5–2 days, and it closes `MV-all_in` recoil, `BAL-COST` (Recoil Strike and Volatile Shot), and the "drawback" justification at plan line 116. All-In's "+1 AP on KO" is separate and small: a `GainAP` with a KO condition (`AfterEachConfirmedHit` plus a target-KO check). It needs one new condition type or a ledger lookup, ≈0.5 day.

### D. Next questions for Cronus

From his answers:

1. **Curve shape.** Should heavy hits follow the cost-curve note's *concave* targets (4 AP single ≈ 82–92, below two 2 AP spines) or a *linear* rule (a 4 AP hit ≥ two 2 AP hits, so power ≈ 128)? This one answer decides every row in table B.
2. **Interest Strike.** Should a hit fully absorbed by a barrier (0 damage) still pay +1 AP? Should Rallying Slam move to the same strict "exceeds" gate?
3. **All-In.** With the self-lock confirmed, keep `SpendRemainingAp`? If yes, should the power rise to pay for it (≈110)? Also confirm the "(2 turns)" log text fix.
4. **Self-damage KO rule.** Should recoil or HP cost be able to KO the user, or floor at 1 HP like the old All-In design?
5. **Self-damage and AP.** When a recoil move gets its self-damage back, does its AP drop back to 2–3 to avoid the double tax? Which move gets which form: Recoil Strike (% of damage), Volatile Shot (on miss), All-In (recoil)?
6. **Archetypes.** Are Forked, Gas and Meme the self-damage archetypes, as the type-profile plan says, or should others get self-trade moves as well?
7. **Coordinated Shot vs Hammer Sync.** Move Coordinated Shot to Uncommon, or cut its rider? Should Hammer Sync count partner *buffs* rather than partners, so the +30% cap is reachable?
8. **Spread rows.** Revert Fork Split and Shard Scatter to 3 AP, or raise their power at 5 AP?
9. **Manifest timing.** Re-export after these data fixes too, or only after the section-3 audit fixes?

Still open from section 7:

10. **(Q3) Balloom.** All its damage is 4 AP, so it cannot attack on turn 1. Is that acceptable, or should it get a 2 AP Meme/Gas chip? For Validape, keep Hammer Sync as its only damaging pool move?
11. **(Q7) Rarity shape.** Accept 209/63, or rebalance toward the targets? Should every typed kit keep a Common 64 spine? 8 types have it at Rare or above.
12. **(Q8)** Uplink and Kindling are Uncommon passives, but the plan floors passives at Rare. Which rule wins?
13. **(Q9)** Transaction Tax: Epic, or the "typed common" that `AP_ECONOMY` §6.2 calls it?
14. **(Q10)** Glitch Feedback cleanses a *foe* debuff. Was a buff strip intended?
15. **New.** Cipher Shot and Precision Strike describe riders that do not run. Implement them (making them Uncommon), or cut the text?
