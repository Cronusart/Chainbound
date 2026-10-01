#if UNITY_EDITOR
using System;
using Chainbound.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Idempotent sanctuary content: wake-line catalog, portal prefab, hub + outdoor
/// latch volumes, and GameServices wiring.
/// </summary>
public static class SanctuarySceneAuthoring
{
    public const string HubObjectName = "SanctuaryHub_PlayerRoom";
    public const string HomeBedSpawnName = "SpawnPoint_home_bed";
    public const string LoomvillePortalName = "SanctuaryPortal_LoomvilleGate";
    public const string LoomvilleSpawnName = "SpawnPoint_sanctuary_loomville_gate";
    public const string JunglePortalName = "SanctuaryPortal_JungleTrailhead";
    public const string JungleSpawnName = "SpawnPoint_sanctuary_jungle_trailhead";

    public const string LoomvilleSpawnId = "sanctuary_loomville_gate";
    public const string JungleSpawnId = "sanctuary_jungle_trailhead";
    public const string HomeBedSpawnId = "home_bed";
    public const string PlayerRoomStartSpawnId = "player_room_start";

    private const string PlayerRoomScenePath = "Assets/Scenes/player_room.unity";
    private const string LoomvilleScenePath = "Assets/Scenes/Loomville.unity";
    private const string JungleScenePath = "Assets/Scenes/Jungle.unity";
    private const string GameServicesPrefabPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    private const string PortalPrefabPath = "Assets/Scripts/World/Environment/Prefabs/SanctuaryPortal.prefab";
    private const string StructureModelPath = "Assets/Sprites/World/Sanctuary.obj";
    private const string PortalModelPath = "Assets/Sprites/World/Sanctuary_Portal.obj";
    private const string SceneryMaterialPath = "Assets/Sprites/Materials/Scenery.mat";
    private const string StructureMaterialPath = "Assets/Sprites/Materials/SanctuaryStructure.mat";
    private const string ArrivalSoundPath = "Assets/Data/Audio/SFX/Combat/Sanctuary_Arrival.asset";
    private const string DepartSoundPath = "Assets/Data/Audio/SFX/Combat/Sanctuary_Depart.asset";
    private const string GlitchClipAPath = "Assets/Data/Audio/SFX/Combat/Combat_SFX_Glitch.asset";
    private const string GlitchClipBPath = "Assets/Data/Audio/SFX/Combat/Combat_SFX_Glitch2.asset";
    private const string GlitchMaterialPath = "Assets/Sprites/Materials/Signal_Glitch.mat";
    private const string PingSoundPath = "Assets/Data/Audio/SFX/Combat/Sanctuary_Checkin.asset";
    private const string DialogueFolder = "Assets/Scripts/Narrative/Dialogue";
    private const string ResourcesNarrativeFolder = "Assets/Resources/Narrative";
    private const string CatalogPath = ResourcesNarrativeFolder + "/SanctuaryWakeCatalog.asset";

    private static readonly Vector3 PlayerRoomHubPosition = new Vector3(-18.12f, 1.11f, 43.73f);
    private static readonly Vector3 PlayerRoomHubSize = new Vector3(10f, 4f, 10f);
    private static readonly Vector3 HomeBedPosition = new Vector3(-18.25f, 1.56f, 43.32f);
    private static readonly Vector3 LoomvillePortalPosition = new Vector3(151.07f, 22.67f, 70.89f);
    private static readonly Vector3 LoomvillePortalSize = new Vector3(14f, 8f, 14f);
    private static readonly Vector3 JungleTrailheadPosition = new Vector3(340f, -26.5f, 255f);
    private static readonly Vector3 JungleTrailheadSize = new Vector3(16f, 10f, 16f);

    private static readonly string[] ContinueWakeLines =
    {
        "Good morning. The sun is glad you're here.",
        "Welcome back. The light kept your place.",
        "Hello again. The day is waiting.",
        "The sun is up. So are you.",
        "Warm light. Same world. Let's go."
    };

    private static readonly string[] DefeatWakeLines =
    {
        "You fell. That's all right. Rise in the light.",
        "What ended here can be made into something new.",
        "Nothing is wasted. Stand up. Begin again.",
        "The sun takes what is finished and starts it over.",
        "Come back. The day is not done with you."
    };

    private static readonly string[] DogFallbackWakeLines =
    {
        "I'm here. That's enough for now.",
        "Take a breath. The world's still here.",
        "Easy. I'm okay.",
        "Back again. No rush.",
        "I saved a spot. Come on."
    };

    [MenuItem("Tools/Chainbound/World/Author Sanctuary Portal Visual")]
    public static void ApplyVisualFromMenu()
    {
        Material glitchMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlitchMaterialPath);
        RebuildPortalPrefab(glitchMaterial);
        AssetDatabase.SaveAssets();
        Debug.Log("[SanctuarySceneAuthoring] Rebuilt SanctuaryPortal prefab visual.");
    }

    [MenuItem("Tools/Chainbound/World/Author Sanctuary Portals")]
    public static void ApplyFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Apply();
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    public static void Apply()
    {
        EnsureFolder(DialogueFolder);
        EnsureFolder(ResourcesNarrativeFolder);
        EnsureFolder("Assets/Scripts/World/Environment/Prefabs");

        DialogueSequenceSO[] continueLines = AuthorWakeSet("Dial_SanctuaryWake", ContinueWakeLines);
        DialogueSequenceSO[] defeatLines = AuthorWakeSet("Dial_SanctuaryWake_Defeat", DefeatWakeLines);
        DialogueSequenceSO[] dogLines = AuthorWakeSet(
            "Dial_SanctuaryWake_Dog",
            DogFallbackWakeLines,
            speakerName: "{playerName}");

        SanctuaryWakeCatalogSO catalog = EnsureAsset<SanctuaryWakeCatalogSO>(CatalogPath);
        AuthorCatalog(catalog, continueLines, defeatLines, dogLines);

        Material glitchMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlitchMaterialPath);
        SoundEventSO pingSound = AssetDatabase.LoadAssetAtPath<SoundEventSO>(PingSoundPath);
        GameObject portalPrefab = RebuildPortalPrefab(glitchMaterial);

        WireGameServices(catalog, glitchMaterial, portalPrefab);

        Scene previous = EditorSceneManager.GetActiveScene();
        string previousPath = previous.path;

        WirePlayerRoom(pingSound, portalPrefab);
        WireLoomville(pingSound, portalPrefab);
        WireJungle(pingSound, portalPrefab);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previousPath))
            EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Single);

        Debug.Log("[SanctuarySceneAuthoring] Authored sanctuary hub, outdoor portals, wake catalog, and GameServices wiring.");
    }

    private static DialogueSequenceSO[] AuthorWakeSet(string filePrefix, string[] texts, string speakerName = "")
    {
        var lines = new DialogueSequenceSO[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            string path = $"{DialogueFolder}/{filePrefix}_{i + 1:00}.asset";
            DialogueSequenceSO dialogue = EnsureAsset<DialogueSequenceSO>(path);
            AuthorWakeLine(dialogue, texts[i], speakerName);
            lines[i] = dialogue;
        }

        return lines;
    }

    private static void AuthorWakeLine(DialogueSequenceSO dialogue, string text, string speakerName = "")
    {
        var serialized = new SerializedObject(dialogue);
        serialized.FindProperty("speakerNpcId").stringValue = "";
        serialized.FindProperty("playerFacingPolicy").enumValueIndex = (int)DialoguePlayerFacingPolicy.None;
        SerializedProperty lines = serialized.FindProperty("lines");
        lines.arraySize = 1;
        SerializedProperty line = lines.GetArrayElementAtIndex(0);
        line.FindPropertyRelative("speakerName").stringValue = speakerName ?? "";
        line.FindPropertyRelative("text").stringValue = text;
        line.FindPropertyRelative("typeSpeed").floatValue = 42f;
        line.FindPropertyRelative("soundEvent").objectReferenceValue = null;
        SerializedProperty camera = line.FindPropertyRelative("camera");
        camera.FindPropertyRelative("shotPreset").objectReferenceValue = null;
        camera.FindPropertyRelative("targetSource").enumValueIndex = (int)DialogueCameraTargetSource.None;
        camera.FindPropertyRelative("npcId").stringValue = "";
        camera.FindPropertyRelative("targetTag").stringValue = "";
        serialized.FindProperty("autoAdvance").boolValue = true;
        serialized.FindProperty("autoAdvanceDelay").floatValue = 2.2f;
        serialized.FindProperty("allowSkip").boolValue = true;
        serialized.FindProperty("skipToEndOfLine").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialogue);
    }

    private static void AuthorCatalog(
        SanctuaryWakeCatalogSO catalog,
        DialogueSequenceSO[] continueLines,
        DialogueSequenceSO[] defeatLines,
        DialogueSequenceSO[] dogFallbackLines)
    {
        var serialized = new SerializedObject(catalog);
        WriteLineList(serialized.FindProperty("lines"), continueLines);
        WriteLineList(serialized.FindProperty("defeatLines"), defeatLines);
        WriteLineList(serialized.FindProperty("dogFallbackLines"), dogFallbackLines);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void WriteLineList(SerializedProperty list, DialogueSequenceSO[] lines)
    {
        if (list == null)
            return;
        list.arraySize = lines != null ? lines.Length : 0;
        for (int i = 0; i < list.arraySize; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = lines[i];
    }

    private static GameObject RebuildPortalPrefab(Material glitchMaterial)
    {
        AssetDatabase.ImportAsset(PortalModelPath, ImportAssetOptions.ForceUpdate);

        Material structureMat = EnsureStructureMaterial();
        SoundEventSO arrival = EnsureSanctuarySound(
            ArrivalSoundPath,
            "Sanctuary_Arrival",
            GlitchClipAPath,
            loop: false,
            pitchMin: 0.62f,
            pitchMax: 0.72f,
            volumeMin: 0.45f,
            volumeMax: 0.55f);
        SoundEventSO depart = EnsureSanctuarySound(
            DepartSoundPath,
            "Sanctuary_Depart",
            GlitchClipBPath,
            loop: false,
            pitchMin: 0.78f,
            pitchMax: 0.88f,
            volumeMin: 0.7f,
            volumeMax: 0.85f);

        GameObject root = new GameObject("SanctuaryPortal");
        Transform visualRoot = EnsureChild(root.transform, "VisualRoot").transform;

        GameObject structure = InstantiateModelChild(StructureModelPath, visualRoot, "Structure");
        GameObject portalMesh = InstantiateModelChild(PortalModelPath, visualRoot, "PortalMesh");
        AlignPortalToStructure(structure, portalMesh);
        GroundVisualFeet(visualRoot);

        MeshRenderer structureRenderer = structure.GetComponentInChildren<MeshRenderer>(true);
        MeshRenderer portalRenderer = portalMesh.GetComponentInChildren<MeshRenderer>(true);
        if (structureRenderer != null)
            structureRenderer.sharedMaterial = structureMat;
        if (portalRenderer != null)
        {
            portalRenderer.sharedMaterial = glitchMaterial;
            portalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            portalRenderer.receiveShadows = false;
        }

        EmissionPulse pulse = structureRenderer != null
            ? structureRenderer.GetComponent<EmissionPulse>()
            : structure.GetComponent<EmissionPulse>();
        if (pulse == null)
            pulse = (structureRenderer != null ? structureRenderer.gameObject : structure).AddComponent<EmissionPulse>();
        var pulseSerialized = new SerializedObject(pulse);
        pulseSerialized.FindProperty("targetRenderer").objectReferenceValue = structureRenderer;
        pulseSerialized.FindProperty("emissionProperty").stringValue = "_EmissionColor";
        pulseSerialized.FindProperty("emissionColor").colorValue = new Color(1.35f, 1.05f, 0.38f, 1f);
        pulseSerialized.FindProperty("minIntensity").floatValue = 0.55f;
        pulseSerialized.FindProperty("maxIntensity").floatValue = 1.45f;
        pulseSerialized.FindProperty("pulseSpeed").floatValue = 0.28f;
        pulseSerialized.FindProperty("useSmoothEase").boolValue = true;
        pulseSerialized.FindProperty("useUnscaledTime").boolValue = true;
        pulseSerialized.FindProperty("randomizePhase").boolValue = true;
        pulseSerialized.ApplyModifiedPropertiesWithoutUndo();

        SignalGlitchEffect structureGlitch = CreateGlitchOverlay(
            "StructureGlitchOverlay",
            structureRenderer,
            glitchMaterial);
        SignalGlitchEffect portalGlitch = CreateGlitchOverlay(
            "GlitchOverlay",
            portalRenderer,
            glitchMaterial);

        GameObject lightGo = EnsureChild(visualRoot, "RadiantLight");
        Light light = lightGo.GetComponent<Light>();
        if (light == null)
            light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.92f, 0.48f);
        light.intensity = 2.15f;
        light.range = structureRenderer != null
            ? Mathf.Max(10f, structureRenderer.bounds.size.y * 0.5f)
            : 10f;
        light.shadows = LightShadows.None;
        if (structureRenderer != null)
            lightGo.transform.position = structureRenderer.bounds.center;
        else
            lightGo.transform.localPosition = new Vector3(0f, 2.4f, 0f);

        SanctuaryPortalPresentation presentation = root.GetComponent<SanctuaryPortalPresentation>();
        if (presentation == null)
            presentation = root.AddComponent<SanctuaryPortalPresentation>();
        var presentationSerialized = new SerializedObject(presentation);
        presentationSerialized.FindProperty("visualRoot").objectReferenceValue = visualRoot;
        presentationSerialized.FindProperty("structureRenderer").objectReferenceValue = structureRenderer;
        presentationSerialized.FindProperty("portalRenderer").objectReferenceValue = portalRenderer;
        presentationSerialized.FindProperty("structureGlitch").objectReferenceValue = structureGlitch;
        presentationSerialized.FindProperty("portalGlitch").objectReferenceValue = portalGlitch;
        presentationSerialized.FindProperty("radiantLight").objectReferenceValue = light;
        presentationSerialized.FindProperty("arrivalHoldDuration").floatValue = 1.5f;
        presentationSerialized.FindProperty("emissionFadeDuration").floatValue = 1.25f;
        presentationSerialized.FindProperty("presentationBehindDistance").floatValue = 1.75f;
        presentationSerialized.FindProperty("arrivalSound").objectReferenceValue = arrival;
        presentationSerialized.FindProperty("departSound").objectReferenceValue = depart;
        presentationSerialized.ApplyModifiedPropertiesWithoutUndo();
        ApplyPortalRenderLayers(root, structure, portalMesh, structureGlitch, portalGlitch);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PortalPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject InstantiateModelChild(string modelPath, Transform parent, string objectName)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
            throw new InvalidOperationException($"Missing sanctuary model at '{modelPath}'.");

        GameObject instance;
        if (PrefabUtility.GetPrefabAssetType(model) != PrefabAssetType.NotAPrefab)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (PrefabUtility.IsPartOfPrefabInstance(instance))
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        else
        {
            instance = UnityEngine.Object.Instantiate(model);
        }

        instance.name = objectName;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            UnityEngine.Object.DestroyImmediate(colliders[i]);

        return instance;
    }

    private static void AlignPortalToStructure(GameObject structure, GameObject portal)
    {
        MeshRenderer structureRenderer = structure.GetComponentInChildren<MeshRenderer>(true);
        MeshRenderer portalRenderer = portal.GetComponentInChildren<MeshRenderer>(true);
        if (structureRenderer == null || portalRenderer == null)
            return;

        Bounds structureBounds = structureRenderer.bounds;
        Bounds portalBounds = portalRenderer.bounds;
        if (portalBounds.size.y < 0.01f || portalBounds.size.z < 0.01f)
            return;

        const float widthFactor = 0.70f;
        const float heightFactor = 0.84f;
        Vector3 scale = portal.transform.localScale;
        scale.y *= (structureBounds.size.y * heightFactor) / portalBounds.size.y;
        scale.z *= (structureBounds.size.z * widthFactor) / portalBounds.size.z;
        portal.transform.localScale = scale;

        portalBounds = portalRenderer.bounds;
        Vector3 target = structureBounds.center;
        target.y = structureBounds.min.y + structureBounds.size.y * 0.52f;
        portal.transform.position += target - portalBounds.center;
    }

    private static void GroundVisualFeet(Transform visualRoot)
    {
        Bounds combined = new Bounds(visualRoot.position, Vector3.zero);
        MeshRenderer[] renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;
            if (!hasBounds)
            {
                combined = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(renderers[i].bounds);
            }
        }

        if (!hasBounds)
            return;

        Transform space = visualRoot.parent != null ? visualRoot.parent : visualRoot;
        Vector3 worldFeet = new Vector3(combined.center.x, combined.min.y, combined.center.z);
        Vector3 localFeet = space.InverseTransformPoint(worldFeet);
        visualRoot.localPosition -= localFeet;
    }

    private static SignalGlitchEffect CreateGlitchOverlay(
        string objectName,
        MeshRenderer source,
        Material glitchMaterial)
    {
        Transform parent = source != null && source.transform.parent != null
            ? source.transform.parent
            : source != null ? source.transform : null;
        if (parent == null)
            return null;

        GameObject overlay = EnsureChild(parent, objectName);
        if (source != null)
        {
            overlay.transform.localPosition = source.transform.localPosition;
            overlay.transform.localRotation = source.transform.localRotation;
            overlay.transform.localScale = source.transform.localScale;
        }
        MeshFilter filter = overlay.GetComponent<MeshFilter>();
        if (filter == null)
            filter = overlay.AddComponent<MeshFilter>();
        MeshRenderer overlayRenderer = overlay.GetComponent<MeshRenderer>();
        if (overlayRenderer == null)
            overlayRenderer = overlay.AddComponent<MeshRenderer>();

        MeshFilter sourceFilter = source != null ? source.GetComponent<MeshFilter>() : null;
        if (sourceFilter != null)
            filter.sharedMesh = sourceFilter.sharedMesh;

        overlayRenderer.enabled = false;
        overlayRenderer.sharedMaterial = glitchMaterial;
        overlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        SignalGlitchEffect effect = overlay.GetComponent<SignalGlitchEffect>();
        if (effect == null)
            effect = overlay.AddComponent<SignalGlitchEffect>();

        var serialized = new SerializedObject(effect);
        serialized.FindProperty("glitchMaterial").objectReferenceValue = glitchMaterial;
        serialized.FindProperty("sourceMeshRenderer").objectReferenceValue = source;
        serialized.FindProperty("defaultGlitchOutDuration").floatValue = 0.85f;
        serialized.FindProperty("loopIntensity").floatValue =
            objectName == "GlitchOverlay" ? 0.34f : 0.18f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return effect;
    }

    private static Material EnsureStructureMaterial()
    {
        Color emission = new Color(1.25f, 0.98f, 0.32f) * 1.8f;
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(StructureMaterialPath);
        if (existing != null)
        {
            ApplyStructureEmission(existing, emission);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        Material scenery = AssetDatabase.LoadAssetAtPath<Material>(SceneryMaterialPath);
        Material created = scenery != null ? new Material(scenery) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        created.name = "SanctuaryStructure";
        ApplyStructureEmission(created, emission);
        AssetDatabase.CreateAsset(created, StructureMaterialPath);
        return created;
    }

    private static void ApplyStructureEmission(Material material, Color emission)
    {
        if (material == null)
            return;

        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emission);
    }

    private static SoundEventSO EnsureSanctuarySound(
        string path,
        string id,
        string sourceEventPath,
        bool loop,
        float pitchMin,
        float pitchMax,
        float volumeMin,
        float volumeMax)
    {
        SoundEventSO source = AssetDatabase.LoadAssetAtPath<SoundEventSO>(sourceEventPath);
        SoundEventSO sound = EnsureAsset<SoundEventSO>(path);
        var serialized = new SerializedObject(sound);
        serialized.FindProperty("id").stringValue = id;
        SerializedProperty clips = serialized.FindProperty("clips");
        clips.arraySize = 1;
        AudioClip clip = null;
        if (source != null)
        {
            var sourceSerialized = new SerializedObject(source);
            SerializedProperty sourceClips = sourceSerialized.FindProperty("clips");
            if (sourceClips.arraySize > 0)
                clip = sourceClips.GetArrayElementAtIndex(0).objectReferenceValue as AudioClip;
        }

        clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
        serialized.FindProperty("outputBus").enumValueIndex = 0;
        serialized.FindProperty("loop").boolValue = loop;
        serialized.FindProperty("volumeMin").floatValue = volumeMin;
        serialized.FindProperty("volumeMax").floatValue = volumeMax;
        serialized.FindProperty("pitchMin").floatValue = pitchMin;
        serialized.FindProperty("pitchMax").floatValue = pitchMax;
        serialized.FindProperty("is3D").boolValue = true;
        serialized.FindProperty("spatialBlend").floatValue = 0.85f;
        serialized.FindProperty("minDistance").floatValue = 2f;
        serialized.FindProperty("maxDistance").floatValue = 28f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sound);
        return sound;
    }

    private static void WireGameServices(
        SanctuaryWakeCatalogSO catalog,
        Material glitchMaterial,
        GameObject portalPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(GameServicesPrefabPath);
        try
        {
            EnsureService<SanctuaryStateService>(root, "SanctuaryStateService");
            SanctuarySpawnDirector director = EnsureService<SanctuarySpawnDirector>(root, "SanctuarySpawnDirector");
            var serialized = new SerializedObject(director);
            serialized.FindProperty("wakeCatalog").objectReferenceValue = catalog;
            serialized.FindProperty("glitchMaterial").objectReferenceValue = glitchMaterial;
            serialized.FindProperty("portalPrefab").objectReferenceValue = portalPrefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(director);
            PrefabUtility.SaveAsPrefabAsset(root, GameServicesPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T EnsureService<T>(GameObject servicesRoot, string objectName) where T : Component
    {
        T existing = servicesRoot.GetComponentInChildren<T>(true);
        if (existing != null)
            return existing;

        var host = new GameObject(objectName);
        host.transform.SetParent(servicesRoot.transform, false);
        return host.AddComponent<T>();
    }

    private static void WirePlayerRoom(SoundEventSO pingSound, GameObject portalPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(PlayerRoomScenePath, OpenSceneMode.Single);
        EnsureSpawnPoint(HomeBedSpawnName, HomeBedSpawnId, HomeBedPosition, indoor: true, facing: 2);
        GameObject hub = EnsureZone(
            HubObjectName,
            PlayerRoomHubPosition,
            PlayerRoomHubSize,
            SanctuaryLatchKind.Hub,
            scene.name,
            PlayerRoomStartSpawnId,
            pingSound,
            portalPrefab: null);
        _ = hub;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireLoomville(SoundEventSO pingSound, GameObject portalPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(LoomvilleScenePath, OpenSceneMode.Single);
        EnsureSpawnPoint(LoomvilleSpawnName, LoomvilleSpawnId, LoomvillePortalPosition, indoor: false, facing: 2);
        EnsureZone(
            LoomvillePortalName,
            LoomvillePortalPosition,
            LoomvillePortalSize,
            SanctuaryLatchKind.Portal,
            scene.name,
            LoomvilleSpawnId,
            pingSound,
            portalPrefab,
            residentChunkSceneIds: new[] { scene.name, "Link1" });
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void WireJungle(SoundEventSO pingSound, GameObject portalPrefab)
    {
        Scene scene = EditorSceneManager.OpenScene(JungleScenePath, OpenSceneMode.Single);
        EnsureSpawnPoint(JungleSpawnName, JungleSpawnId, JungleTrailheadPosition, indoor: false, facing: 2);
        EnsureZone(
            JunglePortalName,
            JungleTrailheadPosition,
            JungleTrailheadSize,
            SanctuaryLatchKind.Portal,
            scene.name,
            JungleSpawnId,
            pingSound,
            portalPrefab,
            residentChunkSceneIds: new[] { scene.name, "Link1" });
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static SpawnPoint EnsureSpawnPoint(
        string objectName,
        string spawnId,
        Vector3 position,
        bool indoor,
        int facing)
    {
        GameObject host = GameObject.Find(objectName);
        if (host == null)
            host = new GameObject(objectName);

        host.transform.position = position;
        SpawnPoint spawn = host.GetComponent<SpawnPoint>();
        if (spawn == null)
            spawn = host.AddComponent<SpawnPoint>();

        var serialized = new SerializedObject(spawn);
        serialized.FindProperty("spawnId").stringValue = spawnId;
        serialized.FindProperty("spawnRotationY").floatValue = 0f;
        serialized.FindProperty("runtimeVerticalOffsetFromPlayer").floatValue = 0.08f;
        serialized.FindProperty("facingDirection").enumValueIndex = facing;
        serialized.FindProperty("lightingType").enumValueIndex = indoor ? 1 : 0;
        serialized.FindProperty("showDebugGizmo").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(spawn);
        return spawn;
    }

    private static GameObject EnsureZone(
        string objectName,
        Vector3 position,
        Vector3 size,
        SanctuaryLatchKind kind,
        string sceneId,
        string spawnId,
        SoundEventSO pingSound,
        GameObject portalPrefab,
        params string[] residentChunkSceneIds)
    {
        GameObject host = GameObject.Find(objectName);
        if (host == null)
            host = new GameObject(objectName);

        host.transform.position = position;
        BoxCollider box = host.GetComponent<BoxCollider>();
        if (box == null)
            box = host.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        box.center = Vector3.zero;

        GameObject ping = EnsureChild(host.transform, "PingVfx");
        ping.transform.localPosition = Vector3.zero;
        ping.transform.localScale = new Vector3(3.2f, 0.04f, 3.2f);
        if (ping.GetComponent<MeshFilter>() == null)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            MeshFilter srcFilter = cylinder.GetComponent<MeshFilter>();
            MeshRenderer srcRenderer = cylinder.GetComponent<MeshRenderer>();
            ping.AddComponent<MeshFilter>().sharedMesh = srcFilter.sharedMesh;
            ping.AddComponent<MeshRenderer>().sharedMaterial = srcRenderer.sharedMaterial;
            UnityEngine.Object.DestroyImmediate(cylinder);
        }

        ping.SetActive(false);
        int pingLayer = LayerMask.NameToLayer("Emissive");
        if (pingLayer >= 0)
            ping.layer = pingLayer;

        int volumeLayer = LayerMask.NameToLayer("Volume");
        if (volumeLayer >= 0)
            host.layer = volumeLayer;

        GameObject portalVisual = null;
        if (kind == SanctuaryLatchKind.Portal && portalPrefab != null)
        {
            Transform existingPortal = host.transform.Find("SanctuaryPortal");
            if (existingPortal != null)
            {
                portalVisual = existingPortal.gameObject;
            }
            else
            {
                portalVisual = (GameObject)PrefabUtility.InstantiatePrefab(portalPrefab);
                portalVisual.name = "SanctuaryPortal";
                portalVisual.transform.SetParent(host.transform, false);
                portalVisual.transform.localPosition = Vector3.up * 1.2f;
            }

            portalVisual.SetActive(false);
        }

        SanctuaryLatchZone zone = host.GetComponent<SanctuaryLatchZone>();
        if (zone == null)
            zone = host.AddComponent<SanctuaryLatchZone>();

        var serialized = new SerializedObject(zone);
        serialized.FindProperty("kind").enumValueIndex = (int)kind;
        serialized.FindProperty("sceneId").stringValue = sceneId;
        string[] chunks = kind == SanctuaryLatchKind.Portal
            ? (residentChunkSceneIds != null && residentChunkSceneIds.Length > 0
                ? residentChunkSceneIds
                : new[] { sceneId })
            : Array.Empty<string>();
        SerializedProperty chunkProp = serialized.FindProperty("residentChunkSceneIds");
        if (chunkProp != null && chunkProp.isArray)
        {
            chunkProp.arraySize = chunks.Length;
            for (int i = 0; i < chunks.Length; i++)
                chunkProp.GetArrayElementAtIndex(i).stringValue = chunks[i] ?? "";
        }
        serialized.FindProperty("spawnId").stringValue = spawnId;
        serialized.FindProperty("requiredFlagKey").stringValue =
            kind == SanctuaryLatchKind.Portal ? SanctuaryLatchZone.DefaultPortalUnlockFlag : "";
        serialized.FindProperty("pingSound").objectReferenceValue = pingSound;
        serialized.FindProperty("pingVfx").objectReferenceValue = ping;
        serialized.FindProperty("pingVfxDuration").floatValue = 0.6f;
        serialized.FindProperty("pingCooldown").floatValue = 4f;
        serialized.FindProperty("portalVisual").objectReferenceValue = portalVisual;
        serialized.FindProperty("playerTag").stringValue = "Player";
        serialized.FindProperty("useDogWakeFallback").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(zone);
        return host;
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

    private static void ApplyPortalRenderLayers(
        GameObject root,
        GameObject structure,
        GameObject portalMesh,
        SignalGlitchEffect structureGlitch,
        SignalGlitchEffect portalGlitch)
    {
        int buildings = LayerMask.NameToLayer("Buildings");
        int emissive = LayerMask.NameToLayer("Emissive");
        if (buildings >= 0)
        {
            SetLayerRecursively(root, buildings);
            if (structure != null)
                SetLayerRecursively(structure, buildings);
        }

        if (emissive >= 0)
        {
            if (portalMesh != null)
                SetLayerRecursively(portalMesh, emissive);
            if (structureGlitch != null)
                SetLayerRecursively(structureGlitch.gameObject, emissive);
            if (portalGlitch != null)
                SetLayerRecursively(portalGlitch.gameObject, emissive);
        }
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null || layer < 0)
            return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
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
