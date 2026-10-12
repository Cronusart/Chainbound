using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Chainbound.Combat.Core;
using DG.Tweening;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public sealed class CombatPostBattleReportController : MonoBehaviour, ICombatPostBattlePresentation
    {
        private enum FlowPage
        {
            Banner,
            Report,
            Rewards
        }

        [Header("Authored Overlay")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private OutcomeBannerProfileSO bannerProfile;
        [SerializeField] private bool buildFallbackVisualsWhenMissing = true;

        [Header("Banner")]
        [SerializeField] private GameObject bannerSurface;
        [SerializeField] private RectTransform bannerCard;
        [SerializeField] private CanvasGroup bannerContentsGroup;
        [SerializeField] private TextMeshProUGUI bannerTitleText;
        [SerializeField] private Image bannerImage;
        [SerializeField] private TextMeshProUGUI bannerPromptText;
        [SerializeField] private Button bannerContinueButton;

        [Header("Report")]
        [SerializeField] private GameObject reportSurface;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI contentText;
        [SerializeField] private Button summaryTabButton;
        [SerializeField] private Button battleLogTabButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private ScrollRect contentScroll;

        [Header("Rewards")]
        [SerializeField] private GameObject rewardsSurface;
        [SerializeField] private Transform xpRowsRoot;
        [SerializeField] private TextMeshProUGUI ramLabel;
        [SerializeField] private TextMeshProUGUI rewardsBodyText;
        [SerializeField] private GameObject lootRow;
        [SerializeField] private TextMeshProUGUI lootLabel;
        [SerializeField] private Button rewardsContinueButton;

        [Header("XP Rows")]
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private CompactStarRatingArtSO compactStarArt;
        [SerializeField] private CycleIconPaletteSO cycleIconPalette;
        [SerializeField] private Sprite xpRowBackground;
        [SerializeField] private Sprite xpBarSprite;
        [SerializeField] private TMP_FontAsset xpRowFont;

        [Header("Progression Folio")]
        [SerializeField] private CombatProgressionFolioRevealView progressionFolioReveal;
        [SerializeField] private ProgressionFolioPresentationSO progressionFolioPresentation;

        private CombatBattleController _battleController;
        private Combat2BattlefieldPresenter _presenter;
        private GameObject _overlay;
        private GameObject _bannerSurface;
        private RectTransform _bannerCard;
        private CanvasGroup _bannerContents;
        private Vector2 _bannerCardRestPosition;
        private Vector3 _bannerCardRestScale = Vector3.one;
        private bool _bannerCardRestCached;
        private GameObject _reportSurface;
        private CanvasGroup _reportGroup;
        private Vector3 _reportRestScale = Vector3.one;
        private bool _reportRestCached;
        private GameObject _rewardsSurface;
        private CanvasGroup _rewardsGroup;
        private Vector3 _rewardsRestScale = Vector3.one;
        private bool _rewardsRestCached;
        private bool _rewardsAuthored;
        private float _lastRamTickUnscaledTime = -1f;
        private readonly List<RewardRevealPiece> _rewardPieces = new List<RewardRevealPiece>();
        private TextMeshProUGUI _bannerTitle;
        private Image _bannerImage;
        private TextMeshProUGUI _bannerPrompt;
        private Button _bannerContinue;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _content;
        private Button _summaryTab;
        private Button _logTab;
        private Button _continueButton;
        private ScrollRect _scroll;
        private Transform _xpRowsRoot;
        private TextMeshProUGUI _ramLabel;
        private TextMeshProUGUI _rewardsBody;
        private GameObject _lootRow;
        private TextMeshProUGUI _lootLabel;
        private Button _rewardsContinue;
        private bool _showingLog;
        private bool _dismissRequested;
        private bool _complete = true;
        private bool _bannerUnlocked;
        private BattleResult _result;
        private BattleOutcomePresentation _outcome;
        private BattleRewardSummary _rewardSummary;
        private PreparedBattleRewards _preparedRewards;
        private InputLockHandle _inputLock;
        private MobileInputManager _mobileInput;
        private bool _latchedCancel;
        private bool _latchedConfirm;
        private GameObject _lastFocusedButton;
        private Image _logOverflowFade;
        private Image _rewardsIntroPlate;
        private Image _rewardsSpoilsPlate;
        private ScrollRect _rewardsScroll;
        private Image _rewardsOverflowFade;
        private Sprite[] _bannerFrames;
        private int _bannerFrameIndex;
        private float _bannerFrameTimer;
        private bool _bannerAnimating;
        private int _ramRecoveryVersion;
        private readonly List<Tween> _activeTweens = new List<Tween>();
        private readonly List<CombatPostBattleXpRowView> _xpRows = new List<CombatPostBattleXpRowView>();
        private readonly List<ChainlingXpReward> _xpRowRewards = new List<ChainlingXpReward>();

        private const string XpRowTemplateName = "XpRowTemplate";
        private const float RewardsRowAppearDelay = 0.16f;
        private const float RewardsRowStagger = 0.1f;
        private const float RewardsXpFillDuration = 0.5f;
        private const float RewardsStarUpDuration = 0.5f;

        public bool IsPresentationComplete => _complete;
        public bool DidPresentThisBattle { get; private set; }
        public bool WillPresentBattleReport =>
            _battleController != null &&
            (_battleController.CurrentEncounter?.EffectiveUiMode ?? CombatUiPresentationMode.Full) != CombatUiPresentationMode.Hidden;
        public event Action<BattleResult> OnReportDismissed;

        public void SetPreparedRewards(PreparedBattleRewards prepared)
        {
            if (prepared == null)
                return;
            _preparedRewards = prepared;
            if (prepared.summary != null)
                _rewardSummary = prepared.summary;
        }

        private void Awake()
        {
            EnsureVisuals();
            if (_overlay != null)
                _overlay.SetActive(false);
        }

        private void OnDisable()
        {
            _ramRecoveryVersion++;
            Unbind();
            KillTweens();
            ReleaseInputLock();
            if (_overlay != null)
                _overlay.SetActive(false);
            _complete = true;
            _bannerAnimating = false;
        }

        private void Update()
        {
            TickBannerAnimation();
        }

        public void Bind(CombatBattleController controller, Combat2BattlefieldPresenter presenter)
        {
            if (_battleController == controller && _presenter == presenter)
                return;
            Unbind();
            _battleController = controller;
            _presenter = presenter;
            if (_battleController != null)
                _battleController.OnBattleEnded += HandleBattleEnded;
        }

        public void BindAuthoredVisuals(
            GameObject overlay,
            TextMeshProUGUI title,
            TextMeshProUGUI content,
            Button summaryTab,
            Button logTab,
            Button continueBtn,
            ScrollRect scroll = null)
        {
            overlayRoot = overlay;
            titleText = title;
            contentText = content;
            summaryTabButton = summaryTab;
            battleLogTabButton = logTab;
            continueButton = continueBtn;
            contentScroll = scroll;
            _overlay = null;
            EnsureVisuals();
            if (_overlay != null)
                _overlay.SetActive(false);
        }

        public IEnumerator WaitForCompletion()
        {
            while (!_complete)
                yield return null;
        }

        public void Dismiss()
        {
            _dismissRequested = true;
        }

        public static CombatPostBattleReportController FindOrCreate(
            Combat2BattlefieldPresenter presenter,
            CombatBattleController battleController)
        {
            CombatPostBattleReportController existing = FindFirstObjectByType<CombatPostBattleReportController>(FindObjectsInactive.Include);
            if (existing == null)
            {
                Canvas canvas = FindCombatCanvas();
                GameObject host = canvas != null ? canvas.gameObject : presenter.gameObject;
                existing = host.AddComponent<CombatPostBattleReportController>();
            }
            existing.Bind(battleController, presenter);
            return existing;
        }

        private void HandleBattleEnded(BattleResult result)
        {
            if (_preparedRewards == null || !ReferenceEquals(_preparedRewards.result, result))
            {
                _preparedRewards = null;
                _rewardSummary = null;
            }
            _result = result;
            _outcome = BattleOutcomePresentation.Resolve(result);
            ApplyRamSettlement(result != null ? result.authoritativeRamSettlement : null);
            RecoverRamSettlementIfNeeded(result, ++_ramRecoveryVersion);
            DidPresentThisBattle = false;
            CombatUiPresentationMode mode = _battleController?.CurrentEncounter?.EffectiveUiMode ?? CombatUiPresentationMode.Full;
            if (mode == CombatUiPresentationMode.Hidden ||
                (result != null && result.skipPostBattlePresentation))
            {
                _complete = true;
                if (_battleController == null || !_battleController.HasRewardsRouterOverride)
                    OnReportDismissed?.Invoke(_result);
                return;
            }

            _complete = false;
            StartCoroutine(ShowWhenReady());
        }

        private IEnumerator ShowWhenReady()
        {
            yield return null;
            if (_presenter != null && (_result == null || !_result.wasFled))
            {
                float timeout = 8f;
                float elapsed = 0f;
                while (_presenter != null && !_presenter.IsEventQueueIdle && elapsed < timeout)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (!EnsureVisuals())
            {
                _complete = true;
                if (_battleController == null || !_battleController.HasRewardsRouterOverride)
                    OnReportDismissed?.Invoke(_result);
                yield break;
            }

            DidPresentThisBattle = true;
            _showingLog = false;
            AcquireInputLock();
            ResolveCombatSfx()?.PlayPostBattle(_result);

            yield return RunPage(FlowPage.Banner);
            yield return RunPage(FlowPage.Report);
            if (_outcome.ShowRewardsPage)
                yield return RunPage(FlowPage.Rewards);

            ResolveCombatSfx()?.FadeOutPostBattleSting();
            KillTweens();
            _bannerAnimating = false;
            if (_overlay != null)
                _overlay.SetActive(false);
            ReleaseInputLock();
            _complete = true;
            if (_battleController == null || !_battleController.HasRewardsRouterOverride)
                OnReportDismissed?.Invoke(_result);
        }

        private IEnumerator RunPage(FlowPage page)
        {
            _dismissRequested = false;
            _latchedCancel = false;
            _latchedConfirm = false;
            _lastFocusedButton = null;
            ShowPage(page);
            yield return PlayPageEnter(page);
            try
            {
                if (page == FlowPage.Rewards)
                {
                    SetAdvanceButtonInteractable(_rewardsContinue, false);
                    yield return PlayRewardsRowReveal();
                    SetAdvanceButtonInteractable(_rewardsContinue, true);
                    EnablePageInteraction(_rewardsGroup);
                    FocusButton(_rewardsContinue);
                }
                else if (page == FlowPage.Report)
                {
                    EnablePageInteraction(_reportGroup);
                    FocusButton(_continueButton);
                }

                if (page == FlowPage.Banner)
                {
                    StartBannerTitleFrames();
                    _bannerUnlocked = false;
                    float lockout = bannerProfile != null ? Mathf.Max(0f, bannerProfile.initialLockoutSeconds) : 0.8f;
                    if (_bannerPrompt != null)
                        _bannerPrompt.gameObject.SetActive(false);
                    if (lockout > 0f)
                        yield return new WaitForSecondsRealtime(lockout);
                    _bannerUnlocked = true;
                    _latchedCancel = false;
                    _latchedConfirm = false;
                    if (_bannerPrompt != null)
                    {
                        _bannerPrompt.gameObject.SetActive(true);
                        if (string.IsNullOrWhiteSpace(_bannerPrompt.text))
                            _bannerPrompt.text = "PRESS ANY BUTTON TO CONTINUE";
                    }
                }

                yield return null;
                while (!_dismissRequested)
                {
                    if (page == FlowPage.Banner)
                    {
                        if (_bannerUnlocked && BannerAdvancePressed())
                            _dismissRequested = true;
                    }
                    else if (PageAdvancePressed(page))
                    {
                        _dismissRequested = true;
                    }
                    else
                    {
                        if (page == FlowPage.Report)
                            TickReportScroll();
                        else if (page == FlowPage.Rewards)
                            TickRewardsScroll();
                        SyncFocusedButtonVisual();
                    }

                    yield return null;
                }
            }
            finally
            {
                RestorePageAdvance(page);
            }

            if (page == FlowPage.Rewards)
                ResolveCombatSfx()?.PlayRewardApplied(_rewardSummary);

            yield return PlayPageExit(page);
        }

        private void ShowPage(FlowPage page)
        {
            if (_overlay != null && !_overlay.activeSelf)
            {
                _overlay.SetActive(true);
                CanvasGroup group = _overlay.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = 1f;
                    group.interactable = true;
                    group.blocksRaycasts = true;
                }
            }

            if (_bannerSurface != null)
                _bannerSurface.SetActive(page == FlowPage.Banner);
            if (_reportSurface != null)
                _reportSurface.SetActive(page == FlowPage.Report);
            if (_rewardsSurface != null)
                _rewardsSurface.SetActive(page == FlowPage.Rewards);

            switch (page)
            {
                case FlowPage.Banner:
                    BindBanner();
                    CacheBannerCardRest();
                    PrepareBannerEnterStart();
                    break;
                case FlowPage.Report:
                    _bannerAnimating = false;
                    CacheMenuPanelRest(_reportSurface, ref _reportGroup, ref _reportRestScale, ref _reportRestCached);
                    RefreshContent();
                    ConfigureAdvanceNavigation();
                    CombatMenuPanelReveal.ApplyClosedState(
                        _reportSurface != null ? _reportSurface.transform as RectTransform : null,
                        _reportRestScale,
                        _reportSurface != null ? _reportSurface.transform as RectTransform : null,
                        _reportRestScale,
                        _reportGroup,
                        CombatMenuPanelReveal.Settings.Default);
                    break;
                case FlowPage.Rewards:
                    _bannerAnimating = false;
                    CacheMenuPanelRest(_rewardsSurface, ref _rewardsGroup, ref _rewardsRestScale, ref _rewardsRestCached);
                    BindRewards();
                    ConfigureAdvanceNavigation();
                    if (_rewardsAuthored)
                        PrepareAuthoredRewardsReveal();
                    else
                    {
                        CombatMenuPanelReveal.ApplyClosedState(
                            _rewardsSurface != null ? _rewardsSurface.transform as RectTransform : null,
                            _rewardsRestScale,
                            _rewardsSurface != null ? _rewardsSurface.transform as RectTransform : null,
                            _rewardsRestScale,
                            _rewardsGroup,
                            CombatMenuPanelReveal.Settings.Default);
                    }
                    break;
            }
        }

        private IEnumerator PlayPageEnter(FlowPage page)
        {
            Sequence sequence = null;
            switch (page)
            {
                case FlowPage.Banner:
                    if (_bannerCard != null)
                    {
                        sequence = CombatBannerStripMotion.BuildEnter(
                            _bannerCard,
                            _bannerCardRestPosition,
                            _bannerCardRestScale,
                            ResolveBannerStripSettings(),
                            contents: _bannerContents);
                    }
                    break;
                case FlowPage.Report:
                    sequence = CombatMenuPanelReveal.BuildOpen(
                        _reportSurface != null ? _reportSurface.transform as RectTransform : null,
                        _reportRestScale,
                        _reportSurface != null ? _reportSurface.transform as RectTransform : null,
                        _reportRestScale,
                        _reportGroup,
                        CombatMenuPanelReveal.Settings.Default);
                    break;
                case FlowPage.Rewards:
                    sequence = _rewardsAuthored
                        ? BuildAuthoredRewardsReveal()
                        : CombatMenuPanelReveal.BuildOpen(
                            _rewardsSurface != null ? _rewardsSurface.transform as RectTransform : null,
                            _rewardsRestScale,
                            _rewardsSurface != null ? _rewardsSurface.transform as RectTransform : null,
                            _rewardsRestScale,
                            _rewardsGroup,
                            CombatMenuPanelReveal.Settings.Default);
                    break;
            }

            if (sequence == null)
                yield break;

            _activeTweens.Add(sequence);
            float timeout = page == FlowPage.Rewards && _rewardsAuthored ? 8f : 4f;
            yield return AwaitTween(sequence, timeout);
        }

        private IEnumerator PlayPageExit(FlowPage page)
        {
            Sequence sequence = null;
            switch (page)
            {
                case FlowPage.Banner:
                    _bannerAnimating = false;
                    if (_bannerCard != null)
                    {
                        sequence = CombatBannerStripMotion.BuildExit(
                            _bannerCard,
                            _bannerContents,
                            _bannerCardRestPosition,
                            _bannerCardRestScale,
                            ResolveBannerStripSettings());
                    }
                    break;
                case FlowPage.Report:
                    sequence = CombatMenuPanelReveal.BuildClose(
                        _reportSurface != null ? _reportSurface.transform as RectTransform : null,
                        _reportRestScale,
                        _reportSurface != null ? _reportSurface.transform as RectTransform : null,
                        _reportRestScale,
                        _reportGroup,
                        CombatMenuPanelReveal.Settings.Default);
                    break;
                case FlowPage.Rewards:
                    if (_rewardsAuthored && _rewardsGroup != null)
                    {
                        sequence = DOTween.Sequence().SetUpdate(true);
                        sequence.Append(_rewardsGroup.DOFade(0f, 0.16f).SetEase(Ease.InSine));
                    }
                    else
                    {
                        sequence = CombatMenuPanelReveal.BuildClose(
                            _rewardsSurface != null ? _rewardsSurface.transform as RectTransform : null,
                            _rewardsRestScale,
                            _rewardsSurface != null ? _rewardsSurface.transform as RectTransform : null,
                            _rewardsRestScale,
                            _rewardsGroup,
                            CombatMenuPanelReveal.Settings.Default);
                    }
                    break;
            }

            if (sequence != null)
            {
                _activeTweens.Add(sequence);
                yield return AwaitTween(sequence);
            }

            RestorePageRest(page);
            if (page == FlowPage.Banner && _bannerSurface != null)
                _bannerSurface.SetActive(false);
            if (page == FlowPage.Report && _reportSurface != null)
                _reportSurface.SetActive(false);
            if (page == FlowPage.Rewards && _rewardsSurface != null)
                _rewardsSurface.SetActive(false);
        }

        private void BindBanner()
        {
            _bannerFrames = bannerProfile != null ? bannerProfile.GetFrames(_outcome) : null;
            bool hasFrames = OutcomeBannerProfileSO.HasFrames(_bannerFrames);
            _bannerAnimating = false;

            if (_bannerTitle != null)
            {
                _bannerTitle.text = _outcome.BannerTitle;
                _bannerTitle.gameObject.SetActive(!hasFrames);
            }

            if (_bannerImage != null)
            {
                _bannerImage.gameObject.SetActive(hasFrames);
                if (hasFrames)
                    _bannerImage.sprite = _bannerFrames[0];
            }
        }

        private void StartBannerTitleFrames()
        {
            if (!OutcomeBannerProfileSO.HasFrames(_bannerFrames) || _bannerImage == null)
                return;

            _bannerFrameIndex = 0;
            _bannerFrameTimer = 0f;
            _bannerImage.sprite = _bannerFrames[0];
            _bannerAnimating = _bannerFrames.Length > 1;
        }

        private CombatBannerStripMotion.Settings ResolveBannerStripSettings()
        {
            CombatBannerStripMotion.Settings settings = CombatBannerStripMotion.Settings.PostBattle;
            if (bannerProfile != null)
                settings.stripScaleY = Mathf.Clamp(bannerProfile.stripScaleY, 0.02f, 0.25f);
            return settings;
        }

        private void CacheBannerCardRest()
        {
            if (_bannerCard == null)
                ResolveBannerCard();
            if (_bannerCard == null || _bannerCardRestCached)
                return;

            Canvas.ForceUpdateCanvases();
            _bannerCardRestPosition = _bannerCard.anchoredPosition;
            _bannerCardRestScale = _bannerCard.localScale;
            _bannerCardRestCached = true;
        }

        private void PrepareBannerEnterStart()
        {
            if (_bannerCard == null)
                return;
            CombatBannerStripMotion.ApplyEnterStart(
                _bannerCard,
                _bannerCardRestPosition,
                _bannerCardRestScale,
                ResolveBannerStripSettings());
            if (_bannerContents != null)
                _bannerContents.alpha = 0f;
        }

        private void CacheMenuPanelRest(
            GameObject surface,
            ref CanvasGroup group,
            ref Vector3 restScale,
            ref bool cached)
        {
            if (surface == null)
                return;

            if (group == null)
                group = surface.GetComponent<CanvasGroup>() ?? surface.AddComponent<CanvasGroup>();

            if (cached)
                return;

            restScale = surface.transform.localScale;
            if (restScale.sqrMagnitude < 0.0001f)
                restScale = Vector3.one;
            cached = true;
        }

        private void RestorePageRest(FlowPage page)
        {
            switch (page)
            {
                case FlowPage.Banner:
                    if (_bannerCard != null)
                    {
                        _bannerCard.anchoredPosition = _bannerCardRestPosition;
                        _bannerCard.localScale = _bannerCardRestScale;
                    }
                    if (_bannerContents != null)
                        _bannerContents.alpha = 1f;
                    break;
                case FlowPage.Report:
                    if (_reportSurface != null)
                        _reportSurface.transform.localScale = _reportRestScale;
                    if (_reportGroup != null)
                    {
                        _reportGroup.alpha = 1f;
                        _reportGroup.interactable = true;
                    }
                    break;
                case FlowPage.Rewards:
                    RestoreAuthoredRewardsReveal();
                    if (_rewardsSurface != null)
                        _rewardsSurface.transform.localScale = _rewardsRestScale;
                    if (_rewardsGroup != null)
                    {
                        _rewardsGroup.alpha = 1f;
                        _rewardsGroup.interactable = true;
                    }
                    break;
            }
        }

        private void ResolveBannerCard()
        {
            if (_bannerCard == null && bannerCard != null)
                _bannerCard = bannerCard;
            if (_bannerCard == null && _bannerSurface != null)
            {
                Transform card = _bannerSurface.transform.Find("BannerCard");
                if (card != null)
                    _bannerCard = card as RectTransform;
                if (_bannerCard == null)
                    _bannerCard = _bannerSurface.transform as RectTransform;
            }

            if (_bannerContents == null && bannerContentsGroup != null)
                _bannerContents = bannerContentsGroup;
            if (_bannerContents == null && _bannerCard != null)
            {
                Transform contents = _bannerCard.Find("BannerContents");
                if (contents != null)
                    _bannerContents = contents.GetComponent<CanvasGroup>();
                if (_bannerContents == null)
                    _bannerContents = _bannerCard.GetComponent<CanvasGroup>();
            }
        }

        private void TickBannerAnimation()
        {
            if (!_bannerAnimating || _bannerImage == null || !OutcomeBannerProfileSO.HasFrames(_bannerFrames) || bannerProfile == null)
                return;

            _bannerFrameTimer += Time.unscaledDeltaTime;
            float frameSeconds = 1f / Mathf.Max(0.01f, bannerProfile.fps);
            if (_bannerFrameTimer < frameSeconds)
                return;

            _bannerFrameTimer = 0f;
            _bannerFrameIndex++;
            if (_bannerFrameIndex >= _bannerFrames.Length)
            {
                _bannerFrameIndex = 0;
                if (bannerProfile.loopIntervalSeconds > 0f)
                    StartCoroutine(PauseBannerLoop(bannerProfile.loopIntervalSeconds));
            }

            _bannerImage.sprite = _bannerFrames[_bannerFrameIndex];
        }

        private IEnumerator PauseBannerLoop(float seconds)
        {
            _bannerAnimating = false;
            yield return new WaitForSeconds(seconds);
            if (_bannerSurface != null && _bannerSurface.activeSelf)
                _bannerAnimating = OutcomeBannerProfileSO.HasFrames(_bannerFrames) && _bannerFrames.Length > 1;
        }

        private void BindRewards()
        {
            KillTweens();
            if (_preparedRewards != null && ReferenceEquals(_preparedRewards.result, _result))
            {
                _rewardSummary = _preparedRewards.summary;
            }
            else if (_rewardSummary == null)
            {
                Debug.LogError("[PostBattleReport] Prepared rewards were not supplied. The report will not recalculate randomized rewards.");
                _rewardSummary = new BattleRewardSummary();
            }

            bool defeatPage = _outcome.Kind == BattleOutcomeKind.Defeat;
            if (_rewardsBody != null)
            {
                _rewardsBody.gameObject.SetActive(true);
                _rewardsBody.text = defeatPage ? BuildDefeatCopy() : BuildRewardsIntro();
            }

            BindRamTicker(defeatPage);
            BindLootRow();
            BindXpRows(defeatPage);
            EnsureRewardPlates();
        }

        private string BuildRewardsIntro()
        {
            if (_rewardSummary == null)
                return "No rewards calculated.";

            return FormatRewardsIntro(
                _outcome.DidCapture,
                _outcome.Kind,
                BattleOutcomePresentation.ResolveCapturedDisplayName(_result),
                _rewardSummary,
                _result != null ? _result.authoritativeRamSettlement : null);
        }

        private string BuildDefeatCopy()
        {
            int recoveryCount = CountKoRecoveryPreview(_result);
            PostBattleRewardsConfigSO config = BalanceConfigRuntime.PostBattleRewards;
            int recoveryPercent = config != null
                ? Mathf.RoundToInt(Mathf.Clamp01(config.defeatKoRespawnHpFraction) * 100f)
                : 0;
            return FormatDefeatCopy(recoveryCount, recoveryPercent);
        }

        public static string FormatRewardsIntro(
            bool didCapture,
            BattleOutcomeKind kind,
            string capturedDisplayName,
            BattleRewardSummary summary,
            BattleRewardSettlement settlement = null)
        {
            if (didCapture)
            {
                string name = string.IsNullOrWhiteSpace(capturedDisplayName) ? "the target" : capturedDisplayName;
                return "Recorded " + name + ". Confirm returns you to the overworld.";
            }

            if (summary == null || summary.chainlingRewards == null || summary.chainlingRewards.Count == 0)
            {
                return kind == BattleOutcomeKind.Draw
                    ? "Draw. No XP earned. Confirm returns you to the overworld."
                    : "No XP rewards available for this battle.";
            }

            if (settlement != null && string.Equals(settlement.Status, BattleRewardStatus.Settled, StringComparison.Ordinal) && settlement.AppliedRam > 0)
                return "XP fills live. RAM was granted automatically. Confirm saves XP and returns you to the overworld.";

            return "XP fills live. Confirm saves XP and returns you to the overworld.";
        }

        public static string FormatDefeatCopy(int recoveryCount, int recoveryPercent)
        {
            var builder = new StringBuilder();
            builder.AppendLine("No XP earned.");
            builder.AppendLine("Confirm returns you to your recovery point.");
            if (recoveryCount > 0)
            {
                string label = recoveryCount == 1 ? "Chainling" : "Chainlings";
                builder.Append(recoveryCount).Append(" KO'd ").Append(label)
                    .Append(" restored to ").Append(recoveryPercent).Append("% HP.");
            }

            return builder.ToString();
        }

        public static string FormatRamTicker(bool defeatPage, int ramEarned)
        {
            _ = ramEarned;
            return FormatRamTicker(defeatPage, null);
        }

        public static string FormatRamTicker(bool defeatPage, BattleRewardSettlement settlement)
        {
            if (settlement == null ||
                string.Equals(settlement.Status, BattleRewardStatus.Pending, StringComparison.Ordinal) ||
                string.Equals(settlement.Status, BattleRewardStatus.Unavailable, StringComparison.Ordinal) ||
                string.Equals(settlement.Status, BattleRewardStatus.Failed, StringComparison.Ordinal))
            {
                return "RAM reward unavailable — check your balance";
            }

            if (settlement.AppliedRam > 0)
            {
                bool capped = settlement.RequestedRam > settlement.AppliedRam;
                return capped
                    ? $"+{settlement.AppliedRam:N0} RAM — daily cap reached"
                    : $"+{settlement.AppliedRam:N0} RAM — granted";
            }

            bool dailyCapReached = string.Equals(settlement.Reason, "daily_cap_reached", StringComparison.Ordinal) ||
                                   (settlement.RequestedRam > 0 && settlement.DailyCap.HasValue &&
                                    settlement.DailyEarned.GetValueOrDefault() >= settlement.DailyCap.Value);
            if (dailyCapReached)
                return "Daily RAM cap reached";

            return defeatPage ? "No RAM lost this battle" : "RAM unchanged this battle";
        }

        private static int CountKoRecoveryPreview(BattleResult result)
        {
            if (!BattleDefeatRecoveryUtility.IsEligiblePveDefeat(result) || result.playerParticipants == null)
                return 0;

            int count = 0;
            for (int i = 0; i < result.playerParticipants.Count; i++)
            {
                BattleParticipant participant = result.playerParticipants[i];
                if (participant != null && participant.wasKOd)
                    count++;
            }

            return count;
        }

        private void BindRamTicker(bool defeatPage)
        {
            if (_ramLabel == null)
                return;

            BattleRewardSettlement settlement = _result != null ? _result.authoritativeRamSettlement : null;
            _ramLabel.text = FormatRamTicker(defeatPage, settlement);
            ApplyRamSettlement(settlement);
        }

        private static void ApplyRamSettlement(BattleRewardSettlement settlement)
        {
            GameSessionContext session = GameSessionContext.Instance;
            if (session != null)
                PlayerRamBalanceStore.ApplySettlement(session.PlayerProfileId, settlement);
        }

        private async void RecoverRamSettlementIfNeeded(BattleResult result, int recoveryVersion)
        {
            BattleRewardSettlement currentSettlement = result != null ? result.authoritativeRamSettlement : null;
            if (result == null || currentSettlement == null ||
                (!string.Equals(currentSettlement.Status, BattleRewardStatus.Pending, StringComparison.Ordinal) &&
                 !string.Equals(currentSettlement.Status, BattleRewardStatus.Unavailable, StringComparison.Ordinal)))
            {
                return;
            }

            CombatSessionEnvelope launch = AuthoritativeBattleLaunchContext.Current;
            ICombatSessionService service = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance.CombatSessionService
                : null;
            GameSessionContext session = GameSessionContext.Instance;
            string profileId = session != null ? session.PlayerProfileId : string.Empty;
            if (launch == null || string.IsNullOrWhiteSpace(launch.BattleId) || service == null ||
                !string.Equals(currentSettlement.BattleId, launch.BattleId, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                // BattleService deliberately cools down missing-receipt settlement attempts.
                // Wait through the default window so this single recovery read can perform
                // useful work instead of immediately receiving reward_retry_throttled.
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(5));
                BattleRewardSettlement recovered = await service.GetBattleRewardAsync(launch.BattleId);
                if (recovered == null ||
                    !string.Equals(recovered.BattleId, launch.BattleId, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                result.authoritativeRamSettlement = recovered;
                launch.Settlement = recovered;
                PlayerRamBalanceStore.ApplySettlement(profileId, recovered);
                if (this == null || recoveryVersion != _ramRecoveryVersion ||
                    !isActiveAndEnabled || !ReferenceEquals(_result, result) || _ramLabel == null)
                {
                    return;
                }

                _ramLabel.text = FormatRamTicker(_outcome.Kind == BattleOutcomeKind.Defeat, recovered);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PostBattleReport] RAM reward recovery remains unavailable: {exception.Message}", this);
            }
        }

        private void BindLootRow()
        {
            if (_lootRow == null && _lootLabel == null)
                return;

            string lootText = BuildLootText(_rewardSummary);
            bool show = !string.IsNullOrWhiteSpace(lootText);
            if (_lootRow != null)
                _lootRow.SetActive(show);
            if (_lootLabel != null)
            {
                _lootLabel.gameObject.SetActive(show);
                _lootLabel.text = lootText;
            }
        }

        private static string BuildLootText(BattleRewardSummary summary)
        {
            if (summary == null)
                return string.Empty;

            var names = new List<string>();
            AddLootName(names, summary.rareConsumableItemId);
            if (summary.bonusItemIds != null)
            {
                for (int i = 0; i < summary.bonusItemIds.Count; i++)
                    AddLootName(names, summary.bonusItemIds[i]);
            }

            if (names.Count == 0)
                return string.Empty;
            return "Loot: " + string.Join("  ·  ", names);
        }

        private static void AddLootName(List<string> names, string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return;
            string display = itemId.Trim();
            if (PlayerItemCatalogSeed.TryGet(display, out PlayerItemDefinition def) && def != null && !string.IsNullOrWhiteSpace(def.displayName))
                display = def.displayName;
            if (!names.Contains(display))
                names.Add(display);
        }

        private void BindXpRows(bool defeatPage)
        {
            if (_xpRowsRoot == null)
                return;

            _xpRows.Clear();
            _xpRowRewards.Clear();
            Transform template = _xpRowsRoot.Find(XpRowTemplateName);
            if (template != null)
                template.gameObject.SetActive(false);
            for (int i = _xpRowsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = _xpRowsRoot.GetChild(i);
                if (child.name == XpRowTemplateName)
                    continue;
                Destroy(child.gameObject);
            }

            if (defeatPage || _rewardSummary?.chainlingRewards == null)
                return;

            ProgressionConfigSO progression = BalanceConfigRuntime.Progression;
            SpeciesDatabaseSO speciesDb = _battleController != null ? _battleController.SpeciesDatabase : null;
            TMP_FontAsset font = xpRowFont != null
                ? xpRowFont
                : Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
            if (!_rewardsAuthored)
                ConfigureXpRowsLayout();
            for (int i = 0; i < _rewardSummary.chainlingRewards.Count; i++)
            {
                ChainlingXpReward reward = _rewardSummary.chainlingRewards[i];
                if (reward == null)
                    continue;
                CreateXpRow(reward, progression, speciesDb, font);
            }

            EnsureRewardsListScroll();
        }

        private void ConfigureXpRowsLayout()
        {
            if (_xpRowsRoot == null || _xpRowsRoot.GetComponent<VerticalLayoutGroup>() != null)
                return;

            VerticalLayoutGroup layout = _xpRowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        /// <summary>
        /// Plates, scroll viewports, and fades stay on the prefab. Missing ones are left missing
        /// so runtime does not invent a second layout on top of the authored rewards surface.
        /// </summary>
        private void EnsureRewardPlates()
        {
            if (_rewardsSurface == null)
                return;

            _rewardsIntroPlate = FindRewardPlate("RewardsIntroPlate");
            _rewardsSpoilsPlate = FindRewardPlate("RewardsSpoilsPlate");
        }

        private Image FindRewardPlate(string name)
        {
            Transform found = _rewardsSurface.transform.Find(name);
            return found != null ? found.GetComponent<Image>() : null;
        }

        private void EnsureRewardsListScroll()
        {
            if (_xpRowsRoot == null)
                return;

            RectTransform content = (RectTransform)_xpRowsRoot.transform;
            if (content.parent == null || content.parent.name != "RewardsListViewport")
                return;

            RectTransform viewportRect = (RectTransform)content.parent;
            ScrollRect scroll = viewportRect.GetComponent<ScrollRect>();
            if (scroll == null)
                return;

            scroll.content = content;
            scroll.viewport = viewportRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            _rewardsScroll = scroll;

            Transform existingFade = viewportRect.Find("RewardsOverflowFade");
            _rewardsOverflowFade = existingFade != null ? existingFade.GetComponent<Image>() : null;
            if (_rewardsOverflowFade != null)
                _rewardsOverflowFade.enabled = false;
        }

        private void UpdateRewardsOverflowFade()
        {
            if (_rewardsOverflowFade == null || _rewardsScroll == null || _rewardsScroll.content == null || _rewardsScroll.viewport == null)
                return;

            float range = _rewardsScroll.content.rect.height - _rewardsScroll.viewport.rect.height;
            bool moreBelow = range > 8f && _rewardsScroll.verticalNormalizedPosition > 0.02f;
            _rewardsOverflowFade.enabled = moreBelow;
        }

        private void CreateXpRow(
            ChainlingXpReward reward,
            ProgressionConfigSO progression,
            SpeciesDatabaseSO speciesDb,
            TMP_FontAsset font)
        {
            CombatPostBattleXpRowView row = InstantiateXpRow(font);
            if (row == null)
                return;

            BattleParticipant participant = FindParticipant(reward);
            SpeciesDefinitionSO species = speciesDb != null && !string.IsNullOrWhiteSpace(reward.speciesId)
                ? speciesDb.GetById(reward.speciesId)
                : null;
            row.Bind(
                reward,
                participant,
                species,
                typeIconPalette,
                compactStarArt,
                cycleIconPalette,
                progression,
                _rewardsAuthored);
            _xpRows.Add(row);
            _xpRowRewards.Add(reward);
        }

        private CombatPostBattleXpRowView InstantiateXpRow(TMP_FontAsset font)
        {
            Transform template = _xpRowsRoot != null ? _xpRowsRoot.Find(XpRowTemplateName) : null;
            if (template == null)
            {
                if (_rewardsAuthored)
                    return null;

                return CombatPostBattleXpRowView.Create(
                    _xpRowsRoot,
                    xpRowBackground,
                    xpBarSprite,
                    font);
            }

            GameObject copy = Instantiate(template.gameObject, _xpRowsRoot);
            copy.name = "XpRow";
            copy.SetActive(true);
            return copy.GetComponent<CombatPostBattleXpRowView>();
        }

        private IEnumerator PlayRewardsRowReveal()
        {
            if (_xpRows.Count == 0)
                yield break;

            if (!_rewardsAuthored)
            {
                Sequence appear = DOTween.Sequence().SetUpdate(true);
                appear.AppendInterval(RewardsRowAppearDelay);
                for (int i = 0; i < _xpRows.Count; i++)
                {
                    CombatPostBattleXpRowView row = _xpRows[i];
                    if (row == null)
                        continue;
                    Tween rowAppear = row.PlayAppear();
                    if (rowAppear == null)
                        continue;
                    appear.Insert(RewardsRowAppearDelay + (i * RewardsRowStagger), rowAppear);
                }

                _activeTweens.Add(appear);
                yield return AwaitTween(appear);
            }

            bool anyXp = false;
            Sequence fills = DOTween.Sequence().SetUpdate(true);
            for (int i = 0; i < _xpRows.Count; i++)
            {
                CombatPostBattleXpRowView row = _xpRows[i];
                if (row == null)
                    continue;
                if (row.HasXpProgress)
                    anyXp = true;
                Tween fill = row.PlayXpFill(RewardsXpFillDuration);
                if (fill == null)
                    continue;
                if (_rewardsAuthored)
                    fills.Insert(i * 0.08f, fill);
                else
                    fills.Join(fill);
            }

            if (anyXp)
                ResolveCombatSfx()?.PlayXpShare(_rewardSummary);

            if (anyXp)
            {
                _activeTweens.Add(fills);
                yield return AwaitTween(fills);
            }
            else
            {
                fills.Kill();
            }

            yield return PlayProgressionEventQueue();
        }

        private IEnumerator PlayProgressionEventQueue()
        {
            if (_preparedRewards != null && _preparedRewards.progressionCompletion != null)
            {
                while (!_preparedRewards.progressionCompletion.IsCompleted)
                    yield return null;
            }

            for (int rowIndex = 0; rowIndex < _xpRows.Count && rowIndex < _xpRowRewards.Count; rowIndex++)
            {
                CombatPostBattleXpRowView row = _xpRows[rowIndex];
                ChainlingXpReward reward = _xpRowRewards[rowIndex];
                if (row == null || reward?.progressionGrants == null)
                    continue;

                int starStep = 0;
                int masteryStep = 0;
                for (int eventIndex = 0; eventIndex < reward.progressionGrants.Count; eventIndex++)
                {
                    ProgressionGrantResult grant = reward.progressionGrants[eventIndex];
                    if (grant == null)
                        continue;

                    bool mastery = grant.eventType == ProgressionGrantEventType.MasteryCycle;
                    row.SetFolioChance(grant.gatePercent, mastery);
                    Tween milestone = mastery
                        ? row.PlayMasteryCycle(masteryStep++, ResolveMasterySweepDuration())
                        : row.PlayStarUp(starStep++, RewardsStarUpDuration);

                    UiHapticsService.Instance?.TriggerSelection();
                    ResolveCombatSfx()?.PlayStarUp(_rewardSummary);
                    if (milestone != null)
                    {
                        _activeTweens.Add(milestone);
                        milestone.Play();
                        yield return AwaitTween(milestone, 6f);
                    }

                    yield return PlayFolioOutcome(grant);
                }
            }
        }

        private IEnumerator PlayFolioOutcome(ProgressionGrantResult grant)
        {
            EnsureProgressionFolioReveal();
            if (progressionFolioReveal == null)
                yield break;

            MoveDatabaseSO moveDatabase = _battleController != null ? _battleController.MoveDatabase : null;
            MoveDefinitionSO move = moveDatabase != null && !string.IsNullOrWhiteSpace(grant.moveId)
                ? moveDatabase.GetById(grant.moveId)
                : null;
            Tween reveal = progressionFolioReveal.Play(
                grant,
                move,
                typeIconPalette,
                progressionFolioPresentation);
            if (reveal == null)
                yield break;

            _activeTweens.Add(reveal);
            reveal.Play();
            bool done = false;
            reveal.OnComplete(() => done = true);
            reveal.OnKill(() => done = true);
            float elapsed = 0f;
            while (!done && reveal.IsActive() && elapsed < 12f)
            {
                if (ConsumeConfirm())
                    progressionFolioReveal.Accelerate();
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            progressionFolioReveal?.FocusContinueButton();
            while (progressionFolioReveal != null && progressionFolioReveal.AwaitingContinue && !_dismissRequested)
            {
                if (ConsumeConfirm() || ConsumeCancel())
                    progressionFolioReveal.ConfirmContinue();
                yield return null;
            }

            if (progressionFolioReveal != null && progressionFolioReveal.AwaitingContinue)
                progressionFolioReveal.ConfirmContinue();
        }

        private float ResolveMasterySweepDuration()
        {
            return progressionFolioPresentation != null
                ? Mathf.Max(0.2f, progressionFolioPresentation.masterySweepSeconds)
                : 0.8f;
        }

        private BattleParticipant FindParticipant(ChainlingXpReward reward)
        {
            if (reward == null || _result?.playerParticipants == null)
                return null;

            for (int i = 0; i < _result.playerParticipants.Count; i++)
            {
                BattleParticipant participant = _result.playerParticipants[i];
                if (participant == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(reward.instanceId) &&
                    string.Equals(participant.instanceId, reward.instanceId, StringComparison.Ordinal))
                    return participant;
                if (!string.IsNullOrWhiteSpace(reward.backendChainlingId) &&
                    string.Equals(participant.backendChainlingId, reward.backendChainlingId, StringComparison.Ordinal))
                    return participant;
            }

            return null;
        }

        private void KillTweens()
        {
            for (int i = 0; i < _activeTweens.Count; i++)
            {
                if (_activeTweens[i] != null && _activeTweens[i].IsActive())
                    _activeTweens[i].Kill();
            }
            _activeTweens.Clear();
            RestoreAuthoredRewardsReveal();
        }

        private void PrepareAuthoredRewardsReveal()
        {
            RestoreAuthoredRewardsReveal();
            if (_rewardsSurface == null)
                return;

            if (_rewardsGroup != null)
            {
                _rewardsGroup.alpha = 1f;
                _rewardsGroup.interactable = false;
            }

            Canvas.ForceUpdateCanvases();
            Transform root = _rewardsSurface.transform;
            var ordered = new List<RectTransform>(root.childCount);
            for (int i = 0; i < root.childCount; i++)
            {
                RectTransform rect = root.GetChild(i) as RectTransform;
                if (rect == null || !rect.gameObject.activeInHierarchy)
                    continue;
                if (rect.name.IndexOf("Scanline", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                ordered.Add(rect);
            }

            ordered.Sort(CompareRewardRevealOrder);
            bool countingRam = ResolveGrantedRam() > 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                RectTransform rect = ordered[i];
                CanvasGroup group = rect.GetComponent<CanvasGroup>();
                if (group == null)
                    group = rect.gameObject.AddComponent<CanvasGroup>();

                bool fullBleed = rect.anchorMin.x <= 0.02f && rect.anchorMax.x >= 0.98f &&
                                 rect.anchorMin.y <= 0.02f && rect.anchorMax.y >= 0.98f;
                var piece = new RewardRevealPiece
                {
                    Rect = rect,
                    RestPosition = rect.anchoredPosition,
                    Group = group,
                    RestAlpha = group.alpha > 0.01f ? group.alpha : 1f,
                    CountsRam = _ramLabel != null && _ramLabel.transform.IsChildOf(rect),
                    Slides = !fullBleed
                };

                if (piece.CountsRam && countingRam)
                {
                    _ramLabel.text = "+0 RAM";
                    _ramLabel.maxVisibleCharacters = 99999;
                }

                TextMeshProUGUI[] texts = rect.GetComponentsInChildren<TextMeshProUGUI>(false);
                for (int t = 0; t < texts.Length; t++)
                {
                    TextMeshProUGUI text = texts[t];
                    if (text == null || (piece.CountsRam && countingRam && text == _ramLabel))
                        continue;
                    if (string.IsNullOrEmpty(text.text))
                        continue;

                    text.ForceMeshUpdate();
                    int characters = text.textInfo != null ? text.textInfo.characterCount : 0;
                    if (characters <= 0)
                        continue;

                    text.maxVisibleCharacters = 0;
                    piece.Typewriters.Add(text);
                }

                group.alpha = 0f;
                if (piece.Slides)
                    rect.anchoredPosition = piece.RestPosition + new Vector2(0f, -28f);
                _rewardPieces.Add(piece);
            }
        }

        private Sequence BuildAuthoredRewardsReveal()
        {
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            if (_rewardPieces.Count == 0)
            {
                sequence.AppendInterval(0.01f);
                return sequence;
            }

            const float stagger = 0.12f;
            for (int i = 0; i < _rewardPieces.Count; i++)
            {
                RewardRevealPiece piece = _rewardPieces[i];
                float at = 0.04f + (i * stagger);
                if (piece.Slides && piece.Rect != null)
                {
                    sequence.Insert(
                        at,
                        piece.Rect.DOAnchorPos(piece.RestPosition, 0.34f).SetEase(Ease.OutCubic).SetUpdate(true));
                }

                if (piece.Group != null)
                {
                    sequence.Insert(
                        at,
                        piece.Group.DOFade(piece.RestAlpha, 0.24f).SetEase(Ease.OutSine).SetUpdate(true));
                }

                if (piece.CountsRam)
                {
                    Tween counter = BuildRamCountTween();
                    if (counter != null)
                        sequence.Insert(at + 0.08f, counter);
                }

                for (int t = 0; t < piece.Typewriters.Count; t++)
                {
                    Tween typewriter = BuildTypewriterTween(piece.Typewriters[t]);
                    if (typewriter != null)
                        sequence.Insert(at + 0.05f, typewriter);
                }
            }

            return sequence;
        }

        private Tween BuildRamCountTween()
        {
            int target = ResolveGrantedRam();
            if (target <= 0 || _ramLabel == null)
                return null;

            float shown = 0f;
            int lastDisplayed = 0;
            _lastRamTickUnscaledTime = -1f;
            float duration = Mathf.Clamp(0.55f + (target * 0.012f), 0.7f, 1.35f);
            bool defeatPage = _outcome.Kind == BattleOutcomeKind.Defeat;
            BattleRewardSettlement settlement = _result != null ? _result.authoritativeRamSettlement : null;
            return DOTween.To(() => shown, value =>
                {
                    shown = value;
                    int displayed = Mathf.Clamp(Mathf.RoundToInt(shown), 0, target);
                    if (_ramLabel != null)
                        _ramLabel.text = $"+{displayed:N0} RAM";
                    if (displayed == lastDisplayed)
                        return;

                    lastDisplayed = displayed;
                    float now = Time.unscaledTime;
                    if (now - _lastRamTickUnscaledTime < 0.07f)
                        return;

                    _lastRamTickUnscaledTime = now;
                    ResolveCombatSfx()?.PlayRamTick();
                }, (float)target, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (_ramLabel != null)
                        _ramLabel.text = FormatRamTicker(defeatPage, settlement);
                });
        }

        private static Tween BuildTypewriterTween(TextMeshProUGUI text)
        {
            if (text == null)
                return null;

            text.ForceMeshUpdate();
            int count = text.textInfo != null ? text.textInfo.characterCount : 0;
            if (count <= 0)
            {
                text.maxVisibleCharacters = 99999;
                return null;
            }

            float visible = 0f;
            text.maxVisibleCharacters = 0;
            float duration = Mathf.Clamp(count / 52f, 0.18f, 0.95f);
            return DOTween.To(() => visible, value =>
                {
                    visible = value;
                    if (text != null)
                        text.maxVisibleCharacters = Mathf.Clamp(Mathf.RoundToInt(value), 0, count);
                }, (float)count, duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (text != null)
                        text.maxVisibleCharacters = 99999;
                });
        }

        private void RestoreAuthoredRewardsReveal()
        {
            bool defeatPage = _outcome.Kind == BattleOutcomeKind.Defeat;
            BattleRewardSettlement settlement = _result != null ? _result.authoritativeRamSettlement : null;
            for (int i = 0; i < _rewardPieces.Count; i++)
            {
                RewardRevealPiece piece = _rewardPieces[i];
                if (piece.Rect != null)
                    piece.Rect.anchoredPosition = piece.RestPosition;
                if (piece.Group != null)
                    piece.Group.alpha = piece.RestAlpha;
                if (piece.CountsRam && _ramLabel != null)
                {
                    _ramLabel.maxVisibleCharacters = 99999;
                    _ramLabel.text = FormatRamTicker(defeatPage, settlement);
                }

                for (int t = 0; t < piece.Typewriters.Count; t++)
                {
                    if (piece.Typewriters[t] != null)
                        piece.Typewriters[t].maxVisibleCharacters = 99999;
                }
            }

            _rewardPieces.Clear();
        }

        private int ResolveGrantedRam()
        {
            BattleRewardSettlement settlement = _result != null ? _result.authoritativeRamSettlement : null;
            if (settlement == null || settlement.AppliedRam <= 0)
                return 0;
            if (string.Equals(settlement.Status, BattleRewardStatus.Pending, StringComparison.Ordinal) ||
                string.Equals(settlement.Status, BattleRewardStatus.Unavailable, StringComparison.Ordinal) ||
                string.Equals(settlement.Status, BattleRewardStatus.Failed, StringComparison.Ordinal))
                return 0;

            return settlement.AppliedRam > int.MaxValue ? int.MaxValue : (int)settlement.AppliedRam;
        }

        private static int CompareRewardRevealOrder(RectTransform a, RectTransform b)
        {
            int byTop = RewardRevealTop(b).CompareTo(RewardRevealTop(a));
            if (byTop != 0)
                return byTop;
            if (a == null || b == null)
                return 0;
            return a.GetSiblingIndex().CompareTo(b.GetSiblingIndex());
        }

        private static float RewardRevealTop(RectTransform rect)
        {
            if (rect == null)
                return 0f;

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[1].y;
        }

        private sealed class RewardRevealPiece
        {
            public RectTransform Rect;
            public Vector2 RestPosition;
            public CanvasGroup Group;
            public float RestAlpha = 1f;
            public bool CountsRam;
            public bool Slides;
            public readonly List<TextMeshProUGUI> Typewriters = new List<TextMeshProUGUI>();
        }

        private bool EnsureVisuals()
        {
            if (_overlay != null && _title != null && _content != null)
            {
                EnsureProgressionFolioReveal();
                return true;
            }

            if (TryBindAuthoredVisuals())
            {
                EnsureProgressionFolioReveal();
                return true;
            }

            if (!buildFallbackVisualsWhenMissing)
                return false;

            BuildFallbackVisuals();
            EnsureProgressionFolioReveal();
            return _overlay != null && _title != null && _content != null;
        }

        private void EnsureProgressionFolioReveal()
        {
            if (progressionFolioReveal == null && _overlay != null)
                progressionFolioReveal = FindChild(_overlay.transform, "ProgressionFolioOverlay")?.GetComponent<CombatProgressionFolioRevealView>();
            if (progressionFolioReveal == null && _overlay != null)
            {
                TMP_FontAsset font = xpRowFont != null
                    ? xpRowFont
                    : Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
                progressionFolioReveal = CombatProgressionFolioRevealView.Create(_overlay.transform, font);
            }
            progressionFolioReveal?.transform.SetAsLastSibling();
        }

        private bool TryBindAuthoredVisuals()
        {
            if (overlayRoot == null)
                return false;

            _overlay = overlayRoot;
            _bannerSurface = bannerSurface != null ? bannerSurface : FindChild(_overlay.transform, "BannerSurface");
            _reportSurface = reportSurface != null ? reportSurface : FindChild(_overlay.transform, "ReportSurface");
            _rewardsSurface = rewardsSurface != null ? rewardsSurface : FindChild(_overlay.transform, "RewardsSurface");
            ResolveBannerCard();
            _bannerTitle = bannerTitleText != null ? bannerTitleText : FindText(_overlay.transform, "BannerTitle");
            _bannerImage = bannerImage != null ? bannerImage : FindImage(_overlay.transform, "BannerImage");
            _bannerPrompt = bannerPromptText != null ? bannerPromptText : FindText(_overlay.transform, "BannerPrompt");
            _bannerContinue = bannerContinueButton != null ? bannerContinueButton : FindButton(_overlay.transform, "BannerContinueButton");
            _title = titleText != null ? titleText : FindText(_overlay.transform, "Title");
            _content = contentText != null ? contentText : FindText(_overlay.transform, "Content");
            _summaryTab = summaryTabButton != null ? summaryTabButton : FindButton(_overlay.transform, "SummaryTab");
            _logTab = battleLogTabButton != null ? battleLogTabButton : FindButton(_overlay.transform, "BattleLogTab");
            _continueButton = continueButton != null ? continueButton : FindButton(_overlay.transform, "ContinueButton");
            _scroll = contentScroll != null ? contentScroll : _overlay.GetComponentInChildren<ScrollRect>(true);
            _xpRowsRoot = xpRowsRoot != null ? xpRowsRoot : FindChild(_overlay.transform, "XpRows")?.transform;
            _ramLabel = ramLabel != null ? ramLabel : FindText(_overlay.transform, "RamLabel");
            _rewardsBody = rewardsBodyText != null ? rewardsBodyText : FindText(_overlay.transform, "RewardsBody");
            _lootRow = lootRow != null ? lootRow : FindChild(_overlay.transform, "LootRow");
            _lootLabel = lootLabel != null ? lootLabel : FindText(_overlay.transform, "LootLabel");
            _rewardsContinue = rewardsContinueButton != null ? rewardsContinueButton : FindButton(_overlay.transform, "RewardsContinueButton");
            _reportGroup = _reportSurface != null ? _reportSurface.GetComponent<CanvasGroup>() : null;
            _rewardsGroup = _rewardsSurface != null ? _rewardsSurface.GetComponent<CanvasGroup>() : null;
            EnsureLogOverflowFade();

            if (_title == null || _content == null)
                return false;

            WireButtons();
            _rewardsAuthored = _rewardsSurface != null;
            return true;
        }

        private void WireButtons()
        {
            if (_summaryTab != null)
            {
                _summaryTab.onClick.RemoveAllListeners();
                _summaryTab.onClick.AddListener(() => SetTab(false));
            }

            if (_logTab != null)
            {
                _logTab.onClick.RemoveAllListeners();
                _logTab.onClick.AddListener(() => SetTab(true));
            }

            if (_continueButton != null)
            {
                _continueButton.onClick.RemoveAllListeners();
                _continueButton.onClick.AddListener(Dismiss);
            }

            if (_bannerContinue != null)
            {
                _bannerContinue.onClick.RemoveAllListeners();
                _bannerContinue.onClick.AddListener(Dismiss);
            }

            if (_rewardsContinue != null)
            {
                _rewardsContinue.onClick.RemoveAllListeners();
                _rewardsContinue.onClick.AddListener(Dismiss);
            }

            ConfigureAdvanceNavigation();
        }

        private void BuildFallbackVisuals()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindCombatCanvas();
            Transform parent = canvas != null ? FindSafeAreaParent(canvas) : transform;

            _overlay = NewUiObject("CombatPostBattleReport", parent, typeof(Image), typeof(CanvasGroup));
            overlayRoot = _overlay;
            Stretch((RectTransform)_overlay.transform, Vector2.zero, Vector2.zero);
            _overlay.GetComponent<Image>().color = new Color(0.05f, 0.04f, 0.08f, 0.88f);

            BuildBannerFallback();
            BuildReportFallback();
            BuildRewardsFallback();
            WireButtons();
        }

        private void BuildBannerFallback()
        {
            _bannerSurface = NewUiObject("BannerSurface", _overlay.transform);
            bannerSurface = _bannerSurface;
            Stretch((RectTransform)_bannerSurface.transform, Vector2.zero, Vector2.zero);

            GameObject card = NewUiObject("BannerCard", _bannerSurface.transform, typeof(Image));
            _bannerCard = (RectTransform)card.transform;
            bannerCard = _bannerCard;
            _bannerCard.anchorMin = new Vector2(0f, 0.5f);
            _bannerCard.anchorMax = new Vector2(1f, 0.5f);
            _bannerCard.pivot = new Vector2(0.5f, 0.5f);
            _bannerCard.anchoredPosition = new Vector2(0f, 7.5f);
            _bannerCard.sizeDelta = new Vector2(0f, 463f);
            Image cardImage = card.GetComponent<Image>();
            cardImage.color = Color.white;
            cardImage.preserveAspect = true;
            cardImage.raycastTarget = false;

            GameObject contents = NewUiObject("BannerContents", _bannerCard, typeof(CanvasGroup));
            _bannerContents = contents.GetComponent<CanvasGroup>();
            bannerContentsGroup = _bannerContents;
            Stretch((RectTransform)contents.transform, Vector2.zero, Vector2.zero);

            _bannerTitle = CreateText("BannerTitle", contents.transform, 72f, FontStyles.Bold, TextAlignmentOptions.Center);
            bannerTitleText = _bannerTitle;
            Stretch(_bannerTitle.rectTransform, new Vector2(24f, 80f), new Vector2(-24f, -80f));
            _bannerTitle.color = Color.white;
            GameObject imageGo = NewUiObject("BannerImage", contents.transform, typeof(Image));
            _bannerImage = imageGo.GetComponent<Image>();
            bannerImage = _bannerImage;
            RectTransform imageRect = (RectTransform)imageGo.transform;
            imageRect.anchorMin = new Vector2(0.18f, 0.28f);
            imageRect.anchorMax = new Vector2(0.82f, 0.82f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageGo.SetActive(false);
            _bannerPrompt = CreateText("BannerPrompt", contents.transform, 28f, FontStyles.Bold, TextAlignmentOptions.Center);
            bannerPromptText = _bannerPrompt;
            RectTransform promptRect = _bannerPrompt.rectTransform;
            promptRect.anchorMin = new Vector2(0.2f, 0.08f);
            promptRect.anchorMax = new Vector2(0.8f, 0.18f);
            promptRect.offsetMin = Vector2.zero;
            promptRect.offsetMax = Vector2.zero;
            _bannerPrompt.color = Color.white;
            _bannerPrompt.text = "PRESS ANY BUTTON TO CONTINUE";
            _bannerPrompt.raycastTarget = false;
        }

        private void BuildReportFallback()
        {
            _reportSurface = NewUiObject("ReportSurface", _overlay.transform, typeof(Image));
            reportSurface = _reportSurface;
            RectTransform reportRect = (RectTransform)_reportSurface.transform;
            reportRect.anchorMin = new Vector2(0.06f, 0.08f);
            reportRect.anchorMax = new Vector2(0.94f, 0.92f);
            reportRect.offsetMin = Vector2.zero;
            reportRect.offsetMax = Vector2.zero;
            _reportSurface.GetComponent<Image>().color = new Color(0.96f, 0.94f, 0.82f, 1f);
            if (_reportSurface.GetComponent<CanvasGroup>() == null)
                _reportSurface.AddComponent<CanvasGroup>();

            _title = CreateText("Title", _reportSurface.transform, 54f, FontStyles.Bold, TextAlignmentOptions.Center);
            titleText = _title;
            RectTransform titleRect = _title.rectTransform;
            titleRect.anchorMin = new Vector2(0.06f, 0.84f);
            titleRect.anchorMax = new Vector2(0.94f, 0.97f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            GameObject tabs = NewUiObject("Tabs", _reportSurface.transform);
            RectTransform tabsRect = (RectTransform)tabs.transform;
            tabsRect.anchorMin = new Vector2(0.08f, 0.73f);
            tabsRect.anchorMax = new Vector2(0.92f, 0.83f);
            tabsRect.offsetMin = Vector2.zero;
            tabsRect.offsetMax = Vector2.zero;
            HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing = 12f;
            tabLayout.childForceExpandWidth = true;
            tabLayout.childForceExpandHeight = true;
            _summaryTab = CreateButton("SummaryTab", tabs.transform, "SUMMARY", () => SetTab(false));
            _logTab = CreateButton("BattleLogTab", tabs.transform, "BATTLE LOG", () => SetTab(true));
            summaryTabButton = _summaryTab;
            battleLogTabButton = _logTab;

            GameObject viewport = NewUiObject("Viewport", _reportSurface.transform, typeof(Image), typeof(Mask), typeof(ScrollRect));
            RectTransform viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = new Vector2(0.08f, 0.18f);
            viewportRect.anchorMax = new Vector2(0.92f, 0.70f);
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.26f);
            viewport.GetComponent<Mask>().showMaskGraphic = true;

            _content = CreateText("Content", viewport.transform, 38f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            contentText = _content;
            RectTransform contentRect = _content.rectTransform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(-56f, 0f);
            _content.enableAutoSizing = true;
            _content.fontSizeMin = 26f;
            _content.fontSizeMax = 40f;
            ContentSizeFitter fitter = _content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll = viewport.GetComponent<ScrollRect>();
            contentScroll = _scroll;
            _scroll.viewport = viewportRect;
            _scroll.content = contentRect;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 32f;
            EnsureLogOverflowFade();

            _continueButton = CreateButton("ContinueButton", _reportSurface.transform, "CONTINUE", Dismiss);
            continueButton = _continueButton;
            RectTransform continueRect = (RectTransform)_continueButton.transform;
            continueRect.anchorMin = new Vector2(0.32f, 0.05f);
            continueRect.anchorMax = new Vector2(0.68f, 0.14f);
            continueRect.offsetMin = Vector2.zero;
            continueRect.offsetMax = Vector2.zero;
        }

        private void BuildRewardsFallback()
        {
            _rewardsAuthored = false;
            _rewardsSurface = NewUiObject("RewardsSurface", _overlay.transform, typeof(Image));
            rewardsSurface = _rewardsSurface;
            RectTransform rewardsRect = (RectTransform)_rewardsSurface.transform;
            rewardsRect.anchorMin = new Vector2(0.06f, 0.08f);
            rewardsRect.anchorMax = new Vector2(0.94f, 0.92f);
            rewardsRect.offsetMin = Vector2.zero;
            rewardsRect.offsetMax = Vector2.zero;
            _rewardsSurface.GetComponent<Image>().color = new Color(0.96f, 0.94f, 0.82f, 1f);
            if (_rewardsSurface.GetComponent<CanvasGroup>() == null)
                _rewardsSurface.AddComponent<CanvasGroup>();

            _rewardsBody = CreateText("RewardsBody", _rewardsSurface.transform, 32f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            rewardsBodyText = _rewardsBody;
            RectTransform bodyRect = _rewardsBody.rectTransform;
            bodyRect.anchorMin = new Vector2(0.08f, 0.78f);
            bodyRect.anchorMax = new Vector2(0.92f, 0.94f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            _ramLabel = CreateText("RamLabel", _rewardsSurface.transform, 40f, FontStyles.Bold, TextAlignmentOptions.Center);
            ramLabel = _ramLabel;
            RectTransform ramRect = _ramLabel.rectTransform;
            ramRect.anchorMin = new Vector2(0.08f, 0.68f);
            ramRect.anchorMax = new Vector2(0.92f, 0.78f);
            ramRect.offsetMin = Vector2.zero;
            ramRect.offsetMax = Vector2.zero;

            _lootRow = NewUiObject("LootRow", _rewardsSurface.transform);
            lootRow = _lootRow;
            RectTransform lootRect = (RectTransform)_lootRow.transform;
            lootRect.anchorMin = new Vector2(0.08f, 0.58f);
            lootRect.anchorMax = new Vector2(0.92f, 0.68f);
            lootRect.offsetMin = Vector2.zero;
            lootRect.offsetMax = Vector2.zero;
            _lootLabel = CreateText("LootLabel", _lootRow.transform, 28f, FontStyles.Bold, TextAlignmentOptions.Center);
            lootLabel = _lootLabel;
            Stretch(_lootLabel.rectTransform, Vector2.zero, Vector2.zero);

            GameObject xpRows = NewUiObject("XpRows", _rewardsSurface.transform, typeof(VerticalLayoutGroup));
            _xpRowsRoot = xpRows.transform;
            xpRowsRoot = _xpRowsRoot;
            RectTransform xpRect = (RectTransform)xpRows.transform;
            xpRect.anchorMin = new Vector2(0.08f, 0.18f);
            xpRect.anchorMax = new Vector2(0.92f, 0.58f);
            xpRect.offsetMin = Vector2.zero;
            xpRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup xpLayout = xpRows.GetComponent<VerticalLayoutGroup>();
            xpLayout.spacing = 8f;
            xpLayout.childForceExpandWidth = true;
            xpLayout.childForceExpandHeight = false;

            _rewardsContinue = CreateButton("RewardsContinueButton", _rewardsSurface.transform, "CONFIRM", Dismiss);
            rewardsContinueButton = _rewardsContinue;
            RectTransform continueRect = (RectTransform)_rewardsContinue.transform;
            continueRect.anchorMin = new Vector2(0.32f, 0.05f);
            continueRect.anchorMax = new Vector2(0.68f, 0.14f);
            continueRect.offsetMin = Vector2.zero;
            continueRect.offsetMax = Vector2.zero;
        }

        private void SetTab(bool showLog)
        {
            if (_showingLog == showLog && _content != null && _content.text.Length > 0)
            {
                return;
            }

            _showingLog = showLog;
            RefreshContent();
            ConfigureAdvanceNavigation();
        }

        private void RefreshContent()
        {
            if (_content == null)
                return;
            if (_title != null)
                _title.text = _outcome.ReportTitle;
            _content.text = _showingLog ? BuildBattleLog() : BuildSummary();
            SetButtonSelected(_summaryTab, !_showingLog);
            SetButtonSelected(_logTab, _showingLog);
            Canvas.ForceUpdateCanvases();
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
        }

        private string BuildSummary()
        {
            if (_result == null)
                return "Battle complete.";
            var builder = new StringBuilder();
            builder.Append("Turns: ").AppendLine(Mathf.Max(0, _result.turnCount).ToString());
            builder.Append("Battle time: ").Append(Mathf.Max(0f, _result.durationSeconds).ToString("0.0")).AppendLine(" seconds");
            if (_result.totalEnemyKOs > 0)
                builder.Append("Opposing Chainlings knocked out: ").AppendLine(_result.totalEnemyKOs.ToString());
            if (_outcome.DidCapture)
            {
                builder.AppendLine();
                List<BattleCaptureRecord> captures = BattleOutcomePresentation.GetSuccessfulCaptures(_result);
                if (captures.Count > 0)
                {
                    for (int i = 0; i < captures.Count; i++)
                        builder.AppendLine(CaptureResultCopy.FormatBattleReportCaptureLine(captures[i]));
                }
                else
                {
                    builder.Append("Recorded: ").AppendLine(BattleOutcomePresentation.ResolveCapturedDisplayName(_result));
                }
            }

            builder.AppendLine();
            builder.Append("Open BATTLE LOG for the complete action transcript.");
            return builder.ToString();
        }

        private string BuildBattleLog()
        {
            List<CombatNarrationTranscriptEntry> entries = _battleController?.ReplayData?.narrationTranscript;
            if (entries == null || entries.Count == 0)
                return "No player-readable battle entries were recorded.";

            var builder = new StringBuilder();
            int displayedTurn = int.MinValue;
            for (int i = 0; i < entries.Count; i++)
            {
                CombatNarrationTranscriptEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.text))
                    continue;
                if (entry.kind != CombatNarrationPageKind.BattleResult && entry.turnIndex != displayedTurn)
                {
                    if (builder.Length > 0)
                        builder.AppendLine();
                    displayedTurn = entry.turnIndex;
                    builder.Append("<b>TURN ").Append(Mathf.Max(1, displayedTurn)).AppendLine("</b>");
                }
                builder.Append("<b>").Append(string.IsNullOrWhiteSpace(entry.speaker) ? "BATTLE LINK" : entry.speaker)
                    .Append(":</b> ").AppendLine(entry.text.Trim());
            }
            return builder.ToString();
        }

        private bool ConsumeConfirm()
        {
            bool pressed = ConfirmPressed() || _latchedConfirm;
            _latchedConfirm = false;
            return pressed;
        }

        private bool ConsumeCancel()
        {
            bool pressed = CancelPressed() || _latchedCancel;
            _latchedCancel = false;
            return pressed;
        }

        private static bool ConfirmPressed()
        {
            return (Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) ||
                   (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        }

        private bool BannerAdvancePressed()
        {
            if (ConsumeConfirm() || ConsumeCancel() || PointerPressed())
                return true;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame)
                return true;

            Gamepad pad = Gamepad.current;
            if (pad == null)
                return false;

            return pad.buttonWest.wasPressedThisFrame ||
                   pad.buttonNorth.wasPressedThisFrame ||
                   pad.startButton.wasPressedThisFrame ||
                   pad.selectButton.wasPressedThisFrame ||
                   pad.leftShoulder.wasPressedThisFrame ||
                   pad.rightShoulder.wasPressedThisFrame ||
                   pad.dpad.up.wasPressedThisFrame ||
                   pad.dpad.down.wasPressedThisFrame ||
                   pad.dpad.left.wasPressedThisFrame ||
                   pad.dpad.right.wasPressedThisFrame;
        }

        private bool PageAdvancePressed(FlowPage page)
        {
            if (ConsumeCancel())
                return true;

            if (!ConsumeConfirm())
                return false;

            Button focused = CurrentFocusedButton();
            if (focused == null || !focused.IsInteractable() || !focused.gameObject.activeInHierarchy)
                return true;

            if (page == FlowPage.Report && (focused == _summaryTab || focused == _logTab))
            {
                FocusButton(_continueButton);
                return false;
            }

            focused.onClick.Invoke();
            return _dismissRequested;
        }

        private static bool PointerPressed()
        {
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;
            return Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        }

        private static bool CancelPressed()
        {
            return (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
                   (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        }

        private void HandleMobileCancelPressed()
        {
            _latchedCancel = true;
        }

        private void HandleMobileConfirmPressed()
        {
            _latchedConfirm = true;
        }

        private void ConfigureAdvanceNavigation()
        {
            Button tabUp = _showingLog && _logTab != null ? _logTab : _summaryTab;
            SetExplicitNavigation(_summaryTab, _logTab, _logTab, null, _continueButton);
            SetExplicitNavigation(_logTab, _summaryTab, _summaryTab, null, _continueButton);
            SetExplicitNavigation(_continueButton, null, null, tabUp, null);
            if (_rewardsContinue != null)
            {
                _rewardsContinue.navigation = new Navigation { mode = Navigation.Mode.None };
            }
        }

        private static void SetExplicitNavigation(Button button, Button left, Button right, Button up, Button down)
        {
            if (button == null)
                return;

            button.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = left,
                selectOnRight = right,
                selectOnUp = up,
                selectOnDown = down,
            };
        }

        private static void EnablePageInteraction(CanvasGroup group)
        {
            if (group == null)
                return;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private void FocusButton(Button button)
        {
            if (button == null)
                return;

            EnsureUiInputReady();
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            _lastFocusedButton = null;
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(button.gameObject);
            SyncFocusedButtonVisual();
        }

        private Button CurrentFocusedButton()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || eventSystem.currentSelectedGameObject == null)
                return null;
            return eventSystem.currentSelectedGameObject.GetComponent<Button>();
        }

        private void SyncFocusedButtonVisual()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject focusedObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            if (focusedObject == _lastFocusedButton)
                return;

            _lastFocusedButton = focusedObject;
            Button focused = focusedObject != null ? focusedObject.GetComponent<Button>() : null;
            if (focused == _summaryTab || focused == _logTab)
                SetTab(focused == _logTab);
            if (_continueButton != null)
                SetButtonSelected(_continueButton, focused == null || focused == _continueButton);
            if (focused == _summaryTab || focused == _logTab)
            {
                SetButtonSelected(_summaryTab, focused == _summaryTab);
                SetButtonSelected(_logTab, focused == _logTab);
            }
            else
            {
                SetButtonSelected(_summaryTab, !_showingLog);
                SetButtonSelected(_logTab, _showingLog);
            }

            if (_rewardsContinue != null)
                SetButtonSelected(_rewardsContinue, focused == null || focused == _rewardsContinue);
        }

        private static void EnsureUiInputReady()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            if (!eventSystem.enabled)
                eventSystem.enabled = true;

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule != null && !inputModule.enabled)
                inputModule.enabled = true;
        }

        private void TickReportScroll()
        {
            ApplyReadingScroll(_scroll);
            UpdateLogOverflowFade();
        }

        private void TickRewardsScroll()
        {
            ApplyReadingScroll(_rewardsScroll);
            UpdateRewardsOverflowFade();
        }

        private static void ApplyReadingScroll(ScrollRect scroll)
        {
            if (scroll == null || scroll.content == null || scroll.viewport == null)
                return;

            float contentHeight = scroll.content.rect.height;
            float viewHeight = scroll.viewport.rect.height;
            float range = contentHeight - viewHeight;
            if (range <= 1f)
                return;

            float delta = 0f;
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                float stick = pad.rightStick.y.ReadValue();
                if (Mathf.Abs(stick) > 0.28f)
                    delta += stick * 520f * Time.unscaledDeltaTime;
                if (pad.leftShoulder.wasPressedThisFrame)
                    delta += viewHeight * 0.85f;
                if (pad.rightShoulder.wasPressedThisFrame)
                    delta -= viewHeight * 0.85f;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.pageUpKey.wasPressedThisFrame)
                    delta += viewHeight * 0.85f;
                if (keyboard.pageDownKey.wasPressedThisFrame)
                    delta -= viewHeight * 0.85f;
            }

            if (Mathf.Abs(delta) < 0.01f)
                return;

            scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + (delta / range));
        }

        private void EnsureLogOverflowFade()
        {
            if (_scroll == null || _scroll.viewport == null)
                return;
            if (_logOverflowFade != null)
                return;

            Transform existing = _scroll.viewport.Find("LogOverflowFade");
            if (existing == null)
                return;

            Image image = existing.GetComponent<Image>();
            if (image == null)
                return;

            _logOverflowFade = image;
            image.enabled = false;
        }

        private void UpdateLogOverflowFade()
        {
            if (_logOverflowFade == null)
                EnsureLogOverflowFade();
            if (_logOverflowFade == null || _scroll == null || _scroll.content == null || _scroll.viewport == null)
                return;

            float range = _scroll.content.rect.height - _scroll.viewport.rect.height;
            bool moreBelow = range > 8f && _scroll.verticalNormalizedPosition > 0.02f;
            _logOverflowFade.enabled = moreBelow;
        }

        private static void SetAdvanceButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void RestorePageAdvance(FlowPage page)
        {
            switch (page)
            {
                case FlowPage.Banner:
                    SetAdvanceButtonInteractable(_bannerContinue, true);
                    break;
                case FlowPage.Report:
                    SetAdvanceButtonInteractable(_continueButton, true);
                    RestoreCanvasGroup(_reportGroup);
                    break;
                case FlowPage.Rewards:
                    SetAdvanceButtonInteractable(_rewardsContinue, true);
                    RestoreCanvasGroup(_rewardsGroup);
                    break;
            }
        }

        private static void RestoreCanvasGroup(CanvasGroup group)
        {
            if (group == null)
                return;

            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        private IEnumerator AwaitTween(Tween tween, float timeoutSeconds = 4f)
        {
            if (tween == null || !tween.IsActive())
                yield break;

            bool done = false;
            tween.OnComplete(() => done = true);
            tween.OnKill(() => done = true);

            float elapsed = 0f;
            float timeout = Mathf.Max(0.25f, timeoutSeconds);
            while (!done && tween.IsActive() && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void AcquireInputLock()
        {
            ReleaseInputLock();
            InputLockService service = InputLockService.Instance;
            if (service == null)
                return;
            _inputLock = service.AcquireLock("CombatPostBattleFlow", InputLockChannel.All, this);
            SubscribeMobileAdvance();
        }

        private void ReleaseInputLock()
        {
            UnsubscribeMobileAdvance();
            if (_inputLock.IsValid)
                _inputLock.Release();
            _inputLock = default;
            _latchedCancel = false;
            _latchedConfirm = false;
        }

        private void SubscribeMobileAdvance()
        {
            UnsubscribeMobileAdvance();
            _mobileInput = FindFirstObjectByType<MobileInputManager>(FindObjectsInactive.Include);
            if (_mobileInput == null)
                return;

            _mobileInput.OnAPressed += HandleMobileConfirmPressed;
            _mobileInput.OnBPressed += HandleMobileCancelPressed;
        }

        private void UnsubscribeMobileAdvance()
        {
            if (_mobileInput == null)
                return;

            _mobileInput.OnAPressed -= HandleMobileConfirmPressed;
            _mobileInput.OnBPressed -= HandleMobileCancelPressed;
            _mobileInput = null;
        }

        private ICombatSfxOrchestrator ResolveCombatSfx()
        {
            CombatSfxOrchestrator orchestrator = FindFirstObjectByType<CombatSfxOrchestrator>(FindObjectsInactive.Include);
            if (orchestrator != null)
                return orchestrator;

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatSfxOrchestrator discovered)
                    return discovered;
            }

            return null;
        }

        private void Unbind()
        {
            if (_battleController != null)
                _battleController.OnBattleEnded -= HandleBattleEnded;
            _battleController = null;
        }

        private static Canvas FindCombatCanvas()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i].name.IndexOf("CombatBattleCanvas", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    canvases[i].transform.root.name.IndexOf("CombatBattleCanvas", StringComparison.OrdinalIgnoreCase) >= 0)
                    return canvases[i];
            }
            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static RectTransform FindSafeAreaParent(Canvas canvas)
        {
            RectTransform[] rects = canvas.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < rects.Length; i++)
            {
                if (string.Equals(rects[i].name, "SafeAreaRoot", StringComparison.OrdinalIgnoreCase))
                    return rects[i];
            }
            return canvas.transform as RectTransform;
        }

        private static GameObject FindChild(Transform root, string name)
        {
            if (root == null)
                return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return transforms[i].gameObject;
            }
            return null;
        }

        private static TextMeshProUGUI FindText(Transform root, string name)
        {
            TextMeshProUGUI[] texts = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (string.Equals(texts[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return texts[i];
            }
            return null;
        }

        private static Image FindImage(Transform root, string name)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (string.Equals(images[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return images[i];
            }
            return null;
        }

        private static Button FindButton(Transform root, string name)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (string.Equals(buttons[i].name, name, StringComparison.OrdinalIgnoreCase))
                    return buttons[i];
            }
            return null;
        }

        private static GameObject NewUiObject(string name, Transform parent, params Type[] components)
        {
            var all = new Type[components.Length + 2];
            all[0] = typeof(RectTransform);
            all[1] = typeof(CanvasRenderer);
            Array.Copy(components, 0, all, 2, components.Length);
            GameObject go = new GameObject(name, all);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            GameObject go = NewUiObject(name, parent, typeof(TextMeshProUGUI));
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.18f, 0.13f, 0.18f, 1f);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static Button CreateButton(string name, Transform parent, string label, Action clicked)
        {
            GameObject go = NewUiObject(name, parent, typeof(Image), typeof(Button));
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.24f, 0.19f, 0.27f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => clicked?.Invoke());
            go.AddComponent<CombatUiSelectableButtonVisual>();
            TextMeshProUGUI text = CreateText("Label", go.transform, 32f, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, new Vector2(16f, 8f), new Vector2(-16f, -8f));
            text.color = Color.white;
            text.text = label;
            return button;
        }

        private static void SetButtonSelected(Button button, bool selected)
        {
            if (button == null)
                return;

            CombatUiSelectableButtonVisual visual = button.GetComponent<CombatUiSelectableButtonVisual>();
            if (visual != null)
            {
                visual.SetSelected(selected);
                return;
            }

            if (button.targetGraphic != null)
                button.targetGraphic.color = selected
                    ? new Color(0.85f, 0.28f, 0.23f, 1f)
                    : new Color(0.24f, 0.19f, 0.27f, 1f);
        }

        private static void Stretch(RectTransform rect, Vector2 minOffset, Vector2 maxOffset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = minOffset;
            rect.offsetMax = maxOffset;
        }
    }
}
