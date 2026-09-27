using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Validates and wires player-menu tab bodies authored on <see cref="PlayerMenuController.tabScreens"/>.
    /// PlayerMenuRoot prefab is the source of truth; tab definition prefabs are editor-sync and dev fallback only.
    /// </summary>
    public static class PlayerMenuTabScreenInstaller
    {
        private const string ChainlingsTabId = "chainlings";
        private const string InventoryTabId = "inventory";

        public static void InstallAll(PlayerMenuController controller, bool enableDebugLogs)
        {
            if (controller == null)
                return;

            MenuTabDefinitionSO[] definitions = controller.GetTabDefinitions();
            if (definitions == null)
                return;

            for (int i = 0; i < definitions.Length; i++)
                InstallTab(controller, i, enableDebugLogs);
        }

        public static bool InstallTab(PlayerMenuController controller, int tabIndex, bool enableDebugLogs)
        {
            if (controller == null || !controller.TryGetTabDefinition(tabIndex, out MenuTabDefinitionSO definition))
                return false;

            MenuScreen assignedScreen = controller.GetTabScreen(tabIndex);
            MenuScreen resolvedScreen = ResolveScreen(controller, tabIndex, definition, assignedScreen, enableDebugLogs);
            if (resolvedScreen == null)
                return false;

            controller.SetTabScreen(tabIndex, resolvedScreen);
            RemoveDuplicateScreens(controller, tabIndex, definition.TabId, resolvedScreen);
            FinalizeScreen(controller, definition, resolvedScreen);
            resolvedScreen.gameObject.SetActive(false);
            return true;
        }

        private static MenuScreen ResolveScreen(
            PlayerMenuController controller,
            int tabIndex,
            MenuTabDefinitionSO definition,
            MenuScreen assignedScreen,
            bool enableDebugLogs)
        {
            string tabId = definition.TabId;
            PlayerMenuTabScreenResolveMode mode = definition.ScreenResolveMode;

            MenuScreen prefab = definition.TabScreenPrefab;

            if (mode != PlayerMenuTabScreenResolveMode.PrefabOnly)
            {
                MenuScreen embedded = FindEmbeddedScreen(controller, tabId, assignedScreen, prefab);
                if (embedded != null)
                    return embedded;
            }

            if (mode == PlayerMenuTabScreenResolveMode.EmbeddedOnly)
            {
                LogMissingScreen(controller, definition, enableDebugLogs,
                    "EmbeddedOnly is enabled but no tab screen is assigned on PlayerMenuRoot.tabScreens " +
                    "or embedded under ScreenStackHost.");
                return assignedScreen;
            }

            if (!IsReplaceableStub(assignedScreen) && IsPreferredEmbeddedScreen(assignedScreen, prefab))
                return assignedScreen;

            if (prefab == null)
            {
                if (ScreenIdsMatch(tabId, InventoryTabId))
                    return CreateGeneratedInventoryScreen(controller, assignedScreen, enableDebugLogs);

                LogMissingScreen(controller, definition, enableDebugLogs,
                    "Assign a tab body prefab on PlayerMenuRoot (recommended) or set MenuTabDefinitionSO.tabScreenPrefab " +
                    "and run Tools/Chainbound/UI/Sync Player Menu Tab Screens.");
                return assignedScreen;
            }

            return InstantiateFallbackScreen(controller, tabIndex, definition, assignedScreen, prefab, enableDebugLogs);
        }

        private static InventoryScreen CreateGeneratedInventoryScreen(
            PlayerMenuController controller,
            MenuScreen assignedScreen,
            bool enableDebugLogs)
        {
            MenuScreenStack stack = controller != null ? controller.GetScreenStack() : null;
            Transform host = stack != null ? stack.transform : null;
            if (host == null)
                return assignedScreen as InventoryScreen;

            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    "[PlayerMenuTabScreenInstaller] Inventory tab prefab is not wired yet. " +
                    "Generating a temporary InventoryView layout. Run Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory View Prefab " +
                    "to create the authored prefab and embed it on PlayerMenuRoot.",
                    controller);
            }

            GameObject screenRoot = new GameObject(
                "InventoryView_RuntimeFallback",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(InventoryScreen),
                typeof(InventoryMotionController));
            screenRoot.transform.SetParent(host, false);
            StretchToHost(screenRoot.transform as RectTransform);

            InventoryScreen inventoryScreen = screenRoot.GetComponent<InventoryScreen>();
            CanvasGroup group = screenRoot.GetComponent<CanvasGroup>();
            inventoryScreen.SetScreenIdentity(InventoryTabId, "Inventory", group);
            inventoryScreen.BuildDefaultLayoutForEditor();

            if (assignedScreen != null && assignedScreen.gameObject != screenRoot)
                DestroyScreenObject(assignedScreen.gameObject);

            return inventoryScreen;
        }

        private static MenuScreen FindEmbeddedScreen(
            PlayerMenuController controller,
            string tabId,
            MenuScreen assignedScreen,
            MenuScreen prefab)
        {
            if (IsValidEmbeddedScreen(assignedScreen, tabId) && IsPreferredEmbeddedScreen(assignedScreen, prefab))
                return assignedScreen;

            MenuScreenStack stack = controller.GetScreenStack();
            if (stack == null)
                return null;

            MenuScreen[] screens = stack.GetComponentsInChildren<MenuScreen>(true);
            for (int i = 0; i < screens.Length; i++)
            {
                MenuScreen candidate = screens[i];
                if (candidate == null || candidate == assignedScreen)
                    continue;

                if (!ScreenIdsMatch(candidate.ScreenId, tabId))
                    continue;

                if (IsValidEmbeddedScreen(candidate, tabId) && IsPreferredEmbeddedScreen(candidate, prefab))
                    return candidate;
            }

            if (prefab == null)
            {
                for (int i = 0; i < screens.Length; i++)
                {
                    MenuScreen candidate = screens[i];
                    if (candidate == null || candidate == assignedScreen)
                        continue;

                    if (!ScreenIdsMatch(candidate.ScreenId, tabId))
                        continue;

                    if (IsValidEmbeddedScreen(candidate, tabId))
                        return candidate;
                }
            }

            return null;
        }

        private static bool IsPreferredEmbeddedScreen(MenuScreen screen, MenuScreen prefab)
        {
            if (screen == null)
                return false;

            if (prefab == null)
                return true;

            if (screen == prefab)
                return true;

            return string.Equals(screen.name, prefab.name, System.StringComparison.OrdinalIgnoreCase) ||
                screen.name.StartsWith(prefab.name + " (", System.StringComparison.OrdinalIgnoreCase);
        }

        private static MenuScreen InstantiateFallbackScreen(
            PlayerMenuController controller,
            int tabIndex,
            MenuTabDefinitionSO definition,
            MenuScreen assignedScreen,
            MenuScreen prefab,
            bool enableDebugLogs)
        {
            MenuScreenStack stack = controller.GetScreenStack();
            Transform host = stack != null ? stack.transform : null;
            if (host == null)
            {
                if (enableDebugLogs)
                {
                    Debug.LogWarning(
                        $"[PlayerMenuTabScreenInstaller] ScreenStackHost is missing; cannot create fallback tab '{definition.TabId}'.",
                        controller);
                }

                return assignedScreen;
            }

            if (enableDebugLogs)
            {
                Debug.LogWarning(
                    $"[PlayerMenuTabScreenInstaller] Tab '{definition.TabId}' has no embedded body on PlayerMenuRoot. " +
                    "Instantiating fallback from MenuTabDefinitionSO.tabScreenPrefab. " +
                    "Run Tools/Chainbound/UI/Sync Player Menu Tab Screens to embed it in the prefab.",
                    controller);
            }

            MenuScreen instance = Object.Instantiate(prefab, host);
            instance.name = prefab.name;

            if (assignedScreen != null && assignedScreen.gameObject != instance.gameObject)
                DestroyScreenObject(assignedScreen.gameObject);

            controller.SetTabScreen(tabIndex, instance);
            return instance;
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

            return screen.transform.childCount == 0;
        }

        private static void RemoveDuplicateScreens(
            PlayerMenuController controller,
            int tabIndex,
            string tabId,
            MenuScreen resolvedScreen)
        {
            MenuScreenStack stack = controller != null ? controller.GetScreenStack() : null;
            if (stack == null || resolvedScreen == null || string.IsNullOrWhiteSpace(tabId))
                return;

            MenuScreen[] screens = stack.GetComponentsInChildren<MenuScreen>(true);
            for (int i = 0; i < screens.Length; i++)
            {
                MenuScreen candidate = screens[i];
                if (candidate == null || candidate == resolvedScreen)
                    continue;

                if (!ScreenIdsMatch(candidate.ScreenId, tabId))
                    continue;

                if (IsReferencedByTabScreens(controller, candidate))
                    continue;

                DestroyScreenObject(candidate.gameObject);
            }
        }

        private static bool IsReferencedByTabScreens(PlayerMenuController controller, MenuScreen screen)
        {
            int count = controller.GetTabScreenCount();
            for (int i = 0; i < count; i++)
            {
                if (controller.GetTabScreen(i) == screen)
                    return true;
            }

            return false;
        }

        private static void FinalizeScreen(
            PlayerMenuController controller,
            MenuTabDefinitionSO definition,
            MenuScreen screen)
        {
            if (screen == null || definition == null)
                return;

            if (screen is ChainlingsPartyScreen partyScreen)
                FinalizeChainlingsScreen(controller, partyScreen);
            else if (screen is InventoryScreen inventoryScreen)
                FinalizeInventoryScreen(inventoryScreen);
        }

        private static void FinalizeChainlingsScreen(PlayerMenuController controller, ChainlingsPartyScreen partyScreen)
        {
            ChainlingsMenuViewsSO views = controller != null ? controller.ChainlingsMenuViews : null;

            ChainlingDetailScreen detailScreen = FindEmbeddedChainlingDetailScreen(controller);
            if (detailScreen == null)
            {
                Debug.LogWarning(
                    "[PlayerMenuTabScreenInstaller] ChainlingDetailScreen was not found under ScreenStackHost. " +
                    "Embed Screen_ChainlingDetail on PlayerMenuRoot or assign detailScreen on ChainlingsPartyScreen.",
                    partyScreen);
            }

            partyScreen.WireViewReferences(detailScreen, views != null ? views.PartySlotViewPrefab : null);

            if (views != null && views.PartyColumnShellPrefab != null)
                partyScreen.WireColumnShellPrefab(views.PartyColumnShellPrefab);

            CanvasGroup partyGroup = partyScreen.CanvasGroup;
            if (partyGroup == null)
                partyGroup = partyScreen.GetComponent<CanvasGroup>();
            if (partyGroup == null)
                partyGroup = partyScreen.gameObject.AddComponent<CanvasGroup>();

            partyScreen.ConfigureScreenIdentity(ChainlingsTabId, "Chainlings", partyGroup);
        }

        private static void FinalizeInventoryScreen(InventoryScreen inventoryScreen)
        {
            CanvasGroup group = inventoryScreen.CanvasGroup;
            if (group == null)
                group = inventoryScreen.GetComponent<CanvasGroup>();
            if (group == null)
                group = inventoryScreen.gameObject.AddComponent<CanvasGroup>();

            inventoryScreen.SetScreenIdentity(InventoryTabId, "Inventory", group);
        }

        private static ChainlingDetailScreen FindEmbeddedChainlingDetailScreen(PlayerMenuController controller)
        {
            MenuScreenStack stack = controller != null ? controller.GetScreenStack() : null;
            if (stack == null)
                return null;

            ChainlingDetailScreen[] existing = stack.GetComponentsInChildren<ChainlingDetailScreen>(true);
            return existing.Length > 0 ? existing[0] : null;
        }

        private static void DestroyScreenObject(GameObject target)
        {
            if (target == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(target);
            else
#endif
                Object.Destroy(target);
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

        private static void LogMissingScreen(
            PlayerMenuController controller,
            MenuTabDefinitionSO definition,
            bool enableDebugLogs,
            string message)
        {
            if (!enableDebugLogs || controller == null || definition == null)
                return;

            Debug.LogWarning(
                $"[PlayerMenuTabScreenInstaller] Tab '{definition.TabId}': {message}",
                controller);
        }

        private static bool ScreenIdsMatch(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left) &&
                string.Equals(left, right, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
