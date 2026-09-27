using System;
using System.Threading.Tasks;

namespace Chainbound.Combat
{
    /// <summary>
    /// Abstraction over a Solana wallet connection (e.g., Solana Unity SDK adapter).
    /// Gameplay code MUST NOT call RPC or sign transactions directly;
    /// all wallet interaction goes through this interface.
    /// </summary>
    public interface IWalletService
    {
        /// <summary>Whether a wallet is currently connected.</summary>
        bool IsConnected { get; }

        /// <summary>The connected wallet's public key (base58). Null if not connected.</summary>
        string PublicKey { get; }

        /// <summary>Whether the wallet can sign and submit through the current MWA session.</summary>
        bool SupportsSignAndSend { get; }

        /// <summary>Connect to a wallet. Returns the public key on success.</summary>
        Task<string> ConnectAsync();

        /// <summary>Disconnect the current wallet session.</summary>
        Task DisconnectAsync();

        /// <summary>
        /// Sign a transaction payload (serialized bytes).
        /// Returns the signed bytes, or null on user rejection.
        /// </summary>
        Task<byte[]> SignTransactionAsync(byte[] transactionBytes);

        /// <summary>
        /// Ask the external wallet to sign and submit an already validated and simulated
        /// transaction. Returns the base58 transaction signature.
        /// </summary>
        Task<string> SignAndSendTransactionAsync(byte[] transactionBytes);

        /// <summary>
        /// Sign an arbitrary message (for authentication / proof-of-ownership).
        /// Returns the signature bytes, or null on user rejection.
        /// </summary>
        Task<byte[]> SignMessageAsync(byte[] messageBytes);

        /// <summary>Fired when wallet connects successfully. Provides the public key.</summary>
        event Action<string> OnConnected;

        /// <summary>Fired when wallet disconnects.</summary>
        event Action OnDisconnected;

        /// <summary>Fired when the connected account changes (e.g., user switched wallet).</summary>
        event Action<string> OnAccountChanged;
    }

    /// <summary>Optional capability for an external wallet request that is still awaiting user action.</summary>
    public interface IWalletRequestCancellation
    {
        void CancelActiveWalletRequest();
    }
}
