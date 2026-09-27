#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Authors the capture picker and trading-trace challenge as editable prefabs and
/// nests them inactive under <c>CombatBattleCanvas</c>.
/// Menu: <b>Tools / Chainbound / Combat / Author Capture Challenge UI</b>
/// </summary>
public static class CaptureChallengePrefabAuthoring
{
    private const string CanvasPrefabPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
    private const string ChallengePrefabPath = "Assets/Prefabs/Combat/CaptureChallenge.prefab";
    private const string PickerPrefabPath = "Assets/Prefabs/Combat/CaptureCommandPicker.prefab";
    private const string PresentationPath = "Assets/Data/Combat/Capture/CapturePresentationConfig.asset";
    private const string HudCanvasName = "CombatHudCanvas";
    private const string SafeAreaRootName = "SafeAreaRoot";
    private const string ChallengeRootName = "CaptureChallenge";
    private const string PickerRootName = "CaptureCommandPicker";
    private const string BottomActionPanelName = "BottomActionPanel";
    private const string PanelFramePath = "Assets/Sprites/Combat/BattleDevice/device_bottom_panelframe.png";
    private const string MoveRowPrefabPath = "Assets/Prefabs/Combat/MoveRow.prefab";
    private const string InventoryPresentationCatalogPath = "Assets/Data/UI/InventoryItemPresentationCatalog.asset";

    [MenuItem("Tools/Chainbound/Combat/Author Capture Challenge UI")]
    public static void Author()
    {
        var report = new List<string>();
        GameObject challengePrefab = EnsureChallengePrefab(report);
        GameObject pickerPrefab = EnsurePickerPrefab(report);
        WireIntoCombatBattleCanvas(challengePrefab, pickerPrefab, report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CaptureChallengePrefabAuthoring]\n - " + string.Join("\n - ", report));
    }

    public static string AuthorCapturePickerRows()
    {
        var report = new StringBuilder();
        InventoryItemPresentationCatalogSO catalog =
            AssetDatabase.LoadAssetAtPath<InventoryItemPresentationCatalogSO>(InventoryPresentationCatalogPath);
        if (catalog == null)
            return "ERROR: missing inventory presentation catalog";

        SerializedObject catalogSo = new SerializedObject(catalog);
        SerializedProperty entries = catalogSo.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("itemId").stringValue == "signal_orb")
            {
                entry.FindPropertyRelative("displayNameOverride").stringValue = "Signal Orb";
                report.AppendLine("Fixed signal_orb displayNameOverride");
            }
        }

        catalogSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);

        catalog.TryGetItemSprites("signal_orb", out Sprite rowIcon, out Sprite portrait);
        report.AppendLine("rowIcon=" + (rowIcon != null ? rowIcon.name : "null"));
        report.AppendLine("portrait=" + (portrait != null ? portrait.name : "null"));

        GameObject moveRowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MoveRowPrefabPath);
        GameObject picker = PrefabUtility.LoadPrefabContents(PickerPrefabPath);
        try
        {
            RectTransform choices = picker.transform.Find("Choices") as RectTransform;
            RectTransform detail = picker.transform.Find("OrbDetailPanel") as RectTransform;
            Vector2 choicesMin = choices.anchorMin;
            Vector2 choicesMax = choices.anchorMax;
            Vector2 choicesPos = choices.anchoredPosition;
            Vector2 choicesSize = choices.sizeDelta;
            Vector2 choicesPivot = choices.pivot;
            Vector2 detailMin = detail.anchorMin;
            Vector2 detailMax = detail.anchorMax;
            Vector2 detailPos = detail.anchoredPosition;
            Vector2 detailSize = detail.sizeDelta;
            Vector2 detailPivot = detail.pivot;

            Transform oldTemplate = choices.Find("ChoiceTemplate");
            int sibling = oldTemplate != null ? oldTemplate.GetSiblingIndex() : 0;
            if (oldTemplate != null)
                Object.DestroyImmediate(oldTemplate.gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(moveRowPrefab, choices);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "ChoiceTemplate";
            instance.transform.SetSiblingIndex(sibling);
            SetLayerRecursively(instance, picker.layer);

            Image rootImage = instance.GetComponent<Image>();
            if (rootImage != null)
            {
                rootImage.raycastTarget = true;
                Color color = rootImage.color;
                color.a = 0f;
                rootImage.color = color;
            }

            Button button = instance.GetComponent<Button>() ?? instance.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = rootImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            LayoutElement layout = instance.GetComponent<LayoutElement>() ?? instance.AddComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.preferredHeight = 58f;

            RectTransform content = instance.transform.Find("RowContent") as RectTransform;
            Transform labelTransform = content != null ? content.Find("Text (TMP)") : null;
            if (labelTransform == null)
                labelTransform = instance.transform.Find("Label");
            TextMeshProUGUI label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>() : null;
            if (label != null)
            {
                label.text = "Signal Orb";
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                RectTransform labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.offsetMin = new Vector2(64f, 2f);
                labelRect.offsetMax = new Vector2(-88f, -2f);
                label.alignment = TextAlignmentOptions.Left;
                label.verticalAlignment = VerticalAlignmentOptions.Middle;
            }

            Transform quantityTransform = content != null ? content.Find("Quantity") : null;
            if (quantityTransform == null)
            {
                var quantityGo = new GameObject("Quantity", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                quantityGo.layer = instance.layer;
                RectTransform quantityRect = quantityGo.GetComponent<RectTransform>();
                quantityRect.SetParent(content, false);
                quantityRect.anchorMin = new Vector2(1f, 0.5f);
                quantityRect.anchorMax = new Vector2(1f, 0.5f);
                quantityRect.pivot = new Vector2(1f, 0.5f);
                quantityRect.anchoredPosition = new Vector2(-22f, 0f);
                quantityRect.sizeDelta = new Vector2(72f, 32f);
                quantityTransform = quantityRect;
            }

            TextMeshProUGUI quantity = quantityTransform.GetComponent<TextMeshProUGUI>();
            quantity.text = "x1";
            quantity.fontSize = label != null ? label.fontSize : 24f;
            if (label != null && label.font != null)
                quantity.font = label.font;
            quantity.color = Color.white;
            quantity.alignment = TextAlignmentOptions.Right;
            quantity.verticalAlignment = VerticalAlignmentOptions.Middle;
            quantity.enableWordWrapping = false;
            quantity.raycastTarget = false;
            quantity.overflowMode = TextOverflowModes.Overflow;

            Transform icon = instance.transform.Find("RowContent/ImageBG/Image");
            Image iconImage = icon != null ? icon.GetComponent<Image>() : null;
            if (iconImage != null && rowIcon != null)
            {
                iconImage.sprite = rowIcon;
                iconImage.preserveAspect = true;
                iconImage.color = Color.white;
            }

            CaptureOrbChoiceRowUGUI rowView = instance.GetComponent<CaptureOrbChoiceRowUGUI>()
                ?? instance.AddComponent<CaptureOrbChoiceRowUGUI>();
            CombatMoveRowItemUGUI moveRow = instance.GetComponent<CombatMoveRowItemUGUI>();
            SerializedObject rowSo = new SerializedObject(rowView);
            rowSo.FindProperty("moveRow").objectReferenceValue = moveRow;
            rowSo.FindProperty("quantityLabel").objectReferenceValue = quantity;
            rowSo.ApplyModifiedPropertiesWithoutUndo();
            instance.SetActive(false);
            report.AppendLine("Authored ChoiceTemplate from MoveRow");

            Transform nameGo = FindDeep(picker.transform, "OrbDetailName");
            if (nameGo != null)
                nameGo.gameObject.SetActive(false);
            Transform metaGo = FindDeep(picker.transform, "OrbMeta");
            if (metaGo != null)
                metaGo.gameObject.SetActive(false);

            Transform desc = FindDeep(picker.transform, "OrbDescription");
            if (desc != null)
            {
                RectTransform descRect = desc as RectTransform;
                descRect.anchorMin = new Vector2(0.06f, 0.08f);
                descRect.anchorMax = new Vector2(0.94f, 0.92f);
                descRect.offsetMin = Vector2.zero;
                descRect.offsetMax = Vector2.zero;
                descRect.anchoredPosition = Vector2.zero;
                TextMeshProUGUI descTmp = desc.GetComponent<TextMeshProUGUI>();
                if (descTmp != null)
                {
                    descTmp.alignment = TextAlignmentOptions.Center;
                    descTmp.horizontalAlignment = HorizontalAlignmentOptions.Center;
                    descTmp.verticalAlignment = VerticalAlignmentOptions.Middle;
                    descTmp.textWrappingMode = TextWrappingModes.Normal;
                }

                report.AppendLine("Centered OrbDescription");
            }

            Transform portraitTransform = FindDeep(picker.transform, "OrbPortrait");
            Image portraitImage = portraitTransform != null ? portraitTransform.GetComponent<Image>() : null;
            if (portraitImage != null && portrait != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.preserveAspect = true;
                portraitImage.color = Color.white;
                report.AppendLine("Assigned catalog portrait");
            }

            choices.anchorMin = choicesMin;
            choices.anchorMax = choicesMax;
            choices.anchoredPosition = choicesPos;
            choices.sizeDelta = choicesSize;
            choices.pivot = choicesPivot;
            detail.anchorMin = detailMin;
            detail.anchorMax = detailMax;
            detail.anchoredPosition = detailPos;
            detail.sizeDelta = detailSize;
            detail.pivot = detailPivot;

            PrefabUtility.SaveAsPrefabAsset(picker, PickerPrefabPath);
            report.AppendLine("Saved " + PickerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(picker);
        }

        GameObject canvas = PrefabUtility.LoadPrefabContents(CanvasPrefabPath);
        try
        {
            CaptureCommandCoordinatorUGUI coordinator = canvas.GetComponent<CaptureCommandCoordinatorUGUI>()
                ?? canvas.GetComponentInChildren<CaptureCommandCoordinatorUGUI>(true);
            if (coordinator != null)
            {
                SerializedObject so = new SerializedObject(coordinator);
                SetObject(so, "itemPresentationCatalog", catalog);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(coordinator);
                report.AppendLine("Wired presentation catalog on CombatBattleCanvas");
            }

            PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvas);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CaptureChallengePrefabAuthoring.AuthorCapturePickerRows]\n" + report);
        return report.ToString();
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static GameObject EnsureChallengePrefab(List<string> report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChallengePrefabPath);
        if (existing != null)
        {
            report.Add($"Challenge prefab already exists: {ChallengePrefabPath}");
            return existing;
        }

        var root = new GameObject(ChallengeRootName, typeof(RectTransform), typeof(CaptureChallengeController));
        try
        {
            Stretch(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            RectTransform overlay = CreateRect("Overlay", root.transform, Vector2.zero, Vector2.one);
            overlay.gameObject.AddComponent<CanvasGroup>();
            Image dim = overlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.02f, 0.04f, 0.92f);
            dim.raycastTarget = true;
            overlay.gameObject.AddComponent<CaptureHoldSurface>();

            RectTransform board = CreateRect("Board", overlay, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.82f));
            Image boardBack = board.gameObject.AddComponent<Image>();
            boardBack.color = new Color(0.02f, 0.07f, 0.1f, 1f);
            boardBack.raycastTarget = false;

            RectTransform artwork = CreateRect("GridArtwork", board, Vector2.zero, Vector2.one);
            Image artworkImage = artwork.gameObject.AddComponent<Image>();
            artworkImage.raycastTarget = false;
            artworkImage.enabled = false;

            RectTransform trace = CreateRect("TradingTrace", board, Vector2.zero, Vector2.one);
            CaptureTraceGraphic graph = trace.gameObject.AddComponent<CaptureTraceGraphic>();
            graph.raycastTarget = false;

            Slider meter = CreateSlider("CaptureMeter", overlay, new Vector2(0.12f, 0.13f), new Vector2(0.88f, 0.18f));
            TextMeshProUGUI meterLabel = CreateText("MeterLabel", overlay, new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.13f), "ESCAPE  50%  LOCK", 22f);
            TextMeshProUGUI timerLabel = CreateText("TimerLabel", overlay, new Vector2(0.44f, 0.84f), new Vector2(0.56f, 0.91f), "20.0", 32f);
            TextMeshProUGUI instruction = CreateText("Instruction", overlay, new Vector2(0.08f, 0.91f), new Vector2(0.92f, 0.99f), "", 20f);
            TextMeshProUGUI scanner = CreateText("ScannerIntel", overlay, new Vector2(0.7f, 0.82f), new Vector2(0.94f, 0.97f), "", 16f);
            scanner.alignment = TextAlignmentOptions.TopRight;
            TextMeshProUGUI outcome = CreateText("Outcome", overlay, new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.58f), "", 34f);

            CaptureChallengeController controller = root.GetComponent<CaptureChallengeController>();
            var so = new SerializedObject(controller);
            SetObject(so, "presentation", AssetDatabase.LoadAssetAtPath<CapturePresentationConfigSO>(PresentationPath));
            SetObject(so, "overlay", overlay.GetComponent<CanvasGroup>());
            SetObject(so, "graphArtwork", artworkImage);
            SetObject(so, "graph", graph);
            SetObject(so, "meter", meter);
            SetObject(so, "meterLabel", meterLabel);
            SetObject(so, "timerLabel", timerLabel);
            SetObject(so, "instructionLabel", instruction);
            SetObject(so, "scannerLabel", scanner);
            SetObject(so, "outcomeLabel", outcome);
            SetObject(so, "interactionSurface", overlay.GetComponent<CaptureHoldSurface>());
            so.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, ChallengePrefabPath);
            report.Add($"Created {ChallengePrefabPath}");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject EnsurePickerPrefab(List<string> report)
    {
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PickerPrefabPath);
        GameObject rootContents = existing != null
            ? PrefabUtility.LoadPrefabContents(PickerPrefabPath)
            : new GameObject(PickerRootName, typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(ProjectedPanelReveal));

        try
        {
            ApplyWalletPickerStyle(rootContents);
            if (existing == null)
            {
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(rootContents, PickerPrefabPath);
                report.Add($"Created {PickerPrefabPath}");
                return saved;
            }

            PrefabUtility.SaveAsPrefabAsset(rootContents, PickerPrefabPath);
            report.Add($"Updated wallet-projection layout on {PickerPrefabPath}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(PickerPrefabPath);
        }
        finally
        {
            if (existing != null)
                PrefabUtility.UnloadPrefabContents(rootContents);
            else
                Object.DestroyImmediate(rootContents);
        }
    }

    private static void ApplyWalletPickerStyle(GameObject root)
    {
        root.name = PickerRootName;
        root.layer = 5;
        RectTransform rect = root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>();
        ApplyWalletProjectionRect(rect);

        Transform authoredFrame = root.transform.Find("Frame");
        Image frame = authoredFrame != null
            ? authoredFrame.GetComponent<Image>() ?? authoredFrame.gameObject.AddComponent<Image>()
            : root.GetComponent<Image>() ?? root.AddComponent<Image>();
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelFramePath);
        if (authoredFrame == null && panelSprite != null)
            frame.sprite = panelSprite;
        frame.raycastTarget = false;
        if (authoredFrame == null)
        {
            frame.color = Color.white;
            frame.type = Image.Type.Simple;
            frame.preserveAspect = false;
        }

        CanvasGroup group = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        ProjectedPanelReveal reveal = root.GetComponent<ProjectedPanelReveal>() ?? root.AddComponent<ProjectedPanelReveal>();
        var revealSo = new SerializedObject(reveal);
        SetObject(revealSo, "canvasGroup", group);
        SetObject(revealSo, "panelRoot", rect);
        SerializedProperty fadeIn = revealSo.FindProperty("fadeInDuration");
        if (fadeIn != null) fadeIn.floatValue = 1.2f;
        SerializedProperty scaleIn = revealSo.FindProperty("scaleInDuration");
        if (scaleIn != null) scaleIn.floatValue = 0.75f;
        SerializedProperty flickerAmp = revealSo.FindProperty("flickerAmplitude");
        if (flickerAmp != null) flickerAmp.floatValue = 0.01f;
        revealSo.ApplyModifiedPropertiesWithoutUndo();

        TextMeshProUGUI title = EnsureText(root.transform, "Title", "SELECT ORB", 30f, new Vector2(0.045f, 0.82f), new Vector2(0.54f, 0.97f));
        EnsureText(root.transform, "Status", "", 18f, new Vector2(0.045f, 0.70f), new Vector2(0.54f, 0.82f));
        EnsureText(root.transform, "Footer", "A Select   B Back", 18f, new Vector2(0.045f, 0.02f), new Vector2(0.955f, 0.14f));

        RectTransform choices = root.transform.Find("Choices") as RectTransform;
        if (choices == null)
        {
            choices = EnsureRect(root.transform, "Choices", new Vector2(0.045f, 0.17f), new Vector2(0.535f, 0.69f));
            VerticalLayoutGroup layout = choices.GetComponent<VerticalLayoutGroup>() ?? choices.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(8, 8, 6, 6);
        }

        Button template = FindButton(choices, "ChoiceTemplate");
        if (template == null)
            template = CreateButton("ChoiceTemplate", choices, "ORB");
        template.gameObject.SetActive(false);

        Button cancel = FindButton(root.transform, "Cancel");
        if (cancel != null)
            Object.DestroyImmediate(cancel.gameObject);

        _ = title;
    }

    private static void ApplyWalletProjectionRect(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, -0.62f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1320f, 280f);
    }

    private static void WireIntoCombatBattleCanvas(GameObject challengePrefab, GameObject pickerPrefab, List<string> report)
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

            GameObject challengeInstance = EnsureNestedInstance(safeArea, ChallengeRootName, challengePrefab, report);
            Stretch(challengeInstance.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            challengeInstance.SetActive(false);

            Transform bottomPanel = FindDeep(root.transform, BottomActionPanelName);
            if (bottomPanel == null)
            {
                report.Add("ERROR: BottomActionPanel not found under CombatBattleCanvas");
                return;
            }

            GameObject pickerInstance = EnsureUnpackedChild(bottomPanel, PickerRootName, pickerPrefab, report);
            ApplyWalletPickerStyle(pickerInstance);
            pickerInstance.SetActive(false);

            CaptureCommandCoordinatorUGUI coordinator =
                root.GetComponent<CaptureCommandCoordinatorUGUI>() ??
                root.GetComponentInChildren<CaptureCommandCoordinatorUGUI>(true);
            if (coordinator == null)
            {
                coordinator = root.AddComponent<CaptureCommandCoordinatorUGUI>();
                report.Add("Added CaptureCommandCoordinatorUGUI to CombatBattleCanvas root");
            }

            CaptureChallengeController challenge = challengeInstance.GetComponent<CaptureChallengeController>();
            CanvasGroup picker = pickerInstance.GetComponent<CanvasGroup>();
            Button template = FindButton(pickerInstance.transform, "ChoiceTemplate");
            Transform choices = pickerInstance.transform.Find("Choices");

            var so = new SerializedObject(coordinator);
            SetObject(so, "challengePrefab", challengePrefab.GetComponent<CaptureChallengeController>());
            SetObject(so, "pickerPrefab", pickerPrefab.GetComponent<CanvasGroup>());
            SetObject(so, "challengeController", challenge);
            SetObject(so, "picker", picker);
            SetObject(so, "pickerTitle", FindText(pickerInstance.transform, "Title"));
            SetObject(so, "pickerStatus", FindText(pickerInstance.transform, "Status"));
            SetObject(so, "choicesRoot", choices as RectTransform);
            SetObject(so, "choiceTemplate", template);
            SetObject(so, "orbPortrait", FindDeep(pickerInstance.transform, "OrbPortrait")?.GetComponent<Image>());
            SetObject(so, "orbDetailName", FindText(pickerInstance.transform, "OrbDetailName"));
            SetObject(so, "orbDescription", FindText(pickerInstance.transform, "OrbDescription"));
            SetObject(so, "orbMeta", FindText(pickerInstance.transform, "OrbMeta"));
            SetObject(so, "projectedReveal", pickerInstance.GetComponent<ProjectedPanelReveal>());
            SetObject(so, "footerPrompt", FindText(pickerInstance.transform, "Footer"));
            SetObject(so, "itemPresentationCatalog", AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Data/UI/InventoryItemPresentationCatalog.asset"));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(coordinator);

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, CanvasPrefabPath);
            report.Add($"Saved {CanvasPrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject EnsureUnpackedChild(
        Transform parent,
        string childName,
        GameObject sourcePrefab,
        List<string> report)
    {
        Transform existing = FindDeep(parent.root, childName);
        if (existing != null && existing != parent)
        {
            Object.DestroyImmediate(existing.gameObject);
            report.Add($"Removed existing '{childName}' before authoring a native wallet panel");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, parent);
        instance.name = childName;
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.SetActive(false);
        report.Add($"Authored native '{childName}' under '{parent.name}'");
        return instance;
    }

    private static TextMeshProUGUI EnsureText(
        Transform parent,
        string name,
        string value,
        float size,
        Vector2 min,
        Vector2 max)
    {
        TextMeshProUGUI existing = FindText(parent, name);
        if (existing != null)
        {
            existing.text = value;
            existing.fontSize = size;
            existing.rectTransform.anchorMin = min;
            existing.rectTransform.anchorMax = max;
            existing.rectTransform.offsetMin = Vector2.zero;
            existing.rectTransform.offsetMax = Vector2.zero;
            existing.gameObject.layer = 5;
            return existing;
        }

        return CreateText(name, parent, min, max, value, size);
    }

    private static RectTransform EnsureRect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
        {
            RectTransform rect = existing as RectTransform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            existing.gameObject.layer = 5;
            return rect;
        }

        return CreateRect(name, parent, min, max);
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
            Object.DestroyImmediate(existing.gameObject);
            report.Add($"Removed existing '{childName}' under '{parent.name}' before nesting prefab");
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab, parent);
        instance.name = childName;
        instance.SetActive(false);
        report.Add($"Nested '{childName}' under '{parent.name}'");
        return instance;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        Vector2 min,
        Vector2 max,
        string value,
        float size)
    {
        RectTransform rect = CreateRect(name, parent, min, max);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;
        return text;
    }

    private static Slider CreateSlider(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform root = CreateRect(name, parent, min, max);
        Image background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.4f, 0.08f, 0.12f, 0.85f);
        background.raycastTarget = false;
        Slider slider = root.gameObject.AddComponent<Slider>();
        RectTransform fill = CreateRect("Fill", root, Vector2.zero, Vector2.one);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.2f, 0.95f, 0.55f, 0.95f);
        fillImage.raycastTarget = false;
        slider.fillRect = fill;
        slider.targetGraphic = fillImage;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.5f;
        slider.interactable = false;
        return slider;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.08f, 0.25f, 0.32f, 0.96f);
        go.GetComponent<LayoutElement>().preferredHeight = 68f;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        TextMeshProUGUI text = CreateText("Label", go.transform, Vector2.zero, Vector2.one, label, 21f);
        Stretch(text.rectTransform, new Vector2(12f, 4f), new Vector2(-12f, -4f));
        return button;
    }

    private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
    {
        if (rect == null)
            return;
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

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
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
}
#endif
