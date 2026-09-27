from pathlib import Path

root = Path(__file__).resolve().parents[1]
prefab = root / "Assets/Prefabs/Combat/CombatBattleCanvas.prefab"
snippet = root / "tools/_intro_burst_snippet.yaml.txt"
text = prefab.read_text(encoding="utf-8")
marker = "--- !u!1 &6856818722607821637"
if marker not in text:
    raise SystemExit("marker not found")
insert = snippet.read_text(encoding="utf-8") + "\n"
text = text.replace(marker, insert + marker, 1)

children_old = """  m_Children:
  - {fileID: 7290463946390843222}
  - {fileID: 4411800399838764609}
  - {fileID: 917773010011002}
  - {fileID: 3829462559898917884}
  - {fileID: 1009235903446032067}
  m_Father: {fileID: 4718094956873596959}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0.5}"""

children_new = """  m_Children:
  - {fileID: 7290463946390843222}
  - {fileID: 4411800399838764609}
  - {fileID: 917773010011002}
  - {fileID: 3829462559898917884}
  - {fileID: 1009235903446032067}
  - {fileID: 826771010020002}
  - {fileID: 826771010020012}
  m_Father: {fileID: 4718094956873596959}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
  m_AnchorMin: {x: 0, y: 0.5}"""

if children_old not in text:
    raise SystemExit("IntroCard children block not found")
text = text.replace(children_old, children_new, 1)

text = text.replace(
    "  topEdgeParticleBurst: {fileID: 0}\n  bottomEdgeParticleBurst: {fileID: 0}",
    "  topEdgeParticleBurst: {fileID: 826771010020003}\n  bottomEdgeParticleBurst: {fileID: 826771010020013}",
    1,
)

prefab.write_text(text, encoding="utf-8")
print("Merged particle sweep emitters into CombatBattleCanvas.prefab")
