#!/usr/bin/env python3
"""Split CombatBattleCanvas prefab into overlay HUD + camera intro canvases."""

from __future__ import annotations

import re
import sys
from pathlib import Path

PREFAB = Path(__file__).resolve().parents[1] / "Assets/Prefabs/Combat/CombatBattleCanvas.prefab"

NEW_BLOCKS = """--- !u!1 &300000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 300001}
  - component: {fileID: 300010}
  - component: {fileID: 300011}
  - component: {fileID: 200004}
  - component: {fileID: 200005}
  - component: {fileID: 200006}
  - component: {fileID: 200007}
  m_Layer: 5
  m_Name: CombatHudCanvas
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &300001
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 300000}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 210001}
  m_Father: {fileID: 200001}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!223 &300010
Canvas:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 300000}
  m_Enabled: 1
  serializedVersion: 3
  m_RenderMode: 0
  m_Camera: {fileID: 0}
  m_PlaneDistance: 100
  m_PixelPerfect: 0
  m_ReceivesEvents: 1
  m_OverrideSorting: 0
  m_OverridePixelPerfect: 0
  m_SortingBucketNormalizedSize: 0
  m_VertexColorAlwaysGammaSpace: 0
  m_AdditionalShaderChannelsFlag: 25
  m_UpdateRectTransformForStandalone: 0
  m_SortingLayerID: 0
  m_SortingOrder: 400
  m_TargetDisplay: 0
--- !u!114 &300011
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 300000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 0cd44c1031e13a943bb63640046fad76, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  m_UiScaleMode: 1
  m_ReferencePixelsPerUnit: 100
  m_ScaleFactor: 1
  m_ReferenceResolution: {x: 1920, y: 2060}
  m_ScreenMatchMode: 0
  m_MatchWidthOrHeight: 0.5
  m_PhysicalUnit: 3
  m_FallbackScreenDPI: 96
  m_DefaultSpriteDPI: 96
  m_DynamicPixelsPerUnit: 1
  m_PresetInfoIsWorld: 0
--- !u!1 &400000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 400001}
  - component: {fileID: 200002}
  - component: {fileID: 200003}
  m_Layer: 5
  m_Name: CombatIntroCanvas
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &400001
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 400000}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 410001}
  m_Father: {fileID: 200001}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!1 &410000
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 410001}
  - component: {fileID: 410002}
  m_Layer: 5
  m_Name: SafeAreaRoot
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &410001
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 410000}
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children:
  - {fileID: 4718094956873596959}
  m_Father: {fileID: 400001}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0}
  m_AnchorMax: {x: 1, y: 1}
  m_AnchoredPosition: {x: 0, y: 0}
  m_SizeDelta: {x: 0, y: 0}
  m_Pivot: {x: 0.5, y: 0.5}
--- !u!114 &410002
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 410000}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: 7e1a4d6d4a2044d793c32e77d6cd9f01, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
  applyInUpdate: 1
"""


def replace_in_block(text: str, block_id: str, replacements: list[tuple[str, str]]) -> str:
    pattern = rf"(--- !u![^\n]+ &{re.escape(block_id)}\n(?:.*?\n)(?=--- !u!|\Z))"
    match = re.search(pattern, text, flags=re.DOTALL)
    if not match:
        raise RuntimeError(f"Block {block_id} not found")
    block = match.group(1)
    for old, new in replacements:
        if old not in block:
            raise RuntimeError(f"Expected snippet missing in block {block_id}: {old!r}")
        block = block.replace(old, new, 1)
    return text[: match.start(1)] + block + text[match.end(1) :]


def main() -> int:
    if not PREFAB.exists():
        print(f"Missing prefab: {PREFAB}", file=sys.stderr)
        return 1

    text = PREFAB.read_text(encoding="utf-8")

    if "CombatHudCanvas" in text and "CombatIntroCanvas" in text:
        print("Prefab already split.")
        return 0

    text = replace_in_block(
        text,
        "200000",
        [
            (
                "  m_Component:\n  - component: {fileID: 200001}\n  - component: {fileID: 200002}\n  - component: {fileID: 200003}\n  - component: {fileID: 200004}\n  - component: {fileID: 200005}\n  - component: {fileID: 200006}\n  - component: {fileID: 200007}",
                "  m_Component:\n  - component: {fileID: 200001}",
            )
        ],
    )

    text = replace_in_block(
        text,
        "200001",
        [
            (
                "  m_Children:\n  - {fileID: 210001}",
                "  m_Children:\n  - {fileID: 300001}\n  - {fileID: 400001}",
            ),
            (
                "  m_LocalScale: {x: 0, y: 0, z: 0}",
                "  m_LocalScale: {x: 1, y: 1, z: 1}",
            ),
            (
                "  m_AnchorMin: {x: 0, y: 0}\n  m_AnchorMax: {x: 0, y: 0}",
                "  m_AnchorMin: {x: 0, y: 0}\n  m_AnchorMax: {x: 1, y: 1}",
            ),
            (
                "  m_Pivot: {x: 0, y: 0}",
                "  m_Pivot: {x: 0.5, y: 0.5}",
            ),
        ],
    )

    text = replace_in_block(
        text,
        "200002",
        [
            ("  m_GameObject: {fileID: 200000}", "  m_GameObject: {fileID: 400000}"),
            ("  m_SortingOrder: 400", "  m_SortingOrder: 500"),
        ],
    )

    text = replace_in_block(
        text,
        "200003",
        [("  m_GameObject: {fileID: 200000}", "  m_GameObject: {fileID: 400000}")],
    )

    for block_id in ("200004", "200005", "200006", "200007"):
        text = replace_in_block(
            text,
            block_id,
            [("  m_GameObject: {fileID: 200000}", "  m_GameObject: {fileID: 300000}")],
        )

    text = replace_in_block(
        text,
        "210001",
        [
            (
                "  m_Children:\n  - {fileID: 220001}\n  - {fileID: 4718094956873596959}",
                "  m_Children:\n  - {fileID: 220001}",
            ),
            ("  m_Father: {fileID: 200001}", "  m_Father: {fileID: 300001}"),
        ],
    )

    text = replace_in_block(
        text,
        "4718094956873596959",
        [("  m_Father: {fileID: 210001}", "  m_Father: {fileID: 410001}")],
    )

    marker = "--- !u!1 &210000"
    if marker not in text:
        raise RuntimeError("Insert marker not found")
    text = text.replace(marker, NEW_BLOCKS + marker, 1)

    PREFAB.write_text(text, encoding="utf-8")
    print(f"Split complete: {PREFAB}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
