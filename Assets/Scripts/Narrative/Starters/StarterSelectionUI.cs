using System.Collections;
using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MenuNavigation.PartyTab;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// Mobile-friendly starter preview overlay.
/// Shows one world-selected starter at a time with confirm/back actions.
/// Falls back to building its own runtime UGUI hierarchy when references are empty.
/// </summary>
public class StarterSelectionUI : MonoBehaviour, ICaptureSuccessPortraitPresenter
{
    private const string PanelLockReason = "StarterSelection";
    private const float RuntimeTypeIconSize = 64f;
    private const float NameEntryPreviewInset = 1000f;
    private const float NameEntryFrameDrop = 30f;
    private const float NameEntryInputPosY = -15f;
    private const float NameEntryLayoutDuration = 0.36f;
    private static readonly Vector2 PanelScanlineTiling = new Vector2(0.4f, 0.4f);
    private static readonly Color DigitalAccent = new Color(0.62f, 0.96f, 1f, 1f);

    private enum PresentationMode
    {
        Browse,
        Naming,
        Closing,
    }

    [Header("UI References")]
    [SerializeField] private GameObject selectionPanel;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private StarterPreviewVisual previewVisual;
    [SerializeField] private TextMeshProUGUI speciesNameText;
    [FormerlySerializedAs("primaryTypeBadge")]
    [SerializeField] private Image typeIcon;
    [FormerlySerializedAs("primaryTypeText")]
    [SerializeField] private TextMeshProUGUI typeLabel;
    [SerializeField] private Image secondaryTypeBadge;
    [SerializeField] private TextMeshProUGUI secondaryTypeText;
    [FormerlySerializedAs("typeDescriptionText")]
    [SerializeField] private TextMeshProUGUI roleText;
    [SerializeField] private TextMeshProUGUI speciesDescriptionText;
    [SerializeField] private Image typeBackgroundImage;
    [SerializeField] private Image pedestalImage;
    [SerializeField] private Sprite pedestalSprite;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;
    [Tooltip("Hidden until the starter-name step begins. May be authored as a shell; the input components are completed at runtime.")]
    [SerializeField] private TMP_InputField starterNameInputField;

    [Header("Data")]
    [SerializeField] private ChainlingTypeDisplaySO typeDisplayData;
    [SerializeField] private TypeIconPaletteSO typeIconPalette;
    [SerializeField] private ChainlingPartyTypeChromePaletteSO typeChromePalette;
    [SerializeField] private Texture panelScanlineTexture;

    [Header("Settings")]
    [SerializeField] private string title = "Choose Your Chainling";
    [SerializeField, Min(0.01f)] private float panelExpandXDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float panelExpandYDuration = 0.38f;
    [SerializeField, Range(0.01f, 0.25f)] private float panelProjectionLineHeight = 0.05f;
    [SerializeField, Min(0f)] private float panelExpandYOvershoot = 1.35f;
    [SerializeField, Min(0f)] private float dimOverlayFadeDuration = 0.14f;
    [SerializeField, Min(0f)] private float panelContentDelay = 0.25f;
    [SerializeField, Min(0f)] private float panelContentFadeDuration = 0.16f;
    [SerializeField, Min(1f)] private float typewriterCharactersPerSecond = 42f;

    [Header("Mobile HUD Navigation")]
    [SerializeField] private bool enableMobileHudNavigation = true;
    [SerializeField, Range(0.1f, 1f)] private float mobileNavigationThreshold = 0.5f;
    [SerializeField] private float mobileNavigationInitialDelay = 0.28f;
    [SerializeField] private float mobileNavigationRepeatRate = 0.12f;

    [Header("Capture portrait")]
    [Tooltip("Played once as the captured-species portrait opens. Combat music ducks until this one-shot ends.")]
    [SerializeField] private SoundEventSO victoryChime;
    [SerializeField, Range(0f, 1f)] private float portraitDuckMultiplier = 0.05f;
    [SerializeField, Min(0f)] private float portraitDuckAttackSeconds = 0.08f;
    [SerializeField, Min(0f)] private float portraitDuckReleaseSeconds = 0.5f;
    [SerializeField, Min(0f)] private float portraitDismissDelaySeconds = 1f;
    [Tooltip("{0} is the captured species name. Shown with a per-character reveal on the portrait.")]
    [SerializeField] private string portraitBannerFormat = "CAPTURED!\n{0}";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<StarterOption> registeredOptions = new List<StarterOption>();
    private StarterOption currentOption;
    private bool isShowing;
    private GameObject runtimeCanvasRoot;
    private InputLockHandle inputLockHandle;
    private bool hasInputLock;
    private MobileHudToken hudToken;
    private Coroutine selectRoutine;
    private Coroutine panelOpeningRoutine;
    private Coroutine typewriterRoutine;
    private Tween panelContentRevealTween;
    private Tween dimOverlayRevealTween;
    private Tween titleRevealTween;
    private Tween namingPresentationTween;
    private Tween nameEntryLayoutTween;
    private Sequence confirmDigitalTween;
    private Sequence backDigitalTween;
    private Sequence typeIconDigitalTween;
    private MobileInputManager mobileInputManager;
    private Vector2 lastMobileNavigation;
    private float nextMobileNavigationTime;
    private bool panelInputArmed;
    private readonly List<CanvasGroup> panelContentGroups = new List<CanvasGroup>();
    private PresentationMode presentationMode;
    private RectTransform visualFrameRect;
    private bool visualFramePoseCaptured;
    private Vector2 visualFrameAnchorMin;
    private Vector2 visualFrameAnchorMax;
    private Vector2 visualFrameAnchoredPosition;
    private Vector2 visualFrameSizeDelta;
    private Vector2 visualFramePivot;
    private Color visualFrameRestColor;
    private bool visualFrameColorCaptured;
    private bool portraitReveal;
    private CombatVisualProfileSO portraitProfile;
    private Sprite portraitFallback;
    private MusicDuckToken activePortraitDuck;
    private Coroutine portraitDuckRoutine;
    private bool speciesNamePoseCaptured;
    private Transform speciesNameParent;
    private int speciesNameSiblingIndex;
    private Vector2 speciesNameAnchorMin;
    private Vector2 speciesNameAnchorMax;
    private Vector2 speciesNameAnchoredPosition;
    private Vector2 speciesNameSizeDelta;
    private Vector2 speciesNamePivot;
    private Vector3 speciesNameLocalScale;
    private TextAlignmentOptions speciesNameAlignment;
    private float speciesNameCharacterSpacing;
    private bool speciesNameSpacingCaptured;
    private TmpCharacterFlourish portraitBanner;
    private Coroutine portraitBannerRoutine;
    private bool portraitPlateCaptured;
    private Color authoredPortraitPlateColor;
    private bool starterNameInputResolved;
    private bool starterNameInputUsesAuthoredPose;
    private bool starterNameTextAuthored;
    private bool starterNamePlaceholderAuthored;
    private bool nameEntryLayoutApplied;
    private bool previewInsetCaptured;
    private Vector2 previewOffsetMin;
    private Vector2 previewOffsetMax;
    private bool nameInputPoseCaptured;
    private Vector2 nameInputAnchoredPosition;
    private float nameEntryInsetTween;
    private bool digitalChromeRestCaptured;
    private int digitalChromeToken;
    private Color confirmDigitalRestColor;
    private Color backDigitalRestColor;
    private bool confirmDigitalRestValid;
    private bool backDigitalRestValid;
    private Vector2 typeIconRestPosition;
    private Vector3 typeIconRestScale;
    private Color typeIconRestColor;
    private bool typeIconRestValid;
    private RectTransform confirmLabelRect;
    private RectTransform backLabelRect;
    private Vector2 confirmLabelRestPosition;
    private Vector2 backLabelRestPosition;

    public StarterPreviewVisual PreviewVisual => previewVisual;
    public TMP_InputField StarterNameInputField => starterNameInputField;
    public string CurrentStarterDisplayName => currentOption != null ? currentOption.DisplayName : string.Empty;
    public bool IsInNamingPresentation => presentationMode == PresentationMode.Naming;

    private void Awake()
    {
        EnsureUi();
        EnsureStarterNameInput();
        BindUiEvents();
        SetVisible(false);
    }

    private void Update()
    {
        if (!isShowing)
            return;

        if (presentationMode != PresentationMode.Browse)
            return;

        KeepDefaultSelection();
        ProcessMobileHudNavigation();
        if (WasUiCancelPressedThisFrame())
            OnBackClicked();
    }

    private void OnDestroy()
    {
        StopPanelPresentation();
        ReleasePortraitDuck();
        UnbindUiEvents();
        UnsubscribeMobileHudInput();
        ReleasePanelLocks();
    }

    /// <summary>
    /// Opens the confirmed-starter portrait (image and species name, no detail cards)
    /// for a chainling that just left the combat slot. The chime starts with the
    /// panel. Any button closes it once the open and glitch have finished, plus a
    /// short hold.
    /// </summary>
    public IEnumerator Present(CaptureSuccessPortraitRequest request)
    {
        if (request == null)
            yield break;

        EnsureUi();
        if (isShowing)
            HideSelection();

        portraitReveal = true;
        portraitProfile = request.visualProfile;
        portraitFallback = request.fallbackSprite;
        currentOption = null;
        isShowing = true;
        presentationMode = PresentationMode.Naming;
        panelInputArmed = false;

        string speciesName = string.IsNullOrWhiteSpace(request.speciesDisplayName)
            ? "Chainling"
            : request.speciesDisplayName.Trim();
        PreparePortraitChrome(speciesName);
        SetVisible(true);
        AcquirePanelLocks();
        BeginPortraitChime();
        BeginPanelPresentation();

        float elapsed = 0f;
        const float openGuardSeconds = 12f;
        while (elapsed < openGuardSeconds &&
               (panelOpeningRoutine != null || (previewVisual != null && !previewVisual.HasSettledReveal)))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (portraitDismissDelaySeconds > 0f)
            yield return new WaitForSecondsRealtime(portraitDismissDelaySeconds);

        bool pressed = false;
        InputSystemAnyButtonGate gate = new InputSystemAnyButtonGate();
        try
        {
            gate.Arm(() => pressed = true);
            while (!pressed && isShowing)
                yield return null;
        }
        finally
        {
            gate.Dispose();
        }

        if (isShowing)
            yield return DismissSelectionAnimated();
    }

    public void ShowSelection(List<StarterOption> options)
    {
        PrepareSelection(options);
    }

    public void PrepareSelection(List<StarterOption> options)
    {
        EnsureUi();

        registeredOptions.Clear();
        if (options != null)
            registeredOptions.AddRange(options);

        currentOption = null;
        ResetNamingPresentation();
        SetVisible(false);

        if (titleText != null)
            titleText.text = title;

        if (enableDebugLogs)
            Debug.Log($"[StarterSelectionUI] Armed preview selection with {registeredOptions.Count} option(s).");
    }

    public void ShowOption(StarterOption option)
    {
        EnsureUi();
        EnsureTypePresentation();
        EnsurePanelScanlines();

        currentOption = option;
        isShowing = option != null;
        ResetNamingPresentation();
        panelInputArmed = false;
        SetVisible(isShowing);

        if (!isShowing)
            return;

        AcquirePanelLocks();
        EnsureButtonNavigation();

        if (titleText != null)
            titleText.text = title;

        if (speciesNameText != null)
        {
            string speciesName = string.IsNullOrWhiteSpace(option.DisplayName) ? option.ChainlingId : option.DisplayName;
            speciesNameText.text = StarterSelectionCopy.FormatSpecies(speciesName);
        }

        if (speciesDescriptionText != null)
            speciesDescriptionText.text = StarterSelectionCopy.FormatLore(option.Description);

        ApplyTypeChrome(option.PrimaryType);
        ApplyTypeIdentity(option.PrimaryType);
        BindRole(option.SpeciesDefinition);

        // Leave the authored visual frame empty until the panel content is
        // visible. The Chainling then enters on its own beat instead of
        // competing with the projection reveal.
        previewVisual?.Clear();
        BeginPanelPresentation();

        if (enableDebugLogs)
            Debug.Log($"[StarterSelectionUI] Showing preview for starter '{option.ChainlingId}'.");
    }

    public void HideSelection()
    {
        currentOption = null;
        isShowing = false;
        ResetNamingPresentation();
        StopPanelPresentation();
        previewVisual?.Clear();
        SetVisible(false);

        if (enableDebugLogs)
            Debug.Log("[StarterSelectionUI] Hiding starter preview.");
    }

    /// <summary>
    /// Locks the selected option into a neutral naming pose while leaving the
    /// preview visible for the dialogue and optional OSK step.
    /// </summary>
    public void EnterNamingPresentation()
    {
        if (!isShowing || currentOption == null || presentationMode != PresentationMode.Browse)
            return;

        presentationMode = PresentationMode.Naming;
        panelInputArmed = false;
        StopSelectRoutine();
        StopDigitalChrome();
        ClearPanelSelection();
        HideStarterNameInput();
        ResolveDetailsPanel();
        CacheVisualFramePose();

        SetPanelTitleVisible(false);
        if (detailsPanel != null)
            detailsPanel.SetActive(false);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);
        if (backButton != null)
            backButton.gameObject.SetActive(false);

        if (visualFrameRect != null)
        {
            visualFrameRect.DOKill();
            namingPresentationTween?.Kill();
            namingPresentationTween = DOTween.Sequence()
                .SetUpdate(true)
                .Append(visualFrameRect.DOAnchorMin(new Vector2(0.5f, 0.5f), 0.24f).SetEase(Ease.OutCubic))
                .Join(visualFrameRect.DOAnchorMax(new Vector2(0.5f, 0.5f), 0.24f).SetEase(Ease.OutCubic))
                .Join(visualFrameRect.DOPivot(new Vector2(0.5f, 0.5f), 0.24f).SetEase(Ease.OutCubic))
                .Join(visualFrameRect.DOAnchorPos(Vector2.zero, 0.24f).SetEase(Ease.OutCubic));
        }

        if (enableDebugLogs)
            Debug.Log("[StarterSelectionUI] Entered reusable starter naming presentation.");
    }

    public bool ShowStarterNameInput()
    {
        if (!isShowing || presentationMode != PresentationMode.Naming || !EnsureStarterNameInput())
            return false;

        SetPanelTitleVisible(false);
        PlaceStarterNameInputInTitleSlot();
        ConfigureStarterNameInput();
        starterNameInputField.SetTextWithoutNotify(string.Empty);
        starterNameInputField.gameObject.SetActive(true);
        starterNameInputField.readOnly = false;
        CanvasGroup inputGroup = starterNameInputField.GetComponent<CanvasGroup>();
        if (inputGroup == null)
            inputGroup = starterNameInputField.gameObject.AddComponent<CanvasGroup>();
        inputGroup.alpha = 1f;
        inputGroup.interactable = false;
        inputGroup.blocksRaycasts = false;

        PlayNameEntryLayout();
        return true;
    }

    /// <summary>
    /// Keeps the confirmed name on screen through the victory chime. The field
    /// closes later with the panel.
    /// </summary>
    public void RetainStarterNameInput()
    {
        if (starterNameInputField == null || !starterNameInputField.gameObject.activeSelf)
            return;

        starterNameInputField.readOnly = true;
        starterNameInputField.DeactivateInputField();
        if (starterNameInputField.placeholder != null)
            starterNameInputField.placeholder.enabled = string.IsNullOrEmpty(starterNameInputField.text);

        CanvasGroup inputGroup = starterNameInputField.GetComponent<CanvasGroup>();
        if (inputGroup != null)
        {
            inputGroup.alpha = 1f;
            inputGroup.interactable = false;
            inputGroup.blocksRaycasts = false;
        }
    }

    public void HideStarterNameInput()
    {
        if (starterNameInputField == null)
            return;

        starterNameInputField.readOnly = false;
        starterNameInputField.DeactivateInputField();
        starterNameInputField.gameObject.SetActive(false);
        starterNameInputField.SetTextWithoutNotify(string.Empty);
    }

    /// <summary>Mirrors the staged panel entrance: fade content, collapse Y, then collapse X/Y to the projection point.</summary>
    public IEnumerator DismissSelectionAnimated()
    {
        if (!isShowing)
            yield break;

        presentationMode = PresentationMode.Closing;
        panelInputArmed = false;
        StopSelectRoutine();
        if (portraitReveal)
            yield return ExitPortraitBanner();
        StopPanelPresentation(restorePortraitBanner: false);

        Transform panelTransform = ResolvePreviewContainer();
        if (panelTransform != null)
        {
            List<CanvasGroup> groups = CollectDismissContentGroups(panelTransform);
            Sequence fade = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < groups.Count; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null)
                    continue;
                group.interactable = false;
                group.blocksRaycasts = false;
                fade.Join(group.DOFade(0f, Mathf.Max(0.01f, panelContentFadeDuration)).SetEase(Ease.InQuad));
            }
            yield return fade.WaitForCompletion();

            panelTransform.DOKill();
            float lineHeight = Mathf.Clamp(panelProjectionLineHeight, 0.01f, 0.25f);
            Sequence projection = DOTween.Sequence().SetUpdate(true);
            projection.Append(panelTransform.DOScaleY(lineHeight, Mathf.Max(0.01f, panelExpandYDuration)).SetEase(Ease.InBack, Mathf.Max(0f, panelExpandYOvershoot)));
            projection.Append(panelTransform.DOScaleX(0.001f, Mathf.Max(0.01f, panelExpandXDuration)).SetEase(Ease.InCubic));
            projection.Join(panelTransform.DOScaleY(0.001f, Mathf.Max(0.01f, panelExpandXDuration)).SetEase(Ease.InCubic));
            yield return projection.WaitForCompletion();
        }

        HideSelection();
    }

    private void BindUiEvents()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
    }

    private void UnbindUiEvents()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(OnConfirmClicked);

        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }

    private void OnConfirmClicked()
    {
        if (!CanAcceptPanelInput())
            return;

        StarterSelectionService.Instance?.SelectStarter(currentOption);
    }

    private void OnBackClicked()
    {
        if (!CanAcceptPanelInput())
            return;

        StarterSelectionService.Instance?.CancelPreview(currentOption);
    }

    private void BindRole(SpeciesDefinitionSO species)
    {
        if (roleText == null)
            return;

        SpeciesCombatRole role = SpeciesCombatRoleResolver.Resolve(species);
        roleText.text = StarterSelectionCopy.FormatRole(role);
    }

    private void EnsureTypePresentation()
    {
        if (typeBackgroundImage == null && previewVisual != null && previewVisual.transform.parent != null)
            typeBackgroundImage = previewVisual.transform.parent.GetComponent<Image>();

        if (secondaryTypeBadge != null)
            secondaryTypeBadge.gameObject.SetActive(false);

        EnsurePedestal();
    }

    private void EnsurePedestal()
    {
        if (pedestalImage == null && previewVisual != null)
        {
            Transform existing = previewVisual.transform.Find("PedestalDither");
            if (existing != null)
                pedestalImage = existing.GetComponent<Image>();
            if (pedestalImage == null)
                pedestalImage = CreatePedestal(previewVisual.transform);
        }

        if (pedestalImage != null && pedestalImage.sprite == null && pedestalSprite != null)
            pedestalImage.sprite = pedestalSprite;
    }

    private void ApplyPortraitPanelPlate()
    {
        EnsurePanelScanlines();
        Transform container = ResolvePreviewContainer();
        if (container == null)
            return;

        Image plate = container.GetComponent<Image>();
        if (plate != null)
        {
            if (!portraitPlateCaptured)
            {
                authoredPortraitPlateColor = plate.color;
                portraitPlateCaptured = true;
            }

            plate.color = ResolvePortraitPlateColor(plate);
            plate.enabled = true;
        }
    }

    private Color ResolvePortraitPlateColor(Image plate)
    {
        ResolveDetailsPanel();
        if (detailsPanel != null)
        {
            Image detailsImage = detailsPanel.GetComponent<Image>();
            if (detailsImage != null && detailsImage.color.a > 0.05f)
                return detailsImage.color;
        }

        Color visible = plate != null ? plate.color : new Color(0.21568628f, 0.30588236f, 0.2901961f, 0.78431374f);
        visible.a = Mathf.Max(visible.a, 0.78f);
        return visible;
    }

    private void RestorePortraitPanelPlate()
    {
        if (!portraitPlateCaptured)
            return;

        Transform container = ResolvePreviewContainer();
        Image plate = container != null ? container.GetComponent<Image>() : null;
        if (plate != null)
            plate.color = authoredPortraitPlateColor;
    }

    private void EnsurePanelScanlines()
    {
        Transform container = ResolvePreviewContainer();
        if (container == null)
            return;

        CombatUiScanlineBackground scanlines = container.GetComponent<CombatUiScanlineBackground>();
        if (scanlines == null)
            scanlines = container.gameObject.AddComponent<CombatUiScanlineBackground>();

        scanlines.Configure(
            enabled: true,
            texture: panelScanlineTexture,
            color: new Color(1f, 1f, 1f, 0.647f),
            tiling: PanelScanlineTiling,
            scrollDirection: Vector2.down,
            uvSpeed: 0.05f);

        if (scanlines.ScanlineOverlay != null)
        {
            scanlines.ScanlineOverlay.gameObject.SetActive(true);
            CanvasGroup overlayGroup = scanlines.ScanlineOverlay.GetComponent<CanvasGroup>();
            if (overlayGroup != null)
                overlayGroup.alpha = 1f;
        }

        // DetailsPanel intentionally owns the darker text-contrast surface.
        // Never let the panel-content helper attach a scanline layer to it.
        ResolveDetailsPanel();
        if (detailsPanel != null)
        {
            CombatUiScanlineBackground detailsScanlines = detailsPanel.GetComponent<CombatUiScanlineBackground>();
            if (detailsScanlines != null && detailsScanlines != scanlines)
                detailsScanlines.Configure(enabled: false);
        }
    }

    private void ApplyTypeChrome(ChainlingType type)
    {
        if (typeBackgroundImage != null)
        {
            Sprite paletteSprite = typeChromePalette != null ? typeChromePalette.BackgroundSprite : null;
            if (paletteSprite != null)
                typeBackgroundImage.sprite = paletteSprite;

            typeBackgroundImage.type = typeBackgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            typeBackgroundImage.color = typeChromePalette != null
                ? typeChromePalette.ResolveBackgroundColor(type)
                : ResolveTypeDisplay(type).badgeColor;
            typeBackgroundImage.enabled = true;
        }

        if (pedestalImage == null)
            return;

        pedestalImage.color = typeChromePalette != null
            ? typeChromePalette.ResolvePedestalColor(type)
            : Color.white;
        pedestalImage.enabled = true;
        pedestalImage.transform.SetAsFirstSibling();
    }

    private void ApplyTypeIdentity(ChainlingType type)
    {
        ChainlingTypeDisplaySO.TypeDisplayEntry entry = ResolveTypeDisplay(type);
        string typeName = string.IsNullOrWhiteSpace(entry.displayName) ? type.ToString() : entry.displayName;

        if (typeLabel != null)
            typeLabel.text = StarterSelectionCopy.FormatType(typeName);

        if (typeIcon == null)
            return;

        Sprite icon = typeIconPalette != null ? typeIconPalette.GetIcon(type) : null;
        typeIcon.sprite = icon;
        typeIcon.enabled = icon != null;
        typeIcon.gameObject.SetActive(true);
    }

    private ChainlingTypeDisplaySO.TypeDisplayEntry ResolveTypeDisplay(ChainlingType type)
    {
        return typeDisplayData != null
            ? typeDisplayData.GetEntryOrFallback(type)
            : ChainlingTypeDisplaySO.CreateFallback(type);
    }

    private void SetVisible(bool visible)
    {
        if (selectionPanel != null)
        {
            if (visible)
                NormalizeSelectionPanelTransform();

            selectionPanel.SetActive(visible);
        }

        if (!visible)
        {
            panelInputArmed = false;
            StopPanelPresentation();
            StopSelectRoutine();
            ClearPanelSelection();
            ReleasePanelLocks();
        }
    }

    /// <summary>
    /// A zero-scale authored overlay is effectively invisible to both the
    /// GraphicRaycaster and EventSystem navigation. Keep the prefab resilient
    /// to that accidental state while still preserving intentional non-zero
    /// authored scaling.
    /// </summary>
    private void NormalizeSelectionPanelTransform()
    {
        if (selectionPanel == null)
            return;

        Transform panelTransform = selectionPanel.transform;
        if (panelTransform == null || panelTransform.localScale.sqrMagnitude > 0.0001f)
            return;

        panelTransform.localScale = Vector3.one;
        if (enableDebugLogs)
            Debug.LogWarning("[StarterSelectionUI] Normalized zero-scale selection overlay to Vector3.one.", selectionPanel);
    }

    private void AcquirePanelLocks()
    {
        if (!hasInputLock)
        {
            InputLockService service = InputLockService.Instance;
            if (service != null)
            {
                inputLockHandle = service.AcquireLock(PanelLockReason, InputLockChannel.All, this);
                hasInputLock = inputLockHandle.IsValid;
            }
        }

        if (!hudToken.IsValid || hudToken.IsReleased)
        {
            MobileHudService hud = MobileHudService.Instance;
            if (hud != null)
                hudToken = hud.AcquireContext(MobileHudContext.Menu, PanelLockReason);
        }

        SubscribeMobileHudInput();
    }

    private void ReleasePanelLocks()
    {
        if (hasInputLock)
        {
            inputLockHandle.Release();
            hasInputLock = false;
        }

        if (hudToken.IsValid && !hudToken.IsReleased)
            hudToken.Release();
        hudToken = default;

        UnsubscribeMobileHudInput();
    }

    private void SubscribeMobileHudInput()
    {
        if (!enableMobileHudNavigation)
            return;

        if (mobileInputManager == null)
        {
            mobileInputManager = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);
            if (mobileInputManager == null)
                return;
        }

        mobileInputManager.OnAPressed -= HandleMobileAPressed;
        mobileInputManager.OnAPressed += HandleMobileAPressed;
        mobileInputManager.OnBPressed -= HandleMobileBPressed;
        mobileInputManager.OnBPressed += HandleMobileBPressed;
    }

    private void UnsubscribeMobileHudInput()
    {
        if (mobileInputManager != null)
        {
            mobileInputManager.OnAPressed -= HandleMobileAPressed;
            mobileInputManager.OnBPressed -= HandleMobileBPressed;
        }

        lastMobileNavigation = Vector2.zero;
        nextMobileNavigationTime = 0f;
    }

    private void HandleMobileAPressed()
    {
        // The same A/Enter press that interacted with the world object can
        // also reach the UI module after this panel is activated. Require a
        // fresh confirmation edge so opening the preview never commits it.
        if (!CanAcceptPanelInput())
            return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        // The HUD's A button also writes <Gamepad>/buttonSouth, so when the UI
        // module still owns Submit it already delivers this press to the focused
        // button. Running the direct route as well would confirm twice.
        InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (IsInputModuleSubmitActive(inputModule))
            return;

        if (!MenuInputIntentGate.TryConsume(MenuInputIntent.Submit))
            return;

        // Read focus before touching it. ApplyDefaultSelection used to run
        // first, so a press made while focus was briefly off a panel button
        // snapped to Confirm and committed it, overriding a navigated Back.
        GameObject selected = eventSystem.currentSelectedGameObject;
        Button button = IsPanelButton(selected) ? selected.GetComponent<Button>() : null;
        if (button == null || !button.interactable)
        {
            ApplyDefaultSelection();
            return;
        }

        ExecuteEvents.Execute(
            button.gameObject,
            new BaseEventData(eventSystem),
            ExecuteEvents.submitHandler);
    }

    private void HandleMobileBPressed()
    {
        if (isShowing && presentationMode == PresentationMode.Browse)
            OnBackClicked();
    }

    private void ProcessMobileHudNavigation()
    {
        if (!isShowing || !panelInputArmed || !enableMobileHudNavigation || mobileInputManager == null)
            return;

        // The shared InputSystemUIInputModule already receives the pad's
        // OnScreenControl left-stick value. Only run the direct mobile route
        // when another menu controller has intentionally detached that move
        // action; this prevents one pad gesture from moving twice.
        EventSystem eventSystem = EventSystem.current;
        InputSystemUIInputModule inputModule = eventSystem != null
            ? eventSystem.GetComponent<InputSystemUIInputModule>()
            : null;
        if (IsInputModuleNavigationActive(inputModule))
            return;

        HybridMovementPad movementPad = mobileInputManager.MovementPad;
        Vector2 move = movementPad != null ? movementPad.GetInput() : Vector2.zero;
        float threshold = Mathf.Clamp01(mobileNavigationThreshold);
        if (move.sqrMagnitude < threshold * threshold)
        {
            lastMobileNavigation = Vector2.zero;
            return;
        }

        Vector2 direction = Mathf.Abs(move.x) >= Mathf.Abs(move.y)
            ? new Vector2(Mathf.Sign(move.x), 0f)
            : new Vector2(0f, Mathf.Sign(move.y));
        bool changedDirection = direction != lastMobileNavigation;
        if (!changedDirection && Time.unscaledTime < nextMobileNavigationTime)
            return;

        lastMobileNavigation = direction;
        nextMobileNavigationTime = Time.unscaledTime + Mathf.Max(
            0.01f,
            changedDirection ? mobileNavigationInitialDelay : mobileNavigationRepeatRate);

        if (eventSystem == null)
            return;

        ApplyDefaultSelection();
        Selectable selected = eventSystem.currentSelectedGameObject != null
            ? eventSystem.currentSelectedGameObject.GetComponent<Selectable>()
            : null;
        if (selected == null)
            return;

        Navigation navigation = selected.navigation;
        Selectable target = direction.x > 0f
            ? navigation.selectOnRight
            : direction.x < 0f
                ? navigation.selectOnLeft
                : direction.y > 0f
                    ? navigation.selectOnUp
                    : navigation.selectOnDown;

        if (target != null && target.IsInteractable() && target.gameObject.activeInHierarchy)
            eventSystem.SetSelectedGameObject(target.gameObject);
    }

    private static bool IsInputModuleNavigationActive(InputSystemUIInputModule inputModule)
    {
        if (inputModule == null || !inputModule.isActiveAndEnabled || inputModule.move == null)
            return false;

        // A bound action reference is not sufficient. The shared UI map can
        // exist while disabled, in which case the module will not receive
        // the movement input and the starter panel must use its direct mobile
        // navigation fallback instead.
        InputActionAsset actionsAsset = inputModule.actionsAsset;
        InputActionMap uiMap = actionsAsset != null
            ? actionsAsset.FindActionMap("UI", false)
            : null;

        return uiMap == null || uiMap.enabled;
    }

    private static bool IsInputModuleSubmitActive(InputSystemUIInputModule inputModule)
    {
        if (inputModule == null || !inputModule.isActiveAndEnabled || inputModule.submit == null)
            return false;

        InputActionAsset actionsAsset = inputModule.actionsAsset;
        InputActionMap uiMap = actionsAsset != null
            ? actionsAsset.FindActionMap("UI", false)
            : null;

        return uiMap == null || uiMap.enabled;
    }

    private void EnsureButtonNavigation()
    {
        if (confirmButton == null || backButton == null)
            return;

        // The panel opens on Confirm; Back is its right-hand neighbor. Navigation
        // must not wrap: when both buttons pointed at each other in every
        // direction, holding a direction toggled focus at the repeat rate, and
        // release landed on whichever button the last tick happened to pick — so
        // navigating to Back could still confirm Choose. Each direction now moves
        // one way only and is idempotent while held.
        confirmButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnRight = backButton,
            selectOnDown = backButton,
            selectOnLeft = null,
            selectOnUp = null,
        };
        backButton.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = confirmButton,
            selectOnUp = confirmButton,
            selectOnRight = null,
            selectOnDown = null,
        };
    }

    private void QueueDefaultSelection()
    {
        StopSelectRoutine();
        panelInputArmed = false;

        // Select Confirm before the first rendered frame. The deferred passes
        // below are still needed because the persistent EventSystem and UI
        // layout can process the newly activated panel after this method.
        ApplyDefaultSelection(forceConfirm: true);
        if (!isActiveAndEnabled)
        {
            panelInputArmed = true;
            return;
        }

        selectRoutine = StartCoroutine(SelectDefaultButtonNextFrame());
    }

    private IEnumerator SelectDefaultButtonNextFrame()
    {
        yield return null;

        // Player/Interact and UI/Submit intentionally share Enter and the
        // south face button. Wait for the opening press to be released before
        // allowing either the InputSystemUIInputModule or the direct mobile A
        // route to commit the selected button.
        while (IsSharedSubmitInputHeld())
            yield return null;

        panelInputArmed = true;
        ApplyDefaultSelection(forceConfirm: true);

        // Let Canvas/layout and the persistent EventSystem finish processing
        // the newly activated panel, then assert focus once more. This keeps
        // mobile HUD and PSG1 navigation on a starter button instead of
        // leaving focus on a hidden scene/menu selectable.
        yield return new WaitForEndOfFrame();
        ApplyDefaultSelection(forceConfirm: true);
        selectRoutine = null;
    }

    private bool CanAcceptPanelInput()
    {
        return isShowing && presentationMode == PresentationMode.Browse && panelInputArmed && currentOption != null;
    }

    private static bool IsSharedSubmitInputHeld()
    {
        InputActionAsset actionsAsset = null;
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            actionsAsset = inputModule != null ? inputModule.actionsAsset : null;
        }

        if (actionsAsset == null)
            actionsAsset = InputSystem.actions;

        InputActionMap uiMap = actionsAsset != null
            ? actionsAsset.FindActionMap("UI", false)
            : null;
        InputAction submit = uiMap != null ? uiMap.FindAction("Submit", false) : null;
        return submit != null && submit.IsPressed();
    }

    private void KeepDefaultSelection()
    {
        if (!isShowing || !panelInputArmed)
            return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (IsPanelButton(selected))
            return;

        ApplyDefaultSelection();
    }

    private void ApplyDefaultSelection(bool forceConfirm = false)
    {
        if (!isShowing || confirmButton == null || !confirmButton.gameObject.activeInHierarchy)
            return;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        Canvas.ForceUpdateCanvases();
        eventSystem.sendNavigationEvents = true;
        if (eventSystem.firstSelectedGameObject != confirmButton.gameObject)
            eventSystem.firstSelectedGameObject = confirmButton.gameObject;

        GameObject selected = eventSystem.currentSelectedGameObject;
        if (!forceConfirm && IsPanelButton(selected))
            return;

        eventSystem.SetSelectedGameObject(null);
        eventSystem.SetSelectedGameObject(confirmButton.gameObject);
        confirmButton.Select();
    }

    private void ClearPanelSelection()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return;

        if (IsPanelButton(eventSystem.currentSelectedGameObject))
            eventSystem.SetSelectedGameObject(null);

        if (eventSystem.firstSelectedGameObject != null && IsPanelButton(eventSystem.firstSelectedGameObject))
            eventSystem.firstSelectedGameObject = null;
    }

    private bool IsPanelButton(GameObject candidate)
    {
        if (candidate == null)
            return false;

        return (confirmButton != null && candidate == confirmButton.gameObject)
            || (backButton != null && candidate == backButton.gameObject);
    }

    private void StopSelectRoutine()
    {
        if (selectRoutine == null)
            return;

        StopCoroutine(selectRoutine);
        selectRoutine = null;
    }

    private void BeginPanelPresentation()
    {
        StopSelectRoutine();
        panelInputArmed = false;
        StopPanelPresentation();
        PrepareDimOverlayReveal();
        PreparePanelContentReveal();
        if (portraitReveal && speciesNameText != null)
            speciesNameText.maxVisibleCharacters = 0;

        if (selectionPanel == null || !isShowing)
            return;

        panelOpeningRoutine = StartCoroutine(PlayPanelOpeningRoutine());
    }

    private IEnumerator PlayPanelOpeningRoutine()
    {
        // The projection belongs to the authored shell only. Scaling the
        // overlay root also scales the dimmer, safe-area layout, and every
        // content element, which hides the intended staged reveal.
        Transform panelTransform = ResolvePreviewContainer();
        if (panelTransform == null)
        {
            RevealDimOverlayImmediately();
            RevealPanelContentImmediately();
            StartPreviewVisualPlayback();
            QueueDefaultSelection();
            panelOpeningRoutine = null;
            yield break;
        }

        panelTransform.DOKill();
        panelTransform.localScale = new Vector3(0.001f, 0.001f, 1f);

        Sequence projection = DOTween.Sequence().SetUpdate(true);
        float xPhaseDuration = Mathf.Max(0.01f, panelExpandXDuration);
        projection.Append(panelTransform.DOScaleX(1f, xPhaseDuration).SetEase(Ease.OutCubic));
        projection.Join(panelTransform.DOScaleY(Mathf.Clamp(panelProjectionLineHeight, 0.01f, 0.25f), xPhaseDuration).SetEase(Ease.OutCubic));
        projection.Append(panelTransform.DOScaleY(1f, Mathf.Max(0.01f, panelExpandYDuration)).SetEase(Ease.OutBack, Mathf.Max(0f, panelExpandYOvershoot)));
        yield return projection.WaitForCompletion();

        if (!isShowing)
            yield break;

        yield return RevealDimOverlay();

        if (!isShowing)
            yield break;

        if (panelContentDelay > 0f)
            yield return new WaitForSecondsRealtime(panelContentDelay);

        Sequence contentReveal = DOTween.Sequence().SetUpdate(true);
        panelContentRevealTween = contentReveal;
        bool hasContentTween = false;
        for (int i = 0; i < panelContentGroups.Count; i++)
        {
            CanvasGroup group = panelContentGroups[i];
            if (group == null)
                continue;

            group.interactable = false;
            group.blocksRaycasts = false;
            Tween fadeTween = group.DOFade(1f, Mathf.Max(0f, panelContentFadeDuration)).SetEase(Ease.OutQuad);
            if (hasContentTween)
                contentReveal.Join(fadeTween);
            else
            {
                contentReveal.Append(fadeTween);
                hasContentTween = true;
            }
        }

        if (hasContentTween && panelContentFadeDuration > 0f)
            yield return contentReveal.WaitForCompletion();

        panelContentRevealTween = null;

        for (int i = 0; i < panelContentGroups.Count; i++)
        {
            CanvasGroup group = panelContentGroups[i];
            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        StartDigitalChrome();
        if (portraitReveal)
            PlayPortraitLayout();

        if (!portraitReveal)
            yield return RevealTitle();

        if (!isShowing)
            yield break;

        StartPreviewVisualPlayback();
        if (portraitReveal)
            yield return StartCoroutine(PlayPortraitBanner());
        else
        {
            StartTypewriter();
            QueueDefaultSelection();
        }
        panelOpeningRoutine = null;
    }

    private void PreparePanelContentReveal()
    {
        panelContentGroups.Clear();

        Transform container = ResolvePreviewContainer();
        if (container == null)
            return;

        Transform scanlineTransform = null;
        CombatUiScanlineBackground scanlines = container.GetComponent<CombatUiScanlineBackground>();
        if (scanlines != null && scanlines.ScanlineOverlay != null)
            scanlineTransform = scanlines.ScanlineOverlay.transform;

        Transform titleTransform = titleText != null ? titleText.transform : null;
        if (titleTransform == null)
        {
            Transform[] transforms = container.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null &&
                    string.Equals(transforms[i].name, "TitleText", System.StringComparison.Ordinal))
                {
                    titleTransform = transforms[i];
                    break;
                }
            }
        }

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == scanlineTransform ||
                child == titleTransform ||
                ShouldSkipPanelContentChild(child.name))
                continue;

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group == null)
                group = child.gameObject.AddComponent<CanvasGroup>();

            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            panelContentGroups.Add(group);
        }

        PrepareTitleReveal();
        // Prime the text state before any content CanvasGroup is allowed to
        // become opaque. Otherwise the labels can be fully rendered during
        // the title fade, before the typewriter coroutine gets its first tick.
        StopTypewriter(revealText: false);
    }

    private void PrepareDimOverlayReveal()
    {
        CanvasGroup dimOverlay = ResolveDimOverlayCanvasGroup();
        if (dimOverlay == null)
            return;

        dimOverlayRevealTween?.Kill();
        dimOverlayRevealTween = null;
        dimOverlay.alpha = 0f;
        dimOverlay.interactable = false;
        dimOverlay.blocksRaycasts = true;
    }

    private IEnumerator RevealDimOverlay()
    {
        CanvasGroup dimOverlay = ResolveDimOverlayCanvasGroup();
        if (dimOverlay == null)
            yield break;

        float duration = Mathf.Max(0f, dimOverlayFadeDuration);
        if (duration <= 0f)
        {
            dimOverlay.alpha = 1f;
            dimOverlay.interactable = false;
            dimOverlay.blocksRaycasts = true;
            yield break;
        }

        dimOverlayRevealTween = dimOverlay
            .DOFade(1f, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
        yield return dimOverlayRevealTween.WaitForCompletion();
        dimOverlayRevealTween = null;

        if (dimOverlay != null)
        {
            dimOverlay.alpha = 1f;
            dimOverlay.interactable = false;
            dimOverlay.blocksRaycasts = true;
        }
    }

    private void RevealDimOverlayImmediately()
    {
        CanvasGroup dimOverlay = ResolveDimOverlayCanvasGroup();
        if (dimOverlay == null)
            return;

        dimOverlayRevealTween?.Kill();
        dimOverlayRevealTween = null;
        dimOverlay.alpha = 1f;
        dimOverlay.interactable = false;
        dimOverlay.blocksRaycasts = true;
    }

    private IEnumerator RevealTitle()
    {
        CanvasGroup titleGroup = ResolveTitleCanvasGroup();
        if (titleGroup == null)
            yield break;

        float duration = Mathf.Max(0f, panelContentFadeDuration);
        if (duration <= 0f)
        {
            RevealTitleImmediately();
            yield break;
        }

        titleRevealTween?.Kill();
        titleRevealTween = titleGroup
            .DOFade(1f, duration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
        yield return titleRevealTween.WaitForCompletion();
        titleRevealTween = null;

        if (titleGroup != null)
        {
            titleGroup.alpha = 1f;
            titleGroup.interactable = false;
            titleGroup.blocksRaycasts = false;
        }
    }

    private void PrepareTitleReveal()
    {
        CanvasGroup titleGroup = ResolveTitleCanvasGroup();
        if (titleGroup == null)
            return;

        titleRevealTween?.Kill();
        titleRevealTween = null;
        titleGroup.alpha = 0f;
        titleGroup.interactable = false;
        titleGroup.blocksRaycasts = false;

        if (titleText != null)
            titleText.maxVisibleCharacters = int.MaxValue;
    }

    private void RevealTitleImmediately()
    {
        CanvasGroup titleGroup = ResolveTitleCanvasGroup();
        if (titleGroup == null)
            return;

        titleRevealTween?.Kill();
        titleRevealTween = null;
        titleGroup.alpha = 1f;
        titleGroup.interactable = false;
        titleGroup.blocksRaycasts = false;

        if (titleText != null)
            titleText.maxVisibleCharacters = int.MaxValue;
    }

    private void RevealPanelContentImmediately()
    {
        for (int i = 0; i < panelContentGroups.Count; i++)
        {
            CanvasGroup group = panelContentGroups[i];
            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        RevealTitleImmediately();
        StartTypewriter();
        StartDigitalChrome();
    }

    private void StartPreviewVisualPlayback()
    {
        if (!isShowing)
            return;

        if (portraitReveal)
        {
            if (previewVisual != null)
                previewVisual.Play(portraitProfile, portraitFallback);
            return;
        }

        if (currentOption == null)
            return;

        previewVisual?.Play(currentOption.VisualProfile, currentOption.Icon);
    }

    private void StopPanelPresentation(bool restorePortraitBanner = true)
    {
        if (panelOpeningRoutine != null)
        {
            StopCoroutine(panelOpeningRoutine);
            panelOpeningRoutine = null;
        }

        StopTypewriter();
        StopPortraitBanner(restorePortraitBanner);

        panelContentRevealTween?.Kill();
        panelContentRevealTween = null;
        dimOverlayRevealTween?.Kill();
        dimOverlayRevealTween = null;
        titleRevealTween?.Kill();
        titleRevealTween = null;
        namingPresentationTween?.Kill();
        namingPresentationTween = null;
        nameEntryLayoutTween?.Kill();
        nameEntryLayoutTween = null;
        StopDigitalChrome();

        Transform previewContainer = ResolvePreviewContainer();
        if (previewContainer != null)
        {
            previewContainer.DOKill();
            if (!isShowing)
                previewContainer.localScale = Vector3.one;
        }

        if (!isShowing)
        {
            CanvasGroup dimOverlay = ResolveDimOverlayCanvasGroup();
            if (dimOverlay != null)
            {
                dimOverlay.alpha = 1f;
                dimOverlay.interactable = false;
                dimOverlay.blocksRaycasts = true;
            }

            RevealTitleImmediately();
        }

        for (int i = 0; i < panelContentGroups.Count; i++)
        {
            CanvasGroup group = panelContentGroups[i];
            if (group == null)
                continue;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
    }

    private void StartTypewriter()
    {
        // Do this synchronously. StopTypewriter used to reveal all text before
        // the coroutine hid it on its first tick, producing a visible full-text
        // frame at the start of every panel opening.
        StopTypewriter(revealText: false);
        if (selectionPanel == null || !selectionPanel.activeInHierarchy)
            return;

        typewriterRoutine = StartCoroutine(TypewriterRoutine());
    }

    private IEnumerator TypewriterRoutine()
    {
        TextMeshProUGUI[] texts = selectionPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        var activeTexts = new List<TextMeshProUGUI>();
        var characterCounts = new List<int>();
        int longestText = 0;

        for (int i = 0; i < texts.Length; i++)
        {
            TextMeshProUGUI text = texts[i];
            if (text == null || !text.gameObject.activeInHierarchy)
                continue;

            if (IsPanelTitle(text))
            {
                text.maxVisibleCharacters = int.MaxValue;
                continue;
            }

            text.maxVisibleCharacters = int.MaxValue;
            text.ForceMeshUpdate();
            int characterCount = text.textInfo.characterCount;
            if (characterCount <= 0)
                continue;

            text.maxVisibleCharacters = 0;
            activeTexts.Add(text);
            characterCounts.Add(characterCount);
            longestText = Mathf.Max(longestText, characterCount);
        }

        float charactersPerSecond = Mathf.Max(1f, typewriterCharactersPerSecond);
        float elapsed = 0f;
        float duration = longestText / charactersPerSecond;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            int revealedCharacters = Mathf.CeilToInt(elapsed * charactersPerSecond);
            for (int i = 0; i < activeTexts.Count; i++)
                activeTexts[i].maxVisibleCharacters = Mathf.Min(characterCounts[i], revealedCharacters);

            yield return null;
        }

        for (int i = 0; i < activeTexts.Count; i++)
            activeTexts[i].maxVisibleCharacters = int.MaxValue;

        typewriterRoutine = null;
    }

    private void StopTypewriter(bool revealText = true)
    {
        if (typewriterRoutine != null)
        {
            StopCoroutine(typewriterRoutine);
            typewriterRoutine = null;
        }

        if (selectionPanel == null)
            return;

        TextMeshProUGUI[] texts = selectionPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] != null)
                texts[i].maxVisibleCharacters = revealText || IsPanelTitle(texts[i])
                    ? int.MaxValue
                    : 0;
        }
    }

    private bool IsPanelTitle(TextMeshProUGUI text)
    {
        return text != null &&
            (text == titleText ||
             string.Equals(text.gameObject.name, "TitleText", System.StringComparison.Ordinal));
    }

    private static bool ShouldSkipPanelContentChild(string name)
    {
        return string.Equals(name, "ScanlineOverlay", System.StringComparison.Ordinal) ||
               string.Equals(name, "TitleText", System.StringComparison.Ordinal) ||
               string.Equals(name, "StarterNameInput", System.StringComparison.Ordinal) ||
               string.Equals(name, "ContainerFrame", System.StringComparison.Ordinal);
    }

    private Transform ResolvePreviewContainer()
    {
        if (selectionPanel == null)
            return null;

        Transform[] transforms = selectionPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && string.Equals(transforms[i].name, "PreviewContainer", System.StringComparison.Ordinal))
                return transforms[i];
        }

        Transform candidate = previewVisual != null ? previewVisual.transform : null;
        while (candidate != null)
        {
            if (string.Equals(candidate.name, "PreviewContainer", System.StringComparison.Ordinal))
                return candidate;

            candidate = candidate.parent;
        }

        return speciesNameText != null ? speciesNameText.transform.parent : null;
    }

    private CanvasGroup ResolveDimOverlayCanvasGroup()
    {
        if (selectionPanel == null)
            return null;

        Transform[] transforms = selectionPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || !string.Equals(candidate.name, "DimBackground", System.StringComparison.Ordinal))
                continue;

            CanvasGroup group = candidate.GetComponent<CanvasGroup>();
            if (group == null)
                group = candidate.gameObject.AddComponent<CanvasGroup>();

            return group;
        }

        return null;
    }

    private CanvasGroup ResolveTitleCanvasGroup()
    {
        if (titleText != null)
        {
            CanvasGroup titleGroup = titleText.GetComponent<CanvasGroup>();
            if (titleGroup == null)
                titleGroup = titleText.gameObject.AddComponent<CanvasGroup>();

            return titleGroup;
        }

        if (selectionPanel == null)
            return null;

        Transform[] transforms = selectionPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null ||
                !string.Equals(candidate.name, "TitleText", System.StringComparison.Ordinal))
                continue;

            CanvasGroup titleGroup = candidate.GetComponent<CanvasGroup>();
            if (titleGroup == null)
                titleGroup = candidate.gameObject.AddComponent<CanvasGroup>();

            return titleGroup;
        }

        return null;
    }

    private void ResolveDetailsPanel()
    {
        if (detailsPanel != null || selectionPanel == null)
            return;

        Transform[] transforms = selectionPanel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && string.Equals(transforms[i].name, "DetailsPanel", System.StringComparison.Ordinal))
            {
                detailsPanel = transforms[i].gameObject;
                return;
            }
        }
    }

    private void PreparePortraitChrome(string speciesName)
    {
        ResolveDetailsPanel();
        CacheVisualFramePose();
        if (visualFrameRect != null)
        {
            visualFrameRect.DOKill();
            visualFrameRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualFrameRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualFrameRect.pivot = new Vector2(0.5f, 0.5f);
            float nameBlock = 156f;
            visualFrameRect.anchoredPosition = new Vector2(0f, -nameBlock * 0.5f);
        }

        PlaceSpeciesNameAbovePortrait(speciesName);
        ApplyPortraitPanelPlate();
        SetPanelTitleVisible(false);
        if (detailsPanel != null)
            detailsPanel.SetActive(false);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(false);
        if (backButton != null)
            backButton.gameObject.SetActive(false);
        HideStarterNameInput();
        if (!Application.isPlaying)
            PlayPortraitLayout();
    }

    private void PlaceSpeciesNameAbovePortrait(string speciesName)
    {
        if (speciesNameText == null)
            return;

        RectTransform nameRect = speciesNameText.rectTransform;
        if (!speciesNamePoseCaptured)
        {
            speciesNameParent = nameRect.parent;
            speciesNameSiblingIndex = nameRect.GetSiblingIndex();
            speciesNameAnchorMin = nameRect.anchorMin;
            speciesNameAnchorMax = nameRect.anchorMax;
            speciesNameAnchoredPosition = nameRect.anchoredPosition;
            speciesNameSizeDelta = nameRect.sizeDelta;
            speciesNamePivot = nameRect.pivot;
            speciesNameLocalScale = nameRect.localScale;
            speciesNameAlignment = speciesNameText.alignment;
            speciesNameCharacterSpacing = speciesNameText.characterSpacing;
            speciesNameSpacingCaptured = true;
            speciesNamePoseCaptured = true;
        }

        Transform host = visualFrameRect != null ? visualFrameRect.parent : ResolvePreviewContainer();
        if (host != null)
            nameRect.SetParent(host, false);

        nameRect.anchorMin = new Vector2(0.5f, 0.5f);
        nameRect.anchorMax = new Vector2(0.5f, 0.5f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        float top = 16f;
        float width = 640f;
        if (visualFrameRect != null)
        {
            top = visualFrameRect.anchoredPosition.y + (visualFrameRect.sizeDelta.y * 0.5f) + 16f;
            width = Mathf.Max(640f, visualFrameRect.sizeDelta.x);
        }

        nameRect.anchoredPosition = new Vector2(visualFrameRect != null ? visualFrameRect.anchoredPosition.x : 0f, top);
        nameRect.sizeDelta = new Vector2(width, 140f);
        speciesNameText.alignment = TextAlignmentOptions.Center;
        speciesNameText.characterSpacing = 8f;
        speciesNameText.text = StarterSelectionCopy.FormatPortraitBanner(portraitBannerFormat, speciesName);
        speciesNameText.maxVisibleCharacters = 0;
        speciesNameText.gameObject.SetActive(true);
    }

    private void RestoreSpeciesNamePose()
    {
        if (!speciesNamePoseCaptured || speciesNameText == null)
            return;

        RectTransform nameRect = speciesNameText.rectTransform;
        if (speciesNameParent != null)
        {
            nameRect.SetParent(speciesNameParent, false);
            nameRect.SetSiblingIndex(Mathf.Clamp(speciesNameSiblingIndex, 0, speciesNameParent.childCount - 1));
        }

        nameRect.DOKill();
        nameRect.anchorMin = speciesNameAnchorMin;
        nameRect.anchorMax = speciesNameAnchorMax;
        nameRect.pivot = speciesNamePivot;
        nameRect.anchoredPosition = speciesNameAnchoredPosition;
        nameRect.sizeDelta = speciesNameSizeDelta;
        nameRect.localScale = speciesNameLocalScale == Vector3.zero ? Vector3.one : speciesNameLocalScale;
        speciesNameText.alignment = speciesNameAlignment;
        if (speciesNameSpacingCaptured)
            speciesNameText.characterSpacing = speciesNameCharacterSpacing;
        StopPortraitBanner(restoreMesh: true);
    }

    private IEnumerator PlayPortraitBanner()
    {
        if (speciesNameText == null)
            yield break;

        portraitBanner = speciesNameText.GetComponent<TmpCharacterFlourish>();
        if (portraitBanner == null)
            portraitBanner = speciesNameText.gameObject.AddComponent<TmpCharacterFlourish>();

        portraitBanner.PrepareHidden();
        portraitBannerRoutine = StartCoroutine(portraitBanner.PlayReveal());
        yield return portraitBannerRoutine;
        portraitBannerRoutine = null;
    }

    private IEnumerator ExitPortraitBanner()
    {
        if (portraitBannerRoutine != null)
        {
            StopCoroutine(portraitBannerRoutine);
            portraitBannerRoutine = null;
        }

        if (portraitBanner == null && speciesNameText != null)
            portraitBanner = speciesNameText.GetComponent<TmpCharacterFlourish>();
        if (portraitBanner == null)
            yield break;

        yield return portraitBanner.PlayExit();
    }

    private void StopPortraitBanner(bool restoreMesh)
    {
        if (portraitBannerRoutine != null)
        {
            StopCoroutine(portraitBannerRoutine);
            portraitBannerRoutine = null;
        }

        portraitBanner?.StopMotion(restoreMesh);
    }

    private void BeginPortraitChime()
    {
        ReleasePortraitDuck();
        AudioHandle chime = default;
        if (victoryChime != null && AudioService.Instance != null)
            chime = AudioService.Instance.Play(victoryChime);

        MusicConductor conductor = MusicConductor.Instance;
        if (conductor != null)
        {
            activePortraitDuck = conductor.AcquireDuckToken(
                "Capture portrait",
                portraitDuckMultiplier,
                portraitDuckAttackSeconds,
                0f,
                portraitDuckReleaseSeconds);
        }

        portraitDuckRoutine = StartCoroutine(ReleasePortraitDuckWhenChimeEnds(chime));
    }

    private IEnumerator ReleasePortraitDuckWhenChimeEnds(AudioHandle chime)
    {
        AudioService audio = AudioService.Instance;
        float elapsed = 0f;
        float silent = 0f;
        const float safetySeconds = 30f;
        while (chime.IsValid && elapsed < safetySeconds)
        {
            bool playing = audio != null && audio.IsPlaying(chime);
            silent = playing ? 0f : silent + Time.unscaledDeltaTime;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed > 0.05f && silent > 0.05f)
                break;
            yield return null;
        }

        portraitDuckRoutine = null;
        ReleasePortraitDuck();
    }

    private void ReleasePortraitDuck()
    {
        if (portraitDuckRoutine != null)
        {
            StopCoroutine(portraitDuckRoutine);
            portraitDuckRoutine = null;
        }

        if (activePortraitDuck.IsValid)
            activePortraitDuck.Release();
        activePortraitDuck = default;
    }

    private void PlayPortraitLayout()
    {
        if (nameEntryLayoutApplied)
            return;

        RectTransform preview = ResolvePreviewContainer() as RectTransform;
        CachePreviewInsets(preview);
        nameEntryLayoutApplied = true;

        if (!Application.isPlaying)
        {
            if (preview != null)
                SetHorizontalInsets(preview, NameEntryPreviewInset);
            return;
        }

        RectTransform banner = speciesNameText != null ? speciesNameText.rectTransform : null;
        Vector2 bannerRest = banner != null ? banner.anchoredPosition : Vector2.zero;
        Image frameImage = visualFrameRect != null ? visualFrameRect.GetComponent<Image>() : null;
        if (frameImage != null && !visualFrameColorCaptured)
        {
            visualFrameRestColor = frameImage.color;
            visualFrameColorCaptured = true;
        }

        nameEntryLayoutTween?.Kill();
        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        if (preview != null)
        {
            nameEntryInsetTween = preview.offsetMin.x;
            sequence.Insert(0f, DOTween.To(
                    () => nameEntryInsetTween,
                    value =>
                    {
                        nameEntryInsetTween = value;
                        SetHorizontalInsets(preview, value);
                    },
                    NameEntryPreviewInset,
                    NameEntryLayoutDuration)
                .SetEase(Ease.OutExpo)
                .SetUpdate(true));
        }

        if (visualFrameRect != null)
        {
            float restX = visualFrameRect.anchoredPosition.x;
            visualFrameRect.localScale = Vector3.one * 1.06f;
            sequence.Insert(0f, visualFrameRect.DOScale(Vector3.one, 0.28f).SetEase(Ease.OutExpo).SetUpdate(true));

            Sequence glitch = DOTween.Sequence().SetUpdate(true);
            glitch.Append(visualFrameRect.DOAnchorPosX(restX + 3f, 0.045f).SetEase(Ease.OutQuad));
            glitch.Append(visualFrameRect.DOAnchorPosX(restX - 2f, 0.04f).SetEase(Ease.OutQuad));
            glitch.Append(visualFrameRect.DOAnchorPosX(restX, 0.05f).SetEase(Ease.InQuad));
            sequence.Insert(0.18f, glitch);

            if (frameImage != null)
            {
                Color pulse = Color.Lerp(visualFrameRestColor, DigitalAccent, 0.35f);
                pulse.a = visualFrameRestColor.a;
                sequence.Insert(0.18f, frameImage.DOColor(pulse, 0.05f).SetEase(Ease.OutQuad).SetUpdate(true));
                sequence.Insert(0.23f, frameImage.DOColor(visualFrameRestColor, 0.1f).SetEase(Ease.InQuad).SetUpdate(true));
            }
        }

        if (banner != null)
        {
            banner.localScale = Vector3.one * 1.05f;
            banner.anchoredPosition = bannerRest + new Vector2(0f, 16f);
            sequence.Insert(0.04f, banner.DOAnchorPos(bannerRest, 0.3f).SetEase(Ease.OutExpo).SetUpdate(true));
            sequence.Insert(0.04f, banner.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutExpo).SetUpdate(true));
        }

        nameEntryLayoutTween = sequence;
    }

    private void PlayNameEntryLayout()
    {
        if (nameEntryLayoutApplied)
            return;

        RectTransform preview = ResolvePreviewContainer() as RectTransform;
        CachePreviewInsets(preview);
        CacheVisualFramePose();
        CacheNameInputPose();

        if (Application.isPlaying && namingPresentationTween != null && namingPresentationTween.IsActive())
        {
            namingPresentationTween.Complete(true);
            namingPresentationTween = null;
        }

        RectTransform frame = visualFrameRect;
        RectTransform nameRect = starterNameInputField != null
            ? starterNameInputField.transform as RectTransform
            : null;
        float frameTargetY = frame != null ? frame.anchoredPosition.y - NameEntryFrameDrop : 0f;
        nameEntryLayoutApplied = true;

        if (!Application.isPlaying)
        {
            if (preview != null)
                SetHorizontalInsets(preview, NameEntryPreviewInset);
            if (frame != null)
                frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, frameTargetY);
            if (nameRect != null)
                nameRect.anchoredPosition = new Vector2(nameRect.anchoredPosition.x, NameEntryInputPosY);
            return;
        }

        nameEntryLayoutTween?.Kill();
        namingPresentationTween?.Kill();
        namingPresentationTween = null;

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        if (preview != null)
        {
            preview.DOKill();
            nameEntryInsetTween = preview.offsetMin.x;
            sequence.Insert(0f, DOTween.To(
                    () => nameEntryInsetTween,
                    value =>
                    {
                        nameEntryInsetTween = value;
                        SetHorizontalInsets(preview, value);
                    },
                    NameEntryPreviewInset,
                    NameEntryLayoutDuration)
                .SetEase(Ease.OutExpo)
                .SetUpdate(true));
        }

        if (frame != null)
        {
            frame.DOKill();
            sequence.Insert(0.04f, frame
                .DOAnchorPosY(frameTargetY, NameEntryLayoutDuration)
                .SetEase(Ease.OutExpo)
                .SetUpdate(true));
        }

        if (nameRect != null)
        {
            nameRect.DOKill();
            sequence.Insert(0.08f, nameRect
                .DOAnchorPosY(NameEntryInputPosY, 0.28f)
                .SetEase(Ease.OutExpo)
                .SetUpdate(true));

            CanvasGroup group = starterNameInputField.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 0f;
                sequence.Insert(0.06f, group.DOFade(1f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true));
            }
        }

        nameEntryLayoutTween = sequence;
    }

    private void CachePreviewInsets(RectTransform preview)
    {
        if (preview == null || previewInsetCaptured)
            return;

        previewOffsetMin = preview.offsetMin;
        previewOffsetMax = preview.offsetMax;
        previewInsetCaptured = true;
    }

    private void CacheNameInputPose()
    {
        if (nameInputPoseCaptured || starterNameInputField == null)
            return;

        RectTransform nameRect = starterNameInputField.transform as RectTransform;
        if (nameRect == null)
            return;

        nameInputAnchoredPosition = nameRect.anchoredPosition;
        nameInputPoseCaptured = true;
    }

    private void RestoreNameEntryLayout()
    {
        nameEntryLayoutTween?.Kill();
        nameEntryLayoutTween = null;
        nameEntryLayoutApplied = false;

        RectTransform preview = ResolvePreviewContainer() as RectTransform;
        if (preview != null)
            preview.DOKill();
        if (previewInsetCaptured && preview != null)
        {
            preview.offsetMin = previewOffsetMin;
            preview.offsetMax = previewOffsetMax;
        }

        previewInsetCaptured = false;

        if (visualFrameRect != null)
        {
            visualFrameRect.DOKill();
            Image frameImage = visualFrameRect.GetComponent<Image>();
            if (frameImage != null)
                frameImage.DOKill();
        }

        if (starterNameInputField != null)
        {
            RectTransform nameRect = starterNameInputField.transform as RectTransform;
            if (nameRect != null)
            {
                nameRect.DOKill();
                if (nameInputPoseCaptured)
                    nameRect.anchoredPosition = nameInputAnchoredPosition;
            }
        }

        nameInputPoseCaptured = false;
    }

    private static void SetHorizontalInsets(RectTransform rect, float inset)
    {
        if (rect == null)
            return;

        Vector2 min = rect.offsetMin;
        Vector2 max = rect.offsetMax;
        min.x = inset;
        max.x = -inset;
        rect.offsetMin = min;
        rect.offsetMax = max;
    }

    private void StartDigitalChrome()
    {
        if (!Application.isPlaying || !isShowing || portraitReveal || presentationMode != PresentationMode.Browse)
            return;

        CaptureDigitalChromeRest();
        RestoreDigitalChromeRest();
        KillDigitalChromeTweens();
        int token = ++digitalChromeToken;

        confirmDigitalTween = StartButtonDigital(confirmButton, confirmDigitalRestColor, confirmDigitalRestValid, 0f, token);
        backDigitalTween = StartButtonDigital(backButton, backDigitalRestColor, backDigitalRestValid, 0.08f, token);
        typeIconDigitalTween = StartTypeIconDigital(token);
    }

    private void StopDigitalChrome()
    {
        digitalChromeToken++;
        KillDigitalChromeTweens();
        RestoreDigitalChromeRest();
    }

    private void CaptureDigitalChromeRest()
    {
        if (digitalChromeRestCaptured)
            return;

        if (TryGetButtonImage(confirmButton, out Image confirmImage))
        {
            confirmDigitalRestColor = confirmImage.color;
            confirmDigitalRestValid = true;
        }

        if (TryGetButtonImage(backButton, out Image backImage))
        {
            backDigitalRestColor = backImage.color;
            backDigitalRestValid = true;
        }

        if (typeIcon != null)
        {
            RectTransform iconRect = typeIcon.rectTransform;
            typeIconRestPosition = iconRect.anchoredPosition;
            typeIconRestScale = iconRect.localScale;
            typeIconRestColor = typeIcon.color;
            typeIconRestValid = true;
        }

        confirmLabelRect = FindButtonLabel(confirmButton);
        backLabelRect = FindButtonLabel(backButton);
        if (confirmLabelRect != null)
            confirmLabelRestPosition = confirmLabelRect.anchoredPosition;
        if (backLabelRect != null)
            backLabelRestPosition = backLabelRect.anchoredPosition;

        digitalChromeRestCaptured = true;
    }

    private void RestoreDigitalChromeRest()
    {
        if (!digitalChromeRestCaptured)
            return;

        if (confirmDigitalRestValid && TryGetButtonImage(confirmButton, out Image confirmImage))
            confirmImage.color = confirmDigitalRestColor;
        if (backDigitalRestValid && TryGetButtonImage(backButton, out Image backImage))
            backImage.color = backDigitalRestColor;
        if (confirmLabelRect != null)
            confirmLabelRect.anchoredPosition = confirmLabelRestPosition;
        if (backLabelRect != null)
            backLabelRect.anchoredPosition = backLabelRestPosition;

        if (!typeIconRestValid || typeIcon == null)
            return;

        RectTransform iconRect = typeIcon.rectTransform;
        iconRect.anchoredPosition = typeIconRestPosition;
        iconRect.localScale = typeIconRestScale;
        typeIcon.color = typeIconRestColor;
    }

    private void KillDigitalChromeTweens()
    {
        confirmDigitalTween?.Kill();
        confirmDigitalTween = null;
        backDigitalTween?.Kill();
        backDigitalTween = null;
        typeIconDigitalTween?.Kill();
        typeIconDigitalTween = null;

        if (TryGetButtonImage(confirmButton, out Image confirmImage))
            confirmImage.DOKill();
        if (TryGetButtonImage(backButton, out Image backImage))
            backImage.DOKill();
        if (typeIcon != null)
        {
            typeIcon.DOKill();
            typeIcon.rectTransform.DOKill();
        }

        if (confirmLabelRect != null)
            confirmLabelRect.DOKill();
        if (backLabelRect != null)
            backLabelRect.DOKill();
    }

    private Sequence StartButtonDigital(Button button, Color rest, bool hasRest, float delay, int token)
    {
        if (!hasRest || !TryGetButtonImage(button, out Image image))
            return null;

        Color pulse = Color.Lerp(rest, DigitalAccent, 0.28f);
        pulse.a = rest.a;
        image.color = Color.Lerp(rest, pulse, 0.7f);
        RectTransform label = button == confirmButton ? confirmLabelRect : backLabelRect;
        Vector2 labelRest = button == confirmButton ? confirmLabelRestPosition : backLabelRestPosition;
        if (label != null)
            label.anchoredPosition = labelRest + new Vector2(0f, 8f);

        Sequence boot = DOTween.Sequence().SetUpdate(true).SetDelay(delay);
        boot.Append(image.DOColor(rest, 0.22f).SetEase(Ease.OutExpo));
        if (label != null)
            boot.Join(label.DOAnchorPos(labelRest, 0.22f).SetEase(Ease.OutExpo));
        boot.OnComplete(() =>
        {
            if (token != digitalChromeToken)
                return;

            Sequence idle = BuildButtonDigitalIdle(image, rest, pulse, label, labelRest);
            if (button == confirmButton)
                confirmDigitalTween = idle;
            else if (button == backButton)
                backDigitalTween = idle;
        });
        return boot;
    }

    private static Sequence BuildButtonDigitalIdle(
        Image image,
        Color rest,
        Color pulse,
        RectTransform label,
        Vector2 labelRest)
    {
        Sequence idle = DOTween.Sequence().SetUpdate(true);
        idle.AppendInterval(1.7f);
        if (label != null)
        {
            idle.Append(label.DOAnchorPosX(labelRest.x + 2f, 0.04f).SetEase(Ease.OutQuad));
            idle.Append(label.DOAnchorPosX(labelRest.x - 1.5f, 0.035f).SetEase(Ease.OutQuad));
            idle.Append(label.DOAnchorPosX(labelRest.x, 0.05f).SetEase(Ease.InQuad));
        }

        idle.Append(image.DOColor(pulse, 0.05f).SetEase(Ease.OutQuad));
        idle.Append(image.DOColor(rest, 0.08f).SetEase(Ease.InQuad));
        idle.SetLoops(-1, LoopType.Restart);
        return idle;
    }

    private Sequence StartTypeIconDigital(int token)
    {
        if (!typeIconRestValid || typeIcon == null)
            return null;

        RectTransform iconRect = typeIcon.rectTransform;
        Color pulse = Color.Lerp(typeIconRestColor, DigitalAccent, 0.55f);
        pulse.a = typeIconRestColor.a;
        iconRect.localScale = typeIconRestScale * 1.08f;

        Sequence boot = DOTween.Sequence().SetUpdate(true);
        boot.Append(iconRect.DOScale(typeIconRestScale, 0.22f).SetEase(Ease.OutExpo));
        boot.OnComplete(() =>
        {
            if (token != digitalChromeToken || typeIcon == null)
                return;

            typeIconDigitalTween = BuildTypeIconDigitalIdle(iconRect, pulse);
        });
        return boot;
    }

    private Sequence BuildTypeIconDigitalIdle(RectTransform iconRect, Color pulse)
    {
        Sequence idle = DOTween.Sequence().SetUpdate(true);
        idle.AppendInterval(1.05f);
        idle.Append(iconRect.DOAnchorPosX(typeIconRestPosition.x + 3f, 0.045f).SetEase(Ease.OutQuad));
        idle.Append(iconRect.DOAnchorPosX(typeIconRestPosition.x - 2f, 0.04f).SetEase(Ease.OutQuad));
        idle.Append(iconRect.DOAnchorPosX(typeIconRestPosition.x, 0.05f).SetEase(Ease.InQuad));
        idle.Append(typeIcon.DOColor(pulse, 0.05f).SetEase(Ease.OutQuad));
        idle.Append(typeIcon.DOColor(typeIconRestColor, 0.08f).SetEase(Ease.InQuad));
        idle.Append(iconRect.DOScale(typeIconRestScale * 1.035f, 0.8f).SetEase(Ease.InOutSine));
        idle.Append(iconRect.DOScale(typeIconRestScale, 0.8f).SetEase(Ease.InOutSine));
        idle.SetLoops(-1, LoopType.Restart);
        return idle;
    }

    private static bool TryGetButtonImage(Button button, out Image image)
    {
        image = null;
        if (button == null)
            return false;

        image = button.targetGraphic as Image;
        if (image == null)
            image = button.GetComponent<Image>();
        return image != null;
    }

    private static RectTransform FindButtonLabel(Button button)
    {
        if (button == null)
            return null;

        Transform label = button.transform.Find("Label");
        return label as RectTransform;
    }

    private void CacheVisualFramePose()
    {
        if (visualFrameRect == null)
            visualFrameRect = FindChildByName<RectTransform>("VisualFrame");
        if (visualFrameRect == null || visualFramePoseCaptured)
            return;

        visualFrameAnchorMin = visualFrameRect.anchorMin;
        visualFrameAnchorMax = visualFrameRect.anchorMax;
        visualFrameAnchoredPosition = visualFrameRect.anchoredPosition;
        visualFrameSizeDelta = visualFrameRect.sizeDelta;
        visualFramePivot = visualFrameRect.pivot;
        visualFramePoseCaptured = true;
    }

    private void SetPanelTitleVisible(bool visible)
    {
        if (titleText != null)
            titleText.gameObject.SetActive(visible);
    }

    private void PlaceStarterNameInputInTitleSlot()
    {
        if (starterNameInputField == null)
            return;

        if (starterNameInputUsesAuthoredPose)
            return;

        RectTransform shell = starterNameInputField.transform as RectTransform;
        if (shell == null)
            return;

        RectTransform titleRect = titleText != null ? titleText.rectTransform : null;
        Transform host = titleRect != null
            ? titleRect.parent
            : ResolvePreviewContainer();
        if (host != null && shell.parent != host)
            shell.SetParent(host, false);

        if (titleRect != null)
        {
            shell.anchorMin = titleRect.anchorMin;
            shell.anchorMax = titleRect.anchorMax;
            shell.pivot = titleRect.pivot;
            shell.anchoredPosition = titleRect.anchoredPosition;
            shell.sizeDelta = titleRect.sizeDelta;
            int titleIndex = titleRect.GetSiblingIndex();
            shell.SetSiblingIndex(titleIndex);
        }

        Image background = shell.GetComponent<Image>();
        if (background != null)
        {
            background.color = Color.clear;
            background.raycastTarget = false;
        }
    }

    private void ResetNamingPresentation()
    {
        presentationMode = PresentationMode.Browse;
        namingPresentationTween?.Kill();
        namingPresentationTween = null;
        RestoreNameEntryLayout();
        HideStarterNameInput();
        SetPanelTitleVisible(true);

        ResolveDetailsPanel();
        if (detailsPanel != null)
            detailsPanel.SetActive(true);
        if (confirmButton != null)
            confirmButton.gameObject.SetActive(true);
        if (backButton != null)
            backButton.gameObject.SetActive(true);

        CacheVisualFramePose();
        if (visualFramePoseCaptured && visualFrameRect != null)
        {
            visualFrameRect.anchorMin = visualFrameAnchorMin;
            visualFrameRect.anchorMax = visualFrameAnchorMax;
            visualFrameRect.anchoredPosition = visualFrameAnchoredPosition;
            visualFrameRect.sizeDelta = visualFrameSizeDelta;
            visualFrameRect.pivot = visualFramePivot;
            visualFrameRect.localScale = Vector3.one;
        }

        if (visualFrameColorCaptured && visualFrameRect != null)
        {
            Image frameImage = visualFrameRect.GetComponent<Image>();
            if (frameImage != null)
                frameImage.color = visualFrameRestColor;
        }
        visualFrameColorCaptured = false;

        RestoreSpeciesNamePose();
        RestorePortraitPanelPlate();
        portraitReveal = false;
        portraitProfile = null;
        portraitFallback = null;
    }

    private List<CanvasGroup> CollectDismissContentGroups(Transform container)
    {
        var groups = new List<CanvasGroup>();
        if (container == null)
            return groups;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null || string.Equals(child.name, "ContainerFrame", System.StringComparison.Ordinal))
                continue;

            CanvasGroup group = child.GetComponent<CanvasGroup>();
            if (group == null)
                group = child.gameObject.AddComponent<CanvasGroup>();
            groups.Add(group);
        }

        return groups;
    }

    private bool EnsureStarterNameInput()
    {
        RectTransform shell = starterNameInputField != null
            ? starterNameInputField.transform as RectTransform
            : FindChildByName<RectTransform>("StarterNameInput");
        if (!starterNameInputResolved)
        {
            starterNameInputUsesAuthoredPose = shell != null;
            starterNameTextAuthored = shell != null && shell.Find("Text")?.GetComponent<TextMeshProUGUI>() != null;
            starterNamePlaceholderAuthored = shell != null && shell.Find("Placeholder")?.GetComponent<TextMeshProUGUI>() != null;
            starterNameInputResolved = true;
        }

        if (shell == null)
        {
            Transform host = titleText != null
                ? titleText.transform.parent
                : ResolvePreviewContainer();
            if (host == null)
                return false;

            GameObject shellObject = CreateUiObject("StarterNameInput", host);
            shell = shellObject.GetComponent<RectTransform>();
            if (titleText != null)
            {
                RectTransform titleRect = titleText.rectTransform;
                shell.anchorMin = titleRect.anchorMin;
                shell.anchorMax = titleRect.anchorMax;
                shell.pivot = titleRect.pivot;
                shell.anchoredPosition = titleRect.anchoredPosition;
                shell.sizeDelta = titleRect.sizeDelta;
                shell.SetSiblingIndex(titleRect.GetSiblingIndex());
            }
            else
            {
                shell.anchorMin = new Vector2(0.5f, 1f);
                shell.anchorMax = new Vector2(0.5f, 1f);
                shell.pivot = new Vector2(0.5f, 1f);
                shell.anchoredPosition = new Vector2(0f, -36f);
                shell.sizeDelta = new Vector2(760f, 80f);
            }
        }

        if (shell.GetComponent<CanvasRenderer>() == null)
            shell.gameObject.AddComponent<CanvasRenderer>();
        bool backgroundWasAuthored = shell.GetComponent<Image>() != null;
        Image background = shell.GetComponent<Image>();
        if (background == null)
            background = shell.gameObject.AddComponent<Image>();
        if (!backgroundWasAuthored)
        {
            background.color = Color.clear;
            background.raycastTarget = false;
        }

        starterNameInputField = shell.GetComponent<TMP_InputField>();
        if (starterNameInputField == null)
            starterNameInputField = shell.gameObject.AddComponent<TMP_InputField>();
        starterNameInputField.targetGraphic = background;
        starterNameInputField.textViewport = shell;

        Color titleColor = titleText != null ? titleText.color : Color.white;
        TextMeshProUGUI inputText = EnsureStarterNameInputText(
            shell,
            "Text",
            string.Empty,
            FontStyles.Bold | FontStyles.UpperCase,
            titleColor,
            starterNameTextAuthored);
        TextMeshProUGUI placeholder = EnsureStarterNameInputText(
            shell,
            "Placeholder",
            "Name your Chainling",
            FontStyles.Italic | FontStyles.UpperCase,
            new Color(titleColor.r, titleColor.g, titleColor.b, 0.45f),
            starterNamePlaceholderAuthored);
        starterNameInputField.textComponent = inputText;
        starterNameInputField.placeholder = placeholder;
        starterNameInputField.gameObject.SetActive(false);
        return true;
    }

    private void ConfigureStarterNameInput()
    {
        if (starterNameInputField == null)
            return;

        starterNameInputField.characterLimit = ChainlingDisplayNameRules.MaximumLength;
        starterNameInputField.contentType = TMP_InputField.ContentType.Name;
        starterNameInputField.lineType = TMP_InputField.LineType.SingleLine;
        starterNameInputField.readOnly = true;
        starterNameInputField.shouldActivateOnSelect = false;
        starterNameInputField.shouldHideSoftKeyboard = true;
        starterNameInputField.shouldHideMobileInput = true;
        starterNameInputField.caretWidth = 3;
        StyleStarterNameInputLikeTitle();
    }

    private void StyleStarterNameInputLikeTitle()
    {
        if (starterNameInputField == null)
            return;

        float fontSize = titleText != null ? titleText.fontSize : 52f;
        Color color = titleText != null ? titleText.color : Color.white;
        TMP_FontAsset font = titleText != null ? titleText.font : TMP_Settings.defaultFontAsset;
        Material fontMaterial = titleText != null ? titleText.fontSharedMaterial : null;

        TextMeshProUGUI inputText = starterNameInputField.textComponent as TextMeshProUGUI;
        TextMeshProUGUI placeholder = starterNameInputField.placeholder as TextMeshProUGUI;
        if (starterNameTextAuthored)
            ApplyUpperCaseFontStyle(inputText);
        else
        {
            StyleNameFieldLabel(
                inputText,
                font,
                fontMaterial,
                fontSize,
                FontStyles.Bold | FontStyles.UpperCase,
                color,
                string.Empty);
        }

        if (starterNamePlaceholderAuthored)
            ApplyUpperCaseFontStyle(placeholder);
        else
        {
            StyleNameFieldLabel(
                placeholder,
                font,
                fontMaterial,
                fontSize,
                FontStyles.Italic | FontStyles.UpperCase,
                new Color(color.r, color.g, color.b, 0.45f),
                "Name your Chainling");
        }
    }

    private static void ApplyUpperCaseFontStyle(TMP_Text text)
    {
        if (text == null)
            return;

        text.fontStyle = (text.fontStyle | FontStyles.UpperCase) & ~FontStyles.LowerCase & ~FontStyles.SmallCaps;
    }

    private static void StyleNameFieldLabel(
        TextMeshProUGUI text,
        TMP_FontAsset font,
        Material fontMaterial,
        float fontSize,
        FontStyles style,
        Color color,
        string placeholderValue)
    {
        if (text == null)
            return;

        if (font != null)
            text.font = font;
        if (fontMaterial != null)
            text.fontSharedMaterial = fontMaterial;
        text.fontSize = fontSize;
        text.fontStyle = (style | FontStyles.UpperCase) & ~FontStyles.LowerCase & ~FontStyles.SmallCaps;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        text.maxVisibleCharacters = int.MaxValue;
        if (!string.IsNullOrEmpty(placeholderValue))
            text.text = placeholderValue;
    }

    private static TextMeshProUGUI EnsureStarterNameInputText(
        RectTransform parent,
        string name,
        string value,
        FontStyles style,
        Color color,
        bool preserveAuthoredPresentation)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        bool created = text == null;
        if (created)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            textObject.transform.SetParent(parent, false);
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        if (created || !preserveAuthoredPresentation)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (text.font == null)
                text.font = TMP_Settings.defaultFontAsset;
            text.fontStyle = style | FontStyles.UpperCase;
            text.color = color;
            if (!string.IsNullOrEmpty(value) && string.IsNullOrEmpty(text.text))
                text.text = value;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
        }

        return text;
    }

    private T FindChildByName<T>(string childName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && string.Equals(components[i].gameObject.name, childName, System.StringComparison.Ordinal))
                return components[i];
        }

        return null;
    }

    private static bool WasUiCancelPressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.buttonEast.wasPressedThisFrame;
    }

    private void EnsureUi()
    {
        if (selectionPanel != null &&
            titleText != null &&
            previewVisual != null &&
            speciesNameText != null &&
            confirmButton != null &&
            backButton != null)
        {
            return;
        }

        BuildRuntimeUi();
    }

    private void BuildRuntimeUi()
    {
        if (runtimeCanvasRoot != null)
            return;

        runtimeCanvasRoot = CreateUiObject("StarterPreviewOverlay", transform);
        Canvas canvas = runtimeCanvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 430;
        runtimeCanvasRoot.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = runtimeCanvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        selectionPanel = runtimeCanvasRoot;
        Stretch(runtimeCanvasRoot.GetComponent<RectTransform>());

        GameObject safeAreaRoot = CreateUiObject("SafeAreaRoot", runtimeCanvasRoot.transform);
        Stretch(safeAreaRoot.GetComponent<RectTransform>());
        safeAreaRoot.AddComponent<SignalJam.UI.MobileControls.SafeAreaAdapter>();

        GameObject dim = CreateUiObject("DimBackground", safeAreaRoot.transform);
        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0.03f, 0.05f, 0.09f, 0.8f);
        Stretch(dim.GetComponent<RectTransform>());

        GameObject container = CreateUiObject("PreviewContainer", safeAreaRoot.transform);
        RectTransform containerRect = container.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0.5f, 0.5f);
        containerRect.anchorMax = new Vector2(0.5f, 0.5f);
        containerRect.pivot = new Vector2(0.5f, 0.5f);
        containerRect.sizeDelta = new Vector2(920f, 1450f);
        Image containerImage = container.AddComponent<Image>();
        containerImage.color = new Color(0.09f, 0.12f, 0.16f, 0.97f);
        Outline containerOutline = container.AddComponent<Outline>();
        containerOutline.effectColor = new Color(0.47f, 0.86f, 0.9f, 0.35f);
        containerOutline.effectDistance = new Vector2(5f, -5f);
        CombatUiScanlineBackground scanlines = container.AddComponent<CombatUiScanlineBackground>();
        scanlines.Configure(
            enabled: true,
            texture: panelScanlineTexture,
            color: new Color(1f, 1f, 1f, 0.647f),
            tiling: PanelScanlineTiling,
            scrollDirection: Vector2.down,
            uvSpeed: 0.05f);

        titleText = CreateText("TitleText", container.transform, 52, FontStyles.Bold, TextAlignmentOptions.Center, title);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -36f);
        titleRect.sizeDelta = new Vector2(760f, 80f);
        titleText.color = Color.white;

        GameObject visualFrame = CreateUiObject("VisualFrame", container.transform);
        RectTransform visualFrameRect = visualFrame.GetComponent<RectTransform>();
        visualFrameRect.anchorMin = new Vector2(0.5f, 1f);
        visualFrameRect.anchorMax = new Vector2(0.5f, 1f);
        visualFrameRect.pivot = new Vector2(0.5f, 1f);
        visualFrameRect.anchoredPosition = new Vector2(0f, -150f);
        visualFrameRect.sizeDelta = new Vector2(760f, 520f);
        typeBackgroundImage = visualFrame.AddComponent<Image>();
        typeBackgroundImage.color = new Color(0.05f, 0.09f, 0.13f, 0.98f);

        GameObject previewRoot = CreateUiObject("PreviewRoot", visualFrame.transform);
        RectTransform previewRootRect = previewRoot.GetComponent<RectTransform>();
        Stretch(previewRootRect, new Vector2(30f, 30f), new Vector2(-30f, -30f));

        pedestalImage = CreatePedestal(previewRoot.transform);

        GameObject previewImageObject = CreateUiObject("PreviewImage", previewRoot.transform);
        RectTransform previewImageRect = previewImageObject.GetComponent<RectTransform>();
        Stretch(previewImageRect);
        Image previewImage = previewImageObject.AddComponent<Image>();
        previewImage.preserveAspect = true;
        UIImageFrameAnimator frameAnimator = previewImageObject.AddComponent<UIImageFrameAnimator>();

        GameObject flashOverlayObject = CreateUiObject("FlashOverlay", previewRoot.transform);
        RectTransform flashOverlayRect = flashOverlayObject.GetComponent<RectTransform>();
        Stretch(flashOverlayRect);
        Image flashOverlayImage = flashOverlayObject.AddComponent<Image>();
        flashOverlayImage.preserveAspect = true;
        flashOverlayImage.color = new Color(0.55f, 1f, 0.95f, 0f);
        flashOverlayImage.raycastTarget = false;

        previewVisual = previewRoot.AddComponent<StarterPreviewVisual>();
        previewVisual.Initialize(previewImage, flashOverlayImage, previewImageRect, frameAnimator);

        speciesNameText = CreateText("SpeciesNameText", container.transform, 48, FontStyles.Bold, TextAlignmentOptions.Left, "");
        RectTransform speciesNameRect = speciesNameText.rectTransform;
        speciesNameRect.anchorMin = new Vector2(0.5f, 1f);
        speciesNameRect.anchorMax = new Vector2(0.5f, 1f);
        speciesNameRect.pivot = new Vector2(0.5f, 1f);
        speciesNameRect.anchoredPosition = new Vector2(0f, -700f);
        speciesNameRect.sizeDelta = new Vector2(760f, 64f);
        speciesNameText.color = Color.white;

        GameObject typeRow = CreateUiObject("TypeRow", container.transform);
        RectTransform typeRowRect = typeRow.GetComponent<RectTransform>();
        typeRowRect.anchorMin = new Vector2(0.5f, 1f);
        typeRowRect.anchorMax = new Vector2(0.5f, 1f);
        typeRowRect.pivot = new Vector2(0.5f, 1f);
        typeRowRect.anchoredPosition = new Vector2(0f, -785f);
        typeRowRect.sizeDelta = new Vector2(760f, 64f);
        HorizontalLayoutGroup typeLayout = typeRow.AddComponent<HorizontalLayoutGroup>();
        typeLayout.childAlignment = TextAnchor.MiddleLeft;
        typeLayout.childControlWidth = false;
        typeLayout.childControlHeight = false;
        typeLayout.childForceExpandWidth = false;
        typeLayout.childForceExpandHeight = false;
        typeLayout.spacing = 12f;

        typeIcon = CreateTypeIcon("TypeIcon", typeRow.transform);
        typeLabel = CreateText(
            "TypeLabel",
            typeRow.transform,
            28,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft,
            "");
        typeLabel.rectTransform.sizeDelta = new Vector2(320f, RuntimeTypeIconSize);
        typeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        typeLabel.color = Color.white;

        roleText = CreateText(
            "RoleText",
            container.transform,
            30,
            FontStyles.Bold,
            TextAlignmentOptions.Left,
            "");
        RectTransform roleRect = roleText.rectTransform;
        roleRect.anchorMin = new Vector2(0.5f, 1f);
        roleRect.anchorMax = new Vector2(0.5f, 1f);
        roleRect.pivot = new Vector2(0.5f, 1f);
        roleRect.anchoredPosition = new Vector2(0f, -885f);
        roleRect.sizeDelta = new Vector2(760f, 48f);
        roleText.color = new Color(0.83f, 0.89f, 0.97f, 0.96f);

        speciesDescriptionText = CreateText(
            "SpeciesDescriptionText",
            container.transform,
            32,
            FontStyles.Normal,
            TextAlignmentOptions.TopLeft,
            "");
        RectTransform speciesDescriptionRect = speciesDescriptionText.rectTransform;
        speciesDescriptionRect.anchorMin = new Vector2(0.5f, 1f);
        speciesDescriptionRect.anchorMax = new Vector2(0.5f, 1f);
        speciesDescriptionRect.pivot = new Vector2(0.5f, 1f);
        speciesDescriptionRect.anchoredPosition = new Vector2(0f, -950f);
        speciesDescriptionRect.sizeDelta = new Vector2(760f, 240f);
        speciesDescriptionText.color = new Color(0.9f, 0.94f, 0.98f, 0.94f);

        GameObject buttonRow = CreateUiObject("ButtonRow", container.transform);
        RectTransform buttonRowRect = buttonRow.GetComponent<RectTransform>();
        buttonRowRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRowRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRowRect.pivot = new Vector2(0.5f, 0f);
        buttonRowRect.anchoredPosition = new Vector2(0f, 48f);
        buttonRowRect.sizeDelta = new Vector2(760f, 150f);
        HorizontalLayoutGroup buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = false;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = true;
        buttonLayout.spacing = 24f;

        confirmButton = CreateButton("ConfirmButton", buttonRow.transform, "Choose!", new Color(0.1f, 0.57f, 0.37f, 1f));
        backButton = CreateButton("BackButton", buttonRow.transform, "Back", new Color(0.27f, 0.31f, 0.38f, 1f));

        Navigation confirmNav = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = backButton,
            selectOnRight = backButton,
            selectOnDown = backButton,
            selectOnUp = backButton,
        };
        confirmButton.navigation = confirmNav;

        Navigation backNav = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = confirmButton,
            selectOnRight = confirmButton,
            selectOnDown = confirmButton,
            selectOnUp = confirmButton,
        };
        backButton.navigation = backNav;
    }

    private Image CreatePedestal(Transform parent)
    {
        GameObject pedestal = CreateUiObject("PedestalDither", parent);
        RectTransform rect = pedestal.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.35f);
        rect.anchoredPosition = new Vector2(0f, 48f);
        rect.sizeDelta = new Vector2(360f, 90f);

        Image image = pedestal.AddComponent<Image>();
        image.sprite = pedestalSprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;
        image.transform.SetAsFirstSibling();
        return image;
    }

    private static Image CreateTypeIcon(string name, Transform parent)
    {
        GameObject iconObject = CreateUiObject(name, parent);
        RectTransform rect = iconObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(RuntimeTypeIconSize, RuntimeTypeIconSize);
        Image image = iconObject.AddComponent<Image>();
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;

        LayoutElement layout = iconObject.AddComponent<LayoutElement>();
        layout.minWidth = RuntimeTypeIconSize;
        layout.minHeight = RuntimeTypeIconSize;
        layout.preferredWidth = RuntimeTypeIconSize;
        layout.preferredHeight = RuntimeTypeIconSize;
        layout.flexibleWidth = 0f;
        layout.flexibleHeight = 0f;
        return image;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color fillColor)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(300f, 110f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = fillColor;

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = fillColor;
        colors.highlightedColor = fillColor * 1.08f;
        colors.pressedColor = fillColor * 0.88f;
        colors.selectedColor = fillColor * 1.04f;
        colors.disabledColor = new Color(fillColor.r * 0.45f, fillColor.g * 0.45f, fillColor.b * 0.45f, 0.85f);
        button.colors = colors;

        TextMeshProUGUI tmp = CreateText("Label", buttonObject.transform, 34, FontStyles.Bold, TextAlignmentOptions.Center, label);
        Stretch(tmp.rectTransform);
        tmp.color = Color.white;

        return button;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions alignment,
        string value)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI tmp = textObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.text = value;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        Stretch(rectTransform, Vector2.zero, Vector2.zero);
    }

    private static void Stretch(RectTransform rectTransform, Vector2 offsetMin, Vector2 offsetMax)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = offsetMin;
        rectTransform.offsetMax = offsetMax;
        rectTransform.anchoredPosition = Vector2.zero;
    }
}
