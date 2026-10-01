# Chemistry Matrix Pass Plan

> **Goal:** Replace the legacy interaction matrix with a **PvP-readable, mutual-free** type chart where **ChemAdv** (×1.2 per stack) and **ChemDis** (×0.8 per stack) are balanced by type role — then map **Market Condition** statuses onto each directed edge.
>
> **Source of truth for type directions:** `Docs/Specs/Combat/COMBAT_TypeProfiles.md` (updated Pass 1).
>
> **Out of scope this pass:** Meme transmutation kit, move data, resolver code changes (unless resist authoring proves too confusing).

---

## 1. What we are building

### Player-facing read

- **Super effective:** typed hit applies **ChemAdv** (+ optional short status).
- **Not very effective / resisted:** typed hit applies **ChemDis only** (damage dampener, no status from resist path).
- **Neutral:** no chemistry modifier on that hit (beyond normal damage formula).

### Design constraints (locked)

| Rule | Detail |
|------|--------|
| No mutual ChemAdv | Never both `A → B` and `B → A` forward cells |
| One outcome per pair | ChemAdv forward **or** ChemDis reverse **or** neutral |
| Role budgets | See `COMBAT_TypeProfiles.md` - Aggressor forward / Net 0 / Wall forward targets |
| Meme excluded | 13-type grid; Meme handled in a later wildcard pass |
| Status pass last | Pick Pumped vs Rugged vs Jammed **after** directions are frozen |

### Runtime mapping (unchanged)

```
On damaging hit (per defender type):
  1. Forward GetReaction(attackType, defenderType) → statuses + ChemAdv stacks
  2. Else reverse GetReaction(defenderType, attackType) → ChemDis stacks only
  3. Damage *= 1.2^ChemAdv × 0.8^ChemDis
```

**Authoring note:** resist when **Gas attacks Block** uses matrix cell **Block → Gas** (defender type first in reverse lookup). Pass 2 grid should mark resist cells explicitly to avoid row/column confusion.

---

## 2. Workflow overview

```mermaid
flowchart LR
  P1[Pass 1 Type Profiles] --> P2[Pass 2 Adv Grid]
  P2 --> P3[Pass 3 Dis Grid]
  P3 --> P4[Pass 4 Status Mapping]
  P4 --> P5[Pass 5 CSV + Asset Sync]
  P5 --> P6[Pass 6 Playtest + Stack Tune]
```

| Pass | Deliverable | Status |
|------|-------------|--------|
| **1** | `COMBAT_TypeProfiles.md` — roles, ChemAdv/ChemDis lists, no mutuals | **Done** |
| **2** | Direction-only **ChemAdv grid** (13×13, Meme excluded) | **Done** |
| **3** | **ChemDis grid** (reverse-lookup resist texture) | **Done** |
| **4** | Status + stack count per forward cell | **Done** — v2 effect suggestions are parked in [`COMBAT_CHEMISTRY_REACTION_REDESIGN_SUGGESTIONS.md`](../review-unimplemented/Combat/COMBAT_CHEMISTRY_REACTION_REDESIGN_SUGGESTIONS.md), not the live chart |
| **5** | New CSV spec + `TypeChemistryMatrix.asset` import | **Done** |
| **6** | PvP mirror tests, stack normalization | Initial tests added |

---

## 3. Pass 2 — ChemAdv direction grid

**V1 result:** `COMBAT_Interaction_Matrix_v1_directions.csv` contains 57 non-Meme directed ChemAdv cells, zero mutual advantages, and no Meme row/column entries. The chart intentionally keeps chemistry density lower while preserving crypto-readable loops such as Node -> Frozen -> Bot -> Node, Gas -> Block -> Burned -> Gas, Mined -> Block -> Forked -> Mined, Encrypted -> Bot -> Glitched -> Encrypted, and Staked -> Shard -> Mined -> Staked.

### Objective

Produce a **blank winner matrix**: each cell is `A`, `B`, or `—` (no effect names yet).

### Steps

1. **Export pair list** from `COMBAT_TypeProfiles.md` columns **ChemAdv →** and **ChemAdv ←**.
2. **Build 13×13 grid** (rows = attacker, cols = defender). Mark `●` where profile says attacker beats defender.
3. **Validate automatically:**
   - No cell where both `(A,B)` and `(B,A)` are marked.
   - Row counts match role targets (±1 tolerance).
   - Column counts match role targets (±1 tolerance).
   - Total forward cells ≈ **52-60**.
4. **Fix drift:** if counts fail, adjust **neutral pairs** first, then swap single edges — never add mutuals.
5. **Save** as `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1_directions.csv` (cells: `-`, `ADV`, or empty).

### Concrete ideas

- Use **triangles** for readability (e.g. Gas → Block → Mined → Gas) instead of random edges.
- Keep **Burned** and **Shard** as the only types with **6+** forward edges.
- Keep the starter trio readable: **Node -> Frozen -> Bot -> Node**.
- Keep **Block** and **Staked** as the only types with a clear wall-forward deficit.
- Keep **Forked** near parity but low density so spread fantasy does not become v0 OC spam.

---

## 4. Pass 3 — ChemDis (resistance) grid

### Objective

Add **soft counters** and **wall texture** without duplicating ChemAdv.

### When to assign ChemDis

| Situation | Use ChemDis? |
|-----------|----------------|
| A beats B (forward Adv) | **No** Dis on same directed attack |
| A neutral into B, but should feel “not very effective” | **Yes** — Dis only |
| Wall holding vs chip types | **Yes** — ChemDis ← on wall row |
| Aggressor | **Few** Dis → and Dis ← |

### Steps

1. Start from Pass 2 grid.
2. For each **neutral** pair that should still feel bad offensively, mark **Dis** on one direction only.
3. For **Block** and **Staked**, add **3–6** ChemDis ← entries each (types that **don’t** already have ChemAdv into the wall).
4. Verify aggressors have **≤3** ChemDis entries total (→ and ← combined).
5. Save as `COMBAT_Interaction_Matrix_v1_resists.csv` or extra column in directions file (`ADV` / `DIS` / `-`).

### Concrete ideas

- **Block ChemDis ←:** Bot, Node, Encrypted *(only where Encrypted does not already beat Block)*.
- **Staked ChemDis ←:** Compressed, Mined *(patient wall absorbs bad trades)*.
- **Do not** give Shard wide ChemDis ←; Shard’s weakness is **four hard ChemAdv counters**, not universal resist into it.

---

## 5. Pass 4 — Status and stack mapping

### Objective

Attach **Market Condition** effects to each **ADV** cell; attach **stack counts** (×1–×3) for PvP balance.

### Principles

| Role | Stack guidance |
|------|----------------|
| Aggressor forward | ×2–×3 on signature matchups; ×1 on filler |
| Net 0 | Mostly ×1–×2 |
| Wall forward | ×1–×2 (walls don’t blow out typings) |
| Dis-only | Stack count only (no status apply) |

### Steps

1. For each **ADV** cell, pick **one primary** chemistry status from the ChemistryOnly catalog (`Statuses.csv`).
2. Map v0 effects **only where direction still matches** after Pass 2; otherwise pick a new effect from the same **family** (Liquidity / Volatility / Latency / Structure / Exposure).
3. **Prune** over-powered kits on multi-stack cells (e.g. Rugged ×3 + Isolated ×1 → pick one primary + lower stacks).
4. Document in `COMBAT_Interaction_Matrix_v1.csv` with columns: `Attacker, Defender, StatusId, Count, ParameterType?`.
5. Update `COMBAT_Chemistry.txt` locked PvP adjustments if Block→Fork Drained ×3 etc. change.

### Concrete v0 → v1 carryover candidates

| Matchup (if direction kept) | Legacy effect | Notes |
|----------------------------|---------------|-------|
| Glitched → Node | Corroded ×2 | Keep — signature counter |
| Gas → Block | Pumped ×3 | Consider ×2 for PvP |
| Shard → Forked | Narrowcast ×3 | Keep — Shard identity |
| Burned → Staked | Rugged + Isolated | **Split** — one effect per cell |

---

## 6. Pass 5 — Data sync and tooling

### Objective

Runtime matches spec.

### Steps

1. Replace `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v0.csv` with v1 (or version alongside).
2. Run / extend combat data importer → `Assets/Data/Combat/Type/Chemistry/TypeChemistryMatrix.asset`.
3. **Diff report:** list cells in asset missing from CSV (known v0 gaps: Node→Shard, Forked→Gas OC, etc.).
4. Update `COMBAT_TypeProfiles.md` **Signature effect** column (optional) once mapping is stable.
5. Add editor validation: **fail import if mutual ADV detected**.

---

## 7. Pass 6 — Balance verification

### Objective

Confirm competitive feel before content lock.

### Checks

| Check | Method |
|-------|--------|
| Mutual Adv | Importer unit test |
| Role counts | Script sums rows/columns vs profile targets and net disparity |
| Damage swing | Mirror match: same stars, typed vs neutral hit, ChemAdv 1 stack = ×1.2 |
| Dis only | Gas→Block resist cell: no status applied, ChemDis > 0 |
| Dual-type | Defender with two types sums chemistry per type |
| No Meme | Meme row/col empty in ranked ruleset |

### Playtest scenarios

1. **Node vs Frozen vs Bot** — starter triangle readable in UI.
2. **Burned vs Glitched** — one-way counter readable in UI.
3. **Shard vs Forked** — Shard wins; Forked does not also win into Shard.
4. **Block vs Gas** — single direction only (Gas beats Block **or** Block resists Gas, not both Adv).
5. **Staked wall** — limited forward chemistry, defined bad matchups.

### Stack normalization (if needed)

- Cap **ChemAdv stacks per hit** at 3 for ranked (design option).
- Reduce global ×3 defaults to ×2 for aggressor filler cells.

---

## 8. Open questions (resolve before Pass 4)

1. **Ranked ruleset:** Meme banned vs Meme restricted to casual?
2. **Dual-type stacking:** sum stacks from both types or cap per hit?
3. **Explicit resist marker in CSV** vs inferred reverse lookup — recommend **explicit `DIS` column** for author clarity.
4. **Shard fantasy name:** keep “precision aggressor” in UI or rename role tag?

---

## 9. File checklist

| File | Action |
|------|--------|
| `Docs/Specs/Combat/COMBAT_TypeProfiles.md` | ✅ Pass 1 updated |
| `Docs/Combat/COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md` | ✅ This doc |
| `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1_directions.csv` | Added |
| `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv` | Added |
| `Assets/Data/Combat/Type/Chemistry/TypeChemistryMatrix.asset` | Synced to v1 |
| `Docs/Specs/Combat/COMBAT_Chemistry.txt` | Pass 4–5 sync |
| `Docs/Specs/Combat/COMBAT_Resolution.txt` | Already documents 1.2 / 0.8 |

---

## 10. Success criteria

- [ ] Every non-Meme type fits its **matrix role** counts (±1).
- [ ] **Zero** mutual ChemAdv pairs in ranked matrix.
- [ ] Walls have **more ChemDis ←** than aggressors.
- [ ] Aggressors have **most ChemAdv →** without also being weak to the same targets.
- [ ] Importer and asset **byte-match** the v1 CSV.
- [ ] One internal playtest session confirms counters feel **Pokémon-clear** (I beat you / you beat me, not both).
