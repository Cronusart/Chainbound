using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Inline lever board for modifier effect ranges, rarity gates, and drop weights.
    /// Menu: Tools/Chainbound/Combat/Moves Modifiers Balance Board
    /// </summary>
    public class MovesModifiersBalanceWindow : EditorWindow
    {
        private static readonly RarityTier[] RarityOrder =
        {
            RarityTier.Common,
            RarityTier.Uncommon,
            RarityTier.Rare,
            RarityTier.Epic,
            RarityTier.Legendary,
            RarityTier.Mythic,
        };

        private Vector2 _scroll;
        private string _status = string.Empty;
        private readonly Dictionary<RarityTier, bool> _foldouts = new Dictionary<RarityTier, bool>();
        private List<ModifierDefinitionSO> _modifiers = new List<ModifierDefinitionSO>();

        [MenuItem("Tools/Chainbound/Combat/Moves Modifiers Balance Board")]
        public static void ShowWindow()
        {
            var window = GetWindow<MovesModifiersBalanceWindow>("Moves/Modifiers Balance");
            window.minSize = new Vector2(720, 420);
            window.RefreshList();
        }

        private void OnEnable()
        {
            RefreshList();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Moves / Modifiers Balance Board", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Edit modifier effect ranges, minimum rarity, and drop weight. Apply & Export writes assets then Modifiers.csv.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh", GUILayout.Height(24)))
                RefreshList();
            if (GUILayout.Button("Apply & Export CSV", GUILayout.Height(24)))
                ApplyAndExport();
            if (GUILayout.Button("Re-import from CSV", GUILayout.Height(24)))
                ReimportFromCsv();
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.HelpBox(_status, MessageType.None);

            EditorGUILayout.Space(6);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            for (int r = 0; r < RarityOrder.Length; r++)
            {
                RarityTier rarity = RarityOrder[r];
                var group = _modifiers
                    .Where(m => m != null && m.minimumRarity == rarity)
                    .OrderBy(m => m.sectionId, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (!_foldouts.ContainsKey(rarity))
                    _foldouts[rarity] = true;

                _foldouts[rarity] = EditorGUILayout.Foldout(
                    _foldouts[rarity],
                    $"{rarity} ({group.Count})",
                    true);

                if (!_foldouts[rarity])
                    continue;

                EditorGUI.indentLevel++;
                DrawColumnHeader();
                for (int i = 0; i < group.Count; i++)
                    DrawModifierRow(group[i]);
                EditorGUI.indentLevel--;
                EditorGUILayout.Space(4);
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawColumnHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("SectionId", EditorStyles.miniBoldLabel, GUILayout.Width(90));
            GUILayout.Label("Cat", EditorStyles.miniBoldLabel, GUILayout.Width(50));
            GUILayout.Label("EffectMin", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Label("EffectMax", EditorStyles.miniBoldLabel, GUILayout.Width(70));
            GUILayout.Label("MinRarity", EditorStyles.miniBoldLabel, GUILayout.Width(100));
            GUILayout.Label("Weight", EditorStyles.miniBoldLabel, GUILayout.Width(60));
            GUILayout.Label("Description", EditorStyles.miniBoldLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawModifierRow(ModifierDefinitionSO mod)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(mod.sectionId ?? string.Empty, GUILayout.Width(90));
            EditorGUILayout.LabelField(mod.category.ToString(), GUILayout.Width(50));

            mod.effectMin = EditorGUILayout.FloatField(mod.effectMin, GUILayout.Width(70));
            mod.effectMax = EditorGUILayout.FloatField(mod.effectMax, GUILayout.Width(70));
            mod.minimumRarity = (RarityTier)EditorGUILayout.EnumPopup(mod.minimumRarity, GUILayout.Width(100));
            mod.weight = Mathf.Max(0, EditorGUILayout.IntField(mod.weight, GUILayout.Width(60)));
            EditorGUILayout.LabelField(mod.effectDescription ?? string.Empty);

            EditorGUILayout.EndHorizontal();

            if (EditorGUI.EndChangeCheck())
            {
                if (mod.effectMax < mod.effectMin)
                    mod.effectMax = mod.effectMin;
                EditorUtility.SetDirty(mod);
            }
        }

        private void RefreshList()
        {
            _modifiers = new List<ModifierDefinitionSO>();
            string[] guids = AssetDatabase.FindAssets(
                "t:ModifierDefinitionSO",
                new[] { CombatCsvRoundTrip.DefaultModifiersFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var mod = AssetDatabase.LoadAssetAtPath<ModifierDefinitionSO>(path);
                if (mod != null)
                    _modifiers.Add(mod);
            }

            _status = $"Loaded {_modifiers.Count} modifiers.";
            Repaint();
        }

        private void ApplyAndExport()
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                ModifierDefinitionSO mod = _modifiers[i];
                if (mod == null) continue;
                if (mod.effectMax < mod.effectMin)
                    mod.effectMax = mod.effectMin;
                mod.weight = Mathf.Max(0, mod.weight);
                EditorUtility.SetDirty(mod);
            }

            AssetDatabase.SaveAssets();
            string result = CombatCsvRoundTrip.ExportModifiersToCSV();
            _status = "Applied & exported. " + result;
            RefreshList();
        }

        private void ReimportFromCsv()
        {
            Combat2DataImporter.RunImportModifiersFromCSV();
            RefreshList();
            _status = "Re-imported modifiers from CSV.";
        }
    }
}
