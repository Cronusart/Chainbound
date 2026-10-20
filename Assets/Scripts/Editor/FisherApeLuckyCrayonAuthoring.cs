#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors the Lucky Crayon conversation as dialogue, interaction rules, and a child sequence.
/// Ambient NPC authoring calls this last so a Fisher-Ape prefab rebuild cannot wipe the rules.
/// </summary>
public static class FisherApeLuckyCrayonAuthoring
{
    public const string ItemId = "lucky_crayon";
    public const string RewardId = "fisher_ape_lucky_crayon";
    public const string TriggerId = "fisher_ape_lucky_crayon";
    public const string TrackId = "fisher_ape_lucky_crayon";
    public const string FlagKey = "world.fisher_ape.crayon_returned";
    public const string SpawnId = "world.pickup.academy_interior.lucky_crayon";
    public const string QuestId = "ambient_npc_fisher_ape";

    private const string ContentFolder = "Assets/Scripts/NPC/Interaction/Ambient/FisherApe";
    private const string HintPath = ContentFolder + "/Dial_FisherApe_StreamWarning.asset";
    private const string ThanksPath = ContentFolder + "/Dial_FisherApe_CrayonThanks.asset";
    private const string FavorPath = ContentFolder + "/Dial_FisherApe_CrayonFavor.asset";
    private const string EpiloguePath = ContentFolder + "/Dial_FisherApe_CrayonEpilogue.asset";
    private const string FailPath = ContentFolder + "/Dial_FisherApe_CrayonHold.asset";
    private const string ProfilePath = ContentFolder + "/fisher_ape_chatter_profile.asset";
    private const string SequenceFolder = "Assets/Scripts/Narrative/Quests/fisher_ape_lucky_crayon/Sequence";
    private const string StepsFolder = SequenceFolder + "/fisher_ape_lucky_crayon_Steps";
    private const string SequencePath = SequenceFolder + "/fisher_ape_lucky_crayon_sequence.asset";
    private const string PrefabPath = "Assets/Prefabs/NPC/Fisher-Ape.prefab";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";
    private const string PortraitPath = "Assets/Sprites/NPCs/Fisherman_Ape/fisherape_idle_down1.png";

    private static readonly string[] HintLines =
    {
        "The stream up ahead is rough today. Be careful.",
        "A lot of crazed Chainlings are biting today.",
        "If only I had my lucky crayon. Then I could catch a strong one.",
    };

    private static readonly string[] ThanksLines =
    {
        "My lucky crayon. You found it.",
        "Here. A Signal Orb, and 15 RAM for the trouble.",
    };

    private static readonly string[] FavorLines =
    {
        "Favor can greatly increase how often rare Chainlings show up. Keep an eye out for something that will raise yours, too.",
    };

    private static readonly string[] EpilogueLines =
    {
        "I'm still looking for that rare Chainling. Good hunting. I hope the stream is kinder to you.",
    };

    private static readonly string[] FailLines =
    {
        "Not now. Hold onto that crayon until I can take it.",
    };

    [MenuItem("Tools/Chainbound/Narrative/Author Fisher-Ape Lucky Crayon")]
    public static void ApplyFromMenu() => Apply();

    public static string Apply()
    {
        EnsureFolder(ContentFolder);
        EnsureFolder(StepsFolder);

        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        Sprite portrait = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);

        DialogueSequenceSO hint = EnsureAsset<DialogueSequenceSO>(HintPath);
        DialogueSequenceSO thanks = EnsureAsset<DialogueSequenceSO>(ThanksPath);
        DialogueSequenceSO favor = EnsureAsset<DialogueSequenceSO>(FavorPath);
        DialogueSequenceSO epilogue = EnsureAsset<DialogueSequenceSO>(EpiloguePath);
        DialogueSequenceSO fail = EnsureAsset<DialogueSequenceSO>(FailPath);
        AuthorDialogue(hint, chatter, portrait, HintLines);
        AuthorDialogue(thanks, chatter, portrait, ThanksLines);
        AuthorDialogue(favor, chatter, portrait, FavorLines);
        AuthorDialogue(epilogue, chatter, portrait, EpilogueLines);
        AuthorDialogue(fail, chatter, portrait, FailLines);

        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(ProfilePath);
        AuthorProfile(profile, hint, epilogue);

        SequenceAsset sequence = EnsureAsset<SequenceAsset>(SequencePath);
        AuthorSequence(sequence, thanks, favor, fail);

        WirePrefab(sequence);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        const string result = "Authored Fisher-Ape Lucky Crayon dialogue, bag rules, and reward sequence.";
        Debug.Log("[FisherApeLuckyCrayonAuthoring] " + result);
        return result;
    }

    private static void AuthorProfile(
        QuestInteractionProfileSO profile,
        DialogueSequenceSO hint,
        DialogueSequenceSO epilogue)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = null;
        serialized.FindProperty("questId").stringValue = QuestId;
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = hint;
        serialized.FindProperty("inactiveTriggerId").stringValue = "";
        SetNpcBehavior(serialized.FindProperty("inactiveNpcBehavior"));
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = hint;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = "";
        SetNpcBehavior(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        serialized.FindProperty("designerNotes").stringValue =
            "Hint without the crayon. Reward when the bag has lucky_crayon and the return flag is false. Epilogue after world.fisher_ape.crayon_returned.";

        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 2;
        WriteRule(
            rules.GetArrayElementAtIndex(0),
            "crayon returned",
            epilogue,
            "",
            FlagKey,
            QuestInteractionMemoryFlagComparison.IsTrue,
            "");
        WriteRule(
            rules.GetArrayElementAtIndex(1),
            "has lucky crayon",
            null,
            TriggerId,
            FlagKey,
            QuestInteractionMemoryFlagComparison.IsFalse,
            ItemId);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void WriteRule(
        SerializedProperty rule,
        string label,
        DialogueSequenceSO dialogue,
        string triggerId,
        string flagKey,
        QuestInteractionMemoryFlagComparison flagComparison,
        string bagItemId)
    {
        rule.FindPropertyRelative("label").stringValue = label;
        rule.FindPropertyRelative("lifecycleScope").enumValueIndex = (int)QuestInteractionRuleLifecycleScope.UseLifecycleMask;
        rule.FindPropertyRelative("lifecycleMask").intValue = (int)NarrativeQuestLifecycleMask.Unknown;
        rule.FindPropertyRelative("objectiveId").stringValue = "";
        rule.FindPropertyRelative("objectiveComparison").enumValueIndex = (int)QuestInteractionObjectiveComparison.None;
        rule.FindPropertyRelative("objectiveValue").intValue = 0;
        rule.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
        rule.FindPropertyRelative("useDialogueCamera").boolValue = false;
        rule.FindPropertyRelative("triggerId").stringValue = triggerId;
        ClearArray(rule, "sequenceProgressPredicates");
        ClearArray(rule, "resolutionPredicates");
        ClearArray(rule, "flagPredicates");
        ClearArray(rule, "counterPredicates");
        SetNpcBehavior(rule.FindPropertyRelative("npcBehavior"));

        SerializedProperty flags = rule.FindPropertyRelative("worldFlagPredicates");
        flags.arraySize = 1;
        SerializedProperty flag = flags.GetArrayElementAtIndex(0);
        flag.FindPropertyRelative("key").stringValue = flagKey;
        flag.FindPropertyRelative("comparison").enumValueIndex = (int)flagComparison;

        SerializedProperty bag = rule.FindPropertyRelative("bagItemPredicates");
        if (string.IsNullOrEmpty(bagItemId))
        {
            bag.arraySize = 0;
        }
        else
        {
            bag.arraySize = 1;
            SerializedProperty item = bag.GetArrayElementAtIndex(0);
            item.FindPropertyRelative("itemId").stringValue = bagItemId;
            item.FindPropertyRelative("minimumQuantity").intValue = 1;
        }
    }

    private static void AuthorSequence(
        SequenceAsset sequence,
        DialogueSequenceSO thanks,
        DialogueSequenceSO favor,
        DialogueSequenceSO fail)
    {
        SequenceStep[] steps =
        {
            ConfigureStep<Step_HoldNpcInteraction>("01_hold_retain", so =>
                so.FindProperty("mode").enumValueIndex = (int)Step_HoldNpcInteraction.HoldMode.Retain),
            ConfigureStep<Step_LockInput>("02_lock_input", so =>
            {
                so.FindProperty("channel").enumValueIndex = (int)InputLockChannel.All;
                so.FindProperty("reason").stringValue = "Fisher-Ape lucky crayon";
            }),
            PlayDialogue(thanks, "03_play_thanks"),
            ConfigureStep<Step_ApplyQuestReward>("04_apply_reward", so =>
            {
                so.FindProperty("rewardId").stringValue = RewardId;
                so.FindProperty("failurePolicy").enumValueIndex = (int)StepFailurePolicy.GotoLabel;
                so.FindProperty("onFailLabel").stringValue = "reward_failed";
            }),
            ConfigureStep<Step_SetProfileWorldFlag>("05_set_flag", so =>
            {
                so.FindProperty("flagKey").stringValue = FlagKey;
                so.FindProperty("flagValue").boolValue = true;
            }),
            ShowItem("signal_orb", 1, "06_show_signal_orb"),
            ShowItem("ram", 15, "07_show_ram"),
            PlayDialogue(favor, "08_play_favor"),
            ConfigureStep<Step_JumpToLabel>("09_jump_cleanup", so =>
                so.FindProperty("targetLabel").stringValue = "cleanup"),
            ConfigureStep<Step_Label>("10_label_reward_failed", so =>
                so.FindProperty("labelName").stringValue = "reward_failed"),
            PlayDialogue(fail, "11_play_fail"),
            ConfigureStep<Step_Label>("12_label_cleanup", so =>
                so.FindProperty("labelName").stringValue = "cleanup"),
            ConfigureStep<Step_HoldNpcInteraction>("13_hold_release", so =>
                so.FindProperty("mode").enumValueIndex = (int)Step_HoldNpcInteraction.HoldMode.Release),
            ConfigureStep<Step_UnlockInput>("14_unlock_input", _ => { }),
        };

        var serialized = new SerializedObject(sequence);
        serialized.FindProperty("sequenceId").stringValue = TrackId;
        serialized.FindProperty("startCheckpointId").stringValue = "";
        serialized.FindProperty("safeResumeLabel").stringValue = "";
        serialized.FindProperty("contentVersion").intValue = 1;
        serialized.FindProperty("autoStartWhenNoSequenceRunning").boolValue = false;
        serialized.FindProperty("autoStartQuestId").stringValue = "";
        serialized.FindProperty("autoStartSkipWorldFlagKey").stringValue = "";
        serialized.FindProperty("autoStartRequiredWorldFlagKey").stringValue = "";
        SerializedProperty list = serialized.FindProperty("steps");
        list.arraySize = steps.Length;
        for (int i = 0; i < steps.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = steps[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
    }

    private static void WirePrefab(SequenceAsset sequence)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogWarning($"[FisherApeLuckyCrayonAuthoring] Fisher-Ape prefab is missing at '{PrefabPath}'.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            SequenceOnTrigger listener = root.GetComponent<SequenceOnTrigger>();
            if (listener == null)
                listener = root.AddComponent<SequenceOnTrigger>();

            var serialized = new SerializedObject(listener);
            serialized.FindProperty("triggerId").stringValue = TriggerId;
            serialized.FindProperty("sequence").objectReferenceValue = sequence;
            serialized.FindProperty("trackId").stringValue = TrackId;
            serialized.FindProperty("trackType").enumValueIndex = (int)SequenceTrackType.NPC;
            serialized.FindProperty("contextMode").enumValueIndex = (int)TrackContextMode.Child;
            serialized.FindProperty("cancelPolicy").enumValueIndex = (int)TrackCancelPolicy.ManualOnly;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Step_PlayDialogue PlayDialogue(DialogueSequenceSO dialogue, string fileName) =>
        ConfigureStep<Step_PlayDialogue>(fileName, so =>
        {
            so.FindProperty("dialogueSequence").objectReferenceValue = dialogue;
            so.FindProperty("lockInputDuringDialogue").boolValue = true;
            so.FindProperty("useSpeakerCameraBridge").boolValue = false;
            so.FindProperty("persistSpeakerCameraAfterDialogue").boolValue = false;
        });

    private static Step_ShowItemObtain ShowItem(string itemId, int quantity, string fileName) =>
        ConfigureStep<Step_ShowItemObtain>(fileName, so =>
        {
            so.FindProperty("itemId").stringValue = itemId;
            so.FindProperty("quantity").intValue = quantity;
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
        Sprite portrait,
        string[] lines)
    {
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = "fisher_ape";
        serialized.FindProperty("playerFacingPolicy").enumValueIndex = (int)DialoguePlayerFacingPolicy.FaceSpeakerOnStart;
        SerializedProperty lineList = serialized.FindProperty("lines");
        lineList.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++)
        {
            SerializedProperty line = lineList.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerName").stringValue = "Fisher-Ape";
            line.FindPropertyRelative("text").stringValue = lines[i];
            line.FindPropertyRelative("typeSpeed").floatValue = 48f;
            line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
            line.FindPropertyRelative("portraitSprite").objectReferenceValue = portrait;
            line.FindPropertyRelative("portraitTint").colorValue = Color.white;
            SerializedProperty camera = line.FindPropertyRelative("camera");
            if (camera != null)
            {
                camera.FindPropertyRelative("shotPreset").objectReferenceValue = null;
                camera.FindPropertyRelative("targetSource").enumValueIndex = (int)DialogueCameraTargetSource.None;
                camera.FindPropertyRelative("npcId").stringValue = "";
                camera.FindPropertyRelative("targetTag").stringValue = "";
            }
        }

        serialized.FindProperty("autoAdvance").boolValue = false;
        serialized.FindProperty("autoAdvanceDelay").floatValue = 2f;
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
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, normalized.Substring(slash + 1));
    }
}
#endif
