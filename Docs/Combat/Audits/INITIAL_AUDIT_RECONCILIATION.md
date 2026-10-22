# Initial audit reconciliation (2026-10-19, CT)

Tree: the working copy of `C:\Users\mando\Downloads\UNITYGAMING\Signal Jam\Signal Jam 3D` as of 06:10 CT. The box mirror is `/workspace/lp/w`. No file under Docs/Specs, Docs/Combat, Assets/Scripts/Combat, Assets/Data/Combat or Supabase changed after 06:00. The repo was read only. `Docs/GameTheory/` was not opened.

## 1. The initial audit set (git log --diff-filter=A)

| Doc | Added in | When (CT) | Rows |
|---|---|---|---|
| Docs/Combat/Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md | d2ea6005 "Document the battle-effect audit and rarity screen" | 10-15 23:23 | E-01 to E-29 |
| Docs/Combat/Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md | d2ea6005 | 10-15 23:23 | BAL-* |
| Docs/Combat/Audits/UNIMPLEMENTED_MOVE_EFFECTS.md | 101b88d8 "Apply the battle-effect decision round" | 10-16 00:50 | MV-* + E-12/13/14/17/20/21/25 |
| Docs/Combat/Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md | 101b88d8 | 10-16 00:50 | NP-* |
| Docs/Combat/Audits/EMPTY_PAYLOAD_INVESTIGATION.md | 6ed1f407 "Trace why Extend Protocol and Network Sync have no effect payload" | 10-16 06:33 | E-19 set |
| Docs/Combat/COMBAT_ISSUES_MASTER.md | e08e6430 "Add a combat issues master…" | 10-16 07:20 | index of all the above + DEC/APR |

The 10-16 10:12 commit 76bce766 re-committed them all. `combat_species_pool_routing_inventory.md` and `resolver_seven_buckets_playtest.md` date from 09-27 and are not part of this set. The master at e08e6430 indexes every MV and E-row in the unimplemented doc. It lists 115 rows. The E-18, E-19 and E-27 group headers are not rows.

## 2. Summary counts (primary class per row)

| Source | FIXED | PARTIAL | STILL BROKEN | DOC STALE | SUPERSEDED | Total |
|---|---|---|---|---|---|---|
| BATTLE_EFFECTS_CORRECTNESS_AUDIT.md | 14 | 1 | 11 | 3 | 0 | 29 |
| UNIMPLEMENTED_MOVE_EFFECTS.md + EMPTY_PAYLOAD_INVESTIGATION.md | 26 | 0 | 17 | 11 | 4 | 58 |
| CATALOG_STATUSES_WITHOUT_PRODUCERS.md | 7 | 0 | 5 | 0 | 0 | 12 |
| BATTLE_MOVE_BALANCE_BY_RARITY.md | 0 | 1 | 1 | 1 | 3 | 6 |
| COMBAT_ISSUES_MASTER.md design/approval rows | 2 | 1 | 8 | 0 | 3 | 14 |
| Post-10-16 master rows | 8 | 2 | 0 | 0 | 0 | 10 |
| **All** | **57** | **5** | **42** | **15** | **10** | **129** |

Initial set only (119 = 115 master rows + 3 group headers + crystal_extraction): **FIXED 49, PARTIAL 3, STILL BROKEN 42 (7 of them unapproved DEC backlog, so 35 real open items), DOC STALE 15, SUPERSEDED 10.** The post-10-16 rows add FIXED 8 and PARTIAL 2.


Class rules:
- FIXED: the code or data does what the row asked, cited.
- DOC STALE: the code changed and the master row did not, or the master claims something the code does not do (marked CLAIM ≠ CODE).
- SUPERSEDED: replaced by a recorded Cronus decision.
- STILL BROKEN: includes unapproved design backlog (DEC-S*), which is not a defect.

"No behaviour test" means the payload exists but no EditMode test drives it.

## BATTLE_EFFECTS_CORRECTNESS_AUDIT.md (d2ea6005, 10-15 23:23 CT; decision round 101b88d8, 10-16 00:50 CT)

| Row | Class | Evidence (current tree) | Master now | Correct status |
|---|---|---|---|---|
| E-01 | FIXED | Moves.csv bean_sip: Epic, RechargeTurns 2, GainAP:1, "Recharges for 1 turn". Test CombatDecisionRoundTests.BeanSipLogsOneTurnAndGrantsOneAp | fixed in PR | fixed in PR (ok) |
| E-02 | DOC STALE | extend_protocol is now RechargeTurns 2, "Skips the next turn". The row still says it stays at 3. ap_boost 3 and fox_feint 999 match | fixed in PR | fixed in PR; rewrite the sentence: Extend Protocol is now 2 (one skipped turn) |
| E-03 | FIXED | Test CombatIssuesRemediationV2Tests.SlowedLabel_DoesNotPromiseAnActionSkip | implemented, unverified | fixed (test) |
| E-04 | FIXED | COMBAT_Statuses.txt:90 "each End of Turn for 3 ticks". Docs only | implemented, unverified | fixed |
| E-05 | FIXED | ChemistryResolver.cs:553 comment corrected | implemented, unverified | fixed |
| E-06 | STILL BROKEN | StatusId.cs:49 still reads "Atk/Def/Acc/Spd -1" (V2 Proposal 12 text batch) | awaiting decision | open (text-only, ready) |
| E-07 | STILL BROKEN | Statuses.csv:10 Jammed "Acc -2", StageEffects empty. StatusId.cs:21. No runtime body (only TypeChemistryMatrixSO). Decision already made: Acc -1. Not built | awaiting decision | decided (Acc -1), not implemented |
| E-08 | FIXED | Statuses.csv:6 Leaked "On action: 10% own MaxHP while the side is leaked". Test Leaked_TaxesOnlyActorsOnThatSide | implemented, unverified | fixed (test) |
| E-09 | STILL BROKEN | Statuses.csv:20 BacklashType "Contact adds typed DoT", no body | awaiting decision | awaiting decision |
| E-10 | FIXED | Statuses.csv:18 Speed:-3\|Evasion:-3. Test Dusted_OwnsClampedEvasionAndDoesNotStackOnRefresh | implemented, unverified | fixed (test) |
| E-11 | STILL BROKEN | HODLed: no producer, no immunity (Statuses.csv:36) | awaiting decision | awaiting decision |
| E-12 | STILL BROKEN | Statuses.csv:38 MEVExtracted EoTAPChange -1 (a drain). The summary and the mev_extract text say "steal" | awaiting decision | awaiting decision |
| E-13 | STILL BROKEN | BurnedTokens has no body. deflation_mark is now in no pool, so exposure is lower | awaiting decision | awaiting decision (ungranted) |
| E-14 | DOC STALE | Statuses.csv:39 Overleveraged "Shelved; not applied by any move". voltage_rush and risk_marker now use NextMoveAPSurcharge | awaiting decision | superseded by SD-SHELF (shelved, 208 reserved) |
| E-15 | FIXED | Tests in CombatDecisionRoundTests (stage expiry, Cleanse reversion) and ApCostLadderTests.GasSpikeAndFud_RestoreOwnedStagesOnExpiry | fixed in PR | fixed in PR (ok) |
| E-16 | FIXED | Moves.csv:111 description is quoted. Move_ice_wall.asset has the full description and both opcodes. Guarded by CombatCsvIntegrityTests.CombatCsvRowsHaveHeaderFieldCountAndValidEnums. It regressed 3 times before (d0c4cf67, 76bce766, d1f4fa99) | fixed in PR | fixed in PR (ok; add a regression note) |
| E-17 | STILL BROKEN | Moves.csv ash_guard / compound_guard / ice_wall MitigationWindow …:0. ChainlingInstance.cs:473 returns on 0. Live in Rattlestake (initial), Cofi, Gilarime | awaiting decision | awaiting decision (V2 Proposal 1: -1) |
| E-18 group | FIXED | All 7 Acc/Eva moves author ChangeStage Accuracy/Evasion (see MV rows). DamageCalculator.cs:296 reads stageAccuracy | (group header) | — |
| E-19 group | PARTIAL | 10 of the 13 whole cards are still empty (see MV rows). Extend Protocol and Network Sync are built | (group header) | — |
| E-20 | STILL BROKEN | streamlined_flow still has StageChanges MeleeDef:-1 and also ChangeStage MeleeDef -1. Live in Humpacked | awaiting decision | awaiting decision (V2 Proposal 6) |
| E-21 | STILL BROKEN | compression_burst has no MoveEffectsOncePerTarget, so both hits drop Def | awaiting decision | awaiting decision (V2 Proposal 8) |
| E-22 | FIXED | Modifiers.csv:104 Group Hug. Test GroupHugGrantsOneApToActiveAllies… | fixed in PR | fixed in PR (ok) |
| E-23 | DOC STALE | Modifiers.csv:27,28,86,89,90: five are "Retired". 7.2-c Setup Feed now authored (12% next ally action), no test seen | open | partial: 5 retired, Setup Feed implemented, unverified |
| E-24 | FIXED | Tests Tripwire_AppliesSlowedForOneTurn…, Tripwire_RefreshDoesNotStackSpeed, Tripwire_FrozenAssets… | implemented, unverified | fixed (test) |
| E-25 | STILL BROKEN | charged_relay unchanged (GainApIfDistinctTargetsHitAtLeast reads the user ledger) | awaiting decision | awaiting decision (V2 Proposal 10) |
| E-26 | FIXED | FUD revert tested. The row text "Reinforced still has no producer" is stale, because Reinforced was removed from Statuses.csv | fixed in PR | fixed in PR; drop the Reinforced clause |
| E-27 group | FIXED | The no-producer statuses DustStorm, ForkedChain, CompressedData, MemeSurge, NodeRelay, BridgedOut and Reinforced were removed from Statuses.csv (see NP rows) | (group header) | — |
| E-28 | STILL BROKEN | Latent. No live passive authors a non-self targetOverride | awaiting decision | defer |
| E-29 | FIXED | Test BenchRechargeTicksAndOtherStatusesPause | fixed in PR | fixed in PR (ok) |

## UNIMPLEMENTED_MOVE_EFFECTS.md + EMPTY_PAYLOAD_INVESTIGATION.md (101b88d8 10-16 00:50 CT; 6ed1f407 06:33 CT) -> master MV-rows

| Row | Class | Evidence (current tree) | Master now | Correct status |
|---|---|---|---|---|
| MV-accuracy_focus | FIXED | ChangeStage:Accuracy:2 Self. CSV-assert test AuthoredRows_MatchCsv… No behaviour test | implemented, unverified | implemented, unverified (ok) |
| MV-evasion_maneuver | FIXED | ChangeStage:Evasion:2 Self. CSV-assert test | implemented, unverified | ok |
| MV-accuracy_disrupt | FIXED | ChangeStage:Accuracy:-1 SingleEnemy | implemented, unverified | ok |
| MV-evasion_pin | FIXED | ChangeStage:Evasion:-1 | implemented, unverified | ok |
| MV-gas_cloud | FIXED | Now Epic. Acc-1/Eva-1, applied per resolved target (CombatResolver.cs:2729-2730) | implemented, unverified | ok (row text still says Common) |
| MV-dust_fees | FIXED | Now Epic. Acc-1/Eva-1 | implemented, unverified | ok (row says Common) |
| MV-optimize_aim | FIXED | ChangeStage:Accuracy:2 | implemented, unverified | ok |
| MV-chain_seal | FIXED | Speed-1 stage plus ChangeStage Evasion-1 | implemented, unverified | ok |
| MV-key_rotation | FIXED | Def+1 plus Eva+1 | implemented, unverified | ok |
| MV-zip_step | FIXED | Def-1 plus Eva+2 | implemented, unverified | ok |
| MV-pivot_line | FIXED | Now Epic. Speed+1, Acc+1, Eva+1 | implemented, unverified | ok |
| MV-precision_strike | FIXED | Common, 34 power, 100 accuracy, AccuracyTuning=SureHit. Test CombatMoveResolveProfileTests (SureHit) | implemented, unverified | fixed (test) |
| MV-shard_scatter | DOC STALE | Changed: Legendary, 4 AP, 22×2-3, text "2-3 hits against all foes". Test asserts the empty utility | awaiting decision | fixed by text cut + decision (4 AP) |
| MV-ap_drain | FIXED | DrainAP:1 plus GainAP 25%. Test ApDrain_HitsForSixteenCutsOneAndGainsOnlyOnTheHit | implemented, unverified | fixed (test) |
| MV-transaction_tax | FIXED | Now Epic. StealAP:1. No behaviour test | implemented, unverified | ok |
| MV-extend_protocol | FIXED | ExtendBuff (UtilityEffectExecutor.cs:145). Tests ExtendProtocol_* | implemented, unverified | fixed (test). Recharge is now 2, not 3 |
| MV-blockade | STILL BROKEN | UtilityEffects empty. Live in Blockwyrm. Test AuthoredRows_MatchCsvAndLeaveBlockedCardsEmpty asserts it stays empty | awaiting decision | awaiting decision (V2 Proposal 1) |
| MV-script_loop | STILL BROKEN | Empty. In no pool | awaiting decision | awaiting decision |
| MV-network_sync | FIXED | RaiseLowestStage. Tests RaiseLowestStage_RespectsTiesAndCap, Uplink_* | implemented, unverified | fixed (test) |
| MV-system_reset | STILL BROKEN | Empty, 3 AP. Now live in Keyper via the learn-pool plan | awaiting decision | awaiting decision (V2 Q8: pull) |
| MV-status_resistance | STILL BROKEN | Empty passive, Rare. In no pool | awaiting decision | awaiting decision |
| MV-isolation_tool | STILL BROKEN | Empty. Live in Crackett and Marrowhead | awaiting decision | awaiting decision (V2 Proposal 3) |
| MV-viral_surge | STILL BROKEN | Empty, Mythic. In no pool | awaiting decision | awaiting decision |
| MV-wildcard_swap | STILL BROKEN | Empty. In no pool | awaiting decision | awaiting decision |
| MV-hype_burst | STILL BROKEN | Empty. Now live in Cinislug via the learn-pool plan | awaiting decision | awaiting decision (V2 Q8: pull) |
| MV-extraction_hit | STILL BROKEN | Empty passive. In no pool | awaiting decision | awaiting decision |
| MV-all_in | FIXED | Epic, 4 AP, SpendRemainingAp tag, RechargeTurns 2. Not in Balloom. Test AllIn_SpendsRemainingApAndSkipsTheNextTurn | implemented, unverified | fixed (test) |
| MV-recoil_strike | SUPERSEDED | Hard Fork: 72 power, 4 AP, Cleanse self (SD-SHELF) | implemented, unverified | superseded by SD-SHELF |
| MV-volatile_shot | SUPERSEDED | Gas War: 72 power, 4 AP, NextMoveAPSurcharge (SD-SHELF) | implemented, unverified | superseded by SD-SHELF |
| MV-jackpot_shot | DOC STALE | RandomDamageMultipliers=2\|1\|0.5 authored. CombatResolver.cs:3480 reads it. Now Mythic, 58 power. No behaviour test | awaiting decision | implemented, unverified |
| MV-hype_train | DOC STALE | Text cut to "Heavy ranged hit." Rarity went UP to Legendary with no rider (contradicts BAL-EMPTY) | awaiting decision | fixed by text cut; rarity -> balance question |
| MV-lucky_scatter | DOC STALE | RandomStageChange 25% per hit authored. Now Common 17×1-4, 3 AP | awaiting decision | implemented, unverified |
| MV-crystal_bash | DOC STALE | CLAIM ≠ CODE. Power 0 (CSV and asset). DamageCalculator.cs:98 returns 0 when power<=0, before the Def override is read. The Def override and the ×2 are authored, but the move still deals 0. The remediation test locks Power "0". In no pool | implemented, unverified | open: power 0 still means 0 damage |
| MV-glitch_barrage | SUPERSEDED | Bit Rot: RandomStageChange -1 on the foe, 25% per hit (SD-SHELF) | implemented, unverified | superseded by SD-SHELF |
| MV-cipher_shot | FIXED | CipherLocked:2 (status 312), MoveEffectsOncePerTarget. Test CipherLockBlocksNewBuffsAndPositiveStages… | implemented, unverified | fixed (test) |
| MV-cipher_burst | DOC STALE | Text cut to "2-3 ranged hits." Now Common | awaiting decision | fixed by text cut |
| MV-ap_siphon | FIXED | DrainAP:1 50% AfterEachConfirmedHit | implemented, unverified | ok |
| MV-deep_dig | DOC STALE | Text cut to "Heavy contact hit." 4 AP | awaiting decision | fixed by text cut |
| MV-momentum_drill | FIXED | ConditionalDamage ×1.25 DebuffCountAtLeast:1. Now Epic | implemented, unverified | ok |
| MV-ore_strike | DOC STALE | Text cut to "Contact hit." | awaiting decision | fixed by text cut |
| MV-stake_strike | DOC STALE | Text cut to "Contact hit." | awaiting decision | fixed by text cut |
| MV-yield_spike | DOC STALE | Text cut to "Strong contact hit." Legendary 56, riderless, still tagged TRIG\|APMAN | awaiting decision | fixed by text cut; tags stale |
| MV-interest_strike | FIXED | GainAP per confirmed hit when Def stage > Atk stage, strict (MoveResolveProfile.cs:662 left>right). Absorbed hits count (CombatResolver.cs:2277 calls utilities on full block). Accuracy 90. Only a cost test | implemented, unverified | ok; add a behaviour test |
| MV-rallying_slam | FIXED | Same strict gate. Epic 52 | implemented, unverified | ok; no test |
| MV-pickaxe_flurry | FIXED | ChangeStage MeleeAtk +1 per hit, cap 2 | implemented, unverified | ok |
| MV-combustive_shift | DOC STALE | Text cut to "Two ranged hits." Legendary riderless | awaiting decision | fixed by text cut |
| MV-crash_routine | STILL BROKEN | The text still promises a 15% MaxHP branch. Only Atk-1 runs. In no pool | awaiting decision | awaiting decision |
| MV-glitch_feedback | FIXED | Cleanse on the foe plus NextMoveAPSurcharge. The text matches. Whether a foe-cleanse is the intent is still a question | implemented, unverified | ok |
| MV-bug_fix | STILL BROKEN | Heal 10 only. Live in Kreechank | awaiting decision | awaiting decision (V2 Proposal 5) |
| MV-dice_roll | STILL BROKEN | Heal 20 only. The text promises a random branch | awaiting decision | V2 Proposal 12 text cut |
| MV-recursive_backup | STILL BROKEN | Immediate heal only | awaiting decision | V2 Proposal 12 text cut |
| MV-data_scramble | FIXED | Both sides lose Atk/Spd, +2 AP. NOTE: APCost 0 (Moves.csv:174), so it is a free +2 AP per use. In no pool | implemented, unverified | ok; flag the 0-AP cost |
| MV-risk_spike | SUPERSEDED | Leverage Up: Surging (SD-SHELF) | implemented, unverified | superseded by SD-SHELF |
| MV-guard_stance | FIXED | Recoil clause removed. Text "Gain Def +2." | implemented, unverified | fixed |
| MV-rally_cry | STILL BROKEN | The text still promises +1 AP at turn end | awaiting decision | V2 Proposal 9 |
| MV-refinery | STILL BROKEN | The text still promises +1 AP for 2 turns | awaiting decision | V2 Proposal 9 |
| MV-double_or_nothing | STILL BROKEN | No AP-drain immunity | awaiting decision | awaiting decision |
| (new) crystal_extraction | STILL BROKEN | NEVER FLAGGED in the initial set. StatusUtility SingleEnemy, HealPercentMaxHP 5. CombatResolver.cs:2711 resolves the selected foe, :2730 runs ApplyUtilityEffects, :3299 ApplyMoveHealing(source,target), and :3719-3721 heals target.EffectiveMaxHP×5%. Result: it heals the FOE. Live in Kodiax | (no row) | add row MV-crystal_extraction: open, heals the foe |

## CATALOG_STATUSES_WITHOUT_PRODUCERS.md (101b88d8, 10-16 00:50 CT) -> NP-rows

| Row | Class | Evidence (current tree) | Master now | Correct status |
|---|---|---|---|---|
| NP-rekt | STILL BROKEN | Statuses.csv:32. No producer | awaiting decision | awaiting decision |
| NP-offline | STILL BROKEN | Statuses.csv:33. No producer | awaiting decision | awaiting decision |
| NP-glitchedmind | STILL BROKEN | Statuses.csv:34. No producer | awaiting decision | awaiting decision |
| NP-duststorm | FIXED | Removed from Statuses.csv. The enum id is kept. Test asserts it is absent | implemented, unverified | fixed (removed) |
| NP-forkedchain | FIXED | Removed from Statuses.csv | implemented, unverified | fixed (removed) |
| NP-compresseddata | FIXED | Removed | implemented, unverified | fixed (removed) |
| NP-memesurge | FIXED | Removed | implemented, unverified | fixed (removed) |
| NP-noderelay | FIXED | Removed. Test asserts it is absent | implemented, unverified | fixed (removed) |
| NP-unsealed | STILL BROKEN | Statuses.csv:53. No producer | awaiting decision | awaiting decision |
| NP-relay-status | STILL BROKEN | Statuses.csv:54. No producer | awaiting decision | awaiting decision |
| NP-bridgedout | FIXED | Removed (Bridged deprecated row stays) | implemented, unverified | fixed (removed) |
| NP-reinforced | FIXED | Removed. Test asserts it is absent. The enum is kept for the importer | implemented, unverified | fixed (removed) |

## BATTLE_MOVE_BALANCE_BY_RARITY.md (d2ea6005) -> BAL-rows

| Row | Class | Evidence (current tree) | Master now | Correct status |
|---|---|---|---|---|
| BAL-RARE | SUPERSEDED | refined_striker/shot/thrust are now Uncommon 34-42. Typed Commons are 30-40 (ladder + Commons-30-40 decision) | awaiting decision | superseded by the AP ladder |
| BAL-SPREAD | SUPERSEDED | baseline_barrage is now Uncommon 10×2-3. fork_split is Legendary at 4 AP. refined_volley/blast are Common | awaiting decision | superseded by the ladder + Fork Split 4 AP decision |
| BAL-EPIC | STILL BROKEN | bulwark_blow Epic 50/90/3 AP unchanged. V2 says do not retune this round | awaiting decision | defer |
| BAL-COST | SUPERSEDED | The AP-by-impact decision is applied | implemented, unverified | superseded (ok) |
| BAL-EMPTY | PARTIAL | glitch_barrage, lucky_scatter and interest_strike now have riders. cipher_burst is Common. hype_train went to Legendary with its rider cut, the opposite of the row | awaiting decision | partial: only hype_train remains |
| BAL-61 | DOC STALE | COMBAT_Moves.txt:52,72 still says rarity is "NOT power" and that Commons stay competitive with Epic. That contradicts the 30-40 Common decision and the ladder | awaiting decision | decided; the spec text needs an edit (V2 Proposal 12) |

## COMBAT_ISSUES_MASTER.md design/approval rows (e08e6430, 10-16 07:20 CT)

| Row | Class | Evidence (current tree) | Master now | Correct status |
|---|---|---|---|---|
| DEC-BEAM | SUPERSEDED | Grind Tax (boss-only, 6 AP). Test CofiHarnessFiresGrindTaxByTurnFive… | implemented, unverified | superseded (ok) |
| DEC-S1 | STILL BROKEN | Unapproved proposal (backlog, not a defect) | awaiting decision | backlog |
| DEC-S2 | STILL BROKEN | Unapproved (backlog) | awaiting decision | backlog |
| DEC-S3 | STILL BROKEN | Unapproved (backlog) | awaiting decision | backlog |
| DEC-S4 | STILL BROKEN | Unapproved (backlog) | awaiting decision | backlog |
| DEC-S5 | STILL BROKEN | Unapproved (backlog) | awaiting decision | backlog |
| DEC-PACK | STILL BROKEN | Unapproved (backlog) | awaiting decision | backlog |
| DEC-CD | STILL BROKEN | Unapproved (backlog) | awaiting decision | backlog |
| DEC-AI | STILL BROKEN | Unapproved. V2 Proposal 11 suggests items 1-2 | awaiting decision | awaiting decision |
| DEC-CHEM | SUPERSEDED | ×1.2/×0.8 decided. Jammed -1 decided (body not built, see E-07) | awaiting decision | superseded; points to E-07 |
| DEC-PACE | SUPERSEDED | The longer Cofi fight is kept, plus Grind Tax | implemented, unverified | superseded (ok) |
| DEC-STALE | FIXED | combat_species_pool_routing_inventory.md:7 note added | implemented, unverified | fixed |
| APR-RELAY | PARTIAL | Built as `uplink` (Moves.csv Passive Node Uncommon). Test Uplink_PaysEachChangedAllyOnce…. No species grants it | implemented, unverified | implemented, ungranted (V2 Q4) |
| APR-KIND | FIXED | `kindling` on Cinislug. Tests Kindling_MarksOnceThenBurnsBeforeRugged, Kindling_MissAndKoDoNotMark | implemented, unverified | fixed (test) |

## Post-10-16 master rows (cross-check)

| Row | Class | Evidence (current tree) | Master now | Correct status |
|---|---|---|---|---|
| SD-SHELF | FIXED | Hard Fork, Gas War, Bit Rot and Leverage Up rows. Guard Stance clause removed | implemented, unverified | ok |
| CL-312 | FIXED | StatusId.cs:80 CipherLocked=312. BuffGate.cs. Test CipherLockBlocks… | implemented, unverified | fixed (test) |
| PR9-INTEG | FIXED | StatusId.cs:78-79 SearField=310, Kindled=311. CombatDecisionRoundTests 10/10 | implemented, unverified | fixed (test) |
| ACC-TUNE | FIXED | CombatMoveResolveProfileTests 11/11 | implemented, unverified | fixed (test) |
| BOSS-DRAW | FIXED | BossNoDrawTests 4/4 | implemented, unverified | fixed (test) |
| POOL-COMMON | FIXED | CombatCsvIntegrityTests.SpeciesPoolsContainTheirInitialMovesAndACommonDamagingMove. Balloom: gas_bump initial, no all_in | implemented, unverified | fixed (test) |
| BOSS-ONLY | FIXED | pvp_party.ts:175 isBossOnlyMove. grind_tax 0 catalog hits | implemented, unverified | fixed |
| GRIND-TAX | FIXED | Moves.csv grind_tax. Cofi harness test | implemented, unverified | fixed (test) |
| SUPA-AP | PARTIAL | move_ap/move_flags/known_pool .ts are generated 10-18 22:16. AP and flags are current (Moves.csv 10-18 12:42). The gameplay-manifest.json species pools predate the 10-19 00:39 pool edit | implemented, unverified | implemented; re-export needed |
| SRV-PARITY | PARTIAL | Same stale manifest. The full EditMode suite was never completed (followups_v1_baseline_tests.md: "do not treat … as the final green light") | implemented, unverified | implemented, unverified (keep) |

## 3. Cronus's decisions vs code

| Decision | In code? | Evidence |
|---|---|---|
| AP by impact; Commons around 30-40 | Yes, mostly | Ladder test `ImportedCatalog_MatchesTheCostLadderAndRaritySpread`. Typeless Pulse Shot 26 and others sit below the band (learn-pool audit Q2) |
| Self-damage shelved; renames | Yes | recoil_strike→Hard Fork, volatile_shot→Gas War, glitch_barrage→Bit Rot, risk_spike→Leverage Up |
| Bosses never draw | Yes | BossNoDrawTests |
| All-In: spend all, no reuse next turn, not in Balloom | Yes | all_in 4 AP, SpendRemainingAp, Recharge 2. Balloom row has no all_in |
| Interest Strike: +1 AP per hit, Def strictly > Atk, absorbed hits count, 90 accuracy; Rallying Slam same gate | Yes (no behaviour test) | MoveResolveProfile.cs:662 (`left > right`). CombatResolver.cs:2277 (utilities run on a fully blocked hit). Accuracy 90 |
| Hammer Sync +20% when the partner is buffed | Yes | BuffedPartnerCountAtLeast, bonus 0.2, max 1.2 |
| Coordinated Shot Rare, 3 AP, 56 | Yes | Moves.csv |
| Fork Split and Shard Scatter 4 AP | Yes | Moves.csv |
| Precision Strike SureHit | Yes | AccuracyTuning=SureHit |
| Cipher Lock 312, 2 turns, blocks all buffs | Yes | StatusId.cs:80, cipher_shot CipherLocked:2 |
| Balloom Gas Bump | Yes | Balloom initial = gas_bump |
| Grind Tax boss-only | Yes | BOSS_ONLY tag. pvp_party.ts:175 |
| Bean Sip +1 AP, Epic | Yes | Moves.csv |
| Kindled 311, SearField 310 | Yes | StatusId.cs:78-79 |
| **Jammed Accuracy -1** | **No** | Statuses.csv:10 still says "Acc -2" with no StageEffects. No runtime body |
| **End-of-turn passives before status ticks** | **No** | CombatResolver.cs:476 runs `_statusProcessor.ProcessEndOfTurn` before :485 `CheckPassiveTriggers(EndOfTurn)` |
| ×1.2/×0.8; typeless no STAB; STAB stacking undecided | Factors yes; STAB stays undecided | DEC-CHEM. V2 Q6 |
| Rugged cap 3 proposed, undecided | Not built (correct) | — |
| Kitsauto starter-only; Humpacked waits for the river zone | Not verifiable by name | Encounter assets reference species by GUID; no test seen (learn-pool audit Q5) |
| Every pool has a Common damaging move; boss-only out of pools and candy | Yes | CombatCsvIntegrityTests, BOSS-ONLY |
| Learn pools follow learn_pool_rarity_6d7ff36f | Yes | PlayableLearnPoolsFollowTheBodyBand. See LEARN_POOL_AUDIT.md |

## 4. Plan claims vs code, regressions, stale docs

1. **MV-crystal_bash is marked "implemented, unverified" but still deals 0 damage.** Power is 0 in the CSV and the asset, and DamageCalculator.cs:98 returns 0 when power ≤ 0 before it reads the MeleeDef override. Brief V1 itself said "Power 0 must not mean 0 damage". `CombatIssuesRemediationV2Tests.AuthoredRows_MatchCsvAndLeaveBlockedCardsEmpty` asserts Power "0", so the test locks the bug in.
2. **Crystal Extraction heals the foe.** None of the 10-16 docs, the master, Brief V1 or Brief V2 flagged it. The code path is CombatResolver.cs:2711 → 2730 → 3299 → 3719.
3. **Two decided rules are not built:** Jammed −1, and end-of-turn passives before status ticks. The master and the briefs still treat both as questions.
4. **Eleven master MV rows say "awaiting decision" for cards that were already fixed** by a text cut or by implementation: shard_scatter, jackpot_shot, hype_train, lucky_scatter, cipher_burst, deep_dig, ore_strike, stake_strike, yield_spike, combustive_shift. E-14 (Overleveraged shelved) and E-23 (5 stamps retired) are also stale.
5. **E-02 is stale.** The master says Extend Protocol stays at recharge 3, but the CSV now has RechargeTurns 2.
6. **The Ice Wall comma regressed three times** (d0c4cf67, 76bce766, d1f4fa99) and is fixed now. CombatCsvIntegrityTests guards it.
7. **The learn-pool plan (status "completed") put four blank or half-working cards into live pools.** They are System Reset, Hype Burst, Bug Fix and Crystal Extraction. It also made the dead-wall Compound Guard Rattlestake's initial move. This contradicts Brief V1's "leave System Reset in no pool".
8. **combat_balance_followups_v1.plan.md still shows all 10 todos as `pending`,** although P0 and P2–P5 and P8 landed. P9 is the full EditMode run, and it was never finished: per followups_v1_baseline_tests.md the Editor needs a restart.
9. **The gameplay manifest and server tables were exported at 10-18 22:16,** which is before the 10-19 00:39 pool edit. The species pools in the manifest are therefore stale. The AP and flags are current.
10. **Blockade's emptiness is locked by a test.** `AuthoredRows_…LeaveBlockedCardsEmpty` asserts blockade has no UtilityEffects. Implementing V2 Proposal 1 must update that test.
11. **Data Scramble costs 0 AP and grants +2 AP** (Moves.csv:174), which makes it a free AP engine. It is in no pool today.
12. **Smaller stale items:** BAL-61 (the COMBAT_Moves.txt §6.1 spec contradicts the Common-band decision), E-26 (it still names Reinforced, which was removed), and the rarity words in the MV-gas_cloud, dust_fees and transaction_tax rows (now Epic).

## 5. Ice Wall and Crystal Extraction verdicts

- **Ice Wall: the quoting is intact.** Moves.csv:111 has the description in double quotes. Move_ice_wall.asset has the full description plus `MitigationWindow` (turnBudget 1, hitBudget 0) and `GainApOnDamageIfSpeedHigher`. CSV↔asset parity shows 0 diffs, and `CombatCsvIntegrityTests` checks field counts. The mitigation is still dead because ChainlingInstance.cs:473 returns when hitsRemaining == 0. Only the +1 AP clause can fire. Status: E-16 fixed; E-17 open.
- **Crystal Extraction: it heals its target, and its target is an enemy.** SingleEnemy resolves to the selected foe (CombatResolver.cs:2711). `ApplyUtilityEffects(actor, foe)` (:2730) calls `ApplyMoveHealing(source, target)` (:3299). That adds `target.EffectiveMaxHP × 5%` to `target.currentHP` (:3719-3721). No debuff counting and no removal happen. The result is a 2 AP Common in Kodiax's pool that heals the opponent 5%. This is from reading the code; it has not been run in Unity.

## 6. Master-doc status corrections (for Codex; NOT applied)

| Row | Current status | Correct status | Evidence |
|---|---|---|---|
| MV-crystal_bash | implemented, unverified | open (power 0 = 0 damage) | DamageCalculator.cs:98. Moves.csv Power 0. The remediation test asserts "0" |
| MV-crystal_extraction (new) | — | open: heals the foe; describes per-debuff heal and removal | CombatResolver.cs:2711/2730/3299/3719. Kodiax pool |
| E-07 | awaiting decision | decided (Acc −1), not implemented | Statuses.csv:10 |
| GAP / new row EOT-ORDER | — | decided (passives before ticks), not implemented | CombatResolver.cs:476 vs 485 |
| E-02 | fixed in PR | fixed in PR; Extend Protocol recharge is now 2 | Moves.csv extend_protocol RechargeTurns 2 |
| E-14 | awaiting decision | superseded (SD-SHELF; Overleveraged shelved) | Statuses.csv:39 |
| E-23 | open | partial: 5 retired, Setup Feed authored and unverified | Modifiers.csv:27,28,80,86,89,90 |
| E-26 | fixed in PR | fixed in PR; drop the "Reinforced" clause | Statuses.csv (row removed) |
| MV-shard_scatter | awaiting decision | fixed (text cut; 4 AP decision) | Moves.csv; remediation test |
| MV-jackpot_shot | awaiting decision | implemented, unverified | RandomDamageMultipliers 2\|1\|0.5. CombatResolver.cs:3480 |
| MV-lucky_scatter | awaiting decision | implemented, unverified | RandomStageChange 25% |
| MV-hype_train | awaiting decision | fixed (text cut); rarity question remains | "Heavy ranged hit." Legendary |
| MV-cipher_burst | awaiting decision | fixed (text cut) | "2-3 ranged hits." Common |
| MV-deep_dig / ore_strike / stake_strike / yield_spike / combustive_shift | awaiting decision | fixed (text cut) | Moves.csv descriptions |
| MV-system_reset | awaiting decision | awaiting decision; LIVE in Keyper | SpeciesMovePools.csv |
| MV-hype_burst | awaiting decision | awaiting decision; LIVE in Cinislug | SpeciesMovePools.csv |
| MV-bug_fix | awaiting decision | awaiting decision; LIVE in Kreechank | SpeciesMovePools.csv |
| E-17 | awaiting decision | awaiting decision; Compound Guard is Rattlestake's initial move | SpeciesMovePools.csv |
| MV-extend_protocol / network_sync / precision_strike / ap_drain / all_in / cipher_shot | implemented, unverified | fixed (tested) | Tests named in §2 |
| E-03 / E-08 / E-10 / E-24 | implemented, unverified | fixed (tested) | CombatIssuesRemediationV2Tests |
| NP-duststorm / forkedchain / compresseddata / memesurge / noderelay / bridgedout / reinforced | implemented, unverified | fixed (removed from catalog) | Statuses.csv |
| BAL-RARE, BAL-SPREAD | awaiting decision | superseded (AP ladder) | Moves.csv rarities and costs |
| BAL-EMPTY | awaiting decision | partial (only hype_train left) | Moves.csv |
| BAL-61 | awaiting decision | decided; spec text edit pending | COMBAT_Moves.txt:52,72 |
| DEC-CHEM | awaiting decision | superseded (×1.2/×0.8 kept; see E-07) | Decision list |
| APR-RELAY | implemented, unverified | implemented, ungranted (uplink in no pool) | SpeciesMovePools.csv |
| SUPA-AP / SRV-PARITY | implemented, unverified | keep; re-export needed after the 10-19 pool edit; full suite unfinished | manifest 10-18 22:16 vs pools 10-19 00:39 |
| Doc: combat_balance_followups_v1.plan.md todos | pending | P0, P2-P5, P8 done; P1/P6/P7 partial; P9 not done | followups_v1_baseline_tests.md |

## 7. Remaining open questions (merged; ordered by impact)

Dropped because they are answered: AP philosophy, self-damage, boss draws, All-In, Interest/Rallying gate, Hammer Sync, Coordinated Shot, Fork/Shard cost, SureHit, Cipher Lock form (types Q11), Balloom opener, Grind Tax, Bean Sip, 310/311, Jammed value (types Q2), EoT order (types Q1), Drip once (types Q15/E-04), ×1.2/×0.8, learn-pool rules.

1. **Blank and half-working cards in live pools: pull them or wire them?** These are System Reset (Keyper), Hype Burst (Cinislug), Blockade (Blockwyrm), Isolation Tool (Crackett, Marrowhead), Bug Fix (Kreechank) and Crystal Extraction (Kodiax). *Why it matters:* players can click them today and nothing happens, or the foe gets healed. *Options:* pull them from pools, wire them (V2 Proposals 1/3/5), or cut the text to match. *Default:* wire Blockade (−1 window), Isolation Tool (Anchored) and Bug Fix (Cleanse + Staggered), since those designs are done. Pull System Reset and Hype Burst (V2 Q8). Fix Crystal Extraction as below.
2. **What should Crystal Extraction do?** *Why:* it heals the enemy. *Options:* (a) heal the user 5% per foe debuff, then cleanse those debuffs off the foe, which needs a new opcode; (b) cut it to "Heal yourself 5%" by retargeting to Self and losing the foe angle; (c) pull it from Kodiax. *Default:* (c) now, then (a) later as part of the control pass.
3. **Wall hit budget (E-17): use −1 (unlimited for the turn window)?** *Why:* Rattlestake starts with a dead wall, and Gilarime and Cofi carry dead walls too. *Options:* −1, a fixed hit count (1–3), or treat 0 as a default the way FullAbsorbBarrier does. *Default:* −1 (V2 Proposal 1).
4. **Crystal Bash: what should power 0 mean?** *Why:* it is an Epic card that does 0 damage, and the master calls it implemented. *Options:* give it a real power (for example 40–50) with the Def override, or pull it. *Default:* power 44, Def as the attack stat, ×2 below 30%, keep it ungranted, and change the remediation test's Power assertion.
5. **Confirm the two decided-but-unbuilt rules can go in this pass:** Jammed Acc −1 for 2 turns (owned, reverted), and EoT passives before status ticks. *Why:* eight chemistry cells do nothing today, and the written rule differs from the code. *Default:* yes to both (V2 Proposals 4 and 7). No question is needed unless Cronus objects.
6. **HODLed: which body, or none this round?** (E-11; V2 Q1; types Q3/Q4.) *Default:* none this round.
7. **Partial riders still on the cards:** Streamlined double Def (E-20), Compression Burst both hits (E-21), Rally Cry and Refinery AP (via Yield), Charged Relay team ledger (E-25). *Why:* Streamlined is live in Humpacked; the others are ungranted. *Default:* apply V2 Proposals 6, 8, 9 and 10.
8. **Text-only fixes:** the FUD comment, Backlash, Dice Roll, Recursive Backup, Crash Routine, MEV "steal" versus drain, and Double-or-Nothing immunity. *Options:* cut the text, or implement. *Default:* cut the text (V2 Proposal 12) and put MEV's summary as "drain".
9. **Statuses with no producer** (Rekt, Offline, Glitched Mind, Unsealed, Relay, Burned Tokens, Overleveraged). *Options:* deprecate, give them a card, or leave them reserved. *Default:* leave Glitched Mind for a future card. Deprecate Rekt, Offline and Relay (Brief V1). Keep Overleveraged shelved (V2 Q2). Mark Burned Tokens "no producer".
10. **Uplink is built but no species has it** (APR-RELAY; V2 Q4). *Default:* add it to Validape as an equip-able passive, keeping the Common damaging move.
11. **Rarity of riderless high cards:** Hype Train (Legendary), Yield Spike, Combustive Shift, Bulwark Blow (BAL-EPIC), and the rarity rivals. *Default:* leave them until a `rarity_rivals` pass. Clear Yield Spike's TRIG/APMAN tags.
12. **Typeless Commons under 30** (Pulse Shot 26 and others; learn-pool Q2). *Default:* raise Pulse Shot to 30 and accept the rest.
13. **Data Scramble at 0 AP for +2 AP.** *Default:* give it a cost of 1 before it enters any pool.
14. **Does STAB stack on ×1.2/×0.8?** (V2 Q6.) *Default:* keep `enableStab` off and decide later.
15. **Which Balloom five is the pool?** (V2 Q3.) Also: keep Gilarime and Validape wild in StarterGrove, and add a test that Kitsauto and Humpacked stay out (learn-pool Q5). *Default:* accept the live five in writing, and add the exclusion test.

Not questions, but needed before the next brief:
- Re-export the manifest and server tables.
- Finish the full EditMode run after a clean Editor restart.
- Mark the followups plan todos.
- Fix the nested `_shared` manifest URL.
