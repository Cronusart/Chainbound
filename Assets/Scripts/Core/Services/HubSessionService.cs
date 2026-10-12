using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Combat;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Chainbound.Hub
{
    /// <summary>
    /// One DontDestroyOnLoad Sessions client for the social hub.
    /// A failed sign-in, ticket, or kill switch stays in the solo hub.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubSessionService : MonoBehaviour
    {
        public const int MaxPlayers = 8;
        public const string SessionType = "social-hub";
        const int LeaveTimeoutMs = 3000;

        public static HubSessionService Instance { get; private set; }

        readonly Dictionary<ulong, string> approvedIds = new Dictionary<ulong, string>();

        ISession session;
        string presenceTicket = string.Empty;
        string localUgsId = string.Empty;
        int joinGeneration;
        bool joining;
        bool intentionalLeave;
        bool recovering;
        bool networkHooked;

        public static void Ensure(GameObject networkPrefab)
        {
            if (Instance != null)
                return;

            if (networkPrefab == null)
            {
                Debug.LogWarning("[Hub] HubNetwork prefab is missing.");
                return;
            }

            GameObject created = Instantiate(networkPrefab);
            created.name = "HubNetwork";
            DontDestroyOnLoad(created);
        }

        public static bool TryGetApprovedUgsId(ulong clientId, out string ugsId)
        {
            ugsId = null;
            return Instance != null && Instance.approvedIds.TryGetValue(clientId, out ugsId) && !string.IsNullOrEmpty(ugsId);
        }

        public static Task LeaveBeforeBattleAsync()
        {
            return Instance != null ? Instance.LeaveAsync() : Task.CompletedTask;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureNetwork();
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void Start()
        {
            ConfigureNetwork();
            if (IsHubScene(SceneManager.GetActiveScene()))
                Begin();
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            UnhookNetwork();
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

        void OnApplicationPause(bool paused)
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

        void OnApplicationQuit()
        {
            _ = LeaveAsync();
        }

        public void Begin()
        {
            if (!HubBuild.Enabled)
                return;

            if (session != null)
            {
                _ = LeaveIfLockedAsync();
                return;
            }

            if (!joining)
                _ = QuickJoinAsync();
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!IsHubScene(scene))
            {
                if ((session != null || joining) && !intentionalLeave)
                    _ = LeaveAsync();
                return;
            }

            ConfigureNetwork();
            Begin();
        }

        async Task LeaveIfLockedAsync()
        {
            HubConfig config = await HubConfigClient.RefreshAsync();
            if (config == null || !config.Success)
            {
                Debug.Log("[Hub] hub config unavailable, staying solo");
                await LeaveAsync();
                return;
            }

            if (!config.MultiplayerUnlocked)
            {
                Debug.Log("[Hub] multiplayer is locked, staying solo");
                await LeaveAsync();
                return;
            }
        }

        async Task QuickJoinAsync()
        {
            if (!HubBuild.Enabled || joining || session != null)
                return;

            joining = true;
            int generation = joinGeneration;
            try
            {
                HubConfig config = await HubConfigClient.RefreshAsync();
                if (generation != joinGeneration)
                    return;
                if (config == null || !config.Success)
                {
                    Debug.Log("[Hub] hub config unavailable, staying solo");
                    DropToSolo();
                    return;
                }

                if (!config.MultiplayerUnlocked)
                {
                    Debug.Log("[Hub] multiplayer is locked, staying solo");
                    DropToSolo();
                    return;
                }

                if (UnityServices.State == ServicesInitializationState.Uninitialized)
                {
                    var optionsInit = new InitializationOptions();
                    if (HubBuild.Enabled)
                        optionsInit.SetEnvironmentName("development");
                    await UnityServices.InitializeAsync(optionsInit);
                }

                if (generation != joinGeneration)
                    return;
                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                if (generation != joinGeneration)
                    return;

                HubLinkResult link = await HubUnlockClient.LinkIdentityAsync(AuthenticationService.Instance.PlayerId);
                if (generation != joinGeneration)
                    return;
                if (link == null || !link.Success || string.IsNullOrEmpty(link.Ticket))
                {
                    Debug.LogWarning("[Hub] presence ticket missing, staying solo");
                    DropToSolo();
                    return;
                }

                presenceTicket = link.Ticket;
                localUgsId = AuthenticationService.Instance.PlayerId;
                ConfigureNetwork();
                NetworkManager manager = NetworkManager.Singleton;
                if (manager != null && manager.NetworkConfig != null)
                    manager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(presenceTicket);

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
                if (generation != joinGeneration)
                {
                    await AbandonSession(joined);
                    return;
                }

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

        async Task LeaveAsync()
        {
            intentionalLeave = true;
            joinGeneration++;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(LeaveTimeoutMs);
            try
            {
                while (joining && DateTime.UtcNow < deadline)
                    await Task.Delay(50);

                ISession current = session;
                Unhook(current);
                session = null;
                approvedIds.Clear();
                if (current != null)
                {
                    Task leave = current.LeaveAsync();
                    int remaining = (int)Math.Max(0, (deadline - DateTime.UtcNow).TotalMilliseconds);
                    Task finished = await Task.WhenAny(leave, Task.Delay(remaining));
                    if (finished != leave)
                        Debug.LogWarning("[Hub] leave timed out");
                    else
                        await Observe(leave);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] leave failed: " + ex.Message);
            }

            ShutdownNet();
            HubLobbyDirector.ShowSolo();
            intentionalLeave = false;
        }

        void Hook(ISession joined)
        {
            joined.Deleted += HandleSessionLost;
            joined.RemovedFromSession += HandleSessionLost;
        }

        void Unhook(ISession joined)
        {
            if (joined == null)
                return;

            joined.Deleted -= HandleSessionLost;
            joined.RemovedFromSession -= HandleSessionLost;
        }

        void HandleSessionLost()
        {
            if (intentionalLeave || recovering)
                return;

            recovering = true;
            joinGeneration++;
            Unhook(session);
            ISession dropped = session;
            session = null;
            approvedIds.Clear();
            ShutdownNet();
            HubLobbyDirector.ShowSolo();
            if (dropped != null)
                _ = AbandonSession(dropped);
            recovering = false;
            if (HubBuild.Enabled && IsHubScene(SceneManager.GetActiveScene()))
                Begin();
        }

        void HandleClientStopped(bool _)
        {
            if (joining)
                return;

            HandleSessionLost();
        }

        void HandleTransportFailure()
        {
            Debug.LogWarning("[Hub] transport failed, rejoining");
            HandleSessionLost();
        }

        void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                ApproveHost(request, response);
                return;
            }

            response.Pending = true;
            _ = ApproveRemoteAsync(request, response);
        }

        void ApproveHost(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string ticket = DecodeTicket(request.Payload);
            bool matches = ticket.Length > 0 && string.Equals(ticket, presenceTicket, StringComparison.Ordinal);
            response.Approved = matches;
            response.CreatePlayerObject = matches;
            response.Reason = matches ? string.Empty : "ticket_rejected";
            ApplySpawn(response);
            if (matches)
                approvedIds[request.ClientNetworkId] = localUgsId;
        }

        async Task ApproveRemoteAsync(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            string ugsId = null;
            try
            {
                ugsId = await HubUnlockClient.VerifyTicketAsync(DecodeTicket(request.Payload));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] ticket verify failed: " + ex.Message);
            }

            bool approved = !string.IsNullOrEmpty(ugsId);
            if (approved)
                approvedIds[request.ClientNetworkId] = ugsId;
            response.Approved = approved;
            response.CreatePlayerObject = approved;
            response.Reason = approved ? string.Empty : "ticket_rejected";
            ApplySpawn(response);
            response.Pending = false;
        }

        static void ApplySpawn(NetworkManager.ConnectionApprovalResponse response)
        {
            GameObject spawn = GameObject.Find("HubSpawn");
            if (spawn == null)
                return;

            response.Position = spawn.transform.position;
            response.Rotation = spawn.transform.rotation;
        }

        static string DecodeTicket(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return string.Empty;

            return Encoding.UTF8.GetString(payload);
        }

        static async Task AbandonSession(ISession dropped)
        {
            if (dropped == null)
                return;

            try
            {
                Task leave = dropped.LeaveAsync();
                Task finished = await Task.WhenAny(leave, Task.Delay(LeaveTimeoutMs));
                if (finished == leave)
                    await Observe(leave);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] leave failed: " + ex.Message);
            }
        }

        static async Task Observe(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Hub] leave failed: " + ex.Message);
            }
        }

        static void DropToSolo()
        {
            ShutdownNet();
            HubLobbyDirector.ShowSolo();
        }

        static void ShutdownNet()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening)
                manager.Shutdown();
        }

        void ConfigureNetwork()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || manager.NetworkConfig == null)
                return;

            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.TickRate = 10;
            manager.NetworkConfig.ConnectionApproval = true;
            if (!networkHooked)
            {
                manager.OnClientStopped += HandleClientStopped;
                manager.OnTransportFailure += HandleTransportFailure;
                manager.ConnectionApprovalCallback = ApproveConnection;
                networkHooked = true;
            }
        }

        void UnhookNetwork()
        {
            if (!networkHooked)
                return;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                manager.OnClientStopped -= HandleClientStopped;
                manager.OnTransportFailure -= HandleTransportFailure;
                if (manager.ConnectionApprovalCallback == ApproveConnection)
                    manager.ConnectionApprovalCallback = null;
            }

            networkHooked = false;
        }

        static bool IsHubScene(Scene scene)
        {
            return scene.IsValid() && string.Equals(scene.name, FerrymanHubGate.HubSceneName, StringComparison.Ordinal);
        }
    }
}
