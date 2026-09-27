using System;
using System.Collections.Generic;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public class ChainlingPartyViewRail : MonoBehaviour
    {
        private const int VisibleSideRows = 2;

        private static readonly ChainlingPartyViewMode[] Modes =
        {
            ChainlingPartyViewMode.General,
            ChainlingPartyViewMode.Attributes,
            ChainlingPartyViewMode.Moves,
            ChainlingPartyViewMode.Detail,
            ChainlingPartyViewMode.Loadout,
            ChainlingPartyViewMode.Stats,
        };

        [Header("Collapsed View")]
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private CanvasGroup promptGroup;
        [SerializeField] private Button collapsedButton;
        [SerializeField] private CanvasGroup collapsedButtonGroup;
        [SerializeField] private TMP_Text collapsedButtonLabel;
        [SerializeField] private TMP_Text collapsedModeLabel;

        [Header("Prompt Motion")]
        [SerializeField] private float promptClosedY = 86f;
        [SerializeField] private float promptCarouselY = 275f;
        [SerializeField] private float promptDriftOvershoot = 10f;
        [SerializeField] private float promptDriftOvershootDuration = 0.14f;
        [SerializeField] private float promptDriftSettleDuration = 0.2f;

        [Header("Carousel")]
        [SerializeField] private RectTransform expandedRoot;
        [SerializeField] private CanvasGroup expandedGroup;
        [SerializeField] private RectTransform modeListRoot;
        [SerializeField] private Button modeButtonTemplate;
        [SerializeField] private PartyTabMotionProfileSO motionProfile;

        [Header("Carousel Visuals")]
        [SerializeField] private Image collapsedButtonImage;
        [SerializeField] private Sprite collapsedButtonSprite;
        [SerializeField] private Sprite carouselButtonSprite;
        [SerializeField] private Sprite carouselButtonSelectedSprite;
        [SerializeField] private Color collapsedButtonTint = Color.white;
        [SerializeField] private Color carouselButtonTint = Color.white;
        [SerializeField] private Color carouselButtonSelectedTint = Color.white;
        [SerializeField] private Color carouselDistanceOverlayColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float carouselCenterOverlayAlpha;
        [SerializeField, Range(0f, 1f)] private float carouselNearOverlayAlpha = 0.22f;
        [SerializeField, Range(0f, 1f)] private float carouselFarOverlayAlpha = 0.45f;
        [SerializeField] private bool enableCarouselShimmer = true;
        [SerializeField] private float carouselShimmerFps = 12f;
        [SerializeField] private List<Sprite> carouselShimmerFrames = new List<Sprite>();

        [Header("Carousel Motion")]
        [SerializeField] private float carouselRowSpacing = 70f;
        [SerializeField] private float carouselCenterScale = 1.08f;
        [SerializeField] private float carouselNearScale = 0.96f;
        [SerializeField] private float carouselFarScale = 0.92f;
        [SerializeField] private float carouselNearAlpha = 0.65f;
        [SerializeField] private float carouselFarAlpha = 0.5f;
        [SerializeField] private float carouselRotateDuration = 0.18f;
        [SerializeField] private float carouselFadeDuration = 0.28f;
        [SerializeField] private float carouselRowStagger = 0.035f;
        [SerializeField] private float cushionOvershoot = 0.05f;
        [SerializeField] private float cushionOvershootDuration = 0.14f;
        [SerializeField] private float cushionSettleDuration = 0.2f;

        private readonly List<Button> modeButtons = new List<Button>();
        private readonly List<CanvasGroup> modeRowGroups = new List<CanvasGroup>();
        private readonly List<Image> modeButtonImages = new List<Image>();
        private readonly List<Image> modeDistanceOverlays = new List<Image>();
        private readonly List<bool> lastCarouselVisibleRows = new List<bool>();
        private readonly List<PartyTabFrameAnimator> shimmerAnimators = new List<PartyTabFrameAnimator>();
        private readonly List<Image> shimmerImages = new List<Image>();
        private readonly List<Tween> activeTweens = new List<Tween>();
        private ChainlingPartyViewMode activeMode = ChainlingPartyViewMode.General;
        private ChainlingPartyViewMode focusedMode = ChainlingPartyViewMode.General;
        private bool isArmed;
        private bool isCarouselOpen;
        private Action<ChainlingPartyViewMode> onModeSelected;
        private Action onOpenRequested;
        private Action onCloseRequested;

        public event Action<ChainlingPartyViewMode> ViewModeChanged;

        public ChainlingPartyViewMode ActiveMode => activeMode;
        public ChainlingPartyViewMode FocusedMode => focusedMode;
        public bool IsOpen => isCarouselOpen;
        public bool IsCarouselOpen => isCarouselOpen;
        public bool IsArmed => isArmed;
        public Button CollapsedButton => collapsedButton;
        public IReadOnlyList<Button> ModeButtons => modeButtons;
        public bool HasAuthoredModeButtons => modeButtons.Count >= Modes.Length;

        public void Configure(
            PartyTabMotionProfileSO profile,
            Action<ChainlingPartyViewMode> modeSelectedHandler,
            Action openRequestedHandler,
            Action closeRequestedHandler)
        {
            if (profile != null)
                motionProfile = profile;
            onModeSelected = modeSelectedHandler;
            onOpenRequested = openRequestedHandler;
            onCloseRequested = closeRequestedHandler;
        }

        public void ConfigureCarouselVisuals(
            Sprite collapsedSprite,
            Sprite normalSprite,
            Sprite selectedSprite,
            IReadOnlyList<Sprite> shimmerFrameSprites)
        {
            collapsedButtonSprite = collapsedSprite;
            carouselButtonSprite = normalSprite;
            carouselButtonSelectedSprite = selectedSprite;

            carouselShimmerFrames.Clear();
            if (shimmerFrameSprites != null)
            {
                for (int i = 0; i < shimmerFrameSprites.Count; i++)
                {
                    if (shimmerFrameSprites[i] != null)
                        carouselShimmerFrames.Add(shimmerFrameSprites[i]);
                }
            }

            RefreshRuntimeState();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            WireModeButtons();
            SetArmed(false, false);
        }

        /// <summary>
        /// Rebinds runtime handlers without rebuilding authored hierarchy.
        /// </summary>
        public void RefreshRuntimeState()
        {
            EnsureBuilt();
            WireModeButtons();
            RefreshCollapsedLabel();
            RefreshPromptLabel(false);
            RefreshCarouselVisuals(false, false);
        }

        public void SetMode(ChainlingPartyViewMode mode, bool notify)
        {
            activeMode = mode;
            focusedMode = mode;
            RefreshCollapsedLabel();
            RefreshCarouselVisuals(false, false);

            if (notify)
                NotifyModeSelected(activeMode);
        }

        public void SetArmed(bool armed, bool animate)
        {
            EnsureBuilt();
            WireModeButtons();

            isArmed = armed;
            RefreshPromptLabel(animate);

            if (!isArmed && isCarouselOpen)
            {
                SetCarouselOpen(false, animate, playConfirmBounce: false);
                return;
            }

            SetCanvasGroupVisible(promptGroup, true, animate, carouselFadeDuration);
            SetCanvasGroupVisible(collapsedButtonGroup, !isCarouselOpen, animate, carouselFadeDuration);
            if (!isCarouselOpen)
                SetCarouselVisible(false, animate);
        }

        public void SetOpen(bool open, bool animate)
        {
            if (open)
            {
                SetArmed(true, animate);
                OpenCarousel(animate);
            }
            else
            {
                SetArmed(false, animate);
            }
        }

        public void OpenCarousel(bool animate)
        {
            EnsureBuilt();
            WireModeButtons();
            SetArmed(true, false);
            focusedMode = activeMode;
            RefreshCarouselVisuals(false, false);
            SetCarouselOpen(true, animate, playConfirmBounce: false);
        }

        public bool MoveCarousel(int delta, bool animate)
        {
            if (!isCarouselOpen || delta == 0)
                return false;

            int currentIndex = ModeIndex(focusedMode);
            int nextIndex = WrapIndex(currentIndex + delta);
            focusedMode = Modes[nextIndex];
            activeMode = focusedMode;
            RefreshCollapsedLabel();
            RefreshCarouselVisuals(animate, true);
            NotifyModeSelected(activeMode);
            return true;
        }

        public bool ConfirmCarousel(bool animate)
        {
            if (!isCarouselOpen)
                return false;

            activeMode = focusedMode;
            RefreshCollapsedLabel();
            SetCarouselOpen(false, animate, playConfirmBounce: true);
            onCloseRequested?.Invoke();
            return true;
        }

        public Button GetModeButton(ChainlingPartyViewMode mode)
        {
            int index = ModeIndex(mode);
            if (index < 0 || index >= modeButtons.Count)
                return null;
            return modeButtons[index];
        }

        private void Awake()
        {
            EnsureBuilt();
            RefreshRuntimeState();
        }

        private void OnEnable()
        {
            if (collapsedButton != null)
                collapsedButton.onClick.AddListener(HandleCollapsedClicked);
        }

        private void OnDisable()
        {
            if (collapsedButton != null)
                collapsedButton.onClick.RemoveListener(HandleCollapsedClicked);
            KillTweens();
        }

        private void HandleCollapsedClicked()
        {
            onOpenRequested?.Invoke();
        }

        private void SetCarouselOpen(bool open, bool animate, bool playConfirmBounce)
        {
            isCarouselOpen = open;

            if (open)
            {
                if (expandedRoot != null)
                    expandedRoot.gameObject.SetActive(true);
                SetCarouselVisible(true, animate);
                MovePromptForCarousel(true, animate);
                PlayCushion(promptGroup != null ? promptGroup.transform : null);
                PlayCushion(collapsedButtonGroup != null ? collapsedButtonGroup.transform : null);
                SetCanvasGroupVisible(promptGroup, true, animate, carouselFadeDuration);
                SetCanvasGroupVisible(collapsedButtonGroup, false, animate, carouselFadeDuration);
                RefreshCarouselVisuals(animate, true);
                return;
            }

            if (playConfirmBounce)
                PlayCushion(GetModeButton(focusedMode) != null ? GetModeButton(focusedMode).transform : null);

            SetCarouselVisible(false, animate);
            isArmed = false;
            RefreshPromptLabel(false);
            MovePromptForCarousel(false, animate);
            SetCanvasGroupVisible(promptGroup, true, animate, carouselFadeDuration);
            SetCanvasGroupVisible(collapsedButtonGroup, true, animate, carouselFadeDuration);
        }

        private void EnsureBuilt()
        {
            EnsureCollapsedButton();
            EnsurePrompt();
            EnsureExpandedRoot();
            EnsureModeListRoot();
            EnsureModeButtonTemplate();
        }

        private void EnsureCollapsedButton()
        {
            if (collapsedButton == null)
            {
                collapsedButton = PlayerMenuUiFactory.CreateButton("ChangeViewButton", transform, "General", PlayerMenuUiFactory.PanelAlt);
                RectTransform collapsedRect = collapsedButton.GetComponent<RectTransform>();
                collapsedRect.anchorMin = new Vector2(0.5f, 0.5f);
                collapsedRect.anchorMax = new Vector2(0.5f, 0.5f);
                collapsedRect.pivot = new Vector2(0.5f, 0.5f);
                collapsedRect.anchoredPosition = Vector2.zero;
                collapsedRect.sizeDelta = new Vector2(275f, 100f);
            }

            if (collapsedButtonGroup == null)
                collapsedButtonGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(collapsedButton.gameObject);

            if (collapsedButtonImage == null)
                collapsedButtonImage = collapsedButton.GetComponent<Image>();
            ApplySprite(collapsedButtonImage, collapsedButtonSprite, collapsedButtonTint);
            RemoveFocusVisual(collapsedButton);

            if (collapsedButtonLabel == null)
                collapsedButtonLabel = FindButtonLabel(collapsedButton, collapsedModeLabel);

            if (collapsedModeLabel == null)
            {
                Transform currentMode = collapsedButton.transform.Find("CurrentMode");
                if (currentMode != null)
                    collapsedModeLabel = currentMode.GetComponent<TMP_Text>();
            }

            if (collapsedModeLabel != null && collapsedModeLabel != collapsedButtonLabel)
                collapsedModeLabel.gameObject.SetActive(false);

            RefreshCollapsedLabel();
        }

        private void EnsurePrompt()
        {
            if (promptLabel == null)
            {
                Transform prompt = transform.Find("ViewPrompt");
                if (prompt != null)
                    promptLabel = prompt.GetComponent<TMP_Text>();
            }

            if (promptLabel == null)
            {
                promptLabel = PlayerMenuUiFactory.CreateText("ViewPrompt", transform, "Party View", 24f, TextAlignmentOptions.Center, false);
                RectTransform rect = promptLabel.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, 86f);
                rect.sizeDelta = new Vector2(275f, 36f);
                promptLabel.color = PlayerMenuUiFactory.Text;
            }

            if (promptGroup == null)
                promptGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(promptLabel.gameObject);
        }

        private void EnsureExpandedRoot()
        {
            if (expandedRoot == null)
            {
                Transform existing = transform.Find("ExpandedList");
                if (existing != null)
                    expandedRoot = existing as RectTransform;
            }

            if (expandedRoot == null)
            {
                expandedRoot = PlayerMenuUiFactory.CreateRect("ExpandedList", transform);
                expandedRoot.anchorMin = new Vector2(0.5f, 0.5f);
                expandedRoot.anchorMax = new Vector2(0.5f, 0.5f);
                expandedRoot.pivot = new Vector2(0.5f, 0.5f);
                expandedRoot.anchoredPosition = Vector2.zero;
                expandedRoot.sizeDelta = new Vector2(300f, 430f);
            }

            expandedGroup = expandedGroup != null
                ? expandedGroup
                : PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(expandedRoot.gameObject);
        }

        private void EnsureModeListRoot()
        {
            if (modeListRoot == null)
            {
                Transform existing = expandedRoot != null ? expandedRoot.Find("ModeList") : null;
                if (existing != null)
                    modeListRoot = existing as RectTransform;
            }

            if (modeListRoot == null)
                modeListRoot = PlayerMenuUiFactory.CreateRect("ModeList", expandedRoot);

            PlayerMenuUiFactory.Stretch(modeListRoot, 0f, 0f, 0f, 0f);
            VerticalLayoutGroup layout = modeListRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;
        }

        private void EnsureModeButtonTemplate()
        {
            if (modeButtonTemplate == null && modeListRoot != null)
            {
                Transform existing = modeListRoot.Find("ModeTemplate");
                if (existing != null)
                    modeButtonTemplate = existing.GetComponent<Button>();
            }

            if (modeButtonTemplate == null)
            {
                modeButtonTemplate = PlayerMenuUiFactory.CreateButton("ModeTemplate", modeListRoot, "General", PlayerMenuUiFactory.Panel);
                RectTransform templateRect = modeButtonTemplate.GetComponent<RectTransform>();
                templateRect.sizeDelta = new Vector2(292f, 82f);
                modeButtonTemplate.gameObject.SetActive(false);
            }
        }

        private void WireModeButtons()
        {
            EnsureBuilt();
            CacheAuthoredModeButtonsFromHierarchy();

            for (int i = 0; i < Modes.Length; i++)
            {
                if (i < modeButtons.Count && modeButtons[i] != null)
                    continue;

                Button clone = Instantiate(modeButtonTemplate, modeListRoot);
                clone.gameObject.SetActive(true);
                RegisterModeButton(i, clone);
            }

            RefreshCarouselVisuals(false, false);
        }

        private void CacheAuthoredModeButtonsFromHierarchy()
        {
            if (modeListRoot == null)
                return;

            for (int i = 0; i < Modes.Length; i++)
            {
                if (i < modeButtons.Count && modeButtons[i] != null)
                    continue;

                Transform child = modeListRoot.Find($"Mode_{Modes[i]}");
                if (child == null)
                    continue;

                Button button = child.GetComponent<Button>();
                if (button == null)
                    continue;

                RegisterModeButton(i, button);
            }
        }

        private void EnsureModeButtonSlots()
        {
            while (modeButtons.Count < Modes.Length)
            {
                modeButtons.Add(null);
                modeRowGroups.Add(null);
                modeButtonImages.Add(null);
                modeDistanceOverlays.Add(null);
                lastCarouselVisibleRows.Add(false);
                shimmerAnimators.Add(null);
                shimmerImages.Add(null);
            }
        }

        private void RegisterModeButton(int index, Button button)
        {
            if (button == null || index < 0 || index >= Modes.Length)
                return;

            EnsureModeButtonSlots();
            if (modeButtons[index] == button)
                return;

            button.gameObject.SetActive(true);
            button.name = $"Mode_{Modes[index]}";
            RectTransform rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(292f, 82f);
            }

            TMP_Text label = FindButtonLabel(button, null);
            if (label != null)
            {
                label.text = ChainlingPartyViewModeLabels.GetDisplayName(Modes[index]);
                ApplyCollapsedLabelStyle(label);
            }

            CanvasGroup rowGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(button.gameObject);
            Image buttonImage = button.GetComponent<Image>();
            ApplySprite(buttonImage, carouselButtonSprite, carouselButtonTint);

            Image distanceOverlay = EnsureDistanceOverlay(button);
            modeButtons[index] = button;
            modeRowGroups[index] = rowGroup;
            modeButtonImages[index] = buttonImage;
            modeDistanceOverlays[index] = distanceOverlay;
            RemoveFocusVisual(button);

            Image shimmerImage = button.transform.Find("Shimmer")?.GetComponent<Image>();
            PartyTabFrameAnimator shimmer = shimmerImage != null
                ? shimmerImage.GetComponent<PartyTabFrameAnimator>()
                : null;
            if (shimmerImage == null)
            {
                shimmerImage = PlayerMenuUiFactory.CreateImage("Shimmer", button.transform, Color.white);
                PlayerMenuUiFactory.Stretch(shimmerImage.rectTransform, 0f, 0f, 0f, 0f);
                shimmerImage.raycastTarget = false;
                shimmerImage.enabled = false;
            }
            shimmerImage.color = Color.white;
            shimmerImage.raycastTarget = false;
            shimmerImage.transform.SetAsFirstSibling();

            if (shimmer == null)
            {
                shimmer = shimmerImage.gameObject.AddComponent<PartyTabFrameAnimator>();
                shimmer.SetFPS(10f);
                shimmer.SetLoop(true);
            }

            shimmer.SetFrames(ResolveShimmerFrames());
            shimmer.SetFPS(carouselShimmerFps);
            shimmer.SetLoop(true);

            shimmerAnimators[index] = shimmer;
            shimmerImages[index] = shimmerImage;

            button.onClick.RemoveAllListeners();
            ChainlingPartyViewMode captured = Modes[index];
            button.onClick.AddListener(() => HandleModeClicked(captured));
        }

        private void HandleModeClicked(ChainlingPartyViewMode mode)
        {
            if (!isCarouselOpen)
            {
                SetMode(mode, true);
                return;
            }

            if (mode == focusedMode)
            {
                ConfirmCarousel(true);
                return;
            }

            int delta = ResolveShortestDelta(ModeIndex(focusedMode), ModeIndex(mode));
            MoveCarousel(delta, true);
        }

        private void RefreshCollapsedLabel()
        {
            string displayName = ChainlingPartyViewModeLabels.GetDisplayName(activeMode);
            if (collapsedButtonLabel != null)
                collapsedButtonLabel.text = displayName;
            if (collapsedModeLabel != null && collapsedModeLabel == collapsedButtonLabel)
                collapsedModeLabel.text = displayName;
        }

        private void RefreshPromptLabel(bool animate)
        {
            if (promptLabel == null)
                return;

            string targetText = isArmed ? "Change View" : "Party View";
            if (promptLabel.text == targetText)
                return;

            if (!animate || !Application.isPlaying || promptGroup == null)
            {
                promptLabel.text = targetText;
                return;
            }

            promptGroup.DOKill();
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(promptGroup.DOFade(0f, 0.09f).SetEase(Ease.OutSine));
            sequence.AppendCallback(() => promptLabel.text = targetText);
            sequence.Append(promptGroup.DOFade(1f, 0.09f).SetEase(Ease.InOutSine));
            Track(sequence);
        }

        private void RefreshCarouselVisuals(bool animate, bool stagger)
        {
            for (int i = 0; i < modeButtons.Count && i < Modes.Length; i++)
            {
                Button modeButton = modeButtons[i];
                if (modeButton == null)
                    continue;

                int offset = ResolveCarouselOffset(i, ModeIndex(focusedMode));
                int distance = Mathf.Abs(offset);
                bool visibleRow = isCarouselOpen && distance <= VisibleSideRows;
                bool wasVisibleRow = i < lastCarouselVisibleRows.Count && lastCarouselVisibleRows[i];
                bool animateRow = animate && Application.isPlaying && (visibleRow || wasVisibleRow);
                float targetScale = ResolveCarouselScale(distance);
                float targetAlpha = visibleRow ? ResolveCarouselAlpha(distance) : 0f;
                Vector2 targetPosition = new Vector2(0f, -offset * carouselRowSpacing);

                RectTransform rect = modeButton.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.DOKill();
                    if (animateRow)
                    {
                        float delay = stagger ? Mathf.Min(Mathf.Abs(offset), VisibleSideRows) * carouselRowStagger : 0f;
                        Track(rect.DOAnchorPos(targetPosition, carouselRotateDuration)
                            .SetDelay(delay)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                        Track(rect.DOScale(Vector3.one * targetScale, carouselRotateDuration)
                            .SetDelay(delay)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                    }
                    else
                    {
                        rect.anchoredPosition = targetPosition;
                        rect.localScale = Vector3.one * targetScale;
                    }
                }

                if (i < modeRowGroups.Count && modeRowGroups[i] != null)
                {
                    CanvasGroup rowGroup = modeRowGroups[i];
                    rowGroup.DOKill();
                    rowGroup.interactable = visibleRow;
                    rowGroup.blocksRaycasts = visibleRow;
                    if (animateRow)
                    {
                        float delay = stagger ? Mathf.Min(Mathf.Abs(offset), VisibleSideRows) * carouselRowStagger : 0f;
                        Track(rowGroup.DOFade(targetAlpha, carouselRotateDuration)
                            .SetDelay(delay)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                    }
                    else
                    {
                        rowGroup.alpha = targetAlpha;
                    }
                }

                if (i < modeDistanceOverlays.Count && modeDistanceOverlays[i] != null)
                    RefreshDistanceOverlay(
                        modeDistanceOverlays[i],
                        distance,
                        visibleRow,
                        animateRow,
                        stagger ? Mathf.Min(Mathf.Abs(offset), VisibleSideRows) * carouselRowStagger : 0f);

                RefreshButtonSelectedVisual(i, visibleRow && distance == 0);
                if (i < lastCarouselVisibleRows.Count)
                    lastCarouselVisibleRows[i] = visibleRow;
            }
        }

        private void RefreshButtonSelectedVisual(int index, bool selected)
        {
            if (index < 0 || index >= modeButtons.Count || modeButtons[index] == null)
                return;

            Image image = index < modeButtonImages.Count ? modeButtonImages[index] : modeButtons[index].GetComponent<Image>();
            if (image != null)
            {
                Sprite targetSprite = selected && carouselButtonSelectedSprite != null
                    ? carouselButtonSelectedSprite
                    : carouselButtonSprite;
                Color targetColor = selected ? carouselButtonSelectedTint : carouselButtonTint;
                ApplySprite(image, targetSprite, targetColor);
            }

            if (index >= shimmerAnimators.Count)
                return;

            bool playShimmer = enableCarouselShimmer &&
                selected &&
                isCarouselOpen &&
                carouselShimmerFrames != null &&
                carouselShimmerFrames.Count > 0;
            if (index < shimmerImages.Count && shimmerImages[index] != null)
                shimmerImages[index].enabled = playShimmer;

            if (playShimmer)
                shimmerAnimators[index]?.Play();
            else
                shimmerAnimators[index]?.Stop();
        }

        private Image EnsureDistanceOverlay(Button button)
        {
            if (button == null)
                return null;

            Transform existing = button.transform.Find("DistanceDimOverlay");
            Image overlay = existing != null ? existing.GetComponent<Image>() : null;
            if (overlay == null)
            {
                overlay = PlayerMenuUiFactory.CreateImage("DistanceDimOverlay", button.transform, Color.clear);
                PlayerMenuUiFactory.Stretch(overlay.rectTransform, 0f, 0f, 0f, 0f);
            }

            overlay.raycastTarget = false;
            overlay.color = WithAlpha(carouselDistanceOverlayColor, 0f);
            overlay.transform.SetAsFirstSibling();
            return overlay;
        }

        private void RefreshDistanceOverlay(Image overlay, int distance, bool visibleRow, bool animate, float delay)
        {
            if (overlay == null)
                return;

            float targetAlpha = visibleRow ? ResolveCarouselOverlayAlpha(distance) : 0f;
            Color targetColor = WithAlpha(carouselDistanceOverlayColor, targetAlpha);
            overlay.DOKill();
            if (animate && Application.isPlaying)
            {
                Track(overlay.DOColor(targetColor, carouselRotateDuration)
                    .SetDelay(delay)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
            }
            else
            {
                overlay.color = targetColor;
            }
        }

        private IReadOnlyList<Sprite> ResolveShimmerFrames()
        {
            if (carouselShimmerFrames != null && carouselShimmerFrames.Count > 0)
                return carouselShimmerFrames;
            return Array.Empty<Sprite>();
        }

        private void ApplyCollapsedLabelStyle(TMP_Text label)
        {
            if (label == null || collapsedButtonLabel == null)
                return;

            label.font = collapsedButtonLabel.font;
            label.fontSharedMaterial = collapsedButtonLabel.fontSharedMaterial;
            label.fontSize = collapsedButtonLabel.fontSize;
            label.enableAutoSizing = collapsedButtonLabel.enableAutoSizing;
            label.fontSizeMin = collapsedButtonLabel.fontSizeMin;
            label.fontSizeMax = collapsedButtonLabel.fontSizeMax;
            label.fontStyle = collapsedButtonLabel.fontStyle;
            label.fontWeight = collapsedButtonLabel.fontWeight;
            label.alignment = collapsedButtonLabel.alignment;
            label.color = collapsedButtonLabel.color;
            label.enableWordWrapping = collapsedButtonLabel.enableWordWrapping;
            label.overflowMode = collapsedButtonLabel.overflowMode;
            label.characterSpacing = collapsedButtonLabel.characterSpacing;
            label.wordSpacing = collapsedButtonLabel.wordSpacing;
            label.lineSpacing = collapsedButtonLabel.lineSpacing;
            label.paragraphSpacing = collapsedButtonLabel.paragraphSpacing;
            label.margin = collapsedButtonLabel.margin;
            label.richText = collapsedButtonLabel.richText;
        }

        private static void ApplySprite(Image image, Sprite sprite, Color tint)
        {
            if (image == null)
                return;

            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = HasSpriteBorder(sprite) ? Image.Type.Sliced : Image.Type.Simple;
            }

            image.color = tint;
        }

        private static bool HasSpriteBorder(Sprite sprite)
        {
            if (sprite == null)
                return false;

            Vector4 border = sprite.border;
            return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void RemoveFocusVisual(Selectable selectable)
        {
            if (selectable == null)
                return;

            MenuSelectableFocusVisual focusVisual = selectable.GetComponent<MenuSelectableFocusVisual>();
            if (focusVisual != null)
                DestroyAuthoringObject(focusVisual);

            Transform focusFrame = selectable.transform.Find("FocusFrame");
            if (focusFrame != null)
                DestroyAuthoringObject(focusFrame.gameObject);
        }

        private static void DestroyAuthoringObject(UnityEngine.Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private void SetCarouselVisible(bool visible, bool animate)
        {
            if (expandedRoot != null)
                expandedRoot.gameObject.SetActive(true);

            if (expandedGroup != null)
            {
                expandedGroup.DOKill();
                expandedGroup.interactable = visible;
                expandedGroup.blocksRaycasts = visible;
                if (animate && Application.isPlaying)
                {
                    Track(expandedGroup.DOFade(visible ? 1f : 0f, carouselFadeDuration)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            if (!visible && expandedRoot != null)
                                expandedRoot.gameObject.SetActive(false);
                        }));
                }
                else
                {
                    expandedGroup.alpha = visible ? 1f : 0f;
                    if (!visible && expandedRoot != null)
                        expandedRoot.gameObject.SetActive(false);
                }
            }

            RefreshCarouselVisuals(animate, visible);
        }

        private void SetCanvasGroupVisible(CanvasGroup group, bool visible, bool animate, float duration)
        {
            if (group == null)
                return;

            group.DOKill();
            group.interactable = visible;
            group.blocksRaycasts = visible;
            if (animate && Application.isPlaying)
            {
                Track(group.DOFade(visible ? 1f : 0f, duration)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true));
            }
            else
            {
                group.alpha = visible ? 1f : 0f;
            }
        }

        private void MovePromptForCarousel(bool carouselOpen, bool animate)
        {
            if (promptLabel == null)
                return;

            RectTransform promptRect = promptLabel.rectTransform;
            if (promptRect == null)
                return;

            float targetY = carouselOpen ? promptCarouselY : promptClosedY;
            promptRect.DOKill();

            if (!animate || !Application.isPlaying)
            {
                promptRect.anchoredPosition = new Vector2(promptRect.anchoredPosition.x, targetY);
                return;
            }

            float direction = Mathf.Sign(targetY - promptRect.anchoredPosition.y);
            if (Mathf.Approximately(direction, 0f))
                direction = carouselOpen ? 1f : -1f;

            float overshootY = targetY + direction * Mathf.Max(0f, promptDriftOvershoot);
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(promptRect.DOAnchorPosY(overshootY, promptDriftOvershootDuration)
                .SetEase(Ease.OutSine));
            sequence.Append(promptRect.DOAnchorPosY(targetY, promptDriftSettleDuration)
                .SetEase(Ease.InOutSine));
            Track(sequence);
        }

        private void PlayCushion(Transform target)
        {
            if (target == null || !Application.isPlaying)
                return;

            target.DOKill();
            Vector3 baseScale = ResolveTargetScale(target);
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(target.DOScale(baseScale * (1f + Mathf.Max(0f, cushionOvershoot)), cushionOvershootDuration)
                .SetEase(Ease.OutSine));
            sequence.Append(target.DOScale(baseScale, cushionSettleDuration)
                .SetEase(Ease.InOutSine));
            Track(sequence);
        }

        private Vector3 ResolveTargetScale(Transform target)
        {
            if (target is RectTransform rect && target.parent == modeListRoot)
            {
                int modeIndex = -1;
                for (int i = 0; i < modeButtons.Count; i++)
                {
                    if (modeButtons[i] != null && modeButtons[i].transform == target)
                    {
                        modeIndex = i;
                        break;
                    }
                }

                if (modeIndex >= 0)
                    return Vector3.one * ResolveCarouselScale(Mathf.Abs(ResolveCarouselOffset(modeIndex, ModeIndex(focusedMode))));
            }

            return Vector3.one;
        }

        private void NotifyModeSelected(ChainlingPartyViewMode mode)
        {
            onModeSelected?.Invoke(mode);
            ViewModeChanged?.Invoke(mode);
        }

        private static TMP_Text FindButtonLabel(Button button, TMP_Text excluded)
        {
            if (button == null)
                return null;

            TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i] != excluded && labels[i].transform.name == "Label")
                    return labels[i];
            }

            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i] != excluded)
                    return labels[i];
            }

            return excluded;
        }

        private int ModeIndex(ChainlingPartyViewMode mode)
        {
            int index = Array.IndexOf(Modes, mode);
            return index >= 0 ? index : 0;
        }

        private static int WrapIndex(int index)
        {
            int count = Modes.Length;
            if (count <= 0)
                return 0;

            while (index < 0)
                index += count;
            return index % count;
        }

        private static int ResolveShortestDelta(int fromIndex, int toIndex)
        {
            int count = Modes.Length;
            int delta = toIndex - fromIndex;
            if (delta > count / 2)
                delta -= count;
            if (delta < -count / 2)
                delta += count;
            return delta;
        }

        private static int ResolveCarouselOffset(int index, int focusedIndex)
        {
            int count = Modes.Length;
            int offset = index - focusedIndex;
            if (offset > count / 2)
                offset -= count;
            if (offset < -count / 2)
                offset += count;
            return offset;
        }

        private float ResolveCarouselScale(int distance)
        {
            if (distance <= 0)
                return carouselCenterScale;
            return distance == 1 ? carouselNearScale : carouselFarScale;
        }

        private float ResolveCarouselAlpha(int distance)
        {
            if (distance <= 0)
                return 1f;
            return distance == 1 ? carouselNearAlpha : carouselFarAlpha;
        }

        private float ResolveCarouselOverlayAlpha(int distance)
        {
            if (distance <= 0)
                return carouselCenterOverlayAlpha;
            return distance == 1 ? carouselNearOverlayAlpha : carouselFarOverlayAlpha;
        }

        private void KillTweens()
        {
            for (int i = activeTweens.Count - 1; i >= 0; i--)
            {
                if (activeTweens[i] != null && activeTweens[i].IsActive())
                    activeTweens[i].Kill();
            }

            activeTweens.Clear();
        }

        private void Track(Tween tween)
        {
            if (tween == null)
                return;

            activeTweens.Add(tween);
            tween.OnKill(() => activeTweens.Remove(tween));
        }
    }
}
