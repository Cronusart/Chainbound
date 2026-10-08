using UnityEngine;
using System.Collections;

namespace Chainbound.Combat
{
    /// <summary>
    /// Simple auto-start trigger for <see cref="CombatBattleController"/>.
    /// Attach this to the BattleController GameObject in your combat scene
    /// to automatically begin a battle after a short delay.
    ///
    /// For production, the overworld encounter system or scene-loading manager
    /// would call <see cref="CombatBattleController.StartBattle"/> directly
    /// instead of relying on this script.
    /// </summary>
    public class CombatAutoStarter : MonoBehaviour
    {
        [Tooltip("The CombatBattleController to start.")]
        [SerializeField] private CombatBattleController controller;

        [Tooltip("Seconds to wait after Start() before triggering the battle. " +
                 "Gives other components time to initialize (presenter, UI, etc.).")]
        [SerializeField] private float delaySeconds = 0.5f;

        [Header("Manual Turn Drive")]
        [Tooltip("When usePlayerInput=true and autoRunTurns=false, automatically call ExecuteNextTurn() so the selector can receive turns.")]
        [SerializeField] private bool driveTurnsWhenAutoRunDisabled = true;
        [SerializeField] private bool verboseLogs = true;

        [Header("Animation Sync")]
        [Tooltip("Optional: wait for the presenter to finish all animations before advancing turns.")]
        [SerializeField] private Combat2BattlefieldPresenter presenter;
        [SerializeField] private MonoBehaviour startupBootstrapOverride;

        private bool _eventsSubscribed;
        private bool _manualTurnKickQueued;
        private ICombatSceneBootstrap _sceneBootstrap;

        private void Start()
        {
            if (controller == null)
            {
                controller = GetComponent<CombatBattleController>();
                if (controller == null)
                {
                    Debug.LogError("[CombatAutoStarter] No CombatBattleController assigned or found on this GameObject.");
                    return;
                }
            }

            if (presenter == null)
                presenter = FindObjectOfType<Combat2BattlefieldPresenter>(true);

            SubscribeEvents();
            ResolveSceneBootstrap();
            StartCoroutine(BeginBattleWhenReady());
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void BeginBattle()
        {
            Debug.Log("[CombatAutoStarter] Starting battle...");
            controller.StartBattle();
            TryQueueManualTurnKick("BeginBattle");
        }

        private IEnumerator BeginBattleWhenReady()
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            if (_sceneBootstrap != null)
            {
                yield return new WaitUntil(() => _sceneBootstrap.IsReady || _sceneBootstrap.HasFailed);

                if (_sceneBootstrap.HasFailed)
                {
                    Debug.LogError($"[CombatAutoStarter] Scene bootstrap failed: {_sceneBootstrap.FailureReason}");
                    yield break;
                }
            }

            if (controller != null && controller.IsCasualPvpSession)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (verboseLogs)
                    Debug.Log("[PvPMatch] Casual spine owns battle start.");
#endif
                yield break;
            }

            BeginBattle();
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed || controller == null)
                return;

            controller.OnTurnEnded += HandleTurnEnded;
            controller.OnBattleEnded += HandleBattleEnded;
            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_eventsSubscribed || controller == null)
                return;

            controller.OnTurnEnded -= HandleTurnEnded;
            controller.OnBattleEnded -= HandleBattleEnded;
            _eventsSubscribed = false;
        }

        private void HandleTurnEnded(int turnIndex)
        {
            TryQueueManualTurnKick($"OnTurnEnded({turnIndex})");
        }

        private void HandleBattleEnded(BattleResult result)
        {
            _manualTurnKickQueued = false;
        }

        private void TryQueueManualTurnKick(string context)
        {
            if (!ShouldDriveManualTurns() || _manualTurnKickQueued)
                return;

            _manualTurnKickQueued = true;
            StartCoroutine(ExecuteNextTurnNextFrame(context));
        }

        private IEnumerator ExecuteNextTurnNextFrame(string context)
        {
            yield return null;

            if (presenter != null)
                yield return new WaitUntil(() => presenter == null || presenter.IsEventQueueIdle);

            _manualTurnKickQueued = false;

            if (!ShouldDriveManualTurns())
                yield break;

            if (verboseLogs)
                Debug.Log($"[CombatAutoStarter] Driving manual turn via ExecuteNextTurn() from {context}.");

            controller.ExecuteNextTurn();
        }

        private bool ShouldDriveManualTurns()
        {
            if (!driveTurnsWhenAutoRunDisabled || controller == null)
                return false;
            if (!controller.UsePlayerInput || controller.AutoRunTurnsEnabled)
                return false;
            if (!controller.BattleStarted || controller.State == null || !controller.State.isActive)
                return false;
            return true;
        }

        private void ResolveSceneBootstrap()
        {
            if (startupBootstrapOverride is ICombatSceneBootstrap explicitBootstrap)
            {
                _sceneBootstrap = explicitBootstrap;
                return;
            }

            if (controller != null)
            {
                var localBehaviours = controller.GetComponents<MonoBehaviour>();
                for (int i = 0; i < localBehaviours.Length; i++)
                {
                    if (localBehaviours[i] is ICombatSceneBootstrap bootstrap)
                    {
                        _sceneBootstrap = bootstrap;
                        return;
                    }
                }
            }

            var allBehaviours = FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                if (allBehaviours[i] is ICombatSceneBootstrap bootstrap)
                {
                    _sceneBootstrap = bootstrap;
                    return;
                }
            }
        }
    }
}
