using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Passive safe-area modal. It deliberately has no cancel/back route: button B
/// never means forfeit, and the player must explicitly choose the destructive action.
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
        completion?.TrySetResult(PendingBattleResumeChoice.Unavailable);
    }

    private void Update()
    {
        ApplySafeArea();
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
        EventSystem.current?.SetSelectedGameObject(primaryButton.gameObject);
        return completion.Task;
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
        SetVisible(false);
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
