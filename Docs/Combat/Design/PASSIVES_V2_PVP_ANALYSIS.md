# Passives v2, competitive 2v2 PvP

Snapshot `chainbound_combat_snapshot_20261020_0215` (local tree 2026-10-20 ~02:15 CDT). Round-12 notes and the 2v2 sims are the 14:15 attachment. Nothing in this file is taken from the repo checkout. Walls design R is a pending proposal, not a decision.

Check ids in square brackets are re-verified by `passives_v2_check.py` against that snapshot. Numbers come from `passives_v2_sim.py` (1,000 paired trials) or from a deterministic fight the check script re-runs. [C74] [C75]

## (a) Headline

Since the 2026-10-17 passives note, six things that matter here actually landed: bench recharge ticks [C04] [C30], end-of-turn passives run before the status tick [C03] [C29], Uplink replaced AP Router on Validape [C70] [C31], Kindling is on Cinislug at 6% and ticks before Rugged [C32] [C37], Status Resistance is retired in place [C34], and Double-or-Nothing's card was cut to the 60/40 roll [C48-double_or_nothing]. Cipher Lock, the learn-pool reformat, and the empty Extraction Hit row are the other changes this round has to live with.

What the 2v2 measurement says: the six passives a species can actually learn are not the strong ones. Immutable Ledger is the only granted passive that moves a close match (+3.27 win rate, +3.77 AP, about 16 HP mitigated, first KO 0.45 turns earlier). Patience is a slow Def walk to +6 (+1.53). Uplink pays, but not on turn 1, and it does not change the win rate. Kindling chips about 2.4 HP a battle. Parallel is a small Speed proc. Streamlined is a wash to a slight loss.

The passives that would be near-mandatory are ungranted: Unstable Code +7.33, Firewall Passive +6.80, Automated Pressure +4.60. Fee Collector is the only passive that drains a foe's AP, and it is not in a pool. Bot's only passive denies no AP.

No measured passive, top or bottom, moves by more than about 1 win-rate point if walls design R is applied. Do not retune passives for R.

Open questions are the caps, whether Fee Collector and a filled Extraction Hit enter a pool, and whether OnMiss passives should keep firing for both fighters. Suggested defaults are in section (k).

## (b) Did the 10-17 proposals and the later decisions land?

| Id | Verdict | Where | Test |
| --- | --- | --- | --- |
| P1 bench recharge | Landed [C04] [C30] | `TickBenchedRecharge` calls `TickRechargeWhileBenched`. Other statuses stay paused. | `BenchRechargeTicksAndOtherStatusesPause` |
| P2 / EOT-ORDER | Landed [C03] [C15] [C29] | EndOfTurn passives, then `ProcessEndOfTurn`, then OnBuffExpired. Kindled is first inside the status pass, still a literal `0.06f` [C05]. | `EndOfTurnPassives_RunBeforeStatusDurationTick` |
| P3 owned stages | Landed as the status rule, not re-opened here | Apply-once and repeating status stages revert with the status. Jammed is Accuracy −1 and reverts [C40]. | `Jammed_AppliesOwnedAccuracyMinusOneAndReverts`, `GasSpikeAndFud_RestoreOwnedStagesOnExpiry` |
| P4 narration | Not landed [C17] | `passiveTemplate` is still `{actor}'s passive activated.` Resolver `PassiveTriggered` messages do name the move. | no test for the template |
| P5 / MV-extraction_hit | Not landed [C48-extraction_hit] [C62] | Trigger `None`, empty utility, not in an implicit list, not in Kodiax's pool. Card still promises Atk and Speed [C57]. | no test |
| P6 Status Resistance | Landed as retire-in-place, not as a delete [C34] [C48-status_resistance] | Description `Retired. No effect.` Trigger `None`, empty payload. | `TextBatch` / the description assert in `AuditReconciliationV1Tests` |
| P7 Streamlined | Partly [C59] [C35] [C72] | The stage-column double drop is gone. The utility drop is still every action above 70% HP, cap −6, `TriggerOnce` false. The discount is separate. | `StreamlinedFlow_DropsDefOnceAbove70` asserts the −6 cap. The name overclaims "once". |
| P8 Low AP | Not landed [C58] [C48-low_ap_surge] [C48-low_ap_burst] | Both still `TurnStart` and `APAtMost` 1, which is AP ≤ 1 [C25]. After +2 regen from 0 they see 2 AP and do not fire. Utility cap is still +6 [C56]. | no test. Mirror `low_ap_no_fire`: Speed stage 0, AP 8 after four passed turns. |
| P9 Patience cap +3 | Not landed [C50] [C48-patience_passive] | Utility is still Def +1, cap +6, every end step, plus the 5% heal [C16]. | no cap test |
| P10 Unstable +1 cap +3 | Not landed [C51] [C48-unstable_code] [C67] | Still +1 or +2, cap +6, every turn start. Not in Kreechank's pool. | no test |
| P11 Automated cap −2 | Not landed [C52] [C48-automated_pressure] [C65] | Still −1 to a random foe stat, cap −6. Not in Kitsauto's pool. | no test |
| P12 Firewall 30% | Not landed [C48-firewall_passive] [C68] | Still 25% projectile. Not in Keyper's pool. `mitigationSingleSourceCap` is 1, so 0.30 would not have been clipped [C27]. | no test |
| APR-RELAY | Landed [C07] [C70] [C31] | `SettleUplink` after every action. Validape's pool is hub_heal, baseline_striker, uplink, hammer_sync, pack_rally. `ap_router` is gone. | `Uplink_PaysEachChangedAllyOnceAndSkipsExtendAndCap`, `LearnPool_ValidapeUplinkAndLiveBuiltCards` |
| APR-KIND | Landed [C06] [C32] [C37] [C46] | Mark only if the hit dealt HP and the foe is still alive. 6% at end of turn, before Rugged. On Cinislug. | `Kindling_MarksOnceThenBurnsBeforeRugged`, `Kindling_MissAndKoDoNotMark` |
| CL-312 | Partly [C10] [C11] [C12] [C36] [C41] | Utility positive stages and new buffs are blocked for 2 turns. Existing buffs stay. The passive stage column and `ApplyMemeStage` do not call `BuffGate`. | `CipherLockBlocksNewBuffsAndPositiveStagesWithoutRemovingExistingBuffs` covers the gate, not those two call sites |
| MV-double_or_nothing | Text cut landed. Immunity not built [C48-double_or_nothing] | Card is the 60/40 roll. No immunity branch in `TriggerPassiveMove`. | description assert in the text batch. No immunity test, because there is no immunity. |
| E-28 | Not landed. Still deferred [C09] | Both arms of the passive status ternary assign the owner. No live passive writes a non-self status target. | no behavior test |

A passive cannot be queued as an action [C02]. The equip cap is 2 [C01]. Candy's known pool is the ten baseline moves plus the `bean_sip` event grant [C21]. PvP party lock rejects boss-only and unknown AP costs and does not read learn pools [C19] [C20]. So an unpooled passive is equip-legal if the fighter already knows it. Nothing in the live pools or the candy table teaches a second passive. A fighter built from a pool has at most one.

## (c) Live passive inventory

Code effect, not the card. "Granted" means the id is in that species' learn pool [C46]. Typeless equips on anyone. A typed passive equips on a matching primary, an entangled match, or a Meme primary. No passive is on an exclusive list.

Heal percents are `floor(MaxHP * pct)`, minimum 1 [C16]. Stage cap ±6 is the shared stage ceiling unless the utility names a tighter cap. Cipher Lock blocks positive utility stages [C10]. It does not block the stage column [C11] [C60] [C61].

| Id | Type / rarity | Trigger, as the code runs it | Cap / once | Granted | Card gap |
| --- | --- | --- | --- | --- | --- |
| turn_start_ap_gain [C48-turn_start_ap_gain] | None / Epic | Turn start, Gain 1 AP. Utility is set, so the hardcoded twin is blank. | none | no | matches |
| on_hit_momentum [C48-on_hit_momentum] | None / Rare | On hit, 25% Melee Atk +1, utility cap +3. Cipher blocks. | +3 | no | matches |
| on_crit_bonus [C48-on_crit_bonus] | None / Rare | On crit, Gain 1. Implicit, because the trigger cell is `None` and the utility is empty. | none | no | matches |
| defensive_reflex [C48-defensive_reflex] | None / Legendary | 20% mitigation on any hit, read at hit time, and Drain 1 AP from yourself at end of turn if you have AP. | none | no | matches |
| sustain_regen [C48-sustain_regen] | None / Rare | End of turn, heal 3% MaxHP. | none | no | matches. Same rarity as Deep Sustain, which is double. |
| deep_sustain [C48-deep_sustain] | None / Rare | End of turn, heal 6% MaxHP. | none | no | matches |
| status_resistance [C48-status_resistance] | None / Rare | Does not run. | — | no | card says retired |
| emergency_recovery [C48-emergency_recovery] | None / Rare | After damage, if HP ≤ 25%, heal 15% once per battle. | once | no | matches |
| precision_focus [C48-precision_focus] | None / Rare | OnMiss, 75% Refund 1. OnMiss runs for the attacker and the defender [C13] [C14]. | none | no | "a miss" does not say both sides |
| reactive_ping [C48-reactive_ping] | None / Rare | OnMiss, `floor(other MaxHP * 0.05)` to the other fighter, minimum 1. Same both-sides call. No chemistry. | none | no | same |
| low_ap_surge [C48-low_ap_surge] | None / Rare | Turn start, only if AP ≤ 1, Speed +1, utility cap +6. Sees AP after the +2 grant. | +6 | no | omits +6 [C56] |
| low_ap_burst [C48-low_ap_burst] | None / Rare | Same window, Speed +3, cap +6. | +6 | no | omits +6 |
| spite_counter [C48-spite_counter] | None / Rare | Not the trigger loop. On a debuff apply, 33% to copy that status onto the source for the same duration. Not filtered by origin, so a chemistry debuff can mirror too. | none | no | matches the 33% |
| desperation_surge [C48-desperation_surge] | None / Epic | After damage, HP ≤ 33%, once: stage-column Melee Atk +2 (no Cipher gate) and Gain 1 [C61]. | once | no | omits the Cipher hole |
| fee_collector [C48-fee_collector] | Gas / Rare | Once per turn, before the queue runs. The first living opposing actor loses 1 AP only if that foe's AP is strictly higher [C08] [C33]. | once / turn | no | matches |
| immutable_ledger [C48-immutable_ledger] | Block / Legendary | First incoming hit each turn: 20% mitigation, then +1 AP, and this happens before a barrier can zero the hit [C15]. A full block still consumes it. | first hit / turn | Blockwyrm, signature | "first hit" includes a hit the wall fully eats |
| firewall_passive [C48-firewall_passive] | Encrypted / Rare | 25% mitigation on projectile hits, read from the utility at hit time. The OnDamageTaken trigger does not apply it. | always | no | matches the 25% |
| conductive_counter [C48-conductive_counter] | Forked / Legendary | After a contact hit that dealt HP, once a turn: `floor(damage * 0.15)` back, minimum 1, then +1 AP. A full block does not reach it. | once / turn | no | matches |
| extraction_hit [C48-extraction_hit] | Mined / Rare | Nothing. Trigger `None`, empty utility, not implicit. | — | no | card promises Atk and Speed [C57] |
| patience_passive [C48-patience_passive] | Staked / Rare | End of turn, Def +1 up to +6, and if you used no damaging move, heal 5% [C16] [C82]. | +6, no reset | Hopoch | omits +6 [C50] |
| stake_refund [C48-stake_refund] | Staked / Epic | After the status tick, if at least one buff expired this end step, Gain 1. Once per end step, not once per buff. | one point | no | does not say "once per end step" |
| streamlined_flow [C48-streamlined_flow] | Compressed / Legendary | While HP% > 70, move costs −1 (read at cost time). Each such action also drops Def −1 down to −6 [C26] [C59]. | −6, repeats | Humpacked, signature | omits the repeat and the floor [C53] |
| automated_pressure [C48-automated_pressure] | Bot / Rare | End of turn, one random core stat on a random living foe, −1, floor −6. Negative, so Cipher does not block it. | −6 | no | omits −6 [C52] |
| unstable_code [C48-unstable_code] | Glitched / Rare | Every turn start, including turn 1: random core stat, +1 or +2, cap +6. Cipher blocks the utility write. | +6 | no | omits the cap and that it is every turn [C51] |
| parallel_passive [C48-parallel_passive] | Shard / Rare | When an ally's attack lands, 25% Speed +1, cap +2. Cipher blocks. | +2 | Crackett | omits 25% [C49] |
| shattered_armor [C48-shattered_armor] | Shard / Rare | On a chemistry-advantage hit, once a turn: 5% mitigation for 1 turn and Gain 1. | once / turn | no | does not say once per turn [C55] |
| double_or_nothing [C48-double_or_nothing] | Meme / Mythic | End of turn, 60% Gain 1, else Drain 1. No immunity. | none | no living bearer | matches the cut text |
| meme_momentum [C48-meme_momentum] | Meme / Mythic | If a hit landed: both Atk +2 (no Cipher, walks to ±6) and Gain 2. If you used a damaging move and landed nothing: both Atk −1 and Drain 1 [C12] [C54]. A full wall block is the miss branch. | ±6 | no living bearer | omits the Drain 1 |
| gecko_guard [C48-gecko_guard] | Frozen / Legendary | After damage, HP ≤ 50%, once: stage-column Def +2 and Speed +1. No Cipher gate [C60]. `rechargeTurns` 999 is unused because a passive is not an action. | once, permanent stages | no | states the once and the stages |
| uplink [C48-uplink] | Node / Uncommon | After your action, each ally who received a positive stage or a new buff this action gains 1 AP, once per ally per turn. A lost point (cap 10 or Thread Lock) still spends the claim. Heals do not pay. | once / ally / turn | Validape | does not say heals fail, or that a lost point is spent |
| kindling [C48-kindling] | Burned / Uncommon | A damaging hit that leaves the foe alive applies Kindled. End of turn, before other DoTs, `floor(MaxHP * 0.06)`, then the status ends [C05] [C06] [C38]. Stacks with Rugged as a separate status [C39]. | one mark, one tick | Cinislug | matches |

There are 31 passive rows [C47]. Ghost is 0 HP, so it is not a bearer [C45]. Meme has no chemistry cell as an attacker [C18]. No Meme passive writes a chemistry status.

## (d) PvP power budget

Method. `passives_v2_sim.py` extends `tempo_2v2_sim.py`. It does not edit it. The base file drops Streamlined's Def on every action [C72]. The extension drops it only while HP% > 70 [C73]. Mirrors that must pass before the matrix: `low_ap_no_fire`, `extraction_dead`, `patience_heal`, `kindling_chip`, `streamlined`, `fee_ahead`, `uplink_rally`.

Each passive is added to one real bearer and three real pool moves. The partner is Kodiax, or Crackett when the bearer is Kodiax. Bench is Balloom. Opponents are a tempo pair (Kitsauto, Forkrook), a walls pair as the engine works today (Cinislug with Kindling, Blockwyrm with Ledger), and a denial pair (Deghoul with Transaction Tax, Keyper with Cipher Shot and Memory Leak). Same seed with the passive on and off. Policy: focus the carrier, swap yourself out under 35% HP, press Packet Skim or Transaction Tax when the foe has 2+ AP, Memory Leak on turns 1–2, walls when a foe has 3+ AP or it is turn 1, and a slow heavy (Kodiax, Blockwyrm, Cinislug, Gilarime) may pass turn 1 to bank a 4 AP hit. Uplink does not bank on that line. A second line lets Validape bank for Pack Rally. 1,000 trials. Win is team 0. KO shift is how many turns earlier the first opposing KO arrives (positive means sooner). [C74]

Cross-carrier win rate is not a power tier. Kodiax's shell already wins about 99–100%, so a passive on Kodiax can hand out 3 AP and still show +0.03. Read the resource columns next to the win rate, and read the matchup where the off-rate is below 95.

`dwr` is the mean win-rate delta in points. `gain` / `deny` are AP per battle. `mit` / `chip` / `heal` are HP per battle. `stage` is successful stage writes per battle. `press` is foe-stat drops (Automated Pressure) or mirrored debuffs (Spite).

<!--SIM unstable_code dwr=7.33 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=3.329 press=0.0 ko=0.203 granted=0 legal=1-->
<!--SIM firewall_passive dwr=6.8 gain=0.0 deny=0.0 mit=15.23 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.169 granted=0 legal=1-->
<!--SIM double_or_nothing dwr=6.27 gain=1.446 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.633 stage=0.0 press=0.0 ko=-0.054 granted=0 legal=0-->
<!--SIM automated_pressure dwr=4.6 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=3.43 ko=0.113 granted=0 legal=1-->
<!--SIM immutable_ledger dwr=3.27 gain=3.772 deny=0.0 mit=15.85 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.449 granted=1 legal=1-->
<!--SIM patience_passive dwr=1.53 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=5.37 tax=0.0 stage=2.965 press=0.0 ko=0.011 granted=1 legal=1-->
<!--SIM fee_collector dwr=1.07 gain=0.0 deny=0.888 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=-0.003 granted=0 legal=1-->
<!--SIM parallel_passive dwr=0.3 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.338 press=0.0 ko=0.0 granted=1 legal=1-->
<!--SIM emergency_recovery dwr=0.1 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=6.26 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM deep_sustain dwr=0.07 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=13.05 tax=0.0 stage=0.0 press=0.0 ko=0.004 granted=0 legal=1-->
<!--SIM meme_momentum dwr=0.07 gain=2.018 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.031 stage=1.009 press=0.0 ko=0.003 granted=0 legal=0-->
<!--SIM turn_start_ap_gain dwr=0.03 gain=2.952 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.026 granted=0 legal=1-->
<!--SIM precision_focus dwr=0.03 gain=0.179 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=-0.001 granted=0 legal=1-->
<!--SIM desperation_surge dwr=0.03 gain=0.527 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.527 press=0.0 ko=0.016 granted=0 legal=1-->
<!--SIM kindling dwr=0.03 gain=0.0 deny=0.0 mit=0.0 chip=2.42 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.014 granted=1 legal=1-->
<!--SIM on_crit_bonus dwr=0.0 gain=0.082 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM sustain_regen dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=5.42 tax=0.0 stage=0.0 press=0.0 ko=0.001 granted=0 legal=1-->
<!--SIM status_resistance dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM low_ap_surge dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.422 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM low_ap_burst dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.409 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM spite_counter dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.058 ko=0.0 granted=0 legal=1-->
<!--SIM extraction_hit dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM stake_refund dwr=0.0 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM shattered_armor dwr=0.0 gain=0.09 deny=0.0 mit=0.04 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM uplink dwr=0.0 gain=0.422 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.0 granted=1 legal=1-->
<!--SIM defensive_reflex dwr=-0.03 gain=0.0 deny=0.0 mit=16.97 chip=0.0 heal=0.0 tax=1.465 stage=0.0 press=0.0 ko=-0.032 granted=0 legal=1-->
<!--SIM gecko_guard dwr=-0.03 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.985 press=0.0 ko=0.002 granted=0 legal=1-->
<!--SIM on_hit_momentum dwr=-0.07 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.311 press=0.0 ko=0.0 granted=0 legal=1-->
<!--SIM reactive_ping dwr=-0.07 gain=0.0 deny=0.0 mit=0.0 chip=1.66 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=0.001 granted=0 legal=1-->
<!--SIM streamlined_flow dwr=-0.2 gain=0.0 deny=0.0 mit=0.0 chip=0.0 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=-0.084 granted=1 legal=1-->
<!--SIM conductive_counter dwr=-0.47 gain=0.775 deny=0.0 mit=0.0 chip=3.12 heal=0.0 tax=0.0 stage=0.0 press=0.0 ko=-0.067 granted=0 legal=1-->

| Passive | Bearer | In a pool | dwr | AP gain | AP deny | Mit HP | Chip | Heal | Other | KO shift |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| unstable_code | Kreechank | no | 7.33 | 0 | 0 | 0 | 0 | 0 | 3.33 stages | +0.20 |
| firewall_passive | Keyper | no | 6.80 | 0 | 0 | 15.2 | 0 | 0 |  | +0.17 |
| double_or_nothing | illegal Meme | no | 6.27 | 1.45 | 0 | 0 | 0 | 0 | self-tax 0.63 | −0.05 |
| automated_pressure | Kitsauto | no | 4.60 | 0 | 0 | 0 | 0 | 0 | 3.43 foe drops | +0.11 |
| immutable_ledger | Blockwyrm | yes | 3.27 | 3.77 | 0 | 15.9 | 0 | 0 |  | +0.45 |
| patience_passive | Hopoch | yes | 1.53 | 0 | 0 | 0 | 0 | 5.4 | 2.97 Def stages | +0.01 |
| fee_collector | Deghoul | no | 1.07 | 0 | 0.89 | 0 | 0 | 0 |  | 0.00 |
| parallel_passive | Crackett | yes | 0.30 | 0 | 0 | 0 | 0 | 0 | 0.34 Speed | 0.00 |
| emergency_recovery | Kodiax | no | 0.10 | 0 | 0 | 0 | 0 | 6.3 |  | 0.00 |
| deep_sustain | Kodiax | no | 0.07 | 0 | 0 | 0 | 0 | 13.1 |  | 0.00 |
| meme_momentum | illegal Meme | no | 0.07 | 2.02 | 0 | 0 | 0 | 0 | 1.01 stage events, self-tax 0.03 | 0.00 |
| turn_start_ap_gain | Kodiax | no | 0.03 | 2.95 | 0 | 0 | 0 | 0 | shell already ~100% | +0.03 |
| precision_focus | Kodiax | no | 0.03 | 0.18 | 0 | 0 | 0 | 0 |  | 0.00 |
| desperation_surge | Kodiax | no | 0.03 | 0.53 | 0 | 0 | 0 | 0 | fires about half the fights | +0.02 |
| kindling | Cinislug | yes | 0.03 | 0 | 0 | 0 | 2.4 | 0 |  | +0.01 |
| on_crit_bonus | Kodiax | no | 0.00 | 0.08 | 0 | 0 | 0 | 0 | ~6.25% crit | 0.00 |
| sustain_regen | Kodiax | no | 0.00 | 0 | 0 | 0 | 0 | 5.4 | half of Deep Sustain | 0.00 |
| status_resistance | Kodiax | no | 0.00 | 0 | 0 | 0 | 0 | 0 | dead | 0.00 |
| low_ap_surge | Kodiax | no | 0.00 | 0 | 0 | 0 | 0 | 0 | 0.42 stages, only when drained | 0.00 |
| low_ap_burst | Kodiax | no | 0.00 | 0 | 0 | 0 | 0 | 0 | 0.41 stages, same window | 0.00 |
| spite_counter | Kodiax | no | 0.00 | 0 | 0 | 0 | 0 | 0 | 0.06 mirrors | 0.00 |
| extraction_hit | Kodiax | no | 0.00 | 0 | 0 | 0 | 0 | 0 | dead | 0.00 |
| stake_refund | Hopoch | no | 0.00 | 0 | 0 | 0 | 0 | 0 | 0 AP; this policy did not expire a buff | 0.00 |
| shattered_armor | Marrowhead | no | 0.00 | 0.09 | 0 | 0.04 | 0 | 0 | needs a chemistry-advantage hit | 0.00 |
| uplink | Validape | yes | 0.00 | 0.42 | 0 | 0 | 0 | 0 | rally line is 1.09 AP, still +0 wr | 0.00 |
| defensive_reflex | Kodiax | no | −0.03 | 0 | 0 | 17.0 | 0 | 0 | self-tax 1.47 AP | −0.03 |
| gecko_guard | Gilarime | no | −0.03 | 0 | 0 | 0 | 0 | 0 | fires once almost every fight | 0.00 |
| on_hit_momentum | Kodiax | no | −0.07 | 0 | 0 | 0 | 0 | 0 | 0.31 stages | 0.00 |
| reactive_ping | Kodiax | no | −0.07 | 0 | 0 | 0 | 1.7 | 0 |  | 0.00 |
| streamlined_flow | Humpacked | yes | −0.20 | 0 | 0 | 0 | 0 | 0 | discount and the Def drop together | −0.08 |
| conductive_counter | Forkrook | no | −0.47 | 0.78 | 0 | 0 | 3.1 | 0 | walls matchup −3.2 | −0.07 |

Where the shell is actually close:

| Passive | Tempo off → on | Walls off → on | Deny off → on |
| --- | --- | --- | --- |
| unstable_code | 76.7 → 86.0 (+9.3) | 100 → 100 | 63.1 → 75.8 (+12.7) |
| firewall_passive | 87.4 → 94.1 (+6.7) | 99.6 → 99.6 | 74.8 → 88.5 (+13.7) |
| automated_pressure | 83.9 → 92.1 (+8.2) | 99.9 → 100 | 84.4 → 89.9 (+5.5) |
| immutable_ledger | 99.7 → 100, KO 1.26 turns earlier | 100 → 100 | 87.5 → 97.0 (+9.5), +2.37 AP |
| patience_passive | 98.6 → 99.6 | 99.8 → 100 | 78.2 → 81.6 (+3.4), 2.25 Def stages |
| fee_collector | 80.0 → 80.2, deny 0.54 | 99 → 99, deny 0.86 | 62.1 → 65.1 (+3.0), deny 1.26 |
| kindling | 84 → 84, chip 0.02 | 98.8 → 98.9, chip 7.2 | 77 → 77, chip 0 |
| uplink, greedy | 100 → 100, +1.07 AP | 99.9 → 99.9, +0.01 AP | 88.2 → 88.2, +0.19 AP |
| streamlined_flow | 94.8 → 94.7 | 100 → 99.5 | 90.3 → 90.3 |
| conductive_counter | 94.9 → 94.9 | 99.2 → 96.0 (−3.2) | 61.6 → 63.4 (+1.8) |

Flags.

Near-mandatory if they were granted as they stand: Unstable Code, Firewall Passive, Automated Pressure. Each is +5 or more on at least one close opponent. They are not in a pool today, which is the only reason they are fair.

Granted and worth using: Immutable Ledger. On the tempo shell the win rate is already 99.7, and the real payoff is the first KO arriving 1.26 turns earlier, plus about 4.5 AP. On the denial shell it is +9.5 points and +2.4 AP.

Snowballs, no reset on swap: Patience walks Def every end step to +6 (about 3 stages a battle; the bench pauses the tick and does not clear the stages). Unstable walks a random stat to +6. Automated Pressure walks foe stats to −6. Streamlined walks its own Def to −6 while healthy. Meme Momentum walks both Atk by +2 a landed hit, with no tighter cap. Ledger, Kindling, Uplink, Fee Collector, and Parallel do not snowball.

Dead in this meta: Status Resistance and Extraction Hit, at zero of everything. Stake Refund paid 0 AP because the greedy Hopoch line did not expire a buff; the function is live and the card still needs the once-per-end-step line. Shattered Armor paid 0.09 AP because the policy rarely landed a chemistry-advantage hit. Low AP Surge and Burst do not fire on a clean open (mirror `low_ap_no_fire`) and only wrote about 0.4 stages when a foe had already cut AP. Spite mirrored 0.06 times a battle.

Same-rarity dominance: Deep Sustain heals 13.1 HP and Sustain Regen heals 5.4, both Rare, same bearer. The Rare typeless 3% heal does not exist as a reason to equip it.

Lower rarity beating a higher one: Firewall Passive is Rare, typed, 25% projectile, +6.80, no tax. Defensive Reflex is Legendary, typeless, 20% on everything, −0.03, and it taxes itself 1.47 AP. The Legendary is the worse card. Conductive Counter is Legendary and the only passive with a negative mean (−0.47), concentrated in the walls matchup (−3.2) where the counter's chip and AP do not pay for the fights it changes.

Counters, checked in the engine and in the traces:

- Swap pauses an end-step or turn-start tick. It does not revert stages already written. Patience and Unstable resume at the same stage.
- Cipher Lock stops new utility stages (Unstable, Parallel, Patience's Def, On Hit Momentum) and does not stop Gecko Guard or Desperation Surge. It does not stop Automated Pressure, because that delta is negative. It does not remove stages already written. See section (g).
- A full barrier absorb does not mark Kindling and does not fire on-hit, on-crit, or Conductive Counter. It does consume Ledger's first-hit mitigation and the +1 AP [C15].
- Focus fire beats Patience. In the scripted trace, Hopoch is at 14 HP before the Def +1 exists.
- Cleanse does not strip a stage. It can strip Kindled, because Kindled is a debuff status. The mark is one turn, so the cleanse has to be the same turn.

Uplink rally line, same 1,000 trials, Validape banks turns 1–2 when Rally is the plan: mean AP gain 1.086, win-rate delta 0.0 (tempo 1.075 AP, walls 1.998 AP, deny 0.184 AP, all at the same win rate as no Uplink). [C79] <!--RALLY uplink dwr=0.0 gain=1.086-->

## (e) Sensitivity to walls design R

R is the pending Protect / Screen / Shield proposal. Ember Wall becomes a priority full block with recharge. Vapor Barrier and Lattice Guard become HP pools. The other walls become category screens. Not decided. Re-ran the five highest and five lowest legal passives with that overlay. Illegal Meme rows were left out.

<!--WALL unstable_code engine=7.33 R=6.4 shift=-0.93-->
<!--WALL firewall_passive engine=6.8 R=6.5 shift=-0.3-->
<!--WALL automated_pressure engine=4.6 R=4.93 shift=0.33-->
<!--WALL immutable_ledger engine=3.27 R=3.23 shift=-0.04-->
<!--WALL patience_passive engine=1.53 R=1.6 shift=0.07-->
<!--WALL gecko_guard engine=-0.03 R=0.03 shift=0.06-->
<!--WALL on_hit_momentum engine=-0.07 R=-0.1 shift=-0.03-->
<!--WALL reactive_ping engine=-0.07 R=0.03 shift=0.1-->
<!--WALL streamlined_flow engine=-0.2 R=0.13 shift=0.33-->
<!--WALL conductive_counter engine=-0.47 R=0.5 shift=0.97-->

| Passive | Engine dwr | R dwr | Shift |
| --- | ---: | ---: | ---: |
| unstable_code | 7.33 | 6.40 | −0.93 |
| firewall_passive | 6.80 | 6.50 | −0.30 |
| automated_pressure | 4.60 | 4.93 | +0.33 |
| immutable_ledger | 3.27 | 3.23 | −0.04 |
| patience_passive | 1.53 | 1.60 | +0.07 |
| gecko_guard | −0.03 | 0.03 | +0.06 |
| on_hit_momentum | −0.07 | −0.10 | −0.03 |
| reactive_ping | −0.07 | 0.03 | +0.10 |
| streamlined_flow | −0.20 | 0.13 | +0.33 |
| conductive_counter | −0.47 | 0.50 | +0.97 |

None move by 5 points. The largest is Conductive Counter, +0.97, and it goes from a small loss to a small gain. Ledger's win rate does not care, which matches the engine order: the +1 AP and the 20% are applied before the absorb, so a Protect still "counts" as Ledger's first hit. Kindling was not in the top or bottom five. Its chip is already small, and a Protect would skip the mark on that one hit. That is a delay of one mark, not a rank change. [C77]

## (f) Type identity

Bot is the AP-denial type. Bot's only passive is Automated Pressure, and it denies 0 AP. It drops a foe's stat 3.43 times a battle and it is not granted [C65]. The live Bot denial tool is the move Packet Skim (Common, 1 AP, Drain 1), which is in Kitsauto's pool. No other passive drains a foe. Fee Collector (Gas, ungranted) is the only one, at 0.89 AP a battle, about 0.54 / 0.86 / 1.26 across the three opponents. That is less denial than a Bot who spends the turn pressing Packet Skim (1 AP removed per successful cast, at the cost of the action). Gas is allowed to tax AP. The profile says so. Fee Collector does not out-deny the Bot move. It does out-deny the Bot passive, because the Bot passive denies nothing.

Node is built to fight with allies. Uplink is the granted expression of that, and it does not pay on turn 1 with the live pool. Pack Rally is 3 AP. Turn 1 both actives have 2 [C28] [C70] [C71]. Hub Heal is also 3 AP and is a heal, which does not pay. Hammer Sync is 4 AP and does not buff. The greedy line, which attacks, gains 0.42 AP a battle and +0 win rate. The rally line, which banks, gains 1.09 AP and still +0 win rate. Walls almost never see a pay (0.006 AP), because Validape is busy staying alive. Uplink is real and it is late. Cipher or a full AP bar can eat the point after the claim is spent.

Gas is HP-fragile and is supposed to deny AP and chip. Deghoul's granted kit does the denial with Transaction Tax (Epic signature, Steal 1, cost 1), not with a passive. Fee Collector would be the passive half and it left the pool in the rarity reformat. Nothing else in the Gas passive slot exists. Kindling is Burned's chip, not Gas's. Putting Fee Collector back does not copy Bot's Packet Skim: one is a free conditional drain of the first actor, the other is a targeted 1-for-1 you choose.

Meme has no chemistry [C18]. Neither Meme passive applies a chemistry status. Both are Mythic, and the only Meme body in the data is Ghost at 0 HP [C45]. The sim's Meme rows use a labeled illegal copy of Kreechank's stats with type 14. Do not read +6.27 as a result a player can field. Script Loop and the Meme cards stay deferred. The one text fix is the miss drain on Meme Momentum [C54]. Do not build the immunity.

Burned Kindling stacks with Rugged. They are different statuses. On Cinislug's 134 HP the Kindled tick is 8 and Rugged's first tick (`1/16`) is 8, so both together are 16 in one end step [C81]. Kindling's own marginal chip in the greedy 2v2 is 2.42 HP, because tempo and denial fights rarely leave a marked foe alive for the tick (0.02 and 0.00). The walls fight is where it shows (7.2 HP). The 6% stays. The literal `0.06f` in the processor is a second copy of the catalog value, not a different number [C05] [C37].

The other types, short: Block's Ledger is the passive that matches the wall profile and it is the best granted card. Staked's Patience matches "patient" and it snowballs. Shard's Parallel matches "ally" weakly and omits its rate. Frozen's Gecko Guard is a one-shot stage package that ignores Cipher and is not granted, correctly, because it is Legendary next to Cryo Ray [C64]. Forked's Conductive Counter is a contact punish on a ranged signature species and it is not worth a pool slot at Legendary [C63]. Encrypted's Firewall is a real projectile wall and it is too strong to grant at 25% always-on [C68]. Glitched's Unstable is the strongest passive in the table and it is ungranted, which is the right accident [C67]. Mined's Extraction Hit is the empty card [C62]. Compressed's Streamlined is the identity (cheap moves, thin defense) and the defense tax currently repeats.

## (g) Speed and tempo

Speeds used below are the species bases the sim loads: Kitsauto 82, Deghoul 72, Forkrook 88, Keyper 68, Validape 60, Hopoch 58, Kreechank 52, Kodiax 46, Cinislug 28. Turn AP is +2, cap 10 [C28]. These four fights are seed 11, IV 0, scripted, and the check re-runs them.

**1. Two faster 2 AP hits, then the slow 4 AP. Patience is too late.** [C83] [C84]

Turn 1 order: Kitsauto 82, Deghoul 72, Hopoch 58, Kodiax 46. Kodiax has 2 AP, so Deep Dig (4) waits. Kitsauto's Blade Flurry lands 19 + 18 + 19. Deghoul's Gas Bump lands 50. That is 106, and Hopoch started at 120. Then Hopoch's Yield Spike hits for 52. End of turn, after those hits, Patience writes Def +1. Hopoch is at 14 HP, Def stage 1, Drained and Isolated. The stage did not touch the damage that mattered.

Turn 2 order is the same four, Hopoch now at 14 HP and 2 AP. Blade Flurry hits 12 and 13 and KOs Hopoch before Kodiax's Deep Dig. Deep Dig then KOs Deghoul for 149. The 4 AP heavy won its own exchange and lost the fighter the passive was supposed to protect. An end-of-turn Def stage loses to two faster 2 AP hits with no rider required.

**2. Uplink cannot pay until Rally is affordable.** [C85] [C86]

Turn 1 order: Deghoul 72, Validape 60, Hopoch 58, Kodiax 46. Everyone has 2 AP. Validape Snap Strikes. Kodiax Ore Strikes and KOs Deghoul. Pack Rally costs 3, so it is not legal. Uplink has nothing to pay: Snap Strike and Ore Strike do not buff an ally. Kodiax's +1 at end of turn is Pumped, from Mined hitting Gas. That is chemistry, not Uplink.

Turn 2: Validape passes (ends at 2, so turn 3 opens at 4). Turn 3: Validape Pack Rallies. Kodiax and Validape both get Atk +1 and Speed +1. Uplink's pstat for the fight is exactly 1 AP. Validape ends the turn at 1 AP (4 − 3). The point arrived on turn 3, after the foe had already had two turns of 2 AP hits, and after Validape had been put to 44 HP and Jammed on turn 1.

**3. Cipher pauses Unstable, and it does not erase the roll that already happened.** [C88] [C89] [C90]

Turn-start passives run before the action queue, for every active fighter. Keyper is faster (68 vs 52) and still cannot stop the opening roll.

Turn 1: Unstable writes Ranged Def +2. Then Cipher Shot hits for 31 and applies Cipher Lock. End of turn: Ranged Def +2, CipherLocked, 93 HP.

Turn 2: the lock is still up at turn start, so the new roll is zeroed. End of turn the lock expires. Stages are still only Ranged Def +2.

Turn 3: the lock is gone. Unstable writes Melee Def +1 on top of the old Ranged Def +2.

Swap would have paused the later rolls the same way the lock did, and it would also have kept the +2. Focus fire is the counter that works. In the unscripted example, Hopoch's second Yield Spike KOs the 18 HP Kreechank before a second roll matters.

**4. Kindling's tick is the same end step as the slow hit, which is after the fast 2 AP hits.** [C87] [C81]

Turn 1 order: Forkrook 88, Kitsauto 82, Kodiax 46, Cinislug 28. Kodiax passes with 2 AP. Forkrook Pulse Shot hits Cinislug for 48. Kitsauto Blade Flurry hits 12 and 12 and misses the third. Then Cinislug Pulse Shot hits Kitsauto for 36 and marks Kindled, because Kitsauto is alive. End of turn the 6% tick is 8, and Kitsauto is at 95. The fast hits did not get a burn. The slow hit did, and the burn landed before turn 2.

Turn 2: Kodiax has 4 AP and Deep Digs Kitsauto for 66. Cinislug's second Pulse Shot KOs. Kindling's total chip in the trace is 8, one tick. A 4 AP hit on turn 2 is swinging at a foe the end-step burn already softened, and the burn did not touch the turn-1 damage.

## (h) What works, what doesn't, what could be better

Works. Ledger is a clean Block passive: first hit, a number, an AP point, no walk to +6. Kindling's rule is readable and it does stack with Rugged without becoming a second Rugged. Uplink's once-per-ally rule is the right shape for a coordinator, and the lost-point line keeps it from doubling through Thread Lock. Fee Collector's "first actor, and only if they are ahead" is a fair Gas tax. The learn-pool band is doing its job: the three bombs are outside the pools.

Doesn't. Three empty or dead cards still wear a rarity: Extraction Hit, Status Resistance (retired, fine), and the Low AP pair on any turn that opens at 2 AP. Patience, Unstable, and Automated Pressure walk to ±6. The Legendary typeless reflex is worse than the Rare typed firewall. Streamlined's card says Def falls by 1 and the code walks to −6. Parallel hides a 25% roll. OnMiss passives fire for both fighters. Cipher does not see the stage column. The narration template still says "passive activated."

Could be better. Put the limits on the cards. Cap the three walks. Fill Extraction Hit at Common and leave Crystal Extraction alone. Give Deghoul Fee Collector at Common in place of Slipstream, so Gas has a passive tax that does not beat Packet Skim. Do not grant Unstable, Firewall, or Automated Pressure until the caps are in and a new sim says they are not +5 on a close match. Point the stage column and Meme stages at BuffGate. Split the two OnMiss passives so each matches its name.

## (i) Ranked proposals

Every item is a **PROPOSAL**. Pool edits below keep the hard band: wild pools stay five moves, the signature stays the unique top rarity, at most one non-signature sits one step above the body, everything else is at the body tier or lower, and a Common damaging move remains. Starters still only owe the unique signature, and they still owe a 4–5 move pool. Validape, Gilarime, and Kitsauto are the starters [C42] [C43] [C44] [C22] [C23] [C24].

**1. PROPOSAL — Cap the snowballs before anyone grants them.**

- Patience, live on Hopoch: utility cap 6 → 3. Heal and the +1 stay. Card: "End of turn, Def +1, up to +3. If you did not attack, heal 5% MaxHP." Files: `Moves.csv` row `patience_passive`, the `6` in `ChangeStage:MeleeDef:1:100%:Self:6`. Band: unchanged, already granted, Rare under Legendary Yield Spike [C69].
- Unstable Code, not granted: `RandomStageChange:1:1:100%:Self:3`. Card states +1, cap +3, every turn start. Do not add it to Kreechank. Error Correction already holds the one Uncommon slot, and Rare is two steps above a Common body [C67].
- Automated Pressure, not granted: cap −6 → −2. Card states the floor. Do not add it to Kitsauto until a re-sim of the −2 version. Packet Skim stays [C65].

Test `Patience_StopsAtDefThree`: after four end steps the Def stage is 3, not 4. Test `UnstableCode_RollsPlusOneAndStopsAtThree`. Test `AutomatedPressure_StopsAtMinusTwo`.

**2. PROPOSAL — Fill Extraction Hit at Common. Do not put it back at Rare.**

Kodiax body is Uncommon. Ore Strike is the Rare signature, so a second Rare cannot enter [C62]. Replace `baseline_slam` with `extraction_hit`. Pickaxe Flurry remains the Common damaging move. Crystal Extraction stays. Deep Dig stays. The above-body slot stays empty, which the band allows.

Payload, from the 10-17 shape, now at Common: trigger `OnHit`, utility `ChangeStage:MeleeAtk:1:40%:Self:3:false:extraction_hit`, plus one resolver branch: if that roll lands and Melee Atk is then +3, Speed +1, cap +1, once per battle. Card states 40%, cap +3, and the once-per-battle Speed point. A typed Common at 40% is ahead of the typeless Rare On Hit Momentum at 25%, which is the typed-beats-typeless rule, and it is not a second Rare next to Ore Strike.

Test `ExtractionHit_RaisesAtkAtFortyAndSpeedsOnceAtPlusThree`: two scripted hits with the roll forced, Atk goes 0 → 1, and the Speed point does not repeat.

**3. PROPOSAL — Put Fee Collector on Deghoul at Common, in Slipstream's slot.**

Gas is supposed to tax AP. The passive that does it is ungranted. Measured value is +1.07 win rate and 0.89 AP denied, which is not mandatory and does not beat Packet Skim per action. Transaction Tax stays the Epic signature. Volatile Shot stays the one Uncommon. Replace `slipstream` (Common) with `fee_collector` retiered Common. Gas Bump or Baseline Striker remains the Common damaging move [C66]. Card already states the first-actor rule. Rarity change is the whole mechanical edit.

Test `LearnPool_DeghoulFeeCollectorStaysInsideTheBand`: pool is five, signature Transaction Tax is the unique top, Fee Collector is Common, exactly one move (Volatile Shot) is Uncommon.

**4. PROPOSAL — Finish Streamlined as one Def drop.**

`TriggerOnce` true. Utility cap −1, not −6. Discount unchanged. Card: "While above 70% HP, your moves cost 1 AP less. The first action in that range also drops Def by 1." Humpacked's signature stays Streamlined, so the band is untouched [C59] [C35].

Test: extend `StreamlinedFlow_DropsDefOnceAbove70` so a second action above 70% HP does not drop Def again, and so the cap field is −1.

**5. PROPOSAL — Cipher Lock covers every positive stage a passive writes.**

`TriggerPassiveMove`'s stage-column loop and `ApplyMemeStage` call `BuffGate.FilterPositiveStage` the same way `ApplyStageDelta` does [C10] [C11] [C12]. Negative writes and reversions stay. Gecko Guard and Desperation Surge then pause under Cipher Shot, matching Unstable. Do not add Gecko Guard to Gilarime: it is Legendary, and Cryo Ray is already the unique Legendary signature [C64] [C43].

Test `CipherLock_BlocksGeckoGuardAndDesperationStageColumn`: a CipherLocked fighter at the HP threshold does not gain Def or Atk from those passives.

**6. PROPOSAL — Say the limit on every card that hides one.**

No mechanic change except where a row names one:

- Parallel: "When an ally's attack lands, 25% chance your Speed +1, cap +2." [C49]
- Uplink: "When you raise an ally's stage or apply a new buff, that ally gains 1 AP, once per ally per turn. A heal does not count. If that ally cannot gain the AP, the point is still spent."
- Stake Refund: "Once per end step in which at least one of your buffs expires, gain 1 AP."
- Shattered Armor: "Once per turn, a super-effective hit grants 5% mitigation for the rest of this turn and +1 AP." [C55]
- Meme Momentum, text only, cards stay deferred: add "A miss also drains 1 AP." Do not add immunity. Do not teach it to a living species. [C54]
- Low AP Surge / Burst, if P8 is also taken: trigger `EndOfTurn`, condition AP ≤ 0, Surge Speed +1 cap +1, Burst Speed +2 cap +2. They stay typeless Rare. They do not grant AP, so they do not rival Turn Start AP Gain. Test `LowAp_FiresAtEndOfTurnOnlyWhenApIsZero`.

**7. PROPOSAL — Split the OnMiss pair so each matches its name.**

Precision Focus refunds only when the holder's own attack missed. Reactive Ping damages the attacker only when the holder was the one missed. Today both run for both fighters [C13] [C14].

Test `OnMiss_PrecisionIsTheAttackerAndPingIsTheDefender`.

**8. PROPOSAL — Fix the two rarity inversions. No pool edit.**

- `sustain_regen` Rare → Common. Deep Sustain stays Rare at 6%. Same bearer, the 6% heal measured 13.1 HP and the 3% heal measured 5.4. A Common 3% does not rival a Rare 6%.
- `defensive_reflex` Legendary → Rare. Keep the 20% and the 1 AP self-tax. Firewall Passive stays Rare, typed, projectile-only, and ungranted. A Rare typeless that taxes itself should not sit above a Rare typed wall. Do not grant Firewall at 25% always-on: it was +6.80, and +13.7 on the denial shell. If it ever enters Keyper, it has to be a weaker version (first projectile each turn) and a new sim has to show it, at Common or by replacing a Common, because Cipher Shot already holds the Uncommon slot and Memory Leak is the Legendary signature [C68]. System Reset stays.

Test `RarityLadder_DeepSustainOutranksSustainRegen`: Deep Sustain's rarity is strictly above Sustain Regen's, and the heal percents stay 6 and 3.

**9. PROPOSAL — Narration says the name.**

`CombatNarration_Default.asset` `passiveTemplate` includes the passive's name. Uplink's grant line and lost-point line stay outcome sentences. This is the unlanded P4 [C17].

Test `Narration_PassiveTemplateNamesTheMove`.

**10. PROPOSAL — Kindling reads the catalog percent.**

`StatusProcessor` uses Kindled's `endOfTurnDamagePercent` (already 0.06) instead of the literal `0.06f` [C05] [C37]. The 6% does not change. The stack with Rugged does not change. Cinislug's pool does not change.

Test: the existing Kindling test asserts the chip equals `floor(MaxHP * catalog percent)`.

Not proposed, on purpose. No Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, Open Line, Cinder Idle, or Scorch Echo. No pull of System Reset, Hype Burst, Crystal Extraction, Ledger, Kindling, or the Meme cards. No second Node passive to make Uplink fire on turn 1. No Conductive Counter in Forkrook's pool while Fork Split is the Legendary signature and Rapid Strike already holds the Rare slot [C63]. No Gecko Guard in Gilarime's pool. No retune because of walls design R.

## (j) Consistency flags

- `StreamlinedFlow_DropsDefOnceAbove70` asserts cap −6 and does not assert once [C35]. The test name and the code disagree.
- `COMBAT_ISSUES_MASTER` marks CL-312 implemented. The gate is implemented. The stage-column and Meme paths are not covered [C11] [C12] [C36].
- Master marks EOT-ORDER and APR-RELAY implemented, unverified. The code and the named tests match the decision [C03] [C07].
- Older audit text that still says Uplink is ungranted is stale. The pool row has it [C70].
- PvP can field a passive the learn pool and the candy table never give out [C19] [C20] [C21]. That is how an ungranted Unstable Code would become legal if some other grant exists later.
- The 10-17 inventory is stale on Fee Collector (it said all foes; the code drains the first actor) [C08], on which passives are granted, and on the end-of-turn order.
- Base `tempo_2v2_sim.py` still drops Streamlined Def with no HP check [C72]. This round's numbers use the gated extension [C73].
- `passiveTemplate` and the resolver's `PassiveTriggered` message disagree about whether the player hears the name [C17].
- Jammed is Accuracy −1 [C40]. The 10-17 P16 ask for −2 did not land. Leave it. The decided value is −1.
- Double-or-Nothing's master row says fixed in PR. That fix is the text cut, not the old immunity sentence [C48-double_or_nothing].

## (k) Questions only Cronus can answer

1. Patience cap +3, or leave the live +6? Default: +3. The heal stays. Hopoch keeps the passive.
2. Fill Extraction Hit at Common in place of Baseline Slam, and leave Crystal Extraction? Default: yes. Rare cannot re-enter next to Ore Strike.
3. Fee Collector at Common in place of Slipstream on Deghoul? Default: yes. +1.07 win rate is a tax, not a must-pick, and Gas is the type that should have it.
4. Leave Unstable, Firewall, and Automated Pressure out of the pools until the caps land and a new sim is under +5 on the close shells? Default: yes.
5. Split OnMiss so Precision Focus is "you missed" and Reactive Ping is "you were missed"? Default: yes.
6. Uplink stays late, card-text only, no new 2 AP buff? Default: yes. Do not add a Node passive.
7. Demote Defensive Reflex to Rare and Sustain Regen to Common? Default: yes. No pool change.
8. Stage column and Meme stages go through Cipher Lock? Default: yes.
9. Narration template names the passive? Default: yes.
10. Ignore walls design R when tuning passives? Default: yes. Nothing in the top or bottom five moved by 5 points.

## Check tally

`passives_v2_check.py` prints one PASS/FAIL line per claim, with the file:line or CSV row. Last run: 166 PASS / 0 FAIL / 166 claims. [C91]
