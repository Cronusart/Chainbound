#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Idempotent authoring pass for self-contained ambient NPC prefabs. Dialogue
/// remains data-driven and placement remains the responsibility of scene instances.
/// </summary>
public static class AmbientNpcPrefabAuthoring
{
    private const string SourcePrefabPath = "Assets/Prefabs/NPC/Momo.prefab";
    private const string InteractionBehaviorPath = "Assets/Scripts/NPC/Interaction/NPCInteractionBehavior.asset";
    private const string ChatterPath = "Assets/Data/Audio/SFX/NPC_CHATTER.asset";
    private const string ContentRoot = "Assets/Scripts/NPC/Interaction/Ambient";

    private sealed class NpcSpec
    {
        public string displayName;
        public string npcId;
        public string spriteFolder;
        public string spritePrefix;
        public string prefabPath;
        public string contentFolder;
        public string dialoguePath;
        public string profilePath;
        public string dialogueText;
        public bool hasBlinkSprites;
    }

    [MenuItem("Tools/Chainbound/Narrative/Author Fisher-Ape and Crayon-Eater Prefabs")]
    public static void ApplyFromMenu() => Apply();

    public static string Apply()
    {
        NpcSpec[] specs =
        {
            new NpcSpec
            {
                displayName = "Fisher-Ape",
                npcId = "fisher_ape",
                spriteFolder = "Assets/Sprites/NPCs/Fisherman_Ape",
                spritePrefix = "fisherape",
                prefabPath = "Assets/Prefabs/NPC/Fisher-Ape.prefab",
                contentFolder = ContentRoot + "/FisherApe",
                dialoguePath = ContentRoot + "/FisherApe/Dial_FisherApe_StreamWarning.asset",
                profilePath = ContentRoot + "/FisherApe/fisher_ape_chatter_profile.asset",
                dialogueText = "That stream nearly took my arm. Watch for Chainlings when you cross.",
                hasBlinkSprites = false,
            },
            new NpcSpec
            {
                displayName = "Crayon-Eater",
                npcId = "crayon_eater",
                spriteFolder = "Assets/Sprites/NPCs/Crayon_Eater",
                spritePrefix = "crayoneater",
                prefabPath = "Assets/Prefabs/NPC/Crayon-Eater.prefab",
                contentFolder = ContentRoot + "/CrayonEater",
                dialoguePath = ContentRoot + "/CrayonEater/Dial_CrayonEater_CandyMachine.asset",
                profilePath = ContentRoot + "/CrayonEater/crayon_eater_chatter_profile.asset",
                dialogueText = "Saving RAM for candy-machine rolls. I'm due for a win.",
                hasBlinkSprites = true,
            },
        };

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) == null)
            throw new InvalidOperationException($"Missing source NPC prefab at '{SourcePrefabPath}'.");

        for (int i = 0; i < specs.Length; i++)
            AuthorNpc(specs[i]);

        FisherApeLuckyCrayonAuthoring.Apply();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string result = "Authored Fisher-Ape and Crayon-Eater prefabs. Fisher-Ape Lucky Crayon dialogue and reward sequence were authored after the prefab pass.";
        Debug.Log("[AmbientNpcPrefabAuthoring] " + result);
        return result;
    }

    private static void AuthorNpc(NpcSpec spec)
    {
        EnsureFolder(spec.contentFolder);
        EnsureSpriteImports(spec);

        DialogueSequenceSO dialogue = EnsureAsset<DialogueSequenceSO>(spec.dialoguePath);
        AuthorDialogue(spec, dialogue);

        QuestInteractionProfileSO profile = EnsureAsset<QuestInteractionProfileSO>(spec.profilePath);
        AuthorProfile(spec, profile, dialogue);
        AssetDatabase.SaveAssets();

        if (AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath) == null &&
            !AssetDatabase.CopyAsset(SourcePrefabPath, spec.prefabPath))
        {
            throw new InvalidOperationException($"Could not create '{spec.prefabPath}'.");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(spec.prefabPath);
        try
        {
            root.name = spec.displayName;
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            AuthorController(spec, root);
            AuthorVisual(spec, root);

            QuestProgressInteractionBridge bridge = root.GetComponent<QuestProgressInteractionBridge>();
            if (bridge == null)
                bridge = root.AddComponent<QuestProgressInteractionBridge>();
            var bridgeSerialized = new SerializedObject(bridge);
            bridgeSerialized.FindProperty("defaultProfile").objectReferenceValue = profile;
            bridgeSerialized.FindProperty("interactionRouter").objectReferenceValue = null;
            bridgeSerialized.FindProperty("enableDebugLogs").boolValue = false;
            bridgeSerialized.ApplyModifiedPropertiesWithoutUndo();

            AuthorInteractable(root, bridge);
            PrefabUtility.SaveAsPrefabAsset(root, spec.prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AuthorController(NpcSpec spec, GameObject root)
    {
        NPCController controller = root.GetComponent<NPCController>();
        if (controller == null)
            throw new InvalidOperationException($"'{spec.displayName}' source is missing NPCController.");

        NPCInteractionBehaviorSO behavior = AssetDatabase.LoadAssetAtPath<NPCInteractionBehaviorSO>(InteractionBehaviorPath);
        if (behavior == null)
            throw new InvalidOperationException($"Missing shared interaction behavior at '{InteractionBehaviorPath}'.");

        var serialized = new SerializedObject(controller);
        serialized.FindProperty("npcId").stringValue = spec.npcId;
        serialized.FindProperty("autonomyEnabled").boolValue = true;
        serialized.FindProperty("routeSchedule").objectReferenceValue = null;
        serialized.FindProperty("interactionBehavior").objectReferenceValue = behavior;
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AuthorVisual(NpcSpec spec, GameObject root)
    {
        MeshRendererNPCVisual visual = root.GetComponentInChildren<MeshRendererNPCVisual>(true);
        if (visual == null)
            throw new InvalidOperationException($"'{spec.displayName}' source is missing MeshRendererNPCVisual.");

        visual.transform.localPosition = Vector3.zero;
        var serialized = new SerializedObject(visual);
        SetDirectionalSprites(spec, serialized.FindProperty("idleSequences"), "idle");
        SetDirectionalSprites(spec, serialized.FindProperty("walkSequences"), "walk");
        ClearDirectionalSprites(serialized.FindProperty("runSequences"));

        serialized.FindProperty("blinkDown").objectReferenceValue = spec.hasBlinkSprites
            ? LoadSprite(spec, $"{spec.spritePrefix}_blink_down")
            : null;
        serialized.FindProperty("blinkLeft").objectReferenceValue = spec.hasBlinkSprites
            ? LoadSprite(spec, $"{spec.spritePrefix}_blink_left")
            : null;
        serialized.FindProperty("blinkRight").objectReferenceValue = spec.hasBlinkSprites
            ? LoadSprite(spec, $"{spec.spritePrefix}_blink_right")
            : null;
        serialized.FindProperty("blinkChance").floatValue = 0.25f;
        serialized.FindProperty("blinkDuration").floatValue = 0.25f;
        serialized.FindProperty("blinkRateMultiplier").floatValue = 1f;
        serialized.FindProperty("idleAnimationSpeed").floatValue = 4f;
        serialized.FindProperty("walkAnimationSpeed").floatValue = 8f;
        serialized.FindProperty("spriteSize").vector2Value = new Vector2(5f, 5f);
        serialized.FindProperty("preserveAspectRatio").boolValue = true;
        serialized.FindProperty("referenceHeight").floatValue = 5f;
        serialized.FindProperty("yPositionOffset").floatValue = 2f;
        serialized.FindProperty("enableDebugLogs").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AuthorDialogue(NpcSpec spec, DialogueSequenceSO dialogue)
    {
        SoundEventSO chatter = AssetDatabase.LoadAssetAtPath<SoundEventSO>(ChatterPath);
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = spec.npcId;
        serialized.FindProperty("playerFacingPolicy").enumValueIndex = (int)DialoguePlayerFacingPolicy.FaceSpeakerOnStart;

        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = 1;
        SerializedProperty line = lines.GetArrayElementAtIndex(0);
        line.FindPropertyRelative("speakerName").stringValue = spec.displayName;
        line.FindPropertyRelative("text").stringValue = spec.dialogueText;
        line.FindPropertyRelative("typeSpeed").floatValue = 48f;
        line.FindPropertyRelative("soundEvent").objectReferenceValue = chatter;
        line.FindPropertyRelative("portraitSprite").objectReferenceValue =
            LoadSprite(spec, $"{spec.spritePrefix}_idle_down1");
        line.FindPropertyRelative("portraitTint").colorValue = Color.white;

        SerializedProperty camera = line.FindPropertyRelative("camera");
        if (camera != null)
        {
            camera.FindPropertyRelative("shotPreset").objectReferenceValue = null;
            camera.FindPropertyRelative("targetSource").enumValueIndex = (int)DialogueCameraTargetSource.SequenceSpeaker;
            camera.FindPropertyRelative("npcId").stringValue = "";
            camera.FindPropertyRelative("targetTag").stringValue = "";
        }

        serialized.FindProperty("autoAdvance").boolValue = false;
        serialized.FindProperty("autoAdvanceDelay").floatValue = 2f;
        serialized.FindProperty("allowSkip").boolValue = true;
        serialized.FindProperty("skipToEndOfLine").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialogue);
    }

    private static void AuthorProfile(NpcSpec spec, QuestInteractionProfileSO profile, DialogueSequenceSO dialogue)
    {
        var serialized = new SerializedObject(profile);
        serialized.FindProperty("questDefinition").objectReferenceValue = null;
        serialized.FindProperty("questId").stringValue = $"ambient_npc_{spec.npcId}";
        serialized.FindProperty("rules").arraySize = 0;
        serialized.FindProperty("inactiveDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("inactiveTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("inactiveNpcBehavior"));
        serialized.FindProperty("activeFallbackDialogue").objectReferenceValue = dialogue;
        serialized.FindProperty("activeFallbackTriggerId").stringValue = "";
        SetNpcBehaviorMode(serialized.FindProperty("activeFallbackNpcBehavior"));
        serialized.FindProperty("designerNotes").stringValue =
            $"Repeatable ambient dialogue for {spec.displayName}; scene placement is intentionally not authored here.";
        serialized.FindProperty("activateQuestOnInactiveInteract").boolValue = false;
        serialized.FindProperty("completeObjectiveOnActivate").stringValue = "";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
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
        collider.center = Vector3.zero;

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
        var onInteracted = eventField?.GetValue(interactable) as UnityEvent ?? new UnityEvent();
        while (onInteracted.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(onInteracted, 0);
        UnityEventTools.AddPersistentListener(onInteracted, bridge.OnInteract);
        eventField?.SetValue(interactable, onInteracted);
        EditorUtility.SetDirty(interactable);
        NpcInteractableAuthoring.EnsureIndicator(interaction);
    }

    private static void SetDirectionalSprites(NpcSpec spec, SerializedProperty sequences, string motion)
    {
        string[] directions = { "down", "left", "right", "up" };
        for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
        {
            string direction = directions[directionIndex];
            SerializedProperty sprites = sequences
                .FindPropertyRelative(direction)
                .FindPropertyRelative("sprites");
            sprites.arraySize = 4;
            for (int frame = 0; frame < 4; frame++)
            {
                sprites.GetArrayElementAtIndex(frame).objectReferenceValue =
                    LoadSprite(spec, $"{spec.spritePrefix}_{motion}_{direction}{frame + 1}");
            }
        }
    }

    private static void ClearDirectionalSprites(SerializedProperty sequences)
    {
        string[] directions = { "down", "left", "right", "up" };
        for (int i = 0; i < directions.Length; i++)
        {
            sequences.FindPropertyRelative(directions[i])
                .FindPropertyRelative("sprites")
                .arraySize = 0;
        }
    }

    private static Sprite LoadSprite(NpcSpec spec, string assetName)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{spec.spriteFolder}/{assetName}.png");
        if (sprite == null)
            throw new InvalidOperationException($"Missing or incorrectly imported sprite '{assetName}' for {spec.displayName}.");
        return sprite;
    }

    private static void EnsureSpriteImports(NpcSpec spec)
    {
        int expectedTextureCount = spec.hasBlinkSprites ? 35 : 32;
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { spec.spriteFolder });
        if (guids.Length != expectedTextureCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedTextureCount} textures for {spec.displayName} in '{spec.spriteFolder}', but found {guids.Length}.");
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException($"Could not access TextureImporter for '{path}'.");

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                           importer.spriteImportMode != SpriteImportMode.Single ||
                           importer.filterMode != FilterMode.Point ||
                           importer.mipmapEnabled ||
                           !importer.alphaIsTransparency ||
                           importer.textureCompression != TextureImporterCompression.Uncompressed;
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
