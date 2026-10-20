#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class AmbientNpcPrefabAuthoringTests
{
    [TestCase(
        "Assets/Prefabs/NPC/Fisher-Ape.prefab",
        "fisher_ape",
        "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/fisher_ape_chatter_profile.asset",
        false,
        3)]
    [TestCase(
        "Assets/Prefabs/NPC/Crayon-Eater.prefab",
        "crayon_eater",
        "Assets/Scripts/NPC/Interaction/Ambient/CrayonEater/crayon_eater_chatter_profile.asset",
        true,
        1)]
    public void Prefab_HasCompleteDirectionalVisualsAndDialogue(
        string prefabPath,
        string expectedNpcId,
        string profilePath,
        bool expectsBlinkSprites,
        int expectedLineCount)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null, prefabPath);

        NPCController controller = prefab.GetComponent<NPCController>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.NPCId, Is.EqualTo(expectedNpcId));

        MeshRendererNPCVisual visual = prefab.GetComponentInChildren<MeshRendererNPCVisual>(true);
        Assert.That(visual, Is.Not.Null);
        var visualSerialized = new SerializedObject(visual);
        AssertDirectionalSequence(visualSerialized, "idleSequences");
        AssertDirectionalSequence(visualSerialized, "walkSequences");

        Assert.That(
            visualSerialized.FindProperty("blinkDown").objectReferenceValue != null,
            Is.EqualTo(expectsBlinkSprites));
        Assert.That(
            visualSerialized.FindProperty("blinkLeft").objectReferenceValue != null,
            Is.EqualTo(expectsBlinkSprites));
        Assert.That(
            visualSerialized.FindProperty("blinkRight").objectReferenceValue != null,
            Is.EqualTo(expectsBlinkSprites));

        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(profilePath);
        QuestProgressInteractionBridge bridge = prefab.GetComponent<QuestProgressInteractionBridge>();
        Assert.That(profile, Is.Not.Null);
        Assert.That(bridge, Is.Not.Null);
        Assert.That(bridge.DefaultProfile, Is.SameAs(profile));
        Assert.That(profile.InactiveDialogue, Is.Not.Null);
        Assert.That(profile.ActiveFallbackDialogue, Is.SameAs(profile.InactiveDialogue));
        Assert.That(profile.InactiveDialogue.SpeakerNpcId, Is.EqualTo(expectedNpcId));
        Assert.That(profile.InactiveDialogue.LineCount, Is.EqualTo(expectedLineCount));
        Assert.That(profile.InactiveDialogue.GetLine(0).portraitSprite, Is.Not.Null);

        InteractableObject interactable = prefab.GetComponentInChildren<InteractableObject>(true);
        InteractableIndicator indicator = prefab.GetComponentInChildren<InteractableIndicator>(true);
        Assert.That(interactable, Is.Not.Null);
        Assert.That(indicator, Is.Not.Null);
        var interactableSerialized = new SerializedObject(interactable);
        Assert.That(interactableSerialized.FindProperty("indicator").objectReferenceValue, Is.SameAs(indicator));
        Assert.That(
            interactableSerialized.FindProperty("onInteracted.m_PersistentCalls.m_Calls").arraySize,
            Is.EqualTo(1));
    }

    [Test]
    public void FisherApeDialogue_WarnsAboutTheStreamWithoutStartingAQuest()
    {
        DialogueSequenceSO dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(
            "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/Dial_FisherApe_StreamWarning.asset");
        Assert.That(dialogue.LineCount, Is.EqualTo(3));
        Assert.That(dialogue.GetLine(0).text, Is.EqualTo("The stream up ahead is rough today. Be careful."));
        Assert.That(dialogue.GetLine(1).text, Is.EqualTo("A lot of crazed Chainlings are biting today."));
        Assert.That(dialogue.GetLine(2).text, Does.Contain("lucky crayon"));
        Assert.That(dialogue.GetLine(0).speakerName, Is.EqualTo("Fisher-Ape"));
    }

    [Test]
    public void CrayonEaterDialogue_AsksForALuckyCandyMachineRoll()
    {
        DialogueSequenceSO dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(
            "Assets/Scripts/NPC/Interaction/Ambient/CrayonEater/Dial_CrayonEater_CandyMachine.asset");
        Assert.That(dialogue.GetLine(0).text, Is.EqualTo(
            "Saving RAM for candy-machine rolls. I'm due for a win."));
        Assert.That(dialogue.GetLine(0).speakerName, Is.EqualTo("Crayon-Eater"));
    }

    private static void AssertDirectionalSequence(SerializedObject visual, string propertyName)
    {
        string[] directions = { "down", "left", "right", "up" };
        SerializedProperty sequences = visual.FindProperty(propertyName);
        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            SerializedProperty sprites = sequences
                .FindPropertyRelative(directions[directionIndex])
                .FindPropertyRelative("sprites");
            Assert.That(sprites.arraySize, Is.EqualTo(4), $"{propertyName}.{directions[directionIndex]}");
            for (int frame = 0; frame < sprites.arraySize; frame++)
            {
                Assert.That(
                    sprites.GetArrayElementAtIndex(frame).objectReferenceValue,
                    Is.Not.Null,
                    $"{propertyName}.{directions[directionIndex]} frame {frame}");
            }
        }
    }
}
#endif
