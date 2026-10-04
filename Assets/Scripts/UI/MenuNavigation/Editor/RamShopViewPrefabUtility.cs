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

                Image panel = PlayerMenuUiFactory.CreateImage("ShopPanel", primary, PlayerMenuUiFactory.Panel);
                PlayerMenuUiFactory.Stretch(panel.rectTransform);

                RectTransform header = Panel("RamHeader", primary, new Vector2(0.02f, 0.86f), new Vector2(0.98f, 0.98f));
                TMP_Text title = PlayerMenuUiFactory.CreateText("Title", header, "RAM SHOP", 30f, TextAlignmentOptions.MidlineLeft, false);
                SetAnchors(title.rectTransform, new Vector2(0.03f, 0.08f), new Vector2(0.45f, 0.92f));
                TMP_Text balance = PlayerMenuUiFactory.CreateText("RamBalanceLabel", header, "RAM —", 26f, TextAlignmentOptions.MidlineRight, false);
                SetAnchors(balance.rectTransform, new Vector2(0.48f, 0.08f), new Vector2(0.97f, 0.92f));

                RectTransform tenderRail = Panel("TenderRail", primary, new Vector2(0.02f, 0.73f), new Vector2(0.98f, 0.84f));
                HorizontalLayoutGroup tenderLayout = PlayerMenuUiFactory.HorizontalLayout(tenderRail.gameObject, 12f);
                tenderLayout.padding = new RectOffset(12, 12, 8, 8);
                tenderLayout.childForceExpandWidth = true;
                Button sol = ShopButton("Tender_SOL", tenderRail, "SOL");
                Button usdc = ShopButton("Tender_USDC", tenderRail, "USDC");
                Button skr = ShopButton("Tender_SKR", tenderRail, "SKR");

                RectTransform bundleRow = PlayerMenuUiFactory.CreateRect("BundleRow", primary);
                SetAnchors(bundleRow, new Vector2(0.04f, 0.26f), new Vector2(0.96f, 0.72f));
                HorizontalLayoutGroup bundleLayout = PlayerMenuUiFactory.HorizontalLayout(bundleRow.gameObject, 18f);
                bundleLayout.padding = new RectOffset(8, 8, 8, 8);
                bundleLayout.childAlignment = TextAnchor.MiddleCenter;
                bundleLayout.childControlWidth = true;
                bundleLayout.childControlHeight = true;
                bundleLayout.childForceExpandWidth = true;
                bundleLayout.childForceExpandHeight = true;
                // $5 is the 20 RAM/$1 rate (100 RAM). $20 grants 500 RAM (20% off). $100 grants 3000 RAM (33% off).
                CreateBundleCard(bundleRow, "Bundle_5", "5Bundle", "5bundle", 100, null, 0f);
                CreateBundleCard(bundleRow, "Bundle_20", "20Bundle", "20bundle", 500, "20% OFF", 0.25f);
                CreateBundleCard(bundleRow, "Bundle_100", "100Bundle", "100bundle", 3000, "33% OFF", 0.5f);

                TMP_Text unavailable = PlayerMenuUiFactory.CreateText(
                    "UnavailableLabel", primary, "RAM bundles are not available yet.", 22f,
                    TextAlignmentOptions.Center, true);
                SetAnchors(unavailable.rectTransform, new Vector2(0.06f, 0.185f), new Vector2(0.94f, 0.25f));
                unavailable.color = PlayerMenuUiFactory.Muted;

                TMP_Text status = PlayerMenuUiFactory.CreateText(
                    "StatusLabel", primary, "Authoritative catalog loading…", 19f,
                    TextAlignmentOptions.Center, true);
                SetAnchors(status.rectTransform, new Vector2(0.06f, 0.135f), new Vector2(0.94f, 0.185f));
                status.color = PlayerMenuUiFactory.Muted;

                RectTransform footer = PlayerMenuUiFactory.CreateRect("Footer", primary);
                SetAnchors(footer, new Vector2(0.02f, 0.025f), new Vector2(0.98f, 0.13f));
                HorizontalLayoutGroup footerLayout = PlayerMenuUiFactory.HorizontalLayout(footer.gameObject, 14f);
                footerLayout.childAlignment = TextAnchor.MiddleRight;
                footerLayout.childForceExpandWidth = true;
                Button retry = ShopButton("RetryButton", footer, "RETRY");
                Button close = ShopButton("CloseButton", footer, "CLOSE  B");

                RectTransform sheet = Panel("ConfirmSheet", primary, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
                sheet.gameObject.AddComponent<CanvasGroup>();
                TMP_Text confirmTitle = PlayerMenuUiFactory.CreateText("ConfirmTitle", sheet, "CONFIRM RAM PURCHASE", 29f, TextAlignmentOptions.Center, false);
                SetAnchors(confirmTitle.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.96f));
                TMP_Text summary = PlayerMenuUiFactory.CreateText("ConfirmSummaryLabel", sheet, "", 20f, TextAlignmentOptions.TopLeft, true);
                SetAnchors(summary.rectTransform, new Vector2(0.08f, 0.23f), new Vector2(0.92f, 0.82f));
                Button confirm = ShopButton("ConfirmButton", sheet, "CONFIRM  A");
                SetAnchors(confirm.transform as RectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.55f, 0.19f));
                Button cancel = ShopButton("CancelConfirmButton", sheet, "BACK  B");
                SetAnchors(cancel.transform as RectTransform, new Vector2(0.58f, 0.08f), new Vector2(0.92f, 0.19f));
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
            string discountLabel,
            float pulseDelay)
        {
            RectTransform cell = PlayerMenuUiFactory.CreateRect(name + "Cell", row);
            PlayerMenuUiFactory.LayoutElement(cell.gameObject, flexibleWidth: 1f, flexibleHeight: 1f);

            var cardObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(AspectRatioFitter));
            cardObject.transform.SetParent(cell, false);
            RectTransform card = cardObject.GetComponent<RectTransform>();
            PlayerMenuUiFactory.Stretch(card);

            Image hit = cardObject.GetComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            Button button = cardObject.GetComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            AspectRatioFitter fitter = cardObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;

            RectTransform pressRoot = PlayerMenuUiFactory.CreateRect("PressRoot", card);
            PlayerMenuUiFactory.Stretch(pressRoot);
            RectTransform visual = PlayerMenuUiFactory.CreateRect("Visual", pressRoot);
            PlayerMenuUiFactory.Stretch(visual);

            Image art = PlayerMenuUiFactory.CreateImage("Art", visual, Color.white);
            PlayerMenuUiFactory.Stretch(art.rectTransform);
            art.preserveAspect = true;
            art.raycastTarget = false;

            Sprite[] frames = LoadBundleFrames(folder, framePrefix);
            if (frames.Length > 0)
                art.sprite = frames[0];
            if (!string.IsNullOrEmpty(discountLabel))
                AddDiscountRibbon(visual, discountLabel);

            RamBundleCardPresenter presenter = cardObject.AddComponent<RamBundleCardPresenter>();
            SerializedObject presenterSerialized = new SerializedObject(presenter);
            presenterSerialized.FindProperty("artImage").objectReferenceValue = art;
            presenterSerialized.FindProperty("pulseRoot").objectReferenceValue = visual;
            presenterSerialized.FindProperty("ramAmount").intValue = ramAmount;
            presenterSerialized.FindProperty("framesPerSecond").floatValue = 8f;
            presenterSerialized.FindProperty("pulseScale").floatValue = 1.03f;
            presenterSerialized.FindProperty("pulseHalfSeconds").floatValue = 0.9f;
            presenterSerialized.FindProperty("pulseDelay").floatValue = pulseDelay;
            SerializedProperty frameArray = presenterSerialized.FindProperty("frames");
            frameArray.arraySize = frames.Length;
            for (int i = 0; i < frames.Length; i++)
                frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

            AddBundleFocusRing(card);
            AddSelectableMotion(button, pressRoot, hit);
        }

        private static void AddDiscountRibbon(Transform visual, string label)
        {
            Image ribbon = PlayerMenuUiFactory.CreateImage(
                "DiscountRibbon", visual, new Color(0.78f, 0.14f, 0.16f, 0.96f));
            RectTransform rect = ribbon.rectTransform;
            rect.anchorMin = new Vector2(0.34f, 0.76f);
            rect.anchorMax = new Vector2(1.08f, 0.98f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.Euler(0f, 0f, -12f);
            ribbon.raycastTarget = false;

            TMP_Text text = PlayerMenuUiFactory.CreateText("Label", ribbon.transform, label, 24f, TextAlignmentOptions.Center, false);
            PlayerMenuUiFactory.Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.enableAutoSizing = true;
            text.fontSizeMin = 14f;
            text.fontSizeMax = 28f;
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
            Button button = PlayerMenuUiFactory.CreateButton(name, parent, label, PlayerMenuUiFactory.PanelAlt);
            PlayerMenuUiFactory.LayoutElement(button.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            AddSelectableMotion(button, button.transform as RectTransform, button.GetComponent<Image>());
            return button;
        }

        private static void AddSelectableMotion(Button button, RectTransform target, Image graphic)
        {
            Type type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(candidate => candidate.Name == "UiSelectableMotion");
            if (type == null) return;
            Component motion = button.GetComponent(type) ?? button.gameObject.AddComponent(type);
            SerializedObject serialized = new SerializedObject(motion);
            Set(serialized, "buttonImage", graphic);
            Set(serialized, "targetTransform", target);
            Set(serialized, "profile", AssetDatabase.LoadAssetAtPath<Object>(MotionPath));
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
            Image image = PlayerMenuUiFactory.CreateImage(name, parent, PlayerMenuUiFactory.PanelAlt);
            image.raycastTarget = true;
            SetAnchors(image.rectTransform, min, max);
            return image.rectTransform;
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
