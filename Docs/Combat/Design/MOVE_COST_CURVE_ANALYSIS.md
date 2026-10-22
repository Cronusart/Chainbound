# Move cost curve

Analysis only. Nothing in this note is applied to moves, modifiers, statuses, or code.

Source of truth: the local Unity snapshot taken 2026-10-17 2:05 PM CT (`chainbound_combat_snapshot_20261017_1405`). Where that tree and the GitHub checkout differ, the snapshot wins. The check script reads `Docs/Specs/Combat/Data/Moves.csv` (208 rows, 72 damaging), `Statuses.csv`, and `SpeciesMovePools.csv`, and the notes below follow that output.

Intended home for this note: `Docs/Combat/Design/MOVE_COST_CURVE_ANALYSIS.md`.

Every recommendation below is a **PROPOSAL**.

This round is the cost curve. Earlier notes already cover design ideas, systems, type gaps, the AP calendar in general, enemy AI tiers, passives and statuses, and type matchups and chemistry. Those results are used here and not restated.

## Summary

The catalog has a real standard hit, and then it stops having a curve.

Thirteen typed commons sit on one number: power 64, accuracy 95, one hit, 2 AP, field index **60.80** (30.40 per AP). That spine is legal on turn 1, because regen opens the battle at 2. Five of those thirteen are the same number because the extra sentence in the description does not run. Above that line, the honest heavy hits are Fork Split (87.40 at 3 AP, about 5% over a spread target) and a pair of typed rares, Hammer Sync and Cryo Ray, that already sit on a typed-rare premium at 2 AP. Below the line, the typeless rares (Impact Knuckle 37.80, Vector Shot 34.20, Rupture Point 30.60) lose to the typed common they are supposed to line up with, and several same-type 3 AP multi-hits lose to the 2 AP button the species already has.

The top of the file is not a curve. All-In is power 98, accuracy 95, AP column 0, field index **93.10**, and it is free. Recoil Strike (72.20) and Volatile Shot (68.40) are the same class of problem: the self-cost in the sentence has no opcode, so the power is unpaid. Crystal Bash authors a Defense override and a low-HP double, then stores power 0, and `DamageCalculator` returns 0 before it reads the attack stat. Those four, and the rest of the still-open rider list, do not anchor the targets in this note.

AP costs in the catalog are 0, 1, 2, and 3. The 4, 5, and 6 bands are empty. A pass opens turn 1 at 2, turn 2 at 4, and turn 3 at 6, so every damaging move that exists is legal by turn 2 with no boost. Bot has no `DrainAP` or `StealAP` move, and the Bot denial that does exist (MEV Extracted, Thread Lock) slips today's top band, 3 AP, by **zero** turns. Boost and denial cannot partly cancel each other until a cost exists that a boost pulls forward and a drain pushes back.

**PROPOSAL**, in one line: publish the target table in this note, raise the undertuned cards onto it, leave the 60.80 spine where it is, and fill the empty top with one new 4 AP Shard hit and one Bot drain. Keep All-In, Recoil Strike, Volatile Shot, and Crystal Bash on a separate change / remove / implement table.

## Method

### Field index

The field index is the one already used in `Docs/Combat/Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md` and `Docs/Combat/Design/AP_ECONOMY_ANALYSIS.md`:

```
expected hits = (hitMin + hitMax) / 2
accuracy factor = accuracy / 100    if accuracy > 0, else 1
spread factor = 2                   if targeting is AllEnemies or Field, else 1
field index = power × expected hits × accuracy factor × spread factor
```

Accuracy 0 is a sure hit. The spread factor of 2 assumes a 2v2 in which both foes are alive. Attack, defense, chemistry, crits, and priority stay out of the field index. Priority is added later, inside the rider, as a flat 10.

The script reconciles these indexes against the snapshot: All-In 93.10, Solid Strike 60.80, Fork Split 87.40, Pellet Storm 47.50, Quick Jab 15.20, Bulwark Blow 45.00, Crystal Bash 0.00, Impact Knuckle 37.80. Chaos Strike's neutral resolved value is 68.40, because 60.80 times the mean of `1.5|0.75` (1.125) is 68.40. Bug Payoff, Momentum Drill, and Finish Burn resolve to 60.80 on a fresh turn, because their gates fail.

### What "resolved" and "EV" add

Resolved = field index × the conditional or random factor that actually applies on a neutral turn. A neutral turn is full HP, 0 stages, no statuses, equal Speed, and a foe who still has AP. Power 0 stays 0. `DamageCalculator` returns 0 before the attack-stat override, which is why Crystal Bash's MeleeDef override and its below-30% double do not produce damage.

EV = resolved + rider credit. Rider credit counts only payloads the snapshot resolver executes, and only for the neutral turn.

Damaging-move utility runs only when `ResolveUtilityTiming` is `AfterEachConfirmedHit`, `AfterMoveComplete`, or `ResolveBeforeFirstHit`. The importer default is `UtilityOnly`, and a damaging move left on that default does not run its utility string. Stage changes and status effects on a damaging move still run per confirmed hit. Support and status moves run their utility because they are not on the damaging path.

Live damaging utilities this screen relies on: AP Siphon's drain, Rallying Slam's AP gain, Flame Charge's AP gain, Deep Reap's lifesteal, Pickaxe Flurry's Attack stage, Interest Strike's per-hit AP gain, Multi-Thread Shot's AP gain, and Automation Burst's random stage drop.

### Neutral gates

Empty conditions pass. `DebuffCountAtLeast` and `StatusCount` pass only when the threshold is 0. `StageAtLeastOtherStage` passes at 0 versus 0, because 0 is at least 0. `NoBuffs` passes on a fresh foe. `HPBelowPercent`, `APAtMost`, and `SpeedHigherThanOther` fail. Random multipliers apply when that block runs. A scaling bonus with a count of 0 is just the base multiplier, which is why Coordinated Shot's "+15% per buffed partner" is a real upside and a neutral index of exactly 60.80.

That 0-versus-0 pass is why Rallying Slam and Interest Strike pay AP on a fresh Chainling. The rider credit for Rallying Slam is 19, not 20: 1 AP × accuracy 0.95 × 20 points per AP.

### Rider credit schedule

These weights are a judgment, one future step, not the whole fight. A 25% Speed proc must not reorder the damage ladder. They are constants in the check script.

| Credit | Points | Reading |
|---|---:|---|
| Damage stage (Attack or Defense, helpful direction) | 18 per stage | About 0.6 of one future standard hit at +50% damage |
| Speed stage | 8 | Tempo, smaller than a damage stage |
| Accuracy or Evasion stage | 6 | |
| Priority | 10 | Inside EV, outside the field index |
| AP gained | 20 per expected AP | One point is half a turn of regen |
| AP drained | 12 per expected AP | The floor eats a drain on an empty bar, so denial is discounted |
| AP stolen | 32 | Gain plus drain |
| Next-move surcharge | 12 | |
| AP transfer | 6 | |
| Cleanse | 8 | |
| Frozen Assets (switch lock) | 12 | |
| Exploit Mark | 0.15 × 60.80 | |
| Heal | percent × 132 HP / 0.925 | 132 is the systems-analysis median Max HP. 0.925 is the mean damage roll |
| Lifesteal | percent × field index | |
| Mitigation | percent × min(hit budget, 2) × 30 | Hit budget 0 scores 0 |
| Extend buff | 8 | |

Stage and status riders on damaging moves are multiplied by accuracy and by expected hits, and capped at 6 stages. Spread, all-allies, and field riders count two targets. Status stages that the snapshot actually reverts (Dusted, Bot Swarm, Fifty One Percent Attacked, FOMO, Slowed) are credited at `min(1, duration / 3)`. Stages that stick get the full permanent credit.

Gas Spike (Attack +2, Defense −2) and FUD (Attack −1, Defense −1, Speed −1) are **not** in that revert list. The issues master marks E-15 and E-26 fixed. This snapshot's `StatusProcessor` whitelist does not revert them. That violates the designer rule that a timed status takes its stat changes with it. On the credit scale Gas Spike's two stages are a wash plus the +3 AP, but the stick is still a rule break. FUD on Verification Lock is a large permanent debuff plus a surcharge. Neither row is a damage-curve anchor. **PROPOSAL:** revert both when the status ends. That call belongs with the status work, and this curve does not retune powers to compensate for the stick.

### Anchor rule (BAL-COST)

`COMBAT_ISSUES_MASTER.md` (dated 2026-10-16, inside the snapshot) says not to retune other powers off All-In, Recoil Strike, Volatile Shot, and Crystal Bash, and not to use them as the Common template, while they are still awaiting a change / remove / implement call.

The master is partly stale against the 2026-10-17 CSV. These rows now have the opcode the older audit called missing, so this round treats them as live and allows a retune: AP Siphon, Rallying Slam, Interest Strike, Pickaxe Flurry, Flame Charge, Momentum Drill, Bug Payoff, Deep Reap, Phantom Hack, Coordinated Shot, Hammer Sync, Chaos Strike, Seam Cut, Glitch Feedback, Data Scramble, Extend Protocol (local recharge column is 2), Multi-Thread Shot, Automation Burst. A live row can still be a bad number. Liveness is what makes it allowed to move.

The script's still-open list is the operational non-anchor set. A proposal id on that list fails the script. Those rows get the conditional table instead of a single number.

### What this screen leaves out

The field index still ignores Attack, Defense, chemistry, and crits. Two Chainlings with different Attack will not experience the same "60.80." The curve is a move-catalog screen, which is what the rarity audit and the AP note already share.

The AP calendar is one holder banking regen. It is not a full 2v2 with swaps, chemistry, or a second action in the same slot. Self-boosts occupy turn 1, so the damage move waits even when the boosted bar could already pay it.

Meme stays out of chemistry. This note does not add a cell.

## Current curve

### Cost census

Non-passive rows:

| AP cost | Rows |
|---:|---:|
| 0 | 6 |
| 1 | 52 |
| 2 | 82 |
| 3 | 37 |
| 4 | 0 |
| 5 | 0 |
| 6 | 0 |

Plus 31 passives. The crowded bands are 1 AP (mostly utility) and 2 AP (the damage pile). The empty bands are 4, 5, and 6. Among damaging moves, the 1 AP band is three cards: Rapid Strike, Lean Hit, and Quick Jab.

### Medians, and why the old median misleads

Anchor-only medians drop unimplemented-rider rows and anything at AP 0. Inclusive medians are every damaging row with AP above 0, which is the screen the earlier AP note used.

| Rarity | Anchor median per AP | Inclusive median per AP |
|---|---:|---:|
| Common | 17.73 (n=31) | 24.07 (n=39) |
| Uncommon | 19.95 (n=4) | 26.60 (n=6) |
| Rare | 18.00 (n=19) | 17.42 (n=25) |
| Epic | 15.00 (n=1) | 15.00 (n=1) |

The inclusive Common figure of about 24 is the one the AP note quoted. It is pulled up by Recoil Strike and Volatile Shot, which are unpaid self-costs. The anchor-only Common figure of 17.73 is pulled down by the weak 3 AP baselines. Neither median is the curve. The number designers can actually see in a fight is the spine: **60.80 at 2 AP, 30.40 per AP.**

By type, anchor damaging rows only, the typeless pile (`None`, 20 rows) has median index 31.05 and median 14.09 per AP. Staked's two anchor rows sit at 12.82 per AP. Encrypted, Frozen, Glitched, and Mined each show a median per AP of 30.40 because their anchor sample is the spine. Bot's three anchor rows median 24.07 per AP, which is the spread and the flurry, not a denial tool.

### The spine

These thirteen are power 64, accuracy 95, one hit, 2 AP, field index 60.80. "No live rider" means the neutral-turn credit is ~0. Several of them have a real upside that simply does not fire on a fresh turn.

| Move | Type | Anchor | Neutral EV | What actually runs on a fresh turn |
|---|---|---|---:|---|
| Coordinated Shot | Node | yes | 60.80 | Scaling is authored. Zero buffed partners adds nothing |
| Solid Strike | Block | yes | 62.70 | 25% Speed −1 |
| Cipher Shot | Encrypted | no | 60.80 | Buff-block sentence has no payload |
| Scorch Shot | Burned | yes | 65.08 | 25% Defense −1 |
| Frost Shot | Frozen | yes | 62.70 | 25% Speed −1 |
| Momentum Drill | Mined | yes | 60.80 | +25% versus a debuff. Gate fails when the foe has none |
| Ore Strike | Mined | no | 60.80 | "+10% per own buff" is not a scaling column |
| Stake Strike | Staked | no | 60.80 | "+10% per own Defense stage" is not a scaling column |
| Precision Strike | Compressed | no | 60.80 | "+20 accuracy" is not a column |
| Chaos Strike | Glitched | yes | 68.40 | Random `1.5\|0.75` runs. The mean is 1.125, so the hit is already over the spine |
| Bug Payoff | Glitched | yes | 60.80 | +50% at 2 debuffs. Fresh turn fails the gate |
| Partition Shot | Shard | yes | 72.20 | Frozen Assets is live |
| Jackpot Shot | Meme | no | 60.80 | Double / normal / half is not a random-multiplier list |

The band is crowded because five commons are copies of the same hit with a sentence that does not execute. Chaos Strike is the one anchor on this list whose neutral damage is already off the spine.

### What sits where, in plain terms

**Works.** The spine is a readable standard hit and it is available on turn 1. Hammer Sync and Cryo Ray at 68.40 are a typed rare at 2 AP, about 12% over the spine, which is the premium this note wants. Fork Split at 87.40 is a typed common spread at 3 AP and is only slightly hot. Ember Shower and Automation Burst at 72.20 are under a 3 AP spread target and are usable. Finish Burn's neutral hit is the spine paid on turn 2; the +50% below 40% HP is the card, and the gate is real. Phantom Hack's Defense −1 and Speed −1 run, because `NoBuffs` is true on a fresh foe. Shard Scatter's damage index (85.50) is already next to an uncommon spread target even with the Evasion rider missing.

**Does not work.** All-In is the largest index in the file and it costs nothing. Recoil Strike and Volatile Shot present unpaid power as if it were the top of the 2 AP band. The typeless rare singles sit at 30–38 against a typed common at 60.80. Rapid Strike (49.40, plus priority, EV 59.40) and Lean Hit (43.70) are 1 AP commons that outdamage that rare line and beat the spine on a per-AP basis. Chain Fork (34.20 at 3 AP) loses to Rapid Strike at 1 AP. Frost Shards and Stone Barrage lose to Frost Shot and Solid Strike at 2 AP. Multi-Thread Shot's field index is 28.50, the same number as typeless Snap Strike. Bean Beam at 22.80 loses to typeless Pellet Storm at 47.50. Crystal Bash is a rare that deals 0.

**Could be better.** The 3 AP band is full of cards a player should never click, because a cheaper same-type hit is larger. The 4–6 bands, which are the only place a boost and a denial can meet, have no moves. Bot's identity in the type profile is AP denial, and no Bot move drains or steals.

### Designer-rule flags this data already trips

- Typed moves beat typeless equivalents. Multi-Thread Shot equals Snap Strike (both 28.50, 2 AP, single). Bean Beam loses to Pellet Storm (22.80 versus 47.50, 2 AP, spread).
- A rare typeless move lines up with a typed common. Impact Knuckle 37.80, Vector Shot 34.20, and Rupture Point 30.60 do not.
- Typed value is available on turn 1, or a slow start pays back. The spine does this. The dead 3 AP same-type hits do not pay the wait back.
- Bot is the AP-denial type and should have AP-drain moves. Kitsauto's pool is Multi-Thread Shot, Automated Pressure, Debuff Script, Fox Feint, and Blade Flurry. No Bot row has `DrainAP` or `StealAP`. MEV Extract and Thread Lock exist on the type and are not in that pool.
- Recharge ticks on the bench. `CombatResolver.ProcessEndOfTurnForTeam` walks active slots only. `CombatRuntimeFixTests.BenchPausesStatusDurationUntilSwapIn` locks a recharge of 2 while benched. The issues master marks E-29 fixed and says bench recharge ticks. This snapshot does the opposite of the designer rule and of that "fixed" line.
- Timed-status stat changes end with the status. Gas Spike and FUD stick. See the method.
- High-damage moves need some AP-boost setup to fire early, and denial should be able to stop a potent hit from landing early. All-In fires on turn 1 for free. Recoil Strike and Volatile Shot fire on turn 1. Every other damaging cost in the file is legal by turn 2 with a pass. Denial of the current top band is a 0-turn delay.
- Each Chainling keeps at least one Common damaging move. Rattlestake, Cofi Lootwak, and Blockwyrm have none. Balloom's three commons are All-In, Volatile Shot, and Recoil Strike, all non-anchor. Hopoch's only common damage is Stake Strike, and Keyper's only common damage is Cipher Shot. Both are spine clones whose rider does not run, so the pool rule is met and the card is still a bare 60.80. This was already flagged in the matchup round. The retunes below do not remove a common damaging move from a pool that has only one.
- Simple effects, matching the name and the type. The still-open sentences (copy a buff at a percent, a random branch the resolver does not have, a miss cost with no opcode) are the violations. New concepts in this note stay on one verb.
- Meme has no chemistry interactions. Left that way.
- `COMBAT_Moves.txt` §6.1 still says rarity is complexity and availability, and that a Common at max evolution stays competitive with an Epic. This screen follows the developer rule for the balance flags: typed beats typeless, and a rare typeless lines up with a typed common. **PROPOSAL:** leave §6.1 untouched until the designer picks which sentence is the spec (see questions). This note does not edit it.

## Outliers

The script flags these. The reading is the point of the section.

### Free high value

All-In, index 93.10, AP 0, non-anchor. It is the only damaging AP-0 row over 20. A player casts it every turn. Denial cannot price it, because there is nothing to price.

### Overtuned against the target (anchor, resolved more than 15% over)

Only Rapid Strike (49.40 versus a 1 AP center of 28) and Lean Hit (43.70 versus 28). They are the entire interesting part of the 1 AP damage band, and they are too large for it. Priority on Rapid Strike pushes EV to 59.40, which is a 2 AP spine hit that also acts first and costs 1.

Fork Split at 87.40 versus a spread target of 82.80 is about 5% hot. The script does not call that overtuned. This note agrees and leaves it.

### Undertuned (anchor, resolved under 85% of target, AP above 0)

The long list in the script output is mostly three piles:

1. Typeless commons and rares whose power was never brought up to the spine. Quick Jab, Pulse Shot, Pressure Point, Snap Strike, Scatter Shot, Pellet Storm's neighbors, Body Check, Focus Shot, Rapid Pummel, Wide Burst, Blink Lunge, Impact Knuckle, Vector Shot, Rupture Point, Titan Crash, Rail Spike, Scatterburst, Kinetic Burst, Ricochet Storm, Breach Rush.
2. Same-type 3 AP hits that lose to the 2 AP button: Frost Shards, Chain Fork, Stone Barrage, plus Bean Beam, which loses to a typeless spread.
3. Hits whose damage index is low because a live rider carries the EV: Pickaxe Flurry (index 45.60, EV 81.60), Compression Burst (41.80, EV 76.00), Interest Strike (42.75, EV 90.25). Raising their power on top of the rider would double-pay them. The retune table leaves Pickaxe Flurry and Compression Burst alone and changes Interest Strike's refund, not by pretending the rider is fake.

Finish Burn prints as undertuned (60.80 versus a 3 AP center of 72) and is accepted. The low-HP rider is the reason it costs 3.

### Higher rarity loses the field index to an anchor Common

The rival is an anchor Common of the same AP and the same shape. Recoil Strike, Volatile Shot, and Deep Dig are not allowed to be that rival, or every 2 AP rare would "lose" to an unpaid 72.

The losses that matter:

- Every typeless rare single at 2 AP loses to Coordinated Shot's 60.80. That is the rare-typeless rule, broken in the data.
- Breach Rush, Titan Crash, and Rail Spike lose to Finish Burn's 60.80 at the same 3 AP.
- Scatterburst loses to Pellet Storm. Kinetic Burst and Ricochet Storm lose to Fork Split.
- Blade Flurry (51.30) loses to the typed common spine. It is Bot's rare damage exclusive, and the index is a common chip.
- Seam Cut (49.40) loses to the spine unless the Speed check hits. The rare's identity is currently "maybe tie a common."
- Bean Beam loses to Pellet Storm.
- Bulwark Blow (45.00 at 3 AP) loses to Finish Burn and, inside Block, to Solid Strike at 2 AP. The epic is a Defense-scaled hit. The Attack-stat index is the wrong ruler, and the cost still keeps a wall from pressing it on turn 1.
- Interest Strike, Cinder Burst, Pickaxe Flurry, and Zipped Strikes lose on the raw index and have live riders. The retune treats the rider as part of the card.
- Crystal Bash, Hype Train, Lucky Scatter, Glitch Barrage, Cipher Burst, Combustive Shift, and Shard Scatter also lose, and they are non-anchor. They are in the conditional table, not the power table. Shard Scatter's 85.50 "loss" to Fork Split is 2 points. That is not a retune.

### Typed at or under the best typeless Common

Only two anchor cases, same AP and same shape, typeless Commons only:

- Multi-Thread Shot 28.50 versus Snap Strike 28.50.
- Bean Beam 22.80 versus Pellet Storm 47.50.

Equal-with-a-condition is the thing the designer rule forbids, and Multi-Thread Shot is equal with no extra condition on the damage. The 10% AP gain is about 4.75 EV points. That does not make a typed move better than the typeless hit.

### Dead clicks

An anchor move whose field index is under a same-type anchor move that costs less AP:

| Dead click | Cost | Index | Cheaper same-type hit |
|---|---:|---:|---|
| Body Check | 3 | 25.50 | Impact Knuckle at 2 AP, 37.80 |
| Focus Shot | 3 | 27.00 | same |
| Rapid Pummel | 3 | 26.60 | same |
| Wide Burst | 3 | 41.80 | Pellet Storm at 2 AP, 47.50 |
| Breach Rush | 3 | 31.50 | Impact Knuckle |
| Stone Barrage | 3 | 38.00 | Solid Strike at 2 AP, 60.80 |
| Cinder Burst | 3 | 51.30 | Scorch Shot at 2 AP, 60.80 |
| Frost Shards | 3 | 34.20 | Cryo Ray at 2 AP, 68.40 |
| Chain Fork | 3 | 34.20 | Rapid Strike at 1 AP, 49.40 |
| Pickaxe Flurry | 3 | 45.60 | Momentum Drill at 2 AP, 60.80 |
| Compression Burst | 3 | 41.80 | Zipped Strikes at 2 AP, 49.40 |
| Chaotic Resonance | 3 | 49.40 | Chaos Strike at 2 AP, 60.80 |
| Bulwark Blow | 3 | 45.00 | Solid Strike at 2 AP, 60.80 |

Pickaxe Flurry and Compression Burst are dead on the damage index and alive on EV, because the stage riders run. Chaotic Resonance is a neutral floor under a status-count cap; raising the base to the 3 AP center would make the cap a second All-In. Those three stay off the power-raise list on purpose.

### Crowded and empty

- 2 AP is crowded: 82 non-passive rows, and 13 typed commons on one index.
- 1 AP damage is thin: three moves, two of them overtuned.
- 3 AP is crowded with cards that lose to 2 AP.
- 4, 5, and 6 are empty. That is the dead tier. It is also the only tier where "high damage needs a setup" can be true.

## Proposed target curve

**PROPOSAL.** Fit damage to this table. The field index stays the audit's definition (spread still counts as ×2 when both foes are alive). The *target* uses a smaller spread factor, because paying double for hitting both foes makes every spread the best button in its band, which is how Fork Split became the number everyone argues about.

### Centers

Typed-common, single target:

| AP | Target index | Per AP | When it is legal with only regen |
|---:|---:|---:|---|
| 0 | 0 | — | Free damage is not a band |
| 1 | 28 | 28.0 | Turn 1 |
| 2 | 60.80 | 30.40 | Turn 1. This is the live spine. It does not move |
| 3 | 72 | 24.0 | Turn 2 after a pass, or turn 1 with a gift or a turn-start +1 |
| 4 | 82 | 20.5 | Turn 2. A gift can put it on turn 1 |
| 5 | 95 | 19.0 | Turn 3. A boost puts it on turn 2 |
| 6 | 108 | 18.0 | Turn 3. A boost puts it on turn 2. A denial can push it to turn 4 |

Each extra AP buys a bigger hit and a worse rate. The 3 AP center is 72, not another 60.80, so the wait of one turn is worth about 11 points. The 6 AP center is 108, in the neighborhood of today's unpaid All-In, except it would be paid.

### Rarity and typing

Multiply the typed-common center by:

| | Common | Uncommon | Rare | Epic |
|---|---:|---:|---:|---:|
| Typeless | 0.75 | 0.88 | 1.00 | 1.00 |
| Typed | 1.00 | 1.05 | 1.12 | 1.00 |

A rare typeless single at 2 AP targets **60.80**, the typed common spine. A typed rare single at 2 AP targets **68.10** (1.12 × 60.80). Hammer Sync and Cryo Ray at 68.40 are that premium already.

Epic stays at 1.00 of the typed common center. Epic is a flexibility or a stat substitution, not a bigger Attack number. Bulwark Blow is the only epic damage row, and it already substitutes Defense.

Spread targets multiply by **1.15**, not by 2. A typed common 3 AP spread targets 82.80. Fork Split at 87.40 is about 5% over that and stays.

### Worked targets used in the retune

| Class | AP | Shape | Target |
|---|---:|---|---:|
| Typed common | 1 | single | 28 |
| Typeless common | 1 | single | 21 |
| Typed common | 2 | single | 60.80 |
| Typeless common | 2 | single | 45.60 |
| Typeless rare | 2 | single | 60.80 |
| Typed uncommon | 2 | single | 63.84 |
| Typed rare | 2 | single | 68.10 |
| Typeless common | 2 | spread | 52.44 |
| Typeless rare | 2 | spread | 69.92 |
| Typed common | 3 | single | 72 |
| Typeless common | 3 | single | 54 |
| Typeless rare | 3 | single | 72 |
| Typed rare | 3 | single | 80.64 |
| Typeless common | 3 | spread | 62.10 |
| Typeless rare | 3 | spread | 82.80 |
| Typed common | 3 | spread | 82.80 |
| Typed uncommon | 3 | spread | 86.94 |
| Typed rare | 4 | single | 91.84 |

### Where this disagrees with the AP-economy cost pass

`AP_ECONOMY_ANALYSIS.md` §6.1 keeps power still and moves costs. Several of those cost raises land on cards that are already on this curve, or on the typed-rare premium. A cost raise and a missing HP cost on the same card is a double tax. A cost cut that leaves the index under the button the species already has does not create a click.

**PROPOSAL:** leave these costs where they are.

| Move | §6.1 cost | This curve | Why |
|---|---:|---|---|
| Fork Split | 3 → 5 | stay 3 | 87.40 versus spread target 82.80. Hot by about 5%. A 5 AP cost would hide a fair spread behind two extra turns |
| Shard Scatter | 3 → 5 | stay 3, conditional | 85.50 versus uncommon spread target 86.94, and the Eva rider is still missing. Adding the rider later means cutting power. Moving it to 5 prices a card that is already on target |
| Ember Shower | 3 → 4 | stay 3 | 72.20 versus spread target 82.80. Under the target. A higher cost makes a fair-to-low spread worse |
| Automation Burst | 3 → 4 | stay 3 | Same index, same reading. The random stage drop is a small live rider |
| Hammer Sync | 2 → 4 | stay 2 | 68.40 is the typed-rare 2 AP center (68.10). The partner scaling is upside |
| Cryo Ray | 2 → 4 | stay 2 | Same center. The 25% Speed −2 is a live rider on top |
| Deep Dig | 3 → 4 | stay 3, conditional | 68.40 is near the 3 AP center of 72, and the Defense-ignore sentence does not run. It is not the template, and it is not a 4 AP nuke |
| Recoil Strike | 2 → 4 | no single cost | The 15% recoil is missing. See the conditional table. Implementing the recoil and also raising AP charges twice |
| Volatile Shot | 2 → 4 | no single cost | The miss self-hit is about one index point. See the conditional table |

**PROPOSAL:** where §6.1 cuts a cost and the index is still under the cheaper same-type hit, raise power onto the rung instead. That is Frost Shards, Chain Fork, Stone Barrage, Body Check, Rapid Pummel, Focus Shot, and Breach Rush. Bean Beam's cut from 2 AP to 1 AP is the one cost cut this curve keeps: the index is too small to be a 2 AP spread, and this note still does not add a 6 AP beam.

The spine staying at 2 AP is the part both notes agree on.

### Accepted distance from the raw index

After the proposals, a handful of anchor rows stay more than 15% off the raw-index target. The script fails if one of those is missing a reason. They are accepted because EV, priority, or a stat substitution is the card:

| Move | Why it stays off the raw index |
|---|---|
| Rapid Strike | Top of the 1 AP band. Priority is the rest of the value |
| Lean Hit | Best non-priority 1 AP chip. Above the center, under the 2 AP line |
| Blink Lunge | Priority tax. Stays under the typeless-rare line |
| Finish Burn | Neutral is the spine at 3 AP. The low-HP bonus is the card |
| Pickaxe Flurry | Damage index stays low. The live per-hit Attack ramp carries EV |
| Compression Burst | Damage index stays low. Both hits drop Defense. E-21 is still open: the text says the first hit, and the resolver drops Defense on every hit |
| Multi-Thread Shot | Typed chip above the typeless line and under the spine |
| Chaotic Resonance | Neutral is the floor. The status-count cap is the card, so the base stays under the 3 AP center |
| Cinder Burst | Attack procs put EV on the typed-rare 3 AP target |
| Bean Beam | 1 AP chip. The Speed drop on both foes is the value |
| Bulwark Blow | Epic is Defense substitution. The Attack index is not the target |
| Interest Strike | Neutral damage sits on the typed-common 3 AP center. The AP refund waits for Defense +1 |

## Per-move retune table

**PROPOSAL.** Data only. Not applied. Non-anchor ids are absent on purpose. "Index" is the field index. "EV" is resolved plus neutral rider.

| Move | Now | Proposed | Index | New index | EV | New EV | Reason |
|---|---|---|---:|---:|---:|---:|---|
| Rapid Strike | 52 / 95 / 1 AP | 36 / 95 / 1 AP | 49.40 | 34.20 | 59.40 | 44.20 | 1 AP priority finisher sits at the top of the 1 AP band. Priority stays |
| Lean Hit | 46 / 95 / 1 | 34 / 95 / 1 | 43.70 | 32.30 | 43.70 | 32.30 | Best non-priority 1 AP hit, under that band cap |
| Quick Jab | 16 / 95 / 1 | 22 / 95 / 1 | 15.20 | 20.90 | 25.20 | 30.90 | Typeless 1 AP chip at 0.75 of the 1 AP center. Priority stays extra |
| Chaos Strike | `1.5\|0.75` | `1.35\|0.65` (mean 1.0) | 60.80 | 60.80 | 68.40 | 60.80 | Keep the swing. The average returns to the spine. Power stays 64 |
| Impact Knuckle | 42 / 90 / 2 | 68 / 90 / 2 | 37.80 | 61.20 | 37.80 | 61.20 | Typeless rare lines up with the typed common spine. Accuracy 90 is the reliability tax |
| Vector Shot | 38 / 90 / 2 | 68 / 90 / 2 | 34.20 | 61.20 | 34.20 | 61.20 | Same typeless-rare line |
| Rupture Point | 34 / 90 / 2 | 62 / 90 / 2 | 30.60 | 55.80 | 34.65 | 59.85 | A step under the plain rare because the 25% Defense drop already runs |
| Snap Strike | 30 / 95 / 2 | 48 / 95 / 2 | 28.50 | 45.60 | 28.50 | 45.60 | Typeless common 2 AP single at 0.75 of the spine |
| Pulse Shot | 26 / 95 / 2 | 48 / 95 / 2 | 24.70 | 45.60 | 24.70 | 45.60 | Same line. Contact is not a damage tax, so the ranged twin matches |
| Pressure Point | 26 / 95 / 2 | 44 / 95 / 2 | 24.70 | 41.80 | 28.97 | 46.07 | Slightly under Snap Strike because the 25% Defense drop runs |
| Body Check | 30 / 85 / 3 | 64 / 85 / 3 | 25.50 | 54.40 | 25.50 | 54.40 | 3 AP typeless common center. Accuracy 85 stays the reckless identity |
| Focus Shot | 30 / 90 / 3 | 60 / 90 / 3 | 27.00 | 54.00 | 27.00 | 54.00 | Same center |
| Rapid Pummel | 8 / 95 / 3, hits 2–5 | 16 / 95 / 3 | 26.60 | 53.20 | 26.60 | 53.20 | Same center. Hit spread stays |
| Breach Rush | 10 / 90 / 3, hits 3–4 | 23 / 90 / 3 | 31.50 | 72.45 | 31.50 | 72.45 | Typeless rare 3 AP lines up with the typed common 3 AP center |
| Rail Spike | 46 / 85 / 3 | 85 / 85 / 3 | 39.10 | 72.25 | 39.10 | 72.25 | Same center. Accuracy 85 stays |
| Titan Crash | 48 / 80 / 3 | 90 / 80 / 3 | 38.40 | 72.00 | 38.40 | 72.00 | Same center. Accuracy 80 stays |
| Scatterburst | 22 / 90 / 2, spread | 39 / 90 / 2 | 39.60 | 70.20 | 39.60 | 70.20 | Typeless rare 2 AP spread |
| Kinetic Burst | 28 / 90 / 3, spread | 46 / 90 / 3 | 50.40 | 82.80 | 50.40 | 82.80 | Typeless rare 3 AP spread |
| Ricochet Storm | 12 / 90 / 3, hits 2–3, spread | 18 / 90 / 3 | 54.00 | 81.00 | 54.00 | 81.00 | Same spread target. Hit count stays |
| Scatter Shot | 16 / 95 / 2, spread | 28 / 95 / 2 | 30.40 | 53.20 | 30.40 | 53.20 | Typeless common 2 AP spread |
| Pellet Storm | 10 / 95 / 2, hits 2–3, spread | 11 / 95 / 2 | 47.50 | 52.25 | 47.50 | 52.25 | Same target. Multi-hit shape stays |
| Wide Burst | 22 / 95 / 3, spread | 33 / 95 / 3 | 41.80 | 62.70 | 41.80 | 62.70 | Typeless common 3 AP spread |
| Blink Lunge | 26 / 90 / 2, priority | 46 / 90 / 2 | 23.40 | 41.40 | 33.40 | 51.40 | Priority rare stays under the spine. The 2 AP cost is the price of acting first with a bigger chip |
| Multi-Thread Shot | 12 / 95 / 2, hits 2–3 | 20 / 95 / 2 | 28.50 | 47.50 | 33.25 | 52.25 | Typed Bot chip beats the retuned typeless 2 AP line (45.60) and stays under the spine. The 10% AP gain stays |
| Seam Cut | 52 / 95 / 2 | 64 / 95 / 2 | 49.40 | 60.80 | 49.40 | 60.80 | Neutral hit matches the spine. The Speed check is upside |
| AP Siphon | 56 / 95 / 3 | 70 / 95 / 3 | 53.20 | 66.50 | 58.90 | 72.20 | 3 AP Gas hit on the 3 AP center. The 50% drain stays a small rider |
| Cinder Burst | 18 / 95 / 3, hits 2–4 | 24 / 95 / 3 | 51.30 | 68.40 | 61.56 | 78.66 | Typed rare 3 AP multi-hit. The 20% Attack drops stay and carry EV |
| Blade Flurry | 18 / 95 / 2, 3 hits | 24 / 95 / 2 | 51.30 | 68.40 | 51.30 | 68.40 | Typed rare 2 AP center. Bot's damage exclusive, not a denial tool |
| Zipped Strikes | 26 / 95 / 2, 2 hits | 32 / 95 / 2 | 49.40 | 60.80 | 53.20 | 64.60 | Typed uncommon on the spine. Speed procs stay upside |
| Frost Shards | 12 / 95 / 3, hits 2–4 | 25 / 95 / 3 | 34.20 | 71.25 | 38.76 | 75.81 | Was under Frost Shot. Raise power onto the 3 AP center. Do not cut the cost |
| Chain Fork | 12 / 95 / 3, hits 2–4 | 25 / 95 / 3 | 34.20 | 71.25 | 34.20 | 71.25 | Belongs beside Fork Split, not under Rapid Strike |
| Stone Barrage | 16 / 95 / 3, hits 2–3 | 30 / 95 / 3 | 38.00 | 71.25 | 41.80 | 75.05 | Belongs on the 3 AP center, not under Solid Strike |
| Bean Beam | 12 / 95 / 2 AP, spread | 12 / 95 / **1 AP** | 22.80 | 22.80 | 38.00 | 38.00 | Weakest spread. At 1 AP it is a legal chip with a real Speed drop on both foes. No 6 AP beam |
| Bulwark Blow | 50 / 90 / 3 AP | 50 / 90 / **2 AP** | 45.00 | 45.00 | 45.00 | 45.00 | Epic stays a Defense-scaled hit. Cost 2 so a wall can press it on turn 1. Power stays, so the Attack index stays under the spine on purpose |
| Rallying Slam | power 64, AP gain if Defense stage ≥ Attack stage | Add a second gate: own Defense stage ≥ +1, require all | 60.80 | 60.80 | 79.80 | 60.80 | A fresh tie at 0 currently pays +1 AP (19 points after accuracy). The refund waits until Defense has actually been raised |
| Interest Strike | power 18, +1 AP per confirmed hit on the same tie | Power 30. Pay +1 AP once per cast, and only if Defense stage ≥ +1. Timing moves from `AfterEachConfirmedHit` to `AfterMoveComplete` | 42.75 | 71.25 | 90.25 | 71.25 | Neutral damage sits on the 3 AP center (30 × 2.5 × 0.95). The refund is the rare rider, and it no longer refunds the cast by itself on a fresh tie |

Left alone, on purpose, and still anchors: the rest of the 60.80 spine (Coordinated Shot, Solid Strike, Scorch Shot, Frost Shot, Momentum Drill, Bug Payoff, Partition Shot), Hammer Sync, Cryo Ray, Fork Split, Ember Shower, Automation Burst, Finish Burn, Phantom Hack, Pickaxe Flurry, Compression Burst, Chaotic Resonance, Flame Charge, Deep Reap. Deep Reap's lifesteal is gated below 50% HP, so the neutral index is the spine and the heal is the rare.

Phantom Hack is left alone and called out. Neutral EV is 85.50: the spine, plus a guaranteed Defense −1 and Speed −1, because a fresh foe has no buffs. That is a typed rare that works on turn 1, which the rule asks for. It is also the largest honest EV on the 2 AP band. Whether that is the encrypted rare or too much is a designer question, not a silent nerf.

## BAL-COST conditional table

**PROPOSAL**, and only after a call. These rows must not be used as the template for the numbers above. For each, the three calls are separate. Picking one does not imply the others.

A 15% Max HP self-hit at the median of 132 HP is about 21 field-index points (`0.15 × 132 / 0.925`).

| Move | Snapshot | CHANGE | REMOVE | IMPLEMENT |
|---|---|---|---|---|
| All-In | Power 98, accuracy 95, AP 0, index 93.10. No spend-all, no recoil, no KO refund. Balloom flagship, initial, and off-type. Largest index, free | Delete the three unimplemented sentences. Set a real cost and a power that lands on the table, for example power 76 at 3 AP (index about 72, the typed-common 3 AP center). Do not set the cost to 6 just because 93 is near the 6 AP center. That would let the unpaid number become the anchor | Balloom's only common damage is All-In, Volatile Shot, and Recoil Strike. Removing All-In without a replacement common damaging move, and without a decision on the other two, breaks the pool rule and removes the flagship. Forkrook does not learn it | Spend-all with a floor of 3 AP, 15% Max HP recoil on hit or miss (about 21 points), +1 AP on KO. Net value is about 93 − 21 = 72, paid with the whole bar. That is a fair committed hit if the spend-all actually runs. Do not also stamp a flat AP 6 on top of a working spend-all |
| Recoil Strike | Power 76, AP 2, index 72.20. The 15% recoil is not an HP-cost column and not a utility. Balloom learns it off-type. Forkrook does not | Power 64, delete the recoil sentence. It becomes the spine | Balloom still has All-In and Volatile Shot as common damage, both also non-anchor. Removing this one does not empty a species that has another honest common. It does remove one of the three cards Balloom is using as damage | Only if power stays 76. Net is about 72 − 21 = 51, under the spine, which is why the power is allowed to sit above the spine. Do not also raise AP from 2 to 4. That is the double tax |
| Volatile Shot | Power 72, AP 2, index 68.40. The 15% on a miss is not authored. Accuracy 95, so the miss is 5% and the expected self-hit is about 1 index point. Balloom initial | Power 64 and delete the sentence. One point of expected recoil does not pay for eight extra power | Same pool caution as Recoil Strike. Deghoul, the Gas species, does not learn this move | Implementing the miss cost alone does not pay for the power. If the miss cost is implemented, set power near 66 (index about 62.7) so the expected self-hit is in the same conversation as the premium. Do not also raise AP to 4 |
| Crystal Bash | Power 0, AP 2, accuracy 95. The MeleeDef override and the below-30% ×2 are in the columns. Power 0 returns before the override is read. Not in any of the 18 species pools | Set a nonzero power and keep the override. Power 48 is index 45.6, a typeless-sized hit when Defense equals Attack. A wall with a high Defense stat then matches the spine through the override, and the ×2 remains the execute. The issues master's "those columns do not exist" line is stale; the columns exist and the power is what zeros them | Shard still has Partition Shot as a common damaging move. Crystal Bash has no species pool row, so removal is catalog-only | "Damage equals the Defense stat" is engine work. The existing override swaps which stat multiplies power. It does not replace power. CHANGE and IMPLEMENT are the same card unless the designer wants literal Defense-as-power. Either way it stays off the template until power is nonzero |
| Cipher Shot, Ore Strike, Stake Strike, Precision Strike, Jackpot Shot | Spine clones. Index 60.80. The described rider is not a column. Cipher Shot is Keyper's only common damage and its initial. Stake Strike is Hopoch's only common damage and its initial. Ore Strike is a Kodiax initial, and Kodiax also has Solid Strike. Precision Strike is a Marrowhead off-type; Marrowhead's common damage is Partition Shot. Jackpot Shot is in no pool | Delete the sentence. Keep power 64. The card is the spine, and the text stops promising a rider | Do not remove Cipher Shot or Stake Strike unless that species gains another Common damaging move in the same change. Ore Strike, Precision Strike, and Jackpot Shot can leave without emptying a common-damage slot | Author the rider, and cut the base so the upside lands on a premium. A +25% or a double that is live on the current 64 makes a common into the best hit in the file. Jackpot's double / normal / half needs a real `RandomDamageMultipliers` list whose mean is 1.0 if power stays 64 |
| Yield Spike | Uncommon, index 60.80, CSV AP cost **2**. The issues master still says 3. The +25% and the extra spend at 4+ AP are not authored. Hopoch learnable | Delete the sentence. An uncommon at exactly the common spine is slightly under the 1.05 uncommon factor (target 63.84). Leaving power 64 at 2 AP is close enough that this note does not invent a new power while the sentence is a lie | Hopoch still has Stake Strike. Removal does not empty the common-damage slot. It does remove Hopoch's uncommon | A single gate, not two systems. Example: if the user has 4+ AP, deal the typed-uncommon premium and spend 1 extra. The current 60.80 becomes the floor and the gate is the uncommon. Do not also raise the printed cost |
| Hype Train | Rare, power 52, index 49.40, 2 AP. Consecutive-hit ramp is not a scaling column. No species pool | Delete the ramp sentence and set power to 72 (index 68.40, the typed-rare 2 AP center) | Catalog-only | A simple stack: +20% per consecutive hit with this move, cap +60%, miss resets. Keep the base at or under 60.80 so the stack is the rare. A cap of +100% on a 68 base is another All-In |
| Lucky Scatter, Glitch Barrage | Rares, indexes 42.75 and 45.60, 3 AP. The random buff and the random self-hit have no payload. No species pool | Delete the sentence and raise power onto the typed-rare 3 AP center (about 85 power at accuracy 95 is index 80.75, target 80.64) | Catalog-only | One verb that already exists. Lucky Scatter can apply one existing self-buff stage at 25% per hit. Glitch Barrage's random self-hit is the Volatile Shot problem again; prefer CHANGE unless the self-hit is priced like Recoil Strike |
| Cipher Burst | Rare, index 52.25, 3 AP. Per-hit buff erase has no payload. No species pool | Delete the sentence and raise onto the typed-rare 3 AP center, or leave the index and add the erase | Catalog-only. Keyper's burst is not this move; Keyper's common is Cipher Shot | Per-hit cleanse of one foe buff is a simple opcode if Cleanse already targets the foe. Cut power so the erase is the premium rather than a free add on 80 points of damage |
| Combustive Shift | Rare, index 41.80, 2 AP. The bonus versus a debuffed foe who is also missing AP is not a condition. No species pool | Delete the double condition and set power to 72. It becomes a plain Gas rare on the typed-rare 2 AP center | Catalog-only | One condition, not two. "+25% if the foe has at least one debuff" matches Momentum Drill's shape. The "and they are missing AP" clause is the niche mechanic this rule is trying to avoid |
| Deep Dig | Common, power 72, AP 3, index 68.40. The 50% Defense-stage ignore is not a damage-tuning flag. Kodiax learnable. Kodiax also has Ore Strike and Solid Strike | Delete the ignore sentence. 68.40 is already near the 3 AP center of 72. Do not raise the cost, and do not use this index as the template | Kodiax keeps Solid Strike as common damage | Add the ignore as a real damage flag. The neutral index stays. The ignore is upside against positive Defense. If playtests show it is worth more than a few points, cut power then. Do not raise AP in the same change |
| Shard Scatter | Uncommon spread, index 85.50, 3 AP. Per-hit Evasion drop on a different foe has no payload. No species pool. Target for a typed uncommon spread is about 86.94 | Leave the numbers. Delete the Eva sentence, or accept that the sentence is currently false | Catalog-only | If the Eva drop is added, cut power in the same change. The damage is already on the uncommon spread target. Adding the rider without a cut puts it over |
| Crash Routine, Bug Fix, Dice Roll, Recursive Backup | Support or utility. Part of the payload runs (an Attack drop, a 10% heal, a 20% heal, an immediate 20% heal). The other clause does not. Dice Roll's tooltip and the row disagree | Keep the clause that runs. Delete the clause that does not. Do not retune a damage curve off these | None of these four are in the 18 species pools, so removal is catalog-only | Implement the missing clause only when it is one existing opcode. Dice Roll's random heal / debuff / +2 AP branch is three cards. Prefer CHANGE |
| Risk Spike, Guard Stance | Attack +2 / Defense −1, and Defense +2, run and **stay**. The next-hit bonus, the recoil, and the contact-recoil window do not | Delete the missing clause. Then decide whether the stages are allowed to stick. The designer rule says a timed status ends with its stats. These stages are move stages, not a status, so they are permanent by the stage system. Say that in the text | Not in the 18 pools | The missing half is a new mechanic (a one-hit flag, a contact-recoil window). That is a separate call from the curve |
| Rally Cry, Refinery | The stage runs (ally Speed +1, Defense +1). The end-of-turn AP does not | Delete the AP sentence, or move the AP onto a `GainAP` that runs now | Not in the 18 pools | An end-of-turn AP rider needs a status or a delayed opcode that actually ticks. Until that exists, CHANGE is the honest card |
| Double or Nothing | The 60/40 AP roll is hardcoded in `CombatResolver`. The 40% drain-immunity is not. Not in the 18 pools | Delete the immunity sentence and keep the hardcoded roll, with the text matching the code | Catalog-only | Immunity is a new flag. It is the niche mechanic to avoid unless the designer specifically wants a drain-proof gamble |
| Ice Wall | The description comma is unquoted, so the row has more fields than the header. The importer stores an empty mitigation window. The issues master marks the comma fixed (E-16) and still has hit budget 0 (E-17). This snapshot has the comma bug again. Learned by Gilarime, Humpacked (off-type), and Blockwyrm | Quote the description so the row imports, and delete the mitigation sentence until someone picks a hit count. The Speed-based AP clause can stay only if its columns survive the quote | Blockwyrm, Gilarime, and Humpacked lose a wall slot. Blockwyrm still has no common damaging move either way, because Ice Wall is not damage | Pick a hit budget greater than 0. This note does not invent that count. E-17 still says not to. A budget of 0 is off, not "unlimited" |

## AP-economy interaction

Regen is +2 at the start of the turn, including turn 1, from a start of 0, cap 10, floor 0. The bench does not regen. Catalog costs are 0–3 only. The script's calendar is the first turn a holder can pay the cost. A self-boost spends turn 1's action.

| Cost | Pass only | AP Boost on turn 1 | Fuel Surge on turn 1 | Turn-start +1 | Router gifts +2 | MEV, two end drains | Memory Leak −2 on turn 2 | Drain 1 on turn 1 | Drain 2 on turn 1 | Drain 2 on turn 2 | Boost and MEV | Fuel and MEV |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 1 | 1 | 2 | 2 | 1 | 1 | 1 | 1 | 1 | 2 | 1 | 2 | 2 |
| 2 | 1 | 2 | 2 | 1 | 1 | 1 | 1 | 2 | 2 | 1 | 2 | 2 |
| 3 | 2 | 2 | 2 | 1 | 1 | 2 | 3 | 2 | 3 | 3 | 2 | 2 |
| 4 | 2 | 2 | 2 | 2 | 1 | 3 | 3 | 3 | 3 | 3 | 2 | 2 |
| 5 | 3 | 2 | 2 | 2 | 2 | 4 | 4 | 3 | 4 | 4 | 2 | 2 |
| 6 | 3 | 2 | 2 | 2 | 2 | 4 | 4 | 4 | 4 | 4 | 3 | 2 |

Reading the columns:

- Pass only: costs 1–2 on turn 1, 3–4 on turn 2, 5–6 on turn 3. Today's entire damage catalog is inside 0–3, so the slowest honest hit is one pass away.
- AP Boost (+2, cost 0, recharge 3) and Fuel Surge (+3, plus Gas Spike) occupy turn 1. Turn 2 opens at 6 after Boost and at 7 after Fuel. Costs 1 through 6 are legal on turn 2. The boost cannot also swing.
- A turn-start +1 makes cost 3 legal on turn 1. Router's gift of 2 is the effect that puts cost 4 on turn 1 for the ally, because the ally did not spend the action.
- MEV Extracted drains 1 at end of turn for two ticks. It does not stop a hit already paid. Against today's top band it is a **0-turn** delay: cost 3 is still turn 2. Cost 4 slips to turn 3. Cost 6 slips to turn 4.
- Memory Leak (−2 after the foe's next regen) slips cost 3 from turn 2 to turn 3. It is Encrypted, not Bot, and it is the denial that actually moves the current top band.
- An immediate drain of 1 on turn 1 (Transaction Tax's shape) slips the 2 AP spine to turn 2 and leaves cost 3 on turn 2. Tax is Gas, it works, and it is a better rate than typeless AP Drain (drain 1 at cost 2, plus a 25% self-gain).
- An immediate drain of 2 on turn 1 slips costs 1 and 2 to turn 2, and slips cost 3 to turn 3. That blanks the spine if the drainer wins Speed on the opening turn. The counterplay is acting first, so the drain hits a bar that was already spent, and the floor eats it.
- A drain of 2 on turn 2, after the holder has passed, slips cost 3 from turn 2 to turn 3 and slips cost 4 the same way. That is the first denial that delays today's top band **and** a future 4 AP hit, and only if it lands on the bank turn before the cast.
- Boost plus MEV: cost 6 moves from turn 2 to turn 3. They cancel on the 6 AP band. Cost 4 stays on turn 2, because a +2 boost minus two drains of 1 still opens turn 2 at 5. Fuel plus MEV still fires a 6 on turn 2. One point of denial does not unwind Fuel Surge.
- Thread Lock, as shipped, matches the pass-only column. Duration 1 expires at the end of the turn it was applied, so the next regen is not blocked. The sentence on the card is ahead of the clock.

**PROPOSAL** on the interaction, not a second retune of the same powers: the curve above is what makes the designer rule true. A 4 AP hit (the new Shard concept below) is turn 2 on a pass and turn 1 only with a gift. A 6 AP hit, if one is added later, is turn 3, or turn 2 after a boost, and MEV pushes that boosted 6 back to turn 3. Today's 3 AP ceiling cannot host that cancellation. Bot still needs a drain in its own pool before any of this is a Bot plan. Kitsauto does not learn MEV Extract or Thread Lock.

Streamlined Flow's 1 AP discount while HP is above 70% still makes a 3 into a 2 on turn 1. That is the clean "fire the heavy hit early" tool, and it is Compressed, not a boost. This note does not retune it. It does mean a 3 AP "turn 2" card is a turn 1 card on that species while healthy.

## New concepts

**PROPOSAL.** Four ids. None of them are in the CSV. The script fails if one appears. They are moves, not passives, so the "new passive must not reuse a move name" rule is the constraint on any later passive: do not name a passive Cut Ledger, Batch Debit, Rime Jab, or Vested Strike. They are not the gap-list concepts from the type-gap note (`brace_yield`, `ping_tax`, and the rest of that C/D list) and not the pack concepts (`fee_tap`, `cipher_skim`, `gas_vent`, `anchor_tap`). Meme gets nothing, and no chemistry cell.

### 1. Cut Ledger — Shard, Rare, 4 AP

Power 96, accuracy 95, one hit, single target, no rider. Field index 91.20. Typed-rare 4 AP target is 91.84.

This is the empty band. It is the first honest high-damage hit that a pass cannot fire on turn 1. Turn 2 after a pass, or turn 1 if a partner gifts the AP. A drain of 2 on the bank turn slips it to turn 3. No extra sentence. Shard is the surgical aggressor in the type profile, and a single expensive cut matches that better than another multi-hit under the spine.

### 2. Batch Debit — Bot, Common, 2 AP

`DrainAP:2` on one foe. No damage. No priority.

Bot has no drain. Ping Tax and Fee Tap, from earlier notes, are drain 1 for 1 AP. This is a different amount: 2 AP removed, for 2 AP spent, which is enough to slip a 3 AP cast when Bot wins Speed on the turn the foe banked (turn 2 opens at 4, drain leaves 2). It also, if Bot wins Speed on turn 1, blanks the 2 AP spine (2 to 0) and slips cost 3 to turn 3. The floor is the counterplay: a foe who spends before the drain loses nothing. Whether 2 is the right amount is a designer question. If the answer is no, the card becomes drain 1 at 1 AP, which is the earlier Ping Tax shape, and Bot still needs it in Kitsauto's pool.

**PROPOSAL** for the pool, separate from the amount: Kitsauto should be able to learn a Bot drain. The current five-move pool cannot express the type.

### 3. Rime Jab — Frozen, Common, 1 AP

Power 30, accuracy 95, 25% Speed −1. Index 28.50 against the 1 AP center of 28.

After Rapid Strike and Lean Hit come down, the 1 AP damage band is Quick Jab and two trimmed chips. Frozen's profile is Speed. A turn-1 jab with a small Speed nick gives the type a common chip that is not Frost Shot and not a 3 AP multi-hit that loses to Frost Shot. It works on turn 1.

### 4. Vested Strike — Staked, Uncommon, 2 AP

Power 72, accuracy 95, recharge 2. Index 68.40 per cast. The typed-uncommon 2 AP target is 63.84, so the hit is a bit hot on the turn it lands and then the user skips a turn.

This is the first damaging move with a recharge. It is only a fair card if recharge ticks on the bench. If the locked test stays (bench pauses the clock), the player benches the Chainling and the lockout waits, and the card is a 68.40 hit at 2 AP with the downside avoided. **PROPOSAL:** add it only together with bench recharge that ticks. If the designer keeps the pause, do not add this move. Staked's profile is patience. A hit you cannot repeat next turn is that profile, and it gives Staked an uncommon that is not Yield Spike's missing rider.

No new Meme move. All-In remains the Meme question in the conditional table.

## Open questions

Only the designer can close these. The curve above is built so that a "no" on any one of them does not collapse the others.

1. Does a fresh 0-versus-0 Defense-versus-Attack tie count as "Defense is at least Attack" for Rallying Slam and Interest Strike, or does the refund wait until Defense is at least +1?
2. Is a Bot drain of 2 acceptable, given a Speed win on turn 1 blanks the 2 AP spine and slips a 3 AP cast to turn 3? If not, is drain 1 the amount, with Batch Debit rewritten to that?
3. Bench recharge: follow the designer rule (it ticks) or the locked test (it pauses)? Vested Strike depends on the tick.
4. Fork Split stays at 3 AP on this curve (about 5% over the spread target). The AP note moves it to 5. Which cost is the card?
5. Is Phantom Hack's guaranteed turn-1 Defense −1 and Speed −1, on a 60.80 hit (EV 85.50), the encrypted rare, or too much?
6. For All-In, Recoil Strike, Volatile Shot, and Crystal Bash, which call is it: change, remove, or implement? The same question, in batch, for the rest of the conditional table. Those rows stay off the template until the call exists.
7. `COMBAT_Moves.txt` §6.1 says rarity is complexity, not power. This screen follows the developer rule (typed beats typeless; a rare typeless lines up with a typed common). Edit the spec, or keep the screen on the developer rule and leave §6.1 for a separate decision?
8. Gas Spike and FUD stages stick in this snapshot, and the issues master calls that fixed. Revert them with the status, or accept the stick?
9. Rattlestake, Cofi Lootwak, and Blockwyrm still have no Common damaging move. Balloom's three commons are the three unpaid cards. If any of those three is removed, what is Balloom's common damage?
10. Interest Strike's refund: once per cast, as proposed here, or per confirmed hit, as the local row does now?

## Test and verification

Companion script, stdlib only: `move_cost_curve_check.py`, saved next to this note. It does not modify the snapshot.

```
python3 move_cost_curve_check.py /path/to/snapshot
```

The default snapshot path is `/tmp/chainbound_snapshot`, or `CHAINBOUND_SNAPSHOT`. The run below used the extracted 2026-10-17 snapshot. Exit code 0. The script checks the reconciled indexes, the move counts (208 and 72), that no proposal id is a non-anchor row, that the four new concept ids are absent, and that the AP calendar stays on the turns in the table above (pass-only 3 → turn 2 and 6 → turn 3, boost 6 → turn 2, MEV 3 → turn 2 and 4 → turn 3, Fuel plus MEV still fires 6 on turn 2, drain 2 on turn 2 slips cost 3 to turn 3). After the proposals, every anchor row more than 15% off the raw-index target is on the accepted list. An unexplained row fails the run.

Limits of the check, stated plainly: rider points are the schedule in the method, not a second damage formula. The spread target of 1.15 is a proposal; the field index still uses ×2. The AP table is one holder. Bench recharge and the Gas Spike / FUD stick are read from the resolver and the status whitelist, not replayed as a battle. Ice Wall's comma is detected because the CSV row has more fields than the header, which matches the imported asset's empty window.

Output of the passing run:

```
Chainbound move cost curve check
snapshot: /tmp/chainbound_snapshot
moves: /tmp/chainbound_snapshot/Docs/Specs/Combat/Data/Moves.csv
rows 208  damaging 72

Field index = power * ((hitMin+hitMax)/2) * (accuracy/100 if accuracy>0 else 1) * (2 if AllEnemies or Field else 1).
Resolved = field index * neutral conditional/random factor. Power 0 stays 0.
Rider credits are neutral-turn only, and only for payloads the resolver executes.
EV = resolved + rider. Priority credit is 10 and is inside the rider, not the field index.
Non-anchor rows are excluded from medians and from the proposal table.

== Cost census (all non-passive rows) ==
  passive: 31
  0: 6
  1: 52
  2: 82
  3: 37
  4: 0
  5: 0
  6: 0

== Damaging field-index medians ==
  Anchor-only excludes unimplemented-rider rows and AP 0.
  Inclusive is every damaging row with AP > 0, which is the prior audit's screen.
  Common: anchor n=31 median/AP 17.73   inclusive n=39 median/AP 24.07
  Uncommon: anchor n=4 median/AP 19.95   inclusive n=6 median/AP 26.60
  Rare: anchor n=19 median/AP 18.00   inclusive n=25 median/AP 17.42
  Epic: anchor n=1 median/AP 15.00   inclusive n=1 median/AP 15.00

== By type, damaging, anchor only, median field index / per AP ==
  Block        n= 4  median index  52.90  median per AP  22.70
  Bot          n= 3  median index  51.30  median per AP  24.07
  Burned       n= 5  median index  60.80  median per AP  24.07
  Compressed   n= 3  median index  43.70  median per AP  24.70
  Encrypted    n= 1  median index  60.80  median per AP  30.40
  Forked       n= 3  median index  49.40  median per AP  29.13
  Frozen       n= 3  median index  60.80  median per AP  30.40
  Gas          n= 1  median index  53.20  median per AP  17.73
  Glitched     n= 3  median index  60.80  median per AP  30.40
  Mined        n= 3  median index  60.80  median per AP  30.40
  Node         n= 2  median index  64.60  median per AP  32.30
  None         n=20  median index  31.05  median per AP  14.09
  Shard        n= 2  median index  55.10  median per AP  27.55
  Staked       n= 2  median index  32.77  median per AP  12.82

== 2 AP single typed commons at the spine ==
  count 13
  coordinated_shot       Node         anchor       EV  60.80  no live rider
  solid_strike           Block        anchor       EV  62.70  stage Speed +1.9
  cipher_shot            Encrypted    NON-ANCHOR   EV  60.80  no live rider
  scorch_shot            Burned       anchor       EV  65.08  stage MeleeDef +4.3
  frost_shot             Frozen       anchor       EV  62.70  stage Speed +1.9
  momentum_drill         Mined        anchor       EV  60.80  no live rider
  ore_strike             Mined        NON-ANCHOR   EV  60.80  no live rider
  stake_strike           Staked       NON-ANCHOR   EV  60.80  no live rider
  precision_strike       Compressed   NON-ANCHOR   EV  60.80  no live rider
  chaos_strike           Glitched     anchor       EV  68.40  no live rider
  bug_payoff             Glitched     anchor       EV  60.80  no live rider
  partition_shot         Shard        anchor       EV  72.20  status FrozenAssets +11.4
  jackpot_shot           Meme         NON-ANCHOR   EV  60.80  no live rider

== Damaging curve, field index, by AP ==
AP Id                     Rar       Type         Shape   Idx     Res     EV      /AP     Tgt     Anchor
 0 all_in                 Common    Meme         single    93.10   93.10   93.10    free    0.00 no
 1 rapid_strike           Common    Forked       single    49.40   49.40   59.40   49.40   28.00 yes
 1 lean_hit               Common    Compressed   single    43.70   43.70   43.70   43.70   28.00 yes
 1 baseline_jab           Common    None         single    15.20   15.20   25.20   15.20   21.00 yes
 2 recoil_strike          Common    Forked       single    72.20   72.20   72.20   36.10   60.80 no
 2 cryo_ray               Rare      Frozen       single    68.40   68.40   72.20   34.20   68.10 yes
 2 hammer_sync            Rare      Node         single    68.40   68.40   68.40   34.20   68.10 yes
 2 volatile_shot          Common    Gas          single    68.40   68.40   68.40   34.20   60.80 no
 2 bug_payoff             Common    Glitched     single    60.80   60.80   60.80   30.40   60.80 yes
 2 chaos_strike           Common    Glitched     single    60.80   68.40   68.40   30.40   60.80 yes
 2 cipher_shot            Common    Encrypted    single    60.80   60.80   60.80   30.40   60.80 no
 2 coordinated_shot       Common    Node         single    60.80   60.80   60.80   30.40   60.80 yes
 2 deep_reap              Rare      Mined        single    60.80   60.80   60.80   30.40   68.10 yes
 2 flame_charge           Uncommon  Burned       single    60.80   60.80   60.80   30.40   63.84 yes
 2 frost_shot             Common    Frozen       single    60.80   60.80   62.70   30.40   60.80 yes
 2 jackpot_shot           Common    Meme         single    60.80   60.80   60.80   30.40   60.80 no
 2 momentum_drill         Common    Mined        single    60.80   60.80   60.80   30.40   60.80 yes
 2 ore_strike             Common    Mined        single    60.80   60.80   60.80   30.40   60.80 no
 2 partition_shot         Common    Shard        single    60.80   60.80   72.20   30.40   60.80 yes
 2 phantom_hack           Rare      Encrypted    single    60.80   60.80   85.50   30.40   68.10 yes
 2 precision_strike       Common    Compressed   single    60.80   60.80   60.80   30.40   60.80 no
 2 rallying_slam          Rare      Block        single    60.80   60.80   79.80   30.40   68.10 yes
 2 scorch_shot            Common    Burned       single    60.80   60.80   65.08   30.40   60.80 yes
 2 solid_strike           Common    Block        single    60.80   60.80   62.70   30.40   60.80 yes
 2 stake_strike           Common    Staked       single    60.80   60.80   60.80   30.40   60.80 no
 2 yield_spike            Uncommon  Staked       single    60.80   60.80   60.80   30.40   63.84 no
 2 blade_flurry           Rare      Bot          single    51.30   51.30   51.30   25.65   68.10 yes
 2 hype_train             Rare      Meme         single    49.40   49.40   49.40   24.70   68.10 no
 2 seam_cut               Rare      Shard        single    49.40   49.40   49.40   24.70   68.10 yes
 2 zipped_strikes         Uncommon  Compressed   single    49.40   49.40   53.20   24.70   63.84 yes
 2 baseline_barrage       Common    None         spread    47.50   47.50   47.50   23.75   52.44 yes
 2 combustive_shift       Rare      Gas          single    41.80   41.80   41.80   20.90   68.10 no
 2 refined_volley         Rare      None         spread    39.60   39.60   39.60   19.80   69.92 yes
 2 refined_striker        Rare      None         single    37.80   37.80   37.80   18.90   60.80 yes
 2 refined_shot           Rare      None         single    34.20   34.20   34.20   17.10   60.80 yes
 2 refined_thrust         Rare      None         single    30.60   30.60   34.65   15.30   60.80 yes
 2 baseline_volley        Common    None         spread    30.40   30.40   30.40   15.20   52.44 yes
 2 baseline_striker       Common    None         single    28.50   28.50   28.50   14.25   45.60 yes
 2 multi_thread_shot      Common    Bot          single    28.50   28.50   33.25   14.25   60.80 yes
 2 baseline_shot          Common    None         single    24.70   24.70   24.70   12.35   45.60 yes
 2 baseline_thrust        Common    None         single    24.70   24.70   28.97   12.35   45.60 yes
 2 quick_lunge            Rare      None         single    23.40   23.40   33.40   11.70   60.80 yes
 2 civet_beam             Uncommon  Staked       spread    22.80   22.80   38.00   11.40   73.42 yes
 2 crystal_bash           Rare      Shard        single     0.00    0.00    0.00    0.00   68.10 no
 3 fork_split             Common    Forked       spread    87.40   87.40   87.40   29.13   82.80 yes
 3 shard_scatter          Uncommon  Shard        spread    85.50   85.50   85.50   28.50   86.94 no
 3 automation_burst       Common    Bot          spread    72.20   72.20   76.00   24.07   82.80 yes
 3 ember_shower           Common    Burned       spread    72.20   72.20   80.75   24.07   82.80 yes
 3 deep_dig               Common    Mined        single    68.40   68.40   68.40   22.80   72.00 no
 3 finish_burn            Common    Burned       single    60.80   60.80   60.80   20.27   72.00 yes
 3 refined_barrage        Rare      None         spread    54.00   54.00   54.00   18.00   82.80 yes
 3 ap_siphon              Common    Gas          single    53.20   53.20   58.90   17.73   72.00 yes
 3 cipher_burst           Rare      Encrypted    single    52.25   52.25   52.25   17.42   80.64 no
 3 cinder_burst           Rare      Burned       single    51.30   51.30   61.56   17.10   80.64 yes
 3 refined_blast          Rare      None         spread    50.40   50.40   50.40   16.80   82.80 yes
 3 chaotic_resonance      Common    Glitched     single    49.40   49.40   49.40   16.47   72.00 yes
 3 glitch_barrage         Rare      Glitched     single    45.60   45.60   45.60   15.20   80.64 no
 3 pickaxe_flurry         Uncommon  Mined        single    45.60   45.60   81.60   15.20   75.60 yes
 3 bulwark_blow           Epic      Block        single    45.00   45.00   45.00   15.00   72.00 yes
 3 interest_strike        Rare      Staked       single    42.75   42.75   90.25   14.25   80.64 yes
 3 lucky_scatter          Rare      Meme         single    42.75   42.75   42.75   14.25   80.64 no
 3 baseline_blast         Common    None         spread    41.80   41.80   41.80   13.93   62.10 yes
 3 compression_burst      Common    Compressed   single    41.80   41.80   76.00   13.93   72.00 yes
 3 overdraw_snipe         Rare      None         single    39.10   39.10   39.10   13.03   72.00 yes
 3 refined_slam           Rare      None         single    38.40   38.40   38.40   12.80   72.00 yes
 3 stone_barrage          Common    Block        single    38.00   38.00   41.80   12.67   72.00 yes
 3 chain_fork             Common    Forked       single    34.20   34.20   34.20   11.40   72.00 yes
 3 frost_shards           Common    Frozen       single    34.20   34.20   38.76   11.40   72.00 yes
 3 guardbreaker_combo     Rare      None         single    31.50   31.50   31.50   10.50   72.00 yes
 3 baseline_snipe         Common    None         single    27.00   27.00   27.00    9.00   54.00 yes
 3 baseline_multi_strike  Common    None         single    26.60   26.60   26.60    8.87   54.00 yes
 3 baseline_slam          Common    None         single    25.50   25.50   25.50    8.50   54.00 yes

== Outliers ==
-- Free or near-free high value (AP 0, damaging, field index > 20) --
  all_in index 93.10 anchor=False
-- Overtuned vs target (anchor, resolved > target * 1.15) --
  rapid_strike           resolved   49.40  target   28.00  AP 1
  lean_hit               resolved   43.70  target   28.00  AP 1
-- Undertuned vs target (anchor, resolved < target * 0.85, AP > 0) --
  baseline_jab           resolved   15.20  target   21.00  AP 1 Common None
  civet_beam             resolved   22.80  target   73.42  AP 2 Uncommon Staked
  quick_lunge            resolved   23.40  target   60.80  AP 2 Rare None
  baseline_shot          resolved   24.70  target   45.60  AP 2 Common None
  baseline_thrust        resolved   24.70  target   45.60  AP 2 Common None
  baseline_slam          resolved   25.50  target   54.00  AP 3 Common None
  baseline_multi_strike  resolved   26.60  target   54.00  AP 3 Common None
  baseline_snipe         resolved   27.00  target   54.00  AP 3 Common None
  baseline_striker       resolved   28.50  target   45.60  AP 2 Common None
  multi_thread_shot      resolved   28.50  target   60.80  AP 2 Common Bot
  baseline_volley        resolved   30.40  target   52.44  AP 2 Common None
  refined_thrust         resolved   30.60  target   60.80  AP 2 Rare None
  guardbreaker_combo     resolved   31.50  target   72.00  AP 3 Rare None
  frost_shards           resolved   34.20  target   72.00  AP 3 Common Frozen
  chain_fork             resolved   34.20  target   72.00  AP 3 Common Forked
  refined_shot           resolved   34.20  target   60.80  AP 2 Rare None
  refined_striker        resolved   37.80  target   60.80  AP 2 Rare None
  stone_barrage          resolved   38.00  target   72.00  AP 3 Common Block
  refined_slam           resolved   38.40  target   72.00  AP 3 Rare None
  overdraw_snipe         resolved   39.10  target   72.00  AP 3 Rare None
  refined_volley         resolved   39.60  target   69.92  AP 2 Rare None
  baseline_blast         resolved   41.80  target   62.10  AP 3 Common None
  compression_burst      resolved   41.80  target   72.00  AP 3 Common Compressed
  interest_strike        resolved   42.75  target   80.64  AP 3 Rare Staked
  bulwark_blow           resolved   45.00  target   72.00  AP 3 Epic Block
  pickaxe_flurry         resolved   45.60  target   75.60  AP 3 Uncommon Mined
  zipped_strikes         resolved   49.40  target   63.84  AP 2 Uncommon Compressed
  chaotic_resonance      resolved   49.40  target   72.00  AP 3 Common Glitched
  seam_cut               resolved   49.40  target   68.10  AP 2 Rare Shard
  refined_blast          resolved   50.40  target   82.80  AP 3 Rare None
  cinder_burst           resolved   51.30  target   80.64  AP 3 Rare Burned
  blade_flurry           resolved   51.30  target   68.10  AP 2 Rare Bot
  ap_siphon              resolved   53.20  target   72.00  AP 3 Common Gas
  refined_barrage        resolved   54.00  target   82.80  AP 3 Rare None
  finish_burn            resolved   60.80  target   72.00  AP 3 Common Burned
-- Higher rarity loses the field index to an anchor Common, same AP and shape --
   Non-anchor Commons (recoil_strike, volatile_shot, deep_dig) are not the rival.
  refined_striker        Rare        37.80  loses to coordinated_shot 60.80  anchor=True
  refined_slam           Rare        38.40  loses to finish_burn 60.80  anchor=True
  quick_lunge            Rare        23.40  loses to coordinated_shot 60.80  anchor=True
  refined_thrust         Rare        30.60  loses to coordinated_shot 60.80  anchor=True
  guardbreaker_combo     Rare        31.50  loses to finish_burn 60.80  anchor=True
  refined_shot           Rare        34.20  loses to coordinated_shot 60.80  anchor=True
  overdraw_snipe         Rare        39.10  loses to finish_burn 60.80  anchor=True
  refined_volley         Rare        39.60  loses to baseline_barrage 47.50  anchor=True
  refined_blast          Rare        50.40  loses to fork_split 87.40  anchor=True
  refined_barrage        Rare        54.00  loses to fork_split 87.40  anchor=True
  combustive_shift       Rare        41.80  loses to coordinated_shot 60.80  anchor=False
  cipher_burst           Rare        52.25  loses to finish_burn 60.80  anchor=False
  cinder_burst           Rare        51.30  loses to finish_burn 60.80  anchor=True
  pickaxe_flurry         Uncommon    45.60  loses to finish_burn 60.80  anchor=True
  interest_strike        Rare        42.75  loses to finish_burn 60.80  anchor=True
  zipped_strikes         Uncommon    49.40  loses to coordinated_shot 60.80  anchor=True
  glitch_barrage         Rare        45.60  loses to finish_burn 60.80  anchor=False
  crystal_bash           Rare         0.00  loses to coordinated_shot 60.80  anchor=False
  shard_scatter          Uncommon    85.50  loses to fork_split 87.40  anchor=False
  hype_train             Rare        49.40  loses to coordinated_shot 60.80  anchor=False
  lucky_scatter          Rare        42.75  loses to finish_burn 60.80  anchor=False
  blade_flurry           Rare        51.30  loses to coordinated_shot 60.80  anchor=True
  civet_beam             Uncommon    22.80  loses to baseline_barrage 47.50  anchor=True
  bulwark_blow           Epic        45.00  loses to finish_burn 60.80  anchor=True
  seam_cut               Rare        49.40  loses to coordinated_shot 60.80  anchor=True
-- Typed anchor at or under the best typeless Common of the same AP and shape --
  multi_thread_shot        28.50  vs typeless common baseline_striker 28.50  AP 2 single
  civet_beam               22.80  vs typeless common baseline_barrage 47.50  AP 2 spread
-- Dead clicks: anchor move under a same-type anchor move that costs less AP --
  baseline_slam          AP 3   25.50  under refined_striker AP 2 37.80
  baseline_snipe         AP 3   27.00  under refined_striker AP 2 37.80
  baseline_blast         AP 3   41.80  under baseline_barrage AP 2 47.50
  baseline_multi_strike  AP 3   26.60  under refined_striker AP 2 37.80
  guardbreaker_combo     AP 3   31.50  under refined_striker AP 2 37.80
  stone_barrage          AP 3   38.00  under solid_strike AP 2 60.80
  cinder_burst           AP 3   51.30  under scorch_shot AP 2 60.80
  frost_shards           AP 3   34.20  under cryo_ray AP 2 68.40
  chain_fork             AP 3   34.20  under rapid_strike AP 1 49.40
  pickaxe_flurry         AP 3   45.60  under momentum_drill AP 2 60.80
  compression_burst      AP 3   41.80  under zipped_strikes AP 2 49.40
  chaotic_resonance      AP 3   49.40  under chaos_strike AP 2 60.80
  bulwark_blow           AP 3   45.00  under solid_strike AP 2 60.80

== Issues-master promises vs this snapshot ==
  all_in                 still open, do not anchor
    Spend-all, recoil, and KO refund have no opcode. AP column is 0.
  recoil_strike          still open, do not anchor
    15% MaxHP recoil is not an HP-cost column and not a utility.
  volatile_shot          still open, do not anchor
    15% MaxHP on a miss is not authored.
  crystal_bash           still open, do not anchor
    Def override and the <30% double are in the columns, but power 0 returns 0 before the attack stat is read.
  jackpot_shot           still open, do not anchor
    Double / normal / half is not a RandomDamageMultipliers list.
  hype_train             still open, do not anchor
    Consecutive-hit ramp is not a scaling column.
  lucky_scatter          still open, do not anchor
    25% random buff per hit has no utility payload.
  glitch_barrage         still open, do not anchor
    Random self-hit has no payload.
  cipher_shot            still open, do not anchor
    25% buff-block has no payload.
  cipher_burst           still open, do not anchor
    Per-hit buff erase has no payload.
  ore_strike             still open, do not anchor
    +10% per own buff is not a scaling column.
  stake_strike           still open, do not anchor
    +10% per own Def stage is not a scaling column.
  yield_spike            still open, do not anchor
    +25% and the extra AP spend at 4+ AP are not authored. CSV AP cost is 2.
  deep_dig               still open, do not anchor
    50% Def-stage ignore is not a damage-tuning flag.
  combustive_shift       still open, do not anchor
    Bonus versus a debuffed foe who is missing AP is not a condition.
  precision_strike       still open, do not anchor
    +20 accuracy is not an accuracy-bonus column.
  shard_scatter          still open, do not anchor
    Per-hit Evasion drop on a different foe has no payload.
  crash_routine          still open, do not anchor
    The below-50% HP damage branch is not authored. The Atk drop is.
  bug_fix                still open, do not anchor
    The 10% heal runs. Cleanse and the next-turn damage cut do not.
  dice_roll              still open, do not anchor
    The 20% heal runs. The random heal / debuff / +2 AP branch does not.
  recursive_backup       still open, do not anchor
    The immediate 20% heal can run. The end-of-turn heal and duration reset do not.
  risk_spike             still open, do not anchor
    Atk +2 and Def -1 run. The next-hit bonus and recoil do not.
  guard_stance           still open, do not anchor
    Def +2 runs and stays. The 3-turn contact recoil does not.
  rally_cry              still open, do not anchor
    Ally Speed +1 runs. The end-of-turn +1 AP does not.
  refinery               still open, do not anchor
    Def +1 runs and stays. The end-of-turn AP does not.
  double_or_nothing      still open, do not anchor
    The 60/40 AP roll is hardcoded. The 40% drain-immunity is not.
  ice_wall               still open, do not anchor
    The description comma is unquoted, so the importer stores an empty mitigation window.

== Proposal table (PROPOSAL, not applied) ==
Id                     Now                    Proposed               Idx     New     EV      NewEV   Reason
rapid_strike           p52/a95/ap1            p36/a95/ap1              49.40   34.20   59.40   44.20 1 AP priority finisher sits at the top of the 1 AP band, not on the 2 AP spine.
lean_hit               p46/a95/ap1            p34/a95/ap1              43.70   32.30   43.70   32.30 Compressed efficiency stays the best non-priority 1 AP hit, under the band cap.
baseline_jab           p16/a95/ap1            p22/a95/ap1              15.20   20.90   25.20   30.90 Typeless 1 AP chip lands on 0.75 of the 1 AP center. Priority stays extra.
chaos_strike           p64/a95/ap2            p64/a95/ap2/rand1.00     60.80   60.80   68.40   60.80 Keep the high/low swing. The mean returns to the 2 AP spine. Proposed list 1.35|0.65.
refined_striker        p42/a90/ap2            p68/a90/ap2              37.80   61.20   37.80   61.20 Typeless rare single lines up with the typed common spine. Accuracy 90 is the reliability tax.
refined_shot           p38/a90/ap2            p68/a90/ap2              34.20   61.20   34.20   61.20 Same typeless-rare line as Impact Knuckle.
refined_thrust         p34/a90/ap2            p62/a90/ap2              30.60   55.80   34.65   59.85 A step under the plain rare because the 25% Def drop already runs.
baseline_striker       p30/a95/ap2            p48/a95/ap2              28.50   45.60   28.50   45.60 Typeless common 2 AP single at 0.75 of the spine.
baseline_shot          p26/a95/ap2            p48/a95/ap2              24.70   45.60   24.70   45.60 Same typeless common line. Contact is not a damage tax.
baseline_thrust        p26/a95/ap2            p44/a95/ap2              24.70   41.80   28.97   46.07 Slightly under Snap Strike because the 25% Def drop runs.
baseline_slam          p30/a85/ap3            p64/a85/ap3              25.50   54.40   25.50   54.40 3 AP typeless common center. Low accuracy stays the reckless identity.
baseline_snipe         p30/a90/ap3            p60/a90/ap3              27.00   54.00   27.00   54.00 3 AP typeless common center.
baseline_multi_strike  p8/a95/ap3             p16/a95/ap3              26.60   53.20   26.60   53.20 Same 3 AP typeless center. Hit spread stays 2-5.
guardbreaker_combo     p10/a90/ap3            p23/a90/ap3              31.50   72.45   31.50   72.45 Typeless rare 3 AP multi-hit lines up with the typed common 3 AP center.
overdraw_snipe         p46/a85/ap3            p85/a85/ap3              39.10   72.25   39.10   72.25 Typeless rare 3 AP center. Accuracy 85 stays the reckless tax.
refined_slam           p48/a80/ap3            p90/a80/ap3              38.40   72.00   38.40   72.00 Typeless rare 3 AP center. Accuracy 80 stays.
refined_volley         p22/a90/ap2            p39/a90/ap2              39.60   70.20   39.60   70.20 Typeless rare 2 AP spread lines up with a typed common spread at that AP.
refined_blast          p28/a90/ap3            p46/a90/ap3              50.40   82.80   50.40   82.80 Typeless rare 3 AP spread lines up with the typed common spread target.
refined_barrage        p12/a90/ap3            p18/a90/ap3              54.00   81.00   54.00   81.00 Same 3 AP rare spread target. Hit count stays 2-3.
baseline_volley        p16/a95/ap2            p28/a95/ap2              30.40   53.20   30.40   53.20 Typeless common 2 AP spread at 1.15 times the typeless single.
baseline_barrage       p10/a95/ap2            p11/a95/ap2              47.50   52.25   47.50   52.25 Same 2 AP typeless spread target. Multi-hit shape stays.
baseline_blast         p22/a95/ap3            p33/a95/ap3              41.80   62.70   41.80   62.70 Typeless common 3 AP spread target.
quick_lunge            p26/a90/ap2            p46/a90/ap2              23.40   41.40   33.40   51.40 Priority rare stays under the spine. The 2 AP cost is the price of a bigger first strike.
multi_thread_shot      p12/a95/ap2            p20/a95/ap2              28.50   47.50   33.25   52.25 Typed Bot chip must beat the typeless 2 AP line and stay under the typed spine.
seam_cut               p52/a95/ap2            p64/a95/ap2              49.40   60.80   49.40   60.80 Neutral hit matches the spine. The speed check is upside, not the only way the rare ties a common.
ap_siphon              p56/a95/ap3            p70/a95/ap3              53.20   66.50   58.90   72.20 3 AP Gas hit sits on the 3 AP center. The 50% drain stays a small rider.
cinder_burst           p18/a95/ap3            p24/a95/ap3              51.30   68.40   61.56   78.66 Typed rare 3 AP multi-hit. The 20% Atk drops stay.
blade_flurry           p18/a95/ap2            p24/a95/ap2              51.30   68.40   51.30   68.40 Typed rare 2 AP center. Bot's damage exclusive, not a denial tool.
zipped_strikes         p26/a95/ap2            p32/a95/ap2              49.40   60.80   53.20   64.60 Typed uncommon 2 AP multi-hit on the spine. Speed procs stay upside.
frost_shards           p12/a95/ap3            p25/a95/ap3              34.20   71.25   38.76   75.81 Same-type 3 AP multi-hit was under Frost Shot. Raise power onto the 3 AP center instead of cutting the cost.
chain_fork             p12/a95/ap3            p25/a95/ap3              34.20   71.25   34.20   71.25 Forked's 3 AP multi-hit belongs on the 3 AP center, beside Fork Split rather than under Rapid Strike.
stone_barrage          p16/a95/ap3            p30/a95/ap3              38.00   71.25   41.80   75.05 Block's 3 AP multi-hit belongs on the 3 AP center, not under Solid Strike.
civet_beam             p12/a95/ap2            p12/a95/ap1              22.80   22.80   38.00   38.00 Weakest spread. At 1 AP it is a legal chip with a real Speed drop. This does not add a 6 AP beam.
bulwark_blow           p50/a90/ap3            p50/a90/ap2              45.00   45.00   45.00   45.00 Epic stays a Def-scaled hit, not a bigger Atk number. Cost 2 so a wall can press it on turn 1.
rallying_slam          p64/a95/ap2            p64/a95/ap2              60.80   60.80   79.80   60.80 Add Self Def stage >= 1, require all, beside the existing Def-versus-Atk check. A fresh tie at 0 currently pays +1 AP.
interest_strike        p18/a95/ap3            p30/a95/ap3              42.75   71.25   90.25   71.25 Pay the +1 AP once per cast, and only when Def stage is at least +1. Neutral damage then sits on the 3 AP center. The refund is the rare rider.

== After proposals, anchor rows more than 15% off the raw-index target ==
  accepted     quick_lunge            resolved   41.40 target   60.80 EV   51.40  Priority tax. The rare stays under the typeless-rare line.
  accepted     finish_burn            resolved   60.80 target   72.00 EV   60.80  Neutral is the 2 AP spine at 3 AP. The +50% below 40% HP is the card.
  accepted     cinder_burst           resolved   68.40 target   80.64 EV   78.66  The Atk procs put EV on the typed-rare 3 AP target.
  accepted     rapid_strike           resolved   34.20 target   28.00 EV   44.20  Top of the 1 AP band. Priority is the rest of the value.
  accepted     pickaxe_flurry         resolved   45.60 target   75.60 EV   81.60  Damage index is low. The live per-hit Atk ramp carries the EV.
  accepted     lean_hit               resolved   32.30 target   28.00 EV   32.30  Best non-priority 1 AP chip, above the center and under the 2 AP line.
  accepted     compression_burst      resolved   41.80 target   72.00 EV   76.00  Damage index is low. Both hits drop Def. E-21 is still open.
  accepted     multi_thread_shot      resolved   47.50 target   60.80 EV   52.25  Typed chip above the typeless line and under the spine.
  accepted     chaotic_resonance      resolved   49.40 target   72.00 EV   49.40  Neutral is the floor. The status-count cap is the card, so the base stays under the 3 AP center.
  accepted     civet_beam             resolved   22.80 target   33.81 EV   38.00  1 AP chip. The Speed drop on both foes is the value. No 6 AP beam.
  accepted     bulwark_blow           resolved   45.00 target   60.80 EV   45.00  Epic is Def substitution. The Atk index is not the target.

== AP calendar: first turn a cost is legal ==
cost        pass only  AP Boost turn 1 Fuel Surge turn     Turn-start +1 Router +2 to all MEV two end drai Memory Leak -2 t Drain 1 on turn  Drain 2 on turn  Drain 2 on turn  AP Boost and MEV     Fuel and MEV
   1                1                2                2                1                1                1                1                1                2                1                2                2
   2                1                2                2                1                1                1                1                2                2                1                2                2
   3                2                2                2                1                1                2                3                2                3                3                2                2
   4                2                2                2                2                1                3                3                3                3                3                2                2
   5                3                2                2                2                2                4                4                3                4                4                2                2
   6                3                2                2                2                2                4                4                4                4                4                3                2
Notes: pass-only turn 1 opens at 2. A self-boost spends that action.
MEV drains at end of turn, so it does not stop a hit already paid.
Thread Lock as shipped matches pass-only: it expires before the next regen.
Immediate drain 2 is not a current Bot move. It is the batch_debit calendar.

== Species pools missing a Common damaging move ==
  validape: baseline_striker
  gilarime: frost_shot, baseline_shot
  kitsauto: multi_thread_shot, baseline_jab
  kreechank: chaos_strike, bug_payoff
  cinislug: scorch_shot
  rattlestake: NONE in ['compound_guard', 'stake_heal', 'ap_reserve', 'yield_cycle', 'hibernate']
  humpacked: lean_hit
  crackett: baseline_jab
  keyper: cipher_shot
  deghoul: ap_siphon
  forkrook: fork_split, rapid_strike, baseline_shot
  cofi_lootwak: NONE in ['civet_beam', 'compound_guard', 'stake_heal', 'bean_sip']
  blockwyrm: NONE in ['bulwark_blow', 'immovable_stance', 'blockade', 'immutable_ledger', 'ice_wall']
  hopoch: stake_strike
  minefolk: baseline_shot
  marrowhead: partition_shot, precision_strike
  kodiax: deep_dig, ore_strike, solid_strike
  balloom: all_in, volatile_shot, recoil_strike

== Rule flags the script can see ==
  Meme chemistry: not in this script. Type profile and the prior chemistry round leave Meme empty. Keep it empty.
  GasSpike stages stick after expiry: True stages=[{'stat': 'MeleeAtk', 'stages': 2.0, 'apply_once': True}, {'stat': 'MeleeDef', 'stages': -2.0, 'apply_once': True}]
  FUD stages stick after expiry: True stages=[{'stat': 'MeleeAtk', 'stages': -1.0, 'apply_once': True}, {'stat': 'MeleeDef', 'stages': -1.0, 'apply_once': True}, {'stat': 'Speed', 'stages': -1.0, 'apply_once': True}]
  Bench recharge: CombatResolver.ProcessEndOfTurnForTeam walks active slots only.
  CombatRuntimeFixTests.BenchPausesStatusDurationUntilSwapIn locks recharge staying at 2 while benched.
  Designer rule says recharge ticks on the bench. This snapshot does the opposite.
  Bot moves with DrainAP or StealAP: none
  Ice Wall parse note: csv-comma-shift

== New concept indexes (not in the CSV) ==
  cut_ledger       Shard      Rare      AP 4 power 96 acc 95 index 91.20 target 91.84
  rime_jab         Frozen     Common    AP 1 power 30 acc 95 index 28.50 target 28.00
  vested_strike    Staked     Uncommon  AP 2 power 72 acc 95 index 68.40 target 63.84
  batch_debit     Bot        Common    AP 2  DrainAP 2  no damage index

ASSERTIONS PASSED
reconciled field indexes: all_in 93.10, solid_strike 60.80, fork_split 87.40, baseline_barrage 47.50, crystal_bash 0.00
```
