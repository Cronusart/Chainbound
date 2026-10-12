using Unity.Netcode;
using UnityEngine;

namespace Chainbound.Hub
{
    /// <summary>
    /// Keeps a local walker in the hub until a session is up, then hides it for the networked copy.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubLobbyDirector : MonoBehaviour
    {
        public static HubLobbyDirector Instance { get; private set; }

        [SerializeField] private GameObject avatarPrefab;
        [SerializeField] private GameObject networkPrefab;
        [SerializeField] private Transform spawnPoint;

        private GameObject soloAvatar;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            HubSessionService.Ensure(networkPrefab);
            ShowSolo();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public static void ShowSolo()
        {
            if (Instance != null)
                Instance.ShowSoloInstance();
        }

        public static void HideSolo()
        {
            if (Instance != null && Instance.soloAvatar != null)
                Instance.soloAvatar.SetActive(false);
        }

        public void Configure(GameObject prefab, Transform spawn)
        {
            avatarPrefab = prefab;
            spawnPoint = spawn;
        }

        private void ShowSoloInstance()
        {
            if (soloAvatar != null)
            {
                soloAvatar.SetActive(true);
                Place(soloAvatar.transform);
                return;
            }

            if (avatarPrefab == null)
                return;

            Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
            Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            soloAvatar = Instantiate(avatarPrefab, position, rotation);
            soloAvatar.name = "HubSoloAvatar";
            DisableNetworking(soloAvatar);
        }

        private void Place(Transform avatar)
        {
            if (spawnPoint == null)
                return;

            avatar.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }

        private static void DisableNetworking(GameObject avatar)
        {
            NetworkObject networkObject = avatar.GetComponent<NetworkObject>();
            if (networkObject != null)
                networkObject.enabled = false;

            NetworkBehaviour[] behaviours = avatar.GetComponents<NetworkBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;
        }
    }
}
