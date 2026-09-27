using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Abstraction over Solana RPC calls.
    /// All chain reads and transaction submissions go through this interface.
    /// Gameplay logic MUST NOT call RPC directly.
    ///
    /// Future implementation will use the Solana Unity SDK RpcClient.
    /// </summary>
    public interface IRpcService
    {
        /// <summary>
        /// Send a signed transaction to the cluster.
        /// Returns the transaction signature (base58) on success.
        /// Throws on failure.
        /// </summary>
        Task<string> SendTransactionAsync(byte[] signedTransaction);

        /// <summary>
        /// Simulate an unsigned legacy transaction with signature verification disabled.
        /// The transaction must pass before any wallet signing request is made.
        /// </summary>
        Task<RpcSimulationResult> SimulateTransactionAsync(byte[] unsignedTransaction);

        /// <summary>
        /// Confirm a transaction by its signature.
        /// Returns true once finalized/confirmed, false on timeout.
        /// </summary>
        Task<RpcConfirmationStatus> ConfirmTransactionAsync(string txSignature, int timeoutMs = 30000);

        /// <summary>
        /// Fetch an untrusted account envelope for a given public key (base58).
        /// Returns null if the account does not exist.
        /// </summary>
        Task<RpcAccountInfo> GetAccountAsync(string publicKey);

        /// <summary>
        /// Fetch multiple accounts in a single batch call.
        /// Returns an array parallel to the input keys (null entries for missing accounts).
        /// </summary>
        Task<RpcAccountInfo[]> GetMultipleAccountsAsync(string[] publicKeys);

        /// <summary>
        /// Get the latest blockhash from the cluster.
        /// Uses an internal cache with TTL to avoid excessive RPC calls.
        /// Returns the blockhash as a base58 string.
        /// </summary>
        Task<string> GetLatestBlockhashAsync();

        /// <summary>
        /// Get the cluster's current unix timestamp (seconds) via getSlot + getBlockTime.
        /// Returns null if the RPC call fails.
        /// </summary>
        Task<long?> GetBlockTimeAsync();
    }

    public sealed class RpcSimulationResult
    {
        public bool success;
        public string errorMessage;
        public string[] logs;
        public long unitsConsumed;
    }

    public enum RpcConfirmationStatus
    {
        TimedOut = 0,
        Confirmed = 1,
        Finalized = 2,
    }

    public sealed class RpcAccountInfo
    {
        public string publicKey;
        public string owner;
        public ulong lamports;
        public bool executable;
        public byte[] data;
        public long contextSlot;
    }
}
