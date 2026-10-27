# Combat issues decision brief

PROPOSALS only. Nothing here is a code, CSV, or catalog edit. The source is the local snapshot `chainbound_combat_snapshot_20261018_2005` (18 Oct 2026, 20:05). Where an older note in [Design/](.) disagrees with that snapshot, the snapshot wins. Links below are relative to `Docs/Combat/Design/`.

The master header still says 68 awaiting decision and 39 implemented-unverified against 115 rows. The table itself is newer: 64 awaiting decision, 1 open, 39 implemented-unverified, 7 fixed in PR, and 7 marked `fixed in follow-up v1`. This brief decides the 65 awaiting and open rows. `issues_brief_check.py` rechecks the snapshot facts cited below.

Saved rules are not reopened. In particular: Jammed is Accuracy −1; self-damage stays shelved; Overleveraged does not come back as HP recoil; Node's passive is Uplink; Burned's passive is Kindling at 6%; boss fights do not draw; boss-only moves stay out of pools and candy; stamp modifiers `3.2-b`, `3.2-c`, `7.2-c`, `10.1-d`, `10.2-b`, and `10.2-c` stay untouched; `Docs/GameTheory/` stays untouched.

## Summary

| Recommendation | Rows | What Cronus is approving |
|---|---:|---|
| Close | 20 | The defect text is stale. The snapshot already does the saved behavior, or the promised rider was deleted from the card on purpose. No new work. |
| Change | 7 | A small edit so the card, comment, or status body matches what should run. |
| Implement | 18 | Build the sentence that is still on the card, with the values in this brief. |
| Remove | 4 | Retire the row. Leave the enum id so old assets still load. |
| Defer | 16 | Do not build it this round. |
| **Decision set** | **65** | 64 awaiting + E-23 (open). |

**Stale rows (Close).** The master still describes these as missing. The 20:05 tree already has the payload, or the card text no longer promises it.

| ID | What the snapshot does now |
|---|---|
| MV-precision_strike | Power 34, accuracy 100, `AccuracyTuning: SureHit`. The asset ignores 6 Evasion stages and floors the hit at 95%. That is the saved near-guaranteed hit. |
| MV-cipher_shot | Power 40, Uncommon, 2 AP. A landed hit applies `CipherLocked` for 2 turns at 100%, once per target. `BuffGate` blocks new buffs and positive stages. Swap-out clears it and logs "Cipher Lock fell away". |
| MV-jackpot_shot | Mythic, power 58, 2 AP. `randomMultipliers` 2 / 1 / 0.5, and `CombatResolver` rolls them. |
| MV-lucky_scatter | Common, power 17, 3 AP, 1–4 hits. `RandomStageChange` +1 at 25%, after each confirmed hit. |
| MV-glitch_barrage | Now Bit Rot. Mythic, power 18, 3 AP, 2–4 hits. 25% to drop a random foe stat. The self-hit is shelved in [SELF_DAMAGE_SHELF.md](SELF_DAMAGE_SHELF.md). |
| MV-risk_spike | Now Leverage Up. Atk +2, Def −1, and Surging (+25% on the next move). The +10% recoil is shelved. |
| MV-guard_stance | Description is only "Gain Def +2." The contact-recoil sentence is gone. |
| MV-cipher_burst | Common, power 17, 3 AP, 2–3 hits. Description is "2-3 ranged hits." The buff-erase sentence is gone. Cipher Shot is the buff block. |
| MV-deep_dig | Uncommon, power 72, 4 AP. Description is "Heavy contact hit." The Def-ignore sentence is gone. |
| MV-shard_scatter | Legendary, power 22, 4 AP, 2–3 hits, all foes. Description is "2-3 hits against all foes." The per-hit Evasion drop is gone. |
| MV-ore_strike, MV-stake_strike | Both Rare, power 52, 2 AP, description "Contact hit." The +10% scaling sentences are gone. |
| MV-yield_spike | Legendary, power 56, 2 AP, description "Strong contact hit." The 4 AP bonus and extra spend are gone. |
| MV-hype_train | Legendary, power 52, 2 AP, description "Heavy ranged hit." The consecutive ramp sentence is gone. |
| MV-combustive_shift | Legendary, power 22, 2 AP, two hits, description "Two ranged hits." The missing-AP bonus sentence is gone. |
| DEC-BEAM | `grind_tax` is boss-only, 6 AP, power 30, 3 hits, all foes, Speed −1 once (`moveEffectsOncePerTarget`). Per-target power is 7.5× Bean Beam (30×3 vs 12×1), above the 2× floor. Options A/B/C in [COMBAT_DESIGN_IDEAS.md](COMBAT_DESIGN_IDEAS.md) are stale. |
| DEC-PACE | The drag the row names is Bean Beam. Grind Tax is the payoff. What remains is the AI still sipping until it can already pay 6. That is the AI question in Batch E, not a new move. |
| BAL-RARE | Impact Knuckle, Vector Shot, and Rupture Point are Uncommon now (indexes 37.8, 34.2, 30.6), not Rare, and the power-64 spine they were compared with is gone. Typed Commons such as Gas Bump sit at 34.2. |
| BAL-SPREAD | Pellet Storm is Uncommon at 47.5 and Scatterburst is Common at 39.6, so the old Common-above-Rare pair is gone. Fork Split is Legendary at 106.4 (4 AP). Ricochet Storm is Uncommon at 54. Kinetic Burst is Common at 50.4. |
| BAL-EMPTY | The five ids are no longer "Rare on the card, Common in the resolver." Hype Train is Legendary, Bit Rot is Mythic, Interest Strike is Epic, Lucky Scatter and Cipher Burst are Common. Interest Strike's rider runs. |

Also already true, and not in the decision set because the master already marks them `fixed in follow-up v1`: E-30 Cipher Lock, E-31 boss KO order, E-32 boss-only exclusion, All-In (`SpendRemainingAp`, power 98, 4 AP, recharge 2, no recoil), Hard Fork (cleanse, no HP recoil), Gas War (next move +1 AP, no HP loss), and BAL-COST. Balloom's pool is `gas_bump` (Common, power 36, 2 AP), Gas War, Hard Fork, Fuel Surge, and Gas Cloud. All-In is not in that pool, so the Turn-1 attack rule is already met.

**Top 5**

1. **Close the 20 stale rows.** About a third of the backlog is text the 20:05 tree already replaced. Approving Batch B shrinks the list before anyone writes code.
2. **Jammed writes Accuracy −1 for 2 turns, and the stage ends with the status.** Eight chemistry cells currently apply a blank token. Dusted's owned combat-stage path already exists, so this is data. Use −1. [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md) Proposal A says −2; that note is from the 10:14 snapshot and loses to the saved rule.
3. **Walls use hit budget −1.** Ash Guard, Compound Guard, Ice Wall, and Blockade name a turn window and ship `hitBudget` 0, which `AddMitigationSource` treats as inactive. −1 is the duration-only sentinel the engine already uses for modifier mitigation. It does not invent a hit count.
4. **Isolation Tool applies Anchored.** It is in Crackett's pool and Marrowhead's pool, costs 1 AP, and does nothing. Anchored already forces the next damaging move to one target. Isolated is the wrong status.
5. **MEV steals, and Burned Tokens heals the Kindled tick only.** Both sentences are on live or test-loadout cards. The processor drains MEV and never reads Burned Tokens. These are the two status bodies [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md) Proposals H and I already specified, and the evening tree has not landed them.

**Batch E questions**

1. Leave DEC-S1 through DEC-S5, DEC-PACK, and DEC-CD unbuilt? **Yes.**
2. Approve only proposals 1 and 2 from [AI_DECISION_QUALITY_ANALYSIS.md](AI_DECISION_QUALITY_ANALYSIS.md) (write the target slot, and a pure damage estimate), and leave the rest of DEC-AI for later? **Yes.**
3. Author new cards this round for Glitched Mind, Unsealed, HODLed, or System Reset? **No.** Fix HODLed's body. Deprecate Rekt, Offline, and the Relay status. Leave the other three with no producer.
4. Give Overleveraged a new body now? **No.** Voltage Rush and Risk Marker no longer apply it. Leverage Up is the live high-risk card. Leave status 208 reserved until a self-damage patch.
5. Retune the riderless high-rarity hits (Hype Train, Yield Spike, Ore Strike, Stake Strike, Combustive Shift, Bulwark Blow) in this round? **No.** The evening ladder already moved costs. A second curve can wait until the smoke list is done.

## How to say yes

Each batch is one yes or no. A yes on Batch B is "mark these master rows implemented or fixed; do not rebuild the old sentence." A yes on Batch C or D is "build the values in that batch." A yes on Batch E is "take the recommended answer."

Work means the cost of the recommendation. For a Close, work is none. For a Remove or a text Change, work is data. Implement says data when an opcode, status stage, or condition the evening engine already runs is enough.

Older notes that must not be followed blindly:

- [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md) was checked against the 10:14 snapshot. Cipher Shot, Precision Strike, Hammer Sync's +20%, Interest Strike's strict Def-over-Atk gate, Bean Sip's +1 AP, and the Overleveraged detach had not landed yet. Proposals H, I, K, L, and the HODLed body change still match this tree. Proposal A (−2) and Proposal J (25% HP on the foe) do not.
- [MOVE_COST_CURVE_ANALYSIS.md](MOVE_COST_CURVE_ANALYSIS.md) and the spread tables in [BATTLE_MOVE_BALANCE_BY_RARITY.md](../Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md) still use the power-64 spine. Typed Commons in this snapshot are about power 26–36 at 2 AP.
- [AP_COST_LADDER_IMPLEMENTATION_AUDIT.md](../Audits/AP_COST_LADDER_IMPLEMENTATION_AUDIT.md) §8 describes a tree from before the evening retune (Fork Split at 5 AP, Hammer Sync at +10%). The assets now match the master line on BAL-COST.

## Batch A — labels and comments, zero gameplay

Yes means: edit text only. No resolver change, no new opcode, no power change.

| ID | Rec | Work | Do this | Why |
|---|---|---|---|---|
| E-06 | Change | data | FUD stays Atk −1, Def −1, Speed −1 for 2 turns, owned, reverted. Delete Accuracy from the code comment and from `StatusId.FUD`'s comment. | Accuracy is not a core stat, and the asset never had it. [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md) §3.2. |
| E-09 | Change | data | Backlash summary becomes "Token. No contact damage." Duration stays 3. No DoT, no matrix edit. | Kindling and Rugged already burn. A third percent is a new burn. Proposal L. |
| BAL-61 | Change | data | Rewrite `COMBAT_Moves.txt` §6.1 so rarity tracks power and availability, typed moves beat typeless, and a rare typeless move lines up with a typed common. | The balance screen already follows that rule. The spec sentence is the one that still disagrees. |
| MV-dice_roll | Change | data | Description becomes "Heal an ally for 20% MaxHP." Leave the 20% heal, the 1 AP cost, and Common. Drop the random heal / debuff / +2 AP sentence. | The heal is what runs. The three-way branch does not, and a Common does not need a new exclusive-roll opcode. Jackpot and Viral Surge carry Meme randomness. |
| MV-recursive_backup | Change | data | Description becomes "Below 50% HP, heal 20% now." Set `SelectionPolicy` to Gate so it cannot be picked at 50% or above. Drop the end-of-turn heal and the duration reset. | The 50% gate and the immediate 20% heal already run (`activeConditionType` SelfHPBelowPercent). Above half it can be selected and spend 2 AP on nothing, because the policy is EffectOnly. The reset was never built. |
| MV-status_resistance | Remove | data | Description becomes "Retired. No effect." Do not build a faster-debuff clock. It is in no pool. | A new duration rule for every General debuff is a large engine change for an ungranted Rare passive. Proposal question 14, answered as cut. |
| NP-rekt | Remove | data | Mark the catalog summary deprecated. Do not add a move. Leave the enum. | 25% action loss overlaps Glitched Mind and Offline, and no type profile names Rekt. Proposal B. |
| NP-offline | Remove | data | Same: deprecate the summary, no move, enum stays. | Frozen's lock is Frozen Assets, which has a move. Sleep has no profile home. Proposal B. |
| NP-relay-status | Remove | data | Deprecate the Relay **status** summary. Do not add a producer. This is not Uplink, not Effect Relay, and not Relay Packet. | Full barrier bypass already exists on modifiers `4.3-b` and `4.8-a`. Duration 1 is not a next-action status, so a producer would expire it on the granting end step. The name collides with three live systems. Proposal B. |

## Batch B — already in the snapshot, do not rebuild

Yes means: treat the master sentence as stale and move the row to `implemented, unverified` (or `fixed in PR` after the smoke pass). Do not implement the old rider, the old power, or the old rarity.

| ID | Rec | Evidence | Why leave it |
|---|---|---|---|
| MV-precision_strike | Close | `Move_precision_strike.asset` `ignoreEvasionStages: 6`, `minHitChance: 0.95`, accuracy 100, power 34. | This is the saved accuracy-raising effect. The old "+20 accuracy, power 64" sentence is gone. |
| MV-cipher_shot | Close | `StatusEffects CipherLocked:2:1.0:`, `moveEffectsOncePerTarget`, `BuffGate`, swap clear in `ResetChemistrySwapState`. | Blocks self and ally buffs, narration calls the block out, swap clears it. The 25% one-buff version is the old card. |
| MV-jackpot_shot | Close | Asset `randomMultipliers` 2, 1, 0.5. Resolver rolls them in the conditional-damage path. | The branch runs. Power is 58, not 64, after the ladder. |
| MV-lucky_scatter | Close | `RandomStageChange` +1 at 25%, utility timing AfterEachConfirmedHit. Common, not Rare. | The buff-per-hit runs. |
| MV-glitch_barrage | Close | Display name Bit Rot. Random stat drop, not a self-hit. | [SELF_DAMAGE_SHELF.md](SELF_DAMAGE_SHELF.md) replaced the self-hit. |
| MV-risk_spike | Close | Display name Leverage Up. `Surging:1` plus Atk +2 and Def −1. | The shelf's non-HP tradeoff. Surging is +25% on the next move. |
| MV-guard_stance | Close | Description "Gain Def +2." No recoil utility. | Shelf question Q5: delete the thorns clause. Thorns can KO an attacker on their own action, which is the mutual-wipe case. |
| MV-cipher_burst | Close | Description no longer mentions a buff erase. | Cipher Shot is the lock. Rebuilding a second erase makes two Encrypted buff-denies. |
| MV-deep_dig | Close | Description "Heavy contact hit." Power 72, 4 AP. | The ladder deleted the Def-ignore clause because it had no opcode. Putting it back is a new move, not a fix. |
| MV-shard_scatter | Close | Description has no Evasion clause. Power 22, 4 AP, all foes, 2–3 hits. Field index about 104.5. | A per-hit Evasion drop on a different foe needs a new retarget. The card no longer asks for it. |
| MV-ore_strike | Close | "Contact hit." Rare, 52, 2 AP. In Kodiax's pool. | The +10% per buff sentence was deleted. Pickaxe Flurry is the Atk ramp. |
| MV-stake_strike | Close | "Contact hit." Rare, 52, 2 AP. In Hopoch's pool. | The Def-scaling sentence was deleted. |
| MV-yield_spike | Close | "Strong contact hit." Legendary, 56, 2 AP. In Hopoch's pool. | The extra AP spend is the double tax [AP_ECONOMY_ANALYSIS.md](AP_ECONOMY_ANALYSIS.md) and the shelf both warn against. |
| MV-hype_train | Close | "Heavy ranged hit." Legendary, 52, 2 AP. | The ramp sentence is gone. Raising a 2 AP Legendary back toward the old 64 spine fights "high damage waits on an AP boost." |
| MV-combustive_shift | Close | "Two ranged hits." Legendary Gas, 22×2, 2 AP. | The missing-AP clause is gone from the card. |
| DEC-BEAM | Close | `grind_tax` tags `BOSS_ONLY` and `NO_CANDY`. 6 AP, 30×3, Speed −1 once. Bean Sip is 1 AP, +1 AP, recharge 2 (one skipped turn), heal 8%, Speed +1. | The boss move exists and clears the 2× bar. Espresso Shot, Double Bean, and Cream Top should not be built. |
| DEC-PACE | Close | Same Grind Tax. | The remaining sip loop is AI, covered by Batch E question 2. |
| BAL-RARE | Close | `refined_striker` / `refined_shot` / `refined_thrust` are Uncommon. | The named Rare-vs-power-64 comparison is not the current catalog. |
| BAL-SPREAD | Close | Rarities and indexes above. | The two inversions the row names are gone. |
| BAL-EMPTY | Close | Current rarities above. Interest Strike's AP gate is live and strict. | "Drop the rarity or implement the rider" assumed a mismatch that the ladder already resolved. |

Dependency: closing MV-cipher_shot is what lets a smoke battle prove E-30. Do not author a second buff-block status.

## Batch C — data payloads on opcodes that already run

Yes means: fill these cells. No new `UtilityEffectType`, no new status id.

| ID | Rec | Work | Values | Why |
|---|---|---|---|---|
| E-17 | Implement | data | Ash Guard `MitigationWindow:20%:2:-1`. Compound Guard `MitigationWindow:20%:2:-1` and keep Def +1. Ice Wall `MitigationWindow:25%:1:-1` and keep `GainApOnDamageIfSpeedHigher`. | `hitsRemaining == 0` returns immediately, so the cards mitigate nothing. `hitsRemaining < 0` is not decremented, so −1 lasts for the turn window the card already names. This does not pick a hit count. Ice Wall's +1 AP on a faster hit already runs. |
| MV-blockade | Implement | data | Uncommon, Block, 3 AP, ally, `MitigationWindow:25%:2:-1:blockade`. The master calls it Common; the asset is Uncommon. Keep Uncommon. | Same dead-window bug as E-17. Blockade is the 3 AP, 25% version of Ash Guard's 2 AP, 20%. It is in Blockwyrm's pool, so the blank click is live. Depends on accepting −1 in E-17. |
| E-20 | Change | data | On `streamlined_flow`, delete the `StageChanges` Def −1. Keep the `ChangeStage` Def −1 and the −1 AP discount, both gated by HP above 70%. | Above 70%, OnAction applies both writes to `stageMeleeDef`. The card says Def falls by 1. |
| E-21 | Implement | data | `compression_burst`: set `MoveEffectsOncePerTarget` true. Power stays 20, 2 hits, 3 AP, Common. | The flag already limits stages to the first confirmed hit on that target. The card says the first hit lowers Def. Both hits do today. |
| E-07 | Implement | data | Jammed: combat stage Accuracy −1, apply once, duration 2, owned, reverted on expiry. Summary "Accuracy down (Acc −1)". Eight cells, no matrix edit. | Dusted already stores Evasion this way and reverts it. The stage column's old limit is gone. −1 is the saved rule. |
| MV-bug_fix | Implement | data | Add `Cleanse:1:100%:Self:bug_fix` and `Staggered:1:1.0:Self`. Description: "Heal 10%, cleanse 1 debuff, and your next move deals −10%." Cost stays 1 AP, Common, Glitched. | Cleanse and Staggered exist. Staggered is −10%, so the card's −20% should change to the status that exists. Heal 10% already runs. Ungranted. |
| MV-rally_cry | Implement | data | Add `Yield:1:1.0:Self`. Keep allies' Speed +1. Cost stays 3 AP, Uncommon, typeless. | Yield is +1 AP at end of turn, and a Self override does not hit the allies. Duration 1 ticks once on the granting end step, then expires. One AP, not three. |
| MV-refinery | Implement | data | Add `Yield:2:1.0:Self`. Keep the Def +1 move stage. Cost stays 2 AP, Rare, Mined. | The card's timed part is +1 AP at end of turn for 2 turns. Def +1 staying is what the stage column already does, and the sentence does not say the Def is temporary. |
| GAP-HOPOCH | Implement | data | Not a master row. Add `baseline_shot` (Pulse Shot, Common, power 26, 2 AP) to Hopoch's `OffTypeMoves`, beside `deep_reap`. | Every pool is supposed to have a Common damaging move. Hopoch's damaging rows are Yield Spike (Legendary), Stake Strike (Rare), and Deep Reap (Legendary offtype). This is the same typeless Common the other pools already use. |

Jammed does not unblock a chart edit. DEC-CHEM stays deferred after −1 lands. Rally Cry and Refinery both depend on Yield, which is already a live Staked status (`yield_cycle`). Bug Fix depends on Staggered, which modifiers already apply.

## Batch D — engine

Yes means: build these, and only these, in the resolver or status processor. Values are fixed so the pass does not turn into a redesign.

| ID | Rec | Work | Values | Why |
|---|---|---|---|---|
| E-25 | Change | engine | `charged_relay` counts distinct targets hit **by the acting team** this turn, threshold 2, gain +2 AP, heal stays 5%, cost stays 2, Epic, Forked. | The ledger is per actor, and this Chainling's only action is the utility, so its own hit count is always 0. A team count makes it a follow-up after an ally spread, which is the Forked job. |
| MV-isolation_tool | Implement | engine | On cast, apply Anchored for 1 turn even though its origin is ChemistryOnly. Do not apply Isolated. Cost stays 1 AP, Common, Shard. | Anchored already consumes on the next damaging move and forces one target. `ApplyStatus` rejects chemistry origins, so the CSV cannot do this. The move is in Crackett and Marrowhead. |
| MV-extraction_hit | Implement | engine | On a damaging hit, if MeleeAtk stage is already +3 or higher, grant Speed +1, once per turn. Do not also raise Atk. Rare Mined passive. Stays in Kodiax's pool. | Pickaxe Flurry already raises Atk on hits. A second Atk ladder on the same pool double-dips. The card's second clause is the one that still has a job. |
| MV-double_or_nothing | Implement | engine | Keep the 60% +1 / 40% −1 end-step roll. On the 40% branch, AP drains and steals against this Chainling fail until the next end step. Narrate it. Mythic Meme passive. No new catalog status. | The roll already runs in `TriggerPassiveMove` by move id. The immunity is the half that does not. Thread Lock still blocks gains; this only blocks drains and steals. |
| MV-crash_routine | Implement | engine | If the target is below 50% MaxHP, deal 15% MaxHP and skip the Atk drop. Otherwise Atk −1 only. The 15% can KO. Legendary, Glitched, 2 AP. Ungranted. | The card states a branch. Today Atk −1 always runs and the damage never does. |
| E-12 | Implement | engine | At end of turn the bearer loses 1 AP and the applier gains 1 if that applier is still active. If the applier is benched or gone, the bearer still loses 1. Duration stays 2. Do not add `mev_extract` to Kitsauto's pool. | The move text says steal. The processor drains. Packet Skim (Common, 1 AP, in the pool) is the immediate drain; the Rare should pay the user or it loses to the Common. Proposal H. `starter_kitsauto_aggro` in [TestLoadouts.csv](../../Specs/Combat/Data/TestLoadouts.csv) is the current producer. |
| E-13 | Implement | engine | While Burned Tokens is up, Kindled's 6% end-step damage also heals the applier for the same integer, capped at missing HP. If the applier is gone, the burn still happens. Do not copy Rugged. Read Kindled's 0.06 from one place. | `deflation_mark` is in Cinislug's pool next to Kindling, and the heal sentence does nothing. Copying Rugged would stack 1/16, 2/16, 3/16 on top of Kindling. Proposal I. Depends on Kindling, which is already approved. |
| MV-hype_burst | Implement | engine | Remove up to 2 buffs from the target. The user gains Atk +1 for each buff actually removed. 2 AP, Common, Burned. Ungranted. | The sentence matches the name. Cleanse only removes debuffs, so this needs a buff strip. |
| MV-script_loop | Implement | engine | If the target has 2 or more General debuffs, extend each of those debuffs by 1 turn. 1 AP, Common, Bot. Ungranted. | `ExtendBuff` only lengthens buffs. The card is the debuff version, and Bot is the denial type. |
| MV-viral_surge | Implement | engine | 50% gain 2 AP, otherwise lose 1 AP, and always +1 to one random core stage (Atk, Def, or Speed). 1 AP, Mythic, Meme. The master calls it Common; the asset is Mythic. Keep Mythic. Ungranted. | Meme's randomness should live on the Mythic, not on Dice Roll. An exclusive branch cannot be three independent 33% utilities. |
| MV-wildcard_swap | Implement | engine | Swap one random non-zero core stage with the target. If neither side has a stage, the move does nothing and should say so. 2 AP, Common, Meme. The master says 1 AP; the asset is 2 AP. Keep 2. Ungranted. | The name is the effect. There is no swap opcode. |
| E-11 | Change | engine | HODLed: one owned MeleeDef +1 per end step, stop at +3, revert the sum when it expires. Delete the extra +1 while Def is below +3. Summary drops "Immune to stat drops." No producer, no new move. | From Def 0 the current body writes +2, +3, +4 across three end steps, and nothing checks immunity. [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md) Proposal C, body only. The card waits on Batch E question 3. |
| GAP-TICK | Change | engine | Not a master row. Run `EndOfTurn` passives before the status duration tick. Leave Kindled's 6% where it is, ahead of Rugged. | The saved rule is that move, modifier, and passive ticks resolve before chemistry and status ticks. The end step still does status first, then passives. Kindled-before-Rugged is the approved exception. [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md) §6. |

Dependencies inside this batch: E-13 wants APR-KIND present (it is). E-11 should land before any future HODLed card. Isolation Tool wants Anchored's consume-on-next-move behavior left as it is. Viral Surge and Wildcard Swap do not depend on Dice Roll once Dice Roll's text is shortened in Batch A.

Do not build, even in this batch: a recoil opcode, a hit-count for the walls, a Jammed −2, an Overleveraged HP body, or a second Cipher Lock.

## Batch E — questions

Each question is one yes or no. The recommended answer is the one to take if the yes is "approve the recommendation."

1. **Leave the unapproved packs unbuilt?** DEC-S1 (Fee Tap, Cipher Skim, Relay Cut, Brace Yield, Mempool Mark, Aim Lock), DEC-S2 (utility AI recharge penalty), DEC-S3 (Gas Vent, Anchor Tap), DEC-S4 (a recharge-2 utility), DEC-S5 (a 1 AP toll on a voluntary swap), DEC-PACK (the other 20 design-idea moves and M1–M6), and DEC-CD (Compressed C1–C7 and Bot D1–D7). **Recommended: yes, leave them.** They were drafted before the typed-pool rule, and the master already says they are not approved. DEC-S2's AI piece is replaced by question 2, which is narrower. Aim Lock would also duplicate Precision Strike, which now has SureHit.

2. **Approve AI proposals 1 and 2 only?** From [AI_DECISION_QUALITY_ANALYSIS.md](AI_DECISION_QUALITY_ANALYSIS.md): (1) `GetLegalActions` emits one action per legal target slot and the scorer keeps that slot; (2) a pure hit estimate beside `EstimateDamage` that uses Atk/Def, the mean roll, average hits, accuracy, and ×1.2 / ×0.8, and does not call the chemistry resolver. Leave proposals 3 through 8, including the boss payoff horizon, for a later round. DEC-AI as written in [COMBAT_SYSTEMS_ANALYSIS.md](COMBAT_SYSTEMS_ANALYSIS.md) stays deferred. **Recommended: yes.** Those two are the measured misses (focus fire on the wrong slot, and a damage estimate that calls Hammer Sync a KO when it is not). The boss still gets +60 on Grind Tax only when it can already pay 6, so it will keep sipping; that horizon is real and it is not this yes.

3. **Author Glitched Mind, Unsealed, HODLed, or System Reset cards this round?** Mind Fault was Proposal B (Glitched, Rare, 2 AP, `GlitchedMind` for 2 turns). Seal Crack was Proposal D (Shard, Uncommon, 2 AP, Unsealed). Vault Hold was Proposal C and waits on the HODLed body. System Reset is 3 AP, the strip and the self-skip do not run, and it is in no pool. The unimplemented doc's "2 AP" is wrong; `Moves.csv` is 3. **Recommended: no.** Deprecate Rekt, Offline, and Relay in Batch A. Change HODLed's body in Batch D. Leave Glitched Mind and Unsealed as working bodies with no card. Leave System Reset empty until a control pass. Kreechank's five is full, and a 50% mis-act plus a 50% barrier bypass plus a full strip are three new buttons.

4. **Give Overleveraged a body now?** Nothing applies it. Voltage Rush is Speed +3 and a 25% next-move +1 AP surcharge. Risk Marker is a 100% next-move +1 AP surcharge on the foe. The status summary already says it is shelved. **Recommended: no.** A 25% HP punish is the shelved effect, whether it hits the user or the foe. Leverage Up is the high-risk card that matches the name without spending HP. Keep status 208 reserved for the self-damage patch, which still has to respect the boss tiebreak.

5. **Retune riderless high-rarity hits now?** Hype Train (Legendary, 52, 2 AP, index 49.4) ties Solid Strike (Rare, 52, with a 25% Speed drop). Yield Spike (Legendary, 56) sits just above Stake Strike (Rare, 52). Combustive Shift (Legendary, 22×2, index 41.8) is only a step above Gas Bump (Common, 34.2). Bulwark Blow (Epic, power 50, 3 AP, index 45, strikes with Def) still looks low on an Atk chart; Crystal Bash is the other Def strike and its power is 0 until the Def override is smoked. **Recommended: no.** High damage at 2 AP is legal on turn 1, and the saved rule is that high damage waits on an AP boost. Smoke Crystal Bash and Bulwark Blow before any Epic number moves. BAL-EPIC stays deferred with this no.

Rows those questions cover: DEC-S1, DEC-S2, DEC-S3, DEC-S4, DEC-S5, DEC-PACK, DEC-CD, DEC-AI, DEC-CHEM (chart stays; Jammed's body is Batch C and does not move cells), MV-system_reset, NP-glitchedmind, NP-unsealed, E-14, E-28, BAL-EPIC, E-23.

E-28, called out so it is not silent: a passive `targetOverride` other than self still applies to the owner, because both branches of the ternary assign the owner (`TriggerPassiveMove`). No live passive authors that override. **Defer.** Fix it when a passive needs it. Kindling applies Kindled in code, not through that column.

E-23, called out so it is not silent: Side Quest, After Hours, All-Stat Burn, Anniversary Mark, and Tournament Badge say "Retired. No combat effect." Setup Feed (`7.2-c`) already has an engine body (the next ally power action gains 12%, and a non-power action wastes it). **Defer the whole row.** Another agent owns these six. Do not fill the retired five, and do not edit Setup Feed.

DEC-CHEM, called out so it is not silent: leave ×1.2 / ×0.8, leave Meme with no cells, and do not move Jammed's eight cells onto thinner families in this round. **Defer.**

## Smoke checklist

These are the 39 `implemented, unverified` rows, plus a short pass over the follow-up v1 fixes so E-30, E-31, and E-32 are not left as "fixed" without a battle. [TestLoadouts.csv](../../Specs/Combat/Data/TestLoadouts.csv) only has three rows. Use them where named. The other fights are harness loadouts.

One battle can retire several rows. Watch the log, not only the popup.

### 1. Validape harness — `starter_validape_balanced`

Moves in the CSV loadout: Hammer Sync, Coordinated Shot, Buff Ally, Heal Ally. Add the Uplink passive if the harness can equip it. Partner starts unbuffed.

- Hammer Sync with an unbuffed partner deals the printed hit. After Buff Ally, the same hit is about +20% and not more. Coordinated Shot is about +15% in that same state. (Saved Hammer rule. Not an unverified row, but it shares this loadout.)
- Uplink: the buffed ally gains +1 AP, once. A second buff on that ally the same turn does not pay again. The user does not gain the AP. This is not Group Hug. **APR-RELAY.**
- If the harness can also equip Extend Protocol and Network Sync: Extend Protocol adds 1 turn to the ally's shortest buff and then locks itself for one upcoming turn (column 2). Network Sync raises each ally's lowest stage by 1 and says which stat. **MV-extend_protocol, MV-network_sync.**

### 2. Kitsauto — `starter_kitsauto_aggro`, then the live pool

The CSV loadout is Blade Flurry, Multi-Thread Shot, Debuff Script, MEV Extract. The live pool replaced the pressure passive with Packet Skim and does not grant MEV Extract. Run the CSV loadout once, then the pool.

- MEV Extract: for 2 end steps the foe loses 1 AP. Today the user does not gain it. After Batch D, the user gains 1 if still active. **E-12's current half is visible here; the steal is the batch, not this smoke.**
- Multi-Thread Shot and Blade Flurry are the damaging buttons. Debuff Script's Atk and Def drops happen. Packet Skim, on the pool loadout, cuts 1 AP immediately.
- Fox Feint, from the pool: the log should say once per battle, and the 999 recharge should not be the thing the player reads as the lock. **E-02 is already fixed in PR; this is the manual confirm.**

### 3. Gilarime — `starter_gilarime_stall`

Cryo Ray, Frost Shot, Hibernate, Barrier Wall. Then swap the harness to the pool (Frost Shards, Ice Wall, Cryo Ray, Gecko Guard) for the wall.

- Ice Wall: after Batch C, 25% mitigation for the rest of the turn, and +1 AP when a faster user is hit. Before Batch C, confirm the mitigation does **not** apply (hit budget 0) while the AP clause still can. **E-17, and the unverified half of E-16's import.**
- Hibernate and Barrier Wall still heal or wall as their cards say. No row closes on those alone; they are the control that the stall loadout did not break.

### 4. Accuracy and Evasion — one custom 2v2

Side A, any species, moves: Accuracy Focus, Evasion Maneuver, Optimize Aim, Zip Step, Pivot Line, Key Rotation. Side B: Balloom with Gas Cloud, plus Accuracy Disrupt, Evasion Pin, Dust Fees, Chain Seal if the harness allows off-pool moves.

- Each self card moves its own stages by the printed amount (Acc +2, Eva +2, Eva +2 with Def −1, Acc +1 and Eva +1 and Speed +1, Def +1 and Eva +1). **MV-accuracy_focus, MV-evasion_maneuver, MV-optimize_aim, MV-zip_step, MV-pivot_line, MV-key_rotation.**
- Each foe card moves the target (Acc −1, Eva −1, both −1, Speed −1 and Eva −1). Gas Cloud hits **both** foes, not one. It is Epic Gas, 2 AP, not the Common the master still describes. **MV-accuracy_disrupt, MV-evasion_pin, MV-dust_fees, MV-gas_cloud, MV-chain_seal.**
- A follow-up 95% hit misses more often into Acc −1 or Eva +2 than it did on turn 1. Precision Strike, if added for one exchange, still lands into Eva +2 at least 95% of the time. That last check is the Close on MV-precision_strike.

### 5. Keyper vs Validape — Cipher Lock

Keyper: Cipher Shot, Key Rotation, Verification Lock, Pulse Shot. Validape: Pack Rally or Buff Ally, then a damaging move.

- Cipher Shot lands: the target cannot gain Atk, Def, Speed, Accuracy, or a buff status from itself or an ally. The log names Cipher Lock. An already-applied buff keeps ticking down. **MV-cipher_shot, E-30.**
- Swap the locked Chainling out. The lock is gone on return, and the log says it fell away. A new buff lands.
- Verification Lock applies FUD (Atk −1, Def −1, Speed −1) and those stages are gone when FUD expires. Accuracy does not move. **E-26 is fixed in PR; this is the manual confirm, and it matches the E-06 recommendation.**

### 6. Cinislug — Kindling

Pool: Ember Wall, Scorch Shot, Pulse Shot, Deflation Mark, Kindling.

- A damaging hit that leaves the foe alive applies Kindled. That foe loses 6% MaxHP at end of turn, once. A second hit the same turn does not stack a second 6%. **APR-KIND.**
- Burned into Gas (or any Rugged cell) still ramps Rugged on its own. Kindled does not replace it. After Batch D, Deflation Mark should heal the user for the Kindled tick only. Before that batch, confirm the heal does not happen. **E-13's current half.**

### 7. Cofi boss

Pool plus the encounter kit: Bean Sip, Bean Beam, Grind Tax. Confirm Grind Tax is not in a learnable pool, a candy roll, or a PvP kit. **E-32.**

- Bean Sip: heal 8%, Speed +1, +1 AP, and Bean Sip itself is locked for one upcoming turn. The user can take a different action next turn. **E-01, already fixed in PR; this is the manual confirm.**
- Grind Tax at 6 AP: three hits on each foe, Speed −1 once per foe, not once per hit. Damage per foe is well above two Bean Beams. **DEC-BEAM close.**
- Let the Utility AI play Cofi from low AP. It should Sip or chip until it can pay 6, then prefer Grind Tax (+60). It will not bank on purpose. That is the known leftover, not a failure of this smoke.
- Same-tick wipe: both sides drop on one action. The player must lose. A KO that clearly happens first must win, including a player KO that lands before the boss falls. Confirm the boss is team 1, because a tied sequence awards team 1. **E-31.**

### 8. Balloom — Turn 1 and the shelf

Pool: Gas Bump, Gas War, Hard Fork, Fuel Surge, Gas Cloud.

- Turn 1, Gas Bump (2 AP, power 36) is selectable. All-In is not on the card list. **The Turn-1 rule.**
- Gas War hits, then the next move costs +1 AP. No HP loss on hit or miss. **MV-volatile_shot follow-up.**
- Hard Fork hits, then removes one of the user's General debuffs. No HP recoil. **MV-recoil_strike follow-up.**
- All-In, on a separate Meme harness if you want the row itself: spends the whole bar (minimum 4), power stays 98, and only All-In is locked next turn. No recoil, no KO refund. **MV-all_in follow-up.**

### 9. Chemistry pairs — one afternoon, five leads

Attacker type on the left. Use any damaging move of that type. Meme stays out.

| Lead | Cell | What to see | Rows |
|---|---|---|---|
| Staked into Compressed | Drip on self | 6% MaxHP at each of 3 end steps, including the granting turn. The spec's "once" is the stale line. | E-04 |
| Glitched into Block | Cascade Failure | The third application deals 25% MaxHP and can KO. It is not an automatic KO at 1 HP. Swap clears the counter. | E-05 |
| Frozen into Staked | Slowed | Speed −2 for 2 turns, then it returns. The compact label must not promise a 25% action loss. | E-03 |
| Forked into Glitched, or Shard into Node | Leaked | 10% MaxHP when that side acts, not a silent end-step DoT. | E-08 |
| Compressed into Block, or Node into Compressed | Dusted | Speed −3 and Evasion −3, both gone when Dusted ends. | E-10 |
| Gas into Node | Jammed | Today, a token and no Accuracy change. After Batch C, Accuracy −1 for 2 turns, then it returns. | E-07 after the batch |
| Burned into Bot | Backlash | A token only. No contact DoT. After Batch A the summary says so. | E-09 after the batch |

Tripwire (`6.6-a`) on an escape applies Slowed for a short duration, not a permanent Speed −1 and not the word "slow" parsed out of a description. **E-24.**

### 10. Def-over-Atk and barrier AP — custom

One Block or Staked fighter with Rallying Slam and Interest Strike. A partner or a setup move puts Def stage above Atk stage.

- At 0 Def and 0 Atk, neither move grants AP. The comparison is strict. **MV-rallying_slam, MV-interest_strike.**
- With Def above Atk, Rallying Slam grants +1 AP once. Interest Strike grants +1 AP on each hit that lands (accuracy 90, 2–3 hits).
- A hit that a barrier fully absorbs still grants that AP. **Saved barrier rule.**

### 11. The other live riders — Kodiax, Deghoul, Kreechank

- Kodiax: Pickaxe Flurry raises Atk on hits and stops at +2. Deep Dig is only a heavy hit. Ore Strike is only a contact hit. Crystal Bash, if equipped off-pool, deals damage from Def and doubles below 30% target HP. Power 0 must not mean 0 damage. **MV-pickaxe_flurry, MV-crystal_bash.** Extraction Hit does nothing until Batch D.
- Deghoul: Transaction Tax steals 1 AP. AP Siphon drains 1 AP on about half its hits. AP Drain, off-pool, is a small hit, drains 1, and sometimes grants 1. **MV-transaction_tax, MV-ap_siphon, MV-ap_drain.**
- Kreechank: Data Scramble drops Atk and Speed on both sides and grants the user +2 AP. Momentum Drill, off-pool, deals +25% into a foe that already has a debuff and the printed amount into a clean foe. Glitch Feedback, off-pool, removes 1 foe debuff and adds +1 AP to their next move. **MV-data_scramble, MV-momentum_drill, MV-glitch_feedback.**

### 12. Catalog file check — no battle

Confirm `Statuses.csv` and `StatusDef_*.asset` have no row for Reinforced, DustStorm, BridgedOut, ForkedChain, CompressedData, MemeSurge, or NodeRelay. The enum ids can remain. **NP-reinforced, NP-duststorm, NP-bridgedout, NP-forkedchain, NP-compresseddata, NP-memesurge, NP-noderelay.**

Confirm `Validape.asset` design notes call Extend Protocol a shared Node utility. Confirm [combat_species_pool_routing_inventory.md](../Audits/combat_species_pool_routing_inventory.md) warns that `GapType none` on an empty utility means both sides are empty. **DEC-STALE.** The master's sentence that the design note still calls Extend Protocol an exclusive is itself stale.

### What this smoke does not cover

Batch C and Batch D rows are not unverified yet. Run section 3 again after the walls, section 5 after Isolation Tool, section 6 after Burned Tokens, section 9's Jammed line after Batch C, and section 2 after the MEV steal. System Reset, Viral Surge, Wildcard Swap, Hype Burst, Script Loop, Crash Routine, and Double-or-Nothing are ungranted; one harness battle each is enough when they land.

## Outside the master, still worth a yes

| ID | Rec | Work | Proposal |
|---|---|---|---|
| GAP-HOPOCH | Implement | data | In Batch C. Hopoch gets Pulse Shot as an off-type Common so the pool rule holds. |
| GAP-TICK | Change | engine | In Batch D. End-of-turn passives move ahead of the status duration tick. Kindled stays in front of Rugged. |

Two pool notes, not new rows. Blockade is in Blockwyrm's five and is blank until Batch C. Isolation Tool is in two pools and is blank until Batch D. Those are the empty clicks a player can hit today. The other empty Implement rows are ungranted.

## Rules already satisfied

Checked so they are not reopened:

- Bean Sip: recharge column 2, +1 AP, heal 8%, Speed +1, `NO_CANDY`.
- Barrier-absorbed hits still reach the confirmed-hit utility path, so Interest Strike and Rallying Slam can pay AP on a fully blocked hit.
- Rallying Slam and Interest Strike use `StageHigherThanOtherStage` with MeleeDef against MeleeAtk. At 0/0 the AP does not pay. The 10:14 note that said `>=` is stale.
- Hammer Sync is +0.20 per buffed partner, cap 1.20. Coordinated Shot is +0.15, cap 1.15.
- Uplink and Kindling are in the resolver and in Validape's and Cinislug's kits.
- Bench recharge ticks. Other statuses pause.
- Boss mutual wipe uses KO order. A tie awards team 1. Non-boss mutual wipes still draw.
- `BOSS_ONLY` is excluded from pools, progression, candy, capture, and PvP.
- Group Hug (`4.5-a`) pays +1 AP to each living active ally after a Support. It is not Uplink.

## Sources

- [COMBAT_ISSUES_MASTER.md](../COMBAT_ISSUES_MASTER.md)
- [BATTLE_EFFECTS_CORRECTNESS_AUDIT.md](../Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md), [UNIMPLEMENTED_MOVE_EFFECTS.md](../Audits/UNIMPLEMENTED_MOVE_EFFECTS.md), [CATALOG_STATUSES_WITHOUT_PRODUCERS.md](../Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md), [BATTLE_MOVE_BALANCE_BY_RARITY.md](../Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md), [AP_COST_LADDER_IMPLEMENTATION_AUDIT.md](../Audits/AP_COST_LADDER_IMPLEMENTATION_AUDIT.md)
- [SELF_DAMAGE_SHELF.md](SELF_DAMAGE_SHELF.md), [STATUS_PRODUCER_GAPS_ANALYSIS.md](STATUS_PRODUCER_GAPS_ANALYSIS.md), [AI_DECISION_QUALITY_ANALYSIS.md](AI_DECISION_QUALITY_ANALYSIS.md), [AP_ECONOMY_ANALYSIS.md](AP_ECONOMY_ANALYSIS.md), [MOVE_COST_CURVE_ANALYSIS.md](MOVE_COST_CURVE_ANALYSIS.md), [COMBAT_DESIGN_IDEAS.md](COMBAT_DESIGN_IDEAS.md), [TYPE_GAP_PROPOSALS.md](TYPE_GAP_PROPOSALS.md)
- `Docs/Specs/Combat/Data/Moves.csv`, `Statuses.csv`, `SpeciesMovePools.csv`, `TestLoadouts.csv` in the 20:05 snapshot
- `Move_*.asset`, `StatusDef_*.asset`, `UtilityEffectExecutor`, `StatusProcessor`, `CombatResolver.CheckBattleEnd`, `BuffGate`, `DamageCalculator.ComputeHitChance`
