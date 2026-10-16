# Combat design ideas

**Status: proposals only.** Nothing in this note is implemented, and none of it closes an open audit item. The developer is answering those items separately. This pass does not retune powers, add Cofi's stronger Bean Beam, pick hit budgets for Ice Wall / Ash Guard / Compound Guard, edit `COMBAT_Moves.txt` §6.1, retune HODLed, or rewrite the unimplemented move rows in [UNIMPLEMENTED_MOVE_EFFECTS.md](../Audits/UNIMPLEMENTED_MOVE_EFFECTS.md).

Numbers cited as current come from this branch after the decision round (Bean Sip +1 AP, timed-stage revert, Group Hug, bench recharge). Arithmetic under a **proposal** heading is a sketch of that change, using the index in [BATTLE_MOVE_BALANCE_BY_RARITY.md](../Audits/BATTLE_MOVE_BALANCE_BY_RARITY.md). It is not a value already stored in data.

Stamp modifiers Side Quest (`3.2-b`), After Hours (`3.2-c`), Setup Feed (`7.2-c`), All-Stat Burn (`10.1-d`), Anniversary Mark (`10.2-b`), and Tournament Badge (`10.2-c`) are another agent's work. This note does not redesign them.

## How current output is scored

The balance screen's index, unchanged here:

- expected hits = `(HitMin + HitMax) / 2`
- accuracy factor = `Accuracy / 100` when accuracy is above 0, otherwise 1
- single-target index = `Power * hits * accuracy factor`
- spread index = that value times 2 when targeting is `AllEnemies` or `Field`
- per AP = field index / AP cost when AP cost is above 0

Crits, the `PRIOR` / `PRIO` priority block, and chemistry are outside that index. A move can look fair on the index and still swing turns. Default crit chance is `0.0625` when tuning is missing (`DamageCalculator`), plus `critChancePerStagePercent` `0.02` per crit stage (`MoveResolveProfile`). Default crit multiplier is `1.5` (`CombatConstants.CRIT_MULTIPLIER_DEFAULT`). Priority moves run in the block before normal combat (`CombatResolver` execution order: swaps, priority block, normal combat, capture).

Stage multiplier (`ChainlingInstance.StageMultiplier`): positive `(2 + stage) / 2`, negative `2 / (2 + abs(stage))`. So +1 is 1.5×, +2 is 2.0×, +6 is 4.0×, −1 is 2/3, −6 is 0.25. Stages clamp at ±6. Authoring caps are 1 for passives, 2 for active moves, 3 for chemistry (`CombatConstants`).

## 1. Balance ideas

Eight proposals. Each one is a tweak to dynamics that already exist.

### B1. Lift the typeless Rare single-target line toward the typed Common band

**Problem.** The developer rule for the balance screen is that a typeless Rare sits roughly with a typed Common. `COMBAT_Moves.txt` §6.1 still says rarity is complexity and availability, and that a Common at max evolution stays competitive with Epic in raw output. That sentence was left in place. Under the developer rule the current gap is out of order.

Evidence from `Docs/Specs/Combat/Data/Moves.csv` and the balance doc's index:

| Move | Rarity | Type | Power | Acc | AP | Index |
|---|---|---|---|---|---|---|
| Typed Common band (power 64, accuracy 95, 1 hit, single target). Thirteen damaging rows sit here, including `coordinated_shot`, `solid_strike`, `cipher_shot`, `stake_strike`. | Common | typed | 64 | 95 | 2 | 60.80 (30.40 per AP) |
| `refined_striker` Impact Knuckle | Rare | None | 42 | 90 | 2 | 37.80 |
| `refined_shot` Vector Shot | Rare | None | 38 | 90 | 2 | 34.20 |
| `refined_thrust` Rupture Point | Rare | None | 34 | 90 | 2 | 30.60 |

**Proposed change.** When a retune is allowed, raise those three refined rows toward the typed Common band and keep accuracy at 90 so the Rare still feels less reliable. Sketch using the same index: power 64 at accuracy 90 indexes 57.60 (about 5% under 60.80). Power 68 at accuracy 90 indexes 61.20. Rupture Point already has a 25% Def −1 rider, so its base power can stay a step under Impact Knuckle.

**Expected effect.** A typeless Rare hit becomes a real alternative to a typed Common hit of the same AP. The accuracy gap (90 vs 95) remains the reason to prefer the typed Common when the rider does not matter.

**Risk.** `all_in` (power 98, AP cost 0 in the CSV), `recoil_strike` (power 76), and `volatile_shot` (power 72) still lack their described costs. Those rows are open in the unimplemented-effects list. Raising refined Rares while those Commons are still free or under-costed aims at the wrong Commons. §6.1 still disagrees with the developer rule until someone edits the spec on purpose.

### B2. Spread: pull typeless Rare up, and treat Fork Split as its own decision

**Problem.** Spread indexes on this branch:

| Move | Rarity | Type | Shape | Index |
|---|---|---|---|---|
| `baseline_barrage` Pellet Storm | Common | None | power 10, acc 95, hits 2–3, AP 2, `AllEnemies` | 47.50 |
| `refined_volley` Scatterburst | Rare | None | power 22, acc 90, AP 2 | 39.60 |
| `refined_blast` Kinetic Burst | Rare | None | power 28, acc 90, AP 3 | 50.40 |
| `refined_barrage` Ricochet Storm | Rare | None | power 12, acc 90, hits 2–3, AP 3 | 54.00 |
| `fork_split` Fork Split | Common | Forked | power 46, acc 95, AP 3 | 87.40 (29.13 per AP) |
| `civet_beam` Bean Beam | Uncommon | Staked | power 12, acc 95, AP 2, Speed −1 on foes | 22.80 |

Pellet Storm, a typeless Common, sits above Scatterburst, a typeless Rare of the same AP. Fork Split, a typed Common, sits far above both typeless Rare spreads. Forked's type profile is the spread type (`COMBAT_TypeProfiles.md`), so Fork Split is carrying that identity.

**Proposed change.** Sketch, not a committed retune:

- Scatterburst power 22 → 26. Index becomes `26 * 0.90 * 2 = 46.8`, in line with Pellet Storm's 47.50.
- Kinetic Burst power 28 → 36. Index becomes `36 * 0.90 * 2 = 64.8`.
- Ricochet Storm power 12 → 16. Index becomes `16 * 2.5 * 0.90 * 2 = 72.0`.

Leave Fork Split's power alone in that same pass. If it is later brought toward the typeless Rare line, that is a separate call about whether Forked keeps a premium spread.

**Expected effect.** The typeless ladder stops inverting at Rare. Forked remains the type you bring when you want both foes hit hard.

**Risk.** `shard_scatter` is called out in the balance doc at index 85.50 with a missing Evasion rider (correctness E-18). Moving Rare spreads without deciding that rider compares against a row whose text and payload disagree.

### B3. AP tempo: the 1 AP band is three moves, and one of them is far above the band

**Problem.** Turn regen is +2 AP, cap 10 (`CombatConstants.AP_PER_TURN`, `AP_CAP`; `COMBAT_Resolution.txt` §1 and §1.1). Of 72 damaging moves, 40 cost 2 AP (regen-neutral), 28 cost 3, 3 cost 1, and 1 costs 0 (`all_in`, open). The three 1 AP damaging moves:

| Move | Type | Power | Acc | Tags | Index | Per AP |
|---|---|---|---|---|---|---|
| `baseline_jab` Quick Jab | None | 16 | 95 | `PRIO\|PRIOR` | 15.20 | 15.20 |
| `lean_hit` Lean Hit | Compressed | 46 | 95 | — | 43.70 | 43.70 |
| `rapid_strike` Rapid Strike | Forked | 52 | 95 | `PRIO\|PRIOR` | 49.40 | 49.40 |

Typed Commons at 2 AP are 30.40 per AP. Rapid Strike is both cheaper and in the priority block, and the index does not count priority. Lean Hit is the other efficient 1 AP hit, without priority. Quick Jab is the only modest 1 AP chip. Banking to 6–10 is rarely forced, because a 2 AP attack spends exactly the turn's regen.

**Proposed change.** Add more weak 1 AP attacks in the power 20–30 range (see `gas_vent` and `anchor_tap` in section 2) so spending 1 and holding 1 is a real turn. Separately, when numbers can move, bring Rapid Strike down or take it out of the priority block. Sketch: power 36 at accuracy 95 indexes 34.20, near the typed Common per-AP figure, and it would still be priority. Dropping `PRIOR` and leaving power 52 keeps the index and removes the "act first" premium.

**Expected effect.** Players choose between a small spend and a stockpile. Rapid Strike stops being the default efficient click.

**Risk.** `all_in`'s CSV AP cost is 0 while §1.3 still says it spends all current AP (minimum 3). `AIHelpers.DamagePerAP` returns 0 when AP cost is 0, so the utility AI does not score that free hit through the damage term. Decide that row before treating the 1 AP band as the only efficiency outlier.

### B4. Recharge is almost unused, and the utility AI ignores it

**Problem.** Rows with `RechargeTurns` above 0: `ap_boost` 3, `extend_protocol` 3, `bean_sip` 2, `fox_feint` 999, `gecko_guard` 999. That is five of 206 moves. Player-facing skipped turns are `rechargeTurns - 1`, because the granting turn's end tick already counts (`COMBAT_Resolution.txt` §1.2). So Bean Sip (`rechargeTurns` 2) skips one upcoming turn. AP Boost and Extend Protocol skip two. Fox Feint's 999 is the once-per-battle sentinel (`rechargeTurns >= 100`).

`UtilityStatusAI.ScoreAction` gives every utility move +30, then subtracts AP cost. It has no recharge term and no current-AP term. A legal recharge move keeps winning on the +30 for as long as it can be selected.

**Proposed change.** Use `rechargeTurns` 2 (one skipped upcoming turn) as the price of an above-band utility, in place of another AP point. Pair that with a small AI change: subtract a flat amount while the move's recharge is greater than 0, enough that a recharge utility does not always beat a damaging move. Do not change Fox Feint's 999 or Gecko Guard's unused passive recharge.

**Expected effect.** Strong utilities occupy the slot next turn the way Bean Sip already does, and bosses stop sipping a recharge move every time it is legal.

**Risk.** The AI term has to be tuned against the existing +30 or every recharge move becomes the last choice. Extend Protocol's "extend a buff" clause is still unimplemented; giving it a harsher recharge would price a clause that does not run.

### B5. Chemistry: keep the flat damage, thin out Jammed as the default cell

**Problem.** Chemistry damage is one relation per hit: advantage ×1.2, disadvantage ×0.8 (`CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR` / `CHEMISTRY_DISADVANTAGE_FACTOR`). Dual typing does not stack. If several forward cells qualify, the defender's primary type picks the reaction (`COMBAT_Chemistry.txt` §2). The live chart is `COMBAT_Interaction_Matrix_v1.csv`: 57 filled cells, Meme's row and column empty. Jammed is 8 of those 57 cells. The next most common are Anchored 6, Drained 5, Rugged 5. Nineteen distinct outputs. The type chart has no mutual advantage (`COMBAT_TypeProfiles.md`). The starter triangle is Node → Frozen → Bot → Node.

Jammed's accuracy drop is an open correctness item: the cell prints the status, and the status body does not apply the accuracy change. Redistributing cells does not fix that body.

**Proposed change.** Leave ×1.2 / ×0.8 alone. In a later chart pass, cap a single status at 4 cells and move the extra Jammed cells onto other latency outputs that already exist (Slowed, Isolated). Restoring Reinforced on the old v0 Glitched → Mined cell is a separate proposal; v1 removed that cell, and this note does not decide whether Reinforced's lack of a producer was a cut.

**Expected effect.** A chemistry hit still changes damage by a flat factor, and the status you receive varies by matchup instead of collapsing to Jammed.

**Risk.** The matrix pass that produced v1 is already closed (`COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md`). A cell edit is a content change to a locked chart, and it is wasted on Jammed until that status body is decided.

### B6. Keep the stage curve; put "for N turns" on a timed status

**Problem.** +2 Attack is a full double for as long as it lasts. Gas Spike, Shard Partition, Sear Field, FUD, and the other timed stage statuses now revert their stages when the status expires (decision round). Bean Sip's Speed +1 is still a move-column stage, so it stays after the sip's recharge ends. The curve itself is readable: each point up is +0.5×, and points down shrink faster.

**Proposed change.** Leave `StageMultiplier` and the ±6 clamp. For any new line that says a stage lasts a stated number of turns, author the stage on a timed status so the revert rule owns it. Leave existing permanent move-column stages, including Bean Sip's Speed +1, until someone wants those to expire too.

**Expected effect.** Future "for 2 turns" text matches the clock. Permanent setup stages stay a different, stronger tool.

**Risk.** A timed +2 that expires is much weaker than a permanent +2. Copies of Gas Spike's magnitude onto a one-turn status will feel like nothing if the text still says "+2". Duration and magnitude have to be chosen together.

### B7. Rarity authoring rule under "typeless Rare ~ typed Common"

**Problem.** Kit counts in `Moves.csv` (206 rows): Common 138, Rare 46, Uncommon 20, Epic 2 (`defensive_reflex`, `bulwark_blow`). §6.1 still says Epic is flexibility, and that Commons stay competitive with Epic in raw output. The developer rule used by the balance doc is narrower: a typeless Rare lines up with a typed Common. Those two sentences pull in different directions, and this note does not edit §6.1.

Epic `bulwark_blow` is power 50, accuracy 90, AP 3, and it attacks with MeleeDef (`DamageOverrideAttackStat`). Index on the attack stat's power is 45 before the Def substitution. It is not sitting on top of the power-64 Commons.

**Proposed change.** When a Rare rider actually runs, set the base index within about 10% of the typed Common band (60.80 at 2 AP, accuracy 95) and let the rider be the reason to pick the Rare. Until the dead riders in the unimplemented list are decided, leave Common powers where they are. Keep Epic on flexibility (stat substitution, multi-part passives), which is what the two Epic rows already do.

**Expected effect.** Rarity stops meaning "smaller number" for typeless Rares, and it also stops meaning "must out-damage Common" for Epic.

**Risk.** Applying the 10% band to a Rare whose rider does not run just buffs an empty card. `crystal_bash` is Rare, power 0, and its Def-scaling text does not run. It has to be decided before any Rare band is treated as complete.

### B8. Bench: AP stays on the slot; other statuses staying paused is cold storage

**Problem.** Roster is 3, actives are 2, so one bench slot (`CombatConstants.ROSTER_SIZE`, `ACTIVE_PER_TEAM`). A voluntary swap copies the outgoing fighter's current AP onto the incoming fighter (`CombatResolver` swap path: "AP belongs to the active slot"). Recharge keeps ticking on the bench. Every other status pauses (`COMBAT_Resolution.txt` §1.2). One swap to bench per team per turn is already reserved. The swap itself spends no AP.

A duration-2 buff can be frozen by benching and walked back in later with time left. That is a strong, easy-to-miss tool.

**Proposed change.** Leave both rules. Author combat buffs at duration 1–2 knowing that benching freezes them, and say so on the card when the status is the reason to bench. A 1 AP toll on the voluntary swap is a separate mechanic in section 3, not a change to slot AP or to the pause.

**Expected effect.** Bench play stays a Chainbound decision: the slot keeps the AP, the benched body keeps its buffs on ice, and its recharge still finishes.

**Risk.** A toll stacked on top of frozen buffs can make swapping strictly bad. If B-toll in section 3 lands, buff durations should be reviewed in the same pass so parking a buff is still worth the point.

## 2. New move ideas

Twenty proposals. Ids below are absent from `Moves.csv`. They are new cards. They are not rewrites of `accuracy_focus`, `evasion_maneuver`, `gas_cloud`, `all_in`, `crystal_bash`, or the other open rows.

Accuracy 0 on a utility means the move does not roll a hit, matching the existing utility rows (AP Boost, Fee Collector, and the rest use accuracy 0). Power 0 means it does not deal damage.

Opcodes with an executor and **zero** CSV rows today: `StealAP`, `TransferAP`, `RefundAP`, `TurnStartPendingStatus`, `HealPercentIfStageAtLeast` (`UtilityEffectExecutor`, `Combat2DataImporter`). `DrainAP` has two rows (`defensive_reflex` drains the user; `fee_collector` drains foes). `ChangeStage` already accepts `CombatStageStat`, which includes Accuracy and Evasion; the move `StageChanges` column does not.

`Refund` in the executor is always called on the user, not on `ResolveUtilityTarget`. A refund-the-ally card is engine work. The Quiet Ledger proposal refunds the user.

**Data-only** means a new CSV row (and the usual import into the move asset) using opcodes and conditions that already resolve. **Engine** means a resolver, status, or payment change.

| # | Id | Display | Type | Rarity | Category | AP | Pow | Acc | Effect | Primitive | Cost |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `fee_tap` | Fee Tap | Gas | Common | StatusUtility | 1 | 0 | 0 | One foe loses 1 AP. | `DrainAP:1:100%:SingleEnemy:fee_tap` | Data-only |
| 2 | `cipher_skim` | Cipher Skim | Encrypted | Rare | StatusUtility | 2 | 0 | 0 | Steal 2 AP from one foe. Stolen AP still clamps at 10. | `StealAP:2:100%:SingleEnemy:cipher_skim` | Data-only |
| 3 | `relay_cut` | Relay Cut | Node | Uncommon | Support | 2 | 0 | 0 | Pay 2, and grant 2 AP to one ally. Team total stays flat; the point moves into the ally's pool. `TransferPaidCost` grants the authored amount and does not drain a second time. | `TransferAP:2:100%:SingleAlly:relay_cut` | Data-only |
| 4 | `brace_yield` | Brace Yield | Compressed | Common | Support | 2 | 0 | 0 | Heal the ally for 8% MaxHP when their Def stage is at least +1. | `HealPercentIfStageAtLeast:8%:MeleeDef:1:SingleAlly:brace_yield` | Data-only |
| 5 | `fork_line` | Fork Line | Forked | Rare | DamagingMelee | 2 | 64 | 95 | Base index 60.80, same band as a typed Common. +25% when the target has at least one debuff. | Conditional damage, `DebuffCountAtLeast` (same columns as `bug_payoff`) | Data-only |
| 6 | `press_shot` | Press Shot | Compressed | Rare | DamagingRanged | 2 | 64 | 95 | Base index 60.80. ×1.2 when the user's Speed is higher than the target's. | Conditional damage, `SpeedHigherThanOther` | Data-only |
| 7 | `aim_lock` | Aim Lock | Gas | Common | StatusUtility | 1 | 0 | 0 | Foe Accuracy −1. | `ChangeStage:Accuracy:-1:100%:SingleEnemy:-6:false:aim_lock` | Data-only |
| 8 | `slip_step` | Slip Step | Staked | Common | StatusUtility | 1 | 0 | 0 | Self Evasion +1. | `ChangeStage:Evasion:1:100%:Self:6:false:slip_step` | Data-only |
| 9 | `mempool_mark` | Mempool Mark | Glitched | Common | StatusUtility | 1 | 0 | 0 | `SearField` (Atk −1, duration 1, owned stage reverts on expiry) is applied at the target's next turn start. | `TurnStartPendingStatus:SearField:1:SingleEnemy:mempool_mark` | Data-only |
| 10 | `node_beacon` | Node Beacon | Node | Rare | Passive | 0 | 0 | 0 | At your turn start, if at least one partner is buffed, gain +1 AP. | `GainAP` plus `BuffedPartnerCountAtLeast`, same trigger shape as `fee_collector` | Data-only |
| 11 | `cinder_idle` | Cinder Idle | Burned | Rare | Passive | 0 | 0 | 0 | At your turn start, if you were damaged last turn, gain +1 AP. | `GainAP` plus `WasDamagedLastTurn` | Data-only |
| 12 | `rampart` | Rampart | Block | Common | Support | 2 | 0 | 0 | 20% mitigation, turn budget 1, hit budget 2. | `MitigationWindow:20%:1:2:wall_rampart` | Data-only |
| 13 | `stockpile_shot` | Stockpile Shot | Shard | Rare | DamagingRanged | 3 | 46 | 90 | Base index 41.40. ×1.4 when your AP is at least 6, which sketches to about 58, near the typed Common band, at 3 AP. | Conditional damage, `APAtLeast` | Data-only |
| 14 | `gas_vent` | Gas Vent | Gas | Common | DamagingRanged | 1 | 28 | 90 | Plain chip. Index sketch 25.20. | Damage only | Data-only |
| 15 | `anchor_tap` | Anchor Tap | Staked | Common | DamagingMelee | 1 | 24 | 95 | Plain chip. Index sketch 22.80. | Damage only | Data-only |
| 16 | `quiet_ledger` | Quiet Ledger | Compressed | Uncommon | StatusUtility | 1 | 0 | 0 | If you have not used a damaging move this turn, refund 1 AP and gain Speed +1 (stage cap +2). | `RefundAP` + `ChangeStage` + `DidNotUseDamagingMoveThisTurn` | Effect is data-only. Selection lock is a small eligibility addition: `ActiveConditionType` only special-cases `SelfHPBelowPercent`, so the condition currently gates resolution, and a player can still pay the AP. |
| 17 | `toll_gate` | Toll Gate | Gas | Uncommon | Support | 2 | 0 | 0 | Full absorb, turn budget 2, hit budget 1. The attacker loses 1 AP when that hit lands. | `FullAbsorbBarrier:2:1:wall_toll` plus `WallHitRetaliation:1:wall_toll` (the pair `ember_wall` already uses, with a 1-hit budget) | Data-only |
| 18 | `confirmation_seal` | Confirmation Seal | Block | Rare | StatusUtility | 2 | 0 | 0 | For the rest of this turn, the next stage this fighter applies is exempt from timed-status revert. | New status. `StatusInstance` owned-stage revert would grow a one-shot exemption. | Engine |
| 19 | `reorg_pulse` | Reorg Pulse | Glitched | Rare | StatusUtility | 2 | 0 | 0 | Revert the target's latest stage delta from this turn. | New "last stage delta this turn" record on the fighter, read from the swap/stage path. | Engine |
| 20 | `partner_float` | Partner Float | Node | Rare | StatusUtility | 2 | 0 | 0 | If the user is short by 1 AP, the other active pays that 1. The move still fails when the pair together cannot pay. | AP payment in the eligibility / spend path. `TransferAP` grants AP; it does not split a cost. | Engine |

Gaps these are aimed at, from the same 206-row count:

- Gas is the smallest typed kit (9). Fee Tap, Aim Lock, Gas Vent, and Toll Gate give it an active tax, an accuracy tool, a cheap hit, and a one-hit toll. Fee Collector remains the passive tax.
- Encrypted's profile is AP drain (`COMBAT_TypeProfiles.md`) and no move uses `StealAP`. Cipher Skim is that card.
- Node has five supports and no passive, and no move uses `TransferAP`. Relay Cut and Node Beacon fill the coordinator role. Partner Float is the engine version of the same idea.
- Compressed has no support and no Rare damaging move. Brace Yield, Press Shot, and Quiet Ledger cover that.
- Forked's four damaging moves are all Common, including Fork Split and Rapid Strike. Fork Line is a Rare single-target line at the typed Common index, with a rider.
- Burned has no passive. Cinder Idle is attrition AP, which matches the type profile better than another wall.
- Block's working multi-hit wall in data is `barrier_wall` (25%, turn budget 2, hit budget 2, AP 3, typeless). Rampart is a cheaper, shorter, typed wall with an explicit hit budget of 2. It does not edit Ice Wall, Ash Guard, or Compound Guard, whose hit budget is 0. `AddMitigationSource` returns immediately when `hitsRemaining` is 0, so those three stay inactive until a hit budget is chosen.
- The 1 AP damaging band is Quick Jab, Lean Hit, and Rapid Strike. Gas Vent and Anchor Tap are the modest chips from B3.
- Stockpile Shot is a player-facing "spend a stockpile" hit. It is not Cofi's missing beam.

## 3. New mechanics

Six proposals. Cost is which systems move, and how invasive that is. Calendar time is not estimated.

### M1. Treat slot AP, personal recharge, and paused buffs as the core loop

**How it works.** Already shipped, and worth authoring toward: AP lives on the active slot and is inherited across a swap. Recharge is on the move and keeps ticking on the bench. Other statuses pause, so a buff can be stored cold.

**Why it's distinct.** Most creature battles pause the whole fighter, including cooldowns, or pool the team's resource. Here the resource stays in the slot, the lockout finishes in the back, and the buff waits.

**Interactions.** Cap 10 and +2 regen still clamp everything. Group Hug's +1 AP hits living actives only, so a benched ally does not receive it. A duration buff frozen on the bench comes back with its remaining turns.

**Cost.** No engine change. Authoring and tooltip copy. Pairs with B8.

### M2. Swap toll

**How it works.** A voluntary swap spends 1 AP from the slot after the incoming fighter inherits that pool. The existing one-swap-per-turn reserve stays. Forced swaps and the KO auto-swap stay free so a faint does not also tax the slot.

**Why it's distinct.** Swapping is currently a free reposition that keeps the AP. A 1 AP toll makes the bench a paid transaction, in the same language as Gas fees and Relay Cut.

**Interactions.** At 0 AP the swap is illegal, which is a real lockout because regen happens at turn start (`StatusProcessor` turn regen) and a fighter can end the previous action at 0. Cold-storage buffs (M1) become a purchase. Bean Sip's AP gain makes the toll easier for Cofi, which is acceptable for a boss who is already AP-positive.

**Cost.** Small. The voluntary swap path in `CombatResolver` (the block that copies `slotAp`) spends 1 and logs it. Eligibility UI needs the same check so the button matches the resolver. No new action type.

### M3. Finality

**How it works.** Confirmation Seal (move 18) applies a one-turn status. The next stage that fighter applies is recorded as permanent even if the move that caused it was a timed status. Further stages in the same turn follow the normal revert rule.

**Why it's distinct.** The decision round made timed stages honest. Finality is the chain flavor of that rule: one change is confirmed and the rest can still be reorged. It is a decision about which buff you want to keep, not a second copy of +2 Attack.

**Interactions.** Owned-stage revert in `StatusInstance` / `StatusProcessor` is the system it hooks. Bean Sip's Speed +1 is already permanent, so Finality does nothing for it. A timed Gas Spike confirmed by Finality would leave the Attack stage behind after Gas Spike expires. Refresh rules stay as they are: confirming does not reapply the stage.

**Cost.** Small, and localized. One new `StatusId`, one exemption flag consumed by the owned-stage apply path. Needs tests beside the decision-round revert tests. Does not retune HODLed's ramp.

### M4. Reorg

**How it works.** Reorg Pulse (move 19) reverts the target's most recent stage delta from the current turn. Benching can be defined later as an optional rider on the same record (the last self-applied stage unwinds when you leave the slot). This proposal is the move, not an automatic bench rewind.

**Why it's distinct.** Finality confirms a write. Reorg undoes one. Together they are a two-move argument about the stage table, which is the combat version of a chain reorganizing.

**Interactions.** The stage record has to store the clamped delta, the same value owned-stage revert already stores, or a later revert will double-undo. Chemistry stages and move-column stages both need to append to that record or Reorg will only see one of them. Paused bench statuses are untouched: Reorg does not expire a status, it only undoes a stage.

**Cost.** Medium. A per-turn delta log on `ChainlingInstance`, written from the stage paths in `StatusProcessor`, `ChemistryResolver`, and move-column application, and a utility opcode to pop the last entry. Wider than Finality because every stage writer has to log.

### M5. Chemistry catalyst

**How it works.** Spend 1 AP when you declare an attack. If more than one of your types has a forward cell into the defender, you choose which cell fires. If you do not spend, the defender's primary type still picks, which is today's rule.

**Why it's distinct.** Dual typing currently collapses to one relation and one status. The catalyst makes the second type a paid choice. Damage stays ×1.2 or ×0.8 either way, so the payment buys the status, not a second damage multiplier.

**Interactions.** `COMBAT_Chemistry.txt` §2 is the rule that changes. Meme has an empty row and column in v1, so a Meme catalyst has nothing to pick until that wildcard exists. Jammed's body is still open; a catalyst that selects Jammed inherits that gap. Self-targeted cells (`Pumped @Self`, `Defueled @Self`) need a clear pick target so the UI can show them.

**Cost.** Small to medium. `ChemistryResolver` gains a selected-type argument. The action needs a field for the paid type, and the UI needs a second step when the attacker is dual-typed and both cells qualify. Single-typed attackers never see it.

### M6. Mempool timing as a content pattern

**How it works.** `TurnStartPendingStatus` already queues a status onto the target and `CombatResolver` applies the queue at the following turn start. Zero moves use it. Mempool Mark (move 9) is the first card: the debuff is visible in the log now and lands next turn, so the opponent can bench, cleanse, or spend before it arrives.

**Why it's distinct.** Priority (`PRIOR`) means "act first in this turn." A mempool card means "this effect confirms next turn." That is the other half of a chain: broadcast now, finality later. It does not need a new action timing system.

**Interactions.** Pending entries live on the fighter. A swap that leaves them in place (the current field is on `ChainlingInstance`, and the swap path does not clear it in the block cited above) lets a benched target carry the mark. That interaction should be stated on the card. Cleanse, if it does not know about the pending list, will miss it; that is a follow-up inside `UtilityEffectExecutor.ApplyCleanse`, still smaller than a new phase.

**Cost.** Low for the first card (data-only, move 9). A pass that teaches Cleanse and the swap path about the pending list is a small executor and swap-path change. A true "resolve last" phase, where the move itself waits until end of turn, is a new timing block in `CombatResolver` and is out of scope for this pattern.

## 4. Boss design: Cofi Lootwak's stronger Bean Beam

**Proposals only.** No stronger beam exists, and this note does not add one.

What the kit is today:

- Encounter `CofiLootwak_Encounter.asset`: species `cofi_lootwak`, stars 2, difficulty 2, `aiPersonality` 3 (Utility), moves `bean_sip` and `civet_beam` only. Authoritative id `coffee_cofi_lootwak`. No starting-AP field was found on that asset (`currentHp` is −1, which is a separate sentinel). Do not assume a starting AP.
- Species pool (`SpeciesMovePools.csv`): learnables `civet_beam|compound_guard|stake_heal`, exclusive `bean_sip`, flagship `bean_sip`, initial `bean_sip|civet_beam`.
- Bean Sip: Staked, Uncommon, utility, self, AP 1, accuracy 0, `rechargeTurns` 2 (skips one upcoming turn), heal 8% MaxHP, Speed +1 as a move-column stage, `GainAP:1`. Tags include `PRIO|PRIOR`.
- Bean Beam (`civet_beam`): Staked, Uncommon, `DamagingRanged`, `AllEnemies`, power 12, AP 2, accuracy 95, Speed −1 on foes. Spread index 22.80.
- Sip turn, assuming the standard +2 regen: spend 1, gain 1, regen 2, net +2, then the sip is locked for one upcoming turn. Beam turn while the sip recharges: spend 2, regen 2, net 0. Alternating is about +1 AP per turn, plus the heal and the permanent Speed +1.
- Utility AI (`UtilityStatusAI`): a utility starts at +30 and then loses its AP cost, so a legal Bean Sip scores 29 from that portion before status bonuses. A damaging move scores `EstimateDamage / apCost * 2` (`AIHelpers.DamagePerAP` returns 0 when cost is 0) and then loses its AP cost. There is no current-AP check and no recharge check. While Bean Sip is legal, it wins unless the beam's damage term clears that head start. Power 12 into two targets is unlikely to.

Any of the options below needs an AI rule or Cofi will keep sipping. Suggested rule, shared by all three: when `currentAP >= 6` and the stronger beam is legal, add a flat bonus large enough to beat Bean Sip's utility score (the sip portion is 29 before bonuses; a bonus of 40 on the beam is the sketch). When AP is under 6, the beam is not selected. When the sip is recharging, fall back to `civet_beam` as today. The bonus lives in `UtilityStatusAI`, not in a hidden second kit.

### Option A — Espresso Shot

Single-target payoff. New id, for example `espresso_shot`. Staked, Uncommon, `DamagingRanged`, `SingleEnemy`, power 80, accuracy 90, AP 4. Index sketch `80 * 0.90 = 72`, above a typed Common's 60.80 and costs twice the AP, so it is a stockpile button. Effect: steal 1 AP (`StealAP`, already implemented, unused in data). AI uses it only at 6 AP or more, and only instead of Bean Sip; `civet_beam` remains the recharge fallback. After a 4 AP spend from 6 AP, Cofi has 2 left plus whatever the steal returns, still under the cap.

### Option B — Double Bean

Keep the spread. New id, for example `double_bean`. Staked, Uncommon, `AllEnemies`, power 36, accuracy 95, AP 3, Speed −1 on each foe (same rider shape as `civet_beam`). Spread index sketch `36 * 0.95 * 2 = 68.4`. That is still under Fork Split's 87.40, and well above Bean Beam's 22.80. AI treats it as the 6 AP button and does not also fire `civet_beam` on that turn. From 6 AP the spend of 3 leaves 3, so the following sip is still affordable once recharge allows it.

### Option C — Cream Top

Modest spread, the 6 AP buys a resource swing. New id, for example `cream_top`. Staked, Uncommon, `AllEnemies`, power 20, accuracy 95, AP 3, and steal 1 AP from each foe hit. Spread index sketch `20 * 0.95 * 2 = 38`. Two steals at the cap of 10 can be partly wasted, which is the risk of using it at exactly 10 rather than at 6. AI rule is the same 6 AP gate. This option stays a decision beside Bean Sip's +1 AP, because the beam takes AP off the player's slot and puts it on Cofi.

None of A, B, or C is selected here. All three are exclusive boss moves, same `NO_CANDY` idea as the current pair, and none of them edits `civet_beam`.

## 5. Shortlist

Highest impact for the least new machinery. Order is the order to do them. All five are still proposals.

1. **Unused-opcode pack (data-only).** Fee Tap, Cipher Skim, Relay Cut, Brace Yield, Mempool Mark, Aim Lock. Six cards, no resolver change, and they fill Gas tax, Encrypted steal, Node handoff, Compressed support, delayed debuffs, and Accuracy. These opcodes already execute.
2. **AI terms for recharge and an AP gate (small, one scorer).** `UtilityStatusAI` subtracts for recharge and can add a bonus when `currentAP` crosses a threshold. This does not add the 6 AP beam. It makes B4 and every option in section 4 possible later, and it stops a legal Bean Sip from always winning.
3. **Two honest 1 AP chips (data-only).** Gas Vent and Anchor Tap. The damaging 1 AP band stops being Quick Jab, Lean Hit, and Rapid Strike. Rapid Strike's own number stays put until the open cost rows are decided.
4. **Price one new above-band utility with `rechargeTurns` 2.** Same clock Bean Sip already uses (one skipped upcoming turn). Depends on shortlist item 2 so the utility AI does not spam it. Does not change Fox Feint's 999.
5. **1 AP voluntary swap toll (small resolver and UI check).** Makes the shipped slot-AP / cold-storage bench rule a paid choice. Forced swaps and KO swaps stay free.

Held back on purpose: raising typeless Rare powers (B1, B2) until `all_in`, `recoil_strike`, `volatile_shot`, and `crystal_bash` have a change / remove / implement call. Finality and Reorg (M3, M4) are the high-identity follow-ups once the data-only pack is in and the revert tests are the safety net.

## Counts

| Section | Count |
|---|---|
| Balance ideas | 8 (B1–B8) |
| New moves | 20 (16 data-only, 1 data-only effect with a small selection-gate gap, 3 engine) |
| New mechanics | 6 (M1–M6) |
| Boss beam options | 3 (A, B, C), none chosen |
| Shortlist | 5 |
