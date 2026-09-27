# Interactable System Setup

Reusable interaction stack for static props, doors, and NPCs.

The canonical split is:

- `NPCInteractableBinding` / `InteractableObject`: range, input, and optional availability gating
- `NarrativeQuestGuard`: whether the interaction is available
- `QuestProgressInteractionBridge`: what happens when interaction succeeds
- `QuestInteractionProfileSO` / `QuestInteractionRouterSO`: reusable interaction content and multi-quest precedence
- `QuestStateGameObjectGate` / `NarrativeRedirectZone`: world-state gating, not post-interaction routing

`InteractionBridge` is deprecated and should only appear as a migration error if stale content still references it.

---

## Components

| Component | Role |
|-----------|------|
| `IInteractable` | Interface for things the player can interact with |
| `InteractableRegistry` | Tracks interactables in range and resolves the nearest target |
| `NarrativeQuestGuard` | Shared quest + lifecycle guard for interactables, redirect zones, and scene gates |
| `InteractableObject` | Static object interaction host |
| `NPCInteractableBinding` | NPC interaction host; can also record `NPCMemoryComponent.RecordInteraction()` |
| `QuestProgressInteractionBridge` | Canonical interaction bridge: runtime override -> router slot -> default profile |
| `QuestInteractionProfileSO` | Per-quest interaction content: active rules, inactive output, active fallback |
| `QuestInteractionRouterSO` | Ordered multi-quest precedence; first matching slot wins |
| `QuestStateGameObjectGate` | Enables/disables scene objects based on `NarrativeQuestGuard` pass/fail |
| `NarrativeRedirectZone` | Optional redirect dialogue zone gated by `NarrativeQuestGuard` |
| `InteractableIndicator` | World-space prompt / bubble |
| `InteractionBridge` | Deprecated migration sentinel only |

---

## Canonical Setup

### Static object

1. Add a trigger `Collider`.
2. Add `InteractableObject`.
3. Wire `On Interacted` to `QuestProgressInteractionBridge.OnInteract`.
4. If the object should only be interactable for certain quest lifecycles, configure `Narrative Quest Guard`.
5. Add `QuestProgressInteractionBridge` on the same object or a child.
6. Assign either:
   - `Default Profile` for single-quest behavior
   - `Interaction Router` for ordered multi-quest behavior
7. Leave deprecated fallback data empty.

### NPC

1. Add a trigger `Collider` to the NPC root or an interaction child.
2. Add `NPCInteractableBinding`.
3. Wire `On Interacted` to `QuestProgressInteractionBridge.OnInteract`.
4. Assign `NPC Memory` if profile rules will use memory predicates or you want interaction timestamps/counts.
5. Configure `Narrative Quest Guard` only when the NPC should be unavailable outside certain lifecycles.
6. Add `QuestProgressInteractionBridge` and assign a `Default Profile` or `Interaction Router`.

### Door or prop with quest-specific response

Use both layers together:

- `QuestStateGameObjectGate` decides which door/interactable object is currently present or enabled.
- `QuestProgressInteractionBridge` decides what dialogue/trigger output that interactable produces.

Example:

- Locked-door collider exists only while `chainbound_intro` is active via `QuestStateGameObjectGate`
- Its `InteractableObject` stays unguarded
- Its `QuestProgressInteractionBridge` points at a minimal locked-door profile

---

## Guard vs Bridge

| Need | Use |
|------|-----|
| Object/NPC should only be interactable for certain quests or lifecycles | `NarrativeQuestGuard` |
| Interaction should always be available, but outcome changes by quest state | `QuestProgressInteractionBridge` with profile/router |
| One NPC/prop needs ordered precedence across multiple quests | `QuestInteractionRouterSO` |
| NPC/prop needs active quest rules based on objective progress or NPC memory | `QuestInteractionProfileSO` rules |
| Scene objects should appear/disappear based on quest state | `QuestStateGameObjectGate` |
| Player should be nudged away from an area based on quest state | `NarrativeRedirectZone` |

Rule of thumb:

- Guards answer `can I interact?`
- Bridges answer `what happens when I do?`

---

## Profile Authoring

`QuestInteractionProfileSO` stores one quest's interaction behavior:

- quest identity via `QuestDefinitionSO` or manual `questId`
- ordered active rules
- optional time-of-day predicates on active rules
- inactive dialogue / trigger bucket for non-active lifecycles
- active fallback dialogue / trigger when no rule matches

Use a router when one host can belong to more than one quest context.

`QuestInteractionRouterSO` resolution order is fixed:

1. first slot whose profile quest lifecycle matches the slot mask wins
2. if no slot matches, the bridge falls back to `Default Profile`
3. runtime override from `Step_NPC_SetQuestInteractionProfile` always wins over both

### Time-aware active rules

Time-of-day checks belong on the ordered active rules inside `QuestInteractionProfileSO`, so the profile remains the single source of truth for interaction selection.

Authoring notes:

- leave the rule's `Time Of Day` predicate empty to ignore time
- use `Allowed Segments` when a rule should only match during `Night`, `Dawn`, `Day`, or `Dusk`
- enable `Use Normalized Time Range` when a rule should only match within a specific inclusive window
- when both are configured, both must pass
- if `Min Normalized Time` is greater than `Max Normalized Time`, the window wraps across midnight

Example greeting setup in one active profile:

- rule 0: `Good morning!`
  `Use Normalized Time Range = true`, `Min = 0.20`, `Max = 0.35`
- rule 1: `Good evening!`
  `Use Normalized Time Range = true`, `Min = 0.70`, `Max = 0.90`
- active fallback: neutral greeting for every other active time

`QuestProgressInteractionBridge` auto-resolves `TimeOfDayManager` when no explicit time source override is assigned. Use the override only when a specific NPC or test scene needs a non-default `ITimeOfDayService`.

---

## Validation

Use `Tools/Signal Jam/Narrative/Validate Canonical Interaction Stack`.

The validator reports:

- any remaining `InteractionBridge`
- `QuestProgressInteractionBridge` instances that still contain deprecated inline data
- interactables, redirect zones, or gates that still carry deprecated single-quest fallback fields
- canonical bridges with no default profile or router assigned
- time-aware profiles that rely on scene-level `TimeOfDayManager` auto-resolution
- contradictory time predicates such as segment filters that cannot overlap their normalized time window

---

## Indicator Setup

1. Create an `Indicator` child.
2. Add world-space canvas/mesh prompt visuals as needed.
3. Add `InteractableIndicator`.
4. Assign the indicator reference on `InteractableObject` or `NPCInteractableBinding`.

---

## Current Authoring Standard

- New content should never add `InteractionBridge`.
- New content should never rely on `guardQuestId` / `questId` compatibility fields.
- Prefer separate reusable `QuestInteractionProfileSO` assets over stuffing many cases into one profile.
- Use routers for precedence, not hard-coded quest checks inside MonoBehaviours.
