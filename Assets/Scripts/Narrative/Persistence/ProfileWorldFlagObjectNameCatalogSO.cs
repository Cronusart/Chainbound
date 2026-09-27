using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Name-based visibility gates for profile world flags when a scene object
/// cannot hold a <see cref="ProfileWorldFlagObjectGate"/> reference.
/// </summary>
[Serializable]
public sealed class ProfileWorldFlagObjectNameRule
{
    [SerializeField] private string sceneId = "";
    [SerializeField] private string objectName = "";
    [SerializeField] private string flagKey = "";
    [SerializeField] private bool expectedFlagValue = true;
    [SerializeField] private bool activeWhenMatched = true;

    public string SceneId => sceneId != null ? sceneId.Trim() : "";
    public string ObjectName => objectName != null ? objectName.Trim() : "";
    public string FlagKey => flagKey != null ? flagKey.Trim() : "";
    public bool ExpectedFlagValue => expectedFlagValue;
    public bool ActiveWhenMatched => activeWhenMatched;
}

[CreateAssetMenu(
    fileName = "ProfileWorldFlagObjectNameCatalog",
    menuName = "Chainbound/Narrative/Persistence/Profile World Flag Object Name Catalog")]
public sealed class ProfileWorldFlagObjectNameCatalogSO : ScriptableObject
{
    [SerializeField] private List<ProfileWorldFlagObjectNameRule> rules =
        new List<ProfileWorldFlagObjectNameRule>();

    public IReadOnlyList<ProfileWorldFlagObjectNameRule> Rules => rules;

    public void GetMatchingRules(string sceneId, List<ProfileWorldFlagObjectNameRule> results)
    {
        if (results == null)
            return;

        results.Clear();
        if (string.IsNullOrWhiteSpace(sceneId) || rules == null)
            return;

        string normalizedScene = sceneId.Trim();
        for (int i = 0; i < rules.Count; i++)
        {
            ProfileWorldFlagObjectNameRule rule = rules[i];
            if (rule == null ||
                string.IsNullOrWhiteSpace(rule.ObjectName) ||
                string.IsNullOrWhiteSpace(rule.FlagKey))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(rule.SceneId) ||
                string.Equals(rule.SceneId, normalizedScene, StringComparison.OrdinalIgnoreCase))
            {
                results.Add(rule);
            }
        }
    }
}
