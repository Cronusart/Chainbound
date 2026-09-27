using System.Collections;
using UnityEngine;

/// <summary>
/// Interface for encounter service.
/// Allows combat system to replace stub implementation later.
/// Extracted from legacy combat to shared location for cross-system access.
/// </summary>
public interface IEncounterService
{
    /// <summary>
    /// Start an encounter. Returns coroutine for async completion.
    /// </summary>
    IEnumerator StartEncounter(string encounterId, EncounterConfig config);
    
    /// <summary>
    /// Last encounter outcome.
    /// </summary>
    EncounterOutcome LastOutcome { get; }
}

/// <summary>
/// Encounter outcome types.
/// </summary>
public enum EncounterOutcome
{
    ForcedRun,
    Win,
    Lose,
    Capture,
    Escaped,
    /// <summary>Combat bootstrap or battle start aborted (e.g. missing lineup).</summary>
    BootstrapFailed,
}

/// <summary>
/// Encounter configuration.
/// </summary>
[System.Serializable]
public class EncounterConfig
{
    public bool canRun = true;
    public bool canCapture = false;
    public string forcedOutcome = ""; // "ForcedRun", "Win", etc.

    /// <summary>
    /// When true, narrative encounter services skip saving return position before battle (use when a
    /// sequence step already saved the snapshot at the encounter zone, e.g. Wait For Trigger).
    /// </summary>
    public bool skipSaveReturnContext;

    /// <summary>
    /// Optional SpawnPoint id in the current overworld scene for post-battle placement (used when registry entry does not set one).
    /// </summary>
    public string returnSpawnPointId;
}
