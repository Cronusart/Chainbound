# Combat issues decision brief v3

Source of truth: snapshot `chainbound_combat_snapshot_20261022_0010` (22 Oct 2026, ~00:10 CDT), plus the attached round-16 sims and `AP_DENIAL_ENDGAME_ANALYSIS.md`. The local checkout was not used. `Docs/GameTheory/` was not read. This note builds on `COMBAT_ISSUES_DECISION_BRIEF_V2.md` and the design notes since that brief. It does not restate their research.

`issues_brief_check_v3.py` recomputes every current-code claim below against the unzipped snapshot. Sim rates are 600 trials, seed 20261022, from `ap_denial_2v2_sim.py` plus `lock_sweep_v3.py`. `tempo_2v2_sim.py` and `walls_pvp_v2_sim.py` still parse `MitigationWindow` and do not mention `BlockWindow` [C-tempo-div] [C-walls-div]. They were not used for the rates. The denial sim is the engine mirror [C-denial-mirror].

**Checker: 138 PASS / 0 FAIL.**

## (a) Headline

The decided wall rules are in the data. The master still describes the old unlimited walls. The one rules lock left is chained Memory Leak, and a drain cap does not open it. Typeless moves still inherit the attacker's type. The AI still does not aim a damaging move [C-ai-untargeted].

| Finding | Today |
|---|---|
| Master rows | 128. Awaiting 18. Partial 2. Open 0. Unknown 0. Fixed in PR 42. Implemented, unverified 58 [M01] [M02] [M03] [M04] [M05] [M06] |
| Wild learn-pool band | 0 failures of 14 [P-band] |
| Move rows | 211. CSV power, AP, and recharge match every move asset. `move_ap.ts` matches every cost [P-move-count] [P-asset-nums] [P-move-ap] |
| Walls | Eight family rows are 1-hit `BlockWindow`s. No row still authors budget −1 [W-no-minus1]. Master E-17 still says unlimited [M08] |
| True rules lock | Memory Leak every turn. A passer stays at action AP `2,2,2,2,2,2,2,2` and never pays 4 [S-leak-pass]. One 2 AP spend then sits at 0 [S-leak-spend] |
| Wall coverage | Attacker win 100.0% through self Ember and through Blockade. Cap 0.0% on every wall line. Not a lock [S-w-ember] [S-w-block] |
| Typeless | `ResolveAttackType` returns `attacker.primaryType` when the move type is None [C-typeless]. STAB is off [C-stab] |
| Guard Posture | Still 25% for 1 hit, key `armor_guard_posture`. Flat 12 / 16 is not in the data [W-guard] |

### Decisions that unblock the most

1. **PROPOSAL**. Memory Leak gains `rechargeTurns` 2. Keep the −2 window and the Speed +2. The passer who is leaked on turn 1 and not on turn 2 pays a 4 AP move on turn 3 (`2,2,4,4,6,6,8,8`, hammer turn 3) [S-leak-cd].
2. **PROPOSAL**. Transaction Tax becomes `DrainAP:1`. The victim's calendar does not change. Deghoul stops ending turns at `2, 4, 6, 8, 10, 10` and ends at `1, 2, 3, 4, 5, 6` [S-deghoul] [S-victim].
3. **PROPOSAL**. Implement the decided typeless rule. A move with `baseType` None is neutral. Do not turn STAB on in that change.
4. **PROPOSAL**. Aim damaging moves, score denial off the foe's bar, and pass for a heavy only when its minimum roll KOs or its damage per AP is at least 1.15× the best legal 2 AP hit.
5. **PROPOSAL**. Cinder Burst from 3 AP to 2 AP. Power stays 15. Cinislug's typed click exists on turn 1. The band does not change.

Do not move the 4 AP heavies to 6. A 4 AP heavy on turn 2 is not an automatic win, and the sims show that. Do not add a drain cap of 2. Under that cap, Tax plus Skim is still `0,0,0,0,0,0,0,0`, and Leak plus Skim is still `1,1,1,1,1,1,1,1` [S-cap-tax] [S-cap-leak]. Do not floor a KO replacement at 2 AP. That swing is 0.0% to 35.8% [S-r-floor].

The questions are grouped in (f). The full list, with a default for each, is in (j).

## (b) Master re-audit

The count block matches the tables [M01] [M02] [M05] [M06]. Two texts are stale. The header still says Ash Guard, Compound Guard, Ice Wall, and Blockade use `hitsRemaining == -1` [M07]. E-17's summary still says an unlimited hit budget, and its status is `implemented, unverified` [M08] [M10]. MV-blockade's summary still says 25% for two turns with unlimited hits [M09]. Live Blockade is 60% melee, 1 hit, 2 turns, 3 AP, PRIOR [W-blockade].

Verdicts for every awaiting row, both partials, and the V2 still-broken and stale-text rows.

| ID | Master status | Today | Verdict |
|---|---|---|---|
| E-17 | implemented, unverified | 1-hit `BlockWindow`, engine forces `hitsRemaining = 1` [W-engine-one-hit] | OBSOLETE. The unlimited rule was replaced by the 2026-10-20 decision. Summary is stale |
| MV-blockade | implemented, unverified | `BlockWindow:60%:Melee:2:wall_blockade` [W-blockade] | FIXED as a card. Summary is stale |
| E-20 | implemented, unverified | One `ChangeStage`, no second stage column. `TriggerOnce` is false and the cap field is −6 [C-stream] | FIXED the double write. The walk to −6 is a separate proposal |
| E-21 | implemented, unverified | `MoveEffectsOncePerTarget` true, and the resolver gates on that set [C-compress] [C-once-engine] | FIXED |
| E-07 | implemented, unverified | Jammed is Accuracy −1 for 2 turns [C-jammed] | FIXED. Do not reopen −2 |
| E-11 | awaiting [M11] | Extra Def +1 while Def is below +3. Summary still says immune [C-hodl] [C-hodl-summary] | STILL OPEN |
| E-12 | fixed in PR on the body | Comment says steal. The processor drains [C-mev-comment] [C-mev-drain] | STILL OPEN as a comment. The drain is what runs |
| E-25 | implemented, unverified | Team-wide distinct-target count [C-charged] | FIXED |
| E-28 | awaiting [M13] | Both arms of the passive-status ternary assign the owner [C-e28] | STILL OPEN. No live passive authors a non-self override |
| MV-isolation_tool | implemented, unverified | `ApplyChemistryStatus:Anchored:1` [C-isolation] | FIXED |
| MV-script_loop | awaiting [M14] | Empty payload [C-empty] | STILL OPEN. Not in a pool |
| MV-viral_surge | awaiting [M15] | Empty payload [C-empty] | STILL OPEN. Not in a pool |
| MV-wildcard_swap | awaiting [M16] | Empty payload [C-empty] | STILL OPEN. Not in a pool |
| MV-crash_routine | fixed in PR | Atk −1 only. The old execute branch is gone | FIXED |
| MV-double_or_nothing | fixed in PR | Text is the 60/40 AP roll. No drain immunity | FIXED as the text cut |
| MV-rally_cry | implemented, unverified | `Yield:1` on self [C-rally] | FIXED |
| MV-refinery | implemented, unverified | `Yield:2` on self [C-refinery] | FIXED |
| MV-recursive_backup | fixed in PR | EffectOnly, 20% heal, text matches | FIXED |
| MV-status_resistance | deprecated [M21] | "Retired. No effect." In no pool [P-status-res] | OBSOLETE. Do not rebuild |
| GAP-TICK / EOT-ORDER | implemented, unverified | End-of-turn passives run, then the status pass [C-eot] | FIXED |
| DEC-AI | awaiting [M12] | Block moves have a target slot [C-ai-blocks]. Damaging moves still emit `UseMove(team, index)` with no slot [C-ai-untargeted] | STILL OPEN |
| MV-system_reset | implemented, unverified | `StripAllStatuses`, 3 AP, recharge 2, in Keyper. Text still says the user skips the next action [C-reset] | FIXED mechanic. Card is stale |
| MV-hype_burst | implemented, unverified | Strip 2, then Melee Atk +1 per buff. Live on Cinislug [C-hype] | FIXED payload. The stat is the wrong one for that pool |
| MV-bug_fix | implemented, unverified | Cleanse 1, Staggered, 10% heal, card says −10% [C-bug] | FIXED |
| MV-extraction_hit | awaiting [M17] | Empty. In no pool [C-extract-hit] | STILL OPEN |
| E-13 | awaiting [M18] | Deflation Mark is ungranted and still applies Burned Tokens. No heal body [C-deflation] | STILL OPEN on the card. Not in a fight |
| APR-RELAY | implemented, unverified [M22] | `uplink` is in Validape's learnable three. `ap_router` is not [P-uplink] | FIXED. The V2 "no species grant" regression is closed |
| APR-KIND | implemented, unverified [M23] | 6% on the card. Cinislug's five starts `ember_wall\|kindling` [P-kindling] | FIXED. The 6% literal in the processor is a tidy, not a retune [C-kindled-literal] |
| E-06 | fixed in PR | FUD comment is Atk/Def/Spd −1 [C-fud-comment] | FIXED |
| E-09 | fixed in PR | Summary is "Token. No contact damage." [C-backlash-summary] | FIXED. The enum comment still says contact DoT [C-backlash-comment] |
| BAL-61 | fixed in PR | `COMBAT_Moves.txt` says typed moves beat typeless [C-moves-txt] | FIXED |
| MV-dice_roll | fixed in PR | "Heal an ally for 20% MaxHP." [C-dice] | FIXED |
| NP-rekt, NP-offline, NP-relay-status | deprecated | Summaries say deprecated, no producer | FIXED as deprecations |
| NP-glitchedmind | awaiting [M24] | Mis-act body, no move | STILL OPEN. Default: no new card |
| NP-unsealed | awaiting [M24] | 50% barrier bypass, no move | STILL OPEN. Default: no new card |
| BAL-EPIC | awaiting [M19] | Bulwark Blow, Epic, power 50, 3 AP, strikes with Def [C-bulwark] | STILL OPEN |
| BAL-EMPTY | partial [M20] | The old "Rare on the card, Common in the resolver" set was retiered or text-cut | OBSOLETE as that defect. Riderless legendaries were an accepted cut |
| E-23 | partial [M20] | Five stamp ids retired. Setup Feed remains the other agent's row | STILL OPEN only as "do not edit those stamps" |
| DEC-S1 … DEC-S5, DEC-PACK, DEC-CD | awaiting [M24] | Not built | STILL OPEN as "do not build". Prefer the empty live cards |
| DEC-CHEM | superseded | Factors are 1.2 and 0.8 [C-chem]. Meme has no cells | OBSOLETE. The inheritance bug is a different row |
| E-14 | superseded | Overleveraged summary is shelved | OBSOLETE as a body. Leave it shelved |
| MV-crystal_extraction | implemented, unverified | Heal 5% per foe debuff, then cleanse the foe. Live on Kodiax [C-extract] | FIXED. The cleanse-self sentence was a doc slip |
| Uplink pool regression (V2) | master said it was in the pools, the tree had removed it | It is in the pool again [P-uplink] | FIXED |
| Blank System Reset / Hype Burst (V2) | both were empty and live | Both have payloads [C-reset] [C-hype] | FIXED |

Awaiting ids, for the record [M24]: BAL-EPIC, DEC-AI, DEC-CD, DEC-PACK, DEC-S1, DEC-S2, DEC-S3, DEC-S4, DEC-S5, E-11, E-13, E-28, MV-extraction_hit, MV-script_loop, MV-viral_surge, MV-wildcard_swap, NP-glitchedmind, NP-unsealed.

## (c) Proposal ledger

Status is against this snapshot. "Pending" means the proposal is not in the data. Withdrawn names were not re-proposed: Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, Open Line, Cinder Idle, Scorch Echo are absent [P-withdrawn].

| Source | Proposal | Status |
|---|---|---|
| Walls v2, section 1 | Melee walls, ranged barriers, 1 hit, 2 turns, recharge 1, PRIOR, one block, Ember 50% and 1 AP drain, Ice +1 if faster, renames | Implemented. CSV [W-ember_wall] [W-blockade] [W-ice_wall] [W-barrier_wall] [W-vapor_barrier] [W-compound_guard] [W-ash_guard] [W-lattice_guard]. Engine denies a second block [W-engine-one-active] and does not pass override on a move cast [W-apply-no-override]. Ice gain is the consume rider, and only when faster [W-ice-ap] [W-ice-faster] [W-ice-no-double-window]. Test name exists [W-test]. Lattice reflect 20% [W-lattice]. Ember drain 1 [W-ember-drain] |
| Walls v2 plan file | Phased todos | Not flipped. Frontmatter is still pending [W-plan-stale]. The code moved anyway |
| Walls v2 "P" (full block, flat 2 AP) | Superseded by the decided rules, which landed |
| Guard Posture "numbers unchanged until the Armor round" | Implemented as the holding pattern [W-guard] |
| Armor v1 flat 12 / 16, cap 16, pool swaps, stamp flats, Core Breach | Pending. No `core_breach` row [C-core] |
| AP economy v2, first ranked list | Power ≥ 72 and All-In to 6 AP | Superseded by that note's own revisit, and by the tempo rule. Live costs are 4 [C-heavies] |
| AP economy v2 revisit | Keep those six at 4 AP | Already the data |
| AP economy v2 | Drain cap 2, and a Skim/Tax refund on a 0 | Pending. Later sim rejects both. Not in the code |
| AP economy v2 | AI pass when one regen pays a power ≥ 60 move | Pending, and wider than the denial note. Use the narrower gate |
| AP-denial endgame | Memory Leak `rechargeTurns` 2 | Pending. Live recharge is 0 [C-leak] |
| AP-denial endgame | Tax becomes a drain | Pending. Live opcode is `StealAP:1` [C-tax] |
| AP-denial endgame | No cap, no whiff refund, no KO floor, Cinder to 2 AP, card sentences | Pending. Cinder is still 3 AP, power 15, Common [C-cinder] |
| Cost curve v2 | Adopt walls P | Superseded. Decided walls landed |
| Cost curve v2 | Tax to a drain, drop Gas War's surcharge, Bulwark to 2 AP, Cryo Ray slow to 100% | Pending. Gas War is still ranged, 4 AP, with `NextMoveAPSurcharge:1` [C-gaswar] |
| Cost curve v2 | Leave Deep Dig, Hammer Sync, Chaos Strike, Bean Beam, Blink Lunge | Already the data. Keep them |
| Type / chemistry v2 | Typeless excluded from the chart and from STAB | Pending. This is the decided rule, and the code still inherits [C-typeless] |
| Type / chemistry v2 | Rugged cap 3 | Pending. Constant is 15 [C-rugged] |
| Type / chemistry v2 | Jammed Accuracy −1 | Implemented [C-jammed] |
| Type / chemistry v2 | HODLed one ramp plus immunity, no producer | Pending [C-hodl] |
| Type / chemistry v2 | Staked common replaces Pulse Shot on Rattlestake only | Pending. Rattlestake's five is still typeless below Compound Barrier |
| Type / chemistry v2 | Margin Loan, narrowed Dusted / Shattered / Corroded, Blade Flurry to 10×3 | Pending. Margin Loan should stay shelved |
| Passives v2 | Patience cap +3, Unstable cap +3, Automated Pressure cap −2 | Pending |
| Passives v2 | Extraction Hit at Common in place of Baseline Slam | Pending [C-extract-hit] |
| Passives v2 | Fee Collector at Common in place of Slipstream | Pending. Not granted |
| Passives v2 | Streamlined drops Def once | Pending. The duplicate write is gone [C-stream] |
| Passives v2 | Cipher Lock on the stage column, OnMiss split, rarity demotions, narration name | Pending |
| Passives v2 | Uplink is in a pool | Implemented [P-uplink]. The card-text tighten is pending |
| AI decision quality | Target slot, real hit estimate, payload score, boss horizon, Defensive ladder, focus-fire pass, tell timing | Pending, except block moves already carry a slot [C-ai-blocks] |
| Status producers A | Jammed Accuracy −2 | Superseded by the landed −1 |
| Status producers B–N | Mind Fault, HODLed card, Unsealed support, and the rest | Pending. Defaults in (j) build none of the new producers this round |
| New concepts | System Reset, Hype Burst, Crystal Extraction as specified | Implemented, with Hype Burst still on Melee Atk [C-hype] [C-extract] [C-reset] |
| New concepts | Yield Jab, Pair Ping, Tine Strike, and the other new ids | Pending. Pair Ping would add a sixth-or-extra move to Validape. The learn-pool plan said to leave that membership. Do not add it |
| New concepts | Pressure Point out, Yield Jab in, on Rattlestake | Obsolete. Pressure Point is not in the five |
| Tempo 2v2 | Ember recharge lengthened to stop self-refresh | Superseded. Recharge 1 plus continuous coverage was accepted, and the attacker still wins [S-w-ember] |
| Tempo 2v2 | Drain cap, and a KO replacement at 0 or one regen | Superseded on the cap. Leave the inherit rule |

## (d) Conflicts, and one reconciled proposal each

| Conflict | Why they disagree | Reconciled **PROPOSAL** |
|---|---|---|
| Heavies at 4 AP or 6 AP | Economy v2's first list said 6. Its revisit, the cost curve, and the tempo rule say 4 is the turn-2 cast, and two faster 2 AP hits can still win | Keep 4. Gas War, Hard Fork, Deep Dig, Hammer Sync, Cryo Ray, and All-In stay at 4 [C-heavies]. Seam Cut into a Hammer passer wins 99.7% to 0.3% [S-r-seam]. Kitsauto's flurry against a Deep Dig passer wins 100.0%, and the heavy still lands on 69.5% of trials [S-r-dig] |
| Drain cap of 2 | Economy v2 and the tempo note wanted it. The denial calendars show it does not free a 4 AP cast [S-cap-tax] [S-cap-leak] | Do not add the cap |
| Refund when a drain removes 0 | Economy v2 wanted the AP back. Denial showed a refund makes Skim free on an empty bar | Do not refund |
| Tax steals or drains | Cost curve, denial, and tempo say drain. The live opcode steals, so Deghoul banks a heavy on the victim's clock [S-deghoul] | **PROPOSAL**. `StealAP:1` becomes `DrainAP:1`. Cost 1. Epic. Deghoul's signature stays Transaction Tax. Victim series stays `1, 2, 3, 4, 5, 6` [S-victim] |
| Memory Leak "leave it" vs recharge 2 | Cost curve said the Speed +2 is the signature. Denial said the missing piece is the recharge, not a weaker drain | **PROPOSAL**. Keep −2 and Speed +2. Set `rechargeTurns` to 2. Card states: after regen, the target loses 2 AP next turn; this is a window; a swap clears it; cleanse does not; you cannot cast it next turn [C-leak] [C-leak-text] |
| Cinder Burst at 3 or 2 | Type v2 and denial agree on 2. Cost curve said not to touch multi-hit until walls were decided | Walls are decided and in the data. **PROPOSAL**. AP 3 → 2. Power 15. Hits 2–4. Rarity Common. Cinislug's signature stays Ember Wall [P-kindling] [C-cinder] |
| Gas War melee, or drop the surcharge | Economy revisit said make it melee. Cost curve said drop the +1 and keep 4 AP ranged | **PROPOSAL**. Drop `NextMoveAPSurcharge`. Stay `DamagingRanged`, power 72, 4 AP [C-gaswar]. Do not also retune the category |
| Jammed −1 or −2 | Status-producer A said −2. The landed card and the later notes say −1 | Leave −1 [C-jammed]. Producer A is superseded |
| HODLed immunity now, or after the body | Type v2 wants one ramp and immunity. The status note said do not ship a producer against the current body | **PROPOSAL**. Change the body first: one Melee Def +1 per end step, stop at +3, own it, revert it, and filter foe negative stages. No new move. No Blockwyrm pool edit. Update the test that expects +2 after one end step |
| Armor 25% or flat 12 / 16 | Walls v2 froze the 25% until the Armor round. Armor v1's flat numbers were never accepted | Live card stays 25%, 1 hit, 2 turns, any category, stacks with a block [W-guard]. **PROPOSAL**. If the Armor answers are yes, ship flat 12 on Guard Posture and flat 16 on Guard Stance, one hit, after the wall percent, cap 16, 10% floor kept. Until then, do not write flat numbers onto the card |
| Armor pool vs Fee Collector pool | Both want a Deghoul common slot | **PROPOSAL**. Do the Fee Collector swap only, and only if that question is yes. Hold the Guard Posture swap until the flat numbers are accepted. Do not do both in one edit |
| Ember extra recharge vs accepted coverage | Tempo wanted recharge 2 to kill self-refresh. The decision accepted recasting | Leave recharge 1. Self Ember vs Ore Strike: attacker 100.0%, waller 0.0%, cap 0.0%, about 2.15 walls a battle [S-w-ember] |
| KO replacement at 0, at one regen, or floored at 2 | Tempo leaned to one regen. Denial measured the floor | Leave the inherit. Floor 2 moves the bench line from 0.0% to 35.8% [S-r-inherit] [S-r-floor] |
| AI pass rules | Economy v2 passes for any power ≥ 60 within one regen. Denial passes only for a KO or a 1.15× rate. The AI note also wants target slots | **PROPOSAL**. One change: target slot on every damaging move, a pure hit estimate (Atk/Def, mean roll, hits, accuracy, ×1.2 / ×0.8, typeless neutral, no chemistry resolver), denial scored from the foe's current AP, and Pass only for the narrow heavy gate. Boss horizon stays inside that Pass gate. Do not rescore the whole utility formula |
| Pair Ping on Validape | New concepts would add it. The learn-pool plan said leave Validape's membership | Do not add it. Hammer Sync stays the only top rarity [P-flagship-hammer] |
| Yield Jab replaces Pressure Point | That slot is gone | Obsolete. The typed common for Rattlestake replaces Pulse Shot, not a move that is no longer there |
| Hype Burst's Atk stat | The built card grants Melee Atk. Cinislug's clicks are ranged | **PROPOSAL**. `MeleeAtk` becomes `RangedAtk`. Cap stays 2. Tooltip says status buffs, not stages. Pool unchanged |
| Overleveraged body | Shelf says no HP. Type v2 wants Margin Loan. New concepts want a damage buff and a surcharge | Leave status 208 shelved. No Margin Loan this round. No HP |

## (e) True-lock sweep

Decided wall rules, today's CSV, 600 trials. A true lock is a line where a repeating rule keeps a side from a legal winning action, or a wall line the attacker never wins. A waller who spends the turns and loses the race is not a lock. A 1v2 that is 0% with or without the wall is not a lock. Continuous recast is accepted.

| Line | Result | Lock? |
|---|---|---|
| Passer, Memory Leak every turn | Action AP `2,2,2,2,2,2,2,2`. Hammer never [S-leak-pass] | Yes. Finisher lock |
| Spend 2 once, then Leak every turn | `2,0,0,0,0,0,0,0` [S-leak-spend] | Yes. Zero lock |
| Tax + Skim every turn, victim passes | `0,0,0,0,0,0,0,0` [S-taxskim] | Yes. Zero lock |
| Same two lines with a cap of 2 | Still 0s, or `1`s that cannot pay 4 [S-cap-tax] [S-cap-leak] | Cap is not a fix |
| Leak with recharge 2, victim passes | `2,2,4,4,6,6,8,8`, hammer on turn 3 [S-leak-cd] | Opens |
| That recharge, but the victim spends 2 on the open turns | `2,0,2,0,2,0,2,0`, still a finisher lock [S-leak-cd-spend] | The recharge frees a passer, not a body that keeps spending the open turn |
| Commit-pass Validape vs Leak + Flurry | Victim 0.0%, heavy lands 0.0%, median KO 3 [S-r-pass] | Yes. The battle form of the finisher lock |
| Same shape, Flurry kit, victim may click Snap | Victim 0.0% [S-r-flurry] | Yes. Clicking the 2 AP chip is the zero lock, and the 2v1 finishes it |
| Bench inherit vs floor 2 | 0.0% vs 35.8% victim wins [S-r-inherit] [S-r-floor] | Do not floor |
| Self Ember vs Ore Strike | Attacker 100.0%, waller 0.0%, cap 0.0% [S-w-ember] | No. The attacker wins |
| Blockade vs Ore Strike | Attacker 100.0%, waller 0.0%, cap 0.0% [S-w-block] | No |
| Buffer Barrier vs Frost Shot | Attacker 99.3%, waller 0.7%, cap 0.0% [S-w-buffer] | No. Both can win |
| Ember on an ally, Crackett vs two | Attacker 0.0% [S-w-ally] | No. The no-wall control `wall_ally_nowall_control` is also 0.0% [S-w-ctrl] |
| Ember and Blockade aimed at one body | 49.3% / 50.7%, cap 0.0%, 0.11 rejected casts a battle [S-w-both] | No. The second cast is rejected |
| Ember + Leak vs a Hammer passer | Victim 0.0%, median KO 10 [S-r-emberleak] | The leak, slowed by the wall. Not a new wall lock |
| Tax + Skim while Crackett clicks Seam | Hammer side 100.0% [S-r-pressure] | No. The zero lock needs the victim to pass |
| Bank of 8 vs Leak + Flurry | Heavy lands 100%, victim still wins 2.2% [S-r-bank] | No |
| Gas Bump vs a Deep Dig bank | Banker 100.0% [S-r-bump] | No. Not every 2 AP line beats a 4 AP heavy |
| Flurry vs a Deep Dig passer | Banker 0.0%, heavy lands 69.5% [S-r-dig] | No rules lock. The heavy is legal and the side loses the race |

Expected damage at roll 0.925, IV 0, no crit: Seam Cut into Deghoul 128 (HP 106), into Validape 92 (HP 156), Hammer Sync into Deghoul 115, Blade Flurry into Kodiax 60 (HP 124), Gas Bump into Kodiax 37 [S-q] [S-hp]. Cinislug's HP is 134. Kitsauto's is 139.

## (f) Consolidated questions

These are the open questions from the design notes since the v2 brief, the seven Armor questions, and the AP-denial questions. The already-decided list at the top of (j) is not asked again. Each default is a suggestion. The same rows, with what each one blocks, are the table in (j). Backlog "Depends" numbers are these Q numbers.

| System | Questions | Suggested default |
|---|---|---|
| AP denial | Q1 Memory Leak recharge 2. Q2 Tax becomes a drain. Q3 refund a whiff. Q4 cap removal at 2. Q5 KO floor of 2. Q6 MEV becomes a steal. Q39 how far a player can push Grind Tax off | Q1 yes. Q2 yes. Q3 no. Q4 no. Q5 no. Q6 no. Q39: one denial slips a turn, a chain does not delete the tax |
| AI | Q7 when a pass may bank a 4 AP heavy. Q36 Grind Tax cadence. Q37 keep sipping at Speed +6. Q38 the due tell. Q40 hold Fox Feint. Q41 the Defensive ladder | Q7 only for a minimum-roll KO or a 1.15× rate. Q36 turn 4, then every four. Q37 no. Q38 yes, read AP before the spend. Q40 yes. Q41 yes, before any encounter uses it |
| Armor | Q10 flat 12 and 16. Q11 the longer Hammer-plus-Blockade line. Q12 Deghoul takes Guard Posture. Q13 Blockwyrm takes Guard Stance. Q14 Immutable Ledger. Q15 Shattered Armor and Core Breach | Q10 yes, and do not ship it ahead of backlog 1–3. Q11 yes. Q12 no until Q10, and not in the same edit as Q24. Q13 no until Q10. Q14 stays a 20% signature. Q15 stay catalog-only |
| Statuses and chemistry | Q16 HODLed. Q17 Rugged cap. Q18 Dusted, Shattered, Corroded, Defueled. Q19 STAB. Q21 Overleveraged. Q46 Ghost, Meme, FOMO | Q16 yes: one ramp, cap +3, revert, no producer. Q17 cap 3. Q18 yes. Q19 off. Q21 stay shelved. Q46 stay as they are |
| Pools and empty moves | Q8 Pair Ping. Q9 Cinder Burst. Q20 Staked common. Q22 Patience. Q23 Extraction Hit. Q24 Fee Collector. Q25 Unstable, Firewall, Automated Pressure. Q27 Uplink. Q28 rarity demotions. Q45 secondary commons | Q8 no. Q9 yes, AP 2, power 15. Q20 yes, Rattlestake only, Pulse Shot stays 26. Q22 cap +3. Q23 yes, Common, replaces Baseline Slam. Q24 yes, Common, replaces Slipstream, not beside Armor. Q25 leave them out. Q27 text only. Q28 yes, no pool change. Q45 no, not in the typeless change |
| Passives and cards | Q26 OnMiss split. Q29 Cipher Lock. Q30 narration. Q31 Streamlined. Q42 Hype Burst. Q43 System Reset's card | Q26 yes. Q29 yes, stage column and Meme stages. Q30 yes. Q31 yes, once. Q42 Ranged Atk, and the tooltip says status buffs. Q43 yes, drop the skip sentence |
| Later costs | Q32 Gas War surcharge. Q33 Bulwark Blow. Q34 Cryo Ray. Q35 Blade Flurry. Q44 new 5 AP moves | Q32 drop the surcharge, stay ranged at 4 AP. Q33 2 AP, power 50. Q34 Speed −2 at 100%. Q35 10×3. Q44 no this round |

## (g) Ranked backlog

Each row is a **PROPOSAL**. Effort is S, M, or L. Pool edits keep the learn-pool rule: a wild five is three at the body tier or lower, one at the body or one step up, and a signature that is the unique highest. Starters only owe a unique top rarity. Ghost and Cofi stay out. Boss-only moves stay out of pools and candy. Prefer filling an empty move over pulling one. No row pulls a move except where it names the one it replaces.

| # | Action | Impact | Effort | Files | Depends | Test |
|---|---|---|---|---|---|---|
| 1 | Memory Leak `rechargeTurns` 2. Window, −2, and Speed +2 stay. Card states the five limits in (d) | Breaks the only rules lock | S | `Moves.csv` `memory_leak`, `Move_memory_leak.asset` description. No pool edit. Band unchanged [P-band] | Q1, default yes | `MemoryLeak_RechargeSkipsOneTurn_FinisherPaysOnTheOpenTurn`: a passer leaked on turn 1 only has at least 4 AP at action time on turn 3 |
| 2 | Transaction Tax `DrainAP:1`. Card: "Drain 1 AP from the target. You do not gain it." | Stops a signature from banking a free heavy | S | `Moves.csv` `transaction_tax`. Deghoul flagship stays. Band unchanged | Q2, default yes | `TaxIsDrain_DeghoulDoesNotGainThePoint` |
| 3 | Typeless moves are neutral. `ResolveAttackType` returns None's neutral path and does not use `primaryType`. STAB stays off | Decided rule that is not in the engine | M | `DamageCalculator.cs` `ResolveAttackType` and `ResolveStabMultiplier`. Move panel stops showing an inherited type | None. Q19 only if someone wants STAB in the same change. Do not | `TypelessMove_IsNeutralAndAppliesNoReaction`: Pulse Shot from a Burned user into Gas is ×1.0 and no Rugged. Cinder Burst into Gas is ×1.2 and Rugged |
| 4 | AI aims, estimates, and scores denial | The brain stops missing the lock and the blank click | L | `AIHelpers.GetLegalActions`, `EstimateDamage` sibling, `UtilityStatusAI.ScoreAction`, `AggressiveAI` | Q7 | `UtilityAi_ScoresEachLegalTargetSlot`. `UtilityAi_ScoresLeakAboveCipherWhenTargetUnderFour`. `Ai_PassesForHeavyOnlyWhenMinRollKillsOrRateIsHigher` |
| 5 | Cinder Burst AP 2, power 15 | Burned has a typed turn-1 click | S | `Moves.csv` `cinder_burst`, the asset, regenerate `move_ap.ts`. Cinislug membership unchanged | Q9, default yes | `CinderBurst_CostsTwo_BandUnchanged`: flagship still Ember Wall, wild band still passes |
| 6 | Card text states the limit | Players can read the rule that already runs | S | Descriptions on `system_reset`, `grind_tax`, `memory_leak`, `thread_lock`, `hammer_sync`, `transaction_tax`. `StatusId.cs` MEV comment | None, except Tax's sentence lands with row 2 | `CardText_StatesTheLimit`: System Reset does not say the user skips an action. Grind Tax names recharge 2. Hammer names the per-partner bonus and the 1.2 cap. MEV comment says drain |
| 7 | Hype Burst grants Ranged Atk, and the tooltip says status buffs | The live pool can spend the payoff | S | `Moves.csv` `hype_burst` stage stat and description. Pool unchanged | Q42, default yes | `HypeBurst_GrantsRangedAtkPerBuffRemoved`: two buffs, Ranged Atk +2, Melee Atk unchanged, a stage buff is not stripped |
| 8 | HODLed: one owned ramp, cap +3, revert, foe drops filtered | The awaiting body stops walking to +4 and claiming an immunity it lacks | M | `StatusProcessor.cs` HODLed block, `Statuses.csv` HODLed, `CombatDecisionRoundTests` assertion of +2 | Q16, default yes | `Hodled_RampsOnePerStep`: +1, +2, +3, then 0. A foe stage drop writes nothing. The holder's own drop still lands |
| 9 | Rugged cap 3 | Refreshes stop climbing toward a full bar | S | `CombatConstants.RUGGED_TICK_CAP`, resolver config `ruggedTickCap`, chemistry spec, the test that asserts 15 | Q17, default yes | Retarget `RuggedEscalatesAndCapsWhileDripAndFomoTickOnce` so the default cap asserts 3 |
| 10 | Streamlined drops Def once | The signature stops walking to −6 | S | `Moves.csv` `streamlined_flow`: `TriggerOnce` true, stage cap −1. Discount stays. Humpacked's signature stays | Q31, default yes | Extend `StreamlinedFlow_DropsDefOnceAbove70`: a second action above 70% does not drop Def again |
| 11 | Patience cap +3 | The granted snowball stops at +3. Heal stays | S | `Moves.csv` `patience_passive` cap field `6` → `3`. Hopoch's pool unchanged. Rare under Legendary Yield Spike | Q22, default yes | `Patience_StopsAtDefThree` |
| 12 | Build Extraction Hit at Common. Replace `baseline_slam` on Kodiax | Empty move, preferred over a new id. Rare cannot sit beside Ore Strike | M | `Moves.csv` `extraction_hit` rarity Common, OnHit, 40% Melee Atk +1 cap +3, Speed +1 once at +3. `SpeciesMovePools.csv` Kodiax. Band: Common damaging move remains Pickaxe Flurry. Signature stays Ore Strike | Q23, default yes | `ExtractionHit_RaisesAtkAtFortyAndSpeedsOnceAtPlusThree`. `LearnPool_KodiaxExtractionStaysInsideTheBand` |
| 13 | One Staked common, power 34, AP 2, ranged, accuracy 95. Replace `baseline_shot` on Rattlestake only | The body has no typed click below Compound Barrier. Pulse Shot stays power 26 as a move [P-pulse] | M | New move row plus `SpeciesMovePools.csv` Rattlestake. Five stays five. Signature stays AP Reserve. The one step-up stays Compound Barrier | Q20, default yes | Pool size 5, signature the unique top, the new move is Staked and Advantage into Forked |
| 14 | Fee Collector becomes Common and replaces Slipstream on Deghoul | Gas gets the tax passive it already has in the catalog | S | `Moves.csv` rarity, `SpeciesMovePools.csv` Deghoul. Signature stays Transaction Tax. The one Uncommon stays Gas War. Gas Bump stays the Common damaging move | Q24, default yes. Not in the same edit as an Armor swap | `LearnPool_DeghoulFeeCollectorStaysInsideTheBand` |
| 15 | Cipher Lock filters positive stages from the passive stage column and from Meme stages | The gate is real and these writes walk around it | M | `CombatResolver.cs` passive stage loop and `ApplyMemeStage`, through `BuffGate.FilterPositiveStage` | Q29, default yes | `CipherLock_BlocksGeckoGuardAndDesperationStageColumn` |

Next, not in the fifteen. **PROPOSAL**. Bulwark Blow to 2 AP, power 50, Def strike kept, so the Epic is a turn-1 click [C-bulwark]. **PROPOSAL**. Delete Gas War's surcharge [C-gaswar]. **PROPOSAL**. Narrow Dusted, Shattered, and Corroded, and make Defueled revert owned stages. **PROPOSAL**. Blade Flurry from 12×3 to 10×3. Kitsauto's signature stays Fox Feint. **PROPOSAL**. Cryo Ray's Speed −2 becomes 100%. Power and AP stay.

Explicitly not in this backlog. No drain cap. No whiff refund. No KO floor. No heavies at 6. No walls-P reboot. No longer Ember recharge. No Margin Loan. No Pair Ping. No Mind Fault. No Core Breach in a pool. No second copy of the withdrawn passives. No pull of System Reset, Hype Burst, Crystal Extraction, Kindling, or Uplink. Finish Burn stays 3 AP and ungranted [C-finish]. Grind Tax stays 6 AP, boss-only, and out of pools [P-grind-pool].

## (h) Consistency flags

| Surface | What it says | What runs |
|---|---|---|
| Master header and E-17 | Unlimited hits, budget −1 [M07] [M08] | 1 hit [W-engine-one-hit] [W-no-minus1] |
| MV-blockade summary | 25%, unlimited [M09] | 60%, 1 hit [W-blockade] |
| `walls_barriers_tiered_v1.plan.md` | Todos pending [W-plan-stale] | CSV, assets, and `WallsBarriersTieredV1Tests` are in |
| Guard Posture asset | Stack key `armor_guard_posture`, and `wallSfxId: wall_guard_posture` [W-gp-asset-key] | The block is Armor. The sound id is still a wall id |
| Family assets | Power, AP, recharge, and names match the CSV [W-assets] [P-asset-nums] | Server `move_ap.ts` matches too [P-move-ap] |
| `tempo_2v2_sim.py`, `walls_pvp_v2_sim.py` | Old `MitigationWindow` engine [C-tempo-div] [C-walls-div] | Live walls are `BlockWindow` [C-denial-mirror] |
| System Reset card | "skip your next action" [C-reset] | Recharge 2 locks System Reset only |
| Grind Tax card | No recharge word [C-grind-text] | `RechargeTurns` 2 |
| Memory Leak card | No window, no swap, no recharge [C-leak-text] | `ArmUtilityWindow` −2, recharge 0 [C-leak] |
| Thread Lock card | No "cannot be applied again" [C-thread-text] | The gap is in the code |
| Hammer Sync card | "+20% if your partner is buffed", no cap [C-hammer-text] | +0.2 per buffed partner, cap 1.2 |
| Transaction Tax card | Steal | `StealAP:1` does pay the user [C-tax] |
| MEV comment | Steal [C-mev-comment] | Drain [C-mev-drain] |
| Backlash comment | Contact DoT [C-backlash-comment] | Summary is a token [C-backlash-summary] |
| HODLed summary | Immune to stat drops [C-hodl-summary] | No such filter |
| Typeless comment in `DamageCalculator.cs` | Inherit the user's type [C-typeless] | The design rule says excluded |
| Kindled | Catalog 0.06 and a literal `0.06f` [C-kindled-literal] | Same number, two authors |
| Blank cards with a promise and no payload | Script Loop, Viral Surge, Wildcard Swap, Extraction Hit, position swap, AP Donation, AP Redistribution, AP Router, Damage Intercept, Effect Relay, Relay Packet [C-empty] [C-extract-hit] | The click spends AP and does not do the sentence. Dice Roll is not in this set. Its heal runs [C-dice] |
| `ap_router` | Still a move row | Validape learns `uplink` instead [P-uplink] |
| Hammer / Crystal / System Reset | Built and granted | Cards still omit a limit, as the rows above |

Card rule for the next text pass: every key limit is on the card. How much AP, how many hits, how many turns, the cap, whether a swap clears it, and whether the user gains the point.

## (i) What works, what doesn't, what could be better

What works. The wall decision is actually in the game: category, one hit, recharge, one block, Ember's drain, Ice's single AP [W-ember_wall] [W-ice-faster]. The attacker wins through a recast wall [S-w-ember]. Uplink is on Validape [P-uplink]. Kindling is 6% [P-kindling]. Pulse Shot is 26 [P-pulse]. System Reset, Hype Burst, Crystal Extraction, and Bug Fix have payloads [C-reset] [C-hype] [C-extract] [C-bug]. End-of-turn passives run before status ticks [C-eot]. The wild band has 0 failures [P-band]. Jammed is a real Accuracy −1 [C-jammed]. A bank of 8 still fires [S-r-bank]. Two faster chips beat a passer who is waiting on Hammer [S-r-seam]. Gas Bump does not, which is the tempo rule doing its job [S-r-bump].

What doesn't. Chained Memory Leak is a finisher lock and, after one spend, a zero lock [S-leak-pass] [S-leak-spend]. Tax plus Skim is the same lock on a passer, and a cap of 2 does not break it [S-taxskim] [S-cap-tax]. Tax's steal lets Deghoul bank the heavy the victim cannot pay [S-deghoul]. Typeless moves still borrow a type [C-typeless]. The AI does not aim a hit [C-ai-untargeted]. HODLed still double-ramps and still claims immunity [C-hodl]. Cinislug's typed move costs 3, so it misses turn 1 [C-cinder]. Hype Burst buffs the stat that pool does not strike with [C-hype]. The master still tells the next agent to ship unlimited walls [M07].

What could be better. Recharge on the leak, a drain on the tax, the typeless exclusion, an AI that can see a bar, and cards that state the limit. Then the empty Extraction Hit and the missing Staked common. Leave the 4 AP band, the wall coverage, the shelved self-damage, and the withdrawn passives where they are.

## (j) Questions only Cronus can answer

Already decided, so they are not questions: wall category split, 1 hit, 2 turns, tiers, recharge 1, priority does not break a wall, one block, continuous recast is allowed, Ember is 50% PRIOR with a 1 AP drain, Guard Posture is Armor rather than a wall, Uplink replaces AP Router, Kindling stays 6%, Pulse Shot stays 26, Status Resistance stays retired, the named passives stay withdrawn, self-damage stays shelved, boss-only moves stay out of pools, the chart is ×1.2 / ×0.8, STAB is not turned on here, Meme has no chemistry, and a 4 AP heavy on turn 2 is not an automatic win.

| # | Question | Default | Blocks |
|---|---|---|---|
| Q1 | Memory Leak stays a window and gains `rechargeTurns` 2? | Yes. Rewrite the card with it | Backlog 1. The finisher lock |
| Q2 | Transaction Tax becomes a drain? | Yes | Backlog 2 |
| Q3 | Refund the cost when a drain removes 0? | No | Nothing, if the default holds. A yes makes Skim free on an empty bar |
| Q4 | Cap enemy AP removal at 2 per target per turn? | No [S-cap-tax] [S-cap-leak] | Nothing useful. It does not free a 4 AP cast |
| Q5 | KO replacement enters with at least 2 AP? | No. 0.0% becomes 35.8% [S-r-floor] | A large swing for a patch the window-clear already covers |
| Q6 | MEV Extracted becomes a steal? | No. Fix the comment. Leave it out of pools | Comment only |
| Q7 | May the AI pass to bank a 4 AP heavy? | Only when the minimum roll KOs, or damage per AP is at least 1.15× the best legal 2 AP move | Backlog 4 |
| Q8 | Add a typed Node move at 2 AP, or Pair Ping, to Validape? | No. Leave that membership | Nothing this round |
| Q9 | Cinder Burst from 3 AP to 2, power stays 15? | Yes | Backlog 5 |
| Q10 | Flat Armor 12 and 16, one hit, after the wall percent, cap 16? | Yes, as the Armor shape. It is not in the data today [W-guard] | Any Armor number. Do not ship it in front of backlog 1–3 |
| Q11 | If Q10 is yes, is the longer Hammer-plus-Blockade line acceptable? | Yes. Do not lower Blockade's 60% | Whether flat Armor stacks or is nostack |
| Q12 | Deghoul replaces Snap Strike with Guard Posture? | No until Q10 is yes, and not in the same edit as Q24 | Armor pool |
| Q13 | Blockwyrm replaces Stone Barrage with Guard Stance? | No until Q10 is yes | Armor pool. The Def +2 would leave |
| Q14 | Immutable Ledger stays a 20% signature, not Armor? | Yes | Stops a third flat on Blockwyrm |
| Q15 | Shattered Armor and Core Breach stay catalog-only? | Yes. No pool edit. There is no Core Breach row [C-core] | Nothing, if the default holds |
| Q16 | HODLed: one ramp, cap +3, revert, immunity, no producer? | Yes | Backlog 8 |
| Q17 | Rugged cap 3? | Yes | Backlog 9 |
| Q18 | Narrow Dusted, Shattered, and Corroded, and revert owned stages on Defueled? | Yes | The "next" chemistry tidy |
| Q19 | STAB off, 1.1, or 1.2? | Off until typeless exclusion ships. If it comes on later, 1.1 [C-stab] | Do not bundle it with backlog 3 |
| Q20 | New Staked common, power 34, AP 2, ranged, replacing Pulse Shot on Rattlestake only? | Yes. Pulse Shot the move stays power 26 | Backlog 13 |
| Q21 | Leave Overleveraged shelved, with no Margin Loan? | Yes | Nothing. Self-damage stays shelved |
| Q22 | Patience cap +3, heal kept, Hopoch keeps it? | Yes | Backlog 11 |
| Q23 | Extraction Hit at Common replaces Baseline Slam on Kodiax? | Yes. Crystal Extraction stays | Backlog 12 |
| Q24 | Fee Collector at Common replaces Slipstream on Deghoul? | Yes. Not beside an Armor swap | Backlog 14 |
| Q25 | Leave Unstable Code, Firewall, and Automated Pressure out of pools until their caps exist? | Yes | Stops a +6 walk from entering a five |
| Q26 | Split OnMiss so Precision Focus is "you missed" and Reactive Ping is "you were missed"? | Yes | A small resolver branch |
| Q27 | Uplink stays the Node passive, card text only, no new 2 AP buff? | Yes. It is already granted [P-uplink] | Card text |
| Q28 | Demote Defensive Reflex to Rare and Sustain Regen to Common? | Yes. No pool change | Rarity ladder only |
| Q29 | Cipher Lock covers passive stage writes and Meme stages? | Yes | Backlog 15 |
| Q30 | Narration says the passive's name? | Yes | A template string |
| Q31 | Streamlined drops Def once, not on every action down to −6? | Yes | Backlog 10 |
| Q32 | Drop Gas War's +1 surcharge, keep 4 AP and ranged? | Yes. Do not also make it melee | The "next" list |
| Q33 | Bulwark Blow to 2 AP, power 50? | Yes | The "next" list. Turn-1 Def hit |
| Q34 | Cryo Ray's Speed −2 from 25% to 100%? | Yes. Power and AP stay | The "next" list |
| Q35 | Blade Flurry to 10×3? | Yes. Fox Feint stays the signature | The common stops beating the uncommon beside it |
| Q36 | On a quiet fight, Grind Tax lands on turn 4 and then every four turns? | Yes. That is the boss loop. Denial may push it | Boss AI horizon inside backlog 4 |
| Q37 | After Speed +6 and full HP, does the boss keep sipping? | No. Prefer the tax, or a Pass that satisfies Q7 | Boss clicks |
| Q38 | The "due" tell reads AP before the spend? | Yes | One narration fix |
| Q39 | How hard can a player turn Grind Tax off? | One denial slips it a turn. A chain must not delete it for the whole fight. Q1 is the bound | Pairs with Q1 |
| Q40 | Fox Feint is held until something is about to hit? | Yes | Utility tie-break |
| Q41 | Fix the Defensive ladder before any encounter uses it? | Yes | `FindBestSupport` and `FindLethalBlow` |
| Q42 | Hype Burst grants Ranged Atk, and strip cards say status buffs? | Yes | Backlog 7 |
| Q43 | System Reset's card stops saying the user skips the next action? | Yes. Recharge 2 already locks only that move [C-reset] | Backlog 6 |
| Q44 | Bench Relay, Pair Window, Stockpile Echo, or Boss Heat to 5 AP? | No this round. Grind Tax stays 6 | Nothing |
| Q45 | Secondary Node and Glitched commons for Validape and Kreechank? | No in the same change as typeless exclusion. Validape's membership stays | A later pass |
| Q46 | Ghost and Meme stay dormant, and FOMO's self tick stays? | Yes. FOMO is chemistry, not move self-damage | Nothing |

## Claim index

The checker requires every id. The lines above carry the ones the decisions use. The rest are the same run.

[M01] [M02] [M03] [M04] [M05] [M06] [M07] [M08] [M09] [M10] [M11] [M12] [M13] [M14] [M15] [M16] [M17] [M18] [M19] [M20] [M21] [M22] [M23] [M24] [W-ember_wall] [W-blockade] [W-ice_wall] [W-barrier_wall] [W-vapor_barrier] [W-compound_guard] [W-ash_guard] [W-lattice_guard] [W-guard] [W-ember-drain] [W-ice-ap] [W-lattice] [W-no-minus1] [W-engine-one-hit] [W-engine-one-active] [W-apply-no-override] [W-ice-faster] [W-ice-no-double-window] [W-test] [W-plan-stale] [W-assets] [W-gp-asset-key] [P-asset-nums] [P-move-ap] [P-uplink] [P-flagship-hammer] [P-withdrawn] [P-status-res] [P-grind-pool] [P-pulse] [P-kindling] [P-band] [P-move-count] [C-reset] [C-hype] [C-extract] [C-bug] [C-cinder] [C-empty] [C-extract-hit] [C-leak] [C-tax] [C-skim] [C-isolation] [C-rally] [C-refinery] [C-stream] [C-compress] [C-once-engine] [C-charged] [C-eot] [C-e28] [C-hodl] [C-hodl-summary] [C-jammed] [C-mev-comment] [C-mev-drain] [C-fud-comment] [C-backlash-summary] [C-backlash-comment] [C-typeless] [C-stab] [C-chem] [C-rugged] [C-kindled-literal] [C-ai-untargeted] [C-ai-blocks] [C-dice] [C-moves-txt] [C-gaswar] [C-bulwark] [C-heavies] [C-finish] [C-grind-text] [C-thread-text] [C-leak-text] [C-hammer-text] [C-core] [C-deflation] [C-tempo-div] [C-walls-div] [C-denial-mirror] [S-leak-pass] [S-leak-spend] [S-leak-cd] [S-leak-cd-spend] [S-taxskim] [S-cap-tax] [S-cap-leak] [S-deghoul] [S-victim] [S-w-ember] [S-w-block] [S-w-buffer] [S-w-ally] [S-w-ctrl] [S-w-both] [S-r-pass] [S-r-flurry] [S-r-inherit] [S-r-floor] [S-r-seam] [S-r-dig] [S-r-emberleak] [S-r-pressure] [S-r-kit] [S-r-bump] [S-r-bank] [S-q] [S-hp]

## Check tally

`issues_brief_check_v3.py` against this snapshot. **138 PASS / 0 FAIL.**
