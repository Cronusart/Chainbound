# ARMOR MECHANIC V1 — analysis and proposals

Snapshot: `chainbound_combat_snapshot_20261021_0205` (local tree, 2026-10-21 ~02:05 CDT). Facts below are tagged `[Cxx]` and re-checked by `armor_v1_check.py` against that tree. Every damage, KO, win-rate, and AP figure is in the script-owned appendix. Suggestions are **PROPOSAL**.

## (a) Headline

Guard Posture is already a separate Armor tag in the CSV, and it is still a 25% window, not flat damage. [C01] guard_posture is in no learn pool, so the Hammer Sync + Guard Posture + Blockade line is not a real kit today. [C02] The tiered wall rules are in the CSV, the engine, and the server AP table. The Unity move assets were not reimported, so a play session still runs the old walls. [C03] [C04] [C05] [C06]

**PROPOSAL.** Armor is a flat subtraction on one hit, applied after the wall or barrier percent, then the existing 10% unrolled floor. Typeless Guard Posture subtracts 12. Typed Guard Stance subtracts 16. Flat armor on one Chainling adds up to 16. On Hammer Sync into Deghoul, a 60% Blockade plus today's 25% and a 60% Blockade plus flat 12 both leave 34. That is hammer open 115 down to the same integer. Flat 12 alone leaves 103, so a heavy still lands. A 24-point pool and all-hits flat are the stalls: Blade Flurry's three hits fall from 39 to 3, and the pool is the longest Hammer line in the 500-trial set.

The tank does not become unbeatable. Blockwyrm with Blockade and flat 16 still loses about 64% of focus-fire battles, and the 30-turn cap is 2%. A faster Seam Cut spends the wall, and the Hammer after it is the open hit.

Open questions are in section (k). Defaults: keep the stack, give Guard Posture to Deghoul, give Guard Stance to Blockwyrm, leave Immutable Ledger as a percent, keep Shattered Armor and Core Breach out of pools, reimport assets before any playtest.

## (b) Current state

### Guard Posture today

`Moves.csv:36` authors Guard Posture as StatusUtility, typeless, Common, Self, 1 AP, recharge 0, tag ARMOR, utility `MitigationWindow:25%:2:1:armor_guard_posture`. The card says the next hit is reduced 25%, lasts 1 hit or 2 turns, stacks with a wall or barrier, and that the numbers are unchanged pending this round. [C01] No species lists it. [C02]

`Move_guard_posture.asset` was not reimported. It still has effectType 0, magnitude 0.25, stack key `wall_guard_posture`, tag UTIL, and the old sentence "Reduce next hit by 25%". [C03] `IsWallTag` matches only a `StartsWith("wall_"` or `barrier_` prefix. The CSV key `armor_guard_posture` is not a wall. The asset key `wall_guard_posture` is a wall. [C10] Until reimport, the playable asset is a wall-tagged percent.

Mitigation stacks by a surviving product: `surviving *= (1f - perSource)` in `ChainlingInstance.cs:607`. [C08] The tiered test `GuardPosture_IsArmor_StacksWithBlock_NotABlockTarget` asserts 0.25 and 0.50 become `Is.EqualTo(0.625f)`. [C11] Armor does not occupy the one-block slot.

### How far tiered v1 got

The plan frontmatter still has all nine todos at `status: pending` (`p0` through `p8`). [C13] The P0 audit walls_barriers_tiered_v1_tests.md is absent. [C14] The code is ahead of that checklist.

Done in CSV, legend, engine, and server:

| Piece | Where |
|---|---|
| Eight family rows are BlockWindow, PRIOR, RechargeTurns 2, card text says Recharge 1, 1 hit, category split | [C04] Blockade is `BlockWindow:60%:Melee:2:wall_blockade` |
| ARMOR tag and BlockWindow opcode, hit budget fixed at 1 | [C25] `hit budget is fixed at 1` |
| AddBlock refuses a second intact block unless override; the stored hit budget is forced to 1 | [C08] |
| ApplyBlockWindow calls AddBlock with `@override: true` | [C09] |
| Ice Wall's reactive AP returns early when the move places a block, so the +1 is on the block, once, if the holder is faster | [C09] |
| Eligibility denies a target that already holds a block; the resolver returns at `CombatResolver.cs:1662` before the action log | [C11] test `Resolver_BlockedCastSafetyNet_PassNoApNoRechargeNoEffects` |
| Voluntary swap calls `ClearTemporaryDefenses` after the AP-continuity comment at `CombatResolver.cs:6743`. Timers run at line 482 before status ticks | [C24] |
| Server `move_ap.ts` matches the new costs, including `"guard_posture": 1` | [C06] |
| `BLOCK_MOVE_IDS` is the eight family ids and does not include guard_posture. `manifestHash=92d83d7d37ca6614acf4170869b645bb9b94f57f1c30ac5fdf563e4c41cb6f02` | [C07] |
| Wallbreaker `4.3-b` and Ignore Walls `4.8-a` set `barrierBypassChance` to 1. Bypass skips `blockKind` sources and does not consume the block. First hit only | [C21] `DamageCalculator.cs` skips block sources when `bypassBlocks` is set |

Not done, or only partly done:

- Move assets still show `displayName: Barrier Wall`, Compound Guard, Ash Guard, Lattice Guard, old AP, FullAbsorb (`effectType: 2`) on Ember and Vapor, and a 40-damage threshold on Lattice. [C05] Play mode is the old data.
- `ApplyBlockWindow` uses override for ordinary move casts. The plan reserved override for passives and modifiers. The selection gate is what stops a second block. A cast that reaches the executor replaces the old block. [C09]
- core_breach is not a Moves.csv row. The test builds a move in memory and sets `ignoreMitigation`. [C15]
- `CombatIssuesRemediationV2Tests.cs:356` still expects `MitigationWindow:25%:2:-1:blockade`. The CSV does not. That test fails on this tree. [C12]
- Thirteen plan test names are absent, including the multi-hit name, Ice Wall's 1 AP test, Lattice reflect, riders-still-apply, swap-clears-blocks, and timers-before-status. A related four-hit test and an Ember drain test do exist. [C11]
- Config is the decided combat math: `enableStab: 0`, floor 0.1, +2 AP per turn, cap 10, chemistry 1.2 / 0.8, crit 1.5, roll 0.85–1. [C22] The floor is `power * (atk / def) * levelFactor * mitigationFloor` at `DamageCalculator.cs:196`. It ignores roll, chemistry, and crit. [C23] Final damage may be 0. There is no forced minimum of 1.

### Passives and stamps today

All of these are percents. None are Armor. None use a wall prefix, except the stale Guard Posture asset key.

| Source | Today | Check |
|---|---|---|
| Brace 6.4-a | 10–15% on swap-in, default `return 0.125f`, rest of this turn | [C20] |
| Cover Ally 6.4-d | 8–12% on an ally at swap-in | [C20] |
| Fade Guard 6.4-g | 5–10% on the replacement at swap-out | [C20] |
| Shield Link 7.1-b | 12–18%, default 0.15, ally, rest of this turn, no stack key | [C20] |
| Grit Guard 6.3-c | 15–25% below 30% HP, default 0.20, rest of this turn | [C20] |
| Sparse Ward 6.8-e | 3–8% per empty bench slot, default 0.05, rest of this turn | [C20] |
| Chemistry Firewall | 30% projectile, 1 hit, stack key `Chemistry.Firewall`. Not a wall | [C10] [C21] |
| Immutable Ledger | Blockwyrm's signature. CSV utility empty. Hardcoded 20% on the first hit each turn, plus +1 AP | [C19] |
| firewall_passive / defensive_reflex | Hardcoded 25% projectile / 20% any contact. Not in a pool | [C19] path in `ApplyDefensivePassiveMitigation` |
| Shattered Armor | Passive, Shard, Rare, utility empty. `defId == "shattered_armor"` grants 5% for the rest of the turn and +1 AP on a super-effective hit, once per turn. Not in a pool | [C17] |
| Gecko Guard | Passive, Frozen, Legendary, recharge 999, below 50% HP once: Melee Def +2 and Speed +1. `gecko_guard is not on Gilarime`. Cryo Ray is the signature | [C18] |
| Guard Stance | Block, Common, 1 AP, Self, `MeleeDef:2:1.0:true`, utility empty, not in a pool. +2 Melee Def doubles that stat under the stage formula | [C16] |

Current wild pools already pass `PlayableLearnPoolsFollowTheBodyBand`. [C26] Starters are Validape, Gilarime, and Kitsauto. Species assets record blockwyrm rarity 4, Deghoul rarity 0 (HP 106, Speed 72), and Crackett rarity 3 (Speed 92). [C32]

### Round-14 sims diverge

`tempo_2v2_sim.py` and `walls_pvp_v2_sim.py` predate BlockWindow. They are copied into the artifacts unchanged. Do not point them at this snapshot.

- Tempo parses `MitigationWindow` and `FullAbsorbBarrier` only. A BlockWindow move does nothing there.
- `is_wall_key` is a substring test (`wall`, `barrier`, or `k == "blockade"`). The engine is prefix-only. [C10] [C31]
- `WALL_MOVES` includes `guard_posture`, so wall counts include Armor.
- Tempo leaves mitigation on a swapped-out body unless `walls_drop_on_swap` is set. The engine clears on swap. [C24]
- Tempo pays Ice Wall AP on later hits. The engine pays +1 once on consume.
- Tempo forces `max(1, math.floor(dmg))`. The engine can floor to 0.
- `walls_pvp_v2_sim.py` says `One block per category`. The engine refuses any second block, any category. [C08] [C31]

`armor_2v2_sim.py` is the extension that models this snapshot: BlockWindow, one block, swap clear, armor models, category filters in the battle loop. Its formula table has a labeling bug: pack names that do not start with `wall` still apply a melee 60% to ranged moves. Cryo Ray `both25_60` in that JSON is 18. A real Buffer (25%) plus 25% armor is 35. ranged pack names mis-apply melee walls. [C29] Cited ranged numbers below are the checker's category-correct cells. Battle rows use the contact filter and are safe.

Typeless moves inherit the attacker's primary type: `return attacker.primaryType;` [C23] The design rule says typeless is excluded from type effectiveness. The sim follows the engine. Pulse Shot from Cinislug (Burned) into Blockwyrm (Block) is a disadvantage hit. STAB is off. [C22] Hammer Sync is Node into Gas, disadvantage. Deep Dig is Mined into Encrypted, advantage. [C28]

## (c) Armor model

Three shapes, same duration choice (1 hit or 2 turns, matching Guard Posture), same 1 AP on the typeless common. Order is wall percent then flat, unless the row says flat-first. Roll 0.925, IV 0, no crit unless named. Numbers are the appendix cells.

| Model | What the card says | Hammer into Deghoul, no wall | Same, after Blockade 60% | Blade Flurry, 3 hits, Ember on hit 1 |
|---|---|---:|---:|---|
| Percent 25% (today) | "25% less" | 86 | 34 | hit 1 is the percent stack; three open hits total 39 |
| Flat 12 after the wall, one hit | "the next hit deals 12 less" | 103 | 34 | ember + 16 on hit 1 then full = 27; open = 39 |
| Flat 16 after the wall, one hit | "the next hit deals 16 less" | — | 30 | ember + 16 on hit 1 then full = 27 |
| Flat 16 before the wall | the 16 is not what the player sees | — | 39 | — |
| Flat 16 on every hit | "each hit deals 16 less" | — | — | ember + 16 on every hit = 3 |
| Pool 24 | a shield that drains | — | worst battle row, not a single hit | — |

Reading the cells:

- Hammer open is 115 (chem disadvantage, floor 15.56, floor does not bind). Wall 60% leaves 46. Both 25% and 60% leaves 34. Flat 12 after that wall also leaves 34. Flat 16 leaves 30. Flat 16 before the wall leaves 39. A crit open is 173; a crit through both percents is 52.
- Hammer into Blockwyrm is neutral, open 61, floor 6.51. Wall 60% leaves 24. Flat 16 after the wall leaves 8. Ledger 20% plus that wall plus flat 16 leaves 6, which is the floor.
- Snap Strike into Deghoul is open 43, floor 5.83. Wall 60% leaves 17. Both percents leave 13. Flat 12 after the wall leaves 5. The floor binds. Flat 12 with no wall leaves 31.
- Deep Dig into Keyper is advantage, open 141. Wall 60% leaves 56. Both percents leave 42. Flat 12 after the wall leaves 44.
- Cryo Ray into Humpacked is disadvantage, open 62. Buffer Barrier (25%, the real barrier) leaves 47. Flat 12 with no wall leaves 50. Flat 16 leaves 46. A melee wall does nothing to this hit.
- Pulse Shot into Blockwyrm is open 17, floor 2.27. Buffer leaves 13. Flat 12 leaves 5. Flat 16 leaves 2. The floor binds.
- Blade Flurry, one hit pre 13.3, floor 1.69, open 13. Three open hits total 39. Ember on hit 1 then full totals 32. Ember plus flat 16 on hit 1 then full totals 27. Ember on hit 1 and flat 16 on every hit totals 3.

**PROPOSAL. Use flat-after, one hit, cap 16.**

The card can say "The next hit deals 12 less." That is the whole effect. Percent armor is stronger on heavies (Hammer 86 vs flat-12's 103) and weaker on a walled chip hit (Snap 13 vs flat-12's 5). Flat is the anti-chip tool. It does not replace a wall on a heavy.

Reject flat-before. Hammer after a 60% wall is 39 if the 16 comes off first and 30 if it comes off after. The player cannot see "16" in the 39.

Reject all-hits flat and a 24 pool. All-hits turns a 39-damage flurry into 3. The pool is the longest Hammer battle in the set (section g). One-hit flat still punishes a multi-hit, because the wall and the armor both die on hit 1 and hits 2 and 3 are full. That is the decided wall rule, applied to Armor.

Duration stays "1 hit or 2 turns, whichever first." No recharge on the Armor moves. Walls keep recharge 1. Armor is not a second wall, so it does not take the wall's recharge. Recasting is allowed. It still only eats one hit.

No priority on Armor. A faster 2 AP hit should land before a slower body finishes armoring. Walls already have PRIOR.

## (d) Stacking and the floor

**PROPOSAL. Order on one hit:**

1. Power, attack/defense, roll, chemistry, crit, conditional (unchanged).
2. Wall or barrier percent, then other percents (Ledger, Firewall, leftover percent passives), as the surviving product that already exists.
3. Subtract flat Armor, after the cap of 16.
4. Apply the existing unrolled 10% floor.
5. Consume the one-hit block and the one-hit armor. A multi-hit spends both on hit 1. Later hits are full. A partly blocked hit still applies its status.

The floor is the lock brake that already ships. It does not bind on Hammer (15.56 against a 34 result). It does bind on Snap + Blockade + flat 12 (5) and on Pulse + flat 16 (2). Those hits still deal damage. They are one hit, and the defender spent AP to get them.

Do not add a second percent floor for "block + Armor." A floor set at the wall's own surviving fraction is the nostack rule under another name. A floor high enough to speed up Hammer + Blockade would nerf Blockade itself. Blockade stays a Greater wall at 60%, 3 AP, PRIOR. That decision stands.

**HS-B.** The line is counterfactual until someone learns Guard Posture. [C02] On the modeled kit (Deghoul casts Guard Posture, Blockwyrm casts Blockade):

- No wall and no armor: mean KO 6.1, 49.8% by turn 3, heavy hit 94.41, attacker battle win 4.8%. The low win is the defenders hitting back.
- Blockade only: mean KO 5.23, 0% by turn 3, heavy 47.82, attacker win 54.4%. The defender spends 5.16 AP on walls and attacks less, so the attacker wins the battle more often while the target lives about as long.
- Percent armor plus Blockade: mean KO 8.71, 0% by turn 3, 0% at the 30-turn cap, heavy 45.75, attacker win 19.0%. Armor casts 3.46, wall casts 2.41, AP on walls 7.24, AP on armor 3.46.
- Flat 12 after the wall: mean KO 8.71, heavy 47.96, attacker win 17.6%, cap 0%. Same length as today's percent.
- Flat 16: mean KO 8.75, heavy 45.64, cap 0%.
- Flat 20: mean KO 9.26, heavy 41.72.
- Pool 24: mean KO 9.26, heavy 38.91, attacker win 14.0%. Worst stall in the set.
- All-hits flat 16: mean KO 8.99, cap 0%.
- Flat 16 before the wall: mean KO 8.31, heavy 51.75. Kinder on the heavy, harder to read.
- Nostack (armor skipped on a hit the block already covers): mean KO 8.10, heavy 53.38, attacker win 23.6%. The shortest stacked variant. It makes "stacks with a wall" false on the hit both cover.

The length is Blockade's 60% plus 3 AP plus PRIOR on a slow partner, plus the armored body spending 1 AP and not attacking. Flat 12 does not make that line longer than the percent the CSV already authors. Nostack is the lever if 8.71 turns is unacceptable. It is not the default, because Cronus already said Armor stacks with a wall.

Armor with no wall, flat 12: mean KO 5.17, 34.6% by turn 3, heavy 94.34. The same kit at 25% is mean KO 7.04 and 7.4% by turn 3, heavy 89.26. Flat 12 gives the heavy its damage back. That is the reason to leave percent.

Ledger stays a percent. On Blockwyrm, Ledger plus Blockade plus flat 16 puts a non-crit Hammer on the floor (6). That is one hit: Ledger is the first hit of the turn, Blockade is one hit, Armor is one hit. The tank trace shows why it is not a lock. Seam Cut is faster, spends the wall for 4, and the Hammer after it is 61.

## (e) Armor move set

Four moves. Two are conversions. One is a catalog shred. One is the missing D2 row. No new names beyond Core Breach, which the wall plan already required and the CSV never received.

| Move | Type | Rarity | AP | Armor | Duration | Priority | Recharge | Rider | Who learns it |
|---|---|---|---|---|---|---|---|---|---|
| Guard Posture | None | Common | 1 | 12 | 1 hit or 2 turns | no | 0 | none | **PROPOSAL** Deghoul replaces Snap Strike |
| Guard Stance | Block | Common | 1 | 16 | 1 hit or 2 turns | no | 0 | none | **PROPOSAL** Blockwyrm replaces Stone Barrage |
| Shattered Armor | Shard | Rare | 2 | — | — | no | 0 | power 34 melee; strip the target's Armor before the hit; walls stay | catalog only |
| Core Breach | Mined | Uncommon | 3 | — | — | no | 0 | power 32 melee; `ignoreMitigation`; consumes neither block nor armor | catalog only |

Typed common Guard Stance (16) beats typeless common Guard Posture (12). A rare typeless move is the benchmark for a typed common; this pair is both common, and the typed one is the larger number. Core Breach at 3 AP is the expensive answer, not a staple. Shattered Armor at 2 AP and power 34 is a typed rare above typeless Snap Strike (power 30) and below Seam Cut (power 46, and `ConditionalDamageMultiplier` 1.25). [C33]

**Guard Posture card (PROPOSAL).** "Armor · 1 AP. The next hit deals 12 less, after a Wall or Barrier. Lasts 1 hit or 2 turns. Any attack can spend it. Stacks with one Wall or Barrier. Armor on this Chainling adds up to 16. A minimum hit still applies."

**Guard Stance card (PROPOSAL).** "Armor · 1 AP. The next hit deals 16 less, after a Wall or Barrier. Lasts 1 hit or 2 turns. Any attack can spend it. Stacks with one Wall or Barrier. Armor on this Chainling adds up to 16. A minimum hit still applies." Drop the Melee Def +2. That stage doubles melee defense for as long as it lasts and rivals a wall without saying so. [C16]

**Shattered Armor card (PROPOSAL).** "2 AP. Contact hit. Remove the target's Armor, then deal damage. Does not break a Wall or Barrier." Delete the hardcoded 5%. [C17]

**Core Breach card (PROPOSAL).** "3 AP. Contact hit. Ignore Armor, Walls, and Barriers. Does not spend them." This is the D2 default the test already assumes. [C15]

### Pools and the band

The band in `_plans/learn_pool_rarity_6d7ff36f.plan.md`, enforced by `PlayableLearnPoolsFollowTheBodyBand`, is a hard constraint. [C26] Signature strictly rarer than every other pool move. Wild pools are five moves. At most one non-signature move is exactly one step above the body. Starters skip the five-move and one-above counts. Ghost and Cofi stay outside.

| Swap | Band | Decision |
|---|---|---|
| Deghoul: `baseline_striker` → `guard_posture` | Body Common. Signature Transaction Tax is Epic. Volatile Shot (Uncommon) stays the one above-body move. Guard Posture is Common. Size stays 5. above-body stays 1 | **PROPOSAL** do this. `deghoul swap keeps the band` [C27] |
| Rattlestake: `baseline_jab` → `guard_posture` | Also legal. Compound Barrier stays the one above-body move. Signature is AP Reserve | Legal alternative. Do not do both. Default is Deghoul, so the Hammer line is a real pool |
| Blockwyrm: `stone_barrage` → `guard_stance` | Body Legendary. Signature Immutable Ledger stays the only Legendary. Bulwark Blow (Epic) is below the signature. above-body stays 0. Size stays 5 | **PROPOSAL** do this [C27] |
| Marrowhead plus Shattered Armor, Partition Shot kept | Size 6 and two Rares above an Uncommon body | Illegal. Do not do it [C27] |
| Gilarime plus Gecko Guard | Two Legendaries. Signature would not be unique | Illegal. `gecko_guard is not on Gilarime` [C18] [C27] |
| Crackett: Quick Jab → Shattered Armor | The rarity band would allow a Rare under an Epic signature | **PROPOSAL** do not take Quick Jab. It is the fast tempo tool. Shattered Armor stays catalog-only |

Gecko Guard stays the threshold passive it is. Do not turn it into Armor and do not return it to Gilarime.

No empty move has a name that means "grant Armor to an ally." Damage Intercept and Signal Redirect are redirects. Proof Shield is a seal. Team Buffer is Def +1. Ally Armor is the Shield Link stamp (section f), not a retitled move.

### Counterplay that already exists

- Speed. Crackett is faster than Deghoul. [C32] In the fast-chip battles, Armor casts average 0.09 and the target is KO'd on turn 1 in 99.2% of trials. Armor does not answer a hit that already landed.
- One-hit budget. A multi-hit spends Armor and the wall on hit 1. The trace of flat 16, one hit, shows a flurry of 1 then 13 then 13 once both are up.
- Focus fire. In the tank trace the faster Seam Cut KOs the partner, then spends Blockade for 4, and the Hammer after it is 61. Blockwyrm ends at 0 HP on turn 7. Attacker wins.
- Swap. `ClearTemporaryDefenses` drops Armor and blocks. [C24] The swap trace sends Deghoul out on turn 2. The Hammer into the naked body on turn 4 is 115, which is the open hit. That policy does not cast Guard Posture, so its armor-cast count is 0. It measures the swap, not the flat number.
- Wallbreaker and Ignore Walls skip the block on hit 1 and leave it standing. Armor still applies. [C21]
- Core Breach, once the row exists, ignores both and spends neither. [C15]
- Shattered Armor, once converted, strips Armor only.

## (f) Passives and stamps

| Source | **PROPOSAL** | Why |
|---|---|---|
| Brace | Become flat Armor 6, rest of this turn, every hit that turn, under the cap of 16 | The 12.5% default is a small percent. 6 is readable and dies into the cap if a move Armor is also up |
| Cover Ally | Flat 5, same window, on the ally | Same conversion, smaller |
| Fade Guard | Flat 4 on the replacement | Same |
| Shield Link | Flat 8 on an ally, rest of this turn | This is the ally-Armor button. 8 is under Guard Posture's 12 so a stamp is not a better move |
| Grit Guard | Flat 8 while below 30% HP, rest of this turn | The old 20% on a low target was a second wall. 8 under the cap is a comeback, not a lock |
| Sparse Ward | Flat 4 per empty bench slot, max 8, rest of this turn | Two empty slots hit the cap contribution. They still share the 16 with move Armor |
| Immutable Ledger | Stay 20% on the first hit each turn, plus +1 AP. Not Armor | It is Blockwyrm's signature. Folding it into the flat cap would erase the card. It still multiplies, then flat comes off |
| Chemistry Firewall | Stay 30% projectile, 1 hit. Not Armor. Not a wall | Chemistry, not a posture |
| firewall_passive, defensive_reflex | Stay percent until they have a pool. Not Armor | No learner, no reason to widen them |
| Shattered Armor | Stop being a passive. Become the shred in section (e) | The 5% on a super-effective hit does not match the name |
| Gecko Guard | Stay the threshold passive | The name is a stance, the effect is a once-per-battle stage. Leave it |

Stamp flats were not in the 500-trial matrix. The cap is what keeps them from stacking into a second wall. A move Armor of 16 plus Brace 6 on the same hit resolves to 16, not 22. The next hit that turn is Brace only.

## (g) 2v2 results

`armor_2v2_sim.py`, 500 trials, `trials=500`, 57 jobs. [C30] IV ±5, seeds from a stable checksum. Attacker win is the battle, not the target KO. `tko_mean` counts a living target as 12. Cap is the share still going at 30 turns. Heavy damage is the line's heavy; it is empty when the line has no heavy (flurry and chip).

What the rows say, in words:

- Flat 12 on the Hammer + Blockade kit matches percent length (both mean KO 8.71) and does not cap.
- Flat 12 with no wall shortens that KO from 7.04 to 5.17 and lifts turn-3 KOs from 7.4% to 34.6%.
- Deep Dig + Blockade + percent: attacker win 28.6%, mean KO 6.35, cap 0%. Flat 12: win 27.2%, mean KO 6.05. Nostack: win 34.2%, mean KO 4.54. Same shape as Hammer, shorter because Deep Dig is advantage (open hit 141).
- Blade Flurry vs Ember + Armor. Percent: attacker win 73.6%, mean KO 4.58, cap 0%. Flat 16 one-hit: win 98.4%, mean KO 6.85, cap 0%. All-hits: win 99.4%, mean KO 7.56, cap 0%. Pool: win 97.0%, mean KO 7.26. The target lives longer. The attacker still wins, because the defender spent AP walling. All-hits does not create a 30-turn lock. It does delete the flurry in the formula (39 down to 3), which is enough to reject it.
- Fast chip (Seam Cut): turn-3 KO about 99%, Armor almost never cast. Armor does not matter.
- Slow chip (Snap Strike, Deghoul armors first). Percent mean KO 4.2, turn-3 27%. Flat 12 mean KO 4.24, turn-3 28%. Flat 16 mean KO 4.76, turn-3 13%. All-hits mean KO 6.28, turn-3 4.4%, attacker win still 85.2%, cap 0%. One-hit flat 12 matches percent. All-hits is the only model that stretches this fight, and it still ends.
- The open slow-chip kit (no Guard Posture) has mean KO 5.28 and attacker win 54.8%. It still has Volatile Shot, which the Armor kit replaced. Do not read that gap as pure Armor value.
- Cryo Ray vs Buffer + Armor: attacker win 98.6%, mean KO 2.85, heavy 80.37. Flat 16: win 98.8%, mean KO 2.63, heavy 80.52. A ranged heavy still deletes a barrier user.
- Tank, focus fire, Blockade + Armor on Blockwyrm. Open (no wall, no armor): attacker win 56.6%, mean KO 6.88, heavy 83.14, cap 0%. Percent: win 65.4%, mean KO 7.08, cap 1.0%, heavy 53.36. Flat 16: win 63.8%, mean KO 7.32, cap 2.0%, heavy 51.07. All-hits: win 60.8%, mean KO 7.74, cap 2.0%. The attacker still wins. Cap stays a sliver, not a lock.
- Tank where Cinislug holds Ember and Blockwyrm holds Armor only, flat 16: attacker win 58.4%, mean KO 6.1, cap 0%, heavy 66.33.
- Stall with Ember + flat 16: attacker win 17.2%, mean KO 3.37, turn-3 KO 89.6%, cap 0%. The wall user spends AP and still dies.

### Worked examples

All four are iv 0, roll 0.925, no crit, trace cap 8 turns. The trace lines in the appendix are the source. They are one seed, not the 500-trial mean.

**1. Hammer + Blockade, percent and flat 12, same seed.** Both traces are the same integers, which is what the formula predicts (both leave 34). Turn 1 Crackett's Seam Cut hits Blockwyrm for 34 before anyone has walled. Deghoul armors. Turn 2 Blockwyrm puts Blockade on itself. Turn 4 Validape's Hammer Sync spends that block and deals 34. The same pair repeats on turn 6. At the trace cap Deghoul is at 38/106 and Blockwyrm is at 0. The 500-trial mean KO for this kit is 8.71 with a 0% cap. The trace did not need a lock to keep Deghoul up for eight turns. Blockwyrm died to Seam Cut.

**2. Blade Flurry, one-hit flat 16 vs all-hits flat 16.** Kitsauto is faster, so turn 1's three hits are 13, 13, 13 into an unarmored body. Once Ember and Armor are both up, the one-hit model on turn 4 is 1, then 13, then 13. The all-hits model on that same turn is 1, 1, 1, and again on turns 6 and 8. Attacker wins both traces (turn 7 and turn 8). Hits 2 and 3 are the reason to keep Armor at one hit.

**3. Tank, flat 16, focus fire.** Turn 2 Hammer into Hopoch is 90 (the partner, not the armored body). Turn 4 Seam Cut spends Blockade for 4, then Hammer into Blockwyrm is 61, the open neutral hit. Turn 6 repeats 4 then 61. Turn 7 Seam Cut is 34 and KOs Blockwyrm. Winner is the attacker. The wall never covered the Hammer, because the faster hit spent it.

**4. Swap.** Turn 2 Deghoul swaps to Hopoch. Turn 4 Hammer into Deghoul, back on the field with no Armor, is 115 and KOs. That is hammer open 115. Swap cleared the body. The battle row for this policy shows armor casts at 0.

## (h) What works, what doesn't, what could be better

Works. One sentence on the card. Flat 12 after a 60% wall matches today's percent on the Hammer that started this round, and it gives that Hammer back its damage when no wall is up. The tank still loses. Faster chip ignores Armor. A multi-hit spends Armor on hit 1.

Doesn't. All-hits flat and a point pool. They are how a 13-damage hit becomes 1, three times. Percent Armor on a heavy is a hidden 29-point cut (115 to 86) that the card calls "25%." The stale assets will lie about all of this until they are reimported. The old blockade test will fail the suite. Guard Stance's Def +2 is a better wall than several real walls and does not say so.

Could be better. Armor has no priority and no recharge, so the counterplay is speed and a second hit, which is already how the game wants tempo to work. The stamp conversion should share the cap or it will reinvent percent stacks under a new name. Ledger should stay a percent so Blockwyrm's signature does not become a third flat on top of Guard Stance.

## (i) Ranked recommendations

Each one is a **PROPOSAL**. AP is set by what the hit does, not by a ladder.

**1. Flat Armor, one hit, after the wall percent, cap 16, keep the 10% floor.** Guard Posture 12. Guard Stance 16. Files: `Moves.csv` rows `guard_posture` and `guard_stance`; `_LEGEND.md` (new flat opcode, or a documented magnitude that is points rather than a percent); `UtilityEffectExecutor.ApplyMitigationWindow` or a sibling; `DamageCalculator.CalculateDamageDetailed` after the percent product and before the floor; `ChainlingInstance` cap when summing flat sources. Test `Armor_Flat12_AfterWall_OneHit_FloorStillBinds`: Hammer Sync, IV 0, roll 0.925, Blockade 60% then flat 12, final damage 34; Snap Strike under the same stack final damage 5; a second hit of a multi-hit is unmodified by that Armor.

**2. Do not ship all-hits flat or a pool.** No file change beyond not authoring them. Test `Armor_AllHitsModel_IsNotTheShippedOpcode`: a 3-hit Blade Flurry against one-hit Armor records a consumed budget on hit 1 and full damage on hits 2 and 3. The formula totals are 27 (one hit clipped) and 3 (every hit clipped). The shipped opcode must produce 27, not 3, when Ember also covered hit 1.

**3. Two pool edits, band kept.** Deghoul's `LearnableMoves` replaces `baseline_striker` with `guard_posture`. Blockwyrm's `LearnableMoves` replaces `stone_barrage` with `guard_stance`. Files: `SpeciesMovePools.csv` rows `deghoul` and `blockwyrm`, then the species assets on reimport. `deghoul swap keeps the band`. Blockwyrm's signature stays `immutable_ledger`. Test `LearnPool_ArmorSwaps_KeepTheBodyBand`: both pools size 5, signature strictly rarest, at most one move exactly one step above the body, starters unchanged. Do not also give Guard Posture to Rattlestake.

**4. Six stamps become small flat Armor under the same cap. Ledger and Firewall stay percent.** Files: `Modifiers.csv` text for 6.4-a, 6.4-d, 6.4-g, 7.1-b, 6.3-c, 6.8-e; `SwapModifierResolver.ResolveLegacyMitigation`; `CombatResolver` at the Shield Link, Grit Guard, and Sparse Ward sites (lines 3826, 5392, 6011). Values: Brace 6, Cover Ally 5, Fade Guard 4, Shield Link 8, Grit Guard 8, Sparse Ward 4 per empty slot max 8. Test `ArmorCap16_MovePlusBrace_DoesNotExceed16`: Guard Stance 16 and Brace 6 on one hit subtract 16, and the next hit that turn subtracts 6.

**5. Shattered Armor shreds Armor. Core Breach gets its row. Both stay out of pools.** Files: `Moves.csv` `shattered_armor` (category DamagingMelee, power 34, AP 2, utility strips flat Armor) and a new `core_breach` row (Mined, Uncommon, DamagingMelee, power 32, AP 3, `ignoreMitigation`). `CombatResolver.cs:4500` loses the 5% fallback. No `SpeciesMovePools.csv` edit. Test `ShatteredArmor_StripsFlat_BlockSurvives` and `CoreBreach_IgnoresArmorAndBlock_ConsumesNeither` against the catalog row, not a fabricated move.

**6. Reimport move assets and fix the stale test before any Armor number is playtested.** Files: the nine family assets plus `Move_guard_posture.asset`, and `CombatIssuesRemediationV2Tests.cs:356`. Test `MoveAsset_GuardPosture_StackKey_IsArmor`: asset stack key is `armor_guard_posture`, not `wall_guard_posture`, and the blockade test reads `BlockWindow:60%:Melee:2:wall_blockade`.

**7. Add the thirteen missing plan tests only where the code already exists** (multi-hit, Ice Wall +1 once, swap clear, timers before status). That is wall-plan debt, not an Armor number. Test names are the ones listed under [C11].

## (j) Consistency flags

| Flag | CSV / code | Other surface |
|---|---|---|
| Guard Posture key | `armor_guard_posture`, tag ARMOR [C01] | Asset `wall_guard_posture`, tag UTIL [C03] |
| Family names and AP | Buffer / Compound / Ash / Lattice Barrier, new AP, BlockWindow [C04] | Assets still say Barrier Wall, Compound Guard, Ash Guard, Lattice Guard, old AP, FullAbsorb or a 40-point threshold [C05] |
| Server AP and block ids | `move_ap.ts` matches CSV [C06]. Flags exclude Guard Posture [C07] | Assets do not. A client build from assets desyncs from the server table |
| Blockade utility | `BlockWindow:60%:Melee:2:wall_blockade` | Test still wants `MitigationWindow:25%:2:-1:blockade` [C12] |
| Core Breach | Test and plan D2 | No CSV row [C15] |
| Plan status | Engine, CSV, and server are largely in | Frontmatter still `status: pending`. Audit doc absent [C13] [C14] |
| Typeless chemistry | Design rule: typeless excluded from type charts | Engine uses the attacker's primary type [C23]. Sim follows the engine |
| STAB | Undecided | `enableStab: 0` while a 1.2 multiplier is authored and unused [C22] |
| One block | Engine: any category [C08] | Round-14 v2 sim: one per category [C31] |
| Executor override | Plan: override for passives only | `ApplyBlockWindow` passes `@override: true` for move casts [C09] |
| Card text | Family cards state category, 1 hit, multi-hit, recharge, and the one-block rule [C04] | Guard Posture's card states 25%, 1 hit, 2 turns, and the stack, and also says the numbers are provisional [C01]. It does not state a floor or a cap, because those are not the shipped percent |
| Shattered Armor | Card says 5% and +1 AP | Utility column empty; effect is a hardcoded branch [C17] |
| Immutable Ledger | Card says 20% and +1 AP | Utility column empty; effect is hardcoded [C19] |
| Sim formula labels | Battle loop filters melee walls off ranged hits | Formula packs whose names do not start with `wall` still apply a 60% melee wall to Cryo Ray. Do not cite those JSON cells [C29] |

**PROPOSAL.** Every Armor card states the flat amount, the one-hit budget, the 2-turn cap, "any attack," "after a Wall or Barrier," the 16 cap, and that a minimum hit still applies. A card that omits one of those is wrong.

## (k) Questions only Cronus can answer

1. **Flat 12 / 16, one hit, after the wall, cap 16?** Default: yes. It is the only shape that matches today's Hammer + Blockade hit and still lets a naked heavy land.
2. **Is mean KO 8.71 on Hammer + Blockade + Armor acceptable?** Default: yes. Flat 12 does not make it longer than the percent already on the card. Switch to nostack only if that length is a no. Do not lower Blockade.
3. **Deghoul replaces Snap Strike with Guard Posture?** Default: yes, so the line is a real pool. Rattlestake replacing Quick Jab is the other legal home. Do not use both.
4. **Blockwyrm replaces Stone Barrage with Guard Stance?** Default: yes. The band holds. The Def +2 goes away.
5. **Immutable Ledger stays a 20% signature, not Armor?** Default: yes.
6. **Shattered Armor and Core Breach stay catalog-only?** Default: yes. No pool edits. No Quick Jab removal.
7. **Reimport assets before playtests?** Default: yes. Until then the client is the old walls, and `wall_guard_posture` is a wall tag.

## Check tally

`armor_v1_check.py` re-reads the snapshot and `armor_2v2_sim.out.json`. Claim ids in this doc: [C01] [C02] [C03] [C04] [C05] [C06] [C07] [C08] [C09] [C10] [C11] [C12] [C13] [C14] [C15] [C16] [C17] [C18] [C19] [C20] [C21] [C22] [C23] [C24] [C25] [C26] [C27] [C28] [C29] [C30] [C31] [C32] [C33] [C34].

CHECK TALLY: 34 PASS / 0 FAIL

## Appendix: script-owned facts

The checker requires every formula row, battle row, and trace line below to appear verbatim.

<!-- CHECK-ROWS-BEGIN -->
trials=500 jobs=57

### Formula cells (iv 0, roll 0.925, no crit unless named)

| cell | chem | pre | floor | dmg |
|---|---|---:|---:|---:|
| hammer_deghoul_open | dis | 115.91 | 15.56 | 115 |
| hammer_deghoul_wall60 | dis | 115.91 | 15.56 | 46 |
| hammer_deghoul_armor25 | dis | 115.91 | 15.56 | 86 |
| hammer_deghoul_both | dis | 115.91 | 15.56 | 34 |
| hammer_deghoul_f12 | dis | 115.91 | 15.56 | 34 |
| hammer_deghoul_f16 | dis | 115.91 | 15.56 | 30 |
| hammer_deghoul_f16_first | dis | 115.91 | 15.56 | 39 |
| hammer_deghoul_f12_alone | dis | 115.91 | 15.56 | 103 |
| hammer_deghoul_crit_open | dis | 173.87 | 15.56 | 173 |
| hammer_deghoul_crit_both | dis | 173.87 | 15.56 | 52 |
| hammer_block_open | neutral | 61.23 | 6.51 | 61 |
| hammer_block_wall60 | neutral | 61.23 | 6.51 | 24 |
| hammer_block_f16 | neutral | 61.23 | 6.51 | 8 |
| hammer_block_ledger_wall_f16 | neutral | 61.23 | 6.51 | 6 |
| snap_open | dis | 43.97 | 5.83 | 43 |
| snap_wall60 | dis | 43.97 | 5.83 | 17 |
| snap_both | dis | 43.97 | 5.83 | 13 |
| snap_f12 | dis | 43.97 | 5.83 | 5 |
| snap_f12_alone | dis | 43.97 | 5.83 | 31 |
| deep_open | adv | 141.06 | 12.6 | 141 |
| deep_wall60 | adv | 141.06 | 12.6 | 56 |
| deep_both | adv | 141.06 | 12.6 | 42 |
| deep_f12 | adv | 141.06 | 12.6 | 44 |
| cryo_open | dis | 62.86 | 8.39 | 62 |
| cryo_wall25 | dis | 62.86 | 8.39 | 47 |
| cryo_f12 | dis | 62.86 | 8.39 | 50 |
| cryo_f16 | dis | 62.86 | 8.39 | 46 |
| cryo_both25 | dis | 62.86 | 8.39 | 35 |
| pulse_open | dis | 17.64 | 2.27 | 17 |
| pulse_wall25 | dis | 17.64 | 2.27 | 13 |
| pulse_f12 | dis | 17.64 | 2.27 | 5 |
| pulse_f16 | dis | 17.64 | 2.27 | 2 |

blade open total 39; ember on hit 1 then full 32; ember+flat16 on hit 1 then full 27; ember on hit 1 and flat16 on every hit 3; one blade hit pre 13.3 floor 1.69 open 13.

### Battle rows (500 trials)

hs_open pct n=500 atk_win=4.8 tko3=49.8 tko_mean=6.1 capped=0.0 wall_casts=0.0 armor_casts=0.0 ap_walls=0.0 ap_armor=0.0 heavy_dmg=94.41
hs_wall pct n=500 atk_win=54.4 tko3=0.0 tko_mean=5.23 capped=0.0 wall_casts=1.72 armor_casts=0.0 ap_walls=5.16 ap_armor=0.0 heavy_dmg=47.82
hs_b pct n=500 atk_win=19.0 tko3=0.0 tko_mean=8.71 capped=0.0 wall_casts=2.41 armor_casts=3.46 ap_walls=7.24 ap_armor=3.46 heavy_dmg=45.75
hs_b f12 n=500 atk_win=17.6 tko3=0.0 tko_mean=8.71 capped=0.0 wall_casts=2.4 armor_casts=3.42 ap_walls=7.19 ap_armor=3.42 heavy_dmg=47.96
hs_b f16 n=500 atk_win=23.4 tko3=0.0 tko_mean=8.75 capped=0.0 wall_casts=2.46 armor_casts=3.45 ap_walls=7.37 ap_armor=3.45 heavy_dmg=45.64
hs_b f20 n=500 atk_win=16.0 tko3=0.0 tko_mean=9.26 capped=0.0 wall_casts=2.43 armor_casts=3.53 ap_walls=7.28 ap_armor=3.53 heavy_dmg=41.72
hs_b pool24 n=500 atk_win=14.0 tko3=0.0 tko_mean=9.26 capped=0.0 wall_casts=2.42 armor_casts=3.53 ap_walls=7.25 ap_armor=3.53 heavy_dmg=38.91
hs_b nostack n=500 atk_win=23.6 tko3=0.0 tko_mean=8.1 capped=0.0 wall_casts=2.38 armor_casts=3.24 ap_walls=7.14 ap_armor=3.24 heavy_dmg=53.38
hs_b f16_before n=500 atk_win=23.4 tko3=0.0 tko_mean=8.31 capped=0.0 wall_casts=2.41 armor_casts=3.39 ap_walls=7.22 ap_armor=3.39 heavy_dmg=51.75
hs_b f16_all n=500 atk_win=17.0 tko3=0.0 tko_mean=8.99 capped=0.0 wall_casts=2.46 armor_casts=3.47 ap_walls=7.37 ap_armor=3.47 heavy_dmg=45.28
hs_armor pct n=500 atk_win=20.8 tko3=7.4 tko_mean=7.04 capped=0.0 wall_casts=0.0 armor_casts=2.39 ap_walls=0.0 ap_armor=2.39 heavy_dmg=89.26
hs_armor f12 n=500 atk_win=17.4 tko3=34.6 tko_mean=5.17 capped=0.0 wall_casts=0.0 armor_casts=1.91 ap_walls=0.0 ap_armor=1.91 heavy_dmg=94.34
hs_armor f16 n=500 atk_win=18.8 tko3=25.2 tko_mean=5.77 capped=0.0 wall_casts=0.0 armor_casts=2.09 ap_walls=0.0 ap_armor=2.09 heavy_dmg=93.77
hs_swap f12 n=500 atk_win=58.0 tko3=0.0 tko_mean=6.39 capped=0.0 wall_casts=0.0 armor_casts=0.0 ap_walls=0.0 ap_armor=0.0 heavy_dmg=85.75
dd_b pct n=500 atk_win=28.6 tko3=0.2 tko_mean=6.35 capped=0.0 wall_casts=2.86 armor_casts=2.86 ap_walls=8.58 ap_armor=2.86 heavy_dmg=45.75
dd_b f12 n=500 atk_win=27.2 tko3=0.2 tko_mean=6.05 capped=0.0 wall_casts=2.74 armor_casts=2.74 ap_walls=8.21 ap_armor=2.74 heavy_dmg=48.43
dd_b nostack n=500 atk_win=34.2 tko3=0.6 tko_mean=4.54 capped=0.0 wall_casts=2.19 armor_casts=2.19 ap_walls=6.58 ap_armor=2.19 heavy_dmg=60.72
multi pct n=500 atk_win=73.6 tko3=1.2 tko_mean=4.58 capped=0.0 wall_casts=2.07 armor_casts=2.07 ap_walls=4.13 ap_armor=2.07 heavy_dmg=None
multi f16 n=500 atk_win=98.4 tko3=0.0 tko_mean=6.85 capped=0.0 wall_casts=3.03 armor_casts=3.06 ap_walls=6.05 ap_armor=3.06 heavy_dmg=None
multi f16_all n=500 atk_win=99.4 tko3=0.0 tko_mean=7.56 capped=0.0 wall_casts=3.4 armor_casts=3.46 ap_walls=6.81 ap_armor=3.46 heavy_dmg=None
multi pool24 n=500 atk_win=97.0 tko3=0.0 tko_mean=7.26 capped=0.0 wall_casts=3.25 armor_casts=3.29 ap_walls=6.5 ap_armor=3.29 heavy_dmg=None
chip pct n=500 atk_win=33.8 tko3=99.2 tko_mean=1.15 capped=0.0 wall_casts=0.08 armor_casts=0.09 ap_walls=0.25 ap_armor=0.09 heavy_dmg=None
chip_slow pct n=500 atk_win=92.0 tko3=27.0 tko_mean=4.2 capped=0.0 wall_casts=0.0 armor_casts=3.79 ap_walls=0.0 ap_armor=3.79 heavy_dmg=None
chip_slow f12 n=500 atk_win=92.6 tko3=28.0 tko_mean=4.24 capped=0.0 wall_casts=0.0 armor_casts=3.87 ap_walls=0.0 ap_armor=3.87 heavy_dmg=None
chip_slow f16 n=500 atk_win=90.6 tko3=13.0 tko_mean=4.76 capped=0.0 wall_casts=0.0 armor_casts=4.25 ap_walls=0.0 ap_armor=4.25 heavy_dmg=None
chip_slow f16_all n=500 atk_win=85.2 tko3=4.4 tko_mean=6.28 capped=0.0 wall_casts=0.0 armor_casts=3.21 ap_walls=0.0 ap_armor=3.21 heavy_dmg=None
chip_slow_open pct n=500 atk_win=54.8 tko3=22.8 tko_mean=5.28 capped=0.0 wall_casts=0.0 armor_casts=0.0 ap_walls=0.0 ap_armor=0.0 heavy_dmg=None
ray pct n=500 atk_win=98.6 tko3=78.2 tko_mean=2.85 capped=0.0 wall_casts=0.99 armor_casts=1.48 ap_walls=0.85 ap_armor=1.06 heavy_dmg=80.37
ray f16 n=500 atk_win=98.8 tko3=86.2 tko_mean=2.63 capped=0.0 wall_casts=0.89 armor_casts=1.39 ap_walls=0.72 ap_armor=0.93 heavy_dmg=80.52
tank pct n=500 atk_win=65.4 tko3=0.0 tko_mean=7.08 capped=1.0 wall_casts=3.44 armor_casts=3.44 ap_walls=10.33 ap_armor=3.44 heavy_dmg=53.36
tank f16 n=500 atk_win=63.8 tko3=0.0 tko_mean=7.32 capped=2.0 wall_casts=3.41 armor_casts=3.41 ap_walls=10.22 ap_armor=3.41 heavy_dmg=51.07
tank f16_all n=500 atk_win=60.8 tko3=0.0 tko_mean=7.74 capped=2.0 wall_casts=3.58 armor_casts=3.58 ap_walls=10.75 ap_armor=3.58 heavy_dmg=47.16
tank_open pct n=500 atk_win=56.6 tko3=2.4 tko_mean=6.88 capped=0.0 wall_casts=0.0 armor_casts=0.0 ap_walls=0.0 ap_armor=0.0 heavy_dmg=83.14
tank_guard f16 n=500 atk_win=58.4 tko3=0.0 tko_mean=6.1 capped=0.0 wall_casts=1.02 armor_casts=2.82 ap_walls=2.04 ap_armor=2.82 heavy_dmg=66.33
stall_armor f16 n=500 atk_win=17.2 tko3=89.6 tko_mean=3.37 capped=0.0 wall_casts=1.2 armor_casts=1.2 ap_walls=2.4 ap_armor=1.2 heavy_dmg=64.63

cryo both25_60 json dmg=18 category-correct buffer+25% armor dmg=35.

### Trace lines (iv 0, roll 0.925, no crit, max 8 turns)

trace hs_b_pct winner=ongoing turns=8 hammers=[(4, 34), (6, 34)] blade_hits=[] seam_on_blockwyrm=[(1, 34), (2, 34), (4, 34), (5, 34), (6, 34)] swaps=[] end_deghoul=(38, 106) end_blockwyrm=(0, 168)
trace hs_b_f12 winner=ongoing turns=8 hammers=[(4, 34), (6, 34)] blade_hits=[] seam_on_blockwyrm=[(1, 34), (2, 34), (4, 34), (5, 34), (6, 34)] swaps=[] end_deghoul=(38, 106) end_blockwyrm=(0, 168)
trace multi_f16_one winner=atk turns=7 hammers=[] blade_hits=[(1, 13, 1), (1, 13, 2), (1, 13, 3), (2, 12, 1), (2, 12, 2), (2, 12, 3), (3, 9, 1), (3, 9, 2), (3, 9, 3), (4, 1, 1), (4, 13, 2), (4, 13, 3), (5, 13, 1), (5, 13, 2), (5, 13, 3), (6, 1, 1), (6, 13, 2), (6, 13, 3), (7, 13, 1)] seam_on_blockwyrm=[] swaps=[] end_deghoul=None end_blockwyrm=(0, 168)
trace multi_f16_all winner=atk turns=8 hammers=[] blade_hits=[(1, 13, 1), (1, 13, 2), (1, 13, 3), (2, 12, 1), (2, 12, 2), (2, 12, 3), (3, 9, 1), (3, 9, 2), (3, 9, 3), (4, 1, 1), (4, 1, 2), (4, 1, 3), (5, 13, 1), (5, 13, 2), (5, 13, 3), (6, 1, 1), (6, 1, 2), (6, 1, 3), (7, 13, 1), (7, 13, 2), (7, 13, 3), (8, 1, 1), (8, 1, 2), (8, 1, 3)] seam_on_blockwyrm=[] swaps=[] end_deghoul=None end_blockwyrm=(0, 168)
trace tank_f16 winner=atk turns=7 hammers=[(2, 90), (4, 61), (6, 61)] blade_hits=[] seam_on_blockwyrm=[(4, 4), (5, 34), (6, 4), (7, 34)] swaps=[] end_deghoul=None end_blockwyrm=(0, 168)
trace hs_swap_f12 winner=atk turns=6 hammers=[(2, 90), (4, 115), (6, 73)] blade_hits=[] seam_on_blockwyrm=[(1, 34), (2, 34)] swaps=[(2, 'deghoul', 'hopoch')] end_deghoul=(0, 106) end_blockwyrm=(0, 168)
<!-- CHECK-ROWS-END -->
