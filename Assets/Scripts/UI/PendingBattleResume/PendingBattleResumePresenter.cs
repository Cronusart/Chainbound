using System.Threading.Tasks;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Passive safe-area modal. It deliberately has no cancel/back route: button B
/// never means forfeit, and the player must explicitly choose the destructive action.
/// Keyboard, gamepad, and the handheld A button confirm the focused choice here,
/// because Play pauses the menu submit owner before this prompt opens.
/// </summary>
public sealed class PendingBattleResumePresenter : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private RectTransform safeAreaRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button primaryButton;
    [SerializeField] private Button secondaryButton;
    [SerializeField] private CanvasGroup canvasGroup;

    private TaskCompletionSource<PendingBattleResumeChoice> completion;
    private Rect lastSafeArea;
    private InputAction submitAction;
    private InputAction navigateAction;
    private bool submitEnabledByPrompt;
    private bool navigateEnabledByPrompt;
    private Vector2 lastNavigate;
    private MobileInputManager mobileInput;

    public bool IsOpen => canvasGroup != null
        ? canvasGroup.alpha > 0.01f && canvasGroup.blocksRaycasts
        : root != null && root.activeInHierarchy;

    private void Awake()
    {
        if (root == null)
            root = gameObject;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        Hide();
    }

    private void OnDestroy()
    {
        DisarmConfirmInput();
        completion?.TrySetResult(PendingBattleResumeChoice.Unavailable);
    }

    private void Update()
    {
        ApplySafeArea();
        PollConfirmInput();
    }

    public Task<PendingBattleResumeChoice> ShowOpenAsync(string status = "")
    {
        return Show(
            "A battle is still waiting.",
            "Rejoin to continue where you left off. Forfeit knocks out your party, revives them at the nearest sanctuary, and applies the battle's cost.",
            "Rejoin",
            PendingBattleResumeChoice.Rejoin,
            "Forfeit",
            PendingBattleResumeChoice.Forfeit,
            status);
    }

    public Task<PendingBattleResumeChoice> ShowExpiredAsync(string status = "")
    {
        return Show(
            "Battle timed out",
            "That battle ran out of time. Your party was knocked out and revived at the nearest sanctuary.",
            "Continue",
            PendingBattleResumeChoice.Continue,
            string.Empty,
            PendingBattleResumeChoice.Continue,
            status);
    }

    private Task<PendingBattleResumeChoice> Show(
        string title,
        string body,
        string primaryLabel,
        PendingBattleResumeChoice primaryChoice,
        string secondaryLabel,
        PendingBattleResumeChoice secondaryChoice,
        string status)
    {
        if (root == null || titleText == null || bodyText == null || statusText == null ||
            primaryButton == null || secondaryButton == null)
        {
            return Task.FromResult(PendingBattleResumeChoice.Unavailable);
        }

        completion?.TrySetResult(PendingBattleResumeChoice.Unavailable);
        completion = new TaskCompletionSource<PendingBattleResumeChoice>();
        titleText.text = title;
        bodyText.text = body;
        statusText.text = status ?? string.Empty;
        ConfigureButton(primaryButton, primaryLabel, primaryChoice, true);
        ConfigureButton(secondaryButton, secondaryLabel, secondaryChoice, !string.IsNullOrWhiteSpace(secondaryLabel));

        Navigation primaryNavigation = new Navigation { mode = Navigation.Mode.Explicit };
        primaryNavigation.selectOnDown = secondaryButton.gameObject.activeSelf ? secondaryButton : primaryButton;
        primaryNavigation.selectOnUp = secondaryButton.gameObject.activeSelf ? secondaryButton : primaryButton;
        primaryButton.navigation = primaryNavigation;
        if (secondaryButton.gameObject.activeSelf)
        {
            Navigation secondaryNavigation = new Navigation { mode = Navigation.Mode.Explicit };
            secondaryNavigation.selectOnDown = primaryButton;
            secondaryNavigation.selectOnUp = primaryButton;
            secondaryButton.navigation = secondaryNavigation;
        }

        SetVisible(true);
        ApplySafeArea(force: true);
        ArmConfirmInput();
        EventSystem.current?.SetSelectedGameObject(primaryButton.gameObject);
        return completion.Task;
    }

    /// <summary>
    /// Submit confirms the focused prompt button. A cleared or foreign selection
    /// confirms the primary choice, which is Continue on a timed-out battle.
    /// </summary>
    public static Button ResolveConfirmTarget(Button primary, Button secondary, GameObject selected)
    {
        if (IsPromptButton(secondary, selected))
            return secondary;
        if (IsPromptButton(primary, selected))
            return primary;
        return IsUsable(primary) ? primary : null;
    }

    private void ConfigureButton(Button button, string label, PendingBattleResumeChoice choice, bool visible)
    {
        button.gameObject.SetActive(visible);
        button.interactable = visible;
        TextMeshProUGUI labelText = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (labelText != null)
            labelText.text = label ?? string.Empty;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => Complete(choice));
    }

    private void Complete(PendingBattleResumeChoice choice)
    {
        if (completion == null || completion.Task.IsCompleted)
            return;
        TaskCompletionSource<PendingBattleResumeChoice> pending = completion;
        completion = null;
        Hide();
        pending.TrySetResult(choice);
    }

    private void Hide()
    {
        DisarmConfirmInput();
        SetVisible(false);
    }

    private void ArmConfirmInput()
    {
        DisarmConfirmInput();
        submitAction = FindUiAction("Submit");
        navigateAction = FindUiAction("Navigate");
        submitEnabledByPrompt = EnableIfNeeded(submitAction);
        navigateEnabledByPrompt = EnableIfNeeded(navigateAction);
        lastNavigate = navigateAction != null && navigateAction.enabled
            ? navigateAction.ReadValue<Vector2>()
            : Vector2.zero;

        mobileInput = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);
        if (mobileInput != null)
            mobileInput.OnAPressed += ConfirmFocusedButton;
    }

    private void DisarmConfirmInput()
    {
        if (mobileInput != null)
        {
            mobileInput.OnAPressed -= ConfirmFocusedButton;
            mobileInput = null;
        }

        if (submitAction != null && submitEnabledByPrompt)
            submitAction.Disable();
        if (navigateAction != null && navigateEnabledByPrompt)
            navigateAction.Disable();

        submitAction = null;
        navigateAction = null;
        submitEnabledByPrompt = false;
        navigateEnabledByPrompt = false;
        lastNavigate = Vector2.zero;
    }

    private void PollConfirmInput()
    {
        if (completion == null || completion.Task.IsCompleted)
            return;

        if (submitAction != null && submitAction.WasPressedThisFrame())
            ConfirmFocusedButton();

        PollPromptNavigation();
    }

    private void PollPromptNavigation()
    {
        if (navigateAction == null || !navigateAction.enabled)
            return;
        if (secondaryButton == null || !secondaryButton.gameObject.activeSelf)
            return;
        if (UiModuleOwnsMove())
            return;

        Vector2 value = navigateAction.ReadValue<Vector2>();
        bool verticalEdge = Mathf.Abs(value.y) >= 0.5f && Mathf.Abs(lastNavigate.y) < 0.5f;
        lastNavigate = value;
        if (!verticalEdge || EventSystem.current == null)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        Button next = selected == secondaryButton.gameObject ? primaryButton : secondaryButton;
        if (IsUsable(next))
            EventSystem.current.SetSelectedGameObject(next.gameObject);
    }

    private void ConfirmFocusedButton()
    {
        if (completion == null || completion.Task.IsCompleted)
            return;

        Button target = ResolveConfirmTarget(
            primaryButton,
            secondaryButton,
            EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null);
        if (target == null)
            return;

        target.onClick.Invoke();
    }

    private static bool UiModuleOwnsMove()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        return module != null && module.move != null;
    }

    private static InputAction FindUiAction(string actionName)
    {
        InputActionAsset asset = ResolveActionAsset();
        if (asset == null || string.IsNullOrWhiteSpace(actionName))
            return null;

        InputActionMap map = asset.FindActionMap("UI", false);
        return map != null ? map.FindAction(actionName, false) : null;
    }

    private static InputActionAsset ResolveActionAsset()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (module != null && module.actionsAsset != null)
                return module.actionsAsset;
        }

        InputSystemUIInputModule[] modules = FindObjectsByType<InputSystemUIInputModule>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < modules.Length; i++)
        {
            if (modules[i] != null && modules[i].actionsAsset != null)
                return modules[i].actionsAsset;
        }

        return Resources.Load<InputActionAsset>("InputSystem_Actions");
    }

    private static bool EnableIfNeeded(InputAction action)
    {
        if (action == null || action.enabled)
            return false;

        action.Enable();
        return true;
    }

    private static bool IsPromptButton(Button button, GameObject selected)
    {
        return IsUsable(button) && selected == button.gameObject;
    }

    private static bool IsUsable(Button button)
    {
        return button != null && button.gameObject.activeInHierarchy && button.IsInteractable();
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        Image dimmer = GetComponent<Image>();
        if (dimmer != null)
            dimmer.enabled = visible;

        if (root != null && root != gameObject)
            root.SetActive(visible);
    }

    private void ApplySafeArea(bool force = false)
    {
        if (safeAreaRoot == null)
            return;
        Rect area = Screen.safeArea;
        if (!force && area == lastSafeArea)
            return;
        lastSafeArea = area;
        Vector2 min = area.position;
        Vector2 max = area.position + area.size;
        min.x /= Mathf.Max(1f, Screen.width);
        min.y /= Mathf.Max(1f, Screen.height);
        max.x /= Mathf.Max(1f, Screen.width);
        max.y /= Mathf.Max(1f, Screen.height);
        safeAreaRoot.anchorMin = min;
        safeAreaRoot.anchorMax = max;
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }
}
