using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Chainbound.Combat
{
    /// <summary>
    /// Bridges player-turn action submission to the existing 3D selector, without
    /// depending on UI Toolkit battle menus.
    /// </summary>
    [DefaultExecutionOrder(-880)]
    [DisallowMultipleComponent]
    public sealed class CombatActionSelectorBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CombatBattleController battleController;
        [SerializeField] private CombatActionSelectorController actionSelector;
        [SerializeField] private CombatSelectionCoordinatorUGUI selectionCoordinator;
        [SerializeField] private CaptureCommandCoordinatorUGUI captureCoordinator;
        [SerializeField] private CombatBattleUGUIController hudController;
        [SerializeField] private Combat2BattlefieldPresenter battlefieldPresenter;
        [SerializeField] private InputActionAsset inputActions;

        [Header("Input")]
        [SerializeField] private string combatActionMapName = "UI";
        [SerializeField] private string navigateActionName = "Navigate";
        [SerializeField] private string confirmActionName = "Submit";
        [SerializeField] private string cancelActionName = "Cancel";
        [SerializeField] private string infoActionName = "DetailedView";
        [SerializeField] private string playerActionMapName = "Player";
        [SerializeField] private bool disablePlayerMapWhileSelecting;
        [SerializeField] private float navigateDeadzone = 0.35f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogs;
        [SerializeField] private bool logInputEvents = true;

        private InputActionMap _combatActionMap;
        private InputActionMap _playerActionMap;
        private InputAction _navigateAction;
        private InputAction _confirmAction;
        private InputAction _cancelAction;
        private InputAction _infoAction;

        private readonly List<int> _pendingSlots = new List<int>(2);
        private readonly Dictionary<int, CombatAction> _queuedActionsBySlot = new Dictionary<int, CombatAction>(2);
        private readonly HashSet<int> _reservedBenchIndices = new HashSet<int>();
        private readonly CombatActionEligibilityService _eligibilityService = new CombatActionEligibilityService();
        private CombatState _pendingState;
        private int _pendingSlotCursor = -1;
        private bool _awaitingSelection;
        private bool _battleEventsSubscribed;
        private bool _selectorEventsSubscribed;
        private bool _warnedUsePlayerInputDisabled;
        private bool _warnedInputAssetMissing;
        private bool _warnedCombatMapMissing;
        private bool _navigateWasActiveLastFrame;

        public bool VerboseLogs => verboseLogs;

        public void SetSelectionCoordinator(CombatSelectionCoordinatorUGUI coordinator)
        {
            if (coordinator != null)
                selectionCoordinator = coordinator;
        }

        public void SetSelectionFeedbackTargets(CombatBattleUGUIController hud, Combat2BattlefieldPresenter presenter)
        {
            if (hud != null)
                hudController = hud;
            if (presenter != null)
                battlefieldPresenter = presenter;
        }

        public void SetVerboseLogging(bool enabled, bool enableInputEventLogs = true)
        {
            verboseLogs = enabled;
            logInputEvents = enableInputEventLogs;
            if (verboseLogs)
                LogInputState("SetVerboseLogging");
        }

        public void Configure(
            CombatBattleController controller,
            CombatActionSelectorController selector,
            InputActionAsset actions,
            bool disablePlayerMapWhileSelectingOption = false)
        {
            if (controller != null)
                battleController = controller;
            if (selector != null)
                actionSelector = selector;
            if (actions != null)
                inputActions = actions;

            disablePlayerMapWhileSelecting = disablePlayerMapWhileSelectingOption;
            if (verboseLogs)
            {
                Debug.Log($"[CombatActionSelectorBridge] Configure(controller='{battleController?.name}', selector='{actionSelector?.name}', inputAsset='{inputActions?.name}').");
            }
            TryResolveSelectionCoordinator();
            TryResolveCaptureCoordinator();
            TryResolveSelectionFeedbackTargets();
            WarnIfManualInputDisabled();
            ResolveInputActions();
            SubscribeBattleEvents();
            SubscribeSelectorEvents();
            TryResumePendingPlayerTurn();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoAttachToBattleControllers()
        {
            var controllers = FindObjectsOfType<CombatBattleController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                var controller = controllers[i];
                if (controller == null)
                    continue;
                if (controller.GetComponent<CombatActionSelectorBridge>() == null)
                    controller.gameObject.AddComponent<CombatActionSelectorBridge>();
            }
        }

        private void Awake()
        {
            if (battleController == null)
                battleController = GetComponent<CombatBattleController>();
            if (battleController == null)
                battleController = FindObjectOfType<CombatBattleController>();

            WarnIfManualInputDisabled();

            TryResolveSelector();
            TryResolveSelectionCoordinator();
            TryResolveCaptureCoordinator();
            TryResolveSelectionFeedbackTargets();
            ResolveInputActions();
            if (verboseLogs)
                LogInputState("Awake");
        }

        private void OnEnable()
        {
            TryResolveSelectionCoordinator();
            TryResolveCaptureCoordinator();
            TryResolveSelectionFeedbackTargets();
            SubscribeBattleEvents();
            SubscribeSelectorEvents();
            TryResumePendingPlayerTurn();
            if (verboseLogs)
                LogInputState("OnEnable");
        }

        private void OnDisable()
        {
            UnsubscribeBattleEvents();
            UnsubscribeSelectorEvents();
            CancelSelectionSession(clearQueuedActions: true);
        }

        private void Update()
        {
            if (!_awaitingSelection)
                return;

            if (_navigateAction != null)
            {
                Vector2 nav = _navigateAction.ReadValue<Vector2>();
                float deadzoneSq = navigateDeadzone * navigateDeadzone;
                bool navActive = nav.sqrMagnitude >= deadzoneSq;
                if (navActive)
                {
                    if (captureCoordinator != null && captureCoordinator.IsPickerOpen)
                    {
                        // The EventSystem owns list navigation while the orb picker is visible.
                    }
                    else if (selectionCoordinator != null && selectionCoordinator.IsSelecting)
                    {
                        selectionCoordinator.HandleNavigate(nav);
                    }
                    else
                    {
                        if (actionSelector == null)
                        {
                            QueueActionForCurrentSlot(BuildFallbackActionForCurrentSlot());
                            AdvanceToNextPendingSlot();
                            return;
                        }
                        actionSelector.HandleNavigate(nav);
                    }

                    if (verboseLogs && logInputEvents && !_navigateWasActiveLastFrame)
                    {
                        Debug.Log($"[CombatActionSelectorBridge] Navigate '{_navigateAction.name}' value={nav} map='{_combatActionMap?.name}'.");
                    }
                }

                _navigateWasActiveLastFrame = navActive;
            }
            else
            {
                _navigateWasActiveLastFrame = false;
            }

            if (_confirmAction != null && _confirmAction.WasPressedThisFrame())
            {
                if (verboseLogs && logInputEvents)
                    Debug.Log($"[CombatActionSelectorBridge] Submit '{_confirmAction.name}' pressed.");
                if (captureCoordinator != null && captureCoordinator.IsPickerOpen)
                {
                    // The EventSystem submits the focused orb row.
                }
                else if (selectionCoordinator != null && selectionCoordinator.IsSelecting)
                {
                    selectionCoordinator.HandleConfirm();
                }
                else
                {
                    if (actionSelector == null)
                    {
                        QueueActionForCurrentSlot(BuildFallbackActionForCurrentSlot());
                        AdvanceToNextPendingSlot();
                        return;
                    }
                    actionSelector.HandleConfirm();
                }
            }

            if (_cancelAction != null && _cancelAction.WasPressedThisFrame())
            {
                if (verboseLogs && logInputEvents)
                    Debug.Log($"[CombatActionSelectorBridge] Cancel '{_cancelAction.name}' pressed.");
                if (captureCoordinator != null && captureCoordinator.IsPickerOpen)
                    captureCoordinator.HandleCancel();
                else if (selectionCoordinator != null && selectionCoordinator.IsSelecting)
                    selectionCoordinator.HandleCancel();
                else
                    HandleWalletBack();
            }

            if (_infoAction != null && _infoAction.WasPressedThisFrame())
            {
                if (verboseLogs && logInputEvents)
                    Debug.Log($"[CombatActionSelectorBridge] Detailed view '{_infoAction.name}' pressed.");
                if (selectionCoordinator != null && selectionCoordinator.IsSelecting)
                    selectionCoordinator.HandleInfo();
            }
        }

        private void ResolveInputActions()
        {
            _combatActionMap = null;
            _playerActionMap = null;
            _navigateAction = null;
            _confirmAction = null;
            _cancelAction = null;
            _infoAction = null;

            if (!TryResolveInputAsset())
            {
                if (!_warnedInputAssetMissing)
                {
                    Debug.LogWarning("[CombatActionSelectorBridge] No InputActionAsset could be resolved. Assign one or ensure InputSystem_Actions is loaded.");
                    _warnedInputAssetMissing = true;
                }
                return;
            }

            _combatActionMap = ResolveCombatActionMap(inputActions);
            _playerActionMap = inputActions.FindActionMap(playerActionMapName, false);
            if (_combatActionMap == null)
            {
                // The initially resolved asset can be the wrong one (e.g. UI-only global actions).
                // Retry by explicitly requiring combat bindings before giving up.
                if (TryResolveInputAsset(requireCombatBindings: true))
                {
                    _combatActionMap = ResolveCombatActionMap(inputActions);
                    _playerActionMap = inputActions != null ? inputActions.FindActionMap(playerActionMapName, false) : null;
                }

                if (_combatActionMap == null)
                {
                    if (!_warnedCombatMapMissing)
                    {
                        Debug.LogWarning($"[CombatActionSelectorBridge] Missing UI action map in InputActionAsset '{inputActions.name}'. Expected '{combatActionMapName}'.");
                        _warnedCombatMapMissing = true;
                    }
                    return;
                }
            }

            _navigateAction = FindAction(_combatActionMap, navigateActionName);
            _confirmAction = FindAction(_combatActionMap, confirmActionName);
            _cancelAction = FindAction(_combatActionMap, cancelActionName);
            _infoAction = FindAction(_combatActionMap, infoActionName);

            if ((_navigateAction == null || _confirmAction == null || _cancelAction == null) && !TryResolveInputAsset(requireCombatBindings: true))
            {
                if (!_warnedCombatMapMissing)
                {
                    Debug.LogWarning($"[CombatActionSelectorBridge] Input asset '{inputActions.name}' is missing required combat UI actions (Navigate/Submit/Cancel).");
                    _warnedCombatMapMissing = true;
                }
                return;
            }

            if (_navigateAction == null || _confirmAction == null || _cancelAction == null)
            {
                // Re-resolve after selecting a compatible asset.
                _combatActionMap = ResolveCombatActionMap(inputActions);
                _playerActionMap = inputActions != null ? inputActions.FindActionMap(playerActionMapName, false) : null;
                _navigateAction = FindAction(_combatActionMap, navigateActionName);
                _confirmAction = FindAction(_combatActionMap, confirmActionName);
                _cancelAction = FindAction(_combatActionMap, cancelActionName);
                _infoAction = FindAction(_combatActionMap, infoActionName);
            }

            if (verboseLogs && inputActions != null && _combatActionMap != null)
            {
                Debug.Log($"[CombatActionSelectorBridge] Using input asset '{inputActions.name}' map '{_combatActionMap.name}' (navigate='{_navigateAction?.name}', confirm='{_confirmAction?.name}', cancel='{_cancelAction?.name}', info='{_infoAction?.name}').");
                LogInputState("ResolveInputActions");
            }
        }

        private void SubscribeBattleEvents()
        {
            if (_battleEventsSubscribed || battleController == null)
                return;

            battleController.OnPlayerTurnWaiting += HandlePlayerTurnWaiting;
            battleController.OnBattleEnded += HandleBattleEnded;
            _battleEventsSubscribed = true;
            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorBridge] Subscribed to battle events on '{battleController.name}'.");
        }

        private void UnsubscribeBattleEvents()
        {
            if (!_battleEventsSubscribed || battleController == null)
                return;

            battleController.OnPlayerTurnWaiting -= HandlePlayerTurnWaiting;
            battleController.OnBattleEnded -= HandleBattleEnded;
            _battleEventsSubscribed = false;
            if (verboseLogs)
                Debug.Log("[CombatActionSelectorBridge] Unsubscribed from battle events.");
        }

        private void TryResolveSelector()
        {
            if (actionSelector == null)
                actionSelector = FindObjectOfType<CombatActionSelectorController>();
        }

        private void TryResolveSelectionCoordinator()
        {
            if (selectionCoordinator != null)
                return;

            if (battleController != null)
                selectionCoordinator = battleController.GetComponent<CombatSelectionCoordinatorUGUI>();
            if (selectionCoordinator == null)
                selectionCoordinator = FindObjectOfType<CombatSelectionCoordinatorUGUI>(true);
        }

        private void TryResolveCaptureCoordinator()
        {
            if (captureCoordinator != null)
                return;
            if (battleController != null)
                captureCoordinator = battleController.GetComponent<CaptureCommandCoordinatorUGUI>();
            if (captureCoordinator == null)
                captureCoordinator = FindObjectOfType<CaptureCommandCoordinatorUGUI>(true);
            if (captureCoordinator == null && battleController != null)
                captureCoordinator = battleController.gameObject.AddComponent<CaptureCommandCoordinatorUGUI>();
        }

        private void TryResolveSelectionFeedbackTargets()
        {
            if (hudController == null)
                hudController = FindObjectOfType<CombatBattleUGUIController>(true);
            if (battlefieldPresenter == null)
                battlefieldPresenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);
        }

        private void SubscribeSelectorEvents()
        {
            TryResolveSelector();
            if (_selectorEventsSubscribed || actionSelector == null)
                return;

            actionSelector.OnOptionConfirmed += HandleSelectorConfirmed;
            _selectorEventsSubscribed = true;
            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorBridge] Subscribed selector events on '{actionSelector.name}'.");
        }

        private void UnsubscribeSelectorEvents()
        {
            if (!_selectorEventsSubscribed || actionSelector == null)
                return;

            actionSelector.OnOptionConfirmed -= HandleSelectorConfirmed;
            _selectorEventsSubscribed = false;
            if (verboseLogs)
                Debug.Log("[CombatActionSelectorBridge] Unsubscribed selector events.");
        }

        private void HandlePlayerTurnWaiting(CombatState state, int teamIndex)
        {
            if (battleController == null || teamIndex != battleController.LocalControlledTeamIndex || state == null)
                return;
            if (battleController != null && battleController.CapturePreviewMode)
                return;

            if (verboseLogs)
                Debug.Log("[CombatActionSelectorBridge] OnPlayerTurnWaiting received for team 0.");

            if (_combatActionMap == null)
                ResolveInputActions();

            _pendingState = state;
            _pendingSlots.Clear();
            _queuedActionsBySlot.Clear();
            _reservedBenchIndices.Clear();
            _pendingSlotCursor = -1;

            var playerTeam = battleController.GetControlledTeam(state);
            if (playerTeam == null || playerTeam.activeIndices == null || playerTeam.activeIndices.Count == 0)
            {
                SubmitFallbackPass();
                return;
            }

            for (int slot = 0; slot < playerTeam.activeIndices.Count; slot++)
            {
                var active = playerTeam.GetActiveInSlot(slot);
                if (active != null && active.isAlive)
                    _pendingSlots.Add(slot);
            }

            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorBridge] Pending selectable slots: {_pendingSlots.Count}.");

            if (_pendingSlots.Count == 0)
            {
                SubmitFallbackPass();
                return;
            }

            _awaitingSelection = true;
            _pendingSlotCursor = 0;
            SubscribeSelectorEvents();
            BeginSelectionForCurrentSlot();
        }

        private void HandleBattleEnded(BattleResult result)
        {
            CancelSelectionSession(clearQueuedActions: true);
        }

        private void TryResumePendingPlayerTurn()
        {
            if (_awaitingSelection || battleController == null)
                return;

            if (!battleController.UsePlayerInput || !battleController.IsWaitingForPlayerActions)
                return;
            if (battleController.CapturePreviewMode)
                return;

            if (battleController.State == null || !battleController.State.isActive)
                return;

            if (verboseLogs)
            {
                Debug.Log("[CombatActionSelectorBridge] Resuming pending player turn selection after late bridge/installer wiring.");
            }

            HandlePlayerTurnWaiting(battleController.State, 0);
        }

        private void BeginSelectionForCurrentSlot()
        {
            if (!_awaitingSelection || _pendingState == null)
                return;

            if (selectionCoordinator != null && selectionCoordinator.IsSelecting)
                selectionCoordinator.AbortSelection();
            if (captureCoordinator != null && captureCoordinator.IsSelecting)
                captureCoordinator.AbortSelection();

            if (_pendingSlotCursor < 0 || _pendingSlotCursor >= _pendingSlots.Count)
            {
                SubmitQueuedActions();
                return;
            }

            var actor = GetCurrentActor();
            if (actor == null)
            {
                QueueActionForCurrentSlot(CombatAction.Pass(ControlledTeamIndex));
                AdvanceToNextPendingSlot();
                return;
            }

            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorBridge] Begin slot {_pendingSlots[_pendingSlotCursor]} selection for '{actor.displayName}' (HP {actor.currentHP}/{actor.EffectiveMaxHP}, AP {actor.currentAP}/{CombatConstants.AP_CAP}).");

            HashSet<int> reservedSwapTargets = GetReservedBenchIndicesExcludingCurrentSlot();
            // Fight stays available even when every move is unaffordable: Move Select always
            // includes Rest, so the player can see locked moves and choose to rest for AP.
            bool canFight = actor.isAlive;
            bool canSwap = TryFindBenchSwapIndex(actor, ControlledTeam(), reservedSwapTargets, out _);

            if (_combatActionMap == null || _confirmAction == null)
            {
                ResolveInputActions();
            }

            if (_combatActionMap == null || _confirmAction == null)
            {
                Debug.LogWarning("[CombatActionSelectorBridge] UI input map/actions missing; using fallback action.");
                QueueActionForCurrentSlot(BuildFallbackActionForCurrentSlot());
                AdvanceToNextPendingSlot();
                return;
            }

            TryResolveSelector();
            SubscribeSelectorEvents();

            if (actionSelector == null)
            {
                QueueActionForCurrentSlot(BuildFallbackActionForCurrentSlot());
                AdvanceToNextPendingSlot();
                return;
            }

            SetWalletSelectionActiveSlot(GetCurrentSlotIndex());

            var available = new bool[CombatActionSelectorController.OPTION_COUNT];
            available[(int)CombatActionSelectorController.SelectorOption.Fight] = canFight;
            available[(int)CombatActionSelectorController.SelectorOption.Swap] = canSwap;
            available[(int)CombatActionSelectorController.SelectorOption.Item] = true;
            available[(int)CombatActionSelectorController.SelectorOption.Flee] = battleController != null
                && battleController.CanPlayerFlee;
            TryResolveCaptureCoordinator();
            bool captureAlreadyQueued = HasQueuedCapture();
            available[(int)CombatActionSelectorController.SelectorOption.Record] = !captureAlreadyQueued &&
                canFight &&
                captureCoordinator != null &&
                captureCoordinator.CanCapture(_pendingState, battleController != null ? battleController.CurrentEncounter : null, battleController != null && (battleController.IsPvP || battleController.IsCasualPvpSession));
            actionSelector.SetAvailableOptions(available);
            actionSelector.Show();
            SetInputSelectionState(selecting: true);
        }

        private void HandleSelectorConfirmed(int optionIndex)
        {
            if (!_awaitingSelection)
                return;

            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorBridge] Selector confirmed option {(CombatActionSelectorController.SelectorOption)optionIndex} ({optionIndex}).");

            switch ((CombatActionSelectorController.SelectorOption)optionIndex)
            {
                case CombatActionSelectorController.SelectorOption.Fight:
                {
                    TryResolveSelectionCoordinator();
                    var actor = GetCurrentActor();
                    int slotIndex = _pendingSlotCursor >= 0 && _pendingSlotCursor < _pendingSlots.Count
                        ? _pendingSlots[_pendingSlotCursor]
                        : 0;

                    int projectedGiftAp = _eligibilityService.ResolveCommittedFasterGiftAp(
                        ControlledTeam(),
                        actor,
                        slotIndex,
                        _queuedActionsBySlot);
                    bool opened = selectionCoordinator != null
                        && selectionCoordinator.BeginMoveSelection(
                            _pendingState,
                            0,
                            slotIndex,
                            actor,
                            HandlePanelActionCommitted,
                            HandlePanelSelectionCanceled,
                            projectedGiftAp,
                            _queuedActionsBySlot);

                    if (!opened)
                    {
                        if (!TryBuildFightAction(out var fightAction))
                            fightAction = CombatAction.Pass(ControlledTeamIndex);
                        QueueActionForCurrentSlot(fightAction);
                        AdvanceToNextPendingSlot();
                    }
                    else if (actionSelector != null)
                    {
                        actionSelector.SetSingleCombatTiltLiftActive(true);
                        actionSelector.Hide();
                    }
                    break;
                }
                case CombatActionSelectorController.SelectorOption.Swap:
                {
                    TryResolveSelectionCoordinator();
                    var actor = GetCurrentActor();
                    int slotIndex = _pendingSlotCursor >= 0 && _pendingSlotCursor < _pendingSlots.Count
                        ? _pendingSlots[_pendingSlotCursor]
                        : 0;

                    bool opened = selectionCoordinator != null
                        && selectionCoordinator.BeginSwapSelection(
                            _pendingState,
                            0,
                            slotIndex,
                            actor,
                            HandlePanelActionCommitted,
                            HandlePanelSelectionCanceled,
                            GetReservedBenchIndicesExcludingCurrentSlot());

                    if (!opened)
                    {
                        if (!TryBuildSwapAction(out var swapAction))
                            swapAction = CombatAction.Pass(ControlledTeamIndex);
                        QueueActionForCurrentSlot(swapAction);
                        AdvanceToNextPendingSlot();
                    }
                    else if (actionSelector != null)
                    {
                        actionSelector.SetSingleCombatTiltLiftActive(true);
                        actionSelector.Hide();
                    }
                    break;
                }
                case CombatActionSelectorController.SelectorOption.Item:
                    Debug.Log("[CombatActionSelectorBridge] Battle Item is not implemented yet.");
                    break;
                case CombatActionSelectorController.SelectorOption.Record:
                {
                    bool opened = BeginCaptureTargetSelection();
                    if (opened)
                    {
                        actionSelector?.SetSingleCombatTiltLiftActive(true);
                        actionSelector?.Hide();
                        ClearWalletSelectionActiveSlot();
                    }
                    else
                    {
                        Debug.Log("[CombatActionSelectorBridge] Capture is unavailable for this target or encounter.");
                    }
                    break;
                }
                case CombatActionSelectorController.SelectorOption.Flee:
                {
                    FleeRequestResult fleeResult = battleController != null
                        ? battleController.RequestFlee(FleeRequestSource.ActionSelector)
                        : new FleeRequestResult(
                            FleeRequestStatus.BattleInactive,
                            EncounterFleePolicy.UseDifficultyDefault,
                            "There is no active battle to flee.");

                    if (fleeResult.Accepted)
                    {
                        CancelSelectionSession(clearQueuedActions: true);
                    }
                    else
                    {
                        Debug.Log($"[CombatActionSelectorBridge] Flee unavailable: {fleeResult.Message}");
                    }
                    break;
                }
            }
        }

        private void AdvanceToNextPendingSlot()
        {
            _pendingSlotCursor++;
            if (_pendingSlotCursor >= _pendingSlots.Count)
            {
                SubmitQueuedActions();
                return;
            }

            BeginSelectionForCurrentSlot();
        }

        private void HandlePanelActionCommitted(CombatAction action)
        {
            if (!_awaitingSelection)
                return;

            actionSelector?.SetSingleCombatTiltLiftActive(false);
            QueueActionForCurrentSlot(action ?? CombatAction.Pass(ControlledTeamIndex));
            AdvanceToNextPendingSlot();
        }

        private void HandlePanelSelectionCanceled()
        {
            if (!_awaitingSelection)
                return;

            actionSelector?.SetSingleCombatTiltLiftActive(false);
            if (TryRewindToPreviousPendingSlot())
                return;

            SetWalletSelectionActiveSlot(GetCurrentSlotIndex());
            if (actionSelector != null)
                actionSelector.Show();
        }

        private void HandleCaptureQueued(CombatAction action)
        {
            if (!_awaitingSelection)
                return;

            actionSelector?.SetSingleCombatTiltLiftActive(false);
            QueueActionForCurrentSlot(action ?? CombatAction.Pass(ControlledTeamIndex));
            AdvanceToNextPendingSlot();
        }

        private bool BeginCaptureTargetSelection()
        {
            TryResolveCaptureCoordinator();
            TryResolveSelectionCoordinator();
            if (captureCoordinator == null || selectionCoordinator == null || _pendingState == null)
                return false;

            EncounterSnapshot currentEncounter = battleController != null ? battleController.CurrentEncounter : null;
            bool isPvp = battleController != null && (battleController.IsPvP || battleController.IsCasualPvpSession);
            IReadOnlyList<string> eligible = captureCoordinator.GetEligibleTargetInstanceIds(
                _pendingState,
                currentEncounter,
                isPvp);
            if (eligible == null || eligible.Count == 0)
                return false;

            return selectionCoordinator.BeginTargetSelection(
                _pendingState,
                0,
                GetCurrentSlotIndex(),
                GetCurrentActor(),
                eligible,
                HandleCaptureTargetConfirmed,
                HandleCaptureTargetSelectionCanceled);
        }

        private void HandleCaptureTargetConfirmed(string targetInstanceId)
        {
            if (!_awaitingSelection)
                return;

            bool opened = captureCoordinator != null && captureCoordinator.BeginOrbSelection(
                _pendingState,
                battleController != null ? battleController.CurrentEncounter : null,
                battleController != null && battleController.IsPvP,
                targetInstanceId,
                HandleCaptureQueued,
                HandleCaptureSelectionCanceled);
            if (!opened)
                HandleCaptureTargetSelectionCanceled();
        }

        private void HandleCaptureTargetSelectionCanceled()
        {
            if (!_awaitingSelection)
                return;
            actionSelector?.SetSingleCombatTiltLiftActive(false);
            SetWalletSelectionActiveSlot(GetCurrentSlotIndex());
            actionSelector?.Show();
        }

        private void HandleCaptureSelectionCanceled()
        {
            if (!_awaitingSelection)
                return;
            if (!BeginCaptureTargetSelection())
                HandleCaptureTargetSelectionCanceled();
        }

        private void HandleWalletBack()
        {
            if (!_awaitingSelection)
                return;

            if (actionSelector != null)
                actionSelector.HandleCancel();

            if (TryRewindToPreviousPendingSlot())
                return;

            SetWalletSelectionActiveSlot(GetCurrentSlotIndex());
        }

        private bool TryRewindToPreviousPendingSlot()
        {
            if (!RewindPendingSlotCursorForBack())
                return false;

            BeginSelectionForCurrentSlot();
            return true;
        }

        private bool RewindPendingSlotCursorForBack()
        {
            if (!_awaitingSelection || _pendingSlotCursor <= 0)
                return false;

            _pendingSlotCursor--;
            RemoveQueuedActionForSlot(GetCurrentSlotIndex());
            return true;
        }

        private void SubmitQueuedActions()
        {
            if (!_awaitingSelection || battleController == null)
                return;

            int activeSlotCount = _pendingState != null &&
                ControlledTeam() != null &&
                ControlledTeam().activeIndices != null
                ? ControlledTeam().activeIndices.Count
                : 0;

            var outgoing = new List<CombatAction>(activeSlotCount > 0 ? activeSlotCount : 1);
            for (int slot = 0; slot < activeSlotCount; slot++)
            {
                var actor = ControlledTeam().GetActiveInSlot(slot);
                if (actor == null || !actor.isAlive)
                {
                    outgoing.Add(CombatAction.Pass(ControlledTeamIndex));
                    continue;
                }

                if (_queuedActionsBySlot.TryGetValue(slot, out CombatAction queued))
                    outgoing.Add(queued ?? CombatAction.Pass(ControlledTeamIndex));
                else
                    outgoing.Add(CombatAction.Pass(ControlledTeamIndex));
            }

            if (outgoing.Count == 0)
                outgoing.Add(CombatAction.Pass(ControlledTeamIndex));

            if (verboseLogs)
                Debug.Log($"[CombatActionSelectorBridge] Submitting {outgoing.Count} queued player action(s).");
            CancelSelectionSession(clearQueuedActions: true);
            battleController.SubmitPlayerActions(outgoing);
        }

        private void SubmitFallbackPass()
        {
            if (battleController == null)
                return;

            int activeSlotCount = _pendingState != null &&
                ControlledTeam() != null &&
                ControlledTeam().activeIndices != null
                ? ControlledTeam().activeIndices.Count
                : 0;

            var fallback = new List<CombatAction>(activeSlotCount > 0 ? activeSlotCount : 1);
            if (activeSlotCount <= 0)
            {
                fallback.Add(CombatAction.Pass(ControlledTeamIndex));
            }
            else
            {
                for (int slot = 0; slot < activeSlotCount; slot++)
                    fallback.Add(CombatAction.Pass(ControlledTeamIndex));
            }

            battleController.SubmitPlayerActions(fallback);
        }

        private void CancelSelectionSession(bool clearQueuedActions)
        {
            _awaitingSelection = false;
            _pendingSlotCursor = -1;
            _pendingSlots.Clear();
            _pendingState = null;
            if (clearQueuedActions)
            {
                _queuedActionsBySlot.Clear();
                _reservedBenchIndices.Clear();
            }

            if (selectionCoordinator != null && selectionCoordinator.IsSelecting)
                selectionCoordinator.AbortSelection();

            if (actionSelector != null)
            {
                actionSelector.SetSingleCombatTiltLiftActive(false);
                actionSelector.Hide();
            }
            ClearWalletSelectionActiveSlot();
            SetInputSelectionState(selecting: false);
        }

        private void SetWalletSelectionActiveSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return;

            TryResolveSelectionFeedbackTargets();
            hudController?.SetSelectionActiveSlot(0, slotIndex);
            battlefieldPresenter?.SetSelectingActorSlot(0, slotIndex);
        }

        private void ClearWalletSelectionActiveSlot()
        {
            TryResolveSelectionFeedbackTargets();
            hudController?.ClearSelectionActiveSlot();
            battlefieldPresenter?.ClearSelectingActorSlot();
        }

        private void SetInputSelectionState(bool selecting)
        {
            if (_combatActionMap != null)
            {
                if (selecting) _combatActionMap.Enable();
                else _combatActionMap.Disable();
            }

            if (_playerActionMap != null && disablePlayerMapWhileSelecting)
            {
                if (selecting) _playerActionMap.Disable();
                else _playerActionMap.Enable();
            }

            if (verboseLogs)
                LogInputState(selecting ? "SetInputSelectionState(selecting=true)" : "SetInputSelectionState(selecting=false)");
        }

        private int ControlledTeamIndex => battleController != null ? battleController.LocalControlledTeamIndex : 0;

        private TeamState ControlledTeam()
        {
            if (battleController == null)
                return null;
            return battleController.GetControlledTeam(_pendingState);
        }

        private ChainlingInstance GetCurrentActor()
        {
            if (_pendingState == null || ControlledTeam() == null)
                return null;
            if (_pendingSlotCursor < 0 || _pendingSlotCursor >= _pendingSlots.Count)
                return null;

            int slot = _pendingSlots[_pendingSlotCursor];
            return ControlledTeam().GetActiveInSlot(slot);
        }

        private CombatAction BuildFallbackActionForCurrentSlot()
        {
            if (TryBuildFightAction(out var fightAction))
                return fightAction;
            if (TryBuildSwapAction(out var swapAction))
                return swapAction;
            return CombatAction.Pass(ControlledTeamIndex);
        }

        private bool TryBuildFightAction(out CombatAction action)
        {
            action = CombatAction.Pass(ControlledTeamIndex);
            var actor = GetCurrentActor();
            if (!TryFindUsableMoveIndex(actor, out int moveIndex))
                return false;

            action = CombatAction.UseMove(ControlledTeamIndex, moveIndex);
            MoveInstance move = actor.moves[moveIndex];
            if (CombatWallStateUtility.MovePlacesBlock(move))
            {
                TeamState team = ControlledTeam();
                for (int targetSlot = 0; team?.activeIndices != null && targetSlot < team.activeIndices.Count; targetSlot++)
                {
                    ChainlingInstance target = team.GetActiveInSlot(targetSlot);
                    if (!_eligibilityService.EvaluateBlockTarget(
                            actor, move, target, _pendingState, _queuedActionsBySlot, GetCurrentSlotIndex()).canTarget)
                        continue;
                    action = CombatAction.UseMove(ControlledTeamIndex, moveIndex, ControlledTeamIndex, targetSlot);
                    return true;
                }
                return false;
            }
            return true;
        }

        private bool TryBuildSwapAction(out CombatAction action)
        {
            action = CombatAction.Pass(ControlledTeamIndex);
            HashSet<int> reservedSwapTargets = GetReservedBenchIndicesExcludingCurrentSlot();
            if (!TryFindBenchSwapIndex(GetCurrentActor(), _pendingState != null ? ControlledTeam() : null, reservedSwapTargets, out int benchIndex))
                return false;

            action = CombatAction.Swap(ControlledTeamIndex, benchIndex);
            return true;
        }

        private bool TryFindUsableMoveIndex(ChainlingInstance actor, out int moveIndex)
        {
            moveIndex = -1;
            if (actor == null || actor.moves == null)
                return false;

            for (int i = 0; i < actor.moves.Count; i++)
            {
                var move = actor.moves[i];
                int projectedGiftAp = _eligibilityService.ResolveCommittedFasterGiftAp(
                    ControlledTeam(),
                    actor,
                    GetCurrentSlotIndex(),
                    _queuedActionsBySlot);
                MoveSelectionEvaluation evaluation = _eligibilityService.EvaluateMoveSelection(
                    actor,
                    move,
                    _pendingState,
                    extraAvailableAp: projectedGiftAp,
                    queuedBySlot: _queuedActionsBySlot,
                    actorSlot: GetCurrentSlotIndex());
                if (!evaluation.canSelect)
                    continue;

                moveIndex = i;
                return true;
            }

            return false;
        }

        private bool TryFindBenchSwapIndex(ChainlingInstance actor, TeamState team, ISet<int> reservedBenchIndices, out int benchIndex)
        {
            benchIndex = -1;
            if (team == null)
                return false;

            List<int> available = _eligibilityService.GetAvailableSwapTargets(team, actor, reservedBenchIndices);
            if (available.Count == 0)
                return false;

            benchIndex = available[0];
            return true;
        }

        private void QueueActionForCurrentSlot(CombatAction action)
        {
            int slot = GetCurrentSlotIndex();
            if (slot < 0)
                return;

            RemoveQueuedActionForSlot(slot);

            CombatAction queued = action ?? CombatAction.Pass(ControlledTeamIndex);
            _queuedActionsBySlot[slot] = queued;
            if (queued.actionType == CombatActionType.Swap)
                _reservedBenchIndices.Add(queued.swapTargetIndex);
            ReconcileQueuedBlockTargets(slot, queued);
        }

        private void ReconcileQueuedBlockTargets(int preferredSlot, CombatAction preferredAction)
        {
            TeamState team = ControlledTeam();
            ChainlingInstance actor = team?.GetActiveInSlot(preferredSlot);
            if (actor?.moves == null || preferredAction == null || preferredAction.actionType != CombatActionType.UseMove ||
                preferredAction.moveIndex < 0 || preferredAction.moveIndex >= actor.moves.Count ||
                !CombatWallStateUtility.MovePlacesBlock(actor.moves[preferredAction.moveIndex]) ||
                preferredAction.targetTeamIndex != ControlledTeamIndex || preferredAction.targetSlotIndex < 0)
            {
                return;
            }

            var conflicts = new List<int>();
            foreach (KeyValuePair<int, CombatAction> pair in _queuedActionsBySlot)
            {
                if (pair.Key == preferredSlot || pair.Value == null || pair.Value.actionType != CombatActionType.UseMove)
                    continue;
                ChainlingInstance otherActor = team.GetActiveInSlot(pair.Key);
                if (otherActor?.moves == null || pair.Value.moveIndex < 0 || pair.Value.moveIndex >= otherActor.moves.Count)
                    continue;
                if (!CombatWallStateUtility.MovePlacesBlock(otherActor.moves[pair.Value.moveIndex]))
                    continue;
                if (pair.Value.targetTeamIndex == preferredAction.targetTeamIndex &&
                    pair.Value.targetSlotIndex == preferredAction.targetSlotIndex)
                {
                    conflicts.Add(pair.Key);
                }
            }

            for (int i = 0; i < conflicts.Count; i++)
                RemoveQueuedActionForSlot(conflicts[i]);
        }

        private bool HasQueuedCapture()
        {
            foreach (KeyValuePair<int, CombatAction> pair in _queuedActionsBySlot)
            {
                if (pair.Value != null && pair.Value.actionType == CombatActionType.Capture)
                    return true;
            }

            return false;
        }

        private void RemoveQueuedActionForSlot(int slot)
        {
            if (!_queuedActionsBySlot.TryGetValue(slot, out CombatAction existing) || existing == null)
                return;

            if (existing.actionType == CombatActionType.Swap)
                _reservedBenchIndices.Remove(existing.swapTargetIndex);

            _queuedActionsBySlot.Remove(slot);
        }

        private int GetCurrentSlotIndex()
        {
            if (_pendingSlotCursor < 0 || _pendingSlotCursor >= _pendingSlots.Count)
                return -1;
            return _pendingSlots[_pendingSlotCursor];
        }

        private HashSet<int> GetReservedBenchIndicesExcludingCurrentSlot()
        {
            var reserved = new HashSet<int>(_reservedBenchIndices);
            int slot = GetCurrentSlotIndex();
            if (slot >= 0 &&
                _queuedActionsBySlot.TryGetValue(slot, out CombatAction queuedForSlot) &&
                queuedForSlot != null &&
                queuedForSlot.actionType == CombatActionType.Swap)
            {
                reserved.Remove(queuedForSlot.swapTargetIndex);
            }
            return reserved;
        }

        private void WarnIfManualInputDisabled()
        {
            if (_warnedUsePlayerInputDisabled || battleController == null)
                return;

            if (!battleController.UsePlayerInput)
            {
                Debug.LogWarning("[CombatActionSelectorBridge] CombatBattleController.usePlayerInput is false. 3D selector input will not be requested.");
                _warnedUsePlayerInputDisabled = true;
            }
        }

        private bool TryResolveInputAsset(bool requireCombatBindings = false)
        {
            if (inputActions != null && (!requireCombatBindings || HasCombatBindings(inputActions)))
                return true;

            var globalActions = InputSystem.actions;
            if (globalActions != null && (!requireCombatBindings || HasCombatBindings(globalActions)))
            {
                inputActions = globalActions;
                return true;
            }

            var loadedAssets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
            InputActionAsset firstCompatible = null;
            InputActionAsset namedInputSystemActions = null;
            for (int i = 0; i < loadedAssets.Length; i++)
            {
                var candidate = loadedAssets[i];
                if (candidate == null)
                    continue;

                bool compatible = HasCombatBindings(candidate);
                if (candidate.name == "InputSystem_Actions")
                {
                    namedInputSystemActions = candidate;
                    if (!requireCombatBindings || compatible)
                    {
                        inputActions = candidate;
                        return true;
                    }
                }

                if (firstCompatible == null && compatible)
                    firstCompatible = candidate;
            }

            var playerInput = FindObjectOfType<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                if (!requireCombatBindings || HasCombatBindings(playerInput.actions))
                {
                    inputActions = playerInput.actions;
                    return true;
                }
            }

            if (TryFindInputAssetFromBehaviours(out var fromBehaviours))
            {
                if (!requireCombatBindings || HasCombatBindings(fromBehaviours))
                {
                    inputActions = fromBehaviours;
                    return true;
                }
            }

            if (firstCompatible != null)
            {
                inputActions = firstCompatible;
                return true;
            }

            if (namedInputSystemActions != null)
            {
                inputActions = namedInputSystemActions;
                return !requireCombatBindings || HasCombatBindings(inputActions);
            }

            if (globalActions != null)
            {
                inputActions = globalActions;
                return !requireCombatBindings || HasCombatBindings(inputActions);
            }

            // Conventional fallback used across this project.
            inputActions = Resources.Load<InputActionAsset>("InputSystem_Actions");
            if (inputActions != null)
                return !requireCombatBindings || HasCombatBindings(inputActions);

            if (playerInput != null && playerInput.actions != null && !requireCombatBindings)
            {
                inputActions = playerInput.actions;
                return true;
            }

            return false;
        }

        private bool TryFindInputAssetFromBehaviours(out InputActionAsset found)
        {
            found = null;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var behaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || behaviour == this)
                    continue;

                var fields = behaviour.GetType().GetFields(flags);
                for (int f = 0; f < fields.Length; f++)
                {
                    var field = fields[f];
                    if (field.FieldType != typeof(InputActionAsset))
                        continue;

                    var candidate = field.GetValue(behaviour) as InputActionAsset;
                    if (candidate == null)
                        continue;

                    found = candidate;
                    return true;
                }
            }

            return false;
        }

        private InputActionMap ResolveCombatActionMap(InputActionAsset asset)
        {
            if (asset == null)
                return null;

            var map = asset.FindActionMap(combatActionMapName, false);
            if (map == null)
                map = asset.FindActionMap("UI", false);
            return map;
        }

        private static InputAction FindAction(InputActionMap map, params string[] actionNames)
        {
            if (map == null || actionNames == null)
                return null;

            for (int i = 0; i < actionNames.Length; i++)
            {
                var action = map.FindAction(actionNames[i], false);
                if (action != null)
                    return action;
            }

            return null;
        }

        private bool HasCombatBindings(InputActionAsset asset)
        {
            var map = ResolveCombatActionMap(asset);
            if (map == null)
                return false;

            var navigate = FindAction(map, navigateActionName);
            var confirm = FindAction(map, confirmActionName);
            var cancel = FindAction(map, cancelActionName);
            return navigate != null && confirm != null && cancel != null;
        }

        private void LogInputState(string context)
        {
            if (!verboseLogs)
                return;

            string assetName = inputActions != null ? inputActions.name : "<null>";
            string combatMap = _combatActionMap != null
                ? $"{_combatActionMap.name} (enabled={_combatActionMap.enabled})"
                : "<null>";
            string playerMap = _playerActionMap != null
                ? $"{_playerActionMap.name} (enabled={_playerActionMap.enabled})"
                : "<null>";

            string playerInputMap = "<none>";
            var playerInput = FindObjectOfType<PlayerInput>();
            if (playerInput != null)
                playerInputMap = playerInput.currentActionMap != null ? playerInput.currentActionMap.name : "<null>";

            bool panelSelecting = selectionCoordinator != null && selectionCoordinator.IsSelecting;
            Debug.Log($"[CombatActionSelectorBridge] [{context}] asset='{assetName}', combatMap={combatMap}, playerMap={playerMap}, playerInput.currentMap='{playerInputMap}', awaitingSelection={_awaitingSelection}, panelSelecting={panelSelecting}.");
        }
    }
}
