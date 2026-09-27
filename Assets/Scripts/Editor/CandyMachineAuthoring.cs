#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the candy-machine UGUI prefab, GameServices host, and Academy interact volume.
/// Idempotent. Menu: Tools / Chainbound / UI / Author Academy Candy Machine
/// </summary>
public static class CandyMachineAuthoring
{
    public const string PrefabPath = "Assets/Prefabs/UI/CandyMachine/CandyMachinePanel.prefab";
    public const string GameServicesPath = "Assets/Prefabs/GameServices/GameServices.prefab";
    public const string AcademyScenePath = "Assets/Scenes/Academy_Interior.unity";
    public const string DevConfigPath = "Assets/Data/UI/CandyMachineDevConfig.asset";

    /// <summary>
    /// Runtime prompt pivot in Interactable local space. Y lifts above the cabinet;
    /// negative Z pulls the bubble toward the approach / camera side.
    /// LateUpdate uses this field, not the Indicator RectTransform.
    /// </summary>
    public static readonly Vector3 IndicatorOffset = new Vector3(0f, 5f, -2.25f);

    /// <summary>
    /// NPC Interactable volumes are scaled (4, 3, 4) and the prompt inherits that.
    /// Candy_Machine uses an identity volume so the trigger collider stays authored in meters;
    /// apply the same local scale on the indicator itself.
    /// </summary>
    public static readonly Vector3 IndicatorScaleMultiplier = new Vector3(4f, 3f, 4f);

    private const string VendingConfigPath = "Assets/Data/Combat/Config/VendingConfig_Default.asset";
    private const string MoveDatabasePath = "Assets/Data/Combat/Moves/Database/MoveDatabase.asset";
    private const string ModifierCatalogPath = "Assets/Data/Combat/Modifiers/Catalog/ModifierCatalog.asset";
    private const string InputActionsPath = "Assets/Scripts/Movement/InputSystem_Actions.inputactions";
    private const string PullShotPath = "Assets/Data/Camera/Shots/CandyMachine_PullShot.asset";
    internal const string LeverSoundPath = "Assets/Data/Audio/SFX/CANDY_MACHINE_LEVER.asset";
    internal const string GlowSoundPath = "Assets/Data/Audio/SFX/CANDY_MACHINE_GLOW.asset";
    internal const string DoorSoundPath = "Assets/Data/Audio/SFX/CANDY_MACHINE_DOOR.asset";
    private const string LeverClipPath = "Assets/Data/Audio/SFX/gumball_machine_sfx.mp3";
    private const string GlowClipPath = "Assets/Data/Audio/SFX/sanctuary_whirr.mp3";
    private const string DoorClipPath = "Assets/Data/Audio/SFX/gumball_door_sfx.mp3";
    private const string GreyButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/gray_button_slice.png";
    private const string OrangeButtonPath = "Assets/Sprites/UI/MainMenu/Buttons/orange_button_slice.png";
    private const string ShimmerFrameFolder = "Assets/Sprites/UI/MainMenu/Buttons/Orange_Button_Anim/Sliced";
    private const string FontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";

    /// <summary>9-slice border scale shared by the idle button and the orange shimmer frames.</summary>
    internal const float ButtonPixelsPerUnitMultiplier = 0.2f;

    [MenuItem("Tools/Chainbound/UI/Author Academy Candy Machine")]
    public static void AuthorFromMenu()
    {
        Author();
    }

    public static void Author()
    {
        var report = new List<string>();
        CandyMachineDevConfigSO devConfig = EnsureDevConfig(report);
        CandyMachineScreen prefab = AuthorPrefab(devConfig, report);
        WireGameServices(prefab, report);
        AuthorAcademyInteractable(prefab, report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CandyMachineAuthoring]\n - " + string.Join("\n - ", report));
    }

    internal static CandyMachineScreen AuthorPrefab(CandyMachineDevConfigSO devConfig, List<string> report)
    {
        EnsureFolder("Assets/Prefabs/UI", "CandyMachine");

        GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (root == null)
        {
            root = new GameObject("CandyMachinePanel", typeof(RectTransform));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
        }

        root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.name = "CandyMachinePanel";
            StripLegacyUiToolkit(root);

            CandyMachineScreen screen = root.GetComponent<CandyMachineScreen>();
            if (screen == null)
                screen = root.AddComponent<CandyMachineScreen>();

            screen.Configure(
                AssetDatabase.LoadAssetAtPath<Chainbound.Combat.VendingConfigSO>(VendingConfigPath),
                AssetDatabase.LoadAssetAtPath<Chainbound.Combat.MoveDatabaseSO>(MoveDatabasePath),
                AssetDatabase.LoadAssetAtPath<Chainbound.Combat.ModifierCatalogSO>(ModifierCatalogPath),
                devConfig);
            // Palette first so every button authored by the layout pass gets sliced chrome.
            ConfigureChromePalette(screen);
            screen.BuildDefaultLayoutForEditor();
            ApplyInputActions(screen);
            ApplyChromeSprites(screen);
            ApplyTypography(root);

            var serialized = new SerializedObject(screen);
            serialized.FindProperty("vendingConfig").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Chainbound.Combat.VendingConfigSO>(VendingConfigPath);
            serialized.FindProperty("moveDatabase").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Chainbound.Combat.MoveDatabaseSO>(MoveDatabasePath);
            serialized.FindProperty("modifierCatalog").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Chainbound.Combat.ModifierCatalogSO>(ModifierCatalogPath);
            serialized.FindProperty("devConfig").objectReferenceValue = devConfig;
            serialized.FindProperty("enableDevnetOwnershipSpine").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            report?.Add($"Prefab {PrefabPath}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<CandyMachineScreen>();
    }

    internal static void WireGameServices(CandyMachineScreen prefab, List<string> report)
    {
        GameObject services = PrefabUtility.LoadPrefabContents(GameServicesPath);
        if (services == null)
        {
            report?.Add($"FAILED to load {GameServicesPath}");
            return;
        }

        try
        {
            CandyMachineScreen existing = services.GetComponentInChildren<CandyMachineScreen>(true);
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                report?.Add($"GameServices: presenter already on '{existing.gameObject.name}' (hidden until Open)");
            }
            else if (prefab == null)
            {
                report?.Add("GameServices: panel prefab missing");
            }
            else
            {
                Transform stale = services.transform.Find(CandyMachineIds.PresenterChildName);
                if (stale != null)
                    Object.DestroyImmediate(stale.gameObject);

                GameObject host = (GameObject)PrefabUtility.InstantiatePrefab(
                    prefab.gameObject,
                    services.transform);
                host.name = CandyMachineIds.PresenterChildName;
                host.SetActive(false);
                report?.Add($"GameServices: instantiated {PrefabPath} (hidden until Open)");
            }

            EditorUtility.SetDirty(services);
            PrefabUtility.SaveAsPrefabAsset(services, GameServicesPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(services);
        }
    }

    internal static void AuthorAcademyInteractable(CandyMachineScreen prefab, List<string> report)
    {
        Scene scene = SceneManager.GetSceneByPath(AcademyScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(AcademyScenePath, OpenSceneMode.Additive);

        try
        {
            GameObject machine = FindNamed(scene, CandyMachineIds.MachineObjectName);
            if (machine == null)
                throw new System.InvalidOperationException(
                    $"'{CandyMachineIds.MachineObjectName}' was not found in Academy_Interior.");

            GameObject interaction = EnsureInteractableVolume(machine);
            InteractableIndicator indicatorComponent = NpcInteractableAuthoring.EnsureIndicator(interaction);
            ApplyIndicatorOffset(indicatorComponent);

            CandyMachinePullDirector director = machine.GetComponent<CandyMachinePullDirector>();
            if (director == null)
                director = machine.AddComponent<CandyMachinePullDirector>();
            WirePullDirector(machine, director);

            CandyMachineInteractable binding = interaction.GetComponent<CandyMachineInteractable>();
            if (binding == null)
                binding = interaction.AddComponent<CandyMachineInteractable>();
            binding.Configure(prefab, director);

            InteractableObject interactable = interaction.GetComponent<InteractableObject>();
            if (interactable == null)
                interactable = interaction.AddComponent<InteractableObject>();

            SerializedObject serialized = new SerializedObject(interactable);
            serialized.FindProperty("triggerMode").enumValueIndex =
                (int)InteractableObject.TriggerMode.RequireInteract;
            serialized.FindProperty("playerTag").stringValue = "Player";
            if (indicatorComponent != null)
                serialized.FindProperty("indicator").objectReferenceValue = indicatorComponent;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            WireOpen(interactable, binding);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report?.Add($"Academy interactable on '{interaction.name}'");
        }
        finally
        {
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    internal static CandyMachineDevConfigSO EnsureDevConfig(List<string> report)
    {
        CandyMachineDevConfigSO existing = AssetDatabase.LoadAssetAtPath<CandyMachineDevConfigSO>(DevConfigPath);
        if (existing == null)
        {
            existing = ScriptableObject.CreateInstance<CandyMachineDevConfigSO>();
            AssetDatabase.CreateAsset(existing, DevConfigPath);
        }

        report?.Add($"DevConfig {DevConfigPath}");
        return existing;
    }

    private static void StripLegacyUiToolkit(GameObject root)
    {
        Component document = root.GetComponent("UIDocument");
        if (document != null)
            Object.DestroyImmediate(document);

        Component legacy = root.GetComponent("VendingMachineController");
        if (legacy != null)
            Object.DestroyImmediate(legacy);
    }

    private static void ApplyInputActions(CandyMachineScreen screen)
    {
        MenuInputContextController input = screen.GetComponent<MenuInputContextController>();
        if (input == null)
            return;

        var serialized = new SerializedObject(input);
        serialized.FindProperty("inputActions").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        serialized.FindProperty("enableMobileHudNavigation").boolValue = true;
        serialized.FindProperty("routeNavigationThroughController").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        UnityEvent cancel = input.OnCancel;
        bool alreadyWired = false;
        for (int i = 0; i < cancel.GetPersistentEventCount(); i++)
        {
            if (cancel.GetPersistentTarget(i) == screen &&
                cancel.GetPersistentMethodName(i) == nameof(CandyMachineScreen.Close))
            {
                alreadyWired = true;
                break;
            }
        }

        if (!alreadyWired)
            UnityEventTools.AddPersistentListener(cancel, screen.Close);
    }

    private static void WirePullDirector(GameObject machine, CandyMachinePullDirector director)
    {
        EnsurePresentationSoundEvents(out SoundEventSO lever, out SoundEventSO glow, out SoundEventSO door);
        SerializedObject serialized = new SerializedObject(director);
        Transform spheres = machine.transform.Find("Spheres");
        Transform emissive = machine.transform.Find("Spheres/emissivesphere");
        serialized.FindProperty("leverRoot").objectReferenceValue = machine.transform.Find("Lever_Root");
        serialized.FindProperty("spheresRoot").objectReferenceValue = spheres;
        serialized.FindProperty("emissiveRenderer").objectReferenceValue = emissive != null ? emissive.GetComponent<Renderer>() : null;
        serialized.FindProperty("candyMachineDoor").objectReferenceValue = machine.transform.Find("CandyMachineDoor");
        serialized.FindProperty("cameraTarget").objectReferenceValue = machine.transform;
        serialized.FindProperty("pullShot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CameraShotPresetSO>(PullShotPath);
        serialized.FindProperty("leverSound").objectReferenceValue = lever;
        serialized.FindProperty("glowSound").objectReferenceValue = glow;
        serialized.FindProperty("doorSound").objectReferenceValue = door;
        serialized.FindProperty("glowShakeStart").floatValue = 1.1f;
        serialized.FindProperty("glowRampUpDuration").floatValue = 2.2f;
        serialized.FindProperty("glowHoldDuration").floatValue = 0.6f;
        serialized.FindProperty("glowRampDownDuration").floatValue = 2.2f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void EnsurePresentationSoundEvents(
        out SoundEventSO lever,
        out SoundEventSO glow,
        out SoundEventSO door)
    {
        lever = EnsureSoundEvent(LeverSoundPath, "candy_machine_lever", LeverClipPath, false, 0.8f);
        glow = EnsureSoundEvent(GlowSoundPath, "candy_machine_glow", GlowClipPath, true, 0.65f);
        door = EnsureSoundEvent(DoorSoundPath, "candy_machine_door", DoorClipPath, false, 0.9f);
    }

    private static SoundEventSO EnsureSoundEvent(string assetPath, string id, string clipPath, bool loop, float volume)
    {
        SoundEventSO soundEvent = AssetDatabase.LoadAssetAtPath<SoundEventSO>(assetPath);
        if (soundEvent == null)
        {
            soundEvent = ScriptableObject.CreateInstance<SoundEventSO>();
            AssetDatabase.CreateAsset(soundEvent, assetPath);
        }

        SerializedObject serialized = new SerializedObject(soundEvent);
        serialized.FindProperty("id").stringValue = id;
        SerializedProperty clips = serialized.FindProperty("clips");
        clips.arraySize = 1;
        clips.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        serialized.FindProperty("outputBus").enumValueIndex = (int)AudioBus.SFX;
        serialized.FindProperty("loop").boolValue = loop;
        serialized.FindProperty("volumeMin").floatValue = volume;
        serialized.FindProperty("volumeMax").floatValue = volume;
        serialized.FindProperty("pitchMin").floatValue = 1f;
        serialized.FindProperty("pitchMax").floatValue = 1f;
        serialized.FindProperty("is3D").boolValue = false;
        serialized.FindProperty("spatialBlend").floatValue = 0f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(soundEvent);
        return soundEvent;
    }

    /// <summary>
    /// Sliced grey idle chrome plus orange shimmer frames, both at the same 9-slice scale.
    /// Shimmer plays on the focused control, so the legacy aqua focus ring is stripped.
    /// </summary>
    private static void ConfigureChromePalette(CandyMachineScreen screen)
    {
        screen.Chrome.Configure(
            LoadSprite(GreyButtonPath),
            LoadSprite(OrangeButtonPath),
            LoadShimmerFrames(),
            ButtonPixelsPerUnitMultiplier,
            12f,
            true);
    }

    private static void ApplyChromeSprites(CandyMachineScreen screen)
    {
        GameObject root = screen.gameObject;
        ConfigureChromePalette(screen);

        Selectable[] selectables = root.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
            screen.Chrome.Apply(selectables[i]);

        MenuSelectableFocusVisual[] strays = root.GetComponentsInChildren<MenuSelectableFocusVisual>(true);
        for (int i = 0; i < strays.Length; i++)
            PlayerMenuChromePalette.StripFocusRing(strays[i].gameObject);
    }

    private static void ApplyTypography(GameObject root)
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (font != null)
                labels[i].font = font;
            labels[i].fontStyle |= FontStyles.UpperCase;
        }
    }

    private static List<Sprite> LoadShimmerFrames()
    {
        var frames = new List<Sprite>();
        for (int i = 1; i <= 14; i++)
        {
            Sprite sprite = LoadSprite($"{ShimmerFrameFolder}/orange_button_sliceanim{i}.png");
            if (sprite != null)
                frames.Add(sprite);
        }

        return frames;
    }

    /// <summary>Button art imports as a sprite sheet, so the sprite is a sub-asset of the texture.</summary>
    private static Sprite LoadSprite(string path)
    {
        Sprite direct = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (direct != null)
            return direct;

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is Sprite sprite)
                return sprite;
        }

        return null;
    }

    private static GameObject EnsureInteractableVolume(GameObject machine)
    {
        Transform found = machine.transform.Find(CandyMachineIds.InteractableChildName);
        GameObject interaction = found != null ? found.gameObject : new GameObject(CandyMachineIds.InteractableChildName);
        interaction.transform.SetParent(machine.transform, false);
        interaction.transform.localPosition = Vector3.zero;
        interaction.transform.localRotation = Quaternion.identity;
        interaction.transform.localScale = Vector3.one;

        int buildings = LayerMask.NameToLayer("Buildings");
        if (buildings >= 0)
            interaction.layer = buildings;

        BoxCollider box = interaction.GetComponent<BoxCollider>();
        bool createdCollider = box == null;
        if (createdCollider)
            box = interaction.AddComponent<BoxCollider>();
        box.isTrigger = true;
        if (createdCollider)
        {
            box.center = new Vector3(0f, 1.6f, 0f);
            box.size = new Vector3(2.6f, 3.4f, 2.6f);
        }

        MeshCollider mesh = machine.GetComponent<MeshCollider>();
        if (mesh != null)
            mesh.isTrigger = false;

        return interaction;
    }

    private static void ApplyIndicatorOffset(InteractableIndicator indicator)
    {
        if (indicator == null)
            return;

        var serialized = new SerializedObject(indicator);
        serialized.FindProperty("offset").vector3Value = IndicatorOffset;
        serialized.FindProperty("scale").floatValue = 1f;
        serialized.FindProperty("scaleMultiplier").vector3Value = IndicatorScaleMultiplier;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(indicator);
    }

    private static void WireOpen(InteractableObject interactable, CandyMachineInteractable binding)
    {
        FieldInfo field = typeof(InteractableObject).GetField(
            "onInteracted",
            BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent interacted = field != null ? field.GetValue(interactable) as UnityEvent : null;
        if (interacted == null)
            throw new System.InvalidOperationException("InteractableObject.onInteracted was unavailable.");

        for (int i = 0; i < interacted.GetPersistentEventCount(); i++)
        {
            if (interacted.GetPersistentTarget(i) == binding &&
                interacted.GetPersistentMethodName(i) == nameof(CandyMachineInteractable.OpenCandyMachine))
            {
                return;
            }
        }

        UnityEventTools.AddPersistentListener(interacted, binding.OpenCandyMachine);
        field.SetValue(interactable, interacted);
    }

    private static GameObject FindNamed(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == objectName)
                    return children[i].gameObject;
            }
        }

        return null;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
