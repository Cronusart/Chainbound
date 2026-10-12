#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class ProfCharkerAuthoringTests
{
    private const string PrefabPath = "Assets/Prefabs/NPC/Prof_Charker.prefab";
    private const string DialoguePath = "Assets/Scripts/NPC/Interaction/Academy/ProfCharker/Dial_ProfCharker_FloodedBindery.asset";
    private const string SoundPath = "Assets/Data/Audio/SFX/CHALKBOARD_LOOP.asset";

    [Test]
    public void AuthoredNpc_FacesUpAndLoopsChalkUntilDialogue()
    {
        ProfCharkerAuthoring.Apply();

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null);

        NPCController controller = prefab.GetComponent<NPCController>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.NPCId, Is.EqualTo("prof_charker"));
        Assert.That(controller.AutonomyEnabled, Is.False);

        NpcStationWorkLoop station = prefab.GetComponent<NpcStationWorkLoop>();
        Assert.That(station, Is.Not.Null);
        var stationSo = new SerializedObject(station);
        Assert.That(stationSo.FindProperty("restFacing").enumValueIndex, Is.EqualTo((int)NPCFaceDirectionPreset.WorldNorth));
        Assert.That(stationSo.FindProperty("workLoop").objectReferenceValue, Is.Not.Null);

        MeshRendererNPCVisual visual = prefab.GetComponentInChildren<MeshRendererNPCVisual>(true);
        var visualSo = new SerializedObject(visual);
        SerializedProperty upIdle = visualSo.FindProperty("idleSequences").FindPropertyRelative("up").FindPropertyRelative("sprites");
        Assert.That(upIdle.arraySize, Is.EqualTo(4));
        Assert.That(upIdle.GetArrayElementAtIndex(0).objectReferenceValue, Is.Not.Null);

        SoundEventSO chalk = AssetDatabase.LoadAssetAtPath<SoundEventSO>(SoundPath);
        Assert.That(chalk, Is.Not.Null);
        Assert.That(chalk.Loop, Is.True);
        Assert.That(chalk.Clips, Is.Not.Null);
        Assert.That(chalk.Clips.Length, Is.EqualTo(1));

        DialogueSequenceSO dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(DialoguePath);
        Assert.That(dialogue, Is.Not.Null);
        var dialogueSo = new SerializedObject(dialogue);
        SerializedProperty lines = dialogueSo.FindProperty("lines");
        Assert.That(lines.arraySize, Is.EqualTo(5));
        string joined = "";
        for (int i = 0; i < lines.arraySize; i++)
            joined += lines.GetArrayElementAtIndex(i).FindPropertyRelative("text").stringValue + " ";
        Assert.That(joined, Does.Contain("flooded"));
        Assert.That(joined, Does.Contain("sewer pipes"));
        Assert.That(joined, Does.Contain("Plumbers"));
    }
}
#endif
