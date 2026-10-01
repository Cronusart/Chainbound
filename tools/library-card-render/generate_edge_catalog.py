"""Generate the small server-side catalog used by Stamp metadata publication.

The Edge renderer deliberately consumes generated, reviewable data rather than
trusting move/modifier labels submitted by Unity.
"""
from __future__ import annotations

import base64
import csv
import io
import json
import re
import sys
from pathlib import Path

from PIL import Image, ImageSequence


REPO = Path(__file__).resolve().parents[2]
SPINE = REPO / "supabase/functions/_shared/ownership_spine.ts"
MOVES = REPO / "Assets/Data/Combat/Moves"
MODIFIERS = REPO / "Docs/Specs/Combat/Data/Modifiers.csv"
BASE_CARD = REPO / "Assets/Sprites/UI/LibraryCards/librarycard_front.png"
LEARNED_BASE_CARD = REPO / "Assets/Sprites/UI/LibraryCards/librarycard_learned_front.png"
SCANLINE_BG = REPO / "Assets/Sprites/UI/LibraryCards/card_scanline_bg.gif"
OUTPUT = REPO / "supabase/functions/_shared/ownership_card_catalog.generated.ts"
MIRROR = REPO / "Supabase/supabase/functions/_shared/ownership_card_catalog.generated.ts"
sys.path.insert(0, str(Path(__file__).resolve().parent))
import render as card_renderer  # noqa: E402


TYPE_NAMES = [
    "Typeless",
    "Node",
    "Gas",
    "Block",
    "Encrypted",
    "Burned",
    "Frozen",
    "Forked",
    "Mined",
    "Staked",
    "Compressed",
    "Bot",
    "Glitched",
    "Shard",
    "Meme",
]
TYPE_ICON_FILES = [
    "typeless.png",
    "node_type.png",
    "gas_type.png",
    "block_type.png",
    "encrypted_type.png",
    "burned_type.png",
    "frozen_type.png",
    "forked_type.png",
    "mined_type.png",
    "staked_type.png",
    "compressed_type.png",
    "bot_type.png",
    "glitched_type.png",
    "shard_type.png",
    "meme_type.png",
]
MOVE_CATEGORY_NAMES = [
    "Damaging Melee",
    "Damaging Ranged",
    "Status Utility",
    "Support",
    "Passive",
]


def render_category(category_id: int) -> str:
    if category_id in (0, 1):
        return "attack"
    if category_id == 4:
        return "passive"
    return "utility"


def effect_line(category_id: int) -> str:
    if category_id == 3:
        return "Support"
    if category_id == 4:
        return "Passive"
    return "Utility"


def png_base64(image: Image.Image) -> str:
    output = io.BytesIO()
    image.save(output, format="PNG", optimize=True)
    return base64.b64encode(output.getvalue()).decode("ascii")


def delta_overlay(base: Image.Image, composed: Image.Image) -> Image.Image:
    """Keep only pixels changed by a deterministic card render.

    Library-card faces are opaque, so changed pixels can be stored as exact
    replacement pixels on a transparent layer and composited server-side.
    This avoids embedding the learned paper face once per move.
    """
    base_rgba = base.convert("RGBA")
    composed_rgba = composed.convert("RGBA")
    if base_rgba.size != composed_rgba.size:
        raise RuntimeError("Card face and rendered card dimensions differ")
    overlay = Image.new("RGBA", base_rgba.size, (0, 0, 0, 0))
    base_pixels = base_rgba.load()
    composed_pixels = composed_rgba.load()
    overlay_pixels = overlay.load()
    width, height = base_rgba.size
    for y in range(height):
        for x in range(width):
            if base_pixels[x, y] != composed_pixels[x, y]:
                if composed_pixels[x, y][3] != 255:
                    raise RuntimeError("Learned card delta requires an opaque composed face")
                overlay_pixels[x, y] = composed_pixels[x, y]
    return overlay


def cropped_layer(image: Image.Image) -> dict[str, object]:
    """Store a transparent overlay at its smallest lossless bounding box."""
    rgba = image.convert("RGBA")
    bounds = rgba.getchannel("A").getbbox()
    if bounds is None:
        return {"x": 0, "y": 0, "png": png_base64(Image.new("RGBA", (1, 1), (0, 0, 0, 0)))}
    left, top, right, bottom = bounds
    return {
        "x": left,
        "y": top,
        "png": png_base64(rgba.crop((left, top, right, bottom))),
    }


def scalar(text: str, field: str, default: str = "") -> str:
    match = re.search(rf"^  {re.escape(field)}:\s*(.*)$", text, re.MULTILINE)
    return match.group(1).strip() if match else default


spine = SPINE.read_text(encoding="utf-8")
pool_match = re.search(r"const KNOWN_POOL = \[(.*?)\] as const;", spine, re.DOTALL)
if not pool_match:
    raise RuntimeError("KNOWN_POOL was not found")
move_ids = re.findall(r'moveId:\s*"([^"]+)"', pool_match.group(1))

moves: dict[str, dict[str, object]] = {}
for move_id in move_ids:
    path = MOVES / f"Move_{move_id}.asset"
    text = path.read_text(encoding="utf-8-sig")
    category_id = int(scalar(text, "category", "0"))
    type_id = int(scalar(text, "baseType", "0"))
    if type_id < 0 or type_id >= len(TYPE_NAMES):
        raise RuntimeError(f"Unsupported ChainlingType {type_id} on {move_id}")
    if category_id < 0 or category_id >= len(MOVE_CATEGORY_NAMES):
        raise RuntimeError(f"Unsupported MoveCategory {category_id} on {move_id}")
    moves[move_id] = {
        "displayName": scalar(text, "displayName", move_id.replace("_", " ").title()),
        "power": int(scalar(text, "power", "0")),
        "accuracy": int(scalar(text, "accuracy", "0")),
        "category": category_id,
        "categoryName": MOVE_CATEGORY_NAMES[category_id],
        "typeId": type_id,
        "typeName": TYPE_NAMES[type_id],
        "typeIconPath": f"Assets/Sprites/UI/{TYPE_ICON_FILES[type_id]}",
    }

modifiers: dict[str, dict[str, str]] = {}
with MODIFIERS.open("r", encoding="utf-8-sig", newline="") as handle:
    rows = (line for line in handle if not line.lstrip().startswith("#") and line.strip())
    for row in csv.DictReader(rows):
        section_id = (row.get("SectionId") or "").strip()
        if section_id:
            modifiers[section_id] = {
                "displayName": (row.get("DisplayName") or section_id).strip(),
                "effect": (row.get("EffectDescription") or "").strip(),
            }

def move_front(move_id: str, move: dict[str, object], face: str) -> str:
    return png_base64(card_renderer.render_front({
        "moveId": move_id,
        "moveName": move["displayName"],
        "typeId": move["typeId"],
        "category": render_category(int(move["category"])),
        "power": move["power"],
        "accuracy": move["accuracy"],
        "effectLine": effect_line(int(move["category"])),
        "mods": [],
        "moveRarity": "Common",
    }, face))


move_cards = {move_id: move_front(move_id, move, "rolled") for move_id, move in moves.items()}
learned_card_front = Image.open(LEARNED_BASE_CARD).convert("RGBA")
learned_move_card_deltas = {
    move_id: cropped_layer(delta_overlay(
        learned_card_front,
        card_renderer.render_front({
            "moveId": move_id,
            "moveName": move["displayName"],
            "typeId": move["typeId"],
            "category": render_category(int(move["category"])),
            "power": move["power"],
            "accuracy": move["accuracy"],
            "effectLine": effect_line(int(move["category"])),
            "mods": [],
            "moveRarity": "Common",
        }, "learned"),
    ))
    for move_id, move in moves.items()
}
type_sticker_layers = {
    str(type_id): cropped_layer(card_renderer.render_type_sticker_overlay(
        f"Assets/Sprites/UI/{TYPE_ICON_FILES[type_id]}"
    ))
    for type_id in sorted({int(move["typeId"]) for move in moves.values()})
}
def modifier_layer(modifier: dict[str, str], face: str) -> dict[str, object]:
    return cropped_layer(card_renderer.render_modifier_overlay({
        "displayName": modifier["displayName"],
        "effectDescription": modifier["effect"],
    }, face=face))


modifier_layers = {modifier_id: modifier_layer(modifier, "rolled") for modifier_id, modifier in modifiers.items()}
authority: dict[str, dict[str, int]] = {}
moves_dir = REPO / "Assets/Data/Combat/Moves"
for path in sorted(moves_dir.glob("Move_*.asset")):
    text = path.read_text(encoding="utf-8-sig")
    move_id = scalar(text, "moveId")
    if not move_id:
        continue
    authority[move_id] = {
        "element": int(scalar(text, "baseType", "0")),
        "basePower": int(scalar(text, "power", "0")),
    }
rarity_names = ["Common", "Uncommon", "Rare", "Epic", "Legendary", "Mythic"]
rarity_layers = {
    str(index): cropped_layer(card_renderer.render_rarity_overlay(name))
    for index, name in enumerate(rarity_names)
}
scanline_frames: list[str] = []
with Image.open(SCANLINE_BG) as gif:
    for frame in ImageSequence.Iterator(gif):
        scanline_frames.append(png_base64(frame.convert("RGBA")))

payload = (
    "// Generated by tools/library-card-render/generate_edge_catalog.py. Do not edit by hand.\n"
    f"export const MOVE_CARD_PNG_BASE64 = {json.dumps(move_cards, indent=2)} as const;\n\n"
    f"export const LEARNED_CARD_FRONT_PNG_BASE64 = {json.dumps(png_base64(learned_card_front))} as const;\n\n"
    f"export const LEARNED_MOVE_CARD_DELTA_PNG_BASE64 = {json.dumps(learned_move_card_deltas, indent=2)} as const;\n\n"
    f"export const TYPE_STICKER_PNG_BASE64 = {json.dumps(type_sticker_layers, indent=2)} as const;\n\n"
    f"export const MODIFIER_INK_PNG_BASE64 = {json.dumps(modifier_layers, indent=2)} as const;\n\n"
    f"export const RARITY_DOT_PNG_BASE64 = {json.dumps(rarity_layers, indent=2)} as const;\n\n"
    f"export const SCANLINE_FRAME_PNG_BASE64 = {json.dumps(scanline_frames, indent=2)} as const;\n\n"
    f"export const MOVE_CARD_CATALOG = {json.dumps(moves, indent=2)} as const;\n\n"
    f"export const MODIFIER_CARD_CATALOG = {json.dumps(modifiers, indent=2)} as const;\n\n"
    f"export const MOVE_AUTHORITY = {json.dumps(authority, indent=2)} as const;\n"
)
OUTPUT.write_text(payload, encoding="utf-8", newline="\n")
if MIRROR.parent.is_dir():
    MIRROR.write_text(payload, encoding="utf-8", newline="\n")
print(f"Wrote {OUTPUT.relative_to(REPO)} ({len(moves)} moves, {len(modifiers)} modifiers, {len(authority)} authorities)")
