# Walls & Barriers — PvP Redesign (PROPOSALS)

> **Status: PROPOSALS ONLY.** Nothing in this document has been applied to gameplay code or data. All values, card text, renames, opcodes and AI rules below are suggestions for Cronus to accept, change or reject.
> Snapshot: local project as of 2026-10-20 (engine mirror `/workspace/balance/s10`, no gameplay code/data newer than 02:10 CT). Sim: `tempo_2v2_sim.py` + `walls_pvp_sim.py`, 4000 trials per cell, shared seeds across designs. Checker: `walls_pvp_check.py` (65 PASS / 0 FAIL).
> Constraints honoured: learn-pool rules unchanged (Ember Wall stays Cinislug's signature and stays the strictly highest rarity in its pool, Legendary); no self-damage costs; AP set case by case; typed > typeless; Block is the barrier type.

## 0. TL;DR

- **The problem is real.** Today Ember Wall (2 AP, full absorb of 3 hits over 2 turns, 1 AP drain per blocked hit, no recharge) lets a self-casting Cinislug refresh forever: in the STALL line **100% of battles hit the engine's 100-turn cap with no result** (30 Ember casts per battle). The engine has no recharge, consecutive-use penalty or breaker on it.
- **What competitive PvP does:** full nullification exists in Pokémon (Protect family), but it is always (1) **one turn only**, (2) **costs the user's whole action**, (3) **cannot be chained** (1/3 success on consecutive use), (4) has **priority** so it's a read, not a reaction, and (5) has **pierce/break counters**. Partial reduction (Reflect/Light Screen) is **split by category, ~1/3 in doubles, 5 turns, does not stack, and is breakable** (Brick Break, Psychic Fangs, Defog, Raging Bull). HP pools (Substitute, Overwatch barriers, Slay the Spire Block) are **capped** and/or **decay**.
- **Cronus's split (walls = melee, barriers = ranged, 1 hit each) — verdict:** keep the *category* idea, drop the *full-block-per-hit* part. As a full block it **does not fix the stall** (Ember refreshing 1 melee block per turn still averages a KO on turn 10.5) and it **overtunes cheap walls** (Guard Posture + Blockade/Ice Wall stop Deep Dig 100% and Hammer Sync stalls 8%). As a percent split it makes each wall a dead card against the other category (Compound Guard vs Cryo Ray: 8% → 56% KO by T2).
- **Recommendation "R" (ranked #1):** three keywords, each with one job.
  - **PROTECT** — only Ember Wall: PRIOR, this turn only, blocks every direct hit on one ally or self, 1-turn recharge (deterministic instead of RNG), the first blocked melee move drains 1 AP.
  - **SCREEN (Melee / Ranged / All)** — every other "Guard/Wall": % reduction, one category, **one Screen per category per Chainling, never multiplied**, breakable.
  - **SHIELD** — Vapor Barrier and Lattice: absorb pool = % of the target's MaxHP, overflow goes through, halves at end of turn.
  - R kills the stall (0% capped), is the **only design that answers turn-1 bursts** (Seam Cut T1 KO 91% → 0%, Partition Shot 64% → 0%) and a patient attacker who baits the Protect still gets the KO a turn later (Cryo Ray: 0% → 56% by T3). It also stops Screens from multiplying into heavy-proof walls (GP + Blockade vs Deep Dig: 64% → 95% KO by T2, i.e. back to "Screens soften, Protect stops").

## 1. Research (Part 1)

All facts below were read from the cited pages on 2026-10-20. Where a page could not confirm something, that is said explicitly.

### 1.1 Pokémon — the Protect family (full block)

Sources: [Protect](https://bulbapedia.bulbagarden.net/wiki/Protect_(move)), [Protection (mechanic page)](https://bulbapedia.bulbagarden.net/wiki/Protection), [Detect](https://bulbapedia.bulbagarden.net/wiki/Detect_(move)), [King's Shield](https://bulbapedia.bulbagarden.net/wiki/King%27s_Shield_(move)), [Spiky Shield](https://bulbapedia.bulbagarden.net/wiki/Spiky_Shield_(move)), [Baneful Bunker](https://bulbapedia.bulbagarden.net/wiki/Baneful_Bunker_(move)), [Silk Trap](https://bulbapedia.bulbagarden.net/wiki/Silk_Trap_(move)), [Burning Bulwark](https://bulbapedia.bulbagarden.net/wiki/Burning_Bulwark_(move)), [Obstruct](https://bulbapedia.bulbagarden.net/wiki/Obstruct_(move)).

| Move | Priority | Contact punishment | Notes |
|---|---|---|---|
| Protect / Detect | +4 | none | baseline full block for the turn |
| King's Shield | +4 | attacker Attack −1 (was −2 before Gen VIII) | Aegislash's signature; blocks damaging moves only. A contact multistrike move only triggers the drop once |
| Spiky Shield | +4 | attacker loses 1/8 max HP | |
| Baneful Bunker | +4 | attacker poisoned | |
| Silk Trap | +4 | attacker Speed −1 | Spidops's **signature** |
| Burning Bulwark | +4 | attacker burned | Gouging Fire's **signature** — a Fire-type signature Protect with a contact burn, i.e. the closest real precedent for Ember Wall |
| Obstruct | +4 | attacker Defense −2 | |

Shared rules (Protection page):
- "a growing chance of failure if used consecutively"; success is **1/3^T** for T consecutive successful uses (Gen VI+). Champions' in-game text for Baneful Bunker: *"With each consecutive use, this move's chance of success becomes 1/3 of what it was before."*
- The move **fails if the user moves last** in the turn.
- **Moves that break protection** (e.g. Feint) do full damage and remove it. A few moves (Hyper Drill, Mighty Cleave) hit through at full power. The abilities **Unseen Fist** / Piercing Drill let contact moves bypass protection, but **"as of Champions, only deal 1/4 of their original damage."**
- Status and end-of-turn damage (weather, status conditions) are not blocked (Spiky Shield page).

Team-wide variants:
- [Wide Guard](https://bulbapedia.bulbagarden.net/wiki/Wide_Guard_(move)) (+3) blocks multi-target moves for the whole side.
- [Quick Guard](https://bulbapedia.bulbagarden.net/wiki/Quick_Guard_(move)) (+3) blocks priority moves for the side.
- Both are **narrow**: they block a class of moves, not everything.

### 1.2 Pokémon — single-hit "free" blocks (and their cost)

- [Substitute](https://bulbapedia.bulbagarden.net/wiki/Substitute_(move)) costs 1/4 of the user's max HP (1/8 in Legends: Z-A). **Ruled out for us:** no self-damage.
- [Disguise](https://bulbapedia.bulbagarden.net/wiki/Disguise_(Ability)) (Mimikyu) blocks one hit. Since Gen VIII the bust costs 1/8 max HP.
- [Ice Face](https://bulbapedia.bulbagarden.net/wiki/Ice_Face_(Ability)) (Eiscue): *"When … hit by a **physical** move, it takes no damage"*. It restores in hail/snow. **This is the real precedent for Cronus's melee-only block**: a one-hit, category-filtered nullify, gated to one species.

### 1.3 Pokémon — screens (partial, category-split)

Sources: [Reflect](https://bulbapedia.bulbagarden.net/wiki/Reflect_(move)), [Light Screen](https://bulbapedia.bulbagarden.net/wiki/Light_Screen_(move)), [Aurora Veil](https://bulbapedia.bulbagarden.net/wiki/Aurora_Veil_(move)), [Light Clay](https://bulbapedia.bulbagarden.net/wiki/Light_Clay), [Brick Break](https://bulbapedia.bulbagarden.net/wiki/Brick_Break_(move)), [Psychic Fangs](https://bulbapedia.bulbagarden.net/wiki/Psychic_Fangs_(move)).

- **Category split.** Reflect cuts physical damage; Light Screen cuts special damage. Both last **5 turns** (8 with Light Clay) and protect the whole side.
- **Size.** Half in singles; in doubles/triples *"physical damage is only reduced by a third"* (multiplier 2732/4096 from Gen VI).
- **Aurora Veil** covers both categories, but only in hail/snow. It *"can be active at the same time as Reflect and Light Screen, [but] the damage reduction effects do not stack."*
- **Breakers.** Brick Break, Psychic Fangs, Raging Bull and Defog remove screens. Champions' Brick Break text: *"Removes the Physical and Special Damage Reduction effects from the opponents' field of play."*

### 1.4 Did Pokémon Champions change any of this?

**Confirmed:**
- **PP.** Protect-family PP was cut. King's Shield and Baneful Bunker have 8 PP instead of 12 (their Bulbapedia "Pokémon Champions" notes), and Spiky Shield has the same kind of note. Protect itself is 8 PP per [Game8](https://game8.co/games/Pokemon-Champions/archives/592945), [ptcgphub](https://ptcgphub.com/champions/moves/protect/) and [GameRant](https://gamerant.com/pokemon-champions-protect-nerf/). **Discrepancy:** [Polygon](https://www.polygon.com/pokemon-champions-sleep-paralysis-nerf-changes-freeze-protect-pp/) reports a different maximum (10). Treat 8 as the majority figure.
- **Consecutive use.** The 1/3 chain is kept (Champions move text above).
- **Unseen Fist.** It now lets only 1/4 of the damage through (Protection page).
- **Light Clay** exists in Champions (Frontier Shop, 1,000 VP).

**Not confirmable:** I found no Champions-specific change to Reflect, Light Screen or Aurora Veil, or to Silk Trap, Burning Bulwark or Obstruct. Their Bulbapedia "In Champions" sections only list learnsets. Treat these as unchanged but unverified.

### 1.5 Cassette Beasts (the original inspiration)

[Elemental Wall](https://wiki.cassettebeasts.com/wiki/Elemental_Wall) (Typeless, 2 AP):
- *"Sacrifice 20% of HP to create a wall that will absorb up to 3 hits for up to 3 turns."*

[Wall status](https://wiki.cassettebeasts.com/wiki/Beast_Wall_(status_effect)):
- *"Walls absorb a certain number of incoming hits. **Type-advantaged attacks immediately break walls.**"*

So Cassette Beasts' walls have **two costs that Ember Wall lacks**: an HP sacrifice, and a hard counter in the type chart. Its design also assumes a PvE, fusion-heavy game where walls are a core loop. Ember Wall copied the 3-hit absorb without either brake, and then added an AP drain on top.

### 1.6 Other games

**[Hearthstone — Divine Shield](https://hearthstone.wiki.gg/wiki/Divine_Shield)** ignores the next damage instance of any size.
- Counterplay is to *"use a weak ability to remove the shield before targeting the minion with a more substantial attack."*
- Multiple Divine Shields **do not stack**.
- **Lesson:** a full one-hit nullify is fair only because cheap pings pop it.

**Overwatch — barriers** are HP pools that are tuned in health numbers.
- Reinhardt's Barrier Field went 1200 → 1400 → 1600 → 1500 → 1100 across patches ([patch tracker](https://patchdelta.gg/overwatch/reinhardt); see also [Blizzard patch notes 2023-07](https://overwatch.blizzard.com/en-us/news/patch-notes/live/2023/07/)).
- **Lesson:** an HP-capped barrier is the tunable knob; "absorb N hits" is not.

**[Slay the Spire — Block](https://slay-the-spire.fandom.com/wiki/Block)** is an absorb pool.
- *"All block is typically removed at the start of your turn"*. It only carries over with Barricade, Blur or Calipers.
- Poison and "lose HP" effects ignore it.
- **Lesson:** decay is what keeps pools from snowballing, and DoT is the natural counter.

**[Darkest Dungeon — Guard](https://darkestdungeon.wiki.gg/wiki/Guard_(Darkest_Dungeon))** redirects direct attacks to the guardian. It lasts about 3 turns, and re-use overrides it.
- **Counters:** AoE that also hits the guardian, stunning the guardian, and Shieldbreaker's Puncture, which *"bypasses and removes a guard … and applies a debuff that prevents this enemy from being guarded for 2 rounds."*
- Existing DoT keeps ticking.
- **Lesson:** the anti-re-guard debuff is the same idea as Protect's consecutive-use penalty.

*(Temtem and Paladins were not researched further; the four games above plus Pokémon and Cassette Beasts already cover full block, percent screen, HP pool, decay and redirect.)*

## 2. Principles (Part 2)

1. **Full nullify is allowed only as a read.** Every competitive version has these properties:
   - it lasts one turn
   - it costs the user's whole action
   - it has priority, so it is chosen blind
   - it cannot be chained (1/3^T, or a re-guard lockout)
   - it has a pierce or break counter
   
   Multi-turn or multi-hit full absorbs exist only in PvE-leaning designs, and even there they come with an HP cost and a type counter (Cassette Beasts).
2. **Partial reduction is split by category and never multiplies.** Reflect is physical and Light Screen is special. Aurora Veil covers both but only under a condition, and it does not stack with them. In doubles it is about 1/3 for about 5 turns.
3. **Pools are capped by HP and decay** (Substitute 1/4 HP, Overwatch barrier HP, Slay the Spire's turn-start reset).
4. **Every defensive layer has a named breaker**: Brick Break/Defog for screens, Feint/Unseen Fist for Protect, pings for Divine Shield, Puncture/AoE/stun for Guard.
5. **DoT and status always go through.**
6. **Signature defensive moves are a normal pattern** (King's Shield, Silk Trap, Burning Bulwark). Making Ember Wall *the* Protect of the game keeps its Legendary-signature identity.

## 3. Engine facts that shape the options (checked by `walls_pvp_check.py`)

- **Mitigation honours a contact filter, but barriers don't.**
  - Percent mitigation honours a contact filter: `ChainlingInstance.cs:531/573/587` (`AppliesToContact`).
  - Barriers have a `contactFilter` field (`ChainlingInstance.cs:1026`), but `ApplyBarrierAbsorption` (`ChainlingInstance.cs:874–925`) never reads it.
- **Only one effect can carry a category.** `MitigationWindow` sources are always added with filter `Any` (`ChainlingInstance.cs:453`), and the importer parses no contact filter for `MitigationWindow` (`Combat2DataImporter.cs:1696`). Only `IncomingDamageMitigation` parses one (`Combat2DataImporter.cs:1815`).
  - So **any category split needs a small importer/runtime change**.
- **Melee/ranged map cleanly to contact.** Every `DamagingMelee` move is `Contact` and every `DamagingRanged` move is `Projectile`.
- **Barriers are exclusive** (`ChainlingInstance.cs:850`). Battles cap at **100 turns with no result** (`CombatBattleController.cs:114`), and SingleAlly falls back to self (`CombatResolver.cs:2701`).
- **No barrage or multi-strike move has a breaker opcode today**: Pellet Storm, Ricochet Storm, Rapid Pummel and Breach Rush all lack one. Core Breach is still only a walls-doc PROPOSAL.

## 4. Options tested (Part 3)

| Design | What it models |
|---|---|
| `engine` | current data |
| `ember_rc3` | current Ember + a 2-turn recharge (earlier R-EW1 proposal) |
| `a_split_full` | **Cronus's split as full blocks.** Every Wall/Guard/Blockade = full block of 1 melee hit; Barrier Wall/Vapor = full block of 1 ranged hit; Ember also drains 1 |
| `a_split_pct` | Cronus's split keeping the percents. Percent walls keep their %, but become 1-hit and category-filtered; Ember = 1 melee full block; Vapor = 1 ranged full block |
| `b_protect` | **Pokémon-style.** Ember = Protect (PRIOR, this turn, all hits, drain 1, consecutive use 1/3^n); Vapor = ranged Protect. Percent walls become category screens (Blockade 1/3 melee 3t; Barrier Wall 1/3 ranged 3t; Compound .25 melee; Ash .25 ranged; Ice .25 ranged 1t; GP .25 any 1 hit); one percent wall at a time |
| `b_protect_rc` | as `b_protect`, with a deterministic 1-turn recharge instead of the 1/3 RNG |
| `c_pool` | **HP-scaled absorb.** Ember = pool of 35% target MaxHP, 2 turns, halves at end of turn, drain 1 per absorbing hit; Vapor = 20% pool |
| **`R`** | **recommended package (section 5)** |
| `R+bait` | R vs an attacker who reads an available Protect: holds the heavy one turn (light hit instead), then fires |
| `R0` | R without the AP drain (isolates the drain) |
| `R3` | R with Ember Wall at 3 AP (cost sensitivity) |

Lines are real learn-pool kits (4 of 5 moves), with random IVs and smart targeting.
- "none" is the same line with the defender never walling.
- **Stall rate** = % of battles that reach the engine's 100-turn cap.
- The "win" column in capped cells is the sim's HP tie-break, so ignore it there.

Cell legend:

- **STALL**: STALL Cinislug self-Ember vs 2 single-hit attackers
- **MULTI**: MULTI Blade Flurry vs self-Ember Cinislug
- **HS**: HS Hammer Sync -> Deghoul, ally Ember
- **HS+GP**: HS+GP Hammer Sync -> Deghoul, GP + ally Ember
- **CR**: CR Cryo Ray (ranged) -> Crackett, ally Ember
- **DD**: DD Deep Dig -> Keyper, ally Ember
- **SC**: SC Seam Cut T1 -> Deghoul, ally Ember
- **PS**: PS Partition Shot (ranged) T1 -> Deghoul, ally Ember
- **PF**: PF partner focus (Seam Cut + HS) -> Deghoul, ally Ember
- **HS-B**: HS-B Hammer Sync -> Deghoul, GP + ally Blockade
- **DD-B**: DD-B Deep Dig -> Keyper, GP + ally Blockade
- **DD-I**: DD-I Deep Dig -> Keyper, GP + ally Ice Wall
- **NCR**: NCR Cryo Ray (ranged) -> Rattlestake, self Compound Guard
- **NHF**: NHF Hard Fork+FS -> Rattlestake, Compound Guard + Blockade
- **NHFh**: NHFh Hard Fork+FS -> Humpacked, self Barrier Wall

**Target KO by end of T1 / T2 / T3 (%)**

| cell | none | engine | ember_rc3 | a_split_full | a_split_pct | b_protect | b_protect_rc | c_pool | R | R+bait | R0 | R3 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| STALL | 0/95/95 | 0/0/0 | 0/0/5 | 0/0/0 | 0/0/0 | 0/0/6 | 0/0/5 | 0/95/95 | 0/0/5 | 0/0/5 | 0/0/5 | 0/95/95 |
| MULTI | 0/0/5 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 |
| HS | 0/32/32 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/2/2 | 0/0/0 | 0/0/18 | 0/0/0 | 0/34/34 |
| HS+GP | 0/34/34 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/0 | 0/0/4 | 0/0/0 | 0/11/11 |
| CR | 0/93/93 | 0/0/0 | 0/0/0 | 0/92/92 | 0/92/92 | 0/0/0 | 0/0/0 | 0/50/51 | 0/0/0 | 0/0/56 | 0/0/0 | 0/94/94 |
| DD | 0/94/94 | 0/0/0 | 0/0/1 | 0/0/0 | 0/0/0 | 0/0/1 | 0/0/1 | 0/89/89 | 0/0/1 | 0/0/2 | 0/0/1 | 0/94/95 |
| SC | 92/100/100 | 91/91/96 | 92/92/100 | 91/91/96 | 91/91/96 | 0/92/92 | 0/92/92 | 92/92/96 | 0/92/92 | 0/92/92 | 0/92/92 | 92/92/99 |
| PS | 65/75/99 | 64/64/66 | 64/64/90 | 64/64/66 | 64/64/66 | 0/19/19 | 0/20/20 | 64/64/66 | 0/21/21 | 0/21/21 | 0/21/21 | 65/65/91 |
| PF | 92/100/100 | 92/92/96 | 92/92/100 | 92/92/96 | 92/92/96 | 0/96/96 | 0/96/96 | 92/95/95 | 0/96/96 | 0/97/97 | 0/96/96 | 91/91/99 |
| HS-B | 0/52/52 | 0/6/6 | 0/6/6 | 0/0/0 | 0/6/6 | 0/7/7 | 0/7/7 | 0/6/6 | 0/16/16 | 0/16/16 | 0/16/16 | 0/16/16 |
| DD-B | 0/94/95 | 0/64/64 | 0/64/64 | 0/0/0 | 0/64/64 | 0/63/63 | 0/63/63 | 0/64/64 | 0/95/95 | 0/95/95 | 0/95/95 | 0/95/95 |
| DD-I | 0/94/95 | 0/64/64 | 0/64/64 | 0/0/0 | 0/64/64 | 0/64/64 | 0/64/64 | 0/64/64 | 0/63/63 | 0/63/63 | 0/63/63 | 0/63/63 |
| NCR | 0/57/57 | 0/8/8 | 0/8/8 | 0/56/56 | 0/58/58 | 0/57/57 | 0/57/57 | 0/8/8 | 0/8/8 | 0/8/8 | 0/8/8 | 0/8/8 |
| NHF | 0/89/95 | 0/14/21 | 0/14/21 | 0/13/13 | 0/14/18 | 0/13/18 | 0/13/18 | 0/14/21 | 0/1/11 | 0/1/11 | 0/1/11 | 0/1/11 |
| NHFh | 0/94/98 | 0/91/95 | 0/91/95 | 0/96/98 | 0/95/98 | 0/95/98 | 0/95/98 | 0/91/95 | 0/95/98 | 0/95/98 | 0/95/98 | 0/95/98 |

**Mean target-KO turn (capped at 12) and stall rate (battle hit maxTurns 100, %)**

| cell | none | engine | ember_rc3 | a_split_full | a_split_pct | b_protect | b_protect_rc | c_pool | R | R+bait | R0 | R3 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| STALL | 2.22 | 12.00 **stall 100%** | 4.82 | 10.46 | 10.46 | 4.71 | 4.77 | 2.19 | 4.77 | 4.71 | 4.77 | 2.46 |
| MULTI | 4.49 | 8.54 **stall 1%** | 7.69 | 7.57 | 7.57 | 9.86 | 9.83 | 7.52 | 9.83 | 9.83 | 9.83 | 7.04 |
| HS | 4.76 | 4.87 | 4.95 | 4.87 | 4.87 | 5.69 | 5.61 | 4.85 | 5.61 | 4.98 | 5.61 | 4.53 |
| HS+GP | 4.77 | 6.72 | 6.66 | 7.56 | 6.72 | 6.49 | 6.49 | 6.71 | 6.43 | 6.20 | 6.43 | 5.35 |
| CR | 2.21 | 4.93 | 4.93 | 2.24 | 2.24 | 4.95 | 4.92 | 3.45 | 5.09 | 3.61 | 5.09 | 2.19 |
| DD | 2.17 | 4.47 | 4.41 | 4.47 | 4.47 | 4.93 | 4.92 | 2.35 | 4.92 | 4.90 | 4.92 | 2.16 |
| SC | 1.09 | 1.21 | 1.17 | 1.21 | 1.21 | 2.18 | 2.17 | 1.21 | 2.17 | 2.17 | 2.17 | 1.17 |
| PS | 1.61 | 2.08 | 1.83 | 2.08 | 2.08 | 3.91 | 3.87 | 2.09 | 3.87 | 3.87 | 3.87 | 1.87 |
| PF | 1.08 | 1.22 | 1.17 | 1.22 | 1.22 | 2.09 | 2.08 | 1.21 | 2.08 | 2.07 | 2.08 | 1.18 |
| HS-B | 4.23 | 6.33 | 6.33 | 8.40 **stall 8%** | 6.33 | 6.18 | 6.18 | 6.33 | 4.05 | 4.05 | 4.05 | 4.05 |
| DD-B | 2.14 | 2.90 | 2.90 | 6.91 | 2.89 | 2.85 | 2.85 | 2.90 | 2.13 | 2.13 | 2.13 | 2.13 |
| DD-I | 2.22 | 2.86 | 2.86 | 9.78 | 2.86 | 2.78 | 2.78 | 2.86 | 2.84 | 2.84 | 2.84 | 2.84 |
| NCR | 2.92 | 3.99 | 3.99 | 2.96 | 2.92 | 2.93 | 2.93 | 3.99 | 3.99 | 3.99 | 3.99 | 3.99 |
| NHF | 2.24 | 5.71 | 5.71 | 10.67 | 6.86 | 6.09 | 6.09 | 5.71 | 6.56 | 6.56 | 6.56 | 6.56 |
| NHFh | 2.09 | 2.19 | 2.19 | 2.08 | 2.09 | 2.10 | 2.10 | 2.19 | 2.10 | 2.10 | 2.10 | 2.10 |

**Attacking-team win % / defender wall casts per battle**

| cell | none | engine | ember_rc3 | a_split_full | a_split_pct | b_protect | b_protect_rc | c_pool | R | R+bait | R0 | R3 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| STALL | 23 / 0.0 | 100 / 30.0 | 83 / 1.2 | 100 / 9.9 | 100 / 9.9 | 5 / 1.0 | 5 / 1.0 | 35 / 1.2 | 5 / 1.0 | 6 / 1.1 | 5 / 1.0 | 23 / 0.1 |
| MULTI | 31 / 0.0 | 100 / 7.8 | 69 / 2.2 | 100 / 6.6 | 100 / 6.6 | 46 / 3.8 | 45 / 3.7 | 100 / 6.5 | 45 / 3.7 | 45 / 3.7 | 45 / 3.7 | 64 / 2.4 |
| HS | 65 / 0.0 | 93 / 1.2 | 88 / 1.0 | 93 / 1.2 | 93 / 1.2 | 63 / 1.0 | 64 / 1.0 | 92 / 1.2 | 64 / 1.0 | 72 / 1.0 | 64 / 1.0 | 74 / 0.0 |
| HS+GP | 65 / 0.0 | 96 / 4.4 | 97 / 4.3 | 100 / 8.1 | 96 / 4.4 | 96 / 4.2 | 96 / 4.2 | 97 / 4.4 | 96 / 4.2 | 97 / 4.3 | 96 / 4.2 | 97 / 2.7 |
| CR | 99 / 0.0 | 100 / 1.0 | 100 / 1.0 | 100 / 1.2 | 100 / 1.2 | 97 / 1.0 | 98 / 1.0 | 100 / 1.1 | 97 / 1.0 | 98 / 1.0 | 97 / 1.0 | 100 / 0.0 |
| DD | 97 / 0.0 | 97 / 1.4 | 96 / 1.0 | 97 / 1.4 | 97 / 1.4 | 78 / 1.3 | 79 / 1.3 | 99 / 2.1 | 79 / 1.3 | 79 / 1.3 | 79 / 1.3 | 99 / 0.0 |
| SC | 100 / 0.0 | 100 / 1.0 | 100 / 1.0 | 100 / 1.0 | 100 / 1.0 | 99 / 1.1 | 99 / 1.1 | 100 / 1.0 | 99 / 1.1 | 99 / 1.1 | 99 / 1.1 | 100 / 0.1 |
| PS | 99 / 0.0 | 100 / 1.3 | 100 / 1.0 | 100 / 1.3 | 100 / 1.3 | 79 / 1.9 | 79 / 1.9 | 100 / 1.3 | 79 / 1.9 | 79 / 1.9 | 79 / 1.9 | 99 / 0.4 |
| PF | 99 / 0.0 | 100 / 1.1 | 100 / 1.0 | 100 / 1.1 | 100 / 1.1 | 95 / 1.1 | 95 / 1.0 | 100 / 1.1 | 95 / 1.0 | 95 / 1.0 | 95 / 1.0 | 99 / 0.1 |
| HS-B | 14 / 0.0 | 51 / 5.7 | 51 / 5.7 | 94 / 10.9 | 51 / 5.7 | 34 / 4.6 | 34 / 4.6 | 51 / 5.7 | 84 / 5.6 | 84 / 5.6 | 84 / 5.6 | 84 / 5.6 |
| DD-B | 22 / 0.0 | 29 / 3.3 | 29 / 3.3 | 100 / 8.2 | 29 / 3.3 | 15 / 2.8 | 15 / 2.8 | 29 / 3.3 | 42 / 3.1 | 42 / 3.1 | 42 / 3.1 | 42 / 3.1 |
| DD-I | 64 / 0.0 | 87 / 1.8 | 87 / 1.8 | 100 / 10.1 | 87 / 1.8 | 88 / 1.7 | 88 / 1.7 | 87 / 1.8 | 97 / 4.2 | 97 / 4.2 | 97 / 4.2 | 97 / 4.2 |
| NCR | 99 / 0.0 | 99 / 1.9 | 99 / 1.9 | 100 / 1.5 | 100 / 1.4 | 100 / 1.0 | 100 / 1.0 | 99 / 1.9 | 99 / 1.1 | 99 / 1.1 | 99 / 1.1 | 99 / 1.1 |
| NHF | 99 / 0.0 | 100 / 6.6 | 100 / 6.6 | 100 / 14.5 | 100 / 8.8 | 100 / 5.2 | 100 / 5.2 | 100 / 6.6 | 99 / 5.4 | 99 / 5.4 | 99 / 5.4 | 99 / 5.4 |
| NHFh | 61 / 0.0 | 84 / 1.1 | 84 / 1.1 | 85 / 1.1 | 84 / 1.1 | 84 / 1.0 | 84 / 1.0 | 84 / 1.1 | 84 / 1.0 | 84 / 1.0 | 84 / 1.0 | 84 / 1.0 |

### 4.1 Reading the numbers

**Stall** (Ember Wall self-refresh):

| Design | Stall rate | Mean KO turn |
|---|---|---|
| engine | **100%** | – |
| a_split (both) | under 1% | 10.5 (10 Ember casts per battle). **Not fixed.** |
| ember_rc3 | 0% | 4.8 |
| b / R | 0% | 4.8 |
| c_pool | 0% | 2.2 (Ember no longer matters) |

**Turn-1 bursts** (Seam Cut, Partition Shot, partner focus):
- Today **nothing** answers them: Ember is not PRIOR, so the target dies before the wall lands (T1 KO 91–92%; 64% for Partition Shot).
- **Only Protect designs** (b/R) answer them: T1 KO 0%. Seam Cut / partner focus still KO on T2 (92–96%), so Protect buys exactly one turn.
- Partition Shot drops to 21% by T2 (79% attacking-team win, down from 100%).

**Heavy-stop** (Hammer Sync, Cryo Ray, Deep Dig into an ally Ember):
- engine, ember_rc3 and R all hold the heavy (0–1% KO by T3).
- **c_pool loses it**: Deep Dig 89% by T2; Cryo Ray 50%.
- **a_split loses it vs ranged**: Cryo Ray 92% by T2, because a melee-only Ember doesn't see it.

**Protect is not a stall under a patient attacker** (`R+bait`):
- Cryo Ray KO by T3 goes 0% → **56%**.
- Hammer Sync: 0% → 18%. Hammer Sync can't always afford the bait turn and still bank the heavy, which is an AP artefact of the line.

**Screens stop multiplying under R:**
- GP + Blockade vs Deep Dig: 64% → **95%** KO by T2 (no-wall 94%).
- GP + Blockade vs Hammer Sync: 6% → 16%.
- This is intended: **Screens soften, Protect stops.** In the engine, two percent walls multiply (0.75 × 0.75) into a heavy-proof wall.

**Overtuning check on Cronus's full-block split:**
- `a_split_full` turns 1-AP Guard Posture into a repeatable full block.
- Deep Dig KO by T3 is **0%** with GP + Blockade or GP + Ice Wall.
- Hammer Sync vs GP + Blockade: **8% stall**, mean KO turn 8.4.
- Defender damage collapses (HS+GP: 80 per battle vs 277 in the engine).

**Category-only percents:**
- `a_split_pct` and `b` make Compound Guard melee-only, so ranged Cryo Ray goes **8% → 56–58%** KO by T2: the wall becomes a dead card.
- R fixes this by giving Compound Guard a **Ranged** Screen plus its existing MeleeDef +1, which keeps it at **8%**.

**AP drain is cosmetic here.** R0 (no drain) is identical to R on every line, because Protect is cast about once per battle.

**3 AP is too expensive for Protect.** In R3, Ember Wall is almost never castable on time (0.0–0.1 casts per battle on HS/CR/DD), and every line reverts to "none". **Keep 2 AP.**

**Attacking-team win rate:**
- Today Ember is a tempo *sink* for its own team. HS attacking-team win: no walls 65% → engine 93%, because Cinislug spends its turns refreshing.
- Under R the same line is 64%. Protect is spent once, at the right moment, and Cinislug fights the rest of the time.
- STALL is the outlier: R gives the attacking team 5% vs 23% with no walls. One blocked 4-AP Hard Fork is a large tempo swing against a naive attacker. This is the place to watch in playtests (see blind spots).

### 4.2 Ranking

1. **R — Protect + category Screens + HP Shields.** The only option that removes the stall, answers T1 bursts, keeps the heavy-stop, stops Screen multiplication and gives the attacker a real read (bait). It costs a small amount of code (section 8).
2. **b_protect_rc / b_protect.** Same Protect numbers as R. Their screens are worse: Compound Guard goes dead vs ranged, and Blockade stays 3 AP. Prefer the deterministic recharge over 1/3 RNG: it is readable, AI-friendly and testable. The numbers are identical.
3. **ember_rc3** (add a recharge, nothing else). The cheapest patch: it kills the stall with a data-only change. But it keeps a multi-turn full nullify (the unfairness Cronus flagged), has no T1 answer, and leaves Screen multiplication alone. **Good as an interim hotfix.**
4. **c_pool.** No stall, but Ember stops being worth a Legendary slot (it loses the heavy-stop).
5. **a_split** (Cronus's split as written). The full-block version does not fix the stall and overtunes cheap walls; the percent version creates dead cards. **Keep its category idea inside R's Screens.**

## 5. Recommended rules (R) — PROPOSAL

### 5.1 Keywords (shown as a chip on the card)

- **PROTECT**
  - [PRIOR]. Until end of turn, every direct hit on the target is blocked, including all hits of a multi-hit move and their on-hit riders (chemistry, stage drops).
  - DoT and end-of-turn effects still apply.
  - Recharge 1 turn, so it is never usable two turns in a row.
- **SCREEN (Melee | Ranged | All) X% for N turns**
  - Direct hits of that category deal X% less.
  - A Chainling holds **at most one Screen per category**; casting a new one replaces it. An *All* Screen replaces both.
  - A hit is reduced by **one** Screen at most. Screens never multiply with each other.
  - Stat stages still stack with Screens (different system).
- **SHIELD X% MaxHP for N turns**
  - Absorbs damage up to X% of the target's max HP; overflow goes through.
  - Remaining Shield **halves at end of each turn**.
  - One Shield per Chainling (existing exclusive rule).
  - Order of application: Protect → Screen % → Shield → HP.

### 5.2 Naming rule

The research supports **no** universal convention:
- Pokémon calls full blocks Protect/Shield/Guard/Bunker/Bulwark.
- Cassette Beasts calls hit-count absorbs "Wall".
- Overwatch calls HP pools "Barrier".

Proposal: **the keyword chip is canonical, and names follow a soft rule.**
- "…Guard", "…Wall", "Blockade" = **Screen**
- "…Barrier", "…Shield" = **Shield**
- **Protect** is a keyword only.
- Ember Wall keeps its name as the legacy signature (like King's Shield being a Protect).

Two renames remove the clashes:
- Barrier Wall → **Buffer Wall**
- Lattice Guard → **Lattice Shield**

### 5.3 Swap / KO

- Screens and Shields belong to the Chainling. They **drop when it swaps out or is KO'd**. Today walls persist on the bench, which enabled the "pre-load the bench" trick found in the tempo extension.
- Protect ends at end of turn, or when its target leaves the field.
- Recharge persists on the bench, as recharge does today.

### 5.4 Breakers

| Layer | Breaker (PROPOSAL) | Precedent |
|---|---|---|
| Screen | **Core Breach** (walls-doc proposal: Mined, Uncommon, melee P32, 3 AP): change from "ignore mitigation" to **"removes the target's Screens, then hits"**. **Breach Rush** (`guardbreaker_combo`, 2 AP melee multi): first hit removes the target's Screens | Brick Break / Psychic Fangs |
| Shield | **Pellet Storm** (`baseline_barrage`) and **Ricochet Storm** (`refined_barrage`): *Shatter*, i.e. each hit deals double damage to a Shield. Multi-hits (Rapid Pummel, Blade Flurry, Cinder Burst) already chip pools hit by hit | Divine Shield pings; Overwatch barrier HP |
| Protect | **Ignore Walls (4.8-a) / Wallbreaker (4.3-b)**: vs Protect deal **25%** damage through and do not remove it. No full Feint in the first pass | Champions Unseen Fist (1/4) |
| All | DoT, Overheat and status ticks ignore all three layers (current behaviour) | Slay the Spire Poison; Darkest Dungeon DoT |

### 5.5 AI notes

**Defender:**
- Cast Protect only when an *affordable* hit this turn threatens at least about 60% of the target's current HP, or a banked heavy is live, and the move is off recharge.
- Pick the slot that is actually threatened.
- Do **not** protect on every legal turn. Mix in about 30% "attack instead" when the threat is not lethal, so the bait read stays live.

**Attacker:**
- If the target's Protect is ready and the heavy is the obvious play, either retarget the partner, or use a light hit/setup and fire on the recharge turn (the `R+bait` model).
- Never spend a multi-hit or a heavy into a Protect cast this turn when visible. Protect is PRIOR, so it resolves before the attacker acts; this applies to replay/telegraph modes only.

**Screens:**
- Use a category-aware damage estimate.
- Against a Melee Screen, prefer ranged moves (and vice versa).
- Use a Screen breaker when 2 or more Screen turns remain.
- Don't refresh a Screen with more than 1 turn left.

**Shields:**
- Open with multi-hit, AoE or barrage (Shatter), then the heavy.

## 6. Per-move rewrites (Part 4) — PROPOSALS

Rarity is unchanged for all nine moves, and pools are untouched. AP changes are marked with ▲ (up) or ▼ (down).

| Move | Type / Rarity | AP | Target | Keyword & values | Card text (proposal) |
|---|---|---|---|---|---|
| **Ember Wall** | Burned / **Legendary** (Cinislug signature) | 2 | Ally or self | **PROTECT**, PRIOR, recharge 1 turn, first blocked melee move drains 1 AP | *"[PRIOR] Protect an ally or yourself this turn: block every direct hit on them. The first melee move blocked drains 1 AP from its attacker. Recharge 1."* |
| **Vapor Barrier** | Gas / Uncommon | 2 | Ally or self | **SHIELD** 25% MaxHP, 2 turns, halves at end of turn | *"Shield an ally or yourself for 25% of their max HP for 2 turns. The Shield halves at end of each turn."* |
| **Lattice Shield** (was Lattice Guard) | Shard / Uncommon | ▲2 (was 1) | Ally or self | **SHIELD** 20% MaxHP, 2 turns, halves at end of turn; reflects 20% of damage absorbed | *"Shield an ally or yourself for 20% of their max HP for 2 turns. Reflect 20% of the damage it absorbs."* |
| **Guard Posture** | None / Common | 1 | Self | **SCREEN (All)** 20%, next hit only, 2 turns | *"Screen yourself: your next hit taken within 2 turns deals 20% less."* |
| **Buffer Wall** (was Barrier Wall) | None / Uncommon | ▼2 (was 3) | Ally or self | **SCREEN (Ranged)** 25%, 3 turns | *"Screen an ally or yourself: ranged hits deal 25% less for 3 turns."* |
| **Blockade** | Block / Uncommon | ▼2 (was 3) | Ally or self | **SCREEN (Melee)** 33%, 3 turns | *"Screen an ally or yourself: melee hits deal 33% less for 3 turns."* |
| **Ash Guard** | Burned / Uncommon | 2 | Ally or self | **SCREEN (Ranged)** 25%, 3 turns | *"Screen an ally or yourself: ranged hits deal 25% less for 3 turns."* |
| **Ice Wall** | Frozen / Uncommon | ▼2 (was 3), **PRIOR** | Ally or self | **SCREEN (All)** 25%, this turn only; keeps the reactive AP gain (`GainApOnDamageIfSpeedHigher:1:1:0`) | *"[PRIOR] Screen an ally or yourself this turn: all hits deal 25% less. If they're hit by a slower foe, they gain 1 AP."* |
| **Compound Guard** | Staked / Uncommon | 3 | Ally or self | **SCREEN (Ranged)** 25%, 3 turns **+ MeleeDef +1** (existing stage) | *"Screen an ally or yourself: ranged hits deal 25% less for 3 turns, and raise their Melee Defense by 1."* |

Why these values:
- **Typed > typeless.** Blockade (Block, the barrier type) is the strongest Screen at 33%, matching Reflect's doubles size. Typeless Buffer Wall and Guard Posture are the weakest.
- **Ember Wall stays the only full nullify.** That keeps it worth its Legendary rarity.
- **AP.** Every ally-castable Screen or Shield costs at least 2. The 1-AP "free" walls were what overtuned `a_split_full`. Ice Wall keeps the 2-AP PRIOR that the tempo extension identified as the only non-Ember T1 answer.

### 6.1 Tests per move (proposed EditMode tests)

**Ember Wall**
1. Blocks all 3 hits of Blade Flurry and all hits of Cinder Burst (multi-hit).
2. Blocks on-hit riders (chemistry, stage drops) on the blocked target.
3. Doesn't block DoT ticks.
4. Is unusable on the next turn (recharge) and usable the turn after.
5. Resolves before a Speed-faster attacker (PRIOR).
6. Drains exactly 1 AP from the first blocked melee move. A second blocked move drains 0 more, and a ranged move drains 0.
7. Ends if the target swaps out.
8. Ignore Walls / Wallbreaker deal 25% through.
9. Learn-pool test: Ember Wall is in Cinislug's pool only and is the strictly highest rarity there.

**Vapor Barrier and Lattice Shield**
1. Pool = floor(pct × target MaxHP).
2. Overflow damage goes through.
3. Remainder halves at end of turn.
4. Expires after 2 turns.
5. Recasting replaces it and never adds to it.
6. A barrage hit deals ×2 to the Shield.
7. Lattice: reflect = 20% of absorbed, applied to the attacker.

**Screens (all six)**
1. Only matching-category hits are reduced.
2. A second Screen of the same category replaces the first; an All Screen replaces both.
3. A hit is reduced by at most one Screen. GP + Blockade on one target gives 33%, not 0.75 × 0.67.
4. Screens drop on swap-out and KO.
5. Core Breach and Breach Rush remove Screens before damage.
6. Guard Posture applies to the next hit only.
7. Ice Wall lasts the cast turn only, resolves PRIOR, and keeps the reactive AP gain.
8. Compound Guard applies the Screen and MeleeDef +1 independently.

**Regression**
1. Battle-level: the STALL fixture (self-Ember Cinislug vs two single-hit attackers) ends before turn 30 in 100% of seeded runs.
2. No battle in the matrix reaches `maxTurns`.

## 7. Cronus's idea, credited

The category split is the right instinct, and Pokémon backs it: Reflect vs Light Screen, and Ice Face blocking physical only. R keeps it as **Melee / Ranged Screens**.

What the sim rejects is pairing the split with **full blocks of 1 hit, refreshable every turn**. That is still a stall (KO mean turn 10.5), and it turns 1-AP walls into repeatable nullifies.

## 8. Code-change notes (if adopted — NOT done)

1. **Protect.** Add a turn-scoped Protect effect: new `UtilityEffectType` or a `FullAbsorbBarrier` mode with `turns=1, hits=unlimited, blocksRiders=true`. Also add PRIOR on `ember_wall`, a drain-once-per-move flag, and `RechargeTurns=2` (engine rc counts the grant tick).
2. **Screen categories.** Either:
   - add a contact-filter param to `MitigationWindow` (`Combat2DataImporter.cs:1696`, `ChainlingInstance.cs:453`), or
   - move Screens onto `IncomingDamageMitigation`, which already parses `contactFilter` (`Combat2DataImporter.cs:1815`). Verify its duration handling.
   
   Then replace per-category, keyed `screen_melee` / `screen_ranged` / `screen_all`.
3. **Shield.** Give `DamageThresholdBarrier` a %MaxHP magnitude option and end-of-turn halving.
   - `ApplyBarrierAbsorption` would only need to read `contactFilter` if a Shield ever gets a category; R doesn't need it.
4. **Swap.** Clear Screens and Shields on swap-out.
5. **Breakers.** New opcodes `RemoveScreens`, `ShieldShatter` (×2 vs Shield) and `ProtectPierce` (25%), plus data rows for Core Breach, Breach Rush, Pellet Storm and Ricochet Storm.
6. **Interim data-only hotfix** if the above is far off: give `ember_wall` `RechargeTurns=3` (= `ember_rc3`: stall 100% → 0%).

## 9. Blind spots

- **Protect policy.** The sim AI casts Protect conservatively (about 1 per battle). A human who protects more often (still bounded by the recharge) is not modelled, and neither is a full mixed-strategy equilibrium of the Protect/bait 50-50.
- **Bait model.** `R+bait` is a single-step hold, not a search.
- **Untested moves.** Vapor Barrier, Ash Guard and Lattice are in **no species pool**, so their R values are untested in kits.
- **Breakers.** The breakers (Core Breach, Shatter, Protect pierce) are not simulated.
- **STALL line.** R makes the self-Ember Cinislug team very strong against naive attackers (attacking-team win 5% vs 23% with no walls). Watch it in playtests. If it holds up, the lever is Protect = self-or-ally but **not usable by the same caster on two different targets in consecutive turns**, or dropping the drain (sim: no effect).
- **Win column in capped cells.** "win" in capped cells is a tie-break artefact.

## 10. Questions for Cronus (with defaults)

1. **Protect chaining:** deterministic 1-turn recharge, or Pokémon's 1/3^n RNG? *Default: recharge (identical sim numbers, readable).*
2. **Ember AP drain:** keep it (melee only, once per move), or drop it? *Default: keep, as flavour à la Burning Bulwark; it has no measurable balance effect.*
3. **Ember target:** ally or self (current SingleAlly), or self only? *Default: ally or self (2v2 identity).*
4. **Renames:** Barrier Wall → Buffer Wall and Lattice Guard → Lattice Shield? *Default: yes.*
5. **Screens/Shields drop on swap?** *Default: yes.*
6. **Blockade:** Melee 33% at 2 AP (tested), or Aurora-Veil-style All 33% at 3 AP? *Default: Melee 33%, 2 AP.*
7. **Protect counter:** 25% pierce for Ignore Walls/Wallbreaker, or a real Feint move? *Default: 25% pierce now; Feint later if Protect is over-used.*
8. **Interim hotfix:** ship `ember_wall` RechargeTurns=3 now, while R is built? *Default: yes.*

## 11. Reproduce

```
cd /workspace/balance/out
python3 walls_pvp_sim.py /workspace/balance/s10 4000 /workspace/balance/out/walls_pvp_sim.out.json   # ~100 s
python3 walls_pvp_check.py /workspace/balance/s10 /workspace/balance/out/walls_pvp_sim.out.json      # 65 PASS / 0 FAIL
```

Supporting checks:
- `tempo_2v2_sim.py` with no `wall_design` is regression-identical to the previous version: main matrix 0 diffs, ext matrix 286/286 cells 0 diffs.
- Designs live in `DESIGNS` / `DESIGN_OVR` in `tempo_2v2_sim.py`.
