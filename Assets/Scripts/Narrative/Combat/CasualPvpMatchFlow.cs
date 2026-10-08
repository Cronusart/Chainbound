using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Chainbound.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SignalJam.Narrative.Combat
{
    /// <summary>
    /// Loads the existing combat scene when a casual match locks, then returns to the previous scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CasualPvpMatchFlow : MonoBehaviour
    {
        public const string BattleSceneName = "Combat2_TestHarness";

        [SerializeField] private SupabaseMatchmakingService matchmaking;

        private string returnScene;
        private bool returnArmed;
        private bool pausedAway;

        private void Start()
        {
            StartCoroutine(RejoinWhenReady());
        }

        private void OnEnable()
        {
            if (matchmaking == null)
                matchmaking = GetComponent<SupabaseMatchmakingService>();
            if (matchmaking != null)
                matchmaking.OnMatchFound += HandleMatchFound;
        }

        private void OnDisable()
        {
            if (matchmaking != null)
                matchmaking.OnMatchFound -= HandleMatchFound;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void HandleMatchFound(MatchFoundPayload payload)
        {
            if (payload == null)
                return;

            returnArmed = false;
            returnScene = SceneManager.GetActiveScene().name;
            CasualPvpLaunchContext.Publish(new CasualPvpLaunchPayload
            {
                MatchId = payload.MatchId,
                Seed = payload.Seed,
                LocalTeamIndex = payload.LocalTeamIndex,
                Seat = string.IsNullOrWhiteSpace(payload.Seat) ? (payload.LocalTeamIndex == 1 ? "b" : "a") : payload.Seat,
                RulesetId = payload.RulesetId,
                Ruleset = payload.Ruleset,
                PartyA = payload.PartyA,
                PartyB = payload.PartyB,
                TurnBridge = matchmaking,
                History = payload.History,
                FullHeal = payload.FullHeal,
                PendingTurnIndex = payload.PendingTurnIndex,
                PendingLocalActions = payload.PendingLocalActions,
            });

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (SceneLoader.Instance == null)
            {
                Debug.LogWarning("[PvPMatch] SceneLoader is missing. Start through Bootstrap.");
                return;
            }

            Debug.Log($"[PvPMatch] Loading {BattleSceneName} for match {payload.MatchId}.");
            SceneLoader.Instance.RequestTransition(new SceneTransitionRequest(
                BattleSceneName,
                sourceId: "CasualPvp"));
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, BattleSceneName, StringComparison.OrdinalIgnoreCase))
                return;

            CombatBattleController controller = FindFirstObjectByType<CombatBattleController>(FindObjectsInactive.Include);
            if (controller == null)
                return;

            controller.OnBattleEnded -= HandleBattleEnded;
            controller.OnBattleEnded += HandleBattleEnded;
        }

        private void HandleBattleEnded(BattleResult result)
        {
            if (result == null || !result.casualSpineMatch || returnArmed)
                return;
            if (string.IsNullOrWhiteSpace(returnScene) || SceneLoader.Instance == null)
                return;

            returnArmed = true;
            Debug.Log($"[PvPMatch] Returning to {returnScene}.");
            SceneLoader.Instance.RequestTransition(new SceneTransitionRequest(
                returnScene,
                sourceId: "CasualPvpReturn"));
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                pausedAway = true;
                CancelQueueIfSearching();
                return;
            }

            if (!pausedAway)
                return;
            pausedAway = false;
            StartCoroutine(RejoinWhenReady());
        }

        private void OnApplicationQuit()
        {
            CancelQueueIfSearching();
        }

        private void OnDestroy()
        {
            CancelQueueIfSearching();
        }

        private void CancelQueueIfSearching()
        {
            if (matchmaking == null || !matchmaking.IsSearching)
                return;
            _ = matchmaking.CancelAsync(CancellationToken.None);
        }

        private IEnumerator RejoinWhenReady()
        {
            yield return null;
            if (matchmaking == null)
                matchmaking = GetComponent<SupabaseMatchmakingService>();
            if (matchmaking == null || matchmaking.IsSearching)
                yield break;
            if (string.Equals(SceneManager.GetActiveScene().name, BattleSceneName, StringComparison.OrdinalIgnoreCase))
                yield break;

            Task<bool> rejoin = matchmaking.TryRejoinActiveMatchAsync(CancellationToken.None);
            while (!rejoin.IsCompleted)
                yield return null;
        }
    }
}
