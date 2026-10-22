# AP economy analysis

Date: 2026-10-17. Research only. No move, modifier, status, pool, or engine value was changed.

Numbers come from the local working-tree snapshot (`Moves.csv`, `Modifiers.csv`, `Statuses.csv`, `SpeciesMovePools.csv`, `TestLoadouts.csv`, `_LEGEND.md`, `COMBAT_TypeProfiles.md`, `CombatResolver.cs`, `StatusProcessor.cs`, `CombatApService.cs`). Where that snapshot and this branch disagree, the snapshot wins. The branch still supplies everything the snapshot does not include: eligibility, the utility executor, AI, species types, chemistry matrix v1, and the combat audits. `Docs/GameTheory/` was not read and was not edited.

The field index below is the same screen as `Docs/Combat/Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md`: expected hits `(HitMin + HitMax) / 2`, accuracy factor `Accuracy / 100` (accuracy 0 would be a sure hit; every damaging row has accuracy above 0), times 2 when the target is `AllEnemies` or `Field`. Per AP divides that index by `APCost`. Attack, defense, chemistry, crits, and priority stay out of the index.

## What the three goals look like today

High-damage moves do not need a boost. The catalog tops out at 3 AP. A 2 AP move is legal on turn 1, and that band holds the power-64 typed commons (field index 60.80) plus several heavier hits (68–72) and a free 98-power All-In. A 3 AP move is legal on turn 2 after a pass, or on turn 1 if a partner hands over 2 AP.

Denial does not stop those hits. Drain and MEV Extracted resolve at end of turn or by 1 AP, after a 2 AP attack has already been paid. Thread Lock expires at the end of the turn it is applied, so the next turn's +2 regen is not blocked. The only Bot species, Kitsauto, does not learn Thread Lock or MEV Extract. Aggressive AI scores non-damage at 0.1 and will not press either button.

Boost and denial therefore do not meet. Boost mostly stocks AP above a ceiling of 3, which any banker already reaches on turn 2. Denial mostly taxes a bar the attacker has already emptied.

## 1. AP baseline

| Rule | Value | Where it lives |
|---|---:|---|
| Start AP | 0 | `ChainlingInstance.currentAP` defaults to 0. `CombatFactory` never writes a starting AP. |
| Regen | +2 at the start of each turn | `CombatResolverConfig_Default.asset` `apPerTurn: 2`, `CombatConstants.AP_PER_TURN`. `StatusProcessor.ProcessStartOfTurn` calls `Gain(..., "TurnRegen")`. |
| Cap | 10 | `apCap: 10`. `CombatApService.Gain` clamps to the cap and logs `APCapped` when the gain is cut. |
| Floor | 0 | `Drain` uses `Math.Max(0, currentAP - amount)`. A drain into an empty bar removes 0. |
| Who regens | Living active Chainlings | `ProcessStartOfTurnForTeam` walks active slots. The bench does not regen. |
| When regen happens | Before actions, including turn 1 | `ExecuteTurnWithAI` gains AP, then asks the AI for actions. The same order is in `ExecuteTurn2v2Internal`. |

Turn 1's action window is 2 AP. That matches the figure already used in `TYPE_GAP_PROPOSALS.md`.

Swap and KO replacement copy the active slot's current AP onto the Chainling coming in. The benched Chainling keeps a stale copy of the AP it left with, and that copy is overwritten the next time it enters the slot. Bench time does not grow a second AP pool.

### First turn each cost can be cast, with no boost

Assumptions: start at 0, gain +2 at the start of the turn, spend nothing on earlier turns, cap 10. "Castable" means the action window opens at that much AP.

| Turn | AP at the action | Costs that first become legal |
|---:|---:|---|
| 1 | 2 | 0, 1, 2 |
| 2 | 4 | 3, 4 |
| 3 | 6 | 5, 6 |
| 4 | 8 | 7, 8 |
| 5 | 10 | 9, 10 |
| 6 | 10 | Cap. Further regen is wasted while the bar stays full. |

Spending changes the calendar. A Chainling that spends 2 every turn opens every later turn at 2 and never casts a 3. Spending 1 on turn 1 leaves 1, and turn 2 opens at 3, so a 3 is still turn 2 and a 4 slips to turn 3.

Nothing in the catalog costs 4, 5, or 6. `CombatConstants` still documents a support ceiling of 4 and a damaging ceiling of 3. The data uses 0–3 only. The empty 4–6 rows are why a boost cannot "unlock the nuke early": the nuke is already on turn 1 or turn 2.

### Recharge, because it gates the boost button

`ap_boost` is authored at `rechargeTurns` 3. The status is created with 3 turns remaining and ticks down at end of turn. Cast on turn 1, it is locked on turns 2 and 3 and can be cast again on turn 4. The branch's player-facing helper reads that as two skipped turns (`rechargeTurns - 1`). The local resolver logs the raw 3.

The design rule is that recharge ticks every turn, including on the bench. The branch resolver calls `TickRechargeWhileBenched`. The local resolver's end-of-turn loop does not. A boost user who swaps out keeps the recharge frozen until they are active again.

## 2. Cost curve

208 move rows in the local `Moves.csv` (the branch has 206; the local file adds `uplink` and `kindling`). Non-passive costs:

| AP cost | Moves |
|---:|---:|
| 0 | 6 |
| 1 | 52 |
| 2 | 82 |
| 3 | 37 |
| 4–10 | 0 |

72 of those rows deal damage. The median per-AP index among damaging moves that cost more than 0 is about 24 for Commons, 27 for the six Uncommons, 18 for Rares, and 15 for the one Epic (`bulwark_blow`). The cluster a typed common actually lives on is tighter than that median: power 64, accuracy 95, one hit, 2 AP, field index 60.80, **30.40 per AP**.

### Scatter

Read down a column for one cost. The free row and the top of the 2 AP and 3 AP columns are the early high-damage problem. The bottom of the 2 AP and 3 AP columns is the overpriced problem. Crystal Bash shows 0.00 because its power column is 0; it attacks with MeleeDef, and this screen ignores that stat.

| AP | Move | Rarity | Type | Power | Acc | Hits | Target | Field index | Per AP |
|---:|---|---|---|---:|---:|---|---|---:|---:|
| 0 | All-In | Common | Meme | 98 | 95 | 1-1 | SingleEnemy | 93.10 | free |
| 1 | Rapid Strike | Common | Forked | 52 | 95 | 1-1 | SingleEnemy | 49.40 | 49.40 |
| 1 | Lean Hit | Common | Compressed | 46 | 95 | 1-1 | SingleEnemy | 43.70 | 43.70 |
| 1 | Quick Jab | Common | None | 16 | 95 | 1-1 | SingleEnemy | 15.20 | 15.20 |
| 2 | Recoil Strike | Common | Forked | 76 | 95 | 1-1 | SingleEnemy | 72.20 | 36.10 |
| 2 | Cryo Ray | Rare | Frozen | 72 | 95 | 1-1 | SingleEnemy | 68.40 | 34.20 |
| 2 | Hammer Sync | Rare | Node | 72 | 95 | 1-1 | SingleEnemy | 68.40 | 34.20 |
| 2 | Volatile Shot | Common | Gas | 72 | 95 | 1-1 | SingleEnemy | 68.40 | 34.20 |
| 2 | Bug Payoff | Common | Glitched | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Chaos Strike | Common | Glitched | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Cipher Shot | Common | Encrypted | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Coordinated Shot | Common | Node | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Deep Reap | Rare | Mined | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Flame Charge | Uncommon | Burned | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Frost Shot | Common | Frozen | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Jackpot Shot | Common | Meme | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Momentum Drill | Common | Mined | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Ore Strike | Common | Mined | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Partition Shot | Common | Shard | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Phantom Hack | Rare | Encrypted | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Precision Strike | Common | Compressed | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Rallying Slam | Rare | Block | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Scorch Shot | Common | Burned | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Solid Strike | Common | Block | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Stake Strike | Common | Staked | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Yield Spike | Uncommon | Staked | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 30.40 |
| 2 | Blade Flurry | Rare | Bot | 18 | 95 | 3-3 | SingleEnemy | 51.30 | 25.65 |
| 2 | Hype Train | Rare | Meme | 52 | 95 | 1-1 | SingleEnemy | 49.40 | 24.70 |
| 2 | Seam Cut | Rare | Shard | 52 | 95 | 1-1 | SingleEnemy | 49.40 | 24.70 |
| 2 | Zipped Strikes | Uncommon | Compressed | 26 | 95 | 2-2 | SingleEnemy | 49.40 | 24.70 |
| 2 | Pellet Storm | Common | None | 10 | 95 | 2-3 | AllEnemies | 47.50 | 23.75 |
| 2 | Combustive Shift | Rare | Gas | 22 | 95 | 2-2 | SingleEnemy | 41.80 | 20.90 |
| 2 | Scatterburst | Rare | None | 22 | 90 | 1-1 | AllEnemies | 39.60 | 19.80 |
| 2 | Impact Knuckle | Rare | None | 42 | 90 | 1-1 | SingleEnemy | 37.80 | 18.90 |
| 2 | Vector Shot | Rare | None | 38 | 90 | 1-1 | SingleEnemy | 34.20 | 17.10 |
| 2 | Rupture Point | Rare | None | 34 | 90 | 1-1 | SingleEnemy | 30.60 | 15.30 |
| 2 | Scatter Shot | Common | None | 16 | 95 | 1-1 | AllEnemies | 30.40 | 15.20 |
| 2 | Snap Strike | Common | None | 30 | 95 | 1-1 | SingleEnemy | 28.50 | 14.25 |
| 2 | Multi-Thread Shot | Common | Bot | 12 | 95 | 2-3 | SingleEnemy | 28.50 | 14.25 |
| 2 | Pulse Shot | Common | None | 26 | 95 | 1-1 | SingleEnemy | 24.70 | 12.35 |
| 2 | Pressure Point | Common | None | 26 | 95 | 1-1 | SingleEnemy | 24.70 | 12.35 |
| 2 | Blink Lunge | Rare | None | 26 | 90 | 1-1 | SingleEnemy | 23.40 | 11.70 |
| 2 | Bean Beam | Uncommon | Staked | 12 | 95 | 1-1 | AllEnemies | 22.80 | 11.40 |
| 2 | Crystal Bash | Rare | Shard | 0 | 95 | 1-1 | SingleEnemy | 0.00 | 0.00 |
| 3 | Fork Split | Common | Forked | 46 | 95 | 1-1 | AllEnemies | 87.40 | 29.13 |
| 3 | Shard Scatter | Uncommon | Shard | 18 | 95 | 2-3 | AllEnemies | 85.50 | 28.50 |
| 3 | Automation Burst | Common | Bot | 38 | 95 | 1-1 | AllEnemies | 72.20 | 24.07 |
| 3 | Ember Shower | Common | Burned | 38 | 95 | 1-1 | AllEnemies | 72.20 | 24.07 |
| 3 | Deep Dig | Common | Mined | 72 | 95 | 1-1 | SingleEnemy | 68.40 | 22.80 |
| 3 | Finish Burn | Common | Burned | 64 | 95 | 1-1 | SingleEnemy | 60.80 | 20.27 |
| 3 | Ricochet Storm | Rare | None | 12 | 90 | 2-3 | AllEnemies | 54.00 | 18.00 |
| 3 | AP Siphon | Common | Gas | 56 | 95 | 1-1 | SingleEnemy | 53.20 | 17.73 |
| 3 | Cipher Burst | Rare | Encrypted | 22 | 95 | 2-3 | SingleEnemy | 52.25 | 17.42 |
| 3 | Cinder Burst | Rare | Burned | 18 | 95 | 2-4 | SingleEnemy | 51.30 | 17.10 |
| 3 | Kinetic Burst | Rare | None | 28 | 90 | 1-1 | AllEnemies | 50.40 | 16.80 |
| 3 | Chaotic Resonance | Common | Glitched | 52 | 95 | 1-1 | SingleEnemy | 49.40 | 16.47 |
| 3 | Glitch Barrage | Rare | Glitched | 16 | 95 | 2-4 | SingleEnemy | 45.60 | 15.20 |
| 3 | Pickaxe Flurry | Uncommon | Mined | 16 | 95 | 2-4 | SingleEnemy | 45.60 | 15.20 |
| 3 | Bulwark Blow | Epic | Block | 50 | 90 | 1-1 | SingleEnemy | 45.00 | 15.00 |
| 3 | Interest Strike | Rare | Staked | 18 | 95 | 2-3 | SingleEnemy | 42.75 | 14.25 |
| 3 | Lucky Scatter | Rare | Meme | 18 | 95 | 1-4 | SingleEnemy | 42.75 | 14.25 |
| 3 | Wide Burst | Common | None | 22 | 95 | 1-1 | AllEnemies | 41.80 | 13.93 |
| 3 | Compression Burst | Common | Compressed | 22 | 95 | 2-2 | SingleEnemy | 41.80 | 13.93 |
| 3 | Rail Spike | Rare | None | 46 | 85 | 1-1 | SingleEnemy | 39.10 | 13.03 |
| 3 | Titan Crash | Rare | None | 48 | 80 | 1-1 | SingleEnemy | 38.40 | 12.80 |
| 3 | Stone Barrage | Common | Block | 16 | 95 | 2-3 | SingleEnemy | 38.00 | 12.67 |
| 3 | Chain Fork | Common | Forked | 12 | 95 | 2-4 | SingleEnemy | 34.20 | 11.40 |
| 3 | Frost Shards | Common | Frozen | 12 | 95 | 2-4 | SingleEnemy | 34.20 | 11.40 |
| 3 | Breach Rush | Rare | None | 10 | 90 | 3-4 | SingleEnemy | 31.50 | 10.50 |
| 3 | Focus Shot | Common | None | 30 | 90 | 1-1 | SingleEnemy | 27.00 | 9.00 |
| 3 | Rapid Pummel | Common | None | 8 | 95 | 2-5 | SingleEnemy | 26.60 | 8.87 |
| 3 | Body Check | Common | None | 30 | 85 | 1-1 | SingleEnemy | 25.50 | 8.50 |

By rarity, the damaging per-AP medians (free All-In excluded) are Common 24.07, Uncommon 26.60, Rare 17.71, Epic 15.00. By type the medians sit between about 17 (Meme, once All-In is excluded) and about 33 (Forked). Forked's median is high because Recoil Strike, Rapid Strike, and Fork Split are all in that small set.

The typed common line at 60.80 / 2 AP beats the typeless rare line on the same turn: Impact Knuckle 37.80, Vector Shot 34.20, Rupture Point 30.60. That direction matches the rule that a typed move beats its typeless equivalent, and that a rare typeless sits near a typed common rather than above it. The gap is still wide. This pass does not retune those powers. The rarity audit already owns that flag.

### High power that is cheap and fires too early

These are legal on turn 1, or on turn 2 with no real bank, at an index well above the 60.80 standard hit.

| Move | Cost | First turn | Field index | Why it is early |
|---|---:|---|---:|---|
| All-In | 0 | 1 | 93.10 | Largest index in the file. The "spend all AP" cost, the recoil, and the KO refund have no opcode. A player casts it every turn for free. |
| Fork Split | 3 | 2, or 1 after a 2 AP gift | 87.40 | Best spread. Legal the moment someone has passed once, or on turn 1 through AP Router. |
| Shard Scatter | 3 | same as Fork Split | 85.50 | Same shape, Uncommon. The Eva rider is still missing (`shard_scatter`, correctness audit). |
| Recoil Strike | 2 | 1 | 72.20 | 76 power. The 15% MaxHP recoil does not run, so the cost that was supposed to pay for the power is absent. |
| Ember Shower | 3 | 2 | 72.20 | Spread plus a 25% Atk drop that is in the stage column. |
| Automation Burst | 3 | 2 | 72.20 | Bot's real damage button. Same index as Ember Shower. |
| Volatile Shot | 2 | 1 | 68.40 | 72 power on turn 1. The miss self-hit does not run. |
| Hammer Sync | 2 | 1 | 68.40 | Rare Node. Base index already 68, and the row can scale another +10% per buffed partner up to +30%. |
| Cryo Ray | 2 | 1 | 68.40 | Rare Frozen. The 25% Speed drop is in the stage column. |
| Deep Dig | 3 | 2 | 68.40 | 72 power. The 50% Def-stage ignore does not run, so the index is the whole card. |

Rapid Strike (1 AP, index 49.40, priority) and Lean Hit (1 AP, index 43.70, no priority) are efficiency outliers, not nuke outliers. Rapid Strike is the typed priority finisher and should stay ahead of Quick Jab (index 15.20). Lean Hit is Compressed's efficiency hit. Both stay in the 1 AP band in the proposals. They are named here so a later pass does not "fix" them into the nuke gate.

### Weak moves that cost too much

A move is on this list when a same-type or same-shape move at a lower cost has a higher index, so the expensive one is never the click.

| Move | Cost | Index | Dominated by |
|---|---:|---:|---|
| Bean Beam | 2 | 22.80 | Almost every 2 AP hit. Lowest spread in the file. Cofi's other move is Bean Sip. |
| Body Check | 3 | 25.50 | Snap Strike, 2 AP, index 28.50. |
| Rapid Pummel | 3 | 26.60 | Snap Strike, 2 AP, index 28.50. |
| Focus Shot | 3 | 27.00 | Pulse Shot, 2 AP, index 24.70, is close on raw index and far better per AP (12.35 vs 9.00). |
| Breach Rush | 3 | 31.50 | Impact Knuckle, 2 AP, index 37.80. |
| Frost Shards | 3 | 34.20 | Frost Shot, 2 AP, index 60.80, same type. |
| Chain Fork | 3 | 34.20 | Recoil Strike and Rapid Strike, same type, both cheaper and higher. |
| Stone Barrage | 3 | 38.00 | Solid Strike, 2 AP, index 60.80, same type. |
| Interest Strike | 3 | 42.75 | Stake Strike, 2 AP, index 60.80. The rare's AP-on-hit rider can run when Def stage is at least Atk, and that still does not pay for a 3 AP tax under a 60-index common. |
| Blink Lunge | 2 | 23.40 | Priority is the reason the index is low. Left at 2 AP on purpose. |
| Multi-Thread Shot | 2 | 28.50 | Bot's common damage is half a typed common. The 10% +1 AP per hit is real and small (expected well under +1 AP per cast). Left at 2 AP; Bot's power problem is a damage pass, not an AP pass. |
| Finish Burn | 3 | 60.80, or 91.20 below 40% HP | The +50% rider is in the conditional-damage columns. At full HP it loses to Scorch Shot. Under the threshold it is a real finisher. Left at 3 AP. |

Wide Burst and Compression Burst (both index 41.80 at 3 AP) are soft. They are the turn-2 chip, and cutting every one of them would flatten the curve. They stay.

## 3. AP boosting

Regen is +2. Everything in this section is extra. "Acceleration" is how many turns earlier a 4, 5, or 6 AP move becomes castable if the boost is used on turn 1 and the Chainling then holds. A self-boost spends turn 1's action, so it cannot also swing on turn 1. No 4, 5, or 6 AP move exists; the columns say what these buttons would do to those costs.

### Moves

| Source | Type | What it pays | State | Accel to 4 / 5 / 6 |
|---|---|---|---|---|
| `ap_boost` AP Boost | None, Common | +2 now, cost 0, recharge 3 | Working. `GainAP:2`. | 0 / 1 / 1. Turn 2 opens at 6. Next boost is turn 4. |
| `fuel_surge` Fuel Surge | Gas, Common | +3 now, cost 0, plus Gas Spike | Working `GainAP:3`. Gas Spike's Atk +2 / Def −2 apply once and do not revert when the status ends. | 0 / 1 / 1. Turn 2 opens at 7. Repeatable, because it has no recharge. |
| `data_scramble` Data Scramble | Glitched, Rare | +2 now, cost 0, Atk −1 and Speed −1 on both sides | Working in the local file. Stage column hits the user. Utility `ChangeStage` hits the foe. The branch audit described the foe half as missing; the local utility row has it. | Same stock as AP Boost for one cast, and it can be repeated every turn. 0 / 1 / 1. |
| `turn_start_ap_gain` | None, Common | +1 after regen, before the action | Working. Utility `GainAP` runs from the turn-start passive path. | Opens turn 1 at 3, so every real 3 AP move comes forward one turn. A held 4 is still turn 2. A held 5 or 6 is turn 2 (window opens at 6). Accel 0 / 1 / 1, and the 3 AP accel is the one that matters today. |
| `ap_reserve` AP Reserve | Staked, Common | Banks up to 5, pays it back next turn with +1 | Working `BankAPUntilNextTurn`. Release runs on the next turn start after regen. | Turn 1 banks 2. Turn 2 opens at 5. Accel 0 / 1 / 0. A 6 is still turn 3. |
| `yield_cycle` Yield Cycle | Staked, Common | Cost 1. Yield: +1 AP at end of turn for 3 turns. | Working. Status `EoTAPChange` +1, duration 3. | Action windows: turn 2 opens at 4, turn 3 at 7. Accel 0 / 0 / 0. The +1 lands at end of turn, so it cushions the next window and does not pull a 5 or a 6 forward. |
| `ap_router` AP Router | Node, Common | Cost 2. The ally gains the 2 you just paid. | Working as a gift of the paid 2 (`TransferPaidCost`). The sentence "between any two allies" and "up to 2" is wider than the code. | If the ally has not acted: their turn 1 opens at 4. Accel 1 / 1 / 1. This is the only current effect that puts a 4 AP move on turn 1. |
| `ap_donation` AP Donation | None, Common | Same gift of 2, once per turn | Working, with the once-per-turn flag. | Same 1 / 1 / 1 on the ally. Router is the typed version and may be used every turn, so it already beats the typeless gift. |
| `ap_redistribution` | None, Common | Same gift of the paid 2 | Partial. It does not pull AP off a third Chainling. | Same gift. |
| `uplink` Uplink | Node, Uncommon | +1 to an ally you just buffed, once per ally per turn | The local resolver pays 1 after the action via `NoteUplinkAlly` / `TryClaimUplinkPay`. Those methods are not on this branch's `TurnActionLedger.cs`, and that file was not in the snapshot. | Ally turn 1 opens at 3 if they have not acted. That pulls a 3 AP move onto turn 1. Accel for 4 / 5 / 6 is 0 / 1 / 0. |
| `network_response` | Node, Common | Cost 1. +1 AP and Speed +1 if a foe misses | Working arm: `ArmUtilityWindow` on `EnemyMiss`. | A missed foe is the condition. One point, not a calendar shift by itself. |
| `overflow_reboot` | Node, Rare | Cost 2. +1 AP and a 10% team heal if a foe faints | Working arm on `FoeKO`. | One point on a KO turn. |
| `rallying_slam` | Block, Rare | +1 on a confirmed hit if Def stage ≥ Atk | Working in the local row (`GainAP` plus the stage condition). | One point after a 2 AP hit. Turn 1 ends at 1 if it hits. |
| `interest_strike` | Staked, Rare | +1 per confirmed hit under the same Def condition | Working the same way. 2–3 hits, so 2 or 3 AP back if the condition holds, on a 3 AP move whose index is 42.75. | Can refund its own cost when the condition holds. It does not stock a later nuke beyond that. |
| `flame_charge` | Burned, Uncommon | +1 and ×1.3 if the foe has 0 AP | Working condition in the local row. | Punishes an empty bar. One point. |
| `multi_thread_shot` | Bot, Common | 10% +1 per hit, 2–3 hits | Working `GainAP` at 10% after each hit. | Expected gain is about 0.25 AP. Not a plan. |
| `charged_relay` | Forked, Rare | +2 if you hit 2+ targets this turn, after a 5% heal, cost 2 | Working `GainApIfDistinctTargetsHitAtLeast`. | Pays the 2 back when the condition hits. Needs a spread hit earlier in the turn. |
| `ice_wall` | Frozen, Common | +1 when hit, if you are faster. Cost 3. | The mitigation window is authored with hit budget 0, so the wall does not mitigate (audit E-17). The speed-gated +1 can still arm. | Unreliable 1. |
| `desperation_surge` | None, Rare | +1 once, below 33% HP, plus Atk +2 | Working once-per-battle passive. | One point at low HP. |
| `on_crit_bonus` | None, Common | +1 on crit | Working hardcoded passive. The row has no utility payload; the resolver keys the id. | Proc. |
| `precision_focus` | None, Common | 75% to refund 1 on your miss | Working hardcoded. | A refund of 1, not a stock. |
| `shattered_armor` | Shard, Common | +1 and 5% mitigation on a super-effective hit | Working hardcoded. | Proc. |
| `conductive_counter` | Forked, Rare | +1 and 15% of contact damage back, once a turn | Working hardcoded. | One point when hit by contact. |
| `immutable_ledger` | Block, Uncommon | +1 the first time you are hit each turn, plus 20% off that hit | Working hardcoded. | One point per turn while under attack. |
| `stake_refund` | Staked, Rare | +1 when your buff expires | Working if the `OnBuffExpired` trigger fires the utility `GainAP`. | One point, late. |
| `meme_momentum` | Meme, Rare | +2 AP and Atk +2 on both attacks after a landed hit; a miss drops both attacks and drains 1 | Working hardcoded. Meme has no chemistry, and this does not add one. | The +2 is end of turn, after a hit. Strong on a turn you connected. A miss is a 1 AP tax. |
| `double_or_nothing` | Meme, Common | End of turn, 60% +1 / 40% −1 | Partial. The AP roll runs. The 40% branch's drain immunity does not. | Expected +0.2 AP per turn. |
| `rally_cry` | None, Common | Cost 3. Allies Speed +1. Sentence also says +1 AP at end of turn. | Empty on the AP half. Speed is in the stage column and runs. There is no end-of-turn AP opcode. | 0. |
| `refinery` | Mined, Common | Cost 2. Def +1. Sentence also says +1 AP at end of turn for 2 turns. | Empty on the AP half. Def +1 runs through the stage column. | 0. |
| `viral_surge` | Meme, Common | Cost 1. Sentence: +2 or −1 AP, plus a random stage. | Empty. No utility payload. | 0. |
| `dice_roll` | Meme, Common | Cost 1. Sentence includes a branch that gives an ally +2 AP. | Empty on that branch. The 20% heal can run from `HealPercentMaxHP`. | 0. |
| `crystal_focus` | Frozen, Uncommon | Cost 0. At 0 AP, heal 10% and Speed +1. | The heal and the stage are on the move. It does not grant AP. It is the low-AP payoff, next to Low AP Surge / Low AP Burst, which grant Speed when AP is at most 1 after regen. | 0 AP. |

`streamlined_flow` (Compressed, Rare) is a discount, not a grant: while HP is above 70%, `MoveAPDiscountWhileHPAbove` cuts 1 off the effective cost. A 3 becomes a 2, so a 3 AP move is legal on turn 1 at high HP. That is a one-turn acceleration of the entire 3 AP band, and it is the cleanest "fire the heavy hit early" tool that exists, because it does not spend the action. The Def −1 is a separate utility on action and will keep firing while the passive's trigger runs; the 70% gate on that half is the resolve condition, which the passive path does not re-check (it passes `shouldApplyConditionalEffects: true`). The discount itself does check HP inside eligibility.

### Passives, modifiers, statuses, chemistry

Chemistry uses matrix v1. Meme's row is empty.

Pumped is +1 at end of turn for 3 turns, ChemistryOnly. Cells: Node→Mined, Gas→Block, Mined→Gas, Compressed→Encrypted, all `@Self`. Three ticks are +3 if the bar can hold them. They land at end of turn. A Chainling who spent 2 to land the hit ends turn 1 at 1, opens turn 2 at 3, and opens turn 3 at 6. A 6 AP follow-up is still turn 3. A 4 AP follow-up waits until turn 3 as well, because turn 2's window is 3. Landing Pumped with a real attack is slower than passing.

Yield is the same +1 tick as a General status from Yield Cycle only. Nothing else in the local move file applies it.

Modifiers that add AP, all of them working when the trigger fires:

| Id | Name | Pay |
|---|---|---|
| 3.1-a / b / c | Spare Change, Rebate, Full Refund | 5–10%, 10–15%, 15–20% to refund 1 AP. The resolver refunds 1, not a percent of the cost. |
| 3.2-a | Echo | 8–12% to repeat the move at no AP cost. A saved cast, not a stock. |
| 6.2-a | Crit Wallet | +1 on crit. |
| 6.4-b | Warm Welcome | +1 to yourself on swap in. |
| 6.4-e | Hand-Off | +1 to an ally on your swap in. |
| 6.4-f | Parting Gift | +1 to yourself on swap out. |
| 6.5-a | Finisher Fee | +1 on KO. |

Group Hug (`4.5-a`) is not an AP boost in this tree. The local modifier text and the local resolver both spread a single-ally support onto both allies. The branch audit describes a later +1 AP behavior. This analysis follows the local resolver.

### Who can actually bring a boost

Progression pools are the live learn sets. Catalog type is wider than the pool.

| Type | In a species pool | Catalog only |
|---|---|---|
| None | — | AP Boost, Turn Start AP Gain, AP Donation, AP Redistribution, Rally Cry, the crit/miss/desperation passives |
| Node | Validape: AP Router, Uplink, Hammer Sync, Pack Rally | Network Response, Overflow Reboot |
| Gas | DeGhoul: Fuel Surge, Fee Collector. Balloom: Fuel Surge, All-In, Volatile Shot | — |
| Staked | Rattlestake: AP Reserve, Yield Cycle. Hopoch: Yield Cycle, Yield Spike | Stake Refund, Interest Strike |
| Glitched | Kreechank: Data Scramble | — |
| Frozen | — | Crystal Focus, Ice Wall, Cryo Ray is on Gilarime |
| Block | — | Rallying Slam, Immutable Ledger |
| Forked | — | Charged Relay, Conductive Counter, Recoil Strike is on Forkrook |
| Compressed | Humpacked: Streamlined Flow | — |
| Meme | Balloom is Gas. Ghost is Meme and has no pool row. | Viral Surge, Dice Roll, Double-or-Nothing, Meme Momentum, Jackpot Shot |

The boost that changes a real calendar is small: Turn Start AP Gain and Streamlined Flow make a 3 legal on turn 1, and AP Router makes a 4 legal on turn 1 for the ally. Fuel Surge, AP Boost, and Data Scramble stock a bar past 3 and then have nothing priced up there to spend it on.

## 4. AP denial

Denial removes AP, blocks a gain, or raises a cost. Delay below is measured on a Chainling who is holding for a 6 AP window (turn 3 at 6 if left alone), because that is the cost the goals are about. Against today's 2 AP hits the same tools delay 0 turns.

### Moves

| Source | Type | What it does | State | Delay on a holder aiming at 6 |
|---|---|---|---|---|
| `thread_lock` | Bot, Rare, cost 2 | Applies Thread Lock, duration 1. "Cannot gain AP next turn." | The status applies. `CombatApService.Gain` blocks every gain while it is up, including regen. It is not a next-action status, so it ticks off at the end of the turn it was applied. The next turn's +2 happens. | 0. It can still delete a same-turn gift (Router, Uplink, Fuel Surge) if the lock lands first, because those gains go through `Gain`. |
| `mev_extract` | Bot, Common, cost 2 | MEV Extracted for 2 turns. Sentence says steal 1 per turn. | Partial. The status applies. `EoTAPChange` is −1, and the processor drains. The caster does not gain it. Two drains: the application turn's end, and the next turn's end. | 1 turn if they are holding (turn 3 window is 4, turn 4 opens at 6). 0 turns if they spend to 0 before each end step, because the floor eats the drain. |
| `transaction_tax` | Gas, Common, cost 1 | Steal 1 immediately. | Working in the local row (`StealAP:1`). The branch audit still lists this payload as empty. | 1 turn on a holder (turn 1 goes 2→1, turn 3 opens at 5, turn 4 at 7). Best rate in the game: 1 AP spent, 1 AP moved onto the caster. |
| `ap_drain` | None, Common, cost 2 | Drain 1, 25% to also gain 1. | Working in the local row. | Same 1 turn on a holder, at twice the cost of Transaction Tax, and the gain is only 25%. The typeless card loses to the typed one, which is the right direction, and it loses so hard that it is not worth casting. |
| `ap_siphon` | Gas, Common, power 56, cost 3 | 50% to drain 1 on hit. | Working in the local row, after each confirmed hit. | A 50% chance at 1. The hit itself is index 53.20 and is legal on turn 2. |
| `memory_leak` | Encrypted, Rare, cost 2 | Foe loses 2 at their next turn start. User Speed +2. | Working arm. The window fires on the foe's turn start after regen, so it cancels that regen and leaves stored AP alone when stored + regen − 2 lands on the stored value. From 2 stored: turn 2 opens at 2, turn 3 at 4, turn 4 at 6. | 1 turn. |
| `verification_lock` | Encrypted, Common, cost 1 | FUD, and the next move costs +1. | Surcharge works (`NextMoveAPSurcharge`, consumed on the next spend). | 0 against a booster. A 0-cost boost soaks the +1. A 6 would become a 7 and slip to turn 4 only if they spend the surcharge on the nuke itself. |
| `glitch_feedback` | Glitched, Common, cost 1 | +1 cost on the next move. Sentence also cleanses one of their debuffs. | Surcharge works. The local row also has `Cleanse`, so the cleanse half is no longer an empty payload. | Same play-around as Verification Lock. |
| `fee_collector` | Gas, Rare | At your turn start, each foe who has more AP than you loses 1. | Working. `DrainAP` on all foes, and the executor special-cases this id. | 0 on turn 1 if both sides open at 2. Later, 1 per turn from each holder who is ahead. A holder slips about 1 turn. A spender at 0 is immune. |
| `ember_wall` | Burned, Rare, cost 2 | Full-absorb wall; the attacker loses 1 AP when they hit it. | The wall and `WallHitRetaliation` drain are implemented (`ResolveWallStrikeApDrain`). | The swing already happened. The 1 comes off the leftover bar. |
| `defensive_reflex` | None, Epic | 20% mitigation. Lose 1 at end of turn. | Mitigation is read from the utility payload. The `DrainAP` runs when the end-of-turn passive trigger executes the utility. | A self-tax of 1 per turn. It slows your own bank. |
| `yield_spike` | Staked, Uncommon | Sentence: at 4+ AP, +25% and spend 1 more. Local cost is 2. | The index is the normal 60.80. The extra spend and the bonus do not run. The issues master still says this move costs 3; the local file says 2. | 0. |
| `all_in` | Meme, Common | Sentence: spend all AP. | Cost column is 0. No spend-all opcode. | It is a boost to the attacker's damage, not a denial. Listed here so the cost is visible next to the denial tools. |
| `combustive_shift` | Gas, Rare | Sentence: bonus vs a debuffed foe who is missing AP. | The bonus does not run. | 0. |

### Statuses, chemistry, modifiers

| Source | Numbers | State | Delay |
|---|---|---|---|
| Thread Lock | Duration 1, blocks `Gain` | See the move. Blocks same-turn gains only. | 0 on the next regen. |
| MEV Extracted | −1 at end of turn, duration 2, drain | Not a steal. | 1 turn on a holder, 0 on a spender. |
| Drained | −1 at end of turn, duration 3 | Working. ChemistryOnly. | A holder who is hit on turn 1 opens turn 3 at 4 and turn 5 at 7. The 6 slips from turn 3 to turn 5. Two turns, and only if they keep AP into each end step. A spender who empties the bar makes every tick remove 0. |
| Isolated | Blocks ally support, including AP transfer | Working. Router and Donation refuse the target. | Deletes one gift. Does not touch regen. |
| Pumped | +1 at end of turn, duration 3 | Boost, listed in section 3. Denial does not stop the tick unless Thread Lock is still up, and Thread Lock has already expired by the next end step. | — |

Drained cells in matrix v1, all `@Foe`: Gas→Bot, Encrypted→Gas, Bot→Node, Bot→Block, Bot→Staked. Bot has the three-target Drained set. That is the chemistry identity the type profile describes. It is also the strongest hold-punish in the game, and it does nothing to a Chainling who spends the bar before the end step.

Modifier `6.1-b` AP Siphon steals 1 on hit. The catalog text says drain. The resolver calls `Steal`, so the attacker gains it. Trigger chance on the row is 0.2.

### Timing that makes the delay small

On a damaging hit the order is chemistry, then damage, then the on-hit AP rider. End of turn runs status damage and status AP (Pumped, Yield, Drained, MEV), then ticks durations, then end-of-turn passives (Double-or-Nothing, Defensive Reflex's drain, Meme Momentum). The design rule puts move, modifier, and passive ticks before chemistry and status ticks. The end-of-turn order runs status first and passives second. One consequence: Thread Lock ticks off before Double-or-Nothing's +1, so the lock does not eat that passive.

### The floor, the cap, and the AI

The floor is why denial whiffs. A turn-1 2 AP attack leaves 0. MEV's first tick removes 0. Drained's tick removes 0. The bar refills to 2 at the next start, the attack happens again, and the tick whiffs again. Denial of 1 only sticks when the target is holding.

The cap does not matter in the first four turns of a real fight. A holder hits 10 on turn 5. Nobody chasing a 2 or 3 AP move is sitting on the cap.

The AI does not play denial.

- Aggressive AI scores damaging moves at `DamagePerAP * 10` and everything else at 0.1. `DamagePerAP` returns 0 when cost is 0, so All-In scores 0 and loses to Volatile Shot and Recoil Strike. MEV Extract and Thread Lock score 0.1. Kitsauto's aggressive loop is Blade Flurry (index 51.30) every turn.
- Utility AI gives a status move 30, plus 15 per attached status, plus 10 per negative stage, minus the AP cost. Damage is `DamagePerAP * 2`, and that estimate multiplies by attack/100. At Kitsauto's base 76 MeleeAtk, Blade Flurry scores about 37. MEV Extract scores 43. Debuff Script scores 49 (Atk −1 and Def −1, cost 1). While Debuff Script is legal, this AI never reaches the drain. MEV would beat the flurry on this stat line if the debuff were not equipped.
- Kitsauto's pool is `multi_thread_shot`, `automated_pressure`, `debuff_script`, `fox_feint`, `blade_flurry`. Thread Lock and MEV Extract are Bot-typed and are not in that pool. The test loadout `starter_kitsauto_aggro` equips MEV anyway, which the pool would not teach.

So a player who builds a denial set can apply the tools above. The AI, on the kits the species actually learn, does not.

### Lock-out

Today's tools do not lock a bar at 0, because regen is not blocked and drains cannot pass the floor. The dangerous version is a Thread Lock that does block regen and can be recast every turn. The caster opens each turn at 2, Thread Lock costs 2, and a faster Bot could suppress every future regen. One 2 AP attack would empty the target, and the target would stay at 0. Section 6's guardrail is there so the fix for "Thread Lock should do what it says" does not become that loop.

## 5. Boost against denial, turns 1–6

Both sides are players, acting with perfect information. The index is expected damage per cast, not a KO clock. "Bot acts first" is the denial player's best case. Regen is +2 at the start. End-of-turn drains happen after both actions.

### Matchup A — Kitsauto denial vs Balloom's turn-1 nuke

Balloom's pool has All-In (93.10, cost 0), Volatile Shot (68.40, cost 2), Recoil Strike (72.20, cost 2), and Fuel Surge. The player casts All-In. It is free and it is the highest index. Denial cannot price it out.

Bot acts first and spends turn 1 on Thread Lock. All-In still fires. The lock expires at end of turn. Turns 2–6, if Bot then casts Blade Flurry (51.30) every turn and Balloom casts All-In every turn:

| Side | Casts across turns 1–6 | Index sum |
|---|---|---:|
| Balloom | All-In × 6 | 558.6 |
| Kitsauto | Thread Lock × 1, Blade Flurry × 5 | 256.5 |

Balloom leads by 302 index, about 2.2×. The lock prevented 0 damage. MEV on turn 1 is the same shape: All-In is already paid (it costs 0), the end-step drain removes whatever All-In did not spend, and the caster gains nothing. If Balloom is the one who acts first, the turn-1 hit lands before the lock and the totals do not change.

Swap All-In for Volatile Shot so the nuke actually spends AP. Bot MEV on turn 1, Balloom Volatile (spends 2, ends at 0, MEV removes 0). Then both have 2 AP every later turn. Balloom Volatile × 6 = 410.4. Bot MEV + Blade Flurry × 5 = 256.5. Balloom leads by 154. Delay on the 68-index hit: 0 turns.

### Matchup B — Validape routes a 3 AP spread, Bot tries to stop it

Forkrook holds Fork Split (87.40, cost 3). Turn 1 both open at 2, so Fork Split is illegal until someone adds AP. Validape uses AP Router: Validape ends at 0, Forkrook goes from 2 to 4 and, acting later the same turn, casts Fork Split.

Bot Thread Lock on Forkrook before the router: `Gain` is blocked, the 2 AP Validape paid is destroyed, Forkrook stays at 2 and casts Rapid Strike (49.40) instead. Turn 2 the lock has expired, Forkrook opens at 4 if they spent the 2, or at 4 anyway from regen if they held, and Fork Split lands. Denial delayed the 87-index spread by 1 turn and replaced it with a 49-index priority hit. Bot's turn 1 dealt 0. Net on turn 1: the spread side lost 38 index and still fires the spread on turn 2. If Validape outspeeds the Bot, the router lands, Fork Split hits for 87.40 on turn 1, and the lock is late.

That 1-turn slip, and only when Bot wins the speed tie, is the best denial result against a move that exists.

### Matchup C — a holder who wants a 6, against the denial that exists

No move costs 6. This row is the hold math, so the gap is visible. A holder who passes opens turn 3 at 6.

| Denial on turn 1, holder keeps passing | Window on turn 3 | First turn they have 6 | Delay |
|---|---:|---|---:|
| None | 6 | 3 | 0 |
| Thread Lock, as shipped | 6 | 3 | 0 |
| MEV Extract (two end-step drains of 1) | 4 | 4 | 1 |
| Transaction Tax (steal 1 immediately) | 5 | 4 | 1 |
| Memory Leak (−2 on turn 2 start, after regen) | 4 | 4 | 1 |
| Drained chemistry, three end-step drains, holder keeps the AP | 4 | 5 | 2 |
| Fuel Surge on turn 1 (+3) and MEV | Turn 2 opens at 6 | 2 | Boost is 1 turn early and fully covers the MEV |
| AP Boost on turn 1 (+2) and MEV | Turn 2 opens at 5, turn 3 at 6 | 3 | Boost cancels MEV's one turn and does not go further |
| Fuel Surge and Drained | Turn 2 opens at 6 (the first Drained tick already fired, 5−1+2) | 2 | The +3 still fires the 6 on turn 2 |

Read that last block as the current offset. MEV, Tax, and Memory Leak each slip a holder by one turn. Drained slips them by two, and only while they hold. Fuel Surge puts the 6 on turn 2 and still does so through one MEV or through Drained. AP Boost only ties MEV; it does not beat it. Nothing on the Bot side, as shipped, stops a Gas Chainling who pressed Fuel Surge.

### Matchup D — what the AI actually does

Aggressive Kitsauto versus aggressive Balloom, turns 1–6, both always able to pay 2.

Scores below use base attack and no stages: Kitsauto MeleeAtk 76 and RangedAtk 60, Balloom MeleeAtk 68 and RangedAtk 40. Aggressive score is `DamagePerAP * 10`. `DamagePerAP` is estimated damage divided by cost, and estimated damage is `Power * (attack/100) * expected hits * accuracy`.

Kitsauto's best aggressive score is Blade Flurry, about 195. Multi-Thread Shot is about 86. MEV and Thread Lock are not in the pool and would score 0.1 if they were. Six flurries on the field-index screen: 307.8.

Balloom's best aggressive score is Recoil Strike, about 245, ahead of Volatile Shot at about 137. All-In scores 0 because cost 0 short-circuits `DamagePerAP`, which puts it under Fuel Surge and Gas Cloud at 0.1. Six recoils on the field-index screen: 433.2.

Balloom leads by 125 index. No denial button is pressed. The scariest move in the file, All-In, is also the one this AI will not click.

Utility AI on the test loadout (Blade Flurry, Multi-Thread Shot, Debuff Script, MEV Extract) picks Debuff Script (49) over MEV Extract (43) over Blade Flurry (about 37 at 76 attack). Every turn is Atk −1 and Def −1. The drain is never selected, and Kitsauto's six-turn damage index is 0. Balloom's recoil line is unchanged.

### Who wins today

The damage side wins every representative fight.

- Against a free or 2 AP nuke, denial delays 0 turns and gives up its own turn 1. The nuke side is ahead by 150–300 field index over six turns.
- Against a routed 3 AP spread, denial delays that one hit by 1 turn if it wins the speed check, and deals 0 on the turn it does so.
- Against a holder building toward a cost that does not exist yet, the best Bot button slips them by 1 turn, chemistry Drained slips them by 2, and Fuel Surge ignores both.
- The AI never takes the denial line, and it refuses All-In for a scoring bug on cost 0.

## 6. Proposals

Each item is a proposal. None of it is applied.

The shape these proposals aim at: a standard typed hit stays 2 AP and turn 1; a heavy hit is 4 AP and turn 2 unless a partner gifts the AP; a nuke is 5 or 6 AP and turn 3 unless a boost pulls it to turn 2. One denial event slips a holder by one turn. One boost pulls a nuke forward by one turn. Both together land on the untouched calendar. A second denial cannot chain into an empty bar that never regens.

### 6.1 Cost-curve retune

Data-only. Power, accuracy, and hit counts stay. The 2 AP power-64 typed commons stay at 2 AP. They are the standard hit, and they already beat the typeless rares on that same turn (60.80 against 30–38). The raises are the hits above that line. Two turns of Impact Knuckle (37.80 × 2 = 75.6) land near one Hammer Sync on the later turn (68.40, or higher with buffed partners). That is the "rare typeless near a typed common" rule read across the wait, with the typed rare still the bigger single hit.

Raises, so the early nukes need a bank or a gift:

| Move | Old cost | Proposed cost | Index | First turn after the change, no boost |
|---|---:|---:|---:|---|
| All-In | 0 | 6 | 93.10 | 3 |
| Fork Split | 3 | 5 | 87.40 | 3 |
| Shard Scatter | 3 | 5 | 85.50 | 3 |
| Recoil Strike | 2 | 4 | 72.20 | 2, and only if turn 1 spent 0 |
| Ember Shower | 3 | 4 | 72.20 | 2, same condition |
| Automation Burst | 3 | 4 | 72.20 | 2, same condition |
| Volatile Shot | 2 | 4 | 68.40 | 2, same condition |
| Hammer Sync | 2 | 4 | 68.40 | 2, same condition |
| Cryo Ray | 2 | 4 | 68.40 | 2, same condition |
| Deep Dig | 3 | 4 | 68.40 | 2, same condition |

All-In's missing spend-all, recoil, and KO refund stay an engine row (issues master `MV-all_in`). The cost change stops the free cast on its own. Recoil Strike and Volatile Shot are still missing their HP costs; raising AP does not replace those riders, and the moves stay undercosted until the riders exist. Fork Split at 5 AP remains the typed common spread, ahead of Ricochet Storm (rare typeless, index 54.00, left at 3 AP).

Cuts, so a dominated card is not taxed for being weak:

| Move | Old cost | Proposed cost | Index | Reason |
|---|---:|---:|---:|---|
| Bean Beam | 2 | 1 | 22.80 | Weakest spread. Cofi keeps a chip. This does not add the 6 AP beam from `DEC-BEAM`. |
| Body Check | 3 | 2 | 25.50 | Loses to Snap Strike at 2 AP. |
| Rapid Pummel | 3 | 2 | 26.60 | Same. |
| Focus Shot | 3 | 2 | 27.00 | Per AP 9.00 against Pulse Shot's 12.35. |
| Breach Rush | 3 | 2 | 31.50 | Loses to Impact Knuckle. |
| Frost Shards | 3 | 2 | 34.20 | Same type has Frost Shot at 60.80 for 2 AP. |
| Chain Fork | 3 | 2 | 34.20 | Same type has cheaper, higher hits. |
| Stone Barrage | 3 | 2 | 38.00 | Same type has Solid Strike at 60.80 for 2 AP. |
| Interest Strike | 3 | 2 | 42.75 | The AP rider is the reason to click a rare that still loses the index to Stake Strike. |
| AP Drain | 2 | 1 | — | Typeless drain of 1 should cost what Gas's steal of 1 costs. Steal still beats drain because the caster gains the point. |

Left on purpose: the entire 60.80 / 2 AP typed-common line, Rapid Strike at 1 AP (priority), Lean Hit at 1 AP (Compressed efficiency), Blink Lunge at 2 AP (priority tax), Finish Burn at 3 AP (the low-HP rider is real), Multi-Thread Shot at 2 AP, Crystal Bash at 2 AP, Wide Burst and Compression Burst at 3 AP.

After this retune, Fuel Surge or AP Boost on turn 1 puts a 6 AP All-In on turn 2. A pass with no boost puts it on turn 3. A Router gift of 2 on turn 1 puts a 4 AP Hammer, Cryo, Recoil, or Volatile on turn 1 for the ally, and leaves All-In on turn 2 (the ally opens turn 2 at 6). That is the boost fantasy the current ceiling cannot express.

### 6.2 Denial and boost tuning

| Proposal | Tag | What changes |
|---|---|---|
| Thread Lock suppresses the next regen and then ends. | Engine | Hold the duration through the application turn so the following turn's +2 is the blocked gain. One use slips a 6 AP holder from turn 3 to turn 4. The sentence on the card starts being true. |
| MEV Extracted steals. | Engine | The end-step −1 is removed from the target and granted to the caster, up to the cap and up to what the target actually has. Two successful steals are 2 AP across two turns, matching the card. A whiff on an empty bar still steals 0. |
| Transaction Tax stays the immediate steal of 1 for 1 AP. | — | Already the right typed common. No change. |
| AP Drain goes to 1 AP. | Data | In the table above. The 25% self-gain stays, so it remains the worse typeless cousin of Tax. |
| Fee Collector drains 2 when the foe has 4 or more AP, and 1 otherwise. | Engine | The current check ("more AP than me") stays. The amount grows only against a stored bar. A spender at 0 or 1 is untouched. This is the "punish stored AP" knob, on a Gas rare that already does the job in miniature. |
| Fuel Surge, AP Boost, AP Reserve's release, and Uplink grant shielded AP. | Engine | Shielded AP is spent like any other AP. Drain, steal, and MEV take unshielded AP first and do not touch the shield. Thread Lock does not block a shielded gain. See the offset math below. |
| Yield Cycle's end-step gain goes from +1 to +2, duration stays 3, cost stays 1. | Data | Only Yield Cycle applies Yield. Turn 1 pays 1 and ends at 3 after the tick (2 − 1 + 2). Turn 2 opens at 5. Turn 3 opens at 9. A 6 arrives on turn 3 still, three points ahead of a plain bank, and a 5 arrives on turn 2. Typed Staked now does something AP Boost does not: it keeps paying after the action. |
| AP Reserve's bonus goes from +1 to +2. | Data | The utility's second number. Turn 1 banks 2. Turn 2 opens at 2 regen + 4 release = 6. Staked can put a 6 on turn 2, which is the typed answer to AP Boost, and the bank is the shield's natural home. |
| Gas Spike stages revert when the status ends. | Engine | Fuel Surge's Atk +2 / Def −2 currently apply once and stick. The design rule is that a timed status takes its stages with it. Record them as owned deltas and revert on expiry. Same treatment for any other timed stat status that is not already owned (FUD is the obvious neighbor). |
| Bench recharge ticks. | Engine | Call the same bench tick the branch resolver already has. AP Boost's two skipped turns keep counting while that Chainling is benched. |
| Kitsauto learns the denial kit. | Data | Pool becomes learnable `mev_extract`, `thread_lock`, `debuff_script` and exclusive `fox_feint`, `blade_flurry`. `automated_pressure` (random stat poke) and `multi_thread_shot` leave the five-move pool. Blade Flurry remains the damage exclusive. Bot is the denial type, and the only Bot species should carry the two denial cards. |
| Utility AI values a denial when the foe is within 2 AP of a heavy cost. | Engine | Add a score when an opposing active's AP is within 2 of a move they cannot cast yet and could cast next turn. Aggressive AI can keep ignoring it. Also stop returning 0 from `DamagePerAP` when cost is 0; score those moves on raw estimated damage so All-In is not the worst aggressive click. |

Shielded boost against a 2-point denial, after the cost retune, holder wants All-In at 6:

| Line | Turn 2 window | Turn 3 window | First cast |
|---|---:|---:|---|
| Pass, no denial | 4 | 6 | Turn 3 |
| Fuel Surge on turn 1, no denial | 7 | — | Turn 2 |
| MEV as a real steal of 1 on two end steps, no boost | 3 | 4 | Turn 4 |
| Thread Lock blocks the turn-2 regen, no boost | 2 | 4 | Turn 4 |
| Fuel Surge (+3 shielded) and a 2-point drain of the unshielded bar | 5 (3 shielded + 2 regen) | 7 | Turn 3 |
| AP Boost (+2 shielded) and the same drain | 4 | 6 | Turn 3 |

Untouched is turn 3. Boost alone is turn 2. Denial alone is turn 4. Both together is turn 3. That is the offset. Fuel Surge's third point is not enough to hold turn 2 through a 2-point denial, and it is enough to be back on schedule instead of a turn late. A second 2-point denial on turn 2 takes that 5 down to 3, turn 3 opens at 5, and the 6 waits until turn 4. A 1-point Tax on turn 2 leaves 4, and turn 3 still opens at 6. Denial answers a second time when the hit is large enough. It does not need a lock-out to do it.

### 6.3 Guardrails against lock-out

| Proposal | Tag | Rule |
|---|---|---|
| Thread Lock cannot be reapplied to the same target until two turns after it ends. | Engine | One suppressed regen, then a gap. A Bot who only presses the lock cannot mute every future +2. |
| One turn can remove at most 2 AP by drain and steal combined, and a Thread Lock counts as that turn's 2. | Engine | Tax plus MEV plus Fee Collector plus a chemistry Drained tick cannot stack into a wipe on the turn the lock also fires. Drained still ticks on later turns, one at a time. |
| The floor stays 0, and regen still happens on the turn after a lock ends. | — | Already true. The cap stays 10. Shielded AP counts toward the cap. |
| A 0 AP Chainling is a legal, if weak, fighter. | — | Low AP Surge, Low AP Burst, and Crystal Focus already pay Speed or a heal for an empty bar. Denial should park people there for a turn, and those buttons should be why the turn is playable. |

### 6.4 New concepts, kept small

Both of these are the offset made visible. They reuse AP, drain, and a threshold. They do not add a new status family.

1. **Shielded AP** (engine). Gains from Fuel Surge, AP Boost, AP Reserve, and Uplink are marked. Drains spend the unmarked AP first. The mark clears when that AP is spent on a move. A player can read it as "the boost survived the tax." It is the reason boost offsets denial instead of merely racing it.

2. **Fat-pool steal** (engine, on tools that already exist). Fee Collector's drain becomes 2 at 4+ AP. MEV's steal becomes 2 on a tick where the target has 4+ AP, still inside the 2-AP-per-turn cap. Holding for a 6 is what gets punished. Spending down to swing a 2 AP standard hit is not. Bot and Gas stay the denial types; Meme does not grow a chemistry cell to join them.

Router stays the partner version of a boost: it moves unmarked AP, so a lock or a shield rule on the recipient still answers it. Uplink's 1 is shielded, so a lock does not delete the Node passive, and a drain does not eat it before the ally's action. Typed Node keeps an edge on typeless Donation, which moves unmarked AP and can be eaten.

## Sources checked

- Local snapshot: the ten combat files named above (the zip also held `UiHapticsService.cs`, which has no AP rules).
- Branch config: `CombatConstants`, `CombatResolverConfig_Default.asset`, `CombatFactory`, `CombatActionEligibilityService`, `UtilityEffectExecutor`, `SwapModifierResolver`, `AggressiveAI`, `UtilityStatusAI`, `AIHelpers`.
- Matrix: `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv`.
- Audits used for payload state, and corrected where the local CSV has since filled the opcode: `COMBAT_ISSUES_MASTER.md`, `UNIMPLEMENTED_MOVE_EFFECTS.md`, `BATTLE_MOVE_BALANCE_BY_RARITY.md`, `BATTLE_EFFECTS_CORRECTNESS_AUDIT.md`.
