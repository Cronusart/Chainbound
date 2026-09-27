using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(ChainlingProgressionRewardTablesSO))]
    public class ChainlingProgressionRewardTablesSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            ChainlingProgressionRewardTablesSO tables = (ChainlingProgressionRewardTablesSO)target;
            bool validStandard = tables.starKnownMoveChanceStandard != null &&
                tables.starKnownMoveChanceStandard.Length == CombatConstants.STAR_MAX;
            bool validEntangled = tables.starKnownMoveChanceEntangled != null &&
                tables.starKnownMoveChanceEntangled.Length == CombatConstants.STAR_MAX;

            MessageType type = validStandard && validEntangled ? MessageType.Info : MessageType.Warning;
            EditorGUILayout.HelpBox(
                $"Star chance arrays should each contain {CombatConstants.STAR_MAX} entries.",
                type);

            if (GUILayout.Button("Reset To ECONOMY_01 Defaults"))
            {
                Undo.RecordObject(tables, "Reset Chainling Progression Reward Tables");
                tables.ResetToEconomyDefaults();
                EditorUtility.SetDirty(tables);
            }
        }
    }
}
