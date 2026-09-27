# Interaction Guards, Multi-Quest Routing & Parallel Narrative Tracks — Problem & Proposed Direction

**Purpose:** Single reference for analysis and refinement before formal planning. Describes the **current scaling bottleneck**, **player-facing symptoms**, and a **systems-first** direction using common RPG/narrative production terms.

---

## 1. Context (how the game layers narrative)

- **Main quest line:** Primary story progression; often long-running and always (or often) **active** during a chapter or act.
- **Side quests / optional content:** Secondary objectives that may run **concurrently** with the main line—same hub, same NPCs, overlapping time windows.
- **Interaction pipeline:** Player enters range → **availability** (can they press Interact?) → **routing** (which dialogue, triggers, or follow-up systems fire).
- **Parallel narrative:** Multiple scripted beats that should coexist—e.g. **ambient barks**, **optional side scenes**, or **non-blocking tracks** while the **main sequence** (authoritative runner) continues.

Signal Jam 3D already separates **quest lifecycle** (multiple quests can be `Active` by id) from **sequence execution** (main `SequenceRunner` plus **child tracks** via `Step_RunTrack`). The gap is **authoring and gating at the interaction layer**, not the existence of multi-quest runtime state.

---

## 2. Problem A — Single-quest interaction gates (scaling / “content debt”)

### 2.1 What exists today (technical)

Several scene components use a **single string** `guardQuestId` (or equivalent “one quest id” check):

- **NPC / object interactable binding:** Interaction is **armed** only while that **one** quest id is active.
- **Similar pattern** on other interactables and narrative triggers (e.g. redirect zones).

Separately, **quest-aware dialogue bridges** resolve **one** “primary” quest per interaction: if that quest is not active, the flow goes down an **inactive** branch; if active, **ordered rules** (objectives, NPC memory) choose outputs. That model is strong for **one quest per NPC configuration**, but it does not natively express **priority across multiple simultaneous active quests**.

### 2.2 Industry-framed issue

| Term | Meaning here |
|------|----------------|
| **Hard gate / availability predicate** | “You may only interact when quest X is active.” |
| **Content scaling** | Adding future main chapters, side arcs, and event quests without duplicating GameObjects or bespoke components per combination. |
| **Combinatorial gating** | Need for “available if **main OR side A OR side B** is active (with optional phase checks)” without N duplicate NPCs. |
| **Dialogue / interaction routing** | Which **branch** fires when **more than one** relevant quest is active—**precedence** (e.g. side beats main for this NPC until complete). |

**Symptom for designers:** To support “this NPC matters for main **and** later for side quest Y,” today’s single-id guard forces either:

- **No guard** (interaction always on—risk of wrong lines or spoilers), or  
- **Wrong exclusivity** (side content invisible while main is the only id checked), or  
- **Workarounds** (duplicate NPCs, extra components, runtime hacks)—**technical debt** and **inconsistent player experience**.

**Symptom for production:** Each new quest wave increases **scene object proliferation** and **one-off wiring** instead of **data-only** updates (new slots, new rules, reused prefabs).

### 2.3 Root cause (concise)

**Availability** and **routing** are modeled as **scalar** (one quest id) where RPG pipelines typically need **vector** configuration: **lists**, **priorities**, and **optional predicates** (quest phase, objective state, world flags, arc membership).

---

## 3. Proposed direction A — Data-driven guards and multi-slot routing

### 3.1 Split responsibilities (industry alignment)

1. **Availability profile (gate)**  
   - Answers: *Is the interact affordance shown / allowed?*  
   - Config: e.g. **ANY** active among quest ids, **ALL** required, optional **world flags**, optional **exclusions** (do not offer if quest Z complete), etc.  
   - **Single reusable asset or struct** consumed by all interactable types that currently take one string.

2. **Interaction / dialogue router (priority stack)**  
   - Answers: *Given the player pressed Interact, which content runs?*  
   - Config: **ordered slots**—each slot binds to a **quest id** (or quest definition reference) and reuses existing **rule lists** (objective checks, NPC memory predicates, dialogue/trigger outputs).  
   - Evaluation: walk slots in order; **first slot whose quest is active and whose rules match** wins; then **fallback** (generic idle, bark, or “wrong time” line).

3. **Preserve existing assets**  
   - Current **per-quest profiles** (e.g. `QuestInteractionProfileSO`) remain valid as **building blocks** inside each slot, avoiding a rewrite of all authored content.

4. **Optional runtime handoffs**  
   - Keep **runtime profile override** (sequence-driven) for **scripted swaps**; the router is the **default** resolution when multiple quests overlap.

### 3.2 Player- and designer-facing outcomes

- **Concurrent quests:** NPCs remain interactable under explicit, **reviewable** rules—no silent loss of side content because only the main quest id was wired.  
- **Precedence:** Clear **priority order** (e.g. active side quest lines override “main chapter idle”).  
- **Scaling:** New quests add **data rows**, not new component types per NPC.

---

## 4. Problem B — Parallel scripted beats vs main-line integrity

### 4.1 What exists today (technical)

- **Main sequence** on `SequenceRunner` is authoritative; **child tracks** start via `Step_RunTrack` with **blocking** vs **non-blocking** modes and **cancel policies** (e.g. stop when parent ends).  
- **Failure modes** are often **authoring errors**: second **full** play on the main director preempting the intro, **duplicate child `trackId`s**, or blocking/wait mismatches—**not** missing low-level APIs.

### 4.2 Industry-framed issue

| Term | Meaning here |
|------|----------------|
| **Parallel / concurrent beats** | Side content running alongside the **critical path** without **stealing** the main **cutscene** or **scripted sequence**. |
| **Non-blocking track** | Child narrative that **does not** hold the main timeline (ambient, optional, fire-and-forget with explicit cleanup). |
| **Authoring guardrails** | Conventions + tooling so **designers** don’t accidentally **abort** or **stall** the main quest sequence. |
| **Template packages** | Pre-wired **asset bundles** (main stub + side sequence + `RunTrack` steps + recommended labels / wait / signal steps) for repeatable **side quest** setup. |

**Symptom:** Parallel content feels **fragile** because correctness depends on **tribal knowledge** (unique track ids, which steps are safe on children, when to use signals/cancel)—easy to regress as the team grows.

### 4.3 Root cause (concise)

**Runtime supports parallel tracks;** the missing piece is **standardized authoring** and **validation** so parallel packages are **repeatable** and **self-documenting**.

---

## 5. Proposed direction B — Editor templates and validation (“parallel packages”)

1. **Template assets (or wizard)**  
   - One-click (or menu-driven) creation of a **side quest parallel package**: e.g. dedicated **child sequence** asset(s), pre-placed **`Step_RunTrack`** with **non-blocking** default, **unique `trackId` naming convention**, default **cancel policy**, optional **wait-for-track** or **signal** steps where the template documents the **contract**.

2. **Validators**  
   - **Sequence / project checks:** duplicate `trackId`, empty ids, risky combinations (e.g. blocking child where policy says non-blocking), references to patterns that **preempt** main director when disallowed.  
   - Align with existing **step identity** tooling (e.g. execute-once GUID hygiene) as part of the same **quality bar**.

3. **Systems-first fit**  
   - Tooling **generates and checks ScriptableObjects**—no story logic in editor code beyond **structure and policy defaults**.

---

## 6. Success criteria (for refinement / planning)

- **Gating:** Interactables and narrative triggers can express **multi-quest** (and optionally **flag / arc**) availability **without** duplicating components per quest combination.  
- **Routing:** When multiple quests are active, **one predictable precedence model** chooses dialogue/triggers; fallback paths are explicit.  
- **Parallel authoring:** New side content can be spawned from a **template** with **documented defaults** and **automated warnings** for common mistakes.  
- **Migration:** Existing scenes can adopt **incrementally** (e.g. new guard asset + router alongside legacy fields until deprecated).

---

## 7. Open questions (for subagent / design pass)

- **Precedence policy:** Global team rule vs per-NPC override; tie-break when two side quests both match.  
- **Inactive / completed semantics:** Whether “completed main + active side” needs different **availability** than “active main + active side.”  
- **Spoilers:** Whether gates need **anti-spoiler** rules (quest Z must be **not** started) in the same asset family.  
- **Arc / taxonomy:** Whether **NarrativeArcSO** (or tags) should be first-class in guards/routers vs **quest id lists only** for v1.  
- **Template scope:** Minimum viable template (one child track) vs full package (main checkpoint labels + side + signals + failure policy copy).

---

*Document version: draft for refinement. Implementation planning should follow after subagent analysis and stakeholder sign-off on precedence and migration strategy.*
