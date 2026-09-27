# Chemistry Reaction Redesign — Creative Suggestions (Pass 4.5)

> **Purpose:** Evaluate ChemistryOnly outputs against the v1 matrix, prune overpowered or redundant effects, and propose a **small standardized kit** aligned with type roles, Pokémon-like clarity, and occasional TCG-style RNG.
>
> **Inputs:** User review (Firewalled, Halted, Crashed, etc.), `Statuses.csv`, `COMBAT_Interaction_Matrix_v1.csv`, `COMBAT_TypeProfiles.md`.
>
> **Not in scope here:** Meme coatings, General statuses, resolver implementation (tracked as follow-ups).

---

## 1. Design goals

| Goal | Detail |
|------|--------|
| **Readable** | Player sees ~6–8 chemistry “families,” not 20 one-offs |
| **PvP-safe** | No full-turn skip, no 50% HP instant, no hard immunity to half of damage categories |
| **Role-aligned** | Aggressors get pressure; walls get mitigation/resist texture; controllers get AP/speed |
| **Pokémon-familiar** | Burn/Poison/Paralyze/Sleep analogues where it helps onboarding |
| **TCG spice** | Optional coin-flip proc on select chemistry hits (deterministic seed in ranked) |
| **Matrix-first** | Pick effect **after** direction is fixed; one primary status per ADV cell at ×1–×2 stacks |

---

## 2. Proposed standardized chemistry kit (v2 target)

Narrow ChemistryOnly outputs to **8 core families + 2 coatings + 2 consumed setups**. Everything else merges, moves to General moves, or deprecates.

| Family | Status ID(s) | Pokémon / TCG analogue | Typical role on matrix |
|--------|----------------|------------------------|-------------------------|
| **Liquidity (AP)** | Pumped, Drained | Energy / Suppression | Gas, Encrypted, Bot, Staked answers |
| **Volatility (DoT)** | Rugged *(revised)*, FOMO *(revised)* | Poison / Toxic, “reckless boost” | Burned, Glitched pressure |
| **Latency (control)** | Jammed, **Slowed** *(replaces Halted)* | Accuracy down / Paralyze | Frozen, Compressed, Shard |
| **Structure (stages)** | Corroded, Dusted, Reinforced | Defense drop / Sand-Attack / Iron Defense | Glitched, Block, Mined |
| **Exposure (contact)** | Backlash[Type], Leaked | Rough Skin / shared pain | Forked, Node, Shard |
| **Leech / sustain** | Siphoned, **Drip** *(replaces Liquidity Pump)* | Leech Life / Leftovers-lite | Staked, Burned sustain lane |
| **Cleanse** | Defueled | Clear Smog–lite | Burned vs Pumped/Rugged |
| **Contagion** | Cascade Failure *(revised)* | “Three strikes” / poison counters | Glitched vs Block |
| **Coatings** | TypeCoating, HypedTypeCoating | Type rewrite | Meme pass / rare ADV cells |
| **Consumed setup** | Amplified, Anchored, **Splinter** *(replaces Bridged)* | Charge / lock targeting / spread | Forked, Block, Staked |

**Deprecate or merge**

| Remove / merge | Into |
|----------------|------|
| **Narrowcast** | **Anchored** (debuff variant: foe’s next move forced single-target) |
| **Firewalled** | **Firewall** *(new name)* — mitigation, not immunity |
| **Halted** | **Slowed** — speed / priority modifier |
| **Crashed** | **Shattered** — small chip + Def −1, no % HP nuke |
| **Liquidity Pump** | **Drip** — smaller heal, different trigger than Siphoned |
| **Evasive** | **Dusted** buff variant or **CompressedData** (General) — avoid +3 Eva chemistry |
| **Overclocked** | Keep for Forked identity only; limit to 1–2 matrix cells |

---

## 3. Flagged outputs — redesign proposals

### 3.1 Firewalled → **Firewall** (mitigation, not immunity)

**Problem:** Full immunity to `Projectile`-tagged moves deletes half the ranged meta for 2 turns.

**Pokémon reference:** Reflect Light Screen reduce damage; Protect blocks one turn — not permanent type-wide immunity.

**Proposal — `Firewall` (Duration 2, buff)**

- First **projectile-tagged** hit each turn: **−30% damage** (or apply 0.25 mitigation source via existing `AddMitigationSource`).
- **Melee hits unaffected.**
- **Multi-hit:** consumes one “charge” per hit OR one charge per turn (pick per-turn for simplicity).
- **Stacks with ChemAdv:** chemistry still applies; Firewall only mitigates damage after formula.

**TCG twist (optional):** On apply, **coin flip** — heads = also **+1 Def stage**; tails = mitigation only. Ranked uses battle seed.

**Matrix fit:** Burned → Gas (was Firewalled) becomes **soft counter** to ranged tempo, not hard counter.

**Engine note:** `Firewalled` is catalogued; **projectile immunity is not clearly wired** in resolver today — good time to replace before implementation.

---

### 3.2 Halted → **Slowed** (Paralyze analogue, not skip turn)

**Problem:** Skip next turn is swingy; no bench = dead slot feel.

**Pokémon reference:** Paralysis — 25% full skip in Gen 8+, often **Speed cut** in modern formats; “can’t move” is rare.

**Proposal — `Slowed` (Duration 2, debuff)**

Choose **one** primary mode (recommend A for PvP):

| Mode | Effect |
|------|--------|
| **A — Speed stage** | **Speed −2** on apply; **25%** chance to **lose action** on turn start (like General `Rekt`, but chemistry-only) |
| **B — Priority tax** | Attacker who applied Slowed gains **priority** on next damaging move *(if priority system exists)*; Slowed target **Speed −1** |
| **C — AP friction** | **−1 AP** on next turn start (lighter than Drained over time) |

**Force-swap fantasy:** Pair Slowed with **Isolated** on a different matchup — encourage swap without hard lock.

**Matrix fit:** Frozen → Staked `Halted x1` becomes **Slowed x1** or **Jammed x1** (Frozen already has Jammed elsewhere).

**Engine note:** `Halted` sets `skippingNextTurn` in `StatusProcessor` — replace with proc roll + Speed stages.

---

### 3.3 Crashed → **Shattered** ( tank break, not execute)

**Problem:** 50% current HP + Def −3 on chemistry hit is fight-ending.

**Pokémon reference:** Super Fang / Ruination cap damage; **Screech** Def drop without massive chip.

**Proposal — `Shattered` (Instant or Duration 1, debuff)**

- **Def −2** (one stage cap for chemistry instant) **OR** **Def −1** if also dealing chip.
- **Chip:** **max(1, floor(8% of defender current HP))** — scales without deleting tanks from full.
- **No** stack with multi-hit on same turn unless each hit rolls chemistry separately (keep one application per hit).

**Alternative for Mined → Block identity:** Use **Corroded x1** (ongoing) + **Dusted x1** instead of instant — slower break, same fantasy.

**Matrix fit:** Mined → Block / Frozen `Crashed` → **Shattered x1** or **Corroded x1**.

---

### 3.4 Liquidity Pump → **Drip** (sustain, not bulk heal)

**Problem:** 25% MaxHP heal is huge on walls; overlaps Siphoned fantasy.

| | **Siphoned** | **Drip** *(new)* |
|--|--------------|------------------|
| Trigger | Heal **25% of damage dealt** on attack | **End of turn:** heal **6% MaxHP** |
| Fantasy | Leech / vampire | Passive yield / liquidity return |
| Best on | Staked, aggressive sustain | Burned self-sustain lane, defensive chem |

**Pokémon reference:** Leftovers (1/16 ≈ 6.25% per turn); Aqua Ring.

**Proposal:** Replace `LiquidityPump` with **Drip** — Duration 3, EoT heal **6% MaxHP** (tune 4–8% in playtest).

**Matrix fit:** Only on **1–2** cells max (not wide); remove from multi-stack Rugged lines.

---

### 3.5 Rugged → **Badly Rugged** (Toxic-style escalation)

**Problem:** Flat 10% MaxHP every turn is burn without escalation; too strong at ×2–×3 stacks on one hit.

**Pokémon reference:** Toxic — **N/16** MaxHP where N increments each turn (1, 2, 3… cap at 15).

**Proposal — revise `Rugged` mechanics**

- Track **`ruggedTurns`** on status instance (or reuse `counterValue`).
- EoT damage: **`ruggedTurns / 16 × MaxHP`**, increment `ruggedTurns` each tick, **cap at 15**.
- **Burn analogue:** separate **Burned** chemistry could stay flat **1/16** if added later; Rugged = **badly poisoned** lane.
- **Defueled** still cleanses Rugged stacks.

**Matrix fit:** Burned aggressor cells at **×1–×2** only (v1 already uses ×1–×2 in many cells — good).

---

### 3.6 FOMO → **FOMO** (revised risk/reward)

**Problem:** **Atk +3** on one chemistry proc snowballs.

**Pokémon reference:** Guts / Flare Boost — power when statused; **Swords Dance** is a **move**, not free on SE hit.

**Proposal**

- **Atk +1** on apply (Duration 3), **not +3**.
- Self DoT: **1/16 MaxHP** EoT (same as Toxic increment **or** flat 1/16 — simpler).
- **Optional TCG:** Each EoT, coin flip — heads **Atk +1** (cap +2 total from FOMO); tails **self chip only**.

**Matrix fit:** Mined → Encrypted / Staked; keep **×1** stacks.

---

### 3.7 Evasive → merge / soften

**Problem:** **Eva +3** is basic and stacks with stages.

**Proposal**

- **Remove** standalone Evasive from chemistry kit.
- Replace with **Dusted** on foe (Eva −2) or **CompressedData**-style buff on self via General moves.
- If a Staked identity cell needs “hard to hit”: **Eva +1** for Duration 2 (wall buff, not chemistry spike).

---

### 3.8 Anchored + Narrowcast → **Anchored only**

**Problem:** Duplicate “next move single-target” consumed statuses.

**Proposal — unified `Anchored`**

| Variant | Applied to | Effect |
|---------|------------|--------|
| **Anchored (debuff)** | Foe | Next move **must** be single-target; **AoE / Bridged / Broadcast suppressed** |
| **Anchored (self)** | Self *(optional future)* | Next move **cannot** spread (same opcode) |

**Matrix:** Shard → Forked used Narrowcast — map to **Anchored x1** debuff on defender.

**Engine:** `CombatResolver` already expands Bridged/Broadcast; Anchored should **block** `hitAllFoes` when consumed on target.

---

### 3.9 Bridged → **Splinter** (controlled spread)

**Problem:** Next move hits **all foes at full power** = wipeout with strong AoE.

**Pokémon reference:** Spread moves in doubles deal **75%** in mainline; **Earthquake** hits everyone.

**Proposal — `Splinter` (Consumed, 1 turn)**

| Active foes | Behavior |
|-------------|----------|
| **2+** | Damaging move hits **all active foes** at **65%** damage each (tune 60–75%) |
| **1** | **No spread** — instead **+1 ChemAdv stack** on that hit **OR** **+15% damage** (pick one) |

**Alternative (simpler):** **Forked-only** General modifier; chemistry applies **Overclocked** (recoil) instead of spread.

**Matrix fit:** Remove Bridged from chemistry unless **one** Forked → Gas cell at ×1; prefer **Overclocked** / **Leaked** for Forked spread fantasy.

---

### 3.10 Cascade Failure — clarify + swap reset

**Problem:** 3rd stack → KO is dramatic; **“resets on swap”** is in CSV but **not implemented** on swap.

**Pokémon / TCG reference:** Poison counters on TCG Pokémon (3 damage counters); **Perish Song** (4 turns) — telegraphed.

**Proposal — `Cascade Failure` (Counter, threshold 3)**

| Rule | Detail |
|------|--------|
| **Each chemistry apply** | +1 counter (×1 stack per hit, not ×3 instant KO setup) |
| **At 3** | **Heavy damage** = **25% MaxHP** (not auto-KO) **OR** **KO only if already ≤25% HP** |
| **On swap out** | **Reset counter** on outgoing Chainling |
| **On bench** | Counter **pauses** (optional Hot/Cold wallet rule) |
| **Cleanse** | Defueled / future cleanse removes counter |

**Matrix fit:** Glitched → Block `Cascade Failure x1` — signature “exploit the wall” without instant kill.

**Engine follow-up:** Wire reset in `ResolveSwap` + document in `COMBAT_Statuses.txt`.

---

### 3.11 Isolated — define support block clearly

**Problem:** “Cannot receive ally Support moves” needs resolver path.

**Proposal — `Isolated` (Duration 2, debuff)**

**Blocked when target has Isolated:**

- `MoveCategory.Support` with `MoveTargeting.SingleAlly` / `AllAllies` where target would be recipient
- **AP transfer / heal / stage buff** from ally to isolated target
- **Not blocked:** self-targeted Support, field effects, **Damaging** moves, **passive** team auras if they don’t target recipient

**Allowed:**

- Swapping out (unless also FrozenAssets)
- Receiving **non-Support** effects (ExploitMark, etc.)

**Resolver hook:** In support resolution (`ProcessSupport*` / target validation), early reject with log `StatusImmune` / `Isolated`.

**Matrix fit:** Gas → Staked, Burned → Staked — **Isolated x1** encourages breaking sustain without skipping turns.

**UI:** Icon + tooltip: *“Cannot receive ally support.”*

---

## 4. TCG-style RNG (optional layer)

Use **deterministic `CombatRng`** — same seed = same replays.

| Proc | When | Effect |
|------|------|--------|
| **Coin flip on apply** | Select chemistry statuses (Firewall, FOMO) | Heads: stronger branch; tails: base effect |
| **Coin flip on tick** | Slowed, Rekt-like | 25% = full para skip; 75% = speed cut only |
| **Coin flip on cleanse** | Defueled | 50% remove 2 stacks, 50% remove all |

**Ranked rule:** Display odds in forensic log; cap proc chains at **1 flip per hit**.

**Pokémon TCG parallels:** Burn / poison flip between turns; **Paralyze** flip to attack; **Confusion** self-hit.

---

## 5. Suggested mapping to v1 matrix (high level)

Effects chosen for **feel**, not final balance — stacks stay **×1–×2** unless noted.

| Type role | Prefer on ADV cells | Avoid |
|-----------|---------------------|-------|
| **Burned** (aggressor) | Rugged (Toxic), Backlash, Isolated, Defueled | Firewalled, Halted, Crashed |
| **Shard** (aggressor) | Anchored, Leaked, Drained, Dusted | Narrowcast duplicate, Bridged |
| **Frozen** (control) | Jammed, Slowed | Halted skip turn |
| **Gas** (tempo) | Pumped, Drained, Isolated | Bridged |
| **Glitched** (chaos) | Corroded, Cascade Failure | Crashed |
| **Mined** (ramp) | FOMO (revised), Shattered, Pumped | Crashed 50% |
| **Staked** (wall) | Siphoned, Drip | Amplified on chemistry |
| **Block** (wall) | Anchored debuff on foes | — |
| **Forked** (spread) | Overclocked, Leaked, Backlash | Bridged full damage spread |
| **Encrypted** | Drained, Jammed, Defueled | Pumped mirror |
| **Node** | Leaked, Dusted | — |
| **Bot** | Drained, Jammed, Backlash | — |
| **Compressed** | Dusted, Jammed | — |

**v1.csv quick swaps (authoring intent)**

| Cell | Current | Suggested |
|------|---------|-----------|
| Mined → Block | Crashed ×1 | Shattered ×1 or Corroded ×1 |
| Mined → Frozen | Crashed ×1 | Dusted ×1 |
| Frozen → Staked | Halted ×1 | Slowed ×1 |
| Glitched → Block | Cascade ×1 | Cascade ×1 (with revised KO rule) |
| Shard → Forked | Narrowcast ×2 | Anchored ×1 |
| Burned → Gas | Rugged ×2 | Rugged ×1 or Firewall ×1 |
| *(any)* | Liquidity Pump | Drip ×1 (if kept at all) |

---

## 6. Implementation / resolver checklist

| Item | Status today | Action |
|------|--------------|--------|
| Firewalled projectile block | Enum only | Replace with mitigation opcode |
| Halted skip turn | Wired | Replace with Slowed proc |
| Crashed 50% HP | Wired in `ChemistryResolver` | Replace Shattered math |
| Rugged flat 10% | Wired EoT | Add escalating counter |
| Isolated | Catalog only | Support target filter |
| Cascade swap reset | Not wired | Reset on swap out |
| Narrowcast | Same as Anchored? | Merge IDs, remove duplicate |
| Bridged full spread | Wired at 100% dmg | Splinter split or deprecate |
| Anchored blocks spread | Verify | Block `hitAllFoes` when debuff active |

---

## 7. Recommended next steps

1. **Sign off** on core kit (§2) and flagged redesigns (§3).
2. **Update `Statuses.csv`** — revise rows, add Slowed / Shattered / Drip / Firewall; deprecate Narrowcast, Halted, Crashed, LiquidityPump, Firewalled, Evasive, Bridged (or alias).
3. **Re-author `COMBAT_Interaction_Matrix_v1.csv`** — one primary status per cell; ×1 default, ×2 for signature only.
4. **Resolver + tests** — Toxic Rugged, Splinter split, Isolated support block, Cascade swap reset.
5. **UI families** — map statuses to Liquidity / Volatility / Latency / Structure icons.

---

## 8. Open decisions for team

1. **Slowed mode:** A (speed + 25% skip) vs B (priority) vs C (AP tax)?
2. **Cascade at 3:** 25% MaxHP chunk vs execute-if-low?
3. **Splinter:** keep as chemistry or move spread to Forked General modifier only?
4. **TCG coin flips:** ranked on/off toggle?
5. **Badly Rugged cap:** 15/16 like Toxic or lower cap for shorter matches?

---

## Related docs

- `Docs/Combat/COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md`
- `Docs/Specs/Combat/COMBAT_TypeProfiles.md`
- `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv`
- `Docs/Specs/Combat/Data/Statuses.csv`
- `Docs/Specs/Combat/COMBAT_Statuses.txt`
