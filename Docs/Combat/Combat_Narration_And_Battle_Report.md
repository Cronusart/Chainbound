# Combat Narration And Battle Report

## Purpose

Combat narration is the player-readable interpretation of the existing combat event stream. It does not replace resolver rules, `CombatEventLog`, VFX, damage popups, HUD updates, or forensic replay events.

The live box answers two questions for each action:

1. What did this Chainling attempt?
2. What was the final result?

The post-battle report preserves those answers in a turn-grouped Battle Log.

## Ownership

| Owner | Responsibility |
| --- | --- |
| `CombatNarrationProfileSO` | Copy templates, delivery rules, reveal speed, page holds, and advance policy |
| `CombatNarrationComposer` | Converts one action's event range into action, resolution, and consequence pages |
| `CombatBattleController` | Defines action ranges, records transcript entries, and queues plans before presentation events |
| `Combat2BattlefieldPresenter` | Plays declaration, existing event presentation, then resolution pages as one sequence |
| `DialogueBoxView` | Shared dialogue visuals, typewriter reveal, portrait/speaker binding, fade, and continue glyph |
| `CombatNarrationBoxController` | Combat-only input and automatic advance policy; never uses `DialogueService` |
| `CombatReplayData` | Stores the player-readable transcript beside unchanged replay and forensic data |
| `CombatPostBattleReportController` | Banner / Summary+Battle Log / Rewards overlay and explicit Continue gate |

## Default Profile

The scene uses [CombatNarration_Default.asset](../../Assets/Data/Combat/Narration/CombatNarration_Default.asset). Its defaults are:

- Speaker: `BATTLE LINK`
- Advance mode: `AutoWithOptionalAdvance`
- Reveal speed: `60` characters per second
- Action hold: `0.22` seconds
- Primary resolution hold: `0.70` seconds
- Consequence hold: `0.55` seconds

Pressing Confirm while text is revealing completes the current line. Pressing Confirm again advances immediately. Without input, the page advances after its configured hold.

## Delivery Rules

Each `CombatEventType` resolves to one policy:

- `Live`: eligible to create the action declaration.
- `OutcomeSummary`: aggregated into a resolution or consequence page.
- `PopupOrHudOnly`: omitted from narration and transcript.
- `TranscriptOnly`: retained in the Battle Log without adding another live page.
- `Silent`: internal or diagnostic data.

Use profile overrides sparingly. Damage, healing, KOs, swaps, misses, dodges, and barriers should remain readable. Hit checks, targeting internals, and raw stat breakdowns should remain silent or visual-only.

## Pacing

`ExecuteActionStep` produces an event range. The controller queues its narration plan before those events enter the presenter. The presenter then performs:

1. Action declaration.
2. Existing VFX, camera work, popups, sound, and HUD updates.
3. Primary resolution.
4. One optional consequence page.

The presenter is not idle until the pages finish, so the next Chainling cannot act early. The old minimum inter-action delay is used only when narration did not handle the action. End-of-turn status events use the same composition path.

Narration is enabled for `Full` and `InfoOnly` encounter UI modes. It is disabled for `Hidden`.

## Transcript And Report

`CombatReplayData.narrationTranscript` contains stable, globally ordered `CombatNarrationTranscriptEntry` records. Raw `CombatEventLog` and forensic events remain unchanged.

After the final result line and presenter queue complete, the post-battle overlay opens as three Confirm-gated pages:

1. **Banner**: `VICTORY!!!` / `DEFEAT` / `CAPTURE` / `DRAW` / `WITHDRAWN`. Short lockout, then Confirm.
2. **Report**: `Summary` (turns, duration, KOs, and each captured wild Chainling with capture time) and `Battle Log` (transcript grouped by turn). Capture details stay on this page even when the banner was VICTORY.
3. **Rewards**: live XP bars, RAM ticker, loot chips. Defeat shows RAM penalty + KO recover instead of XP. Flee / withdrawn skips this page.

`BattleOutcomePresentation.Resolve(BattleResult)` owns outcome kind. Last-wild capture (`EndedByCapture` with `winnerTeamIndex == -1`) is **CAPTURE**, never DRAW. KO after a prior capture remains **VICTORY** with the capture flag. True resolver draws use `winnerTeamIndex == -2`.

Continue on the last shown page is the wait gate. `NarrativeEncounterService` and `CombatPostBattleRouter` wait for `ICombatPostBattlePresentation`, then persist and return. Battle music hard-cuts at overlay start (`MusicConductor.Stop(0)` via the combat audio bridge) and a non-looping outcome sting plays; the overworld soundtrack resumes on scene return.

If no report component exists, those flows retain their immediate persist behavior. Hidden encounter UI mode still skips the overlay.

## Visual Reuse

The combat controller clones the canonical scene `DialoguePanel` when one is available, then binds it through `DialogueBoxView`. A responsive fallback is built only when the shared visual is unavailable. Narrative dialogue still exposes the same `DialogueUIController` API and now delegates its rendering behavior to the same view.

## Authoring Checklist

1. Edit the profile asset rather than hard-coding move-specific text.
2. Keep templates causal and short; values, KOs, and swaps should be explicit.
3. Use event-policy overrides only when an event is genuinely too noisy or uniquely important.
4. Test 1v1 and 2v2 actions, multi-hit and multi-target moves, misses, barriers, status ticks, swaps, KOs, victory, defeat, last-wild capture, and flee.
5. Verify keyboard, gamepad, and touch advancement at mobile-safe resolutions.
6. Re-author with Tools → Chainbound → Combat → Author Combat Narration + Post-Battle Report UI after overlay layout changes.
