using Chainbound.Combat;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[TestFixture]
public sealed class DialoguePanelPresentationTests
{
    private const string GameServicesPrefabPath =
        "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string CombatNarrationPrefabPath =
        "Assets/Prefabs/Combat/CombatNarrationPanel.prefab";

    [Test]
    public void GameServicesDialoguePanel_UsesSharedCombatNarrationPresentation()
    {
        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(GameServicesPrefabPath);
        Assert.That(root, Is.Not.Null);

        DialogueUIController controller =
            root.GetComponentInChildren<DialogueUIController>(true);
        Assert.That(controller, Is.Not.Null);

        var serializedController = new SerializedObject(controller);
        GameObject panel = serializedController.FindProperty("dialoguePanel")
            .objectReferenceValue as GameObject;
        DialogueBoxView view = serializedController.FindProperty("dialogueView")
            .objectReferenceValue as DialogueBoxView;
        TextMeshProUGUI body = serializedController.FindProperty("dialogueText")
            .objectReferenceValue as TextMeshProUGUI;
        TextMeshProUGUI speaker = serializedController.FindProperty("speakerNameText")
            .objectReferenceValue as TextMeshProUGUI;

        Assert.That(panel, Is.Not.Null);
        Assert.That(panel.name, Is.EqualTo("DialoguePanel"));
        Assert.That(view, Is.Not.Null);
        Assert.That(view.gameObject, Is.SameAs(panel));
        Assert.That(view.UsesVerticalMotion, Is.True);
        Assert.That(view.HiddenYOffset, Is.GreaterThan(0f));
        Assert.That(view.HiddenScaleY, Is.LessThan(1f));
        Assert.That(body, Is.Not.Null);
        Assert.That(body.name, Is.EqualTo("DialogText"));
        Assert.That(body.transform.IsChildOf(panel.transform), Is.True);
        Assert.That(speaker, Is.Not.Null);
        Assert.That(speaker.name, Is.EqualTo("SpeakerName"));
        Assert.That(speaker.transform.IsChildOf(panel.transform), Is.True);

        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(panel);
        Assert.That(source, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(CombatNarrationPrefabPath));

        Transform legacyPanel = FindDeep(root.transform, "LegacyDialoguePanel");
        Assert.That(legacyPanel, Is.Not.Null);
        Assert.That(legacyPanel.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void SharedDialoguePanel_AuthorsBottomPopMotionForNarrativeAndCombat()
    {
        GameObject sharedPanel = AssetDatabase.LoadAssetAtPath<GameObject>(CombatNarrationPrefabPath);
        Assert.That(sharedPanel, Is.Not.Null);

        DialogueBoxView view = sharedPanel.GetComponent<DialogueBoxView>();
        Assert.That(view, Is.Not.Null);
        Assert.That(view.UsesVerticalMotion, Is.True);
        Assert.That(view.HiddenYOffset, Is.GreaterThan(sharedPanel.GetComponent<RectTransform>().rect.height));
        Assert.That(view.HiddenScaleY, Is.InRange(0.01f, 0.25f));

        Transform choices = FindDeep(sharedPanel.transform, "DialogueChoices");
        Assert.That(choices, Is.Not.Null, "DialogueChoices row should be authored on the shared panel.");
        Assert.That(choices.gameObject.activeSelf, Is.False);
        Button template = choices.GetComponentInChildren<Button>(true);
        Assert.That(template, Is.Not.Null, "ChoiceButton template should live under DialogueChoices.");
        Assert.That(template.gameObject.activeSelf, Is.False);
        RectTransform templateRect = template.transform as RectTransform;
        Assert.That(templateRect, Is.Not.Null);
        LayoutElement layoutElement = template.GetComponent<LayoutElement>();
        Assert.That(layoutElement, Is.Not.Null);
        Assert.That(layoutElement.minHeight, Is.GreaterThanOrEqualTo(60f));

        RectTransform choiceRect = choices as RectTransform;
        Assert.That(choiceRect, Is.Not.Null);
        Assert.That(choiceRect.anchorMin.y, Is.EqualTo(0f));
        Assert.That(choiceRect.anchorMax.y, Is.EqualTo(0f));
        Assert.That(choiceRect.anchoredPosition.y, Is.GreaterThan(0f));

        Transform dialogText = FindDeep(sharedPanel.transform, "DialogText");
        RectTransform dialogRect = dialogText as RectTransform;
        Assert.That(dialogRect, Is.Not.Null);
        Assert.That(dialogRect.anchorMin.y, Is.EqualTo(1f));
        Assert.That(dialogRect.anchorMax.y, Is.EqualTo(1f));

        Transform plate = FindDeep(sharedPanel.transform, "CombatNarrationBG");
        RectTransform plateRect = plate as RectTransform;
        Assert.That(plateRect, Is.Not.Null);
        Assert.That(plateRect.sizeDelta.y, Is.EqualTo(475f));

        var serializedView = new SerializedObject(view);
        Assert.That(serializedView.FindProperty("choiceRow").objectReferenceValue, Is.SameAs(choices.gameObject));
        Assert.That(serializedView.FindProperty("choiceButtonTemplate").objectReferenceValue, Is.SameAs(template));
        Assert.That(serializedView.FindProperty("choicePlateHeight").floatValue, Is.EqualTo(600f));
        Assert.That(templateRect.sizeDelta.x, Is.GreaterThan(1f));
        Assert.That(layoutElement.preferredWidth, Is.EqualTo(templateRect.sizeDelta.x));
    }

    [Test]
    public void SharedDialoguePanel_ChoiceButtonsKeepAuthoredWidthWhenShown()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatNarrationPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Button template = FindDeep(prefab.transform, "ChoiceButton")?.GetComponent<Button>();
        Assert.That(template, Is.Not.Null);
        float authoredWidth = ((RectTransform)template.transform).sizeDelta.x;
        Assert.That(authoredWidth, Is.GreaterThan(1f));

        var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvasObject.transform) as GameObject;
        try
        {
            DialogueBoxView view = instance.GetComponent<DialogueBoxView>();
            Assert.That(view, Is.Not.Null);
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
            canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            view.ShowChoices(new[] { "Yes", "No" }, _ => { });
            LayoutRebuilder.ForceRebuildLayoutImmediate(instance.GetComponent<RectTransform>());

            Button[] shown = instance.GetComponentsInChildren<Button>(true);
            int liveChoices = 0;
            for (int i = 0; i < shown.Length; i++)
            {
                if (shown[i] == null || !shown[i].gameObject.activeInHierarchy)
                    continue;
                if (shown[i].name != "ChoiceButton")
                    continue;
                liveChoices++;
                RectTransform rect = shown[i].transform as RectTransform;
                LayoutElement layout = shown[i].GetComponent<LayoutElement>();
                Assert.That(rect, Is.Not.Null);
                Assert.That(rect.sizeDelta.x, Is.EqualTo(authoredWidth));
                Assert.That(rect.rect.width, Is.EqualTo(authoredWidth));
                Assert.That(layout, Is.Not.Null);
                Assert.That(layout.preferredWidth, Is.EqualTo(authoredWidth));
            }

            Assert.That(liveChoices, Is.EqualTo(2));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(canvasObject);
        }
    }

    private static Transform FindDeep(Transform root, string objectName)
    {
        if (root == null)
            return null;
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
