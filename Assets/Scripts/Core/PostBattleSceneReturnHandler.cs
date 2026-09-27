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

        if (mvpAlwaysReturnHome)
        {
            Debug.Log($"[PostBattleSceneReturnHandler] MVP override: returning to '{mvpHomeScene}' (spawn '{mvpHomeSpawnId}').");
            sceneLoader.LoadScene(mvpHomeScene, mvpTransitionStyle, mvpHomeSpawnId);
            return;
        }

        EncounterOverworldSceneLoader.LoadReturnScene(info);
    }
}
