using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MobileControls;
using Chainbound.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.Editor
{
    public static class PlayerMenuScaffoldCreator
    {
        private const string DataDir = "Assets/Data/UI";
        private const string PrefabDir = "Assets/Prefabs/UI/PlayerMenu";
        private const string PrefabPath = PrefabDir + "/PlayerMenuRoot.prefab";
        private const string SandboxPrefabPath = PrefabDir + "/PlayerMenuRoot_ChainlingsSandbox.prefab";
        private const string SupabaseConfigPath = "Assets/Data/Supabase/SupabaseConfig.asset";
        private const string PartyTabMotionProfilePath = "Assets/Data/UI/PartyTabMotionProfile.asset";
        private const string SpeciesDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Database/SpeciesDatabase.asset";
        private const string CombatVisualDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Visual/Database/CombatVisualDatabase.asset";
        private const string ChainlingVisualIdentityDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Visual/Cosmetics/ChainlingVisualIdentityDatabase.asset";
        private const string MoveDatabasePath = "Assets/Data/Combat/Moves/Database/MoveDatabase.asset";

        [MenuItem("Tools/Chainbound/UI/Create Player Menu Scaffold")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null &&
                !EditorUtility.DisplayDialog(
                    "Overwrite PlayerMenuRoot?",
                    "This replaces Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab and will erase any hand-authored shell layout. Continue?",
                    "Overwrite PlayerMenuRoot",
                    "Cancel"))
            {
                return;
            }

            CreateInternal(false);
        }

        [MenuItem("Tools/Chainbound/UI/Create Player Menu Scaffold (Chainlings Sandbox)")]
        public static void CreateWithChainlingsTestHarness()
        {
            if (!EditorUtility.DisplayDialog(
                    "Legacy Chainlings Sandbox Prefab",
                    "This creates a separate PlayerMenuRoot_ChainlingsSandbox prefab. " +
                    "Prefer PlayerMenuRoot + Tools/Chainbound/UI/Player Menu Dev/Use Harness Chainlings Data instead. Continue?",
                    "Create Legacy Sandbox",
                    "Cancel"))
            {
                return;
            }

            CreateInternal(true);
        }

        private static void CreateInternal(bool useChainlingsTestHarness)
        {
            EnsureFolder("Assets/Data", "UI");
            EnsureFolder("Assets/Prefabs/UI", "PlayerMenu");

            MenuTabDefinitionSO[] tabs =
            {
                EnsureTab("chainlings", "Chainlings", "View your active party and inspect Chainlings. Change members at a PC.", 0, new Color(0.2f, 0.85f, 0.75f, 1f)),
                EnsureTab("inventory", "Inventory", "Items, cosmetics, consumables, RAM, and wallet inventory.", 0, new Color(0.35f, 0.75f, 1f, 1f)),
                EnsureTab("profile", "Profile", "Player card, lifetime stats, and Trenches progress.", 0, new Color(1f, 0.75f, 0.3f, 1f)),
                EnsureTab("database", "Database", "Discovered Chainlings, species data, zones, and encounter notes.", 0, new Color(0.65f, 0.95f, 0.45f, 1f)),
                EnsureTab("handbook", "Handbook", "Battle mechanics, moves, and unlocked guide chapters.", 1, new Color(1f, 0.4f, 0.55f, 1f)),
            };

            EnsureRarityPalette();
            string prefabPath = useChainlingsTestHarness ? SandboxPrefabPath : PrefabPath;
            CreatePrefab(tabs, useChainlingsTestHarness, prefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(useChainlingsTestHarness
                ? $"[PlayerMenuScaffoldCreator] Created scaffold with Chainlings sandbox harness at {prefabPath}"
                : $"[PlayerMenuScaffoldCreator] Created scaffold at {prefabPath}");
        }

        private static void CreatePrefab(MenuTabDefinitionSO[] tabs, bool useChainlingsTestHarness, string prefabPath)
        {
            GameObject root = new GameObject(
                useChainlingsTestHarness ? "PlayerMenuRoot_ChainlingsSandbox" : "PlayerMenuRoot",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup));

            Stretch(root.GetComponent<RectTransform>());

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2670f, 1200f);
            scaler.matchWidthOrHeight = 0.5f;

            CanvasGroup rootCanvasGroup = root.GetComponent<CanvasGroup>();
            MenuInputContextController input = root.AddComponent<MenuInputContextController>();
            PlayerMenuController controller = root.AddComponent<PlayerMenuController>();
            root.AddComponent<GameplayMenuInputBridge>();

            GameObject backgroundRoot = CreateRect("PlayerMenuBackground", root.transform);
            PlayerMenuBackgroundController backgroundController = backgroundRoot.AddComponent<PlayerMenuBackgroundController>();
            backgroundController.BuildDefaultLayoutForEditor();

            GameObject safe = CreateRect("SafeAreaPanel", root.transform);
            Stretch(safe.GetComponent<RectTransform>());
            safe.AddComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>();

            GameObject container = CreateRect("MenuContainer", safe.transform);
            Stretch(container.GetComponent<RectTransform>(), 36f, 44f, 36f, 44f);
            container.AddComponent<CanvasGroup>();

            GameObject hubRoot = CreateRect("HubRoot", container.transform);
            Stretch(hubRoot.GetComponent<RectTransform>());

            Image leftPanel = CreateImage("LeftTabPanel", hubRoot.transform, new Color(0.12f, 0.16f, 0.2f, 0.98f));
            FixedTopLeft(leftPanel.rectTransform, 560f, 520f, 34f, 34f);

            TMP_Text menuTitle = CreateText("Title", leftPanel.transform, "PLAYER MENU", 32f, TextAlignmentOptions.MidlineLeft);
            Top(menuTitle.rectTransform, 58f, 26f, 26f, 18f);

            GameObject tabBarGo = CreateRect("TabBar", leftPanel.transform);
            Stretch(tabBarGo.GetComponent<RectTransform>(), 24f, 24f, 24f, 92f);

            VerticalLayoutGroup layout = tabBarGo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            MenuTabBar tabBar = tabBarGo.AddComponent<MenuTabBar>();

            Image rightPanel = CreateImage("RightInfoPanel", hubRoot.transform, new Color(0.11f, 0.15f, 0.18f, 0.98f));
            FixedTopRight(rightPanel.rectTransform, 760f, 420f, 34f, 76f);

            TMP_Text areaLabel = CreateText("AreaLabel", rightPanel.transform, "UNKNOWN AREA", 42f, TextAlignmentOptions.Top);
            Top(areaLabel.rectTransform, 92f, 34f, 34f, 46f);

            TMP_Text dateTimeLabel = CreateText("DateTimeLabel", rightPanel.transform, "00/00/00\n--:--", 34f, TextAlignmentOptions.Top);
            Top(dateTimeLabel.rectTransform, 128f, 34f, 34f, 146f);

            TMP_Text segmentLabel = CreateText("SegmentLabel", rightPanel.transform, "UNKNOWN", 28f, TextAlignmentOptions.Top);
            Top(segmentLabel.rectTransform, 52f, 34f, 34f, 284f);
            segmentLabel.color = new Color(0.7f, 0.82f, 0.9f, 1f);

            TMP_Text ramLabel = CreateText("RamLabel", rightPanel.transform, "RAM ----", 34f, TextAlignmentOptions.Bottom);
            Bottom(ramLabel.rectTransform, 62f, 34f, 34f, 30f);

            Image bottomPanel = CreateImage("BottomDescriptionPanel", hubRoot.transform, new Color(0.11f, 0.15f, 0.18f, 0.98f));
            FixedBottomCenter(bottomPanel.rectTransform, 1500f, 300f, 40f);

            TMP_Text tabTitleLabel = CreateText("TabTitleLabel", bottomPanel.transform, "Chainlings", 36f, TextAlignmentOptions.TopLeft);
            Top(tabTitleLabel.rectTransform, 58f, 34f, 34f, 30f);

            TMP_Text tabDescriptionLabel = CreateText("TabDescriptionLabel", bottomPanel.transform, tabs[0].Description, 30f, TextAlignmentOptions.TopLeft);
            Stretch(tabDescriptionLabel.rectTransform, 34f, 34f, 34f, 96f);
            tabDescriptionLabel.color = new Color(0.86f, 0.91f, 0.98f, 1f);

            Image centerPanel = CreateImage("CenterFillerPanel", hubRoot.transform, new Color(0.075f, 0.105f, 0.14f, 0.6f));
            Stretch(centerPanel.rectTransform, 650f, 390f, 850f, 108f);

            TMP_Text centerLabel = CreateText("CenterStatus", centerPanel.transform, "SIGNAL JAM\nMODULE SELECT", 32f, TextAlignmentOptions.Center);
            Stretch(centerLabel.rectTransform, 24f, 24f, 24f, 24f);
            centerLabel.color = new Color(0.62f, 0.75f, 0.84f, 1f);

            PlayerMenuOverviewPanel overview = hubRoot.AddComponent<PlayerMenuOverviewPanel>();
            ConfigureOverview(overview, SupabaseConfigPath, areaLabel, dateTimeLabel, segmentLabel, ramLabel, tabTitleLabel, tabDescriptionLabel);

            GameObject stackHost = CreateRect("ScreenStackHost", container.transform);
            Stretch(stackHost.GetComponent<RectTransform>(), 36f, 36f, 36f, 36f);
            MenuScreenStack stack = stackHost.AddComponent<MenuScreenStack>();

            MenuScreen[] screens = CreateScreens(stackHost.transform, useChainlingsTestHarness);
            TabWiring wiring = CreateTabButtons(tabBarGo.transform, tabs);

            ConfigureTabBar(tabBar, tabs, wiring);
            ConfigureStack(stack, stackHost.GetComponent<RectTransform>(), screens);
            ConfigureInputContext(input, wiring.buttons);
            ConfigureController(
                controller,
                root,
                rootCanvasGroup,
                hubRoot,
                stackHost,
                overview,
                backgroundController,
                stack,
                input,
                tabBar,
                tabs,
                screens,
                useChainlingsTestHarness);
            controller.EnsureRootBootSequenceForEditor();

            Button close = CreateButton("CloseButton", container.transform, new Color(0.18f, 0.23f, 0.31f, 1f));
            FixedTopRight(close.GetComponent<RectTransform>(), 76f, 76f, 24f, 24f);

            TMP_Text closeLabel = CreateText("Label", close.transform, "X", 34f, TextAlignmentOptions.Center);
            Stretch(closeLabel.rectTransform);
            UnityEventTools.AddPersistentListener(close.onClick, controller.Close);

            TMP_Text footer = CreateText("FooterHints", container.transform, "UP/DOWN Tabs   A Open   B Back", 24f, TextAlignmentOptions.BottomLeft);
            Bottom(footer.rectTransform, 42f, 38f, 120f, 2f);
            footer.color = new Color(0.68f, 0.76f, 0.86f, 1f);

            stackHost.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
        }

        private static MenuScreen[] CreateScreens(Transform parent, bool useChainlingsTestHarness)
        {
            string[] ids = { "chainlings", "inventory", "profile", "database", "handbook" };
            string[] labels = { "Chainlings", "Inventory", "Profile / Stats", "Chainling Database", "Combat Handbook" };
            string[] bodies =
            {
                "Active party, focused preview, and Chainling inspection details plug in here.",
                "NFT cosmetics, consumables, RAM, and wallet-aware inventory states plug in here.",
                "Shareable profile card, lifetime stats, and The Trenches leaderboard plug in here.",
                "Seen/caught discovery, species details, zones, and encounter odds plug in here.",
                "Moves inventory and mechanics guide chapters unlock after Professor Toby's Big Brain Book.",
            };

            var screens = new MenuScreen[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                GameObject screenRoot = CreateRect("Tab_" + labels[i].Replace(" / ", "_").Replace(" ", ""), parent);
                Stretch(screenRoot.GetComponent<RectTransform>());
                CanvasGroup canvasGroup = screenRoot.AddComponent<CanvasGroup>();

                if (i == 0)
                {
                    MenuScreen partyScreen = AddMenuScreenComponent(
                        screenRoot,
                        "SignalJam.UI.MenuNavigation.ChainlingsPartyScreen");
                    if (partyScreen is ChainlingsPartyScreen chainlingsPartyScreen)
                    {
                        chainlingsPartyScreen.SetDataSourceMode(
                            useChainlingsTestHarness
                                ? ChainlingsDataSourceMode.Harness
                                : ChainlingsDataSourceMode.Production);
                    }

                    ChainlingsMenuTestHarness testHarness = null;
                    if (useChainlingsTestHarness)
                    {
                        testHarness = screenRoot.AddComponent<ChainlingsMenuTestHarness>();
                        SerializedObject harnessSo = new SerializedObject(testHarness);
                        WireChainlingDataReferences(harnessSo);
                        harnessSo.ApplyModifiedPropertiesWithoutUndo();
                    }

                    Image detailImage = CreateImage("Screen_ChainlingDetail", parent, new Color(0.07f, 0.095f, 0.14f, 0.92f));
                    Stretch(detailImage.rectTransform);
                    CanvasGroup detailCanvasGroup = detailImage.gameObject.AddComponent<CanvasGroup>();
                    MenuScreen detailScreen = AddMenuScreenComponent(
                        detailImage.gameObject,
                        "SignalJam.UI.MenuNavigation.ChainlingDetailScreen");
                    if (detailScreen is ChainlingDetailScreen chainlingDetailScreen)
                    {
                        chainlingDetailScreen.SetDataSourceMode(
                            useChainlingsTestHarness
                                ? ChainlingsDataSourceMode.Harness
                                : ChainlingsDataSourceMode.Production);
                    }

                    SerializedObject detailSo = new SerializedObject(detailScreen);
                    SetStringIfPresent(detailSo, "screenId", "chainling_detail");
                    SetStringIfPresent(detailSo, "displayName", "Chainling Detail");
                    SetIfPresent(detailSo, "canvasGroup", detailCanvasGroup);
                    WireChainlingDataReferences(detailSo);
                    if (testHarness != null)
                        WireChainlingServiceReferences(detailSo, testHarness);
                    detailSo.ApplyModifiedPropertiesWithoutUndo();

                    SerializedObject partySo = new SerializedObject(partyScreen);
                    SetStringIfPresent(partySo, "screenId", ids[i]);
                    SetStringIfPresent(partySo, "displayName", labels[i]);
                    SetIfPresent(partySo, "canvasGroup", canvasGroup);
                    SetIfPresent(partySo, "screenStack", parent.GetComponent<MenuScreenStack>());
                    SetIfPresent(partySo, "detailScreen", detailScreen);
                    WireChainlingDataReferences(partySo);
                    if (testHarness != null)
                        WireChainlingServiceReferences(partySo, testHarness);
                    partySo.ApplyModifiedPropertiesWithoutUndo();

                    detailImage.gameObject.SetActive(false);
                    screens[i] = partyScreen;
                    continue;
                }

                if (i == 1)
                {
                    MenuScreen inventoryMenuScreen = AddMenuScreenComponent(
                        screenRoot,
                        "SignalJam.UI.MenuNavigation.InventoryScreen");
                    SerializedObject inventorySo = new SerializedObject(inventoryMenuScreen);
                    SetStringIfPresent(inventorySo, "screenId", ids[i]);
                    SetStringIfPresent(inventorySo, "displayName", labels[i]);
                    SetIfPresent(inventorySo, "canvasGroup", canvasGroup);
                    SetIfPresent(inventorySo, "supabaseConfig", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SupabaseConfigPath));
                    SetIfPresent(inventorySo, "motionProfile", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PartyTabMotionProfilePath));
                    inventorySo.ApplyModifiedPropertiesWithoutUndo();

                    if (inventoryMenuScreen is InventoryScreen inventoryScreen)
                        inventoryScreen.BuildDefaultLayoutForEditor();

                    screens[i] = inventoryMenuScreen;
                    continue;
                }

                PlaceholderMenuScreen screen = screenRoot.AddComponent<PlaceholderMenuScreen>();

                TMP_Text heading = CreateText("Heading", screenRoot.transform, labels[i], 38f, TextAlignmentOptions.TopLeft);
                Top(heading.rectTransform, 64f, 30f, 30f, 28f);

                TMP_Text body = CreateText("Body", screenRoot.transform, bodies[i], 28f, TextAlignmentOptions.TopLeft);
                Stretch(body.rectTransform, 30f, 92f, 30f, 110f);

                string statusText = i == 4
                    ? "Locked until handbook_unlocked is true."
                    : "Ready for prefab-specific content.";
                TMP_Text status = CreateText("Status", screenRoot.transform, statusText, 23f, TextAlignmentOptions.BottomLeft);
                Bottom(status.rectTransform, 54f, 30f, 30f, 28f);
                status.color = new Color(0.68f, 0.78f, 0.9f, 1f);

                SerializedObject so = new SerializedObject(screen);
                so.FindProperty("screenId").stringValue = ids[i];
                so.FindProperty("displayName").stringValue = labels[i];
                so.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
                so.FindProperty("headingLabel").objectReferenceValue = heading;
                so.FindProperty("bodyLabel").objectReferenceValue = body;
                so.FindProperty("statusLabel").objectReferenceValue = status;
                so.FindProperty("heading").stringValue = labels[i];
                so.FindProperty("body").stringValue = bodies[i];
                so.FindProperty("status").stringValue = statusText;
                so.ApplyModifiedPropertiesWithoutUndo();

                screens[i] = screen;
            }

            return screens;
        }

        private static void WireChainlingDataReferences(SerializedObject so)
        {
            SetIfPresent(so, "supabaseConfig", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SupabaseConfigPath));
            SetIfPresent(so, "speciesDatabase", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SpeciesDatabasePath));
            SetIfPresent(so, "visualDatabase", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CombatVisualDatabasePath));
            SetIfPresent(so, "identityDatabase", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ChainlingVisualIdentityDatabasePath));
            SetIfPresent(so, "moveDatabase", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MoveDatabasePath));
        }

        private static void WireChainlingServiceReferences(SerializedObject so, UnityEngine.Object serviceProvider)
        {
            SetIfPresent(so, "rosterProviderBehaviour", serviceProvider);
            SetIfPresent(so, "partyProviderBehaviour", serviceProvider);
            SetIfPresent(so, "partyLineupServiceBehaviour", serviceProvider);
            SetIfPresent(so, "cosmeticInventoryServiceBehaviour", serviceProvider);
            SetIfPresent(so, "moveInventoryProviderBehaviour", serviceProvider);
            SetIfPresent(so, "moveEquipServiceBehaviour", serviceProvider);
            SetIfPresent(so, "visualIdentityServiceBehaviour", serviceProvider);
            SetIfPresent(so, "rebirthServiceBehaviour", serviceProvider);
        }

        private static void SetIfPresent(SerializedObject so, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static void SetStringIfPresent(SerializedObject so, string propertyName, string value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value ?? "";
        }

        private static MenuScreen AddMenuScreenComponent(GameObject target, string fullTypeName)
        {
            System.Type type = ResolveType(fullTypeName);
            if (type == null || !typeof(MenuScreen).IsAssignableFrom(type))
            {
                Debug.LogWarning($"[PlayerMenuScaffoldCreator] Could not resolve {fullTypeName}; using PlaceholderMenuScreen.");
                return target.AddComponent<PlaceholderMenuScreen>();
            }

            return (MenuScreen)target.AddComponent(type);
        }

        private static System.Type ResolveType(string fullTypeName)
        {
            System.Type type = System.Type.GetType(fullTypeName);
            if (type != null)
                return type;

            foreach (System.Type candidate in TypeCache.GetTypesDerivedFrom<MenuScreen>())
            {
                if (candidate.FullName == fullTypeName)
                    return candidate;
            }

            string typeName = fullTypeName;
            int lastDot = fullTypeName.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < fullTypeName.Length - 1)
                typeName = fullTypeName.Substring(lastDot + 1);

            foreach (System.Type candidate in TypeCache.GetTypesDerivedFrom<MenuScreen>())
            {
                if (candidate.Name == typeName)
                    return candidate;
            }

            var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(fullTypeName);
                if (type != null)
                    return type;
            }

            return null;
        }

        private static TabWiring CreateTabButtons(Transform parent, MenuTabDefinitionSO[] tabs)
        {
            string[] labels = { "Chainlings", "Inventory", "Profile", "Database", "Handbook" };
            var wiring = new TabWiring(labels.Length);

            for (int i = 0; i < labels.Length; i++)
            {
                Button button = CreateButton("TabButton_" + labels[i], parent, new Color(0.105f, 0.135f, 0.19f, 1f));
                wiring.buttons[i] = button;
                LayoutElement(button.gameObject, preferredHeight: 70f, flexibleWidth: 1f);

                Image frame = CreateImage("SelectedFrame", button.transform, tabs[i].AccentColor);
                Stretch(frame.rectTransform);
                frame.enabled = false;
                wiring.selectionFrames[i] = frame;

                TMP_Text label = CreateText("Label", button.transform, labels[i], 24f, TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 8f, 8f, 8f, 8f);
                wiring.labels[i] = label;

                Image badge = CreateImage("LockBadge", button.transform, new Color(0.13f, 0.04f, 0.06f, 0.92f));
                RectTransform badgeRt = badge.rectTransform;
                badgeRt.anchorMin = new Vector2(0.5f, 1f);
                badgeRt.anchorMax = new Vector2(0.5f, 1f);
                badgeRt.pivot = new Vector2(0.5f, 1f);
                badgeRt.sizeDelta = new Vector2(156f, 32f);
                badgeRt.anchoredPosition = new Vector2(0f, -6f);
                badge.gameObject.SetActive(false);
                wiring.lockBadges[i] = badge.gameObject;

                TMP_Text badgeLabel = CreateText("Label", badge.transform, "Book", 17f, TextAlignmentOptions.Center);
                Stretch(badgeLabel.rectTransform);
                wiring.lockLabels[i] = badgeLabel;
            }

            return wiring;
        }

        private static void ConfigureTabBar(MenuTabBar tabBar, MenuTabDefinitionSO[] tabs, TabWiring wiring)
        {
            SerializedObject so = new SerializedObject(tabBar);
            SerializedProperty tabsProp = so.FindProperty("tabs");
            tabsProp.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                tabsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];

            SerializedProperty bindingsProp = so.FindProperty("bindings");
            bindingsProp.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                SerializedProperty elem = bindingsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("button").objectReferenceValue = wiring.buttons[i];
                elem.FindPropertyRelative("label").objectReferenceValue = wiring.labels[i];
                elem.FindPropertyRelative("selectionFrame").objectReferenceValue = wiring.selectionFrames[i];
                elem.FindPropertyRelative("lockedBadge").objectReferenceValue = wiring.lockBadges[i];
                elem.FindPropertyRelative("lockedLabel").objectReferenceValue = wiring.lockLabels[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureOverview(
            PlayerMenuOverviewPanel overview,
            string supabaseConfigPath,
            TMP_Text areaLabel,
            TMP_Text dateTimeLabel,
            TMP_Text segmentLabel,
            TMP_Text ramLabel,
            TMP_Text tabTitleLabel,
            TMP_Text tabDescriptionLabel)
        {
            SerializedObject so = new SerializedObject(overview);
            SetIfPresent(so, "supabaseConfig", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(supabaseConfigPath));
            SetIfPresent(so, "areaLabel", areaLabel);
            SetIfPresent(so, "dateTimeLabel", dateTimeLabel);
            SetIfPresent(so, "segmentLabel", segmentLabel);
            SetIfPresent(so, "ramLabel", ramLabel);
            SetIfPresent(so, "tabTitleLabel", tabTitleLabel);
            SetIfPresent(so, "tabDescriptionLabel", tabDescriptionLabel);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureInputContext(MenuInputContextController input, Button[] tabButtons)
        {
            SerializedObject so = new SerializedObject(input);
            if (tabButtons != null && tabButtons.Length > 0)
            {
                SetIfPresent(so, "defaultSelection", tabButtons[0]);
                SetIfPresent(so, "fallbackSelection", tabButtons[0]);

                SerializedProperty navOrder = so.FindProperty("navigationOrder");
                if (navOrder != null)
                {
                    navOrder.arraySize = tabButtons.Length;
                    for (int i = 0; i < tabButtons.Length; i++)
                        navOrder.GetArrayElementAtIndex(i).objectReferenceValue = tabButtons[i];
                }
            }

            SerializedProperty wrapProp = so.FindProperty("wrapNavigation");
            if (wrapProp != null)
                wrapProp.boolValue = false;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureStack(MenuScreenStack stack, RectTransform host, MenuScreen[] screens)
        {
            SerializedObject so = new SerializedObject(stack);
            so.FindProperty("screenHost").objectReferenceValue = host;
            SerializedProperty initialScreens = so.FindProperty("initialScreens");
            initialScreens.arraySize = screens.Length;
            for (int i = 0; i < screens.Length; i++)
                initialScreens.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureController(
            PlayerMenuController controller,
            GameObject root,
            CanvasGroup rootCanvasGroup,
            GameObject hubRoot,
            GameObject contentRoot,
            PlayerMenuOverviewPanel overviewPanel,
            PlayerMenuBackgroundController backgroundController,
            MenuScreenStack stack,
            MenuInputContextController input,
            MenuTabBar tabBar,
            MenuTabDefinitionSO[] tabs,
            MenuScreen[] screens,
            bool bypassAvailabilityGate)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("menuRoot").objectReferenceValue = root;
            so.FindProperty("rootCanvasGroup").objectReferenceValue = rootCanvasGroup;
            SetIfPresent(so, "hubRoot", hubRoot);
            SetIfPresent(so, "contentRoot", contentRoot);
            so.FindProperty("screenStack").objectReferenceValue = stack;
            so.FindProperty("inputContext").objectReferenceValue = input;
            SetIfPresent(so, "overviewPanel", overviewPanel);
            SetIfPresent(so, "backgroundController", backgroundController);
            so.FindProperty("tabBar").objectReferenceValue = tabBar;

            SerializedProperty defsProp = so.FindProperty("tabDefinitions");
            defsProp.arraySize = tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
                defsProp.GetArrayElementAtIndex(i).objectReferenceValue = tabs[i];

            SerializedProperty screensProp = so.FindProperty("tabScreens");
            screensProp.arraySize = screens.Length;
            for (int i = 0; i < screens.Length; i++)
                screensProp.GetArrayElementAtIndex(i).objectReferenceValue = screens[i];

            so.FindProperty("defaultTabIndex").intValue = 0;
            SerializedProperty bypassProp = so.FindProperty("bypassAvailabilityGate");
            if (bypassProp != null)
                bypassProp.boolValue = bypassAvailabilityGate;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static MenuTabDefinitionSO EnsureTab(string id, string label, string description, int lockRule, Color accent)
        {
            string path = DataDir + "/PlayerMenuTab_" + id + ".asset";
            MenuTabDefinitionSO asset = AssetDatabase.LoadAssetAtPath<MenuTabDefinitionSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            SerializedObject so = new SerializedObject(asset);
            so.FindProperty("tabId").stringValue = id;
            so.FindProperty("displayName").stringValue = label;
            SerializedProperty descriptionProp = so.FindProperty("description");
            if (descriptionProp != null)
                descriptionProp.stringValue = description ?? "";
            so.FindProperty("lockRule").enumValueIndex = lockRule;
            so.FindProperty("lockedLabel").stringValue = lockRule == 1 ? "Big Brain Book" : "Locked";
            so.FindProperty("accentColor").colorValue = accent;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void EnsureRarityPalette()
        {
            string path = DataDir + "/RarityPalette_PlayerMenu.asset";
            if (AssetDatabase.LoadAssetAtPath<RarityPaletteSO>(path) != null)
                return;

            RarityPaletteSO asset = ScriptableObject.CreateInstance<RarityPaletteSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TMP_Text tmp = go.GetComponent<TMP_Text>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = new Color(0.92f, 0.96f, 1f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            return button;
        }

        private static LayoutElement LayoutElement(
            GameObject target,
            float preferredWidth = -1f,
            float preferredHeight = -1f,
            float flexibleWidth = -1f,
            float flexibleHeight = -1f)
        {
            LayoutElement element = target.GetComponent<LayoutElement>();
            if (element == null)
                element = target.AddComponent<LayoutElement>();

            if (preferredWidth >= 0f)
                element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f)
                element.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0f)
                element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f)
                element.flexibleHeight = flexibleHeight;

            return element;
        }

        private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        private static void Top(RectTransform rt, float height, float left = 0f, float right = 0f, float y = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -height - y);
            rt.offsetMax = new Vector2(-right, -y);
        }

        private static void Bottom(RectTransform rt, float height, float left = 0f, float right = 0f, float y = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, y);
            rt.offsetMax = new Vector2(-right, y + height);
        }

        private static void FixedTopLeft(RectTransform rt, float width, float height, float x, float y)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private static void FixedTopRight(RectTransform rt, float width, float height, float x, float y)
        {
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(-x, -y);
        }

        private static void FixedBottomCenter(RectTransform rt, float width, float height, float y)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, y);
        }

        private sealed class TabWiring
        {
            public readonly Button[] buttons;
            public readonly TMP_Text[] labels;
            public readonly Image[] selectionFrames;
            public readonly GameObject[] lockBadges;
            public readonly TMP_Text[] lockLabels;

            public TabWiring(int count)
            {
                buttons = new Button[count];
                labels = new TMP_Text[count];
                selectionFrames = new Image[count];
                lockBadges = new GameObject[count];
                lockLabels = new TMP_Text[count];
            }
        }
    }
}
