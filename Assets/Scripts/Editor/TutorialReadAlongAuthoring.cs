#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds tutorial read-along snippets, panel prefab, GameServices wiring, and
/// intro sequence beats. Idempotent.
/// Menu: Tools / Chainbound / UI / Author Tutorial Read-Along
/// </summary>
public static class TutorialReadAlongAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/UI/TutorialReadAlongPanel.prefab";
    private const string GameServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string SequencePath =
        "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_sequence.asset";
    private const string StepsFolder =
        "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_Steps";
    private const string SnippetFolder = "Assets/Data/UI";
    private const string PresenterChildName = "TutorialReadAlongPresenter";

    private const string ScanlinePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png";
    private const string FramePath = "Assets/Sprites/UI/Combat/MoveSelectPanel/9slicemoveselectoutline.png";
    private const string FontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
    private const string FontMaterialPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans_Underlay.mat";

    private static readonly Color DialogueBackground = new Color(1f, 1f, 1f, 0.76862746f);
    private static readonly Color DialogueFrame = new Color(0.21568628f, 0.30588236f, 0.2901961f, 1f);
    private static readonly Color DialogueText = new Color(0.78039217f, 0.8627451f, 0.8156863f, 1f);

    private const string MobileServicesPath = "Assets/Prefabs/Controls/MobileServices.prefab";
    private const string OutlineSpritePath = "Assets/Sprites/UI/PlayerMenu/9sliceborder_orange_smol.png";
    private const string OutlineChildName = "ControlEmphasisFrame";

    public const string MoveSnippetPath = SnippetFolder + "/TutorialReadAlong_Move.asset";
    public const string InteractSnippetPath = SnippetFolder + "/TutorialReadAlong_Interact.asset";
    public const string SprintSnippetPath = SnippetFolder + "/TutorialReadAlong_Sprint.asset";
    public const string JumpSnippetPath = SnippetFolder + "/TutorialReadAlong_Jump.asset";
    public const string MenuSnippetPath = SnippetFolder + "/TutorialReadAlong_Menu.asset";
    public const string MenuSnippetId = "tutorial.menu";
    public const string MenuSnippetTitle = "Player menu";
    public const string MenuSnippetBody = "Press Y to open the player menu.";
    public const string MoveStepPath = StepsFolder + "/intro_tutorial_move.asset";
    public const string InteractStepPath = StepsFolder + "/intro_tutorial_interact.asset";
    public const string SprintStepPath = StepsFolder + "/intro_tutorial_sprint.asset";
    public const string JumpStepPath = StepsFolder + "/intro_tutorial_jump.asset";

    [MenuItem("Tools/Chainbound/UI/Author Tutorial Read-Along")]
    public static void Author()
    {
        var report = new List<string>();
        TutorialReadAlongSnippetSO move = AuthorSnippet(
            MoveSnippetPath,
            "tutorial.move",
            "Move",
            "Use the D-pad or joystick to move around.",
            MobileHudEmphasisTarget.MovePad,
            report);
        TutorialReadAlongSnippetSO interact = AuthorSnippet(
            InteractSnippetPath,
            "tutorial.interact",
            "Interact",
            "Press A to interact.",
            MobileHudEmphasisTarget.InteractA,
            report);
        TutorialReadAlongSnippetSO sprint = AuthorSnippet(
            SprintSnippetPath,
            "tutorial.sprint",
            "Sprint",
            "Hold X to sprint. Double-tap X to lock it, then tap X again to stop.",
            MobileHudEmphasisTarget.SprintX,
            report);
        TutorialReadAlongSnippetSO jump = AuthorSnippet(
            JumpSnippetPath,
            "tutorial.jump",
            "Jump",
            "Press B to jump.",
            MobileHudEmphasisTarget.JumpB,
            report);
        AuthorSnippet(
            MenuSnippetPath,
            MenuSnippetId,
            MenuSnippetTitle,
            MenuSnippetBody,
            MobileHudEmphasisTarget.MenuY,
            report);

        AuthorPrefab(report);
        WireGameServices(report);

        Step_ShowTutorialReadAlong moveStep = AuthorIntroStep(
            MoveStepPath,
            "intro_tutorial_move",
            move,
            report);
        Step_ShowTutorialReadAlong interactStep = AuthorIntroStep(
            InteractStepPath,
            "intro_tutorial_interact",
            interact,
            report);
        Step_ShowTutorialReadAlong sprintStep = AuthorIntroStep(
            SprintStepPath,
            "intro_tutorial_sprint",
            sprint,
            report);
        Step_ShowTutorialReadAlong jumpStep = AuthorIntroStep(
            JumpStepPath,
            "intro_tutorial_jump",
            jump,
            report);

        SpliceIntroSequence(moveStep, interactStep, sprintStep, jumpStep, report);
        AuthorMobileHudCallouts(report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[TutorialReadAlongAuthoring]\n - " + string.Join("\n - ", report));
    }

    internal static TutorialReadAlongSnippetSO AuthorSnippet(
        string path,
        string snippetId,
        string title,
        string body,
        MobileHudEmphasisTarget emphasis,
        List<string> report)
    {
        TutorialReadAlongSnippetSO snippet = EnsureAsset<TutorialReadAlongSnippetSO>(path);
        SerializedObject serialized = new SerializedObject(snippet);
        serialized.FindProperty("snippetId").stringValue = snippetId;
        serialized.FindProperty("title").stringValue = title;
        serialized.FindProperty("body").stringValue = body;
        serialized.FindProperty("continueHint").stringValue = TutorialReadAlongSnippetSO.DefaultContinueHint;
        serialized.FindProperty("emphasis").enumValueIndex = (int)emphasis;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(snippet);
        report?.Add($"{snippetId} -> {path}");
        return snippet;
    }

    internal static void AuthorPrefab(List<string> report)
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (root == null)
        {
            root = new GameObject("TutorialReadAlongPanel", typeof(RectTransform));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            AuthorHierarchy(root);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report?.Add($"Prefab {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    internal static void WireGameServices(List<string> report)
    {
        GameObject services = PrefabUtility.LoadPrefabContents(GameServicesPath);
        if (services == null)
        {
            report?.Add($"FAILED to load {GameServicesPath}");
            return;
        }

        try
        {
            TutorialReadAlongPresenter existing = services.GetComponentInChildren<TutorialReadAlongPresenter>(true);
            if (existing != null)
            {
                report?.Add($"GameServices: presenter already on '{existing.gameObject.name}'");
            }
            else
            {
                GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (panelPrefab == null)
                {
                    report?.Add("GameServices: panel prefab missing");
                }
                else
                {
                    GameObject host = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, services.transform);
                    host.name = PresenterChildName;
                    report?.Add($"GameServices: instantiated {PrefabPath}");
                }
            }

            EditorUtility.SetDirty(services);
            PrefabUtility.SaveAsPrefabAsset(services, GameServicesPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(services);
        }
    }

    internal static Step_ShowTutorialReadAlong AuthorIntroStep(
        string path,
        string stepName,
        TutorialReadAlongSnippetSO snippet,
        List<string> report)
    {
        Step_ShowTutorialReadAlong step = EnsureAsset<Step_ShowTutorialReadAlong>(path);
        SerializedObject serialized = new SerializedObject(step);
        serialized.FindProperty("stepName").stringValue = stepName;
        serialized.FindProperty("executeOnce").boolValue = true;
        serialized.FindProperty("snippet").objectReferenceValue = snippet;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _ = step.StepGuid;
        EditorUtility.SetDirty(step);
        report?.Add($"{stepName} -> {path}");
        return step;
    }

    internal static void SpliceIntroSequence(
        SequenceStep moveStep,
        SequenceStep interactStep,
        SequenceStep sprintStep,
        SequenceStep jumpStep,
        List<string> report)
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(SequencePath);
        if (sequence == null)
            throw new InvalidOperationException($"Missing sequence at {SequencePath}");

        SequenceStep restoreCamera = AssetDatabase.LoadAssetAtPath<SequenceStep>(
            $"{StepsFolder}/intro_restorecamerafollow.asset");
        SequenceStep startEncounter = AssetDatabase.LoadAssetAtPath<SequenceStep>(
            $"{StepsFolder}/intro_startencounter_firstcreature.asset");
        SequenceStep ledgeDialogue = AssetDatabase.LoadAssetAtPath<SequenceStep>(
            $"{StepsFolder}/intro_playdial_ledge.asset");

        SerializedObject serialized = new SerializedObject(sequence);
        SerializedProperty steps = serialized.FindProperty("steps");
        InsertAfter(steps, restoreCamera, moveStep, report, "move");
        InsertAfter(steps, moveStep, interactStep, report, "interact");
        InsertAfter(steps, startEncounter, sprintStep, report, "sprint");
        InsertAfter(steps, ledgeDialogue, jumpStep, report, "jump");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    private static void AuthorHierarchy(GameObject root)
    {
        root.name = "TutorialReadAlongPanel";
        int uiLayer = LayerMask.NameToLayer("UI");
        if (uiLayer >= 0)
            root.layer = uiLayer;

        RectTransform rootRect = Require<RectTransform>(root);
        Stretch(rootRect);

        Canvas canvas = Require<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 530;

        CanvasScaler scaler = Require<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        Require<GraphicRaycaster>(root);

        CanvasGroup rootGroup = Require<CanvasGroup>(root);
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        GameObject safeArea = EnsureChild(root.transform, "SafeArea");
        Stretch((RectTransform)safeArea.transform);
        Require<SignalJam.UI.MobileControls.SafeAreaAdapter>(safeArea);

        GameObject dismiss = EnsureChild(safeArea.transform, "Dismiss");
        Stretch((RectTransform)dismiss.transform);
        Image dimmerImage = Require<Image>(dismiss);
        dimmerImage.color = new Color(0f, 0f, 0f, 0.62f);
        dimmerImage.raycastTarget = true;
        Button dismissButton = Require<Button>(dismiss);
        dismissButton.targetGraphic = dimmerImage;
        dismissButton.transition = Selectable.Transition.None;
        CanvasGroup dimmerGroup = Require<CanvasGroup>(dismiss);
        dimmerGroup.alpha = 1f;
        dimmerGroup.interactable = true;
        dimmerGroup.blocksRaycasts = true;

        GameObject card = EnsureChild(safeArea.transform, "Card");
        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(980f, 320f);

        Image cardImage = Require<Image>(card);
        cardImage.color = DialogueBackground;
        cardImage.raycastTarget = false;

        Texture scanlineTexture = AssetDatabase.LoadAssetAtPath<Texture>(ScanlinePath);
        GameObject scanlineOverlay = EnsureChild(card.transform, "ScanlineOverlay");
        Stretch((RectTransform)scanlineOverlay.transform);
        RawImage scanlineImage = Require<RawImage>(scanlineOverlay);
        scanlineImage.texture = scanlineTexture;
        scanlineImage.color = new Color(1f, 1f, 1f, 0.7254902f);
        scanlineImage.uvRect = new Rect(0f, 0f, 1f, 0.333f);
        scanlineImage.raycastTarget = false;
        scanlineOverlay.transform.SetAsFirstSibling();

        CombatUiScanlineBackground scanlines = Require<CombatUiScanlineBackground>(card);
        scanlines.Configure(
            enabled: true,
            texture: scanlineTexture,
            color: new Color(1f, 1f, 1f, 0.7254902f),
            tiling: new Vector2(1f, 0.333f),
            scrollDirection: Vector2.down,
            uvSpeed: 0.025f);

        GameObject frame = EnsureChild(card.transform, "CardFrame");
        Stretch((RectTransform)frame.transform, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        Image frameImage = Require<Image>(frame);
        frameImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePath);
        frameImage.type = Image.Type.Sliced;
        frameImage.color = DialogueFrame;
        frameImage.raycastTarget = false;
        frame.transform.SetAsLastSibling();

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Material fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);

        GameObject details = EnsureChild(card.transform, "Details");
        Stretch((RectTransform)details.transform, new Vector2(40f, 28f), new Vector2(-40f, -28f));
        CanvasGroup detailsGroup = Require<CanvasGroup>(details);

        TextMeshProUGUI title = EnsureText(
            details.transform,
            "Title",
            font,
            fontMaterial,
            36f,
            FontStyles.Bold,
            TextAlignmentOptions.TopLeft,
            DialogueText,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0f, -8f),
            new Vector2(0f, 56f),
            new Vector2(0.5f, 1f));
        TextMeshProUGUI body = EnsureText(
            details.transform,
            "Body",
            font,
            fontMaterial,
            24f,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            DialogueText,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0f, 36f),
            new Vector2(0f, -72f),
            new Vector2(0.5f, 0.5f));
        TextMeshProUGUI hint = EnsureText(
            details.transform,
            "Hint",
            font,
            fontMaterial,
            18f,
            FontStyles.Italic,
            TextAlignmentOptions.BottomLeft,
            new Color(DialogueText.r, DialogueText.g, DialogueText.b, 0.8f),
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 0f),
            new Vector2(0f, 32f),
            new Vector2(0.5f, 0f));
        CanvasGroup titleGroup = Require<CanvasGroup>(title.gameObject);

        HideNamedChild(card.transform, "Portrait");
        HideNamedChild(card.transform, "PortraitBG");
        HideNamedChild(card.transform, "Quantity");

        ItemObtainPanelReveal reveal = Require<ItemObtainPanelReveal>(root);
        reveal.Configure(cardRect, rootGroup, dimmerGroup, titleGroup, new[] { detailsGroup });

        TutorialReadAlongPresenter presenter = Require<TutorialReadAlongPresenter>(root);
        SerializedObject presenterSerialized = new SerializedObject(presenter);
        SetObject(presenterSerialized, "canvasGroup", rootGroup);
        SetObject(presenterSerialized, "titleText", title);
        SetObject(presenterSerialized, "bodyText", body);
        SetObject(presenterSerialized, "hintText", hint);
        SetObject(presenterSerialized, "dismissButton", dismissButton);
        SetObject(presenterSerialized, "panelReveal", reveal);
        presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursively(root.transform, root.layer);
        root.SetActive(true);
        EditorUtility.SetDirty(reveal);
        EditorUtility.SetDirty(presenter);
    }

    internal static void AuthorMobileHudCallouts(List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MobileServicesPath);
        if (root == null)
        {
            report?.Add($"FAILED to load {MobileServicesPath}");
            return;
        }

        try
        {
            Transform movePad = FindDeep(root.transform, "MovementPad");
            Transform interactButton = FindDeep(root.transform, "ButtonA");
            Transform sprintButton = FindDeep(root.transform, "ButtonX");
            Transform menuButton = FindDeep(root.transform, "ButtonY");
            Transform jumpButton = FindDeep(root.transform, "ButtonB");
            Transform hudRoot = FindDeep(root.transform, "HudRoot");
            if (movePad == null || interactButton == null || sprintButton == null || menuButton == null || jumpButton == null || hudRoot == null)
                throw new InvalidOperationException("MobileServices is missing MovementPad, ButtonA, ButtonB, ButtonX, ButtonY, or HudRoot.");

            Image moveOutline = EnsureEmphasisOutline(movePad);
            Image interactOutline = EnsureEmphasisOutline(interactButton);
            Image sprintOutline = EnsureEmphasisOutline(sprintButton);
            Image menuOutline = EnsureEmphasisOutline(menuButton);
            Image jumpOutline = EnsureEmphasisOutline(jumpButton);

            MobileHudControlEmphasis emphasis = hudRoot.GetComponent<MobileHudControlEmphasis>();
            if (emphasis == null)
                emphasis = hudRoot.gameObject.AddComponent<MobileHudControlEmphasis>();

            SerializedObject emphasisSerialized = new SerializedObject(emphasis);
            emphasisSerialized.FindProperty("movePad").objectReferenceValue = movePad as RectTransform;
            emphasisSerialized.FindProperty("moveOutline").objectReferenceValue = moveOutline;
            emphasisSerialized.FindProperty("interactButton").objectReferenceValue = interactButton as RectTransform;
            emphasisSerialized.FindProperty("interactOutline").objectReferenceValue = interactOutline;
            emphasisSerialized.FindProperty("sprintButton").objectReferenceValue = sprintButton as RectTransform;
            emphasisSerialized.FindProperty("sprintOutline").objectReferenceValue = sprintOutline;
            emphasisSerialized.FindProperty("menuButton").objectReferenceValue = menuButton as RectTransform;
            emphasisSerialized.FindProperty("menuOutline").objectReferenceValue = menuOutline;
            emphasisSerialized.FindProperty("jumpButton").objectReferenceValue = jumpButton as RectTransform;
            emphasisSerialized.FindProperty("jumpOutline").objectReferenceValue = jumpOutline;
            emphasisSerialized.FindProperty("pulseCount").intValue = MobileHudControlEmphasis.DefaultPulseCount;
            emphasisSerialized.ApplyModifiedPropertiesWithoutUndo();

            MobileInputManager input = root.GetComponentInChildren<MobileInputManager>(true);
            if (input == null)
                throw new InvalidOperationException("MobileServices is missing MobileInputManager.");

            SerializedObject inputSerialized = new SerializedObject(input);
            SerializedProperty window = inputSerialized.FindProperty("doubleTapWindowSeconds");
            if (window != null && window.floatValue <= 0f)
                window.floatValue = 0.32f;
            inputSerialized.ApplyModifiedPropertiesWithoutUndo();

            AuthorAutoSprintLabel(hudRoot, input);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, MobileServicesPath);
            report?.Add($"Mobile HUD callouts -> {MobileServicesPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AuthorAutoSprintLabel(Transform hudRoot, MobileInputManager input)
    {
        GameObject host = EnsureChild(hudRoot, "AutoSprintIndicator");
        RectTransform hostRect = (RectTransform)host.transform;
        Stretch(hostRect);
        host.layer = hudRoot.gameObject.layer;
        host.SetActive(true);

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Material fontMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontMaterialPath);
        TextMeshProUGUI label = EnsureText(
            host.transform,
            "Label",
            font,
            fontMaterial,
            18f,
            FontStyles.Bold,
            TextAlignmentOptions.Center,
            new Color(1f, 0.62f, 0.22f, 1f),
            new Vector2(0.21f, 0.34f),
            new Vector2(0.79f, 0.4f),
            Vector2.zero,
            Vector2.zero,
            new Vector2(0.5f, 0.5f));
        label.text = AutoSprintIndicator.DefaultLabel;
        label.gameObject.layer = host.layer;
        label.gameObject.SetActive(false);

        AutoSprintIndicator indicator = host.GetComponent<AutoSprintIndicator>();
        if (indicator == null)
            indicator = host.AddComponent<AutoSprintIndicator>();

        SerializedObject serialized = new SerializedObject(indicator);
        serialized.FindProperty("input").objectReferenceValue = input;
        serialized.FindProperty("label").objectReferenceValue = label;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(indicator);
    }

    private static Image EnsureEmphasisOutline(Transform parent)
    {
        GameObject frame = EnsureChild(parent, OutlineChildName);
        RectTransform rect = (RectTransform)frame.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-8f, -8f);
        rect.offsetMax = new Vector2(8f, 8f);
        rect.localScale = Vector3.one;

        Image image = Require<Image>(frame);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(OutlineSpritePath);
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
        frame.layer = parent.gameObject.layer;
        frame.SetActive(false);
        frame.transform.SetAsLastSibling();
        return image;
    }

    private static void InsertAfter(
        SerializedProperty steps,
        SequenceStep after,
        SequenceStep insert,
        List<string> report,
        string label)
    {
        if (after == null || insert == null)
            throw new InvalidOperationException($"Cannot splice '{label}' — missing after/insert step.");

        int afterIndex = -1;
        int insertIndex = -1;
        for (int i = 0; i < steps.arraySize; i++)
        {
            UnityEngine.Object value = steps.GetArrayElementAtIndex(i).objectReferenceValue;
            if (value == after && afterIndex < 0)
                afterIndex = i;
            if (value == insert)
                insertIndex = i;
        }

        if (afterIndex < 0)
            throw new InvalidOperationException($"Cannot splice '{label}' — after-step is not in the sequence.");

        if (insertIndex == afterIndex + 1)
        {
            report?.Add($"Sequence '{label}' already after target.");
            return;
        }

        if (insertIndex >= 0)
        {
            steps.DeleteArrayElementAtIndex(insertIndex);
            if (insertIndex < afterIndex)
                afterIndex--;
        }

        int insertAt = afterIndex + 1;
        if (insertAt >= steps.arraySize)
        {
            steps.arraySize++;
            steps.GetArrayElementAtIndex(steps.arraySize - 1).objectReferenceValue = insert;
        }
        else
        {
            steps.InsertArrayElementAtIndex(insertAt);
            steps.GetArrayElementAtIndex(insertAt).objectReferenceValue = insert;
        }

        report?.Add($"Sequence spliced '{label}' after index {afterIndex}.");
    }

    private static TextMeshProUGUI EnsureText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        Material fontMaterial,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        Color color,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta,
        Vector2 pivot)
    {
        GameObject go = EnsureChild(parent, name);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = Require<TextMeshProUGUI>(go);
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        if (fontMaterial != null)
            text.fontSharedMaterial = fontMaterial;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void HideNamedChild(Transform root, string name)
    {
        Transform existing = FindDeep(root, name);
        if (existing != null)
            existing.gameObject.SetActive(false);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;
        if (root.name == name)
            return root;

        Transform direct = root.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static T Require<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static void SetObject(SerializedObject serialized, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    private static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
#endif
