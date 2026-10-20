# Enemy AI: audit and tiered "smart AI" proposal

Date: 2026-10-17. Docs only. No code, data, or asset was changed. `Docs/GameTheory/` was not read or edited.

**Status: audit plus proposals.** Section 1 describes the code as it is in the local working tree (branch `backend/quest-move-inventory`, uncommitted changes included). Everything in sections 2 to 5 is a **proposal**. Tier names (Rookie, Tactician, Mastermind), class names, file names, weights, and thresholds are suggestions, not decisions. Anything that relies on a mechanic the engine does not have yet is marked **(proposed mechanic)**.

Sources read: `Assets/Scripts/Combat/Runtime/AI/*.cs` (including `CombatStateEvaluator.cs`), `CombatResolver.cs`, `CombatActionEligibilityService.cs`, `CombatApService.cs`, `DamageCalculator.cs`, `ChemistryResolver.cs`, `StatusProcessor.cs`, `CombatRng.cs`, `CombatState.cs`, `TeamState.cs`, `ChainlingInstance.cs`, `MoveInstance.cs`, `CombatConstants.cs`, `CombatBattleController.cs`, `Combat2TestHarness.cs`, the encounter providers, `BattleSnapshots.cs`, `Moves.csv`, `SpeciesMovePools.csv`, `COMBAT_TypeProfiles.md`, `AP_ECONOMY_ANALYSIS.md` (Matchup D, §6.2), `PASSIVES_AND_STATUSES_ANALYSIS.md` (§5, P1), `COMBAT_SYSTEMS_ANALYSIS.md` §2 and `COMBAT_DESIGN_IDEAS.md` §4, plus `Move_bean_sip.asset` and `CofiLootwak_Encounter.asset`.

Line numbers are for the working tree as of today. `CombatResolver.cs` is large (about 7,200 lines) and changes often, so treat those numbers as close pointers, not exact anchors.

---

## 0. Summary

- **Every enemy hits the same slot.** No AI writes a target onto its action, so every single-target move falls back to the first living active foe (slot 0). Target scoring exists, but the result is thrown away.
- **The damage estimate is not the engine's damage.** `AIHelpers.EstimateDamage` uses `Power × Atk/100 × hits × accuracy`. It ignores the defender's defense, type chemistry (×1.2 / ×0.8), the +1 constant, mitigation, AoE target count, evasion, and crits. KO checks built on it are often wrong in both directions.
- **Cost-0 moves score 0 and denial is never chosen.** `DamagePerAP` returns 0 at cost 0, so All-In is Aggressive's worst click. No AI reads AP payloads (`DrainAP`, `StealAP`, `GainAP`) or the foe's AP. Aggressive scores denial at 0.1. Utility prefers Debuff Script.
- **No AI reasons about speed, priority, recharge, or affordability after denial.** AP is checked at selection and checked again at execution (`CombatResolver.cs:1554-1562`, `:1648-1654`). It is never reserved, so a faster foe's steal can make the AI's move fail.
- **Personality is set per encounter, not per species.** Every encounter source defaults to Aggressive. Only Cofi (Utility) and the Manual provider (Random) differ. `EncounterDifficulty` exists, but no AI reads it.
- **The AI can see everything in `CombatState`.** That includes full player kits, the bench, AP, and statuses. It also gets the live combat RNG, whose public `Seed` and `CallCount` are enough to predict future rolls. No current AI cheats. The player's chosen action is never passed in, so that cannot leak today.
- **Proposal:** one shared utility-scoring brain, with three tiers that differ by factors weighed, lookahead, noise, and knowledge. Existing personalities become weight profiles on top of a tier. Bosses get a tier, a profile, and small data-driven hooks.

---

## 1. Audit of the current AI

### 1.1 How the AI is wired

| Piece | Where | What it does |
|---|---|---|
| Interface | `ICombatAI.cs:13-29` | `List<CombatAction> GenerateActions(CombatState state, int teamIndex, CombatRng rng)`. One action per active slot. The summary says it is called after AP gain. |
| Personality enum | `Combat2TestHarness.cs:633-649` | `AIPersonality { Dumb, Aggressive, Defensive, Utility, Random }`, serialized as ints (Dumb 0 … Random 4). The comment at `:631` says "In the full game, these will be assignable per species definition." That has not been built. |
| Factory (two copies) | `CombatBattleController.cs:2693-2703`, `Combat2TestHarness.cs:256-266` | Same switch in both places. Unknown values fall back to `DumbAI`. |
| CPU personality source | `CombatBattleController.cs:503-505` | `_cpuAI = CreateAI(encounter.aiPersonality)`, always from the encounter snapshot. The Inspector field `cpuAI` (`:85`) is never read, even though its tooltip says it is "overridden by EncounterSnapshot if provided". |
| Player-side autoplay | `CombatBattleController.cs:83`, `:501`, `:1458` | `playerAI` drives team 0 when `usePlayerInput` is false (test and replay mode). |
| Call order in the live scene | `CombatBattleController.cs:~1407` (`_resolver.BeginTurn`), then the player-input wait, then `:1465` | AP gain happens first. The player submits next. The CPU AI is called last, with `_state` and `_resolver.Rng` only. The player's `actions0` exists in that method but is not passed in. |
| Call order in sims | `CombatResolver.cs:252-278` `ExecuteTurnWithAI` | AP gain and turn-start triggers (`:266-269`), then `ai0` and `ai1` with the same state and the same RNG (`:273-274`). |
| Snapshot field | `BattleSnapshots.cs:179`, default Aggressive at `:243` | `EncounterSnapshot.aiPersonality`. |
| Manual / boss encounters | `ManualEncounterProviderSO.cs:27`, `:80` | Per-asset Inspector field, default Aggressive. |
| Wild encounters | `ZoneEncounterProvider.cs:15`, `:29` | One `defaultAI` for every wild battle, default Aggressive. Species does not matter. |
| Authoritative battles | `AuthoritativeBattleEncounterProvider.cs:26` | Hard-coded Aggressive. |
| Checked-in encounters | `Assets/Data/Combat/BattleProviders/*.asset` | `CofiLootwak_Encounter` uses `aiPersonality: 3` (Utility) and `difficulty: 2` (Boss). `IntroFirstCreature_Encounter` uses 1 (Aggressive). `ManualEncounterProvider` uses 4 (Random). `CoffeeCourierHuntAuthoring.cs:691-693` writes 3 for the Cofi boss. No encounter uses Defensive. |
| Other users of AI code | `CombatResolver.cs:3454-3484` `BuildRandomLegalMisAct` | The **Glitched Mind** status picks a random action from `AIHelpers.GetLegalActions`. Changing that helper's output changes Glitched Mind's odds and its RNG draws, so replays would shift. |

**Answer: personality is assigned per encounter**, through the snapshot. It is not set per species or per Chainling. One AI instance drives both CPU actives. Boss status (`EncounterDifficulty.Boss`) changes rewards and flee rules. It does not change the AI.

### 1.2 Shared plumbing (`AIHelpers.cs`)

| Helper | Lines | Behaviour | Problem |
|---|---|---|---|
| `GetLegalActions` | 15-73 | Lists every move that passes `EvaluateMoveSelection` (AP, recharge, gates, HP cost), plus a swap to each free bench slot. Pass is added only when nothing else is legal. | Moves are built with `CombatAction.UseMove(teamIndex, i)` (`:41`), the overload that sets `targetTeamIndex = targetSlotIndex = -1` (`CombatState.cs:156-166`). The targeted overload already exists (`CombatState.cs:168-178`) and is unused. The `enemyTeam` parameter is unused. Swap reservations are read from state (`:47-54`), but they are only written during resolution, so two CPU actives can both choose the same single bench slot. |
| `PickTarget` | 98-123 | LowestHP, HighestThreat, or MostVulnerable, comparing `targets[0]` with `targets[1]`. | Its result is only used inside scores. It never reaches the action. |
| `EstimateDamage` | 129-142 | `basePower × (Atk/100) × avgHits × accuracy/100`. Attacker stages are included through `GetEffectiveStat`. | No defense, chemistry, +1 constant, mitigation or walls, evasion or accuracy stages, crits, or AoE ×2. |
| `DamagePerAP` | 145-149 | `EstimateDamage / apCost`. | **Returns 0 when `apCost <= 0`** (`:147`). All-In (`Moves.csv:191`, cost 0, power 98) scores as the worst damaging move. |
| `ThreatScore` / `VulnerabilityScore` | `CombatStateEvaluator.cs:115-138` | Threat is `0.6 × min(1, avg(Atk)/300) + 0.4 × HP%`. Vulnerability is `1 − HP%`. | Threat ignores the foe's moves, AP, speed, chemistry against our actives, and what it can actually KO. `CombatStateEvaluator.Evaluate` (`:20-53`) exists, but no AI calls it. |

**Target-slot fixation, the root cause.** With no explicit target, `CombatResolver.ResolveSingleSelectedTarget` (`:2804-2847`) falls through to `GetFirstAliveActive(opposingTeam)` (`:2842-2843`, `:2926-2937`). That function walks slots from 0. Every single-target enemy move from all five personalities lands on the player's slot 0 until that Chainling faints. Support moves fall back to `FindAlly(actingTeam, actor) ?? actor` (`:2844-2845`), so heals go to the partner, not to the hurt ally the scorer had in mind.

**What the engine actually does (for comparison).** `DamageCalculator.CalculateDamageDetailed` (`:74-229`) computes:
`power × (Atk / Def) × levelFactor × roll(0.85–1.0) + 1`, then × STAB (off: `CombatResolverConfig_Default.asset` `enableStab: 0`), then × chemistry (`ChemistryResult.DamageMultiplier`: 1.2 / 1.0 / 0.8, `ChemistryResolver.cs:626-643`), then × crit, × consumed next-hit statuses, × Exploit Mark, × (1 − mitigation), with a mitigation floor. Hit chance is `(accStage × accSources / evaStage) × accuracy/100` (`:324`). A typeless damaging move takes the attacker's primary type (`:505-522`).

Worked gap (power 64, accuracy 95, attacker Atk 54, the Common median from `COMBAT_SYSTEMS_ANALYSIS.md`):

| Defender Def | AI estimate | Engine mean (×0.95 hit) | Error |
|---:|---:|---:|---|
| 84 (Cofi) | 32.8 | 64 × 0.643 × 0.925 + 1 = 39.1 → **37.2** | about 12% low |
| 40 (soft target) | 32.8 | 64 × 1.35 × 0.925 + 1 = 80.9 → **76.9** | 2.3× low, so most KOs on soft targets are missed |
| 84, resisted ×0.8 | 32.8 | **29.8** | high, so it "sees" KOs that fail |

### 1.3 Personality by personality

| Personality | How it picks a move | How it picks a target | Reads | Ignores |
|---|---|---|---|---|
| **Dumb** (`DumbAI.cs`) | The first legal damaging move in list order (`:33-43`). Otherwise the first legal non-passive (`:46-59`). Kit order is the whole policy. | None. Always slot 0. | Legality only. | Everything else. Never swaps. |
| **Random** (`RandomAI.cs`) | Uniform over legal actions, swaps included (`:40`). Passes if `rng == null`. | None. Slot 0. | Legality. | Everything. **It draws from the shared combat RNG**, so its picks shift later damage, hit, and crit rolls. |
| **Aggressive** (`AggressiveAI.cs`) | Damaging moves score `DamagePerAP × 10` (`:50`), plus 50 if `EstimateDamage ≥ currentHP` of the most vulnerable foe (`:53-58`). Non-damaging moves score 0.1 (`:64`). Swap scores 0.05 (`:69`). Ties go to the earlier list entry (`:72`). | Picks a "most vulnerable" foe only to score the KO bonus. The action is still untargeted (`:79`), so the bonus can reward a KO on slot 1 while the hit lands on slot 0. The header comment (`:9`, "Targets the most vulnerable") is not true. | Own attack stat with stages, move power, hits, accuracy, AP cost, foe HP. | Defense, chemistry, speed and priority, foe AP and threat, own HP, recharge, status and utility value, AoE count. Never heals or plays denial. |
| **Defensive** (`DefensiveAI.cs`) | Fixed ladder: (1) a believed KO on the lowest-HP foe, choosing the best `DamagePerAP` (`:43`, `:123-165`); (2) under 20% HP, the **first** legal swap (`:51-60`); (3) if any ally is under 40%, the **first** Support or `HEAL` move (`:64-84`, `:167-178`); (4) if all allies are above 55%, the **first** self-utility with a positive self stage (`:87-106`, `:180-199`); (5) the best `DamagePerAP` attack (`:201-224`). | Same untargeted problem. The "lethal" blow aimed at the weakest foe lands on slot 0 (`:131-137` vs `:152`). | HP fractions, the same estimate. | Bench matchup (it swaps into anything), heal size, the stage cap (keeps buffing at +6), chemistry, speed, denial, foe threat. No encounter uses it. |
| **Utility** (`UtilityStatusAI.cs`, modified in the working tree) | StatusUtility: +30 (`:75`), +15 per status payload (`:78`), +10 per enemy-targeted negative stage (`:81-85`), +10 if the least-debuffed foe has no statuses (`:88-90`). Support: +15, +10 per ally under 50% (`:96-104`), +12 with Uplink (`:106-107`). Damaging: `DamagePerAP × 2` (`:114`), +20 per status rider, +8 per negative stage, Kindling chip (`:126-127`). Everything pays −1 per AP (`:131-132`). Swap scores 1 (`:61`). | Untargeted. `FindLeastDebuffedEnemy` only adds the +10. | Status and stage payload counts, AP cost, ally HP, two passives. | **`utilityEffects`** (DrainAP, StealAP, GainAP, ChangeStage-by-payload, MitigationWindow), so Transaction Tax and AP Boost score like empty utilities; foe AP; recharge (Bean Sip keeps its +30); KO opportunities (it will debuff a 1 HP foe); stage caps; whether the status is already on the target; chemistry; speed. |

### 1.4 Known bugs and gaps

| # | Finding | Evidence | Effect |
|---|---|---|---|
| B1 | **Target-slot fixation** | `AIHelpers.cs:41`, `CombatState.cs:156-166`, `CombatResolver.cs:2842-2843` | Lead with a wall and the second active is never hit by single-target moves. Super-effective targets in slot 1 are ignored. |
| B2 | **Wrong damage model** | `AIHelpers.cs:129-142` vs `DamageCalculator.cs:104-134` | KO bonus and lethal checks misfire. Resists are invisible. Def stats are invisible. |
| B3 | **`DamagePerAP` returns 0 for 0-cost moves** | `AIHelpers.cs:147` | All-In (cost 0) is Aggressive's last choice among damaging moves (`AP_ECONOMY_ANALYSIS.md` Matchup D). |
| B4 | **Denial is never picked** | Aggressive `:64` (0.1), Utility ignores `utilityEffects`, `Moves.csv:71` (Tax is a `StealAP` payload only) | Debuff Script (49) beats MEV Extract (43). Tax scores like a blank utility. No AI reads foe AP. Separately, Kitsauto, the only Bot species, does not learn Thread Lock or MEV (`SpeciesMovePools.csv:7`). |
| B5 | **No affordability-after-denial check** | `CombatResolver.cs:1554-1562`, `:1648-1654`; `CombatApService.cs:23-56` | AP is checked when the action is chosen and again when it resolves. Nothing is reserved. A faster foe's Transaction Tax, AP Drain, or Glitch Feedback surcharge (`CombatActionEligibilityService.cs:102-110`) can make the AI's exact-AP move fail. That wastes its turn. |
| B6 | **Recharge is free** | Utility `:75`; `Move_bean_sip.asset` `rechargeTurns: 2` | Bean Sip always beats Bean Beam while legal (`COMBAT_SYSTEMS_ANALYSIS.md` §2). |
| B7 | **No speed or priority reasoning** | No AI reads `CoreStat.Speed` or the `PRIO`/`PRIOR` tags; the queue order is in `CombatResolver.cs:741-931` | The AI never uses Quick Jab or Rapid Strike to finish a faster threat. It never fears being outsped. |
| B8 | **Swap logic is blind** | Defensive `:51-60` picks the first legal swap; Aggressive and Utility nearly never swap | It swaps into bad matchups. Both actives can pick the single bench slot, and the second swap is rejected at `CombatResolver.cs:6445-6452`. |
| B9 | **No team coordination** | Each slot is chosen independently in every AI | Overkill (two hits into a foe one hit KOs), a duplicate swap, and two buffs when one is needed. |
| B10 | **The stage cap and existing statuses are ignored** | `DefensiveAI.cs:180-199`, Utility `:72-91` | Buffing at +6, re-applying a status the target already has. |
| B11 | **Shared RNG** | `CombatBattleController.cs:1458`, `:1465`; `CombatResolver.cs:273-274` | Any AI randomness changes combat rolls. Replays are safe because they record actions (`CombatReplayData.cs:76`), but "same fight, different AI" comparisons are not. |
| B12 | **Glitched Mind coupling** | `CombatResolver.cs:3461` | `GetLegalActions` cannot be changed without changing a status's behaviour. |
| B13 | **Dead Inspector field** | `CombatBattleController.cs:85` | `cpuAI` is never read. Confusing for designers. |

### 1.5 What the AI can see, and can it cheat?

`GenerateActions` receives the **entire `CombatState`** and the live `CombatRng`.

| Information | Available to the AI today? | Used by any AI today? | Fair? (my reading; see open questions) |
|---|---|---|---|
| Foe active HP, max HP, types, coating | Yes (`ChainlingInstance` fields) | HP only | Yes, the HUD shows it |
| Foe effective stats, stages, IVs | Yes (`GetEffectiveStat`, `stage*`) | No | Stages yes. Raw stats and IVs are borderline. |
| Foe **current AP** | Yes (`currentAP`) | No | Only if the player can see enemy AP (question Q2) |
| Foe statuses, including `Recharging` with `rechargingMoveId` | Yes | Counted only (Utility) | Mostly yes |
| Foe **full equipped kit**, including unrevealed moves | Yes (`moves`) | No | Not by default. A player cannot see an enemy's unused moves. |
| Foe **bench** Chainling and its kit | Yes (`team.chainlings`) | No | Not by default |
| Player's chosen action this turn | **No.** Not in state, not passed (`ICombatAI.cs:28`, `CombatBattleController.cs:1465`) | No | Must stay **no**. A cheat flag is allowed only if clearly labeled. |
| Future RNG rolls | **Derivable**: `CombatRng.Seed` and `CallCount` are public (`CombatRng.cs:17`, `:20`), so `new Random(seed)` advanced by `CallCount` predicts every hit, crit, and damage roll | No | Never |
| Last turn's events | Partly, through `state.turnActionLedger` (misses and hits last turn), and fully through the event log (not passed to the AI) | No | Yes. The player saw them too. |

**Conclusion:** no current AI cheats. The interface lets one read hidden kits, the bench, and future rolls, and nothing enforces the boundary. Section 2.6 proposes an explicit knowledge filter.

### 1.6 Facts about the engine the new AI must model (not change)

| Rule | Where | Why the AI cares |
|---|---|---|
| AP is +2 at turn start for living **active** Chainlings, capped at 10. Start AP is 0. | `StatusProcessor` turn regen; `CombatConstants.cs:10-11`; `AP_ECONOMY_ANALYSIS.md` §1 | AP planning |
| AP is per Chainling. A swap gives the incoming Chainling the outgoing slot's AP. | `CombatResolver.cs:6486-6494` | A swap does not lose AP |
| Turn order: swaps, then the priority block (`PRIO`/`PRIOR` tag or modifier 4.3-a), then normal moves, then capture. Each block is sorted by Speed, then stars, then rebirth, then a 50/50 RNG roll. | `CombatResolver.cs:802-863`, `:908-931`, `:6936-6948` | Speed and priority reasoning |
| Targets resolve **by slot at execution**. A dead or swapped target is retargeted. | `CombatResolver.cs:2828-2846` | A swap-in absorbs the hit aimed at its slot. Overkill matters. |
| AP is re-checked at execution. A failed spend wastes the turn. | `:1554-1562`, `:1648-1654` | Denial timing, both offence and defence |
| Thread Lock blocks every `Gain` while it is active, but it expires at the end of the turn it was applied, so the next regen happens. | `CombatApService.cs:65-78`; `AP_ECONOMY_ANALYSIS.md` §4 | Thread Lock value today is only "same-turn gains" |
| The drain floor is 0. Drain into an empty bar removes nothing. | `CombatApService.cs:101-115` | Denial against a spender is worthless |
| A recharge status is applied on use with `rechargeTurns` and ticks at end of turn. | `CombatResolver.cs:1731-1742`, `StatusProcessor.cs:248-266` | Recharge cost |
| **Recharge does not tick on the bench in this tree.** The design rule (`COMBAT_Resolution.txt` §1.2, E-29) says it should. | `CombatResolver.cs:448-474` walks active slots only; `PASSIVES_AND_STATUSES_ANALYSIS.md` §5 and P1 | The AI's recharge clock must match the engine (see Q7) |
| Stage multiplier is `(2+s)/2` above 0 and `2/(2+|s|)` below, clamped at ±6. | `ChainlingInstance.cs:375-382` | Setup and debuff value |
| Chemistry relation: dual types collapse to Advantage, Neutral, or Resisted, never stacking. | `ChemistryResolver.cs:83-106` | Type effectiveness |
| `ChemistryResolver.Resolve` and `DamageCalculator.CalculateDamageDetailed` **have side effects**: they apply reactions, consume Staggered, Surging, and Amplified, and draw RNG. | `ChemistryResolver.cs:108-128`; `DamageCalculator.cs:126`, `:154-159` | The AI must **not** call them on live state |
| Meme is outside the chemistry pass. | `COMBAT_TypeProfiles.md:5`, `:81` | The AI must treat Meme as Neutral |
| The catalog's highest AP cost is 3 (82 rows at 2, 52 at 1, 37 at 3, 6 at 0). | `Moves.csv` | Next-turn denial rarely matters until 4–6 AP costs exist (proposed in `AP_ECONOMY_ANALYSIS.md` §6.1) |

**Cofi Lootwak, as the working tree has it.** `bean_sip` (`Moves.csv:204`, `Move_bean_sip.asset`) costs 1 AP, is Self, `rechargeTurns: 2` (one skipped upcoming turn), heals 8% MaxHP, gives Speed +1, and has `PRIO|PRIOR|HEAL`. **`utilityEffects: []`, so the asset has no +1 AP gain.** `COMBAT_DESIGN_IDEAS.md:5` and `:268` describe "Bean Sip +1 AP" as decided on another branch. This tree does not have it (see Q6). `civet_beam` Bean Beam (`Moves.csv:205`) is power 12, AP 2, AllEnemies, Speed −1 on foes. **The 6 AP Bean Beam does not exist.** It is the open `DEC-BEAM` item, with options A, B, and C in `COMBAT_DESIGN_IDEAS.md` §4, none chosen.

---

## 2. Proposed design: one shared brain, three tiers

### 2.1 Shape

```
TieredCombatAI : ICombatAI                      (proposal)
  ├─ AiKnowledgeFilter   → AiView      (what this tier may see; never the player's chosen action)
  ├─ AiActionEnumerator  → candidates  (legal moves × valid targets, swaps, pass)
  ├─ CombatAiBrain
  │    ├─ estimators (pure, no side effects, no combat RNG)
  │    │     AiDamageEstimator · AiChemistryQuery · AiTurnOrderEstimator · AiApModel
  │    ├─ considerations (each returns a number, roughly 0..1 or in HP-equivalents)
  │    └─ lookahead (none / one opponent reply / short AP plan)
  ├─ AiTeamCoordinator   (two actives: focus fire, overkill, single bench)
  ├─ AiNoise             (seeded mistake model)
  └─ AiDecisionTrace     (forensic log: top candidates and why)
Config: AiTierConfigSO (per tier) × AiPersonalityProfileSO (flavor) [+ AiBossHooksSO]
```

One scoring formula serves every tier:

```
score(actor, action, target) = Σ_i  tierWeight_i × personalityMult_i × consideration_i(actor, action, target, view)
```

A tier sets which considerations are on (weight 0 means off) and the base weights. A personality only **multiplies** weights that the tier already has on. Flavor cannot add intelligence a tier does not have, and difficulty stays readable.

### 2.2 Considerations (the "factors")

Everything is measured in **expected HP swing** where possible, so the factors trade off on one scale. All math mirrors existing engine rules. Nothing new is invented.

| ID | Consideration | Computation (mirrors engine) |
|---|---|---|
| C1 | **Expected damage** vs this target | `E = hitChance × avgHits × (power × Atk/Def × levelFactor × 0.925 + 1) × chem × critEV × (1 − mitigation)`, ×2 for AllEnemies (summed per target). Uses the right Melee or Ranged pair, stages through `GetEffectiveStat`, the accuracy and evasion stage formula from `DamageCalculator.cs:324`, and chemistry from the matrix. Capped at target HP so overkill is not rewarded. |
| C2 | **KO chance** | `P(hit) × P(roll ≥ r*)`, where `r*` solves `damage(roll) ≥ currentHP` over a uniform 0.85–1.0 roll. Multi-hit is approximated by its average hits. Reward = `P(KO) × (value of removing that foe)`, where value = that foe's threat (C3). |
| C3 | **Threat of each player Chainling** | That foe's best expected damage into each of our actives this turn, given its AP (affordability), recharge, and its speed vs ours. Visible or known moves only (§2.6). Also "can it KO one of ours this turn" and "is it 1–2 AP from its biggest move". |
| C4 | **Speed and turn order** | `P(I act before X)` from Speed, stars, rebirth, and the priority tag, mirroring `CombatResolver.cs:802-931`. Ties count as 0.5. Swaps always go first. It scales C1 and C2 ("damage before it hits me"), and C6. |
| C5 | **AP plan and affordability** | Effective cost (`GetEffectiveApCost`: surcharge and discount). **Robust affordability:** if a foe that may act first has a visible drain, steal, or surcharge, then `P(still affordable at execution)` scales the action's value. Also next-turn AP: `min(10, AP − cost + 2)`. Also the opportunity cost of recharge (turns locked × the move's value). |
| C6 | **Survival risk** | `P(this actor is KO'd before it acts)` and `P(KO'd this turn)` from C3 and C4. A move by an actor that probably dies first is discounted. Defensive moves, walls, swaps, and heals score by risk removed. |
| C7 | **Swap value** | `(bench matchup − current matchup) over about 2 turns` − (the incoming Chainling eats this turn's hits aimed at the slot) − (the actor's lost action). Matchup = expected damage dealt minus taken, using C1 and C3. AP carries over. Blocked by Frozen Assets or a reserved bench. |
| C8 | **Setup value** (self buffs, ally buffs) | Stage delta through the stage multiplier × expected remaining attacking turns (fight-length estimate from HP and C1) × how much it changes C1 or C3. Zero at the ±6 cap. Timed stages count only for their duration. |
| C9 | **Debuff and status value** | Reduction of the target's threat (Atk −1 → ×2/3 on its damage), accuracy and speed effects, DoT expected HP. Zero if the status is already on the target, or immune. |
| C10 | **AP denial value** | (a) **This turn:** if `P(I act first)` and the foe's AP after denial is below the cost of its best affordable moves, value = that foe's threat (C3) × P(I act first). (b) **Next turn:** delay value on a foe holding for an expensive move (matters once 4–6 AP costs exist). (c) Zero against a spender at 0 (the floor). Uses the real payloads: `StealAP`, `DrainAP`, `NextMoveAPSurcharge`, MEV's end-step drain, and Thread Lock's same-turn gain block. |
| C11 | **AP boost value** | Own AP gain (Fuel Surge, AP Boost, AP Reserve) is worth something only if it unlocks a better move within the plan window. Otherwise it is close to 0. It pays recharge cost through C5. |
| C12 | **Heal and support value** | HP restored (capped at missing HP) × survival weight. Must name the ally target (fixes the `FindAlly` fallback). |
| C13 | **Typed preference tie-break** | Within ε of the best score, prefer a typed move over a typeless one. This matches the "typed moves beat typeless" rule and keeps choices readable. |

**Rules baked in:**
- Meme: `AiChemistryQuery` returns Neutral whenever the attack type or a defender type is Meme. A test also asserts that the matrix has no Meme cells.
- Bot is the denial type. C10 is the main tool of the Bot-flavored profile (§2.4).
- The recharge clock (C5) uses one switch, `RechargeTicksOnBench`. It must match the engine. The rule says "ticks on the bench", and the engine currently pauses it (§1.6, Q7).

### 2.3 The three tiers (proposal)

| | **Rookie** (T1) | **Tactician** (T2) | **Mastermind** (T3) |
|---|---|---|---|
| Intent | Plays like a new player: hits hard and hits the right target | Plays like a solid player: reads speed, threat, and matchups | Plays like an expert: plans AP, denies on time, coordinates |
| Considerations on | C1, C2, C12 (simple: heal only under 35%), C13 | All of T1 + C3, C4, C6, C7, C8, C9, C10a, C5 (affordability and recharge only) | All: C1–C13, including C10b and the full C5 AP plan |
| Target choice | Best C1 + C2 over **all** valid targets (fixes B1 and B2) | Plus threat-weighted (remove what hurts us most) | Plus team-coordinated focus fire and overkill avoidance |
| Lookahead | None (this turn only) | **One opponent reply**: for each top-5 candidate, assume each foe plays its best visible reply on a cheap projected state, then re-score our side | **Short AP plan**: 2–3 turns for each own actor (sequences of move, swap, or pass), with the foe modelled as its best visible reply each turn. Includes "bank AP for X", "deny next turn", and recharge timing. |
| Team coordination | None, except no double swap | Overkill check (if slot A's pick KOs X with P ≥ 0.8, slot B re-scores without X) | Joint search over both actives' top-k (k = 4, so 16 pairs) |
| Noise | Softmax over the top 3 with temperature τ = 0.35, plus a 15% "blunder" (uniform among non-pass legal moves) | τ = 0.15, 5% blunder, never a blunder that loses to a visible KO | τ = 0.05, 1% blunder (0% for bosses by default) |
| Knowledge (§2.6) | Visible board only, no foe AP | Visible board + foe AP (if the HUD shows it) + revealed moves | Same as Tactician + species move pools (public data) as "possible moves", weighted by likelihood. **No chosen-action read, no RNG peek.** |
| Swap | Only forced (KO replacement is already automatic) | Yes, through C7, with an anti-ping-pong rule: no re-swap for 2 turns unless a KO is threatened | Yes, and plans swap-in timing |
| Denial | Never deliberately | This-turn denial when it acts first | This-turn and next-turn denial, and refuses denial into the floor |
| Priority and counters | No | Uses priority moves when a faster foe threatens a KO; values walls and Fox Feint when the risk is high | Same, plus baits (e.g., holds Fox Feint's one-time window for the foe's biggest hit) |

The tiers differ in four readable ways: **factors weighed, lookahead depth, mistake rate, and knowledge.** This matches "each understanding combat better than the last."

### 2.4 Personalities become weight profiles

The `AIPersonality` enum keeps its meaning. Each value maps to an `AiPersonalityProfileSO` that multiplies tier weights. Dumb and Random stay as they are, for the tutorial and for fuzz testing.

| Consideration | Aggressive | Defensive | Utility | Bot-denial (proposed new profile) |
|---|---:|---:|---:|---:|
| C1 Expected damage | 1.4 | 0.8 | 0.7 | 0.8 |
| C2 KO chance | 1.5 | 1.2 | 1.0 | 1.0 |
| C3/C6 Threat and survival | 0.6 | 1.5 | 1.0 | 1.0 |
| C7 Swap | 0.3 | 1.4 | 0.8 | 0.8 |
| C8 Setup | 0.6 | 1.0 | 1.2 | 0.8 |
| C9 Debuff and status | 0.5 | 0.8 | 1.5 | 1.0 |
| C10 Denial | 0.3 | 0.8 | 1.2 | **1.8** |
| C11 Boost | 1.0 | 0.8 | 1.2 | 1.0 |
| C12 Heal and support | 0.4 | 1.5 | 1.1 | 0.8 |

Examples: **Rookie + Aggressive** is a hard-hitting brute with no plan. **Tactician + Defensive** is a wall that swaps out of bad matchups and heals the right ally. **Mastermind + Utility** is a controller that debuffs the real threat and denies on time. Because multipliers only scale enabled weights, Rookie + Utility still cannot do denial: C10 is off at T1.

### 2.5 Bosses

A boss is a tier, a profile, and optional `AiBossHooksSO` data. The hooks are small and readable, not a hidden script:

| Hook (proposed) | Meaning |
|---|---|
| `minTier` | Bosses default to Mastermind with 0% blunder |
| `signatureMoveId` + `signatureBonus` | A flat bonus when the move is legal and its C1 or C2 is at least X% of the best alternative (a readable "boss tell") |
| `apGateMoveId` + `apGate` | "Only consider this move at ≥ N AP" (the gate `COMBAT_DESIGN_IDEAS.md` §4 already sketches) |
| `openingSequence` | Optional fixed turn-1 action (presentation beat), then normal brain |

**Cofi Lootwak under this design:**
- **Bean Sip** scores as C12 (8% of 168 ≈ 13 HP, capped at missing HP) + C8 (Speed +1, worth something only if it changes turn order against a foe) + C11 (**+1 AP only if the data gains it**, see Q6) − C5 recharge cost (one locked turn). At full HP and already faster, Sip is worth close to 0, so Cofi beams. **This fixes B6.**
- **Bean Beam** (power 12, AllEnemies) scores C1 across both targets + C9 (Speed −1 on each foe, valuable when Cofi, at Speed 16, can flip turn order). It wins whenever Sip has little to heal.
- **Proposed 6 AP beam** (`DEC-BEAM`, not in data): with `apGateMoveId = <chosen option>` and `apGate = 6`, Mastermind's AP plan (C5) values *banking*. It compares "Sip and Beam now" with "save until 6, then fire". The gate makes sure it never fires early. Because Sip is priority and AP-positive (if +1 AP lands), the planner naturally alternates Sip (recharge) → Beam → bank. That is the intended rhythm, and it comes from scores, not a script.

### 2.6 Knowledge model (proposal)

`AiKnowledgeFilter` builds an `AiView` from `CombatState`. All brain code reads `AiView`, never `CombatState` directly. This makes the boundary testable.

| Field | Rookie | Tactician | Mastermind | Cheat flag (off by default, labeled **CHEAT**) |
|---|---|---|---|---|
| Foe HP, types, stages, statuses | ✔ | ✔ | ✔ | — |
| Foe effective stats | ✔ (derived from species base + stages, as the player could infer) | ✔ | ✔ | — |
| Foe current AP | ✖ | ✔ if the HUD shows enemy AP (Q2) | ✔ if the HUD shows it | `cheatSeeFoeAp` |
| Foe moves | ✖ (assumes a generic typed 64-power hit for threat) | Revealed moves (seen this battle) | Revealed + species pool as weighted "possible" | `cheatSeeFullKit` |
| Foe bench | ✖ | Revealed only | Revealed + "a bench exists" | `cheatSeeBench` |
| Player's chosen action this turn | ✖ never | ✖ never | ✖ never | `cheatReadChosenAction`: **not wired in this proposal.** If Cronus wants it for a "cheating boss", it must be an explicit, logged, labeled option. |
| Future RNG | ✖ never | ✖ never | ✖ never | No flag. Never. |

"Revealed moves" come from an `AiMemory` the AI updates after each turn from `CombatEventLog` `MoveUsed` events (the player saw them too). No new engine state is needed.

---

## 3. Implementation plan (proposal)

### 3.1 Phases

| Phase | Goal | Files to touch / add | Done when |
|---|---|---|---|
| **P0 Quick fixes in the old AIs** (optional, small, ships alone) | Kill the worst exploits before the new brain lands | `AIHelpers.cs`: add `GetLegalActionsTargeted(...)` (**new method**, keep `GetLegalActions` unchanged for Glitched Mind, B12); `DamagePerAP` scores raw damage at cost 0; `EstimateDamage(attacker, defender, move)` overload with Def and chemistry. `AggressiveAI` / `DefensiveAI` / `UtilityStatusAI`: write the target they already pick (`CombatAction.UseMove(team, move, targetTeam, targetSlot)`). | Slot 1 gets hit. All-In is chosen. The P0 tests pass. |
| **P1 Pure estimators** | Engine-faithful, side-effect-free math | New: `Runtime/AI/Brain/AiDamageEstimator.cs`, `AiChemistryQuery.cs` (reads `TypeChemistryMatrixSO.TryGetReaction` only, mirroring `ChemistryResolver.cs:83-106`), `AiTurnOrderEstimator.cs` (mirrors `CombatResolver.cs:802-931`, no RNG), `AiApModel.cs`. Preferred refactor: pull a pure `DamageFormula.Expected(...)` out of `DamageCalculator` so the engine and the AI share one formula. This is a code change to review carefully, because `DamageCalculator` currently consumes statuses and draws RNG. | Parity tests: estimator mean within 3% of `DamageCalculator` averaged over 500 seeded rolls, on 10 fixtures |
| **P2 Brain + Rookie** | The new AI end-to-end at T1 | New: `TieredCombatAI.cs` (`ICombatAI`), `CombatAiBrain.cs`, `AiConsiderations.cs`, `AiActionEnumerator.cs`, `AiKnowledgeFilter.cs` + `AiView.cs`, `AiNoise.cs`, `AiDecisionTrace.cs`. Config: `AiTierConfigSO.cs` (weights, τ, blunder, lookahead depth, caps), `AiPersonalityProfileSO.cs`. Assets: `Assets/Data/Combat/AI/Tier_Rookie.asset`, `Tier_Tactician.asset`, `Tier_Mastermind.asset`, `Profile_Aggressive.asset`, etc. Forensics: a new `CombatForensicEventType.AiDecision` + `AiDecisionPayload` in `CombatForensicLogModels.cs` (top 3 candidates, score breakdown, seed). | Rookie beats Dumb in more than 80% of harness sims. All T1 tests pass. |
| **P3 Tactician** | Threat, speed, swaps, denial, one-reply lookahead | `AiConsiderations` C3–C10a; `AiProjectedState.cs` (a struct copy of actives only: HP, AP, stages, key statuses; **no `CombatState` clone**); `AiTeamCoordinator.cs` | T2 beats T1 in more than 65% of mirror sims. Swap, priority, and denial tests pass. |
| **P4 Mastermind** | AP plan, next-turn denial, joint search, opponent model | `AiPlanner.cs` (depth 2–3, own actor), `AiMemory.cs` (revealed moves from the event log), C10b, C11, full C5 | T3 beats T2 in more than 60% of sims, inside the perf budget |
| **P5 Wiring and data** | Choose tier per encounter | `BattleSnapshots.cs` (`EncounterSnapshot.aiTier`, new field, default derived from `difficulty`); `ManualEncounterProviderSO.cs`, `ZoneEncounterProvider.cs`, `AuthoritativeBattleEncounterProvider.cs` (new `aiTier` fields); new `Runtime/AI/CombatAiFactory.cs`, replacing the two `CreateAI` copies (`CombatBattleController.cs:2693`, `Combat2TestHarness.cs:256`); move the `AIPersonality` enum out of the harness file. **Append-only enum values.** Assets store ints (`aiPersonality: 3`). Remove or repurpose the dead `cpuAI` field (B13). | Cofi and Intro encounters load with an explicit tier and profile |
| **P6 Bosses and tuning** | Cofi feels like a boss | `AiBossHooksSO.cs`, `Boss_CofiLootwak.asset`; tune with `Combat2TestHarness` fast sim | Cofi no longer sip-loops. Win-rate targets met (Q12). |

### 3.2 Deterministic, seeded randomness

- **The AI never draws from the combat `CombatRng`.** Each decision gets its own stream: `new CombatRng(Mix(state.seed, teamIndex, state.currentTurn, slotIndex, salt:"AI"))`, using a fixed integer hash (e.g., SplitMix-style), not `string.GetHashCode`, which is not stable across runtimes. Combat rolls then match between "AI A" and "AI B" in A/B sims (fixes B11). `RandomAI` keeps the shared stream unless Cronus agrees to move it (Q9).
- Stable ordering: candidates are sorted by `(score rounded to 1e-4, actionType, moveIndex, targetSlot, swapIndex)`. No `Dictionary` or `HashSet` iteration order in decisions. No `Time`, no `UnityEngine.Random`.
- Floating point: all estimator math is in `float` in a fixed order. Scores are rounded before comparison so platform differences cannot flip a choice.
- Forensics: `AiDecisionPayload` records the seed inputs, the top 3 candidates with their factor breakdowns, the noise roll, and the final pick. Replays already record actions (`CombatReplayData.TurnActionRecord`), so a replay does not need to re-run the AI. The trace explains *why*.
- If PvE ever becomes server-authoritative, the brain must be pure C# with no `UnityEngine` dependencies outside the SO loaders, so the same code can run headless.

### 3.3 Performance limits

| Item | Budget |
|---|---|
| Candidates per actor | ≤ 5 moves × ≤ 2 targets + ≤ 1 swap + pass ≈ 12 |
| Rookie | ≤ 30 evaluations per turn, < 0.1 ms |
| Tactician (top-5 × each foe's best reply) | ≤ 300 evaluations per turn, < 0.5 ms |
| Mastermind (joint top-4 pairs + depth-3 plan per actor, about 6³ = 216 leaves) | **Hard cap 2,000 evaluations per turn**, < 2 ms on min-spec. When the cap is hit, the best-so-far is returned (anytime search). |
| Memory | No `CombatState` cloning, no LINQ in hot loops, pooled lists, structs for projections |
| Logging | Only the final `AiDecisionPayload`, behind `CombatLogSettings.EnableForensicLogging` |

### 3.4 EditMode tests (proposal)

Location: `Assets/Scripts/Combat/Tests/AI/` in the existing `Chainbound.Combat.Tests` assembly, NUnit, in the same style as `CombatResolverSevenBucketsTests` (in-memory `ChainlingInstance`, `TeamState`, `MoveInstance` fixtures). Species and stat numbers below are test fixtures.

| # | Test | Setup | Expect |
|---|---|---|---|
| T1 | `PicksSuperEffectiveTarget` | AI: Bot attacker with a Bot power-64 move. Foes: slot 0 Frozen (resists Bot, `COMBAT_TypeProfiles.md` Bot row), slot 1 Node (weak to Bot). Equal HP and Def. | Target = slot 1 (all tiers). The old AI would hit slot 0. |
| T2 | `ReadsDefenseNotJustType` | Both neutral. Slot 0 Def 120, slot 1 Def 40. | Target = slot 1 |
| T3 | `FinishesLowHpFoe` | Slot 1 at 12 HP, slot 0 full. AI has a 2 AP power-64 hit and a 1 AP power-16 jab, both lethal on slot 1. | Picks a KO on slot 1, and the cheaper lethal move when both have P(KO) ≥ 0.95 |
| T4 | `UsesPriorityAgainstFasterKoThreat` | AI at 15 HP, Speed 40. Foe Speed 90, low HP, with a revealed move that KOs the AI. AI has Quick Jab (`PRIO`, lethal on the foe) and a stronger normal hit. | Tactician and Mastermind pick Quick Jab. **Control:** with foe Speed 20, they pick the stronger hit. Rookie: no requirement. |
| T5 | `DeniesApWhenFoeShortThisTurn` | AI faster, with Transaction Tax. Foe at exactly 3 AP with a revealed 3 AP move (e.g., Automation Burst) and no cheaper strong move. | Tactician and Mastermind pick Tax on that foe (the foe's move will fail at execution). **Control:** foe at 0 AP, so no Tax (floor). |
| T6 | `DeniesApWhenFoeOneToTwoShortOfBigMove` | Foe at 4 AP holding for a **6 AP fixture move** (test-only `MoveInstance`; 6 AP costs are a proposal). AI has Tax and a weak chip hit. | Mastermind picks Tax (the foe slips a turn). Tactician: no requirement (next-turn denial is T3 only). |
| T7 | `DoesNotWasteTurnOnUnaffordableMove` | (a) AI at 1 AP: never selects a 2 AP move. (b) AI at exactly 2 AP. A faster foe has revealed Transaction Tax. AI options: a 2 AP hit (value V) and a 1 AP hit (value 0.7V). (c) `nextMoveApSurcharge = 1` is counted. (d) A recharging move is never selected. | (a), (c), (d) all tiers. (b): Tactician and Mastermind pick the 1 AP hit when `P(still affordable) × V < 0.7V`. |
| T8 | `SwapsOutOfBadMatchup` | AI active resisted by and threatened with a KO by the foe. The bench Chainling resists the foe and has a super-effective move. | Tactician and Mastermind swap. Rookie does not. **Controls:** no swap when the bench is reserved or the actor has Frozen Assets; no swap back on the next turn (anti-ping-pong). |
| T9 | `TwoActivesDoNotDoubleSwap` | Both AI actives would like to swap, one bench slot. | At most one swap action |
| T10 | `AvoidsOverkill` | One foe at 10 HP, one full. Both AI actives can KO the low one. | One takes the KO, the other hits the full foe (Tactician and up) |
| T11 | `RespectsStageCap` | AI at MeleeAtk +6 with Power Surge | Never picks Power Surge |
| T12 | `MemeIsNeutral` | Meme attacker into any type, and any type into Meme | Chemistry multiplier 1.0 in the estimator. Matrix has no Meme cell. |
| T13 | `TypedBeatsTypelessOnTie` | Typed and typeless moves with equal expected value within ε | Picks the typed move |
| T14 | `CofiBeamsWhenSipIsLowValue` | Cofi full HP, Sip legal | Picks Bean Beam. **Variants:** Cofi at 30% HP picks Sip. Sip recharging picks Beam. A foe in Beam KO range picks Beam. |
| T15 | `RechargeClockMatchesEngine` | Bench a Chainling with Recharging 2, project 1 turn | The AI's projected recharge equals the engine's after a real `ExecuteTurnWithAI` |
| T16 | `DeterministicForSameSeed` | Same state and seed, 100 runs | Identical actions and identical `AiDecisionPayload` |
| T17 | `DoesNotTouchCombatRng` | Record `rng.CallCount`, call `GenerateActions` | `CallCount` unchanged |
| T18 | `KnowledgeFilterHidesUnrevealed` | The foe has an unrevealed lethal move | Tactician's threat ignores it. Mastermind weights it only through the species pool. `cheatSeeFullKit` changes the result (the proof that the flag is the only path). |
| T19 | `NoiseRateWithinTolerance` | 1,000 seeded Rookie decisions on a fixture with a clear best move | Best-move rate 70–90% (τ and blunder as configured) |
| T20 | `EstimatorParity` | 10 fixtures × 500 seeded `DamageCalculator` rolls | Mean within 3% |
| T21 | `GlitchedMindUnchanged` | `BuildRandomLegalMisAct` option count before and after the AI changes | Identical (guards B12) |
| T22 | `PerfBudget` [Category("Perf")] | 1,000 Mastermind decisions on a 2v2 fixture | Under the budget in §3.3 (soft in CI) |

---

## 4. Open questions for Cronus

| # | Question | Why it matters |
|---|---|---|
| Q1 | **How is the tier chosen?** A new per-encounter `aiTier` field, or derived from `EncounterDifficulty` (Standard → Rookie, Elite → Tactician, Boss → Mastermind), with an override? What should wild zones use? They are all Aggressive today (`ZoneEncounterProvider.cs:15`). | Data wiring (P5) |
| Q2 | **What can the player see of the enemy?** Enemy AP? Enemy moves before they are used? The enemy bench? The AI's fair knowledge should mirror this. | §2.6 knowledge filter |
| Q3 | Is a "cheating" boss ever acceptable (reading your chosen action or full kit)? If yes, should it be disclosed in-game? | Cheat flags |
| Q4 | Should difficulty feel like **mistakes** (noise) or **blind spots** (factors off), or both as proposed? | Rookie feel |
| Q5 | Personality per **encounter** (today), per **species** (the enum comment's plan), or per **Chainling** in a mixed team? | Brain runs per team or per actor |
| Q6 | **Bean Sip +1 AP:** this tree's data has none (`utilityEffects: []`), while the design notes say it was decided. Which is canonical? And which `DEC-BEAM` option (A, B, or C) becomes the 6 AP beam? | Boss scoring (§2.5) |
| Q7 | **Bench recharge:** the rule says it ticks. The engine pauses it (P1 in the passives note is not implemented). Should the AI model the engine until P1 lands, or should P1 land first? | Planner correctness |
| Q8 | **Thread Lock** today does not block the next regen. Is the §6.2 fix coming? Denial value depends on it. | C10 |
| Q9 | Move `RandomAI` to the separate AI RNG stream too? (Old seeds would play differently. Recorded replays are unaffected because they store actions.) | Determinism |
| Q10 | Is the in-turn "steal so their move fizzles" play (AP checked at execution) an intended skill play, or a feel-bad to avoid giving the AI? | C10a, T5 |
| Q11 | Should Kitsauto (the only Bot species) learn the denial kit (`AP_ECONOMY_ANALYSIS.md` §6.2)? Without it, the Bot-denial profile has nothing to press on Bot enemies. | Bot identity |
| Q12 | Target win rates per tier for a fair player team (e.g., Rookie 25%, Tactician 45%, Mastermind 60%, boss 70% on the first try)? | Tuning (P6) |
| Q13 | Should player-side autoplay (`playerAI`) also get tiers, for an auto-battle or "hint" feature? | Factory scope |
| Q14 | How often may the AI swap? Is a swap meant to be free (no AP), as now? `COMBAT_DESIGN_IDEAS.md` floats a toll as a proposal. | C7, anti-ping-pong |
| Q15 | Should a boss show readable tells (the signature-move hook), or play purely by score? | §2.5 hooks |

---

## 5. Out of scope and not proposed here

- No change to the damage formula, chemistry, costs, recharge rules, or any move row.
- No new action types (e.g., "hold", "guard"). Defensive play uses existing moves, walls, swaps, and Fox Feint.
- No server-side AI. If PvE becomes authoritative, see §3.2.
