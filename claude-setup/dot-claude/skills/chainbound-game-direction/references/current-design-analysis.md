# Current Design Analysis

## Terminology Override

Use this terminology for future design work even when older specs use legacy wording:

- Ordinal
  - a move with 0 modifiers
- Inscription
  - an ordinal with 1 or more modifiers applied
  - in the short-term vending plan, most inscriptions will carry exactly 1 modifier
- Source-exclusive ordinal
  - an ordinal that only drops from a specific chainling source, but may still be equip-eligible for other chainlings if the broader type-pool rules allow it

When older docs say "inscription drop" for a plain move reward, translate that to "ordinal drop."

## Product Shape

Chainbound / Signal Jam 3D is currently a combat-first creature battler with three intertwined progression layers:

1. Chainling progression
   - capture wild chainlings
   - gain levels and eventually evolve
   - expand ordinal and inscription capacity through slot growth

2. Ordinal and inscription progression
   - acquire ordinals through level-up, market, events, and selective capture rewards
   - convert ordinals into inscriptions through vending or other modifier systems
   - build around type pools, typeless access, species pools, and rare effects

3. Modifier progression
   - attach extra behavior to moves for deeper build crafting
   - chase higher-rarity and more build-defining variations over time

The intended identity is good:
- readable, strategic combat
- rewarding collection and build experimentation
- aspirational rarity chase
- no direct equivalence between rarity and raw power

## Current Economy And Progression Spine

### Reward Sources

- Standard chainling level-up:
  - legacy design: 70% plain move reward, now interpreted as ordinal
  - 20% rare consumable
  - 10% currency or tokens
- Entangled chainling level-up:
  - legacy design: 90% plain move reward, now interpreted as ordinal
  - 5% rare consumable
  - 5% currency or tokens
- Modifier drop chances:
  - 15% standard level-up
  - 25% entangled level-up
  - 30% premium vending
- Capture reward:
  - successful capture creates a Chainling PDA
  - older capture spec mints exactly 1 inscription NFT
- Additional planned acquisition:
  - vending machines
  - daily market
  - event shop
  - future PvP rewards, achievements, trading, crafting

### Progression Spine

- Slot progression is strong and easy to communicate:
  - stage 1 starts with 6 slots
  - each evolution adds 2
  - cap is 16
- Mythic limits prevent total rarity stacking:
  - 2 Mythics per chainling
  - 4 Mythics per team
- Pool access creates long-term variety:
  - type pool
  - secondary type pool
  - typeless pool
  - species exceptions
  - Meme-type wildcard access

### Combat Depth Drivers

- AP management
- speed and priority
- targeting and slot logic
- barrier types and bypass
- support and redirect effects
- species-exclusive moves
- move modifiers

This is a healthy foundation for long-term strategy because power is not coming from one axis alone.

## What Is Already Strong

### 1. Rarity Philosophy Is Healthy

The specs consistently say rarity should affect complexity and availability, not raw output. That is the right anti-pay-to-win principle for a competitive or strategy-heavy game. It means the chase can stay exciting without making lower-rarity players feel invalidated.

### 2. The Gacha Layer Has Real Strategic Meaning

The random chase is not just "bigger numbers." It is:
- new ordinal access
- rarer conditional effects
- modifier combinations
- entangled bonus odds
- selective pre-inscribed capture rewards

That is more addictive than flat item rarity because it changes how teams play, not just how hard they hit.

### 3. Slot Growth Is A Clear Long-Term Reward

Going from 6 to 16 slots gives a visible sense of progression and more room for experimentation. This is easy to understand and naturally rewarding.

### 4. Capture Rewards Tie Exploration To Combat

Encounter -> battle -> capture -> reward payout is a strong loop. It turns battles into acquisition opportunities instead of isolated matches.

### 5. Build Diversity Looks Real

The balance review suggests the game already supports burst, tank, AP control, speed control, support, and hybrid play. That is important because gacha only feels compelling when new rewards can unlock meaningfully different archetypes.

## Main Risks In The Current Design

### 1. Too Many Reward Faucets Before Enough Sinks

The current design already includes:
- ordinal drops on level-up
- modifier drops
- vending
- markets
- events
- capture-minted inscriptions
- entangled rate boosts

That is a lot of supply. The design documents mention future crafting and trading, but they do not yet define a strong duplicate sink, pity conversion, recycling loop, or pressure-release system for inventory saturation.

Risk:
- reward inflation
- choice overload
- duplicate frustration
- reduced excitement when rewards become too common

Default direction:
- add or reserve a duplicate conversion sink before increasing acquisition rates further
- do not add another reward faucet until the sink story is clearer

### 2. The Chase Stack May Become Hard To Read

A player may eventually have to understand all of this at once:
- chainling rarity or entangled status
- capture odds
- ordinal rarity or source identity
- inscription rarity after modification
- modifier rarity
- slot growth
- species pools
- exclusive moves
- Mythic limits

That is rich, but also potentially opaque.

Risk:
- "interesting on paper, confusing in play"
- players not understanding what they should chase next

Default direction:
- maintain one dominant chase target per phase of progression
- early game should emphasize simple move acquisition and roster growth
- mid-game should emphasize synergy and slot expansion
- late game should emphasize modifiers, entangled hunting, and optimization

### 3. Support And Healing Density Looks Thin

The balance review calls out support and healing as thinner categories. If offense and AP denial outgrow sustain tools, players may feel punished before they understand how to stabilize a fight.

Risk:
- early meta feels burst-heavy
- strategic counterplay narrows
- difficulty feels unfair rather than skill-testing

Default direction:
- expand support and healing slowly, especially in under-supported types
- keep sustain tactical, not infinite

### 4. Hackathon Progression May Delay The Best Payoffs

The capture spec explicitly caps chainling level before evolution-stage NFT promotion. That is pragmatic for the demo, but it means the visible long-term progression spine may not fully pay off in the hackathon build.

Risk:
- players see the promise of progression more than they feel it
- slot growth or evolution rewards may arrive too late for the demo loop

Default direction:
- make sure early and mid-run rewards feel satisfying without requiring deep evolution
- lean on captures, ordinals, entangled variants, and build shifts as the primary MVP payoff

### 5. Wallet And On-Chain Friction Can Arrive Too Early

The profile and capture specs are good as architecture, but wallet connection and asset visibility can easily become friction if surfaced before the player cares about the game.

Risk:
- onboarding turns into setup
- the collection loop feels administrative instead of magical

Default direction:
- keep combat fun and understandable before requiring blockchain mental load
- use chain integration as validation and ownership frosting on top of a working battle/collection loop

## Design Implications For Future Decisions

### Economy

- Prefer a small number of currencies with obvious meanings.
- Make each currency solve a different problem:
  - routine progression
  - targeted acquisition
  - event participation
- Avoid adding currencies that only exist to paper over tuning gaps.

### Progression

- Reward breadth first, optimization second.
- Let players feel stronger because they understand more, not only because they rolled rarer content.
- Keep progression milestones visible:
  - new team option
  - new slot
  - new modifier
  - new support tool
  - new capture target

### Gacha

- Randomness should create anticipation, not helplessness.
- Good gacha in this game means:
  - the player can imagine a build before they own it
  - the reward changes strategy, not only numbers
  - unlucky streaks still create some forward motion

Missing pieces to watch:
- pity
- duplicate conversion
- pity-adjacent targeted purchase options
- controlled rarity exposure in early game

### Difficulty

- Difficulty should come from AP pressure, chemistry matchups, positioning, barriers, and timing.
- Avoid making "complexity equals difficulty."
- If a player loses, they should have an actionable lesson.

## Current Default Recommendation

When reviewing future design proposals, bias toward:

- clear session rewards
- strategic breadth over power creep
- rarity as aspiration, not stat inflation
- one major chase layer at a time
- entangled and modifier systems as long-term hooks
- support and sustain additions where the meta is too burst-skewed
- duplicate sinks before more reward inflation
- off-chain-first fun, on-chain validation second

## Questions The Skill Should Always Ask

- What is the player chasing in the next 10 minutes?
- What is the player chasing over the next week?
- Does this change improve combat choices or only increase accumulation?
- Does the player understand why a reward matters?
- Does the change create a sink, a faucet, or both?
- What happens to unlucky players?
- What happens to veterans with large inventories?
- Does this help readability or only add more systems?
