# Intro Sequence Setup Guide

This guide explains how to author the New Game intro sequence using the data-driven systems.

## Prerequisites

All systems are now implemented. You need to:
1. Create ScriptableObject assets (dialogue, camera shots, quests, sequence)
2. Place scene objects (spawn points, triggers)
3. Wire everything together

---

## Step 1: Create Dialogue Assets

### Bedroom Monologue
1. Right-click in Project → Create → SignalJam → Narrative → Dialogue Sequence
2. Name: `Dialogue_BedroomWake`
3. Add lines:
   - Line 1: "Where am I?" (no speaker)
   - Line 2: "This place... it feels familiar." (no speaker)
   - Line 3: "I need to get out of here." (no speaker)
4. Set `autoAdvance = false` (wait for input)

### Loomville Entry Monologue
1. Create: `Dialogue_LoomvilleEntry`
2. Add lines:
   - "The outside world... it's different than I remember."
   - "I should explore carefully."
3. Set `autoAdvance = false`

### Post-Encounter Messages
1. Create: `Dialogue_PostEncounter`
2. Add lines:
   - "That was close! I need to retreat."
   - "There's a ledge over there..."

### Professor Dialogue
1. Create: `Dialogue_ProfessorGreeting`
2. Add lines:
   - Speaker: "Professor", "Welcome to Solana Labs!"
   - Speaker: "Professor", "Before we begin, what should I call you?"
   - (Name input happens here)
   - Speaker: "Professor", "Nice to meet you, {PlayerName}!"
   - Speaker: "Professor", "Now, choose your starter Chainling..."

---

## Step 2: Create Camera Shot Presets

### Wake Shot (Bedroom)
1. Right-click → Create → SignalJam → Camera → Camera Shot Preset
2. Name: `Shot_BedroomWake`
3. Settings:
   - `targetZoom = 3f` (zoomed in)
   - `duration = 2f`
   - `holdTime = 1f`
   - `targetTransformId = "bedroom_player"` (or use world position)

### Zoom Out Shot
1. Create: `Shot_BedroomZoomOut`
2. Settings:
   - `targetZoom = 7f` (normal zoom)
   - `duration = 1.5f`
   - `targetTransformId = "bedroom_center"`

### Pan to Labs
1. Create: `Shot_PanToLabs`
2. Settings:
   - `targetPosition = (world position of labs)`
   - `duration = 2f`
   - `lookAtTarget = true`

---

## Step 3: Create Quest Definition

### First Quest: Coffee + Beer
1. Right-click → Create → SignalJam → Narrative → Quest Definition
2. Name: `Quest_FirstMarketRun`
3. Quest ID: `first_market_run`
4. Quest Name: "Market Run"
5. Description: "Get 2 coffees and 1 beer from the market"
6. Objectives:
   - Objective 1: `coffee` - "Collect 2 coffees" - `targetAmount = 2`
   - Objective 2: `beer` - "Collect 1 beer" - `targetAmount = 1`

---

## Step 4: Create Sequence Asset

### NewGame_Intro Sequence
1. Right-click → Create → SignalJam → Narrative → Sequence
2. Name: `Sequence_NewGameIntro`
3. Sequence ID: `new_game_intro`
4. Start Checkpoint ID: `intro_start`

### Add Steps (in order):

#### Segment 1: Video + Bedroom
1. **Step_PlayVideoCutscene**
   - Video clip: (assign your intro video)
   - Allow skip: true

2. **Step_LoadScene**
   - Scene name: "Bedroom"
   - Spawn ID: "bedroom_start"

3. **Step_LockInput**
   - Channel: All
   - Reason: "Bedroom wake sequence"

4. **Step_PlayCameraShot**
   - Shot preset: `Shot_BedroomWake`
   - Target transform ID: "bedroom_player"

5. **Step_PlayDialogue**
   - Dialogue sequence: `Dialogue_BedroomWake`

6. **Step_PlayCameraShot**
   - Shot preset: `Shot_BedroomZoomOut`

7. **Step_UnlockInput**

8. **Step_WaitForTrigger**
   - Trigger ID: "bedroom_exit_door"
   - Arm trigger: true

9. **Step_LoadScene**
   - Scene name: "Loomville"
   - Spawn ID: "loomville_from_bedroom"

#### Segment 2: Loomville Tutorial
10. **Step_LockInput**
    - Channel: All
    - Reason: "Loomville entry"

11. **Step_PlayDialogue**
    - Dialogue sequence: `Dialogue_LoomvilleEntry`

12. **Step_UnlockInput**

13. **Step_WaitForDistance**
    - Check position: (house position)
    - Distance threshold: 10f
    - Within distance: false (wait until player leaves)

14. **Step_LockInput**
    - Channel: All
    - Reason: "First encounter"

15. **Step_StartEncounter**
    - Encounter ID: "first_encounter"
    - Forced outcome: "ForcedRun"

16. **Step_PlayDialogue**
    - Dialogue sequence: `Dialogue_PostEncounter`

17. **Step_PlayCameraShot**
    - Shot preset: `Shot_PanToLabs`
    - Target transform ID: "solana_labs_focus_target"

18. **Step_RestoreCameraFollow**

19. **Step_WaitForTrigger**
    - Trigger ID: "enter_solana_labs"
    - Arm trigger: true

20. **Step_LoadScene**
    - Scene name: "SolanaLabs"
    - Spawn ID: "labs_entry"

#### Segment 3: Solana Labs
21. **Step_LockInput**
    - Channel: All
    - Reason: "Professor dialogue"

22. **Step_PlayDialogue**
    - Dialogue sequence: `Dialogue_ProfessorGreeting`

23. **Step_PromptNameInput**
    - Use context player name: true
    - **Note (current flow):** Name input is captured earlier during the intro sprite cinematic (`IntroSpriteCinematic` → prompt → commit → `IntroSpriteCinematic_PostName`), not in the lab. Keep this step type for reuse; do not re-prompt at Toby if cinematic already set the name.

24. **Step_SetVariable**
    - Set player name: true
    - Variable value: (will use context.PlayerName)

25. **Step_CommitFirstSave**
    - Use context player name: true

26. **Step_SelectStarterChainling**
    - Use context starter: true

27. **Step_SetVariable**
    - Set starter selected: true
    - Variable value: (will use context.StarterSelected)

28. **Step_StartQuest**
    - Quest definition: `Quest_FirstMarketRun`

29. **Step_UnlockInput**

30. **Step_Log**
    - Message: "Intro sequence complete! Normal gameplay begins."
    - Log level: Log

---

## Step 5: Place Scene Objects

### Bedroom Scene
1. **SpawnPoint**
   - GameObject name: "SpawnPoint_bedroom_start"
   - Spawn ID: "bedroom_start"
   - Position: Player start position

2. **Exit Door (SequenceTrigger + SceneTransitionTrigger)**
   - GameObject name: "Trigger_bedroom_exit"
   - Add **both** components to the same object:
     - **SequenceTrigger:** Trigger ID `bedroom_exit_door`, `startArmed = false`, `fireOnce = true`
     - **SceneTransitionTrigger:** Target scene + spawn, and **Blocking Quest Ids** = your intro quest ID (e.g. `chainbound_intro`)
   - The blocking quest disables the transition trigger during the intro so the sequence controls the load; on return visits the door works normally.

### Loomville Scene
1. **SpawnPoint**
   - Spawn ID: "loomville_from_bedroom"
   - Position: Exit from bedroom

2. **DistanceTrigger**
   - GameObject name: "DistanceTrigger_leave_house"
   - Trigger ID: "leave_house_radius"
   - Position: House center
   - Distance threshold: 10f
   - Start armed: false

3. **SequenceTrigger**
   - Trigger ID: "enter_solana_labs"
   - Position: Lab entrance
   - Start armed: false

4. **Transform Reference** (for camera pan)
   - GameObject name: "solana_labs_focus_target"
   - Position: Labs building

### Solana Labs Scene
1. **SpawnPoint**
   - Spawn ID: "labs_entry"
   - Position: Lab entrance

2. **StarterOption** (x3)
   - GameObject: "StarterOption_1", "StarterOption_2", "StarterOption_3"
   - Chainling IDs: "starter_1", "starter_2", "starter_3"
   - Display names: "Fire Chainling", "Water Chainling", "Earth Chainling"
   - Add interactable component or wire to selection UI

---

## Step 6: Setup Services in Scene

### Persistent Services (DontDestroyOnLoad)
Create a GameObject "GameServices" with:
- `InputLockService`
- `DialogueService`
- `CameraShotController`
- `SceneTransitionManager`
- `SpawnService`
- `TriggerRegistry`
- `SequenceRunner`
- `LocalSaveServiceStub`
- `EncounterServiceStub`
- `StarterSelectionService`
- `QuestService`

### Per-Scene Services
- `DialogueUIController` (on Canvas)
- `NameInputUI` (on Canvas)
- `StarterSelectionUI` (on Canvas)

---

## Step 7: Start Sequence

### Option A: Auto-start
1. Assign `Sequence_NewGameIntro` to `SequenceRunner.startingSequence`
2. Enable `autoStart = true`
3. Sequence starts on game launch

### Option B: Manual start
```csharp
SequenceRunner.Instance.StartSequence(introSequenceAsset);
```

---

## Testing Checklist

- [ ] Video plays and can be skipped
- [ ] Bedroom wake sequence plays correctly
- [ ] Player spawns at correct points
- [ ] Dialogue displays and advances
- [ ] Camera shots execute smoothly
- [ ] Triggers fire when expected
- [ ] Name input works and stores result
- [ ] First save commits after name
- [ ] Starter selection works
- [ ] Quest starts correctly
- [ ] Input locks/unlocks properly
- [ ] Cross-scene continuation works

---

## Debug Tips

1. Enable `showDebugUI` on `SequenceRunner` to see current step
2. Enable `enableDebugLogs` on services for detailed logging
3. Check `InputLockService` debug UI to see active locks
4. Use `Step_Log` steps to trace execution flow

---

## Troubleshooting

**Sequence doesn't start:**
- Check that `SequenceRunner` exists in scene
- Verify `startingSequence` is assigned
- Check console for errors

**Dialogue doesn't show:**
- Ensure `DialogueUIController` exists
- Check that `DialogueService` can find UI controller
- Verify dialogue sequence has lines

**Triggers don't fire:**
- Check trigger IDs match exactly
- Ensure triggers are armed before waiting
- Verify `TriggerRegistry` exists

**Player doesn't spawn:**
- Check `SpawnPoint` has correct spawn ID
- Verify `SpawnService` exists
- Ensure spawn point is enabled

**Camera shots don't work:**
- Check `CameraShotController` exists
- Verify `TopDownCameraController3D` is assigned
- Check shot preset has valid target

---

## Next Steps

Once the intro sequence is working:
1. Create additional sequences for quests/events
2. Add more dialogue sequences for NPCs
3. Create camera shots for discoveries
4. Expand quest system with more objectives
5. Replace stubs with real implementations (combat, wallet)

