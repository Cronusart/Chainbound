#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent authoring for coffee-quest Mart leftover permission, shop access,
/// and the Loomville → Link 1 exit intercepts.
/// </summary>
public static class CoffeeMartGateAuthoring
{
    public static readonly string[] LeftoverDialogueLines =
    {
        "Hold on!... Is that one of Toby's orbs?",
        "I built those. Tiny shell, impossible geometry, ridiculous demand. Best problem I've ever had.",
        "Toby's trust doesn't come lightly. He doesn't hand lab Chainlings to just anyone.",
        "If you're good enough for him, I can trust the dog too. That's as close to a credit check as I need.",
        "Shop's closing. Not folding — expanding. Loomspire's a bigger market, bigger workshop. One supershop for every tamer who can pay.",
        "Truck's leaving soon. I left an extra orb in one of the moving crates in the store. Help yourself. Consider it user acquisition.",
        "Anything you catch with those orbs, you manage from a Chainling PC. Storage, party, the whole roster. Don't leave them rattling around in your pockets."
    };

    public const string OpenFlag = "world.loomville_mart.open";
    public const string ExitTriggerId = "loomville_exit_mart_intercept";
    public const string DoorTriggerId = "mart_door_closed";
    public const string StandingTriggerId = "mart_shop_permission";
    public const string LeftoverSignalId = "mart_leftover_granted";
    public const string TalkToMartObjective = "talk_to_mart";
    public const string CoffeeQuestId = "coffee_and_beer";
    public const string GateSequenceId = "coffee_loomville_gate_sequence";

    public const string DoorTriggerObjectName = "LoomvilleMart_DoorTrigger";
    public const string TransitionObjectName = "TransitionCollider";
    public const string DoorClosedTriggerName = "SeqTrigger_MartDoorClosed";
    public const string ExitTriggerName = "SeqTrigger_LoomvilleExitMart";
    public const string ReynardRedirectName = "Redirect_TalkToReynard";
    public const string ExitBlockerName = "Loomville_Link1_ExitBlocker";
    public const string GatesRootName = "Narrative_LoomvilleGates";

    private const string LoomvilleScenePath = "Assets/Scenes/Loomville.unity";
    private const string CoffeeQuestPath = "Assets/Scripts/Narrative/Quests/Quest_CoffeeAndBeer.asset";
    private const string IntroQuestPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/Chainbound_Intro.asset";
    private const string IntroSequencePath =
        "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_sequence.asset";
    private const string CatalogPath = "Assets/Resources/Narrative/SequenceCatalog.asset";
    private const string MartFolder = "Assets/Scripts/NPC/Interaction/Loomville/Mart";
    private const string IntroProfilePath = MartFolder + "/mart_chainbound_intro_profile.asset";
    private const string CoffeeProfilePath = MartFolder + "/mart_coffee_interaction_profile.asset";
    private const string RouterPath = MartFolder + "/Mart_InteractionRouter.asset";
    private const string PackingDialoguePath = MartFolder + "/Dial_Mart_Packing.asset";
    private const string LeftoverDialoguePath = MartFolder + "/Dial_Mart_OrbCreator.asset";
    private const string HelpYourselfPath = MartFolder + "/Dial_Mart_HelpYourself.asset";
    private const string ReynardNagPath =
        "Assets/Scripts/Narrative/Quests/coffee_and_beer/DialogueSO/Dial_LoomvilleExit_TalkToReynard.asset";
    private const string SequenceFolder = "Assets/Scripts/Narrative/Quests/coffee_and_beer/Sequence";
    private const string StepsFolder = SequenceFolder + "/Steps";
    private const string MartPrefabPath = "Assets/Prefabs/NPC/Mart.prefab";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";

    [MenuItem("Tools/Chainbound/Narrative/Author Coffee Mart Gate")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    public static void Apply()
    {
        EnsureFolder("Assets/Scripts/Narrative/Quests/coffee_and_beer");
        EnsureFolder("Assets/Scripts/Narrative/Quests/coffee_and_beer/DialogueSO");
        EnsureFolder(SequenceFolder);
        EnsureFolder(StepsFolder);

        QuestDefinitionSO coffeeQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(CoffeeQuestPath);
        if (coffeeQuest == null)
            throw new InvalidOperationException($"Missing coffee quest at '{CoffeeQuestPath}'.");
        AuthorCoffeeQuestObjectives(coffeeQuest);

        QuestDefinitionSO introQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(IntroQuestPath);
        DialogueSequenceSO packing = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(PackingDialoguePath);
        DialogueSequenceSO leftover = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(LeftoverDialoguePath);
        if (packing == null || leftover == null)
            throw new InvalidOperationException("Mart packing/leftover dialogues are missing. Run Author Loomville Mart NPC first.");

        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        CameraShotPresetSO shot = AssetDatabase.LoadAssetAtPath<CameraShotPresetSO>(
            AssetDatabase.GUIDToAssetPath("e2e99b5b538b6914d99ce0ebfaca93f7"));
        AuthorDialogue(leftover, chatter, shot, "Mart", "mart", LeftoverDialogueLines);

        DialogueSequenceSO reynardNag = EnsureAsset<DialogueSequenceSO>(ReynardNagPath);
        AuthorDialogue(reynardNag, chatter, shot, "Reynard", "reynard",
            "Hold up. Toby's coffee order isn't going to fetch itself.",
            "Check in with Reynard at the cart before you leave town.");

        DialogueSequenceSO helpYourself = EnsureAsset<DialogueSequenceSO>(HelpYourselfPath);
        AuthorDialogue(helpYourself, chatter, shot, "Mart", "mart",
            "Last of the stock is going in the truck. Shop's open if you still want that leftover orb.",
            "Don't linger. Loomspire won't wait, and neither will I. Safe travels — try not to waste the hardware.");

        QuestInteractionProfileSO introProfile =
            AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(IntroProfilePath);
        QuestInteractionProfileSO coffeeProfile = EnsureAsset<QuestInteractionProfileSO>(CoffeeProfilePath);
        AuthorCoffeeProfile(coffeeProfile, coffeeQuest, leftover, helpYourself, packing);

        QuestInteractionRouterSO router = EnsureAsset<QuestInteractionRouterSO>(RouterPath);
        AuthorMartRouter(router, coffeeProfile, introProfile);

        SequenceAsset doorTrack = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_loomville_gate_door_track.asset");
        SequenceAsset exitTrack = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_loomville_gate_exit_track.asset");
        SequenceAsset standingTrack = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_loomville_gate_standing_track.asset");
        SequenceAsset mainSequence = EnsureAsset<SequenceAsset>(SequenceFolder + "/coffee_loomville_gate_sequence.asset");

        AuthorDoorTrack(doorTrack, packing);
        AuthorStandingTrack(standingTrack, coffeeQuest);
        AuthorExitTrack(exitTrack, leftover, coffeeQuest);
        AuthorMainSequence(mainSequence, doorTrack, exitTrack, standingTrack);
        AuthorQueueStepOnIntro(mainSequence);
        AuthorCatalog(mainSequence);
        AuthorMartPrefabRouter(router, introProfile);

        AssetDatabase.SaveAssets();
        WireLoomvilleScene(reynardNag, coffeeQuest);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CoffeeMartGateAuthoring] Authored coffee leftover permission, shop access, and Link 1 exit intercepts.");
    }

    private static void AuthorCoffeeQuestObjectives(QuestDefinitionSO coffeeQuest)
    {
        var serialized = new SerializedObject(coffeeQuest);
        serialized.FindProperty("contentVersion").intValue = Mathf.Max(2, serialized.FindProperty("contentVersion").intValue);
        SerializedProperty objectives = serialized.FindProperty("objectives");
        int talkMartIndex = -1;
        int talkReynardIndex = 0;
        for (int i = 0; i < objectives.arraySize; i++)
        {
            string id = objectives.GetArrayElementAtIndex(i).FindPropertyRelative("objectiveId").stringValue;
            if (id == TalkToMartObjective)
                talkMartIndex = i;
            if (id == "talk_to_reynard")
                talkReynardIndex = i;
        }

        if (talkMartIndex < 0)
        {
            objectives.InsertArrayElementAtIndex(talkReynardIndex + 1);
            talkMartIndex = talkReynardIndex + 1;
        }

        SerializedProperty talkMart = objectives.GetArrayElementAtIndex(talkMartIndex);
        talkMart.FindPropertyRelative("objectiveId").stringValue = TalkToMartObjective;
        talkMart.FindPropertyRelative("description").stringValue = "Hear Mart out about leftover shop stock";
        talkMart.FindPropertyRelative("targetAmount").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(coffeeQuest);
    }

    private static void AuthorCoffeeProfile(
        QuestInteractionProfileSO profile,
        QuestDefinitionSO coffeeQuest,
        DialogueSequenceSO leftover,
        DialogueSequenceSO helpYourself,
        DialogueSequenceSO packing)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = coffeeQuest;
        serialized.FindProperty("questId").stringValue = "";
        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 2;
        ConfigureCoffeeRule(
            rules.GetArrayElementAtIndex(0),
            "after leftover — loading the truck",
            NarrativeQuestLifecycleMask.Active,
            TalkToMartObjective,
            QuestInteractionObjectiveComparison.Complete,
            helpYourself,
            "");
        ConfigureCoffeeRule(
            rules.GetArrayElementAtIndex(1),
            "after Reynard — still packing",
            NarrativeQuestLifecycleMask.Active,
            "talk_to_reynard",
            QuestInteractionObjectiveComparison.Complete,
            packing,
            "");

        serialized.FindProperty("inactiveDialogue").objectReferenceValue = packing;
        serialized.FindProperty("inactiveTriggerId").stringValue = "";
        SetNpcBehavior(serialized.FindProperty("inactiveNpcBehavior"));
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = packing;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = "";
        SetNpcBehavior(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.FindProperty("designerNotes").stringValue =
            "Packing until the town-exit intercept. That intercept is the only leftover speech and the only shop unlock. After it, Mart stays to load the truck until the player enters the store.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureCoffeeRule(
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
        ClearArray(rule, "counterPredicates");
        rule.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
        rule.FindPropertyRelative("useDialogueCamera").boolValue = true;
        rule.FindPropertyRelative("triggerId").stringValue = triggerId;
        SetNpcBehavior(rule.FindPropertyRelative("npcBehavior"));
    }

    private static void AuthorMartRouter(
        QuestInteractionRouterSO router,
        QuestInteractionProfileSO coffeeProfile,
        QuestInteractionProfileSO introProfile)
    {
        var serialized = new SerializedObject(router);
        SerializedProperty slots = serialized.FindProperty("slots");
        slots.arraySize = 2;
        ConfigureRouterSlot(slots.GetArrayElementAtIndex(0), "coffee leftover / packing", coffeeProfile,
            NarrativeQuestLifecycleMask.Available | NarrativeQuestLifecycleMask.Active);
        ConfigureRouterSlot(slots.GetArrayElementAtIndex(1), "intro packing", introProfile,
            NarrativeQuestLifecycleMask.Active);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(router);
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

    private static void AuthorDoorTrack(SequenceAsset sequence, DialogueSequenceSO packing)
    {
        var steps = new List<SequenceStep>
        {
            Label("door_loop", "coffee_door_label_loop"),
            WaitForTrigger(DoorTriggerId, "coffee_door_wait"),
            LockInput("coffee_door_lock", "Mart door closed"),
            PlayDialogue(packing, "coffee_door_packing", true),
            UnlockInput("coffee_door_unlock"),
            ResetTrigger(DoorTriggerId, "coffee_door_reset"),
            Jump("door_loop", "coffee_door_jump_loop")
        };
        WriteSequence(sequence, "coffee_loomville_gate_door_track", "", "door_loop", steps);
    }

    private static void AuthorStandingTrack(SequenceAsset sequence, QuestDefinitionSO coffeeQuest)
    {
        var steps = new List<SequenceStep>
        {
            WaitForTrigger(StandingTriggerId, "coffee_standing_wait"),
            Objective(coffeeQuest, "coffee_standing_objective"),
            WorldFlag("coffee_standing_flag", OpenFlag),
            WorldFlag("coffee_standing_pc_flag", ChainlingPcTerminalIds.UnlockFlagKey),
            Signal(LeftoverSignalId, "coffee_standing_signal")
        };
        WriteSequence(sequence, "coffee_loomville_gate_standing_track", "", "", steps);
    }

    private static void AuthorExitTrack(
        SequenceAsset sequence,
        DialogueSequenceSO leftover,
        QuestDefinitionSO coffeeQuest)
    {
        var steps = new List<SequenceStep>
        {
            WaitForQuestActive("coffee_exit_wait_reynard"),
            WaitForTrigger(ExitTriggerId, "coffee_exit_wait_trigger"),
            DisableObject(ExitTriggerName, "coffee_exit_consume_trigger"),
            LockInput("coffee_exit_lock", "Mart leftover intercept"),
            NpcStep<Step_NPC_ClaimControl>("coffee_exit_claim", so =>
            {
                so.FindProperty("npcId").stringValue = "mart";
                // Soft (Suppressed): Hard/Frozen forces Idle every frame and hides walk
                // while Follow still drives the NavMesh agent.
                so.FindProperty("claimType").enumValueIndex = (int)ClaimType.Soft;
                so.FindProperty("reason").stringValue = "Mart leftover intercept";
            }),
            NpcStep<Step_NPC_Follow>("coffee_exit_follow", so =>
            {
                so.FindProperty("npcId").stringValue = "mart";
                so.FindProperty("minFollowDistance").floatValue = 2f;
                so.FindProperty("maxFollowDistance").floatValue = 4f;
                so.FindProperty("completeWhenInFollowBand").boolValue = true;
                so.FindProperty("stopOnArrival").boolValue = true;
                so.FindProperty("targetId").stringValue = "";
                so.FindProperty("playerTag").stringValue = "Player";
                so.FindProperty("suspendAutonomyDuringStep").boolValue = true;
                so.FindProperty("suppressPauseForPlayerDuringStep").boolValue = true;
                so.FindProperty("fireAndForget").boolValue = false;
            }),
            NpcStep<Step_NPC_FaceTarget>("coffee_exit_face", so =>
            {
                so.FindProperty("npcId").stringValue = "mart";
                so.FindProperty("targetType").enumValueIndex = 0;
                so.FindProperty("targetId").stringValue = "";
            }),
            PlayDialogue(leftover, "coffee_exit_leftover", true),
            NpcStep<Step_NPC_StopLocomotion>("coffee_exit_stop", so =>
            {
                so.FindProperty("npcId").stringValue = "mart";
            }),
            NpcStep<Step_NPC_ReleaseControl>("coffee_exit_release", so =>
            {
                so.FindProperty("npcId").stringValue = "mart";
                so.FindProperty("releaseAllClaimsIfNoStoredHandle").boolValue = true;
            }),
            Objective(coffeeQuest, "coffee_exit_objective"),
            WorldFlag("coffee_exit_flag", OpenFlag),
            WorldFlag("coffee_exit_pc_flag", ChainlingPcTerminalIds.UnlockFlagKey),
            Signal(LeftoverSignalId, "coffee_exit_signal"),
            UnlockInput("coffee_exit_unlock")
        };
        WriteSequence(sequence, "coffee_loomville_gate_exit_track", "", "", steps);
    }

    private static void AuthorMainSequence(
        SequenceAsset sequence,
        SequenceAsset doorTrack,
        SequenceAsset exitTrack,
        SequenceAsset standingTrack)
    {
        var steps = new List<SequenceStep>
        {
            Label("coffee_gate_idle", "coffee_main_label_idle"),
            Checkpoint("coffee_gate_idle", "coffee_main_checkpoint"),
            UnlockInput("coffee_main_unlock"),
            WaitForScene("Loomville", "coffee_main_wait_scene"),
            RunTrack("door_closed", doorTrack, "coffee_main_run_door"),
            RunTrack("exit_intercept", exitTrack, "coffee_main_run_exit"),
            RunTrack("standing_leftover", standingTrack, "coffee_main_run_standing"),
        };

        SequenceAsset checkIn = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            SequenceFolder + "/coffee_reynard_checkin_sequence.asset");
        if (checkIn != null)
            steps.Add(RunTrack(CoffeeCourierHuntAuthoring.CheckInTrackId, checkIn, "coffee_main_run_checkin"));

        steps.Add(WaitForSignal(LeftoverSignalId, "coffee_main_wait_signal"));
        steps.Add(StopTrack("door_closed", "coffee_main_stop_door"));
        steps.Add(StopTrack("exit_intercept", "coffee_main_stop_exit"));
        steps.Add(StopTrack("standing_leftover", "coffee_main_stop_standing"));
        if (checkIn != null)
            steps.Add(StopTrack(CoffeeCourierHuntAuthoring.CheckInTrackId, "coffee_main_stop_checkin"));

        SequenceAsset courierHunt = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            SequenceFolder + "/coffee_courier_zeb_sequence.asset");
        if (courierHunt != null)
        {
            steps.Add(QueueFollowUp(courierHunt, "coffee_main_queue_courier"));
        }

        var serialized = WriteSequence(sequence, GateSequenceId, "coffee_gate_idle", "coffee_gate_idle", steps);
        serialized.FindProperty("autoStartWhenNoSequenceRunning").boolValue = true;
        serialized.FindProperty("autoStartQuestId").stringValue = CoffeeQuestId;
        serialized.FindProperty("autoStartLifecycleMask").intValue =
            (int)(NarrativeQuestLifecycleMask.Available | NarrativeQuestLifecycleMask.Active);
        serialized.FindProperty("autoStartSkipWorldFlagKey").stringValue = OpenFlag;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    public static void EnsureReynardCheckInTrack(SequenceAsset checkIn)
    {
        if (checkIn == null)
            throw new ArgumentNullException(nameof(checkIn));

        SequenceAsset gate = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            SequenceFolder + "/coffee_loomville_gate_sequence.asset");
        if (gate == null)
            throw new InvalidOperationException("coffee_loomville_gate_sequence is missing. Author Coffee Mart Gate first.");

        Step_RunTrack run = RunTrack(CoffeeCourierHuntAuthoring.CheckInTrackId, checkIn, "coffee_main_run_checkin");
        Step_StopTrack stop = StopTrack(CoffeeCourierHuntAuthoring.CheckInTrackId, "coffee_main_stop_checkin");
        var serialized = new SerializedObject(gate);
        SerializedProperty steps = serialized.FindProperty("steps");
        if (IndexOfReference(steps, run) < 0)
        {
            int standing = IndexOfTrack(steps, "standing_leftover", runTrack: true);
            InsertStep(steps, standing >= 0 ? standing + 1 : steps.arraySize, run);
        }

        if (IndexOfReference(steps, stop) < 0)
        {
            int standingStop = IndexOfTrack(steps, "standing_leftover", runTrack: false);
            int insertAt = standingStop >= 0
                ? standingStop + 1
                : IndexOfQueue(steps);
            InsertStep(steps, insertAt, stop);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gate);
    }

    private static int IndexOfReference(SerializedProperty steps, UnityEngine.Object target)
    {
        for (int i = 0; i < steps.arraySize; i++)
        {
            if (steps.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return i;
        }

        return -1;
    }

    private static int IndexOfTrack(SerializedProperty steps, string trackId, bool runTrack)
    {
        for (int i = 0; i < steps.arraySize; i++)
        {
            UnityEngine.Object step = steps.GetArrayElementAtIndex(i).objectReferenceValue;
            if (runTrack && step is Step_RunTrack run && run.TrackId == trackId)
                return i;
            if (!runTrack && step is Step_StopTrack stop && stop.TrackId == trackId)
                return i;
        }

        return -1;
    }

    private static int IndexOfQueue(SerializedProperty steps)
    {
        for (int i = 0; i < steps.arraySize; i++)
        {
            if (steps.GetArrayElementAtIndex(i).objectReferenceValue is Step_QueueSequence)
                return i;
        }

        return steps.arraySize;
    }

    private static void InsertStep(SerializedProperty steps, int index, SequenceStep step)
    {
        if (index >= steps.arraySize)
        {
            steps.arraySize++;
            steps.GetArrayElementAtIndex(steps.arraySize - 1).objectReferenceValue = step;
            return;
        }

        steps.InsertArrayElementAtIndex(index);
        steps.GetArrayElementAtIndex(index).objectReferenceValue = step;
    }

    private static void AuthorQueueStepOnIntro(SequenceAsset coffeeSequence)
    {
        SequenceAsset intro = AssetDatabase.LoadAssetAtPath<SequenceAsset>(IntroSequencePath);
        if (intro == null)
            throw new InvalidOperationException($"Missing intro sequence at '{IntroSequencePath}'.");

        Step_QueueSequence queue = EnsureAsset<Step_QueueSequence>(
            "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_Steps/intro_queue_coffee_gate.asset");
        var queueSerialized = new SerializedObject(queue);
        queueSerialized.FindProperty("stepName").stringValue = "intro_queue_coffee_gate";
        queueSerialized.FindProperty("sequence").objectReferenceValue = coffeeSequence;
        queueSerialized.FindProperty("acquireInputLock").boolValue = false;
        queueSerialized.ApplyModifiedPropertiesWithoutUndo();
        _ = queue.StepGuid;
        EditorUtility.SetDirty(queue);

        var introSerialized = new SerializedObject(intro);
        SerializedProperty steps = introSerialized.FindProperty("steps");
        int coffeeIndex = -1;
        int existingQueue = -1;
        for (int i = 0; i < steps.arraySize; i++)
        {
            var step = steps.GetArrayElementAtIndex(i).objectReferenceValue as SequenceStep;
            if (step != null && step.name == "intro_setquestavailable_coffee")
                coffeeIndex = i;
            if (step == queue)
                existingQueue = i;
        }

        if (existingQueue >= 0)
            return;
        if (coffeeIndex < 0)
            throw new InvalidOperationException("intro_setquestavailable_coffee was not found on chainbound_intro_sequence.");

        steps.InsertArrayElementAtIndex(coffeeIndex + 1);
        steps.GetArrayElementAtIndex(coffeeIndex + 1).objectReferenceValue = queue;
        introSerialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(intro);
    }

    private static void AuthorCatalog(SequenceAsset coffeeSequence)
    {
        SequenceCatalogSO catalog = AssetDatabase.LoadAssetAtPath<SequenceCatalogSO>(CatalogPath);
        if (catalog == null)
            throw new InvalidOperationException($"Missing sequence catalog at '{CatalogPath}'.");

        var serialized = new SerializedObject(catalog);
        SerializedProperty list = serialized.FindProperty("sequences");
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == coffeeSequence)
                return;
        }

        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = coffeeSequence;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void AuthorMartPrefabRouter(QuestInteractionRouterSO router, QuestInteractionProfileSO introProfile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MartPrefabPath);
        try
        {
            QuestProgressInteractionBridge bridge = root.GetComponent<QuestProgressInteractionBridge>();
            if (bridge == null)
                throw new InvalidOperationException("Mart prefab is missing QuestProgressInteractionBridge.");
            var serialized = new SerializedObject(bridge);
            serialized.FindProperty("defaultProfile").objectReferenceValue = introProfile;
            serialized.FindProperty("interactionRouter").objectReferenceValue = router;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, MartPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireLoomvilleScene(DialogueSequenceSO reynardNag, QuestDefinitionSO coffeeQuest)
    {
        Scene scene = SceneManager.GetSceneByPath(LoomvilleScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(LoomvilleScenePath, OpenSceneMode.Single);

        GameObject martBuilding = FindSceneObject(scene, "Loomville_Mart");
        if (martBuilding == null)
            throw new InvalidOperationException("Loomville_Mart is missing from the Loomville scene.");

        Transform doorTrigger = RenameIfPresent(martBuilding.transform, "Trigger", DoorTriggerObjectName);
        if (doorTrigger == null)
            doorTrigger = FindNamed(martBuilding.transform, DoorTriggerObjectName);
        Transform transition = FindNamed(martBuilding.transform, TransitionObjectName);
        if (doorTrigger == null || transition == null)
            throw new InvalidOperationException("Mart door Trigger or TransitionCollider is missing.");

        doorTrigger.gameObject.SetActive(false);
        transition.gameObject.SetActive(false);

        GameObject doorClosed = EnsureChild(martBuilding.transform, DoorClosedTriggerName);
        CopyWorldPose(transition, doorClosed.transform);
        CopyColliderShape(transition.gameObject, doorClosed);
        AuthorSequenceTrigger(doorClosed, DoorTriggerId, fireOnce: false, startArmed: false);

        GameObject doorClosedGate = EnsureSiblingHost(martBuilding.transform, "Mart_DoorClosedGate");
        AuthorPresence(doorClosedGate, OpenFlag, doorClosed, activeWhenTrue: false);

        GameObject doorAccess = EnsureSiblingHost(martBuilding.transform, "Mart_DoorAccessGate");
        AuthorPresence(doorAccess, OpenFlag, doorTrigger.gameObject, activeWhenTrue: true);

        GameObject transitionAccess = EnsureSiblingHost(martBuilding.transform, "Mart_TransitionAccessGate");
        AuthorPresence(transitionAccess, OpenFlag, transition.gameObject, activeWhenTrue: true);

        GameObject gatesRoot = FindSceneObject(scene, GatesRootName);
        if (gatesRoot == null)
        {
            gatesRoot = new GameObject(GatesRootName);
            SceneManager.MoveGameObjectToScene(gatesRoot, scene);
        }

        GameObject martNpc = FindSceneObject(scene, "Mart");
        Vector3 gatePosition = martNpc != null
            ? martNpc.transform.position + new Vector3(18f, 0f, 28f)
            : martBuilding.transform.position + new Vector3(18f, 0f, 28f);
        gatesRoot.transform.position = Vector3.zero;

        GameObject redirect = EnsureChild(gatesRoot.transform, ReynardRedirectName);
        redirect.transform.position = gatePosition;
        EnsureBox(redirect, new Vector3(0f, 4f, 0f), new Vector3(28f, 10f, 6f), isTrigger: true);
        AuthorReynardRedirect(redirect, reynardNag, coffeeQuest);

        GameObject exitTrigger = EnsureChild(gatesRoot.transform, ExitTriggerName);
        exitTrigger.transform.position = gatePosition;
        EnsureBox(exitTrigger, new Vector3(0f, 4f, 0f), new Vector3(28f, 10f, 8f), isTrigger: true);
        AuthorSequenceTrigger(exitTrigger, ExitTriggerId, fireOnce: true, startArmed: false);

        GameObject blocker = EnsureChild(gatesRoot.transform, ExitBlockerName);
        blocker.transform.position = gatePosition;
        EnsureBox(blocker, new Vector3(0f, 4f, 2.5f), new Vector3(28f, 10f, 1.5f), isTrigger: false);
        AuthorPresence(EnsureChild(gatesRoot.transform, "Mart_ExitBlockerGate"), OpenFlag, blocker, activeWhenTrue: false);

        SerializedObject redirectSerialized = new SerializedObject(redirect.GetComponent<NarrativeRedirectZone>());
        redirectSerialized.FindProperty("noReturnCollider").objectReferenceValue = blocker.GetComponent<Collider>();
        redirectSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save '{LoomvilleScenePath}'.");
    }

    private static void AuthorReynardRedirect(
        GameObject host,
        DialogueSequenceSO dialogue,
        QuestDefinitionSO coffeeQuest)
    {
        NarrativeRedirectZone zone = host.GetComponent<NarrativeRedirectZone>();
        if (zone == null)
            zone = host.AddComponent<NarrativeRedirectZone>();
        var serialized = new SerializedObject(zone);
        serialized.FindProperty("redirectDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.FindProperty("cooldownSeconds").floatValue = 4f;
        serialized.FindProperty("lockInputDuringDialogue").boolValue = true;
        serialized.FindProperty("startArmed").boolValue = true;
        SerializedProperty guard = serialized.FindProperty("questGuard");
        guard.FindPropertyRelative("requiredMatchMode").enumValueIndex = (int)NarrativeQuestGuardMatchMode.All;
        SerializedProperty required = guard.FindPropertyRelative("requiredClauses");
        required.arraySize = 1;
        SerializedProperty clause = required.GetArrayElementAtIndex(0);
        clause.FindPropertyRelative("questDefinition").objectReferenceValue = coffeeQuest;
        clause.FindPropertyRelative("questId").stringValue = "";
        clause.FindPropertyRelative("allowedLifecycleMask").intValue = (int)NarrativeQuestLifecycleMask.Available;
        guard.FindPropertyRelative("excludedClauses").arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AuthorSequenceTrigger(GameObject host, string triggerId, bool fireOnce, bool startArmed)
    {
        if (host.GetComponent<Collider>() == null)
        {
            BoxCollider box = host.AddComponent<BoxCollider>();
            box.isTrigger = true;
        }

        SequenceTrigger trigger = host.GetComponent<SequenceTrigger>();
        if (trigger == null)
            trigger = host.AddComponent<SequenceTrigger>();
        if (trigger == null)
            throw new InvalidOperationException($"Failed to add SequenceTrigger to '{host.name}'.");

        var serialized = new SerializedObject(trigger);
        serialized.FindProperty("triggerId").stringValue = triggerId;
        serialized.FindProperty("fireOnce").boolValue = fireOnce;
        serialized.FindProperty("startArmed").boolValue = startArmed;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.FindProperty("fireOnEnter").boolValue = true;
        serialized.FindProperty("fireOnExit").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AuthorPresence(GameObject host, string flag, GameObject target, bool activeWhenTrue)
    {
        WorldPresenceBinding binding = host.GetComponent<WorldPresenceBinding>();
        if (binding == null)
            binding = host.AddComponent<WorldPresenceBinding>();
        var serialized = new SerializedObject(binding);
        serialized.FindProperty("presenceId").stringValue = flag;
        serialized.FindProperty("target").objectReferenceValue = target;
        serialized.FindProperty("activeWhenTrue").boolValue = activeWhenTrue;
        serialized.FindProperty("lifetimeOverride").enumValueIndex = (int)WorldPresenceLifetime.Unspecified;
        serialized.ApplyModifiedPropertiesWithoutUndo();
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

    private static Step_Label Label(string labelName, string fileName)
    {
        return ConfigureStep<Step_Label>(fileName, so =>
        {
            so.FindProperty("labelName").stringValue = labelName;
        });
    }

    private static Step_JumpToLabel Jump(string label, string fileName)
    {
        return ConfigureStep<Step_JumpToLabel>(fileName, so =>
        {
            so.FindProperty("targetLabel").stringValue = label;
        });
    }

    private static Step_WaitForTrigger WaitForTrigger(string triggerId, string fileName)
    {
        return ConfigureStep<Step_WaitForTrigger>(fileName, so =>
        {
            so.FindProperty("triggerId").stringValue = triggerId;
            so.FindProperty("armTrigger").boolValue = true;
        });
    }

    private static Step_EnableGameObject DisableObject(string objectName, string fileName)
    {
        return ConfigureStep<Step_EnableGameObject>(fileName, so =>
        {
            so.FindProperty("gameObjectName").stringValue = objectName;
            so.FindProperty("enable").boolValue = false;
            so.FindProperty("includeInactive").boolValue = true;
            so.FindProperty("persistAcrossSceneLoads").boolValue = true;
            so.FindProperty("persistAsProfileWorldFlag").boolValue = true;
        });
    }

    private static Step_LockInput LockInput(string fileName, string reason)
    {
        return ConfigureStep<Step_LockInput>(fileName, so =>
        {
            so.FindProperty("channel").enumValueIndex = 0;
            so.FindProperty("reason").stringValue = reason;
        });
    }

    private static Step_UnlockInput UnlockInput(string fileName)
    {
        return ConfigureStep<Step_UnlockInput>(fileName, _ => { });
    }

    private static Step_PlayDialogue PlayDialogue(DialogueSequenceSO dialogue, string fileName, bool camera)
    {
        return ConfigureStep<Step_PlayDialogue>(fileName, so =>
        {
            so.FindProperty("dialogueSequence").objectReferenceValue = dialogue;
            so.FindProperty("lockInputDuringDialogue").boolValue = true;
            so.FindProperty("useSpeakerCameraBridge").boolValue = camera;
            so.FindProperty("persistSpeakerCameraAfterDialogue").boolValue = false;
        });
    }

    private static Step_ResetTrigger ResetTrigger(string triggerId, string fileName)
    {
        return ConfigureStep<Step_ResetTrigger>(fileName, so =>
        {
            so.FindProperty("triggerId").stringValue = triggerId;
            so.FindProperty("alsoUnregister").boolValue = false;
        });
    }

    private static Step_SetQuestObjectiveProgress Objective(QuestDefinitionSO quest, string fileName)
    {
        return ConfigureStep<Step_SetQuestObjectiveProgress>(fileName, so =>
        {
            so.FindProperty("questId").stringValue = quest.QuestId;
            so.FindProperty("objectiveId").stringValue = TalkToMartObjective;
            so.FindProperty("completeObjective").boolValue = true;
            so.FindProperty("autoCompleteQuest").boolValue = false;
        });
    }

    private static Step_SetProfileWorldFlag WorldFlag(string fileName, string flagKey)
    {
        return ConfigureStep<Step_SetProfileWorldFlag>(fileName, so =>
        {
            so.FindProperty("flagKey").stringValue = flagKey;
            so.FindProperty("flagValue").boolValue = true;
            so.FindProperty("lifetimeOverride").enumValueIndex = (int)WorldPresenceLifetime.Unspecified;
        });
    }

    private static Step_Signal Signal(string signalId, string fileName)
    {
        return ConfigureStep<Step_Signal>(fileName, so =>
        {
            so.FindProperty("signalId").stringValue = signalId;
            so.FindProperty("payload").stringValue = "";
            so.FindProperty("sourceTrackId").stringValue = "";
        });
    }

    private static Step_WaitForQuestState WaitForQuestActive(string fileName)
    {
        return ConfigureStep<Step_WaitForQuestState>(fileName, so =>
        {
            so.FindProperty("questId").stringValue = CoffeeQuestId;
            so.FindProperty("targetState").enumValueIndex =
                (int)Step_WaitForQuestState.QuestTargetState.Active;
            so.FindProperty("timeoutSeconds").floatValue = 0f;
            so.FindProperty("onTimeoutLabel").stringValue = "";
        });
    }

    private static Step_SetCheckpoint Checkpoint(string label, string fileName)
    {
        return ConfigureStep<Step_SetCheckpoint>(fileName, so =>
        {
            so.FindProperty("checkpointId").stringValue = label;
            so.FindProperty("checkpointLabel").stringValue = label;
            so.FindProperty("saveToState").boolValue = true;
            so.FindProperty("resumeSceneId").stringValue = "Loomville";
            so.FindProperty("resumeSpawnId").stringValue = "";
        });
    }

    private static Step_WaitForSceneActive WaitForScene(string sceneName, string fileName)
    {
        return ConfigureStep<Step_WaitForSceneActive>(fileName, so =>
        {
            so.FindProperty("sceneName").stringValue = sceneName;
            so.FindProperty("timeoutSeconds").floatValue = 0f;
        });
    }

    private static Step_RunTrack RunTrack(string trackId, SequenceAsset child, string fileName)
    {
        return ConfigureStep<Step_RunTrack>(fileName, so =>
        {
            so.FindProperty("trackId").stringValue = trackId;
            so.FindProperty("trackType").enumValueIndex = (int)SequenceTrackType.World;
            so.FindProperty("sequence").objectReferenceValue = child;
            so.FindProperty("contextMode").enumValueIndex = (int)TrackContextMode.Child;
            so.FindProperty("cancelPolicy").enumValueIndex = (int)TrackCancelPolicy.StopOnParentEnd;
            so.FindProperty("blockingMode").enumValueIndex = (int)TrackBlockingMode.NonBlocking;
        });
    }

    private static Step_WaitForTrack WaitForSignal(string signalId, string fileName)
    {
        return ConfigureStep<Step_WaitForTrack>(fileName, so =>
        {
            so.FindProperty("trackId").stringValue = "";
            so.FindProperty("completionMode").enumValueIndex = (int)TrackWaitCompletionMode.Signaled;
            so.FindProperty("signalId").stringValue = signalId;
            so.FindProperty("timeoutSeconds").floatValue = 0f;
        });
    }

    private static Step_StopTrack StopTrack(string trackId, string fileName)
    {
        return ConfigureStep<Step_StopTrack>(fileName, so =>
        {
            so.FindProperty("trackId").stringValue = trackId;
            so.FindProperty("stopMode").enumValueIndex = (int)TrackStopMode.Immediate;
        });
    }

    private static Step_QueueSequence QueueFollowUp(SequenceAsset followUp, string fileName)
    {
        return ConfigureStep<Step_QueueSequence>(fileName, so =>
        {
            so.FindProperty("sequence").objectReferenceValue = followUp;
            so.FindProperty("acquireInputLock").boolValue = false;
        });
    }

    private static T NpcStep<T>(string fileName, Action<SerializedObject> configure) where T : SequenceStep
    {
        return ConfigureStep<T>(fileName, configure);
    }

    private static T ConfigureStep<T>(string fileName, Action<SerializedObject> configure) where T : SequenceStep
    {
        string path = $"{StepsFolder}/{fileName}.asset";
        T step = EnsureAsset<T>(path);
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

    private static GameObject EnsureChild(Transform parent, string objectName)
    {
        Transform existing = parent.Find(objectName);
        if (existing != null)
            return existing.gameObject;

        var child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static GameObject EnsureSiblingHost(Transform sibling, string objectName)
    {
        Transform parent = sibling.parent;
        if (parent != null)
        {
            Transform existing = parent.Find(objectName);
            if (existing != null)
                return existing.gameObject;
        }

        GameObject host = new GameObject(objectName);
        host.transform.SetParent(parent, false);
        return host;
    }

    private static Transform RenameIfPresent(Transform root, string fromName, string toName)
    {
        Transform named = FindNamed(root, toName);
        if (named != null)
            return named;

        Transform old = FindNamed(root, fromName);
        if (old != null)
            old.name = toName;
        return old;
    }

    private static Transform FindNamed(Transform root, string objectName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
                return child;
        }
        return null;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform match = FindNamed(root.transform, objectName);
            if (match != null)
                return match.gameObject;
        }
        return null;
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

    private static void CopyColliderShape(GameObject source, GameObject dest)
    {
        BoxCollider sourceBox = source.GetComponent<BoxCollider>();
        if (sourceBox == null)
            return;
        BoxCollider destBox = dest.GetComponent<BoxCollider>();
        if (destBox == null)
            destBox = dest.AddComponent<BoxCollider>();
        destBox.isTrigger = true;
        destBox.center = sourceBox.center;
        destBox.size = sourceBox.size;
    }

    private static void CopyWorldPose(Transform source, Transform dest)
    {
        dest.position = source.position;
        dest.rotation = source.rotation;
        dest.localScale = source.localScale;
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

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
            return;

        int slash = normalized.LastIndexOf('/');
        string parent = normalized.Substring(0, slash);
        string name = normalized.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
