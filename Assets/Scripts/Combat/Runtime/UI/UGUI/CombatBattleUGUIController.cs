using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// README:
    /// - Slot mapping: team 0 = PlayerHudPanel (slot 0 -> playerA, slot 1 -> playerB),
    ///   team 1 = EnemyHudPanel (slot 0 -> enemyA, slot 1 -> enemyB).
    /// - Wiring: assign a CombatBattleController (or call Initialize). This controller listens to
    ///   OnTurnActorSelected, OnActionTargetsResolved, and OnVitalsChanged for deterministic HUD updates.
    /// - Turn order extension: TurnOrderRibbon is a placeholder RectTransform; add future icon widgets there
    ///   without changing slot mapping or HUD card bindings.
    /// </summary>
    public class CombatBattleUGUIController : MonoBehaviour, ICombatSingleCombatHudLayoutController
    {
        [Header("Battle Source")]
        [SerializeField] private CombatBattleController battleController;
        [SerializeField] private MonoBehaviour stateSourceOverride;

        [Header("Type Icons")]
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private Sprite fallbackTypeIcon;

        [Header("Visual Identity")]
        [SerializeField] private ChainlingVisualIdentityDatabaseSO visualIdentityDatabase;

        [Header("HUD Roots")]
        [SerializeField] private RectTransform playerHudPanel;
        [SerializeField] private RectTransform enemyHudPanel;
        [SerializeField] private RectTransform turnOrderRibbon;
        [SerializeField] private RectTransform targetIndicatorsRoot;
        [SerializeField] private RectTransform bottomActionPanel;
        [Tooltip("Optional: wallet root GameObject. Leave empty to resolve via CombatActionSelectorController (avoids wrong Hardware_Wallet when duplicate names exist).")]
        [SerializeField] private GameObject walletRoot;

        [Header("HUD Cards")]
        [SerializeField] private HudCardView playerA;
        [SerializeField] private HudCardView playerB;
        [SerializeField] private HudCardView enemyA;
        [SerializeField] private HudCardView enemyB;

        [Header("Runtime")]
        [SerializeField] private bool pollHudFallback = false;
        [SerializeField] private float hudPollInterval = 0.12f;
        [SerializeField] private float targetHighlightSeconds = 0.35f;
        [SerializeField] private CombatUiAnimTuning uiAnimTuning;
        [Tooltip("Log HP/vitals update flow for debugging.")]
        [SerializeField] private bool enableHpDebugLogs = false;

        [Header("Layout")]
        [Tooltip("When false, panel anchors from prefab/scene are preserved. Set true to apply runtime layout.")]
        [SerializeField] private bool enforceHudPanelLayout = false;
        [Tooltip("When false, card anchors from prefab/scene are preserved. When true, cards are repositioned for 1v1/2v2 layout.")]
        [SerializeField] private bool enforceCardLayout = false;
        [SerializeField] private float hudPanelMinY = 0.72f;
        [SerializeField] private float hudPanelMaxY = 0.94f;
        [SerializeField] private float cardVerticalPadding = 0.16f;
        [SerializeField] private bool showBottomActionPanel;

        private ICombatStateSource _stateSource;
        private bool _singleCombatLayoutActive;
        private readonly Dictionary<ChainlingType, Sprite> _typeIconLookup = new Dictionary<ChainlingType, Sprite>();
        private readonly Dictionary<string, HudCardView> _cardsByInstanceId = new Dictionary<string, HudCardView>();
        private readonly Dictionary<HudCardView, CardLayoutSnapshot> _authoredCardLayouts = new Dictionary<HudCardView, CardLayoutSnapshot>();
        private readonly List<HudCardView> _allCards = new List<HudCardView>(4);
        private readonly List<HudCardView> _targetedCards = new List<HudCardView>(4);
        private float _nextPollTime;
        private bool _eventsSubscribed;
        private Coroutine _clearTargetsRoutine;
        private int _selectionActiveTeamIndex = -1;
        private int _selectionActiveSlotIndex = -1;
        private int _battleActiveTeamIndex = -1;
        private int _battleActiveSlotIndex = -1;
        private HudCardView _currentActiveCard;
        private int _captureScannerRequestVersion;
        private bool _captureScannerEnabled;
        private int _captureLowHpThresholdBasisPoints = 1500;

        public TypeIconPaletteSO TypeIconPalette => typeIconPalette;
        public Sprite FallbackTypeIcon => fallbackTypeIcon;
        public RectTransform BottomActionPanel => bottomActionPanel;

        private sealed class CardLayoutSnapshot
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 offsetMin;
            public Vector2 offsetMax;
            public Vector2 anchoredPosition;
        }

        public void SetVisualIdentityDatabase(ChainlingVisualIdentityDatabaseSO database)
        {
            if (database != null)
                visualIdentityDatabase = database;
        }

        public void SetTypeIconPalette(TypeIconPaletteSO palette, Sprite fallback = null)
        {
            if (palette != null) typeIconPalette = palette;
            if (fallback != null) fallbackTypeIcon = fallback;
            BuildTypeIconLookup();
        }

        /// <summary>
        /// When false, preserves prefab/scene layout for panels and cards.
        /// When true, applies runtime layout (anchors) from inspector values.
        /// </summary>
        public void SetLayoutEnforcement(bool enforce)
        {
            enforceHudPanelLayout = enforce;
            enforceCardLayout = enforce;
            ApplyRuntimeLayout();
        }

        public void SetSingleCombatLayoutActive(bool active)
        {
            if (_singleCombatLayoutActive == active)
                return;

            _singleCombatLayoutActive = active;
            RefreshAllHud();
        }

        public void Initialize(CombatBattleController controller)
        {
            battleController = controller;
            ResolveStateSource();
            UnsubscribeEvents();
            SubscribeEvents();
            RefreshAllHud();
        }

        public void SetRuntimeReferences(
            RectTransform playerPanel,
            RectTransform enemyPanel,
            RectTransform turnRibbon,
            RectTransform targetRoot,
            RectTransform actionPanel,
            HudCardView playerSlotA,
            HudCardView playerSlotB,
            HudCardView enemySlotA,
            HudCardView enemySlotB)
        {
            playerHudPanel = playerPanel;
            enemyHudPanel = enemyPanel;
            turnOrderRibbon = turnRibbon;
            targetIndicatorsRoot = targetRoot;
            bottomActionPanel = actionPanel;
            playerA = playerSlotA;
            playerB = playerSlotB;
            enemyA = enemySlotA;
            enemyB = enemySlotB;
            CacheCardList();
        }

        public void BindCombat(CombatResolver resolver, CombatEventLog log)
        {
            // Kept for compatibility with existing call sites that still expect a bind method.
            // This uGUI controller primarily uses ICombatStateSource events.
            RefreshAllHud();
        }

        public void SetBottomActionPanelVisible(bool visible)
        {
            showBottomActionPanel = visible;
            ApplyRuntimeLayout();
        }

        public void SetBattleHudVisible(bool visible)
        {
            if (playerHudPanel != null)
                playerHudPanel.gameObject.SetActive(visible);
            if (enemyHudPanel != null)
                enemyHudPanel.gameObject.SetActive(visible);
            if (turnOrderRibbon != null)
                turnOrderRibbon.gameObject.SetActive(visible);
            if (targetIndicatorsRoot != null)
                targetIndicatorsRoot.gameObject.SetActive(visible);
            if (bottomActionPanel != null)
                bottomActionPanel.gameObject.SetActive(visible && showBottomActionPanel);
        }

        /// <summary>
        /// Hides or shows all combat UI (HUD cards, action pad, wallet) for the entire battle.
        /// Used when hideCombatUI is set on the encounter (e.g. intro cutscene before device is gifted).
        /// </summary>
        public void SetCombatUIHidden(bool hidden)
        {
            bool visible = !hidden;
            if (playerHudPanel != null)
                playerHudPanel.gameObject.SetActive(visible);
            if (enemyHudPanel != null)
                enemyHudPanel.gameObject.SetActive(visible);
            if (turnOrderRibbon != null)
                turnOrderRibbon.gameObject.SetActive(visible);
            if (targetIndicatorsRoot != null)
                targetIndicatorsRoot.gameObject.SetActive(visible);
            if (bottomActionPanel != null)
                bottomActionPanel.gameObject.SetActive(visible);
            if (walletRoot != null)
                walletRoot.SetActive(visible);
        }

        private void Awake()
        {
            ResolveReferencesByName();
            ResolveVisualIdentityDatabase();
            ResolveAnimTuning();
            BuildTypeIconLookup();
            CacheCardList();
            ApplyRuntimeLayout();
        }

        private void OnEnable()
        {
            ResolveStateSource();
            ResolveVisualIdentityDatabase();
            ResolveAnimTuning();
            SubscribeEvents();
            ApplyRuntimeLayout();
            RefreshAllHud();
            _ = RefreshCaptureScannerAsync(++_captureScannerRequestVersion);
        }

        private void OnDisable()
        {
            _captureScannerRequestVersion++;
            UnsubscribeEvents();
        }

        private void Update()
        {
            if (!pollHudFallback || _stateSource == null || _stateSource.State == null)
                return;

            if (Time.unscaledTime < _nextPollTime)
                return;

            _nextPollTime = Time.unscaledTime + Mathf.Max(0.03f, hudPollInterval);
            RefreshAllHud();
        }

        public void PlayCardDamageShake(string instanceId)
        {
            if (!string.IsNullOrEmpty(instanceId) && _cardsByInstanceId.TryGetValue(instanceId, out var card) && card != null)
                card.PlayDamageShake();
        }

        public void PlayCardHealAscend(string instanceId)
        {
            if (!string.IsNullOrEmpty(instanceId) && _cardsByInstanceId.TryGetValue(instanceId, out var card) && card != null)
                card.PlayHealAscend();
        }

        public void RefreshAllHud()
        {
            if (_stateSource == null || _stateSource.State == null)
                return;

            _cardsByInstanceId.Clear();
            var state = _stateSource.State;

            UpdateTeamHud(0, state.team0, playerHudPanel, playerA, playerB);
            UpdateTeamHud(1, state.team1, enemyHudPanel, enemyA, enemyB);
            PruneTargetHighlights();
            RefreshActiveHighlight();
        }

        public void SetActiveTurnSlot(int teamIndex, int slotIndex)
        {
            _battleActiveTeamIndex = teamIndex;
            _battleActiveSlotIndex = slotIndex;
            RefreshActiveHighlight();
        }

        public void SetSelectionActiveSlot(int teamIndex, int slotIndex)
        {
            _selectionActiveTeamIndex = teamIndex;
            _selectionActiveSlotIndex = slotIndex;
            RefreshActiveHighlight();
        }

        public void ClearSelectionActiveSlot()
        {
            _selectionActiveTeamIndex = -1;
            _selectionActiveSlotIndex = -1;
            RefreshActiveHighlight();
        }

        public void SetTargetSlots(List<(int teamIndex, int slotIndex)> targets)
        {
            if (targets == null)
            {
                ClearTargetHighlights();
                return;
            }

            var mapped = new List<CombatBattleController.HudSlotRef>(targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                mapped.Add(new CombatBattleController.HudSlotRef(targets[i].teamIndex, targets[i].slotIndex, string.Empty));
            }

            ApplyTargetHighlights(mapped, autoClear: false);
        }

        public void SetTargetSlots(List<CombatBattleController.HudSlotRef> targets)
        {
            ApplyTargetHighlights(targets, autoClear: false);
        }

        public void PreviewTargets(List<CombatBattleController.HudSlotRef> slots, bool isMulti)
        {
            ApplyTargetHighlights(slots, autoClear: false);
        }

        public void FlashPreviewTargets(List<CombatBattleController.HudSlotRef> slots)
        {
            ApplyTargetHighlights(slots, autoClear: true);
        }

        public void ClearPreviewTargets()
        {
            ClearTargetHighlights();
        }

        private void ResolveStateSource()
        {
            if (stateSourceOverride != null && stateSourceOverride is ICombatStateSource overrideSource)
            {
                _stateSource = overrideSource;
                return;
            }

            if (battleController == null)
                battleController = FindObjectOfType<CombatBattleController>();

            if (battleController != null)
                _stateSource = battleController;
        }

        private void ResolveReferencesByName()
        {
            if (playerHudPanel == null)
                playerHudPanel = FindRectByName("PlayerHudPanel");
            if (enemyHudPanel == null)
                enemyHudPanel = FindRectByName("EnemyHudPanel");
            if (turnOrderRibbon == null)
                turnOrderRibbon = FindRectByName("TurnOrderRibbon");
            if (targetIndicatorsRoot == null)
                targetIndicatorsRoot = FindRectByName("TargetIndicatorsRoot");
            if (bottomActionPanel == null)
                bottomActionPanel = FindRectByName("BottomActionPanel");
            if (walletRoot == null)
            {
                var t = CombatWalletRootResolver.Resolve();
                if (t != null)
                    walletRoot = t.gameObject;
            }

            if (playerA == null)
                playerA = FindHudCardByName("PlayerSlotA_HudCard");
            if (playerB == null)
                playerB = FindHudCardByName("PlayerSlotB_HudCard");
            if (enemyA == null)
                enemyA = FindHudCardByName("EnemySlotA_HudCard");
            if (enemyB == null)
                enemyB = FindHudCardByName("EnemySlotB_HudCard");
        }

        private void ResolveAnimTuning()
        {
            if (uiAnimTuning == null)
                uiAnimTuning = GetComponentInParent<CombatUiAnimTuning>(true);
            if (uiAnimTuning == null)
                uiAnimTuning = FindObjectOfType<CombatUiAnimTuning>(true);
        }

        private void ApplyRuntimeLayout()
        {
            if (enforceHudPanelLayout)
            {
                SetRectAnchors(playerHudPanel, 0.02f, hudPanelMinY, 0.47f, hudPanelMaxY);
                SetRectAnchors(enemyHudPanel, 0.53f, hudPanelMinY, 0.98f, hudPanelMaxY);

                float ribbonMinY = Mathf.Clamp01(hudPanelMaxY - 0.06f);
                float ribbonMaxY = Mathf.Clamp01(hudPanelMaxY + 0.01f);
                if (ribbonMaxY > ribbonMinY)
                    SetRectAnchors(turnOrderRibbon, 0.37f, ribbonMinY, 0.63f, ribbonMaxY);
            }

            if (bottomActionPanel != null)
                bottomActionPanel.gameObject.SetActive(showBottomActionPanel);
        }

        private void CacheCardList()
        {
            _allCards.Clear();
            _authoredCardLayouts.Clear();
            AddCardIfPresent(playerA);
            AddCardIfPresent(playerB);
            AddCardIfPresent(enemyA);
            AddCardIfPresent(enemyB);
        }

        private void AddCardIfPresent(HudCardView card)
        {
            if (card != null)
            {
                _allCards.Add(card);
                CacheAuthoredCardLayout(card);
            }
        }

        private void CacheAuthoredCardLayout(HudCardView card)
        {
            if (card == null || _authoredCardLayouts.ContainsKey(card) || card.RectTransform == null)
                return;

            RectTransform rect = card.RectTransform;
            _authoredCardLayouts[card] = new CardLayoutSnapshot
            {
                anchorMin = rect.anchorMin,
                anchorMax = rect.anchorMax,
                offsetMin = rect.offsetMin,
                offsetMax = rect.offsetMax,
                anchoredPosition = rect.anchoredPosition
            };
        }

        private void BuildTypeIconLookup()
        {
            _typeIconLookup.Clear();
            if (typeIconPalette == null)
                return;

            var built = typeIconPalette.BuildLookup();
            if (built != null)
            {
                foreach (var kvp in built)
                    _typeIconLookup[kvp.Key] = kvp.Value;
            }

            if (fallbackTypeIcon == null)
                fallbackTypeIcon = typeIconPalette.FallbackIcon;
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed || battleController == null)
                return;

            battleController.OnBattleStarted += HandleBattleStarted;
            battleController.OnTurnBegan += HandleTurnBegan;
            battleController.OnActionStepExecuted += HandleActionStepExecuted;
            battleController.OnTurnEnded += HandleTurnEnded;
            battleController.OnChainlingFainted += HandleChainlingFainted;
            battleController.OnBattleEnded += HandleBattleEnded;
            battleController.OnTurnActorSelected += HandleTurnActorSelected;
            battleController.OnActionTargetsResolved += HandleActionTargetsResolved;
            battleController.OnVitalsChanged += HandleVitalsChanged;

            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed || battleController == null)
                return;

            battleController.OnBattleStarted -= HandleBattleStarted;
            battleController.OnTurnBegan -= HandleTurnBegan;
            battleController.OnActionStepExecuted -= HandleActionStepExecuted;
            battleController.OnTurnEnded -= HandleTurnEnded;
            battleController.OnChainlingFainted -= HandleChainlingFainted;
            battleController.OnBattleEnded -= HandleBattleEnded;
            battleController.OnTurnActorSelected -= HandleTurnActorSelected;
            battleController.OnActionTargetsResolved -= HandleActionTargetsResolved;
            battleController.OnVitalsChanged -= HandleVitalsChanged;

            _eventsSubscribed = false;
        }

        private void HandleBattleStarted(CombatState state)
        {
            RefreshAllHud();
            ClearAllHighlights();
            _ = RefreshCaptureScannerAsync(++_captureScannerRequestVersion);
        }

        private void HandleTurnBegan(int turn, List<CombatResolver.CombatActionStep> queue)
        {
            RefreshAllHud();

            if (queue == null || queue.Count == 0)
                return;

            var first = queue[0];
            if (first.actor == null || first.actingTeam == null)
                return;

            if (TryFindActorSlot(first.actingTeam, first.actor.instanceId, out int teamIndex, out int slotIndex))
                SetActiveTurnSlot(teamIndex, slotIndex);
        }

        private void HandleActionStepExecuted(CombatEvent evt)
        {
            // Do NOT refresh HUD here for damage/heal/AP events — they fire before the presenter
            // plays animations. HP bar is synced when the damage popup is shown (HandleDamage
            // calls RefreshAllHud) and via EmitVitalsChanged after the presenter finishes.
            // Other events (swap, KO, etc.) are handled by the presenter or EmitVitalsChanged.
            if (enableHpDebugLogs)
                Debug.Log($"[CombatHUD] OnActionStepExecuted {evt.eventType} target={evt.targetId} (HUD sync deferred to presenter/vitals)");
        }

        private void HandleTurnEnded(int turn)
        {
            RefreshAllHud();
            ClearAllHighlights();
        }

        private void HandleChainlingFainted(string chainlingId, int teamIndex, int slotIndex)
        {
            // Defer to presenter-driven RefreshAllHud (HandleDamage / HandleChainlingKO)
            // so the HP bar updates in sync with popup/shake, not before animations play.
        }

        private void HandleBattleEnded(BattleResult result)
        {
            RefreshAllHud();
            ClearAllHighlights();
        }

        private void HandleTurnActorSelected(CombatBattleController.HudSlotRef actorRef)
        {
            SetActiveTurnSlot(actorRef.teamIndex, actorRef.slotIndex);
        }

        private void HandleActionTargetsResolved(CombatBattleController.HudSlotRef actorRef, List<CombatBattleController.HudSlotRef> targets)
        {
            bool hasTargets = targets != null && targets.Count > 0;
            ApplyTargetHighlights(targets, autoClear: hasTargets);
        }

        private void HandleVitalsChanged(string instanceId, int hp, int ap)
        {
            if (enableHpDebugLogs)
                Debug.Log($"[CombatHUD] OnVitalsChanged instanceId={instanceId} hp={hp} ap={ap}");

            if (!_cardsByInstanceId.TryGetValue(instanceId, out var card) || card == null)
            {
                RefreshAllHud();
                return;
            }

            if (!TryFindChainling(instanceId, out var chainling))
            {
                RefreshAllHud();
                return;
            }

            card.UpdateVitals(chainling.displayHP, chainling.EffectiveMaxHP, chainling.displayAP, CombatConstants.AP_CAP);
            card.SetKO(chainling.displayHP <= 0);
            ApplyCaptureScannerMarker(card, chainling, IsEnemyChainling(instanceId));
        }

        private void UpdateTeamHud(int teamIndex, TeamState team, RectTransform panel, HudCardView slotA, HudCardView slotB)
        {
            if (slotA == null || slotB == null)
                return;

            var first = team != null ? team.GetActiveInSlot(0) : null;
            var second = team != null ? team.GetActiveInSlot(1) : null;

            int visibleCount = 0;
            if (first != null) visibleCount++;
            if (second != null) visibleCount++;

            if (visibleCount <= 0)
            {
                ConfigureCardVisibility(slotA, false);
                ConfigureCardVisibility(slotB, false);
                return;
            }

            if (visibleCount == 1)
            {
                var visible = _singleCombatLayoutActive ? slotB : first != null ? slotA : slotB;
                var hidden = visible == slotA ? slotB : slotA;
                var data = first ?? second;

                ConfigureCardVisibility(hidden, false);
                ConfigureCardVisibility(visible, true);
                if (_singleCombatLayoutActive)
                {
                    // Preserve the authored/grid cells: Slot A remains hidden in place,
                    // while the surviving Chainling is bound to the existing Slot B card.
                    RestoreAuthoredCardLayout(slotA);
                    RestoreAuthoredCardLayout(slotB);
                }
                else if (enforceCardLayout)
                    SetCardLayout(visible.RectTransform, 0.17f, 0.83f);
                BindCardData(visible, data, teamIndex == 1);
                return;
            }

            ConfigureCardVisibility(slotA, true);
            ConfigureCardVisibility(slotB, true);
            if (enforceCardLayout)
            {
                SetCardLayout(slotA.RectTransform, 0.00f, 0.48f);
                SetCardLayout(slotB.RectTransform, 0.52f, 1.00f);
            }
            BindCardData(slotA, first, teamIndex == 1);
            BindCardData(slotB, second, teamIndex == 1);
        }

        private void BindCardData(HudCardView card, ChainlingInstance chainling, bool isEnemy)
        {
            if (card == null)
                return;

            if (chainling == null)
            {
                card.SetEmpty(true);
                return;
            }

            card.BindChainling(chainling);
            if (chainling.isAlive)
                card.ApplyHudCardVariant(ResolveHudCardVariant(chainling));
            else
                card.RestoreDefaultHudCardVariant();

            card.SetTypeIcon(ResolveTypeIcon(chainling));
            card.UpdateVitals(chainling.displayHP, chainling.EffectiveMaxHP, chainling.displayAP, CombatConstants.AP_CAP);
            card.SetKO(!chainling.isAlive);
            ApplyCaptureScannerMarker(card, chainling, isEnemy);

            if (!string.IsNullOrEmpty(chainling.instanceId))
                _cardsByInstanceId[chainling.instanceId] = card;
        }

        private async Task RefreshCaptureScannerAsync(int requestVersion)
        {
            _captureScannerEnabled = false;
            if (AuthoritativeBattleLaunchContext.TryReadState(out AuthoritativeClientBattleState battleState) &&
                battleState.captureScannerRating > 0 &&
                battleState.captureLowHpThresholdBasisPoints > 0)
            {
                _captureScannerEnabled = true;
                _captureLowHpThresholdBasisPoints = Mathf.Clamp(battleState.captureLowHpThresholdBasisPoints, 0, 9999);
                if (requestVersion == _captureScannerRequestVersion)
                    RefreshAllHud();
                return;
            }

            CaptureBalanceConfigSO board = BalanceConfigRuntime.CaptureBalance;
            if (board != null)
                _captureLowHpThresholdBasisPoints = board.ToCoreData().LowHpThresholdBasisPoints;

            AuthoritativeCombatSessionService authority = FindFirstObjectByType<AuthoritativeCombatSessionService>(FindObjectsInactive.Include);
            if (authority == null || !authority.IsRolloutEnabled)
            {
                if (requestVersion == _captureScannerRequestVersion)
                    RefreshAllHud();
                return;
            }

            try
            {
                CaptureLoadoutSnapshot loadout = await authority.FetchAsync();
                if (requestVersion != _captureScannerRequestVersion || !isActiveAndEnabled)
                    return;
                _captureScannerEnabled = loadout != null && loadout.ResolvedEffects.ScanRating > 0;
            }
            catch (System.Exception exception)
            {
                if (requestVersion != _captureScannerRequestVersion)
                    return;
                _captureScannerEnabled = false;
                Debug.LogWarning($"[CombatHUD] Scanner loadout unavailable; keeping capture threshold hidden. {exception.Message}", this);
            }

            RefreshAllHud();
        }

        private void ApplyCaptureScannerMarker(HudCardView card, ChainlingInstance chainling, bool isEnemy)
        {
            if (card == null || chainling == null)
                return;
            int maxHp = Mathf.Max(1, chainling.EffectiveMaxHP);
            int hpBasisPoints = Mathf.Clamp((int)((long)Mathf.Max(0, chainling.displayHP) * 10000L / maxHp), 0, 10000);
            bool isLowHp = hpBasisPoints <= _captureLowHpThresholdBasisPoints;
            card.SetCaptureLowHpMarker(_captureScannerEnabled && isEnemy && chainling.isAlive, _captureLowHpThresholdBasisPoints, isLowHp);
        }

        private bool IsEnemyChainling(string instanceId)
        {
            TeamState enemies = _stateSource != null && _stateSource.State != null ? _stateSource.State.team1 : null;
            if (enemies == null || string.IsNullOrEmpty(instanceId))
                return false;
            for (int i = 0; i < enemies.chainlings.Count; i++)
            {
                ChainlingInstance candidate = enemies.chainlings[i];
                if (candidate != null && candidate.instanceId == instanceId)
                    return true;
            }
            return false;
        }

        private void ConfigureCardVisibility(HudCardView card, bool visible)
        {
            if (card == null)
                return;

            card.SetHiddenInPlace(!visible);
        }

        private void ResolveVisualIdentityDatabase()
        {
            if (visualIdentityDatabase != null)
                return;

            var presenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);
            if (presenter != null)
                visualIdentityDatabase = presenter.VisualIdentityDatabase;
        }

        private void SetCardLayout(RectTransform rect, float minX, float maxX)
        {
            if (rect == null)
                return;

            float minY = Mathf.Clamp01(cardVerticalPadding);
            float maxY = Mathf.Clamp01(1f - cardVerticalPadding);
            if (maxY <= minY)
            {
                minY = 0.08f;
                maxY = 0.92f;
            }

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private void RestoreAuthoredCardLayout(HudCardView card)
        {
            if (card == null || card.RectTransform == null || !_authoredCardLayouts.TryGetValue(card, out var snapshot))
                return;

            RectTransform rect = card.RectTransform;
            rect.anchorMin = snapshot.anchorMin;
            rect.anchorMax = snapshot.anchorMax;
            rect.offsetMin = snapshot.offsetMin;
            rect.offsetMax = snapshot.offsetMax;
            rect.anchoredPosition = snapshot.anchoredPosition;
        }

        private static void SetRectAnchors(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        private void ApplyTargetHighlights(List<CombatBattleController.HudSlotRef> targets, bool autoClear)
        {
            if (_clearTargetsRoutine != null)
            {
                StopCoroutine(_clearTargetsRoutine);
                _clearTargetsRoutine = null;
            }

            var desiredCards = new List<HudCardView>(4);
            if (targets != null)
            {
                for (int i = 0; i < targets.Count; i++)
                {
                    var slot = targets[i];
                    var card = ResolveCardForSlot(slot);
                    if (card == null || card.IsHiddenInPlace || desiredCards.Contains(card))
                        continue;
                    desiredCards.Add(card);
                }
            }

            for (int i = _targetedCards.Count - 1; i >= 0; i--)
            {
                var card = _targetedCards[i];
                if (card == null || !desiredCards.Contains(card))
                {
                    if (card != null)
                        card.SetTargeted(false);
                    _targetedCards.RemoveAt(i);
                }
            }

            for (int i = 0; i < desiredCards.Count; i++)
            {
                var card = desiredCards[i];
                if (_targetedCards.Contains(card))
                    continue;

                card.SetTargeted(true);
                _targetedCards.Add(card);
            }

            float highlightDuration = GetTargetHighlightDuration();
            if (autoClear && highlightDuration > 0f)
            {
                if (_clearTargetsRoutine != null)
                    StopCoroutine(_clearTargetsRoutine);
                _clearTargetsRoutine = StartCoroutine(ClearTargetsAfterDelay(highlightDuration));
            }
        }

        private IEnumerator ClearTargetsAfterDelay(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);
            _clearTargetsRoutine = null;
            ClearTargetHighlights();
        }

        private void ClearTargetHighlights()
        {
            if (_clearTargetsRoutine != null)
            {
                StopCoroutine(_clearTargetsRoutine);
                _clearTargetsRoutine = null;
            }

            for (int i = 0; i < _targetedCards.Count; i++)
            {
                if (_targetedCards[i] != null)
                    _targetedCards[i].SetTargeted(false);
            }

            _targetedCards.Clear();
        }

        private void ClearAllHighlights()
        {
            ClearStoredActiveSlots();
            ClearTargetHighlights();

            for (int i = 0; i < _allCards.Count; i++)
            {
                if (_allCards[i] == null)
                    continue;
                _allCards[i].SetActiveTurn(false);
            }
        }

        private float GetTargetHighlightDuration()
        {
            if (uiAnimTuning != null)
                return Mathf.Max(0f, uiAnimTuning.previewTargetFlashDuration);
            return Mathf.Max(0f, targetHighlightSeconds);
        }

        private HudCardView GetCardBySlot(int teamIndex, int slotIndex)
        {
            if (teamIndex == 0)
                return slotIndex == 0 ? playerA : slotIndex == 1 ? playerB : null;
            if (teamIndex == 1)
                return slotIndex == 0 ? enemyA : slotIndex == 1 ? enemyB : null;
            return null;
        }

        private void RefreshActiveHighlight()
        {
            var desiredCard = ResolveDesiredActiveCard();
            if (_currentActiveCard == desiredCard)
                return;

            if (_currentActiveCard != null)
                _currentActiveCard.SetActiveTurn(false);

            _currentActiveCard = desiredCard;
            if (_currentActiveCard != null)
                _currentActiveCard.SetActiveTurn(true);
        }

        private void ClearStoredActiveSlots()
        {
            if (_currentActiveCard != null)
                _currentActiveCard.SetActiveTurn(false);

            _selectionActiveTeamIndex = -1;
            _selectionActiveSlotIndex = -1;
            _battleActiveTeamIndex = -1;
            _battleActiveSlotIndex = -1;
            _currentActiveCard = null;
        }

        private HudCardView ResolveDesiredActiveCard()
        {
            var selectedCard = GetActiveCardIfVisible(_selectionActiveTeamIndex, _selectionActiveSlotIndex);
            if (selectedCard != null)
                return selectedCard;

            return GetActiveCardIfVisible(_battleActiveTeamIndex, _battleActiveSlotIndex);
        }

        private HudCardView GetActiveCardIfVisible(int teamIndex, int slotIndex)
        {
            var card = ResolveCardForSlot(new CombatBattleController.HudSlotRef(teamIndex, slotIndex, string.Empty));
            if (card == null || card.IsHiddenInPlace)
                return null;

            return card;
        }

        private HudCardView ResolveCardForSlot(CombatBattleController.HudSlotRef slot)
        {
            if (!string.IsNullOrEmpty(slot.instanceId) &&
                _cardsByInstanceId.TryGetValue(slot.instanceId, out var mappedByReference) &&
                mappedByReference != null)
            {
                return mappedByReference;
            }

            ChainlingInstance chainling = GetActiveChainlingInSlot(slot.teamIndex, slot.slotIndex);
            if (chainling != null &&
                !string.IsNullOrEmpty(chainling.instanceId) &&
                _cardsByInstanceId.TryGetValue(chainling.instanceId, out var mappedByState) &&
                mappedByState != null)
            {
                return mappedByState;
            }

            return GetCardBySlot(slot.teamIndex, slot.slotIndex);
        }

        private ChainlingInstance GetActiveChainlingInSlot(int teamIndex, int slotIndex)
        {
            if (_stateSource == null || _stateSource.State == null)
                return null;

            TeamState team = teamIndex == 0
                ? _stateSource.State.team0
                : teamIndex == 1
                    ? _stateSource.State.team1
                    : null;
            return team != null ? team.GetActiveInSlot(slotIndex) : null;
        }

        private void PruneTargetHighlights()
        {
            for (int i = _targetedCards.Count - 1; i >= 0; i--)
            {
                var card = _targetedCards[i];
                if (card == null || card.IsHiddenInPlace)
                {
                    if (card != null)
                        card.SetTargeted(false);
                    _targetedCards.RemoveAt(i);
                }
            }
        }

        private bool TryFindChainling(string instanceId, out ChainlingInstance chainling)
        {
            chainling = null;
            if (_stateSource == null || _stateSource.State == null || string.IsNullOrEmpty(instanceId))
                return false;

            if (TryFindChainlingInTeam(_stateSource.State.team0, instanceId, out chainling))
                return true;
            if (TryFindChainlingInTeam(_stateSource.State.team1, instanceId, out chainling))
                return true;
            return false;
        }

        private bool TryFindChainlingInTeam(TeamState team, string instanceId, out ChainlingInstance chainling)
        {
            chainling = null;
            if (team == null || team.chainlings == null)
                return false;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                var candidate = team.chainlings[i];
                if (candidate != null && candidate.instanceId == instanceId)
                {
                    chainling = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindActorSlot(TeamState team, string actorId, out int teamIndex, out int slotIndex)
        {
            teamIndex = -1;
            slotIndex = -1;
            if (team == null || team.activeIndices == null || team.chainlings == null || string.IsNullOrEmpty(actorId))
                return false;

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                int idx = team.activeIndices[slot];
                if (idx < 0 || idx >= team.chainlings.Count)
                    continue;

                var chainling = team.chainlings[idx];
                if (chainling != null && chainling.instanceId == actorId)
                {
                    teamIndex = team.teamIndex;
                    slotIndex = slot;
                    return true;
                }
            }

            return false;
        }

        private Sprite ResolveTypeIcon(ChainlingInstance chainling)
        {
            if (chainling == null)
                return fallbackTypeIcon;

            ChainlingType resolvedType = ChainlingType.None;
            var resolved = chainling.GetResolvedTypes();
            if (resolved != null && resolved.Length > 0)
                resolvedType = resolved[0];
            else
                resolvedType = chainling.primaryType;

            // Prefer palette's GetIcon (includes its fallback); fall back to our cached lookup and then our fallback
            if (typeIconPalette != null)
            {
                var icon = typeIconPalette.GetIcon(resolvedType);
                if (icon != null)
                    return icon;
            }

            if (_typeIconLookup.TryGetValue(resolvedType, out var cached) && cached != null)
                return cached;

            return fallbackTypeIcon;
        }

        private ChainlingHudCardVariantDefinitionSO ResolveHudCardVariant(ChainlingInstance chainling)
        {
            if (chainling == null || chainling.visualIdentity == null || visualIdentityDatabase == null)
                return null;

            return visualIdentityDatabase.GetHudCardVariant(chainling.visualIdentity.hudCardVariantId);
        }

        private RectTransform FindRectByName(string targetName)
        {
            var transforms = GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].name == targetName)
                    return transforms[i];
            }

            return null;
        }

        private HudCardView FindHudCardByName(string targetName)
        {
            var cards = GetComponentsInChildren<HudCardView>(true);
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i].name == targetName)
                    return cards[i];
            }

            return null;
        }
    }
}
