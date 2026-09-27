using System;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public class ChainlingPartyColumnShell : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image typeBackgroundImage;
        [SerializeField] private Image cardChromeImage;
        [SerializeField] private PartyTabFrameAnimator cardChromeAnimator;
        [SerializeField] private ChainlingPartyTypeChromePaletteSO typeChromePalette;
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private PartyTabArchiveIndicatorArtSO archiveIndicatorArt;
        [SerializeField] private PartyTabCycleTypographySO cycleTypography;
        [SerializeField] private Image archiveIndicatorImage;
        [SerializeField] private TMP_Text archiveIndicatorFallbackLabel;
        [SerializeField] private TMP_Text entangledBadgeLabel;
        [SerializeField] private RectTransform typeIconMask;
        [SerializeField] private RectTransform typeIconScrollRoot;
        [SerializeField] private Image typeIconPrimaryImage;
        [SerializeField] private Image typeIconSecondaryImage;
        [Header("Type Icon Layer")]
        [SerializeField] private bool typeIconOverrideAuthoredViewport;
        [SerializeField] private Vector2 typeIconViewportAnchorMin = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 typeIconViewportAnchorMax = new Vector2(0.5f, 0.5f);
        [SerializeField] private Vector2 typeIconViewportAnchoredPosition = new Vector2(0f, 150f);
        [SerializeField] private Vector2 typeIconViewportSizeDelta = new Vector2(370f, 340f);
        [Tooltip("When disabled, PrimaryTypeIcon keeps its prefab-authored RectTransform position and size at runtime.")]
        [SerializeField] private bool typeIconOverrideAuthoredIconRects;
        [SerializeField] private Vector2 typeIconSize = new Vector2(260f, 260f);
        [SerializeField] private Vector2 typeIconScrollDirection = Vector2.up;
        [SerializeField] private float typeIconScrollDistance = 86f;
        [SerializeField] private float typeIconScrollPixelsPerSecond = 15.64f;
        [SerializeField] private float typeIconAlpha = 0.18f;
        [SerializeField, HideInInspector] private float typeIconScrollDuration = 5.5f;
        [SerializeField, HideInInspector] private Vector2 typeIconScrollOffset = new Vector2(0f, 86f);
        [SerializeField] private TMP_Text roleLabel;
        [SerializeField] private Color roleLabelColor = new Color(0.2f, 0.85f, 0.75f, 1f);
        [SerializeField] private Color benchedRoleLabelColor = new Color(0.64f, 0.72f, 0.82f, 1f);
        [SerializeField] private TMP_Text primaryTypeLabel;
        [SerializeField] private TMP_Text secondaryTypeLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private RectTransform visualStage;
        [SerializeField] private CanvasGroup visualStageCanvasGroup;
        [SerializeField] private Image visualImage;
        [SerializeField] private ChainlingMenuVisualPresenter visualPresenter;
        [SerializeField] private Image emptyPedestalImage;
        [SerializeField] private PartyTabFrameAnimator emptyPedestalAnimator;
        [SerializeField] private Image hpBarBackground;
        [SerializeField] private Image hpBarFill;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private CanvasGroup infoPanelGroup;
        [SerializeField] private ChainlingPartyInfoPanelBinder infoPanelBinder;
        [SerializeField] private CanvasGroup columnCanvasGroup;
        [SerializeField] private Image columnDimOverlay;
        [SerializeField] private Image infoPanelBackgroundImage;

        private int slotIndex;
        private Action<int> onFocused;
        private Action<int> onSubmitted;
        private Vector3 columnBaseScale = Vector3.one;
        private Vector3 visualStageBaseScale = Vector3.one;
        private bool baseScalesCaptured;
        private bool slotFilled;
        private SpeciesDefinitionSO activeSpecies;
        private bool activeEntangled;
        private ArchiveBindingState activeArchiveState = ArchiveBindingState.Dossier;
        private ChainlingPartyColumnVariantDefinitionSO activeVariant;
        private Tween typeIconScrollTween;
        private Sprite activeTypeIconScrollSprite;
        private Vector2 activeTypeIconScrollVector;
        private float activeTypeIconScrollDuration;
        private Vector2 activeTypeIconScrollBasePosition;
        private bool defaultVariantCaptured;
        private Sprite defaultCardChromeSprite;
        private Color defaultCardChromeColor;

        private static readonly Color TransparentHitTargetColor = new Color(1f, 1f, 1f, 0f);
        private static readonly Color DefaultEmptyBackgroundColor = new Color(0.07f, 0.095f, 0.14f, 1f);
        private static readonly Color DefaultEmptyPedestalColor = new Color(0.5f, 0.44f, 0.54f, 1f);

        public Button Button => button;
        public Color RoleLabelColor => roleLabelColor;
        public Color BenchedRoleLabelColor => benchedRoleLabelColor;
        public int SlotIndex => slotIndex;
        public RectTransform ColumnRect => transform as RectTransform;
        public Vector3 ColumnBaseScale => columnBaseScale;
        public RectTransform VisualStage => visualStage;
        public Vector3 VisualStageBaseScale => visualStageBaseScale;
        public CanvasGroup InfoPanelGroup => infoPanelGroup;
        public ChainlingPartyInfoPanelBinder InfoPanelBinder => infoPanelBinder;
        public CanvasGroup InfoPanelViewContentGroup =>
            infoPanelBinder != null ? infoPanelBinder.ViewContentGroup : null;
        public CanvasGroup ColumnCanvasGroup => columnCanvasGroup;
        public Image HpBarFill => hpBarFill;
        public TMP_Text HpLabel => hpLabel;
        public ChainlingMenuVisualPresenter VisualPresenter => visualPresenter;
        public Image TypeBackgroundImage => typeBackgroundImage;
        public Image CardChromeImage => cardChromeImage;
        public Image EmptyPedestalImage => emptyPedestalImage;
        public RectTransform TypeIconMask => typeIconMask;
        public Image TypeIconPrimaryImage => typeIconPrimaryImage;
        public Image TypeIconSecondaryImage => typeIconSecondaryImage;
        public Image ArchiveIndicatorImage => archiveIndicatorImage;
        public TMP_Text EntangledBadgeLabel => entangledBadgeLabel;
        public ChainlingPartyTypeChromePaletteSO TypeChromePalette => typeChromePalette;
        public TypeIconPaletteSO TypeIconPalette => typeIconPalette;

        /// <summary>
        /// Applies navigation-state visibility without mutating authored Image colors on the column root.
        /// Uses a black dim overlay; keeps the root background tint at its prefab-authored value.
        /// </summary>
        public void ApplyColumnVisibility(float visibilityAlpha, bool animate)
        {
            EnsureBuilt();
            EnsureColumnDimOverlay();

            visibilityAlpha = Mathf.Clamp01(visibilityAlpha);
            float dimAlpha = 1f - visibilityAlpha;

            if (columnCanvasGroup != null)
            {
                columnCanvasGroup.DOKill();
                columnCanvasGroup.alpha = 1f;
            }

            if (columnDimOverlay == null)
                return;

            columnDimOverlay.DOKill();
            Color targetColor = new Color(0f, 0f, 0f, dimAlpha);

            if (animate && Application.isPlaying)
            {
                columnDimOverlay
                    .DOColor(targetColor, 0.12f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
            }
            else
            {
                columnDimOverlay.color = targetColor;
            }

            columnDimOverlay.enabled = dimAlpha > 0.001f;
            columnDimOverlay.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Dims only the VisualStage subtree (pedestal + chainling art) when the party section is
        /// selected but this column is not focused.
        /// </summary>
        public void ApplyVisualStageVisibility(float alpha, bool animate)
        {
            EnsureBuilt();
            EnsureVisualStageCanvasGroup();

            if (visualStageCanvasGroup == null)
                return;

            alpha = Mathf.Clamp01(alpha);
            visualStageCanvasGroup.DOKill();

            if (animate && Application.isPlaying)
            {
                visualStageCanvasGroup
                    .DOFade(alpha, 0.12f)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
            }
            else
            {
                visualStageCanvasGroup.alpha = alpha;
            }
        }

        private void Awake()
        {
            EnsureBuilt();
            ApplyUnboundPlaceholder();
        }

        private void OnEnable()
        {
            if (button != null)
                button.onClick.AddListener(HandleSubmitted);
        }

        private void OnDisable()
        {
            if (button != null)
                button.onClick.RemoveListener(HandleSubmitted);
            StopCardChromeAnimation();
            StopTypeIconScroll();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            if (infoPanelBinder != null)
                infoPanelBinder.BuildDefaultLayoutForEditor();
        }

        public void ConfigureTypeChromePalette(ChainlingPartyTypeChromePaletteSO palette)
        {
            typeChromePalette = palette;
            EnsureBuilt();
            if (infoPanelBinder != null)
                infoPanelBinder.ConfigureTypeChromePalette(typeChromePalette);
            RefreshTypeChrome();
        }

        public void ConfigureTypeIconPalette(TypeIconPaletteSO palette)
        {
            typeIconPalette = palette;
            EnsureBuilt();
            if (infoPanelBinder != null)
            {
                PartyTabMoveRarityPaletteSO rarity = null;
                ChainlingPartyMovesViewLayout movesLayout =
                    infoPanelBinder.GetComponentInChildren<ChainlingPartyMovesViewLayout>(true);
                if (movesLayout != null)
                    rarity = movesLayout.RarityPalette;
                infoPanelBinder.ConfigureEntryMovePalettes(typeIconPalette, rarity);
            }

            RefreshTypeIcon();
        }

        public void ConfigureArchiveIndicatorArt(PartyTabArchiveIndicatorArtSO art)
        {
            if (art != null)
                archiveIndicatorArt = art;

            EnsureBuilt();
            RefreshArchiveIndicator();
        }

        public void ConfigureCycleTypography(PartyTabCycleTypographySO typography)
        {
            if (typography != null)
                cycleTypography = typography;

            EnsureBuilt();
            if (infoPanelBinder != null)
                infoPanelBinder.ConfigureCycleTypography(cycleTypography);
        }

        public void ConfigureTypeIconPresentation(
            bool overrideAuthoredViewport,
            bool overrideAuthoredIconRects,
            float alpha,
            Vector2 viewportAnchorMin,
            Vector2 viewportAnchorMax,
            Vector2 viewportAnchoredPosition,
            Vector2 viewportSizeDelta,
            Vector2 iconSize,
            Vector2 scrollDirection,
            float scrollDistance,
            float scrollPixelsPerSecond)
        {
            typeIconOverrideAuthoredViewport = overrideAuthoredViewport;
            typeIconOverrideAuthoredIconRects = overrideAuthoredIconRects;
            typeIconAlpha = Mathf.Clamp01(alpha);
            if (typeIconOverrideAuthoredViewport)
            {
                typeIconViewportAnchorMin = Clamp01(viewportAnchorMin);
                typeIconViewportAnchorMax = Clamp01(viewportAnchorMax);
                typeIconViewportAnchoredPosition = viewportAnchoredPosition;
                typeIconViewportSizeDelta = viewportSizeDelta;
                NormalizeViewportAnchors();
            }

            typeIconSize = SanitizeIconSize(iconSize);
            typeIconScrollDirection = ResolveDirection(scrollDirection);
            typeIconScrollDistance = Mathf.Max(1f, scrollDistance);
            typeIconScrollPixelsPerSecond = Mathf.Max(0f, scrollPixelsPerSecond);

            EnsureBuilt();
            RefreshTypeIcon();
        }

        public void Bind(
            int index,
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            ResolvedChainlingVisualIdentity resolvedVisualIdentity,
            ChainlingPartyColumnSummary summary,
            ChainlingPartyViewMode viewMode,
            bool focused,
            Action<int> focusHandler,
            Action<int> submitHandler)
        {
            EnsureBuilt();
            slotIndex = index;
            onFocused = focusHandler;
            onSubmitted = submitHandler;

            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            slotFilled = snapshot != null;
            activeEntangled = ChainlingProgressionIdentityUtility.IsEntangled(snapshot);
            activeArchiveState = ChainlingPlayerTerms.ResolveArchiveState(snapshot);
            activeSpecies = species;

            ApplyPartyColumnVariant(resolvedVisualIdentity != null ? resolvedVisualIdentity.partyColumnVariant : null);
            RefreshTypeChrome();
            RefreshArchiveIndicator();
            RefreshEntangledBadge();

            if (roleLabel != null)
            {
                roleLabel.text = ChainlingsPartyScreen.SlotLabel(index);
                roleLabel.color = ChainlingsPartyScreen.IsBenchedPartySlot(index)
                    ? benchedRoleLabelColor
                    : roleLabelColor;
            }

            if (nameLabel != null)
                nameLabel.text = slotFilled ? ResolveDisplayName(snapshot, species) : "Empty";

            BindTypes(slotFilled, species, viewMode);
            RefreshTypeIcon();
            BindVisual(slotFilled, resolvedVisualIdentity);
            BindEmptyState(slotFilled);
            SetFocused(focused);

            if (infoPanelBinder != null)
                infoPanelBinder.Bind(summary, viewMode);

            if (button != null)
                button.interactable = true;
        }

        public void ApplyUnboundPlaceholder()
        {
            EnsureBuilt();
            BindTypes(false, null, ChainlingPartyViewMode.General);
            BindVisual(false, null);
            BindEmptyState(false);

            if (nameLabel != null)
                nameLabel.text = "Empty";

            if (infoPanelBinder != null)
                infoPanelBinder.PrepareUnboundPresentation();
        }

        public void SetFocused(bool focused)
        {
        }

        public void BindView(ChainlingPartyColumnSummary summary, ChainlingPartyViewMode viewMode)
        {
            EnsureBuilt();
            BindTypes(slotFilled, activeSpecies, viewMode);

            if (infoPanelBinder != null)
                infoPanelBinder.Bind(summary, viewMode);
        }

        public void ApplyPartyColumnVariant(ChainlingPartyColumnVariantDefinitionSO variant)
        {
            EnsureBuilt();
            CaptureDefaultVariantStateIfNeeded();

            activeVariant = variant;
            if (variant == null || string.Equals(variant.Id, ChainlingVisualIdentity.DefaultId, StringComparison.Ordinal))
            {
                RestoreDefaultPartyColumnVariant();
                RefreshCardChromeVisibility();
                return;
            }

            ApplyCardChromeOverride(variant);
            RefreshCardChromeVisibility();
        }

        public void ConfigureStarRatingArt(PartyTabStarRatingArtSO art)
        {
            EnsureBuilt();
            if (infoPanelBinder != null)
                infoPanelBinder.ConfigureStarRatingArt(art);
        }

        public void OnSelect(BaseEventData eventData)
        {
            onFocused?.Invoke(slotIndex);
        }

        private void HandleSubmitted()
        {
            onSubmitted?.Invoke(slotIndex);
        }

        private void BindTypes(bool filled, SpeciesDefinitionSO species, ChainlingPartyViewMode viewMode)
        {
            bool showTypeText = viewMode != ChainlingPartyViewMode.General;

            if (primaryTypeLabel != null)
            {
                primaryTypeLabel.text = filled && species != null && species.primaryType != ChainlingType.None
                    ? species.primaryType.ToString()
                    : "";
                primaryTypeLabel.gameObject.SetActive(showTypeText && !string.IsNullOrEmpty(primaryTypeLabel.text));
            }

            if (secondaryTypeLabel != null)
            {
                secondaryTypeLabel.text = "";
                secondaryTypeLabel.gameObject.SetActive(false);
            }
        }

        private void BindVisual(bool filled, ResolvedChainlingVisualIdentity resolvedVisualIdentity)
        {
            if (visualPresenter != null)
                visualPresenter.Bind(resolvedVisualIdentity, filled, animateIdentityChange: false);
        }

        private void BindEmptyState(bool filled)
        {
            slotFilled = filled;
            RefreshCardChromeVisibility();
            RefreshTypeIcon();
            RefreshArchiveIndicator();
            RefreshEntangledBadge();
            RefreshSpotlightPedestal();
        }

        private void RefreshSpotlightPedestal()
        {
            if (emptyPedestalImage != null)
                emptyPedestalImage.enabled = true;

            if (emptyPedestalAnimator != null)
                emptyPedestalAnimator.Play();

            EnsureSpotlightPedestalBehindVisual();
        }

        private void EnsureSpotlightPedestalBehindVisual()
        {
            if (emptyPedestalImage == null || visualImage == null)
                return;

            int visualSiblingIndex = visualImage.transform.GetSiblingIndex();
            if (visualSiblingIndex <= 0)
            {
                emptyPedestalImage.transform.SetSiblingIndex(0);
                visualImage.transform.SetSiblingIndex(1);
                return;
            }

            emptyPedestalImage.transform.SetSiblingIndex(visualSiblingIndex - 1);
        }

        private void CaptureDefaultVariantStateIfNeeded()
        {
            if (defaultVariantCaptured)
                return;

            if (cardChromeImage != null)
            {
                defaultCardChromeSprite = cardChromeImage.sprite;
                defaultCardChromeColor = cardChromeImage.color;
            }

            defaultVariantCaptured = true;
        }

        private void RestoreDefaultPartyColumnVariant()
        {
            if (cardChromeImage != null)
            {
                cardChromeImage.sprite = defaultCardChromeSprite;
                cardChromeImage.color = defaultCardChromeColor;
            }

            StopCardChromeAnimation();
        }

        private void ApplyCardChromeOverride(ChainlingPartyColumnVariantDefinitionSO variant)
        {
            if (cardChromeImage == null || variant == null)
                return;

            if (variant.staticCardFrameSprite != null)
                cardChromeImage.sprite = variant.staticCardFrameSprite;

            if (variant.HasCardFrameAnimation)
            {
                EnsureCardChromeAnimator();
                cardChromeAnimator.SetFPS(variant.cardFrameFps);
                cardChromeAnimator.SetLoop(variant.cardFrameLoop);
                cardChromeAnimator.SetFrames(variant.cardFrameSequence);
            }
            else
            {
                StopCardChromeAnimation();
            }
        }

        private void RefreshCardChromeVisibility()
        {
            if (cardChromeImage == null)
                return;

            bool hasVisual = cardChromeImage.sprite != null ||
                (activeVariant != null && activeVariant.HasCardFrameAnimation);
            cardChromeImage.enabled = slotFilled && hasVisual;
            EnsureCardChromeOverlayOrder();

            if (slotFilled && activeVariant != null && activeVariant.HasCardFrameAnimation)
                cardChromeAnimator?.Play();
            else
                StopCardChromeAnimation();
        }

        private void EnsureCardChromeAnimator()
        {
            if (cardChromeImage == null)
                return;

            if (cardChromeAnimator == null)
                cardChromeAnimator = cardChromeImage.GetComponent<PartyTabFrameAnimator>();
            if (cardChromeAnimator == null)
                cardChromeAnimator = cardChromeImage.gameObject.AddComponent<PartyTabFrameAnimator>();
        }

        private void StopCardChromeAnimation()
        {
            cardChromeAnimator?.Stop();
        }

        public void EnsureCosmeticLayers()
        {
            EnsureTypeBackground();
            EnsureTypeIconLayer();
            EnsureArchiveIndicator();
            EnsureEntangledBadge();
            EnsureCardChromeLayer();
            EnsureCardChromeOverlayOrder();
        }

        private void EnsureCardChromeLayer()
        {
            bool createdCardChrome = false;

            if (cardChromeImage == null)
            {
                Transform existing = transform.Find("CardChrome");
                if (existing != null)
                    cardChromeImage = existing.GetComponent<Image>();
            }

            if (cardChromeImage == null)
            {
                cardChromeImage = PlayerMenuUiFactory.CreateImage("CardChrome", transform, Color.white);
                createdCardChrome = true;
            }

            ConfigureStretch(cardChromeImage.rectTransform, 0f, 0f, 0f, 0f);
            cardChromeImage.raycastTarget = false;
            cardChromeImage.type = cardChromeImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            cardChromeImage.fillCenter = false;

            if (createdCardChrome)
                cardChromeImage.enabled = false;
        }

        private void RefreshTypeChrome()
        {
            EnsureTypeBackground();
            EnsureTypeIconLayer();

            if (typeBackgroundImage != null)
            {
                Sprite paletteSprite = typeChromePalette != null ? typeChromePalette.BackgroundSprite : null;
                if (paletteSprite != null)
                    typeBackgroundImage.sprite = paletteSprite;

                typeBackgroundImage.type = typeBackgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
                typeBackgroundImage.raycastTarget = false;
                typeBackgroundImage.enabled = true;
                typeBackgroundImage.color = ResolveTypeBackgroundColor();
                typeBackgroundImage.transform.SetAsFirstSibling();
            }

            if (emptyPedestalImage != null)
                emptyPedestalImage.color = ResolveTypePedestalColor();

            RefreshTypeIconMask();
        }

        private void RefreshTypeIcon()
        {
            EnsureTypeIconLayer();

            ChainlingType type = slotFilled && activeSpecies != null
                ? activeSpecies.primaryType
                : ChainlingType.None;
            Sprite icon = slotFilled && typeIconPalette != null
                ? typeIconPalette.GetIcon(type)
                : null;
            bool showIcon = slotFilled && icon != null;

            ConfigureTypeIconImage(typeIconPrimaryImage, icon, showIcon);
            DisableSecondaryTypeIcon();

            if (typeIconMask != null)
                typeIconMask.gameObject.SetActive(showIcon);

            if (showIcon)
                PlayTypeIconScroll(icon);
            else
                StopTypeIconScroll();
        }

        private void ConfigureTypeIconImage(Image image, Sprite icon, bool showIcon)
        {
            if (image == null)
                return;

            image.sprite = showIcon ? icon : null;
            image.enabled = showIcon;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, typeIconAlpha);
        }

        private void DisableSecondaryTypeIcon()
        {
            if (typeIconSecondaryImage == null)
                return;

            typeIconSecondaryImage.sprite = null;
            typeIconSecondaryImage.enabled = false;
            typeIconSecondaryImage.gameObject.SetActive(false);
        }

        private void RefreshTypeIconMask()
        {
            if (typeIconMask == null)
                return;

            Image maskImage = typeIconMask.GetComponent<Image>();
            if (maskImage == null)
                return;

            Sprite paletteSprite = typeChromePalette != null ? typeChromePalette.BackgroundSprite : null;
            if (paletteSprite != null)
                maskImage.sprite = paletteSprite;
            maskImage.type = maskImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            maskImage.raycastTarget = false;

            Mask mask = typeIconMask.GetComponent<Mask>();
            if (mask != null)
                mask.showMaskGraphic = false;
        }

        private void EnsureArchiveIndicator()
        {
            if (archiveIndicatorImage == null)
            {
                Transform existing = transform.Find("ArchiveIndicator");
                if (existing != null)
                    archiveIndicatorImage = existing.GetComponent<Image>();
            }

            if (archiveIndicatorImage == null)
                archiveIndicatorImage = PlayerMenuUiFactory.CreateImage("ArchiveIndicator", transform, Color.white);

            RectTransform rect = archiveIndicatorImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = archiveIndicatorArt != null
                ? archiveIndicatorArt.IndicatorSize
                : new Vector2(34f, 34f);
            archiveIndicatorImage.raycastTarget = false;
            archiveIndicatorImage.preserveAspect = true;

            if (archiveIndicatorFallbackLabel == null)
                archiveIndicatorFallbackLabel = archiveIndicatorImage.transform.Find("FallbackGlyph")?.GetComponent<TMP_Text>();

            if (archiveIndicatorFallbackLabel == null)
            {
                archiveIndicatorFallbackLabel = PlayerMenuUiFactory.CreateText(
                    "FallbackGlyph",
                    archiveIndicatorImage.transform,
                    "",
                    17f,
                    TextAlignmentOptions.Center,
                    false);
                archiveIndicatorFallbackLabel.fontStyle = FontStyles.Bold;
                PlayerMenuUiFactory.Stretch(archiveIndicatorFallbackLabel.rectTransform, 0f, 0f, 0f, 0f);
            }
        }

        private void EnsureEntangledBadge()
        {
            if (entangledBadgeLabel == null)
                entangledBadgeLabel = transform.Find("EntangledBadge")?.GetComponent<TMP_Text>();

            if (entangledBadgeLabel == null)
            {
                entangledBadgeLabel = PlayerMenuUiFactory.CreateText(
                    "EntangledBadge",
                    transform,
                    "ENT",
                    16f,
                    TextAlignmentOptions.Center,
                    false);
                entangledBadgeLabel.fontStyle = FontStyles.Bold;
                entangledBadgeLabel.color = new Color(1f, 0.66f, 0.86f, 1f);
            }

            RectTransform rect = entangledBadgeLabel.rectTransform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(58f, 26f);
            rect.anchoredPosition = new Vector2(-14f, -38f);
            entangledBadgeLabel.raycastTarget = false;
        }

        private void RefreshArchiveIndicator()
        {
            EnsureArchiveIndicator();
            bool visible = slotFilled;
            if (archiveIndicatorImage == null)
                return;

            archiveIndicatorImage.gameObject.SetActive(visible);
            if (!visible)
                return;

            Sprite sprite = archiveIndicatorArt != null
                ? archiveIndicatorArt.ResolveSprite(activeArchiveState)
                : null;
            Color tint = archiveIndicatorArt != null
                ? archiveIndicatorArt.ResolveTint(activeArchiveState)
                : PartyTabArchiveIndicatorArtSO.FallbackTint(activeArchiveState);

            archiveIndicatorImage.sprite = sprite;
            archiveIndicatorImage.enabled = sprite != null;
            archiveIndicatorImage.color = tint;
            archiveIndicatorImage.rectTransform.sizeDelta = archiveIndicatorArt != null
                ? archiveIndicatorArt.IndicatorSize
                : new Vector2(34f, 34f);

            if (archiveIndicatorFallbackLabel != null)
            {
                archiveIndicatorFallbackLabel.gameObject.SetActive(sprite == null);
                archiveIndicatorFallbackLabel.text = archiveIndicatorArt != null
                    ? archiveIndicatorArt.ResolveFallbackGlyph(activeArchiveState)
                    : activeArchiveState == ArchiveBindingState.Anchored ? "B" : "P";
                archiveIndicatorFallbackLabel.color = tint;
            }
        }

        private void RefreshEntangledBadge()
        {
            EnsureEntangledBadge();
            if (entangledBadgeLabel == null)
                return;

            bool visible = slotFilled && activeEntangled;
            entangledBadgeLabel.gameObject.SetActive(visible);
            if (visible)
                entangledBadgeLabel.text = "ENT";
        }

        private void PlayTypeIconScroll(Sprite icon)
        {
            if (typeIconScrollRoot == null)
                return;

            Vector2 scrollVector = ResolveTypeIconScrollVector();
            float scrollDuration = ResolveTypeIconScrollDuration(scrollVector);
            if (scrollVector.sqrMagnitude <= 0.0001f || scrollDuration <= 0.01f)
                return;

            Vector2 basePosition = ResolveTypeIconScrollBasePosition();
            if (IsCurrentTypeIconScrollLoop(icon, scrollVector, scrollDuration, basePosition))
                return;

            typeIconScrollRoot.DOKill();
            typeIconScrollTween?.Kill();

            activeTypeIconScrollSprite = icon;
            activeTypeIconScrollVector = scrollVector;
            activeTypeIconScrollDuration = scrollDuration;
            activeTypeIconScrollBasePosition = basePosition;

            typeIconScrollRoot.anchoredPosition = basePosition - scrollVector * 0.5f;
            typeIconScrollTween = typeIconScrollRoot
                .DOAnchorPos(basePosition + scrollVector * 0.5f, scrollDuration)
                .SetEase(Ease.Linear)
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true);
        }

        private void StopTypeIconScroll()
        {
            typeIconScrollTween?.Kill();
            typeIconScrollTween = null;
            activeTypeIconScrollSprite = null;
            activeTypeIconScrollVector = Vector2.zero;
            activeTypeIconScrollDuration = 0f;
            activeTypeIconScrollBasePosition = Vector2.zero;

            if (typeIconScrollRoot != null)
            {
                typeIconScrollRoot.DOKill();
                typeIconScrollRoot.anchoredPosition = ResolveTypeIconScrollBasePosition();
            }
        }

        private bool IsCurrentTypeIconScrollLoop(
            Sprite icon,
            Vector2 scrollVector,
            float scrollDuration,
            Vector2 basePosition)
        {
            return typeIconScrollTween != null &&
                typeIconScrollTween.IsActive() &&
                activeTypeIconScrollSprite == icon &&
                Approximately(activeTypeIconScrollVector, scrollVector) &&
                Mathf.Abs(activeTypeIconScrollDuration - scrollDuration) <= 0.001f &&
                Approximately(activeTypeIconScrollBasePosition, basePosition);
        }

        private Vector2 ResolveTypeIconScrollBasePosition()
        {
            return typeIconViewportAnchoredPosition;
        }

        private Color ResolveTypeBackgroundColor()
        {
            if (!slotFilled)
                return typeChromePalette != null
                    ? typeChromePalette.EmptyBackgroundColor
                    : DefaultEmptyBackgroundColor;

            ChainlingType type = activeSpecies != null ? activeSpecies.primaryType : ChainlingType.None;
            Color color = typeChromePalette != null
                ? typeChromePalette.ResolveBackgroundColor(type)
                : ChainlingTypeDisplaySO.CreateFallback(type).badgeColor;
            if (activeEntangled && typeChromePalette != null)
                color = typeChromePalette.ResolveEntangledBackgroundColor(color);
            return WithAlpha(color, 1f);
        }

        private Color ResolveTypePedestalColor()
        {
            if (!slotFilled)
                return typeChromePalette != null
                    ? typeChromePalette.EmptyPedestalColor
                    : DefaultEmptyPedestalColor;

            ChainlingType type = activeSpecies != null ? activeSpecies.primaryType : ChainlingType.None;
            Color color = typeChromePalette != null
                ? typeChromePalette.ResolvePedestalColor(type)
                : ChainlingTypeDisplaySO.CreateFallback(type).badgeColor;
            return WithAlpha(color, 1f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private Vector2 ResolveTypeIconScrollVector()
        {
            Vector2 direction = ResolveDirection(typeIconScrollDirection);
            float distance = Mathf.Max(1f, typeIconScrollDistance);
            return direction * distance;
        }

        private float ResolveTypeIconScrollDuration(Vector2 scrollVector)
        {
            float distance = Mathf.Max(0.01f, scrollVector.magnitude);
            if (typeIconScrollPixelsPerSecond > 0.01f)
                return distance / typeIconScrollPixelsPerSecond;

            return Mathf.Max(0.01f, typeIconScrollDuration);
        }

        private void NormalizeViewportAnchors()
        {
            Vector2 min = Vector2.Min(typeIconViewportAnchorMin, typeIconViewportAnchorMax);
            Vector2 max = Vector2.Max(typeIconViewportAnchorMin, typeIconViewportAnchorMax);
            typeIconViewportAnchorMin = Clamp01(min);
            typeIconViewportAnchorMax = Clamp01(max);
        }

        private void CaptureAuthoredTypeIconViewport()
        {
            if (typeIconScrollRoot == null)
                return;

            typeIconViewportAnchorMin = Clamp01(typeIconScrollRoot.anchorMin);
            typeIconViewportAnchorMax = Clamp01(typeIconScrollRoot.anchorMax);
            typeIconViewportAnchoredPosition = typeIconScrollRoot.anchoredPosition;
            typeIconViewportSizeDelta = typeIconScrollRoot.sizeDelta;
            NormalizeViewportAnchors();
        }

        private void ApplyTypeIconViewport()
        {
            if (typeIconScrollRoot == null)
                return;

            NormalizeViewportAnchors();
            typeIconScrollRoot.anchorMin = typeIconViewportAnchorMin;
            typeIconScrollRoot.anchorMax = typeIconViewportAnchorMax;
            typeIconScrollRoot.anchoredPosition = typeIconViewportAnchoredPosition;
            typeIconScrollRoot.sizeDelta = typeIconViewportSizeDelta;
            typeIconScrollRoot.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Vector2 ResolveDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
                return Vector2.up;

            return direction.normalized;
        }

        private static Vector2 Clamp01(Vector2 value)
        {
            return new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));
        }

        private static bool Approximately(Vector2 lhs, Vector2 rhs)
        {
            return (lhs - rhs).sqrMagnitude <= 0.001f;
        }

        private static Vector2 SanitizeIconSize(Vector2 size)
        {
            return new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        private void EnsureInfoPanelBackgroundReference()
        {
            if (infoPanelBackgroundImage != null || infoPanelGroup == null)
                return;

            Transform backgroundTransform = infoPanelGroup.transform.Find("Background");
            if (backgroundTransform != null)
                infoPanelBackgroundImage = backgroundTransform.GetComponent<Image>();
        }

        private void EnsureBuilt()
        {
            if (button == null)
                button = GetComponent<Button>();
            if (button == null)
                button = gameObject.AddComponent<Button>();

            if (background == null)
            {
                background = GetComponent<Image>();
                if (background == null)
                {
                    background = gameObject.AddComponent<Image>();
                    background.color = new Color(0.07f, 0.095f, 0.14f, 0.92f);
                }
            }

            if (background != null)
            {
                background.raycastTarget = true;
                background.color = TransparentHitTargetColor;
                ConfigureTransparentButtonTarget();
            }

            EnsureTypeBackground();

            if (columnCanvasGroup == null)
                columnCanvasGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(gameObject);

            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform != null && rectTransform.sizeDelta == Vector2.zero)
                rectTransform.sizeDelta = new Vector2(360f, 620f);

            if (roleLabel == null)
            {
                roleLabel = PlayerMenuUiFactory.CreateText("Role", transform, "1", 20f, TextAlignmentOptions.TopLeft, false);
                roleLabel.color = roleLabelColor;
                PlayerMenuUiFactory.Top(roleLabel.rectTransform, 28f, 12f, 12f, 8f);
            }

            if (primaryTypeLabel == null)
            {
                primaryTypeLabel = PlayerMenuUiFactory.CreateText("PrimaryType", transform, "", 18f, TextAlignmentOptions.TopLeft, false);
                primaryTypeLabel.color = PlayerMenuUiFactory.Accent;
                PlayerMenuUiFactory.Top(primaryTypeLabel.rectTransform, 24f, 12f, 12f, 36f);
            }

            if (secondaryTypeLabel == null)
            {
                secondaryTypeLabel = PlayerMenuUiFactory.CreateText("SecondaryType", transform, "", 18f, TextAlignmentOptions.TopLeft, false);
                secondaryTypeLabel.color = PlayerMenuUiFactory.Muted;
                Anchor(secondaryTypeLabel.rectTransform, 0.28f, 0.92f, 0.55f, 0.96f);
            }

            if (secondaryTypeLabel != null)
            {
                secondaryTypeLabel.text = "";
                secondaryTypeLabel.gameObject.SetActive(false);
            }

            if (nameLabel == null)
            {
                nameLabel = PlayerMenuUiFactory.CreateText("Name", transform, "Empty", 26f, TextAlignmentOptions.Top, false);
                PlayerMenuUiFactory.Top(nameLabel.rectTransform, 34f, 12f, 12f, 62f);
            }

            if (visualStage == null)
            {
                visualStage = PlayerMenuUiFactory.CreateRect("VisualStage", transform);
                Anchor(visualStage, 0.08f, 0.38f, 0.92f, 0.72f);
            }

            CaptureBaseScales();

            EnsureVisualStageCanvasGroup();

            if (visualImage == null)
            {
                visualImage = PlayerMenuUiFactory.CreateImage("Visual", visualStage, new Color(0.035f, 0.05f, 0.075f, 1f));
                PlayerMenuUiFactory.Stretch(visualImage.rectTransform, 0f, 0f, 0f, 0f);
                visualImage.preserveAspect = true;
            }

            if (visualPresenter == null && visualImage != null)
            {
                visualPresenter = visualImage.GetComponent<ChainlingMenuVisualPresenter>();
                if (visualPresenter == null)
                    visualPresenter = visualImage.gameObject.AddComponent<ChainlingMenuVisualPresenter>();
            }

            if (emptyPedestalImage == null)
            {
                emptyPedestalImage = PlayerMenuUiFactory.CreateImage("EmptyPedestal", visualStage, Color.white);
                RectTransform pedestalRect = emptyPedestalImage.rectTransform;
                pedestalRect.anchorMin = new Vector2(0.25f, 0.05f);
                pedestalRect.anchorMax = new Vector2(0.75f, 0.35f);
                pedestalRect.offsetMin = Vector2.zero;
                pedestalRect.offsetMax = Vector2.zero;
                emptyPedestalImage.enabled = true;
            }

            if (emptyPedestalAnimator == null)
            {
                emptyPedestalAnimator = emptyPedestalImage.gameObject.AddComponent<PartyTabFrameAnimator>();
                emptyPedestalAnimator.SetFrames(PartyTabPlaceholderArtUtility.GetPedestalFrames());
                emptyPedestalAnimator.SetFPS(6f);
                emptyPedestalAnimator.SetLoop(true);
            }

            EnsureHpBarBuilt();

            if (infoPanelGroup == null)
            {
                RectTransform infoRoot = PlayerMenuUiFactory.CreateRect("InfoPanel", transform);
                Anchor(infoRoot, 0.06f, 0.06f, 0.94f, 0.34f);
                Image infoBackground = PlayerMenuUiFactory.CreateImage("Background", infoRoot, PlayerMenuUiFactory.PanelAlt);
                PlayerMenuUiFactory.Stretch(infoBackground.rectTransform, 0f, 0f, 0f, 0f);
                infoPanelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(infoRoot.gameObject);
                infoPanelBackgroundImage = infoBackground;
            }

            EnsureInfoPanelBackgroundReference();

            if (infoPanelBinder == null)
            {
                infoPanelBinder = infoPanelGroup.GetComponent<ChainlingPartyInfoPanelBinder>();
                if (infoPanelBinder == null)
                    infoPanelBinder = infoPanelGroup.gameObject.AddComponent<ChainlingPartyInfoPanelBinder>();
            }

            EnsureCosmeticLayers();
            RefreshTypeChrome();
            RefreshTypeIcon();
            EnsureColumnDimOverlay();
            EnsureCardChromeOverlayOrder();
        }

        private void CaptureBaseScales()
        {
            if (baseScalesCaptured)
                return;

            columnBaseScale = transform.localScale;
            if (visualStage != null)
                visualStageBaseScale = visualStage.localScale;
            baseScalesCaptured = true;
        }

        private void EnsureTypeBackground()
        {
            if (typeBackgroundImage == null)
            {
                Transform existing = transform.Find("TypeBackground");
                if (existing != null)
                    typeBackgroundImage = existing.GetComponent<Image>();
            }

            if (typeBackgroundImage == null)
                typeBackgroundImage = PlayerMenuUiFactory.CreateImage("TypeBackground", transform, DefaultEmptyBackgroundColor);

            ConfigureStretch(typeBackgroundImage.rectTransform, 0f, 0f, 0f, 0f);
            typeBackgroundImage.raycastTarget = false;
            typeBackgroundImage.type = typeBackgroundImage.sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            typeBackgroundImage.transform.SetAsFirstSibling();
        }

        private void EnsureTypeIconLayer()
        {
            EnsureTypeBackground();
            RectTransform typeBackgroundRect = typeBackgroundImage != null
                ? typeBackgroundImage.rectTransform
                : transform as RectTransform;

            if (typeIconMask == null)
            {
                Transform existing = typeBackgroundRect != null
                    ? typeBackgroundRect.Find("TypeIconMask")
                    : null;
                if (existing == null)
                    existing = transform.Find("TypeIconMask");
                if (existing != null)
                    typeIconMask = existing as RectTransform;
            }

            if (typeIconMask == null)
            {
                typeIconMask = PlayerMenuUiFactory.CreateRect("TypeIconMask", typeBackgroundRect);
                Image maskImage = typeIconMask.gameObject.AddComponent<Image>();
                maskImage.color = Color.white;
                typeIconMask.gameObject.AddComponent<Mask>();
            }
            else if (typeBackgroundRect != null && typeIconMask.parent != typeBackgroundRect)
            {
                typeIconMask.SetParent(typeBackgroundRect, false);
            }

            ConfigureStretch(typeIconMask, 0f, 0f, 0f, 0f);
            RefreshTypeIconMask();

            bool createdScrollRoot = false;
            if (typeIconScrollRoot == null)
            {
                Transform existing = typeIconMask.Find("TypeIconScrollRoot");
                if (existing != null)
                    typeIconScrollRoot = existing as RectTransform;
            }

            if (typeIconScrollRoot == null)
            {
                typeIconScrollRoot = PlayerMenuUiFactory.CreateRect("TypeIconScrollRoot", typeIconMask);
                createdScrollRoot = true;
            }

            bool canMutateViewportTransform = !Application.isPlaying || !IsTypeIconScrollActive();
            if ((typeIconOverrideAuthoredViewport || createdScrollRoot) && canMutateViewportTransform)
                ApplyTypeIconViewport();
            else if (canMutateViewportTransform)
                CaptureAuthoredTypeIconViewport();

            bool createdPrimaryIcon = false;
            if (typeIconPrimaryImage == null)
                typeIconPrimaryImage = FindOrCreateTypeIconImage("PrimaryTypeIcon", typeIconScrollRoot, out createdPrimaryIcon);
            if (typeIconSecondaryImage == null)
            {
                Transform existingSecondary = typeIconScrollRoot != null
                    ? typeIconScrollRoot.Find("SecondaryTypeIcon")
                    : null;
                typeIconSecondaryImage = existingSecondary != null ? existingSecondary.GetComponent<Image>() : null;
            }

            if (typeIconOverrideAuthoredIconRects || createdPrimaryIcon)
                ConfigurePrimaryTypeIconRect(typeIconPrimaryImage != null ? typeIconPrimaryImage.rectTransform : null);
            DisableSecondaryTypeIcon();

            typeIconMask.SetAsLastSibling();
        }

        private Image FindOrCreateTypeIconImage(string imageName, Transform parent, out bool created)
        {
            created = false;
            Transform existing = parent != null ? parent.Find(imageName) : null;
            Image image = existing != null ? existing.GetComponent<Image>() : null;
            if (image != null)
                return image;

            image = PlayerMenuUiFactory.CreateImage(imageName, parent, new Color(1f, 1f, 1f, typeIconAlpha));
            image.raycastTarget = false;
            image.preserveAspect = true;
            created = true;
            return image;
        }

        private bool IsTypeIconScrollActive()
        {
            return typeIconScrollTween != null && typeIconScrollTween.IsActive();
        }

        private void ConfigurePrimaryTypeIconRect(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return;

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = SanitizeIconSize(typeIconSize);
        }

        private static void ConfigureSecondaryTypeIconRect(
            RectTransform secondaryRect,
            RectTransform primaryRect,
            Vector2 scrollVector)
        {
            if (secondaryRect == null || primaryRect == null)
                return;

            secondaryRect.anchorMin = primaryRect.anchorMin;
            secondaryRect.anchorMax = primaryRect.anchorMax;
            secondaryRect.pivot = primaryRect.pivot;
            secondaryRect.anchoredPosition = primaryRect.anchoredPosition - scrollVector;
            secondaryRect.sizeDelta = primaryRect.sizeDelta;
        }

        private void EnsureCardChromeOverlayOrder()
        {
            if (archiveIndicatorImage != null)
                archiveIndicatorImage.transform.SetAsLastSibling();

            if (entangledBadgeLabel != null)
                entangledBadgeLabel.transform.SetAsLastSibling();

            if (cardChromeImage != null)
                cardChromeImage.transform.SetAsLastSibling();

            if (columnDimOverlay != null)
                columnDimOverlay.transform.SetAsLastSibling();
        }

        private void ConfigureTransparentButtonTarget()
        {
            if (button == null || background == null)
                return;

            button.targetGraphic = background;
            ColorBlock colors = button.colors;
            colors.normalColor = TransparentHitTargetColor;
            colors.highlightedColor = TransparentHitTargetColor;
            colors.pressedColor = TransparentHitTargetColor;
            colors.selectedColor = TransparentHitTargetColor;
            colors.disabledColor = TransparentHitTargetColor;
            button.colors = colors;
        }

        private void EnsureVisualStageCanvasGroup()
        {
            if (visualStage == null)
                return;

            if (visualStageCanvasGroup == null)
                visualStageCanvasGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(visualStage.gameObject);

            visualStageCanvasGroup.interactable = true;
            visualStageCanvasGroup.blocksRaycasts = false;
        }

        private void EnsureColumnDimOverlay()
        {
            if (columnDimOverlay != null)
                return;

            Transform existing = transform.Find("ColumnDimOverlay");
            if (existing != null)
            {
                columnDimOverlay = existing.GetComponent<Image>();
                if (columnDimOverlay != null)
                {
                    columnDimOverlay.transform.SetAsLastSibling();
                    return;
                }
            }

            columnDimOverlay = PlayerMenuUiFactory.CreateImage("ColumnDimOverlay", transform, Color.black);
            ConfigureStretch(columnDimOverlay.rectTransform, 0f, 0f, 0f, 0f);
            columnDimOverlay.raycastTarget = false;
            columnDimOverlay.color = new Color(0f, 0f, 0f, 0f);
            columnDimOverlay.enabled = false;
            columnDimOverlay.transform.SetAsLastSibling();
        }

        private void EnsureHpBarBuilt()
        {
            if (hpBarBackground == null)
            {
                hpBarBackground = PlayerMenuUiFactory.CreateImage("HpBarBackground", transform, new Color(0.08f, 0.12f, 0.1f, 1f));
                Anchor(hpBarBackground.rectTransform, 0.08f, 0.35f, 0.92f, 0.37f);
            }

            if (hpBarFill == null)
            {
                hpBarFill = PlayerMenuUiFactory.CreateImage("HpBarFill", hpBarBackground.transform, new Color(0.22f, 0.78f, 0.34f, 1f));
                PlayerMenuUiFactory.Stretch(hpBarFill.rectTransform, 0f, 0f, 0f, 0f);
            }
            PartyTabProgressBarUtility.ConfigureRectFill(hpBarFill);

            if (hpLabel == null)
            {
                hpLabel = PlayerMenuUiFactory.CreateText("HpLabel", transform, "—", 17f, TextAlignmentOptions.MidlineRight, false);
                hpLabel.color = new Color(0.82f, 0.96f, 0.86f, 1f);
                Anchor(hpLabel.rectTransform, 0.08f, 0.35f, 0.92f, 0.37f);
            }
        }

        private static void ConfigureStretch(RectTransform rectTransform, float left, float bottom, float right, float top)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = new Vector2(left, bottom);
            rectTransform.offsetMax = new Vector2(-right, -top);
        }

        private static void Anchor(RectTransform rectTransform, float left, float bottom, float right, float top)
        {
            rectTransform.anchorMin = new Vector2(left, bottom);
            rectTransform.anchorMax = new Vector2(right, top);
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }

        private static string ResolveDisplayName(ChainlingSnapshot snapshot, SpeciesDefinitionSO species)
        {
            if (snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName))
                return snapshot.displayName;
            if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                return species.displayName;
            return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.speciesId) ? snapshot.speciesId : "Chainling";
        }
    }
}
