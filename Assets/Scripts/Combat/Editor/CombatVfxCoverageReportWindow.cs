using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    public sealed class CombatVfxCoverageReportWindow : EditorWindow
    {
        private sealed class Row
        {
            public MoveDefinitionSO move;
            public MoveVfxRole role;
            public VfxResolution resolution;
        }

        private readonly List<Row> _rows = new List<Row>();
        private readonly Dictionary<VfxResolveTier, int> _counts = new Dictionary<VfxResolveTier, int>();
        private readonly Dictionary<string, int> _emptyPreferredSlots = new Dictionary<string, int>();
        private CombatVFXLibrary _library;
        private Vector2 _scroll;
        private string _search = string.Empty;
        private bool _onlyMissing;
        private bool _relevantPhaseMissing;

        [MenuItem("Tools/Chainbound/Combat/VFX/Coverage Report")]
        public static void Open()
        {
            GetWindow<CombatVfxCoverageReportWindow>("Combat VFX Coverage").Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _library = (CombatVFXLibrary)EditorGUILayout.ObjectField("Library", _library, typeof(CombatVFXLibrary), false);
                if (GUILayout.Button("Refresh", GUILayout.Width(80f)))
                    Refresh();
                if (GUILayout.Button("Copy as Markdown", GUILayout.Width(130f)))
                    EditorGUIUtility.systemCopyBuffer = BuildMarkdown();
            }

            _search = EditorGUILayout.TextField("Filter", _search);
            _onlyMissing = EditorGUILayout.Toggle("Only unresolved", _onlyMissing);
            _relevantPhaseMissing = EditorGUILayout.Toggle("Relevant phase missing", _relevantPhaseMissing);

            if (_library == null)
            {
                EditorGUILayout.HelpBox("Assign or create a CombatVFXLibrary asset.", MessageType.Warning);
                return;
            }

            int resolved = _rows.Count(r => r.resolution.tier != VfxResolveTier.None);
            int phaseFilled = _rows.Count(r => r.resolution.isComposed);
            EditorGUILayout.LabelField(
                $"Usable moves: {_rows.Count}   Resolved: {resolved}   Missing: {_rows.Count - resolved}   Phase-filled from fallback: {phaseFilled}",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Join("  |  ", _counts.OrderBy(k => k.Key).Select(k => $"{k.Key}: {k.Value}")), EditorStyles.wordWrappedMiniLabel);

            if (_emptyPreferredSlots.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Empty but used preferred slots", EditorStyles.boldLabel);
                EditorGUILayout.LabelField(string.Join(", ", _emptyPreferredSlots.OrderBy(k => k.Key).Select(k => $"{k.Key} ({k.Value})")), EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Move", GUILayout.Width(190f));
                GUILayout.Label("Type", GUILayout.Width(80f));
                GUILayout.Label("Range / Role", GUILayout.Width(110f));
                GUILayout.Label("Tier", GUILayout.Width(95f));
                GUILayout.Label("Winning Slot", GUILayout.Width(150f));
                GUILayout.Label("Cast", GUILayout.Width(130f));
                GUILayout.Label("Strike", GUILayout.Width(130f));
                GUILayout.Label("Projectile", GUILayout.Width(130f));
                GUILayout.Label("Impact", GUILayout.Width(130f));
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (Row row in FilteredRows())
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(row.move.moveId, GUILayout.Width(190f));
                    GUILayout.Label(row.move.baseType.ToString(), GUILayout.Width(80f));
                    string rangeRole = row.role == MoveVfxRole.Damage
                        ? (row.move.category == MoveCategory.DamagingRanged ? "Ranged" : "Melee")
                        : row.role.ToString();
                    GUILayout.Label(rangeRole, GUILayout.Width(110f));
                    GUILayout.Label(row.resolution.tier.ToString(), GUILayout.Width(95f));
                    GUILayout.Label(row.resolution.slotLabel, GUILayout.Width(150f));
                    GUILayout.Label(row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Cast), GUILayout.Width(130f));
                    GUILayout.Label(row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Strike), GUILayout.Width(130f));
                    GUILayout.Label(row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Projectile), GUILayout.Width(130f));
                    GUILayout.Label(row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Impact), GUILayout.Width(130f));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private IEnumerable<Row> FilteredRows()
        {
            return _rows.Where(row =>
                (!_onlyMissing || row.resolution.tier == VfxResolveTier.None) &&
                (!_relevantPhaseMissing || RelevantPhaseMissing(row)) &&
                (string.IsNullOrWhiteSpace(_search) ||
                 row.move.moveId.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 row.role.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 row.resolution.tier.ToString().IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static bool RelevantPhaseMissing(Row row)
        {
            if (row.role != MoveVfxRole.Damage)
                return false;
            if (row.resolution.PhaseTier(CombatVFXLibrary.VFXPhase.Impact) == VfxResolveTier.None)
                return true;
            return row.move.category == MoveCategory.DamagingRanged &&
                   row.resolution.PhaseTier(CombatVFXLibrary.VFXPhase.Projectile) == VfxResolveTier.None;
        }

        private void Refresh()
        {
            if (_library == null)
            {
                string guid = AssetDatabase.FindAssets("t:CombatVFXLibrary").FirstOrDefault();
                if (!string.IsNullOrEmpty(guid))
                    _library = AssetDatabase.LoadAssetAtPath<CombatVFXLibrary>(AssetDatabase.GUIDToAssetPath(guid));
            }

            _rows.Clear();
            _counts.Clear();
            _emptyPreferredSlots.Clear();
            if (_library == null)
                return;

            string[] guids = AssetDatabase.FindAssets("t:MoveDefinitionSO");
            for (int i = 0; i < guids.Length; i++)
            {
                MoveDefinitionSO move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (move == null || move.category == MoveCategory.Passive)
                    continue;

                MoveVfxRole role = MoveVfxRoleClassifier.Classify(move);
                VfxResolution resolution = _library.Resolve(VfxMoveKey.From(move));
                _rows.Add(new Row { move = move, role = role, resolution = resolution });
                _counts[resolution.tier] = _counts.TryGetValue(resolution.tier, out int count) ? count + 1 : 1;

                string preferred = PreferredSlot(move, role);
                if (!PreferredSlotIsUsable(move, role))
                    _emptyPreferredSlots[preferred] = _emptyPreferredSlots.TryGetValue(preferred, out count) ? count + 1 : 1;
            }

            _rows.Sort((a, b) => string.Compare(a.move.moveId, b.move.moveId, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private bool PreferredSlotIsUsable(MoveDefinitionSO move, MoveVfxRole role)
        {
            CombatVFXFallbacks fallback = _library.Fallbacks;
            if (fallback == null)
                return false;
            CombatVFXLibrary.VFXEntry entry = role == MoveVfxRole.Damage
                ? fallback.GetTyped(move.baseType, move.category)
                : fallback.GetRole(role);
            return CombatVfxResolver.IsUsable(entry);
        }

        private static string PreferredSlot(MoveDefinitionSO move, MoveVfxRole role)
        {
            if (role != MoveVfxRole.Damage)
                return $"Role:{role}";
            string range = move.category == MoveCategory.DamagingRanged ? "Ranged" : "Melee";
            return $"Typed:{move.baseType}/{range}";
        }

        private string BuildMarkdown()
        {
            var builder = new StringBuilder();
            int resolved = _rows.Count(r => r.resolution.tier != VfxResolveTier.None);
            int phaseFilled = _rows.Count(r => r.resolution.isComposed);
            builder.AppendLine("# Combat VFX fallback coverage");
            builder.AppendLine();
            builder.AppendLine($"- Usable moves: {_rows.Count}");
            builder.AppendLine($"- Resolved: {resolved}");
            builder.AppendLine($"- Missing: {_rows.Count - resolved}");
            builder.AppendLine($"- Phase-filled from fallback: {phaseFilled}");
            foreach (KeyValuePair<VfxResolveTier, int> pair in _counts.OrderBy(k => k.Key))
                builder.AppendLine($"- {pair.Key}: {pair.Value}");
            builder.AppendLine();
            builder.AppendLine("| Move | Type | Range / Role | Tier | Winning slot | Cast | Strike | Projectile | Impact |");
            builder.AppendLine("|---|---|---|---|---|---|---|---|---|");
            foreach (Row row in FilteredRows())
            {
                string rangeRole = row.role == MoveVfxRole.Damage
                    ? (row.move.category == MoveCategory.DamagingRanged ? "Ranged" : "Melee")
                    : row.role.ToString();
                builder.AppendLine(
                    $"| {row.move.moveId} | {row.move.baseType} | {rangeRole} | {row.resolution.tier} | {row.resolution.slotLabel} | " +
                    $"{row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Cast)} | " +
                    $"{row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Strike)} | " +
                    $"{row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Projectile)} | " +
                    $"{row.resolution.PhaseLabel(CombatVFXLibrary.VFXPhase.Impact)} |");
            }
            return builder.ToString();
        }
    }
}
