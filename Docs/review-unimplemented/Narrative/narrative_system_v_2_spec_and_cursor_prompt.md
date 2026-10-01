> **Parked for review.** Yet to implement as a whole. `SequenceRunner` already has child tracks; this spec goes further and is not the live authoring guide. Review list: [../README.md](../README.md).

# Narrative System V2 Spec Sheet

## Purpose
Refine the existing systems-first narrative stack into a more scalable, less robotic, more layered runtime that can support quests, narrative scenes, reactive NPC/object behavior, branching content, and concurrent staging without losing determinism, readability, or checkpoint safety.

This spec combines two major improvement directions:

1. **Narrative structure and authoring constraints**
2. **SequenceRunner evolution into concurrent track-based orchestration**

The goal is not to replace the current system. The goal is to preserve what already works and extend it with cleaner hierarchy, safer authoring, better runtime composition, and more lifelike simultaneous narrative events.

---

# 1. Current System Summary

## Existing strengths
The current narrative system already has a strong systems-first foundation:

- `SequenceAsset` as ordered step containers
- `SequenceRunner` as deterministic step executor
- `SequenceDirector` as orchestration entry point
- `SequenceContext` as runtime bag for variables, locks, claims, and services
- `SequenceState` for cross-scene persistence and execute-once tracking
- `SequenceVariableStore` for local/global/persistent variables
- composable `SequenceCondition` assets
- label/jump/branch/call/return control flow
- dialogue, camera, scene loading, onboarding, quest, encounter, and NPC-specific step coverage
- `QuestProgressInteractionBridge` and interaction profiles for data-driven NPC/object reactivity
- `TriggerRegistry` for event waits and trigger-based sequencing
- `NPCMemoryComponent` for local NPC state

## Current structural limits
The current runtime is strong for linear deterministic orchestration, but it has several scaling issues:

- one active `currentSequence`
- one `currentStepIndex`
- one top-level execution coroutine
- one main instruction lane
- one blocking step lifecycle at a time
- no first-class distinction between not-started vs available vs active vs completed quest states
- no top-level campaign or arc hierarchy
- no formal world-state layer
- no first-class time-of-day sequencing condition/step
- no parallel narrative tracks for ambient/camera/NPC/world behaviors
- risk of content entropy if variables, trigger ids, and ownership rules are not standardized

---

# 2. Design Goals

## Primary goals
- Keep the architecture systems-first and data-driven
- Preserve deterministic main narrative flow
- Make scenes feel layered, simultaneous, and alive instead of serialized and robotic
- Improve authoring discipline before content scale increases
- Add hierarchy above quests and sequences
- Improve state granularity for quests, NPCs, and world conditions
- Support reactive world design in RPG/strategy style patterns
- Keep checkpointing and resume behavior reliable
- Avoid uncontrolled shared-state mutation in concurrent narrative flows

## Non-goals
- Do not replace the entire narrative system with an external node editor
- Do not make every sequence parallel by default
- Do not allow child tracks to mutate critical quest state without explicit rules
- Do not turn the runtime into an opaque black box that is hard to debug

---

# 3. Narrative Structure Upgrades

## 3.1 NarrativeArc superstructure
Add a new top-level content container above quests and sequences.

### `NarrativeArcSO`
```csharp
NarrativeArcSO
- arcId
- arcType (Main, Side, WorldEvent, Faction, Seasonal)
- priority
- entryConditions[]
- questIds[]
- requiredVariables[]
- gatingRules[]
- completionRules[]
- fallbackArcIds[]
```

## Responsibilities
- defines campaign-level or arc-level progression
- groups related quests and sequences
- controls what content can activate at a macro level
- enables branch-and-bottleneck structure without forcing every branch into a giant sequence
- supports future seasonal/live-event content

## Runtime behavior
- `SequenceDirector` and/or `NarrativeService` should be able to query active/inactive arcs
- quests should register to arcs
- interaction bridges may optionally gate against arc state
- arc progression may drive world state, NPC availability, encounter tables, and scene dressing

---

## 3.2 Formal quest state machine
Replace the current coarse inactive/active/completed model with a richer runtime state.

### `QuestState`
```csharp
QuestState
- Locked
- Available
- Active
- Completed
- Failed
```

### `QuestRuntimeState`
```csharp
QuestRuntimeState
- questId
- currentState
- objectiveStates[]
- timestamps
- sourceType
- sourceId
- completionContext
```

## Why this matters
- separates discoverable quests from accepted quests
- allows NPCs to advertise or offer quests before activation
- allows better UI and world-state gating
- prevents overloading “inactive” to mean both not-started and completed
- supports richer reactive dialogue logic

## Recommended tags
```csharp
QuestTags
- Main
- Side
- Faction
- Region
- Repeatable
- Timed
- Hidden
- Tutorial
```

---

## 3.3 Dialogue graph upgrade
Keep sequence-triggered dialogue, but evolve dialogue assets toward graph-capable interaction.

### `DialogueNode`
```csharp
DialogueNode
- id
- text
- speakerId
- conditions[]
- responses[]
- sideEffects[]
```

### `DialogueResponse`
```csharp
DialogueResponse
- id
- text
- nextNodeId
- conditions[]
- effects[]
```

## Benefits
- player choice
- soft branching
- reputation/memory-sensitive dialogue
- reusable conditional response logic
- supports future faction/reputation/social-state systems

## Integration rule
- `Step_PlayDialogue` remains the primary narrative step entry point
- dialogue graph resolution reads from `SequenceContext`, quest state, NPC memory, world state, and arc state

---

## 3.4 WorldState layer
Add a dedicated persistent world-state model separate from sequence-local variables and per-NPC memory.

### `WorldState`
```csharp
WorldState
- globalFlags
- regionStates
- factionStates
- unlockedSystems
- currentTimePhase
- eventStates
```

## Uses
- persistent region changes
- world-phase shifts
- dynamic NPC/object reactions
- scene variants
- encounter pools
- unlock chains for systems and services

---

## 3.5 Time-of-day as first-class narrative data
Current time usage is indirect. Promote it to direct narrative conditions and waits.

### Add
```csharp
ConditionTimeOfDay
- isNight
- allowedRanges[]
```

```csharp
Step_WaitForTimeOfDay
- targetRange
- timeout
- onTimeoutLabel
```

---

# 4. Authoring Constraints

These are mandatory guardrails to keep the system scalable.

## 4.1 No raw variable keys in authored content
All variables must come from a registry/definition asset.

### `VariableDefinitionSO`
```csharp
VariableDefinitionSO
- key
- type
- scope
- isPersistent
- category
- description
```

## Rule
- steps and conditions reference variable definitions, not ad hoc strings

---

## 4.2 No direct trigger strings for new authored event workflows
String triggers may remain for legacy support, but new content should move toward typed event or signal channels.

### Preferred evolution
- event channels or typed narrative signals
- centralized signal definitions

---

## 4.3 Every sequence must belong to a content owner
Each `SequenceAsset` should declare one of:
- narrative arc
- quest
- system event
- interaction package
- ambient package

This prevents orphan logic.

---

## 4.4 NPC interaction must remain profile-driven
No custom one-off interaction branches directly on scene objects unless explicitly approved for debugging.

NPC and object narrative behavior should resolve through:
- quest interaction profiles
- narrative state
- world state
- NPC memory
- arc state

---

## 4.5 Ownership rules for mutable scene actors
Any track or step that mutates an NPC, camera, or controlled world object must first own or claim it.

This extends the existing NPC claim pattern into a broader runtime policy.

---

# 5. Sequence Runner V2 — Track-Based Concurrent Orchestration

## Summary
Do not replace the current `SequenceRunner` concept. Reframe it.

The current runner becomes the authoritative **Main Track executor**. Add support for lightweight child executions called **Track Runners** or **mini-runners**.

## Target model
- one **Main Track** for checkpoint-safe, quest-safe progression
- zero or more **Child Tracks** for ambient, camera, NPC, UI, and world side actions
- explicit sync points using waits and signals
- constrained context sharing
- deterministic ownership and cleanup

---

## 5.1 Main conceptual shift
Today the runtime is effectively:
- one sequence
- one instruction pointer
- one blocking loop

V2 should support:
- one host/orchestrator
- many execution lanes
- one authoritative progression lane
- optional simultaneous support lanes

---

## 5.2 Runtime execution types

### `SequenceExecution`
```csharp
public sealed class SequenceExecution
{
    public string ExecutionId;
    public string TrackId;
    public SequenceTrackType TrackType;
    public SequenceAsset Sequence;
    public SequenceContext Context;
    public SequenceState State;
    public int CurrentStepIndex;
    public bool IsRunning;
    public bool IsBlocking;
    public bool IsPersistent;
    public StepOutcome LastStepOutcome;
    public Dictionary<string, int> LabelIndex;
    public Stack<CallStackFrame> CallStack;
    public Coroutine Coroutine;
}
```

### `SequenceTrackType`
```csharp
public enum SequenceTrackType
{
    Main,
    Ambient,
    NPC,
    Camera,
    World,
    UI,
    CombatPrep
}
```

### `SequenceRunnerHost`
```csharp
public sealed class SequenceRunnerHost : MonoBehaviour
{
    private SequenceExecution mainExecution;
    private readonly Dictionary<string, SequenceExecution> activeTracks = new();
}
```

---

## 5.3 Track policies

### Main Track
- authoritative
- checkpoint-safe
- may mutate critical quest state
- may lock input
- may start/stop child tracks
- may gate progress

### Ambient Track
- non-authoritative
- no checkpointing
- no quest-critical writes
- safe to cancel/restart

### NPC Track
- non-authoritative
- must claim controlled NPCs
- may drive locomotion, facing, animation, interaction posture
- may emit signals back to main track

### Camera Track
- non-authoritative
- controls shots, framing, blends, presentation
- never owns quest state
- safe to force-restore

### World Track
- non-authoritative unless explicitly promoted
- may toggle world props, colliders, ambiance, lighting, effects, zone state
- should be idempotent where possible

### UI Track
- non-authoritative
- menu, overlays, cinematic prompts, letterbox, reward panels

---

## 5.4 Context modes for child tracks
Parallel execution must not freely share mutable state.

### `TrackContextMode`
```csharp
public enum TrackContextMode
{
    Shared,
    Child,
    Snapshot
}
```

### Modes
**Shared**
- same variable bag
- dangerous
- only for intentionally coordinated sequences

**Child**
- preferred default
- reads parent globals/persistent state
- owns local variables independently
- safest default for concurrent use

**Snapshot**
- read-mostly fork
- ideal for isolated staging packages

### Rule
Default to `Child`.

---

# 6. New Narrative Track Steps

## `Step_RunTrack`
Starts a child sequence on a named track.

```csharp
Step_RunTrack
- trackId
- trackType
- sequence
- contextMode
- blockingMode
- cancelPolicy
- allowReuseIfRunning
```

### `TrackBlockingMode`
```csharp
TrackBlockingMode
- NonBlocking
- WaitForComplete
- WaitForSignal
```

### `TrackCancelPolicy`
```csharp
TrackCancelPolicy
- StopOnParentEnd
- PersistUntilComplete
- ManualOnly
```

---

## `Step_WaitForTrack`
Allows the main track to join only when needed.

```csharp
Step_WaitForTrack
- trackId
- completionMode (Completed, Signaled, LabelReached)
- targetLabel
- timeoutSeconds
- onTimeoutLabel
```

---

## `Step_StopTrack`
Stops a track or requests graceful shutdown.

```csharp
Step_StopTrack
- trackId
- stopMode (Immediate, Graceful, SignalCancel)
```

---

## `Step_WaitForAll`
Barrier/join synchronization for authored moments.

```csharp
Step_WaitForAll
- trackIds[]
- signalIds[]
- timeoutSeconds
- onTimeoutLabel
```

---

## `Step_WaitForAny`
Race condition / first-finish synchronization.

```csharp
Step_WaitForAny
- trackIds[]
- signalIds[]
- timeoutSeconds
- onTimeoutLabel
```

---

## `Step_Signal`
Emit a typed narrative signal.

```csharp
Step_Signal
- signalId
- payloadMode
- payloadData
```

---

# 7. Narrative Signal Bus

Current trigger strings are useful, but concurrent sequencing benefits from explicit signal semantics.

## `NarrativeSignalBus`
```csharp
NarrativeSignal
- signalId
- sourceTrackId
- sourceSequenceId
- payload
- timestamp
```

## Uses
- camera ready
- NPC reached marker
- animation cue reached
- world state applied
- dialogue branch selected
- ambient package finished

## Rule
Signals are primarily for synchronization and cross-track communication.

---

# 8. Persistence and Resume Rules

Checkpointing is where concurrency becomes dangerous. Keep MVP persistence simple.

## MVP rule
Only the **Main Track** persists instruction-pointer state.

### Child track persistence policy
- child tracks do not restore from mid-step checkpoints in MVP
- child tracks are restored by declarative reapplication when needed
- world and NPC visual state should be rebuilt from world state, quest state, and active sequence conditions

## Why
- prevents complex multi-track deadlock restores
- keeps resume deterministic
- reduces save corruption risk

---

# 9. Recommended Scene Pattern

## Example
A story beat inside a town scene:

### Main Track
- lock input
- face player to NPC
- begin dialogue
- wait for choice
- set quest state
- unlock input

### NPC Track
- companion walks to side marker
- looks at doorway
- loops nervous animation
- signals `companion_ready`

### Camera Track
- blend to close shot
- slow drift
- signal `camera_ready`
- restore follow on signal

### World Track
- dim ambient light
- enable rain particles
- arm trigger collider
- play distant SFX

### Main Track sync point
- `Step_WaitForAll(camera_ready, companion_ready)`
- continue dialogue only when scene presentation is staged

This is the target feel: layered, synchronized, not serialized.

---

# 10. Expanded Narrative State Model

## 10.1 NPC memory persistence
Current NPC memory is useful but should become persistable when needed.

### `NPCMemorySnapshot`
```csharp
NPCMemorySnapshot
- npcId
- disposition
- flags
- counters
- lastInteractionTime
```

## Rule
- short-lived ambient memory can remain runtime only
- quest-relevant or social-state memory should be persisted

---

## 10.2 Social or faction state
Later extension:

```csharp
FactionState
- factionId
- reputationValue
- relationshipTier
- discoveredFlags
```

This can drive dialogue, pricing, access, quest availability, and reactive world behavior.

---

# 11. Debugging and Tooling Requirements

A concurrent system must be deeply inspectable.

## Build a Narrative Debug Overlay
Display at runtime:
- active arc
- active quests and states
- active main sequence
- current main step
- active child tracks
- each track current step
- current waits/barriers
- emitted recent signals
- owned NPC/object claims
- active input locks
- variable values by scope
- world state summary

## Debug actions
- jump main track to label
- force complete objective
- set variable value
- emit signal manually
- stop child track
- toggle arc active state in dev builds

## Deadlock visibility
If multiple tracks are waiting, show a wait graph or dependency chain in the debug view.

---

# 12. Migration Strategy

Do this incrementally.

## Phase 1 — Structure
1. Add `NarrativeArcSO`
2. Expand quest states to include `Available`, `Locked`, `Failed`
3. Add `VariableDefinitionSO`
4. Add `WorldState`

## Phase 2 — Runtime extraction
5. Extract sequence runtime data from `SequenceRunner` into `SequenceExecution`
6. Convert current `SequenceRunner` MonoBehaviour into a host/orchestrator pattern
7. Keep current main-sequence behavior intact

## Phase 3 — Concurrency MVP
8. Add `SequenceTrackType`
9. Add child track registry
10. Implement `Step_RunTrack`
11. Implement `Step_WaitForTrack`
12. Implement `Step_StopTrack`
13. Add `NarrativeSignalBus`

## Phase 4 — Authoring and safety
14. Add ownership rules and validations
15. Add debug overlay for tracks and signals
16. Add validation tools for duplicate ids, invalid references, unsafe shared-context use

## Phase 5 — World depth
17. Add `ConditionTimeOfDay`
18. Add `Step_WaitForTimeOfDay`
19. Add dialogue graph support
20. Add NPC memory persistence for quest-relevant actors

---

# 13. Technical Guardrails

## Must preserve
- deterministic main progression
- existing label/jump/call/return semantics
- execute-once behavior
- existing sequence assets where possible
- quest and interaction bridge compatibility

## Must avoid
- free-for-all shared mutable context
- every track being checkpointed
- silent ownership conflicts
- stringly-typed authored state everywhere
- child tracks mutating critical quest state by default

---

# 14. Success Criteria

The refactor is successful if:

- scenes can stage multiple actors/camera/world beats simultaneously
- authored moments feel more organic and less stop-and-go
- quest progression remains deterministic
- designers can reason about ownership and synchronization clearly
- checkpointing remains safe and understandable
- narrative debugging becomes easier, not harder
- the architecture remains modular and systems-first

---

