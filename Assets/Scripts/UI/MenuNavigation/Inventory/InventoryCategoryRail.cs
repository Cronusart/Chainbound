using System;
using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class InventoryCategoryRail : MonoBehaviour
    {
        private const int VisibleSideRows = 2;

        private static readonly string[] DefaultCategories =
        {
            PlayerItemCategoryKeys.Field,
            PlayerItemCategoryKeys.Capture,
            PlayerItemCategoryKeys.Favor,
            PlayerItemCategoryKeys.Bindery,
            PlayerItemCategoryKeys.Archive,
            PlayerItemCategoryKeys.Cosmetic,
            PlayerItemCategoryKeys.Quest,
        };

        [Header("Collapsed View")]
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private CanvasGroup promptGroup;
        [SerializeField] private Button collapsedButton;
        [SerializeField] private CanvasGroup collapsedButtonGroup;
        [SerializeField] private TMP_Text collapsedButtonLabel;

        [Header("Prompt Motion")]
        [SerializeField] private float promptClosedY = 86f;
        [SerializeField] private float promptCarouselY = 275f;
        [SerializeField] private float promptDriftOvershoot = 10f;
        [SerializeField] private float promptDriftOvershootDuration = 0.14f;
        [SerializeField] private float promptDriftSettleDuration = 0.2f;

        [Header("Carousel")]
        [SerializeField] private RectTransform expandedRoot;
        [SerializeField] private CanvasGroup expandedGroup;
        [SerializeField] private RectTransform categoryListRoot;
        [SerializeField] private Button categoryButtonTemplate;
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

        private readonly List<string> categories = new List<string>(DefaultCategories);
        private readonly List<Button> categoryButtons = new List<Button>();
        private readonly List<UnityAction> categoryButtonHandlers = new List<UnityAction>();
        private readonly List<CanvasGroup> categoryRowGroups = new List<CanvasGroup>();
        private readonly List<Image> categoryButtonImages = new List<Image>();
        private readonly List<Image> categoryDistanceOverlays = new List<Image>();
        private readonly List<bool> lastCarouselVisibleRows = new List<bool>();
        private readonly List<PartyTabFrameAnimator> shimmerAnimators = new List<PartyTabFrameAnimator>();
        private readonly List<Image> shimmerImages = new List<Image>();
        private readonly List<Tween> activeTweens = new List<Tween>();
        private UnityAction collapsedClickHandler;
        private string activeCategory = PlayerItemCategoryKeys.Field;
        private string focusedCategory = PlayerItemCategoryKeys.Field;
        private bool isArmed;
        private bool isCarouselOpen;
        private Action<string> onCategorySelected;
        private Action onOpenRequested;
        private Action onCloseRequested;

        public event Action<string> CategoryChanged;

        public string ActiveCategory => activeCategory;
        public string FocusedCategory => focusedCategory;
        public bool IsOpen => isCarouselOpen;
        public bool IsCarouselOpen => isCarouselOpen;
        public bool IsArmed => isArmed;
        public Button CollapsedButton => collapsedButton;
        public IReadOnlyList<Button> CategoryButtons => categoryButtons;
        public Sprite CarouselButtonSprite => carouselButtonSprite;
        public Sprite CarouselButtonSelectedSprite => carouselButtonSelectedSprite;
        public Color CarouselButtonTint => carouselButtonTint;
        public Color CarouselButtonSelectedTint => carouselButtonSelectedTint;
        public bool EnableCarouselShimmer => enableCarouselShimmer;
        public float CarouselShimmerFps => carouselShimmerFps;
        public IReadOnlyList<Sprite> CarouselShimmerFrames => carouselShimmerFrames;

        public static IReadOnlyList<string> CategoryOrder => DefaultCategories;

        public void Configure(
            PartyTabMotionProfileSO profile,
            Action<string> categorySelectedHandler,
            Action openRequestedHandler,
            Action closeRequestedHandler)
        {
            if (profile != null)
                motionProfile = profile;

            onCategorySelected = categorySelectedHandler;
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
            WireCategoryButtons();
            SetArmed(false, false);
        }

        public void RefreshRuntimeState()
        {
            EnsureBuilt();
            WireCategoryButtons();
            RefreshCollapsedLabel();
            RefreshPromptLabel(false);
            RefreshCarouselVisuals(false, false);
        }

        public void SetCategory(string categoryKey, bool notify)
        {
            string normalized = NormalizeCategory(categoryKey);
            activeCategory = normalized;
            focusedCategory = normalized;
            RefreshCollapsedLabel();

            // While the carousel is open, MoveCarousel owns DOTween row motion.
            // Snapping here (as Render used to) kills those tweens and looks instant.
            if (!isCarouselOpen)
                RefreshCarouselVisuals(false, false);

            if (notify)
                NotifyCategorySelected(activeCategory);
        }

        public void SetArmed(bool armed, bool animate)
        {
            EnsureBuilt();
            WireCategoryButtons();

            isArmed = armed;
            RefreshPromptLabel(animate);

            if (!isArmed && isCarouselOpen)
            {
                SetCarouselOpen(false, animate, false);
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
            WireCategoryButtons();
            SetArmed(true, false);
            focusedCategory = activeCategory;
            RefreshCarouselVisuals(false, false);
            SetCarouselOpen(true, animate, false);
        }

        public bool MoveCarousel(int delta, bool animate)
        {
            if (!isCarouselOpen || delta == 0)
                return false;

            int currentIndex = CategoryIndex(focusedCategory);
            int nextIndex = WrapIndex(currentIndex + delta);
            focusedCategory = categories[nextIndex];
            activeCategory = focusedCategory;
            RefreshCollapsedLabel();
            RefreshCarouselVisuals(animate, true);
            NotifyCategorySelected(activeCategory);
            return true;
        }

        public bool ConfirmCarousel(bool animate)
        {
            if (!isCarouselOpen)
                return false;

            activeCategory = focusedCategory;
            RefreshCollapsedLabel();
            SetCarouselOpen(false, animate, true);
            onCloseRequested?.Invoke();
            return true;
        }

        public Button GetCategoryButton(string categoryKey)
        {
            int index = CategoryIndex(categoryKey);
            return index >= 0 && index < categoryButtons.Count ? categoryButtons[index] : null;
        }

        private void Awake()
        {
            EnsureBuilt();
            RefreshRuntimeState();
        }

        private void OnEnable()
        {
            collapsedClickHandler ??= HandleCollapsedClicked;
            if (collapsedButton != null)
            {
                collapsedButton.onClick.RemoveListener(collapsedClickHandler);
                collapsedButton.onClick.AddListener(collapsedClickHandler);
            }
        }

        private void OnDisable()
        {
            if (collapsedButton != null && collapsedClickHandler != null)
                collapsedButton.onClick.RemoveListener(collapsedClickHandler);

            UnwireCategoryButtons();
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
                PlayCushion(GetCategoryButton(focusedCategory) != null ? GetCategoryButton(focusedCategory).transform : null);

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
            EnsureCategoryListRoot();
            EnsureCategoryButtonTemplate();
            EnsureCategoryButtons();
        }

        private void EnsureCollapsedButton()
        {
            if (collapsedButton == null)
            {
                collapsedButton = PlayerMenuUiFactory.CreateButton("CategoryButton", transform, ToLabel(activeCategory), PlayerMenuUiFactory.PanelAlt);
                RectTransform rect = collapsedButton.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(275f, 100f);
            }

            if (collapsedButtonGroup == null)
                collapsedButtonGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(collapsedButton.gameObject);

            if (collapsedButtonImage == null)
                collapsedButtonImage = collapsedButton.GetComponent<Image>();
            ApplySprite(collapsedButtonImage, collapsedButtonSprite, collapsedButtonTint);
            RemoveFocusVisual(collapsedButton);

            if (collapsedButtonLabel == null)
                collapsedButtonLabel = FindButtonLabel(collapsedButton);

            RefreshCollapsedLabel();
        }

        private void EnsurePrompt()
        {
            if (promptLabel == null)
            {
                Transform prompt = transform.Find("CategoryPrompt");
                if (prompt != null)
                    promptLabel = prompt.GetComponent<TMP_Text>();
            }

            if (promptLabel == null)
            {
                promptLabel = PlayerMenuUiFactory.CreateText("CategoryPrompt", transform, "Inventory", 24f, TextAlignmentOptions.Center, false);
                RectTransform rect = promptLabel.rectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(0f, promptClosedY);
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
                Transform existing = transform.Find("ExpandedCategories");
                if (existing != null)
                    expandedRoot = existing as RectTransform;
            }

            if (expandedRoot == null)
            {
                expandedRoot = PlayerMenuUiFactory.CreateRect("ExpandedCategories", transform);
                expandedRoot.anchorMin = new Vector2(0.5f, 0.5f);
                expandedRoot.anchorMax = new Vector2(0.5f, 0.5f);
                expandedRoot.pivot = new Vector2(0.5f, 0.5f);
                expandedRoot.anchoredPosition = Vector2.zero;
                expandedRoot.sizeDelta = new Vector2(300f, 500f);
            }

            expandedGroup = expandedGroup != null
                ? expandedGroup
                : PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(expandedRoot.gameObject);
        }

        private void EnsureCategoryListRoot()
        {
            if (categoryListRoot == null)
            {
                Transform existing = expandedRoot != null ? expandedRoot.Find("CategoryList") : null;
                if (existing != null)
                    categoryListRoot = existing as RectTransform;
            }

            if (categoryListRoot == null)
                categoryListRoot = PlayerMenuUiFactory.CreateRect("CategoryList", expandedRoot);

            PlayerMenuUiFactory.Stretch(categoryListRoot);
            VerticalLayoutGroup layout = categoryListRoot.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;
        }

        private void EnsureCategoryButtonTemplate()
        {
            if (categoryButtonTemplate == null && categoryListRoot != null)
            {
                Transform existing = categoryListRoot.Find("CategoryTemplate");
                if (existing != null)
                    categoryButtonTemplate = existing.GetComponent<Button>();
            }

            if (categoryButtonTemplate == null)
            {
                categoryButtonTemplate = PlayerMenuUiFactory.CreateButton("CategoryTemplate", categoryListRoot, "Field", PlayerMenuUiFactory.PanelAlt);
                RectTransform rect = categoryButtonTemplate.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(275f, 64f);
            }

            categoryButtonTemplate.gameObject.SetActive(false);
            RemoveFocusVisual(categoryButtonTemplate);
        }

        private void EnsureCategoryButtons()
        {
            if (categoryListRoot == null || categoryButtonTemplate == null)
                return;

            if (categoryButtons.Count == 0)
            {
                Button[] existing = categoryListRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < existing.Length; i++)
                {
                    if (existing[i] != null && existing[i] != categoryButtonTemplate && existing[i].name.StartsWith("Category_", StringComparison.Ordinal))
                        categoryButtons.Add(existing[i]);
                }
            }

            for (int i = categoryButtons.Count; i < categories.Count; i++)
            {
                Button button = Instantiate(categoryButtonTemplate, categoryListRoot);
                button.name = $"Category_{categories[i]}";
                button.gameObject.SetActive(true);
                categoryButtons.Add(button);
            }

            for (int i = 0; i < categoryButtons.Count; i++)
            {
                Button button = categoryButtons[i];
                if (button == null)
                    continue;

                RectTransform rect = button.transform as RectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(275f, 64f);
                button.gameObject.SetActive(i < categories.Count);
                RemoveFocusVisual(button);
                EnsureRowCaches(button, i);
            }
        }

        private void EnsureRowCaches(Button button, int index)
        {
            EnsureListSize(categoryRowGroups, index + 1);
            EnsureListSize(categoryButtonImages, index + 1);
            EnsureListSize(categoryDistanceOverlays, index + 1);
            EnsureListSize(lastCarouselVisibleRows, index + 1);
            EnsureListSize(shimmerAnimators, index + 1);
            EnsureListSize(shimmerImages, index + 1);

            categoryRowGroups[index] = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(button.gameObject);
            categoryButtonImages[index] = button.GetComponent<Image>();
            ApplySprite(categoryButtonImages[index], carouselButtonSprite, carouselButtonTint);

            Transform overlay = button.transform.Find("DistanceOverlay");
            Image overlayImage = overlay != null ? overlay.GetComponent<Image>() : null;
            if (overlayImage == null)
            {
                overlayImage = PlayerMenuUiFactory.CreateImage("DistanceOverlay", button.transform, carouselDistanceOverlayColor);
                PlayerMenuUiFactory.Stretch(overlayImage.rectTransform);
                overlayImage.raycastTarget = false;
            }

            categoryDistanceOverlays[index] = overlayImage;
            EnsureShimmer(button, index);
            TMP_Text label = FindButtonLabel(button);
            if (label != null && index < categories.Count)
                label.text = ToLabel(categories[index]);

            // Match Chainlings view rail draw order: shimmer behind, label on top.
            EnsureExpandedButtonDrawOrder(button, overlayImage, index < shimmerImages.Count ? shimmerImages[index] : null, label);
        }

        private void EnsureShimmer(Button button, int index)
        {
            Transform shimmer = button.transform.Find("Shimmer");
            Image shimmerImage = shimmer != null ? shimmer.GetComponent<Image>() : null;
            if (shimmerImage == null)
            {
                shimmerImage = PlayerMenuUiFactory.CreateImage("Shimmer", button.transform, Color.white);
                PlayerMenuUiFactory.Stretch(shimmerImage.rectTransform);
                shimmerImage.raycastTarget = false;
            }

            shimmerImage.color = Color.white;
            shimmerImage.raycastTarget = false;

            PartyTabFrameAnimator animator = shimmerImage.GetComponent<PartyTabFrameAnimator>();
            if (animator == null)
                animator = shimmerImage.gameObject.AddComponent<PartyTabFrameAnimator>();
            animator.SetTargetImage(shimmerImage);
            animator.SetFrames(carouselShimmerFrames);
            animator.SetFPS(carouselShimmerFps);
            animator.SetLoop(true);
            shimmerImages[index] = shimmerImage;
            shimmerAnimators[index] = animator;
        }

        /// <summary>
        /// Back → front: Shimmer, DistanceOverlay, then label (readable over full-button shimmer).
        /// </summary>
        private static void EnsureExpandedButtonDrawOrder(
            Button button,
            Image overlayImage,
            Image shimmerImage,
            TMP_Text label)
        {
            if (button == null)
                return;

            if (shimmerImage != null)
                shimmerImage.transform.SetAsFirstSibling();
            if (overlayImage != null)
            {
                if (shimmerImage != null)
                    overlayImage.transform.SetSiblingIndex(1);
                else
                    overlayImage.transform.SetAsFirstSibling();
            }

            if (label != null)
                label.transform.SetAsLastSibling();
        }

        private void WireCategoryButtons()
        {
            UnwireCategoryButtons();
            EnsureListSize(categoryButtonHandlers, categoryButtons.Count);

            for (int i = 0; i < categoryButtons.Count; i++)
            {
                Button button = categoryButtons[i];
                if (button == null)
                    continue;

                int captured = i;
                UnityAction handler = () => HandleCategoryButtonClicked(captured);
                categoryButtonHandlers[i] = handler;
                button.onClick.AddListener(handler);
            }
        }

        private void UnwireCategoryButtons()
        {
            for (int i = 0; i < categoryButtons.Count && i < categoryButtonHandlers.Count; i++)
            {
                if (categoryButtons[i] != null && categoryButtonHandlers[i] != null)
                    categoryButtons[i].onClick.RemoveListener(categoryButtonHandlers[i]);
            }

            categoryButtonHandlers.Clear();
        }

        private void HandleCategoryButtonClicked(int index)
        {
            if (index < 0 || index >= categories.Count)
                return;

            if (!isCarouselOpen)
            {
                SetCategory(categories[index], true);
                onOpenRequested?.Invoke();
                return;
            }

            if (string.Equals(categories[index], focusedCategory, StringComparison.OrdinalIgnoreCase))
            {
                ConfirmCarousel(true);
                return;
            }

            int delta = ResolveShortestDelta(CategoryIndex(focusedCategory), index);
            MoveCarousel(delta, true);
        }

        private void RefreshPromptLabel(bool animate)
        {
            if (promptLabel == null)
                return;

            promptLabel.text = isArmed ? "Change Category" : "Inventory";
            MovePromptForCarousel(isCarouselOpen, animate);
        }

        private void RefreshCollapsedLabel()
        {
            if (collapsedButtonLabel != null)
                collapsedButtonLabel.text = ToLabel(activeCategory);
        }

        private void RefreshCarouselVisuals(bool animate, bool stagger)
        {
            int focusedIndex = CategoryIndex(focusedCategory);
            if (focusedIndex < 0)
                focusedIndex = 0;

            for (int i = 0; i < categoryButtons.Count; i++)
            {
                Button button = categoryButtons[i];
                if (button == null)
                    continue;

                RectTransform rect = button.transform as RectTransform;
                CanvasGroup group = i < categoryRowGroups.Count ? categoryRowGroups[i] : null;
                Image image = i < categoryButtonImages.Count ? categoryButtonImages[i] : null;
                Image overlay = i < categoryDistanceOverlays.Count ? categoryDistanceOverlays[i] : null;
                int signedDistance = SignedDistance(i, focusedIndex, categories.Count);
                int absoluteDistance = Mathf.Abs(signedDistance);
                bool visibleRow = isCarouselOpen && absoluteDistance <= VisibleSideRows;
                bool wasVisibleRow = i < lastCarouselVisibleRows.Count && lastCarouselVisibleRows[i];
                // Match ChainlingPartyViewRail: animate whenever requested; second flag only staggers delay.
                bool animateRow = animate && Application.isPlaying && (visibleRow || wasVisibleRow);
                float targetAlpha = visibleRow ? ResolveRowAlpha(absoluteDistance) : 0f;
                float targetScale = ResolveRowScale(absoluteDistance);
                Vector2 targetPosition = new Vector2(0f, -signedDistance * carouselRowSpacing);

                button.gameObject.SetActive(isCarouselOpen || wasVisibleRow || !Application.isPlaying);
                button.interactable = visibleRow;
                ApplySprite(image, absoluteDistance == 0 ? carouselButtonSelectedSprite : carouselButtonSprite, absoluteDistance == 0 ? carouselButtonSelectedTint : carouselButtonTint);
                ApplyOverlay(overlay, absoluteDistance);
                SetShimmer(i, absoluteDistance == 0 && isCarouselOpen && enableCarouselShimmer);

                if (group != null)
                {
                    group.interactable = visibleRow;
                    group.blocksRaycasts = visibleRow;
                }

                if (animateRow)
                {
                    float delay = stagger ? Mathf.Min(absoluteDistance, VisibleSideRows) * carouselRowStagger : 0f;
                    if (rect != null)
                    {
                        rect.DOKill();
                        Track(rect.DOAnchorPos(targetPosition, carouselRotateDuration)
                            .SetDelay(delay)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                        Track(rect.DOScale(Vector3.one * targetScale, carouselRotateDuration)
                            .SetDelay(delay)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                    }

                    if (group != null)
                    {
                        group.DOKill();
                        Track(group.DOFade(targetAlpha, carouselRotateDuration)
                            .SetDelay(delay)
                            .SetEase(Ease.OutSine)
                            .SetUpdate(true));
                    }
                }
                else
                {
                    if (rect != null)
                        rect.DOKill();
                    if (group != null)
                        group.DOKill();

                    if (rect != null)
                    {
                        rect.anchoredPosition = targetPosition;
                        rect.localScale = Vector3.one * targetScale;
                    }

                    if (group != null)
                        group.alpha = targetAlpha;
                }

                if (i < lastCarouselVisibleRows.Count)
                    lastCarouselVisibleRows[i] = visibleRow;
            }
        }

        private void SetCarouselVisible(bool visible, bool animate)
        {
            if (expandedRoot != null)
                expandedRoot.gameObject.SetActive(true);

            SetCanvasGroupVisible(expandedGroup, visible, animate, carouselFadeDuration);
            RefreshCarouselVisuals(animate, visible);
            if (!visible)
            {
                for (int i = 0; i < categoryButtons.Count; i++)
                    SetShimmer(i, false);
            }
        }

        private void MovePromptForCarousel(bool carousel, bool animate)
        {
            if (promptLabel == null)
                return;

            RectTransform rect = promptLabel.rectTransform;
            float targetY = carousel ? promptCarouselY : promptClosedY;
            if (!animate || !Application.isPlaying)
            {
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, targetY);
                return;
            }

            rect.DOKill();
            float overshootY = targetY + (carousel ? promptDriftOvershoot : -promptDriftOvershoot);
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(rect.DOAnchorPosY(overshootY, Mathf.Max(0.01f, promptDriftOvershootDuration)).SetEase(Ease.OutSine));
            sequence.Append(rect.DOAnchorPosY(targetY, Mathf.Max(0.01f, promptDriftSettleDuration)).SetEase(Ease.InOutSine));
            Track(sequence);
        }

        private void PlayCushion(Transform target)
        {
            if (target == null || !Application.isPlaying)
                return;

            target.DOKill();
            Vector3 rest = Vector3.one;
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Append(target.DOScale(rest * (1f + cushionOvershoot), Mathf.Max(0.01f, cushionOvershootDuration)).SetEase(Ease.OutSine));
            sequence.Append(target.DOScale(rest, Mathf.Max(0.01f, cushionSettleDuration)).SetEase(Ease.InOutSine));
            Track(sequence);
        }

        private void SetCanvasGroupVisible(CanvasGroup group, bool visible, bool animate, float duration)
        {
            if (group == null)
                return;

            group.interactable = visible;
            group.blocksRaycasts = visible;

            if (animate && Application.isPlaying)
            {
                group.DOKill();
                Track(group.DOFade(visible ? 1f : 0f, Mathf.Max(0.01f, duration))
                    .SetEase(Ease.Linear)
                    .SetUpdate(true));
            }
            else
            {
                group.alpha = visible ? 1f : 0f;
            }
        }

        private void NotifyCategorySelected(string categoryKey)
        {
            onCategorySelected?.Invoke(categoryKey);
            CategoryChanged?.Invoke(categoryKey);
        }

        private void ApplyOverlay(Image overlay, int absoluteDistance)
        {
            if (overlay == null)
                return;

            float alpha = absoluteDistance == 0
                ? carouselCenterOverlayAlpha
                : absoluteDistance == 1
                    ? carouselNearOverlayAlpha
                    : carouselFarOverlayAlpha;
            overlay.color = new Color(
                carouselDistanceOverlayColor.r,
                carouselDistanceOverlayColor.g,
                carouselDistanceOverlayColor.b,
                alpha);
            overlay.enabled = alpha > 0.001f;
        }

        private void SetShimmer(int index, bool active)
        {
            Image shimmer = index >= 0 && index < shimmerImages.Count ? shimmerImages[index] : null;
            PartyTabFrameAnimator animator = index >= 0 && index < shimmerAnimators.Count ? shimmerAnimators[index] : null;
            Button button = index >= 0 && index < categoryButtons.Count ? categoryButtons[index] : null;
            Image overlay = index >= 0 && index < categoryDistanceOverlays.Count ? categoryDistanceOverlays[index] : null;

            if (shimmer != null)
            {
                shimmer.gameObject.SetActive(active && carouselShimmerFrames.Count > 0);
                if (shimmer.gameObject.activeSelf)
                {
                    TMP_Text label = FindButtonLabel(button);
                    EnsureExpandedButtonDrawOrder(button, overlay, shimmer, label);
                }
            }

            if (animator == null)
                return;

            if (active && carouselShimmerFrames.Count > 0 && !animator.IsPlaying)
                animator.Play();
            else if (!active)
                animator.Stop();
        }

        private float ResolveRowAlpha(int absoluteDistance)
        {
            if (absoluteDistance == 0)
                return 1f;
            if (absoluteDistance == 1)
                return carouselNearAlpha;
            return absoluteDistance <= VisibleSideRows ? carouselFarAlpha : 0f;
        }

        private float ResolveRowScale(int absoluteDistance)
        {
            if (absoluteDistance == 0)
                return carouselCenterScale;
            if (absoluteDistance == 1)
                return carouselNearScale;
            return carouselFarScale;
        }

        private int CategoryIndex(string categoryKey)
        {
            string normalized = NormalizeCategory(categoryKey);
            for (int i = 0; i < categories.Count; i++)
            {
                if (string.Equals(categories[i], normalized, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return 0;
        }

        private int WrapIndex(int index)
        {
            int count = Mathf.Max(1, categories.Count);
            while (index < 0)
                index += count;
            return index % count;
        }

        private int ResolveShortestDelta(int fromIndex, int toIndex)
        {
            int count = Mathf.Max(1, categories.Count);
            int delta = toIndex - fromIndex;
            if (delta > count / 2)
                delta -= count;
            if (delta < -count / 2)
                delta += count;
            return delta;
        }

        private static int SignedDistance(int index, int centerIndex, int count)
        {
            if (count <= 0)
                return 0;

            int distance = index - centerIndex;
            int half = count / 2;
            if (distance > half)
                distance -= count;
            if (distance < -half)
                distance += count;
            return distance;
        }

        private static string NormalizeCategory(string categoryKey)
        {
            return PlayerItemCategoryKeys.TryParse(categoryKey, out PlayerItemCategory category)
                ? PlayerItemCategoryKeys.ToKey(category)
                : PlayerItemCategoryKeys.Field;
        }

        public static string ToLabel(string key)
        {
            switch (NormalizeCategory(key))
            {
                case PlayerItemCategoryKeys.Capture: return "Capture";
                case PlayerItemCategoryKeys.Favor: return "Favor";
                case PlayerItemCategoryKeys.Bindery: return "Bindery";
                case PlayerItemCategoryKeys.Archive: return "Archive";
                case PlayerItemCategoryKeys.Cosmetic: return "Cosmetic";
                case PlayerItemCategoryKeys.Quest: return "Quest";
                default: return "Field";
            }
        }

        private static TMP_Text FindButtonLabel(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static void ApplySprite(Image image, Sprite sprite, Color color)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.color = color;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = true;
        }

        private static void RemoveFocusVisual(Button button)
        {
            if (button == null)
                return;

            Transform focus = button.transform.Find("FocusFrame");
            if (focus != null)
            {
                if (Application.isPlaying)
                    Destroy(focus.gameObject);
                else
                    DestroyImmediate(focus.gameObject);
            }
        }

        private static void EnsureListSize<T>(List<T> list, int size)
        {
            while (list.Count < size)
                list.Add(default);
        }

        private void KillTweens()
        {
            for (int i = activeTweens.Count - 1; i >= 0; i--)
            {
                Tween tween = activeTweens[i];
                if (tween != null && tween.IsActive())
                    tween.Kill();
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
