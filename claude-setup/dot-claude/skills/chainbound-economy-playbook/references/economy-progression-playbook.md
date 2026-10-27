# Economy And Progression Playbook

> **SUPERSEDED by Ownership Spine 2026-09-15.**  
> Canonical sources: [`Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md`](../../../../Docs/Specs/Economy/OWNERSHIP_SPINE_v1.md) and [`Docs/Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md`](../../../../Docs/Specs/Economy/ECONOMY_01_EconomyAndProgression_v1.md).  
> This file is retained as **historical internal notes** where still useful. Prefer Known/Stamped (candy Stamp Core) over Ordinal/Inscription; Bound = Chainling books only. Do not implement Press Charge, Bound-as-move, or Bubblegum/cNFT Stamp from older language below.

---
## 1. Canon Terms

Player-facing (Ownership Spine 2026-09-15):

- **Known**
  - a move with 0 modifiers
  - non-NFT profile/PDA instance
  - legacy internal name: Ordinal — do not use Ordinal in player rules
- **Stamped**
  - a Known move stamped to **Metaplex Core** with exactly **1 or 2** modifiers
  - one player term for both trait counts; show `Traits: 1` or `Traits: 2` when needed
  - legacy internal names: Inscription / Rune — do not use those in player rules
- **Bound**
  - a published **Chainling** archive book (Metaplex Core)
  - **never** a move tier
- **RAM**
  - the primary in-game currency
  - the main pressure valve between reward generation and player spending
- **Rebirth**
  - a heavy RAM sink that resets star progression and grants permanent stat agency
- **Destinations**
  - **Academy candy machine** = roll/grant Known **and** Stamp (Core)
  - **Bindery** = Bind Chainlings into archive books only (no move Stamp)

Retired terms for new rules: Ordinal, Inscription, Rune, Bound move, Press Charge, cNFT/Bubblegum Stamp.

## 2. Core Economy Stance

The economy should feel grindy, but not random for randomness's sake.

The intended player fantasy is:
- farm the right chainlings for the right ordinals
- spend RAM to roll modifiers onto the right ordinals
- choose between short-term build power and long-term rebirth investment

That means:
- RAM is the main regulator
- ordinals are the main farming reward
- inscriptions are the main crafted or gacha-transformed reward
- direct modifier drops should be rare or removed from routine play

## 3. Faucet Policy

### A. Battle Completion

Default reward:
- low RAM
- XP
- very small chance at rare consumables

Do not use routine battles as a major inscription faucet.
Use battles to supplement player choice, not replace it.
Standard battle RAM should remain clearly below the salvage value of an ordinal.

### B. Level-Up Ordinal Farming

Default rule:
- level-ups drop ordinals, not finished inscriptions
- the ordinal must come from the leveling chainling's source pool

Default star-based drop chances:

| Star Level | Ordinal Drop Chance |
|-----------|----------------------|
| 1 | 10% |
| 2 | 20% |
| 3 | 30% |
| 4 | 40% |
| 5 | 50% |

Interpretation:
- each star-up is an independent roll
- no guaranteed drop on a single star-up
- the point is targeted move-farming, not constant reward flood

### C. Star 5 Farming

Do not let star 5 behave like a cheap infinite 50% faucet.

Default rule:
- once a chainling reaches star 5, further XP feeds a slower mastery cycle
- a completed mastery cycle gives one reward lane, not both:
  - an ordinal roll from source pool
  - or a RAM payout if the ordinal roll fails

Default mastery roll:
- 50% ordinal chance per completed mastery cycle

Reason:
- players can keep farming favored chainlings without feeling that max level suddenly became worse
- the balance lever is cycle time, not a feel-bad odds nerf
- the max-star state is regulated by cadence, not by changing the meaning of the drop table

Hard guardrail:
- a mastery cycle should require substantially more XP than a normal star-up
- if mastery farming starts dominating all other progression paths, increase mastery-cycle XP before lowering the drop rate

### D. Entangled Bonus

Keep entangled as the premium gameplay chase, and let that premium status carry into the endgame.

Default entangled benefits:
- +5 flat ordinal drop chance on each star-up roll
- slight rarity-weight improvement when an ordinal or inscription reward occurs
- carry the same flat bonus and rarity-weight improvement into star 5 mastery cycles

Example:
- common and uncommon weight shift downward slightly
- rare and above shift upward slightly

Avoid:
- huge flat guarantees
- entangled becoming required for normal progression

Balance rule:
- accept that entangled max-star chainlings are a premium long-term farm
- regulate that reward with encounter rarity and mastery-cycle time, not by stripping away the entangled payoff once the player reaches the endgame

### E. Modifier Drops

Default rule:
- remove routine direct modifier drops from normal progression
- modifiers should mainly enter through vending outcomes, reroll systems, rare event rewards, or entangled jackpots

Reason:
- too many parallel faucets weaken the value of ordinals and vending
- the player should understand that blank moves come from farming, and modification comes from spending

### F. Markets

Markets are the low-risk entry point.

Default rule:
- sell mostly Common and Uncommon typeless ordinals
- do not sell finished inscriptions in the standard market
- refresh on a predictable timer
- price entirely in RAM

Purpose:
- help new players fill starter loadouts
- give ordinals for vending
- reduce dead time when level-up farming misses

### G. Candy machine (Known roll + Stamp)

The Academy **candy machine** is the main move/modifier gacha. It has a **dual role**; the Bindery never rolls or Stamps moves.

#### Roll / grant Known

Default rule:

- produce 1 fresh **Known** instance (0 modifiers, non-NFT)
- may spend RAM per the active balance board
- never mints an NFT

#### Stamp Known → Stamped Core

Default rule:

- consume **1 Known** + RAM (+ wallet signature)
- output **1 Stamped** Metaplex Core move with **1 or 2** random compatible modifiers
- Stamp **consumes** the Known instance atomically; retries must not double-mint
- baseline cost example (tune via board, not recipe): 75 RAM + 1 Known + network fee

Design intent:

- the player farms the Known through gameplay (or rolls it at candy)
- the player spends currency at candy to Stamp modifiers onto the exact Known they chose
- candy = moves/modifiers; Bindery = Chainling books

Hard rule:

- do not use pity that guarantees a specific modifier, category, or rarity after X rolls
- if Stamp uses VRF, each result should remain genuinely random within the selected pool
- **no Bubblegum/cNFT** Stamp path for new work; Core only (see OWNERSHIP_SPINE_v1)
- **no Press Charge**; **no Bound-as-move** gate

If Stamp frustration is too high, solve it with:

- modifier pool composition tuning
- reroll item access
- RAM pricing and salvage tuning

Do not solve it with:

- forced outcomes after bad streaks
- hidden guaranteed rarity thresholds
- deterministic category assignment after X rolls

### H. Events

Events should be additive, not economy-breaking.

Default rule:
- events grant event currency or quest completion rewards
- rewards are mostly event ordinals, cosmetics, or sidegrades
- event-exclusive finished inscriptions should be rare and not stronger than permanent content

### I. Capture Rewards

Default rule:
- standard captures do not mint finished inscriptions
- entangled captures may mint 1 inscription or otherwise grant a premium modifier-linked reward

Reason:
- keeps capture exciting
- stops routine captures from becoming another large inscription faucet

## 4. Sink Policy

### A. RAM Sinks

RAM should be spent on:
- vending
- rerolls
- market purchases
- rebirth
- selective future convenience systems

RAM should not be so scarce that players never spend it.
RAM should not be so abundant that vending becomes free behavior.

### B. Burn / Salvage

Burning ordinals or inscriptions for RAM is valid, but it must be low efficiency.

Hard rule:
- burning can never return enough RAM to self-fund repeated vending loops

Default salvage stance:
- ordinal burn returns a small RAM fraction
- inscription burn returns a slightly larger RAM fraction than ordinals, but still clearly below acquisition cost

Design rule:
- salvage is a pressure-release valve, not a profit engine

### C. Rebirth

Rebirth is the major long-term sink.

Default rule:
- available only at star 5 (can be triggered mid-mastery or after any mastery cycle)
- max 5 rebirths per chainling (lifetime cap)
- costs heavy RAM per escalating curve (1000 / 1500 / 2000 / 2500 / 3000)
- grants +1 IV point that the player allocates to a single stat of their choice
- resets star progress to 0; XP and mastery progress reset
- increases future leveling difficulty (1.18^rebirth_count)

IV allocation (player-choice model):
- on rebirth, player chooses ONE of the 6 core stats: MaxHP, MeleeAtk, MeleeDef, RangedAtk, RangedDef, Speed
- that stat's IV is permanently increased by +1
- rebirth bonuses stored separately from base IVs; effective IV = base_iv + rebirth_bonus
- no per-stat cap on allocation (player may put all 5 points into one stat)

Player choice tension:
- hold at star 5 to farm ordinals and RAM
- or spend heavily on rebirth for long-term stat growth

This is the intended long-term pacing lever.

Recommended scaling:
- RAM cost increases every rebirth
- XP requirement also increases every rebirth

Good default logic:
- RAM cost grows faster than linearly
- XP curve grows enough to slow infinite loops, but not enough to make rebirth feel pointless
- star 5 mastery remains attractive, but not so efficient that rebirth becomes irrational to delay forever

## 5. Anti-Inflation Rules

- No more than one major item faucet should fire from the same core action unless the moment is explicitly premium
- Finished inscriptions should never be more common than ordinals
- Modifier generation should be gated behind RAM spend more often than behind random free drops
- Market purchases and salvage values must never create a buy-burn-profit loop
- Entangled bonuses should be noticeable, but not so strong that normal chainlings feel disposable

## 6. Player Experience Goals

The economy should produce these feelings:

- "I know which chainling I should level if I want that move."
- "I always have a use for RAM."
- "Vending is exciting because I brought the right ordinal."
- "Rebirth is a meaningful commitment, not an automatic best choice."
- "Entangled finds feel special."
- "Even when I do not high-roll, I still made progress."

## 7. Open Tuning Levers

Tune these before inventing new systems:
- star-up ordinal drop chances
- mastery-cycle XP cadence at star 5
- entangled bonus strength
- RAM payout per battle
- standard vending RAM cost
- vending rarity weights
- salvage return rates
- rebirth RAM cost curve
- rebirth XP curve

If the economy feels too generous, lower free item faucets before increasing RAM costs.
If the economy feels too dry, improve targeted ordinal access or RAM earnings before raising inscription output.
