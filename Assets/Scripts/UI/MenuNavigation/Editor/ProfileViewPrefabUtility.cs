#if UNITY_EDITOR
using System;
using System.Collections.Generic;
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
        private const string PlayerRootPath = "Assets/Prefabs/Player/PlayerRoot.prefab";
        private const string BlankPanelPath = "Assets/Sprites/UI/PlayerMenu/9sliceborder_blank.png";
        private const string FocusRingPath = "Assets/Sprites/UI/PlayerMenu/9sliceborder_orange_smol.png";
        private const string GrayButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png";
        private const string OrangeButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png";
        private const string ShimmerFrameFolder = "Assets/Sprites/UI/MainMenu/Buttons/Orange_Button_Anim";
        private const string MotionPath = "Assets/Scripts/UI/UiMotionProfile.asset";
        private const string PixeloidFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";

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
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            CanvasGroup canvasGroup = GetOrAdd<CanvasGroup>(root);
            ProfileMenuScreen screen = GetOrAdd<ProfileMenuScreen>(root);
            ProfileCardShareService shareService = GetOrAdd<ProfileCardShareService>(root);
            screen.SetScreenIdentity(ProfileMenuScreen.ProfileScreenId, "Profile", canvasGroup);
            Stretch(root.transform as RectTransform);

            RectTransform sheet = EnsureRect(root.transform, "ProfileSheet");
            Stretch(sheet);
            Image fill = GetOrAdd<Image>(sheet.gameObject);
            fill.color = new Color(0.02f, 0.045f, 0.07f, 0.94f);
            fill.raycastTarget = false;

            RectTransform topLeft = CreateSlot(sheet, "TopLeft", new Vector2(0.019f, 0.438f), new Vector2(0.316f, 0.951f));
            RectTransform topCenter = CreateSlot(sheet, "TopCenter", new Vector2(0.334f, 0.438f), new Vector2(0.666f, 0.868f));
            RectTransform topRight = CreateSlot(sheet, "TopRight", new Vector2(0.684f, 0.438f), new Vector2(0.981f, 0.951f));
            RectTransform lower = CreateSlot(sheet, "DialogueLowerCenter", new Vector2(0.206f, 0.014f), new Vector2(0.794f, 0.382f));

            Image dog = EnsureImage(topLeft, "DogImage", Color.white);
            PlayerMenuUiFactory.Stretch(dog.rectTransform, 28f, 48f, 28f, 28f);
            dog.preserveAspect = true;
            Sprite dogSprite = AssetDatabase.LoadAssetAtPath<Sprite>(DogSpritePath);
            if (dogSprite != null)
                dog.sprite = dogSprite;

            Image hat = EnsureImage(topLeft, "HatSlot", Color.white);
            SetAnchors(hat.rectTransform, new Vector2(0.28f, 0.62f), new Vector2(0.72f, 0.9f), 0f, 0f, 0f, 0f);
            hat.preserveAspect = true;
            hat.gameObject.SetActive(false);
            Image outfit = EnsureImage(topLeft, "OutfitSlot", Color.white);
            SetAnchors(outfit.rectTransform, new Vector2(0.22f, 0.16f), new Vector2(0.78f, 0.58f), 0f, 0f, 0f, 0f);
            outfit.preserveAspect = true;
            outfit.gameObject.SetActive(false);

            TMP_Text hint = CreateCaption(topLeft, "CosmeticHintLabel", "COSMETICS UNLOCK WITH FAVOR", 14f, TextAlignmentOptions.Bottom);
            PlayerMenuUiFactory.Bottom(hint.rectTransform, 24f, 16f, 16f, 8f);
            hint.color = PlayerMenuUiFactory.Muted;

            ProfilePortraitTurntable turntable = GetOrAdd<ProfilePortraitTurntable>(topLeft.gameObject);
            CopyIdleSprites(turntable, dog, hat, outfit);

            VerticalLayoutGroup centerLayout = Column(topCenter, 6f);
            TMP_Text nameLabel = CreateCaption(topCenter, "NameLabel", "$PLAYER", 28f, TextAlignmentOptions.Left);
            nameLabel.fontStyle = FontStyles.Bold;
            PlayerMenuUiFactory.LayoutElement(nameLabel.gameObject, preferredHeight: 36f, flexibleWidth: 1f);
            TMP_Text chipLabel = CreateCaption(topCenter, "ChipLabel", "GUEST", 16f, TextAlignmentOptions.Left);
            chipLabel.color = PlayerMenuUiFactory.Muted;
            PlayerMenuUiFactory.LayoutElement(chipLabel.gameObject, preferredHeight: 22f, flexibleWidth: 1f);

            TMP_Text uniqueSpecies = CreateCareerRow(screen, topCenter, "UniqueSpecies", "UNIQUE SPECIES", "DISTINCT SPECIES ON THE ROSTER.");
            Button capturesButton = null;
            TMP_Text captures = CreateCareerRow(screen, topCenter, "Captures", "CAPTURES", "LIFETIME CAPTURES. OPEN THE RECORD.", out capturesButton);
            TMP_Text entangled = CreateCareerRow(screen, topCenter, "Entangled", "ENTANGLED", "CAPTURES THAT ENTANGLED.");
            TMP_Text wins = CreateCareerRow(screen, topCenter, "Wins", "WINS", "BATTLES WON.");
            TMP_Text losses = CreateCareerRow(screen, topCenter, "Losses", "LOSSES", "BATTLES LOST.");
            TMP_Text kos = CreateCareerRow(screen, topCenter, "Kos", "KOS", "KNOCKOUTS SCORED.");
            TMP_Text kod = CreateCareerRow(screen, topCenter, "Kod", "KO'D", "TIMES KNOCKED OUT.");
            centerLayout.padding = new RectOffset(18, 18, 16, 16);

            VerticalLayoutGroup rightLayout = Column(topRight, 10f);
            rightLayout.padding = new RectOffset(18, 18, 24, 24);
            TMP_Text ramEarned = CreateCareerRow(screen, topRight, "RamEarned", "RAM EARNED", "LIFETIME RAM EARNED.");
            TMP_Text ramSpent = CreateCareerRow(screen, topRight, "RamSpent", "RAM SPENT", "LIFETIME RAM SPENT.");
            TMP_Text rebirths = CreateCareerRow(screen, topRight, "Rebirths", "REBIRTHS", "REBIRTHS COMPLETED.");
            TMP_Text starUps = CreateCareerRow(screen, topRight, "StarUps", "STAR UPS", "STAR UPS COMPLETED.");

            TMP_Text focusLine = CreateCaption(lower, "FocusLine", "CAREER", 22f, TextAlignmentOptions.TopLeft);
            PlayerMenuUiFactory.Stretch(focusLine.rectTransform, 24f, 96f, 24f, 20f);
            focusLine.color = PlayerMenuUiFactory.Text;
            Button shareButton = CreateShareButton(lower);

            SerializedObject serializedScreen = new SerializedObject(screen);
            SetObject(serializedScreen, "canvasGroup", canvasGroup);
            SetObject(serializedScreen, "defaultSelection", shareButton);
            SetObject(serializedScreen, "supabaseConfig", AssetDatabase.LoadAssetAtPath<Object>(SupabaseConfigPath));
            SetObject(serializedScreen, "shareCopy", shareCopy);
            SetObject(serializedScreen, "shareService", shareService);
            SetObject(serializedScreen, "profileCardRoot", sheet);
            SetObject(serializedScreen, "nameLabel", nameLabel);
            SetObject(serializedScreen, "linkChipLabel", chipLabel);
            SetObject(serializedScreen, "uniqueSpeciesValue", uniqueSpecies);
            SetObject(serializedScreen, "capturesValue", captures);
            SetObject(serializedScreen, "entangledValue", entangled);
            SetObject(serializedScreen, "winsValue", wins);
            SetObject(serializedScreen, "lossesValue", losses);
            SetObject(serializedScreen, "kosValue", kos);
            SetObject(serializedScreen, "kodValue", kod);
            SetObject(serializedScreen, "ramEarnedValue", ramEarned);
            SetObject(serializedScreen, "ramSpentValue", ramSpent);
            SetObject(serializedScreen, "rebirthsValue", rebirths);
            SetObject(serializedScreen, "starUpsValue", starUps);
            SetObject(serializedScreen, "focusLine", focusLine);
            SetObject(serializedScreen, "cosmeticHintLabel", hint);
            SetObject(serializedScreen, "shareButton", shareButton);
            SetObject(serializedScreen, "captureRecordButton", capturesButton);
            serializedScreen.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedShare = new SerializedObject(shareService);
            SetObject(serializedShare, "cardRoot", sheet);
            SetObject(serializedShare, "portrait", turntable);
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

        private static RectTransform CreateSlot(Transform parent, string name, Vector2 min, Vector2 max)
        {
            RectTransform slot = EnsureRect(parent, name);
            SetAnchors(slot, min, max, 0f, 0f, 0f, 0f);
            Image panel = GetOrAdd<Image>(slot.gameObject);
            Sprite border = AssetDatabase.LoadAssetAtPath<Sprite>(BlankPanelPath);
            panel.sprite = border;
            panel.type = border != null ? Image.Type.Sliced : Image.Type.Simple;
            panel.pixelsPerUnitMultiplier = 0.2f;
            panel.color = PlayerMenuUiFactory.Panel;
            panel.raycastTarget = false;
            return slot;
        }

        private static VerticalLayoutGroup Column(RectTransform parent, float spacing)
        {
            VerticalLayoutGroup layout = GetOrAdd<VerticalLayoutGroup>(parent.gameObject);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        private static TMP_Text CreateCareerRow(ProfileMenuScreen screen, Transform parent, string name, string label, string line)
        {
            return CreateCareerRow(screen, parent, name, label, line, out _);
        }

        private static TMP_Text CreateCareerRow(
            ProfileMenuScreen screen,
            Transform parent,
            string name,
            string label,
            string line,
            out Button button)
        {
            RectTransform row = EnsureRect(parent, name);
            PlayerMenuUiFactory.LayoutElement(row.gameObject, preferredHeight: 40f, flexibleWidth: 1f);
            Image hit = GetOrAdd<Image>(row.gameObject);
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            button = GetOrAdd<Button>(row.gameObject);
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            Image frame = EnsureImage(row, "SelectedFrame", Color.white);
            PlayerMenuUiFactory.Stretch(frame.rectTransform, -4f, -2f, -4f, -2f);
            Sprite ring = AssetDatabase.LoadAssetAtPath<Sprite>(FocusRingPath);
            frame.sprite = ring;
            frame.type = ring != null ? Image.Type.Sliced : Image.Type.Simple;
            frame.pixelsPerUnitMultiplier = 0.2f;
            frame.raycastTarget = false;
            frame.gameObject.SetActive(false);

            TMP_Text caption = CreateCaption(row, "Label", label, 18f, TextAlignmentOptions.MidlineLeft);
            caption.rectTransform.anchorMin = new Vector2(0f, 0f);
            caption.rectTransform.anchorMax = new Vector2(0.68f, 1f);
            caption.rectTransform.offsetMin = new Vector2(8f, 0f);
            caption.rectTransform.offsetMax = Vector2.zero;
            TMP_Text value = CreateCaption(row, "Value", "0", 18f, TextAlignmentOptions.MidlineRight);
            value.rectTransform.anchorMin = new Vector2(0.68f, 0f);
            value.rectTransform.anchorMax = Vector2.one;
            value.rectTransform.offsetMin = Vector2.zero;
            value.rectTransform.offsetMax = new Vector2(-8f, 0f);
            value.color = PlayerMenuUiFactory.Accent;

            ProfileCareerRow careerRow = GetOrAdd<ProfileCareerRow>(row.gameObject);
            careerRow.Configure(screen, line, frame.gameObject);
            return value;
        }

        private static Button CreateShareButton(Transform parent)
        {
            Button button = PlayerMenuUiFactory.CreateButton("ShareButton", parent, "SHARE", Color.white);
            RectTransform rect = button.transform as RectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 64f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            Image image = button.targetGraphic as Image;
            Sprite gray = AssetDatabase.LoadAssetAtPath<Sprite>(GrayButtonPath);
            Sprite orange = AssetDatabase.LoadAssetAtPath<Sprite>(OrangeButtonPath);
            if (image != null)
            {
                image.sprite = gray != null ? gray : orange;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }

            button.transition = Selectable.Transition.None;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            button.navigation = navigation;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = "SHARE";
                label.fontSize = 22f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                ApplyPixeloid(label);
            }

            Type animatorType = FindBehaviourType("UIImageFrameAnimator");
            Type motionType = FindBehaviourType("UiSelectableMotion");
            if (animatorType != null && motionType != null && image != null)
            {
                GameObject shimmerObject = new GameObject("Shimmer", typeof(RectTransform));
                shimmerObject.transform.SetParent(button.transform, false);
                RectTransform shimmerRect = shimmerObject.GetComponent<RectTransform>();
                PlayerMenuUiFactory.Stretch(shimmerRect, 0f, 0f, 0f, 0f);
                shimmerObject.transform.SetSiblingIndex(0);
                Image shimmerImage = shimmerObject.AddComponent<Image>();
                shimmerImage.raycastTarget = false;
                shimmerImage.type = Image.Type.Sliced;
                shimmerImage.color = Color.white;
                Component animator = shimmerObject.AddComponent(animatorType);
                Component motion = button.gameObject.AddComponent(motionType);
                SerializedObject serializedAnimator = new SerializedObject(animator);
                AssignSpriteArray(serializedAnimator.FindProperty("frames"), LoadShimmerFrames());
                SetBool(serializedAnimator, "loop", true);
                SetBool(serializedAnimator, "autoPlayOnStart", false);
                SetFloat(serializedAnimator, "fps", 12f);
                serializedAnimator.ApplyModifiedPropertiesWithoutUndo();

                SerializedObject serializedMotion = new SerializedObject(motion);
                SetObject(serializedMotion, "profile", AssetDatabase.LoadAssetAtPath<Object>(MotionPath));
                SetObject(serializedMotion, "buttonImage", image);
                SetObject(serializedMotion, "targetTransform", button.transform);
                SetObject(serializedMotion, "shimmerAnimator", animator);
                SetBool(serializedMotion, "shimmerOwnsSelectedVisual", true);
                SerializedProperty selected = serializedMotion.FindProperty("selectedSprite");
                SerializedProperty deselected = serializedMotion.FindProperty("deselectedSprite");
                if (selected != null)
                    selected.objectReferenceValue = orange;
                if (deselected != null)
                    deselected.objectReferenceValue = gray;
                serializedMotion.ApplyModifiedPropertiesWithoutUndo();
            }

            ProfileCareerRow row = GetOrAdd<ProfileCareerRow>(button.gameObject);
            row.Configure(button.GetComponentInParent<ProfileMenuScreen>(), "SHARE THIS CAREER SHEET.", null);
            return button;
        }

        private static void CopyIdleSprites(ProfilePortraitTurntable turntable, Image body, Image hat, Image outfit)
        {
            SerializedObject serialized = new SerializedObject(turntable);
            SetObject(serialized, "bodyImage", body);
            SetObject(serialized, "hatImage", hat);
            SetObject(serialized, "outfitImage", outfit);
            SetFloat(serialized, "holdSeconds", ProfilePortraitTurntable.HoldSeconds);
            SetFloat(serialized, "idleFramesPerSecond", ProfilePortraitTurntable.IdleFramesPerSecond);

            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerRootPath);
            MonoBehaviour visual = null;
            if (player != null)
            {
                MonoBehaviour[] behaviours = player.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] != null && behaviours[i].GetType().Name == "MeshRendererPlayerVisual")
                    {
                        visual = behaviours[i];
                        break;
                    }
                }
            }

            if (visual != null)
            {
                SerializedObject source = new SerializedObject(visual);
                SerializedProperty sequences = source.FindProperty("idleSequences");
                CopySpriteArray(sequences, "up", serialized.FindProperty("upSprites"));
                CopySpriteArray(sequences, "right", serialized.FindProperty("rightSprites"));
                CopySpriteArray(sequences, "down", serialized.FindProperty("downSprites"));
                CopySpriteArray(sequences, "left", serialized.FindProperty("leftSprites"));
                SerializedProperty down = serialized.FindProperty("downSprites");
                if (body != null && down != null && down.arraySize > 0)
                    body.sprite = down.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CopySpriteArray(SerializedProperty sequences, string facing, SerializedProperty destination)
        {
            if (sequences == null || destination == null)
                return;

            SerializedProperty sprites = sequences.FindPropertyRelative(facing)?.FindPropertyRelative("sprites");
            if (sprites == null || !sprites.isArray)
                return;

            destination.arraySize = sprites.arraySize;
            for (int i = 0; i < sprites.arraySize; i++)
                destination.GetArrayElementAtIndex(i).objectReferenceValue = sprites.GetArrayElementAtIndex(i).objectReferenceValue;
        }

        private static TMP_Text CreateCaption(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
        {
            TMP_Text text = PlayerMenuUiFactory.CreateText(name, parent, value, size, alignment, false);
            text.fontStyle = FontStyles.Bold;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            ApplyPixeloid(text);
            return text;
        }

        private static void ApplyPixeloid(TMP_Text text)
        {
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixeloidFontPath);
            if (font == null || text == null)
                return;

            text.font = font;
            text.fontStyle |= FontStyles.UpperCase;
        }

        private static Sprite[] LoadShimmerFrames()
        {
            var frames = new List<Sprite>();
            for (int i = 1; i <= 11; i++)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ShimmerFrameFolder}/orange_button_anim{i}.png");
                if (sprite != null)
                    frames.Add(sprite);
            }

            return frames.ToArray();
        }

        private static void AssignSpriteArray(SerializedProperty property, Sprite[] sprites)
        {
            if (property == null || sprites == null)
                return;

            property.arraySize = sprites.Length;
            for (int i = 0; i < sprites.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        private static Type FindBehaviourType(string typeName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type.Name == typeName)
                    return type;
            }

            return null;
        }

        private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.boolValue = value;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null)
                property.floatValue = value;
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
            SetString(serializedTab, "description", "Career sheet and share.");
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
