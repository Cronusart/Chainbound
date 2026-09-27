using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Transaction lifecycle orchestrator.
    /// Wraps IWalletService + IRpcService into a higher-level flow:
    ///   Build â†’ Sign â†’ Submit â†’ Confirm â†’ Done/Retry.
    ///
    /// Designed for:
    ///   - Idempotent tx handling (same tx ID won't double-submit)
    ///   - Retry-safe (automatic retries with back-off)
    ///   - Resume after app background / reconnect (persists pending tx state)
    ///   - Mobile-safe (non-blocking async with cancellation)
    ///
    /// Future implementation will integrate the Anchor escrow program IDL.
    /// </summary>
    public interface IChainTxOrchestrator
    {
        /// <summary>
        /// Submit a transaction end-to-end: sign, send, and confirm.
        /// The <paramref name="idempotencyKey"/> prevents double-submission if called again
        /// with the same key (returns the existing result instead of re-submitting).
        /// </summary>
        /// <param name="idempotencyKey">Caller-supplied unique key for this logical operation.</param>
        /// <param name="transactionBytes">Unsigned serialized transaction.</param>
        /// <returns>Result containing the tx signature and confirmation status.</returns>
        Task<TxResult> SubmitAsync(string idempotencyKey, byte[] transactionBytes);

        /// <summary>
        /// Resume confirmation of a previously submitted but unconfirmed transaction.
        /// Call this on app resume / reconnect.
        /// </summary>
        /// <param name="txSignature">The signature from a prior SubmitAsync call.</param>
        Task<TxResult> ResumeConfirmationAsync(string txSignature);

        /// <summary>
        /// Check whether an idempotency key has already been submitted and is pending or complete.
        /// </summary>
        bool HasPendingTx(string idempotencyKey);

        /// <summary>
        /// Get the result of a previously submitted transaction by its idempotency key.
        /// Returns null if no tx exists for that key.
        /// </summary>
        TxResult GetCachedResult(string idempotencyKey);
    }

    /// <summary>
    /// Result of a transaction submission + confirmation cycle.
    /// </summary>
    [Serializable]
    public class TxResult
    {
        /// <summary>Transaction signature (base58). Null if signing was rejected.</summary>
        public string txSignature;

        /// <summary>Current status of the transaction.</summary>
        public TxStatus status;

        /// <summary>Human-readable error message if failed.</summary>
        public string errorMessage;

        /// <summary>Timestamp (UTC ticks) when this result was last updated.</summary>
        public long lastUpdatedTicks;
    }

    /// <summary>
    /// Lifecycle status of a submitted transaction.
    /// </summary>
    public enum TxStatus
    {
        /// <summary>Transaction has not been submitted yet.</summary>
        NotStarted,

        /// <summary>Transaction is being signed by the wallet.</summary>
        Signing,

        /// <summary>Transaction has been sent to the cluster, awaiting confirmation.</summary>
        Submitted,

        /// <summary>Transaction has been confirmed on-chain.</summary>
        Confirmed,

        /// <summary>Transaction has reached finalized commitment level.</summary>
        Finalized,

        /// <summary>Transaction failed (see errorMessage).</summary>
        Failed,

        /// <summary>User rejected the signing prompt.</summary>
        Rejected,
    }
}
