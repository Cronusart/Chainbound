# AI decision quality

Snapshot: local combat export `chainbound_combat_snapshot_20261018_1605` (18 Oct 2026, 4:05 PM CT). Every code and data fact below comes from that snapshot. GitHub `main` was not used.

This round measures how the five existing personalities choose, given today's kits. `Docs/Combat/Design/ENEMY_AI_TIERS_PROPOSAL.md` already assigned which tier belongs on which encounter and sketched one shared brain. That assignment still holds. What changed in this tree, and what the choices actually do, is the subject here.

Two facts in the earlier audit are stale:

- Bean Sip now has `GainAP:1` (`Moves.csv` `bean_sip`). The earlier note that the asset had an empty `utilityEffects` list is out of date.
- Grind Tax exists. It is the 6 AP boss-only move (`grind_tax`, power 30, three hits, all foes, recharge 2, `BOSS_ONLY`). `CofiLootwak_Encounter.asset` equips `bean_sip`, `civet_beam`, and `grind_tax`, with `aiPersonality: 3` (Utility) and `difficulty: 2` (Boss).
- Recharge now ticks on the bench. `CombatResolver.TickBenchedRecharge` calls `StatusProcessor.TickRechargeWhileBenched`. The earlier audit said the bench clock was paused.

`EncounterDifficulty` is still unread by every AI. No checked-in encounter uses Defensive. Wild and authoritative battles still default to Aggressive. One AI instance still drives every active on the team.

## How a choice is made

`ICombatAI.GenerateActions` returns one `CombatAction` per active slot. `AIHelpers.GetLegalActions` builds that menu:

- A move is included when `CombatActionEligibilityService.EvaluateMoveSelection` says it can be selected: alive, not passive, not already fired if `triggerOncePerBattle`, enough AP via `GetEffectiveApCost`, not recharging, and a `Gate` condition if one is set.
- Each move is `CombatAction.UseMove(team, moveIndex)`. `targetTeamIndex` and `targetSlotIndex` stay at -1.
- Swaps come from `GetAvailableSwapTargets`. Pass is added only when the list is otherwise empty.

`CombatResolver.ResolveSingleSelectedTarget` sends an untargeted single-enemy or single-ally move to `GetFirstAliveActive` (slot 0) or `FindAlly`. All-enemy moves hit every living foe. The personalities that "pick a target" throw the result away. They never write it onto the action.

Damage scoring uses `AIHelpers.EstimateDamage`: `Power × (Atk / 100) × average hits × accuracy`. Melee uses Melee Attack, ranged uses Ranged Attack, after stages. Defense, type, the +1 constant, crits, mitigation, barriers, and extra targets are outside the formula. `DamagePerAP` divides that by the authored `apCost` and returns 0 when `apCost` is 0. `CombatStateEvaluator.Evaluate` is never called. `ThreatScore` is never called. `PickTarget` only compares the first two actives, which matches the 2-active format, so that cap is idle.

## Per-AI walkthrough

### Dumb (`DumbAI.GenerateActions`)

The first legal damaging move in kit order. If none, the first legal non-passive. No target, no swap, no score. Kit order is the whole policy. On the Cofi boss kit the first damaging move is Bean Beam, so Dumb beams whenever Beam is affordable and never selects Grind Tax.

### Random (`RandomAI.GenerateActions`)

Uniform over the legal list, swaps included. It draws with the combat `CombatRng`, so a random pick advances the same stream as hit, crit, and damage rolls. Pass happens when `rng` is null. This round did not re-measure that stream coupling. The blunder rate below is the exact share of legal actions that fail the pareto test, not a sampled draw.

### Aggressive (`AggressiveAI.GenerateActions`)

For each legal action, strict `>` keeps the earliest tie:

- Damaging: `DamagePerAP × 10`, plus 50 if `EstimateDamage` is at least the current HP of the most-vulnerable foe (`PickTarget`, which is `1 − HP%` on the first two actives).
- Anything else: 0.1.
- Swap: 0.05.

The +50 uses a foe the action does not target. A KO on slot 1 can inflate a hit that lands on slot 0. Non-damaging moves, including every Bot denial move, sit at 0.1. On the boss kit at 6 AP, Grind Tax's damage-per-AP (estimate 20.52 / 6 = 3.42) beats Bean Beam (2.736 / 2 = 1.37), so Aggressive would fire the tax if it ever held 6 AP. From a standing start it never does: Beam costs the whole +2 regen, the score for Bean Sip is 0.1, and the AP bar returns to 2 every turn.

### Defensive (`DefensiveAI.GenerateActions`)

A fixed ladder. The first hit wins inside each step.

1. `FindLethalBlow`. Lowest current HP among living foes. Any damaging move whose estimate meets that HP qualifies. The winner is the best `DamagePerAP` among those. The action is still untargeted, so the blow lands on slot 0.
2. Below 20% HP, the first legal swap. The bench is not compared.
3. If any active ally (self included) is under 40% HP, `FindBestSupport` returns the first `Support` move or the first move tagged `HEAL`. Missing HP, heal size, and `HealPercentMaxHP` are not read. Hibernate heals 25% and is `StatusUtility` without the `HEAL` tag, so the ladder skips it.
4. If every active is at or above 55% HP, `FindBestBuff` returns the first self `StatusUtility` whose `stageChanges` include a positive self stage. Cipher Lock, the +6 cap, ally buffs, and `utilityEffects` stage changes (Accuracy Focus, Evasion Maneuver) are invisible. Bean Sip qualifies, so a healthy Cofi sips.
5. `FindBestAttack`: best `DamagePerAP` among damaging moves.

On the boss kit this produces the same undisturbed line as Utility, for a different reason. Sip is the step-4 buff whenever it is legal. On the turns it is recharging, Grind Tax has the better damage-per-AP and step 5 selects it. At 6 AP with Sip also legal, Defensive sips and leaves the tax unfired. The encounter does not use this personality. The spot check is there so the ladder is visible.

Both low-HP actives call the ladder independently. On a Gilarime at 15% HP with one bench slot, both slots return swap to bench index 2. The resolver rejects the second swap.

### Utility (`UtilityStatusAI.ScoreAction`)

Highest score, earliest tie. This is the boss personality.

- Swap scores 1. Pass scores 0.
- `StatusUtility`: +30, plus 15 per entry in `statusEffects`, plus 10 per point of enemy-facing negative stage, plus 10 when the least-debuffed foe has an empty status list. `FindLeastDebuffedEnemy` does not change the target.
- `Support`: +15, plus 10 per active ally under 50% HP, plus 12 with an equipped Uplink passive when the move targets an ally or is Extend Protocol / Network Sync.
- Damaging: `EstimateDamage / effectiveCost × 2` when cost is positive, plus 20 per status entry, plus 8 per point of enemy-facing negative stage. An equipped Kindling passive adds `max(1, 6% of the first unkilled foe's max HP)` to every damaging move. That bonus does not change which damaging move wins.
- Then subtract the effective cost. `ResolveScoringCost` uses current AP for `SpendRemainingAp` and `GetEffectiveApCost` otherwise. Aggressive and Defensive still divide by the authored cost.
- A boss-only move the actor can afford adds 60.

`utilityEffects` are not scored, except the two passive checks above. Transaction Tax (`StealAP`), Packet Skim (`DrainAP`), Memory Leak and Fox Feint (`ArmUtilityWindow`), and Fuel Surge's real payload shape are invisible. Fuel Surge still scores well because it is a utility with a self status and, in this harness, its certain +3 AP is only seen by the oracle, not by `ScoreAction`. The +30 is enough. Cipher Lock, existing statuses, the stage cap, recharge length, type, defense, and KO are unread.

Bean Sip at any board where the lead foe is clean: `30 + 10 − 1 = 39`. The Speed +1 is a self stage, so it adds nothing. The +1 AP adds nothing. The score is 39 at Speed +0 and at Speed +6. Bean Beam, Cofi Ranged Attack 24: estimate `12 × 0.24 × 0.95 = 2.736`, score `2.736 / 2 × 2 + 8 − 2 = 8.736`. Grind Tax when affordable: estimate `30 × 0.24 × 3 × 0.95 = 20.52`, score `20.52 / 6 × 2 + 8 − 6 + 60 = 68.84`. Without the +60 the same line is 8.84, and Sip's 39 wins.

Kitsauto's test kit (Blade Flurry, Multi-Thread Shot, Debuff Script, MEV Extract) at 4 AP against a Node foe: Debuff Script 59, MEV Extract 53, Blade Flurry 24.0, Multi-Thread Shot 15.1. Utility opens with the stat drop. The wild pool kit is Blade Flurry, Fox Feint, Multi-Thread Shot, Packet Skim. Fox Feint and Packet Skim both score 39. The earliest tie is Fox Feint, the once-per-battle dodge (`rechargeTurns` 999). Packet Skim's drain is unscored, so the dodge and the denial are the same number.

Deghoul's pool kit: Fuel Surge 55, Transaction Tax 39, Gas Bump 22.6. Aggressive on that kit scores Gas Bump 123.1 and both utilities 0.1.

Validape's test kit, one Frozen foe, def 55, no partner buff: the oracle (defined below) gives Hammer Sync 108.3 HP and Coordinated Shot 58.9. With one ally carrying a buff status the same hits are 129.2 and 67.5, from the CSV scaling `+0.20` cap `1.20` and `+0.15` cap `1.15`. Aggressive, Defensive, Utility, and Dumb all score those two moves the same whether the partner is buffed or not, and all four select Hammer Sync. The synergy changes the hit and none of the brains.

## Measured blunder rates

Harness: `ai_decision_check.py`. It reimplements the five scorers from the methods above and runs them on kits built from `SpeciesMovePools.csv` and `TestLoadouts.csv`.

Kit rule: initial moves, then the flagship, then learnable, then exclusive, cut at four, passives included when they fall in that prefix. The three test loadouts are extra boards. Cofi is also scored on the encounter kit at 2, 4, 6, and 7 AP. That is 222 boards and 888 personality decisions.

Oracle used only for the "clearly dominated" label, not for the AI's choice. One mean roll (0.925) is floored inside `Power × (Atk/Def) × roll × chemistry + 1`, then multiplied by average hits and accuracy, capped at the target's HP. Chemistry is the matrix collapse at ×1.2 / ×0.8. Typeless moves inherit the attacker's primary type, matching `DamageCalculator.ResolveAttackType`. STAB is off, matching `enableStab: 0`. Full-absorb barriers zero that target. Hammer Sync and Coordinated Shot gain their CSV per-partner bonus when an ally has a buff status. Attack-stat overrides use the named stat. This is a mean-roll floor, not the expectation over the roll distribution, and it ignores crits, mitigation, and partial walls.

A move is clearly dominated when another legal move costs the same or less, recharges for the same or fewer skipped turns, matches its heal, its certain self AP gain, and its real buff, and is strictly better on HP removed (by at least 8, or 20%), on cost at equal HP, or by reliably KO'ing a foe this move leaves alive. A debuff, a new status, or a denial does not protect a move once the alternative reliably KOs that foe. Swaps are dominated only when some move reliably KOs. These rates are counts from the harness.

| Personality | Pareto-dominated picks | Share |
|---|---:|---:|
| Dumb | 47 / 222 | 21.2% |
| Aggressive | 31 / 222 | 14.0% |
| Defensive | 28 / 222 | 12.6% |
| Utility | 27 / 222 | 12.2% |
| Random (exact share of the legal list) | — | 31.1% |

One board had a single legal action. Restricting to the other 221 boards moves each rate by less than half a point.

Named situations, same 222 boards:

- **Slot fixation.** On the 18 split boards (full, resisting front; 28 HP, weak back), the chosen single-target move would remove strictly more HP from slot 1, or KO slot 1 and miss slot 0, and the action still hits slot 0. Dumb 16/18, Aggressive 15/18, Defensive 14/18, Utility 6/18. Utility's lower count is the self-buff and status clicks. Those clicks also fail to hit slot 1.
- **Wall in front.** Slot 0 holds a full absorb, slot 1 is open. Pareto-dominated clicks: Aggressive 9/18, Dumb 8/18, Defensive 8/18, Utility 3/18. The stored Aggressive example is Balloom's Gas Bump into the wall while Fuel Surge is legal at cost 0. The oracle treats the absorbed hit as 0 HP. A barrier-absorbed hit still applies on-hit riders (`CombatResolver` runs statuses, stages, and `AfterEachConfirmedHit` on a full block). The AI scores the hit as if the HP landed.
- **KO race.** Foe HP is set to the oracle of the cheapest damaging move, AP 8. Aggressive's pick is dominated on 5/17 of those boards. The stored case is Balloom selecting Recoil Strike (4 AP) over Gas Bump when both remove the foe. The +50 lethal bonus did not misfire on the 55-def boards, because the estimate sits near 57% of the oracle there (next paragraph) and the foe HP was the oracle number. It does misfire into a wall of defense: Validape into 160 Def and 40 HP estimates Hammer Sync at 53.2 and treats it as a KO; the oracle is 31.4. Balloom's Recoil Strike estimates 46.5 against an oracle of 21.9 on that same body.
- **Estimate versus oracle, neutral board, the move Aggressive actually picks.** 17 damaging picks, foe defense 55. Mean ratio of estimate to oracle is 0.568. All 17 are under by at least 25%. Blockwyrm's Bulwark Blow is the wide miss (estimate 26.1, oracle 65.7): the move strikes with Melee Defense (86) and the helper uses Melee Attack (58), then divides by 100 instead of by defense.
- **Denial.** 23 boards where a certain `StealAP` or `DrainAP` was legal and the lead foe held 2 AP. Dumb, Aggressive, Defensive, and Utility selected Transaction Tax, Packet Skim, MEV Extract, or Thread Lock on 0 of those 23. The ignore flag, which also requires that the chosen move does not already KO, fired on 21, 21, 19, and 23 of them.
- **Status already applied.** Seven kits carry a status payload. The score does not look at the foe's list. Utility re-selected that status on 1 of the 7: Keyper's Cipher Shot into a foe who already had Cipher Lock. The other six preferred a different utility that would have the same blind spot.
- **Cipher Lock on self.** Utility and Defensive each spent a positive self stage into the lock on 3/18 boards. Bean Sip is one of those clicks. `BuffGate.IsCipherLocked` is what the resolver uses, and no AI calls it.
- **Once-per-battle.** Utility opened with Fox Feint on 10 boards. The wild Kitsauto kit ties it with Packet Skim at 39, and Fox Feint is earlier.
- **Empty utility.** Crackett's Isolation Tool has no status, stage, heal, or utility payload. Utility scores it 39 and the pareto test calls it dominated by Baseline Jab, same cost, on every board in the stored example list. A single Isolation Tool pick shows up in 11 families. The +30 pays for the category, whatever the payload contains.
- **Support order.** Validape test kit, ally at 30% HP: Defensive and Utility both select Buff Ally. Heal Ally is later in the list and never compared. Gilarime test kit on the same shape: Defensive selects Barrier Wall (the first `Support`) and the oracle calls it dominated by Frost Shot. Hibernate's 25% heal is not a candidate.
- **Low HP.** Defensive swaps on 18/18 boards at 15% HP. The two-slot collision above is the same rule twice.
- **Focus fire.** Two Aggressive copies of the same kit, foe HP set to one reliable hit. 17/17 species pairs select the same move into the same slot.
- **Node partner.** Validape oracle moves from 108.3 to 129.2 on Hammer Sync when the ally is buffed. All four personalities keep the same scores and the same click.
- **Banking.** `GetLegalActions` has no Pass while any move is legal. A 4 AP or 6 AP move cannot be saved by skipping. The only bank in the current brains is an AP-neutral or AP-positive move that happens to outscore the spend.

## Boss AI: Bean Sip and Grind Tax

Cofi Lootwak, species base, rebirth 0: HP 168, Ranged Attack 24, Speed 16, type Staked. The encounter is a single boss on Utility. Bean Sip costs 1, is Self, priority (`PRIO|PRIOR`), heals 8% max HP, Speed +1, `GainAP` 1, `rechargeTurns` 2. Player-facing recharge skips `rechargeTurns − 1` upcoming turns (`FormatPlayerFacingRecharge`), so Sip and Grind Tax each lock the move for one selection. The granting turn's end tick consumes one count. Grind Tax costs 6, hits all foes three times for 30 power, Speed −1 once per foe, same recharge, boss-only.

Undisturbed 16-turn line, foes never die, no incoming tax, +2 AP at the start of each turn, Sip resolves before a slower foe because of priority. Utility and, separately, Defensive:

| Turn | AP at the decision | Click | AP after | Speed stage | Speed |
|---:|---:|---|---:|---:|---:|
| 1 | 2 | Bean Sip | 2 | 1 | 24 |
| 2 | 4 | Bean Beam | 2 | 1 | 24 |
| 3 | 4 | Bean Sip | 4 | 2 | 32 |
| 4 | 6 | Grind Tax | 0 | 2 | 32 |
| 5–16 | the same four-turn loop | Sip, Beam, Sip, Grind | | caps at 6 on turn 11 | 64 |

Four Grind Taxes in 16 turns. The first is turn 4. Turns 13 and 15 still sip, at Speed +6 and full HP. The heal restores 0. The stage does not move. The +1 AP is what keeps the turn AP-neutral, which is what lets the bar reach 6. The species note still describes a sip-then-beam cadence. With Grind Tax in the encounter kit the measured cadence is sip, beam, sip, tax.

Removing the +60 bonus leaves that 16-turn line unchanged, because Sip is recharging on every turn Grind Tax is affordable. Putting both moves legal at 6 AP is the spot where the bonus matters: Sip 39, Beam 8.736, Grind Tax 68.84 with the bonus and 8.84 without. Utility then picks the tax. Defensive picks Sip. Aggressive picks the tax on damage-per-AP. Dumb picks Beam.

Removing the +1 AP and keeping the cost at 1: first tax on turn 8, two taxes in 16 turns. The gain halves the wait.

Aggressive and Dumb, same kit, no incoming tax: Bean Beam every turn, 0 taxes in 16. Beam spends the regen. Sip scores too low (Aggressive) or is not the first damaging move (Dumb).

Oracle HP on a Node at 50 Def (140 HP) and a Bot at 48 Def (110 HP), mean-roll floor: Bean Beam 10.5 total (5.7 and 4.8). Grind Tax 74.1 total (39.9 and 34.2). One tax plus the one Beam inside the four-turn loop is about 85 HP. Beaming all four turns is about 42. On this board the loop deals more HP than spamming Beam, and the two Sip turns contribute none of it. Absolute size is the Ranged Attack of 24: about 40 HP on the front body per tax.

Denial, scripted, same scorer:

- Steal 1 AP every turn. Sip is priority, so the steal lands after Sip's gain. Beam and Grind Tax have no priority, so the steal lands before the spend. Result: the bar oscillates between 2 and 3 and Grind Tax is never legal. 0 taxes in 16 turns.
- Steal 1 only when the click is Grind Tax. Turn 4 picks the tax at 6 AP, resolves at 5, the spend fails, and recharge is not applied (`ResolveMove` returns at the failed `TrySpendCost`, before the recharge status). Turn 5 opens at 7 with both Sip and the tax legal. The +60 picks the tax. A steal of 1 leaves 6, and it lands. The same fail-then-land pair repeats. Three taxes land, the first on turn 5.
- Thread Lock blocking the turn-4 regen only (the status is queued onto the next turn start, and turn-start regen checks it). First tax moves from turn 4 to turn 6. Three taxes land.
- Thread Lock on every even turn from 4 through 16. The bar never reaches 6. 0 taxes.

Speed at the first tax is 32 (base 16, stage +2, multiplier 2). Of the other 17 species with a real base speed, 15 are faster than 32 before any of their own stages. Cinislug (28) and Blockwyrm (24) are slower. Grind Tax has no priority tag, so those 15 act first and can steal or lock before the spend. By the time Cofi has sipped to Speed +6 (64), Crackett, Forkrook, Kitsauto, Deghoul, and Keyper still have a higher base speed, and Marrowhead ties at 64.

`EmitBossPayoffTell` runs at the end of the action, after the spend. State 1 is "within 2 AP" (`cost − 2`). State 2 is "AP at or above the cost." On the undisturbed line the tell sees 4 AP after the turn-3 Sip, which is state 1: the grinder rattle, `4/6`. After the tax, AP is 0, the state falls, and the "is due" line does not play. The player gets one turn of warning and then the hit, with no line on the turn the move is actually chosen.

The tax plays as a slow windup that a passive Utility boss will cash on turn 4 and then every four turns, as long as nobody takes AP away. It is fragile in the direction the owner's rule asks for: AP denial answers the AP boost. A deny-every-turn opponent erases the move for the whole window this harness ran. A single steal on the payoff attempt only delays it by a turn, and only because the +60 then prefers the tax over another Sip. The speed and the heal are not what the brain is using. Past the stage cap they are doing no work, and the click continues.

## Ranked proposals

Each item is a PROPOSAL. None of them are implemented.

**1. PROPOSAL — write the target on the action.** Highest impact, smallest change that matches a measured hole. `AIHelpers.GetLegalActions` should emit one `UseMove` per legal target slot, with `targetTeamIndex` and `targetSlotIndex` set. Every `GenerateActions` then keeps the target it scored. `ResolveSingleSelectedTarget` already honors an explicit slot. Evidence: 15/18 Aggressive split boards, 16/18 Dumb, 14/18 Defensive, and 17/17 double-Aggressive focus-fire pairs. Risk: `BuildRandomLegalMisAct` (Glitched Mind) draws from this list, so per-target entries change its odds and its RNG draws. Keep the untargeted list for Glitched Mind, or accept the replay shift on purpose.

**2. PROPOSAL — replace `EstimateDamage` with a pure hit estimate.** High impact, contained. A new function beside `AIHelpers.EstimateDamage`, used by the Aggressive score loop and by `DefensiveAI.FindLethalBlow` and `FindBestAttack`. It should use Atk/Def, the +1, the mean roll, average hits, accuracy, the ×1.2 / ×0.8 collapse, all-enemy sums, and the attack-stat override. Cap at current HP so a second KO is not worth 50 more points. Leave STAB as a switch that defaults to the resolver config, which is off. Do not call `ChemistryResolver.Resolve` or `DamageCalculator.CalculateDamageDetailed`; both have side effects. Typeless policy has to follow whatever the owner settles (question below). Evidence: mean estimate/oracle 0.568 on 17 neutral picks, and the tank false KO on Hammer Sync (53 vs 31).

**3. PROPOSAL — score the payload, then refuse a blank.** High impact for Utility and for Bot, local to `UtilityStatusAI.ScoreAction`. Add 0 for a status the actual target already has, for a positive stage into `BuffGate.IsCipherLocked`, and for a stage already at +6. Read `StealAP`, `DrainAP`, `GainAP`, and `ArmUtilityWindow` from `utilityEffects`. On a score tie inside `GenerateActions`, prefer a typed damaging move over an empty utility, and prefer a denial over a `rechargeTurns >= 100` dodge. Evidence: Tax and Packet Skim at 39, Fox Feint winning that tie, Isolation Tool at 39, Cipher Shot reapplied on the already-status board, Bean Sip still at 39 at Speed +6.

**4. PROPOSAL — a boss payoff horizon, in the same score function.** The +60 already lives in `ScoreAction`. It does not change the undisturbed line. When the kit holds a boss-only move, an AP-negative click should lose points equal to the turns it pushes that move out, and a click that leaves the bar one regen short of the cost should lose to a bank. The clean way to bank is to let `GetLegalActions` include Pass for that one personality when `currentAP >= cost − apPerTurn` and the payoff is the best HP click on the following turn. One-turn lookahead, boss only, no search. Evidence: peace path taxes on turn 4 only because Sip is accidentally AP-neutral; Aggressive never arrives; a steal every turn never arrives; the +60 is what makes the retry at 7 AP take the tax instead of another Sip.

**5. PROPOSAL — fix the Defensive ladder before any encounter uses it.** Smaller impact today, because no encounter selects it, and cheap. `FindBestSupport` should rank by missing HP restored and should treat `HealPercentMaxHP` as a heal without requiring the `HEAL` tag, so Hibernate is visible and Buff Ally stops beating Heal Ally by list order. `FindBestBuff` should use the same Cipher and cap checks as proposal 3, and should consider ally-targeted positive stages (Pack Rally, Buff Ally) when the team is the thing being buffed. `FindLethalBlow` should test the foe the action will actually hit.

**6. PROPOSAL — a one-pass team check after both slots have chosen.** Medium impact, a short second loop rather than a search. If slot A's single-target hit reliably KOs slot 0, slot B rescores without that foe. If both slots swap to the same bench index, slot B picks again. This can live in one helper both personalities call at the end of `GenerateActions`. Evidence: 17/17 focus-fire pairs, and the Gilarime double swap to index 2. `CombatResolver` already rejects the illegal second swap; the player just watches a wasted turn.

**7. PROPOSAL — narration for the choice the player can learn from.** `EmitBossPayoffTell` should read AP from before the spend, so state 2 can say the tax is due on the turn it is selected. Today the rattle plays at 4/6 the turn before, and the due-line never sees 6. `CombatNarrationComposer` already has a `BossTell` case. A second line, only when the boss clicks Grind Tax or when a personality takes a reliable KO over a higher-score setup, is enough. Narrating every Utility +30 will be noise.

**8. PROPOSAL — difficulty knobs on the brain that already exists, not five new classes.** `ENEMY_AI_TIERS_PROPOSAL.md` still has the right shape: one scorer, tiers that turn considerations on, a personality that only multiplies. This round says the first switch, including on a rookie, is targeting plus the defense-aware estimate (proposals 1 and 2). Denial, recharge, and the payoff horizon belong on the boss and on a tactician. Dumb stays list-order. `EncounterDifficulty` is the flag to read, in `CombatBattleController.CreateAI` and the matching switch in `Combat2TestHarness`, as a config on the existing classes. A Mastermind lookahead is not justified by these measurements. One turn of "does this spend miss the tax" is the lookahead that changes a real fight.

## Questions only the owner can answer

1. Is Grind Tax meant to land on turn 4 of a quiet fight, then every four turns? The species note still says sip-then-beam. The encounter kit and the Utility scores produce sip, beam, sip, tax.
2. Past Speed +6 and full HP, Bean Sip's remaining job is the +1 AP that cancels its cost. Should the boss keep sipping once the stage and the heal are dead, or should that click start losing to the tax and to Pass?
3. The rattle at 4/6 is the line the player hears. The "Grind Tax is due" line does not play, because the tell reads AP after the spend. Is the one-turn rattle the intended warning?
4. A steal every turn erased the tax for all 16 turns. A steal only on the payoff attempt delayed it by one turn, then the +60 fired it anyway. How hard should a prepared player be able to turn the boss move off?
5. Typeless damaging moves inherit the attacker's primary type in `DamageCalculator.ResolveAttackType`. The recent call was that typeless is excluded from the ×1.2 / ×0.8 table, with STAB still open. Which rule should a new estimator use?
6. Should any AI be allowed to Pass in order to bank, or is the intended bank only an AP-positive move such as Bean Sip or Fuel Surge?
7. Fox Feint is the wild Kitsauto's opening click, on a tie with Packet Skim, with no incoming threat. Should a once-per-battle dodge be held until something is about to hit?
8. Defensive is unused on every checked-in encounter, and its support step follows list order (Buff Ally before Heal Ally, Barrier Wall before a real heal). Is that personality still part of the roster the tier doc described, and should the ladder be fixed before it is assigned anywhere?

## Files

- This document is the analysis artifact. The owner's project path for it is `Docs/Combat/Design/AI_DECISION_QUALITY_ANALYSIS.md`. It was not written into the repo.
- `ai_decision_check.py` is the harness. It reads the extracted snapshot from `/tmp/chainbound_snapshot` and writes a JSON dump to `/tmp/ai_decision_report.json`.
