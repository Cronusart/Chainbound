using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Reusable book-page Chainling dossier. The same prefab is pushed by the Player
    /// Menu and instantiated as the post-capture reveal before nickname selection.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChainlingDetailScreen : MenuScreen
    {
        private enum DetailPanel
        {
            Summary,
            Moves,
            Learnable,
            Style,
            Progression,
        }

        [Header("Data Source")]
        [SerializeField] private ChainlingsDataSourceMode dataSourceMode = ChainlingsDataSourceMode.Production;

        [Header("Services")]
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private MonoBehaviour cosmeticInventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour moveInventoryProviderBehaviour;
        [SerializeField] private MonoBehaviour moveEquipServiceBehaviour;
        [SerializeField] private MonoBehaviour visualIdentityServiceBehaviour;
        [SerializeField] private MonoBehaviour rebirthServiceBehaviour;
        [SerializeField] private MonoBehaviour playerMenuSfxBehaviour;

        [Header("Data")]
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private CombatVisualDatabaseSO visualDatabase;
        [SerializeField] private ChainlingVisualIdentityDatabaseSO identityDatabase;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [FormerlySerializedAs("rewardConfig")]
        [SerializeField] private ProgressionConfigSO progressionConfig;

        [Header("Book Page")]
        [SerializeField] private RectTransform bookPageRoot;
        [SerializeField] private CanvasGroup bookPageGroup;
        [SerializeField] private float entranceDuration = 0.42f;
        [SerializeField] private float exitDuration = 0.24f;
        [SerializeField] private float entranceOvershoot = 20f;
        [SerializeField] private UnityEngine.Object captureSlideSound;
        [SerializeField] private float captureSlideDuration = 0.64f;

        [Header("Capture Name Focus")]
        [Tooltip("Page scale while the player is asked to name the capture. The name label is framed at the focus anchor.")]
        [SerializeField, Min(1f)] private float captureNameZoomScale = 1.6f;
        [Tooltip("Normalized screen point the name label is centered on during the zoom (keeps it above the keyboard).")]
        [SerializeField] private Vector2 captureNameFocusAnchor = new Vector2(0.5f, 0.68f);
        [SerializeField, Min(0.05f)] private float captureNameZoomDuration = 0.46f;
        [SerializeField, Min(0.05f)] private float captureNameZoomOutDuration = 0.4f;

        [Header("Page Stack")]
        [Tooltip("Stacked paper sheets (index + one sheet per tab). Behind pages stay visible; only the front sheet's contents stay enabled.")]
        [SerializeField] private RectTransform sheetStackRoot;
        [SerializeField, Min(0.02f)] private float sheetFanDuration = 0.16f;
        [SerializeField, Min(0.05f)] private float sheetSlideDuration = 0.28f;
        [SerializeField, Min(0f)] private float sheetSlideStagger = 0.05f;
        [SerializeField, Min(0.05f)] private float sheetSettleDuration = 0.22f;
        [SerializeField, Min(0f)] private float sheetFanTilt = 6f;
        [SerializeField] private Vector2 sheetFanSpread = new Vector2(28f, 16f);
        [SerializeField] private Vector2 sheetRestSpread = new Vector2(10f, 8f);
        [SerializeField, Min(0f)] private float sheetRestTilt = 2.2f;

        [Header("Header / Navigation")]
        [SerializeField] private TMP_Text kickerLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text metaLabel;
        [SerializeField] private Image portraitImage;
        [SerializeField] private RectTransform segmentRoot;
        [SerializeField] private RectTransform panelRoot;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text closeButtonLabel;
        [SerializeField] private Sprite idleButtonSprite;
        [SerializeField] private Sprite selectedButtonSprite;

        [Header("Pages")]
        [SerializeField] private ChainlingOverviewPanel overviewPanel;
        [SerializeField] private ChainlingMoveEquipPanel movePanel;
        [SerializeField] private ChainlingLearnableMovesPanel learnablePanel;
        [SerializeField] private ChainlingStylePanel stylePanel;
        [SerializeField] private ChainlingProgressionPanel progressionPanel;

        private readonly Button[] segmentButtons = new Button[5];
        private PlayerRosterEntry entry;
        private PlayerRosterSnapshot rosterCache;
        private SpeciesDefinitionSO species;
        private DetailPanel currentPanel;
        private bool eventsBound;
        private bool capturePresentation;
        private bool captureIndexLocked;
        private bool indexPageOpen;
        private bool closeInProgress;
        private Action captureCloseCallback;
        private TaskCompletionSource<bool> pageLanded;
        private Transform dossierHeader;
        private Tween pageTween;
        private Tween zoomTween;
        private Sequence turnTween;
        private bool pageTurnInProgress;
        private bool bookBaseCaptured;
        private Vector2 bookBaseAnchoredPosition;
        private CanvasGroup panelGroup;
        private CanvasGroup headerGroup;
        private CanvasGroup segmentGroup;
        private readonly List<RectTransform> stackSheets = new List<RectTransform>();
        private readonly List<Selectable> relayedControls = new List<Selectable>();
        private MonoBehaviour cachedSfxBehaviour;
        private MonoBehaviour cachedCombatSfxBehaviour;

        private static readonly DetailPanel[] PanelOrder =
        {
            DetailPanel.Summary,
            DetailPanel.Moves,
            DetailPanel.Learnable,
            DetailPanel.Style,
            DetailPanel.Progression,
        };

        public bool IsPrefabAuthored =>
            overviewPanel != null &&
            movePanel != null &&
            learnablePanel != null &&
            stylePanel != null &&
            progressionPanel != null &&
            panelRoot != null &&
            segmentRoot != null &&
            bookPageRoot != null &&
            closeButton != null;

        public ChainlingsDataSourceMode DataSourceMode => dataSourceMode;

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
        }

        public override void ConfigureInputContext(MenuInputContextController context)
        {
            if (context == null)
                return;

            context.SetContentInputHandler(null);
            base.ConfigureInputContext(context);
        }

        public override void Show()
        {
            EnsureBuilt();
            BindPanelEvents();
            CaptureBookBase();
            closeInProgress = false;
            pageTurnInProgress = false;
            pageLanded = new TaskCompletionSource<bool>();
            base.Show();
            ShowIndexFace();
            RefreshCloseCopy();
            ConfigureExplicitNavigation();
            PlayEntrance();
        }

        /// <summary>
        /// Completes when the book page has finished sliding into place. During a capture
        /// reveal this also waits for the zoom onto the name label, so the prompt starts
        /// with the name framed.
        /// </summary>
        public Task WaitForPageLandedAsync()
        {
            if (pageLanded != null)
                return pageLanded.Task;
            pageLanded = new TaskCompletionSource<bool>();
            return pageLanded.Task;
        }

        /// <summary>Live nickname shown on the index while the capture keyboard is open.</summary>
        public void SetLiveDisplayName(string displayName)
        {
            string trimmed = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            if (entry?.snapshot != null && trimmed.Length > 0)
                entry.snapshot.displayName = trimmed;
            if (nameLabel != null && trimmed.Length > 0)
                nameLabel.text = trimmed;
        }

        /// <summary>
        /// After the name prompt is confirmed or skipped, the page zooms back to its
        /// resting scale and the index unlocks with SUMMARY selected.
        /// </summary>
        public void UnlockCaptureIndex()
        {
            captureIndexLocked = false;
            currentPanel = DetailPanel.Summary;
            ShowIndexFace();

            if (!Application.isPlaying || bookPageRoot == null || !IsBookZoomed())
            {
                FinishUnlock();
                return;
            }

            KillZoomTween();
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(bookPageRoot);
            sequence.Join(bookPageRoot.DOScale(1f, captureNameZoomOutDuration).SetEase(Ease.InOutCubic));
            sequence.Join(bookPageRoot.DOAnchorPos(bookBaseAnchoredPosition, captureNameZoomOutDuration).SetEase(Ease.InOutCubic));
            zoomTween = sequence.OnComplete(() =>
            {
                zoomTween = null;
                FinishUnlock();
            });
        }

        private void FinishUnlock()
        {
            ResetBookTransform();
            if (bookPageGroup != null)
                bookPageGroup.interactable = true;
            ApplyIndexLock();
            SelectDefaultTab();
            PlaySfx(PlayerMenuSfxCue.ChainlingSelect, 0.7f);
        }

        public override void Hide()
        {
            KillPageTween(reset: true);
            KillZoomTween();
            KillTurnTween(resetVisuals: true);
            base.Hide();
        }

        private void OnDestroy()
        {
            KillPageTween(reset: false);
            KillZoomTween();
            KillTurnTween(resetVisuals: false);
            pageLanded?.TrySetResult(true);
        }

        private void CaptureBookBase()
        {
            if (bookBaseCaptured || bookPageRoot == null)
                return;
            bookBaseAnchoredPosition = bookPageRoot.anchoredPosition;
            bookBaseCaptured = true;
        }

        private bool IsBookZoomed()
        {
            return bookPageRoot != null &&
                   (Mathf.Abs(bookPageRoot.localScale.x - 1f) > 0.001f ||
                    (bookPageRoot.anchoredPosition - bookBaseAnchoredPosition).sqrMagnitude > 0.01f);
        }

        private void ResetBookTransform()
        {
            if (bookPageRoot == null)
                return;
            bookPageRoot.anchoredPosition = bookBaseCaptured ? bookBaseAnchoredPosition : new Vector2(bookPageRoot.anchoredPosition.x, 0f);
            bookPageRoot.localRotation = Quaternion.identity;
            bookPageRoot.localScale = Vector3.one;
        }

        private void KillZoomTween()
        {
            if (zoomTween != null && zoomTween.IsActive())
                zoomTween.Kill(false);
            zoomTween = null;
        }

        public void SetDataSourceMode(ChainlingsDataSourceMode mode)
        {
            dataSourceMode = mode;
            cosmeticInventoryServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, cosmeticInventoryServiceBehaviour);
            moveInventoryProviderBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, moveInventoryProviderBehaviour);
            moveEquipServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, moveEquipServiceBehaviour);
            visualIdentityServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, visualIdentityServiceBehaviour);
            rebirthServiceBehaviour = ChainlingsDataSourcePolicy.KeepAllowed(mode, rebirthServiceBehaviour);
        }

        public void Configure(
            SupabaseConfigSO config,
            SpeciesDatabaseSO speciesDb,
            CombatVisualDatabaseSO combatVisualDb,
            ChainlingVisualIdentityDatabaseSO chainlingIdentityDb,
            MoveDatabaseSO moveDb,
            ProgressionConfigSO progression,
            MonoBehaviour cosmeticInventoryServiceSource,
            MonoBehaviour moveInventoryProviderSource,
            MonoBehaviour moveEquipServiceSource,
            MonoBehaviour visualIdentityServiceSource,
            MonoBehaviour rebirthServiceSource)
        {
            if (config != null) supabaseConfig = config;
            if (speciesDb != null) speciesDatabase = speciesDb;
            if (combatVisualDb != null) visualDatabase = combatVisualDb;
            if (chainlingIdentityDb != null) identityDatabase = chainlingIdentityDb;
            if (moveDb != null) moveDatabase = moveDb;
            if (progression != null) progressionConfig = progression;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, cosmeticInventoryServiceSource))
                cosmeticInventoryServiceBehaviour = cosmeticInventoryServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, moveInventoryProviderSource))
                moveInventoryProviderBehaviour = moveInventoryProviderSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, moveEquipServiceSource))
                moveEquipServiceBehaviour = moveEquipServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, visualIdentityServiceSource))
                visualIdentityServiceBehaviour = visualIdentityServiceSource;
            if (ChainlingsDataSourcePolicy.IsProviderAllowed(dataSourceMode, rebirthServiceSource))
                rebirthServiceBehaviour = rebirthServiceSource;

            ConfigurePanels();
            EnsureBuilt();
            BindPanelEvents();
        }

        public void Open(PlayerRosterEntry rosterEntry, PlayerRosterSnapshot cache)
        {
            EnsureBuilt();
            BindPanelEvents();
            capturePresentation = false;
            captureIndexLocked = false;
            captureCloseCallback = null;
            SetCaptureOnlyTabs(false);
            movePanel?.SetReadOnly(false);
            entry = rosterEntry;
            rosterCache = cache;
            species = ResolveSpecies(entry);
            ConfigurePanels();
            RefreshHeader();
            BindPanels();
            ShowPanel(DetailPanel.Summary, playSfx: false);
            ApplyPreviewIdentity(entry?.snapshot?.visualIdentity);
        }

        /// <summary>
        /// Opens the dossier the same way <see cref="Open"/> does, then lands on the Moves page.
        /// Equipping still uses the existing move panel.
        /// </summary>
        public void OpenForManagement(PlayerRosterEntry rosterEntry, PlayerRosterSnapshot cache)
        {
            Open(rosterEntry, cache);
            ShowPanel(DetailPanel.Moves, playSfx: false);
        }

        public event Action Closed;

        public void CloseFromHost()
        {
            RequestClose();
        }

        /// <summary>Switches this dossier instance into the blocking post-capture reveal.</summary>
        public void SetCapturePresentation(Action onContinue)
        {
            capturePresentation = true;
            captureIndexLocked = true;
            captureCloseCallback = onContinue;
            SetCaptureOnlyTabs(false);
            movePanel?.SetReadOnly(true);
            RefreshCloseCopy();
            ConfigureExplicitNavigation();
        }

        public override bool TryHandleCancel()
        {
            if (pageTurnInProgress)
                return true;

            if (indexPageOpen)
            {
                ReturnToIndex();
                return true;
            }

            if (captureIndexLocked)
                return true;

            if (!capturePresentation)
                return false;

            RequestClose();
            return true;
        }

        public override void Refresh()
        {
            RefreshHeader();
            BindPanels();
        }

        private void BindPanelEvents()
        {
            if (eventsBound || stylePanel == null || progressionPanel == null)
                return;

            stylePanel.PreviewIdentityChanged += ApplyPreviewIdentity;
            stylePanel.Saved += HandleStyleSaved;
            progressionPanel.Reborn += HandleReborn;
            movePanel.NavigationChanged += ConfigureExplicitNavigation;
            if (closeButton != null)
                closeButton.onClick.AddListener(RequestClose);
            eventsBound = true;
        }

        private void ConfigurePanels()
        {
            stylePanel?.Configure(
                supabaseConfig,
                visualDatabase,
                identityDatabase,
                cosmeticInventoryServiceBehaviour,
                visualIdentityServiceBehaviour);
            movePanel?.Configure(speciesDatabase, moveDatabase, moveInventoryProviderBehaviour, moveEquipServiceBehaviour);
            progressionPanel?.Configure(
                progressionConfig,
                rebirthServiceBehaviour,
                speciesDb: speciesDatabase);
        }

        private void BindPanels()
        {
            overviewPanel?.Bind(entry, species, rosterCache, speciesDatabase, moveDatabase);
            movePanel?.Bind(entry);
            learnablePanel?.Bind(entry, species);
            stylePanel?.Bind(entry, rosterCache);
            progressionPanel?.Bind(entry, species, rosterCache);
        }

        private void RefreshHeader()
        {
            ChainlingSnapshot snapshot = entry?.snapshot;
            string displayName = snapshot != null && !string.IsNullOrWhiteSpace(snapshot.displayName)
                ? snapshot.displayName
                : species != null && !string.IsNullOrWhiteSpace(species.displayName)
                    ? species.displayName
                    : snapshot != null ? snapshot.speciesId : "Chainling";

            if (kickerLabel != null)
                kickerLabel.gameObject.SetActive(false);
            if (nameLabel != null)
                nameLabel.text = displayName;
            if (metaLabel != null)
            {
                metaLabel.text = species != null && !string.IsNullOrWhiteSpace(species.displayName)
                    ? species.displayName
                    : snapshot != null ? snapshot.speciesId : string.Empty;
            }
        }

        private void HandleStyleSaved(ChainlingVisualIdentity identity)
        {
            if (entry?.snapshot != null)
                entry.snapshot.visualIdentity = ChainlingVisualIdentity.CloneForPlayerSave(identity);
            ApplyPreviewIdentity(identity);
        }

        private void HandleReborn(PlayerRosterEntry updatedEntry)
        {
            if (updatedEntry != null) entry = updatedEntry;
            species = ResolveSpecies(entry);
            RefreshHeader();
            BindPanels();
            ApplyPreviewIdentity(entry?.snapshot?.visualIdentity);
        }

        private void ApplyPreviewIdentity(ChainlingVisualIdentity identity)
        {
            if (portraitImage == null || entry?.snapshot == null)
                return;

            ChainlingVisualIdentity normalized = ChainlingVisualIdentity.CloneOrDefault(identity)
                ?? ChainlingVisualIdentity.CloneOrDefault(entry.snapshot.visualIdentity);
            ChainlingType entangledType = entry.snapshot.isEntangled ? entry.snapshot.entangledType : ChainlingType.None;
            CombatVisualProfileSO profile = visualDatabase != null
                ? visualDatabase.GetProfile(entry.snapshot.speciesId, normalized.visualVariantId, entangledType)
                : null;
            Sprite sprite = profile != null && profile.portrait != null
                ? profile.portrait
                : profile != null && profile.idleSequence != null && profile.idleSequence.Length > 0
                    ? profile.idleSequence[0]
                    : null;
            portraitImage.sprite = sprite;
            portraitImage.enabled = sprite != null;
            portraitImage.preserveAspect = true;
        }

        private SpeciesDefinitionSO ResolveSpecies(PlayerRosterEntry rosterEntry)
        {
            return speciesDatabase != null && rosterEntry?.snapshot != null
                ? speciesDatabase.GetById(rosterEntry.snapshot.speciesId)
                : null;
        }

        private void ShowPanel(DetailPanel panel, bool playSfx = true)
        {
            currentPanel = panel;
            EnableFrontPageContent();
            RefreshSegments();
            ConfigureExplicitNavigation();
            if (playSfx)
                PlaySfx(PlayerMenuSfxCue.PanelSwitch);
        }

        private static void SetPageContentEnabled(Transform page, bool enabled)
        {
            if (page == null)
                return;

            for (int i = 0; i < page.childCount; i++)
                page.GetChild(i).gameObject.SetActive(enabled);

            CanvasGroup group = page.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.interactable = enabled;
                group.blocksRaycasts = enabled;
            }
        }

        private void RefreshSegments()
        {
            BindSegment(0, DetailPanel.Summary, "SUMMARY");
            BindSegment(1, DetailPanel.Moves, "MOVES");
            BindSegment(2, DetailPanel.Learnable, "LEARN");
            BindSegment(3, DetailPanel.Style, "STYLE");
            BindSegment(4, DetailPanel.Progression, "GROWTH");
        }

        private void BindSegment(int index, DetailPanel panel, string label)
        {
            Button button = index >= 0 && index < segmentButtons.Length ? segmentButtons[index] : null;
            if (button == null) return;

            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                image.sprite = null;
                image.color = Color.clear;
            }

            TMP_Text title = button.transform.Find("Title")?.GetComponent<TMP_Text>();
            TMP_Text pageNumber = button.transform.Find("PageNumber")?.GetComponent<TMP_Text>();
            if (title != null)
                title.text = label;
            if (pageNumber != null)
                pageNumber.text = (index + 1).ToString();
            if (title == null)
            {
                TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                    text.text = $"{label}    ··········    {index + 1}";
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => OpenIndexPage(panel));
        }

        private void RefreshCloseCopy()
        {
            if (closeButtonLabel != null)
                closeButtonLabel.text = indexPageOpen || !capturePresentation ? "BACK" : "CONTINUE";
        }

        private void RequestClose()
        {
            if (pageTurnInProgress)
                return;

            if (indexPageOpen)
            {
                ReturnToIndex();
                return;
            }

            if (captureIndexLocked || closeInProgress)
                return;
            closeInProgress = true;
            PlaySfx(capturePresentation ? PlayerMenuSfxCue.Confirm : PlayerMenuSfxCue.Cancel);

            Action complete = () =>
            {
                if (capturePresentation)
                {
                    Action callback = captureCloseCallback;
                    captureCloseCallback = null;
                    callback?.Invoke();
                    return;
                }

                MenuScreenStack stack = GetComponentInParent<MenuScreenStack>(true);
                if (stack != null)
                    stack.Pop();
                else
                    Hide();

                if (!capturePresentation)
                    Closed?.Invoke();
            };

            if (!Application.isPlaying || bookPageRoot == null)
            {
                complete();
                return;
            }

            KillPageTween(reset: false);
            if (bookPageGroup != null)
                bookPageGroup.interactable = false;
            float targetY = Mathf.Max(1100f, ((RectTransform)transform).rect.height + 120f);
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(bookPageRoot.DOAnchorPosY(targetY, exitDuration).SetEase(Ease.InCubic));
            if (bookPageGroup != null)
                sequence.Join(bookPageGroup.DOFade(0f, exitDuration * 0.8f));
            pageTween = sequence.OnComplete(() =>
            {
                pageTween = null;
                complete();
            });
        }

        private void PlayEntrance()
        {
            if (!Application.isPlaying || bookPageRoot == null)
            {
                FinishEntrance();
                return;
            }

            if (capturePresentation)
                PlayCaptureSlide();
            else
                PlayMenuDrop();
        }

        private void PlayCaptureSlide()
        {
            KillPageTween(reset: false);
            KillZoomTween();
            float startY = Mathf.Max(1040f, ((RectTransform)transform).rect.height + 160f);
            bookPageRoot.anchoredPosition = new Vector2(bookBaseAnchoredPosition.x, bookBaseAnchoredPosition.y + startY);
            bookPageRoot.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-8f, 8f));
            bookPageRoot.localScale = Vector3.one * 0.98f;
            PrepareEntranceGroup();
            PlayCaptureSlideSound();

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(bookPageRoot.DOAnchorPosY(bookBaseAnchoredPosition.y, captureSlideDuration).SetEase(Ease.OutCubic));
            sequence.Join(bookPageRoot.DOLocalRotate(Vector3.zero, captureSlideDuration).SetEase(Ease.OutQuad));
            sequence.Join(bookPageRoot.DOScale(1.02f, captureSlideDuration * 0.72f).SetEase(Ease.OutCubic));
            if (bookPageGroup != null)
                sequence.Join(bookPageGroup.DOFade(1f, captureSlideDuration * 0.45f));
            sequence.Append(bookPageRoot.DOScale(1f, 0.12f).SetEase(Ease.OutQuad));
            pageTween = sequence.OnComplete(FinishEntrance);
        }

        private void PlayMenuDrop()
        {
            KillPageTween(reset: false);
            KillZoomTween();
            float startY = Mathf.Max(1100f, ((RectTransform)transform).rect.height + 120f);
            bookPageRoot.anchoredPosition = new Vector2(bookBaseAnchoredPosition.x, bookBaseAnchoredPosition.y + startY);
            bookPageRoot.localRotation = Quaternion.identity;
            bookPageRoot.localScale = Vector3.one;
            PrepareEntranceGroup();
            PlaySfx(PlayerMenuSfxCue.PanelSwitch);

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.Join(bookPageRoot.DOAnchorPosY(bookBaseAnchoredPosition.y - entranceOvershoot, entranceDuration).SetEase(Ease.OutCubic));
            if (bookPageGroup != null)
                sequence.Join(bookPageGroup.DOFade(1f, entranceDuration * 0.55f));
            sequence.Append(bookPageRoot.DOAnchorPosY(bookBaseAnchoredPosition.y, 0.12f).SetEase(Ease.OutBack));
            pageTween = sequence.OnComplete(() =>
            {
                PlaySfx(PlayerMenuSfxCue.ChainlingSelect, 0.75f);
                FinishEntrance();
            });
        }

        private void PrepareEntranceGroup()
        {
            if (bookPageGroup == null)
                return;
            bookPageGroup.alpha = 0f;
            bookPageGroup.interactable = false;
            bookPageGroup.blocksRaycasts = true;
        }

        private void FinishEntrance()
        {
            pageTween = null;
            ResetBookTransform();

            if (bookPageGroup != null)
            {
                bookPageGroup.alpha = 1f;
                bookPageGroup.interactable = !captureIndexLocked;
            }

            if (captureIndexLocked && Application.isPlaying && bookPageRoot != null && nameLabel != null)
            {
                // Capture reveal: the page settles, then the camera pushes in on the
                // name line so the "name your Chainling" prompt reads off the page.
                PlayCaptureNameZoom();
                return;
            }

            if (!captureIndexLocked)
                SelectDefaultTab();
            pageLanded?.TrySetResult(true);
        }

        private void PlayCaptureNameZoom()
        {
            KillZoomTween();
            RectTransform parent = bookPageRoot.parent as RectTransform;
            if (parent == null)
            {
                pageLanded?.TrySetResult(true);
                return;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(bookPageRoot);
            Canvas.ForceUpdateCanvases();

            float scale = Mathf.Max(1f, captureNameZoomScale);
            RectTransform label = nameLabel.rectTransform;
            Vector3 labelWorld = label.TransformPoint(label.rect.center);
            Vector2 labelInBook = bookPageRoot.InverseTransformPoint(labelWorld);

            Rect parentRect = parent.rect;
            Vector2 focusLocal = new Vector2(
                parentRect.xMin + parentRect.width * Mathf.Clamp01(captureNameFocusAnchor.x),
                parentRect.yMin + parentRect.height * Mathf.Clamp01(captureNameFocusAnchor.y));
            Vector2 baseLocal = bookPageRoot.localPosition;
            Vector2 targetLocal = focusLocal - labelInBook * scale;
            Vector2 targetAnchored = bookBaseAnchoredPosition + (targetLocal - baseLocal);

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(bookPageRoot);
            sequence.Join(bookPageRoot.DOScale(scale, captureNameZoomDuration).SetEase(Ease.InOutCubic));
            sequence.Join(bookPageRoot.DOAnchorPos(targetAnchored, captureNameZoomDuration).SetEase(Ease.InOutCubic));
            zoomTween = sequence.OnComplete(() =>
            {
                zoomTween = null;
                pageLanded?.TrySetResult(true);
            });
        }

        private void PlayCaptureSlideSound()
        {
            if (captureSlideSound == null)
            {
                PlaySfx(PlayerMenuSfxCue.PanelSwitch);
                return;
            }

            IPlayerMenuSfxPlayer menuSfx = PlayerMenuSfxResolver.Resolve(playerMenuSfxBehaviour, ref cachedSfxBehaviour);
            if (menuSfx != null)
            {
                menuSfx.PlayLayered(captureSlideSound, 1f);
                return;
            }

            ICombatUiSfxPlayer combatSfx = CombatUiSfxResolver.Resolve(null, ref cachedCombatSfxBehaviour);
            combatSfx?.PlaySound(captureSlideSound);
        }

        private void KillPageTween(bool reset)
        {
            if (pageTween != null && pageTween.IsActive())
                pageTween.Kill(false);
            pageTween = null;
            bookPageRoot?.DOKill(false);
            bookPageGroup?.DOKill(false);
            if (reset && bookPageRoot != null)
            {
                ResetBookTransform();
                if (bookPageGroup != null) bookPageGroup.alpha = 1f;
            }
        }

        private void SelectDefaultTab()
        {
            ApplyIndexLock();
            Button selected = segmentButtons[(int)currentPanel];
            if (selected != null && selected.gameObject.activeInHierarchy && selected.interactable && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
        }

        private void ShowIndexFace()
        {
            indexPageOpen = false;
            CacheChrome();
            DetachPageTurnRelays();
            CacheSheets();
            BringPageToFront(ResolveIndexPage());
            SetPageContentEnabled(ResolveIndexPage(), true);
            EnableFrontPageContent();
            ApplyRestPeek();
            ResetIndexVisuals();
            ApplyIndexLock();
            RefreshCloseCopy();
            ConfigureExplicitNavigation();
        }

        /// <summary>
        /// Confirming an index row fans the stack and slides the index sheet away,
        /// revealing the chosen tab page underneath.
        /// </summary>
        private void OpenIndexPage(DetailPanel panel)
        {
            if (captureIndexLocked || pageTurnInProgress)
                return;

            CacheChrome();
            if (!Application.isPlaying || sheetStackRoot == null)
            {
                SwitchToPage(panel);
                SelectFirstPageControl();
                return;
            }

            PlaySheetTurn(1, () => SwitchToPage(panel), SelectFirstPageControl);
        }

        private void SwitchToPage(DetailPanel panel)
        {
            indexPageOpen = true;
            CacheChrome();
            ResetIndexVisuals();
            SetPageContentEnabled(ResolveIndexPage(), false);
            currentPanel = panel;
            BringPageToFront(ResolvePageSheet(panel));
            EnableFrontPageContent();
            RefreshCloseCopy();
        }

        /// <summary>B / BACK from an open page folds the page away and reopens the index.</summary>
        private void ReturnToIndex()
        {
            if (pageTurnInProgress)
                return;

            if (!Application.isPlaying || segmentRoot == null || panelRoot == null)
            {
                ShowIndexFace();
                SelectDefaultTab();
                PlaySfx(PlayerMenuSfxCue.Cancel);
                return;
            }

            PlaySheetTurn(-1, ShowIndexFace, SelectDefaultTab);
        }

        /// <summary>
        /// D-pad left/right on an open page flips to the neighbouring index page, wrapping
        /// from the last page back to the first (and the reverse).
        /// </summary>
        private void FlipPage(int direction)
        {
            if (!indexPageOpen || pageTurnInProgress || direction == 0)
                return;

            int count = PanelOrder.Length;
            int currentIndex = (int)currentPanel;
            int targetIndex = currentIndex;
            for (int step = 0; step < count; step++)
            {
                targetIndex = (targetIndex + (direction > 0 ? 1 : -1) + count) % count;
                if (IsPageAvailable(targetIndex))
                    break;
            }

            if (targetIndex == currentIndex)
            {
                PlaySfx(PlayerMenuSfxCue.Invalid, 0.6f);
                return;
            }

            DetailPanel target = PanelOrder[targetIndex];
            if (!Application.isPlaying || panelRoot == null || bookPageRoot == null)
            {
                ShowPanel(target);
                SelectFirstPageControl();
                return;
            }

            PlaySheetTurn(direction, () =>
            {
                SwitchToPage(target);
            }, SelectFirstPageControl);
        }

        private bool IsPageAvailable(int index)
        {
            return index >= 0 &&
                   index < segmentButtons.Length &&
                   segmentButtons[index] != null &&
                   segmentButtons[index].gameObject.activeSelf;
        }

        /// <summary>
        /// Fans the pile, slides the front sheet off in the direction the player
        /// scrolled, then settles the remaining papers neatly behind the new front.
        /// </summary>
        private void PlaySheetTurn(int direction, Action swapContent, Action onSettled)
        {
            if (bookPageRoot == null)
            {
                swapContent?.Invoke();
                onSettled?.Invoke();
                return;
            }

            KillTurnTween(resetVisuals: false);
            BeginPageTurn();
            PlayCaptureSlideSound();
            CacheSheets();

            float dir = direction >= 0 ? 1f : -1f;
            RectTransform departing = ResolveFrontSheet();
            float fan = Mathf.Max(0.02f, sheetFanDuration);
            float slide = Mathf.Max(0.05f, sheetSlideDuration);
            float tilt = Mathf.Max(0f, sheetFanTilt);
            RectTransform screenRect = (RectTransform)transform;
            float exitX = screenRect.rect.width * 0.7f + 180f;
            Vector2 exitOffset = new Vector2(dir * exitX, sheetFanSpread.y * 0.35f);
            Vector3 exitRotation = new Vector3(0f, 0f, -dir * tilt * 1.8f);

            if (sheetStackRoot != null)
                sheetStackRoot.gameObject.SetActive(true);

            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            for (int i = 0; i < stackSheets.Count; i++)
            {
                RectTransform sheet = stackSheets[i];
                Vector2 spread = RestPeekOffset(i) + new Vector2(
                    UnityEngine.Random.Range(-1f, 1f) * sheetFanSpread.x,
                    UnityEngine.Random.Range(-1f, 1f) * sheetFanSpread.y);
                float sheetTilt = RestPeekTilt(i) + (i % 2 == 0 ? -1f : 1f) * tilt * UnityEngine.Random.Range(0.4f, 1f);
                sequence.Insert(0f, sheet.DOAnchorPos(spread, fan).SetEase(Ease.OutSine));
                sequence.Insert(0f, sheet.DOLocalRotate(new Vector3(0f, 0f, sheetTilt), fan).SetEase(Ease.OutSine));
            }

            if (departing != null)
            {
                sequence.Insert(fan, departing.DOAnchorPos(exitOffset, slide).SetEase(Ease.InCubic));
                sequence.Insert(fan, departing.DOLocalRotate(exitRotation, slide).SetEase(Ease.InSine));
            }

            sequence.AppendCallback(() =>
            {
                if (departing != null)
                    departing.SetAsFirstSibling();
                swapContent?.Invoke();
                CacheSheets();
                turnTween = BuildSheetSettle(onSettled);
            });
            turnTween = sequence;
        }

        private Sequence BuildSheetSettle(Action onSettled)
        {
            float settle = Mathf.Max(0.05f, sheetSettleDuration);
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            for (int i = 0; i < stackSheets.Count; i++)
            {
                RectTransform sheet = stackSheets[i];
                sequence.Insert(0f, sheet.DOAnchorPos(RestPeekOffset(i), settle).SetEase(Ease.OutCubic));
                sequence.Insert(0f, sheet.DOLocalRotate(new Vector3(0f, 0f, RestPeekTilt(i)), settle).SetEase(Ease.OutCubic));
            }

            return sequence.OnComplete(() =>
            {
                turnTween = null;
                ApplyRestPeek();
                EndPageTurn();
                onSettled?.Invoke();
            });
        }

        private void CacheSheets()
        {
            if (sheetStackRoot == null)
            {
                sheetStackRoot = transform.Find("DossierPageStack") as RectTransform;
                if (sheetStackRoot == null)
                    sheetStackRoot = transform.Find("DossierSheetStack") as RectTransform;
            }

            stackSheets.Clear();
            if (sheetStackRoot == null)
                return;

            // Front sheet is the last sibling.
            for (int i = 0; i < sheetStackRoot.childCount; i++)
            {
                if (sheetStackRoot.GetChild(i) is RectTransform sheet && sheet.gameObject.activeSelf)
                    stackSheets.Add(sheet);
            }
        }

        private RectTransform ResolveFrontSheet()
        {
            CacheSheets();
            return stackSheets.Count > 0 ? stackSheets[stackSheets.Count - 1] : null;
        }

        private RectTransform ResolveIndexPage()
        {
            if (sheetStackRoot != null)
            {
                Transform index = sheetStackRoot.Find("BookPage");
                if (index is RectTransform indexRect)
                    return indexRect;
            }

            Transform book = transform.Find("BookPage");
            return book as RectTransform;
        }

        private RectTransform ResolvePageSheet(DetailPanel panel)
        {
            Component host = panel switch
            {
                DetailPanel.Moves => movePanel,
                DetailPanel.Learnable => learnablePanel,
                DetailPanel.Style => stylePanel,
                DetailPanel.Progression => progressionPanel,
                _ => overviewPanel,
            };
            return host != null ? host.transform as RectTransform : null;
        }

        private void BringPageToFront(RectTransform page)
        {
            if (page == null)
                return;
            page.gameObject.SetActive(true);
            page.SetAsLastSibling();
        }

        private void EnableFrontPageContent()
        {
            CacheSheets();
            RectTransform front = ResolveFrontSheet();
            RectTransform index = ResolveIndexPage();
            for (int i = 0; i < stackSheets.Count; i++)
            {
                RectTransform sheet = stackSheets[i];
                bool isFront = sheet == front;
                if (sheet == index)
                    SetPageContentEnabled(sheet, isFront && !indexPageOpen);
                else
                    SetPageContentEnabled(sheet, isFront && indexPageOpen);
            }
        }

        private Vector2 RestPeekOffset(int backToFrontIndex)
        {
            int depth = stackSheets.Count - 1 - backToFrontIndex;
            if (depth <= 0)
                return Vector2.zero;
            return new Vector2(sheetRestSpread.x * depth, -sheetRestSpread.y * depth);
        }

        private float RestPeekTilt(int backToFrontIndex)
        {
            int depth = stackSheets.Count - 1 - backToFrontIndex;
            if (depth <= 0)
                return 0f;
            return (depth % 2 == 0 ? -1f : 1f) * sheetRestTilt * Mathf.Min(depth, 3);
        }

        private void ApplyRestPeek()
        {
            CacheSheets();
            if (sheetStackRoot != null)
                sheetStackRoot.gameObject.SetActive(true);

            for (int i = 0; i < stackSheets.Count; i++)
            {
                RectTransform sheet = stackSheets[i];
                if (sheet == null)
                    continue;
                sheet.DOKill(false);
                sheet.anchoredPosition = RestPeekOffset(i);
                sheet.localRotation = Quaternion.Euler(0f, 0f, RestPeekTilt(i));
                Graphic graphic = sheet.GetComponent<Graphic>();
                if (graphic != null)
                    graphic.canvasRenderer.SetAlpha(1f);
            }
        }

        private void ResetSheets()
        {
            ApplyRestPeek();
        }

        private void BeginPageTurn()
        {
            pageTurnInProgress = true;
            if (bookPageGroup != null)
                bookPageGroup.interactable = false;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void EndPageTurn()
        {
            pageTurnInProgress = false;
            if (panelRoot != null)
            {
                panelRoot.localScale = Vector3.one;
                panelRoot.localRotation = Quaternion.identity;
            }
            CanvasGroup pages = ResolvePanelGroup();
            if (pages != null)
                pages.alpha = 1f;
            if (bookPageGroup != null)
                bookPageGroup.interactable = !captureIndexLocked;
            ConfigureExplicitNavigation();
        }

        private void KillTurnTween(bool resetVisuals)
        {
            if (turnTween != null && turnTween.IsActive())
                turnTween.Kill(false);
            turnTween = null;
            pageTurnInProgress = false;
            ResetSheets();
            if (!resetVisuals)
                return;

            ResetBookTransform();
            ResetIndexVisuals();
            if (panelRoot != null)
            {
                panelRoot.localScale = Vector3.one;
                panelRoot.localRotation = Quaternion.identity;
            }
            CanvasGroup pages = ResolvePanelGroup();
            if (pages != null)
                pages.alpha = 1f;
        }

        private void ResetIndexVisuals()
        {
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                if (segmentButtons[i] != null)
                    segmentButtons[i].transform.localScale = Vector3.one;
            }
            CanvasGroup header = ResolveHeaderGroup();
            if (header != null)
                header.alpha = 1f;
            CanvasGroup segments = ResolveSegmentGroup();
            if (segments != null)
                segments.alpha = 1f;
        }

        private CanvasGroup ResolveSegmentGroup()
        {
            if (segmentGroup == null && segmentRoot != null)
                segmentGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(segmentRoot.gameObject);
            return segmentGroup;
        }

        private CanvasGroup ResolvePanelGroup()
        {
            if (panelGroup == null && panelRoot != null)
                panelGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(panelRoot.gameObject);
            return panelGroup;
        }

        private CanvasGroup ResolveHeaderGroup()
        {
            CacheChrome();
            if (headerGroup == null && dossierHeader != null)
                headerGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(dossierHeader.gameObject);
            return headerGroup;
        }

        private void AttachPageTurnRelays(List<Selectable> contentControls)
        {
            DetachPageTurnRelays();
            if (!indexPageOpen)
                return;

            for (int i = 0; i < contentControls.Count; i++)
            {
                if (contentControls[i] == null)
                    continue;
                ChainlingDossierPageTurnRelay.Attach(contentControls[i], FlipPage);
                relayedControls.Add(contentControls[i]);
            }

            if (closeButton != null)
            {
                ChainlingDossierPageTurnRelay.Attach(closeButton, FlipPage);
                relayedControls.Add(closeButton);
            }
        }

        private void DetachPageTurnRelays()
        {
            for (int i = 0; i < relayedControls.Count; i++)
                ChainlingDossierPageTurnRelay.Detach(relayedControls[i]);
            relayedControls.Clear();
        }

        private void CacheChrome()
        {
            if (dossierHeader == null && segmentRoot != null && segmentRoot.parent != null)
                dossierHeader = segmentRoot.parent.Find("DossierHeader");
        }

        private void ApplyIndexLock()
        {
            bool tabsUsable = !captureIndexLocked && !indexPageOpen;
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                if (segmentButtons[i] != null)
                    segmentButtons[i].interactable = tabsUsable;
            }

            if (closeButton != null)
                closeButton.interactable = !captureIndexLocked;
        }

        private void SelectFirstPageControl()
        {
            Component activePanel = ResolveActivePanel();
            if (activePanel != null)
            {
                Selectable[] candidates = activePanel.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < candidates.Length; i++)
                {
                    Selectable candidate = candidates[i];
                    if (candidate != null && candidate.gameObject.activeInHierarchy && candidate.interactable && EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(candidate.gameObject);
                        return;
                    }
                }
            }

            if (closeButton != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
        }

        private void ConfigureExplicitNavigation()
        {
            var activeTabs = new List<Button>();
            for (int i = 0; i < segmentButtons.Length; i++)
                if (segmentButtons[i] != null && segmentButtons[i].gameObject.activeInHierarchy)
                    activeTabs.Add(segmentButtons[i]);

            var contentControls = new List<Selectable>();
            Component activePanel = ResolveActivePanel();
            if (activePanel != null)
            {
                Selectable[] candidates = activePanel.GetComponentsInChildren<Selectable>(true);
                for (int i = 0; i < candidates.Length; i++)
                {
                    Selectable candidate = candidates[i];
                    if (candidate != null && candidate.gameObject.activeInHierarchy && candidate.interactable)
                        contentControls.Add(candidate);
                }
            }

            for (int i = 0; i < activeTabs.Count; i++)
            {
                Button button = activeTabs[i];
                if (button == null) continue;
                Navigation nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i > 0 ? activeTabs[i - 1] : null,
                    selectOnDown = i + 1 < activeTabs.Count ? activeTabs[i + 1] : closeButton,
                };
                button.navigation = nav;
            }

            if (indexPageOpen)
            {
                // An open page is a vertical list that wraps through BACK; left/right is
                // reserved for page turns (no horizontal targets, so the relay fires).
                for (int i = 0; i < contentControls.Count; i++)
                {
                    contentControls[i].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = i > 0 ? contentControls[i - 1] : closeButton,
                        selectOnDown = i + 1 < contentControls.Count ? contentControls[i + 1] : closeButton,
                    };
                }

                if (closeButton != null)
                {
                    closeButton.navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = contentControls.Count > 0 ? contentControls[contentControls.Count - 1] : null,
                        selectOnDown = contentControls.Count > 0 ? contentControls[0] : null,
                    };
                }

                AttachPageTurnRelays(contentControls);
                return;
            }

            if (closeButton != null)
            {
                Navigation nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = contentControls.Count > 0
                        ? contentControls[contentControls.Count - 1]
                        : segmentButtons[(int)currentPanel] != null && segmentButtons[(int)currentPanel].gameObject.activeInHierarchy
                            ? segmentButtons[(int)currentPanel]
                            : activeTabs.Count > 0 ? activeTabs[0] : null,
                };
                closeButton.navigation = nav;
            }

            AttachPageTurnRelays(contentControls);
        }

        private Component ResolveActivePanel()
        {
            switch (currentPanel)
            {
                case DetailPanel.Moves: return movePanel;
                case DetailPanel.Learnable: return learnablePanel;
                case DetailPanel.Style: return stylePanel;
                case DetailPanel.Progression: return progressionPanel;
                default: return overviewPanel;
            }
        }

        private void SetCaptureOnlyTabs(bool captureOnly)
        {
            // Capture reveal is intentionally read-only and short: Summary, owned Moves,
            // and Learnable pool. Style/Growth remain available in Player Menu detail.
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                bool visible = !captureOnly || i <= (int)DetailPanel.Learnable;
                if (segmentButtons[i] != null)
                    segmentButtons[i].gameObject.SetActive(visible);
                RectTransform sheet = ResolvePageSheet((DetailPanel)i);
                if (sheet != null && sheet != ResolveIndexPage())
                    sheet.gameObject.SetActive(visible);
            }

            CacheSheets();
            ApplyRestPeek();
        }

        private void PlaySfx(PlayerMenuSfxCue cue, float volume = 1f)
        {
            IPlayerMenuSfxPlayer menuSfx = PlayerMenuSfxResolver.Resolve(playerMenuSfxBehaviour, ref cachedSfxBehaviour);
            if (menuSfx != null)
            {
                menuSfx.PlayCue(cue, null, volume);
                return;
            }

            ICombatUiSfxPlayer combatSfx = CombatUiSfxResolver.Resolve(null, ref cachedCombatSfxBehaviour);
            if (combatSfx == null)
                return;
            CombatUiSfxCue combatCue = cue == PlayerMenuSfxCue.Cancel
                ? CombatUiSfxCue.Cancel
                : cue == PlayerMenuSfxCue.Confirm || cue == PlayerMenuSfxCue.ChainlingSelect
                    ? CombatUiSfxCue.Confirm
                    : CombatUiSfxCue.Navigate;
            combatSfx.PlayCue(combatCue);
        }

        private void EnsureBuilt()
        {
            if (IsPrefabAuthored)
            {
                CacheSegmentButtonsFromRoot();
                AttachFocusVisuals();
                return;
            }

            PlayerMenuUiFactory.Stretch((RectTransform)transform);
            Image overlay = PlayerMenuUiFactory.GetOrAdd<Image>(gameObject);
            overlay.color = new Color(0.025f, 0.018f, 0.025f, 0.82f);
            overlay.raycastTarget = true;

            Image book = PlayerMenuUiFactory.CreateImage("BookPage", transform, new Color(0.34f, 0.23f, 0.16f, 0.99f));
            bookPageRoot = book.rectTransform;
            // Controller-HUD Profile A: Unified dossier. These anchors preserve the
            // central one-page read while keeping all interaction clear of both
            // lower controller clusters.
            bookPageRoot.anchorMin = new Vector2(0.20625f, 0.01389f);
            bookPageRoot.anchorMax = new Vector2(0.79375f, 0.95139f);
            bookPageRoot.offsetMin = Vector2.zero;
            bookPageRoot.offsetMax = Vector2.zero;
            bookPageGroup = book.gameObject.AddComponent<CanvasGroup>();
            Shadow shadow = book.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.58f);
            shadow.effectDistance = new Vector2(10f, -12f);

            var bookLayout = PlayerMenuUiFactory.VerticalLayout(book.gameObject, 8f);
            bookLayout.padding = new RectOffset(18, 18, 14, 14);
            bookLayout.childControlWidth = true;
            bookLayout.childControlHeight = false;
            bookLayout.childForceExpandWidth = true;
            bookLayout.childForceExpandHeight = false;

            RectTransform header = PlayerMenuUiFactory.CreateRect("DossierHeader", book.transform);
            PlayerMenuUiFactory.LayoutElement(header.gameObject, preferredHeight: 104f, flexibleWidth: 1f);
            var headerLayout = PlayerMenuUiFactory.HorizontalLayout(header.gameObject, 14f);
            headerLayout.childForceExpandWidth = false;

            Image portraitBack = PlayerMenuUiFactory.CreateImage("PortraitBack", header, new Color(0.17f, 0.12f, 0.10f, 1f));
            PlayerMenuUiFactory.LayoutElement(portraitBack.gameObject, preferredWidth: 104f, preferredHeight: 104f);
            portraitImage = PlayerMenuUiFactory.CreateImage("Portrait", portraitBack.transform, Color.clear);
            PlayerMenuUiFactory.Stretch(portraitImage.rectTransform, 8f, 8f, 8f, 8f);

            RectTransform headerText = PlayerMenuUiFactory.CreateRect("HeaderText", header);
            PlayerMenuUiFactory.VerticalLayout(headerText.gameObject, 2f);
            PlayerMenuUiFactory.LayoutElement(headerText.gameObject, flexibleWidth: 1f);
            kickerLabel = PlayerMenuUiFactory.CreateText("Kicker", headerText, "CHAINLING FIELD DOSSIER", 16f, TextAlignmentOptions.MidlineLeft, false);
            kickerLabel.color = new Color(0.83f, 0.48f, 0.23f, 1f);
            PlayerMenuUiFactory.LayoutElement(kickerLabel.gameObject, preferredHeight: 22f);
            nameLabel = PlayerMenuUiFactory.CreateText("Name", headerText, "CHAINLING", 32f, TextAlignmentOptions.MidlineLeft, false);
            nameLabel.color = new Color(0.99f, 0.91f, 0.75f, 1f);
            PlayerMenuUiFactory.LayoutElement(nameLabel.gameObject, preferredHeight: 46f);
            metaLabel = PlayerMenuUiFactory.CreateText("Meta", headerText, string.Empty, 17f, TextAlignmentOptions.MidlineLeft, false);
            metaLabel.color = new Color(0.75f, 0.65f, 0.52f, 1f);
            PlayerMenuUiFactory.LayoutElement(metaLabel.gameObject, preferredHeight: 30f);

            segmentRoot = PlayerMenuUiFactory.CreateRect("DossierTabs", book.transform);
            PlayerMenuUiFactory.LayoutElement(segmentRoot.gameObject, preferredHeight: 60f, flexibleWidth: 1f);
            var tabLayout = PlayerMenuUiFactory.HorizontalLayout(segmentRoot.gameObject, 8f);
            tabLayout.childForceExpandWidth = true;
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                segmentButtons[i] = PlayerMenuUiFactory.CreateButton($"Tab_{i:D2}", segmentRoot, string.Empty, new Color(0.25f, 0.18f, 0.13f, 1f));
                PlayerMenuUiFactory.LayoutElement(segmentButtons[i].gameObject, preferredHeight: 60f, flexibleWidth: 1f);
            }

            panelRoot = PlayerMenuUiFactory.CreateRect("DossierPages", book.transform);
            PlayerMenuUiFactory.LayoutElement(panelRoot.gameObject, preferredHeight: 0f, flexibleWidth: 1f, flexibleHeight: 1f);
            overviewPanel = CreatePanel<ChainlingOverviewPanel>("SummaryPage");
            movePanel = CreatePanel<ChainlingMoveEquipPanel>("MovesPage");
            learnablePanel = CreatePanel<ChainlingLearnableMovesPanel>("LearnablePage");
            stylePanel = CreatePanel<ChainlingStylePanel>("StylePage");
            progressionPanel = CreatePanel<ChainlingProgressionPanel>("GrowthPage");
            overviewPanel.BuildDefaultLayoutForEditor();
            learnablePanel.BuildDefaultLayoutForEditor();

            RectTransform footer = PlayerMenuUiFactory.CreateRect("DossierFooter", book.transform);
            PlayerMenuUiFactory.LayoutElement(footer.gameObject, preferredHeight: 64f, flexibleWidth: 1f);
            closeButton = PlayerMenuUiFactory.CreateButton("ContinueButton", footer, "BACK", new Color(0.78f, 0.38f, 0.16f, 1f));
            PlayerMenuUiFactory.LayoutElement(closeButton.gameObject, preferredHeight: 64f);
            RectTransform closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.36f, 0f);
            closeRect.anchorMax = new Vector2(0.64f, 1f);
            closeRect.offsetMin = Vector2.zero;
            closeRect.offsetMax = Vector2.zero;
            closeButtonLabel = closeButton.GetComponentInChildren<TMP_Text>(true);

            RefreshSegments();
            AttachFocusVisuals();
        }

        private void AttachFocusVisuals()
        {
            Color dossierFocus = new Color(1f, 0.52f, 0.22f, 0.98f);
            for (int i = 0; i < segmentButtons.Length; i++)
            {
                Button button = segmentButtons[i];
                if (button == null || button.GetComponent<ChainlingIndexRow>() != null)
                    continue;
                MenuSelectableFocusVisual.EnsureOn(button, dossierFocus);
            }
            if (closeButton != null) MenuSelectableFocusVisual.EnsureOn(closeButton, dossierFocus);
        }

        private void CacheSegmentButtonsFromRoot()
        {
            if (segmentRoot == null) return;
            for (int i = 0; i < segmentButtons.Length && i < segmentRoot.childCount; i++)
                if (segmentButtons[i] == null) segmentButtons[i] = segmentRoot.GetChild(i).GetComponent<Button>();
        }

        private T CreatePanel<T>(string panelName) where T : Component
        {
            Image background = PlayerMenuUiFactory.CreateImage(panelName, panelRoot, new Color(0.11f, 0.075f, 0.06f, 0.96f));
            PlayerMenuUiFactory.Stretch(background.rectTransform);
            return background.gameObject.AddComponent<T>();
        }
    }
}
