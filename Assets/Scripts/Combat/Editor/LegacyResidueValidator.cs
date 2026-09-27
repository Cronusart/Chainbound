using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

namespace Chainbound.Combat.Editor
{
    /// <summary>
    /// Editor validator that checks for legacy combat residue in the project.
    /// Scans scenes and prefabs for MonoBehaviours from the legacy SignalJam.Combat namespace.
    /// Menu: Tools/Chainbound/Combat/Legacy/Validate No Legacy Combat
    /// </summary>
    public static class LegacyResidueValidator
    {
        // Known legacy combat type names (from Assets/_LegacyCombat/)
        private static readonly string[] LegacyTypeNames = new string[]
        {
            "SignalJam.Combat.CombatEngine",
            "SignalJam.Combat.TurnResolver",
            "SignalJam.Combat.CombatTestSceneManager",
            "SignalJam.Combat.CombatUIManager",
            "SignalJam.Combat.ActionButtonController",
            "SignalJam.Combat.SwapPanel",
            "SignalJam.Combat.ChainlingStatsPanel",
            "SignalJam.Combat.CombatLogDisplay",
            "SignalJam.Combat.BattleParticipant",
            "SignalJam.Combat.ChainlingInstance",
            "SignalJam.Combat.CombatLog",
            "SignalJam.Combat.DamageCalculator",
            "SignalJam.Combat.StateEdgeResolver",
        };

        // Legacy MonoBehaviour class names (short names for scene scan)
        private static readonly string[] LegacyMonoBehaviourNames = new string[]
        {
            "CombatEngine",
            "CombatTestSceneManager",
            "CombatUIManager",
            "ActionButtonController",
            "SwapPanel",
            "ChainlingStatsPanel",
            "CombatLogDisplay",
        };

        [MenuItem("Tools/Chainbound/Combat/Legacy/Validate No Legacy Combat")]
        public static void ValidateNoLegacyCombat()
        {
            List<string> issues = new List<string>();

            // 1. Check if ENABLE_LEGACY_COMBAT is defined
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            if (defines.Contains("ENABLE_LEGACY_COMBAT"))
            {
                issues.Add("[DEFINE] ENABLE_LEGACY_COMBAT is set in Player Settings! Legacy combat will compile.");
            }

            // 2. Scan all scenes for legacy MonoBehaviours
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            foreach (string guid in sceneGuids)
            {
                string scenePath = AssetDatabase.GUIDToAssetPath(guid);
                ScanSceneForLegacy(scenePath, issues);
            }

            // 3. Scan all prefabs for legacy MonoBehaviours
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid);
                // Skip prefabs inside _LegacyCombat folder
                if (prefabPath.Contains("_LegacyCombat"))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab != null)
                {
                    ScanGameObjectForLegacy(prefab, prefabPath, issues);
                }
            }

            // 4. Check for legacy asmdef being referenced by other assemblies
            string[] asmdefGuids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets" });
            foreach (string guid in asmdefGuids)
            {
                string asmdefPath = AssetDatabase.GUIDToAssetPath(guid);
                if (asmdefPath.Contains("_LegacyCombat"))
                    continue;

                string content = System.IO.File.ReadAllText(asmdefPath);
                if (content.Contains("SignalJam.LegacyCombat"))
                {
                    issues.Add($"[ASMDEF] {asmdefPath} references SignalJam.LegacyCombat assembly!");
                }
            }

            // 5. Report results
            if (issues.Count == 0)
            {
                Debug.Log("<color=green>[Combat Validator] PASSED ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â No legacy combat residue found.</color>");
                EditorUtility.DisplayDialog(
                    "Combat Legacy Validator",
                    "PASSED\n\nNo legacy combat residue found in scenes, prefabs, or assembly definitions.",
                    "OK");
            }
            else
            {
                string report = string.Join("\n", issues);
                Debug.LogError($"[Combat Validator] FAILED ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â {issues.Count} issue(s) found:\n{report}");
                EditorUtility.DisplayDialog(
                    "Combat Legacy Validator",
                    $"FAILED ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â {issues.Count} issue(s) found.\n\nSee Console for full report.",
                    "OK");
            }
        }

        private static void ScanSceneForLegacy(string scenePath, List<string> issues)
        {
            // Skip scenes inside _LegacyCombat folder
            if (scenePath.Contains("_LegacyCombat"))
                return;

            // Load scene contents without opening it
            // We check for missing script references and known legacy type names
            string[] dependencies = AssetDatabase.GetDependencies(scenePath, false);
            foreach (string dep in dependencies)
            {
                if (dep.Contains("_LegacyCombat") && dep.EndsWith(".cs"))
                {
                    issues.Add($"[SCENE] {scenePath} depends on legacy script: {dep}");
                }
            }
        }

        private static void ScanGameObjectForLegacy(GameObject go, string assetPath, List<string> issues)
        {
            MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (var mb in behaviours)
            {
                if (mb == null)
                {
                    // Missing script reference ÃƒÂ¢Ã¢â€šÂ¬Ã¢â‚¬Â could be a removed legacy component
                    issues.Add($"[MISSING SCRIPT] {assetPath} has a missing script reference (possible legacy residue)");
                    continue;
                }

                string typeName = mb.GetType().FullName;
                if (typeName != null && typeName.StartsWith("SignalJam.Combat"))
                {
                    issues.Add($"[PREFAB] {assetPath} contains legacy MonoBehaviour: {typeName}");
                }
            }
        }
    }
}
