# Library card renderer

Python + Pillow compositor for Moves Library Card v1.

Bakes `Assets/Sprites/UI/PixeloidSans.ttf` into PNGs. Does **not** touch Stamp Prepare/Execute or Unity flags.

## Setup

```powershell
py -3.11 -m pip install -r tools\library-card-render\requirements.txt
py -3.11 tools\library-card-render\render.py --smoke
```

For an optional animated front-card preview, add `--animate scanline`; this keeps the static PNG outputs unchanged and writes a looping 1280px GIF.

## Config knobs

### `layout.json`
- Positions / sizes: `name_origin`, `stats_*`, `mod_*`, `type_inner`, back text
- Mod box padding: `mod_box_pad_x`, `mod_box_pad_y`, `mod_box_min_width`, `mod_box_stroke`
- Horizontal stats gap: `stats_gap` (string between POWER and ACCURACY)
- Non-attack ROLE / EFFECT wrap: `effect_font_size`, `effect_max_lines` (default 2), `effect_line_height` (px at nominal effect size; scales with fitted size). Line 1 origin unchanged; line 2 same x, y += line height.
- Per-label tilt (degrees): `rotations.name`, `.stats`, `.mod1`, `.mod2`, `.type_icon`, `.back`
- Rarity wells: `rarity_wells.move`, `.mod1`, `.mod2` and `rarity_dot_inset`
- Folio dot colors are `LibraryCardRarityColors` 0-1 channels times 255, rounded (`FOLIO` in `render.py`)

Empty mod slots are left blank. The bake does not draw ghost `MOD #N` text. Each filled mod is one stamp on that slot's line: display name, then the effect, separated by `stats_gap`. Rarity dots are stamped only into filled wells.

### `ink.json`
- Stamp distress: `noise_max`, `speckle_chance`, `edge_dilate` / `edge_erode`, `density_amp`, colors
- Sticker look (type icon only): `sticker_outline`, `sticker_outline_px`, `sticker_noise_max`, `sticker_speckle`, `sticker_shadow_*`

Mod boxes are drawn + stamped with the same ink pipeline as the glyphs (no flat undressed PNG frame).
Type icon is a sticker pass (outline + light wear + tilt), not stamped ink.

## Fixtures
- `known_0mod.json`, `stamp_1mod.json`, `stamp_2mod.json`, `stamp_back.json`
- `stress_max_effect.json` — longest catalog `effectLine` (Immutable Ledger, 77 chars); see `fixtures/STRESS_MAX_EFFECT.md` for the wrap/overflow visual check
