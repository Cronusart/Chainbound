# Narrative Encounter Service — Systems-First Improvement Plan

This document aligns future work on encounter orchestration with [.cursor/rules/systems-first-architecture.mdc](../../.cursor/rules/systems-first-architecture.mdc) and the **systems-first-architecture** Cursor skill.

---

## 1. Moving `NarrativeEncounterService` to its own GameObject

**Recommendation:** Yes — put it on a child GameObject under the same `GameServices` prefab root (e.g. `GameServices/NarrativeEncounter` or `Services/CombatEncounters`).

**Impact:** None on code discovery. `Step_StartEncounter.FindEncounterService()` uses `GameServicesContainer.GetComponentsInChildren<MonoBehaviour>(true)`, so any child of the services hierarchy still resolves `IEncounterService`.

**Checklist when moving:**

- Drag component to new child, or Add Component on new GO and copy inspector values from the old slot.
- Remove duplicate component from the old GameObject.
- Save prefab; run one playtest that hits `Step_StartEncounter`.

---

## 2. Current limitations (why it feels quest-specific)

| Area | Issue |
|------|--------|
| **Registry** | Large inline list on one MonoBehaviour — hard to split by quest/campaign at scale. |
| **Data** | Pre/post dialogue and providers are embedded per row; no shared "encounter definition" asset as single source of truth. |
| **Solana coupling** | Launch path assumes `SolanaServiceBridge` for encounter injection — correct for this game but should stay behind a narrow **encounter launch** abstraction for tests/alternate entry points. |
| **Dialogue** | Dialogue sequences are referenced directly on entries; no indirection via quest-specific **EncounterPresentationConfig** or addressable IDs. |
| **Outcomes** | Outcome mapping is fine, but branching "what plays next" is still mostly sequence-driven rather than data tables. |

---

## 3. Target architecture (systems-first)

### 3.1 Encounter definition as data (ScriptableObject)

Introduce something like **`NarrativeEncounterDefinitionSO`** (name TBD) as the **authoring atom**:

- `encounterId` (stable string, matches sequence steps)
- `ManualEncounterProviderSO` (or reference to `IEncounterProvider` via asset)
- Optional: `preBattleDialogue`, `postReturnDialogue` (or GUID/addressable keys if you move to content pipelines later)
- Optional: per-entry **`EncounterBattleLoadSettings`** (`battleLoad` on registry rows): battle scene name, spawn id, `SceneLoader` transition style / `useSceneLoader`, and optional timeout overrides (see `NarrativeEncounterService` defaults when fields are left empty / inherit)

**`NarrativeEncounterService`** (or a thin **`EncounterFlowService`**) then:

- Loads definitions from a **list/array of SOs** *or* a single **registry SO** that only holds references — not a giant inspector list on scene code.

Benefits: designers duplicate assets per encounter; code stays a **single interpreter**.

### 3.2 Registry strategies (pick one for v1)

- **A — Registry ScriptableObject** — One asset `NarrativeEncounterRegistrySO` with `NarrativeEncounterDefinitionSO[]`; service reads it at runtime.
- **B — Addressables / content tags** — `encounterId` resolves definition; heavier lift.
- **A is recommended** for Signal Jam until content volume demands B.

### 3.3 Interface boundaries

Keep **`IEncounterService`** as the sequence-facing API. Internally:

- **`IEncounterLaunchContext`** — save return scene, set active manual encounter, clear on failure (implemented using existing bridge or a facade).
- Dialogue playback behind **`IDialoguePlayback`** wrapper (optional thin adapter over `DialogueService`) for tests and future swapping.

Avoid new `if (questX)` in the service; quest variance = **different definition assets** or **sequence steps** before/after `StartEncounter`.

### 3.4 Dialogue organization

- **Per-encounter**: fields on `NarrativeEncounterDefinitionSO` (pre/post).
- **Per-quest grouping**: folder convention under `Assets/Data/Narrative/Encounters/{QuestId}/` with definitions + dialogue SOs; no extra code.
- **Shared lines**: reuse the same `DialogueSequenceSO` from multiple definitions.

### 3.5 Sequences remain the orchestrator

- **Step_StartEncounter** stays the primitive: `encounterId` + `EncounterConfig`.
- Optional future steps: `Step_PlayEncounterDialogue` only if you need dialogue *without* battle (otherwise pre/post on definition keeps one step).

---

## 4. Phased implementation roadmap

| Phase | Scope | Outcome |
|-------|--------|--------|
| **P0** | Move service to child GO; document in prefab | Cleaner hierarchy, no behavior change |
| **P1** | Extract `NarrativeEncounterDefinitionSO` + `NarrativeEncounterRegistrySO`; service resolves `encounterId` → definition | Registry data lives in `Assets/Data/**`, not only on prefab |
| **P2** | Migrate existing `GameServices` registry rows to definition assets | Parity with current intro quest |
| **P3** | Optional `IEncounterLaunchContext` facade over bridge | Easier tests / alternate battle entry |
| **P4** | Optional dialogue abstraction | If automated tests or second UI backend appear |

---

## 5. What not to do

- Do not add quest name checks inside `NarrativeEncounterService` — use definition assets and sequence flow instead.
- Do not embed Loomville or intro-specific scene names in the service — keep scene overrides on definitions or environment config.

---

## 6. References

- `Assets/Scripts/Narrative/Combat/NarrativeEncounterService.cs`
- `Assets/Scripts/Narrative/Sequences/Steps/Step_StartEncounter.cs`
- `Assets/Scripts/Chainlings/Encounters/ManualEncounterProviderSO.cs`
- `.cursor/skills/systems-first-architecture/SKILL.md`
