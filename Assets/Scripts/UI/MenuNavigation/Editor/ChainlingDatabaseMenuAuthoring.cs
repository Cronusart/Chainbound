using System;
using System.Collections.Generic;
using System.Linq;
using Chainbound.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    [InitializeOnLoad]
    public static class ChainlingDatabaseMenuAuthoring
    {
        const string PlayerMenuRootPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        const string SandboxRootPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab";
        const string CatalogPath = "Assets/Data/Combat/Encounters/EncounterCatalogSource.asset";
        const string SpeciesDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Database/SpeciesDatabase.asset";
        const string VisualDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Visual/Database/CombatVisualDatabase.asset";
        const string IdleButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png";
        const string SelectedButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png";
        const string FontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
        const string MotionPath = "Assets/Scripts/UI/UiMotionProfile.asset";
        const string SessionKey = "ChainlingDatabase.Authored";

        static bool invoked;
        static bool upgrading;

        static ChainlingDatabaseMenuAuthoring()
        {
            EditorApplication.delayCall += UpgradeIfPlaceholderRemains;
        }

        [MenuItem("Tools/Chainbound/UI/Author Chainling Database")]
        public static void AuthorFromMenu()
        {
            UpgradeAll();
            SessionState.SetBool(SessionKey, true);
        }

        public static void UpgradeIfPlaceholderRemains()
        {
            if (invoked || upgrading || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;
            if (SessionState.GetBool(SessionKey, false))
                return;

            invoked = true;
            try
            {
                UpgradeAll();
                SessionState.SetBool(SessionKey, true);
            }
            catch (Exception exception)
            {
                invoked = false;
                Debug.LogError("[ChainlingDatabase] Authoring failed: " + exception);
            }
        }

        public static void UpgradeAll()
        {
            invoked = true;
            if (upgrading)
                return;

            upgrading = true;
            try
            {
                EncounterCatalogSourceSO catalog = LoadOrCreateCatalog();
                UpgradePrefab(PlayerMenuRootPath, catalog);
                UpgradePrefab(SandboxRootPath, catalog);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                upgrading = false;
            }
        }

        static void UpgradePrefab(string path, EncounterCatalogSourceSO catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                return;

            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool editingOpenStage = stage != null &&
                string.Equals(stage.assetPath.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase);
            GameObject root = editingOpenStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                return;

            try
            {
                bool changed = UpgradeRoot(root, catalog);
                if (!changed)
                    return;

                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (!editingOpenStage && root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool UpgradeRoot(GameObject root, EncounterCatalogSourceSO catalog)
        {
            bool changed = false;
            MenuScreen[] screens = root.GetComponentsInChildren<MenuScreen>(true);
            for (int i = 0; i < screens.Length; i++)
            {
                if (!string.Equals(screens[i].ScreenId, "database", StringComparison.Ordinal))
                    continue;
                changed |= UpgradeDatabase(root, screens[i], catalog);
            }

            changed |= AssignPartyCatalog(root, catalog);
            return changed;
        }

        static bool UpgradeDatabase(GameObject root, MenuScreen screen, EncounterCatalogSourceSO catalog)
        {
            if (screen == null)
                return false;

            bool changed = false;
            GameObject tab = screen.gameObject;
            ChainlingDatabaseScreen database = tab.GetComponent<ChainlingDatabaseScreen>();
            if (database == null)
            {
                int slot = FindTabSlot(root, screen);
                ClearChildren(tab.transform);
                UnityEngine.Object.DestroyImmediate(screen);
                database = tab.AddComponent<ChainlingDatabaseScreen>();
                database.SetScreenIdentity("database", "Chainling Database", tab.GetComponent<CanvasGroup>());
                PlayerMenuController controller = root.GetComponentInChildren<PlayerMenuController>(true);
                if (controller != null && slot >= 0)
                    controller.SetTabScreen(slot, database);
                changed = true;
            }

            Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>(IdleButtonPath);
            Sprite selected = AssetDatabase.LoadAssetAtPath<Sprite>(SelectedButtonPath);
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Texture scanline = FindScanlineTexture(root);
            CombatVisualDatabaseSO visuals = AssetDatabase.LoadAssetAtPath<CombatVisualDatabaseSO>(VisualDatabasePath);
            if (AssignMissingReferences(database, catalog, visuals, idle, selected, font, scanline))
                changed = true;

            bool hadLayout = tab.transform.Find("TopLeft") != null;
            database.EnsureLayoutForEditor();
            if (!hadLayout)
                changed = true;

            changed |= EnsureBackground(tab.transform, scanline);
            changed |= EnsureMotion(tab.transform, idle, selected);
            return changed;
        }

        static bool AssignMissingReferences(
            ChainlingDatabaseScreen database,
            EncounterCatalogSourceSO catalog,
            CombatVisualDatabaseSO visuals,
            Sprite idle,
            Sprite selected,
            TMP_FontAsset font,
            Texture scanline)
        {
            SerializedObject serialized = new SerializedObject(database);
            bool changed = false;
            changed |= Assign(serialized, "encounterCatalog", catalog);
            changed |= Assign(serialized, "visualDatabase", visuals);
            changed |= Assign(serialized, "idleButtonSprite", idle);
            changed |= Assign(serialized, "selectedButtonSprite", selected);
            changed |= Assign(serialized, "labelFont", font);
            changed |= Assign(serialized, "scanlineTexture", scanline);
            if (changed)
                serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        static bool Assign(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            if (value == null)
                return false;

            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue != null)
                return false;

            property.objectReferenceValue = value;
            return true;
        }

        static bool EnsureBackground(Transform tab, Texture scanline)
        {
            Transform background = tab.Find("DatabaseBackground");
            if (background == null)
                return false;

            RawImage overlay = background.Find("ScanlineOverlay")?.GetComponent<RawImage>();
            Image tint = background.Find("BaseTint")?.GetComponent<Image>();
            if (overlay != null)
            {
                overlay.raycastTarget = false;
                if (overlay.texture == null && scanline != null)
                    overlay.texture = scanline;
                overlay.gameObject.SetActive(overlay.texture != null);
            }

            PlayerMenuBackgroundController controller = background.GetComponent<PlayerMenuBackgroundController>();
            bool added = false;
            if (controller == null)
            {
                controller = background.gameObject.AddComponent<PlayerMenuBackgroundController>();
                added = true;
            }

            SerializedObject serialized = new SerializedObject(controller);
            bool changed = added;
            changed |= Assign(serialized, "baseTintImage", tint);
            changed |= Assign(serialized, "scanlineOverlay", overlay);
            changed |= Assign(serialized, "scanlineTexture", scanline);
            SerializedProperty enabled = serialized.FindProperty("scanlinesEnabled");
            if (enabled != null && scanline != null && !enabled.boolValue)
            {
                enabled.boolValue = true;
                changed = true;
            }

            SerializedProperty respect = serialized.FindProperty("respectAuthoredPresentation");
            if (respect != null && !respect.boolValue)
            {
                respect.boolValue = true;
                changed = true;
            }

            SerializedProperty above = serialized.FindProperty("scanlinesAboveContent");
            if (above != null && above.boolValue)
            {
                above.boolValue = false;
                changed = true;
            }

            SerializedProperty blocks = serialized.FindProperty("baseTintBlocksRaycasts");
            if (blocks != null && blocks.boolValue)
            {
                blocks.boolValue = false;
                changed = true;
            }

            if (changed)
                serialized.ApplyModifiedPropertiesWithoutUndo();
            if (tint != null)
                tint.raycastTarget = false;
            return changed;
        }

        static bool EnsureMotion(Transform tab, Sprite idle, Sprite selected)
        {
            Type motionType = TypeCache.GetTypesDerivedFrom<MonoBehaviour>()
                .FirstOrDefault(candidate => candidate.Name == "UiSelectableMotion");
            if (motionType == null)
                return false;

            UnityEngine.Object profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MotionPath);
            bool changed = false;
            Button[] buttons = tab.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].GetComponent(motionType) != null)
                    continue;

                Component motion = buttons[i].gameObject.AddComponent(motionType);
                SerializedObject serialized = new SerializedObject(motion);
                Assign(serialized, "buttonImage", buttons[i].targetGraphic);
                Assign(serialized, "deselectedSprite", idle);
                Assign(serialized, "selectedSprite", selected);
                Assign(serialized, "profile", profile);
                SerializedProperty shimmer = serialized.FindProperty("shimmerOwnsSelectedVisual");
                if (shimmer != null)
                    shimmer.boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            return changed;
        }

        static bool AssignPartyCatalog(GameObject root, EncounterCatalogSourceSO catalog)
        {
            if (catalog == null)
                return false;

            bool changed = false;
            ChainlingsPartyScreen[] parties = root.GetComponentsInChildren<ChainlingsPartyScreen>(true);
            for (int i = 0; i < parties.Length; i++)
            {
                if (parties[i].EncounterCatalog != null)
                    continue;

                SerializedObject serialized = new SerializedObject(parties[i]);
                if (Assign(serialized, "encounterCatalog", catalog))
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }

            return changed;
        }

        static int FindTabSlot(GameObject root, MenuScreen screen)
        {
            PlayerMenuController controller = root.GetComponentInChildren<PlayerMenuController>(true);
            if (controller == null)
                return -1;

            for (int i = 0; i < controller.GetTabScreenCount(); i++)
            {
                if (controller.GetTabScreen(i) == screen)
                    return i;
            }

            return -1;
        }

        static Texture FindScanlineTexture(GameObject root)
        {
            PlayerMenuBackgroundController[] controllers = root.GetComponentsInChildren<PlayerMenuBackgroundController>(true);
            for (int i = 0; i < controllers.Length; i++)
            {
                if (controllers[i].transform.name == "DatabaseBackground")
                    continue;
                if (controllers[i].ScanlineTexture != null)
                    return controllers[i].ScanlineTexture;
                if (controllers[i].ScanlineOverlay != null && controllers[i].ScanlineOverlay.texture != null)
                    return controllers[i].ScanlineOverlay.texture;
            }

            return null;
        }

        static EncounterCatalogSourceSO LoadOrCreateCatalog()
        {
            EncounterCatalogSourceSO catalog = AssetDatabase.LoadAssetAtPath<EncounterCatalogSourceSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<EncounterCatalogSourceSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SpeciesDatabaseSO species = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(SpeciesDatabasePath);
            var zones = new List<EncounterZoneSO>();
            string[] guids = AssetDatabase.FindAssets("t:EncounterZoneSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(path);
                if (zone != null)
                    zones.Add(zone);
            }

            zones.Sort((a, b) => string.Compare(a.zoneId, b.zoneId, StringComparison.Ordinal));
            if (SameCatalog(catalog, species, zones))
                return catalog;

            catalog.speciesDatabase = species;
            catalog.zones = zones;
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static bool SameCatalog(EncounterCatalogSourceSO catalog, SpeciesDatabaseSO species, List<EncounterZoneSO> zones)
        {
            if (catalog.speciesDatabase != species || catalog.zones == null || catalog.zones.Count != zones.Count)
                return false;

            for (int i = 0; i < zones.Count; i++)
            {
                if (catalog.zones[i] != zones[i])
                    return false;
            }

            return true;
        }

        static void ClearChildren(Transform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }
    }
}
