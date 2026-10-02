using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Builds the turn a capture preview plays: the lead records the first wild
    /// target, every other actor passes, and the opposing side never attacks.
    /// </summary>
    public static class CombatCapturePreviewActions
    {
        public const string PreviewOrbItemId = "signal_orb";

        public static List<CombatAction> BuildPlayerCaptureTurn(CombatState state, string orbItemId = PreviewOrbItemId)
        {
            var actions = new List<CombatAction>();
            TeamState player = state != null ? state.team0 : null;
            if (player == null || player.activeIndices == null)
                return actions;

            string targetId = FirstAliveInstanceId(state.team1);
            bool captureQueued = false;
            for (int slot = 0; slot < player.activeIndices.Count; slot++)
            {
                ChainlingInstance actor = player.GetActiveInSlot(slot);
                if (actor != null && actor.isAlive && !captureQueued && !string.IsNullOrEmpty(targetId))
                {
                    actions.Add(CombatAction.Capture(0, targetId, orbItemId));
                    captureQueued = true;
                    continue;
                }

                actions.Add(CombatAction.Pass(0));
            }

            return actions;
        }

        public static List<CombatAction> BuildOpponentPasses(CombatState state)
        {
            TeamState opponent = state != null ? state.team1 : null;
            int count = opponent != null && opponent.activeIndices != null
                ? opponent.activeIndices.Count
                : 0;
            var actions = new List<CombatAction>(Mathf.Max(1, count));
            int passes = Mathf.Max(1, count);
            for (int i = 0; i < passes; i++)
                actions.Add(CombatAction.Pass(1));
            return actions;
        }

        private static string FirstAliveInstanceId(TeamState team)
        {
            if (team == null || team.activeIndices == null)
                return "";

            for (int slot = 0; slot < team.activeIndices.Count; slot++)
            {
                ChainlingInstance actor = team.GetActiveInSlot(slot);
                if (actor != null && actor.isAlive && !string.IsNullOrEmpty(actor.instanceId))
                    return actor.instanceId;
            }

            return "";
        }
    }

    /// <summary>
    /// Scene harness for <c>Combat2_TestHarness</c>. On the first player turn it
    /// queues a capture, the wild side passes, and the practice attempt locks so
    /// the dossier and post-battle panels can be reviewed without a fight.
    /// Disable this component to play the harness normally.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatCapturePreviewHarness : MonoBehaviour
    {
        [SerializeField] private CombatBattleController controller;
        [SerializeField] private CaptureCommandCoordinatorUGUI coordinator;
        [SerializeField, Min(0f)] private float capturePanelHoldSeconds = 0.7f;
        [SerializeField] private string orbItemId = CombatCapturePreviewActions.PreviewOrbItemId;

        private bool subscribed;

        private void Awake()
        {
            if (controller == null)
                controller = GetComponent<CombatBattleController>();
        }

        private void OnEnable()
        {
            if (controller == null)
                controller = GetComponent<CombatBattleController>();
            if (controller == null)
            {
                Debug.LogError("[CapturePreview] No CombatBattleController assigned.", this);
                return;
            }

            if (!controller.UsePlayerInput)
                Debug.LogWarning("[CapturePreview] usePlayerInput is off, so this harness cannot submit the capture turn.", controller);

            controller.SetCapturePreviewMode(true);
            if (!subscribed)
            {
                controller.OnPlayerTurnWaiting += HandlePlayerTurnWaiting;
                subscribed = true;
            }

            ResolveCoordinator()?.SetPracticePreviewLock(true, capturePanelHoldSeconds);
        }

        private void OnDisable()
        {
            if (subscribed && controller != null)
            {
                controller.OnPlayerTurnWaiting -= HandlePlayerTurnWaiting;
                subscribed = false;
            }

            if (controller != null)
                controller.SetCapturePreviewMode(false);
            if (coordinator != null)
                coordinator.SetPracticePreviewLock(false);
        }

        private void HandlePlayerTurnWaiting(CombatState state, int teamIndex)
        {
            if (teamIndex != 0 || controller == null || state == null || !state.isActive)
                return;

            ResolveCoordinator()?.SetPracticePreviewLock(true, capturePanelHoldSeconds);
            List<CombatAction> actions = CombatCapturePreviewActions.BuildPlayerCaptureTurn(state, orbItemId);
            if (actions.Count == 0)
            {
                Debug.LogWarning("[CapturePreview] No player slots to capture with.", this);
                return;
            }

            Debug.Log("[CapturePreview] Queuing capture. The opposing side will pass.", this);
            controller.SubmitPlayerActions(actions);
        }

        private CaptureCommandCoordinatorUGUI ResolveCoordinator()
        {
            if (coordinator == null)
                coordinator = FindFirstObjectByType<CaptureCommandCoordinatorUGUI>(FindObjectsInactive.Include);
            return coordinator;
        }
    }
}
