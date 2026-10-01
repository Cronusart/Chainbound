# Mobile HUD — Movement Pad and Tactile Audio

Covers the `MobileServices` prefab's movement pad rework and the tactile audio layout
that replaced the borrowed menu UI sounds.

## Movement pad structure

The pad separates the **touch rect** from the **travelling art**:

```
MovementPad          HybridMovementPad + Image (alpha 0, raycastTarget ON)  <- never moves
└─ PadVisual         Image (dpad_full_0, raycastTarget OFF)                 <- rocks toward the drag
```

`MovementPad`'s Image is the touch surface. It is fully transparent, and its
`CanvasRenderer.cullTransparentMesh` is **off on purpose**: `GraphicRaycaster` skips
graphics whose CanvasRenderer is culled, and `cullTransparentMesh` culls at zero alpha,
so leaving it on makes the pad unhittable.

`padVisual` must be a child. Pointing it at the pad's own RectTransform drags the touch
surface along with the finger, so every subsequent `ScreenPointToLocalPointInRectangle`
sample is measured against a displaced origin and the reported vector decays while a
direction is held. `HybridMovementPad` rejects that wiring at runtime and warns in
`OnValidate`.

## Two radii, not one

| Field | Meaning |
|---|---|
| `deflectionRadiusMode` / `deflectionRadiusFraction` | How far the **finger** travels for full output. Fraction of the pad's smaller half-extent, so it survives resize and resolution changes. |
| `travelRadiusPixels` | How far the **art** moves. Small; this is a rocker, not a slide. |

Conflating them is what made the pad feel broken: a single 15 px radius saturated the
input almost at the centre of a 480 x 500 pad and capped the visual travel at 15 px,
which read as "no visual drift" and "direction not mapped".

Current values: fraction `0.55` of a 240 px half-extent gives roughly a 132 pad-local px
throw; travel is `28` px.

## Feel controls

- `digitalOnly` (on): reported output snaps to 8 unit vectors. The visual still tracks
  the finger continuously, so the pad reads analog and behaves digital.
- `snapTravelToSector` (on): with `digitalOnly`, the art goes to **full** travel along the
  snapped axis instead of ramping. That is what produces the detent / physical-rocker feel.
- `travelFollowSpeed`: exponential follow rate. `0` snaps instantly, `~26` reads as a firm
  mechanical rocker. Framerate-independent.
- Set `digitalOnly` off for free analog walk/run; travel then ramps with deflection.

## Release safety

A latched pad (finger down when the HUD is hidden, scene load, swallowed pointer-up)
is released by a watchdog that requires **both** no pointer events for
`watchdogGraceSeconds` **and** no pressed pointer on any device. Requiring both means the
pad still works on backends where the pressed-pointer query is unavailable, instead of
zeroing every frame. `OnApplicationFocus`/`OnApplicationPause` also reset the pad, which
is the common Android case.

`useReleaseWatchdog` exists as a diagnostic switch: if directions ever cut out after
~0.4 s of holding still, turn it off to confirm the input backend is the cause.

## MobileInputManager reference resolution

Two references were silently null on device and are now handled:

- `MobileInputManager.inputActions` is a serialized field. The old `Resources.Load` by
  name could never succeed because the asset lives at
  `Assets/Scripts/Movement/InputSystem_Actions.inputactions`, not under a `Resources/`
  folder, so PSG1 movement/sprint and the keyboard fallback always reported neutral.
- `PlayerMovement3D.mobileInputManager` cannot be serialized from a scene or from
  `PlayerRoot.prefab` because MobileServices lives on the DontDestroyOnLoad services
  root. It now resolves lazily (0.5 s re-poll) and subscribes the mobile jump events when
  it appears.

## Tactile audio — adding the clips

`MobileHudButtonAudioBridge` uses a **role table** instead of a field per role. A role
with no table entry falls back to `Default`; `Silent` never falls back.

| Role | Assigned to | Sound event |
|---|---|---|
| `Default` | every HUD button not overridden | `UI_hud_tactile_press` / `_release` |
| `Confirm` | ButtonA | `UI_hud_tactile_confirm` / `_release` |
| `Cancel` | ButtonB | `UI_hud_tactile_cancel` / `_release` |
| `System` | Start, Select | `UI_hud_tactile_system` |
| `Directional` | movement pad sector changes | `UI_hud_tactile_dpad` |

All six assets live in `Assets/Data/Audio/UI/` and are already wired. **They ship with an
empty clip list**, and the bridge skips clipless events, so the HUD is silent today and
`AudioService` does not warn. To enable the pass, drop clips into the `clips` array of the
assets you want live — no other change needed.

Pre-tuned so a Game Boy-style click does not overpower menu UI:

- `outputBus: UI`, volumes in the 0.10–0.28 range.
- Narrow pitch bands (e.g. 0.97–1.03) so repeat taps are not identical.
- `polyphonyKey: hud_tactile`, `maxInstances: 2`, `cooldownSeconds: 0.03`. This is the
  important one — without it, mashing four face buttons stacks four clips and the subtle
  click becomes a clack. The D-pad tick uses its own key with `maxInstances: 1`.
- Release events are quieter and pitched down, like a real button returning.

If HUD tactile should be dialled independently of menu UI, move these assets to the
`SFX` bus. Keeping them on `UI` puts them under the same player volume slider as menu
sounds, which is usually what players expect.

`enableAudio` on the bridge is a master mute that leaves all wiring intact.

## Haptic click profile

All eight `GameboyButton` controls use the shared `HapticFeedback` backend on pointer-down.
Ordinary menu navigation, selection, confirm, and cancel feedback routes through the same backend,
preference, and rate limiter so one physical press cannot stack a HUD pulse with a menu pulse.

- Light Android click (API 26+): `8 ms`, amplitude `30/255`.
- Light iOS click: `UISelectionFeedbackGenerator.selectionChanged()`.
- Pre-API-26 Android: `8 ms`; those devices do not expose amplitude control.
- Minimum interval: `75 ms` between ordinary UI pulses.
- Medium/heavy profiles remain available for exceptional UI feedback, but HUD buttons author as `Light`.
- Capture challenge haptics remain separate because their stronger patterns communicate gameplay state.

The shared player preference is `SignalJam_UIHapticsEnabled`. Disabling UI haptics in Settings now
also disables mobile HUD button haptics.

## Authoring tool

`Tools / Chainbound / Mobile / Fix Movement Pad And Wire Tactile Audio` is idempotent and
re-applies everything above to `Assets/Prefabs/Controls/MobileServices.prefab`: creates or
re-binds `PadVisual`, sets the radii and feel values, assigns the action asset, removes a
leftover `OnScreenStick`, and refills the audio role table and button overrides.

## Known remaining item

The gaming-pad frame still sits inside the safe-area-inset `HudRoot`, so it leaves a gap
on the camera-cutout side in landscape. Fix is to keep `SafeAreaAdapter` on an interactive
layer and let the chrome be full-bleed — see the frame notes when that pass happens.
