using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;

namespace Chainbound.Hub
{
    /// <summary>
    /// Opens the existing casual queue screen. It does not use the player-menu tab.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColosseumInteractable : MonoBehaviour
    {
        [SerializeField] private ColosseumQueueScreen queueScreenPrefab;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string closedLine = "The Colosseum is closed.";

        private ColosseumQueueScreen screen;
        private bool opening;

        public void OpenQueue()
        {
            if (!opening)
                _ = OpenAsync();
        }

        private async System.Threading.Tasks.Task OpenAsync()
        {
            opening = true;
            HubConfig config = await HubConfigClient.RefreshAsync();
            opening = false;
            if (this == null)
                return;

            if (config == null || !config.Success || !config.MultiplayerUnlocked)
            {
                ShowLine(closedLine);
                return;
            }

            ShowQueue();
        }

        private void ShowQueue()
        {
            if (screen == null)
                screen = FindFirstObjectByType<ColosseumQueueScreen>(FindObjectsInactive.Include);
            if (screen == null && queueScreenPrefab != null)
            {
                Canvas canvas = FindFirstObjectByType<Canvas>();
                screen = canvas != null
                    ? Instantiate(queueScreenPrefab, canvas.transform)
                    : Instantiate(queueScreenPrefab);
                screen.name = "ColosseumQueueScreen";
            }

            if (screen == null)
            {
                Debug.LogWarning("[Hub] Colosseum queue screen prefab is missing.");
                return;
            }

            screen.Show();
        }

        private void ShowLine(string line)
        {
            if (statusLabel != null)
                statusLabel.text = line;
            Debug.Log("[Hub] " + line);
        }
    }
}
