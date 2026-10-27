# Tempo and counterplay in 2v2: do the counters stop the one-shots?

**Status: PROPOSAL analysis only.** Nothing here is a decision, and nothing here changes gameplay code or data. Every recommendation is a **PROPOSAL** for Cronus to accept or reject. Written 2026-10-20, 06:55 CDT. **Extended 2026-10-20, 09:10 CDT:** see "Extension (2026-10-20): stacked walls and pre-emptive swaps" after Reproduce. Where they differ, the extension supersedes section (i).

**Why this exists.** The earlier 1v1 tempo verdict ("keep heavies at 4 AP") used base stats only. Cronus pushed back: game-defining factors were left out. His example was Validape banking for Hammer Sync to one-shot Deghoul. Deghoul is Gas, so it should have AP denial and chip damage, and in 2v2 its ally could wall it. This doc runs the full 2v2 picture: real kits, passives, chemistry, AP economy, walls, swaps, focus fire, statuses, Speed, IVs and RNG. For each burst line it asks one question: do the obvious counters stop the one-shot, delay it, or do nothing?

**Sources.** The sim reads the snapshot `/workspace/balance/s10`, taken 2026-10-20 at 02:15 CDT. Its code and data match the local tree. Nothing under `Assets/Scripts` or `Assets/Data` is newer than 02:10. The only newer files are two docs: `WALLS_BARRIERS_MITIGATION_ANALYSIS.md` (02:56) and `TYPE_MATCHUPS_AND_CHEMISTRY_V2_ANALYSIS.md` (06:41). Wall facts come from the walls doc. The typeless toggle lines up with the type doc's first proposal.

**Artifacts** are in `/workspace/balance/out/` on the agent box:

| File | What it is |
|---|---|
| `tempo_2v2_sim.py` | Engine mirror plus the policy matrix |
| `tempo_2v2_sim.out.json` | 296 cells × 4000 battles, random IVs |
| `tempo_2v2_check.py` | Checks engine facts (file:line), data rows, the one-shot table, and sim sanity. Result: 114 PASS / 0 FAIL (68 original + 46 extension checks) |
| `tempo_2v2_tables.md` | Every cell; the same tables are in the appendix |

## (a) Headline

1. **The 4 AP heavies are not the tempo problem.** No defender in the matrix can stop a 4 AP heavy from becoming affordable on turn 2. The answer is to deny it a target, which several counters do well:
   - **Ember Wall** shuts out every 4 AP heavy completely. Target KO by turn 3 drops to 0–1%.
   - **Swapping** the target out pushes the hit to turn 4 and onto the tank.
   - **Focus fire** on the burster cuts "heavy lands" from 67–95% to 6–22%.
   - **Chemistry statuses** have a similar effect. Gas→Node Jammed plus Bot→Node Drained on Validape cut it to 6%.

   Cronus's intuition holds for Hammer Sync → Deghoul. Kitsauto's Bot→Node Drained and Gas→Node Jammed together (the "status" counter) cut Hammer Sync's land rate from 67% to 6%. Deghoul's own denial delays the hit by a turn (T2 → T3.3), but the denying team pays for it in damage.
2. **What can't be stopped is a 2 AP hit on turn 1 into a fragile body.** No wall is up before a faster attacker acts on turn 1. Denial does nothing to a 2 AP move: you start with 0 and regen 2, so it is always affordable. The swap counter in this matrix only reacts to a heavy that is already affordable, so it never fires against a 2 AP hit. Only a blind turn-1 swap, which resolves before any move, would move the target out of the way. Examples:

   | Hit | Attacker | Target | P(one-shot from full HP) | Note |
   |---|---|---|---|---|
   | Seam Cut (Rare, 2 AP, ×1.25 when faster) | Crackett | Deghoul | **97%** | |
   | Chaos Strike (Mythic, 2 AP) | Kreechank | Keyper | 100% on the ×1.5 roll | The ×1.5 roll comes up 50% of the time |
   | Partition Shot (Rare, 2 AP) | Marrowhead | Deghoul | 67% | |
   | Ore Strike (Rare, 2 AP) | Kodiax | Keyper | 54% | |

   In the sim, Ember Wall plus partner focus still loses Deghoul on turn 1–2 92–94% of the time. The Chaos Strike line keeps target KO by turn 2 at 66–68% no matter which counter is used. Only Key Rotation or Debuff Script halve it, to 32–33%.
3. **The fragile bodies drive the outcomes, not the heavy costs.**
   - Deghoul (106 HP, MD 36, RD 34) and Keyper (102 HP, MD 40) die to Rare 2 AP hits.
   - Crackett dies to two Frost Shots, and Keyper to one or two Ore Strikes. In those lines, spamming the light move KOs the target as fast as banking for the heavy: L2 is 91% vs 94% by turn 2, and L3 is 94.5% vs 94.2%.
4. **Percent walls do nothing against a single big hit.** Blockade, Compound Guard, Barrier Wall and Ice Wall all leave Hammer Sync's turn-2 KO at 46–51%, against 50% with no counter. Two reasons:
   - The wallers are slow. Blockwyrm has Speed 24, Gilarime 49, Cinislug 28.
   - A 20–25% cut doesn't move a hit that one-shots 79–100% of the time below the KO line. Ice Wall at 2 AP changes nothing (51% vs 50%).
5. **Overtuned counters (flags):**
   - **Key Rotation** (Common, 1 AP; permanent MeleeDef +1 and Evasion +1, stackable). One cast drops Deep Dig's one-shot chance on Keyper from 100% to 31% and its hit chance from 95% to 63%. Kodiax's team win rate falls from 44% to 9%.
   - **Ember Wall** (Legendary, 2 AP). It fully stops every banked heavy. It does have counters: a faster turn-1 hit, multi-hits (three absorbs), or attacking the waller instead.
   - **Memory Leak + Packet Skim** permanently lock a lone 4 AP attacker: −3 AP a turn against +2 regen. Kodiax never casts Deep Dig (mean KO turn 10). But both defenders spend every action on it, and they still lose 84% of games.
6. **Fuel Surge + melee heavy is the one true "bank" one-shot.** With Gas Spike's MeleeAtk +2, Hard Fork one-shots Kitsauto 100% of the time. AP denial can't stop it: 3 + 2 + 2 = 7 AP, and 7 − 1 − 2 is still ≥ 4. Against the informed-deny counter it lands on turn 2 95% of the time. What stops it is Gas Spike's own MeleeDef −2: Balloom dies to Kitsauto's Blade Flurry before it can cash in. With no counter, the heavy lands only 3.8% of the time in BB.

## (b) Method

**Engine mirror.** `tempo_2v2_sim.py` re-implements the paths below from the snapshot source. The check script confirms the cited lines.

- **Format.** 2 active + 1 bench per side (`CombatConstants.ACTIVE_PER_TEAM = 2`, `ROSTER_SIZE = 3`). 4 move slots.
- **AP.**
  - Start at 0, gain +2 at the start of each turn for living actives, cap 10.
  - Cost is paid at resolve. If the AP isn't there, the action fails (`LogNoApActionFailed`).
  - Memory Leak's −2 fires at the owner's next turn start, after regen.
  - Drain and Steal have **no per-turn cap** (`CombatApService.Drain/Steal`).
  - Gas War adds +1 AP to the user's next move.
- **Turn order.** Swaps first, then the PRIOR block, then everyone else by stage-adjusted Speed snapshotted at queue build. Ties go to stars, then rebirth, then a coin flip.
- **Targeting.**
  - An action names a slot and hits whoever is in that slot when it resolves. A swap therefore redirects the incoming hit to the swap-in.
  - If the slot is empty, the action goes to the first living active.
  - After a KO, the bench auto-swaps in and **inherits the dead body's AP** (`TeamState.AutoSwapFromBench`).
  - Walls stay on a benched Chainling.
- **Damage.**
  - Formula: `(P·A/D·U(.85,1)+1)` × chemistry 1.2/0.8 × crit (6.25%, ×1.5) × Exploit Mark ×1.15 × conditional multiplier × multiplicative mitigation (including the passive 20% from Immutable Ledger). The 10% floor is applied after mitigation.
  - Barrier absorbs come after the calculator. Chemistry statuses land even on a hit that is blocked.
  - Typeless moves take the attacker's type (`DamageCalculator.ResolveAttackType`). The `typeless_neutral` toggle tests the exclusion Cronus decided on.
- **Accuracy.** Rolled per hit: `acc × accStage / evaStage`.
- **Fox Feint.** Dodges the next hit that lands and counters for 10% of the attacker's max HP. Once per battle.
- **Conditionals:**
  - Hammer Sync: +0.2 per buffed partner, max ×1.2.
  - Seam Cut: ×1.25 if the user is faster.
  - Chaos Strike: ×1.5 or ×0.75, 50/50.
  - Finish Burn: ×1.5 when the target is below 40% HP.
- **Passives:** Immutable Ledger, Kindling, Uplink, Patience, Parallel.
- **Statuses.** Durations come from `Statuses.csv` and tick at end of turn, including the turn applied. Stages they set revert when they expire. Cipher Lock blocks every positive stage. Anchored collapses a spread move onto one target.
- **Walls** (from `WALLS_BARRIERS_MITIGATION_ANALYSIS.md`):
  - Percent walls take unlimited hits.
  - Ember Wall absorbs 3 hits over 2 turns and drains 1 AP per absorbed hit. That drain lands after cost is paid, so it usually hits an empty bar.
  - Blockade is 25% for 2 turns at 3 AP.
  - Ice Wall is 25% for the rest of the current turn at 3 AP, so it can't be cast on turn 1.
  - DoT ignores walls.

**IVs.** Random flat −5..+5 on every stat for every Chainling, unless the variant says otherwise. Stars and rebirth are equal.

**Policy matrix.** Each line was run 4000 times per cell.

Burster modes:

| Mode | Behaviour |
|---|---|
| L | Spam the best light move (≤ 2 AP) |
| B | Bank: pass until the heavy is affordable, then fire it |
| BB | Bank plus a boost from the partner or self (Shard Partition, Pack Rally + Uplink, Exploit Window, Fuel Surge, Frozen Assets, Defense Corrupt) |

The burster's partner attacks the *other* foe while the burster lives. This keeps partner one-shots from hiding the line. The `partner_focus` variant has it focus the target instead. A burster whose target is behind a barrier retargets.

Defender counters:

| Counter | Behaviour |
|---|---|
| none | Slot-matched light attacks |
| deny (informed) | Drain or steal when the heavy is live or one regen away. Memory Leak when the heavy is coming soon |
| deny_pre (blind) | Drain whenever the burster's AP + 2 ≥ the heavy's cost |
| wall | The ally keeps a wall on the target whenever the heavy is live or coming soon. Ember Wall comes from Cinislug, Blockade from Blockwyrm. Ice, Compound and Barrier walls are variants |
| swap | Bench the target when the heavy is live. `swap_pivot` also swaps it back |
| focus | Both defenders hit the burster |
| status | Line-specific statuses on the burster: chemistry Jammed/Drained, Cipher Lock, Key Rotation, Debuff Script, Cold Snap, Anchored |

**Metrics.**

| Metric | Meaning |
|---|---|
| Target KO by T2 / T3 | % of battles where the target is dead by the end of that turn |
| Mean KO turn | Capped at 12 |
| Heavy lands | % of battles where the heavy connects with the target, and the mean turn it does |
| Heavy KOs | % of battles where the heavy itself delivers the KO |
| Win | Burster's team wins (30-turn cap) |

## (c) Lines and kits (real learn pools; 4 of the 5 pool moves equipped)

| Line | Burster [kit] | Partner / bench | Target, defender team | Notes |
|---|---|---|---|---|
| L1 | Validape (starter) [Snap Strike, Hammer Sync, Pack Rally, Uplink] | Crackett [Seam Cut, Shard Partition, Isolation Tool, Parallel] / Kodiax | **Deghoul** [Gas Bump, Transaction Tax, Gas War, Slipstream] + Kitsauto [Blade Flurry, Packet Skim, Fox Feint, Debuff Script] / Blockwyrm [Immutable Ledger, Blockade, …] | Cronus's example. BB: Shard Partition makes Crackett a buffed partner, so Hammer Sync gets ×1.2 |
| L2 | Gilarime (starter) [Frost Shot, Cryo Ray, Ice Wall, Frost Shards] | Minefolk (Exploit Window) / Hopoch | **Crackett** + Keyper [Cipher Shot, Memory Leak, Key Rotation, System Reset] / Blockwyrm | Status counter: Cipher Shot (Encrypted→Frozen Jammed), Cold Snap |
| L3 | Kodiax [Ore Strike, Deep Dig, …] | Validape (Pack Rally + Uplink) / Hopoch | **Keyper** + Kitsauto / Blockwyrm | Status counter: Key Rotation then Cipher Shot; Debuff Script |
| L4 | Balloom [Gas Bump, Fuel Surge, **Hard Fork** (off-type exception), Slipstream] | Validape / Hopoch | **Kitsauto** + Keyper / Blockwyrm | BB = Fuel Surge on turn 1 (+3 AP, Gas Spike MeleeAtk +2 / MeleeDef −2 for 2 turns) |
| L4g | Same, with **Gas War** (ranged) instead of Hard Fork | same | same | `gas_war_melee` variant |
| L5 | Forkrook [**Fork Split** (spread), …] | Marrowhead (Partition Shot → Frozen Assets) / Hopoch | **Deghoul** + Keyper / Blockwyrm | Status counter: Crackett Isolation Tool (Anchored) |
| L6 | Kreechank [**Chaos Strike** 2 AP, Defense Corrupt, …] | Hopoch / Kodiax | **Keyper** + Kitsauto / Blockwyrm | A 2 AP burst with no heavy |
| L7 | **COUNTERFACTUAL** Cinislug with **Finish Burn** (3 AP; it is in no pool today) | Kitsauto / Hopoch | **Deghoul** + Kitsauto / Blockwyrm | `finish_burn@4AP` variant |

## (d) One-shot table: chance to KO from full HP

Accuracy is excluded. Multiply by 0.95 for every move here. Random IVs on both sides, from `tempo_2v2_check.py` §3.

| Hit | Into | Chemistry | P(KO) |
|---|---|---|---|
| Hammer Sync, no buffed partner | Deghoul | ×0.8 (Node into Gas) | 78.9% |
| Hammer Sync ×1.2 (one buffed partner) | Deghoul | ×0.8 | 99.8% |
| Hammer Sync ×1.2 | Blockwyrm (the swap-in) | ×1.0 | 0% |
| Cryo Ray | Crackett | ×1.2 | 98.6% |
| Frost Shot (2 AP light) | Crackett | ×1.2 | 7.8% (two of them: ~always) |
| Deep Dig | Keyper | ×1.2 | 100% |
| Deep Dig after **one Key Rotation** (Def +1 only) | Keyper | ×1.2 | **31.0%**, and hit chance falls to 63% |
| Ore Strike (2 AP light) | Keyper | ×1.2 | 54.0% |
| Hard Fork, unboosted | Kitsauto | ×1.2 | 5.3% |
| Hard Fork after Fuel Surge (MeleeAtk +2) | Kitsauto | ×1.2 | **100%** |
| Gas War (ranged; Gas Spike doesn't boost it) | Kitsauto | ×1.2 | 0% |
| Fork Split (each target) | Deghoul | ×1.0 | 49.5% |
| Chaos Strike, high roll (50%) | Keyper | ×1.2 | 100% |
| Yield Spike (2 AP) | Keyper | ×1.0 | 29.1% |
| **Seam Cut ×1.25 (partner, 2 AP Rare)** | Deghoul | ×1.0 | **97.0%** |
| Partition Shot (partner, 2 AP Rare) | Deghoul | ×1.0 | 67.5% |
| Finish Burn, target above 40% | Deghoul | ×1.2 | 91.9% |

**Sensitivity to target bulk (PROPOSAL input).** Deghoul +10 / +20 HP takes these one-shot chances to:

| Hit | Today | +10 HP | +20 HP |
|---|---|---|---|
| Seam Cut | 97% | 82% | 59% |
| Hammer Sync ×1.0 | 79% | 53% | 30% |
| Hammer Sync ×1.2 | 99.8% | 96% | 81% |

Seam Cut at ×1.15 instead of ×1.25 gives 84%. For Keyper, +20 HP leaves Chaos Strike's high roll at 99%.

## (e) Matrix headline (bank mode B; the full L / B / BB tables are in the appendix)

Each cell shows: target KO by T2 % / heavy lands % (at mean turn) / burster team win %.

| Line (burster B = bank) | none | informed deny | blind deny | Ember Wall | Blockade | swap out | focus burster | status on burster |
|---|---|---|---|---|---|---|---|---|
| L1 Hammer Sync → Deghoul | 50.2 / 66.8@T2.1 / 81.5 | 0 / 98.8@T3.3 / 100 | 0 / 96.4@T5.1 / 100 | 0 / 7.8@T4.0 / 89 | 49.7 / 66.2@T2.1 / 62.7 | 0.4 / 91.4@T4.1 / 92.9 | 0.7 / 6.2@T2.9 / 99.2 | 0.5 / 5.7@T2.9 / 98.2 |
| L2 Cryo Ray → Crackett | 93.7 / 95.6@T2.0 / 98.7 | 93.6 / 95.3@T2.0 / 100 | 0 / 31.4@T3.2 / 100 | 0.4 / 9.8@T3.9 / 100 | 93.8 / 95.8@T2.0 / 99.7 | 43.1 / 94.6@T3.2 / 95.6 | 22.1 / 22.4@T2.0 / 93.7 | 48.9 / 51.3@T2.1 / 99.7 |
| L3 Deep Dig → Keyper | 94.2 / 94.5@T2.0 / 44.2 | 0 / 17.4@T6.9 / 83.7 | 0 / n/a / 96.2 | 0 / 4.7@T4.0 / 96.8 | 94.1 / 94.3@T2.0 / 68.1 | 0 / 13.9@T5.3 / 21.8 | 5.7 / 0.1@T2.0 / 7.3 | 2 / 71.6@T2.2 / 8.8 |
| L4 Hard Fork → Kitsauto | 3.3 / 68.3@T2.0 / 69.7 | 0 / 82.7@T5.7 / 97.7 | 0 / 97.8@T6.4 / 99.5 | 0 / n/a / 94.7 | 3.6 / 68.2@T2.0 / 39.2 | 0 / 35.7@T4.0 / 31.7 | 0.1 / 0.1@T2.0 / 78.6 | 0 / 5@T5.2 / 60.8 |
| L4g Gas War → Kitsauto | 0 / 66.7@T2.0 / 66.1 | 0 / 79.2@T5.5 / 66.5 | 0 / 95.7@T6.3 / 80.3 | 0 / n/a / 92.9 | 0 / 67.3@T2.0 / 25.1 | 0 / 34.1@T4.0 / 12.2 | 0.1 / 0.1@T2.0 / 77.9 | 0 / 5.6@T6.1 / 61.2 |
| L5 Fork Split → Deghoul | 46.5 / 95@T2.0 / 96.7 | 46.5 / 96.1@T2.0 / 98.7 | 0 / 99.3@T4.1 / 100 | 0.1 / 2.1@T3.9 / 99.3 | 45.8 / 94.8@T2.0 / 97.9 | 49.4 / 51@T2.0 / 91.8 | 83 / 21.9@T2.0 / 97.4 | 48.1 / 95@T2.0 / 80 |
| L6 Chaos Strike → Keyper (2 AP) | 65.9 / n/a / 74.4 | 66.8 / n/a / 74.7 | 66.6 / n/a / 74.3 | 66.8 / n/a / 90.8 | 66.1 / n/a / 87 | 66.8 / n/a / 75.5 | 68.4 / n/a / 98.8 | 32.4 / n/a / 95.7 |
| L7 Finish Burn 3 AP → Deghoul (counterfactual) | 87.4 / 99.4@T2.0 / 94.7 | 0 / 99.9@T4.1 / 100 | 0 / 99.8@T6.0 / 100 | 0 / 89@T3.0 / 99.8 | 72.5 / 99.4@T2.0 / 98 | 0 / 30.2@T5.0 / 55.9 | 18.8 / 20.5@T2.0 / 98.2 | 60 / 65.5@T2.0 / 99.5 |

**Variants (bank mode unless noted):**

| Variant | Result |
|---|---|
| Hammer Sync at 6 AP (L1 B) | KO by T2 0%, by T3 36%. Heavy lands T3. Win 79% (BB 8.5%) |
| Deep Dig at 6 AP (L3 B) | KO by T3 33%. Win 6% |
| IVs, L1 B | All 0: KO by T2 63%. Burster +5 / defender −5: 64%. Burster −5 / defender +5: **3.7%**, win 1.7%. A 10-point IV swing switches the line on or off |
| `typeless_neutral`, L1 | Snap Strike goes from resisted (×0.8, inherited Node into Gas) to neutral. Light-spam KO by T2 rises from 4.3% to 27.6%. The bank line is unchanged (49.6%) |
| Other walls on Deghoul, L1 B (KO by T2) | Ice Wall 50%, Compound Guard 49%, Barrier Wall 46%. In BB, Barrier Wall drops it to 32% |
| Ice Wall at 2 AP (L1 B) | 51% vs 50%. No effect: Gilarime (Speed 49) is slower than Validape (60), and the wall lasts only the turn it is cast |
| Drain cap 2/turn, L1 informed deny | Identical (T3 KO 66% vs 67%). No policy here drains more than 2 in a turn |
| Walls drop on swap, L1 swap_pivot | Identical. There were no walls in that roster. No line tested both walls and swaps together, so the proposal's effect here is unmeasured; on reasoning alone it looks small. |
| Gas War melee (L4g) | B 3.2% / BB 3.8% vs ranged 0% / 0.1%. Unboosted it still doesn't one-shot Kitsauto. Boosted, it would one-shot (as Hard Fork does, 100%), but Balloom dies first |
| Finish Burn at 4 AP (L7) | KO by T2 87% either way. A 3 or 4 AP move first fires on T2 regardless. Cost only matters for how often it can be recast |
| Partner focus (partner hits the target too) | Target KO by T2 with light spam: L1 99.7%, L2 100%, L3 99.8%, L5 99.4%, L6 99.6%. Turn-1 partner hits finish the job before any heavy is affordable |
| Ember Wall + partner focus | Target KO by T2: L1 L 94%, L5 L 93%, L6 L 99.7%. Cinislug (Speed 28) walls after the turn-1 hit |

## (f) Which counters work, and which fail

**Work against a 4 AP banked heavy:**

- **Ember Wall: complete stop.** Target KO by T3 is 0–1% in L1–L5. The burster retargets the waller.
  - It fails against turn-1 hits, because Cinislug at Speed 28 acts last.
  - It fails against 2 AP bursts (L6, unchanged).
  - In the counterfactual L7, a 3 AP Finish Burn gets through one turn later (KO by T3 82%, heavy lands T3).
  - Its own AP drain almost never matters. Smart attackers go around it, and when they don't, the attacker has usually just spent its AP on the move.
- **Swap out: a 2-turn delay.** L1 heavy lands T4.1 instead of T2.1. The hit goes to the swap-in: Blockwyrm takes Hammer Sync ×1.2 with a 0% one-shot chance. The cost is the swap action and AP inherited into the slot. L3 swap: burster win falls to 22%. L4g: 12%.
- **Focus the burster.** This works when the defenders outspeed the burster and two turns of their damage exceed its HP:
  - L1: Validape dies T3, heavy lands 6%.
  - L2: Gilarime, heavy lands 22%.
  - L3: Kodiax, heavy lands 0.1%, win 7%.

  It fails against bulky bursters. The burster's partner is left free, so overall win rates vary.
- **Chemistry and status on the burster.**
  - L1: Gas Bump→Jammed plus Blade Flurry→Drained (Bot→Node) cut Hammer Sync to 5.7% landed. This is Cronus's point: Deghoul's Gas typing *is* its counterplay against Node.
  - L3: Key Rotation lets Deep Dig land (72%) but kill only 15%.
  - L2: Cipher Shot→Jammed halves Cryo Ray (51% landed).
- **Informed AP denial: a 1-turn delay, at a high price.**
  - L1: T2.1 → T3.3. KO by T2 drops from 50% to 0, KO by T3 is 67%.
  - The denying team spends its actions and loses: 100% burster win in this scripted policy.
  - It needs two 1 AP drains (Transaction Tax + Packet Skim) from bodies faster than the burster. Memory Leak is a turn late by design.
  - L2 (only Memory Leak available): no effect.
  - L3 (Memory Leak + Packet Skim on a slow Kodiax): a permanent lock. Mean KO turn 10, but the burster team still wins 84%.

**Fail:**

- **Percent walls of any kind** (Blockade, Ice Wall, Compound Guard, Barrier Wall), against hits that one-shot 80–100%. The walls are too slow (3 AP from slow bodies) and too small (20–25%).
- **Any AP denial against Fuel Surge.** 7 AP banked on turn 2 can't be drained below 4 in one turn. L4 BB vs informed deny: KO by T2 95%.
- **Any AP denial against 2 AP bursts** (L6), and against partner turn-1 hits.
- **Anchored (Isolation Tool) against Fork Split.** It only removes the spread. The hit on the selected target is unchanged (L5 B: 48% vs 46.5%).
- **Blind pre-emptive denial.** It "works" (L1: KO delayed to T5.7) only by spending every action, and the deniers lose 100%.

## (g) Unstoppable lines

No *banked* line is unstoppable. Each one has at least one counter that cuts landing to ≤ 10%: Ember Wall, focus, or the right status.

What is effectively unstoppable is a **turn-1 hit from a faster attacker, using a 2 AP move that one-shots a fragile target**:

- Seam Cut → Deghoul: 97%.
- Chaos Strike high roll → Keyper: 50% × 100%.
- Partition Shot → Deghoul: 67%.

Nothing can be up before the attacker acts on turn 1 except a blind swap, which resolves before any move but can't be read in advance. Walls come from slower bodies, and AP denial can't touch a 2 AP cost.

In 2v2 the matrix shows it directly:

- L6, Chaos Strike: KO by T2 66–68% against none, deny, walls and swap. Only stat/status counters (Key Rotation / Debuff Script) halve it, to 32%.
- With partner focus, every line's target is dead by T2 93–100% of the time, even behind Ember Wall.

Fuel Surge + Hard Fork is the closest thing to an unstoppable bank. It is a 100% one-shot and immune to denial. Its counter is to kill the Gas Spike–weakened Balloom first.

## (h) Overtuned counters (flags)

| Counter | Evidence | Flag |
|---|---|---|
| **Key Rotation** (Common, 1 AP, permanent Def +1 / Eva +1, stacks) | One cast: Deep Dig one-shot 100% → 31%, hit chance 95% → 63%. L3 status: burster win 44% → 9% (B) and 28% → 5.5% (BB) | **Overtuned for a Common.** PROPOSAL: Evasion +1 becomes an owned 2-turn stage, or the move costs 2 AP |
| **Ember Wall** (Legendary, 2 AP, 3 absorbs, 2 turns) | Full stop on every 4 AP heavy against a walled target | **Strong but fair for a Legendary.** Counters: turn-1 speed, multi-hits, hitting the waller. PROPOSAL: no change now. Watch it once AI learns to retarget (AI always targets slot 0 today) |
| **Memory Leak + Packet Skim** on one target | −3 a turn against +2 regen locks a 4 AP move forever (L3 mean KO turn 10) | Costs both defenders' actions, and they still lose 84%. **Not overtuned in 2v2.** A drain cap of 2 wouldn't break the lock (net 0 a turn). See R-drain |
| **Fuel Surge** (Mythic, 0 AP) | Makes denial irrelevant | Self-limiting through MeleeDef −2. **Fine as is.** It becomes a problem only if Gas War goes melee *and* Balloom gets bulk |

## (i) What the walls proposals would change here

- **One percent wall per Chainling.** No effect in this matrix: each team has only one waller. It would matter only with two wallers stacking Blockade + Compound Guard on one target, which wasn't modelled.
- **Walls drop on swap.** No measurable effect (the swap_pivot rows are identical). Ember Wall is never worth benching under, and percent walls don't change outcomes here. Keep the proposal as a rules-clarity fix, not a balance fix.
- **Ice Wall at 2 AP.** No counterplay effect (L1: 51% vs 50%). Ice Wall loses to Speed and its 1-turn duration, not to its cost. PROPOSAL: if Ice Wall is meant to stop heavies, it also needs PRIOR or a 2-turn window. The 2 AP cost alone is a usability fix only.

## (j) Reassessing the earlier recommendations (all PROPOSALS)

| Earlier recommendation | Verdict after 2v2 | Why |
|---|---|---|
| **R1: keep heavies at 4 AP** | **Keep.** Stronger evidence now | At 4 AP every heavy has real counters (Ember Wall, swap, focus, status), and the light lines are as fast in L2/L3. At 6 AP they become dead weight: Hammer Sync BB win 8.5%, Deep Dig B 6%. The tempo problem is the 2 AP top-rarity hits into fragile bodies, not the heavies |
| **R1a: Gas War to melee** | **Hold.** Do it only with Fuel Surge's Def −2 kept | Alone it changes nothing (B 0% → 3.2%). Together with Fuel Surge it becomes a 100% denial-proof one-shot, currently held back only by Balloom's fragility |
| **R-drain: cap enemy AP removal per turn** | **Low priority. If kept, cap at 2 per turn from enemy sources** | No 2v2 line drains more than 2 a turn (deny+cap2 is identical). The only lock found (Memory Leak + Skim) costs the deniers the game. To break that lock, give Memory Leak a 1-turn recharge rather than lowering the cap |
| **R-FB: Finish Burn** | **Keep it out of pools. 3 vs 4 AP doesn't matter for the first cast** | Both costs fire on T2 (87% either way). Against Ember Wall, 3 AP re-threatens one turn later (KO by T3 82%) where 4 AP heavies are fully stopped. Against Deghoul, Pulse Shot spam already reaches 81% by T2, so Deghoul's bulk dominates |
| **NEW P1: bulk for the glass bodies** | **PROPOSAL, re-run first** | Deghoul +10 HP takes Seam Cut's one-shot from 97% to 82% and Hammer Sync ×1.0 from 79% to 53%. This touches every line at once. Keyper needs more than HP against Chaos Strike |
| **NEW P2: Key Rotation** | **PROPOSAL** | Evasion +1 becomes an owned 2-turn stage (MeleeDef +1 stays permanent), or AP 1 → 2 |
| **NEW P3: Seam Cut** | **PROPOSAL, alternative to P1** | ×1.25 → ×1.15 when faster: 97% → 84% one-shot on Deghoul |
| **NEW P4: KO'd body's AP** | **Question** | The replacement inherits the dead Chainling's banked AP. A bank is never lost on death, and denial's value leaks to the next body |

## (k) Blind spots

- **Scripted policies, not search.** Defenders apply one counter at a time. A real player would mix them (deny with one body, attack with the other) and react to the visible bank. Win rates for counters that cost both actions (deny, deny_pre) are therefore pessimistic for the defender. The KO-delay numbers are the trustworthy part.
- **Partial swap logic.** The swap counter fires only on a live heavy. It never pre-emptively swaps against 2 AP threats, so L6 "swap" equals "none".
- **AI not modelled.** The shipped AI never sets a target (it falls back to slot 0) and estimates damage without defense or walls. Against the AI, walls and swaps on slot 1 matter less, and denial isn't used deliberately.
- **Mechanics ignored:** BacklashType, CascadeFailure, Slowed, Splinter, modifiers/augments, stars and rebirth (all equal), held items. Kindling and Patience are mirrored only as far as the kits used them.
- **One kit per species.** The kit is 4 of the 5 pool moves, chosen by hand. Other subsets (for example Kitsauto without Fox Feint) shift individual cells.
- **Typeless inheritance stays on** by default because that is how the code works today. `typeless_neutral` shows the decided exclusion, which mostly helps Node lights into Gas.
- **Small bias in Hammer Sync ×1.2.** The buffed-partner check reads the partner's stage and buff state at resolve time, as the engine does. Parallel's random Speed +1 occasionally turns it on in plain B mode.
- **Not a full tier list.** Eight lines and about 15 species.

## (l) Questions for Cronus (each has a default)

1. **Should a turn-1 2 AP hit be able to one-shot a full-HP Chainling?** Seam Cut → Deghoul is 97% today. *Default:* no, outside Mythic Chaos Strike's high roll. Prefer P1 (Deghoul +10 HP, re-run) over P3.
2. **Key Rotation: should the Evasion be permanent?** *Default:* Evasion lasts 2 turns as an owned stage; Def +1 stays.
3. **Ember Wall as a full shutdown of banked heavies: intended?** *Default:* yes, leave it. It is a Legendary with three counters.
4. **KO'd body's AP passing to the replacement: intended?** *Default:* no. The replacement starts at 0, or at most at the slot's regen.
5. **Gas War to melee: still wanted, given that it unlocks Fuel Surge → one-shot?** *Default:* hold until Balloom's bulk is settled.
6. **Ice Wall: is it meant to stop heavies?** *Default:* yes, so add PRIOR together with the 2 AP proposal. Otherwise accept that it's a chip-damage reducer.
7. **Drain cap.** *Default:* cap enemy AP removal at 2 per target per turn, and give Memory Leak a 1-turn recharge.

## Reproduce

```
python3 /workspace/balance/out/tempo_2v2_sim.py /workspace/balance/s10 4000 /workspace/balance/out/tempo_2v2_sim.out.json   # ~90 s on 8 cores
python3 /workspace/balance/out/tempo_2v2_check.py /workspace/balance/s10                                                     # 114 PASS / 0 FAIL (incl. extension)
```

## Extension (2026-10-20): stacked walls and pre-emptive swaps

**Status: PROPOSAL analysis only.** Written 2026-10-20, about 09:10 CDT. Nothing in gameplay code or data was changed. Same method as the main study: engine mirror against snapshot s10, **random IVs**, **4000 battles per cell**. This extension uses stable crc32 seeds (`sseed`), so every cell in it reproduces exactly. Each comparison inside a group shares its seed.

Cronus noted that IV rolls weighing this heavily is intended, because it rewards the hunt. The random-IV spread is therefore kept as a feature in every number below. One example of the spread: Rattlestake eats Hammer Sync ×1.2 and survives 53% of the time, depending purely on rolls.

**New artifacts** (in `/workspace/balance/out/`):

| File | What it is |
|---|---|
| `tempo_2v2_sim.py` (extended) | New options, counters, kits and lines, plus the `ext` job set. The main matrix is bit-identical to v1 at the same seed. |
| `tempo_2v2_ext.out.json` | 286 cells × 4000 battles |
| `tempo_2v2_ext_tables.py` → `tempo_2v2_ext_tables.md` | Every extension cell |
| `tempo_2v2_check.py` (extended) | Now **114 PASS / 0 FAIL** (was 68). Adds engine facts, wall and data rows, and sanity checks on the extension cells. |

### X.1 Engine behaviour modelled (current code; file:line confirmed by the checker)

| Fact | Where | Consequence |
|---|---|---|
| Queue order: swaps → PRIOR block → Speed order → capture | `CombatResolver.cs:978`, `:988` | A turn-1 swap always resolves before every move, including a faster Seam Cut |
| Actions name a slot; the swap-in takes the hit | (queue entries keep the slot target) | The attacker **does not retarget** within the turn |
| "AP belongs to the active slot context": the swap-in takes the outgoing Chainling's current AP | `CombatResolver.cs:6688` | Swapping costs **0 AP** but uses that slot's action. One bench swap per team per turn. Frozen Assets / OnEscape can block a swap |
| SingleAlly falls back to the actor | `CombatResolver.cs:2701` | Ember Wall, Blockade, Compound Guard, Barrier Wall and Ice Wall can all be **self-cast** |
| Mitigation sources: same `stackKey` refreshes, different keys append, total is multiplicative | `ChainlingInstance.cs:456/487` | Guard Posture 25% + Blockade 25% = 43.75% taken off |
| `AddBarrier` clears existing barriers when `exclusiveBarriersAndWalls` | `ChainlingInstance.cs:850` | **Recasting Ember Wall resets it to 3 absorbs.** Barriers stack with percent walls |
| Ignore Walls (4.8-a) skips barriers on the first hit only | modifier 4.8-a (walls doc) | Percent walls still apply |
| DoT is not a hit | status tick path | Walls never reduce DoT |
| Walls stay on a benched Chainling (frozen) | walls doc | "Bank a wall, pivot out, pivot back" works today |
| Authored recharge includes the grant turn's end tick | `CombatResolver.cs:521` | "1-turn recharge" = `rc 2`; "2-turn recharge" = `rc 3` |
| Auto-run cap `maxTurns = 100`; the loop just stops, no result is emitted | `CombatBattleController.cs:114`, `:1375` | A battle that can't end has no defined outcome |

**Teams.** Guard Posture (Self, 1 AP, 25%/2 turns/1 hit) appears in **no species pool**. Only its move asset and the SFX catalog reference it, and the checker verifies this. All "gp" rows are therefore **PLAUSIBLE kits**: the fragile target gets Guard Posture in place of one move. Group B uses **natural, legal** wallers from real pools:
- Rattlestake: Compound Guard, self-cast
- Cinislug: Ember Wall
- Humpacked: Barrier Wall
- Blockwyrm: Blockade as the ally wall

Seam Cut, Partition Shot and Chaos Strike one-shot no natural waller body (≈0%), so the turn-1 burst tests use the plausible GP kits.

**Policies.**
- Attackers use smart targeting with a line-target bonus.
- The `dwall` defender policy:
  - The target self-walls and the ally walls the target whenever the threat is live or one regen away. Turn-1 burst and partner-focus lines always count as a threat.
  - A wall that is already up isn't refreshed unless it expires this turn and the waller is slower than the burster.
  - The policy banks for 3 AP walls.
- Group A's smart-target baseline is not the main matrix's baseline (L1 B none: KO by T2 35% here vs 50% there). **Compare within a group only.**

### X.2 Stacked walls on a fragile target (group A, PLAUSIBLE GP kits)

"KO by T2" means the target is KO'd by the end of turn 2. "Win" is the **attacking** team's win rate.

| Line (banked heavy) | none | ally Ember | ally Blockade | self GP | GP+Ember | GP+Blockade | GP+Ice | GP+Ice @2 AP | GP+Ice @2 AP+PRIOR |
|---|---|---|---|---|---|---|---|---|---|
| L1 B Hammer Sync → Deghoul | 34.9 (win 77) | **0** (94) | 51.2 (60)\* | 4.8 (96) | **0** (96) | 6.3 (52)\* | 5.8 (91) | 5.8 | 0.9 (96) |
| L1 BB (+Shard boost) | 65.1 (49) | **0** (94) | 64.5 (13)\* | 44.1 (60) | **0** (99) | 45.5 (16)\* | 43.6 (75) | 43.6 | 4.3 (96) |
| L2 B Cryo Ray → Crackett | 94.1 (98) | 0.3 | 94.2 | 28.0 | 0.1 | 27.9 | 28.4 | 27.1 | 4.0 |
| L3 B Deep Dig → Keyper | 93.9 (45) | **0** (97) | 93.8 (31)\* | 61.8 (57) | **0** (97) | 65.0 (29)\* | 63.4 (87) | **22.9** (91) | 9.7 (99) |
| L4 BB Hard Fork+Fuel Surge → Kitsauto | 3.4 (43)† | 0 | 3.7 | 51.7† | 0 | 52.6\* | 51.4 | 39.2 | 4.7 (T3 59) |

\* Blockade rows swap the ally for Blockwyrm (bench Hopoch), so their win rates are **roster-confounded**. Read only the KO columns for those rows.
† L4: Balloom normally dies first. The GP kit drops Fox Feint, which lets Hard Fork land (kit artifact).

| Turn-1 burst (2 AP) | none | any ally wall / self GP / GP+Ember / GP+Blockade | GP+Ice @2 AP+PRIOR | ally Ice @2 AP+PRIOR |
|---|---|---|---|---|
| S1 Seam Cut → Deghoul, **T1 KO** | 92.3 | 91.5–92.7 (walls are all too slow) | **26.1** (T2 92.0) | 24.2 (T2 92.7) |
| S2 Partition Shot → Deghoul, T1 KO | 65.0 | ally walls 63–65; anything with self GP 8.5–9.7 (Deghoul outspeeds Marrowhead) | 1.4 (T2 85) | 7.4 (T2 34.5) |
| L6 Chaos Strike → Keyper, T1 KO | 50.3 | 41–50 | 5.5 (T2 85) | 40.9 |
| L1 B partner focus (Seam Cut + Hammer Sync), T1 KO | 92.8 | 91.4–92.4 | 26.2 (T2 97) | 24.9 (T2 97) |

Three findings:

- **Stacking two percent walls works only when the target walls itself before the hit.**
  - The 1 AP Guard Posture is what goes up in time. The slow 3 AP ally wall adds ≈0: GP alone vs GP+Blockade is 4.8 vs 6.3 on L1 and 61.8 vs 65.0 on L3.
  - Against Deep Dig (100% one-shot), 43.75% off still leaves the KO at 62–65%.
  - Against Hammer Sync (79% one-shot) it drops the T2 KO from 35% to 5–6%, because the hit then falls just short.
- **Ember Wall is the whole answer whenever it's in the stack.** GP+Ember and ally Ember are identical on KO timing (0%).
- **No wall helps against a faster turn-1 burst** unless it carries **PRIOR**, and even then the target still dies on T2 85–97% of the time. A PRIOR wall buys exactly one turn.

**DoT ignores walls (A-DoT).** A Kindling partner focuses a GP+Blockade Deghoul. KO by T2 is 92.4% with no walls and 93.3% with the double wall. The walling team's loss rate rises from 50% to 98%.

**Ignore Walls (barriers only).** Against GP+Ember, Ignore Walls brings the KO back to exactly the GP-only level: L1 5.3%, L2 29%, L3 64%. The percent wall still applies after the barrier is bypassed, as the engine does.

### X.3 Natural teams (group B; legal pools)

| Line (bank BB/B) | none | self only | ally only (Blockade) | double | double + one-pct |
|---|---|---|---|---|---|
| NHS Hammer Sync → Rattlestake | 39.1 | 39.5 | 44.3 | 44.4 | 44.0 |
| NCR Cryo Ray → Rattlestake | 57.4 | **8.5** | 55.8 | 9.2 | 8.7 |
| NHF Hard Fork+FS → Rattlestake | 89.2 | **14.4** | 89.1 | 13.4 (T3 20.9) | 14.0 |
| NHFh Hard Fork+FS → Humpacked (Barrier Wall) | 94.6 | 90.7 | 94.6 | 90.3 | 91.0 |
| **NHFc Hard Fork+FS → Cinislug (Ember Wall on self)** | 94.8 | **0, battle hits the cap 100%** | 20.4 | **0, cap 100%** | 0, cap 100% |

- The ally Blockade from Blockwyrm (Speed 24, 3 AP) **never lands before the heavy**. Natural double walls equal self-walls.
- Hammer Sync ×1.2 overkills Rattlestake, so walls do nothing there.
- Barrier Wall from Humpacked (Speed 44) is slower than Balloom (52).

### X.4 The one real degenerate case: self-refreshing Ember Wall (flag)

Cinislug self-casts Ember Wall every turn: 2 AP, exactly its regen. Each recast resets the barrier to 3 absorbs (`AddBarrier` clears and re-adds). Every absorbed hit also drains 1 AP from the attacker. Two single-hit attackers deal at most 2 hits a turn, so they **never** break it:

| Variant (NHFc, Cinislug + Hopoch + Rattlestake) | Target KO by T3 | Battles hitting the 30-turn cap | Defender wall casts |
|---|---|---|---|
| engine | 0% | **99.8%** | 30 |
| attacker has Ignore Walls (Balloom) | 95.4% | 0% | 1.2 |
| multi-hit attacker (Kitsauto Blade Flurry, ST1) | 0% (KO turn 8.6) | 1% | 7.9 |
| **PROPOSAL: Ember Wall can't target its caster** | 95.4% | 0% | 0 |
| **PROPOSAL: Ember Wall 2-turn recharge (`rc 3`)** | 4.8% (KO turn 4.9) | **0%** | 1.2 |

Effect of the recharge on normal ally use:

| Ally Ember Wall line | KO by T3, engine | KO by T3, `rc 3` |
|---|---|---|
| L1 | 0% | 0.1% |
| L3 | 0% | 1.1% |
| GP+Ember | 0% | 0% |

The sim scores a capped battle on remaining HP. The engine's auto-run loop just stops at 100 turns with no result. The stall therefore yields an **undefined result** in the real game. PROPOSAL **R-EW1**: give Ember Wall a 2-turn recharge (`rc 3`). It removes the stall and leaves Ember Wall's job (stopping banked heavies on an ally) intact. The "no self-target" fix also works, but it deletes Cinislug's self-protection entirely. Separate PROPOSAL **R-CAP**: define a turn-cap outcome (for example, an HP-fraction decision) whatever happens to Ember Wall.

### X.5 Cost to the walling team

- **Walls trade the race for the target's life.** With the roster held constant, the attacking team's win rate goes **up** when the defenders wall:

  | Line | none | self GP | GP+Ember | GP+Ice |
  |---|---|---|---|---|
  | L1 B | 77% | 96% | 96% | 91% |
  | L3 B | 45% | 57% | 97% | 87% |

  - Defender actions per battle rise (L1: 7.9 → 11.1 with GP+Ember; 5.7 wall casts with GP+Blockade).
  - Defender damage dealt falls (L3: 388 → 241).
  - Natural NHS: when Blockwyrm walls instead of attacking, the attacking team's win rate goes 6% → 100%, and defender damage falls 380 → 142.
- Caveat: the GP kits drop a damaging move, and `dwall` walls whenever there is a threat. A human would wall more selectively, so these costs are an upper bound. The KO-delay numbers are the reliable part.

**Verdict on double walling: a fair-to-weak answer, not overtuned.**
- Double walling helps against banked heavies only when the target itself walls before the hit (Speed/AP timing).
- A second, slow ally wall adds ≈0.
- It is useless against faster turn-1 bursts, DoT, and overkill hits.
- It costs the race.
- The one overtuned pattern is the **self-refreshing Ember Wall stall** (X.4). Stacking itself isn't overtuned.

### X.6 What each walls proposal changes (PROPOSALS)

| Proposal | Effect measured | Verdict |
|---|---|---|
| **One percent wall per Chainling** (newer replaces older) | KO timing unchanged everywhere (≤1 pt; e.g. L1 GP+Blockade 6.3 → 6.7, NCR double 9.2 → 8.7). Win rates move only in roster-confounded Blockwyrm rows | Rules-clarity only. Harmless, and not needed for balance |
| **Walls drop on swap-out/KO** (group C, wall + pivot) | Ember Wall + pivot: KO by T2 **0 → 30.8%** (L1) and **0.3 → 34.3%** (L2). Blockade + pivot: no change | **Matters for Ember Wall.** It removes the "bank a barrier on the bench" trick. Recommend it |
| **Ice Wall at 2 AP, no priority** | No effect where the attacker outspeeds Gilarime (L1, S1, S2, L6). Big effect where Gilarime is faster and can now react on T2 without banking: L3 **63.4 → 22.9%** | Usability fix with a Speed-dependent upside |
| **Ice Wall at 2 AP + PRIOR** | The **only** turn-1 counter found: S1 T1 KO 92 → 26, L6 50 → 5.5, S2 65 → 1.4. L1 T2 0.9%, L3 9.7%. But T2 KO stays 85–97% on the burst lines, and the attacking team still wins 96–99% | **Fair.** Buys one turn and doesn't win games. Recommend it if Ice Wall is meant to answer heavies (Q6 default) |

### X.7 Pre-emptive turn-1 swap against 2 AP threats (group D)

**Mechanics** (engine):
- The swap resolves before the attack. The attack hits the slot, so the bulky swap-in takes Seam Cut / Partition Shot / Chaos Strike for ≈0% KO.
- There is **no in-turn retarget**.
- AP cost is 0 (the slot keeps its AP), but the slot loses its action. That's one free hit for the attacker, and the team's single bench swap for the turn is used up.
- When the fragile Chainling comes back, it gets the slot's current AP. The 2 AP burster is still there, and 2 AP is always affordable after regen, so **returning is punished immediately**.

The table below shows target KO by T1/T2/T3, mean KO turn, and the **attacking** team's win rate. "naive" = the attacker hits the target slot on T1. "predict" = the attacker expects the swap and hits the other slot on T1.

| Line | stay / naive | stay / predict | swap / naive | swap / predict | swap+return T3 / naive |
|---|---|---|---|---|---|
| S1 Seam Cut → Deghoul | T1 92.8, KO turn 1.1, win 97.8 | T2 92.2, 2.1, 98.2 | **T3 4.9, 4.8, 84.3** | T3 73.0, 3.3, 97.9 | T3 10.8, 4.2, 94.3 |
| S2 Partition Shot → Deghoul | T1 65.9, 1.6, 97.2 | T2 20.1, 2.9, 98.5 | T3 60.4, 3.5, 97.1 | T2 71.0, 2.3, 99.5 | T3 61.1, 3.5, 97.7 |
| L6 Chaos Strike → Keyper | T1 49.2, 2.0, 73.6 | T2 18.6, 3.3, 84.9 | **T3 8.7, 4.9, 92.6** | T3 45.4, 3.7, 98.6 | T3 24.0, 4.5, 90.3 |
| L1 B partner focus (Seam Cut + HS) | T1 91.7, 1.1, 93.2 | T2 96.5, 2.1, 82.1 | **T3 11.8, 5.6, 74.0** | T2 68.1, 2.4, 99.8 | T3 18.0, 4.6, 61.7 |

**KO delay.** Against a naive attacker, the pre-emptive swap delays the KO by **+3.7 turns** (S1), +1.9 (S2), +2.9 (L6) and +4.5 (L1 PF). A predicting attacker cuts the gain to about +1 turn or less, and in S2 the swap is then *worse* than staying.

**Mind games** (2×2 zero-sum, mixed equilibrium; defender {stay, swap} × attacker {naive, predict}):

- **Win rate.**
  - In S1, S2 and L6, "predict" is weakly dominant for the attacker: hitting the *ally* on T1 is at least as good for winning as killing the fragile target. The swap then doesn't change the game's value: about 98% (S1), 98.5% (S2), 85% (L6).
  - Only the partner-focus Hammer Sync line has a real mixed equilibrium: the defender swaps 30%, the attacker predicts 52%, and the attacking team's win rate is **87.4%** vs 93.2% if the defender always stays.
- **Target survival to T2** is a different payoff. Here the defender should always swap in S1, L6 and L1 PF: T2 KO at equilibrium is 5.8% / 8.5% / 68%. S2 mixes (swap 44%, predict 60%, T2 KO 43% vs 76% staying).

**Verdict.** The pre-emptive swap is the best existing answer to the 2 AP turn-1 one-shot: it is free in AP, resolves first, and nothing in the engine reads it. But it protects the *Chainling*, not the *game*. The attacker's best reply, hitting the other slot, keeps its win rate at 85–98%. It's a fair answer: it creates real guessing only when the attacker's damage is concentrated (partner focus). No change is proposed.

### X.8 Defaults pass (PROPOSALS)

**Deghoul +10 HP vs Seam Cut ×1.15** (common seeds):

| Line | base | Deghoul +10 HP | Seam Cut ×1.15 | both |
|---|---|---|---|---|
| S1 Seam Cut, T1 KO / attacking-team win | 91.3 / 97.2 | 77.9 / 95.8 | 80.3 / 95.0 | **56.5 / 90.8** |
| S2 Partition Shot, T1 KO / win | 63.5 / 97.2 | **41.4** / 95.8 | (n/a) | 41.4 / 95.8 |
| L1 B Hammer Sync, T2 KO / win | 49.5 / 82.0 | **33.3 / 74.4** | 49.2 / 81.2 | 35.5 / 72.6 |
| L1 B partner focus, T1 KO / win | 92.2 / 93.5 | 78.1 / 84.8 | 79.6 / 85.1 | 56.1 / 70.2 |
| L5 B Fork Split, T2 KO | 48.8 | **23.8** | (n/a) | 23.8 |

+10 HP is broad (it helps against every hit on Deghoul). ×1.15 helps only against Seam Cut. Neither alone gets Seam Cut's turn-1 kill under ~78%; both together reach 56%. **Revised PROPOSAL P1:** Deghoul +10 HP first. Add Seam Cut ×1.15 only if a ~78% turn-1 kill is still judged too high.

**Key Rotation's Evasion lasting 2 turns** (MeleeDef +1 stays permanent):
- L3 status line, attacking-team win: B **8.8 → 23.9**, BB **6.5 → 36.2**. Heavy lands 72 → 89%.
- L6: 96 → 99.
- Keyper now has to recast it (defender moves 12.8 → 15.4).

It is a real nerf that still leaves Key Rotation a good counter (Deep Dig's team wins only 24% in B). **P2 confirmed as proposed.**

**A KO'd Chainling's replacement at 0 AP or at one regen.**
- Under the base scripted policies this is **structurally zero**. Partners and replacements only use moves of ≤2 AP, which the +2 regen always covers, so inherited AP never gates an action.
- With `use_heavies` on (non-line Chainlings also fire affordable 3+ AP hits):

  | Rule | Attacking-team win shift |
  |---|---|
  | "0 AP" | −3.4 pts (L1 none), −2.8 (L6), ≈0 elsewhere |
  | "one regen" (min(inherited, 2)) | ≤0.4 pts everywhere |

- Banks are rarely large at the moment of a KO, because the banked hit usually fires first.
- **Revised PROPOSAL P4:** replacement starts at min(inherited, 2), i.e. one regen. It closes the "bank survives death" rule with ~0 balance impact. "0 AP" is a slightly harsher alternative.

**Memory Leak 1-turn recharge (`rc 2`).** It breaks the lock:

| Line | Mean KO turn | Attacking-team win |
|---|---|---|
| L3 B deny | 10.3 → 6.3 | 83.3 → 80.6 |
| L3 B deny_pre | 11.8 → 7.4 | 95.8 → 88.3 |
| L3 BB deny | 9.6 → 7.0 | **45.6 → 29.2** |

The freed defender actions go into damage. L2 deny_pre, T3 KO: 25 → 57. L1: no change. **R-drain revised:** Memory Leak rc 2 is the fix for the lock. The lock was never a win condition, and rc 2 makes denial teams play better, not worse.

### X.9 Revised proposals (all PROPOSALS)

| ID | Proposal | Evidence |
|---|---|---|
| **R-EW1 (new)** | Ember Wall: 2-turn recharge (`rc 3`) | Kills the infinite self-refresh stall (cap 99.8% → 0%) and leaves ally Ember Wall a full stop (L1/L3 KO by T3 ≤1.1%) |
| **R-CAP (new)** | Define a turn-cap result (HP-fraction decision or draw) | The auto-run loop stops at 100 turns with no result |
| **Walls drop on swap/KO** | Upgrade from "rules clarity" to **recommended** | Ember Wall + pivot 0% → 31–34% KO by T2 |
| **One percent wall per Chainling** | Optional (clarity only) | ≤1 pt on KO timing |
| **Ice Wall 2 AP + PRIOR** | Recommended if Ice Wall should answer heavies | The only turn-1 counter; buys one turn; doesn't swing wins |
| **P1** | Deghoul +10 HP (then Seam Cut ×1.15 only if needed) | S1 T1 91 → 78 (56 with both) |
| **P2** | Key Rotation Evasion lasts 2 turns | L3 status win 9 → 24 |
| **P4** | KO replacement AP = min(inherited, 2) | ≈0 balance impact |
| **R-drain** | Memory Leak `rc 2` | Lock KO turn 10 → 6 |

This supersedes (i) above: walls dropping on swap *does* matter once Ember Wall and a pivot are combined.

### X.10 Blind spots (extension)

- **Scripted policies.** `dwall` over-walls, and the attacker's prediction is a fixed rule. Win-rate costs are upper bounds. KO timing is the reliable output.
- **Plausible kits.** Guard Posture is in no pool, and the GP kits drop a move (L4's Fox Feint, a known artifact).
- **Roster confounds.** Blockade configs replace the ally with Blockwyrm, so read their KO columns only.
- **Cap mismatch.** The sim caps battles at 30 turns and scores them on HP. The engine caps at 100 with no outcome.
- **KO-AP test.** Structurally zero under the base policies. It needed `use_heavies`, which is still a scripted bank pattern.
- **Seed drift in the main matrix.** The main matrix uses Python's salted `hash()` seeds, so reruns drift about ±1.5 pts. The extension uses crc32 and is exact.
- **Not modelled:** Frozen Assets / OnEscape interrupting swaps, the AI's slot-0 targeting, and modifiers/held items.

### X.11 Questions for Cronus (each has a default)

1. **Ember Wall self-refresh stall:** fix it with a 2-turn recharge or with "no self-target"? *Default:* 2-turn recharge (`rc 3`).
2. **Turn-cap outcome:** what should happen at turn 100? *Default:* the higher remaining HP fraction wins; a tie is a draw.
3. **Walls on swap-out:** drop them? *Default:* yes, drop them on swap-out and on KO.
4. **Seam Cut turn-1 kill target:** is ~78% (Deghoul +10 HP alone) acceptable? *Default:* yes. Only add ×1.15 if playtests still feel bad.
5. **Pre-emptive swap:** leave it as the free, first-resolving answer to 2 AP bursts? *Default:* yes, no change.
6. **KO replacement AP:** 0 or one regen? *Default:* one regen (min(inherited, 2)).

### X.12 Reproduce

```
python3 /workspace/balance/out/tempo_2v2_sim.py /workspace/balance/s10 4000 /workspace/balance/out/tempo_2v2_ext.out.json ext   # ~95 s, 286 cells
python3 /workspace/balance/out/tempo_2v2_ext_tables.py > /workspace/balance/out/tempo_2v2_ext_tables.md
python3 /workspace/balance/out/tempo_2v2_check.py /workspace/balance/s10                                                            # 114 PASS / 0 FAIL
```

## Appendix: full matrix (4000 battles per cell, random IVs)

Burster modes: L = light spam, B = bank, BB = bank plus boost. "—" means not applicable.

**L1 Validape Hammer Sync → Deghoul**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 82 | 4.3 | 23 | 4.61 | — | — | — |
| L | deny | 82.9 | 4.4 | 23 | 4.52 | — | — | — |
| L | deny_pre | 100 | 0 | 0 | 6.95 | — | — | — |
| L | wall_ember | 92.2 | 0 | 3.7 | 4.77 | — | — | — |
| L | wall_blockade | 82.5 | 4.2 | 19.2 | 4.34 | — | — | — |
| L | swap | 82.8 | 4.3 | 23.8 | 4.54 | — | — | — |
| L | swap_pivot | 81.3 | 4.9 | 22.9 | 4.59 | — | — | — |
| L | focus | 99.7 | 2.8 | 45.2 | 3.53 | — | — | — |
| L | status | 98.9 | 2.6 | 45.8 | 3.58 | — | — | — |
| B | none | 81.5 | 50.2 | 50.3 | 3.78 | 66.8 | 2.08 | 53.2 |
| B | deny | 100 | 0 | 66.8 | 4.06 | 98.8 | 3.26 | 90.4 |
| B | deny_pre | 100 | 0 | 0 | 5.71 | 96.4 | 5.13 | 78 |
| B | wall_ember | 89 | 0 | 0 | 4.94 | 7.8 | 4.01 | 6.3 |
| B | wall_blockade | 62.7 | 49.7 | 49.8 | 3.44 | 66.2 | 2.08 | 50.9 |
| B | swap | 92.9 | 0.4 | 0.4 | 4.99 | 91.4 | 4.12 | 81.8 |
| B | swap_pivot | 94.9 | 0.6 | 8.6 | 4.52 | 88.3 | 4.05 | 78.9 |
| B | focus | 99.2 | 0.7 | 44.7 | 3.56 | 6.2 | 2.87 | 4.7 |
| B | status | 98.2 | 0.5 | 43.5 | 3.63 | 5.7 | 2.9 | 4.5 |
| BB | none | 49.9 | 63.5 | 63.5 | 4.33 | 73.7 | 2.26 | 71.2 |
| BB | deny | 96.7 | 59.1 | 59.1 | 3.89 | 96.5 | 3.67 | 90.4 |
| BB | deny_pre | 100 | 0 | 0 | 6.38 | 99.1 | 6.34 | 98.3 |
| BB | wall_ember | 78.4 | 0 | 0.1 | 4.96 | 29.6 | 4.03 | 29.3 |
| BB | wall_blockade | 14.3 | 64.8 | 64.8 | 2.98 | 81.2 | 2.41 | 72.9 |
| BB | swap | 47.3 | 0 | 0 | 8.48 | 41.5 | 5.62 | 34.4 |
| BB | swap_pivot | 33.5 | 0 | 34.3 | 6.88 | 22.6 | 5.91 | 19.2 |
| BB | focus | 65.2 | 0 | 65.8 | 3.36 | 0 | — | 0 |
| BB | status | 64.4 | 0 | 64.5 | 3.37 | 0.1 | 3 | 0.1 |

**L2 Gilarime Cryo Ray → Crackett**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 99.8 | 91.4 | 96 | 2.05 | — | — | — |
| L | deny | 99.8 | 90.5 | 95.9 | 2.06 | — | — | — |
| L | deny_pre | 99.9 | 7.2 | 57.5 | 3.33 | — | — | — |
| L | wall_ember | 100 | 8.4 | 59.8 | 3.29 | — | — | — |
| L | wall_blockade | 100 | 90.7 | 96 | 2.06 | — | — | — |
| L | swap | 99.7 | 91.3 | 96.2 | 2.06 | — | — | — |
| L | swap_pivot | 99.6 | 91.8 | 96.7 | 2.05 | — | — | — |
| L | focus | 96.7 | 19.6 | 91.2 | 2.96 | — | — | — |
| L | status | 99.8 | 37.4 | 69.8 | 2.94 | — | — | — |
| B | none | 98.7 | 93.7 | 93.8 | 2.18 | 95.6 | 2.01 | 94.7 |
| B | deny | 100 | 93.6 | 93.8 | 2.17 | 95.3 | 2.01 | 94.3 |
| B | deny_pre | 100 | 0 | 26 | 4.12 | 31.4 | 3.16 | 30.9 |
| B | wall_ember | 100 | 0.4 | 0.6 | 4.59 | 9.8 | 3.92 | 9.8 |
| B | wall_blockade | 99.7 | 93.8 | 93.8 | 2.17 | 95.8 | 2.01 | 94.7 |
| B | swap | 95.6 | 43.1 | 43.2 | 3.48 | 94.6 | 3.19 | 93.9 |
| B | swap_pivot | 97.7 | 42 | 42.1 | 3.38 | 95.1 | 3.23 | 94.6 |
| B | focus | 93.7 | 22.1 | 86 | 3.26 | 22.4 | 2 | 22.1 |
| B | status | 99.7 | 48.9 | 69.7 | 2.97 | 51.3 | 2.06 | 50.4 |
| BB | none | 83.9 | 95.1 | 95.1 | 2.24 | 95.6 | 2.01 | 95.6 |
| BB | deny | 98.7 | 95 | 95 | 2.16 | 95 | 2 | 95 |
| BB | deny_pre | 99.5 | 0 | 0 | 5.11 | 4.8 | 4.01 | 4.8 |
| BB | wall_ember | 58 | 0 | 0 | 6.37 | 0.6 | 4.48 | 0.6 |
| BB | wall_blockade | 97.8 | 94.8 | 94.8 | 2.16 | 95.3 | 2.01 | 95.2 |
| BB | swap | 70.2 | 0 | 0 | 6.04 | 82.6 | 4.96 | 82 |
| BB | swap_pivot | 77.1 | 0 | 0.3 | 5.98 | 85.2 | 5.36 | 84.8 |
| BB | focus | 68 | 7 | 19.2 | 4.55 | 6.9 | 2.01 | 6.9 |
| BB | status | 90.9 | 37.8 | 43.8 | 3.38 | 37.8 | 2 | 37.8 |

**L3 Kodiax Deep Dig → Keyper**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 84.1 | 94.5 | 96.6 | 1.58 | — | — | — |
| L | deny | 83.9 | 95.4 | 97 | 1.56 | — | — | — |
| L | deny_pre | 80.6 | 0 | 53 | 4.9 | — | — | — |
| L | wall_ember | 99 | 52.6 | 66.2 | 2.36 | — | — | — |
| L | wall_blockade | 85.5 | 94.1 | 96.3 | 1.55 | — | — | — |
| L | swap | 84.9 | 94.8 | 96.6 | 1.59 | — | — | — |
| L | swap_pivot | 83.5 | 94.7 | 96.7 | 1.57 | — | — | — |
| L | focus | 55 | 55.4 | 99 | 1.99 | — | — | — |
| L | status | 57.5 | 36.5 | 54.1 | 4.72 | — | — | — |
| B | none | 44.2 | 94.2 | 94.8 | 2.39 | 94.5 | 2.01 | 94.5 |
| B | deny | 83.7 | 0 | 0 | 10.25 | 17.4 | 6.92 | 17.4 |
| B | deny_pre | 96.2 | 0 | 0 | 11.82 | 0 | — | 0 |
| B | wall_ember | 96.8 | 0 | 1.2 | 4.43 | 4.7 | 4 | 4.7 |
| B | wall_blockade | 68.1 | 94.1 | 94.8 | 2.14 | 94.3 | 2.01 | 94.3 |
| B | swap | 21.8 | 0 | 0 | 10.47 | 13.9 | 5.3 | 13.9 |
| B | swap_pivot | 75.6 | 0 | 0 | 7.67 | 29.8 | 6.18 | 29.8 |
| B | focus | 7.3 | 5.7 | 87.1 | 3.55 | 0.1 | 2 | 0.1 |
| B | status | 8.8 | 2 | 2 | 8.23 | 71.6 | 2.23 | 15.1 |
| BB | none | 28.3 | 93.6 | 94.8 | 2.33 | 93.8 | 2 | 93.8 |
| BB | deny | 46.1 | 4.3 | 4.3 | 9.62 | 28.9 | 7.95 | 28.9 |
| BB | deny_pre | 27.4 | 0 | 0 | 11.36 | 0.7 | 12.66 | 0.7 |
| BB | wall_ember | 81.2 | 0 | 1.1 | 5.2 | 1.4 | 4.15 | 1.4 |
| BB | wall_blockade | 94.8 | 94.1 | 95.1 | 2.15 | 94.4 | 2.01 | 94.3 |
| BB | swap | 21.3 | 0 | 0 | 10.31 | 21.3 | 5.26 | 21.3 |
| BB | swap_pivot | 52.2 | 0 | 0.2 | 8.84 | 44.6 | 6.08 | 44.6 |
| BB | focus | 12.3 | 1.9 | 37.5 | 4.59 | 0.3 | 2 | 0.3 |
| BB | status | 5.5 | 4.8 | 4.8 | 8.02 | 72 | 2.23 | 17.2 |

**L4 Balloom Hard Fork (+Fuel Surge) → Kitsauto**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 78 | 1.6 | 14.6 | 4.12 | — | — | — |
| L | deny | 77.1 | 1.8 | 15.3 | 4.07 | — | — | — |
| L | deny_pre | 99 | 0 | 0 | 7.51 | — | — | — |
| L | wall_ember | 98.2 | 0 | 12.7 | 4.52 | — | — | — |
| L | wall_blockade | 51.2 | 1.6 | 4.2 | 4.06 | — | — | — |
| L | swap | 77 | 1.5 | 15.6 | 4.09 | — | — | — |
| L | swap_pivot | 77.4 | 1.7 | 15.1 | 4.09 | — | — | — |
| L | focus | 78.5 | 0.1 | 83.6 | 3.22 | — | — | — |
| L | status | 84.8 | 0 | 8.9 | 4.36 | — | — | — |
| B | none | 69.7 | 3.3 | 3.3 | 4.63 | 68.3 | 2 | 3.3 |
| B | deny | 97.7 | 0 | 0.1 | 8.87 | 82.7 | 5.68 | 72.8 |
| B | deny_pre | 99.5 | 0 | 0 | 10 | 97.8 | 6.4 | 78.5 |
| B | wall_ember | 94.7 | 0 | 0 | 4.9 | 0 | — | 0 |
| B | wall_blockade | 39.2 | 3.6 | 3.6 | 4.12 | 68.2 | 2 | 3.6 |
| B | swap | 31.7 | 0 | 0 | 8.39 | 35.7 | 4.02 | 2.5 |
| B | swap_pivot | 39.8 | 0 | 0 | 8.56 | 56.5 | 5 | 11.7 |
| B | focus | 78.6 | 0.1 | 80 | 3.45 | 0.1 | 2 | 0 |
| B | status | 60.8 | 0 | 2 | 5.09 | 5 | 5.22 | 0.6 |
| BB | none | 39.9 | 3.8 | 3.9 | 5.76 | 3.8 | 2 | 3.8 |
| BB | deny | 100 | 95.2 | 95.2 | 2.38 | 99.4 | 2.2 | 98.2 |
| BB | deny_pre | 100 | 0 | 0 | 10.19 | 99.9 | 6.44 | 87.8 |
| BB | wall_ember | 89 | 0 | 0.1 | 4.17 | 0.1 | 3 | 0 |
| BB | wall_blockade | 25.9 | 3.3 | 3.3 | 4.23 | 3.3 | 2 | 3.3 |
| BB | swap | 50.8 | 0 | 2 | 7.49 | 51.1 | 3.41 | 9.7 |
| BB | swap_pivot | 50.5 | 0 | 1.8 | 7.63 | 50.5 | 3.51 | 9.8 |
| BB | focus | 74.9 | 0.1 | 79.7 | 3.67 | 0 | — | 0 |
| BB | status | 61.7 | 0 | 1.9 | 5.08 | 5.3 | 3.92 | 2.1 |

**L4g Balloom Gas War (ranged) → Kitsauto**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 76.5 | 1.6 | 14.7 | 4.13 | — | — | — |
| L | deny | 77.2 | 1.4 | 14.1 | 4.1 | — | — | — |
| L | deny_pre | 98.9 | 0 | 0 | 7.53 | — | — | — |
| L | wall_ember | 98.4 | 0 | 12.3 | 4.51 | — | — | — |
| L | wall_blockade | 50.1 | 1.6 | 4.6 | 4.05 | — | — | — |
| L | swap | 76.8 | 1.6 | 14.5 | 4.1 | — | — | — |
| L | swap_pivot | 77 | 1.6 | 14.8 | 4.11 | — | — | — |
| L | focus | 79.2 | 0.1 | 83.6 | 3.23 | — | — | — |
| L | status | 85.7 | 0 | 8.4 | 4.36 | — | — | — |
| B | none | 66.1 | 0 | 0 | 4.98 | 66.7 | 2 | 0 |
| B | deny | 66.5 | 0 | 0 | 8.67 | 79.2 | 5.55 | 11.7 |
| B | deny_pre | 80.3 | 0 | 0 | 10.23 | 95.7 | 6.33 | 27.1 |
| B | wall_ember | 92.9 | 0 | 0 | 4.84 | 0 | — | 0 |
| B | wall_blockade | 25.1 | 0 | 0 | 4.26 | 67.3 | 2 | 0 |
| B | swap | 12.2 | 0 | 0 | 9.08 | 34.1 | 4.03 | 0.1 |
| B | swap_pivot | 21.8 | 0 | 0 | 8.16 | 79 | 5.6 | 28.6 |
| B | focus | 77.9 | 0.1 | 80.5 | 3.49 | 0.1 | 2 | 0 |
| B | status | 61.2 | 0 | 1.7 | 5.04 | 5.6 | 6.1 | 4.3 |
| BB | none | 37.6 | 0 | 0.2 | 5.86 | 3.9 | 2 | 0.1 |
| BB | deny | 91.3 | 0 | 0 | 6.61 | 98.7 | 2.14 | 25.9 |
| BB | deny_pre | 98.4 | 0 | 0 | 11.07 | 99.7 | 6.47 | 38.8 |
| BB | wall_ember | 88.7 | 0 | 0.1 | 4.17 | 0 | — | 0 |
| BB | wall_blockade | 26.5 | 0 | 0 | 4.33 | 3.7 | 2 | 0 |
| BB | swap | 20.9 | 0 | 0 | 8.29 | 48.7 | 3.37 | 2.9 |
| BB | swap_pivot | 20.6 | 0 | 0 | 8.32 | 54.4 | 3.68 | 3.1 |
| BB | focus | 74.7 | 0 | 80 | 3.65 | 0 | — | 0 |
| BB | status | 62.2 | 0 | 2 | 4.93 | 5.4 | 4.68 | 5 |

**L5 Forkrook Fork Split → Deghoul**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 91.5 | 31.1 | 97 | 2.74 | — | — | — |
| L | deny | 91.2 | 30.8 | 97.2 | 2.73 | — | — | — |
| L | deny_pre | 99.8 | 3.5 | 37.8 | 3.71 | — | — | — |
| L | wall_ember | 99.2 | 0.1 | 84.9 | 3.16 | — | — | — |
| L | wall_blockade | 93.5 | 30.9 | 97 | 2.72 | — | — | — |
| L | swap | 91.8 | 32.1 | 96.9 | 2.74 | — | — | — |
| L | swap_pivot | 91.2 | 29.9 | 96.9 | 2.76 | — | — | — |
| L | focus | 97.9 | 83 | 99.7 | 2.18 | — | — | — |
| L | crackett_iso | 95 | 30.2 | 97.7 | 2.74 | — | — | — |
| B | none | 96.7 | 46.5 | 87.1 | 2.69 | 95 | 2 | 47.5 |
| B | deny | 98.7 | 46.5 | 87.7 | 2.78 | 96.1 | 2.04 | 55.8 |
| B | deny_pre | 100 | 0 | 5.1 | 5.35 | 99.3 | 4.13 | 79.6 |
| B | wall_ember | 99.3 | 0.1 | 75.6 | 3.27 | 2.1 | 3.86 | 1.2 |
| B | wall_blockade | 97.9 | 45.8 | 88 | 2.66 | 94.8 | 2 | 46.8 |
| B | swap | 91.8 | 49.4 | 71.1 | 3.1 | 51 | 2.01 | 25.6 |
| B | swap_pivot | 92.4 | 48.3 | 70.9 | 3.02 | 51.1 | 2.01 | 24.9 |
| B | focus | 97.4 | 83 | 99.3 | 2.18 | 21.9 | 2 | 10.8 |
| B | crackett_iso | 80 | 48.1 | 85.8 | 2.9 | 95 | 2 | 49 |
| BB | none | 70 | 99.2 | 99.8 | 1.37 | 33.3 | 2 | 30.8 |
| BB | deny | 95.3 | 99.7 | 99.9 | 1.36 | 33.5 | 2 | 30.9 |
| BB | deny_pre | 99.5 | 96.6 | 99.7 | 1.39 | 0 | — | 0 |
| BB | wall_ember | 98.7 | 65.5 | 98.7 | 1.7 | 0 | — | 0 |
| BB | wall_blockade | 98.4 | 99.7 | 100 | 1.35 | 32.8 | 2 | 30.5 |
| BB | swap | 64.6 | 63.8 | 64.5 | 3.67 | 0.2 | 6 | 0.2 |
| BB | swap_pivot | 88.8 | 64.6 | 70.3 | 2.2 | 8.6 | 4.17 | 8.1 |
| BB | focus | 96.3 | 99.5 | 100 | 1.36 | 4.5 | 2 | 4.2 |
| BB | crackett_iso | 79 | 97.5 | 99.3 | 1.4 | 33.8 | 2 | 31.4 |

**L6 Kreechank Chaos Strike (2 AP) → Keyper**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 75 | 68.5 | 97.2 | 1.92 | — | — | — |
| L | deny | 74 | 66 | 97 | 1.94 | — | — | — |
| L | deny_pre | 74 | 66.3 | 96.8 | 1.98 | — | — | — |
| L | wall_ember | 91.5 | 67.8 | 98.3 | 1.83 | — | — | — |
| L | wall_blockade | 85.8 | 66.4 | 98 | 1.85 | — | — | — |
| L | swap | 74.5 | 66.8 | 97 | 1.97 | — | — | — |
| L | swap_pivot | 75.7 | 68.2 | 96.9 | 1.96 | — | — | — |
| L | focus | 98.7 | 69.6 | 98.9 | 2.18 | — | — | — |
| L | status | 96.2 | 30.8 | 40.3 | 3.72 | — | — | — |
| B | none | 74.4 | 65.9 | 97.1 | 1.97 | 0 | — | 0 |
| B | deny | 74.7 | 66.8 | 97.3 | 1.97 | 0 | — | 0 |
| B | deny_pre | 74.3 | 66.6 | 97.1 | 1.94 | 0 | — | 0 |
| B | wall_ember | 90.8 | 66.8 | 98 | 1.85 | 0 | — | 0 |
| B | wall_blockade | 87 | 66.1 | 98 | 1.86 | 0 | — | 0 |
| B | swap | 75.5 | 66.8 | 97 | 1.95 | 0 | — | 0 |
| B | swap_pivot | 75.4 | 66.3 | 96.9 | 1.99 | 0 | — | 0 |
| B | focus | 98.8 | 68.4 | 99.2 | 2.2 | 0 | — | 0 |
| B | status | 95.7 | 32.4 | 40.5 | 3.72 | 0 | — | 0 |
| BB | none | 58.3 | 34.8 | 96.5 | 2.81 | 0 | — | 0 |
| BB | deny | 58.6 | 35.6 | 96.7 | 2.78 | 0 | — | 0 |
| BB | deny_pre | 58.4 | 34.6 | 96.7 | 2.81 | 0 | — | 0 |
| BB | wall_ember | 84.2 | 34.2 | 98.3 | 2.67 | 0 | — | 0 |
| BB | wall_blockade | 84.3 | 35.1 | 98.7 | 2.66 | 0 | — | 0 |
| BB | swap | 58.7 | 34.9 | 96.8 | 2.78 | 0 | — | 0 |
| BB | swap_pivot | 57.4 | 34.1 | 97 | 2.8 | 0 | — | 0 |
| BB | focus | 99 | 55.7 | 99.7 | 2.45 | 0 | — | 0 |
| BB | status | 98.3 | 32.1 | 43.5 | 3.49 | 0 | — | 0 |

**L7 COUNTERFACTUAL Cinislug Finish Burn (3 AP) → Deghoul**

| Burster | Counter | Win % | Target KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|
| L | none | 90.4 | 81.4 | 89.5 | 3.06 | — | — | — |
| L | deny | 90.7 | 80.7 | 88.9 | 3.12 | — | — | — |
| L | deny_pre | 100 | 0 | 0 | 7.24 | — | — | — |
| L | wall_ember | 100 | 0 | 43.4 | 6.3 | — | — | — |
| L | wall_blockade | 97.8 | 76.5 | 82.5 | 3.74 | — | — | — |
| L | swap | 90.5 | 82 | 90 | 3.01 | — | — | — |
| L | swap_pivot | 90.6 | 83.2 | 90.3 | 2.95 | — | — | — |
| L | focus | 99.9 | 17.6 | 94.3 | 3.19 | — | — | — |
| L | status | 100 | 55.1 | 86.9 | 3.22 | — | — | — |
| B | none | 94.7 | 87.4 | 91.6 | 2.86 | 99.4 | 2.04 | 91.6 |
| B | deny | 100 | 0 | 25.7 | 4.71 | 99.9 | 4.07 | 91.9 |
| B | deny_pre | 100 | 0 | 0 | 6.54 | 99.8 | 6.05 | 91.9 |
| B | wall_ember | 99.8 | 0 | 82.1 | 3.8 | 89 | 3.01 | 82.5 |
| B | wall_blockade | 98 | 72.5 | 82.8 | 3.75 | 99.4 | 2.05 | 82.8 |
| B | swap | 55.9 | 0 | 0 | 7.06 | 30.2 | 5.03 | 28 |
| B | swap_pivot | 59.1 | 0 | 0.1 | 6.83 | 32.5 | 5.11 | 30.1 |
| B | focus | 98.2 | 18.8 | 92.3 | 3.02 | 20.5 | 2 | 18.8 |
| B | status | 99.5 | 60 | 89.5 | 2.94 | 65.5 | 2 | 60.1 |
| BB | none | 94.9 | 87.9 | 92.3 | 2.79 | 99.5 | 2.04 | 92.3 |
| BB | deny | 100 | 0 | 27.1 | 4.71 | 99.9 | 4.04 | 91.6 |
| BB | deny_pre | 100 | 0 | 0 | 6.49 | 99.7 | 5.99 | 91.7 |
| BB | wall_ember | 99.9 | 0 | 82.8 | 3.76 | 89.1 | 3 | 83 |
| BB | wall_blockade | 97.8 | 73.3 | 83.3 | 3.69 | 99.4 | 2.05 | 83.3 |
| BB | swap | 57 | 0 | 0 | 7.02 | 29.6 | 5.02 | 27 |
| BB | swap_pivot | 58.7 | 0 | 0 | 6.88 | 32.2 | 5.12 | 29.6 |
| BB | focus | 98.7 | 19.1 | 92.3 | 3.02 | 20.8 | 2 | 19.1 |
| BB | status | 99.8 | 59.7 | 89.5 | 2.94 | 65 | 2 | 59.8 |

**Variants**

| Line | Burster | Variant | Win % | KO by T2 % | by T3 % | Mean KO turn | Heavy lands % | Mean heavy turn | Heavy KOs % |
|---|---|---|---|---|---|---|---|---|---|
| L1 | B | wall_ice | 88.2 | 50.2 | 50.3 | 3.38 | 66.5 | 2.11 | 54.4 |
| L1 | BB | wall_ice | 58.8 | 63.5 | 63.5 | 2.91 | 81.1 | 2.43 | 80.7 |
| L1 | B | wall_compound | 95.9 | 48.5 | 48.6 | 3.46 | 65.8 | 2.09 | 51.5 |
| L1 | BB | wall_compound | 94.6 | 64.8 | 64.8 | 3.21 | 82.2 | 2.42 | 65 |
| L1 | B | wall_barrier | 95.2 | 45.8 | 45.8 | 3.54 | 64.3 | 2.1 | 49.8 |
| L1 | BB | wall_barrier | 98.6 | 32.4 | 32.5 | 3.66 | 80.7 | 2.4 | 57.6 |
| L1 | B | wall_ice@2AP | 67.5 | 51.3 | 51.4 | 3.67 | 67.5 | 2.09 | 54.8 |
| L1 | B | deny+cap2 | 100 | 0 | 66.3 | 4.06 | 98.7 | 3.27 | 91.3 |
| L1 | B | swap_pivot+walls_drop | 94.7 | 0.7 | 8.8 | 4.51 | 88 | 4.05 | 78.9 |
| L1 | BB | wall_ice@2AP | 29.6 | 62.4 | 62.4 | 3.29 | 79.7 | 2.4 | 75.7 |
| L1 | BB | deny+cap2 | 96.2 | 59.3 | 59.4 | 3.85 | 96.6 | 3.64 | 91.5 |
| L1 | BB | swap_pivot+walls_drop | 33.5 | 0 | 35.2 | 6.82 | 22.7 | 5.94 | 19.5 |
| L1 | L | iv_b+5_d-5 | 98.7 | 41.4 | 69.8 | 3.2 | 0 | — | 0 |
| L1 | L | iv_b-5_d+5 | 2.7 | 0.1 | 1.6 | 9.36 | 0 | — | 0 |
| L1 | L | iv_all0 | 80.3 | 3.7 | 27.1 | 4.68 | 0 | — | 0 |
| L1 | L | typeless_neutral | 86.7 | 27.6 | 48.9 | 3.77 | 0 | — | 0 |
| L1 | B | iv_b+5_d-5 | 98.8 | 64 | 64 | 3.07 | 68.7 | 2.14 | 68.7 |
| L1 | B | iv_b-5_d+5 | 1.7 | 3.7 | 3.8 | 10.02 | 62.1 | 2.01 | 4.3 |
| L1 | B | iv_all0 | 87.6 | 63 | 63 | 3.28 | 66.2 | 2.1 | 66.2 |
| L1 | B | typeless_neutral | 82.3 | 49.6 | 49.7 | 3.77 | 65.1 | 2.1 | 53.3 |
| L4g | L | gas_war_melee | 76.6 | 1.7 | 15.2 | 4.1 | 0 | — | 0 |
| L7 | L | finish_burn@4AP | 91 | 82.2 | 90 | 3.01 | 0 | — | 0 |
| L1 | L | hammer_sync@6AP | 81.7 | 4.2 | 22.1 | 4.62 | 0 | — | 0 |
| L3 | L | deep_dig@6AP | 85 | 95.2 | 97 | 1.57 | 0 | — | 0 |
| L4g | B | gas_war_melee | 69.4 | 3.2 | 3.2 | 4.59 | 66.6 | 2 | 3.2 |
| L7 | B | finish_burn@4AP | 75.3 | 86.9 | 86.9 | 2.98 | 96.3 | 2.03 | 88.4 |
| L1 | B | hammer_sync@6AP | 79.1 | 0 | 36.4 | 4.63 | 45.9 | 3 | 36.4 |
| L3 | B | deep_dig@6AP | 6.1 | 0 | 32.9 | 7.25 | 32 | 3 | 32 |
| L4g | BB | gas_war_melee | 38.6 | 3.8 | 3.9 | 5.75 | 3.8 | 2 | 3.8 |
| L7 | BB | finish_burn@4AP | 75.7 | 87.2 | 87.2 | 2.97 | 96 | 2.03 | 88.3 |
| L1 | BB | hammer_sync@6AP | 8.5 | 0 | 35 | 7.07 | 44.5 | 3 | 35 |
| L3 | BB | deep_dig@6AP | 3.2 | 0 | 34.3 | 5.82 | 33.2 | 3 | 33.2 |
| L1 | L | partner_focus | 94.5 | 99.7 | 100 | 1.07 | 0 | — | 0 |
| L1 | L | wall_ember+partner_focus | 99.9 | 93.7 | 99.8 | 1.13 | 0 | — | 0 |
| L1 | B | partner_focus | 93.2 | 99.8 | 100 | 1.08 | 0.2 | 2 | 0.2 |
| L1 | B | wall_ember+partner_focus | 99.9 | 92 | 99.2 | 1.17 | 0 | — | 0 |
| L1 | BB | partner_focus | 31.8 | 97.3 | 98.5 | 2.06 | 6.2 | 2.15 | 6.1 |
| L1 | BB | wall_ember+partner_focus | 83 | 0 | 0.1 | 4.68 | 6.5 | 4.05 | 6.5 |
| L2 | L | partner_focus | 99.5 | 100 | 100 | 1.09 | 0 | — | 0 |
| L2 | L | wall_ember+partner_focus | 100 | 90.5 | 99.6 | 1.19 | 0 | — | 0 |
| L2 | B | partner_focus | 86.2 | 99.5 | 99.8 | 1.98 | 58.1 | 2 | 58.1 |
| L2 | B | wall_ember+partner_focus | 100 | 3.6 | 90.5 | 3.03 | 0.6 | 4 | 0.6 |
| L2 | BB | partner_focus | 66.5 | 95.3 | 95.4 | 2.13 | 92.6 | 2 | 92.6 |
| L2 | BB | wall_ember+partner_focus | 57 | 0 | 0 | 6.3 | 0.5 | 4.38 | 0.5 |
| L3 | L | partner_focus | 94.8 | 99.8 | 99.9 | 1.07 | 0 | — | 0 |
| L3 | L | wall_ember+partner_focus | 99.5 | 93.8 | 97.8 | 1.17 | 0 | — | 0 |
| L3 | B | partner_focus | 52 | 95.9 | 98 | 2.07 | 54.6 | 2 | 54.6 |
| L3 | B | wall_ember+partner_focus | 94.5 | 0 | 42 | 4.14 | 0 | — | 0 |
| L3 | BB | partner_focus | 39.7 | 94 | 95.1 | 2.27 | 94.2 | 2 | 94.2 |
| L3 | BB | wall_ember+partner_focus | 93.4 | 0 | 5.2 | 4.97 | 0 | — | 0 |
| L4 | L | partner_focus | 70.3 | 61.6 | 94 | 2.48 | 0 | — | 0 |
| L4 | L | wall_ember+partner_focus | 98.7 | 0 | 30.1 | 4.39 | 0 | — | 0 |
| L4 | B | partner_focus | 62.8 | 61.4 | 83 | 2.79 | 67.2 | 2 | 60 |
| L4 | B | wall_ember+partner_focus | 95.1 | 0 | 1.6 | 4.57 | 0 | — | 0 |
| L4 | BB | partner_focus | 27.8 | 3.4 | 55 | 3.92 | 3.4 | 2 | 3.4 |
| L4 | BB | wall_ember+partner_focus | 85.3 | 0 | 5.5 | 4 | 0 | — | 0 |
| L4g | L | partner_focus | 69.7 | 61.4 | 94.6 | 2.46 | 0 | — | 0 |
| L4g | L | wall_ember+partner_focus | 98.6 | 0 | 28.9 | 4.41 | 0 | — | 0 |
| L4g | B | partner_focus | 38.9 | 10.6 | 68.1 | 3.47 | 67.9 | 2 | 10.1 |
| L4g | B | wall_ember+partner_focus | 90.9 | 0 | 1.6 | 4.33 | 0 | — | 0 |
| L4g | BB | partner_focus | 27.4 | 0.5 | 55.5 | 3.9 | 3.8 | 2 | 0.6 |
| L4g | BB | wall_ember+partner_focus | 85 | 0 | 4.7 | 4.04 | 0 | — | 0 |
| L5 | L | partner_focus | 84.4 | 99.4 | 100 | 1.07 | 0 | — | 0 |
| L5 | L | wall_ember+partner_focus | 99.1 | 92.7 | 100 | 1.15 | 0 | — | 0 |
| L5 | B | partner_focus | 76.8 | 99.1 | 100 | 1.38 | 35.3 | 2 | 32.7 |
| L5 | B | wall_ember+partner_focus | 98.9 | 63.9 | 99 | 1.73 | 0 | — | 0 |
| L5 | BB | partner_focus | 76.5 | 99.5 | 100 | 1.36 | 33.6 | 2 | 31.4 |
| L5 | BB | wall_ember+partner_focus | 99 | 63.2 | 99.2 | 1.75 | 0.1 | 4 | 0.1 |
| L6 | L | partner_focus | 85 | 99.6 | 100 | 1.07 | 0 | — | 0 |
| L6 | L | wall_ember+partner_focus | 97 | 99.7 | 100 | 1.06 | 0 | — | 0 |
| L6 | B | partner_focus | 85.8 | 99.8 | 100 | 1.06 | 0 | — | 0 |
| L6 | B | wall_ember+partner_focus | 96.7 | 99.7 | 100 | 1.06 | 0 | — | 0 |
| L6 | BB | partner_focus | 50.6 | 97.9 | 99.9 | 1.68 | 0 | — | 0 |
| L6 | BB | wall_ember+partner_focus | 72.5 | 97.7 | 100 | 1.67 | 0 | — | 0 |
| L7 | L | partner_focus | 92.8 | 80.3 | 80.4 | 3.29 | 0 | — | 0 |
| L7 | L | wall_ember+partner_focus | 100 | 33.2 | 83.7 | 3.78 | 0 | — | 0 |
| L7 | B | partner_focus | 89.8 | 98.2 | 100 | 2.02 | 31.1 | 2.02 | 31.1 |
| L7 | B | wall_ember+partner_focus | 100 | 0 | 88.5 | 3.67 | 20.9 | 3 | 20.9 |
| L7 | BB | partner_focus | 89.3 | 98.5 | 100 | 2.01 | 31.9 | 2.01 | 31.9 |
| L7 | BB | wall_ember+partner_focus | 100 | 0 | 88 | 3.73 | 19.8 | 3 | 19.8 |
