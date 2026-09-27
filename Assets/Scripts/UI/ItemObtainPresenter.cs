using Chainbound.Combat;
using SignalJam.UI.Audio;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Overworld item-obtain overlay. Reusable for world pickups, Mart shelves, and later side-drops.
/// Combat.Runtime must not reference this type.
/// </summary>
public interface IItemObtainPresenter
{
    bool IsShowing { get; }

    void Show(string itemId, int quantity);

    void Hide();
}

/// <summary>
/// Persistent obtain panel. Locks input while shown; A / tap / Interact dismisses.
/// </summary>
[DisallowMultipleComponent]
public sealed class ItemObtainPresenter : MonoBehaviour, IItemObtainPresenter
{
    private const string InteractMapName = "Player";
    private const string InteractActionName = "Interact";

    [SerializeField] private InventoryItemPresentationCatalogSO presentationCatalog;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text titleText;
    [Tooltip("Legacy quantity label. Hidden; count now lives in the title.")]
    [SerializeField] private TMP_Text quantityText;
    [Tooltip("Catalog item description, same copy as the inventory inspect body.")]
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button dismissButton;
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private ItemObtainPanelReveal panelReveal;
    [SerializeField] private ItemObtainSfxProfileSO obtainSfxProfile;

    private static ItemObtainPresenter instance;

    private InputLockHandle inputLockHandle;
    private InputAction interactAction;
    private MobileInputManager mobileInput;
    private bool dismissArmed;
    private int showGeneration;

    public static ItemObtainPresenter Instance
    {
        get
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<ItemObtainPresenter>(FindObjectsInactive.Include);
            if (instance != null)
                return instance;

            if (!Application.isPlaying)
                return null;

            Transform parent = null;
            GameSessionContext session = GameSessionContext.Instance;
            if (session != null)
                parent = session.transform.root;

            var host = new GameObject("ItemObtainPresenter");
            if (parent != null)
                host.transform.SetParent(parent, false);
            else
                DontDestroyOnLoad(host);

            instance = host.AddComponent<ItemObtainPresenter>();
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

    public void Show(string itemId, int quantity)
    {
        string normalized = (itemId ?? "").Trim();
        int qty = Mathf.Max(1, quantity);
        if (normalized.Length == 0)
            return;

        EnsureRuntimeUi();
        BindCopy(normalized, qty);

        IsShowing = true;
        showGeneration++;
        dismissArmed = false;
        gameObject.SetActive(true);
        PlayObtainSound(normalized);
        if (panelReveal != null)
            panelReveal.Reveal();
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        AcquireLock();
        SubscribeInput();

        if (dismissButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(dismissButton.gameObject);

        StartCoroutine(ArmDismissNextFrame());
    }

    public void Hide()
    {
        if (!IsShowing)
            return;

        HideImmediate();
    }

    private void HideImmediate()
    {
        IsShowing = false;
        dismissArmed = false;
        UnsubscribeInput();
        ReleaseLock();

        if (panelReveal != null)
            panelReveal.HideImmediate();
        else if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private System.Collections.IEnumerator ArmDismissNextFrame()
    {
        int generation = showGeneration;
        yield return null;
        yield return null;
        if (generation == showGeneration && IsShowing)
            dismissArmed = true;
    }

    private void BindCopy(string itemId, int quantity)
    {
        string displayName = itemId;
        string description = "";
        Sprite portrait = null;
        Color tint = PlayerItemCategoryPalette.Field;
        PlayerItemCategory category = PlayerItemCategory.Field;

        if (PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition def) && def != null)
        {
            if (!string.IsNullOrWhiteSpace(def.displayName))
                displayName = def.displayName.Trim();
            if (!string.IsNullOrWhiteSpace(def.description))
                description = def.description.Trim();
            category = def.category;
        }

        if (presentationCatalog == null)
            presentationCatalog = Resources.Load<InventoryItemPresentationCatalogSO>("InventoryItemPresentationCatalog");

        if (presentationCatalog != null &&
            presentationCatalog.TryResolve(itemId, out InventoryItemPresentationEntry entry) &&
            entry != null)
        {
            if (!string.IsNullOrWhiteSpace(entry.displayNameOverride))
                displayName = entry.displayNameOverride;
            portrait = entry.detailPortrait != null ? entry.detailPortrait : entry.rowIcon;
            if (portrait == null)
                portrait = presentationCatalog.FallbackDetailPortrait != null
                    ? presentationCatalog.FallbackDetailPortrait
                    : presentationCatalog.FallbackRowIcon;
            tint = entry.fallbackTint.a > 0.01f ? entry.fallbackTint : presentationCatalog.FallbackTint;
        }
        else if (presentationCatalog != null)
        {
            portrait = presentationCatalog.FallbackDetailPortrait != null
                ? presentationCatalog.FallbackDetailPortrait
                : presentationCatalog.FallbackRowIcon;
            tint = presentationCatalog.FallbackTint;
        }

        if (titleText != null)
        {
            titleText.richText = true;
            SetTextPreserveColor(
                titleText,
                ItemObtainFoundCopy.FormatTitle(displayName, category, quantity));
        }
        HideQuantityLabel();
        if (promptText != null)
            SetTextPreserveColor(promptText, description);

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.color = portrait != null ? Color.white : tint;
            portraitImage.enabled = true;
        }
    }

    private void PlayObtainSound(string itemId)
    {
        if (obtainSfxProfile == null)
            return;

        PlayerItemCategory category = PlayerItemCategory.Field;
        if (PlayerItemCatalogSeed.TryGet(itemId, out PlayerItemDefinition definition) && definition != null)
            category = definition.category;

        SoundEventSO soundEvent = obtainSfxProfile.Resolve(category);
        if (soundEvent == null)
            return;

        AudioService service = AudioService.Instance;
        if (service != null)
            service.Play(soundEvent);
    }

    private void AcquireLock()
    {
        if (inputLockHandle.IsValid)
            return;
        InputLockService service = InputLockService.Instance;
        if (service == null)
            return;
        inputLockHandle = service.AcquireLock("ItemObtain", InputLockChannel.All, this);
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
        if (inputActions != null)
        {
            InputActionMap map = inputActions.FindActionMap(InteractMapName);
            if (map != null)
                interactAction = map.FindAction(InteractActionName);
        }

        if (interactAction != null)
            interactAction.performed += HandleInteractPerformed;

        if (mobileInput == null)
            mobileInput = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);
        if (mobileInput != null)
            mobileInput.OnAPressed += HandleMobileAPressed;

        if (dismissButton != null)
        {
            dismissButton.onClick.RemoveListener(HandleDismissClicked);
            dismissButton.onClick.AddListener(HandleDismissClicked);
        }
    }

    private void UnsubscribeInput()
    {
        if (interactAction != null)
        {
            interactAction.performed -= HandleInteractPerformed;
            interactAction = null;
        }

        if (mobileInput != null)
            mobileInput.OnAPressed -= HandleMobileAPressed;

        if (dismissButton != null)
            dismissButton.onClick.RemoveListener(HandleDismissClicked);
    }

    private void HandleInteractPerformed(InputAction.CallbackContext ctx)
    {
        if (ctx.performed)
            TryDismiss();
    }

    private void HandleMobileAPressed() => TryDismiss();

    private void HandleDismissClicked() => TryDismiss();

    private void TryDismiss()
    {
        if (!IsShowing || !dismissArmed)
            return;
        Hide();
    }

    private void EnsureRuntimeUi()
    {
        BindAuthoredWidgets();
        HideQuantityLabel();
        if (canvasGroup != null && portraitImage != null && titleText != null &&
            promptText != null && dismissButton != null)
        {
            EnsurePanelReveal();
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 520;

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
        }

        Transform card = safeGo.transform.Find("Card");
        GameObject cardGo;
        if (card == null)
        {
            cardGo = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(safeGo.transform, false);
            RectTransform cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.18f);
            cardRect.anchorMax = new Vector2(0.5f, 0.18f);
            cardRect.pivot = new Vector2(0.5f, 0f);
            cardRect.anchoredPosition = Vector2.zero;
            cardRect.sizeDelta = new Vector2(720f, 280f);
            Image cardImage = cardGo.GetComponent<Image>();
            cardImage.color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
            cardImage.raycastTarget = false;
        }
        else
        {
            cardGo = card.gameObject;
        }

        BindAuthoredWidgets();

        Transform portraitParent = FindChildByName(cardGo.transform, "PortraitBG");
        if (portraitParent == null)
            portraitParent = cardGo.transform;
        if (portraitImage == null)
        {
            GameObject portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            portraitGo.transform.SetParent(portraitParent, false);
            RectTransform portraitRect = portraitGo.GetComponent<RectTransform>();
            portraitRect.anchorMin = Vector2.zero;
            portraitRect.anchorMax = Vector2.one;
            portraitRect.pivot = new Vector2(0.5f, 0.5f);
            portraitRect.anchoredPosition = Vector2.zero;
            portraitRect.sizeDelta = new Vector2(-50f, -50f);
            portraitImage = portraitGo.GetComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
        }

        Transform details = FindChildByName(cardGo.transform, "Details");
        if (details == null)
            details = cardGo.transform;
        if (titleText == null)
            titleText = CreateLabel(details, "Title", new Vector2(210f, 150f), new Vector2(480f, 72f), 32, FontStyles.Bold);
        if (promptText == null)
            promptText = CreateLabel(details, "Prompt", new Vector2(210f, 8f), new Vector2(480f, 96f), 22, FontStyles.Normal);

        HideQuantityLabel(cardGo.transform);
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

        if (portraitImage == null)
        {
            Transform portrait = FindChildByName(transform, "Portrait");
            if (portrait != null)
                portraitImage = portrait.GetComponent<Image>();
        }

        if (titleText == null)
        {
            Transform title = FindChildByName(transform, "Title");
            if (title != null)
                titleText = title.GetComponent<TMP_Text>();
        }

        if (promptText == null)
        {
            Transform prompt = FindChildByName(transform, "Prompt");
            if (prompt != null)
                promptText = prompt.GetComponent<TMP_Text>();
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

    private void HideQuantityLabel(Transform searchRoot = null)
    {
        if (quantityText != null)
            quantityText.gameObject.SetActive(false);

        Transform root = searchRoot != null ? searchRoot : transform;
        Transform quantity = FindChildByName(root, "Quantity");
        if (quantity != null)
            quantity.gameObject.SetActive(false);
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

    /// <summary>
    /// Writes copy without replacing the prefab vertex color. Category tint lives
    /// only in the title's item-name rich-text span.
    /// </summary>
    private static void SetTextPreserveColor(TMP_Text label, string value)
    {
        if (label == null)
            return;

        Color authored = label.color;
        label.text = value ?? "";
        if (label.color != authored)
            label.color = authored;
    }

    private static TMP_Text CreateLabel(Transform parent, string name, Vector2 anchoredPos, Vector2 size, int fontSize, FontStyles style)
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
