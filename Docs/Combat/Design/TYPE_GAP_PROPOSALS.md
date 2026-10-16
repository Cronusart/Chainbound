# Type gap proposals

**Status: proposals only.** No move, passive, or AI change in this note is implemented. Open audit items stay open, including unimplemented rows (`optimize_aim`, `zip_step`, `thread_lock`'s duration, `mev_extract`'s steal wording, `all_in`'s cost, Jammed's accuracy body, Burned Tokens' heal, NodeRelay, CompressedData). This note does not edit `Docs/GameTheory/` or the type-profile spec.

Archetype text below is from the developer's current type-profile copy. That copy is the source for what each type should feel like. Meme stays out of chemistry, as that copy already says.

Kit facts are from `Docs/Specs/Combat/Data/Moves.csv` on this branch (206 rows). Among the 14 types, **only Node and Burned have zero Passive rows.** Gas, Block, Encrypted, Frozen, Forked, Mined, Staked, Compressed, Bot, Glitched, Shard, and Meme each have at least one. Section 1 stops there.

## Band these proposals sit in

Passives cost 0 AP (`CombatConstants.AP_COST_PASSIVE`). Turn regen is +2, cap 10 (`AP_PER_TURN`, `AP_CAP`). Turn-start and end-of-turn processing runs for active fighters (`CombatResolver.ProcessStartOfTurnForTeam` walks `activeIndices`). Bench recharge still ticks; other statuses pause (`COMBAT_Resolution.txt` §1.2). A passive on a benched fighter does not take that start-of-turn pass.

Stage +1 is 1.5× and +2 is 2.0× (`ChainlingInstance.StageMultiplier`). Active stage writes cap at ±2, passives at ±1 per change, the table still clamps at ±6.

Working comparisons used below:

| Passive | Rarity | Type | What runs |
|---|---|---|---|
| `turn_start_ap_gain` | Common | None | +1 AP at turn start, on top of the +2 regen. `GainAP` opcode. |
| `fee_collector` | Rare | Gas | At your turn start, if any foe has more AP than you, each foe loses 1. `DrainAP` plus `AnyFoeAPGreaterThanSelf`. |
| `sustain_regen` | Common | None | Heal 3% MaxHP at end of turn. |
| `firewall_passive` | Uncommon | Encrypted | Projectile hits, including spread, deal 25% less. `IncomingDamageMitigation`. |
| `immutable_ledger` | Uncommon | Block | The first hit each turn deals 20% less and grants +1 AP. Hardcoded body, listed as running. |
| `patience_passive` | Uncommon | Staked | End of turn Def +1, stage cap +6 on the opcode. The no-attack 5% heal is a hardcoded body on the working list. |
| `automated_pressure` | Common | Bot | End of turn, one random stat on a random foe drops by 1. `RandomStageChange`. |
| `conductive_counter` | Rare | Forked | Once a turn, return 15% of contact damage taken and gain +1 AP. Hardcoded body. |
| `streamlined_flow` | Rare | Compressed | Above 70% HP, move costs drop by 1. The Def −1 is written twice (stage column and `ChangeStage`); that double drop is an open item and is not a model to copy. |

`Drain` in `CombatApService` stops at 0. It does not wrap or delete a move. `Steal` drains the target, then gains that many on the user, then the cap of 10 deletes the overflow. `Gain` refuses the whole gain while `ThreadLock` is active, including the +2 regen.

### Typed-pool rule

A typed move or passive draws from a smaller equip pool than a typeless card of the same job. It has to be better than that typeless card. A typeless effect plus a condition that only makes it fire less often does not qualify. Partner Beacon was that shape: on its best turn it matched `turn_start_ap_gain` (+1 AP to the holder), and on turn 1 it usually did nothing, because the partner had no buff yet.

Two further tests:

- It works on turn 1, when the fighter's AP is only the +2 regen from an empty pool (`currentAP` defaults to 0, then `ProcessStartOfTurn` adds `AP_PER_TURN`). Or the slow opening clearly pays back, and the payback is written down in numbers.
- It rewards the type's profile with something a typeless card cannot do. Routing AP onto a buffed ally, or putting a burn on a foe you hit, qualifies. A second copy of "gain 1 AP" does not.

Sections 2 and 3 were drafted before this rule. They are not re-cut in this pass.

## 1. Passives for types that lack one

### Node

Profile: team coordinator. Routes AP, extends buffs, wide heals. Keywords: support-heavy, AP management, wide buffs, redirect. Chart role: net 0 starter, four wins (Frozen, Forked, Mined, Compressed).

Node already has the wide heal (`hub_heal`, 15% to all allies, 3 AP) and the AP move (`ap_router`, up to 2 AP between allies, hardcoded). Extend Protocol's "extend a buff" clause is an open unimplemented row. Relay does not implement that clause, and it does not apply the NodeRelay status (no producer, no resolver body).

Partner Beacon and Relay Dividend are withdrawn. Partner Beacon was a conditional copy of the typeless +1 AP passive. Relay Dividend paid +1 on any Support, including heals, which overlaps Group Hug and does not care whether a buff landed.

#### Relay (recommended)

| Field | Proposal |
|---|---|
| Name | Relay (`relay`) |
| Category | Passive, Node, Uncommon, self, AP 0, accuracy 0, power 0, recharge 0 |
| Rule | When this Chainling applies a buff to an ally, that ally gains +1 AP. Each ally can receive this point at most once per turn. |
| Numbers | +1 AP per buffed ally, cap 10. The Node's own pool does not gain this point. No heal and no extra stage. |

**What counts as applying a buff.** A positive stage (`stages > 0`) written onto an ally, or a status applied to an ally whose catalog row is a buff (`IsBuff`). The write has to succeed: a missed chance, a failed status apply, or a stage that does not change the value because of a cap does not count. Heals, cleanses, AP transfers, debuffs, and moving an existing buff (`relay_packet`) do not count. `hub_heal` does not fire Relay.

**Self-buffs.** If the stage or status target is this Chainling, Relay does not pay. `signal_redirect` raises the user's own Def (`MeleeDef:1:1.0:true` on a self-targeted stage) and does not fire Relay. `network_response` raises the user's Speed and does not fire it either.

**Timing.** The check runs once, after the action has finished every buff write for that action (`ApplyMoveStageChanges` and buff-status apply). Two positive stages in one action, such as Pack Rally's Atk +1 and Speed +1, are one application. The ally is marked for this turn before `Gain`, so a later buff in the same turn cannot pay them again.

**Multi-target.** Each living ally who received at least one buff from the action, and who has not already been paid by Relay this turn, gains +1 AP. `AddAliveTargets` includes every living active, including the user. The user is skipped. In the current 2-active roster that is the partner. A second active ally, if one were ever both buffed and not the user, would each get their own point.

**Fainted, benched, cap, Thread Lock.** A fainted ally is not a legal target (`AddUniqueTarget` requires `isAlive`) and is skipped. A benched ally is not in `activeIndices`, so a wide buff does not hit them and Relay does not pay them. `Gain` clamps at 10. If the ally is already at 10, or `ThreadLock` blocks the gain, the point is lost and the once-per-turn mark is still spent. Chemistry is not involved.

**Turn 1 and the payback.** Turn 1 AP is 2. The Node buff that actually writes ally stages today is `pack_rally` (Uncommon, AllAllies, Atk +1 and Speed +1, 3 AP). `network_sync` is 2 AP and would be a turn-1 buff, but its payload is empty (open row), so Relay does not pretend it fires. With the cards that run today, Relay does not pay on turn 1. The payback on that kit is the first rally, available on turn 2 if turn 1 spent 0 or 1 AP (turn 2 opens at 4 or 3). That action gives the partner Atk +1 (1.5×) and Speed +1 from the move, and +1 AP from Relay in the partner's pool. Link Pulse, drafted in the Node ally-buff section, is the Common 2 AP single-ally buff that would pay Relay on turn 1. It is not a row yet. A typeless +1 AP passive over those two turns has put +2 on the Node only. Relay has put the point on the partner, in the same action as the buff, so a partner who has not acted yet can spend it this turn. The typeless card cannot move a point onto the ally you just buffed.

**Side by side**

| | Relay | `turn_start_ap_gain` | `immutable_ledger` | `fee_collector` |
|---|---|---|---|---|
| Rarity | Uncommon, Node | Common, typeless | Uncommon, Block | Rare, Gas |
| Who is paid | The ally who received the buff | The holder, every turn | The holder, when hit | Foes lose 1, if they are ahead |
| Turn 1 | No working ally-buff costs 2 AP or less, so no pay until Link Pulse exists | +1 on top of regen | Pays if they are hit | Pays if a foe has more AP |
| What typeless cannot do | The point is created on the buffed ally, once per ally per turn | Pays only the holder | Pays the holder for being hit | Taxes foes |

Relay is not a smaller `turn_start_ap_gain`. It is a routing payment the typeless passive has no way to make. It stays under Patience Passive's Def +1 every end of turn, and it does not add Group Hug's "any stamped support pays every active."

**AI.** Utility scoring gives a support +15 and does not know Relay (`UtilityStatusAI`). Proposed scorer change, small: a move that writes a positive stage or a buff status onto an ally gains a bonus when that ally is under the AP cap and has not been paid by Relay this turn. The bonus should beat a self-buff that does not route, and it should lose to a believed KO. Aggressive keeps ignoring non-damage. Defensive already buffs when the team is above 55% HP; with Relay equipped it should prefer the ally-buff over a self-buff in that step.

**Primitives.** Engine. No opcode means "after this fighter buffs an ally, that ally gains 1 AP, once per ally per turn." The hook sits after stage and buff-status application in `CombatResolver`, calls `CombatApService.Gain`, and stores the paid ally ids for the turn. It is not a data-only row, and it must not reuse modifier `4.5-a`.

**Recommendation.** Relay. It is the Node passive.

### Burned

Profile: DoT pressure, wide coverage, wins long attrition lanes. Keywords: DoT, burn, coating setups. Statuses named on the profile: Burned Tokens, FUD. Burned Tokens' "enemy DoT heals you" body is an open item (`Statuses.csv` marks it neither a buff nor a debuff, and the heal does not run). These passives do not implement that heal and do not apply FUD. Rugged, the chemistry DoT Burned already prints on advantage hits, stays the matchup pressure: the catalog row is 0.0625, and `StatusProcessor` replaces that with `tick / 16` of MaxHP, then increments the tick, for a catalog duration of 3.

Cinder Idle is withdrawn. It was +1 AP at turn start only if you had been hit, which is `turn_start_ap_gain` with an extra gate. Scorch Echo is withdrawn. Draining 1 AP from whoever hit you is the Gas and Bot verb (`fee_collector`, section 3), it does not burn, and the old draft was Rare.

Both options below share one new status, **Kindled**. Only the equipped passive is authored. They do not stack.

**Kindled** (`StatusId.Kindled`, distinct from the passive id). General, Duration, default duration 1, debuff, not a buff. `hasEndOfTurnDamage` true. No stage string. `StatusProcessor` already deals `endOfTurnDamagePercent` of MaxHP for every end-of-turn damage status except the Rugged and FOMO overrides, using `Math.Max(1, (int)(EffectiveMaxHP * percent))`, which truncates. Kindled must stay off those two overrides, and off `IsNextActionStatus`. A duration that is not a next-action status ticks on the granting end step: the percent lands, then `Tick` drops duration 1 to 0 and the status expires. `HoldThroughGrantTurn` is only applied to next-action statuses today, so Kindled must not be added to that set or the first tick would slip to the following turn.

Encrypted Seal blocks a debuff apply, so a sealed foe does not gain Kindled. A swap-out before end of turn pauses every non-recharge status (`COMBAT_Resolution.txt` §1.2). The percent does not land while they are benched. It lands on a later end step after they return, then duration 1 expires. Being hit again while Kindled is still up does not refresh it and does not add a second tick. Refresh today sets `turnsRemaining = max(current, new)` (`StatusProcessor.ApplyStatus`); the hook below skips that branch entirely when Kindled is already present and not expired.

The Common median used for HP sketches is 132, Ghost excluded (`COMBAT_SYSTEMS_ANALYSIS.md`). The same note's neutral hit model is `power * 0.925 * accuracy`.

#### Kindling (recommended)

| Field | Proposal |
|---|---|
| Name | Kindling (`kindling`) |
| Category | Passive, Burned, Uncommon, self, AP 0, accuracy 0, power 0, recharge 0 |
| Rule | When this Chainling's damaging hit deals HP damage to a living foe, that foe gains Kindled (6% MaxHP at end of turn). Each foe can gain it at most once per turn. |
| Numbers | `endOfTurnDamagePercent` 0.06. On a 132 HP body that truncates to 7. No AP, no heal, no stage. |

**What counts.** The hit is from this Chainling, it is a damaging move, it reduced the foe's HP, and the foe is still alive afterward. A miss deals no HP and does not mark. A hit that KOs does not mark: there is no living body to tick. The user's own HP never counts. An ally never counts. Chemistry damage that is not this Chainling's hit does not count.

**Once per foe per turn.** If that foe already has Kindled and it is not expired, the hook does nothing. It does not refresh. `cinder_burst` (2–4 hits, 3 AP) therefore marks once, not once per hit. `ember_shower` (AllEnemies, power 38, accuracy 95, 3 AP) marks each foe who took HP and lived, each once.

**Turn 1.** Turn 1 AP is 2. `scorch_shot` and `flame_charge` are both power 64, accuracy 95, 2 AP, so either is legal after regen. A landed shot that leaves the foe alive applies Kindled during the action. If they are still active at end of turn, the 6% lands in that same end step and the status expires. The systems-analysis model puts a neutral power-64 accuracy-95 hit at `64 * 0.925 * 0.95 = 56.2` expected damage. Adding the truncated 7 on a 132 HP body is about 63 if the shot lands and they stay in through end of turn. `ember_shower` costs 3 and is not a turn-1 mark. A miss is the attack's 5% miss, not a passive that was off.

**Chemistry.** Rugged and Kindled are different statuses, so an advantage hit can carry both. The reaction list, the stack check, and the 1–3 turn clock are in the next section. A neutral or resisted hit still applies Kindled. The passive does not read Burned Tokens or FUD.

**Bench, faint, cap.** Fainted foes are skipped. A foe who swaps out before end of turn keeps Kindled paused and takes the 7 HP on a later end step after they come back, which is the counterplay. There is no AP to cap. `ThreadLock` does not interact.

**Side by side**

| | Kindling | `reactive_ping` | `sustain_regen` | `on_hit_momentum` | `patience_passive` |
|---|---|---|---|---|---|
| Rarity | Uncommon, Burned | Common, typeless | Common, typeless | Common, typeless | Uncommon, Staked |
| When | You deal HP to a living foe | A foe misses you | Every end of turn | 25% of your hits | Every end of turn |
| Payout | 6% MaxHP to that foe at end of turn (7 on 132 HP), once per foe per turn | 5% MaxHP to the attacker | 3% MaxHP to yourself (about 4 on 132 HP) | Atk +1, cap +3 | Def +1, cap +6 |
| Turn 1 | `scorch_shot` or `flame_charge` at 2 AP | Only if someone misses you | Pays even if you never attack | Only if you hit and win the 25% | Pays even if you never attack |
| What typeless cannot do | A burn on the foe you just hit, including neutral matchups, stacked with Rugged on advantage | Punishes their miss | Heals you | A stage on you | A stage on you |

Kindling is not a smaller `turn_start_ap_gain` and it is not `reactive_ping` with a different gate. The typeless on-hit passive raises your own Attack on a coin flip. Kindling spends the hit on the foe's HP again at end of turn, which no typeless passive does. It does not reach Patience Passive's Def stack or Firewall's 25% projectile cut. `flame_charge` can still grant its own +1 AP and +30% damage when the foe is at 0 AP; Kindling does not replace that rider and does not add AP of its own.

**AI.** Utility scoring starts a status move at +30 and prices damage as `DamagePerAP * 2` (`UtilityStatusAI`), so a Utility Burned will coat and mark instead of attacking, and Kindling will not fire. Proposed scorer change, small: while Kindling is equipped and a living foe does not already have Kindled, a damaging move that can hit them scores above a utility that does not apply Kindled. The bonus loses to a believed KO. Aggressive already picks by `DamagePerAP` and will kindle by attacking. Defensive still heals first.

**Primitives.** Engine. No opcode means "after this fighter's damaging hit deals HP, apply a status once per living foe per turn." The status body is the generic end-of-turn percent that already exists. The new pieces are `StatusId.Kindled`, a `Statuses.csv` row at 0.06, the catalog entry beside the other general statuses, and a hit-path hook in `CombatResolver` after HP is reduced. The hook applies duration 1 only when Kindled is absent. It does not go through the refresh branch. Putting Kindled on each Burned move row would copy the passive onto cards and would refresh; do not do that.

#### Sear Echo (alternative)

| Field | Proposal |
|---|---|
| Name | Sear Echo (`sear_echo`) |
| Category | Passive, Burned, Uncommon, self, AP 0, accuracy 0, power 0, recharge 0 |
| Rule | When this Chainling successfully applies a negative stage or a debuff status to a living foe, that foe gains Kindled at 8% MaxHP. Each foe at most once per turn. |
| Numbers | `endOfTurnDamagePercent` 0.08. On a 132 HP body that truncates to 10. Same duration-1 clock as Kindling. No AP and no heal. |

**What counts.** A stage write with `stages < 0` that actually changes the stage, or a status apply whose catalog row is a debuff (`IsDebuff`). The write has to succeed. A missed chance does not count. `sear_field` (AllEnemies, 2 AP, accuracy 0) applies Sear Field, which is a debuff and an Atk −1 for duration 1, so both living foes qualify. `deflation_mark` (1 AP) writes Atk −1 for certain; Burned Tokens itself is not a debuff, and the stage is what fires Sear Echo. `scorch_shot`'s Def −1 is 25%, and `ember_shower`'s Atk −1 is 25%, so a damage turn often does not qualify. `flame_charge` has no stage and no status, so it never fires Sear Echo. `cinder_burst`'s Atk −1 is 20% per hit; one landed stage marks the foe once.

**Turn 1.** It works. `sear_field` costs 2 and can mark both foes before anyone is hit. `deflation_mark` costs 1 and marks one. If they stay active, each takes the truncated 10 at that end step. A turn spent only on `scorch_shot` usually pays nothing, which is why this is the alternative.

**Same interactions as Kindling.** Once per foe per turn, no refresh, no mark on a fainted foe, bench pauses the tick, Encrypted Seal blocks the debuff apply, Rugged still stacks on advantage hits that also debuff, no Burned Tokens heal. Two stages in one action, or a debuff that also writes a stage, are one mark.

**Side by side.** 10 HP on 132 is larger than Kindling's 7 because fewer actions qualify. It is still one non-escalating tick, under a second Rugged tick (2/16). `automated_pressure` drops a stage and does not burn. `reactive_ping` pays 5% only when they miss you. Sear Echo puts the burn on the foe you coated, which those two cannot do. It does not reach Patience Passive's every-turn Def +1.

**AI.** Utility already starts status moves at +30, so it will cast `sear_field` and `deflation_mark` and play this passive without a new term. It will not chase the 25% riders. Aggressive scores non-damage at 0.1 and will not spend the turn coating.

**Primitives.** Same Kindled status and the same generic end-of-turn percent, authored at 0.08 instead of 0.06. The hook sits where a negative stage or a debuff status has just been applied by this Chainling, not on the HP-damage path. Skip when Kindled is already present. Engine, not a data-only row. Equip one of the two passives, not both; they share the status id so a second apply in the same turn is a no-op anyway.

**Recommendation.** Kindling. The profile is a burn on the hits that carry the attrition lane, and `scorch_shot` or `flame_charge` pays it on turn 1 even when the 25% rider misses. Sear Echo is the coating build: bigger tick, turn-1 spread through Sear Field, and it misses every Flame Charge turn. The chemistry section keeps Kindled at 6% on top of Rugged.

## Kindling and Burned chemistry

This section checks the Kindling proposal against the reactions a Burned damaging hit already triggers. It does not retune Rugged, and it does not implement Backlash. `COMBAT_Interaction_Matrix_v2.csv` is not the live matrix. The importer reads `COMBAT_Interaction_Matrix_v1.csv` (`Combat2DataImporter.ImportChemistryMatrixFromCSV`).

### Reactions a Burned hit can trigger

Chemistry runs only when a damaging hit lands (`COMBAT_Chemistry.txt` §2, `ChemistryResolver.Resolve` from the hit loop in `CombatResolver`). A miss does not apply a reaction. `sear_field` and `deflation_mark` are not damaging hits, so they do not. The relation is one number: a forward cell is +1, a reverse cell is −1, and the hit is Advantage ×1.2, Neutral ×1.0, or Resisted ×0.8 (`chemistryAdvantageFactor` 1.2 on `CombatResolverConfig_Default.asset`). The status applies only on Advantage, at most once per target per action. Later hits in the same action, including `cinder_burst`, still use the ×1.2 multiplier.

Burned's row in `COMBAT_Interaction_Matrix_v1.csv`:

| Defender | Cell | What the defender receives | Numbers |
|---|---|---|---|
| Gas, Frozen, Mined, Compressed, Shard | `Rugged @Foe` | Rugged, ChemistryOnly, debuff | Duration 3. End-of-turn damage is `counter / 16` of MaxHP, then the counter increments, cap 15 (`StatusProcessor`, `CombatConstants.RUGGED_TICK_CAP`). First apply sets the counter to 1 (`ChemistryResolver`). A later advantage hit sets `turnsRemaining = max(current, 3)` and does not raise the counter. Catalog percent 0.0625 is the stored column; the processor replaces it for this status id. |
| Staked | `Isolated @Foe` | Isolated, ChemistryOnly, debuff | Duration 2. No HP. Ally Support, healing, AP transfer, and ally stage buffs are rejected (`COMBAT_Statuses.txt`). Refresh sets the duration back to 2. |
| Bot | `Backlash Bot Dmg @Foe` | `BacklashType`, parameterized Bot, ChemistryOnly, debuff | Duration 3. Catalog end-of-turn percent is 0. No contact tick runs (`ChemistryResolver` has no Backlash branch). Open item E-09. The token is applied. It deals 0 HP. |
| Node, Burned, Meme | `-` | None | Neutral ×1.0. No status. |
| Block, Encrypted, Forked, Glitched | reverse cell only | None from Burned | Resisted ×0.8. The reverse cell is the other type's reaction when *they* hit Burned. This hit does not apply it. |

Five of the seven Advantage cells are the same Rugged tick. Isolated and Backlash are not damage. A Burned hit does not apply FOMO, FUD, or Burned Tokens through chemistry. FUD is an Encrypted move (`verification_lock`). Burned Tokens is `deflation_mark`, and its heal does not run.

### Can Kindled sit beside those statuses?

Yes. Statuses are a list. Refresh and the Rugged branch look up one `statusId`. Kindled is a new id, so it does not replace Rugged, Isolated, or Backlash, and their refresh does not clear it. End of turn walks the list and deals each status's own percent. Rugged's `tick / 16` override is `status.statusId == StatusId.Rugged` only, so Kindled keeps 0.06. No engine change is required for them to coexist.

Rules that already fall out of that:

- Same action: the first landed hit can apply Rugged and Kindled. Further hits in that action do not apply Rugged again (once per target per action) and do not apply Kindled again (once per foe per turn, no refresh).
- Later turns: Rugged is still up, so a new hit refreshes its duration and does not bump the counter. Kindled expired at the previous end step, so the new hit applies a fresh duration-1 burn.
- Encrypted Seal blocks both. Rugged is a debuff, and Kindled is proposed as a debuff.
- A bench before end of turn pauses both. Rugged's counter does not increment while benched. Kindled does not tick. That is the same counterplay as the Kindling spec.
- If an earlier status in the list KOs at end of turn, `StatusProcessor` returns and later ticks do not run. Rugged is applied during the hit, before damage. Kindled is applied after the hit leaves them alive, so Rugged is earlier in the list and ticks first.
- Backlash adds no HP today. Kindled is the only burn on a Bot body besides the ×1.2 hit. This note does not give Backlash a tick.

### Damage clock

Model, same shape as the systems note, with the miss pulled out of the hit because the riders only exist when the hit landed. Atk/Def = 1, no crit, no mitigation, STAB off (`enableStab: 0`), `PROGRESSION_DAMAGE_FACTOR` 1. The roll is uniform from 0.85 to 1 (`randomRollMin`, `randomRollMax`). A landed power-64 Advantage hit at the midpoint is `64 * 0.925 * 1.2 = 71.04`, and `DamageCalculator` floors, so 71. A max roll is `64 * 1.2 = 76.8`, floored to 76. The systems note's 67.5 is that landed hit times accuracy 0.95, which is the right number for turns-to-KO across misses and the wrong number for a hit that has already landed. DoT uses `Math.Max(1, (int)(MaxHP * percent))`, which truncates. Median MaxHP is 132 Common and 122 Rare, Ghost excluded (`COMBAT_SYSTEMS_ANALYSIS.md`).

One landed `scorch_shot` or `flame_charge`, then no further hit, target stays active for the whole Rugged duration. Kindled ticks once, on the granting end step. Rugged ticks three times: 1/16, 2/16, 3/16.

| Body | Landed hit | Kindled | Rugged 1/16, 2/16, 3/16 | Total | Left |
|---|---|---|---|---|---|
| Common 132, midpoint roll | 71 | 7 | 8 + 16 + 24 | 126 | 6 |
| Common 132, max roll | 76 | 7 | 8 + 16 + 24 | 131 | 1 |
| Rare 122, midpoint roll | 71 | 7 | 7 + 15 + 22 | 122 | 0 |
| Rare 122, max roll | 76 | 7 | 7 + 15 + 22 | 127 | 0, by 5 |
| Rare 122, midpoint, no Kindling | 71 | 0 | 7 + 15 + 22 | 115 | 7 |

The flagged line is the Rare row. One landed Advantage hit, plus staying in for three end steps, is a KO on 122 HP at the midpoint roll. Without Kindling that body has 7 left. The Common median survives the same line with 6 left, or 1 on a max roll. Rugged is 44 of the 51 DoT HP on the Rare body. Kindling is the last 7.

Keeping the attack up does not get more dangerous because of Kindling. Two landed Advantage hits are 71 + 71 = 142 before any end step. Both medians die on the second hit. The accuracy-weighted figure from the systems note, 67.5 a turn, is still a 2-turn KO on 132 (135) and on 122. Kindling's 7 on the first land does not remove a turn. The turn-2 and turn-3 Rugged ticks in the table above happen only when the second hit does not.

`flame_charge`'s +30% (`1.3` while the foe has 0 AP) multiplies the hit on top of ×1.2. That is a conditional spike on the move, not a Kindling number. A foe at 0 AP is already outside the +2 regen, so it is not the clock above.

Other typed passives on the same power-64 fight, Common 132:

| Passive | What it changes | Turns to KO on Advantage |
|---|---|---|
| None | ×1.2 only | 2 |
| Kindling | +7 HP on each landed damaging turn, and Rugged is already there | 2 |
| `on_hit_momentum` | 25% chance of Atk +1. A proc makes the next hit 1.5×, which can pull a *neutral* KO from 3 hits to 2 | Already 2 on Advantage |
| `conductive_counter` | 15% of one contact hit back, plus 1 AP. About 11 off a 71 hit | Does not speed your KO |
| `patience_passive` | Def +1 at end of turn. After one tick, incoming damage is divided by 1.5 | Lengthens the fight |
| `firewall_passive` | Projectile hits deal 25% less | Lengthens the fight |
| `immutable_ledger` | The first hit each turn deals 20% less | Lengthens the fight |
| `sustain_regen` | About 4 HP to the holder | Does not outheal a 71 hit |

Kindling is a 7 HP chip in the band of Conductive Counter's reflect. It is smaller than one stage of Patience. The large rider on this lane is Rugged, which already ships on five cells.

### Guardrails, and the recommendation

Three gates were considered.

- **Skip Kindled when a chemistry burn is already on the target.** This does not touch the flagged line. The first hit applies Rugged and Kindled together, so Rugged was not already there. Later hits are the ones this gate blocks, and those are the hits that already KO on contact.
- **Cut Kindled to about 3% while Rugged is up, including the hit that just applied it.** On the Rare cook that is `floor(122 * 0.03) = 3` instead of 7, and the body lives with 4. It keeps a small burn on the win cells. It is a special case for 4 HP.
- **Cap every damage-over-time tick in the end step, or skip Kindled's tick when Rugged also ticks.** A shared cap would also clip Rugged, Leaked, and FOMO, which this note is not changing. Skipping only Kindled's tick on an end step that already dealt chemistry damage would remove the 7 from the cook and leave the neutral, Isolated, and Backlash burns alone. The cost is that Kindling goes silent on the five Rugged matchups, which are the profile's attrition cells.

**Recommendation: keep the 6% stack. Add none of these gates.** Two landed hits already KO both medians without Kindling, so the stack does not shorten the fight the attacker is trying to play. The one-hit KO is real on the Rare median, and it is 44 HP of Rugged plus 7 of Kindling, and only if that body stays active through three end steps. Benching pauses both ticks, which is the counterplay already in the Kindling spec. A 15% heal is about 18 HP on 122 and erases the last 7 with room to spare. Turning Kindling off on Rugged cells would make the passive disappear on five of Burned's seven wins.

Approved: Kindling stays at 6% and keeps the stack. The optional trim, if a later pass wants it, is the shared end step: when Rugged deals HP at end of turn, skip Kindled's tick that same step. Do not use "already on the target," and do not cap Rugged.

### Node ally buffs at 1–2 AP

No working Common move at 1 or 2 AP buffs an ally. Relay's turn-1 gap is the whole Node kit, not one missing card that was overlooked. Rows are `Moves.csv`. Empty payloads are the open list in `UNIMPLEMENTED_MOVE_EFFECTS.md`.

| Move | Rarity | AP | Ally buff? |
|---|---|---|---|
| `extend_protocol` | Common | 1 | No. The text extends one ally buff by 1 turn. The extend does not run. Recharge 3 does. |
| `network_response` | Common | 1 | No. Self Speed +1 after an enemy miss. |
| `relay_packet` | Common | 2 | No. Moves an existing General buff or debuff. Relay does not count a move. |
| `ap_router` | Common | 2 | No. Moves up to 2 AP. |
| `signal_redirect` | Common | 2 | No. Def +1 is `targetsSelf` true. |
| `network_sync` | Common | 2 | No. The text raises every ally's lowest stage by 1. The payload is empty. |
| `coordinated_shot` | Common | 2 | No. A hit that reads a buffed partner. It does not write a buff. |
| `hub_heal` | Common | 3 | No. 15% heal. Also above 2 AP. |
| `overflow_reboot` | Rare | 2 | No. Heals allies 10% if a foe faints. A heal is not a buff. |
| `hammer_sync` | Rare | 2 | No. A hit. |
| `pack_rally` | Uncommon | 3 | Yes. Atk +1 and Speed +1 on living allies. This is the only working ally buff. |

`extend_protocol` and `network_sync` stay on the open list. The draft below is a new id. It does not fill either payload.

### Turn-1 ally buff

Node's profile is the coordinator: route AP, extend buffs, heal wide. The wide heal is `hub_heal` (3 AP). The wide stage buff is `pack_rally` (Uncommon, 3 AP). Neither fits in turn 1's 2 AP, so Relay cannot pay on the first turn with a card that runs. The typeless card of this job is `buff_ally`: Common, Support, SingleAlly, 2 AP, accuracy 0, power 0, recharge 0, Atk +1 and Def +1, no duration. Those stages are permanent move-column writes, the same shape as Pack Rally. `rally_cry` is Common, AllAllies, 3 AP, and its working payload is Speed +1. The turn-end +1 AP in that tooltip is not in `UtilityEffects`. Self buffs at 1 AP (`power_surge` Atk +1, `defensive_stance` Def +1, `speed_boost` Speed +1) do not touch an ally, so they cannot fire Relay.

Stage +1 is 1.5×. Speed does not enter the damage formula. Def +1 divides incoming damage by 1.5. The table still clamps at ±6. A write that does not change a capped stage does not count.

#### Link Pulse (recommended)

| Field | Proposal |
|---|---|
| Name | Link Pulse (`link_pulse`) |
| Rarity | Common, Node |
| Category | Support |
| AP | 2 |
| Accuracy | 0 |
| Power | 0 |
| Target | SingleAlly |
| Recharge | 0 |
| Duration | None. Atk +1, Def +1, and Speed +1 stick, same as `buff_ally` and `pack_rally`. |
| Effect | The chosen living ally gains Atk +1, Def +1, and Speed +1. Column: `MeleeAtk:1:1.0:false\|MeleeDef:1:1.0:false\|Speed:1:1.0:false`. |

**Why it beats the typeless card.** Same rarity band, same 2 AP, same single ally, same Atk +1 and Def +1, plus Speed +1. That is `buff_ally` with the coordinator's tempo stat added. It is not a second damage multiplier. It is castable on turn 1, because 2 AP is the whole regen.

**Against Pack Rally.** Pack Rally is Uncommon, 3 AP, AllAllies, Atk +1 and Speed +1 on every living active, including the user. On the 2-active roster that is four stage writes and both attackers at 1.5×. It does not fit in turn 1. Link Pulse is one body, three stages, 2 AP, and it does not raise the Node's own Attack unless the Node targets themself. The Uncommon stays the team damage button. The Common is the turn you can actually take at the start. Casting Link Pulse every turn spends the +2 regen, so Pack Rally still waits for a turn that opens at 3 or more.

**Relay.** Three positive stages on the ally, all `targetsSelf` false, are one application. Relay pays that ally +1 AP once this turn, then marks them. The move itself grants no AP, so the passive is not a second copy of a point the card already gave. With no explicit target, `ResolveSingleSelectedTarget` uses `FindAlly`, the other living active, so the default cast is the partner. An explicit self-cast writes the stages on the Node and Relay does not pay. A fainted ally is not added (`isAlive`). A benched ally is not in `activeIndices`. If every stage is already at the ±6 cap, nothing landed and Relay does not pay. If one of the three lands, it pays once. Isolated blocks ally support, including these stages (`IsAllySupportBlockedByIsolation`); a blocked write does not pay Relay. Self-support is not blocked.

Turn 1: the Node spends 2. The partner, if they have not acted, has their +2 regen plus this +1, so 3 AP, and can spend a 3 AP move in the same turn. If they have already acted, the point waits in their pool, cap 10. Thread Lock still deletes the gain and the mark is still spent.

**Side by side**

| | Link Pulse | `buff_ally` | `pack_rally` | `rally_cry` | `power_surge` |
|---|---|---|---|---|---|
| Rarity | Common, Node | Common, typeless | Uncommon, Node | Common, typeless | Common, typeless |
| AP | 2 | 2 | 3 | 3 | 1 |
| Who | One ally | One ally | All allies, including the user | All allies | Self |
| Stages | Atk +1, Def +1, Speed +1 | Atk +1, Def +1 | Atk +1, Speed +1 | Speed +1 | Atk +1 |
| Turn 1 | Yes | Yes | No | No | Yes, and it does not buff an ally |
| Relay | +1 AP on that ally, once | Would pay, on a Node that had it | Pays the partner once; the user's own stages do not pay | Would pay each other ally once | Does not pay |

**AI.** A Support scores from 15, then minus AP (`UtilityStatusAI`), so this is about 13 before any Relay term. A status utility still starts at +30, so a Utility Node will not cast Link Pulse unless the Relay scorer bonus from the passive section is in. That bonus should beat a self-buff and lose to a believed KO. Aggressive still ignores non-damage. Defensive, with the team above 55% HP, should be able to pick this ally buff.

**Primitives.** Data-only. The stage column and `ApplyMoveStageChanges` already do this for `buff_ally` and `pack_rally`. No new status and no new opcode. Relay stays the engine hook it already is. Do not put a `GainAP` on this row. That would pay the ally twice when Relay is equipped.

#### Open Line (alternative)

| Field | Proposal |
|---|---|
| Name | Open Line (`open_line`) |
| Rarity | Common, Node |
| Category | Support |
| AP | 1 |
| Accuracy | 0 |
| Power | 0 |
| Target | SingleAlly |
| Recharge | 0 |
| Duration | None. Atk +1 and Def +1 stick. |
| Effect | The chosen living ally gains Atk +1 and Def +1. Column: `MeleeAtk:1:1.0:false\|MeleeDef:1:1.0:false`. |

This is `buff_ally` at 1 AP instead of 2. Same stages, half the cost, so it is strictly cheaper than the typeless card, and turn 1 leaves 1 AP on the Node (turn 2 then opens at 3). Relay pays the ally +1 once, on the same rules as Link Pulse, including the self-cast miss and the Isolated block. It does not add Speed, so it is the typeless package with a better cost, not the Node tempo package. Pack Rally remains the only Atk-and-Speed button, and it still costs 3.

Data-only, same stage column. No `GainAP` on the row.

**Recommendation.** Link Pulse. It is the one that is better than `buff_ally` on the same 2 AP and still reads as Node, and it is the turn-1 Relay trigger. Open Line is the cut to take if three stages at Common is too close to Pack Rally.

#### Do not reuse the empty rows

`extend_protocol` should stay `extend_protocol`. It is Common, SingleAlly, 1 AP, recharge 3. The recharge runs and skips the next 2 turns. The extend does not. An extend is not a new positive stage and not a new buff status, so Relay would not pay it even if the clause were built. Turn 1 often has no buff to extend, which is the dead first turn this rule already rejected. Recharge 3 also means it cannot be the every-turn Relay button. Replacing that tooltip with Link Pulse would throw away the extend job and the recharge that was just clarified. Leave the open row open.

`network_sync` should stay `network_sync`. It is Common, AllAllies, 2 AP, and the text raises every ally's lowest stage by 1. That is a wide one-stage bump, not a single-ally three-stage buff. The payload is empty, and there is no opcode for "lowest stage," so filling the text is engine work. One stage on the partner is less than `buff_ally`'s two stages, so the written card does not beat the typeless buff. Putting Link Pulse's column on that id would change the target and the tooltip and close an open row by renaming it. Leave it open.

The new ids are `link_pulse` and `open_line`. Neither is in `Moves.csv`.

## 2. Compressed support and utility

Profile: low-density soft wall. Precision efficiency. Consistent drain and evasion. Keywords: AP efficiency, Jam, evasion, precision. Three chart wins: Block, Encrypted, Frozen. Key status on the profile is CompressedData, which has no move producer; these cards do not apply it. `optimize_aim` (Acc +2, empty payload) and `zip_step` (Eva +2 text, only Def −1 runs) stay on the open list. Every id below is new.

Compressed already has the efficiency passive `streamlined_flow` (−1 AP cost above 70% HP). New cards stay small: +1 stage, 1 AP of drain, a heal under the 15% support line, a wall under `barrier_wall`. Bot, in section 3, owns denial that skips regen. Compressed's "drain" here is a precision nick, not that lock.

Accuracy 0 on a utility is the existing "does not roll a hit" pattern (`DamageCalculator` treats `baseAccuracy <= 0` as a hit).

| # | Id | Name | Rarity | Cat. | AP | Pow | Acc | Recharge | Effect | Role | Primitive |
|---|---|---|---|---|---|---|---|---|---|---|---|
| C1 | `brace_yield` | Brace Yield | Common | Support | 2 | 0 | 0 | 0 | Heal one ally for 8% MaxHP if their Def stage is at least +1. Otherwise the heal is 0. | Soft-wall care. 8% is under `heal_ally` (15%, 2 AP) and `stake_heal` (25%, 2 AP). | Data-only. `HealPercentIfStageAtLeast`. The executor exists and no CSV row uses it. |
| C2 | `folded_step` | Folded Step | Common | StatusUtility | 1 | 0 | 0 | 0 | Your Evasion +1. No Def drop. | The evasion keyword. +1 is inside the active stage-change cap of 2. The opcode's own cap should be authored at 6, which is the table clamp. | Data-only. `ChangeStage` on `CombatStageStat.Evasion`. Not a rewrite of `zip_step`. |
| C3 | `packet_jam` | Packet Jam | Common | StatusUtility | 1 | 0 | 0 | 0 | One foe's Accuracy −1. | The Jam keyword without the Jammed status, whose accuracy body is still open. | Data-only. `ChangeStage` on Accuracy. |
| C4 | `checksum_line` | Checksum Line | Uncommon | StatusUtility | 1 | 0 | 0 | 0 | Your Accuracy +1. | Precision setup. +1, not the +2 `optimize_aim` promises and does not perform. | Data-only. `ChangeStage` on Accuracy. New id. |
| C5 | `soft_fold` | Soft Fold | Uncommon | Support | 2 | 0 | 0 | 0 | One ally gets 15% mitigation, turn budget 2, hit budget 2. | Soft wall. `barrier_wall` is 25%, the same budgets, 3 AP, typeless. This is a smaller, cheaper, typed version. Hit budget 2 so it actually mitigates (`AddMitigationSource` returns immediately at hit budget 0). | Data-only. `MitigationWindow:15%:2:2`. Does not edit Ice Wall, Ash Guard, or Compound Guard. |
| C6 | `outspeed_levy` | Outspeed Levy | Rare | StatusUtility | 1 | 0 | 0 | 0 | If your Speed is higher than the target's, that foe loses 1 AP. Floor 0. If you are not faster, no drain. | Consistent drain with a precision gate. One point, not a regen skip. | Data-only. `DrainAP:1` and `SpeedHigherThanOther`. The condition gates resolution the way other resolve conditions do. Selection can still spend the 1 AP when you are slower, unless a later eligibility check hides it. Say that on the card. |
| C7 | `compact_reserve` | Compact Reserve | Common | StatusUtility | 1 | 0 | 0 | 0 | Pay 1, then bank up to 3 of your remaining AP. Next turn that bank returns with no bonus. | AP efficiency. `ap_reserve` banks up to 5 and adds +1 (`BankAPUntilNextTurn:5:1`). This cap is 3 and the bonus is 0. | Data-only. `BankAPUntilNextTurn`. The opcode drains the banked amount now and releases `banked + bonus` at the owner's next turn start. |

No engine work in C1–C7. C6 is the one that can spend AP on a whiff if the speed check is only applied at resolution.

## 3. Bot AP denial

Profile: automated pressure, AP denial, multi-hit attrition. Keywords: automation, AP denial, multi-thread, attrition. Bot already has `automated_pressure` (random stage drop), `multi_thread_shot` (2–3 hits, 10% chance of +1 AP each), `automation_burst` (spread), and `self_repair_protocol` (15% self heal, 1 AP). It has no `DrainAP` or `StealAP` row.

Two related cards already exist and are not rewritten here:

- `mev_extract` applies `MEVExtracted` for 2 turns. The catalog end-of-turn AP change is −1, and `StatusProcessor` calls `Drain`, not `Steal`. The tooltip says steal. Open item.
- `thread_lock` applies `ThreadLock` at duration 1. `ThreadLock` is not in `StatusInstance.IsNextActionStatus`, so it does not skip the granting end-of-turn tick. A duration of 1 expires on that tick, before the next start-of-turn regen. `Gain` does block regen while the status is still up. This draft does not change `thread_lock`.

### Primitives that exist today

| Primitive | Behavior | In CSV now |
|---|---|---|
| `DrainAP` | Target loses that many AP, floor 0. The user does not gain it. | `fee_collector` (foes), `defensive_reflex` (self at end of turn). |
| `StealAP` | `Drain` the target, then `Gain` that many on the user. Cap 10 drops the rest. | No row. Executor and importer accept it. |
| `NextMoveAPSurcharge` | The target's next move costs +1 (or the authored amount). | `verification_lock`, `glitch_feedback`. |
| `ThreadLock` | While active, `Gain` returns 0, including +2 regen. Spending AP still works. | `thread_lock`, duration 1, clock note above. |
| `WallHitRetaliation` | The attacker loses AP when they hit the paired wall. | `ember_wall` (Burned), 1 AP. |
| Chemistry `Drained` | End of turn −1 AP for a catalog duration of 3, only when the hit is Advantage. | Matrix cells, not a Bot move. |

There is no diminishing-returns counter on drain. A second `DrainAP` in the same turn removes another full point. The guardrail is in what these cards are allowed to say, plus one optional engine clamp.

### Guardrails (every card below)

- One card removes at most 1 AP from a given target, or it blocks one regen, not both.
- Nothing drains 2 or more in a single effect.
- Floor stays 0, which `Drain` already enforces. A move that wants to leave them at least 1 AP says so and only fires when they have 2 or more.
- The regen-block card uses duration 2, so it is still active at the next start of turn, and `rechargeTurns` 2, so the user skips one upcoming turn of that move (`rechargeTurns` 2 skips one upcoming turn; the granting end tick counts). They cannot reapply it on the very next turn.
- Stored AP can still be spent under `ThreadLock`. Denial cuts income, not the ability to pay a move you already saved for.
- Swapping inherits slot AP, so a drained pool follows the incoming fighter. A `ThreadLock` on a body that then benches pauses with the other non-recharge statuses and is still there when they return. The card text should say that.
- Optional engine clamp, not required for the data-only cards: the second `Drain` on the same target in the same turn removes 0. That is a few lines in `CombatApService` plus a turn flag. Recommend it if two of these cards can land in one turn (a damaging nick plus a utility tax).

### How the AI should use them

Utility personality starts a status move at +30 (`UtilityStatusAI`), so it will tax forever unless the scorer learns three exceptions. Proposals, small scorer change, no new action type:

- If the target's AP is 0, a drain or steal scores 0.
- If the target already has `ThreadLock`, the regen-block scores 0.
- If your Speed is not higher, Outspeed-style gates score 0. For Bot's speed-free cards this does not apply.
- A believed KO still outranks a tax (the lethal check in the systems-analysis note). Aggressive keeps ignoring non-damage (score 0.1). Defensive does not need these cards to heal.

### The cards

| # | Id | Name | Rarity | Cat. | AP | Pow | Acc | Recharge | Effect | Counterplay | Primitive |
|---|---|---|---|---|---|---|---|---|---|---|---|
| D1 | `ping_tax` | Ping Tax | Common | StatusUtility | 1 | 0 | 0 | 0 | One foe loses 1 AP. | Regen is still +2, so this is a nick. At 0 already, the drain removes nothing. | Data-only. `DrainAP:1` on `SingleEnemy`. |
| D2 | `skim_script` | Skim Script | Uncommon | StatusUtility | 2 | 0 | 0 | 0 | Steal 1 AP from one foe. | You paid 2 to move 1. At your cap of 10 the gained point is deleted. At their 0, you gain 0. | Data-only. `StealAP:1`. First row for that opcode. |
| D3 | `halt_script` | Halt Script | Rare | StatusUtility | 2 | 0 | 0 | 2 | Apply `ThreadLock` for a duration of 2. No drain on this card. | One upcoming regen is blocked. They can still spend saved AP. Your move then skips one upcoming turn, so you cannot refresh the lock immediately. Bench pauses the status. | Data-only status apply on a new id. Uses the existing `ThreadLock` body. Does not edit `thread_lock`. Duration 2 is the whole trick; duration 1 would fall off before that regen. |
| D4 | `surcharge_bot` | Surcharge Bot | Common | StatusUtility | 1 | 0 | 0 | 0 | The foe's next move costs +1 AP. | They can Pass, or pay the extra from the +2 regen. It does not remove AP they already hold. | Data-only. `NextMoveAPSurcharge:1`, same opcode as `verification_lock`. |
| D5 | `nick_shot` | Nick Shot | Common | DamagingRanged | 2 | 48 | 95 | 0 | One hit, then that foe loses 1 AP. A miss does not drain. | Index sketch `48 * 0.95 = 45.6`, under a typed Common's 60.80, because the tax is the rider. Floor 0. | Data-only if the utility timing matches `multi_thread_shot` (`GainAP` on a damaging row, after a confirmed hit). `DrainAP:1` at that same timing. |
| D6 | `swarm_tariff` | Swarm Tariff | Uncommon | StatusUtility | 2 | 0 | 0 | 2 | Each active foe loses 1 AP. | Both bodies lose a point, then you cannot tariff again next turn. It does not block regen. A foe at 0 loses nothing. | Data-only. `DrainAP:1` on `AllEnemies`, plus `rechargeTurns` 2. |
| D7 | `loaded_ping` | Loaded Ping | Rare | StatusUtility | 1 | 0 | 0 | 0 | One foe loses 1 AP, and only if they have at least 2 AP. They are left at 1 or more. | The last point is safe from this card. A foe at 0 or 1 is a whiff if the check is at resolution. | Data-only effect: `DrainAP:1` and `APAtLeast` 2 on the target. Same selection-whiff note as C6. |

D1–D7 need no new status and no change to `Drain`'s floor. The AI exceptions above are the engine piece, and they are shared. The optional same-turn second-drain clamp is separate and smaller than a new action.

## 4. Meme

Meme stays out of chemistry. The developer's type-profile copy marks it excluded from the 13-type balance pass until a transmutation kit exists, with ChemAdv and ChemDis still TBD. The live matrix row and column are empty (`COMBAT_Interaction_Matrix_v1.csv`). No proposal in this note adds a Meme cell, a Meme passive, or a Meme move. `all_in`'s missing cost remains an open row.

## Recommended picks

| Gap | Pick | Why |
|---|---|---|
| Node passive | **Relay**, not Partner Beacon | When this Chainling buffs an ally, that ally gains +1 AP, once per ally per turn. The turn-1 card that would fire it is **Link Pulse** (Common, 2 AP, one ally, Atk +1, Def +1, Speed +1). Open Line is the 1 AP alternative. Do not rewrite `extend_protocol` or `network_sync`. |
| Burned passive | **Kindling**, not Cinder Idle | Approved at 6% MaxHP, stacked with Rugged. Optional trim, if wanted later: skip Kindled's tick on an end step where Rugged also deals HP. Sear Echo is the coating alternative. |
| Other types missing a passive | None | The other 12 types already have a Passive row. |
| Compressed | All seven (C1–C7), data-only | Soft heal, Evasion +1, Accuracy −1, Accuracy +1, a 15% wall with a real hit budget, a speed-gated 1 AP drain, and a no-bonus bank of 3. Drafted before the typed-pool rule; not re-cut in this pass. |
| Bot denial | Ship the data-only set D1–D7 with the AI exceptions | Drain 1, steal 1, one skipped regen on a recharging duration-2 Thread Lock, a +1 surcharge, a weaker hit that drains 1, an every-other-turn team tax, and a drain that leaves at least 1. No card drains and blocks regen together. Drafted before the typed-pool rule; not re-cut in this pass. |
| Meme chemistry | Leave it | Already out of the pass by design. |
