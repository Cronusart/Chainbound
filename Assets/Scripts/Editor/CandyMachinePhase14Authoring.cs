#if UNITY_EDITOR
using System;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CandyMachinePhase14Authoring
{
    private const string PrefabPath = "Assets/Prefabs/UI/CandyMachine/CandyMachinePanel.prefab";
    private const string ScenePath = "Assets/Scenes/Academy_Interior.unity";
    private const string ShotPath = "Assets/Data/Camera/Shots/CandyMachine_PullShot.asset";
    private const string MaterialPath = "Assets/Materials/CandyMachine/CandyMachine_EmissiveSphere.mat";
    private const string StingerPath = "Assets/Data/Audio/SFX/LIBRARY_STAMP.asset";
    private const string ImpactStingerPath = "Assets/Data/Audio/SFX/LIBRARY_STAMP_IMPACT.asset";
    private const string SlideStingerPath = "Assets/Data/Audio/SFX/LIBRARY_CARD_SLIDE.asset";
    private const string TypeStickerPath = "Assets/Data/Audio/SFX/TYPE_STICKER.asset";

    [MenuItem("Tools/Chainbound/UI/Apply Candy Machine Phases 1-4")]
    [Unity.Pipeline.Commands.CliCommand("author_candy_machine_phases_1_4", "Author Candy Machine UI, pull assets, and Academy scene wiring")]
    public static void Apply()
    {
        CameraShotPresetSO shot = EnsureShot();
        Material material = EnsureEmissiveMaterial();
        AuthorPrefab();
        AuthorScene(shot, material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CandyMachinePhase14Authoring] Authored prefab, pull shot, emissive material, and Academy scene wiring.");
    }

    private static void AuthorPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            CandyMachineScreen screen = root.GetComponent<CandyMachineScreen>();
            CanvasGroup presentation = GetOrAdd<CanvasGroup>(root);
            Transform safeContent = root.transform.Find("SafeAreaPanel/SafeContent");
            Transform body = safeContent.Find("BodyRow");
            CanvasGroup bodyGroup = GetOrAdd<CanvasGroup>(body.gameObject);
            LayoutElement bodyLayout = GetOrAdd<LayoutElement>(body.gameObject);
            bodyLayout.preferredHeight = -1f;
            bodyLayout.flexibleHeight = 1f;

            Transform nested = body.Find("DetailColumn/DetailBody");
            if (nested != null)
                UnityEngine.Object.DestroyImmediate(nested.gameObject);

            TMP_Text textTemplate = body.GetComponentInChildren<TMP_Text>(true);
            Image panelTemplate = body.Find("DetailColumn")?.GetComponent<Image>()
                ?? body.Find("Slot_Detail/DetailColumn")?.GetComponent<Image>();
            Button buttonTemplate = body.Find("DetailColumn/ButtonRow/PrimaryButton")?.GetComponent<Button>()
                ?? body.Find("Slot_Detail/DetailColumn/ButtonRow/PrimaryButton")?.GetComponent<Button>();

            Transform oldGrant = safeContent.Find("DetailBody/DevGrantStrip")
                ?? safeContent.Find("DevGrantPanel")
                ?? root.transform.Find("SafeAreaPanel/DevGrantPanel");
            if (oldGrant != null && oldGrant.IsChildOf(safeContent.Find("DetailBody")))
                oldGrant.SetParent(safeContent, false);

            Transform staleDetail = safeContent.Find("DetailBody");
            if (staleDetail != null)
                UnityEngine.Object.DestroyImmediate(staleDetail.gameObject);
            GameObject detailObject = CreateUiObject("DetailBody", safeContent);
            Image detailImage = detailObject.AddComponent<Image>();
            CopyImage(panelTemplate, detailImage);
            CanvasGroup detailGroup = detailObject.AddComponent<CanvasGroup>();
            LayoutElement detailLayout = detailObject.AddComponent<LayoutElement>();
            detailLayout.preferredHeight = 184f;
            detailLayout.flexibleHeight = 0f;
            VerticalLayoutGroup detailStack = detailObject.AddComponent<VerticalLayoutGroup>();
            detailStack.padding = new RectOffset(28, 28, 18, 18);
            detailStack.spacing = 8f;
            detailStack.childControlWidth = true;
            detailStack.childControlHeight = true;
            detailStack.childForceExpandWidth = true;
            detailStack.childForceExpandHeight = false;
            TMP_Text detailTitle = CreateText("Title", detailObject.transform, textTemplate, "FOLIO", 28f, 32f, TextAlignmentOptions.MidlineLeft);
            TMP_Text detailSubtitle = CreateText("Subtitle", detailObject.transform, textTemplate, string.Empty, 18f, 28f, TextAlignmentOptions.MidlineLeft);
            TMP_Text detailBody = CreateText("Body", detailObject.transform, textTemplate, string.Empty, 20f, 72f, TextAlignmentOptions.TopLeft);
            TMP_Text detailHint = CreateText("Hint", detailObject.transform, textTemplate, "PRESS A TO PULL", 18f, 30f, TextAlignmentOptions.MidlineRight);
            detailBody.color = PlayerMenuUiFactory.Muted;
            detailHint.color = PlayerMenuUiFactory.Muted;
            CandyMachineDetailBodyView detailView = detailObject.AddComponent<CandyMachineDetailBodyView>();
            detailView.Bind(detailGroup, detailTitle, detailSubtitle, detailBody, detailHint);

            if (oldGrant != null)
            {
                oldGrant.SetParent(detailObject.transform, false);
                oldGrant.name = "DevGrantStrip";
                oldGrant.gameObject.SetActive(false);
            }

            Transform staleResult = safeContent.Find("ResultPanel");
            if (staleResult != null)
                UnityEngine.Object.DestroyImmediate(staleResult.gameObject);
            GameObject resultObject = CreateUiObject("ResultPanel", safeContent);
            Image resultImage = resultObject.AddComponent<Image>();
            CopyImage(panelTemplate, resultImage);
            resultImage.color = new Color(0.08f, 0.105f, 0.15f, 0f);
            resultImage.raycastTarget = false;
            CanvasGroup resultGroup = resultObject.AddComponent<CanvasGroup>();
            LayoutElement resultLayout = resultObject.AddComponent<LayoutElement>();
            resultLayout.preferredHeight = -1f;
            resultLayout.flexibleHeight = 1f;

            Sprite front = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/LibraryCards/librarycard_frontUI.png");
            Sprite outline = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/LibraryCards/librarycard_outline.png");
            TMP_FontAsset stampFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset");

            GameObject cardObject = CreateUiObject("LibraryCardRoot", resultObject.transform);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(LibraryCardLayout.CardWidth, LibraryCardLayout.CardHeight);

            Image cardFace = CreateStampImage("CardFace", cardObject.transform, front);
            Stretch(cardFace.rectTransform);
            cardFace.preserveAspect = true;

            Image typeWell = CreateStampImage("TypeWell", cardObject.transform, null);
            Image moveName = CreateStampImage("MoveNameStamp", cardObject.transform, null);
            Image stats = CreateStampImage("StatsStamp", cardObject.transform, null);
            Image mod1 = CreateStampImage("Mod1Stamp", cardObject.transform, null);
            Image mod2 = CreateStampImage("Mod2Stamp", cardObject.transform, null);
            Image effect = CreateStampImage("EffectStamp", cardObject.transform, null);

            GameObject rarityRow = CreateUiObject("RarityRow", cardObject.transform);
            Image moveDot = CreateStampImage("MoveDot", rarityRow.transform, null);
            Image mod1Dot = CreateStampImage("Mod1Dot", rarityRow.transform, null);
            Image mod2Dot = CreateStampImage("Mod2Dot", rarityRow.transform, null);

            Image flash = CreateStampImage("OutlineFlash", cardObject.transform, outline);
            Stretch(flash.rectTransform);
            flash.color = new Color(1f, 1f, 1f, 0f);

            Button continueButton = UnityEngine.Object.Instantiate(buttonTemplate, resultObject.transform);
            continueButton.name = "ContinueButton";
            continueButton.onClick = new Button.ButtonClickedEvent();
            continueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            RectTransform continueRect = continueButton.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.5f, 0f);
            continueRect.anchorMax = new Vector2(0.5f, 0f);
            continueRect.pivot = new Vector2(0.5f, 0f);
            continueRect.anchoredPosition = new Vector2(0f, 12f);
            continueRect.sizeDelta = new Vector2(420f, 72f);
            LayoutElement continueLayout = GetOrAdd<LayoutElement>(continueButton.gameObject);
            continueLayout.ignoreLayout = true;
            TMP_Text continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
            if (continueLabel != null) continueLabel.text = "CONTINUE";
            continueButton.gameObject.SetActive(false);

            LibraryCardResultPresenter presenter = cardObject.AddComponent<LibraryCardResultPresenter>();
            presenter.Bind(
                cardRect,
                cardFace,
                typeWell,
                moveName,
                stats,
                mod1,
                mod2,
                effect,
                moveDot,
                mod1Dot,
                mod2Dot,
                flash,
                stampFont);

            CandyMachineResultView resultView = resultObject.AddComponent<CandyMachineResultView>();
            resultView.Bind(resultGroup, presenter, continueButton);
            resultObject.transform.SetSiblingIndex(detailObject.transform.GetSiblingIndex());
            resultObject.SetActive(false);

            body.SetSiblingIndex(0);
            resultObject.transform.SetSiblingIndex(1);
            detailObject.transform.SetSiblingIndex(2);

            SerializedObject serialized = new SerializedObject(screen);
            serialized.FindProperty("presentationGroup").objectReferenceValue = presentation;
            serialized.FindProperty("bodyRow").objectReferenceValue = bodyGroup;
            serialized.FindProperty("detailBodyView").objectReferenceValue = detailView;
            serialized.FindProperty("resultView").objectReferenceValue = resultView;
            serialized.FindProperty("revealStinger").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(StingerPath);
            serialized.FindProperty("revealImpactStinger").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ImpactStingerPath);
            serialized.FindProperty("revealSlideStinger").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SlideStingerPath);
            serialized.FindProperty("revealTypeSticker").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TypeStickerPath);
            serialized.FindProperty("allowEditorShellRebuild").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    [MenuItem("Tools/Chainbound/UI/Apply Library Card Result Panel")]
    public static void ApplyLibraryCardResult()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            Transform safeContent = root.transform.Find("SafeAreaPanel/SafeContent");
            Transform result = safeContent.Find("ResultPanel");
            if (result == null)
                throw new InvalidOperationException("ResultPanel is missing from CandyMachinePanel.");

            Button template = null;
            Transform primary = root.transform.Find("SafeAreaPanel/SafeContent/BodyRow/Slot_Detail/DetailColumn/ButtonRow/PrimaryButton")
                ?? root.transform.Find("SafeAreaPanel/SafeContent/BodyRow/DetailColumn/ButtonRow/PrimaryButton");
            if (primary != null)
                template = primary.GetComponent<Button>();

            Transform stale = result.Find("Card");
            if (stale != null)
                UnityEngine.Object.DestroyImmediate(stale.gameObject);
            Transform oldCard = result.Find("LibraryCardRoot");
            if (oldCard != null)
                UnityEngine.Object.DestroyImmediate(oldCard.gameObject);
            Transform oldContinue = result.Find("ContinueButton");
            if (oldContinue != null)
                UnityEngine.Object.DestroyImmediate(oldContinue.gameObject);

            CanvasGroup resultGroup = GetOrAdd<CanvasGroup>(result.gameObject);
            Image panelTemplate = result.GetComponent<Image>();
            if (panelTemplate != null)
                panelTemplate.raycastTarget = false;

            Sprite front = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/LibraryCards/librarycard_frontUI.png");
            Sprite outline = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/LibraryCards/librarycard_outline.png");
            TMP_FontAsset stampFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset");

            GameObject cardObject = CreateUiObject("LibraryCardRoot", result);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(LibraryCardLayout.CardWidth, LibraryCardLayout.CardHeight);

            Image cardFace = CreateStampImage("CardFace", cardObject.transform, front);
            Stretch(cardFace.rectTransform);
            cardFace.preserveAspect = true;
            Image typeWell = CreateStampImage("TypeWell", cardObject.transform, null);
            Image moveName = CreateStampImage("MoveNameStamp", cardObject.transform, null);
            Image stats = CreateStampImage("StatsStamp", cardObject.transform, null);
            Image mod1 = CreateStampImage("Mod1Stamp", cardObject.transform, null);
            Image mod2 = CreateStampImage("Mod2Stamp", cardObject.transform, null);
            Image effect = CreateStampImage("EffectStamp", cardObject.transform, null);
            GameObject rarityRow = CreateUiObject("RarityRow", cardObject.transform);
            Image moveDot = CreateStampImage("MoveDot", rarityRow.transform, null);
            Image mod1Dot = CreateStampImage("Mod1Dot", rarityRow.transform, null);
            Image mod2Dot = CreateStampImage("Mod2Dot", rarityRow.transform, null);
            Image flash = CreateStampImage("OutlineFlash", cardObject.transform, outline);
            Stretch(flash.rectTransform);
            flash.color = new Color(1f, 1f, 1f, 0f);

            if (template == null)
                throw new InvalidOperationException("No button template found for the library card continue control.");
            Button continueButton = UnityEngine.Object.Instantiate(template, result);
            continueButton.name = "ContinueButton";
            continueButton.onClick = new Button.ButtonClickedEvent();
            continueButton.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            RectTransform continueRect = continueButton.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.5f, 0f);
            continueRect.anchorMax = new Vector2(0.5f, 0f);
            continueRect.pivot = new Vector2(0.5f, 0f);
            continueRect.anchoredPosition = new Vector2(0f, 12f);
            continueRect.sizeDelta = new Vector2(420f, 72f);
            LayoutElement continueLayout = GetOrAdd<LayoutElement>(continueButton.gameObject);
            continueLayout.ignoreLayout = true;
            TMP_Text continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
            if (continueLabel != null)
                continueLabel.text = "CONTINUE";
            continueButton.gameObject.SetActive(false);

            LibraryCardResultPresenter presenter = cardObject.AddComponent<LibraryCardResultPresenter>();
            presenter.Bind(cardRect, cardFace, typeWell, moveName, stats, mod1, mod2, effect, moveDot, mod1Dot, mod2Dot, flash, stampFont);
            CandyMachineResultView resultView = GetOrAdd<CandyMachineResultView>(result.gameObject);
            resultView.Bind(resultGroup, presenter, continueButton);
            result.gameObject.SetActive(false);

            CandyMachineScreen screen = root.GetComponent<CandyMachineScreen>();
            SerializedObject serialized = new SerializedObject(screen);
            serialized.FindProperty("resultView").objectReferenceValue = resultView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AuthorScene(CameraShotPresetSO shot, Material material)
    {
        CandyMachineAuthoring.EnsurePresentationSoundEvents(
            out SoundEventSO leverSound,
            out SoundEventSO glowSound,
            out SoundEventSO doorSound);
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject machine = FindNamed(scene, CandyMachineIds.MachineObjectName);
            if (machine == null)
                throw new InvalidOperationException("Candy_Machine was not found in Academy_Interior.");

            CandyMachinePullDirector director = GetOrAdd<CandyMachinePullDirector>(machine);
            Transform emissive = machine.transform.Find("Spheres/emissivesphere");
            Renderer emissiveRenderer = emissive != null ? emissive.GetComponent<Renderer>() : null;
            if (emissiveRenderer != null && material != null)
                emissiveRenderer.sharedMaterial = material;

            SerializedObject directorSerialized = new SerializedObject(director);
            directorSerialized.FindProperty("leverRoot").objectReferenceValue = machine.transform.Find("Lever_Root");
            directorSerialized.FindProperty("spheresRoot").objectReferenceValue = machine.transform.Find("Spheres");
            directorSerialized.FindProperty("emissiveRenderer").objectReferenceValue = emissiveRenderer;
            directorSerialized.FindProperty("candyMachineDoor").objectReferenceValue = machine.transform.Find("CandyMachineDoor");
            directorSerialized.FindProperty("cameraTarget").objectReferenceValue = machine.transform;
            directorSerialized.FindProperty("pullShot").objectReferenceValue = shot;
            directorSerialized.FindProperty("leverDuration").floatValue = 2.05f;
            directorSerialized.FindProperty("leverSpringDuration").floatValue = 0.72f;
            directorSerialized.FindProperty("leverOvershootDegrees").floatValue = 32f;
            directorSerialized.FindProperty("leverElasticAmplitude").floatValue = 1.85f;
            directorSerialized.FindProperty("leverElasticPeriod").floatValue = 0.36f;
            directorSerialized.FindProperty("leverSound").objectReferenceValue = leverSound;
            directorSerialized.FindProperty("glowSound").objectReferenceValue = glowSound;
            directorSerialized.FindProperty("doorSound").objectReferenceValue = doorSound;
            directorSerialized.FindProperty("glowShakeStart").floatValue = 1.1f;
            directorSerialized.FindProperty("glowRampUpDuration").floatValue = 2.2f;
            directorSerialized.FindProperty("glowHoldDuration").floatValue = 0.6f;
            directorSerialized.FindProperty("glowRampDownDuration").floatValue = 2.2f;
            directorSerialized.ApplyModifiedPropertiesWithoutUndo();

            CandyMachineInteractable binding = machine.GetComponentInChildren<CandyMachineInteractable>(true);
            if (binding == null)
                throw new InvalidOperationException("CandyMachineInteractable was not found under Candy_Machine.");
            binding.Configure(binding.ScreenPrefab, director);

            EditorUtility.SetDirty(machine);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static CameraShotPresetSO EnsureShot()
    {
        EnsureFolder("Assets/Data", "Camera");
        EnsureFolder("Assets/Data/Camera", "Shots");
        CameraShotPresetSO shot = AssetDatabase.LoadAssetAtPath<CameraShotPresetSO>(ShotPath);
        if (shot == null)
        {
            shot = ScriptableObject.CreateInstance<CameraShotPresetSO>();
            AssetDatabase.CreateAsset(shot, ShotPath);
        }
        SerializedObject serialized = new SerializedObject(shot);
        serialized.FindProperty("followMode").enumValueIndex = (int)CameraShotFollowMode.PauseFollowAndMoveRig;
        serialized.FindProperty("duration").floatValue = 0.35f;
        serialized.FindProperty("holdTime").floatValue = 0f;
        serialized.FindProperty("deferRestore").boolValue = true;
        serialized.FindProperty("targetZoom").floatValue = 3.8f;
        serialized.FindProperty("targetPitch").floatValue = 45f;
        serialized.FindProperty("lookAtTarget").boolValue = true;
        serialized.FindProperty("smoothRotation").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(shot);
        return shot;
    }

    private static Material EnsureEmissiveMaterial()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "CandyMachine");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { name = "CandyMachine_EmissiveSphere" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetColor("_BaseColor", new Color(1f, 0.82f, 0.25f, 0f));
        material.SetColor("_EmissionColor", new Color(1f, 0.55f, 0.12f, 1f));
        material.EnableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Image CreateStampImage(string name, Transform parent, Sprite sprite)
    {
        GameObject go = CreateUiObject(name, parent);
        go.AddComponent<CanvasRenderer>();
        Image image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
        if (sprite == null)
            image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    private static void CreateResultRow(
        string name,
        Transform parent,
        TMP_Text template,
        out GameObject row,
        out TMP_Text caption,
        out TMP_Text value)
    {
        row = CreateUiObject(name, parent);
        LayoutElement rowLayout = row.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 72f;
        VerticalLayoutGroup stack = row.AddComponent<VerticalLayoutGroup>();
        stack.spacing = 2f;
        stack.childControlWidth = true;
        stack.childControlHeight = true;
        stack.childForceExpandWidth = true;
        stack.childForceExpandHeight = false;
        caption = CreateText("Caption", row.transform, template, name, 17f, 22f, TextAlignmentOptions.MidlineLeft);
        caption.color = PlayerMenuUiFactory.Muted;
        value = CreateText("Value", row.transform, template, string.Empty, 28f, 42f, TextAlignmentOptions.MidlineLeft);
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_Text template,
        string text,
        float fontSize,
        float preferredHeight,
        TextAlignmentOptions alignment)
    {
        GameObject go = CreateUiObject(name, parent);
        go.AddComponent<CanvasRenderer>();
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (template != null)
        {
            label.font = template.font;
            label.fontSharedMaterial = template.fontSharedMaterial;
            label.color = template.color;
            label.fontStyle = template.fontStyle;
        }
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = preferredHeight;
        return label;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.localScale = Vector3.one;
        return go;
    }

    private static void CopyImage(Image source, Image target)
    {
        if (source == null || target == null) return;
        target.sprite = source.sprite;
        target.type = source.type;
        target.color = source.color;
        target.material = source.material;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.raycastTarget = false;
    }

    private static void Stretch(RectTransform rect, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static GameObject FindNamed(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
                if (children[i].name == name) return children[i].gameObject;
        }
        return null;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
