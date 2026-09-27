# New Game Intro Sequence - Unity Setup Guide

**Purpose:** Step-by-step guide for authoring the New Game intro sequence using existing narrative systems.

**Audience:** Unity developers/designers who need to wire content together.

**Prerequisites:** All sequence systems are implemented. This guide covers content authoring only.

---

## 1. Prerequisites Checklist

Before starting, verify these exist:

### Required Services (Bootstrap Scene or DontDestroyOnLoad)
- [ ] `InputLockService` - Singleton in scene
- [ ] `DialogueService` - Singleton in scene
- [ ] `CameraShotController` - Singleton in scene
- [ ] `SceneTransitionManager` - Singleton in scene
- [ ] `SpawnService` - Singleton in scene
- [ ] `TriggerRegistry` - Singleton in scene
- [ ] `SequenceRunner` - Singleton in scene (enable `showDebugUI` for testing)
- [ ] `SequenceDirector` - Singleton in scene (game-wide entry point)
- [ ] `LocalSaveServiceStub` - Singleton in scene
- [ ] `EncounterServiceStub` - Singleton in scene
- [ ] `StarterSelectionService` - Singleton in scene
- [ ] `QuestService` - Singleton in scene

**Tip:** Create a "GameServices" GameObject in your bootstrap scene with all services. Mark it `DontDestroyOnLoad`.

### UI Components (Per-Scene or Persistent Canvas)
- [ ] `DialogueUIController` - On Canvas with dialogue panel, text component, speaker name text
- [ ] `NameInputUI` - On Canvas with input field, confirm button
- [ ] `StarterSelectionUI` - On Canvas with selection panel, option container

### Player & Camera Setup
- [ ] Player GameObject tagged "Player"
- [ ] `PlayerMovement3D` component on player
- [ ] `TopDownCameraController3D` in scene
- [ ] Camera follows player correctly

### Scene Files
- [ ] `Bedroom.unity` scene exists
- [ ] `Loomville.unity` scene exists
- [ ] `SolanaLabs.unity` scene exists

---

## 2. Scene Preparation

### 2.1 Bedroom Scene Setup

#### Step 1: Add Spawn Point
1. Create empty GameObject: `SpawnPoint_bedroom_start`
2. Position at player start location (e.g., bed position)
3. Add component: `SpawnPoint`
4. Set `spawnId = "bedroom_start"`
5. Set `spawnRotationY` to desired facing direction (e.g., 0°)

**Validation:** In Play mode, `SpawnService` should log: "Registered spawn point: bedroom_start"

#### Step 2: Add Exit Door Trigger
1. Find or create door GameObject
2. Add `Collider` component (BoxCollider recommended)
3. **Important:** Set `Is Trigger = true`
4. Add component: `SequenceTrigger`
5. Set `triggerId = "bedroom_exit_door"`
6. Set `startArmed = false` (sequence will arm it)
7. Set `fireOnce = true`
8. Set `fireOnEnter = true`
9. Set `playerTag = "Player"`

**Validation:** Trigger should NOT fire when player touches it (not armed yet)

### Step 3: Add Boundary Colliders (Optional)
1. Create empty GameObject: `BedroomBoundaries`
2. Add colliders around room edges to prevent leaving before exit door
3. These will be disabled by sequence after dialogue completes
4. **Note:** Colliders should NOT be triggers - they block movement

#### Step 4: Add Camera Focus Target
1. Create empty GameObject: `bedroom_player`
2. Position at player spawn location
3. This will be referenced by camera shot preset

**Scene Checklist:**
- [ ] SpawnPoint with ID `bedroom_start` exists
- [ ] SequenceTrigger with ID `bedroom_exit_door` exists
- [ ] Trigger is NOT armed by default
- [ ] Trigger collider is set to "Is Trigger"
- [ ] Boundary colliders exist (optional, for restricting movement)

---

### 2.2 Loomville Scene Setup

#### Step 1: Add Entry Spawn Point
1. Create: `SpawnPoint_loomville_from_bedroom`
2. Position at exit from bedroom (door location)
3. Add `SpawnPoint` component
4. Set `spawnId = "loomville_from_bedroom"`

#### Step 2: Add Path Trigger
1. Create GameObject at path location (where player should go)
2. Add `Collider` (trigger)
3. Add `SequenceTrigger` component
4. Set `triggerId = "loomville_path_reached"`
5. Set `startArmed = false`
6. Set `fireOnce = true`

#### Step 3: Add Path Camera Focus Target
1. Create empty GameObject: `loomville_path_focus`
2. Position at path location
3. This will be used by camera pan shot

#### Step 4: Add Boundary Colliders (Restrict Movement)
1. Create empty GameObject: `LoomvilleBoundaries_ToPath`
2. Add colliders blocking routes EXCEPT the path
3. These will be enabled/disabled by sequence
4. **Note:** Use regular colliders (not triggers) to block movement

#### Step 5: Add Wrong Direction Triggers
1. Create GameObjects at blocked routes
2. Add `Collider` (trigger) covering blocked area
3. Add `SequenceTrigger` component
4. Set `triggerId = "loomville_wrong_direction"` (or unique IDs per area)
5. Set `startArmed = false`
6. Set `fireOnce = false` (can fire multiple times)

#### Step 6: Add Ledge Jump Trigger
1. Create GameObject at ledge location
2. Add `Collider` (trigger) at ledge edge
3. Add `SequenceTrigger` component
4. Set `triggerId = "loomville_ledge_jump"`
5. Set `startArmed = false`
6. Set `fireOnce = true`

#### Step 7: Add Landing Zone Trigger
1. Create GameObject below ledge (where player lands)
2. Add `Collider` (trigger) covering landing area
3. Add `SequenceTrigger` component
4. Set `triggerId = "loomville_landing_zone"`
5. Set `startArmed = false`
6. Set `fireOnce = true`

#### Step 8: Add Ledge Camera Focus Target
1. Create empty GameObject: `loomville_ledge_focus`
2. Position at ledge location
3. This will be used by camera pan shot

#### Step 9: Add Lab Camera Focus Target
1. Create empty GameObject: `loomville_lab_focus`
2. Position at Solana Labs building (visible from landing area)
3. This will be used by camera pan shot

#### Step 10: Add Lab Entry Trigger
1. Create GameObject at Solana Labs entrance
2. Add `Collider` (trigger)
3. Add `SequenceTrigger` component
4. Set `triggerId = "enter_solana_labs"`
5. Set `startArmed = false`
6. Set `fireOnce = true`

#### Step 11: Add Final Boundary Colliders
1. Create empty GameObject: `LoomvilleBoundaries_Final`
2. Add colliders around Loomville perimeter
3. These keep player in Loomville until lab is entered
4. Enable after NPC dialogue completes

**Scene Checklist:**
- [ ] SpawnPoint `loomville_from_bedroom` exists
- [ ] SequenceTrigger `loomville_path_reached` exists
- [ ] SequenceTrigger `loomville_wrong_direction` exists (or multiple)
- [ ] SequenceTrigger `loomville_ledge_jump` exists
- [ ] SequenceTrigger `loomville_landing_zone` exists
- [ ] SequenceTrigger `enter_solana_labs` exists
- [ ] Camera focus targets exist (path, ledge, lab)
- [ ] Boundary colliders exist (to path, final boundaries)
- [ ] All triggers start unarmed

---

### 2.3 Solana Labs Scene Setup

#### Step 1: Add Entry Spawn Point
1. Create: `SpawnPoint_labs_entry`
2. Position at lab entrance
3. Add `SpawnPoint` component
4. Set `spawnId = "labs_entry"`

#### Step 2: Place Professor NPC
1. Create GameObject: `Professor`
2. Position where dialogue should occur
3. **Important:** Do NOT add dialogue logic to NPC
4. Dialogue is triggered by sequence, not NPC interaction
5. Add visual representation (sprite/model) if needed

#### Step 3: Add Starter Ledger Objects
1. Create 3 GameObjects: `StarterLedger_1`, `StarterLedger_2`, `StarterLedger_3`
2. Position where starter selection should occur
3. Add `StarterOption` component to each:
   - **Ledger 1:**
     - `chainlingId = "starter_fire"`
     - `displayName = "Fire Chainling"`
     - `description = "A fiery companion"
   - **Ledger 2:**
     - `chainlingId = "starter_water"`
     - `displayName = "Water Chainling"`
     - `description = "A watery companion"
   - **Ledger 3:**
     - `chainlingId = "starter_earth"`
     - `displayName = "Earth Chainling"`
     - `description = "An earthy companion"

**Validation:** In Play mode, `StarterSelectionService` should log: "Registered option: starter_fire" (x3)

**Scene Checklist:**
- [ ] SpawnPoint `labs_entry` exists
- [ ] Professor GameObject exists (no logic)
- [ ] 3 StarterOption components exist with unique chainling IDs
- [ ] StarterSelectionUI exists on Canvas

---

## 3. Creating Dialogue Assets

### Step 1: Create Bedroom Wake Monologue

1. Right-click in Project → `Create` → `SignalJam` → `Narrative` → `Dialogue Sequence`
2. Name: `Dialogue_BedroomWake`
3. Configure:
   - `autoAdvance = false` (wait for input)
   - `allowSkip = true`
   - `skipToEndOfLine = true`
4. Add lines (click `+` in Lines list):
   - **Line 1:**
     - `speakerName = ""` (empty = narrator/internal monologue)
     - `text = "Where am I?"`
     - `typeSpeed = 30` (characters per second)
   - **Line 2:**
     - `speakerName = ""`
     - `text = "This room... it looks like my old room, but it's not quite the same."`
     - `typeSpeed = 30`
   - **Line 3:**
     - `speakerName = ""`
     - `text = "I need to get out of here and find out what's going on."`
     - `typeSpeed = 30`

**Best Practice:** Keep intro dialogue short (2-3 lines per beat)

### Step 2: Create Loomville Entry Monologue

1. Create: `Dialogue_LoomvilleEntry`
2. Configure:
   - `autoAdvance = false`
   - `allowSkip = true`
3. Add lines:
   - **Line 1:**
     - `speakerName = ""`
     - `text = "I don't recognize this place at all..."`
   - **Line 2:**
     - `speakerName = ""`
     - `text = "Wait, there's a small path off to the side of the house."`
   - **Line 3:**
     - `speakerName = ""`
     - `text = "Maybe I should head that way."`

### Step 3: Create Wrong Direction Dialogue

1. Create: `Dialogue_WrongDirection`
2. Configure:
   - `autoAdvance = false`
   - `allowSkip = true`
3. Add lines:
   - **Line 1:**
     - `speakerName = ""`
     - `text = "I shouldn't go there. I should follow the path."`

### Step 4: Create Post-Encounter Dialogue

1. Create: `Dialogue_PostEncounter`
2. Configure:
   - `autoAdvance = false`
   - `allowSkip = true`
3. Add lines:
   - **Line 1:**
     - `speakerName = ""`
     - `text = "That was terrifying! I need to get away from here."`
   - **Line 2:**
     - `speakerName = ""`
     - `text = "There's a grassy area over there... I can see a ledge I could jump from."`

### Step 5: Create Ledge Jump Dialogue

1. Create: `Dialogue_LedgeJump`
2. Configure:
   - `autoAdvance = false`
   - `allowSkip = true`
3. Add lines:
   - **Line 1:**
     - `speakerName = ""`
     - `text = "That jump is pretty high... but I have to get away from that creature chasing me."`
   - **Line 2:**
     - `speakerName = ""`
     - `text = "I'll have to jump."`

### Step 6: Create NPC Greeting Dialogue

1. Create: `Dialogue_NPCGreeting`
2. Configure:
   - `autoAdvance = false`
   - `allowSkip = true`
3. Add lines:
   - **Line 1:**
     - `speakerName = "NPC"` (or NPC name)
     - `text = "Are you okay? That was quite a fall!"`
   - **Line 2:**
     - `speakerName = ""` (player response)
     - `text = "I... I think so. Where am I?"`
   - **Line 3:**
     - `speakerName = "NPC"`
     - `text = "You're in Loomville. You look confused - are you new here?"`
   - **Line 4:**
     - `speakerName = ""`
     - `text = "I don't remember how I got here. What's going on?"`
   - **Line 5:**
     - `speakerName = "NPC"`
     - `text = "You should head to the lab. They might have answers for you."`
   - **Line 6:**
     - `speakerName = ""`
     - `text = "The lab? Where is that?"`
   - **Line 7:**
     - `speakerName = "NPC"`
     - `text = "It's that way. You can't miss it."`

### Step 4: Create Professor Dialogue

1. Create: `Dialogue_ProfessorGreeting`
2. Configure:
   - `autoAdvance = false`
3. Add lines:
   - `speakerName = "Professor"`, `text = "Welcome to Solana Labs!"`
   - `speakerName = "Professor"`, `text = "Before we begin, what should I call you?"`
   - (Name input step happens here - no dialogue line needed)
   - `speakerName = "Professor"`, `text = "Nice to meet you, {PlayerName}!"` (variable will be replaced)
   - `speakerName = "Professor"`, `text = "Now, choose your starter Chainling..."`

**Note:** `{PlayerName}` is replaced at runtime by `Step_Log` and dialogue system. For now, use placeholder text.

**Dialogue Checklist:**
- [ ] `Dialogue_BedroomWake` created with 3 lines
- [ ] `Dialogue_LoomvilleEntry` created with 3 lines
- [ ] `Dialogue_WrongDirection` created
- [ ] `Dialogue_PostEncounter` created with 2 lines
- [ ] `Dialogue_LedgeJump` created with 2 lines
- [ ] `Dialogue_NPCGreeting` created with 7 lines
- [ ] `Dialogue_ProfessorGreeting` created with professor lines (for lab scene)
- [ ] All dialogues have `autoAdvance = false`
- [ ] All dialogues have `allowSkip = true`

---

## 4. Creating Camera Shot Presets

### Step 1: Create Bedroom Wake Shot (Zoom In)

1. Right-click → `Create` → `SignalJam` → `Camera` → `Camera Shot Preset`
2. Name: `Shot_BedroomWake`
3. Configure:
   - `targetTransformId = "bedroom_player"` (or leave empty, use world position)
   - `useWorldPosition = false` (if using transform ID)
   - `targetPosition = (0, 0, 0)` (if using world position - set to player spawn)
   - `duration = 2f`
   - `holdTime = 1f` (hold at target after reaching)
   - `targetZoom = 3f` (zoomed in)
   - `targetPitch = -1` (don't change pitch)
   - `offset = (0, 0, 0)`
   - `lookAtTarget = true`
   - `smoothRotation = true`
   - `easingCurve = AnimationCurve.EaseInOut(0,0,1,1)`

**Tip:** Test shot in Play mode by calling `CameraShotController.Instance.PlayShot(shotPreset)`

### Step 2: Create Bedroom Zoom Out Shot

1. Create: `Shot_BedroomZoomOut`
2. Configure:
   - `targetTransformId = "bedroom_center"` (or world position)
   - `duration = 1.5f`
   - `holdTime = 0f`
   - `targetZoom = 7f` (normal zoom)
   - `lookAtTarget = false` (maintain current rotation)

### Step 3: Create Pan to Path Shot

1. Create: `Shot_PanToPath`
2. Configure:
   - `targetTransformId = "loomville_path_focus"`
   - `duration = 2f`
   - `holdTime = 1f`
   - `targetZoom = -1` (don't change zoom)
   - `lookAtTarget = true`
   - `smoothRotation = true`

### Step 4: Create Pan to Ledge Shot

1. Create: `Shot_PanToLedge`
2. Configure:
   - `targetTransformId = "loomville_ledge_focus"`
   - `duration = 2f`
   - `holdTime = 1f`
   - `targetZoom = -1`
   - `lookAtTarget = true`
   - `smoothRotation = true`

### Step 5: Create Pan to Lab Shot

1. Create: `Shot_PanToLab`
2. Configure:
   - `targetTransformId = "loomville_lab_focus"`
   - `duration = 2f`
   - `holdTime = 1f`
   - `targetZoom = -1`
   - `lookAtTarget = true`
   - `smoothRotation = true`

**Camera Shot Checklist:**
- [ ] `Shot_BedroomWake` created (zoom in)
- [ ] `Shot_BedroomZoomOut` created (zoom out)
- [ ] `Shot_PanToPath` created (pan to path)
- [ ] `Shot_PanToLedge` created (pan to ledge)
- [ ] `Shot_PanToLab` created (pan to lab)
- [ ] All shots have valid target IDs or world positions
- [ ] Easing curves are set (default EaseInOut is fine)

---

## 5. Authoring the NewGame_Intro SequenceAsset

### Step 1: Create Sequence Asset

1. Right-click → `Create` → `SignalJam` → `Narrative` → `Sequence`
2. Name: `Sequence_NewGameIntro`
3. Configure:
   - `sequenceId = "new_game_intro"`
   - `startCheckpointId = "intro_start"`

**Note:** This sequence uses the new control flow features including labels, branching, and checkpoints. See `SEQUENCE_AUTHORING_GUIDE.md` for details on these features.

### Step 2: Add Steps (In Order)

Add steps to the `Steps` list. Order matters! This sequence uses labels and branching for better organization.

#### Segment 1: Bedroom Wake Sequence (Steps 1-10)

**Step 1: Label - Bedroom Start**
- Add: `Step_Label`
- Set `labelName = "bedroom_start"`

**Step 2: Set Checkpoint**
- Add: `Step_SetCheckpoint`
- Set `checkpointId = "bedroom_wake"`
- Set `checkpointLabel = "bedroom_start"`
- Set `saveToState = true`

**Step 3: Load Bedroom Scene**
- Add: `Step_LoadScene`
- Set `sceneName = "Bedroom"`
- Set `spawnId = "bedroom_start"`

**Step 4: Lock Input**
- Add: `Step_LockInput`
- Set `channel = All`
- Set `reason = "Bedroom wake sequence"`
- Set `executeOnce = false`

**Step 5: Play Wake Camera Shot (Zoom In)**
- Add: `Step_PlayCameraShot`
- Assign `shotPreset = Shot_BedroomWake`
- Leave `targetTransformId` empty (uses preset's target)

**Step 6: Play Bedroom Dialogue**
- Add: `Step_PlayDialogue`
- Assign `dialogueSequence = Dialogue_BedroomWake`

**Step 7: Zoom Out Camera**
- Add: `Step_PlayCameraShot`
- Assign `shotPreset = Shot_BedroomZoomOut`

**Step 8: Restore Camera Follow**
- Add: `Step_RestoreCameraFollow`

**Step 9: Unlock Input**
- Add: `Step_UnlockInput`

**Step 10: Wait for Exit Door**
- Add: `Step_WaitForTrigger`
- Set `triggerId = "bedroom_exit_door"`
- Set `armTrigger = true` (important!)

**Step 11: Load Loomville**
- Add: `Step_LoadScene`
- Set `sceneName = "Loomville"`
- Set `spawnId = "loomville_from_bedroom"`

#### Segment 2: Loomville Entry (Steps 12-18)

**Step 12: Label - Loomville Entry**
- Add: `Step_Label`
- Set `labelName = "loomville_entry"`

**Step 13: Set Checkpoint**
- Add: `Step_SetCheckpoint`
- Set `checkpointId = "loomville_entry"`
- Set `checkpointLabel = "loomville_entry"`
- Set `saveToState = true`

**Step 14: Lock Input**
- Add: `Step_LockInput`
- Set `channel = All`
- Set `reason = "Loomville entry dialogue"`

**Step 15: Play Entry Dialogue**
- Add: `Step_PlayDialogue`
- Assign `dialogueSequence = Dialogue_LoomvilleEntry`

**Step 16: Pan Camera to Path**
- Add: `Step_PlayCameraShot`
- Assign `shotPreset = Shot_PanToPath`
- Set `targetTransformId = "loomville_path_focus"`

**Step 17: Restore Camera Follow**
- Add: `Step_RestoreCameraFollow`

**Step 18: Unlock Input**
- Add: `Step_UnlockInput`

#### Segment 3: Path Navigation with Colliders (Steps 19-25)

**Step 19: Enable Boundary Colliders**
- Add: `Step_SetColliderState`
- Set `gameObjectName = "LoomvilleBoundaries_ToPath"`
- Set `enabled = true`
- Set `affectChildren = true`

**Step 20: Arm Wrong Direction Triggers**
- **Note:** Wrong direction triggers should be armed here
- Create `SequenceTrigger` components covering wrong direction areas
- Set trigger IDs (e.g., `"loomville_wrong_direction"`)
- Set `startArmed = false`, `fireOnce = false` (can fire multiple times)
- **Important:** Configure triggers to call `TriggerRegistry.FireTrigger("wrong_direction")` when fired
- **Note:** See Section 7 for detailed wrong direction handling setup

**Step 21: Label - Wait for Path**
- Add: `Step_Label`
- Set `labelName = "wait_for_path"`

**Step 22: Wait for Path Trigger**
- Add: `Step_WaitForTrigger`
- Set `triggerId = "loomville_path_reached"`
- Set `armTrigger = true`
- **Note:** Wrong direction is handled via event subscription (see Section 7)

**Step 23: Label - Wrong Direction Handler**
- Add: `Step_Label`
- Set `labelName = "wrong_direction"`

**Step 24: Lock Input**
- Add: `Step_LockInput`
- Set `channel = All`
- Set `reason = "Wrong direction warning"`

**Step 25: Play Wrong Direction Dialogue**
- Add: `Step_PlayDialogue`
- Assign `dialogueSequence = Dialogue_WrongDirection`

**Step 26: Pan Camera to Path Again**
- Add: `Step_PlayCameraShot`
- Assign `shotPreset = Shot_PanToPath`

**Step 27: Restore Camera Follow**
- Add: `Step_RestoreCameraFollow`

**Step 28: Unlock Input**
- Add: `Step_UnlockInput`

**Step 29: Jump Back to Wait**
- Add: `Step_JumpToLabel`
- Set `targetLabel = "wait_for_path"`

**Step 30: Disable Boundary Colliders**
- Add: `Step_SetColliderState`
- Set `gameObjectName = "LoomvilleBoundaries_ToPath"`
- Set `enabled = false`
- Set `affectChildren = true`

#### Segment 4: First Encounter (Steps 32-38)

**Step 32: Lock Input**
- Add: `Step_LockInput`
- Set `channel = All`
- Set `reason = "First encounter"`

**Step 33: Start Encounter**
- Add: `Step_StartEncounter`
- Set `encounterId = "first_encounter"`
- Set `canRun = true`
- Set `canCapture = false`
- Set `forcedOutcome = ""` (let player choose - but UI only shows Run)
- Set `outcomeVariableName = "first_encounter_outcome"`
- **Note:** Encounter service should restrict UI to Run option only

**Step 34: Set Variable - Encounter Outcome**
- Add: `Step_SetVariable`
- Set `variableName = "encounter_completed"`
- Set `variableValue = "true"`
- Set `variableType = Bool`
- Set `variableScope = Local`

**Step 35: Play Post-Encounter Dialogue**
- Add: `Step_PlayDialogue`
- Assign `dialogueSequence = Dialogue_PostEncounter`

**Step 36: Pan Camera to Ledge**
- Add: `Step_PlayCameraShot`
- Assign `shotPreset = Shot_PanToLedge`
- Set `targetTransformId = "loomville_ledge_focus"`

**Step 37: Restore Camera Follow**
- Add: `Step_RestoreCameraFollow`

**Step 38: Unlock Input**
- Add: `Step_UnlockInput`

#### Segment 5: Ledge Jump Sequence (Steps 39-51)

**Step 39: Enable Boundary Colliders (To Ledge)**
- Add: `Step_SetColliderState`
- Set `gameObjectName = "LoomvilleBoundaries_ToLedge"` (create this GameObject)
- Set `enabled = true`
- Set `affectChildren = true`

**Step 40: Wait for Ledge Jump Trigger**
- Add: `Step_WaitForTrigger`
- Set `triggerId = "loomville_ledge_jump"`
- Set `armTrigger = true`

**Step 41: Lock Input**
- Add: `Step_LockInput`
- Set `channel = All`
- Set `reason = "Ledge jump dialogue"`

**Step 42: Play Ledge Jump Dialogue**
- Add: `Step_PlayDialogue`
- Assign `dialogueSequence = Dialogue_LedgeJump`

**Step 43: Unlock Input (Allow Jump)**
- Add: `Step_UnlockInput`
- **Note:** Player needs movement to jump, but dialogue should instruct to press jump button

**Step 44: Wait for Landing Zone**
- Add: `Step_WaitForTrigger`
- Set `triggerId = "loomville_landing_zone"`
- Set `armTrigger = true`

**Step 45: Lock Input**
- Add: `Step_LockInput`
- Set `channel = All`
- Set `reason = "NPC dialogue"`

**Step 46: Play NPC Greeting Dialogue**
- Add: `Step_PlayDialogue`
- Assign `dialogueSequence = Dialogue_NPCGreeting`
- **Note:** Dialogue includes player response options (handled by dialogue system)

**Step 47: Pan Camera to Lab**
- Add: `Step_PlayCameraShot`
- Assign `shotPreset = Shot_PanToLab`
- Set `targetTransformId = "loomville_lab_focus"`

**Step 48: Restore Camera Follow**
- Add: `Step_RestoreCameraFollow`

**Step 49: Unlock Input**
- Add: `Step_UnlockInput`

**Step 50: Enable Final Boundary Colliders**
- Add: `Step_SetColliderState`
- Set `gameObjectName = "LoomvilleBoundaries_Final"`
- Set `enabled = true`
- Set `affectChildren = true`

**Step 51: Wait for Lab Entry**
- Add: `Step_WaitForTrigger`
- Set `triggerId = "enter_solana_labs"`
- Set `armTrigger = true`

**Step 52: Load Solana Labs**
- Add: `Step_LoadScene`
- Set `sceneName = "SolanaLabs"`
- Set `spawnId = "labs_entry"`

**Step 53: Label - End Intro**
- Add: `Step_Label`
- Set `labelName = "intro_complete"`

**Step 54: Set Checkpoint**
- Add: `Step_SetCheckpoint`
- Set `checkpointId = "intro_complete"`
- Set `checkpointLabel = "intro_complete"`
- Set `saveToState = true`

**Note:** Solana Labs segment will be created in a separate guide. The intro sequence ends when player enters the lab.

**Sequence Checklist:**
- [ ] `Sequence_NewGameIntro` created
- [ ] All 54 steps added in correct order
- [ ] Labels added at key points (`bedroom_start`, `loomville_entry`, `wrong_direction`, `wait_for_path`, `intro_complete`)
- [ ] Checkpoints set at major milestones (`bedroom_wake`, `loomville_entry`, `intro_complete`)
- [ ] All dialogue sequences assigned
- [ ] All camera shots assigned
- [ ] All trigger IDs match scene objects
- [ ] All spawn IDs match scene spawn points
- [ ] Input locks are balanced (every LockInput has UnlockInput)
- [ ] Camera shots are followed by RestoreCameraFollow
- [ ] Boundary colliders managed via `Step_SetColliderState`
- [ ] Wrong direction triggers configured (event-based or polling)

---

## 6. Starting the Sequence

### Using SequenceDirector (Recommended)

The new `SequenceDirector` service provides a game-wide entry point:

**In Bootstrap Scene or Game Manager:**
1. Ensure `SequenceDirector` exists (auto-creates if missing)
2. Call `SequenceDirector.Instance.Play(sequenceAsset)` to start
3. Or use gate-based play: `SequenceDirector.Instance.TryPlayOnce("intro_once", sequenceAsset)`

**Example Code:**
```csharp
// In your game start script
SequenceAsset introSequence = Resources.Load<SequenceAsset>("Sequences/Sequence_NewGameIntro");
SequenceDirector.Instance.Play(introSequence);
```

### Using SequenceRunner Directly (Legacy)

If you need direct control:
1. Assign `Sequence_NewGameIntro` to `SequenceRunner.startingSequence`
2. Set `autoStart = true`
3. Sequence will start automatically on scene load

**Note:** `SequenceDirector` is preferred as it handles input locks, gating, and queueing automatically.

---

## 7. Handling Wrong Direction Triggers

Wrong direction triggers need special handling since they can fire multiple times. The recommended approach uses event-driven waiting with label jumping.

### Recommended Implementation

**Setup in Scene:**
1. Create `SequenceTrigger` components covering wrong direction areas
2. Set `triggerId` (e.g., `"loomville_wrong_direction"`)
3. Set `startArmed = false`, `fireOnce = false`
4. **Important:** Modify `SequenceTrigger` to call `TriggerRegistry.FireTrigger("wrong_direction")` when fired
   - Or create custom trigger component that fires both its own trigger AND the event

**In Sequence:**
1. Main wait is for path trigger (`Step_WaitForTrigger` with `"loomville_path_reached"`)
2. Wrong direction triggers fire event `"wrong_direction"` via `TriggerRegistry`
3. Use `Step_WaitForEvent` in parallel (or handle via custom step)
4. When wrong direction event fires, jump to `wrong_direction` label
5. Show dialogue, pan camera, jump back to `wait_for_path` label

### Alternative: Custom Wrong Direction Step

Create `Step_HandleWrongDirection` that:
- Subscribes to `TriggerRegistry.OnTriggerFired` for `"wrong_direction"`
- When fired, plays dialogue and pans camera
- Returns to waiting state
- Can be used alongside `Step_WaitForTrigger` for path

**For Now:** Use the label jumping pattern documented in the sequence steps above.

---

## 8. Collider Management

Currently, collider management requires manual GameObject activation. Future steps can automate this:

### Current Workaround

1. Create parent GameObjects for boundary colliders:
   - `LoomvilleBoundaries_ToPath` (enabled when player should go to path)
   - `LoomvilleBoundaries_Final` (enabled after NPC dialogue)

2. Manually enable/disable in sequence (requires custom step or manual intervention)

### Future Enhancement

Create `Step_SetColliderState` step:
- Takes GameObject name or ID
- Enables/disables colliders
- Can target multiple GameObjects

**For Now:** Document collider states in sequence comments or use `Step_Log` to remind when to enable/disable.

---

## 9. Save Gate & Name Input Validation

### How Save Gate Works

The `Step_CommitFirstSave` step enforces:
- **No save before name:** If player quits before Step 25, no profile is committed
- **Save after name:** Once Step 25 completes, profile is saved with playerName

### Testing Save Gate

**Test 1: Quit Before Name Input**
1. Start sequence
2. Let it run until Step 23 (name input)
3. Quit game (Alt+F4 or stop play mode)
4. Check: `LocalSaveServiceStub.HasProfile()` should return `false`
5. Check: No PlayerPrefs key `SignalJam_Profile` exists

**Test 2: Quit After Name Input**
1. Start sequence
2. Complete name input (Step 23)
3. Let Step 25 (CommitFirstSave) complete
4. Quit game
5. Check: `LocalSaveServiceStub.HasProfile()` should return `true`
6. Check: PlayerPrefs contains `SignalJam_Profile` with playerName

**Validation Checklist:**
- [ ] Save does NOT occur before name input
- [ ] Save DOES occur after name input
- [ ] PlayerPrefs contains correct playerName
- [ ] Profile data includes timestamp

---

## 10. Starter Selection Setup (Lab Scene)

### Step 1: Verify Starter Options Register

1. Enter Play mode in Solana Labs scene
2. Check console for: "Registered option: starter_fire" (x3)
3. If not logging, check:
   - `StarterOption` components are enabled
   - `StarterSelectionService` exists in scene
   - Options have non-empty `chainlingId`

### Step 2: Test Selection UI

1. In Play mode, call: `StarterSelectionService.Instance.StartSelection()`
2. UI should appear with 3 options
3. Click an option
4. Check console: "Starter selected: starter_fire" (or selected ID)
5. Verify: `StarterSelectionService.Instance.SelectedChainlingId` matches selection

### Step 3: Verify Sequence Integration

1. Run sequence until Step 26 (`Step_SelectStarterChainling`)
2. Selection UI should appear automatically
3. After selection, sequence should continue to Step 27
4. Check: `SequenceContext.StarterSelected` contains selected ID

**Starter Selection Checklist:**
- [ ] 3 StarterOption components exist
- [ ] All have unique chainling IDs
- [ ] StarterSelectionUI exists on Canvas
- [ ] Selection UI shows options correctly
- [ ] Selection stores in context
- [ ] Sequence continues after selection

---

## 11. Quest Assignment Verification (Lab Scene)

### Step 1: Create First Quest Definition

1. Right-click → `Create` → `SignalJam` → `Narrative` → `Quest Definition`
2. Name: `Quest_FirstMarketRun`
3. Configure:
   - `questId = "first_market_run"`
   - `questName = "Market Run"`
   - `description = "Get 2 coffees and 1 beer from the market"`
4. Add objectives:
   - **Objective 1:**
     - `objectiveId = "coffee"`
     - `description = "Collect 2 coffees"`
     - `targetAmount = 2`
   - **Objective 2:**
     - `objectiveId = "beer"`
     - `description = "Collect 1 beer"`
     - `targetAmount = 1`

### Step 2: Assign to Sequence

1. In `Sequence_NewGameIntro`, find Step 28 (`Step_StartQuest`)
2. Assign `questDefinition = Quest_FirstMarketRun`

### Step 3: Verify Quest Starts

1. Run sequence until Step 28 completes
2. Check console: "Started quest: first_market_run"
3. Verify: `QuestService.Instance.IsQuestActive("first_market_run")` returns `true`
4. Verify: `QuestService.Instance.GetQuestState("first_market_run")` returns valid state

**Quest Checklist:**
- [ ] `Quest_FirstMarketRun` created with 2 objectives
- [ ] Quest assigned to sequence step
- [ ] Quest starts when step executes
- [ ] Quest state is accessible via QuestService

---

## 12. End-to-End Testing Checklist

### Pre-Test Setup

- [ ] All services exist in bootstrap scene
- [ ] All scenes are added to Build Settings
- [ ] `SequenceRunner` has `startingSequence` assigned
- [ ] `SequenceRunner.autoStart = true`
- [ ] `SequenceRunner.showDebugUI = true` (for testing)

### Test Flow

**Test 1: Sequence Starts**
- [ ] Sequence begins automatically on scene load
- [ ] Debug overlay shows "Sequence: NewGame_Intro"
- [ ] Step index increments correctly

**Test 2: Bedroom Wake Sequence**
- [ ] Bedroom scene loads
- [ ] Player spawns at `bedroom_start`
- [ ] Input is locked during wake sequence
- [ ] Camera shot executes (zoom in on player)
- [ ] Dialogue displays correctly (room looks different)
- [ ] Camera zooms out slowly
- [ ] Camera follow restores
- [ ] Input unlocks
- [ ] Player can move around room
- [ ] Exit door trigger fires when player touches it
- [ ] Loomville scene loads

**Test 3: Loomville Entry**
- [ ] Player spawns at `loomville_from_bedroom`
- [ ] Input locks during entry dialogue
- [ ] Entry dialogue plays (doesn't recognize environment, notices path)
- [ ] Camera pans to path
- [ ] Camera follow restores
- [ ] Input unlocks
- [ ] Boundary colliders are enabled (player restricted to path route)

**Test 4: Wrong Direction Handling**
- [ ] Player tries to go wrong direction
- [ ] Wrong direction trigger fires
- [ ] Dialogue plays ("shouldn't go there, follow path")
- [ ] Camera pans to path again
- [ ] Player can continue to path

**Test 5: Path to Encounter**
- [ ] Player reaches path trigger
- [ ] Boundary colliders disable
- [ ] Input locks
- [ ] Encounter starts
- [ ] Combat UI shows "Run" option
- [ ] Other inputs show dialogue ("should run")
- [ ] Player selects "Run"
- [ ] Encounter exits
- [ ] Post-encounter dialogue plays
- [ ] Camera pans to ledge
- [ ] Input unlocks

**Test 6: Ledge Jump Sequence**
- [ ] Boundary colliders enable (restrict to ledge route)
- [ ] Player reaches ledge jump trigger
- [ ] Input locks
- [ ] Ledge jump dialogue plays
- [ ] Input unlocks (movement only)
- [ ] Player can jump off ledge
- [ ] Landing zone trigger fires
- [ ] Input locks
- [ ] NPC greeting dialogue plays
- [ ] Camera pans to lab
- [ ] Input unlocks
- [ ] Final boundary colliders enable
- [ ] Player can explore Loomville (restricted to area)

**Test 7: Lab Entry**
- [ ] Player reaches lab entry trigger
- [ ] Solana Labs scene loads
- [ ] Sequence continues in lab (separate guide)

**Test 5: Input Lock Balance**
- [ ] No input locks remain after sequence completes
- [ ] Debug overlay shows "Active: 0" locks
- [ ] Player can move and interact normally

**Test 6: Camera Control**
- [ ] Camera returns to follow mode after shots
- [ ] Camera zoom returns to normal
- [ ] Camera pitch returns to normal

**Test 7: Trigger Firing**
- [ ] Each trigger fires exactly once
- [ ] Triggers don't fire when not armed
- [ ] Triggers fire when armed by sequence

**Test 8: Cross-Scene Continuation**
- [ ] Sequence state persists across scene loads
- [ ] Step index resumes correctly
- [ ] Context variables persist (playerName, starterSelected)

**Test 9: Restart Cleanly**
- [ ] Sequence can be restarted without errors
- [ ] No duplicate services created
- [ ] State resets correctly

---

## 13. Common Pitfalls & How to Avoid Them

### Pitfall 1: Forgetting to Arm Triggers

**Symptom:** `Step_WaitForTrigger` waits forever, sequence hangs.

**Solution:**
- Always set `armTrigger = true` on `Step_WaitForTrigger`
- Or manually arm triggers before waiting: `SequenceTrigger.Arm()`
- Check trigger `startArmed` setting (should be `false` for sequence control)

**Prevention:** Add a `Step_Log` before each `Step_WaitForTrigger` to verify trigger ID.

---

### Pitfall 2: Mismatched Trigger IDs

**Symptom:** Trigger never fires, or wrong trigger fires.

**Solution:**
- Use exact string matching (case-sensitive)
- Copy-paste trigger IDs instead of typing
- Verify IDs in Inspector match exactly
- Use consistent naming: `scene_object_purpose` (e.g., `bedroom_exit_door`)

**Prevention:** Create a reference document listing all trigger IDs used in sequence.

---

### Pitfall 3: Hard-Coding Scene References

**Symptom:** Sequence breaks when scenes are renamed or reorganized.

**Solution:**
- Always use spawn IDs, not GameObject names
- Use trigger IDs, not GameObject references
- Store IDs in sequence steps, not in code

**Prevention:** Never drag GameObjects into sequence step fields. Use string IDs only.

---

### Pitfall 4: Leaving Input Locked

**Symptom:** Player cannot move after sequence completes.

**Solution:**
- Ensure every `Step_LockInput` has a matching `Step_UnlockInput`
- Count locks: LockInput steps should equal UnlockInput steps
- Check debug overlay: "Active: 0" locks after sequence

**Prevention:** Add `Step_UnlockInput` immediately after dialogue/cutscene steps.

---

### Pitfall 5: Forgetting to Restore Camera Follow

**Symptom:** Camera stays in shot position, doesn't follow player.

**Solution:**
- Add `Step_RestoreCameraFollow` after camera shots
- Or set `restoreFollowAfterShot = true` on `CameraShotController`

**Prevention:** Always add `Step_RestoreCameraFollow` after `Step_PlayCameraShot`.

---

### Pitfall 6: Spawn Point Not Found

**Symptom:** Player doesn't spawn, or spawns at wrong location.

**Solution:**
- Verify spawn point `spawnId` matches `Step_LoadScene.spawnId`
- Check spawn point is enabled in scene
- Verify `SpawnService` exists and spawn points register (check console)
- Wait a frame after scene load before spawning (handled automatically)

**Prevention:** Test spawn points manually: `SpawnService.Instance.SpawnPlayerAt("spawn_id")`

---

### Pitfall 7: Dialogue UI Not Found

**Symptom:** Dialogue doesn't display, sequence hangs.

**Solution:**
- Ensure `DialogueUIController` exists in scene
- Verify `DialogueService` can find UI controller (check console)
- Check dialogue panel is active and visible
- Verify text components are assigned

**Prevention:** Test dialogue manually: `DialogueService.Instance.PlayDialogue(dialogueAsset)`

---

### Pitfall 8: Sequence Doesn't Start

**Symptom:** Nothing happens when scene loads.

**Solution:**
- Check `SequenceRunner.startingSequence` is assigned
- Verify `autoStart = true`
- Check `SequenceRunner` exists in scene
- Verify sequence asset is not null

**Prevention:** Add `Step_Log` as first step to verify sequence execution.

---

### Pitfall 9: Context Variables Not Persisting

**Symptom:** PlayerName or StarterSelected is empty after scene load.

**Solution:**
- Verify `Step_SetVariable` steps execute before scene loads
- Check `SequenceState` persistence is enabled
- Ensure variables are set before `Step_LoadScene`
- Use `context.PlayerName` directly, not variable dictionary

**Prevention:** Use typed properties (`context.PlayerName`) instead of generic variables when possible.

---

### Pitfall 10: Starter Selection Doesn't Work

**Symptom:** Selection UI doesn't appear, or selection isn't stored.

**Solution:**
- Verify `StarterSelectionUI` exists on Canvas
- Check `StarterOption` components are registered (check console)
- Ensure selection UI has option button prefab assigned
- Verify `StarterSelectionService` exists

**Prevention:** Test selection manually: `StarterSelectionService.Instance.StartSelection()`

---

## Quick Reference: ID Mapping

### Spawn IDs
- `bedroom_start` → Bedroom spawn point (bed position)
- `loomville_from_bedroom` → Loomville entry spawn (door exit)
- `labs_entry` → Solana Labs entry spawn (for lab scene)

### Trigger IDs
- `bedroom_exit_door` → Bedroom exit door trigger
- `loomville_path_reached` → Path location trigger
- `loomville_wrong_direction` → Wrong direction warning trigger(s)
- `loomville_ledge_jump` → Ledge jump trigger
- `loomville_landing_zone` → Landing area trigger
- `enter_solana_labs` → Labs entry trigger

### Transform IDs (for camera shots)
- `bedroom_player` → Player spawn position
- `bedroom_center` → Bedroom center
- `loomville_path_focus` → Path location
- `loomville_ledge_focus` → Ledge location
- `loomville_lab_focus` → Lab building position

### Label Names (for jumping)
- `bedroom_start` → Start of bedroom sequence
- `loomville_entry` → Start of Loomville sequence
- `wrong_direction` → Wrong direction handler
- `wait_for_path` → Waiting for path trigger
- `intro_complete` → End of intro sequence

### Checkpoint IDs
- `bedroom_wake` → After bedroom wake sequence
- `loomville_entry` → After Loomville entry
- `intro_complete` → End of intro (before lab)

### GameObject Names (for collider management)
- `LoomvilleBoundaries_ToPath` → Colliders restricting to path route
- `LoomvilleBoundaries_Final` → Colliders keeping player in Loomville area

### Chainling IDs (starters - for lab scene)
- `starter_fire` → Fire Chainling
- `starter_water` → Water Chainling
- `starter_earth` → Earth Chainling

---

## Final Validation

Before considering the intro sequence complete:

- [ ] All steps execute in order without errors
- [ ] All triggers fire correctly (path, wrong direction, ledge, landing, lab)
- [ ] Input locks release correctly at each stage
- [ ] Camera control returns to gameplay after each shot
- [ ] Boundary colliders enable/disable correctly
- [ ] Wrong direction triggers show dialogue and pan camera
- [ ] Encounter allows only "Run" option
- [ ] Ledge jump works correctly
- [ ] NPC dialogue plays after landing
- [ ] Camera pans to lab correctly
- [ ] Sequence can be restarted cleanly
- [ ] Debug overlay shows correct state (call stack, variables, labels)
- [ ] Checkpoints can be used to resume
- [ ] No console errors or warnings

## Notes on New Features

### Using Labels and Jumping

The sequence uses labels for organization:
- Labels mark key points (`bedroom_start`, `loomville_entry`)
- `Step_JumpToLabel` can jump back to labels (wrong direction handler)
- Labels make sequences easier to debug and modify

### Using Checkpoints

Checkpoints allow resume:
- Set at major milestones (`bedroom_wake`, `loomville_entry`, `intro_complete`)
- Can resume from checkpoint if game is quit
- Checkpoints save scene ID and spawn ID automatically

### Typed Variables

Variables are now typed:
- Use `Step_SetVariable` with type selection (Bool, Int, Float, String, Vector3)
- Choose scope: Local (cleared on sequence end) or Global (persists)
- Variables can be used in conditions for branching

### Event-Driven Waits

Use `Step_WaitForEvent` for better performance:
- Subscribes to events instead of polling
- Supports timeout with label jump
- More efficient than polling triggers

---

## Appendix A: Scene Transition Triggers (Non-Sequence)

For scene transitions outside of sequences (normal gameplay doors, portals):

### SceneTransitionTrigger Component

Use `SceneTransitionTrigger` for direct scene transitions that don't need sequence control.

#### Setup Steps

1. Create a trigger zone GameObject (e.g., `DoorTrigger_ToLoomville`)
2. Add `Collider` component (BoxCollider recommended)
3. Set `Is Trigger = true`
4. Add `SceneTransitionTrigger` component:
   - `targetSceneName` = Scene name (must match Build Settings)
   - `spawnId` = Spawn point ID in target scene
   - `transitionMode`:
     - `AutoOnEnter` = Transition when player enters (doors, portals)
     - `RequireInteract` = Require interact input (elevators, special doors)
   - `useBufferZone` = Enable if spawn point is inside trigger area

#### Buffer Zone Setup (Prevents Re-Triggering)

When spawn points are inside transition trigger areas:

1. Create child GameObject: `BufferZone`
2. Add `Collider` component (BoxCollider)
3. Set `Is Trigger = true`
4. Make collider **larger** than parent trigger (extends outward)
5. Add `SceneTransitionBuffer` component

**Logic:**
- Player spawns inside trigger → Transition is NOT armed
- Player walks out of buffer zone → Transition becomes armed
- Player re-enters trigger → Transition fires

```
         +---------------------------+
         |       BUFFER ZONE         |
         |   (SceneTransitionBuffer) |
         |                           |
         |   +-------------------+   |
         |   | TRANSITION TRIGGER|   |
         |   | (SceneTransition  |   |
         |   |    Trigger)       |   |
         |   |                   |   |
         |   |    [SPAWN]        |   |
         |   +-------------------+   |
         |                           |
         +---------------------------+
```

#### Inspector Settings

**SceneTransitionTrigger:**
- `targetSceneName`: Target scene name
- `spawnId`: Spawn point ID in target scene
- `transitionMode`: AutoOnEnter or RequireInteract
- `useBufferZone`: Enable buffer zone system
- `playerTag`: Tag to detect (default: "Player")
- `enableSafetyNet`: Enable jump edge case detection

**SceneTransitionBuffer:**
- `enableSafetyNet`: Enable jump edge case detection
- `safetyCheckInterval`: How often to verify player position

#### Example: Bedroom Exit Door

**In Bedroom Scene:**

1. Create `DoorTrigger_ToLoomville` at exit door
2. Add BoxCollider (Is Trigger = true)
3. Add SceneTransitionTrigger:
   - `targetSceneName = "Loomville"`
   - `spawnId = "loomville_from_bedroom"`
   - `transitionMode = AutoOnEnter`
   - `useBufferZone = true`
4. Create child `BufferZone`:
   - Add BoxCollider (Is Trigger = true, larger than parent)
   - Add SceneTransitionBuffer

**In Loomville Scene:**

1. Create `DoorTrigger_ToBedroom` at entry door
2. Add BoxCollider (Is Trigger = true)
3. Add SceneTransitionTrigger:
   - `targetSceneName = "Bedroom"`
   - `spawnId = "bedroom_start"`
   - `transitionMode = AutoOnEnter`
   - `useBufferZone = true`
4. Create child `BufferZone`:
   - Add BoxCollider (Is Trigger = true, larger than parent)
   - Add SceneTransitionBuffer

#### Gizmo Colors

- **Green**: Transition trigger zone
- **Pink/Magenta**: Buffer zone

---

## Next Steps After Intro

Once the intro sequence works:

1. **Create Lab Sequence:** Continue intro in Solana Labs scene (name input, starter selection, quest assignment)
2. **Create More Sequences:** Use the same pattern for quests, events, cutscenes
3. **Expand Dialogue:** Add more `DialogueSequenceSO` assets for NPCs
4. **Add Camera Shots:** Create shots for discoveries, battles, story beats
5. **Replace Stubs:** Implement real combat system, wallet save system
6. **Add Branching:** Use `Step_If`, `Step_Branch`, `Step_Switch` for conditional flow
7. **Create Sub-Sequences:** Use `Step_CallSequence` for reusable dialogue/encounter sequences
8. **Add Custom Steps:** Create steps for collider management, GameObject activation, etc.

The same systems can author any scripted sequence without code changes. See `SEQUENCE_AUTHORING_GUIDE.md` for advanced features.

---

## Appendix B: Collider Management Steps

Two helper steps have been created for managing colliders:

### Step_EnableGameObject

Enables or disables a GameObject by name. Use when you want to activate/deactivate entire objects.

**Usage:**
- Enable/disable UI panels
- Show/hide scene objects
- Activate/deactivate entire boundary systems

### Step_SetColliderState

Enables or disables colliders on a GameObject without affecting the GameObject itself. Use for boundary colliders.

**Usage:**
- Enable/disable boundary colliders
- Control movement restrictions
- Manage collision zones

**Benefits:**
- GameObject stays active (can have other components)
- Can affect children colliders
- More granular control than GameObject.SetActive

Both steps are now available in the step creation menu: `SignalJam` → `Narrative` → `Steps`

