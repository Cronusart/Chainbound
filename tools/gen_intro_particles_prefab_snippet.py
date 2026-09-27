"""Extract WindowBeam DustMotes particle + renderer; retarget to UI IntroCard sweep emitters."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BEAM = ROOT / "Assets/Prefabs/World/Lighting/WindowBeam.prefab"
lines = BEAM.read_text(encoding="utf-8").splitlines()
ps_start = 209 - 1
ps_end = 4931
rend_start = 4932 - 1
rend_end = 5008

ps_blob = "\n".join(lines[ps_start:ps_end])
rend_blob = "\n".join(lines[rend_start:rend_end])

intro_father = "2780027832015543393"


def patch(blob: str, go: str, ps: str, rend: str) -> str:
    out = blob
    out = out.replace("7560145809384247304", ps)
    out = out.replace("385507313645737227", rend)
    out = out.replace("4327871191189773686", go)
    out = out.replace("looping: 1", "looping: 0", 1)
    out = out.replace("playOnAwake: 1", "playOnAwake: 0", 1)
    out = out.replace("lengthInSec: 10", "lengthInSec: 1", 1)
    return out


TOP_GO, TOP_RECT, TOP_PS, TOP_REND = "826771010020001", "826771010020002", "826771010020003", "826771010020004"
BOT_GO, BOT_RECT, BOT_PS, BOT_REND = "826771010020011", "826771010020012", "826771010020013", "826771010020014"


def header_top() -> str:
    return f"""--- !u!1 &{TOP_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {TOP_RECT}}}
  - component: {{fileID: {TOP_PS}}}
  - component: {{fileID: {TOP_REND}}}
  m_Layer: 0
  m_Name: TopEdgeSweepBurst
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{TOP_RECT}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {TOP_GO}}}
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {intro_father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
  m_AnchorMin: {{x: 0, y: 1}}
  m_AnchorMax: {{x: 0, y: 1}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 0, y: 1}}
"""


def header_bottom() -> str:
    return f"""--- !u!1 &{BOT_GO}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
  - component: {{fileID: {BOT_RECT}}}
  - component: {{fileID: {BOT_PS}}}
  - component: {{fileID: {BOT_REND}}}
  m_Layer: 0
  m_Name: BottomEdgeSweepBurst
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!224 &{BOT_RECT}
RectTransform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {BOT_GO}}}
  m_LocalRotation: {{x: 0, y: 0, z: 1, w: 0}}
  m_LocalPosition: {{x: 0, y: 0, z: 0}}
  m_LocalScale: {{x: 1, y: 1, z: 1}}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {intro_father}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 180}}
  m_AnchorMin: {{x: 1, y: 0}}
  m_AnchorMax: {{x: 1, y: 0}}
  m_AnchoredPosition: {{x: 0, y: 0}}
  m_SizeDelta: {{x: 0, y: 0}}
  m_Pivot: {{x: 1, y: 0}}
"""


top = (
    header_top()
    + patch(ps_blob, TOP_GO, TOP_PS, TOP_REND)
    + "\n"
    + patch(rend_blob, TOP_GO, TOP_PS, TOP_REND)
)
bottom = (
    header_bottom()
    + patch(ps_blob, BOT_GO, BOT_PS, BOT_REND)
    + "\n"
    + patch(rend_blob, BOT_GO, BOT_PS, BOT_REND)
)

out_path = ROOT / "tools/_intro_burst_snippet.yaml.txt"
out_path.write_text(top + "\n" + bottom, encoding="utf-8")
print(f"Wrote {out_path} ({out_path.stat().st_size} bytes)")
