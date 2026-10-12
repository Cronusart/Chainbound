using System.Collections;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Persistent tutorial read-along overlay. Locks movement, interaction, and
/// gameplay menus while shown. Dismiss stays closed until the panel reveal
/// finishes and a short read delay elapses; any button or dimmer tap then dismisses.
/// Combat.Runtime must not reference this type.
/// </summary>
[DisallowMultipleComponent]
public sealed class TutorialReadAlongPresenter : MonoBehaviour
{
    public const string MenuSuppressionReason = "TutorialReadAlong";
    public const string InputLockReason = "TutorialReadAlong";
    public const float DefaultDismissDelayAfterRevealSeconds = 1.5f;

    /// <summary>
    /// Shortens the pause after the reveal without rewriting authored prefab values.
    /// </summary>
    private const float RevealReadDelayTrimSeconds = 0.5f;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Button dismissButton;
    [SerializeField] private ItemObtainPanelReveal panelReveal;
    [SerializeField, Min(0f)] private float dismissDelayAfterRevealSeconds = DefaultDismissDelayAfterRevealSeconds;

    private static TutorialReadAlongPresenter instance;

    private InputLockHandle inputLockHandle;
    private GameplayMenuAvailabilityToken menuSuppression;
    private bool hasMenuSuppression;
    private MobileHudToken hudToken;
    private bool hasHudToken;
    private InputSystemAnyButtonGate anyButtonGate;
    private bool dismissArmed;
    private int showGeneration;
    private Coroutine dimmerReleaseRoutine;

    public static TutorialReadAlongPresenter Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<TutorialReadAlongPresenter>(FindObjectsInactive.Include);
            return instance;
        }
    }

    public bool IsShowing { get; private set; }

    private void Awake()
    {
        instance = this;
        EnsureRuntimeUi();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        HideImmediate();
    }

    public bool Show(TutorialReadAlongSnippetSO snippet)
    {
        if (snippet == null || !snippet.IsValid)
            return false;

        EnsureRuntimeUi();
        BindCopy(snippet);

        if (dimmerReleaseRoutine != null)
        {
            StopCoroutine(dimmerReleaseRoutine);
            dimmerReleaseRoutine = null;
        }

        IsShowing = true;
        showGeneration++;
        dismissArmed = false;
        gameObject.SetActive(true);

        if (panelReveal != null)
            panelReveal.Reveal(keepDimmer: true);
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        AcquireGameplayFreeze(snippet.KeepsHudVisible);
        SubscribeDismiss();
        SetDismissInteractable(false);
        if (Application.isPlaying && isActiveAndEnabled)
            StartCoroutine(ArmDismissAfterReveal(snippet.Emphasis));
        return true;
    }

    public void Hide()
    {
        if (!IsShowing)
            return;

        IsShowing = false;
        dismissArmed = false;
        UnsubscribeDismiss();
        MobileHudControlEmphasis.Instance?.Cancel();
        ReleaseGameplayFreeze();

        if (Application.isPlaying && isActiveAndEnabled && panelReveal != null)
        {
            panelReveal.HidePanelKeepDimmer();
            if (dimmerReleaseRoutine != null)
                StopCoroutine(dimmerReleaseRoutine);
            dimmerReleaseRoutine = StartCoroutine(ReleaseDimmerIfIdle());
            return;
        }

        HideVisual();
    }

    public IEnumerator WaitUntilDismissed()
    {
        while (IsShowing)
            yield return null;
    }

    private void HideImmediate()
    {
        if (dimmerReleaseRoutine != null)
        {
            StopCoroutine(dimmerReleaseRoutine);
            dimmerReleaseRoutine = null;
        }

        IsShowing = false;
        dismissArmed = false;
        UnsubscribeDismiss();
        MobileHudControlEmphasis.Instance?.Cancel();
        ReleaseGameplayFreeze();
        HideVisual();
    }

    private IEnumerator ReleaseDimmerIfIdle()
    {
        int generation = showGeneration;
        yield return null;
        dimmerReleaseRoutine = null;
        if (generation != showGeneration || IsShowing)
            yield break;

        HideVisual();
    }

    private void HideVisual()
    {
        if (panelReveal != null)
            panelReveal.HideImmediate();
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private IEnumerator ArmDismissAfterReveal(MobileHudEmphasisTarget emphasis)
    {
        int generation = showGeneration;
        if (panelReveal != null)
        {
            while (panelReveal.IsPlaying)
            {
                if (generation != showGeneration || !IsShowing)
                    yield break;
                yield return null;
            }
        }

        if (emphasis != MobileHudEmphasisTarget.None)
        {
            MobileHudControlEmphasis emphasisPlayer = MobileHudControlEmphasis.Instance;
            if (emphasisPlayer == null)
                emphasisPlayer = FindFirstObjectByType<MobileHudControlEmphasis>(FindObjectsInactive.Include);

            if (emphasisPlayer != null)
            {
                emphasisPlayer.Begin(emphasis);
                while (emphasisPlayer.IsPlaying)
                {
                    if (generation != showGeneration || !IsShowing)
                        yield break;
                    yield return null;
                }
            }
        }

        if (generation != showGeneration || !IsShowing)
            yield break;

        float elapsed = 0f;
        float readDelay = Mathf.Max(0f, dismissDelayAfterRevealSeconds - RevealReadDelayTrimSeconds);
        while (elapsed < readDelay)
        {
            if (generation != showGeneration || !IsShowing)
                yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (generation != showGeneration || !IsShowing)
            yield break;

        dismissArmed = true;
        SetDismissInteractable(true);
        ArmAnyButtonGate();
    }

    private void BindCopy(TutorialReadAlongSnippetSO snippet)
    {
        if (titleText != null)
        {
            titleText.gameObject.SetActive(!string.IsNullOrWhiteSpace(snippet.Title));
            titleText.text = snippet.Title;
        }

        if (bodyText != null)
            bodyText.text = snippet.Body;

        if (hintText != null)
            hintText.text = snippet.ContinueHint;

        if (iconImage != null)
        {
            iconImage.sprite = snippet.Icon;
            iconImage.enabled = snippet.Icon != null;
            iconImage.gameObject.SetActive(snippet.Icon != null);
        }
    }

    private void AcquireGameplayFreeze(bool keepHudVisible)
    {
        InputLockService lockService = ResolveInputLockService();
        if (lockService != null && !inputLockHandle.IsValid)
            inputLockHandle = lockService.AcquireLock(InputLockReason, InputLockChannel.All, this);

        GameplayMenuAvailabilityService menuService = ResolveMenuAvailability();
        if (menuService != null && !hasMenuSuppression)
        {
            menuSuppression = menuService.AcquireSuppression(MenuSuppressionReason);
            hasMenuSuppression = menuSuppression.IsValid;
        }

        if (keepHudVisible)
        {
            ReleaseHudToken();
            return;
        }

        MobileHudService hudService = ResolveHudService();
        if (hudService != null && !hasHudToken)
        {
            hudToken = hudService.AcquireContext(MobileHudContext.Dialogue, InputLockReason);
            hasHudToken = hudToken.IsValid;
        }
    }

    private void ReleaseHudToken()
    {
        if (!hasHudToken)
            return;

        hudToken.Release();
        hudToken = default;
        hasHudToken = false;
    }

    private void ReleaseGameplayFreeze()
    {
        if (inputLockHandle.IsValid)
        {
            inputLockHandle.Release();
            inputLockHandle = default;
        }

        if (hasMenuSuppression)
        {
            menuSuppression.Release();
            menuSuppression = default;
            hasMenuSuppression = false;
        }

        ReleaseHudToken();
    }

    private void SubscribeDismiss()
    {
        UnsubscribeDismiss();
        if (dismissButton == null)
            return;

        dismissButton.onClick.AddListener(HandleDismissClicked);
    }

    private void SetDismissInteractable(bool interactable)
    {
        if (dismissButton == null)
            return;

        dismissButton.interactable = interactable;
        if (!interactable || EventSystem.current == null)
            return;

        EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);
    }

    private void UnsubscribeDismiss()
    {
        anyButtonGate?.Disarm();
        anyButtonGate = null;

        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(HandleDismissClicked);
    }

    private void ArmAnyButtonGate()
    {
        anyButtonGate?.Disarm();
        anyButtonGate = new InputSystemAnyButtonGate();
        anyButtonGate.Arm(TryDismiss);
    }

    private void HandleDismissClicked() => TryDismiss();

    private void TryDismiss()
    {
        if (!IsShowing || !dismissArmed)
            return;
        Hide();
    }

    private static InputLockService ResolveInputLockService()
    {
        InputLockService service = InputLockService.Instance;
        if (service != null)
            return service;
        return FindFirstObjectByType<InputLockService>(FindObjectsInactive.Include);
    }

    private static GameplayMenuAvailabilityService ResolveMenuAvailability()
    {
        GameplayMenuAvailabilityService service = FindFirstObjectByType<GameplayMenuAvailabilityService>(
            FindObjectsInactive.Include);
        if (service != null)
            return service;
        return Application.isPlaying ? GameplayMenuAvailabilityService.Instance : null;
    }

    private static MobileHudService ResolveHudService()
    {
        return FindFirstObjectByType<MobileHudService>(FindObjectsInactive.Include);
    }

    private void EnsureRuntimeUi()
    {
        BindAuthoredWidgets();
        if (canvasGroup != null && titleText != null && bodyText != null && dismissButton != null)
        {
            EnsurePanelReveal();
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 530;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        RectTransform rootRect = GetComponent<RectTransform>();
        if (rootRect == null)
            rootRect = gameObject.AddComponent<RectTransform>();
        Stretch(rootRect);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Transform safe = transform.Find("SafeArea");
        GameObject safeGo;
        if (safe == null)
        {
            safeGo = new GameObject("SafeArea", typeof(RectTransform), typeof(SignalJam.UI.MobileControls.SafeAreaAdapter));
            safeGo.transform.SetParent(transform, false);
            Stretch(safeGo.GetComponent<RectTransform>());
        }
        else
        {
            safeGo = safe.gameObject;
            if (safeGo.GetComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>() == null)
                safeGo.AddComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>();
        }

        if (dismissButton == null)
        {
            GameObject dimmer = new GameObject("Dismiss", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            dimmer.transform.SetParent(safeGo.transform, false);
            Stretch(dimmer.GetComponent<RectTransform>());
            Image dimmerImage = dimmer.GetComponent<Image>();
            dimmerImage.color = new Color(0f, 0f, 0f, 0.62f);
            dismissButton = dimmer.GetComponent<Button>();
            dismissButton.targetGraphic = dimmerImage;
            dismissButton.transition = Selectable.Transition.None;
        }

        Transform card = safeGo.transform.Find("Card");
        GameObject cardGo;
        if (card == null)
        {
            cardGo = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(safeGo.transform, false);
            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(980f, 320f);
            Image cardImage = cardGo.GetComponent<Image>();
            cardImage.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
            cardImage.raycastTarget = false;
        }
        else
        {
            cardGo = card.gameObject;
        }

        BindAuthoredWidgets();

        Transform details = FindChildByName(cardGo.transform, "Details");
        if (details == null)
            details = cardGo.transform;
        if (titleText == null)
            titleText = CreateLabel(details, "Title", new Vector2(40f, 210f), new Vector2(900f, 56f), 32, FontStyles.Bold);
        if (bodyText == null)
            bodyText = CreateLabel(details, "Body", new Vector2(40f, 88f), new Vector2(900f, 110f), 24, FontStyles.Normal);
        if (hintText == null)
            hintText = CreateLabel(details, "Hint", new Vector2(40f, 24f), new Vector2(900f, 40f), 18, FontStyles.Italic);

        EnsurePanelReveal();
    }

    private void BindAuthoredWidgets()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (panelReveal == null)
            panelReveal = GetComponent<ItemObtainPanelReveal>();
        if (dismissButton == null)
        {
            Transform dismiss = FindChildByName(transform, "Dismiss");
            if (dismiss != null)
                dismissButton = dismiss.GetComponent<Button>();
        }

        if (iconImage == null)
        {
            Transform icon = FindChildByName(transform, "Icon");
            if (icon != null)
                iconImage = icon.GetComponent<Image>();
        }

        if (titleText == null)
        {
            Transform title = FindChildByName(transform, "Title");
            if (title != null)
                titleText = title.GetComponent<TMP_Text>();
        }

        if (bodyText == null)
        {
            Transform body = FindChildByName(transform, "Body");
            if (body != null)
                bodyText = body.GetComponent<TMP_Text>();
        }

        if (hintText == null)
        {
            Transform hint = FindChildByName(transform, "Hint");
            if (hint != null)
                hintText = hint.GetComponent<TMP_Text>();
        }
    }

    private void EnsurePanelReveal()
    {
        if (panelReveal == null)
            panelReveal = GetComponent<ItemObtainPanelReveal>();
        if (panelReveal == null)
            panelReveal = gameObject.AddComponent<ItemObtainPanelReveal>();
        panelReveal.AutoConfigure();
    }

    private static Transform FindChildByName(Transform root, string name)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        Transform direct = root.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 anchoredPos,
        Vector2 size,
        int fontSize,
        FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.richText = true;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
            return;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
