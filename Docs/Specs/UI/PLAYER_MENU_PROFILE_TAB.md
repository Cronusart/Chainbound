# PLAYER_MENU_PROFILE_TAB — Career Sheet

**Status:** Demo v1  
**Target prefab:** `Assets/Prefabs/UI/PlayerMenu/ProfileView.prefab`  
**Tab asset:** `Assets/Data/UI/PlayerMenuTab_profile.asset` (`tabId: profile`)  
**Share copy:** `Assets/Data/UI/ProfileShareCopy.asset`

---

## Purpose

The Profile tab is a full-screen career sheet inside the player menu. The overworld dog turns through its idle facings on the left. Chainlings stay on the Chainlings tab. Live RAM stays on the hub panel. Share exports the sheet image plus a short prefilled line through the Android share sheet. There is no X OAuth.

---

## Layout

Profile opens full-screen, so the four slots use the Profile B anchors from `HANDHELD_HUD_SAFE_AREAS_AND_VISUAL_LANGUAGE.md`. The menu already supplies `BaseTint` and `ScanlineOverlay`. Do not add another of either. Panels use `9sliceborder_blank`, sliced, at PPU multiplier `0.2`. Labels are Pixeloid, all caps.

| Slot | Anchors (min → max) | Contents |
|---|---|---|
| `TopLeft` | `(0.019, 0.438)` → `(0.316, 0.951)` | Portrait stage only. |
| `TopCenter` | `(0.334, 0.438)` → `(0.666, 0.868)` | `$NAME`, `GUEST` / `SECURED`, then `UNIQUE SPECIES`, `CAPTURES`, `ENTANGLED`, `WINS`, `LOSSES`, `KOS`, `KO'D`. Zeros stay visible. |
| `TopRight` | `(0.684, 0.438)` → `(0.981, 0.951)` | `RAM EARNED`, `RAM SPENT`, `REBIRTHS`, `STAR UPS`. |
| `DialogueLowerCenter` | `(0.206, 0.014)` → `(0.794, 0.382)` | One line for the focused row, plus `SHARE`. |

`ProfileSheet` is the capture target. A sheet fill sits behind the four panels so the shared PNG does not show the world in the controller corners. `SHARE` uses the gray idle sprite and the orange selected sprite. `UiSelectableMotion` owns the shimmer, and the button transition stays None. Default selection is Share.

There is no title line. Candy rolls and stamps are not on this sheet. `total_ordinals_acquired` counts capture Known grants, and the current Stamp path does not increment `total_inscriptions_created`.

B / Close remains on `PlayerMenuController`. B closes the capture record drawer first.

---

## On-sheet fields

| Field | Source | Rule |
|---|---|---|
| `$` + display name | `GameSessionContext.DisplayName`, then `PlayerStatsSnapshot.displayName` | Fallback `PLAYER` |
| GUEST / SECURED | `GameSessionContext.IsGuestSession` | Never print wallet or profile UUID |
| Unique species | `uniqueSpeciesCaptured` | Always, including zero |
| Captures | `totalCaptures` | Always. Selecting the row opens the capture record drawer. |
| Entangled | `entangledCaptures` | Always, including zero |
| Wins / losses | `battlesWon` / `battlesLost` | Always, including zero |
| KOs / KO'd | `totalKosScored` / `totalKosTaken` | Always, including zero |
| RAM earned / spent | `totalRamEarned` / `totalRamSpent` | Lifetime totals. Live balance stays on the hub. |
| Rebirths / star ups | `totalRebirths` / `totalStarUps` | Always, including zero |

Name and guest chip bind immediately from session so the sheet is not empty while `IPlayerStatsProvider.FetchStatsAsync` runs.

The capture record drawer stays in the center channel. It still lists attempts, orbs, achievements, and active boosts.

## Off-sheet

Live RAM balance, candy rolls, stamps, inscriptions, mythic count, party, Trenches, pubkey, X handle, referral codes.

---

## Portrait

`ProfilePortraitTurntable` on `TopLeft` holds four sprite lists copied from the player idle sequences. Playback uses unscaled time. Facing order is Up, Right, Down, Left. Each facing holds for about `0.6s` while that direction's idle frames step at about `8` fps. `DogImage` shows the body. `HatSlot` and `OutfitSlot` stay inactive.

Before the share capture waits for end of frame, the turntable freezes on the Down rest frame. It resumes when the capture finishes.

---

## Share capture

`ProfileCardShareService` on the screen:

1. Disable Share and hide Close / FooterHints / hub / menu background, plus the cosmetic hint. Snapshot previous active states.
2. Freeze the portrait on the Down rest frame.
3. `MobileHudService.AcquireContext(MobileHudContext.Hidden, "profile_share")` (priority 60 beats Menu).
4. Wait until end of frame.
5. Capture **`ProfileSheet`** via canvas camera / `ReadPixels`, then scale to 1920×1080.
6. Restore chrome, resume the turntable, and release the HUD token in `finally` (success or failure).
7. Write PNG to `Application.temporaryCachePath`.
8. Android: `ACTION_SEND` `image/png` + `EXTRA_TEXT` from `ProfileShareCopySO`. Editor: log the file path.

Default copy: `just checked $NAME's chainbound id`.

---

## Android FileProvider

- Authority: `${applicationId}.fileprovider`
- Manifest provider is not exported and grants URI permissions.
- `Assets/Plugins/Android/res/xml/file_paths.xml` exposes `cache-path`.
- Share uses a `content://` URI. No storage permission.

---

## Cosmetic-slot hook

`HatSlot` and `OutfitSlot` are empty inactive children of the portrait stage. The turntable's facing index is what a later cosmetic overlay reads, once Favor art has a sprite per direction. This pass does not add that art or an equip flow. The hint `COSMETICS UNLOCK WITH FAVOR` hides during share.

---

## Authoring

1. `Tools > Chainbound > UI > Player Menu Dev > Ensure Profile View Prefab`
2. `Tools > Chainbound > UI > Player Menu Dev > Sync Profile View Into Player Menu Roots`  
   or `Tools > Chainbound > UI > Sync Player Menu Tab Screens`

---

## Out of scope

X OAuth, Trenches on the sheet, party strip, live RenderTexture dog, Favor-granting cosmetics (slots only), share prompts on capture/vending, referral codes, candy-roll and stamp counters.
