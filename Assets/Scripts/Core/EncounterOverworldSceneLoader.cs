using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Loads the overworld from an <see cref="OverworldReturnInfo"/> (exact encounter return teleport or spawn-based loads).
/// Shared by post-battle flow and aborted combat bootstrap (e.g. narrative lineup failure).
/// </summary>
public static class EncounterOverworldSceneLoader
{
    /// <inheritdoc cref="PostBattleSceneReturnHandler.HandleReturnToOverworld"/>
    public static void LoadReturnScene(in OverworldReturnInfo info)
    {
        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
        {
            Debug.LogWarning("[EncounterOverworldSceneLoader] No SceneLoader found. Cannot return to overworld.");
            return;
        }

        // Encounter / narrative returns: saved scene + exact teleport placement.
        if (info.useExactPosition && !string.IsNullOrEmpty(info.sceneName))
        {
            string returnLoadSceneId = info.sceneName;
            OverworldStreamingService streamingService = OverworldStreamingService.Instance;
            if (streamingService != null &&
                streamingService.PrepareReturnToSet(info.sceneName, info.loadedChunkIds))
            {
                // Return through the hub's existing hard-load pipeline, then
                // restore the saved additive members before exact placement.
                returnLoadSceneId = streamingService.ResolveHubFor(info.sceneName);
            }

            Debug.Log(
                $"[EncounterOverworldSceneLoader] Exact return: scene='{info.sceneName}', load='{returnLoadSceneId}', position={info.position}, rotation={info.rotation.eulerAngles}, returnSpawnId='{info.returnSpawnId}'.");

            SanctuarySpawnDirector.ClearPendingPresentation();
            EncounterReturnPlacement.ArmWorldVolumeEnterSuppress();

            SpawnService spawnService = SpawnService.Instance;
            if (spawnService != null)
                spawnService.SkipAutoSpawnForNextSceneLoad();
            if (sceneLoader.IsTransitioning)
            {
                Debug.LogWarning(
                    "[EncounterOverworldSceneLoader] SceneLoader.IsTransitioning is true — " +
                    "return load may be rejected as busy. SkipAutoSpawn is armed for the next accepted load.");
            }

            EncounterReturnPlacement.EnsureCreated();
            OverworldReturnInfo captured = info;
            sceneLoader.LoadSceneAsync(
                returnLoadSceneId,
                TransitionStyle.QuickFade,
                "",
                onComplete: null,
                onBeforeReveal: () => EncounterReturnPlacement.Instance.ApplyEncounterReturnPlacement(captured));
            return;
        }

        var style = string.IsNullOrEmpty(info.spawnId) ? TransitionStyle.QuickFade : TransitionStyle.FullLoading;
        sceneLoader.LoadScene(
            info.sceneName,
            style,
            info.spawnId ?? "",
            preferredOverworldChunkSceneIds: info.preferredOverworldChunkSceneIds);
    }

    /// <summary>
    /// Builds return info mirroring successful post-battle return (prefer saved snapshot, else fallback home spawn).
    /// </summary>
    public static OverworldReturnInfo ResolveDefaultReturnInfo(string fallbackHomeScene, string fallbackSpawnId)
    {
        var bridge = Object.FindObjectOfType<SolanaServiceBridge>();
        IEncounterReturnContextService returnContextService = bridge?.EncounterReturnContextService;

        var returnCtx = returnContextService?.GetReturnContext();
        if (returnCtx != null && returnCtx.isValid)
        {
            return new OverworldReturnInfo
            {
                sceneName = returnCtx.sceneId,
                useExactPosition = true,
                position = returnCtx.position,
                rotation = returnCtx.rotation,
                returnSpawnId = returnCtx.returnSpawnId ?? "",
                loadedChunkIds = returnCtx.loadedChunkIds,
            };
        }

        return new OverworldReturnInfo
        {
            sceneName = fallbackHomeScene,
            spawnId = fallbackSpawnId,
            useExactPosition = false,
        };
    }
}
