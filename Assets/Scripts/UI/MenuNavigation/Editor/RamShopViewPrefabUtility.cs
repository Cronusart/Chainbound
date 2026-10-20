#if UNITY_EDITOR
using System;
using System.Linq;
using Chainbound.Combat;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class RamShopViewPrefabUtility
    {
        private const string ViewPath = "Assets/Prefabs/UI/PlayerMenu/RamShopView.prefab";
        private const string RootPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        private const string TabPath = "Assets/Data/UI/PlayerMenuTab_ram_shop.asset";
        private const string SupabasePath = "Assets/Data/Supabase/SupabaseConfig.asset";
        private const string MotionPath = "Assets/Scripts/UI/UiMotionProfile.asset";
        private const string BlankPanelPath = "Assets/Sprites/UI/PlayerMenu/9sliceborder_blank.png";
        private const string ScanlinePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png";
        private const string GrayButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png";
        private const string OrangeButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png";

        private static readonly Color ShopInk = new Color(0.035f, 0.025f, 0.065f, 0.985f);
        private static readonly Color PanelInk = new Color(0.075f, 0.055f, 0.12f, 0.98f);
        private static readonly Color PanelRaised = new Color(0.11f, 0.085f, 0.16f, 0.98f);
        private static readonly Color HotOrange = new Color(1f, 0.31f, 0.08f, 1f);
        private static readonly Color ElectricMint = new Color(0.26f, 0.94f, 0.77f, 1f);
        private static readonly Color WarmGold = new Color(1f, 0.82f, 0.28f, 1f);

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure RAM Shop View", priority = 63)]
        public static void EnsureRamShopView()
        {
            RamShopScreen screen = CreateOrReplaceView();
            MenuTabDefinitionSO tab = EnsureTab(screen);
            WireRoot(tab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[RamShopViewPrefabUtility] Authored RAM Shop prefab, sixth tab, and embedded PlayerMenuRoot screen.");
        }

        private static RamShopScreen CreateOrReplaceView()
        {
            var root = new GameObject("RamShopView", typeof(RectTransform), typeof(CanvasGroup), typeof(RamShopScreen));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                PlayerMenuUiFactory.Stretch(rootRect);
                CanvasGroup group = root.GetComponent<CanvasGroup>();
                RamShopScreen screen = root.GetComponent<RamShopScreen>();
                screen.SetScreenIdentity(RamShopScreen.RamShopScreenId, "RAM Shop", group);

                RectTransform primary = PlayerMenuUiFactory.CreateRect("PrimaryCenter", root.transform);
                primary.anchorMin = new Vector2(0.206f, 0.014f);
                primary.anchorMax = new Vector2(0.794f, 0.951f);
                primary.offsetMin = Vector2.zero;
                primary.offsetMax = Vector2.zero;

                Image panel = SlicedImage("ShopPanel", primary, BlankPanelPath, ShopInk, false);
                PlayerMenuUiFactory.Stretch(panel.rectTransform);

                Image scanlines = PlayerMenuUiFactory.CreateImage(
                    "ScanlineOverlay", primary, new Color(1f, 0.35f, 0.2f, 0.08f));
                scanlines.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ScanlinePath);
                scanlines.type = Image.Type.Tiled;
                scanlines.pixelsPerUnitMultiplier = 0.75f;
                scanlines.raycastTarget = false;
                PlayerMenuUiFactory.Stretch(scanlines.rectTransform, 10f, 10f, 10f, 10f);

                Image topSignal = PlayerMenuUiFactory.CreateImage("TopSignal", primary, HotOrange);
                SetAnchors(topSignal.rectTransform, new Vector2(0.018f, 0.972f), new Vector2(0.982f, 0.985f));

                RectTransform header = Panel("RamHeader", primary, new Vector2(0.02f, 0.835f), new Vector2(0.98f, 0.965f));
                TMP_Text kicker = PlayerMenuUiFactory.CreateText(
                    "Kicker", header, "SYSTEM MEMORY EXCHANGE // VERIFIED QUOTES", 15f,
                    TextAlignmentOptions.MidlineLeft, false);
                SetAnchors(kicker.rectTransform, new Vector2(0.035f, 0.61f), new Vector2(0.68f, 0.92f));
                kicker.color = ElectricMint;
                kicker.fontStyle = FontStyles.Bold;
                TMP_Text title = PlayerMenuUiFactory.CreateText(
                    "Title", header, "RAM MARKET", 35f, TextAlignmentOptions.MidlineLeft, false);
                SetAnchors(title.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.58f, 0.63f));
                title.fontStyle = FontStyles.Bold;

                RectTransform balanceWell = Panel(
                    "BalanceWell", header, new Vector2(0.70f, 0.13f), new Vector2(0.97f, 0.87f));
                TMP_Text balanceCaption = PlayerMenuUiFactory.CreateText(
                    "BalanceCaption", balanceWell, "AVAILABLE MEMORY", 13f,
                    TextAlignmentOptions.Midline, false);
                SetAnchors(balanceCaption.rectTransform, new Vector2(0.05f, 0.57f), new Vector2(0.95f, 0.9f));
                balanceCaption.color = ElectricMint;
                balanceCaption.fontStyle = FontStyles.Bold;
                TMP_Text balance = PlayerMenuUiFactory.CreateText(
                    "RamBalanceLabel", balanceWell, "RAM --", 27f,
                    TextAlignmentOptions.Midline, false);
                SetAnchors(balance.rectTransform, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.6f));
                balance.fontStyle = FontStyles.Bold;

                RectTransform tenderRail = Panel(
                    "TenderRail", primary, new Vector2(0.02f, 0.715f), new Vector2(0.98f, 0.82f));
                TMP_Text payWith = PlayerMenuUiFactory.CreateText(
                    "PayWithLabel", tenderRail, "PAY WITH", 18f,
                    TextAlignmentOptions.MidlineLeft, false);
                SetAnchors(payWith.rectTransform, new Vector2(0.025f, 0.08f), new Vector2(0.18f, 0.92f));
                payWith.color = PlayerMenuUiFactory.Muted;
                payWith.fontStyle = FontStyles.Bold;
                Button sol = ShopButton("Tender_SOL", tenderRail, "SOL");
                SetAnchors(sol.transform as RectTransform, new Vector2(0.19f, 0.12f), new Vector2(0.445f, 0.88f));
                AddTenderSelectedMark(sol);
                Button usdc = ShopButton("Tender_USDC", tenderRail, "USDC");
                SetAnchors(usdc.transform as RectTransform, new Vector2(0.46f, 0.12f), new Vector2(0.715f, 0.88f));
                AddTenderSelectedMark(usdc);
                Button skr = ShopButton("Tender_SKR", tenderRail, "SKR");
                SetAnchors(skr.transform as RectTransform, new Vector2(0.73f, 0.12f), new Vector2(0.975f, 0.88f));
                AddTenderSelectedMark(skr);

                RectTransform bundleRow = PlayerMenuUiFactory.CreateRect("BundleRow", primary);
                SetAnchors(bundleRow, new Vector2(0.035f, 0.235f), new Vector2(0.965f, 0.70f));
                HorizontalLayoutGroup bundleLayout = PlayerMenuUiFactory.HorizontalLayout(bundleRow.gameObject, 16f);
                bundleLayout.padding = new RectOffset(6, 6, 6, 6);
                bundleLayout.childAlignment = TextAnchor.MiddleCenter;
                bundleLayout.childControlWidth = true;
                bundleLayout.childControlHeight = true;
                bundleLayout.childForceExpandWidth = true;
                bundleLayout.childForceExpandHeight = true;
                // $5 is the 20 RAM/$1 rate (100 RAM). $20 grants 500 RAM (20% off). $100 grants 3000 RAM (33% off).
                CreateBundleCard(
                    bundleRow, "Bundle_5", "5Bundle", "5bundle", 100,
                    "QUICK CACHE", "BASE PACK", null, ElectricMint, false, 0f);
                CreateBundleCard(
                    bundleRow, "Bundle_20", "20Bundle", "20bundle", 500,
                    "POWER PACK", "POPULAR PICK", "20% OFF", WarmGold, false, 0.25f);
                CreateBundleCard(
                    bundleRow, "Bundle_100", "100Bundle", "100bundle", 3000,
                    "MEGA STACK", "BEST VALUE", "33% OFF", HotOrange, true, 0.5f);

                RectTransform infoStrip = Panel(
                    "InfoStrip", primary, new Vector2(0.02f, 0.11f), new Vector2(0.98f, 0.22f));
                TMP_Text unavailable = PlayerMenuUiFactory.CreateText(
                    "UnavailableLabel", infoStrip, "RAM bundles are not available yet.", 18f,
                    TextAlignmentOptions.Center, true);
                SetAnchors(unavailable.rectTransform, new Vector2(0.035f, 0.49f), new Vector2(0.965f, 0.92f));
                unavailable.color = WarmGold;
                unavailable.fontStyle = FontStyles.Bold;
                TMP_Text status = PlayerMenuUiFactory.CreateText(
                    "StatusLabel", infoStrip, "Authoritative catalog loading...", 17f,
                    TextAlignmentOptions.Center, true);
                SetAnchors(status.rectTransform, new Vector2(0.035f, 0.08f), new Vector2(0.965f, 0.51f));
                status.color = PlayerMenuUiFactory.Muted;

                RectTransform footer = PlayerMenuUiFactory.CreateRect("Footer", primary);
                SetAnchors(footer, new Vector2(0.02f, 0.022f), new Vector2(0.98f, 0.095f));
                TMP_Text footerHint = PlayerMenuUiFactory.CreateText(
                    "FooterHint", footer, "SELECT A BUNDLE  //  LIVE QUOTE BEFORE SIGNING", 14f,
                    TextAlignmentOptions.MidlineLeft, false);
                SetAnchors(footerHint.rectTransform, new Vector2(0.015f, 0.02f), new Vector2(0.52f, 0.98f));
                footerHint.color = PlayerMenuUiFactory.Muted;
                Button retry = ShopButton("RetryButton", footer, "RETRY");
                SetAnchors(retry.transform as RectTransform, new Vector2(0.54f, 0.03f), new Vector2(0.75f, 0.97f));
                Button close = ShopButton("CloseButton", footer, "CLOSE  B");
                SetAnchors(close.transform as RectTransform, new Vector2(0.765f, 0.03f), new Vector2(0.985f, 0.97f));

                RectTransform sheet = Panel(
                    "ConfirmSheet", primary, new Vector2(0.055f, 0.065f), new Vector2(0.945f, 0.935f));
                sheet.gameObject.AddComponent<CanvasGroup>();
                Image sheetScanlines = PlayerMenuUiFactory.CreateImage(
                    "ScanlineOverlay", sheet, new Color(1f, 0.31f, 0.08f, 0.08f));
                sheetScanlines.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ScanlinePath);
                sheetScanlines.type = Image.Type.Tiled;
                sheetScanlines.raycastTarget = false;
                PlayerMenuUiFactory.Stretch(sheetScanlines.rectTransform, 6f, 6f, 6f, 6f);
                TMP_Text confirmKicker = PlayerMenuUiFactory.CreateText(
                    "ConfirmKicker", sheet, "FINAL CHECK // WALLET SIGNATURE REQUIRED", 15f,
                    TextAlignmentOptions.Center, false);
                SetAnchors(confirmKicker.rectTransform, new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.95f));
                confirmKicker.color = ElectricMint;
                TMP_Text confirmTitle = PlayerMenuUiFactory.CreateText(
                    "ConfirmTitle", sheet, "CONFIRM RAM PURCHASE", 31f,
                    TextAlignmentOptions.Center, false);
                SetAnchors(confirmTitle.rectTransform, new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.89f));
                confirmTitle.fontStyle = FontStyles.Bold;
                RectTransform summaryWell = Panel(
                    "QuoteSummaryWell", sheet, new Vector2(0.07f, 0.22f), new Vector2(0.93f, 0.77f));
                TMP_Text summary = PlayerMenuUiFactory.CreateText(
                    "ConfirmSummaryLabel", summaryWell, "", 19f,
                    TextAlignmentOptions.TopLeft, true);
                SetAnchors(summary.rectTransform, new Vector2(0.055f, 0.06f), new Vector2(0.945f, 0.94f));
                Button confirm = ShopButton("ConfirmButton", sheet, "CONFIRM  A");
                SetAnchors(confirm.transform as RectTransform, new Vector2(0.07f, 0.075f), new Vector2(0.58f, 0.18f));
                Button cancel = ShopButton("CancelConfirmButton", sheet, "BACK  B");
                SetAnchors(cancel.transform as RectTransform, new Vector2(0.61f, 0.075f), new Vector2(0.93f, 0.18f));
                AddPendingIndicator(confirm);
                sheet.gameObject.SetActive(false);

                SerializedObject serialized = new SerializedObject(screen);
                Set(serialized, "canvasGroup", group);
                Set(serialized, "defaultSelection", sol);
                Set(serialized, "supabaseConfig", AssetDatabase.LoadAssetAtPath<SupabaseConfigSO>(SupabasePath));
                Set(serialized, "balanceLabel", balance);
                Set(serialized, "statusLabel", status);
                Set(serialized, "unavailableLabel", unavailable);
                Set(serialized, "solButton", sol);
                Set(serialized, "usdcButton", usdc);
                Set(serialized, "skrButton", skr);
                Set(serialized, "retryButton", retry);
                Set(serialized, "closeButton", close);
                Set(serialized, "confirmSheet", sheet.gameObject);
                Set(serialized, "confirmSummaryLabel", summary);
                Set(serialized, "confirmButton", confirm);
                Set(serialized, "cancelConfirmButton", cancel);
                MonoBehaviour pending = confirm.GetComponents<MonoBehaviour>().FirstOrDefault(component => component.GetType().Name == "UiPendingIndicator");
                Set(serialized, "pendingIndicator", pending);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                ApplyExplicitConfirmNavigation(confirm, cancel);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ViewPath);
                return saved.GetComponent<RamShopScreen>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static MenuTabDefinitionSO EnsureTab(RamShopScreen screen)
        {
            MenuTabDefinitionSO tab = AssetDatabase.LoadAssetAtPath<MenuTabDefinitionSO>(TabPath);
            if (tab == null)
            {
                tab = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
                AssetDatabase.CreateAsset(tab, TabPath);
            }
            SerializedObject serialized = new SerializedObject(tab);
            serialized.FindProperty("tabId").stringValue = RamShopScreen.RamShopScreenId;
            serialized.FindProperty("displayName").stringValue = "RAM Shop";
            serialized.FindProperty("description").stringValue = "Verified RAM bundles paid through your connected Solana wallet.";
            serialized.FindProperty("accentColor").colorValue = PlayerMenuUiFactory.Accent;
            serialized.FindProperty("lockRule").enumValueIndex = (int)PlayerMenuTabLock.None;
            serialized.FindProperty("tabScreenPrefab").objectReferenceValue = screen;
            serialized.FindProperty("screenResolveMode").enumValueIndex = (int)PlayerMenuTabScreenResolveMode.EmbeddedOnly;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tab);
            return tab;
        }

        private static void WireRoot(MenuTabDefinitionSO tab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RootPath);
            try
            {
                ReplaceEmbeddedRamShop(root);
                PlayerMenuController controller = root.GetComponent<PlayerMenuController>();
                SerializedObject serialized = new SerializedObject(controller);
                SerializedProperty definitions = serialized.FindProperty("tabDefinitions");
                SerializedProperty screens = serialized.FindProperty("tabScreens");
                int index = FindAsset(definitions, tab);
                if (index < 0)
                {
                    index = definitions.arraySize;
                    definitions.arraySize++;
                    definitions.GetArrayElementAtIndex(index).objectReferenceValue = tab;
                }
                if (screens.arraySize < definitions.arraySize) screens.arraySize = definitions.arraySize;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                EnsureTabBinding(controller, definitions.arraySize);
                PlayerMenuRootTabScreenSyncEditor.SyncController(controller, false);
                AssignRootSfx(root);
                PrefabUtility.SaveAsPrefabAsset(root, RootPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ReplaceEmbeddedRamShop(GameObject root)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPath);
            if (root == null || source == null)
                return;

            RamShopScreen[] existingScreens = root.GetComponentsInChildren<RamShopScreen>(true);
            Transform parent = existingScreens.Length > 0
                ? existingScreens[0].transform.parent
                : root.transform;
            int siblingIndex = existingScreens.Length > 0
                ? existingScreens[0].transform.GetSiblingIndex()
                : parent.childCount;
            bool active = existingScreens.Length > 0 && existingScreens[0].gameObject.activeSelf;

            foreach (RamShopScreen existing in existingScreens)
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

            GameObject instance = PrefabUtility.InstantiatePrefab(source, parent) as GameObject;
            if (instance == null)
                return;

            instance.name = source.name;
            instance.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
            instance.SetActive(active);
        }

        private static void EnsureTabBinding(PlayerMenuController controller, int requiredCount)
        {
            MenuTabBar tabBar = controller.GetComponentInChildren<MenuTabBar>(true);
            SerializedObject serialized = new SerializedObject(tabBar);
            SerializedProperty bindings = serialized.FindProperty("bindings");
            while (bindings.arraySize < requiredCount)
            {
                int sourceIndex = bindings.arraySize - 1;
                SerializedProperty source = bindings.GetArrayElementAtIndex(sourceIndex);
                Button sourceButton = source.FindPropertyRelative("button").objectReferenceValue as Button;
                GameObject clone = Object.Instantiate(sourceButton.gameObject, sourceButton.transform.parent);
                clone.name = "RamShopTabButton";
                clone.SetActive(true);
                bindings.arraySize++;
                SerializedProperty target = bindings.GetArrayElementAtIndex(bindings.arraySize - 1);
                CopyBinding(source, target, sourceButton.transform, clone.transform);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tabBar);
        }

        private static void CopyBinding(SerializedProperty source, SerializedProperty target, Transform sourceRoot, Transform cloneRoot)
        {
            string[] names = { "button", "label", "icon", "selectionFrame", "lockedBadge", "lockedLabel" };
            foreach (string name in names)
            {
                Object original = source.FindPropertyRelative(name).objectReferenceValue;
                target.FindPropertyRelative(name).objectReferenceValue = Remap(original, sourceRoot, cloneRoot);
            }
        }

        private static Object Remap(Object original, Transform sourceRoot, Transform cloneRoot)
        {
            if (original == null) return null;
            Component component = original as Component;
            GameObject gameObject = original as GameObject;
            Transform source = component != null ? component.transform : gameObject != null ? gameObject.transform : null;
            if (source == null) return original;
            string path = AnimationUtility.CalculateTransformPath(source, sourceRoot);
            Transform mapped = string.IsNullOrEmpty(path) ? cloneRoot : cloneRoot.Find(path);
            if (mapped == null) return null;
            if (component != null) return mapped.GetComponent(component.GetType());
            return mapped.gameObject;
        }

        private static void AssignRootSfx(GameObject root)
        {
            MonoBehaviour router = root.GetComponentsInChildren<MonoBehaviour>(true)
                .FirstOrDefault(component => component.GetType().Name == "UiSfxRouter");
            if (router == null) return;
            foreach (MonoBehaviour motion in root.GetComponentsInChildren<MonoBehaviour>(true)
                         .Where(component => component.GetType().Name == "UiSelectableMotion"))
            {
                SerializedObject serialized = new SerializedObject(motion);
                SerializedProperty property = serialized.FindProperty("sfxRouter");
                if (property != null) property.objectReferenceValue = router;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private const string BundleRoot = "Assets/Sprites/UI/PlayerMenu/RamShop/RamBundles";
        private const string FocusRingPath = "Assets/Sprites/UI/PlayerMenu/9sliceborder_orange_smol.png";

        private static void CreateBundleCard(
            Transform row,
            string name,
            string folder,
            string framePrefix,
            int ramAmount,
            string tierLabel,
            string valueLabel,
            string discountLabel,
            Color accent,
            bool featured,
            float pulseDelay)
        {
            RectTransform cell = PlayerMenuUiFactory.CreateRect(name + "Cell", row);
            PlayerMenuUiFactory.LayoutElement(cell.gameObject, flexibleWidth: 1f, flexibleHeight: 1f);

            var cardObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            cardObject.transform.SetParent(cell, false);
            RectTransform card = cardObject.GetComponent<RectTransform>();
            PlayerMenuUiFactory.Stretch(card);

            Image hit = cardObject.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            Button button = cardObject.GetComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            RectTransform revealRoot = PlayerMenuUiFactory.CreateRect("RevealRoot", card);
            PlayerMenuUiFactory.Stretch(revealRoot, 4f, 4f, 4f, 4f);
            CanvasGroup revealGroup = revealRoot.gameObject.AddComponent<CanvasGroup>();
            RectTransform pressRoot = PlayerMenuUiFactory.CreateRect("PressRoot", revealRoot);
            PlayerMenuUiFactory.Stretch(pressRoot);

            Image glow = PlayerMenuUiFactory.CreateImage("SelectionGlow", pressRoot, accent);
            Sprite glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FocusRingPath);
            if (glowSprite != null)
            {
                glow.sprite = glowSprite;
                glow.type = Image.Type.Sliced;
                glow.pixelsPerUnitMultiplier = 0.8f;
            }
            PlayerMenuUiFactory.Stretch(glow.rectTransform, -12f, -12f, -12f, -12f);
            glow.raycastTarget = false;
            CanvasGroup glowGroup = glow.gameObject.AddComponent<CanvasGroup>();
            glowGroup.alpha = featured ? 0.22f : 0f;

            Color surfaceColor = Color.Lerp(PanelRaised, accent, featured ? 0.19f : 0.09f);
            surfaceColor.a = 0.985f;
            Image surface = SlicedImage("CardSurface", pressRoot, BlankPanelPath, surfaceColor, false);
            PlayerMenuUiFactory.Stretch(surface.rectTransform);

            Image accentLine = PlayerMenuUiFactory.CreateImage("SignalBar", pressRoot, accent);
            SetAnchors(accentLine.rectTransform, new Vector2(0.06f, 0.94f), new Vector2(0.94f, 0.965f));

            TMP_Text tier = PlayerMenuUiFactory.CreateText(
                "TierLabel", pressRoot, tierLabel, 18f, TextAlignmentOptions.MidlineLeft, false);
            SetAnchors(tier.rectTransform, new Vector2(0.065f, 0.835f), new Vector2(0.52f, 0.94f));
            tier.fontStyle = FontStyles.Bold;
            tier.color = accent;
            tier.enableAutoSizing = true;
            tier.fontSizeMin = 13f;
            tier.fontSizeMax = 19f;

            TMP_Text value = PlayerMenuUiFactory.CreateText(
                "ValueLabel", pressRoot, valueLabel, 14f, TextAlignmentOptions.MidlineLeft, false);
            SetAnchors(value.rectTransform, new Vector2(0.065f, 0.77f), new Vector2(0.52f, 0.84f));
            value.fontStyle = FontStyles.Bold;
            value.color = new Color(0.68f, 0.74f, 0.86f, 1f);

            RectTransform artPulse = PlayerMenuUiFactory.CreateRect("ArtPulse", pressRoot);
            SetAnchors(artPulse, new Vector2(0.16f, 0.31f), new Vector2(0.84f, 0.77f));
            Image artHalo = PlayerMenuUiFactory.CreateImage(
                "ArtHalo", artPulse, new Color(accent.r, accent.g, accent.b, featured ? 0.18f : 0.09f));
            Sprite haloSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BlankPanelPath);
            if (haloSprite != null)
            {
                artHalo.sprite = haloSprite;
                artHalo.type = Image.Type.Sliced;
            }
            PlayerMenuUiFactory.Stretch(artHalo.rectTransform, 18f, 12f, 18f, 12f);
            artHalo.raycastTarget = false;

            Image art = PlayerMenuUiFactory.CreateImage("Art", artPulse, Color.white);
            PlayerMenuUiFactory.Stretch(art.rectTransform, 4f, 4f, 4f, 4f);
            art.preserveAspect = true;
            art.raycastTarget = false;

            Sprite[] frames = LoadBundleFrames(folder, framePrefix);
            if (frames.Length > 0)
                art.sprite = frames[0];

            TMP_Text amount = PlayerMenuUiFactory.CreateText(
                "RamAmountLabel", pressRoot, $"{ramAmount:N0} RAM", 29f,
                TextAlignmentOptions.Center, false);
            SetAnchors(amount.rectTransform, new Vector2(0.05f, 0.17f), new Vector2(0.95f, 0.32f));
            amount.fontStyle = FontStyles.Bold;
            amount.color = Color.white;
            amount.enableAutoSizing = true;
            amount.fontSizeMin = 20f;
            amount.fontSizeMax = 31f;

            TMP_Text price = PlayerMenuUiFactory.CreateText(
                "PriceLabel", pressRoot, "QUOTE IN SOL", 17f,
                TextAlignmentOptions.Center, false);
            SetAnchors(price.rectTransform, new Vector2(0.08f, 0.055f), new Vector2(0.92f, 0.18f));
            price.fontStyle = FontStyles.Bold;
            price.color = new Color(0.66f, 0.72f, 0.84f, 1f);
            price.enableAutoSizing = true;
            price.fontSizeMin = 13f;
            price.fontSizeMax = 18f;

            if (!string.IsNullOrEmpty(discountLabel))
                AddDiscountRibbon(pressRoot, discountLabel);

            RamBundleCardPresenter presenter = cardObject.AddComponent<RamBundleCardPresenter>();
            SerializedObject presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("artImage").objectReferenceValue = art;
            presenterSerialized.FindProperty("pulseRoot").objectReferenceValue = artPulse;
            presenterSerialized.FindProperty("revealRoot").objectReferenceValue = revealRoot;
            presenterSerialized.FindProperty("revealGroup").objectReferenceValue = revealGroup;
            presenterSerialized.FindProperty("selectionGlow").objectReferenceValue = glowGroup;
            presenterSerialized.FindProperty("priceLabel").objectReferenceValue = price;
            presenterSerialized.FindProperty("ramAmount").intValue = ramAmount;
            presenterSerialized.FindProperty("framesPerSecond").floatValue = 8f;
            presenterSerialized.FindProperty("pulseScale").floatValue = 1.03f;
            presenterSerialized.FindProperty("pulseHalfSeconds").floatValue = 0.9f;
            presenterSerialized.FindProperty("pulseDelay").floatValue = pulseDelay;
            presenterSerialized.FindProperty("featured").boolValue = featured;
            SerializedProperty frameArray = presenterSerialized.FindProperty("frames");
            frameArray.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            AddBundleFocusRing(card);
            AddSelectableMotion(button, pressRoot, hit, false);
        }

        private static void AddDiscountRibbon(Transform visual, string label)
        {
            Image ribbon = PlayerMenuUiFactory.CreateImage(
                "DiscountRibbon", visual, new Color(0.78f, 0.14f, 0.16f, 0.96f));
            RectTransform rect = ribbon.rectTransform;
            rect.anchorMin = new Vector2(0.56f, 0.80f);
            rect.anchorMax = new Vector2(1.02f, 0.94f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, -8f);
            ribbon.raycastTarget = false;

            TMP_Text text = PlayerMenuUiFactory.CreateText("Label", ribbon.transform, label, 19f, TextAlignmentOptions.Center, false);
            PlayerMenuUiFactory.Stretch(text.rectTransform, 6f, 2f, 6f, 2f);
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 20f;
        }

        private static void AddBundleFocusRing(RectTransform card)
        {
            Image ring = PlayerMenuUiFactory.CreateImage("FocusFrame", card, Color.white);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FocusRingPath);
            if (sprite != null)
            {
                ring.sprite = sprite;
                ring.type = Image.Type.Sliced;
                ring.pixelsPerUnitMultiplier = 1.4f;
            }
            PlayerMenuUiFactory.Stretch(ring.rectTransform, -8f, -8f, -8f, -8f);
            ring.raycastTarget = false;
            ring.enabled = false;

            MenuSelectableFocusVisual visual = card.gameObject.AddComponent<MenuSelectableFocusVisual>();
            SerializedObject serialized = new SerializedObject(visual);
            serialized.FindProperty("focusFrame").objectReferenceValue = ring;
            serialized.FindProperty("focusColor").colorValue = Color.white;
            serialized.FindProperty("focusOutlineInset").floatValue = -8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Sprite[] LoadBundleFrames(string folder, string framePrefix)
        {
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++)
            {
                string path = $"{BundleRoot}/{folder}/{framePrefix}{i + 1}.png";
                EnsurePointSprite(path);
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (frames[i] == null)
                    Debug.LogError($"[RamShopViewPrefabUtility] Missing bundle frame '{path}'.");
            }
            return frames;
        }

        private static void EnsurePointSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool dirty = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || importer.mipmapEnabled
                || importer.filterMode != FilterMode.Point
                || !importer.alphaIsTransparency
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || settings.spriteMeshType != SpriteMeshType.FullRect;

            if (!dirty)
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.filterMode = FilterMode.Point;
            settings.alphaIsTransparency = true;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }

        private static Button ShopButton(string name, Transform parent, string label)
        {
            Button button = PlayerMenuUiFactory.CreateButton(name, parent, label, Color.white);
            Image image = button.GetComponent<Image>();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GrayButtonPath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.75f;
            }
            PlayerMenuUiFactory.LayoutElement(button.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.fontStyle = FontStyles.Bold;
                text.color = new Color(0.06f, 0.045f, 0.09f, 1f);
                text.enableAutoSizing = true;
                text.fontSizeMin = 15f;
                text.fontSizeMax = 23f;
            }
            AddSelectableMotion(button, button.transform as RectTransform, image, true);
            return button;
        }

        private static void AddSelectableMotion(
            Button button,
            RectTransform target,
            Image graphic,
            bool configureButtonSprites)
        {
            Type type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(candidate => candidate.Name == "UiSelectableMotion");
            if (type == null) return;
            Component motion = button.GetComponent(type) ?? button.gameObject.AddComponent(type);
            SerializedObject serialized = new SerializedObject(motion);
            Set(serialized, "buttonImage", graphic);
            Set(serialized, "targetTransform", target);
            Set(serialized, "profile", AssetDatabase.LoadAssetAtPath<Object>(MotionPath));
            if (configureButtonSprites)
            {
                Set(serialized, "deselectedSprite", AssetDatabase.LoadAssetAtPath<Sprite>(GrayButtonPath));
                Set(serialized, "selectedSprite", AssetDatabase.LoadAssetAtPath<Sprite>(OrangeButtonPath));
                SerializedProperty shimmerOwns = serialized.FindProperty("shimmerOwnsSelectedVisual");
                if (shimmerOwns != null) shimmerOwns.boolValue = false;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddTenderSelectedMark(Button button)
        {
            Image mark = PlayerMenuUiFactory.CreateImage("SelectedMark", button.transform, HotOrange);
            SetAnchors(mark.rectTransform, new Vector2(0.14f, 0.04f), new Vector2(0.86f, 0.12f));
            mark.raycastTarget = false;
            mark.gameObject.SetActive(button.name == "Tender_SOL");
        }

        private static void AddPendingIndicator(Button confirm)
        {
            Type type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(candidate => candidate.Name == "UiPendingIndicator");
            if (type == null) return;
            Component indicator = confirm.gameObject.AddComponent(type);
            SerializedObject serialized = new SerializedObject(indicator);
            Set(serialized, "label", confirm.GetComponentInChildren<TMP_Text>(true));
            serialized.FindProperty("idleText").stringValue = "CONFIRM  A";
            serialized.FindProperty("pendingText").stringValue = "PROCESSING…";
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyExplicitConfirmNavigation(Button confirm, Button cancel)
        {
            Navigation confirmNavigation = confirm.navigation;
            confirmNavigation.mode = Navigation.Mode.Explicit;
            confirmNavigation.selectOnRight = cancel;
            confirmNavigation.selectOnLeft = cancel;
            confirm.navigation = confirmNavigation;
            Navigation cancelNavigation = cancel.navigation;
            cancelNavigation.mode = Navigation.Mode.Explicit;
            cancelNavigation.selectOnRight = confirm;
            cancelNavigation.selectOnLeft = confirm;
            cancel.navigation = cancelNavigation;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
        {
            Image image = SlicedImage(name, parent, BlankPanelPath, PanelInk, true);
            image.raycastTarget = true;
            SetAnchors(image.rectTransform, min, max);
            return image.rectTransform;
        }

        private static Image SlicedImage(
            string name,
            Transform parent,
            string spritePath,
            Color color,
            bool raycastTarget)
        {
            Image image = PlayerMenuUiFactory.CreateImage(name, parent, color);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 0.75f;
            }
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static void SetAnchors(RectTransform transform, Vector2 min, Vector2 max)
        {
            transform.anchorMin = min;
            transform.anchorMax = max;
            transform.offsetMin = Vector2.zero;
            transform.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject serialized, string propertyName, Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }

        private static int FindAsset(SerializedProperty array, Object target)
        {
            for (int i = 0; i < array.arraySize; i++)
                if (array.GetArrayElementAtIndex(i).objectReferenceValue == target) return i;
            return -1;
        }
    }
}
#endif
