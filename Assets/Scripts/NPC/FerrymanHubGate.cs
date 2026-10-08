using TMPro;
using UnityEngine;

namespace Chainbound.Hub
{
    /// <summary>
    /// Dock gate. Release builds and a locked server stay ashore. A development
    /// build with multiplayer unlocked stamps the profile and loads the hub.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FerrymanHubGate : MonoBehaviour
    {
        public const string HubSceneName = "SocialLobby";

        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string dockedLine = "The ferry is not sailing yet.";

        private bool traveling;

        public void Talk()
        {
            if (!traveling)
                _ = TravelAsync();
        }

        private async System.Threading.Tasks.Task TravelAsync()
        {
            if (!HubBuild.Enabled)
            {
                ShowLine(dockedLine);
                return;
            }

            traveling = true;
            HubConfig config = await HubConfigClient.RefreshAsync();
            if (this == null)
                return;
            if (config == null || !config.Success || !config.MultiplayerUnlocked)
            {
                traveling = false;
                ShowLine(dockedLine);
                return;
            }

            bool unlocked = await HubUnlockClient.UnlockAsync();
            traveling = false;
            if (this == null)
                return;
            if (!unlocked)
            {
                ShowLine(dockedLine);
                return;
            }

            if (SceneLoader.Instance == null)
            {
                Debug.LogWarning("[Hub] SceneLoader is missing. Start through Bootstrap.");
                return;
            }

            SceneLoader.Instance.RequestTransition(new SceneTransitionRequest(
                HubSceneName,
                sourceId: "Ferryman"));
        }

        private void ShowLine(string line)
        {
            if (statusLabel != null)
                statusLabel.text = line;
            Debug.Log("[Hub] " + line);
        }
    }
}
