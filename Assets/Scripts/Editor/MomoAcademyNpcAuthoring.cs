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
/// Idempotent authoring pass for Momo in Academy_Interior. Runtime content remains
/// data-driven through the shared NPC, interaction-profile, and dialogue systems.
/// </summary>
public static class MomoAcademyNpcAuthoring
{
    private const string ScenePath = "Assets/Scenes/Academy_Interior.unity";
    private const string SourcePrefabPath = "Assets/Prefabs/NPC/Bert.prefab";
    private const string MomoPrefabPath = "Assets/Prefabs/NPC/Momo.prefab";
    private const string SpriteFolder = "Assets/Sprites/NPCs/Momo";
    private const string ContentFolder = "Assets/Scripts/NPC/Interaction/Academy/Momo";
    private const string DialoguePath = ContentFolder + "/Dial_Momo_AcademyIntroduction.asset";
    private const string ProfilePath = ContentFolder + "/momo_academy_profile.asset";
    private const string IntroQuestPath = "Assets/Scripts/Narrative/Quests/chainbound_intro/Chainbound_Intro.asset";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";
    private const string InteractionBehaviorPath = "Assets/Scripts/NPC/Interaction/NPCInteractionBehavior.asset";
    private const string NpcId = "momo";

    [MenuItem("Tools/Chainbound/Narrative/Author Academy Momo NPC")]
    public static void ApplyFromMenu() => Apply();

    public static string Apply()
    {
        EnsureFolder(ContentFolder);
        EnsureSpriteImports();

        DialogueSequenceSO dialogue = EnsureAsset<DialogueSequenceSO>(DialoguePath);
        AuthorDialogue(dialogue);

        QuestDefinitionSO introQuest = AssetDatabase.LoadAssetAtPath<QuestDefinitionSO>(IntroQuestPath);
        if (introQuest == null)
            throw new InvalidOperationException($"Missing intro quest at '{IntroQuestPath}'.");

        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(ProfilePath);
        AuthorProfile(profile, introQuest, dialogue);
        AssetDatabase.SaveAssets();

        AuthorPrefab(profile);
        AssetDatabase.SaveAssets();
        string replaced = WireScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string result = $"Authored Momo prefab, 32 directional sprites, introduction dialogue, and replaced '{replaced}' in Academy_Interior/2nd_Floor.";
        Debug.Log("[MomoAcademyNpcAuthoring] " + result);
        return result;
    }

    private static void AuthorDialogue(DialogueSequenceSO dialogue)
    {
        string[] text =
        {
            "Easy there, newcomer. The paint is wet, the machinery bites, and I dislike repeating myself. I'm Momo.",
            "This is the Academy. We built it around an antique we found humming beneath the ruins: a candy machine, if you can believe the label.",
            "It wasn't made for sweets. Not really. The old thing can read a combat pattern and teach that pattern to a Chainling.",
            "We apes learned how to tune the process. Put a Chainling through the system, and it can come away knowing a move it never knew before.",
            "Master a move deeply enough and we can stamp its pattern, bind it in ink and fiber, and make the knowledge physical.",
            "Books. Actual books. Each one puts a little something back on those empty shelves. Someday, if we don't make a mess of it, we'll restore the whole library.",
            "So learn something sharp, make it yours, and bring it back worth binding. Try to have a little style while you're at it."
        };

        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = NpcId;
        serialized.FindProperty("playerFacingPolicy").enumValueIndex = (int)DialoguePlayerFacingPolicy.FaceSpeakerOnStart;
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerName").stringValue = "Momo";
            line.FindPropertyRelative("text").stringValue = text[i];
            line.FindPropertyRelative("typeSpeed").floatValue = 48f;
            line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
            line.FindPropertyRelative("portraitSprite").objectReferenceValue = LoadSprite("momo_idle_down1");
            line.FindPropertyRelative("portraitTint").colorValue = Color.white;

            SerializedProperty camera = line.FindPropertyRelative("camera");
            if (camera != null)
            {
                camera.FindPropertyRelative("shotPreset").objectReferenceValue = null;
                camera.FindPropertyRelative("targetSource").enumValueIndex = (int)DialogueCameraTargetSource.SequenceSpeaker;
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

    private static void AuthorProfile(QuestInteractionProfileSO profile, QuestDefinitionSO quest, DialogueSequenceSO dialogue)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = quest;
        serialized.FindProperty("questId").stringValue = "";
        SerializedProperty rules = serialized.FindProperty("rules");
        int introIndex = -1;
        for (int i = 0; i < rules.arraySize; i++)
        {
            if (rules.GetArrayElementAtIndex(i).FindPropertyRelative("label").stringValue == "academy introduction")
            {
                introIndex = i;
                break;
            }
        }

        if (introIndex < 0)
        {
            rules.InsertArrayElementAtIndex(rules.arraySize);
            introIndex = rules.arraySize - 1;
        }

        SerializedProperty rule = rules.GetArrayElementAtIndex(introIndex);
        rule.FindPropertyRelative("label").stringValue = "academy introduction";
        rule.FindPropertyRelative("lifecycleScope").enumValueIndex = (int)QuestInteractionRuleLifecycleScope.UseLifecycleMask;
        rule.FindPropertyRelative("lifecycleMask").intValue = (int)(NarrativeQuestLifecycleMask.Active | NarrativeQuestLifecycleMask.Available | NarrativeQuestLifecycleMask.Completed);
        rule.FindPropertyRelative("objectiveId").stringValue = "";
        rule.FindPropertyRelative("objectiveComparison").enumValueIndex = (int)QuestInteractionObjectiveComparison.None;
        rule.FindPropertyRelative("dialogue").objectReferenceValue = dialogue;
        rule.FindPropertyRelative("useDialogueCamera").boolValue = false;
        rule.FindPropertyRelative("triggerId").stringValue = "";
        ClearArray(rule, "sequenceProgressPredicates");
        ClearArray(rule, "resolutionPredicates");
        ClearArray(rule, "flagPredicates");
        ClearArray(rule, "worldFlagPredicates");
        ClearArray(rule, "counterPredicates");
        SetNpcBehaviorMode(rule.FindPropertyRelative("npcBehavior"));

        serialized.FindProperty("inactiveDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("inactiveTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("inactiveNpcBehavior"));
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.FindProperty("designerNotes").stringValue = "Momo's repeatable Academy introduction; available across intro quest lifecycle states.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void AuthorPrefab(QuestInteractionProfileSO profile)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
            throw new InvalidOperationException($"Missing source NPC prefab at '{SourcePrefabPath}'.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(MomoPrefabPath) == null && !AssetDatabase.CopyAsset(SourcePrefabPath, MomoPrefabPath))
            throw new InvalidOperationException($"Could not create '{MomoPrefabPath}'.");

        GameObject root = PrefabUtility.LoadPrefabContents(MomoPrefabPath);
        try
        {
            root.name = "Momo";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            NPCController controller = root.GetComponent<NPCController>();
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("npcId").stringValue = NpcId;
            controllerSo.FindProperty("routeSchedule").objectReferenceValue = null;
            controllerSo.FindProperty("autonomyEnabled").boolValue = true;
            SerializedProperty behavior = controllerSo.FindProperty("interactionBehavior");
            if (behavior != null)
                behavior.objectReferenceValue = AssetDatabase.LoadAssetAtPath<NPCInteractionBehaviorSO>(InteractionBehaviorPath);
            controllerSo.FindProperty("enableDebugLogs").boolValue = false;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            MeshRendererNPCVisual visual = root.GetComponentInChildren<MeshRendererNPCVisual>(true);
            if (visual == null)
                throw new InvalidOperationException("Momo prefab source has no MeshRendererNPCVisual.");
            var visualSo = new SerializedObject(visual);
            SetDirectionalSprites(visualSo.FindProperty("idleSequences"), "idle");
            SetDirectionalSprites(visualSo.FindProperty("walkSequences"), "walk");
            visualSo.FindProperty("blinkDown").objectReferenceValue = null;
            visualSo.FindProperty("blinkLeft").objectReferenceValue = null;
            visualSo.FindProperty("blinkRight").objectReferenceValue = null;
            visualSo.FindProperty("idleAnimationSpeed").floatValue = 4f;
            visualSo.FindProperty("walkAnimationSpeed").floatValue = 8f;
            visualSo.FindProperty("spriteSize").vector2Value = new Vector2(5f, 5f);
            visualSo.FindProperty("preserveAspectRatio").boolValue = true;
            visualSo.FindProperty("referenceHeight").floatValue = 5f;
            visualSo.FindProperty("yPositionOffset").floatValue = 2f;
            visualSo.FindProperty("enableDebugLogs").boolValue = false;
            visualSo.ApplyModifiedPropertiesWithoutUndo();

            QuestProgressInteractionBridge bridge = root.GetComponent<QuestProgressInteractionBridge>();
            if (bridge == null) bridge = root.AddComponent<QuestProgressInteractionBridge>();
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("defaultProfile").objectReferenceValue = profile;
            bridgeSo.FindProperty("interactionRouter").objectReferenceValue = null;
            bridgeSo.FindProperty("enableDebugLogs").boolValue = false;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            AuthorInteractable(root, bridge);
            PrefabUtility.SaveAsPrefabAsset(root, MomoPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static string WireScene()
    {
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GameObject secondFloor = FindSceneObject(scene, "2nd_Floor");
        if (secondFloor == null)
            throw new InvalidOperationException("Could not find '2nd_Floor' in Academy_Interior.");

        NPCController existingController = secondFloor.GetComponentInChildren<NPCController>(true);
        if (existingController == null)
            throw new InvalidOperationException("No authored NPC was found beneath Academy_Interior/2nd_Floor; refusing to guess Momo's placement.");

        GameObject existing = PrefabUtility.GetOutermostPrefabInstanceRoot(existingController.gameObject) ?? existingController.gameObject;
        Transform parent = existing.transform.parent;
        Vector3 position = existing.transform.localPosition;
        Quaternion rotation = existing.transform.localRotation;
        Vector3 scale = existing.transform.localScale;
        int sibling = existing.transform.GetSiblingIndex();
        string replacedName = existing.name;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MomoPrefabPath);
        GameObject momo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        momo.name = "Momo";
        momo.transform.SetParent(parent, false);
        momo.transform.localPosition = position;
        momo.transform.localRotation = rotation;
        momo.transform.localScale = scale;
        momo.transform.SetSiblingIndex(sibling);
        UnityEngine.Object.DestroyImmediate(existing);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save '{ScenePath}'.");
        return replacedName;
    }

    private static void SetDirectionalSprites(SerializedProperty sequences, string motion)
    {
        string[] directions = { "down", "left", "right", "up" };
        foreach (string direction in directions)
        {
            SerializedProperty sprites = sequences.FindPropertyRelative(direction).FindPropertyRelative("sprites");
            sprites.arraySize = 4;
            for (int i = 0; i < 4; i++)
                sprites.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite($"momo_{motion}_{direction}{i + 1}");
        }
    }

    private static Sprite LoadSprite(string name)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");
        if (sprite == null)
            throw new InvalidOperationException($"Missing or incorrectly imported Momo sprite '{name}'.");
        return sprite;
    }

    private static void EnsureSpriteImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteFolder });
        if (guids.Length != 32)
            throw new InvalidOperationException($"Expected 32 Momo textures in '{SpriteFolder}', but found {guids.Length}.");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Could not access TextureImporter for '{path}'.");

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.filterMode != FilterMode.Point ||
                           importer.mipmapEnabled;
            if (!changed) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void AuthorInteractable(GameObject root, QuestProgressInteractionBridge bridge)
    {
        Transform found = root.transform.Find("Interactable");
        GameObject interaction = found != null ? found.gameObject : new GameObject("Interactable");
        interaction.transform.SetParent(root.transform, false);
        interaction.transform.localPosition = new Vector3(0f, 1f, 0f);
        interaction.transform.localRotation = Quaternion.identity;
        interaction.transform.localScale = new Vector3(4f, 3f, 4f);
        interaction.layer = LayerMask.NameToLayer("Buildings");

        SphereCollider collider = interaction.GetComponent<SphereCollider>();
        if (collider == null) collider = interaction.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.85f;

        InteractableObject interactable = interaction.GetComponent<InteractableObject>();
        if (interactable == null) interactable = interaction.AddComponent<InteractableObject>();
        var serialized = new SerializedObject(interactable);
        serialized.FindProperty("triggerMode").enumValueIndex = (int)InteractableObject.TriggerMode.RequireInteract;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        FieldInfo eventField = typeof(InteractableObject).GetField("onInteracted", BindingFlags.Instance | BindingFlags.NonPublic);
        var onInteracted = eventField.GetValue(interactable) as UnityEvent ?? new UnityEvent();
        while (onInteracted.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(onInteracted, 0);
        UnityEventTools.AddPersistentListener(onInteracted, bridge.OnInteract);
        eventField.SetValue(interactable, onInteracted);
        EditorUtility.SetDirty(interactable);
        NpcInteractableAuthoring.EnsureIndicator(interaction);
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(transform.name, objectName, StringComparison.OrdinalIgnoreCase))
                    return transform.gameObject;
        return null;
    }

    private static void SetNpcBehaviorMode(SerializedProperty property)
    {
        if (property == null) return;
        property.FindPropertyRelative("mode").enumValueIndex = (int)QuestInteractionNpcBehaviorMode.UseNpcDefault;
        property.FindPropertyRelative("behaviorOverride").objectReferenceValue = null;
    }

    private static void ClearArray(SerializedProperty parent, string name)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null && property.isArray) property.arraySize = 0;
    }

    private static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized)) return;
        int slash = normalized.LastIndexOf('/');
        string parent = normalized.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, normalized.Substring(slash + 1));
    }
}
#endif
