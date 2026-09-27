using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    [CustomPropertyDrawer(typeof(ManualChainlingEntry))]
    public class ManualChainlingEntryDrawer : PropertyDrawer
    {
        private static string[] _modifierLabels;
        private static string[] _modifierIds;
        private static double _lastCatalogRefresh;

        private static readonly string[] IvFields =
        {
            "ivMaxHP", "ivMeleeAtk", "ivMeleeDef", "ivRangedAtk", "ivRangedDef", "ivSpeed"
        };

        private static readonly string[] PickFields =
        {
            "pickMaxHP", "pickMeleeAtk", "pickMeleeDef", "pickRangedAtk", "pickRangedDef", "pickSpeed"
        };

        private static void RefreshModifierCatalog()
        {
            if (EditorApplication.timeSinceStartup - _lastCatalogRefresh < 5.0) return;
            _lastCatalogRefresh = EditorApplication.timeSinceStartup;

            var guids = AssetDatabase.FindAssets("t:ModifierCatalogSO");
            ModifierCatalogSO catalog = null;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                catalog = AssetDatabase.LoadAssetAtPath<ModifierCatalogSO>(path);
                if (catalog != null) break;
            }

            if (catalog == null || catalog.modifiers == null || catalog.modifiers.Count == 0)
            {
                _modifierLabels = new[] { "(none)", "(no catalog found)" };
                _modifierIds = new[] { "", "" };
                return;
            }

            var labels = new List<string> { "(none)" };
            var ids = new List<string> { "" };
            foreach (var mod in catalog.modifiers)
            {
                if (mod == null) continue;
                string label = $"{mod.GetDisplayName()} ({mod.sectionId}) — {mod.category}/{mod.scope}";
                if (label.Length > 80) label = label.Substring(0, 77) + "...";
                labels.Add(label);
                ids.Add(mod.sectionId);
            }

            _modifierLabels = labels.ToArray();
            _modifierIds = ids.ToArray();
        }

        private static int FindModifierIndex(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId) || _modifierIds == null) return 0;
            for (int i = 0; i < _modifierIds.Length; i++)
                if (_modifierIds[i] == sectionId) return i;
            return 0;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
                return EditorGUIUtility.singleLineHeight;

            float lineH = EditorGUIUtility.singleLineHeight + 2;
            int rows = 0;
            rows += 7; // species, name, stars, rebirth, mastery, evo, preview
            rows += 2; // visual identity header + variant
            var moveIds = property.FindPropertyRelative("moveIds");
            int moveCount = moveIds != null ? moveIds.arraySize : 0;
            rows += 1; // moves label
            rows += Mathf.Max(moveCount, 0);
            rows += moveCount * 2;
            rows += 1; // add button
            rows += 1; // IV header
            rows += 6;
            rows += 1; // picks header + sum
            rows += 7;
            rows += 1; // wild/vitals header
            rows += 5; // mutated, mutationId, entangled, entangledType, currentHp
            return lineH * (rows + 2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            RefreshModifierCatalog();

            EditorGUI.BeginProperty(position, label, property);
            float lineH = EditorGUIUtility.singleLineHeight + 2;
            Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var speciesId = property.FindPropertyRelative("speciesId");
            string foldoutLabel = !string.IsNullOrEmpty(speciesId.stringValue)
                ? speciesId.stringValue
                : "(empty entry)";
            property.isExpanded = EditorGUI.Foldout(line, property.isExpanded, foldoutLabel, true);
            line.y += lineH;

            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;

            EditorGUI.PropertyField(line, speciesId);
            line.y += lineH;

            EditorGUI.PropertyField(line, property.FindPropertyRelative("displayName"));
            line.y += lineH;

            var stars = property.FindPropertyRelative("stars");
            var rebirth = property.FindPropertyRelative("rebirthCount");
            stars.intValue = EditorGUI.IntSlider(line, "Stars", stars.intValue, 0, CombatConstants.STAR_MAX);
            line.y += lineH;
            rebirth.intValue = EditorGUI.IntSlider(line, "Cycle (Rebirth)", rebirth.intValue, 0, CombatConstants.REBIRTH_MAX);
            line.y += lineH;

            EditorGUI.PropertyField(line, property.FindPropertyRelative("masteryXp"));
            line.y += lineH;

            EditorGUI.PropertyField(line, property.FindPropertyRelative("evolutionStage"));
            line.y += lineH;

            DrawVisualIdentity(ref line, lineH, property);

            DrawProgressionPreview(ref line, lineH, property);

            DrawMovesAndModifiers(ref line, lineH, property);
            DrawIvSliders(ref line, lineH, property);
            DrawCyclePicks(ref line, lineH, property, rebirth.intValue);

            EditorGUI.LabelField(line, "Wild / Vitals", EditorStyles.boldLabel);
            line.y += lineH;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("isMutated"));
            line.y += lineH;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("mutationId"));
            line.y += lineH;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("isEntangled"));
            line.y += lineH;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("entangledType"));
            line.y += lineH;
            EditorGUI.PropertyField(line, property.FindPropertyRelative("currentHp"));
            line.y += lineH;

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static CombatVisualDatabaseSO _visualDatabase;
        private static double _lastVisualDbRefresh;

        private static CombatVisualDatabaseSO ResolveVisualDatabase()
        {
            if (EditorApplication.timeSinceStartup - _lastVisualDbRefresh < 5.0 && _visualDatabase != null)
                return _visualDatabase;

            _lastVisualDbRefresh = EditorApplication.timeSinceStartup;
            string[] guids = AssetDatabase.FindAssets("t:CombatVisualDatabaseSO");
            _visualDatabase = null;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _visualDatabase = AssetDatabase.LoadAssetAtPath<CombatVisualDatabaseSO>(path);
                if (_visualDatabase != null)
                    break;
            }

            return _visualDatabase;
        }

        private static void DrawVisualIdentity(ref Rect line, float lineH, SerializedProperty property)
        {
            EditorGUI.LabelField(line, "Visual Identity", EditorStyles.boldLabel);
            line.y += lineH;

            SerializedProperty identity = property.FindPropertyRelative("visualIdentity");
            SerializedProperty variantProp = identity != null
                ? identity.FindPropertyRelative("visualVariantId")
                : null;
            if (variantProp == null)
                return;

            string speciesId = property.FindPropertyRelative("speciesId") != null
                ? property.FindPropertyRelative("speciesId").stringValue
                : string.Empty;
            CombatVisualDatabaseSO database = ResolveVisualDatabase();
            List<string> ids = database != null
                ? database.CollectBodyVariantIds(speciesId)
                : new List<string> { ChainlingVisualIdentity.BaseVariantId };

            string current = ChainlingVisualIdentity.NormalizeBodySkinVariantId(variantProp.stringValue);
            if (!ids.Contains(current))
                ids.Insert(0, current);

            var labels = new string[ids.Count];
            int selected = 0;
            ChainlingSpeciesVisualSetSO set = database != null ? database.GetVisualSet(speciesId) : null;
            for (int i = 0; i < ids.Count; i++)
            {
                labels[i] = FormatVariantLabel(set, ids[i]);
                if (string.Equals(ids[i], current, StringComparison.Ordinal))
                    selected = i;
            }

            int next = EditorGUI.Popup(line, "Body Variant", selected, labels);
            variantProp.stringValue = ids[Mathf.Clamp(next, 0, ids.Count - 1)];
            line.y += lineH;
        }

        private static string FormatVariantLabel(ChainlingSpeciesVisualSetSO set, string variantId)
        {
            if (set != null)
            {
                SpeciesVisualVariantEntry entry = set.FindVariant(variantId);
                if (entry != null)
                {
                    string name = !string.IsNullOrWhiteSpace(entry.displayName)
                        ? entry.displayName
                        : variantId;
                    return $"{variantId} — {name} ({entry.rarity})";
                }
            }

            if (string.Equals(variantId, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal))
                return "base — Default";

            return variantId;
        }

        private static void DrawProgressionPreview(ref Rect line, float lineH, SerializedProperty property)
        {
            int rebirth = property.FindPropertyRelative("rebirthCount").intValue;
            ProgressionConfigSO prog = BalanceConfigRuntime.Progression;
            float hpAutoPct = prog != null ? prog.cycleAutoHpPercent : CombatConstants.CYCLE_AUTO_HP_PERCENT;
            float hpPickPct = prog != null ? prog.cyclePickHpPercent : CombatConstants.CYCLE_PICK_HP_PERCENT;
            int autoStat = prog != null ? prog.cycleAutoStatFlat : CombatConstants.CYCLE_AUTO_STAT_FLAT;

            string preview =
                $"Cycle preview: auto combat +{autoStat * rebirth}/stat; " +
                $"MaxHP auto +{hpAutoPct * 100f:0.#}%×base×{rebirth}; " +
                $"picks add +{hpPickPct * 100f:0.#}%×base per HP point. Stars ≠ combat power.";
            EditorGUI.HelpBox(new Rect(line.x, line.y, line.width, lineH * 2.2f), preview, MessageType.None);
            line.y += lineH * 2.4f;
        }

        private static void DrawMovesAndModifiers(ref Rect line, float lineH, SerializedProperty property)
        {
            var moveIds = property.FindPropertyRelative("moveIds");
            var mod1Ids = property.FindPropertyRelative("modifier1SectionIds");
            var mod2Ids = property.FindPropertyRelative("modifier2SectionIds");

            EditorGUI.LabelField(line, "Moves & Modifiers", EditorStyles.boldLabel);
            line.y += lineH;

            int moveCount = moveIds.arraySize;
            for (int i = 0; i < moveCount; i++)
            {
                var moveProp = moveIds.GetArrayElementAtIndex(i);
                Rect moveRect = new Rect(line.x, line.y, line.width - 24, line.height);
                Rect delRect = new Rect(line.x + line.width - 22, line.y, 22, line.height);

                EditorGUI.PropertyField(moveRect, moveProp, new GUIContent($"Move {i}"));
                if (GUI.Button(delRect, "X"))
                {
                    moveIds.DeleteArrayElementAtIndex(i);
                    if (i < mod1Ids.arraySize) mod1Ids.DeleteArrayElementAtIndex(i);
                    if (i < mod2Ids.arraySize) mod2Ids.DeleteArrayElementAtIndex(i);
                    break;
                }
                line.y += lineH;

                while (mod1Ids.arraySize <= i) mod1Ids.InsertArrayElementAtIndex(mod1Ids.arraySize);
                string m1Val = mod1Ids.GetArrayElementAtIndex(i).stringValue;
                int m1Idx = FindModifierIndex(m1Val);
                int newM1 = EditorGUI.Popup(line, "  Modifier 1", m1Idx, _modifierLabels);
                if (newM1 != m1Idx)
                    mod1Ids.GetArrayElementAtIndex(i).stringValue = _modifierIds[newM1];
                line.y += lineH;

                while (mod2Ids.arraySize <= i) mod2Ids.InsertArrayElementAtIndex(mod2Ids.arraySize);
                string m2Val = mod2Ids.GetArrayElementAtIndex(i).stringValue;
                int m2Idx = FindModifierIndex(m2Val);
                int newM2 = EditorGUI.Popup(line, "  Modifier 2", m2Idx, _modifierLabels);
                if (newM2 != m2Idx)
                    mod2Ids.GetArrayElementAtIndex(i).stringValue = _modifierIds[newM2];
                line.y += lineH;
            }

            Rect addBtn = new Rect(line.x + EditorGUI.indentLevel * 15, line.y, 120, line.height);
            if (GUI.Button(addBtn, "+ Add Move Slot"))
            {
                moveIds.InsertArrayElementAtIndex(moveCount);
                moveIds.GetArrayElementAtIndex(moveCount).stringValue = "";
            }
            line.y += lineH;
        }

        private static void DrawIvSliders(ref Rect line, float lineH, SerializedProperty property)
        {
            EditorGUI.LabelField(line,
                $"IVs ({CombatConstants.IV_MIN} to {CombatConstants.IV_MAX}) — zeros are honored",
                EditorStyles.boldLabel);
            line.y += lineH;

            foreach (string ivName in IvFields)
            {
                var iv = property.FindPropertyRelative(ivName);
                iv.intValue = EditorGUI.IntSlider(line, iv.displayName, iv.intValue, CombatConstants.IV_MIN, CombatConstants.IV_MAX);
                line.y += lineH;
            }
        }

        private static void DrawCyclePicks(ref Rect line, float lineH, SerializedProperty property, int rebirthCount)
        {
            EditorGUI.LabelField(line, "Cycle Picks (allocation counts)", EditorStyles.boldLabel);
            line.y += lineH;

            int sum = 0;
            foreach (string pickName in PickFields)
            {
                var pick = property.FindPropertyRelative(pickName);
                pick.intValue = EditorGUI.IntSlider(line, pick.displayName, Mathf.Max(0, pick.intValue), 0, CombatConstants.REBIRTH_MAX);
                sum += pick.intValue;
                line.y += lineH;
            }

            MessageType msgType = sum > rebirthCount ? MessageType.Warning
                : (sum < rebirthCount && sum > 0 ? MessageType.Info : MessageType.None);
            string sumLabel = $"Pick sum: {sum} / Cycle {rebirthCount}";
            if (sum > rebirthCount)
                sumLabel += " (excess truncated at build)";
            else if (sum < rebirthCount && sum > 0)
                sumLabel += " (shortfall unused; auto still applies)";
            EditorGUI.HelpBox(line, sumLabel, msgType == MessageType.None ? MessageType.None : msgType);
            line.y += lineH;
        }
    }
}
