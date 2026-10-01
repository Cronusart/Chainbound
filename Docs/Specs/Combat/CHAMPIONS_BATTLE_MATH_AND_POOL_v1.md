# Champions Battle Math and Pool Comparison

Research note, 2026-09-30. This does not change combat data, species stats, or the resolver. It records how Pokémon Champions lines up health, combat stats, and move power against the current Chainbound roster, then how wide each pool's base stats actually are.

Champions numbers below are the published Regulation M-A adjusted bases unless a row is marked as usage. Ranked rules as of this note are Regulation M-C (2026-09-09 through 2026-12-02). M-C adds species on top of M-A and M-B. M-B patch notes change moves, abilities, and items. They do not publish a reversal of the base-stat adjustment.

## Sources

- Chainbound damage: `DamageCalculator` uses `power × (attack ÷ defense) × progressionDamageFactor × roll + 1`. `progressionDamageFactor` is 1. The roll is 0.85–1.00. Same-type is 1.2.
- Chainbound species: the twelve `Assets/Data/Combat/ChainlingSpecies` assets with real stats. `Ghost.asset` is an empty placeholder and is left out.
- Champions damage: the mainline level-50 formula, as published by ChampsDex, VGC.tools, and Game8. Before type, same-type, items, and the 0.85–1.00 roll, damage is about `0.44 × power × (attack ÷ defense) + 2`. Same-type is 1.5. Game8 lists doubles spread damage at two-thirds.
- Champions bases: The Game Haus Regulation M-A table, 2026-04-11. Every checked row is the main-series base plus 75 HP and plus 20 to each other stat (Pikachu, Snorlax, Ditto, Abomasnow, Sneasler). The flat add does not change who has more stats than whom. It shrinks the ratio between a high stat and a low one.
- Champions pool size: Bulbapedia lists 186 species and 59 Mega Evolutions for the game. The M-A table parsed here is 211 resting battle forms (regional forms, sizes, and gendered forms are separate rows) and 55 Mega rows. Seven Mega rows in that table dropped the "Mega" label; they were folded back out of the resting set by keeping the lower total when a name appeared twice.
- Usage: Pikalytics Regulation M-C sample, and the Baltimore Regional (1,081 players). Those two samples do not agree on exact percentages. Both agree on the same leading names.

## 1. Health, stats, and move power

Level is a constant in Champions. Every competitive battle uses the same level-50 coefficient, so the comparison here ignores progression.

A matched-stat hit in Champions is a smaller slice of the health bar than the same kind of hit here.

| Matchup | Champions | Chainbound |
|---|---|---|
| Standard attack, matched stats | An 80-power same-type hit into a base-80 Pokémon is about 47–56 damage into roughly 155 HP. That is 30–36%, a three-hit knockout. | Validape using a 64-power move on another Validape is about 65–77 damage into 144 HP. That is 45–53%, a two-hit knockout. |
| Committed attacker into a frail defender | A maxed attack stat and a same-type 90-power move is a two-hit knockout. A resist turns that into about four hits. | Kitsauto's 70 attack into Lad's 28 defense and 90 HP, with a 64-power move, is a one-hit knockout. The attack/defense ratio is 2.5. |
| Wall | An invested wall near 250 HP and high defense takes a same-type hit for about 16–18%. That is five or six hits before recovery. | Whale at 146 HP and 72 defense still loses 37–43% to a 64-power hit. That is a three-hit knockout. |

Champions is not uniformly tankier. The middle of the format is still a two- or three-hit knockout. The extra durability sits at the bulky end, and it comes from three things the raw species sheet does not show:

- The 0.44 coefficient means an 80-power move does not deal 80 damage.
- Permanent attack/defense gaps are cushioned. A nature is 10%. A 32-point stat investment moves one stat by 32. Large swings are temporary stages: +1 is 1.5×, −1 is two-thirds.
- Doubles adds shared durability rules: spread hits at two-thirds, Protect buys a turn, and several items chip or heal a percent of max HP.

Our 64-power moves deal about 64 when attack and defense match. The 0.75 power pass changed the labels on `Moves.csv`. It did not insert a coefficient between power and health. `AllEnemies` still hits at full power unless a specific effect such as Splinter applies a cut.

## 2. What the full pool's stats actually look like

### Resting forms, after the Champions adjustment

211 resting rows from the M-A table:

| | Minimum | Median | Maximum | Spread |
|---|---:|---:|---:|---:|
| Total | 495 (Pikachu) | 675 | 826 (Eternal Floette) | 331 |
| HP | 110 | 153 | 235 (Snorlax) | 125 |
| Speed | 40 (Torkoal) | 98 | 162 (Dragapult) | 122 |

The tenth percentile total is 635. The ninetieth is 715. Most of the roster lives in a band about 80 points wide, with a long tail underneath (Pikachu at 495) and a short tail above (Dragonite 795, Dragapult and Garchomp and Archaludon 775, Palafin Hero 825, Eternal Floette 826).

Subtracting the flat buff puts the same rows back on main-series scale: totals 320–651, HP 35–160, the same 331-point total spread and the same 125-point HP spread. The buff did not compress the gap between the strongest and weakest species. It only made a 185 attack versus a 60 defense look like less of a multiple than 165 versus 40.

Megas are a second ceiling, not a second roster. The 55 Mega rows run from 655 to 875. Mega Dragonite, Mega Garchomp, and Mega Tyranitar share 875. A player may Mega Evolve once per battle. The Mega total does not describe the creature while it is sitting on the bench.

### Roles inside that spread

High total is not the same thing as high usage.

| Creature | Champions total | What it is doing in M-C |
|---|---:|---|
| Eternal Floette | 826 | High ceiling. Pikalytics lists Eternal Floette around 10% usage, not at the top. |
| Palafin, Hero | 825 | A form-change attacker. Strong total, not the format's most common Pokémon. |
| Dragonite | 795 | High total. Behind the support-and-priority core in both usage samples. |
| Dragapult | 775 | Fast attacker. Present, not leading. |
| Kingambit | 725 | Real meta. Pikalytics about 25%. Baltimore about 26% usage and a winning record. |
| Incineroar | 705 | Real meta. Pikalytics about 28%. Fake Out and Intimidate, not a stat ceiling. |
| Sneasler | 685 | Real meta. Pikalytics about 37%. Baltimore about 47%. |
| Farigiraf | 695 | Trick Room and support. Pikalytics about 18%. |
| Pelipper | 615 | Weather support. Pikalytics about 15%. Baltimore about 12%, and a losing record in that event. |
| Whimsicott | 655 | Speed control. Pikalytics about 11%. |
| Pikachu | 495 | Legal, and the bottom of the published sheet. |

Rillaboom leads both usage samples (Pikalytics about 38%, Baltimore 54.8% even on Day 2) and is not in the April M-A stat table. It was added with a later regulation. Its main-series total is 530. This note does not invent a Champions row for it.

Baltimore also showed the split between "brought a lot" and "winning." Rillaboom, Sneasler, and Salamence were all under a 50% win rate in the 1,081-player field. Charizard, Sylveon, Garchomp, Archaludon, and Kingambit were brought less and won more. Mega Absol Z, despite a high Mega total, has shown up as a low-usage, low-win experiment in early M-C reports. The pool's stat ceiling and the pool's meta are different lists.

The niche layer is real and it is not the bottom of the sheet. Pelipper, Whimsicott, Indeedee, and Torkoal are there to set weather, redirect, or move first. Toxapex (670 total, 172 defense, 55 speed) is a wall that sits near the median total. Off-meta in this game is mostly "no role the current teams need," not "the base stats were too low to function."

### Our sheet, from the live assets

Six stats, same shape as theirs: HP, melee attack, melee defense, ranged attack, ranged defense, speed. Totals are the sum of those six. Stats are used raw. There is no level cushion and no 32-point investment pass.

| Species | Role | HP | Melee atk | Melee def | Ranged atk | Ranged def | Speed | Total |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| Cinislug | Wild | 134 | 31 | 54 | 63 | 42 | 28 | 352 |
| Caw | Wild | 98 | 30 | 42 | 60 | 40 | 84 | 354 |
| Whale | Wild | 146 | 48 | 72 | 24 | 50 | 18 | 358 |
| Kreechank | Wild | 124 | 63 | 51 | 37 | 32 | 52 | 359 |
| Deghoul | Wild | 106 | 72 | 36 | 40 | 34 | 72 | 360 |
| Ghosty | Wild | 102 | 32 | 40 | 70 | 48 | 68 | 360 |
| Lad | Wild | 90 | 82 | 28 | 42 | 28 | 90 | 360 |
| Snek | Wild | 110 | 28 | 58 | 55 | 52 | 57 | 360 |
| Cofi Lootwak | Boss, not catchable | 168 | 18 | 84 | 24 | 84 | 16 | 394 |
| Gilarime | Starter | 148 | 50 | 65 | 60 | 55 | 45 | 423 |
| Kitsauto | Starter | 128 | 70 | 50 | 55 | 45 | 75 | 423 |
| Validape | Starter | 144 | 65 | 55 | 50 | 55 | 55 | 424 |

The eight wild species sit between 352 and 360. That is an 8-point spread. The three starters sit together at 423–424, about 64 points above the wild cluster. Cofi sits between them at 394, with the points shoved into HP and both defenses.

Inside a shared total, the wild roster already has the three competitive jobs:

- Glass offense: Lad, 82 melee attack, 28 defense, 90 speed. Best attack versus its own defense is 2.93.
- Wall: Whale, 146 HP, 72 melee defense, 18 speed. Best attack versus its own defense is 0.67.
- Support-shaped bulk: Snek, defenses near its offenses, no extreme.
- Fast special: Caw, 84 speed and 60 ranged attack, on the same 354 total as the others.

Cross-species, Kitsauto's 70 attack into Lad's 28 defense is a 2.5 ratio before chemistry. That is the same neighborhood as Champions' worst realized cross-species case (Rampardos's 185 attack against Beedrill's 60 defense becomes about 2.56 after the level-50 stat formula, down from 3.08 on the raw adjusted bases). Our ordinary pairs are calmer. Validape into Validape is 65/55, about 1.18.

## 3. What the disparity comparison says

Champions has a wide pool and a narrow meta. The resting totals span 331 points, and the middle half of the roster is still only about 80 points apart. Usage then ignores both tails. The format is led by Fake Out, priority, Intimidate, weather, and one Mega slot. A 875 Mega that does not fill one of those jobs stays off teams. A 615 weather setter gets brought because the team needs rain.

Our pool is the opposite shape. The catchable wild totals are already tied. Disparity is where the points sit, not how many points a species was given. That produces archetypes on purpose: Lad is frail because 28 defense is on the sheet, not because Lad was given a smaller budget than Snek. The starter tier is the only flat step up, and it is about 64 points, smaller than the gap from Champions' median (675) to its top resting forms (826).

Two consequences follow from that, still as research rather than a data change:

- Adding future species into the wild band can keep the 352–360 total and still read as different creatures, the way Caw and Whale already do. A new species that breaks that band by 100 points would be a different kind of addition than anything currently catchable. Champions can absorb that kind of addition because the formula and the doubles rules shrink how much of the extra total shows up as damage. We would feel the whole total.
- The fragile end of our sheet is already as extreme as Champions' worst attack-versus-defense ratio, and we apply it with no cushion. Champions can print Rampardos at 185 attack because the stat formula and the 0.44 damage coefficient keep that from being a one-hit against the whole roster. Lad at 82 attack and 28 defense does not have that cushion. New glass cannons in this pool get more dangerous, per point of defense they give up, than a new glass cannon in Champions.

The three mechanics worth a later design pass, unchanged from the battle-math read:

1. A shared coefficient so a matched-stat hit is about a third of a mid health bar, and same-type or chemistry is what turns that into a two-hit.
2. A softer floor on permanent defense, with the large swings left on stages and chemistry.
3. Doubles durability as shared rules: a spread cut, a common one-turn protect, and percent-of-health chip or recovery.

None of those are applied by this note.
