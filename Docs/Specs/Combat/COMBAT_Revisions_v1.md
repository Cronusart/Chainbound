# COMBAT_Revisions_v1

> Combat mechanics revisions: tags, barriers, terminology, position swap, and bypass sources.
> Ready for planning and implementation.

---

## 1. Tag Taxonomy for Move Filtering

### Current Tags (Retained)

| Tag | Meaning | Example Moves |
|-----|---------|---------------|
| DMG | Damaging | baseline_striker, coordinated_shot |
| MULTI | Multi-hit | baseline_multi_strike, stone_barrage |
| AOE | Hits all foes/allies | fork_split, rally_cry |
| PRIO | Priority (speed-based, goes first) | rapid_strike, baseline_jab |
| TRIG | Triggered/conditional | momentum_shift, reactive_ping |
| APMAN | AP manipulation | ap_drain, ap_donation |
| STAT | Stat-based damage | crystal_bash |
| UTIL | Status/utility | power_surge, cleanse |
| SUP | Support (buff/heal/barrier/redirect/position swap) | heal_ally, barrier_wall, damage_intercept, position_swap |
| PAS | Passive | sustain_regen |
| HEAL | Heals HP | heal_ally |

### Refinements

- **APXFER** → falls under **APMAN** (ap_donation, ap_redistribution, ap_router)
- **EFFXFER** → falls under **UTIL** (effect_relay, relay_packet)
- **REDIRECT** → falls under **SUP** (damage_intercept, signal_redirect)
- **WALL** → falls under **SUP** (barrier_wall, vapor_barrier, blockade, etc.)
- **POSSWAP** → falls under **SUP** (position_swap)

### New Tag: PRIOR (Priority)

Add **PRIOR** (or similar short form for "priority") as a strategic tag for moves that participate in the priority block of turn order. This enables:

- Position swap to be counterable by faster enemy priority moves
- Priority as a meaningful strategic facet of combat
- Filtering in the move UI for "priority" moves

**Note:** PRIO and PRIOR may be consolidated—PRIO already exists for moves like rapid_strike. The distinction: PRIOR could be the canonical tag for "participates in priority block," with PRIO as an alias or legacy name. Implementation can use either; the spec uses **PRIOR** for clarity.

### Tag Categories for UI Filtering

| Category | Tags |
|----------|------|
| **Offense** | DMG, MULTI, AOE, PRIO, STAT |
| **Support** | SUP, HEAL, APMAN |
| **Utility** | UTIL |
| **Passive** | PAS, TRIG |
| **Strategic** | PRIOR |

---

## 2. Barrier System: Definitions & Behaviors

### Barrier Types

| Type | Behavior | Example Moves |
|------|----------|---------------|
| **Single-hit** | Blocks one hit entirely, then expires | vapor_barrier |
| **Damage-threshold** | Absorbs X damage before breaking | lattice_guard (40 dmg absorb) |
| **Duration** | % mitigation for N turns | barrier_wall, blockade, ice_wall, compound_guard |

### Key Definitions

1. **Single-hit barriers**
   - One barrier = one hit blocked entirely.
   - Multi-hit attacks: each hit consumes one barrier stack.
   - Example: baseline_multi_strike (2–5 hits) can break 2–5 single-hit barriers.

2. **Damage-threshold barriers**
   - Absorb up to X damage before breaking; overflow hits the defender.
   - Multi-hit: damage accumulates against the threshold.

3. **Duration barriers**
   - % mitigation for N turns; stack multiplicatively with other mitigation sources.
   - Per COMBAT_02C §8: single-source cap 30%, multiplicative stacking.

### Barrier Variety

Differing barrier behaviors make defensive moves feel as varied and valuable as attack moves. This also opens design space for **move modifiers** that integrate barrier behaviors (e.g. % chance to apply barrier on hit, scaled by rarity)—to be explored later.

---

## 3. Bypass Sources (No Chemistry Changes)

Bypass effects allow moves to ignore or partially bypass barrier absorption. **Do not modify the chemistry reaction matrix.** Bypass comes from:

### A. Move Modifiers

- Modifier 4.3-b already: "Priority + bypass barriers first hit (1-2 AP DMG moves)"
- Future modifiers: % chance to bypass single-hit barriers, scaled by rarity
- Applied to the move instance at resolution time

### B. Support-Type Status Effects

- New General statuses applied by moves (not chemistry)
- Examples:
  - **Unsealed**: X% chance to bypass single-hit barriers (e.g. 50%)
  - **Relay** (or similar): Damage "relays" past barrier—full bypass for that hit
- Applied via support moves that grant these statuses to the attacker or their team

### Implementation Note

Split bypass sources between:
1. **Move modifier** — attached to inscription, fires when that move is used
2. **Status effect** — applied to chainling, checked when resolving damage against barriers

---

## 4. Terminology: Ally / Partner / Team

### Definitions

| Term | Meaning |
|------|---------|
| **Partner** | The other active ally (slot 0 or 1 when self is excluded) |
| **Team** | Self + partner (both active allies) |
| **Active ally** | Partner, when self is excluded |

### Updates

- Replace "per ally" with **"per partner"** or **"per active ally"** in move descriptions
- Replace "per buffed ally" with **"per buffed partner"** in moves and modifiers
- Modifier 7.1-d already uses "partner"—keep as reference
- AllAllies = team (self + partner)

---

## 5. Position Swap: No Separate Phase

### Slot-Based Targeting (Already Correct)

- Attacks target **slots**, not chainlings.
- `GetActiveInSlot(slot)` resolves at execution time.
- After a position swap, the slot still receives the attack; the occupant may have changed.

### Turn Order: Priority Block, Not Position-Swap Phase

- **No** dedicated position-swap phase.
- Position swap uses **PRIOR** (or PRIO) and participates in the **priority block**.
- Priority block: all actors using priority moves, sorted by **speed**.
- Combat block: all actors using non-priority moves, sorted by **speed**.
- Priority block executes before combat block.

### Counterplay

- Enemy priority moves that are **faster** (higher Speed) can act before position swap.
- Example: Enemy rapid_strike (PRIOR, high Speed) resolves before our position_swap (PRIOR, lower Speed).
- This keeps position swap strong but counterable.

### Current System Note

The current battle system does **not** have a priority-move phase. Turn order is speed-only. Implementation will add:

1. Partition queue into **priority block** (PRIOR moves) and **combat block** (non-priority)
2. Sort each block by speed (desc)
3. Execute: swap phase → priority block → combat block

---

## 6. Summary of Changes

### Data / Spec

| Area | Change |
|------|--------|
| **Tags** | Add PRIOR; keep APXFER/EFFXFER/REDIRECT/WALL/POSSWAP under existing tags (APMAN, UTIL, SUP) |
| **Barrier definitions** | Formalize single-hit, damage-threshold, duration in COMBAT_Moves / COMBAT_Resolution |
| **Bypass** | Move modifiers + General status effects (Unsealed, Relay); no chemistry changes |
| **Terminology** | "per ally" → "per partner" / "per active ally" in Moves.csv, Modifiers.csv, _LEGEND.md |
| **Position swap** | PRIOR tag; priority block sorted by speed; no separate phase |

### Engine

| Area | Change |
|------|--------|
| **Turn order** | Add priority block (PRIOR moves) before combat block; both sorted by speed |
| **Barrier resolution** | Implement single-hit and damage-threshold barriers in damage pipeline |
| **Bypass** | Check attacker status + move modifiers; skip barrier consumption when bypass applies |
| **Position swap** | Implement position_swap move; runs in priority block |
| **AP transfer** | Implement ap_donation, ap_redistribution, ap_router |
| **Damage redirect** | Implement damage_intercept, signal_redirect |
| **Buff/debuff transfer** | Implement effect_relay, relay_packet |
| **% heal** | Extend HEAL tag to support % MaxHP (e.g. heal_ally 25% MaxHP) |

### Move Tag Assignments

- **position_swap** → PRIOR (and SUP)
- **rapid_strike, baseline_jab** → PRIOR (and DMG)
- Barrier moves remain SUP (no separate WALL tag)

---

## 7. Suggested Implementation Order

1. **Terminology** — Update Moves.csv, Modifiers.csv, _LEGEND.md
2. **PRIOR tag** — Add to _LEGEND.md; assign to position_swap, rapid_strike, baseline_jab
3. **Turn order** — Implement priority block in BuildActionQueue
4. **Position swap** — Implement position_swap move effect in ResolveUtilityMove
5. **Barrier system** — Single-hit and damage-threshold barriers in damage pipeline
6. **Bypass** — Modifier 4.3-b + status effects (Unsealed, Relay)
7. **AP transfer** — ap_donation, ap_redistribution, ap_router
8. **Redirect & transfer** — damage_intercept, signal_redirect, effect_relay, relay_packet
9. **% heal** — Extend HEAL tag for % MaxHP

---

*End of COMBAT_Revisions_v1*
