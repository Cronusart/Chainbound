using System;
using UnityEngine;

/// <summary>
/// Narrative lever board for profile world-presence lifetime and hydrate policy.
/// Presence content IDs stay on scene <see cref="WorldPresenceBinding"/> components
/// and sequence steps; this board only owns prefixes and apply rules.
/// Not registered on Combat <c>BalanceConfigRuntime</c> (Combat.Runtime cannot
/// reference Assembly-CSharp).
/// </summary>
[CreateAssetMenu(
    fileName = "WorldPresenceConfig",
    menuName = "Chainbound/Narrative/Persistence/World Presence Config")]
public sealed class WorldPresenceConfigSO : ScriptableObject
{
    public const string DefaultResourcePath = "Narrative/WorldPresenceConfig";

    [Header("Identity")]
    [Min(1)] [SerializeField] private int configVersion = 1;

    [Header("Lifetime Prefixes")]
    [Tooltip("Keys starting with these prefixes are durable world facts. Never rewound by continue-game.")]
    [SerializeField] private string[] durableKeyPrefixes = { "world.", "world.pickup." };

    [Tooltip("Keys starting with these prefixes are sequence rails. Auto-snapshotted onto the checkpoint row.")]
    [SerializeField] private string[] railKeyPrefixes = { "intro.guardrail.", "sequence." };

    [Tooltip("Safer default for jungle obstacles than accidental rewind.")]
    [SerializeField] private WorldPresenceLifetime defaultLifetimeWhenUnmatched = WorldPresenceLifetime.Durable;

    [Header("Hydrate Policy")]
    [Tooltip("When a flag has never been written, leave the authored scene active state.")]
    [SerializeField] private bool keepSceneDefaultUntilFlagExists = true;

    [Tooltip("Continue from an earlier checkpoint turns off rails written after that row.")]
    [SerializeField] private bool rewindMissingRailsToFalse = true;

    public int ConfigVersion => Mathf.Max(1, configVersion);
    public bool KeepSceneDefaultUntilFlagExists => keepSceneDefaultUntilFlagExists;
    public bool RewindMissingRailsToFalse => rewindMissingRailsToFalse;
    public WorldPresenceLifetime DefaultLifetimeWhenUnmatched => defaultLifetimeWhenUnmatched;

    public WorldPresenceLifetime Classify(string flagKey)
    {
        string key = flagKey != null ? flagKey.Trim() : "";
        if (string.IsNullOrEmpty(key))
            return WorldPresenceLifetime.Unspecified;

        if (MatchesPrefix(key, railKeyPrefixes))
            return WorldPresenceLifetime.Rail;
        if (MatchesPrefix(key, durableKeyPrefixes))
            return WorldPresenceLifetime.Durable;
        return defaultLifetimeWhenUnmatched;
    }

    public static WorldPresenceConfigSO CreateRuntimeDefault()
    {
        var config = CreateInstance<WorldPresenceConfigSO>();
        config.hideFlags = HideFlags.HideAndDontSave;
        config.name = "WorldPresenceConfig_RuntimeDefault";
        config.configVersion = 1;
        config.durableKeyPrefixes = new[] { "world.", "world.pickup." };
        config.railKeyPrefixes = new[] { "intro.guardrail.", "sequence." };
        config.defaultLifetimeWhenUnmatched = WorldPresenceLifetime.Durable;
        config.keepSceneDefaultUntilFlagExists = true;
        config.rewindMissingRailsToFalse = true;
        return config;
    }

    private static bool MatchesPrefix(string key, string[] prefixes)
    {
        if (prefixes == null || string.IsNullOrEmpty(key))
            return false;

        for (int i = 0; i < prefixes.Length; i++)
        {
            string prefix = prefixes[i];
            if (string.IsNullOrWhiteSpace(prefix))
                continue;
            if (key.StartsWith(prefix.Trim(), StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Resolves the shipped world-presence board. Prefers a service-assigned asset,
/// then <c>Resources/Narrative/WorldPresenceConfig</c>, then runtime defaults.
/// </summary>
public static class WorldPresenceConfigRuntime
{
    private static WorldPresenceConfigSO cachedResource;
    private static WorldPresenceConfigSO runtimeFallback;
    private static WorldPresenceConfigSO testOverride;

    public static WorldPresenceConfigSO Current
    {
        get
        {
            if (testOverride != null)
                return testOverride;

            WorldPresenceConfigSO assigned = ProfileWorldFlagStateService.AssignedPresenceConfig;
            if (assigned != null)
                return assigned;

            if (cachedResource == null)
                cachedResource = Resources.Load<WorldPresenceConfigSO>(WorldPresenceConfigSO.DefaultResourcePath);
            if (cachedResource != null)
                return cachedResource;

            if (runtimeFallback == null)
                runtimeFallback = WorldPresenceConfigSO.CreateRuntimeDefault();
            return runtimeFallback;
        }
    }

    public static void SetTestOverride(WorldPresenceConfigSO config)
    {
        testOverride = config;
    }
}
