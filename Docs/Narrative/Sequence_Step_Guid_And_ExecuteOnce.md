# Sequence steps: Execute Once and Step GUID

## Duplicate-and-rename in the Project window

`SequenceStep` assets store a **hidden serialized `stepGuid`** used when **Execute Once** is enabled. `SequenceRunner` skips a step if that GUID is already listed in `SequenceState.completedStepGuids`.

If you **duplicate** a `.asset` (Ctrl+D) and rename it, Unity copies **all** serialized fields, including `stepGuid`. The new file behaves as the **same** execute-once step: the first time either asset completes, **both** can be skipped on later runs.

### Fix without deleting the asset

1. Select the duplicated step asset in the Project window.
2. In the Inspector, use **Regenerate Step GUID** (added by `SequenceStepGuidEditor`).
3. Save assets.

You do **not** need to delete and recreate the asset if you regenerate the GUID.

### Safer workflow

- Prefer **Regenerate Step GUID** after duplicating any execute-once step.
- Or turn **Execute Once** off on the copy if it should run every time regardless of save state.

## Post-battle “return” dialogue input lock

On **`NarrativeEncounterService`** → encounter registry entry:

- **`postEncounterDialogue`** — dialogue played after battle when returning to the overworld (flow handled in `StartEncounter`).
- **`lockInputDuringPostEncounterDialogue`** — default **on**; passes through to `DialogueService.PlayDialogue` so movement/interaction are locked until the sequence completes.

Configured on the **GameServices** prefab (or any scene instance) under `NarrativeEncounterService` → registry row for your `encounterId`.
