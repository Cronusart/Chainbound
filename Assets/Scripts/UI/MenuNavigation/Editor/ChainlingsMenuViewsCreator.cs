#if UNITY_EDITOR
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEditor;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class ChainlingsMenuViewsCreator
    {
        private const string DataDir = "Assets/Data/UI";
        private const string PartyTabDataDir = DataDir + "/PartyTab";
        private const string PrefabDir = "Assets/Prefabs/UI/PlayerMenu";
        private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
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

            return SavePrefabAsset(go, DetailPrefabPath).GetComponent<ChainlingDetailScreen>();
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
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            Object.DestroyImmediate(source);
            return prefab;
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
