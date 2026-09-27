using System;

namespace Chainbound.Combat
{
    public enum ProgressionGrantEventType
    {
        StarUp,
        MasteryCycle,
    }

    public enum ProgressionGrantOutcome
    {
        IssuancePending,
        FolioIssued,
        NoFolioIssued,
        IssuanceError,
    }

    [Serializable]
    public class ProgressionGrantResult
    {
        public string chainlingBackendId;
        public string instanceId;
        public string speciesId;
        public string chainlingDisplayName;
        public ProgressionGrantEventType eventType;
        public int starIndex;
        public int masteryCycleIndex;
        public int gatePercent;
        public ProgressionGrantOutcome outcome = ProgressionGrantOutcome.IssuancePending;
        public bool gatePassed;
        public bool poolHadEligibleMoves;
        public string moveId;
        public string moveName;
        public string sourceEventId;
        public string idempotencyKey;
        public bool grantSucceeded;
        public bool alreadyApplied;
        public string moveItemId;
        public int ownedCopyCount;
        public string error;

        public bool AwardedMove =>
            outcome == ProgressionGrantOutcome.FolioIssued &&
            gatePassed &&
            grantSucceeded &&
            !string.IsNullOrWhiteSpace(moveId);
    }

    [Serializable]
    public class ProgressionGrantRequest
    {
        public string playerProfileId;
        public string walletAddress;
        public string authUserId;
        public string chainlingBackendId;
        public string moveId;
        public string sourceEventId;
        public string idempotencyKey;
        public ProgressionGrantEventType eventType;
        public int starIndex;
        public int masteryCycleIndex;
        public int battleSeed;
    }

    [Serializable]
    public class ProgressionGrantPersistenceResult
    {
        public bool success;
        public bool alreadyApplied;
        public string moveItemId;
        public string moveId;
        public int ownedCopyCount;
        public string error;

        public static ProgressionGrantPersistenceResult Failed(string error)
        {
            return new ProgressionGrantPersistenceResult
            {
                success = false,
                error = error ?? "",
            };
        }
    }

    public interface IProgressionGrantPersistence
    {
        bool IsAvailable { get; }
        System.Threading.Tasks.Task<ProgressionGrantPersistenceResult> GrantProgressionKnownMoveAsync(ProgressionGrantRequest request);
    }
}
