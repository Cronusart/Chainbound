using TMPro;
using UnityEngine;

namespace Chainbound.Hub
{
    /// <summary>
    /// Dock gate. The Loomville ferry stamps a development profile and loads the hub.
    /// The SocialLobby ferry sails back to Loomville without that gate.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FerrymanHubGate : MonoBehaviour
    {
        public const string HubSceneName = "SocialLobby";
        public const string OverworldSceneName = "Loomville";

        [SerializeField] private string destinationScene = HubSceneName;
        [SerializeField] private bool requireUnlock = true;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private string dockedLine = "The ferry is not sailing yet.";

        bool traveling;

        public void Talk()
        {
            if (!traveling)
                _ = TravelAsync();
        }

        async System.Threading.Tasks.Task TravelAsync()
        {
            if (!requireUnlock)
            {
                LoadDestination();
                return;
            }

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

            LoadDestination();
        }

        void LoadDestination()
        {
            if (string.IsNullOrWhiteSpace(destinationScene))
                destinationScene = HubSceneName;

            if (SceneLoader.Instance == null)
            {
                Debug.LogWarning("[Hub] SceneLoader is missing. Start through Bootstrap.");
                return;
            }

            SceneLoader.Instance.RequestTransition(new SceneTransitionRequest(
                destinationScene,
                sourceId: "Ferryman"));
        }

        void ShowLine(string line)
        {
            if (statusLabel != null)
                statusLabel.text = line;
            Debug.Log("[Hub] " + line);
        }
    }
}
