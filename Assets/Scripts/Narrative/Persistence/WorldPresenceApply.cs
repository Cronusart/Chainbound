using UnityEngine;

/// <summary>
/// Shared presence apply rules for <see cref="WorldPresenceBinding"/>,
/// <see cref="ProfileWorldFlagObjectGate"/>, and the name catalog.
/// </summary>
public static class WorldPresenceApply
{
    public static void Apply(
        GameObject target,
        ProfileWorldFlagStateService service,
        string flagKey,
        bool expectedFlagValue,
        bool activeWhenMatched,
        bool defaultActive,
        bool hasCapturedDefault)
    {
        if (target == null)
            return;

        WorldPresenceConfigSO config = WorldPresenceConfigRuntime.Current;
        bool keepSceneDefault = config == null || config.KeepSceneDefaultUntilFlagExists;

        if (service == null || !service.IsHydrated)
            return;

        if (!service.HasFlag(flagKey))
        {
            if (keepSceneDefault)
            {
                if (hasCapturedDefault)
                    target.SetActive(defaultActive);
                return;
            }
        }

        bool matches = service.GetFlag(flagKey) == expectedFlagValue;
        target.SetActive(matches ? activeWhenMatched : !activeWhenMatched);
    }
}
