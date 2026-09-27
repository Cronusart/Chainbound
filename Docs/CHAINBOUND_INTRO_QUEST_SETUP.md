# Chainbound Intro Quest â€” Full Setup Guide

> **Scope:** This guide covers everything needed to author and configure the
> `chainbound_intro` tutorial quest end-to-end in the Unity Editor.
> It builds on the existing narrative system (SequenceAsset, DialogueSequenceSO,
> SequenceTrigger, QuestService, etc.) plus three new scripts added in this pass.

---

## Required Steps vs Guardrails â€” Design Principle

Every moment in the intro falls into exactly one of these two categories.
Understanding the difference determines *where* the logic lives.

### Required Steps (GATES) `[GATE]`
> The sequence runner **blocks and waits** here. Player must do this to advance.

- Implemented as **`Step_WaitForTrigger`** or **`Step_WaitForDistance`** inside the `SequenceAsset`
- Represent genuine narrative progression gates: leaving the room, surviving the encounter, landing after the jump, talking to the Admiral, entering the lab
- Mark these clearly with a `Step_Label` above them named e.g. `[GATE] leave_house`

### Guardrails (AMBIENT) `[GUARDRAIL]`
> Fire **independently** of the sequence. Never hold up progression.
> Player can ignore them entirely and the quest still completes normally.

- Implemented as **`NarrativeRedirectZone`** (zone-based) or
  **`SequenceTrigger.OnTriggerFired` â†’ `SequenceDirector.TryPlayOnce(key, miniSeq)`** (interaction-based)
- Represent helpful nudges: interacting with the computer monitor, straying toward the tall grass, trying to go back through a locked door, approaching the ledge
- These are **never** placed inside the main `SequenceAsset` â€” they live entirely in the scene

### Why not `StepFailurePolicy.SkipAndContinue`?
That policy only fires when a step *fails* (null ref, network error, etc.). It has no mechanism for
"player chose not to do this optional action." Forcing an artificial timeout failure to skip an
optional step creates unpredictable state and pollutes the sequence log. Don't use it for guardrails.

### Labeling Convention (in SequenceAsset)
Add a `Step_Label` directly above each `Step_WaitForTrigger` with the name:
- `[GATE] leave_house` â€” required progression
- `[GATE] creature_zone`
- `[GATE] jumped_ledge`
- etc.

This makes the sequence list self-documenting and immediately clear in the inspector which
steps are genuine blockers vs. state-setting helpers that run automatically.

---

## New Scripts Added

| Script | Location | Purpose |
|---|---|---|
| `Step_SetIntroComplete` | `Scripts/Narrative/Sequences/Steps/` | Marks intro done in Supabase + GameSessionContext + QuestService in one step |
| `Step_SetQuestObjectiveProgress` | `Scripts/Narrative/Sequences/Steps/` | Advances/completes a named quest objective (reusable for all future quests) |
| `NarrativeRedirectZone` | `Scripts/Narrative/Triggers/` | Trigger-zone component that plays a redirect dialogue nudge; used to guide players away from off-path areas |

---

## Scenes Involved

| Scene | Role |
|---|---|
| `player_room` | Starting room; dog wakes up confused |
| `Loomville` | Overworld; creature encounter + ledge jump + Admiral NPC |
| `loom_lab` | Professor Toby's lab; starter selection + quest complete |

The **Main Menu** already checks `chainbound_intro_completed` from Supabase and
sets `MainMenuSequenceController2._routeDecision` to `PlayRoute.IntroPath` when
the flag is absent. It loads `IntroSequenceStartScene` â€” name that scene
`player_room` (or make `IntroSequenceStartScene` an alias in `Build Settings`).

---

## Step 1 â€” Create Dialogue Sequence Assets

Right-click in Project â†’ `Create â†’ SignalJam â†’ Narrative â†’ Dialogue Sequence`

Create **all of the following** assets in `Assets/Narrative/Dialogue/Intro/`
(create that folder if it doesn't exist).

### `Dial_Wake_Confusion`
The opening monologue. Text is **configurable** â€” just edit the lines in the Inspector.
Suggested defaults:
```
Line 1 | Speaker: (blank) | "Ugh... my head..."
Line 2 | Speaker: (blank) | "Wait â€” this doesn't look right. My room... but different."
Line 3 | Speaker: (blank) | "Everything feels off. I need to figure out what's going on."
```
`autoAdvance = false` | `allowSkip = false`

---

### `Dial_Computer_Redirect`
Plays when dog interacts with the monitor.
```
Line 1 | Speaker: (blank) | "I'd better check what's going on outside."
```
`autoAdvance = false`

---

### `Dial_RoomDoor_Blocked`
Plays if dog tries to re-enter player_room after leaving (via NarrativeRedirectZone).
```
Line 1 | Speaker: (blank) | "We've gotta find out what's going on around here."
```
`autoAdvance = true` | `autoAdvanceDelay = 2.5`

---

### `Dial_LoomvilleEntry`
First beat stepping outside.
```
Line 1 | Speaker: (blank) | "Where am I...?"
Line 2 | Speaker: (blank) | "This place looks nothing like home."
Line 3 | Speaker: (blank) | "I've gotta find some answers around here."
```
`autoAdvance = false`

---

### `Dial_WrongPath_TallGrass`
Plays when dog approaches the tall-grass path (before encounter).
```
Line 1 | Speaker: (blank) | "Maybe we should avoid that tall grass and take the other path."
```
`autoAdvance = true` | `autoAdvanceDelay = 3`

---

### `Dial_PostEncounter`
Plays immediately after the forced-flee encounter ends.
```
Line 1 | Speaker: (blank) | "I've gotta get out of here quick!"
```
`autoAdvance = false`

---

### `Dial_PostEncounter_Redirect`
Plays if dog tries to go BACK toward the tall grass after the encounter.
```
Line 1 | Speaker: (blank) | "I gotta get away from that thing â€” whatever it was."
```
`autoAdvance = true` | `autoAdvanceDelay = 3`

---

### `Dial_LedgePrompt`
Plays when dog approaches the ledge.
```
Line 1 | Speaker: (blank) | "Only way to escape is jumping down!"
```
`autoAdvance = false`

---

### `Dial_AdmiralGreeting`
Admiral's first words after falling off the ledge.
```
Line 1 | Speaker: "Admiral" | "Are you okay? That was quite a fall!"
Line 2 | Speaker: "Admiral" | "Don't see many newcomers drop in like that â€” literally."
```
`autoAdvance = false`

---

### `Dial_AdmiralConversation`
Multi-beat conversation. Player asks questions; Admiral answers and directs to lab.
```
Line 1 | Speaker: (blank)   | "W-where am I?"
Line 2 | Speaker: "Admiral" | "You're in Loomville â€” blockchain district of the Chainbound network."
Line 3 | Speaker: "Admiral" | "Strange place to wake up, I know. Happened to all of us."
Line 4 | Speaker: (blank)   | "How do I get back home?"
Line 5 | Speaker: "Admiral" | "The only person who might know is Professor Toby at the Loom Lab."
Line 6 | Speaker: "Admiral" | "Head down that road â€” you'll see it. He'll give you answers."
```
`autoAdvance = false`

---

### `Dial_FarFromLab_Redirect`
Plays when dog wanders too far from the intended path to the lab.
```
Line 1 | Speaker: (blank) | "The Professor's lab is this way. Better not get lost."
```
`autoAdvance = true` | `autoAdvanceDelay = 3`

---

### `Dial_ProfessorGreeting`
Professor Toby's intro. He's nonchalant about the whole situation.
```
Line 1 | Speaker: "Prof. Toby" | "Ah, another one. Come in, come in â€” don't scratch the floors."
Line 2 | Speaker: "Prof. Toby" | "Welcome to the Chainbound network. You've been transported here from your world."
Line 3 | Speaker: "Prof. Toby" | "Fascinating phenomenon, really. We've been studying it for years."
Line 4 | Speaker: (blank)      | "Can you send me back home?"
Line 5 | Speaker: "Prof. Toby" | "...Possibly. No promises. These things are complicated."
Line 6 | Speaker: "Prof. Toby" | "But while you're here, I could use some assistance. Purely for research, of course."
Line 7 | Speaker: "Prof. Toby" | "First things first â€” you'll need a Chainling if you're going to survive out there."
```
`autoAdvance = false`

---

### `Dial_ProfessorOrbIntro`
Professor walks player to the starter display.
```
Line 1 | Speaker: "Prof. Toby" | "These are Orb devices â€” hardware that stores Chainlings in digital form."
Line 2 | Speaker: "Prof. Toby" | "Have a look at each one. Take your time. Choose wisely."
```
`autoAdvance = false`

---

### `Dial_ProfessorPostSelection`
After the player picks a starter.
```
Line 1 | Speaker: "Prof. Toby" | "Good choice. That one's been waiting for someone like you."
Line 2 | Speaker: "Prof. Toby" | "Now â€” about that favor."
```
`autoAdvance = false`

---

### `Dial_CoffeeBeerQuest`
The Professor's outrageous request that starts the next quest.
```
Line 1 | Speaker: "Prof. Toby" | "I need 2 coffees and a beer. From the market on East Loom Street."
Line 2 | Speaker: (blank)      | "...A beer?"
Line 3 | Speaker: "Prof. Toby" | "Research is thirsty work. Don't judge me."
Line 4 | Speaker: "Prof. Toby" | "You'll find the market nearby. I'll be here when you return."
```
`autoAdvance = false`

---

## Step 2 â€” Create the Quest Definitions

### `Chainbound_Intro.asset` (already exists)
Verify `questId = "chainbound_intro"`. Fix the `targetAmount` on each objective to `1`
(the existing asset has some set to `6` or `0`, which breaks `IsComplete` logic).

| objectiveId | description | targetAmount |
|---|---|---|
| `leave_house` | Player leaves player_room | 1 |
| `escape_creature` | Player flees first encounter | 1 |
| `jump_off_ledge` | Player jumps off ledge | 1 |
| `talk_to_admiral` | Player talks to Admiral | 1 |
| `go_to_loom_lab` | Player reaches Loom Lab | 1 |
| `talk_to_toby` | Player talks to Professor Toby | 1 |

### `Quest_CoffeeAndBeer.asset` (NEW)
Create: `Assets/Narrative/Quests/Quest_CoffeeAndBeer.asset`
- **Quest ID:** `coffee_and_beer`
- **Quest Name:** "The Professor's Order"
- **Description:** "Fetch Professor Toby 2 coffees and a beer from East Loom Market."
- Objectives:
  - `fetch_coffee` | "Collect 2 coffees" | targetAmount: 2
  - `fetch_beer`   | "Collect 1 beer"   | targetAmount: 1

---

## Step 3 â€” Create the Sequence Asset

Right-click â†’ `Create â†’ SignalJam â†’ Narrative â†’ Sequence`
Name: `Sequence_ChainboundIntro`
**Sequence ID:** `chainbound_intro_sequence`
**Start Checkpoint ID:** `intro_start`

### Early name capture (sprite cinematic)

Name input happens **during the intro sprite cinematic**, not in the lab with Toby:

1. `Step_PlaySpriteCinematic` -> `IntroSpriteCinematic` (photo + `computer_upload` placeholders; `allowSkip = false`)
2. `Step_PromptNameInput` -> stores `SequenceContext.PlayerName` and `GameSessionContext.DisplayName`
3. `Step_CommitPlayerName` -> Supabase commit (`SkipAndContinue` on failure so intro does not abort)
4. `Step_PlaySpriteCinematic` -> `IntroSpriteCinematic_PostName` (`computer_mint` placeholder; `allowSkip = false`)
5. Then quest start / load `player_room` / wake dialogue as usual

Toby greetings still run later; they must not re-open name UI. `{playerName}` tokens in lab dialogue resolve from context set at cinematic time.

Add steps **in this exact order**. Each bullet is one step ScriptableObject
created inline (use the `+` button in the steps list).

---

### SEGMENT A â€” player_room

> **Guardrail note:** The computer monitor interaction is **not** a gate. If the player
> ignores the monitor and walks straight to the door, the sequence advances normally.
> Wire the computer as a `[GUARDRAIL]` in the scene â€” see Scene Setup below.

| # | Step Type | Key Settings |
|---|---|---|
| 1 | `Step_StartQuest` | questDefinition = `Chainbound_Intro.asset` |
| 2 | `Step_SetCheckpoint` | checkpointId = `intro_player_room` |
| 3 | `Step_LockInput` | channel = All, reason = "wake cutscene" |
| 4 | `Step_PlayDialogue` | dialogueSequence = `Dial_Wake_Confusion` |
| 5 | `Step_UnlockInput` | â€” |
| 6 | `Step_Label` | labelName = `[GATE] leave_house` |
| 7 | `Step_WaitForTrigger` | triggerId = `trigger_room_exit` |
| 8 | `Step_SetQuestObjectiveProgress` | questId = `chainbound_intro`, objectiveId = `leave_house`, completeObjective = true |
| 9 | `Step_SetCheckpoint` | checkpointId = `intro_leaving_room` |
| 10 | `Step_LoadScene` | sceneName = `Loomville`, spawnId = `spawn_intro_loomville_entry` |

---

### SEGMENT B â€” Loomville (upper, post-exit)

> **Guardrail note:** "Only way to escape is jumping down!" (`Dial_LedgePrompt`) is **not** a gate.
> A player who jumps without walking through the ledge-approach zone still lands and fires
> `trigger_jumped_ledge`. The nudge is wired as a `[GUARDRAIL]` `NarrativeRedirectZone`
> (`Redirect_LedgeApproach`) that is armed/disarmed by the sequence around the landing gate.

| # | Step Type | Key Settings |
|---|---|---|
| 11 | `Step_LockInput` | reason = "Loomville arrival" |
| 12 | `Step_PlayDialogue` | dialogueSequence = `Dial_LoomvilleEntry` |
| 13 | `Step_UnlockInput` | â€” |
| 14 | `Step_SetColliderState` | gameObjectName = `Blocker_PlayerRoomDoor`, enabled = true |
| 15 | `Step_EnableGameObject` | gameObjectName = `Redirect_TallGrass`, enable = true |
| 16 | `Step_Label` | labelName = `[GATE] creature_zone` |
| 17 | `Step_WaitForTrigger` | triggerId = `trigger_creature_zone` |
| 18 | `Step_EnableGameObject` | gameObjectName = `Redirect_TallGrass`, enable = false |
| 19 | `Step_StartEncounter` | encounterId = `chainbound_intro_creature`, canRun = true, forcedOutcome = ForcedRun |
| 20 | `Step_LockInput` | reason = "post encounter" |
| 21 | `Step_PlayDialogue` | dialogueSequence = `Dial_PostEncounter` |
| 22 | `Step_UnlockInput` | â€” |
| 23 | `Step_EnableGameObject` | gameObjectName = `Redirect_PostEncounter`, enable = true |
| 24 | `Step_EnableGameObject` | gameObjectName = `Redirect_LedgeApproach`, enable = true |
| 25 | `Step_SetQuestObjectiveProgress` | questId = `chainbound_intro`, objectiveId = `escape_creature`, completeObjective = true |
| 26 | `Step_SetCheckpoint` | checkpointId = `intro_escaped_creature` |
| 27 | `Step_Label` | labelName = `[GATE] jumped_ledge` |
| 28 | `Step_WaitForTrigger` | triggerId = `trigger_jumped_ledge` |
| 29 | `Step_EnableGameObject` | gameObjectName = `Redirect_LedgeApproach`, enable = false |
| 30 | `Step_EnableGameObject` | gameObjectName = `Redirect_PostEncounter`, enable = false |
| 31 | `Step_SetQuestObjectiveProgress` | questId = `chainbound_intro`, objectiveId = `jump_off_ledge`, completeObjective = true |
| 32 | `Step_SetCheckpoint` | checkpointId = `intro_jumped_ledge` |

---

### SEGMENT C â€” Loomville (lower, Admiral encounter)

| # | Step Type | Key Settings |
|---|---|---|
| 33 | `Step_EnableGameObject` | gameObjectName = `NPC_Admiral_Intro`, enable = true |
| 34 | `Step_Label` | labelName = `[GATE] admiral_arrived` |
| 35 | `Step_WaitForTrigger` | triggerId = `trigger_admiral_arrived` |
| 36 | `Step_LockInput` | reason = "Admiral dialogue" |
| 37 | `Step_PlayDialogue` | dialogueSequence = `Dial_AdmiralGreeting` |
| 38 | `Step_PlayDialogue` | dialogueSequence = `Dial_AdmiralConversation` |
| 39 | `Step_UnlockInput` | â€” |
| 40 | `Step_SetQuestObjectiveProgress` | questId = `chainbound_intro`, objectiveId = `talk_to_admiral`, completeObjective = true |
| 41 | `Step_SetCheckpoint` | checkpointId = `intro_met_admiral` |
| 42 | `Step_EnableGameObject` | gameObjectName = `Redirect_FarFromLab`, enable = true |
| 43 | `Step_Label` | labelName = `[GATE] loom_lab_entrance` |
| 44 | `Step_WaitForTrigger` | triggerId = `trigger_loom_lab_entrance` |
| 45 | `Step_EnableGameObject` | gameObjectName = `Redirect_FarFromLab`, enable = false |
| 46 | `Step_SetQuestObjectiveProgress` | questId = `chainbound_intro`, objectiveId = `go_to_loom_lab`, completeObjective = true |
| 47 | `Step_SetCheckpoint` | checkpointId = `intro_entering_lab` |
| 48 | `Step_LoadScene` | sceneName = `loom_lab`, spawnId = `spawn_lab_entry` |

---

### SEGMENT D â€” loom_lab

| # | Step Type | Key Settings |
|---|---|---|
| 49 | `Step_SetColliderState` | gameObjectName = `Blocker_LabExit`, enabled = true |
| 50 | `Step_LockInput` | reason = "Professor intro" |
| 51 | `Step_PlayDialogue` | dialogueSequence = `Dial_ProfessorGreeting` |
| 52 | `Step_PlayDialogue` | dialogueSequence = `Dial_ProfessorOrbIntro` |
| 53 | `Step_EnableGameObject` | gameObjectName = `StarterOrb_Display`, enable = true |
| 54 | `Step_UnlockInput` | â€” |
| 55 | `Step_Label` | labelName = `[GATE] starter_selection` |
| 56 | `Step_SelectStarterChainling` | useContextStarter = true |
| 57 | `Step_LockInput` | reason = "post-selection dialogue" |
| 58 | `Step_PlayDialogue` | dialogueSequence = `Dial_ProfessorPostSelection` |
| 59 | `Step_PlayDialogue` | dialogueSequence = `Dial_CoffeeBeerQuest` |
| 60 | `Step_SetQuestObjectiveProgress` | questId = `chainbound_intro`, objectiveId = `talk_to_toby`, completeObjective = true |
| 61 | `Step_StartQuest` | questDefinition = `Quest_CoffeeAndBeer.asset` |
| 62 | `Step_SetIntroComplete` | supabaseConfig = `SupabaseConfig.asset`, introQuestId = `chainbound_intro` |
| 63 | `Step_CommitFirstSave` | useContextStarter = true |
| 64 | `Step_SetCheckpoint` | checkpointId = `intro_complete` |
| 65 | `Step_SetColliderState` | gameObjectName = `Blocker_LabExit`, enabled = false |
| 66 | `Step_UnlockInput` | â€” |
| 67 | `Step_Log` | message = "Chainbound intro quest complete.", logLevel = Log |

---

## Step 4 â€” Scene Setup

### Scene: `player_room`

#### Objects to add / configure

**SpawnPoint â€” `spawn_player_room_start`**
- Add an empty GameObject named `SpawnPoint_player_room_start`
- Add your SpawnPoint component; set Spawn ID = `player_room_start`
- Place at the dog's bed / starting position

**`[GUARDRAIL]` Computer Monitor â€” `InteractableObject`**
> This is NOT in the main sequence. Uses the Interactable system (see `Scripts/Interactable/INTERACTABLE_SETUP.md`).

- On the monitor/computer GameObject:
  1. Add `Collider` (BoxCollider, Is Trigger = true)
  2. Add `InteractableObject`:
     - Trigger Mode: **RequireInteract**
     - Narrative Quest Guard: required `chainbound_intro` with lifecycle mask `Active` (optional â€” only during intro)
     - On Interacted: Wire to `SequenceDirector.TryPlayOnce("computer_redirect", Seq_ComputerRedirect)`
  3. (Optional) Add `InteractableIndicator` child for bubble + key prompt
- Create mini `SequenceAsset` `Seq_ComputerRedirect`: 1Ã— `Step_PlayDialogue` â†’ `Dial_Computer_Redirect`
- Ensure **PlayerInteractor** is on the Player (routes Interact to nearest)

**Exit Door (SequenceTrigger + SceneTransitionTrigger)**
- Place a BoxCollider trigger in the doorway
- Add **both** components to the same GameObject:
  1. **SequenceTrigger**
     - Trigger ID: `trigger_room_exit`
     - `startArmed = false` | `fireOnce = true`
  2. **SceneTransitionTrigger**
     - Target scene: `Loomville` | Spawn ID: `spawn_intro_loomville_entry` (or your spawn)
     - **Quest Gating â†’ Blocking Quest Ids:** add `chainbound_intro`
     - When `chainbound_intro` is active, this component disables itself so the sequence controls the transition via `Step_LoadScene`. When the quest completes, the door works normally on return visits.

**NarrativeRedirectZone â€” `Redirect_RoomDoor` (used AFTER leaving)**
> This zone lives in the **Loomville** scene in front of the player_room door exterior.
> See Loomville section below.

---

### Scene: `Loomville`

#### SpawnPoints

| Name | Spawn ID | Placement |
|---|---|---|
| `SpawnPoint_intro_loomville_entry` | `spawn_intro_loomville_entry` | Just outside player_room front door |
| `SpawnPoint_lower_loomville` | `spawn_lower_loomville` | Landing area at bottom of ledge |

#### Blockers (invisible wall colliders â€” disable Mesh Renderer if using MeshCollider)

**`Blocker_PlayerRoomDoor`**
- BoxCollider over the player_room door entrance, **initially disabled** (step 15 enables it)
- No Renderer needed
- A `NarrativeRedirectZone` component can go on the same GO (or a sibling) with
  `redirectDialogue = Dial_RoomDoor_Blocked` and `startArmed = false`
  (arm via step 15 or set `startArmed = true` since it only ever fires when blocked)

#### Redirect Zones (NarrativeRedirectZone components)

**`Redirect_TallGrass`** â€” wrong-path warning before encounter
- BoxCollider covering the tall-grass path opening
- `redirectDialogue = Dial_WrongPath_TallGrass`
- `startArmed = false` (enabled at step 16, disabled at step 18)
- `NarrativeQuestGuard` required clause: `chainbound_intro` with lifecycle mask `Active`
- `cooldownSeconds = 5`

**`Redirect_PostEncounter`** â€” back-toward-creature warning
- BoxCollider on the tall-grass side of the area (same zone or a new collider)
- `redirectDialogue = Dial_PostEncounter_Redirect`
- `startArmed = false` (enabled at step 23)
- `cooldownSeconds = 5`

**`Redirect_LedgeApproach`** â€” "Only way to escape is jumping down!" nudge
- BoxCollider zone at the ledge edge (triggered when player walks to the brink)
- `redirectDialogue = Dial_LedgePrompt`
- `startArmed = false` (enabled at sequence step 24, disabled at step 29 after landing)
- `cooldownSeconds = 6`

**`Redirect_FarFromLab`** â€” keeps player heading to the lab
- One or more BoxCollider zones at the perimeter of the intended path
- `redirectDialogue = Dial_FarFromLab_Redirect`
- `startArmed = false` (enabled at step 39, disabled at step 41)
- `cooldownSeconds = 8`

#### Sequence Triggers

**`trigger_creature_zone`**
- SequenceTrigger + BoxCollider at entrance to tall-grass encounter corridor
- `startArmed = false` | `fireOnce = true`

**`trigger_jumped_ledge`**
- SequenceTrigger + BoxCollider at the **landing zone** at the bottom of the ledge
- `startArmed = false` | `fireOnce = true`

**`trigger_admiral_arrived`**
- Place on the **Admiral NPC** GameObject â€” fire this via the Admiral's arrival
  animation/NavMesh callback:
  ```csharp
  TriggerRegistry.Instance.FireTrigger("trigger_admiral_arrived");
  ```
  Or use a `DistanceTrigger` if NavMesh callbacks aren't wired:
  - DistanceTrigger on a child of NPC_Admiral_Intro
  - Trigger ID: `trigger_admiral_arrived`
  - `distanceThreshold = 1.5f` | `startArmed = false` (arm when NPC is enabled at step 31)

**`trigger_loom_lab_entrance`**
- SequenceTrigger + BoxCollider at the lab building entrance
- `startArmed = false` | `fireOnce = true`

#### NPC: `NPC_Admiral_Intro`
- Start with this GameObject **disabled** (step 31 enables it)
- Assign a NavMeshAgent that navigates to the player on `OnEnable()`
- When the agent arrives (OnDestinationReached or distance < threshold), fire
  `TriggerRegistry.Instance.FireTrigger("trigger_admiral_arrived")`
- Keep the Admiral in **lower Loomville** area near the ledge landing zone

---

### Scene: `loom_lab`

#### SpawnPoint
- Name: `SpawnPoint_lab_entry` | Spawn ID: `spawn_lab_entry`
- Place just inside the lab entrance

#### Blockers

**`Blocker_LabExit`**
- BoxCollider over the lab exit/door, **initially disabled**
- Step 45 enables it (blocks going back to Loomville)
- Step 60 disables it (player can leave after quest completes)

#### Starter Orb Display

**`StarterOrb_Display`** (parent GameObject, **starts disabled**)
- Contains three child GameObjects, each with a `StarterOption` component:

| Child Name | Chainling ID | Display Name | Description |
|---|---|---|---|
| `Orb_Axion` | `axion` | "Axion" | "A defensive hardware type. Steady, reliable, built to endure." |
| `Orb_Vyre` | `vyre` | "Vyre" | "A swift processing type. Strikes fast, leaves quickly." |
| `Orb_Nullex` | `nullex` | "Nullex" | "A disruptive nullification type. Unpredictable and volatile." |

- Each `StarterOption` also needs an interactable component that opens the
  description popup **and** calls `StarterOption.Select()` on confirmation.
- `StarterSelectionService` must be present in the scene (or under GameServices).

---

## Step 5 â€” Services Checklist

Ensure the following are present in your **GameServices** (DontDestroyOnLoad) hierarchy
before the sequence starts. Check `Assets/Scenes/BOOTSTRAP_SETUP.md` for the full list.

| Service | Required by Intro Seq |
|---|---|
| `SequenceRunner` | Yes â€” drives all steps |
| `SequenceDirector` | Yes â€” for redirect mini-sequences (if used) |
| `TriggerRegistry` | Yes â€” all WaitForTrigger steps |
| `DialogueService` | Yes â€” all PlayDialogue steps |
| `QuestService` | Yes â€” StartQuest + SetQuestObjectiveProgress |
| `StarterSelectionService` | Yes â€” SelectStarterChainling step |
| `SceneTransitionManager` | Yes â€” LoadScene steps |
| `InputLockService` | Yes â€” LockInput / UnlockInput |
| `GameSessionContext` | Yes â€” SetIntroComplete reads wallet address |
| `InteractableRegistry` | Yes â€” for InteractableObject (computer monitor) |
| `PlayerInteractor` | Yes â€” on Player; routes Interact to nearest |

**Per-scene (on Canvas or scene root):**
- `DialogueUIController` (assign to `DialogueService.uiController`)
- `StarterSelectionUI` (assign to `StarterSelectionService.selectionUI`)

---

## Step 6 â€” Wire the Sequence to Main Menu

In `MainMenuSequenceController2` Inspector:
- `introSequenceAsset` â†’ `Sequence_ChainboundIntro`
- `introSequenceStartSceneName` â†’ `player_room`

The sequence auto-starts when the scene loads because `MainMenuSequenceController2`
calls `SequenceRunner.Instance.StartSequence(introSequenceAsset)` when route is `IntroPath`.

---

## Step 7 â€” Fix Chainbound_Intro.asset Objective Targets

Open `Assets/Scripts/Narrative/Quests/Quest_Types/Chainbound_Intro.asset`.
Set **each objective's `targetAmount` to `1`** (the existing values of 0 and 6 will
prevent `IsComplete` from working correctly with `Step_SetQuestObjectiveProgress`).

---

## Testing Checklist

### Functional flow
- [ ] Main Menu detects no intro_complete â†’ loads player_room
- [ ] Wake dialogue plays; input is locked during it
- [ ] Player can walk around room after dialogue
- [ ] Interacting with monitor plays redirect dialogue
- [ ] Walking to door fires room_exit trigger â†’ loads Loomville
- [ ] Loomville entry dialogue plays
- [ ] `Blocker_PlayerRoomDoor` is enabled after entry dialogue
- [ ] `Redirect_TallGrass` fires nudge when player approaches wrong path
- [ ] Walking to encounter zone starts the creature encounter
- [ ] Encounter ends with flee outcome; post-encounter dialogue plays
- [ ] `Redirect_PostEncounter` fires when player heads back toward creature
- [ ] Walking to ledge fires ledge prompt dialogue
- [ ] Jumping/falling fires `trigger_jumped_ledge` â†’ quest objective recorded
- [ ] Admiral NPC enables and walks toward player
- [ ] Admiral conversation plays in full
- [ ] `Redirect_FarFromLab` zones fire nudges when player wanders
- [ ] Walking to lab entrance fires trigger â†’ loads loom_lab
- [ ] Lab exit is blocked on entry
- [ ] Professor dialogue plays in full
- [ ] Starter Orb display enables; player can inspect each one
- [ ] Selecting a starter advances the sequence
- [ ] Post-selection + coffee/beer dialogues play
- [ ] `chainbound_intro` quest completes in QuestService
- [ ] `Quest_CoffeeAndBeer` quest starts in QuestService
- [ ] `SupabaseIntroCompleteHelper.SetIntroComplete` fires (check console)
- [ ] `GameSessionContext.IntroComplete` is true after `Step_SetIntroComplete` (step 62)
- [ ] Lab exit blocker is disabled; player can exit
- [ ] Re-loading Main Menu â†’ `chainbound_intro_completed` flag detected â†’ skips intro

### Redirect zones
- [ ] Each `NarrativeRedirectZone` respects cooldown (no dialogue spam)
- [ ] Zones correctly arm/disarm at the right sequence steps
- [ ] Zones do not interrupt active sequence dialogue (DialogueService.IsPlaying check)

---

## Troubleshooting

**Sequence doesn't start after Main Menu loads player_room:**
- Check `SequenceRunner` exists in GameServices
- Verify `MainMenuSequenceController2.introSequenceAsset` is assigned
- Enable `showDebugUI` on `SequenceRunner`

**Triggers don't fire:**
- Trigger IDs must match **exactly** (case-sensitive)
- `Step_WaitForTrigger.armTrigger = true` will auto-arm; set to false only if the
  trigger should be armed earlier in the sequence
- Ensure player GameObject has the `Player` tag

**`Step_SetIntroComplete` skips Supabase sync:**
- Assign `SupabaseConfig.asset` to the step's `supabaseConfig` field
- Ensure `GameSessionContext.WalletAddress` is populated (wallet connected in Main Menu)

**`Step_SetQuestObjectiveProgress` reports "Quest not active":**
- `Step_StartQuest` (step 1) must appear before any objective progress steps
- Check that `Chainbound_Intro.asset` questId = `"chainbound_intro"` exactly

**Starter selection hangs:**
- Ensure `StarterSelectionService` is in the scene
- Ensure each `StarterOption` child calls `StarterOption.Select()` on player confirm
- Check that `Step_SelectStarterChainling.useContextStarter = true`

**Professor dialogue / lab door never unlocks:**
- Confirm `Blocker_LabExit` GameObject name matches the string in `Step_SetColliderState` exactly (step 45 and 60)

