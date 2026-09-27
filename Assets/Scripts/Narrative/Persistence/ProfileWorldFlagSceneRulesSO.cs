using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One authored mapping from entering a scene to a durable profile world flag.
/// Scene IDs and flag keys are stable content IDs; scene objects do not own the
/// persistence decision.
/// </summary>
[Serializable]
public sealed class ProfileWorldFlagSceneRule
{
    [SerializeField] private string sceneId = "";
    [SerializeField] private string flagKey = "";
    [SerializeField] private bool flagValue = true;
    [SerializeField] private string requiredFlagKey = "";

    public string SceneId => sceneId != null ? sceneId.Trim() : "";
    public string FlagKey => flagKey != null ? flagKey.Trim() : "";
    public bool FlagValue => flagValue;
    public string RequiredFlagKey => requiredFlagKey != null ? requiredFlagKey.Trim() : "";
}

/// <summary>
/// Reusable catalog of profile-scoped world events caused by scene entry.
/// </summary>
[CreateAssetMenu(
    fileName = "ProfileWorldFlagSceneRules",
    menuName = "Chainbound/Narrative/Persistence/Profile World Flag Scene Rules")]
public sealed class ProfileWorldFlagSceneRulesSO : ScriptableObject
{
    [SerializeField] private List<ProfileWorldFlagSceneRule> rules =
        new List<ProfileWorldFlagSceneRule>();

    public IReadOnlyList<ProfileWorldFlagSceneRule> Rules => rules;

    public void GetMatchingRules(string sceneId, List<ProfileWorldFlagSceneRule> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (string.IsNullOrWhiteSpace(sceneId) || rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            ProfileWorldFlagSceneRule rule = rules[i];
            if (rule != null &&
                string.Equals(rule.SceneId, sceneId.Trim(), StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(rule.FlagKey))
            {
                results.Add(rule);
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < rules.Count; i++)
        {
            ProfileWorldFlagSceneRule rule = rules[i];
            if (rule == null || string.IsNullOrWhiteSpace(rule.SceneId) ||
                string.IsNullOrWhiteSpace(rule.FlagKey))
            {
                Debug.LogWarning(
                    $"[ProfileWorldFlagSceneRulesSO] Rule {i} needs both a scene ID and flag key.",
                    this);
                continue;
            }

            string identity = $"{rule.SceneId}:{rule.FlagKey}";
            if (!seen.Add(identity))
            {
                Debug.LogWarning(
                    $"[ProfileWorldFlagSceneRulesSO] Duplicate scene/flag mapping '{identity}'.",
                    this);
            }
        }
    }
#endif
}
