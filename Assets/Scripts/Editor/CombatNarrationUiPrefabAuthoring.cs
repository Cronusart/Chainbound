#if UNITY_EDITOR
using System.Collections.Generic;
using Chainbound.Combat;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Authors combat narration + post-battle report UI as editable prefabs, nests them under
/// <c>CombatBattleCanvas</c>, and wires controller/router references.
///
/// Menu: <b>Tools / Chainbound / Combat / Author Combat Narration + Post-Battle Report UI</b>
/// </summary>
public static class CombatNarrationUiPrefabAuthoring
{
    private const string CanvasPrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
    private const string NarrationPrefabPath = "Assets/Prefabs/Combat/CombatNarrationPanel.prefab";
    private const string ReportPrefabPath = "Assets/Prefabs/Combat/CombatPostBattleReport.prefab";
    private const string TestHarnessScenePath = "Assets/Scenes/Combat2_TestHarness.unity";

    private const string HudCanvasName = "CombatHudCanvas";
    private const string SafeAreaRootName = "SafeAreaRoot";
    private const string NarrationRootName = "CombatNarrationPanel";
    private const string ReportRootName = "CombatPostBattleReport";

    [MenuItem("Tools/Chainbound/Combat/Author Combat Narration + Post-Battle Report UI")]
    public static void Author()
    {
        var report = new List<string>();

        GameObject narrationPrefab = EnsureNarrationPrefab(report);
        GameObject reportPrefab = EnsureReportPrefab(report);
        WireIntoCombatBattleCanvas(narrationPrefab, reportPrefab, report);
        WireTestHarnessRouter(report);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CombatNarrationUiAuthoring]\n - " + string.Join("\n - ", report));
    }

    [MenuItem("Tools/Chainbound/Combat/Author Dialogue Choice Buttons")]
    public static void AuthorDialogueChoiceButtons()
    {
        var report = new List<string>();
        UpgradeNarrationChoiceUi(report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CombatNarrationUiAuthoring] Dialogue choices\n - " + string.Join("\n - ", report));
    }

    private static GameObject EnsureNarrationPrefab(List<string> report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(NarrationPrefabPath);
        if (existing != null)
        {
            report.Add($"Narration prefab already exists: {NarrationPrefabPath}");
            UpgradeNarrationChoiceUi(report);
            return AssetDatabase.LoadAssetAtPath<GameObject>(NarrationPrefabPath);
        }

        var root = new GameObject(NarrationRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        try
        {
            RectTransform panel = root.GetComponent<RectTransform>();
            ConfigureNarrationRect(panel);
            root.GetComponent<Image>().color = new Color(0.96f, 0.94f, 0.82f, 0.98f);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            TextMeshProUGUI speaker = CreateText(
                "SpeakerName",
                panel,
                36f,
                FontStyles.Bold,
                TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(56f, -96f),
                new Vector2(-56f, -40f));
            speaker.color = new Color(0.35f, 0.18f, 0.22f, 1f);

            TextMeshProUGUI body = CreateText(
                "DialogText",
                panel,
                48f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(56f, 40f),
                new Vector2(-56f, -108f));
            body.enableAutoSizing = true;
            body.fontSizeMin = 28f;
            body.fontSizeMax = 52f;

            DialogueBoxView view = root.AddComponent<DialogueBoxView>();
            view.Configure(
                root,
                body,
                speaker,
                prefixSpeakerInBody: false,
                panelFadeDuration: 0.12f,
                requiredBlinkCycles: 0,
                blinkHalfCycle: 0.18f,
                continueDimAlpha: 0.35f);
            EnsureNarrationChoiceUi(root, report);

            // Keep inactive in prefab so opening the asset doesn't look "live".
            root.SetActive(false);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, NarrationPrefabPath);
            report.Add($"Created narration prefab: {NarrationPrefabPath}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject EnsureReportPrefab(List<string> report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ReportPrefabPath);
        if (existing != null)
        {
            UpgradeReportPrefab(report);
            return AssetDatabase.LoadAssetAtPath<GameObject>(ReportPrefabPath);
        }

        var root = new GameObject(ReportRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        try
        {
            RectTransform overlayRect = root.GetComponent<RectTransform>();
            Stretch(overlayRect, Vector2.zero, Vector2.zero);
            root.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.08f, 0.88f);
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;

            EnsureBannerSurface(root);
            GameObject reportSurface = NewUiObject("ReportSurface", root.transform, typeof(Image));
            RectTransform reportRect = (RectTransform)reportSurface.transform;
            reportRect.anchorMin = new Vector2(0.06f, 0.08f);
            reportRect.anchorMax = new Vector2(0.94f, 0.92f);
            reportRect.offsetMin = Vector2.zero;
            reportRect.offsetMax = Vector2.zero;
            reportSurface.GetComponent<Image>().color = new Color(0.96f, 0.94f, 0.82f, 1f);

            TextMeshProUGUI title = CreateText(
                "Title",
                reportRect,
                54f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.84f),
                new Vector2(0.94f, 0.97f),
                Vector2.zero,
                Vector2.zero);

            GameObject tabs = NewUiObject("Tabs", reportRect);
            RectTransform tabsRect = (RectTransform)tabs.transform;
            tabsRect.anchorMin = new Vector2(0.08f, 0.73f);
            tabsRect.anchorMax = new Vector2(0.92f, 0.83f);
            tabsRect.offsetMin = Vector2.zero;
            tabsRect.offsetMax = Vector2.zero;
            HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 12f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            Button summaryTab = CreateButton("SummaryTab", tabs.transform, "SUMMARY");
            summaryTab.gameObject.SetActive(false);
            CreateButton("BattleLogTab", tabs.transform, "BATTLE LOG");

            GameObject viewport = NewUiObject("Viewport", reportRect, typeof(Image), typeof(Mask), typeof(ScrollRect));
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0.08f, 0.18f);
            viewportRect.anchorMax = new Vector2(0.92f, 0.70f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.26f);
            viewport.GetComponent<Mask>().showMaskGraphic = true;

            TextMeshProUGUI content = CreateText(
                "Content",
                viewportRect,
                38f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                Vector2.zero,
                Vector2.zero);
            content.rectTransform.pivot = new Vector2(0.5f, 1f);
            content.rectTransform.sizeDelta = new Vector2(-56f, 0f);
            content.enableAutoSizing = true;
            content.fontSizeMin = 26f;
            content.fontSizeMax = 40f;
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = content.rectTransform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            Button continueButton = CreateButton("ContinueButton", reportRect, "CONTINUE");
            RectTransform continueRect = (RectTransform)continueButton.transform;
            continueRect.anchorMin = new Vector2(0.32f, 0.05f);
            continueRect.anchorMax = new Vector2(0.68f, 0.14f);
            continueRect.offsetMin = Vector2.zero;
            continueRect.offsetMax = Vector2.zero;

            title.text = "BATTLE COMPLETE";
            content.text = "Summary / battle log content binds at runtime.";
            EnsureRewardsSurface(root);
            PolishPostBattleVisuals(root);
            root.SetActive(false);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ReportPrefabPath);
            report.Add($"Created report prefab: {ReportPrefabPath}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void UpgradeNarrationChoiceUi(List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(NarrationPrefabPath);
        if (root == null)
        {
            report.Add($"ERROR: could not load {NarrationPrefabPath} for choice UI");
            return;
        }

        try
        {
            EnsureNarrationChoiceUi(root, report);
            PrefabUtility.SaveAsPrefabAsset(root, NarrationPrefabPath);
            report.Add($"Saved authored choice row on {NarrationPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureNarrationChoiceUi(GameObject panelRoot, List<string> report)
    {
        if (panelRoot == null)
            return;

        Transform visualParent = FindDeep(panelRoot.transform, "CombatNarrationBG") ?? panelRoot.transform;
        Transform rowTransform = FindDeep(panelRoot.transform, "DialogueChoices");
        bool createdRow = rowTransform == null;
        GameObject row = createdRow ? NewUiObject("DialogueChoices", visualParent) : rowTransform.gameObject;
        RectTransform rowRect = (RectTransform)row.transform;
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.anchoredPosition = new Vector2(0f, 18f);
        if (createdRow)
            rowRect.sizeDelta = new Vector2(-600f, 86f);
        rowRect.SetAsLastSibling();

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>() ?? row.AddComponent<HorizontalLayoutGroup>();
        if (createdRow)
        {
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        Transform templateTransform = FindDeep(row.transform, "ChoiceButton");
        bool createdTemplate = templateTransform == null;
        Button template = createdTemplate
            ? CreateButton("ChoiceButton", row.transform, "Choice")
            : templateTransform.GetComponent<Button>();
        if (createdTemplate)
        {
            LayoutElement layoutElement = template.GetComponent<LayoutElement>() ?? template.gameObject.AddComponent<LayoutElement>();
            layoutElement.minWidth = 200f;
            layoutElement.minHeight = 74f;
            layoutElement.preferredHeight = 74f;
            layoutElement.flexibleWidth = 1f;
            layoutElement.flexibleHeight = 0f;

            TextMeshProUGUI body = FindText(panelRoot.transform, "DialogText");
            TextMeshProUGUI label = FindText(template.transform, "Label");
            if (label != null)
            {
                if (body != null && body.font != null)
                    label.font = body.font;
                label.fontSize = 32f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 22f;
                label.fontSizeMax = 32f;
                label.text = "Choice";
            }

            Sprite gray = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png");
            Sprite orange = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png");
            PolishButton(template, gray, orange, LoadButtonShimmerFrames());
        }

        if (template != null)
            template.gameObject.SetActive(false);
        row.SetActive(false);

        DialogueBoxView view = panelRoot.GetComponent<DialogueBoxView>() ??
                               panelRoot.GetComponentInChildren<DialogueBoxView>(true);
        if (view != null)
        {
            var so = new SerializedObject(view);
            SetObject(so, "choiceRow", row);
            SetObject(so, "choiceButtonTemplate", template);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
        }

        EditorUtility.SetDirty(row);
        EditorUtility.SetDirty(template);
        report.Add("Authored DialogueChoices row and ChoiceButton template");
    }

    private static void UpgradeReportPrefab(List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ReportPrefabPath);
        if (root == null)
        {
            report.Add($"ERROR: could not load {ReportPrefabPath} for upgrade");
            return;
        }

        try
        {
            bool needsBanner = FindDeep(root.transform, "BannerSurface") == null;
            bool needsRewards = FindDeep(root.transform, "RewardsSurface") == null;
            if (FindDeep(root.transform, "ReportSurface") == null)
                report.Add("WARNING: ReportSurface missing on CombatPostBattleReport prefab");
            if (needsBanner)
                EnsureBannerSurface(root);
            if (needsRewards)
                EnsureRewardsSurface(root);
            if (needsBanner || needsRewards)
            {
                PrefabUtility.SaveAsPrefabAsset(root, ReportPrefabPath);
                report.Add($"Added missing report surfaces: {ReportPrefabPath}");
            }
            else
            {
                report.Add($"Left authored report prefab unchanged: {ReportPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureBannerSurface(GameObject overlayRoot)
    {
        Transform existing = FindDeep(overlayRoot.transform, "BannerSurface");
        bool created = existing == null;
        GameObject banner = created ? NewUiObject("BannerSurface", overlayRoot.transform) : existing.gameObject;
        if (created)
            Stretch((RectTransform)banner.transform, Vector2.zero, Vector2.zero);

        if (FindDeep(banner.transform, "BannerTitle") == null)
        {
            TextMeshProUGUI title = CreateText(
                "BannerTitle",
                banner.transform,
                72f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.42f),
                new Vector2(0.94f, 0.78f),
                Vector2.zero,
                Vector2.zero);
            title.color = Color.white;
            title.text = "VICTORY!!!";
        }

        if (FindDeep(banner.transform, "BannerImage") == null)
        {
            GameObject imageGo = NewUiObject("BannerImage", banner.transform, typeof(Image));
            RectTransform imageRect = (RectTransform)imageGo.transform;
            imageRect.anchorMin = new Vector2(0.18f, 0.28f);
            imageRect.anchorMax = new Vector2(0.82f, 0.82f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageGo.SetActive(false);
        }

        if (FindDeep(banner.transform, "BannerPrompt") == null)
        {
            TextMeshProUGUI prompt = CreateText(
                "BannerPrompt",
                banner.transform,
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center,
                new Vector2(0.2f, 0.08f),
                new Vector2(0.8f, 0.16f),
                Vector2.zero,
                Vector2.zero);
            prompt.color = Color.white;
            prompt.text = "PRESS CONFIRM";
        }

        if (FindButton(banner.transform, "BannerContinueButton") == null)
        {
            Button confirm = CreateButton("BannerContinueButton", banner.transform, "CONFIRM");
            RectTransform confirmRect = (RectTransform)confirm.transform;
            confirmRect.anchorMin = new Vector2(0.32f, 0.18f);
            confirmRect.anchorMax = new Vector2(0.68f, 0.28f);
            confirmRect.offsetMin = Vector2.zero;
            confirmRect.offsetMax = Vector2.zero;
        }

        banner.SetActive(false);
        PolishBannerSurface(banner);
    }

    private static void PolishPostBattleVisuals(GameObject overlayRoot)
    {
        Transform banner = FindDeep(overlayRoot.transform, "BannerSurface");
        if (banner != null)
            PolishBannerSurface(banner.gameObject);

        Transform report = FindDeep(overlayRoot.transform, "ReportSurface");
        if (report != null)
            PolishMenuSurface(report.gameObject, new Vector2(1f, 0.66f));

        Transform rewards = FindDeep(overlayRoot.transform, "RewardsSurface");
        if (rewards != null)
            PolishMenuSurface(rewards.gameObject, new Vector2(1f, 0.66f));

        PolishAllButtons(overlayRoot);
        PolishXpRowsLayout(overlayRoot);
    }

    private static void PolishBannerSurface(GameObject banner)
    {
        Transform cardTransform = FindDeep(banner.transform, "BannerCard");
        GameObject card = cardTransform != null ? cardTransform.gameObject : NewUiObject("BannerCard", banner.transform, typeof(Image));
        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = new Vector2(0f, 0.5f);
        cardRect.anchorMax = new Vector2(1f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = new Vector2(0f, 7.5f);
        cardRect.sizeDelta = new Vector2(0f, 463f);
        Image cardImage = card.GetComponent<Image>() ?? card.AddComponent<Image>();
        Sprite bannerSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Combat/encounter_banner.png");
        if (bannerSprite != null)
            cardImage.sprite = bannerSprite;
        cardImage.color = Color.white;
        cardImage.preserveAspect = true;
        cardImage.raycastTarget = false;
        cardImage.type = Image.Type.Simple;

        Transform contentsTransform = FindDeep(card.transform, "BannerContents");
        GameObject contents = contentsTransform != null
            ? contentsTransform.gameObject
            : NewUiObject("BannerContents", card.transform, typeof(CanvasGroup));
        if (contents.GetComponent<CanvasGroup>() == null)
            contents.AddComponent<CanvasGroup>();
        Stretch((RectTransform)contents.transform, Vector2.zero, Vector2.zero);

        ReparentIfNeeded(banner.transform, contents.transform, "BannerTitle");
        ReparentIfNeeded(banner.transform, contents.transform, "BannerImage");
        ReparentIfNeeded(banner.transform, contents.transform, "BannerPrompt");
        ReparentIfNeeded(banner.transform, contents.transform, "BannerContinueButton");
    }

    private static void PolishMenuSurface(GameObject surface, Vector2? scanlineTiling = null)
    {
        Image plate = surface.GetComponent<Image>() ?? surface.AddComponent<Image>();
        plate.color = new Color(0.96f, 0.94f, 0.82f, 1f);
        if (surface.GetComponent<CanvasGroup>() == null)
            surface.AddComponent<CanvasGroup>();

        Texture scanlineTexture = AssetDatabase.LoadAssetAtPath<Texture>(
            "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png");
        CombatUiScanlineBackground scanlines = surface.GetComponent<CombatUiScanlineBackground>() ??
                                               surface.AddComponent<CombatUiScanlineBackground>();
        scanlines.Configure(
            enabled: true,
            texture: scanlineTexture,
            color: new Color(1f, 1f, 1f, 0.45f),
            tiling: scanlineTiling ?? new Vector2(1f, 0.333f),
            scrollDirection: Vector2.down,
            uvSpeed: 0.05f);
        EditorUtility.SetDirty(scanlines);
        EditorUtility.SetDirty(surface);
    }

    private static void PolishAllButtons(GameObject overlayRoot)
    {
        Sprite gray = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png");
        Sprite orange = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png");
        Sprite[] shimmer = LoadButtonShimmerFrames();
        Button[] buttons = overlayRoot.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
            PolishButton(buttons[i], gray, orange, shimmer);
    }

    private static void PolishButton(Button button, Sprite gray, Sprite orange, Sprite[] shimmer)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>() ?? button.targetGraphic as Image;
        if (image != null)
        {
            if (gray != null)
                image.sprite = gray;
            image.color = Color.white;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        button.transition = Selectable.Transition.None;
        LayoutPrimaryConfirmButton(button);
        CombatUiSelectableButtonVisual visual = button.GetComponent<CombatUiSelectableButtonVisual>() ??
                                                button.gameObject.AddComponent<CombatUiSelectableButtonVisual>();
        var so = new SerializedObject(visual);
        so.FindProperty("buttonImage").objectReferenceValue = image;
        so.FindProperty("unselectedSprite").objectReferenceValue = gray;
        so.FindProperty("selectedSprite").objectReferenceValue = orange;
        SerializedProperty frames = so.FindProperty("shimmerFrames");
        frames.arraySize = shimmer != null ? shimmer.Length : 0;
        for (int i = 0; i < frames.arraySize; i++)
            frames.GetArrayElementAtIndex(i).objectReferenceValue = shimmer[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        visual.Configure(gray, orange, shimmer);
        EditorUtility.SetDirty(visual);
        EditorUtility.SetDirty(button);
    }

    private static void LayoutPrimaryConfirmButton(Button button)
    {
        if (button == null)
            return;
        if (!string.Equals(button.name, "ContinueButton", System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(button.name, "RewardsContinueButton", System.StringComparison.OrdinalIgnoreCase))
            return;

        RectTransform rect = button.transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 72f);
        rect.sizeDelta = new Vector2(560f, 88f);

        Image image = button.GetComponent<Image>();
        if (image != null)
            image.pixelsPerUnitMultiplier = 1f;

        Transform label = button.transform.Find("Label");
        if (label != null)
        {
            RectTransform labelRect = label as RectTransform;
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(16f, 8f);
                labelRect.offsetMax = new Vector2(-16f, -8f);
            }

            TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
            if (text != null)
            {
                text.color = Color.white;
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
            }
        }
    }

    private static void PolishXpRowsLayout(GameObject overlayRoot)
    {
        Transform xpRows = FindDeep(overlayRoot.transform, "XpRows");
        if (xpRows == null)
            return;

        VerticalLayoutGroup layout = xpRows.GetComponent<VerticalLayoutGroup>() ??
                                     xpRows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(8, 8, 4, 4);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        EditorUtility.SetDirty(layout);
    }

    private static Sprite[] LoadButtonShimmerFrames()
    {
        var frames = new List<Sprite>();
        for (int i = 1; i <= 14; i++)
        {
            Sprite sliced = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Sprites/UI/MainMenu/Buttons/Orange_Button_Anim/Sliced/orange_button_sliceanim{i}.png");
            if (sliced != null)
                frames.Add(sliced);
        }

        if (frames.Count > 0)
            return frames.ToArray();

        for (int i = 1; i <= 11; i++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                $"Assets/Sprites/UI/MainMenu/Buttons/Orange_Button_Anim/orange_button_anim{i}.png");
            if (sprite != null)
                frames.Add(sprite);
        }

        return frames.ToArray();
    }

    private static void ReparentIfNeeded(Transform searchRoot, Transform newParent, string childName)
    {
        Transform child = FindDeep(searchRoot, childName);
        if (child == null || child.parent == newParent)
            return;
        child.SetParent(newParent, true);
    }

    private static void EnsureRewardsSurface(GameObject overlayRoot)
    {
        if (FindDeep(overlayRoot.transform, "RewardsSurface") != null)
            return;

        GameObject rewards = NewUiObject("RewardsSurface", overlayRoot.transform, typeof(Image));
        RectTransform rewardsRect = (RectTransform)rewards.transform;
        rewardsRect.anchorMin = new Vector2(0.06f, 0.08f);
        rewardsRect.anchorMax = new Vector2(0.94f, 0.92f);
        rewardsRect.offsetMin = Vector2.zero;
        rewardsRect.offsetMax = Vector2.zero;
        rewards.GetComponent<Image>().color = new Color(0.96f, 0.94f, 0.82f, 1f);

        CreateText(
            "RewardsBody",
            rewardsRect,
            32f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            new Vector2(0.08f, 0.78f),
            new Vector2(0.92f, 0.94f),
            Vector2.zero,
            Vector2.zero);

        TextMeshProUGUI ram = CreateText(
            "RamLabel",
            rewardsRect,
            40f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.68f),
            new Vector2(0.92f, 0.78f),
            Vector2.zero,
            Vector2.zero);
        ram.text = "RAM";

        GameObject lootRow = NewUiObject("LootRow", rewardsRect);
        RectTransform lootRect = (RectTransform)lootRow.transform;
        lootRect.anchorMin = new Vector2(0.08f, 0.58f);
        lootRect.anchorMax = new Vector2(0.92f, 0.68f);
        lootRect.offsetMin = Vector2.zero;
        lootRect.offsetMax = Vector2.zero;
        CreateText(
            "LootLabel",
            lootRow.transform,
            28f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero);

        GameObject xpRows = NewUiObject("XpRows", rewardsRect, typeof(VerticalLayoutGroup));
        RectTransform xpRect = (RectTransform)xpRows.transform;
        xpRect.anchorMin = new Vector2(0.08f, 0.18f);
        xpRect.anchorMax = new Vector2(0.92f, 0.58f);
        xpRect.offsetMin = Vector2.zero;
        xpRect.offsetMax = Vector2.zero;
        VerticalLayoutGroup xpLayout = xpRows.GetComponent<VerticalLayoutGroup>();
        xpLayout.spacing = 8f;
        xpLayout.childForceExpandWidth = true;
        xpLayout.childForceExpandHeight = false;

        Button confirm = CreateButton("RewardsContinueButton", rewardsRect, "CONFIRM");
        RectTransform confirmRect = (RectTransform)confirm.transform;
        confirmRect.anchorMin = new Vector2(0.32f, 0.05f);
        confirmRect.anchorMax = new Vector2(0.68f, 0.14f);
        confirmRect.offsetMin = Vector2.zero;
        confirmRect.offsetMax = Vector2.zero;
        rewards.SetActive(false);
    }

    private static void WireIntoCombatBattleCanvas(GameObject narrationPrefab, GameObject reportPrefab, List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        if (root == null)
        {
            report.Add($"ERROR: could not load {CanvasPrefabPath}");
            return;
        }

        try
        {
            Transform hudCanvas = FindDeep(root.transform, HudCanvasName);
            Transform safeArea = hudCanvas != null
                ? FindDeep(hudCanvas, SafeAreaRootName)
                : FindDeep(root.transform, SafeAreaRootName);
            if (safeArea == null)
            {
                report.Add("ERROR: SafeAreaRoot not found under CombatBattleCanvas");
                return;
            }

            GameObject narrationInstance = EnsureNestedInstance(safeArea, NarrationRootName, narrationPrefab, report);
            GameObject reportInstance = EnsureNestedInstance(safeArea, ReportRootName, reportPrefab, report);

            CombatNarrationBoxController narrationController =
                root.GetComponent<CombatNarrationBoxController>() ??
                root.GetComponentInChildren<CombatNarrationBoxController>(true);
            if (narrationController == null)
            {
                narrationController = root.AddComponent<CombatNarrationBoxController>();
                report.Add("Added CombatNarrationBoxController to CombatBattleCanvas root");
            }
            else
            {
                report.Add($"CombatNarrationBoxController present on '{narrationController.gameObject.name}'");
            }

            CombatPostBattleReportController reportController =
                root.GetComponent<CombatPostBattleReportController>() ??
                root.GetComponentInChildren<CombatPostBattleReportController>(true);
            if (reportController == null)
            {
                reportController = root.AddComponent<CombatPostBattleReportController>();
                report.Add("Added CombatPostBattleReportController to CombatBattleCanvas root");
            }
            else
            {
                report.Add($"CombatPostBattleReportController present on '{reportController.gameObject.name}'");
            }

            WireNarrationController(narrationController, narrationInstance, report);
            WireReportController(reportController, reportInstance, report);

            // Controllers own inactive authored panels; keep nested roots inactive in prefab.
            if (narrationInstance != null)
                narrationInstance.SetActive(false);
            if (reportInstance != null)
                reportInstance.SetActive(false);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            report.Add($"Saved {CanvasPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireTestHarnessRouter(List<string> report)
    {
        Scene scene = EditorSceneManager.OpenScene(TestHarnessScenePath, OpenSceneMode.Additive);
        try
        {
            CombatPostBattleRouter router = Object.FindFirstObjectByType<CombatPostBattleRouter>(FindObjectsInactive.Include);
            if (router == null)
            {
                report.Add("CombatPostBattleRouter not found in Combat2_TestHarness (skipped scene wire)");
                return;
            }

            GameObject canvasGo = GameObject.Find("CombatBattleCanvas");
            CombatPostBattleReportController reportController =
                Object.FindFirstObjectByType<CombatPostBattleReportController>(FindObjectsInactive.Include);

            if (reportController == null && canvasGo != null)
            {
                reportController = canvasGo.GetComponent<CombatPostBattleReportController>() ??
                                   canvasGo.GetComponentInChildren<CombatPostBattleReportController>(true);
            }

            // Prefab asset was just updated; scene instances may lag one import. Sync nested
            // authored UI onto the canvas instance without reverting unrelated overrides.
            if (reportController == null && canvasGo != null)
            {
                reportController = EnsureSceneCanvasControllers(canvasGo, report);
            }

            if (reportController == null)
            {
                report.Add("CombatPostBattleReportController not found in scene (assign router.battleReport manually after reopen)");
                return;
            }

            var so = new SerializedObject(router);
            SerializedProperty prop = so.FindProperty("battleReport");
            if (prop == null)
            {
                report.Add("ERROR: CombatPostBattleRouter.battleReport field missing after compile");
                return;
            }

            if (prop.objectReferenceValue != reportController)
            {
                prop.objectReferenceValue = reportController;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(router);
                report.Add($"Wired CombatPostBattleRouter.battleReport -> '{reportController.gameObject.name}'");
            }
            else
            {
                report.Add("CombatPostBattleRouter.battleReport already wired");
            }

            SerializedProperty skip = so.FindProperty("mvpSkipStubs");
            if (skip != null && skip.boolValue)
            {
                skip.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(router);
                report.Add("Cleared CombatPostBattleRouter.mvpSkipStubs");
            }

            SerializedProperty popup = so.FindProperty("outcomePopup");
            if (popup != null && popup.objectReferenceValue != null)
            {
                popup.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(router);
                report.Add("Cleared CombatPostBattleRouter.outcomePopup");
            }

            SerializedProperty document = so.FindProperty("postBattleDocument");
            if (document != null && document.objectReferenceValue != null)
            {
                document.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(router);
                report.Add("Cleared CombatPostBattleRouter.postBattleDocument");
            }

            StripLegacyOutcomePopup(report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add($"Saved {TestHarnessScenePath}");
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void StripLegacyOutcomePopup(List<string> report)
    {
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] == null)
                continue;
            if (!string.Equals(transforms[i].name, "OutcomePopup", System.StringComparison.OrdinalIgnoreCase))
                continue;
            report.Add($"Removed leftover OutcomePopup '{GetPath(transforms[i])}'");
            Object.DestroyImmediate(transforms[i].gameObject);
        }

        CombatPostBattleRouter router = Object.FindFirstObjectByType<CombatPostBattleRouter>(FindObjectsInactive.Include);
        if (router == null)
            return;

        var documents = router.GetComponents<UnityEngine.UIElements.UIDocument>();
        for (int i = 0; i < documents.Length; i++)
        {
            if (documents[i] == null)
                continue;
            report.Add("Removed leftover UIDocument from PostBattleController");
            Object.DestroyImmediate(documents[i]);
        }
    }

    private static string GetPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        return path;
    }

    private static CombatPostBattleReportController EnsureSceneCanvasControllers(GameObject canvasGo, List<string> report)
    {
        Transform safeArea = FindDeep(canvasGo.transform, SafeAreaRootName);
        if (safeArea == null)
            return null;

        GameObject narrationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(NarrationPrefabPath);
        GameObject reportPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReportPrefabPath);
        if (narrationPrefab == null || reportPrefab == null)
            return null;

        // Prefer nesting under Hud canvas SafeArea when present.
        Transform hud = FindDeep(canvasGo.transform, HudCanvasName);
        if (hud != null)
        {
            Transform hudSafe = FindDeep(hud, SafeAreaRootName);
            if (hudSafe != null)
                safeArea = hudSafe;
        }

        GameObject narrationInstance = EnsureNestedInstance(safeArea, NarrationRootName, narrationPrefab, report);
        GameObject reportInstance = EnsureNestedInstance(safeArea, ReportRootName, reportPrefab, report);

        CombatNarrationBoxController narrationController =
            canvasGo.GetComponent<CombatNarrationBoxController>() ??
            canvasGo.AddComponent<CombatNarrationBoxController>();
        CombatPostBattleReportController reportController =
            canvasGo.GetComponent<CombatPostBattleReportController>() ??
            canvasGo.AddComponent<CombatPostBattleReportController>();

        WireNarrationController(narrationController, narrationInstance, report);
        WireReportController(reportController, reportInstance, report);
        report.Add("Synced authored narration/report onto scene CombatBattleCanvas instance");
        return reportController;
    }

    private static GameObject EnsureNestedInstance(
        Transform parent,
        string childName,
        GameObject sourcePrefab,
        List<string> report)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            // Replace old runtime/fallback hierarchy with nested prefab instance when possible.
            Object.DestroyImmediate(existing.gameObject);
            report.Add($"Removed existing '{childName}' under '{parent.name}' before nesting prefab");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, parent);
        instance.name = childName;
        instance.SetActive(false);
        report.Add($"Nested '{childName}' under '{parent.name}'");
        return instance;
    }

    private static void WireNarrationController(CombatNarrationBoxController controller, GameObject panelRoot, List<string> report)
    {
        if (controller == null || panelRoot == null)
            return;

        RectTransform panel = panelRoot.GetComponent<RectTransform>();
        TextMeshProUGUI body = FindText(panelRoot.transform, "DialogText");
        TextMeshProUGUI speaker = FindText(panelRoot.transform, "SpeakerName");
        DialogueBoxView view = panelRoot.GetComponent<DialogueBoxView>();

        var so = new SerializedObject(controller);
        SetObject(so, "authoredPanel", panel);
        SetObject(so, "authoredBodyText", body);
        SetObject(so, "authoredSpeakerText", speaker);
        SetObject(so, "authoredDialogueView", view);
        SetObject(so, "visualParent", panel != null ? panel.parent as RectTransform : null);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        report.Add("Wired CombatNarrationBoxController authored panel refs");
    }

    private static void WireReportController(CombatPostBattleReportController controller, GameObject overlayRoot, List<string> report)
    {
        if (controller == null || overlayRoot == null)
            return;

        TextMeshProUGUI title = FindText(overlayRoot.transform, "Title");
        TextMeshProUGUI content = FindText(overlayRoot.transform, "Content");
        Button summary = FindButton(overlayRoot.transform, "SummaryTab");
        Button log = FindButton(overlayRoot.transform, "BattleLogTab");
        Button cont = FindButton(overlayRoot.transform, "ContinueButton");
        ScrollRect scroll = overlayRoot.GetComponentInChildren<ScrollRect>(true);
        GameObject banner = FindDeep(overlayRoot.transform, "BannerSurface")?.gameObject;
        GameObject reportSurface = FindDeep(overlayRoot.transform, "ReportSurface")?.gameObject;
        GameObject rewards = FindDeep(overlayRoot.transform, "RewardsSurface")?.gameObject;
        OutcomeBannerProfileSO profile = AssetDatabase.LoadAssetAtPath<OutcomeBannerProfileSO>(
            "Assets/Data/Combat/BattleOutcome/OutcomeBanner.asset");

        var so = new SerializedObject(controller);
        SetObject(so, "overlayRoot", overlayRoot);
        SetObject(so, "bannerProfile", profile);
        SetObject(so, "bannerSurface", banner);
        Transform bannerCard = FindDeep(overlayRoot.transform, "BannerCard");
        SetObject(so, "bannerCard", bannerCard as RectTransform);
        Transform bannerContents = FindDeep(overlayRoot.transform, "BannerContents");
        SetObject(so, "bannerContentsGroup", bannerContents != null ? bannerContents.GetComponent<CanvasGroup>() : null);
        SetObject(so, "bannerTitleText", FindText(overlayRoot.transform, "BannerTitle"));
        SetObject(so, "bannerImage", FindImage(overlayRoot.transform, "BannerImage"));
        SetObject(so, "bannerPromptText", FindText(overlayRoot.transform, "BannerPrompt"));
        SetObject(so, "bannerContinueButton", FindButton(overlayRoot.transform, "BannerContinueButton"));
        SetObject(so, "reportSurface", reportSurface);
        SetObject(so, "titleText", title);
        SetObject(so, "contentText", content);
        SetObject(so, "summaryTabButton", summary);
        SetObject(so, "battleLogTabButton", log);
        SetObject(so, "continueButton", cont);
        SetObject(so, "contentScroll", scroll);
        SetObject(so, "rewardsSurface", rewards);
        Transform xpRows = FindDeep(overlayRoot.transform, "XpRows");
        if (so.FindProperty("xpRowsRoot") != null)
            so.FindProperty("xpRowsRoot").objectReferenceValue = xpRows;
        SetObject(so, "ramLabel", FindText(overlayRoot.transform, "RamLabel"));
        SetObject(so, "rewardsBodyText", FindText(overlayRoot.transform, "RewardsBody"));
        SetObject(so, "lootRow", FindDeep(overlayRoot.transform, "LootRow")?.gameObject);
        SetObject(so, "lootLabel", FindText(overlayRoot.transform, "LootLabel"));
        SetObject(so, "rewardsContinueButton", FindButton(overlayRoot.transform, "RewardsContinueButton"));
        SetObject(so, "typeIconPalette", AssetDatabase.LoadAssetAtPath<TypeIconPaletteSO>(
            "Assets/Data/Combat/Type/Icons/Type_Icons.asset"));
        SetObject(so, "compactStarArt", AssetDatabase.LoadAssetAtPath<CompactStarRatingArtSO>(
            "Assets/Data/UI/CompactStarRatingArt_Smol.asset"));
        SetObject(so, "cycleIconPalette", AssetDatabase.LoadAssetAtPath<CycleIconPaletteSO>(
            "Assets/Data/UI/CycleIconPalette.asset"));
        SetObject(so, "xpRowBackground", AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/UI/PlayerMenu/9sliceborder_blank.png"));
        SetObject(so, "xpBarSprite", AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/UI/PlayerMenu/bar_sliced.png"));
        SetObject(so, "xpRowFont", AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset"));
        SerializedProperty fallback = so.FindProperty("buildFallbackVisualsWhenMissing");
        if (fallback != null)
            fallback.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(controller);
        report.Add("Wired CombatPostBattleReportController authored banner/report/rewards refs");
    }

    private static void ConfigureNarrationRect(RectTransform panel)
    {
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 48f);
        panel.sizeDelta = new Vector2(-64f, 420f);
        panel.localScale = Vector3.one;
        panel.localRotation = Quaternion.identity;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, System.StringComparison.OrdinalIgnoreCase))
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private static GameObject NewUiObject(string name, Transform parent, params System.Type[] components)
    {
        var all = new System.Type[components.Length + 2];
        all[0] = typeof(RectTransform);
        all[1] = typeof(CanvasRenderer);
        System.Array.Copy(components, 0, all, 2, components.Length);
        var go = new GameObject(name, all);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float size,
        FontStyles style,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject go = NewUiObject(name, parent, typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = new Color(0.18f, 0.13f, 0.18f, 1f);
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        GameObject go = NewUiObject(name, parent, typeof(Image), typeof(Button));
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.24f, 0.19f, 0.27f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = CreateText(
            "Label",
            go.transform,
            32f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            new Vector2(16f, 8f),
            new Vector2(-16f, -8f));
        text.color = Color.white;
        text.text = label;
        return button;
    }

    private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = minOffset;
        rect.offsetMax = maxOffset;
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        SerializedProperty prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static TextMeshProUGUI FindText(Transform root, string name)
    {
        TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (string.Equals(texts[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return texts[i];
        }
        return null;
    }

    private static Button FindButton(Transform root, string name)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (string.Equals(buttons[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return buttons[i];
        }
        return null;
    }

    private static Image FindImage(Transform root, string name)
    {
        Image[] images = root.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (string.Equals(images[i].name, name, System.StringComparison.OrdinalIgnoreCase))
                return images[i];
        }
        return null;
    }
}
#endif
