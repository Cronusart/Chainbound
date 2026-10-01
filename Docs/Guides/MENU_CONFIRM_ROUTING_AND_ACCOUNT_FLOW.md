# Menu Confirm Routing, HUD Chrome Safe Area, and Main Menu Account Flow

Covers Android/Seeker menu input, HUD safe-area behavior, and the unified main-menu
wallet/profile connection flow.

---

## 1. Mobile `A` reached menus twice

### Why

A single tap on the HUD's `A` button produces **two independent signals**, and both are
wanted:

| Transport | Path | Purpose |
|---|---|---|
| `GameboyButton.OnButtonDown` | → `MobileInputManager.OnAPressed` | Direct C# route for panels that own their own input |
| Sibling `OnScreenButton` | writes `<Gamepad>/buttonSouth` | Makes a physical PSG1 pad and the on-screen pad behave identically |

`<Gamepad>/buttonSouth` is bound to **both** `UI/Submit` and `Player/Interact` in
`InputSystem_Actions`. `MenuInputContextController` subscribed to `UI/Submit.performed`
*and* `OnAPressed`, so one tap called `SubmitSelected()` twice, roughly one frame apart.

### Fix — `MenuInputIntentGate`

`Assets/Scripts/UI/MobileControls/MenuInputIntentGate.cs`

One arbitration point. Handlers claim a logical intent (`Submit`, `Cancel`, `CloseMenu`,
`DetailedView`, `SpecialInteraction`, `Guide`); the first transport to arrive wins and the
rest of an 80 ms window is swallowed. Behaviour is now identical for touch, gamepad, and
keyboard.

The window must exceed the frame gap between transports (the `OnScreenButton` write is
observed on the following input update) while staying well below a deliberate double tap.
Adjust `MenuInputIntentGate.DefaultWindowSeconds` only if the game runs below ~12 fps.

**When adding a new HUD button or menu action, route it through the gate** if the action
can also arrive via an `InputAction`. Otherwise it will double-fire.

### Fix — confirm never establishes focus

`MenuInputContextController.SubmitSelected()` previously called
`EnsureSelectedBeforeMobileInput()` (which restores `defaultSelection`) and then submitted
whatever that produced. Any frame where focus was momentarily invalid turned the press into
a **default-action auto-confirm**.

The rule is now: **confirm acts on focus, but never establishes it.** If focus is invalid at
submit time, focus is repaired so the *next* press is meaningful and the current press is
dropped with an `Invalid` cue.

This is what caused "navigated to Settings, pressed A, landed in the Game Menu": submit #1
opened Settings, then submit #2 found focus lost mid-transition, restored it to the scene's
`defaultSelection` (the bonkDog hotspot), and submitted that.

---

## 2. Starter select confirmed "Choose" while "Back" was navigated

Two separate causes, both fixed in `StarterSelectionUI.cs`:

1. **Wrapping navigation.** `Confirm` and `Back` pointed at each other in *all four*
   directions. Holding a direction toggled focus at the repeat rate, so release landed on
   whichever button the last tick happened to pick. Navigation is now one-way and
   idempotent while held: right/down → `Back`, left/up → `Confirm`, with the far edges
   `null`.

2. **`ApplyDefaultSelection()` ran before focus was read.** A press made while focus was
   briefly off a panel button snapped to `Confirm` and committed it. Focus is now read
   first; if it is not a valid panel button the press only repairs focus.

`HandleMobileAPressed` also now defers entirely to the UI module when the module still owns
`Submit` (mirroring the existing `IsInputModuleNavigationActive` pattern), since the
synthetic `buttonSouth` already delivers that press to the focused button.

---

## 3. HUD frame gap beside the camera cutout

`androidRenderOutsideSafeArea` is enabled, so the display renders under the cutout.
`SafeAreaAdapter` on `HudRoot` insets **everything** below it — correct for interactive
controls, wrong for the decorative `GamingPadFrame`, which then stopped short of the screen
edge and showed a strip of gameplay.

### Fix — `SafeAreaOverscan`

`Assets/Scripts/UI/MobileControls/SafeAreaOverscan.cs`

The inverse of `SafeAreaAdapter`: expands a `RectTransform` back outward by the current
inset. Physical pixels are divided by `Canvas.scaleFactor` before they can cancel the
inset, because the parent rect is expressed in canvas reference units.

Attached to `GamingPadFrameGroup` in `Assets/Prefabs/Controls/MobileServices.prefab`;
that group and `GamingPadFrame` are both stretched to fill (the frame was previously pinned
at a fixed 2670×1200).

**Why an opt-out marker rather than a hierarchy split:** the safe area stays the default for
everything under `HudRoot`, so a newly added control group is inset automatically. Escaping
the safe area is the explicit, reviewable choice. A hierarchy split would instead require
every future author to remember which branch to parent into.

Constraint: no `Mask` / `RectMask2D` may exist on an ancestor, since the rect deliberately
overflows its parent. There is none today.

`additionalOverscan` adds extra expansion in canvas units if the frame art needs bleed
beyond the reported inset.

---

## 4. Connect Wallet did nothing on device

### Cause — Editor mocks took precedence in a player build

`main_menu.unity` wires `EditorMockWalletProvider` and `EditorMockIdentityProvider` on
`[ChainboundAccountServices]` for Play Mode convenience. Neither type was Editor-gated, so
both compiled into the Android build, and
`MainMenuFlowOrchestrator.ResolveWalletProvider()` returned the serialized reference
**before** ever reaching the Mobile Wallet Adapter path. The build was connecting a
fabricated wallet with public key `1111…`.

### Fix — providers self-disqualify off-Editor

- New marker `IEditorOnlyAccountProvider` (`AccountInterfaces.cs`).
- `EditorMockWalletProvider` / `EditorMockIdentityProvider` implement it, and their
  `IsAvailable` now requires `Application.isEditor`.
- `MainMenuFlowOrchestrator.DiscardEditorOnlyProvider()` clears a marked provider in player
  builds (with a warning naming the field) so the existing null check falls through to
  `SolanaWalletAdapterProvider` → `SolanaWalletService` → `AndroidMobileWalletAdapterBridge`.

Editor behaviour is unchanged.

### Identity configuration resolved

`Assets/Data/Solana/SolanaConfig.asset` now uses the project-owned HTTPS identity
`https://chainbound-pub.chainboundsolana.workers.dev` with relative icon `favicon.ico`.
Both URLs returned HTTP 200 and the Android release validator passed on 2026-09-23.
The remaining gate is confirmation in the real wallet authorization sheet on the physical Seeker.

Also verify the app launches through `Bootstrap.unity` (which spawns `GameServices` and
`SolanaServiceBridge`) before `main_menu`, and that `mwaIconUri` resolves relative to the
identity URI.

Clearing the mock references in the scene is now optional — they are ignored on device —
but recommended, since the warning will fire on every resolve.

---

## 5. Connect Wallet also restores wallet-owned profiles

The Game Menu now exposes only **Play Game** and **Connect Wallet**. Connect Wallet
always performs the complete proof flow: MWA authorize, server nonce, exact-message
signature, and `wallet_connect_verified`.

After proof, the backend decides without trusting a client-supplied profile:

- an unowned wallet is linked to the fresh profile created during bootstrap;
- an already-linked wallet resolves its existing profile and transactionally moves
  the current install's auth identity onto it;
- a profile that already has a different wallet still fails closed.

The recovery transaction revokes stale identity mappings but does not delete or merge
the displaced fresh profile. Once the returned session is cached, the panel switches to
its profile/Continue presentation for an existing game.

The status label is the `Status` GameObject in
`Assets/Prefabs/UI/MainMenu/GameMenuPanel.prefab`, bound to `statusLabel`.

> Note: `GameMenuPanel.prefab` has `sequenceController: {fileID: 0}`. The scene
> overrides it, so buttons work in `main_menu` but would be dead in any other
> instance of the prefab.

Connect Wallet uses the configured project-owned identity. If it is later
removed or changed to a non-HTTPS URL, MWA fails closed in
`SolanaWalletService.ValidateMobileIdentity()`.
