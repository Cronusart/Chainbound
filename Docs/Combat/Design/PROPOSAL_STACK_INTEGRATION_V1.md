# PROPOSAL_STACK_INTEGRATION_V1

Proposals only. Nothing here is applied to the game. The source of truth is the combat snapshot `chainbound_combat_snapshot_20261023_0010` plus the round-18 sim attachment. The GitHub checkout was not used.

Checker: `proposal_stack_check.py`. Combined sim: `stack_2v2_sim.py`. Seed 20261023 [S-seed]. Archetype trials per pair 80 [S-field], type-face trials 48 [S-type-n], scripted trials 280 [S-script-n]. Move rows in the snapshot: 211 [F-move-count-n].

## Key findings

The pending defaults do not fight each other on the learn-pool band, the typed-versus-typeless rule, or the tempo rule, and they do open the Memory Leak passer lock. They do pile onto one team. Cinder Burst at 2 AP moves Burned from 5.7% [S-type-Burned-base] to 54.2% [S-type-Burned-stack] and moves the Blockwyrm plus Cinislug duo from 31.4% [S-arch-walls-base] to 97.5% [S-arch-walls-stack] in the archetype round robin (1120 [S-arch-walls-g] games a side). Holding Bulwark Blow at 3 AP, that duo is still 96.8% [S-sens-walls-stack]. The duo spike is the Cinder change. Cutting Bulwark to 2 AP is a second turn-1 heavy on a team that no longer needs one, and it takes Blockwyrm's stack clicks to bulwark_blow [S-spid-stack-blockwyrm-0] at 94.1% [S-sp-stack-blockwyrm-0]. The scripted Ember line is not a TRUE lock: the attacker still wins 81.8% [S-w-stack-ember_vs_ore-a]. The 1v2 control is 0.0% [S-w-baseline-ember_control_no_wall-a] attacker with the wall click absent and 0.0% [S-w-stack-ember_control_no_wall-a] under the stack, so that loss is the 1v2, not the wall. Two faster 2 AP hits still beat a Deep Dig banker at 100.0% [S-sc-baseline-two_fast_hits-fast] baseline and 100.0% [S-sc-stack-two_fast_hits-fast] under the full stack. Tax plus Skim on a passer stays a zero lock (0,0,0,0,0,0,0,0 [S-cal-taxskim_pass_base] and 0,0,0,0,0,0,0,0 [S-cal-taxskim_pass_stack]). Fee Collector does not deepen it. Armor flat 12 still ties the live 25% on the Hammer-plus-Blockade cell, so the flat numbers can sit on the card and should not enter a pool.

## Top 5 recommendations

1. **PROPOSAL.** Memory Leak `rechargeTurns` 2. Live recharge is 0 [F-leak-rc] at Moves.csv:95. Passer action AP goes from 2,2,2,2,2,2,2,2 [S-cal-leak_pass_base] to 2,2,4,4,6,6,8,8 [S-cal-leak_pass_stack]. A CSV value of 2 skips the next turn [F-rc-means-skip]. Do not lengthen Ember Wall. Its recharge column is already 2 [F-ember].
2. **PROPOSAL.** Transaction Tax `StealAP:1` becomes `DrainAP:1`. Live opcode steals [F-tax-opcode]. Deghoul's bank goes from 2,4,6,8,10,10,10,10 [S-cal-deghoul_base] to 1,2,3,4,5,6,7,8 [S-cal-deghoul_stack]. No drain cap, no whiff refund, no KO floor.
3. **PROPOSAL.** Typeless moves stay neutral. `ResolveAttackType` still returns `attacker.primaryType` when the move type is None DamageCalculator.cs:546 [F-inherit-line]. The comment still blesses inheritance [F-inherit-comment]. STAB stays off [F-stab-off]. Pulse Shot from Cinislug into Deghoul is chem 1.2 [S-f-pulse_inherited-chem] and 54 [S-f-pulse_inherited-dmg] damage today, and chem 1.0 [S-f-pulse_neutral-chem] and 45 [S-f-pulse_neutral-dmg] damage once inheritance is off. Pulse Shot stays power 26 [F-pulse].
4. **PROPOSAL.** Cinder Burst from 3 AP to 2 AP, power stays 15 [F-cinder]. Accept the Ember click shift and the duo spike named above. **PROPOSAL.** Do not also cut Bulwark Blow from 3 AP to 2. Live Bulwark is power 50, AP 3, and it strikes with Melee Def [F-bulwark] [F-bulwark-def]. This revises brief Q33.
5. **PROPOSAL.** Ship the three pool fills and the rarity riders that do not add a second membership change: Extraction Hit at Common replaces Body Check on Kodiax only, Fee Collector at Common replaces Slipstream on Deghoul only, Stake Line is the one new Staked common and replaces Pulse Shot on Rattlestake only, and the Ore / Yield / Stake / Fork / Combustive / Hype Train riders stay off any extra pool. Gas War's surcharge comes off once. Patience, Streamlined, Rugged, and HODLed take their caps. Armor flat 12 and flat 16 are written on the cards only. Guard Posture does not replace a Deghoul move. Guard Stance does not replace Stone Barrage.

## (a) Headline

Smallest set that makes the stack consistent:

- Ship Leak recharge, Tax-as-drain, typeless exclusion with STAB left off, and Cinder at 2 AP.
- Keep Bulwark at 3 AP. The duo spike survives without that cut, and the cut removes Stone Barrage from the greedy mix.
- Ship the pool fills and the riders. Do not put Armor, Combustive Shift, or Hype Train into a learn pool.
- Do not add a drain cap, a whiff refund, a KO floor, a longer Ember recharge, or a walls-P reboot. Tax plus Skim on a passer remains a zero lock. That was already accepted.

Out of band under the run's own rule (stack win rate outside 35–65, absolute delta at least 8, and either the baseline was inside the band or the stack pushed further from 50 by more than 7): Block, Compressed.
- Block: baseline 76.4% [S-type-Block-base], stack 86.6% [S-type-Block-stack], delta 10.2 [S-type-Block-delta].
- Compressed: baseline 34.9% [S-type-Compressed-base], stack 26.2% [S-type-Compressed-stack], delta -8.7 [S-type-Compressed-delta].
Bot moves toward the center, 72.4% [S-type-Bot-base] to 49.1% [S-type-Bot-stack], so it is not a new hole. Denial as a team falls from 76.8% [S-arch-denial-base] to 39.6% [S-arch-denial-stack]. The sensitivity with Bulwark held at 3 AP still shows denial at 40.7% [S-sens-denial-stack], so the drop is not the Bulwark cut. The walls duo takes those wins once Cinder is a turn-1 click. Greedy Keyper still clicks cipher_shot [S-spid-stack-keyper-0] at 100.0% [S-sp-stack-keyper-0]. Field denied-AP is not the denial measurement. The calendars are.

Match length, mean of the archetype pair means, goes from 4.25 [S-len-base] turns to 3.39 [S-len-stack]. AP spent per battle-turn goes from 5.178 [S-spent-base] to 5.261 [S-spent-stack]. AP denied per battle-turn goes from 0.035 [S-deny-base] to 0.076 [S-deny-stack]. Share of active turns that start behind a block goes from 0.0156 [S-block-base] to 0.0025 [S-block-stack]. The greedy policy stops walling once a cheap damaging click exists. Continuous coverage by recasting is still accepted. The new fact is that this policy barely uses the wall.

Modal click changes in the archetype field:

- cinislug: Ember Wall (`ember_wall [S-best-cinislug-bmove]`) 55.3% [S-best-cinislug-bshare] becomes Cinder Burst (`cinder_burst [S-best-cinislug-smove]`) 99.1% [S-best-cinislug-sshare].
- rattlestake: Pulse Shot (`baseline_shot [S-best-rattlestake-bmove]`) 86.1% [S-best-rattlestake-bshare] becomes stake_line (`stake_line [S-best-rattlestake-smove]`) 88.8% [S-best-rattlestake-sshare].
Blockwyrm's modal stays Bulwark Blow in the full stack, so it is not in that list. The share moves from 49.1% [S-sp-baseline-blockwyrm-0] `bulwark_blow [S-spid-baseline-blockwyrm-0]` to 94.1% [S-sp-stack-blockwyrm-0] `bulwark_blow [S-spid-stack-blockwyrm-0]`. With Bulwark held at 3 AP the stack modal is `stone_barrage [S-sens-spid-stack-blockwyrm-0]` at 52.0% [S-sens-sp-stack-blockwyrm-0].

## (b) Proposal inventory

Still-open rows are the brief v3 backlog, the brief's next list, and the rarity-fit backlog. Withdrawn ids are absent from `Moves.csv` and are not re-proposed. Decided rows that already landed are listed so they are not relitigated. Superseded levers from the brief ledger are dropped.

### Open

| Id | Source | System | Rows / files | Suggested default | Status |
|---|---|---|---|---|---|
| B01 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:204 [I-b01] | backlog | `Moves.csv` `memory_leak`, `Move_memory_leak.asset` description. No pool edit. Band unchanged | Q1, default yes. Memory Leak `rechargeTurns` 2. Window, −2, and Speed +2 stay. Card states the five limits in (d) | OPEN |
| B02 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:205 [I-b02] | backlog | `Moves.csv` `transaction_tax`. Deghoul flagship stays. Band unchanged | Q2, default yes. Transaction Tax `DrainAP:1`. Card: "Drain 1 AP from the target. You do not gain it." | OPEN |
| B03 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:206 [I-b03] | backlog | `DamageCalculator.cs` `ResolveAttackType` and `ResolveStabMultiplier`. Move panel stops showing an inherited type | None. Q19 only if someone wants STAB in the same change. Do not. Typeless moves are neutral. `ResolveAttackType` returns None's neutral path and does not use `primaryType`. STAB stays off | OPEN |
| B04 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:207 [I-b04] | backlog | `AIHelpers.GetLegalActions`, `EstimateDamage` sibling, `UtilityStatusAI.ScoreAction`, `AggressiveAI` | Q7. AI aims, estimates, and scores denial | OPEN |
| B05 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:208 [I-b05] | backlog | `Moves.csv` `cinder_burst`, the asset, regenerate `move_ap.ts`. Cinislug membership unchanged | Q9, default yes. Cinder Burst AP 2, power 15 | OPEN |
| B06 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:209 [I-b06] | backlog | Descriptions on `system_reset`, `grind_tax`, `memory_leak`, `thread_lock`, `hammer_sync`, `transaction_tax`. `StatusId.cs` MEV comment | None, except Tax's sentence lands with row 2. Card text states the limit | OPEN |
| B07 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:210 [I-b07] | backlog | `Moves.csv` `hype_burst` stage stat and description. Pool unchanged | Q42, default yes. Hype Burst grants Ranged Atk, and the tooltip says status buffs | OPEN |
| B08 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:211 [I-b08] | backlog | `StatusProcessor.cs` HODLed block, `Statuses.csv` HODLed, `CombatDecisionRoundTests` assertion of +2 | Q16, default yes. HODLed: one owned ramp, cap +3, revert, foe drops filtered | OPEN |
| B09 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:212 [I-b09] | backlog | `CombatConstants.RUGGED_TICK_CAP`, resolver config `ruggedTickCap`, chemistry spec, the test that asserts 15 | Q17, default yes. Rugged cap 3 | OPEN |
| B10 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:213 [I-b10] | backlog | `Moves.csv` `streamlined_flow`: `TriggerOnce` true, stage cap −1. Discount stays. Humpacked's signature stays | Q31, default yes. Streamlined drops Def once | OPEN |
| B11 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:214 [I-b11] | backlog | `Moves.csv` `patience_passive` cap field `6` → `3`. Hopoch's pool unchanged. Rare under Legendary Yield Spike | Q22, default yes. Patience cap +3 | OPEN |
| B12 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:215 [I-b12] | backlog | `Moves.csv` `extraction_hit` rarity Common, OnHit, 40% Melee Atk +1 cap +3, Speed +1 once at +3. `SpeciesMovePools.csv` Kodiax. Band: Common damaging move remains Pickaxe Flurry. Signature stays Ore Strike | Q23, default yes. Build Extraction Hit at Common. Replace `baseline_slam` on Kodiax | OPEN |
| B13 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:216 [I-b13] | backlog | New move row plus `SpeciesMovePools.csv` Rattlestake. Five stays five. Signature stays AP Reserve. The one step-up stays Compound Barrier | Q20, default yes. One Staked common, power 34, AP 2, ranged, accuracy 95. Replace `baseline_shot` on Rattlestake only | OPEN |
| B14 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:217 [I-b14] | backlog | `Moves.csv` rarity, `SpeciesMovePools.csv` Deghoul. Signature stays Transaction Tax. The one Uncommon stays Gas War. Gas Bump stays the Common damaging move | Q24, default yes. Not in the same edit as an Armor swap. Fee Collector becomes Common and replaces Slipstream on Deghoul | OPEN |
| B15 | COMBAT_ISSUES_DECISION_BRIEF_V3.md:218 [I-b15] | backlog | `CombatResolver.cs` passive stage loop and `ApplyMemeStage`, through `BuffGate.FilterPositiveStage` | Q29, default yes. Cipher Lock filters positive stages from the passive stage column and from Meme stages | OPEN |
| N01 | COMBAT_ISSUES_DECISION_BRIEF_V3.md next list [I-n01] | later costs / chemistry | see the sentence | Bulwark Blow to 2 AP, power 50, Def strike kept, so the Epic is a turn-1 click . | REVISED this round. Keep Bulwark at 3 AP. The brief's 2 AP default is the click monopoly, and it is not what makes the duo win. |
| N02 | COMBAT_ISSUES_DECISION_BRIEF_V3.md next list [I-n02] | later costs / chemistry | see the sentence | Delete Gas War's surcharge . | OPEN |
| N03 | COMBAT_ISSUES_DECISION_BRIEF_V3.md next list [I-n03] | later costs / chemistry | see the sentence | Narrow Dusted, Shattered, and Corroded, and make Defueled revert owned stages. | OPEN |
| N04 | COMBAT_ISSUES_DECISION_BRIEF_V3.md next list [I-n04] | later costs / chemistry | see the sentence | Blade Flurry from 12×3 to 10×3. Kitsauto's signature stays Fox Feint. | OPEN |
| N05 | COMBAT_ISSUES_DECISION_BRIEF_V3.md next list [I-n05] | later costs / chemistry | see the sentence | Cryo Ray's Speed −2 becomes 100%. Power and AP stay. | OPEN |
| R01 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:418 [I-r01] | rarity rider | Moves.csv:131, Move_ore_strike.asset, card | Q1. Ore Strike Def −1 at 25%, once. | OPEN |
| R02 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:419 [I-r02] | rarity rider | Moves.csv:145 and :143 | none. Yield Spike lifesteal 20%. Stake Strike lifesteal 15%. | OPEN |
| R03 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:420 [I-r03] | rarity rider | Moves.csv:77 | Q3. Combustive Shift DrainAP 1 at 50%, once. | OPEN |
| R04 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:421 [I-r04] | rarity rider | Moves.csv:195 | Q2. Hype Train applies Surging at 100%. Drop the TRIG tag. | OPEN |
| R05 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:422 [I-r05] | rarity rider | Moves.csv:118, :187, :123 | Q4. Fork Split, Shard Scatter, Chain Fork: Speed −1 at 20%, once. | OPEN |
| R06 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:423 [I-r06] | rarity rider | same rows, Description column | 1–5. Rewrite the card sentence for each row in (d) to the Description stored on the proposal. | OPEN |
| R07 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:424 [I-r07] | rarity rider | Moves.csv:72 | Q6. Drop Gas War's self `NextMoveAPSurcharge`. | MERGED into Q32. Apply the surcharge drop once |
| R08 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:425 [I-r08] | rarity rider | none | Q5. Leave Lean Hit plain. | OPEN |
| R09 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:426 [I-r09] | rarity rider | none | Q10. Leave Titan Crash and Rail Spike plain. | OPEN |
| R10 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:427 [I-r10] | rarity rider | none | Q9. Do not rebuild Status Resistance. | DECIDED. Status Resistance stays retired. Do not rebuild |
| R11 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:428 [I-r11] | rarity rider | Moves.csv:116, :172, :78 | none. Silent PRIOR cards: none. Gate Crystal Focus with `ResolveConditions=Self:APAtMost:0`. Drop TRIG from Crash Routine. Drop APMAN from Vapor Barrier. No power change. | OPEN |
| R12 | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:429 [I-r12] | rarity rider | Move_*.asset via the importer | 1–5. Re-import moves after the CSV edits and require asset power, AP, accuracy, and rarity to match. Today the checker sees 0 mismatches out of 211 assets (C18). | OPEN |

### Already decided, and where the data stands

| Item | Data today | Status |
|---|---|---|
| Walls and barriers: melee vs ranged, one hit, recharge column 2 means one skipped turn, one block, priority does not break a wall | Ember Wall is BlockWindow 50% melee, PRIOR, recharge 2 [F-ember] [F-ember-window] [F-rc-means-skip] | DECIDED, landed |
| Uplink replaces AP Router on Validape | [F-uplink] | DECIDED, landed |
| Kindling 6%, Pulse Shot 26, Status Resistance retired, withdrawn passives stay out | Kindling literal is 0.06f before Rugged [F-kindling-literal]. Pulse Shot is 26 [F-pulse]. Withdrawn ids are absent. | DECIDED, landed |
| System Reset, Hype Burst, and Crystal Extraction are built | Crystal Extraction has a heal payload [F-crystal-built]. Hype Burst still grants Melee Atk [F-hype-melee]. The Ranged Atk rewrite is still open (B07 / Q42). | DECIDED that they exist. Hype's stat is OPEN |
| All-In wipes the caster and the active ally | Master decision [F-allin-decided]. CSV utility is empty and the tag spends only the caster [F-allin-utility-empty] [F-allin-caster-only]. Not in any pool [F-allin-unpooled]. | DECIDED, not landed. No pool. No meta effect until it is pooled, and no pool is proposed |
| Guard Posture is Armor, not a wall | The card is still a 25% window [F-guard-25]. Flat 12 / 16 is open (Q10) and is not decided. | Shape DECIDED. Numbers OPEN |
| Heavies stay at 4 AP. Self-damage stays shelved. Boss-only moves stay out of pools | Gas War is 4 AP ranged [F-gaswar]. Grind Tax carries BOSS_ONLY. All-In is unpooled [F-allin-unpooled]. | DECIDED |

### Dropped

| Item | Why it is not in the open table |
|---|---|
| Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, Open Line, Cinder Idle, Scorch Echo | Absent from Moves.csv. [F-absent-partner_beacon], [F-absent-relay_dividend], [F-absent-node_beacon], [F-absent-link_pulse], [F-absent-open_line], [F-absent-cinder_idle], [F-absent-scorch_echo] |
| Walls-P full block, drain cap of 2, whiff refund, KO floor of 2, heavies at 6 AP, longer Ember recharge, Margin Loan, Pair Ping, Mind Fault, secondary commons this round | Brief v3 already reconciled these to no. This combined run does not revive them. |

Brief ledger rows parsed: 32 [I-ledger-rows]. Brief questions parsed: Q1–Q46 [I-brief-q-count]. Rarity questions parsed: Q1–Q10 [I-rarity-q-count].

Live pools the open swaps touch:

- kodiax signature `ore_strike`, members `ore_strike|deep_dig|pickaxe_flurry|crystal_extraction|baseline_slam` [F-pool-kodiax].
- deghoul signature `transaction_tax`, members `transaction_tax|volatile_shot|gas_bump|slipstream|baseline_striker` [F-pool-deghoul].
- rattlestake signature `ap_reserve`, members `ap_reserve|compound_guard|baseline_shot|baseline_jab|baseline_thrust` [F-pool-rattlestake].
Kodiax already has Crystal Extraction and does not have Extraction Hit [F-kodiax-crystal-stays]. Fee Collector is not on Deghoul [F-fee-ungranted]. Damaging Staked commons, boss rows excluded: 0 [F-no-staked-common] .

## (c) Conflict matrix

Each row is a pair that lands on the same move, the same status, the same AP lever, or the same resolver step when the suggested defaults are on together. The resolution is a **PROPOSAL**.

| Pair | Relation | Resolution |
|---|---|---|
| Cinder AP 2 and Ember Wall on Cinislug | Stack. Different moves. The greedy click switches from Ember to Cinder. Baseline top is `ember_wall [S-spid-baseline-cinislug-0]` 55.3% [S-sp-baseline-cinislug-0]. Stack top is `cinder_burst [S-spid-stack-cinislug-0]` 99.1% [S-sp-stack-cinislug-0]. | **PROPOSAL.** Ship both. Ember stays the signature and the wall. Do not lengthen its recharge. The unused wall is not a lock. |
| Cinder AP 2 and Bulwark AP 2 on the walls duo | The duo spike is Cinder. Sensitivity walls stack 96.8% [S-sens-walls-stack] with Bulwark left at 3, against full-stack walls 97.5% [S-arch-walls-stack]. Bulwark at 2 AP then monopolises the Blockwyrm click. | **PROPOSAL.** Ship Cinder. Keep Bulwark at 3 AP. Do not put a second turn-1 heavy on this duo. |
| Bulwark AP 2 and Stone Barrage | Contradict the mix. At 3 AP the sensitivity stack still clicks `stone_barrage [S-sens-spid-stack-blockwyrm-0]`. At 2 AP the full stack's top is `bulwark_blow [S-spid-stack-blockwyrm-0]` 94.1% [S-sp-stack-blockwyrm-0]. | **PROPOSAL.** Keep 3 AP so Stone Barrage remains a real click. |
| Armor flat 12 and a 60% wall on one hit | Stack, and they tie. Hammer open 115 [S-f-hammer_open-dmg] (chem 0.8 [S-f-hammer_open-chem]). Wall plus today's 25% leaves 34 [S-f-hammer_wall_and_percent-dmg]. Wall plus flat 12 leaves 34 [S-f-hammer_wall_and_flat12-dmg]. [S-f-flats-match]. Flat 12 alone leaves 103 [S-f-hammer_flat12-dmg]. | **PROPOSAL.** Write flat 12 on Guard Posture and flat 16 on Guard Stance. Do not retune 12. Do not equip either card. |
| Guard Stance plus Immutable Ledger plus a wall, only if Q13 is yes | Triple-count on one hit. Hammer into Blockwyrm through a 60% wall, flat 16, and Ledger's 20% leaves 6 [S-f-triple_if_q13-dmg]. Still damage, not a zero. | **PROPOSAL.** Q13 stays no. Ledger stays a 20% signature, not Armor. |
| Fee Collector, Tax-as-drain, and Packet Skim | Same AP lever. A passer who is not ahead of Deghoul stays at 0,0,0,0,0,0,0,0 [S-cal-fee_tax_skim_stack], the same zero as today 0,0,0,0,0,0,0,0 [S-cal-fee_tax_skim]. Fee only drains when the foe's AP is strictly higher [F-fee-rule]. A pre-built bank of 4 is held at 3,2,2,2,2,2,2,2 [S-cal-fee_from_bank_stack]. | **PROPOSAL.** Ship Fee Collector and the Tax drain. Do not add a cap. Do not put Fee Collector in the same edit as an Armor swap. |
| Leak recharge and the Tax plus Skim zero lock | They do not fix each other. Leak opens a passer (2,2,4,4,6,6,8,8 [S-cal-leak_pass_stack]). Tax plus Skim stays zeros. A body that spends the open turn stays on 2,0,2,0,2,0,2,0 [S-cal-leak_spend_stack]. | **PROPOSAL.** Ship the recharge anyway. Do not relitigate the cap. |
| Gas War surcharge, brief Q32 and rarity Q6 | Same row `volatile_shot`. Live surcharge is on [F-gaswar-surcharge], power 72, AP 4, ranged [F-gaswar]. | **PROPOSAL.** Delete `NextMoveAPSurcharge` once. Do not also make it melee. |
| Typeless exclusion, Pulse Shot at 26, and Stake Line | Stack. Pulse Shot the move stays 26. Only Rattlestake's pool swaps. | **PROPOSAL.** Ship all three. The typed-common cell still wins. See (f). |
| Ore Strike's Def rider and Kodiax's AP | Different levers. The rider is a 25% Def nick on the signature. Deep Dig's cost does not change. | **PROPOSAL.** Ship the rider. Leave Deep Dig at 4 AP. |
| Combustive Shift's drain and Deghoul's Tax | Would double-count a Gas drain if Combustive entered Deghoul's five. | **PROPOSAL.** Ship the drain on the card. Leave it out of every Gas pool. |
| Hype Train's Surging and Hype Burst | Different moves. Hype Train is Meme and has no species. Hype Burst's open rewrite is Ranged Atk, which is the stat Cinder uses. | **PROPOSAL.** Ship Surging on Hype Train and leave it unpooled. Ship Hype Burst's Ranged Atk. |
| Blade Flurry 10×3, a 50% wall, and flat 12 | The wall and the flat spend on one hit. Open baseline hit 13 [S-f-blade_base_open], first hit behind the wall and flat 12 is 1 [S-f-blade_base_first]. Stack power 10 open hit 11 [S-f-blade_stack_open], first hit 1 [S-f-blade_stack_first]. The per-hit loop spends the block on that hit [S-f-blade-rest], so the later hits are not mitigated. | **PROPOSAL.** Ship 10×3. Do not let Armor change the later hits. |
| Patience cap +3 and a 4 AP bank | The cap stops the +6 Def walk on a passer. It does not change the heavy's cost. | **PROPOSAL.** Ship the cap. The tempo table is the check. |
| Chemistry narrow (Dusted, Shattered, Corroded, HODLed, Rugged) and new status producers | The narrow edits change bodies that already exist. Mind Fault, Unsealed, and the other new producers are not in the default stack. | **PROPOSAL.** Narrow the bodies. Do not add producers this round. |
| Cipher Lock's stage filter and self buffs | The filter is positive stages on a locked foe. Patience and Hype Burst write on the user. | **PROPOSAL.** Ship the filter. It does not eat the user's own buff. |
| Rugged cap 3 and typeless exclusion | Same chemistry step, no double-count. Kindling's 6% literal runs before Rugged [F-kindling-literal]. Passives run before that [F-eot-order]. A typed Cinder into Gas is chem 1.2 [S-f-cinder_into_gas-chem]. A neutral Pulse Shot is chem 1.0 [S-f-pulse_neutral-chem] and does not start Rugged. Live Rugged cap constant is 15 CombatConstants.cs:92 [F-rugged-15]. | **PROPOSAL.** Ship the cap and the exclusion. Do not reorder ticks. |
| Ember's 1 AP drain and Bot denial | Decided: a wall grants at most 1 AP. After Cinder exists, greedy Cinislug barely casts Ember. | **PROPOSAL.** Leave the drain. Do not add a second denial rider to Ember. |
| Yield Spike 20% lifesteal and Stake Strike 15% | Same rider family, two percents. Yield stays higher. | **PROPOSAL.** Ship both percents. Do not swap either move's pool slot for this. |
| All-In's ally wipe and the denial stack | All-In is in no pool [F-allin-unpooled]. The wipe cannot change these win rates until a Meme body exists. | **PROPOSAL.** Implement the decided ally wipe. Do not add a pool. |

## (d) Combined 2v2

The combined sim extends `ap_denial_2v2_sim.py`. Two attached sims still diverge and were not used for rates. `tempo_2v2_sim.py` parses MitigationWindow and does not mention BlockWindow [F-tempo-no-block]. `walls_pvp_v2_sim.py` does the same [F-walls-no-block]. The denial sim is the BlockWindow mirror [F-denial-block].

Fixes applied to both the baseline and the stack, because the denial sim omitted them and the snapshot engine does not: authored stage changes and on-hit riders, AllEnemies hitting every living foe with the cost paid once, separate melee and ranged stages, Bulwark striking with Melee Def [F-sim-def-scale], end step order of passive ticks then Kindling then chemistry [F-eot-order], recharge on the bench [F-bench-recharge], and Fee Collector's strict-ahead drain [F-fee-rule]. Baseline rates will not match an older denial write-up. The calendars were recomputed on this engine.

The stack overlay is data: Leak recharge 2, Tax as drain, typeless neutrality, Cinder AP 2, Hype Burst on Ranged Atk, Patience cap 3, Streamlined once and cap −1, Bulwark AP 2 in this full stack only, Blade Flurry power 10, Cryo Ray slow at 100%, Gas War surcharge removed, the rarity riders, Extraction Hit and Fee Collector retiered, virtual Stake Line, Armor flats on the cards and not in a kit, Rugged cap 3, and the narrowed Dusted / Shattered / Corroded / HODLed bodies. STAB stays off. Ghost and Cofi are not in the field.

### Archetype round robin

Eight teams, both directions. Games per side 1120 [S-arch-heavy-g].

| Archetype | Baseline | Stack | Delta |
|---|---:|---:|---:|
| heavy | 81.0% [S-arch-heavy-base] | 78.0% [S-arch-heavy-stack] | -3.0 [S-arch-heavy-delta] |
| tempo | 60.4% [S-arch-tempo-base] | 55.1% [S-arch-tempo-stack] | -5.3 [S-arch-tempo-delta] |
| denial | 76.8% [S-arch-denial-base] | 39.6% [S-arch-denial-stack] | -37.2 [S-arch-denial-delta] |
| walls | 31.4% [S-arch-walls-base] | 97.5% [S-arch-walls-stack] | 66.1 [S-arch-walls-delta] |
| stake | 52.4% [S-arch-stake-base] | 49.1% [S-arch-stake-stack] | -3.3 [S-arch-stake-delta] |
| fork | 30.3% [S-arch-fork-base] | 36.5% [S-arch-fork-stack] | 6.2 [S-arch-fork-delta] |
| frozen | 45.8% [S-arch-frozen-base] | 27.2% [S-arch-frozen-stack] | -18.6 [S-arch-frozen-delta] |
| gas | 21.9% [S-arch-gas-base] | 16.9% [S-arch-gas-stack] | -5.0 [S-arch-gas-delta] |

### Type faces

Each face brings Humpacked, except Humpacked brings Rattlestake. Games per type 576 [S-type-Node-g]. Type-face picks are not in the most-picked table. Humpacked was the universal partner and was inflating those counts.

| Type | Baseline | Stack | Delta |
|---|---:|---:|---:|
| Node | 66.3% [S-type-Node-base] | 60.1% [S-type-Node-stack] | -6.2 [S-type-Node-delta] |
| Gas | 16.8% [S-type-Gas-base] | 19.1% [S-type-Gas-stack] | 2.3 [S-type-Gas-delta] |
| Block | 76.4% [S-type-Block-base] | 86.6% [S-type-Block-stack] | 10.2 [S-type-Block-delta] |
| Encrypted | 32.3% [S-type-Encrypted-base] | 30.2% [S-type-Encrypted-stack] | -2.1 [S-type-Encrypted-delta] |
| Burned | 5.7% [S-type-Burned-base] | 54.2% [S-type-Burned-stack] | 48.5 [S-type-Burned-delta] |
| Frozen | 68.6% [S-type-Frozen-base] | 64.9% [S-type-Frozen-stack] | -3.7 [S-type-Frozen-delta] |
| Forked | 43.9% [S-type-Forked-base] | 42.7% [S-type-Forked-stack] | -1.2 [S-type-Forked-delta] |
| Mined | 45.0% [S-type-Mined-base] | 37.5% [S-type-Mined-stack] | -7.5 [S-type-Mined-delta] |
| Staked | 78.0% [S-type-Staked-base] | 77.1% [S-type-Staked-stack] | -0.9 [S-type-Staked-delta] |
| Compressed | 34.9% [S-type-Compressed-base] | 26.2% [S-type-Compressed-stack] | -8.7 [S-type-Compressed-delta] |
| Bot | 72.4% [S-type-Bot-base] | 49.1% [S-type-Bot-stack] | -23.3 [S-type-Bot-delta] |
| Shard | 75.2% [S-type-Shard-base] | 67.0% [S-type-Shard-stack] | -8.2 [S-type-Shard-delta] |
| Glitched | 34.5% [S-type-Glitched-base] | 35.2% [S-type-Glitched-stack] | 0.7 [S-type-Glitched-delta] |

### Length, AP, blocks

| Measure | Baseline | Stack |
|---|---:|---:|
| Mean match length | 4.25 [S-len-base] | 3.39 [S-len-stack] |
| AP spent per battle-turn | 5.178 [S-spent-base] | 5.261 [S-spent-stack] |
| AP denied per battle-turn | 0.035 [S-deny-base] | 0.076 [S-deny-stack] |
| Turns starting behind a block | 0.0156 [S-block-base] | 0.0025 [S-block-stack] |

### Most-picked moves by rarity

Archetype games only.

| Rarity | Baseline | Stack |
|---|---|---|
| Common | Gas Bump (`gas_bump`) 25.5% [S-pick-baseline-Common-0]; Zipped Strikes (`zipped_strikes`) 21.4% [S-pick-baseline-Common-1]; Pulse Shot (`baseline_shot`) 16.1% [S-pick-baseline-Common-2] | Gas Bump (`gas_bump`) 27.5% [S-pick-stack-Common-0]; Zipped Strikes (`zipped_strikes`) 24.0% [S-pick-stack-Common-1]; Blade Flurry (`blade_flurry`) 16.3% [S-pick-stack-Common-2] |
| Uncommon | Cipher Shot (`cipher_shot`) 41.6% [S-pick-baseline-Uncommon-0]; Gas War (`volatile_shot`) 21.8% [S-pick-baseline-Uncommon-1]; Multi-Thread Shot (`multi_thread_shot`) 13.4% [S-pick-baseline-Uncommon-2] | Cipher Shot (`cipher_shot`) 46.5% [S-pick-stack-Uncommon-0]; Multi-Thread Shot (`multi_thread_shot`) 17.1% [S-pick-stack-Uncommon-1]; Gas War (`volatile_shot`) 13.7% [S-pick-stack-Uncommon-2] |
| Rare | Seam Cut (`seam_cut`) 35.8% [S-pick-baseline-Rare-0]; Partition Shot (`partition_shot`) 32.1% [S-pick-baseline-Rare-1]; Frost Shot (`frost_shot`) 19.0% [S-pick-baseline-Rare-2] | Seam Cut (`seam_cut`) 38.2% [S-pick-stack-Rare-0]; Partition Shot (`partition_shot`) 33.9% [S-pick-stack-Rare-1]; Frost Shot (`frost_shot`) 18.7% [S-pick-stack-Rare-2] |
| Epic | Bulwark Blow (`bulwark_blow`) 77.2% [S-pick-baseline-Epic-0]; Transaction Tax (`transaction_tax`) 22.8% [S-pick-baseline-Epic-1] | Bulwark Blow (`bulwark_blow`) 100.0% [S-pick-stack-Epic-0] |
| Legendary | Yield Spike (`yield_spike`) 59.3% [S-pick-baseline-Legendary-0]; Ember Wall (`ember_wall`) 29.8% [S-pick-baseline-Legendary-1]; Cryo Ray (`cryo_ray`) 7.8% [S-pick-baseline-Legendary-2] | Yield Spike (`yield_spike`) 88.0% [S-pick-stack-Legendary-0]; Cryo Ray (`cryo_ray`) 6.9% [S-pick-stack-Legendary-1]; Fork Split (`fork_split`) 4.7% [S-pick-stack-Legendary-2] |

Epic under the full stack is only Bulwark Blow. That is the click monopoly Q33 should not ship. Common's third place moves from Pulse Shot to Blade Flurry because Rattlestake no longer clicks Pulse Shot. Gas Bump stays the most-picked Common in both modes.

### Species clicks that matter

| Species | Baseline | Stack |
|---|---|---|
| cinislug | Ember Wall (`ember_wall [S-spid-baseline-cinislug-0]`) 55.3% [S-sp-baseline-cinislug-0]; Hype Burst (`hype_burst [S-spid-baseline-cinislug-1]`) 43.9% [S-sp-baseline-cinislug-1]; Cinder Burst (`cinder_burst [S-spid-baseline-cinislug-2]`) 0.7% [S-sp-baseline-cinislug-2] | Cinder Burst (`cinder_burst [S-spid-stack-cinislug-0]`) 99.1% [S-sp-stack-cinislug-0]; Ember Wall (`ember_wall [S-spid-stack-cinislug-1]`) 0.9% [S-sp-stack-cinislug-1] |
| blockwyrm | Bulwark Blow (`bulwark_blow [S-spid-baseline-blockwyrm-0]`) 49.1% [S-sp-baseline-blockwyrm-0]; Stone Barrage (`stone_barrage [S-spid-baseline-blockwyrm-1]`) 40.2% [S-sp-baseline-blockwyrm-1]; Blockade (`blockade [S-spid-baseline-blockwyrm-2]`) 10.8% [S-sp-baseline-blockwyrm-2] | Bulwark Blow (`bulwark_blow [S-spid-stack-blockwyrm-0]`) 94.1% [S-sp-stack-blockwyrm-0]; Blockade (`blockade [S-spid-stack-blockwyrm-1]`) 5.9% [S-sp-stack-blockwyrm-1] |
| rattlestake | Pulse Shot (`baseline_shot [S-spid-baseline-rattlestake-0]`) 86.1% [S-sp-baseline-rattlestake-0]; Compound Barrier (`compound_guard [S-spid-baseline-rattlestake-1]`) 8.5% [S-sp-baseline-rattlestake-1]; Quick Jab (`baseline_jab [S-spid-baseline-rattlestake-2]`) 5.4% [S-sp-baseline-rattlestake-2] | stake_line (`stake_line [S-spid-stack-rattlestake-0]`) 88.8% [S-sp-stack-rattlestake-0]; Compound Barrier (`compound_guard [S-spid-stack-rattlestake-1]`) 11.2% [S-sp-stack-rattlestake-1] |
| keyper | Cipher Shot (`cipher_shot [S-spid-baseline-keyper-0]`) 100.0% [S-sp-baseline-keyper-0] | Cipher Shot (`cipher_shot [S-spid-stack-keyper-0]`) 100.0% [S-sp-stack-keyper-0] |
| humpacked | Zipped Strikes (`zipped_strikes [S-spid-baseline-humpacked-0]`) 100.0% [S-sp-baseline-humpacked-0] | Zipped Strikes (`zipped_strikes [S-spid-stack-humpacked-0]`) 100.0% [S-sp-stack-humpacked-0] |
| kitsauto | Blade Flurry (`blade_flurry [S-spid-baseline-kitsauto-0]`) 85.4% [S-sp-baseline-kitsauto-0]; Multi-Thread Shot (`multi_thread_shot [S-spid-baseline-kitsauto-1]`) 14.6% [S-sp-baseline-kitsauto-1] | Blade Flurry (`blade_flurry [S-spid-stack-kitsauto-0]`) 86.2% [S-sp-stack-kitsauto-0]; Multi-Thread Shot (`multi_thread_shot [S-spid-stack-kitsauto-1]`) 13.8% [S-sp-stack-kitsauto-1] |
| deghoul | Gas Bump (`gas_bump [S-spid-baseline-deghoul-0]`) 76.6% [S-sp-baseline-deghoul-0]; Gas War (`volatile_shot [S-spid-baseline-deghoul-1]`) 15.9% [S-sp-baseline-deghoul-1]; Transaction Tax (`transaction_tax [S-spid-baseline-deghoul-2]`) 7.6% [S-sp-baseline-deghoul-2] | Gas Bump (`gas_bump [S-spid-stack-deghoul-0]`) 91.5% [S-sp-stack-deghoul-0]; Gas War (`volatile_shot [S-spid-stack-deghoul-1]`) 8.5% [S-sp-stack-deghoul-1] |

Humpacked's greedy click does not change. Frozen's archetype drop is the field getting Cinder and Stake Line while that click stays put. Compressed was already near the floor and is pushed further. That is a watch item, not a new Compressed move. Q45 stays no.

### Sensitivity: Bulwark left at 3 AP

Smaller sample. Games per archetype side 560 [S-sens-walls-g].

| Slice | Baseline | Stack at Bulwark AP 3 | Delta |
|---|---:|---:|---:|
| archetype walls | 33.6% [S-sens-walls-base] | 96.8% [S-sens-walls-stack] | 63.2 [S-sens-walls-delta] |
| archetype denial | 78.2% [S-sens-denial-base] | 40.7% [S-sens-denial-stack] | -37.5 [S-sens-denial-delta] |
| type Burned | 6.2% [S-sens-type-Burned-base] | 53.5% [S-sens-type-Burned-stack] | 47.3 [S-sens-type-Burned-delta] |
| type Block | 77.4% [S-sens-type-Block-base] | 84.0% [S-sens-type-Block-stack] | 6.6 [S-sens-type-Block-delta] |
| type Bot | 70.1% [S-sens-type-Bot-base] | 50.7% [S-sens-type-Bot-stack] | -19.4 [S-sens-type-Bot-delta] |
| type Compressed | 35.1% [S-sens-type-Compressed-base] | 26.7% [S-sens-type-Compressed-stack] | -8.4 [S-sens-type-Compressed-delta] |

Blockwyrm under that sensitivity:

- baseline: Bulwark Blow (`bulwark_blow [S-sens-spid-baseline-blockwyrm-0]`) 49.1% [S-sens-sp-baseline-blockwyrm-0]; Stone Barrage (`stone_barrage [S-sens-spid-baseline-blockwyrm-1]`) 40.3% [S-sens-sp-baseline-blockwyrm-1]; Blockade (`blockade [S-sens-spid-baseline-blockwyrm-2]`) 10.6% [S-sens-sp-baseline-blockwyrm-2]
- stack: Stone Barrage (`stone_barrage [S-sens-spid-stack-blockwyrm-0]`) 52.0% [S-sens-sp-stack-blockwyrm-0]; Bulwark Blow (`bulwark_blow [S-sens-spid-stack-blockwyrm-1]`) 42.8% [S-sens-sp-stack-blockwyrm-1]; Blockade (`blockade [S-sens-spid-stack-blockwyrm-2]`) 5.2% [S-sens-sp-stack-blockwyrm-2]

### Wall lines

Attacker is team 0. A TRUE lock is a repeating rule that keeps a side from a legal winning action, or a wall line the attacker never wins. A waller who spends turns and loses the race is not a lock. A 1v2 that is a loss with or without the wall is not a lock. Recasting is accepted.

| Line | Mode | Attacker | Waller | Cap | Length |
|---|---|---:|---:|---:|---:|
| ember_vs_ore | baseline | 96.4% [S-w-baseline-ember_vs_ore-a] | 3.6% [S-w-baseline-ember_vs_ore-w] | 0.0% [S-w-baseline-ember_vs_ore-cap] | 4.30 [S-w-baseline-ember_vs_ore-len] |
| ember_vs_ore | stack | 81.8% [S-w-stack-ember_vs_ore-a] | 18.2% [S-w-stack-ember_vs_ore-w] | 0.0% [S-w-stack-ember_vs_ore-cap] | 3.49 [S-w-stack-ember_vs_ore-len] |
| blockade_vs_ore | baseline | 99.6% [S-w-baseline-blockade_vs_ore-a] | 0.4% [S-w-baseline-blockade_vs_ore-w] | 0.0% [S-w-baseline-blockade_vs_ore-cap] | 3.23 [S-w-baseline-blockade_vs_ore-len] |
| blockade_vs_ore | stack | 98.9% [S-w-stack-blockade_vs_ore-a] | 1.1% [S-w-stack-blockade_vs_ore-w] | 0.0% [S-w-stack-blockade_vs_ore-cap] | 3.20 [S-w-stack-blockade_vs_ore-len] |
| ember_control_no_wall | baseline | 0.0% [S-w-baseline-ember_control_no_wall-a] | 100.0% [S-w-baseline-ember_control_no_wall-w] | 0.0% [S-w-baseline-ember_control_no_wall-cap] | 2.58 [S-w-baseline-ember_control_no_wall-len] |
| ember_control_no_wall | stack | 0.0% [S-w-stack-ember_control_no_wall-a] | 100.0% [S-w-stack-ember_control_no_wall-w] | 0.0% [S-w-stack-ember_control_no_wall-cap] | 1.21 [S-w-stack-ember_control_no_wall-len] |

No new wall TRUE lock. Ember versus Ore Strike still has an attacker win under the stack. The control 1v2 is a loss in both modes.

### Denial calendars

Action-time AP for eight turns. These are the lock measurement. The greedy field barely clicks Leak or Tax, which is why denied-AP per turn stays near zero.

| Calendar | Series | Reading |
|---|---|---|
| leak_pass_base | 2,2,2,2,2,2,2,2 [S-cal-leak_pass_base] | Passer finisher lock. Hammer never. |
| leak_pass_stack | 2,2,4,4,6,6,8,8 [S-cal-leak_pass_stack] | Opens. Hammer is payable on turn 3. |
| leak_spend_base | 2,0,0,0,0,0,0,0 [S-cal-leak_spend_base] | Zero lock after one spend. |
| leak_spend_stack | 2,0,2,0,2,0,2,0 [S-cal-leak_spend_stack] | Recharge frees a passer, not a body that spends the open turn. |
| taxskim_pass_base | 0,0,0,0,0,0,0,0 [S-cal-taxskim_pass_base] | Zero lock. Already accepted. |
| taxskim_pass_stack | 0,0,0,0,0,0,0,0 [S-cal-taxskim_pass_stack] | Still a zero lock. The stack does not make it worse. |
| deghoul_base | 2,4,6,8,10,10,10,10 [S-cal-deghoul_base] | Steal lets Deghoul bank. |
| deghoul_stack | 1,2,3,4,5,6,7,8 [S-cal-deghoul_stack] | Drain. Deghoul climbs one point at a time. |
| victim_tax_base | 1,2,3,4,5,6,7,8 [S-cal-victim_tax_base] | Victim series. |
| victim_tax_stack | 1,2,3,4,5,6,7,8 [S-cal-victim_tax_stack] | Victim series unchanged by steal versus drain. |
| fee_tax_skim | 0,0,0,0,0,0,0,0 [S-cal-fee_tax_skim] | Fee does not fire on a passer who is not strictly ahead. |
| fee_tax_skim_stack | 0,0,0,0,0,0,0,0 [S-cal-fee_tax_skim_stack] | Same zero. Fee does not deepen it. |
| fee_from_bank_stack | 3,2,2,2,2,2,2,2 [S-cal-fee_from_bank_stack] | A pre-built bank of 4 is cut and then held. |

## (e) Tempo sanity

Team 0 is the 4 AP side. Team 1 is the faster side. The question is whether a Deep Dig or Hammer on turn 2 is an automatic win when the faster side lands two 2 AP hits first. It is not.

| Line | Mode | Fast | Heavy | Heavy lands | Land turn | Hits before | Length | Cap |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| seam_vs_dig | baseline | 100.0% [S-sc-baseline-seam_vs_dig-fast] | 0.0% [S-sc-baseline-seam_vs_dig-heavy] | 0.0% [S-sc-baseline-seam_vs_dig-lands] | — | 5.19 [S-sc-baseline-seam_vs_dig-hits] | 3.33 [S-sc-baseline-seam_vs_dig-len] | 0.4% [S-sc-baseline-seam_vs_dig-cap] |
| seam_vs_dig | stack | 100.0% [S-sc-stack-seam_vs_dig-fast] | 0.0% [S-sc-stack-seam_vs_dig-heavy] | 0.0% [S-sc-stack-seam_vs_dig-lands] | — | 5.07 [S-sc-stack-seam_vs_dig-hits] | 3.23 [S-sc-stack-seam_vs_dig-len] | 0.0% [S-sc-stack-seam_vs_dig-cap] |
| pulse_vs_dig | baseline | 100.0% [S-sc-baseline-pulse_vs_dig-fast] | 0.0% [S-sc-baseline-pulse_vs_dig-heavy] | 0.0% [S-sc-baseline-pulse_vs_dig-lands] | — | 12.53 [S-sc-baseline-pulse_vs_dig-hits] | 7.15 [S-sc-baseline-pulse_vs_dig-len] | 2.1% [S-sc-baseline-pulse_vs_dig-cap] |
| pulse_vs_dig | stack | 83.2% [S-sc-stack-pulse_vs_dig-fast] | 16.8% [S-sc-stack-pulse_vs_dig-heavy] | 0.0% [S-sc-stack-pulse_vs_dig-lands] | — | 13.71 [S-sc-stack-pulse_vs_dig-hits] | 11.39 [S-sc-stack-pulse_vs_dig-len] | 67.9% [S-sc-stack-pulse_vs_dig-cap] |
| skim_blade_vs_dig | baseline | 100.0% [S-sc-baseline-skim_blade_vs_dig-fast] | 0.0% [S-sc-baseline-skim_blade_vs_dig-heavy] | 17.9% [S-sc-baseline-skim_blade_vs_dig-lands] | 4.0 [S-sc-baseline-skim_blade_vs_dig-turn] | 11.47 [S-sc-baseline-skim_blade_vs_dig-hits] | 14.00 [S-sc-baseline-skim_blade_vs_dig-len] | 100.0% [S-sc-baseline-skim_blade_vs_dig-cap] |
| skim_blade_vs_dig | stack | 100.0% [S-sc-stack-skim_blade_vs_dig-fast] | 0.0% [S-sc-stack-skim_blade_vs_dig-heavy] | 14.6% [S-sc-stack-skim_blade_vs_dig-lands] | 4.0 [S-sc-stack-skim_blade_vs_dig-turn] | 10.43 [S-sc-stack-skim_blade_vs_dig-hits] | 12.39 [S-sc-stack-skim_blade_vs_dig-len] | 6.8% [S-sc-stack-skim_blade_vs_dig-cap] |
| two_fast_hits | baseline | 100.0% [S-sc-baseline-two_fast_hits-fast] | 0.0% [S-sc-baseline-two_fast_hits-heavy] | 0.0% [S-sc-baseline-two_fast_hits-lands] | — | 5.10 [S-sc-baseline-two_fast_hits-hits] | 3.23 [S-sc-baseline-two_fast_hits-len] | 0.0% [S-sc-baseline-two_fast_hits-cap] |
| two_fast_hits | stack | 100.0% [S-sc-stack-two_fast_hits-fast] | 0.0% [S-sc-stack-two_fast_hits-heavy] | 0.0% [S-sc-stack-two_fast_hits-lands] | — | 5.29 [S-sc-stack-two_fast_hits-hits] | 3.33 [S-sc-stack-two_fast_hits-len] | 0.0% [S-sc-stack-two_fast_hits-cap] |
| cinder_vs_dig | baseline | 98.9% [S-sc-baseline-cinder_vs_dig-fast] | 1.1% [S-sc-baseline-cinder_vs_dig-heavy] | 0.0% [S-sc-baseline-cinder_vs_dig-lands] | — | 7.05 [S-sc-baseline-cinder_vs_dig-hits] | 4.79 [S-sc-baseline-cinder_vs_dig-len] | 1.1% [S-sc-baseline-cinder_vs_dig-cap] |
| cinder_vs_dig | stack | 100.0% [S-sc-stack-cinder_vs_dig-fast] | 0.0% [S-sc-stack-cinder_vs_dig-heavy] | 0.0% [S-sc-stack-cinder_vs_dig-lands] | — | 6.05 [S-sc-stack-cinder_vs_dig-hits] | 3.59 [S-sc-stack-cinder_vs_dig-len] | 0.0% [S-sc-stack-cinder_vs_dig-cap] |
| ore_rider_vs_hammer | baseline | 100.0% [S-sc-baseline-ore_rider_vs_hammer-fast] | 0.0% [S-sc-baseline-ore_rider_vs_hammer-heavy] | 1.8% [S-sc-baseline-ore_rider_vs_hammer-lands] | 2.0 [S-sc-baseline-ore_rider_vs_hammer-turn] | 5.90 [S-sc-baseline-ore_rider_vs_hammer-hits] | 3.74 [S-sc-baseline-ore_rider_vs_hammer-len] | 0.4% [S-sc-baseline-ore_rider_vs_hammer-cap] |
| ore_rider_vs_hammer | stack | 100.0% [S-sc-stack-ore_rider_vs_hammer-fast] | 0.0% [S-sc-stack-ore_rider_vs_hammer-heavy] | 1.4% [S-sc-stack-ore_rider_vs_hammer-lands] | 2.0 [S-sc-stack-ore_rider_vs_hammer-turn] | 5.68 [S-sc-stack-ore_rider_vs_hammer-hits] | 3.67 [S-sc-stack-ore_rider_vs_hammer-len] | 0.0% [S-sc-stack-ore_rider_vs_hammer-cap] |
| leak_vs_hammer | baseline | 0.0% [S-sc-baseline-leak_vs_hammer-fast] | 0.0% [S-sc-baseline-leak_vs_hammer-heavy] | 0.0% [S-sc-baseline-leak_vs_hammer-lands] | — | 0.00 [S-sc-baseline-leak_vs_hammer-hits] | 14.00 [S-sc-baseline-leak_vs_hammer-len] | 100.0% [S-sc-baseline-leak_vs_hammer-cap] |
| leak_vs_hammer | stack | 100.0% [S-sc-stack-leak_vs_hammer-fast] | 0.0% [S-sc-stack-leak_vs_hammer-heavy] | 0.0% [S-sc-stack-leak_vs_hammer-lands] | — | 6.63 [S-sc-stack-leak_vs_hammer-hits] | 13.89 [S-sc-stack-leak_vs_hammer-len] | 27.5% [S-sc-stack-leak_vs_hammer-cap] |

`two_fast_hits` is the design rule: Seam Cut plus Blade Flurry against a Deep Dig banker. Fast side 100.0% [S-sc-stack-two_fast_hits-fast], heavy lands 0.0% [S-sc-stack-two_fast_hits-lands]. Blade Flurry at power 10 does not hand the heavy the win.
`pulse_vs_dig` is the typeless line. Baseline fast 100.0% [S-sc-baseline-pulse_vs_dig-fast]. Stack fast 83.2% [S-sc-stack-pulse_vs_dig-fast], heavy 16.8% [S-sc-stack-pulse_vs_dig-heavy], cap 67.9% [S-sc-stack-pulse_vs_dig-cap]. Speed still wins. The heavy is no longer shut out, because Pulse Shot stops inheriting a type. That is the rule working.
`skim_blade_vs_dig` stays a fast-side win at 100.0% [S-sc-stack-skim_blade_vs_dig-fast]. Baseline hits the turn cap on 100.0% [S-sc-baseline-skim_blade_vs_dig-cap] of trials because Patience walks Def to +6 and Skim keeps firing. The stack cap is 6.8% [S-sc-stack-skim_blade_vs_dig-cap]. The Patience cap is what lets the fight end.
`cinder_vs_dig` first casts Cinder on turn 1.0 [S-sc-stack-cinder_vs_dig-cast-cinder_burst] under the stack and turn 2.0 [S-sc-baseline-cinder_vs_dig-cast-cinder_burst] at 3 AP. That is the turn-1 click the AP change buys.
`leak_vs_hammer` baseline is a capped draw at 100.0% [S-sc-baseline-leak_vs_hammer-draw], which is the finisher lock. Under the stack the fast side wins 100.0% [S-sc-stack-leak_vs_hammer-fast] and Cipher Shot is first cast on turn 2.0 [S-sc-stack-leak_vs_hammer-cast-cipher_shot]. The hammer still does not land (0.0% [S-sc-stack-leak_vs_hammer-lands]), because Skim keeps hitting. The recharge lets the denial side attack. It does not make the heavy an automatic loss by rule. The heavy loses the race.
`ore_rider_vs_hammer` stays a fast-side win at 100.0% [S-sc-stack-ore_rider_vs_hammer-fast]. The Def rider does not flip it. Hammer lands on 1.4% [S-sc-stack-ore_rider_vs_hammer-lands].

## (f) Typed versus typeless, and learn pools

Score is the damage term from `rarity_fit_check.Model.strike` at equal attack and defense 60. Reference 2 AP single median 47.64 [T-ref-hit]. In-line band from the rarity doc is 0.80 [T-band-lo] to 1.25 [T-band-hi]. A cell with fewer than two cards on a side is thin and is not a broken rule. The plan's check is the wild band [F-plan-band] [F-csv-band-comment]: three at the body tier or lower, one at the body or one step up, signature strictly highest. Starters (gilarime,kitsauto,validape [F-starters]) only owe a unique top rarity. Ghost and Cofi are skipped. Off-type moves are outside the test.

| Mode | Rarity | Shape | AP | Typed n | Typed median | Typeless n | Typeless median | Typed higher | Thin |
|---|---|---|---:|---:|---:|---:|---:|---|---|
| baseline | Common | multi | 2 | 5 [T-base-0-tn] | 34.19 [T-base-0-tmed] | 2 [T-base-0-un] | 29.31 [T-base-0-umed] | yes [T-base-0-yes] | no |
| baseline | Common | single | 2 | 2 [T-base-1-tn] | 33.03 [T-base-1-tmed] | 6 [T-base-1-un] | 24.45 [T-base-1-umed] | yes [T-base-1-yes] | no |
| baseline | Rare | single | 3 | 3 [T-base-2-tn] | 49.42 [T-base-2-tmed] | 2 [T-base-2-un] | 37.40 [T-base-2-umed] | yes [T-base-2-yes] | no |
| baseline | Uncommon | single | 2 | 1 [T-base-3-tn] | 36.82 [T-base-3-tmed] | 4 [T-base-3-un] | 31.39 [T-base-3-umed] | yes [T-base-3-yes] | yes |
| stack | Common | multi | 2 | 6 [T-stack-0-tn] | 33.91 [T-stack-0-tmed] | 2 [T-stack-0-un] | 29.31 [T-stack-0-umed] | yes [T-stack-0-yes] | no |
| stack | Common | single | 2 | 3 [T-stack-1-tn] | 32.92 [T-stack-1-tmed] | 6 [T-stack-1-un] | 24.45 [T-stack-1-umed] | yes [T-stack-1-yes] | no |
| stack | Rare | single | 3 | 3 [T-stack-2-tn] | 49.42 [T-stack-2-tmed] | 2 [T-stack-2-un] | 37.40 [T-stack-2-umed] | yes [T-stack-2-yes] | no |
| stack | Uncommon | single | 2 | 1 [T-stack-3-tn] | 36.82 [T-stack-3-tmed] | 4 [T-stack-3-un] | 31.39 [T-stack-3-umed] | yes [T-stack-3-yes] | yes |

No non-thin cell flips. [T-stack-rule]. Cinder joining the Common multi cell does not let typeless pass it. Stake Line joins the Common single cell and typed still leads. The rarity doc's Uncommon spread row is one card against one card on the full delivery score [T-spread-prior]. That pair is not in this damage-term table [T-spread-absent]. It stays a thin cell, not a broken rule, and the stack does not add a move to it.

Rare typeless per AP against the typed-common median of the same shape, any AP. The stack does not edit these two moves. Stake Line changes the typed-common median, which is why the ratio moves.

| Mode | Move | Per AP | Typed-common median | n | Ratio | In band |
|---|---|---:|---:|---:|---:|---|
| baseline | Titan Crash (`refined_slam`) | 12.35 [T-base-refined_slam-per] | 16.51 [T-base-refined_slam-med] | 2 [T-base-refined_slam-n] | 0.748 [T-base-refined_slam-ratio] | OUT [T-base-refined_slam-out] |
| baseline | Rail Spike (`overdraw_snipe`) | 12.58 [T-base-overdraw_snipe-per] | 16.51 [T-base-overdraw_snipe-med] | 2 [T-base-overdraw_snipe-n] | 0.762 [T-base-overdraw_snipe-ratio] | OUT [T-base-overdraw_snipe-out] |
| stack | Titan Crash (`refined_slam`) | 12.35 [T-stack-refined_slam-per] | 16.46 [T-stack-refined_slam-med] | 3 [T-stack-refined_slam-n] | 0.750 [T-stack-refined_slam-ratio] | OUT [T-stack-refined_slam-out] |
| stack | Rail Spike (`overdraw_snipe`) | 12.58 [T-stack-overdraw_snipe-per] | 16.46 [T-stack-overdraw_snipe-med] | 3 [T-stack-overdraw_snipe-n] | 0.764 [T-stack-overdraw_snipe-ratio] | OUT [T-stack-overdraw_snipe-out] |

**PROPOSAL.** Leave Titan Crash and Rail Spike plain. Accuracy 80 and 85 is the commitment. They sit under the line. Raising power would make a rare typeless stronger, which fights the rule that typed commons win on per AP. This is rarity Q10. No new question.

Learn-pool problems before the edits: 0 [T-pool-base] [T-pool-base-empty]. After Extraction Hit and Fee Collector retier to Common, Stake Line added as Common, and the three swaps: 0 [T-pool-stack] [T-pool-stack-empty]. Nothing is dropped for the band.

The plan overview also says existing moves only, no new definitions [F-plan-no-new]. Stake Line is a new id [T-stake-line-band]. There is no damaging Staked common to build out, Stake Strike is Hopoch's Rare and retiering it would change a second pool, and boss moves cannot fill the slot. **PROPOSAL.** Keep Stake Line. It passes the band, which is the plan's check. Q20 is the question that allows the one new row. If that answer is no, Rattlestake keeps Pulse Shot and the rest of the stack still stands.

## (g) Implementation order

Top 12. Each step is testable alone. Effort is S, M, or L. Bulwark's AP cut is not a step.

| # | PROPOSAL | Impact | Effort | Files / rows | Depends | Editor test |
|---|---|---|---|---|---|---|
| 1 | Memory Leak rechargeTurns 2. Window, −2, and Speed +2 stay. | Opens the passer finisher lock. | S | Moves.csv:95 memory_leak. No pool edit. | Q1, default yes | MemoryLeak_RechargeSkipsOneTurn_FinisherPaysOnTheOpenTurn: a passer leaked on turn 1 has at least 4 AP at action time on turn 3. |
| 2 | Transaction Tax DrainAP:1. The user does not gain the point. | Stops the signature from banking a free heavy. | S | Moves.csv:71 transaction_tax. Deghoul's signature stays. | Q2, default yes | TaxIsDrain_DeghoulDoesNotGainThePoint: after one cast, the target is down 1 AP and Deghoul's AP did not rise by that point. |
| 3 | Typeless moves are neutral. STAB stays off. | The decided rule enters the engine. | M | DamageCalculator.cs:546 ResolveAttackType. CombatResolverConfigSO.cs:29 enableStab stays false. | None. Do not bundle Q19. | TypelessMove_IsNeutralAndAppliesNoReaction: Pulse Shot from a Burned user into Gas is ×1.0 and does not apply Rugged. Cinder Burst into Gas is ×1.2. |
| 4 | Cinder Burst AP 2, power 15. | Burned has a typed turn-1 click. The walls duo becomes the watch item. | S | Moves.csv:107 cinder_burst. Cinislug membership unchanged. Ember stays the signature. | Q9, default yes. Accept the duo spike. | CinderBurst_CostsTwo_BandUnchanged: APCost is 2, power is 15, flagship is still ember_wall, wild band still passes. |
| 5 | Fee Collector becomes Common and replaces Slipstream on Deghoul. | Gas gains the tax passive already in the catalog. | S | Moves.csv:75 rarity Common. SpeciesMovePools.csv:14 slipstream becomes fee_collector. | Q24, default yes. Not beside Armor. | LearnPool_DeghoulFeeCollectorStaysInsideTheBand: signature stays transaction_tax, one move above the body, size stays 5. |
| 6 | Stake Line, Staked, Common, ranged, power 34, AP 2, accuracy 95, replaces Pulse Shot on Rattlestake only. | The body gets a typed click below Compound Barrier. Pulse Shot the move stays 26. | M | New Moves.csv row stake_line. SpeciesMovePools.csv:10 baseline_shot becomes stake_line. | Q20, default yes. This is the one new id. | LearnPool_RattlestakeStakeLineStaysInsideTheBand: size 5, signature ap_reserve is the unique top, the new move is Staked. |
| 7 | Extraction Hit at Common replaces Body Check on Kodiax. OnHit 40% Melee Atk +1 cap +3, Speed +1 once at +3. Crystal Extraction stays. | Empty passive gets the payload the card already promises. | M | Moves.csv:127 rarity Common and the OnHit utility. SpeciesMovePools.csv:21 baseline_slam becomes extraction_hit. crystal_extraction stays. | Q23, default yes | ExtractionHit_RaisesAtkAtFortyAndSpeedsOnceAtPlusThree. LearnPool_KodiaxExtractionStaysInsideTheBand: signature stays ore_strike. |
| 8 | Riders with no pool add: Ore Strike Def −1 at 25% once, Yield Spike lifesteal 20%, Stake Strike lifesteal 15%, Fork Split and Shard Scatter and Chain Fork Speed −1 at 20% once, Combustive Shift DrainAP 1 at 50% once, Hype Train Surging at 100%. | Plain high-rarity hits match the type profile. None of them enters a new pool. | S | Moves.csv ore_strike:131, yield_spike:145, stake_strike:143, fork_split:118, shard_scatter:187, chain_fork:123, combustive_shift:77, hype_train:195. | Rarity Q1, Q2, Q3, Q4. Defaults yes. | RarityFitTests.OreStrike_CracksDefAndStaysSignature. YieldLifestealBeatsStake. CombustiveDrainsOnce and the move is absent from Deghoul and Balloom. HypeTrain_AppliesSurging and no pool contains it. ForkSpeedNick_DoesNotChangePower. |
| 9 | Delete Gas War's NextMoveAPSurcharge once. Power 72, AP 4, ranged. | The same edit as rarity Q6 and brief Q32. | S | Moves.csv:72 volatile_shot. | Q32, default yes. Do not also make it melee. | GasWar_HasNoSelfSurcharge: utility has no NextMoveAPSurcharge, power 72, AP 4, both Gas pools still list it under the signature. |
| 10 | Patience cap +3, Streamlined drops Def once, Rugged cap 3, HODLed one owned ramp cap +3 then revert. | Stops the +6 walks and the Rugged climb. No new move. | M | Moves.csv patience_passive:142 and streamlined_flow:156. CombatConstants.cs:92. StatusProcessor HODLed block and Statuses.csv HODLed. | Q22, Q31, Q17, Q16. Defaults yes. | Patience_StopsAtDefThree. StreamlinedFlow_DropsDefOnceAbove70: a second action above 70% does not drop Def again. RuggedEscalatesAndCapsWhileDripAndFomoTickOnce asserts cap 3. Hodled_RampsOnePerStep: +1, +2, +3, then the owned stages revert. |
| 11 | Blade Flurry power 10 at 3 hits, Cryo Ray Speed −2 at 100%, Hype Burst grants Ranged Atk. | The common stops outrunning the card beside it. Cryo slow matches the name. Hype buffs the stat Cinislug strikes with. | S | Moves.csv blade_flurry:202, cryo_ray:200, hype_burst:101. Pools unchanged. | Q35, Q34, Q42. Defaults yes. | BladeFlurry_IsTenTimesThree. CryoRay_SlowsAtOneHundred. HypeBurst_GrantsRangedAtkPerBuffRemoved: two buffs, Ranged Atk +2, Melee Atk unchanged. |
| 12 | Armor flat 12 on Guard Posture and flat 16 on Guard Stance, one hit, after the wall percent, cap 16. Remove Guard Stance's Def +2. Do not equip either move. Do not change Bulwark's AP. | The flat matches the live percent on the measured hammer. The duo does not gain a second heavy or a third mitigation. | S | Moves.csv guard_posture:36 and guard_stance:86. No SpeciesMovePools.csv edit. bulwark_blow:206 stays AP 3. | Q10 yes for the numbers. Q12 no. Q13 no. Q33 revised to no. | GuardPosture_SubtractsTwelveAfterTheWall. GuardStance_SubtractsSixteenAndDoesNotRaiseDef. DeghoulAndBlockwyrmPools_DoNotGainArmor. BulwarkBlow_StaysAtThreeAp. |

Decided, and not one of the twelve: All-In's ally wipe. UtilityEffects `DrainAP:10:100%:AllAllies:all_in` on Moves.csv:191. Enemy AP untouched. No pool edit. Test `AllIn_WipesAllyApOnCast` plus the existing caster spend. It reaches nobody until a Meme body exists, and none does.

Explicitly not in this order. No drain cap. No whiff refund. No KO floor. No heavies at 6. No longer Ember recharge. No Margin Loan. No Pair Ping. No Mind Fault. No Armor pool swap. No Bulwark AP cut. No second copy of a withdrawn passive.

## (h) What works, what doesn't, what could be better

What works. The wall decision is in the data, and the scripted attacker still beats a recast Ember 81.8% [S-w-stack-ember_vs_ore-a]. Two faster hits still beat a 4 AP banker under the whole stack 100.0% [S-sc-stack-two_fast_hits-fast]. Typed still beats typeless in every non-thin cell [T-stack-rule]. The learn-pool band is empty before and after the three edits [T-pool-stack-empty]. Leak recharge opens the passer lock 2,2,4,4,6,6,8,8 [S-cal-leak_pass_stack]. Tax-as-drain stops the free bank 1,2,3,4,5,6,7,8 [S-cal-deghoul_stack]. Armor flat 12 ties the live 25% on this hammer [S-f-flats-match]. Kindling stays 6% and still ticks before Rugged [F-kindling-literal]. Uplink is already the Validape passive [F-uplink].

What doesn't. The defaults were tuned alone, and together they hand the round robin to Blockwyrm plus Cinislug 97.5% [S-arch-walls-stack]. Greedy Cinislug stops casting Ember 99.1% [S-sp-stack-cinislug-0]. Bulwark at 2 AP, which the brief suggested, deletes the rest of Blockwyrm's clicks 94.1% [S-sp-stack-blockwyrm-0]. Tax plus Skim on a passer is still a zero lock 0,0,0,0,0,0,0,0 [S-cal-taxskim_pass_stack]. Typeless moves still inherit a type DamageCalculator.cs:546 [F-inherit-line]. Rare typeless hits sit under the line [T-stack-refined_slam-out] [T-stack-overdraw_snipe-out]. Compressed is pushed further down 26.2% [S-type-Compressed-stack]. The greedy field denies almost no AP 0.076 [S-deny-stack], so a win-rate table is the wrong tool for the lock. All-In's ally wipe is decided and not in the CSV [F-allin-utility-empty].

What could be better. Ship the four engine and data fixes first (Leak, Tax, typeless, Cinder), hold Bulwark at 3 AP, and put Armor on the card without putting it on a Chainling. Leave the rare typeless hits plain. Leave the Tax plus Skim zero lock alone. Watch the walls duo and Compressed after Cinder lands. Do not add a new Compressed move in the same pass. Do not reorder ticks. Do not reopen walls, STAB, Kindling's percent, Pulse Shot's power, or the withdrawn passives.

## (i) Questions only Cronus can answer

These are the brief's Q1–Q46 and the rarity doc's questions that are not the same ask. Rarity Q6 is brief Q32 (Gas War's surcharge), one edit. Rarity Q8 is brief Q19 (STAB off). Rarity Q9 is already decided: Status Resistance stays retired. Suggested defaults are the earlier docs' defaults, except Q33, which this round revises.

| # | Question | Suggested default | What it blocks | This round |
|---|---|---|---|---|
| Q1 | Memory Leak stays a window and gains `rechargeTurns` 2? | Yes. Rewrite the card with it | Backlog 1. The finisher lock |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:265 [I-Q1] |
| Q2 | Transaction Tax becomes a drain? | Yes | Backlog 2 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:266 [I-Q2] |
| Q3 | Refund the cost when a drain removes 0? | No | Nothing, if the default holds. A yes makes Skim free on an empty bar |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:267 [I-Q3] |
| Q4 | Cap enemy AP removal at 2 per target per turn? | No | Nothing useful. It does not free a 4 AP cast |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:268 [I-Q4] |
| Q5 | KO replacement enters with at least 2 AP? | No. 0.0% becomes 35.8% | A large swing for a patch the window-clear already covers |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:269 [I-Q5] |
| Q6 | MEV Extracted becomes a steal? | No. Fix the comment. Leave it out of pools | Comment only |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:270 [I-Q6] |
| Q7 | May the AI pass to bank a 4 AP heavy? | Only when the minimum roll KOs, or damage per AP is at least 1.15× the best legal 2 AP move | Backlog 4 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:271 [I-Q7] |
| Q8 | Add a typed Node move at 2 AP, or Pair Ping, to Validape? | No. Leave that membership | Nothing this round |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:272 [I-Q8] |
| Q9 | Cinder Burst from 3 AP to 2, power stays 15? | Yes | Backlog 5 | Keep yes. Accept the Blockwyrm plus Cinislug spike. It is not a TRUE lock. COMBAT_ISSUES_DECISION_BRIEF_V3.md:273 [I-Q9] |
| Q10 | Flat Armor 12 and 16, one hit, after the wall percent, cap 16? | Yes, as the Armor shape. It is not in the data today | Any Armor number. Do not ship it in front of backlog 1–3 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:274 [I-Q10] |
| Q11 | If Q10 is yes, is the longer Hammer-plus-Blockade line acceptable? | Yes. Do not lower Blockade's 60% | Whether flat Armor stacks or is nostack |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:275 [I-Q11] |
| Q12 | Deghoul replaces Snap Strike with Guard Posture? | No until Q10 is yes, and not in the same edit as Q24 | Armor pool | Keep no. Flat 12 ties the live percent. Do not equip it. COMBAT_ISSUES_DECISION_BRIEF_V3.md:276 [I-Q12] |
| Q13 | Blockwyrm replaces Stone Barrage with Guard Stance? | No until Q10 is yes | Armor pool. The Def +2 would leave | Keep no. The triple with Ledger still deals damage, and it is not the default. COMBAT_ISSUES_DECISION_BRIEF_V3.md:277 [I-Q13] |
| Q14 | Immutable Ledger stays a 20% signature, not Armor? | Yes | Stops a third flat on Blockwyrm |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:278 [I-Q14] |
| Q15 | Shattered Armor and Core Breach stay catalog-only? | Yes. No pool edit. There is no Core Breach row | Nothing, if the default holds |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:279 [I-Q15] |
| Q16 | HODLed: one ramp, cap +3, revert, immunity, no producer? | Yes | Backlog 8 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:280 [I-Q16] |
| Q17 | Rugged cap 3? | Yes | Backlog 9 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:281 [I-Q17] |
| Q18 | Narrow Dusted, Shattered, and Corroded, and revert owned stages on Defueled? | Yes | The "next" chemistry tidy |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:282 [I-Q18] |
| Q19 | STAB off, 1.1, or 1.2? | Off until typeless exclusion ships. If it comes on later, 1.1 | Do not bundle it with backlog 3 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:283 [I-Q19] |
| Q20 | New Staked common, power 34, AP 2, ranged, replacing Pulse Shot on Rattlestake only? | Yes. Pulse Shot the move stays power 26 | Backlog 13 | Keep yes. One new id. It passes the band. The plan's 'no new definitions' sentence is what this answer allows. COMBAT_ISSUES_DECISION_BRIEF_V3.md:284 [I-Q20] |
| Q21 | Leave Overleveraged shelved, with no Margin Loan? | Yes | Nothing. Self-damage stays shelved |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:285 [I-Q21] |
| Q22 | Patience cap +3, heal kept, Hopoch keeps it? | Yes | Backlog 11 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:286 [I-Q22] |
| Q23 | Extraction Hit at Common replaces Baseline Slam on Kodiax? | Yes. Crystal Extraction stays | Backlog 12 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:287 [I-Q23] |
| Q24 | Fee Collector at Common replaces Slipstream on Deghoul? | Yes. Not beside an Armor swap | Backlog 14 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:288 [I-Q24] |
| Q25 | Leave Unstable Code, Firewall, and Automated Pressure out of pools until their caps exist? | Yes | Stops a +6 walk from entering a five |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:289 [I-Q25] |
| Q26 | Split OnMiss so Precision Focus is "you missed" and Reactive Ping is "you were missed"? | Yes | A small resolver branch |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:290 [I-Q26] |
| Q27 | Uplink stays the Node passive, card text only, no new 2 AP buff? | Yes. It is already granted | Card text |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:291 [I-Q27] |
| Q28 | Demote Defensive Reflex to Rare and Sustain Regen to Common? | Yes. No pool change | Rarity ladder only |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:292 [I-Q28] |
| Q29 | Cipher Lock covers passive stage writes and Meme stages? | Yes | Backlog 15 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:293 [I-Q29] |
| Q30 | Narration says the passive's name? | Yes | A template string |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:294 [I-Q30] |
| Q31 | Streamlined drops Def once, not on every action down to −6? | Yes | Backlog 10 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:295 [I-Q31] |
| Q32 | Drop Gas War's +1 surcharge, keep 4 AP and ranged? | Yes. Do not also make it melee | The "next" list | Keep yes. This is also rarity Q6. Apply it once. COMBAT_ISSUES_DECISION_BRIEF_V3.md:296 [I-Q32] |
| Q33 | Bulwark Blow to 2 AP, power 50? | Yes | The "next" list. Turn-1 Def hit | REVISE to no. Keep Bulwark at 3 AP. The 2 AP cut is the click monopoly, and it is not what makes the duo win. COMBAT_ISSUES_DECISION_BRIEF_V3.md:297 [I-Q33] |
| Q34 | Cryo Ray's Speed −2 from 25% to 100%? | Yes. Power and AP stay | The "next" list |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:298 [I-Q34] |
| Q35 | Blade Flurry to 10×3? | Yes. Fox Feint stays the signature | The common stops beating the uncommon beside it |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:299 [I-Q35] |
| Q36 | On a quiet fight, Grind Tax lands on turn 4 and then every four turns? | Yes. That is the boss loop. Denial may push it | Boss AI horizon inside backlog 4 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:300 [I-Q36] |
| Q37 | After Speed +6 and full HP, does the boss keep sipping? | No. Prefer the tax, or a Pass that satisfies Q7 | Boss clicks |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:301 [I-Q37] |
| Q38 | The "due" tell reads AP before the spend? | Yes | One narration fix |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:302 [I-Q38] |
| Q39 | How hard can a player turn Grind Tax off? | One denial slips it a turn. A chain must not delete it for the whole fight. Q1 is the bound | Pairs with Q1 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:303 [I-Q39] |
| Q40 | Fox Feint is held until something is about to hit? | Yes | Utility tie-break |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:304 [I-Q40] |
| Q41 | Fix the Defensive ladder before any encounter uses it? | Yes | `FindBestSupport` and `FindLethalBlow` |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:305 [I-Q41] |
| Q42 | Hype Burst grants Ranged Atk, and strip cards say status buffs? | Yes | Backlog 7 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:306 [I-Q42] |
| Q43 | System Reset's card stops saying the user skips the next action? | Yes. Recharge 2 already locks only that move | Backlog 6 |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:307 [I-Q43] |
| Q44 | Bench Relay, Pair Window, Stockpile Echo, or Boss Heat to 5 AP? | No this round. Grind Tax stays 6 | Nothing |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:308 [I-Q44] |
| Q45 | Secondary Node and Glitched commons for Validape and Kreechank? | No in the same change as typeless exclusion. Validape's membership stays | A later pass |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:309 [I-Q45] |
| Q46 | Ghost and Meme stay dormant, and FOMO's self tick stays? | Yes. FOMO is chemistry, not move self-damage | Nothing |  COMBAT_ISSUES_DECISION_BRIEF_V3.md:310 [I-Q46] |
| R-Q1 | Ship Ore Strike as a 25% Def crack. The closed per-buff sentence stays deleted. | Ship the Def crack. | Blocks B-ore. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:443 [I-RQ1] |
| R-Q2 | Ship Hype Train as Surging on the next hit. The closed consecutive ramp stays deleted. | Ship Surging. Leave it out of every pool. | Blocks B-hype. No species is Meme. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:444 [I-RQ2] |
| R-Q3 | Ship Combustive Shift as a one-time 1 AP drain. The closed missing-AP damage bonus stays deleted. | Ship the drain. Leave it out of the Gas learn pools. | Blocks B-combustive. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:445 [I-RQ3] |
| R-Q4 | Fork Split and Shard Scatter keep their damage. The new text is a 20% Speed nick, priced at 0 HP on this screen. | Ship the nick. Do not add damage. | Blocks B-fork and B-shard. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:446 [I-RQ4] |
| R-Q5 | Lean Hit stays a plain 1 AP Rare. The low cost is the whole card. | Leave Lean Hit alone. | Blocks any rider on lean_hit. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:447 [I-RQ5] |
| R-Q7 | Speed, accuracy, evasion, and priority stay at 0 HP in the rarity score. | Keep them at 0. Judge them in the sim. | Blocks a rescore of the whole table. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:449 [I-RQ7] |
| R-Q10 | When a low rarity outruns a higher one, trim the number if a rarity bump would break a learn pool. | Trim. Bump only when the pool test still passes. | Blocks the over-deliver rows. | RARITY_FIT_AND_RIDER_BUILDOUT_V1.md:452 [I-RQ10] |

No further questions. The duo spike, the Bulwark monopoly, the remaining Tax plus Skim lock, and the rare-typeless gap are evidence on Q9, Q33, Q4, and R-Q10. They are not new asks.


## Check tally

PASS 556 [TALLY-PASS]. FAIL 0 [TALLY-FAIL]. Every tagged figure was recomputed from the snapshot, the attachments, or stack_2v2_sim.py at seed 20261023. The checker exits non-zero if any claim failed or a cited id has no check.
