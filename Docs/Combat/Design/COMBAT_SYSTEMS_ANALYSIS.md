# Combat systems analysis

**Status: analysis plus proposals.** Nothing here is implemented. Open audit items stay open: the unimplemented move rows, Ice Wall / Ash Guard / Compound Guard hit budgets, `COMBAT_Moves.txt` §6.1, HODLed's ramp, Fox Feint's 999, and Cofi's missing 6 AP beam. This note does not edit `Docs/GameTheory/`.

Move and reaction proposals that already live in [COMBAT_DESIGN_IDEAS.md](COMBAT_DESIGN_IDEAS.md) are pointed at, not repeated as new cards.

## 1. Type matchup and chemistry

Source of the live chart: `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv` (57 filled cells, 19 distinct outputs). Resolution: `ChemistryResolver`. Damage factors: `CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR` 1.2 and `CHEMISTRY_DISADVANTAGE_FACTOR` 0.8. STAB is off in `Assets/Data/Combat/Config/CombatResolverConfig_Default.asset` (`enableStab: 0`). The constant `STAB_MULTIPLIER` 1.2 is unused while that flag stays false.

### How a cell becomes damage and a status

For each of the defender's types, a filled attacker→defender cell adds 1 to the relation score and a filled defender→attacker cell subtracts 1. The first forward cell, primary type first, is the reaction candidate. The status is applied only when the final relation is Advantage (`ChemistryResolver`: the apply branch requires `relation == Advantage`). A mono-type forward cell is therefore always Advantage, and it always prints its status. A mono-type reverse cell is Resisted and prints nothing. Zero mutual pairs exist in v1 (checked across all 14 types). Dual typing cannot stack past ×1.2 / ×1.0 / ×0.8 (`COMBAT_Chemistry.txt` §2).

### Coverage

Outgoing wins are filled cells in that type's row. Weaknesses are columns where someone else has a cell and this type has none back. Neutrals are the rest of the 13 other types. Meme's row and column are empty, so its "13 neutral" is the whole chart.

| Type | Wins | Weaknesses | Neutrals | Wins into |
|---|---|---|---|---|
| Burned | 7 | 4 | 2 | Gas, Frozen, Mined, Staked, Compressed, Bot, Shard |
| Shard | 6 | 4 | 3 | Node, Encrypted, Forked, Mined, Compressed, Glitched |
| Frozen | 5 | 5 | 3 | Gas, Staked, Bot, Glitched, Shard |
| Mined | 5 | 5 | 3 | Gas, Block, Encrypted, Frozen, Staked |
| Bot | 5 | 5 | 3 | Node, Block, Mined, Staked, Glitched |
| Node | 4 | 4 | 5 | Frozen, Forked, Mined, Compressed |
| Gas | 4 | 4 | 5 | Node, Block, Staked, Bot |
| Encrypted | 4 | 4 | 5 | Gas, Burned, Frozen, Bot |
| Forked | 4 | 4 | 5 | Burned, Mined, Bot, Glitched |
| Glitched | 4 | 4 | 5 | Node, Block, Encrypted, Burned |
| Block | 3 | 5 | 5 | Burned, Forked, Shard |
| Staked | 3 | 5 | 5 | Forked, Compressed, Shard |
| Compressed | 3 | 4 | 6 | Block, Encrypted, Frozen |
| Meme | 0 | 0 | 13 | — |

The starter triangle in `COMBAT_TypeProfiles.md` is real on this chart: Node beats Frozen, Frozen beats Bot, Bot beats Node, and none of those three edges run backwards.

### What is dominant, thin, or dead

**Burned is the chart's wide type.** Seven advantages and only two neutrals. Five of its seven cells are `Rugged @Foe`. Landing Burned into Gas, Frozen, Mined, Compressed, or Shard is the same DoT. Rugged does run: at end of turn the tick is `counter / 16` of MaxHP, then the counter increments, capped at 15 (`StatusProcessor`, `CombatConstants.RUGGED_TICK_CAP`). Catalog duration is 3 (`Statuses.csv`), so a single application is about 1/16, then 2/16, then 3/16, which is 6.25% + 12.5% + 18.75% of MaxHP across three end steps. That is a large rider on top of ×1.2, and it is the same rider on five matchups.

**Shard is the second-widest attacker** (6 wins) and its cells are mostly `Shattered @Foe` (four of the file's four Shattered cells sit on Shard or on Mined→Block). Shard species back this up: Crackett is the only Epic species, with base melee attack 86 and speed 92 (`Crackett.asset`).

**Block and Staked lose more often than they win** (3 wins, 5 weaknesses). Both are the species stat walls: Blockwyrm is Legendary, MaxHP 168, melee defense 86, speed 24; Cofi Lootwak is MaxHP 168, both defenses 84, speed 16. The chart asks them to be hit, and the stat line agrees. Their kits have to win by mitigation and sustain, because the damage relation will not.

**Compressed is the quiet type.** Three wins and six neutrals, the most neutrals of any type that has cells. A Compressed mirror and most of the chart deal ×1.0 and print no status.

**Meme is dead on the chart.** No cell, no advantage, no resistance, no reaction. The only Meme species, Ghost, is base 0 on every stat (`Ghost.asset`). Meme moves still exist in `Moves.csv` (10 rows). They never receive chemistry.

### Reactions that repeat, and reactions that barely exist

Counts from the 57 cells:

| Output | Cells | Note |
|---|---|---|
| Jammed @Foe | 8 | Latency default. Accuracy −2 is still an open status-body item. The cell count is real; the debuff landing is not settled. |
| Anchored @Foe | 6 | |
| Drained @Foe | 5 | |
| Rugged @Foe | 5 | All five are Burned's row. |
| Pumped @Self | 4 | |
| Dusted @Foe | 4 | |
| Shattered @Foe | 4 | |
| Defueled @Self | 3 | |
| Corroded @Foe | 3 | All three are Glitched's row. |
| Isolated, Backlash Bot Dmg, Splinter, Leaked, FOMO | 2 each | |
| Firewall, Slowed, Drip, Siphoned, Cascade Failure | 1 each | |

`COMBAT_Chemistry.txt` §4 still lists Reinforced in the Structure family. v1 has no Reinforced cell. That status cannot be reached from the live chart. This note does not decide whether to put the cell back.

Slowed exists once (Frozen→Staked). Drip once (Staked→Compressed). Siphoned once (Staked→Shard). Cascade Failure once (Glitched→Block). Those are the only way to see that status. A player who never brings that pair never sees the family.

Self outputs (Pumped, Defueled, Splinter, FOMO, Firewall, Drip, Siphoned) print only on Advantage. A neutral or resisted hit does not grant the self-buff. Pumped's +1 AP at end of turn (`Statuses.csv`) is a reward for a matchup you already win at ×1.2.

### Unreachable with the current roster

All 19 species assets under `Assets/Data/Combat/ChainlingSpecies/` have `secondaryType` 0. The primary-type tie-break and the dual-type score cancel in `ChemistryResolver` have no species to run on. Every live matchup is mono-type, so every filled cell is reachable by bringing that attacker into that defender, and every empty pair is a permanent neutral.

**Proposal.** Leave ×1.2 / ×0.8. When the chart is next edited, cap one output at 4 cells and move Burned's extra Rugged cells and the extra Jammed cells onto the one-cell families (Slowed, Drip, Siphoned, Cascade Failure) so those statuses can appear in more than one pair. Do that only after Jammed's accuracy body is decided. Adding a secondary type to even one species is a separate content change; until then the dual-type rule is unused code.

**Proposal.** Give Meme a real row only as its own pass. The type profile already marks Meme out of the chemistry pass. Ghost's zero stat line should stay out of balance math until those stats are authored.

## 2. Utility AI, and the other personalities

Personalities are `AIPersonality` in `Combat2TestHarness.cs`: Dumb 0, Aggressive 1, Defensive 2, Utility 3, Random 4. Encounter assets that set one: Intro First Creature is Aggressive (`aiPersonality: 1`), Cofi Lootwak is Utility (`aiPersonality: 3`), Manual Encounter is Random (`aiPersonality: 4`). `AuthoritativeBattleEncounterProvider` defaults to Aggressive. No checked-in encounter uses Defensive.

Every AI builds actions through `AIHelpers.GetLegalActions`, which calls `CombatAction.UseMove(teamIndex, moveIndex)`. That overload sets `targetTeamIndex` and `targetSlotIndex` to −1 (`CombatState`). With no explicit target, `CombatResolver.ResolveSingleSelectedTarget` returns `GetFirstAliveActive`, which walks active slots from 0. `FindLeastDebuffedEnemy` and `AIHelpers.PickTarget` affect scores only. They are not written onto the action.

`AIHelpers.EstimateDamage` is `Power * (Atk / 100) * average hits * accuracy`. It does not divide by defense, and it does not apply chemistry, the 0.85–1.0 roll, or crits. `DamagePerAP` returns 0 when AP cost is 0.

### What each personality actually does

**Utility (`UtilityStatusAI`).** A status-utility move starts at +30, plus 15 per status payload, plus 10 per enemy-targeted negative stage, plus 10 if the least-debuffed enemy has an empty status list, then minus AP cost. Support starts at 15, plus 10 for each active ally under half HP, then minus AP cost. A damaging move scores `DamagePerAP * 2`, plus 20 per status payload, plus 8 per enemy-targeted negative stage, then minus AP cost. Swap scores 1. Pass scores 0. There is no recharge term and no current-AP term.

At a Common-median attack near 54 (see section 3), a power-64 accuracy-95 move at 2 AP scores about 31. A plain 1 AP utility with no status scores 29. A utility that carries one status scores 44. Bean Sip is a 1 AP utility with a self Speed stage, so the enemy-debuff bonus does not apply and the score stays 29. Cofi's Bean Beam uses ranged attack 24 and power 12: the estimate is about 2.7 before the AP penalty, so the beam scores under 1. While Sip is legal, this personality sips. It will also sip, or cast any other status utility, when the target is at 1 HP.

The "+10 if a clean enemy exists" bonus is added to every utility move in that position. It does not choose which enemy is hit.

**Aggressive.** Damaging moves score `DamagePerAP * 10`, plus 50 if `EstimateDamage` is at least the lowest enemy's current HP. Non-damaging moves score 0.1. Swaps score 0.05. The KO check uses the same Atk/100 estimate, so against a defense near 50–80 it compares a number that is roughly `Atk/100` with a real hit that is `Atk/Def`. It will call a KO that the tank survives, and it will miss a KO against a low-defense target whose defense is under 100. It never heals.

**Defensive.** Order is fixed: a believed KO, then a swap under 20% HP, then the first support or `HEAL`-tagged move if any ally is under 40% HP, then the first self-utility with a positive stage if every ally is above 55% HP, then the best `DamagePerAP` attack. The first legal support wins, not the strongest heal. No encounter asset selects this personality, so the tank species do not currently receive the AI that benches and heals.

**Dumb.** First affordable damaging move in list order, otherwise the first non-passive. No swap. Kit order is the whole policy.

**Random.** Uniform over the legal list, including swaps.

### Patterns a player can abuse

1. **Lead with the wall.** Single-target enemy moves from all five personalities hit slot 0. The second active is safe until the lead faints or the move is `AllEnemies` / `Field`. Cofi's beam is `AllEnemies`, so this exploit does not apply to Bean Beam. It applies to Intro's Aggressive kit and to any future single-target boss.
2. **Stay debuffed and low.** A Utility fighter with a status move scores in the 40s and will not take a lethal hit. Chipping Cofi into range does not make Sip stop, because Sip's 29 still crushes Beam's score, and any real status utility crushes a power-64 hit.
3. **Ignore the bench against Utility and Aggressive.** Swap scores 1 or 0.05. They will not bench a 1 HP attacker to keep a buff or a recharge. The player's bench rules (slot AP, frozen statuses) are not mirrored.
4. **Put the cheap move first against Dumb.** Selection is list order. A tutorial or scripted Dumb fight uses whatever damaging row was authored first.
5. **Resists are free.** Chemistry never enters the score. A ×0.8 target in slot 0 is hit forever. A ×1.2 target in slot 1 is ignored.
6. **Recharge is free in the score.** A legal recharge utility keeps the +30. Bean Sip is the live case. AP Boost (`rechargeTurns` 3, skip two upcoming turns, +2 AP) would be treated as an ordinary 0-cost utility if a Utility AI ever held it.

`all_in` has CSV AP cost 0. `DamagePerAP` then returns 0, so Aggressive scores that hit as 0, below every other damaging move. That row is an open unimplemented-cost item. This note does not pick a cost.

### Proposals

**Proposal A. Write the target onto the action. Cost: small, AI plus the existing `UseMove` overload.** `PickTarget` and `FindLeastDebuffedEnemy` already choose a body. Pass that slot into `CombatAction.UseMove(team, move, targetTeam, targetSlot)`. Utility aims at the cleaner enemy. Aggressive and Defensive aim at the lower HP or the higher threat. Slot 0 stops being the only legal victim. This is the highest-leverage AI change because it removes exploit 1 without a new action type.

**Proposal B. Lethal check before the +30. Cost: small, `UtilityStatusAI` only.** If `EstimateDamage` against the chosen target is at least that target's HP, pick the damaging move. Copy the shape of `DefensiveAI.FindLethalBlow`. Cofi and any later Utility boss will finish a 1 HP target. Pair it with Proposal C or the check stays biased by Atk/100.

**Proposal C. Estimate against the target's defense and chemistry. Cost: medium, `AIHelpers.EstimateDamage` gains a defender argument.** Use `Atk/Def` and ×1.2 / ×0.8 from the same matrix the resolver uses. The KO bonus and DamagePerAP then describe the hit the engine will roll, minus the 0.85–1.0 variance. Resists in slot 0 stop being the default.

**Proposal D. Recharge and hurt-ally weights. Cost: small, one scorer.** Subtract a flat amount for `rechargeTurns > 0` so a sip does not always beat a beam. Raise support above a naked +30 when an ally is under 40% HP, which Defensive already treats as danger and Utility does not. This matches the shortlist in the design-ideas note and still does not add the 6 AP beam.

**Proposal E. Assign Defensive to the walls. Cost: data on the encounter asset, no new code.** Blockwyrm-style and other high-defense encounters should use personality 2 so the swap-at-20% and heal-at-40% rules actually run. Cofi can stay Utility until Proposal B and D exist; Defensive would heal, and Sip is a utility, so the personality change alone does not fix the sip loop.

## 3. Pacing

### Model

Live combat has no level multiplier on the hit. `CombatConstants.PROGRESSION_DAMAGE_FACTOR` is 1. `ChainlingProgressionUtility.GetStatScaleMultiplier` returns 1 for every star count. Stars break speed ties (`GetProgressionTieBreakScore`) and do not change HP or attack. Rebirth does: `ProgressionConfig_Default.asset` adds `cycleAutoHpPercent` 0.05 of base HP per rebirth (flat HP auto is 0) and `cycleAutoStatFlat` 1 to each non-HP stat per rebirth (percent 0). Five rebirths are +25% MaxHP and +5 attack, defense, and speed.

Expected hit, stated so the arithmetic can be checked:

`Power * (Atk / Def) * 1 * 0.925 * (Accuracy / 100) * chemistry`

0.925 is the midpoint of `RANDOM_ROLL_MIN` 0.85 and `RANDOM_ROLL_MAX` 1.0. Crits are left out of the base figure. At the default 6.25% chance and 1.5× multiplier they add about 3% expected damage, which does not move a turns-to-KO band. No stages, no mitigation, no STAB. One attacker, one target, the move available every turn.

Species bases are the 19 assets in `Assets/Data/Combat/ChainlingSpecies/`. Ghost is 0 on every stat and is excluded from the medians below. Nobody has a secondary type.

| Species band | n (Ghost excluded from Common) | Median MaxHP | Median melee atk | Median melee def | Median speed |
|---|---|---|---|---|---|
| Common | 10 | 132 | 58.5 | 54 | (mixed; Cofi is 16, Kitsauto is 82) |
| Uncommon | 4 | 116 | 38 | 47 | 55 |
| Rare | 2 | 122 | 60 | 55 | 53 |
| Epic | 1 (Crackett) | 112 | 86 | 46 | 92 |
| Legendary | 1 (Blockwyrm) | 168 | 58 | 86 | 24 |

Common MaxHP values behind the 132 median: Validape 156, Balloom 130, Deghoul 106, Keyper 102, Cinislug 134, Gilarime 161, Cofi 168, Rattlestake 110, Kitsauto 139, Kreechank 124. Species rarity is not an HP ladder. Uncommon sits under Common. Epic is the glass cannon. Legendary is the wall. Stars do not open a second ladder.

### Turns to KO one body

Baseline body: 132 HP, Atk/Def = 1, chemistry ×1.0, accuracy as authored. A power-64 accuracy-95 hit expects `64 * 0.925 * 0.95 = 56.2` damage. **132 / 56.2 = 2.3 turns.**

The Common melee medians are about 58.5 attack and 54 defense, ratio about 1.08, which pulls that estimate to about **2.2 turns**. Chemistry ×1.2 is about **1.9 turns**. Chemistry ×0.8 is about **2.9 turns**.

| Attack | Expected damage | Turns vs 132 HP |
|---|---|---|
| Typed Common, power 64, acc 95, 2 AP, neutral, ratio 1 | 56.2 | 2.3 |
| Same, advantage ×1.2 | 67.5 | 2.0 |
| Same, resisted ×0.8 | 45.0 | 2.9 |
| Impact Knuckle, power 42, acc 90 (typeless Rare) | 35.0 | 3.8 |
| Vector Shot, power 38, acc 90 | 31.6 | 4.2 |
| Both actives use power 64 into the same body | 112.5 | 1.2 |
| Bean Beam, power 12, acc 95, ratio 1, one target | 10.5 | 12.5 |
| Bean Beam, Cofi ranged attack 24 vs defense 48 | 5.3 | 25.0 |
| Power 64, ranged attack 54.5 vs Cofi ranged defense 84, HP 168 | about 36 | 4.6 |
| Power 64 vs Blockwyrm ranged defense 72, HP 168, attack 54.5 | about 42 | 4.0 |
| Rebirth 5 vs rebirth 5, power 64, ratio still ~1, HP 132 × 1.25 | 56.2 | 2.9 |

A 3 AP move cannot be used every turn from 0 AP with +2 regen (`AP_PER_TURN`). The cadence is attack, attack, skip, repeating after the first idle turn: two casts per three turns. Fork Split's per-target clock is longer than its power suggests for that reason, and it hits both foes, so the fight clock is the body with more HP, not two sequential KOs.

### Do regen, recharge, and healing drag or snowball?

**Regen matches the modal attack, so ordinary fights do not wait.** Forty of 72 damaging moves cost 2 AP (`Moves.csv`). +2 per turn means that attack is available every turn from the first turn, if the fighter started at 0 AP (`currentAP` has no other default). The fight length above is the fight length. Banking to 10 is optional, not required, for the power-64 line.

**Recharge almost never inserts a skip.** Five rows have `RechargeTurns` above 0. Bean Sip is the one that shows up in a real kit: it locks the sip for one upcoming turn and the fallback beam does not KO. That pair drags. AP Boost and Extend Protocol skip two upcoming turns and are not on the Cofi kit.

**Healing does not drag a power-64 fight.** `heal_ally` and `hub_heal` restore 15% MaxHP (about 20 HP on a 132 body) and cost the turn's 2 AP, so the healer deals 0 while the opponent deals about 56. `stake_heal` and Hibernate restore 25% (about 33 HP) on the same 2 AP clock. `sustain_regen` is 3% at end of turn (about 4 HP) against a 56 HP hit. `deep_sustain` is 6%. Emergency Recovery is 15% once, under 25% HP. One heal is a fraction of one hit. The HP swing favors the side that kept attacking.

**Healing drags when the hit is smaller than the heal.** Bean Beam into a real defense expects about 5–11 damage. Bean Sip heals 8% of 168, about 13 HP, and also grants +1 AP. Stake Heal, which is on Cofi's learnable pool, is 25%. In that kit the defender's HP does not trend down on the turns the beam is the only damage.

**The snowball that exists is focus fire plus stages, not rarity.** Both enemies default to slot 0, so two power-64 hits remove a 132 HP lead in about 1.2 turns. The second active may never act. A +1 attack stage is 1.5× (`ChainlingInstance.StageMultiplier`). Spending a turn to gain +1 and then hitting twice at 1.5× deals about 0 + 84 + 84 = 168, the same as three unbuffed hits. A +2 stage is 2.0× and does shorten the fight after the setup. Permanent move-column stages (Bean Sip's Speed +1 is one) stick around; timed stages now fall off when the status ends. Speed stages do not change the damage formula unless the move attacks with Speed.

**Proposal.** Treat 2–3 hits as the intended KO for a neutral power-64 exchange, and treat anything past 6 hits as a drag that needs a reason (a wall's defense, a recharge lock, a beam under 20 power). Cofi is that drag with the current beam. The three beam options in the design-ideas note are the pacing fix, still unchosen. A second proposal: when Proposal A in section 2 lands, retune focus fire, because two attackers will then sometimes split and the 1.2-turn lead KO becomes a choice instead of the default.

## 4. Per-type identity

Profiles are the table in `COMBAT_TypeProfiles.md`. Kit counts are `Moves.csv` (206 rows). Species notes use the assets above. Tweaks are proposals.

**Node** is supposed to route AP, extend buffs, and heal wide. The kit is 11 moves: 5 support, 4 utility, 2 damaging, no passive, no priority. `hub_heal` is 15% at 3 AP. `coordinated_shot` is already the typed Common power-64 hit with a partner rider. Extend Protocol's extend clause is an open unimplemented row, so the card does not yet do the profile's main verb. **Proposal:** add the Node Beacon passive from the design note (turn-start +1 AP if a partner is buffed). **Proposal:** leave the extend clause on the open list; until it runs, the identity button is Relay Cut (hand 2 AP to the ally), which the executor already supports.

**Gas** is supposed to tax AP and trade attack for defense. The kit is the smallest typed kit, 9 moves, no heal, no priority. The live tax is the passive `fee_collector` (foes with more AP lose 1 at your turn start). The chart is an even 4 and 4, so Gas is not winning by coverage. **Proposal:** Fee Tap, a 1 AP single-target `DrainAP`, as in the design note. **Proposal:** keep Gas Spike's timed +2 / −2 as the trade, and do not also give Gas a sustain heal. The missing heal is part of the identity.

**Block** is supposed to be the physical wall: barriers, grounding, defense ramp. The chart is 3 wins and 5 weaknesses. Blockwyrm's body matches (168 HP, 86 defense, 24 speed). The kit has 4 melee attacks, 4 utilities, 1 support, 1 passive, and no heal. `bulwark_blow` is the Epic that strikes with defense. Ash-style walls with hit budget 0 are an open item and are not Block's row; Block's working shape is closer to `barrier_wall` (typeless, hit budget 2). **Proposal:** Rampart from the design note, a Block support with an explicit hit budget of 2, without editing the open hit-0 rows. **Proposal:** put Defensive AI on Blockwyrm-style encounters (section 2, Proposal E) so the wall actually benches and heals.

**Encrypted** is supposed to strip AP and ignore debuffs. The kit is 5 utilities, 3 damaging, 1 support, 1 passive, no heal. No move uses `StealAP`. Encrypted Seal is a real status token on a move; a general "immune to debuffs" body is not what this note is deciding. **Proposal:** Cipher Skim, steal 2 AP, from the design note. **Proposal:** one Rare damaging row that already exists (`phantom_hack` drops Def and Speed) should stay the damage identity, and new Encrypted cards should stay on AP rather than adding a heal.

**Burned** is supposed to be the attrition attacker. The chart already is: 7 wins, five of them Rugged. The kit has 5 damaging moves, no passive, one spread (`ember_shower` is in the spread table in the balance doc). Cinislug's attack line is ranged 63 and melee 31, which fits a ranged DoT type and leaves the melee moves in the kit on the wrong stat. **Proposal:** add Cinder Idle, the "if you were hit, +1 AP" passive from the design note, so the type has a passive that rewards staying in. **Proposal:** when Rugged's five cells are thinned (section 1), keep at least two, so Burned's matchup still means the DoT and not only ×1.2.

**Frozen** is supposed to jam and lock speed. It has 6 utilities, the most status-shaped kit after Bot, plus Hibernate (25% heal and Def +2). Jammed is 8 cells and its accuracy drop is an open body, so Frozen's chart verb is ahead of the debuff. Slowed (Speed −2) is one cell, Frozen→Staked, and that body is the temporary stage the revert rule already knows. **Proposal:** point new Frozen reactions at Slowed rather than more Jammed, after the Jammed body is decided. **Proposal:** Hibernate's Def +2 is a move-column stage, so it stays after the heal. If the card is meant as a rest, move that +2 onto a timed status. That is a data change, and it is optional.

**Forked** is supposed to spread. It does: `fork_split` is power 46, accuracy 95, all foes, index 87.40 in the balance doc, above the typeless Rare spreads. `rapid_strike` is power 52, 1 AP, and priority. All four damaging rows are Common. The chart is an even 4 and 4, so the power is in the moves, not in coverage. **Proposal:** add Fork Line, a Rare single-target at power 64 with a debuff rider, from the design note, so the type has a Rare hit that is not another spread. **Proposal:** leave Fork Split's power in place until a dedicated spread pass; cutting it in the same breath as the typeless Rare lift removes the type's identity.

**Mined** is supposed to ramp and crack tanks. The chart beats Block, and Shattered is on Mined→Block. The kit is 5 damaging moves, including `deep_reap` with a real lifesteal opcode, plus a 20% heal support. Species are Kodiax (melee attack 70) and Minefolk (ranged attack 74). **Proposal:** keep the single-target ramp and do not add a Mined spread. **Proposal:** `ore_restoration` at 20% is already a strong sustain hit for a ramp type; new Mined cards should be damage or the existing lifesteal, not a second big heal.

**Staked** is supposed to siphon and anchor. Three species, including the 168 HP boss. The kit has the heals (Stake Heal 25%, Compound Returns 10%, Bean Sip 8%), a spread beam at power 12, and Yield, which grants +1 AP at end of turn for three ticks (`yield_cycle`). The chart only wins into Forked, Compressed, and Shard. **Proposal:** the boss beam options in the design note are the pacing tweak; the type's other damaging moves can stay near the typed Common band. **Proposal:** Yield is the identity AP button. New Staked sustain should be a percent heal or Yield, not another Drain/Steal, which belongs to Gas and Encrypted.

**Compressed** is supposed to be precise and efficient. The chart is the most neutral. The kit has 4 damaging moves, 4 utilities, 1 passive, no support, no Rare damaging move, and `lean_hit` at power 46 for 1 AP (index 43.70, well above the typed Common per-AP figure of 30.40). **Proposal:** Brace Yield, the Def-gated 8% ally heal, from the design note, so the type has a support. **Proposal:** when numbers can move, bring Lean Hit down toward Quick Jab's job (a small 1 AP chip) or toward the 30 per-AP band. Gas Vent and Anchor Tap in the design note are the chips that should exist around it.

**Bot** is supposed to deny AP and grind. The kit is 12 moves with 7 utilities, one spread (`automation_burst`), and a 15% heal at 1 AP (`self_repair_protocol`). The chart is a clean 5 and 5 and closes the starter triangle. **Proposal:** keep the 1 AP heal; it is the attrition stay-in tool and it is cheap enough to use between 2 AP attacks. **Proposal:** one utility in this kit should be a real `DrainAP` or `StealAP` so "AP denial" is a button, not only the type-profile sentence. Fee Collector is Gas. Bot does not have that row today.

**Glitched** is supposed to corrupt defense and punish a pattern. The chart is Corroded on three of its four cells, plus Cascade Failure once. The kit has `chaos_strike` (random 1.5× or 0.75×) and `bug_payoff` (+50% if the target has 2 debuffs), both real riders. **Proposal:** Mempool Mark from the design note, queuing `SearField` for next turn, matches "punish a pattern" with an opcode that already runs. **Proposal:** leave Cascade Failure as the one-cell spike. Copying it onto more Glitched cells makes every Glitched hit a 25% MaxHP bomb at 3 stacks (`COMBAT_Chemistry.txt` §5) and erases Corroded.

**Shard** is supposed to be the single-target blade. The chart is 6 wins. The kit has 4 damaging moves, 2 support, 2 passive, one spread (`shard_scatter`), and no heal. `crystal_bash` is power 0 with Def-scaling text that does not run; that row stays on the open list. **Proposal:** Stockpile Shot from the design note (power 46, accuracy 90, 3 AP, ×1.4 at 6 AP) is the "spend the stockpile" blade and does not rewrite `crystal_bash`. **Proposal:** `shard_scatter`'s missing Evasion rider is an open correctness item; do not add a second Shard spread while that row is unsettled.

**Meme** is supposed to be the wildcard, and the chart gives it nothing. The kit still has 4 damaging moves, 2 passives, a 20% heal utility, and `all_in` at power 98 with CSV AP cost 0. Ghost's stats are 0, so no species is currently carrying this kit into a real HP total. **Proposal:** decide `all_in`'s cost on the open list before any other Meme number moves. A free 98-power hit makes the rest of the type's identity unread. **Proposal:** keep Meme out of the v1 matrix until that pass exists. Authored Ghost stats can wait for the same pass; a 0 HP species should not be used as a pacing sample.

## Top findings

1. Burned wins 7 of 13 matchups, five of them the same Rugged DoT. Meme wins 0. No species has a second type, so the dual-type rule never runs. Jammed is 8 of 57 cells and its accuracy drop is still open.
2. Every AI sends `UseMove` with no target slot, so single-target hits land on active slot 0. Utility's +30 spends the turn on a status even at 1 HP. Chemistry and defense are absent from `EstimateDamage`.
3. Stars do not scale damage or HP. A neutral power-64 hit KOs a Common body (median 132 HP, Ghost excluded) in about 2.3 turns, or about 1.2 if both foes focus the lead. A 15% heal does not drag that fight. Bean Beam does.
4. Kits diverge from the profiles in a few repeated ways: Node has no passive, Compressed and Burned have no support or no passive respectively, Bot's "AP denial" has no drain row, and Meme's chart is empty while `all_in` is still an open cost.
