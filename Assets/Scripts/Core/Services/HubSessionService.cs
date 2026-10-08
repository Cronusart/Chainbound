using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chainbound.Hub
{
    /// <summary>
    /// Sessions quick-join for the social hub. A failed sign-in or network stays in the solo hub.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubSessionService : MonoBehaviour
    {
        public const int MaxPlayers = 8;
        public const string SessionType = "social-hub";

        public static HubSessionService Instance { get; private set; }

        private ISession session;
        private bool joining;
        private bool intentionalLeave;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ConfigureNetwork();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            if (IsHubScene(SceneManager.GetActiveScene()))
                Begin();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            ISession current = session;
            Unhook(current);
            session = null;
            if (current == null)
                return;

            try
            {
                _ = current.LeaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] leave failed: " + ex.Message);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (!HubBuild.Enabled)
                return;

            if (paused)
            {
                _ = LeaveAsync();
                return;
            }

            if (IsHubScene(SceneManager.GetActiveScene()))
                Begin();
        }

        private void OnApplicationQuit()
        {
            _ = LeaveAsync();
        }

        public static Task LeaveBeforeBattleAsync()
        {
            return Instance != null ? Instance.LeaveAsync() : Task.CompletedTask;
        }

        public void Begin()
        {
            if (!HubBuild.Enabled || joining || session != null)
                return;

            _ = QuickJoinAsync();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsHubScene(scene) || !HubBuild.Enabled)
                return;

            ConfigureNetwork();
            Begin();
        }

        private async Task QuickJoinAsync()
        {
            if (!HubBuild.Enabled || joining || session != null)
                return;

            joining = true;
            try
            {
                await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                await HubUnlockClient.LinkIdentityAsync(AuthenticationService.Instance.PlayerId);
                ConfigureNetwork();

                string version = string.IsNullOrEmpty(Application.version) ? "0" : Application.version;
                var options = new SessionOptions
                {
                    MaxPlayers = MaxPlayers,
                    Name = SessionType,
                    Type = SessionType,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        ["build"] = new SessionProperty(PvpBuildStamp.Current, VisibilityPropertyOptions.Public, PropertyIndex.String1),
                        ["version"] = new SessionProperty(version, VisibilityPropertyOptions.Public, PropertyIndex.String2),
                    },
                }.WithRelayNetwork();

                var quickJoin = new QuickJoinOptions
                {
                    CreateSession = true,
                    Timeout = TimeSpan.FromSeconds(8),
                    Filters = new List<FilterOption>
                    {
                        new(FilterField.StringIndex1, PvpBuildStamp.Current, FilterOperation.Equal),
                        new(FilterField.StringIndex2, version, FilterOperation.Equal),
                        new(FilterField.AvailableSlots, "1", FilterOperation.GreaterOrEqual),
                    },
                };

                ISession joined = await MultiplayerService.Instance.MatchmakeSessionAsync(quickJoin, options);
                Hook(joined);
                session = joined;
                HubLobbyDirector.HideSolo();
                Debug.Log("[Hub] joined shard " + joined.Id + " players=" + joined.PlayerCount);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] session failed, staying solo: " + ex.Message);
                DropToSolo();
            }
            finally
            {
                joining = false;
            }
        }

        private async Task LeaveAsync()
        {
            intentionalLeave = true;
            ISession current = session;
            Unhook(current);
            session = null;
            try
            {
                if (current != null)
                    await current.LeaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] leave failed: " + ex.Message);
            }

            ShutdownNet();
            HubLobbyDirector.ShowSolo();
            intentionalLeave = false;
        }

        private void Hook(ISession joined)
        {
            joined.Deleted += HandleSessionLost;
            joined.RemovedFromSession += HandleSessionLost;
        }

        private void Unhook(ISession joined)
        {
            if (joined == null)
                return;

            joined.Deleted -= HandleSessionLost;
            joined.RemovedFromSession -= HandleSessionLost;
        }

        private void HandleSessionLost()
        {
            Unhook(session);
            session = null;
            ShutdownNet();
            HubLobbyDirector.ShowSolo();
            if (!intentionalLeave && HubBuild.Enabled && IsHubScene(SceneManager.GetActiveScene()))
                Begin();
        }

        private static void DropToSolo()
        {
            ShutdownNet();
            HubLobbyDirector.ShowSolo();
        }

        private static void ShutdownNet()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
                manager.Shutdown();
        }

        private static void ConfigureNetwork()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.NetworkConfig == null)
                return;

            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.TickRate = 10;
        }

        private static bool IsHubScene(Scene scene)
        {
            return scene.IsValid() && string.Equals(scene.name, FerrymanHubGate.HubSceneName, StringComparison.Ordinal);
        }
    }
}
