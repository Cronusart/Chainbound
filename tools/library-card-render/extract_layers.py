"""Slice library-card templates into compositor layers.

Run from repo root:
  python tools/library-card-render/extract_layers.py
"""
from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

REPO = Path(__file__).resolve().parents[2]
FRONT = REPO / "Docs/Art/MovesLibraryCard/templates/card_front_base.png"
BACK = REPO / "Docs/Art/MovesLibraryCard/templates/card_back_base.png"
LAYERS = REPO / "Docs/Art/MovesLibraryCard/templates/layers"

SAGE = (178, 186, 144, 255)
LINE = (146, 169, 132, 255)
INK = (92, 36, 28, 90)

TYPE_WELL = (70, 70, 250, 250)  # x0,y0,x1,y1
BARCODE = (40, 548, 320, 612)
MOD1 = (280, 440, 1220, 492)
MOD2 = (280, 498, 1220, 548)


def _match(px, color, tol=8):
    return all(abs(int(px[i]) - color[i]) <= tol for i in range(3)) and px[3] > 8


def extract_body_and_frame(src: Image.Image) -> tuple[Image.Image, Image.Image]:
    w, h = src.size
    body = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    frame = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sp = src.load()
    bp = body.load()
    fp = frame.load()
    for y in range(h):
        for x in range(w):
            p = sp[x, y]
            if p[3] < 8:
                continue
            if _match(p, SAGE) or _match(p, LINE):
                bp[x, y] = p
            else:
                fp[x, y] = p
    return body, frame


def crop_keep(src: Image.Image, box, keep_line_only=False) -> Image.Image:
    x0, y0, x1, y1 = box
    w, h = src.size
    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sp = src.load()
    op = out.load()
    for y in range(y0, y1):
        for x in range(x0, x1):
            p = sp[x, y]
            if p[3] < 8:
                continue
            if keep_line_only and not _match(p, LINE):
                continue
            op[x, y] = p
    return out


def dashed_slot(size, box, filled: bool) -> Image.Image:
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    x0, y0, x1, y1 = box
    color = (92, 36, 28, 220) if filled else (146, 169, 132, 180)
    if filled:
        draw.rectangle([x0, y0, x1, y1], outline=color, width=3)
        return img
    dash, gap = 18, 12
    x = x0
    y = y1 - 4
    while x < x1:
        draw.line([(x, y), (min(x + dash, x1), y)], fill=color, width=3)
        x += dash + gap
    return img


def ink_noise(size, seed=7) -> Image.Image:
    import random

    rng = random.Random(seed)
    img = Image.new("RGBA", size, (0, 0, 0, 0))
    px = img.load()
    w, h = size
    for y in range(h):
        for x in range(w):
            n = rng.randint(0, 255)
            if n > 210:
                a = int((n - 210) * 1.4)
                px[x, y] = (INK[0], INK[1], INK[2], a)
    return img


def main() -> None:
    LAYERS.mkdir(parents=True, exist_ok=True)
    front = Image.open(FRONT).convert("RGBA")
    body, frame = extract_body_and_frame(front)
    body.save(LAYERS / "body.png")
    frame.save(LAYERS / "frame.png")
    crop_keep(front, TYPE_WELL, keep_line_only=True).save(LAYERS / "type_well.png")
    crop_keep(front, BARCODE, keep_line_only=False).save(LAYERS / "barcode.png")
    dashed_slot(front.size, MOD1, False).save(LAYERS / "mod_slot_empty.png")
    dashed_slot(front.size, MOD1, True).save(LAYERS / "mod_slot_filled.png")
    ink_noise(front.size).save(LAYERS / "ink_noise.png")
    print(f"Wrote layers to {LAYERS}")


if __name__ == "__main__":
    main()
