# Bound Encyclopedia Book Presentation

**Status:** Presentation method for the Bound encyclopedia  
**Date:** 2026-10-03  
**Applies to:** The 3D book shown for a Bound Chainling, its cover atlas, and its page turn

This spec does not change the Bind recipe, cover consumption, or dossier data. Those stay in `Docs/Specs/UI/CHAINLING_ARCHIVE_ENCYCLOPEDIA.md` and `Docs/Specs/Solana/CASE_BINDING_COVER_ART_DROPS_v1.md`.

## 0. What this object is

A Bound encyclopedia is one book object with two presentations of the same root: a closed cover, and an open book the player reads. Opening it does not push another screen.

The mesh is not in the project yet. It is a new Crocotile model, exported as its own OBJ, with UV0 and a material slot laid out for this book. Unity does not unwrap it.

`Assets/Sprites/World/Academy/Blue_Book.obj` is scenery in `Academy_Interior`. Its UV0 samples the shared academy atlas through `Academy_Scaled.mat`. `new_academy.crocotile` is that scenery source. Do not re-UV that prop, do not measure the cover template from it, and do not assign encyclopedia art onto `Academy_Scaled`.

The unbound dossier stays the paper stack it is today. The Bound reading view hides that stack and shows this book. Binding, focus, and page order stay on the dossier.

## 1. Cover mesh and UV0

Author the encyclopedia in Crocotile as a closed book and an open spread. Export OBJ. Import with the existing ModelImporter contract:

- `swapUVChannels: 0`
- `generateSecondaryUV: 0`
- `externalObjects` remaps material slot 0 to a new book material, not the academy material

Required UV0 islands, each non-overlapping and upright:

| Island | Rule |
|---|---|
| Front board | Own island. Hinge toward the spine. No mirrored lettering. |
| Back board | Own island. Hinge toward the spine. |
| Spine | Own island. Lettering upright along the spine. |
| Page block | Head, tail, and fore-edge. Paper texture, not partner art. |
| Board rims | Thickness faces. Neutral binding trim, not partner art. |

Bevels get an explicit island. A face with no UV must not fall onto a board.

Do not use triplanar projection, a Decal Projector, a Unity-generated unwrap, or lightmap UV1. The OBJ already has UV0.

## 2. Cover PNG

Partner and first-party art is an opaque sRGB atlas whose islands match the exported UV0. A portrait of the front cover is not a book texture. The closed book also samples the back, the spine, and the edges. Unity cannot invent those regions from a front-only image.

Write the pixel rectangles only after the mesh exists:

1. Measure each board and the spine in model units.
2. Pick one texel density for the front, the back, and the spine, so the hinge does not change scale.
3. Pack those islands with a gutter at least twice the bleed. Bleed is edge-color extrusion past each island, including corners, so bilinear filtering does not pull in the neighbor.
4. Inset a safe rectangle from the outer edges, and a larger inset along the hinge. Titles, logos, faces, and marks stay inside it. Background art runs through the island and the bleed.
5. Put page-block and rim art in the remaining trim islands. Those pixels are the same in every submission.
6. Check the smallest size the book is shown at. If mips mix two islands, reduce mip use. Do not crop the atlas into a thumbnail.

Partner art replaces the front, back, and spine. Trim stays the template. The texture assigned at runtime is the full atlas, including that unchanged trim.

## 3. Assigning a cover

1. Import the encyclopedia OBJ and remap slot 0 to the book material.
2. Create a material instance per displayed book. Two books on screen are two instances.
3. Load the image through the validated, size-limited, cached path in the cover-art spec.
4. Assign it to the URP base map (`_BaseMap`) at scale `(1, 1)` and offset `(0, 0)`.
5. Leave the mesh and UV0 alone while the player browses.

Before Bind, the preview reads the owned `cb.kind = book_cover` asset. After Bind, the cover is consumed. The encyclopedia then uses the cover recorded on the archive (`cb.book_cover_asset` and the catalog entry), not a live hold on the burned asset. The cover remains cosmetic.

Page surfaces use a second material. They sample the dossier RenderTexture, not the cover atlas. The cover atlas remains boards, spine, page-block edges, and rims.

## 4. Why the current turn looks like a stack

`ChainlingDossierPageTurnRelay` calls `ChainlingDetailScreen.FlipPage` when left or right is not already a navigation target. `FlipPage` picks the next available sheet, wraps, and calls `PlaySheetTurn`. That sequence fans the `DossierPageStack` rects, slides the front sheet off in X, tilts it on Z, then `SetAsFirstSibling` and settles the pile. `EnableFrontPageContent` shows only the front sheet.

Those sheets are flat UI rectangles in one plane. The Z tilt is a card lean. Reordering siblings is a sort. Nothing hinges on a spine, and the back of the departing page is never shown. That stays the unbound dossier. It is not the Bound page turn.

Sheet order stays Summary, Moves, Learnable, Style, Growth, then BookPage. BookPage is the index. `FlipPage` remains the only page index.

## 5. Page turn that reads as a flip

The open book shows the current dossier sheet on the page meshes. A UI camera renders that existing hierarchy into a RenderTexture. Do not rebuild the labels, and do not reparent the dossier canvas onto the mesh. Controller focus stays on the dossier Selectables. A touch on the page is a raycast into the page UV, mapped back into the source canvas.

Use two RenderTextures during a turn:

| Texture | What it shows |
|---|---|
| Outgoing | The sheet that is leaving, captured before the swap |
| Incoming | The sheet `FlipPage` is opening |

A static page underneath already shows the incoming texture, so the lift reveals the next sheet. One turning leaf hinges on the spine. Its page UV is `u = 0` at the spine and `u = 1` at the fore-edge. A vertex bend curls that leaf around the spine: the fore-edge lifts, the leaf passes through a half-cylinder, then it flattens on the other side. The front of the leaf samples the outgoing texture. The back samples the incoming texture. Past halfway the camera sees the back, which is why the page turns over instead of being replaced.

Drive that curl with one unscaled 0–1 parameter for the same duration as `PlaySheetTurn` (fan, slide, settle). When the curl passes halfway, run the existing swap: `SwitchToPage`, then `EnableFrontPageContent`. The incoming texture updates from that sheet. On settle, the leaf lies flat, the resting page keeps the incoming texture, and the outgoing texture is released. `SelectFirstPageControl` still restores focus.

`FlipPage(+1)` curls the leaf one way. `FlipPage(-1)`, including Back to the index, curls it the other way. Do not add a second index, and do not turn `PlaySheetTurn` into the visible motion of the Bound book. The stack stays hidden for this presentation. The curl is a runtime bend of a flat page whose rest pose matches the open page. The artist does not animate it in Crocotile.

A world-space canvas parented to the page is the wrong display. It fights the authored layout, clipping, and readable size. A skinned curl of the whole book is unnecessary. The bend is one leaf.

## 6. Placement

The closed cover and the open spread both sit in Profile A `PrimaryCenter` from `Docs/Specs/UI/HANDHELD_HUD_SAFE_AREAS_AND_VISUAL_LANGUAGE.md`:

- Anchors min `(0.206, 0.014)`, max `(0.794, 0.951)`
- Source grid about x 66–253, y 7–141 on 320×144

Keep text, focus, and hit targets inside that rect, and keep both lower corners clear of the controller HUD. The reading pose is nearly face-on. Size the RenderTexture from the projected page, and redraw it when the visible sheet changes, scrolls, or animates.

## 7. Unchanged

- Bind still consumes one owned cover (`cb.kind = book_cover`) and exactly one Stamped move or cosmetic skin, with the configured RAM cost and the owner's signature, in one transaction.
- Confirmed authoritative state decides when the book is shown as Bound.
- Dossier data binding, capture-time base skin, and live gameplay authority stay as they are.
- Player copy says Bound, book cover, and encyclopedia.

## 8. Acceptance

- [ ] The encyclopedia mesh is a new OBJ. `Blue_Book` and `Academy_Scaled` are unchanged.
- [ ] Front, back, and spine are separate UV0 islands. A front-only PNG is rejected.
- [ ] Pixel rectangles are measured from that mesh. They are not copied from the academy prop.
- [ ] Two books on screen show two covers from two material instances.
- [ ] A page turn curls one leaf about the spine, shows the back of the leaving sheet, and reveals the next sheet underneath.
- [ ] `FlipPage` is still the page index. Left/right and Back still reach the same sheets, including BookPage.
- [ ] The visible book stays inside `PrimaryCenter` with the handheld HUD up.

## 9. Decision log

| Date | Decision |
|---|---|
| 2026-10-03 | The Bound book is a new Crocotile mesh. The academy `Blue_Book` prop is not that mesh. |
| 2026-10-03 | Cover art is a UV0 atlas on a per-book material instance. No decal, triplanar, or generated unwrap. |
| 2026-10-03 | The Bound page turn is a spine-hinged leaf over dossier RenderTextures. The unbound dossier keeps its paper-stack turn. |
