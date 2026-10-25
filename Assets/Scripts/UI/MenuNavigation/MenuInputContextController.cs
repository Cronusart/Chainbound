using System.Collections;
using System.Collections.Generic;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Reusable input/focus bridge for UGUI menus.
    /// Keeps menu navigation data in inspector fields while routing input through
    /// Unity's InputSystemUIInputModule and the shared mobile HUD context service.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuInputContextController : MonoBehaviour
    {
        [Header("Input System")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string uiActionMapName = "UI";
        [SerializeField] private string cancelActionName = "Cancel";
        [SerializeField] private string navigateActionName = "Navigate";
        [SerializeField] private string submitActionName = "Submit";
        [SerializeField] private string detailedViewActionName = "DetailedView";
        [SerializeField] private string closeMenuActionName = "CloseMenu";
        [SerializeField] private string specialInteractionActionName = "SpecialInteraction";
        [SerializeField] private string guideActionName = "Guide";
        [SerializeField, Range(0.1f, 1f)] private float keyboardNavigationThreshold = 0.5f;
        [SerializeField] private float keyboardNavigationInitialDelay = 0.28f;
        [SerializeField] private float keyboardNavigationRepeatRate = 0.12f;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule inputModule;
        [SerializeField, Tooltip("When enabled this controller is the sole Navigate/Submit/Cancel owner. The UI module keeps pointer actions only, preventing duplicate moves when keyboard/gamepad and the mobile HUD are combined.")]
        private bool routeNavigationThroughController = true;

        [Header("Selection")]
        [SerializeField] private Selectable defaultSelection;
        [SerializeField] private Selectable fallbackSelection;
        [SerializeField] private List<Selectable> navigationOrder = new List<Selectable>();
        [SerializeField] private bool selectOnEnable = true;
        [SerializeField] private bool restoreSelectionWhenLost = true;

        [Header("Mobile HUD Navigation")]
        [SerializeField] private MobileInputManager mobileInputManager;
        [SerializeField] private bool enableMobileHudNavigation = true;
        [SerializeField, Range(0.1f, 1f)] private float mobileNavigationThreshold = 0.5f;
        [SerializeField] private float mobileNavigationInitialDelay = 0.28f;
        [SerializeField] private float mobileNavigationRepeatRate = 0.12f;
        [SerializeField] private bool mobileAPressesSubmit = true;

        [Header("Explicit Navigation")]
        [SerializeField] private bool configureExplicitNavigation = true;
        [SerializeField] private bool wrapNavigation = false;

        [Header("Mobile HUD Context")]
        [SerializeField] private bool acquireMobileHudMenuContext = true;
        [SerializeField] private MobileHudService mobileHudService;
        [SerializeField] private string mobileHudTokenReason = "menu input context";

        [Header("Events")]
        [SerializeField] private UnityEvent onCancel = new UnityEvent();
        [SerializeField] private UnityEvent onCloseMenu = new UnityEvent();

        [Header("Audio")]
        [SerializeField] private MonoBehaviour sfxPlayerBehaviour;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private InputAction cancelAction;
        private InputAction navigateAction;
        private InputAction submitAction;
        private InputAction detailedViewAction;
        private InputAction closeMenuAction;
        private InputAction specialInteractionAction;
        private InputAction guideAction;
        private MobileHudToken menuHudToken;
        private bool hasMenuHudToken;
        private bool hudButtonAudioSuppressed;
        private Coroutine deferredSelectionCoroutine;
        private Vector2 lastDirectionalMove;
        private float nextDirectionalMoveTime;
        private InputActionReference cachedMoveActionReference;
        private InputActionReference cachedSubmitActionReference;
        private InputActionReference cachedCancelActionReference;
        private readonly List<Selectable> inspectorNavigationOrder = new List<Selectable>();
        private Selectable inspectorDefaultSelection;
        private Selectable inspectorFallbackSelection;
        private Transform contentNavigationRoot;
        private IMenuContentInputHandler contentInputHandler;
        private IMenuAdvancedInputHandler advancedInputHandler;
        private bool contentRestoreSelectionWhenLost = true;
        private MonoBehaviour cachedSfxPlayerBehaviour;
        private GameObject lastSelectedForSfx;
        private bool interactionEnabled = true;
        private bool animationHold;

        public UnityEvent OnCancel => onCancel;
        public UnityEvent OnCloseMenu => onCloseMenu;
        public bool IsUsingContentNavigation => contentNavigationRoot != null;
        public bool IsInteractionEnabled => interactionEnabled;
        public bool HasAnimationHold => animationHold;

        private void Awake()
        {
            ResolveSceneReferences();
            CaptureInspectorNavigation();
        }

        private void OnEnable()
        {
            ResolveSceneReferences();
            ConfigureInputModule();
            ConfigureNavigation();
            SubscribeCancelAction();
            SubscribeMobileHudInput();
            AcquireMenuHudToken();
            SyncInputModuleContentRouting();
            SceneManager.sceneLoaded += HandleSceneLoaded;

            if (selectOnEnable && interactionEnabled)
                SelectDefaultDeferred();
            RefreshHudButtonAudioSuppression();
        }

        private void Update()
        {
            if (!interactionEnabled)
                return;

            // GameServices owns the persistent EventSystem and removes any
            // scene-local duplicate after a scene load. This controller can
            // therefore start with a serialized scene reference that becomes
            // invalid one frame later. Rebind before processing HUD/keyboard
            // input so selection and navigation use the same EventSystem as
            // the active InputSystemUIInputModule.
            EnsureEventSystemReferences();

            if (OnScreenKeyboardService.Instance != null && OnScreenKeyboardService.Instance.IsOpen)
                return;

            TrackPlayerNavigationIntent();

            if (animationHold)
                return;

            if (restoreSelectionWhenLost && contentRestoreSelectionWhenLost)
                RestoreSelectionIfNeeded();
            else
                ClearContentSelectionIfNeeded();

            ProcessDirectionalInput();
        }

        private void LateUpdate()
        {
            if (!interactionEnabled || !restoreSelectionWhenLost || !isActiveAndEnabled)
                return;
            if (animationHold)
                return;
            if (OnScreenKeyboardService.Instance != null && OnScreenKeyboardService.Instance.IsOpen)
                return;

            // InputSystemUIInputModule/EventSystem can process after this
            // component's Update. Repair a lost panel selection once more in
            // LateUpdate, without changing a valid player selection.
            EnsureEventSystemReferences();
            if (eventSystem != null && contentRestoreSelectionWhenLost)
                RestoreSelectionIfNeeded();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            if (deferredSelectionCoroutine != null)
            {
                StopCoroutine(deferredSelectionCoroutine);
                deferredSelectionCoroutine = null;
            }

            UnsubscribeCancelAction();
            UnsubscribeMobileHudInput();
            ReleaseMenuHudToken();
            RestoreInputModuleReferences();
            animationHold = false;
            SetHudButtonAudioSuppressed(false);
        }

        /// <summary>
        /// Rebuilds explicit navigation from the current inspector order.
        /// Useful after menus dynamically enable/disable selectable entries.
        /// </summary>
        public void ConfigureNavigation()
        {
            if (!configureExplicitNavigation) return;

            List<Selectable> valid = GetUsableNavigationOrder();
            for (int i = 0; i < valid.Count; i++)
            {
                Selectable previous = i > 0 ? valid[i - 1] : (wrapNavigation && valid.Count > 1 ? valid[valid.Count - 1] : null);
                Selectable next = i < valid.Count - 1 ? valid[i + 1] : (wrapNavigation && valid.Count > 1 ? valid[0] : null);

                Navigation nav = valid[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = previous;
                nav.selectOnDown = next;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                valid[i].navigation = nav;
            }
        }

        /// <summary>
        /// Pauses menu focus and action routing without disabling this component.
        /// Keeping the component active preserves its mobile-HUD context token,
        /// allowing a virtual button to participate in an external any-button gate.
        /// </summary>
        public void SetInteractionEnabled(bool enabled)
        {
            if (interactionEnabled == enabled)
                return;

            interactionEnabled = enabled;
            RefreshHudButtonAudioSuppression();
            if (!enabled)
            {
                if (deferredSelectionCoroutine != null)
                {
                    StopCoroutine(deferredSelectionCoroutine);
                    deferredSelectionCoroutine = null;
                }

                if (eventSystem != null)
                    eventSystem.SetSelectedGameObject(null);
                return;
            }

            if (isActiveAndEnabled && selectOnEnable)
                SelectDefaultDeferred();
        }

        /// <summary>
        /// Blocks Submit and directional navigation while a panel/boot animation owns
        /// the menu. Cancel still routes so Back/B can recover a stuck hold.
        /// </summary>
        public void SetAnimationHold(bool held)
        {
            animationHold = held;
        }

        /// <summary>
        /// Rebinds focus/navigation to the active tab screen content.
        /// </summary>
        public void SetContentNavigation(
            IReadOnlyList<Selectable> order,
            Selectable defaultSelect,
            Transform searchRoot,
            Selectable fallbackSelect = null,
            bool restoreSelectionWhenLost = true)
        {
            navigationOrder.Clear();
            if (order != null)
            {
                for (int i = 0; i < order.Count; i++)
                {
                    Selectable selectable = order[i];
                    if (selectable != null && !navigationOrder.Contains(selectable))
                        navigationOrder.Add(selectable);
                }
            }

            defaultSelection = defaultSelect;
            fallbackSelection = fallbackSelect != null ? fallbackSelect : defaultSelect;
            contentNavigationRoot = searchRoot;
            contentRestoreSelectionWhenLost = restoreSelectionWhenLost;
            SelectDefaultDeferred();
        }

        public void SetContentInputHandler(IMenuContentInputHandler handler)
        {
            contentInputHandler = handler;
            advancedInputHandler = handler as IMenuAdvancedInputHandler;
            SyncInputModuleContentRouting();
        }

        /// <summary>
        /// Restores the prefab-wired hub tab navigation order.
        /// </summary>
        public void RestoreInspectorNavigation()
        {
            navigationOrder.Clear();
            navigationOrder.AddRange(inspectorNavigationOrder);
            defaultSelection = inspectorDefaultSelection;
            fallbackSelection = inspectorFallbackSelection;
            contentNavigationRoot = null;
            contentInputHandler = null;
            advancedInputHandler = null;
            contentRestoreSelectionWhenLost = true;
            SyncInputModuleContentRouting();
            ConfigureNavigation();
        }

        private void CaptureInspectorNavigation()
        {
            inspectorNavigationOrder.Clear();
            for (int i = 0; i < navigationOrder.Count; i++)
            {
                Selectable selectable = navigationOrder[i];
                if (selectable != null && !inspectorNavigationOrder.Contains(selectable))
                    inspectorNavigationOrder.Add(selectable);
            }

            inspectorDefaultSelection = defaultSelection;
            inspectorFallbackSelection = fallbackSelection;
        }

        public void SelectDefault()
        {
            if (!interactionEnabled) return;
            SelectBestAvailable();
        }

        public void SelectDefaultDeferred()
        {
            if (!isActiveAndEnabled || !interactionEnabled) return;

            if (deferredSelectionCoroutine != null)
                StopCoroutine(deferredSelectionCoroutine);

            deferredSelectionCoroutine = StartCoroutine(SelectDefaultNextFrame());
        }

        public void Select(Selectable selectable)
        {
            if (!interactionEnabled) return;

            EnsureEventSystemReferences();

            if (!IsSelectableUsable(selectable))
            {
                SelectBestAvailable();
                return;
            }

            SetSelected(selectable);
        }

        public void HandleCancel()
        {
            if (!isActiveAndEnabled || !interactionEnabled) return;
            if (IsOnScreenKeyboardOpen())
                return;
            if (!MenuInputIntentGate.TryConsume(MenuInputIntent.Cancel))
                return;

            if (enableDebugLogs)
                Debug.Log($"[MenuInputContextController] Cancel invoked on {name}.", this);

            if (contentInputHandler != null && contentInputHandler.TryHandleCancel())
            {
                PlayCue(PlayerMenuSfxCue.Cancel);
                return;
            }

            onCancel?.Invoke();
        }

        private IEnumerator SelectDefaultNextFrame()
        {
            yield return null;
            if (!interactionEnabled)
            {
                deferredSelectionCoroutine = null;
                yield break;
            }

            EnsureEventSystemReferences();

            if (IsUsingContentNavigation && !contentRestoreSelectionWhenLost)
                ClearContentSelectionIfNeeded();
            else
                SelectBestAvailable();
            deferredSelectionCoroutine = null;
        }

        private void ResolveSceneReferences()
        {
            // Do not retain a serialized scene EventSystem that has been
            // disabled/destroyed by PersistentUiEventSystem. Always pair the
            // EventSystem and InputSystemUIInputModule from the same GameObject
            // so pointer, HUD, keyboard, and gamepad focus share one owner.
            if (eventSystem == null || !eventSystem.isActiveAndEnabled)
                eventSystem = null;

            if (inputModule == null || !inputModule.isActiveAndEnabled || inputModule.GetComponent<EventSystem>() != eventSystem)
                inputModule = null;

            if (eventSystem == null && inputModule != null)
                eventSystem = inputModule.GetComponent<EventSystem>();

            if (eventSystem == null)
                eventSystem = FindActiveEventSystem();

            if (inputModule == null && eventSystem != null)
                inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();

            if (inputModule == null)
            {
                InputSystemUIInputModule[] modules = FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < modules.Length; i++)
                {
                    InputSystemUIInputModule candidate = modules[i];
                    if (candidate == null || !candidate.isActiveAndEnabled)
                        continue;

                    EventSystem candidateEventSystem = candidate.GetComponent<EventSystem>();
                    if (candidateEventSystem != null && candidateEventSystem.isActiveAndEnabled)
                    {
                        inputModule = candidate;
                        if (eventSystem == null)
                            eventSystem = candidateEventSystem;
                        break;
                    }
                }
            }

            if (inputActions == null && inputModule != null)
                inputActions = inputModule.actionsAsset;
            if (inputActions == null)
                inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");

            if (mobileHudService == null)
                mobileHudService = FindObjectOfType<MobileHudService>();
            if (mobileInputManager == null)
                mobileInputManager = FindObjectOfType<MobileInputManager>(true);
        }

        private static EventSystem FindActiveEventSystem()
        {
            EventSystem current = EventSystem.current;
            if (current != null && current.isActiveAndEnabled)
                return current;

            EventSystem[] candidates = FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < candidates.Length; i++)
            {
                EventSystem candidate = candidates[i];
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                InputSystemUIInputModule module = candidate.GetComponent<InputSystemUIInputModule>();
                if (module != null && module.isActiveAndEnabled)
                    return candidate;
            }

            for (int i = 0; i < candidates.Length; i++)
            {
                EventSystem candidate = candidates[i];
                if (candidate != null && candidate.isActiveAndEnabled)
                    return candidate;
            }

            return null;
        }

        private void EnsureEventSystemReferences()
        {
            bool eventSystemValid = eventSystem != null && eventSystem.isActiveAndEnabled;
            bool inputModuleValid = inputModule != null
                && inputModule.isActiveAndEnabled
                && inputModule.GetComponent<EventSystem>() == eventSystem;

            if (eventSystemValid && inputModuleValid)
                return;

            ResolveSceneReferences();
            ConfigureInputModule();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isActiveAndEnabled)
                return;

            // PersistentUiEventSystem removes scene-local duplicates during
            // the same sceneLoaded dispatch. Wait a frame, then bind to the
            // surviving EventSystem before restoring focus.
            StartCoroutine(RebindEventSystemNextFrame());
        }

        private IEnumerator RebindEventSystemNextFrame()
        {
            yield return null;
            if (!isActiveAndEnabled)
                yield break;

            EnsureEventSystemReferences();
            if (interactionEnabled && selectOnEnable)
                SelectDefaultDeferred();
        }

        private void ConfigureInputModule()
        {
            if (inputActions == null || inputModule == null) return;
            inputModule.actionsAsset = inputActions;
            // Virtual HUD controls are intentionally not UGUI Selectables. On touch devices,
            // treating them as background clicks clears the menu's current selection before
            // their GameboyButton event reaches us, so every A press only repairs focus.
            // Preserve menu focus while the shared HUD is being used.
            inputModule.deselectOnBackgroundClick = false;
            SyncInputModuleContentRouting();
        }

        private void SubscribeCancelAction()
        {
            UnsubscribeCancelAction();

            if (inputActions == null || string.IsNullOrWhiteSpace(uiActionMapName) || string.IsNullOrWhiteSpace(cancelActionName))
                return;

            InputActionMap uiMap = inputActions.FindActionMap(uiActionMapName, throwIfNotFound: false);
            if (uiMap == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[MenuInputContextController] Action map '{uiActionMapName}' not found.", this);
                return;
            }

            cancelAction = uiMap.FindAction(cancelActionName, throwIfNotFound: false);
            if (cancelAction == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[MenuInputContextController] Cancel action '{cancelActionName}' not found.", this);
                return;
            }

            cancelAction.performed += HandleCancelPerformed;
            cancelAction.Enable();

            navigateAction = FindUiAction(uiMap, navigateActionName);
            if (navigateAction != null)
                navigateAction.Enable();

            submitAction = FindUiAction(uiMap, submitActionName);
            if (submitAction != null)
            {
                submitAction.performed += HandleSubmitPerformed;
                submitAction.Enable();
            }

            detailedViewAction = FindUiAction(uiMap, detailedViewActionName);
            if (detailedViewAction != null)
            {
                detailedViewAction.performed += HandleDetailedViewPerformed;
                detailedViewAction.Enable();
            }

            closeMenuAction = FindUiAction(uiMap, closeMenuActionName);
            if (closeMenuAction != null)
            {
                closeMenuAction.performed += HandleCloseMenuPerformed;
                closeMenuAction.Enable();
            }

            specialInteractionAction = FindUiAction(uiMap, specialInteractionActionName);
            if (specialInteractionAction != null)
            {
                specialInteractionAction.performed += HandleSpecialInteractionPerformed;
                specialInteractionAction.Enable();
            }

            guideAction = FindUiAction(uiMap, guideActionName);
            if (guideAction != null)
            {
                guideAction.performed += HandleGuidePerformed;
                guideAction.Enable();
            }

            uiMap.Enable();
        }

        private void UnsubscribeCancelAction()
        {
            if (cancelAction != null)
            {
                cancelAction.performed -= HandleCancelPerformed;
                cancelAction = null;
            }

            if (navigateAction != null)
            {
                navigateAction.Disable();
                navigateAction = null;
            }

            if (submitAction != null)
            {
                submitAction.performed -= HandleSubmitPerformed;
                submitAction = null;
            }

            if (detailedViewAction != null)
            {
                detailedViewAction.performed -= HandleDetailedViewPerformed;
                detailedViewAction = null;
            }

            if (closeMenuAction != null)
            {
                closeMenuAction.performed -= HandleCloseMenuPerformed;
                closeMenuAction = null;
            }

            if (specialInteractionAction != null)
            {
                specialInteractionAction.performed -= HandleSpecialInteractionPerformed;
                specialInteractionAction = null;
            }

            if (guideAction != null)
            {
                guideAction.performed -= HandleGuidePerformed;
                guideAction = null;
            }
        }

        private static InputAction FindUiAction(InputActionMap uiMap, params string[] actionNames)
        {
            if (uiMap == null || actionNames == null)
                return null;

            for (int i = 0; i < actionNames.Length; i++)
            {
                string actionName = actionNames[i];
                if (string.IsNullOrWhiteSpace(actionName))
                    continue;

                InputAction action = uiMap.FindAction(actionName, throwIfNotFound: false);
                if (action != null)
                    return action;
            }

            return null;
        }

        private void HandleSubmitPerformed(InputAction.CallbackContext context)
        {
            SubmitSelected();
        }

        private void HandleDetailedViewPerformed(InputAction.CallbackContext context)
        {
            HandleAdvancedInput(MenuInputIntent.DetailedView, PlayerMenuSfxCue.Confirm, handler => handler.TryHandleDetailedView());
        }

        private void HandleCloseMenuPerformed(InputAction.CallbackContext context)
        {
            HandleCloseMenu();
        }

        private void HandleSpecialInteractionPerformed(InputAction.CallbackContext context)
        {
            HandleAdvancedInput(MenuInputIntent.SpecialInteraction, PlayerMenuSfxCue.Confirm, handler => handler.TryHandleSpecialInteraction());
        }

        private void HandleGuidePerformed(InputAction.CallbackContext context)
        {
            HandleAdvancedInput(MenuInputIntent.Guide, PlayerMenuSfxCue.Confirm, handler => handler.TryHandleGuide());
        }

        private void HandleCloseMenu()
        {
            if (!isActiveAndEnabled || !interactionEnabled)
                return;
            if (IsOnScreenKeyboardOpen())
                return;
            if (!MenuInputIntentGate.TryConsume(MenuInputIntent.CloseMenu))
                return;

            if (advancedInputHandler != null && advancedInputHandler.TryHandleCloseMenu())
            {
                PlayCue(PlayerMenuSfxCue.Cancel);
                return;
            }

            onCloseMenu?.Invoke();
        }

        private void HandleAdvancedInput(
            MenuInputIntent intent,
            PlayerMenuSfxCue handledCue,
            System.Func<IMenuAdvancedInputHandler, bool> handler)
        {
            if (!isActiveAndEnabled || !interactionEnabled)
                return;
            if (animationHold)
                return;
            if (IsOnScreenKeyboardOpen())
                return;
            if (!MenuInputIntentGate.TryConsume(intent))
                return;

            if (advancedInputHandler != null && handler != null && handler(advancedInputHandler))
            {
                PlayCue(handledCue);
                return;
            }

            PlayCue(PlayerMenuSfxCue.Invalid);
        }

        private void SyncInputModuleContentRouting()
        {
            if (inputModule == null)
                return;

            if (routeNavigationThroughController || contentInputHandler != null)
            {
                CacheInputModuleReferencesIfNeeded();
                inputModule.move = null;
                inputModule.submit = null;
                if (routeNavigationThroughController)
                    inputModule.cancel = null;
                return;
            }

            RestoreInputModuleReferences();
        }

        private void CacheInputModuleReferencesIfNeeded()
        {
            if (inputModule == null)
                return;

            if (cachedMoveActionReference == null && inputModule.move != null)
                cachedMoveActionReference = inputModule.move;
            if (cachedSubmitActionReference == null && inputModule.submit != null)
                cachedSubmitActionReference = inputModule.submit;
            if (cachedCancelActionReference == null && inputModule.cancel != null)
                cachedCancelActionReference = inputModule.cancel;
        }

        private void RestoreInputModuleReferences()
        {
            if (inputModule == null)
                return;

            if (cachedMoveActionReference != null)
                inputModule.move = cachedMoveActionReference;
            if (cachedSubmitActionReference != null)
                inputModule.submit = cachedSubmitActionReference;
            if (cachedCancelActionReference != null)
                inputModule.cancel = cachedCancelActionReference;
        }

        private void HandleCancelPerformed(InputAction.CallbackContext context)
        {
            HandleCancel();
        }

        private void SubscribeMobileHudInput()
        {
            if (!enableMobileHudNavigation) return;

            ResolveSceneReferences();
            if (mobileInputManager == null) return;

            mobileInputManager.OnAPressed -= HandleMobileAPressed;
            mobileInputManager.OnAPressed += HandleMobileAPressed;
            mobileInputManager.OnBPressed -= HandleMobileBPressed;
            mobileInputManager.OnBPressed += HandleMobileBPressed;
            mobileInputManager.OnYPressed -= HandleMobileYPressed;
            mobileInputManager.OnYPressed += HandleMobileYPressed;
            mobileInputManager.OnXPressed -= HandleMobileXPressed;
            mobileInputManager.OnXPressed += HandleMobileXPressed;
            mobileInputManager.OnStartPressed -= HandleMobileStartPressed;
            mobileInputManager.OnStartPressed += HandleMobileStartPressed;
        }

        private void UnsubscribeMobileHudInput()
        {
            if (mobileInputManager == null) return;

            mobileInputManager.OnAPressed -= HandleMobileAPressed;
            mobileInputManager.OnBPressed -= HandleMobileBPressed;
            mobileInputManager.OnYPressed -= HandleMobileYPressed;
            mobileInputManager.OnXPressed -= HandleMobileXPressed;
            mobileInputManager.OnStartPressed -= HandleMobileStartPressed;
        }

        private void HandleMobileAPressed()
        {
            if (!mobileAPressesSubmit) return;
            SubmitSelected(allowFocusRecovery: true);
        }

        private void HandleMobileBPressed()
        {
            HandleCancel();
        }

        private void HandleMobileYPressed()
        {
            HandleAdvancedInput(MenuInputIntent.DetailedView, PlayerMenuSfxCue.Confirm, handler => handler.TryHandleDetailedView());
        }

        private void HandleMobileXPressed()
        {
            HandleAdvancedInput(MenuInputIntent.SpecialInteraction, PlayerMenuSfxCue.Confirm, handler => handler.TryHandleSpecialInteraction());
        }

        private void HandleMobileStartPressed()
        {
            HandleCloseMenu();
        }

        private void RefreshHudButtonAudioSuppression()
        {
            SetHudButtonAudioSuppressed(isActiveAndEnabled && interactionEnabled);
        }

        private void SetHudButtonAudioSuppressed(bool suppressed)
        {
            if (hudButtonAudioSuppressed == suppressed)
                return;

            hudButtonAudioSuppressed = suppressed;
            if (suppressed)
                HudButtonAudioGate.Suppress();
            else
                HudButtonAudioGate.Release();
        }

        private void AcquireMenuHudToken()
        {
            if (!acquireMobileHudMenuContext || hasMenuHudToken) return;

            MobileHudService service = mobileHudService != null ? mobileHudService : MobileHudService.Instance;
            if (service == null) return;

            menuHudToken = service.AcquireContext(MobileHudContext.Menu, mobileHudTokenReason);
            hasMenuHudToken = menuHudToken.IsValid;
        }

        private void ReleaseMenuHudToken()
        {
            if (!hasMenuHudToken) return;

            menuHudToken.Release();
            hasMenuHudToken = false;
        }

        private void SelectBestAvailable()
        {
            if (IsUsingContentNavigation && !contentRestoreSelectionWhenLost)
            {
                ClearContentSelectionIfNeeded();
                return;
            }

            Selectable target = ResolveBestSelectable();
            if (target == null || eventSystem == null) return;
            SetSelected(target);
        }

        private void ClearContentSelectionIfNeeded()
        {
            if (contentRestoreSelectionWhenLost || eventSystem == null)
                return;

            if (eventSystem.currentSelectedGameObject != null)
                eventSystem.SetSelectedGameObject(null);
        }

        private Selectable ResolveBestSelectable()
        {
            if (IsSelectableUsable(defaultSelection)) return defaultSelection;
            if (IsSelectableUsable(fallbackSelection)) return fallbackSelection;

            for (int i = 0; i < navigationOrder.Count; i++)
            {
                Selectable selectable = navigationOrder[i];
                if (IsSelectableUsable(selectable))
                    return selectable;
            }

            if (navigationOrder.Count > 0)
                return null;

            Transform searchRoot = contentNavigationRoot != null ? contentNavigationRoot : transform;
            Selectable[] children = searchRoot.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Selectable selectable = children[i];
                if (IsSelectableUsable(selectable))
                    return selectable;
            }

            return null;
        }

        private void RestoreSelectionIfNeeded()
        {
            if (eventSystem == null) return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected != null)
            {
                Selectable selectable = selected.GetComponent<Selectable>();
                if (IsSelectableUsable(selectable))
                    return;
            }

            SelectBestAvailable();
        }

        private void ProcessDirectionalInput()
        {
            if (eventSystem == null)
                return;

            if (contentInputHandler != null)
            {
                ProcessManagedContentDirectionalInput();
                return;
            }

            ProcessLegacyMobileHudNavigation();
        }

        private void ProcessManagedContentDirectionalInput()
        {
            Vector2 move = ReadCombinedDirectionalInput();
            float threshold = Mathf.Max(keyboardNavigationThreshold, mobileNavigationThreshold);
            if (move.sqrMagnitude < threshold * threshold)
            {
                lastDirectionalMove = Vector2.zero;
                nextDirectionalMoveTime = 0f;
                return;
            }

            move = QuantizeMove(move);
            bool changedDirection = lastDirectionalMove == Vector2.zero || Vector2.Dot(lastDirectionalMove, move) < 0.5f;
            if (!changedDirection && Time.unscaledTime < nextDirectionalMoveTime)
                return;

            if (contentInputHandler.TryHandleMove(move))
            {
                RegisterDirectionalMove(move, changedDirection, useKeyboardTiming: IsKeyboardDirectionalInput(move));
                return;
            }

            if (!contentRestoreSelectionWhenLost)
                return;

            EnsureSelectedBeforeMobileInput();
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return;

            var axisEventData = new AxisEventData(eventSystem)
            {
                moveVector = move,
                moveDir = ToMoveDirection(move),
            };
            ExecuteEvents.Execute(selected, axisEventData, ExecuteEvents.moveHandler);
            RegisterDirectionalMove(move, changedDirection, useKeyboardTiming: IsKeyboardDirectionalInput(move));
        }

        private void ProcessLegacyMobileHudNavigation()
        {
            Vector2 move = ReadCombinedDirectionalInput();
            float threshold = IsKeyboardDirectionalInput(move)
                ? keyboardNavigationThreshold
                : mobileNavigationThreshold;
            if (move.sqrMagnitude < threshold * threshold)
            {
                lastDirectionalMove = Vector2.zero;
                nextDirectionalMoveTime = 0f;
                return;
            }

            move = QuantizeMove(move);
            bool changedDirection = lastDirectionalMove == Vector2.zero || Vector2.Dot(lastDirectionalMove, move) < 0.5f;
            if (!changedDirection && Time.unscaledTime < nextDirectionalMoveTime)
                return;

            EnsureSelectedBeforeMobileInput();
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null)
                return;

            var axisEventData = new AxisEventData(eventSystem)
            {
                moveVector = move,
                moveDir = ToMoveDirection(move),
            };
            ExecuteEvents.Execute(selected, axisEventData, ExecuteEvents.moveHandler);
            RegisterDirectionalMove(move, changedDirection, useKeyboardTiming: IsKeyboardDirectionalInput(move));
        }

        private Vector2 ReadCombinedDirectionalInput()
        {
            Vector2 keyboardMove = ReadKeyboardNavigate();
            if (keyboardMove.sqrMagnitude >= keyboardNavigationThreshold * keyboardNavigationThreshold)
                return keyboardMove;

            if (!enableMobileHudNavigation || mobileInputManager == null)
                return Vector2.zero;

            return ReadMobileHudMove();
        }

        private void TrackPlayerNavigationIntent()
        {
            Vector2 keyboardMove = ReadKeyboardNavigate();
            if (keyboardMove.sqrMagnitude >= keyboardNavigationThreshold * keyboardNavigationThreshold)
            {
                PlayerMenuSfxResolver.MarkPlayerNavigationIntent();
                return;
            }

            if (!enableMobileHudNavigation || mobileInputManager == null)
                return;

            Vector2 mobileMove = ReadMobileHudMove();
            if (mobileMove.sqrMagnitude >= mobileNavigationThreshold * mobileNavigationThreshold)
                PlayerMenuSfxResolver.MarkPlayerNavigationIntent();
        }

        private Vector2 ReadKeyboardNavigate()
        {
            return navigateAction != null ? navigateAction.ReadValue<Vector2>() : Vector2.zero;
        }

        private bool IsKeyboardDirectionalInput(Vector2 move)
        {
            Vector2 keyboardMove = ReadKeyboardNavigate();
            return keyboardMove.sqrMagnitude >= keyboardNavigationThreshold * keyboardNavigationThreshold
                && Vector2.Dot(QuantizeMove(keyboardMove), move) > 0.5f;
        }

        private void RegisterDirectionalMove(Vector2 move, bool changedDirection, bool useKeyboardTiming)
        {
            lastDirectionalMove = move;
            float initialDelay = useKeyboardTiming ? keyboardNavigationInitialDelay : mobileNavigationInitialDelay;
            float repeatRate = useKeyboardTiming ? keyboardNavigationRepeatRate : mobileNavigationRepeatRate;
            float delay = changedDirection ? initialDelay : repeatRate;
            nextDirectionalMoveTime = Time.unscaledTime + Mathf.Max(0.01f, delay);
        }

        private Vector2 ReadMobileHudMove()
        {
            HybridMovementPad pad = mobileInputManager.MovementPad;
            return pad != null ? pad.GetInput() : Vector2.zero;
        }

        private void EnsureSelectedBeforeMobileInput()
        {
            if (!contentRestoreSelectionWhenLost)
                return;

            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null || !IsSelectableUsable(selected.GetComponent<Selectable>()))
                SelectBestAvailable();
        }

        private void SubmitSelected(bool allowFocusRecovery = false)
        {
            if (!interactionEnabled || animationHold)
                return;
            if (IsOnScreenKeyboardOpen())
                return;

            if (eventSystem == null)
            {
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            // Content handlers (Candy Machine, object-navigation menus, etc.) own
            // their submit semantics and do not require a selected UGUI button.
            // Standard button menus do. Repair an invalid selection before claiming
            // the de-duplication gate so the sibling mobile callback can still act.
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (contentInputHandler == null
                && !IsSelectableUsable(selected != null ? selected.GetComponent<Selectable>() : null))
            {
                EnsureSelectedBeforeMobileInput();
                selected = eventSystem.currentSelectedGameObject;
                if (!allowFocusRecovery
                    || !IsSelectableUsable(selected != null ? selected.GetComponent<Selectable>() : null))
                {
                    PlayCue(PlayerMenuSfxCue.Invalid);
                    return;
                }
            }

            if (!MenuInputIntentGate.TryConsume(MenuInputIntent.Submit))
                return;

            PlayerMenuSfxResolver.SuppressSubmitTransitionCues(0.12f);
            if (contentInputHandler != null && contentInputHandler.TryHandleSubmit())
            {
                PlayCue(PlayerMenuSfxCue.Confirm);
                return;
            }

            selected = eventSystem.currentSelectedGameObject;
            if (!IsSelectableUsable(selected != null ? selected.GetComponent<Selectable>() : null))
            {
                PlayCue(PlayerMenuSfxCue.Invalid);
                return;
            }

            PlayCue(PlayerMenuSfxCue.Confirm);
            var baseEventData = new BaseEventData(eventSystem);
            ExecuteEvents.Execute(selected, baseEventData, ExecuteEvents.submitHandler);
        }

        private static Vector2 QuantizeMove(Vector2 move)
        {
            if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
                return new Vector2(Mathf.Sign(move.x), 0f);

            return new Vector2(0f, Mathf.Sign(move.y));
        }

        private static MoveDirection ToMoveDirection(Vector2 move)
        {
            if (Mathf.Abs(move.x) > Mathf.Abs(move.y))
                return move.x > 0f ? MoveDirection.Right : MoveDirection.Left;

            return move.y > 0f ? MoveDirection.Up : MoveDirection.Down;
        }

        private List<Selectable> GetValidNavigationOrder()
        {
            List<Selectable> valid = new List<Selectable>(navigationOrder.Count);
            for (int i = 0; i < navigationOrder.Count; i++)
            {
                Selectable selectable = navigationOrder[i];
                if (selectable != null && !valid.Contains(selectable))
                    valid.Add(selectable);
            }

            return valid;
        }

        private List<Selectable> GetUsableNavigationOrder()
        {
            List<Selectable> valid = new List<Selectable>(navigationOrder.Count);
            for (int i = 0; i < navigationOrder.Count; i++)
            {
                Selectable selectable = navigationOrder[i];
                if (IsSelectableUsable(selectable) && !valid.Contains(selectable))
                    valid.Add(selectable);
            }

            return valid;
        }

        private bool IsSelectableUsable(Selectable selectable)
        {
            return selectable != null
                && selectable.gameObject.activeInHierarchy
                && selectable.IsActive()
                && selectable.IsInteractable();
        }

        private void SetSelected(Selectable selectable)
        {
            EnsureEventSystemReferences();
            if (eventSystem == null || selectable == null) return;

            GameObject target = selectable.gameObject;
            if (eventSystem.currentSelectedGameObject == target) return;

            // Switch in one EventSystem operation. A null intermediate target
            // invokes OnDeselect and lets animated controls write their rest
            // scale before the replacement selection is applied.
            eventSystem.SetSelectedGameObject(target);

            if (target != lastSelectedForSfx)
            {
                lastSelectedForSfx = target;
                PlayCue(PlayerMenuSfxCue.ButtonSwitch);
            }
        }

        private void PlayCue(PlayerMenuSfxCue cue)
        {
            if (IsBlockedInputMuted(cue) || PlayerMenuSfxResolver.IsCueSuppressed(cue))
                return;

            IPlayerMenuSfxPlayer sfxPlayer = PlayerMenuSfxResolver.Resolve(sfxPlayerBehaviour, ref cachedSfxPlayerBehaviour);
            sfxPlayer?.PlayCue(cue);
        }

        private bool IsBlockedInputMuted(PlayerMenuSfxCue cue)
        {
            if (!(contentInputHandler is IMenuBlockedInputSilence silence) || !silence.MuteBlockedInput)
                return false;

            return cue == PlayerMenuSfxCue.Confirm
                || cue == PlayerMenuSfxCue.Cancel
                || cue == PlayerMenuSfxCue.Invalid
                || cue == PlayerMenuSfxCue.ButtonSwitch;
        }

        private static bool IsOnScreenKeyboardOpen()
        {
            OnScreenKeyboardService keyboard = OnScreenKeyboardService.Instance;
            return keyboard != null && keyboard.IsOpen;
        }
    }
}
