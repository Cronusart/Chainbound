# COMBAT Type Profiles

Type playstyle profiles for Chainbound combat. Each type has a blockchain theme, playstyle role, chemistry matchup identity, key General statuses, and move design keywords.

**Meme** is documented below but **excluded from the 13-type chemistry balance pass** until its transmutation kit is finalized.

---

## Chemistry Matchup Rules

These rules govern how lists in this doc map to `COMBAT_Interaction_Matrix` cells and runtime resolution.

### ChemAdv

| List column | Meaning | Matrix authoring |
|-------------|---------|------------------|
| **ChemAdv -> (beats)** | When this type attacks listed types, the forward cell is filled. The hit gets flat Advantage and applies that cell's one chemistry reaction. | Attacker row -> defender column |
| **ChemAdv <- (weak to)** | When listed types attack this type, their forward cell is filled. They get ChemAdv. | Their row -> this column |

### ChemDis

ChemDis is produced by the current resolver's reverse lookup. If `A -> B` has a chemistry cell and `B -> A` is empty, then:

- `A` attacking `B` gets ChemAdv and applies the status.
- `B` attacking `A` gets ChemDis only; no status is applied from the resist path.

| List column | Meaning | Matrix authoring |
|-------------|---------|------------------|
| **ChemDis -> (resisted by)** | When this type attacks listed types, it is resisted through the listed type's reverse cell. | Listed type row -> this type column |
| **ChemDis <- (resists)** | When listed types attack this type, they are resisted through this type's forward cell. | This type row -> listed type column |

### Hard Constraints

1. **No mutual ChemAdv:** For types A and B, never both `A -> B` and `B -> A` forward cells.
2. **No Adv + Dis on the same directed attack:** If `A -> B` is ChemAdv, `A -> B` cannot also be ChemDis.
3. **Dual-type defenders** evaluate each resolved type, then collapse the total to flat Advantage, Neutral, or Resisted damage. They never compound multipliers.
4. **One reaction per cell:** every authored cell has one status and an explicit `@Self` or `@Foe` recipient; no numeric stacks or compound outputs.
5. **Status effects** are authored in `COMBAT_Interaction_Matrix_v1.csv`; `COMBAT_Interaction_Matrix_v1_directions.csv` is the audit grid.

### Matrix Role Targets

The v1 pass intentionally uses a Pokemon-starter style graph: clear one-way counters, several readable triangle loops, and no mutual advantage pairs.

| Role | Types | ChemAdv -> target | ChemAdv <- target |
|------|-------|-------------------|-------------------|
| **Aggressor forward** | Burned, Shard | 6-7 | 4 |
| **Net 0 controller** | Node, Gas, Encrypted, Frozen, Forked, Mined, Bot, Glitched | 4-5 | 4-5 |
| **Low-density soft wall** | Compressed | 3 | 3-4 |
| **Wall forward** | Block, Staked | 3 | 5 |

### Reaction Role Rules

- **Aggressors (Burned, Shard):** forward cells primarily place foe-facing Volatility, Structure, Exposure, or Latency pressure. Their matchup breadth converts into pressure, not extra numeric stacks.
- **Controllers:** forward cells use foe-facing Liquidity denial, Latency, Structure debuffs, and Contagion. Self-facing reactions are limited to profile-defining tempo or ramp, such as Pumped and FOMO.
- **Walls (Block, Staked):** their narrow forward coverage favors self-protection or sustain (Firewall, Drip, Siphoned), with Anchored as the anti-spread exception.
- **Infrastructure / efficiency (Node, Forked, Compressed):** reactions route value through cleanse, propagation, controlled setup, and precise disruption. Splinter replaces Forked's legacy recoil stacking.

Every filled cell remains within Liquidity, Volatility, Latency, Structure, Exposure, Sustain, Cleanse, or Contagion.

---

## Summary Table

Pairing audit: **no mutual ChemAdv** between any two non-Meme types in this table.

| Type | Matrix role | Playstyle | ChemAdv -> (beats) | ChemAdv <- (weak to) | ChemDis -> (resisted by) | ChemDis <- (resists) | Key General statuses | Move keywords |
|------|-------------|-----------|--------------------|----------------------|--------------------------|----------------------|----------------------|---------------|
| **Node** | Net 0 starter | Team coordinator. Routes AP, extends buffs, wide heals. | Frozen, Forked, Mined, Compressed | Gas, Bot, Glitched, Shard | Gas, Bot, Glitched, Shard | Frozen, Forked, Mined, Compressed | HODLed, Immovable | SUP-heavy, APMAN, AOE buff, redirect |
| **Gas** | Net 0 | AP tempo disruptor. Taxes enemy AP, spikes Atk/Def trade. | Node, Block, Staked, Bot | Encrypted, Burned, Frozen, Mined | Encrypted, Burned, Frozen, Mined | Node, Block, Staked, Bot | GasSpike, MEV Extracted | APMAN, volatile self-trade DMG, Bridging |
| **Block** | Wall forward | Physical wall. Grounding, barriers, anti-mobility. | Burned, Forked, Shard | Gas, Mined, Compressed, Bot, Glitched | Gas, Mined, Compressed, Bot, Glitched | Burned, Forked, Shard | HODLed, Immovable, 51% Attacked | Barriers, defense ramp, grounding |
| **Encrypted** | Net 0 | AP drain controller. Strips resources, debuff immunity. | Gas, Burned, Frozen, Bot | Mined, Compressed, Glitched, Shard | Mined, Compressed, Glitched, Shard | Gas, Burned, Frozen, Bot | EncryptedSeal, MEV Extracted | AP drain, cipher effects, debuff immunity |
| **Burned** | Aggressor forward | DoT pressure. Wide typed coverage; wins long attrition lanes. | Gas, Frozen, Mined, Staked, Compressed, Bot, Shard | Block, Encrypted, Forked, Glitched | Block, Encrypted, Forked, Glitched | Gas, Frozen, Mined, Staked, Compressed, Bot, Shard | BurnedTokens, FUD | DoT, burn, coating setups |
| **Frozen** | Net 0 starter | Speed controller. Jams, halts, locks assets. | Gas, Bot, Staked, Glitched, Shard | Node, Encrypted, Burned, Mined, Compressed | Node, Encrypted, Burned, Mined, Compressed | Gas, Bot, Staked, Glitched, Shard | FrozenAssets, HODLed | Speed debuffs, Jam, asset lock |
| **Forked** | Net 0 spread | Infrastructure spreader. Chain splits propagate pressure or route a defensive cleanup without recoil stacking. | Burned, Mined, Bot, Glitched | Node, Block, Staked, Shard | Node, Block, Staked, Shard | Burned, Mined, Bot, Glitched | Splinter | AOE, chain split, propagation, cleanup |
| **Mined** | Net 0 ramp | Ramp attacker. Crashes tanks; spikes into soft typings. | Gas, Block, Encrypted, Frozen, Staked | Node, Burned, Forked, Bot, Shard | Node, Burned, Forked, Bot, Shard | Gas, Block, Encrypted, Frozen, Staked | FOMO | Physical ramp, extraction, scaling |
| **Staked** | Wall forward | Sustain leech. Patient, evasive, anchoring. | Forked, Compressed, Shard | Gas, Burned, Frozen, Mined, Bot | Gas, Burned, Frozen, Mined, Bot | Forked, Compressed, Shard | HODLed, Yield | Siphon, evasion, leech, growth |
| **Compressed** | Low-density soft wall | Precision efficiency. Consistent drain and evasion. | Block, Encrypted, Frozen | Node, Burned, Staked, Shard | Node, Burned, Staked, Shard | Block, Encrypted, Frozen | — | AP efficiency, Jam, evasion, precision |
| **Bot** | Net 0 starter | Automated pressure. AP denial, multi-hit attrition. | Node, Block, Mined, Staked, Glitched | Gas, Encrypted, Burned, Frozen, Forked | Gas, Encrypted, Burned, Frozen, Forked | Node, Block, Mined, Staked, Glitched | BotSwarm, MEV Extracted, ThreadLock | Automation, AP denial, multi-thread, attrition |
| **Glitched** | Net 0 | Chaos exploiter. Corrupts defense; punishes predictability. | Node, Block, Encrypted, Burned | Frozen, Forked, Bot, Shard | Frozen, Forked, Bot, Shard | Node, Block, Encrypted, Burned | GlitchedMind, ExploitMark | Random effects, exploit setups, defense corruption |
| **Shard** | Aggressor forward | Precision aggressor. Surgical counters; few hard answers. | Node, Encrypted, Forked, Mined, Compressed, Glitched | Block, Burned, Frozen, Staked | Block, Burned, Frozen, Staked | Node, Encrypted, Forked, Mined, Compressed, Glitched | ShardPartition | Single-target, partition field, precision burst |
| **Meme** | Out of pass | Universal wildcard. Transmutates typings it touches. | TBD | TBD | TBD | TBD | Staggered, Surging | Random, hype effects, all-in plays |

---

## Crypto-Readable Triangle Loops

These loops are intended to make the chart feel like starter-type rock/paper/scissors rather than a random lookup table.

- **Starter triangle:** Node -> Frozen -> Bot -> Node. Nodes keep liveness through frozen state, frozen locks automation, bots overload node infrastructure.
- **Fee market triangle:** Gas -> Block -> Burned -> Gas. Fees fill blocks, blocks contain burn pressure, burns eat gas spend.
- **Finality triangle:** Mined -> Block -> Forked -> Mined. Miners produce blocks, blocks canonicalize forks, forks reorganize miners.
- **Security triangle:** Encrypted -> Bot -> Glitched -> Encrypted. Encryption hides signals from bots, bots exploit unstable glitches, glitches break cipher implementations.
- **Control cycle:** Burned -> Frozen -> Gas -> Block -> Burned. Burns thaw frozen assets, frozen state stalls gas tempo, gas fills blockspace, blocks contain burn pressure.
- **Scale triangle:** Mined -> Frozen -> Shard -> Mined. Hash power breaks frozen stalls, frozen state slows precision shards, shards out-scale mining.
- **Yield triangle:** Staked -> Shard -> Mined -> Staked. Staking secures shards, shards out-scale mining, mining pressure challenges staking economics.
- **Efficiency triangle:** Compressed -> Encrypted -> Burned -> Compressed. Compression exploits structured payload overhead, encryption defuels burn pressure, burn pressure destroys efficiency.
- **Coordination triangle:** Node -> Forked -> Bot -> Node. Nodes canonicalize forks, forks split automation, bots overload node infrastructure.

---

## Per-Type Notes

### Aggressors

**Burned** is the broad attrition aggressor: it pressures Gas, Frozen, Mined, Staked, Compressed, Bot, and Shard, but folds to hard containment from Block, Encrypted, Forked, and Glitched.

**Shard** is the surgical aggressor: it cuts through routing, encryption, forks, mining, compression, and glitches, but has four clean answers in Block, Burned, Frozen, and Staked.

### Controllers

**Node / Gas / Encrypted / Frozen / Forked / Mined / Bot / Glitched** sit near the chart center. Each has four to five proactive wins and four to five clear losses, so their value comes from kit shape instead of raw coverage alone.

The starter trio is deliberately readable: **Node beats Frozen**, **Frozen beats Bot**, and **Bot beats Node**.

### Walls

**Block / Staked** have few forward wins and several readable bad matchups. They win by anchoring, resisting via reverse lookup, and forcing bad trades, not by sweeping the chart.

### Infrastructure And Efficiency

**Node** is no longer a weak-coverage outlier because it is one of the three starter chainlings. Its primary gameplay value is still support, AP routing, and coordination, but the chart now gives it four clean parity matchups.

**Compressed** is the lowest-density soft-wall type. It has three narrow proactive wins: Block, Encrypted, and Frozen. It represents rollup-style efficiency that solves blockspace, cipher overhead, and stalled-state problems without becoming a broad counter type.

---

## Legacy v0 Reference

Do **not** import these deprecated v0 pairings without re-audit. They violated mutual ChemAdv or role targets:

- Gas <-> Burned both forward
- Encrypted <-> Block both forward
- Forked <-> Block both forward
- Glitched <-> Burned both forward
- Frozen <-> Mined both forward
- Meme row/column using Hyped coatings against every type

Status names from v0 remain valid chemistry effects; type directions now come from `COMBAT_Interaction_Matrix_v1.csv`.

---

## Related Specs

- `COMBAT_Chemistry.txt` - resolver order, ChemistryOnly whitelist
- `COMBAT_Interaction_Matrix_v1_directions.csv` - v1 audit grid
- `COMBAT_Interaction_Matrix_v1.csv` - v1 runtime/status grid
- `Docs/Combat/COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md` - pass workflow
