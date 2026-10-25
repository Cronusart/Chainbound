# Rarity fit and rider build-out, v1

Proposals only. Nothing here is applied. Source of truth is the local snapshot `chainbound_combat_snapshot_20261023_0010` (working tree, 2026-10-23 ~00:10). The round-17 sim file is not the source of the win rates: its Ember Wall path still absorbs three full hits (C09), and the snapshot row is a 50% melee block for one hit (C08).

Checker: `69 PASS, 0 FAIL`. Every claim id below is that check. Every DV, per-AP, and win rate is computed by `rarity_fit_check.py` from the snapshot. This note does not reopen the 2026-10-22 no-effect verdicts for `script_loop`, `viral_surge`, `wildcard_swap`, `extraction_hit`, `deflation_mark`, `all_in`, and `recoil_strike` (C02).

## Key findings

The catalog is 211 moves (C01). One is boss-only and seven are the no-effect rows already decided, so the fit table has 203 moves. Rarity on this sheet is a complexity check plus a damage-per-AP check. 10 Rare-or-higher moves are plain hits with no wired rider: refined_slam, overdraw_snipe, combustive_shift, fork_split, ore_strike, stake_strike, yield_spike, lean_hit, shard_scatter, hype_train (V01). The ones that matter in a pool are Ore Strike, Stake Strike, Yield Spike, and Fork Split. Hype Train and Combustive Shift are the same hole with nobody learning them, and no species is Meme (C07). A false TRIG tag still sits on crystal_focus, ore_strike, stake_strike, crash_routine, hype_train (C12). Id-driven passives such as Kindling, On Crit Bonus, and Spite Counter are wired and are not part of that hole (C13).

Typed moves beat typeless in the cells with at least two cards on each side. P01 counts only those cells. A thin cell is listed in the table and is not a broken rule. Rare typeless hits are compared with typed commons of the same shape even when the AP differs (P02). Pulse Shot stays 26 (C10). Kindling stays 6% (C11). STAB is off (C03). The learn-pool band passes today (C06) and the rider proposals do not change a rarity, so it still passes (C17).

On the fight screen the sim is an active pair, no bench and no walls. Crackett's Seam Cut with the faster bonus wins 99.8% and Deep Dig is never paid (never (400/400)) (T1). With the bonus forced to x1.0 the fast side still wins 98.2% and Deep Dig comes out on turn 2.0 (T1c). The bonus is what keeps the 4 AP move unpaid. The win itself is Seam Cut on Crackett's body. Plain Pulse Shot plus Snap Strike lose: the heavy side wins 82.8% and casts on turn 2.0 (T2). Packet Skim, spent while a foe has 2 or more AP, moves that cast to turn 4.0 and the fast side wins 88.2% (T3). A 4 AP hit is not an automatic win.

## Top 5 recommendations

All five are **PROPOSAL**. AP stays where it is. None of them add or remove a learn-pool member.

1. **PROPOSAL.** Ore Strike, Kodiax's Rare signature, cracks Melee Def by 1 on a 25% proc. False TRIG becomes a real proc. DV 47.64 to 53.30. Lane ceiling 70.27.
2. **PROPOSAL.** Yield Spike, Hopoch's Legendary signature, heals 20% of damage dealt. Stake Strike heals 15%. Yield stays ahead per AP (30.77 vs 27.39).
3. **PROPOSAL.** Combustive Shift, a Legendary Gas two-hit with no rider, drains 1 AP once at 50%. DV 40.99 to 47.78. It is not added to a learn pool.
4. **PROPOSAL.** Hype Train, a Legendary Meme hit with a false TRIG and no Meme species, grants Surging at 100%. DV 47.64 to 58.96, under cap 70.27.
5. **PROPOSAL.** Fork Split and Shard Scatter do not gain damage. Each gets a 20% Speed −1. Chain Fork, the Forkrook Common, gets the same nick. The HP score does not move. The sim delta on Fork Split is 6.0 points.

## (a) How to read the score

Damage is a neutral hit. Attack and defense are both 60, so the ratio is 1. The roll walks 0.85 to 1.0 in 21 steps. Crit is 6.25% at x1.5 (C04). Expected hits, accuracy, and a x2 for a move that hits both foes are included. Conditional multipliers apply only when the fresh-turn gate passes. A full-HP foe with no statuses and equal speed fails HP-below, speed-higher, and stage-higher gates, so Seam Cut's x1.25, Deep Reap's lifesteal, and Hammer Sync's buff bonus add 0 on this screen. They still count as wired.

An AP point is 23.82, half of the median 2 AP single-target hit (47.64, n=31) (C05). Immediate drain is worth 0.6 of an AP point. Immediate gain is worth 1. A steal is both. A surcharge on yourself is a full AP-point debit. A surcharge on the foe is a drain. Delayed AP, from a status tick or Thread Lock, is worth half, and at most two ticks are counted. One stage of a damage stat is 0.5 of a reference hit, once, capped at 2 stages. The sign follows who it helps: your Atk up and the foe's Def down add, your Def down subtracts, and Gas Spike's +2 Atk / −2 Def nets near zero. Speed, accuracy, evasion, and priority add 0 HP. Surging is 25% of a reference hit. Exploit Mark is 15%. Cipher Lock and Frozen Assets add 0 HP and still count as wired. Kindling is 6% of median HP (124.0).

DV is damage plus rider HP. DV/AP is that total divided by AP. A 0 AP move has no per-AP rate.

**Band.** `plain` means Rare or higher with no wired rider. `low` means a damaging move whose DV/AP is under 80% of the previous rarity's median in the same shape and AP, and that median has at least three moves. `high` means a Common or Uncommon whose DV/AP is over 115% of the next rarity's median, same rule. `retired` is Status Resistance.

**Sensitivity.** The plain list does not use the weights (V01). stage_0.25 moves 0 low flags and 0 high flags. stage_0.75 moves 0 low flags and 0 high flags. drain_0.4 moves 0 low flags and 0 high flags. drain_0.8 moves 0 low flags and 0 high flags. speed_on moves 0 low flags and 0 high flags. delay_1 moves 0 low flags and 0 high flags.

## (b) Fit table

Boss-only and the seven decided no-effect ids are left out. `Wired` is the live payload or the id body, not the card text.

| Move | Rarity | Type | Cat | AP | Pow | Acc | Hits | Rc | Dmg | Rider | DV | DV/AP | Wired | Band | Pools |
|---|---|---|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|---|---|
| chain_seal | Common | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage, utility; tempo Speed -1 100%; tempo Evasion -1 | in | — |
| guard_stance | Common | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage | in | — |
| stone_barrage | Common | Block | DamagingMelee | 2 | 14 | 95 | 2-3 | 0 | 33.04 | 0.00 | 33.04 | 16.52 | stage; tempo Speed -1 20% | in | blockwyrm |
| blade_flurry | Common | Bot | DamagingMelee | 2 | 12 | 95 | 3-3 | 0 | 34.19 | 0.00 | 34.19 | 17.10 | — | in | kitsauto |
| debuff_script | Common | Bot | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage | in | kitsauto |
| packet_skim | Common | Bot | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 14.29 | utility | in | kitsauto |
| recursive_backup | Common | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | heal, active-gate | in | — |
| cinder_burst | Common | Burned | DamagingRanged | 3 | 15 | 95 | 2-4 | 0 | 42.17 | 13.58 | 55.75 | 18.58 | stage | in | cinislug |
| hype_burst | Common | Burned | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 9.53 | 9.53 | 4.76 | utility | in | cinislug |
| compression_burst | Common | Compressed | DamagingMelee | 3 | 20 | 95 | 2-2 | 0 | 37.35 | 22.63 | 59.98 | 19.99 | stage | in | — |
| compression_trade | Common | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage; tempo Speed -1 100% | in | humpacked |
| optimize_aim | Common | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy +2 | in | — |
| precision_strike | Common | Compressed | DamagingRanged | 2 | 34 | 100 | 1-1 | 0 | 32.92 | 0.00 | 32.92 | 16.46 | — | in | marrowhead |
| zip_step | Common | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -23.82 | stage, utility; tempo Evasion +2 | in | — |
| zipped_strikes | Common | Compressed | DamagingRanged | 2 | 18 | 95 | 2-2 | 0 | 33.62 | 0.00 | 33.62 | 16.81 | stage; tempo Speed +1 25% | in | humpacked |
| cipher_burst | Common | Encrypted | DamagingRanged | 3 | 17 | 95 | 2-3 | 0 | 39.70 | 0.00 | 39.70 | 13.23 | — | in | keyper |
| key_rotation | Common | Encrypted | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage, utility; tempo Evasion +1 | in | keyper |
| purge_protocol | Common | Encrypted | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | -16.67 | -16.67 | -8.34 | stage, utility | in | — |
| system_reset | Common | Encrypted | StatusUtility | 3 | 0 | 0 | 1-1 | 2 | 0.00 | 11.91 | 11.91 | 3.97 | utility | in | keyper |
| chain_fork | Common | Forked | DamagingMelee | 2 | 12 | 95 | 2-4 | 0 | 34.19 | 0.00 | 34.19 | 17.10 | — | in | forkrook |
| chilling_retort | Common | Frozen | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility | in | gilarime |
| cold_snap | Common | Frozen | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage; tempo Speed -2 100% | in | crackett |
| crystal_focus | Common | Frozen | StatusUtility | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 12.40 | 12.40 | — | stage, heal; tempo Speed +1 100%; heal 10% | in | — |
| frost_shards | Common | Frozen | DamagingRanged | 2 | 12 | 95 | 2-4 | 0 | 34.19 | 0.00 | 34.19 | 17.10 | stage; tempo Speed -1 20% | in | gilarime |
| hibernate | Common | Frozen | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 78.64 | 78.64 | 39.32 | stage, heal; heal 25% | in | — |
| gas_bump | Common | Gas | DamagingMelee | 2 | 36 | 95 | 1-1 | 0 | 33.14 | 0.00 | 33.14 | 16.57 | — | in | balloom,deghoul |
| slipstream | Common | Gas | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -23.82 | stage; tempo Speed +2 100% | in | balloom,deghoul |
| bug_fix | Common | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 19.55 | 19.55 | 19.55 | status, utility, heal; heal 10% | in | kreechank |
| defense_corrupt | Common | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage | in | kreechank |
| dice_roll | Common | Meme | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 24.80 | 24.80 | 24.80 | heal; heal 20% | in | — |
| lucky_scatter | Common | Meme | DamagingRanged | 3 | 17 | 95 | 1-4 | 0 | 39.70 | 6.79 | 46.49 | 15.50 | utility; random stage | in | — |
| crystal_extraction | Common | Mined | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 3.57 | utility; heal per debuff is 0 on a fresh foe | in | kodiax |
| pickaxe_flurry | Common | Mined | DamagingMelee | 3 | 14 | 95 | 2-4 | 0 | 39.65 | 45.26 | 84.90 | 28.30 | utility | in | kodiax,minefolk |
| accuracy_disrupt | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy -1 | in | — |
| accuracy_focus | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy +2 | in | — |
| baseline_blast | Common | None | DamagingRanged | 3 | 22 | 95 | 1-1 | 0 | 40.99 | 0.00 | 40.99 | 13.66 | — | in | — |
| baseline_jab | Common | None | DamagingMelee | 1 | 16 | 95 | 1-1 | 0 | 14.99 | 0.00 | 14.99 | 14.99 | priority; priority | in | crackett,rattlestake |
| baseline_multi_strike | Common | None | DamagingMelee | 2 | 8 | 95 | 2-5 | 0 | 27.01 | 0.00 | 27.01 | 13.50 | — | in | — |
| baseline_shot | Common | None | DamagingRanged | 2 | 26 | 95 | 1-1 | 0 | 24.13 | 0.00 | 24.13 | 12.06 | — | in | cinislug,cofi_lootwak,forkrook,minefolk,rattlestake |
| baseline_slam | Common | None | DamagingMelee | 2 | 30 | 85 | 1-1 | 0 | 24.77 | 0.00 | 24.77 | 12.38 | — | in | balloom,kodiax |
| baseline_snipe | Common | None | DamagingRanged | 2 | 30 | 90 | 1-1 | 0 | 26.23 | 0.00 | 26.23 | 13.11 | — | in | marrowhead |
| baseline_striker | Common | None | DamagingMelee | 2 | 30 | 95 | 1-1 | 0 | 27.68 | 0.00 | 27.68 | 13.84 | — | in | deghoul,hopoch,kreechank,validape |
| baseline_thrust | Common | None | DamagingMelee | 2 | 26 | 95 | 1-1 | 0 | 24.13 | 5.66 | 29.78 | 14.89 | stage | in | rattlestake |
| baseline_volley | Common | None | DamagingRanged | 2 | 16 | 95 | 1-1 | 0 | 29.98 | 0.00 | 29.98 | 14.99 | — | in | forkrook |
| cleanse | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 7.15 | utility | in | — |
| defense_break | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| defensive_stance | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| evasion_maneuver | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Evasion +2 | in | — |
| evasion_pin | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Evasion -1 | in | — |
| guard_posture | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 11.91 | 11.91 | 11.91 | utility | in | — |
| guardbreaker_combo | Common | None | DamagingMelee | 2 | 10 | 90 | 3-4 | 0 | 31.62 | 0.00 | 31.62 | 15.81 | — | in | — |
| position_swap | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | priority, id; priority; id swap | in | — |
| power_drain | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| power_surge | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| quick_lunge | Common | None | DamagingMelee | 2 | 26 | 90 | 1-1 | 0 | 22.86 | 0.00 | 22.86 | 11.43 | priority; priority | in | — |
| refined_blast | Common | None | DamagingRanged | 3 | 28 | 90 | 1-1 | 0 | 49.08 | 0.00 | 49.08 | 16.36 | — | in | — |
| refined_volley | Common | None | DamagingRanged | 2 | 22 | 90 | 1-1 | 0 | 38.83 | 0.00 | 38.83 | 19.42 | — | in | — |
| speed_boost | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage; tempo Speed +1 100% | in | — |
| speed_reduction | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage; tempo Speed -1 100% | in | — |
| isolation_tool | Common | Shard | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; ApplyChemistryStatus | in | crackett,marrowhead |
| blockade | Uncommon | Block | Support | 3 | 0 | 0 | 1-1 | 2 | 0.00 | 28.58 | 28.58 | 9.53 | utility, priority; block 60% x1; priority | in | blockwyrm |
| automation_burst | Uncommon | Bot | DamagingRanged | 4 | 38 | 95 | 1-1 | 0 | 69.99 | 6.79 | 76.78 | 19.20 | utility; random stage | in | — |
| multi_thread_shot | Uncommon | Bot | DamagingRanged | 2 | 12 | 95 | 2-3 | 0 | 28.49 | 5.66 | 34.15 | 17.08 | utility | in | kitsauto |
| self_repair_protocol | Uncommon | Bot | Support | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | 18.60 | heal; heal 15% | in | — |
| ash_guard | Uncommon | Burned | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 22.87 | 22.87 | 11.44 | utility, heal, priority; block 35% x1; heal 5%; priority | in | — |
| ember_shower | Uncommon | Burned | DamagingRanged | 4 | 38 | 95 | 1-1 | 0 | 69.99 | 11.31 | 81.31 | 20.33 | stage | in | — |
| kindling | Uncommon | Burned | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.44 | 7.44 | — | trigger, id; id Kindled 6% | in | cinislug |
| cipher_shot | Uncommon | Encrypted | DamagingRanged | 2 | 40 | 95 | 1-1 | 0 | 36.82 | 0.00 | 36.82 | 18.41 | status; Cipher Lock priced 0 HP | in | keyper |
| proof_shield | Uncommon | Encrypted | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | — |
| surge_recovery | Uncommon | Forked | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 31.00 | 31.00 | 15.50 | utility, heal; heal 15% | in | — |
| ice_wall | Uncommon | Frozen | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 19.06 | 19.06 | 9.53 | utility, priority; block 40% x1; GainApOnDamageIfSpeedHigher; priority | in | gilarime |
| vapor_barrier | Uncommon | Gas | Support | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 14.29 | 14.29 | 14.29 | utility, priority; block 30% x1; priority | in | — |
| volatile_shot | Uncommon | Gas | DamagingRanged | 4 | 72 | 95 | 1-1 | 0 | 65.75 | -23.82 | 41.93 | 10.48 | utility; self surcharge +1 | in | balloom,deghoul |
| error_correction | Uncommon | Glitched | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 25.75 | 25.75 | 12.87 | utility, heal; heal 15% | in | kreechank |
| meme_magic | Uncommon | Meme | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 24.80 | 24.80 | 12.40 | heal; heal 20% | in | — |
| deep_dig | Uncommon | Mined | DamagingMelee | 4 | 72 | 95 | 1-1 | 0 | 65.75 | 0.00 | 65.75 | 16.44 | — | in | kodiax |
| ore_restoration | Uncommon | Mined | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 31.95 | 31.95 | 15.97 | utility, heal; heal 20% | in | — |
| ap_router | Uncommon | Node | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | id; id AP transfer net 0 | in | — |
| hub_heal | Uncommon | Node | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | 6.20 | heal; heal 15% | in | validape |
| pack_rally | Uncommon | Node | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -7.94 | stage; tempo Speed +1 100% | in | validape |
| relay_packet | Uncommon | Node | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 3.57 | id; id relay one status | in | — |
| signal_redirect | Uncommon | Node | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 40.50 | 40.50 | 20.25 | stage, id; id redirect one hit | in | — |
| uplink | Uncommon | Node | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id +1 AP when you buff an ally | in | validape |
| ap_donation | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | id; id AP transfer net 0 | in | — |
| ap_drain | Uncommon | None | DamagingRanged | 2 | 16 | 95 | 1-1 | 0 | 14.99 | 19.24 | 34.23 | 17.11 | utility, id | in | — |
| ap_redistribution | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | id; id AP transfer net 0 | in | — |
| barrier_wall | Uncommon | None | Support | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 11.91 | 11.91 | 11.91 | utility, priority; block 25% x1; priority | in | humpacked |
| baseline_barrage | Uncommon | None | DamagingRanged | 2 | 10 | 95 | 2-3 | 0 | 47.68 | 0.00 | 47.68 | 23.84 | — | in | — |
| buff_ally | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | -47.64 | -47.64 | -23.82 | stage | in | — |
| damage_intercept | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 16.67 | 16.67 | 8.34 | id; id redirect one hit | in | — |
| effect_relay | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 3.57 | id; id relay one status | in | — |
| heal_ally | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | 9.30 | heal; heal 15% | in | — |
| rally_cry | Uncommon | None | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | 11.91 | 11.91 | 3.97 | status, stage; tempo Speed +1 100% | in | — |
| refined_barrage | Uncommon | None | DamagingRanged | 3 | 12 | 90 | 2-3 | 0 | 53.99 | 0.00 | 53.99 | 18.00 | — | in | — |
| refined_shot | Uncommon | None | DamagingRanged | 2 | 38 | 90 | 1-1 | 0 | 33.16 | 0.00 | 33.16 | 16.58 | — | in | minefolk |
| refined_striker | Uncommon | None | DamagingMelee | 2 | 42 | 90 | 1-1 | 0 | 36.56 | 0.00 | 36.56 | 18.28 | — | in | — |
| refined_thrust | Uncommon | None | DamagingMelee | 2 | 34 | 90 | 1-1 | 0 | 29.63 | 5.36 | 34.99 | 17.49 | stage | in | — |
| lattice_guard | Uncommon | Shard | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 19.06 | 19.06 | 9.53 | utility, priority; block 40% x1; BlockReflect; priority | in | — |
| team_buffer | Uncommon | Shard | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -7.94 | stage | in | — |
| civet_beam | Uncommon | Staked | DamagingRanged | 2 | 12 | 95 | 1-1 | 0 | 22.79 | 0.00 | 22.79 | 11.40 | stage; tempo Speed -1 100% | in | cofi_lootwak |
| compound_guard | Uncommon | Staked | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | -7.15 | -7.15 | -3.57 | stage, utility, priority; block 35% x1; priority | in | cofi_lootwak,rattlestake |
| stake_heal | Uncommon | Staked | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 31.00 | 31.00 | 15.50 | heal; heal 25% | in | cofi_lootwak,hopoch |
| immovable_stance | Rare | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | status | in | blockwyrm |
| solid_strike | Rare | Block | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | stage; tempo Speed -1 25% | in | kodiax |
| automated_pressure | Rare | Bot | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | — | utility, trigger; random stage | in | — |
| mev_extract | Rare | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 7.15 | status | in | — |
| scorch_shot | Rare | Burned | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 5.66 | 53.30 | 26.65 | stage | in | — |
| lean_hit | Rare | Compressed | DamagingMelee | 1 | 26 | 95 | 1-1 | 0 | 24.13 | 0.00 | 24.13 | 24.13 | — | plain | humpacked |
| risk_marker | Rare | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 14.29 | utility; foe surcharge +1 | in | — |
| firewall_passive | Rare | Encrypted | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger, id | in | — |
| rapid_strike | Rare | Forked | DamagingMelee | 1 | 26 | 95 | 1-1 | 0 | 24.13 | 0.00 | 24.13 | 24.13 | priority; priority | in | forkrook |
| frost_shot | Rare | Frozen | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | stage; tempo Speed -1 25% | in | gilarime |
| lock_assets | Rare | Frozen | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status; Frozen Assets priced 0 HP | in | — |
| stasis_field | Rare | Frozen | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; FieldEntryStageChange | in | — |
| ap_siphon | Rare | Gas | DamagingRanged | 3 | 54 | 95 | 1-1 | 0 | 49.42 | 6.79 | 56.21 | 18.74 | utility | in | — |
| fee_collector | Rare | Gas | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | id; id fee drain only when the foe is ahead | in | — |
| chaotic_resonance | Rare | Glitched | DamagingRanged | 3 | 54 | 95 | 1-1 | 0 | 49.42 | 0.00 | 49.42 | 16.47 | conditional | in | — |
| unstable_code | Rare | Glitched | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | — | utility, trigger; random stage | in | — |
| exploit_window | Rare | Mined | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 7.15 | status; Exploit Mark +15% | in | minefolk |
| ore_strike | Rare | Mined | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | — | plain | kodiax |
| refinery | Rare | Mined | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 23.82 | status, stage | in | — |
| coordinated_shot | Rare | Node | DamagingRanged | 3 | 56 | 95 | 1-1 | 0 | 51.28 | 0.00 | 51.28 | 17.09 | conditional | in | — |
| broadcast | Rare | None | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | — |
| deep_sustain | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.44 | 7.44 | — | heal, trigger; heal 6% | in | — |
| emergency_recovery | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | — | heal, trigger; heal 15% | in | — |
| low_ap_burst | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger | in | — |
| low_ap_surge | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger | in | — |
| on_crit_bonus | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 1.49 | 1.49 | — | id; id crit +1 AP | in | — |
| on_hit_momentum | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 5.96 | 5.96 | — | utility, trigger, id | in | — |
| overdraw_snipe | Rare | None | DamagingRanged | 3 | 46 | 85 | 1-1 | 0 | 37.74 | 0.00 | 37.74 | 12.58 | — | plain | — |
| precision_focus | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id refund on a miss | in | — |
| reactive_ping | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id 5% ping on a miss | in | — |
| refined_slam | Rare | None | DamagingMelee | 3 | 48 | 80 | 1-1 | 0 | 37.06 | 0.00 | 37.06 | 12.35 | — | plain | — |
| spite_counter | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.86 | 7.86 | — | id; id spite 33% | in | — |
| status_resistance | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | — | retired | — |
| sustain_regen | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 3.72 | 3.72 | — | heal, trigger; heal 3% | in | — |
| parallel_passive | Rare | Shard | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger; tempo Speed +1 | in | crackett |
| partition_shot | Rare | Shard | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | status; Frozen Assets priced 0 HP | in | marrowhead |
| seam_cut | Rare | Shard | DamagingMelee | 2 | 46 | 95 | 1-1 | 0 | 42.18 | 0.00 | 42.18 | 21.09 | conditional | in | crackett |
| shattered_armor | Rare | Shard | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id only on a chemistry advantage | in | — |
| patience_passive | Rare | Staked | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger, id | in | hopoch |
| stake_strike | Rare | Staked | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | — | plain | hopoch |
| yield_cycle | Rare | Staked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | status | in | — |
| bulwark_blow | Epic | Block | DamagingMelee | 3 | 50 | 90 | 1-1 | 0 | 43.45 | 0.00 | 43.45 | 14.48 | stat-override | in | blockwyrm |
| dust_fees | Epic | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy -1; tempo Evasion -1 | in | — |
| rallying_slam | Epic | Block | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | utility | in | — |
| bot_swarm | Epic | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 95.28 | 95.28 | 47.64 | status | in | — |
| fox_feint | Epic | Bot | StatusUtility | 1 | 0 | 0 | 1-1 | 999 | 0.00 | 0.00 | 0.00 | 0.00 | utility | in | kitsauto |
| flame_charge | Epic | Burned | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | utility, conditional | in | — |
| sear_field | Epic | Burned | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 23.82 | status | in | — |
| phantom_hack | Epic | Encrypted | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 22.63 | 70.27 | 35.14 | stage; tempo Speed -1 100% | in | — |
| verification_lock | Epic | Encrypted | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 61.93 | 61.93 | 61.93 | status, utility; foe surcharge +1 | in | — |
| charged_relay | Epic | Forked | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 6.20 | 6.20 | 3.10 | utility, heal; GainApIfDistinctTargetsHitAtLeast; heal 5% | in | — |
| pivot_line | Epic | Forked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage, utility; tempo Speed +1 100%; tempo Accuracy +1; tempo Evasion +1 | in | — |
| gas_cloud | Epic | Gas | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy -1; tempo Evasion -1 | in | — |
| transaction_tax | Epic | Gas | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 38.11 | 38.11 | 38.11 | utility | in | deghoul |
| bug_payoff | Epic | Glitched | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | conditional | in | — |
| glitch_feedback | Epic | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 21.44 | 21.44 | 21.44 | utility; foe surcharge +1 | in | — |
| majority_pressure | Epic | Mined | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | — |
| momentum_drill | Epic | Mined | DamagingMelee | 2 | 54 | 95 | 1-1 | 0 | 49.42 | 0.00 | 49.42 | 24.71 | conditional | in | — |
| extend_protocol | Epic | Node | StatusUtility | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 0.00 | 0.00 | 0.00 | utility; ExtendBuff | in | — |
| network_response | Epic | Node | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility | in | — |
| network_sync | Epic | Node | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; RaiseLowestStage | in | — |
| ap_boost | Epic | None | StatusUtility | 0 | 0 | 0 | 1-1 | 3 | 0.00 | 47.64 | 47.64 | — | utility | in | — |
| desperation_surge | Epic | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 71.46 | 71.46 | — | stage, utility, trigger | in | — |
| momentum_shift | Epic | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage | in | — |
| turn_start_ap_gain | Epic | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger, id | in | — |
| crystal_bash | Epic | Shard | DamagingMelee | 2 | 44 | 95 | 1-1 | 0 | 40.51 | 0.00 | 40.51 | 20.25 | conditional, stat-override | in | — |
| shard_partition | Epic | Shard | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | crackett |
| bean_sip | Epic | Staked | StatusUtility | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 33.74 | 33.74 | 33.74 | stage, utility, heal, priority; tempo Speed +1 100%; heal 8%; priority | in | cofi_lootwak |
| compound_returns | Epic | Staked | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 60.04 | 60.04 | 30.02 | stage, heal; heal 10% | in | — |
| interest_strike | Epic | Staked | DamagingMelee | 2 | 18 | 90 | 2-3 | 0 | 39.82 | 0.00 | 39.82 | 19.91 | utility | in | — |
| stake_refund | Epic | Staked | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger | in | — |
| immutable_ledger | Legendary | Block | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 33.35 | 33.35 | — | trigger, id; id 20% first hit +1 AP | in | blockwyrm |
| thread_lock | Legendary | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 7.15 | status; Thread Lock denies next +2 regen | in | — |
| ember_wall | Legendary | Burned | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 38.11 | 38.11 | 19.06 | utility, priority; block 50% x1; priority | in | cinislug |
| finish_burn | Legendary | Burned | DamagingRanged | 3 | 60 | 95 | 1-1 | 0 | 54.92 | 0.00 | 54.92 | 18.31 | conditional | in | — |
| streamlined_flow | Legendary | Compressed | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger; AP discount 1 | in | humpacked |
| memory_leak | Legendary | Encrypted | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 7.15 | stage, utility; tempo Speed +2 100%; armed AP -2 | in | keyper |
| conductive_counter | Legendary | Forked | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 30.97 | 30.97 | — | trigger, id; id counter 15% +1 AP | in | — |
| fork_split | Legendary | Forked | DamagingRanged | 4 | 56 | 95 | 1-1 | 0 | 102.56 | 0.00 | 102.56 | 25.64 | — | plain | forkrook |
| cryo_ray | Legendary | Frozen | DamagingRanged | 4 | 80 | 95 | 1-1 | 0 | 73.11 | 0.00 | 73.11 | 18.28 | stage; tempo Speed -2 25% | in | gilarime |
| gecko_guard | Legendary | Frozen | Passive | 0 | 0 | 0 | 1-1 | 999 | 0.00 | 47.64 | 47.64 | — | stage, trigger; tempo Speed +1 100% | in | — |
| combustive_shift | Legendary | Gas | DamagingRanged | 2 | 22 | 95 | 2-2 | 0 | 40.99 | 0.00 | 40.99 | 20.50 | — | plain | — |
| crash_routine | Legendary | Glitched | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 11.91 | stage | in | — |
| hype_train | Legendary | Meme | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | — | plain | — |
| deep_reap | Legendary | Mined | DamagingRanged | 2 | 58 | 95 | 1-1 | 0 | 53.10 | 0.00 | 53.10 | 26.55 | utility | in | hopoch,minefolk |
| hammer_sync | Legendary | Node | DamagingMelee | 4 | 80 | 95 | 1-1 | 0 | 73.11 | 0.00 | 73.11 | 18.28 | conditional | in | validape |
| overflow_reboot | Legendary | Node | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 12.40 | 12.40 | 6.20 | utility, heal; heal 10% | in | — |
| defensive_reflex | Legendary | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 33.35 | 33.35 | — | utility, trigger, id | in | — |
| field_split | Legendary | Shard | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; FieldEntryStageChange | in | marrowhead |
| shard_scatter | Legendary | Shard | DamagingRanged | 4 | 22 | 95 | 2-3 | 0 | 102.48 | 0.00 | 102.48 | 25.62 | — | plain | — |
| ap_reserve | Legendary | Staked | StatusUtility | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility; BankAPUntilNextTurn | in | rattlestake |
| yield_spike | Legendary | Staked | DamagingMelee | 2 | 56 | 95 | 1-1 | 0 | 51.28 | 0.00 | 51.28 | 25.64 | — | plain | hopoch |
| risk_spike | Mythic | Forked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 35.73 | 35.73 | 35.73 | status, stage; Surging +25% next hit | in | — |
| voltage_rush | Mythic | Forked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | -5.96 | -5.96 | -5.96 | stage, utility; tempo Speed +3 100%; self surcharge +1 | in | — |
| fuel_surge | Mythic | Gas | StatusUtility | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 71.46 | 71.46 | — | status, utility | in | balloom |
| chaos_strike | Mythic | Glitched | DamagingMelee | 2 | 58 | 95 | 1-1 | 0 | 59.73 | 0.00 | 59.73 | 29.87 | random | in | kreechank |
| data_scramble | Mythic | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage, utility; tempo Speed -1 100%; tempo Speed -1 | in | — |
| glitch_barrage | Mythic | Glitched | DamagingRanged | 3 | 18 | 95 | 2-4 | 0 | 50.43 | 6.79 | 57.22 | 19.07 | utility; random stage | in | — |
| double_or_nothing | Mythic | Meme | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 8.58 | 8.58 | — | trigger, id; id 60/40 AP | in | — |
| jackpot_shot | Mythic | Meme | DamagingRanged | 2 | 58 | 95 | 1-1 | 0 | 61.97 | 0.00 | 61.97 | 30.99 | random | in | — |
| meme_momentum | Mythic | Meme | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 88.61 | 88.61 | — | trigger, id; id meme momentum | in | — |

## (c) Card versus runtime

The PRIOR tag is the runtime. `CombatResolver` returns priority when the tag is PRIOR or PRIO (C04, line 7571). A silent PRIOR card is one whose description never says priority, first, or before: none.

False TRIG, the tag with no trigger column, no resolve condition, no proc, and no id body: crystal_focus, ore_strike, stake_strike, crash_routine, hype_train (C12). Walls, Charged Relay, and Surge Recovery are live triggers and stay off that list (C25).

**PROPOSAL.** Crystal Focus is a Common Frozen self-heal. The card says "At 0 AP". The row has heal 10, Speed +1, and an empty resolve column, so the heal runs at any AP (C23). Gate it with `ResolveConditions=Self:APAtMost:0`. Do not add power.

**PROPOSAL.** Crash Routine is a Legendary Glitched Atk drop. The stage `MeleeAtk:-1` runs. The TRIG tag does not (C24). Remove TRIG. Leave the Atk drop and the power at 0.

False APMAN: vapor_barrier (C26).

**PROPOSAL.** Vapor Barrier is tagged APMAN and its payload is a BlockWindow only (C26). Remove the APMAN tag. The card text does not promise an AP change.

HEAL tag with no heal payload: none.

Id bodies that the CSV payload does not duplicate, with the line that runs them:

| Move | Where | What the code does on a fresh turn |
|---|---|---|
| kindling | CombatResolver.cs:5660 | Marks the foe Kindled. The status deals 6% MaxHP at end of turn. |
| on_crit_bonus | CombatResolver.cs:4460 | Trigger column is None, and the None case calls the implicit OnCrit path. A crit grants 1 AP. |
| spite_counter | CombatResolver.cs:4108 | 33% chance to copy a debuff just applied to the holder. |
| immutable_ledger | id literal in CombatResolver | First hit each turn is cut 20%, and the holder gains 1 AP. CSV payload is empty. |
| conductive_counter | id literal, OnDamageTaken | Once a turn, 15% of contact damage taken comes back, plus 1 AP. |
| fee_collector | ApplyFeeCollectorDrains | Drains 1 from the first foe who is ahead on AP. Equal bars do nothing, so the fresh score is 0. |
| uplink | SettleUplink | The ally you buff gains 1 AP. No buff action, so the fresh score is 0. |
| meme_momentum | EndOfTurn id path | A landed hit raises Atk by 2 and grants 2 AP. A miss drops Atk and drains 1. |
| double_or_nothing | EndOfTurn id path | 60% gain 1 AP, 40% drain 1 AP. |
| shattered_armor | OnHit id path | 5% mitigation and 1 AP, only when the hit was super effective. Fresh neutral score is 0. |

When a CSV payload is present, the id fallback is skipped. `TriggerPassiveMove` clears the definition id if `utilityEffects` is not empty, so On Hit Momentum's 25% Atk stage runs from the CSV `ChangeStage`, not twice.

Typeless damaging moves inherit the attacker's primary type (`DamageCalculator.cs:546`). The typed-versus-typeless table below is the authored card, before that inheritance. Chemistry in the sim uses the move type, or the body type when the move is typeless. Meme has no matrix cells.

Quick Lunge is a Common typeless priority hit at power 26, accuracy 90 (C20). The card says priority. It is not a build-out.

## (d) Rider build-outs

Each row is a **PROPOSAL**. The opcode already exists. Engine cost is None. The cap is the best other damaging card in the same shape and AP when that card is already ahead. When this card is the best in the lane, the cap is 20% above its current DV. A weaker card does not pull the ceiling under this card. The tuner keeps a 0.75 HP gap under a peer that is ahead, and it picks the largest clean magnitude that fits (B01–B08). Yield's lifesteal percent stays above Stake's (B10). The 2 AP single ceiling in this score is phantom_hack at DV 70.27 (Epic Encrypted).

| Move | Rarity | Type | Rider | DV | DV/AP | Cap | Engine | Payload |
|---|---|---|---|---|---|---|---:|---|
| hype_train | Legendary | Meme | surging 1.0 | 47.64 -> 58.96 | 23.82 -> 29.48 | 70.27 | None | `StatusEffects=Surging:1:1.0:Self` |
| combustive_shift | Legendary | Gas | drain 0.5 | 40.99 -> 47.78 | 20.50 -> 23.89 | 49.19 | None | `UtilityEffects=DrainAP:1:50%:SingleEnemy:combustive_shift` |
| ore_strike | Rare | Mined | foe_def 0.25 | 47.64 -> 53.30 | 23.82 -> 26.65 | 70.27 | None | `UtilityEffects=ChangeStage:MeleeDef:-1:25%:SingleEnemy:-6:false:ore_strike` |
| yield_spike | Legendary | Staked | lifesteal 0.2 | 51.28 -> 61.54 | 25.64 -> 30.77 | 70.27 | None | `UtilityEffects=LifestealPercent:20%:yield_spike` |
| stake_strike | Rare | Staked | lifesteal 0.15 | 47.64 -> 54.79 | 23.82 -> 27.39 | 70.27 | None | `UtilityEffects=LifestealPercent:15%:stake_strike` |
| fork_split | Legendary | Forked | speed 0.2 | 102.56 -> 102.56 | 25.64 -> 25.64 | 123.07 | None | `StageChanges=Speed:-1:0.2:false` |
| shard_scatter | Legendary | Shard | speed 0.2 | 102.48 -> 102.48 | 25.62 -> 25.62 | 122.97 | None | `StageChanges=Speed:-1:0.2:false` |
| chain_fork | Common | Forked | speed 0.2 | 34.19 -> 34.19 | 17.10 -> 17.10 | 40.99 | None | `StageChanges=Speed:-1:0.2:false` |

Payload strings are the CSV fields to write:

| Move | Field | Value |
|---|---|---|
| ore_strike | UtilityEffects | `ChangeStage:MeleeDef:-1:25%:SingleEnemy:-6:false:ore_strike` |
| ore_strike | ResolveUtilityTiming | AfterEachConfirmedHit |
| ore_strike | MoveEffectsOncePerTarget | true |
| stake_strike | UtilityEffects | `LifestealPercent:15%:stake_strike` |
| stake_strike | ResolveUtilityTiming | AfterEachConfirmedHit |
| yield_spike | UtilityEffects | `LifestealPercent:20%:yield_spike` |
| yield_spike | ResolveUtilityTiming | AfterEachConfirmedHit |
| combustive_shift | UtilityEffects | `DrainAP:1:50%:SingleEnemy:combustive_shift` |
| combustive_shift | ResolveUtilityTiming | AfterMoveComplete |
| combustive_shift | MoveEffectsOncePerTarget | true |
| hype_train | StatusEffects | `Surging:1:1.0:Self` |
| fork_split | StageChanges | `Speed:-1:0.2:false` |
| shard_scatter | StageChanges | `Speed:-1:0.2:false` |
| chain_fork | StageChanges | `Speed:-1:0.2:false` |

Why these riders, and why they are not the deleted sentences:

- **Ore Strike** is Mined. The closed text cut removed "more damage per buff." Pickaxe Flurry already raises Atk. This proc lowers the foe's Def, which is the mined hit cracking a body. The TRIG tag becomes true. Kodiax's signature stays Ore Strike (C15).
- **Stake Strike** and **Yield Spike** are Staked. The old scaling sentence on Stake Strike stays deleted. Lifesteal matches siphon, and Yield's percent stays above Stake's (B09). Deep Reap remains Hopoch's off-type Mined tool (C16). It is not in the 3+1 pool.
- **Combustive Shift** is Gas. The closed text cut removed a damage bonus for a foe with no AP. A one-time drain is the Gas tax, and `MoveEffectsOncePerTarget` keeps the two hits from draining twice. Balloom and Deghoul can equip it because it is Gas. It is not inserted into their five-move pools.
- **Hype Train** is Meme. Meme gets no chemistry rider. Surging is a General consumed buff, and the damage code multiplies the next hit by 1.25. There is no Meme species, so this does not enter a pool (C07).
- **Fork Split**, **Shard Scatter**, and **Chain Fork** take a 20% Speed −1. Speed is 0 HP here, so the DV does not move and none of them becomes a new best pick on this screen. Forked's profile is propagation. Shard's profile is a precise nick. Chain Fork is the Common that currently has the shape and no rider.

Bot is not given a chemistry rider. No proposal adds a Meme chemistry status.

### 2v2 check

The sim is 400 trials, active pair only, no bench and no walls. It is not the Unity resolver and it is not the round-17 tempo sim (C09). Turn start grants +2 AP, cap 10, from 0. Order is speed, with a random tiebreak. Damage uses the snapshot roll, crit, and chemistry. An AllEnemies move hits every living foe. A power-0 utility still applies DrainAP, so Packet Skim cuts 1 AP. A miss on a single hit deals 0. The cost is paid only when the move is affordable. Recharge on these rows is 0, so the sim does not tick recharge. Side A is the side named first.

| Cell | Side A | Side B | A% | B% | Heavy cast turn | Hits before that cast |
|---|---|---|---:|---:|---:|---:|
| T1 | Kodiax banks Deep Dig, Hopoch Snap Strike | Crackett Seam Cut (x1.25 while faster), Deghoul Gas Bump | 0.2 | 99.8 | never (400/400) | 4.74 |
| T1c | same heavy | Seam Cut with the faster bonus forced to x1.0, Deghoul Gas Bump | 1.8 | 98.2 | 2.0 | 5.3 |
| T2 | same heavy | Crackett Pulse Shot, Deghoul Snap Strike | 82.8 | 17.2 | 2.0 | 4.11 |
| T3 | same heavy | Kitsauto skims a foe with 2+ AP, else Blade Flurry on the heavy; Deghoul Gas Bump | 11.8 | 88.2 | 4.0 | 11.93 |
| O0 | Kodiax Ore Strike, Hopoch Snap Strike | Keyper Cipher Shot, Kitsauto Blade Flurry | 4.8 | 95.2 | — | 0.0 |
| O1 | Ore Strike plus the Def crack | same foes | 4.0 | 96.0 | — | 0.0 |
| MO | Kodiax Ore Strike plus the Def crack, Hopoch Snap Strike | the same two bodies, plain Ore Strike | 53.0 | 47.0 | — | 0.0 |
| Y0 | Hopoch Yield Spike, Rattlestake Pulse Shot | Kodiax Ore Strike, Minefolk Pulse Shot | 11.8 | 88.2 | — | 0.0 |
| Y1 | Yield Spike plus lifesteal | same foes | 12.2 | 87.8 | — | 0.0 |
| S1 | Stake Strike plus lifesteal, same partner | same foes | 12.0 | 88.0 | — | 0.0 |
| MY | Hopoch Yield Spike plus lifesteal, Rattlestake Pulse Shot | the same two bodies, plain Yield Spike | 47.8 | 52.2 | — | 0.0 |
| MS | Hopoch Yield Spike plus lifesteal, Rattlestake Pulse Shot | Hopoch Stake Strike plus lifesteal, same partner | 51.5 | 48.5 | — | 0.0 |
| H0 | Kreechank stats casting Hype Train, Validape Snap Strike | Deghoul Gas Bump, Kitsauto Blade Flurry | 1.0 | 99.0 | — | 0.0 |
| H1 | Hype Train plus Surging | same foes | 0.2 | 99.8 | — | 0.0 |
| MH | Kreechank Hype Train plus Surging, Validape Snap Strike | the same two bodies, plain Hype Train | 50.8 | 49.2 | — | 0.0 |
| C0 | Balloom Combustive Shift, Deghoul Gas Bump | Kitsauto Blade Flurry, Keyper Cipher Shot | 0.5 | 99.5 | — | 0.0 |
| C1 | Combustive Shift plus the drain | same foes | 0.8 | 99.2 | — | 0.0 |
| MC | Balloom Combustive Shift plus the drain, Deghoul Gas Bump | the same two bodies, plain Combustive Shift | 51.5 | 48.5 | — | 0.0 |
| F0 | Forkrook Fork Split, Crackett Snap Strike | Deghoul Gas Bump, Keyper Cipher Shot | 78.0 | 22.0 | 2.0 | 3.73 |
| F1 | Fork Split plus Speed −1 at 20% | same foes | 78.0 | 22.0 | 2.0 | 3.73 |
| MF | Forkrook Fork Split plus the Speed nick, Crackett Snap Strike | the same two bodies, plain Fork Split | 53.0 | 47.0 | 2.0 | 3.64 |

T1, T1c, and T2 are the tempo rule. T1c turns Seam Cut's faster bonus off and leaves Crackett's stats. O0 through F1 are body context: the shell often decides the fight before the rider does. MO, MY, MH, MC, and MF are the rider measurement. Same bodies, one rider. The absolute gap is Ore 6.0, Yield 4.4, Hype 1.6, Combustive 3.0, Fork 6.0. The gate is 15 points (S01). Yield's lifesteal side finished MY at 47.8% to 52.2%. That gap is inside the gate. The DV screen is what keeps the siphon in band (B04, B09, B10). MS is Yield's lifesteal against Stake's lifesteal on the same Hopoch body (S02).

Kreechank's body is only a stat block for Hype Train. The move's type is Meme, so chemistry is neutral.

## (e) High common and uncommon cards

A lower rarity at the same or lower AP whose DV/AP is at least 95% of a higher rarity of the same type:

| Type | Lower | Rarity | AP | DV/AP | Higher | Rarity | AP | DV/AP | Ratio |
|---|---|---|---:|---:|---|---|---:|---:|---:|
| Mined | pickaxe_flurry | Common | 3 | 28.30 | deep_dig | Uncommon | 4 | 16.44 | 1.722 |
| Block | solid_strike | Rare | 2 | 23.82 | bulwark_blow | Epic | 3 | 14.48 | 1.645 |
| Gas | gas_bump | Common | 2 | 16.57 | volatile_shot | Uncommon | 4 | 10.48 | 1.581 |
| Burned | scorch_shot | Rare | 2 | 26.65 | finish_burn | Legendary | 3 | 18.31 | 1.456 |
| Frozen | frost_shot | Rare | 2 | 23.82 | cryo_ray | Legendary | 4 | 18.28 | 1.303 |
| Burned | flame_charge | Epic | 2 | 23.82 | finish_burn | Legendary | 3 | 18.31 | 1.301 |
| Glitched | bug_payoff | Epic | 2 | 23.82 | glitch_barrage | Mythic | 3 | 19.07 | 1.249 |
| Staked | stake_strike | Rare | 2 | 23.82 | interest_strike | Epic | 2 | 19.91 | 1.197 |
| Shard | partition_shot | Rare | 2 | 23.82 | crystal_bash | Epic | 2 | 20.25 | 1.176 |
| Block | stone_barrage | Common | 2 | 16.52 | bulwark_blow | Epic | 3 | 14.48 | 1.140 |
| Burned | scorch_shot | Rare | 2 | 26.65 | flame_charge | Epic | 2 | 23.82 | 1.119 |
| Shard | seam_cut | Rare | 2 | 21.09 | crystal_bash | Epic | 2 | 20.25 | 1.041 |

`high` on the fit table, and the response that keeps the learn-pool rule:

| Move | Response | New value | Why |
|---|---|---|---|
| — | leave | | no Common or Uncommon cleared the 115% bar |

A bump is applied in the checker to every pool that contains the move, then the same test as `PlayableLearnPoolsFollowTheBodyBand` is run (C06). Ghost and Cofi stay skipped. Off-type moves are outside that test, matching the C# test. Gas Bump beating Gas War per AP is the surcharge, and G01 names the lowest learnable rate. The response to that pair is Q6, not a new rarity.

## (f) Typed versus typeless

Typed median DV against typeless median DV, same rarity, shape, and AP. A cell with one card on a side is reported and is not treated as a broken rule.

| Rarity | Shape | AP | Typed n | Typed DV | Typeless n | Typeless DV | Typed higher |
|---|---|---:|---:|---:|---:|---:|---|
| Common | multi | 2 | 5 | 34.19 | 2 | 29.31 | yes |
| Common | single | 2 | 2 | 33.03 | 6 | 25.50 | yes |
| Uncommon | single | 2 | 1 | 36.82 | 4 | 34.61 | yes |
| Uncommon | spread | 2 | 1 | 22.79 | 1 | 47.68 | NO |
| Rare | single | 3 | 3 | 51.28 | 2 | 37.40 | yes |

Rare typeless against typed common, same shape and same AP. In line means the ratio is between 0.80 and 1.25. A cell is missing when no typed common shares that AP.

| Shape | AP | Rare typeless n | DV | Typed common n | DV | Ratio | | Names |
|---|---:|---:|---:|---:|---:|---:|---|---|
| — | | | | | | | | |

Rare typeless DV/AP against the typed-common median of the same shape, any AP (P02).

| Move | Shape | AP | Power | Acc | DV/AP | Typed common n | Typed common DV/AP | Ratio | | Pools |
|---|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| refined_slam | single | 3 | 48 | 80 | 12.35 | 2 | 16.51 | 0.748 | OUT | — |
| overdraw_snipe | single | 3 | 46 | 85 | 12.58 | 2 | 16.51 | 0.762 | OUT | — |

Pulse Shot is the budget typeless Common at power 26 (C10). It stays there. Titan Crash (`refined_slam`) and Rail Spike (`overdraw_snipe`) are the plain Rare typeless hits, at accuracy 80 and 85. **PROPOSAL:** leave Titan Crash and Rail Spike plain. Accuracy 80 and 85 is the commitment. They sit under that line (refined_slam ratio 0.748, overdraw_snipe ratio 0.762). Typed commons still win on DV/AP. Raising power or adding a rider would close the gap the rarity rule wants.

## (g) Ranked backlog

| # | PROPOSAL | Impact | Effort | Files | Depends | Test |
|---|---|---|---|---|---|---|
| 1 | Ore Strike Def −1 at 25%, once. | Kodiax signature stops being a false TRIG. | S | Moves.csv:131, Move_ore_strike.asset, card | Q1 | `RarityFitTests.OreStrike_CracksDefAndStaysSignature`: utility is the ChangeStage string, rarity Rare, Kodiax signature ore_strike, pool band passes. |
| 2 | Yield Spike lifesteal 20%. Stake Strike lifesteal 15%. | Staked signature gains a siphon and stays ahead of Stake Strike. | S | Moves.csv:145 and :143 | none | `RarityFitTests.YieldLifestealBeatsStake`: Yield percent greater than Stake percent, both Hopoch, signature still Yield Spike. |
| 3 | Combustive Shift DrainAP 1 at 50%, once. | Legendary Gas hit gains a tax without a second drain from the second hit. | S | Moves.csv:77 | Q3 | `RarityFitTests.CombustiveDrainsOnce`: utility DrainAP amount 1, once-per-target true, rarity Legendary, absent from Deghoul and Balloom learn lists. |
| 4 | Hype Train applies Surging at 100%. Drop the TRIG tag. | False TRIG on a Meme Legendary becomes a next-hit buff. | S | Moves.csv:195 | Q2 | `RarityFitTests.HypeTrain_AppliesSurging`: status Surging, no TRIG tag, rarity Legendary, no pool contains it. |
| 5 | Fork Split, Shard Scatter, Chain Fork: Speed −1 at 20%, once. | Three blank multi or spread cards match the type profile without a DV jump. | S | Moves.csv:118, :187, :123 | Q4 | `RarityFitTests.ForkSpeedNick_DoesNotChangePower`: stage Speed −1 chance 0.2, power unchanged, Forkrook signature still fork_split. |
| 6 | Rewrite the card sentence for each row in (d) to the Description stored on the proposal. | The card stops lying. | S | same rows, Description column | 1–5 | The test above also asserts the description contains the new rider and does not contain the deleted sentence. |
| 7 | Drop Gas War's self `NextMoveAPSurcharge`. | Gas Bump stops beating the Uncommon heavy per AP. Lowest learnable rate today is volatile_shot at 10.48 (G01). | S | Moves.csv:72 | Q6 | `RarityFitTests.GasWar_HasNoSelfSurcharge`: utility has no NextMoveAPSurcharge, power 72, AP 4, both Gas pools still list it under the signature. |
| 8 | Leave Lean Hit plain. | A 1 AP Rare stays the efficient click. | S | none | Q5 | `RarityFitTests.LeanHit_StaysPlain`: power 26, AP 1, rarity Rare, empty utility, Humpacked pool unchanged. |
| 9 | Leave Titan Crash and Rail Spike plain. | Rare typeless stays under the typed common line. | S | none | Q10 | `RarityFitTests.RareTypeless_StayLowAccuracy`: refined_slam accuracy 80, overdraw_snipe accuracy 85, both Rare, both typeless, both power unchanged. |
| 10 | Do not rebuild Status Resistance. | The retired row stays inert. | S | none | Q9 | `RarityFitTests.StatusResistance_StaysRetired`: description contains Retired, utility empty. |
| 11 | Silent PRIOR cards: none. Gate Crystal Focus with `ResolveConditions=Self:APAtMost:0`. Drop TRIG from Crash Routine. Drop APMAN from Vapor Barrier. No power change. | The card matches the resolver. | S | Moves.csv:116, :172, :78 | none | `RarityFitTests.CrystalFocus_GatesOnZeroAp`, `CrashRoutine_DropsFalseTrig`, `VaporBarrier_DropsApman`. |
| 12 | Re-import moves after the CSV edits and require asset power, AP, accuracy, and rarity to match. Today the checker sees 0 mismatches out of 211 assets (C18). | The SO stops drifting from the CSV. | S | Move_*.asset via the importer | 1–5 | `RarityFitTests.MovedRows_MatchAssets` on the eight edited ids. |

## (h) What works, what doesn't, what could be better

What works. The learn-pool band is real and it passes (C06). Typed commons sit above typeless commons where the cell is big enough (P01). Pulse Shot at 26 is what keeps that gap (C10). Priority is a tag the resolver honors. Kindling, Uplink, Cipher Lock, Surging, and Exploit Mark have bodies. The 2 AP rare singles are a readable damage step, and they do not need another damage buff.

What does not work. A Legendary or Rare that is only a hit is wearing a rarity the card does not earn. Ore Strike, Stake Strike, and Hype Train still say TRIG and then do nothing but damage (C12). Combustive Shift is two small hits at Legendary. Yield Spike is a stronger Stake Strike with a higher rarity and the same empty payload. Status Resistance is a Rare passive with no effect, and that part is already retired. The round-17 tempo sim still models Ember Wall as three full absorbs, so its old win rates are the wrong engine (C09).

What could be better. Put one existing opcode on each plain high-rarity hit, keep the number under the next tier, and say it on the card. Leave the AOE legendaries' damage alone. Stop pricing rarity as if it were a power ladder. The 52-power rares are already the power step. The missing piece is the rider.

## (i) Questions only Cronus can answer

| # | Question | Default | What it blocks |
|---|---|---|---|
| Q1 | Ship Ore Strike as a 25% Def crack. The closed per-buff sentence stays deleted. | Ship the Def crack. | Blocks B-ore. |
| Q2 | Ship Hype Train as Surging on the next hit. The closed consecutive ramp stays deleted. | Ship Surging. Leave it out of every pool. | Blocks B-hype. No species is Meme. |
| Q3 | Ship Combustive Shift as a one-time 1 AP drain. The closed missing-AP damage bonus stays deleted. | Ship the drain. Leave it out of the Gas learn pools. | Blocks B-combustive. |
| Q4 | Fork Split and Shard Scatter keep their damage. The new text is a 20% Speed nick, priced at 0 HP on this screen. | Ship the nick. Do not add damage. | Blocks B-fork and B-shard. |
| Q5 | Lean Hit stays a plain 1 AP Rare. The low cost is the whole card. | Leave Lean Hit alone. | Blocks any rider on lean_hit. |
| Q6 | Gas War's next-move surcharge. | Drop the surcharge. Power stays 72. AP stays 4. The earlier curve already asked for this. | Blocks a nerf of Gas Bump. |
| Q7 | Speed, accuracy, evasion, and priority stay at 0 HP in the rarity score. | Keep them at 0. Judge them in the sim. | Blocks a rescore of the whole table. |
| Q8 | STAB stays off. | Leave enableStab at 0. | Blocks any STAB term in the score. |
| Q9 | Status Resistance stays a dead Rare passive. | Do not rebuild it. | Blocks a rider on status_resistance. |
| Q10 | When a low rarity outruns a higher one, trim the number if a rarity bump would break a learn pool. | Trim. Bump only when the pool test still passes. | Blocks the over-deliver rows. |

## Check tally

69 PASS, 0 FAIL.

| Id | Result | Evidence |
|---|---|---|
| C01 | PASS | Moves.csv rows=211 header line 1 |
| C02 | PASS | excluded no-effect ids=['all_in', 'deflation_mark', 'extraction_hit', 'recoil_strike', 'script_loop', 'viral_surge', 'wildcard_swap'] boss-only=1 |
| C03 | PASS | ap 2/10 chem 1.2/0.8 stab 0 roll 0.85-1.0 crit x1.5 |
| C04 | PASS | base crit line 354; typeless inherits primary type at DamageCalculator.cs:546; PRIOR tag at CombatResolver.cs:7571 |
| C05 | PASS | AP point=23.82 ref hit=47.64 from 31 non-boss 2 AP single hits; median HP=124.0 |
| C06 | PASS | learn-pool band problems=0 |
| C07 | PASS | playable species with primary type Meme=none |
| C08 | PASS | ember_wall utility=BlockWindow:50%:Melee:2:wall_ember|WallHitRetaliation:1:wall_ember ap=2 rc=2 tags=SUP|TRIG|APMAN|PRIOR |
| C09 | PASS | round17 tempo_2v2_sim.py still says Ember Wall absorbs 3 full hits at line 22; snapshot ember_wall is BlockWindow 50% melee 1 hit |
| C10 | PASS | Pulse Shot power 26 ap 2 |
| C11 | PASS | Kindled EoTDamagePercent=0.06 applied at CombatResolver.cs:5660 |
| C12 | PASS | false TRIG=['crystal_focus', 'ore_strike', 'stake_strike', 'crash_routine', 'hype_train'] |
| C13 | PASS | on_crit implicit at CombatResolver.cs:4460; spite 0.33 at :4108; kindling at :5660 |
| C14 | PASS | plain Rare+ with no wired rider includes ore_strike, hype_train, combustive_shift, yield_spike |
| C15 | PASS | Kodiax pool ['ore_strike', 'deep_dig', 'pickaxe_flurry', 'crystal_extraction', 'baseline_slam'] signature ore_strike body Uncommon |
| C16 | PASS | Hopoch pool ['yield_spike', 'patience_passive', 'stake_strike', 'stake_heal', 'baseline_striker'] off-type ['deep_reap'] |
| C17 | PASS | rider build-outs change no rarity, so the learn-pool band is unchanged |
| C18 | PASS | move assets read=211 power/AP/accuracy/rarity mismatches=0 |
| B01 | PASS | hype_train surging mag 1.0 DV 47.64->58.96 perAP 23.82->29.48 cap 70.27 payload StatusEffects=Surging:1:1.0:Self |
| B02 | PASS | combustive_shift drain mag 0.5 DV 40.99->47.78 perAP 20.5->23.89 cap 49.19 payload UtilityEffects=DrainAP:1:50%:SingleEnemy:combustive_shift |
| B03 | PASS | ore_strike foe_def mag 0.25 DV 47.64->53.3 perAP 23.82->26.65 cap 70.27 payload UtilityEffects=ChangeStage:MeleeDef:-1:25%:SingleEnemy:-6:false:ore_strike |
| B04 | PASS | yield_spike lifesteal mag 0.2 DV 51.28->61.54 perAP 25.64->30.77 cap 70.27 payload UtilityEffects=LifestealPercent:20%:yield_spike |
| B05 | PASS | stake_strike lifesteal mag 0.15 DV 47.64->54.79 perAP 23.82->27.39 cap 70.27 payload UtilityEffects=LifestealPercent:15%:stake_strike |
| B06 | PASS | fork_split speed mag 0.2 DV 102.56->102.56 perAP 25.64->25.64 cap 123.07 payload StageChanges=Speed:-1:0.2:false |
| B07 | PASS | shard_scatter speed mag 0.2 DV 102.48->102.48 perAP 25.62->25.62 cap 122.97 payload StageChanges=Speed:-1:0.2:false |
| B08 | PASS | chain_fork speed mag 0.2 DV 34.19->34.19 perAP 17.1->17.1 cap 40.99 payload StageChanges=Speed:-1:0.2:false |
| B09 | PASS | yield perAP 30.77 stake perAP 27.39 |
| B10 | PASS | yield lifesteal 0.2 stake lifesteal 0.15 |
| P01 | PASS | typed-vs-typeless cells=5 violations with n>=2 on both sides=0 thin-cell misses=1 |
| P02 | PASS | same-AP rare-typeless cells=0 outside 0.80-1.25=0; cross-AP refined_slam DV/AP 12.35 vs typed-common single 16.51 ratio 0.748 OUT; overdraw_snipe DV/AP 12.58 vs typed-common single 16.51 ratio 0.762 OUT |
| T1 | PASS | fast Seam Cut x1.25 + Gas Bump vs banked Deep Dig: sideA 0.2% sideB 99.8% tie 0.0% heavyTurn never (400/400) hitsBefore 4.74 n=400 |
| T1c | PASS | Seam Cut bonus forced to x1.0 + Gas Bump vs banked Deep Dig: sideA 1.8% sideB 98.2% tie 0.0% heavyTurn 2.0 hitsBefore 5.3 n=400 |
| T2 | PASS | fast plain 2 AP hits vs banked Deep Dig: sideA 82.8% sideB 17.2% tie 0.0% heavyTurn 2.0 hitsBefore 4.11 n=400 |
| T3 | PASS | Packet Skim on a foe with 2+ AP, then Blade Flurry, vs banked Deep Dig: sideA 11.8% sideB 88.2% tie 0.0% heavyTurn 4.0 hitsBefore 11.93 n=400 |
| O0 | PASS | Ore Strike current: sideA 4.8% sideB 95.2% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| O1 | PASS | Ore Strike with the Def crack: sideA 4.0% sideB 96.0% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| Y0 | PASS | Yield Spike current: sideA 11.8% sideB 88.2% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| Y1 | PASS | Yield Spike with lifesteal: sideA 12.2% sideB 87.8% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| S1 | PASS | Stake Strike with lifesteal in the Yield shell: sideA 12.0% sideB 88.0% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| H0 | PASS | Hype Train current: sideA 1.0% sideB 99.0% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| H1 | PASS | Hype Train with Surging: sideA 0.2% sideB 99.8% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| C0 | PASS | Combustive Shift current: sideA 0.5% sideB 99.5% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| C1 | PASS | Combustive Shift with the drain: sideA 0.8% sideB 99.2% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| F0 | PASS | Fork Split current: sideA 78.0% sideB 22.0% tie 0.0% heavyTurn 2.0 hitsBefore 3.73 n=400 |
| F1 | PASS | Fork Split with the Speed nick: sideA 78.0% sideB 22.0% tie 0.0% heavyTurn 2.0 hitsBefore 3.73 n=400 |
| MO | PASS | mirror Ore Strike Def crack vs plain Ore Strike: sideA 53.0% sideB 47.0% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| MY | PASS | mirror Yield Spike lifesteal vs plain Yield Spike: sideA 47.8% sideB 52.2% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| MS | PASS | mirror Yield Spike lifesteal vs Stake Strike lifesteal: sideA 51.5% sideB 48.5% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| MH | PASS | mirror Hype Train Surging vs plain Hype Train: sideA 50.8% sideB 49.2% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| MC | PASS | mirror Combustive Shift drain vs plain Combustive Shift: sideA 51.5% sideB 48.5% tie 0.0% heavyTurn — hitsBefore 0.0 n=400 |
| MF | PASS | mirror Fork Split Speed nick vs plain Fork Split: sideA 53.0% sideB 47.0% tie 0.0% heavyTurn 2.0 hitsBefore 3.64 n=400 |
| S01 | PASS | mirror swings Ore 6.0 Yield 4.4 Hype 1.6 Combustive 3.0 Fork 6.0 points |
| S02 | PASS | Yield-with-rider vs Stake-with-rider mirror sideA 51.5% sideB 48.5% |
| G01 | PASS | lowest learnable damaging perAP is volatile_shot 10.48 DV 41.93 |
| V01 | PASS | plain Rare+ ids (10): combustive_shift, fork_split, hype_train, lean_hit, ore_strike, overdraw_snipe, refined_slam, shard_scatter, stake_strike, yield_spike |
| V_stage_0.25 | PASS | stage_0.25 low flips 0 high flips 0 |
| V_stage_0.75 | PASS | stage_0.75 low flips 0 high flips 0 |
| V_drain_0.4 | PASS | drain_0.4 low flips 0 high flips 0 |
| V_drain_0.8 | PASS | drain_0.8 low flips 0 high flips 0 |
| V_speed_on | PASS | speed_on low flips 0 high flips 0 |
| V_delay_1 | PASS | delay_1 low flips 0 high flips 0 |
| C19 | PASS | Forkrook pool ['fork_split', 'rapid_strike', 'baseline_shot', 'baseline_volley', 'chain_fork'] |
| C20 | PASS | quick_lunge power 26 acc 90 tags DMG|PRIO|PRIOR desc Quick priority lunge for stealing a finish. |
| C23 | PASS | crystal_focus line 116 heal 10 stage Speed:1:1.0:true resolve empty card says At 0 AP |
| C24 | PASS | crash_routine line 172 stage MeleeAtk:-1:1.0:false tags UTIL|TRIG|DMG effect is the Atk drop |
| C25 | PASS | walls and armed heals stay out of false TRIG; remaining=['crystal_focus', 'ore_strike', 'stake_strike', 'crash_routine', 'hype_train'] |
| C26 | PASS | false APMAN=['vapor_barrier'] |
| C21 | PASS | doc table rows=203 expected=203 mismatches=0 |
| C22 | PASS | every claim evidence except the table round-trip is present in RARITY_FIT_AND_RIDER_BUILDOUT_V1.md |

<!-- TABLE_BEGIN -->
| Move | Rarity | Type | Cat | AP | Pow | Acc | Hits | Rc | Dmg | Rider | DV | DV/AP | Wired | Band | Pools |
|---|---|---|---|---:|---:|---:|---|---:|---:|---:|---:|---:|---|---|---|
| chain_seal | Common | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage, utility; tempo Speed -1 100%; tempo Evasion -1 | in | — |
| guard_stance | Common | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage | in | — |
| stone_barrage | Common | Block | DamagingMelee | 2 | 14 | 95 | 2-3 | 0 | 33.04 | 0.00 | 33.04 | 16.52 | stage; tempo Speed -1 20% | in | blockwyrm |
| blade_flurry | Common | Bot | DamagingMelee | 2 | 12 | 95 | 3-3 | 0 | 34.19 | 0.00 | 34.19 | 17.10 | — | in | kitsauto |
| debuff_script | Common | Bot | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage | in | kitsauto |
| packet_skim | Common | Bot | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 14.29 | utility | in | kitsauto |
| recursive_backup | Common | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | heal, active-gate | in | — |
| cinder_burst | Common | Burned | DamagingRanged | 3 | 15 | 95 | 2-4 | 0 | 42.17 | 13.58 | 55.75 | 18.58 | stage | in | cinislug |
| hype_burst | Common | Burned | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 9.53 | 9.53 | 4.76 | utility | in | cinislug |
| compression_burst | Common | Compressed | DamagingMelee | 3 | 20 | 95 | 2-2 | 0 | 37.35 | 22.63 | 59.98 | 19.99 | stage | in | — |
| compression_trade | Common | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage; tempo Speed -1 100% | in | humpacked |
| optimize_aim | Common | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy +2 | in | — |
| precision_strike | Common | Compressed | DamagingRanged | 2 | 34 | 100 | 1-1 | 0 | 32.92 | 0.00 | 32.92 | 16.46 | — | in | marrowhead |
| zip_step | Common | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -23.82 | stage, utility; tempo Evasion +2 | in | — |
| zipped_strikes | Common | Compressed | DamagingRanged | 2 | 18 | 95 | 2-2 | 0 | 33.62 | 0.00 | 33.62 | 16.81 | stage; tempo Speed +1 25% | in | humpacked |
| cipher_burst | Common | Encrypted | DamagingRanged | 3 | 17 | 95 | 2-3 | 0 | 39.70 | 0.00 | 39.70 | 13.23 | — | in | keyper |
| key_rotation | Common | Encrypted | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage, utility; tempo Evasion +1 | in | keyper |
| purge_protocol | Common | Encrypted | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | -16.67 | -16.67 | -8.34 | stage, utility | in | — |
| system_reset | Common | Encrypted | StatusUtility | 3 | 0 | 0 | 1-1 | 2 | 0.00 | 11.91 | 11.91 | 3.97 | utility | in | keyper |
| chain_fork | Common | Forked | DamagingMelee | 2 | 12 | 95 | 2-4 | 0 | 34.19 | 0.00 | 34.19 | 17.10 | — | in | forkrook |
| chilling_retort | Common | Frozen | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility | in | gilarime |
| cold_snap | Common | Frozen | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage; tempo Speed -2 100% | in | crackett |
| crystal_focus | Common | Frozen | StatusUtility | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 12.40 | 12.40 | — | stage, heal; tempo Speed +1 100%; heal 10% | in | — |
| frost_shards | Common | Frozen | DamagingRanged | 2 | 12 | 95 | 2-4 | 0 | 34.19 | 0.00 | 34.19 | 17.10 | stage; tempo Speed -1 20% | in | gilarime |
| hibernate | Common | Frozen | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 78.64 | 78.64 | 39.32 | stage, heal; heal 25% | in | — |
| gas_bump | Common | Gas | DamagingMelee | 2 | 36 | 95 | 1-1 | 0 | 33.14 | 0.00 | 33.14 | 16.57 | — | in | balloom,deghoul |
| slipstream | Common | Gas | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -23.82 | stage; tempo Speed +2 100% | in | balloom,deghoul |
| bug_fix | Common | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 19.55 | 19.55 | 19.55 | status, utility, heal; heal 10% | in | kreechank |
| defense_corrupt | Common | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage | in | kreechank |
| dice_roll | Common | Meme | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 24.80 | 24.80 | 24.80 | heal; heal 20% | in | — |
| lucky_scatter | Common | Meme | DamagingRanged | 3 | 17 | 95 | 1-4 | 0 | 39.70 | 6.79 | 46.49 | 15.50 | utility; random stage | in | — |
| crystal_extraction | Common | Mined | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 3.57 | utility; heal per debuff is 0 on a fresh foe | in | kodiax |
| pickaxe_flurry | Common | Mined | DamagingMelee | 3 | 14 | 95 | 2-4 | 0 | 39.65 | 45.26 | 84.90 | 28.30 | utility | in | kodiax,minefolk |
| accuracy_disrupt | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy -1 | in | — |
| accuracy_focus | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy +2 | in | — |
| baseline_blast | Common | None | DamagingRanged | 3 | 22 | 95 | 1-1 | 0 | 40.99 | 0.00 | 40.99 | 13.66 | — | in | — |
| baseline_jab | Common | None | DamagingMelee | 1 | 16 | 95 | 1-1 | 0 | 14.99 | 0.00 | 14.99 | 14.99 | priority; priority | in | crackett,rattlestake |
| baseline_multi_strike | Common | None | DamagingMelee | 2 | 8 | 95 | 2-5 | 0 | 27.01 | 0.00 | 27.01 | 13.50 | — | in | — |
| baseline_shot | Common | None | DamagingRanged | 2 | 26 | 95 | 1-1 | 0 | 24.13 | 0.00 | 24.13 | 12.06 | — | in | cinislug,cofi_lootwak,forkrook,minefolk,rattlestake |
| baseline_slam | Common | None | DamagingMelee | 2 | 30 | 85 | 1-1 | 0 | 24.77 | 0.00 | 24.77 | 12.38 | — | in | balloom,kodiax |
| baseline_snipe | Common | None | DamagingRanged | 2 | 30 | 90 | 1-1 | 0 | 26.23 | 0.00 | 26.23 | 13.11 | — | in | marrowhead |
| baseline_striker | Common | None | DamagingMelee | 2 | 30 | 95 | 1-1 | 0 | 27.68 | 0.00 | 27.68 | 13.84 | — | in | deghoul,hopoch,kreechank,validape |
| baseline_thrust | Common | None | DamagingMelee | 2 | 26 | 95 | 1-1 | 0 | 24.13 | 5.66 | 29.78 | 14.89 | stage | in | rattlestake |
| baseline_volley | Common | None | DamagingRanged | 2 | 16 | 95 | 1-1 | 0 | 29.98 | 0.00 | 29.98 | 14.99 | — | in | forkrook |
| cleanse | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 7.15 | utility | in | — |
| defense_break | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| defensive_stance | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| evasion_maneuver | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Evasion +2 | in | — |
| evasion_pin | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Evasion -1 | in | — |
| guard_posture | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 11.91 | 11.91 | 11.91 | utility | in | — |
| guardbreaker_combo | Common | None | DamagingMelee | 2 | 10 | 90 | 3-4 | 0 | 31.62 | 0.00 | 31.62 | 15.81 | — | in | — |
| position_swap | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | priority, id; priority; id swap | in | — |
| power_drain | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| power_surge | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | stage | in | — |
| quick_lunge | Common | None | DamagingMelee | 2 | 26 | 90 | 1-1 | 0 | 22.86 | 0.00 | 22.86 | 11.43 | priority; priority | in | — |
| refined_blast | Common | None | DamagingRanged | 3 | 28 | 90 | 1-1 | 0 | 49.08 | 0.00 | 49.08 | 16.36 | — | in | — |
| refined_volley | Common | None | DamagingRanged | 2 | 22 | 90 | 1-1 | 0 | 38.83 | 0.00 | 38.83 | 19.42 | — | in | — |
| speed_boost | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage; tempo Speed +1 100% | in | — |
| speed_reduction | Common | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage; tempo Speed -1 100% | in | — |
| isolation_tool | Common | Shard | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; ApplyChemistryStatus | in | crackett,marrowhead |
| blockade | Uncommon | Block | Support | 3 | 0 | 0 | 1-1 | 2 | 0.00 | 28.58 | 28.58 | 9.53 | utility, priority; block 60% x1; priority | in | blockwyrm |
| automation_burst | Uncommon | Bot | DamagingRanged | 4 | 38 | 95 | 1-1 | 0 | 69.99 | 6.79 | 76.78 | 19.20 | utility; random stage | in | — |
| multi_thread_shot | Uncommon | Bot | DamagingRanged | 2 | 12 | 95 | 2-3 | 0 | 28.49 | 5.66 | 34.15 | 17.08 | utility | in | kitsauto |
| self_repair_protocol | Uncommon | Bot | Support | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | 18.60 | heal; heal 15% | in | — |
| ash_guard | Uncommon | Burned | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 22.87 | 22.87 | 11.44 | utility, heal, priority; block 35% x1; heal 5%; priority | in | — |
| ember_shower | Uncommon | Burned | DamagingRanged | 4 | 38 | 95 | 1-1 | 0 | 69.99 | 11.31 | 81.31 | 20.33 | stage | in | — |
| kindling | Uncommon | Burned | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.44 | 7.44 | — | trigger, id; id Kindled 6% | in | cinislug |
| cipher_shot | Uncommon | Encrypted | DamagingRanged | 2 | 40 | 95 | 1-1 | 0 | 36.82 | 0.00 | 36.82 | 18.41 | status; Cipher Lock priced 0 HP | in | keyper |
| proof_shield | Uncommon | Encrypted | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | — |
| surge_recovery | Uncommon | Forked | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 31.00 | 31.00 | 15.50 | utility, heal; heal 15% | in | — |
| ice_wall | Uncommon | Frozen | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 19.06 | 19.06 | 9.53 | utility, priority; block 40% x1; GainApOnDamageIfSpeedHigher; priority | in | gilarime |
| vapor_barrier | Uncommon | Gas | Support | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 14.29 | 14.29 | 14.29 | utility, priority; block 30% x1; priority | in | — |
| volatile_shot | Uncommon | Gas | DamagingRanged | 4 | 72 | 95 | 1-1 | 0 | 65.75 | -23.82 | 41.93 | 10.48 | utility; self surcharge +1 | in | balloom,deghoul |
| error_correction | Uncommon | Glitched | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 25.75 | 25.75 | 12.87 | utility, heal; heal 15% | in | kreechank |
| meme_magic | Uncommon | Meme | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 24.80 | 24.80 | 12.40 | heal; heal 20% | in | — |
| deep_dig | Uncommon | Mined | DamagingMelee | 4 | 72 | 95 | 1-1 | 0 | 65.75 | 0.00 | 65.75 | 16.44 | — | in | kodiax |
| ore_restoration | Uncommon | Mined | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 31.95 | 31.95 | 15.97 | utility, heal; heal 20% | in | — |
| ap_router | Uncommon | Node | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | id; id AP transfer net 0 | in | — |
| hub_heal | Uncommon | Node | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | 6.20 | heal; heal 15% | in | validape |
| pack_rally | Uncommon | Node | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -7.94 | stage; tempo Speed +1 100% | in | validape |
| relay_packet | Uncommon | Node | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 3.57 | id; id relay one status | in | — |
| signal_redirect | Uncommon | Node | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 40.50 | 40.50 | 20.25 | stage, id; id redirect one hit | in | — |
| uplink | Uncommon | Node | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id +1 AP when you buff an ally | in | validape |
| ap_donation | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | id; id AP transfer net 0 | in | — |
| ap_drain | Uncommon | None | DamagingRanged | 2 | 16 | 95 | 1-1 | 0 | 14.99 | 19.24 | 34.23 | 17.11 | utility, id | in | — |
| ap_redistribution | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | id; id AP transfer net 0 | in | — |
| barrier_wall | Uncommon | None | Support | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 11.91 | 11.91 | 11.91 | utility, priority; block 25% x1; priority | in | humpacked |
| baseline_barrage | Uncommon | None | DamagingRanged | 2 | 10 | 95 | 2-3 | 0 | 47.68 | 0.00 | 47.68 | 23.84 | — | in | — |
| buff_ally | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | -47.64 | -47.64 | -23.82 | stage | in | — |
| damage_intercept | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 16.67 | 16.67 | 8.34 | id; id redirect one hit | in | — |
| effect_relay | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 3.57 | id; id relay one status | in | — |
| heal_ally | Uncommon | None | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | 9.30 | heal; heal 15% | in | — |
| rally_cry | Uncommon | None | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | 11.91 | 11.91 | 3.97 | status, stage; tempo Speed +1 100% | in | — |
| refined_barrage | Uncommon | None | DamagingRanged | 3 | 12 | 90 | 2-3 | 0 | 53.99 | 0.00 | 53.99 | 18.00 | — | in | — |
| refined_shot | Uncommon | None | DamagingRanged | 2 | 38 | 90 | 1-1 | 0 | 33.16 | 0.00 | 33.16 | 16.58 | — | in | minefolk |
| refined_striker | Uncommon | None | DamagingMelee | 2 | 42 | 90 | 1-1 | 0 | 36.56 | 0.00 | 36.56 | 18.28 | — | in | — |
| refined_thrust | Uncommon | None | DamagingMelee | 2 | 34 | 90 | 1-1 | 0 | 29.63 | 5.36 | 34.99 | 17.49 | stage | in | — |
| lattice_guard | Uncommon | Shard | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 19.06 | 19.06 | 9.53 | utility, priority; block 40% x1; BlockReflect; priority | in | — |
| team_buffer | Uncommon | Shard | Support | 3 | 0 | 0 | 1-1 | 0 | 0.00 | -23.82 | -23.82 | -7.94 | stage | in | — |
| civet_beam | Uncommon | Staked | DamagingRanged | 2 | 12 | 95 | 1-1 | 0 | 22.79 | 0.00 | 22.79 | 11.40 | stage; tempo Speed -1 100% | in | cofi_lootwak |
| compound_guard | Uncommon | Staked | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | -7.15 | -7.15 | -3.57 | stage, utility, priority; block 35% x1; priority | in | cofi_lootwak,rattlestake |
| stake_heal | Uncommon | Staked | Support | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 31.00 | 31.00 | 15.50 | heal; heal 25% | in | cofi_lootwak,hopoch |
| immovable_stance | Rare | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | status | in | blockwyrm |
| solid_strike | Rare | Block | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | stage; tempo Speed -1 25% | in | kodiax |
| automated_pressure | Rare | Bot | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | — | utility, trigger; random stage | in | — |
| mev_extract | Rare | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 7.15 | status | in | — |
| scorch_shot | Rare | Burned | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 5.66 | 53.30 | 26.65 | stage | in | — |
| lean_hit | Rare | Compressed | DamagingMelee | 1 | 26 | 95 | 1-1 | 0 | 24.13 | 0.00 | 24.13 | 24.13 | — | plain | humpacked |
| risk_marker | Rare | Compressed | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 14.29 | utility; foe surcharge +1 | in | — |
| firewall_passive | Rare | Encrypted | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger, id | in | — |
| rapid_strike | Rare | Forked | DamagingMelee | 1 | 26 | 95 | 1-1 | 0 | 24.13 | 0.00 | 24.13 | 24.13 | priority; priority | in | forkrook |
| frost_shot | Rare | Frozen | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | stage; tempo Speed -1 25% | in | gilarime |
| lock_assets | Rare | Frozen | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status; Frozen Assets priced 0 HP | in | — |
| stasis_field | Rare | Frozen | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; FieldEntryStageChange | in | — |
| ap_siphon | Rare | Gas | DamagingRanged | 3 | 54 | 95 | 1-1 | 0 | 49.42 | 6.79 | 56.21 | 18.74 | utility | in | — |
| fee_collector | Rare | Gas | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | id; id fee drain only when the foe is ahead | in | — |
| chaotic_resonance | Rare | Glitched | DamagingRanged | 3 | 54 | 95 | 1-1 | 0 | 49.42 | 0.00 | 49.42 | 16.47 | conditional | in | — |
| unstable_code | Rare | Glitched | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | — | utility, trigger; random stage | in | — |
| exploit_window | Rare | Mined | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 7.15 | 7.15 | 7.15 | status; Exploit Mark +15% | in | minefolk |
| ore_strike | Rare | Mined | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | — | plain | kodiax |
| refinery | Rare | Mined | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 23.82 | status, stage | in | — |
| coordinated_shot | Rare | Node | DamagingRanged | 3 | 56 | 95 | 1-1 | 0 | 51.28 | 0.00 | 51.28 | 17.09 | conditional | in | — |
| broadcast | Rare | None | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | — |
| deep_sustain | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.44 | 7.44 | — | heal, trigger; heal 6% | in | — |
| emergency_recovery | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 18.60 | 18.60 | — | heal, trigger; heal 15% | in | — |
| low_ap_burst | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger | in | — |
| low_ap_surge | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger | in | — |
| on_crit_bonus | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 1.49 | 1.49 | — | id; id crit +1 AP | in | — |
| on_hit_momentum | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 5.96 | 5.96 | — | utility, trigger, id | in | — |
| overdraw_snipe | Rare | None | DamagingRanged | 3 | 46 | 85 | 1-1 | 0 | 37.74 | 0.00 | 37.74 | 12.58 | — | plain | — |
| precision_focus | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id refund on a miss | in | — |
| reactive_ping | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id 5% ping on a miss | in | — |
| refined_slam | Rare | None | DamagingMelee | 3 | 48 | 80 | 1-1 | 0 | 37.06 | 0.00 | 37.06 | 12.35 | — | plain | — |
| spite_counter | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 7.86 | 7.86 | — | id; id spite 33% | in | — |
| status_resistance | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | — | retired | — |
| sustain_regen | Rare | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 3.72 | 3.72 | — | heal, trigger; heal 3% | in | — |
| parallel_passive | Rare | Shard | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger; tempo Speed +1 | in | crackett |
| partition_shot | Rare | Shard | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | status; Frozen Assets priced 0 HP | in | marrowhead |
| seam_cut | Rare | Shard | DamagingMelee | 2 | 46 | 95 | 1-1 | 0 | 42.18 | 0.00 | 42.18 | 21.09 | conditional | in | crackett |
| shattered_armor | Rare | Shard | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | trigger, id; id only on a chemistry advantage | in | — |
| patience_passive | Rare | Staked | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger, id | in | hopoch |
| stake_strike | Rare | Staked | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | — | plain | hopoch |
| yield_cycle | Rare | Staked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 23.82 | status | in | — |
| bulwark_blow | Epic | Block | DamagingMelee | 3 | 50 | 90 | 1-1 | 0 | 43.45 | 0.00 | 43.45 | 14.48 | stat-override | in | blockwyrm |
| dust_fees | Epic | Block | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy -1; tempo Evasion -1 | in | — |
| rallying_slam | Epic | Block | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | utility | in | — |
| bot_swarm | Epic | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 95.28 | 95.28 | 47.64 | status | in | — |
| fox_feint | Epic | Bot | StatusUtility | 1 | 0 | 0 | 1-1 | 999 | 0.00 | 0.00 | 0.00 | 0.00 | utility | in | kitsauto |
| flame_charge | Epic | Burned | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | utility, conditional | in | — |
| sear_field | Epic | Burned | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 23.82 | status | in | — |
| phantom_hack | Epic | Encrypted | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 22.63 | 70.27 | 35.14 | stage; tempo Speed -1 100% | in | — |
| verification_lock | Epic | Encrypted | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 61.93 | 61.93 | 61.93 | status, utility; foe surcharge +1 | in | — |
| charged_relay | Epic | Forked | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 6.20 | 6.20 | 3.10 | utility, heal; GainApIfDistinctTargetsHitAtLeast; heal 5% | in | — |
| pivot_line | Epic | Forked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage, utility; tempo Speed +1 100%; tempo Accuracy +1; tempo Evasion +1 | in | — |
| gas_cloud | Epic | Gas | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; tempo Accuracy -1; tempo Evasion -1 | in | — |
| transaction_tax | Epic | Gas | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 38.11 | 38.11 | 38.11 | utility | in | deghoul |
| bug_payoff | Epic | Glitched | DamagingMelee | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | conditional | in | — |
| glitch_feedback | Epic | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 21.44 | 21.44 | 21.44 | utility; foe surcharge +1 | in | — |
| majority_pressure | Epic | Mined | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | — |
| momentum_drill | Epic | Mined | DamagingMelee | 2 | 54 | 95 | 1-1 | 0 | 49.42 | 0.00 | 49.42 | 24.71 | conditional | in | — |
| extend_protocol | Epic | Node | StatusUtility | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 0.00 | 0.00 | 0.00 | utility; ExtendBuff | in | — |
| network_response | Epic | Node | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility | in | — |
| network_sync | Epic | Node | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; RaiseLowestStage | in | — |
| ap_boost | Epic | None | StatusUtility | 0 | 0 | 0 | 1-1 | 3 | 0.00 | 47.64 | 47.64 | — | utility | in | — |
| desperation_surge | Epic | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 71.46 | 71.46 | — | stage, utility, trigger | in | — |
| momentum_shift | Epic | None | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | stage | in | — |
| turn_start_ap_gain | Epic | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger, id | in | — |
| crystal_bash | Epic | Shard | DamagingMelee | 2 | 44 | 95 | 1-1 | 0 | 40.51 | 0.00 | 40.51 | 20.25 | conditional, stat-override | in | — |
| shard_partition | Epic | Shard | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | status | in | crackett |
| bean_sip | Epic | Staked | StatusUtility | 1 | 0 | 0 | 1-1 | 2 | 0.00 | 33.74 | 33.74 | 33.74 | stage, utility, heal, priority; tempo Speed +1 100%; heal 8%; priority | in | cofi_lootwak |
| compound_returns | Epic | Staked | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 60.04 | 60.04 | 30.02 | stage, heal; heal 10% | in | — |
| interest_strike | Epic | Staked | DamagingMelee | 2 | 18 | 90 | 2-3 | 0 | 39.82 | 0.00 | 39.82 | 19.91 | utility | in | — |
| stake_refund | Epic | Staked | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | — | utility, trigger | in | — |
| immutable_ledger | Legendary | Block | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 33.35 | 33.35 | — | trigger, id; id 20% first hit +1 AP | in | blockwyrm |
| thread_lock | Legendary | Bot | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 7.15 | status; Thread Lock denies next +2 regen | in | — |
| ember_wall | Legendary | Burned | Support | 2 | 0 | 0 | 1-1 | 2 | 0.00 | 38.11 | 38.11 | 19.06 | utility, priority; block 50% x1; priority | in | cinislug |
| finish_burn | Legendary | Burned | DamagingRanged | 3 | 60 | 95 | 1-1 | 0 | 54.92 | 0.00 | 54.92 | 18.31 | conditional | in | — |
| streamlined_flow | Legendary | Compressed | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility, trigger; AP discount 1 | in | humpacked |
| memory_leak | Legendary | Encrypted | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 14.29 | 14.29 | 7.15 | stage, utility; tempo Speed +2 100%; armed AP -2 | in | keyper |
| conductive_counter | Legendary | Forked | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 30.97 | 30.97 | — | trigger, id; id counter 15% +1 AP | in | — |
| fork_split | Legendary | Forked | DamagingRanged | 4 | 56 | 95 | 1-1 | 0 | 102.56 | 0.00 | 102.56 | 25.64 | — | plain | forkrook |
| cryo_ray | Legendary | Frozen | DamagingRanged | 4 | 80 | 95 | 1-1 | 0 | 73.11 | 0.00 | 73.11 | 18.28 | stage; tempo Speed -2 25% | in | gilarime |
| gecko_guard | Legendary | Frozen | Passive | 0 | 0 | 0 | 1-1 | 999 | 0.00 | 47.64 | 47.64 | — | stage, trigger; tempo Speed +1 100% | in | — |
| combustive_shift | Legendary | Gas | DamagingRanged | 2 | 22 | 95 | 2-2 | 0 | 40.99 | 0.00 | 40.99 | 20.50 | — | plain | — |
| crash_routine | Legendary | Glitched | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 23.82 | 23.82 | 11.91 | stage | in | — |
| hype_train | Legendary | Meme | DamagingRanged | 2 | 52 | 95 | 1-1 | 0 | 47.64 | 0.00 | 47.64 | 23.82 | — | plain | — |
| deep_reap | Legendary | Mined | DamagingRanged | 2 | 58 | 95 | 1-1 | 0 | 53.10 | 0.00 | 53.10 | 26.55 | utility | in | hopoch,minefolk |
| hammer_sync | Legendary | Node | DamagingMelee | 4 | 80 | 95 | 1-1 | 0 | 73.11 | 0.00 | 73.11 | 18.28 | conditional | in | validape |
| overflow_reboot | Legendary | Node | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 12.40 | 12.40 | 6.20 | utility, heal; heal 10% | in | — |
| defensive_reflex | Legendary | None | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 33.35 | 33.35 | — | utility, trigger, id | in | — |
| field_split | Legendary | Shard | StatusUtility | 2 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | 0.00 | utility; FieldEntryStageChange | in | marrowhead |
| shard_scatter | Legendary | Shard | DamagingRanged | 4 | 22 | 95 | 2-3 | 0 | 102.48 | 0.00 | 102.48 | 25.62 | — | plain | — |
| ap_reserve | Legendary | Staked | StatusUtility | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 0.00 | 0.00 | — | utility; BankAPUntilNextTurn | in | rattlestake |
| yield_spike | Legendary | Staked | DamagingMelee | 2 | 56 | 95 | 1-1 | 0 | 51.28 | 0.00 | 51.28 | 25.64 | — | plain | hopoch |
| risk_spike | Mythic | Forked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 35.73 | 35.73 | 35.73 | status, stage; Surging +25% next hit | in | — |
| voltage_rush | Mythic | Forked | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | -5.96 | -5.96 | -5.96 | stage, utility; tempo Speed +3 100%; self surcharge +1 | in | — |
| fuel_surge | Mythic | Gas | StatusUtility | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 71.46 | 71.46 | — | status, utility | in | balloom |
| chaos_strike | Mythic | Glitched | DamagingMelee | 2 | 58 | 95 | 1-1 | 0 | 59.73 | 0.00 | 59.73 | 29.87 | random | in | kreechank |
| data_scramble | Mythic | Glitched | StatusUtility | 1 | 0 | 0 | 1-1 | 0 | 0.00 | 47.64 | 47.64 | 47.64 | stage, utility; tempo Speed -1 100%; tempo Speed -1 | in | — |
| glitch_barrage | Mythic | Glitched | DamagingRanged | 3 | 18 | 95 | 2-4 | 0 | 50.43 | 6.79 | 57.22 | 19.07 | utility; random stage | in | — |
| double_or_nothing | Mythic | Meme | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 8.58 | 8.58 | — | trigger, id; id 60/40 AP | in | — |
| jackpot_shot | Mythic | Meme | DamagingRanged | 2 | 58 | 95 | 1-1 | 0 | 61.97 | 0.00 | 61.97 | 30.99 | random | in | — |
| meme_momentum | Mythic | Meme | Passive | 0 | 0 | 0 | 1-1 | 0 | 0.00 | 88.61 | 88.61 | — | trigger, id; id meme momentum | in | — |
<!-- TABLE_END -->
