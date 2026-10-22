# Chainbound — Player Profile (career sheet)

The in-game **Profile** tab under `PlayerMenuRoot` is a three-column career sheet fitted to the Profile B controller HUD slots.

## Source of truth (Unity)

| Asset | Path |
|---|---|
| Host | `Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab` |
| Profile tab | nested `ProfileView` → `Assets/Prefabs/UI/PlayerMenu/ProfileView.prefab` |
| Screen | `Assets/Scripts/UI/MenuNavigation/ProfileMenuScreen.cs` |
| Portrait | `Assets/Scripts/UI/MenuNavigation/ProfilePortraitTurntable.cs` |
| Rows | `Assets/Scripts/UI/MenuNavigation/ProfileCareerRow.cs` |
| Tab def | `Assets/Data/UI/PlayerMenuTab_profile.asset` (“Career sheet and share.”) |
| Layout spec | `Docs/Specs/UI/PLAYER_MENU_PROFILE_TAB.md` |
| Safe-area slots | `Docs/Specs/UI/HANDHELD_HUD_SAFE_AREAS_AND_VISUAL_LANGUAGE.md` (Profile B) |

Canvas scaler on PlayerMenuRoot is landscape (`2670×1200`). Profile opens full-screen inside the menu.

## What Profile shows

- **TopLeft** — dog portrait. `ProfilePortraitTurntable` cycles Up, Right, Down, Left idle sprites. `HatSlot` and `OutfitSlot` stay inactive.
- **TopCenter** — `$NAME`, `GUEST`/`SECURED`, unique species, captures, entangled, wins, losses, KOs, KO'd. Zeros stay visible. Captures opens the record drawer.
- **TopRight** — lifetime RAM earned, RAM spent, rebirths, star ups. Live RAM stays on the hub panel.
- **DialogueLowerCenter** — one line for the focused row, plus `SHARE`.
- **Capture record** drawer (runtime) — orb, achievement, and boost detail in the center channel. Cancel/B closes the drawer first.

Not on this tab: inscriptions, stamps, candy rolls, wallet address, edit-name, live RAM balance.

## HUD safe area

Profile B is an inverted-T:

- `TopLeft` `(0.019, 0.438)` → `(0.316, 0.951)`
- `TopCenter` `(0.334, 0.438)` → `(0.666, 0.868)`
- `TopRight` `(0.684, 0.438)` → `(0.981, 0.951)`
- `DialogueLowerCenter` `(0.206, 0.014)` → `(0.794, 0.382)`

Lower corners stay clear of the controller pads. The menu already draws `BaseTint` and `ScanlineOverlay`.

## Layout rules

1. All Profile content stays in the Profile B slots.
2. **No panel Back / Confirm.** **A = Share** (or the focused row), **B = close drawer then close the menu**.
3. `SHARE` sits in `DialogueLowerCenter`, gray idle / orange selected, with `UiSelectableMotion` shimmer behind the label.
4. Capture drawer stays in the center channel. CLOSE maps to **[B]**.
5. Typography: **PixeloidSans SDF**, **ALL CAPS** labels. Panels use `9sliceborder_blank` sliced at PPU multiplier `0.2`.
6. Career rows show counts, including zero. Do not print inscribed or stamped totals on this sheet.
7. Share capture hides chrome and the cosmetic hint, freezes the dog on the Down rest frame, then resumes. The sheet must stay readable at 1920×1080.

## Wireframes

| File | Purpose |
|---|---|
| `prof-01-main-844x390.png` / `1280x720` | Earlier ID-card study. The shipped layout is the career sheet above. |
| `prof-02-capture-844x390.png` / `1280x720` | Capture Record drawer |
| `prof-03-safe-map-*.png` | Safe-area overlay + pad callouts |
| `hud-overlay-*.png` | Transparent HUD frame used as overlay |

## Authoring

`Tools > Chainbound > UI > Player Menu Dev > Ensure Profile View Prefab` rebuilds `ProfileView` from `ProfileViewPrefabUtility`. Sync embeds it into the menu roots.
