using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject defining a quest with objectives.
/// </summary>
[CreateAssetMenu(fileName = "QuestDefinition", menuName = "Chainbound/Narrative/Quests/Quest Definition")]
public class QuestDefinitionSO : ScriptableObject
{
    [Header("Quest Info")]
    [Tooltip("Unique quest ID")]
    [SerializeField] private string questId = "";
    
    [Tooltip("Quest name")]
    [SerializeField] private string questName = "";
    
    [Tooltip("Quest description")]
    [TextArea(2, 5)]
    [SerializeField] private string description = "";

    [Header("Persistence")]
    [Tooltip("Increment when authored quest state semantics change in a way that may require migration.")]
    [Min(1)]
    [SerializeField] private int contentVersion = 1;

    [Tooltip("When true, completing the final objective completes the quest using its default resolution.")]
    [SerializeField] private bool autoCompleteWhenObjectivesComplete = true;
    
    [Header("Objectives")]
    [Tooltip("List of objectives")]
    [SerializeField] private List<QuestObjective> objectives = new List<QuestObjective>();

    [Header("Resolutions")]
    [Tooltip("Resolution used by objective auto-completion and CompleteQuest without an explicit outcome.")]
    [SerializeField] private string defaultResolutionId = "standard";

    [Tooltip("Allowed stable resolution IDs and their optional context-key allowlists.")]
    [SerializeField] private List<QuestResolutionDefinition> resolutions = new List<QuestResolutionDefinition>();

    [Header("Tags")]
    [Tooltip("Optional tags for filtering (Main, Side, Tutorial, etc.).")]
    [SerializeField] private List<string> tags = new List<string>();
    
    /// <summary>Quest ID.</summary>
    public string QuestId => questId;
    
    /// <summary>Quest name.</summary>
    public string QuestName => questName;
    
    /// <summary>Quest description.</summary>
    public string Description => description;

    public int ContentVersion => Mathf.Max(1, contentVersion);

    public bool AutoCompleteWhenObjectivesComplete => autoCompleteWhenObjectivesComplete;
    
    /// <summary>List of objectives.</summary>
    public IReadOnlyList<QuestObjective> Objectives => objectives;

    public string DefaultResolutionId => string.IsNullOrWhiteSpace(defaultResolutionId)
        ? "standard"
        : defaultResolutionId.Trim();

    public IReadOnlyList<QuestResolutionDefinition> Resolutions => resolutions;

    /// <summary>Designer tags.</summary>
    public IReadOnlyList<string> Tags => tags;
    
    /// <summary>Get objective by index.</summary>
    public QuestObjective GetObjective(int index)
    {
        if (index < 0 || index >= objectives.Count) return null;
        return objectives[index];
    }

    public bool ValidateResolution(
        string resolutionId,
        IEnumerable<string> contextKeys,
        out string error)
    {
        error = "";
        string normalizedId = resolutionId != null ? resolutionId.Trim() : "";
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            error = $"Quest '{questId}' resolution ID is empty.";
            return false;
        }

        QuestResolutionDefinition definition = null;
        if (resolutions != null)
        {
            for (int i = 0; i < resolutions.Count; i++)
            {
                QuestResolutionDefinition candidate = resolutions[i];
                if (candidate != null && string.Equals(candidate.resolutionId, normalizedId, System.StringComparison.Ordinal))
                {
                    definition = candidate;
                    break;
                }
            }
        }

        if (definition == null)
        {
            if ((resolutions == null || resolutions.Count == 0) &&
                string.Equals(normalizedId, DefaultResolutionId, System.StringComparison.Ordinal))
            {
                return true;
            }

            error = $"Quest '{questId}' does not allow resolution '{normalizedId}'.";
            return false;
        }

        if (contextKeys == null || definition.allowedContextKeys == null || definition.allowedContextKeys.Count == 0)
            return true;

        var allowed = new HashSet<string>(definition.allowedContextKeys, System.StringComparer.Ordinal);
        foreach (string key in contextKeys)
        {
            if (!string.IsNullOrWhiteSpace(key) && !allowed.Contains(key))
            {
                error = $"Quest '{questId}' resolution '{normalizedId}' does not allow context key '{key}'.";
                return false;
            }
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        contentVersion = Mathf.Max(1, contentVersion);
        if (string.IsNullOrWhiteSpace(defaultResolutionId))
            defaultResolutionId = "standard";

        var ids = new HashSet<string>(System.StringComparer.Ordinal);
        if (resolutions != null)
        {
            for (int i = 0; i < resolutions.Count; i++)
            {
                QuestResolutionDefinition definition = resolutions[i];
                if (definition == null || string.IsNullOrWhiteSpace(definition.resolutionId))
                    continue;
                if (!ids.Add(definition.resolutionId))
                    Debug.LogWarning($"[QuestDefinitionSO] Duplicate resolution ID '{definition.resolutionId}'.", this);
            }
        }

        if (resolutions != null && resolutions.Count > 0 && !ids.Contains(DefaultResolutionId))
        {
            Debug.LogWarning(
                $"[QuestDefinitionSO] Default resolution '{DefaultResolutionId}' is not listed in allowed resolutions.",
                this);
        }
    }
#endif
}

[System.Serializable]
public class QuestResolutionDefinition
{
    [Tooltip("Stable, localization-independent outcome ID.")]
    public string resolutionId = "standard";

    [Tooltip("Optional allowlist of context keys for this outcome. Empty allows any key.")]
    public List<string> allowedContextKeys = new List<string>();
}

/// <summary>
/// Quest objective definition.
/// </summary>
[System.Serializable]
public class QuestObjective
{
    [Tooltip("Objective ID")]
    public string objectiveId = "";
    
    [Tooltip("Objective description")]
    [TextArea(1, 3)]
    public string description = "";
    
    [Tooltip("Target amount (e.g., collect 2 coffees)")]
    public int targetAmount = 1;
    
    [Tooltip("Current progress")]
    public int currentProgress = 0;
    
    /// <summary>Is this objective complete?</summary>
    public bool IsComplete => currentProgress >= targetAmount;
}

