using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Account-system wallet provider backed by the Solana wallet service.
    /// On mobile builds the underlying Solana service should use wallet-adapter signing,
    /// while editor/test flows can use explicit mock providers instead.
    /// </summary>
    public class SolanaWalletAdapterProvider : MonoBehaviour, IWalletProvider
    {
        [SerializeField] private WalletProviderType providerType = WalletProviderType.Other;
        [SerializeField] private bool enableDebugLogs = true;

        private IWalletService boundWallet;
        private bool connectEventRaised;

        public WalletProviderType ProviderType
        {
            get
            {
                if (providerType != WalletProviderType.None && providerType != WalletProviderType.Other)
                    return providerType;

                return PlatformContext.ResolveCapabilities().supportsSeedVault
                    ? WalletProviderType.SeedVault
                    : WalletProviderType.Other;
            }
        }
        public bool IsAvailable => ResolveWallet() != null;
        public bool IsConnected => ResolveWallet()?.IsConnected == true;
        public string PublicKey => ResolveWallet()?.PublicKey ?? "";

        public event Action<WalletConnectResult> OnWalletConnected;
        public event Action OnWalletDisconnected;

        private void OnEnable()
        {
            BindWalletEvents();
        }

        private void OnDisable()
        {
            UnbindWalletEvents();
        }

        public async Task<WalletConnectResult> ConnectAsync()
        {
            IWalletService wallet = ResolveWallet();
            if (wallet == null)
                return WalletConnectResult.Failed("Solana wallet service is not available.");

            string publicKey;
            connectEventRaised = false;
            try
            {
                publicKey = await wallet.ConnectAsync();
            }
            catch (WalletUserCancelledException ex)
            {
                return new WalletConnectResult { success = false, userCancelled = true, errorMessage = ex.Message };
            }
            catch (TimeoutException ex)
            {
                return new WalletConnectResult { success = false, userCancelled = true, errorMessage = ex.Message };
            }
            catch (Exception ex)
            {
                return WalletConnectResult.Failed(ex.Message);
            }
            if (string.IsNullOrWhiteSpace(publicKey))
                return WalletConnectResult.Failed("Wallet connection was cancelled or unavailable.");

            var result = new WalletConnectResult
            {
                success = true,
                providerType = ProviderType,
                walletPublicKey = publicKey.Trim(),
            };

            // SolanaWalletService raises OnConnected while ConnectAsync is completing.
            // Keep compatibility with providers that do not raise it, without sending
            // two connection events for the native MWA path.
            if (!connectEventRaised)
                OnWalletConnected?.Invoke(result);
            return result;
        }

        public async Task DisconnectAsync()
        {
            IWalletService wallet = ResolveWallet();
            if (wallet != null)
                await wallet.DisconnectAsync();

            OnWalletDisconnected?.Invoke();
        }

        public async Task<byte[]> SignMessageAsync(byte[] messageBytes)
        {
            IWalletService wallet = ResolveWallet();
            if (wallet == null)
                return null;

            return await wallet.SignMessageAsync(messageBytes);
        }

        private IWalletService ResolveWallet()
        {
            IWalletService wallet = SolanaServiceBridge.Instance != null
                ? SolanaServiceBridge.Instance.WalletService
                : null;

            if (!ReferenceEquals(wallet, boundWallet))
            {
                UnbindWalletEvents();
                boundWallet = wallet;
                BindWalletEvents();
            }

            return wallet;
        }

        private void BindWalletEvents()
        {
            if (boundWallet == null && SolanaServiceBridge.Instance != null)
                boundWallet = SolanaServiceBridge.Instance.WalletService;

            if (boundWallet == null)
                return;

            boundWallet.OnConnected -= HandleConnected;
            boundWallet.OnDisconnected -= HandleDisconnected;
            boundWallet.OnConnected += HandleConnected;
            boundWallet.OnDisconnected += HandleDisconnected;
        }

        private void UnbindWalletEvents()
        {
            if (boundWallet == null)
                return;

            boundWallet.OnConnected -= HandleConnected;
            boundWallet.OnDisconnected -= HandleDisconnected;
        }

        private void HandleConnected(string publicKey)
        {
            connectEventRaised = true;
            if (enableDebugLogs)
                Debug.Log($"[SolanaWalletAdapterProvider] Wallet connected: {publicKey}", this);

            OnWalletConnected?.Invoke(new WalletConnectResult
            {
                success = true,
                providerType = ProviderType,
                walletPublicKey = publicKey ?? "",
            });
        }

        private void HandleDisconnected()
        {
            if (enableDebugLogs)
                Debug.Log("[SolanaWalletAdapterProvider] Wallet disconnected.", this);

            OnWalletDisconnected?.Invoke();
        }
    }
}
