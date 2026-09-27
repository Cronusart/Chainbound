#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class InventoryViewPrefabUtility
    {
        private const string InventoryPrefabPath = "Assets/Prefabs/UI/PlayerMenu/InventoryView.prefab";
        private const string PlayerMenuRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        private const string SandboxRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab";
        private const string InventoryTabAssetPath = "Assets/Data/UI/PlayerMenuTab_inventory.asset";
        private const string InventoryPresentationCatalogPath = "Assets/Data/UI/InventoryItemPresentationCatalog.asset";
        private const string SupabaseConfigPath = "Assets/Data/Supabase/SupabaseConfig.asset";
        private const string MotionProfilePath = "Assets/Data/UI/PartyTabMotionProfile.asset";
        private const string TypeIconPalettePath = "Assets/Data/Combat/Type/Icons/Type_Icons.asset";
        private const string CompactStarRatingArtPath = "Assets/Data/UI/CompactStarRatingArt_Smol.asset";
        private const string CycleIconPalettePath = "Assets/Data/UI/CycleIconPalette.asset";
        private const string SmolStarSpritePath = "Assets/Sprites/UI/StarSystem/smol_stars/smol_star.png";
        private const string PlayerMenuScanlineTexturePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory View Prefab", priority = 52)]
        public static void EnsureInventoryViewPrefab()
        {
            EnsureInventoryViewPrefab(syncRoots: false);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Sync Inventory View Into Player Menu Roots", priority = 53)]
        public static void SyncInventoryViewIntoPlayerMenuRoots()
        {
            EnsureInventoryViewPrefab(syncRoots: true);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory Structural Layout", priority = 54)]
        public static void EnsureInventoryStructuralLayout()
        {
            EnsureInventoryViewPrefab(syncRoots: false);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Sync Inventory Category Rail Visuals From Chainlings", priority = 55)]
        public static void SyncInventoryCategoryRailVisualsFromChainlings()
        {
            EditorUtility.DisplayDialog(
                "Inventory Rail Visuals",
                "Inventory category rail visuals are now authored directly on InventoryView.prefab. The old prefab-to-prefab sync path has been disabled to avoid Unity editor crashes.",
                "OK");
            Debug.Log("[InventoryViewPrefabUtility] Inventory category rail visuals are authored directly on InventoryView.prefab; sync command is intentionally non-destructive.");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory Category Rail Authoring", priority = 56)]
        public static void EnsureInventoryCategoryRailAuthoring()
        {
            InventoryCategoryRail selectedRail = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<InventoryCategoryRail>(true)
                : null;
            if (selectedRail != null)
            {
                EnsureCategoryRailAuthoring(selectedRail);
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(
                    "Select Category Rail",
                    "Select InventoryView/CategoryPanel or its InventoryCategoryRail component, then run this command again.",
                    "OK");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                InventoryScreen screen = instance.GetComponent<InventoryScreen>();
                if (screen != null)
                    screen.EnsureStructuralLayoutForEditor();

                InventoryCategoryRail rail = instance.GetComponentInChildren<InventoryCategoryRail>(true);
                if (rail == null)
                {
                    Debug.LogWarning("[InventoryViewPrefabUtility] No InventoryCategoryRail found to author.");
                    return;
                }

                EnsureCategoryRailAuthoring(rail);
                PrefabUtility.SaveAsPrefabAsset(instance, InventoryPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[InventoryViewPrefabUtility] Authored Inventory category rail controls on InventoryView.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory List Panel Authoring", priority = 57)]
        public static void EnsureInventoryListPanelAuthoring()
        {
            InventoryScreen selectedScreen = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<InventoryScreen>(true)
                : null;
            if (selectedScreen != null)
            {
                EnsureListPanelAuthoring(selectedScreen);
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(
                    "Select Inventory View",
                    "Select InventoryView or any child under its ListPanel, then run this command again.",
                    "OK");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                InventoryScreen screen = instance.GetComponent<InventoryScreen>();
                if (screen == null)
                {
                    Debug.LogWarning("[InventoryViewPrefabUtility] No InventoryScreen found to author ListPanel.");
                    return;
                }

                EnsureListPanelAuthoring(screen);
                PrefabUtility.SaveAsPrefabAsset(instance, InventoryPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[InventoryViewPrefabUtility] Authored Inventory ListPanel controls on InventoryView.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory Actions Panel Authoring", priority = 58)]
        public static void EnsureInventoryActionsPanelAuthoring()
        {
            InventoryScreen selectedScreen = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<InventoryScreen>(true)
                : null;
            if (selectedScreen != null)
            {
                EnsureActionsPanelAuthoring(selectedScreen);
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(
                    "Select Inventory View",
                    "Select InventoryView or any child under its ActionsPanel, then run this command again.",
                    "OK");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                InventoryScreen screen = instance.GetComponent<InventoryScreen>();
                if (screen == null)
                {
                    Debug.LogWarning("[InventoryViewPrefabUtility] No InventoryScreen found to author ActionsPanel.");
                    return;
                }

                EnsureActionsPanelAuthoring(screen);
                PrefabUtility.SaveAsPrefabAsset(instance, InventoryPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[InventoryViewPrefabUtility] Authored Inventory ActionsPanel controls on InventoryView.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory Use Popup Authoring", priority = 59)]
        public static void EnsureInventoryUsePopupAuthoring()
        {
            InventoryScreen selectedScreen = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<InventoryScreen>(true)
                : null;
            if (selectedScreen != null)
            {
                EnsureUsePopupAuthoring(selectedScreen);
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(
                    "Select Inventory View",
                    "Select InventoryView or any child under UsePopup, then run this command again.",
                    "OK");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                InventoryScreen screen = instance.GetComponent<InventoryScreen>();
                if (screen == null)
                {
                    Debug.LogWarning("[InventoryViewPrefabUtility] No InventoryScreen found to author UsePopup.");
                    return;
                }

                EnsureUsePopupAuthoring(screen);
                PrefabUtility.SaveAsPrefabAsset(instance, InventoryPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[InventoryViewPrefabUtility] Authored Inventory UsePopup on InventoryView.prefab. Edit UsePopupRoot / Message / TargetSlotTemplate in Prefab Mode.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Inventory Section Frame Authoring", priority = 60)]
        public static void EnsureInventorySectionFrameAuthoring()
        {
            InventoryScreen selectedScreen = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<InventoryScreen>(true)
                : null;
            if (selectedScreen != null)
            {
                EnsureSectionFrameAuthoring(selectedScreen);
                return;
            }

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(
                    "Select Inventory View",
                    "Select InventoryView, SafeContent, or any child under the Inventory tab, then run this command again.",
                    "OK");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                InventoryScreen screen = instance.GetComponent<InventoryScreen>();
                if (screen == null)
                {
                    Debug.LogWarning("[InventoryViewPrefabUtility] No InventoryScreen found to author section frames.");
                    return;
                }

                EnsureSectionFrameAuthoring(screen);
                PrefabUtility.SaveAsPrefabAsset(instance, InventoryPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[InventoryViewPrefabUtility] Authored Inventory section frames on InventoryView.prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        public static void EnsureCategoryRailAuthoring(InventoryCategoryRail rail)
        {
            if (rail == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(rail.gameObject, "Ensure Inventory Category Rail Authoring");
            rail.BuildDefaultLayoutForEditor();
            EditorUtility.SetDirty(rail);
            EditorUtility.SetDirty(rail.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(rail);
        }

        public static void EnsureListPanelAuthoring(InventoryScreen screen)
        {
            if (screen == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(screen.gameObject, "Ensure Inventory List Panel Authoring");
            screen.EnsureListPanelAuthoringForEditor();
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(screen.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        }

        public static void EnsureSectionFrameAuthoring(InventoryScreen screen)
        {
            if (screen == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(screen.gameObject, "Ensure Inventory Section Frame Authoring");
            screen.EnsureSectionFrameAuthoringForEditor();
            InventorySectionChrome chrome = screen.GetComponentInChildren<InventorySectionChrome>(true);
            if (chrome != null)
                EditorUtility.SetDirty(chrome);
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(screen.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        }

        public static void EnsureActionsPanelAuthoring(InventoryScreen screen)
        {
            if (screen == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(screen.gameObject, "Ensure Inventory Actions Panel Authoring");
            screen.EnsureActionsPanelAuthoringForEditor();
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(screen.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        }

        public static void EnsureUsePopupAuthoring(InventoryScreen screen)
        {
            if (screen == null)
                return;

            Undo.RegisterFullObjectHierarchyUndo(screen.gameObject, "Ensure Inventory Use Popup Authoring");
            screen.EnsureUsePopupAuthoringForEditor();

            InventoryItemUsePopupController popup = screen.GetComponentInChildren<InventoryItemUsePopupController>(true);
            if (popup != null)
            {
                TypeIconPaletteSO typeIcons = AssetDatabase.LoadAssetAtPath<TypeIconPaletteSO>(TypeIconPalettePath);
                CompactStarRatingArtSO starArt = EnsureCompactStarRatingArt();
                CycleIconPaletteSO cyclePalette = EnsureCycleIconPalette();
                PartyTabMotionProfileSO motion = AssetDatabase.LoadAssetAtPath<PartyTabMotionProfileSO>(MotionProfilePath);
                popup.ConfigurePresentation(typeIcons, starArt, cyclePalette);
                popup.ConfigureMotion(motion);
                popup.EnsureAuthoringLayoutForEditor(popup.transform as RectTransform);

                Texture2D scanlineTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(PlayerMenuScanlineTexturePath);
                if (scanlineTexture != null)
                    popup.ConfigurePopupScanlines(scanlineTexture, enabled: true);
                else
                    Debug.LogWarning($"[InventoryViewPrefabUtility] Missing scanline texture at {PlayerMenuScanlineTexturePath}.");

                SerializedObject serializedPopup = new SerializedObject(popup);
                SetObject(serializedPopup, "typeIconPalette", typeIcons, onlyWhenMissing: true);
                SetObject(serializedPopup, "compactStarRatingArt", starArt, onlyWhenMissing: false);
                SetObject(serializedPopup, "cycleIconPalette", cyclePalette, onlyWhenMissing: false);
                SetObject(serializedPopup, "motionProfile", motion, onlyWhenMissing: true);
                Transform frame = popup.transform.Find("UsePopupRoot/UsePopupPanel/TargetSlotFrame");
                if (frame != null)
                    SetObject(serializedPopup, "selectionFrame", frame, onlyWhenMissing: false);
                serializedPopup.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedScreen = new SerializedObject(screen);
                SetObject(serializedScreen, "usePopup", popup, onlyWhenMissing: false);
                serializedScreen.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(popup);
            }

            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(screen.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(screen);
        }

        private static void EnsureInventoryViewPrefab(bool syncRoots)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorUtility.DisplayDialog(
                    "Close Prefab Mode",
                    "Close Prefab Mode before running the Inventory View prefab utility. This tool loads and saves prefab contents directly.",
                    "OK");
                Debug.LogWarning("[InventoryViewPrefabUtility] Aborted because Prefab Mode is open.");
                return;
            }

            Debug.Log(syncRoots
                ? "[InventoryViewPrefabUtility] Starting InventoryView prefab ensure with PlayerMenuRoot sync."
                : "[InventoryViewPrefabUtility] Starting InventoryView prefab ensure without root sync.");

            EnsureFolder("Assets/Prefabs/UI", "PlayerMenu");
            EnsureFolder("Assets/Data", "UI");
            try
            {
                EditorUtility.DisplayProgressBar("Inventory View", "Ensuring InventoryView prefab...", 0.2f);
                InventoryScreen screenPrefab = EnsureInventoryPrefabAsset();
                if (screenPrefab == null)
                    return;

                EditorUtility.DisplayProgressBar("Inventory View", "Wiring inventory tab asset...", 0.5f);
                WireInventoryTabAsset(screenPrefab);

                if (syncRoots)
                {
                    EditorUtility.DisplayProgressBar("Inventory View", "Syncing PlayerMenuRoot prefabs...", 0.75f);
                    Debug.Log("[InventoryViewPrefabUtility] Syncing PlayerMenuRoot prefabs.");
                    SyncRootIfPresent(PlayerMenuRootPrefabPath);
                    SyncRootIfPresent(SandboxRootPrefabPath);
                }

                Debug.Log("[InventoryViewPrefabUtility] Saving inventory view assets.");
                AssetDatabase.SaveAssets();
                Debug.Log(syncRoots
                    ? "[InventoryViewPrefabUtility] Ensured InventoryView prefab, tab asset wiring, and PlayerMenuRoot embedded screen sync."
                    : "[InventoryViewPrefabUtility] Ensured InventoryView prefab and inventory tab asset wiring. Run Sync Inventory View Into Player Menu Roots when you are ready to embed it.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static InventoryScreen EnsureInventoryPrefabAsset()
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath);
            if (prefabAsset == null)
            {
                Debug.Log($"[InventoryViewPrefabUtility] Creating missing prefab at {InventoryPrefabPath}.");
                return CreateInventoryPrefab();
            }

            Debug.Log($"[InventoryViewPrefabUtility] Repairing existing prefab at {InventoryPrefabPath}.");
            GameObject instance = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
            try
            {
                InventoryScreen screen = instance.GetComponent<InventoryScreen>();
                if (screen == null)
                    screen = instance.AddComponent<InventoryScreen>();

                CanvasGroup canvasGroup = instance.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = instance.AddComponent<CanvasGroup>();

                screen.SetScreenIdentity("inventory", "Inventory", canvasGroup);
                WireDefaultReferences(screen, onlyWhenMissing: true);
                EnsureMinimalScaffold(instance, screen);

                RectTransform rectTransform = instance.transform as RectTransform;
                Stretch(rectTransform);

                EditorUtility.SetDirty(screen);
                PrefabUtility.SaveAsPrefabAsset(instance, InventoryPrefabPath);
                Debug.Log("[InventoryViewPrefabUtility] Saved repaired InventoryView prefab.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath);
            return saved != null ? saved.GetComponent<InventoryScreen>() : null;
        }

        private static InventoryScreen CreateInventoryPrefab()
        {
            Debug.Log("[InventoryViewPrefabUtility] Building minimal InventoryView scaffold.");
            GameObject root = new GameObject(
                "InventoryView",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(InventoryScreen),
                typeof(InventoryMotionController));

            try
            {
                Stretch(root.GetComponent<RectTransform>());
                InventoryScreen screen = root.GetComponent<InventoryScreen>();
                CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
                screen.SetScreenIdentity("inventory", "Inventory", canvasGroup);
                WireDefaultReferences(screen, onlyWhenMissing: false);
                EnsureMinimalScaffold(root, screen);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, InventoryPrefabPath);
                Debug.Log("[InventoryViewPrefabUtility] Saved new InventoryView prefab.");
                return saved != null ? saved.GetComponent<InventoryScreen>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void EnsureMinimalScaffold(InventoryScreen screen)
        {
            if (screen == null)
                return;

            WireDefaultReferences(screen, onlyWhenMissing: true);
            EnsureMinimalScaffold(screen.gameObject, screen);
            EditorUtility.SetDirty(screen);
        }

        private static void EnsureMinimalScaffold(GameObject root, InventoryScreen screen)
        {
            if (root == null || screen == null)
                return;

            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = root.AddComponent<CanvasGroup>();

            InventoryMotionController motionController = root.GetComponent<InventoryMotionController>();
            if (motionController == null)
                motionController = root.AddComponent<InventoryMotionController>();

            screen.SetScreenIdentity("inventory", "Inventory", canvasGroup);

            RectTransform rootRect = root.transform as RectTransform;
            Stretch(rootRect);

            RectTransform safeContent = root.transform.Find("SafeContent") as RectTransform;
            if (safeContent == null)
            {
                GameObject safeContentObject = new GameObject("SafeContent", typeof(RectTransform));
                safeContentObject.transform.SetParent(root.transform, false);
                safeContent = safeContentObject.GetComponent<RectTransform>();
                Debug.Log("[InventoryViewPrefabUtility] Added minimal SafeContent authoring root.");
            }

            Stretch(safeContent, 48f, 72f, 48f, 48f);

            RectTransform statusStrip = EnsureRect(root.transform, "StatusStrip");
            Top(statusStrip, 54f, 48f, 48f, 16f);

            RectTransform categoryPanel = EnsurePanelRoot(safeContent, "CategoryPanel", new Vector2(0f, 0.08f), new Vector2(0.22f, 0.94f));
            RectTransform listPanel = EnsurePanelRoot(safeContent, "ListPanel", new Vector2(0.245f, 0.08f), new Vector2(0.72f, 0.94f));
            RectTransform actionsPanel = EnsurePanelRoot(safeContent, "ActionsPanel", new Vector2(0.745f, 0.08f), new Vector2(1f, 0.94f));

            CanvasGroup categoryGroup = GetOrAdd<CanvasGroup>(categoryPanel.gameObject);
            CanvasGroup listGroup = GetOrAdd<CanvasGroup>(listPanel.gameObject);
            CanvasGroup actionsGroup = GetOrAdd<CanvasGroup>(actionsPanel.gameObject);
            InventoryCategoryRail categoryRail = GetOrAdd<InventoryCategoryRail>(categoryPanel.gameObject);
            InventorySectionChrome sectionChrome = GetOrAdd<InventorySectionChrome>(safeContent.gameObject);
            sectionChrome.Configure(categoryPanel, listPanel, actionsPanel);

            SerializedObject serializedRail = new SerializedObject(categoryRail);
            SetObject(serializedRail, "motionProfile", AssetDatabase.LoadAssetAtPath<PartyTabMotionProfileSO>(MotionProfilePath), onlyWhenMissing: true);
            serializedRail.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedScreen = new SerializedObject(screen);
            SetObject(serializedScreen, "canvasGroup", canvasGroup, onlyWhenMissing: false);
            SetObject(serializedScreen, "safeContent", safeContent, onlyWhenMissing: false);
            SetObject(serializedScreen, "motionController", motionController, onlyWhenMissing: true);
            SetObject(serializedScreen, "categoryRail", categoryRail, onlyWhenMissing: true);
            SetObject(serializedScreen, "sectionChrome", sectionChrome, onlyWhenMissing: true);
            SetObject(serializedScreen, "categoryPanelGroup", categoryGroup, onlyWhenMissing: true);
            SetObject(serializedScreen, "listPanelGroup", listGroup, onlyWhenMissing: true);
            SetObject(serializedScreen, "actionsPanelGroup", actionsGroup, onlyWhenMissing: true);
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();

            screen.EnsureStructuralLayoutForEditor();

            InventoryItemUsePopupController usePopup = root.GetComponentInChildren<InventoryItemUsePopupController>(true);
            if (usePopup != null)
            {
                serializedScreen = new SerializedObject(screen);
                SetObject(serializedScreen, "usePopup", usePopup, onlyWhenMissing: false);
                serializedScreen.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(usePopup);
            }

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(canvasGroup);
            EditorUtility.SetDirty(motionController);
            EditorUtility.SetDirty(categoryRail);
            EditorUtility.SetDirty(sectionChrome);
        }

        private static void WireDefaultReferences(InventoryScreen screen, bool onlyWhenMissing)
        {
            if (screen == null)
                return;

            SerializedObject serializedScreen = new SerializedObject(screen);
            SetObject(serializedScreen, "supabaseConfig", AssetDatabase.LoadAssetAtPath<Object>(SupabaseConfigPath), onlyWhenMissing);
            SetObject(serializedScreen, "motionProfile", AssetDatabase.LoadAssetAtPath<PartyTabMotionProfileSO>(MotionProfilePath), onlyWhenMissing);
            SetObject(serializedScreen, "presentationCatalog", EnsurePresentationCatalog(), onlyWhenMissing);
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();
        }

        private static InventoryItemPresentationCatalogSO EnsurePresentationCatalog()
        {
            InventoryItemPresentationCatalogSO catalog =
                AssetDatabase.LoadAssetAtPath<InventoryItemPresentationCatalogSO>(InventoryPresentationCatalogPath);
            if (catalog != null)
                return catalog;

            EnsureFolder("Assets/Data", "UI");
            catalog = ScriptableObject.CreateInstance<InventoryItemPresentationCatalogSO>();
            AssetDatabase.CreateAsset(catalog, InventoryPresentationCatalogPath);
            EditorUtility.SetDirty(catalog);
            Debug.Log($"[InventoryViewPrefabUtility] Created {InventoryPresentationCatalogPath}.");
            return catalog;
        }

        private static CompactStarRatingArtSO EnsureCompactStarRatingArt()
        {
            EnsureSmolStarSpriteImport();
            CompactStarRatingArtSO art = AssetDatabase.LoadAssetAtPath<CompactStarRatingArtSO>(CompactStarRatingArtPath);
            Sprite smol = AssetDatabase.LoadAssetAtPath<Sprite>(SmolStarSpritePath);
            if (art == null)
            {
                EnsureFolder("Assets/Data", "UI");
                art = ScriptableObject.CreateInstance<CompactStarRatingArtSO>();
                AssetDatabase.CreateAsset(art, CompactStarRatingArtPath);
                Debug.Log($"[InventoryViewPrefabUtility] Created {CompactStarRatingArtPath}.");
            }

            SerializedObject so = new SerializedObject(art);
            SerializedProperty filled = so.FindProperty("filledStar");
            SerializedProperty empty = so.FindProperty("emptyStar");
            SerializedProperty rtl = so.FindProperty("fillRightToLeft");
            SerializedProperty cell = so.FindProperty("cellSize");
            SerializedProperty spacing = so.FindProperty("spacing");
            if (filled != null && (filled.objectReferenceValue == null || filled.objectReferenceValue != smol) && smol != null)
                filled.objectReferenceValue = smol;
            if (empty != null && empty.objectReferenceValue == null && smol != null)
                empty.objectReferenceValue = smol;
            if (rtl != null)
                rtl.boolValue = true;
            if (cell != null && cell.floatValue <= 0f)
                cell.floatValue = 18f;
            if (spacing != null && spacing.floatValue <= 0f)
                spacing.floatValue = 2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(art);
            return art;
        }

        private static CycleIconPaletteSO EnsureCycleIconPalette()
        {
            CycleIconPaletteSO palette = AssetDatabase.LoadAssetAtPath<CycleIconPaletteSO>(CycleIconPalettePath);
            if (palette != null)
                return palette;

            EnsureFolder("Assets/Data", "UI");
            palette = ScriptableObject.CreateInstance<CycleIconPaletteSO>();
            AssetDatabase.CreateAsset(palette, CycleIconPalettePath);
            EditorUtility.SetDirty(palette);
            Debug.Log(
                $"[InventoryViewPrefabUtility] Created {CycleIconPalettePath}. Assign cycle I–V / Max sprites under Assets/Sprites/UI (CycleSystem) when ready.");
            return palette;
        }

        private static void EnsureSmolStarSpriteImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SmolStarSpritePath) as TextureImporter;
            if (importer == null)
                return;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                dirty = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                dirty = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                dirty = true;
            }

            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                dirty = true;
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (!settings.spriteGenerateFallbackPhysicsShape)
            {
                // keep default
            }

            if (dirty)
            {
                importer.SaveAndReimport();
                Debug.Log("[InventoryViewPrefabUtility] Reimported smol_star as Sprite (Point filter).");
            }
        }

        private static void WireInventoryTabAsset(InventoryScreen screenPrefab)
        {
            Debug.Log("[InventoryViewPrefabUtility] Wiring PlayerMenuTab_inventory.asset.");
            MenuTabDefinitionSO tab = AssetDatabase.LoadAssetAtPath<MenuTabDefinitionSO>(InventoryTabAssetPath);
            if (tab == null)
            {
                tab = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
                AssetDatabase.CreateAsset(tab, InventoryTabAssetPath);
            }

            SerializedObject serializedTab = new SerializedObject(tab);
            SetString(serializedTab, "tabId", "inventory");
            SetString(serializedTab, "displayName", "Inventory");
            SetString(serializedTab, "description", "Items, cosmetics, consumables, RAM, and wallet inventory.");
            SerializedProperty prefabProperty = serializedTab.FindProperty("tabScreenPrefab");
            if (prefabProperty != null)
                prefabProperty.objectReferenceValue = screenPrefab;
            SerializedProperty modeProperty = serializedTab.FindProperty("screenResolveMode");
            if (modeProperty != null)
                modeProperty.enumValueIndex = (int)PlayerMenuTabScreenResolveMode.PreferEmbeddedThenPrefab;
            SerializedProperty autoResolveProperty = serializedTab.FindProperty("autoResolveTabScreen");
            if (autoResolveProperty != null)
                autoResolveProperty.boolValue = false;
            serializedTab.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(tab);
        }

        private static void SyncRootIfPresent(string prefabPath)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                return;

            PlayerMenuRootTabScreenSyncEditor.SyncPrefabAtPath(prefabPath);
        }

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value, bool onlyWhenMissing)
        {
            if (value == null)
                return;

            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
                return;

            if (onlyWhenMissing && property.objectReferenceValue != null)
                return;

            property.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rectTransform, float left, float top, float right, float bottom)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
            rectTransform.localScale = Vector3.one;
        }

        private static void Top(RectTransform rectTransform, float height, float left, float right, float y)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.offsetMin = new Vector2(left, -height - y);
            rectTransform.offsetMax = new Vector2(-right, -y);
            rectTransform.localScale = Vector3.one;
        }

        private static RectTransform EnsurePanelRoot(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rectTransform = EnsureRect(parent, name);
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;

            if (rectTransform.GetComponent<CanvasRenderer>() == null)
                rectTransform.gameObject.AddComponent<CanvasRenderer>();

            Image image = rectTransform.GetComponent<Image>();
            if (image == null)
                image = rectTransform.gameObject.AddComponent<Image>();
            image.color = new Color(0.07f, 0.095f, 0.14f, 0.86f);
            image.raycastTarget = false;

            return rectTransform;
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            Transform existing = parent != null ? parent.Find(name) : null;
            if (existing is RectTransform existingRect)
                return existingRect;

            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component == null)
                component = gameObject.AddComponent<T>();
            return component;
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (AssetDatabase.IsValidFolder(path))
                return;

            AssetDatabase.CreateFolder(parent, child);
        }
    }

    [CustomEditor(typeof(InventoryScreen))]
    public sealed class InventoryScreenEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Inventory Authoring", EditorStyles.boldLabel);
            if (GUILayout.Button("Ensure Inventory Structural Layout"))
            {
                InventoryScreen screen = (InventoryScreen)target;
                Undo.RegisterFullObjectHierarchyUndo(screen.gameObject, "Ensure Inventory Structural Layout");
                InventoryViewPrefabUtility.EnsureMinimalScaffold(screen);
                EditorUtility.SetDirty(screen);
            }

            EditorGUILayout.HelpBox(
                "This repairs missing Inventory roots, list/detail structure, action buttons, and references without cloning a large external prefab.",
                MessageType.Info);

            if (GUILayout.Button("Ensure Authored List Panel Children"))
                InventoryViewPrefabUtility.EnsureListPanelAuthoring((InventoryScreen)target);

            EditorGUILayout.HelpBox(
                "Creates and wires the concept-image middle panel only: item list area, ScrollRect viewport, scrollbar, item row template, item portrait frame, and description fields.",
                MessageType.Info);

            if (GUILayout.Button("Ensure Authored Actions Panel Children"))
                InventoryViewPrefabUtility.EnsureActionsPanelAuthoring((InventoryScreen)target);

            EditorGUILayout.HelpBox(
                "Creates and wires the right-side ActionsPanel only: select-item prompt plus authorable Use/Drop/optional Refresh vertical buttons.",
                MessageType.Info);

            if (GUILayout.Button("Ensure Authored Use Popup"))
                InventoryViewPrefabUtility.EnsureUsePopupAuthoring((InventoryScreen)target);

            EditorGUILayout.HelpBox(
                "Creates UsePopup/UsePopupRoot: EffectLabel, vertical TargetRow + TargetSlotTemplate, TargetSlotFrame, PartyGroupFrame, SelfConfirmBody, InspectBody, DialoguePanel, PlayerMenu scanline PopupThemeBackground masked to panel 9-slice. Wires Type_Icons, CompactStarRatingArt_Smol, CycleIconPalette.",
                MessageType.Info);

            if (GUILayout.Button("Ensure Authored Section Frames"))
                InventoryViewPrefabUtility.EnsureSectionFrameAuthoring((InventoryScreen)target);

            EditorGUILayout.HelpBox(
                "Creates and wires independent Category/List/Actions section frames as SafeContent children with the Chainlings-style left/center/right authored rects.",
                MessageType.Info);

            if (GUILayout.Button("Ensure Inventory View Prefab"))
                InventoryViewPrefabUtility.EnsureInventoryViewPrefab();
        }
    }

    [CustomEditor(typeof(InventoryCategoryRail))]
    public sealed class InventoryCategoryRailEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Category Rail Authoring", EditorStyles.boldLabel);
            if (GUILayout.Button("Ensure Authored Category Rail Children"))
                InventoryViewPrefabUtility.EnsureCategoryRailAuthoring((InventoryCategoryRail)target);

            EditorGUILayout.HelpBox(
                "Creates and wires CategoryPrompt, CategoryButton, ExpandedCategories, CategoryList, CategoryTemplate, and category row buttons under this rail. It does not copy from Chainlings or save unrelated prefab roots.",
                MessageType.Info);
        }
    }

    [CustomEditor(typeof(InventoryItemUsePopupController))]
    public sealed class InventoryItemUsePopupControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Use Popup Authoring", EditorStyles.boldLabel);
            if (GUILayout.Button("Ensure Authored Use Popup Children"))
            {
                InventoryItemUsePopupController popup = (InventoryItemUsePopupController)target;
                Undo.RegisterFullObjectHierarchyUndo(popup.gameObject, "Ensure Use Popup Authoring");
                popup.EnsureAuthoringLayoutForEditor(popup.transform as RectTransform);
                EditorUtility.SetDirty(popup);
                PrefabUtility.RecordPrefabInstancePropertyModifications(popup);

                InventoryScreen screen = popup.GetComponentInParent<InventoryScreen>(true);
                if (screen != null)
                    InventoryViewPrefabUtility.EnsureUsePopupAuthoring(screen);
            }

            EditorGUILayout.HelpBox(
                "Wires Title / DialoguePanel / TargetSlotTemplate / TargetSlotFrame. Compact RTL smol stars + CycleIconDisplay. Up/Down moves the frame; A applies; B cancels.",
                MessageType.Info);
        }
    }
}
#endif
