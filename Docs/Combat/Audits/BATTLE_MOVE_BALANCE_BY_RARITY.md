# Battle move balance by rarity

Date: 2026-10-16. Numbers are computed from `Docs/Specs/Combat/Data/Moves.csv`. This doc suggests changes. It does not apply them.

Read [BATTLE_EFFECTS_CORRECTNESS_AUDIT.md](BATTLE_EFFECTS_CORRECTNESS_AUDIT.md) first. Several "strong" moves are strong because a described cost or rider does not run. Retiering those numbers before the payload exists will aim at the wrong move.

## Value model

The index is a damage screen only. Attack versus defense, chemistry, crits, and priority stay out of it, because those depend on the battle. The index uses the authored move.

1. Expected hits = `(HitMin + HitMax) / 2`.
2. Accuracy factor = `Accuracy / 100` when Accuracy is above 0. Accuracy 0 always hits (`CombatResolver` treats `baseAccuracy <= 0` like `forceHit`), so the factor is 1. Every damaging row in this file has Accuracy above 0.
3. Single-target index = `Power * expected hits * accuracy factor`.
4. Field index = that value times 2 when Targeting is `AllEnemies` or `Field`, else times 1. The 2 is a stated 2v2 assumption that both foes are alive. A spread move into one living foe is worth half of this field index.
5. Per AP = field index / APCost when APCost is above 0. AP 0 is reported as free.

`COMBAT_Moves.txt` §6.1 still says rarity is complexity and availability, and that a Common move at max evolution should stay competitive with Epic in raw output. That sentence was not edited. The developer rule for this doc is narrower and overrides §6.1 for the flags below: a typeless Rare should sit roughly in line with a typed Common. A typed Common field index above a typeless Rare of the same AP and shape is a flag. No power, accuracy, hit count, or AP number was changed.

Two flag shapes:

- **Strictly worse as shipped.** Same AP, same shape (single-target or spread), and the higher-rarity move's field index is lower while its described upside is missing from the columns (see the correctness audit) and the lower-rarity move's described downside is also missing. The higher rarity then loses on the only number the battle uses.
- **Typeless Rare below a typed Common.** The refined Rare ladder sits under the typed Common power-64 band. Under the rule above, that gap is out of order. It is not "allowed by §6.1" for this screen. Fixing it means raising the refined Rares toward that Common band, or lowering the typed Commons once their missing riders exist. This pass only re-labels the flag.

Effect text is listed beside the index. It is not converted into bonus points, because many of those clauses do not run.

## Rarity and shape

| Rarity | Moves | Damaging |
|---|---:|---:|
| Common | 138 | 40 |
| Uncommon | 20 | 6 |
| Rare | 46 | 25 |
| Epic | 2 | 1 |

Epic damaging moves: `bulwark_blow` only. Epic non-damaging: `defensive_reflex` (20% incoming mitigation, drain 1 AP at end of turn). Both have real payloads.

## Damaging buckets

Field index, higher first, inside each AP and shape. Spread rows use the ×2 assumption.

### AP 0, single

| Move | Name | Rarity | Type | Power | Acc | Hits | Field index | Per AP | Payload |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| `all_in` | All-In | Common | Meme | 98 | 95 | 1.0 | 93.10 | free | — |

### AP 1, single

| Move | Name | Rarity | Type | Power | Acc | Hits | Field index | Per AP | Payload |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| `rapid_strike` | Rapid Strike | Common | Forked | 52 | 95 | 1.0 | 49.40 | 49.40 | — |
| `lean_hit` | Lean Hit | Common | Compressed | 46 | 95 | 1.0 | 43.70 | 43.70 | — |
| `baseline_jab` | Quick Jab | Common | None | 16 | 95 | 1.0 | 15.20 | 15.20 | — |

### AP 2, single

| Move | Name | Rarity | Type | Power | Acc | Hits | Field index | Per AP | Payload |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| `recoil_strike` | Recoil Strike | Common | Forked | 76 | 95 | 1.0 | 72.20 | 36.10 | — |
| `volatile_shot` | Volatile Shot | Common | Gas | 72 | 95 | 1.0 | 68.40 | 34.20 | — |
| `hammer_sync` | Hammer Sync | Rare | Node | 72 | 95 | 1.0 | 68.40 | 34.20 | cond x1 +0.1/count cap 1.3 |
| `cryo_ray` | Cryo Ray | Rare | Frozen | 72 | 95 | 1.0 | 68.40 | 34.20 | stage Speed:-2:0.25:false |
| `coordinated_shot` | Coordinated Shot | Common | Node | 64 | 95 | 1.0 | 60.80 | 30.40 | cond x1 +0.15/count cap 1.3 |
| `solid_strike` | Solid Strike | Common | Block | 64 | 95 | 1.0 | 60.80 | 30.40 | stage Speed:-1:0.25:false |
| `rallying_slam` | Rallying Slam | Rare | Block | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `cipher_shot` | Cipher Shot | Common | Encrypted | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `phantom_hack` | Phantom Hack | Rare | Encrypted | 64 | 95 | 1.0 | 60.80 | 30.40 | stage MeleeDef:-1:1.0:false|Speed:-1:1.0:false |
| `scorch_shot` | Scorch Shot | Common | Burned | 64 | 95 | 1.0 | 60.80 | 30.40 | stage MeleeDef:-1:0.25:false |
| `flame_charge` | Flame Charge | Uncommon | Burned | 64 | 95 | 1.0 | 60.80 | 30.40 | util GainAP:1:100%:Self:flame_charge_ap; cond x1.3 +0/count cap 1.3 |
| `frost_shot` | Frost Shot | Common | Frozen | 64 | 95 | 1.0 | 60.80 | 30.40 | stage Speed:-1:0.25:false |
| `momentum_drill` | Momentum Drill | Common | Mined | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `ore_strike` | Ore Strike | Common | Mined | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `deep_reap` | Deep Reap | Rare | Mined | 64 | 95 | 1.0 | 60.80 | 30.40 | util LifestealPercent:20%:deep_reap |
| `stake_strike` | Stake Strike | Common | Staked | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `yield_spike` | Yield Spike | Uncommon | Staked | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `precision_strike` | Precision Strike | Common | Compressed | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `chaos_strike` | Chaos Strike | Common | Glitched | 64 | 95 | 1.0 | 60.80 | 30.40 | cond x1 +0/count cap 0 rand 1.5|0.75 |
| `bug_payoff` | Bug Payoff | Common | Glitched | 64 | 95 | 1.0 | 60.80 | 30.40 | cond x1.5 +0/count cap 1.5 |
| `partition_shot` | Partition Shot | Common | Shard | 64 | 95 | 1.0 | 60.80 | 30.40 | status FrozenAssets:1:1.0: |
| `jackpot_shot` | Jackpot Shot | Common | Meme | 64 | 95 | 1.0 | 60.80 | 30.40 | — |
| `blade_flurry` | Blade Flurry | Rare | Bot | 18 | 95 | 3.0 | 51.30 | 25.65 | — |
| `zipped_strikes` | Zipped Strikes | Uncommon | Compressed | 26 | 95 | 2.0 | 49.40 | 24.70 | stage Speed:1:0.25:true |
| `hype_train` | Hype Train | Rare | Meme | 52 | 95 | 1.0 | 49.40 | 24.70 | — |
| `seam_cut` | Seam Cut | Rare | Shard | 52 | 95 | 1.0 | 49.40 | 24.70 | cond x1.25 |
| `combustive_shift` | Combustive Shift | Rare | Gas | 22 | 95 | 2.0 | 41.80 | 20.90 | — |
| `refined_striker` | Impact Knuckle | Rare | None | 42 | 90 | 1.0 | 37.80 | 18.90 | — |
| `refined_shot` | Vector Shot | Rare | None | 38 | 90 | 1.0 | 34.20 | 17.10 | — |
| `refined_thrust` | Rupture Point | Rare | None | 34 | 90 | 1.0 | 30.60 | 15.30 | stage MeleeDef:-1:0.25:false |
| `baseline_striker` | Snap Strike | Common | None | 30 | 95 | 1.0 | 28.50 | 14.25 | — |
| `multi_thread_shot` | Multi-Thread Shot | Common | Bot | 12 | 95 | 2.5 | 28.50 | 14.25 | util GainAP:1:10%:Self:multi_thread_shot |
| `baseline_shot` | Pulse Shot | Common | None | 26 | 95 | 1.0 | 24.70 | 12.35 | — |
| `baseline_thrust` | Pressure Point | Common | None | 26 | 95 | 1.0 | 24.70 | 12.35 | stage MeleeDef:-1:0.25:false |
| `quick_lunge` | Blink Lunge | Rare | None | 26 | 90 | 1.0 | 23.40 | 11.70 | — |
| `crystal_bash` | Crystal Bash | Rare | Shard | 0 | 95 | 1.0 | 0.00 | 0.00 | — |

### AP 2, spread

| Move | Name | Rarity | Type | Power | Acc | Hits | Field index | Per AP | Payload |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| `baseline_barrage` | Pellet Storm | Common | None | 10 | 95 | 2.5 | 47.50 | 23.75 | — |
| `refined_volley` | Scatterburst | Rare | None | 22 | 90 | 1.0 | 39.60 | 19.80 | — |
| `baseline_volley` | Scatter Shot | Common | None | 16 | 95 | 1.0 | 30.40 | 15.20 | — |
| `civet_beam` | Bean Beam | Uncommon | Staked | 12 | 95 | 1.0 | 22.80 | 11.40 | stage Speed:-1:1.0:false |

### AP 3, single

| Move | Name | Rarity | Type | Power | Acc | Hits | Field index | Per AP | Payload |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| `deep_dig` | Deep Dig | Common | Mined | 72 | 95 | 1.0 | 68.40 | 22.80 | — |
| `finish_burn` | Finish Burn | Common | Burned | 64 | 95 | 1.0 | 60.80 | 20.27 | cond x1.5 +0/count cap 1.5 |
| `ap_siphon` | AP Siphon | Common | Gas | 56 | 95 | 1.0 | 53.20 | 17.73 | — |
| `cipher_burst` | Cipher Burst | Rare | Encrypted | 22 | 95 | 2.5 | 52.25 | 17.42 | — |
| `cinder_burst` | Cinder Burst | Rare | Burned | 18 | 95 | 3.0 | 51.30 | 17.10 | stage MeleeAtk:-1:0.2:false |
| `chaotic_resonance` | Chaotic Resonance | Common | Glitched | 52 | 95 | 1.0 | 49.40 | 16.47 | cond x1 +0.15/count cap 2 |
| `pickaxe_flurry` | Pickaxe Flurry | Uncommon | Mined | 16 | 95 | 3.0 | 45.60 | 15.20 | — |
| `glitch_barrage` | Glitch Barrage | Rare | Glitched | 16 | 95 | 3.0 | 45.60 | 15.20 | — |
| `bulwark_blow` | Bulwark Blow | Epic | Block | 50 | 90 | 1.0 | 45.00 | 15.00 | cond atk MeleeDef |
| `interest_strike` | Interest Strike | Rare | Staked | 18 | 95 | 2.5 | 42.75 | 14.25 | — |
| `lucky_scatter` | Lucky Scatter | Rare | Meme | 18 | 95 | 2.5 | 42.75 | 14.25 | — |
| `compression_burst` | Compression Burst | Common | Compressed | 22 | 95 | 2.0 | 41.80 | 13.93 | stage MeleeDef:-1:1.0:false |
| `overdraw_snipe` | Rail Spike | Rare | None | 46 | 85 | 1.0 | 39.10 | 13.03 | — |
| `refined_slam` | Titan Crash | Rare | None | 48 | 80 | 1.0 | 38.40 | 12.80 | — |
| `stone_barrage` | Stone Barrage | Common | Block | 16 | 95 | 2.5 | 38.00 | 12.67 | stage Speed:-1:0.2:false |
| `frost_shards` | Frost Shards | Common | Frozen | 12 | 95 | 3.0 | 34.20 | 11.40 | stage Speed:-1:0.2:false |
| `chain_fork` | Chain Fork | Common | Forked | 12 | 95 | 3.0 | 34.20 | 11.40 | — |
| `guardbreaker_combo` | Breach Rush | Rare | None | 10 | 90 | 3.5 | 31.50 | 10.50 | — |
| `baseline_snipe` | Focus Shot | Common | None | 30 | 90 | 1.0 | 27.00 | 9.00 | — |
| `baseline_multi_strike` | Rapid Pummel | Common | None | 8 | 95 | 3.5 | 26.60 | 8.87 | — |
| `baseline_slam` | Body Check | Common | None | 30 | 85 | 1.0 | 25.50 | 8.50 | — |

### AP 3, spread

| Move | Name | Rarity | Type | Power | Acc | Hits | Field index | Per AP | Payload |
|---|---|---|---|---:|---:|---:|---:|---:|---|
| `fork_split` | Fork Split | Common | Forked | 46 | 95 | 1.0 | 87.40 | 29.13 | — |
| `shard_scatter` | Shard Scatter | Uncommon | Shard | 18 | 95 | 2.5 | 85.50 | 28.50 | — |
| `ember_shower` | Ember Shower | Common | Burned | 38 | 95 | 1.0 | 72.20 | 24.07 | stage MeleeAtk:-1:0.25:false |
| `automation_burst` | Automation Burst | Common | Bot | 38 | 95 | 1.0 | 72.20 | 24.07 | util RandomStageChange:-1:-1:25%:SingleEnemy:-6:automation_burst |
| `refined_barrage` | Ricochet Storm | Rare | None | 12 | 90 | 2.5 | 54.00 | 18.00 | — |
| `refined_blast` | Kinetic Burst | Rare | None | 28 | 90 | 1.0 | 50.40 | 16.80 | — |
| `baseline_blast` | Wide Burst | Common | None | 22 | 95 | 1.0 | 41.80 | 13.93 | — |

## Out of order

### Strictly worse as shipped

These lose on the field index to a lower rarity of the same AP and shape, and the correctness audit shows the higher rarity's described advantage is not in the data. The comparison move's described downside, when it has one, is also missing, so the gap is at least this wide.

| Higher rarity | Index | Same AP and shape, lower rarity | Index | Why it is strict |
|---|---:|---|---:|---|
| Higher rarity | Field index | Compared with | Field index | Note |
|---|---:|---|---:|---|
| `crystal_bash` (Rare, AP 2) | 0.00 | `recoil_strike` (Common) | 72.20 | Rare power is 0. Def scaling and the execute-style double are absent. Recoil Strike's recoil cost is also absent, so the Common is a 76-power hit. |
| `hype_train` (Rare, AP 2) | 49.40 | `solid_strike` (Common) | 60.80 | The Rare ramp is absent. Solid Strike's 25% Speed -1 is authored on the stage column. |
| `lucky_scatter` (Rare, AP 3) | 42.75 | `deep_dig` (Common) | 68.40 | The Rare random-buff rider is absent (18 power, 1–4 hits). Deep Dig's Def ignore is absent, so it is a 72-power hit. |
| `cipher_burst` (Rare, AP 3) | 52.25 | `deep_dig` (Common) | 68.40 | The Rare buff-erase is absent (22 power, 2–3 hits). Same comparison as Lucky Scatter. |
| `interest_strike` (Rare, AP 3) | 42.75 | `deep_dig` (Common) | 68.40 | The Rare AP-on-hit rider is absent (18 power, 2–3 hits). |
| `guardbreaker_combo` (Rare, AP 3) | 31.50 | `deep_dig` (Common) | 68.40 | Rare 10 power, 3–4 hits, no extra payload. Deep Dig is a larger plain hit. Barrier-shred text on the baseline multi-hit line is flavor; this Rare has the same gap. |

`bulwark_blow` (Epic, AP 3, field index 45.00, accuracy 90, attacks with MeleeDef) sits under several AP-3 Commons on the Atk index, including `deep_dig` at 68.40. The Def substitution is a real column (`DamageOverrideAttackStat`). On a wall Chainling the Epic can pass the Common. On an attacker the Atk index stays lower. Treat that gap as a design question. The payload is present.

### Spread ladder

| AP | Move | Rarity | Field index | Note |
|---:|---|---|---:|---|
| 2 | `baseline_barrage` | Common | 47.50 | Pellet barrage that batters barriers across the field. |
| 2 | `refined_volley` | Rare | 39.60 | Explosive scattershot that pressures all foes. |
| 2 | `baseline_volley` | Common | 30.40 | Wide spray that chips all foes. |
| 2 | `civet_beam` | Uncommon | 22.80 | Low-power beam hits all foes and drops their Speed. |
| 3 | `fork_split` | Common | 87.40 | Ranged attack that hits all foes. |
| 3 | `shard_scatter` | Uncommon | 85.50 | 2-3 AoE hits; each lowers Eva on a different foe. |
| 3 | `ember_shower` | Common | 72.20 | Hit all foes; each has 25% chance to lose Atk. |
| 3 | `automation_burst` | Common | 72.20 | Hit all foes; each has 25% chance to lose a random stat. |
| 3 | `refined_barrage` | Rare | 54.00 | Ricocheting volley that batters barriers across the field. |
| 3 | `refined_blast` | Rare | 50.40 | Kinetic wave for sweeping enemy-line pressure. |
| 3 | `baseline_blast` | Common | 41.80 | Broad burst for pressuring the enemy line. |

At AP 2, Common `baseline_barrage` (47.50) is above Rare `refined_volley` (39.60). At AP 3, typed Common `fork_split` (87.40) is above typeless Rare `refined_barrage` (54.00) and `refined_blast` (50.40). That second gap is a flag under the rule that a typeless Rare lines up with a typed Common. Pellet Storm is a typeless Common above a typeless Rare of the same AP, and it is flagged with them. Uncommon `civet_beam` (22.80) is the lowest AP-2 spread and is also the one whose Speed -1 rider is in the stage column. Uncommon `shard_scatter` (85.50) is close to Fork Split on the index, and its Eva rider is missing (correctness E-18). No spread power was changed.

The typeless baseline → refined ladder itself is ordered: Snap Strike 30 to Impact Knuckle 42, Body Check 30 to the Rare slam 48, and the wide Common burst 22 to Kinetic Burst 28. Under the developer rule, the typed Common band (power 64, accuracy 95, field index 60.80 at 2 AP) sitting on top of that Rare typeless line (Impact Knuckle 37.80, Vector Shot 34.20, Rupture Point 30.60) is a flag. A typeless Rare should land near those typed Commons. The numbers were not moved.

### Costs that do not exist

These Commons are the best hits in their AP bucket because the text's drawback is not implemented (correctness E-19):

| Move | AP | Field index | Missing cost |
|---|---:|---:|---|
| `all_in` | 0 | 93.10 | Spend all AP, recoil on hit or miss, +1 AP on KO. AP cost in the column is 0. |
| `recoil_strike` | 2 | 72.20 | 15% MaxHP recoil. |
| `volatile_shot` | 2 | 68.40 | 15% MaxHP on a miss. |

`all_in` is the largest single index in the file and it is free.

## Suggestions

Each line is a suggestion. None of these numbers were edited in the CSV.

1. **SUGGESTION — fix payloads before retiering.** Give `crystal_bash` a real power or a Def-scaling column, and give `recoil_strike`, `volatile_shot`, and `all_in` their costs, before comparing tiers. With the costs missing, those three Commons should not be the template for "what a Common is allowed to do."
2. **FLAG — typeless Rare should line up with typed Common.** Do not treat the power-64 typed Commons (field index 60.80 at 2 AP) above Impact Knuckle (42 power, index 37.80), Vector Shot (38, index 34.20), and Rupture Point (34, index 30.60) as allowed. §6.1 still says rarity is complexity and availability, and that a Common should stay competitive with Epic. This screen follows the developer rule instead, and it does not edit §6.1 or any power number. When a retune happens, raise the refined Rares toward the typed Common band or lower the typed Commons after their dead riders exist. Doing either while `all_in`, `recoil_strike`, and `volatile_shot` are still missing their costs will aim at the wrong Commons.
3. **FLAG — spread.** `baseline_barrage` (Common, multi-hit, index 47.50) is above Rare `refined_volley` (39.60) at 2 AP. `fork_split` (Common, index 87.40) is above Rare `refined_barrage` (54.00) and Rare `refined_blast` (50.40) at 3 AP. Those are the same typeless-Rare versus typed-or-plain-Common gap. `civet_beam` is the Uncommon at index 22.80; its Speed drop is real, and the power 12 is why the index is low. No spread number was changed.
4. **SUGGESTION — Epic.** `bulwark_blow` is the only Epic damage move (field index 45.00 at 3 AP, accuracy 90, attacks with MeleeDef). The Def column is real. On a wall Chainling it can pass an Atk-based Common. On an attacker the Atk index stays under several AP-3 Commons, including `deep_dig` at 68.40. Leave the identity until a retune. `defensive_reflex` is a real 20% mitigation passive with an AP drain. §6.1's "Common stays competitive with Epic" line is still in the spec and was not rewritten.
5. **SUGGESTION — do not spend rarity budget on empty text.** `hype_train`, `glitch_barrage`, `cipher_burst`, `lucky_scatter`, and `interest_strike` are Rare in the table and Common in the resolver. Implement the rider or drop the rarity until the rider exists.
6. **SUGGESTION — Ice Wall, Ash Guard, and Compound Guard** are support, not damage, and their mitigation does not run (correctness E-16, E-17). A balance pass that buffs their neighbors will not make those three walls work.

## Full move table

All 206 rows from `Moves.csv`, in file order. Field index is 0 for non-damaging moves. Payload lists the authored columns. Whether a clause runs is in the correctness audit.

| Move | Name | Category | Rarity | Type | AP | Power | Acc | Hits | Recharge | Target | Field index | Payload | Description |
|---|---|---|---|---|---:|---:|---:|---:|---:|---|---:|---|---|
| `baseline_striker` | Snap Strike | DamagingMelee | Common | None | 2 | 30 | 95 | 1.0 | 0 | SingleEnemy | 28.50 | — | Clean contact strike for steady pressure. |
| `baseline_slam` | Body Check | DamagingMelee | Common | None | 3 | 30 | 85 | 1.0 | 0 | SingleEnemy | 25.50 | — | Reckless body blow built for impact. |
| `baseline_jab` | Quick Jab | DamagingMelee | Common | None | 1 | 16 | 95 | 1.0 | 0 | SingleEnemy | 15.20 | — | Quick priority jab for finishing fragile targets. |
| `baseline_shot` | Pulse Shot | DamagingRanged | Common | None | 2 | 26 | 95 | 1.0 | 0 | SingleEnemy | 24.70 | — | Clean ranged pulse that avoids contact risk. |
| `baseline_snipe` | Focus Shot | DamagingRanged | Common | None | 3 | 30 | 90 | 1.0 | 0 | SingleEnemy | 27.00 | — | Focused ranged pick for exposed targets. |
| `baseline_volley` | Scatter Shot | DamagingRanged | Common | None | 2 | 16 | 95 | 1.0 | 0 | AllEnemies | 30.40 | — | Wide spray that chips all foes. |
| `baseline_blast` | Wide Burst | DamagingRanged | Common | None | 3 | 22 | 95 | 1.0 | 0 | AllEnemies | 41.80 | — | Broad burst for pressuring the enemy line. |
| `baseline_barrage` | Pellet Storm | DamagingRanged | Common | None | 2 | 10 | 95 | 2.5 | 0 | AllEnemies | 47.50 | — | Pellet barrage that batters barriers across the field. |
| `baseline_thrust` | Pressure Point | DamagingMelee | Common | None | 2 | 26 | 95 | 1.0 | 0 | SingleEnemy | 24.70 | stage MeleeDef:-1:0.25:false | Precise thrust with 25% chance to apply Def -1. |
| `baseline_multi_strike` | Rapid Pummel | DamagingMelee | Common | None | 3 | 8 | 95 | 3.5 | 0 | SingleEnemy | 26.60 | — | Rapid combo for breaking barriers and guard stacks. |
| `refined_striker` | Impact Knuckle | DamagingMelee | Rare | None | 2 | 42 | 90 | 1.0 | 0 | SingleEnemy | 37.80 | — | Crushing contact strike for decisive pressure. |
| `refined_slam` | Titan Crash | DamagingMelee | Rare | None | 3 | 48 | 80 | 1.0 | 0 | SingleEnemy | 38.40 | — | Devastating body crash with high commitment. |
| `quick_lunge` | Blink Lunge | DamagingMelee | Rare | None | 2 | 26 | 90 | 1.0 | 0 | SingleEnemy | 23.40 | — | Quick priority lunge for stealing a finish. |
| `refined_thrust` | Rupture Point | DamagingMelee | Rare | None | 2 | 34 | 90 | 1.0 | 0 | SingleEnemy | 30.60 | stage MeleeDef:-1:0.25:false | Piercing thrust with 25% chance to apply Def -1. |
| `guardbreaker_combo` | Breach Rush | DamagingMelee | Rare | None | 3 | 10 | 90 | 3.5 | 0 | SingleEnemy | 31.50 | — | Rapid contact combo for shredding barriers. |
| `refined_shot` | Vector Shot | DamagingRanged | Rare | None | 2 | 38 | 90 | 1.0 | 0 | SingleEnemy | 34.20 | — | Aimed kinetic shot for clean ranged pressure. |
| `overdraw_snipe` | Rail Spike | DamagingRanged | Rare | None | 3 | 46 | 85 | 1.0 | 0 | SingleEnemy | 39.10 | — | Piercing rail shot for high-commitment picks. |
| `refined_volley` | Scatterburst | DamagingRanged | Rare | None | 2 | 22 | 90 | 1.0 | 0 | AllEnemies | 39.60 | — | Explosive scattershot that pressures all foes. |
| `refined_blast` | Kinetic Burst | DamagingRanged | Rare | None | 3 | 28 | 90 | 1.0 | 0 | AllEnemies | 50.40 | — | Kinetic wave for sweeping enemy-line pressure. |
| `refined_barrage` | Ricochet Storm | DamagingRanged | Rare | None | 3 | 12 | 90 | 2.5 | 0 | AllEnemies | 54.00 | — | Ricocheting volley that batters barriers across the field. |
| `power_surge` | Power Surge | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeAtk:1:1.0:true | Raise own Atk +1 for contact pressure. |
| `defensive_stance` | Defensive Stance | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:1:1.0:true | Raise own Def +1 to blunt incoming contact damage. |
| `speed_boost` | Speed Boost | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage Speed:1:1.0:true | Raise own Speed +1 to act sooner. |
| `accuracy_focus` | Accuracy Focus | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Raise own Acc +2 for risky or evasive targets. |
| `evasion_maneuver` | Evasion Maneuver | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Raise own Eva +2 to dodge incoming attacks. |
| `power_drain` | Power Drain | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage MeleeAtk:-1:1.0:false | Lower target Atk by 1 to soften damage. |
| `defense_break` | Defense Break | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage MeleeDef:-1:1.0:false | Lower target Def by 1 to set up damage. |
| `speed_reduction` | Speed Reduction | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage Speed:-1:1.0:false | Lower target Speed by 1 to win turn order. |
| `accuracy_disrupt` | Accuracy Disrupt | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Lower target Acc by 1 to force misses. |
| `evasion_pin` | Evasion Pin | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Lower target Eva by 1 so allies can land hits. |
| `ap_boost` | AP Boost | StatusUtility | Common | None | 0 | 0 | 0 | 1.0 | 3 | Self | 0.00 | util GainAP:2:100%:Self:ap_boost; recharge 3 | Gain +2 AP now. Skips the next 2 turns. |
| `ap_drain` | AP Drain | StatusUtility | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Cut target AP by 1; 25% chance to steal it. |
| `cleanse` | Cleanse | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util Cleanse:3:1.0:SingleAlly | Remove up to 3 General duration debuffs from a target ally. |
| `position_swap` | Position Swap | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Priority swap with partner; redirects slot-based targeting. |
| `guard_posture` | Guard Posture | StatusUtility | Common | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util MitigationWindow:25%:2:1:wall_guard_posture | Reduce next hit by 25%; lasts 1 hit or 2 turns. |
| `broadcast` | Broadcast | StatusUtility | Common | None | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | status Broadcast:2:1.0: | Make your next move hit all foes; consumed on use. |
| `heal_ally` | Heal Ally | Support | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | heal 15% | Heal a target ally for 15% MaxHP. |
| `buff_ally` | Buff Ally | Support | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | stage MeleeAtk:1:1.0:false/MeleeDef:1:1.0:false | Give a target ally Atk +1 and Def +1. |
| `ap_donation` | AP Donation | Support | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Transfer paid AP to active partner; once per turn. |
| `damage_intercept` | Damage Intercept | Support | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Take the next single-target hit aimed at a partner. Holds through next turn. |
| `barrier_wall` | Barrier Wall | Support | Common | None | 3 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util MitigationWindow:25%:2:2:wall_barrier; heal 0% | Give ally 25% mitigation for 2 turns or 2 hits. |
| `effect_relay` | Effect Relay | Support | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Move one General debuff from ally to self; cleanse if none. |
| `ap_redistribution` | AP Redistribution | Support | Common | None | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Move up to 2 AP from one ally to another. |
| `rally_cry` | Rally Cry | Support | Common | None | 3 | 0 | 0 | 1.0 | 0 | AllAllies | 0.00 | stage Speed:1:1.0:false | All allies gain Speed +1; user gains +1 AP at turn end. |
| `turn_start_ap_gain` | Turn Start AP Gain | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util GainAP:1:100%:Self:turn_start_ap_gain | Passive - gain +1 AP at turn start. |
| `on_hit_momentum` | On Hit Momentum | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ChangeStage:MeleeAtk:1:25%:Self:3:false:on_hit_momentum | Passive - hits have 25% chance to raise Atk +1; cap +3. |
| `on_crit_bonus` | On Crit Bonus | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - crits grant +1 AP. |
| `defensive_reflex` | Defensive Reflex | Passive | Epic | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util IncomingDamageMitigation:20%:Any:defensive_reflex/DrainAP:1:100%:Self:defensive_reflex_ap | Passive - hits deal 20% less damage. Lose 1 AP at end of turn. |
| `sustain_regen` | Sustain Regen | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | heal 3% | Passive - heal 3% MaxHP at end of turn. |
| `deep_sustain` | Deep Sustain | Passive | Rare | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | heal 6% | Passive - heal 6% MaxHP at end of turn. |
| `status_resistance` | Status Resistance | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - General debuffs expire 1 turn faster. |
| `emergency_recovery` | Emergency Recovery | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | heal 15% | Passive - below 25% HP heal 15% MaxHP once per battle. |
| `precision_focus` | Precision Focus | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - a miss has a 75% chance to refund 1 AP. |
| `reactive_ping` | Reactive Ping | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - a miss deals 5% MaxHP to the attacker. |
| `low_ap_surge` | Low AP Surge | Passive | Common | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ChangeStage:Speed:1:100%:Self:6:false:low_ap_surge | Passive - below 2 AP after turn AP gain Speed +1. |
| `low_ap_burst` | Low AP Burst | Passive | Rare | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ChangeStage:Speed:3:100%:Self:6:false:low_ap_burst | Passive - below 2 AP after turn AP gain Speed +3. |
| `spite_counter` | Spite Counter | Passive | Rare | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - 33% chance to mirror a received debuff onto the attacker. |
| `desperation_surge` | Desperation Surge | Passive | Rare | None | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeAtk:2:1.0:true; util GainAP:1:100%:Self:desperation_surge | Passive - below 33% HP gain Atk +2 and +1 AP once. |
| `momentum_shift` | Momentum Shift | StatusUtility | Uncommon | None | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage Speed:2:1.0:true/MeleeAtk:1:1.0:true | If you were hit last turn gain Speed +2 and Atk +1. |
| `relay_packet` | Relay Packet | Support | Common | Node | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Move one General buff or debuff between allies. |
| `hub_heal` | Hub Heal | Support | Common | Node | 3 | 0 | 0 | 1.0 | 0 | AllAllies | 0.00 | heal 15% | Heal all allies for 15% MaxHP. |
| `ap_router` | AP Router | Support | Common | Node | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Move up to 2 AP between any two allies. |
| `signal_redirect` | Signal Redirect | Support | Common | Node | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | stage MeleeDef:1:1.0:true | Redirect the next hit on an ally to yourself and gain Def +1. Holds through next turn. |
| `network_sync` | Network Sync | StatusUtility | Common | Node | 2 | 0 | 0 | 1.0 | 0 | AllAllies | 0.00 | — | All allies raise their lowest stage stat by +1. |
| `coordinated_shot` | Coordinated Shot | DamagingRanged | Common | Node | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | cond x1 +0.15/count cap 1.3 | Ranged hit gains +15% damage per buffed partner. |
| `extend_protocol` | Extend Protocol | StatusUtility | Common | Node | 1 | 0 | 0 | 1.0 | 3 | SingleAlly | 0.00 | recharge 3 | Extend one ally buff by 1 turn. Skips the next 2 turns. |
| `network_response` | Network Response | StatusUtility | Common | Node | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ArmUtilityWindow:EnemyMiss:1:1:1:0%:Speed:1:Self:0%:false:0:network_response | If an enemy misses this turn gain +1 AP and Speed +1. |
| `overflow_reboot` | Overflow Reboot | StatusUtility | Rare | Node | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ArmUtilityWindow:FoeKO:1:1:1:10%:MeleeAtk:0:Self:0%:true:0:overflow_reboot; heal 10% | If a foe faints this turn gain +1 AP and heal allies 10%. |
| `fuel_surge` | Fuel Surge | StatusUtility | Common | Gas | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | status GasSpike:2:1.0:Self; util GainAP:3:100%:Self:fuel_surge | Gain +3 AP; also gain Atk +2 and Def -2 for 2 turns. |
| `transaction_tax` | Transaction Tax | StatusUtility | Common | Gas | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Make target lose 1 AP; gain it if they had more AP. |
| `volatile_shot` | Volatile Shot | DamagingRanged | Common | Gas | 2 | 72 | 95 | 1.0 | 0 | SingleEnemy | 68.40 | — | High-power ranged hit; miss costs you 15% MaxHP. |
| `slipstream` | Slipstream | StatusUtility | Common | Gas | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage Speed:2:1.0:true/MeleeDef:-1:1.0:true | Gain Speed +2 but drop Def -1. |
| `gas_cloud` | Gas Cloud | StatusUtility | Common | Gas | 2 | 0 | 0 | 1.0 | 0 | AllEnemies | 0.00 | — | All foes lose Acc -1 and Eva -1. |
| `fee_collector` | Fee Collector | Passive | Rare | Gas | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util DrainAP:1:100%:AllEnemies:fee_collector | Passive - foes with more AP lose 1 AP at your turn start. |
| `ap_siphon` | AP Siphon | DamagingRanged | Common | Gas | 3 | 56 | 95 | 1.0 | 0 | SingleEnemy | 53.20 | — | Ranged hit with 50% chance to drain 1 AP. |
| `combustive_shift` | Combustive Shift | DamagingRanged | Rare | Gas | 2 | 22 | 95 | 2.0 | 0 | SingleEnemy | 41.80 | — | 2 hits; bonus damage vs debuffed targets missing AP. |
| `vapor_barrier` | Vapor Barrier | Support | Uncommon | Gas | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util FullAbsorbBarrier:2:1:wall_vapor | Block the next hit within 2 turns. |
| `immovable_stance` | Immovable Stance | StatusUtility | Common | Block | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | status Immovable:2:1.0:Self | Gain Def +2 and forced-swap immunity for 2 turns. |
| `chain_seal` | Chain Seal | StatusUtility | Common | Block | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage Speed:-1:1.0:false | Lower target Speed and Eva by 1. |
| `dust_fees` | Dust Fees | StatusUtility | Common | Block | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Lower target Acc and Eva by 1. |
| `solid_strike` | Solid Strike | DamagingMelee | Common | Block | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | stage Speed:-1:0.25:false | Contact hit with 25% chance to lower Speed. |
| `blockade` | Blockade | Support | Common | Block | 3 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | — | Give ally 25% mitigation for 2 turns. |
| `immutable_ledger` | Immutable Ledger | Passive | Uncommon | Block | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - the first hit each turn deals 20% less damage and grants +1 AP. |
| `stone_barrage` | Stone Barrage | DamagingMelee | Common | Block | 3 | 16 | 95 | 2.5 | 0 | SingleEnemy | 38.00 | stage Speed:-1:0.2:false | 2-3 hits; each can lower Speed; cap -2. |
| `guard_stance` | Guard Stance | StatusUtility | Common | Block | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:2:1.0:true | Gain Def +2; melee contact takes +10% recoil for 3 turns. |
| `rallying_slam` | Rallying Slam | DamagingMelee | Rare | Block | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Contact hit that grants +1 AP if Def stage exceeds Atk. |
| `proof_shield` | Proof Shield | Support | Common | Encrypted | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | status EncryptedSeal:1:1.0: | Give ally Encrypted Seal for debuff immunity this turn. |
| `purge_protocol` | Purge Protocol | StatusUtility | Common | Encrypted | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | stage MeleeDef:1:1.0:false | Cleanse all ally debuffs and grant Def +1. |
| `verification_lock` | Verification Lock | StatusUtility | Common | Encrypted | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status FUD:2:1.0:; util NextMoveAPSurcharge:1:100%:SingleEnemy:verification_lock | Apply FUD; target next move costs +1 AP. |
| `cipher_shot` | Cipher Shot | DamagingRanged | Common | Encrypted | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Ranged hit with 25% chance to block target next buff. |
| `key_rotation` | Key Rotation | StatusUtility | Common | Encrypted | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:1:1.0:true | Gain Def +1 and Eva +1. |
| `firewall_passive` | Firewall Passive | Passive | Uncommon | Encrypted | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util IncomingDamageMitigation:25%:Projectile:firewall_passive | Passive - projectile hits including spread deal 25% less damage. |
| `system_reset` | System Reset | StatusUtility | Common | Encrypted | 3 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Strip all target buffs and debuffs; skip your next action. |
| `memory_leak` | Memory Leak | StatusUtility | Rare | Encrypted | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage Speed:2:1.0:true; util ArmUtilityWindow:OwnerTurnStart:1:1:-2:0%:MeleeAtk:0:SingleEnemy:0%:false:0:memory_leak | Target loses 2 AP next turn; user gains Speed +2. |
| `phantom_hack` | Phantom Hack | DamagingRanged | Rare | Encrypted | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | stage MeleeDef:-1:1.0:false/Speed:-1:1.0:false | Hit the target; if it has no buffs lower Def and Speed. |
| `cipher_burst` | Cipher Burst | DamagingRanged | Rare | Encrypted | 3 | 22 | 95 | 2.5 | 0 | SingleEnemy | 52.25 | — | 2-3 hits; each has 25% chance to erase one target buff. |
| `deflation_mark` | Deflation Mark | StatusUtility | Common | Burned | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status BurnedTokens:2:1.0:; stage MeleeAtk:-1:1.0:false | Apply Burned Tokens so enemy DoT heals you; lower Atk -1. |
| `scorch_shot` | Scorch Shot | DamagingRanged | Common | Burned | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | stage MeleeDef:-1:0.25:false | Ranged hit with 25% chance to lower Def. |
| `sear_field` | Sear Field | StatusUtility | Common | Burned | 2 | 0 | 0 | 1.0 | 0 | AllEnemies | 0.00 | status SearField:1:1.0: | All foes lose Atk -1 for the turn. |
| `hype_burst` | Hype Burst | StatusUtility | Common | Burned | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Remove up to 2 target buffs; gain Atk +1 for each. |
| `ash_guard` | Ash Guard | Support | Common | Burned | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util MitigationWindow:20%:2:0:wall_ash; heal 5% | Give an ally 20% mitigation for 2 turns. |
| `finish_burn` | Finish Burn | DamagingRanged | Common | Burned | 3 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | cond x1.5 +0/count cap 1.5 | Ranged finisher deals +50% damage below 40% target HP. |
| `ember_shower` | Ember Shower | DamagingRanged | Common | Burned | 3 | 38 | 95 | 1.0 | 0 | AllEnemies | 72.20 | stage MeleeAtk:-1:0.25:false | Hit all foes; each has 25% chance to lose Atk. |
| `flame_charge` | Flame Charge | DamagingRanged | Uncommon | Burned | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | util GainAP:1:100%:Self:flame_charge_ap; cond x1.3 +0/count cap 1.3 | If foe has 0 AP deal +30% damage and gain +1 AP. |
| `ember_wall` | Ember Wall | Support | Rare | Burned | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util FullAbsorbBarrier:2:3:wall_ember/WallHitRetaliation:1:wall_ember | Barrier ally for 2 turns; attackers lose 1 AP when they hit it. |
| `cinder_burst` | Cinder Burst | DamagingRanged | Rare | Burned | 3 | 18 | 95 | 3.0 | 0 | SingleEnemy | 51.30 | stage MeleeAtk:-1:0.2:false | 2-4 hits; each has 20% chance to lower Atk. |
| `cold_snap` | Cold Snap | StatusUtility | Common | Frozen | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage Speed:-2:1.0:false | Lower target Speed by 2. |
| `lock_assets` | Lock Assets | StatusUtility | Common | Frozen | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status FrozenAssets:1:1.0: | Apply Frozen Assets next turn; target cannot swap. |
| `stasis_field` | Stasis Field | StatusUtility | Common | Frozen | 2 | 0 | 0 | 1.0 | 0 | Field | 0.00 | util FieldEntryStageChange:Speed:-1:2:100%:stasis_field | Field effect for 2 turns; entrants lose Speed -1. |
| `ice_wall` | Ice Wall | Support | Common | Frozen | 3 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util MitigationWindow 25% turn 1 hit 0, plus GainApOnDamageIfSpeedHigher +1 | Give an ally 25% mitigation for the rest of this turn. If you are faster, gain +1 AP when hit. |
| `frost_shot` | Frost Shot | DamagingRanged | Common | Frozen | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | stage Speed:-1:0.25:false | Ranged hit with 25% chance to lower Speed. |
| `hibernate` | Hibernate | StatusUtility | Common | Frozen | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:2:1.0:true; heal 25% | Skip action to heal 25% MaxHP and gain Def +2. |
| `frost_shards` | Frost Shards | DamagingRanged | Common | Frozen | 3 | 12 | 95 | 3.0 | 0 | SingleEnemy | 34.20 | stage Speed:-1:0.2:false | 2 hits guaranteed; possible 3-4; each can lower Speed. |
| `chilling_retort` | Chilling Retort | StatusUtility | Rare | Frozen | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ArmUtilityWindow:IncomingDebuff:1:1:0:0%:MeleeAtk:0:Self:0%:false:0:chilling_retort | This turn reflect the next enemy debuff back at them. |
| `crystal_focus` | Crystal Focus | StatusUtility | Uncommon | Frozen | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage Speed:1:1.0:true; heal 10% | At 0 AP heal 10% MaxHP and gain Speed +1. |
| `rapid_strike` | Rapid Strike | DamagingMelee | Common | Forked | 1 | 52 | 95 | 1.0 | 0 | SingleEnemy | 49.40 | — | Priority contact hit for finishing before normal moves. |
| `fork_split` | Fork Split | DamagingRanged | Common | Forked | 3 | 46 | 95 | 1.0 | 0 | AllEnemies | 87.40 | — | Ranged attack that hits all foes. |
| `voltage_rush` | Voltage Rush | StatusUtility | Common | Forked | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | status Overleveraged:2:0.25:Self; stage Speed:3:1.0:true | Gain Speed +3; 25% chance to self-Overleverage. |
| `risk_spike` | Risk Spike | StatusUtility | Common | Forked | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeAtk:2:1.0:true/MeleeDef:-1:1.0:true | Gain Atk +2 and Def -1; next hit +25% damage and +10% recoil. |
| `recoil_strike` | Recoil Strike | DamagingMelee | Common | Forked | 2 | 76 | 95 | 1.0 | 0 | SingleEnemy | 72.20 | — | 76-power contact hit; take 15% MaxHP recoil. |
| `pivot_line` | Pivot Line | StatusUtility | Common | Forked | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage Speed:1:1.0:true | Gain Acc +1; Eva +1; Speed +1. |
| `chain_fork` | Chain Fork | DamagingMelee | Common | Forked | 3 | 12 | 95 | 3.0 | 0 | SingleEnemy | 34.20 | — | 2-4 contact hits; guaranteed 2 with chances for 3-4. |
| `conductive_counter` | Conductive Counter | Passive | Rare | Forked | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - once a turn counter 15% of contact damage taken and gain +1 AP. |
| `charged_relay` | Charged Relay | StatusUtility | Rare | Forked | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util GainApIfDistinctTargetsHitAtLeast:2:2:charged_relay; heal 5% | Heal 5%; if you hit 2+ targets this turn gain +2 AP. |
| `surge_recovery` | Surge Recovery | Support | Uncommon | Forked | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util BonusHealPercentIfSpeedHigher:10; heal 15% | Heal self 15% MaxHP. Heal +10% more if your Speed beats any active foe. |
| `extraction_hit` | Extraction Hit | Passive | Common | Mined | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - hits can raise Atk; at Atk +3 also gain Speed. |
| `momentum_drill` | Momentum Drill | DamagingMelee | Common | Mined | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Contact hit deals +25% damage to debuffed targets. |
| `majority_pressure` | Majority Pressure | StatusUtility | Common | Mined | 2 | 0 | 0 | 1.0 | 0 | AllEnemies | 0.00 | status FiftyOnePercentAttacked:1:1.0: | Foes lose Speed for the rest of this turn. |
| `exploit_window` | Exploit Window | StatusUtility | Common | Mined | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status ExploitMark:2:1.0: | Mark target for 2 turns so allies deal +15% damage. |
| `ore_strike` | Ore Strike | DamagingMelee | Common | Mined | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Contact hit gains +10% damage per own buff; cap +30%. |
| `refinery` | Refinery | StatusUtility | Common | Mined | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:1:1.0:true | Gain Def +1 and +1 AP at turn end for 2 turns. |
| `deep_dig` | Deep Dig | DamagingMelee | Common | Mined | 3 | 72 | 95 | 1.0 | 0 | SingleEnemy | 68.40 | — | Heavy contact hit that ignores 50% of target Def stages. |
| `crystal_extraction` | Crystal Extraction | StatusUtility | Rare | Mined | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | heal 5% | Heal 5% MaxHP per target debuff; then remove them. |
| `deep_reap` | Deep Reap | DamagingRanged | Rare | Mined | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | util LifestealPercent:20%:deep_reap | Below 50% target HP heal 20% of damage dealt. |
| `pickaxe_flurry` | Pickaxe Flurry | DamagingMelee | Uncommon | Mined | 3 | 16 | 95 | 3.0 | 0 | SingleEnemy | 45.60 | — | 2-4 hits; each can raise Atk; cap +2. |
| `ore_restoration` | Ore Restoration | Support | Uncommon | Mined | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util Cleanse:1:1.0:SingleAlly; heal 20% | Heal ally 20% MaxHP and cleanse up to 1 General debuff. |
| `yield_cycle` | Yield Cycle | StatusUtility | Common | Staked | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | status Yield:3:1.0:Self | Gain +1 AP at end of each turn for 3 turns. |
| `stake_heal` | Stake Heal | Support | Common | Staked | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | heal 25% | Heal a target ally for 25% MaxHP. |
| `compound_guard` | Compound Guard | Support | Common | Staked | 3 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | stage MeleeDef:1:1.0:false; util MitigationWindow:20%:2:0:wall_compound | Give an ally 20% mitigation for 2 turns and Def +1. |
| `ap_reserve` | AP Reserve | StatusUtility | Common | Staked | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util BankAPUntilNextTurn:5:1:ap_reserve | Bank up to 5 AP now; release it next turn with +1 bonus. |
| `patience_passive` | Patience Passive | Passive | Uncommon | Staked | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ChangeStage:MeleeDef:1:100%:Self:6:false:patience_def | Passive - end of turn grants Def +1. No attack heals 5% MaxHP. |
| `stake_strike` | Stake Strike | DamagingMelee | Common | Staked | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Contact hit gains +10% damage per own Def stage; cap +30%. |
| `compound_returns` | Compound Returns | StatusUtility | Common | Staked | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeAtk:2:1.0:true; heal 10% | If unharmed last turn gain Atk +2 and heal 10% MaxHP. |
| `yield_spike` | Yield Spike | DamagingMelee | Uncommon | Staked | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | At 4+ AP deal +25% damage then spend 1 AP. |
| `stake_refund` | Stake Refund | Passive | Rare | Staked | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util GainAP:1:100%:Self:stake_refund | Passive - when your buff expires regain 1 AP. |
| `interest_strike` | Interest Strike | DamagingMelee | Rare | Staked | 3 | 18 | 95 | 2.5 | 0 | SingleEnemy | 42.75 | — | 2-3 hits; each can grant AP if Def stage exceeds Atk. |
| `precision_strike` | Precision Strike | DamagingRanged | Common | Compressed | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Accurate ranged hit with +20% accuracy. |
| `compression_trade` | Compression Trade | StatusUtility | Common | Compressed | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeAtk:2:1.0:true/Speed:-1:1.0:true | Gain Atk +2 but lose Speed -1. |
| `optimize_aim` | Optimize Aim | StatusUtility | Common | Compressed | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Gain Acc +2. |
| `zip_step` | Zip Step | StatusUtility | Common | Compressed | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:-1:1.0:true | Gain Eva +2 but lose Def -1. |
| `risk_marker` | Risk Marker | StatusUtility | Common | Compressed | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status Overleveraged:2:1.0: | Apply Overleveraged so heavy hits make target take recoil. |
| `lean_hit` | Lean Hit | DamagingMelee | Common | Compressed | 1 | 46 | 95 | 1.0 | 0 | SingleEnemy | 43.70 | — | Lean contact jab for efficient pressure. |
| `compression_burst` | Compression Burst | DamagingMelee | Common | Compressed | 3 | 22 | 95 | 2.0 | 0 | SingleEnemy | 41.80 | stage MeleeDef:-1:1.0:false | 2 contact hits; first hit lowers target Def. |
| `zipped_strikes` | Zipped Strikes | DamagingRanged | Uncommon | Compressed | 2 | 26 | 95 | 2.0 | 0 | SingleEnemy | 49.40 | stage Speed:1:0.25:true | 2 ranged hits; each has 25% chance to raise Speed. |
| `streamlined_flow` | Streamlined Flow | Passive | Rare | Compressed | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | stage MeleeDef:-1:1.0:true; util MoveAPDiscountWhileHPAbove:1:70:streamlined_discount/ChangeStage:MeleeDef:-1:100%:Self:-6:false:streamlined_def | Passive - above 70% HP moves cost -1 AP but Def falls -1. |
| `debuff_script` | Debuff Script | StatusUtility | Common | Bot | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage MeleeAtk:-1:1.0:false/MeleeDef:-1:1.0:false | Lower target Atk and Def by 1. |
| `bot_swarm` | Bot Swarm | StatusUtility | Common | Bot | 2 | 0 | 0 | 1.0 | 0 | AllEnemies | 0.00 | status BotSwarm:1:1.0: | Foes lose Atk and Def for the rest of this turn. |
| `mev_extract` | MEV Extract | StatusUtility | Common | Bot | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status MEVExtracted:2:1.0: | Apply MEV Extracted; steal 1 AP per turn for 2 turns. |
| `automated_pressure` | Automated Pressure | Passive | Common | Bot | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util RandomStageChange:-1:-1:100%:RandomEnemy:-6:automated_pressure | Passive - end of turn lowers one random stat on a random foe by 1. |
| `multi_thread_shot` | Multi-Thread Shot | DamagingRanged | Common | Bot | 2 | 12 | 95 | 2.5 | 0 | SingleEnemy | 28.50 | util GainAP:1:10%:Self:multi_thread_shot | 2-3 ranged hits; each has 10% chance to gain +1 AP. |
| `automation_burst` | Automation Burst | DamagingRanged | Common | Bot | 3 | 38 | 95 | 1.0 | 0 | AllEnemies | 72.20 | util RandomStageChange:-1:-1:25%:SingleEnemy:-6:automation_burst | Hit all foes; each has 25% chance to lose a random stat. |
| `script_loop` | Script Loop | StatusUtility | Common | Bot | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | If target has 2+ debuffs extend all by 1 turn. |
| `recursive_backup` | Recursive Backup | StatusUtility | Rare | Bot | 2 | 0 | 0 | 1.0 | 0 | Self | 0.00 | heal 20% | Below 50% HP heal 20% at turn end and reset durations. |
| `thread_lock` | Thread Lock | StatusUtility | Rare | Bot | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | status ThreadLock:1:1.0: | Apply Thread Lock so target cannot gain AP next turn. |
| `self_repair_protocol` | Self-Repair Protocol | Support | Uncommon | Bot | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | heal 15% | Immediate self-repair: heal 15% MaxHP. |
| `defense_corrupt` | Defense Corrupt | StatusUtility | Common | Glitched | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage MeleeDef:-2:1.0:false | Lower target Def by 2. |
| `chaos_strike` | Chaos Strike | DamagingMelee | Common | Glitched | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | cond x1 +0/count cap 0 rand 1.5/0.75 | Contact hit swings high or low with +50% or -25% damage. |
| `chaotic_resonance` | Chaotic Resonance | DamagingRanged | Common | Glitched | 3 | 52 | 95 | 1.0 | 0 | SingleEnemy | 49.40 | cond x1 +0.15/count cap 2 | Ranged hit gains +15% damage per target status. |
| `unstable_code` | Unstable Code | Passive | Common | Glitched | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util RandomStageChange:1:2:100%:Self:6:unstable_code | Passive - turn start can raise a random stat by +1 or +2. |
| `bug_payoff` | Bug Payoff | DamagingMelee | Common | Glitched | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | cond x1.5 +0/count cap 1.5 | Deal +50% damage if target has 2+ debuffs. |
| `crash_routine` | Crash Routine | StatusUtility | Common | Glitched | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage MeleeAtk:-1:1.0:false | Below 50% target HP deal 15% MaxHP damage; otherwise lower Atk. |
| `glitch_feedback` | Glitch Feedback | StatusUtility | Common | Glitched | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | util NextMoveAPSurcharge:1:100%:SingleEnemy:glitch_feedback | Remove 1 target debuff; their next move costs +1 AP. |
| `data_scramble` | Data Scramble | StatusUtility | Rare | Glitched | 0 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | stage MeleeAtk:-1:1.0:true/Speed:-1:1.0:true; util GainAP:2:100%:Self:data_scramble | Both lose Atk and Speed -1; you gain +2 AP. |
| `bug_fix` | Bug Fix | StatusUtility | Uncommon | Glitched | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | heal 10% | Heal 10%; cleanse 1 debuff; deal -20% damage next turn. |
| `glitch_barrage` | Glitch Barrage | DamagingRanged | Rare | Glitched | 3 | 16 | 95 | 3.0 | 0 | SingleEnemy | 45.60 | — | 2-4 hits; each randomly hits target or self. |
| `error_correction` | Error Correction | Support | Rare | Glitched | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util Cleanse:1:0.5:SingleAlly; heal 15% | Heal ally 15% MaxHP; 50% chance to cleanse 1 General debuff. |
| `shard_partition` | Shard Partition | StatusUtility | Common | Shard | 2 | 0 | 0 | 1.0 | 0 | AllAllies | 0.00 | status ShardPartition:2:1.0: | Apply Shard Partition; team gains Speed +1 for 2 turns. |
| `partition_shot` | Partition Shot | DamagingRanged | Common | Shard | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | status FrozenAssets:1:1.0: | Ranged hit applies Frozen Assets next turn. |
| `team_buffer` | Team Buffer | Support | Common | Shard | 3 | 0 | 0 | 1.0 | 0 | AllAllies | 0.00 | stage MeleeDef:1:1.0:false | All allies gain Def +1. |
| `field_split` | Field Split | StatusUtility | Common | Shard | 2 | 0 | 0 | 1.0 | 0 | Field | 0.00 | util FieldEntryStageChange:Evasion:-1:2:100%:field_split | For 2 turns foes entering field lose Eva -1. |
| `isolation_tool` | Isolation Tool | StatusUtility | Common | Shard | 1 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Force target next move to be single-target. |
| `parallel_passive` | Parallel Passive | Passive | Common | Shard | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | util ChangeStage:Speed:1:25%:Self:2:false:parallel_passive | Passive - ally attacks can raise your Speed; cap +2. |
| `shattered_armor` | Shattered Armor | Passive | Common | Shard | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - a super-effective hit grants 5% mitigation this turn and +1 AP. |
| `crystal_bash` | Crystal Bash | DamagingMelee | Rare | Shard | 2 | 0 | 95 | 1.0 | 0 | SingleEnemy | 0.00 | — | Damage scales from your Def; doubles below 30% target HP. |
| `lattice_guard` | Lattice Guard | Support | Uncommon | Shard | 1 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | util DamageThresholdBarrier:40:2:0:20%:wall_lattice | 40-damage barrier for ally; reflects 20% absorbed damage. |
| `shard_scatter` | Shard Scatter | DamagingRanged | Uncommon | Shard | 3 | 18 | 95 | 2.5 | 0 | AllEnemies | 85.50 | — | 2-3 AoE hits; each lowers Eva on a different foe. |
| `viral_surge` | Viral Surge | StatusUtility | Common | Meme | 1 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Randomly gain +2 AP or lose 1 AP; always gain +1 random stat. |
| `dice_roll` | Dice Roll | StatusUtility | Common | Meme | 1 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | heal 20% | Random support can heal ally; debuff foe; or give ally +2 AP. |
| `jackpot_shot` | Jackpot Shot | DamagingRanged | Common | Meme | 2 | 64 | 95 | 1.0 | 0 | SingleEnemy | 60.80 | — | Ranged hit rolls for double; normal; or half damage. |
| `all_in` | All-In | DamagingMelee | Common | Meme | 0 | 98 | 95 | 1.0 | 0 | SingleEnemy | 93.10 | — | Spend all AP for huge contact hit; recoil on hit or miss; +1 AP on KO. |
| `wildcard_swap` | Wildcard Swap | StatusUtility | Common | Meme | 2 | 0 | 0 | 1.0 | 0 | SingleEnemy | 0.00 | — | Randomly swap one of your stage changes with target. |
| `double_or_nothing` | Double-or-Nothing | Passive | Common | Meme | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - end turn 60% +1 AP; 40% -1 AP and AP-drain immunity. |
| `meme_momentum` | Meme Momentum | Passive | Rare | Meme | 0 | 0 | 0 | 1.0 | 0 | Self | 0.00 | — | Passive - a landed hit raises both Atk by 2 and grants +2 AP. A miss drops them by 1. |
| `hype_train` | Hype Train | DamagingRanged | Rare | Meme | 2 | 52 | 95 | 1.0 | 0 | SingleEnemy | 49.40 | — | Consecutive hits gain +20% damage; cap +100%; miss resets. |
| `lucky_scatter` | Lucky Scatter | DamagingRanged | Rare | Meme | 3 | 18 | 95 | 2.5 | 0 | SingleEnemy | 42.75 | — | 1-4 hits; each has 25% chance to grant a random buff. |
| `meme_magic` | Meme Magic | Support | Uncommon | Meme | 2 | 0 | 0 | 1.0 | 0 | SingleAlly | 0.00 | heal 20% | Heal ally 20% MaxHP with unreliable meme energy. |
| `hammer_sync` | Hammer Sync | DamagingMelee | Rare | Node | 2 | 72 | 95 | 1.0 | 0 | SingleEnemy | 68.40 | cond x1 +0.1/count cap 1.3 | Damage rises +10% per buffed partner; cap +30%. |
| `pack_rally` | Pack Rally | Support | Uncommon | Node | 3 | 0 | 0 | 1.0 | 0 | AllAllies | 0.00 | stage MeleeAtk:1:1.0:false/Speed:1:1.0:false | All allies gain Atk +1 and Speed +1. |
| `cryo_ray` | Cryo Ray | DamagingRanged | Rare | Frozen | 2 | 72 | 95 | 1.0 | 0 | SingleEnemy | 68.40 | stage Speed:-2:0.25:false | Heavy ranged hit with 25% chance to lower Speed by 2. |
| `gecko_guard` | Gecko Guard | Passive | Rare | Frozen | 0 | 0 | 0 | 1.0 | 999 | Self | 0.00 | stage MeleeDef:2:1.0:true/Speed:1:1.0:true; recharge 999 | Passive - below 50% HP gain Def +2 and Speed +1 once. |
| `blade_flurry` | Blade Flurry | DamagingMelee | Rare | Bot | 2 | 18 | 95 | 3.0 | 0 | SingleEnemy | 51.30 | — | 3-hit contact combo. |
| `fox_feint` | Fox Feint | StatusUtility | Rare | Bot | 1 | 0 | 0 | 1.0 | 999 | Self | 0.00 | util ArmUtilityWindow:IncomingHit:999:1:0:0%:MeleeAtk:0:Self:10%:false:0:fox_feint; recharge 999 | Dodge and counter once per battle. |
| `bean_sip` | Bean Sip | StatusUtility | Uncommon | Staked | 1 | 0 | 0 | 1.0 | 2 | Self | 0.00 | stage Speed:1:1.0:true; heal 8%; util GainAP:1:100%:Self:bean_sip; recharge 2 | Priority sip. Heal 8% MaxHP, raise Speed +1, and gain +1 AP. Recharges for 1 turn. |
| `civet_beam` | Bean Beam | DamagingRanged | Uncommon | Staked | 2 | 12 | 95 | 1.0 | 0 | AllEnemies | 22.80 | stage Speed:-1:1.0:false | Low-power beam hits all foes and drops their Speed. |
| `bulwark_blow` | Bulwark Blow | DamagingMelee | Epic | Block | 3 | 50 | 90 | 1.0 | 0 | SingleEnemy | 45.00 | cond atk MeleeDef | Contact hit that strikes with Def instead of Atk. |
| `seam_cut` | Seam Cut | DamagingMelee | Rare | Shard | 2 | 52 | 95 | 1.0 | 0 | SingleEnemy | 49.40 | cond x1.25 | Precise contact cut. Deals more damage if you are faster. |

## Data not in this table

- No per-move playtest log. The index is not win rate.
- Stamp modifiers are not folded into the index. A stamped power modifier can reorder two moves that this table calls equal.
- Priority tags (`PRIO`, `PRIOR`) are visible in the description or tags and are not given index points. `rapid_strike`, `baseline_jab`, `quick_lunge`, and `bean_sip` are the priority cases called out in the correctness audit or in this file's AP-1 bucket.
- Chemistry multipliers (1.2 advantage, 0.8 disadvantage in `COMBAT_Resolution.txt`) are not in the index.
- The gameplay manifest mirrors these columns for the moves it exports. It was not used as a second source of truth for power or AP.
