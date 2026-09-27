using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Editor/test entropy source. It mirrors IVrfService fulfillment semantics while deriving
    /// repeatable bytes from an explicit seed and request context. It owns no stamp rules.
    /// </summary>
    public sealed class DeterministicVrfService : IVrfService
    {
        private readonly byte[] baseSeed;
        private readonly Dictionary<string, VrfRequestResult> fulfilled =
            new Dictionary<string, VrfRequestResult>(StringComparer.Ordinal);

        public DeterministicVrfService(int seed)
        {
            baseSeed = BitConverter.GetBytes(seed);
        }

        public bool HasPendingRequest => false;
        public string PendingRequestId => null;

        public Task<VrfRequestResult> RequestRandomnessAsync(byte[] seedContext)
        {
            byte[] context = seedContext ?? Array.Empty<byte>();
            byte[] input = new byte[baseSeed.Length + context.Length];
            Buffer.BlockCopy(baseSeed, 0, input, 0, baseSeed.Length);
            if (context.Length > 0)
                Buffer.BlockCopy(context, 0, input, baseSeed.Length, context.Length);

            byte[] randomness;
            using (SHA256 sha = SHA256.Create())
                randomness = sha.ComputeHash(input);

            string requestId = BitConverter.ToString(randomness).Replace("-", string.Empty).ToLowerInvariant();
            var result = new VrfRequestResult
            {
                requestId = requestId,
                txSignature = $"deterministic-{requestId}",
                status = VrfRequestStatus.Fulfilled,
                randomness = randomness,
            };
            fulfilled[requestId] = result;
            return Task.FromResult(result);
        }

        public Task<VrfRequestResult> PollRandomnessAsync(
            string requestId,
            int pollIntervalMs = 2000,
            int timeoutMs = 30000)
        {
            if (!string.IsNullOrWhiteSpace(requestId) && fulfilled.TryGetValue(requestId, out VrfRequestResult result))
                return Task.FromResult(result);
            return Task.FromResult(new VrfRequestResult
            {
                requestId = requestId,
                status = VrfRequestStatus.Failed,
                errorMessage = "Unknown deterministic VRF request.",
            });
        }

        public Task<VrfRequestResult> ConsumeRandomnessAsync(string requestId)
        {
            if (string.IsNullOrWhiteSpace(requestId)
                || !fulfilled.TryGetValue(requestId, out VrfRequestResult result)
                || result.status != VrfRequestStatus.Fulfilled)
            {
                return Task.FromResult(new VrfRequestResult
                {
                    requestId = requestId,
                    status = VrfRequestStatus.Failed,
                    errorMessage = "Deterministic VRF request is not fulfillable or was already consumed.",
                });
            }

            result.status = VrfRequestStatus.Consumed;
            return Task.FromResult(result);
        }

        public Task<VrfRequestResult> ResumePendingAsync() => Task.FromResult(new VrfRequestResult
        {
            status = VrfRequestStatus.Failed,
            errorMessage = "Deterministic VRF requests fulfill immediately.",
        });
    }
}
