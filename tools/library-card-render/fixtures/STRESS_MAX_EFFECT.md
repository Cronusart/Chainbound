# Stress fixture: max-length effect text

`stress_max_effect.json` is a bake stress case for the library-card **ROLE / EFFECT** stats label (non-attack front).

## What it is
- Real move: **Immutable Ledger** (`immutable_ledger`) from `Assets/Data/Combat/Moves/Move_immutable_ledger.asset`
- Full `description` / `effectLine` (**77** characters) — longest player-facing effect string among catalog moves that use the effect/stats area (passives / support / status-utility; attacks draw POWER/ACCURACY instead)

## Visual check
Render and inspect the effect label:
- **Wrap expected:** up to `effect_max_lines` (2) under the same left x as line 1 (`stats_power_x`). Line 1 origin stays `stats_power_x` / `stats_y + 8` (bake) — do not shift the top line.
- Line 2 is directly underneath (`y += effect_line_height`, scaled with fitted font size).
- Word-wrap at spaces with a slight preference for breaks after `;`. Font may still shrink (min 18) if a wrapped line exceeds `name_max_width` (920).
- For Immutable Ledger, expect **2 lines** at a readable size (typically ~32), not a single clipped line at 18.

Relevant layout knobs: `stats_y`, `stats_power_x`, `name_max_width`, `effect_font_size`, `effect_max_lines`, `effect_line_height`, `rotations.stats`. Unused on front: `effect_origin`, `effect_font_size_card`.

Unity mirror: `LibraryCardLayout.FormatStatsInk` inserts `\n` via the same wrap; `LibraryCardStampStrip.Bake` rasterizes multi-line stamp strips; first line stays at `StatsOrigin`.

## How to render
```powershell
py -3.11 tools\library-card-render\render.py --smoke
# or only this fixture:
py -3.11 tools\library-card-render\render.py --input tools\library-card-render\fixtures\stress_max_effect.json --out-dir tools\library-card-render\out --stem smoke_front_stress_max_effect
```
