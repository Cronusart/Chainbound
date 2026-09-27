using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Monte Carlo rarity / weighted modifier pool simulator for vending levers.
    /// Menu: Tools/Chainbound/Combat/Odds Simulator
    /// </summary>
    public class OddsSimulatorWindow : EditorWindow
    {
        private static readonly RarityTier[] Tiers =
        {
            RarityTier.Common,
            RarityTier.Uncommon,
            RarityTier.Rare,
            RarityTier.Epic,
            RarityTier.Legendary,
            RarityTier.Mythic,
        };

        private VendingConfigSO _vendingConfig;
        private VendingConfigSO _compareConfig;
        private ModifierCatalogSO _catalog;
        private ModifierBalanceOverlaySO _overlay;
        private MoveDefinitionSO _sampleMove;
        private int _simulationCount = 10000;
        private bool _simulateModifierPicks = true;
        private Vector2 _scroll;
        private string _report = string.Empty;

        [MenuItem("Tools/Chainbound/Combat/Odds Simulator")]
        public static void ShowWindow()
        {
            var window = GetWindow<OddsSimulatorWindow>("Odds Simulator");
            window.minSize = new Vector2(560, 420);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Odds Simulator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Monte Carlo rarity rolls via MoveModifierGenerator.GenerateRarityFromVrf with synthetic seeds. Optional weighted modifier picks for a sample move.",
                MessageType.Info);

            _vendingConfig = (VendingConfigSO)EditorGUILayout.ObjectField(
                "Vending Config", _vendingConfig, typeof(VendingConfigSO), false);
            _compareConfig = (VendingConfigSO)EditorGUILayout.ObjectField(
                "Compare Config (optional)", _compareConfig, typeof(VendingConfigSO), false);
            _catalog = (ModifierCatalogSO)EditorGUILayout.ObjectField(
                "Modifier Catalog", _catalog, typeof(ModifierCatalogSO), false);
            _overlay = (ModifierBalanceOverlaySO)EditorGUILayout.ObjectField(
                "Balance Overlay (optional)", _overlay, typeof(ModifierBalanceOverlaySO), false);
            _sampleMove = (MoveDefinitionSO)EditorGUILayout.ObjectField(
                "Sample Move (optional)", _sampleMove, typeof(MoveDefinitionSO), false);

            _simulationCount = Mathf.Max(1, EditorGUILayout.IntField("Simulation Count N", _simulationCount));
            _simulateModifierPicks = EditorGUILayout.Toggle("Simulate Modifier Picks", _simulateModifierPicks);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Use Runtime Default Config", GUILayout.Height(24)))
            {
                if (_vendingConfig != null && _vendingConfig.hideFlags == HideFlags.HideAndDontSave)
                    DestroyImmediate(_vendingConfig);
                _vendingConfig = VendingConfigSO.CreateRuntimeDefault();
            }

            if (GUILayout.Button("Run", GUILayout.Height(24)))
                RunSimulation();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (!string.IsNullOrEmpty(_report))
                EditorGUILayout.TextArea(_report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        private void OnDisable()
        {
            if (_vendingConfig != null && _vendingConfig.hideFlags == HideFlags.HideAndDontSave)
            {
                DestroyImmediate(_vendingConfig);
                _vendingConfig = null;
            }
        }

        private void RunSimulation()
        {
            VendingConfigSO primary = _vendingConfig != null
                ? _vendingConfig
                : (BalanceConfigRuntime.Vending != null
                    ? BalanceConfigRuntime.Vending
                    : VendingConfigSO.CreateRuntimeDefault());

            bool ownsTempPrimary = _vendingConfig == null && primary.hideFlags == HideFlags.HideAndDontSave;

            var sb = new StringBuilder();
            sb.AppendLine($"N = {_simulationCount}");
            sb.AppendLine($"Primary config: {(primary != null ? primary.name : "(runtime default)")}");
            if (_overlay != null)
                sb.AppendLine($"Overlay: {_overlay.overlayId}");
            sb.AppendLine();

            AppendRarityTable(sb, "Primary", primary);

            if (_compareConfig != null)
            {
                sb.AppendLine();
                AppendRarityTable(sb, "Compare", _compareConfig);
                sb.AppendLine();
                AppendDiffTable(sb, primary, _compareConfig);
            }

            if (_simulateModifierPicks && _catalog != null && _sampleMove != null)
            {
                sb.AppendLine();
                AppendModifierPickTable(sb, primary);
            }
            else if (_simulateModifierPicks)
            {
                sb.AppendLine();
                sb.AppendLine("Modifier pick simulation skipped (assign Modifier Catalog + Sample Move).");
            }

            _report = sb.ToString();

            if (ownsTempPrimary)
                DestroyImmediate(primary);
        }

        private void AppendRarityTable(StringBuilder sb, string label, VendingConfigSO config)
        {
            int[] counts = SimulateRarityCounts(config);
            sb.AppendLine($"=== {label} rarity distribution ===");
            sb.AppendLine($"{"Tier",-12} {"Count",8} {"Percent",10} {"Configured%",12}");
            float[] configured = config.rarityTierWeightsPercent;
            float configuredSum = 0f;
            if (configured != null)
            {
                for (int i = 0; i < configured.Length; i++)
                    configuredSum += Mathf.Max(0f, configured[i]);
            }

            for (int i = 0; i < Tiers.Length; i++)
            {
                float pct = _simulationCount > 0 ? (counts[i] * 100f) / _simulationCount : 0f;
                float cfgPct = (configured != null && i < configured.Length && configuredSum > 0f)
                    ? (Mathf.Max(0f, configured[i]) * 100f) / configuredSum
                    : 0f;
                sb.AppendLine($"{Tiers[i],-12} {counts[i],8} {pct,9:0.00}% {cfgPct,11:0.00}%");
            }
        }

        private void AppendDiffTable(StringBuilder sb, VendingConfigSO a, VendingConfigSO b)
        {
            int[] countsA = SimulateRarityCounts(a);
            int[] countsB = SimulateRarityCounts(b);
            sb.AppendLine("=== Side-by-side delta (Compare − Primary) ===");
            sb.AppendLine($"{"Tier",-12} {"Primary%",10} {"Compare%",10} {"Delta pp",10}");
            for (int i = 0; i < Tiers.Length; i++)
            {
                float pctA = _simulationCount > 0 ? (countsA[i] * 100f) / _simulationCount : 0f;
                float pctB = _simulationCount > 0 ? (countsB[i] * 100f) / _simulationCount : 0f;
                sb.AppendLine($"{Tiers[i],-12} {pctA,9:0.00}% {pctB,9:0.00}% {pctB - pctA,9:+0.00;-0.00}%");
            }
        }

        private void AppendModifierPickTable(StringBuilder sb, VendingConfigSO config)
        {
            var pickCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int successes = 0;
            byte[] owner = new byte[32];
            for (int i = 0; i < owner.Length; i++)
                owner[i] = (byte)(i + 1);

            byte[] context = BitConverter.GetBytes(config.generatorVersion);

            for (int i = 0; i < _simulationCount; i++)
            {
                byte[] seed = BuildSyntheticSeed(i);
                ushort moveId = (ushort)((i * 17) & 0xFFFF);
                if (MoveModifierGenerator.TryGenerateModifierFromVrf(
                        seed,
                        actionType: 1,
                        moveId: moveId,
                        ownerPubkey: owner,
                        contextBytes: context,
                        modifierCatalog: _catalog,
                        moveDefinition: _sampleMove,
                        existingModifierSectionIds: null,
                        out string sectionId,
                        out _,
                        maxAttempts: 24,
                        vendingConfig: config,
                        overlay: _overlay))
                {
                    successes++;
                    if (!pickCounts.ContainsKey(sectionId))
                        pickCounts[sectionId] = 0;
                    pickCounts[sectionId]++;
                }
            }

            sb.AppendLine($"=== Modifier picks for '{_sampleMove.moveId}' ===");
            sb.AppendLine($"Successes: {successes}/{_simulationCount} ({(successes * 100f / _simulationCount):0.00}%)");

            var ranked = new List<KeyValuePair<string, int>>(pickCounts);
            ranked.Sort((x, y) => y.Value.CompareTo(x.Value));
            int show = Math.Min(25, ranked.Count);
            sb.AppendLine($"Top {show} sectionIds:");
            for (int i = 0; i < show; i++)
            {
                float pct = _simulationCount > 0 ? (ranked[i].Value * 100f) / _simulationCount : 0f;
                sb.AppendLine($"  {ranked[i].Key,-12} {ranked[i].Value,8}  {pct:0.00}%");
            }
        }

        private int[] SimulateRarityCounts(VendingConfigSO config)
        {
            var counts = new int[Tiers.Length];
            byte[] owner = new byte[32];
            for (int i = 0; i < owner.Length; i++)
                owner[i] = (byte)(0xA0 + i);

            byte[] context = BitConverter.GetBytes(config != null ? config.generatorVersion : 2);

            for (int i = 0; i < _simulationCount; i++)
            {
                byte[] seed = BuildSyntheticSeed(i);
                ushort moveId = (ushort)((i * 31) & 0xFFFF);
                RarityTier rarity = MoveModifierGenerator.GenerateRarityFromVrf(
                    seed,
                    actionType: 1,
                    moveId: moveId,
                    ownerPubkey: owner,
                    contextBytes: context,
                    vendingConfig: config);

                int idx = Array.IndexOf(Tiers, rarity);
                if (idx < 0) idx = 0;
                counts[idx]++;
            }

            return counts;
        }

        private static byte[] BuildSyntheticSeed(int index)
        {
            var seed = new byte[32];
            byte[] indexBytes = BitConverter.GetBytes(index);
            byte[] mixBytes = BitConverter.GetBytes(unchecked(index * 0x9E3779B9));
            Buffer.BlockCopy(indexBytes, 0, seed, 0, 4);
            Buffer.BlockCopy(mixBytes, 0, seed, 4, 4);
            for (int i = 8; i < seed.Length; i++)
                seed[i] = (byte)(indexBytes[i % 4] ^ mixBytes[i % 4] ^ (i * 13));
            return seed;
        }
    }
}
