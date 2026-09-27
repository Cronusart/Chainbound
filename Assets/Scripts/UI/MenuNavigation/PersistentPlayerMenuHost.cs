using System;
using SignalJam.UI.MobileControls;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Owns the persistent gameplay/player menu from GameServices.
    /// The menu panel can be inactive; this host stays active and routes global input to it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersistentPlayerMenuHost : MonoBehaviour
    {
        [Header("Menu")]
        [SerializeField] private PlayerMenuController playerMenuPrefab;
        [SerializeField] private PlayerMenuController playerMenuInstance;

        [Header("Input")]
        [SerializeField] private MobileControlRouter mobileControlRouter;
        [SerializeField] private bool openOnMenuButton = true;
        [SerializeField] private bool routeStartToPauseMenu = true;
        [SerializeField] private bool toggleMenuButton = true;
        [SerializeField] private bool openOnSelectButton = false;
        [SerializeField] private bool closeOnBackButton = false;

        [Header("Input Actions")]
        [SerializeField] private bool enableInputActions = true;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string inputActionMapName = "Player";
        [SerializeField] private string toggleMenuActionName = "PlayerMenu";
        [SerializeField] private string pauseMenuActionName = "StartMenu";
        [SerializeField] private string walletConnectActionName = "WalletConnect";
        [SerializeField] private string inventoryTabId = "inventory";

        [Header("Wallet")]
        [SerializeField] private UnityEvent onWalletConnectRequested = new UnityEvent();

        [Header("Debug")]
        [SerializeField] private float duplicateToggleWindowSeconds = 0.08f;
        [SerializeField] private bool enableDebugLogs = false;

        [Header("Dev Bootstrap")]
        [SerializeField] private PlayerMenuDevBootstrap devBootstrap;

        private bool isSubscribed;
        private bool isInputActionSubscribed;
        private InputAction toggleMenuAction;
        private InputAction pauseMenuAction;
        private InputAction walletConnectAction;
        private InputLockService inputLockService;
        private int lastMenuToggleFrame = -1;
        private float lastMenuToggleTime = -1f;

        public PlayerMenuController Menu => EnsureMenuInstance();
        public bool IsMenuOpen => IsResolvedMenuOpen();

        private void Awake()
        {
            if (devBootstrap == null)
                devBootstrap = GetComponent<PlayerMenuDevBootstrap>();

            EnsureMenuInstance();
            ResolveRouter();
            WarmChainlingPresentation();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            SubscribeInputLocks();
            ResolveRouter();
            SubscribeRouter();
            SubscribeInputActions();
            CloseMenuIfBlocked(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            UnsubscribeInputLocks();
            UnsubscribeRouter();
            UnsubscribeInputActions();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CloseMenuIfBlocked(scene);
            if (!GameplayMenuScenePolicy.BlocksPlayerMenu(scene))
                WarmChainlingPresentation();
        }

        private void HandleActiveSceneChanged(Scene previous, Scene next)
        {
            CloseMenuIfBlocked(next);
        }

        private void CloseMenuIfBlocked(Scene scene)
        {
            if (!GameplayMenuScenePolicy.BlocksPlayerMenu(scene))
                return;

            PlayerMenuController menu = EnsureMenuInstance();
            if (menu != null && menu.IsOpen)
                menu.Close();
        }

        private void SubscribeInputLocks()
        {
            if (inputLockService != null)
                return;

            inputLockService = InputLockService.Instance;
            if (inputLockService != null)
                inputLockService.LockAcquired += HandleInputLockAcquired;
        }

        private void UnsubscribeInputLocks()
        {
            if (inputLockService != null)
                inputLockService.LockAcquired -= HandleInputLockAcquired;
            inputLockService = null;
        }

        private void HandleInputLockAcquired(InputLockChannel channel, UnityEngine.Object owner)
        {
            if (channel != InputLockChannel.All && channel != InputLockChannel.PlayerMenu)
                return;

            PlayerMenuController menu = EnsureMenuInstance();
            if (menu == null || owner == menu || !menu.IsOpen)
                return;

            menu.Close();
        }

        private void Update()
        {
            if (!isSubscribed)
            {
                ResolveRouter();
                SubscribeRouter();
            }

            if (!isInputActionSubscribed)
                SubscribeInputActions();
        }

        public void OpenMenu()
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            EnsureMenuInstance()?.Open();
        }

        public void CloseMenu()
        {
            EnsureMenuInstance()?.Close();
        }

        public void ToggleMenu()
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            PlayerMenuController menu = EnsureMenuInstance();
            if (menu == null) return;

            if (menu.IsOpen) menu.Close();
            else menu.Open();
        }

        private PlayerMenuController EnsureMenuInstance()
        {
            if (playerMenuInstance != null)
            {
                NormalizeMenuTransform(playerMenuInstance);
                return playerMenuInstance;
            }

            playerMenuInstance = GetComponentInChildren<PlayerMenuController>(true);
            if (playerMenuInstance == null)
                playerMenuInstance = FindExistingSceneMenu();

            if (playerMenuInstance == null && playerMenuPrefab != null)
            {
                playerMenuInstance = Instantiate(playerMenuPrefab, transform);
                playerMenuInstance.name = playerMenuPrefab.name;
            }

            if (playerMenuInstance == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[PersistentPlayerMenuHost] No PlayerMenu prefab or scene instance found.", this);
                return null;
            }

            NormalizeMenuTransform(playerMenuInstance);
            ApplyDevBootstrap(playerMenuInstance);
            return playerMenuInstance;
        }

        private void WarmChainlingPresentation()
        {
            if (playerMenuInstance != null)
                playerMenuInstance.WarmChainlingPresentation();
        }

        private void ApplyDevBootstrap(PlayerMenuController menu)
        {
            if (menu == null)
                return;

            if (devBootstrap == null)
                devBootstrap = GetComponent<PlayerMenuDevBootstrap>();

            devBootstrap?.ApplyTo(menu);
        }

        private PlayerMenuController FindExistingSceneMenu()
        {
            PlayerMenuController[] menus = FindObjectsOfType<PlayerMenuController>(true);
            for (int i = 0; i < menus.Length; i++)
            {
                PlayerMenuController menu = menus[i];
                if (menu == null) continue;
                if (!menu.gameObject.scene.IsValid()) continue;
                return menu;
            }

            return null;
        }

        private void NormalizeMenuTransform(PlayerMenuController menu)
        {
            if (menu == null) return;

            Transform menuTransform = menu.transform;
            if (menuTransform.parent != transform)
                menuTransform.SetParent(transform, false);

            if (menuTransform.localScale == Vector3.zero)
                menuTransform.localScale = Vector3.one;

            if (menuTransform is RectTransform rectTransform)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }
        }

        private void ResolveRouter()
        {
            if (mobileControlRouter != null) return;

            mobileControlRouter = GetComponentInChildren<MobileControlRouter>(true);
            if (mobileControlRouter == null)
                mobileControlRouter = FindObjectOfType<MobileControlRouter>(true);
        }

        private void SubscribeRouter()
        {
            if (isSubscribed || mobileControlRouter == null) return;

            if (openOnMenuButton)
                mobileControlRouter.MenuRouted += HandleMenuButton;
            if (routeStartToPauseMenu)
                mobileControlRouter.StartRouted += HandlePauseMenuToggle;
            if (openOnSelectButton)
                mobileControlRouter.SelectRouted += HandleSelectButton;
            if (closeOnBackButton)
                mobileControlRouter.BackRouted += HandleBackButton;
            mobileControlRouter.WalletRouted += HandleWalletConnectRequest;

            isSubscribed = true;
        }

        private void UnsubscribeRouter()
        {
            if (!isSubscribed || mobileControlRouter == null) return;

            mobileControlRouter.MenuRouted -= HandleMenuButton;
            mobileControlRouter.StartRouted -= HandlePauseMenuToggle;
            mobileControlRouter.SelectRouted -= HandleSelectButton;
            mobileControlRouter.BackRouted -= HandleBackButton;
            mobileControlRouter.WalletRouted -= HandleWalletConnectRequest;
            isSubscribed = false;
        }

        private void SubscribeInputActions()
        {
            if (!enableInputActions || isInputActionSubscribed) return;

            InputActionAsset asset = ResolveInputActions();
            if (asset == null) return;

            InputActionMap actionMap = asset.FindActionMap(inputActionMapName, false);
            if (actionMap == null)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[PersistentPlayerMenuHost] Input action map '{inputActionMapName}' not found.", this);
                return;
            }

            toggleMenuAction = FindAction(actionMap, toggleMenuActionName);
            pauseMenuAction = FindAction(actionMap, pauseMenuActionName);
            walletConnectAction = FindAction(actionMap, walletConnectActionName);

            bool subscribedAny = false;
            if (toggleMenuAction != null)
            {
                toggleMenuAction.performed += HandleToggleMenuActionPerformed;
                toggleMenuAction.Enable();
                subscribedAny = true;
            }

            if (pauseMenuAction != null && pauseMenuAction != toggleMenuAction)
            {
                pauseMenuAction.performed += HandlePauseActionPerformed;
                pauseMenuAction.Enable();
                subscribedAny = true;
            }

            if (walletConnectAction != null && walletConnectAction != toggleMenuAction && walletConnectAction != pauseMenuAction)
            {
                walletConnectAction.performed += HandleWalletConnectActionPerformed;
                walletConnectAction.Enable();
                subscribedAny = true;
            }

            if (!subscribedAny) return;

            actionMap.Enable();
            isInputActionSubscribed = true;
        }

        private void UnsubscribeInputActions()
        {
            if (!isInputActionSubscribed) return;

            if (toggleMenuAction != null)
                toggleMenuAction.performed -= HandleToggleMenuActionPerformed;
            if (pauseMenuAction != null && pauseMenuAction != toggleMenuAction)
                pauseMenuAction.performed -= HandlePauseActionPerformed;
            if (walletConnectAction != null && walletConnectAction != toggleMenuAction && walletConnectAction != pauseMenuAction)
                walletConnectAction.performed -= HandleWalletConnectActionPerformed;

            toggleMenuAction = null;
            pauseMenuAction = null;
            walletConnectAction = null;
            isInputActionSubscribed = false;
        }

        private InputActionAsset ResolveInputActions()
        {
            if (inputActions != null)
                return inputActions;

            InputSystemUIInputModule localModule = GetComponent<InputSystemUIInputModule>();
            if (localModule != null && localModule.actionsAsset != null)
                inputActions = localModule.actionsAsset;

            if (inputActions == null)
            {
                InputSystemUIInputModule inputModule = FindObjectOfType<InputSystemUIInputModule>(true);
                if (inputModule != null && inputModule.actionsAsset != null)
                    inputActions = inputModule.actionsAsset;
            }

            if (inputActions == null)
                inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");

            return inputActions;
        }

        private static InputAction FindAction(InputActionMap actionMap, params string[] actionNames)
        {
            if (actionMap == null || actionNames == null)
                return null;

            for (int i = 0; i < actionNames.Length; i++)
            {
                string actionName = actionNames[i];
                if (string.IsNullOrWhiteSpace(actionName)) continue;

                InputAction action = actionMap.FindAction(actionName, false);
                if (action != null)
                    return action;
            }

            return null;
        }

        private void HandleToggleMenuActionPerformed(InputAction.CallbackContext context)
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            HandleInventoryMenuRequest();
        }

        private void HandlePauseActionPerformed(InputAction.CallbackContext context)
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            HandlePauseMenuToggle();
        }

        private void HandleWalletConnectActionPerformed(InputAction.CallbackContext context)
        {
            HandleWalletConnectRequest();
        }

        private void HandleMenuButton()
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            HandleInventoryMenuRequest();
        }

        private void HandleInventoryMenuRequest()
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            PlayerMenuController menu = EnsureMenuInstance();
            if (menu == null)
                return;

            if (menu.IsOpen)
            {
                LogMenuOwnedRouteIgnored("PlayerMenu");
                return;
            }

            if (IsDuplicateMenuToggle())
                return;

            RecordMenuToggle();
            int inventoryTabIndex = ResolveInventoryTabIndex(menu);
            if (inventoryTabIndex >= 0)
                menu.OpenToTab(inventoryTabIndex);
            else
                menu.Open();
        }

        private void HandlePauseMenuToggle()
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            PlayerMenuController menu = EnsureMenuInstance();
            if (menu == null)
                return;

            if (menu.IsOpen)
            {
                LogMenuOwnedRouteIgnored("StartMenu");
                return;
            }

            if (IsDuplicateMenuToggle())
                return;

            RecordMenuToggle();
            menu.Open();
        }

        private int ResolveInventoryTabIndex(PlayerMenuController menu)
        {
            if (menu == null || string.IsNullOrWhiteSpace(inventoryTabId))
                return -1;

            return menu.ResolveTabIndex(inventoryTabId);
        }

        private void HandleSelectButton()
        {
            if (IsGameplayMenuBlockedInCurrentScene())
                return;

            PlayerMenuController menu = EnsureMenuInstance();
            if (menu == null)
                return;

            if (menu.IsOpen)
            {
                LogMenuOwnedRouteIgnored("Select");
                return;
            }

            menu.Open();
        }

        private void HandleBackButton()
        {
            PlayerMenuController menu = EnsureMenuInstance();
            if (menu == null || !menu.IsOpen) return;

            LogMenuOwnedRouteIgnored("Back");
        }

        private void HandleWalletConnectRequest()
        {
            if (IsResolvedMenuOpen())
            {
                LogMenuOwnedRouteIgnored("WalletConnect");
                return;
            }

            onWalletConnectRequested?.Invoke();

            if (enableDebugLogs)
                Debug.Log("[PersistentPlayerMenuHost] Wallet connect requested.", this);
        }

        private bool IsResolvedMenuOpen()
        {
            PlayerMenuController menu = playerMenuInstance;
            if (menu == null)
                menu = GetComponentInChildren<PlayerMenuController>(true);

            return menu != null && menu.IsOpen;
        }

        private static bool IsGameplayMenuBlockedInCurrentScene()
        {
            return GameplayMenuScenePolicy.BlocksPlayerMenuInActiveScene();
        }

        private void LogMenuOwnedRouteIgnored(string routeName)
        {
            if (!enableDebugLogs)
                return;

            Debug.Log(
                $"[PersistentPlayerMenuHost] Ignored gameplay route '{routeName}' while Player Menu is open. " +
                "The UI action map owns in-menu actions.",
                this);
        }

        private bool IsDuplicateMenuToggle()
        {
            if (Time.frameCount == lastMenuToggleFrame)
                return true;

            return duplicateToggleWindowSeconds > 0f
                && lastMenuToggleTime >= 0f
                && Time.unscaledTime - lastMenuToggleTime <= duplicateToggleWindowSeconds;
        }

        private void RecordMenuToggle()
        {
            lastMenuToggleFrame = Time.frameCount;
            lastMenuToggleTime = Time.unscaledTime;
        }
    }
}
