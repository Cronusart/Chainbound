# Chainbound — Player Profile (landscape, HUD-safe)

Wireframes for the in-game **Profile** tab under `PlayerMenuRoot`, fitted to the mobile controller HUD frame safe area.

## Source of truth (Unity)

| Asset | Path |
|---|---|
| Host | `Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab` |
| Profile tab | nested `ProfileView` → `Assets/Prefabs/UI/PlayerMenu/ProfileView.prefab` |
| Screen | `Assets/Scripts/UI/MenuNavigation/ProfileMenuScreen.cs` |
| Tab def | `Assets/Data/UI/PlayerMenuTab_profile.asset` (“Player ID card and share.”) |

Canvas scaler on PlayerMenuRoot is already landscape (`2670×1200`).

## What Profile shows today

Two-column ID card:

- **LeftStage** — dog art (`BONKY_D2`), HEAD / BODY utility slots
- **RightIdentity** — `$NAME`, `GUEST`/`SECURED` chip, title (`New Signal`), unique species, captures (+ entangled), stamped moves, W–L record
- **Share** — captures card PNG → native share sheet
- **Capture record** drawer (runtime) — stats + achievements + boosts; Cancel/B closes

Not on this tab: wallet address, edit-name, RAM (those live on main-menu summary).

## HUD safe area

The orange controller frame defines an **inverted-T** content region:

- Wide band across the top (~full width below the top notch)
- Narrower **center channel** between bottom-left (D-pad) and bottom-right (Y/X/A/B) pads
- Pads ≈ **20% width × 28% height** each corner — no interactive content under them

## Layout rules (locked for redesign)

1. All Profile content stays inside the inverted-T; ProfileView gets explicit HUD insets (not only `SafeAreaAdapter`).
2. **No panel Back / Confirm.** Footer chrome can drop duplicate hints; **A = Share** (or confirm focused action), **B = close drawer then pop/close menu** (already partially wired via `TryHandleCancel` + `MenuInputContextController`).
3. Move **Share** off bottom-center (current authored `y≈100`) into the **right column CTA**, above pad tops.
4. Capture drawer: centered in the **middle channel**, width ~60%, bottom edge above pad tops; CLOSE maps to **[B]**.
5. Typography: **PixeloidSans SDF**, **ALL CAPS** labels, bold titles (matches candy-machine plan).
6. Prefer formatter copy **“N STAMPED”** over prefab leftover “inscribed”.
7. Share capture still hides chrome + MobileHud; card must remain readable at 1920×1080 after any HUD inset shrink.

## Wireframes

| File | Purpose |
|---|---|
| `prof-01-main-844x390.png` / `1280x720` | Default Profile ID card |
| `prof-02-capture-844x390.png` / `1280x720` | Capture Record drawer |
| `prof-03-safe-map-*.png` | Safe-area overlay + pad callouts |
| `hud-overlay-*.png` | Transparent HUD frame used as overlay |

## Suggested build steps

0. Measure HUD pad rects in reference resolution; bake as constants / SO on ProfileView (or shared `HudSafeInsets`).
1. Relayout `ProfileCardRoot` / RightIdentity / Share via `ProfileViewPrefabUtility` to match wireframes; kill drifted anchors.
2. Remap nav: default focus Share; Captures → open drawer; B closes drawer first.
3. Align capture drawer anchors to center channel; remove CLOSE collision with pads (or rely on B only and demote CLOSE visual).
4. Play-mode check on device / Device Simulator with HUD frame sprite on top.
