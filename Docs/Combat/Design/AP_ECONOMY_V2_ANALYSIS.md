# AP economy follow-up (v2)

## Post-reconciliation revisit (2026-10-19)

Every line here is a **PROPOSAL** or a status note. No gameplay code, data, pool, or test was changed.

Checked against the local working tree at about 23:30 CDT on 2026-10-19, after Codex finished `audit_reconciliation_v1.plan.md`. Between the 20:10 snapshot and now, the CSVs, assets, engine code, tests, and `COMBAT_ISSUES_MASTER.md` are all byte-identical. `ap_economy_v2_check.py` still gives **69 PASS, 0 FAIL**. `post_recon_revisit_check.py` gives **44 PASS, 0 FAIL**. The tempo model is `/workspace/balance/out/ap_tempo_check.py`, and its output is in `ap_tempo_check.out.txt`.

**Tally of the 29 items below:** Still applies 19 · Already done 0 · Changed 8 · Obsolete 2. There are also two new findings (N1, N2).

| # | Item | Status | Evidence / change |
|---:|---|---|---|
| 1 | (a) Headline counts and cost × rarity grid | Still applies | 211 rows. `move_ap.ts` agrees. Ten rows cost 4, one costs 6, none costs 5 |
| 2 | (b) Status of the 22 recommendations from 2026-10-17 | Still applies | Nothing has landed since 20:10 |
| 3 | (c) AP flow and bank calendar | Still applies | One clarification: a 1 AP move on turn 1 leaves 3 AP on turn 2, not 4. A turn-2 heavy needs a full turn-1 pass (or a 0 AP action) |
| 4 | (c) Typical-kit castability table | Still applies | — |
| 5 | (d) "Power ≥ 60 castable on turn 2 = violation" | Changed | The pass *is* the setup. It costs a whole action, and AP accrues at +2 per turn whatever you do. See the tempo section |
| 6 | (d) Fork Split / Shard Scatter called out, not gated | Still applies | — |
| 7 | (e) Boost / denial calendar on 4 and 6 | Still applies | The facts hold. The "move the band to 6" conclusion is withdrawn (row 12) |
| 8 | (e) Denial swing table | Still applies | — |
| 9 | (f) Per-type AP identity | Still applies | — |
| 10 | (g) The AI cannot bank and does not score denial | Still applies | — |
| 11 | (h) "Every power ≥ 60 move is legal on turn 2" listed as a defect | Changed | No longer a defect by itself |
| 12 | Rec 1: Gas War, Hard Fork, Deep Dig, Hammer Sync, Cryo Ray, All-In → 6 | Changed | Withdrawn for the five 72/80 cards. All-In is held. See R1–R1c |
| 13 | Rec 2: Finish Burn → 4 | Changed | Softened to "stays 3 while ungranted" (R2) |
| 14 | Rec 3: cap drain and steal at 2 per target per turn | Still applies | `CombatApService.Drain` and `Steal` still share no counter |
| 15 | Rec 4: Skim / Tax refund when they remove 0 | Still applies | — |
| 16 | Rec 5: the AI passes when one regen pays a heavy | Changed | The gate is narrowed (R5). An unconditional pass loses tempo |
| 17 | Lower: All-In's minimum ignores the Streamlined discount | Still applies | — |
| 18 | Lower: System Reset text | Still applies | It still says "skip your next action". The recharge blocks only System Reset (`IsMoveRecharging` matches `rechargingMoveId`) |
| 19 | (j) Crystal Extraction consistency flag | Obsolete | Cronus confirmed: heal the user 5% per foe debuff, then cleanse the foe. The code, CSV, master row, and tests match |
| 20 | (j) System Reset AP note | Still applies | `UNIMPLEMENTED_MOVE_EFFECTS.md` still says "Spends 2 AP". The live cost is 3 |
| 21 | (j) The other eight mismatches (Network Sync rarity, the two `AP_COST_*_MAX` constants, Viral Surge, Vapor Barrier, Dice Roll, Parting Gift, Group Hug) | Still applies | All unchanged. Network Sync is Epic in the CSV. The constants are 5 and 2 |
| 22 | Q1: is a pass a boost? | Changed | New default: the pass counts as the setup, and the 4 AP band stays |
| 23 | Q2: Hammer Sync and Cryo Ray stay at 4? | Changed | New default: **yes** |
| 24 | Q3: Crystal Extraction cleanse | Obsolete | Answered (row 19) |
| 25 | Q4: System Reset text and the stale 2 AP note | Still applies | Default unchanged |
| 26 | Q5: Fork Split and Shard Scatter at 4 | Still applies | Default unchanged (leave at 4) |
| 27 | Q6: MEV as a steal | Still applies | Default unchanged (no) |
| 28 | Q7: shielded AP | Changed | Still no, but for a new reason. The old reason was "6 AP does the job". The new one is that a bank already costs an action and is already exposed to Drained and skims |
| 29 | Q8: Parting Gift pays the benched copy | Still applies | Default unchanged |
| N1 | **New:** Gas War is `DamagingRanged` on two melee bodies | New finding | `DamageCalculator.cs:243-244` makes it read Ranged Atk. Balloom is Melee Atk 68 / Ranged Atk 40, and Deghoul is 72 / 40. Expected damage per cast: Gas War 44 into Keyper and 53 into Validape (4 AP), against Gas Bump's 45 and 48 (2 AP). Gas War one-shots no body from full HP |
| N2 | **New:** Cryo Ray and Deep Dig are out-rated by their own pools' Rare 2 AP hits | New finding | Gilarime's Frost Shot gives 109 expected damage for 2 AP, against Cryo Ray's 168 for 4 (0.77×). Kodiax's Ore Strike gives 100 for 2, against Deep Dig's 138 for 4 (0.69×). This is a rarity-rival flag, not a cost problem |

### Tempo: pass then heavy on turn 2, or two 2 AP hits

Engine facts the model mirrors:

- **AP.** Start at 0. Every living active gains +2 at the start of each turn, turn 1 included (`StatusProcessor.cs:457`, `apPerTurn: 2`). The cap is 10.
- **Order.** Both sides pick their actions, then the queue runs by Speed, then stars, then rebirths, then a 50/50 roll. `PRIOR` moves go first (`CombatResolver.cs:803-992`).
- **Damage.** `floor((power × Atk/Def × U(0.85,1.0) + 1) × chem × crit)`. Chemistry is ×1.2 or ×0.8. Typeless moves take the attacker's primary type. Melee reads Melee Atk/Def and ranged reads Ranged Atk/Def. Crit is 6.25% for ×1.5.
- **Accuracy.** Hit chance = accuracy × the accuracy-stage multiplier. Jammed is −1, which is ×2/3, so 95% becomes 63%.
- **Riders.** Jammed lasts 2 turns. Drained is −1 AP at each end step for 3 turns, and it ticks on the turn it lands. FOMO and Pumped go on the attacker. Gas War adds a +1 surcharge to the user's next move.
- **Model scope.** Base stats, IV 0, no modifiers, a 1v1 slot duel, 20,000 trials each. 2v2 focus fire and Hammer Sync's partner +20% are not modeled.

Policies:

- **L:** cast the 2 AP hit every turn.
- **PH:** pass on turn 1, cast the heavy on turn 2, then 2 AP hits.
- **PHc:** bank for the heavy every time.

In every case the foe casts its 2 AP hit every turn.

**The AP math first.** Both lines spend exactly 2 AP per turn, so neither leaves AP over. The heavy is two lights merged into one action and delivered a turn later. The heavy hitter can fire a 2 AP hit on turn 1 instead. But then turn 2 opens at 2, or at 3 after a 1 AP move, so every 4 AP cast costs exactly one idle action somewhere. The only real question is the heavy's damage compared with two of that body's own 2 AP hits. Timing, riders, roll variance, and KO thresholds then adjust the answer.

| Matchup (faster side first) | Light vs heavy (expected damage per cast) | Heavy ÷ 2 lights | Heavy-holder win %: L / PH / PHc | Holder HP after T2, L / PH | Notes |
|---|---|---:|---|---|---|
| M1 Deghoul (Gas, Spd 72, Gas Bump ×1.2 + Jammed) vs **Validape** (156 HP, Spd 60) | Snap Strike 43 (×0.8), Hammer Sync 114 (range 106–125) | 1.32 | 31 / **64** / 67 | 60.5 / 60.5 | Hammer Sync's minimum roll of 106 equals Deghoul's 106 HP, so one landed hit ends it, even at Jammed 63%. The L line needs three Snap Strikes |
| M8 Keyper (Spd 68, **Uncommon** Cipher Shot 40, neutral) vs **Validape** | Snap Strike 49, Hammer Sync 128 (minimum 120 against 102 HP) | 1.32 | 88 / 95 / 96 | 70.3 / 70.2 | One-shot threshold again |
| M7 Validape mirror (speed tie, 50/50 roll each turn) | Snap Strike 33, Hammer Sync 86 | 1.31 | 50 / 68 / **91** | 91.6 / 70.8 | No threshold involved. The 1.31× premium alone decides it |
| M4 Keyper vs **Kodiax**, Body Check light (30 power, 85% accuracy) | Body Check 52, Deep Dig 138 | 1.33 | 71 / 95 / 95 | 26 / 32 | Mined→Encrypted gives FOMO (Atk +1) to Kodiax |
| M4b the same, with **Ore Strike** as the light (Rare, 52, 2 AP) | Ore Strike 100, Deep Dig 138 | **0.69** | 94 / 95 / 94; KO turn 1.53 / 2.06 | **51 / 32** | The light line wins faster and keeps 19 more HP, because Keyper dies before its second hit |
| M3 Deghoul vs **Gilarime**, Frost Shot light (Rare, 52, 2 AP) | Frost Shot 109, Cryo Ray 168 | **0.77** | 100 / 100 / 100; KO turn 1.24 / 2.06 | **131 / 108** | Waiting gives Deghoul one more hit |
| M3b the same, Frost Shards light (Common, 12 × 2–4) | 78 / 168 | 1.07 | 100 / 100 / 100; KO turn 1.95 / 2.10 | 125 / 108 | — |
| M9 Deghoul vs **Kodiax** (Body Check light; Mined→Gas ×1.2 + Pumped) | 58 / 153 | 1.33 | 92 / 96 / 96 | 51 / 51 | — |
| M5 **Deghoul** with Gas War vs a slower Validape | Gas Bump 48, Gas War 53 (ranged, Ranged Atk 40) | **0.55** | **69** / 17 / 2 | 49 / 35 | N1. The surcharge also blocks turn 3's 2 AP hit |
| M5′ the same, with Gas War made **melee** (counterfactual) | 48 / 95 | 0.99 | **69** / 30 / 15 | — | At parity, tempo and the surcharge favor the lights |
| M2 Keyper (Cipher Shot ×1.2 + Drained) vs **Balloom** | Gas Bump 45, Gas War 44 | 0.49 | 1.6 / 0 / 0 | — | Drained eats the bank. After a turn-1 pass, 2 − 1 + 2 = 3 on turn 2, and the heavy can't be paid |
| M6 **Validape** vs a slower Cinislug (neutral, Kindling) | 36 / 95 | 1.31 | 97.5 / 95.7 / 92.9; KO turn 4.16 / 3.57 | 88 / 90 | — |

Counterfactual 6 AP costs, each the best banking line against the line above:

| Change | Result |
|---|---|
| Hammer Sync at 6, M1 | 67% → 54%. Power 80 and power 120 give the same 54%, because 80 already one-shots Deghoul |
| Deep Dig at 6, M4 | 95% → 13% |
| Hammer Sync at 6, mirror | 91% → 11% |
| Melee Gas War at 6 | 15% → 0.5% |

At 6, these cards fall to 12–19 expected damage per AP, and the second idle turn is fatal against a faster foe.

One landed heavy from full HP (no crit; "KO" means even the minimum roll kills):

- **Hammer Sync** (Validape): KO on Deghoul, Forkrook, Keyper. Possible KO on Balloom, Kodiax, Minefolk.
- **Cryo Ray** (Gilarime): KO on Balloom, Crackett, Deghoul, Kreechank, Marrowhead.
- **Deep Dig** (Kodiax): KO on Balloom, Deghoul, Keyper.
- **Hard Fork** (Balloom, off-type): KO on Deghoul, Keyper.
- **Gas War:** no KO on any body.
- **All-In:** KO on 6 bodies, possible KO on 4 more.
- **Finish Burn** (Cinislug): KO on Deghoul at base power, before its below-40% bonus.

**Verdict.**

Cronus is right where the heavy is priced at about 2× the holder's best 2 AP hit. That covers every 72-power card at parity, and every body whose pool has a strong Rare 2 AP hit:

- Pass then heavy never deals more damage, and it deals it later. The light line lands riders on two actions instead of one (chemistry reactions, Kindling, stage drops), and it can KO the foe before the foe's second action (M3, M4b, M5′).
- A bank is exposed in a way a spent bar is not. Drained, or one skim, turns turn 2's 4 AP into 3 (M2).
- One roll carries two turns of output. Under Jammed that roll is 63%.
- "Castable on turn 2 without setup" is not a real problem once tempo is counted. The pass is the setup.

What he is missing:

1. **The foe's two hits land either way.** Waiting does not give the foe extra hits. The holder's HP after turn 2 is identical in M1 (60.5 / 60.5) and in M9 (51 / 51). It only differs when the light line KOs the foe first.
2. **Burst KO thresholds.** A heavy that one-shots ends the fight in one action, where lights need three (M1: 31% → 64%; M8: 88% → 95%).
3. **The light that body actually has.** Validape's only 2 AP hit is typeless Snap Strike at 30, so Hammer Sync is a real 1.31× Legendary premium. In the mirror, banking wins 91% to 50%. Kodiax with Body Check rather than Ore Strike shows the same thing.
4. **Passing is not forced, and it is not free.** Every heavy costs exactly one idle action. There is no leftover AP in either line, except that Gas War's +1 surcharge pushes the following 2 AP hit to 3, so Gas War costs about one more idle action.
5. **Speed ties are a coin flip each turn.** They decide who reaches the KO first. They do not change the AP math.

Net: the 4 AP band is not the problem. A move to 6 would turn fair or weak cards into dead ones. The real defects are a mislabelled category (N1) and two heavies that their own Rares out-rate (N2).

### Revised recommendations (all PROPOSALS)

- **R1 (replaces Rec 1).** Keep Gas War, Hard Fork, Deep Dig, Hammer Sync, and Cryo Ray at **4 AP**. Withdraw the move to 6. Working rule: a 4 AP single-target hit should land at about 1.0–1.35× two of its holder's 2 AP hits, and casting it on turn 2 after a pass is accepted. Drop the planned test `HighPowerNeedsABoostBeforeTurnTwo`.
- **R1a (new, N1).** Make Gas War `DamagingMelee` / Contact, and keep power 72, 4 AP, and the self surcharge. It then reads Melee Atk 68 or 72 (about 95 expected damage into Validape, roughly 1.0× of two Gas Bumps). Data-only. The test would assert the category, and that Balloom's Gas War deals more than one Gas Bump against the same target.
- **R1b (replaces the All-In half of Rec 1).** All-In stays at minimum 4 while it is in no pool. It is the one real outlier: 98 power, about 1.36× two typed commons, a KO on 6 bodies from full HP, and it also spends any extra AP. If it is ever granted, cut the power to about 80 rather than raising the cost to 6.
- **R1c (N2).** Record "Cryo Ray < Frost Shot per AP" and "Deep Dig < Ore Strike per AP" in `rarity_rivals.md`. Accept them for now: both heavies keep one-shot value against specific bodies. Do not cut Frost Shot or Ore Strike in this pass.
- **R2 (replaces Rec 2).** Finish Burn stays at **3** while it is ungranted. If it enters a pool, set it to 4. From Cinislug it already KOs Deghoul from full HP at base power, before its ×1.5 below 40%.
- **R3, R4.** Unchanged: the denial cap of 2 per target per turn, and the Skim/Tax refund when nothing is removed.
- **R5 (narrows Rec 5).** The AI may pass only when one of these is true:
  - the next turn pays a boss-only move;
  - the heavy's minimum roll KOs the current target;
  - the heavy's expected damage per AP is at least 1.15× the best legal 2 AP hit.

  Otherwise it keeps clicking, because a blanket pass costs tempo (M5: 69% → 17%). Test: `UtilityAi_PassesOnlyForBossOrThresholdHeavy`.
- **Unchanged lower items:** All-In's printed minimum ignores Streamlined, and System Reset's text becomes "Can't use System Reset next turn" at 3 AP.

---

Date: 2026-10-20. Proposals only. No move, modifier, status, pool, or engine value was changed.

Source of truth: the local working-tree snapshot `chainbound_combat_snapshot_20261019_2010` (2026-10-19 20:10 CDT). `Docs/GameTheory/` was not read. This note builds on `Docs/Combat/Design/AP_ECONOMY_ANALYSIS.md` (2026-10-17) and on the ladder, the follow-up, the reconciliation, the cost-curve note, the AI note, and the new-concepts note. It does not restate those passes.

Check script: `ap_economy_v2_check.py`. Tally against this snapshot: **69 PASS, 0 FAIL, 69 claims.**

Every recommendation below is a **PROPOSAL**.

## (a) Headline counts

211 move rows (180 non-passive, 31 passive). Every passive costs 0. `move_ap.ts` matches all 211 CSV costs (0 mismatches).

Non-passive moves by printed AP cost:

| AP cost | Moves |
|---:|---:|
| 0 | 4 |
| 1 | 54 |
| 2 | 86 |
| 3 | 25 |
| 4 | 10 |
| 5 | 0 |
| 6 | 1 (`grind_tax`) |

Non-passive moves by AP cost and rarity:

| Rarity | 0 | 1 | 2 | 3 | 4 | 6 | Total |
|---|---:|---:|---:|---:|---:|---:|---:|
| Common | 1 | 30 | 23 | 8 | 0 | 0 | 62 |
| Uncommon | 0 | 2 | 25 | 9 | 5 | 0 | 41 |
| Rare | 0 | 8 | 11 | 5 | 0 | 0 | 24 |
| Epic | 1 | 10 | 15 | 1 | 1 | 0 | 28 |
| Legendary | 1 | 0 | 10 | 1 | 4 | 1 | 17 |
| Mythic | 1 | 4 | 2 | 1 | 0 | 0 | 8 |

The 4 AP rows are Gas War, Ember Shower, Automation Burst, Hard Fork, Deep Dig, Shard Scatter, All-In, Hammer Sync, Cryo Ray, and Fork Split. The only 6 is Grind Tax. Nothing costs 5.

AP sources and sinks that actually run:

| Kind | What pays | Amount | Where |
|---|---|---:|---|
| Regen | Living actives, start of turn, including turn 1 | +2 | `StatusProcessor.cs:457`, `CombatResolverConfig_Default.asset` `apPerTurn: 2` |
| Cap / floor | Gain clamps, drain floors | 10 / 0 | `CombatApService.cs:81-82`, `:107` |
| Self boost | AP Boost, Fuel Surge, Data Scramble, Bean Sip, Turn Start AP Gain, Desperation Surge | +2, +3, +2, +1, +1, +1 | `Moves.csv` rows 32, 70, 174, 204, 46, 59 |
| Bank | AP Reserve, up to 5, released next turn with +1 | +1 bonus | `Moves.csv:141` `BankAPUntilNextTurn:5:1` |
| End-step gain | Pumped, Yield (Yield Cycle, Rally Cry, Refinery) | +1 per tick | `Statuses.csv` Pumped, Yield |
| Team AP | Uplink, Group Hug, AP Router, AP Donation, AP Redistribution | +1, +1 each active, paid 2, paid 2, paid 2 | `CombatResolver.cs:5478`, `:2754`, transfer path |
| On-hit / proc gain | Rallying Slam, Interest Strike, Flame Charge, Multi-Thread Shot, Ice Wall, crit / miss / ledger / shatter passives | +1, often conditional | move rows and hardcoded passive ids |
| Discount | Streamlined Flow above 70% HP | −1, floor 1 if the printed cost is at least 1 | `CombatActionEligibilityService.cs:190-198` |
| Refunds | Spare Change, Rebate, Full Refund, Precision Focus, Echo (free repeat) | 1, or a free recast | `Modifiers.csv` 3.1-a/b/c, 3.2-a |
| Immediate denial | Transaction Tax steal, Packet Skim drain, AP Drain on hit, AP Siphon 50% | 1 | rows 71, 210, 33, 76 |
| Delayed denial | MEV Extracted, Drained, Memory Leak, Thread Lock, Fee Collector | −1 ×2, −1 ×3, −2 next start, block next regen, −1 if ahead | statuses and `CombatResolver.cs:6819` |
| Cost-up | Verification Lock, Glitch Feedback, Risk Marker, Gas War (self), Voltage Rush 25% self | +1 on the next move | surcharge opcode |
| Spend-all | All-In | printed 4, then the rest of the bar | `CombatResolver.cs:1732-1735` |
| Wall chip | Ember Wall | attacker loses 1 on the hit | `Moves.csv:106` |

Working AP role by type. "Live" means a current species pool or Cofi's encounter actually grants it.

| Type | Boost | Denial or tax | Discount / surcharge | Live pool? |
|---|---|---|---|---|
| None | AP Boost, Turn Start AP Gain, Desperation Surge | AP Drain, Defensive Reflex (self −1) | — | No species pool |
| Node | Uplink, Network Response, Overflow Reboot, Router / Donation class | — | — | Uplink on Validape. Router is not in that pool |
| Gas | Fuel Surge +3 | Transaction Tax steal, AP Siphon, Fee Collector | Gas War self +1 | Yes: Balloom boost, Deghoul steal |
| Bot | Multi-Thread Shot 10% | Packet Skim, MEV, Thread Lock | — | Kitsauto has Skim and Multi-Thread only |
| Encrypted | — | Memory Leak −2, Verification Lock +1 | — | Memory Leak is Keyper's signature |
| Staked | AP Reserve, Bean Sip, Interest Strike, Stake Refund, Yield | Grind Tax is a 6 AP sink, not a drain | — | AP Reserve, Bean Sip. Grind Tax is encounter-only |
| Compressed | — | — | Streamlined −1, Risk Marker +1 | Streamlined on Humpacked |
| Block | Rallying Slam, Immutable Ledger | — | — | Immutable Ledger on Blockwyrm |
| Burned | Flame Charge | Ember Wall chip | — | Ember Wall is Cinislug's signature |
| Frozen | Ice Wall +1 if faster, Crystal Focus is a 0 AP payoff and grants no AP | — | — | Ice Wall on Gilarime |
| Forked | Charged Relay +2, Conductive Counter +1 | — | Voltage Rush 25% self +1 | None of these are in Forkrook's pool |
| Mined | Refinery's Yield | — | — | Not in Kodiax or Minefolk |
| Glitched | Data Scramble +2 | Glitch Feedback +1 | — | Neither is in Kreechank's pool |
| Shard | Shattered Armor +1 on a super-effective hit | — | — | Not in Crackett or Marrowhead |
| Meme | Double-or-Nothing, Meme Momentum, All-In spends the bar | the miss branch drains 1 | — | No Meme pool. Ghost has no row. Cards stay ungranted |

| | Count |
|---|---:|
| Findings in this note | 8 |
| **PROPOSAL** items ranked below | 5 |
| Questions only Cronus can answer | 8 |

## (b) Status of the 2026-10-17 recommendations

Evidence is the current snapshot. "Implemented" means the number or the behavior landed, even when a later pass also changed power or rarity.

| 2026-10-17 recommendation | Status | Evidence |
|---|---|---|
| All-In 0 → 6 | Partly | `Moves.csv:191` cost 4, tag `SpendRemainingAp`, Epic, recharge 2. Not 6. Text no longer says the whole turn is skipped |
| Fork Split 3 → 5 | Partly | `Moves.csv:118` cost 4, Legendary, power 56. Landed at 4, not 5 |
| Shard Scatter 3 → 5 | Partly | `Moves.csv:187` cost 4, Legendary, power 22 |
| Recoil Strike 2 → 4 | Implemented | `Moves.csv:121` Hard Fork, cost 4, Uncommon, power 72. Recoil sentence is gone; it cleanses one self debuff |
| Ember Shower 3 → 4 | Implemented | `Moves.csv:104` cost 4. Power was later retuned to 38 |
| Automation Burst 3 → 4 | Implemented | `Moves.csv:162` cost 4, power 38 |
| Volatile Shot 2 → 4 | Implemented | `Moves.csv:72` Gas War, cost 4, power 72, self `NextMoveAPSurcharge:1` |
| Hammer Sync 2 → 4 | Implemented | `Moves.csv:198` cost 4, power 80, Legendary, +20% if the partner is buffed |
| Cryo Ray 2 → 4 | Implemented | `Moves.csv:200` cost 4, power 80, Legendary |
| Deep Dig 3 → 4 | Implemented | `Moves.csv:133` cost 4, power 72, Uncommon. The Def-ignore sentence is gone |
| Bean Beam 2 → 1 | Obsolete | Ladder left it at 2. `Moves.csv:205` |
| Body Check, Rapid Pummel, Focus Shot, Breach Rush, Frost Shards, Chain Fork, Stone Barrage 3 → 2 | Implemented | `Moves.csv:3`, `:11`, `:6`, `:16`, `:114`, `:123`, `:85` all cost 2 |
| Interest Strike 3 → 2 | Implemented | `Moves.csv:147` cost 2, Epic. Gate is now `StageHigherThanOtherStage`, so a fresh 0-vs-0 body does not refund |
| AP Drain 2 → 1 | Obsolete | Ladder made it a 16-power hit at 2 AP. `Moves.csv:33` |
| Thread Lock blocks the next regen, then ends | Implemented | Queued to next turn start (`CombatResolver.cs:3873-3875`) before regen (`StatusProcessor.cs:457`). Duration 1 then expires |
| MEV Extracted steals | Obsolete | Card and status say drain. `Statuses.csv` MEVExtracted `EoTAPChange` −1. Steal was left out of the ladder on purpose |
| Transaction Tax stays a 1-for-1 steal | Implemented | `Moves.csv:71`. Now Epic, and it is Deghoul's signature |
| Fee Collector drains 2 at 4+ AP | Still open | Still drains 1, and only from the first foe who has more AP than the holder (`CombatResolver.cs:6818-6822`) |
| Shielded AP on Fuel Surge, AP Boost, AP Reserve, Uplink | Still open | No such effect in `MoveEnums.cs`. Not recommended below; the 6 AP calendar does the same job |
| Yield Cycle +1 → +2 | Still open | `Moves.csv:138` still `Yield:3` at +1. Not recommended; it would pull a 6 forward |
| AP Reserve bonus +1 → +2 | Still open | `Moves.csv:141` still `BankAPUntilNextTurn:5:1`. Same reason |
| Gas Spike stages revert when the status ends | Implemented | Application writes owned stages (`StatusProcessor.cs:704`); expiry reverts them (`:336-346`) |
| Bench recharge ticks | Implemented | `CombatResolver.cs:501-516`, `StatusProcessor.cs:304-326` |
| Kitsauto learns MEV Extract and Thread Lock | Obsolete | `SpeciesMovePools.csv:7` is Multi-Thread, Packet Skim, Debuff Script, Fox Feint, Blade Flurry. Thread Lock is Legendary and Fox Feint is Epic, so adding the lock would break the starter signature rule |
| Utility AI scores denial when the foe is near a heavy cost; cost 0 damage is not scored as 0 | Still open | `UtilityStatusAI.cs` never reads `StealAP` or `DrainAP`. `AggressiveAI.cs` scores non-damage at 0.1. `AIHelpers.cs:147` still returns 0 when `apCost` is 0. No damaging move costs 0 now, so the All-In case is gone |
| Thread Lock cannot be reapplied until two turns after it ends | Implemented | `StatusProcessor.cs:284` sets `threadLockBlockedUntilTurn = turn + 2` |
| One turn removes at most 2 AP by drain and steal | Still open | `CombatApService.Drain` and `Steal` have no shared cap |
| Floor stays 0, regen returns the turn after a lock | Implemented | `CombatApService.cs:107`. The lock expires at the end of the suppressed turn, before the next regen |
| A 0 AP Chainling can still act | Implemented | Low AP Surge, Low AP Burst, Crystal Focus |
| Shielded AP and fat-pool steal as new engine rules | Still open | Neither was built. Fat-pool drain of 2 every turn would lock a holder; see finding 5 |

## (c) Current AP flow

| Rule | Value | Where |
|---|---|---|
| Start | 0 | `ChainlingInstance.cs:92` `public int currentAP`. Factory copies `displayAP` from it and does not grant a starting bank |
| Regen | +2 | `CombatConstants.cs:10`, config `apPerTurn: 2` |
| Cap | 10 | `CombatConstants.cs:11`, config `apCap: 10` |
| Floor | 0 | `CombatApService.cs:107` |
| Who regens | Living actives only | `CombatResolver.cs:416-422` |
| Bench | No regen. Recharge still ticks | `CombatResolver.cs:501-516` |
| Carry | The bar sits on the Chainling. The slot is what the next body inherits | Below |

Order inside a turn:

1. Pending turn-start statuses, including a Thread Lock that was queued last turn (`CombatResolver.cs:415`, `:440-464`).
2. Regen (`StatusProcessor.cs:455-457`). Turn-start passives run after that, so Low AP Surge sees the granted AP (`CombatResolver.cs:426-437`).
3. The action queue is built. Fee Collector then drains 1 from the first opposing actor if that foe has more AP (`CombatResolver.cs:994`, `:6818-6822`). This is before any spend.
4. The actor pays effective cost, then All-In pays whatever is left (`CombatResolver.cs:1714-1735`). Effective cost is printed + surcharge − Streamlined discount, floored at 1 when the printed cost is at least 1 (`CombatActionEligibilityService.cs:190-198`).
5. The move resolves. Uplink pays after that (`CombatResolver.cs:1805`, `:5445-5478`). Group Hug pays +1 to every living active on a support move that has modifier `4.5-a` (`CombatResolver.cs:2741-2754`).
6. End of turn runs end-of-turn passives first, then status ticks (`CombatResolver.cs:473-483`). That matches the rule that passive ticks land before status ticks. Thread Lock is still up while Double-or-Nothing rolls, then the duration tick removes it.

Swap and KO both copy the active slot's current AP onto the Chainling coming in (`CombatResolver.cs:6688-6695`, `TeamState.cs:168-178`). The Chainling who left keeps the AP it had. That copy does not regen on the bench, and the next time that Chainling enters, the slot overwrites it. Bench time does not grow a second bar.

Bank with no spend and no boost. "Castable" means the action window is at least the printed cost.

| Turn | AP at the action | Costs that open |
|---:|---:|---|
| 1 | 2 | 0, 1, 2 |
| 2 | 4 | 3, 4 |
| 3 | 6 | 6 |
| 4 | 8 | — |
| 5 | 10 | Cap. Further regen is wasted while the bar stays full |

One AP boost, defined here as a turn-1 action that grants +2 and costs 0 (AP Boost). The boost uses the turn, so the damage move waits. Turn 2 opens at 6. A +3 Fuel Surge opens turn 2 at 7.

One denial hit, defined here as an immediate −1 on turn 1 (Tax or Packet Skim) and then a bank: turn 1 ends at 1, turn 2 opens at 3, turn 3 at 5, turn 4 at 7, turn 5 at 9.

Typical kit, using live costs. "Yes" means that move is selectable that turn under the column's rule.

| Kit | Move | Cost | No setup | One +2 boost | One −1 denial |
|---|---|---:|---|---|---|
| Common | Gas Bump, Snap Strike, Blade Flurry, Crystal Extraction | 2 | Turn 1 | Turn 2 (the boost took turn 1) | Turn 1 only if the hit is faster than the drain; otherwise turn 2 |
| Common | Packet Skim, Quick Jab, Bean Sip, Transaction Tax | 1 | Turn 1 | Turn 2 | Turn 1 |
| Common | System Reset, Cinder Burst, Compression Burst | 3 | Turn 2 | Turn 2 | Turn 2 (window is 3) |
| Uncommon | Cipher Shot, Bean Beam, AP Drain, Multi-Thread Shot | 2 | Turn 1 | Turn 2 | Same speed check as Gas Bump |
| Uncommon | Ice Wall | 3 | Turn 2 | Turn 2 | Turn 2 |
| Uncommon | Gas War, Deep Dig, Hard Fork, Ember Shower, Automation Burst | 4 | Turn 2 | Turn 2, with AP left over | Turn 3 |
| Rare | Rapid Strike, Lean Hit, Yield Cycle | 1 | Turn 1 | Turn 2 | Turn 1 |
| Rare | Ore Strike, Frost Shot, MEV Extract, Refinery | 2 | Turn 1 | Turn 2 | Speed check |
| Rare | Coordinated Shot, AP Siphon | 3 | Turn 2 | Turn 2 | Turn 2 |
| Epic | All-In | 4 minimum, then the rest | Turn 2 at exactly 4 | Turn 2, and the extra 2 is spent | Turn 3 |
| Epic | Bulwark Blow | 3 | Turn 2 | Turn 2 | Turn 2 |
| Legendary | Hammer Sync, Cryo Ray, Fork Split, Shard Scatter | 4 | Turn 2 | Turn 2, not earlier | Turn 3 |
| Legendary | Finish Burn | 3 | Turn 2 | Turn 2 | Turn 2 |
| Legendary | Grind Tax | 6 | Turn 3 | Turn 2 | Turn 4 |
| Legendary | Fuel Surge, AP Reserve | 0 | Turn 1 | They are the boost | Turn 1, then the gain is smaller if the drain landed first |
| Legendary | Thread Lock, Memory Leak | 2 | Turn 1 | Turn 2 | Speed check |

Streamlined Flow on a 3 makes it a 2, so Finish Burn would be turn 1 at high HP. Streamlined is not in a pool with Finish Burn. On a 4 it makes a 3, which is still not turn 1 (the window is 2).

## (d) Setup gate

Rule used here: a high-damage move needs an AP boost before it is castable on turn 1 or turn 2. A pass is not a boost. Printed power ≥ 60, plus All-In and Grind Tax.

Field index is power × accuracy/100 × average hits, ×2 for `AllEnemies` or `Field`. It is evidence, not the gate.

| Move | Power | Cost | Index | First turn, regen only | Verdict |
|---|---:|---:|---:|---|---|
| All-In | 98 | 4 | 93.10 | 2 | Violation. No boost required |
| Hammer Sync | 80 | 4 | 76.00 | 2 | Violation. Validape's signature |
| Cryo Ray | 80 | 4 | 76.00 | 2 | Violation. Gilarime's signature |
| Gas War | 72 | 4 | 68.40 | 2 | Violation. Balloom and Deghoul |
| Hard Fork | 72 | 4 | 68.40 | 2 | Violation. Balloom off-type, not in the five |
| Deep Dig | 72 | 4 | 68.40 | 2 | Violation. Kodiax |
| Finish Burn | 60 | 3 | 57.00 | 2 | Violation. Also survives one −1 denial. In no pool |
| Grind Tax | 30 | 6 | 85.50 per foe, 171.00 across two foes | 3 | Not a violation. Three hits, boss only |

Nothing on this list is legal on turn 1 from regen alone. The whole violation is turn 2.

Fork Split (power 56, cost 4, spread index 106.40) and Shard Scatter (power 22, 2–3 hits, cost 4, spread index 104.50) are under the power gate and above every single-target index. Both are legal on turn 2. They are called out in the questions. They are not marked as power-gate violations.

## (e) Counterplay

Boost and denial now meet, and they do not meet evenly.

On the 4 AP band a pass is already enough. A +2 self-boost does not pull Gas War, Hammer Sync, Cryo Ray, Deep Dig, Hard Fork, or All-In onto an earlier turn than that pass. A single −1 on turn 1 does push them from turn 2 to turn 3. Denial is stronger than boost on the band the ladder actually shipped.

On a 6 AP move the two sides match:

| Line | First turn the 6 is affordable |
|---|---|
| Pass, no denial | 3 |
| Turn-1 +2 boost, no denial | 2 |
| Turn-1 −1, then pass | 4 |
| Turn-1 −1 and a +2 boost | 3 |
| Turn-1 Fuel Surge +3 and a −1 | 2 |

A +2 boost pulls one turn. One point of denial pushes one turn. Together they land on the untouched turn. Fuel Surge's third point beats one denial. That is the Mythic Gas signature doing more than a typeless +2, which is the right direction, and it is why Gas currently outruns Bot.

Net swing when the target actually has the AP. "Per AP spent" is the foe's loss plus the caster's gain, divided by the printed cost.

| Denial | Cost | What it removes | Swing per AP spent | Speed |
|---|---:|---|---:|---|
| Transaction Tax | 1 | Steal 1 now | 2 | Immediate. Best rate in the game. Live on Deghoul |
| Packet Skim | 1 | Drain 1 now | 1 | Immediate. Live on Kitsauto |
| Memory Leak | 2 | Drain 2 after the foe's next regen | 1 | Next turn. Live on Keyper |
| Thread Lock | 2 | Blocks the next +2 regen, then ends | 1, if they needed that regen | Next turn. In no pool |
| MEV Extract | 2 | Drain 1 at two end steps | 1 if they hold, 0 if they empty the bar | End step. In no pool |
| Fee Collector | 0 | Drain 1 before actions if the foe is ahead | Free when it fires | Before the spend. In no pool |
| AP Drain | 2 | Drain 1 on a 16-power hit, 25% to gain 1 | About 0.6 | On hit. The typeless card loses to Tax |
| AP Siphon | 3 | 50% drain 1 | About 0.2 | On hit |
| Drained chemistry | 0 | −1 at three end steps | Free if they hold | Bot → Node, Block, Staked; Gas → Bot; Encrypted → Gas |
| Ember Wall | 2 | Attacker loses 1 after the hit | The hit already landed | Cinislug |
| Next-move +1 | 1 | The next move costs 1 more | A 4 becomes a 5 | Soaked by spending it on a cheap move |
| Gas War | 4 | The user's own next move costs +1 | Self-tax, not foe denial | Balloom, Deghoul |

A 2 AP attack still shrugs off end-step denial. The attacker spends to 0, MEV and Drained remove 0, and the next regen puts the 2 back. Immediate denial only stops that attack when the denier is faster: 2 − 1 = 1, and the hit cannot be paid. If the attacker is faster, the drain hits an empty bar.

Grind Tax is the long-battle tax, and Bean Sip does not stock it faster than a pass. Sip costs 1 and grants 1, so the bar is unchanged. The sip-then-beam cadence the AI note measured reaches 6 on turn 4 because the beam spends 2. A boss who passes turn 2 instead can tax on turn 3. One −1 on turn 1 slips a banking boss from turn 3 to turn 4. A −1 on the tax turn itself, from a faster denier, drops 6 to 5 and the spend fails. The next turn opens at 7, one skim leaves 6, and the tax lands. Players can delay it. They cannot delete it with one click, and they should not be given a tool that does. Cofi's Speed is low enough that a faster Packet Skim or Transaction Tax wins that race. Thread Lock is not required.

A per-turn skim of 1 does not lock the bar, because regen is 2. A per-turn drain of 2 would. That is why the old fat-pool idea stays shelved.

## (f) Per-type identity

Bot is the denial type on the profile, and the only Bot species does not carry the denial kit. Kitsauto's live denial is Packet Skim. Thread Lock and MEV Extract are real, and they are in no pool. Gas holds the better button: Deghoul's signature steals 1 for 1 AP, and Balloom's signature is +3 AP. Gas is doing Bot's job and the boost job at the same time.

Node's live team AP is Uplink on Validape, plus Group Hug if a support move is modified. AP Router is still in the catalog and is not in Validape's five. Uplink's +1 pulls a 3 onto turn 1. It does not pull a 4 onto turn 1, and it does not pull a 6 onto turn 2. Node still wants an ally. That part of the profile holds. The "routes AP" sentence is ahead of the pool.

Meme has no chemistry cell, and none of its AP cards are in a pool. All-In, Double-or-Nothing, and Meme Momentum are self variance. Viral Surge's utility string is empty (`Moves.csv:188`). That is deferred work, not a chemistry leak. Leaving it empty is correct until a Meme pass.

Glitched, Shard, Forked, and Mined have AP cards in the catalog and none in a live pool. Their profiles are chaos, precision, spread, and ramp. They do not need an AP button to be readable. Frozen's live AP is Ice Wall's conditional +1. Block's is Immutable Ledger's on-hit +1. Both are side effects of a wall, which fits.

Encrypted matches. Memory Leak is Keyper's signature and it is a real −2 after regen.

Staked matches the patient bank: AP Reserve on Rattlestake, Bean Sip on Cofi, Grind Tax on the boss only.

Compressed matches the efficiency profile: Streamlined Flow is Humpacked's signature.

## (g) AI

Short version: the AI still does not play this economy.

`AggressiveAI` scores damage at `DamagePerAP × 10` and everything else at 0.1, so Packet Skim, Tax, MEV, and Thread Lock lose to any hit (`AggressiveAI.cs` around the 0.1 branch). `DamagePerAP` returns 0 when cost is 0 (`AIHelpers.cs:147`). `UtilityStatusAI.ScoreAction` gives every utility a flat 30, subtracts effective cost, and adds 60 when a boss-only move is already affordable (`UtilityStatusAI.cs:135`). It does not read `StealAP`, `DrainAP`, or `GainAP`. Bean Sip's +1 is invisible. At 6 AP the +60 is what makes Grind Tax beat Sip; without a bank, the +60 never comes up.

`GetLegalActions` adds Pass when the actor is dead or when no move is legal (`AIHelpers.cs`, the "Dead/null" and "No options" branches). It does not add Pass while a 1 AP move is legal. A brain that can click Gas Bump cannot save for Hammer Sync, and a brain that can click Bean Beam cannot save for Grind Tax unless Sip happens to be AP-neutral. The AI note already specified the fix. It is still open. This round does not redesign it.

## (h) What works, what does not, what could be better

What works. The free turn-1 nuke is gone. A standard typed or typeless hit is 1 or 2 AP and legal on turn 1. The heavy band is 4 AP and legal on turn 2 after a pass. Grind Tax is 6 AP, boss-only, and not in a learn pool. Bean Sip is +1 with a one-turn recharge. Thread Lock blocks the next regen and then cannot be reapplied for two turns. Interest Strike no longer refunds on a fresh body. Bench recharge ticks. End-of-turn passives run before status ticks. Boost and denial do offset on a 6 AP move. One point of denial delays Grind Tax and does not erase it.

What does not. Every power ≥ 60 move except Grind Tax is legal on turn 2 with no boost. A +2 boost does not make those moves earlier, so denial is the only side that moves the calendar. Finish Burn is the cheap one: power 60 at 3 AP, and one drain does not even slip it. Bot's identity is a 1 AP drain, while Gas steals and also holds the best boost. The AI cannot bank and does not score denial. System Reset's description still says the user skips the next action; recharge only locks that move. Viral Surge still has no payload.

What could be better. Put the real nukes on the 6 AP calendar that already offsets, leave the 2 AP line alone, cap stacked denial at 2 in a turn, and let a whiffed skim give the AP back so a slower Bot is not taxed for hitting an empty bar. Do not add shielded AP, do not double Yield, and do not put Thread Lock on Kitsauto.

## (i) Ranked recommendations

None of these change learn-pool membership or rarity. The 3+1+signature rule and the starter "one top rarity" rule stay as they are. Cost edits do not add a move above a body's band.

**1. PROPOSAL — power ≥ 72, and All-In, cost 6.**

Gas War, Hard Fork, Deep Dig, Hammer Sync, Cryo Ray, and All-In go from 4 to 6. Power, accuracy, hits, rarity, and riders stay. A pass then casts them on turn 3. A turn-1 +2 boost casts them on turn 2. One −1 on turn 1 pushes them to turn 4. Boost and denial land together on turn 3.

Validape still has Snap Strike and Gilarime still has Frost Shards for turns 1 and 2. Balloom's Fuel Surge is what pulls Gas War to turn 2, which is the kit Balloom already has. Deghoul and Kodiax keep Gas Bump and Ore Strike as the turn-1 hit. Hard Fork is off-type and stays off the five. All-In is in no pool. At a minimum of 6, All-In's 93 index no longer beats Hammer Sync's 76 index at the same printed 4.

Files: `Docs/Specs/Combat/Data/Moves.csv` rows 72, 121, 133, 191, 198, 200. The matching `Move_*.asset` `apCost` fields. Regenerate `Supabase/supabase/functions/_shared/move_ap.ts`; do not hand-edit it. The cost dictionary in `ApCostLadderTests.cs`.

Test: `HighPowerNeedsABoostBeforeTurnTwo`. After regen only, AP is 4 on turn 2 and `EvaluateMoveSelection` fails for each of the six ids. After a +2 gain, AP is 6 and selection succeeds. Rarity and pool membership are unchanged, and Validape's only Legendary is still Hammer Sync.

**2. PROPOSAL — Finish Burn costs 4.**

`Moves.csv:103` `finish_burn` `APCost` 3 → 4. Power stays 60. It is the only power ≥ 60 move under 4, it is legal on turn 2 even after a 1 AP turn-1 click, and one drain does not slip it. At 4 it joins the heavy band: turn 2 on a pass, turn 3 after one denial. It is in no pool, so the learn-pool rules are untouched. It is not raised to 6, because the base index is 57 and the +50% is the low-HP rider, not a second nuke.

Test: `FinishBurn_CostsFourAndSlipsOneTurnAfterADrain`. Printed cost is 4. With turn-2 AP at 3, selection fails. With turn-2 AP at 4, selection succeeds.

**3. PROPOSAL — one target loses at most 2 AP to drain and steal in a turn.**

`CombatApService.Drain` and `Steal` share a per-target, per-turn counter. The third point does not land. Thread Lock's blocked regen counts as that turn's 2, so a lock and a Tax do not also empty the bar. Chemistry Drained on later turns still ticks 1. The floor stays 0. This is the guardrail from the first note. It was never built. It matters more now that Tax, Skim, MEV, Fee Collector, and Drained can sit on the same target.

Files: `CombatApService.cs`, a turn stamp on `ChainlingInstance` or the turn ledger, reset in `ProcessStartOfTurn`.

Test: `DenialCap_StopsAtTwoPerTargetPerTurn`. A foe at 6 loses 1 to Tax and 1 to Packet Skim. A Drained tick in the same turn removes 0. End AP is 4.

**4. PROPOSAL — Packet Skim and Transaction Tax refund their cost when they remove 0.**

If the drain or the steal returns 0, the caster is refunded the 1 they just paid. The action is still spent. A faster holder who emptied the bar does not also tax the denier's next turn. A holder who had AP still loses it, and the denier still paid. Kitsauto and Deghoul keep the moves they have. No pool edit. No rarity edit. Do not raise the drain to 2. A repeated −2 locks every 4 AP and 6 AP cast for as long as the denier is faster, because regen is only +2.

Files: the `DrainAP` and `StealAP` branches in `UtilityEffectExecutor.cs`, limited to `packet_skim` and `transaction_tax`.

Test: `SkimAndTax_RefundCostWhenTheBarIsEmpty`. Foe at 0. After Packet Skim the caster's AP equals the pre-cast AP. Foe at 2. After Packet Skim the foe is at 1 and the caster is down 1.

**5. PROPOSAL — let the AI pass when one regen would pay a heavy or a boss move.**

This is the banking hole already measured in `Docs/Combat/Design/AI_DECISION_QUALITY_ANALYSIS.md`. The only addition is the gate: Pass becomes a legal action when `currentAP >= cost - apPerTurn` and the unpaid move is boss-only or has printed power ≥ 60, and no already-legal move is AP-neutral. Utility then prefers that Pass over a click that pushes the payoff back. Aggressive can keep ignoring denial. Do not rescore the whole utility formula in this change.

Files: `AIHelpers.GetLegalActions`, `UtilityStatusAI.ScoreAction`.

Test: `UtilityAi_PassesWhenOneRegenFromGrindTax`. Cofi kit, Sip recharging, AP 4, Grind Tax the best damage click next turn. The chosen action is Pass. At AP 2, with Gas Bump legal and no payoff within one regen, the chosen action is not Pass.

Lower, not in the five: All-In's printed minimum should ignore Streamlined's discount (`GetEffectiveApCost` can turn a printed 4 into 3 today, and would turn a printed 6 into 5). Test `AllIn_DiscountCannotLowerThePrintedMinimum`. And System Reset's description should follow All-In's corrected sentence, at cost 3. Test `SystemReset_TextMatchesRechargeAndCostStaysThree`.

Explicitly not proposed: shielded AP, Yield +2, AP Reserve +2, MEV becoming a steal, Thread Lock on Kitsauto, a new Meme chemistry cell, and any power retune of the 2 AP line.

## (j) Consistency flags

Crystal Extraction. Cronus's decision was self heal 5% per foe debuff, then cleanse. The reconciliation plan spells that as heal the user, then cleanse those debuffs off the foe. The issues master now says it "cleanses the foe" (`COMBAT_ISSUES_MASTER.md`, MV-crystal_extraction). The code does both halves in that order. `HealSelfPerTargetDebuff` counts General debuffs on the foe and heals the user 5% MaxHP per debuff (`UtilityEffectExecutor.cs:1110-1124`). `Cleanse:99` on `SingleEnemy` then removes General debuffs from the foe (`StatusProcessor.cs:889-907`). It does not heal the foe. It does not remove buffs. It does not cleanse the user. If the foe has no General debuff, the heal is 0, the cleanse removes 0, and the 2 AP is still spent. Removing the foe's debuffs also helps the foe. That is a question, not a change.

System Reset AP. `UNIMPLEMENTED_MOVE_EFFECTS.md` still says `system_reset` "Spends 2 AP" and "Empty payload." `Moves.csv:94` is APCost 3, `StripAllStatuses`, recharge 2, and the description still says "skip your next action." `move_ap.ts` has `"system_reset": 3`. The issues master correctly says 3 AP. The cost to keep is 3. The stale note and the description are the defect.

Other mismatches noticed in passing:

| Item | Doc or constant | Data or code |
|---|---|---|
| Network Sync rarity | Issues master calls it Common, 2 AP | `Moves.csv:65` Epic, 2 AP. `move_ap.ts` cost 2 |
| Damaging AP ceiling | `CombatConstants.cs:13` `AP_COST_DAMAGING_MAX = 5` | Grind Tax costs 6. All-In can spend up to the cap |
| Utility AP ceiling | `AP_COST_UTILITY_MAX = 2` | System Reset, Rally Cry, Ice Wall, Hub Heal are 3 |
| Viral Surge | Description still offers +2 or −1 AP | `UtilityEffects` empty |
| Vapor Barrier | Tag `APMAN` | Payload is a barrier, no AP opcode |
| Dice Roll | Older notes describe an ally +2 AP branch | Description is now a 20% heal only |
| Parting Gift | +1 to yourself on swap out | The point lands on the benched copy and is overwritten the next time that Chainling enters (`TeamState.cs:178`, `CombatResolver.cs:6695`) |
| Group Hug text | "each active ally" | The loop pays every living active, including the caster (`CombatResolver.cs:2747-2754`). The existing test expects the caster to gain the point |

`move_ap.ts` is not a cost mismatch. All 211 ids agree with the CSV.

## (k) Questions only Cronus can answer

1. Does a skipped turn count as the setup a high-damage move needs, or does an AP boost have to be spent? Suggested default: a pass is not a boost. Power ≥ 72 and All-In cost 6, so the unboosted cast is turn 3.
2. Should Hammer Sync and Cryo Ray stay at 4 because they are starter signatures? Suggested default: no. Both species already have a 2 AP hit for turns 1 and 2. Include them in the move to 6.
3. Crystal Extraction cleanses the foe's General debuffs after the user is healed from them. Is that the cleanse you wanted? Suggested default: leave the code. It matches the approved plan sentence. Say so in the master if the wording is the only worry.
4. System Reset's description says the user skips the next action, and one audit note still says 2 AP. Suggested default: keep cost 3, rewrite the description to "Can't use System Reset next turn," and correct the unimplemented note.
5. Fork Split and Shard Scatter are under power 60 and over every single-target index, at 4 AP, legal on turn 2. Suggested default: leave the ladder's 4. The gate in this note is printed power.
6. Should MEV Extracted become a steal again? Suggested default: no. The card and the status say drain. Steal would also outrun Transaction Tax across two turns, on a move that is in no pool.
7. Build shielded AP? Suggested default: no. A 6 AP cost already gives boost one turn forward and denial one turn back.
8. Parting Gift's +1 is written onto the benched copy and wiped on the next entry. Suggested default: pay the active slot (the Chainling who just came in), not the copy that the next swap overwrites.
