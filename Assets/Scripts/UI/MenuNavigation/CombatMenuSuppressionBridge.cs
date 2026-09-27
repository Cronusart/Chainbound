using UnityEngine;
using UnityEngine.SceneManagement;
using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// UI-side adapter that suppresses the player menu for the full lifetime of a
    /// combat scene, including bootstrap and post-battle transition windows.
    /// </summary>
    // FUTURE PvP INTERRUPT CYCLE: server-authoritative per-player timers and
    // timeout notifications belong in the match service, not this client gate.
    [DisallowMultipleComponent]
    public sealed class CombatMenuSuppressionBridge : MonoBehaviour
    {
        private CombatBattleController boundBattle;
        private GameplayMenuAvailabilityToken suppression;
        private bool hasSuppression;

        private void Awake()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            Rebind();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            Unbind();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => Rebind();
        private void HandleActiveSceneChanged(Scene previous, Scene next) => Rebind();

        private void Rebind()
        {
            CombatBattleController battle = FindBattleInActiveScene();
            if (battle != null && boundBattle == battle)
            {
                AcquireSuppression();
                return;
            }

            Unbind();
            if (battle == null)
                return;

            boundBattle = battle;
            battle.OnBattleStarted += HandleBattleStarted;
            AcquireSuppression();
        }

        private void Unbind()
        {
            if (boundBattle != null)
            {
                boundBattle.OnBattleStarted -= HandleBattleStarted;
            }
            boundBattle = null;
            ReleaseSuppression();
        }

        private void HandleBattleStarted(CombatState state) => AcquireSuppression();

        private void AcquireSuppression()
        {
            if (hasSuppression)
                return;

            suppression = GameplayMenuAvailabilityService.Instance.AcquireSuppression("combat_scene");
            hasSuppression = true;
        }

        private static CombatBattleController FindBattleInActiveScene()
        {
            return CombatSceneUtility.FindBattleController(SceneManager.GetActiveScene());
        }

        private void ReleaseSuppression()
        {
            if (!hasSuppression) return;
            suppression.Release();
            hasSuppression = false;
        }
    }
}
