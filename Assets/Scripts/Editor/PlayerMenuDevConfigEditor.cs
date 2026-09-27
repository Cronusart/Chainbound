#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using UnityEditor;
using UnityEngine;

namespace SignalJam.Editor
{
    public static class PlayerMenuDevConfigEditor
    {
        private const string ConfigPath = "Assets/Data/UI/PlayerMenuDevConfig.asset";
        private static readonly string[] ProductionPrefabPaths =
        {
            "Assets/Prefabs/GameServices/GameServices.prefab",
            "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab",
            "Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab",
        };

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Use Production Chainlings Data", priority = 10)]
        public static void UseProductionChainlingsData()
        {
            PlayerMenuDevConfigSO config = LoadOrCreateConfig();
            config.SetChainlingsDataSource(ChainlingsDataSourceMode.Production);
            config.SetBypassAvailabilityGate(false);
            SaveConfig(config);

            int removedHarnessCount = 0;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                removedHarnessCount = ScrubHarnessComponentsFromProductionPrefabs();
            }
            else
            {
                Debug.LogWarning(
                    "[PlayerMenuDevConfig] Production mode was selected, but prefab cleanup was deferred until Edit Mode.");
            }

            Debug.Log(
                $"[PlayerMenuDevConfig] Chainlings data source set to Production. " +
                $"Removed {removedHarnessCount} persisted test harness component(s) from production prefabs.");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Use Harness Chainlings Data", priority = 11)]
        public static void UseHarnessChainlingsData()
        {
            PlayerMenuDevConfigSO config = LoadOrCreateConfig();
            config.SetChainlingsDataSource(ChainlingsDataSourceMode.Harness);
            config.SetBypassAvailabilityGate(true);
            SaveConfig(config);
            Debug.Log("[PlayerMenuDevConfig] Chainlings data source set to Harness (in-memory sandbox roster).");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Toggle Bypass Availability Gate", priority = 20)]
        public static void ToggleBypassAvailabilityGate()
        {
            PlayerMenuDevConfigSO config = LoadOrCreateConfig();
            config.SetBypassAvailabilityGate(!config.BypassAvailabilityGate);
            SaveConfig(config);
            Debug.Log($"[PlayerMenuDevConfig] bypassAvailabilityGate={(config.BypassAvailabilityGate ? "ON" : "OFF")}.");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Select Dev Config Asset", priority = 30)]
        public static void SelectDevConfigAsset()
        {
            PlayerMenuDevConfigSO config = LoadOrCreateConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Use Production Chainlings Data", validate = true)]
        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Use Harness Chainlings Data", validate = true)]
        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Toggle Bypass Availability Gate", validate = true)]
        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Select Dev Config Asset", validate = true)]
        private static bool ValidateMenuItems()
        {
            return LoadConfig() != null || AssetDatabase.IsValidFolder("Assets/Data/UI");
        }

        private static PlayerMenuDevConfigSO LoadOrCreateConfig()
        {
            PlayerMenuDevConfigSO config = LoadConfig();
            if (config != null)
                return config;

            config = ScriptableObject.CreateInstance<PlayerMenuDevConfigSO>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static PlayerMenuDevConfigSO LoadConfig()
        {
            return AssetDatabase.LoadAssetAtPath<PlayerMenuDevConfigSO>(ConfigPath);
        }

        private static void SaveConfig(PlayerMenuDevConfigSO config)
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static int ScrubHarnessComponentsFromProductionPrefabs()
        {
            int removedCount = 0;
            for (int i = 0; i < ProductionPrefabPaths.Length; i++)
            {
                string path = ProductionPrefabPaths[i];
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    continue;

                GameObject prefabRoot = null;
                try
                {
                    prefabRoot = PrefabUtility.LoadPrefabContents(path);
                    ChainlingsMenuTestHarness[] harnesses =
                        prefabRoot.GetComponentsInChildren<ChainlingsMenuTestHarness>(true);
                    if (harnesses == null || harnesses.Length == 0)
                        continue;

                    for (int harnessIndex = 0; harnessIndex < harnesses.Length; harnessIndex++)
                    {
                        if (harnesses[harnessIndex] == null)
                            continue;

                        Object.DestroyImmediate(harnesses[harnessIndex], true);
                        removedCount++;
                    }

                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                }
                finally
                {
                    if (prefabRoot != null)
                        PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }

            AssetDatabase.SaveAssets();
            return removedCount;
        }
    }
}
#endif
