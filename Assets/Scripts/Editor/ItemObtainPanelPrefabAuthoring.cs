#if UNITY_EDITOR
using System;
using Chainbound.Combat;
using SignalJam.UI.Audio;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the editable ItemObtainPanel hierarchy from the presentation used by
/// the shared dialogue panel. The generated asset remains a normal prefab that
/// can be opened and adjusted in Prefab Mode.
/// </summary>
public static class ItemObtainPanelPrefabAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/UI/ItemObtainPanel.prefab";
    private const string ScanlinePath = "Assets/Sprites/Combat/BattleDevice/SCANLINE_FRAME_PMROOT.png";
    private const string FramePath = "Assets/Sprites/UI/Combat/MoveSelectPanel/9slicemoveselectoutline.png";
    private const string FontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";
    private const string FontMaterialPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans_Underlay.mat";
    private const string AudioProfilePath = "Assets/Data/Audio/UI/ItemObtainSfxProfile.asset";

    private static readonly Color DialogueBackground = new Color(1f, 1f, 1f, 0.76862746f);
    private static readonly Color DialogueFrame = new Color(0.21568628f, 0.30588236f, 0.2901961f, 1f);
    private static readonly Color DialogueText = new Color(0.78039217f, 0.8627451f, 0.8156863f, 1f);

    [MenuItem("Tools/Chainbound/UI/Author Item Obtain Panel")]
    public static void Author()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError($"[ItemObtainPanelAuthoring] Could not load {PrefabPath}.");
            return;
        }

        try
        {
            AuthorHierarchy(root);
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[ItemObtainPanelAuthoring] Authored {PrefabPath}. Open it in Prefab Mode to tune the layout.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AuthorHierarchy(GameObject root)
    {
        root.name = "ItemObtainPanel";
        root.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : root.layer;

        RectTransform rootRect = Require<RectTransform>(root);
        Stretch(rootRect);

        Canvas canvas = Require<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 520;

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
        cardRect.sizeDelta = new Vector2(1180f, 380f);

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

        Image portraitImage = BindAuthoredPortrait(card.transform);
        CanvasGroup portraitGroup = portraitImage != null
            ? Require<CanvasGroup>(ResolveContentHost(portraitImage.transform, "PortraitBG").gameObject)
            : null;

        TextMeshProUGUI title;
        TextMeshProUGUI prompt;
        CanvasGroup detailsGroup;
        BindAuthoredDetails(card.transform, font, fontMaterial, out title, out prompt, out detailsGroup);
        CanvasGroup titleGroup = title != null ? Require<CanvasGroup>(title.gameObject) : null;

        HideNamedChild(card.transform, "Quantity");

        ItemObtainPanelReveal reveal = Require<ItemObtainPanelReveal>(root);
        var contentGroups = new System.Collections.Generic.List<CanvasGroup>();
        if (portraitGroup != null)
            contentGroups.Add(portraitGroup);
        if (detailsGroup != null)
            contentGroups.Add(detailsGroup);
        else if (prompt != null)
            contentGroups.Add(Require<CanvasGroup>(prompt.gameObject));
        reveal.Configure(cardRect, rootGroup, dimmerGroup, titleGroup, contentGroups.ToArray());

        ItemObtainPresenter presenter = Require<ItemObtainPresenter>(root);
        SerializedObject presenterSerialized = new SerializedObject(presenter);
        SetObject(presenterSerialized, "canvasGroup", rootGroup);
        SetObject(presenterSerialized, "portraitImage", portraitImage);
        SetObject(presenterSerialized, "titleText", title);
        SetObject(presenterSerialized, "quantityText", null);
        SetObject(presenterSerialized, "promptText", prompt);
        SetObject(presenterSerialized, "dismissButton", dismissButton);
        SetObject(presenterSerialized, "panelReveal", reveal);
        SetObject(
            presenterSerialized,
            "obtainSfxProfile",
            AssetDatabase.LoadAssetAtPath<ItemObtainSfxProfileSO>(AudioProfilePath));
        presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

        SetLayerRecursively(root.transform, root.layer);
        root.SetActive(true);
        EditorUtility.SetDirty(reveal);
        EditorUtility.SetDirty(presenter);
    }

    private static Image BindAuthoredPortrait(Transform card)
    {
        Transform portrait = FindDeep(card, "Portrait");
        Transform portraitBg = FindDeep(card, "PortraitBG");
        if (portrait == null)
        {
            Transform parent = portraitBg != null ? portraitBg : card;
            GameObject created = EnsureChild(parent, "Portrait");
            RectTransform rect = (RectTransform)created.transform;
            if (portraitBg != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(25f, 25f);
                rect.offsetMax = new Vector2(-25f, -25f);
            }
            portrait = created.transform;
        }

        Image image = Require<Image>(portrait.gameObject);
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static void BindAuthoredDetails(
        Transform card,
        TMP_FontAsset font,
        Material fontMaterial,
        out TextMeshProUGUI title,
        out TextMeshProUGUI prompt,
        out CanvasGroup detailsGroup)
    {
        Transform details = FindDeep(card, "Details");
        if (details == null)
        {
            details = EnsureChild(card, "Details").transform;
            Stretch((RectTransform)details.transform);
        }

        detailsGroup = Require<CanvasGroup>(details.gameObject);

        Transform titleTransform = FindDeep(details, "Title") ?? FindDeep(card, "Title");
        if (titleTransform != null && titleTransform.parent != details)
            titleTransform.SetParent(details, true);
        if (titleTransform == null)
        {
            title = EnsureText(
                details,
                "Title",
                font,
                fontMaterial,
                36f,
                FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft,
                DialogueText,
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(35f, 75f),
                new Vector2(725f, 140f));
        }
        else
        {
            title = Require<TextMeshProUGUI>(titleTransform.gameObject);
            title.richText = true;
            // Keep prefab vertex color. Runtime only tints the item name via rich text.
        }

        Transform promptTransform = FindDeep(details, "Prompt") ?? FindDeep(card, "Prompt");
        if (promptTransform != null && promptTransform.parent != details)
            promptTransform.SetParent(details, true);
        if (promptTransform == null)
        {
            prompt = EnsureText(
                details,
                "Prompt",
                font,
                fontMaterial,
                22f,
                FontStyles.Normal,
                TextAlignmentOptions.TopLeft,
                new Color(DialogueText.r, DialogueText.g, DialogueText.b, 0.8f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(35f, -55f),
                new Vector2(726f, 96f));
        }
        else
        {
            prompt = Require<TextMeshProUGUI>(promptTransform.gameObject);
            prompt.richText = true;
            // Keep prefab vertex color. Description copy is uncolored.
        }
    }

    private static Transform ResolveContentHost(Transform authored, string preferredAncestorName)
    {
        if (authored == null)
            return null;

        Transform current = authored;
        while (current != null)
        {
            if (string.Equals(current.name, preferredAncestorName, StringComparison.OrdinalIgnoreCase))
                return current;
            current = current.parent;
        }

        return authored;
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

    private static void HideNamedChild(Transform root, string name)
    {
        Transform existing = FindDeep(root, name);
        if (existing != null)
            existing.gameObject.SetActive(false);
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
        Vector2 size)
    {
        GameObject go = EnsureChild(parent, name);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

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

    private static void HideChild(Transform parent, string name)
    {
        HideNamedChild(parent, name);
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
}
#endif
