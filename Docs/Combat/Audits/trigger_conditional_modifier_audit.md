# Trigger and conditional modifier audit

> Second pass (2026-10-21) on modifier narration: see [Second pass (2026-10-21): Modifier narration](#second-pass-2026-10-21-modifier-narration) at the end of this doc.

Date: 2026-10-21

Scope: catalog sections 6.1–6.8 (triggers) and the conditional synergy rows 7.1-a, 7.1-b, 7.1-c, 7.1-d, 7.2-a, 7.2-b, 7.2-c, 7.3-a. These are the stamps whose value is "this fires when the situation is right."

This pass does not change resolver behavior. The follow-up agent should make the known-fault tests in `Assets/Scripts/Combat/Tests/TriggerConditionalModifierAuditTests.cs` pass, and should update a contract test only when the finding below says the current behavior is the rule to keep.

Source of truth for what a stamp promises: `Docs/Specs/Combat/Data/Modifiers.csv`. Runtime truth for what it does: `CombatResolver`, `SwapModifierResolver`, `TurnActionLedger`.

## How these modifiers actually resolve

There is no generic "read the description and do it" engine. Each section id has its own branch. `TriggerCondition` only selects the moment (`OnHit`, `OnCrit`, `OnKO`, `OnEscape`, `OnSwapIn`, `OnSwapOut`, `OnLowHP`). The effect inside that moment is hardcoded.

Two different chance paths exist:

- On-hit, on-crit, on-KO, and on-escape call `CheckModifierTriggerChance`. That reads `ModifierDefinitionSO.triggerChance` only. A locked stamp value is ignored. `triggerChance <= 0` is treated as 100%.
- Battle-start, riposte, cleanse, mirror, and the other 6.8 rolls call `RollPassAChance`, which uses the stamped effect percent. Those stamps do change the odds.

`ProcessAfterDamageModifiers` is per landed hit, after barrier absorption. A full absorb `continue`s before that method, before riposte, and before the damage-context popup. Move statuses and stage changes still run on that same fully blocked hit.

`BuildDamageModifierContext` runs once, against the selected target, before the hit loop. Execute, Underdog, Revenge Spark, Punish Miss, Duo Boost, Tag Team, and Pile-On then ride that one context onto every target of a spread.

Swap mods are not in `CombatResolver`. `SwapModifierResolver` runs on a voluntary swap and on a faint auto-swap. Ally-target rows (6.4-d, 6.4-e, 6.4-h, 6.4-i) refuse a host that is not a Support move. The first successful swap mod of that trigger wins; later swap mods on the same Chainling do not fire.

On-escape is not a flee from battle. The only caller is `ResolveSwap`, and it scans the opponent's moves, not the swapper's. One successful on-escape mod per opponent stops the scan.

End of turn ticks mitigation and barriers (`TickMitigationSources`, `TickBarriers`). Start of turn does not re-fire battle-start stamps. It does roll the previous turn's misses and hits into the "last turn" lists (`TurnActionLedger.BeginTurn`), which is what Punish Miss reads.

## What the two players see

A success popup uses `ResolveModifierDisplayText`. Named labels exist for leech (`HEAL`), siphon (`AP DRAIN`), stagger (`STAGGER`), the three crit rows, both KO rows, both escape rows, and the 6.8 rows. Everything else in section 6 falls through to `TRIGGER`. Section 7 falls through to `SYNC`, except 7.1-d and 7.3-a which say `SHARE`. Shield Link and Hype Pass bypass that map and say `MITIGATE` and `STAT +1`. Grit Guard says `MITIGATE`.

`HandleModifierTriggered` drops any outcome with `gameplayApplied == false`. A condition that was not met is logged and never shown.

The default narration template is `A combat modifier changed the outcome.` Setup Feed is the only modifier with its own sentence. HP, AP, stage, and status events still narrate themselves, so a leech heal or a speed stage is visible. A pure damage multiplier is not. In a PvP battle both clients share the log, and neither log names the stamp.

## Row ledger

| Section | Promised moment | What the code does | Verdict |
|---|---|---|---|
| 6.1-a/d/e/f | On hit, heal a percent of damage | Heals from damage that got through. Full HP still reports success. Full wall block skips it. Each hit and each spread target procs. | Works, with false success and wall skip |
| 6.1-b AP Siphon | On hit, 15–25% chance, drain 1 AP | Flat `triggerChance` 0.2. Stamped 15–25 is unused. Drain is hardcoded 1. Success is logged even when no AP moves. | Stamp range is dead |
| 6.1-c Suppress | On hit, 15–25% chance, next move −10% | Same dead range. Applies `Staggered`. The −10% comes from `staggeredDamageMultiplier` (0.90), not the stamp. Return value of `ApplyStatus` is ignored. | Stamp range is dead |
| 6.2-a Crit Wallet | On crit, +1 AP | Fires only on a crit. Amount is hardcoded 1, which matches the row. | Works |
| 6.2-b Crit Shatter | On crit, −2 Def | Drops the defense of the contact type only (melee def or ranged def), by 2. At the −6 floor it still reports `DEF -2`. | Works, false success at the cap |
| 6.2-c Crit Feed | On crit, next move +25% | Applies `Surging`. The +25% is `surgingDamageMultiplier`, not the stamp. | Works |
| 6.3-a Execute | Target HP < 50%, +25–35% power | Strict `<`. Exactly half does not fire. Popup is `TRIGGER`. The multiplier is reused on every spread target. | Fires, unreadable, leaks on spread |
| 6.3-b Underdog | Your HP < target HP, +20–30% | Compares current HP, not percent. Same popup and spread leak. | Fires, unreadable, leaks on spread |
| 6.3-c Grit Guard | HP < 30%, mitigation for the rest of this turn | Only when that move is used. Duration 1 is removed at end of turn. A slower carrier mitigates nothing. | Timing hole |
| 6.3-d Revenge Spark | "If an ally KO'd" | Fires when a teammate fainted this turn. Does not fire when a teammate scores a KO. Popup is `TRIGGER`. | Name matches the code; the sentence is ambiguous |
| 6.3-e Punish Miss | Target missed last turn | Uses `actorsThatMissedLastTurn`, filled at the next `BeginTurn`. +20% accuracy and ×1.15 power are hardcoded, not the stamp. | Ledger is correct; popup is `TRIGGER` |
| 6.4-a/b/c/f | Swap in or out, self | Brace, Warm Welcome, Flex In, Parting Gift hit the actor who owns the stamp. Flex In uses `BuffGate`. One successful swap trigger per Chainling. | Works on the right host |
| 6.4-d/e/g/h/i | Swap, often an ally | Cover, Hand-Off, Spotter, and Cheer require a Support host and a living other active. Fade Guard puts mitigation on the replacement. Cheer uses `BuffGate`. A non-support host logs a refusal and does nothing. | Combat gate is correct; the stamp pool can still assign these ids |
| 6.5-a/b | On KO, +1 AP or +1 Speed | Description sniff: the text must contain `ap` or `speed stage`. Popup labels are right. A full AP bar grants nothing and shows nothing. | Works while the description text stays |
| 6.6-a Tripwire | On the opponent's swap, Slow 1 turn | Implemented and already covered by `CombatIssuesRemediationV2Tests`. | Works |
| 6.6-b Parting Shot | On the opponent's swap, 5–10% Max HP chip | Chips from the description words `chip` / `maxhp`. If Tripwire on an earlier move already fired, this row is skipped. | Second escape stamp is dropped |
| 6.7-a/b/c | On hit, a barrier | Afterguard is 1 hit / 2 turns. Double Guard uses the effect value as hits and lasts 3 turns. Threshold Guard is a percent of max HP for 2 turns. A miss or a full wall block grants nothing. `exclusiveBarriersAndWalls` replaces any barrier already on the user. The 2-turn barrier survives the granting end-of-turn tick. | Works, and it deletes a stronger existing barrier |
| 6.8-a Patch Kit | On use, heal | Already covered. Heal of 0 does not report success. | Works |
| 6.8-b Pre-emptive | Battle start, chance to cast | Chance uses the stamp. Damaging hosts are refused. The cast runs utility effects only. It does not repeat at the next turn start. | Works on a support/utility host |
| 6.8-c Riposte | When hit, chance to cast | Requires damage that got through. Same damaging-host refusal. | Same gate as Pre-emptive |
| 6.8-d Sticky Mirror | On incoming hit, reflect | Separate from 6.7. Not re-tested in this sweep. | Present, thin coverage |
| 6.8-e Sparse Ward | Empty bench slots add mitigation | Battle start only if the host is Passive; also on use. Duration 1. | Present |
| 6.8-f Cleanse | On use, clear one self debuff | Chance uses the stamp. Only `isDebuff` statuses of `StatusOrigin.General`. | Works |
| 6.8-g Spotlight | On use, ally accuracy | No ally, no effect. Duration 1. | Works |
| 7.1-a Duo Boost | Power per buffed partner | Damage context. Popup `SYNC`. Spread uses one context. | Fires, unreadable |
| 7.1-b Shield Link | Support gives the target mitigation | Popup `MITIGATE`. Duration 1. | Works |
| 7.1-c Hype Pass | Support also grants +1 random stage | `SetStage` directly. Cipher Lock does not stop it. | Status gate missing |
| 7.1-d Mind-Meld | Copy self-buffs to an ally | Same path as 7.3-a. Cipher Lock does not stop the copy. | Status gate missing |
| 7.2-a Tag Team | An ally already used a damaging move | Ledger excludes the user. Popup `SYNC`. | Fires |
| 7.2-b Pile-On | Target was hit earlier this turn | Recorded per hit, and the context is built before this move's own hits, so a multi-hit does not pile onto itself. Popup `SYNC`. | Fires |
| 7.2-c Setup Feed | Next ally power move | Already has its own narration. A new turn does not clear a pending feed. | Present |
| 7.3-a Shared Buff | Self-buffs also hit one ally | Copies self statuses that are buffs and self stage changes. Cipher Lock does not stop it. | Status gate missing |

## Faults for the follow-up agent

> **Runner check 2026-10-25:** all 25 tests in `TriggerConditionalModifierAuditTests.cs` pass in the Editor, including every `TriggerConditionalModifierKnownFaultTests` case. Faults 1–5 are fixed in `CombatResolver`. Faults 7 and 9 were resolved by the rule changes now locked in the contract tests `GritGuard_ProtectsAfterTheCarrierActs_AndCoversTheFollowingTurn` and `PostCastBarrier_GrantsOnHit_SkipsAMiss_AndPreservesAnEqualBarrier`. Fault 6 (full-block proc rule) is still open: its contract test still locks today's split. Fault 8 (stamp roller ignores host category, server side) was not re-checked.

Ordered by how directly they cheat a stamped asset.

### 1. The stamped percent does not drive on-hit, on-crit, on-KO, or on-escape odds

`CheckModifierTriggerChance` never reads `ModifierInstance.effectValue`. AP Siphon and Suppress are sold as 15–25%. The catalog column `TriggerChance` is 0.2, and that is the only number the resolver rolls. The locked stamp is decorative. Crit and KO rows happen to be 100%, so they hide this. Pass A rows already use the stamp, so the two families disagree.

Known-fault test: `ApSiphon_UsesTheStampedChanceRatherThanTheFlatTriggerChance`.

Fix inside the existing section handlers. Do not add another one-off description parser. The follow-up should pick one rule and apply it to every proc in this family: the locked effect value is the chance when the description's range is a chance, and `triggerChance` is only the fallback when no stamp value is locked. Then either keep the drain/stage amounts hardcoded, or stop putting those amounts in `EffectMin`/`EffectMax` so the columns stop meaning two things.

### 2. A successful popup can describe a change that did not happen

- Leech logs `HEAL` when the attacker is already at full HP (`ProcessAfterDamageModifiers` always passes `true`).
- Crit Shatter logs `DEF -2` when the stage was already at the floor and the delta is 0.
- Suppress and Crit Feed log success without checking `ApplyStatus`'s return.
- AP Siphon logs success without checking that `Steal` moved any AP.

Known-fault tests: `Leech_AtFullHpDoesNotClaimAHeal`, `CritShatter_AtTheStageFloorDoesNotClaimADrop`.

`gameplayApplied` has to be false when the HP, AP, stage, or status did not change. The presenter already hides `false`.

### 3. Execute and the other damage conditionals are invisible, and they leak across a spread

`ResolveModifierDisplayText` has no case for 6.3-a/b/d/e, 7.1-a, 7.2-a, or 7.2-b. Players get `TRIGGER` or `SYNC`, then the narration line "A combat modifier changed the outcome."

`BuildDamageModifierContext` is once per move. An All Enemies move whose selected target is under half HP gives Execute's multiplier to the healthy targets too. On-hit leech does not have this bug; it runs inside the per-target loop.

Known-fault tests: `Execute_PopupNamesTheConditional`, `Execute_DoesNotLeakOntoAHealthySpreadTarget`.

The popup text should name the row (`EXECUTE`, `UNDERDOG`, `REVENGE`, `PUNISH`, `TAG`, `PILE`, `DUO`) the same way KO and crit rows already do. The context for HP comparisons and "was this target hit" has to be built per target. Team-wide checks (Tag Team, Duo Boost, Revenge Spark) can stay once per move.

### 4. Cipher Lock does not stop copied or random buffs from these stamps

`BuffGate` guards Flex In and Cheer. It does not guard Hype Pass, Mind-Meld, or Shared Buff. Those call `SetStage` and `ApplyStatus` directly. A locked ally still receives the buff. Crit Shatter is a debuff and should keep bypassing the gate.

Known-fault tests: `SharedBuff_RespectsCipherLock`, `HypePass_RespectsCipherLock`.

### 5. A second on-escape stamp on the same Chainling never fires

`ProcessOnEscapeModifiers` sets `triggeredForOpponent` after the first applied mod. Tripwire on move 1 prevents Parting Shot on move 2. Swap mods have the same "first success wins" stop, and that matches the one-swap-mod loadout rule. Escape has no such rule. Two stamped moves should both be allowed to answer a swap.

Known-fault test: `EscapeTriggers_OnSeparateMovesBothApply`.

### 6. Full wall block is a hit for the move and a miss for the stamp

In `ResolveDamagingMove`, damage that a barrier reduces to 0 still applies the move's statuses and stage changes, then `continue`s. Leech, siphon, stagger, crit procs, post-cast barriers, and riposte do not run. The attack roll succeeded.

Contract test that locks today's split, so a change is deliberate: `Leech_DoesNotHealWhenABarrierFullyBlocksTheHit_ButTheMoveStatusStillLands`.

Recommended rule: a hit that connected procs on-hit stamps from the pre-barrier damage for anything that is not a heal-from-damage. Leech of zero damage should not heal and should not popup. Siphon, stagger, and Afterguard should still see the connection. Do not change this test's assertion until that rule is chosen.

### 7. Grit Guard expires before it can matter for the slower Chainling

`ProcessLowHpModifierWindows` runs at the start of the action that uses the stamped move. Mitigation duration is 1 and `EndTurn` ticks it off. Swaps are safer because they resolve before the combat phase, so Brace still covers that turn's attacks. Grit only covers hits that happen after the carrier acts and before end of turn.

Contract test: `GritGuard_ProtectsOnlyHitsAfterTheCarrierActs_ThenExpiresAtEndOfTurn`.

The row says "for the rest of this turn," and the code matches that sentence. The value problem is that "rest of this turn" is empty when you move last. The follow-up should extend this one row to last until the carrier's next action, or until the end of the next turn, and should say so in the CSV. Do not silently make every duration-1 mitigation last longer.

### 8. The stamp roller does not know the host move

`deriveStampResult` in `Supabase/functions/_shared/ownership_spine.ts` picks `MODIFIER_POOL[digest % length]`. It does not receive the move category. The pinned pool includes 6.4-d, 6.4-e, 6.4-h, 6.4-i, and 6.8-b. Those rows are support, utility, or passive only.

`OwnershipSpineServices.IsTrustedModifier` then calls `ModifierValidationService.CanApply`, which rejects a category mismatch. A player can be assigned a stamp the client will not trust. If the id is forced onto the move anyway, `SwapModifierResolver.TargetsAlly` no-ops it on a damaging host.

This is not a Unity test. The follow-up should not regenerate the pool in this fix. The combat-side refusal is correct. The acquisition-side fix belongs in a pool cut that filters by host category before the modulo, or in a reroll when `CanApply` fails. Leave the pinned hash alone until that cut is reviewed.

### 9. Afterguard deletes a barrier the Chainling already had

`ChainlingInstance.AddBarrier` clears existing barriers when `exclusiveBarriersAndWalls` is true, and that flag is true. A 1-hit post-cast barrier replaces a wall that was still up.

Contract test: `PostCastBarrier_GrantsOnHit_SkipsAMiss_AndReplacesAnExistingBarrier`.

Recommended rule: if an intact stronger barrier is already up, do not replace it, and do not report a new barrier. Update the test when that is implemented.

## Behavior the tests lock on purpose

These are not bugs. The follow-up should keep them green.

- A miss does not leech. A landed hit does. Two hits proc twice. A two-target spread procs twice.
- Execute is strict half HP. Underdog compares the two current HP totals.
- Punish Miss reads the miss from the previous turn, after `BeginTurn` rolls the ledger.
- Revenge Spark boosts after a teammate faints. It does not boost because a teammate scored a KO. If design wants the knockout reading instead, change `RecordKo` so the scoring team's index is what `DidAllyKoOccurThisTurn` sees, and change the two revenge tests together. The display name Revenge Spark matches the faint reading.
- Crit Wallet requires a crit. Crit Shatter drops melee defense on a melee hit and leaves ranged defense alone.
- Finisher Fee and Momentum fire from the killing move and use the labels `KO AP` and `SPEED +1`.
- Afterguard's 2-turn barrier is still up on the next turn and absorbs that hit.
- Warm Welcome pays the Chainling who swapped in. Cover Ally on a Support move mitigates the partner. The same id on a melee move does not.
- Parting Shot chips the Chainling who swapped.
- Cleanse removes one general debuff. Spotlight resolves when an ally is out. Pre-emptive casts a support host at battle start and does not cast again next turn. A damaging host is refused.
- Tag Team and Pile-On require an earlier action that turn.
- Tripwire's Slow duration and refresh rules stay in `CombatIssuesRemediationV2Tests`. Do not reopen them.

## Not in this sweep

Sticky Mirror, Sparse Ward, Duo Boost's per-partner math, and Setup Feed's consume rules were not given new failing tests. The code paths are present. A later pass can add them if the faults above move.

On-KO and on-escape still dispatch by sniffing the description (`ap`, `speed stage`, `chip`, `maxhp`) instead of the section id. That is brittle. The follow-up can switch those four rows to section ids while touching them, without a new opcode system.

## Tests

File: `Assets/Scripts/Combat/Tests/TriggerConditionalModifierAuditTests.cs`

- `TriggerConditionalModifierContractTests` — current rules that should stay green.
- `TriggerConditionalModifierKnownFaultTests` — the contracts in faults 1–5. They are expected to fail until the resolver changes. Do not weaken them to match today's output.

The script compiled in the open editor (`recompile_status` completed with no errors). The known-fault tests are expected to fail from the code paths above. That expectation was not confirmed in the runner.

The open editor's pipeline stopped accepting commands after a broad EditMode run was cancelled. That run is not a result for this audit. The filtered command to use once the editor is answering again:

`unity command run_tests --mode editor --filter TriggerConditionalModifier --filter_type testName`

Pass `--mode editor`. The default mode is `all`, and a run without the filter walks the whole EditMode suite.

## Second pass (2026-10-21): Modifier narration

Scope: how modifier outcomes reach the player through the narration box, the Battle Log transcript, and modifier popups. It covers every trigger moment the resolver dispatches: on hit, on crit, low HP, the section-dispatched conditionals, swap in and out, on KO, on escape, the 6.8 on-use, battle-start, and reaction rows, and the section 7 synergy rows. It also covers how walls and barriers interact with modifier lines. This pass changes no code. It names the code that produces today's text and gives Codex a phased plan.

Method and data limits:

- Every claim below cites `path:line` in the snapshot. `/workspace/narration/narration_audit_check.py` re-checks these citations and the counts. See "Verification" at the end of this section.
- `Docs/Specs/Combat/Data/Modifiers.csv` was not captured, and the individual modifier `.asset` files were not captured either. `Assets/Data/Combat/Modifiers/Catalog/ModifierCatalog.asset` holds 109 GUID references and no per-modifier fields. Because of that, the per-row trigger column below comes from the resolver branches, the first-pass ledger, and the test fixtures, not from asset values. Display names in the example text are the names this doc already uses (Execute, AP Siphon, and so on). At runtime the text has to come from `ModifierDefinitionSO.GetDisplayName()`.
- Example names follow `CombatNarrationComposerTests`: Validape (actor), Kitsauto (target), Gilarime (ally), Snap Strike (move). The local team uses bare names. Opponents get the `Enemy` / `Wild` / `Entangled` prefixes from `CombatNarrationNaming`.

Two statements in the first pass need a correction for narration. They are still correct for popups:

- "A condition that was not met is logged and never shown." That is true for popups only. In the narration box, a not-met condition shows the same generic sentence as a success (N1).
- "HP, AP, stage, and status events still narrate themselves." HP, stage, and status events do. AP gains do not narrate, and AP steals narrate the wrong Chainling (N3, N4).

### 1. Summary

- Narration prints one fixed sentence for every modifier event: "A combat modifier changed the outcome." It prints that sentence for failed rolls and unmet conditions too, so a stamp that did nothing reads the same as one that fired.
- No modifier line names the stamp or its owner. The payload has no display name, even though the move-select UI already shows one.
- AP Siphon narrates that the attacker lost the AP it actually gained.
- Many trigger effects are silent apart from the generic line: AP gains, mitigation, accuracy, barrier grants, and damage multipliers. Modifier barriers that later block a hit are named with a raw id ("Modifier.6.7 A").
- Wall lines count modifier mitigation as wall absorption.
- Priority and battle-start modifier outcomes fall outside every narration range.
- Order and placement are fixed by page, not by cause. Every modifier line goes to the consequence page, after the damage it explains. Defender reactions (Riposte, Sticky Mirror, escape triggers) read as part of the other side's action, with no owner named.
- The tests lock the Setup Feed sentences, several popup labels, and one reason substring. No test asserts the generic sentence or any modifier narration beyond Setup Feed.

### 2. Findings

Severity: **High** = the player is told something false or cannot tell success from failure. **Medium** = the information is missing or misplaced. **Low** = polish or noise. **Info** = context for the plan.

#### N1 (High): Failed and unmet modifier outcomes narrate as if they changed the outcome

- The composer turns every `ModifierTriggered` event into the generic sentence and never reads `gameplayApplied` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:180`, `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:185`). The payload carries the flag (`Assets/Scripts/Combat/Runtime/Data/ModifierOutcomePayload.cs:33`). The popup path drops `false` (`Assets/Scripts/Combat/Runtime/Presentation/Combat2BattlefieldPresenter.Pacing.cs:339`). Narration does not.
- The default delivery for `ModifierTriggered` is `OutcomeSummary` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs:170`), and the default profile has no overrides (`Assets/Data/Combat/Narration/CombatNarration_Default.asset:56`).
- `LogModifierOutcome` writes an event for failures as well as successes (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5635`, `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5654`). Examples:
  - Not-met conditionals: Execute (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4832`), Underdog (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4844`), Revenge Spark (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4858`), Punish Miss (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4872`), Duo Boost (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4886`), Tag Team (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4900`), Pile-On (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:4913`), Grit Guard (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5401`).
  - Failed rolls: on hit (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5320`), on crit (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5358`), on KO (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6444`), and Pass A rolls such as Riposte, Pre-emptive, Cleanse, and Sticky Mirror (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6173`).
  - Empty results: no ally for Shared Buff (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5937`), a Patch Kit heal of 0 (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5998`), no empty bench for Sparse Ward (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6011`), no ally for Spotlight (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6061`), and a damaging host refused (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6191`).
- What the player sees:
  - "Validape used Snap Strike! … A combat modifier changed the outcome." This appears whether Execute fired or not.
  - AP Siphon rolls a flat 0.2 chance (first pass, fault 1), so on about four hits in five the box claims a change that did not happen.
  - Grit Guard prints the line on every use while the carrier is healthy.
  - A Riposte carrier prints it on every incoming hit whose roll fails.

#### N2 (High): Modifier lines are anonymous, and several modifiers collapse into one line

- The only modifier sentence is the profile's `modifierTemplate` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs:63`, `Assets/Data/Combat/Narration/CombatNarration_Default.asset:47`). It has no tokens.
- `ModifierOutcomePayload` carries `sectionId`, the popup label `displayText` (`Assets/Scripts/Combat/Runtime/Data/ModifierOutcomePayload.cs:29`), the owner, the target, and the move, but no player-facing name.
- The name exists. `ModifierDefinitionSO.displayName` (`Assets/Scripts/Combat/Runtime/ScriptableObjects/ModifierDefinitionSO.cs:18`) and `GetDisplayName()` (`Assets/Scripts/Combat/Runtime/ScriptableObjects/ModifierDefinitionSO.cs:89`) are already used by the move-select card (`Assets/Scripts/Combat/Runtime/UI/UGUI/CombatSelectionCoordinatorUGUI.cs:757`).
- `JoinSentences` drops repeated sentences (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:750`). Two or three modifiers on one action therefore print one generic line.
- What the player sees: one unnamed line per action. In PvP neither player can tell whose stamp acted or which stamp it was.

#### N3 (High): AP Siphon narrates that the thief lost AP

- `CombatApService.Steal` drains the victim and logs `APSpent` on the victim (`Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs:112`). `APSpent` is not narrated. It then logs `APStolen` on the stealer with a positive amount (`Assets/Scripts/Combat/Runtime/Engine/CombatApService.cs:123`).
- The composer narrates `APStolen` as `{target} lost {amount} AP.` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:201`, `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:202`, `Assets/Data/Combat/Narration/CombatNarration_Default.asset:49`). Here `{target}` is the stealer.
- What the player sees: "Validape lost 1 AP." Validape gained the AP. Kitsauto, who lost it, is not mentioned.
- The same bug hits utility moves that steal AP (`Assets/Scripts/Combat/Runtime/Engine/UtilityEffectExecutor.cs:101`). The fix in the plan covers both.

#### N4 (Medium): Many trigger effects are silent apart from the generic line

- AP gains log `APGained`, `APRefunded`, or `APCapped`. None of these is in the `OutcomeSummary` list, so they fall to `PopupOrHudOnly` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs:141`, `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs:196`). The composer skips those before it reaches its switch (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:70`). This affects Crit Wallet, Finisher Fee, Warm Welcome, Hand-Off, and Parting Gift.
- Swap accuracy and mitigation (Spotter, Brace, Cover, Fade Guard) are written only as `SystemMessage` lines (`Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs:294`, `Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs:310`). Those are `PopupOrHudOnly` by default.
- Post-cast barrier grants are a `SystemMessage` too (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6346`).
- Grit Guard, Shield Link, Sparse Ward, and Spotlight add a mitigation or accuracy source and log no effect event at all.
- Damage multipliers change the number on the damage line and add nothing else.
- What the player sees: "A combat modifier changed the outcome." with no hint of what changed, plus a short popup (`+1 AP`, `MITIGATE`, `BARRIER`, `ACCURACY`).

#### N5 (Medium): Barriers granted by modifiers are named with a raw id

- Afterguard, Double Guard, and Threshold Guard tag their barrier `modifier.{section}` (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6321`, `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6339`). The wall-state helper returns that tag as the wall source (`Assets/Scripts/Combat/Runtime/Data/CombatWallStateUtility.cs:75`).
- `ResolveWallName` asks the log's name registry (`Assets/Scripts/Combat/Runtime/Engine/CombatEventLog.cs:147`). The tag is not registered, because only Chainlings and moves are (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:206`). So it title-cases the raw tag (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:522`).
- What the player sees: "Modifier.6.7 A blocked all 14 damage." and, at expiry, "Validape's Modifier.6.7 A faded." (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:505`).

#### N6 (Medium): Wall lines take credit for modifier mitigation

- When a wall is up, `CreateWallInterceptPayload` sets `damageAbsorbed` to `preMitigationDamage - finalHpDamage` (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:7150`). `preMitigationDamage` is taken before all mitigation sources are applied (`Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs:188`). The mitigation loop multiplies every active source together, wall or not (`Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs:471`).
- What the player sees: Ember Wall plus Brace or Shield Link produces "Ember Wall blocked 30 damage." That counts Brace's share as the wall's. Without a wall, modifier mitigation is never mentioned at the moment it reduces a hit.

#### N7 (Medium): Priority and battle-start modifier outcomes are outside every narration range

- Priority modifiers log while the action queue is built (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:847`, `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:7306`). The controller builds the queue (`Assets/Scripts/Combat/Runtime/Presentation/CombatBattleController.cs:1476`) and only then records where the first narration range starts (`Assets/Scripts/Combat/Runtime/Presentation/CombatBattleController.cs:1481`). The action line still says "struck first with priority", so nothing is lost. That is acceptable.
- Battle-start stamps run inside `InitializeBattle` (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:134`, called from `Assets/Scripts/Combat/Runtime/Presentation/CombatBattleController.cs:508`). No range covers them. A Pre-emptive cast, and a Sparse Ward or Cleanse on a Passive host, leave no narration line and no Battle Log entry. Whether their popups play depends on when the presenter first polls the log. That was not verified in this pass.

#### N8 (Medium): Placement is by page, not by cause

- Every modifier sentence goes to the consequence page (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:185`, `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:261`). Wall lines, damage, and heals go to the resolution page (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:237`, `Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:244`).
- So a leech heal ("Validape recovered 5 HP.") and its cause sit on different pages. An unmet condition that was logged before the hit shows after the damage. A damage multiplier is explained one page after the number it changed.
- Popups play while events play back. The resolution and consequence pages appear only after the action's last event (`Assets/Scripts/Combat/Runtime/Presentation/Combat2BattlefieldPresenter.Narration.cs:80`). The player sees `HEAL` / `POWER` popups before any text explains them.
- On-KO stamps run before the KO is logged (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:2563` before `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:2595`). The `KO AP` popup plays before the knockout is shown.

#### N9 (Medium): Reactions from the other side read as part of the actor's move

- Riposte runs inside the attacker's hit loop (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:2529`) and logs under the defender (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6201`). Sticky Mirror does the same (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6111`). Escape stamps run inside the swapper's action (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6718`).
- The payload already records the owner as `sourceChainlingId`, so the information exists. Narration never prints it.
- What the player sees: "Validape used Snap Strike! … Validape became Slowed. A combat modifier changed the outcome." It reads as if Validape's own move slowed Validape. In fact it was Kitsauto's Riposte.

#### N10 (Low): Resolver strings pass through without the registered names

- Parting Shot logs its chip as an `HPDotDamage` message with no target id: "{displayName} took {actual} chip damage from escape trigger." (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6538`). The composer prints such messages as they are (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:101`). The name loses its `Enemy` prefix, and the player reads the phrase "escape trigger".
- A Cipher Lock block on Flex In or Cheer logs "Cipher Lock blocked {displayName}'s {stat} +1." with the raw enum name, for example `MeleeAtk` (`Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs:270`). `BuffBlocked` messages are printed as they are (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:195`).

#### N11 (Low): Repeats are not coalesced, and dedupe hides a real repeat

- Leech logs one outcome per landed hit and per spread target (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5331`). Each applied outcome is a popup that holds `modifierLabelHold` (`Assets/Scripts/Combat/Runtime/Presentation/Combat2BattlefieldPresenter.Pacing.cs:361`). The field default is 0.9 s (`Assets/Scripts/Combat/Runtime/ScriptableObjects/CombatBattlePacingProfileSO.cs:26`); the pacing asset was not captured. A three-hit leech therefore spends about 2.7 s on `HEAL` popups before the first text page.
- The same text dedupe (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:750`) collapses two identical stage lines. Crit Shatter on two crits of one multi-hit lowers Defense by 4 but prints "Kitsauto's Melee Def changed by -2." once.

#### N12 (Low): Unconditional stamps print the generic line on nearly every attack

- `EmitDamageContextModifierOutcomes` writes an outcome once per move, for every applied context modifier, after the first landed hit (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5243`, `Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5268`). This includes always-on POT, AUG, and TRX rows.
- These outcomes are correct as popups. As narration they add a generic line to most stamped attacks and bury the trigger rows that matter.

#### N13 (Low): Four trigger enum values have no runtime reader

- `ModifierTriggerCondition` has 15 values (`Assets/Scripts/Combat/Runtime/Data/MoveEnums.cs:106`). `OnMiss`, `EndOfTurn`, `OnBattleStart`, and `OnIncomingHit` are not referenced anywhere under `Assets/Scripts/Combat/Runtime`. The battle-start and incoming-hit rows dispatch by section id instead.
- If a catalog asset sets one of these values, that stamp neither fires nor narrates. This cannot be confirmed here because the asset files are absent. Treat it as a check to run in the editor, not as a fault.

#### N14 (Low): Popup labels and narration copy drift

- Modifier barriers pop `BARRIER` for any hit count. Tiered walls use `FormatBlockHits` (`BLOCK`, `BLOCK · 2H`; `Assets/Scripts/Combat/Runtime/Presentation/CombatWallPopupLabelUtility.cs:95`). Double Guard's hit count is never shown.
- `Combat_Narration_And_Battle_Report.md` lists 60 characters per second and a 0.55 s consequence hold (`Docs/Combat/Combat_Narration_And_Battle_Report.md:33`, `Docs/Combat/Combat_Narration_And_Battle_Report.md:36`). The default asset uses 45 and 1.45 s, with a 1.15 s skip lockout (`Assets/Data/Combat/Narration/CombatNarration_Default.asset:17`, `Assets/Data/Combat/Narration/CombatNarration_Default.asset:20`, `Assets/Data/Combat/Narration/CombatNarration_Default.asset:21`). Pacing proposals below use the asset values.

#### N15 (Info): Rich text is technically possible, but no style uses it yet

- The box body is TextMeshPro. The typewriter counts `textInfo.characterCount` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/DialogueBoxView.cs:304`), so inline tags would not break the reveal.
- Transcript entries store the page text as is (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:776`). The Battle Log and replay would carry any tags.
- The only colored narration today, the battle intro, paints vertex spans after layout and does not use inline tags (`Assets/Scripts/Combat/Runtime/Presentation/Narration/BattleIntroCopySpanApplier.cs:13`).

#### N16 (Info): What the tests lock today

Keep these green, or change them deliberately:

- `ComposeAction_SetupFeedStatesTheExactPowerBonus` and `ComposeAction_SetupFeedFadeStatesTheLostBonus` assert the whole consequence page text (`Assets/Scripts/Combat/Tests/CombatNarrationComposerTests.cs:430`, `Assets/Scripts/Combat/Tests/CombatNarrationComposerTests.cs:460`). Setup Feed's fade is a `gameplayApplied == false` outcome that must still narrate. The N1 fix needs an explicit exception for it.
- `AllyFirstDamagingMoveEnablesComboPowerPayload` locks Tag Team's popup label as `SYNC` (`Assets/Scripts/Combat/Tests/CombatModifierResolverAndPopupTests.cs:235`). The first-pass known-fault test `Execute_PopupNamesTheConditional` wants `EXECUTE` (`Assets/Scripts/Combat/Tests/TriggerConditionalModifierAuditTests.cs:624`). First-pass fault 3 also suggests `TAG`, which would conflict with the `SYNC` lock. Narration should not depend on popup labels (see R2).
- `CombatDecisionRoundTests` asserts that a modifier event message contains `+1 AP` (`Assets/Scripts/Combat/Tests/CombatDecisionRoundTests.cs:329`). The message is built from `reason` (`Assets/Scripts/Combat/Runtime/Data/ModifierOutcomePayload.cs:107`). Do not reword resolver `reason` strings. Add new fields instead.
- The shared test helper sets `triggerChance = 1f` and no `displayName` (`Assets/Scripts/Combat/Tests/CombatModifierResolverAndPopupTests.cs:388`). New name tests must set `displayName` themselves.
- No test asserts the generic sentence. Nothing locks it.

### 3. Per-trigger table

"Today" is what the box shows with the default profile. The page is shown as **R** (resolution) or **C** (consequence). "Generic" means "A combat modifier changed the outcome." Proposed text follows the rules in section 4. All of it is **Proposal**.

| Row | Trigger as dispatched | Today | Issue | Proposed narration |
|---|---|---|---|---|
| 6.1-a, 6.1-d, 6.1-e, 6.1-f Leech family | `OnHit`, per landed hit | R: "Validape recovered 5 HP." C: Generic. At full HP: Generic with no heal. Roll failed: Generic. | N1, N2, N8, N11 | R, replacing the heal line: "Validape's Leech restored 9 HP." (summed over hits). Nothing at full HP or on a failed roll. |
| 6.1-b AP Siphon | `OnHit`, `triggerChance` roll | C: "Validape lost 1 AP." Generic. Roll failed (most hits): Generic. | N3 wrong subject, N1 | C: "Validape's AP Siphon drained 1 AP from Kitsauto." Nothing on a failed roll. |
| 6.1-c Suppress | `OnHit`, `triggerChance` roll | C: "Kitsauto became Staggered." Generic. | N2 | C, replacing the status line: "Validape's Suppress made Kitsauto Staggered." |
| 6.7-a Afterguard, 6.7-b Double Guard | `OnHit` post-cast barrier | C: Generic (the grant is a hidden `SystemMessage`). Later R: "Modifier.6.7 A blocked all 14 damage." and "Validape's Modifier.6.7 A faded." | N4, N5, N14 | C: "Validape's Afterguard raised a barrier. Blocks 1 hit." (Double Guard: "Blocks 2 hits."). Later: "Afterguard blocked all 14 damage." / "Validape's Afterguard faded." |
| 6.7-c Threshold Guard | `OnHit` post-cast barrier | Same as above | N4, N5 | C: "Validape's Threshold Guard raised a barrier. Absorbs up to 15 damage." |
| 6.2-a Crit Wallet | `OnCrit` | C: Generic (the AP gain is silent) | N4 | C: "Validape's Crit Wallet granted 1 AP." |
| 6.2-b Crit Shatter | `OnCrit` | C: "Kitsauto's Melee Def changed by -2." Generic. Same text at the stage floor. | N2, N11 | C, replacing the stage line: "Validape's Crit Shatter lowered Kitsauto's Melee Def by 2." Nothing at the floor once first-pass fault 2 is fixed. |
| 6.2-c Crit Feed | `OnCrit` | C: "Validape became Surging." Generic. | N2 | C: "Validape's Crit Feed made Validape Surging." |
| 6.3-c Grit Guard | `OnLowHP` at the carrier's action | Met: C Generic. Not met (every healthy use): C Generic. | N1, N4 | Met: C "Validape's Grit Guard put up a guard for the rest of the turn." Not met: Battle Log only, "Grit Guard did not trigger: Validape was not below 30% HP." |
| 6.3-a Execute | Section dispatch in `BuildDamageModifierContext` | Met: C Generic, after damage. Not met: C Generic, logged before the hit. The two are identical. | N1, N2, N8 | R prefix: "Execute! Kitsauto took 42 damage." Not met: Battle Log only, "Execute did not trigger: Kitsauto was not below half HP." |
| 6.3-b Underdog | Section dispatch | Same as Execute | N1, N2, N8 | "Underdog! Kitsauto took 38 damage." Not met: "Underdog did not trigger: Validape did not have less HP than Kitsauto." |
| 6.3-d Revenge Spark | Section dispatch (`OnAllyKO` passes the filter) | Same as Execute | N1, N2, N8 | "Revenge Spark! Kitsauto took 40 damage." Not met: "Revenge Spark did not trigger: no teammate was knocked out this turn." |
| 6.3-e Punish Miss | Section dispatch | Same as Execute | N1, N2, N8 | "Punish Miss! Kitsauto took 35 damage." Not met: "Punish Miss did not trigger: Kitsauto did not miss last turn." |
| 7.1-a Duo Boost | Section dispatch | Same as Execute | N1, N2, N8 | "Duo Boost! Kitsauto took 33 damage." Not met: "Duo Boost did not trigger: no partner had a buff." |
| 7.2-a Tag Team | Section dispatch | Same as Execute | N1, N2, N8 | "Tag Team! Kitsauto took 36 damage." Not met: "Tag Team did not trigger: no ally attacked earlier this turn." |
| 7.2-b Pile-On | Section dispatch | Same as Execute | N1, N2, N8 | "Pile-On! Kitsauto took 44 damage." Not met: "Pile-On did not trigger: Kitsauto was not hit earlier this turn." |
| 7.2-c Setup Feed | Armed after a power move, fades on a non-power move | C: "Setup Feed triggered. The next ally move gains 12% power." / "Setup Feed faded. …" | None. This is the model. | Keep both sentences exactly (tests lock them). |
| 7.1-b Shield Link | Support move on use | C: Generic | N4 | C: "Validape's Shield Link guarded Gilarime for the rest of the turn." |
| 7.1-c Hype Pass | Support move on use | C: "Gilarime's Speed changed by 1." Generic. | N2 | C, replacing the stage line: "Validape's Hype Pass raised Gilarime's Speed by 1." |
| 7.1-d Mind-Meld, 7.3-a Shared Buff | On use, copies self-buffs | C: the copied status and stage lines for Gilarime, then Generic. No ally: Generic. | N1, N2 | C, placed before the copied lines: "Validape's Shared Buff shared the buffs with Gilarime." The copied lines stay. No ally: nothing. |
| 6.4-a Brace, 6.4-d Cover, 6.4-g Fade Guard | `OnSwapIn` / `OnSwapOut` mitigation | C: Generic (the mitigation message is hidden) | N4 | C: "Gilarime's Brace put up a guard for the rest of the turn." / "Validape's Cover guarded Gilarime for the rest of the turn." |
| 6.4-b Warm Welcome, 6.4-e Hand-Off, 6.4-f Parting Gift | Swap AP gain | C: Generic (the AP gain is silent) | N4 | C: "Gilarime's Warm Welcome granted 1 AP." / "Validape's Hand-Off granted Gilarime 1 AP." |
| 6.4-c Flex In, 6.4-i Cheer | Swap random +1 stage | C: "Gilarime's Melee Atk changed by 1." Generic. Under Cipher Lock: "Cipher Lock blocked Gilarime's MeleeAtk +1." | N2, N10 | C: "Gilarime's Flex In raised Gilarime's Melee Atk by 1." Under Cipher Lock: "Cipher Lock blocked Gilarime's Melee Atk +1." (registered name, readable stat). |
| 6.4-h Spotter | Swap accuracy | C: Generic (the accuracy message is hidden) | N4 | C: "Validape's Spotter sharpened Gilarime's aim for the rest of the turn." |
| 6.5-a Finisher Fee | `OnKO` | C: Generic after the KO line. The popup plays before the KO. At full AP: nothing. | N4, N8 | C: "Validape's Finisher Fee granted 1 AP." |
| 6.5-b Momentum | `OnKO` | C: "Validape's Speed changed by 1." Generic. | N2 | C, replacing the stage line: "Validape's Momentum raised Validape's Speed by 1." |
| 6.6-a Tripwire | `OnEscape`, scans the opponent's moves | In the swapper's action, C: "Validape became Slowed." Generic. | N2, N9 | C: "Enemy Kitsauto's Tripwire made Validape Slowed." |
| 6.6-b Parting Shot | `OnEscape` | R: "Validape took 6 chip damage from escape trigger." C: Generic. | N9, N10 | R, in place: "Enemy Kitsauto's Parting Shot dealt 6 damage to Validape." |
| 6.8-a Patch Kit | On use | R: "Validape recovered 6 HP." C: Generic. A heal of 0: Generic. | N1, N2 | R, in place: "Validape's Patch Kit restored 6 HP." A heal of 0: nothing. |
| 6.8-b Pre-emptive | Battle start (section dispatch, not `OnBattleStart`) | No narration and no Battle Log entry | N7 | Battle-start page: "Validape's Pre-emptive cast <host move>!" followed by that move's effect lines. |
| 6.8-c Riposte | Defender, on a hit that got through | Inside the attacker's action: the cast's effect lines, then Generic. A failed roll on every hit: Generic. | N1, N9 | C: "Kitsauto's Riposte cast <host move>!" before the cast's effect lines. Nothing on a failed roll. |
| 6.8-d Sticky Mirror | Defender, on an incoming debuff; also boosts reflect | C: Generic. The reflect boost is silent. | N2, N9 | C: "Kitsauto's Sticky Mirror reflected the effect back to Validape." The reflect boost stays inside the existing reflect line. |
| 6.8-e Sparse Ward | On use; also battle start on a Passive host | On use, C: Generic. Battle start: nothing. No empty slots: Generic. | N1, N4, N7 | C: "Validape's Sparse Ward put up a guard for the rest of the turn." No empty slots: nothing. |
| 6.8-f Cleanse | On use; also battle start on a Passive host | C: "Validape was cleansed of Corroded." Generic. Nothing to clear, or roll failed: Generic. | N1, N2 | C, replacing the cleanse line: "Validape's Cleanse cleansed Validape of Corroded." Otherwise nothing. |
| 6.8-g Spotlight | On use | C: Generic. No ally: Generic. | N1, N4 | C: "Validape's Spotlight sharpened Gilarime's aim." No ally: nothing. |
| Non-trigger rows (POT 2.x, AUG 4.x, TRX 5.x, DUR 8.x, EFF 3.x, INIT 1.x and 4.3) | Context modifiers, refunds, priority | Generic on most stamped attacks and refunds. Priority: no line (the action line says priority). | N12 | Popup only (Quiet tier). Two exceptions. A wall bypass while a wall was up prefixes the damage line: "Ignore Walls! Kitsauto took 30 damage." A follow-up action (3.2-a, 3.3-a) becomes "Validape's <modifier> granted another action." in place of "Validape gained another action." |

Count check: this table covers all 45 trigger and synergy section ids from 6.1-a to 7.3-a, the same set as the stamp pool in `MoveModifierGenerator` plus 6.5, 6.6, 6.8, and 7.3-a.

### 4. Proposed narration rules

Every rule here is a **Proposal**. They use only the composer, payload, profile, and name registry that already exist. The only new data is two optional payload fields (`modifierName`, `amount`) and a name registration for modifier barrier tags.

- **R1 (Proposal): only applied outcomes speak live.**
  - Narrate a `ModifierTriggered` event live only when `gameplayApplied` is true.
  - One exception: Setup Feed's fade stays live. Something the player armed was lost, and the existing tests lock that sentence.
  - Chance failures, refusals (damaging host, non-Support host), and "nothing to do" results are never narrated anywhere.
  - Unmet conditions on the acting move (6.3-a, 6.3-b, 6.3-c, 6.3-d, 6.3-e, 7.1-a, 7.2-a, 7.2-b) go to the Battle Log only, as "<Modifier> did not trigger: <reason>." See Q1.
- **R2 (Proposal): attribute the owner and the stamp, never the id.**
  - The subject is "{owner}'s {modifier}". `{owner}` is the registered name of `payload.sourceChainlingId`, which already includes the Enemy / Wild / Entangled prefix. `{modifier}` is `payload.modifierName`, filled from `ModifierDefinitionSO.GetDisplayName()`.
  - If the name is empty or equals the section id, fall back to "{owner}'s {move} stamp" ("Validape's Snap Strike stamp restored 5 HP."). With no move either, use "{owner}'s stamp".
  - Popup labels (`displayText`) never appear in narration. Narration and popup labels can then change independently.
- **R3 (Proposal): put each line in one of three places, by what the stamp changed.**
  - (a) Damage shapers (6.3-a, 6.3-b, 6.3-d, 6.3-e, 7.1-a, 7.2-a, 7.2-b, plus wall-bypass rows when a wall was up) prefix the damage line of the target they applied to: "Execute! Kitsauto took 42 damage." This follows the existing `Critical hit! ` prefix (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:389`). It stacks in front of it: "Execute! Critical hit! Kitsauto took 42 damage." Use no owner here, because the action line already names the attacker. Use no prefix when that target took no HP damage.
  - (b) Procs whose effect already has a line (heal, AP, stage, status, cleanse, chip damage, bonus action) **fold**. The attributed sentence replaces that effect line in the same page and position. The player then reads one line, not two.
  - (c) Effects with no line of their own (guard, accuracy, barrier grant, cast, reflect, share) add one consequence sentence. Share and cast sentences go before the lines they caused. All others go at the point of the event.
- **R4 (Proposal): use one verb per effect family, in past tense.**
  - Results use past tense. Action lines keep their current present forms.
  - When the effect target is the owner, drop the target where English allows it: "restored 5 HP", "granted 1 AP", "put up a guard". Stage and status lines always name the target.
  - The families key off the existing popup labels and the folded event type:

  | Family | Template (Proposal) |
  |---|---|
  | Heal | "{owner}'s {modifier} restored {amount} HP." / "… restored {amount} HP to {target}." |
  | AP gain | "{owner}'s {modifier} granted {amount} AP." / "… granted {target} {amount} AP." |
  | AP drain | "{owner}'s {modifier} drained {amount} AP from {target}." |
  | Stage | "{owner}'s {modifier} raised / lowered {target}'s {stat} by {amount}." |
  | Status | "{owner}'s {modifier} made {target} {status}." |
  | Cleanse | "{owner}'s {modifier} cleansed {target} of {status}." |
  | Damage | "{owner}'s {modifier} dealt {amount} damage to {target}." |
  | Guard | "{owner}'s {modifier} put up a guard for the rest of the turn." / "… guarded {target} for the rest of the turn." |
  | Accuracy | "{owner}'s {modifier} sharpened {target}'s aim." |
  | Barrier | "{owner}'s {modifier} raised a barrier. Blocks {amount} hit(s)." / "… Absorbs up to {amount} damage." |
  | Cast | "{owner}'s {modifier} cast {move}!" |
  | Reflect | "{owner}'s {modifier} reflected the effect back to {target}." |
  | Share | "{owner}'s {modifier} shared the buffs with {target}." |
  | Bonus action | "{owner}'s {modifier} granted another action." |
  | Fallback | "{owner}'s {modifier} activated." |

  The barrier wording matches the tiered-wall copy ("Blocks 1 hit") in `WALLS_BARRIERS_PVP_REDESIGN_V2.md`.
- **R5 (Proposal): coalesce within one action.**
  - Merge applied outcomes that share owner, section id, target, and family. Sum the amounts. A three-hit leech prints "restored 9 HP" once.
  - Merging happens before the sentence is built, so the text dedupe never hides a real repeat. Two Crit Shatter drops print "lowered … by 4".
- **R6 (Proposal): verbosity tiers come from `payload.category`, which the resolver already fills.**
  - Narrated: `TRG` and `SYN`.
  - Quiet, popup only: `POT`, `AUG`, `TRX`, `DUR`, `INIT`, `EFF`. Exceptions: wall-bypass rows when a wall was up (R3a), and bonus-action rows, which fold into the existing bonus-action line.
  - Silent everywhere: failed rolls and refusals (R1).
- **R7 (Proposal): reactions always name the owner.**
  - Riposte, Sticky Mirror, Tripwire, and Parting Shot lines always carry the owner, even inside the other side's action. The owner is `sourceChainlingId`, which is already correct in every call site listed in N9.
  - Their lines follow the actor's own lines on the same page. That matches event order, so no reordering is needed.
- **R8 (Proposal): walls and modifiers stay separate.**
  - Wall lines come first, as today (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:237`).
  - A wall line counts only the wall's share of the absorbed damage (N6).
  - Modifier barriers are named by their modifier (N5).
  - A fully blocked hit shows the wall line and no damage-shaper prefix. Fold or add proc lines only for procs that actually ran on that hit; today none do (first-pass fault 6).
  - A bypass names the bypassing stamp only when a wall was up, because `WallInterceptMode.Bypass` prints no wall line today (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:412`).
- **R9 (Proposal): pacing.**
  - Folding adds no pages.
  - Modifier lines are capped at 3 per action on the live consequence page. The rest go to the Battle Log only (Q5).
  - In `HandleModifierTriggered`, only the first popup per (section id, anchor) in an action holds `modifierLabelHold`. Repeats spawn with no hold.
  - Battle-start outcomes get one consequence page before the first selection (Q6).
- **R10 (Proposal): plain text in this pass.**
  - Add no inline color tags. The transcript and Battle Log reuse the text as is (N15).
  - If color is wanted later, add a modifier-name span the way `BattleIntroCopySpanApplier` paints intro spans. Leave that out of this work (Q7).
- **R11 (Proposal): expirations.**
  - A modifier barrier's expiry already narrates through `WallExpired`. Once named, it reads "Validape's Afterguard faded."
  - Duration-1 guards and accuracy grants end with the turn and stay silent.
  - Status expiries stay Battle Log only, as today.
  - Setup Feed's fade stays live.

### 5. Implementation plan for Codex

Work in order. Each phase compiles and passes on its own. Do not change combat outcomes. The first-pass faults 1 to 9 are separate work. Where a rule depends on one of them, it says so.

#### Phase 1: Stop false and wrong lines (composer only, plus one name registration)

1. `CombatNarrationComposer.ComposeAction`, case `ModifierTriggered` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:180`):
   - Parse the payload with `ModifierOutcomePayload.TryFromEvent`.
   - If `TrySetupFeedSentence` (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:723`) succeeds, keep today's behavior.
   - Else, if the payload is missing, or `gameplayApplied` is false, add nothing.
   - Else, if `payload.category` is Quiet (R6), add nothing.
   - Else, use the existing generic template for now. Phase 2 replaces it.
2. `APStolen`, same method (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationComposer.cs:201`):
   - Pair the event with the nearest earlier `APSpent` in the range whose `apMutationSource` is `CombatApMutationSource.Drain`. Read that event before the `PopupOrHudOnly` skip at line 70.
   - Format with a new profile field `apStealTemplate = "{actor} stole {amount} AP from {target}."`. Here `{actor}` is the stealer (`APStolen.targetId`) and `{target}` is the victim (`APSpent.targetId`).
   - Keep the field `apStolenTemplate` for an `APStolen` event with no pair, but change its default and the asset value (`Assets/Data/Combat/Narration/CombatNarration_Default.asset:49`) to `{target} stole {amount} AP.`
   - Do not change `CombatApService`. The popups rely on the current event subject.
3. `CombatResolver.ApplyPostCastBarrierModifier`: after `AddBarrier`, call `_log.RegisterName($"modifier.{section}", name)`. `name` is the definition's `GetDisplayName()` when it differs from the id, else `"Barrier"`. `ResolveWallName` then prints the name without any composer change.

Phase 1 acceptance:

- A not-met or failed modifier outcome adds no live sentence and no transcript sentence. Phase 2 adds Battle Log reasons for the eight conditionals. Setup Feed's fade is unchanged.
- An applied POT, AUG, TRX, DUR, INIT, or EFF outcome adds no sentence. Its popup is unchanged.
- An AP steal narrates the thief and the victim correctly.
- A barrier from 6.7-a blocks with "Afterguard blocked all N damage." and never prints "Modifier.6.7".
- All 19 existing `CombatNarrationComposerTests` stay green, along with `CombatModifierResolverAndPopupTests`, `TriggerConditionalModifierContractTests`, and `CombatDecisionRoundTests`.

#### Phase 2: Attribution, folding, prefixes, coalescing

1. `ModifierOutcomePayload` (`Assets/Scripts/Combat/Runtime/Data/ModifierOutcomePayload.cs`):
   - Add `public string modifierName;` and `public int amount;`.
   - Add matching optional trailing parameters to `Create` and `CreateEvent`, with defaults `null` and `0`.
   - Keep `CurrentSchemaVersion = 1`. `JsonUtility` leaves missing fields at their defaults, and `TryParse` rejects any other version (`Assets/Scripts/Combat/Runtime/Data/ModifierOutcomePayload.cs:127`), so bumping the version would break old replays.
2. `CombatResolver.LogModifierOutcome` (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:5635`):
   - Fill `modifierName` from `mod.definition?.GetDisplayName()`, or empty when it equals the section id.
   - Add an optional `amount` parameter. Pass the barrier hit count or pool from `ApplyPostCastBarrierModifier`. To do that, return the values or log there.
   - Do not change any `reason` string.
3. `SwapModifierResolver.ApplySwapModifierEffect` (`Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs:320`): fill `modifierName` from `modRef.definition`.
4. `CombatNarrationComposer`:
   - Add a private effect tracker. For each effect event in the range, record the event index, type, target id, and where its sentence went (consequence list index, or heal or damage summary key). Effect events are `HealApplied`, `StageChanged`, `StatusApplied`, `StatusRefreshed`, `StatusCleansed`, `APGained`, `APRefunded`, `APStolen`, `HPDotDamage`, and `BonusActionGranted`. Record AP events before the delivery skip.
   - On an applied, narrated modifier outcome, pick the family from `payload.displayText` and the moment:
     - Prefix family (R3a): remember `{modifier}` for `payload.targetChainlingId`. `FormatDamage` prepends `"{modifier}! "` for each name in event order, before the critical prefix.
     - Fold family (R3b): find the last tracked effect since the previous `ModifierTriggered` whose target matches the payload anchor or target. Replace its sentence with the R4 template, and remove its amount from the heal or damage summary.
     - Add family (R3c): add a sentence from the R4 template. Cast and Share sentences go at the index of the first effect they caused.
   - Coalesce by (owner, section id, target, family) before the sentences are built (R5).
   - Move the R4 templates into `CombatNarrationProfileSO` as serialized strings, with the defaults above, under a new "Modifier Templates" header. Add them to the default asset.
   - Keep the existing `modifierTemplate` field as the R4 fallback. Change its default (`Assets/Scripts/Combat/Runtime/Presentation/Narration/CombatNarrationProfileSO.cs:63`) and its asset value (`Assets/Data/Combat/Narration/CombatNarration_Default.asset:47`) to `{owner}'s {modifier} activated.`. Do not rename the field.
5. `CombatResolver.ProcessOnEscapeModifiers` chip (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:6538`): log the chip with `sourceId` = owner, `targetId` = swapper, and `intValue` = the actual damage, so the composer can fold it. Keep the message for the raw log.
6. `SwapModifierResolver` Cipher Lock line (`Assets/Scripts/Combat/Runtime/Engine/SwapModifierResolver.cs:270`): use `log.Name(target.instanceId)` and `CombatLogTypeNames.GetStatName(stat)`.
7. Unmet-condition Battle Log lines (R1, Q1): in the composer, map the eight conditional section ids to the reason sentences in the table. Add them to `transcriptOnlySentences`, never to live pages. Use the payload's `modifierName`, the registered names, and the fallback from R2.

Phase 2 acceptance:

- No narration line contains a section id, "A combat modifier changed the outcome.", "escape trigger", or a raw `CoreStat` name.
- Each row in the section 3 table produces its proposed text in a composer test or a resolver round-trip test.
- A three-hit leech prints one heal line with the summed amount, on the resolution page.
- Execute met prefixes only the target it applied to. Execute not met adds only a Battle Log line.
- Riposte, Sticky Mirror, Tripwire, and Parting Shot lines name their owner with the registered prefix.

#### Phase 3: Range, walls, pacing

1. Battle start: in `CombatBattleController.StartBattle`, record the log count before and after `_resolver.InitializeBattle` (`Assets/Scripts/Combat/Runtime/Presentation/CombatBattleController.cs:508`). After the intro, call `ComposeAndQueueNarration(before, after, 0, -1)`. With R1 and R6 in place, only applied battle-start outcomes produce text. The `BattleStart` message itself is `PopupOrHudOnly`.
2. Wall credit (N6):
   - Add `wallMitigation` to `DamageComputationTrace`. That is the product of only the sources with `blockKind != None`, or with a wall or barrier tag, computed in the same loop (`Assets/Scripts/Combat/Runtime/Engine/DamageCalculator.cs:471`).
   - In `CreateWallInterceptPayload` (`Assets/Scripts/Combat/Runtime/Engine/CombatResolver.cs:7150`), credit only `preMitigationDamage × wallMitigation` to the wall.
   - Keep the full-block path as it is.
3. Popup holds: in `HandleModifierTriggered` (`Assets/Scripts/Combat/Runtime/Presentation/Combat2BattlefieldPresenter.Pacing.cs:332`), skip the hold for a repeat (section id, anchor) inside the active narration plan's event range. Still spawn the popup.
4. Live cap: in the composer, count modifier-originated consequence sentences. Past 3 (Q5), move the rest to `transcriptOnlySentences`.
5. Optional: show `FormatBlockHits(hits)` for 6.7-a and 6.7-b popups in place of `BARRIER` (N14). Leave this out if Cronus prefers one label per modifier family.

Phase 3 acceptance:

- A Pre-emptive cast at battle start appears once, on a battle-start page and in the Battle Log, and not again on turn 2.
- With Ember Wall and Brace both up, the wall line's amount equals the wall's own share.
- A three-hit leech holds its popup once.

#### New and updated EditMode tests

In `Assets/Scripts/Combat/Tests/CombatNarrationComposerTests.cs`. Build events with `ModifierOutcomePayload.CreateEvent` and set `modifierName` where noted.

| Test | Asserts |
|---|---|
| `ComposeAction_SkipsModifierOutcomeThatWasNotApplied` | A 6.3-a outcome with `gameplayApplied = false` after a damage line leaves `consequencePage` null. No page contains "modifier". |
| `ComposeAction_QuietTierModifierAddsNoLine` | An applied 2.1-a outcome (`POT`) adds no sentence. The resolution text equals the damage line. |
| `ComposeAction_ApStealNamesThiefAndVictim` | `APSpent` (victim, −1, Drain) then `APStolen` (thief, +1) gives "Validape stole 1 AP from Kitsauto." and never "Validape lost". |
| `ComposeAction_ModifierBarrierUsesRegisteredName` | With `RegisterName("modifier.6.7-a", "Afterguard")`, a `BarrierBlocked` with that tag gives "Afterguard blocked all 14 damage." Without the registration, the text does not contain "Modifier.6.7". |
| `ComposeAction_FoldsLeechHealIntoAttributedLine` | Damage, `HealApplied` (5), then an applied 6.1-a "Leech" gives a resolution page containing "Validape's Leech restored 5 HP." and not "Validape recovered 5 HP." |
| `ComposeAction_CoalescesMultiHitLeech` | Two hits with heals of 3 and 4, each with an applied 6.1-a, give one "restored 7 HP" line. |
| `ComposeAction_ApSiphonFoldsIntoDrainLine` | The steal pair plus an applied 6.1-b "AP Siphon" gives "Validape's AP Siphon drained 1 AP from Kitsauto." once. |
| `ComposeAction_StageFoldStatesDirectionAndSize` | `StageChanged` (−2 Melee Def) plus an applied 6.2-b "Crit Shatter" gives "Validape's Crit Shatter lowered Kitsauto's Melee Def by 2." Two such pairs give "by 4". |
| `ComposeAction_PrefixesOnlyTheTargetTheConditionalHit` | A spread onto target and target2 with an applied 6.3-a "Execute" whose target is "target" gives "Execute! Kitsauto took …" and no prefix on Gilarime's line. |
| `ComposeAction_ConditionalPrefixStacksBeforeCritical` | A critical hit plus an applied 6.3-a gives a resolution line starting "Execute! Critical hit! ". |
| `ComposeAction_NotMetConditionalGoesToTranscriptOnly` | A not-applied 6.3-a gives no live page text and one System transcript entry: "Execute did not trigger: Kitsauto was not below half HP." |
| `ComposeAction_OpponentReactionNamesOwner` | An applied 6.8-c whose source is "target" gives "Kitsauto's Riposte cast …" ahead of the cast's status line. |
| `ComposeAction_ModifierNameFallbackNeverShowsSectionId` | An applied 6.1-c with an empty `modifierName` and move "move" gives text containing "Validape's Snap Strike stamp" and not containing "6.1-c". |
| `ComposeAction_ModifierLinesRespectLiveCap` | Five applied TRG outcomes put 3 modifier sentences on the live page and the other 2 in System transcript entries. |
| Keep `ComposeAction_SetupFeedStatesTheExactPowerBonus` and `ComposeAction_SetupFeedFadeStatesTheLostBonus` unchanged | Setup Feed text stays exact, including the fade's `gameplayApplied = false`. |

In a new `Assets/Scripts/Combat/Tests/CombatModifierNarrationTests.cs`, derived from `CombatModifierResolverTestBase`. These run the resolver, then `CombatNarrationComposer.ComposeAction` over the action's event range:

| Test | Asserts |
|---|---|
| `ModifierOutcome_CarriesDefinitionDisplayName` | A definition with `displayName = "AP Siphon"` gives `payload.modifierName == "AP Siphon"` on the applied event. |
| `ModifierOutcome_LeavesNameEmptyWhenDefinitionHasNone` | The helper definition (no `displayName`) gives an empty `modifierName`. |
| `ModifierOutcomePayload_RoundTripsNameAndAmount_AndParsesOldJson` | The new fields round-trip. JSON without them still parses with schema 1. |
| `PostCastBarrier_RegistersDisplayNameForWallNarration` | After Afterguard grants, `resolver.Log.Name("modifier.6.7-a") == "Afterguard"`. |
| `Execute_NotMet_NarratesNoLiveModifierLine` | A healthy target gives no "Execute!" and no generic sentence in the action's pages. |
| `Execute_Met_PrefixesTheDamageLine` | A target under half HP gives a resolution page containing "Execute! ". |
| `PartingShot_NarratesWithOwnerAndRegisteredNames` | The swapper's action contains "Enemy … Parting Shot dealt" and not "escape trigger". |
| `CipherLockSwapBlock_UsesRegisteredNameAndReadableStat` | The `BuffBlocked` text has no raw `CoreStat` token such as `MeleeAtk`. |
| `PreEmptive_BattleStartOutcomeIsNarratedOnce` (Phase 3) | Composing the battle-start range yields one "Pre-emptive cast" line. Turn 2's ranges yield none. |
| `WallCredit_ExcludesModifierMitigation` (Phase 3) | With a wall plus Brace, the payload's `damageAbsorbed` equals the wall's own share. |

Run filters, editor mode, the same way as the first pass:

`unity command run_tests --mode editor --filter CombatNarrationComposerTests --filter_type testName`

`unity command run_tests --mode editor --filter CombatModifierNarration --filter_type testName`

### 6. Open questions for Cronus

Each question has a recommended default. Codex should use the default unless Cronus says otherwise.

- **Q1. Should players see why a conditional did not fire?**
  - Default: yes, in the Battle Log only ("Execute did not trigger: Kitsauto was not below half HP.").
  - Never in the live box.
  - Never for chance rolls or refusals.
- **Q2. Should unconditional stamps (POT, AUG, TRX, DUR, INIT, EFF) ever narrate?**
  - Default: no. Popup only.
  - Exceptions: a wall bypass while a wall was up, and attribution of follow-up actions.
- **Q3. How should a damage shaper show: as a prefix or as its own sentence?**
  - Default: a prefix, "Execute! Kitsauto took 42 damage.", matching "Critical hit!".
  - The alternative is a consequence sentence such as "Validape's Execute added bonus damage."
- **Q4. Should guard and accuracy lines show numbers?**
  - Default: no numbers in this pass. The popup and the move card carry the value.
  - Barrier lines do show hits or pool, to match "Blocks 1 hit".
- **Q5. How many modifier sentences can one action show live?**
  - Default: 3.
  - The rest go to the Battle Log only.
- **Q6. Should battle-start stamps get a narration page?**
  - Default: yes. Show one page after the intro and before the first move selection, with applied outcomes only.
- **Q7. Should modifier names be colored in the box?**
  - Default: not in this pass.
  - If wanted later, use span painting like the battle intro, not inline tags, so the Battle Log stays clean.
- **Q8. What popup label should Tag Team and the other conditionals use?**
  - Default: decide this with first-pass fault 3, and update `AllyFirstDamagingMoveEnablesComboPowerPayload` in the same change if `SYNC` becomes `TAG`.
  - Narration uses names, so this choice does not block the narration work.
- **Q9. How should the local player's own Chainlings be named in modifier lines?**
  - Default: keep the current rule (bare names for the local team, prefixes for opponents) and add no "Your".

### Verification

`/workspace/narration/narration_audit_check.py` checks the claims in this section against the snapshot:

- Every `path:line` citation points at a line that contains the expected code or text.
- The cited event types, delivery rules, and template strings exist.
- The catalog holds 109 references and no trigger fields.
- `ModifierTriggerCondition` has 15 values, 4 of which have no runtime reader.
- All 45 trigger and synergy ids appear in runtime code and in the table above.
- The cited test names exist, and the proposed test names do not exist yet.
- The original audit text above this section is unchanged, apart from the one-line pointer at the top.
- No other snapshot file changed.

Last run: 250 of 250 checks passed.
