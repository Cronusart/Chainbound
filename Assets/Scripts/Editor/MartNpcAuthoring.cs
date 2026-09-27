#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent authoring pass for Mart. Runtime behavior remains data-driven by
/// the NPC prefab, dialogue/profile assets, and profile world-flag rules.
/// </summary>
public static class MartNpcAuthoring
{
    private const string LoomvilleScenePath = "Assets/Scenes/Loomville.unity";
    private const string BertPrefabPath = "Assets/Prefabs/NPC/Bert.prefab";
    private const string MartPrefabPath = "Assets/Prefabs/NPC/Mart.prefab";
    private const string ContentFolder = "Assets/Scripts/NPC/Interaction/Loomville/Mart";
    private const string ShortDialoguePath = ContentFolder + "/Dial_Mart_Packing.asset";
    private const string CompletedDialoguePath = ContentFolder + "/Dial_Mart_OrbCreator.asset";
    private const string ProfilePath = ContentFolder + "/mart_chainbound_intro_profile.asset";
    private const string IntroQuestPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/Chainbound_Intro.asset";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";
    private const string SceneRulesFolder = "Assets/Resources/Narrative";
    private const string SceneRulesPath = SceneRulesFolder + "/ProfileWorldFlagSceneRules.asset";
    private const string MartNpcId = "mart";
    private const string DepartureFlag = "mart_departed_loomville";
    private const string ShopSceneId = "loomville_mart";
    private const string InteractionBehaviorPath = "Assets/Scripts/NPC/Interaction/NPCInteractionBehavior.asset";

    [MenuItem("Tools/Chainbound/Narrative/Author Loomville Mart NPC")]
    public static void ApplyFromMenu()
    {
        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static void Apply()
    {
        EnsureFolder(ContentFolder);
        EnsureFolder(SceneRulesFolder);

        QuestDefinitionSO introQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(IntroQuestPath);
        if (introQuest == null || !string.Equals(introQuest.QuestId, "chainbound_intro", StringComparison.Ordinal))
            throw new InvalidOperationException($"Missing chainbound_intro quest at '{IntroQuestPath}'.");

        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        CameraShotPresetSO shot = AssetDatabase.LoadAssetAtPath<CameraShotPresetSO>(
            AssetDatabase.GUIDToAssetPath("e2e99b5b538b6914d99ce0ebfaca93f7"));

        DialogueSequenceSO shortDialogue = EnsureAsset<DialogueSequenceSO>(ShortDialoguePath);
        AuthorDialogue(shortDialogue, chatter, shot,
            "Can't talk long. I'm packing the shop, and every second counts.",
            "Mind the boxes, yeah?");

        DialogueSequenceSO completedDialogue = EnsureAsset<DialogueSequenceSO>(CompletedDialoguePath);
        AuthorDialogue(completedDialogue, chatter, shot, CoffeeMartGateAuthoring.LeftoverDialogueLines);

        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(ProfilePath);
        AuthorInteractionProfile(profile, introQuest, shortDialogue);
        AuthorSceneEntryRule();

        AssetDatabase.SaveAssets();
        AuthorMartPrefab(profile);
        AssetDatabase.SaveAssets();
        WireLoomvilleScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[MartNpcAuthoring] Authored Mart with Bert directional visuals/blinks, " +
            "chainbound_intro lifecycle dialogue, and durable loomville_mart departure state.");
    }

    private static void AuthorDialogue(
        DialogueSequenceSO dialogue,
        SoundEventSO chatter,
        CameraShotPresetSO shot,
        params string[] text)
    {
        var serialized = new SerializedObject(dialogue);
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerName").stringValue = "Mart";
            line.FindPropertyRelative("text").stringValue = text[i];
            line.FindPropertyRelative("typeSpeed").floatValue = 55f;
            line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
            line.FindPropertyRelative("portraitSprite").objectReferenceValue = null;
            line.FindPropertyRelative("portraitTint").colorValue = Color.white;

            SerializedProperty camera = line.FindPropertyRelative("camera");
            camera.FindPropertyRelative("shotPreset").objectReferenceValue = shot;
            camera.FindPropertyRelative("targetSource").enumValueIndex =
                (int)DialogueCameraTargetSource.NpcId;
            camera.FindPropertyRelative("npcId").stringValue = MartNpcId;
            camera.FindPropertyRelative("targetTag").stringValue = "";
        }

        serialized.FindProperty("autoAdvance").boolValue = false;
        serialized.FindProperty("autoAdvanceDelay").floatValue = 2f;
        serialized.FindProperty("allowSkip").boolValue = true;
        serialized.FindProperty("skipToEndOfLine").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialogue);
    }

    private static void AuthorInteractionProfile(
        QuestInteractionProfileSO profile,
        QuestDefinitionSO introQuest,
        DialogueSequenceSO shortDialogue)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = introQuest;
        serialized.FindProperty("questId").stringValue = "";

        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 1;
        ConfigureRule(
            rules.GetArrayElementAtIndex(0),
            "intro active — packing",
            NarrativeQuestLifecycleMask.Active,
            shortDialogue);

        serialized.FindProperty("inactiveDialogue").objectReferenceValue = shortDialogue;
        serialized.FindProperty("inactiveTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("inactiveNpcBehavior"), QuestInteractionNpcBehaviorMode.UseNpcDefault);
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = shortDialogue;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("activeFallbackNpcBehavior"), QuestInteractionNpcBehaviorMode.UseNpcDefault);
        serialized.FindProperty("designerNotes").stringValue =
            "Mart is brief while chainbound_intro is active. Leftover-shop permission lives on the coffee quest profile.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void ConfigureRule(
        SerializedProperty rule,
        string label,
        NarrativeQuestLifecycleMask lifecycle,
        DialogueSequenceSO dialogue)
    {
        rule.FindPropertyRelative("label").stringValue = label;
        rule.FindPropertyRelative("lifecycleScope").enumValueIndex =
            (int)QuestInteractionRuleLifecycleScope.UseLifecycleMask;
        rule.FindPropertyRelative("lifecycleMask").intValue = (int)lifecycle;
        rule.FindPropertyRelative("objectiveId").stringValue = "";
        rule.FindPropertyRelative("objectiveComparison").enumValueIndex =
            (int)QuestInteractionObjectiveComparison.None;
        rule.FindPropertyRelative("objectiveValue").intValue = 0;
        ClearArray(rule, "sequenceProgressPredicates");
        ClearArray(rule, "resolutionPredicates");
        ClearArray(rule, "flagPredicates");
        ClearArray(rule, "counterPredicates");
        rule.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
        rule.FindPropertyRelative("useDialogueCamera").boolValue = true;
        rule.FindPropertyRelative("triggerId").stringValue = "";
        SetNpcBehaviorMode(rule.FindPropertyRelative("npcBehavior"), QuestInteractionNpcBehaviorMode.UseNpcDefault);
    }

    private static void SetNpcBehaviorMode(SerializedProperty binding, QuestInteractionNpcBehaviorMode mode)
    {
        if (binding == null)
            return;

        binding.FindPropertyRelative("mode").enumValueIndex = (int)mode;
        binding.FindPropertyRelative("behaviorOverride").objectReferenceValue = null;
    }

    private static void ClearArray(SerializedProperty parent, string relativeName)
    {
        SerializedProperty property = parent.FindPropertyRelative(relativeName);
        if (property != null && property.isArray)
            property.arraySize = 0;
    }

    private static void AuthorSceneEntryRule()
    {
        ProfileWorldFlagSceneRulesSO rules = EnsureAsset<ProfileWorldFlagSceneRulesSO>(SceneRulesPath);
        var serialized = new SerializedObject(rules);
        SerializedProperty list = serialized.FindProperty("rules");

        SerializedProperty match = null;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty candidate = list.GetArrayElementAtIndex(i);
            string sceneId = candidate.FindPropertyRelative("sceneId").stringValue;
            string flagKey = candidate.FindPropertyRelative("flagKey").stringValue;
            if (string.Equals(sceneId, ShopSceneId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(flagKey, DepartureFlag, StringComparison.Ordinal))
            {
                match = candidate;
                break;
            }
        }

        if (match == null)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            match = list.GetArrayElementAtIndex(list.arraySize - 1);
        }

        match.FindPropertyRelative("sceneId").stringValue = ShopSceneId;
        match.FindPropertyRelative("flagKey").stringValue = DepartureFlag;
        match.FindPropertyRelative("flagValue").boolValue = true;
        match.FindPropertyRelative("requiredFlagKey").stringValue = CoffeeMartGateAuthoring.OpenFlag;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rules);
    }

    private static void AuthorMartPrefab(QuestInteractionProfileSO profile)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BertPrefabPath) == null)
            throw new InvalidOperationException($"Missing Bert visual prefab at '{BertPrefabPath}'.");

        if (AssetDatabase.LoadAssetAtPath<GameObject>(MartPrefabPath) == null &&
            !AssetDatabase.CopyAsset(BertPrefabPath, MartPrefabPath))
        {
            throw new InvalidOperationException($"Could not create '{MartPrefabPath}' from Bert.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(MartPrefabPath);
        try
        {
            root.name = "Mart";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            NPCController controller = root.GetComponent<NPCController>();
            if (controller == null)
                throw new InvalidOperationException("Mart prefab is missing NPCController.");
            var controllerSerialized = new SerializedObject(controller);
            NPCInteractionBehaviorSO interactionBehavior =
                AssetDatabase.LoadAssetAtPath<NPCInteractionBehaviorSO>(InteractionBehaviorPath);
            if (interactionBehavior == null)
                throw new InvalidOperationException($"Missing NPC interaction behavior at {InteractionBehaviorPath}.");

            controllerSerialized.FindProperty("npcId").stringValue = MartNpcId;
            controllerSerialized.FindProperty("routeSchedule").objectReferenceValue = null;
            controllerSerialized.FindProperty("autonomyEnabled").boolValue = true;
            controllerSerialized.FindProperty("interactionBehavior").objectReferenceValue = interactionBehavior;
            controllerSerialized.FindProperty("enableDebugLogs").boolValue = false;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            MeshRendererNPCVisual visual = root.GetComponentInChildren<MeshRendererNPCVisual>(true);
            if (visual == null)
                throw new InvalidOperationException("Mart prefab is missing MeshRendererNPCVisual.");
            visual.transform.localPosition = Vector3.zero;
            var visualSerialized = new SerializedObject(visual);
            visualSerialized.FindProperty("spriteSize").vector2Value = new Vector2(5f, 5f);
            visualSerialized.FindProperty("preserveAspectRatio").boolValue = true;
            visualSerialized.FindProperty("referenceHeight").floatValue = 5f;
            visualSerialized.FindProperty("yPositionOffset").floatValue = 2f;
            visualSerialized.FindProperty("anchorBottomCenter").boolValue = false;
            visualSerialized.FindProperty("enableDebugLogs").boolValue = false;
            visualSerialized.ApplyModifiedPropertiesWithoutUndo();
            ValidateVisualSequences(visualSerialized);

            QuestProgressInteractionBridge bridge = root.GetComponent<QuestProgressInteractionBridge>();
            if (bridge == null)
                bridge = root.AddComponent<QuestProgressInteractionBridge>();
            var bridgeSerialized = new SerializedObject(bridge);
            bridgeSerialized.FindProperty("defaultProfile").objectReferenceValue = profile;
            QuestInteractionRouterSO router = AssetDatabase.LoadAssetAtPath<QuestInteractionRouterSO>(
                "Assets/Scripts/NPC/Interaction/Loomville/Mart/Mart_InteractionRouter.asset");
            if (router != null)
                bridgeSerialized.FindProperty("interactionRouter").objectReferenceValue = router;
            bridgeSerialized.FindProperty("enableDebugLogs").boolValue = false;
            bridgeSerialized.ApplyModifiedPropertiesWithoutUndo();

            AuthorInteractable(root, bridge);
            PrefabUtility.SaveAsPrefabAsset(root, MartPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void ValidateVisualSequences(SerializedObject visual)
    {
        ValidateDirectionalSequence(visual, "idleSequences", 8);
        ValidateDirectionalSequence(visual, "walkSequences", 4);
        if (visual.FindProperty("blinkDown").objectReferenceValue == null ||
            visual.FindProperty("blinkLeft").objectReferenceValue == null ||
            visual.FindProperty("blinkRight").objectReferenceValue == null)
        {
            throw new InvalidOperationException("Mart's Bert visual source is missing directional blink sprites.");
        }
    }

    private static void ValidateDirectionalSequence(SerializedObject visual, string propertyName, int expectedCount)
    {
        string[] directions = { "down", "left", "right", "up" };
        SerializedProperty sequence = visual.FindProperty(propertyName);
        for (int i = 0; i < directions.Length; i++)
        {
            SerializedProperty sprites = sequence
                .FindPropertyRelative(directions[i])
                .FindPropertyRelative("sprites");
            if (sprites.arraySize != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Mart {propertyName}.{directions[i]} expected {expectedCount} sprites but found {sprites.arraySize}.");
            }
        }
    }

    private static void AuthorInteractable(GameObject root, QuestProgressInteractionBridge bridge)
    {
        Transform child = root.transform.Find("Interactable");
        GameObject interaction = child != null ? child.gameObject : new GameObject("Interactable");
        interaction.transform.SetParent(root.transform, false);
        interaction.transform.localPosition = new Vector3(0f, 1f, 0f);
        interaction.transform.localRotation = Quaternion.identity;
        interaction.transform.localScale = new Vector3(4f, 3f, 4f);
        // Prof Toby's interaction volume uses the Buildings layer in the
        // current project collision matrix.
        interaction.layer = LayerMask.NameToLayer("Buildings");

        SphereCollider collider = interaction.GetComponent<SphereCollider>();
        if (collider == null)
            collider = interaction.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.85f;
        collider.center = Vector3.zero;

        InteractableObject interactable = interaction.GetComponent<InteractableObject>();
        if (interactable == null)
            interactable = interaction.AddComponent<InteractableObject>();
        var serialized = new SerializedObject(interactable);
        serialized.FindProperty("triggerMode").enumValueIndex =
            (int)InteractableObject.TriggerMode.RequireInteract;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        FieldInfo eventField = typeof(InteractableObject).GetField(
            "onInteracted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var onInteracted = eventField.GetValue(interactable) as UnityEvent ?? new UnityEvent();
        while (onInteracted.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(onInteracted, 0);
        UnityEventTools.AddPersistentListener(onInteracted, bridge.OnInteract);
        eventField.SetValue(interactable, onInteracted);
        EditorUtility.SetDirty(interactable);
        NpcInteractableAuthoring.EnsureIndicator(interaction);
    }

    private static void WireLoomvilleScene()
    {
        Scene scene = SceneManager.GetSceneByPath(LoomvilleScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(LoomvilleScenePath, OpenSceneMode.Single);

        GameObject existing = FindSceneObject(scene, "Mart");
        if (existing == null)
            throw new InvalidOperationException("The Loomville scene does not contain the user-authored 'Mart' NPC instance.");

        GameObject martPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MartPrefabPath);
        string existingAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing);
        GameObject mart = existing;
        if (!string.Equals(existingAssetPath, MartPrefabPath, StringComparison.OrdinalIgnoreCase))
        {
            Transform parent = existing.transform.parent;
            Vector3 localPosition = existing.transform.localPosition;
            Quaternion localRotation = existing.transform.localRotation;
            Vector3 localScale = existing.transform.localScale;
            int siblingIndex = existing.transform.GetSiblingIndex();

            mart = (GameObject)PrefabUtility.InstantiatePrefab(martPrefab, scene);
            mart.name = "Mart";
            mart.transform.SetParent(parent, false);
            mart.transform.localPosition = localPosition;
            mart.transform.localRotation = localRotation;
            mart.transform.localScale = localScale;
            mart.transform.SetSiblingIndex(siblingIndex);
            UnityEngine.Object.DestroyImmediate(existing);
        }

        Transform gateParent = mart.transform.parent;
        GameObject gateHost = FindDirectChild(gateParent, "Mart_ProfileGate");
        if (gateHost == null)
        {
            gateHost = new GameObject("Mart_ProfileGate");
            gateHost.transform.SetParent(gateParent, false);
        }
        gateHost.SetActive(true);

        ProfileWorldFlagObjectGate gate = gateHost.GetComponent<ProfileWorldFlagObjectGate>();
        if (gate == null)
            gate = gateHost.AddComponent<ProfileWorldFlagObjectGate>();
        var gateSerialized = new SerializedObject(gate);
        gateSerialized.FindProperty("target").objectReferenceValue = mart;
        gateSerialized.FindProperty("flagKey").stringValue = DepartureFlag;
        gateSerialized.FindProperty("expectedFlagValue").boolValue = true;
        gateSerialized.FindProperty("activeWhenMatched").boolValue = false;
        gateSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save '{LoomvilleScenePath}'.");
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, objectName, StringComparison.Ordinal))
                    return transforms[i].gameObject;
            }
        }
        return null;
    }

    private static GameObject FindDirectChild(Transform parent, string objectName)
    {
        if (parent == null)
            return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, objectName, StringComparison.Ordinal))
                return child.gameObject;
        }
        return null;
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

        string parent = normalized.Substring(0, normalized.LastIndexOf('/'));
        string name = normalized.Substring(normalized.LastIndexOf('/') + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
