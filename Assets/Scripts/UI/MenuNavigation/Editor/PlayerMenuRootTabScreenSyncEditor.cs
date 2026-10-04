using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    /// <summary>
    /// Embeds tab body prefabs under PlayerMenuRoot ScreenStackHost and wires tabScreens + initialScreens.
    /// </summary>
    public static class PlayerMenuRootTabScreenSyncEditor
    {
        private const string PlayerMenuRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        private const string SandboxRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab";

        [MenuItem("Tools/Chainbound/UI/Sync Player Menu Tab Screens", priority = 1001)]
        public static void SyncPlayerMenuRootPrefab()
        {
            SyncPrefabAtPath(PlayerMenuRootPrefabPath);
        }

        [MenuItem("Tools/Chainbound/UI/Sync Player Menu Tab Screens (Selected)", priority = 1002)]
        public static void SyncSelectedPlayerMenuRoot()
        {
            PlayerMenuController selected = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PlayerMenuController>(true)
                : null;

            if (selected == null)
            {
                Debug.LogWarning("[PlayerMenuRootTabScreenSync] Select a PlayerMenuRoot instance or open its prefab.");
                return;
            }

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(selected.gameObject);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                Debug.LogWarning("[PlayerMenuRootTabScreenSync] Selected PlayerMenuRoot is not part of a prefab asset.");
                return;
            }

            SyncPrefabAtPath(assetPath);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Wire Tab Prefabs Into Player Menu Roots", priority = 60)]
        public static void WireTabPrefabsIntoPlayerMenuRoots()
        {
            SyncPrefabAtPath(PlayerMenuRootPrefabPath);
            SyncPrefabAtPath(SandboxRootPrefabPath);
        }

        public static void SyncPrefabAtPath(string prefabPath)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError($"[PlayerMenuRootTabScreenSync] Prefab not found at '{prefabPath}'.");
                return;
            }

            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                PlayerMenuController controller = prefabContents.GetComponent<PlayerMenuController>();
                if (controller == null)
                {
                    Debug.LogError("[PlayerMenuRootTabScreenSync] PlayerMenuController missing on prefab root.");
                    return;
                }

                int synced = SyncController(controller, logEachTab: true);
                PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                Debug.Log($"[PlayerMenuRootTabScreenSync] Synced {synced} tab screen(s) on '{prefabPath}'.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static int SyncController(PlayerMenuController controller, bool logEachTab)
        {
            if (controller == null)
                return 0;

            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty tabDefinitionsProperty = serializedController.FindProperty("tabDefinitions");
            SerializedProperty tabScreensProperty = serializedController.FindProperty("tabScreens");
            if (tabDefinitionsProperty == null || tabScreensProperty == null)
                return 0;

            MenuScreenStack stack = controller.GetComponentInChildren<MenuScreenStack>(true);
            Transform host = stack != null ? stack.transform : null;
            if (host == null)
            {
                Debug.LogError("[PlayerMenuRootTabScreenSync] ScreenStackHost / MenuScreenStack not found.");
                return 0;
            }

            int synced = 0;
            int tabCount = Mathf.Min(tabDefinitionsProperty.arraySize, tabScreensProperty.arraySize);
            for (int i = 0; i < tabCount; i++)
            {
                MenuTabDefinitionSO definition =
                    tabDefinitionsProperty.GetArrayElementAtIndex(i).objectReferenceValue as MenuTabDefinitionSO;
                if (definition == null || definition.TabScreenPrefab == null)
                    continue;

                MenuScreen currentScreen = tabScreensProperty.GetArrayElementAtIndex(i).objectReferenceValue as MenuScreen;
                MenuScreen prefab = definition.TabScreenPrefab;
                if (IsValidEmbeddedScreen(currentScreen, definition.TabId) &&
                    IsDesiredPrefabInstance(currentScreen, prefab))
                    continue;

                MenuScreen desiredInstance = FindDesiredPrefabInstance(host, definition.TabId, prefab);
                if (desiredInstance != null)
                {
                    tabScreensProperty.GetArrayElementAtIndex(i).objectReferenceValue = desiredInstance;
                    synced++;

                    if (logEachTab)
                    {
                        Debug.Log(
                            $"[PlayerMenuRootTabScreenSync] Tab '{definition.TabId}' -> reused embedded '{desiredInstance.name}' under ScreenStackHost.",
                            desiredInstance);
                    }

                    continue;
                }

                if (currentScreen != null && currentScreen.transform.IsChildOf(host))
                    Object.DestroyImmediate(currentScreen.gameObject);

                MenuScreen instance = PrefabUtility.InstantiatePrefab(prefab, host) as MenuScreen;
                if (instance == null)
                {
                    Debug.LogWarning($"[PlayerMenuRootTabScreenSync] Failed to instantiate tab prefab for '{definition.TabId}'.");
                    continue;
                }

                instance.name = prefab.name;
                StretchToHost(instance.transform as RectTransform);
                tabScreensProperty.GetArrayElementAtIndex(i).objectReferenceValue = instance;
                synced++;

                if (logEachTab)
                {
                    Debug.Log(
                        $"[PlayerMenuRootTabScreenSync] Tab '{definition.TabId}' -> embedded '{prefab.name}' under ScreenStackHost.",
                        instance);
                }
            }

            SyncInitialScreens(stack, tabScreensProperty);
            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            if (stack != null)
                EditorUtility.SetDirty(stack);

            RemoveUnreferencedDuplicateScreens(host, tabScreensProperty);
            return synced;
        }

        private static MenuScreen FindDesiredPrefabInstance(Transform host, string tabId, MenuScreen prefab)
        {
            if (host == null || prefab == null)
                return null;

            MenuScreen[] screens = host.GetComponentsInChildren<MenuScreen>(true);
            for (int i = 0; i < screens.Length; i++)
            {
                MenuScreen candidate = screens[i];
                if (!IsValidEmbeddedScreen(candidate, tabId))
                    continue;

                if (IsDesiredPrefabInstance(candidate, prefab))
                    return candidate;
            }

            return null;
        }

        private static bool IsDesiredPrefabInstance(MenuScreen screen, MenuScreen prefab)
        {
            if (screen == null || prefab == null)
                return false;

            Object correspondingSource = PrefabUtility.GetCorrespondingObjectFromSource(screen);
            if (correspondingSource == prefab)
                return true;

            string screenPrefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screen.gameObject);
            string desiredPrefabPath = AssetDatabase.GetAssetPath(prefab);
            return !string.IsNullOrWhiteSpace(screenPrefabPath) &&
                !string.IsNullOrWhiteSpace(desiredPrefabPath) &&
                string.Equals(screenPrefabPath, desiredPrefabPath, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void SyncInitialScreens(MenuScreenStack stack, SerializedProperty tabScreensProperty)
        {
            SerializedObject serializedStack = new SerializedObject(stack);
            SerializedProperty initialScreensProperty = serializedStack.FindProperty("initialScreens");
            if (initialScreensProperty == null)
                return;

            initialScreensProperty.arraySize = tabScreensProperty.arraySize;
            for (int i = 0; i < tabScreensProperty.arraySize; i++)
            {
                initialScreensProperty.GetArrayElementAtIndex(i).objectReferenceValue =
                    tabScreensProperty.GetArrayElementAtIndex(i).objectReferenceValue;
            }

            serializedStack.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveUnreferencedDuplicateScreens(Transform host, SerializedProperty tabScreensProperty)
        {
            MenuScreen[] screens = host.GetComponentsInChildren<MenuScreen>(true);
            for (int i = 0; i < screens.Length; i++)
            {
                MenuScreen candidate = screens[i];
                if (candidate == null || IsReferencedByTabScreens(candidate, tabScreensProperty))
                    continue;

            if (candidate is ChainlingsPartyScreen partyScreen && partyScreen.HasEmbeddedLayout)
            {
                Object.DestroyImmediate(candidate.gameObject);
                continue;
            }

            if (candidate is InventoryScreen inventoryScreen && inventoryScreen.HasEmbeddedLayout)
            {
                Object.DestroyImmediate(candidate.gameObject);
                continue;
            }

            if (candidate is ProfileMenuScreen profileScreen && profileScreen.HasEmbeddedLayout)
            {
                Object.DestroyImmediate(candidate.gameObject);
                continue;
            }

            if (candidate is RamShopScreen ramShopScreen && ramShopScreen.HasEmbeddedLayout)
            {
                Object.DestroyImmediate(candidate.gameObject);
                continue;
            }

            if (candidate.transform.childCount == 0)
                Object.DestroyImmediate(candidate.gameObject);
            }
        }

        private static bool IsReferencedByTabScreens(MenuScreen screen, SerializedProperty tabScreensProperty)
        {
            for (int i = 0; i < tabScreensProperty.arraySize; i++)
            {
                if (tabScreensProperty.GetArrayElementAtIndex(i).objectReferenceValue as MenuScreen == screen)
                    return true;
            }

            return false;
        }

        private static bool IsValidEmbeddedScreen(MenuScreen screen, string tabId)
        {
            if (screen == null || !ScreenIdsMatch(screen.ScreenId, tabId))
                return false;

            if (screen is PlaceholderMenuScreen)
                return false;

            if (screen is ChainlingsPartyScreen partyScreen)
                return partyScreen.HasEmbeddedLayout;

            if (screen is InventoryScreen inventoryScreen)
                return inventoryScreen.HasEmbeddedLayout;

            if (screen is ProfileMenuScreen profileScreen)
                return profileScreen.HasEmbeddedLayout;

            if (screen is RamShopScreen ramShopScreen)
                return ramShopScreen.HasEmbeddedLayout;

            return screen.transform.childCount > 0;
        }

        private static bool IsReplaceableStub(MenuScreen screen)
        {
            if (screen == null)
                return true;

            if (screen is PlaceholderMenuScreen)
                return true;

            if (screen is ChainlingsPartyScreen partyScreen)
                return !partyScreen.HasEmbeddedLayout;

            if (screen is InventoryScreen inventoryScreen)
                return !inventoryScreen.HasEmbeddedLayout;

            if (screen is ProfileMenuScreen profileScreen)
                return !profileScreen.HasEmbeddedLayout;

            if (screen is RamShopScreen ramShopScreen)
                return !ramShopScreen.HasEmbeddedLayout;

            return screen.transform.childCount == 0;
        }

        private static bool ScreenIdsMatch(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
        }

        private static void StretchToHost(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }
    }
}
