using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable modal-panel motion and selectable exposure for main-menu object
/// destinations. The sequence controller owns which panel is active and where
/// focus returns when it closes.
/// </summary>
public abstract class MainMenuPanelControllerBase : MonoBehaviour
{
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button closeButton;
    [SerializeField] private List<Selectable> navigationOrder = new List<Selectable>();
    [SerializeField] private Selectable defaultSelection;
    [SerializeField] private float fadeSeconds = 0.16f;
    [SerializeField] private float slidePixels = 42f;

    private Action onClosed;
    private Vector2 restingPosition;
    private bool initialized;

    public bool IsOpen { get; private set; }
    public Transform NavigationRoot => panelRect != null ? panelRect : transform;
    public Selectable DefaultSelection
    {
        get
        {
            if (defaultSelection != null && defaultSelection.gameObject.activeInHierarchy && defaultSelection.IsInteractable())
                return defaultSelection;

            IReadOnlyList<Selectable> usable = GetUsableNavigationOrder();
            return usable.Count > 0 ? usable[0] : null;
        }
    }
    /// <summary>
    /// Selection to use when this panel has multiple authored views. Panels
    /// with one view can use the normal DefaultSelection property.
    /// </summary>
    public virtual Selectable ActiveViewDefaultSelection => DefaultSelection;
    public Selectable FallbackSelection => closeButton != null ? closeButton : DefaultSelection;

    /// <summary>
    /// Raised while open when async content shows or hides a selectable, so the
    /// sequence controller can rebuild gamepad focus.
    /// </summary>
    public event Action<MainMenuPanelControllerBase> SelectablesChanged;

    protected virtual void Awake()
    {
        InitializeIfNeeded();
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);
        // OpenPanel sets IsOpen before SetActive(true). Skip HideImmediate in that
        // path so the first activation does not clear IsOpen / kill raycasts.
        if (!IsOpen)
            HideImmediate();
    }

    protected virtual void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);
        panelGroup?.DOKill();
        panelRect?.DOKill();
    }

    public virtual void OpenPanel(Action closedCallback = null)
    {
        InitializeIfNeeded();
        onClosed = closedCallback;
        IsOpen = true;
        gameObject.SetActive(true);
        // Awake can deactivate during first enable; re-assert open state after.
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        IsOpen = true;

        // Sit above illustration / bubble layers so raycasts hit panel buttons.
        transform.SetAsLastSibling();

        // CanvasGroup.interactable must be true BEFORE we query IsInteractable()
        // for navigation chains / default selection. Otherwise usable order is empty
        // and Explicit up/down links stay null.
        if (panelGroup != null)
        {
            panelGroup.DOKill();
            panelGroup.alpha = 0f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        OnPanelOpening();
        ConfigureExplicitNavigation();

        if (panelGroup != null)
        {
            panelGroup.DOFade(1f, Mathf.Max(0.01f, fadeSeconds)).SetUpdate(true);
        }

        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.anchoredPosition = restingPosition + Vector2.down * slidePixels;
            panelRect.DOAnchorPos(restingPosition, Mathf.Max(0.01f, fadeSeconds))
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }
    }

    /// <summary>Rebuilds Explicit up/down links from currently usable selectables.</summary>
    public void RefreshNavigation()
    {
        ConfigureExplicitNavigation();
    }

    public virtual void ClosePanel()
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        OnPanelClosing();

        if (panelGroup == null)
        {
            CompleteClose();
            return;
        }

        panelGroup.interactable = false;
        panelGroup.blocksRaycasts = false;
        panelGroup.DOKill();
        panelGroup.DOFade(0f, Mathf.Max(0.01f, fadeSeconds))
            .SetUpdate(true)
            .OnComplete(CompleteClose);

        if (panelRect != null)
        {
            panelRect.DOKill();
            panelRect.DOAnchorPos(restingPosition + Vector2.down * slidePixels, Mathf.Max(0.01f, fadeSeconds))
                .SetEase(Ease.InCubic)
                .SetUpdate(true);
        }
    }

    public void HideImmediate()
    {
        InitializeIfNeeded();
        IsOpen = false;
        panelGroup?.DOKill();
        panelRect?.DOKill();

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        if (panelRect != null)
            panelRect.anchoredPosition = restingPosition;

        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    public IReadOnlyList<Selectable> GetUsableNavigationOrder()
    {
        var result = new List<Selectable>();
        for (int i = 0; i < navigationOrder.Count; i++)
        {
            Selectable selectable = navigationOrder[i];
            if (selectable != null && selectable.gameObject.activeInHierarchy && selectable.IsInteractable())
                result.Add(selectable);
        }

        return result;
    }

    protected virtual void OnPanelOpening()
    {
    }

    protected virtual void OnPanelClosing()
    {
    }

    protected void ConfigureExplicitNavigation()
    {
        IReadOnlyList<Selectable> usable = GetUsableNavigationOrder();
        int count = usable.Count;
        for (int i = 0; i < count; i++)
        {
            Selectable current = usable[i];
            Navigation navigation = current.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            navigation.selectOnUp = count > 1 ? usable[(i - 1 + count) % count] : null;
            navigation.selectOnDown = count > 1 ? usable[(i + 1) % count] : null;
            navigation.selectOnLeft = null;
            navigation.selectOnRight = null;
            current.navigation = navigation;
        }
    }

    protected void SetDefaultSelection(Selectable selectable)
    {
        defaultSelection = selectable;
    }

    protected void NotifySelectablesChanged()
    {
        ConfigureExplicitNavigation();
        if (IsOpen)
            SelectablesChanged?.Invoke(this);
    }

    private void CompleteClose()
    {
        if (panelRect != null)
            panelRect.anchoredPosition = restingPosition;
        gameObject.SetActive(false);

        Action callback = onClosed;
        onClosed = null;
        callback?.Invoke();
    }

    private void InitializeIfNeeded()
    {
        if (initialized)
            return;

        initialized = true;
        if (panelGroup == null)
            panelGroup = GetComponent<CanvasGroup>();
        if (panelRect == null)
            panelRect = transform as RectTransform;
        if (panelRect != null)
            restingPosition = panelRect.anchoredPosition;
    }
}
