> **Note added after the run (2026-10-19, 5:45 AM CT):** The snapshot left out a few files that do exist in the local tree: `Assets/Scripts/Editor/Tests/Combat/ManifestParityTests.cs`, `Assets/Scripts/Editor/Tests/Combat/KnownPoolGenerationTests.cs`, `backend/Chainbound.BattleService/manifests/gameplay-manifest.json`, and both copies of `pvp_party.ts`. So the "Parity" findings about missing files, and proposal 13, are snapshot gaps, not real defects. Re-check them against the real tree.

# Combat issues decision brief v2

Source of truth: snapshot `chainbound_combat_snapshot_20261019_0522` (19 Oct 2026, 05:22 CDT), branch `wip/combat-local-2026-10-18` plus uncommitted work. The previous brief is the 18 Oct 2026 20:05 analysis. Where that brief and this tree disagree, this tree wins. `Docs/GameTheory/` was not read.

`issues_brief_check_v2.py` re-checks the numeric and structural claims below against the unzipped snapshot.

**Checker: 298 PASS, 0 FAIL.**

Index, when a number is given, is `power × accuracy × average hits`. An all-foes total multiplies that by 2.

## Headline

| | Count | What it is |
|---|---:|---|
| Rows re-walked | 67 | 57 awaiting + E-23 open, plus the 7 closes follow-ups moved to `implemented, unverified`, plus GAP-HOPOCH and GAP-TICK |
| Settled | 20 | Previous Close rows. The live card matches the saved behavior |
| Changed | 7 | Facts moved. 5 of these are also still broken |
| Still broken | 26 | The card, or the saved rule, still does not run. Includes those 5 |
| Text still stale | 7 | Runtime already matches the recommended cut. The sentence was not edited |
| Unchanged defer | 12 | Same "do not build" as the previous brief |
| New finds | 32 | 6 regressions, 6 interactions, 8 balance outliers, 7 plan gaps, 5 parity holes |
| Proposals | 13 | Ranked, with values and a named test |
| Questions | 9 | Each has a suggested default |

The 67 partition as 20 settled + 2 changed-and-done (E-14, GAP-HOPOCH) + 21 still-broken-and-unchanged + 5 still-broken-and-changed + 7 text + 12 defer.

Follow-ups v1 did land the shelf, Cipher Lock, SureHit, the strict Def-over-Atk gate, Hammer Sync at +20%, the boss no-draw, and Grind Tax. It did not land Batch C or Batch D from the previous brief. The plan file's own todos are still `pending`.

## What follow-ups v1 settled

These match `_plans/combat_balance_followups_v1.plan.md` and the live CSV or resolver.

| Plan | Live result | Cite |
|---|---|---|
| P3 shelf | Hard Fork cleanses 1 debuff. Gas War surcharges the next move +1 AP. Bit Rot drops a random foe stat at 25% per hit. Leverage Up is Atk +2, Def −1, Surging. Voltage Rush and Risk Marker use `NextMoveAPSurcharge`. Guard Stance text is "Gain Def +2." Overleveraged summary is "Shelved; not applied by any move." | `Moves.csv` `recoil_strike`, `volatile_shot`, `glitch_barrage`, `risk_spike`, `voltage_rush`, `risk_marker`, `guard_stance`; `Statuses.csv` Overleveraged |
| P2.4 SureHit | Precision Strike power 34, accuracy 100, `AccuracyTuning: SureHit`. Importer sets ignore-evasion 6 and a 95% floor. `forceMiss` still returns 0 | `Combat2DataImporter.cs:1943` |
| P2.5 Cipher Lock | `CipherLocked = 312`. Cipher Shot applies it for 2 turns, once per target. `BuffGate` zeroes positive stages and blocks buff applies. Swap-out removes it | `StatusId.cs:80`; `Moves.csv` `cipher_shot`; `BuffGate.cs:9` |
| P0.0 ids | `SearField = 310`, `Kindled = 311`, `CipherLocked = 312` | `StatusId.cs:78-80` |
| P2.1–P2.2 gate | Interest Strike and Rallying Slam use `StageHigherThanOtherStage` (strict `>`). Interest Strike accuracy is 90, power 18, Epic, +1 AP per confirmed hit. Rallying Slam power 52, Epic | `Moves.csv` `interest_strike`, `rallying_slam`; `MoveResolveProfile.cs` strict compare |
| P2.7 Hammer | Power 80, 4 AP, +0.20 per buffed partner, cap 1.20. The count skips the user | `Moves.csv` `hammer_sync` |
| P2.6 no draw | Boss mutual wipe uses KO order. A missing or tied order sets `winnerTeamIndex` to 1. Non-boss mutual wipe stays −2. The player is team 0 and the encounter is team 1, so a tie is a player loss | `CombatResolver.cs:7499-7517`; `CombatBattleController.cs:485-511`; `BossNoDrawTests.cs:42` |
| P2.8–P2.10, P5.3 Grind Tax | Power 30, 3 hits, 6 AP, recharge 2, tags `NO_CANDY\|BOSS_ONLY`, Speed −1 with `MoveEffectsOncePerTarget`. Encounter kit is Bean Sip, Bean Beam, Grind Tax, difficulty Boss. Species pools do not list it. Utility AI adds +60 when the actor can pay the effective cost | `Moves.csv` `grind_tax`; `CofiLootwak_Encounter.asset:39-64`; `UtilityStatusAI.cs:135` |
| P4 curve | Coordinated Shot is Rare, power 56, 3 AP, +15% cap 1.15. Fork Split is 4 AP, power 56. Shard Scatter is 4 AP, power 22. Typed commons in the 2 AP band sit near 34. All-In stays power 98 and `SpendRemainingAp` | `Moves.csv` |
| P5 common attack | Every species row has a Common damaging move in learnable or exclusive. Balloom's initial move is Gas Bump. All-In is in no pool | `SpeciesMovePools.csv` |
| P6 pieces that did land | Effective cost floors a positive AP cost at 1. Cleanse reverts FUD's owned stages. Thread Lock resets at battle start. Boss AI scores `SpendRemainingAp` as current AP | `CombatActionEligibilityService.cs:198`; `CombatDecisionRoundTests.CleanseRevertsOwnedDebuffStages` |
| P1 / P8 tables that are in the tree | `move_ap.ts` includes `grind_tax: 6`. `move_flags.generated.ts` lists only `grind_tax`. Known pool powers match the plan's generated column (striker 30, jab 16, shot 26, barrage 10, multi-strike 8). Event grant is `bean_sip` only | `Supabase/supabase/functions/_shared/` |

Kindled's 6% still runs before the other end-step damage, including Rugged, inside `StatusProcessor.ProcessEndOfTurn` (`StatusProcessor.cs:87-94`). That ordering inside the status pass is the approved exception. The pass itself is still after the problem in GAP-TICK.

## What changed since the last brief

Previous table: 64 awaiting, 1 open, 39 implemented-unverified, 7 fixed in PR, 7 fixed in follow-up v1. This master: **125 rows, 57 awaiting, 1 open, 60 implemented-unverified, 7 fixed in PR.**

| Bucket | Count | Rows |
|---|---:|---|
| Resolved | 20 | Every previous Close. Behavior holds. See the settled table |
| Changed | 7 | E-14 shelf detach; GAP-HOPOCH satisfied by Snap Strike; System Reset, Hype Burst, and Bug Fix are now granted; Extraction Hit left Kodiax; Deflation Mark left Cinislug |
| New | 32 | Section "New finds" |
| Unchanged | 40 | 21 runtime defects that read the same as last time, 7 stale sentences, 12 defers |

Seven closes moved to `implemented, unverified`: MV-precision_strike, MV-cipher_shot, MV-glitch_barrage, MV-risk_spike, MV-guard_stance, DEC-BEAM, DEC-PACE. The other 13 closes are still `awaiting decision` even though the card text and the numbers already match the close. Ten new rows were added, all `implemented, unverified`: SD-SHELF, CL-312, PR9-INTEG, ACC-TUNE, BOSS-DRAW, POOL-COMMON, SUPA-AP, BOSS-ONLY, GRIND-TAX, SRV-PARITY.

Unverified grew by 21 (39 to 60). The 7 flips plus the 10 new rows explain 17. The published counts do not name the other 4.

## Re-walk

Verdicts: **Settled** means do not rebuild the old sentence. **Changed** means a fact the previous brief used has moved. **Still broken** means the runtime effect is missing. **Text** means edit a sentence. **Defer** means leave it.

### Settled closes (20)

The master summary is often the old defect. The CSV is the card that runs.

| ID | Master now | Live card | Verdict |
|---|---|---|---|
| MV-precision_strike | implemented, unverified | Power 34, accuracy 100, `SureHit` | Settled |
| MV-cipher_shot | implemented, unverified | Uncommon, power 40, 2 AP, `CipherLocked:2`, once per target | Settled |
| MV-jackpot_shot | awaiting, summary still says power 64 and no branch | Mythic, power 58, `RandomDamageMultipliers 2\|1\|0.5`. `CombatResolver.cs:3480` rolls them with inclusive bounds, so all three options roll | Settled. Master text is stale |
| MV-lucky_scatter | awaiting, summary still says Rare and the buff does not run | Common, power 17, 3 AP, 1–4 hits, `RandomStageChange +1` at 25% after each confirmed hit | Settled |
| MV-glitch_barrage | implemented, unverified | Display name Bit Rot. Mythic, power 18, 3 AP, 2–4 hits, 25% random foe stat drop | Settled |
| MV-risk_spike | implemented, unverified | Display name Leverage Up. Atk +2, Def −1, Surging | Settled |
| MV-guard_stance | implemented, unverified | Description "Gain Def +2." | Settled |
| MV-cipher_burst | awaiting, summary still says Rare power 22 and a buff erase | Common, power 17, 3 AP, description "2-3 ranged hits." | Settled |
| MV-deep_dig | awaiting, summary still says 3 AP and a Def-ignore | Uncommon, power 72, 4 AP, "Heavy contact hit." | Settled |
| MV-shard_scatter | awaiting, summary still says power 18 and a per-hit Evasion drop | Legendary, power 22, 4 AP, "2-3 hits against all foes." Index about 52.3 per foe, 104.5 total | Settled |
| MV-ore_strike | awaiting, summary still says power 64 and +10% per buff | Rare, power 52, 2 AP, "Contact hit." | Settled |
| MV-stake_strike | awaiting, same stale power-64 Def rider | Rare, power 52, 2 AP, "Contact hit." | Settled |
| MV-yield_spike | awaiting, summary still says power 64, 3 AP, and an extra spend | Legendary, power 56, 2 AP, "Strong contact hit." | Settled |
| MV-hype_train | awaiting, summary still says Rare and index 49.40 losing to a power-64 Common | Legendary, power 52, 2 AP, "Heavy ranged hit." Index 49.4, tied with Solid Strike | Settled as a text cut. The tie is a balance note, not a reason to rebuild the ramp |
| MV-combustive_shift | awaiting, summary still says Rare and a missing-AP rider | Legendary, power 22, 2 hits, 2 AP, "Two ranged hits." Index 41.8 | Settled as a text cut |
| DEC-BEAM | implemented, unverified | Grind Tax is the 6 AP payoff. Per-foe index 85.5 versus Bean Beam 11.4, 7.5×. Espresso Shot, Double Bean, and Cream Top are not in the tree | Settled |
| DEC-PACE | implemented, unverified | The remaining sip-until-6 pattern is the Utility AI. +60 applies only when Grind Tax is already affordable | Settled as "not a new move" |
| BAL-RARE | awaiting, summary still compares Impact Knuckle, Vector Shot, and Rupture Point to a power-64 Rare | Those three are Uncommon at indexes 37.8, 34.2, and 30.6 | Settled. Master text is stale |
| BAL-SPREAD | awaiting, summary still says Common Pellet Storm above Rare Scatterburst, and Common Fork Split | Pellet Storm is Uncommon. Scatterburst is Common at 39.6 total. Fork Split is Legendary at 4 AP, index 106.4 total | Settled |
| BAL-EMPTY | awaiting, summary still says those five are Rare on the card and Common in the resolver | Hype Train Legendary, Bit Rot Mythic, Interest Strike Epic with a live gate, Lucky Scatter and Cipher Burst Common | Settled |

### Changed (7)

| ID | Previous brief | This tree | Verdict |
|---|---|---|---|
| E-14 | Defer a new Overleveraged body. Voltage Rush and Risk Marker should already be detached | They are detached. Status 208 remains, summary "Shelved; not applied by any move." No C# handler reads `OVERLEVERAGED_RECOIL` (0.25). Master still says both moves apply it | Changed, and the shelf is what runs. The types plan wants a different body. See the conflict section |
| GAP-HOPOCH | Add Pulse Shot (`baseline_shot`, power 26) to Hopoch's offtype, beside Deep Reap | Hopoch's learnable five includes Snap Strike (`baseline_striker`, power 30, 2 AP). Offtype is still `deep_reap`. `yield_cycle` is not in the pool. The "every pool has a Common damaging move" rule holds | Changed. The specific Pulse Shot offtype was not added. Do not add a sixth move |
| MV-system_reset | "In no pool." Leave it empty | Keyper's learnable five includes `system_reset`. AP cost is 3. Utility and status lists are empty. The strip and the self-skip do not run | Changed and still broken. A blank 3 AP click is live |
| MV-hype_burst | Ungranted. Build a 2-buff strip and Atk +1 per buff removed | Cinislug's learnable five includes `hype_burst`. Payload is empty. Description still promises the strip | Changed and still broken |
| MV-bug_fix | Ungranted. Add Cleanse and Staggered, and change the card's −20% to Staggered's −10% | Kreechank's learnable five includes `bug_fix`. Heal 10% runs (`HealPercentMaxHP: 10`). Cleanse and Staggered are absent. Description still says "cleanse 1 debuff; deal -20% damage next turn." | Changed and still broken |
| MV-extraction_hit | "Stays in Kodiax's pool." On a hit at Atk +3, grant Speed +1 once per turn, and do not also raise Atk | `extraction_hit` is in no pool column. Payload is empty. The only on-hit Atk proc in the resolver is `on_hit_momentum` (`CombatResolver.cs:4448`) | Changed and still broken if something equips it. Kodiax's five is Ore Strike, Deep Dig, Pickaxe Flurry, Crystal Extraction, Snap Slam |
| E-13 | Deflation Mark is in Cinislug's pool beside Kindling, and the heal does nothing | Cinislug's five is Ember Wall, Kindling, Cinder Burst, Hype Burst, Pulse Shot. `deflation_mark` is in no pool. The move row still applies `BurnedTokens` plus Atk −1. No engine read of Burned Tokens heals the applier | Changed and still broken on the card. The live Cinislug click is Hype Burst, which is also empty |

Balloom is the same kind of fact change and is not a master row. Previous brief and plan P5.1 both named Gas Bump, Gas War, Hard Fork, Fuel Surge, and Gas Cloud. The live learnable row is `fuel_surge|volatile_shot|gas_bump|slipstream|baseline_slam`. Initial is Gas Bump. Offtype is Hard Fork (`recoil_strike`). Flagship is Fuel Surge. Gas Cloud and All-In are absent. Turn 1 still has a Common Gas hit. Gas Cloud's Acc/Eva drop is not on this species.

Uplink is the other pool fact. Plan P5.2 said remove it from Validape. No species row contains `uplink`. The master still says Uplink is in the pools (`APR-RELAY`, and the approved-proposals intro). The resolver pay still exists (`SettleUplink`). The approved passive has no species grant.

### Still broken (26)

Citations are the place that shows the miss.

| ID | What still happens | Cite |
|---|---|---|
| E-17 | Ash Guard, Compound Guard, and Ice Wall author `hitBudget` 0. `AddMitigationSource` returns immediately. Ice Wall's +1 AP on a faster hit can still run. Ash Guard is in no pool. Ice Wall is in Gilarime. Compound Guard is in Rattlestake and Cofi's species pool | `Moves.csv` `MitigationWindow:…:0:`; `ChainlingInstance.cs:473` |
| MV-blockade | Uncommon, Block, 3 AP, ally, in Blockwyrm's five. Description promises 25% for 2 turns. Utility list is empty, so it is a blank click, not only a zero budget | `Moves.csv` `blockade`; `SpeciesMovePools.csv` blockwyrm |
| E-20 | Above 70% HP, Streamlined Flow writes Def −1 from `StageChanges` and again from `ChangeStage`. The card says Def falls by 1. The new AP floor (`GetEffectiveApCost` at least 1) is a different fix and it did land | `Moves.csv` `streamlined_flow`; `CombatActionEligibilityService.cs:198` |
| E-21 | Compression Burst says the first hit lowers Def. `MoveEffectsOncePerTarget` is false, so both hits drop Def. The flag works for Cipher Shot and Grind Tax | `Moves.csv` `compression_burst` |
| E-07 | Jammed's eight chemistry cells apply a token. `StatusDef` stage list is empty. Summary and the enum comment say Acc −2. The saved rule is Acc −1. Nothing writes Accuracy | `Statuses.csv` Jammed; `StatusId.cs:21` |
| E-11 | HODLed applies no Def on the apply. Each end step writes +1 from the repeating stage and another +1 while Def is below +3. From 0 the first end step reaches +2, the second +3. The summary still says "Immune to stat drops." No stage write checks HODLed. `CombatDecisionRoundTests` asserts the +2 | `StatusProcessor.cs` HODLed block; `CombatDecisionRoundTests.cs:190-195` |
| E-12 | MEV Extracted's end step drains 1 AP from the bearer. The comment on that branch says "steal." The applier does not gain the point. The move is in no current pool. Kitsauto's live pressure button is Packet Skim, an immediate drain | `StatusProcessor.cs:194-201`; `Moves.csv` `mev_extract`, `packet_skim` |
| E-25 | Charged Relay reads `CountDistinctTargetsHitThisTurn(source.instanceId)`. A turn whose only action is the utility still sees 0. Heal 5% and the +2 AP / threshold 2 payload are real | `Moves.csv` `charged_relay`; `UtilityEffectExecutor` distinct-target helper |
| E-28 | Both arms of the passive status ternary assign the owner | `CombatResolver.cs:4389` |
| MV-isolation_tool | Common, 1 AP, in Crackett and Marrowhead. Description says the next move is single-target. Payload is empty. `ApplyStatus` rejects a ChemistryOnly origin, so a CSV `Anchored` token would not land. Anchored's consume-on-spread behavior already exists (`CombatResolver.cs:1980`) | `Moves.csv` `isolation_tool` |
| MV-script_loop | Common, 1 AP, Bot. Empty payload. `ExtendBuff` lengthens buffs. The debuff extend does not exist. Not in a pool | `Moves.csv` `script_loop` |
| MV-viral_surge | Mythic, 1 AP, Meme. Master still says Common. Empty payload. Description still promises +2 AP or −1 AP, plus a random core stage. Not in a pool | `Moves.csv` `viral_surge` |
| MV-wildcard_swap | Common, 2 AP. Master still says 1 AP. Empty payload. Not in a pool | `Moves.csv` `wildcard_swap` |
| MV-crash_routine | Legendary, 2 AP. Always applies Atk −1. The below-50% 15% MaxHP branch has no opcode. Not in a pool | `Moves.csv` `crash_routine` |
| MV-double_or_nothing | End step rolls 60% gain 1 AP, otherwise drain 1. The 40% branch does not set drain or steal immunity | `CombatResolver.cs:4507-4519`; description on `double_or_nothing` |
| MV-rally_cry | Allies' Speed +1 runs. No `Yield` on self, so the user's end-step +1 AP does not | `Moves.csv` `rally_cry` |
| MV-refinery | Def +1 stays. No `Yield`. The 2-turn end-step AP does not run | `Moves.csv` `refinery` |
| MV-recursive_backup | The 50% gate and the immediate 20% heal run. `SelectionPolicy` is `EffectOnly`, so at 50% HP or above it can be selected and spend 2 AP on nothing. The end-step heal and the duration reset are still absent | `Moves.csv` `recursive_backup` |
| MV-status_resistance | Rare passive, in no pool. Description still promises a faster General-debuff clock. No such clock exists | `Moves.csv` `status_resistance` |
| GAP-TICK | `ProcessEndOfTurnForTeam` runs `StatusProcessor.ProcessEndOfTurn` (damage, AP, then duration ticks) and only then `CheckPassiveTriggers(..., EndOfTurn)`. The saved rule is the other order. Kindled-before-Rugged stays inside the status pass | `CombatResolver.cs:475` then `:485` |
| DEC-AI | `GetLegalActions` emits one `UseMove(team, moveIndex)` per affordable move, with no target slot. `EstimateDamage` is power × atk/100 × average hits × accuracy and is commented "no chemistry." Proposals 1 and 2 from the previous brief were the recommended yes. They are not in this tree. Proposals 3–8 stay deferred | `AIHelpers.cs:33-43` and `:125-129` |

MV-system_reset, MV-hype_burst, MV-bug_fix, MV-extraction_hit, and E-13 are the other five and are in the Changed table. They count in the 26.

### Text still stale (7)

| ID | Previous rec | Live text |
|---|---|---|
| E-06 | Delete Accuracy from the FUD comment | `StatusId.cs:49` still says `Atk/Def/Acc/Spd -1`. The catalog stages are Atk, Def, and Speed. Cleanse does revert those three |
| E-09 | Summary becomes "Token. No contact damage." | `Statuses.csv` BacklashType summary is still "Contact adds typed DoT." Duration 3. No contact DoT runs |
| BAL-61 | Rewrite `COMBAT_Moves.txt` §6.1 | Lines 51–72 still say rarity is complexity and availability, and that a Common stays competitive with Epic |
| MV-dice_roll | Description becomes "Heal an ally for 20% MaxHP." | Description is still "Random support can heal ally; debuff foe; or give ally +2 AP." The 20% heal is what runs. Common, 1 AP |
| NP-rekt | Deprecate the summary. Leave the enum | `Statuses.csv` still says "25% chance to lose action." No move applies it |
| NP-offline | Same | Summary still "Sleep; wakes on damage." `PreventsAction` is true. No move applies it |
| NP-relay-status | Deprecate the Relay status summary | Summary still "Full barrier bypass for next hit." No move applies `StatusId.Relay` |

### Unchanged defer (12)

| ID | Why it stays deferred |
|---|---|
| E-23 | Open. Side Quest `3.2-b`, After Hours `3.2-c`, All-Stat Burn `10.1-d`, Anniversary Mark `10.2-b`, and Tournament Badge `10.2-c` still say retired. Setup Feed `7.2-c` still has the 12% next-ally-power body (`Modifiers.csv`, and `CombatResolver` multiplies `powerMultiplier` by `1 + setupFeedFraction`). Another agent owns the six. Do not edit them |
| E-28 | Already in the still-broken table. No live passive authors a non-self `targetOverride`. Fix it when one does |
| NP-glitchedmind | Mis-act at 50% exists. No producer. Previous answer was no new card |
| NP-unsealed | 50% barrier bypass exists. No producer. Same answer |
| BAL-EPIC | Bulwark Blow is Epic, power 50, accuracy 90, 3 AP, strikes with Def. Index 45. Crystal Bash is power 0 with `DamageOverrideAttackStat` MeleeDef and a ×2 below 30% HP (`DamageCalculator.cs:249` reads the override). Smoke Crystal Bash before any Epic number moves |
| DEC-S1 … DEC-S5, DEC-PACK, DEC-CD, DEC-CHEM | Not built. ×1.2 / ×0.8 is the live chemistry factor (`CombatResolverConfig_Default.asset`). Meme still has no cells. Jammed's eight cells were not moved. `enableStab: 0`, so STAB is off. Whether STAB would stack on chemistry is still undecided; this brief does not turn it on |

DEC-AI's proposals 3–8, including the boss payoff horizon, stay in this defer. The +60 term is the only AI addition, and it fires when Grind Tax is already payable.

## New finds

### Regressions

1. **Uplink has no species grant.** Plan P5.2 removed it from Validape on purpose. `APR-RELAY` and the master intro still say the passive is in the pools. A player cannot learn the approved Node passive from any row in `SpeciesMovePools.csv`.
2. **Blank clicks entered live fives.** System Reset is in Keyper. Hype Burst is in Cinislug. The previous brief recorded both as ungranted. `SpeciesMovePools.csv` is in the s6-to-s7 diff.
3. **Bug Fix is in Kreechank and only the heal runs.** The cleanse and the −20% sentence are still on the card.
4. **Balloom's five is neither plan P5.1 nor the previous brief.** Live: Fuel Surge, Gas War, Gas Bump, Slipstream, baseline slam. Hard Fork is offtype only. Gas Cloud is gone. All-In stays gone, which is the part that matches.
5. **Cinislug lost Deflation Mark and Scorch Shot.** The Burned Tokens producer is ungranted. The replacement utility in the five is empty Hype Burst.
6. **`Move_bean_sip.asset` design notes still deny the payoff.** Lines 158–161 say the kit is Bean Sip plus Bean Beam and that no 6 AP threshold exists. The encounter lists Grind Tax.

### Unintended interactions

1. **The HODLed test locks the double ramp.** `CombatDecisionRoundTests.cs:193` expects Def +2 after one end step. A body fix has to change that assertion in the same edit.
2. **Cipher Lock's gate is not on every positive stage write.** Move hit stages go through `BuffGate.FilterPositiveStage` (`CombatResolver.cs:4145`). These writes do not: the passive stage loop (`:4361`), field-entry combat stages (`:3699`), `on_hit_momentum`'s Atk +1 (`:4461`), and the ally copy of self-targeted stage changes (`:5902`). A Cipher Locked target can still gain Atk from a passive proc.
3. **Jackpot's high roll outhits All-In.** Equal weight on 2 / 1 / 0.5. Base index 55.1. The double is 110.2. All-In is 93.1 and also spends the bar and locks. Plan appendix B called All-In the hardest single hit. That sentence is false on Jackpot's high roll. Chaos Strike's 1.5 / 0.75 pair has expected index 62.0 at 2 AP.
4. **Grind Tax versus HODLed was specified and not built.** Plan P5.3 says a HODLed foe is unchanged. There is no immunity and no test. The types plan's T0 calls the same gap.
5. **The MEV comment and the processor disagree.** `StatusProcessor.cs:194` says "steal." Line 201 drains the bearer.
6. **End-step order still violates the saved rule.** Status duration ticks, then EndOfTurn passives. Double-or-Nothing therefore rolls after durations have already decremented.

### Balance outliers

Same-AP, lower-rarity index at or above a higher rarity. Riders that actually run are noted so they are not treated as raw-damage ties.

| Move | Rarity, AP | Index | Against |
|---|---|---:|---|
| Cinder Burst | Common, 3 AP, 2–4 hits, power 15 | 42.8 | Above the common 30–40 band. The plan's own appendix printed 42.8 and still called the band 30–40 |
| Cipher Burst, Lucky Scatter | Common, 3 AP | 40.4 | Just over the band. Lucky Scatter's 25% stage is real. Cipher Burst's erase was cut |
| Hype Train | Legendary, 2 AP, power 52 | 49.4 | Ties Solid Strike (Rare, power 52, plus a 25% Speed drop) |
| Combustive Shift | Legendary, 2 AP, 22×2 | 41.8 | Under the Rare 49.4 spine and a step above Gas Bump's 34.2 |
| Jackpot Shot high roll | Mythic, 2 AP, power 58 | 110.2 | Above All-In's 93.1 |
| Chaos Strike expected | Mythic, 2 AP, power 58, ×1.5 or ×0.75 | 62.0 | Above Finish Burn's base 57.0 (Legendary, 3 AP) before Finish Burn's low-HP ×1.5 |
| Interest Strike | Epic, 2 AP, power 18, accuracy 90, 2–3 hits | 40.5 | Under the Rare 49.4 spine. The +1 AP per hit is the live rider, so this is the accepted shape |
| Bulwark Blow | Epic, 3 AP, power 50, accuracy 90, Def strike | 45.0 | Still the low Epic. Deferred with BAL-EPIC |

`RarityRivalReport` and `Docs/Combat/Audits/rarity_rivals.md` were not generated. Plan P4's accept line asked for that file.

### Plan items that landed differently, or only in part

The plan's YAML todos are all `status: pending` (`combat_balance_followups_v1.plan.md` front matter). The types plan's T0.1 says to confirm those todos are completed before starting. They are not flipped.

| Plan section | What the code or data does |
|---|---|
| P5.1 Balloom row `gas_bump\|volatile_shot\|recoil_strike\|fuel_surge\|gas_cloud`, flagship Gas War | Live learnable row is `fuel_surge\|volatile_shot\|gas_bump\|slipstream\|baseline_slam`, flagship Fuel Surge, offtype Hard Fork |
| P5.2 Keyper "add `baseline_shot`" once Cipher Shot is Uncommon | Keyper's Common damaging move is Cipher Burst. `baseline_shot` was not added. The pool-size rule still holds. System Reset stayed in the five |
| P5.2 "remove uplink" | Done. The master sentence that Uplink is in the pools was not updated |
| P5.3 "A HODLed foe is unchanged" | No immunity, no test |
| P2.5 route every positive stage through `BuffGate` | Move stages and several status paths are gated. The passive loop, field entry, `on_hit_momentum`, and the ally self-stage copy are not |
| P6.1 "Streamlined floor" | The 1 AP floor landed. E-20's second Def write was a different bug and is still there. The plan did not ask to delete it |
| Q10 default: build riders that already have an opcode, cut the text on the rest | Jackpot's multipliers and Lucky Scatter's random stage were built. Ore Strike, Stake Strike, Yield Spike, Hype Train, Combustive Shift, and Cipher Burst had the rider sentence removed. That matches the default |
| P9.1 full EditMode, no new failures | `followups_v1_baseline_tests.md:121` says the partial assembly run is not the final green light. One combat assembly reported 655 passed and 15 failed, and the other two assemblies did not finish |

### Parity

CSV, `Move_*.asset`, and `move_ap.ts` agree on the moves checked here: 211 rows, SureHit fields on the Precision Strike asset, `moveEffectsOncePerTarget: 1` on Cipher Shot and Grind Tax, Known-pool powers as in the plan table, and a single boss-only id.

The server proof the plan called "match now" is not re-runnable from this snapshot:

| Claim in `followups_v1_baseline_tests.md` | What this snapshot contains |
|---|---|
| Gameplay manifest SHA-256 `427d8dffa848…`, and the generated headers carry it | The headers carry that hash. `backend/Chainbound.BattleService/manifests/gameplay-manifest.json` is not in the snapshot, so the hash cannot be recomputed here |
| `ManifestParityTests` 1/1 and `KnownPoolGenerationTests` 1/1 | Neither `.cs` file is under `Assets/Scripts/Combat/Tests` |
| `Chainbound.CaptureCoreVerification` passed manifest-v3 assertions | That project is not in the snapshot. `ManifestEncounterAuthorityTests.cs` covers encounter time and IV rolls, not `grind_tax.bossOnly` or statuses 310–312 |
| Deno 120/120, including PvP boss-only rejection | `pvp_party_boss_only_test.ts` imports `./pvp_party.ts`. That module is absent. `pvp_rules.ts` does not call `isBossOnlyMove` |
| The Deno parity test reads the manifest | `move_ap_parity_test.ts` uses `../../../backend/.../gameplay-manifest.json`. From `Supabase/supabase/functions/_shared` that resolves to `Supabase/backend/...`, not the repo `backend/` folder. A copy at `Supabase/functions/_shared` would resolve correctly. This snapshot only has the nested copy |

`SRV-PARITY` is marked implemented, unverified. The generated tables that are present look aligned. The manifest, the PvP party guard, and the test sources the audit names are not in this tree, and the full suite note already refuses a green light.

### Test gaps

Plan-named tests that are missing, or that do not assert the claim:

| Promised | In this tree |
|---|---|
| `IceWallImportTests` (asset utilities, and a faster holder gains +1 AP) | No file. The asset has both utilities. Nothing asserts the +1 AP, and nothing asserts that budget 0 mitigates nothing |
| `InterestStrikeTests`, `RallyingSlamTests` | No per-hit AP test, no 0-versus-0 regression, no barrier-absorbed hit |
| `HammerSyncPartnerBuffTests` | No test that a buff on the user stays ×1.0 and a buff on the partner becomes ×1.2 |
| `CipherLockTests` as specified (swap, cleanse, seal, 1,000 hits, barrier) | One case inside `CombatDecisionRoundTests` covers a buff block. The ungated passive write above is not asserted |
| `SelfDamageShelfTests` and "no move damages its user" | No file. The shelf payloads are data. There is no EditMode proof that Hard Fork cleanses, Gas War surcharges, Bit Rot drops a stat, or Leverage Up applies Surging |
| `OncePerTargetEffectsTests` beyond −1 versus −3 | `CombatMoveResolveProfileTests` covers the stage count. Compression Burst is not the fixture, so the first-hit card is untested |
| `BossPayoffAiTests` on the imported Grind Tax asset | `ApCostLadderTests` covers a synthetic 6 AP gate and a harness timing claim. Worth re-reading against the imported asset during smoke |
| P2.8 path tests (pool import error, progression roll, candy `GrantKnown` throw, capture strip, victory grant, wild traits) | The CSV integrity test rejects a boss id in a pool. The other paths have no EditMode test in this tree |
| `RarityRivalReport` | Absent |
| P0.1 "list every test with its result" after the work | The post-change section is class-level counts plus an explicit incomplete full suite |

## Ranked proposals

Each one is a **PROPOSAL**. Values are fixed so the pass does not become a redesign. HODLed's card and Overleveraged's body are questions, not proposals.

1. **PROPOSAL — Walls use hit budget −1, and Blockade gets the same window.** Ash Guard `MitigationWindow:20%:2:-1`. Compound Guard the same, and keep Def +1. Ice Wall `MitigationWindow:25%:1:-1` and keep `GainApOnDamageIfSpeedHigher`. Blockade, stay Uncommon, 3 AP, ally: `MitigationWindow:25%:2:-1:blockade`. `ChainlingInstance.cs:449` documents `hitsRemaining: -1` as unlimited hits, and `:573` decrements only when `hitsRemaining > 0`. A budget of 0 still returns immediately at `:473`. Test `WallHitBudgetNegative_MitigatesForTheNamedTurns`: a budget of −1 is not decremented, a 20% window reduces a hit, a budget of 0 still mitigates nothing, and a faster Ice Wall holder gains +1 AP when hit.

2. **PROPOSAL — Pull the blank clicks out of live fives until they have a payload.** Remove `system_reset` from Keyper and `hype_burst` from Cinislug. Leave their move rows. Do not add a replacement that would make a six-move pool. Keyper still has Cipher Burst as its Common damaging move. Cinislug still has Cinder Burst and Pulse Shot. Test `LiveFives_DoNotContainEmptyUtilityPayloads`: every learnable or exclusive utility with a non-empty description has a non-empty `UtilityEffects`, `StatusEffects`, or `StageChanges` list.

3. **PROPOSAL — Isolation Tool applies Anchored for 1 turn.** Cost stays 1 AP, Common, Shard. On cast, call `ApplyStatus` with the chemistry-origin bypass. Do not apply Isolated. Anchored already forces the next damaging spread down to one target and is consumed. Test `IsolationTool_AppliesAnchoredOnCrackettAndMarrowhead`: after the cast, the foe's next all-foe damaging move hits one target, and a second move spreads again.

4. **PROPOSAL — Jammed is Accuracy −1 for 2 turns, owned, reverted.** Summary "Accuracy −1 for 2 turns." Eight cells, no matrix edit. The enum comment drops "−2". Test `Jammed_AppliesOwnedAccuracyMinusOneAndReverts`: Gas into Node sets Accuracy −1, refresh does not stack a second −1, expiry returns the stage to 0. Precision Strike into that Accuracy −1 still rolls at least 95%.

5. **PROPOSAL — Bug Fix matches Staggered, because Kreechank can click it.** Add `Cleanse:1:100%:Self:bug_fix` and `Staggered:1:1.0:Self`. Description: "Heal 10%, cleanse 1 debuff, and your next move deals −10%." Cost stays 1 AP, Common, Glitched. Test `BugFix_CleansesOneDebuffAndAppliesStaggered`: one General debuff is removed, the next damaging move is ×0.9, the heal is 10% of max HP.

6. **PROPOSAL — Streamlined Flow drops Def once.** On `streamlined_flow`, delete the `StageChanges` Def −1. Keep the `ChangeStage` Def −1 and the −1 AP discount, both gated by HP above 70%. The 1 AP floor stays. Test `StreamlinedFlow_DropsDefOnceAbove70`: one action above 70% HP moves Def by −1, and a 1 AP move still costs 1.

7. **PROPOSAL — End-of-turn passives run before the status duration tick.** One team pass of `EndOfTurn` passives, then the status pass. Kindled's 6% stays ahead of Rugged inside that status pass. Test `EndOfTurnPassives_RunBeforeStatusDurationTick`: a duration-1 status is still active when Double-or-Nothing rolls, and it is expired after the status pass of the same end step. Kindled damage is logged before a Rugged tick on the same fighter.

8. **PROPOSAL — Compression Burst uses the once-per-target flag.** Set `MoveEffectsOncePerTarget` true. Power stays 20, 2 hits, 3 AP, Common. Test `CompressionBurst_DropsDefOnTheFirstHitOnly`: two forced hits move Def by −1, not −2.

9. **PROPOSAL — Rally Cry and Refinery use Yield on self.** Rally Cry: `Yield:1:1.0:Self`, keep allies' Speed +1, stay 3 AP, Uncommon. Refinery: `Yield:2:1.0:Self`, keep the lasting Def +1, stay 2 AP, Rare. Yield's +1 AP already runs at end of turn for `yield_cycle`. A duration of 1 ticks once on the granting end step. Test `RallyCry_GrantsOneApToTheUserOnly` and `Refinery_GrantsApForTwoEndSteps`.

10. **PROPOSAL — Charged Relay counts distinct targets the acting team hit this turn.** Threshold stays 2, gain stays +2 AP, heal stays 5%, cost stays 2, Epic, Forked. Test `ChargedRelay_PaysAfterAnAllySpread`: the user's own hit count is 0, an ally has hit two foes, the cast grants +2 AP.

11. **PROPOSAL — AI proposals 1 and 2 only.** `GetLegalActions` emits one action per legal target slot and the scorer keeps that slot. Add `EstimateHitDamage` beside `EstimateDamage`: Atk/Def, the mean roll, average hits, accuracy, and ×1.2 / ×0.8, and it does not call the chemistry resolver. Leave proposals 3–8. Test `UtilityAi_ScoresEachLegalTargetSlot` and `EstimateHitDamage_UsesAtkDefMeanHitsAccuracyAndChemistryFactor`.

12. **PROPOSAL — Text batch, zero gameplay.** FUD's comment loses Accuracy. Backlash summary becomes "Token. No contact damage." `COMBAT_Moves.txt` §6.1 says rarity tracks power and availability, typed moves beat typeless, and a rare typeless move lines up with a typed common. Dice Roll's description becomes "Heal an ally for 20% MaxHP." Recursive Backup's description becomes "Below 50% HP, heal 20% now," and `SelectionPolicy` becomes Gate. Status Resistance's description becomes "Retired. No effect." Rekt, Offline, and the Relay status summaries become "Deprecated. No producer." Enum ids stay. Delete the Bean Sip design note that says no 6 AP move exists. Mark the 13 stale Close rows `implemented, unverified` and rewrite their summaries to the live card. Test `CatalogText_MatchesTheCutSentences` on those description and summary strings.

13. **PROPOSAL — Finish the server proof before calling SRV-PARITY smoked.** Put `gameplay-manifest.json` where the Deno test can read it, or fix the three-level URL so the nested `_shared` copy points at the repo `backend/`. Restore `pvp_party.ts` so a party that lists `grind_tax` returns `party_move_boss_only`. Have `pvp_rules.ts` call `isBossOnlyMove`. Check `ManifestParityTests` and `KnownPoolGenerationTests` back in, asserting `interest_strike` conditions, Precision Strike tuning, `grind_tax.bossOnly`, once-per-target on Grind Tax and Cipher Shot, and statuses 310, 311, and 312. Test names are those four, plus the existing `pvp_party_boss_only_test.ts`.

Do not build in this round: a recoil opcode, a hit count for the walls, Jammed at −2, a second Cipher Lock, HODLed's producer, Margin Loan, the unapproved packs, or a retune of Hype Train, Yield Spike, Combustive Shift, or Bulwark Blow.

## Conflicts with `types_chemistry_statuses_v1.plan.md`

That plan says it runs after follow-ups v1. Both sides are listed. This brief does not pick.

### HODLed

| | Previous brief and the live tree | `types_chemistry_statuses_v1.plan.md` |
|---|---|---|
| Ramp | Live: +0 on apply, then +2, +3 across end steps, because the repeating +1 and the extra block both fire. The test locks +2. Previous brief: one owned MeleeDef +1 per end step, stop at +3, delete the extra block, revert the sum | T4: +1 Def on apply and +1 each end step, cap +3, owned, delete the extra block (`types_chemistry_statuses_v1.plan.md` around the HODLed deep dive) |
| Immunity | Live summary promises it. No code filters a negative stage. Previous brief: delete "Immune to stat drops" from the summary | Real immunity for foe, chemistry, and foe-status drops. The holder's own drawbacks still land |
| Card | No producer. Previous brief: do not author one this round | **Diamond Hands**, Block, Rare, 2 AP, Self, `HODLed:3:1.0:Self`, is a proposal (Q3b), not the decided body |
| Grind Tax | Plan P5.3 wants a HODLed foe unchanged. That test was not written | T0 says that test depends on the immunity |

### Overleveraged / Margin Loan

| | Follow-ups v1, now live | Types plan T5 |
|---|---|---|
| Status 208 | Kept. Summary "Shelved; not applied by any move." No move's `StatusEffects` names it. `OVERLEVERAGED_RECOIL` is unread | Keep it. Give it a debt body. Duration 2. Not a shelved row |
| `risk_marker` | Rare, 1 AP, foe, `NextMoveAPSurcharge:1:100%:SingleEnemy` | Re-point to **Margin Loan**: Self, Rare, 0 AP, display name Margin Loan. Gain +2 AP now. Repay 2 at the end of the next turn. Shortfall becomes a next-move surcharge, max +2 |
| `voltage_rush` | Speed +3 and a 25% self surcharge. No Overleveraged | The types plan leaves that surcharge in place |
| Self-damage | Shelved on both sides. Margin Loan does not spend HP | Same constraint |

Jammed is the place the two plans agree and the tree does not. Both say Accuracy −1, owned, duration 2. The tree's summary and enum comment say −2, and the stage list is empty. Proposal 4 is that shared value. It does not decide HODLed or Margin Loan.

STAB: the types plan treats "typeless moves never get STAB" as decided, and leaves the ×1.2 / ×0.8 factors. This brief's instructions say whether STAB stacks on chemistry is still undecided. Live `enableStab` is 0. `DamageCalculator` multiplies STAB and then chemistry when the switch is on, and `ResolveAttackType` can hand a typeless move the attacker's primary type. Leave the switch off until that question is answered.

## Questions only Cronus can answer

1. **HODLed: previous-brief body, or the types-plan body, or neither this round?** Suggested default: neither this round. Leave the test-locked +2 ramp in place until you pick. Do not author Diamond Hands. Do not write the "HODLed foe is unchanged" Grind Tax test until the immunity exists, because that test would lock the types-plan side.

2. **Overleveraged: leave the shelf, or build Margin Loan?** Suggested default: leave the shelf. Status 208 stays reserved. `risk_marker` stays the foe surcharge follow-ups already shipped. Margin Loan is a new opcode and a new card, and it reopens a row the shelf just closed.

3. **Which Balloom five is the pool?** Suggested default: restore plan P5.1 if Gas Cloud and Hard Fork are supposed to be learnable (Gas Bump, Gas War, Hard Fork, Fuel Surge, Gas Cloud, flagship Gas War, initial Gas Bump). Accept the live five in writing if Slipstream and baseline slam were a later choice. All-In stays out either way.

4. **Uplink with no species row.** Suggested default: put `uplink` back on Validape as a passive the species can equip, without dropping the Common damaging move, or change the `APR-RELAY` sentence so it says the passive is implemented and ungranted. A silent removal reads as the approval being withdrawn.

5. **Jackpot's double versus "All-In is the hardest hit."** Suggested default: keep the 2 / 1 / 0.5 roll. Stop describing All-In as the hardest single hit without "except Jackpot's double." Do not retune Jackpot or Chaos Strike in this round.

6. **Does STAB stack on the ×1.2 / ×0.8 factor, and do typeless moves stay at ×1?** Suggested default: leave `enableStab` at 0. Do not decide the stack here. If the switch is turned on later, typeless moves need an explicit `baseType == None` exclusion so they do not inherit a primary type.

7. **Commons sitting just over the 30–40 band.** Suggested default: leave Cinder Burst at index 42.8 and Cipher Burst and Lucky Scatter at 40.4 until `rarity_rivals.md` exists. Do not retune Hype Train, Combustive Shift, or Bulwark Blow in this round.

8. **System Reset and Hype Burst.** Suggested default: pull both ids out of the live fives (proposal 2) and leave the rows empty. Building either effect is a later control pass. System Reset stays 3 AP, which is the CSV, not the unimplemented doc's 2 AP.

9. **Can SRV-PARITY stay `implemented, unverified` while the full suite is unfinished and the manifest is not in this tree?** Suggested default: keep the row, and do not move it to `fixed in PR` until proposal 13's tests pass on a tree that contains the manifest and `pvp_party.ts`. The generated AP, flags, and Known pool that are present can stay.

## Smoke checklist

Twelve battles. Watch the log. Rows already covered by EditMode are marked confirm, not rediscovery. The 05:22 pools replace several loadouts from the previous checklist.

### 1. Cofi — Grind Tax, Bean Sip, no draw

Encounter kit: Bean Sip, Bean Beam, Grind Tax. Difficulty Boss, capture disabled.

- Bean Sip: heal 8%, Speed +1, +1 AP, and Bean Sip is locked for one upcoming turn. Another move is legal next turn.
- Grind Tax at 6 AP: three hits on each foe, Speed −1 once per foe. A second cast goes to −2. Per-foe damage is well above two Bean Beams.
- At 5 AP the Utility AI does not pick Grind Tax. At 6 AP, off recharge, it picks Grind Tax over Bean Sip. The log shows the Charging tell and the Ready tell once each.
- Grind Tax is absent from learnable pools, the Known pool, and a PvP party if that guard is restored.
- Same-tick wipe: the player loses. A KO that is clearly first wins, including a player KO that lands before the boss. Confirm the boss is team 1.

Closes the manual half of DEC-BEAM, GRIND-TAX, BOSS-DRAW, E-31, E-32, E-01.

### 2. Keyper — Cipher Lock, and the blank System Reset

Keyper: Cipher Shot, Key Rotation, and whatever else the five grants (Memory Leak, Cipher Burst, System Reset). Foe: a buff move.

- A landed Cipher Shot blocks a new buff and a positive stage. The log names Cipher Lock. An already-applied buff keeps ticking.
- Swap the locked fighter out. The log says the lock fell away. A new buff lands.
- System Reset spends 3 AP and does not strip and does not skip the user. That is the current bug, recorded so nobody reads the card as done.
- Key Rotation's Def +1 and Eva +1 both run (`StageChanges` Def +1 and `ChangeStage` Evasion +1). Accuracy does not move on FUD if Verification Lock is added off-pool.

Closes MV-cipher_shot and E-30. Records MV-system_reset as still empty.

### 3. Validape — Hammer Sync +20%, Uplink only if equipped off-pool

Learnable: hub heal, Snap Strike, AP Router. Exclusive: Hammer Sync, Pack Rally. Uplink is not in the row.

- Hammer Sync with a clean partner deals the printed hit. A buff on the user alone does not raise it. After the partner is buffed, the same hit is about +20% and not more.
- Equip Uplink off-pool for one fight. The buffed ally gains +1 AP once. A second buff on that ally the same turn does not pay again. The user does not gain it.

Confirms the Hammer rule and APR-RELAY. The missing pool grant is question 4, not a smoke failure of the resolver.

### 4. Interest Strike and Rallying Slam

One Block or Staked fighter with both moves. Put Def above Atk, then repeat at 0/0.

- At 0 Def and 0 Atk, neither move grants AP.
- With Def above Atk, Rallying Slam grants +1 AP once. Interest Strike grants +1 AP on each hit that lands (accuracy 90, 2–3 hits).
- A hit a barrier fully absorbs still grants that AP.

Confirms the follow-ups gate. MV-interest_strike, MV-rallying_slam.

### 5. Shelf replacements

One Forked fighter with Leverage Up and, off-pool if needed, Hard Fork. One Gas fighter with Gas War. One Glitched fighter with Bit Rot. Guard Stance on a Block fighter. All-In on a separate Meme harness.

- Leverage Up: Atk +2, Def −1, and the next hit is about +25%. No HP loss.
- Gas War: the next move costs +1 AP. No HP loss on hit or miss.
- Hard Fork: one General debuff is removed from the user. No HP recoil.
- Bit Rot: a seeded run shows a foe stat drop on some hits, not a self-hit.
- Guard Stance: Def +2, and the log has no recoil line.
- All-In spends the bar (minimum 4), power stays 98, and the log says 1 turn of recharge. Another move is legal next turn.

Confirms SD-SHELF, MV-all_in, MV-recoil_strike, MV-volatile_shot, MV-glitch_barrage, MV-risk_spike, MV-guard_stance.

### 6. Balloom's live five

Fuel Surge, Gas War, Gas Bump, Slipstream, baseline slam. Initial Gas Bump. Offtype Hard Fork.

- Turn 1, Gas Bump (2 AP, power 36) is selectable. All-In is not on the card list. Gas Cloud is not on the card list.
- Record the five that actually appear, so the pool question has a witnessed row.

Confirms the Turn-1 rule. Does not confirm the previous brief's Gas Cloud line, because that move is no longer in the pool.

### 7. Dead walls and blank tools

Gilarime with Ice Wall. Rattlestake or Cofi's species pool with Compound Guard. Blockwyrm with Blockade. Crackett or Marrowhead with Isolation Tool.

- Ice Wall: the +1 AP clause can fire when the holder is faster. Mitigation does not. Hit budget is 0.
- Compound Guard: Def +1 lands. The 20% window does not.
- Blockade spends 3 AP and applies no window.
- Isolation Tool spends 1 AP and does not force the next move to one target.

Records E-17 and MV-blockade and MV-isolation_tool as still open. After proposal 1 and proposal 3, rerun this fight and expect the windows and Anchored.

### 8. Cinislug — Kindling on the live five

Ember Wall, Kindling, Cinder Burst, Hype Burst, Pulse Shot. Deflation Mark is not in the five. Add it off-pool only if you are checking the card, and label that fight off-pool.

- A damaging hit that leaves the foe alive applies Kindled. That foe loses 6% max HP at end of turn, once. A second hit the same turn does not stack a second 6%.
- Hype Burst spends 2 AP and removes no buff.
- Off-pool Deflation Mark applies Burned Tokens and Atk −1, and the Kindled tick does not heal the user.

Confirms APR-KIND. Records MV-hype_burst and E-13.

### 9. Jammed, FUD, and one chemistry lead

Gas into Node for Jammed. Any FUD applier, or Verification Lock, for the stages.

- Jammed: a token for 2 turns, and Accuracy does not move. After proposal 4, Accuracy −1 returns when Jammed expires.
- FUD: Atk −1, Def −1, Speed −1, reverted on expiry and on Cleanse. Accuracy does not move. The code comment still lists Accuracy until proposal 12.

Confirms the current E-07 miss and the E-26 / E-06 stage behavior.

### 10. Humpacked — Streamlined Flow's double Def

Flagship Streamlined Flow. HP above 70%.

- One action discounts a move by 1 AP and does not go below 1.
- Def falls by 2, not by 1. That is E-20, and it is the expected current bug.
- After proposal 6, the same action moves Def by −1.

### 11. Accuracy cards and SureHit

One fighter with Accuracy Focus, Evasion Maneuver, and Precision Strike. Foe with Evasion +2, then a separate exchange into Accuracy −1 if proposal 4 has landed.

- Accuracy Focus moves Acc by +2. Evasion Maneuver moves Eva by +2.
- Precision Strike into Eva +2 lands at least 95% of the time across a seeded sample. A plain 95% move misses more often than it did on turn 1.

Confirms ACC-TUNE and the accuracy payloads the master summary still calls empty.

### 12. End step, and a non-boss draw

One fighter with Double-or-Nothing, holding a duration-1 status. A separate non-boss mirror that can wipe both teams on one action (reflect is enough).

- The duration-1 status expires in the status pass before Double-or-Nothing's 60/40 roll. That is the current order. After proposal 7, the roll happens while the status is still up, and the status is gone at the end of the same end step.
- The 40% branch drains 1 AP and does not stop a later Packet Skim.
- A non-boss mutual wipe is a draw (`winnerTeamIndex` −2). A boss mutual wipe is not.

Confirms GAP-TICK as still open, and the non-boss half of BOSS-DRAW.

### What this smoke does not cover

Rally Cry, Refinery, Crash Routine, Script Loop, Viral Surge, Wildcard Swap, Extraction Hit, and the MEV steal are ungranted or off the live fives. One harness fight each is enough when proposals 9 and 10, or a later engine pass, land them. Stamp modifiers `3.2-b`, `3.2-c`, `7.2-c`, `10.1-d`, `10.2-b`, and `10.2-c` stay out of this pass. Setup Feed's 12% body is already in the resolver; do not edit it while checking the others.
