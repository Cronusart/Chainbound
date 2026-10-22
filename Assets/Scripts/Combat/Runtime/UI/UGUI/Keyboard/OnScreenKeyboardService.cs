using System;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared text-entry service that keeps TMP_InputField as the authoritative
    /// backing store while routing input through an authored in-game keyboard.
    /// </summary>
    public class OnScreenKeyboardService : MonoBehaviour
    {
        private static OnScreenKeyboardService instance;

        [Header("UI")]
        [SerializeField] private OnScreenKeyboardUI keyboardUI;

        [Header("Feedback")]
        [SerializeField] private MonoBehaviour uiSfxBehaviour;
        [SerializeField] private MonoBehaviour audioBridgeBehaviour;
        [Tooltip("Played when a character/shift/clear key successfully applies.")]
        [SerializeField] private SoundEventSO keyPressSound;
        [Tooltip("Played when input is rejected (max length, invalid character, empty backspace, etc.).")]
        [SerializeField] private SoundEventSO inputDeniedSound;
        [Tooltip("Played when Backspace successfully deletes a character.")]
        [SerializeField] private SoundEventSO backspaceSound;
        [Tooltip("Played when Confirm succeeds.")]
        [SerializeField] private SoundEventSO confirmSound;
        [Tooltip("Played when Cancel closes the keyboard.")]
        [SerializeField] private SoundEventSO cancelSound;
        [Tooltip("Played when D-Pad / arrow focus moves between keys (PSG1 / gamepad).")]
        [SerializeField] private SoundEventSO navigateSound;

        [Header("Input Lock")]
        [SerializeField] private bool acquireInputLock = true;
        [SerializeField] private InputLockChannel inputLockChannel = InputLockChannel.All;
        [SerializeField] private string inputLockReason = "OnScreenKeyboard";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private IKeyboardInputTarget currentTarget;
        private KeyboardSessionOptions currentSessionOptions;
        private bool shiftEnabled;
        private MobileHudToken mobileHudSuppressionToken;
        private InputLockHandle inputLockHandle;
        private bool hasInputLock;
        private UiHapticsService cachedUiHapticsService;
        private MonoBehaviour cachedAudioBridgeBehaviour;
        private bool hardwareInputSubscribed;
        private Keyboard subscribedKeyboard;

        private static bool hasLoggedEventSystemUiHealth;

        /// <summary>Raised after a keyboard session becomes visible.</summary>
        public static event Action SessionOpened;

        /// <summary>Raised after a keyboard session fully ends.</summary>
        public static event Action SessionClosed;

        public static OnScreenKeyboardService Instance
        {
            get
            {
                if (instance == null)
                    instance = UnityEngine.Object.FindFirstObjectByType<OnScreenKeyboardService>(FindObjectsInactive.Include);

                return instance;
            }
        }

        public bool IsOpen => currentTarget != null && keyboardUI != null && keyboardUI.IsVisible;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[OnScreenKeyboardService] Duplicate instance found. Destroying {gameObject.name}");
                Destroy(gameObject);
                return;
            }

            instance = this;

            if (keyboardUI == null)
                keyboardUI = UnityEngine.Object.FindFirstObjectByType<OnScreenKeyboardUI>(FindObjectsInactive.Include);

            keyboardUI?.SetService(this);
            keyboardUI?.Hide();
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            // Reassert the lock if an external owner released it unexpectedly.
            EnsureInputLockHeld();

            if (!hardwareInputSubscribed)
                SubscribeHardwareTextInput();

            ProcessHardwareSpecialKeys();
        }

        private void OnDestroy()
        {
            bool wasOpen = IsOpen;
            UnsubscribeHardwareTextInput();
            ReleaseInputLockIfHeld();
            ReleaseMobileHudSuppressionIfHeld();

            if (instance == this)
                instance = null;

            if (wasOpen)
                SessionClosed?.Invoke();
        }

        public bool BeginSession(TMP_InputField inputField, KeyboardSessionOptions options)
        {
            return BeginSession(inputField != null ? new TMPInputFieldKeyboardTarget(inputField) : null, options);
        }

        public bool BeginSession(IKeyboardInputTarget target, KeyboardSessionOptions options)
        {
            if (target == null || !target.IsValid)
            {
                Debug.LogWarning("[OnScreenKeyboardService] Cannot begin a keyboard session without a valid target.");
                return false;
            }

            EnsureKeyboardUi();
            if (keyboardUI == null)
            {
                Debug.LogWarning("[OnScreenKeyboardService] Keyboard UI is missing. Falling back to standard input.");
                return false;
            }

            // Replace any existing session cleanly (releases locks/HUD, hides UI, fires SessionClosed).
            if (currentTarget != null || currentSessionOptions != null || (keyboardUI != null && keyboardUI.IsVisible))
                EndSession();

            currentTarget = target;
            currentSessionOptions = options ?? new KeyboardSessionOptions();
            shiftEnabled = currentSessionOptions.StartShifted;

            mobileHudSuppressionToken = MobileHudService.Instance.AcquireContext(
                MobileHudContext.Hidden,
                "OnScreenKeyboardService");
            AcquireInputLock();

            currentTarget.Focus();
            RefreshUi();
            keyboardUI.Show(currentSessionOptions, shiftEnabled, GetCurrentText(), CanSubmitCurrentText());
            SubscribeHardwareTextInput();

            if (enableDebugLogs)
                Debug.Log("[OnScreenKeyboardService] Keyboard session started.");

            LogEventSystemUiHealthOnce();
            SessionOpened?.Invoke();

            return true;
        }

        private void LogEventSystemUiHealthOnce()
        {
            if (hasLoggedEventSystemUiHealth)
                return;

            hasLoggedEventSystemUiHealth = true;

            if (EventSystem.current == null)
            {
                Debug.LogError(
                    "[OnScreenKeyboardService] EventSystem.current is null — UGUI raycasts and Button clicks will not run. " +
                    "Ensure a scene loaded before gameplay includes an EventSystem (Bootstrap is the usual source).",
                    this);
                return;
            }

            GameObject esGo = EventSystem.current.gameObject;
            bool hasInputSystemUi = EventSystem.current.GetComponent<InputSystemUIInputModule>() != null;
            bool hasStandalone = EventSystem.current.GetComponent<StandaloneInputModule>() != null;
            if (!hasInputSystemUi && !hasStandalone)
            {
                Debug.LogWarning(
                    "[OnScreenKeyboardService] EventSystem has no InputSystemUIInputModule or StandaloneInputModule — UI pointer input will not dispatch.",
                    esGo);
            }
        }

        public void EndSession()
        {
            bool hadSession = currentTarget != null || currentSessionOptions != null;

            UnsubscribeHardwareTextInput();

            currentTarget = null;
            currentSessionOptions = null;

            if (hadSession)
                keyboardUI?.Hide();

            ReleaseInputLockIfHeld();
            ReleaseMobileHudSuppressionIfHeld();

            if (hadSession && enableDebugLogs)
                Debug.Log("[OnScreenKeyboardService] Keyboard session ended.");

            if (hadSession)
                SessionClosed?.Invoke();
        }

        private void AcquireInputLock()
        {
            if (!acquireInputLock || hasInputLock)
                return;

            InputLockService service = InputLockService.Instance;
            if (service == null)
            {
                Debug.LogWarning("[OnScreenKeyboardService] InputLockService missing; player movement may not be blocked.", this);
                return;
            }

            inputLockHandle = service.AcquireLock(inputLockReason, inputLockChannel, this);
            hasInputLock = inputLockHandle.IsValid;
        }

        private void EnsureInputLockHeld()
        {
            if (!acquireInputLock)
                return;

            InputLockService service = InputLockService.Instance;
            if (service == null)
                return;

            // Keep the local handle state synchronized with the service.
            if (hasInputLock && service.IsLocked(inputLockChannel))
                return;

            hasInputLock = false;
            inputLockHandle = default;
            AcquireInputLock();
        }

        private void ReleaseInputLockIfHeld()
        {
            if (!hasInputLock)
                return;

            inputLockHandle.Release();
            inputLockHandle = default;
            hasInputLock = false;
        }

        private void ReleaseMobileHudSuppressionIfHeld()
        {
            if (!mobileHudSuppressionToken.IsValid)
                return;

            mobileHudSuppressionToken.Release();
            mobileHudSuppressionToken = default;
        }

        public void HandleKeyPressed(VirtualKeyButton keyButton)
        {
            if (keyButton == null || currentTarget == null)
                return;

            switch (keyButton.KeyType)
            {
                case VirtualKeyType.Character:
                    AppendValue(keyButton.GetOutput(shiftEnabled));
                    break;
                case VirtualKeyType.Shift:
                    shiftEnabled = !shiftEnabled;
                    RefreshUi();
                    PlayKeyPressFeedback();
                    break;
                case VirtualKeyType.Backspace:
                    Backspace();
                    break;
                case VirtualKeyType.Clear:
                    Clear();
                    break;
                case VirtualKeyType.Cancel:
                    Cancel();
                    break;
                case VirtualKeyType.Confirm:
                    Confirm();
                    break;
            }
        }

        /// <summary>
        /// Face-button / Escape cancel path used when EventSystem delivers Cancel
        /// or hardware Escape is pressed while a session is open.
        /// </summary>
        public void RequestCancel()
        {
            if (!IsOpen)
                return;

            Cancel();
        }

        /// <summary>
        /// D-Pad / arrow focus moved onto a different key.
        /// </summary>
        public void NotifyFocusMoved()
        {
            if (!IsOpen)
                return;

            PlayNavigateFeedback();
        }

        private void SubscribeHardwareTextInput()
        {
            if (hardwareInputSubscribed)
                return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            keyboard.onTextInput += HandleHardwareTextInput;
            subscribedKeyboard = keyboard;
            hardwareInputSubscribed = true;
        }

        private void UnsubscribeHardwareTextInput()
        {
            if (!hardwareInputSubscribed)
                return;

            if (subscribedKeyboard != null)
                subscribedKeyboard.onTextInput -= HandleHardwareTextInput;

            subscribedKeyboard = null;
            hardwareInputSubscribed = false;
        }

        private void HandleHardwareTextInput(char character)
        {
            if (!IsOpen || currentTarget == null)
                return;

            // Control characters are handled as specials in Update.
            if (char.IsControl(character))
                return;

            string value = character.ToString();
            if (keyboardUI != null && keyboardUI.TryFindCharacterKey(value, out VirtualKeyButton keyButton))
            {
                keyButton.PlayPressPulse();
                AppendValue(value);
                return;
            }

            PlayDeniedFeedback();
        }

        private void ProcessHardwareSpecialKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    ActivateKeyByType(VirtualKeyType.Cancel);
                    return;
                }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    ActivateKeyByType(VirtualKeyType.Confirm);
                    return;
                }

                if (keyboard.backspaceKey.wasPressedThisFrame)
                {
                    ActivateKeyByType(VirtualKeyType.Backspace);
                    return;
                }

                if (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame)
                {
                    ActivateKeyByType(VirtualKeyType.Shift);
                    return;
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
                RequestCancel();
        }

        private void ActivateKeyByType(VirtualKeyType keyType)
        {
            if (keyboardUI == null || !keyboardUI.TryFindKeyByType(keyType, out VirtualKeyButton keyButton))
            {
                if (keyType == VirtualKeyType.Cancel)
                    RequestCancel();
                return;
            }

            keyButton.PlayPressPulse();
            HandleKeyPressed(keyButton);
        }

        private void AppendValue(string value)
        {
            if (string.IsNullOrEmpty(value) || currentTarget == null)
                return;

            string currentText = GetCurrentText();
            string nextValue = currentText;
            int maxLength = GetEffectiveMaxLength();

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (currentSessionOptions != null && !currentSessionOptions.AllowsCharacter(character))
                    continue;

                if (maxLength > 0 && nextValue.Length >= maxLength)
                    break;

                nextValue += character;
            }

            if (string.Equals(nextValue, currentText, System.StringComparison.Ordinal))
            {
                PlayDeniedFeedback();
                return;
            }

            currentTarget.Text = nextValue;
            currentTarget.Focus();
            PublishTextChanged();
            RefreshUi();
            PlayKeyPressFeedback();
        }

        private void Backspace()
        {
            string currentText = GetCurrentText();
            if (string.IsNullOrEmpty(currentText))
            {
                PlayDeniedFeedback();
                return;
            }

            currentTarget.Text = currentText.Substring(0, currentText.Length - 1);
            currentTarget.Focus();
            PublishTextChanged();
            RefreshUi();
            PlayBackspaceFeedback();
        }

        private void Clear()
        {
            if (string.IsNullOrEmpty(GetCurrentText()))
            {
                PlayDeniedFeedback();
                return;
            }

            currentTarget.Text = string.Empty;
            currentTarget.Focus();
            PublishTextChanged();
            RefreshUi();
            PlayKeyPressFeedback();
        }

        private void Confirm()
        {
            if (!CanSubmitCurrentText())
            {
                PlayDeniedFeedback();
                return;
            }

            string submittedText = GetCurrentText();
            System.Action<string> onSubmitted = currentSessionOptions?.OnSubmitted;
            PlayConfirmFeedback();
            EndSession();
            onSubmitted?.Invoke(submittedText);
        }

        private void Cancel()
        {
            if (currentTarget == null && currentSessionOptions == null)
                return;

            System.Action onCancelled = currentSessionOptions?.OnCancelled;
            PlayCancelFeedback();
            EndSession();
            onCancelled?.Invoke();
        }

        /// <summary>
        /// A submitted value was refused. Keeps the session and backing field
        /// open, plays the back/cancel cue, and nudges the keyboard.
        /// </summary>
        public void NotifySubmissionRejected()
        {
            if (!IsOpen)
                return;

            PlayCancelFeedback();
            keyboardUI?.PlayRejectionNudge();
        }

        private void PublishTextChanged()
        {
            currentSessionOptions?.OnTextChanged?.Invoke(GetCurrentText());
        }

        private void RefreshUi()
        {
            keyboardUI?.Refresh(currentSessionOptions, shiftEnabled, GetCurrentText(), CanSubmitCurrentText());
        }

        private void EnsureKeyboardUi()
        {
            if (keyboardUI != null)
            {
                keyboardUI.SetService(this);
                return;
            }

            keyboardUI = UnityEngine.Object.FindFirstObjectByType<OnScreenKeyboardUI>(FindObjectsInactive.Include);
            keyboardUI?.SetService(this);
        }

        private string GetCurrentText()
        {
            return currentTarget != null ? currentTarget.Text ?? "" : "";
        }

        private bool CanSubmitCurrentText()
        {
            return currentSessionOptions == null || currentSessionOptions.CanSubmit(GetCurrentText());
        }

        private int GetEffectiveMaxLength()
        {
            int targetLimit = currentTarget != null ? Mathf.Max(0, currentTarget.CharacterLimit) : 0;
            int sessionLimit = currentSessionOptions != null ? Mathf.Max(0, currentSessionOptions.MaxLength) : 0;

            if (targetLimit <= 0)
                return sessionLimit;

            if (sessionLimit <= 0)
                return targetLimit;

            return Mathf.Min(targetLimit, sessionLimit);
        }

        private ICombatAudioBridge ResolveAudioBridge()
        {
            return CombatUiSfxResolver.ResolveAudioBridge(audioBridgeBehaviour, ref cachedAudioBridgeBehaviour);
        }

        private UiHapticsService GetHapticsService()
        {
            if (cachedUiHapticsService == null)
                cachedUiHapticsService = UiHapticsService.Instance;

            return cachedUiHapticsService;
        }

        private void PlayKeyPressFeedback()
        {
            PlayKeyboardSound(keyPressSound);
            GetHapticsService()?.TriggerSelection();
        }

        private void PlayBackspaceFeedback()
        {
            PlayKeyboardSound(backspaceSound != null ? backspaceSound : keyPressSound);
            GetHapticsService()?.TriggerSelection();
        }

        private void PlayDeniedFeedback()
        {
            PlayKeyboardSound(inputDeniedSound);
            GetHapticsService()?.TriggerWarning();
        }

        private void PlayConfirmFeedback()
        {
            PlayKeyboardSound(confirmSound);
            GetHapticsService()?.TriggerConfirm();
        }

        private void PlayCancelFeedback()
        {
            PlayKeyboardSound(cancelSound);
            GetHapticsService()?.TriggerCancel();
        }

        private void PlayNavigateFeedback()
        {
            PlayKeyboardSound(navigateSound != null ? navigateSound : keyPressSound);
            GetHapticsService()?.TriggerSelection();
        }

        /// <summary>
        /// Keyboard-owned SoundEventSO only. Combat / menu UI cues are never used as
        /// fallbacks so hardware typing cannot leak ButtonSwitch / Confirm / Cancel.
        /// </summary>
        private void PlayKeyboardSound(SoundEventSO soundEvent)
        {
            if (soundEvent == null)
                return;

            ICombatAudioBridge audioBridge = ResolveAudioBridge();
            audioBridge?.PlayOneShot(soundEvent);
        }
    }
}
