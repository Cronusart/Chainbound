#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using SignalJam.Narrative.Combat;
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
    private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string IntroEncounterId = "intro_first_creature";
    private const string CofiEncounterId = "coffee_cofi_lootwak";

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
        Assert.That(profile.InactiveDialogue, Is.Null);
        Assert.That(profile.InactiveTriggerId, Is.EqualTo(CoffeeCourierHuntAuthoring.ReynardCheckInTriggerId));
        Assert.That(profile.ActivateQuestOnInactiveInteract, Is.True);
        Assert.That(profile.CompleteObjectiveOnActivate, Is.EqualTo(CoffeeCourierHuntAuthoring.TalkToReynardObjective));

        DialogueSequenceSO offer = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(
            "Assets/Scripts/Narrative/Quests/coffee_and_beer/DialogueSO/Dial_Reynard_CheckInOffer.asset");
        Assert.That(offer, Is.Not.Null);
        string checkIn = string.Join(" ", Enumerable.Range(0, offer.LineCount).Select(i => offer.GetLine(i).text));
        Assert.That(checkIn, Does.Contain("Zeb"));
        Assert.That(checkIn, Does.Contain("jungle").IgnoreCase);
        Assert.That(checkIn, Does.Contain("Nitro Brew"));

        Assert.That(profile.ActiveFallbackDialogue, Is.Not.Null);
        string reminder = string.Join(" ", Enumerable.Range(0, profile.ActiveFallbackDialogue.LineCount)
            .Select(i => profile.ActiveFallbackDialogue.GetLine(i).text));
        Assert.That(reminder, Does.Contain("Zeb"));
        Assert.That(reminder, Does.Contain("jungle").IgnoreCase);
        Assert.That(reminder, Does.Not.Contain("Nitro Brew"));
    }

    [Test]
    public void ReynardCheckIn_GrantsBrewThenPanelThenClosingDialogue()
    {
        SequenceAsset checkIn = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence/coffee_reynard_checkin_sequence.asset");
        SequenceAsset gate = AssetDatabase.LoadAssetAtPath<SequenceAsset>(GateSequencePath);
        Assert.That(checkIn, Is.Not.Null);
        Assert.That(checkIn.SequenceId, Is.EqualTo(CoffeeCourierHuntAuthoring.CheckInSequenceId));
        Assert.That(checkIn.AutoStartWhenNoSequenceRunning, Is.False);

        int offer = -1;
        int grant = -1;
        int obtain = -1;
        int closing = -1;
        int menuFlag = -1;
        int tutorial = -1;
        int offerCount = 0;
        for (int i = 0; i < checkIn.StepCount; i++)
        {
            SequenceStep step = checkIn.GetStep(i);
            if (step is Step_PlayDialogue dialogue)
            {
                SerializedObject so = new SerializedObject(dialogue);
                if (offerCount == 0)
                {
                    offer = i;
                    Assert.That(so.FindProperty("useSpeakerCameraBridge").boolValue, Is.True);
                }
                else if (offerCount == 1)
                    closing = i;
                offerCount++;
            }

            if (step is Step_ApplyProfileItemDelta delta)
            {
                grant = i;
                Assert.That(delta.IdempotencyKey, Is.EqualTo(CoffeeCourierHuntAuthoring.NitroBrewIdempotencyKey));
                Assert.That(delta.Grants[0].itemId, Is.EqualTo(CoffeeCourierHuntAuthoring.NitroBrewItemId));
            }

            if (step is Step_ShowItemObtain shown)
            {
                obtain = i;
                Assert.That(shown.ItemId, Is.EqualTo(CoffeeCourierHuntAuthoring.NitroBrewItemId));
            }

            if (step is Step_SetProfileWorldFlag flag &&
                flag.FlagKey == GameplayMenuWorldFlagGate.MenuUnlockFlagKey)
                menuFlag = i;

            if (step is Step_ShowTutorialReadAlong readAlong)
            {
                tutorial = i;
                Assert.That(readAlong.Snippet, Is.Not.Null);
                Assert.That(readAlong.Snippet.Emphasis, Is.EqualTo(SignalJam.UI.MobileControls.MobileHudEmphasisTarget.MenuY));
            }
        }

        Assert.That(offer, Is.GreaterThanOrEqualTo(0));
        Assert.That(grant, Is.GreaterThan(offer));
        Assert.That(obtain, Is.GreaterThan(grant));
        Assert.That(closing, Is.GreaterThan(obtain));
        Assert.That(menuFlag, Is.GreaterThan(closing));
        Assert.That(tutorial, Is.GreaterThan(menuFlag));

        bool runsCheckIn = false;
        for (int i = 0; i < gate.StepCount; i++)
        {
            if (gate.GetStep(i) is Step_RunTrack track &&
                track.TrackId == CoffeeCourierHuntAuthoring.CheckInTrackId &&
                track.Sequence == checkIn)
                runsCheckIn = true;
        }

        Assert.That(runsCheckIn, Is.True);
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
    public void CofiLoss_ReturnsToSanctuaryUntilZebReopensTheTrigger()
    {
        SequenceAsset cofi = AssetDatabase.LoadAssetAtPath<SequenceAsset>(CofiSequencePath);
        int lossFlag = -1;
        int awaitCheckpoint = -1;
        int awaitZeb = -1;
        int enableRetry = -1;
        int jumpIdle = -1;
        for (int i = 0; i < cofi.StepCount; i++)
        {
            SequenceStep step = cofi.GetStep(i);
            if (step is Step_SetProfileWorldFlag flag &&
                flag.FlagKey == CoffeeCourierHuntAuthoring.CofiPlayerLostFlag &&
                flag.FlagValue)
                lossFlag = i;

            if (step is Step_SetCheckpoint checkpoint &&
                checkpoint.CheckpointLabel == "coffee_cofi_await_zeb")
            {
                awaitCheckpoint = i;
                Assert.That(checkpoint.ResumeSceneId, Is.EqualTo("Jungle"));
                Assert.That(checkpoint.ResumeSpawnId, Is.EqualTo("sanctuary_jungle_trailhead"));
            }

            if (step is Step_WaitForTrigger wait)
            {
                SerializedObject so = new SerializedObject(wait);
                if (so.FindProperty("triggerId").stringValue == CoffeeCourierHuntAuthoring.ZebCofiRetryTriggerId)
                {
                    awaitZeb = i;
                    Assert.That(so.FindProperty("saveReturnSnapshotOnTriggerComplete").boolValue, Is.False);
                    Assert.That(so.FindProperty("returnSpawnPointId").stringValue, Is.Empty);
                }
            }

            if (step is Step_EnableGameObject enable)
            {
                SerializedObject so = new SerializedObject(enable);
                if (so.FindProperty("gameObjectName").stringValue == CoffeeCourierHuntAuthoring.CofiTriggerObjectName &&
                    so.FindProperty("enable").boolValue)
                    enableRetry = i;
            }

            if (step is Step_JumpToLabel jump)
            {
                SerializedObject so = new SerializedObject(jump);
                if (so.FindProperty("targetLabel").stringValue == "coffee_cofi_idle")
                    jumpIdle = i;
            }
        }

        Step_StartEncounter encounter = null;
        for (int i = 0; i < cofi.StepCount; i++)
        {
            if (cofi.GetStep(i) is Step_StartEncounter start)
                encounter = start;
        }

        Assert.That(encounter, Is.Not.Null);
        SerializedObject encounterObject = new SerializedObject(encounter);
        Assert.That(encounterObject.FindProperty("returnSpawnPointId").stringValue, Is.EqualTo("sanctuary_jungle_trailhead"));
        Assert.That(lossFlag, Is.GreaterThanOrEqualTo(0));
        Assert.That(awaitCheckpoint, Is.GreaterThan(lossFlag));
        Assert.That(awaitZeb, Is.GreaterThan(awaitCheckpoint));
        Assert.That(enableRetry, Is.GreaterThan(awaitZeb));
        Assert.That(jumpIdle, Is.GreaterThan(enableRetry));
    }

    [Test]
    public void CombatLaunchProfiles_KeepIntroAutoEscaperScopedToIntroEncounter()
    {
        GameObject gameServices = AssetDatabase.LoadAssetAtPath<GameObject>(GameServicesPrefabPath);
        Assert.That(gameServices, Is.Not.Null);

        NarrativeEncounterService service = gameServices.GetComponentInChildren<NarrativeEncounterService>(true);
        Assert.That(service, Is.Not.Null);

        SerializedObject serialized = new SerializedObject(service);
        SerializedProperty registry = serialized.FindProperty("registry");
        Assert.That(registry, Is.Not.Null);

        SerializedProperty introProfile = null;
        SerializedProperty cofiProfile = null;
        for (int i = 0; i < registry.arraySize; i++)
        {
            SerializedProperty entry = registry.GetArrayElementAtIndex(i);
            string encounterId = entry.FindPropertyRelative("encounterId").stringValue;
            if (encounterId == IntroEncounterId)
                introProfile = entry.FindPropertyRelative("combatLaunchProfile");
            else if (encounterId == CofiEncounterId)
                cofiProfile = entry.FindPropertyRelative("combatLaunchProfile");
        }

        Assert.That(introProfile, Is.Not.Null);
        Assert.That(introProfile.objectReferenceValue, Is.Not.Null);
        Assert.That(introProfile.objectReferenceValue.name, Is.EqualTo("IntroEncounterLaunchProfile"));
        Assert.That(cofiProfile, Is.Not.Null);
        Assert.That(cofiProfile.objectReferenceValue, Is.Null,
            "Cofi Lootwak must use the normal player party and combat flow, not the intro ghost/auto-escape profile.");
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
        Assert.That(zeb.Rules[2].label, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebRetryAdviceRuleLabel));
        Assert.That(zeb.Rules[2].triggerId, Is.EqualTo(CoffeeCourierHuntAuthoring.ZebCofiRetryTriggerId));
        Assert.That(zeb.Rules[2].worldFlagPredicates[0].key, Is.EqualTo(CoffeeCourierHuntAuthoring.CofiPlayerLostFlag));
        Assert.That(zeb.Rules[2].dialogue.GetLine(1).text, Does.Contain("party").IgnoreCase);

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
