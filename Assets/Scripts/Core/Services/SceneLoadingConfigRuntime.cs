using UnityEngine;

/// <summary>
/// Runtime registry for the scene-loading lever board. Resolution order is
/// explicit injection, the persistent SceneLoader registration, then defaults.
/// </summary>
public static class SceneLoadingConfigRuntime
{
    private static SceneLoadingConfigSO registered;
    private static SceneLoadingConfigSO runtimeDefault;

    public static void Register(SceneLoadingConfigSO config)
    {
        if (config != null)
            registered = config;
    }

    public static void Unregister(SceneLoadingConfigSO config)
    {
        if (registered == config)
            registered = null;
    }

    public static SceneLoadingConfigSO Resolve(SceneLoadingConfigSO injected = null)
    {
        if (injected != null)
            return injected;
        if (registered != null)
            return registered;

        if (runtimeDefault == null)
            runtimeDefault = SceneLoadingConfigSO.CreateRuntimeDefault();
        return runtimeDefault;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        registered = null;
        runtimeDefault = null;
    }
}
