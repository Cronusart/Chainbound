using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reusable readiness coordinator configured per large scene. Loomville uses it
/// to keep camera binding and staged hierarchy work behind the loading cover.
/// </summary>
public sealed class SceneReadinessWarmup : MonoBehaviour, ISceneWarmupTask
{
    [Header("Task")]
    [SerializeField] private string warmupId = "scene-readiness";
    [SerializeField] private int order = 100;
    [SerializeField, Min(0.01f)] private float weight = 3f;
    [SerializeField, Min(0f)] private float timeoutSeconds;

    [Header("Critical Readiness")]
    [SerializeField] private bool requireActiveCamera = true;
    [SerializeField] private bool requirePlayer = true;
    [SerializeField] private List<MonoBehaviour> requiredSceneServices = new List<MonoBehaviour>();

    [Header("Degraded-Mode Checks")]
    [SerializeField] private bool expectLightingCoordinator = true;
    [SerializeField] private bool expectAmbienceZone = true;

    [Header("Staged Work")]
    [SerializeField, Min(1)] private int billboardBatchSizeOverride;

    public string WarmupId => string.IsNullOrWhiteSpace(warmupId) ? "scene-readiness" : warmupId;
    public int Order => order;
    public float Weight => Mathf.Max(0.01f, weight);
    public bool IsCritical => true;
    public float TimeoutSeconds => timeoutSeconds;

    public IEnumerator Warmup(SceneWarmupContext context)
    {
        Scene scene = context.Transition.Scene;
        context.ReportProgress(0.02f, "Checking scene readiness");

        if (!string.IsNullOrEmpty(context.Transition.SpawnId))
        {
            float spawnWait = ResolveWaitSeconds(context);
            float spawnElapsed = 0f;
            SpawnPoint requestedSpawn = null;
            while (spawnElapsed <= spawnWait)
            {
                requestedSpawn = SpawnService.Instance?.GetSpawnPoint(context.Transition.SpawnId);
                if (requestedSpawn != null &&
                    requestedSpawn.gameObject != null &&
                    requestedSpawn.gameObject.scene.IsValid() &&
                    requestedSpawn.gameObject.scene.isLoaded)
                {
                    break;
                }

                requestedSpawn = null;
                spawnElapsed += Time.unscaledDeltaTime;
                context.ReportProgress(0.04f, "Waiting for spawn point");
                yield return null;
            }

            if (requestedSpawn == null)
            {
                context.Fail($"Requested spawn '{context.Transition.SpawnId}' is missing from '{scene.name}'.");
                yield break;
            }
        }

        Camera sceneCamera = null;
        if (requireActiveCamera)
        {
            float cameraWait = ResolveWaitSeconds(context);
            float cameraElapsed = 0f;
            while (sceneCamera == null && cameraElapsed <= cameraWait)
            {
                sceneCamera = SceneCameraResolver.ResolveGameplayCamera()
                    ?? SceneCameraResolver.Resolve(scene);
                if (sceneCamera != null)
                    break;
                cameraElapsed += Time.unscaledDeltaTime;
                context.ReportProgress(0.06f, "Waiting for gameplay camera");
                yield return null;
            }

            if (sceneCamera == null)
            {
                context.Fail($"Scene '{scene.name}' has no active camera.");
                yield break;
            }
        }
        else
        {
            sceneCamera = SceneCameraResolver.ResolveGameplayCamera()
                ?? SceneCameraResolver.Resolve(scene);
        }

        if (requirePlayer)
        {
            GameObject player = null;
            float playerWait = ResolveWaitSeconds(context);
            float playerElapsed = 0f;
            while (player == null && playerElapsed <= playerWait)
            {
                player = ResolveWarmupPlayer();
                if (player != null)
                    break;
                playerElapsed += Time.unscaledDeltaTime;
                context.ReportProgress(0.08f, "Waiting for player");
                yield return null;
            }

            if (player == null)
            {
                context.Fail($"Scene '{scene.name}' has no resolved player.");
                yield break;
            }
        }

        for (int i = 0; i < requiredSceneServices.Count; i++)
        {
            MonoBehaviour service = requiredSceneServices[i];
            if (service == null || service.gameObject.scene != scene || !service.isActiveAndEnabled)
            {
                context.Fail($"Required scene service at index {i} is missing or inactive in '{scene.name}'.");
                yield break;
            }
        }

        // The authoritative applier is persistent under GameServices, so a
        // streamed chunk is valid when it does not own a local lighting writer.
        if (expectLightingCoordinator &&
            !ContainsSceneComponent<TimeOfDayVisualApplier>(scene) &&
            TimeOfDayVisualApplier.ActiveInstance == null)
            context.Warn($"'{scene.name}' has no TimeOfDayVisualApplier; continuing with authored lighting.");
        if (expectAmbienceZone && !ContainsSceneComponent<AmbienceZone>(scene))
            context.Warn($"'{scene.name}' has no AmbienceZone; continuing without scene ambience.");

        SpriteBillboard[] allBillboards = FindObjectsByType<SpriteBillboard>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);
        int billboardCount = CountInScene(allBillboards, scene);
        int processedBillboards = 0;
        int batchSize = billboardBatchSizeOverride > 0
            ? billboardBatchSizeOverride
            : context.Config.WarmupBatchSize;
        for (int i = 0; i < allBillboards.Length; i++)
        {
            SpriteBillboard billboard = allBillboards[i];
            if (billboard == null || billboard.gameObject.scene != scene)
                continue;
            if (sceneCamera != null)
                billboard.SetTargetCamera(sceneCamera);
            processedBillboards++;
            context.ReportProgress(
                0.1f + 0.55f * processedBillboards / Mathf.Max(1f, billboardCount),
                $"Binding scene visuals ({processedBillboards}/{billboardCount})");
            if (processedBillboards % batchSize == 0)
                yield return null;
        }

        GrassPatchManager[] allGrassManagers = FindObjectsByType<GrassPatchManager>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);
        int grassCount = CountInScene(allGrassManagers, scene);
        int processedGrass = 0;
        for (int i = 0; i < allGrassManagers.Length; i++)
        {
            GrassPatchManager manager = allGrassManagers[i];
            if (manager == null || manager.gameObject.scene != scene)
                continue;
            manager.InitializeForSceneWarmup();
            processedGrass++;
            context.ReportProgress(
                0.65f + 0.34f * processedGrass / Mathf.Max(1f, grassCount),
                $"Preparing grass patches ({processedGrass}/{grassCount})");
            yield return null;
        }

        context.ReportProgress(1f, "Scene ready");
    }

    private static float ResolveWaitSeconds(SceneWarmupContext context)
    {
        if (context.Config != null && context.Config.WarmupTaskTimeoutSeconds > 0f)
            return Mathf.Min(2f, context.Config.WarmupTaskTimeoutSeconds);
        return 2f;
    }

    private static GameObject ResolveWarmupPlayer()
    {
        GameObject player = SpawnService.Instance?.GetResolvedPlayerRoot();
        if (player != null)
            return player;

        PlayerMovement3D[] movements = FindObjectsByType<PlayerMovement3D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < movements.Length; i++)
        {
            if (movements[i] != null)
                return movements[i].gameObject;
        }

        return null;
    }

    private static bool ContainsSceneComponent<T>(Scene scene) where T : Component
    {
        T[] components = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].gameObject.scene == scene)
                return true;
        }
        return false;
    }

    private static int CountInScene<T>(T[] components, Scene scene) where T : Component
    {
        int count = 0;
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].gameObject.scene == scene)
                count++;
        }
        return count;
    }
}
