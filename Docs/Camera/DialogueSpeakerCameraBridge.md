# DialogueSpeakerCameraBridge

Speaker-based `CameraShotPresetSO` selection during dialogue (`DialogueService` line events).

## Default: disabled on GameServices

The component on **`Assets/Prefabs/GameServices/GameServices.prefab`** (`DialogueSpeakerCamBridge`) is **disabled** so it does not subscribe to dialogue globally.

## Opt-in per sequence dialogue

On **`Step_PlayDialogue`** assets, enable **Use Speaker Camera Bridge**.

The step will:

1. `FindInstance()` (includes disabled components)
2. Set **`enabled = true`** before `PlayDialogue`
3. Set **`enabled = false`** in a `finally` block after dialogue completes

Other code paths that call `DialogueService.PlayDialogue` directly are **not** affected unless you enable the bridge another way.

## Configuration

Rules, anchored session, and neutralize follow offset are still edited on the **DialogueSpeakerCameraBridge** component on the prefab (rules apply whenever the step enables it).

## Code

- `DialogueSpeakerCameraBridge.FindInstance()`
- `Step_PlayDialogue` — `useSpeakerCameraBridge`
