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

## 1. Passives for types that lack one

### Node

Profile: team coordinator. Routes AP, extends buffs, wide heals. Keywords: support-heavy, AP management, wide buffs, redirect. Chart role: net 0 starter, four wins (Frozen, Forked, Mined, Compressed).

Node already has the wide heal (`hub_heal`, 15% to all allies, 3 AP) and the AP move (`ap_router`, up to 2 AP between allies, hardcoded). Extend Protocol's "extend a buff" clause is an open unimplemented row. These passives do not implement that clause, and they do not apply NodeRelay (no producer, no resolver body).

#### Option N1 — Partner Beacon (recommended)

| Field | Proposal |
|---|---|
| Name | Partner Beacon (`partner_beacon`) |
| Category | Passive, Node, Uncommon, self, AP 0, accuracy 0, power 0, recharge 0 |
| Rule | At the start of your turn, after the +2 regen, if at least one other active ally has a buff, you gain +1 AP. |
| Trigger | `TurnStart`, condition `BuffedPartnerCountAtLeast` 1, opcode `GainAP:1` on self. Same column shape as `fee_collector`. |
| Numbers | +1 AP. Cap 10 still applies. No stage, no heal, no status. |

**Chemistry, stages, AP, bench.** No chemistry and no stages. The point is the same size as `turn_start_ap_gain`, gated on a buffed partner, so a solo Node or a Node whose partner is unbuffed gets nothing. Turn start does not run on the bench, and a benched partner is not an active ally, so parking the buffed partner turns the passive off. Recharge on the Node's moves is unaffected.

**Side by side**

| | Partner Beacon | `turn_start_ap_gain` | `fee_collector` | `low_ap_surge` |
|---|---|---|---|---|
| When | Turn start, partner buffed | Turn start, always | Turn start, a foe is ahead on AP | Turn start, your AP is at most 1 after regen |
| Payout | +1 AP to you | +1 AP to you | −1 AP on each foe | Speed +1 |
| Floor | Can whiff the whole turn | Never whiffs | Whiffs when you are ahead or tied | Whiffs when you are at 2+ AP |

It is strictly inside the Common "plus one AP" band, and weaker than that Common because of the partner gate. It does not reach Patience Passive's Def stack or Firewall's 25% projectile cut.

**Primitives.** Data-only: `GainAP`, `TurnStart`, `BuffedPartnerCountAtLeast`. No new status.

#### Option N2 — Relay Dividend

| Field | Proposal |
|---|---|
| Name | Relay Dividend (`relay_dividend`) |
| Category | Passive, Node, Rare, self, AP 0 |
| Rule | After you resolve a Support move, the other living active ally gains +1 AP. Your own AP is unchanged by the passive. |
| Trigger | `OnAction`, then a category check that the action was Support. |
| Numbers | +1 AP to one ally. Cap 10. No copy of the support's heal or stages. |

**Chemistry, stages, AP, bench.** The ally must be active. A benched partner gets nothing, matching Group Hug. Group Hug (`4.5-a`) already grants +1 AP to each active ally after a *stamped* support. This passive would do a smaller version on every Node support, with no stamp. Support costs are already 2–4, so the turn is still spent.

**Side by side.** Against `turn_start_ap_gain` it pays less often (only on support turns) but pays the partner, which is the coordinator fantasy. Against `hub_heal` (15% to all allies for 3 AP) it adds a point of AP on top of a heal the player already paid for. Against Group Hug it is the same resource without the Epic stamp. That overlap is why it is not the recommendation.

**Primitives.** Engine. `OnAction` exists. The utility runner does not currently test "the action's category was Support" before `GainAP`. A small branch next to the Group Hug check would do it. Do not reuse modifier `4.5-a`.

**Recommendation.** N1. It is inside the existing AP-passive band, it uses the coordinator condition the profile already points at (a buffed partner), and it needs no new code. N2 can wait until Group Hug's stamp and a passive routing trigger are meant to be different buttons.

### Burned

Profile: DoT pressure, wide coverage, wins long attrition lanes. Keywords: DoT, burn, coating setups. Statuses named on the profile: Burned Tokens, FUD. Burned Tokens' "enemy DoT heals you" body is an open item. This passive does not depend on it. Rugged, the chemistry DoT Burned already prints on five cells, remains the matchup pressure: `counter / 16` of MaxHP at end of turn for a catalog duration of 3 (`StatusProcessor`, `Statuses.csv`).

#### Option B1 — Cinder Idle (recommended)

| Field | Proposal |
|---|---|
| Name | Cinder Idle (`cinder_idle`) |
| Category | Passive, Burned, Uncommon, self, AP 0, accuracy 0, power 0, recharge 0 |
| Rule | At the start of your turn, after regen, if you were damaged last turn, gain +1 AP. |
| Trigger | `TurnStart`, condition `WasDamagedLastTurn`, opcode `GainAP:1` on self. |
| Numbers | +1 AP. No damage, no stage, no heal. |

**Chemistry, stages, AP, bench.** It does not add a second DoT on top of Rugged, and it does not heal, so it does not quietly implement Burned Tokens. A Burned fighter who is not being hit gets no extra AP and has to spend the +2 regen like anyone else. Turn start does not run while benched. The "damaged last turn" flag is on the fighter; if a swap-in still carries a stale flag, the first turn back could pay the point once. That is a reason to check the flag when the passive is built, not a change to bench rules here.

**Side by side**

| | Cinder Idle | `turn_start_ap_gain` | `flame_charge` | `sustain_regen` |
|---|---|---|---|---|
| When | Turn start, you were hit last turn | Turn start, always | You spend 2 AP on a power-64 hit, and the foe has 0 AP | End of turn, always |
| Payout | +1 AP | +1 AP | +30% damage and +1 AP | 3% MaxHP (about 4 HP on a 132 HP Common) |
| Attrition fit | Pays for staying in a lane that hits you | Pays for existing | Pays for finding a foe at 0 AP | A small heal, any type |

`flame_charge` is the active Burned card that already pays +1 AP, and it also boosts a 64-power hit. The passive is only the point, and only after you have been hit. That is under both the Common AP passive and the Uncommon attack.

**Primitives.** Data-only: `GainAP`, `TurnStart`, `WasDamagedLastTurn`. No new status.

#### Option B2 — Scorch Echo

| Field | Proposal |
|---|---|
| Name | Scorch Echo (`scorch_echo`) |
| Category | Passive, Burned, Rare, self, AP 0 |
| Rule | The first time you take damage each turn, the attacker loses 1 AP. Further hits that turn do not drain again. |
| Trigger | `OnDamageTaken`, once per turn, `DrainAP:1` on the attacker. |
| Numbers | −1 AP on the attacker, floor 0. No reflect, no heal, no stage. |

**Chemistry, stages, AP, bench.** Chemistry DoTs that are not a "hit" should not trip it, or Rugged would tax AP as well as HP. The drain follows the attacker, not the slot: if they swap afterward, the incoming fighter inherits the slot's AP, including the missing point (`CombatResolver` swap path). While you are benched the passive does not see hits.

**Side by side.** `conductive_counter` returns 15% of contact damage and gives *you* +1 AP, once a turn. `ember_wall` makes the attacker lose 1 AP when they hit that barrier, up to its hit budget. `immutable_ledger` reduces the first hit by 20% and gives you +1 AP. Scorch Echo is the smaller one: no damage cut, no point for you, one point off the attacker. That is in band only if the once-per-turn cap is real. Without the cap, a multi-hit would drain once per hit and walk someone to 0 in a single action.

**Primitives.** Engine. `OnDamageTaken` exists, and `Drain` exists, but a new passive has to receive the attacker as the drain target and ignore later hits that turn. `immutable_ledger`'s "first hit" behavior is hardcoded, not a shared opcode. Do not fold this into `ember_wall`.

**Recommendation.** B1. Attrition income for being hit matches the long-lane profile, stays under the Common +1 AP passive, and is data-only. B2 is the better "burn taxes the striker" fantasy once a once-per-turn attacker drain exists as a real primitive.

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
| Node passive | **Partner Beacon** (N1), not Relay Dividend | +1 AP at turn start only if a partner is buffed. Inside the Common AP-passive band, data-only, and it does not stack on Group Hug. |
| Burned passive | **Cinder Idle** (B1), not Scorch Echo | +1 AP at turn start only if you were hit last turn. Under `turn_start_ap_gain` and under `flame_charge`. Scorch Echo needs a once-per-turn attacker drain that is not an opcode yet. |
| Other types missing a passive | None | The other 12 types already have a Passive row. |
| Compressed | All seven (C1–C7), data-only | Soft heal, Evasion +1, Accuracy −1, Accuracy +1, a 15% wall with a real hit budget, a speed-gated 1 AP drain, and a no-bonus bank of 3. |
| Bot denial | Ship the data-only set D1–D7 with the AI exceptions | Drain 1, steal 1, one skipped regen on a recharging duration-2 Thread Lock, a +1 surcharge, a weaker hit that drains 1, an every-other-turn team tax, and a drain that leaves at least 1. No card drains and blocks regen together. |
| Meme chemistry | Leave it | Already out of the pass by design. |
