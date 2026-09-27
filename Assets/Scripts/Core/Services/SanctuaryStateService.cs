using System;
using System.Threading.Tasks;
using Chainbound.Combat;
using Newtonsoft.Json.Linq;
using UnityEngine;

public enum SanctuaryLatchKind
{
    Hub = 0,
    Portal = 1
}

internal static class SanctuaryResidentChunkIds
{
    public static string[] Copy(string[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<string>();

        var copy = new string[source.Length];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    public static bool SequenceEqual(string[] left, string[] right)
    {
        string[] a = left ?? Array.Empty<string>();
        string[] b = right ?? Array.Empty<string>();
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (!string.Equals(a[i] ?? "", b[i] ?? "", StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public static JArray ToJson(string[] ids)
    {
        var array = new JArray();
        if (ids == null)
            return array;
        for (int i = 0; i < ids.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(ids[i]))
                array.Add(ids[i].Trim());
        }

        return array;
    }

    public static string[] FromJson(JToken token, string legacySingleId)
    {
        var ids = new System.Collections.Generic.List<string>();
        if (token is JArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                string value = array[i]?.ToString()?.Trim() ?? "";
                if (string.IsNullOrEmpty(value))
                    continue;
                bool exists = false;
                for (int j = 0; j < ids.Count; j++)
                {
                    if (string.Equals(ids[j], value, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    ids.Add(value);
            }
        }

        return SanctuaryLatchZone.NormalizeResidentChunkSceneIds(
            ids.ToArray(),
            legacySingleId,
            "");
    }
}

[Serializable]
public sealed class SanctuaryLatchRecord
{
    public SanctuaryLatchKind kind;
    public string sceneId = "";
    public string spawnId = "";
    public string requiredFlagKey = "";
    public string overworldChunkSceneId = "";
    public string[] residentChunkSceneIds = Array.Empty<string>();

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(sceneId) && !string.IsNullOrWhiteSpace(spawnId);

    public SanctuaryLatchRecord Clone()
    {
        return new SanctuaryLatchRecord
        {
            kind = kind,
            sceneId = sceneId ?? "",
            spawnId = spawnId ?? "",
            requiredFlagKey = requiredFlagKey ?? "",
            overworldChunkSceneId = overworldChunkSceneId ?? "",
            residentChunkSceneIds = SanctuaryResidentChunkIds.Copy(residentChunkSceneIds)
        };
    }
}

[Serializable]
public sealed class SanctuaryPortalRecord
{
    public string sceneId = "";
    public string spawnId = "";
    public string requiredFlagKey = "";
    public string overworldChunkSceneId = "";
    public string[] residentChunkSceneIds = Array.Empty<string>();

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(sceneId) && !string.IsNullOrWhiteSpace(spawnId);

    public SanctuaryPortalRecord Clone()
    {
        return new SanctuaryPortalRecord
        {
            sceneId = sceneId ?? "",
            spawnId = spawnId ?? "",
            requiredFlagKey = requiredFlagKey ?? "",
            overworldChunkSceneId = overworldChunkSceneId ?? "",
            residentChunkSceneIds = SanctuaryResidentChunkIds.Copy(residentChunkSceneIds)
        };
    }
}

[Serializable]
public sealed class SanctuaryStateSnapshot
{
    public SanctuaryLatchRecord lastLatch;
    public SanctuaryPortalRecord lastPortal;

    public SanctuaryStateSnapshot Clone()
    {
        return new SanctuaryStateSnapshot
        {
            lastLatch = lastLatch != null ? lastLatch.Clone() : null,
            lastPortal = lastPortal != null ? lastPortal.Clone() : null
        };
    }
}

/// <summary>
/// Profile-scoped sanctuary latch. Continue uses lastLatch (hub or portal).
/// Party wipe uses lastPortal only. Local snapshot is immediate; remote writes
/// go through profile_set_sanctuary_latch because flag RPCs are boolean-only.
/// </summary>
public sealed class SanctuaryStateService : MonoBehaviour
{
    public const string DefaultContinueSceneId = "player_room";
    public const string DefaultContinueSpawnId = "player_room_start";
    public const string DefaultWipeSceneId = "player_room";
    public const string DefaultWipeSpawnId = "home_bed";

    private const string SnapshotPrefix = "SignalJam_SanctuaryState_v1_";
    private const string OutboxPrefix = "SignalJam_SanctuaryOutbox_v1_";

    [Serializable]
    private sealed class SanctuaryMutationQueue
    {
        public string mutationId = "";
        public string payloadJson = "";
    }

    [Serializable]
    private sealed class StatsRequest
    {
        public string player_profile_id = "";
        public string wallet_address = "";
        public string auth_user_id = "";
    }

    private static SanctuaryStateService instance;

    [Header("Backend")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    [Header("Diagnostics")]
    [SerializeField] private bool enableDebugLogs;

    private NarrativeProfileIdentity currentIdentity;
    private SanctuaryStateSnapshot snapshot = new SanctuaryStateSnapshot();
    private bool flushInProgress;
    private Task activeHydration;

    public static SanctuaryStateService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<SanctuaryStateService>();
                if (instance == null)
                {
                    GameServicesContainer services = GameServicesContainer.Instance;
                    var host = new GameObject("SanctuaryStateService");
                    if (services != null)
                        host.transform.SetParent(services.transform, false);
                    instance = host.AddComponent<SanctuaryStateService>();
                    if (services == null && Application.isPlaying)
                        DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    public bool IsHydrated { get; private set; }

    public SanctuaryLatchRecord LastLatch =>
        snapshot != null && snapshot.lastLatch != null ? snapshot.lastLatch.Clone() : null;

    public SanctuaryPortalRecord LastPortal =>
        snapshot != null && snapshot.lastPortal != null ? snapshot.lastPortal.Clone() : null;

    public bool HasLastLatch => snapshot != null && snapshot.lastLatch != null && snapshot.lastLatch.IsValid;

    public bool HasLastPortal => snapshot != null && snapshot.lastPortal != null && snapshot.lastPortal.IsValid;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent == null && Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        if (supabaseConfig == null)
            supabaseConfig = Resources.Load<SupabaseConfigSO>("SupabaseConfig");
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public Task HydrateFromCurrentSessionAsync(bool forceOffline = false)
    {
        GameSessionContext session = GameSessionContext.Instance;
        var identity = new NarrativeProfileIdentity
        {
            playerProfileId = session != null ? session.PlayerProfileId : "",
            walletAddress = session != null ? session.WalletAddress : "",
            authUserId = session != null ? session.AuthUserId : ""
        };
        return HydrateProfileAsync(identity, forceOffline);
    }

    public Task HydrateProfileAsync(NarrativeProfileIdentity identity, bool forceOffline = false)
    {
        string requestedIdentity = ResolveCacheIdentity(identity);
        if (activeHydration != null && !activeHydration.IsCompleted &&
            currentIdentity != null && ResolveCacheIdentity(currentIdentity) == requestedIdentity)
        {
            return activeHydration;
        }

        activeHydration = HydrateInternalAsync(identity ?? new NarrativeProfileIdentity(), forceOffline);
        return activeHydration;
    }

    public void GetContinueDestination(
        out string sceneId,
        out string spawnId,
        out SanctuaryLatchKind kind,
        out string[] residentChunkSceneIds)
    {
        if (HasLastLatch && IsLatchDestinationUnlocked(snapshot.lastLatch))
        {
            sceneId = snapshot.lastLatch.sceneId.Trim();
            spawnId = snapshot.lastLatch.spawnId.Trim();
            kind = snapshot.lastLatch.kind;
            residentChunkSceneIds = ResolveResidentChunkSceneIds(
                kind,
                sceneId,
                snapshot.lastLatch.residentChunkSceneIds,
                snapshot.lastLatch.overworldChunkSceneId);
            return;
        }

        sceneId = DefaultContinueSceneId;
        spawnId = DefaultContinueSpawnId;
        kind = SanctuaryLatchKind.Hub;
        residentChunkSceneIds = Array.Empty<string>();
    }

    public void GetWipeDestination(
        out string sceneId,
        out string spawnId,
        out bool usePortalPresentation,
        out string[] residentChunkSceneIds)
    {
        if (HasLastPortal && IsPortalDestinationUnlocked(snapshot.lastPortal))
        {
            sceneId = snapshot.lastPortal.sceneId.Trim();
            spawnId = snapshot.lastPortal.spawnId.Trim();
            usePortalPresentation = true;
            residentChunkSceneIds = ResolveResidentChunkSceneIds(
                SanctuaryLatchKind.Portal,
                sceneId,
                snapshot.lastPortal.residentChunkSceneIds,
                snapshot.lastPortal.overworldChunkSceneId);
            return;
        }

        sceneId = DefaultWipeSceneId;
        spawnId = DefaultWipeSpawnId;
        usePortalPresentation = false;
        residentChunkSceneIds = Array.Empty<string>();
    }

    public void LatchHub(string sceneId, string spawnId)
    {
        ApplyLatch(
            SanctuaryLatchKind.Hub,
            sceneId,
            spawnId,
            updatePortal: false,
            requiredFlagKey: "",
            residentChunkSceneIds: Array.Empty<string>());
    }

    public void LatchPortal(
        string sceneId,
        string spawnId,
        string requiredFlagKey = "",
        string[] residentChunkSceneIds = null)
    {
        ApplyLatch(
            SanctuaryLatchKind.Portal,
            sceneId,
            spawnId,
            updatePortal: true,
            requiredFlagKey,
            residentChunkSceneIds);
    }

    public bool MatchesLatch(SanctuaryLatchKind kind, string sceneId, string spawnId)
    {
        if (!HasLastLatch)
            return false;

        return snapshot.lastLatch.kind == kind &&
               string.Equals(snapshot.lastLatch.sceneId, sceneId?.Trim(), StringComparison.Ordinal) &&
               string.Equals(snapshot.lastLatch.spawnId, spawnId?.Trim(), StringComparison.Ordinal);
    }

    private void ApplyLatch(
        SanctuaryLatchKind kind,
        string sceneId,
        string spawnId,
        bool updatePortal,
        string requiredFlagKey,
        string[] residentChunkSceneIds)
    {
        string normalizedScene = sceneId != null ? sceneId.Trim() : "";
        string normalizedSpawn = spawnId != null ? spawnId.Trim() : "";
        string normalizedFlag = requiredFlagKey != null ? requiredFlagKey.Trim() : "";
        string[] normalizedChunks = SanctuaryLatchZone.NormalizeResidentChunkSceneIds(
            residentChunkSceneIds,
            "",
            kind == SanctuaryLatchKind.Portal ? normalizedScene : "");
        if (string.IsNullOrEmpty(normalizedScene) || string.IsNullOrEmpty(normalizedSpawn))
        {
            Debug.LogWarning("[SanctuaryStateService] Cannot latch an empty scene or spawn id.", this);
            return;
        }

        if (snapshot == null)
            snapshot = new SanctuaryStateSnapshot();

        bool latchChanged = !HasLastLatch ||
                            snapshot.lastLatch.kind != kind ||
                            !string.Equals(snapshot.lastLatch.sceneId, normalizedScene, StringComparison.Ordinal) ||
                            !string.Equals(snapshot.lastLatch.spawnId, normalizedSpawn, StringComparison.Ordinal) ||
                            !string.Equals(snapshot.lastLatch.requiredFlagKey ?? "", normalizedFlag, StringComparison.Ordinal) ||
                            !SanctuaryResidentChunkIds.SequenceEqual(
                                snapshot.lastLatch.residentChunkSceneIds,
                                normalizedChunks);

        snapshot.lastLatch = new SanctuaryLatchRecord
        {
            kind = kind,
            sceneId = normalizedScene,
            spawnId = normalizedSpawn,
            requiredFlagKey = normalizedFlag,
            overworldChunkSceneId = normalizedChunks.Length > 0 ? normalizedChunks[0] : "",
            residentChunkSceneIds = SanctuaryResidentChunkIds.Copy(normalizedChunks)
        };

        bool portalChanged = false;
        if (updatePortal)
        {
            portalChanged = !HasLastPortal ||
                            !string.Equals(snapshot.lastPortal.sceneId, normalizedScene, StringComparison.Ordinal) ||
                            !string.Equals(snapshot.lastPortal.spawnId, normalizedSpawn, StringComparison.Ordinal) ||
                            !string.Equals(snapshot.lastPortal.requiredFlagKey ?? "", normalizedFlag, StringComparison.Ordinal) ||
                            !SanctuaryResidentChunkIds.SequenceEqual(
                                snapshot.lastPortal.residentChunkSceneIds,
                                normalizedChunks);
            snapshot.lastPortal = new SanctuaryPortalRecord
            {
                sceneId = normalizedScene,
                spawnId = normalizedSpawn,
                requiredFlagKey = normalizedFlag,
                overworldChunkSceneId = normalizedChunks.Length > 0 ? normalizedChunks[0] : "",
                residentChunkSceneIds = SanctuaryResidentChunkIds.Copy(normalizedChunks)
            };
        }

        if (!latchChanged && !portalChanged)
            return;

        SaveLocalSnapshot();
        UpsertOutboxMutation();
        if (CanUseRemote())
            _ = FlushOutboxAsync();

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[SanctuaryStateService] Latched {kind} scene='{normalizedScene}' spawn='{normalizedSpawn}' portal={HasLastPortal}.",
                this);
        }
    }

    public async Task FlushOutboxAsync()
    {
        if (flushInProgress || currentIdentity == null || !CanUseRemote())
            return;

        flushInProgress = true;
        try
        {
            while (true)
            {
                SanctuaryMutationQueue queue = LoadOutbox();
                if (queue == null || string.IsNullOrWhiteSpace(queue.payloadJson))
                    break;

                MenuMutationResult result;
                try
                {
                    result = await SupabaseSanctuaryLatchService.SetLatchAsync(
                        supabaseConfig,
                        currentIdentity.playerProfileId,
                        currentIdentity.walletAddress,
                        currentIdentity.authUserId,
                        queue.payloadJson);
                }
                catch (Exception ex)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[SanctuaryStateService] Sanctuary sync deferred: {ex.Message}", this);
                    break;
                }

                if (!result.success)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[SanctuaryStateService] Sanctuary sync deferred: {result.error}", this);
                    break;
                }

                PlayerPrefs.DeleteKey(GetOutboxKey());
                PlayerPrefs.Save();
            }
        }
        finally
        {
            flushInProgress = false;
        }
    }

    private async Task HydrateInternalAsync(NarrativeProfileIdentity identity, bool forceOffline)
    {
        IsHydrated = false;
        currentIdentity = CloneIdentity(identity);

        SanctuaryStateSnapshot local = LoadLocalSnapshot();
        SanctuaryStateSnapshot hydrated = local != null ? local.Clone() : new SanctuaryStateSnapshot();
        SanctuaryMutationQueue pending = LoadOutbox();
        bool hasPending = pending != null && !string.IsNullOrWhiteSpace(pending.payloadJson);

        if (!forceOffline && CanUseRemote() && !hasPending)
        {
            try
            {
                SanctuaryStateSnapshot remote = await FetchRemoteSnapshotAsync();
                if (remote != null)
                    hydrated = remote;
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[SanctuaryStateService] Remote hydration failed; using local sanctuary. {ex.Message}",
                    this);
            }
        }
        else if (hasPending)
        {
            SanctuaryStateSnapshot pendingSnapshot = ParseSnapshotJson(pending.payloadJson);
            if (pendingSnapshot != null)
                hydrated = pendingSnapshot;
        }

        snapshot = hydrated ?? new SanctuaryStateSnapshot();
        SaveLocalSnapshot();
        IsHydrated = true;

        if (!forceOffline && CanUseRemote())
            await FlushOutboxAsync();
    }

    private async Task<SanctuaryStateSnapshot> FetchRemoteSnapshotAsync()
    {
        if (supabaseConfig == null)
            throw new InvalidOperationException("SupabaseConfigSO was not available for sanctuary state.");

        var request = new StatsRequest
        {
            player_profile_id = currentIdentity.playerProfileId ?? "",
            wallet_address = currentIdentity.walletAddress ?? "",
            auth_user_id = currentIdentity.authUserId ?? ""
        };
        SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
            supabaseConfig,
            "profile_get_stats",
            JsonUtility.ToJson(request));
        if (!response.IsSuccess)
            throw new InvalidOperationException($"profile_get_stats_http_{response.statusCode}: {response.body}");

        JObject payload = JObject.Parse(response.body);
        JObject progression = payload["progression_state"] as JObject;
        JToken sanctuaryToken = progression != null ? progression["sanctuary"] : null;
        return ParseSnapshotToken(sanctuaryToken);
    }

    private SanctuaryStateSnapshot LoadLocalSnapshot()
    {
        string json = PlayerPrefs.GetString(GetSnapshotKey(), "");
        if (string.IsNullOrWhiteSpace(json))
            return new SanctuaryStateSnapshot();

        try
        {
            return JsonUtility.FromJson<SanctuaryStateSnapshot>(json) ?? new SanctuaryStateSnapshot();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SanctuaryStateService] Invalid local sanctuary snapshot was ignored: {ex.Message}", this);
            return new SanctuaryStateSnapshot();
        }
    }

    private void SaveLocalSnapshot()
    {
        if (currentIdentity == null)
            return;

        PlayerPrefs.SetString(GetSnapshotKey(), JsonUtility.ToJson(snapshot ?? new SanctuaryStateSnapshot()));
        PlayerPrefs.Save();
    }

    private SanctuaryMutationQueue LoadOutbox()
    {
        string json = PlayerPrefs.GetString(GetOutboxKey(), "");
        if (string.IsNullOrWhiteSpace(json))
            return new SanctuaryMutationQueue();

        try
        {
            return JsonUtility.FromJson<SanctuaryMutationQueue>(json) ?? new SanctuaryMutationQueue();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SanctuaryStateService] Invalid sanctuary outbox was discarded: {ex.Message}", this);
            return new SanctuaryMutationQueue();
        }
    }

    private void UpsertOutboxMutation()
    {
        if (currentIdentity == null)
            return;

        var queue = new SanctuaryMutationQueue
        {
            mutationId = Guid.NewGuid().ToString("N"),
            payloadJson = BuildRemotePayloadJson(snapshot)
        };
        PlayerPrefs.SetString(GetOutboxKey(), JsonUtility.ToJson(queue));
        PlayerPrefs.Save();
    }

    private bool CanUseRemote()
    {
        return currentIdentity != null &&
               supabaseConfig != null &&
               !string.IsNullOrWhiteSpace(currentIdentity.playerProfileId) &&
               !string.IsNullOrWhiteSpace(SupabaseAuthSessionContext.AccessToken);
    }

    private string GetSnapshotKey()
    {
        return SnapshotPrefix + Hash128.Compute(ResolveCacheIdentity(currentIdentity));
    }

    private string GetOutboxKey()
    {
        return OutboxPrefix + Hash128.Compute(ResolveCacheIdentity(currentIdentity));
    }

    private static string ResolveCacheIdentity(NarrativeProfileIdentity identity)
    {
        return identity != null ? identity.CacheIdentity : "offline_default";
    }

    private static NarrativeProfileIdentity CloneIdentity(NarrativeProfileIdentity identity)
    {
        return new NarrativeProfileIdentity
        {
            playerProfileId = identity != null ? identity.playerProfileId ?? "" : "",
            walletAddress = identity != null ? identity.walletAddress ?? "" : "",
            authUserId = identity != null ? identity.authUserId ?? "" : ""
        };
    }

    internal static string BuildRemotePayloadJson(SanctuaryStateSnapshot state)
    {
        JObject sanctuary = BuildSanctuaryToken(state);
        return sanctuary != null ? sanctuary.ToString(Newtonsoft.Json.Formatting.None) : "{}";
    }

    internal static JObject BuildSanctuaryToken(SanctuaryStateSnapshot state)
    {
        var sanctuary = new JObject();
        if (state != null && state.lastLatch != null && state.lastLatch.IsValid)
        {
            sanctuary["lastLatch"] = new JObject
            {
                ["kind"] = state.lastLatch.kind.ToString(),
                ["sceneId"] = state.lastLatch.sceneId,
                ["spawnId"] = state.lastLatch.spawnId,
                ["requiredFlagKey"] = state.lastLatch.requiredFlagKey ?? "",
                ["overworldChunkSceneId"] = state.lastLatch.overworldChunkSceneId ?? "",
                ["residentChunkSceneIds"] = SanctuaryResidentChunkIds.ToJson(state.lastLatch.residentChunkSceneIds)
            };
        }

        if (state != null && state.lastPortal != null && state.lastPortal.IsValid)
        {
            sanctuary["lastPortal"] = new JObject
            {
                ["sceneId"] = state.lastPortal.sceneId,
                ["spawnId"] = state.lastPortal.spawnId,
                ["requiredFlagKey"] = state.lastPortal.requiredFlagKey ?? "",
                ["overworldChunkSceneId"] = state.lastPortal.overworldChunkSceneId ?? "",
                ["residentChunkSceneIds"] = SanctuaryResidentChunkIds.ToJson(state.lastPortal.residentChunkSceneIds)
            };
        }

        return sanctuary;
    }

    internal static SanctuaryStateSnapshot ParseSnapshotJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SanctuaryStateSnapshot();

        try
        {
            return ParseSnapshotToken(JToken.Parse(json));
        }
        catch (Exception)
        {
            try
            {
                return JsonUtility.FromJson<SanctuaryStateSnapshot>(json) ?? new SanctuaryStateSnapshot();
            }
            catch
            {
                return new SanctuaryStateSnapshot();
            }
        }
    }

    private static SanctuaryStateSnapshot ParseSnapshotToken(JToken token)
    {
        var result = new SanctuaryStateSnapshot();
        JObject sanctuary = token as JObject;
        if (sanctuary == null)
            return result;

        JObject latch = sanctuary["lastLatch"] as JObject;
        if (latch != null)
        {
            string kindText = latch.Value<string>("kind") ?? "";
            SanctuaryLatchKind kind = SanctuaryLatchKind.Hub;
            if (string.Equals(kindText, "Portal", StringComparison.OrdinalIgnoreCase) ||
                kindText == "1")
            {
                kind = SanctuaryLatchKind.Portal;
            }

            var record = new SanctuaryLatchRecord
            {
                kind = kind,
                sceneId = latch.Value<string>("sceneId") ?? "",
                spawnId = latch.Value<string>("spawnId") ?? "",
                requiredFlagKey = latch.Value<string>("requiredFlagKey") ?? "",
                overworldChunkSceneId = latch.Value<string>("overworldChunkSceneId") ?? "",
                residentChunkSceneIds = SanctuaryResidentChunkIds.FromJson(
                    latch["residentChunkSceneIds"],
                    latch.Value<string>("overworldChunkSceneId") ?? "")
            };
            if (record.IsValid)
                result.lastLatch = record;
        }

        JObject portal = sanctuary["lastPortal"] as JObject;
        if (portal != null)
        {
            var record = new SanctuaryPortalRecord
            {
                sceneId = portal.Value<string>("sceneId") ?? "",
                spawnId = portal.Value<string>("spawnId") ?? "",
                requiredFlagKey = portal.Value<string>("requiredFlagKey") ?? "",
                overworldChunkSceneId = portal.Value<string>("overworldChunkSceneId") ?? "",
                residentChunkSceneIds = SanctuaryResidentChunkIds.FromJson(
                    portal["residentChunkSceneIds"],
                    portal.Value<string>("overworldChunkSceneId") ?? "")
            };
            if (record.IsValid)
                result.lastPortal = record;
        }

        return result;
    }

    private static bool IsLatchDestinationUnlocked(SanctuaryLatchRecord latch)
    {
        if (latch == null || latch.kind != SanctuaryLatchKind.Portal)
            return true;
        return SanctuaryLatchZone.IsUnlockFlagSatisfied(latch.requiredFlagKey, treatEmptyAsDefaultPortalFlag: true);
    }

    private static bool IsPortalDestinationUnlocked(SanctuaryPortalRecord portal)
    {
        if (portal == null)
            return false;
        return SanctuaryLatchZone.IsUnlockFlagSatisfied(portal.requiredFlagKey, treatEmptyAsDefaultPortalFlag: true);
    }

    private static string[] ResolveResidentChunkSceneIds(
        SanctuaryLatchKind kind,
        string sceneId,
        string[] authoredChunkSceneIds,
        string legacySingleId)
    {
        if (kind != SanctuaryLatchKind.Portal)
            return Array.Empty<string>();

        return SanctuaryLatchZone.NormalizeResidentChunkSceneIds(
            authoredChunkSceneIds,
            legacySingleId,
            sceneId);
    }
}
