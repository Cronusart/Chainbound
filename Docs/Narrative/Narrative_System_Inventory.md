# Narrative system inventory (current layout)

**Purpose:** Single reference for what exists today in Signal Jam 3D narrative: sequences, conditions, variables, branching, state, events, and interact/NPC/quest/time-of-day touchpoints. Aligns with the systems-first architecture rule (data-driven steps, IDs, composable primitives). That rule file is not in this public snapshot.

**Related docs:** [SEQUENCE_AUTHORING_GUIDE.md](SEQUENCE_AUTHORING_GUIDE.md), [INTERACTABLE_SETUP.md](INTERACTABLE_SETUP.md), [CHAINBOUND_INTRO_QUEST_SETUP.md](CHAINBOUND_INTRO_QUEST_SETUP.md). Older [SEQUENCE_SYSTEMS_SUMMARY.md](SEQUENCE_SYSTEMS_SUMMARY.md) is partially superseded for step lists.

---

## 1. Core orchestration

| Piece | Role |
|--------|------|
| **SequenceAsset** (`ScriptableObject`) | Ordered list of `SequenceStep` assets; `sequenceId`, optional start checkpoint. |
| **SequenceRunner** | Executes steps; supports **labels**, **jump**, **execute-once** per step, failure policies. |
| **SequenceDirector** | Entry point: `Play(SequenceAsset)`, `Queue`, `Interrupt`, `StopCurrent`, **`TryPlayOnce(gateKey, …)`** (tracks `playedGateKeys` in persistent state). `Play(string id)` not implemented (TODO). |
| **SequenceContext** | Per-run bag: **SequenceVariableStore**, typed shortcuts (`PlayerName`, `StarterSelected`, `CurrentSequenceId`, `CurrentStepIndex`), **input lock handles**, **NPC claim handles** (`Step_NPC_ClaimControl` / release). Resolves services (dialogue, camera shots, scene load, spawn, etc.). |
| **SequenceState** | Cross-scene persistence: current sequence/step, checkpoint, scene/spawn ids, **execute-once completed step GUIDs**, **playerName / starterSelected**, **persistentVariables** dictionary, **`playedGateKeys`** for `TryPlayOnce`. |
| **PendingSequenceStarter** | On scene load, if `GameSessionContext.PendingSequence` is a `SequenceAsset`, starts it via `SequenceDirector` and clears pending. |
| **DemoSceneSequenceBootstrap** | Dev/bootstrap helper for sequences in demo scenes (see script for setup). |

---

## 2. Variables

| Mechanism | Description |
|------------|-------------|
| **SequenceVariableStore** | Typed **Local** vs **Global** scope; local cleared per sequence; global persists until cleared. Optional **persistent** keys synced with `SequenceState`. |
| **SequenceContext** | `GetVariable` / `SetVariable` (legacy object), `GetVariable<T>`, `Variables.Set/Get` with `VariableScope`. |
| **Built-in context fields** | `PlayerName`, `StarterSelected` (used by name/starter/save steps). |
| **Step_SetVariable** | Authoring step to set variables. |

**Conditions** reading variables: `ConditionVariableExists`, `ConditionVariableCompare`. **Control flow:** `Step_Switch` jumps by string value of a variable key.

---

## 3. Conditions (`SequenceCondition` assets)

Composable via **ConditionAnd**, **ConditionOr**, **ConditionNot**.

| Condition | Evaluates |
|-----------|-----------|
| **ConditionQuestState** | Quest started/active/completed via `QuestService`. |
| **ConditionVariableExists** | Named variable present. |
| **ConditionVariableCompare** | Compare variable to value (typed compare in asset). |
| **ConditionSceneActive** | `SceneManager.GetActiveScene().name` equals configured name. |
| **ConditionTriggerState** | State of a trigger id in `TriggerRegistry` (armed/fired per implementation). |

---

## 4. Control flow & branching (sequence steps)

| Step | Behavior |
|------|----------|
| **Step_Label** | Named anchor for jumps. |
| **Step_JumpToLabel** | Unconditional jump. |
| **Step_If** | One `SequenceCondition` → jump **trueLabel** / **falseLabel**. |
| **Step_Branch** | Ordered **cases** (condition + label); first match wins; optional **defaultLabel**. |
| **Step_Switch** | Switch on string value of a variable → case labels + default. |
| **Step_CallSequence** | Nested / sub-sequence invocation (see asset). |
| **Step_Return** | Return from call stack frame (paired with call semantics in runner). |

**Step-level failure handling:** Each `SequenceStep` can use **StepFailurePolicy** (runner default, goto label, retry count, etc.).

---

## 5. Waits, retries, time

| Step | Role |
|------|------|
| **Step_WaitSeconds** | Fixed delay. |
| **Step_WaitForEvent** | Wait for `TriggerRegistry` **eventId**; optional **timeout** + **onTimeoutLabel**. |
| **Step_WaitForTrigger** | Wait until trigger id fires (collider/distance triggers register with registry). |
| **Step_WaitForSceneActive** | Wait until active scene name matches. |
| **Step_WaitForDistance** | Wait for player within distance of target. |
| **Step_WaitForQuestState** | Wait until quest reaches target state. |
| **Step_Retry** | Wrapper / retry pattern (see asset). |
| **Step_Timeout** | Timeout wrapper pattern (see asset). |
| **Step_ResetTrigger** | Reset a trigger id in registry (pairs with wait/event patterns). |

---

## 6. Dialogue, camera, video, world

| Step | Role |
|------|------|
| **Step_PlayDialogue** | `DialogueSequenceSO` via `DialogueService`; optional **lockInputDuringDialogue**; optional **DialogueSpeakerCameraBridge** + persist flag. |
| **Step_ReleaseDialogueCamera** | Releases speaker camera after persisted dialogue. |
| **Step_PlayCameraShot** | `CameraShotController` shot. |
| **Step_RestoreCameraFollow** | Return to follow camera. |
| **Step_PlayVideoCutscene** | Video playback. |
| **Step_LoadScene** | Scene load + spawn integration via `SceneLoader` / spawn ids. |
| **Step_EnableGameObject** | Enable/disable scene objects (often used to arm zones or props). |
| **Step_SetColliderState** | Enable/disable colliders. |
| **Step_Player_FaceTarget** | Orient player toward target. |

---

## 7. Input, quests, profile, intro, onboarding

| Step | Role |
|------|------|
| **Step_LockInput** / **Step_UnlockInput** | `InputLockService` token locks. |
| **Step_StartQuest** | Starts a quest by id / definition. |
| **Step_SetQuestObjectiveProgress** | Mutates objective progress. |
| **Step_SetIntroComplete** | Supabase intro flag (fire-and-forget), `GameSessionContext.MarkIntroComplete`, `QuestService.CompleteQuest(introQuestId)`. |
| **Step_PromptNameInput** | `NameInputUI` → context name. |
| **Step_SelectStarterChainling** | `StarterSelectionService` → `StarterSelected`. |
| **Step_CommitFirstSave** | `ISaveService.CommitNewProfile` (`LocalSaveServiceStub` default). |
| **Step_SetCheckpoint** | Checkpoint for resume. |
| **Step_Log** | Debug log step. |

---

## 8. Combat / encounters (narrative bridge)

| Piece | Role |
|--------|------|
| **Step_StartEncounter** | Starts encounter via `IEncounterService` / narrative encounter pipeline. |
| **NarrativeEncounterService**, **NarrativeCombatLaunchBridge**, **NarrativeCombatLaunchProfileSO** | Launch combat from narrative with profiles and flags (e.g. Supabase party resolution toggles). |
| **NpcEncounterTriggerZone**, **EncounterTriggerZone** (world) | Zone-based encounter firing; world encounter trigger may consult **TimeOfDayManager** (e.g. night). |
| **BattleIntroBossDialogueBridge** | Boss dialogue hook into combat presentation. |
| **CombatSceneInstaller** | Wires combat scene providers (including Supabase profile party when configured). |

---

## 9. NPC-specific sequence steps (`Assets/Scripts/NPC/Steps`)

| Step | Role |
|------|------|
| **Step_NPC_ClaimControl** / **Step_NPC_ReleaseControl** | Narrative control claims on `NPCController`. |
| **Step_NPC_MoveTo**, **Step_NPC_Follow**, **Step_NPC_StopLocomotion** | Locomotion / follow. |
| **Step_NPC_FaceTarget** | Face target. |
| **Step_NPC_SetAnimState**, **Step_NPC_SetRouteSchedule**, **Step_NPC_EnableAutonomy** | Animation, routes, autonomy. |
| **Step_NPC_WaitForInteraction** | Wait for player interact on NPC. |
| **Step_NPC_SetQuestInteractionProfile** | Sets **`QuestProgressInteractionBridge`** runtime profile override. |
| **Step_NPCMemory_SetFlag**, **Step_NPCMemory_IncrementCounter**, **Step_NPCMemory_AddDisposition** | Mutate `NPCMemoryComponent`. |

---

## 10. Events & triggers

| Piece | Role |
|--------|------|
| **TriggerRegistry** | String **trigger ids**; `FireTrigger`, `RegisterTrigger`, `IsTriggered`, **`OnTriggerFired`** event. DontDestroyOnLoad singleton pattern. |
| **SequenceTrigger** | MonoBehaviour trigger (collider) that fires registry ids. |
| **Step_WaitForEvent** / **Step_WaitForTrigger** | Sequence side waits on registry. |
| **ConditionTriggerState** | Branch conditions on trigger state. |

**Note:** “Events” in sequences are **trigger id strings**, not C# events, unless counting `OnTriggerFired`.

---

## 11. Interaction layer (player / NPC / objects)

| Component | Role |
|-----------|------|
| **InteractableRegistry** | Discovers `IInteractable` (player uses to find interactable in range). |
| **NarrativeQuestGuard** | Shared serialized guard: required/excluded quest clauses, `Any` / `All`, per-clause lifecycle masks. Canonical availability gate for interactables, redirect zones, and scene gates. |
| **NPCInteractableBinding** | NPC in range + interact; evaluates `NarrativeQuestGuard`, optionally records `NPCMemoryComponent.RecordInteraction`, then invokes UnityEvent targets such as bridges. |
| **InteractableObject** | Static-object interactable using the same canonical guard model. |
| **InteractionBridge** | Deprecated migration sentinel only. New content should not use it. |
| **QuestInteractionProfileSO** | Reusable per-quest profile: ordered rules, inactive outputs, active fallback outputs. |
| **QuestInteractionRouterSO** | Ordered multi-quest router. First slot whose profile quest lifecycle matches its mask wins. |
| **QuestProgressInteractionBridge** | Canonical interaction bridge with shared resolution order: runtime override -> router slot -> default profile. Active quests use ordered rules + active fallback; non-active lifecycles share the inactive bucket in v1. |
| **QuestStateGameObjectGate** | Enables/disables GameObjects based on whether a `NarrativeQuestGuard` passes. |

---

## 12. NPC memory & “time of day”

| Piece | Role |
|--------|------|
| **NPCMemoryComponent** | Per-NPC **disposition**, **flags**, **counters**, **last interaction time** (normalized time via **`TimeOfDayManager`** / `ITimeOfDayService` when recording). Used by **QuestProgressInteractionBridge** predicates and NPC sequence steps. **Not automatically persisted** to save file unless integrated elsewhere. |
| **TimeOfDayManager** | Implements **`ITimeOfDayService`**; used for day/night and for NPC interaction timestamp / ambience / lighting hooks. **Not** a dedicated narrative “condition step” in the sequence library—gameplay zones (e.g. encounters) may read `IsNight` directly. |

There is **no** `Step_WaitForTimeOfDay` in the current inventory; time-gating is via **world scripts** or **memory/time** recorded on NPCs.

---

## 13. Dialogue data

| Piece | Role |
|--------|------|
| **DialogueSequenceSO** | Dialogue asset consumed by `DialogueService` / bridges. |
| **DialogueLine**, **DialogueLineCameraDirective**, **DialogueUIController** | Line-level presentation. |

---

## 14. Session / menu hooks

| Piece | Role |
|--------|------|
| **GameSessionContext** | Wallet, intro flags, **`PendingSequence`** for post-load sequence start, offline session, etc. |
| **Main menu / profile** | Routes play flow; can set pending sequence before load (see `PendingSequenceStarter`). |

---

## 15. Gaps & notes (for future passes)

- **SequenceDirector.Play(string id)** — not implemented; sequences referenced by **asset reference** or pending SO.
- **Quest “inactive”** — single bucket for not-started and completed in bridges/gates; finer splits need extra rules, quests, or memory flags.
- **NPC memory persistence** — in-memory component state; align with save/`SequenceState` if long-term memory is required.
- **Time-of-day as sequence condition** — would be a new **Condition** or **Step** if designers need it in `SequenceAsset` graphs.

---

---

## 16. Narrative V2 additions (runtime)

- **Quest lifecycle:** `QuestLifecycleState`, `QuestRuntimeEntry`; `QuestService` keeps completed/failed quests registered; `IsQuestComplete` is true when lifecycle is **Completed** (no longer “missing from dictionary”).
- **Arcs / authoring:** `NarrativeArcSO`, `QuestDefinitionSO.Tags`, `VariableDefinitionSO`, `NarrativeVariableType`.
- **World / time:** `WorldState`, `ConditionWorldFlag`, `ConditionTimeOfDay`, `Step_WaitForTimeOfDay`.
- **Tracks:** `SequenceExecution`, `SequenceTrackType`, `TrackContextMode`, `TrackCancelPolicy`, `TrackStopMode`; `SequenceRunner` hosts main + child tracks; steps `Step_RunTrack`, `Step_WaitForTrack`, `Step_StopTrack`, `Step_Signal`.
- **Track authoring guardrails:** `SequenceParallelTrackValidation` feeds `SequenceAsset.OnValidate`; `NarrativeParallelTrackAuthoringEditor` creates starter child-track assets and validates all sequences from the Tools menu.
- **Signals:** `NarrativeSignalBus`, `ConditionSignal`.
- **Debug:** `NarrativeDebugOverlay` (add to scene; optional `SequenceRunner` **Show Debug UI**).
- **Variables:** `SequenceVariableStore.SetParentGlobalScope` + `SequenceContext.CreateChildFrom` for child track global inheritance.

*Generated as a baseline inventory for progressive narrative tooling work.*
