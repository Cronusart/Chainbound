🎯 Objectives

Replicate real combat – ensure the test scene runs the same deterministic engine as the live game, including seed‑based RNG and strict action ordering.

Validate UI interactions – make sure the UI elements (buttons, toggles, panels) enforce game rules (e.g., swap + jam banned) and expose all critical state information (jam used, swap fatigue, data state).

Deliver actionable combat logs – provide a scrollable, filterable log that includes all events (actions, damage math, edges, jams, regen, debuffs) as required in the spec.

Educate the player – include a lightweight “edge indicator” to show whether the active Chainling’s Data State has an advantage or disadvantage versus the opponent, and offer an optional detailed breakdown mode for strategists.

🔧 Core Setup

Scene Initialization

Use the existing CombatEngine prefab (Phase 4) and CombatUIManager as the entry point.

Ensure the scene reads a battle seed and chainling roster; allow the designer to override the seed for reproducibility.

Call combatLogDisplay.Initialize(combatEngine.CombatLog) and combatLogDisplay.RefreshLog() each turn to sync the UI with the log.

Action Buttons & Jam Toggle

Confirm that ActionButtonController treats Signal Jam as an optional flag rather than a separate action.

When a player selects Swap, automatically clear the jam toggle (already in OnSwapClicked).

Disable the jam toggle once the participant’s hasUsedSignalJam flag is true, reflecting the single‑use rule.

Show contextual tooltip text (“Cannot jam while swapping”) on hover.

Stats & Indicators

In ChainlingStatsPanel, continue to update HP, ATK, SPD, Integrity and Data State.

Extend the stats panel to include an edge indicator:

When updating stats, compare chainling.currentDataState vs the opponent’s state. If this chainling has an edge (soft counter) over the opponent, show a green “Advantage” icon/text; if the opponent has an edge over this chainling, show a red “Disadvantage” icon/text. If neither has an edge, show “Neutral.”

This indicator guides players on whether to exploit their state or consider Signal Jam.

Keep the existing Jam Used and Swap Fatigue flags; ensure fatigue is cleared exactly one regen cycle later.

Swap Panel

Keep the SwapPanel minimal; list only eligible Chainlings (alive and not currently active).

When a swap is chosen, pass the index back via actionController.OnSwapSelected.

Indicate off‑field chainlings that are jammed (e.g., greyed‑out state label plus “(jammed)” tag).

📜 Combat Log Enhancements

Structured Logging

Use CombatLog.LogResolutionOrder, LogStateEdge, LogStateSignature, LogJamDetailed, LogSwap, LogDamage, LogRegen, etc., to capture all deterministic events as mandated.

For every entry, include structured details (JSON string) to support deeper inspection (e.g., speed comparison, candidate pool sizes).

Color‑Coding & Filters

Continue to map log types to colors (Action, Damage, Debuff, StateEdge, Regen, Jam, Swap, System).

Add a small filter bar (toggle buttons or checkboxes) allowing players to hide or show categories (e.g., only show Actions and Damage).

Expose a “Detailed mode” toggle:

Off (default) – show one‑line summaries per log entry.

On – expand each entry to reveal the JSON details field (e.g., full damage formula, edge resolution explanation).

Scroll & Export

Auto‑scroll the log to the newest entry each turn.

Provide an “Export Log” button that calls combatEngine.CombatLog.ExportForReplay() and writes to clipboard or a file; this will allow manual replay testing outside the UI.

🧪 Test Scene Guidelines

Configurable Seeds and Roster

Place a small panel at the top for testers to input a seed and select test Chainlings/abilities.

Support random seed generation for varied matches; display the used seed in the UI.

Deterministic Replay

Include a “Replay from Log” option: read a previous log (via CombatLog.ExportForReplay) and reconstruct the battle frame by frame, verifying that the same outcomes occur.

Edge‑Case Scenarios

Add quick‑access buttons to simulate key edge cases from the refinement list:

Attempt to use Signal Jam twice (should be blocked).

Attempt Swap + Jam in the same turn (should be prevented).

Force Speed ties and confirm deterministic tie‑break logging.

Trigger ability cooldown or max‑use violation and verify fallback and logging.

✅ Acceptance Checklist for the Test Scene

Jam enforcement – Jam toggle disabled after use; cannot be toggled when swap is selected.

Swap fatigue display – Integrity regen penalty (-8%) clearly indicated, and cleared after the next regen.

Data state edge indicator – Advantage/Disadvantage/Neutral icons update each turn based on current states.

Battle log – Every turn logs actions, resolution order, damage math, state signatures, edges, jams, debuffs, regen, and system messages.

Detailed mode – Strategists can toggle to see JSON details for each log entry.

Replay capability – Exported logs can be replayed deterministically to reproduce identical outcomes.

UI clarity – Minimal by default but with tooltips and optional filters/details for advanced analysis.

Final Note

While the scene should remain minimal for accessibility, the added edge indicator and detailed log mode will empower competitive players to understand and exploit the deterministic nature of Signal Jam. Follow the guardrails and specification strictly—every combat outcome must remain deterministic and fully reproducible, and the UI must never allow illegal actions (swap + jam).