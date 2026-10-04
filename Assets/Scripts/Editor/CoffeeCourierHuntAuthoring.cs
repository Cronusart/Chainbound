#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent authoring for the coffee quest after Mart leftover: Reynard/Toby
/// reminders, Zeb courier talk, and the Cofi Lootwak boss collider unlock.
/// </summary>
public static class CoffeeCourierHuntAuthoring
{
    public const string CoffeeQuestId = CoffeeMartGateAuthoring.CoffeeQuestId;
    public const string TalkToZebObjective = "talk_to_zeb";
    public const string ZebTalkedFlag = "world.coffee.zeb_talked";
    public const string CofiDefeatedFlag = "world.coffee.cofi_lootwak.defeated";
    public const string CofiPlayerLostFlag = "world.coffee.cofi_lootwak.player_lost";
    public const string FetchCoffeeObjective = "fetch_coffee";
    public const string FetchBeerObjective = "fetch_beer";
    public const string DeliverToTobyObjective = "deliver_to_toby";
    public const string HoldingCoffeeBeansFlag = "world.coffee.holding_coffee_beans";
    public const string ZebDepartedFlag = "world.coffee.zeb_departed";
    public const string HoldingColdBeerFlag = "world.coffee.holding_cold_beer";
    public const string ColdBeerGrantedFlag = "world.coffee.cold_beer_granted";
    public const string ReynardBrewedFlag = "world.coffee.reynard_brewed";
    public const string HoldingTobysDeliveryFlag = "world.coffee.holding_tobys_delivery";
    public const string ZebTriggerId = "trigger_coffee_zeb_talked";
    public const string ZebReturnTriggerId = "trigger_coffee_zeb_return";
    public const string ZebCofiRetryTriggerId = "trigger_coffee_zeb_cofi_retry";
    public const string ZebRetryAdviceRuleLabel = "lost to cofi — advise and reopen";
    public const string MomoBeerTriggerId = "trigger_coffee_momo_beer";
    public const string ReynardBrewTriggerId = "trigger_coffee_reynard_brew";
    public const string CofiTriggerId = "trigger_cofi_lootwak";
    public const string ZebSequenceId = "coffee_courier_zeb_sequence";
    public const string CofiSequenceId = "coffee_cofi_lootwak_sequence";
    public const string ZebReturnSequenceId = "coffee_zeb_return_sequence";
    public const string MomoBeerSequenceId = "coffee_momo_beer_sequence";
    public const string ReynardBrewSequenceId = "coffee_reynard_brew_sequence";
    public const string MomoBeerRuleLabel = "zeb sent cold beer";
    public const string CoffeeBeansItemId = "coffee_beans";
    public const string ColdBeerItemId = "cold_beer";
    public const string TobysDeliveryItemId = "tobys_delivery";
    public const string CoffeeBeansIdempotencyKey = "quest:coffee_and_beer:coffee_beans";
    public const string ColdBeerIdempotencyKey = "quest:coffee_and_beer:cold_beer";
    public const string ReynardBrewIdempotencyKey = "quest:coffee_and_beer:reynard_brew";
    public const string EncounterId = "coffee_cofi_lootwak";
    public const string ZebNpcId = "zeb";
    public const string CofiTriggerObjectName = "Trigger_CofiLootwak";
    public const string GatesRootName = "Narrative_CoffeeCourier";

    public static readonly string[] ReynardCheckInLines =
    {
        "Toby sent you, huh? Figures. That dog runs on coffee and bad decisions.",
        "Two blacks and a beer. Trouble is, my bean shipment's still playing hard to get.",
        "My courier Zeb went deep into the jungle to pick it up. Find him and send those beans home.",
        "Head out past the trail into the jungle. You want Toby's order, you start with Zeb.",
    };

    public static readonly string[] ReynardReminderLines =
    {
        "Still no beans. Look for Zeb — my courier.",
        "He was picking the shipment up deep in the jungle. That's the direction. Go.",
    };

    public static readonly string[] TobyThirstyLines =
    {
        "I'm parched, pup. That order is not going to walk itself into the lab.",
        "Get moving on my two blacks and a beer before I have to do science uncaffeinated.",
    };

    public static readonly string[] ZebCourierLines =
    {
        "You're Reynard's runner? Finally. I had the shipment.",
        "Then Cofi Lootwak hit the trail and ran off with the whole load.",
        "I am not going back in there. You want Toby's coffee, you face that beast yourself.",
    };

    public static readonly string[] ZebReturnLines =
    {
        "You actually got the bag back. I owe you for that.",
        "I would finish the run myself, but the cart is wrecked and I still have to scrape up what's left of the shipment.",
        "Take the beans to Reynard. He is the one who brews Toby's order.",
        "Across the stream, the academy is just ahead. Tell Momo I sent you. He is holding the beer.",
    };

    public static readonly string[] ZebRetryAdviceLines =
    {
        "Cofi sent you back to the sanctuary. One Chainling was never going to hold that trail.",
        "Level your Chainlings up, and don't walk in alone. Bring a party.",
        "When you're ready, I'll open the trail again.",
    };

    public static readonly string[] ZebDepartedReminderLines =
    {
        "Take that bean bag to Reynard. And tell Momo I sent you — he has the beer.",
    };

    public static readonly string[] MomoBeerLines =
    {
        "Zeb sent you? Then this is yours. One cold beer. Don't let it sit.",
    };

    public static readonly string[] ReynardNeedBeerLines =
    {
        "Beans are here, beer is not. If I brew now, Toby's coffee goes cold and stale before the beer shows up.",
        "He wants it piping hot. Fetch the beer first.",
    };

    public static readonly string[] ReynardBrewLines =
    {
        "Beans and a cold beer. That is the order. Give them here.",
        "Two blacks, still hot, and the beer packed with them. This is Toby's delivery. Get it to him before it cools.",
    };

    public static readonly string[] ReynardBrewedReminderLines =
    {
        "That parcel is Toby's. I am not brewing it twice.",
    };

    public static readonly string[] TobyBeansLines =
    {
        "Coffee beans? What do you want me to do, gnaw on them? Reynard brews. I drink.",
    };

    public static readonly string[] TobyDeliveryStallLines =
    {
        "You have the order. Hold it. We are not doing this in the doorway.",
    };

    public static readonly string[] CofiBossIntroLines =
    {
        "Don't fade the civet, anon. I rugged the courier — not the community.",
        "You're still early. Stake your hopium, ignore the FUD, and definitely don't ask where the beans came from.",
    };

    private const string CoffeeQuestPath = "Assets/Scripts/Narrative/Quests/Quest_CoffeeAndBeer.asset";
    private const string DialogueFolder = "Assets/Scripts/Narrative/Quests/coffee_and_beer/DialogueSO";
    private const string IntroDialogueFolder = "Assets/Scripts/Narrative/Quests/chainbound_intro/DialogueSO";
    private const string SequenceFolder = "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence";
    private const string StepsFolder = SequenceFolder + "/Steps";
    private const string CatalogPath = "Assets/Resources/Narrative/SequenceCatalog.asset";
    private const string TobyRouterPath = "Assets/Scripts/NPC/Interaction/Intro/prof_toby_interaction_router.asset";
    private const string TobyIntroProfilePath = "Assets/Scripts/NPC/Interaction/Intro/chainbound_intro_prof_toby_profile.asset";
    private const string TobyAvailablePath = "Assets/Scripts/NPC/Interaction/Intro/prof_toby_coffee_available_profile.asset";
    private const string TobyActivePath = "Assets/Scripts/NPC/Interaction/Intro/prof_toby_coffee_active_profile.asset";
    private const string ReynardCoffeePath = "Assets/Scripts/NPC/Interaction/Reynard/Reynard_CoffeeInteractionProfile.asset";
    private const string ZebProfilePath = "Assets/Scripts/NPC/Interaction/Intro/zeb_coffee_interaction_profile.asset";
    private const string MomoProfilePath = "Assets/Scripts/NPC/Interaction/Academy/Momo/momo_academy_profile.asset";
    private const string LoominaryProfilePath = "Assets/Scripts/NPC/Interaction/Loominary/Loominary_QuestInteractionProfile.asset";
    private const string LoominaryBrokenPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/DialogueSO/Dial_LoominaryBroken.asset";
    private const string ReynardCheckInPath = IntroDialogueFolder + "/Dial_ReynardCoffeeCheckIn.asset";
    private const string EncounterSourcePath = "Assets/Data/Combat/BattleProviders/IntroFirstCreature_Encounter.asset";
    private const string EncounterPath = "Assets/Data/Combat/BattleProviders/CofiLootwak_Encounter.asset";
    private const string BertPrefabPath = "Assets/Prefabs/NPC/Bert.prefab";
    private const string ZebPrefabPath = "Assets/Prefabs/NPC/Zeb.prefab";
    private const string GameServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string JungleScenePath = "Assets/Scenes/Jungle.unity";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";
    private const string InteractionBehaviorPath = "Assets/Scripts/NPC/Interaction/NPCInteractionBehavior.asset";

    [MenuItem("Tools/Chainbound/Narrative/Author Coffee Courier Hunt")]
    public static void ApplyFromMenu() => Apply();

    public static void ApplyFromCommandLine() => Apply();

    public static void Apply()
    {
        QuestDefinitionSO coffeeQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(CoffeeQuestPath);
        if (coffeeQuest == null)
            throw new InvalidOperationException($"Missing coffee quest at '{CoffeeQuestPath}'.");

        AuthorTalkToZebObjective(coffeeQuest);
        AuthorDeliveryObjectives(coffeeQuest);

        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        CameraShotPresetSO shot = AssetDatabase.LoadAssetAtPath<CameraShotPresetSO>(
            AssetDatabase.GUIDToAssetPath("e2e99b5b538b6914d99ce0ebfaca93f7"));

        DialogueSequenceSO reynardCheckIn = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(ReynardCheckInPath);
        if (reynardCheckIn == null)
            throw new InvalidOperationException($"Missing Reynard check-in dialogue at '{ReynardCheckInPath}'.");
        AuthorDialogue(reynardCheckIn, chatter, shot, "Reynard", "reynard", ReynardCheckInLines);

        DialogueSequenceSO reynardReminder = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Reynard_FindCourier.asset");
        AuthorDialogue(reynardReminder, chatter, shot, "Reynard", "reynard", ReynardReminderLines);

        DialogueSequenceSO tobyThirsty = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Professor_ThirstyOrder.asset");
        AuthorDialogue(tobyThirsty, chatter, shot, "Prof Toby", "prof_toby", TobyThirstyLines);

        DialogueSequenceSO zebTalk = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Zeb_CoffeeCourier.asset");
        AuthorDialogue(zebTalk, chatter, shot, "Zeb", ZebNpcId, ZebCourierLines);

        DialogueSequenceSO zebReturn = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Zeb_BeansReturned.asset");
        AuthorDialogue(zebReturn, chatter, shot, "Zeb", ZebNpcId, ZebReturnLines);

        DialogueSequenceSO zebReminder = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Zeb_DepartedReminder.asset");
        AuthorDialogue(zebReminder, chatter, shot, "Zeb", ZebNpcId, ZebDepartedReminderLines);

        DialogueSequenceSO zebRetry = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Zeb_CofiRetryAdvice.asset");
        AuthorDialogue(zebRetry, chatter, shot, "Zeb", ZebNpcId, ZebRetryAdviceLines);

        DialogueSequenceSO momoBeer = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Momo_ColdBeer.asset");
        AuthorDialogue(momoBeer, chatter, shot, "Momo", "momo", MomoBeerLines);

        DialogueSequenceSO reynardNeedBeer = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Reynard_NeedBeer.asset");
        AuthorDialogue(reynardNeedBeer, chatter, shot, "Reynard", "reynard", ReynardNeedBeerLines);

        DialogueSequenceSO reynardBrew = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Reynard_BrewOrder.asset");
        AuthorDialogue(reynardBrew, chatter, shot, "Reynard", "reynard", ReynardBrewLines);

        DialogueSequenceSO reynardBrewed = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Reynard_OrderPacked.asset");
        AuthorDialogue(reynardBrewed, chatter, shot, "Reynard", "reynard", ReynardBrewedReminderLines);

        DialogueSequenceSO tobyBeans = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Professor_BeansTooEarly.asset");
        AuthorDialogue(tobyBeans, chatter, shot, "Prof Toby", "prof_toby", TobyBeansLines);

        DialogueSequenceSO tobyStall = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_Professor_DeliveryStall.asset");
        AuthorDialogue(tobyStall, chatter, shot, "Prof Toby", "prof_toby", TobyDeliveryStallLines);

        DialogueSequenceSO cofiIntro = EnsureAsset<DialogueSequenceSO>(
            DialogueFolder + "/Dial_CofiLootwak_BossIntro.asset");
        AuthorCombatBossIntroDialogue(cofiIntro, chatter);

        AuthorReynardCoffeeProfile(coffeeQuest, reynardCheckIn, reynardReminder, reynardNeedBeer, reynardBrew, reynardBrewed);
        AuthorTobyActiveProfile(coffeeQuest, tobyThirsty, tobyBeans, tobyStall);
        AuthorTobyRouter();
        AuthorZebProfile(coffeeQuest, zebTalk, zebReturn, zebReminder, zebRetry);
        AuthorMomoBeerRule(momoBeer);
        AuthorLoominaryFallback();

        ScriptableObject encounter = AuthorEncounter();
        SequenceAsset zebSequence = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_courier_zeb_sequence.asset");
        SequenceAsset cofiSequence = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_cofi_lootwak_sequence.asset");
        SequenceAsset zebReturnSequence = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_zeb_return_sequence.asset");
        SequenceAsset momoBeerSequence = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_momo_beer_sequence.asset");
        SequenceAsset reynardBrewSequence = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_reynard_brew_sequence.asset");
        AuthorZebSequence(zebSequence, coffeeQuest, cofiSequence);
        AuthorCofiSequence(cofiSequence, coffeeQuest, zebReturnSequence);
        AuthorZebReturnSequence(zebReturnSequence, momoBeerSequence);
        AuthorMomoBeerSequence(momoBeerSequence, coffeeQuest, reynardBrewSequence);
        AuthorReynardBrewSequence(reynardBrewSequence);
        AuthorCatalog(zebSequence, cofiSequence, zebReturnSequence, momoBeerSequence, reynardBrewSequence);
        AuthorZebPrefab(zebTalk);
        AuthorEncounterRegistry(encounter);
        QueueOnGateSequence(zebSequence);

        WireJungleScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CoffeeCourierHuntAuthoring] Authored coffee delivery items, Zeb's departure, Momo's beer, and Reynard's brew.");
    }

    private static void AuthorTalkToZebObjective(QuestDefinitionSO coffeeQuest)
    {
        var serialized = new SerializedObject(coffeeQuest);
        SerializedProperty objectives = serialized.FindProperty("objectives");
        int existing = -1;
        int talkMartIndex = 0;
        for (int i = 0; i < objectives.arraySize; i++)
        {
            string id = objectives.GetArrayElementAtIndex(i).FindPropertyRelative("objectiveId").stringValue;
            if (id == TalkToZebObjective)
                existing = i;
            if (id == CoffeeMartGateAuthoring.TalkToMartObjective)
                talkMartIndex = i;
        }

        if (existing < 0)
        {
            objectives.InsertArrayElementAtIndex(talkMartIndex + 1);
            existing = talkMartIndex + 1;
        }

        SerializedProperty row = objectives.GetArrayElementAtIndex(existing);
        row.FindPropertyRelative("objectiveId").stringValue = TalkToZebObjective;
        row.FindPropertyRelative("description").stringValue = "Find Reynard's courier Zeb in the jungle";
        row.FindPropertyRelative("targetAmount").intValue = 1;
        serialized.FindProperty("contentVersion").intValue =
            Mathf.Max(4, serialized.FindProperty("contentVersion").intValue);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(coffeeQuest);
    }

    private static void AuthorDeliveryObjectives(QuestDefinitionSO coffeeQuest)
    {
        var serialized = new SerializedObject(coffeeQuest);
        UpsertObjective(
            serialized.FindProperty("objectives"),
            FetchCoffeeObjective,
            "Recover Zeb's coffee beans from Cofi Lootwak",
            1);
        UpsertObjective(
            serialized.FindProperty("objectives"),
            FetchBeerObjective,
            "Pick up Toby's cold beer from Momo",
            1);
        UpsertObjective(
            serialized.FindProperty("objectives"),
            DeliverToTobyObjective,
            "Bring Toby's delivery to the lab",
            1);
        serialized.FindProperty("contentVersion").intValue =
            Mathf.Max(4, serialized.FindProperty("contentVersion").intValue);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(coffeeQuest);
    }

    private static void UpsertObjective(
        SerializedProperty objectives,
        string objectiveId,
        string description,
        int targetAmount)
    {
        int existing = -1;
        for (int i = 0; i < objectives.arraySize; i++)
        {
            if (objectives.GetArrayElementAtIndex(i).FindPropertyRelative("objectiveId").stringValue == objectiveId)
            {
                existing = i;
                break;
            }
        }

        if (existing < 0)
        {
            objectives.InsertArrayElementAtIndex(objectives.arraySize);
            existing = objectives.arraySize - 1;
        }

        SerializedProperty row = objectives.GetArrayElementAtIndex(existing);
        row.FindPropertyRelative("objectiveId").stringValue = objectiveId;
        row.FindPropertyRelative("description").stringValue = description;
        row.FindPropertyRelative("targetAmount").intValue = targetAmount;
    }

    private static void AuthorReynardCoffeeProfile(
        QuestDefinitionSO coffeeQuest,
        DialogueSequenceSO checkIn,
        DialogueSequenceSO reminder,
        DialogueSequenceSO needBeer,
        DialogueSequenceSO brew,
        DialogueSequenceSO brewed)
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(ReynardCoffeePath);
        if (profile == null)
            throw new InvalidOperationException($"Missing '{ReynardCoffeePath}'.");

        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = coffeeQuest;
        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 4;
        ConfigureRule(
            rules.GetArrayElementAtIndex(0),
            "order already brewed",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            brewed,
            "");
        SetWorldFlags(rules.GetArrayElementAtIndex(0), (ReynardBrewedFlag, QuestInteractionMemoryFlagComparison.IsTrue));
        ConfigureRule(
            rules.GetArrayElementAtIndex(1),
            "beans and beer — brew",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            brew,
            ReynardBrewTriggerId);
        SetWorldFlags(
            rules.GetArrayElementAtIndex(1),
            (HoldingCoffeeBeansFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (HoldingColdBeerFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (ReynardBrewedFlag, QuestInteractionMemoryFlagComparison.IsFalse));
        ConfigureRule(
            rules.GetArrayElementAtIndex(2),
            "beans without beer",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            needBeer,
            "");
        SetWorldFlags(
            rules.GetArrayElementAtIndex(2),
            (HoldingCoffeeBeansFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (HoldingColdBeerFlag, QuestInteractionMemoryFlagComparison.IsFalse));
        ConfigureRule(
            rules.GetArrayElementAtIndex(3),
            "after check-in — find Zeb",
            NarrativeQuestLifecycleMask.Active,
            "talk_to_reynard",
            QuestInteractionObjectiveComparison.Complete,
            reminder,
            "");
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = checkIn;
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = reminder;
        serialized.FindProperty("designerNotes").stringValue =
            "Check-in sends the player after Zeb. Later rules ask for the beer, brew the order, or remind Toby's parcel is packed.";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void AuthorTobyActiveProfile(
        QuestDefinitionSO coffeeQuest,
        DialogueSequenceSO thirsty,
        DialogueSequenceSO beans,
        DialogueSequenceSO stall)
    {
        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(TobyActivePath);
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = coffeeQuest;
        serialized.FindProperty("questId").stringValue = "";
        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 2;
        ConfigureRule(
            rules.GetArrayElementAtIndex(0),
            "delivery ready — stall",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            stall,
            "");
        SetWorldFlags(rules.GetArrayElementAtIndex(0), (HoldingTobysDeliveryFlag, QuestInteractionMemoryFlagComparison.IsTrue));
        ConfigureRule(
            rules.GetArrayElementAtIndex(1),
            "beans before Reynard brews",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            beans,
            "");
        SetWorldFlags(
            rules.GetArrayElementAtIndex(1),
            (HoldingCoffeeBeansFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (ReynardBrewedFlag, QuestInteractionMemoryFlagComparison.IsFalse));
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = thirsty;
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = thirsty;
        serialized.FindProperty("designerNotes").stringValue =
            "Beans before the brew get a sarcastic redirect. Toby's packed delivery stalls until the next cinematic pass. Otherwise he nags.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        SetNpcBehavior(serialized.FindProperty("inactiveNpcBehavior"));
        SetNpcBehavior(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void AuthorTobyRouter()
    {
        QuestInteractionRouterSO router = AssetDatabase.LoadAssetAtPath<QuestInteractionRouterSO>(TobyRouterPath);
        QuestInteractionProfileSO intro = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(TobyIntroProfilePath);
        QuestInteractionProfileSO available = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(TobyAvailablePath);
        QuestInteractionProfileSO active = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(TobyActivePath);
        if (router == null || intro == null || available == null || active == null)
            throw new InvalidOperationException("Toby coffee router/profiles are missing.");

        var serialized = new SerializedObject(router);
        SerializedProperty slots = serialized.FindProperty("slots");
        slots.arraySize = 3;
        ConfigureRouterSlot(slots.GetArrayElementAtIndex(0), "Chainbound Intro Active", intro, NarrativeQuestLifecycleMask.Active);
        ConfigureRouterSlot(slots.GetArrayElementAtIndex(1), "Coffee and Beer Active", active, NarrativeQuestLifecycleMask.Active);
        ConfigureRouterSlot(slots.GetArrayElementAtIndex(2), "Coffee and Beer Available", available, NarrativeQuestLifecycleMask.Available);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(router);
    }

    private static void AuthorZebProfile(
        QuestDefinitionSO coffeeQuest,
        DialogueSequenceSO talk,
        DialogueSequenceSO returned,
        DialogueSequenceSO departedReminder,
        DialogueSequenceSO retryAdvice)
    {
        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(ZebProfilePath);
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = coffeeQuest;
        serialized.FindProperty("questId").stringValue = "";
        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 3;
        ConfigureRule(
            rules.GetArrayElementAtIndex(0),
            "already sent the player on",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            departedReminder,
            "");
        SetWorldFlags(rules.GetArrayElementAtIndex(0), (ZebDepartedFlag, QuestInteractionMemoryFlagComparison.IsTrue));
        ConfigureRule(
            rules.GetArrayElementAtIndex(1),
            "beans recovered — send to Reynard and Momo",
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            returned,
            ZebReturnTriggerId);
        SetWorldFlags(
            rules.GetArrayElementAtIndex(1),
            (CofiDefeatedFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (ZebDepartedFlag, QuestInteractionMemoryFlagComparison.IsFalse));
        ConfigureRule(
            rules.GetArrayElementAtIndex(2),
            ZebRetryAdviceRuleLabel,
            NarrativeQuestLifecycleMask.Active,
            "",
            QuestInteractionObjectiveComparison.None,
            retryAdvice,
            ZebCofiRetryTriggerId);
        SetWorldFlags(
            rules.GetArrayElementAtIndex(2),
            (CofiPlayerLostFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (CofiDefeatedFlag, QuestInteractionMemoryFlagComparison.IsFalse));
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = talk;
        serialized.FindProperty("inactiveTriggerId").stringValue = ZebTriggerId;
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = talk;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = ZebTriggerId;
        serialized.FindProperty("designerNotes").stringValue =
            "First talk points at Cofi. A loss sends the player to the sanctuary; talking to Zeb again gives party advice and reopens the boss trigger. After the win, Zeb sends the player to Reynard and Momo. He stays until Jungle loads again.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        SetNpcBehavior(serialized.FindProperty("inactiveNpcBehavior"));
        SetNpcBehavior(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void AuthorMomoBeerRule(DialogueSequenceSO beer)
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(MomoProfilePath);
        if (profile == null)
            throw new InvalidOperationException($"Missing Momo profile at '{MomoProfilePath}'.");

        var serialized = new SerializedObject(profile);
        SerializedProperty rules = serialized.FindProperty("rules");
        int beerIndex = FindRuleIndex(rules, MomoBeerRuleLabel);
        if (beerIndex < 0)
        {
            rules.InsertArrayElementAtIndex(0);
            beerIndex = 0;
        }
        else if (beerIndex != 0)
        {
            rules.MoveArrayElement(beerIndex, 0);
            beerIndex = 0;
        }

        ConfigureRule(
            rules.GetArrayElementAtIndex(beerIndex),
            MomoBeerRuleLabel,
            NarrativeQuestLifecycleMask.Active | NarrativeQuestLifecycleMask.Available | NarrativeQuestLifecycleMask.Completed,
            "",
            QuestInteractionObjectiveComparison.None,
            beer,
            MomoBeerTriggerId);
        SetWorldFlags(
            rules.GetArrayElementAtIndex(beerIndex),
            (ZebDepartedFlag, QuestInteractionMemoryFlagComparison.IsTrue),
            (ColdBeerGrantedFlag, QuestInteractionMemoryFlagComparison.IsFalse));
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static int FindRuleIndex(SerializedProperty rules, string label)
    {
        for (int i = 0; i < rules.arraySize; i++)
        {
            if (rules.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue == label)
                return i;
        }

        return -1;
    }

    private static void AuthorLoominaryFallback()
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(LoominaryProfilePath);
        DialogueSequenceSO broken = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(LoominaryBrokenPath);
        if (profile == null || broken == null)
            return;

        var serialized = new SerializedObject(profile);
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = broken;
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = broken;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static ScriptableObject AuthorEncounter()
    {
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(EncounterPath) == null)
        {
            if (!AssetDatabase.CopyAsset(EncounterSourcePath, EncounterPath))
                throw new InvalidOperationException($"Could not copy '{EncounterSourcePath}' to '{EncounterPath}'.");
        }

        ScriptableObject encounter = AssetDatabase.LoadAssetAtPath<ScriptableObject>(EncounterPath);
        var serialized = new SerializedObject(encounter);
        serialized.FindProperty("authoritativeEncounterId").stringValue = EncounterId;
        serialized.FindProperty("teamName").stringValue = "Cofi Lootwak";
        SerializedProperty difficulty = serialized.FindProperty("difficulty");
        if (difficulty != null)
            difficulty.enumValueIndex = 2;
        SerializedProperty aiPersonality = serialized.FindProperty("aiPersonality");
        if (aiPersonality != null)
            aiPersonality.enumValueIndex = 3;
        SerializedProperty capturePolicy = serialized.FindProperty("capturePolicy");
        if (capturePolicy != null)
            capturePolicy.enumValueIndex = 2;
        serialized.FindProperty("hideCombatUI").boolValue = false;
        serialized.FindProperty("skipManualTurnAfterIntro").boolValue = false;
        SerializedProperty intro = serialized.FindProperty("bossIntroDialogueAsset");
        if (intro != null)
        {
            intro.objectReferenceValue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(
                DialogueFolder + "/Dial_CofiLootwak_BossIntro.asset");
        }

        SerializedProperty roster = serialized.FindProperty("roster");
        if (roster != null)
        {
            if (roster.arraySize < 1)
                roster.arraySize = 1;
            SerializedProperty entry = roster.GetArrayElementAtIndex(0);
            SerializedProperty speciesId = entry.FindPropertyRelative("speciesId");
            if (speciesId != null)
                speciesId.stringValue = "cofi_lootwak";
            SerializedProperty display = entry.FindPropertyRelative("displayName");
            if (display != null)
                display.stringValue = "Cofi Lootwak";
            SerializedProperty stars = entry.FindPropertyRelative("stars");
            if (stars != null)
                stars.intValue = 2;
            SerializedProperty moveIds = entry.FindPropertyRelative("moveIds");
            if (moveIds != null)
            {
                moveIds.arraySize = 2;
                moveIds.GetArrayElementAtIndex(0).stringValue = "bean_sip";
                moveIds.GetArrayElementAtIndex(1).stringValue = "civet_beam";
            }
        }

        SerializedProperty victoryGrants = serialized.FindProperty("victoryKnownGrants");
        if (victoryGrants != null)
        {
            victoryGrants.arraySize = 1;
            SerializedProperty grant = victoryGrants.GetArrayElementAtIndex(0);
            SerializedProperty moveId = grant.FindPropertyRelative("moveId");
            if (moveId != null)
                moveId.stringValue = "bean_sip";
            SerializedProperty idempotency = grant.FindPropertyRelative("idempotencyKey");
            if (idempotency != null)
                idempotency.stringValue = $"boss:{EncounterId}:bean_sip";
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(encounter);
        return encounter;
    }

    private static void AuthorZebSequence(
        SequenceAsset sequence,
        QuestDefinitionSO coffeeQuest,
        SequenceAsset cofiSequence)
    {
        var steps = new List<SequenceStep>
        {
            Label("coffee_zeb_idle", "coffee_zeb_label_idle"),
            Checkpoint("coffee_zeb_idle", "coffee_zeb_checkpoint", "Jungle", "sanctuary_jungle_trailhead"),
            UnlockInput("coffee_zeb_unlock"),
            WaitForTrigger(ZebTriggerId, "coffee_zeb_wait", saveReturn: false),
            Objective(coffeeQuest, TalkToZebObjective, "coffee_zeb_objective"),
            WorldFlag(ZebTalkedFlag, "coffee_zeb_flag"),
            EnableObject(CofiTriggerObjectName, "coffee_zeb_enable_cofi"),
            QueueFollowUp(cofiSequence, "coffee_zeb_queue_cofi"),
        };

        SerializedObject serialized = WriteSequence(sequence, ZebSequenceId, "coffee_zeb_idle", "coffee_zeb_idle", steps);
        serialized.FindProperty("autoStartWhenNoSequenceRunning").boolValue = true;
        serialized.FindProperty("autoStartQuestId").stringValue = CoffeeQuestId;
        serialized.FindProperty("autoStartLifecycleMask").intValue = (int)NarrativeQuestLifecycleMask.Active;
        serialized.FindProperty("autoStartRequiredWorldFlagKey").stringValue = CoffeeMartGateAuthoring.OpenFlag;
        serialized.FindProperty("autoStartSkipWorldFlagKey").stringValue = ZebTalkedFlag;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    private static void AuthorCofiSequence(
        SequenceAsset sequence,
        QuestDefinitionSO coffeeQuest,
        SequenceAsset zebReturn)
    {
        var steps = new List<SequenceStep>
        {
            Label("coffee_cofi_idle", "coffee_cofi_label_idle"),
            Checkpoint("coffee_cofi_idle", "coffee_cofi_checkpoint", "Jungle", "sanctuary_jungle_trailhead"),
            UnlockInput("coffee_cofi_unlock"),
            ResetTrigger(CofiTriggerId, "coffee_cofi_reset"),
            WaitForTrigger(CofiTriggerId, "coffee_cofi_wait", saveReturn: true),
            StartEncounter(EncounterId, "coffee_cofi_encounter"),
            IfEquals("lastEncounterOutcome", "Win", "coffee_cofi_grant", "", "coffee_cofi_if_win", "coffee_cofi_win_condition"),
            ResetTrigger(CofiTriggerId, "coffee_cofi_reset_loss"),
            WorldFlag(CofiPlayerLostFlag, "coffee_cofi_player_lost_flag", true),
            EnableObject(CofiTriggerObjectName, "coffee_cofi_disable_trigger", false),
            Label("coffee_cofi_await_zeb", "coffee_cofi_label_await_zeb"),
            Checkpoint("coffee_cofi_await_zeb", "coffee_cofi_await_checkpoint", "Jungle", "sanctuary_jungle_trailhead"),
            UnlockInput("coffee_cofi_await_unlock"),
            WaitForTrigger(ZebCofiRetryTriggerId, "coffee_cofi_await_zeb", saveReturn: false),
            EnableObject(CofiTriggerObjectName, "coffee_cofi_enable_retry"),
            Jump("coffee_cofi_idle", "coffee_cofi_jump_idle"),
            Label("coffee_cofi_grant", "coffee_cofi_label_grant"),
            ItemDelta(CoffeeBeansIdempotencyKey, CoffeeBeansItemId, "", "", "coffee_cofi_grant_beans"),
            IfEquals("lastItemDeltaOk", "true", "coffee_cofi_granted", "coffee_cofi_grant_retry", "coffee_cofi_if_beans", "coffee_cofi_beans_condition"),
            Label("coffee_cofi_granted", "coffee_cofi_label_granted"),
            WorldFlag(HoldingCoffeeBeansFlag, "coffee_cofi_holding_beans", true),
            Objective(coffeeQuest, FetchCoffeeObjective, "coffee_cofi_objective"),
            WorldFlag(CofiPlayerLostFlag, "coffee_cofi_clear_player_lost", false),
            WorldFlag(CofiDefeatedFlag, "coffee_cofi_defeated_flag", true),
            QueueFollowUp(zebReturn, "coffee_cofi_queue_return"),
            Jump("coffee_cofi_end", "coffee_cofi_jump_end"),
            Label("coffee_cofi_grant_retry", "coffee_cofi_label_retry"),
            WaitSeconds(2f, "coffee_cofi_retry_wait"),
            Jump("coffee_cofi_grant", "coffee_cofi_jump_grant"),
            Label("coffee_cofi_end", "coffee_cofi_label_end"),
        };

        SerializedObject serialized = WriteSequence(sequence, CofiSequenceId, "coffee_cofi_idle", "coffee_cofi_idle", steps);
        serialized.FindProperty("autoStartWhenNoSequenceRunning").boolValue = true;
        serialized.FindProperty("autoStartQuestId").stringValue = CoffeeQuestId;
        serialized.FindProperty("autoStartLifecycleMask").intValue = (int)NarrativeQuestLifecycleMask.Active;
        serialized.FindProperty("autoStartRequiredWorldFlagKey").stringValue = ZebTalkedFlag;
        serialized.FindProperty("autoStartSkipWorldFlagKey").stringValue = CofiDefeatedFlag;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    private static void AuthorZebReturnSequence(SequenceAsset sequence, SequenceAsset momoBeer)
    {
        var steps = new List<SequenceStep>
        {
            Label("coffee_zeb_return_idle", "coffee_zeb_return_label_idle"),
            Checkpoint("coffee_zeb_return_idle", "coffee_zeb_return_checkpoint", "Jungle", "sanctuary_jungle_trailhead"),
            UnlockInput("coffee_zeb_return_unlock"),
            WaitForTrigger(ZebReturnTriggerId, "coffee_zeb_return_wait", saveReturn: false),
            WorldFlag(ZebDepartedFlag, "coffee_zeb_return_flag", true),
            QueueFollowUp(momoBeer, "coffee_zeb_return_queue_momo"),
        };
        WriteAutoStart(
            sequence,
            ZebReturnSequenceId,
            "coffee_zeb_return_idle",
            steps,
            CofiDefeatedFlag,
            ZebDepartedFlag);
    }

    private static void AuthorMomoBeerSequence(
        SequenceAsset sequence,
        QuestDefinitionSO coffeeQuest,
        SequenceAsset reynardBrew)
    {
        var steps = new List<SequenceStep>
        {
            Label("coffee_momo_idle", "coffee_momo_label_idle"),
            Checkpoint("coffee_momo_idle", "coffee_momo_checkpoint", "Jungle", "sanctuary_jungle_trailhead"),
            UnlockInput("coffee_momo_unlock"),
            Label("coffee_momo_wait", "coffee_momo_label_wait"),
            WaitForTrigger(MomoBeerTriggerId, "coffee_momo_wait", saveReturn: false),
            Label("coffee_momo_grant", "coffee_momo_label_grant"),
            ItemDelta(ColdBeerIdempotencyKey, ColdBeerItemId, "", "", "coffee_momo_grant_beer"),
            IfEquals("lastItemDeltaOk", "true", "coffee_momo_granted", "coffee_momo_retry", "coffee_momo_if_beer", "coffee_momo_beer_condition"),
            Label("coffee_momo_granted", "coffee_momo_label_granted"),
            WorldFlag(HoldingColdBeerFlag, "coffee_momo_holding_beer", true),
            WorldFlag(ColdBeerGrantedFlag, "coffee_momo_beer_flag", true),
            Objective(coffeeQuest, FetchBeerObjective, "coffee_momo_objective"),
            QueueFollowUp(reynardBrew, "coffee_momo_queue_reynard"),
            Jump("coffee_momo_end", "coffee_momo_jump_end"),
            Label("coffee_momo_retry", "coffee_momo_label_retry"),
            ResetTrigger(MomoBeerTriggerId, "coffee_momo_reset"),
            Jump("coffee_momo_wait", "coffee_momo_jump_wait"),
            Label("coffee_momo_end", "coffee_momo_label_end"),
        };
        WriteAutoStart(
            sequence,
            MomoBeerSequenceId,
            "coffee_momo_idle",
            steps,
            ZebDepartedFlag,
            ColdBeerGrantedFlag);
    }

    private static void AuthorReynardBrewSequence(SequenceAsset sequence)
    {
        var steps = new List<SequenceStep>
        {
            Label("coffee_reynard_idle", "coffee_reynard_label_idle"),
            Checkpoint("coffee_reynard_idle", "coffee_reynard_checkpoint", "Jungle", "sanctuary_jungle_trailhead"),
            UnlockInput("coffee_reynard_unlock"),
            Label("coffee_reynard_wait", "coffee_reynard_label_wait"),
            WaitForTrigger(ReynardBrewTriggerId, "coffee_reynard_wait", saveReturn: false),
            Label("coffee_reynard_grant", "coffee_reynard_label_grant"),
            ItemDelta(
                ReynardBrewIdempotencyKey,
                TobysDeliveryItemId,
                CoffeeBeansItemId,
                ColdBeerItemId,
                "coffee_reynard_brew_delta"),
            IfEquals("lastItemDeltaOk", "true", "coffee_reynard_granted", "coffee_reynard_retry", "coffee_reynard_if_brew", "coffee_reynard_brew_condition"),
            Label("coffee_reynard_granted", "coffee_reynard_label_granted"),
            WorldFlag(HoldingCoffeeBeansFlag, "coffee_reynard_clear_beans", false),
            WorldFlag(HoldingColdBeerFlag, "coffee_reynard_clear_beer", false),
            WorldFlag(HoldingTobysDeliveryFlag, "coffee_reynard_holding_delivery", true),
            WorldFlag(ReynardBrewedFlag, "coffee_reynard_brewed_flag", true),
            Jump("coffee_reynard_end", "coffee_reynard_jump_end"),
            Label("coffee_reynard_retry", "coffee_reynard_label_retry"),
            ResetTrigger(ReynardBrewTriggerId, "coffee_reynard_reset"),
            Jump("coffee_reynard_wait", "coffee_reynard_jump_wait"),
            Label("coffee_reynard_end", "coffee_reynard_label_end"),
        };
        WriteAutoStart(
            sequence,
            ReynardBrewSequenceId,
            "coffee_reynard_idle",
            steps,
            ColdBeerGrantedFlag,
            ReynardBrewedFlag);
    }

    private static void WriteAutoStart(
        SequenceAsset sequence,
        string sequenceId,
        string idleLabel,
        List<SequenceStep> steps,
        string requiredFlag,
        string skipFlag)
    {
        SerializedObject serialized = WriteSequence(sequence, sequenceId, idleLabel, idleLabel, steps);
        serialized.FindProperty("autoStartWhenNoSequenceRunning").boolValue = true;
        serialized.FindProperty("autoStartQuestId").stringValue = CoffeeQuestId;
        serialized.FindProperty("autoStartLifecycleMask").intValue = (int)NarrativeQuestLifecycleMask.Active;
        serialized.FindProperty("autoStartRequiredWorldFlagKey").stringValue = requiredFlag;
        serialized.FindProperty("autoStartSkipWorldFlagKey").stringValue = skipFlag;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    private static void QueueOnGateSequence(SequenceAsset zebSequence)
    {
        SequenceAsset gate = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            SequenceFolder + "/coffee_loomville_gate_sequence.asset");
        if (gate == null)
            throw new InvalidOperationException("coffee_loomville_gate_sequence is missing. Author Coffee Mart Gate first.");

        Step_QueueSequence queue = QueueFollowUp(zebSequence, "coffee_main_queue_courier");
        var serialized = new SerializedObject(gate);
        SerializedProperty steps = serialized.FindProperty("steps");
        for (int i = 0; i < steps.arraySize; i++)
        {
            if (steps.GetArrayElementAtIndex(i).objectReferenceValue == queue)
            {
                serialized.Dispose();
                return;
            }
        }

        steps.InsertArrayElementAtIndex(steps.arraySize);
        steps.GetArrayElementAtIndex(steps.arraySize - 1).objectReferenceValue = queue;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gate);
    }

    private static void AuthorCatalog(params SequenceAsset[] sequences)
    {
        SequenceCatalogSO catalog = AssetDatabase.LoadAssetAtPath<SequenceCatalogSO>(CatalogPath);
        if (catalog == null)
            throw new InvalidOperationException($"Missing sequence catalog at '{CatalogPath}'.");

        var serialized = new SerializedObject(catalog);
        SerializedProperty list = serialized.FindProperty("sequences");
        for (int i = 0; i < sequences.Length; i++)
        {
            SequenceAsset sequence = sequences[i];
            bool found = false;
            for (int j = 0; j < list.arraySize; j++)
            {
                if (list.GetArrayElementAtIndex(j).objectReferenceValue == sequence)
                {
                    found = true;
                    break;
                }
            }

            if (found)
                continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sequence;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void AuthorZebPrefab(DialogueSequenceSO talk)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ZebPrefabPath) == null &&
            !AssetDatabase.CopyAsset(BertPrefabPath, ZebPrefabPath))
        {
            throw new InvalidOperationException($"Could not create '{ZebPrefabPath}' from Bert.");
        }

        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(ZebProfilePath);
        GameObject root = PrefabUtility.LoadPrefabContents(ZebPrefabPath);
        try
        {
            root.name = "Zeb";
            NPCController controller = root.GetComponent<NPCController>();
            if (controller == null)
                throw new InvalidOperationException("Zeb prefab source has no NPCController.");
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("npcId").stringValue = ZebNpcId;
            controllerSo.FindProperty("routeSchedule").objectReferenceValue = null;
            SerializedProperty behavior = controllerSo.FindProperty("interactionBehavior");
            if (behavior != null)
                behavior.objectReferenceValue = AssetDatabase.LoadAssetAtPath<NPCInteractionBehaviorSO>(InteractionBehaviorPath);
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            QuestProgressInteractionBridge bridge = root.GetComponent<QuestProgressInteractionBridge>();
            if (bridge == null)
                bridge = root.AddComponent<QuestProgressInteractionBridge>();
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("defaultProfile").objectReferenceValue = profile;
            bridgeSo.FindProperty("interactionRouter").objectReferenceValue = null;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();
            AuthorInteractable(root, bridge);
            PrefabUtility.SaveAsPrefabAsset(root, ZebPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AuthorEncounterRegistry(ScriptableObject encounter)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameServicesPath);
        try
        {
            MonoBehaviour service = null;
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().Name == "NarrativeEncounterService")
                {
                    service = behaviour;
                    break;
                }
            }

            if (service == null)
                throw new InvalidOperationException("GameServices is missing NarrativeEncounterService.");

            var serialized = new SerializedObject(service);
            SerializedProperty registry = serialized.FindProperty("registry");
            int existing = -1;
            for (int i = 0; i < registry.arraySize; i++)
            {
                if (registry.GetArrayElementAtIndex(i).FindPropertyRelative("encounterId").stringValue == EncounterId)
                    existing = i;
            }

            if (existing < 0)
            {
                registry.InsertArrayElementAtIndex(registry.arraySize);
                existing = registry.arraySize - 1;
            }

            SerializedProperty entry = registry.GetArrayElementAtIndex(existing);
            entry.FindPropertyRelative("encounterId").stringValue = EncounterId;
            entry.FindPropertyRelative("encounterProvider").objectReferenceValue = encounter;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GameServicesPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireJungleScene()
    {
        Scene scene = SceneManager.GetSceneByPath(JungleScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(JungleScenePath, OpenSceneMode.Single);

        GameObject spawn = FindSceneObject(scene, "SpawnPoint_sanctuary_jungle_trailhead");
        Vector3 origin = spawn != null ? spawn.transform.position : new Vector3(340f, -84.35f, 403.53f);

        GameObject root = FindSceneObject(scene, GatesRootName);
        if (root == null)
        {
            root = new GameObject(GatesRootName);
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        root.transform.position = Vector3.zero;
        GameObject zebPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ZebPrefabPath);
        GameObject zeb = FindSceneObject(scene, "Zeb");
        if (zeb == null)
        {
            zeb = (GameObject)PrefabUtility.InstantiatePrefab(zebPrefab, scene);
            zeb.name = "Zeb";
            zeb.transform.SetParent(root.transform, true);
        }

        zeb.transform.position = origin + new Vector3(8f, 0f, 6f);
        zeb.SetActive(false);
        AuthorPresence(EnsureChild(root.transform, "Zeb_Presence"), CoffeeMartGateAuthoring.OpenFlag, zeb, true);

        GameObject cofi = EnsureChild(root.transform, CofiTriggerObjectName);
        cofi.transform.position = origin + new Vector3(22f, 0f, 18f);
        EnsureBox(cofi, new Vector3(0f, 4f, 0f), new Vector3(16f, 10f, 16f), true);
        AuthorSequenceTrigger(cofi, CofiTriggerId, fireOnce: false, startArmed: false);
        cofi.SetActive(false);
        AuthorPresence(EnsureChild(root.transform, "CofiLootwak_Presence"), ZebTalkedFlag, cofi, true);

        GameObject cart = EnsureChild(root.transform, "Zeb_BrokenDeliveryCart");
        cart.transform.position = zeb.transform.position + new Vector3(2f, 0f, -2f);
        AuthorPresence(EnsureChild(root.transform, "Zeb_DepartedPresence"), ZebDepartedFlag, zeb, false, applyOnEnableOnly: true);
        AuthorPresence(EnsureChild(root.transform, "ZebCart_DepartedPresence"), ZebDepartedFlag, cart, false, applyOnEnableOnly: true);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save '{JungleScenePath}'.");
    }

    private static void ConfigureRule(
        SerializedProperty rule,
        string label,
        NarrativeQuestLifecycleMask lifecycle,
        string objectiveId,
        QuestInteractionObjectiveComparison comparison,
        DialogueSequenceSO dialogue,
        string triggerId)
    {
        rule.FindPropertyRelative("label").stringValue = label;
        rule.FindPropertyRelative("lifecycleScope").enumValueIndex =
            (int)QuestInteractionRuleLifecycleScope.UseLifecycleMask;
        rule.FindPropertyRelative("lifecycleMask").intValue = (int)lifecycle;
        rule.FindPropertyRelative("objectiveId").stringValue = objectiveId;
        rule.FindPropertyRelative("objectiveComparison").enumValueIndex = (int)comparison;
        rule.FindPropertyRelative("objectiveValue").intValue = 0;
        ClearArray(rule, "sequenceProgressPredicates");
        ClearArray(rule, "resolutionPredicates");
        ClearArray(rule, "flagPredicates");
        ClearArray(rule, "worldFlagPredicates");
        ClearArray(rule, "counterPredicates");
        rule.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
        rule.FindPropertyRelative("useDialogueCamera").boolValue = true;
        rule.FindPropertyRelative("triggerId").stringValue = triggerId;
        SetNpcBehavior(rule.FindPropertyRelative("npcBehavior"));
    }

    private static void ConfigureRouterSlot(
        SerializedProperty slot,
        string label,
        QuestInteractionProfileSO profile,
        NarrativeQuestLifecycleMask mask)
    {
        slot.FindPropertyRelative("label").stringValue = label;
        slot.FindPropertyRelative("profile").objectReferenceValue = profile;
        slot.FindPropertyRelative("allowedLifecycleMask").intValue = (int)mask;
    }

    private static SerializedObject WriteSequence(
        SequenceAsset sequence,
        string sequenceId,
        string startCheckpointId,
        string safeResumeLabel,
        List<SequenceStep> steps)
    {
        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("sequenceId").stringValue = sequenceId;
        serialized.FindProperty("startCheckpointId").stringValue = startCheckpointId;
        serialized.FindProperty("safeResumeLabel").stringValue = safeResumeLabel;
        serialized.FindProperty("contentVersion").intValue = 1;
        SerializedProperty list = serialized.FindProperty("steps");
        list.arraySize = steps.Count;
        for (int i = 0; i < steps.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = steps[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        return serialized;
    }

    private static Step_Label Label(string labelName, string fileName) =>
        ConfigureStep<Step_Label>(fileName, so => so.FindProperty("labelName").stringValue = labelName);

    private static Step_SetCheckpoint Checkpoint(string label, string fileName, string scene, string spawn) =>
        ConfigureStep<Step_SetCheckpoint>(fileName, so =>
        {
            so.FindProperty("checkpointId").stringValue = label;
            so.FindProperty("checkpointLabel").stringValue = label;
            so.FindProperty("saveToState").boolValue = true;
            so.FindProperty("resumeSceneId").stringValue = scene;
            so.FindProperty("resumeSpawnId").stringValue = spawn;
        });

    private static Step_UnlockInput UnlockInput(string fileName) =>
        ConfigureStep<Step_UnlockInput>(fileName, _ => { });

    private static Step_WaitForTrigger WaitForTrigger(string triggerId, string fileName, bool saveReturn) =>
        ConfigureStep<Step_WaitForTrigger>(fileName, so =>
        {
            so.FindProperty("triggerId").stringValue = triggerId;
            so.FindProperty("armTrigger").boolValue = true;
            so.FindProperty("saveReturnSnapshotOnTriggerComplete").boolValue = saveReturn;
            so.FindProperty("returnSpawnPointId").stringValue = saveReturn ? "sanctuary_jungle_trailhead" : "";
        });

    private static Step_SetQuestObjectiveProgress Objective(QuestDefinitionSO quest, string objectiveId, string fileName) =>
        ConfigureStep<Step_SetQuestObjectiveProgress>(fileName, so =>
        {
            so.FindProperty("questId").stringValue = quest.QuestId;
            so.FindProperty("objectiveId").stringValue = objectiveId;
            so.FindProperty("completeObjective").boolValue = true;
            so.FindProperty("autoCompleteQuest").boolValue = false;
        });

    private static Step_SetProfileWorldFlag WorldFlag(string flagKey, string fileName, bool value = true) =>
        ConfigureStep<Step_SetProfileWorldFlag>(fileName, so =>
        {
            so.FindProperty("flagKey").stringValue = flagKey;
            so.FindProperty("flagValue").boolValue = value;
            so.FindProperty("lifetimeOverride").enumValueIndex = (int)WorldPresenceLifetime.Unspecified;
        });

    private static Step_ResetTrigger ResetTrigger(string triggerId, string fileName) =>
        ConfigureStep<Step_ResetTrigger>(fileName, so => so.FindProperty("triggerId").stringValue = triggerId);

    private static Step_JumpToLabel Jump(string label, string fileName) =>
        ConfigureStep<Step_JumpToLabel>(fileName, so => so.FindProperty("targetLabel").stringValue = label);

    private static Step_WaitSeconds WaitSeconds(float seconds, string fileName) =>
        ConfigureStep<Step_WaitSeconds>(fileName, so => so.FindProperty("durationSeconds").floatValue = seconds);

    private static Step_If IfEquals(
        string variableName,
        string compareValue,
        string trueLabel,
        string falseLabel,
        string fileName,
        string conditionFile)
    {
        ConditionVariableCompare condition = EnsureAsset<ConditionVariableCompare>($"{StepsFolder}/{conditionFile}.asset");
        var conditionObject = new SerializedObject(condition);
        conditionObject.FindProperty("variableName").stringValue = variableName;
        conditionObject.FindProperty("comparisonType").enumValueIndex = (int)ConditionVariableCompare.ComparisonType.Equals;
        conditionObject.FindProperty("compareValue").stringValue = compareValue;
        conditionObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(condition);

        return ConfigureStep<Step_If>(fileName, so =>
        {
            so.FindProperty("condition").objectReferenceValue = condition;
            so.FindProperty("trueLabel").stringValue = trueLabel;
            so.FindProperty("falseLabel").stringValue = falseLabel;
        });
    }

    private static Step_ApplyProfileItemDelta ItemDelta(
        string idempotencyKey,
        string grantItemId,
        string consumeItemId,
        string secondConsumeItemId,
        string fileName) =>
        ConfigureStep<Step_ApplyProfileItemDelta>(fileName, so =>
        {
            so.FindProperty("idempotencyKey").stringValue = idempotencyKey;
            so.FindProperty("resultVariableName").stringValue = "lastItemDeltaOk";
            SerializedProperty grants = so.FindProperty("grants");
            grants.arraySize = string.IsNullOrEmpty(grantItemId) ? 0 : 1;
            if (!string.IsNullOrEmpty(grantItemId))
            {
                SerializedProperty grant = grants.GetArrayElementAtIndex(0);
                grant.FindPropertyRelative("itemId").stringValue = grantItemId;
                grant.FindPropertyRelative("quantity").intValue = 1;
            }

            int consumeCount = 0;
            if (!string.IsNullOrEmpty(consumeItemId))
                consumeCount++;
            if (!string.IsNullOrEmpty(secondConsumeItemId))
                consumeCount++;
            SerializedProperty consumes = so.FindProperty("consumes");
            consumes.arraySize = consumeCount;
            int index = 0;
            if (!string.IsNullOrEmpty(consumeItemId))
            {
                SerializedProperty consume = consumes.GetArrayElementAtIndex(index++);
                consume.FindPropertyRelative("itemId").stringValue = consumeItemId;
                consume.FindPropertyRelative("quantity").intValue = 1;
            }

            if (!string.IsNullOrEmpty(secondConsumeItemId))
            {
                SerializedProperty consume = consumes.GetArrayElementAtIndex(index);
                consume.FindPropertyRelative("itemId").stringValue = secondConsumeItemId;
                consume.FindPropertyRelative("quantity").intValue = 1;
            }
        });

    private static Step_EnableGameObject EnableObject(string objectName, string fileName, bool enable = true) =>
        ConfigureStep<Step_EnableGameObject>(fileName, so =>
        {
            so.FindProperty("gameObjectName").stringValue = objectName;
            so.FindProperty("enable").boolValue = enable;
            so.FindProperty("includeInactive").boolValue = true;
            so.FindProperty("persistAcrossSceneLoads").boolValue = true;
            so.FindProperty("allowMissingWhenPersisting").boolValue = true;
            so.FindProperty("persistAsProfileWorldFlag").boolValue = false;
        });

    private static Step_QueueSequence QueueFollowUp(SequenceAsset followUp, string fileName) =>
        ConfigureStep<Step_QueueSequence>(fileName, so =>
        {
            so.FindProperty("sequence").objectReferenceValue = followUp;
            so.FindProperty("acquireInputLock").boolValue = false;
        });

    private static Step_StartEncounter StartEncounter(string encounterId, string fileName) =>
        ConfigureStep<Step_StartEncounter>(fileName, so =>
        {
            so.FindProperty("encounterId").stringValue = encounterId;
            so.FindProperty("canRun").boolValue = false;
            so.FindProperty("canCapture").boolValue = false;
            so.FindProperty("skipSaveReturnContext").boolValue = true;
            so.FindProperty("returnSpawnPointId").stringValue = "sanctuary_jungle_trailhead";
        });

    private static T ConfigureStep<T>(string fileName, Action<SerializedObject> configure) where T : SequenceStep
    {
        T step = EnsureAsset<T>($"{StepsFolder}/{fileName}.asset");
        var serialized = new SerializedObject(step);
        serialized.FindProperty("stepName").stringValue = fileName;
        configure?.Invoke(serialized);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        _ = step.StepGuid;
        EditorUtility.SetDirty(step);
        return step;
    }

    private static void AuthorDialogue(
        DialogueSequenceSO dialogue,
        SoundEventSO chatter,
        CameraShotPresetSO shot,
        string speakerName,
        string npcId,
        params string[] text)
    {
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = npcId;
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerName").stringValue = speakerName;
            line.FindPropertyRelative("text").stringValue = text[i];
            line.FindPropertyRelative("typeSpeed").floatValue = 75f;
            line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
            SerializedProperty camera = line.FindPropertyRelative("camera");
            camera.FindPropertyRelative("shotPreset").objectReferenceValue = shot;
            camera.FindPropertyRelative("targetSource").enumValueIndex = (int)DialogueCameraTargetSource.NpcId;
            camera.FindPropertyRelative("npcId").stringValue = npcId;
        }

        serialized.FindProperty("autoAdvance").boolValue = false;
        serialized.FindProperty("allowSkip").boolValue = true;
        serialized.FindProperty("skipToEndOfLine").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialogue);
    }

    private static void AuthorCombatBossIntroDialogue(DialogueSequenceSO dialogue, SoundEventSO chatter)
    {
        Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Sprites/Combat/BossBattles/Cofi_Lootwak/cofi_lootwak1.png");
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = string.Empty;
        serialized.FindProperty("playerFacingPolicy").enumValueIndex = 0;
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = CofiBossIntroLines.Length;
        for (int i = 0; i < CofiBossIntroLines.Length; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerName").stringValue = "Cofi Lootwak";
            line.FindPropertyRelative("text").stringValue = CofiBossIntroLines[i];
            line.FindPropertyRelative("typeSpeed").floatValue = 75f;
            line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
            line.FindPropertyRelative("portraitSprite").objectReferenceValue = portrait;
            SerializedProperty camera = line.FindPropertyRelative("camera");
            camera.FindPropertyRelative("shotPreset").objectReferenceValue = null;
            camera.FindPropertyRelative("targetSource").enumValueIndex = 0;
            camera.FindPropertyRelative("npcId").stringValue = string.Empty;
        }

        serialized.FindProperty("autoAdvance").boolValue = false;
        serialized.FindProperty("allowSkip").boolValue = true;
        serialized.FindProperty("skipToEndOfLine").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialogue);
    }

    private static void AuthorInteractable(GameObject root, QuestProgressInteractionBridge bridge)
    {
        Transform found = root.transform.Find("Interactable");
        GameObject interaction = found != null ? found.gameObject : new GameObject("Interactable");
        interaction.transform.SetParent(root.transform, false);
        interaction.transform.localPosition = new Vector3(0f, 1f, 0f);
        interaction.transform.localRotation = Quaternion.identity;
        interaction.transform.localScale = new Vector3(4f, 3f, 4f);

        SphereCollider collider = interaction.GetComponent<SphereCollider>();
        if (collider == null)
            collider = interaction.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.85f;

        InteractableObject interactable = interaction.GetComponent<InteractableObject>();
        if (interactable == null)
            interactable = interaction.AddComponent<InteractableObject>();
        var serialized = new SerializedObject(interactable);
        serialized.FindProperty("triggerMode").enumValueIndex = (int)InteractableObject.TriggerMode.RequireInteract;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.ApplyModifiedPropertiesWithoutUndo();

        FieldInfo eventField = typeof(InteractableObject).GetField("onInteracted", BindingFlags.Instance | BindingFlags.NonPublic);
        var onInteracted = eventField.GetValue(interactable) as UnityEvent ?? new UnityEvent();
        while (onInteracted.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(onInteracted, 0);
        UnityEventTools.AddPersistentListener(onInteracted, bridge.OnInteract);
        eventField.SetValue(interactable, onInteracted);
        NpcInteractableAuthoring.EnsureIndicator(interaction);
    }

    private static void AuthorSequenceTrigger(GameObject host, string triggerId, bool fireOnce, bool startArmed)
    {
        SequenceTrigger trigger = host.GetComponent<SequenceTrigger>();
        if (trigger == null)
            trigger = host.AddComponent<SequenceTrigger>();
        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("triggerId").stringValue = triggerId;
        serialized.FindProperty("fireOnce").boolValue = fireOnce;
        serialized.FindProperty("startArmed").boolValue = startArmed;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.FindProperty("fireOnEnter").boolValue = true;
        serialized.FindProperty("fireOnExit").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AuthorPresence(
        GameObject host,
        string flag,
        GameObject target,
        bool activeWhenTrue,
        bool applyOnEnableOnly = false)
    {
        WorldPresenceBinding binding = host.GetComponent<WorldPresenceBinding>();
        if (binding == null)
            binding = host.AddComponent<WorldPresenceBinding>();
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("presenceId").stringValue = flag;
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.FindProperty("activeWhenTrue").boolValue = activeWhenTrue;
        serialized.FindProperty("applyOnEnableOnly").boolValue = applyOnEnableOnly;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetWorldFlags(
        SerializedProperty rule,
        params (string key, QuestInteractionMemoryFlagComparison comparison)[] flags)
    {
        SerializedProperty list = rule.FindPropertyRelative("worldFlagPredicates");
        list.arraySize = flags.Length;
        for (int i = 0; i < flags.Length; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("key").stringValue = flags[i].key;
            row.FindPropertyRelative("comparison").enumValueIndex = (int)flags[i].comparison;
        }
    }

    private static GameObject EnsureChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.gameObject;
        var child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static BoxCollider EnsureBox(GameObject host, Vector3 center, Vector3 size, bool isTrigger)
    {
        BoxCollider box = host.GetComponent<BoxCollider>();
        if (box == null)
            box = host.AddComponent<BoxCollider>();
        box.isTrigger = isTrigger;
        box.center = center;
        box.size = size;
        return box;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.name, objectName, StringComparison.Ordinal))
                    return transform.gameObject;
            }
        }

        return null;
    }

    private static void SetNpcBehavior(SerializedProperty binding)
    {
        if (binding == null)
            return;
        binding.FindPropertyRelative("mode").enumValueIndex = (int)QuestInteractionNpcBehaviorMode.UseNpcDefault;
        binding.FindPropertyRelative("behaviorOverride").objectReferenceValue = null;
    }

    private static void ClearArray(SerializedProperty parent, string relativeName)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);
        if (property != null && property.isArray)
            property.arraySize = 0;
    }

    private static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }
}
#endif
