using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Chainbound.Combat;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Profile-scoped boolean world state with durable vs rail lifetimes.
/// Local writes are immediate; remote writes use the progression-flag endpoint.
/// Rail flags are auto-snapshotted onto sequence checkpoints for continue-game.
/// </summary>
public sealed class ProfileWorldFlagStateService : MonoBehaviour
{
    [Serializable]
    private sealed class FlagRecord
    {
        public string key = "";
        public bool value;
    }

    [Serializable]
    private sealed class FlagSnapshot
    {
        public List<FlagRecord> flags = new List<FlagRecord>();
    }

    [Serializable]
    private sealed class FlagMutation
    {
        public string mutationId = "";
        public string key = "";
        public bool value;
    }

    [Serializable]
    private sealed class FlagMutationQueue
    {
        public List<FlagMutation> mutations = new List<FlagMutation>();
    }

    [Serializable]
    private sealed class StatsRequest
    {
        public string player_profile_id = "";
        public string wallet_address = "";
        public string auth_user_id = "";
    }

    private const string SnapshotPrefix = "SignalJam_ProfileWorldFlags_v1_";
    private const string OutboxPrefix = "SignalJam_ProfileWorldFlagOutbox_v1_";
    private const string DefaultRulesResourcePath = "Narrative/ProfileWorldFlagSceneRules";
    private const string DefaultObjectCatalogResourcePath = "Narrative/ProfileWorldFlagObjectNameCatalog";
    private const string DefaultPresenceConfigResourcePath = WorldPresenceConfigSO.DefaultResourcePath;

    private static ProfileWorldFlagStateService instance;
    private static WorldPresenceConfigSO assignedPresenceConfig;

    [Header("Backend")]
    [SerializeField] private SupabaseConfigSO supabaseConfig;

    [Header("Scene Entry Rules")]
    [SerializeField] private ProfileWorldFlagSceneRulesSO sceneEntryRules;
    [SerializeField] private ProfileWorldFlagObjectNameCatalogSO objectNameCatalog;

    [Header("World Presence")]
    [SerializeField] private WorldPresenceConfigSO presenceConfig;

    [Header("Diagnostics")]
    [SerializeField] private bool enableDebugLogs;

    private readonly Dictionary<string, bool> flags =
        new Dictionary<string, bool>(StringComparer.Ordinal);
    private readonly Dictionary<string, WorldPresenceLifetime> lifetimeOverrides =
        new Dictionary<string, WorldPresenceLifetime>(StringComparer.Ordinal);
    private readonly HashSet<string> mirroredFlagKeys =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly List<ProfileWorldFlagSceneRule> matchingSceneRules =
        new List<ProfileWorldFlagSceneRule>();
    private readonly List<ProfileWorldFlagObjectNameRule> matchingObjectRules =
        new List<ProfileWorldFlagObjectNameRule>();

    private NarrativeProfileIdentity currentIdentity;
    private bool flushInProgress;
    private Task activeHydration;

    public static ProfileWorldFlagStateService Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<ProfileWorldFlagStateService>();
                if (instance == null)
                {
                    GameServicesContainer services = GameServicesContainer.Instance;
                    var host = new GameObject("ProfileWorldFlagStateService");
                    if (services != null)
                        host.transform.SetParent(services.transform, false);
                    instance = host.AddComponent<ProfileWorldFlagStateService>();
                    if (services == null && Application.isPlaying)
                        DontDestroyOnLoad(host);
                }
            }

            return instance;
        }
    }

    public bool IsHydrated { get; private set; }
    public event Action<bool> OnReadyChanged;
    public event Action<string, bool> OnFlagChanged;

    public static WorldPresenceConfigSO AssignedPresenceConfig => assignedPresenceConfig;
    public WorldPresenceConfigSO PresenceConfig => WorldPresenceConfigRuntime.Current;

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
        if (sceneEntryRules == null)
            sceneEntryRules = Resources.Load<ProfileWorldFlagSceneRulesSO>(DefaultRulesResourcePath);
        if (objectNameCatalog == null)
            objectNameCatalog = Resources.Load<ProfileWorldFlagObjectNameCatalogSO>(DefaultObjectCatalogResourcePath);
        if (presenceConfig == null)
            presenceConfig = Resources.Load<WorldPresenceConfigSO>(DefaultPresenceConfigResourcePath);
        assignedPresenceConfig = presenceConfig;

        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (instance == this)
        {
            instance = null;
            assignedPresenceConfig = null;
        }
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

    public bool GetFlag(string flagKey, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(flagKey))
            return defaultValue;

        return flags.TryGetValue(flagKey.Trim(), out bool value) ? value : defaultValue;
    }

    public bool HasFlag(string flagKey)
    {
        return !string.IsNullOrWhiteSpace(flagKey) && flags.ContainsKey(flagKey.Trim());
    }

    public WorldPresenceLifetime Classify(string flagKey)
    {
        string key = flagKey != null ? flagKey.Trim() : "";
        if (string.IsNullOrEmpty(key))
            return WorldPresenceLifetime.Unspecified;

        if (lifetimeOverrides.TryGetValue(key, out WorldPresenceLifetime overrideLifetime) &&
            overrideLifetime != WorldPresenceLifetime.Unspecified)
        {
            return overrideLifetime;
        }

        WorldPresenceConfigSO config = WorldPresenceConfigRuntime.Current;
        return config != null ? config.Classify(key) : WorldPresenceLifetime.Durable;
    }

    public void RegisterLifetimeOverride(string flagKey, WorldPresenceLifetime lifetime)
    {
        string key = flagKey != null ? flagKey.Trim() : "";
        if (string.IsNullOrEmpty(key) || lifetime == WorldPresenceLifetime.Unspecified)
            return;
        lifetimeOverrides[key] = lifetime;
    }

    public void ClearLifetimeOverride(string flagKey)
    {
        string key = flagKey != null ? flagKey.Trim() : "";
        if (string.IsNullOrEmpty(key))
            return;
        lifetimeOverrides.Remove(key);
    }

    /// <summary>
    /// Accepts a durable local mutation immediately and schedules remote sync.
    /// Repeating the same value is a no-op unless an unsynced mutation exists.
    /// Lifetime defaults to the prefix board unless an override is supplied.
    /// </summary>
    public void SetFlag(string flagKey, bool value)
    {
        SetFlag(flagKey, value, WorldPresenceLifetime.Unspecified);
    }

    public void SetFlag(string flagKey, bool value, WorldPresenceLifetime lifetime)
    {
        string key = flagKey != null ? flagKey.Trim() : "";
        if (string.IsNullOrWhiteSpace(key))
        {
            Debug.LogWarning("[ProfileWorldFlagStateService] Cannot set an empty flag key.", this);
            return;
        }

        if (lifetime != WorldPresenceLifetime.Unspecified)
            lifetimeOverrides[key] = lifetime;

        bool changed = !flags.TryGetValue(key, out bool existing) || existing != value;
        flags[key] = value;
        SaveLocalSnapshot();
        UpsertOutboxMutation(key, value);
        MirrorFlag(key, value, changed);
        if (changed)
            ApplyNamedObjectGatesForFlag(key);

        if (CanUseRemote())
            _ = FlushOutboxAsync();
    }

    public List<NarrativeFlagValue> CaptureRailSnapshot()
    {
        var snapshot = new List<NarrativeFlagValue>();
        foreach (KeyValuePair<string, bool> pair in flags)
        {
            if (Classify(pair.Key) != WorldPresenceLifetime.Rail)
                continue;
            snapshot.Add(new NarrativeFlagValue(pair.Key, pair.Value));
        }

        snapshot.Sort((a, b) => string.CompareOrdinal(a?.key ?? "", b?.key ?? ""));
        return snapshot;
    }

    public void RestoreRailSnapshot(IReadOnlyList<(string key, bool value)> entries)
    {
        var snapshotKeys = new HashSet<string>(StringComparer.Ordinal);
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                (string key, bool value) = entries[i];
                string trimmed = key != null ? key.Trim() : "";
                if (string.IsNullOrEmpty(trimmed) || Classify(trimmed) != WorldPresenceLifetime.Rail)
                    continue;
                snapshotKeys.Add(trimmed);
                SetFlag(trimmed, value);
            }
        }

        WorldPresenceConfigSO config = WorldPresenceConfigRuntime.Current;
        if (config == null || !config.RewindMissingRailsToFalse)
        {
            ReapplyNamedObjectGates();
            return;
        }

        var missingRails = new List<string>();
        foreach (KeyValuePair<string, bool> pair in flags)
        {
            if (Classify(pair.Key) != WorldPresenceLifetime.Rail)
                continue;
            if (snapshotKeys.Contains(pair.Key))
                continue;
            missingRails.Add(pair.Key);
        }

        for (int i = 0; i < missingRails.Count; i++)
            SetFlag(missingRails[i], false);

        ReapplyNamedObjectGates();
    }

    public void RestoreRailSnapshot(IReadOnlyList<NarrativeFlagValue> entries)
    {
        var tuples = new List<(string key, bool value)>();
        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                NarrativeFlagValue entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;
                tuples.Add((entry.key, entry.value));
            }
        }

        RestoreRailSnapshot(tuples);
    }

    /// <summary>
    /// Writes several flags then reapplies name-catalog gates. Used by checkpoint
    /// resume so scene defaults pick up already-persisted values even when unchanged.
    /// </summary>
    public void SetFlags(IReadOnlyList<(string key, bool value)> entries)
    {
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            (string key, bool value) = entries[i];
            SetFlag(key, value);
        }

        ReapplyNamedObjectGates();
    }

    public void ReapplyNamedObjectGates()
    {
        if (!IsHydrated)
            return;

        ApplyNamedObjectGatesForLoadedScenes();
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
                FlagMutationQueue queue = LoadOutbox();
                FlagMutation mutation = queue.mutations != null
                    ? queue.mutations.FirstOrDefault(item => item != null)
                    : null;
                if (mutation == null)
                    break;

                MenuMutationResult result;
                try
                {
                    result = await SupabaseProgressionFlagService.SetFlagAsync(
                        supabaseConfig,
                        currentIdentity.playerProfileId,
                        currentIdentity.walletAddress,
                        currentIdentity.authUserId,
                        mutation.key,
                        mutation.value);
                }
                catch (Exception ex)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[ProfileWorldFlagStateService] Flag sync deferred: {ex.Message}", this);
                    break;
                }

                if (!result.success)
                {
                    if (enableDebugLogs)
                        Debug.LogWarning($"[ProfileWorldFlagStateService] Flag sync deferred: {result.error}", this);
                    break;
                }

                queue.mutations.RemoveAll(item =>
                    item != null && string.Equals(item.mutationId, mutation.mutationId, StringComparison.Ordinal));
                SaveOutbox(queue);
            }
        }
        finally
        {
            flushInProgress = false;
        }
    }

    private async Task HydrateInternalAsync(NarrativeProfileIdentity identity, bool forceOffline)
    {
        SetReady(false);
        currentIdentity = CloneIdentity(identity);

        Dictionary<string, bool> localFlags = LoadLocalSnapshot();
        Dictionary<string, bool> hydratedFlags = new Dictionary<string, bool>(localFlags, StringComparer.Ordinal);
        FlagMutationQueue pending = LoadOutbox();

        if (!forceOffline && CanUseRemote())
        {
            try
            {
                Dictionary<string, bool> remoteFlags = await FetchRemoteFlagsAsync();
                hydratedFlags = remoteFlags;
                if (pending.mutations != null)
                {
                    for (int i = 0; i < pending.mutations.Count; i++)
                    {
                        FlagMutation mutation = pending.mutations[i];
                        if (mutation != null && !string.IsNullOrWhiteSpace(mutation.key))
                            hydratedFlags[mutation.key] = mutation.value;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    $"[ProfileWorldFlagStateService] Remote hydration failed; using profile local flags. {ex.Message}",
                    this);
            }
        }

        ReplaceRuntimeFlags(hydratedFlags);
        SaveLocalSnapshot();
        SetReady(true);
        ApplySceneEntryRules(SceneManager.GetActiveScene().name);
        ApplyNamedObjectGatesForLoadedScenes();

        if (!forceOffline && CanUseRemote())
            await FlushOutboxAsync();
    }

    private async Task<Dictionary<string, bool>> FetchRemoteFlagsAsync()
    {
        if (supabaseConfig == null)
            throw new InvalidOperationException("SupabaseConfigSO was not available for profile world flags.");

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
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        if (progression == null)
            return result;

        foreach (JProperty property in progression.Properties())
        {
            if (property.Value.Type == JTokenType.Boolean)
                result[property.Name] = property.Value.Value<bool>();
        }

        return result;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsHydrated)
            return;

        ApplySceneEntryRules(scene.name);
        ApplyNamedObjectGatesForScene(scene.name);
    }

    private void ApplySceneEntryRules(string sceneId)
    {
        if (!IsHydrated || sceneEntryRules == null)
            return;

        sceneEntryRules.GetMatchingRules(sceneId, matchingSceneRules);
        for (int i = 0; i < matchingSceneRules.Count; i++)
        {
            ProfileWorldFlagSceneRule rule = matchingSceneRules[i];
            if (!string.IsNullOrEmpty(rule.RequiredFlagKey) && !GetFlag(rule.RequiredFlagKey))
                continue;
            if (!HasFlag(rule.FlagKey) || GetFlag(rule.FlagKey) != rule.FlagValue)
                SetFlag(rule.FlagKey, rule.FlagValue);
        }
    }

    private void ApplyNamedObjectGatesForLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.IsValid() && scene.isLoaded)
                ApplyNamedObjectGatesForScene(scene.name);
        }
    }

    private void ApplyNamedObjectGatesForFlag(string flagKey)
    {
        if (objectNameCatalog == null || string.IsNullOrWhiteSpace(flagKey))
            return;

        IReadOnlyList<ProfileWorldFlagObjectNameRule> rules = objectNameCatalog.Rules;
        if (rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            ProfileWorldFlagObjectNameRule rule = rules[i];
            if (rule == null || !string.Equals(rule.FlagKey, flagKey.Trim(), StringComparison.Ordinal))
                continue;

            if (string.IsNullOrWhiteSpace(rule.SceneId))
            {
                ApplyNamedObjectGatesForLoadedScenes();
                return;
            }

            Scene scene = SceneManager.GetSceneByName(rule.SceneId);
            if (scene.IsValid() && scene.isLoaded)
                ApplyNamedObjectRule(rule, scene);
        }
    }

    private void ApplyNamedObjectGatesForScene(string sceneId)
    {
        if (objectNameCatalog == null || string.IsNullOrWhiteSpace(sceneId))
            return;

        objectNameCatalog.GetMatchingRules(sceneId, matchingObjectRules);
        Scene scene = SceneManager.GetSceneByName(sceneId);
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        for (int i = 0; i < matchingObjectRules.Count; i++)
            ApplyNamedObjectRule(matchingObjectRules[i], scene);
    }

    private void ApplyNamedObjectRule(ProfileWorldFlagObjectNameRule rule, Scene scene)
    {
        if (rule == null || !scene.IsValid())
            return;

        GameObject target = FindNamedObjectInScene(scene, rule.ObjectName);
        if (target == null)
            return;

        WorldPresenceApply.Apply(
            target,
            this,
            rule.FlagKey,
            rule.ExpectedFlagValue,
            rule.ActiveWhenMatched,
            defaultActive: false,
            hasCapturedDefault: false);
    }

    private static GameObject FindNamedObjectInScene(Scene scene, string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || !scene.IsValid())
            return null;

        GameObject[] objects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null &&
                candidate.scene == scene &&
                string.Equals(candidate.name, objectName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
    }

    private void ReplaceRuntimeFlags(Dictionary<string, bool> values)
    {
        WorldState worldState = WorldState.Instance;
        foreach (string key in mirroredFlagKeys)
            worldState.SetGlobalFlag(key, false);

        mirroredFlagKeys.Clear();
        flags.Clear();
        lifetimeOverrides.Clear();
        if (values == null)
            return;

        foreach (KeyValuePair<string, bool> pair in values)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                continue;
            flags[pair.Key] = pair.Value;
            MirrorFlag(pair.Key, pair.Value, false);
        }
    }

    private void MirrorFlag(string key, bool value, bool notify)
    {
        mirroredFlagKeys.Add(key);
        WorldState.Instance.SetGlobalFlag(key, value);
        if (notify)
            OnFlagChanged?.Invoke(key, value);
    }

    private Dictionary<string, bool> LoadLocalSnapshot()
    {
        var result = new Dictionary<string, bool>(StringComparer.Ordinal);
        string json = PlayerPrefs.GetString(GetSnapshotKey(), "");
        if (string.IsNullOrWhiteSpace(json))
            return result;

        try
        {
            FlagSnapshot snapshot = JsonUtility.FromJson<FlagSnapshot>(json);
            if (snapshot?.flags == null)
                return result;
            for (int i = 0; i < snapshot.flags.Count; i++)
            {
                FlagRecord record = snapshot.flags[i];
                if (record != null && !string.IsNullOrWhiteSpace(record.key))
                    result[record.key] = record.value;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileWorldFlagStateService] Invalid local flag snapshot was ignored: {ex.Message}", this);
        }

        return result;
    }

    private void SaveLocalSnapshot()
    {
        if (currentIdentity == null)
            return;

        var snapshot = new FlagSnapshot();
        foreach (KeyValuePair<string, bool> pair in flags.OrderBy(item => item.Key, StringComparer.Ordinal))
            snapshot.flags.Add(new FlagRecord { key = pair.Key, value = pair.Value });
        PlayerPrefs.SetString(GetSnapshotKey(), JsonUtility.ToJson(snapshot));
        PlayerPrefs.Save();
    }

    private FlagMutationQueue LoadOutbox()
    {
        string json = PlayerPrefs.GetString(GetOutboxKey(), "");
        if (string.IsNullOrWhiteSpace(json))
            return new FlagMutationQueue();

        try
        {
            FlagMutationQueue queue = JsonUtility.FromJson<FlagMutationQueue>(json);
            return queue ?? new FlagMutationQueue();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[ProfileWorldFlagStateService] Invalid flag outbox was discarded: {ex.Message}", this);
            return new FlagMutationQueue();
        }
    }

    private void UpsertOutboxMutation(string key, bool value)
    {
        if (currentIdentity == null)
            return;

        FlagMutationQueue queue = LoadOutbox();
        queue.mutations.RemoveAll(item => item != null && string.Equals(item.key, key, StringComparison.Ordinal));
        queue.mutations.Add(new FlagMutation
        {
            mutationId = Guid.NewGuid().ToString("N"),
            key = key,
            value = value
        });
        SaveOutbox(queue);
    }

    private void SaveOutbox(FlagMutationQueue queue)
    {
        PlayerPrefs.SetString(GetOutboxKey(), JsonUtility.ToJson(queue ?? new FlagMutationQueue()));
        PlayerPrefs.Save();
    }

    private bool CanUseRemote()
    {
        return currentIdentity != null &&
            supabaseConfig != null &&
            !string.IsNullOrWhiteSpace(currentIdentity.playerProfileId) &&
            !string.IsNullOrWhiteSpace(SupabaseAuthSessionContext.AccessToken);
    }

    private void SetReady(bool ready)
    {
        if (IsHydrated == ready)
            return;
        IsHydrated = ready;
        OnReadyChanged?.Invoke(ready);
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
}
