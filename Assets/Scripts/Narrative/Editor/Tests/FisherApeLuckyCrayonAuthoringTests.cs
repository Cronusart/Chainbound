#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FisherApeLuckyCrayonAuthoringTests
{
    private const string ProfilePath =
        "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/fisher_ape_chatter_profile.asset";
    private const string SequencePath =
        "Assets/Scripts/Narrative/Quests/fisher_ape_lucky_crayon/Sequence/fisher_ape_lucky_crayon_sequence.asset";
    private const string PrefabPath = "Assets/Prefabs/NPC/Fisher-Ape.prefab";
    private const string BackpackPath = "Assets/Prefabs/World/Pickups/Backpack.prefab";

    [Test]
    public void Profile_GatesHintRewardAndEpilogue()
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(ProfilePath);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.Rules.Count, Is.EqualTo(2));
        Assert.That(profile.InactiveDialogue.GetLine(2).text, Does.Contain("lucky crayon"));

        QuestInteractionRule epilogue = profile.Rules[0];
        Assert.That(epilogue.dialogue.GetLine(0).text, Does.Contain("still looking"));
        Assert.That(epilogue.triggerId, Is.EqualTo(""));
        Assert.That(epilogue.lifecycleScope, Is.EqualTo(QuestInteractionRuleLifecycleScope.UseLifecycleMask));
        Assert.That(epilogue.lifecycleMask, Is.EqualTo(NarrativeQuestLifecycleMask.Unknown));
        Assert.That(epilogue.worldFlagPredicates[0].comparison, Is.EqualTo(QuestInteractionMemoryFlagComparison.IsTrue));
        Assert.That(epilogue.bagItemPredicates, Is.Empty);

        QuestInteractionRule reward = profile.Rules[1];
        Assert.That(reward.dialogue, Is.Null);
        Assert.That(reward.triggerId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.TriggerId));
        Assert.That(reward.worldFlagPredicates[0].key, Is.EqualTo(FisherApeLuckyCrayonAuthoring.FlagKey));
        Assert.That(reward.worldFlagPredicates[0].comparison, Is.EqualTo(QuestInteractionMemoryFlagComparison.IsFalse));
        Assert.That(reward.bagItemPredicates[0].itemId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.ItemId));
        Assert.That(reward.bagItemPredicates[0].minimumQuantity, Is.EqualTo(1));
    }

    [Test]
    public void Sequence_RewardsOrbAndRamWithoutAutoStart()
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(SequencePath);
        Assert.That(sequence, Is.Not.Null);
        Assert.That(sequence.SequenceId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.TrackId));
        Assert.That(sequence.AutoStartWhenNoSequenceRunning, Is.False);
        Assert.That(sequence.StepCount, Is.EqualTo(14));

        Step_ApplyQuestReward reward = sequence.GetStep(3) as Step_ApplyQuestReward;
        Assert.That(reward, Is.Not.Null);
        Assert.That(reward.RewardId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.RewardId));
        Assert.That(reward.GetFailurePolicy(), Is.EqualTo(StepFailurePolicy.GotoLabel));
        Assert.That(reward.GetOnFailLabel(), Is.EqualTo("reward_failed"));

        Step_ShowItemObtain orb = sequence.GetStep(5) as Step_ShowItemObtain;
        Step_ShowItemObtain ram = sequence.GetStep(6) as Step_ShowItemObtain;
        Assert.That(orb.ItemId, Is.EqualTo("signal_orb"));
        Assert.That(orb.Quantity, Is.EqualTo(1));
        Assert.That(ram.ItemId, Is.EqualTo("ram"));
        Assert.That(ram.Quantity, Is.EqualTo(15));

        Step_SetProfileWorldFlag flag = sequence.GetStep(4) as Step_SetProfileWorldFlag;
        Assert.That(flag.FlagKey, Is.EqualTo(FisherApeLuckyCrayonAuthoring.FlagKey));
        Assert.That(flag.FlagValue, Is.True);
    }

    [Test]
    public void FisherPrefab_ListensForTheRewardTrigger()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        SequenceOnTrigger listener = prefab.GetComponent<SequenceOnTrigger>();
        Assert.That(listener, Is.Not.Null);
        Assert.That(listener.TriggerId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.TriggerId));
        Assert.That(listener.TrackId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.TrackId));
        Assert.That(listener.Sequence, Is.Not.Null);
        Assert.That(listener.Sequence.SequenceId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.TrackId));
    }

    [Test]
    public void Dialogues_HaveNoCameraShot()
    {
        string[] dialoguePaths =
        {
            "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/Dial_FisherApe_StreamWarning.asset",
            "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/Dial_FisherApe_CrayonThanks.asset",
            "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/Dial_FisherApe_CrayonFavor.asset",
            "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/Dial_FisherApe_CrayonEpilogue.asset",
            "Assets/Scripts/NPC/Interaction/Ambient/FisherApe/Dial_FisherApe_CrayonHold.asset",
        };

        for (int pathIndex = 0; pathIndex < dialoguePaths.Length; pathIndex++)
        {
            DialogueSequenceSO dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(dialoguePaths[pathIndex]);
            Assert.That(dialogue, Is.Not.Null, dialoguePaths[pathIndex]);
            for (int lineIndex = 0; lineIndex < dialogue.LineCount; lineIndex++)
            {
                DialogueLine line = dialogue.GetLine(lineIndex);
                Assert.That(line.camera, Is.Not.Null, dialoguePaths[pathIndex]);
                Assert.That(line.camera.shotPreset, Is.Null, dialoguePaths[pathIndex]);
                Assert.That(line.camera.targetSource, Is.EqualTo(DialogueCameraTargetSource.None), dialoguePaths[pathIndex]);
                Assert.That(line.camera.npcId, Is.EqualTo("").Or.Null, dialoguePaths[pathIndex]);
            }
        }

        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(ProfilePath);
        Assert.That(profile.Rules[0].useDialogueCamera, Is.False);
        Assert.That(profile.Rules[1].useDialogueCamera, Is.False);

        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(SequencePath);
        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is not Step_PlayDialogue play)
                continue;

            var serialized = new SerializedObject(play);
            Assert.That(serialized.FindProperty("useSpeakerCameraBridge").boolValue, Is.False, play.name);
            Assert.That(serialized.FindProperty("persistSpeakerCameraAfterDialogue").boolValue, Is.False, play.name);
        }
    }

    [Test]
    public void AcademyBackpack_GrantsTheLuckyCrayon()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BackpackPath);
        WorldPickupGrant grant = prefab.GetComponentInChildren<WorldPickupGrant>(true);
        Assert.That(grant, Is.Not.Null);
        Assert.That(grant.ItemId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.ItemId));
        Assert.That(grant.PresenceId, Is.EqualTo(FisherApeLuckyCrayonAuthoring.SpawnId));
    }
}
#endif
