# Chainling Archetypes and Rarity Slots

Design note, 2026-09-30. Applied to species assets on 2026-10-01. Species ids are `blockwyrm`, `hopoch`, `minefolk`, and `marrowhead`. Renames are Keyper (`keyper`, was Ghosty, briefly Bootane), Rattlestake (`rattlestake`, was Snek), Humpacked (`humpacked`, was Whale), Forkrook (`forkrook`, was Caw), and Crackett (`crackett`, was Lad). Later additions are Kodiax (`kodiax`, Mined melee) and Balloom (`balloom`, Gas self-trade).

Sources: `Docs/Specs/Combat/COMBAT_TypeProfiles.md`, `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv`, `Docs/Specs/Combat/COMBAT_Moves.txt` section 12, `Docs/Specs/Combat/Data/Statuses.csv`, `SpeciesDatabase.asset`, and `RarityTier` in `Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs`.

## Chemistry rules used here

A winning chemistry hit deals 1.2× damage and applies that cell's one status. The return hit is 0.8× and applies no status. There is no mutual advantage. Walls (Block, Staked) have three forward wins. Shard has six. Mined has five. Statuses below are the v1 cell, not a move the species must learn.

Two loops already in the type spec are the reason these four were paired this way:

- **Yield:** Staked beats Shard, Shard beats Mined, Mined beats Staked.
- **Finality:** Mined beats Block, Block beats Forked, Forked beats Mined. Block also beats Shard, so the shield answers the bow directly.

## Blockwyrm

| | |
|---|---|
| Type | Block |
| Role | Melee Guard |
| Weapon read | Shield first, sword as a close deterrent |
| Sprites | `Assets/Sprites/Combat/Chainlings/BoryoDragon/` |

Block beats and resists Burned, Forked, and Shard. Into Burned the reaction is Firewall on self: for two turns, the first projectile each turn deals 30% less. Into Forked and Shard the reaction is Anchored on the foe: their next move must be single-target. That pins a spreader. It is a softer punish on a bow that was already aiming at one target.

The body stays planted. Preferred moves are barriers, defense ramp, and grounding. HODLed (defense climbs, stat drops are ignored) and Immovable (cannot be forced out) match the shield. One reliable contact strike is enough. Skip speed and burst moves.

Bad matchups are Gas, Mined, Compressed, Bot, and Glitched. The Mined hit is Shattered: 8% of current HP and defense −1. Boryo's answer is to be holding before that shot lands.

This is the catchable physical wall. Humpacked is Compressed and is the soft wall. Cofi Lootwak is an uncatchable Staked wall.

## Minefolk

| | |
|---|---|
| Type | Mined |
| Role | Ranged Striker |
| Weapon read | Rifle. Damage stays ranged. |
| Sprites | `Assets/Sprites/Combat/Chainlings/Minefolk/` |

Mined beats and resists Gas, Block, Encrypted, Frozen, and Staked. That is the ramp that crashes tanks.

| Defender | Reaction on the winning hit |
|---|---|
| Block | Shattered on the foe. Chip 8% current HP and defense −1. |
| Frozen | Dusted on the foe. Evasion −3 and speed −3 for two turns. |
| Gas | Pumped on self. +1 AP at end of turn for three turns. |
| Encrypted, Staked | FOMO on self. Attack +1 and about 1/16 max HP self-damage per turn for three turns. |

Preferred moves are single-target projectiles, conditional damage, and scaling. No team support. One shot can hit harder once the target's defense is already down.

FOMO's attack stage is authored as melee attack (`Statuses.csv` stage `MeleeAtk:1`). A rifle that only uses ranged attack would take the self-damage and the 1.2×, and would miss the attack bump. Keep a short contact follow-up, a stock strike, that spends that melee stage once the wall is open. The identity stays the rifle.

Losses are Node, Burned, Forked, Bot, and Shard. Shard's winning hit Shatters Mined the same way Mined Shatters Block.

## Marrowhead

| | |
|---|---|
| Type | Shard |
| Role | Ranged Striker |
| Weapon read | One aimed bow shot |
| Sprites | `Assets/Sprites/Combat/Chainlings/Skele/` |

Shard beats Node, Encrypted, Forked, Mined, Compressed, and Glitched. It loses to Block, Burned, Frozen, and Staked.

| Defender | Reaction on the winning hit |
|---|---|
| Mined, Encrypted, Glitched | Shattered on the foe. The follow-up shot is the one that matters. |
| Forked | Anchored on the foe. The volley becomes a duel. |
| Node | Leaked on the foe. For three turns, acting costs that side 1/10 HP. |
| Compressed | Dusted on the foe. |

Preferred moves are one accurate single-target projectile and a partition effect that keeps one foe isolated. Crit and accuracy belong here. Spread does not. Burned already owns damage over time. Block already owns barriers.

Lad is the melee Shard. Skele is the same type in the ranged lane, so a team that cannot walk Lad up still has a precision answer to Minefolk.

Boryo resists the bow at 0.8×, and Firewall then cuts the first projectile of the turn by another 30%. Staked's winning hit into Shard is Siphoned: the attacker heals 25% of damage dealt for three turns. The bow wants the exchange over before that leech is online.

## Hopoch

| | |
|---|---|
| Type | Staked |
| Role | Melee Striker |
| Weapon read | Naginata. Contact, one target. |
| Sprites | `Assets/Sprites/Combat/Chainlings/Froggy/` |

Staked beats and resists Forked, Compressed, and Shard. It loses to Gas, Burned, Frozen, Mined, and Bot.

| Defender | Reaction on the winning hit |
|---|---|
| Shard | Siphoned on self. Heal 25% of damage dealt for three turns. |
| Compressed | Drip on self. Heal 6% max HP at end of turn for three turns. |
| Forked | Anchored on the foe, then stay on that one target. |

Preferred moves are a leeching contact strike, a small evasion or endurance posture, and something that improves if the fight runs long. HODLed and Yield (slow AP gain) fit. Skip a one-turn delete.

Snek is Staked as support. Cofi Lootwak is Staked as an uncatchable wall. Froggy is the catchable duelist, not a third wall.

Minefolk is the direct check. That hit ramps the rifle with FOMO while Froggy is on the resisted side of the trade. Gas and Burned Isolate Froggy, so allies cannot heal or buff it. Frozen Slows it.

## How the four answer each other

| Species | Answers | Answered by |
|---|---|---|
| Blockwyrm | Cinislug, Forkrook, Crackett, Marrowhead | Minefolk, plus Deghoul, Humpacked, Kitsauto, Kreechank |
| Minefolk | Blockwyrm, Cofi Lootwak, Rattlestake, Gilarime, Keyper, Deghoul, Hopoch | Marrowhead, plus Validape, Cinislug, Forkrook, Kitsauto, Crackett |
| Marrowhead | Minefolk, Validape, Keyper, Forkrook, Humpacked, Kreechank | Blockwyrm, Hopoch, plus Cinislug and Gilarime |
| Hopoch | Marrowhead, Crackett, Forkrook, Humpacked | Minefolk, plus Deghoul, Cinislug, Gilarime, Kitsauto |

## Roster count

`SpeciesDatabase.asset` now lists 18 species. `Ghost.asset` is still a zero-stat placeholder and is not in that database.

| Group | Count | Who |
|---|---:|---|
| Registered species | 18 | The database list |
| Boss, outside the ladder | 1 | Cofi Lootwak. Uncatchable. Net stays 394. |
| Starters, outside the ladder | 3 | Validape, Gilarime, Kitsauto. Net 460. One step above Legendary, one per team in 2v2. |
| Wild bodies in the ladder | 14 | Six Common, four Uncommon, two Rare, one Epic, one Legendary |

```
18 registered − 1 boss − 3 starters = 14 wild bodies
```

Starters stay outside the tiers. They are granted, not rolled as wild encounters. Their shared budget is 460, one 20-point step above Legendary, because a 2v2 team can bring only one. Cofi Lootwak stays outside the tiers. Its total is a boss exception.

## Rarity tiers that exist today

`RarityTier` has six values, in order: Common, Uncommon, Rare, Epic, Legendary, Mythic.

No species definition has a rarity field. The enum's own comment says a tier affects availability and complexity, not raw power. Today the tiers label moves and visual skins. Validape's hockey skin is Rare as a skin label. Encounter zones roll species by a per-entry weight, not by a species tier. `EncounterRarityTierMultipliers` scales variant and mutation tables. It does not assign a species to a tier.

Using these tiers later for wild encounter bands and net point totals is a new use. This note only sets how many wild bodies each tier should hold.

The vending curve in `LEVER_BOARDS_v1.md` is the existing weight shape for those same six names: Common 45, Uncommon 30, Rare 17.5, Epic 5, Legendary 2, Mythic 0.5. Those are roll odds for vending, not species counts. A tier can hold one species and still be rolled at 2%.

## Slot counts

Twelve wild bodies across the six tiers:

| Tier | Species slots | Share of the 12 |
|---|---:|---:|
| Common | 5 | 42% |
| Uncommon | 3 | 25% |
| Rare | 2 | 17% |
| Epic | 1 | 8% |
| Legendary | 1 | 8% |
| Mythic | 0 | 0% |
| **Wild total** | **12** | **100%** |

Common is the layer zones actually fill. Five bodies is enough for a route to vary without one species being the whole grass table. Uncommon stays clearly smaller. Rare holds two so that tier's later point budget is not a single stat line. Epic and Legendary each hold one, so the follow-up point pass has two steps above Rare without inventing a seventh tier. Mythic stays empty. One body out of twelve would be 8% of the wild roster, and the vending weight for that name is half a percent. The enum value remains for moves and skins. A species slot can be opened there when the wild roster grows past this twelve.

## Applied bands

Net total is HP plus the five combat stats. The Champions note's useful band is about 80 points wide. Wild totals run from Cinislug at 352 to Blockwyrm at 440, an 88-point span. Starters sit at 460, twenty points above Legendary, and are capped to one per team in 2v2. Cofi Lootwak stays at 394.

| Tier | Species | Net |
|---|---|---:|
| Common | Cinislug | 352 |
| Common | Kreechank | 359 |
| Common | Deghoul, Keyper, Rattlestake | 360 |
| Common | Balloom | 356 |
| Uncommon | Forkrook, Humpacked, Marrowhead, Kodiax | 376 |
| Rare | Minefolk, Hopoch | 396 |
| Epic | Crackett | 416 |
| Legendary | Blockwyrm | 440 |
| Starter | Validape, Gilarime, Kitsauto | 460 |

Common keeps the stat lines it already had. Uncommon is +16 to +22 over that cluster. Rare, Epic, and Legendary step by about 20. Starters take the same size step past Legendary. Each starter was scaled from its old line, so the high stat and the low stat stayed the same stats.

Within a total, the shape still follows the type. Blockwyrm's points are HP 168, melee defense 86, ranged defense 72, sword 58, speed 24. Humpacked gave up the old HP sponge so it stays the Compressed soft wall. Crackett's melee defense moved from 28 to 46, so the self ratio is 86/46 instead of 82/28. Minefolk's melee attack is 48 so FOMO's melee-attack stage has a stat to spend. The rifle stays at 74.
