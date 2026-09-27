#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CoffeeCourierHuntAuthoringTests
{
    private const string TobyRouterPath =
        "Assets/Scripts/NPC/Interaction/Intro/prof_toby_interaction_router.asset";
    private const string ReynardCoffeePath =
        "Assets/Scripts/NPC/Interaction/Reynard/Reynard_CoffeeInteractionProfile.asset";
    private const string LoominaryPath =
        "Assets/Scripts/NPC/Interaction/Loominary/Loominary_QuestInteractionProfile.asset";
    private const string ZebSequencePath =
        "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_courier_zeb_sequence.asset";
    private const string CofiSequencePath =
        "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_cofi_lootwak_sequence.asset";
    private const string GateSequencePath =
        "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_loomville_gate_sequence.asset";
    private const string QuestPath = "Assets/Scripts/Narrative/Quests/Quest_CoffeeAndBeer.asset";

    [Test]
    public void TobyRouter_HasCoffeeActiveNag()
    {
        QuestInteractionRouterSO router = AssetDatabase.LoadAssetAtPath<QuestInteractionRouterSO>(TobyRouterPath);
        Assert.That(router, Is.Not.Null);
        Assert.That(router.Slots.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(router.Slots[1].Label, Does.Contain("Active").IgnoreCase);
        Assert.That(router.Slots[1].Profile, Is.Not.Null);
        Assert.That(router.Slots[1].Profile.QuestId, Is.EqualTo(CoffeeCourierHuntAuthoring.CoffeeQuestId));
        Assert.That(router.Slots[1].AllowedLifecycleMask, Is.EqualTo(NarrativeQuestLifecycleMask.Active));
        Assert.That(router.Slots[1].Profile.ActiveFallbackDialogue, Is.Not.Null);
        Assert.That(
            router.Slots[1].Profile.ActiveFallbackDialogue.GetLine(0).text,
            Does.Contain("parched").IgnoreCase.Or.Contain("moving").IgnoreCase);
    }

    [Test]
    public void ReynardCoffeeProfile_NamesCourierZebAndJungle()
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(ReynardCoffeePath);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.InactiveDialogue, Is.Not.Null);
        string checkIn = string.Join(" ", Enumerable.Range(0, profile.InactiveDialogue.LineCount)
            .Select(i => profile.InactiveDialogue.GetLine(i).text));
        Assert.That(checkIn, Does.Contain("Zeb"));
        Assert.That(checkIn, Does.Contain("jungle").IgnoreCase);
        Assert.That(profile.ActiveFallbackDialogue, Is.Not.Null);
        string reminder = string.Join(" ", Enumerable.Range(0, profile.ActiveFallbackDialogue.LineCount)
            .Select(i => profile.ActiveFallbackDialogue.GetLine(i).text));
        Assert.That(reminder, Does.Contain("Zeb"));
        Assert.That(reminder, Does.Contain("jungle").IgnoreCase);
    }

    [Test]
    public void LoominaryProfile_HasFallbackDialogue()
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(LoominaryPath);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.InactiveDialogue, Is.Not.Null);
        Assert.That(profile.ActiveFallbackDialogue, Is.Not.Null);
    }

    [Test]
    public void CoffeeQuest_HasTalkToZebObjective()
    {
        QuestDefinitionSO quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(QuestPath);
        Assert.That(quest, Is.Not.Null);
        Assert.That(quest.Objectives.Any(item => item.objectiveId == CoffeeCourierHuntAuthoring.TalkToZebObjective), Is.True);
    }

    [Test]
    public void CourierSequences_WaitForZebThenUnlockCofi()
    {
        SequenceAsset zeb = AssetDatabase.LoadAssetAtPath<SequenceAsset>(ZebSequencePath);
        SequenceAsset cofi = AssetDatabase.LoadAssetAtPath<SequenceAsset>(CofiSequencePath);
        SequenceAsset gate = AssetDatabase.LoadAssetAtPath<SequenceAsset>(GateSequencePath);
        Assert.That(zeb, Is.Not.Null);
        Assert.That(cofi, Is.Not.Null);
        Assert.That(zeb.SequenceId, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebSequenceId));
        Assert.That(zeb.AutoStartRequiredWorldFlagKey, Is.EqualTo(CoffeeMartGateAuthoring.OpenFlag));
        Assert.That(zeb.AutoStartSkipWorldFlagKey, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebTalkedFlag));
        Assert.That(cofi.AutoStartRequiredWorldFlagKey, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebTalkedFlag));
        Assert.That(cofi.AutoStartSkipWorldFlagKey, Is.EqualTo(CoffeeCourierHuntAuthoring.CofiDefeatedFlag));

        bool waitsForZeb = false;
        bool queuesCofi = false;
        for (int i = 0; i < zeb.StepCount; i++)
        {
            if (zeb.GetStep(i) is Step_WaitForTrigger wait)
            {
                SerializedObject so = new SerializedObject(wait);
                if (so.FindProperty("triggerId").stringValue == CoffeeCourierHuntAuthoring.ZebTriggerId)
                    waitsForZeb = true;
            }

            if (zeb.GetStep(i) is Step_QueueSequence queue && queue.Sequence == cofi)
                queuesCofi = true;
        }

        Assert.That(waitsForZeb, Is.True);
        Assert.That(queuesCofi, Is.True);

        bool waitsForCofi = false;
        bool startsEncounter = false;
        for (int i = 0; i < cofi.StepCount; i++)
        {
            if (cofi.GetStep(i) is Step_WaitForTrigger wait)
            {
                SerializedObject so = new SerializedObject(wait);
                if (so.FindProperty("triggerId").stringValue == CoffeeCourierHuntAuthoring.CofiTriggerId)
                    waitsForCofi = true;
            }

            if (cofi.GetStep(i) is Step_StartEncounter)
                startsEncounter = true;
        }

        Assert.That(waitsForCofi, Is.True);
        Assert.That(startsEncounter, Is.True);

        bool gateQueuesZeb = false;
        for (int i = 0; i < gate.StepCount; i++)
        {
            if (gate.GetStep(i) is Step_QueueSequence queue && queue.Sequence == zeb)
                gateQueuesZeb = true;
        }

        Assert.That(gateQueuesZeb, Is.True, "Mart leftover should hand off to the Zeb courier sequence.");
    }
}
#endif
