# Battle effects correctness audit

Date: 2026-10-16. Snapshot: combat CSVs under `Docs/Specs/Combat/Data/`, the live chemistry matrix `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv`, and the Unity resolver in `Assets/Scripts/Combat/Runtime/Engine/`.

This audit was written before the follow-up. The original findings below stay as the record of what was observed. The decision round is the current status.

## Decision round (2026-10-16)

Developer answers applied on this branch. Stamp modifiers `3.2-b`, `3.2-c`, `7.2-c`, `10.1-d`, `10.2-b`, and `10.2-c` were not touched.

| Finding | Status |
|---|---|
| E-01 Bean Sip | **Fixed.** `rechargeTurns` stays 2, which skips the next turn. The log and the card say 1 turn. The move also grants +1 AP. |
| E-02 Other recharge wording | **Fixed for the sentence.** AP Boost and Extend Protocol stay at 3 and the card and log say 2 skipped turns. Fox Feint stays at 999 and the log says it cannot be used again this battle. Gecko Guard's unused 999 was left on the passive. |
| E-03 Slowed action-loss label | **Deferred.** |
| E-04 Drip "once" | **Deferred.** |
| E-05 Cascade "KO" comment | **Deferred.** |
| E-06 FUD Accuracy comment | **Deferred.** The stages now revert with the status (E-15). The Accuracy word was not added. |
| E-07 Jammed Acc -2 | **Deferred.** Listed in [UNIMPLEMENTED_MOVE_EFFECTS.md](UNIMPLEMENTED_MOVE_EFFECTS.md). |
| E-08 Leaked | **Deferred.** Same doc. |
| E-09 Backlash | **Deferred.** Same doc. |
| E-10 Dusted | **Partial.** Speed -3 reverts with the status. Eva -3 was not added. |
| E-11 HODLed ramp | **Deferred.** The double ramp and the missing immunity were not retuned. Owned stages revert if it is ever applied. No producer was added. |
| E-12 MEV Extracted drain vs steal | **Deferred.** |
| E-13 Burned Tokens | **Deferred.** |
| E-14 Overleveraged | **Deferred.** |
| E-15 Timed stages | **Fixed.** Apply-once and repeating stages are owned by the status instance and revert when it expires. General refresh sets `turnsRemaining` to `max(current, new)` and does not reapply apply-once stages. Chemistry refresh sets `turnsRemaining` back to `defaultDuration` and does not reapply stages. Sear Field is status `SearField` (310) for the granting turn. Immovable's Def +2 lives on the Immovable status. |
| E-16 Ice Wall comma | **Fixed as an import bug.** The description is quoted and both opcodes import. The restored mitigation `hitBudget` is still 0, so the wall does not mitigate. The speed-based +1 AP opcode is present. |
| E-17 Hit budget 0 | **Deferred.** Ash Guard, Compound Guard, and Ice Wall. No hit count was invented. |
| E-18 Empty Acc/Eva | **Deferred.** Listed per move in the unimplemented doc. |
| E-19 Missing payloads | **Deferred.** Same doc. Extend Protocol's recharge sentence was fixed; the extend clause was not implemented. |
| E-20 Streamlined Flow double Def | **Deferred.** |
| E-21 Compression Burst every hit | **Deferred.** |
| E-22 Group Hug | **Fixed by replacement.** The percent-copy is gone. After a Support move resolves, each living active ally gains +1 AP. The host move stays single-target. |
| E-23 Six stamp modifiers | **Deferred.** Another agent owns them. Not edited. |
| E-24 Tripwire | **Deferred.** |
| E-25 Charged Relay ledger timing | **Deferred.** |
| E-26 FUD and Reinforced stages | **Fixed as the shared revert rule.** FUD's stages revert. Reinforced still has no producer; if applied, Def +3 reverts. |
| E-27 No producer | **No content change.** Findings per status are in [CATALOG_STATUSES_WITHOUT_PRODUCERS.md](CATALOG_STATUSES_WITHOUT_PRODUCERS.md). |
| E-28 Passive target override | **Deferred.** |
| E-29 Bench recharge | **Fixed.** Recharge keeps ticking on the bench. Other statuses still pause. Spec §1.2 point 6 and the changelog match. `BenchPausesStatusDurationUntilSwapIn` was replaced. |

Open item from E-01: Cofi's encounter is `bean_sip` plus `civet_beam` (Bean Beam, power 12, 2 AP). Utility AI has no 6 AP check and no stronger Bean Beam. None was added. Once Cofi reaches 6 AP the kit still only has the power-12 beam.

The duration clock below describes the pre-follow-up log and the old bench pause. Player-facing recharge text is now `rechargeTurns - 1` upcoming turns, and benched recharge ticks. See the table above before using those paragraphs as current behavior.

## Counts

| | |
|---|---|
| Root findings | **29** |
| Wording | **6** (E-01–E-06) |
| Bug | **19** (E-07–E-25) |
| Design question | **4** (E-26–E-29) |
| Move rows called out | **75** of 206 |
| Moves whose described effect does not run | **20** |
| Moves that still do something, with a missing or miscounted clause | **55** |

The 75-row table is under [Move rows called out](#move-rows-called-out). The other 131 moves were checked for an empty payload against a description that names an effect, and for recharge wording. Flavor-only damaging moves whose power, accuracy, and hit counts are what `DamageCalculator` uses are treated as matching. This pass did not play them out in a battle.

## How this was checked

Battle math is local and deterministic. `CombatResolver` applies moves, `StatusProcessor` ticks statuses, `ChemistryResolver` applies one matrix output per cell, and `UtilityEffectExecutor` runs utility opcodes. There is no second resolver in `backend/chainbound_game`. `backend/Chainbound.BattleService/manifests/gameplay-manifest.json` is an exported snapshot of the move assets (Bean Sip is `rechargeTurns: 2` at that file around the `bean_sip` object). Supabase does not store a separate move table that the battle reads.

Authoring source, per `Docs/Specs/Combat/Data/_LEGEND.md`: the CSVs import into ScriptableObjects. Move `StageChanges` and status `StageEffects` use `CoreStat` only (`MaxHP`, `MeleeAtk`, `MeleeDef`, `RangedAtk`, `RangedDef`, `Speed`). Accuracy and Evasion are `CombatStageStat` values. They can be written by utility `ChangeStage` or `FieldEntryStageChange`. They cannot be written by the status stage column or the move stage column. The importer skips a stage token that does not parse as `CoreStat` (`Combat2DataImporter.ParseStatusStageEffects`).

Limits of this pass:

- Unity tests were read, not executed. The recharge conclusion uses the source of `TwoTurnRechargeTicksOncePerActiveEndOfTurn`.
- Full CSV-to-asset parity was not re-diffed. Spot checks: `Move_bean_sip.asset`, `Move_ice_wall.asset`, `Move_ash_guard.asset`, `Move_compound_guard.asset`, the chemistry matrix status ids, and the manifest copy of Bean Sip.
- `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v2.csv` and `Docs/Combat/COMBAT_CHEMISTRY_MATRIX_PASS_PLAN.md` are plans. The live kit is matrix v1 (57 filled cells).
- Modifier sections that the resolver handles by prefix (`2.1` power, `2.2` accuracy, `2.3` crit, `5.2` stat substitution, `6.4` in `SwapModifierResolver`, `8.1` in `PassiveMaxHpUtility`) were not re-measured number by number. Findings call out sections with no effect body.

## Inventory

| Set | Count | Where |
|---|---|---|
| Moves | 206 | `Docs/Specs/Combat/Data/Moves.csv`, assets under `Assets/Data/Combat/Moves/` |
| Status catalog rows | 62, of which 8 are deprecated | `Docs/Specs/Combat/Data/Statuses.csv` |
| Chemistry cells | 57 | `COMBAT_Interaction_Matrix_v1.csv` and `TypeChemistryMatrixSO` |
| Modifier sections | 104 | `Docs/Specs/Combat/Data/Modifiers.csv` |

Move categories: StatusUtility 77, DamagingRanged 39, DamagingMelee 33, Passive 29, Support 28.

Move rarity: Common 138, Rare 46, Uncommon 20, Epic 2 (`defensive_reflex`, `bulwark_blow`). No Legendary or Mythic moves. Those rarities exist on modifiers.

Deprecated status ids, kept for old saves (`COMBAT_Statuses.txt` §7.6): Overclocked, Bridged, Firewalled, Halted, Crashed, Evasive, Narrowcast, LiquidityPump.

Chemistry v1 cell counts: Jammed 8, Anchored 6, Drained 5, Rugged 5, Pumped 4, Dusted 4, Shattered 4, Defueled 3, Corroded 3, Isolated 2, Backlash Bot Dmg 2, Splinter 2, Leaked 2, FOMO 2, Firewall 1, Slowed 1, Drip 1, Siphoned 1, Cascade Failure 1.

Moves that apply a status token today: FrozenAssets 2, Overleveraged 2, and one each of Broadcast, GasSpike, Immovable, EncryptedSeal, FUD, BurnedTokens, FiftyOnePercentAttacked, ExploitMark, Yield, BotSwarm, MEVExtracted, ThreadLock, ShardPartition.

Recharge column greater than 0: `ap_boost` 3, `extend_protocol` 3, `gecko_guard` 999, `fox_feint` 999, `bean_sip` 2.

## Duration clock

`StatusProcessor.ProcessEndOfTurn` runs the end-of-turn effect, then calls `StatusInstance.Tick`. A duration of N therefore includes the granting turn's end tick, unless the status calls `HoldThroughGrantTurn`.

`StatusInstance.IsNextActionStatus` holds Staggered, Surging, Splinter, Amplified, and DamageIntercept through the granting turn. `Recharging` is Consumed and is not in that set, so it ticks on the turn the move was used.

Worked example, locked by `CombatRuntimeFixTests.TwoTurnRechargeTicksOncePerActiveEndOfTurn`:

1. Turn T: the move resolves, then the resolver adds `StatusId.Recharging` with `turnsRemaining = move.rechargeTurns` and logs that number.
2. End of turn T: the status ticks. For `rechargeTurns = 2`, remaining becomes 1.
3. Turn T+1: `IsRecharging` still blocks that move.
4. End of turn T+1: the tick expires it. The move can be selected on turn T+2.

So authored `2` means "skip the next turn." Authored `3` means "skip the next two turns," and the move is selectable on the third turn after the use. The log prints the pre-tick number.

The same end-of-turn-before-decrement order applies to damage-over-time, heals, and AP ticks (Yield, Pumped, Drained, Drip, Rugged, FOMO, MEV Extracted). A duration of N produces N procs, including the granting turn.

Mitigation and barrier turn budgets also tick at end of turn (`ProcessMitigationTimers` / `ProcessBarrierTimers` inside `ProcessEndOfTurnForTeam`). A wall with `turnBudget` 2 is active for the rest of the granting turn and the whole next turn. Printing "2T" matches that reading, because the current turn still receives the protection. Recharge is the case where the current turn cannot be "used" again, so printing the pre-tick count reads as one turn too many.

`hitsRemaining == 0` means a mitigation source is inactive (`ChainlingInstance.IsMitigationSourceActive`). Unlimited hits would be a negative budget. `FullAbsorbBarrier` rewrites a hit budget of 0 into 3. `MitigationWindow` does not.

Thread Lock and Frozen Assets are queued by `IsTurnStartPendingStatus` and applied at the start of the next turn, before baseline AP gain. Duration 1 on those two matches "next turn." They are a different mechanism from recharge.

Bench time pauses duration ticks. `BenchPausesStatusDurationUntilSwapIn` leaves a benched Recharging status at 2 across a full turn, then ticks it after swap-in. `Docs/Specs/Combat/CHANGELOG.md` (2026-09-16) says recharge ticks once per active end of turn and benched durations stay paused. `COMBAT_Resolution.txt` §1.2 point 6 still says switching out does not pause the timer. See E-29.

## Findings

### E-01 — Bean Sip recharge sentence

| | |
|---|---|
| Severity | Wording |
| Also called | Sip Coffee |

**Where.** Move `bean_sip`, display name Bean Sip. Boss species `cofi_lootwak`, encounter `coffee_cofi_lootwak`, exclusive learn in `SpeciesMovePools.csv`. Asset `Assets/Data/Combat/Moves/Move_bean_sip.asset`. Log site `CombatResolver` recharge block (the `MoveRecharging` message). Narration `CombatNarrationComposer` prefers `evt.message` over the template `"{move} began recharging."`.

**Observed.** The asset heals 8% MaxHP, raises Speed +1, costs 1 AP, and sets `rechargeTurns: 2`. There is no move named Sip Coffee. After the move resolves, the resolver logs `Bean Sip now recharging (2 turns)` using `move.rechargeTurns`, then the same turn's end tick drops `turnsRemaining` to 1. The unit test asserts remaining 1 after the use turn and gone after the next full turn. The asset design note says: "Cofi Lootwak charge-up. RechargeTurns 2 skips the next turn after EOT tick so Utility AI leads with sip, then Bean Beam." The heal, the Speed stage, and the priority tags are real. The move resolves immediately. It is a recharge, with a charge-up note for the boss AI, and it does not spend a charge turn before the effect.

**Expected.** From the player's seat the move is locked for one upcoming turn. The design note and the test encode that.

**Suggested fix.** Change the log and the player sentence to the number of upcoming turns the move cannot be selected, `max(0, rechargeTurns - 1)`. Bean Sip should read as recharging for 1 turn, or as skipping the next turn. Leave `rechargeTurns = 2`, the unit test, and the boss note alone unless a real extra lockout is wanted. An extra lockout would also change the Cofi sequence the design note describes.

### E-02 — Same recharge sentence on the other recharge moves

| | |
|---|---|
| Severity | Wording |

**Where.** `ap_boost` (RechargeTurns 3, description "3-turn recharge"), `extend_protocol` (RechargeTurns 3, description "3-turn recharge"), `fox_feint` (RechargeTurns 999, description "Dodge and counter once per battle"), `gecko_guard` (RechargeTurns 999 on a Passive). Spec `COMBAT_Resolution.txt` §1.2: Extend Protocol and AP Boost are "Recharging(3) — after use, 3 turns before reuse."

**Observed.** AP Boost and Extend Protocol log `(3 turns)` and, after the granting end tick, have two turns left. The move is locked for the next two turns and can be selected on the third turn after use. Fox Feint also sets `triggerOncePerBattle`, and its utility window dodges one incoming hit and counters 10% MaxHP (`ArmUtilityWindow` on the asset path described by the CSV). The recharge log still says `(999 turns)`. Gecko Guard is Passive. `CombatResolver` rejects a passive used as an action before the recharge block, so the 999 never applies. `triggerOncePerBattle` plus `HPBelowThreshold` 50 is what makes the Def +2 / Speed +1 happen once.

**Expected.** Player text should name upcoming skipped turns. "3-turn recharge" and the spec line "3 turns before reuse" can be read as the pre-tick counter. Fox Feint's once-per-battle line is the behavior that matters. Gecko Guard's 999 is unused data.

**Suggested fix.** Use the same player-facing count as E-01: AP Boost and Extend Protocol skip the next 2 turns. Fox Feint's sentence should say once per battle and omit 999. Clear Gecko Guard's recharge column, or stop treating it as a recharge inscription in §1.2. Rewrite §1.2 so "3" means either the authored counter or the skipped turns, and pick one.

### E-03 — Slowed label says a 25% action loss

| | |
|---|---|
| Severity | Wording |

**Where.** `ChemistryStatusPresentation` compact label for Slowed: `SPD -2 / 25% LOSS`. `CombatConstants.SLOWED_LOSE_ACTION_CHANCE = 0.25` is copied onto `CombatResolverConfigSO.slowedLoseActionChance` and hashed by `BalanceConfigHashUtility`. No resolver read of that field was found. Spec `COMBAT_Statuses.txt` §7.6: "Slowed: Duration 2. Speed -2 only. It does not roll an action skip."

**Observed.** `ChemistryResolver` applies Speed -2 as a temporary stage and reverts it when Slowed expires. The action-skip chance is unused.

**Expected.** The compact label should match the spec and the stage write.

**Suggested fix.** Change the compact label to Speed -2. Leave the unused constant until someone decides to delete it, so a later reader does not wire the 25% back in by mistake.

### E-04 — Drip "once" versus every end of turn

| | |
|---|---|
| Severity | Wording |

**Where.** `COMBAT_Statuses.txt` §7.6: "Drip: Duration 3. Heal 6% MaxHP once at End of Turn." Catalog row: "Heal 6% MaxHP at End of Turn," duration 3, and the heal is hardcoded in `StatusProcessor` from `dripHealPercentMaxHp` (0.06), not from the end-of-turn damage column.

**Observed.** The heal runs on every end-of-turn pass while Drip is active, before the duration tick. Duration 3 yields three heals, including the granting turn. `CombatChemistryReactionRedesignTests` exercises that end-of-turn heal.

**Expected.** The catalog and the runtime agree with each other. The spec's "once" does not.

**Suggested fix.** Change §7.6 to "6% MaxHP at each End of Turn for 3 ticks, including the turn it is applied."

### E-05 — Cascade Failure comment says KO

| | |
|---|---|
| Severity | Wording |

**Where.** `ChemistryResolver.ApplyCounterChemistry`, comment "Cascade Failure: KO on 3rd apply." Constant `CASCADE_FAILURE_MAX_HP_DAMAGE = 0.25`. Spec §7.6: third application deals 25% MaxHP and resets. Swap-out clears the counter (`CombatResolver` swap reset).

**Observed.** The third application subtracts 25% MaxHP. That can KO if the result is 0. It is not a scripted KO. The spec and the percent agree.

**Suggested fix.** Replace the comment with the 25% MaxHP line from the spec.

### E-06 — FUD comment lists Accuracy

| | |
|---|---|
| Severity | Wording |

**Where.** `StatusId.FUD` comment says Atk/Def/Acc/Spd. Catalog stages are `MeleeAtk:-1|MeleeDef:-1|Speed:-1`. Accuracy is not a `CoreStat`, so it would be skipped on import even if it were added in that column.

**Observed.** Verification Lock applies FUD and a real `NextMoveAPSurcharge` of 1. The stage drop is Atk, Def, and Speed. See E-26 for whether those stages should expire.

**Suggested fix.** Drop Accuracy from the `StatusId` comment, or add an Accuracy utility if the comment is the design.

### E-07 — Jammed does not lower Accuracy

| | |
|---|---|
| Severity | Bug |

**Where.** Eight matrix cells. Catalog: "Accuracy down (Acc -2)," duration 2, empty stage column. `StatusId.Jammed` comment says Acc -2. `ChemistryResolver` has no Jammed branch.

**Observed.** The cell applies the status token for 2 turns. No stage, damage, or accuracy source is written.

**Expected.** Acc -2 for the duration, then a revert.

**Suggested fix.** Apply a temporary Accuracy stage (the swap path already has `AddAccuracySource` for timed accuracy) and revert it on expiry. Accuracy cannot be authored in `StageEffects` until that column accepts `CombatStageStat`.

### E-08 — Leaked's summary and its number disagree

| | |
|---|---|
| Severity | Bug |

**Where.** Two matrix cells. Catalog summary: "On action: 1/10 HP to all." The same row sets `EoTDamagePercent` to 0.1. The importer sets `hasEndOfTurnDamage` when that percent is above 0. `StatusProcessor` then deals `MaxHP * percent` to the bearer at end of turn.

**Observed.** Leaked is a 10% MaxHP self damage-over-time for 3 ticks, including the granting turn. Nothing charges 1/10 HP to act, and nothing hits "all."

**Expected.** The summary describes an action tax on the field. The numeric column describes a self damage-over-time. The runtime follows the number.

**Suggested fix.** Pick one. If the summary is the design, stop setting the end-of-turn percent and implement the action tax. If the number is the design, rewrite the summary to "10% MaxHP damage at each End of Turn for 3 ticks."

### E-09 — Backlash has no contact damage-over-time

| | |
|---|---|
| Severity | Bug |

**Where.** Two matrix cells, "Backlash Bot Dmg." Catalog: parameterized, "Contact adds typed DoT," duration 3, no end-of-turn percent, no stages. `ChemistryResolver` has no Backlash branch. `StatusId.BacklashType` documents the contact DoT.

**Observed.** The status token is applied. Contact hits do not add a typed tick.

**Expected.** Contact against that bearer adds a typed damage-over-time.

**Suggested fix.** Add the contact tick in the damage path, using the parameterized type already stored on `StatusInstance.parameterType`.

### E-10 — Dusted is a permanent Speed -3, and Evasion is missing

| | |
|---|---|
| Severity | Bug |

**Where.** Four matrix cells. Catalog: "Eva -3 and Spd -3," stages `Speed:-3`. With no third field, `ParseStatusStageEffects` sets `applyOnce` true. Apply-once stages are written in `ApplyStatus` and are reverted only for FOMO, Slowed, Bot Swarm, and Majority Pressure (`FiftyOnePercentAttacked`).

**Observed.** Speed drops by 3 once and stays down after Dusted expires. Evasion is not representable in that column, and no Evasion write exists for Dusted.

**Expected.** Eva -3 and Speed -3 while the status is up, then both revert.

**Suggested fix.** Store both deltas as owned stages and revert them on expiry. Evasion needs the same schema extension as E-07.

### E-11 — HODLed ramps twice and does not stop at +3

| | |
|---|---|
| Severity | Bug |

**Where.** Catalog: "Immune to stat drops; Def ramps +1/turn," stages `MeleeDef:1:false`, duration 3. The `:false` makes `applyOnce` false, so the generic end-of-turn stage loop adds +1 every tick with no cap. A second block in `StatusProcessor` adds another +1 while MeleeDef is below 3. No move and no v1 cell applies HODLed. No stat-drop immunity check was found.

**Observed.** Starting from 0: tick 1 goes to +2 (loop + special), tick 2 goes to +3 (loop only; special sees 3), tick 3 goes to +4 (loop has no cap). The stages are not owned, so they remain after expiry.

**Expected.** The summary says immunity plus +1 Def per turn, capped at +3.

**Suggested fix.** One ramp, cap +3, revert or stop on expiry, and a real immunity check. Also decide a producer (E-27) before the status can appear in a battle.

### E-12 — MEV Extracted drains and does not steal

| | |
|---|---|
| Severity | Bug |

**Where.** Move `mev_extract`: "steal 1 AP per turn for 2 turns," status `MEVExtracted:2`. Catalog: "Steal 1 AP per turn," `EoTAPChange` -1. `StatusProcessor` calls `CombatApService.Drain` on the bearer when the change is negative. `Steal` is a different AP path and is not used here.

**Observed.** Duration 2 produces two end-of-turn drains of 1 AP on the target, including the granting turn. The user's AP does not increase.

**Expected.** Each tick moves 1 AP from the target to the user.

**Suggested fix.** Call the steal path with the move's user as the recipient. Keep two ticks if "for 2 turns" should follow the duration-N clock, and say so in the tooltip.

### E-13 — Burned Tokens is inert

| | |
|---|---|
| Severity | Bug |

**Where.** Move `deflation_mark`: "Apply Burned Tokens so enemy DoT heals you; lower Atk -1." Status `BurnedTokens:2` plus stage `MeleeAtk:-1`. Catalog: "Enemy DoT heals user." No resolver branch reads `StatusId.BurnedTokens`.

**Observed.** The Atk -1 lands and does not expire (apply-once, not owned). The token does not convert enemy damage-over-time into healing.

**Expected.** While the token is up, damage-over-time on that foe heals the user.

**Suggested fix.** On the end-of-turn damage path, if the damaged unit has Burned Tokens, heal the source. Say whether the Atk drop is permanent.

### E-14 — Overleveraged is inert

| | |
|---|---|
| Severity | Bug |

**Where.** `voltage_rush` (25% chance, status duration 2) and `risk_marker` (100%). Catalog: "Next heavy hit causes recoil." `COMBAT_Statuses.txt` §7.5: "Overleveraged recoil: 25% of damage dealt." No damage-path read of `StatusId.Overleveraged` was found. Voltage Rush's Speed +3 stage is real.

**Observed.** The token is applied. Landing or taking a hit does not add recoil.

**Expected.** The next heavy hit recoils for 25% of damage dealt, then the Consumed status is spent.

**Suggested fix.** In the damage path, on a hit that meets the "heavy" rule, deal 25% of that damage as recoil and consume the status. The catalog duration 2 on a Consumed status also needs a decision: next-hit, or two turns of exposure.

### E-15 — "For N turns" stages that never revert

| | |
|---|---|
| Severity | Bug |

**Where.** Apply-once stages stick. Owned or temporary deltas revert. Owners today: FOMO, Slowed, Bot Swarm, Majority Pressure.

| Move or status | Text | What sticks |
|---|---|---|
| `fuel_surge` / Gas Spike | "Atk +2 and Def -2 for 2 turns" plus a real +3 AP | Atk +2 and Def -2 stay after the status expires |
| `shard_partition` | "team gains Speed +1 for 2 turns" | Speed +1 stays. AllAllies does fan the status out |
| `sear_field` | "All foes lose Atk -1 for the turn" | Atk -1 is a normal stage change and stays |
| FUD via `verification_lock` | duration 2, "Broad stat pressure" | Atk/Def/Speed -1 stay. The +1 AP surcharge is real. See E-26 |

Bot Swarm and Majority Pressure do revert, and duration 1 plus the granting end tick matches "rest of this turn." Yield duration 3 matches three end-of-turn AP gains. Encrypted Seal duration 1 matches "this turn" as a status window.

**Expected.** A duration in the tooltip is a window, and the stages end with it.

**Suggested fix.** Mark Gas Spike, Shard Partition, and FUD stages as owned deltas and revert them in `RevertChemistryStageEffects`. For move stage changes described as "for the turn" (`sear_field`), either apply a duration status or stop saying the drop expires.

### E-16 — Ice Wall CSV comma breaks the import

| | |
|---|---|
| Severity | Bug |

**Where.** `Moves.csv` row 111, `ice_wall`. The description contains an unquoted comma: "Give an ally 25% mitigation for the rest of this turn. If you are faster, gain +1 AP when hit." `Docs/Combat/Audits/moves_import_validation.md` reports SelectionPolicy `'gain +1 AP when hit.'` and ActiveConditionType `'EffectOnly'`. Asset `Move_ice_wall.asset` description is cut at that comma, and the mitigation payload is magnitude 0, turnBudget 0, hitBudget 0.

**Observed.** This was the only unquoted comma found in `Moves.csv`. The wall does not mitigate, and the speed-based AP gain does not import. A magnitude of 0 also returns early in `ApplyMitigationWindow`.

**Expected.** 25% mitigation for the rest of this turn, and +1 AP when hit if the user is faster. The second opcode is `GainApOnDamageIfSpeedHigher`, which is a real opcode when the row parses.

**Suggested fix.** Quote the description field and reimport. Set a non-zero hit budget if the wall should survive `IsMitigationSourceActive` (E-17). A rest-of-turn wall wants `turnBudget` 1 so the granting end tick is the expiry.

### E-17 — Hit budget 0 turns mitigation off

| | |
|---|---|
| Severity | Bug |

**Where.** `ChainlingInstance.IsMitigationSourceActive`: `hitsRemaining == 0` is inactive. `ApplyMitigationWindow` passes `effect.hitBudget` through.

| Move | Asset | Result |
|---|---|---|
| `ash_guard` | magnitude 0.2, turnBudget 2, hitBudget 0 | 20% window never mitigates. Heal column is 5 and is a separate heal |
| `compound_guard` | same window, plus stage MeleeDef +1 | Def +1 works. The 20% window does not |
| `ice_wall` | imported zeros | See E-16 |

`blockade` is the same promise with an empty utility column, so it is in the dead-move list under E-19 rather than a zero hit budget.

**Expected.** "20% mitigation for 2 turns" is an active window. For a turn budget of 2, the clock above covers the rest of the granting turn and the next turn.

**Suggested fix.** Author a positive hit budget, or teach `MitigationWindow` the same 0-means-default rule `FullAbsorbBarrier` already uses (0 becomes 3 there). Say which hit count is intended.

### E-18 — Accuracy and Evasion moves cannot say what they do in the stage column

| | |
|---|---|
| Severity | Bug |

**Where.** `_LEGEND.md` CoreStat list. These rows name Acc or Eva and ship no utility opcode that could write those stats.

| Move | Description | What the row actually has |
|---|---|---|
| `accuracy_focus` | Acc +2 | empty |
| `evasion_maneuver` | Eva +2 | empty |
| `accuracy_disrupt` | Acc -1 | empty |
| `evasion_pin` | Eva -1 | empty |
| `gas_cloud` | all foes Acc -1 and Eva -1 | empty |
| `dust_fees` | Acc -1 and Eva -1 | empty |
| `optimize_aim` | Acc +2 | empty |
| `chain_seal` | Speed and Eva -1 | Speed -1 only |
| `key_rotation` | Def +1 and Eva +1 | Def +1 only |
| `zip_step` | Eva +2, Def -1 | Def -1 only |
| `pivot_line` | Acc +1, Eva +1, Speed +1 | Speed +1 only |
| `precision_strike` | +20% accuracy on a 64-power hit | power 64, accuracy 95, no bonus |
| `shard_scatter` | 2–3 AoE hits, each lowers Eva | power 18, hits 2–3, no Eva |

**Expected.** The named Acc/Eva change.

**Suggested fix.** Author `ChangeStage` or `FieldEntryStageChange` with Accuracy or Evasion. Those opcodes already accept `CombatStageStat`. `zip_step` currently applies only its downside.

### E-19 — Described effects with no payload and no move-id branch

| | |
|---|---|
| Severity | Bug |

**Where.** `CombatResolver` special-cases a fixed set of definition ids when `utilityEffects` is empty: `position_swap`, `ap_donation`, `ap_redistribution`, `ap_router`, `damage_intercept`, `signal_redirect`, `effect_relay`, `relay_packet`, `turn_start_ap_gain`, `shattered_armor`, `on_hit_momentum`, `on_crit_bonus`, `precision_focus`, `reactive_ping`, `conductive_counter`, `double_or_nothing` (the AP coin flip only), `meme_momentum`, `patience_passive`, `spite_counter`, `immutable_ledger`, `defensive_reflex`, `firewall_passive`. If a move has any utility opcode, that fallback id is blanked, so `turn_start_ap_gain` and `on_hit_momentum` do not double-apply. The moves below are not in that set, and their described clause has no column.

Entire description does not run:

| Move | Description |
|---|---|
| `ap_drain` | Cut 1 AP; 25% chance to steal it |
| `transaction_tax` | Target loses 1 AP; user gains it if the target had more |
| `extend_protocol` | Extend one ally buff by 1 turn. Recharge still applies (E-02) |
| `blockade` | 25% mitigation for 2 turns |
| `script_loop` | If the target has 2+ debuffs, extend them 1 turn |
| `network_sync` | Allies raise their lowest stage by +1 |
| `system_reset` | Strip buffs and debuffs; user skips the next action |
| `status_resistance` | General debuffs expire 1 turn faster |
| `isolation_tool` | Force the target's next move to be single-target |
| `viral_surge` | +2 AP or -1 AP at random, plus a random stat |
| `wildcard_swap` | Swap one stage with the target |
| `hype_burst` | Remove up to 2 buffs; Atk +1 per buff removed |
| `extraction_hit` | Hits can raise Atk; at Atk +3 also gain Speed |

Damage or one clause runs, and the named rider does not:

| Move | What runs | What the text adds |
|---|---|---|
| `all_in` | 98 power, 0 AP, accuracy 95 | Spend all AP, recoil on hit or miss, +1 AP on KO |
| `recoil_strike` | 76 power | 15% MaxHP recoil |
| `volatile_shot` | 72 power | Miss costs 15% MaxHP |
| `jackpot_shot` | 64 power | Double, normal, or half at random |
| `hype_train` | 52 power | +20% per consecutive hit, cap +100%, miss resets |
| `lucky_scatter` | 18 power, 1–4 hits | 25% chance each hit grants a random buff |
| `crystal_bash` | power 0, so the index is 0 | Damage from Def; doubles below 30% target HP |
| `glitch_barrage` | 16 power, 2–4 hits | Each hit randomly strikes the target or the user |
| `cipher_shot` | 64 power | 25% chance to block the target's next buff |
| `cipher_burst` | 22 power, 2–3 hits | 25% chance each hit erases a buff |
| `ap_siphon` | 56 power | 50% chance to drain 1 AP |
| `deep_dig` | 72 power | Ignore 50% of Def stages |
| `momentum_drill` | 64 power | +25% versus a debuffed target |
| `ore_strike` | 64 power | +10% per own buff, cap +30% |
| `stake_strike` | 64 power | +10% per own Def stage, cap +30% |
| `yield_spike` | 64 power | At 4+ AP, +25% damage then spend 1 AP |
| `interest_strike` | 18 power, 2–3 hits | Each hit can grant AP if Def stage exceeds Atk |
| `rallying_slam` | 64 power | +1 AP if Def stage exceeds Atk |
| `pickaxe_flurry` | 16 power, 2–4 hits | Each hit can raise Atk, cap +2 |
| `combustive_shift` | 22 power, 2 hits | Bonus versus a debuffed target who is missing AP |
| `crash_routine` | always Atk -1 | 15% MaxHP if the target is below 50% HP, otherwise the Atk drop |
| `glitch_feedback` | +1 AP surcharge on the target's next move | Remove 1 debuff |
| `bug_fix` | heal 10% | Cleanse 1 debuff; user's next move deals -20% |
| `dice_roll` | heal 20% | Random: heal, debuff a foe, or +2 AP |
| `recursive_backup` | immediate 20% heal while under 50% HP (`EffectOnly`) | Heal at end of turn, and reset durations |
| `data_scramble` | self Atk -1, self Speed -1, +2 AP | Both sides lose Atk and Speed |
| `risk_spike` | Atk +2, Def -1 | Next hit +25% damage and +10% recoil |
| `guard_stance` | Def +2 | Melee contact recoils +10% for 3 turns |
| `rally_cry` | allies Speed +1 | User gains +1 AP at end of turn |
| `refinery` | Def +1 | +1 AP at end of turn for 2 turns |
| `double_or_nothing` | end of turn, 60% +1 AP, 40% -1 AP | The 40% branch also grants AP-drain immunity |

These riders are authored and `ResolveConditionalDamageMultiplier` reads them, so they are not in the dead list: `coordinated_shot` and `hammer_sync` (partner-buff scaling), `chaotic_resonance` (per-status scaling, cap 2), `bug_payoff` (×1.5), `seam_cut` (×1.25), `finish_burn` (×1.5), `flame_charge` (×1.3 and +1 AP), `chaos_strike` (random ×1.5 or ×0.75), `bulwark_blow` (attacks with MeleeDef), `cryo_ray` (25% Speed -2), `deep_reap` (20% lifesteal), `automation_burst` (25% random stage drop after each hit).

**Expected.** Either the tooltip matches the columns, or the columns grow a payload the resolver already understands.

**Suggested fix.** For each row, add the opcode or conditional the sentence names, or rewrite the sentence to the clause that runs. `all_in`, `recoil_strike`, and `volatile_shot` are the urgent ones because the missing clause is a cost, so the move is strictly stronger than its text. `crystal_bash` is the urgent one in the other direction: Rare, 2 AP, 0 power.

### E-20 — Streamlined Flow drops Defense twice

| | |
|---|---|
| Severity | Bug |

**Where.** `streamlined_flow`. Stage change `MeleeDef:-1` targets self, and utility `ChangeStage:MeleeDef:-1`. Trigger `OnAction`. Description: "above 70% HP moves cost -1 AP but Def falls -1." `MoveAPDiscountWhileHPAbove` is consumed by `CombatActionEligibilityService`, not by the utility switch.

**Observed.** The AP discount is one path. The Def drop is two paths on the same action, so Def falls by 2 when both fire.

**Expected.** Def -1 per action while the passive is live.

**Suggested fix.** Keep one of the two Def writes.

### E-21 — Compression Burst lowers Defense on every hit

| | |
|---|---|
| Severity | Bug |

**Where.** `compression_burst`: power 22, hits 2–2, stage `MeleeDef:-1` at 100%, description "first hit lowers target Def." `ResolveDamagingMove` calls `ApplyMoveStageChanges` inside the per-hit loop.

**Observed.** Each confirmed hit applies the stage. Two hits drop Def by 2.

**Expected.** The first confirmed hit drops Def by 1.

**Suggested fix.** Apply that stage when `hitIndex == 0`, or set the later hits' stage chance to 0. The per-hit loop is also why a future "on first hit" stage will repeat until it is gated.

### E-22 — Group Hug does not reduce potency

| | |
|---|---|
| Severity | Bug |

**Where.** Modifier `4.5-a` Group Hug, Epic, description "Single-target ally support affects both allies at 20–30% reduced potency." `ResolveUtilityMove` adds every living ally and calls `ApplyUtilityEffects` at full strength. The popup text is `LINK`. Crowd Control `4.4-a` does multiply power by `1 - rolled percent` when it spreads a single-target attack.

**Observed.** Both allies receive the full support effect.

**Expected.** The second ally, or both allies, receive 20–30% less.

**Suggested fix.** Scale the copied support the way `4.4-a` scales damage, using the 20–30 roll already on the modifier.

### E-23 — Six stampable modifiers have no effect body

| | |
|---|---|
| Severity | Bug |

**Where.** `Docs/Specs/Combat/Data/Modifiers.csv`. `MoveModifierGenerator.LegacyModifierPool` includes `3.2-b`, `3.2-c`, and `7.2-c`.

| Section | Name | Text | What exists |
|---|---|---|---|
| 3.2-b | Side Quest | 15–20% chance to auto-use a random Common/Uncommon typeless move at end of turn | No branch. `3.2-a` is the economy proc that does exist |
| 3.2-c | After Hours | 12–15% chance to auto-use a random equipped move at end of turn | No branch |
| 7.2-c | Setup Feed | Next ally move +10–15% power | `7.2-a` and `7.2-b` have bodies. `7.2-c` does not |
| 10.1-d | All-Stat Burn | +3 to +4 stages on all 5 stats; 5% MaxHP self-damage per turn | No branch. `10.1-a/b/c` exist |
| 10.2-b | Anniversary Mark | 3–5% all stats | Popup label `STATS` only (`ResolveModifierDisplayText`) |
| 10.2-c | Tournament Badge | Badge display plus a ranked bonus | No branch |

**Expected.** A stamped modifier does the sentence on its card.

**Suggested fix.** Implement the six, or remove them from the catalog and the legacy pool so they cannot be offered.

### E-24 — Tripwire is a permanent Speed -1

| | |
|---|---|
| Severity | Bug |

**Where.** Modifier `6.6-a` Tripwire: "On Escape apply Slow for 1 turn." `ProcessOnEscapeModifiers` looks for the word "slow" in the description and applies `SetStage(Speed, old - 1)`. It does not apply `StatusId.Slowed`.

**Observed.** The fleeing unit loses 1 Speed stage permanently. Slowed's real effect is Speed -2 for 2 ticks, reverted on expiry (E-03).

**Expected.** Slowed for 1 turn, or a 1-turn Speed drop that reverts.

**Suggested fix.** Apply `StatusId.Slowed` for the duration the card names, or an owned Speed -1 that reverts after one tick. Stop matching the behavior by searching the description string.

### E-25 — Charged Relay checks the hit ledger at cast time

| | |
|---|---|
| Severity | Bug |

**Where.** `charged_relay`: heal 5%, utility `GainApIfDistinctTargetsHitAtLeast:2:2`. `UtilityEffectExecutor.ApplyLedgerTargetHitPayoff` reads `CountDistinctTargetsHitThisTurn` when the utility move executes.

**Observed.** The heal runs. The +2 AP requires two distinct targets already hit earlier this turn. A one-action turn that only casts Charged Relay has not recorded those hits yet, so the AP clause does not pay.

**Expected.** The sentence reads as a payoff for having hit 2+ targets this turn. On a one-action turn that is only true if another action already hit them.

**Suggested fix.** If the payoff is meant for the same turn's earlier hits, say "if you already hit 2+ targets this turn." If it is meant to include a later hit, move the check to end of turn.

### E-26 — FUD and Reinforced: duration versus a permanent stage

| | |
|---|---|
| Severity | Design question |

**Where.** FUD is live via `verification_lock` (E-06, E-15). Reinforced is catalog-only: `MeleeDef:+3` apply-once, duration 3, not in the v1 matrix and not on a move. The same apply-once rule would leave Def +3 in place after expiry.

**Observed.** The duration on the status is real as a token. The stages do not end with the token.

**Expected.** Open. Pokémon-style stages are often permanent. These tooltips and durations read as windows.

**Suggested fix.** Decide one rule for "status with a duration and a stage," then apply it to FUD, Reinforced, Gas Spike, and Shard Partition together.

### E-27 — Catalog statuses with no current producer

| | |
|---|---|
| Severity | Design question |

**Where.** Not in `Moves.csv` `StatusEffects`, and not in matrix v1: Rekt, Offline, GlitchedMind, HODLed, DustStorm, ForkedChain, CompressedData, MemeSurge, NodeRelay, Unsealed, Relay, BridgedOut. Reinforced is the same gap on the chemistry side. Type Coating and Hyped Type Coating are spec-retired for new authoring (`COMBAT_Statuses.txt` §7) and are absent from v1.

**Observed.** Several of these have resolver bodies (Rekt can skip an action, Offline wakes on damage, Glitched Mind mis-acts, Unsealed and Relay are consumed in the damage path). They cannot be applied by the current move list or the live matrix. HODLed is the one whose body also mis-ramps (E-11).

**Expected.** A player-facing catalog entry should have a way to appear, or it should be marked unused.

**Suggested fix.** Add a producer, or move the row to the deprecated list so tooltips and chemistry plans stop promising it. HODLed is called out in type profiles as the Block/Staked hold fantasy, and nothing applies it.

### E-28 — Passive status target override is ignored

| | |
|---|---|
| Severity | Design question |

**Where.** `TriggerPassiveMove`: `effect.targetOverride == Self ? chainling : chainling`. No current passive authors a non-self status, so nothing in the live set hits this.

**Observed.** A future passive that targets an ally or a foe with a status will buff or debuff the owner.

**Suggested fix.** Honor `targetOverride` when a passive status is authored, or reject non-self overrides at import.

### E-29 — Bench pause: spec line versus the clock the tests lock

| | |
|---|---|
| Severity | Design question |

**Where.** `COMBAT_Resolution.txt` §1.2 point 6: switching out does not pause recharge. `CHANGELOG.md` 2026-09-16: recharge ticks once per active end of turn, and benched durations stay paused. Test `BenchPausesStatusDurationUntilSwapIn`. `ProcessEndOfTurnForTeam` ticks actives.

**Observed.** A benched Recharging status does not tick. The code and the changelog and the test agree. The spec line does not.

**Suggested fix.** Rewrite §1.2 point 6 to the active-only tick. Leave the pause behavior, unless the designer wants bench time to count, which means changing the test.

## What matches

These were easy to "fix" into a bug. They already do what the duration clock says.

- Thread Lock and Frozen Assets wait until next turn, then a duration of 1 covers that turn and expires at its end.
- Bot Swarm and Majority Pressure revert at the granting end tick, which is "rest of this turn."
- Yield's three end-of-turn AP gains match duration 3.
- Exploit Mark multiplies damage by 1.15 in `DamageCalculator`.
- Firewall's first projectile hit each turn is a mitigation source armed by chemistry.
- Slowed's Speed -2 reverts. The bad part is the label (E-03).
- Shattered chips 8% of current HP and stops at 1 HP left, then Def -1. It is not an execute. That matches §7.6.
- Drip heals 6% MaxHP every end tick. The spec's "once" is E-04.
- Next-action statuses survive the granting tick (`NextActionStatusSurvivesTheGrantingTurnTick`).
- Bean Sip's heal, Speed +1, and priority are real. Only the recharge sentence is wrong (E-01).
- Siphoned heals 25% of damage dealt.
- Anchored, Broadcast, and Splinter are consumed by the spread path.
- Isolated blocks ally support, healing, AP transfer, and ally stage buffs.
- Defueled cleanses one harmful chemistry status.
- Cascade Failure's 25% MaxHP and swap reset match the spec. The comment is E-05.
- Corroded's Def -1 is `applyOnce` false, so it ticks at end of turn for the duration.
- Rugged's escalating sixteenths and FOMO's Atk +1 plus self damage-over-time have dedicated branches and tests.
- Group-adjacent `4.4-a` does reduce spread damage. Group Hug (`4.5-a`) does not reduce support (E-22).
- Hardcoded ids listed under E-19 do run. `fee_collector` drains foes who have more AP than the user. Fox Feint's dodge-and-counter window runs; the 999 log line is E-02.
- `6.4-*` swap modifiers resolve in `SwapModifierResolver`, including a timed accuracy source for `6.4-h`. `8.1-*` MaxHP bonuses resolve in `PassiveMaxHpUtility`.

## Move rows called out

75 rows. "Full" means the described effect does not run. "Partial" means at least one clause runs.

| Move | Class | Finding |
|---|---|---|
| `accuracy_focus` | Full | E-18 |
| `evasion_maneuver` | Full | E-18 |
| `accuracy_disrupt` | Full | E-18 |
| `evasion_pin` | Full | E-18 |
| `gas_cloud` | Full | E-18 |
| `dust_fees` | Full | E-18 |
| `optimize_aim` | Full | E-18 |
| `chain_seal` | Partial | E-18 |
| `key_rotation` | Partial | E-18 |
| `zip_step` | Partial | E-18 |
| `pivot_line` | Partial | E-18 |
| `precision_strike` | Partial | E-18 |
| `shard_scatter` | Partial | E-18 |
| `ap_drain` | Full | E-19 |
| `transaction_tax` | Full | E-19 |
| `extend_protocol` | Full effect, recharge still logs | E-19, E-02 |
| `blockade` | Full | E-19 |
| `script_loop` | Full | E-19 |
| `network_sync` | Full | E-19 |
| `system_reset` | Full | E-19 |
| `status_resistance` | Full | E-19 |
| `isolation_tool` | Full | E-19 |
| `viral_surge` | Full | E-19 |
| `wildcard_swap` | Full | E-19 |
| `hype_burst` | Full | E-19 |
| `extraction_hit` | Full | E-19 |
| `all_in` | Partial | E-19 |
| `recoil_strike` | Partial | E-19 |
| `volatile_shot` | Partial | E-19 |
| `jackpot_shot` | Partial | E-19 |
| `hype_train` | Partial | E-19 |
| `lucky_scatter` | Partial | E-19 |
| `crystal_bash` | Partial | E-19 |
| `glitch_barrage` | Partial | E-19 |
| `cipher_shot` | Partial | E-19 |
| `cipher_burst` | Partial | E-19 |
| `ap_siphon` | Partial | E-19 |
| `deep_dig` | Partial | E-19 |
| `momentum_drill` | Partial | E-19 |
| `ore_strike` | Partial | E-19 |
| `stake_strike` | Partial | E-19 |
| `yield_spike` | Partial | E-19 |
| `interest_strike` | Partial | E-19 |
| `rallying_slam` | Partial | E-19 |
| `pickaxe_flurry` | Partial | E-19 |
| `combustive_shift` | Partial | E-19 |
| `crash_routine` | Partial | E-19 |
| `glitch_feedback` | Partial | E-19 |
| `bug_fix` | Partial | E-19 |
| `dice_roll` | Partial | E-19 |
| `recursive_backup` | Partial | E-19 |
| `data_scramble` | Partial | E-19 |
| `risk_spike` | Partial | E-19 |
| `guard_stance` | Partial | E-19 |
| `rally_cry` | Partial | E-19 |
| `refinery` | Partial | E-19 |
| `double_or_nothing` | Partial | E-19 |
| `fuel_surge` | Fixed (stages revert) | E-15 |
| `sear_field` | Fixed (status window) | E-15 |
| `shard_partition` | Fixed (stages revert) | E-15 |
| `verification_lock` | Fixed for stages; Acc comment deferred | E-15, E-06, E-26 |
| `ash_guard` | Partial | E-17 |
| `compound_guard` | Partial | E-17 |
| `ice_wall` | Comma fixed; hit budget 0 deferred | E-16, E-17 |
| `mev_extract` | Partial | E-12 |
| `deflation_mark` | Partial | E-13 |
| `voltage_rush` | Partial | E-14 |
| `risk_marker` | Partial | E-14 |
| `streamlined_flow` | Partial | E-20 |
| `compression_burst` | Partial | E-21 |
| `charged_relay` | Partial | E-25 |
| `bean_sip` | Fixed (wording + 1 AP) | E-01 |
| `ap_boost` | Fixed (wording) | E-02 |
| `fox_feint` | Fixed (wording) | E-02 |
| `gecko_guard` | Unused recharge column | E-02 |

## Decisions for the developer

Answered in the decision round at the top of this file:

1. Recharge copy uses upcoming skipped turns. `rechargeTurns` was left as authored. Bean Sip also grants +1 AP. A stronger Bean Beam at 6 AP was not in the kit and was not added.
2. Timed-status stages revert with the token. Refresh does not stack apply-once stages.
3. Group Hug no longer copies a support move. Each active ally gains +1 AP.
4. Bench recharge keeps ticking. Other statuses still pause. §1.2 matches.
5. The balance screen now flags a typeless Rare that sits under a typed Common. §6.1 was not rewritten, and no power number was changed.

Still open:

1. Empty tooltips (E-18, E-19) and the chemistry clauses in E-07 through E-14. Each row is in [UNIMPLEMENTED_MOVE_EFFECTS.md](UNIMPLEMENTED_MOVE_EFFECTS.md), with change / remove / implement. Missing costs (`all_in`, `recoil_strike`, `volatile_shot`) and `crystal_bash` at power 0 still dominate any later number pass.
2. Hit budget 0 on Ash Guard, Compound Guard, and the restored Ice Wall. Pick a hit count before those walls mitigate.
3. The six stamp modifiers in E-23. Left for the other agent.
4. Producer-less statuses. Research is in [CATALOG_STATUSES_WITHOUT_PRODUCERS.md](CATALOG_STATUSES_WITHOUT_PRODUCERS.md). No row was deprecated and no producer was added.
5. HODLed's double ramp and missing immunity. The shared revert covers the stages if it is applied. The formula was not changed.
