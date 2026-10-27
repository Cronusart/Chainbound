using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Chainbound.Combat
{
    /// <summary>
    /// Editor-authored UGUI shell for the shared in-game keyboard.
    /// Layout is authored in the GameServices prefab; this component only binds
    /// the visual state and forwards button presses to the service.
    /// </summary>
    public class OnScreenKeyboardUI : MonoBehaviour
    {
        private const int ExpectedKeyCount = 46;
        private const float SymbolKeyWidth = 150f;
        private const float KeyRowsHeightWithSymbols = 546f;
        private const float KeyboardShellHeightWithSymbols = 706f;
        private const string SymbolRowName = "RowSymbols";
        private const float FocusArrowYOffset = 10f;

        [Header("UI References")]
        [SerializeField] private GameObject keyboardRoot;
        [SerializeField] private GameObject headerRoot;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private GameObject displayStripRoot;
        [SerializeField] private TMP_Text targetDisplayText;
        [Tooltip("Simple downward arrow shown above the EventSystem-selected key (D-Pad / gamepad focus).")]
        [SerializeField] private RectTransform focusArrow;

        [Header("Authoring")]
        [SerializeField] private Sprite defaultKeySprite;
        [SerializeField] private Image.Type defaultKeyImageType = Image.Type.Sliced;
        [Tooltip(
            "GameObject name of the key whose Label (TMP) is the template for context-menu 'Apply Label Style To All Keys' " +
            "and for label-style preservation when you rebuild authored keys in the editor (default: Q).")]
        [SerializeField] private string labelStyleSourceKeyName = "Q";

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private readonly List<VirtualKeyButton> boundKeyButtons = new List<VirtualKeyButton>();
        private OnScreenKeyboardService service;
        private VirtualKeyButton confirmKey;
        private VirtualKeyButton focusedKey;
        private bool suppressNextNavigateSfx;
        private bool hasLoggedRootScaleCorrection;
        private static bool hasLoggedMissingService;
        private bool capturedPromptColor;
        private Color authoredPromptColor = Color.white;
        private Tween rejectionNudge;
        private RectTransform rejectionNudgeTarget;
        private Vector2 rejectionNudgeHome;

        private readonly struct RuntimeKeyDefinition
        {
            public RuntimeKeyDefinition(
                string name,
                VirtualKeyType keyType,
                string unshiftedValue,
                string shiftedValue,
                string unshiftedLabel,
                string shiftedLabel,
                float width,
                Color color)
            {
                Name = name;
                KeyType = keyType;
                UnshiftedValue = unshiftedValue;
                ShiftedValue = shiftedValue;
                UnshiftedLabel = unshiftedLabel;
                ShiftedLabel = shiftedLabel;
                Width = width;
                Color = color;
            }

            public string Name { get; }
            public VirtualKeyType KeyType { get; }
            public string UnshiftedValue { get; }
            public string ShiftedValue { get; }
            public string UnshiftedLabel { get; }
            public string ShiftedLabel { get; }
            public float Width { get; }
            public Color Color { get; }
        }

        public bool IsVisible => keyboardRoot != null && keyboardRoot.activeSelf;

        private void Awake()
        {
            EnsureUiReferences();
            EnsureKeyboardRootScale();

            if (Application.isPlaying)
            {
                BindKeyButtons();
                SetVisible(false);
            }
        }

        private void OnDestroy()
        {
            StopRejectionNudge();
            UnbindKeyButtons();
        }

        public void SetService(OnScreenKeyboardService owner)
        {
            service = owner;
            EnsureUiReferences();
            EnsureKeyboardRootScale();

            if (Application.isPlaying)
                BindKeyButtons();
        }

        public void Show(KeyboardSessionOptions options, bool shiftEnabled, string currentText, bool canSubmit)
        {
            EnsureUiReferences();
            EnsureKeyboardRootScale();
            EnsureFocusArrow();
            SetVisible(true);
            Refresh(options, shiftEnabled, currentText, canSubmit);
            SelectInitialKey();
        }

        public void Hide()
        {
            StopRejectionNudge();
            focusedKey = null;
            SetFocusArrowVisible(false);
            SetVisible(false);
        }

        /// <summary>
        /// Short horizontal nudge used when a submitted value is refused.
        /// </summary>
        public void PlayRejectionNudge()
        {
            RectTransform target = ResolveNudgeTarget();
            if (target == null)
                return;

            StopRejectionNudge();
            rejectionNudgeTarget = target;
            rejectionNudgeHome = target.anchoredPosition;
            rejectionNudge = target
                .DOShakeAnchorPos(0.32f, new Vector2(16f, 0f), 14, 90f, false, true)
                .SetUpdate(true)
                .OnComplete(RestoreRejectionNudge);
        }

        public bool TryFindCharacterKey(string value, out VirtualKeyButton keyButton)
        {
            keyButton = null;
            if (string.IsNullOrEmpty(value))
                return false;

            for (int i = 0; i < boundKeyButtons.Count; i++)
            {
                VirtualKeyButton candidate = boundKeyButtons[i];
                if (candidate == null || !candidate.MatchesCharacterOutput(value))
                    continue;

                keyButton = candidate;
                return true;
            }

            return false;
        }

        public bool TryFindKeyByType(VirtualKeyType keyType, out VirtualKeyButton keyButton)
        {
            keyButton = null;

            for (int i = 0; i < boundKeyButtons.Count; i++)
            {
                VirtualKeyButton candidate = boundKeyButtons[i];
                if (candidate == null || candidate.KeyType != keyType)
                    continue;

                keyButton = candidate;
                return true;
            }

            return false;
        }

        public void Refresh(KeyboardSessionOptions options, bool shiftEnabled, string currentText, bool canSubmit)
        {
            EnsureUiReferences();
            EnsureKeyboardRootScale();

            string title = options?.Title ?? "";
            string prompt = options?.Prompt ?? "";
            bool hasTitle = !string.IsNullOrWhiteSpace(title);
            bool hasPrompt = !string.IsNullOrWhiteSpace(prompt);

            if (titleText != null)
            {
                titleText.text = hasTitle ? title : "";
                titleText.gameObject.SetActive(hasTitle);
            }

            if (promptText != null)
            {
                CaptureAuthoredPromptColor();
                promptText.text = hasPrompt ? prompt : "";
                promptText.color = options != null && options.PromptColor.HasValue
                    ? options.PromptColor.Value
                    : authoredPromptColor;
                promptText.gameObject.SetActive(hasPrompt);
            }

            if (headerRoot != null)
                headerRoot.SetActive(hasTitle || hasPrompt);

            bool showLivePreviewStrip = options != null && options.ShowLivePreviewStrip;
            if (displayStripRoot != null)
                displayStripRoot.SetActive(showLivePreviewStrip);

            if (showLivePreviewStrip && targetDisplayText != null)
            {
                string emptyDisplay = options != null && !string.IsNullOrWhiteSpace(options.EmptyDisplayText)
                    ? options.EmptyDisplayText
                    : "...";
                targetDisplayText.text = string.IsNullOrWhiteSpace(currentText) ? emptyDisplay : currentText;
            }

            if (confirmKey != null)
            {
                string submitLabel = string.IsNullOrWhiteSpace(options?.SubmitLabel) ? "Confirm" : options.SubmitLabel;
                confirmKey.SetRuntimeLabels(submitLabel);
                confirmKey.SetInteractable(canSubmit);
            }

            for (int i = 0; i < boundKeyButtons.Count; i++)
                boundKeyButtons[i]?.RefreshVisual(shiftEnabled);

            if (enableDebugLogs)
                Debug.Log($"[OnScreenKeyboardUI] Refreshed keyboard. Visible={IsVisible}, CanSubmit={canSubmit}.");
        }

        private void EnsureUiReferences()
        {
            if (keyboardRoot == null)
                keyboardRoot = transform.Find("KeyboardCanvasRoot")?.gameObject;

            if (headerRoot == null)
                headerRoot = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/Header")?.gameObject;

            if (titleText == null)
                titleText = FindText("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/Header/TitleText");

            if (promptText == null)
                promptText = FindText("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/Header/PromptText");

            if (displayStripRoot == null)
                displayStripRoot = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/DisplayStrip")?.gameObject;

            if (targetDisplayText == null)
                targetDisplayText = FindText("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/DisplayStrip/DisplayText");

            if (focusArrow == null)
            {
                Transform arrowTransform = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/FocusArrow");
                if (arrowTransform != null)
                    focusArrow = arrowTransform as RectTransform;
            }
        }

        private void BindKeyButtons()
        {
            EnsureUiReferences();
            EnsureRuntimeKeyLayout();
            UnbindKeyButtons();

            if (keyboardRoot == null)
                return;

            VirtualKeyButton[] keyButtons = keyboardRoot.GetComponentsInChildren<VirtualKeyButton>(true);
            for (int i = 0; i < keyButtons.Length; i++)
            {
                VirtualKeyButton keyButton = keyButtons[i];
                if (keyButton == null)
                    continue;

                keyButton.Pressed += HandleKeyPressed;
                keyButton.Selected += HandleKeySelected;
                keyButton.Deselected += HandleKeyDeselected;
                keyButton.CancelRequested += HandleCancelRequested;
                boundKeyButtons.Add(keyButton);

                if (keyButton.KeyType == VirtualKeyType.Confirm)
                    confirmKey = keyButton;
            }
        }

        private void UnbindKeyButtons()
        {
            for (int i = 0; i < boundKeyButtons.Count; i++)
            {
                VirtualKeyButton keyButton = boundKeyButtons[i];
                if (keyButton == null)
                    continue;

                keyButton.Pressed -= HandleKeyPressed;
                keyButton.Selected -= HandleKeySelected;
                keyButton.Deselected -= HandleKeyDeselected;
                keyButton.CancelRequested -= HandleCancelRequested;
            }

            boundKeyButtons.Clear();
            confirmKey = null;
            focusedKey = null;
        }

        private void HandleKeyPressed(VirtualKeyButton keyButton)
        {
            if (service == null)
                service = OnScreenKeyboardService.Instance;

            if (service == null)
            {
                if (!hasLoggedMissingService)
                {
                    hasLoggedMissingService = true;
                    Debug.LogWarning(
                        "[OnScreenKeyboardUI] No OnScreenKeyboardService is bound; key presses are ignored. " +
                        "Ensure GameServices includes OnScreenKeyboardService and it runs Awake before the keyboard is used.",
                        this);
                }

                return;
            }

            service.HandleKeyPressed(keyButton);
        }

        private void HandleCancelRequested()
        {
            if (service == null)
                service = OnScreenKeyboardService.Instance;

            service?.RequestCancel();
        }

        private void HandleKeySelected(VirtualKeyButton keyButton)
        {
            bool playNavigateSfx = !suppressNextNavigateSfx && focusedKey != keyButton;

            focusedKey = keyButton;
            PositionFocusArrow(keyButton);
            SetFocusArrowVisible(true);

            if (suppressNextNavigateSfx)
                suppressNextNavigateSfx = false;

            if (!playNavigateSfx)
                return;

            if (service == null)
                service = OnScreenKeyboardService.Instance;

            service?.NotifyFocusMoved();
        }

        private void HandleKeyDeselected(VirtualKeyButton keyButton)
        {
            if (focusedKey != keyButton)
                return;

            focusedKey = null;
            SetFocusArrowVisible(false);
        }

        private void SelectInitialKey()
        {
            VirtualKeyButton startKey = null;
            if (!TryFindCharacterKey("q", out startKey) &&
                !TryFindCharacterKey("Q", out startKey))
            {
                for (int i = 0; i < boundKeyButtons.Count; i++)
                {
                    if (boundKeyButtons[i] != null)
                    {
                        startKey = boundKeyButtons[i];
                        break;
                    }
                }
            }

            if (startKey == null || EventSystem.current == null)
            {
                SetFocusArrowVisible(false);
                return;
            }

            suppressNextNavigateSfx = true;
            EventSystem.current.SetSelectedGameObject(startKey.gameObject);
            HandleKeySelected(startKey);
        }

        private void EnsureFocusArrow()
        {
            if (focusArrow == null)
            {
                Transform shell = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell");
                if (shell == null)
                    return;

                GameObject arrowObject = new GameObject("FocusArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                arrowObject.transform.SetParent(shell, false);
                focusArrow = arrowObject.GetComponent<RectTransform>();
                arrowObject.SetActive(false);
            }

            focusArrow.sizeDelta = new Vector2(36f, 28f);
            focusArrow.pivot = new Vector2(0.5f, 0f);
            focusArrow.localScale = Vector3.one;
            focusArrow.localEulerAngles = new Vector3(0f, 0f, 180f);

            Image image = focusArrow.GetComponent<Image>();
            if (image == null)
                image = focusArrow.gameObject.AddComponent<Image>();

            image.color = new Color(1f, 0.92f, 0.35f, 0.95f);
            image.raycastTarget = false;
            image.type = Image.Type.Simple;
            if (image.sprite == null)
            {
                image.sprite = defaultKeySprite != null
                    ? defaultKeySprite
                    : Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            }
        }

        private void PositionFocusArrow(VirtualKeyButton keyButton)
        {
            if (focusArrow == null || keyButton == null)
                return;

            RectTransform keyRect = keyButton.transform as RectTransform;
            if (keyRect == null)
                return;

            focusArrow.SetParent(keyRect, false);
            focusArrow.anchorMin = new Vector2(0.5f, 1f);
            focusArrow.anchorMax = new Vector2(0.5f, 1f);
            focusArrow.pivot = new Vector2(0.5f, 0f);
            focusArrow.anchoredPosition = new Vector2(0f, FocusArrowYOffset);
            focusArrow.localRotation = Quaternion.Euler(0f, 0f, 180f);
            focusArrow.localScale = Vector3.one;

            // Keep arrow as last sibling so it draws above the key art.
            focusArrow.SetAsLastSibling();
        }

        private void SetFocusArrowVisible(bool visible)
        {
            if (focusArrow == null)
                return;

            if (focusArrow.gameObject.activeSelf != visible)
                focusArrow.gameObject.SetActive(visible);
        }

        private void SetVisible(bool visible)
        {
            EnsureKeyboardRootScale();

            if (keyboardRoot != null)
                keyboardRoot.SetActive(visible);

            if (!visible)
                SetFocusArrowVisible(false);
        }

        private void EnsureKeyboardRootScale()
        {
            if (keyboardRoot == null)
                return;

            Transform rootTransform = keyboardRoot.transform;
            Vector3 localScale = rootTransform.localScale;
            if (!Mathf.Approximately(localScale.x, 0f) ||
                !Mathf.Approximately(localScale.y, 0f) ||
                !Mathf.Approximately(localScale.z, 0f))
            {
                return;
            }

            rootTransform.localScale = Vector3.one;

            if (enableDebugLogs && !hasLoggedRootScaleCorrection)
            {
                Debug.LogWarning("[OnScreenKeyboardUI] KeyboardCanvasRoot was authored with zero scale. Restoring it to (1,1,1) at runtime.", this);
                hasLoggedRootScaleCorrection = true;
            }
        }

        private void CaptureAuthoredPromptColor()
        {
            if (capturedPromptColor || promptText == null)
                return;

            authoredPromptColor = promptText.color;
            capturedPromptColor = true;
        }

        private RectTransform ResolveNudgeTarget()
        {
            Transform shell = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell");
            if (shell is RectTransform shellRect)
                return shellRect;

            return keyboardRoot != null ? keyboardRoot.transform as RectTransform : transform as RectTransform;
        }

        private void StopRejectionNudge()
        {
            if (rejectionNudge != null && rejectionNudge.IsActive())
                rejectionNudge.Kill(false);

            rejectionNudge = null;
            RestoreRejectionNudge();
        }

        private void RestoreRejectionNudge()
        {
            if (rejectionNudgeTarget == null)
                return;

            rejectionNudgeTarget.anchoredPosition = rejectionNudgeHome;
        }

        private TMP_Text FindText(string path)
        {
            Transform child = transform.Find(path);
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private void EnsureRuntimeKeyLayout()
        {
            if (keyboardRoot == null)
                return;

            VirtualKeyButton[] existingButtons = keyboardRoot.GetComponentsInChildren<VirtualKeyButton>(true);
            if (existingButtons.Length == ExpectedKeyCount)
                return;

            RebuildKeyLayout(false);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor / batch: (re)creates the symbol row and Row1–4 under the prefab so all 46
        /// <see cref="VirtualKeyButton"/> instances are serialized and editable (sprites,
        /// sprite-swap states, TMP). Invoked from the context menu or
        /// <c>Tools/Chainbound/UI/Repair On-Screen Keyboard In GameServices Prefab</c>.
        /// </summary>
        public void EditorRebuildAuthoredKeys()
        {
            EnsureUiReferences();
            RebuildKeyLayout(true);
        }

        [ContextMenu("Rebuild Authored Keys")]
        private void RebuildAuthoredKeys()
        {
            EditorRebuildAuthoredKeys();
        }

        [ContextMenu("Clear Authored Keys")]
        private void ClearAuthoredKeys()
        {
            EnsureUiReferences();
            ClearAllRowChildren(true);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        [ContextMenu("Apply Label Style To All Keys")]
        private void ApplyLabelStyleToAllKeys()
        {
            EnsureUiReferences();

            if (keyboardRoot == null)
            {
                Debug.LogWarning("[OnScreenKeyboardUI] Cannot apply label style because KeyboardRoot is missing.", this);
                return;
            }

            if (!TryFindLabelStyleSource(out TextMeshProUGUI sourceLabel))
            {
                Debug.LogWarning(
                    $"[OnScreenKeyboardUI] Could not find label style source key '{labelStyleSourceKeyName}'. " +
                    "Rebuild authored keys first or set Label Style Source Key Name to an existing key.",
                    this);
                return;
            }

            int appliedCount = ApplyLabelStyleFromSourceToAllTargets(sourceLabel);
            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);
            Debug.Log($"[OnScreenKeyboardUI] Applied label style from '{sourceLabel.gameObject.name}' to {appliedCount} keyboard labels.", this);
        }

        [ContextMenu("Apply Key Visual Style To All Keys")]
        private void ApplyKeyVisualStyleToAllKeys()
        {
            ApplyKeyVisualStyleFromSourceInternal(skipUtilityKeys: false);
        }

        /// <summary>
        /// Copies Image + Button (colors, sprite swap, transitions) from the style source key to other keys,
        /// skipping Shift, Backspace, Clear, Cancel, Confirm, and Space so those can keep distinct art.
        /// </summary>
        [ContextMenu("Apply Key Visual Style To Character Keys (excludes utilities)")]
        private void ApplyKeyVisualStyleToCharacterKeysExcludingUtilities()
        {
            ApplyKeyVisualStyleFromSourceInternal(skipUtilityKeys: true);
        }

        private void ApplyKeyVisualStyleFromSourceInternal(bool skipUtilityKeys)
        {
            EnsureUiReferences();

            if (keyboardRoot == null)
            {
                Debug.LogWarning("[OnScreenKeyboardUI] Cannot apply key visual style because KeyboardRoot is missing.", this);
                return;
            }

            if (!TryFindSourceKeyButton(out VirtualKeyButton sourceKey) ||
                !TryGetKeyVisualParts(sourceKey, out Button sourceButton, out Image sourceImage))
            {
                Debug.LogWarning(
                    $"[OnScreenKeyboardUI] Could not find key visual style source key '{labelStyleSourceKeyName}'. " +
                    "Rebuild authored keys first or set Label Style Source Key Name to an existing key.",
                    this);
                return;
            }

            VirtualKeyButton[] keys = keyboardRoot.GetComponentsInChildren<VirtualKeyButton>(true);
            int appliedCount = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                VirtualKeyButton targetKey = keys[i];
                if (targetKey == null || targetKey == sourceKey)
                    continue;

                if (skipUtilityKeys && ShouldExcludeUtilityKeyFromKeyVisualCopy(targetKey))
                    continue;

                if (!TryGetKeyVisualParts(targetKey, out Button targetButton, out Image targetImage))
                    continue;

                EditorUtility.CopySerialized(sourceImage, targetImage);
                CopyButtonVisualState(sourceButton, targetButton, targetImage);

                targetImage.SetAllDirty();
                EditorUtility.SetDirty(targetImage);
                EditorUtility.SetDirty(targetButton);
                appliedCount++;
            }

            EditorUtility.SetDirty(this);
            EditorUtility.SetDirty(gameObject);
            string scope = skipUtilityKeys ? "character keys (utilities skipped)" : "all keys";
            Debug.Log($"[OnScreenKeyboardUI] Applied key visual style from '{sourceKey.gameObject.name}' to {appliedCount} keyboard {scope}.", this);
        }

        /// <summary>
        /// Keys that should keep their own Image/Button when using
        /// <see cref="ApplyKeyVisualStyleToCharacterKeysExcludingUtilities"/>.
        /// </summary>
        private static bool ShouldExcludeUtilityKeyFromKeyVisualCopy(VirtualKeyButton key)
        {
            if (key == null)
                return true;

            switch (key.KeyType)
            {
                case VirtualKeyType.Shift:
                case VirtualKeyType.Backspace:
                case VirtualKeyType.Clear:
                case VirtualKeyType.Cancel:
                case VirtualKeyType.Confirm:
                    return true;
                case VirtualKeyType.Character:
                default:
                    break;
            }

            // Space is authored as a character key but uses wide layout / distinct art.
            string name = key.gameObject.name;
            if (string.Equals(name, "Space", System.StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }
#endif

        private bool TryFindLabelStyleSource(out TextMeshProUGUI sourceLabel)
        {
            sourceLabel = null;

            if (!TryFindSourceKeyButton(out VirtualKeyButton sourceKey))
                return false;

            sourceLabel = sourceKey.GetComponentInChildren<TextMeshProUGUI>(true);
            return sourceLabel != null;
        }

        private bool TryFindSourceKeyButton(out VirtualKeyButton sourceKey)
        {
            sourceKey = null;

            if (keyboardRoot == null)
                return false;

            VirtualKeyButton[] keys = keyboardRoot.GetComponentsInChildren<VirtualKeyButton>(true);
            for (int i = 0; i < keys.Length; i++)
            {
                VirtualKeyButton key = keys[i];
                if (key == null)
                    continue;

                string keyName = key.gameObject.name;
                if (!string.Equals(keyName, labelStyleSourceKeyName, System.StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(keyName, $"Key_{labelStyleSourceKeyName}", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sourceKey = key;
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Copies TMP + RectTransform serialized state from <paramref name="sourceLabel"/> to every other
        /// key label, preserving each key's current <c>text</c> (so key letters stay correct).
        /// </summary>
        private int ApplyLabelStyleFromSourceToAllTargets(TextMeshProUGUI sourceLabel)
        {
            if (sourceLabel == null || keyboardRoot == null)
                return 0;

            RectTransform sourceRect = sourceLabel.rectTransform;
            VirtualKeyButton[] keys = keyboardRoot.GetComponentsInChildren<VirtualKeyButton>(true);
            int appliedCount = 0;

            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == null)
                    continue;

                TextMeshProUGUI targetLabel = keys[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (targetLabel == null || targetLabel == sourceLabel)
                    continue;

                string preservedText = targetLabel.text;

                EditorUtility.CopySerialized(sourceLabel, targetLabel);
                EditorUtility.CopySerialized(sourceRect, targetLabel.rectTransform);

                targetLabel.text = preservedText;
                targetLabel.SetAllDirty();

                EditorUtility.SetDirty(targetLabel);
                EditorUtility.SetDirty(targetLabel.rectTransform);
                appliedCount++;
            }

            return appliedCount;
        }

        private static bool TryGetKeyVisualParts(VirtualKeyButton key, out Button button, out Image image)
        {
            button = key != null ? key.GetComponent<Button>() : null;
            image = key != null ? key.GetComponent<Image>() : null;
            return button != null && image != null;
        }

        private static void CopyButtonVisualState(Button sourceButton, Button targetButton, Image targetImage)
        {
            targetButton.transition = sourceButton.transition;
            targetButton.colors = sourceButton.colors;
            targetButton.spriteState = sourceButton.spriteState;
            targetButton.animationTriggers = CopyAnimationTriggers(sourceButton.animationTriggers);
            targetButton.interactable = sourceButton.interactable;
            targetButton.targetGraphic = targetImage;
        }

        private static AnimationTriggers CopyAnimationTriggers(AnimationTriggers source)
        {
            AnimationTriggers copy = new AnimationTriggers();
            if (source == null)
                return copy;

            copy.normalTrigger = source.normalTrigger;
            copy.highlightedTrigger = source.highlightedTrigger;
            copy.pressedTrigger = source.pressedTrigger;
            copy.selectedTrigger = source.selectedTrigger;
            copy.disabledTrigger = source.disabledTrigger;
            return copy;
        }
#endif

        private void RebuildKeyLayout(bool immediate)
        {
            if (keyboardRoot == null)
                return;

            if (!TryGetKeyRows(out Transform row1, out Transform row2, out Transform row3, out Transform row4))
                return;

            Transform symbolRow = EnsureSymbolRow(row1.parent);

#if UNITY_EDITOR
            // Rebuild destroys every key GameObject — without this, any custom TMP on the style source key is lost.
            TextMeshProUGUI cachedStyleLabel = null;
            RectTransform cachedStyleRect = null;
            GameObject styleCacheRoot = null;
            if (immediate && TryFindLabelStyleSource(out TextMeshProUGUI styleSourceLabel))
            {
                styleCacheRoot = new GameObject("OnScreenKeyboard_LabelStyleCache");
                styleCacheRoot.hideFlags = HideFlags.HideAndDontSave;
                GameObject labelClone = Object.Instantiate(styleSourceLabel.gameObject, styleCacheRoot.transform, false);
                cachedStyleLabel = labelClone.GetComponent<TextMeshProUGUI>();
                cachedStyleRect = labelClone.GetComponent<RectTransform>();
            }
#endif

            ClearAllRowChildren(immediate);

            Color characterColor = new Color(0.14f, 0.18f, 0.22f, 1f);
            Color utilityColor = new Color(0.24f, 0.28f, 0.33f, 1f);
            Color neutralColor = new Color(0.28f, 0.31f, 0.38f, 1f);
            Color confirmColor = new Color(0.12f, 0.55f, 0.37f, 1f);
            Color spaceColor = new Color(0.1f, 0.13f, 0.17f, 1f);

            BuildRow(symbolRow, new[]
            {
                Symbol("1"), Symbol("2"), Symbol("3"), Symbol("4"), Symbol("5"),
                Symbol("6"), Symbol("7"), Symbol("8"), Symbol("9"), Symbol("0"),
                Symbol("_"), Symbol("."),
            });

            BuildRow(row1, new[]
            {
                Character("Q"), Character("W"), Character("E"), Character("R"), Character("T"),
                Character("Y"), Character("U"), Character("I"), Character("O"), Character("P"),
            });

            BuildRow(row2, new[]
            {
                Character("A"), Character("S"), Character("D"), Character("F"), Character("G"),
                Character("H"), Character("J"), Character("K"), Character("L"),
                new RuntimeKeyDefinition("Apostrophe", VirtualKeyType.Character, "'", "'", "'", "'", 180f, characterColor),
            });

            BuildRow(row3, new[]
            {
                new RuntimeKeyDefinition("Shift", VirtualKeyType.Shift, "", "", "Shift", "SHIFT", 220f, utilityColor),
                Character("Z"), Character("X"), Character("C"), Character("V"),
                Character("B"), Character("N"), Character("M"),
                new RuntimeKeyDefinition("Hyphen", VirtualKeyType.Character, "-", "-", "-", "-", 180f, characterColor),
                new RuntimeKeyDefinition("Backspace", VirtualKeyType.Backspace, "", "", "Back", "Back", 220f, utilityColor),
            });

            BuildRow(row4, new[]
            {
                new RuntimeKeyDefinition("Clear", VirtualKeyType.Clear, "", "", "Clear", "Clear", 220f, neutralColor),
                new RuntimeKeyDefinition("Cancel", VirtualKeyType.Cancel, "", "", "Cancel", "Cancel", 220f, neutralColor),
                new RuntimeKeyDefinition("Space", VirtualKeyType.Character, " ", " ", "Space", "Space", 760f, spaceColor),
                new RuntimeKeyDefinition("Confirm", VirtualKeyType.Confirm, "", "", "Confirm", "Confirm", 260f, confirmColor),
            });

            if (enableDebugLogs)
                Debug.Log($"[OnScreenKeyboardUI] Rebuilt keyboard keys. Immediate={immediate}.");

#if UNITY_EDITOR
            if (immediate && styleCacheRoot != null)
            {
                if (cachedStyleLabel != null &&
                    cachedStyleRect != null &&
                    TryFindLabelStyleSource(out TextMeshProUGUI newSourceLabel))
                {
                    string preservedSourceText = newSourceLabel.text;
                    EditorUtility.CopySerialized(cachedStyleLabel, newSourceLabel);
                    EditorUtility.CopySerialized(cachedStyleRect, newSourceLabel.rectTransform);
                    newSourceLabel.text = preservedSourceText;
                    newSourceLabel.SetAllDirty();
                    EditorUtility.SetDirty(newSourceLabel);
                    EditorUtility.SetDirty(newSourceLabel.rectTransform);

                    int propagated = ApplyLabelStyleFromSourceToAllTargets(newSourceLabel);
                    if (enableDebugLogs)
                        Debug.Log($"[OnScreenKeyboardUI] Restored label style from cache and propagated to {propagated} other key label(s).", this);
                }

                Object.DestroyImmediate(styleCacheRoot);
            }

            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(gameObject);
            }
#endif

            RuntimeKeyDefinition Character(string upper)
            {
                string lower = upper.ToLowerInvariant();
                return new RuntimeKeyDefinition(
                    upper,
                    VirtualKeyType.Character,
                    lower,
                    upper,
                    lower,
                    upper,
                    180f,
                    characterColor);
            }

            RuntimeKeyDefinition Symbol(string glyph)
            {
                string name = glyph == "_" ? "Underscore" : glyph == "." ? "Period" : "Digit" + glyph;
                return new RuntimeKeyDefinition(
                    name,
                    VirtualKeyType.Character,
                    glyph,
                    glyph,
                    glyph,
                    glyph,
                    SymbolKeyWidth,
                    characterColor);
            }
        }

        private Transform EnsureSymbolRow(Transform keyRows)
        {
            if (keyRows == null)
                return null;

            RectTransform keyRowsRect = keyRows as RectTransform;
            if (keyRowsRect != null)
                keyRowsRect.sizeDelta = new Vector2(keyRowsRect.sizeDelta.x, KeyRowsHeightWithSymbols);

            RectTransform shellRect = keyRows.parent as RectTransform;
            if (shellRect != null)
                shellRect.sizeDelta = new Vector2(shellRect.sizeDelta.x, KeyboardShellHeightWithSymbols);

            Transform existing = keyRows.Find(SymbolRowName);
            if (existing != null)
            {
                existing.SetAsFirstSibling();
                return existing;
            }

            GameObject rowObject = new GameObject(SymbolRowName, typeof(RectTransform));
            rowObject.transform.SetParent(keyRows, false);
            rowObject.transform.SetAsFirstSibling();

            RectTransform rect = rowObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 82f);

            HorizontalLayoutGroup layout = rowObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 34f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return rowObject.transform;
        }

        private bool TryGetKeyRows(out Transform row1, out Transform row2, out Transform row3, out Transform row4)
        {
            row1 = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/KeyRows/Row1");
            row2 = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/KeyRows/Row2");
            row3 = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/KeyRows/Row3");
            row4 = transform.Find("KeyboardCanvasRoot/SafeAreaRoot/KeyboardShell/KeyRows/Row4");
            return row1 != null && row2 != null && row3 != null && row4 != null;
        }

        private void ClearAllRowChildren(bool immediate)
        {
            if (!TryGetKeyRows(out Transform row1, out Transform row2, out Transform row3, out Transform row4))
                return;

            ClearRow(row1.parent != null ? row1.parent.Find(SymbolRowName) : null, immediate);
            ClearRow(row1, immediate);
            ClearRow(row2, immediate);
            ClearRow(row3, immediate);
            ClearRow(row4, immediate);
        }

        private void ClearRow(Transform rowTransform, bool immediate)
        {
            if (rowTransform == null)
                return;

            for (int i = rowTransform.childCount - 1; i >= 0; i--)
            {
                GameObject child = rowTransform.GetChild(i).gameObject;
                if (immediate)
                {
#if UNITY_EDITOR
                    DestroyImmediate(child);
#endif
                }
                else
                {
                    Destroy(child);
                }
            }
        }

        private void BuildRow(Transform rowTransform, RuntimeKeyDefinition[] definitions)
        {
            if (rowTransform == null || definitions == null)
                return;

            for (int i = 0; i < definitions.Length; i++)
                CreateRuntimeKeyButton(rowTransform, definitions[i]);
        }

        private void CreateRuntimeKeyButton(Transform parent, RuntimeKeyDefinition definition)
        {
            GameObject buttonObject = new GameObject(definition.Name, typeof(RectTransform));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(definition.Width, 82f);

            buttonObject.AddComponent<CanvasRenderer>();

            Image image = buttonObject.AddComponent<Image>();
            image.color = definition.Color;
            image.sprite = defaultKeySprite != null
                ? defaultKeySprite
                : Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = defaultKeyImageType;

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelObject.transform.SetParent(buttonObject.transform, false);

            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);

            TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = definition.UnshiftedLabel;
            label.fontSize = 28f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;

            VirtualKeyButton keyButton = buttonObject.AddComponent<VirtualKeyButton>();
            keyButton.Configure(
                definition.KeyType,
                definition.UnshiftedValue,
                definition.ShiftedValue,
                definition.UnshiftedLabel,
                definition.ShiftedLabel);
        }
    }
}
