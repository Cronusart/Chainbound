using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Casual queue and turn bridge. Writes go through edge functions only.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupabaseMatchmakingService : MonoBehaviour, IMatchmakingService, IPvPTurnBridge
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private CombatRulesetSO casualRuleset;
        [SerializeField] private float pollSeconds = 1f;

        public static SupabaseMatchmakingService Instance { get; private set; }

        public bool IsSearching { get; private set; }
        public string LastError { get; private set; }
        public string ActiveMatchId { get; private set; }
        public string ActiveSeat { get; private set; }

        public event Action<MatchFoundPayload> OnMatchFound;

        private CancellationTokenSource searchCancel;
        private readonly object gate = new object();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PvPMatch] Duplicate SupabaseMatchmakingService.");
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            CancelSearch();
            if (Instance == this)
                Instance = null;
        }

        public void Configure(SupabaseConfigSO config, CombatRulesetSO ruleset = null)
        {
            if (config != null)
                supabaseConfig = config;
            if (ruleset != null)
                casualRuleset = ruleset;
        }

        public async Task<MatchmakingEnqueueResult> EnqueueAsync(MatchQueueRequest request, CancellationToken cancellationToken)
        {
            LastError = string.Empty;
            if (MatchmakingConfigService.Instance == null || !await MatchmakingConfigService.Instance.RefreshAsync())
            {
                if (MatchmakingConfigService.Instance != null && !MatchmakingConfigService.Instance.ServerCasualEnabled)
                    return Fail("casual_disabled");
                string reason = MatchmakingConfigService.Instance != null
                    ? MatchmakingConfigService.Instance.LastError
                    : "server_unreachable";
                return Fail(string.IsNullOrEmpty(reason) ? "server_unreachable" : reason);
            }

            MatchmakingConfigSO board = BalanceConfigRuntime.Matchmaking;
            if (board == null || !board.casualEnabled || !MatchmakingConfigService.Instance.ServerCasualEnabled)
                return Fail("casual_disabled");
            string serverHash = MatchmakingConfigService.Instance.ServerConfigHash;
            string localHash = board.ComputeConfigHash();
            if (!string.Equals(localHash, serverHash, StringComparison.OrdinalIgnoreCase))
                return Fail("config_mismatch");

            if (board.requireWalletForCasual)
            {
                PlayerAccountSession session = AccountSessionService.Instance != null
                    ? AccountSessionService.Instance.CurrentSession
                    : null;
                if (session == null || !session.isWalletConnected || !session.HasFreshWalletProof)
                    return Fail("wallet_required");
            }

            string profileId = request != null ? request.ProfileId : string.Empty;
            if (string.IsNullOrWhiteSpace(profileId) && AccountSessionService.Instance != null)
                profileId = AccountSessionService.Instance.CurrentSession.PlayerProfileId;
            if (string.IsNullOrWhiteSpace(profileId))
                return Fail("profile_required");

            string appVersion = request != null && !string.IsNullOrWhiteSpace(request.AppVersion)
                ? request.AppVersion.Trim()
                : Application.version;
            if (string.IsNullOrWhiteSpace(appVersion))
                appVersion = "editor";

            CombatResolverConfigSO resolverConfig = casualRuleset != null
                ? casualRuleset.ResolveResolverConfig()
                : BalanceConfigRuntime.ResolverConfig;
            string resolverHash = BalanceConfigHashUtility.HashResolverConfig(resolverConfig);
            string rulesetHash = BalanceConfigHashUtility.HashRuleset(casualRuleset, resolverHash);
            string buildStamp = PvpBuildStamp.Current;

            if (searchCancel != null)
            {
                searchCancel.Cancel();
                searchCancel.Dispose();
            }
            searchCancel = new CancellationTokenSource();
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(searchCancel.Token, cancellationToken);
            IsSearching = true;

            PvpEdgeResponse queued = await PostAsync("pvp_enqueue", new PvpEnqueueBody
            {
                player_profile_id = profileId,
                queue_type = "casual",
                ruleset_id = board.defaultCasualRulesetId,
                config_hash = serverHash,
                app_version = appVersion,
                resolver_hash = resolverHash,
                ruleset_hash = rulesetHash,
                build_stamp = buildStamp,
            }, linked.Token);

            if (queued == null)
                return Fail("server_unreachable");
            if (!queued.success && queued.error == "match_in_progress" && TryPublishMatch(queued))
            {
                IsSearching = false;
                return new MatchmakingEnqueueResult { Accepted = true, Waiting = false };
            }
            if (!queued.success)
                return Fail(string.IsNullOrEmpty(queued.error) ? "enqueue_rejected" : queued.error);

            if (TryPublishMatch(queued))
            {
                IsSearching = false;
                return new MatchmakingEnqueueResult { Accepted = true, Waiting = false };
            }

            try
            {
                while (!linked.IsCancellationRequested)
                {
                    await Task.Delay(Mathf.Max(250, Mathf.RoundToInt(pollSeconds * 1000f)), linked.Token);
                    PvpEdgeResponse polled = await PostAsync("pvp_get_match", new PvpMatchBody
                    {
                        player_profile_id = profileId,
                        match_id = string.Empty,
                    }, linked.Token);
                    if (polled == null)
                        continue;
                    if (!polled.success && !string.IsNullOrEmpty(polled.error) && polled.error != "not_waiting")
                    {
                        return Fail(polled.error);
                    }

                    if (polled.phase == "waiting" || polled.state == "created")
                        continue;

                    if (TryPublishMatch(polled))
                    {
                        IsSearching = false;
                        return new MatchmakingEnqueueResult { Accepted = true, Waiting = false };
                    }

                    IsSearching = false;
                    string expired = polled.phase == "expired" || polled.phase == "idle"
                        ? "search_expired"
                        : (string.IsNullOrEmpty(polled.error) ? polled.state : polled.error);
                    LastError = string.IsNullOrEmpty(expired) ? "search_expired" : expired;
                    return new MatchmakingEnqueueResult { Accepted = false, Error = LastError };
                }
            }
            catch (Exception) when (linked.IsCancellationRequested)
            {
                IsSearching = false;
                return new MatchmakingEnqueueResult { Accepted = false, Error = "cancelled" };
            }

            IsSearching = false;
            return new MatchmakingEnqueueResult { Accepted = true, Waiting = true };
        }

        public async Task CancelAsync(CancellationToken cancellationToken)
        {
            CancelSearch();
            IsSearching = false;
            string profileId = AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession.PlayerProfileId
                : string.Empty;
            if (string.IsNullOrWhiteSpace(profileId) || supabaseConfig == null)
                return;

            await PostAsync("pvp_cancel_queue", new PvpMatchBody
            {
                player_profile_id = profileId,
            }, cancellationToken);
            Debug.Log("[PvPMatch] Queue cancel requested.");
        }

        public async Task<PvpTurnCommit> SubmitTurnAndWaitAsync(
            string matchId,
            string seat,
            int turnIndex,
            IReadOnlyList<CombatAction> localActions,
            CancellationToken cancellationToken)
        {
            string profileId = CurrentProfileId();
            string actionsJson = PvpPartyMapper.ToActionJson(localActions);
            int delayMs = 500;
            while (!cancellationToken.IsCancellationRequested)
            {
                PvpEdgeResponse submitted = await PostAsync("pvp_submit_turn", new PvpTurnBody
                {
                    player_profile_id = profileId,
                    match_id = matchId,
                    seat = seat,
                    turn_index = turnIndex,
                    actions_json = actionsJson,
                }, cancellationToken);

                if (submitted != null)
                {
                    PvpTurnCommit immediate = ToTurnCommit(submitted, turnIndex);
                    if (immediate.Committed || immediate.Forfeited)
                        return immediate;
                    if (!submitted.success && !IsRetryableTurnError(submitted))
                        return immediate;
                    if (!submitted.success)
                    {
                        Debug.Log($"[PvPTurn] reconnecting match={matchId} turn={turnIndex}.");
                        await Task.Delay(delayMs, cancellationToken);
                        delayMs = Math.Min(delayMs * 2, 8000);
                    }
                }
                else
                {
                    Debug.Log($"[PvPTurn] reconnecting match={matchId} turn={turnIndex}.");
                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, 8000);
                }

                PvpEdgeResponse polled = await PostAsync("pvp_get_match", new PvpMatchBody
                {
                    player_profile_id = profileId,
                    match_id = matchId,
                }, cancellationToken);
                if (polled == null)
                {
                    await Task.Delay(Mathf.Max(250, Mathf.RoundToInt(pollSeconds * 1000f)), cancellationToken);
                    continue;
                }

                PvpTurnCommit commit = ToTurnCommit(polled, turnIndex);
                if (commit.Committed || commit.Forfeited || PvpPartyMapper.IsTerminal(polled.state))
                    return commit;
                if (!string.IsNullOrEmpty(polled.error) && !IsRetryableTurnError(polled))
                    return commit;

                await Task.Delay(Mathf.Max(250, Mathf.RoundToInt(pollSeconds * 1000f)), cancellationToken);
            }

            return new PvpTurnCommit { Error = "cancelled" };
        }

        public async Task<bool> TryRejoinActiveMatchAsync(CancellationToken cancellationToken)
        {
            PvpEdgeResponse snapshot = await PostAsync("pvp_get_match", new PvpMatchBody
            {
                player_profile_id = CurrentProfileId(),
                match_id = string.Empty,
            }, cancellationToken);
            return TryPublishMatch(snapshot, republish: true);
        }

        public async Task<PvpTurnCommit> PollMatchAsync(string matchId, CancellationToken cancellationToken)
        {
            PvpEdgeResponse polled = await PostAsync("pvp_get_match", new PvpMatchBody
            {
                player_profile_id = CurrentProfileId(),
                match_id = matchId,
            }, cancellationToken);
            if (polled == null)
                return new PvpTurnCommit { Error = "server_unreachable" };
            return ToTurnCommit(polled, polled.turn_index);
        }

        public async Task<PvpResultCommit> SubmitResultAsync(
            string matchId,
            string forensicHash,
            int winnerTeam,
            CancellationToken cancellationToken)
        {
            PvpEdgeResponse response = await PostAsync("pvp_submit_result", new PvpResultBody
            {
                player_profile_id = CurrentProfileId(),
                match_id = matchId,
                forensic_hash = forensicHash ?? string.Empty,
                winner_team = winnerTeam,
            }, cancellationToken);
            if (response == null)
                return new PvpResultCommit { Error = "server_unreachable" };

            int window = BalanceConfigRuntime.Matchmaking != null
                ? BalanceConfigRuntime.Matchmaking.resultWindowSeconds
                : 45;
            float elapsed = 0f;
            float budget = window + 15f;
            while (!PvpPartyMapper.IsTerminal(response.state) && elapsed < budget && !cancellationToken.IsCancellationRequested)
            {
                int delayMs = Mathf.Max(250, Mathf.RoundToInt(pollSeconds * 1000f));
                await Task.Delay(delayMs, cancellationToken);
                elapsed += delayMs / 1000f;
                PvpEdgeResponse polled = await PostAsync("pvp_get_match", new PvpMatchBody
                {
                    player_profile_id = CurrentProfileId(),
                    match_id = matchId,
                }, cancellationToken);
                if (polled == null)
                {
                    Debug.Log($"[PvPTurn] reconnecting match={matchId} result.");
                    continue;
                }

                response = polled;
            }

            Debug.Log($"[PvPMatch] Result {response.state} match={matchId}.");
            return new PvpResultCommit
            {
                Accepted = response.success,
                State = response.state,
                Error = response.error,
                ForensicHashA = response.forensic_hash_a,
                ForensicHashB = response.forensic_hash_b,
                WinnerSeat = response.winner_seat,
            };
        }

        private static bool IsRetryableTurnError(PvpEdgeResponse response)
        {
            if (response == null)
                return true;
            if (response.success)
                return false;
            return response.error == "turn_mismatch" ||
                   response.error == "server_unreachable" ||
                   response.error == "pvp_failed" ||
                   response.error == "empty_rpc" ||
                   string.IsNullOrEmpty(response.error);
        }

        private bool TryPublishMatch(PvpEdgeResponse response, bool republish = false)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.match_id))
                return false;
            if (response.phase != "matched")
                return false;
            if (response.state != "locked" && response.state != "in_progress")
                return false;
            if (response.seed == 0)
                return false;

            PartySnapshot partyA = PvpPartyMapper.ToParty(response.party_a_json);
            PartySnapshot partyB = PvpPartyMapper.ToParty(response.party_b_json);
            if (partyA.Count == 0 || partyB.Count == 0)
                return false;

            lock (gate)
            {
                if (!republish && ActiveMatchId == response.match_id)
                    return true;
                ActiveMatchId = response.match_id;
                ActiveSeat = response.seat;
            }

            var payload = new MatchFoundPayload
            {
                MatchId = response.match_id,
                Seed = response.seed,
                LocalTeamIndex = PvpPartyMapper.TeamIndexForSeat(response.seat),
                Seat = response.seat,
                RulesetId = string.IsNullOrWhiteSpace(response.ruleset_id) ? "pvp_casual" : response.ruleset_id,
                Ruleset = casualRuleset,
                PartyA = partyA,
                PartyB = partyB,
                History = PvpPartyMapper.ToHistory(response.turns_json),
                FullHeal = string.IsNullOrEmpty(response.hp_policy) || response.hp_policy == "full_heal",
                PendingTurnIndex = response.turn_index,
                PendingLocalActions = PvpPartyMapper.ToActions(response.pending_actions_json),
            };
            Debug.Log($"[PvPMatch] Match {payload.MatchId} seat={response.seat} seed={payload.Seed}.");
            OnMatchFound?.Invoke(payload);
            return true;
        }

        private static PvpTurnCommit ToTurnCommit(PvpEdgeResponse response, int turnIndex)
        {
            bool terminal = PvpPartyMapper.IsTerminal(response.state);
            bool forfeited = response.state == "forfeit_a" || response.state == "forfeit_b" || response.state == "cancelled";
            bool committed = response.committed_turn_index == turnIndex &&
                             !string.IsNullOrWhiteSpace(response.actions_a_json) &&
                             !string.IsNullOrWhiteSpace(response.actions_b_json);
            return new PvpTurnCommit
            {
                Committed = committed,
                Forfeited = forfeited || (terminal && !committed),
                State = response.state,
                Error = response.success ? string.Empty : response.error,
                TurnIndex = response.turn_index,
                ActionsA = committed ? PvpPartyMapper.ToActions(response.actions_a_json) : new List<CombatAction>(),
                ActionsB = committed ? PvpPartyMapper.ToActions(response.actions_b_json) : new List<CombatAction>(),
                PendingLocalActions = PvpPartyMapper.ToActions(response.pending_actions_json),
            };
        }

        private async Task<PvpEdgeResponse> PostAsync(string functionName, object body, CancellationToken cancellationToken)
        {
            if (supabaseConfig == null)
            {
                LastError = "server_unreachable";
                return null;
            }

            try
            {
                string json = JsonUtility.ToJson(body);
                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(supabaseConfig, functionName, json);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(response.body))
                {
                    LastError = "server_unreachable";
                    return null;
                }

                PvpEdgeResponse dto = JsonUtility.FromJson<PvpEdgeResponse>(response.body);
                if (dto == null)
                {
                    LastError = "server_unreachable";
                    return null;
                }

                if (!response.IsSuccess && string.IsNullOrEmpty(dto.error))
                    dto.error = "server_unreachable";
                if (!dto.success && !string.IsNullOrEmpty(dto.error))
                    LastError = dto.error;
                return dto;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LastError = "server_unreachable";
                Debug.LogWarning($"[PvPMatch] {functionName} failed: {ex.Message}");
                return null;
            }
        }

        private MatchmakingEnqueueResult Fail(string error)
        {
            IsSearching = false;
            LastError = error;
            CancelSearch();
            Debug.LogWarning($"[PvPMatch] Enqueue refused: {error}");
            return new MatchmakingEnqueueResult { Accepted = false, Error = error };
        }

        private void CancelSearch()
        {
            if (searchCancel == null)
                return;
            if (!searchCancel.IsCancellationRequested)
                searchCancel.Cancel();
        }

        private static string CurrentProfileId()
        {
            return AccountSessionService.Instance != null
                ? AccountSessionService.Instance.CurrentSession.PlayerProfileId
                : string.Empty;
        }

        [Serializable]
        private class PvpEnqueueBody
        {
            public string player_profile_id;
            public string queue_type;
            public string ruleset_id;
            public string config_hash;
            public string app_version;
            public string resolver_hash;
            public string ruleset_hash;
            public string build_stamp;
        }

        [Serializable]
        private class PvpMatchBody
        {
            public string player_profile_id;
            public string match_id;
        }

        [Serializable]
        private class PvpTurnBody
        {
            public string player_profile_id;
            public string match_id;
            public string seat;
            public int turn_index;
            public string actions_json;
        }

        [Serializable]
        private class PvpResultBody
        {
            public string player_profile_id;
            public string match_id;
            public string forensic_hash;
            public int winner_team;
        }
    }
}
