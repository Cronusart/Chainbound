# Main Menu Setup

## 1) Scene Hierarchy (UI)

Canvas (Screen Space - Overlay):
- `SplashGroup` — `CanvasGroup` + studio logo Image
- `Background` — `CanvasGroup`, optional `UIImageFrameAnimator`
- `Foreground` — `RectTransform` + `CanvasGroup`, optional `UIImageFrameAnimator`
- `GameLogo` — `RectTransform` + `CanvasGroup`
- `ContrastOverlay` — `CanvasGroup`, semi-transparent Image
- `ConnectSettingsGroup` — `CanvasGroup`
  - `ConnectWalletButton` — `Button`
  - `SettingsButton` — `Button`
- `PlayButtonGroup` — `CanvasGroup`
  - `PlayButton` — `Button`

---

## 2) Add the Controller

Create an empty GameObject **MainMenuController** (or add to an existing one).  
Add **`MainMenuSequenceController`** component.

---

## 3) Wire Inspector Fields

### Splash
| Field | Assign |
|-------|--------|
| Splash Group | `SplashGroup` CanvasGroup |

### Reveal Elements
| Field | Assign |
|-------|--------|
| Background Group | `Background` CanvasGroup |
| Background Animator | `Background` UIImageFrameAnimator *(optional)* |
| Foreground Transform | `Foreground` RectTransform |
| Foreground Group | `Foreground` CanvasGroup |
| Foreground Animator | `Foreground` UIImageFrameAnimator *(optional)* |
| Game Logo Transform | `GameLogo` RectTransform |
| Game Logo Group | `GameLogo` CanvasGroup |
| Contrast Overlay Group | `ContrastOverlay` CanvasGroup |

### Buttons
| Field | Assign |
|-------|--------|
| Connect Settings Group | `ConnectSettingsGroup` CanvasGroup |
| Connect Wallet Button | `ConnectWalletButton` Button |
| Settings Button | `SettingsButton` Button |
| Play Button Group | `PlayButtonGroup` CanvasGroup |
| Play Button | `PlayButton` Button |

> **Do not add onClick listeners in the Inspector.** The controller registers them in code via `Awake()`.

---

## 4) Button Behavior

| Button | What it does |
|--------|--------------|
| `ConnectWalletButton` | Calls `walletService.Connect()` → SDK opens wallet adapter → on success, fetches on-chain profile and swaps UI to Play button |
| `SettingsButton` | Fires the `onSettingsRequested` UnityEvent — wire this to open your settings panel |
| `PlayButton` | Reads profile state and routes to the correct scene (intro or post-intro) |

---

## 5) Add Services

Create an empty GameObject **SolanaMenuServices** in the scene.

### Live Solana (production)
- Add **`MainMenuWalletServiceSolana`**
  - `Reconnect If Already Connected`: enable — skips the connect dialog if a session is still active
- Add **`MainMenuProfileServiceSolana`**
  - `Intro Complete Player Prefs Key`: `SignalJam_IntroQuestComplete`

### Testing without a wallet
- Add **`MainMenuWalletServiceStub`** instead (simulates connect delay + success/failure)
- Add **`MainMenuProfileServiceStub`** instead (reads local PlayerPrefs + override toggles)

Then on **MainMenuSequenceController**, assign under **Services**:
- **Wallet Service** → `SolanaMenuServices`
- **Profile Service** → `SolanaMenuServices`

See **`MAIN_MENU_SOLANA_WALLET_SETUP_NOTES.md`** for full Bootstrap/SolanaServiceBridge config required for the live adapters to work.

---

## 6) Set Scene Routing

On **MainMenuSequenceController**:
- `Intro Sequence Start Scene Name` → `IntroSequenceStartScene`
- `Post Intro Start Scene Name` → `PostIntroStartScene`
- `Route Mode` → `AutoFromProfile` (normal), or force a path during testing

---

## 7) Route Logic

| Profile State | Route |
|--------------|-------|
| No wallet / connect failed | Stays on connect screen |
| Wallet connected, no profile | → IntroSequenceStartScene (new game) |
| Profile exists, intro incomplete | → IntroSequenceStartScene |
| Profile exists, intro complete | → PostIntroStartScene |

---

## 8) Optional Services

- **Music Service** — add `MainMenuMusicServiceStub` (or real impl); assign to `Music Service` field
- **Intro Video Service** — add `MainMenuIntroVideoServiceStub` (or real impl); assign to `Intro Video Service` field. If null, video step is skipped silently.

---

## 9) PSG1 / Controller Input

- Ensure scene has an **EventSystem**.
- The controller auto-selects `ConnectWalletButton` after the reveal sequence and `PlayButton` after connect succeeds — no manual EventSystem selection needed.

### MainMenuSequenceController2 (V2) — Input Setup

For **MainMenuSequenceController2**, keyboard/gamepad navigation and back require:

1. **EventSystem** must use **InputSystemUIInputModule** (not StandaloneInputModule).
2. Assign **Input Actions** (Header 10) on the controller: drag `InputSystem_Actions` from `Assets/Scripts/Movement/` into the field.
3. The asset includes a **UI** map with Move (WASD/arrows), Submit (Enter/Space), Cancel (Escape/Backspace) — same bindings as CombatUI.
4. The controller assigns this asset to the InputSystemUIInputModule at runtime, so navigation and Backspace work without editing the EventSystem in the scene.

---

## 10) DOTween

- Main menu animations use DOTween (required). Ensure DOTween Pro is installed in the project.
