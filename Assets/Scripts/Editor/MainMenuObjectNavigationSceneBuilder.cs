#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SignalJam.UI.MenuNavigation;

/// <summary>
/// Deterministic authoring/validation utility for the illustrated main menu.
/// Re-running it repairs generated layers and serialized sprite/navigation refs.
/// </summary>
public static class MainMenuObjectNavigationSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/main_menu.unity";
    private const string ArtRoot = "Assets/Sprites/UI/MainMenu/MainMenuRefactor";
    private const string InputActionsPath = "Assets/Scripts/Movement/InputSystem_Actions.inputactions";
    private const string MobileServicesPrefabPath = "Assets/Prefabs/Controls/MobileServices.prefab";
    private const string EventsFeedPath = "Assets/Data/UI/MainMenuEventsFeed.asset";
    private const float CanvasWidth = 1335f;
    private const float CanvasHeight = 600f;
    private const float ArtWidth = 241f;
    private const float ArtHeight = 119f;

    private sealed class ObjectAuthoring
    {
        public string name;
        public MainMenuObjectHotspot hotspot;
        public SyncedOutlineFrameAnimator animator;
        public RectTransform visualRoot;
        public Image body;
        public Image fill;
        public Image outline;
        public RectTransform bubble;
        public CanvasGroup group;
    }

    [MenuItem("Tools/Signal Jam/Main Menu/Rebuild Object Navigation")]
    public static void Build()
    {
        EnsureMobileHudRootCanvasGroup();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject canvasObject = FindSceneObject(scene, "MainMenuCanvas");
        GameObject sequenceObject = FindSceneObject(scene, "MainMenuSequenceController");
        GameObject eventSystemObject = FindSceneObject(scene, "EventSystem");
        if (canvasObject == null || sequenceObject == null || eventSystemObject == null)
            throw new InvalidOperationException("main_menu is missing MainMenuCanvas, MainMenuSequenceController, or EventSystem.");

        RectTransform canvas = canvasObject.GetComponent<RectTransform>();
        MainMenuSequenceController2 sequence = sequenceObject.GetComponent<MainMenuSequenceController2>();
        MenuInputContextController menuInput = sequenceObject.GetComponent<MenuInputContextController>();
        SpriteCinematicStageBinder stageBinder = sequenceObject.GetComponent<SpriteCinematicStageBinder>();
        TMP_Text textTemplate = canvasObject.transform.Find("ConnectSettingsGroup/NewGameButton/Text (TMP)")
            ?.GetComponent<TMP_Text>();
        Button buttonTemplate = canvasObject.transform.Find("ConnectSettingsGroup/NewGameButton")
            ?.GetComponent<Button>();
        if (sequence == null || menuInput == null || stageBinder == null || textTemplate == null || buttonTemplate == null)
            throw new InvalidOperationException("The scene is missing a required controller or legacy UI template.");

        Transform existingForegroundMotionRoot = canvas.Find("ForegroundMotionRoot");
        Transform foregroundTransform = existingForegroundMotionRoot != null
            ? existingForegroundMotionRoot.Find("Foreground")
            : canvas.Find("Foreground");
        if (foregroundTransform == null)
            throw new InvalidOperationException("main_menu is missing the static Foreground layer.");

        // Preserve the hand-authored static foreground when repairing an already-migrated scene.
        if (existingForegroundMotionRoot != null)
        {
            foregroundTransform.SetParent(canvas, false);
            UnityEngine.Object.DestroyImmediate(existingForegroundMotionRoot.gameObject);
        }

        DeleteChild(canvas, "BG");
        DeleteChild(canvas, "BG (1)");
        DeleteChild(canvas, "Objects");
        DeleteChild(canvas, "FGVinyl");
        DeleteChild(canvas, "BubbleButtons");
        DeleteChild(canvas, "GameMenuPanel");
        // Clean up the pre-rename generated root if this scene has not been
        // rebuilt since the panel received its player-facing name.
        DeleteChild(canvas, "BonkDogPanel");
        DeleteChild(canvas, "PCDeskPanel");
        DeleteChild(canvas, "EventsPanel");

        CanvasGroup backgroundGroup = ConfigureStaticLayer(
            canvas.Find("Background"), $"{ArtRoot}/BG/mainmenu_bg.png");
        CanvasGroup foregroundGroup = ConfigureStaticLayer(
            foregroundTransform, $"{ArtRoot}/FG/FG.png");

        GameObject foregroundMotionObject = NewUi("ForegroundMotionRoot", canvas);
        RectTransform foregroundMotionRoot = (RectTransform)foregroundMotionObject.transform;
        SetStretch(foregroundMotionRoot);
        foregroundTransform.SetParent(foregroundMotionRoot, false);
        SetStretch((RectTransform)foregroundTransform);

        GameObject objectsObject = NewUi("Objects", canvas, typeof(CanvasGroup), typeof(MainMenuObjectNavigator));
        SetStretch((RectTransform)objectsObject.transform);
        CanvasGroup objectsGroup = objectsObject.GetComponent<CanvasGroup>();
        MainMenuObjectNavigator navigator = objectsObject.GetComponent<MainMenuObjectNavigator>();

        GameObject bubblesObject = NewUi("BubbleButtons", canvas, typeof(CanvasGroup));
        SetStretch((RectTransform)bubblesObject.transform);
        CanvasGroup bubblesGroup = bubblesObject.GetComponent<CanvasGroup>();

        ObjectAuthoring pc = CreateObject(
            objectsObject.transform, bubblesObject.transform, textTemplate,
            "PCDesk", "PC DESK", "PCDesk/pcdesk", "PCDesk/Outline/pcdeskoutline.png",
            "PCDesk/PCDeskFill/PCDeskFill.png",
            "BubbleButtons/pcdeskbubble.png", 0.105f, 0.43f, 0.31f, 0.546f,
            new Vector2(-525f, -62f), new Vector2(285f, 215f),
            new Vector2(-252f, 28f), new Vector2(425f, 76f), animatedOutline: false);

        ObjectAuthoring bonk = CreateObject(
            objectsObject.transform, bubblesObject.transform, textTemplate,
            "BonkDog", "NEW GAME", "Bonky/bonkyanim", "Bonky/BonkyOutline/bonkyoutlineanim",
            "Bonky/BonkyFill/BonkyFill.png",
            "BubbleButtons/bonkybubble.png", 0.5f, 0.55f, 0.5f, 0.534f,
            new Vector2(0f, 35f), new Vector2(250f, 135f),
            new Vector2(0f, 20f), new Vector2(425f, 78f), animatedOutline: true);

        ObjectAuthoring xmas = CreateObject(
            objectsObject.transform, bubblesObject.transform, textTemplate,
            "XmasTree", "EVENTS", "XmasTree/xmastreeanim", "XmasTree/Outline/xmastreeoutline.png",
            "XmasTree/XmasTreeFill/XmasTreeFill.png",
            "BubbleButtons/xmastreebubble.png", 0.84f, 0.48f, 0.676f, 0.538f,
            new Vector2(455f, -8f), new Vector2(245f, 245f),
            new Vector2(238f, 22f), new Vector2(425f, 76f), animatedOutline: false);

        GameObject vinylGroupObject = NewUi("FGVinyl", foregroundMotionRoot, typeof(CanvasGroup));
        SetStretch((RectTransform)vinylGroupObject.transform);
        CanvasGroup vinylGroup = vinylGroupObject.GetComponent<CanvasGroup>();
        ObjectAuthoring vinyl = CreateObject(
            vinylGroupObject.transform, bubblesObject.transform, textTemplate,
            "VinylPlayer", "SETTINGS", "FG_Vinyl/FGVinylAnim", "FG_Vinyl/Outline/FGVinylAnimOutline",
            "FG_Vinyl/Fill/FGVinylFill.png",
            "BubbleButtons/vinylbubble.png", 0.5f, 0.2f, 0.5f, 0.26f,
            new Vector2(0f, -205f), new Vector2(930f, 105f),
            new Vector2(0f, -145f), new Vector2(425f, 78f), animatedOutline: true);
        vinyl.group = vinylGroup;

        WireNavigation(pc.hotspot, bonk.hotspot, xmas.hotspot, vinyl.hotspot);
        SetObjectList(navigator, "hotspots", pc.hotspot, bonk.hotspot, xmas.hotspot, vinyl.hotspot);
        SetObject(navigator, "defaultHotspot", bonk.hotspot);
        SetColor(navigator, "availabilityOutlineTint", new Color32(155, 171, 178, 255));
        SetColor(navigator, "selectedOutlineTint", new Color32(251, 255, 134, 255));
        SetFloat(navigator, "availabilityMinAlpha", 0.35f);
        SetFloat(navigator, "availabilityMaxAlpha", 0.7f);
        SetFloat(navigator, "availabilityPulseSeconds", 2.4f);

        MainMenuEventsFeedSO feed = EnsureEventsFeed();
        BonkDogPanelController bonkPanel = CreateBonkPanel(canvas, buttonTemplate, textTemplate, sequence);
        PCDeskPanelController pcPanel = CreatePcPanel(canvas, buttonTemplate, textTemplate);
        EventsPanelController eventsPanel = CreateEventsPanel(canvas, buttonTemplate, textTemplate, feed);

        ClearPersistentListeners(pc.hotspot.OnConfirmed);
        ClearPersistentListeners(bonk.hotspot.OnConfirmed);
        ClearPersistentListeners(xmas.hotspot.OnConfirmed);
        ClearPersistentListeners(vinyl.hotspot.OnConfirmed);
        UnityEventTools.AddPersistentListener(pc.hotspot.OnConfirmed, sequence.OpenPCDeskPanel);
        UnityEventTools.AddPersistentListener(bonk.hotspot.OnConfirmed, sequence.OpenBonkDogPanel);
        UnityEventTools.AddPersistentListener(xmas.hotspot.OnConfirmed, sequence.OpenEventsPanel);
        UnityEventTools.AddPersistentListener(vinyl.hotspot.OnConfirmed, sequence.OpenSettingsFromVinyl);

        SetObject(sequence, "backgroundGroup", backgroundGroup);
        SetObject(sequence, "backgroundAnimator", null);
        SetObject(sequence, "foregroundGroup", foregroundGroup);
        SetObject(sequence, "foregroundMotionRoot", foregroundMotionRoot);
        SetObject(sequence, "foregroundAnimator", null);
        SetObject(sequence, "objectLayersGroup", objectsGroup);
        SetObject(sequence, "foregroundVinylGroup", vinylGroup);
        SetObject(sequence, "bubbleButtonsGroup", bubblesGroup);
        SetObject(sequence, "objectNavigator", navigator);
        SetObject(sequence, "pcDeskHotspot", pc.hotspot);
        SetObject(sequence, "bonkDogHotspot", bonk.hotspot);
        SetObject(sequence, "xmasTreeHotspot", xmas.hotspot);
        SetObject(sequence, "vinylHotspot", vinyl.hotspot);
        SetObject(sequence, "bonkDogPanel", bonkPanel);
        SetObject(sequence, "pcDeskPanel", pcPanel);
        SetObject(sequence, "eventsPanel", eventsPanel);
        SetObjectArray(sequence, "objectFrameAnimators", pc.animator, bonk.animator, xmas.animator, vinyl.animator);
        SetFloat(sequence, "foregroundMoveSpeedY", 376f);
        SetFloat(sequence, "logoMoveSpeedY", 120f);
        SetFloat(sequence, "logoDismissFadeOutSeconds", 1.25f);
        SetFloat(sequence, "objectRevealScaleUpSeconds", 0.2f);
        SetFloat(sequence, "objectRevealScale", 1.12f);
        SetFloat(sequence, "objectRevealFillAlpha", 0.55f);
        SetFloat(sequence, "objectRevealFadeOutSeconds", 0.65f);

        CanvasGroup mobileHudRoot = FindSceneTransform(scene, "MobileHudCanvas")?.GetComponent<CanvasGroup>();
        SetObject(sequence, "mobileHudRootGroup", mobileHudRoot);

        SetObject(menuInput, "defaultSelection", bonk.hotspot);
        SetObject(menuInput, "fallbackSelection", pc.hotspot);
        SetObjectList(menuInput, "navigationOrder", pc.hotspot, bonk.hotspot, xmas.hotspot, vinyl.hotspot);
        SetBool(menuInput, "configureExplicitNavigation", false);
        SetBool(menuInput, "routeNavigationThroughController", true);

        WireInputModule(eventSystemObject.GetComponent<InputSystemUIInputModule>());
        WireStageBinder(stageBinder, pc, bonk, xmas, vinyl, objectsObject, foregroundMotionObject, bubblesObject, mobileHudRoot,
            bonkPanel, pcPanel, eventsPanel, canvasObject.GetComponent<SettingsPanelController>(),
            canvas.Find("Background")?.gameObject);

        GameObject legacyConnect = canvas.Find("ConnectSettingsGroup")?.gameObject;
        GameObject legacyPlay = canvas.Find("PlayButtonGroup")?.gameObject;
        if (legacyConnect != null) legacyConnect.SetActive(false);
        if (legacyPlay != null) legacyPlay.SetActive(false);

        ApplySiblingOrder(canvas, objectsObject.transform, foregroundMotionRoot, bubblesObject.transform,
            bonkPanel.transform, pcPanel.transform, eventsPanel.transform);
        SetLayerRecursively(canvasObject, 5);

        EnsureAssetFolder("Assets/Prefabs/UI");
        EnsureAssetFolder("Assets/Prefabs/UI/MainMenu");
        SavePanelPrefab(scene, "GameMenuPanel", "Assets/Prefabs/UI/MainMenu/GameMenuPanel.prefab");
        SavePanelPrefab(scene, "PCDeskPanel", "Assets/Prefabs/UI/MainMenu/PCDeskPanel.prefab");
        SavePanelPrefab(scene, "EventsPanel", "Assets/Prefabs/UI/MainMenu/EventsPanel.prefab");

        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(menuInput);
        EditorUtility.SetDirty(stageBinder);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        ValidateOrThrow();
        Debug.Log("[MainMenuObjectNavigationSceneBuilder] Build and validation completed successfully.");
    }

    [MenuItem("Tools/Signal Jam/Main Menu/Wire Object Fill Sprites")]
    public static void WireObjectFillSprites()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureObjectFill(FindSceneTransform(scene, "PCDesk")?.GetComponent<MainMenuObjectHotspot>(),
            "PCDesk/PCDeskFill/PCDeskFill.png");
        EnsureObjectFill(FindSceneTransform(scene, "BonkDog")?.GetComponent<MainMenuObjectHotspot>(),
            "Bonky/BonkyFill/BonkyFill.png");
        EnsureObjectFill(FindSceneTransform(scene, "XmasTree")?.GetComponent<MainMenuObjectHotspot>(),
            "XmasTree/XmasTreeFill/XmasTreeFill.png");
        EnsureObjectFill(FindSceneTransform(scene, "VinylPlayer")?.GetComponent<MainMenuObjectHotspot>(),
            "FG_Vinyl/Fill/FGVinylFill.png");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[MainMenuObjectNavigationSceneBuilder] Object fill sprites wired.");
    }

    [MenuItem("Tools/Signal Jam/Main Menu/Create Panel Prefabs")]
    public static void CreatePanelPrefabs()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        const string prefabFolder = "Assets/Prefabs/UI/MainMenu";
        EnsureAssetFolder("Assets/Prefabs/UI");
        EnsureAssetFolder(prefabFolder);

        SavePanelPrefab(scene, "GameMenuPanel", $"{prefabFolder}/GameMenuPanel.prefab");
        SavePanelPrefab(scene, "PCDeskPanel", $"{prefabFolder}/PCDeskPanel.prefab");
        SavePanelPrefab(scene, "EventsPanel", $"{prefabFolder}/EventsPanel.prefab");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MainMenuObjectNavigationSceneBuilder] Main-menu panel prefabs created in {prefabFolder}.");
    }

    [MenuItem("Tools/Signal Jam/Main Menu/Validate Object Navigation")]
    public static void ValidateOrThrow()
    {
        Scene scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var failures = new List<string>();
        MainMenuObjectHotspot pc = FindSceneTransform(scene, "PCDesk")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectHotspot bonk = FindSceneTransform(scene, "BonkDog")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectHotspot xmas = FindSceneTransform(scene, "XmasTree")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectHotspot vinyl = FindSceneTransform(scene, "VinylPlayer")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectNavigator navigator = FindSceneTransform(scene, "Objects")?.GetComponent<MainMenuObjectNavigator>();
        ValidateOutlinePulse(navigator, new[] { pc, bonk, xmas, vinyl }, failures);
        Transform foregroundMotionRoot = FindSceneTransform(scene, "ForegroundMotionRoot");
        Transform foreground = FindSceneTransform(scene, "Foreground");
        Transform foregroundVinyl = FindSceneTransform(scene, "FGVinyl");
        ValidateAnimator(pc, 32, 1, "PCDesk/pcdesk", "PCDesk/Outline/pcdeskoutline.png", false, failures);
        ValidateAnimator(bonk, 32, 32, "Bonky/bonkyanim", "Bonky/BonkyOutline/bonkyoutlineanim", true, failures);
        ValidateAnimator(xmas, 32, 1, "XmasTree/xmastreeanim", "XmasTree/Outline/xmastreeoutline.png", false, failures);
        ValidateAnimator(vinyl, 32, 32, "FG_Vinyl/FGVinylAnim", "FG_Vinyl/Outline/FGVinylAnimOutline", true, failures);

        ValidateDirection(pc, "up", null, failures);
        ValidateDirection(pc, "right", bonk, failures);
        ValidateDirection(pc, "down", vinyl, failures);
        ValidateDirection(pc, "left", null, failures);
        ValidateDirection(bonk, "up", null, failures);
        ValidateDirection(bonk, "left", pc, failures);
        ValidateDirection(bonk, "right", xmas, failures);
        ValidateDirection(bonk, "down", vinyl, failures);
        ValidateDirection(xmas, "up", null, failures);
        ValidateDirection(xmas, "left", bonk, failures);
        ValidateDirection(xmas, "down", vinyl, failures);
        ValidateDirection(xmas, "right", null, failures);
        ValidateDirection(vinyl, "up", bonk, failures);
        ValidateDirection(vinyl, "down", null, failures);
        ValidateDirection(vinyl, "left", pc, failures);
        ValidateDirection(vinyl, "right", xmas, failures);

        if (foregroundMotionRoot == null)
            failures.Add("ForegroundMotionRoot is missing.");
        if (foreground == null || foregroundVinyl == null ||
            foreground.parent != foregroundMotionRoot || foregroundVinyl.parent != foregroundMotionRoot)
        {
            failures.Add("Foreground and FGVinyl must be direct children of ForegroundMotionRoot.");
        }
        else if (foreground.GetSiblingIndex() >= foregroundVinyl.GetSiblingIndex())
        {
            failures.Add("Foreground must render immediately below FGVinyl inside ForegroundMotionRoot.");
        }

        Transform sequenceTransform = FindSceneTransform(scene, "MainMenuSequenceController");
        MainMenuSequenceController2 sequence = sequenceTransform?.GetComponent<MainMenuSequenceController2>();
        MenuInputContextController input = sequenceTransform?.GetComponent<MenuInputContextController>();
        if (input == null)
            failures.Add("MenuInputContextController is missing.");
        else
        {
            SerializedObject inputSo = new SerializedObject(input);
            if (inputSo.FindProperty("configureExplicitNavigation").boolValue)
                failures.Add("MenuInputContextController must not overwrite the authored 2D navigation graph.");
            if (inputSo.FindProperty("defaultSelection").objectReferenceValue != bonk)
                failures.Add("Bonk Dog is not the default selection.");
        }

        ValidatePersistentAction(pc, sequence, nameof(MainMenuSequenceController2.OpenPCDeskPanel), failures);
        ValidatePersistentAction(bonk, sequence, nameof(MainMenuSequenceController2.OpenBonkDogPanel), failures);
        ValidatePersistentAction(xmas, sequence, nameof(MainMenuSequenceController2.OpenEventsPanel), failures);
        ValidatePersistentAction(vinyl, sequence, nameof(MainMenuSequenceController2.OpenSettingsFromVinyl), failures);

        BonkDogPanelController bonkPanel = FindSceneTransform(scene, "GameMenuPanel")?.GetComponent<BonkDogPanelController>();
        PCDeskPanelController pcPanel = FindSceneTransform(scene, "PCDeskPanel")?.GetComponent<PCDeskPanelController>();
        EventsPanelController eventsPanel = FindSceneTransform(scene, "EventsPanel")?.GetComponent<EventsPanelController>();
        ValidatePanel(bonkPanel, 3, new[]
        {
            "newGameButton", "connectWalletButton", "continueButton", "sequenceController",
        }, failures, requireCloseButton: false);
        ValidatePanel(pcPanel, 1, Array.Empty<string>(), failures);
        ValidatePanel(eventsPanel, 1, new[] { "feed", "titleLabel", "bodyLabel" }, failures);
        ValidatePanelPrefab(bonkPanel, "Assets/Prefabs/UI/MainMenu/GameMenuPanel.prefab", failures);
        ValidatePanelPrefab(pcPanel, "Assets/Prefabs/UI/MainMenu/PCDeskPanel.prefab", failures);
        ValidatePanelPrefab(eventsPanel, "Assets/Prefabs/UI/MainMenu/EventsPanel.prefab", failures);

        if (sequence == null)
            failures.Add("MainMenuSequenceController2 is missing.");
        else
        {
            SerializedObject sequenceSo = new SerializedObject(sequence);
            RequireReference(sequenceSo, "foregroundMotionRoot", failures);
            RequireReference(sequenceSo, "objectNavigator", failures);
            RequireReference(sequenceSo, "settingsPanel", failures);
            RequireReference(sequenceSo, "bonkDogPanel", failures);
            RequireReference(sequenceSo, "pcDeskPanel", failures);
            RequireReference(sequenceSo, "eventsPanel", failures);
            RequireReference(sequenceSo, "mobileHudRootGroup", failures);
            RequirePositiveFloat(sequenceSo, "backgroundFadeInSeconds", failures);
            RequirePositiveFloat(sequenceSo, "foregroundMoveSpeedY", failures);
            RequirePositiveFloat(sequenceSo, "logoMoveSpeedY", failures);
            RequirePositiveFloat(sequenceSo, "logoDismissFadeOutSeconds", failures);
            RequirePositiveFloat(sequenceSo, "objectRevealScaleUpSeconds", failures);
            RequirePositiveFloat(sequenceSo, "objectRevealScale", failures);
            RequirePositiveFloat(sequenceSo, "objectRevealFadeOutSeconds", failures);
            SerializedProperty fillAlpha = sequenceSo.FindProperty("objectRevealFillAlpha");
            if (fillAlpha == null || fillAlpha.floatValue < 0f || fillAlpha.floatValue > 1f)
                failures.Add("MainMenuSequenceController2.objectRevealFillAlpha must be between 0 and 1.");
        }

        InputSystemUIInputModule inputModule = FindSceneTransform(scene, "EventSystem")
            ?.GetComponent<InputSystemUIInputModule>();
        ValidateInputActions(inputModule, failures);
        ValidateHudPrefab(failures);

        if (failures.Count > 0)
            throw new InvalidOperationException("Main-menu object navigation validation failed:\n- " + string.Join("\n- ", failures));

        Debug.Log("[MainMenuObjectNavigationSceneBuilder] Validation passed: synchronized reveal/motion, sprites, 2D navigation, panels, input bindings, and mobile HUD policy are wired.");
    }

    [MenuItem("Tools/Signal Jam/Main Menu/Run Play Mode Smoke")]
    public static void RunPlayModeSmoke()
    {
        if (!Application.isPlaying)
            throw new InvalidOperationException("Enter Play Mode and wait for the main-menu reveal before running the smoke test.");

        Scene scene = SceneManager.GetActiveScene();
        var failures = new List<string>();
        EventSystem eventSystem = EventSystem.current;
        MainMenuObjectHotspot pc = FindSceneTransform(scene, "PCDesk")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectHotspot bonk = FindSceneTransform(scene, "BonkDog")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectHotspot xmas = FindSceneTransform(scene, "XmasTree")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuObjectHotspot vinyl = FindSceneTransform(scene, "VinylPlayer")?.GetComponent<MainMenuObjectHotspot>();
        MainMenuSequenceController2 sequence = FindSceneTransform(scene, "MainMenuSequenceController")
            ?.GetComponent<MainMenuSequenceController2>();
        MenuInputContextController menuInput = FindSceneTransform(scene, "MainMenuSequenceController")
            ?.GetComponent<MenuInputContextController>();
        BonkDogPanelController bonkPanel = FindSceneTransform(scene, "GameMenuPanel")?.GetComponent<BonkDogPanelController>();
        PCDeskPanelController pcPanel = FindSceneTransform(scene, "PCDeskPanel")?.GetComponent<PCDeskPanelController>();
        EventsPanelController eventsPanel = FindSceneTransform(scene, "EventsPanel")?.GetComponent<EventsPanelController>();
        SettingsPanelController settingsPanel = FindSceneTransform(scene, "MainMenuCanvas")?.GetComponent<SettingsPanelController>();

        if (eventSystem == null)
            failures.Add("No active EventSystem exists at runtime.");
        if (sequence == null)
            failures.Add("MainMenuSequenceController2 is missing at runtime.");

        if (sequence != null && sequence.IsAwaitingMenuStart)
        {
            if (sequence.IsObjectMenuInteractive)
                failures.Add("Object navigation became interactive before the Chainbound logo was dismissed.");
            if (menuInput != null && menuInput.IsInteractionEnabled)
                failures.Add("MenuInputContextController is routing interaction during the Chainbound logo gate.");
            if (sequence.GameLogoAlpha < 0.99f)
                failures.Add($"Chainbound logo alpha is {sequence.GameLogoAlpha:0.###} at the input gate, expected 1.");
            if (bonk != null && bonk.IsSelected)
                failures.Add("Bonk Dog received focus before the menu was enabled.");

            if (failures.Count > 0)
                throw new InvalidOperationException("Main-menu logo-gate smoke failed:\n- " + string.Join("\n- ", failures));

            sequence.RequestMenuStart();
            Debug.Log("[MainMenuObjectNavigationSceneBuilder] Logo-gate smoke passed and dismissal was requested. Re-run after the fade to verify navigation, confirms, and back routes.");
            return;
        }

        if (sequence != null && !sequence.IsObjectMenuInteractive)
            failures.Add("Object navigation is not interactive. Wait for the reveal or run the smoke once to dismiss the logo gate.");
        if (bonk == null || !bonk.IsSelected)
            failures.Add("Bonk Dog did not receive default focus after the reveal.");
        if (bonk == null || bonk.FrameAnimator == null || !bonk.FrameAnimator.IsPlaying)
            failures.Add("Bonk Dog's synchronized frame animator is not playing.");

        if (eventSystem != null)
        {
            ValidateRuntimeMove(eventSystem, pc, MoveDirection.Right, bonk, failures);
            ValidateRuntimeMove(eventSystem, pc, MoveDirection.Down, vinyl, failures);
            ValidateRuntimeMove(eventSystem, bonk, MoveDirection.Left, pc, failures);
            ValidateRuntimeMove(eventSystem, bonk, MoveDirection.Right, xmas, failures);
            ValidateRuntimeMove(eventSystem, bonk, MoveDirection.Down, vinyl, failures);
            ValidateRuntimeMove(eventSystem, xmas, MoveDirection.Left, bonk, failures);
            ValidateRuntimeMove(eventSystem, xmas, MoveDirection.Down, vinyl, failures);
            ValidateRuntimeMove(eventSystem, vinyl, MoveDirection.Up, bonk, failures);
            ValidateRuntimeMove(eventSystem, vinyl, MoveDirection.Left, pc, failures);
            ValidateRuntimeMove(eventSystem, vinyl, MoveDirection.Right, xmas, failures);

            ValidateRuntimePanel(eventSystem, sequence, pc, pcPanel, failures);
            ValidateRuntimePanel(eventSystem, sequence, bonk, bonkPanel, failures);
            ValidateRuntimePanel(eventSystem, sequence, xmas, eventsPanel, failures);
            ValidateRuntimeSettings(eventSystem, sequence, vinyl, settingsPanel, failures);
            eventSystem.SetSelectedGameObject(bonk != null ? bonk.gameObject : null);
        }

        if (failures.Count > 0)
            throw new InvalidOperationException("Main-menu play-mode smoke failed:\n- " + string.Join("\n- ", failures));

        Debug.Log("[MainMenuObjectNavigationSceneBuilder] Play-mode smoke passed: default focus, all 10 moves, four confirms, and four back/return-focus routes work.");
    }

    private static void ValidateRuntimeMove(
        EventSystem eventSystem,
        MainMenuObjectHotspot from,
        MoveDirection direction,
        MainMenuObjectHotspot expected,
        List<string> failures)
    {
        if (from == null || expected == null)
            return;

        eventSystem.SetSelectedGameObject(from.gameObject);
        Vector2 vector = direction == MoveDirection.Left ? Vector2.left :
            direction == MoveDirection.Right ? Vector2.right :
            direction == MoveDirection.Up ? Vector2.up : Vector2.down;
        var move = new AxisEventData(eventSystem) { moveDir = direction, moveVector = vector };
        ExecuteEvents.Execute(from.gameObject, move, ExecuteEvents.moveHandler);
        if (eventSystem.currentSelectedGameObject != expected.gameObject)
            failures.Add($"Runtime move {from.name} {direction} did not select {expected.name}.");
    }

    private static void ValidateRuntimePanel(
        EventSystem eventSystem,
        MainMenuSequenceController2 sequence,
        MainMenuObjectHotspot hotspot,
        MainMenuPanelControllerBase panel,
        List<string> failures)
    {
        if (sequence == null || hotspot == null || panel == null)
            return;

        eventSystem.SetSelectedGameObject(hotspot.gameObject);
        ExecuteEvents.Execute(hotspot.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        if (!panel.IsOpen)
        {
            failures.Add($"{hotspot.name} confirm did not open {panel.name}.");
            return;
        }

        Selectable expectedSelection = panel.DefaultSelection;
        if (expectedSelection == null || eventSystem.currentSelectedGameObject != expectedSelection.gameObject)
        {
            failures.Add($"Opening {panel.name} did not select its default button.");
        }

        sequence.HandleMenuCancelRequested();
        panel.GetComponent<CanvasGroup>()?.DOKill(true);
        (panel.NavigationRoot as RectTransform)?.DOKill(true);
        if (panel.IsOpen)
            failures.Add($"Back did not close {panel.name}.");
        if (eventSystem.currentSelectedGameObject != hotspot.gameObject)
            failures.Add($"Closing {panel.name} did not return focus to {hotspot.name}.");
    }

    private static void ValidateRuntimeSettings(
        EventSystem eventSystem,
        MainMenuSequenceController2 sequence,
        MainMenuObjectHotspot vinyl,
        SettingsPanelController settingsPanel,
        List<string> failures)
    {
        if (sequence == null || vinyl == null || settingsPanel == null)
            return;

        eventSystem.SetSelectedGameObject(vinyl.gameObject);
        ExecuteEvents.Execute(vinyl.gameObject, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
        if (!settingsPanel.IsOpen)
        {
            failures.Add("Vinyl confirm did not open SettingsPanel.");
            return;
        }

        Selectable expectedSelection = settingsPanel.DefaultNavigationSelection;
        if (expectedSelection == null || eventSystem.currentSelectedGameObject != expectedSelection.gameObject)
        {
            failures.Add("Opening SettingsPanel did not select its default control.");
        }

        sequence.HandleMenuCancelRequested();
        settingsPanel.NavigationRoot.GetComponent<CanvasGroup>()?.DOKill(true);
        (settingsPanel.NavigationRoot as RectTransform)?.DOKill(true);
        if (settingsPanel.IsOpen)
            failures.Add("Back did not close SettingsPanel.");
        if (eventSystem.currentSelectedGameObject != vinyl.gameObject)
            failures.Add("Closing SettingsPanel did not return focus to VinylPlayer.");
    }

    private static ObjectAuthoring CreateObject(
        Transform artParent,
        Transform bubbleParent,
        TMP_Text textTemplate,
        string objectName,
        string bubbleText,
        string bodyPrefix,
        string outlinePathOrPrefix,
        string fillPath,
        string bubblePath,
        float pivotX,
        float pivotY,
        float bubblePivotX,
        float bubblePivotY,
        Vector2 hitPosition,
        Vector2 hitSize,
        Vector2 bubbleLabelPosition,
        Vector2 bubbleHitSize,
        bool animatedOutline)
    {
        GameObject container = NewUi(objectName, artParent, typeof(MainMenuObjectHotspot), typeof(SyncedOutlineFrameAnimator));
        SetStretch((RectTransform)container.transform);

        GameObject visual = NewUi("VisualRoot", container.transform);
        RectTransform visualRect = (RectTransform)visual.transform;
        visualRect.pivot = new Vector2(pivotX, pivotY);
        SetStretch(visualRect);

        Image body = CreateFullCanvasImage("Body", visual.transform, LoadSprite($"{ArtRoot}/{bodyPrefix}1.png"));
        Image fill = CreateFullCanvasImage("Fill", visual.transform, LoadSprite($"{ArtRoot}/{fillPath}"));
        fill.color = new Color(1f, 1f, 1f, 0f);
        fill.enabled = false;
        fill.raycastTarget = false;
        Image outline = CreateFullCanvasImage(
            "Outline", visual.transform,
            LoadSprite($"{ArtRoot}/{outlinePathOrPrefix}{(animatedOutline ? "1.png" : string.Empty)}"));
        // Availability outlines stay visible at rest; focus changes their
        // tint in MainMenuObjectHotspot rather than hiding them.
        outline.enabled = true;

        GameObject hitObject = NewUi("HitZone", container.transform, typeof(CanvasRenderer), typeof(Image));
        RectTransform hitRect = (RectTransform)hitObject.transform;
        SetCenteredRect(hitRect, hitPosition, hitSize);
        Image hitImage = hitObject.GetComponent<Image>();
        hitImage.color = new Color(1f, 1f, 1f, 0.001f);
        hitImage.raycastTarget = true;

        GameObject bubbleObject = NewUi(objectName + "Bubble", bubbleParent, typeof(CanvasRenderer), typeof(Image));
        RectTransform bubbleRect = (RectTransform)bubbleObject.transform;
        bubbleRect.pivot = new Vector2(bubblePivotX, bubblePivotY);
        SetStretch(bubbleRect);
        Image bubbleImage = bubbleObject.GetComponent<Image>();
        bubbleImage.sprite = LoadSprite($"{ArtRoot}/{bubblePath}");
        bubbleImage.preserveAspect = true;
        bubbleImage.raycastTarget = false;

        TMP_Text bubbleLabel = CreateText(
            "Label", bubbleObject.transform, textTemplate, bubbleText,
            bubbleLabelPosition, new Vector2(360f, 54f), 24f, Color.black, TextAlignmentOptions.Center);

        GameObject bubbleHit = NewUi(objectName + "BubbleHit", bubbleParent,
            typeof(CanvasRenderer), typeof(Image), typeof(MainMenuHotspotPointerProxy));
        RectTransform bubbleHitRect = (RectTransform)bubbleHit.transform;
        SetCenteredRect(bubbleHitRect, bubbleLabelPosition, bubbleHitSize);
        Image bubbleHitImage = bubbleHit.GetComponent<Image>();
        bubbleHitImage.color = new Color(1f, 1f, 1f, 0.001f);
        bubbleHitImage.raycastTarget = true;

        SyncedOutlineFrameAnimator animator = container.GetComponent<SyncedOutlineFrameAnimator>();
        SetObject(animator, "bodyImage", body);
        SetObject(animator, "outlineImage", outline);
        SetSpriteList(animator, "bodyFrames", LoadFrameSet(bodyPrefix));
        SetSpriteList(animator, "outlineFrames", animatedOutline
            ? LoadFrameSet(outlinePathOrPrefix)
            : new List<Sprite> { LoadSprite($"{ArtRoot}/{outlinePathOrPrefix}") });
        SetFloat(animator, "fps", 12f);
        SetBool(animator, "loop", true);

        MainMenuObjectHotspot hotspot = container.GetComponent<MainMenuObjectHotspot>();
        hotspot.targetGraphic = hitImage;
        hotspot.transition = Selectable.Transition.None;
        SetObject(hotspot, "visualRoot", visualRect);
        SetObject(hotspot, "bodyImage", body);
        SetObject(hotspot, "fillImage", fill);
        SetObject(hotspot, "outlineImage", outline);
        SetObject(hotspot, "frameAnimator", animator);
        SetObject(hotspot, "bubbleButton", bubbleRect);
        SetObject(hotspot, "bubbleHitTarget", bubbleHit);
        SetObject(hotspot, "bubbleLabel", bubbleLabel);
        SetColor(hotspot, "selectedOutlineTint", new Color32(251, 255, 134, 255));
        SetColor(hotspot, "availableOutlineTint", new Color32(155, 171, 178, 255));
        SetBool(hotspot, "bubbleTypewriterEnabled", true);
        SetFloat(hotspot, "bubbleTypewriterCharactersPerSecond", 30f);
        SetFloat(hotspot, "bubbleTypewriterStartDelaySeconds", 0f);
        SetObject(bubbleHit.GetComponent<MainMenuHotspotPointerProxy>(), "hotspot", hotspot);

        bubbleObject.SetActive(false);
        bubbleHit.SetActive(false);

        return new ObjectAuthoring
        {
            name = objectName,
            hotspot = hotspot,
            animator = animator,
            visualRoot = visualRect,
            body = body,
            fill = fill,
            outline = outline,
            bubble = bubbleRect,
        };
    }

    private static void EnsureObjectFill(MainMenuObjectHotspot hotspot, string relativePath)
    {
        if (hotspot == null || hotspot.VisualRoot == null)
            throw new InvalidOperationException("Cannot wire an object fill without a hotspot VisualRoot.");

        Image fill = hotspot.FillImage;
        if (fill == null)
        {
            Transform existing = hotspot.VisualRoot.Find("Fill");
            fill = existing != null ? existing.GetComponent<Image>() : null;
        }

        if (fill == null)
        {
            GameObject fillObject = NewUi("Fill", hotspot.VisualRoot, typeof(CanvasRenderer), typeof(Image));
            SetStretch((RectTransform)fillObject.transform);
            fill = fillObject.GetComponent<Image>();
        }

        fill.sprite = LoadSprite($"{ArtRoot}/{relativePath}");
        fill.preserveAspect = true;
        fill.raycastTarget = false;
        fill.color = new Color(1f, 1f, 1f, 0f);
        fill.enabled = false;
        // Keep the temporary fill above the body but below the outline so
        // the silhouette tint never obscures the authored border.
        fill.transform.SetAsLastSibling();
        if (hotspot.OutlineImage != null && fill.transform.parent == hotspot.OutlineImage.transform.parent)
            hotspot.OutlineImage.transform.SetAsLastSibling();
        SetObject(hotspot, "fillImage", fill);
    }

    private static BonkDogPanelController CreateBonkPanel(
        RectTransform canvas, Button template, TMP_Text textTemplate, MainMenuSequenceController2 sequence)
    {
        GameObject root = CreatePanelRoot("GameMenuPanel", canvas, typeof(BonkDogPanelController));
        BonkDogPanelController controller = root.GetComponent<BonkDogPanelController>();
        CreateText("Title", root.transform, textTemplate, "GAME MENU", new Vector2(0f, 174f), new Vector2(600f, 52f), 34f, Color.white, TextAlignmentOptions.Center);
        TMP_Text status = CreateText("Status", root.transform, textTemplate, string.Empty, new Vector2(0f, 132f), new Vector2(720f, 36f), 17f, new Color(1f, .72f, .35f), TextAlignmentOptions.Center);

        GameObject newView = CreateVerticalView("NewGameView", root.transform, new Vector2(0f, -22f), new Vector2(520f, 270f), 14f);
        Button play = CloneButton(template, newView.transform, "NewGameButton", "PLAY GAME", Vector2.zero, new Vector2(430f, 66f));
        Button connect = CloneButton(template, newView.transform, "ConnectWalletButton", "CONNECT WALLET", Vector2.zero, new Vector2(430f, 66f));

        GameObject continueView = CreateVerticalView("ContinueView", root.transform, new Vector2(0f, -24f), new Vector2(650f, 300f), 12f);
        GameObject card = NewUi("ProfileSummary", continueView.transform, typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement), typeof(MainMenuProfileSummaryView));
        card.GetComponent<Image>().color = new Color(.03f, .025f, .055f, .92f);
        card.GetComponent<LayoutElement>().preferredHeight = 190f;
        TMP_Text heading = CreateText("Heading", card.transform, textTemplate, "PLAYER PROFILE", new Vector2(0f, 55f), new Vector2(560f, 44f), 28f, Color.white, TextAlignmentOptions.Center);
        TMP_Text body = CreateText("Body", card.transform, textTemplate, string.Empty, new Vector2(0f, 4f), new Vector2(560f, 82f), 18f, new Color(.9f, .9f, .95f), TextAlignmentOptions.TopLeft);
        TMP_Text stats = CreateText("Stats", card.transform, textTemplate, "READY TO CONTINUE", new Vector2(0f, -67f), new Vector2(560f, 34f), 16f, new Color(.55f, 1f, .75f), TextAlignmentOptions.Center);
        MainMenuProfileSummaryView summary = card.GetComponent<MainMenuProfileSummaryView>();
        SetObject(summary, "headingLabel", heading);
        SetObject(summary, "bodyLabel", body);
        SetObject(summary, "statusLabel", stats);
        Button continueButton = CloneButton(template, continueView.transform, "ContinueButton", "CONTINUE", Vector2.zero, new Vector2(430f, 66f));

        // Back/cancel is routed by MenuInputContextController and the HUD
        // input actions. Keep the panel's authored navigation strictly
        // vertical and do not add a duplicate visual back button.
        SetBasePanelFields(controller, root, null, new Selectable[] { play, connect, continueButton }, play);
        SetObject(controller, "newGameView", newView);
        SetObject(controller, "continueView", continueView);
        SetObject(controller, "profileSummaryView", summary);
        SetObject(controller, "statusLabel", status);
        SetObject(controller, "newGameButton", play);
        SetObject(controller, "connectWalletButton", connect);
        SetObject(controller, "continueButton", continueButton);
        SetObject(controller, "sequenceController", sequence);
        continueView.SetActive(false);
        return controller;
    }

    private static PCDeskPanelController CreatePcPanel(RectTransform canvas, Button template, TMP_Text textTemplate)
    {
        GameObject root = CreatePanelRoot("PCDeskPanel", canvas, typeof(PCDeskPanelController));
        PCDeskPanelController controller = root.GetComponent<PCDeskPanelController>();
        CreateText("Title", root.transform, textTemplate, "COLLECTION TERMINAL", new Vector2(0f, 170f), new Vector2(700f, 52f), 34f, Color.white, TextAlignmentOptions.Center);
        CreateText(
            "Body", root.transform, textTemplate,
            "Your Chainling collection will live here.\n\nWallet-linked profiles will also surface tradeable Core assets through the same terminal.",
            new Vector2(0f, 5f), new Vector2(700f, 235f), 23f, new Color(.9f, .9f, .96f), TextAlignmentOptions.Center);
        Button close = CloneButton(template, root.transform, "CloseButton", "BACK", new Vector2(0f, -175f), new Vector2(280f, 58f));
        SetBasePanelFields(controller, root, close, new Selectable[] { close }, close);
        return controller;
    }

    private static EventsPanelController CreateEventsPanel(
        RectTransform canvas, Button template, TMP_Text textTemplate, MainMenuEventsFeedSO feed)
    {
        GameObject root = CreatePanelRoot("EventsPanel", canvas, typeof(EventsPanelController));
        EventsPanelController controller = root.GetComponent<EventsPanelController>();
        TMP_Text title = CreateText("Title", root.transform, textTemplate, feed.title, new Vector2(0f, 170f), new Vector2(680f, 52f), 34f, Color.white, TextAlignmentOptions.Center);

        GameObject scrollObject = NewUi("Scroll", root.transform, typeof(CanvasRenderer), typeof(Image), typeof(ScrollRect));
        RectTransform scrollRectTransform = (RectTransform)scrollObject.transform;
        SetCenteredRect(scrollRectTransform, new Vector2(0f, 0f), new Vector2(720f, 260f));
        scrollObject.GetComponent<Image>().color = new Color(.03f, .025f, .055f, .9f);

        GameObject viewport = NewUi("Viewport", scrollObject.transform, typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
        SetStretch((RectTransform)viewport.transform, 18f);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, .001f);
        GameObject content = NewUi("Content", viewport.transform, typeof(ContentSizeFitter));
        RectTransform contentRect = (RectTransform)content.transform;
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text body = CreateText("Body", content.transform, textTemplate, feed.body, Vector2.zero, new Vector2(670f, 220f), 22f, new Color(.92f, .92f, .97f), TextAlignmentOptions.TopLeft);
        RectTransform bodyRect = (RectTransform)body.transform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(.5f, 1f);
        bodyRect.anchoredPosition = Vector2.zero;
        bodyRect.sizeDelta = new Vector2(0f, 220f);
        body.textWrappingMode = TextWrappingModes.Normal;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = (RectTransform)viewport.transform;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        Button close = CloneButton(template, root.transform, "CloseButton", "BACK", new Vector2(0f, -175f), new Vector2(280f, 58f));
        SetBasePanelFields(controller, root, close, new Selectable[] { close }, close);
        SetObject(controller, "feed", feed);
        SetObject(controller, "titleLabel", title);
        SetObject(controller, "bodyLabel", body);
        return controller;
    }

    private static GameObject CreatePanelRoot(string name, RectTransform canvas, Type controllerType)
    {
        GameObject root = NewUi(name, canvas, typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), controllerType);
        RectTransform rect = (RectTransform)root.transform;
        rect.anchorMin = new Vector2(.16f, .12f);
        rect.anchorMax = new Vector2(.84f, .88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = root.GetComponent<Image>();
        image.color = new Color(.075f, .045f, .09f, .985f);
        image.raycastTarget = true;
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        // Keep panels visible in the editor and prefab stages for authoring.
        // MainMenuPanelControllerBase.Awake() still hides them immediately at runtime.
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;
        root.SetActive(true);
        return root;
    }

    private static void SavePanelPrefab(Scene scene, string panelName, string prefabPath)
    {
        GameObject panel = FindSceneObject(scene, panelName);
        if (panel == null)
            throw new InvalidOperationException($"Cannot create {panelName} prefab: scene object is missing.");

        CanvasGroup group = panel.GetComponent<CanvasGroup>();
        if (group != null)
        {
            // Prefab and Scene views should show the authored panel. Runtime
            // lifecycle still calls HideImmediate before the menu is usable.
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
            EditorUtility.SetDirty(group);
        }

        panel.SetActive(true);
        PrefabUtility.SaveAsPrefabAssetAndConnect(panel, prefabPath, InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(panel);
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        int separator = folder.LastIndexOf('/');
        if (separator <= 0)
            return;

        string parent = folder.Substring(0, separator);
        string name = folder.Substring(separator + 1);
        EnsureAssetFolder(parent);
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static void SetBasePanelFields(
        MainMenuPanelControllerBase controller,
        GameObject root,
        Button close,
        Selectable[] order,
        Selectable defaultSelection)
    {
        SetObject(controller, "panelGroup", root.GetComponent<CanvasGroup>());
        SetObject(controller, "panelRect", root.GetComponent<RectTransform>());
        SetObject(controller, "closeButton", close);
        SetObjectList(controller, "navigationOrder", order);
        SetObject(controller, "defaultSelection", defaultSelection);
    }

    private static GameObject CreateVerticalView(string name, Transform parent, Vector2 position, Vector2 size, float spacing)
    {
        GameObject view = NewUi(name, parent, typeof(VerticalLayoutGroup));
        SetCenteredRect((RectTransform)view.transform, position, size);
        VerticalLayoutGroup layout = view.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return view;
    }

    private static Button CloneButton(Button template, Transform parent, string name, string label, Vector2 position, Vector2 size)
    {
        GameObject clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
        clone.name = name;
        clone.SetActive(true);
        RectTransform rect = clone.GetComponent<RectTransform>();
        SetCenteredRect(rect, position, size);
        TMP_Text text = clone.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
        LayoutElement element = clone.GetComponent<LayoutElement>();
        if (element == null)
            element = clone.AddComponent<LayoutElement>();
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;
        Button button = clone.GetComponent<Button>();
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        return button;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        TMP_Text template,
        string value,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = NewUi(name, parent, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        EditorUtility.CopySerialized(template, text);
        text.text = value;
        text.fontSize = fontSize;
        text.enableAutoSizing = false;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        SetCenteredRect((RectTransform)textObject.transform, position, size);
        return text;
    }

    private static Image CreateFullCanvasImage(string name, Transform parent, Sprite sprite)
    {
        GameObject imageObject = NewUi(name, parent, typeof(CanvasRenderer), typeof(Image));
        SetStretch((RectTransform)imageObject.transform);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        return image;
    }

    private static CanvasGroup ConfigureStaticLayer(Transform groupTransform, string spritePath)
    {
        if (groupTransform == null)
            throw new InvalidOperationException($"Missing static layer for {spritePath}.");

        CanvasGroup group = GetOrAdd<CanvasGroup>(groupTransform.gameObject);
        Image image = groupTransform.GetComponentInChildren<Image>(true);
        if (image == null)
            throw new InvalidOperationException($"{groupTransform.name} has no Image.");

        image.sprite = LoadSprite(spritePath);
        image.preserveAspect = true;
        image.raycastTarget = false;
        SetStretch(image.rectTransform);

        UIImageFrameAnimator oldAnimator = image.GetComponent<UIImageFrameAnimator>();
        if (oldAnimator != null)
            UnityEngine.Object.DestroyImmediate(oldAnimator, true);
        return group;
    }

    private static void WireNavigation(
        MainMenuObjectHotspot pc,
        MainMenuObjectHotspot bonk,
        MainMenuObjectHotspot xmas,
        MainMenuObjectHotspot vinyl)
    {
        pc.navigation = Explicit(null, vinyl, null, bonk);
        bonk.navigation = Explicit(null, vinyl, pc, xmas);
        xmas.navigation = Explicit(null, vinyl, bonk, null);
        vinyl.navigation = Explicit(bonk, null, pc, xmas);
    }

    private static Navigation Explicit(Selectable up, Selectable down, Selectable left, Selectable right)
    {
        return new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right,
        };
    }

    private static void WireInputModule(InputSystemUIInputModule module)
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (module == null || asset == null)
            throw new InvalidOperationException("InputSystemUIInputModule or InputSystem_Actions is missing.");

        module.actionsAsset = asset;
        module.move = EnsureActionReference(asset, "Navigate");
        module.submit = EnsureActionReference(asset, "Submit");
        module.cancel = EnsureActionReference(asset, "Cancel");
        EditorUtility.SetDirty(module);
    }

    private static InputActionReference EnsureActionReference(InputActionAsset asset, string actionName)
    {
        const string folder = "Assets/Data/UI/InputActionReferences";
        EnsureFolder(folder);
        string path = $"{folder}/UI_{actionName}.asset";
        InputActionReference reference = AssetDatabase.LoadAssetAtPath<InputActionReference>(path);
        InputAction action = asset.FindActionMap("UI", true).FindAction(actionName, true);
        if (reference == null)
        {
            reference = InputActionReference.Create(action);
            reference.name = "UI_" + actionName;
            AssetDatabase.CreateAsset(reference, path);
        }
        else
        {
            reference.Set(action);
            reference.name = "UI_" + actionName;
            EditorUtility.SetDirty(reference);
        }

        return reference;
    }

    private static void WireStageBinder(
        SpriteCinematicStageBinder binder,
        ObjectAuthoring pc,
        ObjectAuthoring bonk,
        ObjectAuthoring xmas,
        ObjectAuthoring vinyl,
        GameObject objectsRoot,
        GameObject foregroundRoot,
        GameObject bubblesRoot,
        CanvasGroup mobileHudRoot,
        BonkDogPanelController bonkPanel,
        PCDeskPanelController pcPanel,
        EventsPanelController eventsPanel,
        SettingsPanelController settingsPanel,
        GameObject backgroundRoot)
    {
        SetObjectArray(binder, "stageAnimatorsToStop", pc.animator, bonk.animator, xmas.animator, vinyl.animator);
        SetObjectArray(binder, "stageRootsToHide",
            objectsRoot, foregroundRoot, bubblesRoot, mobileHudRoot != null ? mobileHudRoot.gameObject : null, backgroundRoot);

        var chrome = new List<CanvasGroup>();
        SerializedObject so = new SerializedObject(binder);
        SerializedProperty existing = so.FindProperty("chromeGroupsToHide");
        if (existing != null)
        {
            for (int i = 0; i < existing.arraySize; i++)
            {
                CanvasGroup group = existing.GetArrayElementAtIndex(i).objectReferenceValue as CanvasGroup;
                if (group != null && !chrome.Contains(group))
                    chrome.Add(group);
            }
        }

        AddUnique(chrome, bubblesRoot.GetComponent<CanvasGroup>());
        AddUnique(chrome, mobileHudRoot);
        AddUnique(chrome, bonkPanel.GetComponent<CanvasGroup>());
        AddUnique(chrome, pcPanel.GetComponent<CanvasGroup>());
        AddUnique(chrome, eventsPanel.GetComponent<CanvasGroup>());
        if (settingsPanel != null)
        {
            SerializedObject settingsSo = new SerializedObject(settingsPanel);
            AddUnique(chrome, settingsSo.FindProperty("panelGroup")?.objectReferenceValue as CanvasGroup);
        }
        SetObjectArray(binder, "chromeGroupsToHide", chrome.ToArray());
    }

    private static void ApplySiblingOrder(
        RectTransform canvas,
        Transform objects,
        Transform foregroundMotion,
        Transform bubbles,
        Transform bonkPanel,
        Transform pcPanel,
        Transform eventsPanel)
    {
        var order = new List<Transform>
        {
            canvas.Find("Background"),
            objects,
            canvas.Find("PhotoMiddle"),
            foregroundMotion,
            bubbles,
            canvas.Find("ContrastOverlay"),
            canvas.Find("GameLogo"),
            canvas.Find("ConnectSettingsGroup"),
            canvas.Find("PlayButtonGroup"),
            bonkPanel,
            pcPanel,
            eventsPanel,
            canvas.Find("SettingsPanel"),
            canvas.Find("ControlPanel"),
            canvas.Find("WalletConnectionPrompt"),
            canvas.Find("SplashGroup"),
        };

        int index = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] != null)
                order[i].SetSiblingIndex(index++);
        }
    }

    private static MainMenuEventsFeedSO EnsureEventsFeed()
    {
        EnsureFolder("Assets/Data/UI");
        MainMenuEventsFeedSO feed = AssetDatabase.LoadAssetAtPath<MainMenuEventsFeedSO>(EventsFeedPath);
        if (feed == null)
        {
            feed = ScriptableObject.CreateInstance<MainMenuEventsFeedSO>();
            feed.title = "SIGNAL JAM EVENTS";
            feed.body =
                "LIVE SIGNAL\n\n" +
                "Seasonal drops, community challenges, and special Chainling encounters will appear here.\n\n" +
                "The terminal is online. Check back when a new signal arrives.";
            AssetDatabase.CreateAsset(feed, EventsFeedPath);
        }

        return feed;
    }

    private static void EnsureMobileHudRootCanvasGroup()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(MobileServicesPrefabPath);
        try
        {
            Transform canvas = FindDescendant(prefabRoot.transform, "MobileHudCanvas");
            if (canvas == null)
                throw new InvalidOperationException("MobileServices.prefab has no MobileHudCanvas.");

            CanvasGroup group = canvas.GetComponent<CanvasGroup>();
            if (group == null)
                group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, MobileServicesPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static void ValidateAnimator(
        MainMenuObjectHotspot hotspot,
        int bodyCount,
        int outlineCount,
        string bodyPrefix,
        string outlinePrefixOrPath,
        bool animatedOutline,
        List<string> failures)
    {
        if (hotspot == null || hotspot.FrameAnimator == null)
        {
            failures.Add("A hotspot or synchronized animator is missing.");
            return;
        }

        if (hotspot.FrameAnimator.SerializedBodyFrameCount != bodyCount)
            failures.Add($"{hotspot.name} body frame count is {hotspot.FrameAnimator.SerializedBodyFrameCount}, expected {bodyCount}.");
        if (hotspot.FrameAnimator.SerializedOutlineFrameCount != outlineCount)
            failures.Add($"{hotspot.name} outline frame count is {hotspot.FrameAnimator.SerializedOutlineFrameCount}, expected {outlineCount}.");
        if (hotspot.BodyImage == null || hotspot.BodyImage.sprite == null ||
            hotspot.FillImage == null || hotspot.FillImage.sprite == null ||
            hotspot.OutlineImage == null || hotspot.OutlineImage.sprite == null)
        {
            failures.Add($"{hotspot.name} has an unwired body/fill/outline Image sprite.");
        }

        SerializedObject hotspotSo = new SerializedObject(hotspot);
        RequireReference(hotspotSo, "bubbleLabel", failures);
        SerializedProperty typewriterCps = hotspotSo.FindProperty("bubbleTypewriterCharactersPerSecond");
        SerializedProperty typewriterDelay = hotspotSo.FindProperty("bubbleTypewriterStartDelaySeconds");
        if (typewriterCps == null || typewriterCps.floatValue <= 0f)
            failures.Add($"{hotspot.name} bubble typewriter characters-per-second must be greater than zero.");
        if (typewriterDelay == null || typewriterDelay.floatValue < 0f)
            failures.Add($"{hotspot.name} bubble typewriter start delay cannot be negative.");

        SerializedObject animatorSo = new SerializedObject(hotspot.FrameAnimator);
        ValidateFrameSequence(animatorSo.FindProperty("bodyFrames"), bodyCount, bodyPrefix, true, hotspot.name, failures);
        ValidateFrameSequence(animatorSo.FindProperty("outlineFrames"), outlineCount, outlinePrefixOrPath, animatedOutline, hotspot.name, failures);
    }

    private static void ValidateOutlinePulse(
        MainMenuObjectNavigator navigator,
        MainMenuObjectHotspot[] hotspots,
        List<string> failures)
    {
        if (navigator == null)
        {
            failures.Add("Objects/MainMenuObjectNavigator is missing.");
            return;
        }

        SerializedObject navigatorSo = new SerializedObject(navigator);
        SerializedProperty minAlpha = navigatorSo.FindProperty("availabilityMinAlpha");
        SerializedProperty maxAlpha = navigatorSo.FindProperty("availabilityMaxAlpha");
        SerializedProperty pulseSeconds = navigatorSo.FindProperty("availabilityPulseSeconds");
        if (minAlpha == null || maxAlpha == null || pulseSeconds == null)
        {
            failures.Add("MainMenuObjectNavigator availability outline pulse settings are missing.");
        }
        else
        {
            if (minAlpha.floatValue < 0f || minAlpha.floatValue > 1f)
                failures.Add("MainMenuObjectNavigator availabilityMinAlpha must be between 0 and 1.");
            if (maxAlpha.floatValue < minAlpha.floatValue || maxAlpha.floatValue > 1f)
                failures.Add("MainMenuObjectNavigator availabilityMaxAlpha must be >= min alpha and <= 1.");
            if (pulseSeconds.floatValue <= 0f)
                failures.Add("MainMenuObjectNavigator availabilityPulseSeconds must be greater than zero.");
        }

        for (int i = 0; hotspots != null && i < hotspots.Length; i++)
        {
            if (hotspots[i] == null || hotspots[i].OutlineImage == null)
                continue;
            if (!hotspots[i].OutlineImage.enabled)
                failures.Add($"{hotspots[i].name} outline Image must be enabled for availability feedback.");
        }
    }

    private static void ValidateFrameSequence(
        SerializedProperty frames,
        int expectedCount,
        string prefixOrPath,
        bool numbered,
        string owner,
        List<string> failures)
    {
        if (frames == null || !frames.isArray || frames.arraySize != expectedCount)
            return;

        for (int i = 0; i < expectedCount; i++)
        {
            UnityEngine.Object frame = frames.GetArrayElementAtIndex(i).objectReferenceValue;
            string actualPath = frame != null ? AssetDatabase.GetAssetPath(frame) : string.Empty;
            string expectedPath = numbered
                ? $"{ArtRoot}/{prefixOrPath}{i + 1}.png"
                : $"{ArtRoot}/{prefixOrPath}";
            if (string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
                continue;

            failures.Add($"{owner} frame {i + 1} is '{actualPath}', expected '{expectedPath}'.");
        }
    }

    private static void ValidateDirection(
        MainMenuObjectHotspot from, string direction, MainMenuObjectHotspot expected, List<string> failures)
    {
        if (from == null)
            return;
        Navigation nav = from.navigation;
        Selectable actual = direction == "up" ? nav.selectOnUp :
            direction == "down" ? nav.selectOnDown :
            direction == "left" ? nav.selectOnLeft : nav.selectOnRight;
        if (actual != expected)
            failures.Add($"{from.name}.{direction} does not point to {(expected != null ? expected.name : "null")}.");
    }

    private static void ValidatePersistentAction(
        MainMenuObjectHotspot hotspot,
        MainMenuSequenceController2 expectedTarget,
        string expectedMethod,
        List<string> failures)
    {
        if (hotspot == null || expectedTarget == null)
            return;

        UnityEvent confirmed = hotspot.OnConfirmed;
        if (confirmed == null || confirmed.GetPersistentEventCount() != 1 ||
            confirmed.GetPersistentTarget(0) != expectedTarget ||
            !string.Equals(confirmed.GetPersistentMethodName(0), expectedMethod, StringComparison.Ordinal))
        {
            failures.Add($"{hotspot.name} confirm does not invoke {expectedMethod} exactly once.");
        }
    }

    private static void ValidatePanel(
        MainMenuPanelControllerBase panel,
        int expectedNavigationCount,
        IReadOnlyList<string> requiredFields,
        List<string> failures,
        bool requireCloseButton = true)
    {
        if (panel == null)
        {
            failures.Add("A required object panel is missing.");
            return;
        }

        SerializedObject panelSo = new SerializedObject(panel);
        RequireReference(panelSo, "panelGroup", failures);
        RequireReference(panelSo, "panelRect", failures);
        if (requireCloseButton)
            RequireReference(panelSo, "closeButton", failures);
        RequireReference(panelSo, "defaultSelection", failures);

        SerializedProperty navigationOrder = panelSo.FindProperty("navigationOrder");
        if (navigationOrder == null || navigationOrder.arraySize != expectedNavigationCount)
            failures.Add($"{panel.name} navigation order has {navigationOrder?.arraySize ?? 0} entries, expected {expectedNavigationCount}.");
        else
        {
            for (int i = 0; i < navigationOrder.arraySize; i++)
            {
                if (navigationOrder.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    failures.Add($"{panel.name} navigation entry {i} is not wired.");
            }
        }

        for (int i = 0; i < requiredFields.Count; i++)
            RequireReference(panelSo, requiredFields[i], failures);
    }

    private static void ValidatePanelPrefab(
        MainMenuPanelControllerBase panel,
        string prefabPath,
        List<string> failures)
    {
        if (panel == null)
            return;

        if (!PrefabUtility.IsPartOfPrefabInstance(panel.gameObject))
            failures.Add($"{panel.name} is not connected to its authoring prefab.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            failures.Add($"Panel prefab is missing at {prefabPath}.");
    }

    private static void RequireReference(SerializedObject owner, string propertyName, List<string> failures)
    {
        SerializedProperty property = owner?.FindProperty(propertyName);
        if (property == null || property.objectReferenceValue == null)
            failures.Add($"{owner?.targetObject?.name ?? "Unknown"}.{propertyName} is not wired.");
    }

    private static void RequirePositiveFloat(SerializedObject owner, string propertyName, List<string> failures)
    {
        SerializedProperty property = owner?.FindProperty(propertyName);
        if (property == null || property.floatValue <= 0f)
            failures.Add($"{owner?.targetObject?.name ?? "Unknown"}.{propertyName} must be greater than zero.");
    }

    private static void ValidateInputActions(InputSystemUIInputModule inputModule, List<string> failures)
    {
        InputActionAsset asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        InputActionMap ui = asset != null ? asset.FindActionMap("UI", false) : null;
        if (ui == null)
        {
            failures.Add("InputSystem_Actions has no UI map.");
            return;
        }

        RequireBinding(ui.FindAction("Navigate"), "<Keyboard>/upArrow", failures);
        RequireBinding(ui.FindAction("Navigate"), "<Keyboard>/w", failures);
        RequireBinding(ui.FindAction("Navigate"), "<Gamepad>/dpad/up", failures);
        RequireBinding(ui.FindAction("Navigate"), "<PSG1>/dpad/up", failures);
        RequireBinding(ui.FindAction("Submit"), "<Keyboard>/enter", failures);
        RequireBinding(ui.FindAction("Submit"), "<Gamepad>/buttonSouth", failures);
        RequireBinding(ui.FindAction("Cancel"), "<Keyboard>/escape", failures);
        RequireBinding(ui.FindAction("Cancel"), "<Gamepad>/buttonEast", failures);

        if (inputModule == null)
        {
            failures.Add("InputSystemUIInputModule is missing from the authored EventSystem.");
            return;
        }

        if (inputModule.actionsAsset != asset)
            failures.Add("InputSystemUIInputModule does not use InputSystem_Actions.");
        ValidateActionReference(inputModule.move, ui.FindAction("Navigate"), "Navigate", failures);
        ValidateActionReference(inputModule.submit, ui.FindAction("Submit"), "Submit", failures);
        ValidateActionReference(inputModule.cancel, ui.FindAction("Cancel"), "Cancel", failures);
    }

    private static void ValidateActionReference(
        InputActionReference reference,
        InputAction expected,
        string label,
        List<string> failures)
    {
        if (reference == null || reference.action == null || reference.action.id != expected.id)
            failures.Add($"InputSystemUIInputModule.{label} action reference is not wired to UI/{label}.");
    }

    private static void RequireBinding(InputAction action, string path, List<string> failures)
    {
        if (action == null)
        {
            failures.Add($"Missing action for required binding {path}.");
            return;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (string.Equals(action.bindings[i].path, path, StringComparison.OrdinalIgnoreCase))
                return;
        }
        failures.Add($"{action.actionMap.name}/{action.name} is missing {path}.");
    }

    private static void ValidateHudPrefab(List<string> failures)
    {
        GameObject mobileRoot = PrefabUtility.LoadPrefabContents(MobileServicesPrefabPath);
        try
        {
            Transform canvas = FindDescendant(mobileRoot.transform, "MobileHudCanvas");
            if (canvas == null || canvas.GetComponent<CanvasGroup>() == null)
                failures.Add("MobileHudCanvas has no root CanvasGroup for the reveal fade.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(mobileRoot);
        }

        string gameServicesText = File.ReadAllText(Path.GetFullPath("Assets/Prefabs/GameServices/GameServices.prefab"));
        if (!gameServicesText.Contains("propertyPath: sceneRules.Array.data[0].context\n      value: 4") &&
            !gameServicesText.Contains("propertyPath: sceneRules.Array.data[0].context\r\n      value: 4"))
        {
            failures.Add("GameServices main_menu HUD policy is not MobileHudContext.Menu (4).");
        }
    }

    private static List<Sprite> LoadFrameSet(string relativePrefix)
    {
        var frames = new List<Sprite>(32);
        for (int i = 1; i <= 32; i++)
            frames.Add(LoadSprite($"{ArtRoot}/{relativePrefix}{i}.png"));
        return frames;
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException("Missing sprite: " + path);
        return sprite;
    }

    private static GameObject NewUi(string name, Transform parent, params Type[] extraComponents)
    {
        var components = new List<Type> { typeof(RectTransform) };
        if (extraComponents != null)
            components.AddRange(extraComponents);
        GameObject gameObject = new GameObject(name, components.ToArray());
        gameObject.layer = 5;
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void SetStretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    private static void SetCenteredRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void DeleteChild(RectTransform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
            UnityEngine.Object.DestroyImmediate(child.gameObject);
    }

    private static T GetOrAdd<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        Transform result = FindSceneTransform(scene, name);
        return result != null ? result.gameObject : null;
    }

    private static Transform FindSceneTransform(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform result = FindDescendant(roots[i].transform, name);
            if (result != null)
                return result;
        }
        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendant(root.GetChild(i), name);
            if (result != null)
                return result;
        }
        return null;
    }

    private static void EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static void ClearPersistentListeners(UnityEngine.Events.UnityEvent unityEvent)
    {
        while (unityEvent.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(unityEvent, unityEvent.GetPersistentEventCount() - 1);
    }

    private static void AddUnique(List<CanvasGroup> groups, CanvasGroup group)
    {
        if (group != null && !groups.Contains(group))
            groups.Add(group);
    }

    private static void SetObject(UnityEngine.Object target, string field, UnityEngine.Object value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetBool(UnityEngine.Object target, string field, bool value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
        property.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetFloat(UnityEngine.Object target, string field, float value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
        property.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetColor(UnityEngine.Object target, string field, Color value)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
        property.colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetObjectList(UnityEngine.Object target, string field, params UnityEngine.Object[] values)
    {
        SetObjectArray(target, field, values);
    }

    private static void SetObjectArray(UnityEngine.Object target, string field, params UnityEngine.Object[] values)
    {
        SerializedObject so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new InvalidOperationException($"{target.GetType().Name}.{field} is not serialized.");
        property.arraySize = values != null ? values.Length : 0;
        for (int i = 0; values != null && i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetSpriteList(UnityEngine.Object target, string field, List<Sprite> sprites)
    {
        UnityEngine.Object[] values = new UnityEngine.Object[sprites.Count];
        for (int i = 0; i < sprites.Count; i++)
            values[i] = sprites[i];
        SetObjectArray(target, field, values);
    }
}
#endif
