using System;

/// <summary>
/// Interface for save service.
/// Allows wallet-based saving to replace stub implementation later.
/// </summary>
public interface ISaveService
{
    /// <summary>
    /// Check if a profile exists.
    /// </summary>
    bool HasProfile();
    
    /// <summary>
    /// Commit a new profile (first save).
    /// </summary>
    void CommitNewProfile(ProfileData data);
    
    /// <summary>
    /// Save a checkpoint (future saves).
    /// </summary>
    void SaveCheckpoint(CheckpointData data);
    
    /// <summary>
    /// Load profile data.
    /// </summary>
    ProfileData LoadProfile();
}

/// <summary>
/// Profile data for first save.
/// </summary>
[Serializable]
public class ProfileData
{
    public string playerName = "";
    public string starterChainlingId = "";
    public string starterDisplayName = "";
    public long timestamp = 0;
    public string walletId = ""; // Placeholder for future wallet integration
    
    public ProfileData()
    {
        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}

/// <summary>
/// Checkpoint data for future saves.
/// </summary>
[Serializable]
public class CheckpointData
{
    public string sceneName = "";
    public string spawnId = "";
    public string sequenceId = "";
    public int stepIndex = -1;
    // Add more checkpoint data as needed
}

