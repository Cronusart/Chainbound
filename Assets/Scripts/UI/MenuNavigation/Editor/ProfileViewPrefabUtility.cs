#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SignalJam.UI.MenuNavigation.Editor
{
    public static class ProfileViewPrefabUtility
    {
        private const string ProfilePrefabPath = "Assets/Prefabs/UI/PlayerMenu/ProfileView.prefab";
        private const string PlayerMenuRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab";
        private const string SandboxRootPrefabPath = "Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot_ChainlingsSandbox.prefab";
        private const string ProfileTabAssetPath = "Assets/Data/UI/PlayerMenuTab_profile.asset";
        private const string ShareCopyAssetPath = "Assets/Data/UI/ProfileShareCopy.asset";
        private const string SupabaseConfigPath = "Assets/Data/Supabase/SupabaseConfig.asset";
        private const string DogSpritePath = "Assets/Sprites/player/BONKY_D2.png";

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Ensure Profile View Prefab", priority = 61)]
        public static void EnsureProfileViewPrefab()
        {
            EnsureProfileViewPrefab(syncRoots: false);
        }

        [MenuItem("Tools/Chainbound/UI/Player Menu Dev/Sync Profile View Into Player Menu Roots", priority = 62)]
        public static void SyncProfileViewIntoPlayerMenuRoots()
        {
            EnsureProfileViewPrefab(syncRoots: true);
        }

        private static void EnsureProfileViewPrefab(bool syncRoots)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogWarning("[ProfileViewPrefabUtility] Aborted because Prefab Mode is open.");
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog(
                        "Close Prefab Mode",
                        "Close Prefab Mode before running the Profile View prefab utility.",
                        "OK");
                }

                return;
            }

            EnsureFolder("Assets/Prefabs/UI", "PlayerMenu");
            EnsureFolder("Assets/Data", "UI");
            try
            {
                EditorUtility.DisplayProgressBar("Profile View", "Ensuring share copy and ProfileView prefab...", 0.25f);
                ProfileShareCopySO shareCopy = EnsureShareCopyAsset();
                ProfileMenuScreen screenPrefab = EnsureProfilePrefabAsset(shareCopy);
                if (screenPrefab == null)
                    return;

                EditorUtility.DisplayProgressBar("Profile View", "Wiring profile tab asset...", 0.55f);
                WireProfileTabAsset(screenPrefab);

                if (syncRoots)
                {
                    EditorUtility.DisplayProgressBar("Profile View", "Syncing PlayerMenuRoot prefabs...", 0.8f);
                    SyncRootIfPresent(PlayerMenuRootPrefabPath);
                    SyncRootIfPresent(SandboxRootPrefabPath);
                }

                AssetDatabase.SaveAssets();
                Debug.Log(syncRoots
                    ? "[ProfileViewPrefabUtility] Ensured ProfileView prefab, tab wiring, and PlayerMenuRoot sync."
                    : "[ProfileViewPrefabUtility] Ensured ProfileView prefab and tab wiring. Run Sync Profile View Into Player Menu Roots to embed it.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static ProfileShareCopySO EnsureShareCopyAsset()
        {
            ProfileShareCopySO copy = AssetDatabase.LoadAssetAtPath<ProfileShareCopySO>(ShareCopyAssetPath);
            if (copy == null)
            {
                copy = ScriptableObject.CreateInstance<ProfileShareCopySO>();
                AssetDatabase.CreateAsset(copy, ShareCopyAssetPath);
            }

            SerializedObject so = new SerializedObject(copy);
            SerializedProperty template = so.FindProperty("template");
            if (template != null && string.IsNullOrWhiteSpace(template.stringValue))
                template.stringValue = ProfileCardFormatter.DefaultShareTemplate;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(copy);
            return copy;
        }

        private static ProfileMenuScreen EnsureProfilePrefabAsset(ProfileShareCopySO shareCopy)
        {
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProfilePrefabPath);
            if (prefabAsset == null)
                return CreateProfilePrefab(shareCopy);

            GameObject instance = PrefabUtility.LoadPrefabContents(ProfilePrefabPath);
            try
            {
                BuildOrRepairLayout(instance, shareCopy);
                PrefabUtility.SaveAsPrefabAsset(instance, ProfilePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(ProfilePrefabPath);
            return saved != null ? saved.GetComponent<ProfileMenuScreen>() : null;
        }

        private static ProfileMenuScreen CreateProfilePrefab(ProfileShareCopySO shareCopy)
        {
            GameObject root = new GameObject("ProfileView", typeof(RectTransform), typeof(CanvasGroup), typeof(ProfileMenuScreen), typeof(ProfileCardShareService));
            try
            {
                BuildOrRepairLayout(root, shareCopy);
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ProfilePrefabPath);
                return saved != null ? saved.GetComponent<ProfileMenuScreen>() : null;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildOrRepairLayout(GameObject root, ProfileShareCopySO shareCopy)
        {
            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(root);
            ProfileMenuScreen screen = GetOrAdd<ProfileMenuScreen>(root);
            ProfileCardShareService shareService = GetOrAdd<ProfileCardShareService>(root);
            screen.SetScreenIdentity(ProfileMenuScreen.ProfileScreenId, "Profile", canvasGroup);
            Stretch(root.transform as RectTransform);

            RectTransform card = EnsurePanel("ProfileCardRoot", root.transform, PlayerMenuUiFactory.Panel);
            PlayerMenuUiFactory.Stretch(card, 24f, 88f, 24f, 24f);
            AspectRatioFitter fitter = GetOrAdd<AspectRatioFitter>(card.gameObject);
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 16f / 9f;

            RectTransform left = EnsureRect(card, "LeftStage");
            SetAnchors(left, new Vector2(0f, 0f), new Vector2(0.45f, 1f), 20f, 20f, 8f, 20f);

            Image dog = EnsureImage(left, "DogImage", Color.white);
            PlayerMenuUiFactory.Stretch(dog.rectTransform, 12f, 12f, 12f, 12f);
            dog.preserveAspect = true;
            Sprite dogSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DogSpritePath);
            if (dogSprite != null)
                dog.sprite = dogSprite;

            RectTransform hat = EnsureRect(left, "HatSlot");
            SetAnchors(hat, new Vector2(0.25f, 0.72f), new Vector2(0.75f, 0.96f), 0f, 0f, 0f, 0f);
            hat.gameObject.SetActive(false);

            RectTransform outfit = EnsureRect(left, "OutfitSlot");
            SetAnchors(outfit, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.42f), 0f, 0f, 0f, 0f);
            outfit.gameObject.SetActive(false);

            RectTransform right = EnsureRect(card, "RightIdentity");
            SetAnchors(right, new Vector2(0.45f, 0f), new Vector2(1f, 1f), 8f, 24f, 28f, 24f);
            VerticalLayoutGroup column = PlayerMenuUiFactory.VerticalLayout(right.gameObject, 10f);
            column.childAlignment = TextAnchor.MiddleLeft;
            column.padding = new RectOffset(8, 8, 12, 12);
            column.childControlHeight = true;
            column.childForceExpandHeight = false;

            TMP_Text nameLabel = EnsureLabel(right, "NameLabel", "$PLAYER", 42f, FontStyles.Bold);
            TMP_Text chipLabel = EnsureLabel(right, "ChipLabel", "GUEST", 20f, FontStyles.Bold);
            chipLabel.color = PlayerMenuUiFactory.Muted;
            TMP_Text titleLabel = EnsureLabel(right, "TitleLabel", "New Signal", 26f, FontStyles.Normal);
            titleLabel.color = PlayerMenuUiFactory.Accent;
            TMP_Text uniqueLabel = EnsureLabel(right, "UniqueSpeciesLabel", "0 species", 22f, FontStyles.Normal);
            TMP_Text capturesLabel = EnsureLabel(right, "CapturesLabel", "0 captured   0 entangled", 22f, FontStyles.Normal);
            TMP_Text inscriptionsLabel = EnsureLabel(right, "StampedMovesLabel", "0 stamped", 22f, FontStyles.Normal);
            TMP_Text recordLabel = EnsureLabel(right, "RecordLabel", "", 22f, FontStyles.Normal);
            recordLabel.gameObject.SetActive(false);

            TMP_Text hint = EnsureHint(root.transform);
            Button shareButton = EnsureShareButton(root.transform);

            SerializedObject serializedScreen = new SerializedObject(screen);
            SetObject(serializedScreen, "canvasGroup", canvasGroup);
            SetObject(serializedScreen, "defaultSelection", shareButton);
            SetObject(serializedScreen, "supabaseConfig", AssetDatabase.LoadAssetAtPath<Object>(SupabaseConfigPath));
            SetObject(serializedScreen, "shareCopy", shareCopy);
            SetObject(serializedScreen, "shareService", shareService);
            SetObject(serializedScreen, "profileCardRoot", card);
            SetObject(serializedScreen, "dogImage", dog);
            SetObject(serializedScreen, "dogSprite", dogSprite);
            SetObject(serializedScreen, "hatSlot", hat.gameObject);
            SetObject(serializedScreen, "outfitSlot", outfit.gameObject);
            SetObject(serializedScreen, "nameLabel", nameLabel);
            SetObject(serializedScreen, "linkChipLabel", chipLabel);
            SerializedProperty baseTitle = serializedScreen.FindProperty("titleLabel");
            if (baseTitle != null)
                baseTitle.objectReferenceValue = null;
            SetObject(serializedScreen, "cardTitleLabel", titleLabel);
            SetObject(serializedScreen, "uniqueSpeciesLabel", uniqueLabel);
            SetObject(serializedScreen, "capturesLabel", capturesLabel);
            SetObject(serializedScreen, "inscriptionsLabel", inscriptionsLabel);
            SetObject(serializedScreen, "recordLabel", recordLabel);
            SetObject(serializedScreen, "cosmeticHintLabel", hint);
            SetObject(serializedScreen, "shareButton", shareButton);
            SerializedProperty title = serializedScreen.FindProperty("defaultTitle");
            if (title != null)
                title.stringValue = "New Signal";
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedShare = new SerializedObject(shareService);
            SetObject(serializedShare, "cardRoot", card);
            SetObject(serializedShare, "shareButton", shareButton);
            SetObject(serializedShare, "shareCopy", shareCopy);
            SerializedProperty width = serializedShare.FindProperty("captureWidth");
            if (width != null)
                width.intValue = ProfileCardShareService.CaptureWidth;
            SerializedProperty height = serializedShare.FindProperty("captureHeight");
            if (height != null)
                height.intValue = ProfileCardShareService.CaptureHeight;
            SerializedProperty hide = serializedShare.FindProperty("hideDuringCapture");
            if (hide != null)
            {
                hide.arraySize = 2;
                hide.GetArrayElementAtIndex(0).objectReferenceValue = shareButton.gameObject;
                hide.GetArrayElementAtIndex(1).objectReferenceValue = hint.gameObject;
            }

            serializedShare.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(screen);
            EditorUtility.SetDirty(shareService);
        }

        private static TMP_Text EnsureHint(Transform parent)
        {
            TMP_Text hint = EnsureWorldLabel(parent, "CosmeticHintLabel", "Cosmetic slots unlock with Favor", 16f);
            RectTransform rt = hint.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0.45f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(24f, -40f);
            rt.offsetMax = new Vector2(0f, -8f);
            hint.color = PlayerMenuUiFactory.Muted;
            hint.alignment = TextAlignmentOptions.TopLeft;
            return hint;
        }

        private static Button EnsureShareButton(Transform parent)
        {
            Button button = parent.Find("ShareButton") != null
                ? parent.Find("ShareButton").GetComponent<Button>()
                : PlayerMenuUiFactory.CreateButton("ShareButton", parent, "SHARE", PlayerMenuUiFactory.Accent);
            button.name = "ShareButton";
            RectTransform rt = button.transform as RectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(200f, 64f);
            rt.anchoredPosition = new Vector2(-24f, 16f);
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            return button;
        }

        private static TMP_Text EnsureLabel(Transform parent, string name, string value, float size, FontStyles style)
        {
            TMP_Text text = EnsureWorldLabel(parent, name, value, size);
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.enableWordWrapping = false;
            PlayerMenuUiFactory.LayoutElement(text.gameObject, preferredHeight: size + 8f, flexibleWidth: 1f);
            return text;
        }

        private static TMP_Text EnsureWorldLabel(Transform parent, string name, string value, float size)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                TMP_Text existingText = existing.GetComponent<TMP_Text>();
                if (existingText != null)
                {
                    existingText.text = value;
                    existingText.fontSize = size;
                    return existingText;
                }
            }

            return PlayerMenuUiFactory.CreateText(name, parent, value, size, TextAlignmentOptions.Left, false);
        }

        private static RectTransform EnsurePanel(string name, Transform parent, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing is RectTransform existingRect)
            {
                Image image = existing.GetComponent<Image>();
                if (image == null)
                    image = existing.gameObject.AddComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                return existingRect;
            }

            return PlayerMenuUiFactory.CreateImage(name, parent, color).rectTransform;
        }

        private static Image EnsureImage(Transform parent, string name, Color color)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Image image = existing.GetComponent<Image>();
                if (image != null)
                    return image;
            }

            return PlayerMenuUiFactory.CreateImage(name, parent, color);
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

        private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, float left, float bottom, float right, float top)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            rt.localScale = Vector3.one;
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

        private static void WireProfileTabAsset(ProfileMenuScreen screenPrefab)
        {
            MenuTabDefinitionSO tab = AssetDatabase.LoadAssetAtPath<MenuTabDefinitionSO>(ProfileTabAssetPath);
            if (tab == null)
            {
                tab = ScriptableObject.CreateInstance<MenuTabDefinitionSO>();
                AssetDatabase.CreateAsset(tab, ProfileTabAssetPath);
            }

            SerializedObject serializedTab = new SerializedObject(tab);
            SetString(serializedTab, "tabId", "profile");
            SetString(serializedTab, "displayName", "Profile");
            SetString(serializedTab, "description", "Player ID card and share.");
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

        private static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null && value != null)
                property.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject serializedObject, string propertyName, string value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.stringValue = value;
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
}
#endif
