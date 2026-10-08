using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public sealed class MatchQueueRequest
    {
        public string ProfileId;
        public string QueueType = "casual";
        public string RulesetId;
        public string ConfigHash;
        public string AppVersion;
    }

    public sealed class MatchmakingEnqueueResult
    {
        public bool Accepted;
        public bool Waiting;
        public string Error;
    }

    public sealed class MatchFoundPayload
    {
        public string MatchId;
        public int Seed;
        public int LocalTeamIndex;
        public string Seat;
        public string RulesetId;
        public CombatRulesetSO Ruleset;
        public PartySnapshot PartyA;
        public PartySnapshot PartyB;
        public System.Collections.Generic.List<CasualPvpCommittedTurn> History;
        public bool FullHeal = true;
        public int PendingTurnIndex;
        public System.Collections.Generic.List<CombatAction> PendingLocalActions;
    }

    public sealed class CasualPvpCommittedTurn
    {
        public int TurnIndex;
        public System.Collections.Generic.List<CombatAction> ActionsA;
        public System.Collections.Generic.List<CombatAction> ActionsB;
    }

    public sealed class PvpTurnCommit
    {
        public bool Committed;
        public bool Forfeited;
        public string State;
        public string Error;
        public int TurnIndex;
        public System.Collections.Generic.List<CombatAction> ActionsA;
        public System.Collections.Generic.List<CombatAction> ActionsB;
        public System.Collections.Generic.List<CombatAction> PendingLocalActions;
    }

    public sealed class PvpResultCommit
    {
        public bool Accepted;
        public string State;
        public string Error;
        public string ForensicHashA;
        public string ForensicHashB;
        public string WinnerSeat;
    }

    public interface IMatchmakingService
    {
        bool IsSearching { get; }
        string LastError { get; }
        Task<MatchmakingEnqueueResult> EnqueueAsync(MatchQueueRequest request, CancellationToken cancellationToken);
        Task CancelAsync(CancellationToken cancellationToken);
        event Action<MatchFoundPayload> OnMatchFound;
    }

    public interface IPvPTurnBridge
    {
        Task<PvpTurnCommit> SubmitTurnAndWaitAsync(
            string matchId,
            string seat,
            int turnIndex,
            System.Collections.Generic.IReadOnlyList<CombatAction> localActions,
            CancellationToken cancellationToken);

        Task<PvpResultCommit> SubmitResultAsync(
            string matchId,
            string forensicHash,
            int winnerTeam,
            CancellationToken cancellationToken);

        Task<PvpTurnCommit> PollMatchAsync(string matchId, CancellationToken cancellationToken);
    }
}
