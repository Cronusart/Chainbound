# Academy Candy Machine — Landscape Build Plan

**Status:** Ready to build against  
**Target layouts:** `/workspace/chainbound-candy-wireframes-landscape/` (844×390 + 1280×720)  
**Unity root:** `Signal Jam 3D` (CandyMachineScreen + MenuInputContextController + Mobile HUD)

---

## 0. Goal

Ship a **landscape, HUD-first** Academy Candy Machine that makes two mechanisms obvious:

| Mode tab | Mechanism | Outcome |
|---|---|---|
| **FOLIO** | Get Known | New Known move, **0** modifiers |
| **STAMP** | Stamp once | Fill **next empty** modifier slot (max **2** total) |

No third peer tab (Reroll stays hidden/nested until spine says live).  
No on-panel **BACK** or **CONFIRM** chrome — HUD **B/Cancel** and **A/Submit** own those intents.

---

## 1. Non‑negotiables

### 1.1 Typography
- **Font asset (all TMP):** `PixeloidSans SDF`  
  Path: `Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset`  
  (User name: PixeloidSans_SDF — use the project’s existing SDF asset.)
- **All labels / text fields:** **ALL UPPERCASE** (set via copy helpers + TMP; do not rely on players typing casing).
- **Titles (bold):** use **PixeloidSans Bold** weight.
  - Prefer a dedicated `PixeloidSans-Bold SDF` TMP asset if/when authored from `Assets/Sprites/UI/PixeloidSans-Bold.ttf`.
  - Until Bold SDF exists: titles may use `PixeloidSans SDF` with `Font Styles = Bold` **only if** the atlas includes bold; otherwise author Bold SDF in Phase 0.
- **Title surfaces:** `ACADEMY CANDY MACHINE`, mode tab labels, section headers (`FOLIO BROWSE`, `STAMPABLE MOVES`, `STAMP A MOVE`), primary CTA label text.
- **Body / meta:** costs, status, odds, slot captions — uppercase, regular weight.

### 1.2 HUD / input (MenuInputContextController)
Already wired patterns to reuse:
- `Submit` → A / mobile A (`mobileAPressesSubmit`)
- `Cancel` → B / back (`OnCancel`)
- Sole Navigate/Submit/Cancel owner while candy is open (`InputLockReason = "CandyMachine"`)

**Rules for this panel:**
- **Do not** author a visible Back button (remove / never show `backButton` from `CandyMachineScreen.EnsureHierarchy`).
- **Do not** author a second Confirm button beside the primary CTA.
- Primary CTA is a **focused Selectable** whose label reads the action (`GET KNOWN` / `REVIEW STAMP` / `CONFIRM STAMP`) — **A/Submit activates the focused control**, which is the CTA when focus is on it.
- **B/Cancel** → step back (cancel review → close machine) via `inputContext.OnCancel` only.
- Hold navigation during open/close DOTween (`SetNavigationHeld` pattern already on MenuInputContextController).

### 1.3 Landscape + HUD safe area
- Design res: **1280×720** game frame; phone landscape reference **844×390**.
- Content inset: clear **bottom-left D-pad** and **bottom-right YXAB** thumb zones (~160px corners). Prefer a centered safe content band; `SafeAreaAdapter` stays on root.
- Portrait stack wireframes are reference only — **do not ship portrait as primary.**

### 1.4 Taxonomy / copy lock
- Badges: `KNOWN` / `STAMPED` only (never Ordinal / Inscription / Rune / Bound-as-move).
- Stamp UI: read-only slot meters `[●][○]` — **no** `1 MOD` / `2 MODS` pickers.
- One stamp action fills **next empty slot only**; `2/2` rows are non-selectable / locked.

---

## 2. Target layout (from wireframes)

```
┌─ TITLE (BOLD) ─ RAM ──────────── [ FOLIO ] [ STAMP ] ─┐
│ LEFT ~40%                  │ RIGHT ~55%                 │
│ browse / list / chips      │ detail + slots + CTA label │
│                            │ status (read-only)         │
└────────────────────────────┴────────────────────────────┘
   [D-PAD]                                      [Y][X]
   (ghost / real HUD)                           [A][B]
```

| Region | Folio | Stamp (default) | Stamp (carousel variant) |
|---|---|---|---|
| Left | Odds + what you get + optional recent carousel | Vertical **stampable moves list** + meters | Optional: thin summary only |
| Top of content | — | — | **Horizontal move chip carousel** |
| Right | Preview card + cost + `GET KNOWN` | Selected move + slot cards + `STAMP` / review | Same detail pane |

**Default Stamp presentation:** vertical list.  
**Carousel:** enable when stampable count **> 8–12**, or via presentation toggle — same data, different browse chrome.

---

## 3. Architecture — what to build / change

### 3.1 Prefer evolving `CandyMachineScreen` (UGUI)
Keep `SignalJam.UI.MenuNavigation.CandyMachineScreen` as the runtime owner (already has `CandyMachineMode`, services, Review→Confirm). Refactor presentation:

| Remove / stop shipping | Add |
|---|---|
| Landscape 3-column Mode\|List\|Actions as primary | Landscape two-column safe content |
| `backButton` visible | Cancel → HUD only |
| `modifierOneButton` / `modifierTwoButton` | `StampSlotMeter` (2 slots, next-empty highlight) |
| Desktop-first anchors | HUD-safe anchors + focus graph |

Legacy `VendingMachineController` tabs (`Roll`/`Upgrade`/`Reroll`) map conceptually to Folio/Stamp; **do not** dual-ship two candy UIs. Candy Machine panel is the player destination; vending remains legacy until migrated.

### 3.2 New / split types (suggested)
```
CandyMachineScreen.cs              — orchestration, modes, services, HUD cancel/submit
CandyMachineLandscapeLayout.cs     — anchors, show/hide columns, safe bounds
CandyMachineModeTabs.cs            — FOLIO | STAMP segmented control + focus
CandyMachineMoveListView.cs        — vertical stampable list + row view
CandyMachineMoveCarouselView.cs    — horizontal chip carousel
CandyMachineStampSlotMeter.cs      — [1][2] filled/empty/next UI
CandyMachineDetailPane.cs          — preview, cost, status, CTA Selectable
CandyMachineTextStyle.cs           — PixeloidSans SDF + Bold titles + ToUpperInvariant helpers
CandyMachineTweens.cs              — DOTween sequences (open/close, tab, list, carousel, stamp)
```

### 3.3 Prefab / authoring
- Update `CandyMachineAuthoring` (`Tools/Chainbound/UI/Author Academy Candy Machine`) to emit **landscape** hierarchy matching §2.
- Prefab: `Assets/Prefabs/UI/CandyMachine/CandyMachinePanel.prefab`
- Assign `PixeloidSans SDF` (and Bold SDF) on all TMP in authoring so rebuilds stay consistent.

---

## 4. Carousels — explicit spec

### 4.1 Folio — “Recent pulls” carousel (secondary)
- **Axis:** Horizontal.  
- **Items:** Last N Known grants (e.g. 6–8 chips): move name uppercase + `KNOWN` badge.  
- **Nav:** When focus is in Recent region — D-pad **L/R** moves selection; **U/D** leaves carousel to odds/detail/CTA.  
- **Does not** drive the primary roll (roll stays cost + CTA). Recent is history, not a picker for Get Known.  
- **Overflow:** Peek next chip + optional `+N` overflow pill (non-focusable or focuses “more” → opens vertical history list later).

### 4.2 Stamp — Move chip carousel (alternate browse)
- **Axis:** Horizontal, full content width under mode tabs.  
- **Item chrome:** `MOVE NAME` + mini meter `[●][○]` + locked state for `2/2`.  
- **Selection:** Center or highlighted chip = selected Known/Stamped instance; detail pane binds to selection.  
- **Nav:** Focus in carousel → **L/R** changes chip (wrap optional: **no wrap** preferred to avoid mis-stamps); **U** to mode tabs; **D** to detail / next-empty / CTA.  
- **Skip locked:** L/R skips `2/2` chips (or lands on them read-only without enabling CTA).  
- **SFX:** `PlayerMenuSfxCue.CarouselMove` (already in `PlayerMenuSfxProfileSO` / `UiSfxRouter`).  
- **Pooling:** Recycle chip views; bind from `IKnownMoveService` / stampable query.  
- **Sync:** If list mode and carousel mode share selection id — switching presentation keeps the same selected move.

### 4.3 Stamp — Vertical list (default browse)
- Not a carousel; `ScrollRect` + vertical layout.  
- Row: `NAME` · meter · `0/2|1/2|2/2`.  
- **U/D** moves row; ensure selected row stays visible (`ScrollRect.EnsureVisible`).  
- Same selection contract as carousel.

### 4.4 Presentation switch
```
if (stampableCount >= carouselThreshold)  // default 10
    show carousel browse
else
    show list browse
```
Authorable threshold on `CandyMachineDevConfigSO` or layout component.

---

## 5. DOTween plan (fit the landscape shell)

Reuse patterns from `ItemObtainPanelReveal` (Sequence, unscaled time, OutCubic / OutBack, CanvasGroup fades). Centralize in `CandyMachineTweens`; kill sequences on disable.

| Moment | Tween | Notes |
|---|---|---|
| **Open overlay** | Dimmer `DOFade` 0→1; panel `DOScale` X then Y (ItemObtain-style) **or** `DOAnchorPos` slide from slight Y+ with fade | Hold nav until complete |
| **Close** | Reverse fade + scale down; then deactivate; release input lock | Cancel during tween still closes |
| **Mode tab FOLIO ↔ STAMP** | Crossfade left/right column `CanvasGroup`s (120–180ms OutQuad); optional 8–12px horizontal nudge of content | Play navigate SFX; reset focus to tab then default body |
| **List row select** | Selection chrome: frame `DOFade` / thin `DOScale` pulse 1→1.04→1 (80–100ms) | Don’t move layout hard |
| **Carousel chip change** | Selected chip `DOScale` 1→1.08; neighbors 1.0; content `DOAnchorPosX` snap to chip (OutCubic 0.15–0.2s) | Fire `CarouselMove` SFX |
| **Stamp slot “next empty”** | Empty slot dashed frame soft `DOFade` pulse loop while Stamp mode + selectable | Kill loop on leave mode / 2/2 |
| **Stamp success** | Slot fill: flash + `DOScale` pop; optional short shake on detail card; then hand off to **ItemObtain** / result toast if used | Don’t block Cancel recovery |
| **Get Known success** | Preview card punch scale + fade-in name; reuse ItemObtain ceremony when mint/grant returns | Matches existing obtain language |
| **CTA focus gained** | Subtle label brightness / scale 1.02 — keep readable on Pixeloid | Focused Selectable only |
| **Disabled CTA** | No loop animations; grey uppercase label | |

**Rules:** Always `SetUpdate(true)` for menu (unscaled). Kill with `Kill(false)` on Close. Never animate HUD d-pad/buttons (those are MobileHud services).

---

## 6. Focus graph (landscape)

```
1 Mode tabs          ← L/R switches FOLIO|STAMP
2 Browse             ← list U/D  |  carousel L/R
3 Next empty slot    ← optional focus stop (read-only amplify); skip if Folio
4 Primary CTA        ← A/Submit activates
5 Status             ← never focusable
```

- **A/Submit:** `SubmitSelected()` on focused Selectable (row select, tab, CTA).  
- **B/Cancel:** review pending → clear review; else `Close()`.  
- Default on open: **FOLIO** tab (existing `SetMode(GetKnown)`).  
- After mode switch to Stamp: focus first stampable row/chip.

---

## 7. Build phases

### Phase 0 — Prep (0.5 day)
- [ ] Confirm / author **PixeloidSans-Bold SDF** TMP from Bold TTF if needed.  
- [ ] Add `CandyMachineTextStyle` helper (`ApplyTitle`, `ApplyLabel`, force uppercase).  
- [ ] Inventory sample art slots (user photos) for preview/chip icons — placeholders OK.  
- [ ] Freeze wireframe acceptance: land-01…04 + 1280×720.

### Phase 1 — Shell + typography + HUD (1–2 days)
- [ ] Rebuild prefab landscape two-column via authoring.  
- [ ] Apply PixeloidSans SDF everywhere; bold titles; all caps.  
- [ ] Wire `MenuInputContextController` only for Cancel/Submit; **remove visible Back**.  
- [ ] Mode tabs FOLIO|STAMP; hide mod pickers.  
- [ ] Safe bounds vs HUD thumbs; sorting order 860 unchanged.  
- [ ] Open/close DOTween + nav hold.

### Phase 2 — Folio mode (1 day)
- [ ] Odds / what-you-get / cost bind from `VendingConfigSO` / `DevCandyMachineInventory`.  
- [ ] Primary CTA → Get Known (existing async path).  
- [ ] Optional recent-pulls carousel (§4.1).  
- [ ] Success → ItemObtain or inline preview tween.

### Phase 3 — Stamp list + slots (1–2 days)
- [ ] Stampable query (Known `0/2` + Stamped `1/2`; exclude `2/2` from action).  
- [ ] Vertical list + meters.  
- [ ] Slot meter UI; **force next-empty only** (API: prefer `requestedModifierCount = 1` per pull, or stamp-into-next-slot service contract — align with Ownership Spine; UI never offers double-stamp).  
- [ ] REVIEW → CONFIRM still via **same CTA Selectable** + A (label swaps); B cancels review.  
- [ ] Slot pulse + success tweens.

### Phase 4 — Stamp carousel variant (1 day)
- [ ] Chip carousel (§4.2) + threshold switch.  
- [ ] CarouselMove SFX + snap tweens.  
- [ ] Parity tests: same selection id as list mode.

### Phase 5 — Polish + gates (1 day)
- [ ] ClosedBeta / spine: Stamp disabled or “COMING” copy when services null.  
- [ ] EditMode tests: mode switch, no Back button present, uppercase labels, carousel threshold.  
- [ ] Playmode pass on device/PSG1: thumbs don’t cover CTA; A/B only.

---

## 8. Acceptance criteria

1. Landscape only feels correct inside existing HUD frames (1280×720).  
2. Player always sees **exactly two** mode tabs: FOLIO | STAMP.  
3. All visible text uses **PixeloidSans SDF**, **uppercase**; titles **bold**.  
4. **No** panel Back / Confirm buttons; B closes/cancels; A activates focused control / CTA.  
5. Stamp cannot apply two modifiers in one candy action; meters show 0/2–2/2.  
6. Lists handle long libraries; carousel available for long Stamp sets.  
7. DOTween open/tab/carousel/stamp feel snappy; Cancel never soft-locks.  
8. Dev grants strip remains editor/dev-only.

---

## 9. Open items (waiting on you)

- Sample photos / move icon refs for chips + preview cards.  
- Confirm Bold SDF authoring vs FontStyles.Bold.  
- Final stamp service contract: “one modifier per candy stamp” vs spine’s historical “1 or 2 mods in one Stamp tx” — **UI plan assumes one slot per action** as you specified; backend may need a follow-up to match.

---

## 10. Reference map

| Asset | Role |
|---|---|
| `land-01-folio-*.png` | Folio columns |
| `land-02-stamp-list-*.png` | Default Stamp |
| `land-03-stamp-carousel-*.png` | Carousel Stamp |
| `land-04-focus-map-*.png` | Focus + HUD |
| `CandyMachineScreen.cs` | Current runtime |
| `MenuInputContextController.cs` | Submit/Cancel owner |
| `ItemObtainPanelReveal.cs` | DOTween reference |
| `PlayerMenuSfxCue.CarouselMove` | Carousel audio |
