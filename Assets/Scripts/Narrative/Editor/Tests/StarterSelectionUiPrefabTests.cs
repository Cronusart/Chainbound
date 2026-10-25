using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

[TestFixture]
public sealed class StarterSelectionUiPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/UI/StarterSelection/StarterSelectionUI.prefab";
    private const string StarterNameDialoguePath =
        "Assets/Scripts/Narrative/Quests/chainbound_intro/DialogueSO/Dial_ProfessorNameStarter.asset";

    [Test]
    public void Prefab_HasUsableModalCanvasAndButtonPresentation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null, $"Could not load {PrefabPath}");

        Transform overlay = FindChild(prefab.transform, "StarterPreviewOverlay");
        Assert.That(overlay, Is.Not.Null);

        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        Assert.That(overlayRect, Is.Not.Null);
        Assert.That(overlayRect.localScale.sqrMagnitude, Is.GreaterThan(0.99f));
        Assert.That(overlayRect.anchorMax.x, Is.GreaterThan(overlayRect.anchorMin.x));
        Assert.That(overlayRect.anchorMax.y, Is.GreaterThan(overlayRect.anchorMin.y));
        Assert.That(overlay.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Assert.That(overlay.GetComponent<GraphicRaycaster>(), Is.Not.Null);

        Transform confirmTransform = FindChild(prefab.transform, "ConfirmButton");
        Transform backTransform = FindChild(prefab.transform, "BackButton");
        AssertButtonPresentation(confirmTransform);
        AssertButtonPresentation(backTransform);

        Button confirmButton = confirmTransform.GetComponent<Button>();
        Button backButton = backTransform.GetComponent<Button>();
        Selectable[] selectables = overlay.GetComponentsInChildren<Selectable>(true);
        Assert.That(selectables, Has.Length.EqualTo(3),
            "Starter selection should expose Confirm, Back, and the hidden starter-name field.");
        Assert.That(selectables, Does.Contain(confirmButton));
        Assert.That(selectables, Does.Contain(backButton));
        Assert.That(confirmButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(confirmButton.navigation.selectOnLeft, Is.SameAs(backButton));
        Assert.That(confirmButton.navigation.selectOnRight, Is.SameAs(backButton));
        Assert.That(confirmButton.navigation.selectOnUp, Is.SameAs(backButton));
        Assert.That(confirmButton.navigation.selectOnDown, Is.SameAs(backButton));
        Assert.That(backButton.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(backButton.navigation.selectOnLeft, Is.SameAs(confirmButton));
        Assert.That(backButton.navigation.selectOnRight, Is.SameAs(confirmButton));
        Assert.That(backButton.navigation.selectOnUp, Is.SameAs(confirmButton));
        Assert.That(backButton.navigation.selectOnDown, Is.SameAs(confirmButton));

        Transform nameInputTransform = FindChild(prefab.transform, "StarterNameInput");
        Assert.That(nameInputTransform, Is.Not.Null);
        TMP_InputField nameInput = nameInputTransform.GetComponent<TMP_InputField>();
        Assert.That(nameInput, Is.Not.Null);
        Transform titleTransform = FindChild(prefab.transform, "TitleText");
        Assert.That(titleTransform, Is.Not.Null);
        Assert.That(nameInputTransform.parent, Is.SameAs(titleTransform.parent),
            "The name field should replace the panel title slot above the chainling.");
        RectTransform nameRect = nameInputTransform.GetComponent<RectTransform>();
        RectTransform titleRect = titleTransform.GetComponent<RectTransform>();
        Assert.That(nameRect.anchorMin, Is.EqualTo(titleRect.anchorMin));
        Assert.That(nameRect.anchorMax, Is.EqualTo(titleRect.anchorMax));
        Assert.That(nameInputTransform.gameObject.activeSelf, Is.False,
            "The field is revealed only after the player chooses Yes in the dialogue.");
    }

    [Test]
    public void ShowStarterNameInput_MovesIntoNamingPoseAndUsesUpperCase()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        Assert.That(instance, Is.Not.Null);

        try
        {
            StarterSelectionUI ui = instance.GetComponent<StarterSelectionUI>();
            Assert.That(ui, Is.Not.Null);
            RectTransform nameRect = ui.StarterNameInputField.transform as RectTransform;
            Assert.That(nameRect, Is.Not.Null);
            Vector2 authoredPosition = nameRect.anchoredPosition;
            Vector2 authoredSize = nameRect.sizeDelta;
            Vector2 authoredAnchorMin = nameRect.anchorMin;
            Vector2 authoredAnchorMax = nameRect.anchorMax;
            RectTransform preview = FindChild(instance.transform, "PreviewContainer") as RectTransform;
            RectTransform frame = FindChild(instance.transform, "VisualFrame") as RectTransform;
            Assert.That(preview, Is.Not.Null);
            Assert.That(frame, Is.Not.Null);
            Vector2 previewMin = preview.offsetMin;
            Vector2 previewMax = preview.offsetMax;
            float frameY = frame.anchoredPosition.y;

            SetPrivateField(ui, "isShowing", true);
            FieldInfo modeField = typeof(StarterSelectionUI).GetField(
                "presentationMode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modeField, Is.Not.Null);
            SetPrivateField(ui, "presentationMode", Enum.Parse(modeField.FieldType, "Naming"));

            Assert.That(ui.ShowStarterNameInput(), Is.True);

            Assert.That(nameRect.anchoredPosition.x, Is.EqualTo(authoredPosition.x).Within(0.01f));
            Assert.That(nameRect.anchoredPosition.y, Is.EqualTo(-15f).Within(0.01f));
            Assert.That(nameRect.sizeDelta, Is.EqualTo(authoredSize));
            Assert.That(nameRect.anchorMin, Is.EqualTo(authoredAnchorMin));
            Assert.That(nameRect.anchorMax, Is.EqualTo(authoredAnchorMax));
            Assert.That(preview.offsetMin.x, Is.EqualTo(1000f).Within(0.01f));
            Assert.That(preview.offsetMax.x, Is.EqualTo(-1000f).Within(0.01f));
            Assert.That(preview.offsetMin.y, Is.EqualTo(previewMin.y).Within(0.01f));
            Assert.That(preview.offsetMax.y, Is.EqualTo(previewMax.y).Within(0.01f));
            Assert.That(frame.anchoredPosition.y, Is.EqualTo(frameY - 30f).Within(0.01f));

            ui.StarterNameInputField.SetTextWithoutNotify("KIT");
            ui.RetainStarterNameInput();
            Assert.That(ui.StarterNameInputField.gameObject.activeSelf, Is.True);
            Assert.That(ui.StarterNameInputField.text, Is.EqualTo("KIT"));
            Assert.That(ui.StarterNameInputField.readOnly, Is.True);

            TextMeshProUGUI label = ui.StarterNameInputField.textComponent as TextMeshProUGUI;
            TextMeshProUGUI placeholder = ui.StarterNameInputField.placeholder as TextMeshProUGUI;
            Assert.That(label, Is.Not.Null);
            Assert.That(placeholder, Is.Not.Null);
            Assert.That(label.fontStyle, Is.EqualTo(label.fontStyle | FontStyles.UpperCase));
            Assert.That(placeholder.fontStyle, Is.EqualTo(placeholder.fontStyle | FontStyles.UpperCase));
            Assert.That(label.fontStyle & FontStyles.LowerCase, Is.EqualTo(FontStyles.Normal));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    [Test]
    public void PortraitChrome_ShowsTheStarterPanelPlateAndScanlines()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        Assert.That(instance, Is.Not.Null);

        try
        {
            StarterSelectionUI ui = instance.GetComponent<StarterSelectionUI>();
            Assert.That(ui, Is.Not.Null);
            Transform container = FindChild(instance.transform, "PreviewContainer");
            Assert.That(container, Is.Not.Null);
            Image plate = container.GetComponent<Image>();
            Assert.That(plate, Is.Not.Null);
            Color authored = plate.color;

            InvokePrivate(ui, "PreparePortraitChrome", "Kitsauto");

            Assert.That(plate.color.a, Is.GreaterThan(0.5f));
            Transform scanline = container.Find("ScanlineOverlay");
            Assert.That(scanline, Is.Not.Null);
            Assert.That(scanline.gameObject.activeSelf, Is.True);
            RawImage scanlineImage = scanline.GetComponent<RawImage>();
            Assert.That(scanlineImage, Is.Not.Null);
            Assert.That(scanlineImage.texture, Is.Not.Null);
            Assert.That(scanlineImage.color.a, Is.GreaterThan(0.4f));
            Assert.That(scanline.GetSiblingIndex(), Is.EqualTo(0));
            RectTransform previewRect = container as RectTransform;
            Assert.That(previewRect, Is.Not.Null);
            Assert.That(previewRect.offsetMin.x, Is.EqualTo(1000f).Within(0.01f));
            Assert.That(previewRect.offsetMax.x, Is.EqualTo(-1000f).Within(0.01f));
            Assert.That(ui.StarterNameInputField.gameObject.activeSelf, Is.False);

            InvokePrivate(ui, "ResetNamingPresentation");
            Assert.That(plate.color, Is.EqualTo(authored));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, args);
    }

    [Test]
    public void Prefab_StarterPreviewGlitchScaleSettlesAtOne()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        StarterPreviewVisual preview = prefab.GetComponentInChildren<StarterPreviewVisual>(true);
        Assert.That(preview, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(preview);
        SerializedProperty curve = serialized.FindProperty("glitchInScaleCurve");
        Assert.That(curve, Is.Not.Null);
        Assert.That(curve.animationCurveValue.length, Is.GreaterThan(0));

        Keyframe last = curve.animationCurveValue.keys[curve.animationCurveValue.length - 1];
        Assert.That(last.value, Is.EqualTo(1f).Within(0.02f),
            "Starter preview should spawn at 1x, not the combat battlefield 1.2x visual-root scale.");
    }

    [Test]
    public void StarterNameDialogue_EndsWithReusableYesNoChoiceValues()
    {
        DialogueSequenceSO dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(StarterNameDialoguePath);
        Assert.That(dialogue, Is.Not.Null);
        Assert.That(dialogue.LineCount, Is.GreaterThan(0));

        DialogueLine finalLine = dialogue.GetLine(dialogue.LineCount - 1);
        Assert.That(finalLine, Is.Not.Null);
        Assert.That(finalLine.HasChoices, Is.True);
        Assert.That(finalLine.choices, Has.Length.EqualTo(2));
        Assert.That(finalLine.choices[0].value, Is.EqualTo("yes"));
        Assert.That(finalLine.choices[1].value, Is.EqualTo("no"));
    }

    private static void AssertButtonPresentation(Transform buttonTransform)
    {
        Assert.That(buttonTransform, Is.Not.Null);

        Button button = buttonTransform.GetComponent<Button>();
        Assert.That(button, Is.Not.Null);
        Assert.That(button.interactable, Is.True);
        Assert.That(button.targetGraphic, Is.Not.Null);
        Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None));

        UiSelectableMotion motion = buttonTransform.GetComponent<UiSelectableMotion>();
        Assert.That(motion, Is.Not.Null);

        Transform shimmerTransform = buttonTransform.Find("Shimmer");
        Assert.That(shimmerTransform, Is.Not.Null);
        Assert.That(shimmerTransform.gameObject.activeSelf, Is.False);
        UIImageFrameAnimator shimmer = shimmerTransform.GetComponent<UIImageFrameAnimator>();
        Assert.That(shimmer, Is.Not.Null);
        Assert.That(shimmer.SerializedAuthoringFrameCount, Is.GreaterThan(0));
        Assert.That(shimmer.GetComponent<Image>().raycastTarget, Is.False);
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name)
                return transforms[i];
        }

        return null;
    }
}
