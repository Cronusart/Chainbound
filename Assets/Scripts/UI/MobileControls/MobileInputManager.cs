using System;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR || UNITY_ANDROID
using PlaySolanaSdk;
#endif

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Façade for Signal Jam mobile controls.
    ///
    /// Role:
    /// - Resolves input priority (PSG1 physical device &gt; mobile virtual &gt; keyboard/gamepad).
    /// - Exposes unified movement and sprint queries consumed by <c>PlayerMovement3D</c>.
    /// - Routes action-button taps to systems via <see cref="Action"/> events (no scene loads here).
    /// - Listens to <see cref="InputSystem.onDeviceChange"/> so PSG1 hotplug updates HUD instantly.
    ///
    /// Visibility and safe-area handling belong to <c>MobileHudVisibilityController</c> and
    /// <c>SafeAreaAdapter</c>. Landscape orientation is enforced by <c>MobileBootstrap</c>.
    /// </summary>
    public class MobileInputManager : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Hybrid disc + D-pad overlay control. Primary movement source on phones.")]
        [SerializeField] private HybridMovementPad movementPad;

        [Header("Action Buttons (Right Cluster)")]
        [SerializeField] private GameboyButton buttonX;
        [SerializeField] private GameboyButton buttonY;
        [SerializeField] private GameboyButton buttonA;
        [SerializeField] private GameboyButton buttonB;

        [Header("Movement Buttons")]
        [Tooltip("Deprecated: jump is now mapped to ButtonB in the six-button HUD layout.")]
        [SerializeField] private GameboyButton jumpButton;
        [Tooltip("Deprecated: sprint is now mapped to ButtonX hold in the six-button HUD layout.")]
        [SerializeField] private GameboyButton sprintButton;

        [Header("System Buttons")]
        [SerializeField] private GameboyButton startButton;
        [SerializeField] private GameboyButton selectButton;

        [Header("Wallet")]
        [Tooltip("Deprecated wallet button. Select opens the RAM shop; this button still raises OnWalletPressed.")]
        [SerializeField] private GameboyButton solanaButton;

        [Header("Settings")]
        [SerializeField] private bool enableMobileControls = true;
        [SerializeField] private bool autoDetectPlatform = true;
        [Tooltip("Allows on-screen HUD buttons to route while testing in the Unity Editor without making mobile movement override keyboard/gamepad movement.")]
        [SerializeField] private bool enableEditorHudButtonInput = true;
        [Tooltip("Optional editor-only override for testing the virtual movement pad. Keep disabled for normal keyboard/gamepad editor play.")]
        [SerializeField] private bool enableEditorMovementInput = false;
        [SerializeField] private bool enableDebugLogs = false;
        [SerializeField, Min(0.05f)] private float doubleTapWindowSeconds = 0.32f;

        [Header("HUD Service")]
        [Tooltip("Optional explicit MobileHudService reference. When null the manager resolves MobileHudService.Instance lazily so PSG1 hotplug can push a Hidden context token.")]
        [SerializeField] private MobileHudService hudService;

        [Header("Input Actions")]
        [Tooltip("Action asset holding the Player map. Must be assigned: the asset is not under a Resources folder, so the name-based fallback cannot find it.")]
        [SerializeField] private InputActionAsset inputActions;

        // Input System bindings
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction sprintAction;

        // Runtime state
        private bool isMobilePlatform;
        private bool isPlaySolanaDevice;
        private bool isSprintingMobile;
        private readonly MobileSprintLock sprintLock = new MobileSprintLock();
        private Vector2 currentMoveInput;
        private bool wasMobileControlsActive;

        // PSG1 "Hidden" token held for the duration of a PSG1 device connection.
        // This lets the HUD react to hotplug without every subsystem wiring up
        // OnPsg1ConnectionChanged directly.
        private MobileHudToken psg1HiddenToken;
        private bool hasPsg1HiddenToken;

        #region Events (consumed by MobileControlRouter and other systems)

        /// <summary>Legacy back / cancel request. Menu screens consume raw B presses directly.</summary>
        public event Action OnBackPressed;
        /// <summary>Open in-game menu (mapped to the Y face button).</summary>
        public event Action OnMenuPressed;
        /// <summary>Special / secondary interaction (mapped to the X face button).</summary>
        public event Action OnSecondaryInteract;
        /// <summary>Primary interact / confirm (mapped to the A face button).</summary>
        public event Action OnPrimaryInteract;
        /// <summary>Start button: system menu / pause.</summary>
        public event Action OnStartPressed;
        /// <summary>Select button. Gameplay routes it to the RAM shop where the player menu is allowed.</summary>
        public event Action OnSelectPressed;
        /// <summary>Wallet button.</summary>
        public event Action OnWalletPressed;
        /// <summary>Raised whenever PSG1 hotplug state changes. Payload: true if PSG1 now connected.</summary>
        public event Action<bool> OnPsg1ConnectionChanged;

        /// <summary>Raw mobile X button press. In overworld this maps to Sprint hold; in menus, Special Interaction.</summary>
        public event Action OnXPressed;
        /// <summary>Raw mobile B button press. In overworld this maps to Jump; in menus, Back/Cancel.</summary>
        public event Action OnBPressed;
        /// <summary>Raw mobile B button release. Used by held-jump movement timing.</summary>
        public event Action OnBReleased;
        /// <summary>Raw mobile A button press. In overworld this maps to Interact; in menus, Confirm.</summary>
        public event Action OnAPressed;
        /// <summary>Raw mobile Y button press. In overworld this maps to player menu; in menus, Details.</summary>
        public event Action OnYPressed;

        #endregion

        #region Public accessors

        public Vector2 MoveInput => currentMoveInput;

        /// <summary>
        /// True when on-screen controls should drive gameplay input. False on non-mobile,
        /// when a PSG1 physical device is connected, or when <see cref="MobileHudService"/>
        /// reports a suppressing context (Hidden / Menu / Cutscene / Dialogue).
        /// Consumed by <c>PlayerMovement3D</c> to decide whether to read the mobile pad.
        /// </summary>
        public bool IsMobileControlsActive
            => enableMobileControls
               && IsMobileMovementPlatformEligible
               && !isPlaySolanaDevice
               && !IsHudSuppressed;

        public bool IsPlaySolanaDevice => isPlaySolanaDevice;
        public HybridMovementPad MovementPad => movementPad;
        public GameboyButton JumpButton => jumpButton;
        public GameboyButton SprintButton => buttonX != null ? buttonX : sprintButton;
        public bool SprintLocked => sprintLock.IsLocked;
        public float DoubleTapWindowSeconds => doubleTapWindowSeconds;

        #endregion

        private bool IsHudSuppressed
        {
            get
            {
                var svc = ResolveHudService();
                return svc != null && svc.IsSuppressed;
            }
        }

        private MobileHudService ResolveHudService()
        {
            if (hudService == null)
            {
                hudService = MobileHudService.Instance;
            }
            return hudService;
        }

        private bool IsMobileMovementPlatformEligible
        {
            get
            {
                bool eligible = isMobilePlatform;
#if UNITY_EDITOR
                eligible |= enableEditorMovementInput;
#endif
                return eligible;
            }
        }

        private bool CanRouteHudButtonInput
        {
            get
            {
                if (!enableMobileControls || isPlaySolanaDevice) return false;

                bool eligible = isMobilePlatform;
#if UNITY_EDITOR
                eligible |= enableEditorHudButtonInput;
#endif
                return eligible && IsHudButtonContextAllowed;
            }
        }

        private bool IsHudButtonContextAllowed
        {
            get
            {
                MobileHudService svc = ResolveHudService();
                if (svc == null) return true;

                return svc.CurrentContext == MobileHudContext.Overworld
                    || svc.CurrentContext == MobileHudContext.Battle
                    || svc.CurrentContext == MobileHudContext.Menu
                    || svc.CurrentContext == MobileHudContext.Dialogue;
            }
        }

        private void Awake()
        {
            ResolvePlatform();
            ResolveInputActions();
            SubscribeButtons();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
            RefreshPsg1Detection();
            wasMobileControlsActive = IsMobileControlsActive;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
            ReleasePsg1HiddenToken();
        }

        private void OnDestroy()
        {
            UnsubscribeButtons();
            ReleasePsg1HiddenToken();
        }

        private void Update()
        {
            bool mobileActive = IsMobileControlsActive;
            if (wasMobileControlsActive && !mobileActive && movementPad != null)
                movementPad.ResetPad();

            wasMobileControlsActive = mobileActive;

            if (!mobileActive)
            {
                currentMoveInput = Vector2.zero;
                return;
            }

            currentMoveInput = movementPad != null ? movementPad.GetInput() : Vector2.zero;
        }

        #region Platform / device detection

        private void ResolvePlatform()
        {
            if (autoDetectPlatform)
            {
                isMobilePlatform = Application.isMobilePlatform
                                   || Application.platform == RuntimePlatform.Android
                                   || Application.platform == RuntimePlatform.IPhonePlayer;
            }
            else
            {
                isMobilePlatform = enableMobileControls;
            }
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added
                || change == InputDeviceChange.Removed
                || change == InputDeviceChange.Reconnected
                || change == InputDeviceChange.Disconnected)
            {
                RefreshPsg1Detection();
            }
        }

        private void RefreshPsg1Detection()
        {
            bool previous = isPlaySolanaDevice;
            isPlaySolanaDevice = DetectPsg1();
            if (previous == isPlaySolanaDevice) return;

            if (enableDebugLogs)
            {
                Debug.Log($"[MobileInputManager] PSG1 connection changed: {previous} -> {isPlaySolanaDevice}");
            }

            // Push the transition through MobileHudService so the HUD hides itself
            // automatically, without the visibility controller having to subscribe here.
            if (isPlaySolanaDevice)
            {
                AcquirePsg1HiddenToken();
            }
            else
            {
                ReleasePsg1HiddenToken();
            }

            OnPsg1ConnectionChanged?.Invoke(isPlaySolanaDevice);
        }

        private void AcquirePsg1HiddenToken()
        {
            if (hasPsg1HiddenToken) return;
            var svc = ResolveHudService();
            if (svc == null) return;

            psg1HiddenToken = svc.AcquireContext(MobileHudContext.Hidden, "MobileInputManager.PSG1Connected");
            hasPsg1HiddenToken = true;
        }

        private void ReleasePsg1HiddenToken()
        {
            if (!hasPsg1HiddenToken) return;
            psg1HiddenToken.Release();
            hasPsg1HiddenToken = false;
        }

        private bool DetectPsg1()
        {
#if UNITY_EDITOR || UNITY_ANDROID
            try
            {
                if (PSG1.current != null) return true;

                foreach (var device in InputSystem.devices)
                {
                    var product = device?.description.product;
                    if (string.IsNullOrEmpty(product)) continue;
                    string lower = product.ToLowerInvariant();
                    if (lower.Contains("psg1") || lower.Contains("playsolana"))
                    {
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                if (enableDebugLogs)
                    Debug.LogWarning($"[MobileInputManager] PSG1 detection failed: {e.Message}");
            }
#endif
            return false;
        }

        #endregion

        #region Input System wiring

        private void ResolveInputActions()
        {
            if (inputActions == null)
            {
                inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            }

            if (inputActions == null)
            {
                Debug.LogWarning(
                    "[MobileInputManager] No InputActionAsset assigned. PSG1 movement/sprint and the keyboard fallback " +
                    "will report neutral. Assign the asset holding the 'Player' action map in the inspector.",
                    this);
                return;
            }

            playerMap = inputActions.FindActionMap("Player");
            if (playerMap == null)
            {
                Debug.LogWarning($"[MobileInputManager] Action map 'Player' not found in '{inputActions.name}'.", this);
                return;
            }

            moveAction = playerMap.FindAction("Move");
            sprintAction = playerMap.FindAction("Sprint");
        }

        #endregion

        #region Button wiring

        private void SubscribeButtons()
        {
            if (buttonX != null)
            {
                buttonX.OnButtonDown += HandleXPressed;
                buttonX.OnButtonUp += HandleXReleased;
            }
            if (buttonY != null) buttonY.OnButtonDown += HandleYPressed;
            if (buttonA != null)
            {
                buttonA.OnButtonDown += HandleAPressed;
                buttonA.OnButtonUp += HandleAReleased;
            }
            if (buttonB != null)
            {
                buttonB.OnButtonDown += HandleBPressed;
                buttonB.OnButtonUp += HandleBReleased;
            }
            if (startButton != null) startButton.OnButtonDown += HandleStartPressed;
            if (selectButton != null) selectButton.OnButtonDown += HandleSelectPressed;
        }

        private void UnsubscribeButtons()
        {
            if (buttonX != null)
            {
                buttonX.OnButtonDown -= HandleXPressed;
                buttonX.OnButtonUp -= HandleXReleased;
            }
            if (buttonY != null) buttonY.OnButtonDown -= HandleYPressed;
            if (buttonA != null)
            {
                buttonA.OnButtonDown -= HandleAPressed;
                buttonA.OnButtonUp -= HandleAReleased;
            }
            if (buttonB != null)
            {
                buttonB.OnButtonDown -= HandleBPressed;
                buttonB.OnButtonUp -= HandleBReleased;
            }
            if (startButton != null) startButton.OnButtonDown -= HandleStartPressed;
            if (selectButton != null) selectButton.OnButtonDown -= HandleSelectPressed;
        }

        private void HandleXPressed()
        {
            if (!CanRouteHudButtonInput) return;
            sprintLock.WindowSeconds = doubleTapWindowSeconds;
            sprintLock.RegisterDown(Time.unscaledTime);
            isSprintingMobile = true;
            OnXPressed?.Invoke();
            OnSecondaryInteract?.Invoke();
            if (enableDebugLogs) Debug.Log("[MobileInputManager] X pressed / sprint held (mobile)");
        }

        private void HandleXReleased()
        {
            if (!CanRouteHudButtonInput) return;
            isSprintingMobile = false;
            if (enableDebugLogs) Debug.Log("[MobileInputManager] X released / sprint stopped (mobile)");
        }

        private void HandleYPressed()
        {
            if (!CanRouteHudButtonInput) return;
            OnYPressed?.Invoke();
            OnMenuPressed?.Invoke();
        }

        private void HandleAPressed()
        {
            if (!CanRouteHudButtonInput) return;
            OnAPressed?.Invoke();
            OnPrimaryInteract?.Invoke();
            if (enableDebugLogs) Debug.Log("[MobileInputManager] A pressed / primary interact (mobile)");
        }

        private void HandleAReleased()
        {
            if (!CanRouteHudButtonInput) return;
            if (enableDebugLogs) Debug.Log("[MobileInputManager] A released (mobile)");
        }

        private void HandleBPressed()
        {
            if (!CanRouteHudButtonInput) return;
            OnBPressed?.Invoke();
        }

        private void HandleBReleased()
        {
            if (!CanRouteHudButtonInput) return;
            OnBReleased?.Invoke();
        }

        private void HandleStartPressed()
        {
            if (!CanRouteHudButtonInput) return;
            OnStartPressed?.Invoke();
        }

        private void HandleSelectPressed()
        {
            if (!CanRouteHudButtonInput) return;
            OnSelectPressed?.Invoke();
        }

        private void HandleWalletPressed()
        {
            if (!CanRouteHudButtonInput) return;
            OnWalletPressed?.Invoke();
        }

        private void HandleSprintPressed()
        {
            if (!IsMobileControlsActive) return;
            isSprintingMobile = true;
            if (enableDebugLogs) Debug.Log("[MobileInputManager] Sprint pressed (mobile)");
        }

        private void HandleSprintReleased()
        {
            if (!IsMobileControlsActive) return;
            isSprintingMobile = false;
            if (enableDebugLogs) Debug.Log("[MobileInputManager] Sprint released (mobile)");
        }

        #endregion

        #region Public query API

        /// <summary>
        /// Unified movement vector. Priority: PSG1 device &gt; mobile virtual pad &gt; Input System.
        /// </summary>
        public Vector2 GetMovementInput()
        {
            if (isPlaySolanaDevice && moveAction != null)
            {
                return moveAction.ReadValue<Vector2>();
            }

            if (IsMobileControlsActive && movementPad != null)
            {
                return movementPad.GetInput();
            }

            return moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        }

        /// <summary>
        /// Unified sprint query. Priority: PSG1 device &gt; mobile toggle/hold state &gt; Input System.
        /// </summary>
        public bool IsSprinting()
        {
            if (isPlaySolanaDevice && sprintAction != null)
            {
                return sprintAction.IsPressed();
            }

            if (IsMobileControlsActive)
            {
                if (buttonX != null)
                    return sprintLock.IsLocked || buttonX.IsPressed;
                return sprintLock.IsLocked || isSprintingMobile;
            }

            return sprintAction != null && sprintAction.IsPressed();
        }

        /// <summary>Enable or disable mobile controls. No-op when a PSG1 device is present.</summary>
        public void SetMobileControlsEnabled(bool enabled)
        {
            if (isPlaySolanaDevice)
            {
                if (enableDebugLogs)
                    Debug.LogWarning("[MobileInputManager] Cannot toggle mobile controls while PSG1 is connected.");
                return;
            }
            enableMobileControls = enabled;
        }

        #endregion
    }
}
