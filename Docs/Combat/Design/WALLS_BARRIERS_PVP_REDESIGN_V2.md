# Walls & Barriers v2: testing Cronus's simple rule set (PROPOSALS)

> **Status: PROPOSALS ONLY.** Nothing here is applied to code or data. Every value, card text, rename and code note is for Cronus to accept, change or reject.
> **Snapshot:** engine mirror `/workspace/balance/s10` (2026-10-20 02:15 CT). **Sim:** `tempo_2v2_sim.py` (unchanged) + `walls_pvp_v2_sim.py` (new designs and policies, as a subclass). 4000 battles per cell, random IVs (±5 per stat), real learn-pool kits, type chemistry, passives, statuses, AP denial, swaps, focus fire and ally-targeted walls. Seeds are shared across designs, so columns are directly comparable.
> **Checker:** `walls_pvp_v2_check.py`. Result: see section 11.
> **Hard rules kept:** no move changes rarity or learn-pool tier (`learn_pool_rarity_6d7ff36f`); Ember Wall stays Cinislug's Legendary signature and the strictly highest rarity in its pool; no self-damage; Block stays the barrier type.

## 0. Plain-language summary

**Cronus's rule set:**
- **Walls** stop melee.
- **Barriers** stop ranged.
- Both act first (PRIOR).
- Both need a recharge.

**Verdict.** It works as a rule, and the sim supports the core idea. It needs **three fixes** before it is safe to ship:

1. **"Blocks one hit, this turn only."** Of the four versions, the 1-hit version (V4) is the only one where multi-hit moves are the counter Cronus wants. Blade Flurry into a self-walled Cinislug:
   - V4: mean KO turn **4.98**, close to no walls at all (4.49).
   - V1 (one whole move blocked): **9.71**. V2 (all hits that turn blocked): **9.83**.
2. **Recharge 2 turns, not 1.** Our heavies cost 4 AP and Chainlings regen 2 AP a turn, so a heavy fires every other turn. A 1-turn-recharge wall is also up every other turn. The two line up, and the wall then blocks the heavy every time. With a 1-turn recharge:
   - Deep Dig into Keyper (Guard Posture + ally Blockade): KO by turn 3 is **0%**, and the mean KO turn is **11.25**.
   - The wall-caster spends **25.52 AP** a battle on walls.
   
   A 2-turn recharge breaks that lockstep. The mean KO turn falls to **4.22**, and every turn-1 answer is kept.
3. **Every wall/barrier costs 2 AP.** Chainlings start at 0 AP and gain 2 a turn, so a 3-AP wall can never be cast on turn 1.
   - Crackett's Seam Cut into Deghoul behind an ally Blockade: **92.2%** turn-1 KO at today's 3 AP, **0%** at 2 AP.

**What the rule does well:**
- It answers turn-1 bursts: Seam Cut turn-1 KO goes from **91.3%** to **0%**.
- It ends the Ember Wall stall: **99.8%** of battles hit the turn cap today, **0%** under the proposal.
- It hard-counters a one-sided attacker for the turns the wall is up.

**What it does not do:**
- **It does not protect against the other category.** Cryo Ray into a target with an ally Ember Wall: **93.3%** KO by turn 3, the same as no wall. Package R's Protect held it to **0.4%**.
- **It does not stop a one-sided attacker for long.** Against Crackett it buys one turn, because the attacker waits out the recharge. With a ranged partner, the wall is mostly bypassed (**63.6%** turn-1 KO).
- **"Only a faster priority attack beats it" does almost nothing in practice.**
  - Only three attacks have priority today (Quick Jab, Blink Lunge, Rapid Strike), all melee and Power 16–26. Only Quick Jab and Rapid Strike are in any learn pool.
  - The species that learn them (Crackett, Forkrook) are faster than every wall user.
  - A priority hit lands before the wall, but it does not remove the wall. In the sim it never changed the KO turn (mean **2.06** vs **2.09** naive).

**Recommendation (PROPOSAL "P"):**
- **Ruleset:**
  - **Wall** = blocks the next **melee hit** on one ally or yourself, **this turn only**, [PRIOR], **Recharge 2**, **2 AP**.
  - **Barrier** = the same for **ranged**.
  - A blocked hit does nothing: no damage, riders or chemistry.
- **Moves that don't fit:**
  - Guard Posture stays a plain "Guard" (25% next hit, no priority).
  - The three "Guard"-named family moves are renamed to Barriers: Compound, Ash and Lattice (section 7).
- **Keep package R's Ember Wall (all-category Protect) instead?** That is Cronus's call (open question Q1). The data for both is in the table.

## 1. Task A: does a priority system exist in s10?

**Yes. Turn order, priority tag, stamps and UI all exist.** Nothing about walls uses it yet.

| What | Where (s10) | Notes |
|---|---|---|
| Turn order | `CombatResolver.cs:863` sorts by Speed desc → stars → rebirth → 50/50 roll; `:970` / `:988` split the queue: **swaps → priority block → normal moves → capture** | The priority block is itself Speed-sorted, so of two PRIOR actions the faster resolves first. This is the existing basis for Cronus's "beaten only by a *faster* priority attack". |
| Priority flag | `CombatResolver.cs:7225` `ResolvePriorityForAction`: a move is PRIOR if its `Tags` contain `PRIOR` or `PRIO` (`:7236`) | **No numeric priority field.** Priority is binary: in the block or not. There is no "+4 beats +1" ladder. |
| Priority from stamps | same function, `:7243` / `:7250` / `:7274` | `4.3-a` Jump the Queue (Epic, PRIOR, power ×0.90 at `:4700`). `4.3-b` Wallbreaker (Legendary, PRIOR **and** first hit bypasses barriers, `:7178`). `1.1-a` Hair Trigger, `1.3-a` First Dibs and `1.3-b` Quiet Priority are % chances. `1.2-a` Last Gasp gives priority below a HP threshold. All are on 1–2 AP damaging moves, or utility moves for 1.3-a/b. |
| UI | `MoveSelectDisplayUtility.cs:199–213` shows a PRIOR pill | Already player-facing |
| Moves tagged PRIOR (`Moves.csv`, 211 rows) | **Quick Jab** (`baseline_jab`, None/Common, melee P16, 1 AP; in Crackett's and Rattlestake's pools). **Blink Lunge** (`quick_lunge`, None/Common, melee P26, 2 AP; in **no** pool). **Rapid Strike** (`rapid_strike`, Forked/Rare, melee P26, 1 AP; Forkrook). Position Swap (utility). Bean Sip (utility heal, Cofi). | **Every PRIOR attack is melee, Power 16–26.** No ranged priority attack exists without a stamp. |
| Walls today | No wall/barrier move has PRIOR; all have `RechargeTurns` 0 | |

**Speed reality for "beaten by a faster priority attack".** Base Speed of the priority attackers vs the wall learners, and P(attacker faster) with ±5 IVs:

| Priority attacker | Cinislug 28 | Blockwyrm 24 | Gilarime 49 | Humpacked 44 | Rattlestake 57 | Cofi 16 |
|---|---|---|---|---|---|---|
| Crackett 92 (Quick Jab) | 100% | 100% | 100% | 100% | 100% | 100% |
| Forkrook 88 (Rapid Strike) | 100% | 100% | 100% | 100% | 100% | 100% |
| Rattlestake 57 (Quick Jab) | 100% | 100% | 95% | 100% | 45.5% | 100% |

So the condition is basically on/off by species.
- Crackett and Forkrook always beat any wall with a priority hit.
- Nobody else can, unless they carry a stamp.

**What would need building** (data first, code second; none of it is done):

1. **Data:**
   - Add `PRIOR` to every Wall/Barrier's `Tags`.
   - Set `RechargeTurns = 3`. Authored recharge counts the cast turn's end tick, so 3 means "skip 2 turns" (`FormatPlayerFacingRecharge`, `CombatResolver.cs:525`).
   - Set AP to 2.
2. **Category filter on barriers.** `BarrierInstance.contactFilter` exists, but `ApplyBarrierAbsorption` never reads it (`ChainlingInstance.cs:874–925`). The importer must also parse a contact parameter for `FullAbsorbBarrier`. Melee maps 1:1 to Contact and ranged to Projectile (checked). % mitigation windows already honour a contact filter (`AppliesToContact`, `ChainlingInstance.cs:587`). Chemistry and status code already create Projectile-only windows (`ChemistryResolver.cs:414`, `StatusProcessor.cs:425`). V3-style Screens therefore need only an importer parameter, not new filter logic.
3. **One Wall + one Barrier per Chainling.** Today `exclusiveBarriersAndWalls: 1` makes the second barrier **replace** the first (`ChainlingInstance.cs:850`). Without this change, Cronus's "ally Wall + self Barrier" double coverage is impossible: the second cast silently deletes the first.
4. **A blocked hit applies nothing.** Today statuses and type chemistry still land through a blocked hit (`tempo_2v2_sim.py` mirrors this). The proposal needs a "blocked" early-out before riders/chemistry.
5. **"This turn only"** = `FullAbsorbBarrier:1:1`. Verify `TickBarriers` expires it at the end of the cast turn.
6. **Ember Wall's drain:** at most 1 AP per blocked move.
7. **AI:**
   - Category-aware wall choice.
   - Bank AP for a wall when the threat fires next turn.
   - Don't wall on turns no matching threat can fire (section 4.7).

## 2. Variants tested

All V-variants:
- Walls (Ember Wall, Blockade, Ice Wall) = **melee**.
- Barriers (Barrier Wall, Vapor Barrier, Compound Guard) = **ranged**. Compound Guard keeps its MeleeDef +1.
- All are PRIOR (except V2np), target one ally or self, and use today's AP (except V2ap2 and P).
- Ember Wall keeps "attacker loses 1 AP", once per blocked move, melee only.
- A blocked hit deals 0 and applies no rider or chemistry.
- Guard Posture is left as today's 25% next-hit softener (it is not a Wall or Barrier by name).

| Design | Block | Lasts | Recharge |
|---|---|---|---|
| `engine` | today's data (Ember 3-hit absorb 2 turns, % walls) | | none |
| `R` | previous study's package (Ember = PRIOR Protect all categories; % Screens per category; HP Shields) | | 1 turn on Ember |
| **V1** | full block of the **first matching move** (all its hits) | this turn | 1 turn |
| **V2** | full block of **every** matching hit (Protect-by-category) | this turn | 1 turn |
| **V3** | **50%** less damage from the category (one Screen per category, never multiplied) | 2 turns (`V3t3`: 3) | 1 turn after expiry |
| **V4** | full block of the **first matching hit**; later hits of a multi-hit land | this turn | 1 turn |
| `V1L` | V1, but lasts until it blocks (up to 2 turns) | ≤ 2 turns | 1 turn |
| `V2np` | V2 without PRIOR | this turn | 1 turn |
| `V2ap2` | V2 with every wall/barrier at 2 AP | this turn | 1 turn |
| `V2cgm` | V2 with Compound Guard as a melee Wall | this turn | 1 turn |
| `V2rc2` / `V4rc2` | V2 / V4 with a **2-turn** recharge | this turn | 2 turns |
| **P (recommended)** | **V4 + 2-turn recharge + every wall/barrier 2 AP** | this turn | 2 turns |

**Cells.** The 15 lines from the previous study (same seeds) plus 15 new ones:
- CR-H: Cryo Ray into a ranged Barrier.
- DC, DC/strict: double coverage.
- STALL/greedy: wall every legal turn.
- 8 Crackett cells: one per attacker response.
- 3 "wait" cells: rhythm reads.

**Defender AI (V-designs):** a team plan.
- Each turn, the target and its walling ally pick the wall(s) that cover the largest *affordable* threat of each category, with at most one block per category.
- Turn-only walls are cast only on turns the threat can fire.
- Casters bank AP when the line's heavy fires next turn and the wall costs more than one regen.
- `engine` and `R` use the previous study's policy unchanged. That makes their 45 cells **byte-identical** to `walls_pvp_sim.out.json`: the regression check is in the checker.

**Attacker reads** (Crackett and "wait" cells):
- The attacker sees the defender's AP and recharges, so it knows when a matching wall is *ready*.
- When it is, the attacker either retargets, uses a priority hit, or holds the heavy one turn.

**Correction to the previous doc.** Its "100-turn cap" stall cells are the sim's **30-turn** cap (`tempo_2v2_sim.py:188`, `max_turns=30`). The engine cap is 100 (`CombatBattleController.cs:114`). The conclusion ("Ember Wall stalls forever") is unchanged.

## 3. Comparison table (headline)

How to read the table:
- Each cell is **target KO % by end of turn 1 / by end of turn 3 · mean KO turn** (capped at 12). 4000 battles per cell.
- "Target" is the Chainling the line attacks. Lower = the wall protects better.
- `none` = the same teams with every wall/barrier move removed.
- The full set of tables is in section 4. The rows below are copied from T-MAIN / T-CK.

| key line | none | engine (today) | R (prev. package) | V1 move | V2 category-Protect | V3 50% | V4 1 hit | **P** |
|---|---|---|---|---|---|---|---|---|
| Ember Wall recast stall (STALL) | 0/95 · 2.22 | 0/0 · 12.00 | 0/5.3 · 4.77 | 0/4.9 · 4.58 | 0/4.9 · 4.58 | 0/65.2 · 3.52 | 0/4.9 · 4.58 | 0/5 · 4.59 |
| Seam Cut T1 → Deghoul, ally Ember (SC) | 91.7/100 · 1.09 | 91.3/96.5 · 1.21 | 0/92 · 2.17 | 0/93.8 · 2.16 | 0/92 · 2.17 | 1.3/99.3 · 2.12 | 0/93.8 · 2.16 | 0/99.3 · 2.09 |
| Cryo Ray → Crackett, ally Ember (CR) | 0/93.2 · 2.21 | 0/0.4 · 4.93 | 0/0.4 · 5.09 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 |
| Cryo Ray → Crackett, ally Barrier Wall (CR-H) | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/28.7 · 3.75 | 0/0 · 4.84 | 0/0 · 4.84 | 0/0.5 · 4.80 | 0/0 · 4.84 | 0/0 · 4.83 |
| Deep Dig → Keyper, GP + ally Blockade (DD-B, "stacked") | 0/95 · 2.14 | 0/64.2 · 2.90 | 0/94.7 · 2.13 | 0/0 · 11.25 | 0/0 · 11.25 | 0/7.5 · 3.96 | 0/0 · 11.25 | 0/0 · 4.22 |
| Blade Flurry (3 hits) vs self-Ember (MULTI) | 0/5.4 · 4.49 | 0/0 · 8.54 | 0/0 · 9.83 | 0/0 · 9.71 | 0/0 · 9.83 | 0/0 · 8.76 | 0/0 · 4.98 | 0/0.3 · 4.87 |
| Crackett naive vs ally Ember (CK-E) | 92/100 · 1.09 | 91.8/97 · 1.19 | 0/91.6 · 2.18 | 0/93.5 · 2.16 | 0/91.6 · 2.18 | 1.7/99.5 · 2.11 | 0/93.5 · 2.16 | 0/99.4 · 2.09 |
| Crackett naive vs ally Blockade (CK-B) | 92.7/100 · 1.08 | 92.2/99.8 · 1.09 | 92.2/99.7 · 1.12 | 92.2/99.6 · 1.16 | 92/99.6 · 1.17 | 92/99.5 · 1.13 | 92.2/99.6 · 1.16 | 0/99.6 · 2.09 |
| Double coverage: Cryo Ray + Kodiax → Humpacked, self Barrier + ally Ember (DC) | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/12.4 · 3.84 | 0/65.4 · 2.73 | 0/65.4 · 2.73 | 0/94.2 · 3.03 | 0/65.4 · 2.73 | 0/97.1 · 2.40 |
| Stall rate (STALL, % at 30-turn cap) | 0% | 99.8% | 0% | 0% | 0% | 0% | 0% | 0% |
| DD-B: defender AP spent on walls / battle | 0 | 6.96 | 4.2 | 25.52 | 25.52 | 6.05 | 25.52 | 6.32 |

**Reading the variants:**
- **V1 (one move).** Fixes the stall and turn-1 bursts. Multi-hits are blocked wholesale (MULTI 9.71). Locks against 4-AP heavies through the recharge parity.
- **V2 (Protect-by-category).** Plays the same as V1 in nearly every line. The only differences are focus fire with two same-category attackers, and PS (74.1 vs 95.3 by turn 3). The likely reason is that V2 also blocks the melee partner's follow-up into the target. It has the same parity lock.
- **V3 (50% for 2 turns).** The only variant with no lock (DD-B mean 3.96), but it is also the weakest answer to a turn-1 burst:
  - SC still gives 1.3% turn-1 KOs. That is a big drop from 91.3% because 50% off Seam Cut is no longer lethal, but kills come back as soon as damage stacks.
  - Self-cast stall: 65.2% KO by turn 3.
  - Against a retargeting Crackett it is the strongest (35% by turn 3). The screen covers 2 turns, so retargeting buys nothing.
- **V4 (one hit).** Plays like V1, except multi-hits beat it (MULTI 4.98 vs none 4.49). It has the same parity lock.
- **P = V4 + 2-turn recharge + 2 AP.** Keeps every turn-1 answer (SC, PF, CK-E, CK-B all 0% turn-1). Breaks the lock (DD-B 4.22, HS-B 6.42, NCR 4.94, DD-I 4.16). Multi-hit stays the counter (4.87). Wall AP drops back to package-R levels (DD-B 6.32 vs 25.52 under V1).

## 4. Key lines (Task B)

### 4.1 Ember Wall recast stall
- **Today:** 99.8% of STALL battles hit the sim's 30-turn cap, with 29.97 Ember casts a battle.
- **Every variant with a recharge** ends the stall: 0% capped.
  - V1/V2/V4: mean KO turn 4.58. P: 4.59.
  - P blocks for one turn and then is down for two.
- **Greedy walling is punished.** STALL/greedy has the AI wall on every legal turn, even with no heavy coming. KO by turn 3 is 95.3% under P.
- **Don't let a block persist until used.** V1L (the block waits up to 2 turns for a hit) brings the stall back: mean 10.24, greedy 11.21.

### 4.2 Seam Cut turn 1 into Deghoul (ally Ember)
- Engine: 91.3% turn-1 KO.
- V1/V2/V4/P: 0%. V3: 1.3%.
- KO by turn 3 is still 92–99.3%. The wall buys exactly one turn, which is the stated goal of a hard counter to a turn-1 burst.
- **Priority is what makes this work.** V2np (no PRIOR) is back to 92.9% turn-1, because Crackett (Speed 92) outruns Cinislug (28).

### 4.3 Cryo Ray into an ally-walled target
- **Ember is melee-only, so it does nothing against Cryo Ray.** CR is 93.3% KO by turn 3 under every V-variant, the same as none (93.2). Package R's all-category Ember held it to 0.4%.
- **The right barrier works.** With a ranged Barrier Wall on the ally (CR-H): 0% by turn 3 under V1–V4 and P.
- **The attacker's read.** If it holds Cryo Ray while the barrier is ready (CR-H/wait), turn-3 KO comes back: 56.3% (V1), 57.9% (P).

### 4.4 Deep Dig into stacked walls
- **DD-B (Guard Posture + ally Blockade).**
  - V1/V2/V4: 0% KO by turn 3, mean 11.25, wall AP 25.52.
  - P: 0% by turn 3, mean 4.22, wall AP 6.32.
- **DD-I (with Ice Wall).**
  - V1: mean 6.84, wall AP 13.96.
  - P: mean 4.16.
- **HS-B (Hammer Sync).** V1 8.55, P 6.42.
- **This is the recharge-parity lock.**
  - A 4-AP heavy fires on turns 2, 4, 6…
  - A 1-turn-recharge wall is ready on turns 2, 4, 6…
  - A 3-AP wall at +2 AP a turn affords 2 casts every 3 turns, enough to cover every heavy.
- **Two ways to break it:**
  - A 2-turn recharge (V2rc2 4.21, V4rc2 4.21).
  - The attacker reading the wall (DD/wait 34.2% by turn 3 under V1).

### 4.5 Crackett vs a melee wall
Crackett has 86 melee Atk and 38 ranged Atk, and no damaging ranged move. The target is Deghoul behind an ally-cast Ember Wall (Cinislug).

| Crackett's response | Does it work under P? | Number (P) |
|---|---|---|
| Naive: Seam Cut every turn | The wall eats turn 1. KO lands on turn 2 while the wall is on recharge. | T1 0%, mean 2.09 |
| **Swap target** (hit the other foe while the wall is ready) | Slightly *slower* to kill the target. Damage moves to the other foe; the target dies on turn 2–3 anyway. | mean 2.21, T3 93.1% |
| **Multi-hit partner** (Kitsauto Blade Flurry) | Hit 1 pops the wall and hits 2–3 land, but they are not enough to kill on turn 1. | T1 0.2% |
| **Debuff / AP denial** (Keyper Memory Leak on Cinislug) | **No effect.** A 2-AP wall is always affordable on the turns it is off recharge. | mean 2.08 |
| **Priority attack** (Quick Jab while the wall is ready) | The jab lands before the wall: 0.95 jabs a battle, all before a same-turn wall. It adds chip (165.3 vs 132.8 damage) but **does not remove the wall**, so the KO turn barely moves. | mean 2.06 |
| **Ranged partner** (Marrowhead) | **Works.** A melee wall cannot touch a ranged hit. | T1 63.6% |

**Summary.** Crackett is hard-countered for exactly one turn, and his real answer is his partner. Against a 3-AP ally **Blockade** at today's AP, the wall cannot be cast on turn 1 (CK-B 92.2% turn-1 KO under V1). At 2 AP (P) it can: 0%.

V3 is the variant that slows Crackett most. A 2-turn screen survives the retarget: with retarget, 35% KO by turn 3 and mean 3.81.

### 4.6 Double coverage on one target (DC: Humpacked self Barrier Wall + ally Ember, vs Cryo Ray + Kodiax melee focus)
- **This requires the code change in section 1 item 3.** The engine today lets only one barrier live on a Chainling.
- **V1:**
  - 1.1 turns a battle with both categories covered.
  - 1.31 turns where every hit on the target was blocked.
  - 65.4% KO by turn 3.
- **P:** 1.02 / 0.97 turns, and 97.1% KO by turn 3.
- **Why it doesn't stall:** both walls recharge together, so a shutdown turn is followed by two open turns.
- **DC/strict** (the defender walls only when a threat is ≥40% of the target's current HP): 89.6% (V1) and 92.9% (P).
- For comparison, engine 18.2% and R 12.4%. Their walls last 2 turns or are % screens.
- **Verdict:** double coverage is a one-turn "turtle" with a built-in punish window. It is safe under P.

### 4.7 AP cost
- **At today's 3 AP**, the bank-or-cast choice decides everything:
  - Under V1, DD-B burns 25.52 AP a battle on walls, and the defending team deals 250.9 damage vs 393.4 without walls.
  - The attacking side then *wins more* (T-WIN DD-B: 96.3% vs 22.4% with no walls). Walls became a tempo sink: the defenders stop attacking.
- **Under P:**
  - DD-B: 6.32 AP, 365.5 damage, attacker win 28.6%.
  - CK-B: 2.01 AP. The single turn-1 cast is affordable.
- **Win % is the most AI-dependent number here.** Use it for direction only.

### 4.8 How often the priority counter matters
- **In the sim** (CK-E/jab), Crackett lands 0.95 Quick Jabs a battle on the target under P, and all 0.95 resolve before a same-turn wall.
- **Statically**, only Crackett, Forkrook and Rattlestake learn a priority attack:
  - Crackett and Forkrook are faster than every wall learner 100% of the time.
  - Rattlestake is faster than Gilarime 95% of the time, and wins the mirror 45.5% of the time.
- **But a priority attack is never the counter.** It lands *before* the wall goes up, so the wall still blocks the real hit.
- **What would make it a counter:** "a priority hit breaks a wall", or the Wallbreaker stamp (4.3-b, Legendary: PRIOR + first hit bypasses barriers). That is open question Q4.

## 5. Design gaps (Task C)

1. **Can the wall user's ally simply be hit? Yes, and that is the main leak.**
   - A wall covers one Chainling for one turn. The attacker can always hit the other one.
   - Under P, Crackett's retarget line still kills the target by turn 3 in 93.1% of battles. It also deals 246.8 damage to the walling team, vs 227.9 when naive.
   - The caster has spent its action on the wall, so walling an ally also means not attacking.
   - Under P this trade is fair. Under V1, at 3 AP, it is a tempo sink (section 4.7).
2. **The other category walks straight through** (CR 93.3%, PS 62.8% turn-1 under P, CK-E/rng 63.6% turn-1).
   - The hard counter only applies to one-sided teams. Most 2v2 teams have both categories across their two Chainlings.
   - The wrong-category wall is a dead card: NHFh, a ranged Barrier Wall vs a melee Hard Fork, is 97.6% = none.
   - This is the honest price of "instantly understandable".
3. **Priority is binary, and speed decides it before the battle starts** (section 4.8).
   - There is no ranged priority attack, so **nothing can pre-empt a Barrier** without a stamp.
   - A faster priority hit only chips the target; it doesn't beat the wall.
   - If Cronus wants "faster priority beats it" to be a real counter, it has to be a rule: "a hit that resolves before the wall goes up, on the same turn, cancels it". Or rely on Wallbreaker (4.3-b). See Q4.
4. **Statuses, DoT and riders.** Proposed: a blocked hit **does nothing**: no damage, no on-hit status, no chemistry status, no on-hit passive.
   - The engine today still applies statuses and chemistry through a full-absorb barrier. Changing that is a code change.
   - These still go through, because they are not hits: DoT / end-of-turn ticks; pure status moves (e.g. Cipher Lock), which are "StatusUtility", not melee or ranged; and AP drains from utility moves.
   - That matches "walls stop hits" and keeps the card text short.
   - The one existing hard breaker is the **Isolated** chemistry status ("cannot receive ally Support moves", `StatusId.cs:38`). It comes from a **Gas** or **Burned** hit on a **Staked** target. It stops an ally's wall, not a self-cast. That is narrow and fine.
5. **Every-other-turn rhythm.**
   - **With a 1-turn recharge** the rhythm is the problem (section 4.4): it lines up exactly with 4-AP heavies and locks them out (DD-B mean 11.25, HS-B 8.55, NCR 9.76).
   - Holding the heavy for one turn is a real read (CR-H/wait 56.3% by turn 3 under V1). Players will find it, but the AI and new players won't.
   - **With a 2-turn recharge** (P) the wall is up one turn in three. That leaves a visible punish window and no lock.
   - Under P the read still helps vs barriers (CR-H/wait 57.9%). Vs Hammer Sync it is worse than firing into the wall (HS/wait 3.4%).
6. **Moves that don't fit melee/ranged.**
   - **Ice Wall** (all-category 25% today): proposed melee Wall. Its "+1 AP if faster" rider is kept but reworded for a block. Untested, because the sim counts the rider only on landed hits.
   - **Lattice Guard** (40-damage pool + 20% reflect): proposed ranged Barrier, rename **Lattice Barrier**. It reflects 20% of the blocked hit's damage. Reflect is not self-damage. Not in any pool, so untested.
   - **Vapor Barrier** (any hit within 2 turns): becomes a ranged Barrier, this turn only. Not in any pool.
   - **Ash Guard** (20% for 2 turns, 5% heal): rename **Ash Barrier**, ranged; keeps the heal. Not in any pool.
   - **Compound Guard** (Rattlestake, Cofi): ranged Barrier plus its MeleeDef +1, rename **Compound Barrier**.
     - Making it a melee Wall instead (V2cgm) creates a two-melee-wall lock on Rattlestake (NHF mean 10.06) and frees Cryo Ray (NCR 54.9% by turn 3).
     - As a ranged Barrier, Rattlestake/Cofi get one hard Barrier plus a soft melee buff.
   - **Barrier Wall** (Humpacked): it has both words in its name. Proposed ranged, rename **Buffer Barrier**.
   - **Guard Posture** (Common, 1 AP, self): **not** a Wall or Barrier. It stays a 25% next-hit softener with no PRIOR and no recharge.
     - A 1-AP Common full block with priority would be strictly better than the Uncommon walls.
     - Keeping it outside the family keeps the rule "Wall = melee block, Barrier = ranged block, Guard = soften anything".
7. **3-AP walls can't answer turn 1.** Chainlings start at 0 and gain +2 a turn, so a 3-AP Blockade has 2 AP on turn 1 (CK-B 92.2% turn-1 KO at 3 AP). All family moves go to 2 AP.
8. **One Wall + one Barrier per Chainling.** Engine exclusivity has to become per category, or the second cast deletes the first.
9. **Signature identity.** Under P, Ember Wall is "Blockade plus a 1 AP drain". That may be too thin for a Legendary signature (Q1).

## 6. Recommendation: **PROPOSAL "P"**

> **PROPOSAL P.** Every **Wall** blocks the next **melee hit**, and every **Barrier** the next **ranged hit**, on one ally or the caster. The block lasts **this turn only**. The move is **[PRIOR]**, has **Recharge 2**, and costs **2 AP**. A blocked hit deals no damage and applies nothing.

**Why P over V1–V4:**

| Need (from the brief) | V1 | V2 | V3 | V4 | **P** |
|---|---|---|---|---|---|
| Hard counter to a turn-1 one-sided burst (SC T1) | 0% | 0% | 1.3% | 0% | **0%** |
| No stall (STALL capped) | 0% | 0% | 0% | 0% | **0%** |
| No lock vs 4-AP heavies (DD-B mean) | 11.25 | 11.25 | 3.96 | 11.25 | **4.22** |
| Multi-hit is the counter (MULTI mean, none 4.49) | 9.71 | 9.83 | 8.76 | 4.98 | **4.87** |
| Cheap Blockade answers turn 1 (CK-B T1) | 92.2% | 92% | 92% | 92.2% | **0%** |
| Simple card text | yes | yes | % maths | yes | **yes** |

**Choices behind it:**
- **"One hit"** gives the multi-hit counter. It is also Cronus's own "1 absorbed attack" cap in its simplest form.
- **"This turn"** avoids the V1L stall.
- **"Recharge 2"** removes the parity lock.
- **"2 AP"** makes every wall castable on turn 1.

**Alternatives:**
- **V3** (50% for 2 turns) is the best runner-up if Cronus prefers softer walls. It has no lock and is best vs retarget. But it is not a hard counter, and it needs % maths on the card.
- **V2rc2** (Protect-by-category, 2-turn recharge) has the same lock-break as P (DD-B 4.21). It loses the multi-hit counter (MULTI 7.92).

## 7. Per-move rewrites (PROPOSAL P)

**What does not change:**
- Rarity, BaseType, learn pools and Targeting (SingleAlly; Guard Posture Self) all stay as they are.
- Ember Wall stays Cinislug's Legendary signature.

**Shared rules for every Wall and Barrier:**
- **Tags:** add `PRIOR` to the existing tags.
- **Recharge:** `RechargeTurns = 3`, shown as "Recharge 2".
- **AP:** 2.
- **Effect:** the data form `FullAbsorbBarrier:1:1:<id>` plus a new contact parameter (`Contact` for Walls, `Projectile` for Barriers).

**Card text template:**
- **Wall:** "[PRIOR] This turn, block the next melee hit on an ally or yourself. Ranged hits are not blocked. Recharge 2."
- **Barrier:** "[PRIOR] This turn, block the next ranged hit on an ally or yourself. Melee hits are not blocked. Recharge 2."

| MoveId | Name (new) | Type / Rarity (unchanged) | Pool (unchanged) | Kind | AP | Card text (full) |
|---|---|---|---|---|---|---|
| `ember_wall` | Ember Wall | Burned / Legendary | Cinislug (signature) | **Wall** (melee) | 2 → 2 | [PRIOR] This turn, block the next melee hit on an ally or yourself (1 hit; ranged hits are not blocked). The attacker whose hit is blocked loses 1 AP. Recharge 2. |
| `blockade` | Blockade | Block / Uncommon | Blockwyrm | **Wall** (melee) | 3 → 2 | [PRIOR] This turn, block the next melee hit on an ally or yourself (1 hit; ranged hits are not blocked). Recharge 2. |
| `ice_wall` | Ice Wall | Frozen / Uncommon | Gilarime | **Wall** (melee) | 3 → 2 | [PRIOR] This turn, block the next melee hit on an ally or yourself (1 hit; ranged hits are not blocked). If the protected Chainling is faster than that attacker, the protected Chainling gains 1 AP. Recharge 2. *(rider untested)* |
| `barrier_wall` | **Buffer Barrier** (rename) | None / Uncommon | Humpacked | **Barrier** (ranged) | 3 → 2 | [PRIOR] This turn, block the next ranged hit on an ally or yourself (1 hit; melee hits are not blocked). Recharge 2. |
| `compound_guard` | **Compound Barrier** (rename) | Staked / Uncommon | Rattlestake, Cofi | **Barrier** (ranged) | 3 → 2 | [PRIOR] This turn, block the next ranged hit on an ally or yourself (1 hit; melee hits are not blocked). The target also gains Melee Def +1. Recharge 2. |
| `vapor_barrier` | Vapor Barrier | Gas / Uncommon | (none) | **Barrier** (ranged) | 2 → 2 | [PRIOR] This turn, block the next ranged hit on an ally or yourself (1 hit; melee hits are not blocked). Recharge 2. *(untested, no pool)* |
| `ash_guard` | **Ash Barrier** (rename) | Burned / Uncommon | (none) | **Barrier** (ranged) | 2 → 2 | [PRIOR] This turn, block the next ranged hit on an ally or yourself (1 hit; melee hits are not blocked). The target heals 5% max HP. Recharge 2. *(untested, no pool)* |
| `lattice_guard` | **Lattice Barrier** (rename) | Shard / Uncommon | (none) | **Barrier** (ranged) | 1 → 2 | [PRIOR] This turn, block the next ranged hit on an ally or yourself (1 hit; melee hits are not blocked). The attacker takes 20% of the blocked hit's damage. Recharge 2. *(untested, no pool)* |
| `guard_posture` | Guard Posture | None / Common | (none) | **Guard** (not a wall) | 1 → 1 | Reduce the next hit on yourself by 25% (melee or ranged). Lasts 1 hit or 2 turns. *(no PRIOR, no recharge; unchanged)* |

**Notes on individual moves:**
- **Two splits across Walls and Barriers:** Burned has one of each (Ember Wall, Ash Barrier). Staked (Compound Barrier) gets a Barrier while Block (Blockade) keeps a Wall. Block remains the barrier *type* per the hard rule. "Barrier" as a move-kind word is separate from the type.
- **Compound Barrier's Melee Def +1** is a stage on the target. It keeps a small melee answer on a ranged card.
  - It stacks with stage caps as today.
  - Because it recharges, it can be cast at most once every 3 turns.
- **Ice Wall's rider** reuses `GainApOnDamageIfSpeedHigher`, triggered on the block rather than on damage (a small code change). If that is unwanted, drop the rider and Ice Wall plays as Blockade.

## 8. Cautions

1. **AI quality decides how these numbers play out in the live game.** The V results use a category-aware, AP-banking team plan.
   - Without the bank rule, 3-AP walls looked falsely weak (fixed during this study).
   - The live AI needs the same three behaviours: pick the right category, bank for the wall, and don't wall when no matching threat can fire.
   - Greedy walling (STALL/greedy) is punished hard under P: 95.3% KO by turn 3.
2. **Win % (T-WIN) is AI- and kit-dependent.**
   - Example: DD-B attacker win is 96.3% under V1 but 28.6% under P. The V1 defenders spend their game walling.
   - Use KO-turn numbers for balance and win % only for direction.
3. **Not simulated:**
   - The Ice Wall AP rider on blocks.
   - Lattice reflect, Ash heal and Vapor/Ash/Lattice in general (no species learns them).
   - Stamps on walls.
   - 4.3-b Wallbreaker vs P.
   - Swapping a walled Chainling out.
   - Human reads beyond "hold the heavy one turn".
4. **The category escape is by design.** Expect mixed-category teams to rise in value and one-sided teams (Crackett, Kodiax) to lean on a partner of the other category.
5. **Recharge semantics:** authored `RechargeTurns` counts the cast turn's tick. The card's "Recharge 2" must be authored as **3**. Authoring 2 gives the V1/V4 parity lock.
6. **Exclusivity:**
   - Until per-category slots exist, an ally's Wall and the target's own Barrier overwrite each other. Ship the code change with the data change.
   - Alternatively, keep exclusivity and accept no double coverage. That is safe: double coverage is self-limiting under P (section 4.6).
7. **Ember Wall's drain** is melee-only now. In the previous study (R) the drain was a large part of why Ember lines held.
8. **Multi-hit randomness:** Cinder Burst (2–4 hits) through a 1-hit block varies a lot. Expect complaints that it "sometimes kills through a wall".

## 9. Open questions only Cronus can answer

1. **Q1. Ember Wall identity.** Under P it is "Blockade + 1 AP drain". Options:
   - (a) Keep it melee-only.
   - (b) As the Legendary perk, it blocks the next hit of **either** category. That is R's Ember Protect: CR 0.4% by turn 3, PS 0% turn-1. It would break "walls = melee" for one card.
   - (c) Keep it melee and block the whole move (V1 style) as the perk.
2. **Q2. Recharge 1 or 2?** The data says 2, because 1 locks out 4-AP heavies. Is that slower rhythm acceptable for the "every other turn" feel Cronus pictured?
3. **Q3. One hit, one move, or a whole turn?** P blocks one hit, so multi-hits counter it. V2 (all hits of the category this turn) is the Pokémon-style Protect.
4. **Q4. Should a priority attack beat a wall?** Today it only lands first, as chip. Options:
   - (a) Leave it as is. Wallbreaker stays the stamp counter.
   - (b) A same-turn hit that resolves before the wall cancels it.
   - (c) PRIOR hits ignore walls.
   
   Also: should any native ranged priority attack exist?
5. **Q5. Renames:**
   - Barrier Wall → Buffer Barrier.
   - Compound Guard → Compound Barrier.
   - Ash Guard → Ash Barrier.
   - Lattice Guard → Lattice Barrier.
   
   Or keep the names and rely on a "Wall / Barrier" keyword pill.
6. **Q6. Blocked-hit riders.** Should a blocked hit skip its statuses and chemistry (proposed), or keep today's behaviour where they land?
7. **Q7. Double coverage.** Allow one Wall + one Barrier per Chainling (code change), or keep today's one-barrier rule?
8. **Q8. Ice Wall's AP rider.** Keep it (reworked for blocks) or drop it?
9. **Q9. AP 2 for all.** Blockade, Ice Wall, Barrier Wall and Compound Guard go 3 → 2, and Lattice goes 1 → 2. Is a flat 2 acceptable, or should one family member stay cheap or expensive as a deliberate trade?
10. **Q10. Guard Posture** stays a % softener outside the family. Agreed?

## 10. Full tables (generated, verbatim from `walls_pvp_v2_tables.py`)

- **Format:** "T1 / T3 KO % · mean KO turn", capped at 12.
- **4000 battles per cell.**
- **Seeds:** cells reused from the previous study keep its seeds.

<!-- generated by walls_pvp_v2_tables.py -->

### T-MAIN  Target KO % by end of T1 / T3 · mean KO turn (capped at 12)

| cell | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 0/95 · 2.22 | 0/0 · 12.00 | 0/5.3 · 4.77 | 0/4.9 · 4.58 | 0/4.9 · 4.58 | 0/65.2 · 3.52 | 0/4.9 · 4.58 | 0/5 · 4.59 |
| STALL, wall every legal turn | 0/95 · 2.22 | 0/0 · 12.00 | 0/5.3 · 4.77 | 0/95 · 2.50 | 0/95 · 2.50 | 0/66 · 3.71 | 0/95 · 2.50 | 0/95.3 · 2.18 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 0/5.4 · 4.49 | 0/0 · 8.54 | 0/0 · 9.83 | 0/0 · 9.71 | 0/0 · 9.83 | 0/0 · 8.76 | 0/0 · 4.98 | 0/0.3 · 4.87 |
| SC Seam Cut T1 -> Deghoul, ally Ember | 91.7/100 · 1.09 | 91.3/96.5 · 1.21 | 0/92 · 2.17 | 0/93.8 · 2.16 | 0/92 · 2.17 | 1.3/99.3 · 2.12 | 0/93.8 · 2.16 | 0/99.3 · 2.09 |
| PF Seam Cut + Hammer Sync focus -> Deghoul, ally Ember | 92.2/100 · 1.08 | 91.7/96.3 · 1.22 | 0/96.3 · 2.08 | 0/96.4 · 2.08 | 0/96.3 · 2.08 | 1.4/99.7 · 2.04 | 0/96.4 · 2.08 | 0/99.8 · 2.04 |
| PS Partition Shot (ranged) T1 -> Deghoul, ally Ember | 64.8/98.8 · 1.61 | 63.9/65.8 · 2.08 | 0/20.6 · 3.87 | 64.6/95.3 · 1.66 | 63.7/74.1 · 1.89 | 63/98.2 · 1.66 | 64.6/95.3 · 1.66 | 62.8/98.4 · 1.66 |
| CR Cryo Ray (ranged) -> Crackett, ally Ember | 0/93.2 · 2.21 | 0/0.4 · 4.93 | 0/0.4 · 5.09 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 |
| CR-H Cryo Ray -> Crackett, ally Humpacked (Barrier Wall) | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/28.7 · 3.75 | 0/0 · 4.84 | 0/0 · 4.84 | 0/0.5 · 4.80 | 0/0 · 4.84 | 0/0 · 4.83 |
| DD Deep Dig -> Keyper, ally Ember | 0/94.5 · 2.17 | 0/0 · 4.47 | 0/1 · 4.92 | 0/1.1 · 4.93 | 0/1.1 · 4.93 | 0/4.9 · 4.25 | 0/1.1 · 4.93 | 0/0.8 · 4.83 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0/95 · 2.14 | 0/64.2 · 2.90 | 0/94.7 · 2.13 | 0/0 · 11.25 | 0/0 · 11.25 | 0/7.5 · 3.96 | 0/0 · 11.25 | 0/0 · 4.22 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 0/95 · 2.22 | 0/63.6 · 2.86 | 0/63 · 2.84 | 0/0 · 6.84 | 0/0 · 6.84 | 0/6.2 · 3.98 | 0/0 · 6.84 | 0/0 · 4.16 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0/51.6 · 4.23 | 0/5.8 · 6.33 | 0/16.3 · 4.05 | 0/0 · 8.55 | 0/0 · 8.55 | 0/0.1 · 5.12 | 0/0 · 8.55 | 0/0 · 6.42 |
| NCR Cryo Ray -> Rattlestake, self Compound Guard | 0/57.2 · 2.92 | 0/8.5 · 3.99 | 0/7.8 · 3.99 | 0/0 · 9.76 | 0/0 · 9.87 | 0/0 · 4.14 | 0/0 · 9.76 | 0/0 · 4.94 |
| NHF Hard Fork -> Rattlestake, Compound Guard + Blockade | 0/94.7 · 2.24 | 0/20.9 · 5.71 | 0/10.7 · 6.56 | 0/0.3 · 5.55 | 0/0.3 · 5.55 | 0/10.5 · 5.05 | 0/0.3 · 5.55 | 0/0.3 · 5.55 |
| NHFh Hard Fork (melee) -> Humpacked, self Barrier Wall | 0/97.6 · 2.09 | 0/95.3 · 2.19 | 0/98.2 · 2.10 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Barrier + ally Ember | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/12.4 · 3.84 | 0/65.4 · 2.73 | 0/65.4 · 2.73 | 0/94.2 · 3.03 | 0/65.4 · 2.73 | 0/97.1 · 2.40 |
| DC, defenders wall only vs >=40% HP threats | 0.1/99.2 · 2.15 | 0/18.2 · 3.94 | 0/12.4 · 3.84 | 0/89.6 · 3.05 | 0/89.6 · 3.05 | 0.1/96.7 · 2.37 | 0/89.6 · 3.05 | 0/92.9 · 3.02 |

### T-SENS  Sensitivities, same cells and format

| cell | V3t3 | V1L | V2np | V2ap2 | V2cgm | V2rc2 | V4rc2 |
|---|---|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 0/60.4 · 3.99 | 0/0 · 10.24 | 0/95.6 · 2.12 | 0/4.9 · 4.58 | 0/4.9 · 4.58 | 0/5 · 4.59 | 0/5 · 4.59 |
| STALL, wall every legal turn | 0/60.1 · 4.26 | 0/0 · 11.21 | 0/95.5 · 2.15 | 0/95 · 2.50 | 0/95 · 2.50 | 0/95.2 · 2.18 | 0/95.3 · 2.18 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 0/0 · 9.39 | 0/0 · 9.99 | 0/5.3 · 4.10 | 0/0 · 9.83 | 0/0 · 9.83 | 0/0 · 7.92 | 0/0.3 · 4.87 |
| SC Seam Cut T1 -> Deghoul, ally Ember | 1.7/98.4 · 2.12 | 0/92.3 · 2.22 | 92.9/100 · 1.08 | 0/92 · 2.17 | 0/92 · 2.17 | 0/99.3 · 2.09 | 0/99.3 · 2.09 |
| PF Seam Cut + Hammer Sync focus -> Deghoul, ally Ember | 1.4/99.5 · 2.04 | 0/94 · 2.16 | 92.2/100 · 1.08 | 0/96.3 · 2.08 | 0/96.3 · 2.08 | 0/99.8 · 2.04 | 0/99.8 · 2.04 |
| PS Partition Shot (ranged) T1 -> Deghoul, ally Ember | 63.3/95.7 · 1.67 | 63.6/87.7 · 1.88 | 64/98.5 · 1.63 | 63.7/74.1 · 1.89 | 63.7/74.1 · 1.89 | 63.2/98.3 · 1.66 | 62.8/98.4 · 1.66 |
| CR Cryo Ray (ranged) -> Crackett, ally Ember | 0/93.2 · 2.22 | 0/93.2 · 2.22 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 | 0/93.3 · 2.21 |
| CR-H Cryo Ray -> Crackett, ally Humpacked (Barrier Wall) | 0/0.6 · 4.79 | 0/0.1 · 4.91 | 0/83.7 · 2.46 | 0/0 · 4.81 | 0/0 · 4.84 | 0/0 · 4.80 | 0/0 · 4.80 |
| DD Deep Dig -> Keyper, ally Ember | 0/3.9 · 4.25 | 0/1.1 · 4.69 | 0/94.6 · 2.16 | 0/1.1 · 4.93 | 0/1.1 · 4.93 | 0/0.8 · 4.83 | 0/0.8 · 4.83 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0/7.2 · 4.05 | 0/0 · 11.25 | 0/95 · 2.11 | 0/0 · 11.23 | 0/0 · 11.25 | 0/0 · 4.21 | 0/0 · 4.21 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 0/6.5 · 4.07 | 0/0 · 6.11 | 0/24.8 · 5.17 | 0/0 · 6.96 | 0/0 · 6.84 | 0/0 · 4.15 | 0/0 · 4.15 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0/0.2 · 6.31 | 0/0 · 8.57 | 0/9.1 · 4.02 | 0/0 · 8.12 | 0/0 · 8.55 | 0/0 · 6.83 | 0/0 · 6.83 |
| NCR Cryo Ray -> Rattlestake, self Compound Guard | 0/0 · 4.77 | 0/0 · 6.31 | 0/2.6 · 9.28 | 0/0 · 9.88 | 0/54.9 · 2.97 | 0/0 · 4.92 | 0/0 · 4.94 |
| NHF Hard Fork -> Rattlestake, Compound Guard + Blockade | 0/10.2 · 5.03 | 0/0.2 · 6.25 | 0/94.8 · 2.23 | 0/0.3 · 5.56 | 0/0 · 10.06 | 0/0.2 · 5.55 | 0/0.2 · 5.55 |
| NHFh Hard Fork (melee) -> Humpacked, self Barrier Wall | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 | 0/97.6 · 2.09 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Barrier + ally Ember | 0/68.2 · 3.28 | 0/23.9 · 4.65 | 0.1/99.1 · 2.14 | 0/64.5 · 2.73 | 0/65.4 · 2.73 | 0/97.2 · 2.41 | 0/97.2 · 2.41 |
| DC, defenders wall only vs >=40% HP threats | 0.1/96.6 · 2.37 | 0/76.5 · 3.49 | 0.1/98.6 · 2.22 | 0/89.2 · 3.12 | 0/89.6 · 3.05 | 0/93.2 · 2.98 | 0/93.2 · 2.98 |

### T-STALL  Stall rate (% of battles that reach the sim's 30-turn cap) and wall casts per battle

| cell | none | engine | R | V1 | V2 | V3 | V4 | P | V1L |
|---|---|---|---|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 0% · 0 | 99.8% · 29.97 | 0% · 1.01 | 0% · 1.01 | 0% · 1.01 | 0% · 1.05 | 0% · 1.01 | 0% · 1.01 | 0% · 2.11 |
| STALL, wall every legal turn | 0% · 0 | 99.8% · 29.97 | 0% · 1.01 | 0% · 1.13 | 0% · 1.13 | 0% · 1.35 | 0% · 1.13 | 0% · 1.05 | 0% · 4.05 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 0% · 0 | 1.1% · 7.84 | 0% · 3.72 | 0% · 3.68 | 0% · 3.72 | 0% · 2.58 | 0% · 2.74 | 0% · 2 | 0% · 3.76 |

### T-CK  Crackett (86 melee / 38 ranged Atk, no damaging ranged move) vs a melee wall on Deghoul. T1 / T3 KO % · mean KO turn

| attacker response | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| naive: Seam Cut into the target every turn | 92/100 · 1.09 | 91.8/97 · 1.19 | 0/91.6 · 2.18 | 0/93.5 · 2.16 | 0/91.6 · 2.18 | 1.7/99.5 · 2.11 | 0/93.5 · 2.16 | 0/99.4 · 2.09 |
| retarget: hit the other foe while the wall is ready | 92/100 · 1.09 | 0/63.9 · 3.40 | 0/90.5 · 2.24 | 0/93.8 · 2.16 | 0/93.6 · 2.16 | 0/35 · 3.81 | 0/93.8 · 2.16 | 0/93.1 · 2.21 |
| priority: Quick Jab the target while the wall is ready | 92/100 · 1.09 | 0/0.3 · 4.20 | 0/94.8 · 2.11 | 0/96.6 · 2.09 | 0/94.8 · 2.11 | 0/97.5 · 2.69 | 0/96.6 · 2.09 | 0/99.8 · 2.06 |
| ranged partner (Marrowhead) focuses the target | 98.1/100 · 1.02 | 97.9/99.4 · 1.05 | 0/98 · 2.04 | 63.9/100 · 1.36 | 63.9/100 · 1.36 | 93.3/100 · 1.07 | 63.9/100 · 1.36 | 63.6/100 · 1.36 |
| multi-hit partner (Kitsauto Blade Flurry) focuses | 94.7/100 · 1.05 | 95.2/95.3 · 1.15 | 0/94.7 · 2.11 | 0.3/99.8 · 2.02 | 0/94.7 · 2.11 | 14.9/100 · 1.86 | 0.5/100 · 2.01 | 0.2/100 · 2.02 |
| AP denial partner (Keyper Memory Leak on Cinislug) | 91.9/100 · 1.08 | 92.7/99.2 · 1.16 | 0/99.2 · 2.08 | 0/99.2 · 2.08 | 0/99.2 · 2.08 | 1.5/99.3 · 2.11 | 0/99.2 · 2.08 | 0/99.2 · 2.08 |
| naive, wall = ally Blockwyrm Blockade | 92.7/100 · 1.08 | 92.2/99.8 · 1.09 | 92.2/99.7 · 1.12 | 92.2/99.6 · 1.16 | 92/99.6 · 1.17 | 92/99.5 · 1.13 | 92.2/99.6 · 1.16 | 0/99.6 · 2.09 |
| retarget, wall = ally Blockwyrm Blockade | 92.7/100 · 1.08 | 92.2/99.8 · 1.09 | 0/12.4 · 4.34 | 92.7/99.5 · 1.15 | 92.4/99.4 · 1.17 | 91.9/94.7 · 1.22 | 92.7/99.5 · 1.15 | 0/99.6 · 2.08 |

### T-CKD  Same cells: Crackett's damage to the target per battle

| attacker response | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| naive: Seam Cut into the target every turn | 136.6 | 130.1 | 126.9 | 126.6 | 126.9 | 135 | 126.6 | 132.8 |
| retarget: hit the other foe while the wall is ready | 136.6 | 64.2 | 120.8 | 126.4 | 126.5 | 12.6 | 126.4 | 122.2 |
| priority: Quick Jab the target while the wall is ready | 136.6 | 44.4 | 163.2 | 163.8 | 163.2 | 149.5 | 163.8 | 165.3 |
| ranged partner (Marrowhead) focuses the target | 128.4 | 127.4 | 127.1 | 45.7 | 45.7 | 66.8 | 45.7 | 46.3 |
| multi-hit partner (Kitsauto Blade Flurry) focuses | 132.2 | 127.2 | 126 | 127.3 | 126 | 116.1 | 125.5 | 127.5 |
| AP denial partner (Keyper Memory Leak on Cinislug) | 136.9 | 133.2 | 132.9 | 132.9 | 132.9 | 134.9 | 132.9 | 132.9 |
| naive, wall = ally Blockwyrm Blockade | 136.1 | 135.8 | 135.1 | 132.7 | 132.6 | 133.7 | 132.7 | 132 |
| retarget, wall = ally Blockwyrm Blockade | 136.1 | 135.8 | 11.1 | 132.8 | 132.7 | 129.7 | 132.8 | 132.4 |

### T-RHY  Rhythm read (attacker holds the heavy while a matching wall is ready). T1 / T3 KO % · mean KO turn

| cell | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| CR-H naive | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/28.7 · 3.75 | 0/0 · 4.84 | 0/0 · 4.84 | 0/0.5 · 4.80 | 0/0 · 4.84 | 0/0 · 4.83 |
| CR-H attacker holds Cryo Ray while the wall is ready | 0/93.7 · 2.17 | 0/75 · 2.63 | 0/28.7 · 3.75 | 0/56.3 · 3.59 | 0/56.3 · 3.59 | 0/0.6 · 4.80 | 0/56.3 · 3.59 | 0/57.9 · 3.57 |
| HS naive | 0/32.2 · 4.76 | 0/0.1 · 4.87 | 0/0.1 · 5.61 | 0/0.1 · 5.61 | 0/0.1 · 5.61 | 0/0.1 · 4.95 | 0/0.1 · 5.61 | 0/0.1 · 5.63 |
| HS attacker holds Hammer Sync while the wall is ready | 0/32.2 · 4.76 | 0/8.5 · 4.70 | 0/3.4 · 5.84 | 0/16.9 · 5.32 | 0/16.9 · 5.32 | 0/0.1 · 5.13 | 0/16.9 · 5.32 | 0/3.4 · 5.84 |
| DD naive | 0/94.5 · 2.17 | 0/0 · 4.47 | 0/1 · 4.92 | 0/1.1 · 4.93 | 0/1.1 · 4.93 | 0/4.9 · 4.25 | 0/1.1 · 4.93 | 0/0.8 · 4.83 |
| DD attacker holds Deep Dig while the wall is ready | 0/94.5 · 2.17 | 0/0 · 5.40 | 0/26.2 · 4.03 | 0/34.2 · 3.82 | 0/34.2 · 3.82 | 0/5.1 · 4.36 | 0/34.2 · 3.82 | 0/26.2 · 4.03 |

### T-AP  AP the defending team spends on walls per battle · defending team damage dealt per battle

| cell | none | engine | R | V1 | V2 | V3 | V4 | P | V2ap2 |
|---|---|---|---|---|---|---|---|---|---|
| SC Seam Cut T1 -> Deghoul, ally Ember | 0 · 179.3 | 2.06 · 138.4 | 2.16 · 175.6 | 2.17 · 174.3 | 2.16 · 175.6 | 2.01 · 174.5 | 2.17 · 174.3 | 2.01 · 173.7 | 2.16 · 175.6 |
| CR-H Cryo Ray -> Crackett, ally Humpacked (Barrier Wall) | 0 · 217.1 | 1.69 · 186.3 | 1.86 · 239.7 | 3.05 · 276.7 | 3.05 · 276.7 | 2.01 · 266.6 | 3.05 · 276.7 | 1.95 · 304.4 | 2.03 · 303.5 |
| DD Deep Dig -> Keyper, ally Ember | 0 · 259.7 | 2.75 · 306.4 | 2.67 · 335.1 | 2.67 · 335.1 | 2.67 · 335.1 | 2.6 · 304.2 | 2.67 · 335.1 | 2 · 332.3 | 2.67 · 335.1 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 0 · 393.4 | 6.96 · 378.8 | 4.2 · 353.6 | 25.52 · 250.9 | 25.52 · 250.9 | 6.05 · 376.6 | 25.52 · 250.9 | 6.32 · 365.5 | 20.69 · 248.3 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 0 · 360.4 | 2.5 · 323.9 | 6.98 · 262.6 | 13.96 · 122.7 | 13.96 · 122.7 | 6.08 · 287.8 | 13.96 · 122.7 | 6.21 · 291.9 | 11.81 · 169.5 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 0 · 380.7 | 11.6 · 338 | 7.5 · 291.6 | 15.76 · 322.6 | 15.76 · 322.6 | 7.71 · 333.4 | 15.76 · 322.6 | 8.6 · 304.4 | 12.67 · 310.7 |
| NCR Cryo Ray -> Rattlestake, self Compound Guard | 0 · 224.2 | 5.7 · 174.7 | 3.14 · 190.8 | 14.05 · 197 | 14.13 · 198.4 | 3.21 · 189.5 | 14.05 · 197 | 2.58 · 219.2 | 9.4 · 244.3 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Barrier + ally Ember | 0 · 241.6 | 8.28 · 81.6 | 6.48 · 178.9 | 5.8 · 202.3 | 5.8 · 202.3 | 4.19 · 205.7 | 5.8 · 202.3 | 3.1 · 200.3 | 4.44 · 200.2 |
| CK-B Crackett vs ally Blockade | 0 · 213.8 | 0.25 · 190.9 | 2.06 · 226.8 | 0.25 · 195.9 | 0.25 · 196 | 0.24 · 192.5 | 0.25 · 195.9 | 2.01 · 266.8 | 2.17 · 268.4 |

### T-DC  Double coverage: turns per battle with both categories covered on the target · turns where every hit on the target was blocked

| cell | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| DC | 0 · 0 | 0 · 0 | 0 · 1.78 | 1.1 · 1.31 | 1.1 · 1.31 | 0.61 · 0 | 1.1 · 1.31 | 1.02 · 0.97 |
| DC/strict | 0 · 0 | 0 · 0 | 0 · 1.78 | 0.33 · 0.65 | 0.33 · 0.65 | 0.11 · 0 | 0.33 · 0.65 | 0.86 · 0.95 |

### T-PRIO  Priority hits on the target per battle · of those, landed before a same-turn wall on it

| cell | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| CK-E/jab | 0 · 0 | 0.95 · 0 | 0.98 · 0 | 0.98 · 0.98 | 0.98 · 0.97 | 0.95 · 0.95 | 0.98 · 0.98 | 0.95 · 0.95 |

### T-WIN  Attacking-team win % (AI-dependent; read with section 4.6)

| cell | none | engine | R | V1 | V2 | V3 | V4 | P |
|---|---|---|---|---|---|---|---|---|
| STALL self-Ember Cinislug vs Hard Fork + Hammer Sync | 22.6 | 99.8 | 5.1 | 3.9 | 3.9 | 30.8 | 3.9 | 3.9 |
| STALL, wall every legal turn | 22.6 | 99.8 | 5.1 | 30.6 | 30.6 | 28.9 | 30.6 | 31.8 |
| MULTI Blade Flurry (3 hits) vs self-Ember | 31.2 | 99.9 | 45.4 | 45.1 | 45.4 | 41.7 | 58.5 | 57 |
| SC Seam Cut T1 -> Deghoul, ally Ember | 99.9 | 100 | 99.4 | 99.8 | 99.4 | 100 | 99.8 | 99.9 |
| PF Seam Cut + Hammer Sync focus -> Deghoul, ally Ember | 98.8 | 100 | 95.2 | 95.3 | 95.2 | 97.2 | 95.3 | 97.6 |
| PS Partition Shot (ranged) T1 -> Deghoul, ally Ember | 98.7 | 99.8 | 79.2 | 99 | 96.7 | 99.5 | 99 | 99.5 |
| CR Cryo Ray (ranged) -> Crackett, ally Ember | 99.1 | 100 | 97.2 | 99.1 | 99.1 | 99.1 | 99.1 | 99.1 |
| CR-H Cryo Ray -> Crackett, ally Humpacked (Barrier Wall) | 100 | 100 | 100 | 100 | 100 | 100 | 100 | 100 |
| DD Deep Dig -> Keyper, ally Ember | 96.9 | 96.9 | 79.2 | 79.3 | 79.3 | 92.2 | 79.3 | 76.6 |
| DD-B Deep Dig -> Keyper, GP + ally Blockade | 22.4 | 29.3 | 42.3 | 96.3 | 96.3 | 18.7 | 96.3 | 28.6 |
| DD-I Deep Dig -> Keyper, GP + ally Ice Wall | 63.7 | 87 | 97.3 | 100 | 100 | 96.2 | 100 | 90.6 |
| HS-B Hammer Sync -> Deghoul, GP + ally Blockade | 13.8 | 51.2 | 83.7 | 37.6 | 37.6 | 61.5 | 37.6 | 62.8 |
| NCR Cryo Ray -> Rattlestake, self Compound Guard | 98.9 | 99 | 98.9 | 96.7 | 96.6 | 98.8 | 96.7 | 97.5 |
| NHF Hard Fork -> Rattlestake, Compound Guard + Blockade | 99.1 | 100 | 99.1 | 99.7 | 99.7 | 99.8 | 99.7 | 97.1 |
| NHFh Hard Fork (melee) -> Humpacked, self Barrier Wall | 61.2 | 83.5 | 84.2 | 61.2 | 61.2 | 61.2 | 61.2 | 61.2 |
| DC Cryo Ray + Kodiax focus -> Humpacked, self Barrier + ally Ember | 98.2 | 100 | 99.2 | 99.6 | 99.6 | 99.6 | 99.6 | 99.4 |
| DC, defenders wall only vs >=40% HP threats | 98.2 | 100 | 99.2 | 97.3 | 97.3 | 98.4 | 97.3 | 95.5 |


## 11. Reproduce and checker

```
cd /workspace/balance/out
python3 walls_pvp_v2_sim.py /workspace/balance/s10 4000   # ~3.5 min, writes walls_pvp_v2_sim.out.json (450 cells)
python3 walls_pvp_v2_tables.py > walls_pvp_v2_tables.md
python3 walls_pvp_v2_check.py                              # engine facts, regression, every table and prose number
```

Checker result (2026-10-20, run against s10 and walls_pvp_v2_sim.out.json): **858 PASS, 0 FAIL**. A mutation test (one table cell, one headline cell and one prose number edited) gave 3 FAIL, as expected.
