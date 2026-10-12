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
/// Idempotent authoring for Professor Charker in Academy_Interior.
/// Dialogue, sprites, and the chalkboard loop stay on shared NPC data.
/// </summary>
public static class ProfCharkerAuthoring
{
    private const string ScenePath = "Assets/Scenes/Academy_Interior.unity";
    private const string SourcePrefabPath = "Assets/Prefabs/NPC/Momo.prefab";
    private const string PrefabPath = "Assets/Prefabs/NPC/Prof_Charker.prefab";
    private const string SpriteRoot = "Assets/Sprites/NPCs/Prof Charker";
    private const string ContentFolder = "Assets/Scripts/NPC/Interaction/Academy/ProfCharker";
    private const string DialoguePath = ContentFolder + "/Dial_ProfCharker_FloodedBindery.asset";
    private const string ProfilePath = ContentFolder + "/prof_charker_academy_profile.asset";
    private const string SoundPath = "Assets/Data/Audio/SFX/CHALKBOARD_LOOP.asset";
    private const string ClipPath = "Assets/Data/Audio/SFX/chalkboardsounds.mp3";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";
    private const string InteractionBehaviorPath = "Assets/Scripts/NPC/Interaction/NPCInteractionBehavior.asset";
    private const string NpcId = "prof_charker";

    [MenuItem("Tools/Chainbound/Narrative/Author Academy Prof Charker")]
    public static void ApplyFromMenu() => Apply();

    public static string Apply()
    {
        EnsureFolder(ContentFolder);
        EnsureSpriteImports();

        SoundEventSO chalk = EnsureAsset<SoundEventSO>(SoundPath);
        AuthorChalkLoop(chalk);

        DialogueSequenceSO dialogue = EnsureAsset<DialogueSequenceSO>(DialoguePath);
        AuthorDialogue(dialogue);

        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(ProfilePath);
        AuthorProfile(profile, dialogue);
        AssetDatabase.SaveAssets();

        AuthorPrefab(profile, chalk);
        AssetDatabase.SaveAssets();
        string replaced = WireScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string result = $"Authored Prof Charker with up-facing chalkboard idle, looping chalk, flooded-bindery dialogue, and replaced '{replaced}' in Academy_Interior.";
        Debug.Log("[ProfCharkerAuthoring] " + result);
        return result;
    }

    private static void AuthorChalkLoop(SoundEventSO sound)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        if (clip == null)
            throw new InvalidOperationException($"Missing chalkboard clip at '{ClipPath}'.");

        var serialized = new SerializedObject(sound);
        serialized.FindProperty("id").stringValue = "chalkboard_loop";
        SerializedProperty clips = serialized.FindProperty("clips");
        clips.arraySize = 1;
        clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        serialized.FindProperty("outputBus").enumValueIndex = (int)AudioBus.Ambience;
        serialized.FindProperty("loop").boolValue = true;
        serialized.FindProperty("volumeMin").floatValue = 0.42f;
        serialized.FindProperty("volumeMax").floatValue = 0.42f;
        serialized.FindProperty("pitchMin").floatValue = 1f;
        serialized.FindProperty("pitchMax").floatValue = 1f;
        serialized.FindProperty("is3D").boolValue = true;
        serialized.FindProperty("spatialBlend").floatValue = 1f;
        serialized.FindProperty("minDistance").floatValue = 1.5f;
        serialized.FindProperty("maxDistance").floatValue = 14f;
        serialized.FindProperty("polyphonyKey").stringValue = "prof_charker_chalk";
        serialized.FindProperty("maxInstances").intValue = 1;
        serialized.FindProperty("cooldownSeconds").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
    }

    private static void AuthorDialogue(DialogueSequenceSO dialogue)
    {
        string[] text =
        {
            "One moment. If I don't write this down now, it'll wander off before I ever see my desk again",
            "Professor Charker. Binding is my work. Bring me a book worth the ink, and I'll gladly put it through the press",
            "It would have to wait, though. The basement is flooded, so the workshop has taken up swimming",
            "The old sewer pipes are acting up again. That's the story I'm telling myself, anyway",
            "Plumbers should be on their way. Check back once the water gets bored and leaves."
        };

        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        Sprite portrait = LoadSprite("Idle/charker_idle_down1");
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = NpcId;
        serialized.FindProperty("playerFacingPolicy").enumValueIndex = (int)DialoguePlayerFacingPolicy.FaceSpeakerOnStart;
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = text.Length;
        for (int i = 0; i < text.Length; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speakerName").stringValue = i == 0 ? "" : "Prof. Charker";
            line.FindPropertyRelative("text").stringValue = text[i];
            line.FindPropertyRelative("typeSpeed").floatValue = 48f;
            line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
            line.FindPropertyRelative("portraitSprite").objectReferenceValue = portrait;
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

    private static void AuthorProfile(QuestInteractionProfileSO profile, DialogueSequenceSO dialogue)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = null;
        serialized.FindProperty("questId").stringValue = "ambient_npc_prof_charker";
        SerializedProperty rules = serialized.FindProperty("rules");
        rules.arraySize = 0;
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("inactiveTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("inactiveNpcBehavior"));
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.FindProperty("designerNotes").stringValue =
            "Default Academy line while the bindery basement is flooded. A later bindery quest can add a profile rule without replacing this NPC.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }

    private static void AuthorPrefab(QuestInteractionProfileSO profile, SoundEventSO chalk)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
            throw new InvalidOperationException($"Missing source NPC prefab at '{SourcePrefabPath}'.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null &&
            !AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
        {
            throw new InvalidOperationException($"Could not create '{PrefabPath}'.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.name = "Prof Charker";
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            NPCController controller = root.GetComponent<NPCController>();
            if (controller == null)
                throw new InvalidOperationException("Prof Charker prefab is missing NPCController.");
            var controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("npcId").stringValue = NpcId;
            controllerSo.FindProperty("routeSchedule").objectReferenceValue = null;
            controllerSo.FindProperty("autonomyEnabled").boolValue = false;
            SerializedProperty behavior = controllerSo.FindProperty("interactionBehavior");
            if (behavior != null)
                behavior.objectReferenceValue = AssetDatabase.LoadAssetAtPath<NPCInteractionBehaviorSO>(InteractionBehaviorPath);
            controllerSo.FindProperty("enableDebugLogs").boolValue = false;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            MeshRendererNPCVisual visual = root.GetComponentInChildren<MeshRendererNPCVisual>(true);
            if (visual == null)
                throw new InvalidOperationException("Prof Charker prefab source has no MeshRendererNPCVisual.");
            var visualSo = new SerializedObject(visual);
            SetDirectionalSprites(visualSo.FindProperty("idleSequences"), "idle");
            SetDirectionalSprites(visualSo.FindProperty("walkSequences"), "walk");
            visualSo.FindProperty("blinkDown").objectReferenceValue = LoadSprite("Idle/charker_blink_down");
            visualSo.FindProperty("blinkLeft").objectReferenceValue = LoadSprite("Idle/charker_blink_left");
            visualSo.FindProperty("blinkRight").objectReferenceValue = LoadSprite("Idle/charker_blink_right");
            visualSo.FindProperty("idleAnimationSpeed").floatValue = 6f;
            visualSo.FindProperty("walkAnimationSpeed").floatValue = 8f;
            visualSo.FindProperty("spriteSize").vector2Value = new Vector2(5f, 5f);
            visualSo.FindProperty("preserveAspectRatio").boolValue = true;
            visualSo.FindProperty("referenceHeight").floatValue = 5f;
            visualSo.FindProperty("yPositionOffset").floatValue = 2f;
            visualSo.FindProperty("enableDebugLogs").boolValue = false;
            visualSo.ApplyModifiedPropertiesWithoutUndo();

            NpcStationWorkLoop station = root.GetComponent<NpcStationWorkLoop>();
            if (station == null)
                station = root.AddComponent<NpcStationWorkLoop>();
            var stationSo = new SerializedObject(station);
            stationSo.FindProperty("workLoop").objectReferenceValue = chalk;
            stationSo.FindProperty("restFacing").enumValueIndex = (int)NPCFaceDirectionPreset.WorldNorth;
            stationSo.ApplyModifiedPropertiesWithoutUndo();

            QuestProgressInteractionBridge bridge = root.GetComponent<QuestProgressInteractionBridge>();
            if (bridge == null)
                bridge = root.AddComponent<QuestProgressInteractionBridge>();
            var bridgeSo = new SerializedObject(bridge);
            bridgeSo.FindProperty("defaultProfile").objectReferenceValue = profile;
            bridgeSo.FindProperty("interactionRouter").objectReferenceValue = null;
            bridgeSo.FindProperty("enableDebugLogs").boolValue = false;
            bridgeSo.ApplyModifiedPropertiesWithoutUndo();

            AuthorInteractable(root, bridge);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
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

        GameObject existing = FindNamedNpc(scene, "Prof Charker") ?? FindNamedNpc(scene, "Charker");
        if (existing == null)
            throw new InvalidOperationException("Academy_Interior has no 'Prof Charker' or 'Charker' NPC to replace.");

        Transform parent = existing.transform.parent;
        Vector3 localPosition = existing.transform.localPosition;
        Vector3 localScale = existing.transform.localScale;
        int sibling = existing.transform.GetSiblingIndex();
        string replacedName = existing.name;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject charker = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        charker.name = "Prof Charker";
        charker.transform.SetParent(parent, false);
        charker.transform.localPosition = localPosition;
        charker.transform.localScale = localScale;
        charker.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        charker.transform.SetSiblingIndex(sibling);
        UnityEngine.Object.DestroyImmediate(existing);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException($"Failed to save '{ScenePath}'.");
        return replacedName;
    }

    private static void SetDirectionalSprites(SerializedProperty sequences, string motion)
    {
        string[] directions = { "down", "left", "right", "up" };
        string folder = motion == "walk" ? "Walk" : "Idle";
        foreach (string direction in directions)
        {
            SerializedProperty sprites = sequences.FindPropertyRelative(direction).FindPropertyRelative("sprites");
            sprites.arraySize = 4;
            for (int i = 0; i < 4; i++)
                sprites.GetArrayElementAtIndex(i).objectReferenceValue =
                    LoadSprite($"{folder}/charker_{motion}_{direction}{i + 1}");
        }
    }

    private static Sprite LoadSprite(string relativePath)
    {
        string path = $"{SpriteRoot}/{relativePath}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException($"Missing or incorrectly imported sprite '{path}'.");
        return sprite;
    }

    private static void EnsureSpriteImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SpriteRoot });
        if (guids.Length != 35)
            throw new InvalidOperationException($"Expected 35 Prof Charker textures in '{SpriteRoot}', but found {guids.Length}.");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Could not access TextureImporter for '{path}'.");

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.filterMode != FilterMode.Point ||
                           importer.mipmapEnabled ||
                           !importer.alphaIsTransparency;
            if (!changed)
                continue;

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

    private static GameObject FindNamedNpc(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (NPCController controller in root.GetComponentsInChildren<NPCController>(true))
            {
                if (string.Equals(controller.gameObject.name, objectName, StringComparison.Ordinal))
                    return controller.gameObject;
            }
        }

        return null;
    }

    private static void SetNpcBehaviorMode(SerializedProperty property)
    {
        if (property == null)
            return;
        property.FindPropertyRelative("mode").enumValueIndex = (int)QuestInteractionNpcBehaviorMode.UseNpcDefault;
        property.FindPropertyRelative("behaviorOverride").objectReferenceValue = null;
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
