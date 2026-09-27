# Chainbound intro — Loomville ↔ player house transition

## What was added

Two **`Step_EnableGameObject`** steps are wired into **`chainbound_intro_sequence`**:

| Step asset | When it runs | Effect |
|------------|----------------|--------|
| `intro_disable_loomville_house_transition` | Right after **`intro_loadscene`** (Loomville) | `GameObject.SetActive(false)` on the return trigger |
| `intro_enable_loomville_house_transition` | Right after **`intro_unlockinput`** following **`intro_playdialogue3`** (Admiral / Loomville entry line) | `SetActive(true)` again |

This blocks walking back into **`player_room`** until after the ledge trigger (`trigger_room_exit`) and the outdoor dialogue, so **`SceneTransitionTrigger`** buffer/arm state does not get into a bad mid-intro state.

## How to configure in Unity

1. **Open** `Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_sequence.asset` and confirm the step list includes the two new steps (Unity may refresh automatically).

2. **Name check (important)**  
   Both steps target **`gameObjectName: TransitionTrigger`**, matching the GameObject in **`Loomville.unity`** that has **`SceneTransitionTrigger`** with **`targetSceneName: player_room`**.  
   - If you **rename** that object in the hierarchy, set the **same string** on both step assets under **Primary Target → Game Object Name**.  
   - If you ever have **more than one** object named `TransitionTrigger` in loaded scenes, rename the Loomville door to something unique (e.g. `Loomville_Transition_To_PlayerRoom`) and update both steps.

3. **Optional**  
   - Turn on **Enable Debug Logs** on either step while testing to see success/fail in the Console.  
   - If a step **fails** with “GameObject not found,” the sequence runner will treat it as a failed step—fix the name or ensure the object exists in **Loomville** when the step runs.

4. **Timing tweaks**  
   - To keep the door closed until **after** the first intro battle, move **`intro_enable_loomville_house_transition`** to **after** `intro_disableintrobarriers` in the sequence (instead of after Admiral unlock).

## Files

- Steps: `Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/Steps/intro_disable_loomville_house_transition.asset`  
- Steps: `Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/Steps/intro_enable_loomville_house_transition.asset`  
- Sequence: `chainbound_intro_sequence.asset`
