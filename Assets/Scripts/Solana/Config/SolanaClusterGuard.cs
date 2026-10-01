using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Verifies the configured RPC endpoint against its expected genesis hash before
    /// wallet authorization or transaction signing. Successful checks are cached for
    /// this process; failures are never cached.
    /// </summary>
    public static class SolanaClusterGuard
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, Task> Pending = new Dictionary<string, Task>(StringComparer.Ordinal);

        public static async Task EnsureExpectedClusterAsync(SolanaConfigSO config)
        {
            config = PlatformContext.ResolveSolanaConfig(config);
            if (config == null)
                throw new InvalidOperationException("Solana deployment configuration is unavailable.");

            string endpoint = config.rpcEndpoint?.Trim();
            string expected = config.expectedGenesisHash?.Trim();
            if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(expected))
                throw new InvalidOperationException("Solana RPC endpoint and expected genesis hash must be configured.");

            string key = endpoint + "|" + expected;
            Task verification;
            lock (Sync)
            {
                if (!Pending.TryGetValue(key, out verification))
                {
                    verification = VerifyAsync(endpoint, expected);
                    Pending[key] = verification;
                }
            }

            try
            {
                await verification;
            }
            catch
            {
                lock (Sync)
                    Pending.Remove(key);
                throw;
            }
        }

        private static async Task VerifyAsync(string endpoint, string expected)
        {
            using (var rpc = new SolanaRpcService(endpoint))
            {
                string actual = await rpc.GetGenesisHashAsync();
                if (!string.Equals(actual, expected, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Solana RPC cluster mismatch. Expected genesis {expected}, received {actual}. Wallet access is blocked.");
                }
            }
        }
    }
}
