using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Casual queue tab. Cancels a waiting row when the screen closes.
    /// </summary>
    public sealed class ColosseumQueueScreen : MenuScreen
    {
        public const string ColosseumScreenId = "colosseum";

        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Button queueButton;
        [SerializeField] private Button cancelButton;

        private SupabaseMatchmakingService matchmaking;
        private CancellationTokenSource queueCancel;
        private bool searching;

        protected override void Awake()
        {
            if (string.IsNullOrWhiteSpace(ScreenId))
                SetScreenIdentity(ColosseumScreenId, "Colosseum", GetComponent<CanvasGroup>());
            EnsureLayout();
            base.Awake();
        }

        public override void Show()
        {
            base.Show();
            BindService();
            ApplyCasualGate();
            if (MatchmakingConfigService.Instance != null)
                _ = RefreshCasualGate();
        }

        public override void Hide()
        {
            if (searching)
                _ = CancelQueue();
            base.Hide();
        }

        private void OnDestroy()
        {
            if (queueCancel != null)
            {
                queueCancel.Cancel();
                queueCancel.Dispose();
                queueCancel = null;
            }
        }

        private void EnsureLayout()
        {
            if (statusLabel != null && queueButton != null && cancelButton != null)
                return;

            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            var panel = PlayerMenuUiFactory.CreateRect("ColosseumBody", root);
            PlayerMenuUiFactory.Stretch(panel, 48f, 48f, 48f, 48f);
            PlayerMenuUiFactory.VerticalLayout(panel.gameObject, 16f);

            if (statusLabel == null)
            {
                TMP_Text label = PlayerMenuUiFactory.CreateText(
                    "Status",
                    panel,
                    "Queue a casual battle. No wager.",
                    28f,
                    TextAlignmentOptions.TopLeft);
                var layout = label.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 120f;
                statusLabel = label;
            }

            if (queueButton == null)
            {
                queueButton = PlayerMenuUiFactory.CreateButton("QueueButton", panel, "Find Match", PlayerMenuUiFactory.Accent);
                var layout = queueButton.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 72f;
                queueButton.onClick.AddListener(() => _ = Queue());
            }

            if (cancelButton == null)
            {
                cancelButton = PlayerMenuUiFactory.CreateButton("CancelButton", panel, "Cancel", PlayerMenuUiFactory.PanelAlt);
                var layout = cancelButton.gameObject.AddComponent<LayoutElement>();
                layout.preferredHeight = 72f;
                cancelButton.onClick.AddListener(() => _ = CancelQueue());
            }
        }

        private async Task Queue()
        {
            BindService();
            if (matchmaking == null)
            {
                SetStatus("Matchmaking is not available.");
                return;
            }

            if (searching)
                return;
            if (!CasualQueueOpen())
            {
                SetStatus("Casual battles are turned off.");
                return;
            }

            searching = true;
            SetStatus("Searching for a match.");
            queueCancel = new CancellationTokenSource();
            string profileId = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession.PlayerProfileId
                : string.Empty;
            MatchmakingEnqueueResult result = await matchmaking.EnqueueAsync(new MatchQueueRequest
            {
                ProfileId = profileId,
                QueueType = "casual",
                AppVersion = Application.version,
            }, queueCancel.Token);

            searching = matchmaking.IsSearching;
            if (result != null && result.Accepted && !result.Waiting)
                SetStatus("Match found.");
            else if (result != null && !string.IsNullOrEmpty(result.Error) && result.Error != "cancelled")
                SetStatus(Readable(result.Error));
            else if (!searching)
                SetStatus("Queue a casual battle. No wager.");
        }

        private async Task CancelQueue()
        {
            searching = false;
            if (queueCancel != null)
            {
                queueCancel.Cancel();
                queueCancel.Dispose();
                queueCancel = null;
            }

            if (matchmaking != null)
                await matchmaking.CancelAsync(CancellationToken.None);
            SetStatus("Queue cancelled.");
        }

        private async Task RefreshCasualGate()
        {
            if (MatchmakingConfigService.Instance == null)
                return;
            await MatchmakingConfigService.Instance.RefreshAsync();
            if (this == null)
                return;
            ApplyCasualGate();
        }

        private void ApplyCasualGate()
        {
            bool casualOpen = CasualQueueOpen();
            if (queueButton != null)
                queueButton.gameObject.SetActive(casualOpen);
            if (!casualOpen)
                SetStatus("Casual battles are turned off.");
            else if (!searching)
                SetStatus("Queue a casual battle. No wager.");
        }

        private static bool CasualQueueOpen()
        {
            if (MatchmakingConfigService.Instance != null && !MatchmakingConfigService.Instance.ServerCasualEnabled)
                return false;
            MatchmakingConfigSO board = BalanceConfigRuntime.Matchmaking;
            return board == null || board.casualEnabled;
        }

        private void BindService()
        {
            if (matchmaking == null)
                matchmaking = SupabaseMatchmakingService.Instance != null
                    ? SupabaseMatchmakingService.Instance
                    : FindFirstObjectByType<SupabaseMatchmakingService>(FindObjectsInactive.Include);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
                statusLabel.text = message;
        }

        private static string Readable(string error)
        {
            switch (error)
            {
                case "config_mismatch":
                    return "Game version does not match the server. Update and try again.";
                case "server_unreachable":
                    return "The match server cannot be reached.";
                case "casual_disabled":
                    return "Casual battles are turned off.";
                case "empty_party":
                    return "Equip a party before queueing.";
                case "wallet_required":
                    return "Connect a wallet before queueing.";
                case "match_in_progress":
                    return "You already have a match in progress.";
                case "search_expired":
                    return "Search expired.";
                case "enqueue_cooldown":
                    return "Wait a moment before searching again.";
                case "party_fainted":
                    return "Your party has no chainlings that can battle.";
                case "party_too_large":
                    return "Casual battles allow three chainlings.";
                case "party_move_unknown":
                    return "A party move is not legal for casual battles.";
                default:
                    return error;
            }
        }
    }
}
