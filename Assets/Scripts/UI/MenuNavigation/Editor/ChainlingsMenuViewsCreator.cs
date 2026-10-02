#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static partial class ChainlingsMenuViewsCreator
    {
        private const string DataDir = "Assets/Data/UI";
        private const string PartyTabDataDir = DataDir + "/PartyTab";
        private const string PrefabDir = "Assets/Prefabs/UI/PlayerMenu";
        private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
        private const string CombatBattleCanvasPrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        private const string ViewsAssetPath = DataDir + "/ChainlingsMenuViews.asset";
        private const string CardPrefabPath = PrefabDir + "/ChainlingCardView.prefab";
        private const string ColumnShellPrefabPath = PrefabDir + "/ChainlingPartyColumnShell.prefab";
        private const string PartySlotPrefabPath = PrefabDir + "/ChainlingPartySlotView.prefab";
        private const string DetailPrefabPath = PrefabDir + "/ChainlingDetailView.prefab";
        private const string PartyPrefabPath = PrefabDir + "/ChainlingsPartyView.prefab";
        private const string MotionProfilePath = DataDir + "/PartyTabMotionProfile.asset";
        private const string StarRatingArtPath = DataDir + "/PartyTabStarRatingArt.asset";
        private const string TypeChromePalettePath = PartyTabDataDir + "/ChainlingPartyTypeChromePalette.asset";
        private const string TypeIconPalettePath = "Assets/Data/Combat/Type/Icons/Type_Icons.asset";
        private const string DossierFrameSpritePath = "Assets/Sprites/UI/MainMenu/Buttons/menu_panel_sliced.png";
        private const string DossierPanelSpritePath = "Assets/Sprites/UI/PlayerMenu/infopanel.png";
        private const string DossierOutlineSpritePath = "Assets/Sprites/UI/PlayerMenu/9sliceborder_outline.png";
        private const string DossierIdleButtonSpritePath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png";
        private const string DossierSelectedButtonSpritePath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png";
        private const string DossierScanlineTexturePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png";

        private const string SupabaseConfigPath = "Assets/Data/Supabase/SupabaseConfig.asset";
        private const string SpeciesDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Database/SpeciesDatabase.asset";
        private const string CombatVisualDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Visual/Database/CombatVisualDatabase.asset";
        private const string ChainlingVisualIdentityDatabasePath = "Assets/Data/Combat/ChainlingSpecies/Visual/Cosmetics/ChainlingVisualIdentityDatabase.asset";
        private const string MoveDatabasePath = "Assets/Data/Combat/Moves/Database/MoveDatabase.asset";
        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Create Chainlings Menu View Prefabs", priority = 40)]
        public static void CreateViewPrefabs()
        {
            if ((AssetExists(PartyPrefabPath) || AssetExists(ColumnShellPrefabPath)) &&
                !EditorUtility.DisplayDialog(
                    "Overwrite Chainlings View Prefabs?",
                    "Chainlings menu view prefabs already exist. Overwrite them with a fresh Phase 1 baseline layout?",
                    "Overwrite",
                    "Cancel"))
            {
                return;
            }

            EnsureFolder("Assets/Prefabs/UI", "PlayerMenu");
            EnsureFolder("Assets/Data", "UI");
            EnsureFolder(DataDir, "PartyTab");

            PartyTabMotionProfileSO motionProfile = EnsureMotionProfile();
            PartyTabStarRatingArtSO starRatingArt = EnsureStarRatingArt();
            ChainlingPartyTypeChromePaletteSO typeChromePalette = EnsureTypeChromePalette();
            TypeIconPaletteSO typeIconPalette = AssetDatabase.LoadAssetAtPath<TypeIconPaletteSO>(TypeIconPalettePath);
            ChainlingCardWidget cardPrefab = SaveCardPrefab();
            ChainlingPartyColumnShell columnShellPrefab = SaveColumnShellPrefab(starRatingArt);
            ChainlingDetailScreen detailPrefab = SaveDetailPrefab();
            ChainlingsPartyScreen partyPrefab = SavePartyPrefab(
                columnShellPrefab,
                motionProfile,
                starRatingArt,
                typeChromePalette,
                typeIconPalette);
            ChainlingsMenuViewsSO views = SaveViewsAsset(partyPrefab, columnShellPrefab, detailPrefab, cardPrefab);
            WireGameServicesBootstrap(views);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[ChainlingsMenuViewsCreator] Created Chainlings Phase 1 view prefabs and ChainlingsMenuViews.asset. " +
                "Edit layout in Assets/Prefabs/UI/PlayerMenu/ChainlingsPartyView.prefab.");
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Select Chainlings Menu Views Asset", priority = 41)]
        public static void SelectViewsAsset()
        {
            ChainlingsMenuViewsSO views = LoadOrCreateViewsAsset();
            Selection.activeObject = views;
            EditorGUIUtility.PingObject(views);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Rebuild Chainling Dossier + Capture Wiring", priority = 42)]
        public static void RebuildChainlingDossierAndCaptureWiring()
        {
            EnsureFolder("Assets/Prefabs/UI", "PlayerMenu");
            EnsureFolder("Assets/Data", "UI");

            ChainlingDetailScreen detailPrefab = SaveDetailPrefab();
            ChainlingsMenuViewsSO views = LoadOrCreateViewsAsset();
            SerializedObject viewsSo = new SerializedObject(views);
            SetIfPresent(viewsSo, "detailViewPrefab", detailPrefab);
            viewsSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(views);

            WireCaptureDossierPresenter(detailPrefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ChainlingsMenuViewsCreator] Rebuilt the shared Chainling dossier prefab and wired capture presentation.");
        }

        private static PartyTabMotionProfileSO EnsureMotionProfile()
        {
            PartyTabMotionProfileSO profile = AssetDatabase.LoadAssetAtPath<PartyTabMotionProfileSO>(MotionProfilePath);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<PartyTabMotionProfileSO>();
            AssetDatabase.CreateAsset(profile, MotionProfilePath);
            return profile;
        }

        private static ChainlingCardWidget SaveCardPrefab()
        {
            var go = new GameObject(
                "ChainlingCardView",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(ChainlingCardWidget));

            RectTransform rectTransform = go.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(220f, 260f);

            ChainlingCardWidget widget = go.GetComponent<ChainlingCardWidget>();
            widget.BuildDefaultLayoutForEditor();

            return SavePrefabAsset(go, CardPrefabPath).GetComponent<ChainlingCardWidget>();
        }

        private static ChainlingDetailScreen SaveDetailPrefab()
        {
            var go = new GameObject(
                "ChainlingDetailView",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(ChainlingDetailScreen));

            Stretch(go.GetComponent<RectTransform>());

            CanvasGroup canvasGroup = go.GetComponent<CanvasGroup>();
            ChainlingDetailScreen screen = go.GetComponent<ChainlingDetailScreen>();
            screen.SetScreenIdentity("chainling_detail", "Chainling Detail", canvasGroup);
            WireChainlingDataReferences(new SerializedObject(screen));
            screen.BuildDefaultLayoutForEditor();
            ApplyDossierVisualStyle(go, screen);

            return SavePrefabAsset(go, DetailPrefabPath).GetComponent<ChainlingDetailScreen>();
        }

        private static void ApplyDossierVisualStyle(GameObject root, ChainlingDetailScreen screen)
        {
            if (root == null || screen == null)
                return;

            Sprite frame = AssetDatabase.LoadAssetAtPath<Sprite>(DossierFrameSpritePath);
            Sprite panel = AssetDatabase.LoadAssetAtPath<Sprite>(DossierPanelSpritePath);
            Sprite outline = AssetDatabase.LoadAssetAtPath<Sprite>(DossierOutlineSpritePath);
            Sprite idleButton = AssetDatabase.LoadAssetAtPath<Sprite>(DossierIdleButtonSpritePath);
            Sprite selectedButton = AssetDatabase.LoadAssetAtPath<Sprite>(DossierSelectedButtonSpritePath);
            Texture scanlines = AssetDatabase.LoadAssetAtPath<Texture>(DossierScanlineTexturePath);

            Image modalBlocker = root.GetComponent<Image>();
            if (modalBlocker != null)
            {
                modalBlocker.color = Color.clear;
                modalBlocker.raycastTarget = true;
            }

            PlayerMenuBackgroundController background = root.GetComponent<PlayerMenuBackgroundController>();
            if (background == null)
                background = root.AddComponent<PlayerMenuBackgroundController>();
            background.RespectAuthoredPresentation = true;
            background.ScanlinesAboveContent = false;
            background.ConfigureBaseTint(new Color(0.035f, 0.018f, 0.025f, 0.82f), blocksRaycasts: false, force: true);
            background.ConfigureScanlines(
                enabled: true,
                texture: scanlines,
                color: new Color(1f, 0.80f, 0.55f, 0.10f),
                tiling: Vector2.one,
                scrollDirection: Vector2.down,
                uvSpeed: 0.018f,
                force: true);

            Transform baseTint = root.transform.Find("BaseTint");
            Transform scanlineOverlay = root.transform.Find("ScanlineOverlay");
            if (baseTint != null) baseTint.SetSiblingIndex(0);
            if (scanlineOverlay != null)
            {
                scanlineOverlay.SetSiblingIndex(baseTint != null ? 1 : 0);
                RawImage scanlineImage = scanlineOverlay.GetComponent<RawImage>();
                if (scanlineImage != null)
                {
                    scanlineImage.raycastTarget = false;
                    scanlineImage.color = new Color(1f, 0.80f, 0.55f, 0.10f);
                }
            }

            Transform book = root.transform.Find("BookPage");
            StyleSlicedImage(book != null ? book.GetComponent<Image>() : null, frame, Color.white);
            if (book != null)
            {
                Image outlineImage = PlayerMenuUiFactory.CreateImage("DossierOutline", book, Color.white);
                StyleSlicedImage(outlineImage, outline, Color.white);
                PlayerMenuUiFactory.Stretch(outlineImage.rectTransform, 6f, 6f, 6f, 6f);
                outlineImage.transform.SetAsFirstSibling();
            }

            string[] panelNames =
            {
                "SummaryPage", "MovesPage", "LearnablePage", "StylePage", "GrowthPage",
                "FieldNotes", "AttributeRadar", "SpecimenData", "RadarPaper", "Viewport"
            };
            for (int i = 0; i < panelNames.Length; i++)
            {
                Transform target = FindDescendant(root.transform, panelNames[i]);
                StyleSlicedImage(target != null ? target.GetComponent<Image>() : null, panel, Color.white);
            }

            SerializedObject screenSo = new SerializedObject(screen);
            SetIfPresent(screenSo, "idleButtonSprite", idleButton);
            SetIfPresent(screenSo, "selectedButtonSprite", selectedButton);
            screenSo.ApplyModifiedPropertiesWithoutUndo();

            Transform tabs = root.transform.Find("BookPage/DossierTabs");
            if (tabs != null)
            {
                for (int i = 0; i < tabs.childCount; i++)
                    StyleButton(tabs.GetChild(i).GetComponent<Button>(), i == 0 ? selectedButton : idleButton);
            }

            Transform continueButton = root.transform.Find("BookPage/DossierFooter/ContinueButton");
            StyleButton(continueButton != null ? continueButton.GetComponent<Button>() : null, selectedButton);
        }

        private static void StyleButton(Button button, Sprite sprite)
        {
            if (button == null)
                return;
            StyleSlicedImage(button.targetGraphic as Image, sprite, Color.white);
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.90f, 1f);
            colors.selectedColor = new Color(1f, 0.96f, 0.90f, 1f);
            colors.pressedColor = new Color(0.88f, 0.72f, 0.56f, 1f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.72f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void StyleSlicedImage(Image image, Sprite sprite, Color color)
        {
            if (image == null)
                return;
            if (sprite != null)
                image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.pixelsPerUnitMultiplier = 1f;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == name)
                    return child;
                Transform nested = FindDescendant(child, name);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static PartyTabStarRatingArtSO EnsureStarRatingArt()
        {
            PartyTabStarRatingArtSO art = AssetDatabase.LoadAssetAtPath<PartyTabStarRatingArtSO>(StarRatingArtPath);
            if (art != null)
                return art;

            art = ScriptableObject.CreateInstance<PartyTabStarRatingArtSO>();
            AssetDatabase.CreateAsset(art, StarRatingArtPath);
            return art;
        }

        private static ChainlingPartyTypeChromePaletteSO EnsureTypeChromePalette()
        {
            EnsureFolder(DataDir, "PartyTab");

            ChainlingPartyTypeChromePaletteSO palette =
                AssetDatabase.LoadAssetAtPath<ChainlingPartyTypeChromePaletteSO>(TypeChromePalettePath);
            if (palette != null)
                return palette;

            palette = ScriptableObject.CreateInstance<ChainlingPartyTypeChromePaletteSO>();
            AssetDatabase.CreateAsset(palette, TypeChromePalettePath);
            return palette;
        }

        private static ChainlingPartyColumnShell SaveColumnShellPrefab(PartyTabStarRatingArtSO starRatingArt)
        {
            var go = new GameObject(
                "ChainlingPartyColumnShell",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.Button),
                typeof(CanvasGroup),
                typeof(ChainlingPartyColumnShell));

            RectTransform rectTransform = go.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(360f, 620f);

            ChainlingPartyColumnShell shell = go.GetComponent<ChainlingPartyColumnShell>();
            shell.BuildDefaultLayoutForEditor();
            WireStarRatingArtOnInfoPanels(go, starRatingArt);

            return SavePrefabAsset(go, ColumnShellPrefabPath).GetComponent<ChainlingPartyColumnShell>();
        }

        private static ChainlingsPartyScreen SavePartyPrefab(
            ChainlingPartyColumnShell columnShellPrefab,
            PartyTabMotionProfileSO motionProfile,
            PartyTabStarRatingArtSO starRatingArt,
            ChainlingPartyTypeChromePaletteSO typeChromePalette,
            TypeIconPaletteSO typeIconPalette)
        {
            var go = new GameObject(
                "ChainlingsPartyView",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(ChainlingsPartyScreen),
                typeof(ChainlingPartyMotionController));

            Stretch(go.GetComponent<RectTransform>());

            CanvasGroup canvasGroup = go.GetComponent<CanvasGroup>();
            ChainlingsPartyScreen screen = go.GetComponent<ChainlingsPartyScreen>();
            screen.ConfigureScreenIdentity("chainlings", "Chainlings", canvasGroup);
            SerializedObject screenSo = new SerializedObject(screen);
            WireChainlingDataReferences(screenSo);
            SetIfPresent(screenSo, "columnShellPrefab", columnShellPrefab);
            SetIfPresent(screenSo, "motionProfile", motionProfile);
            SetIfPresent(screenSo, "starRatingArt", starRatingArt);
            SetIfPresent(screenSo, "typeChromePalette", typeChromePalette);
            SetIfPresent(screenSo, "typeIconPalette", typeIconPalette);
            screenSo.ApplyModifiedPropertiesWithoutUndo();
            screen.BuildDefaultLayoutForEditor();
            WireStarRatingArtOnInfoPanels(go, starRatingArt);

            ChainlingPartyMotionController motionController = go.GetComponent<ChainlingPartyMotionController>();
            motionController.Configure(motionProfile);

            return SavePrefabAsset(go, PartyPrefabPath).GetComponent<ChainlingsPartyScreen>();
        }

        private static void WireStarRatingArtOnInfoPanels(GameObject root, PartyTabStarRatingArtSO starRatingArt)
        {
            if (root == null || starRatingArt == null)
                return;

            ChainlingPartyInfoPanelBinder[] binders = root.GetComponentsInChildren<ChainlingPartyInfoPanelBinder>(true);
            for (int i = 0; i < binders.Length; i++)
            {
                binders[i].ConfigureStarRatingArt(starRatingArt);
                binders[i].BuildDefaultLayoutForEditor();
            }
        }

        private static ChainlingsMenuViewsSO SaveViewsAsset(
            ChainlingsPartyScreen partyPrefab,
            ChainlingPartyColumnShell columnShellPrefab,
            ChainlingDetailScreen detailPrefab,
            ChainlingCardWidget cardPrefab)
        {
            ChainlingsMenuViewsSO views = LoadOrCreateViewsAsset();
            SerializedObject so = new SerializedObject(views);
            SetIfPresent(so, "partyViewPrefab", partyPrefab);
            SetIfPresent(so, "partyColumnShellPrefab", columnShellPrefab);
            SetIfPresent(so, "detailViewPrefab", detailPrefab);
            SetIfPresent(so, "cardViewPrefab", cardPrefab);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(views);
            return views;
        }

        private static void WireGameServicesBootstrap(ChainlingsMenuViewsSO views)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(GameServicesPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[ChainlingsMenuViewsCreator] Could not find GameServices prefab at {GameServicesPrefabPath}.");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
            PlayerMenuDevBootstrap bootstrap = instance.GetComponentInChildren<PlayerMenuDevBootstrap>(true);
            if (bootstrap == null)
            {
                PrefabUtility.UnloadPrefabContents(instance);
                Debug.LogWarning("[ChainlingsMenuViewsCreator] PlayerMenuDevBootstrap was not found on GameServices.");
                return;
            }

            SerializedObject so = new SerializedObject(bootstrap);
            SetIfPresent(so, "menuViews", views);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(instance, GameServicesPrefabPath);
            PrefabUtility.UnloadPrefabContents(instance);
        }

        private static ChainlingsMenuViewsSO LoadOrCreateViewsAsset()
        {
            ChainlingsMenuViewsSO views = AssetDatabase.LoadAssetAtPath<ChainlingsMenuViewsSO>(ViewsAssetPath);
            if (views != null)
                return views;

            views = ScriptableObject.CreateInstance<ChainlingsMenuViewsSO>();
            AssetDatabase.CreateAsset(views, ViewsAssetPath);
            return views;
        }

        private static GameObject SavePrefabAsset(GameObject source, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            return prefab;
        }

        private static void WireCaptureDossierPresenter(ChainlingDetailScreen detailPrefab)
        {
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(CombatBattleCanvasPrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogWarning($"[ChainlingsMenuViewsCreator] Combat prefab not found at {CombatBattleCanvasPrefabPath}.");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(CombatBattleCanvasPrefabPath);
            try
            {
                ChainlingCaptureDossierPresenter presenter = instance.GetComponent<ChainlingCaptureDossierPresenter>();
                if (presenter == null)
                    presenter = instance.AddComponent<ChainlingCaptureDossierPresenter>();

                Transform safeArea = instance.transform.Find("CombatHudCanvas/SafeAreaRoot");
                SerializedObject presenterSo = new SerializedObject(presenter);
                SetIfPresent(presenterSo, "dossierPrefab", detailPrefab);
                SetIfPresent(presenterSo, "overlayParent", safeArea != null ? safeArea.GetComponent<RectTransform>() : null);
                SetIfPresent(presenterSo, "supabaseConfig", AssetDatabase.LoadAssetAtPath<Object>(SupabaseConfigPath));
                presenterSo.ApplyModifiedPropertiesWithoutUndo();

                CaptureCommandCoordinatorUGUI coordinator = instance.GetComponent<CaptureCommandCoordinatorUGUI>();
                if (coordinator == null)
                    coordinator = instance.GetComponentInChildren<CaptureCommandCoordinatorUGUI>(true);
                if (coordinator != null)
                {
                    SerializedObject coordinatorSo = new SerializedObject(coordinator);
                    SetIfPresent(coordinatorSo, "captureDossierPresenterBehaviour", presenter);
                    coordinatorSo.ApplyModifiedPropertiesWithoutUndo();
                }
                else
                {
                    Debug.LogWarning("[ChainlingsMenuViewsCreator] CaptureCommandCoordinatorUGUI was not found on CombatBattleCanvas.");
                }

                PrefabUtility.SaveAsPrefabAsset(instance, CombatBattleCanvasPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static void WireChainlingDataReferences(SerializedObject so)
        {
            SetIfPresent(so, "supabaseConfig", AssetDatabase.LoadAssetAtPath<Object>(SupabaseConfigPath));
            SetIfPresent(so, "speciesDatabase", AssetDatabase.LoadAssetAtPath<Object>(SpeciesDatabasePath));
            SetIfPresent(so, "visualDatabase", AssetDatabase.LoadAssetAtPath<Object>(CombatVisualDatabasePath));
            SetIfPresent(so, "identityDatabase", AssetDatabase.LoadAssetAtPath<Object>(ChainlingVisualIdentityDatabasePath));
            SetIfPresent(so, "moveDatabase", AssetDatabase.LoadAssetAtPath<Object>(MoveDatabasePath));
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIfPresent(SerializedObject so, string propertyName, Object value)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (property != null)
                property.objectReferenceValue = value;
        }

        private static bool AssetExists(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Object>(path) != null;
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private const string CaptureSlideSoundPath = "Assets/Data/Audio/SFX/LIBRARY_CARD_SLIDE.asset";
        private const string IndexFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Apply Chainling Book Index", priority = 43)]
        public static void ApplyChainlingBookIndex()
        {
            ApplyChainlingBookIndexToPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[ChainlingsMenuViewsCreator] Applied the book-index layout to ChainlingDetailView.");
        }

        public static void ApplyChainlingBookIndexToPrefab()
        {
            GameObject instance = PrefabUtility.LoadPrefabContents(DetailPrefabPath);
            try
            {
                ApplyBookIndex(instance);
                PrefabUtility.SaveAsPrefabAsset(instance, DetailPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        private static void ApplyBookIndex(GameObject root)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(IndexFontPath);
            Sprite selectorSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Color idle = ChainlingIndexRow.IdleInk;
            Color selector = ChainlingIndexRow.SelectorTint;
            selector.a = 0f;

            Transform book = root.transform.Find("BookPage") ??
                             root.transform.Find(PageStackName + "/BookPage");
            if (book == null)
                return;

            VerticalLayoutGroup bookLayout = book.GetComponent<VerticalLayoutGroup>();
            if (bookLayout != null)
            {
                bookLayout.padding = new RectOffset(48, 48, 36, 18);
                bookLayout.spacing = 10f;
                bookLayout.childAlignment = TextAnchor.UpperCenter;
            }

            Transform header = book.Find("DossierHeader");
            if (header != null)
            {
                HorizontalLayoutGroup horizontal = header.GetComponent<HorizontalLayoutGroup>();
                if (horizontal != null)
                    Object.DestroyImmediate(horizontal);
                VerticalLayoutGroup headerLayout = header.GetComponent<VerticalLayoutGroup>();
                if (headerLayout == null)
                    headerLayout = header.gameObject.AddComponent<VerticalLayoutGroup>();
                headerLayout.spacing = 4f;
                headerLayout.childAlignment = TextAnchor.MiddleCenter;
                headerLayout.childControlWidth = true;
                headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = true;
                headerLayout.childForceExpandHeight = false;
                LayoutElement headerElement = header.GetComponent<LayoutElement>();
                if (headerElement != null)
                    headerElement.preferredHeight = 132f;

                Transform portrait = header.Find("PortraitBack");
                if (portrait != null)
                    portrait.gameObject.SetActive(false);
                Transform kicker = header.Find("HeaderText/Kicker");
                if (kicker != null)
                    kicker.gameObject.SetActive(false);
                StyleIndexText(header.Find("HeaderText/Name")?.GetComponent<TMP_Text>(), font, 34f, idle, TextAlignmentOptions.Center);
                StyleIndexText(header.Find("HeaderText/Meta")?.GetComponent<TMP_Text>(), font, 20f, idle, TextAlignmentOptions.Center);
            }

            Transform tabs = book.Find("DossierTabs");
            if (tabs != null)
            {
                HorizontalLayoutGroup horizontal = tabs.GetComponent<HorizontalLayoutGroup>();
                if (horizontal != null)
                    Object.DestroyImmediate(horizontal);
                VerticalLayoutGroup tabLayout = tabs.GetComponent<VerticalLayoutGroup>();
                if (tabLayout == null)
                    tabLayout = tabs.gameObject.AddComponent<VerticalLayoutGroup>();
                tabLayout.spacing = 12f;
                tabLayout.padding = new RectOffset(12, 12, 8, 8);
                tabLayout.childAlignment = TextAnchor.MiddleCenter;
                tabLayout.childControlWidth = true;
                tabLayout.childControlHeight = true;
                tabLayout.childForceExpandWidth = true;
                tabLayout.childForceExpandHeight = false;
                LayoutElement tabsElement = tabs.GetComponent<LayoutElement>();
                if (tabsElement != null)
                {
                    tabsElement.preferredHeight = 420f;
                    tabsElement.flexibleHeight = 1f;
                }

                string[] titles = { "SUMMARY", "MOVES", "LEARN", "STYLE", "GROWTH" };
                for (int i = 0; i < tabs.childCount && i < titles.Length; i++)
                    StyleIndexRow(tabs.GetChild(i), titles[i], (i + 1).ToString(), font, idle, selector, selectorSprite);
            }

            Transform pages = book.Find("DossierPages");
            Transform summaryPage = pages != null ? pages.Find("SummaryPage") : null;
            if (summaryPage == null && book.parent != null)
                summaryPage = book.parent.Find("SummaryPage");
            ApplySummaryMovesView(summaryPage);

            Transform footer = book.Find("DossierFooter") ?? root.transform.Find("DossierFooter");
            FlattenNestedFooter(footer);

            RectTransform pageStack = ApplyPageStack(root, book);
            footer = book.Find("DossierFooter") ?? root.transform.Find("DossierFooter");
            LiftFooterOffTheStack(root, pageStack, footer);
            ApplyBackdropOrder(root, pageStack);

            ChainlingDetailScreen screen = root.GetComponent<ChainlingDetailScreen>();
            if (screen != null)
            {
                SerializedObject screenSo = new SerializedObject(screen);
                SetIfPresent(screenSo, "captureSlideSound", AssetDatabase.LoadAssetAtPath<Object>(CaptureSlideSoundPath));
                SetIfPresent(screenSo, "bookPageRoot", pageStack);
                SetIfPresent(screenSo, "bookPageGroup", pageStack != null ? pageStack.GetComponent<CanvasGroup>() : null);
                SetIfPresent(screenSo, "sheetStackRoot", pageStack);
                SetIfPresent(screenSo, "panelRoot", pageStack);
                Transform indexPage = pageStack != null ? pageStack.Find("BookPage") : book;
                SetIfPresent(screenSo, "segmentRoot", indexPage != null ? indexPage.Find("DossierTabs") as RectTransform : null);
                footer = root.transform.Find("DossierFooter");
                Button continueButton = footer != null ? footer.Find("ContinueButton")?.GetComponent<Button>() : null;
                if (continueButton != null)
                    SetIfPresent(screenSo, "closeButton", continueButton);
                screenSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private const string PageStackName = "DossierPageStack";
        private static readonly string[] TabPageNames =
        {
            "SummaryPage", "MovesPage", "LearnablePage", "StylePage", "GrowthPage"
        };

        /// <summary>
        /// Each index tab is a full paper sheet stacked under the index (BookPage).
        /// Sheets stay visible at rest so the dossier reads as a pile of pages; only
        /// the front sheet's contents stay enabled at runtime.
        /// </summary>
        private static RectTransform ApplyPageStack(GameObject root, Transform book)
        {
            if (book == null)
                return null;

            RectTransform bookRect = (RectTransform)book;
            Image bookImage = book.GetComponent<Image>();

            RectTransform stack = root.transform.Find(PageStackName) as RectTransform;
            if (stack == null && book.parent == root.transform)
            {
                GameObject stackObject = new GameObject(PageStackName, typeof(RectTransform), typeof(CanvasGroup));
                stackObject.layer = book.gameObject.layer;
                stackObject.transform.SetParent(root.transform, false);
                stack = (RectTransform)stackObject.transform;
                stack.anchorMin = bookRect.anchorMin;
                stack.anchorMax = bookRect.anchorMax;
                stack.pivot = bookRect.pivot;
                stack.offsetMin = bookRect.offsetMin;
                stack.offsetMax = bookRect.offsetMax;
            }
            else if (stack == null && book.parent != null && book.parent.name == PageStackName)
            {
                stack = book.parent as RectTransform;
            }

            if (stack == null)
                return null;

            CanvasGroup stackGroup = stack.GetComponent<CanvasGroup>();
            if (stackGroup == null)
                stackGroup = stack.gameObject.AddComponent<CanvasGroup>();
            stackGroup.alpha = 1f;
            stackGroup.interactable = true;
            stackGroup.blocksRaycasts = true;

            Transform pages = book.Find("DossierPages") ?? stack.Find("DossierPages");
            for (int i = 0; i < TabPageNames.Length; i++)
            {
                Transform page = pages != null ? pages.Find(TabPageNames[i]) : null;
                if (page == null)
                    page = stack.Find(TabPageNames[i]);
                if (page == null)
                    continue;

                page.SetParent(stack, false);
                page.SetSiblingIndex(i);
                page.gameObject.SetActive(true);
                StyleStackedSheet(page, bookImage);
                Stretch((RectTransform)page);
            }

            if (pages != null && pages.parent == book)
                pages.gameObject.SetActive(false);

            if (book.parent != stack)
                book.SetParent(stack, false);
            book.SetAsLastSibling();
            Stretch(bookRect);
            StyleStackedSheet(book, bookImage);

            Transform leftover = root.transform.Find("DossierSheetStack");
            if (leftover != null && leftover != stack)
                Object.DestroyImmediate(leftover.gameObject);

            stack.gameObject.SetActive(true);
            return stack;
        }

        private static void StyleStackedSheet(Transform sheet, Image paperSource)
        {
            if (sheet == null)
                return;

            Image paper = sheet.GetComponent<Image>();
            if (paper == null)
                paper = sheet.gameObject.AddComponent<Image>();
            if (paperSource != null)
            {
                paper.sprite = paperSource.sprite;
                paper.type = paperSource.type;
                paper.color = paperSource.color;
                paper.pixelsPerUnitMultiplier = paperSource.pixelsPerUnitMultiplier;
                paper.preserveAspect = paperSource.preserveAspect;
            }
            paper.raycastTarget = sheet.name == "BookPage";

            Shadow shadow = sheet.GetComponent<Shadow>();
            if (shadow == null)
                shadow = sheet.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.28f);
            shadow.effectDistance = new Vector2(5f, -7f);
        }

        private static void LiftFooterOffTheStack(GameObject root, RectTransform stack, Transform footer)
        {
            if (root == null || footer == null)
                return;

            RectTransform footerRect = (RectTransform)footer;
            footer.SetParent(root.transform, false);
            if (stack != null)
            {
                footerRect.anchorMin = new Vector2(stack.anchorMin.x, stack.anchorMin.y);
                footerRect.anchorMax = new Vector2(stack.anchorMax.x, stack.anchorMin.y);
                footerRect.pivot = new Vector2(0.5f, 0f);
                footerRect.anchoredPosition = Vector2.zero;
                footerRect.sizeDelta = new Vector2(0f, 68f);
                footerRect.offsetMin = new Vector2(stack.offsetMin.x, stack.offsetMin.y);
                footerRect.offsetMax = new Vector2(stack.offsetMax.x, stack.offsetMin.y + 68f);
            }
        }

        /// <summary>
        /// Root draw order: BaseTint, ScanlineOverlay, page stack, footer. Scanlines
        /// stay behind the papers so they never hide the book.
        /// </summary>
        private static void ApplyBackdropOrder(GameObject root, RectTransform pageStack)
        {
            PlayerMenuBackgroundController background = root.GetComponent<PlayerMenuBackgroundController>();
            if (background != null)
                background.ScanlinesAboveContent = false;

            Transform baseTint = root.transform.Find("BaseTint");
            Transform scanlineOverlay = root.transform.Find("ScanlineOverlay");
            Transform footer = root.transform.Find("DossierFooter");
            int index = 0;
            if (baseTint != null) baseTint.SetSiblingIndex(index++);
            if (scanlineOverlay != null)
            {
                scanlineOverlay.SetSiblingIndex(index++);
                RawImage scanlineImage = scanlineOverlay.GetComponent<RawImage>();
                if (scanlineImage != null)
                {
                    scanlineImage.raycastTarget = false;
                    scanlineImage.color = new Color(1f, 0.80f, 0.55f, 0.10f);
                }
            }

            if (pageStack != null) pageStack.SetSiblingIndex(index++);
            if (footer != null) footer.SetSiblingIndex(index);
        }

        /// <summary>
        /// Repairs a footer that ended up as DossierFooter/DossierFooter/ContinueButton
        /// (seen after a merge) so the button sits directly under the footer again.
        /// </summary>
        private static void FlattenNestedFooter(Transform footer)
        {
            if (footer == null)
                return;

            Transform nested = footer.Find("DossierFooter");
            while (nested != null)
            {
                for (int i = nested.childCount - 1; i >= 0; i--)
                    nested.GetChild(i).SetParent(footer, false);
                Object.DestroyImmediate(nested.gameObject);
                nested = footer.Find("DossierFooter");
            }
        }

        private const string SummaryMovesViewName = "MovesList";

        /// <summary>
        /// Gives the SUMMARY page the same move rows the Chainlings tab uses: Column_1's
        /// authored MovesView is cloned (rows, viewport, palettes, row sprites) under
        /// SpecimenData and bound through <see cref="ChainlingOverviewPanel"/>.
        /// </summary>
        private static void ApplySummaryMovesView(Transform summaryPage)
        {
            if (summaryPage == null)
                return;

            ChainlingOverviewPanel overview = summaryPage.GetComponent<ChainlingOverviewPanel>();
            Transform specimen = summaryPage.Find("SpecimenData");
            if (overview == null || specimen == null)
                return;

            Transform existing = specimen.Find(SummaryMovesViewName);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject partyRoot = PrefabUtility.LoadPrefabContents(PartyPrefabPath);
            GameObject clone = null;
            try
            {
                ChainlingPartyMovesViewLayout source = FindColumnOneMovesView(partyRoot);
                if (source == null)
                {
                    Debug.LogWarning("[ChainlingsMenuViewsCreator] Column_1 MovesView not found in ChainlingsPartyView; SUMMARY keeps the text move list.");
                    return;
                }

                clone = Object.Instantiate(source.gameObject, specimen, false);
                clone.name = SummaryMovesViewName;
                clone.SetActive(true);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(partyRoot);
            }

            // The party column drives this view through its InfoPanelBinder; the dossier
            // binds it directly, so drop the column-only chrome.
            ChainlingPartyInfoViewLayout infoLayout = clone.GetComponent<ChainlingPartyInfoViewLayout>();
            if (infoLayout != null)
                Object.DestroyImmediate(infoLayout);
            Transform lines = clone.transform.Find("Lines");
            if (lines != null)
                Object.DestroyImmediate(lines.gameObject);

            RectTransform cloneRect = (RectTransform)clone.transform;
            cloneRect.anchorMin = new Vector2(0f, 0f);
            cloneRect.anchorMax = new Vector2(1f, 1f);
            cloneRect.offsetMin = Vector2.zero;
            cloneRect.offsetMax = Vector2.zero;
            cloneRect.localScale = Vector3.one;
            LayoutElement cloneElement = clone.GetComponent<LayoutElement>();
            if (cloneElement == null)
                cloneElement = clone.AddComponent<LayoutElement>();
            cloneElement.preferredHeight = 290f;
            cloneElement.flexibleHeight = 0f;

            ChainlingPartyMovesViewLayout movesView = clone.GetComponent<ChainlingPartyMovesViewLayout>();
            if (movesView != null)
            {
                SerializedObject viewSo = new SerializedObject(movesView);
                RectTransform viewport = clone.transform.Find("MoveRowsViewport") as RectTransform;
                RectTransform rows = viewport != null ? viewport.Find("MoveRows") as RectTransform : null;
                TMP_Text slotCount = clone.transform.Find("SlotCountText")?.GetComponent<TMP_Text>();
                if (slotCount != null)
                {
                    slotCount.text = "DEFAULT MOVE LIST";
                    RectTransform slotRect = slotCount.rectTransform;
                    slotRect.anchorMin = new Vector2(0f, 1f);
                    slotRect.anchorMax = new Vector2(1f, 1f);
                    slotRect.pivot = new Vector2(0.5f, 1f);
                    slotRect.anchoredPosition = Vector2.zero;
                    slotRect.sizeDelta = new Vector2(0f, 26f);
                }
                if (viewport != null)
                {
                    viewport.anchorMin = new Vector2(0f, 0f);
                    viewport.anchorMax = new Vector2(1f, 1f);
                    viewport.pivot = new Vector2(0.5f, 1f);
                    viewport.offsetMin = new Vector2(0f, 0f);
                    viewport.offsetMax = new Vector2(0f, -30f);
                }

                SetIfPresent(viewSo, "viewportRoot", viewport);
                SetIfPresent(viewSo, "moveRowsRoot", rows);
                SetIfPresent(viewSo, "slotCountText", slotCount);
                SetIfPresent(viewSo, "scrollRect", viewport != null ? viewport.GetComponent<ScrollRect>() : null);
                SetIfPresent(viewSo, "viewportLayoutElement", viewport != null ? viewport.GetComponent<LayoutElement>() : null);
                SerializedProperty formatProperty = viewSo.FindProperty("slotCountFormat");
                if (formatProperty != null)
                    formatProperty.stringValue = "MOVES {0}/{1}";
                SerializedProperty authored = viewSo.FindProperty("authoredMoveRows");
                if (authored != null && rows != null)
                {
                    ChainlingPartyMoveRowDisplay[] rowDisplays = rows.GetComponentsInChildren<ChainlingPartyMoveRowDisplay>(true);
                    System.Array.Sort(rowDisplays, (a, b) => string.CompareOrdinal(a.name, b.name));
                    authored.arraySize = rowDisplays.Length;
                    for (int i = 0; i < rowDisplays.Length; i++)
                        authored.GetArrayElementAtIndex(i).objectReferenceValue = rowDisplays[i];
                }
                viewSo.ApplyModifiedPropertiesWithoutUndo();
            }

            Transform defaultMoves = specimen.Find("DefaultMoves");
            if (defaultMoves != null)
                defaultMoves.gameObject.SetActive(false);

            SerializedObject overviewSo = new SerializedObject(overview);
            SetIfPresent(overviewSo, "movesView", movesView);
            overviewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ChainlingPartyMovesViewLayout FindColumnOneMovesView(GameObject partyRoot)
        {
            if (partyRoot == null)
                return null;

            ChainlingPartyMovesViewLayout[] views = partyRoot.GetComponentsInChildren<ChainlingPartyMovesViewLayout>(true);
            ChainlingPartyMovesViewLayout fallback = null;
            for (int i = 0; i < views.Length; i++)
            {
                ChainlingPartyMovesViewLayout view = views[i];
                if (view == null)
                    continue;
                fallback ??= view;
                for (Transform cursor = view.transform; cursor != null; cursor = cursor.parent)
                {
                    if (cursor.name == "Column_1")
                        return view;
                }
            }

            return fallback;
        }

        private static void StyleIndexRow(
            Transform row,
            string title,
            string pageNumber,
            TMP_FontAsset font,
            Color ink,
            Color selectorColor,
            Sprite selectorSprite)
        {
            if (row == null)
                return;

            MenuSelectableFocusVisual focus = row.GetComponent<MenuSelectableFocusVisual>();
            if (focus != null)
                Object.DestroyImmediate(focus);
            Transform focusFrame = row.Find("FocusFrame");
            if (focusFrame != null)
                Object.DestroyImmediate(focusFrame.gameObject);

            Image hit = row.GetComponent<Image>();
            if (hit != null)
            {
                hit.sprite = null;
                hit.color = Color.clear;
                hit.raycastTarget = true;
            }

            Button button = row.GetComponent<Button>();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
                button.targetGraphic = hit;
            }

            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null)
                rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = 64f;
            rowLayout.minHeight = 64f;
            rowLayout.flexibleWidth = 1f;

            Image selector = row.Find("Selector")?.GetComponent<Image>();
            if (selector == null)
            {
                GameObject selectorObject = new GameObject("Selector", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                selectorObject.transform.SetParent(row, false);
                selectorObject.transform.SetAsFirstSibling();
                selector = selectorObject.GetComponent<Image>();
            }

            selector.sprite = selectorSprite;
            selector.type = Image.Type.Sliced;
            selector.color = selectorColor;
            selector.raycastTarget = false;
            LayoutElement selectorLayout = selector.GetComponent<LayoutElement>();
            if (selectorLayout == null)
                selectorLayout = selector.gameObject.AddComponent<LayoutElement>();
            selectorLayout.ignoreLayout = true;
            RectTransform selectorRect = selector.rectTransform;
            selectorRect.anchorMin = Vector2.zero;
            selectorRect.anchorMax = Vector2.one;
            selectorRect.offsetMin = new Vector2(8f, 2f);
            selectorRect.offsetMax = new Vector2(-8f, -2f);

            Transform existingLabel = row.Find("Label");
            if (existingLabel != null && row.Find("Title") == null)
                existingLabel.name = "Title";

            TMP_Text titleText = EnsureRowText(row, "Title", title, font, 22f, ink, TextAlignmentOptions.MidlineLeft);
            TMP_Text leaderText = EnsureRowText(row, "Leader", new string('.', 48), font, 22f, ink, TextAlignmentOptions.MidlineLeft);
            TMP_Text numberText = EnsureRowText(row, "PageNumber", pageNumber, font, 22f, ink, TextAlignmentOptions.MidlineRight);
            leaderText.overflowMode = TextOverflowModes.Ellipsis;
            leaderText.textWrappingMode = TextWrappingModes.NoWrap;

            LayoutElement titleElement = titleText.gameObject.GetComponent<LayoutElement>() ?? titleText.gameObject.AddComponent<LayoutElement>();
            titleElement.preferredWidth = 168f;
            titleElement.flexibleWidth = 0f;
            LayoutElement leaderElement = leaderText.gameObject.GetComponent<LayoutElement>() ?? leaderText.gameObject.AddComponent<LayoutElement>();
            leaderElement.preferredWidth = 80f;
            leaderElement.flexibleWidth = 1f;
            LayoutElement numberElement = numberText.gameObject.GetComponent<LayoutElement>() ?? numberText.gameObject.AddComponent<LayoutElement>();
            numberElement.preferredWidth = 36f;
            numberElement.flexibleWidth = 0f;

            HorizontalLayoutGroup rowGroup = row.GetComponent<HorizontalLayoutGroup>();
            if (rowGroup == null)
                rowGroup = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            rowGroup.padding = new RectOffset(28, 28, 8, 8);
            rowGroup.spacing = 10f;
            rowGroup.childAlignment = TextAnchor.MiddleCenter;
            rowGroup.childControlWidth = true;
            rowGroup.childControlHeight = true;
            rowGroup.childForceExpandWidth = false;
            rowGroup.childForceExpandHeight = false;

            ChainlingIndexRow indexRow = row.GetComponent<ChainlingIndexRow>();
            if (indexRow == null)
                indexRow = row.gameObject.AddComponent<ChainlingIndexRow>();
            SerializedObject rowSo = new SerializedObject(indexRow);
            SerializedProperty selectorProperty = rowSo.FindProperty("selector");
            if (selectorProperty != null)
                selectorProperty.objectReferenceValue = selector;
            rowSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static TMP_Text EnsureRowText(
            Transform row,
            string name,
            string value,
            TMP_FontAsset font,
            float size,
            Color color,
            TextAlignmentOptions alignment)
        {
            Transform existing = row.Find(name);
            TMP_Text text = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(row, false);
                text = textObject.GetComponent<TMP_Text>();
            }

            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            if (font != null)
                text.font = font;
            return text;
        }

        private static void StyleIndexText(TMP_Text text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            if (text == null)
                return;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            if (font != null)
                text.font = font;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
#endif
