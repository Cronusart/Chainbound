# Type matchups and chemistry

Date: 2026-10-17. Docs only. No gameplay code, no combat data, and no `Docs/GameTheory/` file was edited. Nothing here is implemented.

Source of truth is the local snapshot `chainbound_combat_snapshot_20261017_1015` (Assets/Data/Combat, Assets/Scripts/Combat, Docs/Combat, Docs/Specs/Combat). The git checkout was not used. Counts below are reproduced by `type_chemistry_check.py` in the same artifacts folder. The snapshot contains no `.meta` files, so a zone’s parent area is the `EncounterArea_*.asset` in the same directory. That matches the four zones: Starter Grove extends its area, and Jungle, Link 1, and Loomville replace theirs (`entryMergeMode` 0 vs 1 in `EncounterZoneSO.cs`).

This round builds on the earlier notes. It does not reopen their settled calls: Kindling stays 6% and stacks with Rugged (APR-KIND), Meme stays out of chemistry, the ×1.2 / ×0.8 factors stay, and the enemy-AI tier sketch in `ENEMY_AI_TIERS_PROPOSAL.md` stays the shape of the AI work. What this round adds is the live matrix checked against the resolver, the empty bodies, the effect sizes, and the encounter-weighted numbers.

## Summary

### Top findings

1. The directions are in good shape. `TypeChemistryMatrix.asset` has 57 cells and matches `COMBAT_Interaction_Matrix_v1.csv` and the direction grid, including recipients. There are no mutual advantages, no self-cells, and no Meme cells. All nine loops in `COMBAT_TypeProfiles.md` are one-way. Every non-Meme type sits inside its role budget. Damage collapses to flat ×1.2, ×1.0, or ×0.8. A uniform draw over the 13 non-Meme types, self included, gives Burned 1.046, Shard 1.031, Block and Staked 0.969, Compressed 0.985, and everyone else 1.000.

2. The wild tables break that tie. Averaging the four merged zones, Burned’s hits average ×1.055 and wilds hit Burned for ×0.945. Staked’s hits average ×0.922 and wilds hit Staked for ×1.078. Those two are mirrors of each other because a win is ×1.2 and the reverse is ×0.8. Bot and Compressed never appear in a wild table. Rattlestake (Staked) has no damaging move, so it can only receive chemistry.

3. Ten of the 57 advantage cells apply a status and then change nothing. Jammed is 8 cells and its stage list is empty. Backlash is 2 cells and nothing reads the stored type. Frozen’s five wins include three Jammed blanks. The pass plan’s “super effective means a status” is false on those cells.

4. The reactions that do run are not the same size as ×1.2. Dusted cuts Speed to 0.4× and its Evasion −3 makes a 95% move a sure hit. Rugged, if the advantage hit keeps landing, climbs toward 15/16 of MaxHP per end step (about 94%). Corroded’s Melee Def drops stay after the status ends. Shattered’s Melee Def −1 is applied before the same hit is calculated, so a melee advantage hit is ×1.2 and ×1.5 defense on the hit that caused it. FOMO’s Melee Atk +1 lands the same way. Ranged hits ignore all three of those stage writes.

5. A typeless damaging move inherits the attacker’s primary type and gets that type’s multiplier and reaction (`DamageCalculator.ResolveAttackType`). STAB is off. The STAB function, if the flag were turned on as written, would also grant STAB to that inherited type. Same-type typed moves and typeless moves are equal in the chemistry system. That misses the rule that a typed move has to beat a typeless one.

### Top recommendations

1. Give Jammed a reverting Accuracy −1 for its existing 2 turns. Do not use −2. Accuracy −2 would turn a 95% move into a 47.5% move on the most common reaction.
2. Cap Rugged’s severity at 3. That is an explicit exception to the written 1–15 scale. Leave Kindling at 6%.
3. Make Corroded and Shattered own their defense drops and revert them, and write both defenses. Until the hit-chance clamp changes, make Dusted Speed −2 only.
4. Give Backlash a once-per-turn 6% MaxHP contact chip that does not re-enter chemistry. If that body is rejected, move the two cells onto statuses that already run.
5. Do not flip `enableStab` until typeless inherited attacks are excluded. Add one Common damaging move of the species’ own type to Rattlestake, Cofi Lootwak, and Blockwyrm. Do not reshuffle the 57 edges, and do not give Meme a row.

### Open questions

1. Is the Rugged cap of 15 still wanted, now that refresh plus the end-step increment can walk a target to tick 15?
2. Should Jammed be Accuracy −1, or should those eight cells move to other working statuses?
3. Should Backlash get the 6% contact chip, or should Burned→Bot and Bot→Mined change status?
4. Should STAB turn on at the authored 1.2 once typeless hits are excluded? On a same-type typed hit that is also advantage, that is ×1.44 before the reaction.
5. Are Kitsauto (Bot) and Humpacked (Compressed) missing from wild tables on purpose?
6. Should Ghost stay at all-zero stats until a Meme pass, with no Common damaging move? The pool rule says every Chainling keeps one. This note leaves Ghost alone.

## 1. Implemented map

### 1.1 What a damaging hit does

Chemistry runs only from `ResolveDamagingMove`, after the hit check and before damage (`CombatResolver.cs` around 2024–2076). A miss does not call it. The attack type is the move’s `baseType`, unless the move is typeless (`ChainlingType.None`), in which case it is the attacker’s primary type (`DamageCalculator.cs` 510–522). A coating does not change the attack type. Coating only replaces the defender’s types, and nothing in the live matrix or anywhere else sets `coatingType` except the unused coating branch in `ChemistryResolver.ApplyCoating`.

For each of the defender’s resolved types, in primary-then-secondary order (`ChainlingInstance.GetResolvedTypes`, 396–405):

- A filled attacker→defender cell adds 1 to the score and is the reaction candidate if none has been chosen yet (`ChemistryResolver.cs` 83–95).
- Otherwise a filled defender→attacker cell subtracts 1 and applies no status (97–99).

The score collapses (`ChemistryResolver.cs` 102–106, `ChemistryResult.DamageMultiplier` 626–642):

| Score | Relation | Damage |
|---|---|---|
| Positive | Advantage | `chemistryAdvantageFactor` 1.2 |
| Zero | Neutral | 1.0 |
| Negative | Resisted | `chemistryDisadvantageFactor` 0.8 |

Both factors are 1.2 and 0.8 in `CombatConstants.cs` 24–25 and in `CombatResolverConfig_Default.asset` 23–24. Two advantages do not become ×1.44. One advantage and one resist become Neutral, and the status is not applied, because the apply branch requires `relation == Advantage` (`ChemistryResolver.cs` 108). Every live species has `secondaryType: 0`, so that cancel never happens in content. It is live code with no species.

The status is applied only on Advantage, at most once per attacker per action and once per defender per action (`CombatResolver.cs` 1917–2034). Later hits in a multi-hit move, and a second foe on a spread, still get the damage multiplier. A second foe can still receive their own defender reaction. The attacker’s self reaction fires once.

Strict mode rejects a recipient that breaks `ChemistryStatusRules.IsRecipientValid`: beneficial and hybrid must be `@Self`, harmful must be `@Foe` (`TypeChemistryMatrixSO.cs` 153–160). All 57 cells pass. `EncryptedSeal` blocks a chemistry debuff (`ChemistryResolver.cs` 248–268). It does not block a self buff.

STAB is a separate multiplier, currently forced off: `enableStab: 0` in the resolver config, while `stabMultiplier` is authored at 1.2. `ResolveStabMultiplier` (`DamageCalculator.cs` 380–399) returns 1 whenever the flag is false.

### 1.2 The 57 cells

Rows are attackers. The status is what the asset applies. “Empty” means the status is applied and no later system reads it.

| Attacker | Cells |
|---|---|
| Node | Frozen Defueled `@Self`, Forked Anchored, Mined Pumped `@Self`, Compressed Dusted |
| Gas | Node Jammed (empty), Block Pumped `@Self`, Staked Isolated, Bot Drained |
| Block | Burned Firewall `@Self`, Forked Anchored, Shard Anchored |
| Encrypted | Gas Drained, Burned Defueled `@Self`, Frozen Jammed (empty), Bot Jammed (empty) |
| Burned | Gas, Frozen, Mined, Compressed, Shard all Rugged; Staked Isolated; Bot Backlash Bot (empty) |
| Frozen | Gas Jammed (empty), Staked Slowed, Bot Jammed (empty), Glitched Jammed (empty), Shard Anchored |
| Forked | Burned Defueled `@Self`, Mined Splinter `@Self`, Bot Splinter `@Self`, Glitched Leaked |
| Mined | Gas Pumped `@Self`, Block Shattered, Encrypted FOMO `@Self`, Frozen Dusted, Staked FOMO `@Self` |
| Staked | Forked Anchored, Compressed Drip `@Self`, Shard Siphoned `@Self` |
| Compressed | Block Dusted, Encrypted Pumped `@Self`, Frozen Jammed (empty) |
| Bot | Node Drained, Block Drained, Mined Backlash Bot (empty), Staked Drained, Glitched Jammed (empty) |
| Glitched | Node Corroded, Block Cascade Failure, Encrypted Corroded, Burned Corroded |
| Shard | Node Leaked, Encrypted Shattered, Forked Anchored, Mined Shattered, Compressed Dusted, Glitched Shattered |
| Meme | none |

Unmarked cells are `@Foe`. Parameter type is `None` except the two Backlash cells, which store Bot (`TypeChemistryMatrix.asset`, `parameterType: 11`).

Output counts: Jammed 8, Anchored 6, Drained 5, Rugged 5, Pumped 4, Dusted 4, Shattered 4, Defueled 3, Corroded 3, and then Isolated, Backlash, Splinter, Leaked, FOMO at 2 each, and Firewall, Slowed, Drip, Siphoned, Cascade Failure at 1 each.

Anchored’s six cells hit only two defenders, Forked and Shard. Rugged’s five cells are all Burned’s row. Corroded’s three cells are all Glitched’s row. Isolated’s two cells are both into Staked.

The loops in `COMBAT_TypeProfiles.md` 85–97 all hold, and none of them run backwards:

- Node → Frozen → Bot → Node
- Gas → Block → Burned → Gas
- Mined → Block → Forked → Mined
- Encrypted → Bot → Glitched → Encrypted
- Burned → Frozen → Gas → Block → Burned
- Mined → Frozen → Shard → Mined
- Staked → Shard → Mined → Staked
- Compressed → Encrypted → Burned → Compressed
- Node → Forked → Bot → Node

Role budgets from the same spec (44–49) all hold. Wins are forward cells. Losses are the reverse cells. Neutrals are the rest of the other 12 types.

| Type | Role | Wins | Losses | Neutrals | Wins whose body runs |
|---|---|---|---|---|---|
| Burned | Aggressor 6–7 / 4 | 7 | 4 | 1 | 6 |
| Shard | Aggressor 6–7 / 4 | 6 | 4 | 2 | 6 |
| Frozen | Net 0, 4–5 / 4–5 | 5 | 5 | 2 | 2 |
| Mined | Net 0 | 5 | 5 | 2 | 5 |
| Bot | Net 0 | 5 | 5 | 2 | 3 |
| Node | Net 0 | 4 | 4 | 4 | 4 |
| Gas | Net 0 | 4 | 4 | 4 | 3 |
| Encrypted | Net 0 | 4 | 4 | 4 | 2 |
| Forked | Net 0 | 4 | 4 | 4 | 4 |
| Glitched | Net 0 | 4 | 4 | 4 | 4 |
| Block | Wall 3 / 5 | 3 | 5 | 4 | 3 |
| Staked | Wall 3 / 5 | 3 | 5 | 4 | 3 |
| Compressed | Soft wall 3 / 3–4 | 3 | 4 | 5 | 2 |

### 1.3 What each status does when the cell lands

Durations and percents are the status assets plus `CombatResolverConfig_Default.asset`. Stage math is `(2+stage)/2` up and `2/(2+|stage|)` down (`ChainlingInstance.StageMultiplier`).

| Status | Cells | Duration | What runs |
|---|---|---|---|
| Jammed | 8 | 2 | Nothing. `StatusDef_Jammed.asset` has `stageEffects: []`. No resolver branch writes Accuracy. |
| Backlash Bot | 2 | 3 | The status is stored with `parameterType` Bot. No combat code reads `StatusId.BacklashType` to deal damage. |
| Anchored | 6 | 3, Consumed | The defender’s next damaging move is forced to one target, then the status is consumed (`CombatResolver.cs` 1839–1850). Not in `IsNextActionStatus`, so it does tick on the granting end step. Duration 3 still covers the next action. |
| Drained | 5 | 3 | −1 AP at end of turn (`endOfTurnAPChange: -1`). |
| Rugged | 5 | 3 | End step deals `counter/16` of MaxHP, minimum 1, then counter += 1, cap 15 (`StatusProcessor.cs` 113–167, `ruggedTickCap: 15`). Refresh resets duration and does not bump the counter (`ChemistryResolver.cs` 293–307). The end step still bumps it. |
| Pumped | 4 | 3 | +1 AP at end of turn. |
| Dusted | 4 | 2 | Speed −3 and Evasion −3, once, both owned, both revert. Speed is CoreStat index 5. Evasion is `CombatStageStat` index 6 (`StatusDef_Dusted.asset` 22–31, `StatusProcessor.ApplyApplyOnceStages` 596–601). |
| Shattered | 4 | Instant | 8% of current HP, and the loss cannot drop the target below 1 (`ChemistryResolver.cs` 475–477). Melee Def −1 in the same call, before this hit’s damage is calculated. The drop is not owned. There is no status left to expire. Ranged Def is untouched. |
| Defueled | 3 | Instant, `@Self` | Removes the newest harmful chemistry status on the attacker, and undoes a Slowed stage if that was the one removed (`ChemistryResolver.cs` 487–516). A clean attacker gets nothing. |
| Corroded | 3 | 2 | Melee Def −1 at each end step (`applyOnce: 0`, stat 2 = Melee Def). Two end steps leave Melee Def −2. `RevertChemistryStageEffects` does not own that write, so it stays. Ranged Def is untouched. |
| Isolated | 2 | 2 | Blocks ally support, heals, AP transfer, and ally stage buffs (`CombatResolver.cs` 3144, 3176, 6607). |
| Splinter | 2 | 1, Consumed, `@Self` | Next damaging move spreads at ×0.65 if it hits more than one foe, or ×1.15 if it hits one (`splinterDamageMultiplier`, `splinterSingleTargetDamageMultiplier`). Held through the granting end step (`IsNextActionStatus`). |
| Leaked | 2 | 3 | If any active ally is Leaked, the Chainling who is about to act loses 10% of their own MaxHP, and it can KO (`CombatResolver.ApplyLeakedActionTax`, 5197–5233, called at 1379). The end-step damage loop skips Leaked. |
| FOMO | 2 | 3, `@Self` | Melee Atk +1, owned, reverts when FOMO ends. Self damage is 6.25% MaxHP per end step (`fomoSelfDotPercent: 0.0625`). The Atk write happens before this hit’s damage, so a melee hit gets the +1 on the applying hit. A ranged hit does not. |
| Firewall | 1 | 2, `@Self` | First projectile each turn takes 30% less (`firewallProjectileMitigation: 0.30`). Armed on apply and re-armed at turn start (`ChemistryResolver.cs` 395–405, `StatusProcessor.cs` 364–380). |
| Slowed | 1 | 2 | Speed −2, owned, reverts. `slowedLoseActionChance` 0.25 is on the config and is not read by the resolver. |
| Drip | 1 | 3, `@Self` | Heal 6% MaxHP at each end step, rounded half away from zero (`dripHealPercentMaxHp: 0.06`). |
| Siphoned | 1 | 3, `@Self` | Heal 25% of damage dealt, truncating (`CombatResolver.cs` 2347–2352). |
| Cascade Failure | 1 | Counter 3 | The third application deals 25% MaxHP and can KO (`cascadeFailureMaxHpDamage: 0.25`). Swap removes the counter (`CombatResolver.ResetChemistrySwapState`, 6546–6579). Swap does not clear stages. |

### 1.4 Ordering

On a hit: modifier context, then chemistry (status, instant chip, same-hit stage), then damage (which already sees Shattered’s Melee Def and FOMO’s Melee Atk), then HP loss, then Kindling if the foe lived, then on-hit passives. Siphoned heals from the damage just dealt.

At end of turn, `StatusProcessor.ProcessEndOfTurn` (87–95) deals Kindled first, then the other end-step damage including Rugged and FOMO, then Drip, then status AP (Pumped, Drained, Yield), then repeating stages (Corroded), then the duration tick and the revert. There is no separate chemistry phase. Narration for Kindled (“Kindled burns”) and Rugged (“Rugged scorches”) is a real combat event. A generic DoT that is only a log message is not.

This round does not reopen the end-step order proposal in `PASSIVES_AND_STATUSES_ANALYSIS.md`. Kindled is in front of Rugged, which matches the “burn before chemistry tick” sentence for Kindling only. Other end-of-turn passives still run after the status pass. A Rugged cap has to keep that narration order.

### 1.5 Defined and not reachable

- Meme row and column. By design. `Ghost.asset` is 0 on every stat. v2’s CSV still paints a Meme column; the asset does not.
- Type coating and hyped coating. The apply function exists. No cell and no other writer sets `coatingType`.
- Reinforced. Named in `COMBAT_Chemistry.txt` §4 and in `ChemistryStatusRules`. There is no `StatusDef_Reinforced.asset` and no cell. Same for the §7 migration ids (Firewalled, Halted, Crashed, LiquidityPump, Evasive, Bridged, Narrowcast, Overclocked, Amplified): readable in the family map, polarity Neutral so strict mode would reject them, not in the asset.
- Dual-type score cancel and “primary type picks the reaction.” No species has a secondary type.
- `slowedLoseActionChance`. Config only.
- Backlash’s typed contact damage, and Jammed’s Accuracy drop. The cells are reachable. The effects are not.

## 2. Where docs, data, and code disagree

The asset, `COMBAT_Interaction_Matrix_v1.csv`, and `COMBAT_Interaction_Matrix_v1_directions.csv` agree: 57 cells, same pairs, same recipients, no stack counts. Move assets and `Moves.csv` agree on damaging-move counts by type (72 damaging rows: 52 typed, 20 typeless).

These do not agree:

| Claim | Where it still says that | What the snapshot does |
|---|---|---|
| Damage is `1.2^ChemAdv × 0.8^ChemDis`, and cells can carry stacks | `COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md` 29–36 and §4 | Flat 1.2 / 1.0 / 0.8. One status, no count. `COMBAT_Chemistry.txt` §2 matches the code. |
| Jammed cannot store Accuracy, so Acc −2 is missing | `COMBAT_ISSUES_MASTER.md` E-07 | The reason is stale. Dusted already stores Evasion through `usesCombatStage`. Jammed’s list is simply empty, so Acc −2 still does not happen. |
| Dusted’s Evasion −3 is not applied | `COMBAT_ISSUES_MASTER.md` E-10 and the correctness audit | Stale. The asset writes Evasion −3 and the Dusted branch owns it. Speed −3 reverts too. |
| Leaked is a 10% end-of-turn self DoT | `COMBAT_ISSUES_MASTER.md` E-08 | Stale. The asset says “10% own MaxHP while the side is leaked,” `hasEndOfTurnDamage` is 0, and `ApplyLeakedActionTax` taxes the actor when any active ally is Leaked. `StatusId.cs` still says “1/10 HP to all,” which is the same moment and a different target set. |
| Backlash adds a typed contact DoT | `StatusId.cs` 31, `StatusDef_BacklashType.asset` | The cell applies the status. No damage runs. E-09 is still accurate. |
| v0 and v2 are charts you might import | `COMBAT_Interaction_Matrix_v0.csv` (91 filled cells), `COMBAT_Interaction_Matrix_v2.csv` (83, 26 of them touch Meme) | Runtime is v1. v2 gives every non-Meme attacker a Meme cell. Do not import either. |
| Reinforced is a Structure reaction you can author | `COMBAT_Chemistry.txt` §4 | No asset, no cell. |
| A dual-type defender compounds per type, then may still print a status from a forward cell | Pass plan §7 “Dual-type” check | The score can cancel to Neutral, and Neutral prints nothing. |

`PASSIVES_AND_STATUSES_ANALYSIS.md` already corrected Dusted and Leaked. The issues master has not caught up. This note treats the assets and the resolver as the authority.

## 3. Balance

### 3.1 The damage relation is mild. The roster and the wilds are not.

Against a uniform non-Meme type, self included as ×1.0:

| Type | Mean | Advantage share | Resist share |
|---|---:|---:|---:|
| Burned | 1.046 | 53.8% | 30.8% |
| Shard | 1.031 | 46.2% | 30.8% |
| Block | 0.969 | 23.1% | 38.5% |
| Staked | 0.969 | 23.1% | 38.5% |
| Compressed | 0.985 | 23.1% | 30.8% |
| Every net-0 type | 1.000 | 30.8% or 38.5% | same as its win share |

Net-0 types are zero-sum by construction: each win is paid for by a loss of the same size. Burned’s extra three wins over a 4-win type are worth `3/13 × 0.2` = about +0.046. That is one extra hit of +4.6% across a random typed opponent, before the reaction. The chart is not where a type becomes dominant. The reaction and the roster are.

There are 19 species, all mono-type. Staked has three (Hopoch, Rattlestake, Cofi). Gas, Mined, and Shard have two. Everyone else has one. Ghost is the Meme. A roster-equal draw, Ghost excluded from the weights because it is not a real body, gives:

| Attacker | Mean vs the 18 real species | Why it moved |
|---|---:|---|
| Burned | 1.089 | Beats both Gas, both Mined, both Shard, all three Staked, Frozen, Compressed, Bot. That is 12 of 18. |
| Frozen | 1.033 | Beats the three Staked plus Gas, Bot, Glitched, Shard. |
| Mined, Bot | 1.022 | Both beat Staked. |
| Shard | 1.011 | Six chart wins, but three Staked resist it. |
| Compressed | 0.956 | Its three wins are one species each. It loses to three Staked and two Shard. |
| Block, Staked | 0.967 | Wall budgets, and Staked mirrors itself. |

Burned is the roster’s best attacker by a clear step, and it is also the type wilds hit the least hard. That is the dominant type. It is not because ×1.2 is large. It is because Burned’s seven wins land on the types the roster and the zones actually field, and five of those seven wins also print Rugged.

No type is dead on the grid except Meme, which is empty on purpose. Compressed is the thinnest real row: three wins, five neutrals, and one of the three wins is a blank Jammed.

### 3.2 Encounter-weighted multipliers

Merged tables, weights as authored:

| Zone | Mix |
|---|---|
| Starter Grove (area extended by the grass zone) | Gilarime Frozen 40, Validape Node 35, Kreechank Glitched 15, Cinislug Burned 10. Total 100. |
| Jungle (replace) | Rattlestake Staked 450, Deghoul Gas 450, Forkrook Forked 220, Hopoch Staked 100, Blockwyrm Block 6. Total 1226. |
| Link 1 (replace) | Cinislug Burned 450, Kodiax Mined 220, Marrowhead Shard 220, Minefolk Mined 100. Total 990. |
| Loomville (replace) | Kreechank Glitched 450, Balloom Gas 450, Keyper Encrypted 450, Crackett Shard 14. Total 1364. |

Not in any merged zone: Cofi Lootwak, Ghost, Humpacked, Kitsauto. Types never faced in the wild: Compressed, Bot, Meme.

Offensive mean if you attack as that type into the zone (a typeless move, or a move of your own type). Each zone counts once in the average, so Jungle’s 1226 does not drown Starter Grove’s 100.

| Attacker | Zone average | Starter | Jungle | Link 1 | Loomville |
|---|---:|---:|---:|---:|---:|
| Burned | 1.055 | 1.050 | 1.126 | 1.109 | 0.936 |
| Encrypted | 1.031 | 1.070 | 1.073 | 0.982 | 0.998 |
| Mined | 1.028 | 0.990 | 1.128 | 0.865 | 1.130 |
| Glitched | 1.021 | 1.010 | 0.965 | 1.046 | 1.064 |
| Forked | 1.016 | 0.980 | 0.909 | 1.111 | 1.064 |
| Frozen | 1.015 | 0.940 | 1.163 | 0.889 | 1.068 |
| Shard | 1.013 | 1.000 | 0.945 | 0.974 | 1.132 |
| Node | 0.975 | 1.050 | 0.962 | 1.020 | 0.866 |
| Block | 0.973 | 0.990 | 0.962 | 1.071 | 0.870 |
| Bot | 0.972 | 1.000 | 0.981 | 0.974 | 0.934 |
| Gas | 0.960 | 0.970 | 1.091 | 0.844 | 0.934 |
| Compressed | 0.958 | 0.990 | 0.911 | 0.865 | 1.064 |
| Staked | 0.922 | 0.900 | 0.962 | 0.889 | 0.936 |

Because every edge is one-way, the multiplier wilds get when they hit you is `2 −` your offensive mean against that same mix. Wilds hit Staked for 1.078 on average and hit Burned for 0.945. Link 1 is a Burned/Mined/Shard zone: Gas attacking into it is ×0.844, because Burned and Mined both resist Gas. Jungle is a Staked/Gas zone: Frozen attacking into it is ×1.163, and Burned is ×1.126. Loomville resists Node (×0.866) because Gas, Glitched, and Encrypted all beat Node, and those three are 99% of the zone.

These means assume the wild attacks as its species type. That is true for a typeless damaging move and for a same-type move. It is wrong when the pool’s only real hit is off-type. Section 3.4 is that list. Balloom’s 98-power hit is Meme, so it does not get Gas’s ×1.091 into Jungle.

### 3.3 Species lines next to the chart

Walls are slow and bulky, and the chart asks them to be hit. Blockwyrm is 168 HP, Melee Def 86, Speed 24. Cofi is 168 HP, both defenses 84, Speed 16. Gilarime (Frozen) is 161 HP. Validape (Node) is 156 HP. The glass side is Crackett (Shard, 112 HP, Melee Atk 86, Speed 92), Keyper (102 HP, Ranged Atk 70), Forkrook (104 HP, Speed 88), Deghoul (106 HP, Speed 72). Cinislug, the only Burned, is a ranged wall-leaning starter: 134 HP, Ranged Atk 63, Melee Atk 31, Speed 28. Burned’s wide row is carried by a slow ranged body, not by a sweeper.

Minefolk is the other way around from Kodiax. Minefolk’s Ranged Atk is 74 and Melee Atk is 48. Kodiax’s Melee Atk is 70. Chemistry stages that only touch Melee Atk or Melee Def pick a winner between those two species.

### 3.4 Moves, and who can actually press the row

Damaging moves in `Moves.csv` / the move assets: typeless 20, Burned 5, Mined 5, and then Block, Forked, Staked, Compressed, Glitched, Shard, Meme at 4, Gas, Encrypted, Frozen, Bot at 3, Node at 2. Burned, Gas, Encrypted, and Frozen have no melee damaging moves. Block has no ranged damaging moves. A Block advantage that cares about projectiles (Firewall) is aimed at the right kits. A stage that only drops Melee Def does nothing to Burned’s, Gas’s, Encrypted’s, or Frozen’s hits.

Learnable pools (`SpeciesMovePools.csv`), counting a typeless damaging move as the species’ primary type:

| Species | Type | Damaging moves in the pool | Can press its own row? |
|---|---|---|---|
| Cinislug | Burned | `scorch_shot` only (power 64, 2 AP, projectile) | Yes, one button |
| Gilarime | Frozen | `frost_shot`, `cryo_ray`, typeless `baseline_shot` | Yes. Three of Frozen’s five wins are blank Jammed |
| Forkrook | Forked | `fork_split` (AllEnemies), `rapid_strike`, typeless shot | Yes, and the spread is real |
| Kitsauto | Bot | `multi_thread_shot` power 12, `blade_flurry` power 18, typeless jab power 16 | The row is pressable. The hits are tiny. Not in any zone |
| Validape | Node | `hammer_sync`, typeless striker | Yes |
| Keyper | Encrypted | `cipher_shot` | Yes. Two of four wins are blank Jammed |
| Kodiax | Mined | three Mined hits plus `solid_strike` (Block) | Own row, plus Block’s row on one move |
| Minefolk | Mined | `deep_reap` (ranged), `pickaxe_flurry` (melee), two typeless shots | Own row. FOMO’s Melee Atk does not help `deep_reap` |
| Hopoch | Staked | `stake_strike`, `yield_spike`, plus off-type `deep_reap` (Mined) | Own row, and one Mined row |
| Crackett | Shard | `seam_cut` (melee) and typeless `baseline_jab` | Own row. No spread, so Anchored into Crackett often changes nothing |
| Marrowhead | Shard | `partition_shot`, plus off-type `precision_strike` (Compressed) | Own row and Compressed’s row |
| Balloom | Gas | `volatile_shot` (Gas, 72), `recoil_strike` (Forked, 76), `all_in` (Meme, 98, AP 0) | Only the 72-power shot uses the Gas row |
| Deghoul | Gas | `ap_siphon` (56, 3 AP, ranged) | Yes, one move |
| Kreechank | Glitched | `chaos_strike`, `bug_payoff` | Yes |
| Humpacked | Compressed | `lean_hit` (46, 1 AP) | Yes. Not in any zone |
| Blockwyrm | Block | `bulwark_blow` only (Epic, power 50, 3 AP, hits with Melee Def) | No Common damaging move |
| Cofi | Staked | `civet_beam` only (Uncommon, power 12, AllEnemies) | No Common damaging move |
| Rattlestake | Staked | none | Cannot apply Anchored, Drip, or Siphoned |
| Ghost | Meme | none | Out of this pass |

The design rule says a learnable pool keeps at least one Common damaging move. Rattlestake, Cofi, and Blockwyrm miss it. Ghost misses it too and stays out of scope. Cinislug meets it with a single move, so the entire Burned chemistry row in the wild is `scorch_shot`.

`fork_split` is the spread Anchored is for. Anchored into Forked (from Node, Block, or Staked) turns that 3 AP spread into one target. Anchored into Shard does that only if the Shard is holding a spread. Crackett is not.

### 3.5 How big the reactions are

Floor is `Max(1, (int)(MaxHP × percent))` unless noted. Cinislug’s base MaxHP is 134. The 132 figure is the older common-median body, kept so these numbers sit next to the earlier notes.

Rugged, one application, no refresh, three end steps: ticks 1, 2, 3.

| HP | Tick 1 | Tick 2 | Tick 3 | Sum | Share of MaxHP |
|---:|---:|---:|---:|---:|---:|
| 132 | 8 | 16 | 24 | 48 | 36.4% |
| 134 | 8 | 16 | 25 | 49 | 36.6% |

That is on top of the ×1.2 hit. Kindling, already approved, adds another `floor(MaxHP × 0.06)` on each end step where a non-KO hit landed: 7 on 132 HP, 8 on 134. The first advantage end step on Cinislug is 8 + 8 = 16 HP from the two burns, about 12% of MaxHP, and then Rugged keeps climbing if they stay in.

If the Burned hit lands again before Rugged expires, duration goes back to 3 and the counter does not jump, but the next end step still increments it. Seven such end steps on 134 HP are 8, 16, 25, 33, 41, 50, 58. The sum is 231, 172% of MaxHP, and the cap of 15 has not been reached yet. Tick 15 on 134 HP is `floor(134 × 15/16)` = 125, which is 93% of that body in one end step. The written scale of 1–15 (`COMBAT_Chemistry.txt` §5, `RUGGED_TICK_CAP`) describes a fight-ending DoT once the attacker can repeat the matchup. Repeating it is the whole point of a seven-win type with one reliable move.

Dusted, for two turns: Speed ×0.4, and Evasion ×0.4. Hit chance is `(accuracyStage / evasionStage) × accuracy/100`, clamped to 1 (`DamageCalculator.cs` 324). A 95% move into Evasion −3 is `0.95 / 0.4` = 2.375, which clamps to a sure hit. Evasion −1 is already enough to do that: `0.95 / (2/3)` = 1.425, still clamped. So the evasion half of Dusted does not “make them easier to hit.” It deletes the miss chance on the common accuracy band. The speed half is the part that changes turn order.

Jammed, if someone turned on the commented Acc −2: stage ×0.5, so a 95% move hits 47.5% of the time for two turns, on eight cells. Acc −1 would be ×2/3, so 95% becomes 63.3%. That is still a large rider, and it is a rider the cell does not have today.

Shattered on a melee hit: the Melee Def −1 is in place before damage, so that hit takes ×1.5 from the defense stage and ×1.2 from chemistry, then an 8% current-HP chip that cannot KO by itself. The defense drop never reverts, including on the bench, because swap does not reset stages. A ranged shattered hit gets the chip and the ×1.2 only. Later melee hits keep the ×1.5 for the rest of the fight.

Corroded: two end steps, Melee Def −2, which is ×2 melee damage taken, and it stays after the status expires. Ranged damage ignores it. Glitched→Burned is one of the three cells, and Cinislug has no melee attack, so that cell’s stage does nothing to the only Burned in the game. The ×1.2 still applies.

FOMO: Melee Atk ×1.5 on the applying hit and for three turns, plus three self ticks of 6.25% MaxHP (8 HP on 132 or 134, 24 HP total). A ranged Mined hit gets the self damage and not the attack boost.

Other sizes, on 134 HP: Drip is about 8 HP a turn for three turns (24 HP). Cascade is 33 HP on the third application and can KO. Leaked is 13 HP per action of every active teammate, and it can KO. Firewall is 30% off the first projectile each turn for two turns. Siphoned is 25% of damage dealt for three turns. Pumped and Drained are ±1 AP at up to three end steps. Defueled is one cleanse or nothing. Splinter is a next-move choice, ×0.65 across both foes or ×1.15 into one. Slowed is Speed ×0.5 for two turns (stage −2).

## 4. Chemistry as gameplay

### What is a real decision

A good chemistry cell tells you to do something before or after the hit, and the payoff is not the same as “you already won the damage roll.”

These are that kind of cell:

- Splinter (Forked→Mined, Forked→Bot). You hit the matchup, then you choose a spread or a harder single hit. Forkrook actually has `fork_split`. This is the clearest setup in the matrix.
- Anchored into Forked. You pin the spread type before `fork_split`. That is a team-comp counter, and three different attackers can do it.
- Cascade Failure (Glitched→Block). Three advantage hits, or the Block swaps and the counter is gone. Stay or swap is a readable choice. It is one cell, which is the right amount.
- Isolated into Staked (Gas and Burned). The sustain type cannot be healed or buffed by its partner for two turns. That matters for Cofi and Hopoch. It does nothing if the Staked is alone.
- Firewall (Block→Burned). The wall’s reward for hitting the burn type is “the next projectile hurts less,” and Burned’s hits are projectiles. One cell, and it matches the names.
- Drip and Siphoned (Staked→Compressed, Staked→Shard). The wall’s forward wins pay it back in HP. You only see them in those pairs, which is fine. Rattlestake cannot press them.
- Leaked (Forked→Glitched, Shard→Node). The side pays 10% MaxHP on each action. Swapping the leaked body out pauses it. Strong, and a bit opaque because the partner pays too, but it is a real tax.
- Pumped, when you are banking toward a 3 AP move. See below.

These are damage spikes wearing a status name:

- Rugged, once it is allowed to climb past tick 3, plus Kindling on the same end step.
- Shattered and FOMO on melee hits, because the stage is inside the same hit.
- Dusted’s evasion clamp.
- Corroded’s permanent Melee Def −2.

These are often blank:

- Jammed, eight times. The player sees a status and the hit chance does not move.
- Backlash, twice. Burned’s win into Bot, and Bot’s win into Mined, are ×1.2 and a tooltip.
- Defueled, whenever the attacker is not already holding a harmful chemistry status. Three cells spend their identity on a cleanse you might not need.
- Anchored into a Shard who only has single-target moves.
- Slowed’s unused 25% action-loss. The speed drop itself is real and is only one cell, so Frozen’s “speed controller” profile is mostly blank Jammed plus one Slowed into Staked.

### AP boost, AP denial, and these reactions

Turn regen is +2 at the start of the turn, cap 10, start at 0. Pumped and Drained tick at end of turn, after the action has been paid. They do not change the action window of the turn they are applied.

A fighter who spends down to 0 is immune to Drained. The drain floors at 0. The useful case is a bank:

- Pass turn 1 (hold 2). Without Drained, turn 2 opens at 4 and a 3 AP move leaves 1, so turn 3 opens at 3 and the second 3 AP move is legal. With Drained on that first end step, the bank goes from 2 to 1, turn 2 opens at 3, the 3 AP move leaves 0, and turn 3 opens at 2. Drained ate the leftover that chained the second nuke. It did not stop the first one.
- Pumped is the mirror. Spend 2 on turn 1 and get Pumped at that end step: the bar is 0, Pumped makes it 1, turn 2 opens at 3. The 3 AP move is legal on turn 2, the same turn a pass would have unlocked it, and turn 1 was still an attack. Without Pumped, spending turn 1 leaves turn 2 at 2 AP, and the 3 AP move waits until turn 3. If you keep the status up, you keep a +1 leftover every end step.

So chemistry boost and chemistry denial do meet, and they meet on the leftover, not on the turn-1 2 AP hit. They do not meet the design rule that denial should stop a potent hit early. Thread Lock is the tool that blocks regen. It is a move, not a chemistry cell. Bot’s three Drained cells are the right identity for Bot, and they are the wrong tool if the goal is “they do not fire the big move on turn 2.” Encrypted→Gas and Gas→Bot being the other two Drained cells is fine: those are the AP types taxing each other. Adding more Drained cells would not fix early nukes.

Pumped on Gas→Block and Mined→Gas is a real tempo reward for the fee and mining edges. It is also free AP on a hit you already win at ×1.2. That is acceptable as long as it stays a leftover bonus and not a second damage multiplier.

### Kindling and the other passives

Kindling is a separate status from Rugged. An advantage Burned hit can apply both. A neutral or resisted Burned hit still applies Kindling and does not apply Rugged. That split is the right one: the passive works from turn 1 on `scorch_shot` (2 AP), including into neutrals, and the matchup adds the escalating DoT. This note does not change the 6%.

The stack is the problem only because Rugged does not stop at the three ticks its duration describes. On Cinislug’s 134 HP, three unrefreshed Rugged ticks plus three Kindled ticks are 49 + 24 = 73 HP, about 54% of MaxHP, before the ×1.2 hits themselves. That is a long fight’s worth of burn for winning one matchup three times. Capping Rugged at tick 3 keeps the approved stack and removes the execute.

Firewall the chemistry status and `firewall_passive` (Encrypted, 25% projectile mitigation) multiply if both are up. A projectile could see both. That is one Block→Burned cell overlapping one Encrypted passive, so it is rare, and it is worth remembering if both stay at 30% and 25%.

Defueled does not cleanse Kindled. Kindled is General, and Defueled only removes ChemistryOnly harmful statuses. A Burned who is also holding Rugged can Defueled the Rugged by hitting Frozen, Burned’s own matchup from the other side (Node→Frozen, Encrypted→Burned, Forked→Burned). That is a real, narrow answer to Rugged, and it asks you to bring Node, Encrypted, or Forked. It does not answer Kindling.

Encrypted Seal stops Rugged, Jammed, Dusted, and the other debuff reactions. It does not stop the ×1.2. It does not stop Pumped, Splinter, FOMO, Firewall, Drip, Siphoned, or Defueled, because those land on the attacker.

## 5. Proposals

Directions stay. The 57 edges match the role table and the loops. DEC-CHEM can close on the multipliers and the edges. The work is the bodies and three stage bugs. Meme stays empty.

### P1. Jammed is Accuracy −1, owned, for 2 turns

Author one apply-once combat stage: Accuracy, −1, `usesCombatStage` set. Add Jammed to the owned-stage revert set beside Dusted, so the stage ends when Jammed ends. Do not use −2.

Accuracy −1 takes a 95% move to 63.3% for two turns. On eight cells that is already a lot, and it is the same debuff on Gas, Encrypted, Frozen, Compressed, and Bot. That sameness is a later chart edit, not this one. Shipping the body unblocks DEC-CHEM’s “wait until Jammed is decided.”

This is not a new passive and it does not reuse a move name.

### P2. Cap Rugged severity at 3

Set `ruggedTickCap` to 3. Refresh still only resets duration. The end step still increments, and it stops at 3. One application remains ticks of 1/16, 2/16, 3/16. A Burned who keeps hitting holds the status at 3/16 per end step instead of walking it to 15/16.

Exception to `COMBAT_Chemistry.txt` §5 and `CombatConstants.RUGGED_TICK_CAP` 15. The 1–15 scale belongs to the stacked chart the pass plan already abandoned. The live duration is 3. Kindling stays 6% and still stacks, as APR-KIND asked. Narration stays “Rugged scorches” after “Kindled burns.”

Do not move Rugged’s five cells in this pass. After the cap, five copies of a 3-tick DoT are Burned’s identity, which the profile asked for, and they are no longer a delete button.

### P3. Defense drops revert, and they hit both defenses. Dusted loses the evasion stage for now

Corroded: each end step, Melee Def −1 and Ranged Def −1, owned on the status, reverted when Corroded ends. Two turns means the drop is there for two end steps and then gone. That matches the timed-status rule, and it stops Glitched→Burned from being a melee-only debuff on a ranged species.

Shattered: keep the 8% current-HP chip that cannot KO. Change the defense drop to a 2-turn status that owns Melee Def −1 and Ranged Def −1, reverted on expiry. Apply the chip during chemistry, and apply the defense drop after this hit’s damage, so the spike is the chip plus ×1.2, and the defense drop is the next exchange. Today the melee drop is inside the same hit and then lasts the whole fight.

FOMO: raise Melee Atk and Ranged Atk by 1, still owned, still reverted, self DoT unchanged. A ranged Mined then gets the boost the cell promises.

Dusted: Speed −2 only, still reverted. Drop the Evasion −3 until hit chance is not `accuracy / evasionStage` clamped at 1. Any negative evasion stage makes the 95% band a sure hit, so the evasion number cannot be tuned. Speed −2 is ×0.5 speed for two turns, which is the same size as the one Slowed cell and is enough.

### P4. Backlash gets a small contact chip, or the two cells move

Preferred: while Backlash is up, the first contact hit each turn deals an extra `floor(MaxHP × 0.06)` to the bearer, minimum 1, and does not call `ChemistryResolver` again. The stored type is narration only (“Backlash [Bot]”). Duration stays 3. That matches the name, stays in the Kindling band, and does not invent a second type chart inside the hit.

If that body is rejected: change Burned→Bot from Backlash to Rugged (Burned’s DoT, and P2 has capped it), and change Bot→Mined from Backlash to Drained (Bot already drains Node, Block, and Staked). Do not leave the tooltip lying.

### P5. Typed moves beat typeless ones in the damage formula, not by a second copy of the reaction

Do not turn `enableStab` on as the function is written. `ResolveAttackType` makes a typeless move the primary type, and `ResolveStabMultiplier` then treats that as STAB.

Proposed rule: STAB 1.2 applies only when `move.baseType` is not `None` and equals the attacker’s primary or secondary type. Typeless hits still use the primary type for the chemistry relation, so the chart still matters, and they do not get STAB. A same-type typed hit is then:

| Relation | Typed, with STAB | Typeless, no STAB |
|---|---:|---:|
| Advantage | 1.44 | 1.20 |
| Neutral | 1.20 | 1.00 |
| Resisted | 0.96 | 0.80 |

Typed is strictly higher on every relation, at equal power. The 1.44 is the number to playtest before the flag flips. If it is too much, the lever is `stabMultiplier` (try 1.1: advantage 1.32, neutral 1.10, resist 0.88), not a conditional that sometimes equals the typeless hit.

Chemistry reactions stay on inherited typeless hits. Taking the reaction away would make typeless and typed equal on neutral and resist, and the rule says they should not be equal. STAB is the always-on gap. The reaction is the chart, and the chart should show up on both.

### P6. Pools that cannot press a row get one Common hit. The edges do not move

Add one existing Common damaging move of the species’ type to:

- Rattlestake: `stake_strike` (Staked, power 64, accuracy 95, 2 AP). The pool currently has no damaging move, so Staked’s Drip, Siphoned, and Anchored never come from the most common Staked in Jungle.
- Cofi Lootwak: the same `stake_strike`, beside `civet_beam`. The beam stays the boss spread. The pool rule wants a Common hit.
- Blockwyrm: `solid_strike` (Block, the typed Common band). `bulwark_blow` stays the Epic.

Do not invent a new passive to do this. Do not add a Meme cell. Do not move the 57 edges. Kitsauto and Humpacked are a content-placement question, not a chart question: their rows are fine, and no wild fight contains them.

## 6. What the AI tiers should read

`ENEMY_AI_TIERS_PROPOSAL.md` already says the damage estimate ignores chemistry, and its C1 term is the right place for the multiplier. This section only adds the matchup and chemistry reading. It does not redo target slots, personalities, or the tier names. Chemistry scoring does nothing useful until the chosen target is the target that gets hit. That fix is already proposed there.

Resolve the attack type the way the engine does: move type, or the attacker’s primary type when the move is typeless. Look up the defender’s resolved types. Use the same +1 / −1 collapse, so a future dual type does not get a different answer from the AI than from `ChemistryResolver`. Meme is always Neutral. Do not score a Meme move as the user’s species type. `all_in` is Meme, not Gas.

| Tier | Matchup | Chemistry rider |
|---|---|---|
| Rookie | Ignores it, as today | Ignores it |
| Tactician | Multiplies expected damage by 1.2, 1.0, or 0.8 | Ignores the status. Still prefers the ×1.2 target over the ×0.8 target when damage is otherwise close |
| Mastermind | Same multiplier, and it will swap using C7 when the bench’s mean against the foes beats the active’s | Adds the rider table below. A blank body scores 0 |

Rider adds, for Mastermind only. They sit on top of the damage term. They are suggestions.

| Status | Add | Skip when |
|---|---|---|
| Jammed, Backlash | 0 until P1 and P4 exist. After that, Jammed is “their hit chance ×2/3 for 2 turns,” and Backlash is one 6% contact chip per turn | The body is still empty |
| Rugged | Sum of the remaining ticks, using the cap from P2 (max 3) | The target would faint from the hit itself |
| Kindling | One 6% tick if the hit leaves them alive and they do not already have Kindled | Same |
| Drained | Worth one banked AP if the foe would end the turn above 0 AP | They will spend to 0 anyway. Do not treat Drained as “they cannot press a 2 AP move” |
| Pumped | Worth one AP if this attacker is short of a 3 AP move next turn | The bar is already at the cap, or they have no 3 AP move |
| Dusted | They act later (Speed ×0.4 today, ×0.5 if P3 lands) and, today only, the miss chance is gone | After P3, do not score a sure-hit |
| Shattered | 8% current HP now, plus melee defense ×1.5 afterward | Against a foe whose damaging moves are all ranged, score the chip only |
| Corroded | Melee damage taken ×1.5 after one end step, ×2 after two, and today it does not fall off | All-ranged foe: score 0 |
| FOMO | Melee damage dealt ×1.5, minus the self DoT | Ranged-only attacker: score the self DoT as a cost, not a buff |
| Anchored | High if the target has an AllEnemies or Field damaging move (`fork_split`, `ember_shower`, `civet_beam`, `shard_scatter`) | Target’s kit is single-target |
| Splinter | The next damaging move is a spread if two foes are alive, otherwise ×1.15 | Anchored is already on the user |
| Cascade | High when the counter is already 2 and the target has no legal swap | Counter is 0 and they have a bench. Swap clears it |
| Isolated | High when the target has a living partner with a support or heal | Target is alone |
| Leaked | 10% MaxHP per future action of each active foe | They are about to KO the leaked body |
| Firewall | 30% off the foe’s first projectile each turn | Foe has no projectile |
| Defueled | One harmful chemistry status removed | User has none |
| Drip, Siphoned, Slowed | Score the heal or the Speed −2 directly | — |

Mastermind swaps out of Cascade when the third hit is coming and a bench mate resists Glitched. It swaps a burned target out before a Rugged tick that would KO, knowing the status pauses on the bench and the counter does not reset (only Cascade resets). It does not swap to “cure” Shattered or Corroded today, because those stages are not on the status and the bench keeps them. If P3 lands, the swap becomes a way to pause the defense drop, and the AI should learn that.

Tactician does not need the rider table. The multiplier alone fixes “hit the resisted slot 0 forever,” which is the bug that matters in the current zones: a Loomville Node lead into Gas, Glitched, and Encrypted is ×0.866 and today scores the same as a Burned lead.

## 7. Tests

Sketches only. They are not added to the repo. Style follows `Assets/Scripts/Combat/Tests/CombatChemistryMatrixPassTests.cs` and `CombatChemistryReactionRedesignTests.cs`: NUnit, a built matrix or the real asset, assertions on relation, status list, stages, and HP. The Python script is the check for the census and the encounter means.

### Script

`type_chemistry_check.py <snapshot root>` prints asset-vs-CSV equality, triangle directions, role counts, empty-body counts, species types, pool reach, merged encounter weights, offensive and defensive means, and the HP sketches. Re-run it after any matrix or encounter edit. It exits non-zero only on a duplicate asset cell.

### Matrix parity

`Asset_MatchesV1Csv_AndHasNoMutuals`

Load `TypeChemistryMatrix.asset` and `COMBAT_Interaction_Matrix_v1.csv`. Assert 57 cells, the same attacker/defender pairs, the same status and recipient, Backlash parameter Bot on the two Backlash cells, zero pairs where both directions are filled, zero cells whose attacker or defender is Meme. Assert the direction CSV’s ADV set is the same 57 pairs.

`Triangles_AreOneWay`

For each loop in section 1.2, assert the three forward edges are Advantage and the three reverse edges are Resisted.

### Resolver

`MonoType_AdvantageAppliesStatus_ResistAppliesNone`

Gas into Block: relation Advantage, multiplier 1.2, attacker gains Pumped. Block into Gas: relation Resisted, multiplier 0.8, nobody gains a status.

`DualType_AdvPlusResist_IsNeutral_AndPrintsNothing`

Defender primary Block, secondary Burned. Attacker Gas. Gas→Block is a cell, Burned→Gas is a cell, so the score is +1 and −1. Assert Neutral, multiplier 1.0, and no new status. This locks the cancel that no species uses yet.

`DualType_TwoAdvantages_StayAt1_2_AndPrimaryWinsTheStatus`

Defender primary Frozen, secondary Shard, attacker Burned. Both cells are Rugged, so the status happens to match. Use a made-up second cell with a different status in the test matrix. Assert multiplier 1.2, not 1.44, and the status is the primary type’s status.

`MultiHit_MultiplierEveryHit_ReactionOnce`

A 3-hit Bot move into Node. Assert Drained is applied once. Assert each hit’s chemistry multiplier is 1.2.

`TypelessMove_UsesPrimaryType`

A `baseType == None` move on a Burned attacker into Gas. Assert Advantage and Rugged. The same move on a Node attacker into Gas. Assert Neutral and no status.

`StabFlagOff_AndTypelessWouldReceiveStabIfFlipped`

With the default config, a same-type typed hit and a typeless hit on the same species produce the same stab factor, 1. Assert `enableStab` is false. A second assertion, behind a local config with `enableStab` true, documents today’s bug: the typeless hit’s stab factor equals the typed hit’s. That assertion should be inverted when P5 lands: typed stab 1.2, typeless stab 1.

### Bodies

`Jammed_DoesNotChangeAccuracy` (locks today)

Gas into Node. Assert Jammed is on the defender and `stageAccuracy` is 0. When P1 lands, replace it with `Jammed_DropsAccuracyBy1_AndReverts`: after apply, Accuracy stage is −1; after the status expires, it is 0; it never reaches −2.

`Backlash_StoresBot_AndDealsNoChip` (locks today)

Burned into Bot. Assert a Backlash status with parameter Bot and no HP change from the status itself. When P4 lands, a contact hit while it is up deals `floor(MaxHP × 0.06)` once that turn and does not call chemistry again.

`Rugged_ThreeTicks_ThenRefreshDoesNotBumpCounter`

Apply Rugged. Run three end steps. On 134 MaxHP assert damage 8, then 16, then 25, and counter 4 if the cap is still 15. Reapply before expiry and assert the counter is unchanged and duration is 3. When P2 lands, assert the counter never passes 3 and the fourth end step is still `floor(MaxHP × 3/16)`.

`Kindled_AndRugged_BothTick_KindledFirst`

A Burned with Kindling lands an advantage hit. The end-step log contains “Kindled burns” before “Rugged scorches”. HP loss is the sum of the two floors. A second hit the same turn does not add a second Kindled.

`Corroded_MeleeDefStaysAfterExpiry` (locks the bug)

Glitched into Node, two end steps, then expire the status. Assert Melee Def stage is −2 and Ranged Def is 0. When P3 lands, assert both defenses were −1 while the status was up and both are 0 after it expires.

`Shattered_MeleeDefAppliesBeforeThisHit`

Mined melee move into Block at equal attack and defense. Record damage with the stage drop suppressed and with it live. Assert the live hit is about 1.5× the suppressed hit, times the 1.2 chemistry on both, and that current HP also fell by about 8% before the hit. Assert Ranged Def did not change. When P3 lands, assert this hit does not get the defense multiplier and the next melee hit does, and that a swap does not delete the drop until the status expires.

`Dusted_EvasionClampsA95PercentMove`

Apply Dusted. Assert Speed stage −3 and Evasion stage −3. Assert `ComputeHitChance` on a 95% move is 1. When P3 lands, assert Evasion is unchanged and Speed is −2, and a 95% move is not forced to 1 by Dusted.

`Leaked_TaxesTheActingAlly`

One active is Leaked. The partner acts. Assert the partner loses `floor(partner.MaxHP × 0.10)` and the leaked body does not also lose HP on that action. Assert an end step does not also tick Leaked as a DoT.

`Cascade_ResetsOnSwap_AndCanKo`

Three Glitched advantage hits into Block. On 134 MaxHP the third application deals 33 and can set `isAlive` false. Swap the Block after two applications and assert the counter is gone. Assert Melee Def stages, if any, are not cleared by that swap.

`Drained_DoesNotChangeThisTurnsActionWindow`

Defender has 2 AP, spends 2, then Drained is applied, then the end step runs. Assert AP is 0, not −1. Next turn starts with +2. A second case: they pass at 2 AP, the end step drains to 1, the next turn opens at 3.

`Defueled_RemovesNewestHarmfulChemistry_Only`

Attacker holds Rugged and then Jammed. They land a Defueled cell. Assert Jammed is gone and Rugged remains. A second attacker with no chemistry debuff, including one who holds Kindled, is unchanged.

`EncryptedSeal_BlocksRugged_NotPumped`

A sealed defender hit by Burned takes ×1.2 and does not gain Rugged. An attacker who hits their Pumped cell still gains Pumped.

### Census

`Pools_HaveACommonDamagingMove`

For every species except Ghost, assert the union of learnable, exclusive, initial, and off-type ids contains a damaging move of rarity Common. Today this fails for Rattlestake, Cofi Lootwak, and Blockwyrm. That failure is the P6 claim.

`WildTables_OmitBotAndCompressed`

Build the merged zone tables the script builds. Assert no entry’s species primary type is Bot, Compressed, or Meme. This documents the placement hole. It should be deleted or inverted when those species are placed.

## What this round is not

It does not retune move power, it does not pick Cofi’s beam, and it does not move the 57 edges. It does not give Meme chemistry. It does not change Kindling’s 6%. The pass plan’s stack exponents are not a proposal to bring back.
