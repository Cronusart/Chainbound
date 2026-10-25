using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomEditor(typeof(CombatVFXFallbacks))]
    public sealed class CombatVFXFallbacksEditor : UnityEditor.Editor
    {
        private readonly Dictionary<string, int> _usage = new Dictionary<string, int>();
        private SerializedProperty _typed;

        private void OnEnable()
        {
            _typed = serializedObject.FindProperty("typed");
            RebuildUsage();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Damage Fallbacks by Base Type", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Marker format: filled/empty • number of usable move definitions that prefer this slot.", MessageType.Info);

            if (_typed != null)
            {
                for (int i = 0; i < _typed.arraySize; i++)
                {
                    SerializedProperty row = _typed.GetArrayElementAtIndex(i);
                    SerializedProperty type = row.FindPropertyRelative("type");
                    ChainlingType typeValue = (ChainlingType)type.enumValueIndex;
                    EditorGUILayout.LabelField(typeValue.ToString(), EditorStyles.miniBoldLabel);
                    DrawEntry(row.FindPropertyRelative("melee"), "Melee", $"Typed:{typeValue}/Melee");
                    DrawEntry(row.FindPropertyRelative("ranged"), "Ranged", $"Typed:{typeValue}/Ranged");
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Utility Roles", EditorStyles.boldLabel);
            DrawNamed("heal", "Heal", "Role:Heal");
            DrawNamed("buff", "Buff", "Role:Buff");
            DrawNamed("debuff", "Debuff", "Role:Debuff");
            DrawNamed("wall", "Wall (optional)", "Role:Wall");
            DrawNamed("barrier", "Barrier (optional)", "Role:Barrier");
            DrawNamed("armor", "Armor (optional)", "Role:Armor");
            DrawNamed("apGain", "AP Gain (optional)", "Role:ApGain");
            DrawNamed("apDrain", "AP Drain (optional)", "Role:ApDrain");
            DrawNamed("swap", "Swap (optional)", "Role:Swap");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Defaults", EditorStyles.boldLabel);
            DrawNamed("globalMelee", "Global Melee", "Global:Melee");
            DrawNamed("globalRanged", "Global Ranged", "Global:Ranged");
            DrawNamed("globalUtility", "Global Utility", "Global:Utility");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wall State Profiles", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Resolution: per-move wall art -> exact kind/tier row -> kind default -> global wall visual profile. Wall audio remains in CombatWallSfxCatalogSO.",
                MessageType.Info);
            SerializedProperty wallStates = serializedObject.FindProperty("wallStates");
            if (wallStates != null)
                EditorGUILayout.PropertyField(wallStates, true);

            EditorGUILayout.Space();
            if (GUILayout.Button("Refresh Move Usage"))
                RebuildUsage();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawNamed(string propertyName, string label, string key)
        {
            DrawEntry(serializedObject.FindProperty(propertyName), label, key);
        }

        private void DrawEntry(SerializedProperty property, string label, string key)
        {
            bool filled = CombatVfxResolver.IsUsable(ResolveEntry(key));
            int count = _usage.TryGetValue(key, out int value) ? value : 0;
            string marker = filled ? "●" : "○";
            EditorGUILayout.PropertyField(property, new GUIContent($"{label}  {marker} used by {count}"), true);
        }

        private CombatVFXLibrary.VFXEntry ResolveEntry(string key)
        {
            var fallbacks = (CombatVFXFallbacks)target;
            if (key.StartsWith("Typed:", StringComparison.Ordinal))
            {
                string[] parts = key.Substring("Typed:".Length).Split('/');
                if (parts.Length == 2 && Enum.TryParse(parts[0], out ChainlingType type))
                {
                    MoveCategory category = parts[1] == "Ranged"
                        ? MoveCategory.DamagingRanged
                        : MoveCategory.DamagingMelee;
                    return fallbacks.GetTyped(type, category);
                }
            }
            if (key.StartsWith("Role:", StringComparison.Ordinal) &&
                Enum.TryParse(key.Substring("Role:".Length), out MoveVfxRole role))
                return fallbacks.GetRole(role);
            if (key == "Global:Melee") return fallbacks.GetGlobal(MoveCategory.DamagingMelee);
            if (key == "Global:Ranged") return fallbacks.GetGlobal(MoveCategory.DamagingRanged);
            if (key == "Global:Utility") return fallbacks.GetGlobal(MoveCategory.Support);
            return null;
        }

        private void RebuildUsage()
        {
            _usage.Clear();
            string[] guids = AssetDatabase.FindAssets("t:MoveDefinitionSO");
            for (int i = 0; i < guids.Length; i++)
            {
                MoveDefinitionSO move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (move == null || move.category == MoveCategory.Passive)
                    continue;

                MoveVfxRole role = MoveVfxRoleClassifier.Classify(move);
                string key;
                if (role == MoveVfxRole.Damage)
                {
                    string range = move.category == MoveCategory.DamagingRanged ? "Ranged" : "Melee";
                    key = $"Typed:{move.baseType}/{range}";
                }
                else
                {
                    key = $"Role:{role}";
                }
                _usage[key] = _usage.TryGetValue(key, out int count) ? count + 1 : 1;
            }
        }
    }
}
