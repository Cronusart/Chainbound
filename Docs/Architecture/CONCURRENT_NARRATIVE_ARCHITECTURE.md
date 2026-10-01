# Concurrent Narrative Quest Sequences — Architecture & Migration

> **Status:** Partial. `SequenceRunner` now hosts one main `SequenceExecution` plus child tracks (`TryStartChildTrack`). Later phases in this file (a separate `SequenceRunnerManager`, compensate/rollback, a quest event queue) are not that class. Where this file disagrees with `Assets/Scripts/Narrative/Sequences/SequenceRunner.cs`, the code wins.

> Reference document for enabling safe concurrent quest/sequence execution.
> Based on research into Saga pattern, event queues, idempotent consumers, and deterministic simulation.

---

## Executive Summary

The current codebase **cannot safely run multiple narrative sequences concurrently** because both the quest layer and the sequencer layer use shared mutable runtime state:

- **SequenceRunner** explicitly stops any running sequence on `StartSequence()`, clears shared context, and uses a single `persistentState` for execute-once, checkpoints, and variables.
- **QuestService** stores all quest state in one dictionary; it supports multiple active quests but has no versioning or idempotency.
- Cooperative concurrency (coroutines, callbacks, event handlers) is vulnerable to reentrancy, out-of-order events, and duplicate triggers even on a single thread.

The safest path is **incremental migration**: per-instance state, idempotent step completion, and a resource arbitration layer (tokens) for exclusive channels (dialogue/camera/input).

---

## Current State

### QuestService

- **Storage:** `Dictionary<string, QuestState> activeQuests` — supports multiple active quests.
- **Gap:** No public enumeration API; no versioning; no idempotency for progress updates.
- **Events:** `OnQuestStarted`, `OnObjectiveProgressChanged`, `OnQuestCompleted`.

### SequenceRunner

- **Storage:** Single `SequenceContext`, single `SequenceState`, single `currentSequence`, single coroutine.
- **Behavior:** `StartSequence()` stops current sequence, clears context, overwrites state.
- **Gap:** No per-execution isolation; execute-once keys are global; no token-owner-aware lock release.

### Step_CallSequence

- Sub-sequences run **synchronously** (call/return). Parent yields until child completes.
- Not parallel execution.

---

## Concurrency Hazards

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Cross-sequence state corruption | High | Critical | Per-instance `SequenceContext`/`SequenceState`, namespaced keys |
| Input/dialogue/camera conflicts | High | High | Tokenized channels + arbitration; single owner per channel |
| Duplicate triggers causing double rewards | Medium | High | Idempotent handlers + processed IDs store (per quest/execution) |
| Reentrancy and out-of-order events | Medium | High | Event queue processed at stable loop phase; versioning |
| Cancellation leaves world inconsistent | Medium | High | Saga-like compensations and explicit failure policies |
| Debugging complexity increases | High | Medium | Instrumentation: IDs, versions, structured logs, metrics |

---

## Migration Phases

### Phase 0: Instrumentation + Debug Harness ✅ (Implemented)

- Add `GetActiveQuestIds()` to QuestService.
- Add `QuestSequenceDebugHarness` overlay: active quests, objectives, current sequence, step, pending step. Toggle with F10.
- Add structured logs for `StartSequence`, `StopSequence`, step start/complete, failures.
- **Setup:**
  1. Add `QuestSequenceDebugHarness` component to the GameServices GameObject (or any persistent object).
  2. Enable `Enable Debug Logs` on SequenceRunner for structured `[SEQ]` logs in the console.
  3. Press F10 to toggle the harness overlay at runtime.
- **Risk:** Low. **Benefit:** Visibility for testing and future phases.

### Phase 1a: Idempotent Step Completion

- Introduce stable step keys (e.g. `sequenceId_stepIndex` or per-step GUID).
- Before applying completion, check per-execution `ProcessedStepKeys`.
- Ignore duplicates; optionally log.
- **Risk:** Low. **Benefit:** Prevents double rewards/effects from duplicate callbacks.

### Phase 1b: Namespace Execute-Once Keys

- Change `ShouldSkip` / `MarkCompleted` to use `{sequenceId}_{stepIndex}` instead of global keys.
- **Risk:** Low. **Benefit:** Prepares for multiple executions; avoids collisions.

### Phase 1c: Extract SequenceExecution (Single Instance)

- Extract `SequenceExecution` class: `context`, `persistentState`, `stepIndex`, `callStack`.
- Keep `SequenceRunner` as thin wrapper managing exactly one execution.
- **Risk:** Medium. **Benefit:** Clean structure; no behavior change yet.

### Phase 2: Token-Owner-Aware Lock Release

- Associate input locks with owner (e.g. `executionId`).
- On `StopSequence`, release only that execution’s locks.
- **Risk:** Medium. **Benefit:** Cancellation doesn’t unlock another sequence’s locks.

### Phase 3: Optional Compensate() on Steps

- Extend `SequenceStep` with optional `Compensate()`.
- Call when sequence is cancelled.
- **Risk:** Medium. **Benefit:** Saga-style rollback for critical side effects.

### Phase 4: Event Queue for Quest/Sequence Events

- Queue quest/sequence events; process at stable point (e.g. end of Update).
- **Risk:** Medium. **Benefit:** Stable ordering; reduces reentrancy.

### Phase 5+: Multiple SequenceExecution Instances

- `SequenceRunnerManager` hosting `Dictionary<executionId, SequenceExecution>`.
- Resource arbitration for dialogue/camera/input channels.
- **Risk:** High. **Benefit:** True concurrent sequences.

---

## Architectural Patterns (Reference)

| Approach | Complexity | Migration | Use Case |
|----------|------------|-----------|----------|
| Per-Quest Runner Instances | Medium | Medium | Best default for multiple active quests; easiest reasoning |
| Centralized Sequencer with Queue | Medium | Low–Medium | Serialize on channels; concurrent background evaluation |
| Event-Sourced Saga | High | High | Auditability, rollback, deterministic replay |
| Deterministic Tick Scheduler | High | High | Strict determinism, multiplayer-like consistency |

---

## Quest State Machine (Target)

```
Inactive → StartQuest → Active
Active → TriggerSequence → WaitingForSequence
WaitingForSequence → SequenceCompleted → Active
WaitingForSequence → SequenceCancelled (compensate) → Active
Active → AllObjectivesComplete → ReadyToComplete
ReadyToComplete → ConfirmCompletion → Completed
Active → CancelQuest → Cancelled (run compensations)
```

---

## Instrumentation (Minimum Viable)

- `questId`, `sequenceId`, `stepIndex`, `stepKey`
- Monotonic version per quest and per execution (future)
- Event IDs for external triggers (future)
- Structured logs: `[SEQ] StartSequence sequenceId=X`, `[SEQ] StepComplete step=N`, etc.
- Counters: active executions, queued events, lock contention (future)

---

## References

- [Unity Coroutines](https://docs.unity3d.com/6000.3/Documentation/Manual/Coroutines.html)
- [Saga Pattern (Garcia-Molina & Salem)](https://www.cs.cornell.edu/andru/cs711/2002fa/reading/sagas.pdf)
- [Idempotent Consumer](https://microservices.io/post/microservices/patterns/2020/10/16/idempotent-consumer.html)
- [Event-Driven (Martin Fowler)](https://martinfowler.com/articles/201701-event-driven.html)
- [Event Sourcing (Martin Fowler)](https://martinfowler.com/eaaDev/EventSourcing.html)
- [Saga Design Pattern (Azure)](https://learn.microsoft.com/en-us/azure/architecture/patterns/saga)
- [Event Queue (Game Programming Patterns)](https://gameprogrammingpatterns.com/event-queue.html)
- [Deterministic Simulation (GDC)](https://media.gdcvault.com/gdceurope2016/presentations/Salz_David_Deterministic_Simulation.pdf)
