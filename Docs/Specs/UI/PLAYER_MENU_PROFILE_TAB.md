# PLAYER_MENU_PROFILE_TAB — Landscape Player ID Card

**Status:** Demo v1  
**Target prefab:** `Assets/Prefabs/UI/PlayerMenu/ProfileView.prefab`  
**Tab asset:** `Assets/Data/UI/PlayerMenuTab_profile.asset` (`tabId: profile`)  
**Share copy:** `Assets/Data/UI/ProfileShareCopy.asset`

---

## Purpose

The Profile tab is a **landscape 16:9 player ID card**. The overworld dog is the face. Chainlings stay on the Chainlings tab. Share exports the card image plus a short prefilled line through the Android share sheet. There is no X OAuth.

---

## Layout

The tab body stretches inside the existing player-menu `SafeAreaPanel`.

| Region | Contents |
|---|---|
| `ProfileCardRoot` | 16:9 `AspectRatioFitter` (Fit In Parent). This is the only capture target. |
| Left ~45% | Idle-down dog `Image` plus inactive `HatSlot` / `OutfitSlot` parents for later Favor cosmetics. |
| Right ~55% | `$NAME`, `GUEST` / `SECURED` chip, title (`New Signal`), unique species, captures + entangled, inscriptions, record. |
| Tab siblings | `cosmeticHintLabel` (in-menu only) and **Share** (bottom-right thumb zone). Both stay **outside** the card root. |

B / Close remains on `PlayerMenuController`. Default selection is Share.

---

## On-image fields

| Field | Source | Rule |
|---|---|---|
| `$` + display name | `GameSessionContext.DisplayName`, then `PlayerStatsSnapshot.displayName` | Fallback `PLAYER` |
| GUEST / SECURED | `GameSessionContext.IsGuestSession` | Never print wallet or profile UUID |
| Title | serialized default `New Signal` | Placeholder frame |
| Unique species | `uniqueSpeciesCaptured` | Always |
| Captures + entangled | `totalCaptures` / `entangledCaptures` | Always |
| Inscriptions created | `totalInscriptionsCreated` | Always |
| Record | `battlesWon` / `battlesLost` | Hide if both are 0 |

Name and guest chip bind immediately from session so the card is not empty while `IPlayerStatsProvider.FetchStatsAsync` runs.

## Off-image / not demo

RAM balance, mythic count, party, Trenches, pubkey, X handle, referral codes.

---

## Share capture

`ProfileCardShareService` on the screen:

1. Disable Share and hide Close / FooterHints / hub / menu background. Snapshot previous active states.
2. `MobileHudService.AcquireContext(MobileHudContext.Hidden, "profile_share")` (priority 60 beats Menu).
3. Wait until end of frame.
4. Capture **`ProfileCardRoot` only** via canvas camera / `ReadPixels`, then scale to 1920×1080.
5. Restore chrome and release the HUD token in `finally` (success or failure).
6. Write PNG to `Application.temporaryCachePath`.
7. Android: `ACTION_SEND` `image/png` + `EXTRA_TEXT` from `ProfileShareCopySO`. Editor: log the file path.

Default copy: `just checked $NAME's chainbound id`.

---

## Android FileProvider

- Authority: `${applicationId}.fileprovider`
- Manifest provider is not exported and grants URI permissions.
- `Assets/Plugins/Android/res/xml/file_paths.xml` exposes `cache-path`.
- Share uses a `content://` URI. No storage permission.

---

## Cosmetic-slot hook

`HatSlot` and `OutfitSlot` are empty inactive children of the dog stage. Later Favor outfits parent `Image`s here. Keep Favor / unlock copy on `cosmeticHintLabel` (sibling of the card) so it never appears in the shared PNG.

---

## Authoring

1. `Tools > Chainbound > UI > Player Menu Dev > Ensure Profile View Prefab`
2. `Tools > Chainbound > UI > Player Menu Dev > Sync Profile View Into Player Menu Roots`  
   or `Tools > Chainbound > UI > Sync Player Menu Tab Screens`

---

## Out of scope

X OAuth, Trenches on the card, party strip, second aspect ratio, live RenderTexture dog, Favor-granting cosmetics (slots only), share prompts on capture/vending, referral codes.
