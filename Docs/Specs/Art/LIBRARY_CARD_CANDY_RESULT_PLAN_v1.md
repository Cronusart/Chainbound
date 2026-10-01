# Library Card Candy Result Panel - Cursor Plan (Grok 4.7)

**Status:** Partial. `LibraryCardResultPresenter` and `LibraryCardLayout` are in the client. This file remains the visual contract; do not assume every checklist item is done. Do **not** flip Unity ownership flags. Do **not** change Stamp Prepare/Execute or on-chain attrs.
**Goal:** Replace `CandyMachinePanel` ResultPanel generic UI with a physical library-card reveal that reuses the existing sequential stamp cadence - and that **looks like the same card** as the NFT/marketplace bake.

Reference screenshot intent (user):
- Orange = printed persistent labels (`MOVE`, `MOD`, `MOD`) - never ghost/hide; stamps sit beside/underneath
- Red = printed stats box - stamp POWER/ACCURACY (or ROLE/EFFECT) into this box
- Yellow = rarity row (`RARITY:` + MOVE / MOD / MOD wells) - stamp colored rarity dots into the three boxes

---

## 0) Assets (already on disk)

`Assets/Sprites/UI/LibraryCards/`

| File | Role |
|------|------|
| `librarycard_front.png` | Blank **printed** front chrome (labels, stats box, rarity wells, barcode). Primary ResultPanel card face. |
| `librarycard_back.png` | Back face (provenance later; not required for ResultPanel v1). |
| `librarycard_outline.png` | Pixel-perfect outline for rarity flash around the card. |
| `smoke_front_*.png` / `smoke_back.png` | Prior Pillow bake previews - **do not** use as ResultPanel chrome. |

Import notes (`.meta`): Sprite, filterMode Point (0), alphaIsTransparency, PPU 100. Keep Point filter for pixel art.

**Two delivery paths, one visual product:**
- **ResultPanel (live):** overlays on blank printed front - animation, sequential stamps, shake/flash.
- **NFT / marketplace:** `tools/library-card-render/` (`layout.json` + `ink.json` + `render.py`) bakes the finished PNG.

Do **not** re-bake a full card PNG on every reveal beat. Do **require** visual parity (see section 1b) so live stamps and NFT stamps read as the same object.

---

## 1) Current code to reuse / replace

### Prefab
- `Assets/Prefabs/UI/CandyMachine/CandyMachinePanel.prefab`
- Hierarchy target: `ResultPanel` -> `Card` (+ existing `RarityFlash`, StepOne/Two/Three, Continue)

### Scripts
- `Assets/Scripts/UI/MenuNavigation/CandyMachine/CandyMachineResultView.cs`
  - Today: generic `stepOne/Two/Three` scale-pop reveal + `PlayImpactBeat()` shake + `rarityFlash` fade
  - `Prepare()` barely nudges card Y by 3px - replace with slide-from-top + random settle rotation
- `Assets/Scripts/UI/MenuNavigation/CandyMachine/CandyMachineRevealData.cs`
  - Extend with rarity tiers for move / mod1 / mod2 (nullable for empty mod slots)
- `Assets/Scripts/UI/MenuNavigation/CandyMachine/CandyMachineScreen.cs`
  - `PlayRevealSequence()` stages 1->2->3 (keep order)
  - `BuildKnownReveal` / `BuildStampReveal` - remap captions/values onto card stamp slots
  - ResultView bind around ~2324-2351

### Locked reveal mapping (author-confirmed)

**Known (`GetKnown`) - blank card fills in sequentially:**
1. Type sticker/icon into type well
2. Outline flash + MOVE rarity dot (Folio color)
3. Move name under printed `MOVE` + stats ink into printed stats box

After Known completes, the card shows: type, move name, stats, MOVE rarity. Printed `MOD` labels/wells stay empty.

**Stamp - same physical card, host already persisted (LOCKED):**

When the player stamps a Known move, ResultPanel must **not** re-reveal type / move name / stats / MOVE rarity. Those elements are already on the card and stay visible for the whole Stamp reveal so it feels like the **same card** returning to the table to receive new ink.

`Prepare(Stamp)` / pre-stage:
1. Apply host Known state immediately at full opacity: type well, move name, stats box, MOVE rarity dot
2. Then run entrance slide (card slides in already filled - do not start blank and restamp host)
3. Empty mod slots remain empty until their stamp stages

**Stamp sequential stages (new ink only):**
1. First mod: MOD well 1 rarity dot + mod1 name under first printed `MOD` (shake + outline flash + ink fade)
2. Second mod if present: MOD well 2 rarity + mod2 name; if single-mod stamp, use stage 2 for effect/details flourish instead
3. Finalize: mod effect details / secondary ink as needed + Enable Continue

| Stage | Known (filling blank card) | Stamp (same card, host already on it) |
|------:|----------------------------|----------------------------------------|
| Prepare | blank chrome only | **host already painted**: type, move name, stats, MOVE rarity |
| 1 | Type icon | Mod1 rarity + mod1 name |
| 2 | MOVE rarity dot | Mod2 rarity + mod2 name (or effects if only one mod) |
| 3 | Move name + stats box | Effects finalize / Continue |

Do **not** animate host move name or MOVE rarity again during Stamp. Do **not** clear Known ink when opening Stamp ResultPanel for that host.

---

## 1b) NFT / ResultPanel visual parity (author-locked)

**Hard requirement:** Move names, mod names, stats ink, type treatment, and rarity dots must read as the **same card** in ResultPanel and in NFT/marketplace PNGs. Dual pipelines (live overlay vs Pillow bake) are allowed for *animation and delivery* only - not for a different look.

### Why
Players will own the NFT image and see the candy reveal. If live text is clean TMP and the NFT is distressed stamped ink (or positions/tilts disagree), it feels like two products. Continuity of the *object* matters as much as continuity of the *sequence*.

### Source of truth
| Concern | Source of truth | Consumers |
|---------|-----------------|-----------|
| Label / logo / mod / type / stats positions, sizes, max widths, tilts | `tools/library-card-render/layout.json` | Bake + ResultPanel presenter |
| Stamp ink color, distress, ghost ink (bake only for empty slots until removed) | `tools/library-card-render/ink.json` | Bake; ResultPanel stamp *appearance* must match |
| Rarity hues | Folio `FolioRarityColor` table (section 2) | Bake dots + live dots + outline flash |
| Printed chrome (MOVE/MOD labels, wells, stats box) | `librarycard_front.png` | Both (bake composites onto matching chrome; live uses sprite as CardFace) |

### Texture (stamp ink)
- **Forbidden as final ship look:** plain undressed TMP that lacks the bake’s stamp-distress language.
- **Required:** move name, mod names, and stats ink on ResultPanel must match the Pillow stamp treatment (same PixeloidSans family, same ink color family, visible stamp wear / irregular edges consistent with `ink.json` + `stamp_glyphs` in `render.py`).
- **Allowed implementation paths** (pick one; document in code):
  1. **Preferred:** stamp-strip sprites - render name/mod/stats glyphs through the same bake stamp helpers (or a thin Unity-facing export) into small RGBA sprites / atlas slices, show those as `Image` overlays, animate alpha/scale. Keeps one ink pipeline.
  2. **Acceptable:** TMP (or UI text) with a stamp-distress material/shader tuned against bake smoke side-by-side until parity passes.
- Type icon stays **sticker** treatment (off-white outline / light wear), matching bake - not stamped flat ink.
- Rarity dots: live may use Folio-tinted `Image` circles for v1 motion; **bake must add stamped rarity dots** (Phase E promoted - see section 6) so NFT end-state includes them. Live dots should use the same diameters / well centers as bake once bake lands; until then, match printed well centers on `librarycard_front.png`.

### Layout
- ResultPanel anchors for move name, stats, mod1/mod2, type, rarity wells must be derived from or audited against `layout.json` (pixel origins -> UI local positions on the card rect). Do not invent a second freehand layout.
- Per-label tilts in `layout.json` `rotations` apply to live stamp overlays the same way.
- Long names: honor bake `name_max_width` / fit behavior so wrapping or scale-down matches smoke PNGs.

### Acceptance bar for parity
- Side-by-side: ResultPanel final Known card vs `smoke_front_0mod` (same fixture data) - positions within ~a few bake pixels; ink character clearly the same family (not “clean UI vs rubber stamp”).
- Same for a 1-mod / 2-mod stamp end state vs matching smoke fixtures once bake rarity dots land.

---

## 2) Rarity colors (use Folio palette)

From `ProgressionFolioAuthoringUtility.cs` (`FolioRarityColor`):

| Tier | Color (RGBA 0-1) |
|------|------------------|
| Common | (0.42, 0.36, 0.27, 1) |
| Uncommon | (0.22, 0.45, 0.29, 1) |
| Rare | (0.18, 0.36, 0.58, 1) |
| Epic | (0.48, 0.25, 0.58, 1) |
| Legendary | (0.68, 0.39, 0.12, 1) |
| Mythic | (0.61, 0.17, 0.20, 1) |

Centralize in a small helper e.g. `LibraryCardRarityColors.For(RarityTier)` used by ResultView rarity dots + outline flash tint. Do not invent new hues. Bake rarity dots must use the same RGB mapping (document conversion 0-1 -> 0-255 in render notes).

Empty mod slot -> leave well empty (no ghost `MOD #N` text on the printed label; printed `MOD` chrome stays). Bake should stop drawing ghost `MOD #N` once Phase E chrome parity lands.

---

## 3) Prefab / hierarchy rewrite (ResultPanel)

Replace generic Step* plates with a `LibraryCardReveal` root under ResultPanel:

```
ResultPanel
  LibraryCardRoot (RectTransform)          ← slides + rotates
    CardFace (Image) = librarycard_front
    TypeWell (Image, type icon / sticker)
    MoveNameStamp (Image or TMP+stamp mat) ← under printed MOVE; stamp-parity required
    StatsStamp (Image or TMP+stamp mat)    ← inside printed red box
    Mod1Stamp (Image or TMP+stamp mat)     ← under first printed MOD
    Mod2Stamp
    RarityRow
      MoveDot (Image)
      Mod1Dot (Image)
      Mod2Dot (Image)
    OutlineFlash (Image) = librarycard_outline  ← full-card flash
  ContinueButton (keep)
```

**Rules:**
- Printed chrome is **only** the `CardFace` sprite. Do not duplicate MOVE/MOD labels in overlays.
- Stamp overlays start alpha 0; never cover the printed label glyphs (anchor to the right of / below the printed word per `layout.json` / art).
- `OutlineFlash` uses `librarycard_outline.png`, same rect as CardFace, raycast off, default alpha 0.
- Overlay placement must satisfy section 1b (layout.json + stamp texture parity).

Wire serialized refs on `CandyMachineResultView` (or new `LibraryCardResultPresenter` component owned by ResultView). Prefer loading or mirroring `layout.json` values into presenter fields rather than magic numbers.

---

## 4) Motion / FX (DOTween)

### 4a Card entrance (replace tiny Y nudge in `Prepare`)
On Prepare / before stage 1:
1. Place `LibraryCardRoot` above viewport (`anchoredPosition.y = +ScreenOffset`, e.g. 900-1200)
2. Start rotation Z = random in `[entranceTiltMin, entranceTiltMax]` (suggest ±12° to ±18°)
3. Tween in parallel:
   - Y -> settle Y (0) with `Ease.OutCubic` or custom friction ease (~0.55-0.75s)
   - Z -> random settle in `[settleTiltMin, settleTiltMax]` (suggest ±2° to ±6°) with `Ease.OutQuad`
4. Optional tiny secondary settle (overshoot 1.02 scale then 1.0) for table friction

Serialize ranges on the presenter so design can tune without code edits.

**Stamp entrance special case:** before the slide starts, host Known ink (type, move name, stats, MOVE rarity) must already be applied at alpha 1. The player sees a completed Known card slide onto the table, then only mod stamps animate.

### 4b Per-stamp impact (keep spirit of `PlayImpactBeat`)
For each stage activation:
1. Brief stamp “press” (scale 0.85->1.15->1.0 on the stamp target, ~0.28s)
2. `LibraryCardRoot.DOShakeAnchorPos` (reuse current magnitudes or slightly softer)
3. `OutlineFlash` tinted to the rarity color for that beat -> fade out (~1.0s OutSine) - **use outline sprite**, not a full-rect wash
4. Stamp text/dot alpha 0->1 with slow ink bleed (`Ease.InOutSine`, 0.35-0.55s) **after** impact peak

### 4c Stage orchestration
Keep `CandyMachineScreen.PlayRevealSequence` calling `SetStage(1..3)`.
Rewrite `CandyMachineResultView.SetStage` to drive LibraryCard overlays instead of StepOne/Two/Three GameObjects.
Preserve return duration so Screen timing still waits.

---

## 5) Data plumbing

Extend `CandyMachineRevealData` with:
- `RarityTier MoveRarity`
- `RarityTier? Mod1Rarity` / `Mod2Rarity` (null if slot empty)
- `string MoveName`, `string Mod1Name`, `string Mod2Name`
- Keep existing TypeIcon / Power / Accuracy / Ap / Details

Update `BuildKnownReveal` / `BuildStampReveal` to populate structured fields (not only caption/value strings). Caption/value strings can remain for accessibility / debug.

`BuildStampReveal` must also carry the **host Known snapshot** (type icon, move name, power/accuracy/effect, MOVE rarity) so `Prepare` can paint the card before stage 1. Prefer resolving from the selected Known/host move definition + existing move rarity - do not wait for stage animations to fill host fields.

**Mod effect text:** Prefer `CandyMachineResultView.FormatModifierEffect(...)` which already substitutes locked values into description ranges when `hasLockedValue`. For card stamp line, show `DisplayName` under MOD; put effect into Details or a secondary ink line under the mod if space allows. Do **not** block on on-chain `effectValue` persistence for this UI slice.

---

## 6) Implementation phases (Cursor order)

### Phase A - Prefab + presenter shell (no economy/ownership changes)
1. Author ResultPanel hierarchy with `librarycard_front` + outline + stamp slots + rarity dots
2. Add `LibraryCardResultPresenter` (or expand ResultView) with serialized anchors seeded from `layout.json`
3. Wire Bind from `CandyMachineScreen` (~2324)
4. Editor playmode: card visible at rest, no animation yet

### Phase B - Entrance motion
1. Slide-from-top + random entrance/settle tilt
2. Tune easings; add serialized ranges
3. Ensure Continue stays hidden until stage 3 completes

### Phase C - Sequential stamps
1. Map Known stages onto blank card; map Stamp stages onto **pre-filled host card** (host ink at Prepare)
2. Reuse shake + outline flash; add ink fade-in for **new** stamps only
3. Paint rarity dots with Folio colors; skip empty mod wells
4. Stats ink into printed red box on Known stage 3; on Stamp, stats already present from host - do not restamp unless values change
5. Stamp overlays must use stamp-parity path from section 1b (not clean TMP as final)

### Phase D - Polish + parity audit
1. SFX hooks (existing `PlayRevealStinger` per stage - keep)
2. Pixel-perfect outline flash verification against `librarycard_outline.png`
3. Long move names: fit within stamp region per `layout.json` `name_max_width`
4. Empty Known (0 mods) still shows two empty printed MOD labels + empty mod wells
5. Side-by-side parity check vs bake smokes (section 1b acceptance bar)
6. Back/provenance **out of scope** for ResultPanel v1

### Phase E - Bake parity (required for NFT sameness; can parallel ResultPanel)
1. Update `tools/library-card-render` to composite onto printed chrome matching `librarycard_front` (or equivalent)
2. Add stamped rarity dots (Folio colors, `layout.json` well positions, `ink.json` distress)
3. Remove bake ghost `MOD #N` / ghost move labels so empty slots match ResultPanel
4. Regenerate `--smoke` fixtures; use as golden reference for live parity

### Phase F - Optional later
- Persist true stamp `effectValue` on-chain (not required for this UI)

---

## 7) Explicit non-goals
- No ownership flag flips; no live mint; no Edge Function changes
- No re-baking a full card PNG on every ResultPanel stage beat
- No ghost `MOD #N` / ghost move labels on the ResultPanel card
- Do not cover printed MOVE/MOD chrome with stamp ink
- Do **not** ship ResultPanel with a deliberately different ink/layout language than the NFT bake

---

## 8) Acceptance checklist
- [ ] ResultPanel shows `librarycard_front` resting on table BG after slide-in
- [ ] Each pull has unique settle rotation within configured range
- [ ] Y + rotation both ease (friction feel)
- [ ] Known sequence: type -> MOVE rarity -> move name (+ stats in box)
- [ ] Stamp Prepare shows same card with type, move name, stats, MOVE rarity already visible
- [ ] Stamp stages only add mod rarity/name (no host restamp)
- [ ] Each stamp: shake + outline flash + ink fade-in
- [ ] Rarity dots use Folio colors; empty mod slots blank
- [ ] Printed MOVE/MOD labels always readable
- [ ] Move/mod/stats stamp **texture** matches bake stamp language (section 1b)
- [ ] Move/mod/stats/type/dot **layout/tilts** audited against `layout.json` (section 1b)
- [ ] Bake Phase E: rarity dots + no ghost labels on smoke PNGs (when Phase E done)
- [ ] Continue enables only after final stage
- [ ] Ownership flags untouched

---

## 9) Paste-ready Cursor goal

```
GOAL: Replace CandyMachine ResultPanel generic steps with a library-card reveal using Assets/Sprites/UI/LibraryCards/librarycard_front.png + librarycard_outline.png.

Read Docs/Specs/Art/LIBRARY_CARD_CANDY_RESULT_PLAN_v1.md and implement Phases A->D (and Phase E bake parity if in scope for this run).

Constraints:
- Presentation only; do not flip ownership flags or touch Stamp Prepare/Execute.
- Keep CandyMachineScreen.PlayRevealSequence stage 1/2/3 cadence.
- Printed MOVE/MOD/stats-box/rarity-wells come from the front sprite; stamps are overlays that never obscure printed labels.
- Rarity colors from ProgressionFolioAuthoringUtility FolioRarityColor table (centralize helper).
- Card entrance: slide from top + random settle tilt; ease both Y and Z.
- Per stamp: keep shake + flash; flash must use librarycard_outline; add ink alpha fade-in after impact.
- Empty mod slots: no ghost text; leave wells empty.
- STAMP CONTINUITY (author-locked): when stamping a Known move, Prepare must show the same card already filled with type icon, move name, stats, and MOVE rarity. Stamp stages only animate new mod rarity/name ink - never restamp or clear host fields.
- VISUAL PARITY (author-locked): ResultPanel move/mod/stats ink must match NFT bake stamp texture + layout.json positions/tilts - not clean undressed TMP as the ship look. Prefer stamp-strip sprites from the bake ink pipeline, or a stamp-distress material audited against smoke PNGs. layout.json is layout source of truth; ink.json defines stamp character.

Deliver: prefab wiring, ResultView/presenter code, playmode-verifiable Known + Stamp reveals (Stamp looks like the same Known card returning for more stamps), with parity notes vs tools/library-card-render smokes.
```
