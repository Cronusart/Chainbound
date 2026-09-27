"""Composite Moves Library Card PNGs (PixeloidSans + stamped-ink glyphs).

Layout / ink knobs live beside this script:
  tools/library-card-render/layout.json
  tools/library-card-render/ink.json

Edit those, then re-run --smoke.

Run from repo root:
  py -3.11 tools/library-card-render/render.py --smoke
"""
from __future__ import annotations

import argparse
import json
import math
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageSequence

REPO = Path(__file__).resolve().parents[2]
TOOL = Path(__file__).resolve().parent
KIT = REPO / "Docs/Art/MovesLibraryCard"
TEMPLATES = KIT / "templates"
LAYERS = TEMPLATES / "layers"
EXAMPLES = KIT / "examples"
FONT_PATH = REPO / "Assets/Sprites/UI/PixeloidSans.ttf"
CARD_FRONT = REPO / "Assets/Sprites/UI/LibraryCards/librarycard_front.png"
SCANLINE_BG = REPO / "Assets/Sprites/UI/LibraryCards/card_scanline_bg.gif"
CARD_CONTENT_HEIGHT = 640

# PartyTabMoveRarityPalette is authored in 0-1. Bake dots use round(channel * 255).
# Keep in lockstep with LibraryCardRarityColors / PartyTabMoveRarityPalette, not Folio muted hues.
FOLIO = {
    "Common": (184, 194, 204, 255),
    "Uncommon": (107, 224, 133, 255),
    "Rare": (107, 173, 250, 255),
    "Epic": (184, 122, 250, 255),
    "Legendary": (250, 184, 71, 255),
    "Mythic": (250, 107, 148, 255),
}
LAYOUT = json.loads((TOOL / "layout.json").read_text(encoding="utf-8-sig"))
INK_CFG = json.loads((TOOL / "ink.json").read_text(encoding="utf-8-sig"))


def rgba(key: str, default: list[int] | None = None) -> tuple[int, int, int, int]:
    c = INK_CFG.get(key, default)
    if c is None:
        raise KeyError(key)
    return int(c[0]), int(c[1]), int(c[2]), int(c[3]) if len(c) > 3 else 255


SAGE = rgba("sage")
INK = rgba("ink")
MOD_GHOST = rgba("mod_ghost")
STICKER_OUTLINE = rgba("sticker_outline", [245, 240, 228, 255])


def load_font(size: int) -> ImageFont.FreeTypeFont:
    if not FONT_PATH.is_file():
        raise FileNotFoundError(f"PixeloidSans TTF missing: {FONT_PATH}")
    return ImageFont.truetype(str(FONT_PATH), size=size)


def fit_font(draw: ImageDraw.ImageDraw, text: str, size: int, max_width: int) -> ImageFont.FreeTypeFont:
    while size >= 18:
        font = load_font(size)
        bbox = draw.textbbox((0, 0), text, font=font)
        if bbox[2] - bbox[0] <= max_width:
            return font
        size -= 2
    return load_font(18)


def text_width(draw: ImageDraw.ImageDraw, text: str, font: ImageFont.ImageFont) -> int:
    bbox = draw.textbbox((0, 0), text, font=font)
    return max(0, bbox[2] - bbox[0])


def choose_wrap_lines(text: str, max_lines: int = 2) -> list[str]:
    """Word-wrap into at most max_lines. Prefer a mid break; slight bias after ';'."""
    text = (text or "").strip()
    if not text:
        return [""]
    max_lines = max(1, int(max_lines))

    # Keep "ROLE / EFFECT  " (two spaces) on line 1; wrap the effect body only.
    prefix = ""
    body = text
    marker = "ROLE / EFFECT"
    if text.upper().startswith(marker):
        after = text[len(marker) :]
        # Preserve the authored gap (usually two spaces) after the label.
        gap_len = len(after) - len(after.lstrip(" "))
        gap = after[:gap_len] if gap_len > 0 else "  "
        prefix = marker + gap
        body = after.lstrip(" ")

    words = body.split()
    if max_lines == 1 or len(words) <= 1:
        return [text]

    # Repeatedly split the longest remaining segment until max_lines.
    lines = [body]
    while len(lines) < max_lines:
        idx = max(range(len(lines)), key=lambda i: len(lines[i]))
        target = lines[idx]
        parts = target.split()
        if len(parts) < 2:
            break
        best_i = 1
        best_score = None
        # When splitting the first body line, count the ROLE / EFFECT prefix on the left.
        left_pad = len(prefix) if idx == 0 else 0
        for i in range(1, len(parts)):
            left = " ".join(parts[:i])
            right = " ".join(parts[i:])
            score = abs((left_pad + len(left)) - len(right))
            if left.endswith(";"):
                score -= 8  # prefer wrapping after a clause semicolon
            if best_score is None or score < best_score:
                best_score = score
                best_i = i
        left = " ".join(parts[:best_i])
        right = " ".join(parts[best_i:])
        lines[idx : idx + 1] = [left, right]

    if prefix:
        lines[0] = prefix + lines[0]
    return lines


def fit_wrapped_font(
    draw: ImageDraw.ImageDraw,
    text: str,
    size: int,
    max_width: int,
    max_lines: int,
) -> tuple[ImageFont.FreeTypeFont, list[str], int]:
    """Shrink font until wrapped lines each fit max_width. Returns font, lines, size."""
    lines = choose_wrap_lines(text, max_lines)
    while size >= 18:
        font = load_font(size)
        if all(text_width(draw, line, font) <= max_width for line in lines):
            return font, lines, size
        size -= 2
    font = load_font(18)
    # Last resort: greedy pack at min size (may still overflow a single token).
    packed: list[str] = []
    words = text.split()
    current: list[str] = []
    for word in words:
        trial = word if not current else f"{' '.join(current)} {word}"
        if current and text_width(draw, trial, font) > max_width and len(packed) + 1 < max_lines:
            packed.append(" ".join(current))
            current = [word]
        else:
            current.append(word)
    if current:
        packed.append(" ".join(current))
    if not packed:
        packed = [text]
    if len(packed) > max_lines:
        head = packed[: max_lines - 1]
        tail = " ".join(packed[max_lines - 1 :])
        packed = head + [tail]
    return font, packed, 18


def folio_color(name: str | None) -> tuple[int, int, int, int]:
    key = str(name or "Common").strip().title()
    return FOLIO.get(key, FOLIO["Common"])


def rotation(key: str) -> float:
    return float((LAYOUT.get("rotations") or {}).get(key, 0.0))


def content_y_offset(image: Image.Image) -> int:
    """Center the authored 1280x640 card content inside a square source canvas."""
    return max(0, (image.height - CARD_CONTENT_HEIGHT) // 2)


def text_size(font: ImageFont.ImageFont, text: str) -> tuple[int, int]:
    bbox = font.getbbox(text)
    return bbox[2] - bbox[0], bbox[3] - bbox[1]


def blit_L(dst: Image.Image, src: Image.Image, xy: tuple[int, int]) -> None:
    x, y = xy
    sw, sh = src.size
    dw, dh = dst.size
    if x >= dw or y >= dh or x + sw <= 0 or y + sh <= 0:
        return
    sx0 = max(0, -x)
    sy0 = max(0, -y)
    sx1 = min(sw, dw - x)
    sy1 = min(sh, dh - y)
    dx0 = x + sx0
    dy0 = y + sy0
    crop = src.crop((sx0, sy0, sx1, sy1))
    region = dst.crop((dx0, dy0, dx0 + crop.width, dy0 + crop.height))
    dst.paste(ImageChops.lighter(region, crop), (dx0, dy0))


def draw_rotated_text_L(
    mask: Image.Image,
    text: str,
    origin: tuple[int, int],
    font: ImageFont.ImageFont,
    angle_deg: float,
    fill: int = 255,
) -> None:
    # getbbox top is often negative (above baseline). Drawing at (pad, pad)
    # without compensating clips descenders â€” offset by -left/-top so the
    # full ink fits, and pad enough for later stamp dilate/erode.
    left, top, right, bottom = font.getbbox(text)
    tw, th = max(1, right - left), max(1, bottom - top)
    pad = 16
    patch = Image.new("L", (tw + pad * 2, th + pad * 2), 0)
    ImageDraw.Draw(patch).text((pad - left, pad - top), text, font=font, fill=fill)
    if abs(angle_deg) > 0.01:
        patch = patch.rotate(angle_deg, resample=Image.Resampling.BICUBIC, expand=True)
    cx = origin[0] + tw / 2
    cy = origin[1] + th / 2
    x = int(round(cx - patch.width / 2))
    y = int(round(cy - patch.height / 2))
    blit_L(mask, patch, (x, y))


def draw_rotated_box_L(
    mask: Image.Image,
    box: tuple[int, int, int, int],
    stroke: int,
    angle_deg: float,
    fill: int = 255,
) -> None:
    x0, y0, x1, y1 = box
    w, h = max(1, x1 - x0), max(1, y1 - y0)
    pad = max(4, stroke + 2)
    patch = Image.new("L", (w + pad * 2, h + pad * 2), 0)
    ImageDraw.Draw(patch).rectangle(
        (pad, pad, pad + w - 1, pad + h - 1),
        outline=fill,
        width=max(1, stroke),
    )
    if abs(angle_deg) > 0.01:
        patch = patch.rotate(angle_deg, resample=Image.Resampling.BICUBIC, expand=True)
    cx = (x0 + x1) / 2
    cy = (y0 + y1) / 2
    x = int(round(cx - patch.width / 2))
    y = int(round(cy - patch.height / 2))
    blit_L(mask, patch, (x, y))


def stamp_glyphs(base: Image.Image, draw_fn, seed: int = 11, color: tuple[int, int, int, int] | None = None) -> Image.Image:
    """Draw white glyphs into a mask, distress, tint as rubber-stamp ink."""
    tint = color or INK
    mask = Image.new("L", base.size, 0)
    draw_fn(mask)

    dilate = int(INK_CFG.get("edge_dilate", 3))
    erode = int(INK_CFG.get("edge_erode", 3))
    if dilate % 2 == 0:
        dilate += 1
    if erode % 2 == 0:
        erode += 1
    mask = mask.filter(ImageFilter.MaxFilter(max(3, dilate)))
    for _ in range(max(1, int(float(INK_CFG.get("erode_passes", 1))))):
        mask = mask.filter(ImageFilter.MinFilter(max(3, erode)))

    noise = Image.new("L", base.size, 0)
    rng = random.Random(seed)
    npx = noise.load()
    mp = mask.load()
    w, h = base.size
    noise_max = int(INK_CFG.get("noise_max", 40))
    speckle_chance = float(INK_CFG.get("speckle_chance", 0.0))
    punch_min = int(INK_CFG.get("speckle_punch_min", 140))
    punch_max = int(INK_CFG.get("speckle_punch_max", 255))
    density_amp = float(INK_CFG.get("density_amp", 0.0))
    dxs = float(INK_CFG.get("density_x_scale", 0.03))
    dys = float(INK_CFG.get("density_y_scale", 0.05))
    phase = (seed % 97) * 0.17

    for y in range(h):
        for x in range(w):
            src = mp[x, y]
            if src < 8:
                continue
            wave = 0.5 + 0.5 * math.sin(x * dxs + phase) * math.sin(y * dys + phase * 1.3)
            density = max(0.25, 1.0 - density_amp * (1.0 - wave))
            n = rng.randint(0, noise_max)
            if rng.random() < speckle_chance:
                n = max(n, rng.randint(punch_min, punch_max))
            npx[x, y] = int(max(0, src - n) * density)

    ink = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ip = ink.load()
    npp = noise.load()
    for y in range(h):
        for x in range(w):
            a = npp[x, y]
            if a:
                ip[x, y] = (tint[0], tint[1], tint[2], a)

    sox, soy = INK_CFG.get("shadow_offset", [1, 1])
    shadow_a = float(INK_CFG.get("shadow_alpha", 0.0))
    if shadow_a > 0:
        shadow = ImageChops.offset(ink, int(sox), int(soy))
        shadow.putalpha(shadow.split()[3].point(lambda p: int(p * shadow_a)))
        return Image.alpha_composite(Image.alpha_composite(base, shadow), ink)
    return Image.alpha_composite(base, ink)


def paste_sticker_icon(card: Image.Image, icon_path: str) -> None:
    """Type icon as a tilted sticker: off-white outline, light surface wear, soft shadow."""
    if not icon_path:
        return
    path = Path(icon_path)
    if not path.is_absolute():
        path = (REPO / icon_path).resolve()
    if not path.is_file():
        return

    x0, y0, x1, y1 = [int(v) for v in LAYOUT["type_inner"]]
    y_shift = content_y_offset(card)
    y0 += y_shift
    y1 += y_shift
    iw, ih = x1 - x0, y1 - y0
    icon = Image.open(path).convert("RGBA").resize((iw, ih), Image.Resampling.NEAREST)

    outline_px = int(INK_CFG.get("sticker_outline_px", 5))
    alpha = icon.split()[3]
    ring = alpha
    for _ in range(max(1, outline_px)):
        ring = ring.filter(ImageFilter.MaxFilter(3))

    pad = outline_px + 4
    sticker = Image.new("RGBA", (iw + pad * 2, ih + pad * 2), (0, 0, 0, 0))
    ox = (sticker.width - iw) // 2
    oy = (sticker.height - ih) // 2

    paper = Image.new("RGBA", sticker.size, (0, 0, 0, 0))
    ring_full = Image.new("L", sticker.size, 0)
    ring_full.paste(ring, (ox, oy))
    pp = paper.load()
    rp = ring_full.load()
    rng = random.Random(91)
    noise_max = int(INK_CFG.get("sticker_noise_max", 22))
    speckle = float(INK_CFG.get("sticker_speckle", 0.06))
    for y in range(sticker.height):
        for x in range(sticker.width):
            if rp[x, y] < 8:
                continue
            n = rng.randint(0, noise_max)
            if rng.random() < speckle:
                n = max(n, rng.randint(40, 90))
            r, g, b, a = STICKER_OUTLINE
            pp[x, y] = (
                max(0, r - n // 4),
                max(0, g - n // 5),
                max(0, b - n // 6),
                max(0, a - n // 3),
            )

    sticker = Image.alpha_composite(paper, sticker)
    sticker.paste(icon, (ox, oy), icon)

    sp = sticker.load()
    for y in range(sticker.height):
        for x in range(sticker.width):
            r, g, b, a = sp[x, y]
            if a < 8:
                continue
            if rng.random() < speckle * 0.5:
                sp[x, y] = (r, g, b, max(0, a - rng.randint(20, 60)))

    sh_off = INK_CFG.get("sticker_shadow_offset", [3, 4])
    sh_a = float(INK_CFG.get("sticker_shadow_alpha", 0.28))
    sh_patch = Image.new("RGBA", sticker.size, (0, 0, 0, 0))
    sa = sticker.split()[3].point(lambda p: int(p * sh_a))
    sh_patch.paste((20, 24, 18, 255), (0, 0, sticker.width, sticker.height))
    sh_patch.putalpha(sa)

    angle = rotation("type_icon")
    if abs(angle) > 0.01:
        sticker = sticker.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)
        sh_patch = sh_patch.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)

    cx = (x0 + x1) / 2
    cy = (y0 + y1) / 2
    sx = int(round(cx - sticker.width / 2))
    sy = int(round(cy - sticker.height / 2))
    card.alpha_composite(sh_patch, (sx + int(sh_off[0]), sy + int(sh_off[1])))
    card.alpha_composite(sticker, (sx, sy))



def format_mod_effect(mod: dict) -> str:
    """Prefer stamp-locked exact magnitude; fall back to catalog range text."""
    if not isinstance(mod, dict):
        return ""
    override = str(mod.get("effectLine") or "").strip()
    if override:
        return override

    locked = bool(mod.get("effectValueLocked"))
    raw_val = mod.get("effectValue")
    if locked and raw_val is not None and str(raw_val).strip() != "":
        try:
            val = float(raw_val)
        except (TypeError, ValueError):
            val = None
        if val is not None:
            unit = str(mod.get("effectUnit") or "%").strip()
            noun = str(mod.get("effectNoun") or "").strip()
            # Keep integers clean: 7 not 7.0
            if abs(val - round(val)) < 1e-6:
                num = str(int(round(val)))
            else:
                num = f"{val:.2f}".rstrip("0").rstrip(".")
            sign = "+" if val >= 0 else ""
            core = f"{sign}{num}{unit}"
            return f"{core} {noun}".strip()

    return str(mod.get("effectDescription") or mod.get("shortEffect") or "").strip()


def format_mod_line(mod: dict) -> str:
    """One stamp: display name and effect share the slot's horizontal line."""
    name = str(mod.get("displayName") or "").strip().upper()
    if not name:
        return ""
    effect = format_mod_effect(mod).strip().upper()
    if not effect:
        return name
    gap = str(LAYOUT.get("stats_gap", "      "))
    return f"{name}{gap}{effect}"


def format_mod_label(mod: dict | None, slot: int) -> tuple[str, bool]:
    """Return (label, filled). Empty slots are not drawn."""
    if not mod or not format_mod_line(mod):
        return "", False
    return format_mod_line(mod), True


def render_front(data: dict) -> Image.Image:
    front_path = CARD_FRONT if CARD_FRONT.is_file() else TEMPLATES / "card_front_base.png"
    card = Image.open(front_path).convert("RGBA")
    paste_sticker_icon(card, str(data.get("typeIconPath") or ""))

    name = str(data["moveName"]).upper()
    y_shift = content_y_offset(card)
    name_xy = (int(LAYOUT["name_origin"][0]), int(LAYOUT["name_origin"][1]) + y_shift)
    name_angle = rotation("name")

    def draw_name(mask: Image.Image) -> None:
        probe = ImageDraw.Draw(Image.new("L", card.size))
        font = fit_font(probe, name, int(LAYOUT["name_font_size"]), int(LAYOUT["name_max_width"]))
        draw_rotated_text_L(mask, name, name_xy, font, name_angle)

    card = stamp_glyphs(card, draw_name, seed=hash(str(data.get("moveId", name))) % 997)

    stats_y = int(LAYOUT["stats_y"]) + y_shift
    stats_x = int(LAYOUT["stats_power_x"])
    stats_angle = rotation("stats")
    gap = str(LAYOUT.get("stats_gap", "      "))

    def draw_stats(mask: Image.Image) -> None:
        probe = ImageDraw.Draw(Image.new("L", card.size))
        max_w = int(LAYOUT.get("name_max_width", 920))
        if data.get("category") == "attack":
            power = data.get("power")
            acc = data.get("accuracy")
            acc_txt = "AUTO" if acc is None or acc <= 0 else str(int(acc))
            power_txt = "-" if power is None else str(int(power))
            line = f"POWER: {power_txt}{gap}ACCURACY: {acc_txt}"
            font = fit_font(probe, line, int(LAYOUT.get("stats_font_size", LAYOUT["value_font_size"])), max_w)
            draw_rotated_text_L(mask, line, (stats_x, stats_y), font, stats_angle)
        else:
            effect = (data.get("effectLine") or data.get("category") or "").upper()
            line = f"ROLE / EFFECT  {effect}"
            max_lines = int(LAYOUT.get("effect_max_lines", 2))
            base_size = int(LAYOUT["effect_font_size"])
            font, lines, used_size = fit_wrapped_font(probe, line, base_size, max_w, max_lines)
            # Line 1 keeps the same origin as before (stats_power_x, stats_y + 8).
            origin_y = stats_y + 8
            nominal = max(1, int(LAYOUT.get("effect_font_size", used_size)))
            line_h = int(LAYOUT.get("effect_line_height", 42))
            line_h = max(1, int(round(line_h * (used_size / nominal))))
            for i, wrapped in enumerate(lines):
                draw_rotated_text_L(
                    mask,
                    wrapped,
                    (stats_x, origin_y + i * line_h),
                    font,
                    stats_angle,
                )

    card = stamp_glyphs(card, draw_stats, seed=23)

    mods = {int(m["slot"]): m for m in data.get("mods") or []}
    mod_x = int(LAYOUT["mod_x"])

    for slot, y in ((1, int(LAYOUT["mod1_y"]) + y_shift), (2, int(LAYOUT["mod2_y"]) + y_shift)):
        mod = mods.get(slot)
        label = format_mod_line(mod) if mod else ""
        if not label:
            continue
        angle = rotation(f"mod{slot}")
        probe = ImageDraw.Draw(Image.new("L", card.size))
        max_mod_w = int(LAYOUT.get("name_max_width", 920))
        font = fit_font(probe, label, int(LAYOUT["mod_font_size"]), max_mod_w)

        def draw_mod(
            mask: Image.Image,
            _text=label,
            _origin=(mod_x, y),
            _font=font,
            _angle=angle,
        ) -> None:
            draw_rotated_text_L(mask, _text, _origin, _font, _angle)

        card = stamp_glyphs(card, draw_mod, seed=40 + slot)

    card = stamp_rarity_dots(card, data, mods)
    return card


def render_modifier_overlay(mod: dict, slot: int = 1) -> Image.Image:
    """Render canonical Pixeloid/distressed modifier ink onto a transparent square layer."""
    base = Image.open(CARD_FRONT).convert("RGBA")
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    label = format_mod_line(mod)
    if not label:
        return layer
    y_shift = content_y_offset(base)
    origin = (
        int(LAYOUT["mod_x"]),
        int(LAYOUT["mod1_y"] if slot == 1 else LAYOUT["mod2_y"]) + y_shift,
    )
    probe = ImageDraw.Draw(Image.new("L", base.size))
    font = fit_font(probe, label, int(LAYOUT["mod_font_size"]), int(LAYOUT.get("name_max_width", 920)))
    angle = rotation(f"mod{slot}")
    return stamp_glyphs(
        layer,
        lambda mask: draw_rotated_text_L(mask, label, origin, font, angle),
        seed=40 + slot,
    )


def render_rarity_overlay(rarity: str, slot: int = 1) -> Image.Image:
    """Render only a modifier rarity dot, allowing one shared layer per rarity."""
    base = Image.open(CARD_FRONT).convert("RGBA")
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    wells = LAYOUT.get("rarity_wells") or {}
    key = f"mod{slot}"
    if key not in wells:
        return layer
    x0, y0, x1, y1 = [int(v) for v in wells[key]]
    y_shift = content_y_offset(base)
    y0 += y_shift
    y1 += y_shift
    inset = int(LAYOUT.get("rarity_dot_inset", 8))
    return stamp_glyphs(
        layer,
        lambda mask: ImageDraw.Draw(mask).ellipse(
            (x0 + inset, y0 + inset, x1 - inset, y1 - inset),
            fill=255,
        ),
        seed=4 + slot,
        color=folio_color(rarity),
    )


def stamp_rarity_dots(card: Image.Image, data: dict, mods: dict) -> Image.Image:
    wells = LAYOUT.get("rarity_wells") or {}
    if "move" not in wells:
        return card
    inset = int(LAYOUT.get("rarity_dot_inset", 8))
    y_shift = content_y_offset(card)

    def draw_well(mask: Image.Image, key: str) -> None:
        x0, y0, x1, y1 = [int(v) for v in wells[key]]
        y0 += y_shift
        y1 += y_shift
        ImageDraw.Draw(mask).ellipse(
            (x0 + inset, y0 + inset, x1 - inset, y1 - inset),
            fill=255,
        )

    move_rarity = str(data.get("moveRarity") or "Common")
    card = stamp_glyphs(
        card,
        lambda mask: draw_well(mask, "move"),
        seed=3,
        color=folio_color(move_rarity),
    )
    for slot in (1, 2):
        mod = mods.get(slot)
        key = f"mod{slot}"
        if key not in wells or not mod or not str(mod.get("displayName") or "").strip():
            continue
        rarity = str(mod.get("rarity") or data.get("stampRarity") or move_rarity)
        card = stamp_glyphs(
            card,
            lambda mask, well=key: draw_well(mask, well),
            seed=4 + slot,
            color=folio_color(rarity),
        )
    return card


def render_back(data: dict) -> Image.Image:
    card = Image.open(TEMPLATES / "card_back_base.png").convert("RGBA")
    prov = data.get("provenance") or {}
    owners = list(prov.get("owners") or [])
    events = list(prov.get("events") or [])
    ox = int(LAYOUT["back_origin_x"])
    step = int(LAYOUT["back_line_step"])
    font_size = int(LAYOUT["back_font_size"])
    angle = rotation("back")

    def draw_back(mask: Image.Image) -> None:
        font = load_font(font_size)
        y = int(LAYOUT["back_origin_y"])
        lines: list[str | None] = ["PREVIOUS OWNERS"]
        if not owners:
            lines.append("â€”")
        else:
            lines.extend(str(o) for o in owners[:6])
        lines.append(None)
        lines.append("EVENTS")
        labels = {
            "rolled": "ROLLED BY",
            "stamped": "STAMPED BY",
            "transferred": "TRANSFERRED",
            "bound": "BOUND",
        }
        shown: set = set()
        for ev in events:
            label = labels.get(ev.get("type"), str(ev.get("type", "")).upper())
            lines.append(f"{label}  {ev.get('actor', '')}")
            shown.add(ev.get("type"))
        for reserved in ("transferred", "bound"):
            if reserved not in shown:
                lines.append(f"{labels[reserved]}  â€”")
        yy = y
        for i, line in enumerate(lines):
            if line is None:
                yy += 16
                continue
            wobble = angle + (0.35 if i % 2 == 0 else -0.25)
            draw_rotated_text_L(mask, line, (ox, yy), font, wobble)
            yy += step

    return stamp_glyphs(card, draw_back, seed=77)


def square_pad(card: Image.Image, underlay: Image.Image | None = None) -> Image.Image:
    # New authored fronts are already square with the card centered vertically.
    # Keep legacy 1280x640 sources compatible while avoiding a second offset.
    canvas = underlay.copy().convert("RGBA") if underlay is not None else Image.new("RGBA", (1280, 1280), SAGE)
    if card.size == canvas.size:
        canvas = Image.alpha_composite(canvas, card)
    else:
        canvas.paste(card, (0, max(0, (canvas.height - card.height) // 2)), card)
    return canvas


def scanline_frames(card: Image.Image) -> list[Image.Image]:
    if not SCANLINE_BG.is_file():
        raise FileNotFoundError(f"scanline GIF missing: {SCANLINE_BG}")
    frames: list[Image.Image] = []
    with Image.open(SCANLINE_BG) as gif:
        for frame in ImageSequence.Iterator(gif):
            frames.append(square_pad(card, frame.convert("RGBA")))
    return frames


def write_scanline_gif(card: Image.Image, out_dir: Path, stem: str) -> Path:
    frames = scanline_frames(card)
    gif_path = out_dir / f"{stem}_square_1280.gif"
    frames[0].save(
        gif_path,
        save_all=True,
        append_images=frames[1:],
        duration=140,
        loop=0,
        disposal=2,
        optimize=False,
    )
    return gif_path


def market_1024(square: Image.Image) -> Image.Image:
    return square.resize((1024, 1024), Image.Resampling.NEAREST)


def write_outputs(
    card: Image.Image,
    out_dir: Path,
    stem: str,
    also_market: bool,
    animate: str | None = None,
) -> list[Path]:
    out_dir.mkdir(parents=True, exist_ok=True)
    paths: list[Path] = []
    card_path = out_dir / f"{stem}.png"
    card.save(card_path)
    paths.append(card_path)
    if also_market:
        sq = square_pad(card)
        sq_path = out_dir / f"{stem}_square_1280.png"
        mk_path = (
            out_dir / "smoke_market_1024.png"
            if stem.startswith("smoke_front")
            else out_dir / f"{stem}_market_1024.png"
        )
        sq.save(sq_path)
        market_1024(sq).save(mk_path)
        paths.extend([sq_path, mk_path])
        if animate == "scanline":
            paths.append(write_scanline_gif(card, out_dir, stem))
    return paths


def render_input(
    path: Path,
    out_dir: Path,
    stem: str | None = None,
    animate: str | None = None,
) -> list[Path]:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    side = data.get("side") or "front"
    card = render_back(data) if side == "back" else render_front(data)
    return write_outputs(
        card,
        out_dir,
        stem or path.stem,
        also_market=(side == "front"),
        animate=animate,
    )


def smoke(out_dir: Path, animate: str | None = None) -> None:
    fixtures = TOOL / "fixtures"
    render_input(fixtures / "known_0mod.json", out_dir, "smoke_front_0mod", animate)
    render_input(fixtures / "stamp_1mod.json", out_dir, "smoke_front_1mod", animate)
    render_input(fixtures / "stamp_2mod.json", out_dir, "smoke_front_2mod", animate)
    render_input(fixtures / "stamp_back.json", out_dir, "smoke_back", animate)
    # Longest catalog effectLine stress case (ROLE / EFFECT label overflow check).
    render_input(fixtures / "stress_max_effect.json", out_dir, "smoke_front_stress_max_effect", animate)
    one = Image.open(out_dir / "smoke_front_1mod_square_1280.png").convert("RGBA")
    market_1024(one).save(out_dir / "smoke_market_1024.png")
    print(f"Smoke PNGs written to {out_dir}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Render Chainbound library cards")
    parser.add_argument("--input", type=Path)
    parser.add_argument("--out-dir", type=Path, default=EXAMPLES)
    parser.add_argument("--stem")
    parser.add_argument("--smoke", action="store_true")
    parser.add_argument(
        "--animate",
        choices=("scanline",),
        default=None,
        help="also write a looping scanline GIF for front cards (default: off)",
    )
    parser.add_argument("--extract-layers", action="store_true")
    args = parser.parse_args()
    if args.extract_layers:
        from extract_layers import main as extract_main

        extract_main()
    if args.smoke:
        smoke(args.out_dir, args.animate)
        return
    if args.input:
        for p in render_input(args.input, args.out_dir, args.stem, args.animate):
            print(p)
        return
    parser.print_help()


if __name__ == "__main__":
    main()


