# Narrative Sequence Authoring Guide

This guide covers how to author sequences using the expanded narrative sequence system.

## Table of Contents

1. [Basic Concepts](#basic-concepts)
2. [Control Flow](#control-flow)
3. [Variables and Conditions](#variables-and-conditions)
4. [Checkpoints and Resume](#checkpoints-and-resume)
5. [Execute-Once Steps](#execute-once-steps)
6. [Failure Handling](#failure-handling)
7. [Sub-Sequences](#sub-sequences)
8. [Parallel Tracks](#parallel-tracks)
9. [Best Practices](#best-practices)

---

## Basic Concepts

### SequenceAsset
A ScriptableObject containing an ordered list of steps. Each step performs a single action.

### Steps
Steps are ScriptableObjects that execute actions. They can:
- Complete immediately (synchronous)
- Wait for async operations (dialogue, triggers, etc.)
- Return success, failure, or running states

### SequenceRunner
Executes sequences step-by-step. Handles:
- Label indexing
- Call stack for sub-sequences
- Failure policies
- Checkpoint management

### SequenceDirector
Game-wide entry point for playing sequences. Provides:
- Play/Stop/Queue operations
- Gate-based "play once" semantics
- Automatic input lock management

---

## Control Flow

### Labels

Labels mark positions in a sequence for jumping:

1. Create a `Step_Label` step
2. Set the label name (must be unique within sequence)
3. Use `Step_JumpToLabel` to jump to it

**Example:**
```
Step_Label: "start_combat"
Step_Log: "Combat begins!"
Step_JumpToLabel: "start_combat"  // Loops back
```

### Conditional Branching

#### If Step

Evaluates a condition and jumps to labels:

1. Create a `SequenceCondition` asset (e.g., `ConditionVariableCompare`)
2. Create a `Step_If` step
3. Assign the condition
4. Set `trueLabel` and optionally `falseLabel`

**Example:**
```
Step_SetVariable: playerLevel = 5
Step_If:
  Condition: playerLevel >= 5
  True Label: "high_level_path"
  False Label: "low_level_path"
```

#### Branch Step

Evaluates multiple conditions in order, jumps to first matching label:

1. Create a `Step_Branch` step
2. Add multiple condition-label pairs
3. Set optional `defaultLabel` for no matches

**Example:**
```
Step_Branch:
  Cases:
    - Condition: quest_completed, Label: "quest_done"
    - Condition: quest_started, Label: "quest_active"
  Default Label: "no_quest"
```

#### Switch Step

Switches on a variable value:

1. Create a `Step_Switch` step
2. Set `variableKey` to switch on
3. Add value-label pairs
4. Set optional `defaultLabel`

**Example:**
```
Step_SetVariable: choice = "option_a"
Step_Switch:
  Variable: choice
  Cases:
    - Value: "option_a", Label: "path_a"
    - Value: "option_b", Label: "path_b"
  Default Label: "invalid_choice"
```

---

## Variables and Conditions

### Typed Variables

Variables support types: `bool`, `int`, `float`, `string`, `Vector3`

**Setting Variables:**
- Use `Step_SetVariable` with type selection
- Choose scope: `Local` (cleared on sequence end) or `Global` (persists)
- Mark as persistent to save across sessions

**Getting Variables:**
- Conditions automatically read typed variables
- Use variable name in conditions

### Conditions

Conditions evaluate to true/false:

**Available Conditions:**
- `ConditionVariableCompare` - Compare variable to value
- `ConditionVariableExists` - Check if variable exists
- `ConditionQuestState` - Check quest state
- `ConditionAnd` - All conditions must be true
- `ConditionOr` - At least one condition must be true
- `ConditionNot` - Negate a condition

**Example Condition Setup:**
1. Create `ConditionVariableCompare` asset
2. Set `variableName` = "playerLevel"
3. Set `comparisonType` = GreaterThan
4. Set `compareValue` = "10"
5. Use in `Step_If` or `Step_Branch`

---

## Checkpoints and Resume

### Setting Checkpoints

Use `Step_SetCheckpoint` to mark a resume point:

1. Create `Step_SetCheckpoint` step
2. Set `checkpointId` or `checkpointLabel`
3. Enable `saveToState` to persist

**Example:**
```
Step_SetCheckpoint:
  Checkpoint ID: "after_intro"
  Checkpoint Label: "intro_complete"
```

### Resuming Sequences

The `SequenceRunner` supports three resume modes:

1. **Resume from Beginning**: `ResumeFromBeginning()`
2. **Resume from Checkpoint**: `ResumeFromCheckpoint()` (uses last checkpoint)
3. **Resume from Label**: `ResumeFromLabel("label_name")`

**Note:** Checkpoints save scene ID and spawn ID automatically when using `Step_LoadScene`.

---

## Execute-Once Steps

Steps can be marked to execute only once per sequence:

1. Select a step in the inspector
2. Enable `Execute Once`
3. Step will be skipped on resume if already completed

**Use Cases:**
- Starting quests (shouldn't restart on resume)
- Committing saves (idempotent operations)
- Playing cutscenes (don't replay on resume)

**Example:**
```
Step_StartQuest: (Execute Once = true)
Step_SetCheckpoint: "after_quest_start"
// On resume, quest won't restart, but sequence continues
```

---

## Failure Handling

### Failure Policies

Each step can have a failure policy:

- **UseRunner**: Use runner's default policy
- **AbortSequence**: Stop entire sequence
- **SkipAndContinue**: Log warning and continue
- **RetryStep**: Retry with configured attempts
- **GotoLabel**: Jump to `onFailLabel`
- **RunFallback**: Run fallback sequence

**Setting Failure Policy:**
1. Select step in inspector
2. Set `Failure Policy`
3. If `GotoLabel`, set `On Fail Label`

**Example:**
```
Step_LoadScene:
  Failure Policy: GotoLabel
  On Fail Label: "scene_load_failed"
```

### Runner Default Policy

Configure on `SequenceRunner`:
- `Default Failure Policy` (default: AbortSequence)
- `Fallback Sequence` (if policy is RunFallback)

---

## Sub-Sequences

### Calling Sub-Sequences

Use `Step_CallSequence` to call another sequence:

1. Create `Step_CallSequence` step
2. Assign `Sub Sequence` asset
3. Optionally add parameter bindings:
   - `Target Variable`: Variable name in called sequence
   - `Source Variable`: Variable name in current context (or literal)
   - `Is Literal`: Treat source as literal value

**Example:**
```
Step_SetVariable: playerName = "Alice"
Step_CallSequence:
  Sub Sequence: DialogueSequence
  Parameter Bindings:
    - Target: "speakerName", Source: "playerName", Is Literal: false
```

### Returning from Sub-Sequences

Use `Step_Return` to return to calling sequence:

1. Create `Step_Return` step
2. Place at end of sub-sequence
3. Execution returns to step after `Step_CallSequence`

**Note:** Sub-sequences have their own local variable scope. Variables are not automatically shared.

---

## Parallel Tracks

Child tracks let one sequence start side work without blocking the main track unless you explicitly wait on it.

### Core Steps

- `Step_RunTrack` starts a child `SequenceAsset` with a stable `trackId`
- `Step_WaitForTrack` waits for a child track to complete or for a narrative signal
- `Step_StopTrack` stops a child track
- `Step_Signal` emits a narrative signal that waiting steps can observe

### Recommended Defaults

For most new child tracks, start with:

- `TrackBlockingMode = NonBlocking`
- `TrackCancelPolicy = StopOnParentEnd`
- `TrackContextMode = Child`
- a slug-style `trackId` such as `ambient_lab_banter`

### Starter Tool

Use `Assets > Create > SignalJam > Narrative > Tracks > Parallel Track Starter` to create:

- a child `SequenceAsset`
- a paired `Step_RunTrack` asset already wired to that child sequence

The starter assigns the recommended defaults above and generates a slug-based `trackId` from the selected asset or folder name.

### Validation

`SequenceAsset` validation now includes child-track authoring issues:

- empty `trackId` on `Step_RunTrack`
- duplicate `trackId` values within one sequence
- `Step_WaitForTrack` references to unknown track ids
- `Step_StopTrack` references to unknown track ids
- warning-only review for blocking child tracks

Use `Tools > Signal Jam > Narrative > Validate Parallel Tracks` for a project-wide report.

---

## Best Practices

### Label Naming

- Use descriptive names: `"combat_start"`, `"dialogue_complete"`
- Use snake_case for consistency
- Document label purpose in step name

### Variable Management

- Use `Local` scope for temporary variables
- Use `Global` scope for cross-sequence state
- Mark variables as persistent only when needed
- Use descriptive variable names

### Checkpoint Placement

- Place checkpoints before major state changes
- Place after idempotent operations (quest start, save commit)
- Don't place checkpoints in loops (unless intentional)

### Error Handling

- Use `GotoLabel` policy for recoverable errors
- Use `SkipAndContinue` for non-critical failures
- Use `AbortSequence` for critical failures
- Always validate step configuration

### Performance

- Avoid deep call stacks (limit sub-sequence nesting)
- Use event-driven waits (`Step_WaitForEvent`) instead of polling
- Mark execute-once steps appropriately
- Clear local variables when sequence completes

### Testing

- Test sequences from beginning
- Test resume from checkpoint
- Test resume from label
- Test failure scenarios
- Validate all label targets exist

---

## Common Patterns

### Dialogue Choice

```
Step_Label: "choice_point"
Step_PlayDialogue: "What do you do?"
Step_Branch:
  Cases:
    - Condition: choice == "fight", Label: "fight_path"
    - Condition: choice == "flee", Label: "flee_path"
```

### Quest Gating

```
Step_If:
  Condition: quest_completed("main_quest")
  True Label: "quest_complete_path"
  False Label: "quest_incomplete_path"
```

### Retry Logic

```
Step_Retry:
  Inner Step: Step_LoadScene
  Max Attempts: 3
  Backoff Seconds: 2
```

### Timeout Handling

```
Step_Timeout:
  Inner Step: Step_WaitForEvent("player_arrived")
  Timeout Seconds: 10
  On Timeout Label: "timeout_path"
```

---

## Debugging

### Debug Overlay

Enable `Show Debug UI` on `SequenceRunner` to see:
- Current step and sequence
- Call stack depth
- Active variables with types
- Input locks
- Last step outcome

### Validation

`SequenceAsset` automatically validates:
- Missing label targets
- Duplicate labels
- Step configuration errors
- Parallel-track authoring issues (duplicate / missing track ids and blocking warnings)

Check Console for validation warnings.

---

## Migration Notes

### Updating Existing Sequences

Existing sequences continue to work. Steps use backwards-compatible `Execute(context)` signature.

### New Features

To use new features:
1. Update steps to use typed variables
2. Add labels for branching
3. Set failure policies as needed
4. Mark execute-once steps
5. Add checkpoints for resume

---

For more information, see `SEQUENCE_SYSTEMS_SUMMARY.md`.

