# Status producer gaps

**Proposals only.** No move, status, pool, or engine row was edited. No branch was pushed.

Source of truth is the local snapshot `chainbound_combat_snapshot_20261018_1014` (zip taken 2026-10-18 10:14 AM CT). The GitHub checkout was not used for live numbers. `Docs/GameTheory` was not read.

This round starts from `Docs/Combat/Design/PASSIVES_AND_STATUSES_ANALYSIS.md` (the 2026-10-17 snapshot) and does not repeat that passive-by-passive balance pass. Where this snapshot disagrees with that note, the snapshot is the finding.

Recorded rules these proposals follow: typed moves beat typeless, and a rare typeless move sits near a typed common. Commons stay weak. Bot is the AP-denial type. Meme has no chemistry. Node is better with an ally. Move, modifier, and passive ticks are supposed to resolve before chemistry and status ticks, and narration calls each event out. Recharge ticks on the bench. Timed-status stat changes end with the status unless it is refreshed. Boss-only moves stay out of learnable pools and candy. Every species pool keeps at least one Common damaging move. Self-damage stays shelved (`SELF_DAMAGE_SHELF.md`); nothing below adds a self-damage producer. AP cost is set by battle impact, not a formula.

Recent decisions this snapshot has **not** absorbed, so no proposal below rewrites them:

| Decision | What the 10:14 data actually does |
|---|---|
| All-In locks only itself for 1 turn | `rechargeTurns` 2 stamps `Recharging` on All-In only. The same-turn end tick makes that a one-turn lock. The description still says "Skip your next action." The log says "recharging (2 turns)." |
| Interest Strike refunds 1 AP per hit only when melee Def stage **>** melee Atk stage | The gate is `StageAtLeastOtherStage`, which is `>=`. A fresh 0/0 Chainling refunds. |
| Hammer Sync +20% only when the ally is buffed | The asset is +10% per buffed partner, cap +30% (`bonusMultiplierPerCount` 0.1, `maxMultiplier` 1.3). |
| Bean Sip: 1-turn recharge, +1 AP | Boss-only (`NO_CANDY`, Cofi exclusive). `rechargeTurns` 2 is already a one-turn self-lock. There is no `GainAP`. Heal 8% and Speed +1 do run. Speed +1 is a move stage, so it stays after the recharge ends. |
| Cipher Shot blocks self/ally buffs on the target until that target swaps out | Description still says "25% chance to block target next buff." `statusEffects` and `utilityEffects` are empty. No resolver read of `cipher_shot`. |
| Precision Strike is the first near-guaranteed-hit accuracy effect | Description says "+20% accuracy." `accuracy` is 95 and `accuracyBonusPercent` is 0. |

---

## 1. Where the 2026-10-17 note still holds, and where this snapshot moved

### Still true

- `Statuses.csv` has 56 rows. The seven enum ids with no CSV row and no `StatusDef_*.asset` stay cut: `Reinforced`, `DustStorm`, `BridgedOut`, `ForkedChain`, `CompressedData`, `MemeSurge`, `NodeRelay`. `CATALOG_STATUSES_WITHOUT_PRODUCERS.md` (2026-10-16) is still stale for those seven. Do not put them back. Splinter is the spread. Dusted is the evasion drop. Uplink is the Node passive. Meme's live keys are Staggered and Surging, and both are modifier procs, not Meme moves and not chemistry.
- True orphans with a running body and no producer are still `Rekt`, `Offline`, `GlitchedMind`, `HODLed`, `Unsealed`, and `Relay`.
- `COMBAT_Chemistry.txt` §7 still bars Firewalled, Halted, Crashed, LiquidityPump, Evasive, Bridged, Narrowcast, Overclocked, TypeCoating, HypedTypeCoating, and Amplified from the v1 matrix. The matrix has 57 filled cells. Meme's row and column are empty. Cell counts match the October 17 note: Jammed 8, Anchored 6, Drained 5, Rugged 5, Pumped 4, Dusted 4, Shattered 4, Defueled 3, Corroded 3, Isolated 2, Backlash 2, Splinter 2, Leaked 2, FOMO 2, Firewall 1, Slowed 1, Drip 1, Siphoned 1, Cascade Failure 1.
- Jammed applies a token and writes no Accuracy. Backlash applies a token and deals no contact damage. Burned Tokens applies a token and heals nobody. Overleveraged applies a token; `CombatConstants.OVERLEVERAGED_RECOIL` (0.25) is never read. Immovable applies a token; `ResolveSwap` checks `FrozenAssets` only.
- MEV Extracted drains 1 AP. It does not give that point to the user.
- End of turn is still status processing, then `OnBuffExpired`, then `EndOfTurn` passives. Kindled still ticks first, from a literal `0.06f`, ahead of Rugged. Thread Lock can still expire before Double-or-Nothing's end-step AP roll.
- Yield and Pumped are the same +1 AP tick on purpose (General vs chemistry). Leave that split.
- HODLed still has no stat-drop immunity. `SetStage` does not check it. The asset ramp plus the extra +1 while Defense is below +3 can still write more than +1 per end step, and neither write is owned.

### No longer true (do not build on these sentences)

- **Bench recharge now ticks.** `CombatResolver.TickBenchRecharge` walks living benched fighters and ticks `Recharging` only. `CombatRuntimeFixTests.BenchRechargeTicksWhileBenched` expects a 2-turn timer to fall to 1. `BenchPausesStatusDurationUntilSwapIn` still freezes a benched Slowed. The October 17 P1 ("the snapshot pauses recharge") is done. The written rule and E-29 match this tree.
- **Gas Spike and FUD stages now end with the status.** `StatusProcessor.ApplyApplyOnceStages` calls `AddOwnedStage` for `GasSpike` and `FUD` (and still for Bot Swarm, 51% Attacked, and Dusted). `RevertChemistryStageEffects` subtracts those deltas on expiry. The October 17 note, and the unimplemented-effects note it was correcting, are both stale for these two. The AP-ladder audit (item 31) is the change record. Shard Partition, Corroded, and HODLed are still not owned.
- **Kitsauto is no longer granted `automated_pressure`.** The pool is `multi_thread_shot`, `packet_skim`, `debuff_script`, exclusive `fox_feint` and `blade_flurry`. Packet Skim is the new Common 1 AP drain. The October 17 claim that Bot's granted passive walks foe stats to −6 is a description of a card that is no longer in the pool. The passive row still walks to −6 if something equips it.
- **Rarities moved with the AP ladder.** Kindling and Uplink stayed Uncommon, which `COMBAT_ISSUES_MASTER` already approved against the "passives are at least Rare" floor. Fuel Surge is Mythic. Thread Lock is Legendary. Bot Swarm, Majority Pressure, Verification Lock, and Shard Partition are Epic. Several typeless passives that the October 17 note called Common are now Rare or higher. Status proposals below use the new rarities.
- **209 move rows, not 208.** Packet Skim is the extra Common.

---

## 2. How a status gets into a fight

Checked against the resolver, not the CSV sentence.

A move applies a status only through the `StatusEffects` column (`StatusId:Duration:Chance:TargetOverride`), which `ApplyStatus` rejects when the catalog origin is `ChemistryOnly`. No live move tries to apply a chemistry status. Spite Counter can mirror a General debuff that a move just applied (33%). It is not a primary producer, and it cannot mirror chemistry, because that apply hits the chemistry guard.

These producers do not use the `StatusEffects` column:

| Status | Who applies it | Verified in |
|---|---|---|
| Kindled | Passive `kindling`, after a non-KO damaging hit, duration 1, no refresh while up | `TryApplyKindling` |
| Staggered | Modifier `6.1-c` Suppress, on hit | `ProcessAfterDamageModifiers` |
| Surging | Modifier `6.2-c` Crit Feed, on crit | same |
| Slowed | Modifier `6.6-a` Tripwire, on escape, duration 1, plus the one Frozen → Staked chemistry cell | `ApplySlowedForTurns` and `ChemistryResolver` |
| DamageIntercept | `damage_intercept` and `signal_redirect` by move id | `ResolveDamageRedirect` |
| Recharging | Any non-passive action whose `rechargeTurns` > 0. Duration is the column value. The granting turn's end tick counts. | `ResolveMove`, `TickBenchRecharge` |
| Everything else in the v1 matrix | Chemistry on a damaging hit, once per target per action | `ChemistryResolver`, matrix importer maps "Backlash Bot Dmg" to `BacklashType` |

Frozen Assets and Thread Lock are queued and applied at the **start of the next turn**, before regen (`ApplyPendingTurnStartStatusesForTeam`, then `ProcessStartOfTurn`). A duration of 1 therefore covers that next turn's regen and its actions, then the end step ticks it off before `EndOfTurn` passives.

`ApplyStatus` refresh keeps the instance, takes the longer duration, and does not re-apply apply-once stages.

Cleanse (`StatusProcessor.Cleanse`) removes up to N General debuffs. It does not revert owned stages, temporary stages, or Slowed's Speed. Defueled removes one harmful chemistry status and reverts a temporary delta or Slowed only. It does not revert Dusted's owned Speed and Evasion. Effect Relay and Relay Packet remove a status and re-apply it on the other fighter, so the original bearer keeps the stages and the receiver gets a fresh apply-once write.

---

## 3. Inventory

Legend for the class column: **live** means a producer exists and the body does what the catalog says. **token** means a producer exists and the body does not. **orphan** means nothing applies it. **barred** means `COMBAT_Chemistry.txt` §7 forbids a new matrix cell. **engine-only** means the resolver handles it and no data applies it.

Pool names are `SpeciesMovePools.csv`. "Ungranted" means no learnable, exclusive, initial, flagship, or off-type cell. Equip is wider than the pool: a known typed move still equips on a matching primary, and a typeless move equips on anyone.

### 3.1 Chemistry outputs (v1 matrix)

| Status | Cells | Body in this snapshot | Class |
|---|---:|---|---|
| Jammed | 8 | Token. `StatusDef_Jammed.asset` has `stageEffects: []`. No Accuracy write. `slowedLoseActionChance` is also unused, and it is not wired to Jammed. | token |
| Anchored | 6 | Next damaging move is forced to one target, then consumed. | live |
| Drained | 5 | −1 AP at end of turn for 3 turns. | live |
| Rugged | 5 | Escalating MaxHP damage, first tick 1/16, cap 15. Refresh does not raise the counter. | live |
| Pumped | 4 | +1 AP at end of turn for 3 turns. All four cells are `@Self`. | live |
| Dusted | 4 | Speed −3 and Evasion −3, both owned, both revert. | live |
| Shattered | 4 | Instant: 8% of current HP, floored so it cannot KO by itself, and MeleeDef −1 via `SetStage`. The Def drop is not a status delta. It stays, and Defueled cannot undo it because the status is already gone. | live, stage stays |
| Defueled | 3 | Removes one harmful chemistry status. See the revert hole in §2. | live |
| Corroded | 3 | MeleeDef −1 at each end step, `applyOnce` false, duration 2. The drops are not owned. Two end steps leave Def −2 after the status ends. | live, stage stays |
| Isolated | 2 | Blocks ally support, healing, AP transfer, and ally stage buffs. | live |
| BacklashType | 2 | Token. Parameter type Bot. No contact DoT. `endOfTurnDamagePercent` is 0. | token |
| Splinter | 2 | Next damaging move spreads at 65%, or the single target takes ×1.15. In `IsNextActionStatus`, so a duration of 1 survives the granting end step. | live |
| Leaked | 2 | If any active ally is Leaked, the actor loses 10% MaxHP on action. The end-step DoT loop skips Leaked. | live |
| FOMO | 2 | MeleeAtk +1, owned as a temporary delta, reverts. Self DoT is `fomoSelfDotPercent` 0.0625. | live |
| Firewall | 1 | First projectile each turn, mitigation 0.30, re-armed at turn start. | live |
| Slowed | 1 + Tripwire | Speed −2, reverted on expiry. The 25% action-loss constant is unused. | live |
| Drip | 1 | Heals `dripHealPercentMaxHp` (0.06) at each end step for the duration. The spec's "once" is still the open E-04. | live |
| Siphoned | 1 | Heals 25% of damage dealt. | live |
| CascadeFailure | 1 | Third application deals 25% MaxHP and can KO. Swap clears the counter. Bench time pauses it. | live |

### 3.2 General statuses a move, passive, or modifier applies

| Status | Producer | Rarity / cost | Pool | Body |
|---|---|---|---|---|
| Kindled | Passive `kindling` | Burned / Uncommon / 0 AP | Cinislug | 6% MaxHP, then the duration-1 status expires. Second hit the same turn does not refresh. Literal `0.06f` in the processor; the catalog percent is also 0.06 and is not read. |
| GasSpike | `fuel_surge` | Gas / Mythic / 0 AP | Deghoul (initial), Balloom | Atk +2 and Def −2, owned, revert after 2 turns. The move also grants +3 AP. |
| FUD | `verification_lock` | Encrypted / Epic / 1 AP | Keyper | Atk −1, Def −1, Speed −1, owned, revert after 2 turns. The move's `NextMoveAPSurcharge:1` does run. Accuracy is not in the asset. |
| ShardPartition | `shard_partition` | Shard / Epic / 2 AP, all allies | Crackett | Speed +1 apply-once on each recipient. **Not owned. Stays after the 2 turns.** "Team" is the move's targeting, not an aura. |
| BotSwarm | `bot_swarm` | Bot / Epic / 2 AP, all foes | Ungranted | Atk −1 and Def −1, owned, revert at the granting end step. |
| FiftyOnePercentAttacked | `majority_pressure` | Mined / Epic / 2 AP, all foes | Ungranted | Speed −1, owned, reverts at the granting end step. Enum comment still says accuracy too. The asset does not. |
| ExploitMark | `exploit_window` | Mined / Rare / 1 AP | Minefolk | Allies' hits ×1.15 for 2 turns. |
| EncryptedSeal | `proof_shield` | Encrypted / Uncommon / 2 AP, one ally | Ungranted | Blocks new debuff applies for the duration (1). |
| FrozenAssets | `lock_assets` and `partition_shot` | Frozen Rare 1 AP utility, ungranted. Shard Rare power 64 / acc 95 / 2 AP, Marrowhead flagship | Marrowhead only | Queued to next turn start. Voluntary swap is rejected for that turn. |
| ThreadLock | `thread_lock` | Bot / Legendary / 2 AP | Ungranted | Queued to next turn start. `CombatApService.Gain` blocks gains, including regen. Reapply is blocked until two turns after a natural expiry. |
| Yield | `yield_cycle` | Staked / Rare / 1 AP | Hopoch, Rattlestake | +1 AP at each end step for 3 turns. |
| MEVExtracted | `mev_extract` | Bot / Rare / 2 AP | Ungranted. Test loadout `starter_kitsauto_aggro` only | Catalog and move text say steal. Processor drains 1. The user gains nothing. |
| BurnedTokens | `deflation_mark` | Burned / Rare / 1 AP | Cinislug | No heal body. The move's Atk −1 stage does run and stays. |
| Overleveraged | `voltage_rush` (25% self), `risk_marker` (100% foe) | Forked Mythic 1 AP, in Forkrook. Compressed Rare 1 AP, ungranted | Forkrook's self-proc only | No recoil body. Class Consumed, duration 2, and nothing consumes it, so it just ticks off. |
| Immovable | `immovable_stance` | Block / Rare / 1 AP | Blockwyrm (initial) | No swap read. The move's Def +2 stage runs and stays. The card says 2 turns. |
| Broadcast | `broadcast` | None / Rare / 2 AP | Ungranted | Next move hits all foes, then consumed. Duration 2 and not in `IsNextActionStatus`, so the extra turn of duration is what keeps it alive past the granting end step. |
| Staggered | Modifier `6.1-c` Suppress, min rarity Rare, on hit, 15–25% | Not a move | n/a | Next move ×0.90. Holds through the granting end step. |
| Surging | Modifier `6.2-c` Crit Feed, min rarity Legendary, on crit | Not a move | n/a | Next move ×1.25. Same hold. |
| DamageIntercept | `damage_intercept` (None / Uncommon / 2 AP), `signal_redirect` (Node / Uncommon / 2 AP, also Def +1) | Both ungranted | n/a | Redirects the next single-target hit. In `IsNextActionStatus`. |
| Recharging | See §3.4 | | | Blocks that move until the timer hits 0. Ticks on the bench. |
| Slowed | Also Tripwire, above | | | |

Spite Counter is a secondary producer of whatever General debuff a move just landed. It does not create a status that nothing else applies.

### 3.3 Orphans and barred rows

| Status | Defined | Resolver | Class |
|---|---|---|---|
| Rekt | CSV + asset, General, duration 2 | 25% lose action. `rektLoseActionChance` 0.25. | orphan, body runs |
| Offline | CSV + asset, General, duration 2 | Sleep. `skippingNextTurn`. Wakes on damage. | orphan, body runs |
| GlitchedMind | CSV + asset, General, duration 2 | 50% mis-act. `glitchedMindMisactChance` 0.5. | orphan, body runs |
| HODLed | CSV + asset, General, duration 3, buff | Repeating MeleeDef +1 (`applyOnce` false) plus another +1 while Def < 3. No immunity. Neither write reverts. | orphan, body runs and overstays |
| Unsealed | CSV + asset, General, duration 2 | 50% per hit to bypass a single-hit barrier. Not consumed on a failed roll. | orphan, body runs |
| Relay | CSV + asset, General, Consumed, duration 1 | Full barrier bypass, consumed ahead of Unsealed. Not in `IsNextActionStatus`, so a duration of 1 would expire on the granting end step even if a move applied it. | orphan, body runs, duration trap |
| Amplified | CSV + asset, Consumed, duration 1 | Next move ×2 if present. In `IsNextActionStatus`. | barred, engine-ready |
| Overclocked, Bridged, Firewalled, Halted, Crashed, Evasive, Narrowcast, LiquidityPump | CSV + asset, display names marked Deprecated | No v1 cell. No move. Leftover reads in `TypeChemistryMatrixSO` only. | barred |
| TypeCoating, HypedTypeCoating | CSV + asset | `ApplyCoating` would set `coatingType` if a cell existed. Barred. | barred, engine-ready |
| Reinforced, DustStorm, BridgedOut, ForkedChain, CompressedData, MemeSurge, NodeRelay | `StatusId.cs` only. No CSV row. No asset. | No apply path. `Reinforced` is still named in `COMBAT_Chemistry.txt` §4 and is absent from §7's barred list and from the v1 matrix. v0 had one Reinforced cell. | dead enum |

### 3.4 Recharging producers

`rechargeTurns` > 0 stamps `Recharging` on that move. Because the stamp happens before the same turn's end tick, a column value of 2 locks the **next** turn only, and a column value of 1 expires before the next action.

| Move | Type / rarity | Column | In a pool | What the clock does |
|---|---|---:|---|---|
| `ap_boost` | None / Epic / 0 AP, +2 AP | 3 | Ungranted | Two upcoming turns locked. |
| `extend_protocol` | Node / Epic / 1 AP | 2 | Ungranted | One upcoming turn. Extends one General buff by 1. |
| `all_in` | Meme / Epic / 4 AP, power 98 | 2 | Balloom, also off-type | One upcoming turn, this move only. |
| `bean_sip` | Staked / Epic / 1 AP | 2 | Cofi exclusive, `NO_CANDY` | One upcoming turn. Boss-only. |
| `fox_feint` | Bot / Epic / 1 AP | 999 | Kitsauto exclusive and flagship | Also `TriggerOnce`. A legal cast stamps a 999-turn recharge on top of the once-per-battle flag. The bench tick spends that clock. |
| `gecko_guard` | Frozen / Legendary passive | 999 | Gilarime exclusive | A passive is rejected as an action, so legal play never stamps this. The recharge check sits after that rejection and does not return, so an illegal cast would stamp it. |

### 3.5 Consumers

Moves and modifiers that remove, move, or scale off a status. "Runs" is the resolver, not the description.

| Consumer | What it claims | What runs |
|---|---|---|
| `cleanse` (None / Common / 1 AP) | Up to 3 General debuffs on an ally | `Cleanse:3` runs. Stages stay. Ungranted. |
| `ore_restoration` (Mined / Uncommon / 2 AP) | Heal 20% and cleanse 1 | Both run. Ungranted. |
| `error_correction` (Glitched / Uncommon / 2 AP) | Heal 15%, 50% cleanse 1 | Both run. Ungranted. |
| `glitch_feedback` (Glitched / Epic / 1 AP) | "Remove 1 target debuff" on an enemy, plus +1 AP surcharge | Both run. It cleanses a **foe** debuff. That helps the foe. |
| `purge_protocol` (Encrypted / Common / 2 AP) | "Cleanse all ally debuffs and grant Def +1" | Utility is empty. The Def +1 stage runs. The cleanse does not. Ungranted. |
| `bug_fix` (Glitched / Common / 1 AP) | Heal 10%, cleanse 1, −20% damage next turn | The 10% heal runs (`HealPercentMaxHP`). Cleanse and the damage cut do not. Ungranted. |
| `effect_relay` (None / Uncommon / 2 AP) | Move one General debuff from an ally to self, cleanse if none | The move runs. The empty branch logs "cleansed" and removes nothing. Removing the debuff does not revert its stages, and the re-apply writes them again on the receiver. |
| `relay_packet` (Node / Uncommon / 2 AP) | Move one General buff or debuff between allies | Same remove-and-reapply hole. |
| Modifier `6.8-f` Cleanse Soft | Chance to clear 1 self debuff | Runs through the same `Cleanse`. |
| Defueled | One harmful chemistry status | Runs. Dusted's owned stages are not reverted. |
| Encrypted Seal | Incoming debuff | Blocks the apply. |
| Isolated | Ally help | Blocks it, and logs `StatusImmune`. |
| Exploit Mark, Staggered, Surging, Amplified, Splinter, Broadcast, Anchored, Siphoned, Firewall, Relay, Unsealed, DamageIntercept, Frozen Assets, Thread Lock, Kindled, Rugged, Leaked, the AP ticks | See §3.1–3.2 | Each of those reads exists. |
| `extend_protocol` | +1 turn on the shortest General buff | Runs. It will extend Gas Spike, Shard Partition, Yield, Broadcast's unused duration, and a future HODLed. It will not extend a debuff. |
| Cipher Burst (Encrypted / Common) | "25% chance to erase one target buff" | Empty payload. Not a consumer. |
| `status_resistance` | "General debuffs expire 1 turn faster" | Still does not run (trigger `None`, empty utility, not an implicit passive). It is now Rare. It is a passive, not a status. No pool lists it. |

No move `ResolveConditions` cell names a status. No passive's `StatusEffects` cell is filled. Kindling is the only passive that applies a status, and it does it in code.

### 3.6 Data says produced, engine applies the token only

These are the rows where a producer is real and the promised effect is not:

| Status | Producer is real | Missing effect |
|---|---|---|
| Jammed | 8 chemistry cells | Acc −2 |
| BacklashType | 2 chemistry cells | Contact DoT |
| BurnedTokens | `deflation_mark`, in Cinislug's pool | "Enemy DoT heals user" |
| Overleveraged | `voltage_rush` in Forkrook's pool, `risk_marker` ungranted | 25% recoil. The constant exists and is unread. |
| Immovable | `immovable_stance`, in Blockwyrm's pool | Forced-swap immunity. There is still no forced-swap move. |
| MEVExtracted | `mev_extract`, test loadout only | The steal. The drain is the part that runs. |

The reverse mismatch, engine body with no producer: Rekt, Offline, GlitchedMind, HODLed, Unsealed, Relay, Amplified, and the coating pair.

---

## 4. Classification

| Bucket | Statuses |
|---|---|
| Orphan, body ready | Rekt, Offline, GlitchedMind, HODLed, Unsealed, Relay |
| Orphan, barred on purpose | Overclocked, Bridged, Firewalled, Halted, Crashed, Evasive, Narrowcast, LiquidityPump, Amplified, TypeCoating, HypedTypeCoating |
| Dead enum, no asset | Reinforced, DustStorm, BridgedOut, ForkedChain, CompressedData, MemeSurge, NodeRelay |
| Boss-only producer | None. Bean Sip and Bean Beam are the only `NO_CANDY` moves. Bean Sip produces `Recharging`, which other moves also produce. No catalog status is boss-exclusive. |
| Single producer | Kindled (`kindling`), GasSpike (`fuel_surge`), FUD (`verification_lock`), ShardPartition (`shard_partition`), BotSwarm (`bot_swarm`), FiftyOnePercentAttacked (`majority_pressure`), EncryptedSeal (`proof_shield`), ExploitMark (`exploit_window`), BurnedTokens (`deflation_mark`), ThreadLock (`thread_lock`), Yield (`yield_cycle`), Immovable (`immovable_stance`), Broadcast (`broadcast`), MEVExtracted (`mev_extract`), Staggered (`6.1-c`), Surging (`6.2-c`) |
| Two producers | FrozenAssets (`lock_assets` ungranted, `partition_shot` in pool), Overleveraged (`voltage_rush` granted at 25% self, `risk_marker` ungranted at 100%), Slowed (one chemistry cell + Tripwire), DamageIntercept (two ungranted moves) |
| Producer exists only at Epic+ | GasSpike (Mythic), FUD (Epic), ShardPartition (Epic), BotSwarm (Epic), FiftyOnePercentAttacked (Epic), ThreadLock (Legendary), Surging (Legendary modifier). Overleveraged's reliable producer is the Rare; the granted one is the Mythic self-proc. |
| Produced, no payoff | Jammed, BacklashType, BurnedTokens, Overleveraged, Immovable |
| Produced, payoff outlives the status | ShardPartition, Corroded, Shattered's Def −1, Immovable's move-stage Def +2, Burned Tokens' move-stage Atk −1, and any owned status that is cleansed, Defueled, or relayed (FUD, Bot Swarm, 51% Attacked, Dusted) |
| Fragile because ungranted | BotSwarm, FiftyOnePercentAttacked, EncryptedSeal, ThreadLock, MEVExtracted, Broadcast, `lock_assets`, `risk_marker`, both DamageIntercept moves, `proof_shield` |

Frozen Assets is the sharp cross-type case. Frozen's own card (`lock_assets`) is ungranted. The status the Frozen profile names is delivered by Marrowhead's Shard flagship, which is also a power-64 hit. Gilarime's pool does not contain `lock_assets`.

---

## 5. Proposals

Each block is a **PROPOSAL**. Numbers are the proposed numbers unless the line says to keep a value that is already in the data.

### PROPOSAL A. Jammed writes Acc −2 for 2 turns, and the stage ends with the status

`Statuses.csv` row `Jammed`: combat-stage `Accuracy:-2`, apply-once, duration stays 2. The apply path owns it the way Dusted's Evasion write is owned. No matrix edit.

Frozen's profile is speed control and jams. Gas, Encrypted, Compressed, and Bot also spend wins on this cell. Eight cells currently apply a blank. Acc −2 is the summary already on the row. Chemistry fires on the damaging hit, including turn 1, so the typed effect does not wait. Moves with accuracy 0 still force a hit, so this does not touch status utilities.

Risk: those eight matchups gain a real accuracy drop for two end steps. That is the point of filling the blank. DEC-CHEM can stay "do not move cells."

### PROPOSAL B. One Glitched move applies Glitched Mind. Cut Rekt, Offline, and Relay

Glitched's profile names Glitched Mind next to Exploit Mark. Exploit Mark has `exploit_window` (Mined Rare, in Minefolk's pool). Glitched Mind has the 50% mis-act body and no card.

New row, not inserted into Kreechank's five (`data_scramble`, `chaos_strike`, `bug_payoff`, `defense_corrupt`, `unstable_code`). `chaos_strike` is Mythic and `bug_payoff` is Epic, so that pool currently has no Common damaging move. This card does not add or remove a damaging move, and it does not close that gap.

`mind_fault,Mind Fault,StatusUtility,Glitched,Rare,SingleEnemy,None,0,2,0,1,1,0,GlitchedMind:2:1.0:,,UTIL`

Description: "Foe mis-acts half the time for 2 turns."

Rare. A 50% mis-act for two turns is a control button, and Commons are the weak band (power about 30–40, baseline-like). 2 AP is legal on turn 1 after the +2 regen, so the typed effect is available on the first action. Kreechank can equip it once it is known. The five damaging and utility rows already in the pool stay there.

Cut `Rekt`, `Offline`, and `Relay` from `Statuses.csv`. Leave the enum values so old assets still deserialize.

- Rekt's 25% action loss overlaps Glitched Mind and Offline, and no type profile names it.
- Offline's sleep has a body and no profile home. Frozen's lock is Frozen Assets, which has a move.
- Relay is a full barrier bypass. Modifiers `4.3-b` (Legendary) and `4.8-a` (Epic) already set first-hit barrier bypass to guaranteed. The name collides with Effect Relay, Relay Packet, and Charged Relay. Duration 1 is not in `IsNextActionStatus`, so the status would die on the granting end step even after a producer was added. Unsealed stays the partial bypass.

### PROPOSAL C. HODLed gets one owned ramp, then one typeless card. No immunity yet

Do not ship a producer against the current body. On a duration of 3, starting from Def 0, the asset's repeating +1 and the extra +1 while Def is below +3 write: end 1 → +2, end 2 → +3, end 3 → +4. The special case stops at +3. The asset ramp does not. None of it reverts. There is still no stat-drop check in `SetStage`.

Body change first:

- One MeleeDef +1 per end step.
- Stop at +3.
- Remember the sum and revert it when the status ends.
- Refresh still does not reapply the writes.
- Drop "Immune to stat drops" from the catalog summary until immunity exists.

Then, and only then, add a typeless card so Node, Block, Frozen, and Staked can all equip it. A Block-typed move would lock the other three profiles out.

`vault_hold,Vault Hold,StatusUtility,None,Rare,Self,None,0,2,0,1,1,0,HODLed:3:1.0:Self`

Description: "Def rises by 1 at each end step for 3 turns, up to +3, then the extra Def ends."

A rare typeless card is the band that should line up with a typed common. Proposal E makes Block's typed Rare the faster wall (Def +2 for 2 turns). This typeless Rare is the slower climb to +2 and then +3. It costs 2 AP, so it is legal on turn 1, and the ramp is the payoff rather than an immediate wall. Do not insert it into Blockwyrm's five. `bulwark_blow` stays a damaging move.

### PROPOSAL D. Unsealed gets one Shard support

`COMBAT_Statuses.txt` §10.1 says support moves apply it. The 50% bypass body runs. Damage Intercept already has two moves. This is the missing half of that pair.

`seal_crack,Seal Crack,Support,Shard,Uncommon,Self,None,0,2,0,1,1,0,Unsealed:2:1.0:Self`

Description: "For 2 turns, each of your hits has a 50% chance to ignore a single-hit barrier."

Uncommon matches the support floor from the AP ladder (support is at least Uncommon). 2 AP is legal on turn 1. Shard is the blade that wants to cut walls. The typed card is the only move that does the partial bypass. It does not bypass on a miss. Leave Marrowhead's five as they are. `partition_shot` stays the Rare damaging flagship. The pool's Common damaging move is the off-type `precision_strike`.

### PROPOSAL E. Immovable's card becomes the Def +2, and the Def ends with the status

`immovable_stance` is in Blockwyrm's pool. The status is unread. The Def +2 is a move stage and stays forever, while the card says 2 turns.

- Move the +2 onto the status asset as an apply-once MeleeDef stage, and add `Immovable` to the owned-stage set.
- Clear the move's `StageChanges` cell so the +2 is not written twice.
- Description: "Gain Def +2 for 2 turns."
- Keep the move: Block, Rare, 1 AP, Self, `Immovable:2:1.0:Self`.
- Immunity stays off the card until a forced swap exists.

Def +2 is 2.0× for two turns, then it ends. That is a typed Rare wall, legal on turn 1, and it is stronger and faster than Proposal C's typeless ramp. Block's profile (barriers, defense ramp, grounding) matches a timed Def spike better than a promise the engine does not keep.

### PROPOSAL F. Debuff Script becomes the Common Bot Swarm. The Epic stays the spread

`debuff_script` is Bot, Common, 1 AP, single foe, and it writes permanent MeleeAtk −1 and MeleeDef −1. It is in Kitsauto's pool. `bot_swarm` is Bot, Epic, 2 AP, all foes, and the same −1s revert at the end of the turn. It is in nobody's pool.

The Common is the fight-long ratchet. The Epic is the one-turn copy. That is the low-rarity card beating the high-rarity one.

- `debuff_script`: clear `StageChanges`. Set `StatusEffects` to `BotSwarm:1:1.0:`. Description: "Foe loses Atk and Def until the end of this turn."
- `bot_swarm`: keep the Epic, 2 AP, all foes, same status. Description stays the AoE line.
- Do not add `bot_swarm` to Kitsauto's five. Blade Flurry stays the Common damaging move. Packet Skim stays the Common AP denial. The Epic is the spread upgrade a Bot can find later.

Bot's profile line for this status is the short swarm, not a permanent −6 ladder. The permanent ladder on `automated_pressure` is a separate card and is no longer granted. This proposal does not retune that passive.

### PROPOSAL G. 51% Attacked moves to Block, and it stays a one-turn Speed drop

Block's profile lists 51% Attacked with HODLed and Immovable. The only producer is `majority_pressure`, Mined, Epic, 2 AP, ungranted. Mined's key chemistry is FOMO, which already has two cells. The enum comment promises accuracy. The asset is Speed −1 only, and that −1 does revert.

- Change `majority_pressure` to Block, Rare, 2 AP, `AllEnemies`, `FiftyOnePercentAttacked:1:1.0:`.
- Description: "Foes lose Speed until the end of this turn."
- Do not add an accuracy drop. The asset never had one.
- Do not insert it into Blockwyrm's five (`bulwark_blow`, `immovable_stance`, `blockade`, `immutable_ledger`, `ice_wall`). Epic was too high for a one-turn Speed −1 that a Common permanent debuff already outclassed in spirit. Rare is the single-job debuff.

### PROPOSAL H. MEV Extracted steals 1, and it stays off Kitsauto's pool

At end of turn, the bearer loses 1 AP and the Chainling who applied `mev_extract` gains 1, if that Chainling is active. If they are benched or gone, the bearer still loses 1 and nobody gains it. Duration stays 2. Thread Lock still blocks the gain.

The move text already says steal. The processor drains. After this, the Rare is a 2 AP swing per tick for two turns. Packet Skim, the Common in Kitsauto's pool, is an immediate drain of 1 at 1 AP. The Rare then does a job the Common does not: it pays the user. Today, before this change, the Common is the better denial, because the Rare only drains, it costs 2, and it waits for the end step. That is the inversion.

The AP ladder kept `mev_extract` and `thread_lock` out of Kitsauto on purpose. This proposal does not put either into the five. Packet Skim remains the turn-1 denial. Thread Lock remains the Legendary regen lock, ungranted, single producer, body already correct. Do not add a second Thread Lock card.

### PROPOSAL I. Burned Tokens heals the Kindled tick only

While Burned Tokens is up, end-step damage from Kindled on that fighter also heals the Chainling who applied `deflation_mark`, for the same integer, capped at that applier's missing HP. If the applier is gone or benched, the burn still happens and nobody is healed. Rugged is not copied.

`deflation_mark` is already in Cinislug's pool next to `kindling`. The Atk −1 stage is a move stage and stays; that matches other move-column debuffs, and it is the part that already runs. The heal is the typed clause the profile names (Burned Tokens). Copying Rugged as well would hand the applier 1/16, then 2/16, then 3/16 of the foe's MaxHP on top of Kindling. That stack is too much for a 1 AP Rare.

The processor should read Kindled's catalog percent (already 0.06) instead of the second literal `0.06f`, so the heal and the burn share one number. That does not change the 6%.

### PROPOSAL J. Overleveraged recoils on the foe. Voltage Rush stops applying it to itself

On the next damaging hit that deals HP damage to the bearer, the bearer loses `floor(damage × 0.25)` more HP, then the status is consumed. No power threshold. The catalog word "heavy" has no number anywhere.

`risk_marker` (Compressed, Rare, 1 AP, single foe, ungranted) stays the producer. Description stays the recoil sentence, with "heavy" removed: "The target's next damaging hit also deals 25% of that damage back to them."

`voltage_rush` drops `Overleveraged:2:0.25:Self`. The Mythic stays Speed +3 at 1 AP, in Forkrook's pool. Description: "Gain Speed +3." Wiring the self token would be a self-damage producer, and self-damage is shelved. The 25% self-proc is the shelved half. The foe punish is not.

Do not insert `risk_marker` into Humpacked's five. Compressed's profile key-status cell is "—". The punish is a Rare tool a Compressed fighter can find, not a second chemistry. Meme stays out of this. No new matrix cell.

### PROPOSAL K. Own Shard Partition and Corroded. Revert stages on every removal

- Shard Partition's Speed +1 is owned and reverts when the 2 turns end. The move stays Shard, Epic, 2 AP, all allies, in Crackett's pool. A permanent Speed +1 on the team is a different card from the one the row prints.
- Corroded remembers each end-step Def −1 and reverts the sum when the status ends. Two ticks still mean Def −2 while it is up.
- `Cleanse`, Defueled, Effect Relay, and Relay Packet revert owned stages, owned combat stages, temporary deltas, and Slowed's Speed on the fighter who lost the status. Effect Relay's empty branch stops saying "cleansed."
- Shattered's Def −1 stays a one-shot stage write. It is called out in the questions because an instant has no status left to expire. This proposal does not change it.

Gas Spike and FUD already revert on natural expiry. This proposal is the remaining hole, including the cleanse path the AP-ladder audit flagged as M4.

### PROPOSAL L. Backlash stays a token

Do not add a contact DoT. Kindling and Rugged already burn. A third percent would be a new burn this snapshot does not contain. Change the catalog summary to "Token. No contact damage." No matrix edit. The two cells stay blank on purpose until a later chart pass.

### PROPOSAL M. Fox Feint's recharge column goes to 0

`fox_feint` keeps `TriggerOnce` and the armed counter. `RechargeTurns` becomes 0. A 999-turn `Recharging` stamp double-locks a once-per-battle move and the bench tick spends it in public. Kitsauto's pool does not change. Blade Flurry stays the Common damaging move.

### PROPOSAL N. Narration for the ticks players cannot see

Expiry and consume stay transcript-only. Putting every expiry on the live page would bury the turn.

These become outcome sentences:

- End-step AP from Pumped, Yield, Drained, and MEV. Today `Gain` / `Drain` land on `APGained` / `APSpent`, which fall through to popup-or-HUD. `APStolen` is already an outcome line, and MEV does not use it. After Proposal H, the steal sentence should name both fighters.
- FOMO's self DoT, Shattered's chip, and Cascade Failure's burst. Rugged ("Rugged scorches"), Kindled ("Kindled burns"), and Leaked ("Leaked taxes") already go out as `HPDotDamage` with a verb. FOMO, Shattered, and Cascade use `LogMessage` text that leads with an instance id or a label, not that verb pattern.
- Effect Relay's empty branch, once it stops claiming a cleanse.
- The recharge log prints `rechargeTurns - 1` upcoming turns, so All-In and Bean Sip say one turn when the column says 2.

`PassiveTriggered` still uses "{actor}'s passive activated." The event message already names the passive. That template gap is unchanged from the October 17 note. It is real and it is not a status-producer fix, so it waits.

### What this round leaves alone

- Amplified and the other §7 migration ids. No producer.
- The seven dead enum ids. No producer, no asset.
- The Yield / Pumped split.
- Kindling's 6%. Proposal I only unifies the literal with the catalog.
- Jammed's cells. Proposal A fills the body and does not move the chart.
- Meme chemistry. Staggered and Surging stay modifier procs. No Meme status move, no Meme matrix cell.
- Thread Lock's body and its absence from Kitsauto's pool.
- Uplink, and the decision that Node's passive pays the ally.
- Self-damage on Voltage Rush, Recoil Strike, Volatile Shot, and All-In.
- Cipher Shot, Precision Strike, Interest Strike, Hammer Sync, and Bean Sip's missing +1 AP. Those are decided and not in this data. They are not given new statuses here. Cipher Shot's buff-block is the one that might want a status id; that is a question, not a card.

---

## 6. Timing, tick order, narration

### End step, this snapshot

For each living active, `ProcessEndOfTurnForTeam`:

1. `StatusProcessor.ProcessEndOfTurn`
2. If a buff count dropped, `OnBuffExpired` passives
3. `EndOfTurn` passives
4. Mitigation, accuracy, reactive AP, and barrier timers, then armed windows

Then `TickBenchRecharge` for everyone not in `activeIndices`.

Inside `ProcessEndOfTurn`:

1. Kindled damage. Return if that KOs.
2. Other end-step damage (not Kindled, not Leaked), Drip, status AP, repeating stages, the extra HODLed +1.
3. Duration `Tick`, then revert owned stages, then remove. Thread Lock's reapply gap is stamped here (`threadLockBlockedUntilTurn = turn + 2`).

There is still no separate chemistry phase at end of turn. Chemistry statuses tick in that same status pass. End-of-turn passives run after the duration tick. That is the opposite of "passive ticks before status ticks." Kindled is the only passive burn pulled in front of Rugged. Patience, regen, Automated Pressure, Defensive Reflex's drain, Meme Momentum, and Stake Refund still run after the status pass.

On a damaging hit, modifier setup still runs before `ChemistryResolver.Resolve`. Kindling is applied after HP is subtracted, only if the foe is still alive. `OnHit` passives run after that. The hit order is not the end-step failure. The end step is.

Thread Lock, specifically: the queued apply lands before regen, so the locked turn's regen is blocked, and status AP ticks on that turn still see the lock because they run before the duration tick. The lock then expires, and an `EndOfTurn` passive can gain AP anyway. Double-or-Nothing is the case the October 17 note already named. It is still the case.

### Stages that end, and stages that do not

Owned, and reverted on a natural expiry: Dusted (Speed and Evasion), Bot Swarm, 51% Attacked, FOMO's Atk, Slowed, Gas Spike, FUD. Refresh does not reapply those writes.

Not owned, so they stay: Shard Partition's Speed +1, Corroded's repeating Def, HODLed's Def writes. Shattered's Def −1 and every move-column stage (`immovable_stance` Def +2, `deflation_mark` Atk −1, `voltage_rush` Speed +3, Bean Sip's Speed +1, `signal_redirect` Def +1) were never status deltas.

Removed without a revert: Cleanse, Defueled (except Slowed and a temporary delta), Effect Relay, Relay Packet. A cleansed FUD keeps Atk −1, Def −1, and Speed −1, which makes the new ownership on FUD leak the moment someone presses Cleanse.

### Recharge

Bench recharge matches the rule. Other statuses still pause on the bench. A queued Thread Lock or Frozen Assets is applied in the all-roster pending pass even if that fighter is benched, and then it pauses, because only actives take `ProcessEndOfTurn`. Swapping out of a queued lock delays it instead of spending it.

`fox_feint`'s 999 and the "(2 turns)" log line are the remaining recharge smells. Proposal M and Proposal N cover them.

### Narration gaps that are status-shaped

Called out on a live or consequence page: damage, heals, misses, barriers, KOs, swaps, status applied / refreshed / immune / cleansed, stage changes, `PassiveTriggered`, `ModifierTriggered`, AP gain blocked, AP stolen, recharge start.

Not called out as a live line:

- `StatusExpired`, `StatusConsumed`, `MoveRechargeComplete` are transcript-only. The battle log can say the status ended. The live page does not have to. Proposal N keeps that.
- Status AP ticks are popup-or-HUD. A Pumped fighter silently has more AP.
- FOMO, Shattered, and Cascade do not get the verb sentence Rugged, Kindled, and Leaked get.
- Glitched Mind's mis-act, Rekt's lost action, Offline's sleep, and Thread Lock's "no AP gain" are `SystemMessage`, which falls through to popup-or-HUD. If Proposal B adds Mind Fault, the mis-act line has to become an outcome sentence or the card will look like it did nothing.
- Effect Relay can announce a cleanse that did not happen.
- The passive template still hides the passive's name.

---

## 7. Where a lower rarity beats a higher one

1. **Debuff Script (Bot Common, in Kitsauto's pool) beats Bot Swarm (Bot Epic, ungranted).** Permanent Atk −1 and Def −1 against a one-turn copy of the same pair. Proposal F flips that: the Common applies the status, the Epic is the AoE.
2. **Packet Skim (Bot Common, in the pool) beats MEV Extract (Bot Rare, ungranted) at the job MEV currently does.** Skim drains 1 now for 1 AP. MEV drains 1 at end of turn for 2 AP and does not pay the user. Proposal H makes the Rare a steal, which Skim is not. Thread Lock (Legendary) is still the regen lock, and Skim does not beat that.
3. **Risk Marker (Compressed Rare, ungranted, 100% on the foe) is the real Overleveraged producer. Voltage Rush (Forked Mythic, in Forkrook's pool) is a 25% self-proc of a token.** Once the body exists, the Rare is the card and the Mythic's token would be self-damage. Proposal J deletes the Mythic's token and keeps the Rare.
4. **Partition Shot (Shard Rare flagship, power 64) is the live Frozen Assets producer. Lock Assets (Frozen Rare) is ungranted.** Same rarity, so this is not a rarity inversion. It is the Frozen identity status arriving on a Shard damaging move that also deals a spine hit. Gilarime never learns the Frozen card. Called out because a pool can feel the status without the type that owns it.
5. **Coordinated Shot's live rider versus Hammer Sync** is a damage-rider inversion (Common +15% per buffed partner against Legendary +10%), not a status inversion. The decided Hammer Sync shape is +20% only when the ally is buffed. This note does not retune it.

`automated_pressure` (Bot Rare, cap −6, ungranted) would still beat Bot Swarm if someone equipped both. It is not in a pool, so it is not the live inversion. Proposal F does not touch it.

---

## 8. Questions only Cronus can answer

1. End of turn: should `EndOfTurn` passives move ahead of the status duration tick, or should the written rule change to the order this snapshot runs (status, then passives)? Kindled can stay in front of Rugged either way.
2. Jammed: is Acc −2, reverted after 2 turns, the body (Proposal A), or should those eight cells stay tokens until a chart pass?
3. HODLed immunity to stat drops: still part of the status, or only the Def ramp in Proposal C?
4. Should the HODLed card be typeless, so Node, Frozen, and Staked can equip it, or should Block own a typed one and the other three wait?
5. Overleveraged: foe recoil at 25% with Voltage Rush's self token removed (Proposal J), or retire the status and give `risk_marker` a different Compressed effect?
6. Burned Tokens: heal the Kindled tick only (Proposal I), or take the sentence off `deflation_mark` and cut the status?
7. 51% Attacked is a Block key produced by an ungranted Mined Epic. Is moving that move to Block Rare (Proposal G) right, and should Blockwyrm ever give up `ice_wall` for it?
8. Thread Lock, MEV Extract, and Proof Shield are real and ungranted. The ladder kept Thread Lock and MEV off Kitsauto. Is candy-only the long-term home, or does a later pool slot open?
9. Glitched Mind at Rare and 2 AP (Proposal B): is Rare the right band for a 50% mis-act, or should the key status be Uncommon so it shows up earlier?
10. Shattered's Def −1 is permanent because the status is instant. Should that stage stay, or should it be a short owned drop?
11. Cipher Shot's buff-block is a flag-shaped effect with no status id. Should it stay a non-status flag (so Cleanse cannot remove it, which matches "until they swap"), or become a catalog status so narration and the status row can see it?
12. Cofi's pool still has no Staked passive and no unique status. Bean Sip is heal, Speed, and recharge. Should the boss stay that way?
13. `purge_protocol` and `bug_fix` describe cleanses the engine does not run. Wire them, or delete the sentences? `glitch_feedback` cleanses a foe's debuff. Was a buff strip the intent?
14. `status_resistance` still does nothing, and it is now Rare. Cut the row, or is a later "debuffs fall off" passive going to reuse the id?
15. Drip heals every end step for 3 turns. The spec still says once (E-04). Which one is the status?

---

## 9. Top recommendations

1. **Proposal K.** Shard Partition (in Crackett's pool) and Corroded still leave their stat changes behind, and Cleanse undoes the Gas Spike / FUD revert the ladder just added. This changes more fights than a new card.
2. **Proposal A.** Eight chemistry cells apply Jammed and change nothing. Fill Acc −2. Leave the chart where it is.
3. **Proposal F.** Kitsauto's Common Debuff Script permanently outclasses the Epic Bot Swarm nobody is granted. Make the Common the timed status and leave the Epic as the spread.
4. **Proposal B.** Glitched Mind is the profile's key General status and has no card. Rekt, Offline, and Relay are unused bodies stacked on top of it and on top of modifier bypass.
5. **Proposal E.** Immovable Stance is in Blockwyrm's pool, promises a 2-turn immunity the resolver never reads, and leaves Def +2 in place forever. Make the status the timed Def +2.
6. **Proposal H.** MEV's Rare card loses to Packet Skim until it actually steals. Keep it off Kitsauto's pool so the Common stays the starter denial and the Rare is the upgrade.
7. **Proposal I, with J only if the shelf stays shut.** Burned Tokens is in Cinislug's pool and heals nothing. Overleveraged's only granted producer is a self-proc; do not turn that into self-damage. The foe-facing Rare can carry the 25% if the status stays.
8. **Proposal N.** Pumped, Yield, Drained, MEV, FOMO, Shattered, and Cascade change the fight on lines the live page does not have to say. Expiry can stay in the log.

Proposal C (HODLed) waits on the body fix and on questions 3 and 4. Proposal D (Unsealed) is a real missing card and can land with B. Proposal G waits on question 7. Proposal L is a one-line catalog honesty fix. Proposal M is a one-cell recharge cleanup on a move Kitsauto already has.

---

## Sources checked

Snapshot root: the unzipped `chainbound_combat_snapshot_20261018_1014` archive. The producer table can be regenerated with `status_producer_check.py` against that tree. The script parses the CSVs and the v1 matrix. The engine-only producer and consumer lists inside it are hand-verified against the files below and will lie if the resolver moves without them.

- `Docs/Combat/Design/PASSIVES_AND_STATUSES_ANALYSIS.md` (baseline; §4–§5 stale where §1 of this note says so)
- `Docs/Combat/Audits/AP_COST_LADDER_IMPLEMENTATION_AUDIT.md` (bench recharge, Gas Spike / FUD ownership, Kitsauto pool, cleanse hole M4, narration M3)
- `Docs/Combat/Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md` (stale on the seven cut ids)
- `Docs/Combat/Design/SELF_DAMAGE_SHELF.md` (no self-damage producers)
- `Docs/Specs/Combat/Data/Statuses.csv` (56 rows)
- `Docs/Specs/Combat/Data/Moves.csv` (209 rows)
- `Docs/Specs/Combat/Data/SpeciesMovePools.csv`
- `Docs/Specs/Combat/Data/Modifiers.csv` (`6.1-c`, `6.2-c`, `6.6-a`, `4.3-b`, `4.8-a`, `6.8-f`)
- `Docs/Specs/Combat/Data/TestLoadouts.csv` (`mev_extract` on `starter_kitsauto_aggro` only)
- `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv` (57 cells)
- `Docs/Specs/Combat/COMBAT_TypeProfiles.md` (key-status column)
- `Docs/Specs/Combat/COMBAT_Chemistry.txt` §4 and §7
- `Docs/Specs/Combat/COMBAT_Statuses.txt` §7.1, §7.5, §10
- `Docs/Specs/Combat/Data/_LEGEND.md`
- `Assets/Scripts/Combat/Runtime/Data/StatusId.cs` (63 ids plus `None`; 7 have no CSV row)
- `Assets/Scripts/Combat/Runtime/Data/StatusInstance.cs` (`IsNextActionStatus`, owned deltas, `Tick`)
- `Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs` (end step, owned set, Cleanse, HODLed, Kindled literal)
- `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs` (end step, bench recharge, Rekt, Glitched Mind, chemistry order, Kindling, Staggered, Surging, Tripwire, Damage Intercept, Relay, Unsealed, swap, leaked tax, recharge stamp)
- `Assets/Scripts/Combat/Runtime/Engine/ChemistryResolver.cs` (instant Shattered and Defueled, Slowed, coatings, Cascade)
- `Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs` (Staggered, Surging, Amplified, Exploit Mark)
- `Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs` (Thread Lock)
- `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs` and `CombatNarration_Default.asset` (`eventRules: []`)
- `Assets/Data/Combat/StatusDef/StatusDef_*.asset` (56 defs; Jammed, GasSpike, FUD, ShardPartition, HODLed, Corroded, Dusted, MEV, Overleveraged, BurnedTokens, Immovable, Backlash, Kindled, Leaked read directly)
- `Assets/Data/Combat/Moves/Move_cipher_shot.asset`, `Move_precision_strike.asset`, `Move_interest_strike.asset`, `Move_hammer_sync.asset`, `Move_bean_sip.asset`, `Move_all_in.asset`
- `Assets/Scripts/Combat/Tests/CombatRuntimeFixTests.cs` (bench pause vs bench recharge)
- `Assets/Data/Combat/Config/CombatResolverConfig_Default.asset` (Rekt 0.25, Glitched Mind 0.5, Firewall 0.30, Drip 0.06, FOMO 0.0625, Shattered Def −1)

Not read: anything under `Docs/GameTheory/`.
