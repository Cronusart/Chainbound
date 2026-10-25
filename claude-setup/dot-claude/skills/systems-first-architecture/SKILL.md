---
name: systems-first-architecture
description: Signal Jam 3D systems-first design rules. Use when designing or refactoring narrative, combat, quests, sequences, tutorials, cutscenes, services, ScriptableObject configs, or when the user asks for reusable architecture, data-driven flow, or avoiding one-off story code.
---

# Systems-First Architecture (Signal Jam 3D)

Source of truth for full rule text: [.cursor/rules/systems-first-architecture.mdc](../../../.cursor/rules/systems-first-architecture.mdc) (also imported into CLAUDE.md)

## When to apply

- Adding features that could appear in multiple quests, events, or tutorials later.
- Reviewing PRs or proposals for "intro-only" or scene-specific logic in core systems.
- Refactoring managers that hard-code story beats.
- Designing encounter, dialogue, or sequence integration.

## Core philosophy

Build **reusable systems**; implement **content as data** (IDs, ScriptableObjects, sequence steps). MonoBehaviours are **services/interpreters**, not storytellers.

## Rules (operational checklist)

1. **Systems over scripts** — No scene-only "manager" scripts that encode story logic. Prefer sequence steps, SO configs, and stable IDs.
2. **Data-driven** — Dialogue, sequences, camera, quests: **content in assets**; code **reads** IDs/configs, does not embed copy or one-off branches.
3. **Composable primitives** — Prefer small reusable pieces: lock input, play dialogue, load scene, wait for trigger, read/write state. Compose; don't monolith.
4. **Token-based locks** — No global `isInCutscene` booleans for flow control; use handles/tokens that stack where applicable.
5. **Interfaces for volatile domains** — Encounter/combat bridge, save, quest extensions: code behind interfaces when the implementation will change.
6. **IDs over scene references** — Cross-scene flow uses encounter IDs, sequence IDs, trigger IDs. Avoid SOs that reference scene-only objects.
7. **Scene objects are dumb** — Triggers/NPCs raise events or expose IDs; sequences/systems arm them.
8. **No intro-specific branches in core** — No `if (isIntro)` in shared combat/narrative cores; behavior lives in assets (sequences, dialogue, shots).
9. **Explicit state** — Sequence index, active locks, encounter outcome: inspectable and deterministic transitions.
10. **Bias extensibility** — If a task can be solved with a new **Step**, **Config**, or **Asset**, prefer that over new one-off logic.

## Enforcement shortcut

Ask: *"Could this be reused for another quest, event, or tutorial without copying code?"*  
If no → refactor toward data + small primitives before merging.

## Project-specific pointers

- **Encounters**: Registry by `encounterId` + `ManualEncounterProviderSO`; dialogue hooks should stay data on entries or separate encounter definition assets—not hard-coded in the service body.
- **Sequences**: New beats → new `SequenceStep` assets or steps, not new MonoBehaviours per quest. Continue-game resumes load `Step_SetCheckpoint` scene/spawn, then the checkpoint label (idle gate).
- **World presence**: Durable per-profile facts (intro guardrails, unique pickups, lab starter orbs) use profile flags + name catalog. Guardrail Enable/Disable can persist per object; checkpoints snapshot flags for continue-game. Inventory is bag state; `world.pickup.<spawn_id>` is “this spawn is spent.” Do not add a pickup-only sequence step.
- **Combat narrative bridge**: `IEncounterService` + `Step_StartEncounter` discover services under `GameServicesContainer`; keep services discoverable (e.g. child objects OK).
- **Lever boards**: Tunable systems use per-system Config SOs via `BalanceConfigRuntime` (resolver, rewards, progression, vending, modifier overlays). One board owns each value — no cross-board duplication. CSVs remain SoT for catalog rows; overlays are patches. See [Docs/Specs/Combat/LEVER_BOARDS_v1.md](../../../Docs/Specs/Combat/LEVER_BOARDS_v1.md).

## Related docs

- Narrative encounter evolution plan: [Docs/Narrative/Narrative_Encounter_Service_Systems_First_Plan.md](../../../Docs/Narrative/Narrative_Encounter_Service_Systems_First_Plan.md) (if present).
- Lever boards: [Docs/Specs/Combat/LEVER_BOARDS_v1.md](../../../Docs/Specs/Combat/LEVER_BOARDS_v1.md).
