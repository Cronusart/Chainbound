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
