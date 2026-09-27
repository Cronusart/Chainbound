using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Status of a VRF randomness request.
    /// </summary>
    public enum VrfRequestStatus
    {
        /// <summary>Request has been submitted, waiting for fulfillment.</summary>
        Pending,

        /// <summary>Randomness has been fulfilled and is available.</summary>
        Fulfilled,

        /// <summary>Randomness has been consumed by the program.</summary>
        Consumed,

        /// <summary>Request failed or timed out.</summary>
        Failed,
    }

    /// <summary>
    /// Result of a VRF randomness request.
    /// </summary>
    [Serializable]
    public class VrfRequestResult
    {
        /// <summary>Unique request ID (on-chain PDA address or similar).</summary>
        public string requestId;

        /// <summary>Transaction signature for the request.</summary>
        public string txSignature;

        /// <summary>Current status.</summary>
        public VrfRequestStatus status;

        /// <summary>The 32-byte randomness output. Null until fulfilled.</summary>
        public byte[] randomness;

        /// <summary>Error message if failed.</summary>
        public string errorMessage;
    }

    /// <summary>
    /// Service for requesting and consuming MagicBlock VRF randomness.
    /// Uses request + callback consume model.
    /// </summary>
    public interface IVrfService
    {
        /// <summary>
        /// Submit a VRF randomness request on-chain.
        /// Returns the request ID and tx signature.
        /// </summary>
        /// <param name="seedContext">Application-specific seed (e.g., zone_id + encounter_table_id).</param>
        Task<VrfRequestResult> RequestRandomnessAsync(byte[] seedContext);

        /// <summary>
        /// Poll the status of a VRF request until fulfilled or failed.
        /// Uses debounced polling (no per-frame calls).
        /// </summary>
        /// <param name="requestId">The request ID from RequestRandomnessAsync.</param>
        /// <param name="pollIntervalMs">Milliseconds between polls (default: 2000).</param>
        /// <param name="timeoutMs">Maximum wait time in ms (default: 30000).</param>
        Task<VrfRequestResult> PollRandomnessAsync(
            string requestId,
            int pollIntervalMs = 2000,
            int timeoutMs = 30000);

        /// <summary>Atomically transition a fulfilled request to Consumed. A second call must fail.</summary>
        Task<VrfRequestResult> ConsumeRandomnessAsync(string requestId);

        /// <summary>
        /// Check if there is a pending VRF request that needs to be resumed.
        /// </summary>
        bool HasPendingRequest { get; }

        /// <summary>
        /// The pending request ID, if any.
        /// </summary>
        string PendingRequestId { get; }

        /// <summary>
        /// Resume a pending VRF request (e.g., after app backgrounding).
        /// </summary>
        Task<VrfRequestResult> ResumePendingAsync();
    }
}
