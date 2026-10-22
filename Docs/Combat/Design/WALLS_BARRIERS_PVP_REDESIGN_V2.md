# Walls & Barriers: decided rules and implementation plan

> **Status: DECIDED, implementation-ready.** Cronus approved these rules on 2026-10-20, then sent two corrections (block denial at target selection; 2-turn duration) and one design acceptance (near-continuous coverage). Everything in section 1 is a design decision. Details Cronus left open carry a default marked **(default, accepted unless Cronus objects)**.
> **Codex plan:** `walls_barriers_tiered_v1.plan.md` (same folder) turns section 5 into phased work.
> **Snapshot:** engine mirror `/workspace/balance/s10`. **Sim:** `walls_pvp_v2_sim.py`, an extension of the unchanged `tempo_2v2_sim.py`.
> - 4000 battles per cell, 31 cells, random IVs (±5).
> - Real learn-pool kits, type chemistry, passives, statuses, AP denial, swaps and focus fire.
> - All designs share seeds, so columns are directly comparable.
>
> **Checker:** `walls_pvp_v2_check.py`; result in section 11.
> **Hard rules kept:**
> - No move changes rarity, type, or learn-pool membership or tier (`learn_pool_rarity_6d7ff36f`). Renames change the display name only; the `MoveId` stays.
> - Typed moves beat typeless ones.
> - No self-damage.
> - Block stays the barrier type.
>
> **Supersedes:** the PROPOSAL "P" version of this doc (full block, flat 2 AP, Recharge 2) and the first decided round (blocks lasting "this turn", cancel-on-resolution). Both are kept only as comparison columns (`P`, `F`).

## 1. Decided rules (approved by Cronus 2026-10-20)

1. **Walls block MELEE only. Barriers block RANGED only.** Ember Wall is a melee Wall and has no dual-category perk.
2. **Three tiers.** A block mitigates a percentage of the **one hit** it blocks.

   | Tier | Mitigation | Priority | AP | Role |
   |---|---|---|---|---|
   | Lower | 10–30% | almost always [PRIOR] | 1–2 | quick, cheap answer |
   | Mid | 30–50% | optional, depends on the effects | 2–3 | "utility" blocks: about half, plus an effect |
   | Greater | 50–75% | rarely | 3–4 | damage mitigators on tanky / guardian profiles. Turn-1 use usually needs an AP setup. |

3. **Every Wall/Barrier blocks exactly 1 hit.** A multi-hit breaks it on its first hit, and the remaining hits land normally.
4. **Duration: until it blocks one hit, or 2 turns (the cast turn and the next turn), whichever comes first.** Authored as a 2-turn window with a 1-hit budget. *(Correction 2, 2026-10-20. Replaces "this turn only".)*
5. **Recharge: 1 turn for every Wall/Barrier.** Authored `RechargeTurns = 2`, because the authored number counts the cast turn's end tick. Cast on turn N, usable again on turn N+2.
6. **Accepted design consequence: near-continuous coverage.** A 2-turn block plus a 1-turn recharge lets one caster keep a block up on most turns. Cronus accepted this on 2026-10-20: each block is one hit at a fractional %, so a waller still loses to sustained attacks. The sim reports the numbers (section 4) and flags only a **true lock** (battles at the 30-turn cap, or a line the attacker can never win). There is none (section 4.1).
7. **Priority hits do not break walls.** A block still mitigates its % against a priority hit that arrives after it is up. Ranged priority attacks are wanted later and are **out of scope** this round.
8. **Renames** must fit the logic (Wall = melee, Barrier = ranged):
   - Barrier Wall → **Buffer Barrier**
   - Compound Guard → **Compound Barrier**
   - Ash Guard → **Ash Barrier**
   - Lattice Guard → **Lattice Barrier**
9. **One active block per Chainling, enforced at target selection.** *(Correction 1, 2026-10-20.)* A blocked cast never reaches turn resolution.
   - A Chainling **holds a block** while it has an intact Wall/Barrier from any source (not yet consumed, not yet expired).
   - In target selection, a Wall/Barrier move cannot target a Chainling that holds a block. That target is greyed out and the cursor skips it.
   - **Same-turn allies:** a Chainling already chosen as the target of a Wall/Barrier by a teammate's queued action this turn counts as holding one. The second ally sees it greyed out with "<Chainling> is already getting a block."
   - If every target of the move is invalid, the move is not selectable. The player stays on (or returns to) the MoveSelectPanel, no AP is spent, and the move row shows "Every target already has a block."
   - The AI uses the same check. The Unity resolver and the battle service validate it too (section 5.2 E4, 5.5).
   - Blocks granted any other way (passives, modifiers/stamps) **override** an intact block.
   - A **broken or expired** block no longer counts, so the Chainling is a valid block target again from the next selection on.
10. **Ice Wall keeps its AP gain,** capped at **1 AP** for the one hit it blocks.
11. **No flat AP.** Each move gets a tier and an AP cost inside its tier range, chosen by rarity, type profile and existing effects. Block-type and guardian/tanky moves get Greater.
12. **Guard Posture leaves the family** and becomes the first **Armor** move (section 3). Its numbers are unchanged until the Armor round.

**Defaults from the first decided round, now accepted** (Cronus accepted the other defaults with the corrections):
- Ember Wall = Mid 50%, [PRIOR], 2 AP, keeps its 1 AP drain.
- **Blockade = Greater with [PRIOR].** Without PRIOR it is still a dead card under the 2-turn rule (section 4.2).
- A mitigated hit still applies its riders, statuses and chemistry.

**New defaults for the open items (default, accepted unless Cronus objects):**
- **D1. Wallbreaker (4.3-b) and Ignore Walls (4.8-a)** also bypass Walls/Barriers: the stamped attack's first hit ignores the block's % and **does not consume it**. They do not bypass Armor. Today both bypass only `BarrierInstance` barriers, and the family will no longer use those.
- **D2. Core Breach (`ignoreMitigation`)** ignores Walls/Barriers **and** Armor, and consumes neither. That is what `ignoreMitigation` means today (`DamageCalculator.cs:179`). Core Breach stays catalog-only.
- **D3. No Greater-tier Barrier** this round. Ranged tanks keep Lower/Mid barriers.
- **D4. Compound Barrier's Melee Def +1 stacks on recast** under the normal stage rules and stage cap. A cast denied at selection never happens, so it grants nothing.

## 2. Final per-move table

How to read it:
- Recharge is "card / authored `RechargeTurns`".
- Every block lasts until it blocks 1 hit or until the end of the next turn (2 turns).
- Every move keeps its MoveId, BaseType, Rarity, Targeting (`SingleAlly`, self allowed) and learn pools.
- "Effects" are in addition to the block.

| MoveId | Name | Kind | Tier | % | AP (s10 → new) | PRIOR | Recharge | Effects | Type / Rarity (unchanged) | Learned by (unchanged) |
|---|---|---|---|---|---|---|---|---|---|---|
| `ember_wall` | Ember Wall | Wall (melee) | Mid | 50% | 2 → 2 | Y | 1 / 2 | The melee attacker whose hit it mitigates loses 1 AP | Burned / Legendary | Cinislug (signature) |
| `blockade` | Blockade | Wall (melee) | Greater | 60% | 3 → 3 | Y (accepted default) | 1 / 2 | none | Block / Uncommon | Blockwyrm |
| `ice_wall` | Ice Wall | Wall (melee) | Mid | 40% | 3 → 2 | Y | 1 / 2 | If the protected Chainling is faster than the attacker, it gains 1 AP (max 1) | Frozen / Uncommon | Gilarime |
| `barrier_wall` | **Buffer Barrier** | Barrier (ranged) | Lower | 25% | 3 → 1 | Y | 1 / 2 | none | None / Uncommon | Humpacked |
| `vapor_barrier` | Vapor Barrier | Barrier (ranged) | Lower | 30% | 2 → 1 | Y | 1 / 2 | none | Gas / Uncommon | (none) |
| `compound_guard` | **Compound Barrier** | Barrier (ranged) | Mid | 35% | 3 → 2 | Y | 1 / 2 | Target gains Melee Def +1 (stacks on recast, D4) | Staked / Uncommon | Rattlestake, Cofi |
| `ash_guard` | **Ash Barrier** | Barrier (ranged) | Mid | 35% | 2 → 2 | Y | 1 / 2 | Target heals 5% max HP | Burned / Uncommon | (none) |
| `lattice_guard` | **Lattice Barrier** | Barrier (ranged) | Mid | 40% | 1 → 2 | Y | 1 / 2 | The attacker takes 20% of the damage this barrier prevented | Shard / Uncommon | (none) |
| `guard_posture` | Guard Posture | **Armor** | n/a | 25% next hit (unchanged) | 1 → 1 | N | none | Stacks with a Wall/Barrier | None / Common | (none) |

**Exact card text** (every limit stated):

| MoveId | Card text |
|---|---|
| `ember_wall` | **Mid Wall · 2 AP · [PRIOR] · Recharge 1.** The next melee hit on an ally or yourself deals 50% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Ranged attacks are not blocked. The attacker loses 1 AP. |
| `blockade` | **Greater Wall · 3 AP · [PRIOR] · Recharge 1.** The next melee hit on an ally or yourself deals 60% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Ranged attacks are not blocked. |
| `ice_wall` | **Mid Wall · 2 AP · [PRIOR] · Recharge 1.** The next melee hit on an ally or yourself deals 40% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Ranged attacks are not blocked. If the protected Chainling is faster than the attacker, it gains 1 AP (max 1). |
| `barrier_wall` | **Lower Barrier · 1 AP · [PRIOR] · Recharge 1.** The next ranged hit on an ally or yourself deals 25% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Melee attacks are not blocked. |
| `vapor_barrier` | **Lower Barrier · 1 AP · [PRIOR] · Recharge 1.** The next ranged hit on an ally or yourself deals 30% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Melee attacks are not blocked. |
| `compound_guard` | **Mid Barrier · 2 AP · [PRIOR] · Recharge 1.** The next ranged hit on an ally or yourself deals 35% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Melee attacks are not blocked. The target also gains Melee Def +1. |
| `ash_guard` | **Mid Barrier · 2 AP · [PRIOR] · Recharge 1.** The next ranged hit on an ally or yourself deals 35% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Melee attacks are not blocked. The target also heals 5% of its max HP. |
| `lattice_guard` | **Mid Barrier · 2 AP · [PRIOR] · Recharge 1.** The next ranged hit on an ally or yourself deals 40% less. Lasts until it blocks 1 hit or until the end of next turn. Blocks 1 hit; a multi-hit breaks it on the first hit. Melee attacks are not blocked. The attacker takes 20% of the damage this barrier prevented. |
| `guard_posture` | **Armor · 1 AP.** Reduce the next hit by 25%; lasts 1 hit or 2 turns. Stacks with a Wall or Barrier. *(numbers unchanged; reviewed in the Armor round)* |

Every block card adds the shared line: "Only one Wall or Barrier can be on a Chainling at a time."

**Why each tier** (rarity, type profile, existing effect):
- **Ember Wall: Mid.**
  - Cinislug is not a tank (HP 134, MeleeDef 54). The drain is a real effect, which makes it a "utility" block.
  - It must have PRIOR. In the first decided round's sensitivity without it (FEg: Greater 60%, 3 AP, no PRIOR), Seam Cut was back to 92% turn-1 KOs, and the self-Ember stall line cast 0.04 blocks a battle.
  - It stays the highest-rarity move in Cinislug's pool. Rarity is unchanged.
- **Blockade: Greater.**
  - Block type, and Blockwyrm is the tank (HP 168, MeleeDef 86).
  - 3 AP, so it cannot be cast on turn 1 without setup. That is the intended Greater-tier trade.
- **Ice Wall: Mid.** Gilarime is tanky-ish (HP 161), and the AP rider is the "utility".
- **Buffer Barrier: Lower.** It is typeless, so it sits below the typed Vapor Barrier (25% vs 30% at the same AP).
- **Vapor Barrier: Lower.** Gas is evasive, not tanky. It has no pool.
- **Compound, Ash and Lattice: Mid.** Each keeps its existing effect (Melee Def +1, 5% heal, 20% reflect).
- **No Greater Barrier exists** (D3).

## 3. Armor (new move kind)

**For players:**
- **Walls and Barriers** stop part of **one** attack of **one** category, for up to 2 turns. You can have only one at a time.
- **Armor** is flat mitigation that works against **any** category and **stacks** with a Wall or Barrier.
- When both apply, they multiply. A 25% Armor plus a 60% Wall leaves 0.75 × 0.40 = 30% of the hit.

**This round:**
- Guard Posture becomes Armor. Its numbers stay the same (25%, next hit, lasts 1 hit or 2 turns, 1 AP, no priority).
- Its stack key moves from `wall_guard_posture` to `armor_guard_posture`, so the wall UI and the single-active rule stop treating it as a wall.
- Armor is not a block: it never greys out a block target, and a block never greys out Armor.
- That is the only Armor move this round.

**Queued for a separate Armor round** (not designed here):
- How Armor stacks with Walls/Barriers: multiply, or add with a cap.
- A **% floor** for the combined block + Armor. The slowest stacked line, HS-B, is GP Armor 25% stacked with Blockade 60% (section 4.1).
- Which existing moves and modifiers become Armor or stay plain mitigation. Candidates:
  - the passives and stamps 6.4-a Brace, 6.4-d Cover Ally, 6.4-g Fade Guard, 7.1-b Shield Link, 6.3-c Grit Guard and 6.8-e Sparse Ward;
  - Chemistry Firewall;
  - Immutable Ledger.

## 4. Sim results under the decided rules

**Designs:**
- `none` = no wall moves.
- `engine` = today's s10 data.
- `P` = the superseded proposal.
- `F` = the first decided round: the section 2 table, but blocks last **this turn only** and a second block is **cancelled on resolution**.
- **`C` = the decided rules in section 1** (2-turn blocks, denial at selection, Blockade [PRIOR]).
- Sensitivities around C:
  - `Cnp`: C, but Blockade without PRIOR.
  - `Crc2`: C with a 2-turn recharge.
  - `Cmax`: every move at the top of its tier (Ember, Ice and Compound 50%; Buffer and Vapor 30%; Blockade 75%).

The sim covers Ember Wall, Blockade, Ice Wall, Buffer Barrier and Compound Barrier: the five family moves any species learns. Vapor, Ash and Lattice are in no pool, so they are not simulated.

**Headline** (target KO % by end of turn 1 / turn 3 · mean KO turn, capped at 12):

| key line | none | engine (today) | P (superseded) | F (1-turn round) | **C (decided)** |
|---|---|---|---|---|---|
| Ember Wall stall: self-Ember Cinislug (STALL) | 0/95 · 2.22 | 0/0 · 12.00 | 0/5 · 4.59 | 0/90.8 · 3.11 | 0/90.6 · 3.35 |
| Greedy waller: wall every legal turn (STALL/greedy) | 0/95 · 2.22 | 0/0 · 12.00 | 0/95.3 · 2.18 | 0/95.5 · 2.31 | 0/3.3 · 7.16 |
| Seam Cut turn 1 → Deghoul, ally Ember (SC) | 91.7/100 · 1.09 | 91.3/96.5 · 1.21 | 0/99.3 · 2.09 | 1/99.5 · 2.05 | 1.4/98 · 2.10 |
| Cryo Ray → Crackett, ally Ember Wall (CR) | 0/93.2 · 2.21 | 0/0.4 · 4.93 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 |
| Cryo Ray → Crackett, ally Buffer Barrier (CR-H) | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/0 · 4.83 | 0/28.3 · 3.76 | 0/27.4 · 3.80 |
| Deep Dig → Keyper, GP Armor + ally Blockade (DD-B) | 0/95 · 2.14 | 0/64.2 · 2.90 | 0/0 · 4.22 | 0/1.6 · 4.18 | 0/1.6 · 4.18 |
| Hammer Sync → Deghoul, GP Armor + ally Blockade (HS-B) | 0/51.6 · 4.23 | 0/5.8 · 6.33 | 0/0 · 6.42 | 0/0 · 6.50 | 0/0 · 6.50 |
| Blade Flurry (3 hits) vs self-Ember (MULTI) | 0/5.4 · 4.49 | 0/0 · 8.54 | 0/0.3 · 4.87 | 0/1.4 · 4.46 | 0/1.3 · 4.47 |
| Crackett vs ally Blockade (CK-B) | 92.7/100 · 1.08 | 92.2/99.8 · 1.09 | 0/99.6 · 2.09 | 92/99.8 · 1.13 | 91.7/99.3 · 1.15 |
| Double coverage: Cryo Ray + Kodiax → Humpacked (DC) | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/97.1 · 2.40 | 0/96.4 · 2.32 | 0/92.4 · 2.40 |
| Crackett retargets while the wall is up (CK-E/rt) | 92/100 · 1.09 | 0/63.9 · 3.40 | 0/93.1 · 2.21 | 0/93.9 · 2.15 | 0/28.6 · 3.90 |
| Priority line: Crackett Quick Jab while the wall is up (CK-E/jab) | 92/100 · 1.09 | 0/0.3 · 4.20 | 0/99.8 · 2.06 | 0/96.6 · 2.09 | 0/24.4 · 4.00 |
| Deep Dig held while a block is up (DD/wait) | 0/94.5 · 2.17 | 0/0 · 5.40 | 0/26.2 · 4.03 | 0/34.2 · 3.82 | 0/0 · 4.68 |
| Battles at the 30-turn cap, worst of 31 cells | 0% | 99.8% | 0% | 0% | 0.1% |
| Attacking-team win %, lowest of 31 cells | 13.8 | 29.3 | 3.9 | 9.9 | 9.5 |

### 4.1 Stall / lock check under the decided rules: **no true lock**

Per rule 6, near-continuous coverage is accepted and is not treated as a problem. This section only looks for a true lock.

- **30-turn cap.** 0% of battles reach it in 30 of the 31 cells under C and every sensitivity. Today's engine reaches it in 99.8% of the Ember stall battles.
- **The one non-zero cell is not a wall lock.** CK-E/deny under C (and Cnp, Crc2, Cmax) has 0.1% of battles at the cap: 4 of 4000. I replayed all 4:
  - Each ends in a Keyper vs Blockwyrm 1v1 that starts by turn 10.
  - No block is cast or up at any point in the capped stretch.
  - The cause is the sim's scripted AP-denial partner: it casts Memory Leak on Blockwyrm every turn and never attacks. The same script runs under F; the 2-turn rule only changes which battles reach that endgame.
  - The attacking team wins 99.8% of this line.
  - No wall fix is proposed. It is noted for an AP-denial review (Q2).
- **The attacker can win every line.** The lowest attacking-team win rate under C is the STALL line at 9.5%. That is the same as F (9.9%), so the 2-turn rule did not cause it, and it is above 0 in every cell.
- **The slowest lines:**
  - **STALL/greedy**, the defender walling every legal turn: mean KO turn **7.16**, 3.3% by turn 3. It is the near-continuous coverage case. The greedy waller spends 6.98 AP a battle on blocks, and the attacking team's win rate **rises** to 56.1% (F 32%), because the waller does not attack. Accepted by rule 6.
  - **HS-B**, GP Armor 25% stacked with Blockade 60% (30% of Hammer Sync gets through): mean **6.50**, identical to F. This is the Armor stack, not the duration. The Armor round's **% floor** is the place to tune it.
- **The old lockstep stays gone.** It came from a 4-AP heavy fired every other turn into a *full* block that was up every other turn. Under the old V1 rules Deep Dig into stacked walls had a mean KO turn of 11.25 with 0% by turn 3. Under C the same line (DD-B) is mean **4.18**, 1.6% by turn 3: the heavy always lands, just smaller.

### 4.2 Blockade still needs [PRIOR] under the 2-turn rule

- **Cnp** (C with Blockade non-PRIOR): Blockade mitigated **0** hits on the target per battle in DD-B, NHF and CK-B (T-ABS).
  - DD-B is 95% by turn 3, the same as no walls. NHF is 94.5% (C 57.1%).
  - Blockwyrm has Speed 24. On turn 1 it has 2 AP and Blockade costs 3. When it does cast, the attacker sees the block and hits the other Chainling.
  - HS-B is the one line where a slow Blockade helps: 0.78 hits a battle, every one of them carried into the turn after the cast.
- **C** (Blockade [PRIOR]): 1.99 mitigated hits per battle in DD-B (1.6% by turn 3, mean 4.18), HS-B 0% by turn 3.
- The first decided round showed the same thing for "this turn" blocks (D: 0 hits in DD-B, NHF 94.8% by turn 3). So [PRIOR] stays the accepted default.
- **Turn 1 is unchanged by design.** At 3 AP Blockade cannot be cast on turn 1 (CK-B 91.7% turn-1 KO under C). That is the Greater-tier "needs an AP setup" trade.

### 4.3 What the 2-turn rule changed (F → C)

- **Naive lines barely move** (within 0.3 of a turn): SC 2.05 → 2.10, DD 4.23 → 4.50, NHF 3.95 → 4.15, CR-H 3.76 → 3.80. MULTI, DD-B, DD-I, HS-B and NCR are unchanged.
  - PS (ranged Partition Shot into Deghoul, ally Ember) moves most: 89.5% by turn 3, mean 1.84 (F 97.5%, 1.65). The attacking team still wins 99.7%.
- **Reads change.** The sim's attacker avoids a block it can see. A block that is not hit now stays up through the next turn, so the attacker avoids it for longer:
  - CK-E/rt (retarget): 28.6% by turn 3, mean 3.90 (F 93.9%).
  - CK-E/jab (priority): 24.4%, mean 4.00 (F 96.6%). Quick Jab lands 1.65 times a battle, 1.14 of them before a same-turn block on the target.
  - DD/wait and HS/wait: 0% by turn 3 (F 34.2% and 18%). Holding the heavy for one turn no longer finds the target open.
  - The attacking team still wins 99.8% of CK-E/rt and CK-E/jab, 94.7% of DD/wait and 68.7% of HS/wait. The damage goes into the other Chainling.
- **Blocks that absorb on the turn after the cast are rare on the target:** at most 0.04 a battle in any C cell. PRIOR blocks resolve before that turn's attacks, so a block that absorbs a hit almost always does it on the cast turn. The second turn mostly works as a deterrent (the reads above).
- **The single-active rule holds:**
  - Turns with both categories covered on the target: **0** in every C cell (P 1.02).
  - Blocked casts that reached resolution: **0** in every C cell, including DC/naive, where the AI plans both blocks. Under F's cancel-on-resolution rule that line had 0.24 cancels a battle.
- **Unchanged from the first decided round:**
  - Seam Cut turn 1 into Deghoul: 1.4% turn-1 KO under C (engine 91.3%, P 0%). 50% off Seam Cut is no longer lethal on turn 1.
  - Ember (melee) does nothing against ranged Cryo Ray: CR 93.3% by turn 3, same as none (93.2%).
  - The right-category Buffer Barrier (25%) holds Crackett to 27.4% by turn 3 (none 93.7%, P 0%).
  - Blade Flurry still counters a wall: MULTI mean 4.47, the same as no walls (4.49). With a multi-hit partner, Crackett's line is 81.9% turn-1 KO (P 0.2%).
  - AP denial on the wall caster does nothing (CK-E/deny mean 2.09, CK-E 2.11).

## 5. IMPLEMENTATION (for Codex)

Scope rules:
- Do not change rarity, type or learn pools.
- Do not touch `Docs/GameTheory/`.
- Keep every `MoveId`. Renames are `DisplayName` only, so saves, pools and `known_pool` stay valid.
- The phased plan with acceptance checks is `walls_barriers_tiered_v1.plan.md`.

### 5.1 Data changes

**`Docs/Specs/Combat/Data/Moves.csv`**, then reimport the `Assets/Data/Combat/Moves/Move_<id>.asset` files. Columns per row:

| MoveId | DisplayName | APCost | RechargeTurns | Tags (add) | UtilityEffects (new) | Other columns |
|---|---|---|---|---|---|---|
| `ember_wall` | Ember Wall | 2 | 2 | `PRIOR` | `BlockWindow:50%:Melee:2:wall_ember\|WallHitRetaliation:1:wall_ember` | Description = card text |
| `blockade` | Blockade | 3 | 2 | `PRIOR` | `BlockWindow:60%:Melee:2:wall_blockade` | Description; asset `wallSfxId: wall_blockade` |
| `ice_wall` | Ice Wall | 2 | 2 | `PRIOR` | `BlockWindow:40%:Melee:2:wall_ice\|GainApOnDamageIfSpeedHigher:1:1:1:wall_ice_ap` | Description |
| `barrier_wall` | Buffer Barrier | 1 | 2 | `PRIOR` | `BlockWindow:25%:Ranged:2:barrier_buffer` | Description |
| `vapor_barrier` | Vapor Barrier | 1 | 2 | `PRIOR` (keep `SUP\|TRIG\|APMAN`) | `BlockWindow:30%:Ranged:2:barrier_vapor` | Description |
| `compound_guard` | Compound Barrier | 2 | 2 | `PRIOR` | `BlockWindow:35%:Ranged:2:barrier_compound` | `StageChanges` unchanged (`MeleeDef:1:1.0:false`) |
| `ash_guard` | Ash Barrier | 2 | 2 | `PRIOR` | `BlockWindow:35%:Ranged:2:barrier_ash` | `HealPercentMaxHP` 5 unchanged |
| `lattice_guard` | Lattice Barrier | 2 | 2 | `PRIOR` | `BlockWindow:40%:Ranged:2:barrier_lattice\|BlockReflect:20%:barrier_lattice` | Description |
| `guard_posture` | Guard Posture | 1 | 0 | `ARMOR` | `MitigationWindow:25%:2:1:armor_guard_posture` | Numbers unchanged; key rename only |

`BlockWindow` arguments: `%`, category (`Melee` / `Ranged`), turns (`2` = the cast turn and the next), stack key. The hit budget is always 1 and is not authored.

**Recharge authoring note.** The authored `RechargeTurns` counts the cast turn's end tick (`FormatPlayerFacingRecharge`, `CombatResolver.cs:525`, `skippedTurns = rechargeTurns - 1`). So:
- "Recharge 1" on the card = **`RechargeTurns = 2`**.
- Authoring 1 means no recharge at all ("can be used again next turn").

**Duration authoring note.** A 2-turn window survives two end-of-turn ticks: the cast turn's and the next turn's. This matches Guard Posture's `MitigationWindow:25%:2:1` ("lasts 1 hit or 2 turns") and the existing `Walls_MitigateForTheirTurnWindowWithUnlimitedHits` test, which ticks a 2-turn source twice before it reads zero.

**Other data:**
- `BlockWindow` and `BlockReflect` are new opcodes (5.2 E1, E9).
  - Tier is derived from the % (Lower ≤30, Mid 31–50, Greater >50). Alternatively add an explicit `BlockTier` column if the catalog wants it.
  - Tie rule: 30% is Lower and 50% is Mid, matching section 2.
- **Supabase generated tables:** regenerate with `CombatServerTablesExporter` (section 5.5).
  - `move_ap.ts` changes for `ice_wall` 2, `barrier_wall` 1, `vapor_barrier` 1, `compound_guard` 2 and `lattice_guard` 2.
  - `move_flags.generated.ts` gains a `BLOCK_MOVE_IDS` set (the eight family moves) for the server check in 5.5.
  - `ownership_card_catalog.generated.ts` (element / basePower) and `known_pool.generated.ts` do not change.

### 5.2 Engine changes

- **E1. Block = a tagged mitigation source.**
  - Add `BlockWindow` to the importer next to `MitigationWindow` (`Combat2DataImporter.cs:1696`). The `MitigationWindow` parser reads only magnitude / turns / hits today and has **no contact field**.
  - `BlockWindow` installs a `MitigationSource` with: magnitude = %, `turnsRemaining = 2` (cast turn + next), `hitsRemaining = 1`, `contactFilter` = Contact for Melee / Projectile for Ranged, and new fields `blockKind` (Wall/Barrier), `blockTier` and `sourceMoveId`.
  - `AddMitigationSource` already takes a contact filter and an attacker drain (`ChainlingInstance.cs:455`).
  - Add `ChainlingInstance.HasIntactBlock()`: true while any source with a `blockKind` has `hitsRemaining > 0` and `turnsRemaining > 0`.
  - Remove `FullAbsorbBarrier` / `DamageThresholdBarrier` from the family moves (Ember, Vapor, Lattice).
- **E2. Melee/ranged.**
  - % sources already honour `AppliesToContact` (`ChainlingInstance.cs:587`).
  - `ApplyBarrierAbsorption` (`ChainlingInstance.cs:874–925`) ignores `BarrierInstance.contactFilter` (`:1014`). Make it read the filter too, so any remaining `BarrierInstance` source respects melee/ranged.
- **E3. One hit, then consumed; else 2 turns.**
  - The block's % applies to the first matching hit that lands. It is consumed by `ConsumeMitigationHitBudgets(move.contactType)`, which already runs per hit (`CombatResolver.cs:2323`).
  - So a multi-hit breaks it on hit 1, and hits 2+ see no block.
  - Misses do not consume it (as today). A block not hit expires at the end of the turn after the cast.
  - Remove a consumed or expired block immediately, so `HasIntactBlock()` is false from then on.
- **E4. Single-active block: denial at selection, never at resolution.**
  - Add one legality check to `CombatActionEligibilityService` (already the "single authority for move/swap legality checks", shared by UI, AI and resolver):
    - `EvaluateBlockTarget(actor, move, target, state, queuedBySlot)` returns `TargetHasBlock` when `target.HasIntactBlock()`, `TargetBlockQueued` when a teammate's queued action this turn is a family move on the same target, else OK.
    - Resolve a queued action's target the way `ResolveCommittedFasterGiftAp` already does (explicit `targetTeamIndex`/`targetSlotIndex`, or the implicit single living ally; `CombatActionEligibilityService.cs:112`, `:184`).
    - `EvaluateMoveSelection` gets an optional `queuedBySlot`. For a family move it returns `canSelect = false`, reason `AllTargetsBlocked`, when no candidate target passes.
  - **UI** (5.3) and **AI** (5.4) call it before an action is queued.
  - **Resolver safety net.** The resolver re-runs the same check where it already validates a move (`CombatResolver.cs:1621`). It checks against the state at the start of the turn plus the same team's actions in this turn's batch. On failure: log `ValidationError`, spend no AP, start no recharge, apply nothing, narrate nothing, and treat the action as a Pass. This path should be unreachable from an honest client. A test pins that it never fires in normal play.
  - Selection-time state decides. A block that will break later this turn does not free the target for a cast chosen this turn.
  - If the teammate's queued block never lands (its caster is KO'd or loses the AP first), the target just has no block this turn. There is no re-selection mid-turn.
- **E5. Passive / modifier override.**
  - Blocks granted by passives or modifiers/stamps use `AddBlock(override: true)`, which replaces an intact block. They do not go through selection.
  - After a block is consumed or expires, a later block from any source applies normally.
- **E6. Priority does not break walls.**
  - No special case: a PRIOR hit that resolves after the block is up is mitigated and consumes it.
  - Turn order stays swaps → priority block → normal → capture, each sorted by Speed (`CombatResolver.cs:863`, `:970`, `:988`).
  - **D1:** 4.3-b Wallbreaker and 4.8-a Ignore Walls make the stamped attack's first hit skip every `blockKind` source's % and leave it unconsumed (today they bypass only `BarrierInstance`, `CombatResolver.cs:7178`). Armor still applies.
  - **D2:** Core Breach's `ignoreMitigation` (`DamageCalculator.cs:179`) already zeroes all mitigation. Also skip `ConsumeMitigationHitBudgets` for that hit, so blocks and Armor survive it.
- **E7. Ice Wall AP, cap 1.** Bind `GainApOnDamageIfSpeedHigher` to the block: grant +1 AP once, when the block consumes, if the protected Chainling is faster than the attacker. Today the reactive window ignores hit budget.
- **E8. Ember drain.**
  - `MitigationSource.attackerApDrainOnHit` exists (`ChainlingInstance.cs:1025`), but only barrier absorption applies drains (`:896`, `:916`).
  - Apply the drain once, when a Wall consumes on a melee hit.
- **E9. Riders on a mitigated hit (accepted default): riders, statuses and chemistry still apply,** because the hit lands for partial damage.
  - `BlockReflect` deals 20% of the prevented amount (unmitigated − mitigated for that hit) to the attacker. That is reflect, not self-damage.
  - Ash's heal and Compound's stage apply on cast. Compound's stage stacks on recast (D4).
- **E10. Swap / KO.** Clear blocks (and all mitigation/barriers) on swap-out and on KO. Do not tick them on the bench (folded item, section 6). A block does not follow its holder to the bench and back.
- **E11. Timer order.** Tick mitigation and barrier timers before `_statusProcessor.ProcessEndOfTurn` (today status ticks run first: `CombatResolver.cs:483` before `:492` / `:495`).
- **E12. Exclusivity.** `exclusiveBarriersAndWalls: 1` (`CombatResolverConfig_Default.asset:31`, `ChainlingInstance.cs:850`) stays for `BarrierInstance`. The single-active rule (E4) governs the family.
- **E13. Wall tag prefix.**
  - `IsWallTag` matches the substring `wall`/`barrier` (`CombatWallStateUtility.cs:119`). That makes `Chemistry.Firewall` count as a wall and hides `blockade`.
  - Match the prefixes `wall_` / `barrier_` and use them as the stack keys above. `armor_` is not a wall.

### 5.3 UI

- **Target selection** (`CombatSelectionCoordinatorUGUI`; `BuildTargetsForMove` at `:857`, `HandleConfirm` at `:300`):
  - Build candidates as today, then mark each one with `EvaluateBlockTarget` (pass the bridge's `_queuedActionsBySlot`, `CombatActionSelectorBridge.cs:47`).
  - Invalid targets stay visible but **greyed**, with a lock badge. Navigation skips them. Confirm on one plays the Invalid cue and shows "<Chainling> already has a block." or "<Chainling> is already getting a block."
  - Exactly one valid target: auto-commit to it as today (no target step).
  - No valid target: the move row is unusable (`AllTargetsBlocked`). Confirm plays `PlayUnavailableMoveFeedback` (`:327`) and the player stays on the MoveSelectPanel with "Every target already has a block." No AP is spent and no action is queued.
  - **Back-navigation.** If the player goes back to slot 0 and queues a block onto the target slot 1 had chosen, slot 1's queued block becomes invalid. Clear it and reopen slot 1's selection.
- **Icons.** Each move gets a kind icon (Wall = melee shield, Barrier = ranged shield, Armor = plate) and a tier pip (Lower / Mid / Greater). The existing PRIOR pill shows on every family move (`MoveSelectDisplayUtility.cs:199–213`).
- **Status chip on the holder:** e.g. "WALL 50% · MELEE · 1 hit · 2 turns", counting down to "1 turn"; "BROKEN" when consumed.
- **Narration:**
  - "<attacker> broke <Chainling>'s <move> (−<n>)" on consume;
  - "<Chainling>'s <move> faded" when it expires unhit;
  - a line when DoT ticks while a block is up (DoT ignores blocks).
  - There is **no** cancel line any more. A denied cast never reaches resolution.
- **Blockade visibility fix.**
  - Its stack key is `blockade`, so wall UI, popup and narration never see it. Its `wallSfxId` is empty.
  - Fix with `wall_blockade` plus an sfx id (folded item).
  - All family `wallSprite` slots are unauthored; art can follow.

### 5.4 AI

- **Legality.** `AIHelpers` already filters moves through `EvaluateMoveSelection` (`AIHelpers.cs:36`). Pass the team's actions queued so far this turn, and filter family-move targets with `EvaluateBlockTarget`. The AI never queues a blocked cast.
- **Team order.** Build slot 0's action first, then slot 1's with slot 0's queued action visible, so two AI allies never block the same target.
- **Category.** Cast a block only against an expected attack of its category. Value = tier % × the largest single matching attack into the target.
- **Banking.**
  - Bank AP for Greater tier: Blockade needs 3, so pass on the turn before a known heavy if regen alone won't cover it.
  - Skip Lower-tier casts when the matching threat is small.
- **No greedy walling.** Greedy walling costs AP for little (STALL/greedy: 6.98 AP a battle on blocks).
- The sim policy `vwall`/`team_plan` in `walls_pvp_v2_sim.py` is a reference implementation.

### 5.5 Supabase / battle-service parity

- **How PvP works:** casual dual-sim. The server does structural checks only. Per `pvp_rules.ts`: "Cross-turn AP regen stays in the Unity resolver. A tampered resolver disputes on hash compare."
- **Server check (same turn).** In `validateTurnActions` (`pvp_rules.ts:97`), reject a submission with `block_target_duplicate` when two `UseMove` actions both resolve to a `BLOCK_MOVE_IDS` move with the same `targetTeamIndex`/`targetSlotIndex`. Only reject when both move ids are certain from the reconstructed active roster. `rosterMoveError` deliberately does not bind indices to slots because of auto-swaps, so never false-reject an honest turn.
- **Cross-turn state** (a block still up from last turn) is not visible to the stateless server. The Unity resolver safety net (E4) enforces it on both clients, and a tampered client disputes on hash compare.
- **Regenerate** `move_ap.ts` / `move_flags.generated.ts` with `CombatServerTablesExporter`. This bumps `manifestHash`.
- **Run** `move_ap_parity_test.ts` and `known_pool_parity_test.ts`, plus new `block_target_duplicate` cases in `pvp_matchmaking_test.ts` (where `validateTurnActions` is tested today).
- **Ship together.** Edge functions and the client build must ship together. Both PvP clients must run the same resolver version, or every battle with a family move disputes on hash compare. Version-gate matchmaking.
- **No server-side combat resolution** is added.

### 5.6 Tests

| Test | Checks |
|---|---|
| `Wall_BlocksMeleeOnly_BarrierBlocksRangedOnly` | category filter |
| `Block_MitigatesTierPercent_OnOneHit_ThenConsumed` | one hit, % exact, consumed |
| `Block_MultiHit_BreaksOnFirstHit_RestLandFull` | multi-hit rule |
| `Block_Miss_DoesNotConsume` | misses |
| `Block_NotHitOnCastTurn_StillBlocksNextTurn` | 2-turn duration |
| `Block_ExpiresEndOfNextTurn_WhenNotHit` | 2-turn duration |
| `BlockTarget_HoldingIntactBlock_IsNotSelectable` | E4, UI + AI share the check |
| `BlockTarget_QueuedByTeammateThisTurn_IsNotSelectable` | E4 same-turn allies |
| `BlockMove_AllTargetsBlocked_StaysOnMoveSelect_NoApSpent` | E4 / 5.3 |
| `BlockTarget_BackNavigation_ClearsInvalidQueuedBlock` | 5.3 |
| `AI_NeverQueuesBlockedCast` | 5.4 |
| `Resolver_BlockedCastSafetyNet_PassNoApNoRechargeNoEffects` | E4 safety net |
| `Resolver_BlockedCastSafetyNet_NeverFiresInScriptedBattles` | E4 unreachable in normal play |
| `Block_PassiveOrModifier_OverridesIntactBlock` | E5 |
| `Block_ConsumedOrExpired_TargetSelectableAgain` | E3 / E5 |
| `Block_PriorityHitAfterBlock_IsMitigated` | E6 |
| `Wallbreaker_IgnoreWalls_SkipBlockFirstHit_BlockSurvives` | D1 |
| `CoreBreach_IgnoresBlockAndArmor_ConsumesNeither` | D2 |
| `Block_AuthoredRecharge2_ShowsRecharge1_UsableTurnAfterNext` | recharge authoring |
| `IceWall_GrantsAtMost1Ap_OnlyWhenHolderFaster` | E7 |
| `EmberWall_DrainsAttackerOnce_OnMeleeConsume` | E8 |
| `Block_MitigatedHit_StillAppliesRidersAndChemistry` | E9 |
| `LatticeBarrier_Reflects20PercentOfPrevented` | E9 |
| `CompoundBarrier_MeleeDefStacksOnRecast` | D4 |
| `SwapOut_ClearsBlocks_BenchDoesNotTickThem` | E10 |
| `WallTimers_TickBeforeStatusTicks` | E11 |
| `WallTag_PrefixOnly_FirewallIsNotAWall` | E13 |
| `Blockade_IsAWallForPresentation` | UI fix |
| `GuardPosture_IsArmor_StacksWithBlock_NotABlockTarget` | Armor |
| `MoveCatalog_RarityTypePoolsUnchanged` | hard rule |
| `move_ap_parity_test.ts`, `pvp_matchmaking_test.ts` `block_target_duplicate` | Supabase parity |

**Existing tests to rewrite** (they encode the old rules): `AuditReconciliationV1Tests.Walls_MitigateForTheirTurnWindowWithUnlimitedHits` (`:143`, unlimited hits), `CombatWallInterceptTests.ResolverSequencesFourHitMoveAgainstTwoHitWall` and `WallHitRetaliationDrainsAttackerApOnBarrierBlock` (Ember moves from barrier to % source), and `CombatModifierResolverAndPopupTests.IgnoreWalls_BypassesFirstHitBarrier` (D1).

## 6. Folded in from `WALLS_BARRIERS_MITIGATION_ANALYSIS.md`

| Earlier item | Status now |
|---|---|
| 1. Blockade visible: stack key `wall_blockade`, `wallSfxId` | **KEEP** (5.1, 5.3) |
| 1. Wall tags match a `wall_` / `barrier_` prefix, not a substring | **KEEP** (E13) |
| 2. One wall-tagged % source, newer replaces older | **SUPERSEDED** by the single-active rule. A move cast onto a blocked target is denied at selection; only passives/modifiers override (E4, E5). |
| 3. Ice Wall 2 AP, text fix | **KEEP** (2 AP; new card text; AP tied to the block, E7) |
| 4. Full absorb still grants reactive AP | **SUPERSEDED**: no full absorb in the family (E1, E7) |
| 5. Core Breach, catalog-only (`ignoreMitigation`) | **KEEP** catalog-only. It ignores every Wall/Barrier and Armor and consumes neither (D2). |
| 6. Lattice Guard 1 → 2 AP | **KEEP** (Lattice Barrier, 2 AP, Mid) |
| 7. Ash Guard stays out of Cinislug | **KEEP** (no pool change) |
| 8. Clear walls on swap-out and KO; no bench ticking | **KEEP** (E10) |
| 9. Tick wall timers before status ticks | **KEEP** (E11) |
| 10. Ignore Walls (4.8-a) stays a barrier bypass | **DECIDED (D1)**: it also skips Walls/Barriers on the first hit |
| 11. Narrate DoT that lands while a wall is up | **KEEP** (5.3) |
| 12. AI casts a wall on the ally who will be hit | **REPLACED** by 5.4 |
| 13. Guard Stance stays out of pools | **KEEP** |

## 7. Engine facts this plan relies on (s10)

| Fact | Where |
|---|---|
| Turn order: swaps → priority block → normal → capture; each block sorted by Speed desc → stars → rebirth → roll | `CombatResolver.cs:863`, `:970`, `:988` |
| Priority is binary: tag `PRIOR`/`PRIO`, or stamps 4.3-a Jump the Queue (power ×0.90), 4.3-b Wallbreaker, 1.1-a, 1.3-a, 1.3-b, 1.2-a | `CombatResolver.cs:7225`, `:7236`, `:7243`, `:4700` |
| PRIOR attacks today: Quick Jab (P16, Crackett and Rattlestake), Blink Lunge (P26, no pool), Rapid Strike (P26, Forkrook). All melee, so **no ranged priority attack exists** (out of scope this round). | `Moves.csv`, `SpeciesMovePools.csv` |
| No family move has PRIOR or a recharge today; all eight are `SingleAlly` | `Moves.csv` |
| % mitigation sources take a contact filter and a drain field; consumed per landed hit | `ChainlingInstance.cs:455`, `:587`, `CombatResolver.cs:2323` |
| Barrier absorption ignores the contact filter; only barriers apply drains | `ChainlingInstance.cs:874–925`, `:896`, `:916`, `:1014`, `:1025` |
| Wallbreaker bypasses barriers on the first hit only | `CombatResolver.cs:7178` |
| `ignoreMitigation` zeroes mitigation in damage | `DamageCalculator.cs:179` |
| One barrier per Chainling | `CombatResolverConfig_Default.asset:31`, `ChainlingInstance.cs:850` |
| Status ticks run before mitigation and barrier timers | `CombatResolver.cs:483`, `:492`, `:495` |
| Wall tag = substring match | `CombatWallStateUtility.cs:119` |
| Blockade stack key `blockade`, empty `wallSfxId` | `Move_blockade.asset` |
| Recharge display = authored − 1 | `CombatResolver.cs:525` |
| One legality service shared by UI, AI and resolver; it already reads teammates' queued actions | `CombatActionEligibilityService.cs:21`, `:112`, `:184` |
| UI, AI and resolver call `EvaluateMoveSelection` | `CombatSelectionCoordinatorUGUI.cs:514`, `AIHelpers.cs:36`, `CombatResolver.cs:1621` |
| Target candidates and the confirm path | `CombatSelectionCoordinatorUGUI.cs:857`, `:300`, `:327` |
| The bridge keeps this turn's queued actions per slot | `CombatActionSelectorBridge.cs:47` |
| Server turn validation is structural and stateless | `pvp_rules.ts:97` |
| Sim turn cap 30 (engine 100) | `tempo_2v2_sim.py:188`, `CombatBattleController.cs:114` |

## 8. Method and caveats

- **Sim mechanics** (`tblock` in `walls_pvp_v2_sim.py`):
  - The block's % multiplies the first matching landed hit, then the block is removed. Under C it otherwise lasts the cast turn and the next.
  - Riders and chemistry still apply to that hit.
  - Ember drains 1 AP on a melee consume. Ice Wall grants 1 AP if the holder is faster.
  - **Denial at selection (C designs):** the AI never picks a block onto a target that holds one, and the team plan never queues two blocks onto one target in a turn. The resolver-side guard (refund, no recharge) is still in the sim as a safety net. It counts as "blocked casts that reached resolution", and it fired 0 times in all 124 C-family cells.
  - A "carry" event is logged when a block absorbs on a later turn than its cast.
  - GP Armor stays today's 25% window and multiplies.
- **AI:**
  - Category-aware, one block per target.
  - Value = % × the largest single matching attack, with a threshold of 15% of the target's current HP.
  - Banks AP for blocks that cost more than one regen.
  - A non-PRIOR lasting block (Cnp Blockade) is cast on the prep turn before the heavy.
  - The attacker "reads" a block when one is up on its target, or castable and at least 33%. It then retargets, jabs, or holds the heavy (the `/rt`, `/jab`, `/wait` cells).
- **Regression:** the `engine`, `P` and `none` cells reproduce the previous run (`walls_pvp_v2_sim.prev.json`) exactly, and every cell shared with the first decided round (`walls_pvp_v2_sim.decided1.json`, including all of F) is identical. `engine` and `none` also match `walls_pvp_sim.out.json`.
- **Not simulated:**
  - Vapor, Ash and Lattice (no pool), and Lattice reflect.
  - Passive/modifier override blocks (no such source exists yet).
  - Wallbreaker / Ignore Walls / Core Breach vs blocks (D1, D2).
  - A priority attacker slower than the wall caster.
  - Human reads beyond "avoid a block that is up".
- **Win %** (T-WIN) is AI- and kit-dependent; use it for direction only. KO timing is the balance signal.

## 9. Open questions (still undecided)

Everything the first decided round left open (duration, the cancel refund, Blockade [PRIOR], Ember's tier, riders, the bypass stamps, a Greater Barrier, Compound stacking) is now a decision or an accepted default in section 1. What is left:

1. **Q1. HS-B** (Armor + Greater stack, mean 6.50): acceptable, or set the Armor round's % floor now?
2. **Q2. AP-denial endgame** (not a wall rule). In the sim, a scripted Memory Leak partner can hold a lone Blockwyrm at 0 AP until the 30-turn cap (CK-E/deny, 4 of 4000 battles). Worth a look in the AP-denial review: can a human Keyper do this on purpose?
3. **Q3. The Armor round** (section 3): stacking rule, % floor, and which moves and modifiers become Armor.

## 10. Full tables (generated, verbatim from `walls_pvp_v2_tables.py`)

- **Format:** "T1 / T3 KO % · mean KO turn", capped at 12.
- **Battles:** 4000 per cell.

<!-- generated by walls_pvp_v2_tables.py -->

### T-MAIN  Target KO % by end of T1 / T3 · mean KO turn (capped at 12)

| cell | none | engine | P | F | C |
|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 0/95 · 2.22 | 0/0 · 12.00 | 0/5 · 4.59 | 0/90.8 · 3.11 | 0/90.6 · 3.35 |
| STALL, wall every legal turn | 0/95 · 2.22 | 0/0 · 12.00 | 0/95.3 · 2.18 | 0/95.5 · 2.31 | 0/3.3 · 7.16 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 0/5.4 · 4.49 | 0/0 · 8.54 | 0/0.3 · 4.87 | 0/1.4 · 4.46 | 0/1.3 · 4.47 |
| SC Seam Cut T1 -> Deghoul, ally Ember | 91.7/100 · 1.09 | 91.3/96.5 · 1.21 | 0/99.3 · 2.09 | 1/99.5 · 2.05 | 1.4/98 · 2.10 |
| PF Seam Cut + Hammer Sync focus -> Deghoul, ally Ember | 92.2/100 · 1.08 | 91.7/96.3 · 1.22 | 0/99.8 · 2.04 | 1.1/99.8 · 2.01 | 1/97.6 · 2.09 |
| PS Partition Shot (ranged) T1 -> Deghoul, ally Ember | 64.8/98.8 · 1.61 | 63.9/65.8 · 2.08 | 62.8/98.4 · 1.66 | 63.8/97.5 · 1.65 | 64.7/89.5 · 1.84 |
| CR Cryo Ray (ranged) -> Crackett, ally Ember | 0/93.2 · 2.21 | 0/0.4 · 4.93 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 |
| CR-H Cryo Ray -> Crackett, ally Humpacked Buffer Barrier | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/0 · 4.83 | 0/28.3 · 3.76 | 0/27.4 · 3.80 |
| DD Deep Dig -> Keyper, ally Ember | 0/94.5 · 2.17 | 0/0 · 4.47 | 0/0.8 · 4.83 | 0/5 · 4.23 | 0/3.9 · 4.50 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0/95 · 2.14 | 0/64.2 · 2.90 | 0/0 · 4.22 | 0/1.6 · 4.18 | 0/1.6 · 4.18 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 0/95 · 2.22 | 0/63.6 · 2.86 | 0/0 · 4.16 | 0/28.6 · 3.57 | 0/28.6 · 3.57 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0/51.6 · 4.23 | 0/5.8 · 6.33 | 0/0 · 6.42 | 0/0 · 6.50 | 0/0 · 6.50 |
| NCR Cryo Ray -> Rattlestake, self Compound Barrier | 0/57.2 · 2.92 | 0/8.5 · 3.99 | 0/0 · 4.94 | 0/2.9 · 4.12 | 0/2.9 · 4.12 |
| NHF Hard Fork -> Rattlestake, Compound Barrier + ally Blockade | 0/94.7 · 2.24 | 0/20.9 · 5.71 | 0/0.3 · 5.55 | 0/57 · 3.95 | 0/57.1 · 4.15 |
| NHFh Hard Fork (melee) -> Humpacked, self Buffer Barrier | 0/97.6 · 2.09 | 0/95.3 · 2.19 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Buffer Barrier + ally Ember | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/97.1 · 2.40 | 0/96.4 · 2.32 | 0/92.4 · 2.40 |
| DC, AI plans both blocks (F: 2nd cancelled on resolution; C: denied at selection) | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/97.1 · 2.40 | 0/96.2 · 2.31 | 0/92.4 · 2.40 |
| DC, defenders block only vs >=40% current-HP threats | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/92.9 · 3.02 | 0.1/97.6 · 2.35 | 0.1/95.8 · 2.37 |

### T-SENS  Sensitivities around C (the decided rules), same cells and format

| cell | Cnp | Crc2 | Cmax |
|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 0/90.6 · 3.35 | 0/90.7 · 3.35 | 0/90.6 · 3.35 |
| STALL, wall every legal turn | 0/3.3 · 7.16 | 0/9.1 · 4.81 | 0/3.3 · 7.16 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 0/1.3 · 4.47 | 0/1.4 · 4.52 | 0/1.3 · 4.47 |
| SC Seam Cut T1 -> Deghoul, ally Ember | 1.4/98 · 2.10 | 1.5/99.2 · 2.09 | 1.4/98 · 2.10 |
| PF Seam Cut + Hammer Sync focus -> Deghoul, ally Ember | 1/97.6 · 2.09 | 1.2/99.5 · 2.06 | 1/97.6 · 2.09 |
| PS Partition Shot (ranged) T1 -> Deghoul, ally Ember | 64.7/89.5 · 1.84 | 63.6/90.2 · 1.83 | 64.7/89.5 · 1.84 |
| CR Cryo Ray (ranged) -> Crackett, ally Ember | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 |
| CR-H Cryo Ray -> Crackett, ally Humpacked Buffer Barrier | 0/27.4 · 3.80 | 0/29.6 · 3.73 | 0/14.5 · 4.10 |
| DD Deep Dig -> Keyper, ally Ember | 0/3.9 · 4.50 | 0/4.3 · 4.23 | 0/3.9 · 4.50 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0/95 · 2.22 | 0/1.5 · 4.09 | 0/0 · 6.36 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 0/28.6 · 3.57 | 0/29 · 3.51 | 0/5.3 · 4.09 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0/9.9 · 4.31 | 0/0 · 4.42 | 0/0 · 7.02 |
| NCR Cryo Ray -> Rattlestake, self Compound Barrier | 0/2.9 · 4.12 | 0/2.9 · 4.07 | 0/0 · 4.76 |
| NHF Hard Fork -> Rattlestake, Compound Barrier + ally Blockade | 0/94.5 · 2.27 | 0/56.8 · 4.15 | 0/10.1 · 5.46 |
| NHFh Hard Fork (melee) -> Humpacked, self Buffer Barrier | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Buffer Barrier + ally Ember | 0/92.4 · 2.40 | 0/94.5 · 2.37 | 0/91.8 · 2.42 |
| DC, AI plans both blocks (F: 2nd cancelled on resolution; C: denied at selection) | 0/92.4 · 2.40 | 0/94.5 · 2.37 | 0/91.8 · 2.42 |
| DC, defenders block only vs >=40% current-HP threats | 0.1/95.8 · 2.37 | 0.1/95.3 · 2.37 | 0.1/95.8 · 2.37 |

### T-STALL  % of battles that reach the sim's 30-turn cap · block casts per battle

| cell | none | engine | P | F | C | Cnp | Crc2 | Cmax |
|---|---|---|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 0% · 0 | 99.8% · 29.97 | 0% · 1.01 | 0% · 1 | 0% · 1 | 0% · 1 | 0% · 1 | 0% · 1 |
| STALL, wall every legal turn | 0% · 0 | 99.8% · 29.97 | 0% · 1.05 | 0% · 1.11 | 0% · 3.49 | 0% · 3.49 | 0% · 1.92 | 0% · 3.49 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 0% · 0 | 1.1% · 7.84 | 0% · 2 | 0% · 2 | 0% · 2 | 0% · 2 | 0% · 1.25 | 0% · 2 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0% · 0 | 0% · 3.27 | 0% · 5.27 | 0% · 6.27 | 0% · 6.27 | 0% · 2.27 | 0% · 5.14 | 0% · 9.53 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0% · 0 | 0% · 5.68 | 0% · 7.5 | 0% · 8.9 | 0% · 8.9 | 0% · 5.24 | 0% · 5.48 | 0% · 9.43 |
| worst cell (highest cap %), all 31 cells | 0% | 99.8% | 0% | 0% | 0.1% | 0.1% | 0.1% | 0.1% |

### T-CK  Crackett (86 melee / 38 ranged Atk, no damaging ranged move) vs a melee wall on Deghoul. T1 / T3 KO % · mean KO turn

| attacker response | none | engine | P | F | C |
|---|---|---|---|---|---|
| naive: Seam Cut into the target every turn | 92/100 · 1.09 | 91.8/97 · 1.19 | 0/99.4 · 2.09 | 1.3/99.6 · 2.05 | 1.2/98.1 · 2.11 |
| retarget: hit the other foe while the wall is ready | 92/100 · 1.09 | 0/63.9 · 3.40 | 0/93.1 · 2.21 | 0/93.9 · 2.15 | 0/28.6 · 3.90 |
| priority: Quick Jab the target while the wall is ready | 92/100 · 1.09 | 0/0.3 · 4.20 | 0/99.8 · 2.06 | 0/96.6 · 2.09 | 0/24.4 · 4.00 |
| ranged partner (Marrowhead) focuses the target | 98.1/100 · 1.02 | 97.9/99.4 · 1.05 | 63.6/100 · 1.36 | 93.3/100 · 1.07 | 93/100 · 1.09 |
| multi-hit partner (Kitsauto Blade Flurry) focuses | 94.7/100 · 1.05 | 95.2/95.3 · 1.15 | 0.2/100 · 2.02 | 81.9/100 · 1.18 | 81.9/100 · 1.18 |
| AP denial partner (Keyper Memory Leak on Cinislug) | 91.9/100 · 1.08 | 92.7/99.2 · 1.16 | 0/99.2 · 2.08 | 1/99.7 · 2.04 | 1.3/98.8 · 2.09 |
| naive, wall = ally Blockwyrm Blockade | 92.7/100 · 1.08 | 92.2/99.8 · 1.09 | 0/99.6 · 2.09 | 92/99.8 · 1.13 | 91.7/99.3 · 1.15 |
| retarget, wall = ally Blockwyrm Blockade | 92.7/100 · 1.08 | 92.2/99.8 · 1.09 | 0/99.6 · 2.08 | 92.7/99.5 · 1.15 | 91.2/91.2 · 1.33 |

### T-RHY  Rhythm read (attacker holds the heavy while a matching block is ready). T1 / T3 KO % · mean KO turn

| cell | none | engine | P | F | C |
|---|---|---|---|---|---|
| CR-H naive | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/0 · 4.83 | 0/28.3 · 3.76 | 0/27.4 · 3.80 |
| CR-H attacker holds Cryo Ray while the wall is ready | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/57.9 · 3.57 | 0/28.3 · 3.76 | 0/27.4 · 3.80 |
| HS naive | 0/32.2 · 4.76 | 0/0.1 · 4.87 | 0/0.1 · 5.63 | 0/0.3 · 5.53 | 0/0.2 · 5.59 |
| HS attacker holds Hammer Sync while the wall is ready | 0/32.2 · 4.76 | 0/8.5 · 4.70 | 0/3.4 · 5.84 | 0/18 · 4.92 | 0/0 · 5.56 |
| DD naive | 0/94.5 · 2.17 | 0/0 · 4.47 | 0/0.8 · 4.83 | 0/5 · 4.23 | 0/3.9 · 4.50 |
| DD attacker holds Deep Dig while the wall is ready | 0/94.5 · 2.17 | 0/0 · 5.40 | 0/26.2 · 4.03 | 0/34.2 · 3.82 | 0/0 · 4.68 |

### T-ABS  Blocks that mitigated a hit on the target per battle · of those, on a later turn than the cast (Blockade cells; Cnp = Blockade without PRIOR)

| cell | P | F | C | Cnp | Cmax |
|---|---|---|---|---|---|
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 1 · 0 | 1.99 · 0 | 1.99 · 0 | 0 · 0 | 3 · 0 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 1.04 · 0 | 2.28 · 0 | 2.28 · 0 | 0.78 · 0.78 | 2.3 · 0 |
| NHF Hard Fork -> Rattlestake, Compound Barrier + ally Blockade | 1.07 · 0 | 1.01 · 0 | 1 · 0 | 0 · 0 | 1.05 · 0 |
| CK-B Crackett vs ally Blockade | 0.96 · 0 | 0.08 · 0 | 0.09 · 0 | 0 · 0 | 0.09 · 0 |

### T-AP  AP the defending team spends on blocks per battle · defending team damage dealt per battle

| cell | none | engine | P | F | C |
|---|---|---|---|---|---|
| SC Seam Cut T1 -> Deghoul, ally Ember | 0 · 179.3 | 2.06 · 138.4 | 2.01 · 173.7 | 2.11 · 171.1 | 2.12 · 172.6 |
| CR-H Cryo Ray -> Crackett, ally Humpacked Buffer Barrier | 0 · 217.1 | 1.69 · 186.3 | 1.95 · 304.4 | 0.97 · 251 | 0.98 · 252.5 |
| DD Deep Dig -> Keyper, ally Ember | 0 · 259.7 | 2.75 · 306.4 | 2 · 332.3 | 2.61 · 300.6 | 2.6 · 313 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0 · 393.4 | 6.96 · 378.8 | 6.32 · 365.5 | 10.45 · 349.5 | 10.45 · 349.5 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 0 · 360.4 | 2.5 · 323.9 | 6.21 · 291.9 | 7.04 · 242.5 | 7.04 · 242.5 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0 · 380.7 | 11.6 · 338 | 8.6 · 304.4 | 13.71 · 287.7 | 13.71 · 287.7 |
| NCR Cryo Ray -> Rattlestake, self Compound Barrier | 0 · 224.2 | 5.7 · 174.7 | 2.58 · 219.2 | 4.1 · 194.5 | 4.1 · 194.5 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Buffer Barrier + ally Ember | 0 · 241.6 | 8.28 · 81.6 | 3.1 · 200.3 | 2.61 · 223.5 | 2.63 · 222 |
| CK-B Crackett vs ally Blockade | 0 · 213.8 | 0.25 · 190.9 | 2.01 · 266.8 | 0.25 · 192.6 | 0.27 · 193.4 |

### T-DC  Double coverage: turns per battle with both categories covered on the target · blocked casts that reached resolution per battle

| cell | none | engine | P | F | C |
|---|---|---|---|---|---|
| DC | 0 · 0 | 0 · 0 | 1.02 · 0 | 0 · 0 | 0 · 0 |
| DC/naive | 0 · 0 | 0 · 0 | 1.02 · 0 | 0 · 0.24 | 0 · 0 |
| DC/strict | 0 · 0 | 0 · 0 | 0.86 · 0 | 0 · 0 | 0 · 0 |

### T-PRIO  Priority hits on the target per battle · of those, landed before a same-turn block on it

| cell | none | engine | P | F | C |
|---|---|---|---|---|---|
| CK-E/jab | 0 · 0 | 0.95 · 0 | 0.95 · 0.95 | 0.98 · 0.98 | 1.65 · 1.14 |

### T-WIN  Attacking-team win % (AI-dependent; direction only)

| cell | none | engine | P | F | C |
|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 22.6 | 99.8 | 3.9 | 9.9 | 9.5 |
| STALL, wall every legal turn | 22.6 | 99.8 | 31.8 | 32 | 56.1 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 31.2 | 99.9 | 57 | 60.8 | 60.9 |
| SC Seam Cut T1 -> Deghoul, ally Ember | 99.9 | 100 | 99.9 | 99.9 | 99.9 |
| PF Seam Cut + Hammer Sync focus -> Deghoul, ally Ember | 98.8 | 100 | 97.6 | 98.1 | 97.3 |
| PS Partition Shot (ranged) T1 -> Deghoul, ally Ember | 98.7 | 99.8 | 99.5 | 99.7 | 99.7 |
| CR Cryo Ray (ranged) -> Crackett, ally Ember | 99.1 | 100 | 99.1 | 99.1 | 99.1 |
| CR-H Cryo Ray -> Crackett, ally Humpacked Buffer Barrier | 100 | 100 | 100 | 100 | 100 |
| DD Deep Dig -> Keyper, ally Ember | 96.9 | 96.9 | 76.6 | 93.8 | 88 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 22.4 | 29.3 | 28.6 | 30.7 | 30.7 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 63.7 | 87 | 90.6 | 96 | 96 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 13.8 | 51.2 | 62.8 | 64.4 | 64.4 |
| NCR Cryo Ray -> Rattlestake, self Compound Barrier | 98.9 | 99 | 97.5 | 99 | 99 |
| NHF Hard Fork -> Rattlestake, Compound Barrier + ally Blockade | 99.1 | 100 | 97.1 | 100 | 99.7 |
| NHFh Hard Fork (melee) -> Humpacked, self Buffer Barrier | 61.2 | 83.5 | 61.2 | 61.2 | 61.2 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Buffer Barrier + ally Ember | 98.2 | 100 | 99.4 | 99.5 | 99.4 |
| DC, AI plans both blocks (F: 2nd cancelled on resolution; C: denied at selection) | 98.2 | 100 | 99.4 | 99.5 | 99.4 |
| DC, defenders block only vs >=40% current-HP threats | 98.2 | 100 | 95.5 | 97.7 | 97.8 |

## 11. Reproduce and checker

```
cd /workspace/balance/out
python3 walls_pvp_v2_sim.py /workspace/balance/s10 4000   # ~2 min, writes walls_pvp_v2_sim.out.json (248 cells)
python3 walls_pvp_v2_tables.py > walls_pvp_v2_tables.md
python3 walls_pvp_v2_check.py                              # engine facts, data rules, regression, every table and prose number
```

Checker result (2026-10-20 21:31 CT, run against s10 and the 248-cell JSON): **5077 PASS, 0 FAIL**. That includes a replay of the 4 capped CK-E/deny battles and the plan's data rows against section 5.1. A mutation test (one card's duration line, one rarity, one headline cell and two prose numbers edited) gave 5 FAIL, as expected.
