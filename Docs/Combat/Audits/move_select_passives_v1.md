# Move Select Passives Presentation v1

Implemented 2026-10-23 as a UI-only refinement. No combat rules, resolver hashes, move data, backend, or PvP behavior changed.

## Result

- Equipped passives are stable-partitioned above active moves while every entry retains its original equipped `moveIndex`.
- The default cursor selects the first usable active move, or Rest when none is usable.
- Passives remain inspectable but unselectable. Confirming one plays the Invalid feedback exactly once, remains in Move Select, and cannot enter targeting or build an action.
- Passive rows use a distinct gray treatment and a `PAS` pill. Selected rows use dark label text for contrast.
- Passive details keep the name, description, modifiers, and icon readable while muting AP, role, power, accuracy, and targeting. The Details2 library card also mutes passive AP and role headers.

## Navigation

The row order is `[passives][actives][Rest]`.

- Down from Rest wraps to the first active row when an active exists.
- Up from the first active enters the last passive.
- Up from the top passive wraps to Rest.
- With no passives, navigation matches the previous plain wrap behavior.
- With only passives, every passive and Rest remain reachable.

## Serialized presentation defaults

`CombatMoveRowItemUGUI` now exposes:

- `passiveSelectedColor`: `#c7dcd0`
- `passiveNormalColor`: `#9babb2`
- `passiveLabelColor`: `#2e222f`
- `selectedLabelColor`: `#2e222f`
- `useSelectedLabelColor`: `true`
- `priorityPillSelectedTextColor`: `#2e222f`
- `passivePillBackgroundColor`: `#2e222f`
- `passivePillTextColor`: `#c7dcd0`

The selected PRIOR pill needed its dark-text variant on the bright selected fill. Type icons were left unchanged.

The optional divider was not added. The `PAS` pill already communicates the passive group and avoids adding a non-navigable layout child to scroll/index calculations.

## Prefab verification

The live Editor was used to render the real row prefab in passive, selected passive, and selected active states. The passive fills, dark row labels, `PAS` pill, and selected PRIOR contrast were readable.

`CaptureCommandPicker.prefab` shares `CombatMoveRowItemUGUI`, but its selected fill is dark. It has the sole prefab override `useSelectedLabelColor = false` so its existing light selected label remains readable. `MoveRow.prefab` and `CombatBattleCanvas.prefab` required no overrides; the new serialized defaults apply normally.

The unattended combat harness did not expose a populated Move Select panel for a conventional gameplay screenshot, so verification used the live prefab render plus EditMode behavior tests. There is no separate row pointer/click confirmation path; confirm remains centralized in `CombatSelectionCoordinatorUGUI`.

## Verification

Baseline before the change:

- `CombatMoveRowHeightTests`: 3/3 passed
- `CombatMoveLibraryCardViewTests`: 10/10 passed

Final focused EditMode matrix:

- `CombatMoveSelectPassivesTests`: 15/15 passed
- `CombatMoveRowHeightTests`: 3/3 passed
- `CombatMoveLibraryCardViewTests`: 10/10 passed
- Total: 28/28 passed

The final script recompile completed without errors.
