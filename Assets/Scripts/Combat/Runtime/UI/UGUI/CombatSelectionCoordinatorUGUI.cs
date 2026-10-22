using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using SignalJam.UI.MobileControls;
using TMPro;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatSelectionCoordinatorUGUI : MonoBehaviour
    {
        [SerializeField] private CombatBattleController battleController;
        [SerializeField] private CombatBattleUGUIController hudController;
        [SerializeField] private CombatMoveSelectPanelUGUI moveSelectPanel;
        [SerializeField] private CombatSwapSelectPanelUGUI swapSelectPanel;
        [SerializeField] private CombatMoveInfoPanelUGUI moveInfoPanel;
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private ModifierCatalogSO modifierCatalog;
        [SerializeField] private TypeChemistryMatrixSO typeChemistryMatrix;
        [SerializeField] private TypeIconPaletteSO moveTypeIconPalette;
        [SerializeField] private Sprite fallbackMoveTypeIcon;
        [SerializeField] private float navigateThreshold = 0.4f;
        [SerializeField] private float navigateRepeatDelay = 0.16f;
        [SerializeField] private float autoCommitPreviewSeconds = 0.18f;
        [SerializeField] private CombatUiAnimTuning uiAnimTuning;
        [SerializeField] private Combat2BattlefieldPresenter battlefieldPresenter;
        [SerializeField] private bool verboseLogs;
        [Header("Audio")]
        [SerializeField] private MonoBehaviour uiSfxBehaviour;
        [Header("Targeting Prompt")]
        [Tooltip("Message shown while the player is choosing a target. Auto-creates a label if unassigned.")]
        [SerializeField] private TMP_Text targetingPromptLabel;
        [SerializeField] private string targetingPromptMessage = "Select your target";
        [SerializeField] private float targetingPromptFadeDuration = 0.22f;

        private Tween _targetingPromptTween;
        private string _activeTargetingPromptMessage;

        private enum Mode { None, MoveSelect, TargetSelect, SwapSelect, ExternalTargetSelect, InfoModal }

        private struct MoveEntry
        {
            public MoveInstance move;
            public int moveIndex;
            public bool usable;
            public string blockReason;
            public PassiveDisplayState passiveState;
        }

        private struct SwapEntry
        {
            public int chainlingIndex;
            public ChainlingInstance chainling;
            public bool available;
        }

        private Mode _mode;
        private Mode _returnModeAfterInfo;
        private float _nextNavigateTime;

        private CombatState _state;
        private TeamState _actingTeam;
        private TeamState _opposingTeam;
        private ChainlingInstance _actor;
        private int _projectedGiftAp;
        private int _actingTeamIndex;
        private int _actingSlotIndex;
        private IReadOnlyDictionary<int, CombatAction> _queuedActionsBySlot;

        private readonly List<MoveEntry> _moves = new List<MoveEntry>(10);
        private readonly List<SwapEntry> _swaps = new List<SwapEntry>(4);
        private readonly List<CombatBattleController.HudSlotRef> _targetCandidates = new List<CombatBattleController.HudSlotRef>(4);
        private readonly List<CombatBattleController.HudSlotRef> _previewTargets = new List<CombatBattleController.HudSlotRef>(4);
        private readonly List<CombatBattleController.HudSlotRef> _lockedBlockTargets = new List<CombatBattleController.HudSlotRef>(4);
        private readonly List<CombatMoveInfoPanelUGUI.ChemistryRowData> _chemistryRows = new List<CombatMoveInfoPanelUGUI.ChemistryRowData>(16);
        private readonly List<CombatMoveSelectPanelUGUI.ModifierOverviewRowData> _modifierOverviewRows = new List<CombatMoveSelectPanelUGUI.ModifierOverviewRowData>(4);
        private readonly HashSet<int> _reservedSwapBenchIndices = new HashSet<int>();
        private readonly HashSet<string> _invalidBlockTargetIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly CombatActionEligibilityService _eligibilityService = new CombatActionEligibilityService();

        private int _moveCursor;
        private int _swapCursor;
        private int _targetCursor;
        private int _libraryCardDirection;

        private Action<CombatAction> _commitAction;
        private Action _cancelToWallet;
        private Action<string> _externalTargetCommit;
        private Coroutine _autoCommitRoutine;
        private bool _autoCommitPending;
        private MonoBehaviour _cachedUiSfxBehaviour;
        private bool _uiSfxUnavailable;
        private bool _hudButtonAudioSuppressed;

        public bool IsSelecting => _mode != Mode.None;

        private void OnEnable()
        {
            if (_hudButtonAudioSuppressed)
                return;

            HudButtonAudioGate.Suppress();
            _hudButtonAudioSuppressed = true;
        }

        private void OnDisable()
        {
            if (_hudButtonAudioSuppressed)
            {
                HudButtonAudioGate.Release();
                _hudButtonAudioSuppressed = false;
            }

            CancelAutoCommit();
            HideTargetingPrompt();
            hudController?.ClearSelectionActiveSlot();
            hudController?.ClearPreviewTargets();
            hudController?.SetSelectionLockedTargets(null);
            battlefieldPresenter?.ClearSelectionPreview();
        }

        public void Configure(
            CombatBattleController controller,
            CombatBattleUGUIController hud,
            CombatMoveSelectPanelUGUI movePanel,
            CombatSwapSelectPanelUGUI swapPanel,
            CombatMoveInfoPanelUGUI infoPanel,
            MoveDatabaseSO database,
            TypeIconPaletteSO typePalette,
            Sprite fallbackTypeIcon,
            bool enableVerboseLogs = false)
        {
            if (controller != null) battleController = controller;
            if (hud != null) hudController = hud;
            if (movePanel != null) moveSelectPanel = movePanel;
            if (swapPanel != null) swapSelectPanel = swapPanel;
            if (infoPanel != null) moveInfoPanel = infoPanel;
            if (database != null) moveDatabase = database;
            if (typePalette != null) moveTypeIconPalette = typePalette;
            if (fallbackTypeIcon != null) fallbackMoveTypeIcon = fallbackTypeIcon;
            verboseLogs = enableVerboseLogs;

            ResolveFallbacks();
            HidePanels();
        }

        public bool BeginMoveSelection(
            CombatState state,
            int teamIndex,
            int slotIndex,
            ChainlingInstance actor,
            Action<CombatAction> onCommit,
            Action onCancel,
            int projectedGiftAp = 0,
            IReadOnlyDictionary<int, CombatAction> queuedActionsBySlot = null)
        {
            ResolveFallbacks();
            if (state == null || actor == null || hudController == null || moveSelectPanel == null)
                return false;

            _projectedGiftAp = Math.Max(0, projectedGiftAp);
            _queuedActionsBySlot = queuedActionsBySlot;
            SetContext(state, teamIndex, slotIndex, actor, onCommit, onCancel);
            _reservedSwapBenchIndices.Clear();
            BuildMoveEntries();
            _moveCursor = FirstUsableMoveIndex();
            if (_moveCursor < 0)
                _moveCursor = GetMoveListRowCount() > 0 ? _moves.Count : 0;
            _targetCursor = 0;
            _mode = Mode.MoveSelect;

            ShowMovePanel();
            RefreshMoveUI();
            RefreshPreviewTargets();
            return true;
        }

        public bool BeginSwapSelection(
            CombatState state,
            int teamIndex,
            int slotIndex,
            ChainlingInstance actor,
            Action<CombatAction> onCommit,
            Action onCancel,
            ISet<int> reservedBenchIndices = null)
        {
            ResolveFallbacks();
            if (state == null || hudController == null || swapSelectPanel == null)
                return false;

            _queuedActionsBySlot = null;
            SetContext(state, teamIndex, slotIndex, actor, onCommit, onCancel);
            _reservedSwapBenchIndices.Clear();
            if (reservedBenchIndices != null)
            {
                foreach (int reserved in reservedBenchIndices)
                    _reservedSwapBenchIndices.Add(reserved);
            }
            BuildSwapEntries();
            _swapCursor = FirstAvailableSwapIndex();
            _mode = Mode.SwapSelect;

            ShowSwapPanel();
            RefreshSwapUI();
            hudController.ClearPreviewTargets();
            battlefieldPresenter?.ClearSelectionPreview();
            return true;
        }

        /// <summary>
        /// Opens the combat-native slot targeter for a non-move command. Eligible instance IDs
        /// keep command-specific rules outside the shared presentation layer. The move-select
        /// frame stays closed; capture uses the battlefield targeter only.
        /// </summary>
        public bool BeginTargetSelection(
            CombatState state,
            int actingTeamIndex,
            int actingSlotIndex,
            ChainlingInstance actor,
            IReadOnlyCollection<string> eligibleInstanceIds,
            Action<string> onConfirm,
            Action onCancel)
        {
            ResolveFallbacks();
            if (state == null || hudController == null || onConfirm == null || eligibleInstanceIds == null)
                return false;

            _queuedActionsBySlot = null;
            SetContext(state, actingTeamIndex, actingSlotIndex, actor, null, onCancel);
            _externalTargetCommit = onConfirm;
            _targetCandidates.Clear();
            _previewTargets.Clear();

            TeamState targets = state.GetOpposingTeam(actingTeamIndex);
            var eligible = new HashSet<string>(eligibleInstanceIds, StringComparer.Ordinal);
            if (targets != null && targets.activeIndices != null && targets.chainlings != null)
            {
                for (int slot = 0; slot < targets.activeIndices.Count; slot++)
                {
                    int index = targets.activeIndices[slot];
                    if (index < 0 || index >= targets.chainlings.Count)
                        continue;
                    ChainlingInstance candidate = targets.chainlings[index];
                    if (candidate == null || !candidate.isAlive || !eligible.Contains(candidate.instanceId ?? ""))
                        continue;
                    _targetCandidates.Add(new CombatBattleController.HudSlotRef(targets.teamIndex, slot, candidate.instanceId));
                }
            }

            if (_targetCandidates.Count == 0)
            {
                AbortSelection();
                return false;
            }

            _targetCursor = 0;
            _activeTargetingPromptMessage = null;
            _mode = Mode.ExternalTargetSelect;
            HideTargetingPrompt();
            moveSelectPanel?.SetVisible(false);
            swapSelectPanel?.SetVisible(false);
            moveInfoPanel?.SetVisible(false);
            RefreshPreviewTargets();
            return true;
        }

        public void HandleNavigate(Vector2 navigate)
        {
            if (_mode == Mode.None || _mode == Mode.InfoModal || _autoCommitPending || Time.unscaledTime < _nextNavigateTime)
                return;

            int step = NavToStep(navigate);
            if (step == 0)
                return;
            _nextNavigateTime = Time.unscaledTime + navigateRepeatDelay;

            if (_mode == Mode.MoveSelect && GetMoveListRowCount() > 0)
            {
                _libraryCardDirection = step;
                _moveCursor = Wrap(_moveCursor + step, GetMoveListRowCount());
                RefreshMoveUI();
                RefreshPreviewTargets();
                PlayUiCue(CombatUiSfxCue.Navigate);
            }
            else if (_mode == Mode.SwapSelect && _swaps.Count > 0)
            {
                _swapCursor = Wrap(_swapCursor + step, _swaps.Count);
                RefreshSwapUI();
                PlayUiCue(CombatUiSfxCue.Navigate);
            }
            else if (_mode == Mode.TargetSelect && _targetCandidates.Count > 0)
            {
                _targetCursor = FindNextValidTargetIndex(_targetCursor, step);
                RefreshMoveUI();
                RefreshPreviewTargets();
                PlayUiCue(CombatUiSfxCue.TargetChanged);
            }
            else if (_mode == Mode.ExternalTargetSelect && _targetCandidates.Count > 0)
            {
                _targetCursor = Wrap(_targetCursor + step, _targetCandidates.Count);
                RefreshPreviewTargets();
                PlayUiCue(CombatUiSfxCue.TargetChanged);
            }
        }

        public void HandleConfirm()
        {
            if (_autoCommitPending)
                return;

            if (_mode == Mode.InfoModal)
            {
                PlayUiCue(CombatUiSfxCue.Confirm);
                CloseInfo();
                return;
            }

            if (_mode == Mode.MoveSelect)
            {
                if (IsRestRowSelected())
                {
                    PlayUiCue(CombatUiSfxCue.Confirm);
                    Commit(CombatAction.Pass(_actingTeamIndex));
                    return;
                }

                if (!IndexValid(_moveCursor, _moves.Count))
                    return;

                var entry = _moves[_moveCursor];
                if (!entry.usable)
                {
                    moveSelectPanel?.PlayUnavailableMoveFeedback();
                    if (entry.blockReason == "AllTargetsBlocked")
                    {
                        _activeTargetingPromptMessage = "Every target already has a block.";
                        ShowTargetingPrompt();
                        PlayUiCue(CombatUiSfxCue.Invalid);
                    }
                    return;
                }

                BuildTargetsForMove(entry.move, out bool requiresTargetSelect, out bool isMulti);
                if (CombatWallStateUtility.MovePlacesBlock(entry.move) &&
                    _targetCandidates.Count > 0 &&
                    _invalidBlockTargetIds.Count >= _targetCandidates.Count)
                {
                    _activeTargetingPromptMessage = "Every target already has a block.";
                    ShowTargetingPrompt();
                    moveSelectPanel?.PlayUnavailableMoveFeedback();
                    PlayUiCue(CombatUiSfxCue.Invalid);
                    return;
                }
                if (requiresTargetSelect && _targetCandidates.Count > 1)
                {
                    _mode = Mode.TargetSelect;
                    _targetCursor = FindNextValidTargetIndex(-1, 1);
                    _activeTargetingPromptMessage = null;
                    ShowTargetingPrompt();
                    RefreshMoveUI();
                    RefreshPreviewTargets();
                    PlayUiCue(CombatUiSfxCue.Confirm);
                    return;
                }

                var action = BuildMoveAction(entry.moveIndex, isMulti, useCursorTarget: false);
                if (action == null)
                    return;

                if (_previewTargets.Count > 0)
                    hudController?.FlashPreviewTargets(_previewTargets);
                PlayUiCue(CombatUiSfxCue.Confirm);
                BeginAutoCommit(action);
                return;
            }

            if (_mode == Mode.TargetSelect)
            {
                if (IndexValid(_moveCursor, _moves.Count))
                {
                    var action = BuildMoveAction(_moves[_moveCursor].moveIndex, isMulti: false, useCursorTarget: true);
                    if (action != null)
                    {
                        PlayUiCue(CombatUiSfxCue.Confirm);
                        Commit(action);
                    }
                }
                return;
            }

            if (_mode == Mode.ExternalTargetSelect && _targetCandidates.Count > 0)
            {
                CombatBattleController.HudSlotRef target = _targetCandidates[Mathf.Clamp(_targetCursor, 0, _targetCandidates.Count - 1)];
                Action<string> callback = _externalTargetCommit;
                PlayUiCue(CombatUiSfxCue.Confirm);
                AbortSelection();
                callback?.Invoke(target.instanceId);
                return;
            }

            if (_mode == Mode.SwapSelect && IndexValid(_swapCursor, _swaps.Count))
            {
                var swap = _swaps[_swapCursor];
                if (!swap.available)
                {
                    PlayUiCue(CombatUiSfxCue.Invalid);
                    return;
                }
                PlayUiCue(CombatUiSfxCue.Confirm);
                Commit(CombatAction.Swap(_actingTeamIndex, swap.chainlingIndex));
            }
        }

        public void HandleCancel()
        {
            if (_mode == Mode.None)
                return;

            if (_mode == Mode.InfoModal)
            {
                PlayUiCue(CombatUiSfxCue.Cancel);
                CloseInfo();
                return;
            }

            if (_mode == Mode.TargetSelect)
            {
                _mode = Mode.MoveSelect;
                HideTargetingPrompt();
                ShowMovePanel();
                RefreshMoveUI();
                RefreshPreviewTargets();
                PlayUiCue(CombatUiSfxCue.Cancel);
                return;
            }


            if (_mode == Mode.ExternalTargetSelect)
            {
                PlayUiCue(CombatUiSfxCue.Cancel);
                Action externalCancel = _cancelToWallet;
                AbortSelection();
                externalCancel?.Invoke();
                return;
            }

            if (verboseLogs)
                Debug.Log("[CombatSelectionCoordinatorUGUI] Selection canceled.");
            PlayUiCue(CombatUiSfxCue.Cancel);
            var callback = _cancelToWallet;
            AbortSelection();
            callback?.Invoke();
        }

        public void HandleInfo()
        {
            if ((_mode != Mode.MoveSelect && _mode != Mode.TargetSelect) || IsRestRowSelected() || !IndexValid(_moveCursor, _moves.Count) || moveInfoPanel == null)
                return;

            var move = _moves[_moveCursor].move;
            var def = FindDefinition(move);
            string description = def != null && !string.IsNullOrEmpty(def.description) ? def.description : "No description available for this move.";
            string modifierDetails = BuildModifierDetail(move);
            ChainlingType resolvedType = ResolveMoveType(move);

            _returnModeAfterInfo = _mode;
            _mode = Mode.InfoModal;
            moveInfoPanel.SetContent(move.displayName, description, modifierDetails);
            moveInfoPanel.SetTypeSummary(ResolveMoveIconByType(resolvedType), BuildMoveTypeLabel(move, resolvedType));
            moveInfoPanel.SetChemistryRows(BuildChemistryRows(resolvedType));
            moveInfoPanel.SetVisible(true);
        }

        public void AbortSelection()
        {
            CancelAutoCommit();
            _mode = Mode.None;
            _returnModeAfterInfo = Mode.None;
            _state = null;
            _actingTeam = null;
            _opposingTeam = null;
            _actor = null;
            _queuedActionsBySlot = null;
            _actingSlotIndex = -1;
            _commitAction = null;
            _cancelToWallet = null;
            _externalTargetCommit = null;
            _activeTargetingPromptMessage = null;

            _moves.Clear();
            _swaps.Clear();
            _targetCandidates.Clear();
            _previewTargets.Clear();
            _invalidBlockTargetIds.Clear();
            _lockedBlockTargets.Clear();
            hudController?.SetSelectionLockedTargets(null);
            _reservedSwapBenchIndices.Clear();

            HidePanels();
            if (hudController != null)
            {
                hudController.ClearSelectionActiveSlot();
                hudController.ClearPreviewTargets();
            }
            battlefieldPresenter?.ClearSelectionPreview();
        }

        private void SetContext(CombatState state, int teamIndex, int slotIndex, ChainlingInstance actor, Action<CombatAction> onCommit, Action onCancel)
        {
            _state = state;
            _actingTeamIndex = teamIndex;
            _actingTeam = state.GetTeam(teamIndex);
            _opposingTeam = state.GetOpposingTeam(teamIndex);
            _actor = actor;
            _actingSlotIndex = slotIndex;
            _commitAction = onCommit;
            _cancelToWallet = onCancel;
            _targetCursor = 0;
            hudController?.SetSelectionActiveSlot(teamIndex, slotIndex);
        }

        private int ProjectedCurrentAp()
        {
            int current = _actor != null ? _actor.currentAP : 0;
            return current + Math.Max(0, _projectedGiftAp);
        }

        private void BuildMoveEntries()
        {
            _moves.Clear();
            if (_actor == null || _actor.moves == null)
                return;

            for (int i = 0; i < _actor.moves.Count; i++)
            {
                var move = _actor.moves[i];
                if (move == null)
                    continue;
                bool isPassive = move.category == MoveCategory.Passive;
                MoveSelectionEvaluation evaluation = isPassive
                    ? new MoveSelectionEvaluation(false, true, "PassiveMove")
                    : _eligibilityService.EvaluateMoveSelection(
                        _actor,
                        move,
                        _state,
                        extraAvailableAp: _projectedGiftAp,
                        queuedBySlot: _queuedActionsBySlot,
                        actorSlot: _actingSlotIndex);
                _moves.Add(new MoveEntry
                {
                    move = move,
                    moveIndex = i,
                    usable = evaluation.canSelect,
                    blockReason = evaluation.reason,
                    passiveState = isPassive
                        ? _eligibilityService.GetPassiveDisplayState(_actor, move)
                        : PassiveDisplayState.Active,
                });
            }
        }

        private void BuildSwapEntries()
        {
            _swaps.Clear();
            if (_actingTeam == null || _actingTeam.chainlings == null || _actingTeam.activeIndices == null)
                return;

            List<int> legal = _eligibilityService.GetAvailableSwapTargets(_actingTeam, _actor, _reservedSwapBenchIndices);
            for (int i = 0; i < _actingTeam.chainlings.Count; i++)
            {
                var chainling = _actingTeam.chainlings[i];
                if (chainling == null || !chainling.isAlive || _actingTeam.activeIndices.Contains(i))
                    continue;
                _swaps.Add(new SwapEntry
                {
                    chainlingIndex = i,
                    chainling = chainling,
                    available = legal.Contains(i),
                });
            }
        }

        private void RefreshMoveUI()
        {
            int rowCount = GetMoveListRowCount();
            var rows = new List<CombatMoveSelectPanelUGUI.MoveRowData>(rowCount);
            for (int i = 0; i < _moves.Count; i++)
            {
                var entry = _moves[i];
                var resolvedType = ResolveMoveType(entry.move);
                rows.Add(new CombatMoveSelectPanelUGUI.MoveRowData
                {
                    actionKind = CombatMoveSelectPanelUGUI.MoveRowActionKind.Move,
                    label = entry.move.displayName,
                    typeIcon = ResolveMoveIconByType(resolvedType),
                    isUsable = entry.usable,
                    hasPriority = MoveSelectDisplayUtility.HasPriorityTag(entry.move),
                });
            }

            if (rowCount > _moves.Count)
                rows.Add(moveSelectPanel.CreateRestRowData());

            moveSelectPanel.SetTitle(_mode == Mode.TargetSelect ? "Target Select" : "Move Select");
            moveSelectPanel.SetMoveRows(rows, _moveCursor);
            moveSelectPanel.SetSelectedDetails(BuildMoveDetailData());
            moveSelectPanel.SetLibraryCard(BuildLibraryCardPresentation());
            moveSelectPanel.SetFooterPrompt(_mode == Mode.TargetSelect
                ? "Navigate=Target   Confirm=Commit   Cancel=Back   Info=Details"
                : "Navigate=Move   Confirm=Choose   Cancel=Back   Info=Details");
        }

        private CombatMoveSelectPanelUGUI.MoveDetailData BuildMoveDetailData()
        {
            if (IsRestRowSelected())
            {
                CombatMoveSelectPanelUGUI.MoveDetailData rest = moveSelectPanel.CreateRestDetailData();
                if (AllEquippedMovesUnusableForAp())
                {
                    rest.shortDescription =
                        "No affordable moves this turn. Rest to recover AP.";
                }
                return rest;
            }

            if (_mode != Mode.ExternalTargetSelect && !IndexValid(_moveCursor, _moves.Count))
            {
                _modifierOverviewRows.Clear();
                return new CombatMoveSelectPanelUGUI.MoveDetailData
                {
                    moveName = "No Moves",
                    apText = "AP Cost --",
                    powerText = "Power --",
                    accuracyText = "Accuracy --",
                    targetingText = "Target --",
                    typeRoleLabel = "--",
                    isUsable = false,
                    insufficientAp = false,
                    modifierSummary = "Mods: None",
                    modifierRows = _modifierOverviewRows,
                    shortDescription = "No moves available.",
                    typeIcon = null,
                };
            }

            var entry = _moves[_moveCursor];
            var move = entry.move;
            var resolvedType = ResolveMoveType(move);
            var def = FindDefinition(move);
            string shortDesc = def != null && !string.IsNullOrEmpty(def.description) ? def.description : "No description available.";
            if (shortDesc.Length > 180) shortDesc = shortDesc.Substring(0, 180).TrimEnd() + "...";
            var roleColor = moveSelectPanel != null
                ? moveSelectPanel.ResolveTargetingColor(move)
                : MoveSelectDisplayUtility.ResolveTargetingColor(
                    move,
                    new Color(0.95f, 0.38f, 0.38f, 1f),
                    new Color(0.98f, 0.62f, 0.22f, 1f),
                    new Color(0.98f, 0.88f, 0.28f, 1f),
                    new Color(0.42f, 0.88f, 0.52f, 1f));

            return new CombatMoveSelectPanelUGUI.MoveDetailData
            {
                moveName = move.displayName,
                apText = move.category == MoveCategory.Passive
                    ? $"Passive {entry.passiveState.ToString().ToUpperInvariant()}"
                    : MoveSelectDisplayUtility.FormatApCostLine(move.apCost, ProjectedCurrentAp()),
                powerText = MoveSelectDisplayUtility.FormatPowerOrEffectLabel(move),
                accuracyText = MoveSelectDisplayUtility.FormatAccuracyLine(move),
                targetingText = MoveSelectDisplayUtility.FormatTargetingLine(move.targeting),
                typeRoleLabel = MoveSelectDisplayUtility.FormatTypeRoleLine(
                    MoveSelectDisplayUtility.FormatTypeLabel(move.baseType, resolvedType),
                    MoveSelectDisplayUtility.FormatRoleLabel(move.category)),
                roleTextColor = roleColor,
                hasRoleTextColor = true,
                isUsable = entry.usable,
                insufficientAp = !entry.usable && entry.blockReason == MoveSelectDisplayUtility.InsufficientApReason,
                hasPriority = MoveSelectDisplayUtility.HasPriorityTag(move),
                modifierSummary = BuildModifierSummary(move),
                modifierRows = BuildModifierOverviewRows(move),
                shortDescription = shortDesc,
                typeIcon = ResolveMoveIconByType(resolvedType),
            };
        }

        private CombatMoveLibraryCardView.Presentation BuildLibraryCardPresentation()
        {
            int direction = ConsumeLibraryCardDirection();
            if (IsRestRowSelected() || (_mode != Mode.ExternalTargetSelect && !IndexValid(_moveCursor, _moves.Count)))
            {
                CombatMoveLibraryCardView.Presentation blank = CombatMoveLibraryCardView.Presentation.Blank();
                blank.direction = direction;
                blank.roleLabel = IsRestRowSelected() ? "Rest" : string.Empty;
                return blank;
            }

            MoveInstance move = _moves[_moveCursor].move;
            if (move == null)
            {
                CombatMoveLibraryCardView.Presentation blank = CombatMoveLibraryCardView.Presentation.Blank();
                blank.direction = direction;
                return blank;
            }

            MoveDefinitionSO definition = FindDefinition(move);
            var presentation = new CombatMoveLibraryCardView.Presentation
            {
                hasMove = true,
                learnedFace = LibraryCardFaces.IsLearned(move.acquisition),
                moveName = move.displayName,
                statsInk = definition != null
                    ? LibraryCardLayout.FormatStatsInk(definition, definition.description)
                    : string.Empty,
                typeIcon = ResolveMoveIconByType(ResolveMoveType(move)),
                moveRarity = move.rarity,
                apCost = move.apCost,
                currentAp = ProjectedCurrentAp(),
                roleLabel = ResolveLibraryCardRoleLabel(move),
                direction = direction,
            };

            var sectionIds = MoveSelectDisplayUtility.CollectAttachedModifierSectionIds(move);
            if (sectionIds.Count > 0)
                FillLibraryCardMod(ref presentation, move, sectionIds[0], first: true);
            if (sectionIds.Count > 1)
                FillLibraryCardMod(ref presentation, move, sectionIds[1], first: false);
            return presentation;
        }

        private static string ResolveLibraryCardRoleLabel(MoveInstance move)
        {
            string defenseBadge = CombatWallPopupLabelUtility.FormatDefenseCardBadge(move);
            return !string.IsNullOrWhiteSpace(defenseBadge)
                ? defenseBadge
                : MoveSelectDisplayUtility.FormatRoleLabel(move.category);
        }

        private int ConsumeLibraryCardDirection()
        {
            int direction = _libraryCardDirection;
            _libraryCardDirection = 0;
            return direction;
        }

        private void FillLibraryCardMod(
            ref CombatMoveLibraryCardView.Presentation presentation,
            MoveInstance move,
            string sectionId,
            bool first)
        {
            ModifierInstance runtime = MoveSelectDisplayUtility.FindRuntimeModifier(move, sectionId);
            ModifierDefinitionSO definition = modifierCatalog != null
                ? modifierCatalog.GetBySectionId(sectionId)
                : null;
            string name = definition != null ? definition.GetDisplayName() : sectionId;
            string effect = definition != null ? definition.effectDescription : string.Empty;
            RarityTier rarity = MoveSelectDisplayUtility.ResolveModifierDisplayRarity(runtime, definition);
            if (first)
            {
                presentation.hasMod1 = !string.IsNullOrWhiteSpace(name);
                presentation.mod1Name = name;
                presentation.mod1Effect = effect;
                presentation.mod1Rarity = rarity;
            }
            else
            {
                presentation.hasMod2 = !string.IsNullOrWhiteSpace(name);
                presentation.mod2Name = name;
                presentation.mod2Effect = effect;
                presentation.mod2Rarity = rarity;
            }
        }

        private IReadOnlyList<CombatMoveSelectPanelUGUI.ModifierOverviewRowData> BuildModifierOverviewRows(MoveInstance move)
        {
            _modifierOverviewRows.Clear();
            if (move == null)
                return _modifierOverviewRows;

            var sectionIds = MoveSelectDisplayUtility.CollectAttachedModifierSectionIds(move);
            if (sectionIds.Count == 0)
                return _modifierOverviewRows;

            for (int i = 0; i < sectionIds.Count; i++)
            {
                string sectionId = sectionIds[i];
                var runtime = MoveSelectDisplayUtility.FindRuntimeModifier(move, sectionId);
                var definition = modifierCatalog != null
                    ? modifierCatalog.GetBySectionId(sectionId)
                    : null;

                _modifierOverviewRows.Add(new CombatMoveSelectPanelUGUI.ModifierOverviewRowData
                {
                    description = MoveSelectDisplayUtility.FormatModifierDescription(runtime, definition),
                    rarity = MoveSelectDisplayUtility.ResolveModifierDisplayRarity(runtime, definition),
                });
            }

            return _modifierOverviewRows;
        }

        private void RefreshSwapUI()
        {
            var rows = new List<CombatSwapSelectPanelUGUI.SwapRowData>(_swaps.Count);
            int availableCount = 0;
            for (int i = 0; i < _swaps.Count; i++)
            {
                var c = _swaps[i].chainling;
                bool available = _swaps[i].available;
                if (available)
                    availableCount++;
                rows.Add(new CombatSwapSelectPanelUGUI.SwapRowData
                {
                    label = $"{c.displayName}  ★{c.stars}  HP {c.currentHP}/{c.EffectiveMaxHP}",
                    typeIcon = ResolveChainlingIcon(c),
                    isAvailable = available,
                });
            }

            if (availableCount == 0)
            {
                if (_swaps.Count == 0)
                    swapSelectPanel.SetEmptyStateMessage("No swap candidates available");
                else
                    swapSelectPanel.SetEmptyStateMessage("No chainlings available to swap");
            }

            swapSelectPanel.SetRows(rows, _swapCursor);
            swapSelectPanel.SetFooterPrompt(availableCount > 0
                ? "Confirm=Swap   Cancel=Back"
                : "No chainlings available to swap   Cancel=Back");
        }

        private void RefreshPreviewTargets()
        {
            if (hudController == null || _mode == Mode.None || _mode == Mode.SwapSelect || _mode == Mode.InfoModal)
            {
                hudController?.ClearPreviewTargets();
                battlefieldPresenter?.ClearSelectionPreview();
                return;
            }

            string actorId = _actor != null ? _actor.instanceId : null;

            if (IsRestRowSelected())
            {
                hudController.ClearPreviewTargets();
                if (!string.IsNullOrEmpty(actorId))
                    battlefieldPresenter?.SetSelectionPreview(actorId, null);
                else
                    battlefieldPresenter?.ClearSelectionPreview();
                return;
            }

            if (_mode != Mode.ExternalTargetSelect && !IndexValid(_moveCursor, _moves.Count))
            {
                hudController.ClearPreviewTargets();
                battlefieldPresenter?.ClearSelectionPreview();
                return;
            }

            // During MoveSelect, only show the active-actor indicator — no target highlights yet.
            if (_mode == Mode.MoveSelect)
            {
                hudController.ClearPreviewTargets();
                if (!string.IsNullOrEmpty(actorId))
                    battlefieldPresenter?.SetSelectionPreview(actorId, null);
                else
                    battlefieldPresenter?.ClearSelectionPreview();
                return;
            }

            bool isMulti = false;
            // Move target selection rebuilds from the selected move. External command selection
            // supplies its already-filtered candidates and only moves the cursor over them.
            if (_mode == Mode.TargetSelect)
                BuildTargetsForMove(_moves[_moveCursor].move, out _, out isMulti);
            if (_targetCandidates.Count > 0)
            {
                _previewTargets.Clear();
                _previewTargets.Add(_targetCandidates[Mathf.Clamp(_targetCursor, 0, _targetCandidates.Count - 1)]);
            }

            if (_previewTargets.Count == 0)
            {
                hudController.ClearPreviewTargets();
                if (!string.IsNullOrEmpty(actorId))
                    battlefieldPresenter?.SetSelectionPreview(actorId, null);
                else
                    battlefieldPresenter?.ClearSelectionPreview();
            }
            else
            {
                hudController.PreviewTargets(_previewTargets, isMulti);
                string targetId = _previewTargets[0].instanceId;
                battlefieldPresenter?.SetSelectionPreview(actorId, targetId);
            }
        }

        private void BuildTargetsForMove(MoveInstance move, out bool requiresTargetSelect, out bool isMulti)
        {
            requiresTargetSelect = false;
            isMulti = false;
            _targetCandidates.Clear();
            _previewTargets.Clear();
            _invalidBlockTargetIds.Clear();
            _lockedBlockTargets.Clear();
            hudController?.SetSelectionLockedTargets(null);
            if (move == null) return;

            bool isDamaging = move.category == MoveCategory.DamagingMelee || move.category == MoveCategory.DamagingRanged;
            bool spreadDamage = isDamaging && !HasStatus(_actor, StatusId.Anchored) &&
                                (HasStatus(_actor, StatusId.Broadcast) || HasStatus(_actor, StatusId.Splinter));
            if (spreadDamage)
            {
                AddActiveTargets(_opposingTeam, _previewTargets);
                _targetCandidates.AddRange(_previewTargets);
                isMulti = true;
                return;
            }

            if (move.targeting == MoveTargeting.AllEnemies) { AddActiveTargets(_opposingTeam, _previewTargets); isMulti = true; }
            else if (move.targeting == MoveTargeting.AllAllies) { AddActiveTargets(_actingTeam, _previewTargets); isMulti = true; }
            else if (move.targeting == MoveTargeting.Field) { AddActiveTargets(_actingTeam, _previewTargets); AddActiveTargets(_opposingTeam, _previewTargets); isMulti = true; }
            else if (move.targeting == MoveTargeting.Self) { TryAddTarget(_actingTeam, _actor, _previewTargets); }
            else if (move.targeting == MoveTargeting.SingleAlly) { AddActiveTargets(_actingTeam, _targetCandidates); }
            else { AddActiveTargets(_opposingTeam, _targetCandidates); }

            if (!isMulti && _targetCandidates.Count > 0)
            {
                if (CombatWallStateUtility.MovePlacesBlock(move))
                {
                    for (int i = 0; i < _targetCandidates.Count; i++)
                    {
                        CombatBattleController.HudSlotRef candidate = _targetCandidates[i];
                        ChainlingInstance target = _actingTeam?.GetActiveInSlot(candidate.slotIndex);
                        BlockTargetEvaluation evaluation = _eligibilityService.EvaluateBlockTarget(
                            _actor, move, target, _state, _queuedActionsBySlot, _actingSlotIndex);
                        if (!evaluation.canTarget && !string.IsNullOrEmpty(candidate.instanceId))
                        {
                            _invalidBlockTargetIds.Add(candidate.instanceId);
                            _lockedBlockTargets.Add(candidate);
                        }
                    }
                    hudController?.SetSelectionLockedTargets(_lockedBlockTargets);
                    _targetCursor = FindNextValidTargetIndex(_targetCursor - 1, 1);
                }
                _previewTargets.Add(_targetCandidates[Mathf.Clamp(_targetCursor, 0, _targetCandidates.Count - 1)]);
            }

            int selectableCount = _targetCandidates.Count - _invalidBlockTargetIds.Count;
            requiresTargetSelect = !isMulti && (CombatWallStateUtility.MovePlacesBlock(move)
                ? selectableCount > 1
                : _targetCandidates.Count > 1);
        }

        private void AddActiveTargets(TeamState team, List<CombatBattleController.HudSlotRef> output)
        {
            if (team == null || team.activeIndices == null || team.chainlings == null) return;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                int idx = team.activeIndices[slot];
                if (idx < 0 || idx >= team.chainlings.Count) continue;
                var c = team.chainlings[idx];
                if (c == null || !c.isAlive) continue;
                TryAddTarget(team, c, output);
            }
        }

        private void TryAddTarget(TeamState team, ChainlingInstance chainling, List<CombatBattleController.HudSlotRef> output)
        {
            if (!TryBuildHudSlotRef(team, chainling, out var slotRef))
                return;
            for (int i = 0; i < output.Count; i++)
                if (output[i].instanceId == slotRef.instanceId) return;
            output.Add(slotRef);
        }

        private bool TryBuildHudSlotRef(TeamState team, ChainlingInstance chainling, out CombatBattleController.HudSlotRef slotRef)
        {
            slotRef = default;
            if (team == null || chainling == null || team.activeIndices == null || team.chainlings == null) return false;
            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                int idx = team.activeIndices[slot];
                if (idx < 0 || idx >= team.chainlings.Count) continue;
                var c = team.chainlings[idx];
                if (c != null && c.instanceId == chainling.instanceId)
                {
                    slotRef = new CombatBattleController.HudSlotRef(team.teamIndex, slot, c.instanceId);
                    return true;
                }
            }
            return false;
        }

        private void ShowMovePanel()
        {
            HideTargetingPrompt();
            hudController.SetBottomActionPanelVisible(true);
            moveSelectPanel.SetVisible(true);
            swapSelectPanel?.SetVisible(false);
            moveInfoPanel?.SetVisible(false);
        }

        private void ShowSwapPanel()
        {
            hudController.SetBottomActionPanelVisible(true);
            moveSelectPanel?.SetVisible(false);
            swapSelectPanel.SetVisible(true);
            moveInfoPanel?.SetVisible(false);
        }

        private void HidePanels()
        {
            HideTargetingPrompt();
            moveSelectPanel?.SetVisible(false);
            swapSelectPanel?.SetVisible(false);
            moveInfoPanel?.SetVisible(false);
            hudController?.SetBottomActionPanelVisible(false);
        }

        private void ShowTargetingPrompt()
        {
            hudController?.SetBottomActionPanelVisible(true);
            var label = EnsureTargetingPromptLabel();
            moveSelectPanel?.HoldOpenForTargetingPrompt(label != null ? label.gameObject : null);
            if (label == null) return;

            label.text = !string.IsNullOrWhiteSpace(_activeTargetingPromptMessage)
                ? _activeTargetingPromptMessage
                : string.IsNullOrEmpty(targetingPromptMessage) ? "Select your target" : targetingPromptMessage;
            label.gameObject.SetActive(true);

            _targetingPromptTween?.Kill();
            var c = label.color;
            c.a = 0f;
            label.color = c;
            _targetingPromptTween = DOTween.ToAlpha(() => label.color, x => label.color = x, 1f, Mathf.Max(0.05f, targetingPromptFadeDuration))
                .SetUpdate(true)
                .SetEase(Ease.OutSine);
        }

        private void HideTargetingPrompt()
        {
            _targetingPromptTween?.Kill();
            _targetingPromptTween = null;
            if (targetingPromptLabel != null)
                targetingPromptLabel.gameObject.SetActive(false);
        }

        private TMP_Text EnsureTargetingPromptLabel()
        {
            if (targetingPromptLabel != null)
                return targetingPromptLabel;

            if (moveSelectPanel == null)
                return null;

            var parent = moveSelectPanel.transform.parent;
            if (parent == null)
                parent = moveSelectPanel.transform;

            var go = new GameObject("TargetingPromptLabel", typeof(RectTransform), typeof(CanvasRenderer));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);

            var panelRect = moveSelectPanel.transform as RectTransform;
            if (panelRect != null)
            {
                rect.anchorMin = panelRect.anchorMin;
                rect.anchorMax = panelRect.anchorMax;
                rect.offsetMin = panelRect.offsetMin;
                rect.offsetMax = panelRect.offsetMax;
                rect.anchoredPosition = panelRect.anchoredPosition;
            }
            else
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 36;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            text.text = "Select your target";

            go.SetActive(false);
            targetingPromptLabel = text;
            return targetingPromptLabel;
        }

        private void CloseInfo()
        {
            moveInfoPanel?.SetVisible(false);
            _mode = _returnModeAfterInfo;
            _returnModeAfterInfo = Mode.None;
            if (_mode == Mode.MoveSelect || _mode == Mode.TargetSelect) { ShowMovePanel(); RefreshMoveUI(); RefreshPreviewTargets(); }
            else if (_mode == Mode.SwapSelect) { ShowSwapPanel(); RefreshSwapUI(); }
            else HidePanels();
        }

        private CombatAction BuildMoveAction(int moveIndex, bool isMulti, bool useCursorTarget)
        {
            if (isMulti)
                return CombatAction.UseMove(_actingTeamIndex, moveIndex);

            if (TryGetSelectedTarget(useCursorTarget, out var target))
            {
                MoveInstance move = IndexValid(_moveCursor, _moves.Count) ? _moves[_moveCursor].move : null;
                if (CombatWallStateUtility.MovePlacesBlock(move) && _invalidBlockTargetIds.Contains(target.instanceId))
                {
                    ChainlingInstance targetChainling = _actingTeam?.GetActiveInSlot(target.slotIndex);
                    BlockTargetEvaluation evaluation = _eligibilityService.EvaluateBlockTarget(
                        _actor, move, targetChainling, _state, _queuedActionsBySlot, _actingSlotIndex);
                    string targetName = targetChainling != null && !string.IsNullOrWhiteSpace(targetChainling.displayName)
                        ? targetChainling.displayName
                        : "That Chainling";
                    _activeTargetingPromptMessage = evaluation.reason == "BlockAlreadyQueued"
                        ? $"{targetName} is already getting a block."
                        : $"{targetName} already has a block.";
                    ShowTargetingPrompt();
                    PlayUiCue(CombatUiSfxCue.Invalid);
                    return null;
                }
                return CombatAction.UseMove(_actingTeamIndex, moveIndex, target.teamIndex, target.slotIndex);
            }

            return CombatAction.UseMove(_actingTeamIndex, moveIndex);
        }

        private bool TryGetSelectedTarget(bool useCursorTarget, out CombatBattleController.HudSlotRef target)
        {
            target = default;

            if (_targetCandidates.Count > 0)
            {
                MoveInstance selectedMove = IndexValid(_moveCursor, _moves.Count) ? _moves[_moveCursor].move : null;
                bool honorCursor = useCursorTarget || CombatWallStateUtility.MovePlacesBlock(selectedMove);
                int index = honorCursor ? Mathf.Clamp(_targetCursor, 0, _targetCandidates.Count - 1) : 0;
                target = _targetCandidates[index];
                return true;
            }

            if (_previewTargets.Count > 0)
            {
                target = _previewTargets[0];
                return true;
            }

            return false;
        }

        private int FindNextValidTargetIndex(int startIndex, int step)
        {
            if (_targetCandidates.Count == 0)
                return 0;
            int direction = step < 0 ? -1 : 1;
            int index = Mathf.Clamp(startIndex, -1, _targetCandidates.Count - 1);
            for (int attempts = 0; attempts < _targetCandidates.Count; attempts++)
            {
                index = Wrap(index + direction, _targetCandidates.Count);
                string instanceId = _targetCandidates[index].instanceId;
                if (!_invalidBlockTargetIds.Contains(instanceId))
                    return index;
            }
            return Mathf.Clamp(startIndex, 0, _targetCandidates.Count - 1);
        }

        private void BeginAutoCommit(CombatAction action)
        {
            CancelAutoCommit();
            _autoCommitPending = true;
            _autoCommitRoutine = StartCoroutine(AutoCommitRoutine(action));
        }

        private IEnumerator AutoCommitRoutine(CombatAction action)
        {
            float delay = GetAutoCommitDelay();
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            _autoCommitRoutine = null;
            _autoCommitPending = false;

            if (_mode != Mode.None)
                Commit(action);
        }

        private void CancelAutoCommit()
        {
            _autoCommitPending = false;
            if (_autoCommitRoutine != null)
            {
                StopCoroutine(_autoCommitRoutine);
                _autoCommitRoutine = null;
            }
        }

        private float GetAutoCommitDelay()
        {
            if (uiAnimTuning != null)
                return Mathf.Max(0f, uiAnimTuning.selectionAutoCommitDelay);
            return Mathf.Max(0f, autoCommitPreviewSeconds);
        }

        private void Commit(CombatAction action)
        {
            if (verboseLogs) Debug.Log($"[CombatSelectionCoordinatorUGUI] Commit {action.actionType}.");
            var callback = _commitAction;
            AbortSelection();
            callback?.Invoke(action);
        }

        private void PlayUiCue(CombatUiSfxCue cue)
        {
            if (_uiSfxUnavailable && uiSfxBehaviour == null && _cachedUiSfxBehaviour == null)
                return;

            ICombatUiSfxPlayer uiSfx = CombatUiSfxResolver.Resolve(uiSfxBehaviour, ref _cachedUiSfxBehaviour);
            if (uiSfx == null)
            {
                _uiSfxUnavailable = true;
                return;
            }

            uiSfx?.PlayCue(cue);
        }

        private MoveDefinitionSO FindDefinition(MoveInstance move)
        {
            if (move == null || string.IsNullOrEmpty(move.definitionId)) return null;
            if (moveDatabase == null && battleController != null) moveDatabase = battleController.MoveDatabase;
            return moveDatabase != null ? moveDatabase.GetById(move.definitionId) : null;
        }

        private Sprite ResolveMoveIconByType(ChainlingType type)
        {
            if (moveTypeIconPalette != null)
            {
                var icon = moveTypeIconPalette.GetIcon(type);
                if (icon != null) return icon;
            }
            return fallbackMoveTypeIcon;
        }

        private Sprite ResolveChainlingIcon(ChainlingInstance chainling)
        {
            if (chainling != null && moveTypeIconPalette != null)
            {
                var resolved = chainling.GetResolvedTypes();
                var type = resolved != null && resolved.Length > 0 ? resolved[0] : chainling.primaryType;
                var icon = moveTypeIconPalette.GetIcon(type);
                if (icon != null) return icon;
            }
            return fallbackMoveTypeIcon;
        }

        private static bool HasStatus(ChainlingInstance chainling, StatusId status)
        {
            if (chainling == null || chainling.statuses == null) return false;
            for (int i = 0; i < chainling.statuses.Count; i++) if (chainling.statuses[i].statusId == status) return true;
            return false;
        }

        private string BuildModifierSummary(MoveInstance move)
        {
            if (move == null)
                return "Mods: None";

            var sectionIds = MoveSelectDisplayUtility.CollectAttachedModifierSectionIds(move);
            if (sectionIds.Count == 0)
                return "Mods: None";

            var sb = new StringBuilder($"Mods {sectionIds.Count}: ");
            for (int i = 0; i < Mathf.Min(3, sectionIds.Count); i++)
            {
                if (i > 0) sb.Append(", ");
                string sectionId = sectionIds[i];
                ModifierDefinitionSO definition = modifierCatalog != null
                    ? modifierCatalog.GetBySectionId(sectionId)
                    : null;
                sb.Append(definition != null ? definition.GetDisplayName() : sectionId);
            }
            if (sectionIds.Count > 3) sb.Append("...");
            return sb.ToString();
        }

        private string BuildModifierDetail(MoveInstance move)
        {
            if (move == null || move.modifiers == null || move.modifiers.Count == 0) return "No modifiers attached.";
            var sb = new StringBuilder();
            for (int i = 0; i < move.modifiers.Count; i++)
            {
                var m = move.modifiers[i];
                if (i > 0) sb.Append('\n');
                sb.Append("- ");
                string sectionId = !string.IsNullOrEmpty(m.definitionSectionId) ? m.definitionSectionId : "Modifier";
                ModifierDefinitionSO definition = modifierCatalog != null
                    ? modifierCatalog.GetBySectionId(sectionId)
                    : null;
                sb.Append(definition != null ? definition.GetDisplayName() : sectionId);
                sb.Append(" [").Append(m.category).Append('/').Append(m.scope).Append("] value=").Append(m.effectValue);
            }
            return sb.ToString();
        }

        private ChainlingType ResolveMoveType(MoveInstance move)
        {
            if (move == null)
                return ChainlingType.None;
            return _actor != null ? DamageCalculator.ResolveAttackType(_actor, move) : move.baseType;
        }

        private string BuildMoveTypeLabel(MoveInstance move, ChainlingType resolvedType)
        {
            if (move == null)
                return "Type: --";

            return $"Type: {MoveSelectDisplayUtility.FormatTypeLabel(move.baseType, resolvedType)}";
        }

        private IReadOnlyList<CombatMoveInfoPanelUGUI.ChemistryRowData> BuildChemistryRows(ChainlingType attackerType)
        {
            _chemistryRows.Clear();

            if (typeChemistryMatrix == null || attackerType == ChainlingType.None)
                return _chemistryRows;

            foreach (ChainlingType defenderType in Enum.GetValues(typeof(ChainlingType)))
            {
                if (defenderType == ChainlingType.None)
                    continue;

                if (!typeChemistryMatrix.TryGetReaction(attackerType, defenderType, out ChemistryOutput output))
                    continue;

                _chemistryRows.Add(new CombatMoveInfoPanelUGUI.ChemistryRowData
                {
                    defenderIcon = ResolveMoveIconByType(defenderType),
                    defenderLabel = defenderType.ToString(),
                    reactionSummary = BuildChemistryOutputSummary(output),
                });
            }

            return _chemistryRows;
        }

        private static string BuildChemistryOutputSummary(ChemistryOutput output)
        {
            if (output.statusId == StatusId.None)
                return "No chemistry outputs";

            var sb = new StringBuilder();
            sb.Append(FormatStatusName(output.statusId));
            if (output.parameterType != ChainlingType.None)
                sb.Append(" (").Append(output.parameterType).Append(')');
            sb.Append(output.recipient == ChemistryRecipient.Attacker ? " → Self" : " → Foe");

            return sb.ToString();
        }

        private static string FormatStatusName(StatusId statusId)
        {
            string raw = statusId.ToString();
            if (string.IsNullOrEmpty(raw))
                return "Unknown";

            var sb = new StringBuilder(raw.Length + 8);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (i > 0 && char.IsUpper(c) && !char.IsUpper(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(c);
            }

            return sb.ToString();
        }

        private void ResolveFallbacks()
        {
            if (battleController == null) battleController = GetComponent<CombatBattleController>();
            if (battleController == null) battleController = FindObjectOfType<CombatBattleController>();
            if (hudController == null) hudController = FindObjectOfType<CombatBattleUGUIController>(true);
            if (moveSelectPanel == null && hudController != null) moveSelectPanel = hudController.GetComponentInChildren<CombatMoveSelectPanelUGUI>(true);
            if (swapSelectPanel == null && hudController != null) swapSelectPanel = hudController.GetComponentInChildren<CombatSwapSelectPanelUGUI>(true);
            if (moveInfoPanel == null && hudController != null) moveInfoPanel = hudController.GetComponentInChildren<CombatMoveInfoPanelUGUI>(true);
            if (moveDatabase == null && battleController != null) moveDatabase = battleController.MoveDatabase;
            if (modifierCatalog == null && battleController != null) modifierCatalog = battleController.ModifierCatalog;
            if (typeChemistryMatrix == null && battleController != null) typeChemistryMatrix = battleController.TypeChemistryMatrix;
            if (moveTypeIconPalette == null && hudController != null) moveTypeIconPalette = hudController.TypeIconPalette;
            if (fallbackMoveTypeIcon == null && hudController != null) fallbackMoveTypeIcon = hudController.FallbackTypeIcon;
            if (uiAnimTuning == null && hudController != null) uiAnimTuning = hudController.GetComponentInParent<CombatUiAnimTuning>(true);
            if (uiAnimTuning == null) uiAnimTuning = FindObjectOfType<CombatUiAnimTuning>(true);
            if (battlefieldPresenter == null) battlefieldPresenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);
        }

        private int FirstUsableMoveIndex()
        {
            for (int i = 0; i < _moves.Count; i++)
            {
                if (_moves[i].usable)
                    return i;
            }

            // No affordable/legal moves: let the caller land on Rest.
            return -1;
        }

        private bool AllEquippedMovesUnusableForAp()
        {
            if (_moves.Count == 0)
                return false;

            bool sawApBlock = false;
            for (int i = 0; i < _moves.Count; i++)
            {
                MoveEntry entry = _moves[i];
                if (entry.move != null && entry.move.category == MoveCategory.Passive)
                    continue;
                if (entry.usable)
                    return false;
                if (entry.blockReason == MoveSelectDisplayUtility.InsufficientApReason)
                    sawApBlock = true;
            }

            return sawApBlock;
        }

        private int FirstAvailableSwapIndex()
        {
            for (int i = 0; i < _swaps.Count; i++)
            {
                if (_swaps[i].available)
                    return i;
            }

            return _swaps.Count > 0 ? 0 : -1;
        }

        private int NavToStep(Vector2 nav)
        {
            float ax = Mathf.Abs(nav.x);
            float ay = Mathf.Abs(nav.y);
            if (ax < navigateThreshold && ay < navigateThreshold) return 0;
            if (ay >= ax) return nav.y > 0f ? -1 : 1;
            return nav.x > 0f ? 1 : -1;
        }

        private int GetMoveListRowCount() => _moves.Count + 1;

        private bool IsRestRowSelected() => _mode == Mode.MoveSelect && _moveCursor == _moves.Count;

        private static int Wrap(int index, int count) => count <= 0 ? 0 : (index < 0 ? count - 1 : index >= count ? 0 : index);
        private static bool IndexValid(int index, int count) => count > 0 && index >= 0 && index < count;
    }
}
