# Unimplemented move effects

Date: 2026-10-16. This list is for a one-by-one decision. Nothing in it was implemented or rewritten in the follow-up, except the Ice Wall comma, which is an import bug and is called out below.

Each row is one described effect that does not run. Options are always the same three:

- **Change** — rewrite the tooltip so it matches what the battle already does.
- **Remove** — take the move, clause, or status out of the catalog so it cannot be offered.
- **Implement** — add the missing behavior later. This pass did not do that.

Rarity and tooltip text are the current `Moves.csv` / `Statuses.csv` rows. "Currently" is what the resolver does with that row today.

## Empty Accuracy and Evasion moves

Accuracy and Evasion are `CombatStageStat`. The move stage column only accepts `CoreStat`, so these rows cannot express the named change. `ChangeStage` already accepts Accuracy and Evasion.

| Move | Rarity | Tooltip | Currently | Options |
|---|---|---|---|---|
| `accuracy_focus` | Common | Raise own Acc +2 for risky or evasive targets. | Spends 1 AP. No stage, status, or utility. | Change: say it does nothing. Remove the move. Implement `ChangeStage` Accuracy +2 on self. |
| `evasion_maneuver` | Common | Raise own Eva +2 to dodge incoming attacks. | Spends 1 AP. Empty payload. | Change, remove, or implement Eva +2 on self. |
| `accuracy_disrupt` | Common | Lower target Acc by 1 to force misses. | Spends 1 AP. Empty payload. | Change, remove, or implement Acc -1 on the target. |
| `evasion_pin` | Common | Lower target Eva by 1 so allies can land hits. | Spends 1 AP. Empty payload. | Change, remove, or implement Eva -1 on the target. |
| `gas_cloud` | Common | All foes lose Acc -1 and Eva -1. | Spends 2 AP. Empty payload. Targeting is AllEnemies, so a future opcode can fan out. | Change, remove, or implement both drops on each foe. |
| `dust_fees` | Common | Lower target Acc and Eva by 1. | Spends 1 AP. Empty payload. | Change, remove, or implement both drops. |
| `optimize_aim` | Common | Gain Acc +2. | Spends 1 AP. Empty payload. | Change, remove, or implement Acc +2 on self. |

## Accuracy or Evasion is only half of the card

| Move | Rarity | Tooltip | Currently | Options |
|---|---|---|---|---|
| `chain_seal` | Common | Lower target Speed and Eva by 1. | Speed -1 is a real stage. Eva is not. | Change: drop the Eva sentence. Remove the move. Implement Eva -1 beside the Speed drop. |
| `key_rotation` | Common | Gain Def +1 and Eva +1. | Def +1 is real. Eva is not. | Change: "Gain Def +1." Remove. Implement Eva +1. |
| `zip_step` | Common | Gain Eva +2 but lose Def -1. | Only Def -1 runs, so the card's downside is the whole move. | Change: "Lose Def -1." Remove. Implement Eva +2 and keep the Def drop. |
| `pivot_line` | Common | Gain Acc +1; Eva +1; Speed +1. | Only Speed +1 runs. | Change: "Gain Speed +1." Remove. Implement Acc +1 and Eva +1. |
| `precision_strike` | Common | Accurate ranged hit with +20% accuracy. | Power 64, accuracy 95, 2 AP. No extra accuracy. | Change: drop "+20% accuracy." Remove. Implement a +20 accuracy bonus on the hit. |
| `shard_scatter` | Uncommon | 2-3 AoE hits; each lowers Eva on a different foe. | Power 18, hits 2–3, AllEnemies. No Eva drop. | Change: drop the Eva sentence. Remove. Implement an Eva drop per hit, on a foe that has not already been dropped this action. |

## Whole card does not run

The recharge on Extend Protocol does run. Its extend clause does not. The player-facing recharge sentence was updated this round ("Skips the next 2 turns"). `rechargeTurns` stays 3.

| Move | Rarity | Tooltip | Currently | Options |
|---|---|---|---|---|
| `ap_drain` | Common | Cut target AP by 1; 25% chance to steal it. | Spends 1 AP. No drain and no steal. | Change: say it does nothing. Remove. Implement drain 1, then a 25% steal. |
| `transaction_tax` | Common | Make target lose 1 AP; gain it if they had more AP. | Spends 1 AP. Empty payload. | Change, remove, or implement the conditional steal. |
| `extend_protocol` | Common | Extend one ally buff by 1 turn. Skips the next 2 turns. | Recharge is real and the log says 2 turns. No buff is extended. | Change: "Skips the next 2 turns." Remove. Implement a +1 turn extend on one ally buff, and keep the recharge. |
| `blockade` | Common | Give ally 25% mitigation for 2 turns. | Spends 3 AP. No mitigation window. | Change, remove, or implement `MitigationWindow` at 25% for 2 turns. A hit budget of 0 would still leave it inactive (see dead walls). |
| `script_loop` | Common | If target has 2+ debuffs extend all by 1 turn. | Spends 1 AP. Empty payload. | Change, remove, or implement the debuff-duration extend. |
| `network_sync` | Common | All allies raise their lowest stage stat by +1. | Spends 2 AP. Empty payload. | Change, remove, or implement the lowest-stage bump on each ally. |
| `system_reset` | Common | Strip all target buffs and debuffs; skip your next action. | Spends 2 AP. Empty payload. | Change, remove, or implement the strip and the self-skip. |
| `status_resistance` | Common | Passive - General debuffs expire 1 turn faster. | Passive with no body. | Change, remove, or implement a faster General-debuff tick. |
| `isolation_tool` | Common | Force target next move to be single-target. | Spends 1 AP. Empty payload. Isolated, the chemistry status, is a different effect. | Change, remove, or implement a one-move single-target lock. |
| `viral_surge` | Common | Randomly gain +2 AP or lose 1 AP; always gain +1 random stat. | Spends 1 AP. Empty payload. | Change, remove, or implement the AP coin flip and the random stage. |
| `wildcard_swap` | Common | Randomly swap one of your stage changes with target. | Spends 1 AP. Empty payload. | Change, remove, or implement one stage swap. |
| `hype_burst` | Common | Remove up to 2 target buffs; gain Atk +1 for each. | Spends 2 AP. Empty payload. | Change, remove, or implement the buff strip and the Atk gain. |
| `extraction_hit` | Common | Passive - hits can raise Atk; at Atk +3 also gain Speed. | Passive with no body. | Change, remove, or implement the on-hit Atk ramp and the Speed bonus at +3. |

## A real hit, with a cost or rider that does not run

| Move | Rarity | Tooltip | Currently | Options |
|---|---|---|---|---|
| `all_in` | Common | Spend all AP for huge contact hit; recoil on hit or miss; +1 AP on KO. | Power 98, AP cost 0, accuracy 95. No spend, no recoil, no KO refund. Spec §1.3 still says it consumes all AP. | Change: a free 98-power hit. Remove. Implement the spend, the recoil, and the KO refund. This is the largest damage index in the file because the cost is missing. |
| `recoil_strike` | Common | 76-power contact hit; take 15% MaxHP recoil. | Power 76, AP 2. No recoil. | Change: drop the recoil sentence. Remove. Implement 15% MaxHP recoil. |
| `volatile_shot` | Common | High-power ranged hit; miss costs you 15% MaxHP. | Power 72, AP 2. A miss has no HP cost. | Change: drop the miss cost. Remove. Implement 15% MaxHP on a miss. |
| `jackpot_shot` | Common | Ranged hit rolls for double; normal; or half damage. | Power 64, AP 2. Damage is the normal roll only. | Change: a normal 64-power hit. Remove. Implement the three-way multiplier. |
| `hype_train` | Rare | Consecutive hits gain +20% damage; cap +100%; miss resets. | Power 52, AP 2. No streak. | Change: a 52-power hit. Remove, or drop the Rare until the streak exists. Implement the streak. |
| `lucky_scatter` | Rare | 1-4 hits; each has 25% chance to grant a random buff. | Power 18, hits 1–4, AP 2. No buff. | Change: drop the buff sentence. Remove or drop the rarity. Implement the per-hit buff. |
| `crystal_bash` | Rare | Damage scales from your Def; doubles below 30% target HP. | Power 0, AP 2. The index is 0. Def is not the attack stat. | Change: say it deals no damage. Remove. Implement Def as the attack stat and the low-HP double. |
| `glitch_barrage` | Rare | 2-4 hits; each randomly hits target or self. | Power 16, hits 2–4, AP 2. Every hit uses the normal target. | Change: a normal multi-hit. Remove or drop the rarity. Implement the random self hit. |
| `cipher_shot` | Common | Ranged hit with 25% chance to block target next buff. | Power 64, AP 2. No buff block. | Change: drop the buff-block sentence. Remove. Implement the buff block. |
| `cipher_burst` | Rare | 2-3 hits; each has 25% chance to erase one target buff. | Power 22, hits 2–3, AP 3. No erase. | Change: drop the erase sentence. Remove or drop the rarity. Implement the erase. |
| `ap_siphon` | Common | Ranged hit with 50% chance to drain 1 AP. | Power 56, AP 3. No drain. | Change: drop the drain sentence. Remove. Implement the 50% drain. |
| `deep_dig` | Common | Heavy contact hit that ignores 50% of target Def stages. | Power 72, AP 3. Defense stages apply in full. | Change: drop the ignore sentence. Remove. Implement 50% Def-stage ignore. |
| `momentum_drill` | Common | Contact hit deals +25% damage to debuffed targets. | Power 64, AP 2. No debuff bonus. | Change: drop the bonus. Remove. Implement +25% versus a debuffed target. |
| `ore_strike` | Common | Contact hit gains +10% damage per own buff; cap +30%. | Power 64, AP 2. No buff scaling. | Change: drop the scaling. Remove. Implement it. |
| `stake_strike` | Common | Contact hit gains +10% damage per own Def stage; cap +30%. | Power 64, AP 2. No Def-stage scaling. | Change: drop the scaling. Remove. Implement it. |
| `yield_spike` | Uncommon | At 4+ AP deal +25% damage then spend 1 AP. | Power 64, AP 3. No bonus and no extra spend. | Change: drop the AP clause. Remove. Implement +25% and the extra AP spend at 4+ AP. |
| `interest_strike` | Rare | 2-3 hits; each can grant AP if Def stage exceeds Atk. | Power 18, hits 2–3, AP 3. No AP grant. | Change: drop the AP sentence. Remove or drop the rarity. Implement the per-hit AP check. |
| `rallying_slam` | Rare | Contact hit that grants +1 AP if Def stage exceeds Atk. | Power 64, AP 2. No AP grant. | Change: drop the AP sentence. Remove or drop the rarity. Implement the check. |
| `pickaxe_flurry` | Uncommon | 2-4 hits; each can raise Atk; cap +2. | Power 16, hits 2–4, AP 2. No Atk gain. | Change: drop the Atk sentence. Remove. Implement the capped Atk gain. |
| `combustive_shift` | Rare | 2 hits; bonus damage vs debuffed targets missing AP. | Power 22, hits 2, AP 3. No bonus. | Change: drop the bonus. Remove or drop the rarity. Implement it. |
| `crash_routine` | Common | Below 50% target HP deal 15% MaxHP damage; otherwise lower Atk. | Always applies Atk -1. The HP clause does not run. | Change: "Lower Atk -1." Remove. Implement the branch. |
| `glitch_feedback` | Common | Remove 1 target debuff; their next move costs +1 AP. | The +1 AP surcharge runs. The cleanse does not. | Change: drop the cleanse sentence. Remove. Implement the cleanse and keep the surcharge. |
| `bug_fix` | Uncommon | Heal 10%; cleanse 1 debuff; deal -20% damage next turn. | Heal 10% runs. No cleanse and no damage penalty. | Change: "Heal 10%." Remove. Implement the other two clauses. |
| `dice_roll` | Common | Random support can heal ally; debuff foe; or give ally +2 AP. | Heal 20% runs. The random branch does not. | Change: "Heal 20%." Remove. Implement the three-way roll. The 20% heal and the tooltip's "heal ally" also disagree on the number. |
| `recursive_backup` | Rare | Below 50% HP heal 20% at turn end and reset durations. | The immediate 20% heal under 50% HP runs (`EffectOnly`). There is no end-of-turn heal and no duration reset. | Change: "Below 50% HP, heal 20% now." Remove. Implement the end-of-turn heal and the reset. |
| `data_scramble` | Rare | Both lose Atk and Speed -1; you gain +2 AP. | Self Atk -1, self Speed -1, and +2 AP run. The target's stages do not. | Change: "Lose Atk -1 and Speed -1; gain +2 AP." Remove. Implement the same drops on the target. |
| `risk_spike` | Common | Gain Atk +2 and Def -1; next hit +25% damage and +10% recoil. | Atk +2 and Def -1 run. The next-hit clause does not. | Change: drop the next-hit sentence. Remove. Implement it. |
| `guard_stance` | Common | Gain Def +2; melee contact takes +10% recoil for 3 turns. | Def +2 runs and stays (it is a move stage, not a timed status). The recoil window does not. | Change: "Gain Def +2." Remove. Implement a 3-turn contact-recoil window. The Def stage is permanent today. |
| `rally_cry` | Common | All allies gain Speed +1; user gains +1 AP at turn end. | Allies' Speed +1 runs. The end-of-turn AP does not. | Change: drop the AP sentence. Remove. Implement the end-of-turn AP. |
| `refinery` | Common | Gain Def +1 and +1 AP at turn end for 2 turns. | Def +1 runs and stays. The AP-over-time does not. | Change: "Gain Def +1." Remove. Implement +1 AP at end of turn for 2 turns. |
| `double_or_nothing` | Common | Passive - end turn 60% +1 AP; 40% -1 AP and AP-drain immunity. | The 60/40 AP roll runs. The 40% branch does not grant drain immunity. | Change: drop the immunity sentence. Remove. Implement drain immunity on the 40% branch. |

## Dead walls

`IsMitigationSourceActive` treats `hitsRemaining == 0` as inactive. Unlimited hits would be a negative budget. `FullAbsorbBarrier` rewrites 0 into 3. `MitigationWindow` does not. These windows are authored and then do not mitigate.

| Move | Rarity | Tooltip | Currently | Options |
|---|---|---|---|---|
| `ash_guard` | Common | Give an ally 20% mitigation for 2 turns. | `MitigationWindow` magnitude 0.2, turnBudget 2, hitBudget 0. The window does not mitigate. | Change: say it grants no mitigation. Remove. Implement by choosing a hit budget (a positive count, or negative for unlimited). This pass did not invent that number. |
| `compound_guard` | Common | Give an ally 20% mitigation for 2 turns and Def +1. | Same dead window as Ash Guard. Def +1 is a real stage and stays. | Change: "Gain Def +1." Remove. Implement a real hit budget and keep the Def stage. |
| `ice_wall` | Common | Give an ally 25% mitigation for the rest of this turn. If you are faster, gain +1 AP when hit. | The description comma was unquoted, so the import shifted SelectionPolicy and the utility opcodes. That comma is now quoted, and the asset has both opcodes: `MitigationWindow` 25%, turnBudget 1, hitBudget 0, stack key `wall_ice`, and `GainApOnDamageIfSpeedHigher` +1 AP, turnBudget 1, stack key `wall_ice_ap`. The speed AP window does not use the mitigation hit check, so that clause can run. Mitigation stays inactive because the restored hit budget is 0. | Change: "If you are faster, gain +1 AP when hit." Remove. Implement by choosing a hit budget. Do not treat 0 as "unlimited." |
| `blockade` | Common | Give ally 25% mitigation for 2 turns. | Listed above. No opcode at all, so it is empty rather than a zero hit budget. | Same three options. If implemented, it still needs a non-zero hit budget. |

## Status text the move applies, and the status body does not match

| Move | Rarity | Tooltip | Currently | Options |
|---|---|---|---|---|
| `mev_extract` | Common | Apply MEV Extracted; steal 1 AP per turn for 2 turns. | Applies `MEVExtracted` for 2 turns. The catalog AP change is -1, and `CombatApService.Drain` spends that AP. It does not steal. | Change: "drain 1 AP per turn for 2 turns." Remove the move or the status. Implement a steal of 1 AP per turn. |
| `deflation_mark` | Common | Apply Burned Tokens so enemy DoT heals you; lower Atk -1. | Applies `BurnedTokens` and Atk -1. Burned Tokens has no resolver body, so the DoT-heal does not run. The Atk drop does, and it stays. | Change: "Lower Atk -1." Remove. Implement the DoT-to-heal conversion on Burned Tokens. |
| `voltage_rush` | Common | Gain Speed +3; 25% chance to self-Overleverage. | Speed +3 runs. Overleveraged can be applied. The status has no recoil body (see below). | Change: "Gain Speed +3." Remove. Implement Overleveraged, then this 25% apply starts to matter. |
| `risk_marker` | Common | Apply Overleveraged so heavy hits make target take recoil. | Applies Overleveraged. No recoil is dealt. | Change: say the status has no effect. Remove. Implement the recoil. |
| `streamlined_flow` | Rare | Passive - above 70% HP moves cost -1 AP but Def falls -1. | The AP discount above 70% HP runs. Def is dropped twice: the passive stage column and a `ChangeStage` opcode both apply Def -1, and the stage cap on the opcode is -6. | Change: "Above 70% HP, moves cost -1 AP and Def falls -1" and delete one of the two drops. Remove. Implement a single Def -1. This pass did not pick which write to keep. |
| `compression_burst` | Common | 2 contact hits; first hit lowers target Def. | Power 22, hits 2. `ApplyMoveStageChanges` runs inside the per-hit loop, so both hits drop Def. | Change: "each hit lowers Def." Remove. Implement a first-hit-only gate. |
| `charged_relay` | Rare | Heal 5%; if you hit 2+ targets this turn gain +2 AP. | The heal runs. The +2 AP reads the hit ledger when the utility move resolves, so a turn whose only action is Charged Relay has not recorded those hits yet. | Change: "if you already hit 2+ targets this turn." Remove. Implement an end-of-turn check if later hits should count. |

## Chemistry outputs whose card does not match the body

These are live matrix cells. The status is applied. The described extra is not.

| Status | Where | Tooltip | Currently | Options |
|---|---|---|---|---|
| Jammed | 8 v1 cells, foe | Accuracy down (Acc -2) | Duration 2. No Accuracy stage. The stage column cannot store Accuracy. | Change: a 2-turn token with no stat. Remove the cells. Implement Acc -2 with `ChangeStage` or a chemistry-owned combat stage, and revert it with the status. |
| Leaked | 2 v1 cells, foe | On action: 1/10 HP to all | `EoTDamagePercent` 0.1, so it is a 10% MaxHP self damage-over-time at end of turn. It does not hit the field on action. | Change: "10% MaxHP damage at end of turn." Remove the cells. Implement the on-action field chip and stop the self DoT, or keep both on purpose. |
| Backlash (`BacklashType`) | 2 v1 cells, foe | Contact adds typed DoT | Duration 3, parameterized. No contact damage-over-time body. | Change: a token with no damage. Remove the cells. Implement a typed contact DoT. |
| Dusted | 4 v1 cells, foe | Eva -3 and Spd -3 | Speed -3 is apply-once and now reverts when Dusted expires (shared timed-stage rule). Eva -3 is not applied. Refresh does not stack the Speed drop. | Change: "Speed -3 while Dusted." Remove the cells. Implement Eva -3 and revert it with the status. |
| MEV Extracted | `mev_extract` | Steal 1 AP per turn | Drain, not steal. See `mev_extract`. | Same options as that move. |
| Burned Tokens | `deflation_mark` | Enemy DoT heals user | No resolver body. | Same options as `deflation_mark`. |
| Overleveraged | `voltage_rush`, `risk_marker` | Next heavy hit causes recoil | Consumed, duration 2, no recoil body. Spec §7.5 says 25% of damage dealt. | Change: say it does nothing. Remove the status and both moves' clauses. Implement 25% recoil on the next heavy hit, then consume it. The catalog duration on a Consumed status still needs a call: next hit, or two turns. |
| HODLed | No producer | Immune to stat drops; Def ramps +1/turn | No move and no v1 cell applies it. There is no stat-drop immunity. The repeating Def +1 and a second +1 while Def is below +3 both run if something applies it, so the first end tick can reach +2. Both writes are now owned and revert together when the status expires. The ramp formula was not retuned. | Change: describe the double ramp and the expiry revert, and drop the immunity sentence. Remove the catalog row. Implement one ramp capped at +3, plus immunity, and a producer. Producer history is in [CATALOG_STATUSES_WITHOUT_PRODUCERS.md](CATALOG_STATUSES_WITHOUT_PRODUCERS.md). |

## Not in this list

These were checked and left alone because the described clause already runs, or because another decision owns them:

- Timed stages that now revert with the status: Gas Spike (`fuel_surge`), Shard Partition, FUD (`verification_lock`), Sear Field, Immovable's Def +2. See the correctness audit decision round.
- Working riders: `coordinated_shot`, `hammer_sync`, `chaotic_resonance`, `bug_payoff`, `seam_cut`, `finish_burn`, `flame_charge`, `chaos_strike`, `bulwark_blow`, `cryo_ray`, `deep_reap`, `automation_burst`, `ember_shower`.
- Hardcoded move ids that already have a body: `position_swap`, `ap_donation`, `ap_redistribution`, `ap_router`, `damage_intercept`, `signal_redirect`, `effect_relay`, `relay_packet`, `turn_start_ap_gain`, `shattered_armor`, `on_hit_momentum`, `on_crit_bonus`, `precision_focus`, `reactive_ping`, `conductive_counter`, `double_or_nothing` (AP roll only), `meme_momentum`, `patience_passive`, `spite_counter`, `immutable_ledger`, `defensive_reflex`, `firewall_passive`, `fee_collector`, `fox_feint`, `gecko_guard`.
- Stamp modifiers Side Quest (`3.2-b`), After Hours (`3.2-c`), Setup Feed (`7.2-c`), All-Stat Burn (`10.1-d`), Anniversary Mark (`10.2-b`), and Tournament Badge (`10.2-c`). Another agent is handling them. This pass did not edit those sections.
