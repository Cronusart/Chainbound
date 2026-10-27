# AI decision quality v2

Snapshot: local combat export `chainbound_combat_snapshot_20261024_0015` (24 Oct 2026). Every code, data, and simulation figure below comes from that snapshot, checked by `ai_decision_quality_v2_check.py`. GitHub `main` was not used. The 18 Oct writeup (`Docs/Combat/Design/AI_DECISION_QUALITY_ANALYSIS.md`) is the v1 baseline.

This round asks whether the shipped chooser can play the kit that exists now, and whether it would play the proposal stack (AP economy, cost curve, tempo, passives, tiered walls, Armor, AP denial, rarity build-out). The stack is a set of open proposals. The chooser is the code in `Assets/Scripts/Combat/Runtime/AI`. Supabase functions contain no `ICombatAI`, no `GenerateActions`, and no `UtilityStatusAI`. There is no server-side chooser.

## Executive summary

The shipped brain is still the five personalities from October, plus one new heuristic: Defensive and Utility may Pass to bank a block whose mitigation is above 50% and whose cost is above the +2 regen. That gate matches one catalog row, `blockade`. They then spend the saved AP on something else.

Wild fights and authoritative battles still run Aggressive. That personality spends the whole regen whenever a damaging move costs 2 or less. On the rebirth-0 prefix kits the 12-turn quiet line never reaches Hammer Sync, Hub Heal, Cryo Ray, Cinder Burst, Cipher Burst, Volatile Shot, Fork Split, Blockade, Deep Dig, or either Pickaxe Flurry. The one heavy it does reach is Bulwark Blow, because Immovable Stance costs 1 and the bar is allowed to climb. A 4 AP hit on turn 2 is not a free win against a faster 2 AP rider, and Aggressive cannot even make that choice: an Aggressive Validape dies to an Aggressive Kitsauto on turn 4 with Hammer Sync still uncast, Validape at 0 HP and Kitsauto at 58.

Utility is the Cofi boss and almost nothing else. It scores a category. Bean Sip is 39 at Speed +0 and at Speed +6. Cipher Shot is 44.6, Key Rotation is 39, Memory Leak is 38, so the signature lock never fires. Fox Feint and Packet Skim tie at 39 and Feint, earlier in the list, wins, so the once-per-battle dodge is spent with nothing incoming and the drain is what remains. Transaction Tax is clicked every turn because it is a 1 AP StatusUtility, and the `StealAP` opcode is unread. The same unread category bonus locks Utility onto AP Reserve, Fuel Surge, Crystal Extraction, and Hype Burst.

Under the proposal stack the personality that is actually assigned gets worse in one specific way. Cinder Burst at 2 AP becomes Aggressive Cinislug's every-turn click, and Ember Wall leaves the line. Utility switches too, from Hype at 38.0 to Cinder at 42.3, because a 20% attack drop is counted as a certain stage and Kindling adds 9.4 to every damaging move. Memory Leak's proposed recharge of 2 does nothing while Leak scores 38. Renaming Transaction Tax from Steal to Drain does nothing while the opcode is unread. `stack_2v2_sim.py`'s `policy_greedy` can Pass, reads defense and chemistry, and is not this brain. Its win rates are not a measurement of the shipped AI.

The five proposals that would move real fights: write the target on the action, replace `EstimateDamage` with a hit estimate that includes defense, chemistry, walls, and armor, score the payload and break ties toward the move that does the thing, give the personality that banks one turn of horizon and then let it spend that bank on the move it waited for, and turn those pieces on by `EncounterDifficulty` instead of adding a sixth class.

## How the measurements were made

`ai_decision_quality_v2_check.py` parses the snapshot and reimplements Dumb, Aggressive, Defensive, and Utility from the methods in `Assets/Scripts/Combat/Runtime/AI`. Structural claims are string asserts on that source. Quiet lines and fights are the reimplementation, not the Unity runtime. They use expected HP (one floor of the mean roll 0.925), accuracy as expected value, and average hits. Crits are ignored. A quiet foe is a real Validape kit with current HP forced to 5000 so the KO bonus cannot fire, and max HP left at 156 so Kindling's 6% stays real. Block duration ticks, so a wall can expire on a foe that never swings. Each line gets a fresh foe.

Rebirth 0 equips four slots. The kit is the pool prefix: Initial, then Flagship, then Learnable, then Exclusive, deduped, cut at four, passives included when they fall in that prefix. Eighteen species have a pool and a base-stat asset. Cofi's encounter kit (`bean_sip`, `civet_beam`, `grind_tax`) is scored separately from its pool prefix. The proposal-stack lines change only the open defaults this round cares about: Cinder Burst to 2 AP, Memory Leak recharge to 2, Transaction Tax `StealAP` renamed to `DrainAP`, Blade Flurry power 12 to 10, Volatile Shot's surcharge removed, Bulwark held at 3 AP, Stake Line (typed Staked, power 34, 2 AP) replacing `baseline_shot` on Rattlestake, Fee Collector replacing Slipstream on Deghoul, Extraction Hit replacing `baseline_slam` on Kodiax. Extraction Hit is outside the rebirth-0 prefix, so that swap does not change the kit the chooser sees.

The oracle used to judge a click puts the +1 on before chemistry, then mitigation, matching `DamageCalculator`. Typeless damaging moves inherit the attacker's primary type unless a line says otherwise. STAB is off (`enableStab: 0`). Regen is 2, the cap is 10, the mitigation floor is 0.1. This is a mean-roll expectation, not a sampled engine roll.

## v1 findings, revisited

v1's 222-board pareto harness (`ai_decision_check.py`) is not in this snapshot and was not re-run. The shares 21.2 / 14.0 / 12.6 / 12.2 / 31.1 are last October's numbers. This round replaces them with quiet lines, a 17-board split, and the fights below. A smaller, different test (legal actions at 4 AP against Gilarime) finds 35 of 84 actions HP-dominated, 41.7%. That is not the v1 random rate.

| v1 finding | Status | What this snapshot does |
|---|---|---|
| Damage moves are `UseMove(team, index)` with target −1, and the resolver sends them to slot 0 | **Remains** | Still true for every non-block move. On 17 split boards (full Blockwyrm in front, 28 HP Deghoul behind) a single-enemy click was the worse target for Dumb 17/17, Aggressive 16/17, Defensive 13/17, Utility 6/17. Aggressive's other board is Forkrook's Fork Split, which is all-enemy and hits both slots because of the move, not because the AI aimed. Two Aggressive Validapes over 4 turns deal 168 to slot 0 and 0 to a full-HP Deghoul in slot 1. |
| No personality can Pass while a move is legal, so nothing banks a 4 AP or 6 AP hit | **Narrowed, then missed** | Defensive and Utility call `ShouldBankForGreaterBlock`. The gate is mitigation above 0.5 and cost above 2. Of the 8 `BlockWindow` rows, only `blockade` (60% melee, 3 AP) passes. Ember Wall is 50% and 2 AP, so the gate skips it. Blockwyrm at 2 AP against Validape's Hammer does Pass. Utility then clicks Immovable Stance for the rest of the line and never casts Blockade or Bulwark. Defensive's next clicks are Bulwark, Bulwark, and Blockade never appears. Aggressive still has no Pass. |
| `EstimateDamage` is `Power × (Atk/100) × hits × accuracy`, with no defense, chemistry, +1, walls, or the attack-stat override | **Remains.** The ratio moved because the foe changed | Against full-HP Gilarime, mean estimate/oracle on the 18 damaging moves Aggressive actually picks is 0.727. v1's 0.568 was against defense 55. Bulwark Blow is still the structural miss: estimate 26.10 from Melee Attack 58, oracle 80.10 against Deghoul because the move strikes with Melee Defense 86. Hammer Sync into Deghoul is 53.2 estimated against an oracle of 106.0 open, 81.7 at 25% mitigation, 43.7 behind a 60% melee wall, and 97.8 behind flat armor 12. Damage-per-AP stays 13.300 through all four. |
| `DamagePerAP` returns 0 at cost 0 | **Remains** | The source still returns 0. Fuel Surge and AP Reserve are cost 0. Aggressive therefore scores them with the non-damage 0.1. Utility clicks both anyway, off the category bonus. |
| Steal, Drain, Gain, and armed windows are unscored. Denial was 0/23 | **Remains**, on a smaller board | The AI sources do not name `StealAP`, `DrainAP`, `GainAP`, `ArmUtilityWindow`, `MitigationWindow`, `BankAP`, `HealSelf`, `StripBuff`, or `ChangeStage`. Rebirth-0 prefix kits put a certain denial on 3 boards where the foe holds 2 AP and the chosen move does not already KO. Dumb, Aggressive, and Defensive pick it 0/3. Utility picks it 1/3, and that one is Deghoul's Tax, which wins as an empty 1 AP utility. |
| No Speed, priority, or recharge length in the score | **Remains** | Choice code does not read `CoreStat.Speed` or a `PRIO` tag. Kitsauto at Speed 82 deletes an Aggressive Validape (Speed 60) before Hammer Sync is ever legal. Fox Feint's recharge of 999 still wins its tie. |
| No chemistry, and typeless inherits the attacker's type in the engine | **Remains** | The estimate comment says it skips chemistry. No AI calls `ChemistryResolver`. Cinislug's typeless Pulse into Deghoul is 51.3 if it inherits Burned and 42.8 if typeless is neutral. |
| No read of the foe's wall or mitigation | **Remains, and Armor makes it newly expensive** | No AI reads `mitigationSources` or `HasIntactBlock`. `UtilityEffectType` has no `ArmorFlat`. Guard Posture is a 25% `MitigationWindow` with an ARMOR tag, and it is in no learn pool. A flat 12 on the foe cuts Hammer's oracle from 106.0 to 97.8 and leaves the score unchanged. |
| Cipher Lock and the +6 cap are unread. Sip scores 39 at the cap | **Remains** | Sip is 39.0 at Speed +0 and 39.0 at Speed +6. Utility re-applies an already-present foe status on Crackett's Shard Partition, Keyper's Cipher Shot, and Marrowhead's Partition Shot. |
| One AI, one personality, `EncounterDifficulty` unread, `cpuAI` unused, Defensive unused, no server AI | **Remains** | Cofi is Utility and Boss (`aiPersonality: 3`, `difficulty: 2`). The intro encounter is Aggressive. The manual provider is Random. No checked-in encounter asset selects Defensive. Authoritative battles hard-code Aggressive. `CreateAI` takes `encounter.aiPersonality`. |
| Boss +60 does not change the natural sip / beam / sip / tax loop | **Remains** | Undisturbed 16-turn Utility and Defensive lines on the encounter kit are still that loop, four taxes. At 6 AP with both legal, Tax is 68.84 and Sip is 39. Without the +60 the tax is 8.84 and Sip wins. Aggressive and Dumb beam all 16 turns and tax zero times. |
| Fox Feint wins the 39-tie with Packet Skim | **Remains** | Utility Kitsauto opens Feint, then Packet Skim for the other 11 turns. Aggressive Kitsauto is Blade Flurry only. |
| Both low-HP actives swap to the same bench index | **Remains** | Two Defensive Gilarime at 15% HP both return swap. |
| `CombatStateEvaluator.Evaluate` and `ThreatScore` are unused by the chooser | **Remains** | Evaluate is never called. ThreatScore exists only as a `PickTarget` option. Aggressive uses most-vulnerable (1 − HP%) and then throws the slot away. |
| Bean Sip grants 1 AP, Grind Tax exists, recharge ticks on the bench | **Still true** | These had already landed relative to the 17 Oct tier note. The chooser still does not read the bench clock. Aggressive never reads recharge. |
| The boss tell reads AP after the spend, so "is due" never plays | **Fixed** | `EmitBossPayoffTell` is now the first call in `ResolveAction`, before `TrySpendCost`. The comment says the bank is read before the spend so the ready state is visible on the payoff turn. This round did not replay the log line. It checked the call order. |
| Block moves cannot name an ally | **Fixed** | Block moves emit `UseMove(team, index, team, targetSlot)`. |
| Random draws the combat RNG | **Remains** | `RandomAI` takes `CombatRng`. This round did not re-sample the stream coupling. |
| Glitched Mind draws from the legal list | **Remains** | `BuildRandomLegalMisAct` calls `AIHelpers.GetLegalActions`. Per-target entries would change that status's odds and its RNG draws. |

What got worse, or became visible only because the kit changed:

- The new bank's 0.5 line puts Ember Wall, the wall Cinislug actually leads with, on the wrong side of "greater." The one wall that qualifies is then not the click.
- Defensive Cinislug, with no damaging move affordable at 2 AP, returns Pass, then Cinder Burst, Cinder Burst, and never Ember Wall. v1 described Pass as absent whenever any move was legal. The ladder's default is Pass when the damaging search finds nothing, and this kit makes that the line.
- Kindling and a 20% rider are scored as certain. That is what flips Utility from Hype to Cinder the moment Cinder costs 2. Live Cinder at 3 AP scores 32.3 and loses to Hype at 38, and it is never affordable while Hype spends the regen.
- Utility's +10 "clean foe" bonus applies to self buffs. Key Rotation scores 39 against Leak's 38 on a clean board. Cipher Shot's damage-plus-status score of 44.6 beats both, so the lock move is never the click on the kit the AI equips.
- Empty opcodes with a real payload become the whole line: AP Reserve (banks and releases), Fuel Surge (+3 AP and a self spike), Crystal Extraction (heal per debuff, then cleanse), Hype Burst (strip, then a stage). The score sees the category.

## Key findings

### The score is not the hit

`EstimateDamage` is still `Power × (Atk/100) × average hits × accuracy`. Melee reads Melee Attack after stages. Ranged reads Ranged Attack. Defense, the +1, the 0.85–1 roll, chemistry, crits, mitigation, walls, armor, the attack-stat override, conditional multipliers, and extra targets are outside it.

On Aggressive's actual damaging pick against full-HP Gilarime, 18 species:

| Click | Estimate | Oracle | Ratio |
|---|---:|---:|---:|
| Validape Hammer Sync | 53.20 | 83.60 | 0.636 |
| Cinislug Cinder Burst | 26.93 | 51.30 | 0.525 |
| Keyper Cipher Shot | 26.60 | 50.35 | 0.528 |
| Blockwyrm Bulwark Blow | 26.10 | 51.30 | 0.509 |
| Kitsauto Blade Flurry | 25.99 | 28.50 | 0.912 |
| Deghoul Gas Bump | 24.62 | 25.65 | 0.960 |
| Cofi Bean Beam | 2.74 | 3.80 | 0.720 |
| Mean of all 18 | | | 0.727 |

Bulwark against Deghoul, where Melee Defense is 86 and Melee Attack is 58, is the number that matters for the override: estimate 26.10, oracle 80.10. Hammer Sync's CSV bonus is `+0.20` per buffed partner, cap `1.20`. The estimate has no partner term, so a buffed ally does not change the click. Nine moves carry a conditional damage row, including Hammer Sync and Seam Cut. Two moves override the attack stat: `crystal_bash` and `bulwark_blow`.

Utility's damaging term is that estimate divided by effective cost, times 2, plus 20 per status entry, plus 8 per enemy-facing negative stage point, plus Kindling's `max(1, 6% of the first unkilled foe's max HP)` on every damaging move, then minus the cost. The 20% attack drop on Cinder is stored as a stage of −1. The scorer adds 8 for it without multiplying by 0.2. Kindling on a real Validape max HP of 156 adds 9.36. Together, against Hype Burst's flat 38.0:

- Cinder at the live 3 AP scores 32.315. Hype wins, and Hype costs 2, so Cinder is never affordable on the quiet line.
- Cinder at the proposed 2 AP scores 42.292. Utility's whole line becomes Cinder.

Aggressive does not use that utility math. It uses damage per authored AP. Cinder at 2 AP is simply the best damaging click it can afford every turn, so Ember leaves the Aggressive line as well. The stack writeup's "Cinislug clicks Cinder 99%" describes `policy_greedy`. The shipped Aggressive line under the same AP change is also all Cinder, for the cheaper reason that a 2 AP damaging move is always legal and a wall scores 0.1.

### Tempo, with the turn order included

Kitsauto Speed 82, Blade Flurry 12×3 at 2 AP. Validape Speed 60, Hammer Sync power 80 at 4 AP. Both Aggressive, 1v1, mean-roll chunks:

- The AI Validape clicks Snap Strike every turn it can. Hammer never lands. The fight ends on turn 4. Validape is at 0. Kitsauto is at 58. The last click is Blade Flurry for 3 into an already-dead Validape.
- A human Validape who Passes until 4 AP and then hammers does land Hammer on turn 2. Validape still dies. Kitsauto is left at 66.

The design rule holds. A 4 AP heavy on turn 2 is not an automatic win against a faster foe's 2 AP hits. The AI failure is narrower than "it banks badly." It never has a Pass, so the timing choice does not exist. The human who does bank still loses this pairing, and the chip line leaves the fox lower only because the human skipped turn 1.

The same shape is the whole heavy list. Quiet Aggressive lines that never cast a move of 3 AP or more: Validape (Hammer Sync, Hub Heal), Gilarime (Cryo Ray), Cinislug (Cinder Burst), Keyper (Cipher Burst), Deghoul (Volatile Shot), Forkrook (Fork Split), Blockwyrm (Blockade), Minefolk (Pickaxe Flurry), Kodiax (Deep Dig, Pickaxe Flurry), Balloom (Volatile Shot). Reached: Blockwyrm Bulwark Blow only. Dumb Gilarime clicks Frost Shards, because that is the first damaging move. Aggressive, Defensive, and Utility click Frost Shot. Ice Wall and Cryo Ray do not appear.

### AP banking

Three different "banks" are in the tree. Only one of them is a decision.

1. **The block bank.** `ShouldBankForGreaterBlock` requires mitigation above 0.5, cost above 2, current AP below that cost, AP below the cap of 10, and `ScoreBlockValue` above 0. Score is the largest matching-category foe `EstimateDamage`, times the mitigation, and it has to clear `max(1, 15% of the ally's current HP)`. Contact filters: a Melee block looks at melee moves, anything else looks at ranged. It does not read the foe's live wall, and it uses the estimate, not the oracle. Only Blockade qualifies. Ember Wall fails both halves of the gate. Blockwyrm does Pass at 2 AP against Validape. The following turn the bar is at 4, the gate closes because the cost is now affordable, and the click is Immovable Stance (Utility, score from the Immovable status) or Bulwark Blow (Defensive, best damage per AP). Blockade is never cast on either line, live or under the stack with Bulwark held at 3.

2. **The accidental Pass.** Defensive's last step only searches damaging moves. When none are affordable it returns Pass even if a wall or a utility is legal. Cinislug's defensive line is Pass, Cinder, Cinder, repeating, and Ember is absent. That Pass is what lets the bar reach 3. It is not a plan to wall.

3. **The category bank.** Utility clicks cost-0 and cost-1 utilities forever because +30 beats a real hit. Balloom's line is Fuel Surge, whose payload is `GainAP` 3 plus a self spike, and Aggressive Balloom is Gas Bump, so the +3 is never what Aggressive uses to reach Volatile Shot. Rattlestake's line is AP Reserve (`BankAPUntilNextTurn`), and Aggressive Rattlestake is Pulse Shot. Under the stack, Aggressive switches to Stake Line by damage per AP (8.883 against Pulse's 6.793) and Utility stays on AP Reserve. Kodiax Utility is Crystal Extraction for all 12 turns. Aggressive is Ore Strike and never reaches Deep Dig. The bank, the heal, and the cleanse are not in the score.

Aggressive and Defensive still divide by the authored `apCost`. Utility's `ResolveScoringCost` uses current AP for `SpendRemainingAp` and adds an expected self `NextMoveAPSurcharge`. That surcharge path does not matter on the quiet lines, because the moves that carry it are the heavies Aggressive never affords. The stack deletes Volatile Shot's surcharge. Deghoul's Utility line stays Transaction Tax either way.

Cofi, encounter kit, is the one bank that works, and it works because Bean Sip is AP-neutral by accident. Sip costs 1, grants 1, recharge 2, heal 8%, Speed +1, priority tags. Utility and Defensive, 16 undisturbed turns: Sip, Beam, Sip, Tax, and the same four turns again, four taxes. The +60 does not change that loop, because Sip is recharging whenever Tax is affordable. It matters only when both are legal at 6 AP: Dumb beams, Aggressive taxes, Defensive sips, Utility taxes. Sip at the stage cap still scores 39, so turns past Speed +6 keep sipping. The heal at full HP restores nothing. The stage does not move. The +1 AP is what keeps the loop on schedule, and the score does not know that.

A faster Packet Skim every turn, against that Utility line, produces zero Grind Taxes in 16 turns. Sip is priority, so the skim lands after the gain. Beam and Tax are not, so the skim lands before the spend. The bar never reaches 6. Against Aggressive Cofi the same skim does not blank Bean Beam. Over 8 turns the beam lands 4 times and fizzles 4. A failed spend does not consume AP, so the next turn opens high enough for the 2 AP beam to resolve. A 1 AP drain on a committed 2 AP move is an alternating fizzle, not a lock. Kitsauto's own AP climbs, because Skim costs 1 and regen is 2.

### Walls, barriers, and Armor

Eight `BlockWindow` rows. The bank sees one of them.

The chooser can name an ally for a block, and `ScoreBlockValue` knows the contact filter. It does not know:

- the foe's current wall, so it will refresh a category the foe is already protected in, and it will not prefer the category the foe is open in
- Armor, because there is no opcode. Guard Posture remains a 25% window, unpooled. A proposal that puts flat 12 on Guard Posture and flat 16 on Guard Stance is invisible until the estimator reads a number the engine actually applies
- its own wall's remaining turns, except that a target who already holds a block is illegal, which is an eligibility fact rather than a score

Utility Cinislug's quiet line is Hype Burst twelve times. Hype is a StatusUtility with a `StripBuffsThenStage` payload and no status column, so it scores 30 + 10 − 2 = 38. Ember's block score against Validape's melee kit is a real positive number and still loses to 38. Aggressive Cinislug alternates Ember Wall and Hype Burst, because both score 0.1, Ember is earlier, Ember's recharge is 2, and Cinder costs 3. The wall is a tie-break, not a plan. Defensive never clicks it.

Utility Humpacked clicks Compression Trade twice (self Melee Attack +2, self Speed −1), then Lean Hit for the rest of the line, and never clicks Barrier Wall. The attack stage feeds `EstimateDamage`, so the hit's score eventually crosses the flat 39. The Speed drop on the same move adds nothing, and the +6 cap is unread. A ranged barrier into a melee kit is the contact filter working. The AI is not weighing the wall. It is failing a threshold and moving on.

Immovable Stance's card says Def +2. The stage column is empty. The Def lives on the Immovable status. Utility scores the status entry (+15) and the category (+30) and the clean-foe bonus (+10), and that 54-class number is why Blockwyrm's bank becomes a stance lock. The passive in the same prefix, Immutable Ledger, is not named anywhere in the AI sources.

### Passives

Utility names two passive ids: `uplink` and `kindling`. It does not name `immutable_ledger`, `patience_passive`, `fee_collector`, `streamlined_flow`, or `parallel`. Uplink adds 12 to an ally-targeted support or to Extend Buff / Raise Lowest Stage when the passive is equipped. Kindling adds the 6% chip to every damaging move, which does not change which damaging move wins unless the chip pushes the hit across a utility. That is exactly the Cinder-versus-Hype flip at 2 AP.

The other passives change the fight and the click stays put. Streamlined Flow's discount, Patience's ramp, Fee Collector's "drain 1 from the first opposing actor whose AP is strictly higher," and Immutable Ledger's team rule are all outside the score. The stack puts Fee Collector on Deghoul in place of Slipstream. Utility Deghoul still clicks Tax. This round did not re-sim Defensive under that swap. Live Defensive Deghoul is Slipstream for all 12 turns, a self Speed +2, and removing Slipstream would drop that ladder onto whatever self-stage utility remains, or onto Gas Bump.

### Status, chemistry, and the type rule

Typed moves are supposed to beat typeless, and a rare typeless move is supposed to sit near a typed common. The chooser cannot apply either rule. It has no type term.

Cinislug (Burned) firing typeless Pulse Shot into Deghoul (Gas): the engine's inherit path is 51.3 expected HP, the neutral path is 42.8. Burned beats Gas, so today's resolver hands the typeless move the advantage. Rattlestake firing the same Pulse into Forkrook: inherit 35.1, neutral 29.4. A typed Stake Line at power 34, scored with neutral chemistry the way a typed move always is, does 45.6 into Forkrook and 38.9 into Deghoul. A neutral Pulse from that same body into Deghoul is 37.0. Power 34 at a 0.8 resist still edges power 26 at neutral. Aggressive prefers Stake Line anyway, 8.883 damage per AP against 6.793, because the estimate is the power. Into a Gas foe that preference is a small oracle edge. Into a Forked foe it is the rule working for a reason the AI did not use. If typeless stays on the inherit path, Pulse into Gas (51.3 from Cinislug's Ranged Attack) is the bigger hit, and Aggressive will still take Stake Line.

Meme has no advantage cell in the v1 directions matrix, which is the importer's first choice. The v2 matrix still has Meme cells and is not what this oracle used.

Statuses are a flat bonus per CSV entry. Utility does not look at the foe's list, so Cipher Lock, Shard Partition, and Partition Shot are re-clicked into a foe who already has them. A 20% stage is paid as if it landed. A self stage into a cipher, or into a stat already at +6, is paid in full. Bean Sip's Speed +1 is a self stage, so it adds nothing to the 39, which is why the cap does not matter to the score and the click continues after the stage is dead.

Hype Burst, Crystal Extraction, and AP Reserve are the build-out case. The payloads exist. The score awards the category and the cost. Building the empty move out, which is the right design move, does not change the click until the scorer reads the opcode.

### Target selection in 2v2

Single-enemy and single-ally moves still leave the target at −1. `ResolveSingleSelectedTarget` sends them to the first living active, slot 0. All-enemy moves hit everyone, which is the only way slot 1 takes damage from this chooser.

The split board is a full Blockwyrm in front and a 28 HP Deghoul behind, AP 8, one actor. A board counts when some single-target move removes at least 8 more HP from the back, or KOs the back and leaves the front alive. That is 17 of the 18 prefix kits. The counter increments only when the chosen move is single-enemy and that click is the worse one. Dumb is 17/17. Aggressive is 16/17, and the other board is Forkrook's Fork Split, an all-enemy move that hits both slots because the move says so. Defensive is 13/17. The other four are self buffs (Compression Trade, Slipstream twice) and that same Fork Split. Utility is 6/17. The rest are self buffs, ally supports, or single-enemy utilities whose payload removes no HP. None of those retarget slot 1. |

A short exchange makes the same point in damage. Two Aggressive Validapes, four turns, Blockwyrm in slot 0 and a full Deghoul in slot 1: 168 damage on the tank, 0 on the squish, squish still at 106 HP. A human who leads a live wall or a fat body in slot 0 and puts the real target in slot 1 is not being clever. They are standing where the action points.

Defensive's lethal-blow step has the same hole. It tests the lowest current HP and then emits an untargeted action, so a KO it "found" on slot 1 lands on slot 0. The +50 lethal bonus on Aggressive uses that same untargeted foe.

There is no second pass. If slot A already KOs slot 0, slot B still selects into slot 0. v1 measured 17/17 same-kit pairs doing this. This round's two-Validape exchange is the same rule with HP attached.

### Switching and the bench

Recharge ticks on the bench. `CombatResolver.TickBenchedRecharge` calls `TickRechargeWhileBenched`. Other statuses pause there. The chooser never reads a recharge clock, on the field or on the bench, so it will not swap a recharging attacker out to buy the tick, and it will not hold a benched body in because the clock is already running.

Defensive's swap rule is still "below 20% HP, the first legal swap." The bench is not compared. Two Defensive Gilarime at 15% HP with a bench both return swap. The resolver rejects the second. The player watches a wasted action. No other personality swaps on the quiet lines, because the quiet bodies stay healthy and Aggressive scores a swap at 0.05, under the 0.1 it gives every non-damaging move.

### AP denial, which is Bot's job

Bot is the denial type. The denial the Kitsauto prefix actually has is Packet Skim, `DrainAP` 1, Common, 1 AP. Thread Lock is not in that pool. MEV Extract is in no pool. The learn-pool band is a hard constraint: three moves at the body tier or lower, one at that tier or one step higher, signature strictly highest. Scoring Skim is the available fix. Pulling Thread Lock or MEV onto Kitsauto is not a proposal this round will make.

What each personality does with the denial it already holds:

| Body | Denial it has | Quiet click |
|---|---|---|
| Kitsauto | Packet Skim, and Fox Feint's armed dodge | Utility: Feint, then Skim forever. Aggressive: Blade Flurry forever. The drain is why Utility's line looks like denial. The score is the 39-tie, and Feint was earlier. |
| Deghoul | Transaction Tax, `StealAP` 1 | Utility: Tax for all 12 turns. Aggressive: Gas Bump. Defensive: Slipstream. Renaming the opcode to Drain does not change the Utility line. |
| Keyper | Memory Leak, armed −2 at the owner's next turn start, plus Speed +2. Signature. | Utility, Dumb, and Aggressive: Cipher Shot for all 12 turns. Defensive: Key Rotation for all 12. Leak is absent live and under the proposed recharge of 2. Scores on a clean foe: Cipher 44.6, Rotation 39.0, Leak 38.0. |
| Cofi, scripted | A human Packet Skim every turn | Utility: 0 taxes in 16. Aggressive: beam lands 4, fizzles 4, in 8 turns. |

A scripted Memory Leak every turn against Aggressive Validape: the victim clicks Snap Strike once, then Pass for the remaining seven turns, because nothing it wants is affordable. Hammer Sync never lands. The Pass is an empty legal list, not a plan to spend the open turn the proposed recharge would create. Leak recharge 2 opens a passer to 2, 2, 4, 4, 6, 6, 8, 8 in the denial calendar. It opens nothing in this chooser's hands, because this chooser does not click Leak.

Fee Collector, unpooled today and proposed onto Deghoul, drains 1 from the first opposing actor only when their AP is strictly higher. The AI does not name it. A tactician that learned to click Tax would still not be playing Fee Collector unless the passive is in the score.

### What a human PvP player can do to this brain

These are the lines a person can force without knowing anything the code hides.

- Lead the wall or the fat body in slot 0. Put the thing you care about in slot 1. Single-target personalities never leave slot 0. All-enemy moves still hit both, so this exploit is about the single-target kits, which is most of them.
- Outspeed an Aggressive body that has committed to a 2 AP move and Skim on that turn. The move fizzles, the AP is not spent, and the next turn the move lands. You get every other swing, not a permanent blank. Against the Utility Cofi boss the same Skim is stronger: zero Grind Taxes across 16 turns.
- Let Utility Kitsauto open. Fox Feint is spent on turn 1 with no attack coming in. After that the fox Skims and does not swing. Blade Flurry, which is what Aggressive would have been doing, never appears.
- Stay healthy against Defensive. It will buff (Slipstream, Key Rotation, Bean Sip, Compression Trade) instead of attacking. Drop both actives under 20% and both of them swap at the same bench index. The second swap fails.
- Do not respect a Blockwyrm that "banks." The Pass is for Blockade. The next click is Immovable Stance or Bulwark Blow. Blockade does not come out.
- Against Utility Keyper, the lock is not coming. Cipher Shot is a real status, and it will be reapplied after it has already landed. Memory Leak will not be clicked. Key Rotation is what Defensive does instead, including into a full def stage, because the cap is unread.
- Against wild Aggressive, any move of 3 AP or more is unreachable while the kit has an always-legal 2 AP damaging move. You do not have to play around Hammer, Cryo Ray, Deep Dig, Fork Split, or Cinder at 3. You do have to play around them the turn the stack drops Cinder to 2, because then Aggressive Cinislug is Cinder every turn and the Ember line is gone.
- A human Validape who Passes to hammer still loses the 1v1 to faster Blade Flurry. The exploit is not "bait the bank." The exploit is that the AI cannot bank, so you are always fighting the 2 AP click, and the 2 AP click is enough.

### How the proposal stack would actually be played

`policy_greedy` in `stack_2v2_sim.py` estimates with defense and chemistry, aims at the lowest HP percent, scores Skim and Tax, scores walls by incoming damage times the percent, scores Hype only when the foe has buffs, and can Pass to bank a 4 AP heavy that is one regen away. It does not call `UtilityStatusAI`. Quoting its archetype win rates as the metagame of this AI will mis-rank the stack.

What the shipped personalities do under the open defaults, quiet line, same Validape foil:

| Species | Aggressive | Utility |
|---|---|---|
| Cinislug | Cinder every turn. Live line was Ember / Hype, and Cinder was absent. | Cinder every turn. Live line was Hype. The flip is 42.3 against 38.0, Kindling included at max HP 156. |
| Kitsauto | Blade Flurry still, at power 10. | Feint, then Skim. Unchanged. |
| Deghoul | Gas Bump. | Tax, after the opcode rename. |
| Keyper | Cipher Shot. | Cipher Shot. Leak recharge 2 is unused. |
| Blockwyrm | Stance / Bulwark, Bulwark held at 3. | Pass, then Stance. Blockade still absent. |
| Validape | Snap Strike. Hammer still absent. | Snap Strike. |
| Rattlestake | Stake Line, by damage per AP. | AP Reserve still. |
| Kodiax | Ore Strike. Extraction Hit replaced a move outside the prefix. | Crystal Extraction still. |
| Gilarime | Frost Shot. Cryo still absent. | Frost Shot. |

The stack item that changes a shipped click is Cinder's AP, and Stake Line's power on Aggressive Rattlestake. The stack items that were supposed to change denial (Leak recharge, Tax as drain, Blade Flurry's power) do not change the click. Armor flats on unpooled cards do not change it either. A greedy sim that stops walling is describing its own Pass rule. Shipped Utility stops walling, in the one case where it ever walled, because a mis-counted rider outscores Hype. Shipped Aggressive stops walling because a 2 AP hit outscores a 0.1.

## Ranked recommendations

Each item is a **proposal**. None of them are implemented. None of them add a move to a learn pool.

**1. Proposal — write the target on the action.** Highest leverage, and the code path already exists. `GetLegalActions` should emit one `UseMove` per legal slot, with the team and the slot set. The personality keeps the target it scored. `ResolveSingleSelectedTarget` already honors an explicit slot. Evidence: Aggressive stuck on the worse body on 16 of 17 split boards, Dumb on 17 of 17, and 168 damage into a tank beside a full squish over four turns. Risk: Glitched Mind draws uniformly from that same list, so per-target entries change its odds and advance the combat RNG differently. Keep an untargeted list for that status, or accept the replay shift on purpose.

**2. Proposal — replace `EstimateDamage` with a pure hit estimate.** Use it in Aggressive's loop and in Defensive's lethal blow and best attack. Atk/Def, the +1, the mean roll, average hits, accuracy, the 1.2/0.8 collapse behind a switch, all-enemy sums, the attack-stat override, Hammer's partner multiplier, a cap at current HP, and the foe's first-hit wall, mitigation, and flat armor. The typeless switch defaults to the live resolver, which inherits, and has a second position for the proposal's neutral rule. A stage rider is multiplied by its chance, so Cinder's 20% drop is 0.2 of a stage and not a certain −1. Do not call `ChemistryResolver.Resolve` or `CalculateDamageDetailed`. Both have side effects. Evidence: mean ratio 0.727 against real Gilarime defense, Bulwark 26.10 against an oracle of 80.10, Hammer's damage per AP unchanged from 106.0 down to 43.7 behind a 60% wall and down to 97.8 behind flat 12.

**3. Proposal — score the payload, then break the tie toward the move that does the thing.** Local to `UtilityStatusAI.ScoreAction`, and it is what makes Bot, the boss, and the build-out real.

- Add 0 for a status the actual target already has, for a positive stage into cipher, and for a stage already at +6.
- Read `StealAP`, `DrainAP`, `GainAP`, `ArmUtilityWindow`, `BankAPUntilNextTurn`, `HealSelfPerTargetDebuff`, `StripBuffsThenStage`, and a mitigation or armor window from `utilityEffects`. Value a denial by the AP it removes times what that AP was about to pay for, not by a flat +30.
- On a tie, prefer a typed damaging move over an empty utility, and prefer a denial over a `rechargeTurns >= 100` or once-per-battle dodge unless an incoming hit is actually going to land.
- Kindling's chip applies once, to the move that would apply it, and does not get added again to a move that does not spread Kindled.

Evidence: Sip still 39 at Speed +6, Feint beating Skim on a tie, Leak at 38 under Cipher at 44.6, Tax clicked with the opcode unread, AP Reserve and Fuel Surge and Crystal Extraction and Hype as the whole line, Cipher Shot reapplied, and the 20%-plus-Kindling sum that produces Cinder at 42.3.

**4. Proposal — one turn of AP horizon, and the bank has to be allowed to spend itself.** An AP-negative click loses points equal to the turns it pushes the best unaffordable payoff out. Pass is legal for that personality when current AP is at least cost minus 2 and the payoff is the best HP click next turn. The boss bonus looks at pre-spend AP. The tell already does. The chooser does not.

The block bank needs a second sentence. If the Pass was for Blockade, the turn Blockade becomes affordable it has to be allowed to beat Bulwark and Immovable Stance. Otherwise the heuristic saves for a wall it will never click. Ember at exactly 50% should sit on the "worth banking" side of whatever line the heuristic uses, or the line should be "mitigation of 0.5 or more," because 50% is the mid wall the kit actually ships.

Aggressive is the personality wild and authoritative battles run. It needs this horizon or every 3 AP and 4 AP move behind a legal 2 AP click stays dead. One turn is the lookahead. A full search is not justified by these lines.

**5. Proposal — turn the pieces on with `EncounterDifficulty`, and add one team pass.** The tier doc's shape still fits: one scorer, tiers that enable terms, a personality that only multiplies. Read the flag in `CreateAI` and in the test harness. The `cpuAI` inspector field is not that flag.

- Rookie stays Dumb: first legal damaging move, list order.
- Standard, which is what a wild fight should be, gets the target and the defense-aware estimate. That is the gap. Today's wild brain is Aggressive, which has neither.
- Tactician adds denial, walls, the foe's wall and armor, the passives it actually has equipped, recharge, the one-turn bank, and the payload scores.
- Boss is the tactician plus the payoff horizon. Cofi is already Utility. The +60 can stay. It should finally change the quiet line.

After both slots have chosen, one short pass: if slot A's single-target hit KOs slot 0, slot B rescores without that foe. If both slots swap to the same bench index, slot B picks again. Evidence is the double swap and the 168/0 exchange. This is a second loop, not a search.

### Mechanic notes the AI exposes

These are proposals about the design, kept separate because the chooser is the thing that makes them visible.

- **Ember's 50% sits on the wrong side of a brand-new gate.** The gate is the AI's, and the card is a mid wall of 50% at 2 AP. Either the heuristic should treat 50% as worth banking, or Ember should not be the wall a "greater block" rule ignores. Lengthening Ember's recharge is the wrong knob. The column is already 2.
- **A 2 AP move that is always legal deletes the rest of the kit for any personality that cannot Pass.** The stack's greedy Pass was covering that hole. Dropping Cinder to 2 AP without a chooser that can still value Ember means the wall archetype's own Aggressive body abandons the wall. Utility abandons it too, and for a bad reason. If Cinder at 2 is the intended click, the wall needs a score that can beat a mis-counted rider. If Ember is the intended click, Cinder at 2 needs the chooser to be allowed to wait, or the wall's score has to clear 42 on the turns it matters.
- **Leak recharge 2 and Tax-as-drain do not touch this brain.** They are real calendar changes for a human or for `policy_greedy`. They are a no-op for Utility Keyper and a no-op for Utility Deghoul. Ship them for the player. Do not expect the CPU to play the new calendar until proposal 3 is in.
- **Armor is a number the estimator must grow a slot for, and not before the opcode exists.** Tuning weights for a flat 12 that no equipped move applies will not show up in a fight. When the flat lands, Hammer's damage per AP has to move or every Aggressive body will swing into armor at the open-board score.
- **Do not read the stack sim as this AI.** The duo spike, the 99% Cinder share, and the denial-archetype drop are `policy_greedy` plus the AP changes. The shipped Aggressive Cinder line is real and narrower. Re-run that sim with the shipped personalities before those rates are treated as the metagame.

## Questions only Cronus can answer

1. Is sip, beam, sip, tax still the intended boss cadence? The encounter kit and both Utility and Defensive produce it, four taxes in 16 quiet turns. The +60 does not create that loop. Sip's +1 AP does.
2. Past Speed +6 and full HP, Sip's remaining job is the +1 AP that cancels its cost, and the score is still 39. Should that click start losing to the tax and to Pass?
3. The tell now reads AP before the spend, so the "is due" state can fire on the turn the tax is selected, and the rattle can still fire the turn before at 4/6. Is hearing both the warning you want? This round checked the call order and did not replay the log.
4. A faster Packet Skim erased the Utility tax for all 16 turns. The same Skim against Aggressive Cofi fizzles the beam every other turn and does not blank it. How hard should a prepared player be able to turn the boss move off, and is the alternating fizzle the intended answer to a wild Aggressive?
5. Typeless damaging moves inherit the attacker's primary type today. The stack proposal makes them neutral, with STAB left off. Cinislug's Pulse into Deghoul is 51.3 inherited and 42.8 neutral. Which rule should the estimator use?
6. Who is allowed to Pass? Defensive and Utility can, and only to bank a block above 50% that costs more than 2. Aggressive, which is the wild and the authoritative brain, cannot. Should a wild body be allowed one turn of bank, or only the boss?
7. Fox Feint is still Utility Kitsauto's opening click, tied at 39 with Packet Skim, with no attack coming in. Should a once-per-battle dodge be held until something is about to hit?
8. Defensive is unused on every checked-in encounter. Its new visible lines are Cinislug's accidental Pass into Cinder, and Blockwyrm's bank spent on Bulwark. Is that personality still part of the roster, and should the ladder be fixed before it is assigned anywhere?
9. On the Keyper kit the AI equips, Cipher Shot scores 44.6, Key Rotation 39.0, and Memory Leak 38.0. Leak is the signature. Is Cipher supposed to outrank it on the brain that plays this kit? Leak's proposed recharge does nothing until something clicks Leak.
10. The stack sim's greedy win rates are a different policy. Wild and authoritative battles will play the stack with Aggressive. Should those rates wait until a tactician exists, or are they meant as a human-versus-greedy picture only?
11. Should a 50% wall count as a greater block for the new bank? Ember is the wall that fails the gate. Blockade is the wall that passes it and is then not cast.

## Check script

`ai_decision_quality_v2_check.py` is self-contained. It takes the snapshot root and an optional JSON path.

```
python3 ai_decision_quality_v2_check.py <snapshot_root> [results.json]
```

Run against this snapshot:

```
SUMMARY 120 PASS / 0 FAIL
```

Exit code 0. The script writes the quiet lines, the stack lines, the oracles, and the fight traces next to itself when the JSON path is omitted. This run wrote `ai_decision_quality_v2_results.json` beside the script.
