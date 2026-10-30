using System.Collections;
using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Daily gift overlay: one card with both grants (<c>10 RAM</c> and
/// <c>Signal Orb x1</c>), or a notice such as a full orb stack. Matches the
/// item-obtain card's look. One dismiss control: tap, mobile A, or the
/// Player/Interact action (gamepad South / keyboard). Locks input while shown.
/// Combat.Runtime must not reference this type.
/// </summary>
[DisallowMultipleComponent]
public sealed class DailyLoginClaimPresenter : MonoBehaviour
{
    private const string InteractMapName = "Player";
    private const string InteractActionName = "Interact";
    private const string TitleCopy = "Daily gift";

    [SerializeField] private InventoryItemPresentationCatalogSO presentationCatalog;
    [SerializeField] private InputActionAsset inputActions;

    private static DailyLoginClaimPresenter instance;

    private CanvasGroup canvasGroup;
    private Button dismissButton;
    private Image itemIcon;
    private TMP_Text titleText;
    private TMP_Text ramLine;
    private TMP_Text itemLine;
    private InputLockHandle inputLockHandle;
    private InputAction interactAction;
    private MobileInputManager mobileInput;
    private bool dismissArmed;
    private int showGeneration;

    public static DailyLoginClaimPresenter Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance = FindAnyObjectByType<DailyLoginClaimPresenter>(FindObjectsInactive.Include);
            if (instance != null || !Application.isPlaying)
                return instance;

            var host = new GameObject("DailyLoginClaimPresenter");
            GameSessionContext session = GameSessionContext.Instance;
            if (session != null)
                host.transform.SetParent(session.transform.root, false);
            else
                DontDestroyOnLoad(host);

            instance = host.AddComponent<DailyLoginClaimPresenter>();
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
        UnsubscribeInput();
        ReleaseLock();
    }

    /// <summary>Shows the first grant of the day.</summary>
    public void ShowGrant(long ramAmount, string itemId, int itemQuantity)
    {
        EnsureRuntimeUi();
        titleText.text = TitleCopy;
        ramLine.text = $"{ramAmount} RAM";
        itemLine.text = $"{ResolveItemName(itemId)} x{Mathf.Max(1, itemQuantity)}";
        itemIcon.sprite = ResolveItemIcon(itemId);
        itemIcon.enabled = itemIcon.sprite != null;
        Reveal();
    }

    /// <summary>Shows a single-line notice, for example a full orb stack.</summary>
    public void ShowNotice(string message)
    {
        EnsureRuntimeUi();
        titleText.text = TitleCopy;
        ramLine.text = message ?? "";
        itemLine.text = "";
        itemIcon.enabled = false;
        Reveal();
    }

    public void Hide()
    {
        if (IsShowing)
            HideImmediate();
    }

    private void Reveal()
    {
        IsShowing = true;
        showGeneration++;
        dismissArmed = false;
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        AcquireLock();
        SubscribeInput();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);
        StartCoroutine(ArmDismissAfterFrames());
    }

    private void HideImmediate()
    {
        IsShowing = false;
        dismissArmed = false;
        UnsubscribeInput();
        ReleaseLock();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    // The Interact press that opened the card must not also close it.
    private IEnumerator ArmDismissAfterFrames()
    {
        int generation = showGeneration;
        yield return null;
        yield return null;
        if (generation == showGeneration && IsShowing)
            dismissArmed = true;
    }

    private void TryDismiss()
    {
        if (IsShowing && dismissArmed)
            Hide();
    }

    private void AcquireLock()
    {
        if (inputLockHandle.IsValid)
            return;
        InputLockService service = InputLockService.Instance;
        if (service != null)
            inputLockHandle = service.AcquireLock("DailyLoginClaim", InputLockChannel.All, this);
    }

    private void ReleaseLock()
    {
        if (!inputLockHandle.IsValid)
            return;
        inputLockHandle.Release();
        inputLockHandle = default;
    }

    private void SubscribeInput()
    {
        UnsubscribeInput();

        if (inputActions == null)
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
        InputActionMap map = inputActions != null ? inputActions.FindActionMap(InteractMapName) : null;
        interactAction = map != null ? map.FindAction(InteractActionName) : null;
        if (interactAction != null)
            interactAction.performed += HandleInteractPerformed;

        if (mobileInput == null)
            mobileInput = FindAnyObjectByType<MobileInputManager>(FindObjectsInactive.Include);
        if (mobileInput != null)
            mobileInput.OnAPressed += TryDismiss;

        dismissButton.onClick.AddListener(TryDismiss);
    }

    private void UnsubscribeInput()
    {
        if (interactAction != null)
        {
            interactAction.performed -= HandleInteractPerformed;
            interactAction = null;
        }

        if (mobileInput != null)
            mobileInput.OnAPressed -= TryDismiss;
        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(TryDismiss);
    }

    private void HandleInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            TryDismiss();
    }

    private string ResolveItemName(string itemId)
    {
        if (ResolvePresentation(itemId, out InventoryItemPresentationEntry entry) &&
            !string.IsNullOrWhiteSpace(entry.displayNameOverride))
            return entry.displayNameOverride;
        return PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition def) && !string.IsNullOrWhiteSpace(def.displayName)
            ? def.displayName.Trim()
            : itemId;
    }

    private Sprite ResolveItemIcon(string itemId)
    {
        if (!ResolvePresentation(itemId, out InventoryItemPresentationEntry entry))
            return null;
        return entry.rowIcon != null ? entry.rowIcon : entry.detailPortrait;
    }

    private bool ResolvePresentation(string itemId, out InventoryItemPresentationEntry entry)
    {
        entry = null;
        if (presentationCatalog == null)
            presentationCatalog = Resources.Load<InventoryItemPresentationCatalogSO>("InventoryItemPresentationCatalog");
        return presentationCatalog != null &&
               presentationCatalog.TryResolve(itemId, out entry) &&
               entry != null;
    }

    private void EnsureRuntimeUi()
    {
        if (canvasGroup != null && dismissButton != null && titleText != null)
            return;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 520;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        Stretch(GetComponent<RectTransform>());

        var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SignalJam.UI.MobileControls.SafeAreaAdapter));
        safe.transform.SetParent(transform, false);
        Stretch(safe.GetComponent<RectTransform>());

        // Full-screen dimmer doubles as the tap target and the selected control.
        var dimmer = new GameObject("Dismiss", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dimmer.transform.SetParent(safe.transform, false);
        Stretch(dimmer.GetComponent<RectTransform>());
        Image dimmerImage = dimmer.GetComponent<Image>();
        dimmerImage.color = new Color(0f, 0f, 0f, 0.62f);
        dismissButton = dimmer.GetComponent<Button>();
        dismissButton.targetGraphic = dimmerImage;
        Navigation navigation = dismissButton.navigation;
        navigation.mode = Navigation.Mode.None;
        dismissButton.navigation = navigation;

        var card = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        card.transform.SetParent(safe.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.18f);
        cardRect.anchorMax = new Vector2(0.5f, 0.18f);
        cardRect.pivot = new Vector2(0.5f, 0f);
        cardRect.sizeDelta = new Vector2(720f, 280f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
        cardImage.raycastTarget = false;

        var icon = new GameObject("ItemIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(card.transform, false);
        RectTransform iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchoredPosition = new Vector2(-240f, 0f);
        iconRect.sizeDelta = new Vector2(180f, 180f);
        itemIcon = icon.GetComponent<Image>();
        itemIcon.preserveAspect = true;
        itemIcon.raycastTarget = false;

        titleText = CreateLabel(card.transform, "Title", new Vector2(110f, 82f), 34f, FontStyles.Bold);
        ramLine = CreateLabel(card.transform, "RamLine", new Vector2(110f, 10f), 28f, FontStyles.Normal);
        itemLine = CreateLabel(card.transform, "ItemLine", new Vector2(110f, -52f), 28f, FontStyles.Normal);
        CreateLabel(card.transform, "Hint", new Vector2(110f, -112f), 18f, FontStyles.Italic).text = "Tap or press A to continue";
    }

    private static TMP_Text CreateLabel(Transform parent, string name, Vector2 position, float size, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(440f, 56f);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
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
