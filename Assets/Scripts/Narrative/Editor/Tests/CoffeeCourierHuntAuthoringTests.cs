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

    [Test]
    public void CoffeeQuest_TracksBeansAndBeerWithoutClosingOnToby()
    {
        QuestDefinitionSO quest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(QuestPath);
        QuestObjective beans = quest.Objectives.First(item => item.objectiveId == CoffeeCourierHuntAuthoring.FetchCoffeeObjective);
        QuestObjective beer = quest.Objectives.First(item => item.objectiveId == CoffeeCourierHuntAuthoring.FetchBeerObjective);
        QuestObjective delivery = quest.Objectives.First(item => item.objectiveId == CoffeeCourierHuntAuthoring.DeliverToTobyObjective);

        Assert.That(beans.targetAmount, Is.EqualTo(1));
        Assert.That(beans.description, Does.Contain("coffee beans").IgnoreCase);
        Assert.That(beer.targetAmount, Is.EqualTo(1));
        Assert.That(beer.description, Does.Contain("Momo"));
        Assert.That(delivery.targetAmount, Is.EqualTo(1));
        Assert.That(delivery.description, Does.Contain("Toby"));
        Assert.That(quest.AutoCompleteWhenObjectivesComplete, Is.True);
    }

    [Test]
    public void CofiSequence_GrantsBeansOnlyAfterAWin()
    {
        SequenceAsset cofi = AssetDatabase.LoadAssetAtPath<SequenceAsset>(CofiSequencePath);
        int winIf = IndexOfStep<Step_If>(cofi);
        int grant = IndexOfStep<Step_ApplyProfileItemDelta>(cofi);
        int defeated = -1;
        for (int i = 0; i < cofi.StepCount; i++)
        {
            if (cofi.GetStep(i) is Step_SetProfileWorldFlag flag &&
                flag.FlagKey == CoffeeCourierHuntAuthoring.CofiDefeatedFlag)
            {
                defeated = i;
            }
        }

        Assert.That(winIf, Is.GreaterThanOrEqualTo(0));
        Assert.That(grant, Is.GreaterThan(winIf));
        Assert.That(defeated, Is.GreaterThan(grant));

        Step_ApplyProfileItemDelta delta = (Step_ApplyProfileItemDelta)cofi.GetStep(grant);
        Assert.That(delta.IdempotencyKey, Is.EqualTo(CoffeeCourierHuntAuthoring.CoffeeBeansIdempotencyKey));
        Assert.That(delta.Grants[0].itemId, Is.EqualTo(CoffeeCourierHuntAuthoring.CoffeeBeansItemId));
        Assert.That(delta.Consumes, Is.Empty);
    }

    [Test]
    public void DeliveryProfiles_GateOnWorldFlags()
    {
        QuestInteractionProfileSO zeb = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(
            "Assets/Scripts/NPC/Interaction/Intro/zeb_coffee_interaction_profile.asset");
        QuestInteractionProfileSO reynard = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(
            "Assets/Scripts/NPC/Interaction/Reynard/Reynard_CoffeeInteractionProfile.asset");
        QuestInteractionProfileSO toby = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(
            "Assets/Scripts/NPC/Interaction/Intro/prof_toby_coffee_active_profile.asset");
        QuestInteractionProfileSO momo = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(
            "Assets/Scripts/NPC/Interaction/Academy/Momo/momo_academy_profile.asset");

        Assert.That(zeb.Rules[0].worldFlagPredicates[0].key, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebDepartedFlag));
        Assert.That(zeb.Rules[0].triggerId, Is.Empty);
        Assert.That(zeb.Rules[1].triggerId, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebReturnTriggerId));
        Assert.That(zeb.Rules[1].dialogue.GetLine(0).text, Does.Contain("bag back"));

        Assert.That(reynard.Rules[0].worldFlagPredicates[0].key, Is.EqualTo(CoffeeCourierHuntAuthoring.ReynardBrewedFlag));
        Assert.That(reynard.Rules[1].triggerId, Is.EqualTo(CoffeeCourierHuntAuthoring.ReynardBrewTriggerId));
        Assert.That(reynard.Rules[2].dialogue.GetLine(0).text, Does.Contain("piping hot").IgnoreCase.Or.Contain("beer"));

        Assert.That(toby.Rules[0].worldFlagPredicates[0].key, Is.EqualTo(CoffeeCourierHuntAuthoring.HoldingTobysDeliveryFlag));
        Assert.That(toby.Rules[0].dialogue.GetLine(0).text, Does.Contain("doorway"));
        Assert.That(toby.Rules[1].dialogue.GetLine(0).text, Does.Contain("gnaw"));

        Assert.That(momo.Rules[0].label, Is.EqualTo(CoffeeCourierHuntAuthoring.MomoBeerRuleLabel));
        Assert.That(momo.Rules[0].triggerId, Is.EqualTo(CoffeeCourierHuntAuthoring.MomoBeerTriggerId));
        Assert.That(momo.Rules[0].dialogue.GetLine(0).text, Does.Contain("cold beer").IgnoreCase);
        Assert.That(momo.Rules.Count, Is.GreaterThan(1));
    }

    [Test]
    public void Jungle_ZebAndCartHideOnTheNextEnable()
    {
        const string scenePath = "Assets/Scenes/Jungle.unity";
        UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(scenePath);
        bool opened = false;
        if (!scene.isLoaded)
        {
            scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                scenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Additive);
            opened = true;
        }

        try
        {
            int departed = 0;
            bool sawZeb = false;
            bool sawCart = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                WorldPresenceBinding[] bindings = root.GetComponentsInChildren<WorldPresenceBinding>(true);
                for (int i = 0; i < bindings.Length; i++)
                {
                    if (bindings[i].PresenceId != CoffeeCourierHuntAuthoring.ZebDepartedFlag)
                        continue;
                    departed++;
                    Assert.That(bindings[i].ApplyOnEnableOnly, Is.True);
                    Assert.That(bindings[i].ActiveWhenTrue, Is.False);
                    if (bindings[i].Target != null && bindings[i].Target.name == "Zeb")
                        sawZeb = true;
                    if (bindings[i].Target != null && bindings[i].Target.name == "Zeb_BrokenDeliveryCart")
                        sawCart = true;
                }
            }

            Assert.That(departed, Is.EqualTo(2));
            Assert.That(sawZeb, Is.True);
            Assert.That(sawCart, Is.True);
        }
        finally
        {
            if (opened)
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static int IndexOfStep<T>(SequenceAsset sequence) where T : SequenceStep
    {
        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is T)
                return i;
        }

        return -1;
    }
}
#endif
