using System;
using Chainbound.Combat;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mobile-friendly onboarding name input overlay backed by the shared
/// on-screen keyboard and an editor-authored UGUI panel.
/// </summary>
[DisallowMultipleComponent]
public class NameInputUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Root panel that becomes visible while name entry is active.")]
    [SerializeField] private GameObject inputPanel;

    [Tooltip("Hidden backing TMP input field used by the shared keyboard service.")]
    [SerializeField] private TMP_InputField nameInputField;

    [Tooltip("Reusable display panel that mirrors the current entered name.")]
    [SerializeField] private NameTagDisplayPanel tagPanel;

    [Tooltip("Collar / nametag chrome hidden while entering the dog's name over the cinematic.")]
    [SerializeField] private GameObject collarContainer;

    [Tooltip("Shared keyboard service. Auto-found when empty.")]
    [SerializeField] private OnScreenKeyboardService onScreenKeyboardService;

    [Header("Settings")]
    [Tooltip("Minimum name length (trimmed).")]
    [SerializeField] private int minNameLength = 2;

    [Tooltip("Maximum name length.")]
    [SerializeField] private int maxNameLength = 12;

    [Tooltip("Placeholder text used by the hidden TMP input field.")]
    [SerializeField] private string placeholderText = "Dog's name";

    [Tooltip("Show the in-game keyboard even in editor/desktop builds for layout QA.")]
    [SerializeField] private bool forceSharedKeyboardOnDesktop = true;

    [Tooltip("Shared keyboard header shown while naming the playable dog.")]
    [SerializeField] private string keyboardTitle = "Dog's Name";

    [Tooltip("Shared keyboard prompt shown while naming the playable dog.")]
    [SerializeField] private string keyboardPrompt = "What is your dog's name?";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public event Action<string> OnNameConfirmed;
    public event Action OnNameCancelled;

    private bool isShowing;
    private bool uiEventsBound;
    private Button panelActivationButton;
    private bool hasLoggedPanelScaleCorrection;
    private bool collarWasActive;
    private bool collarSuppressed;
    private MobileHudToken mobileHudSuppressionToken;
    private bool hasMobileHudSuppressionToken;

    private void Awake()
    {
        EnsureUi();
        BindUiEvents();
        SetPanelVisible(false);
        RefreshNamePreview();
    }

    private void OnEnable()
    {
        isShowing = false;
        SetPanelVisible(false);
        RefreshNamePreview();
    }

    private void OnDisable()
    {
        isShowing = false;
        SetPanelVisible(false);
        SetCollarVisible(true);
        onScreenKeyboardService?.EndSession();
        ReleaseMobileHudSuppression();

        if (nameInputField != null)
            nameInputField.DeactivateInputField();
    }

    private void OnDestroy()
    {
        UnbindUiEvents();
    }

    /// <summary>
    /// Replaces the keyboard title and prompt for the next Show call.
    /// Used when a committed name is already taken.
    /// </summary>
    public void SetEntryCopy(string title, string prompt)
    {
        if (!string.IsNullOrWhiteSpace(title))
            keyboardTitle = title;
        if (prompt != null)
            keyboardPrompt = prompt;
    }

    public bool Show()
    {
        if (!EnsureUi())
        {
            Debug.LogError("[NameInputUI] Missing authored references. Cannot show name input overlay.", this);
            return false;
        }

        BindUiEvents();
        // Suppress the gameplay HUD before activating the name-entry panel.
        // The keyboard service acquires its own token moments later, but this
        // closes the one-frame gap in which the mobile HUD could flash over
        // the native/on-screen keyboard during the intro.
        AcquireMobileHudSuppression();
        isShowing = true;
        SetPanelVisible(true);
        SetCollarVisible(false);
        ResetInputField();
        RefreshNamePreview();

        if (ShouldUseSharedKeyboard())
        {
            if (!OpenKeyboardSession())
            {
                Debug.LogError("[NameInputUI] Failed to open the shared keyboard session. Check GameServices keyboard wiring.", this);
                isShowing = false;
                SetPanelVisible(false);
                SetCollarVisible(true);
                ReleaseMobileHudSuppression();
                return false;
            }
        }
        else
        {
            nameInputField.ActivateInputField();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(nameInputField.gameObject);
        }

        if (enableDebugLogs)
            Debug.Log("[NameInputUI] Showing onboarding name input.", this);

        return true;
    }

    public void Hide()
    {
        isShowing = false;
        SetPanelVisible(false);
        SetCollarVisible(true);
        onScreenKeyboardService?.EndSession();
        ReleaseMobileHudSuppression();

        if (nameInputField != null)
        {
            nameInputField.text = string.Empty;
            nameInputField.DeactivateInputField();
        }

        tagPanel?.Clear();

        if (enableDebugLogs)
            Debug.Log("[NameInputUI] Hiding onboarding name input.", this);
    }

    public void CancelEntry()
    {
        OnNameCancelled?.Invoke();
        Hide();
    }

    private void BindUiEvents()
    {
        UnbindUiEvents();

        if (nameInputField != null)
        {
            ConfigureInputField();
            nameInputField.onValueChanged.AddListener(OnInputValueChanged);

            if (nameInputField.placeholder is TMP_Text placeholder)
                placeholder.text = placeholderText;
        }

        panelActivationButton = inputPanel != null ? inputPanel.GetComponent<Button>() : null;
        if (panelActivationButton != null)
            panelActivationButton.onClick.AddListener(HandleKeyboardActivationRequested);

        uiEventsBound = true;
    }

    private void UnbindUiEvents()
    {
        if (!uiEventsBound)
            return;

        if (nameInputField != null)
            nameInputField.onValueChanged.RemoveListener(OnInputValueChanged);

        if (panelActivationButton != null)
            panelActivationButton.onClick.RemoveListener(HandleKeyboardActivationRequested);

        panelActivationButton = null;
        uiEventsBound = false;
    }

    private void OnInputValueChanged(string value)
    {
        string sanitized = SanitizeNameInput(value);
        if (nameInputField != null && sanitized != value)
        {
            int caret = Mathf.Min(nameInputField.caretPosition, sanitized.Length);
            nameInputField.SetTextWithoutNotify(sanitized);
            nameInputField.caretPosition = caret;
            nameInputField.selectionAnchorPosition = caret;
            nameInputField.selectionFocusPosition = caret;
        }

        RefreshNamePreview();
        SpriteCinematicPlayer.Instance?.SetCoinNameMirrorText(sanitized);
    }

    private void ConfirmName()
    {
        string confirmedName = GetTrimmedName();
        if (!ValidateName(confirmedName))
            return;

        OnNameConfirmed?.Invoke(confirmedName);
        Hide();

        if (enableDebugLogs)
            Debug.Log($"[NameInputUI] Confirmed player name '{confirmedName}'.", this);
    }

    private string GetTrimmedName()
    {
        return nameInputField != null && nameInputField.text != null
            ? nameInputField.text.Trim()
            : string.Empty;
    }

    private bool ValidateName(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        string trimmed = candidate.Trim();
        if (trimmed.Length < minNameLength || trimmed.Length > maxNameLength)
            return false;

        for (int i = 0; i < trimmed.Length; i++)
        {
            if (!IsAllowedNameCharacter(trimmed[i]))
                return false;
        }

        // No leading/trailing spaces (already trimmed) and no consecutive spaces.
        if (trimmed.Contains("  "))
            return false;

        return true;
    }

    private static bool IsAllowedNameCharacter(char character)
    {
        return char.IsLetter(character) || character == ' ' || character == '\'' || character == '-';
    }

    private static string SanitizeNameInput(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new System.Text.StringBuilder(value.Length);
        char previous = '\0';
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!IsAllowedNameCharacter(c))
                continue;

            if (c == ' ' && (builder.Length == 0 || previous == ' '))
                continue;

            builder.Append(c);
            previous = c;
        }

        return builder.ToString();
    }

    private void RefreshNamePreview()
    {
        if (tagPanel == null)
            return;

        tagPanel.SetDisplayName(nameInputField != null ? nameInputField.text : string.Empty);
    }

    private void SetPanelVisible(bool visible)
    {
        EnsureInputPanelScale();

        if (inputPanel != null)
            inputPanel.SetActive(visible);
    }

    private bool EnsureUi()
    {
        AutoBindAuthoredUi();

        if (nameInputField == null)
        {
            RectTransform inputFieldShell = FindChildByName<RectTransform>("NameInputField");
            if (inputFieldShell != null)
                nameInputField = EnsureInputFieldOnShell(inputFieldShell);
        }

        if (onScreenKeyboardService == null)
            onScreenKeyboardService = OnScreenKeyboardService.Instance;

        EnsureInputPanelScale();

        bool requiresSharedKeyboard = ShouldUseSharedKeyboard();
        return inputPanel != null &&
               nameInputField != null &&
               tagPanel != null &&
               (!requiresSharedKeyboard || onScreenKeyboardService != null);
    }

    private void AutoBindAuthoredUi()
    {
        if (inputPanel == null)
        {
            RectTransform inputPanelTransform = FindChildByName<RectTransform>("NameInputCanvasRoot");
            inputPanel = inputPanelTransform != null ? inputPanelTransform.gameObject : null;
        }

        if (nameInputField == null)
        {
            RectTransform inputFieldTransform = FindChildByName<RectTransform>("NameInputField");
            if (inputFieldTransform != null)
            {
                nameInputField = inputFieldTransform.GetComponent<TMP_InputField>();
                if (nameInputField == null)
                    nameInputField = EnsureInputFieldOnShell(inputFieldTransform);
            }
        }

        if (tagPanel == null)
            tagPanel = FindChildComponent<NameTagDisplayPanel>();

        if (collarContainer == null)
        {
            Transform collar = FindChildByName<Transform>("CollarContainer");
            collarContainer = collar != null ? collar.gameObject : null;
        }

        if (onScreenKeyboardService == null)
        {
            onScreenKeyboardService = OnScreenKeyboardService.Instance;
            if (onScreenKeyboardService == null)
                onScreenKeyboardService = FindFirstObjectByType<OnScreenKeyboardService>(FindObjectsInactive.Include);
        }
    }

    private void SetCollarVisible(bool visible)
    {
        if (collarContainer == null)
            return;

        if (!visible)
        {
            if (!collarSuppressed)
            {
                collarWasActive = collarContainer.activeSelf;
                collarSuppressed = true;
            }

            collarContainer.SetActive(false);
            return;
        }

        if (!collarSuppressed)
            return;

        collarContainer.SetActive(collarWasActive);
        collarSuppressed = false;
    }

    private void ConfigureInputField()
    {
        if (nameInputField == null)
            return;

        bool useSharedKeyboard = ShouldUseSharedKeyboard();
        nameInputField.characterLimit = maxNameLength;
        nameInputField.contentType = TMP_InputField.ContentType.Name;
        nameInputField.lineType = TMP_InputField.LineType.SingleLine;
        nameInputField.readOnly = useSharedKeyboard;
        nameInputField.shouldActivateOnSelect = !useSharedKeyboard;
        nameInputField.shouldHideSoftKeyboard = useSharedKeyboard;
        nameInputField.shouldHideMobileInput = useSharedKeyboard;
    }

    private void ResetInputField()
    {
        if (nameInputField == null)
            return;

        nameInputField.text = string.Empty;
        nameInputField.caretPosition = 0;
        nameInputField.selectionAnchorPosition = 0;
        nameInputField.selectionFocusPosition = 0;
        ConfigureInputField();
    }

    private bool OpenKeyboardSession()
    {
        if (!isShowing || !ShouldUseSharedKeyboard() || nameInputField == null)
            return false;

        if (onScreenKeyboardService == null)
            onScreenKeyboardService = OnScreenKeyboardService.Instance;

        if (onScreenKeyboardService == null)
            return false;

        return onScreenKeyboardService.BeginSession(
            nameInputField,
            new KeyboardSessionOptions
            {
                Title = string.IsNullOrWhiteSpace(keyboardTitle) ? "Dog's Name" : keyboardTitle,
                Prompt = string.IsNullOrWhiteSpace(keyboardPrompt) ? "What is your dog's name?" : keyboardPrompt,
                SubmitLabel = "Confirm",
                EmptyDisplayText = "...",
                ShowLivePreviewStrip = false,
                MaxLength = maxNameLength,
                StartShifted = true,
                CharacterPolicy = KeyboardCharacterPolicy.NameEntry,
                SubmitValidator = ValidateName,
                OnSubmitted = _ => ConfirmName(),
                OnCancelled = HandleKeyboardSessionCancelled,
            });
    }

    private void HandleKeyboardSessionCancelled()
    {
        if (enableDebugLogs)
            Debug.Log("[NameInputUI] Shared keyboard session closed without submission.", this);
    }

    private void HandleKeyboardActivationRequested()
    {
        if (!isShowing)
            return;

        if (ShouldUseSharedKeyboard())
        {
            OpenKeyboardSession();
            return;
        }

        if (nameInputField == null)
            return;

        nameInputField.ActivateInputField();

        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(nameInputField.gameObject);
    }

    private void AcquireMobileHudSuppression()
    {
        if (hasMobileHudSuppressionToken)
            return;

        MobileHudService hudService = MobileHudService.Instance;
        if (hudService == null)
            return;

        mobileHudSuppressionToken = hudService.AcquireContext(
            MobileHudContext.Hidden,
            "NameInputUI");
        hasMobileHudSuppressionToken = mobileHudSuppressionToken.IsValid;
    }

    private void ReleaseMobileHudSuppression()
    {
        if (!hasMobileHudSuppressionToken)
            return;

        mobileHudSuppressionToken.Release();
        mobileHudSuppressionToken = default;
        hasMobileHudSuppressionToken = false;
    }

    private bool ShouldUseSharedKeyboard()
    {
        return Application.isMobilePlatform || forceSharedKeyboardOnDesktop;
    }

    private void EnsureInputPanelScale()
    {
        if (inputPanel == null)
            return;

        Transform panelTransform = inputPanel.transform;
        Vector3 localScale = panelTransform.localScale;
        if (!Mathf.Approximately(localScale.x, 0f) ||
            !Mathf.Approximately(localScale.y, 0f) ||
            !Mathf.Approximately(localScale.z, 0f))
        {
            return;
        }

        panelTransform.localScale = Vector3.one;

        if (enableDebugLogs && !hasLoggedPanelScaleCorrection)
        {
            Debug.LogWarning("[NameInputUI] NameInputCanvasRoot was authored with zero scale. Restoring it to (1,1,1) at runtime.", this);
            hasLoggedPanelScaleCorrection = true;
        }
    }

    private T FindChildComponent<T>() where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].gameObject != gameObject)
                return components[i];
        }

        return null;
    }

    private T FindChildByName<T>(string childName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null &&
                component.gameObject != gameObject &&
                string.Equals(component.gameObject.name, childName, StringComparison.Ordinal))
            {
                return component;
            }
        }

        return null;
    }

    private TMP_InputField EnsureInputFieldOnShell(RectTransform inputFieldShell)
    {
        if (inputFieldShell == null)
            return null;

        if (inputFieldShell.GetComponent<CanvasRenderer>() == null)
            inputFieldShell.gameObject.AddComponent<CanvasRenderer>();

        Image inputBackground = inputFieldShell.GetComponent<Image>();
        if (inputBackground == null)
            inputBackground = inputFieldShell.gameObject.AddComponent<Image>();

        inputBackground.color = new Color(0f, 0f, 0f, 0f);
        inputBackground.raycastTarget = false;

        TMP_InputField inputField = inputFieldShell.GetComponent<TMP_InputField>();
        if (inputField == null)
            inputField = inputFieldShell.gameObject.AddComponent<TMP_InputField>();

        inputField.targetGraphic = inputBackground;

        TextMeshProUGUI inputText = EnsureHiddenText(inputFieldShell, "Text", string.Empty, FontStyles.Normal);
        TextMeshProUGUI placeholder = EnsureHiddenText(inputFieldShell, "Placeholder", placeholderText, FontStyles.Italic);

        inputField.textViewport = inputFieldShell;
        inputField.textComponent = inputText;
        inputField.placeholder = placeholder;

        return inputField;
    }

    private static TextMeshProUGUI EnsureHiddenText(
        RectTransform parent,
        string objectName,
        string text,
        FontStyles fontStyle)
    {
        Transform existingChild = parent.Find(objectName);
        TextMeshProUGUI label = existingChild != null
            ? existingChild.GetComponent<TextMeshProUGUI>()
            : null;

        if (label == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(parent, false);
            label = textObject.AddComponent<TextMeshProUGUI>();
        }

        RectTransform rectTransform = label.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;

        label.text = text;
        label.fontSize = 18f;
        label.fontStyle = fontStyle;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = new Color(1f, 1f, 1f, 0f);
        label.raycastTarget = false;
        return label;
    }
}
