using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent state for sequences across scenes.
/// Stored in DontDestroyOnLoad object.
/// </summary>
[System.Serializable]
public class SequenceState
{
    [Header("Current Sequence")]
    [Tooltip("ID of currently running sequence")]
    public string currentSequenceId = "";
    
    [Tooltip("Current step index")]
    public int currentStepIndex = -1;
    
    [Header("Checkpoint")]
    [Tooltip("Checkpoint ID for resume")]
    public string checkpointId = "";
    
    [Tooltip("Checkpoint label (alternative to checkpointId)")]
    public string checkpointLabel = "";
    
    [Tooltip("Step index where checkpoint was set")]
    public int checkpointStepIndex = -1;

    [Tooltip("Last successfully completed authored label")]
    public string lastCompletedLabel = "";

    [Tooltip("Local/server revision of this sequence checkpoint")]
    public long revision;

    [Tooltip("Authored sequence content version used when this checkpoint was saved")]
    public int sequenceVersion = 1;
    
    [Header("Scene State")]
    [Tooltip("Current scene ID")]
    public string sceneId = "";
    
    [Tooltip("Current spawn point ID")]
    public string spawnId = "";
    
    [Header("Execute Once Tracking")]
    [Tooltip("Set of completed step GUIDs (for execute-once semantics)")]
    public HashSet<string> completedStepGuids = new HashSet<string>();
    
    [Header("Variables")]
    [Tooltip("Player name (from name input)")]
    public string playerName = "";
    
    [Tooltip("Selected starter chainling ID")]
    public string starterSelected = "";
    
    [Tooltip("Persistent variables dictionary")]
    public Dictionary<string, string> persistentVariables = new Dictionary<string, string>();

    [Tooltip("Rail world-presence flags captured when this checkpoint was set.")]
    public Dictionary<string, bool> railFlagSnapshot = new Dictionary<string, bool>(StringComparer.Ordinal);
    
    [Header("Director State")]
    [Tooltip("Set of gate keys that have been played (for TryPlayOnce)")]
    public HashSet<string> playedGateKeys = new HashSet<string>();
    
    /// <summary>
    /// Check if state is valid (has active sequence).
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(currentSequenceId);
    
    /// <summary>
    /// Reset state to empty.
    /// </summary>
    public void Reset()
    {
        currentSequenceId = "";
        currentStepIndex = -1;
        checkpointId = "";
        checkpointLabel = "";
        checkpointStepIndex = -1;
        lastCompletedLabel = "";
        revision = 0;
        sequenceVersion = 1;
        sceneId = "";
        spawnId = "";
        playerName = "";
        starterSelected = "";
        persistentVariables.Clear();
        if (railFlagSnapshot == null)
            railFlagSnapshot = new Dictionary<string, bool>(StringComparer.Ordinal);
        else
            railFlagSnapshot.Clear();
        completedStepGuids.Clear();
        // Note: playedGateKeys is NOT cleared on reset (persists across sequences)
    }
    
    /// <summary>
    /// Set a persistent variable.
    /// </summary>
    public void SetVariable(string name, string value)
    {
        persistentVariables[name] = value;
    }
    
    /// <summary>
    /// Get a persistent variable.
    /// </summary>
    public string GetVariable(string name, string defaultValue = "")
    {
        persistentVariables.TryGetValue(name, out string value);
        return value ?? defaultValue;
    }
    
    /// <summary>
    /// Set checkpoint information.
    /// </summary>
    public void SetCheckpoint(string checkpointId, string checkpointLabel, int stepIndex, string sceneId = "", string spawnId = "")
    {
        this.checkpointId = checkpointId;
        this.checkpointLabel = checkpointLabel;
        this.checkpointStepIndex = stepIndex;
        if (!string.IsNullOrEmpty(sceneId))
        {
            this.sceneId = sceneId;
        }
        if (!string.IsNullOrEmpty(spawnId))
        {
            this.spawnId = spawnId;
        }
    }

    public void ReplaceRailFlagSnapshot(IReadOnlyList<NarrativeFlagValue> entries)
    {
        if (railFlagSnapshot == null)
            railFlagSnapshot = new Dictionary<string, bool>(StringComparer.Ordinal);

        railFlagSnapshot.Clear();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            NarrativeFlagValue entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                continue;
            railFlagSnapshot[entry.key.Trim()] = entry.value;
        }
    }

    public List<NarrativeFlagValue> CopyRailFlagSnapshot()
    {
        var copy = new List<NarrativeFlagValue>();
        if (railFlagSnapshot == null)
            return copy;

        foreach (KeyValuePair<string, bool> pair in railFlagSnapshot)
            copy.Add(new NarrativeFlagValue(pair.Key, pair.Value));
        copy.Sort((a, b) => string.CompareOrdinal(a?.key ?? "", b?.key ?? ""));
        return copy;
    }
    
    /// <summary>
    /// Clear checkpoint information.
    /// </summary>
    public void ClearCheckpoint()
    {
        checkpointId = "";
        checkpointLabel = "";
        checkpointStepIndex = -1;
    }
}

