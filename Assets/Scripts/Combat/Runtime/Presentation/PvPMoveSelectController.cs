using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;

namespace Chainbound.Combat
{
    /// <summary>
    /// UI controller for the PvP move selection screen.
    /// After a PvP wager match, the winner selects a move from the opponent's
    /// escrowed pool (shown as anonymized face-down cards).
    ///
    /// Features:
    ///   - Navigable card grid (Dpad / joystick)
    ///   - Countdown timer with auto-select fallback on expiry
    ///   - Winner perspective: select and claim
    ///   - Loser perspective: "Opponent is selecting..." + wait for settlement
    ///   - Fires <see cref="OnSelectionComplete"/> when done
    ///
    /// Integrates with <see cref="WinnerSelectionAPI"/> and <see cref="WagerMatchOrchestrator"/>.
    /// </summary>
    public class PvPMoveSelectController : MonoBehaviour
    {
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Inspector
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("References")]
        [Tooltip("UIDocument that loads MoveSelect.uxml.")]
        [SerializeField] private UIDocument selectDocument;

        [Header("Input")]
        [Tooltip("Same InputActionAsset used by the rest of the combat UI.")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Timer")]
        [Tooltip("Seconds allowed for the winner to select. Auto-selects index 0 on expiry.")]
        [SerializeField] private float selectionTimeoutSeconds = 30f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Events
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Fired when the selection process is complete (claimed or auto-selected).
        /// Null move means the loser's perspective completed or an error occurred.
        /// </summary>
        public event Action<MoveInstance> OnSelectionComplete;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Runtime State
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private enum SelectState
        {
            Idle,
            WinnerSelecting,
            LoserWaiting,
            ShowingResult,
        }

        private SelectState _state = SelectState.Idle;
        private string _matchId;
        private bool _isWinner;
        private IReadOnlyList<AnonymizedMove> _cards;
        private int _selectedIndex;
        private float _timer;
        private bool _selectionSubmitted;
        private MoveInstance _claimedMove;

        // External services (injected via SetServices or found in scene)
        private WinnerSelectionAPI _selectionAPI;
        private WagerMatchOrchestrator _orchestrator;

        // Input
        private InputActionMap _combatUIMap;
        private InputAction _navigateAction;
        private InputAction _confirmAction;

        // Nav throttle
        private float _navCooldown;
        private const float NAV_REPEAT_DELAY = 0.2f;

        // UITK elements
        private VisualElement _root;
        private VisualElement _timerBarFill;
        private Label _timerText;
        private Label _header;
        private VisualElement _cardGrid;
        private Label _footer;
        private VisualElement _selectionResult;
        private Label _resultTitle;
        private Label _resultBody;
        private Label _resultContinue;

        // Card element references
        private readonly List<VisualElement> _cardElements = new List<VisualElement>();

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Public API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Inject the wager services. Call before <see cref="BeginSelection"/>.
        /// </summary>
        public void SetServices(WinnerSelectionAPI selectionAPI, WagerMatchOrchestrator orchestrator)
        {
            _selectionAPI = selectionAPI;
            _orchestrator = orchestrator;
        }

        /// <summary>
        /// Start the move selection UI for the given match.
        /// </summary>
        /// <param name="matchId">Active PvP match ID.</param>
        /// <param name="localPlayerIsWinner">True if the local player is the winner (gets to select).</param>
        public void BeginSelection(string matchId, bool localPlayerIsWinner)
        {
            _matchId = matchId;
            _isWinner = localPlayerIsWinner;
            _timer = selectionTimeoutSeconds;
            _selectedIndex = 0;
            _selectionSubmitted = false;
            _claimedMove = null;

            CacheElements();
            SetupInput();

            if (_isWinner)
            {
                StartWinnerFlow();
            }
            else
            {
                StartLoserFlow();
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Unity Lifecycle
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void OnDisable()
        {
            if (_orchestrator != null)
                _orchestrator.OnStateChanged -= HandleOrchestratorStateChanged;
        }

        private void Update()
        {
            if (_state == SelectState.Idle)
                return;

            if (_state == SelectState.WinnerSelecting)
            {
                UpdateTimer();
                PollInput();
            }
            else if (_state == SelectState.ShowingResult)
            {
                // Wait for confirm to dismiss
                if (_confirmAction != null && _confirmAction.WasPressedThisFrame())
                {
                    FinishAndClose();
                }
            }
            // LoserWaiting: no input needed, just waiting for orchestrator callback
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Setup
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void CacheElements()
        {
            if (selectDocument == null)
                return;

            var docRoot = selectDocument.rootVisualElement;
            if (docRoot == null)
                return;

            _root = docRoot.Q<VisualElement>("MoveSelectRoot");
            _timerBarFill = docRoot.Q<VisualElement>("TimerBarFill");
            _timerText = docRoot.Q<Label>("TimerText");
            _header = docRoot.Q<Label>("MoveHeader");
            _cardGrid = docRoot.Q<VisualElement>("CardGrid");
            _footer = docRoot.Q<Label>("MoveFooter");
            _selectionResult = docRoot.Q<VisualElement>("SelectionResult");
            _resultTitle = docRoot.Q<Label>("ResultTitle");
            _resultBody = docRoot.Q<Label>("ResultBody");
            _resultContinue = docRoot.Q<Label>("ResultContinue");
        }

        private void SetupInput()
        {
            if (inputActions == null)
                return;

            _combatUIMap = inputActions.FindActionMap("UI");
            if (_combatUIMap != null)
            {
                _navigateAction = _combatUIMap.FindAction("Navigate");
                _confirmAction = _combatUIMap.FindAction("Submit");
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Winner Flow
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void StartWinnerFlow()
        {
            _state = SelectState.WinnerSelecting;

            // Populate cards from WinnerSelectionAPI
            if (_selectionAPI != null && !string.IsNullOrEmpty(_matchId))
            {
                try
                {
                    _cards = _selectionAPI.GetOpponentEscrowedMoves(_matchId);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PvPMoveSelect] Failed to get escrowed moves: {ex.Message}");
                    _cards = null;
                }
            }

            ShowOverlay();

            if (_header != null)
                _header.text = "SELECT YOUR REWARD";

            if (_footer != null)
                _footer.text = "Press Confirm to claim";

            PopulateCardGrid();
            UpdateSelection();
            _combatUIMap?.Enable();

            Debug.Log($"[PvPMoveSelect] Winner selection started. {(_cards != null ? _cards.Count : 0)} cards available.");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Loser Flow
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void StartLoserFlow()
        {
            _state = SelectState.LoserWaiting;

            ShowOverlay();

            if (_header != null)
                _header.text = "OPPONENT IS SELECTING...";

            if (_footer != null)
                _footer.text = "Please wait";

            // Show empty card grid (no interaction)
            _cardGrid?.Clear();
            _cardElements.Clear();

            // Listen for orchestrator settlement
            if (_orchestrator != null)
                _orchestrator.OnStateChanged += HandleOrchestratorStateChanged;

            _combatUIMap?.Disable(); // No input for loser

            Debug.Log("[PvPMoveSelect] Loser waiting for opponent selection.");
        }

        private void HandleOrchestratorStateChanged(WagerMatchState newState)
        {
            if (newState == WagerMatchState.Settled)
            {
                if (_orchestrator != null)
                    _orchestrator.OnStateChanged -= HandleOrchestratorStateChanged;

                ShowResult("MOVE TAKEN", "Your opponent has selected a move from your collection.", true);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Card Grid
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void PopulateCardGrid()
        {
            if (_cardGrid == null)
                return;

            _cardGrid.Clear();
            _cardElements.Clear();

            if (_cards == null || _cards.Count == 0)
            {
                var placeholder = new Label("No moves available");
                placeholder.style.color = new StyleColor(new Color(0.56f, 0.64f, 0.70f));
                placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
                _cardGrid.Add(placeholder);
                return;
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                var card = new VisualElement();
                card.AddToClassList("move-card");

                // Card icon (face-down: question mark)
                var icon = new Label("?");
                icon.AddToClassList("move-card__icon");
                card.Add(icon);

                // Card label (index only â€” anonymized)
                var label = new Label($"Card {i + 1}");
                label.AddToClassList("move-card__label");
                card.Add(label);

                _cardGrid.Add(card);
                _cardElements.Add(card);
            }
        }

        private void UpdateSelection()
        {
            for (int i = 0; i < _cardElements.Count; i++)
            {
                if (i == _selectedIndex)
                    _cardElements[i].AddToClassList("move-card--selected");
                else
                    _cardElements[i].RemoveFromClassList("move-card--selected");
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Timer
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void UpdateTimer()
        {
            _timer -= Time.unscaledDeltaTime;

            if (_timer <= 0f)
            {
                _timer = 0f;
                // Auto-select index 0 on timeout
                if (!_selectionSubmitted)
                {
                    Debug.Log("[PvPMoveSelect] Timer expired â€” auto-selecting index 0.");
                    SubmitSelection(0);
                }
            }

            // Update UI
            float pct = Mathf.Clamp01(_timer / selectionTimeoutSeconds);
            if (_timerBarFill != null)
                _timerBarFill.style.width = Length.Percent(pct * 100f);

            if (_timerText != null)
                _timerText.text = $"{Mathf.CeilToInt(_timer)}s";
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Input Handling
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void PollInput()
        {
            if (_combatUIMap == null || !_combatUIMap.enabled)
                return;

            // Navigate
            Vector2 nav = _navigateAction != null ? _navigateAction.ReadValue<Vector2>() : Vector2.zero;
            if (nav.sqrMagnitude > 0.1f)
            {
                _navCooldown -= Time.unscaledDeltaTime;
                if (_navCooldown <= 0f)
                {
                    HandleNavigate(nav);
                    _navCooldown = NAV_REPEAT_DELAY;
                }
            }
            else
            {
                _navCooldown = 0f;
            }

            // Confirm
            if (_confirmAction != null && _confirmAction.WasPressedThisFrame())
            {
                if (!_selectionSubmitted && _cards != null && _cards.Count > 0)
                {
                    SubmitSelection(_selectedIndex);
                }
            }
        }

        private void HandleNavigate(Vector2 nav)
        {
            if (_cards == null || _cards.Count <= 1)
                return;

            int columns = Mathf.Max(1, Mathf.FloorToInt(Mathf.Sqrt(_cards.Count)));

            // Horizontal
            if (Mathf.Abs(nav.x) > Mathf.Abs(nav.y))
            {
                if (nav.x > 0.3f)
                    _selectedIndex = Mathf.Min(_selectedIndex + 1, _cards.Count - 1);
                else if (nav.x < -0.3f)
                    _selectedIndex = Mathf.Max(_selectedIndex - 1, 0);
            }
            else
            {
                // Vertical
                if (nav.y < -0.3f) // Down
                    _selectedIndex = Mathf.Min(_selectedIndex + columns, _cards.Count - 1);
                else if (nav.y > 0.3f) // Up
                    _selectedIndex = Mathf.Max(_selectedIndex - columns, 0);
            }

            UpdateSelection();
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Selection Submission
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private async void SubmitSelection(int index)
        {
            if (_selectionSubmitted)
                return;

            _selectionSubmitted = true;
            _selectedIndex = index;

            // Highlight the selected card
            UpdateSelection();
            if (index >= 0 && index < _cardElements.Count)
                _cardElements[index].AddToClassList("move-card--claimed");

            if (_footer != null)
                _footer.text = "Claiming...";

            Debug.Log($"[PvPMoveSelect] Submitting selection: index {index}");

            // Call the orchestrator to settle
            if (_orchestrator != null)
            {
                try
                {
                    _claimedMove = await _orchestrator.SelectAndSettleAsync(index);
                    string name = _claimedMove != null ? _claimedMove.displayName : "Unknown";
                    ShowResult("MOVE CLAIMED!", $"You claimed: {name}", false);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PvPMoveSelect] Settlement failed: {ex.Message}");
                    ShowResult("ERROR", $"Settlement failed: {ex.Message}", false);
                }
            }
            else
            {
                // No orchestrator â€” stub behavior
                Debug.LogWarning("[PvPMoveSelect] No WagerMatchOrchestrator assigned. Completing with null.");
                ShowResult("MOVE CLAIMED!", "(Stub â€” no orchestrator)", false);
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Result Display
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void ShowResult(string title, string body, bool isLoserView)
        {
            _state = SelectState.ShowingResult;

            if (_selectionResult != null)
            {
                _selectionResult.RemoveFromClassList("selection-result--hidden");
                _selectionResult.AddToClassList("selection-result--visible");
            }

            if (_resultTitle != null)
                _resultTitle.text = title;

            if (_resultBody != null)
                _resultBody.text = body;

            // Enable input for dismiss
            _combatUIMap?.Enable();

            Debug.Log($"[PvPMoveSelect] Showing result: {title}");
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        // Cleanup
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void FinishAndClose()
        {
            HideOverlay();
            _state = SelectState.Idle;
            _combatUIMap?.Disable();

            Debug.Log("[PvPMoveSelect] Selection complete. Closing.");
            OnSelectionComplete?.Invoke(_claimedMove);
        }

        private void ShowOverlay()
        {
            if (_root == null)
                return;

            _root.RemoveFromClassList("move-select--hidden");
            _root.AddToClassList("move-select--visible");

            // Hide result panel
            if (_selectionResult != null)
            {
                _selectionResult.RemoveFromClassList("selection-result--visible");
                _selectionResult.AddToClassList("selection-result--hidden");
            }
        }

        private void HideOverlay()
        {
            if (_root == null)
                return;

            _root.RemoveFromClassList("move-select--visible");
            _root.AddToClassList("move-select--hidden");
        }
    }
}
