using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;

namespace Chainbound.Combat
{
    [DefaultExecutionOrder(-900)]
    public class CombatBattleUGUIInstaller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatBattleController battleController;
        [SerializeField] private CombatBattleUGUIController uguiController;
        [SerializeField] private GameObject combatBattleCanvasPrefab;
        [SerializeField] private CombatActionSelectorController actionSelector;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private CombatSelectionCoordinatorUGUI selectionCoordinator;
        [SerializeField] private Combat2BattlefieldPresenter battlefieldPresenter;
        [SerializeField] private MoveDatabaseSO moveDatabase;

        [Header("Selector Bridge")]
        [SerializeField] private bool disablePlayerMapWhileSelecting;

        [Header("HUD Card Sprites")]
        [SerializeField] private Sprite hudCardNormalSprite;
        [SerializeField] private Sprite hudCardActiveSprite;
        [SerializeField] private Sprite hudCardTargetSprite;
        [SerializeField] private Sprite hudCardActiveTargetSprite;

        [Header("HUD Card HP Frame Overlay (shadows/highlights per state)")]
        [SerializeField] private Sprite hpFrameOverlayNormalSprite;
        [SerializeField] private Sprite hpFrameOverlayActiveSprite;
        [SerializeField] private Sprite hpFrameOverlayTargetSprite;
        [SerializeField] private Sprite hpFrameOverlayActiveTargetSprite;

        [Header("Move UI Icons")]
        [SerializeField] private TypeIconPaletteSO moveTypeIconPalette;
        [SerializeField] private Sprite moveTypeFallbackIcon;

        [Header("Diagnostics")]
        [SerializeField] private bool enableSelectorDiagnostics = true;
        [SerializeField] private bool logSelectorInputEvents = true;

        [Header("Canvas Provisioning")]
        [SerializeField] private bool useSceneCanvasFirst = true;
        [SerializeField] private bool instantiateCanvasPrefabIfMissing;
        [SerializeField] private bool buildRuntimeCanvasIfMissing;
        [SerializeField] private bool preserveSceneHudLayout = true;

        [Header("Migration")]
        [SerializeField] private string runtimeCanvasName = "CombatBattleCanvas";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachInstaller()
        {
            var controllers = FindObjectsOfType<CombatBattleController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null)
                    continue;
                if (controller.GetComponent<CombatBattleUGUIInstaller>() == null)
                    controller.gameObject.AddComponent<CombatBattleUGUIInstaller>();
            }
        }

        private void Awake()
        {
            if (battleController == null)
                battleController = GetComponent<CombatBattleController>();
            if (battleController == null)
                battleController = FindObjectOfType<CombatBattleController>();

            if (actionSelector == null)
                actionSelector = FindObjectOfType<CombatActionSelectorController>();
            if (battlefieldPresenter == null)
                battlefieldPresenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);
            if (inputActions == null || !HasCombatBindings(inputActions))
                inputActions = ResolveInputActions();
            if (moveDatabase == null && battleController != null)
                moveDatabase = battleController.MoveDatabase;

            if (enableSelectorDiagnostics)
                Debug.Log($"[CombatBattleUGUIInstaller] Awake(controller='{battleController?.name}', selector='{actionSelector?.name}', inputAsset='{inputActions?.name}').");

            ConfigureUiInputModule();

            if (preserveSceneHudLayout)
                ApplyLayoutPreservationEarly();
        }

        private void ApplyLayoutPreservationEarly()
        {
            var controller = uguiController != null ? uguiController : FindObjectOfType<CombatBattleUGUIController>(true);
            if (controller != null)
                controller.SetLayoutEnforcement(false);
        }

        private void Start()
        {
            InstallIfNeeded();
        }

        private void InstallIfNeeded()
        {
            EnsurePlayerInputEnabled();

            if (uguiController == null)
                uguiController = ResolveOrCreateCanvasController();

            if (uguiController != null)
            {
                if (preserveSceneHudLayout)
                    uguiController.SetLayoutEnforcement(false);
                uguiController.SetTypeIconPalette(moveTypeIconPalette, moveTypeFallbackIcon);
                if (battlefieldPresenter != null)
                    uguiController.SetVisualIdentityDatabase(battlefieldPresenter.VisualIdentityDatabase);
                ConfigureHudCardSprites();
                uguiController.Initialize(battleController);
                uguiController.SetBottomActionPanelVisible(false);
            }
            else
            {
                Debug.LogError("[CombatBattleUGUIInstaller] No CombatBattleUGUIController could be resolved. Assign a scene canvas controller or enable prefab/runtime creation fallback.");
            }

            EnsureSelectionCoordinator();
            WireSelectorBridge();

            if (battleController != null && battleController.UsePlayerInput && !battleController.AutoRunTurnsEnabled)
            {
                Debug.LogWarning("[CombatBattleUGUIInstaller] AutoRunTurns is disabled. Call CombatBattleController.ExecuteNextTurn() to trigger OnPlayerTurnWaiting and unlock wallet input.");
            }
        }

        private CombatBattleUGUIController ResolveOrCreateCanvasController()
        {
            if (uguiController != null)
                return uguiController;

            if (useSceneCanvasFirst)
                uguiController = FindObjectOfType<CombatBattleUGUIController>(true);
            if (uguiController != null)
                return uguiController;

            if (instantiateCanvasPrefabIfMissing && combatBattleCanvasPrefab != null)
            {
                var instance = Instantiate(combatBattleCanvasPrefab);
                var fromPrefab = instance.GetComponentInChildren<CombatBattleUGUIController>(true);
                if (fromPrefab != null)
                    return fromPrefab;
            }

            if (buildRuntimeCanvasIfMissing)
                return BuildRuntimeCanvas();

            if (enableSelectorDiagnostics)
                Debug.LogWarning("[CombatBattleUGUIInstaller] Canvas spawning is disabled (prefab/runtime fallback off). Expecting a CombatBattleUGUIController already present in scene.");
            return null;
        }

        private CombatBattleUGUIController BuildRuntimeCanvas()
        {
            var rootGo = new GameObject(
                runtimeCanvasName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CombatBattleUGUIController));
            var rootRect = rootGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var canvas = rootGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var scaler = rootGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1240f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = rootGo.GetComponent<GraphicRaycaster>();
            raycaster.enabled = false;

            var safeAreaRoot = CreateRectChild(rootRect, "SafeAreaRoot");
            safeAreaRoot.gameObject.AddComponent<SafeAreaAdapter>();
            Stretch(safeAreaRoot, 0f, 0f, 1f, 1f);

            var hudRoot = CreateRectChild(safeAreaRoot, "HudRoot");
            Stretch(hudRoot, 0f, 0f, 1f, 1f);

            var playerPanel = CreateRectChild(hudRoot, "PlayerHudPanel");
            Stretch(playerPanel, 0.02f, 0.72f, 0.47f, 0.94f);

            var playerSlotA = CreateHudCard(playerPanel, "PlayerSlotA_HudCard");
            var playerSlotB = CreateHudCard(playerPanel, "PlayerSlotB_HudCard");

            var enemyPanel = CreateRectChild(hudRoot, "EnemyHudPanel");
            Stretch(enemyPanel, 0.53f, 0.72f, 0.98f, 0.94f);

            var enemySlotA = CreateHudCard(enemyPanel, "EnemySlotA_HudCard");
            var enemySlotB = CreateHudCard(enemyPanel, "EnemySlotB_HudCard");

            var turnRibbon = CreatePanel(hudRoot, "TurnOrderRibbon", new Color(0f, 0f, 0f, 0.35f));
            Stretch(turnRibbon, 0.37f, 0.88f, 0.63f, 0.95f);

            var targetIndicatorsRoot = CreateRectChild(hudRoot, "TargetIndicatorsRoot");
            Stretch(targetIndicatorsRoot, 0f, 0f, 1f, 1f);
            var activeTurnOutline = CreatePanel(targetIndicatorsRoot, "ActiveTurnOutline", new Color(1f, 0.95f, 0.2f, 0.12f));
            Stretch(activeTurnOutline, 0f, 0f, 1f, 1f);
            activeTurnOutline.gameObject.SetActive(false);
            var targetOutline = CreatePanel(targetIndicatorsRoot, "TargetOutline", new Color(1f, 0.2f, 0.2f, 0.10f));
            Stretch(targetOutline, 0f, 0f, 1f, 1f);
            targetOutline.gameObject.SetActive(false);

            var bottomPanel = CreatePanel(hudRoot, "BottomActionPanel", new Color(0.05f, 0.08f, 0.11f, 0.65f));
            Stretch(bottomPanel, 0.21f, 0.03f, 0.79f, 0.21f);
            bottomPanel.gameObject.SetActive(false);
            EnsureRuntimeSelectionPanels(bottomPanel, hudRoot);

            var controller = rootGo.GetComponent<CombatBattleUGUIController>();
            controller.SetRuntimeReferences(
                playerPanel,
                enemyPanel,
                turnRibbon,
                targetIndicatorsRoot,
                bottomPanel,
                playerSlotA,
                playerSlotB,
                enemySlotA,
                enemySlotB);

            return controller;
        }

        private void EnsureRuntimeSelectionPanels(RectTransform bottomPanel, RectTransform hudRoot)
        {
            if (bottomPanel == null)
                return;

            var movePanel = CreateRectChild(bottomPanel, "MoveSelectPanel");
            Stretch(movePanel, 0f, 0f, 1f, 1f);
            movePanel.gameObject.AddComponent<CombatMoveSelectPanelUGUI>();

            var swapPanel = CreateRectChild(bottomPanel, "SwapSelectPanel");
            Stretch(swapPanel, 0f, 0f, 1f, 1f);
            swapPanel.gameObject.AddComponent<CombatSwapSelectPanelUGUI>();

            var infoRoot = CreateRectChild(hudRoot != null ? hudRoot : bottomPanel, "MoveInfoPanel");
            Stretch(infoRoot, 0f, 0f, 1f, 1f);
            infoRoot.gameObject.AddComponent<CombatMoveInfoPanelUGUI>();
            infoRoot.gameObject.SetActive(false);
        }

        private void EnsurePlayerInputEnabled()
        {
            if (battleController == null)
                return;

            if (battleController.UsePlayerInput)
            {
                if (enableSelectorDiagnostics)
                    Debug.Log("[CombatBattleUGUIInstaller] CombatBattleController.usePlayerInput already enabled.");
                return;
            }

            battleController.SetUsePlayerInput(true);
            Debug.Log("[CombatBattleUGUIInstaller] Enabled CombatBattleController.usePlayerInput for 3D selector action entry.");
        }

        private void ConfigureHudCardSprites()
        {
            if (uguiController == null)
                return;

            var cards = uguiController.GetComponentsInChildren<HudCardView>(true);
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null)
                    continue;
                card.ConfigureBackgroundSprites(
                    hudCardNormalSprite,
                    hudCardActiveSprite,
                    hudCardTargetSprite,
                    hudCardActiveTargetSprite);
                card.ConfigureHpFrameOverlaySprites(
                    hpFrameOverlayNormalSprite,
                    hpFrameOverlayActiveSprite,
                    hpFrameOverlayTargetSprite,
                    hpFrameOverlayActiveTargetSprite);
            }
        }

        private void EnsureSelectionCoordinator()
        {
            if (battleController == null)
                return;

            if (selectionCoordinator == null)
                selectionCoordinator = battleController.GetComponent<CombatSelectionCoordinatorUGUI>();
            if (selectionCoordinator == null)
                selectionCoordinator = battleController.gameObject.AddComponent<CombatSelectionCoordinatorUGUI>();

            var movePanel = uguiController != null
                ? uguiController.GetComponentInChildren<CombatMoveSelectPanelUGUI>(true)
                : FindObjectOfType<CombatMoveSelectPanelUGUI>(true);
            var swapPanel = uguiController != null
                ? uguiController.GetComponentInChildren<CombatSwapSelectPanelUGUI>(true)
                : FindObjectOfType<CombatSwapSelectPanelUGUI>(true);
            var infoPanel = uguiController != null
                ? uguiController.GetComponentInChildren<CombatMoveInfoPanelUGUI>(true)
                : FindObjectOfType<CombatMoveInfoPanelUGUI>(true);

            var iconPalette = moveTypeIconPalette != null
                ? moveTypeIconPalette
                : uguiController != null ? uguiController.TypeIconPalette : null;
            var iconFallback = moveTypeFallbackIcon != null
                ? moveTypeFallbackIcon
                : uguiController != null ? uguiController.FallbackTypeIcon : null;

            selectionCoordinator.Configure(
                battleController,
                uguiController,
                movePanel,
                swapPanel,
                infoPanel,
                moveDatabase,
                iconPalette,
                iconFallback,
                enableSelectorDiagnostics);
        }

        private void WireSelectorBridge()
        {
            if (battleController == null)
            {
                Debug.LogError("[CombatBattleUGUIInstaller] Cannot wire selector bridge: CombatBattleController reference is missing.");
                return;
            }

            if (actionSelector == null)
                actionSelector = FindObjectOfType<CombatActionSelectorController>();
            if (actionSelector == null)
            {
                Debug.LogError("[CombatBattleUGUIInstaller] Cannot wire selector bridge: CombatActionSelectorController was not found in scene.");
                return;
            }
            if (!actionSelector.gameObject.activeSelf)
            {
                actionSelector.gameObject.SetActive(true);
                Debug.Log("[CombatBattleUGUIInstaller] Activated CombatActionSelectorController GameObject.");
            }

            if (!actionSelector.enabled)
            {
                actionSelector.enabled = true;
                Debug.Log("[CombatBattleUGUIInstaller] Enabled CombatActionSelectorController component.");
            }

            if (!actionSelector.isActiveAndEnabled)
            {
                Debug.LogWarning("[CombatBattleUGUIInstaller] CombatActionSelectorController is still not active and enabled. Check parent hierarchy active state.");
            }

            if (inputActions == null)
                inputActions = ResolveInputActions();
            if (inputActions == null)
                Debug.LogWarning("[CombatBattleUGUIInstaller] InputActionAsset not found. Bridge will fallback to auto-resolution and may run in fallback mode.");

            var bridge = battleController.GetComponent<CombatActionSelectorBridge>();
            if (bridge == null)
                bridge = battleController.gameObject.AddComponent<CombatActionSelectorBridge>();
            if (!bridge.enabled)
                bridge.enabled = true;

            bridge.SetVerboseLogging(enableSelectorDiagnostics, logSelectorInputEvents);
            actionSelector.SetVerboseLogging(enableSelectorDiagnostics);
            bridge.SetSelectionCoordinator(selectionCoordinator);
            bridge.SetSelectionFeedbackTargets(uguiController, battlefieldPresenter);

            DisableDuplicateSelectorBridges(bridge);

            bridge.Configure(
                battleController,
                actionSelector,
                inputActions,
                disablePlayerMapWhileSelectingOption: disablePlayerMapWhileSelecting);

            if (enableSelectorDiagnostics)
                Debug.Log("[CombatBattleUGUIInstaller] Selector bridge wiring complete.");
        }

        private static void DisableDuplicateSelectorBridges(CombatActionSelectorBridge keepBridge)
        {
            if (keepBridge == null)
                return;

            var allBridges = FindObjectsOfType<CombatActionSelectorBridge>(true);
            for (int i = 0; i < allBridges.Length; i++)
            {
                var candidate = allBridges[i];
                if (candidate == null || candidate == keepBridge)
                    continue;

                if (candidate.enabled)
                {
                    candidate.enabled = false;
                    Debug.LogWarning($"[CombatBattleUGUIInstaller] Disabled duplicate CombatActionSelectorBridge on '{candidate.gameObject.name}'. Keep bridge only on BattleController.");
                }
            }
        }

        private static InputActionAsset ResolveInputActions()
        {
            var globalActions = InputSystem.actions;
            if (globalActions != null && HasCombatBindings(globalActions))
                return globalActions;

            var loaded = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            InputActionAsset firstCompatible = null;
            for (int i = 0; i < loaded.Length; i++)
            {
                var candidate = loaded[i];
                if (candidate == null)
                    continue;

                bool compatible = HasCombatBindings(candidate);
                if (firstCompatible == null && compatible)
                    firstCompatible = candidate;

                if (candidate.name == "InputSystem_Actions" && compatible)
                    return candidate;
            }

            var playerInput = FindObjectOfType<PlayerInput>();
            if (playerInput != null && playerInput.actions != null && HasCombatBindings(playerInput.actions))
                return playerInput.actions;

            if (firstCompatible != null)
                return firstCompatible;

            if (globalActions != null)
                return globalActions;

            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null && loaded[i].name == "InputSystem_Actions")
                    return loaded[i];
            }

            var fromResources = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (fromResources != null)
                return fromResources;

            if (playerInput != null && playerInput.actions != null)
                return playerInput.actions;

            return null;
        }

        private void ConfigureUiInputModule()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                eventSystem = eventSystemGo.GetComponent<EventSystem>();
            }

            var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
                uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();

            var uiActions = inputActions != null ? inputActions : InputSystem.actions;
            if (uiActions != null && TryConfigureUiModuleFromAsset(uiModule, uiActions))
                return;

            // Fall back to generated defaults if the current asset does not define the required UI actions.
            uiModule.AssignDefaultActions();
            Debug.LogWarning("[CombatBattleUGUIInstaller] InputSystem_Actions is missing required UI actions (UI/Point, UI/Click, etc.). Assigned default UI module actions for runtime UI input.");
        }

        private static bool TryConfigureUiModuleFromAsset(InputSystemUIInputModule uiModule, InputActionAsset asset)
        {
            if (uiModule == null || asset == null)
                return false;

            var uiMap = asset.FindActionMap("UI", false);
            if (uiMap == null)
                return false;

            var point = FindAction(uiMap, "Point");
            var click = FindAction(uiMap, "Click", "LeftClick");
            var scrollWheel = FindAction(uiMap, "ScrollWheel");
            var move = FindAction(uiMap, "Navigate");
            var submit = FindAction(uiMap, "Submit");
            var cancel = FindAction(uiMap, "Cancel");

            if (point == null || click == null || move == null || submit == null || cancel == null)
                return false;

            uiModule.actionsAsset = asset;
            uiModule.point = InputActionReference.Create(point);
            uiModule.leftClick = InputActionReference.Create(click);
            if (scrollWheel != null)
                uiModule.scrollWheel = InputActionReference.Create(scrollWheel);
            uiModule.move = InputActionReference.Create(move);
            uiModule.submit = InputActionReference.Create(submit);
            uiModule.cancel = InputActionReference.Create(cancel);
            return true;
        }

        private static InputAction FindAction(InputActionMap map, params string[] names)
        {
            if (map == null || names == null)
                return null;

            for (int i = 0; i < names.Length; i++)
            {
                var action = map.FindAction(names[i], false);
                if (action != null)
                    return action;
            }

            return null;
        }

        private static bool HasCombatBindings(InputActionAsset asset)
        {
            if (asset == null)
                return false;

            var uiMap = asset.FindActionMap("UI", false);
            if (uiMap == null)
                return false;

            var navigate = FindAction(uiMap, "Navigate");
            var confirm = FindAction(uiMap, "Submit");
            var cancel = FindAction(uiMap, "Cancel");
            return navigate != null && confirm != null && cancel != null;
        }

        private static RectTransform CreateRectChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private HudCardView CreateHudCard(Transform parent, string name)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CombatHudAnimator),
                typeof(CombatHudCardBackgroundState),
                typeof(HudCardView));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.localScale = Vector3.one;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.06f, 0.09f, 0.12f, 0.86f);
            image.raycastTarget = false;
            image.type = Image.Type.Simple;

            var card = go.GetComponent<HudCardView>();
            card.ConfigureBackgroundSprites(
                hudCardNormalSprite,
                hudCardActiveSprite,
                hudCardTargetSprite,
                hudCardActiveTargetSprite);
            return card;
        }

        private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
