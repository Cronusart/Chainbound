# Passives and the status ecosystem

**Proposals only.** Nothing in this note is a decision. No move, status, pool, or engine row was edited.

Source of truth is the local snapshot `chainbound_combat_snapshot_2026-10-17` (zip dated 2026-10-17). Where an older audit disagrees with that snapshot, the snapshot is the finding and the audit is called stale. The repo checkout was not used for live numbers.

This round does not re-open recorded decisions:

- Node passive stays Uplink (`uplink`, APR-RELAY). Partner Beacon, Relay Dividend, Node Beacon, Link Pulse, and Open Line stay withdrawn.
- Burned passive stays Kindling at 6% (`kindling`, APR-KIND). Cinder Idle and Scorch Echo stay withdrawn. Sear Echo stays the unchosen alternative.
- Compressed C1–C7 and Bot D1–D7 stay unapproved (DEC-CD).
- Design-idea moves and mechanics M1–M6 stay unapproved (DEC-PACK).
- Meme stays out of chemistry (DEC-CHEM and `COMBAT_TypeProfiles.md`).
- A learnable pool keeps at least one Common damaging move. Proposals below do not remove one.

HP examples use `baseMaxHP` from the species assets, before IVs and rebirth. Runtime ticks use `EffectiveMaxHP`, so a real fighter’s integer will differ once IVs are applied. That difference was not recomputed. Stage math is `ChainlingInstance.StageMultiplier`: positive `(2 + stage) / 2`, negative `2 / (2 + abs(stage))`. So +1 is 1.5×, +2 is 2.0×, +3 is 2.5×, +6 is 4.0×, −1 is 2/3, −2 is 0.5×, −6 is 0.25×. Stages clamp at ±6 (`CombatConstants.STAGE_MIN` / `STAGE_MAX`).

`CombatConstants.STAGE_PASSIVE_CHANGE_CAP` is 1. A search of `Assets/Scripts` finds that constant only at its declaration. The resolver does not read it. Passive stage writes are limited by the opcode’s own cap, and negative writes that go through `move.stageChanges` are also clamped to `stageActiveChangeCap` (2 in `CombatResolverConfig_Default.asset`). Positive passive writes are not clamped to +1.

---

## 1. How a passive actually runs

A passive is a `Moves.csv` row with category `Passive`. It costs 0 AP (`COMBAT_Resolution.txt` §1.3). `ResolveAction` rejects it as an action (`CombatResolver` around the `MoveCategory.Passive` branch) and still calls `SettleUplink` and `CheckPassiveTriggers(..., OnAction)` afterward.

`CheckPassiveTriggers` walks equipped passives and matches `TriggerCondition` to a trigger point. `TriggerCondition` `None` only fires for four hardcoded ids, in `ShouldTriggerImplicitPassive`: `turn_start_ap_gain`, `on_hit_momentum`, `on_crit_bonus`, `conductive_counter`. Any other passive with trigger `None` never enters `TriggerPassiveMove`.

When it does fire, `TriggerPassiveMove`:

1. Logs `PassiveTriggered` (`"{name}'s {passive} triggered!"`).
2. Rolls `stageChanges` and `statusEffects` onto the owner. A non-self `targetOverride` is ignored: both branches assign the owner (`E-28`, still true; no current passive puts a status in that column).
3. Heals `HealPercentMaxHP` of `EffectiveMaxHP` if that cell is above 0.
4. Runs `UtilityEffects` through `UtilityEffectExecutor`.
5. Runs a hardcoded body only when `utilityEffects` is empty. If the utility list is non-empty, the hardcoded id is blanked, so the opcode is the body.

Two utilities are metadata, not a trigger effect. `IncomingDamageMitigation` and `MoveAPDiscountWhileHPAbove` hit empty cases in the executor. Mitigation is read later in `ApplyDefensivePassiveMitigation`. The discount is read in `CombatActionEligibilityService.GetEffectiveApCost`.

Turn start, for each living active: `StatusProcessor.ProcessStartOfTurn` grants `apPerTurn` (2) first, then `ProcessStartOfTurnTriggersForTeam` runs turn-start passives. The code comment on the regen says Low AP Surge is meant to see the AP after that grant. End of turn, for each living active: status ticks, then `OnBuffExpired`, then `EndOfTurn` passives. Benched fighters are not in `activeIndices`, so their passives do not take those passes. Starting AP is 0 (`ChainlingInstance.currentAP` default; `CombatFactory` does not write one). Turn 1’s action window is 2 unless something adds AP before the action.

There are 31 passive rows in `Moves.csv` (208 move rows). Species pools below are `SpeciesMovePools.csv`. Equip is wider than the pool: a typeless move equips on anyone, and a typed move equips on a matching primary type (`SpeciesMoveEquipRules`). A pool is what that species is granted. A same-type move that is not in the pool can still be equipped if it is known, unless another species owns it as an exclusive.

---

## 2. Passive inventory

“Runs” means the snapshot engine has a body for the row. “Turn 1” means a symmetric fight that opens at 0 AP, gains +2, and has not banked.

| Id | Type / rarity | What the row says | What runs | Practical worth | Turn 1 |
|---|---|---|---|---|---|
| `turn_start_ap_gain` | None / Common | +1 AP at turn start | `GainAP:1` after regen | +1 every turn on top of +2. Opens the action at 3. | Pays |
| `on_hit_momentum` | None / Common | 25% Atk +1, cap +3 | Utility `ChangeStage` 25%, cap 3. Hardcoded twin is blanked because the utility list is non-empty. | Each success is 1.5×, then 2.0×, then 2.5× Atk, and it stops at +3. | Pays on a hit |
| `on_crit_bonus` | None / Common | Crits grant +1 AP | Hardcoded `Gain` 1. No utility. Trigger `None` is allowed for this id. | +1 when a crit lands. Crit rate was not recomputed. | Pays only on a crit |
| `defensive_reflex` | None / Epic | 20% less damage, lose 1 AP at end of turn | Mitigation is read from the utility on every incoming hit. `DrainAP:1` runs at end of turn. | 20% on every hit. The AP tax only removes a point when AP is still above 0 at end of turn. A fighter who spends the window to 0 is not taxed. | Mitigation pays if hit. Tax often misses. |
| `sustain_regen` | None / Common | Heal 3% at end of turn | `HealPercentMaxHP` 3 | `floor(MaxHP × 0.03)`, minimum 1. On base 124 HP that is 3. | Pays at end of turn 1 |
| `deep_sustain` | None / Rare | Heal 6% | Same path, 6% | On base 124 HP that is 7. Strictly double the Common, which is the rarity step. | Pays at end of turn 1 |
| `status_resistance` | None / Common | General debuffs expire 1 turn faster | Nothing. Trigger `None`, empty utility, not in `ShouldTriggerImplicitPassive`. | 0 | Never |
| `emergency_recovery` | None / Common | Below 25% HP, heal 15% once | `HPBelowThreshold` on `AfterDamage`, `TriggerOnce`, heal 15% | On base 124 HP that is 18, once. | Only if turn 1 damage crosses 25% |
| `precision_focus` | None / Common | A miss refunds 1 AP, 75% | Hardcoded `Refund` 1 at 75% | About three refunds per four misses. | Pays when you miss |
| `reactive_ping` | None / Common | A miss deals 5% MaxHP to the attacker | Hardcoded `floor(attacker EffectiveMaxHP × 0.05)`, minimum 1. No chemistry. | On a 124 HP attacker that is 6. | Pays when they miss you |
| `low_ap_surge` | None / Common | Below 2 AP after turn AP gain, Speed +1 | `ChangeStage` Speed +1, cap +6, condition `APAtMost` 1, after regen | See §3. With regen +2 from 0, AP is 2, and 2 is not ≤ 1. | Does not pay |
| `low_ap_burst` | None / Rare | Same, Speed +3 | Same gate, amount +3, cap +6 | Same dead gate. If the gate ever passes, +3 is 2.5× Speed in one tick and can later walk to +6 (4.0×). Strictly the Common plus a much larger write. The unread passive cap of +1 does not stop it. | Does not pay |
| `spite_counter` | None / Rare | 33% to mirror a received debuff | `TrySpiteCounter` on a General debuff apply, 33%, mirrors that status. Not the trigger loop. | A copied debuff. No duration change. | Pays when a debuff lands |
| `desperation_surge` | None / Rare | Below 33% HP, Atk +2 and +1 AP once | Stage column Atk +2 and utility `GainAP:1`, `TriggerOnce` | Atk 2.0× and +1 AP, once. The +2 exceeds the unread passive cap. | Only under 33% HP |
| `fee_collector` | Gas / Rare | Foes with more AP lose 1 at your turn start | `DrainAP:1` on all foes, and the executor skips a foe who does not have more AP than you. Condition `AnyFoeAPGreaterThanSelf` also has to pass. | 0 while banks are equal. 1 AP per ahead foe on your turn start after that. Spenders who stay at 0 are not taxed. | No, if both sides open at 2 |
| `immutable_ledger` | Block / Uncommon | First hit each turn is 20% lighter and grants +1 AP | Hardcoded mitigation on `hitsTakenThisTurn == 0`, then `TryImmutableLedgerAp` +1. | 20% on one hit, plus +1 AP. Later hits that turn are full. | Pays if hit |
| `firewall_passive` | Encrypted / Uncommon | Projectile hits, including spread, deal 25% less | Utility mitigation, contact filter `Projectile`, read at hit time | 25% on projectile hits only. Contact is unchanged. | Pays if a projectile hits |
| `conductive_counter` | Forked / Rare | Once a turn, return 15% of contact damage and gain +1 AP | Hardcoded. Contact only, `MarkOncePerTurn`, `floor(damage × 0.15)` minimum 1, then +1 AP. No chemistry. | 15% of that one contact hit, plus +1 AP. A ranged foe never triggers it. | Pays if a contact hit lands |
| `extraction_hit` | Mined / Common | Hits can raise Atk; at Atk +3 also gain Speed | Nothing. Same empty shape as `status_resistance`. | 0. It is in Kodiax’s pool. | Never |
| `patience_passive` | Staked / Uncommon | End of turn Def +1. No attack heals 5% | Utility Def +1, cap +6, every end step. `ApplyPatienceHeal` adds 5% if the ledger says this fighter did not use a damaging move. | Def walks +1 per end step to +6 (4.0×). The heal is on top when they do not attack. On base 124 HP the heal is 6. | Def +1 at end of turn 1. Heal if they did not attack. |
| `stake_refund` | Staked / Rare | When your buff expires, regain 1 AP | `OnBuffExpired` runs once per end step if the buff count fell, then `GainAP:1`. The count is not the amount gained. | +1 AP on an end step where at least one buff expired, not +1 per buff. | No, unless a buff is already up and expires |
| `streamlined_flow` | Compressed / Rare | Above 70% HP, moves cost −1 AP and Def falls −1 | Discount of 1 while HP% > 70, read at cost time. On each action that passes `HPAbovePercent` 70, Def −1 from the stage column and Def −1 again from `ChangeStage` (cap −6). | A 3 AP move costs 2 while healthy, so a 3 is legal on turn 1. Def falls by 2 per such action, down to −6 (0.25×). | Discount pays on turn 1 while healthy. The Def tax starts on the first such action. |
| `automated_pressure` | Bot / Common | End of turn, one random foe stat −1 | `RandomStageChange` −1, cap −6, random foe, stat index 0–4 inclusive (`MeleeAtk` through `Speed`) | One permanent −1 per end step, spread across five stats, each able to reach −6 (0.25×). | Pays at end of turn 1 |
| `unstable_code` | Glitched / Common | Turn start, a random stat +1 or +2 | `RandomStageChange` amount 1 through 2 inclusive, cap +6, self, same five stats | Every turn start, including turn 1, a random stat becomes 1.5× or 2.0× for that step of the climb, and it can reach +6 (4.0×). | Pays before the action |
| `parallel_passive` | Shard / Common | Ally attacks can raise your Speed, cap +2, 25% | Utility `ChangeStage` Speed +1 at 25%, cap +2, trigger `OnAllyHit` | Up to 2.0× Speed, only when an ally’s hit connects. | Pays if an ally hits on turn 1 |
| `shattered_armor` | Shard / Common | A super-effective hit grants 5% mitigation this turn and +1 AP | Hardcoded, only when `chemAdvCount > 0`, once per turn: mitigation 0.05 for the rest of the turn, +1 AP | The AP is the payload. 5% is much smaller than Firewall’s 25% or Ledger’s 20%. | Pays on a ChemAdv hit |
| `double_or_nothing` | Meme / Common | End of turn 60% +1 AP, 40% −1 AP and AP-drain immunity | Hardcoded 60% `Gain` 1, else `Drain` 1. No immunity flag and no status. | Expected +0.2 AP per end step if both branches can change AP. The immunity sentence does not run. | The roll pays at end of turn 1. The immunity does not. |
| `meme_momentum` | Meme / Rare | A landed hit raises both Atk by 2 and grants +2 AP. A miss drops them by 1. | If the ledger has a hit: MeleeAtk +2, RangedAtk +2, `Gain` 2. If a damaging move was used and no hit landed: both Atk −1 and `Drain` 1. If they did not attack, nothing. | A hit is 2.0× on both attacks and +2 AP. The miss path also drains 1 AP, which the description does not say. | Pays at end of turn 1 if they attacked |
| `gecko_guard` | Frozen / Rare | Below 50% HP, Def +2 and Speed +1 once | Stage column, `HPBelowThreshold` 50, `TriggerOnce`. `rechargeTurns` 999 is not applied, because a passive is not an action (`E-02`). | Def 2.0× and Speed 1.5×, once, and the stages are not a timed status so they stay. | Only after damage crosses 50% |
| `uplink` | Node / Uncommon | An ally you buff gains 1 AP, once per ally per turn | `SettleUplink` after the action. A successful buff status or a positive stage on an ally calls `NoteUplinkAlly`. `TryClaimUplinkPay` allows one pay per ally per turn. Cap 10. A lost pay (cap, or Thread Lock) still spends the claim. | +1 AP on that ally, not on the Node. | See §3 |
| `kindling` | Burned / Uncommon | A damaging hit that leaves a foe alive burns 6% at end of turn | `TryApplyKindling` applies `Kindled` duration 1 if they do not already have it. The end step deals `floor(EffectiveMaxHP × 0.06)` from a literal `0.06f`, then duration 1 expires. A second hit in the same turn does not refresh. | One burn per end step in which at least one non-KO hit landed. On Cinislug’s 134 HP that is 8. On base 124 HP that is 7. It is a separate status from Rugged, so both can tick. | Pays if a non-KO hit lands on turn 1 |

Median `baseMaxHP` of the 18 species assets other than Ghost is 124 (the two middle values are both 124). Ghost is 0.

### Species pools versus the passive they can be granted

| Species | Type | Passive in `LearnableMoves` or `ExclusiveMoves` | Gap |
|---|---|---|---|
| Validape | Node | `uplink` | Pool has no 2 AP ally buff. `pack_rally` is 3 AP. `network_sync` is 2 AP and its payload now runs, but it is not in this pool. |
| Deghoul | Gas | `fee_collector` | None for the passive. |
| Blockwyrm | Block | `immutable_ledger` | None. |
| Keyper | Encrypted | `firewall_passive` | None. |
| Cinislug | Burned | `kindling` | None. |
| Gilarime | Frozen | `gecko_guard` (exclusive) | None. |
| Forkrook | Forked | none | `conductive_counter` exists and is Forked, so it can be equipped if known. The five-move pool does not grant it. |
| Kodiax | Mined | `extraction_hit` | The granted passive does not run. |
| Minefolk | Mined | none | Same empty passive is equippable, not granted. |
| Hopoch | Staked | `patience_passive` | `stake_refund` is not in the pool. |
| Rattlestake | Staked | none | Can equip Staked passives if known. |
| Cofi Lootwak | Staked | none | Boss pool is `civet_beam`, `compound_guard`, `stake_heal`, exclusive `bean_sip`. |
| Humpacked | Compressed | `streamlined_flow` | The double Def drop is the problem, not the pool. |
| Kitsauto | Bot | `automated_pressure` | The passive is stat pressure. Bot’s profile is AP denial. |
| Kreechank | Glitched | `unstable_code` | Runs, and it is the largest turn-1 stage engine in the passive list. |
| Crackett | Shard | `parallel_passive` | None. |
| Marrowhead | Shard | `shattered_armor` | None. |
| Balloom | Gas (`primaryType` 2) | none of the Meme passives | Off-type exceptions are `all_in` and `recoil_strike` only. |
| Ghost | Meme | none | `baseMaxHP` 0. A Meme primary can equip any move (`SpeciesMoveEquipRules`), including `double_or_nothing` and `meme_momentum`. There is no HP to value them on. |

---

## 3. Balance across type profiles

Typed passives have a smaller equip pool than a typeless card of the same job, so they are supposed to be better than that typeless card, to work on turn 1 or to pay back a slow open, and to do something typeless cannot do.

### What is fine to leave

- **Uplink (APR-RELAY)** pays the ally, once per ally per turn, after a buff write that actually changes a stage or newly applies a buff status. Heals, cleanses, and AP transfers do not pay. That is something `turn_start_ap_gain` cannot do. With Validape’s granted cards, turn 1 does not pay: the stage buff in the pool is `pack_rally` at 3 AP, and turn 1 is 2 AP. The payback is the first rally. `network_sync` is now a real 2 AP buff (`RaiseLowestStage` in `Moves.csv`; the empty-payload investigation is stale for this row). It would pay Uplink on turn 1, and it is not in Validape’s pool. This note does not change the +1.
- **Kindling (APR-KIND)** is 6% of MaxHP once per connecting end step, including into neutral matchups, and it does not refresh mid-turn. Rugged’s first tick is `counter / 16` with `counterValue` starting at 1, so 6.25%, then 12.5%, then 18.75% across a duration of 3 if it is not refreshed. Kindling does not escalate and is slightly under that first tick. Refreshing Rugged does not raise the counter. The two burns are different statuses, so a Burned advantage lane can tick both. This note does not change the 6%. The processor uses a literal `0.06f` and does not read the catalog percent. The catalog row already says 0.06.
- **Fee Collector** is denial, not a copy of +1 AP. It is 0 on a symmetric turn 1 and 0 against a foe who spends to empty. It removes 1 from each foe who is ahead. That is the Gas profile, and it is the tool that answers a bank.
- **Immutable Ledger** mitigates less often than Epic `defensive_reflex` (first hit versus every hit) and pays +1 AP instead of taxing. The AP on the wall is the part typeless mitigation does not do.
- **Gecko Guard** is a one-time Def +2 and Speed +1 after the 50% line. It does not pay on turn 1 unless damage arrives that early. The stages stay. That is a slow-open payback, and it is Frozen’s exclusive rather than a second copy of regen.
- **Parallel Passive** and **Shattered Armor** are different Shard jobs: ally-hit Speed, and a ChemAdv hit’s +1 AP. They are not copies of each other.
- **Spite Counter**, **Emergency Recovery**, **Precision Focus**, **Reactive Ping**, **On Crit Bonus**, and the two regen passives do what their rows say. `deep_sustain` being strictly double `sustain_regen` is the Rare step, not a duplicate bug.

### What is empty, dead, or strictly out of line

- **`status_resistance` and `extraction_hit` do not run.** Extraction Hit is the Mined passive Kodiax is granted. On Hit Momentum, the typeless peer, does run. A Mined fighter with Extraction Hit equipped has no passive.
- **Low AP Surge and Low AP Burst do not fire in a normal open.** Regen is +2 and happens first. The condition is `currentAP <= 1`. After regen from 0, AP is 2. The gate passes if regen was blocked (Thread Lock) or AP was already under 2 and regen added nothing. It does not pass for a fighter who is merely out of banked AP. Burst is the same gate with Speed +3, so the Rare is a dead card that would be far above the unread +1 passive cap if the gate were loosened without a new cap.
- **Patience walks to Def +6.** That is 4.0× defense for standing on the field, plus 5% when not attacking. No other passive grants a permanent stage ladder that tall. Unstable Code is the other ladder, and it is random and offensive. Patience is deterministic and is the Staked wall. It is strictly the largest defensive passive. Firewall is 25% of projectile hits. Ledger is 20% of one hit. Patience at +6 quarters incoming damage from the Def stat itself, on top of those if they were stacked, and it does not end.
- **Unstable Code is the largest turn-1 passive.** Before the action on turn 1 it adds +1 or +2 to a random core stat, and it repeats every turn until that stat is +6. Expected step is +1.5 if the inclusive roll is even. A typeless Speed +1 move costs an action. This passive does not.
- **Automated Pressure walks foe stats to −6.** It is Bot’s granted passive, it pays at end of turn 1, and it is not AP denial. Fee Collector is the AP tax, and it is Gas and conditional. In a long fight the Bot Common puts a permanent 0.25× on five stats. That is strictly more stat pressure than any single debuff move in the kits, and it does not match the profile line that Bot is the AP-denial type.
- **Streamlined Flow drops Def twice per action.** The description says Def falls −1. The stage column and the `ChangeStage` opcode each apply −1 while HP is above 70% (`UNIMPLEMENTED_MOVE_EFFECTS.md` already records the double write; the resolver path is the stage loop plus the utility executor). The discount is the typed payoff: a healthy Compressed fighter fires a 3 AP move on turn 1. The tax, as it runs, is −2 Def per action down to −6.
- **Double-or-Nothing’s live roll is a worse `turn_start_ap_gain`.** Expected +0.2 AP, and only at end of turn, and the immunity that would make the 40% branch a Meme effect does not exist in the resolver. Meme Momentum, on a hit, is strictly stronger (+2 to both attacks and +2 AP) and is the Rare. Neither is in a pool that a species with HP is granted.
- **Firewall versus Defensive Reflex.** The typeless Epic is 20% on every hit and taxes 1 AP when a point is left. The typed Uncommon is 25% on projectiles only. On contact, the Epic is strictly better. On projectiles, 25% is a small step over 20%, and the Epic still taxes itself. Encrypted’s profile is AP drain and debuff immunity. The projectile wall is real, and it is not clearly ahead of the typeless Epic.
- **Conductive Counter** runs, and Forkrook’s pool does not grant it. Forked’s profile is spread. The passive is a contact counter. It is the only Forked passive row.
- **Stake Refund** runs once per expiring end step, not once per buff. It is not in Hopoch’s, Rattlestake’s, or Cofi’s pool.

---

## 4. Status ecosystem

`Statuses.csv` has 56 rows. `StatusId.cs` still names seven ids that are not in the CSV and have no `StatusDef_*.asset`: `Reinforced`, `DustStorm`, `BridgedOut`, `ForkedChain`, `CompressedData`, `MemeSurge`, `NodeRelay`. `CombatIssuesRemediationV2Tests.AuthoredRows_MatchCsvAndLeaveBlockedCardsEmpty` asserts that `DustStorm`, `NodeRelay`, and `Reinforced` are absent. `CATALOG_STATUSES_WITHOUT_PRODUCERS.md` (2026-10-16) still treats those seven as live catalog rows. That audit is stale for the CSV. The enum comments and `COMBAT_TypeProfiles.md` are not fully aligned with the cut: the profile summary table already does not list ForkedChain, CompressedData, MemeSurge, or NodeRelay (Forked’s key is Splinter, Compressed’s key cell is “—”, Meme’s keys are Staggered and Surging, Node’s keys are HODLed and Immovable).

Chemistry producers are the 57 filled cells of `COMBAT_Interaction_Matrix_v1.csv`. Meme’s row and column are empty. Counts:

| Status | Cells | What the body does in this snapshot |
|---|---|---|
| Jammed | 8 | Nothing to Accuracy. `StatusDef_Jammed.asset` has `stageEffects: []`. `E-07` is still open. The most common chemistry output does not change a stat. |
| Anchored | 6 | Next damaging move is forced to one target, then consumed. Runs. |
| Drained | 5 | −1 AP at end of turn for 3 turns. Runs through `endOfTurnAPChange`. |
| Rugged | 5 | Escalating MaxHP damage, first tick 1/16, cap 15. Runs. Five of Burned’s seven wins. |
| Pumped | 4 | +1 AP at end of turn for 3 turns. Runs. |
| Dusted | 4 | Speed −3 and Evasion −3, both apply-once. The asset owns both (Speed via the Dusted branch, Evasion via `usesCombatStage` / `combatStage` 6). Both revert on expire. The unimplemented note that Evasion is not applied is stale for this asset. |
| Shattered | 4 | Instant 8% of current HP, and Def −1 (`shatteredCurrentHpDamage` 0.08, `shatteredDefenseStages` −1). Runs. |
| Defueled | 3 | Instant: remove one harmful chemistry status. Runs. |
| Corroded | 3 | Def −1 at each end step, `applyOnce` false, duration 2. The drops are not owned, so they stay after the status ends. Two end steps leave Def −2. |
| Isolated | 2 | Blocks ally support, healing, AP transfer, and ally stage buffs. Runs. |
| Backlash | 2 | No contact damage-over-time body. The cells apply a token. |
| Splinter | 2 | Next damaging move spreads at 65% (`splinterDamageMultiplier` 0.65) or the single target takes ×1.15. Runs. |
| Leaked | 2 | `ApplyLeakedActionTax`: if any active ally is Leaked, the actor loses 10% MaxHP on action. The end-step damage loop skips Leaked. The CSV percent is 0. The “end of turn DoT” reading in the old unimplemented note does not match this CSV. |
| FOMO | 2 | Atk +1 is stored as a temporary delta and reverts. Self damage is `fomoSelfDotPercent` 0.0625. Runs. |
| Firewall | 1 | First projectile hit each turn, mitigation 0.30. Runs. |
| Slowed | 1 | Speed −2, reverted on expire. The 25% action-loss constant is unused (`E-03`). |
| Drip | 1 | Heal 6% MaxHP each end step for the duration, from `dripHealPercentMaxHp`. Runs. Spec “once” remains `E-04`. |
| Siphoned | 1 | Heal 25% of damage dealt. Runs. |
| Cascade Failure | 1 | At 3 counters, 25% MaxHP (`cascadeFailureMaxHpDamage`). Swap clears it. Runs. |

`COMBAT_Chemistry.txt` §7 says these stay readable for migration and cannot be newly authored into the v1 matrix: Firewalled, Halted, Crashed, LiquidityPump, Evasive, Bridged, Narrowcast, Overclocked, TypeCoating, HypedTypeCoating, and Amplified. They are still CSV rows. This note does not give them producers.

### Move, passive, and modifier producers

From the `StatusEffects` column, plus the resolver branches that apply a status without that column:

| Status | Producer | Duration in the row | Body |
|---|---|---|---|
| Kindled | Passive `kindling` | 1 | 6% MaxHP, then expires. Does not refresh if reapplied while up. |
| GasSpike | `fuel_surge` | 2 | Atk +2 and Def −2, apply-once. Not in the owned-stage set, so the stages stay after expire. The move also grants +3 AP and costs 0. |
| FUD | `verification_lock` | 2 | Atk −1, Def −1, Speed −1, apply-once, not owned, so they stay. The move also puts +1 AP on the next move. Accuracy is not in the asset (the enum comment still mentions it). `E-06`. |
| ShardPartition | `shard_partition` (all allies) | 2 | Speed +1 on each recipient, apply-once, not owned, so it stays. “Team” is the move’s targeting, not an aura. |
| BotSwarm | `bot_swarm` | 1 | Atk −1 and Def −1, owned, revert at the granting end step. |
| FiftyOnePercentAttacked | `majority_pressure` | 1 | Speed −1, owned, reverts at the granting end step. |
| ExploitMark | `exploit_window` | 2 | Allies’ hits ×1.15 (`exploitMarkDamageMultiplier` default 1.15). Runs. |
| EncryptedSeal | `proof_shield` | 1 | Blocks new debuff applies. Runs. |
| FrozenAssets | `lock_assets`, and `partition_shot` | 1, queued to next turn start | Voluntary swap is rejected. Runs for that next turn. |
| ThreadLock | `thread_lock` | 1, queued to next turn start | `CombatApService.Gain` blocks gains, including regen, while it is up. |
| Yield | `yield_cycle` | 3 | +1 AP at each end step. Runs. |
| MEVExtracted | `mev_extract` | 2 | Catalog AP change −1 is a Drain, not a steal. The foe loses 1. The user does not gain it. |
| BurnedTokens | `deflation_mark` | 2 | No heal body. The move’s Atk −1 stage does run and stays. |
| Overleveraged | `voltage_rush` (25%), `risk_marker` (100%) | 2, class Consumed | No recoil body. `COMBAT_Statuses.txt` still says 25% of damage dealt. “Heavy” has no threshold in the catalog or the resolver. |
| Immovable | `immovable_stance` | 2 | No resolver read of `StatusId.Immovable`. `ResolveSwap` checks Frozen Assets only. There is no forced-swap move. The move’s Def +2 is a stage write and stays. |
| Broadcast | `broadcast` | 2, Consumed | Next move hits all foes. Runs. |
| Staggered | Modifier `6.1-c` Suppress, on hit | 1, Consumed | Next move ×0.90. Not a Meme move. |
| Surging | Modifier `6.2-c` Crit Feed, on crit | 1, Consumed | Next move ×1.25. Not a Meme move. |
| DamageIntercept | `damage_intercept` / `signal_redirect` (hardcoded) | 1, Consumed | Redirects the next single-target hit. Runs. |
| Recharging | Any move with `rechargeTurns` > 0 | that value | Ticks only while the fighter is active. See §5. |
| GlitchedMind, HODLed, Rekt, Offline, Unsealed, Relay | no move, no passive, no matrix cell | 2, 3, 2, 2, 2, 1 | Bodies exist for all six. Nothing applies them. |

Refresh keeps the same instance, sets `turnsRemaining` to the max of the old and new duration, and does not run apply-once stages again (`StatusProcessor.ApplyStatus`).

### No producer: keep, merge, or cut

The seven ids already missing from `Statuses.csv` stay cut. Do not put DustStorm, ForkedChain, CompressedData, MemeSurge, NodeRelay, BridgedOut, or Reinforced back. Splinter is the spread. Dusted is the evasion drop. Uplink is the Node passive. Meme’s wildcard statuses that actually have producers are Staggered and Surging, and those producers are modifiers, not Meme moves.

| Status still in the CSV | Proposal | Why |
|---|---|---|
| Rekt | Cut the CSV row. | 25% action loss (`rektLoseActionChance` 0.25) overlaps Glitched Mind and Offline. No type profile names it. |
| Offline | Cut the CSV row. | Sleep that wakes on damage has a body and no profile home. Frozen’s lock is Frozen Assets, which has a move. |
| GlitchedMind | Keep, and give it one move. | The Glitched profile names it. The 50% mis-act body runs (`glitchedMindMisactChance` 0.5). |
| HODLed | Keep the row, and do not give it a producer until the body is one ramp that ends with the status. | The profile names it for Node, Block, Frozen, and Staked. The asset’s repeating Def +1 plus a second +1 while Def is below +3 can write +2 on the first end step. Neither write is owned. There is no stat-drop immunity. The unimplemented note that both writes revert does not match `ApplyApplyOnceStages`. |
| Unsealed | Keep, and give it one support move. | `COMBAT_Statuses.txt` §10.1 says support moves apply it. The resolver rolls 50% barrier bypass per hit while it is up. |
| Relay (the status) | Cut the CSV row. | Full barrier bypass for the next hit. Modifiers `4.3-b` and `4.8-a` already set barrier bypass chance to 1. The name collides with the withdrawn passive and with Effect Relay / Relay Packet. APR-RELAY kept the Node passive’s name as Uplink so this status could stay. Nothing applies the status. Cutting the unused full bypass leaves Unsealed as the partial one. |
| Amplified and the other §7 migration ids | Leave, no producer. | Already barred from the v1 matrix. |

### Dominant, redundant, or never worth applying

- **Dominant and real:** Rugged on five Burned wins, Drained on five cells, Pumped on four, Anchored on six, Dusted on four (both stages now in the asset). Kindling adds a flat 6% on top of Rugged in those lanes and does not replace it.
- **Dominant and empty:** Jammed, eight cells, no Accuracy write. Frozen, Gas, Encrypted, Compressed, and Bot all spend wins on a token. DEC-CHEM already says the chart stays until this body is chosen. This note does not move cells.
- **Redundant:** Rekt versus Glitched Mind versus Offline. Status Relay versus modifier full bypass. Yield versus Pumped (same +1 AP tick; Yield is the General version and has `yield_cycle`; Pumped is chemistry). That split is useful. Do not merge them.
- **Never worth the sentence on the card:** Burned Tokens (the Atk −1 is the only running clause), Overleveraged (no recoil), Immovable (no forced swap to block), Backlash (no contact DoT), MEV Extracted if the player expects a steal. Jammed, if the player expects Acc −2.
- **Worth applying:** Thread Lock (deletes regen while up), Exploit Mark (×1.15 for two turns), Encrypted Seal, Frozen Assets, Yield, Broadcast, Splinter, Siphoned, Drip, Firewall, Shattered, Cascade Failure at three applications, Kindled.
- **Worth applying, and the stages do not end:** Gas Spike, FUD, Shard Partition, Corroded’s repeating Def. A two-turn buff that leaves Atk +2 forever is a different card from the one `fuel_surge` prints.

---

## 5. Timing rules versus the engine

The rules this round was asked to check: move, modifier, and passive ticks resolve before chemistry and status ticks at end step; narration calls out each event; stat changes from a timed status end when the status ends unless it is refreshed; recharge counts down every turn, including on the bench.

### End step order

`ProcessEndOfTurnForTeam` (`CombatResolver`) for each living active:

1. `StatusProcessor.ProcessEndOfTurn`
2. If a buff count dropped, `OnBuffExpired` passives
3. `EndOfTurn` passives
4. Mitigation, accuracy, reactive AP, and barrier timers, then armed windows

Inside `ProcessEndOfTurn`:

1. Kindled damage, and return if that KOs
2. Other end-step damage (not Kindled, not Leaked), Drip, status AP, repeating stages, the extra HODLed +1
3. Duration `Tick`, then revert owned stages, then remove

There is no separate chemistry phase at end of turn. Chemistry statuses tick in that same status pass. End-of-turn passives run after the status pass. That is the opposite of “passive ticks before status ticks.” One result, already noted in the AP economy round and still true here: Thread Lock can tick off before Double-or-Nothing’s +1, so the lock does not eat that passive.

Kindled is pulled in front of Rugged and the other chemistry ticks. The comment in `StatusProcessor` calls that “passive and move burns before chemistry.” That part matches the rule for Kindling only. Patience, regen, Automated Pressure, Defensive Reflex’s drain, Meme Momentum, and Stake Refund still run after the status pass.

On a damaging hit the order is different from the end step. `BuildDamageModifierContext` runs before `ChemistryResolver.Resolve`. Chemistry-aware modifier bonuses run after chemistry and before damage. Kindling is applied after HP is subtracted, only if the foe is still alive. `OnHit` passives run after that. So modifier setup is before chemistry, and the on-hit passive is after chemistry and after the Kindled apply. This note treats the end-step rule as the one that fails, and the hit order as a separate fact.

### Narration

`CombatNarration_Default.asset` has `eventRules: []`, so `CombatNarrationProfileSO.ResolveDefaultDelivery` is the policy.

Called out on a live or consequence page: damage, heals, misses, dodges, barriers, KOs, swaps, status applied / refreshed / immune, stage changes, `PassiveTriggered`, `ModifierTriggered`, AP gain blocked, AP stolen, recharge start.

Not called out as a live line:

- `StatusExpired`, `StatusConsumed`, `MoveRechargeComplete`, and `WallExpired` are transcript-only. The battle log can say the status ended. The live page does not have to.
- `APGained`, `APSpent`, and `APCapped` fall through to popup-or-HUD. A passive that only grants or drains AP does not get a sentence from the AP event.
- `SystemMessage` is the same popup bucket. Uplink’s “Uplink gives {name} 1 AP.”, the lost-pay line, Spite Counter’s mirror line, and Glitched Mind’s mis-act line are system messages.
- `PassiveTriggered` uses the template “{actor}'s passive activated.” The event’s own message names the passive. The template does not.
- Kindled is in better shape: status applied uses the status template, and the burn is `HPDotDamage` with the message “Kindled burns {name} for {amount}”, which the composer keeps when the message starts with that verb. Rugged’s “Rugged scorches” is the same pattern. A generic DoT that is only `LogMessage` text is not that path.

### Timed stages end with the status

Owned, and reverted on expire: Dusted (Speed and Evasion), Bot Swarm, 51% Attacked, FOMO’s Atk, Slowed. Refresh does not reapply those writes, so a refresh extends the clock and the same delta reverts once at the end.

Not owned, so they stay after the status ends: Gas Spike, FUD, Shard Partition, HODLed’s Def writes, Corroded’s end-step Def drops. Move stage writes (`immovable_stance` Def +2, `sear_field` Atk −1, and the passive stage ladders) are not status deltas. They stay because they were never a status.

`UNIMPLEMENTED_MOVE_EFFECTS.md` says Gas Spike, Shard Partition, FUD, and HODLed’s writes now revert. `StatusProcessor.ApplyApplyOnceStages` does not call `AddOwnedStage` for those ids. The asset files match the “not owned” reading.

### Recharge on the bench

`COMBAT_Resolution.txt` §1.2 point 6: switching out does not pause recharge. `COMBAT_ISSUES_MASTER.md` E-29: “Bench recharge ticks. Other statuses still pause. Spec §1.2 matches.” Status on that row: fixed in PR.

This snapshot does not do that. `ProcessEndOfTurnForTeam` only walks `activeIndices`. There is no `TickRechargeWhileBenched` anywhere under the snapshot. `CombatRuntimeFixTests.BenchPausesStatusDurationUntilSwapIn` puts Recharging at 2 turns on a benched fighter, runs a full turn, and expects the timer to still be 2. Other statuses pausing on the bench matches the master sentence. Recharge pausing does not match §1.2 or E-29. The AP economy note already said the local resolver was behind a branch that had the bench tick. The local snapshot still is.

`gecko_guard`’s `rechargeTurns` 999 does not matter for this clock. The passive is never used as an action, so the recharge status is not applied. `TriggerOnce` is what makes it once.

### Related spec line the hit path does not follow

`COMBAT_Resolution.txt` §7.3 says passive damage with a resolved type goes through chemistry. `ApplyPassiveStrike` (Reactive Ping, Conductive Counter) subtracts HP and can KO. It does not call `ChemistryResolver`. Conductive Counter is Forked. Reactive Ping is typeless, which §7.2 would resolve as the user’s current type. Neither happens. This is separate from the end-step rule.

---

## 6. Proposals

Each item is a proposal. Numbers are the proposed numbers, not numbers already in the data, except where the proposal says to keep a value that is already there.

### P1. Make bench recharge match §1.2

Tick `StatusId.Recharging` once per end step for living benched fighters. Do not tick their other statuses.

- **Rationale.** The written rule and E-29 say the recharge clock keeps moving. The snapshot pauses it, and the test locks the pause. A boost with `rechargeTurns` 3 (`ap_boost`) can be swapped out to freeze the lock.
- **Risk.** AP Boost and Extend Protocol come back sooner if the fighter sits down. That is the rule as written.
- **Touches.** `CombatResolver.ProcessEndOfTurnForTeam` (or a bench-only recharge pass next to it). `CombatRuntimeFixTests.BenchPausesStatusDurationUntilSwapIn` would have to expect the Recharging timer to fall, and a second case would keep a non-recharge status frozen. No CSV number change.

### P2. Run end-of-turn passives before the status duration tick

At end of turn, run `OnBuffExpired` and `EndOfTurn` passives, then the status damage / AP / stage pass, then the duration tick. Keep Kindled in the passive half so it still lands before Rugged.

- **Rationale.** The end-step rule puts passive ticks before status ticks. Today Thread Lock can expire and then Double-or-Nothing gains AP.
- **Risk.** A lock would eat that end-step gain. Patience’s Def +1 would land before Corroded’s Def −1 in the same end step. Narration order would change.
- **Touches.** `ProcessEndOfTurnForTeam` and the Kindled loop in `StatusProcessor`. No CSV change. If the current “status, then passive” order is the one that should stay, the written rule is what should change. That choice is an open question, not this proposal’s assumption about approval.

### P3. Own every apply-once status stage, and revert Corroded’s ticks

On expire, subtract the stages that status applied. Refresh still does not reapply them.

- **Gas Spike, FUD, Shard Partition:** the apply-once writes revert when the duration ends.
- **Corroded:** each end-step Def −1 is remembered and the sum reverts when the status ends. Two ticks still mean Def −2 while it is up, then back.
- **HODLed:** do not ship the current double ramp. One Def +1 per end step, stop at +3, remember the sum, revert it. The immunity sentence stays unimplemented until P8.
- **Rationale.** The rule is that a timed status’s stats end with the status. `fuel_surge` otherwise leaves Atk +2 and Def −2 in place after two turns, which is a free permanent spike on a 0 AP move.
- **Risk.** Fuel Surge, Verification Lock, and Shard Partition get shorter. Corroded stops being a permanent Def ratchet. Fights that were leaning on the leftover stages get shorter walls and shorter debuffs.
- **Touches.** `StatusProcessor.ApplyApplyOnceStages`, the repeating-stage loop, and `RevertChemistryStageEffects`. Catalog text for HODLed only if the ramp number changes (`Statuses.csv` row `HODLed`). No matrix edit.

### P4. Say the passive’s name, and say when AP moves

- `PassiveTriggered` template includes the passive name. The event message already has it.
- Uplink’s grant line, Uplink’s lost-pay line, and an end-step AP change from a passive or from Pumped / Yield / Drained / MEV are outcome sentences, not popup-only.
- Status expiry stays transcript-only. Putting every expiry on the live page would bury the turn.

- **Rationale.** The narration rule asks each event to be called out. AP is the resource these passives move, and the default profile drops it.
- **Risk.** End steps get one more sentence when several AP statuses tick.
- **Touches.** `CombatNarration_Default.asset` templates, and `ResolveDefaultDelivery` for the AP events and for `SystemMessage` only where the message is already a player sentence (Uplink). Not every `SystemMessage`.

### P5. Fill Extraction Hit so Mined beats On Hit Momentum

`extraction_hit` row, trigger `OnHit`, utility:

`ChangeStage:MeleeAtk:1:40%:Self:3:false:extraction_hit`

Plus one resolver check used only by this id: if that roll succeeds and MeleeAtk is then at +3, also Speed +1, cap +1, once per battle.

- **Rationale.** The row is empty and it is in Kodiax’s pool. The typeless peer is 25% Atk +1, cap +3. 40% is a higher rate on the same cap, so the typed Common is ahead even before the Speed point. The Speed point is the Mined ramp the typeless card does not have. 40% is a proposal, not a number from the CSV. The description does not state a percent.
- **Risk.** Together with Pickaxe Flurry’s on-hit Atk raises, Mined reaches the +3 line faster and then picks up Speed. The once-per-battle gate stops the Speed point from repeating.
- **Touches.** `Moves.csv` row `extraction_hit`. A short branch in `TriggerPassiveMove` next to the On Hit Momentum case. Kodiax’s pool already lists the id.

### P6. Remove Status Resistance

Delete the `status_resistance` row.

- **Rationale.** It does not run. The described effect, with a floor of 1 turn, would not change Thread Lock, Frozen Assets, or Bot Swarm, because those are already duration 1. Chemistry would stay excluded by the description. The card would still do nothing against the debuffs players actually press.
- **Risk.** A later “debuffs fall off” passive has to be a new id. No species pool lists this row.
- **Touches.** `Moves.csv` row `status_resistance`, and the move asset on the next import. If the floor of 1 is not the intended card, the alternative is in the open questions.

### P7. Streamlined Flow: one Def drop, discount unchanged

- Delete the stage-column cell `MeleeDef:-1:1.0:true`.
- Keep `MoveAPDiscountWhileHPAbove:1:70` and one `ChangeStage:MeleeDef:-1:100%:Self:-1`.
- Set `TriggerOnce` to true.

The discount is read from the utility list at cost time, not from the trigger, so it keeps working on every action above 70% HP. The Def write happens on the first action that passes the 70% check, then the once-flag stops it.

- **Rationale.** The row says Def falls −1. The engine applies −2 per action down to −6. A single −1 keeps the typed payoff (a 3 AP move costs 2 while healthy, including turn 1) without deleting Humpacked’s defense.
- **Risk.** The self-tax becomes a one-time −1 (2/3 defense) instead of a ladder. If the intent was a repeating tax, this is the wrong shape. That is an open question.
- **Touches.** `Moves.csv` row `streamlined_flow` only.

### P8. Low AP Surge and Low AP Burst check the end of the turn, and they stop early

Both rows: trigger `EndOfTurn`, condition `Self:APAtMost:0`.

- Surge: `ChangeStage:Speed:1:100%:Self:1`
- Burst: `ChangeStage:Speed:2:100%:Self:2`

- **Rationale.** “At most 1 AP after +2 regen” does not happen when regen lands. End of turn, AP 0 means they spent the window. Turn 1 pays if they spend both starting points. Surge stops at Speed +1 (1.5×). Burst stops at +2 (2.0×). The Rare is the same job, one stage higher, and neither walks to +6. They do not fire for a fighter who is banking, which keeps them off Fee Collector’s job.
- **Risk.** Any spender who dumps AP gets a permanent Speed bump by the end of turn 1. That is still a strong typeless passive. The caps are what keep it to one step.
- **Touches.** `Moves.csv` rows `low_ap_surge` and `low_ap_burst`. Description text on those rows should say “end of turn, if you have 0 AP, Speed +1” and “Speed +2”.

### P9. Patience stops at Def +3

`patience_passive` utility cap changes from 6 to 3. The +1 per end step and the 5% heal stay.

- **Rationale.** Cap +6 is 4.0× defense for free, every fight, on top of the heal. Cap +3 is 2.5× and still the strongest wall passive. It still pays Def +1 at the end of turn 1.
- **Risk.** Long Staked fights are less sticky. Hopoch’s pool grants this passive, so the species gets the nerf.
- **Touches.** `Moves.csv` row `patience_passive`, the `6` in `ChangeStage:MeleeDef:1:100%:Self:6:false:patience_def`.

### P10. Unstable Code adds +1, and stops at +3

`unstable_code` utility becomes `RandomStageChange:1:1:100%:Self:3`.

- **Rationale.** +1 or +2 every turn start up to +6 is a turn-1 stage engine with no action cost. +1 per turn, ceiling +3, is still a random climb the typeless cards do not get, and it still pays before the action on turn 1. Glitched stays random. It stops at 2.5× on the stat that won the rolls.
- **Risk.** Kreechank’s pool grants this. The species loses the +2 spikes and the +6 ceiling.
- **Touches.** `Moves.csv` row `unstable_code`.

### P11. Automated Pressure stops at −2

`automated_pressure` stage cap changes from −6 to −2. The −1 per end step, the random foe, and the random stat stay.

- **Rationale.** Walking five stats to 0.25× is more pressure than a Common passive should have next to Fee Collector’s conditional −1 AP. A floor of −2 is 0.5× on a stat that keeps getting picked, and the random spread means it often sits at −1. It still pays at end of turn 1. It stays a stat chip. Turning it into an unconditional AP drain would beat Fee Collector in a 1v1, and Fee Collector is the Rare tax. DEC-CD already withdrew the earlier Bot card set, so this proposal does not add a new Bot move.
- **Risk.** Bot’s granted passive remains off the “AP denial” profile line. Kitsauto’s long fights debuff less.
- **Touches.** `Moves.csv` row `automated_pressure`, the cap in `RandomStageChange:-1:-1:100%:RandomEnemy:-6:automated_pressure`.

### P12. Firewall Passive mitigates projectile hits by 30%

`firewall_passive` changes from 25% to 30%: `IncomingDamageMitigation:30%:Projectile:firewall_passive`.

- **Rationale.** `COMBAT_Resolution.txt` §8.4 caps one mitigation source at 30%. The typed Uncommon should beat the typeless Epic’s 20% on the hits it cares about. 30% does that for projectiles and still does nothing to contact. The config’s `mitigationSingleSourceCap` is 1, so 0.30 is not clipped by the asset.
- **Risk.** Keyper’s pool grants this. Ranged teams lose more into Encrypted. Stacking with chemistry Firewall (also 30% on the first projectile each turn) is multiplicative in `ApplyDefensivePassiveMitigation`’s formula. A projectile could see both.
- **Touches.** `Moves.csv` row `firewall_passive`.

### P13. Wire Double-or-Nothing’s immunity, and do not teach it to a 0 HP species yet

On the 40% branch, drain 1 AP, and until the next end step `CombatApService.Drain` and `Steal` from another fighter do not reduce this fighter’s AP. The self drain of this branch still happens. The 60% branch stays +1 AP. No new status.

- **Rationale.** The immunity is the sentence that makes a Meme Common more than a noisy typeless +1. Without it, expected value is about +0.2 AP and `turn_start_ap_gain` is strictly better.
- **Risk.** Fee Collector, MEV, and Transaction Tax lose a point into this passive on the turns the 40% hits. Ghost still has 0 HP, so this does not put the passive on a battling species by itself.
- **Touches.** `TriggerPassiveMove`’s `double_or_nothing` branch, and `CombatApService.Drain` / `Steal`. No new move name. Do not add it to Balloom’s off-type list in this proposal.

### P14. Grant Conductive Counter to Forkrook without dropping a Common damaging move

`SpeciesMovePools.csv` Forkrook learnable: replace `pivot_line` with `conductive_counter`.

The pool stays five. `rapid_strike`, `fork_split`, and `baseline_shot` remain. `rapid_strike` and `fork_split` are Forked Common damaging moves. `baseline_shot` is the typeless Common damaging move.

- **Rationale.** Forked’s only passive is real and ungranted. Pivot Line is a self stage utility, not the last damaging move.
- **Risk.** Forkrook loses the Accuracy / Evasion self-buff. The passive still does nothing against a foe who never makes contact.
- **Touches.** `SpeciesMovePools.csv` row `forkrook`.

### P15. Kindling reads the catalog percent

`StatusProcessor` uses the Kindled row’s `endOfTurnDamagePercent` (already 0.06) instead of a second literal `0.06f`.

- **Rationale.** APR-KIND’s number should have one source. This does not change the 6%.
- **Risk.** None while the asset stays 0.06. A later CSV edit would start to matter, which is the point.
- **Touches.** `StatusProcessor` Kindled loop. No balance change.

### P16. Jammed applies Acc −2 for its 2 turns, and the stage ends with the status

`Statuses.csv` row `Jammed`: stage `Accuracy:-2` as a combat stage, apply-once, duration stays 2. The apply path owns it the way Dusted’s Evasion write is owned. No matrix edit.

- **Rationale.** Eight cells currently apply a blank. Frozen’s speed-control profile and Gas’s disrupt profile are spending wins on that blank. Acc −2 is the summary already on the row. DEC-CHEM can stay “do not move cells” after the body exists.
- **Risk.** Those eight matchups gain a real accuracy drop for two end steps. Moves with accuracy 0 still force a hit (`ResolveDamagingMove`), so this does not touch them.
- **Touches.** `Statuses.csv` row `Jammed`, the importer’s combat-stage parse (it already accepts Accuracy), and the owned-combat-stage path that Dusted already uses.

### P17. One Glitched move applies Glitched Mind

New row, not in Kreechank’s five-move pool until a slot is chosen:

`mind_fault,Mind Fault,StatusUtility,Glitched,Common,SingleEnemy,None,0,2,0,1,1,0,GlitchedMind:2:1.0:,,UTIL,...`

Description: “Foe mis-acts half the time for 2 turns.”

The name is not on `Moves.csv` today. Kreechank can equip it once it is known, because equip follows type. `chaos_strike` and `bug_payoff` stay the Common damaging moves in the pool. This proposal does not remove `defense_corrupt` or `unstable_code`.

- **Rationale.** The profile’s key General status has a body and no card.
- **Risk.** 50% mis-act for two turns at 2 AP is a strong control button. It costs the action, so it does not also swing damage that turn.
- **Touches.** `Moves.csv` only, until a pool slot is chosen.

### P18. HODLed gets a producer only after P3

After P3’s single ramp (Def +1 per end step, cap +3, reverts):

`vault_hold,Vault Hold,StatusUtility,Block,Common,Self,None,0,2,0,1,1,0,HODLed:3:1.0:Self,...`

Description: “Def rises by 1 at each end step for 3 turns, up to +3, then the extra Def ends.”

Do not import this row first. The current body can write +2 on the first tick and then leave the Def in place.

- **Rationale.** Four profiles name HODLed. Block is the wall. 2 AP is legal on turn 2, not on turn 1, so the ramp is the payback rather than a turn-1 wall. The name is not on `Moves.csv`.
- **Risk.** Blockwyrm’s pool is already five moves (`bulwark_blow`, `immovable_stance`, `blockade`, `immutable_ledger`, `ice_wall`). This proposal does not insert it there. `bulwark_blow` stays a damaging move.
- **Touches.** `Moves.csv` after the `StatusProcessor` change in P3. `Statuses.csv` summary for HODLed should drop “Immune to stat drops” until immunity exists. Immunity is not part of this proposal: there is still no stat-drop check in `SetStage`.

### P19. Cut Rekt, Offline, and the Relay status

Remove those three rows from `Statuses.csv`. Leave the enum values so old assets still deserialize. Do not add moves.

- **Rationale.** Section 4. Rekt and Offline have no profile home. The Relay status duplicates modifier bypass and collides with the Uplink naming history.
- **Risk.** A future card that wanted a 25% action skip or a full bypass has to be a new status. The resolver branches can stay; they will not be reached.
- **Touches.** `Statuses.csv` rows `Rekt`, `Offline`, `Relay`.

### P20. One support applies Unsealed

`seal_crack,Seal Crack,Support,Shard,Common,Self,None,0,2,0,1,1,0,Unsealed:2:1.0:Self,...`

Description: “For 2 turns, each of your hits has a 50% chance to ignore a single-hit barrier.”

Shard is the blade that wants to cut walls. The name is not on `Moves.csv`. Marrowhead’s pool stays as it is (`partition_shot` is the Common damaging move already in that pool). This is not inserted into the five.

- **Rationale.** The bypass body runs. No card applies it. Damage Intercept already has moves, so this is the missing half of that pair.
- **Risk.** 50% per hit for two turns is a real answer to barriers, on a type that also has Shattered Armor. It does not bypass on a miss.
- **Touches.** `Moves.csv` only.

### P21. Burned Tokens heals the applier for the end-step burn, or the sentence comes off the card

Preferred: while Burned Tokens is up, end-step damage from Kindled or Rugged on that fighter also heals the Chainling who applied `deflation_mark`, for the same integer, capped at that applier’s missing HP. If the applier is gone, the burn still happens and nobody is healed.

- **Rationale.** Burned’s profile names Burned Tokens. The move applies the status and an Atk −1. Only the Atk −1 runs. The heal is the typed clause.
- **Risk.** In a Rugged lane the applier can heal 1/16, then 2/16, then 3/16 of the foe’s MaxHP on top of Kindling’s 6% if both are up. That is a lot of free healing. If that stack is too much, the smaller version is: the heal copies Kindled only, not Rugged.
- **Touches.** `StatusProcessor` end-step damage, using a stored applier id. `deflation_mark` already applies the status. The status row stays.

Alternative if the heal is rejected: change `deflation_mark`’s description to “Lower Atk −1.” and stop applying `BurnedTokens`. The status row then has no producer and should be cut in the same change. This note prefers the heal, with the Kindled-only option if the Rugged copy is too large.

### P22. Overleveraged recoils 25% of the next damaging hit, then is consumed

On the next damaging hit that deals HP damage, the bearer loses `floor(damage × 0.25)` more HP, then the status is consumed. No power threshold.

- **Rationale.** `COMBAT_Statuses.txt` gives 25%. The catalog says “heavy” and never defines it. `voltage_rush` and `risk_marker` already apply the status. Using any damaging hit makes the existing cards do what the percent says.
- **Risk.** Voltage Rush’s 25% self-apply becomes a real self-tax. Risk Marker becomes a real punish. A defined “heavy” threshold would hit less often.
- **Touches.** The damage path in `CombatResolver` after HP is applied, and `Statuses.csv` summary if “heavy” is dropped. Both move rows keep their status tokens.

### P23. MEV Extracted steals 1

At end of turn, the bearer loses 1 AP and the Chainling who owns the `mev_extract` that applied it gains 1, if that Chainling is active. If they are benched or gone, the bearer still loses 1 and nobody gains it. Duration stays 2.

- **Rationale.** The move text says steal. The processor drains. A steal of 1 per tick for two turns is the Bot Common that typeless `ap_drain` (one drain, 25% of that point stolen) does not match. Bot stays the denial type without a new move, and DEC-CD is untouched.
- **Risk.** The user is paid as well as the foe being taxed, so the swing is 2 AP of distance per tick. Thread Lock still blocks the gain if it is up on the user.
- **Touches.** `StatusProcessor` AP section for `MEVExtracted`, and a stored applier. `Moves.csv` description can stay.

### P24. Immovable’s card says what runs

Change `immovable_stance` description to “Gain Def +2.” Leave the `Immovable` status on the move until a forced swap exists, or take the status token off in the same edit so the row does not promise immunity.

- **Rationale.** Nothing reads the status. The Def +2 runs and does not expire. Printing a two-turn immunity is the bug.
- **Risk.** Players lose a promise the engine was not keeping. Block’s real wall tools stay Ledger, the Def +2, and barriers.
- **Touches.** `Moves.csv` row `immovable_stance`. Optional: clear `StatusEffects` on that row.

### P25. Backlash stays a token until a contact number is chosen

Do not add a contact DoT in this round. Change the catalog summary to “Token. No contact damage.” or leave the two cells and accept they are blank. A typed contact DoT needs a percent this snapshot does not contain. Inventing one would be a new burn on top of Kindling and Rugged.

- **Touches.** `Statuses.csv` row `BacklashType`, summary only, if the blank should be visible. No matrix edit.

---

## 7. Shortlist

If only a few of these move:

1. **P1 and P3.** The bench clock and the stages that do not end are rules the current code breaks. They change fights more than a new card does. `fuel_surge` leaving a permanent Atk +2 is the sharpest case.
2. **P5.** Kodiax is granted a passive that does nothing, next to a typeless passive that works.
3. **P7.** Streamlined Flow’s discount is the right Compressed payoff. The double Def drop is not what the row says.
4. **P10 and P9.** Unstable Code and Patience are the two passives that are strictly ahead of every peer. Cap them before adding anything new.
5. **P8.** Low AP Surge and Burst are dead under +2 regen. The end-of-turn, 0 AP, capped Speed bump gives them a job that is not Fee Collector’s.
6. **P16.** Eight chemistry cells are blank. The chart decision can wait. The body should not.
7. **P17 and P19.** Glitched Mind gets one card. Rekt, Offline, and the Relay status come out so the action-loss and bypass lists stop stacking unused rows.
8. **P11.** Automated Pressure should not walk stats to −6. Whether Bot’s passive should be AP instead is an open question, not a second card.

P2 (end-step order), P4 (narration), P12 (Firewall 30%), P13 (Meme immunity), P14 (Forkrook’s pool), and P21–P24 are real and should wait on the questions below. P15 is a one-line cleanup whenever Kindling is next touched. P6 should happen if Status Resistance is not going to be wired to a floor of 0.

---

## 8. Open questions for Cronus

1. End of turn: should passive ticks move ahead of the status tick (P2), or should the written rule change to the order the snapshot runs (status, then passives)?
2. Bench recharge: E-29 says the clock ticks, and this snapshot’s test says it does not. Which clock is the rule?
3. Patience at Def +6: is walking to the global cap the Staked wall, or is +3 enough?
4. Unstable Code: is a random +1 or +2 every turn, up to +6, the Glitched passive, or is a +1 climb that stops at +3 the right size?
5. Low AP: the current “at most 1 after regen” gate does not fire when regen lands. Should the passive mean “you spent to 0” (P8), or is it only meant to fire when something blocked regen?
6. Streamlined Flow’s Def drop: one −1 per battle, or −1 on every action while healthy?
7. Bot’s granted passive is a stat chip, not AP denial. Should it stay that way once the floor is −2, given that the earlier Bot card set was not approved?
8. Jammed: is Acc −2, reverted after 2 turns, the body, or should those eight cells stay tokens until the chart pass?
9. Overleveraged: does “heavy” mean any damaging hit, or is there a power or AP line that was never written down?
10. Burned Tokens: should the applier heal the Kindled tick, the Kindled tick and the Rugged tick, or should the sentence come off `deflation_mark`?
11. HODLed immunity to stat drops: is that still part of the status, or only the Def ramp?
12. Cofi’s pool has no Staked passive. Should the boss stay that way?
13. Ghost has 0 HP. Should Double-or-Nothing and Meme Momentum wait until Ghost has a body, rather than being tuned in the abstract?
14. Status Resistance: cut it, or should a General debuff be allowed to fall to 0 turns so the passive can erase a duration-1 lock?

---

## Sources checked

Snapshot root used for every path below: the unzipped `chainbound_combat_snapshot_2026-10-17` archive. Line-level behavior is from these files.

- `Docs/Specs/Combat/Data/Moves.csv` (208 data rows, 31 passives)
- `Docs/Specs/Combat/Data/Statuses.csv`
- `Docs/Specs/Combat/Data/SpeciesMovePools.csv`
- `Docs/Specs/Combat/Data/Modifiers.csv` (`6.1-c`, `6.2-c`, `4.3-b`, `4.8-a`)
- `Docs/Specs/Combat/COMBAT_TypeProfiles.md`
- `Docs/Specs/Combat/COMBAT_Resolution.txt` §1.2, §1.3, §7, §8.4
- `Docs/Specs/Combat/COMBAT_Statuses.txt` (Overleveraged 25%, Rekt 25%)
- `Docs/Specs/Combat/COMBAT_Chemistry.txt` §7
- `Docs/Specs/Combat/COMBAT_Interaction_Matrix_v1.csv` (57 filled cells)
- `Docs/Combat/COMBAT_ISSUES_MASTER.md` (APR-RELAY, APR-KIND, DEC-PACK, DEC-CD, DEC-CHEM, E-29, E-02, E-07, E-08)
- `Docs/Combat/Audits/CATALOG_STATUSES_WITHOUT_PRODUCERS.md` (stale where it still lists rows removed from the CSV)
- `Docs/Combat/Audits/EMPTY_PAYLOAD_INVESTIGATION.md` (stale for `network_sync` and `extend_protocol`; still accurate that an empty utility cell does not run)
- `Docs/Combat/Audits/UNIMPLEMENTED_MOVE_EFFECTS.md` (double Def on Streamlined Flow; stale where it says Gas Spike, FUD, Shard Partition, and HODLed stages now revert, and where it says Dusted’s Evasion is not applied)
- `Docs/Combat/Audits/BATTLE_EFFECTS_CORRECTNESS_AUDIT.md` (E-28, E-29 body versus the decision table)
- `Docs/Combat/Design/TYPE_GAP_PROPOSALS.md`, `AP_ECONOMY_ANALYSIS.md`, `COMBAT_SYSTEMS_ANALYSIS.md`, `COMBAT_DESIGN_IDEAS.md` (prior proposals; not re-litigated)
- `Docs/Combat/Combat_Narration_And_Battle_Report.md`
- `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs` (passive triggers, Uplink, Kindling, end of turn, recharge apply, hit order)
- `Assets/Scripts/Combat/Runtime/Engine/StatusProcessor.cs`
- `Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs`
- `Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs`
- `Assets/Scripts/Combat/Runtime/Engine/CombatActionEligibilityService.cs`
- `Assets/Scripts/Combat/Runtime/Engine/ChemistryResolver.cs` (Rugged counter starts at 1)
- `Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs` (Exploit Mark, Staggered, Surging, Amplified)
- `Assets/Scripts/Combat/Runtime/Data/ChainlingInstance.cs` (`StageMultiplier`)
- `Assets/Scripts/Combat/Runtime/Data/CombatConstants.cs`
- `Assets/Scripts/Combat/Runtime/Data/StatusId.cs`
- `Assets/Scripts/Combat/Runtime/Data/SpeciesMoveEquipRules.cs`
- `Assets/Scripts/Combat/Runtime/Data/TurnActionLedger.cs` (`NoteUplinkAlly`, `TryClaimUplinkPay`)
- `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs`
- `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs`
- `Assets/Data/Combat/Config/CombatResolverConfig_Default.asset`
- `Assets/Data/Combat/Narration/CombatNarration_Default.asset`
- `Assets/Data/Combat/StatusDef/StatusDef_Jammed.asset`, `StatusDef_Dusted.asset`, `StatusDef_HODLed.asset`, `StatusDef_Corroded.asset`, `StatusDef_GasSpike.asset`, `StatusDef_FUD.asset`, `StatusDef_ShardPartition.asset`
- Species `baseMaxHP` and `primaryType` from `Assets/Data/Combat/ChainlingSpecies/*.asset` (the 19 species files in that folder, not the Visual copies)
- `Assets/Scripts/Combat/Tests/CombatRuntimeFixTests.cs` (`BenchPausesStatusDurationUntilSwapIn`)
- `Assets/Scripts/Combat/Tests/CombatIssuesRemediationV2Tests.cs` (Uplink, Kindling, CSV assertions)

Not read: anything under `Docs/GameTheory/`.
