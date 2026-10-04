using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    public interface IChainTxOrchestrator
    {
        Task<RpcSimulationResult> SimulateAsync(ChainTxRequest request);
        Task<TxResult> SubmitAsync(ChainTxRequest request);
        Task<TxResult> ResumeAsync(ChainTxScope scope);
        PendingTxRecord Query(ChainTxScope scope);

        // Compatibility surface for callers being migrated to scoped requests.
        Task<TxResult> SubmitAsync(string idempotencyKey, byte[] transactionBytes);
        Task<TxResult> ResumeConfirmationAsync(string txSignature);
        bool HasPendingTx(string idempotencyKey);
        TxResult GetCachedResult(string idempotencyKey);
    }

    [Serializable]
    public sealed class ChainTxScope
    {
        public string cluster;
        public string wallet;
        public string operationId;
        public string orderId;

        public ChainTxScope(string cluster, string wallet, string operationId = null, string orderId = null)
        {
            this.cluster = cluster ?? string.Empty;
            this.wallet = wallet ?? string.Empty;
            this.operationId = operationId ?? string.Empty;
            this.orderId = orderId ?? string.Empty;
        }

        public string Key => string.Join("|", cluster, wallet, operationId, orderId);
    }

    [Serializable]
    public sealed class ChainTxRequest
    {
        public ChainTxScope scope;
        public byte[] transactionBytes;
        public bool requiresSignAndSend;

        public ChainTxRequest(ChainTxScope scope, byte[] transactionBytes, bool requiresSignAndSend)
        {
            this.scope = scope;
            this.transactionBytes = transactionBytes;
            this.requiresSignAndSend = requiresSignAndSend;
        }
    }

    [Serializable]
    public sealed class PendingTxRecord
    {
        public int version = 1;
        public string cluster;
        public string wallet;
        public string operationId;
        public string orderId;
        public string signature;
        public string messageHash;
        public TxStatus status;
        public string errorMessage;
        public long lastUpdatedTicks;

        public string ScopeKey => string.Join("|", cluster ?? string.Empty, wallet ?? string.Empty,
            operationId ?? string.Empty, orderId ?? string.Empty);
    }

    [Serializable]
    public class TxResult
    {
        public string txSignature;
        public TxStatus status;
        public string errorMessage;
        public long lastUpdatedTicks;
        public ChainTxScope scope;
        public RpcSimulationResult simulation;
    }

    public enum TxStatus
    {
        NotStarted,
        Simulating,
        AwaitingWallet,
        Signing,
        Submitted,
        Confirming,
        Confirmed,
        Finalized,
        UnknownOutcome,
        Failed,
        Rejected,
    }
}
