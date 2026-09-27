using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene-local production entry point. Persistent services remain owned by the
/// GameServices prefab; this controller only submits the first transition.
/// </summary>
[DefaultExecutionOrder(1000)]
public sealed class BootstrapStartupController : MonoBehaviour
{
    public const string BootstrapSceneId = "Bootstrap";
    public const string EditorLaunchSceneSessionKey = "SignalJam.Loading.EditorLaunchScene";

    [SerializeField, Min(1)] private int serviceReadyFrameBudget = 120;

    private IEnumerator Start()
    {
        SceneLoader loader = null;
        for (int frame = 0; frame < serviceReadyFrameBudget && loader == null; frame++)
        {
            loader = FindObjectOfType<SceneLoader>();
            if (loader == null)
                yield return null;
        }

        if (loader == null)
        {
            Debug.LogError("[Bootstrap] SceneLoader was not initialized by GameServices.");
            yield break;
        }

        // All service Awake calls have completed by Start. One additional frame
        // lets service-owned startup coroutines publish their initial state.
        yield return null;

        SceneLoadingConfigSO config = loader.Config;
        string destination = config.StartupSceneId;
        TransitionStyle? styleOverride = config.StartupStyle;

#if UNITY_EDITOR
        string editorDestination = UnityEditor.SessionState.GetString(
            EditorLaunchSceneSessionKey,
            string.Empty);
        UnityEditor.SessionState.EraseString(EditorLaunchSceneSessionKey);
        if (!string.IsNullOrWhiteSpace(editorDestination))
        {
            destination = editorDestination.Trim();
            styleOverride = null;
        }
#endif

        if (string.Equals(destination, BootstrapSceneId, System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[Bootstrap] Bootstrap cannot transition to itself; using the configured startup scene.");
            destination = config.StartupSceneId;
            styleOverride = config.StartupStyle;
        }

        loader.RequestTransition(new SceneTransitionRequest(
            destination,
            styleOverride: styleOverride,
            sourceId: "Bootstrap.Startup",
            completed: ValidateStartupCompletion));
    }

    private static void ValidateStartupCompletion(SceneTransitionResult result)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!result.Succeeded)
            return;

        GameServicesContainer[] containers = FindObjectsByType<GameServicesContainer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        if (containers.Length != 1)
        {
            Debug.LogError(
                $"[Bootstrap] Startup completed with {containers.Length} GameServices containers; exactly one is required.");
            return;
        }

        Debug.Log("[Bootstrap] Startup completed with one authoritative GameServices container.");
#endif
    }
}
