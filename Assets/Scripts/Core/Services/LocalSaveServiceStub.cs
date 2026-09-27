using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Stub implementation of ISaveService using PlayerPrefs.
/// Will be replaced with wallet-based implementation later.
/// </summary>
public class LocalSaveServiceStub : MonoBehaviour, ISaveService, IPlayerNameService, IStarterCommitService
{
    private static LocalSaveServiceStub instance;
    
    /// <summary>
    /// Singleton instance. Prefer using scene reference or service locator.
    /// </summary>
    public static LocalSaveServiceStub Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<LocalSaveServiceStub>();
                if (instance == null)
                {
                    GameObject go = new GameObject("LocalSaveServiceStub");
                    instance = go.AddComponent<LocalSaveServiceStub>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private const string PROFILE_KEY = "SignalJam_Profile";
    private const string CHECKPOINT_KEY = "SignalJam_Checkpoint";
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[LocalSaveServiceStub] Duplicate instance found. Destroying {gameObject.name}");
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
    
    public bool HasProfile()
    {
        bool hasProfile = PlayerPrefs.HasKey(PROFILE_KEY);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LocalSaveServiceStub] HasProfile: {hasProfile}");
        }
        
        return hasProfile;
    }
    
    public void CommitNewProfile(ProfileData data)
    {
        if (data == null)
        {
            Debug.LogError("[LocalSaveServiceStub] Cannot commit null profile data!");
            return;
        }
        
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(PROFILE_KEY, json);
        PlayerPrefs.Save();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LocalSaveServiceStub] Committed new profile: {data.playerName} (timestamp: {data.timestamp})");
        }
    }
    
    public void SaveCheckpoint(CheckpointData data)
    {
        if (data == null)
        {
            Debug.LogError("[LocalSaveServiceStub] Cannot save null checkpoint data!");
            return;
        }
        
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(CHECKPOINT_KEY, json);
        PlayerPrefs.Save();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LocalSaveServiceStub] Saved checkpoint: {data.sceneName} (spawn: {data.spawnId})");
        }
    }
    
    public ProfileData LoadProfile()
    {
        if (!HasProfile())
        {
            return null;
        }
        
        string json = PlayerPrefs.GetString(PROFILE_KEY);
        ProfileData data = JsonUtility.FromJson<ProfileData>(json);
        
        if (enableDebugLogs)
        {
            Debug.Log($"[LocalSaveServiceStub] Loaded profile: {data.playerName}");
        }
        
        return data;
    }

    /// <summary>
    /// Offline onboarding adapter used by the main-menu New Game test route.
    /// Keeps the sequence's interface-driven commit steps moving without
    /// attempting a network request or requiring a wallet/profile identity.
    /// </summary>
    public Task<PlayerNameCommitResult> CommitDisplayNameAsync(
        string walletAddress,
        string displayName,
        Chainbound.Combat.SupabaseConfigSO configOverride = null)
    {
        string trimmedName = displayName != null ? displayName.Trim() : "";
        if (string.IsNullOrWhiteSpace(trimmedName))
            return Task.FromResult(PlayerNameCommitResult.FromError("Display name was empty."));

        ProfileData profile = LoadProfile() ?? new ProfileData();
        profile.playerName = trimmedName;
        profile.walletId = walletAddress != null ? walletAddress.Trim() : profile.walletId;
        CommitNewProfile(profile);

        return Task.FromResult(PlayerNameCommitResult.FromSuccess(trimmedName));
    }

    /// <summary>
    /// Offline starter commit used by the main-menu New Game test route.
    /// The deterministic local id makes downstream diagnostics readable while
    /// preserving the species id as the authoritative selection value.
    /// </summary>
    public Task<StarterCommitResult> CommitStarterAsync(
        string walletAddress,
        string starterSpeciesId,
        Chainbound.Combat.SupabaseConfigSO configOverride = null,
        string displayName = null)
    {
        string trimmedSpeciesId = starterSpeciesId != null ? starterSpeciesId.Trim() : "";
        string trimmedDisplayName = displayName != null ? displayName.Trim() : "";
        if (string.IsNullOrWhiteSpace(trimmedSpeciesId))
            return Task.FromResult(StarterCommitResult.FromError("Starter species id was empty."));

        ProfileData profile = LoadProfile() ?? new ProfileData();
        if (string.IsNullOrWhiteSpace(profile.starterChainlingId))
            profile.starterChainlingId = trimmedSpeciesId;
        if (string.IsNullOrWhiteSpace(profile.starterDisplayName) && !string.IsNullOrWhiteSpace(trimmedDisplayName))
            profile.starterDisplayName = trimmedDisplayName;
        profile.walletId = walletAddress != null ? walletAddress.Trim() : profile.walletId;
        CommitNewProfile(profile);

        string committedSpeciesId = profile.starterChainlingId;
        string localChainlingId = $"offline-{committedSpeciesId}";
        return Task.FromResult(StarterCommitResult.FromSuccess(committedSpeciesId, localChainlingId));
    }
}

