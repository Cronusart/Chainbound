using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Bridges the CombatPostBattleRouter's OnReturnToOverworld event to SceneLoader.
/// Lives in Assembly-CSharp so it can access both Combat and SceneLoader.
///
/// Attach to the same GameObject as CombatPostBattleRouter in the battle scene.
/// </summary>
public class PostBattleSceneReturnHandler : MonoBehaviour
{
    [SerializeField] private CombatPostBattleRouter postBattleRouter;

    [Header("MVP Override")]
    [Tooltip("When enabled, non-exact returns (no saved encounter position) go to the home scene below. Exact narrative returns (useExactPosition) always win.")]
    [SerializeField] private bool mvpAlwaysReturnHome = false;
    [Tooltip("Scene to load when mvpAlwaysReturnHome is on.")]
    [SerializeField] private string mvpHomeScene = "Loomville";
    [Tooltip("Spawn point ID in the home scene. Leave empty for default spawn.")]
    [SerializeField] private string mvpHomeSpawnId = "";
    [Tooltip("Transition style used for the MVP home return.")]
    [SerializeField] private TransitionStyle mvpTransitionStyle = TransitionStyle.FullLoading;

    private void Awake()
    {
        if (postBattleRouter == null)
            postBattleRouter = GetComponent<CombatPostBattleRouter>();
    }

    private void OnEnable()
    {
        if (postBattleRouter != null)
            postBattleRouter.OnReturnToOverworld += HandleReturnToOverworld;
    }

    private void OnDisable()
    {
        if (postBattleRouter != null)
            postBattleRouter.OnReturnToOverworld -= HandleReturnToOverworld;
    }

    private void HandleReturnToOverworld(OverworldReturnInfo info)
    {
        SceneLoader sceneLoader = SceneLoader.Instance;
        if (sceneLoader == null)
        {
            Debug.LogWarning("[PostBattleSceneReturnHandler] No SceneLoader found. Cannot return to overworld.");
            return;
        }

        if (info.isDefeatReturn)
        {
            OverworldReturnInfo wipeInfo = info;
            SanctuaryStateService sanctuary = SanctuaryStateService.Instance;
            if (sanctuary != null)
            {
                sanctuary.GetWipeDestination(
                    out string sceneId,
                    out string spawnId,
                    out bool _,
                    out string[] residentChunkSceneIds);
                wipeInfo.sceneName = sceneId;
                wipeInfo.spawnId = spawnId;
                wipeInfo.useExactPosition = false;
                wipeInfo.returnSpawnId = "";
                wipeInfo.loadedChunkIds = null;
                wipeInfo.preferredOverworldChunkSceneIds = residentChunkSceneIds;
                SanctuarySpawnDirector.ArmPendingPresentation(sceneId, spawnId, SanctuaryWakeKind.CombatDefeat);
            }

            EncounterOverworldSceneLoader.LoadReturnScene(wipeInfo);
            return;
        }

        // Encounter return (NarrativeEncounterService / flee): always use saved scene + teleport.
        if (info.useExactPosition && !string.IsNullOrEmpty(info.sceneName))
        {
            EncounterOverworldSceneLoader.LoadReturnScene(info);
            return;
        }

        // A rejoined battle can finish after the process that captured the
        // overworld snapshot was interrupted. Do not trust the combat scene's
        // generic home fallback in that case: use the last safe sanctuary
        // destination instead.
        if (PostBattleReturnSafetyPolicy.ShouldRouteToSanctuary(info))
        {
            OverworldReturnInfo sanctuaryInfo = info;
            SanctuaryStateService sanctuary = SanctuaryStateService.Instance;
            if (sanctuary != null)
            {
                sanctuary.GetWipeDestination(
                    out string sceneId,
                    out string spawnId,
                    out bool _,
                    out string[] residentChunkSceneIds);
                sanctuaryInfo.sceneName = sceneId;
                sanctuaryInfo.spawnId = spawnId;
                sanctuaryInfo.preferredOverworldChunkSceneIds = residentChunkSceneIds;
            }
            else
            {
                sanctuaryInfo.sceneName = SanctuaryStateService.DefaultWipeSceneId;
                sanctuaryInfo.spawnId = SanctuaryStateService.DefaultWipeSpawnId;
                sanctuaryInfo.preferredOverworldChunkSceneIds = null;
            }

            sanctuaryInfo.useExactPosition = false;
            sanctuaryInfo.returnSpawnId = "";
            sanctuaryInfo.loadedChunkIds = null;
            sanctuaryInfo.returnContextMissing = false;
            SanctuarySpawnDirector.ClearPendingPresentation();
            Debug.LogWarning(
                $"[PostBattleSceneReturnHandler] Missing return context; routing to sanctuary " +
                $"'{sanctuaryInfo.sceneName}' (spawn '{sanctuaryInfo.spawnId}').");
            EncounterOverworldSceneLoader.LoadReturnScene(sanctuaryInfo);
            return;
        }

        if (mvpAlwaysReturnHome)
        {
            Debug.Log($"[PostBattleSceneReturnHandler] MVP override: returning to '{mvpHomeScene}' (spawn '{mvpHomeSpawnId}').");
            sceneLoader.LoadScene(mvpHomeScene, mvpTransitionStyle, mvpHomeSpawnId);
            return;
        }

        EncounterOverworldSceneLoader.LoadReturnScene(info);
    }
}

/// <summary>
/// Keeps the post-battle recovery decision independent from scene loading so
/// rejoin safety rules remain small and testable.
/// </summary>
public static class PostBattleReturnSafetyPolicy
{
    public static bool ShouldRouteToSanctuary(OverworldReturnInfo info)
    {
        return !info.isDefeatReturn && info.returnContextMissing;
    }
}
