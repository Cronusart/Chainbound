using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Non-custodial wallet service. Android delegates authorization, message signing, and
    /// transaction sign-and-send to the official Mobile Wallet Adapter native bridge.
    /// No private key, mnemonic, or local player wallet is created by this service.
    /// </summary>
    public sealed class SolanaWalletService : IWalletService, IWalletRequestCancellation, IDisposable
    {
        private const string LastPublicKeyPreference = "SolWallet_LastPubkey";
        private const string AuthTokenStorageKey = "mwa.authToken";

        private readonly SolanaConfigSO _config;
        private readonly IMobileWalletAdapterBridge _mobileBridge;
        private bool _connected;
        private string _publicKey;
        private string _authToken;

#if UNITY_EDITOR
        private bool _usingDesktopBridge;
#endif

        public SolanaWalletService(SolanaConfigSO config = null)
            : this(config, new AndroidMobileWalletAdapterBridge())
        {
        }

        internal SolanaWalletService(SolanaConfigSO config, IMobileWalletAdapterBridge mobileBridge)
        {
            _config = config;
            _mobileBridge = mobileBridge ?? new AndroidMobileWalletAdapterBridge();
            _authToken = SecureCredentialStore.Get(AuthTokenStorageKey);
        }

        public bool IsConnected => _connected;
        /// <summary>True while the native wallet UI owns the foreground operation.</summary>
        public bool IsWalletUiActive { get; private set; }
        public string PublicKey => _publicKey;
        public bool SupportsSignAndSend =>
#if UNITY_EDITOR
            false;
#else
            _mobileBridge.IsAvailable;
#endif

        public string LastKnownPubkey => PlayerPrefs.GetString(LastPublicKeyPreference, null);

        public event Action<string> OnConnected;
        public event Action OnDisconnected;
        public event Action<string> OnAccountChanged;

        public async Task<string> ConnectAsync()
        {
#if UNITY_EDITOR
            if (_config == null || _config.useDesktopBridgeInEditor)
            {
                string editorPublicKey = await DesktopWalletBridge.Instance.ConnectAsync();
                if (string.IsNullOrWhiteSpace(editorPublicKey) || editorPublicKey == DesktopWalletBridge.UseInGameSentinel)
                {
                    Debug.LogWarning("[SolanaWalletService] Local keypair wallets have been removed. Connect a browser wallet instead.");
                    return null;
                }

                _usingDesktopBridge = true;
                SetConnected(editorPublicKey.Trim());
                return _publicKey;
            }

            Debug.LogWarning("[SolanaWalletService] Native MWA is unavailable in the Unity Editor.");
            return null;
#else
            ValidateMobileIdentity();
            if (!_mobileBridge.IsAvailable)
                throw new InvalidOperationException("No Mobile Wallet Adapter bridge is available in this Android build.");

            IsWalletUiActive = true;
            MobileWalletAuthorization authorization;
            try
            {
                authorization = await _mobileBridge.ConnectAsync(
                    _config.mwaIdentityUri,
                    _config.mwaIconUri,
                    _config.mwaIdentityName,
                    _authToken);
            }
            finally { IsWalletUiActive = false; }

            _authToken = authorization.AuthToken;
            if (!string.IsNullOrWhiteSpace(_authToken))
                SecureCredentialStore.Set(AuthTokenStorageKey, _authToken);
            SetConnected(authorization.PublicKey);
            return _publicKey;
#endif
        }

        public async Task DisconnectAsync()
        {
#if UNITY_EDITOR
            if (_usingDesktopBridge)
            {
                await DesktopWalletBridge.Instance.DisconnectAsync();
                _usingDesktopBridge = false;
            }
#else
            if (_mobileBridge.IsAvailable && _config != null)
            {
                try
                {
                    IsWalletUiActive = true;
                    await _mobileBridge.DisconnectAsync(
                        _config.mwaIdentityUri,
                        _config.mwaIconUri,
                        _config.mwaIdentityName,
                        _authToken);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SolanaWalletService] MWA disconnect failed: {ex.Message}");
                }
                finally { IsWalletUiActive = false; }
            }
#endif
            _authToken = null;
            SecureCredentialStore.Delete(AuthTokenStorageKey);
            _connected = false;
            _publicKey = null;
            OnDisconnected?.Invoke();
        }

        public void CancelActiveWalletRequest()
        {
            _mobileBridge.CancelActiveRequest();
        }

        public async Task<byte[]> SignTransactionAsync(byte[] transactionBytes)
        {
#if UNITY_EDITOR
            if (_usingDesktopBridge && DesktopWalletBridge.Instance.IsConnected)
                return await DesktopWalletBridge.Instance.SignTransactionAsync(transactionBytes);
#endif
            throw new NotSupportedException(
                "MWA 2.0 deprecates sign-only transactions. Use SignAndSendTransactionAsync after client-side simulation.");
        }

        public async Task<string> SignAndSendTransactionAsync(byte[] transactionBytes)
        {
            EnsureConnected();
#if UNITY_EDITOR
            await Task.Yield();
            throw new NotSupportedException("The Editor browser bridge supports sign-only transactions.");
#else
            ValidateMobileIdentity();
            IsWalletUiActive = true;
            try
            {
                return await _mobileBridge.SignAndSendTransactionAsync(
                    _config.mwaIdentityUri,
                    _config.mwaIconUri,
                    _config.mwaIdentityName,
                    _authToken,
                    transactionBytes);
            }
            finally { IsWalletUiActive = false; }
#endif
        }

        public async Task<byte[]> SignMessageAsync(byte[] messageBytes)
        {
            EnsureConnected();
#if UNITY_EDITOR
            if (_usingDesktopBridge && DesktopWalletBridge.Instance.IsConnected)
                return await DesktopWalletBridge.Instance.SignMessageAsync(messageBytes);
            return null;
#else
            ValidateMobileIdentity();
            IsWalletUiActive = true;
            try
            {
                return await _mobileBridge.SignMessageAsync(
                    _config.mwaIdentityUri,
                    _config.mwaIconUri,
                    _config.mwaIdentityName,
                    _authToken,
                    messageBytes);
            }
            finally { IsWalletUiActive = false; }
#endif
        }

        private void SetConnected(string publicKey)
        {
            if (string.IsNullOrWhiteSpace(publicKey))
                throw new InvalidOperationException("Wallet authorization returned no public key.");

            string previous = _publicKey;
            _publicKey = publicKey.Trim();
            _connected = true;
            PlayerPrefs.SetString(LastPublicKeyPreference, _publicKey);
            PlayerPrefs.Save();

            if (!string.IsNullOrEmpty(previous) && !string.Equals(previous, _publicKey, StringComparison.Ordinal))
                OnAccountChanged?.Invoke(_publicKey);
            OnConnected?.Invoke(_publicKey);
        }

        private void EnsureConnected()
        {
            if (!_connected || string.IsNullOrWhiteSpace(_publicKey))
                throw new InvalidOperationException("A wallet must be connected before requesting a signature.");
        }

        private void ValidateMobileIdentity()
        {
            if (_config == null)
                throw new InvalidOperationException("SolanaConfigSO is required for Mobile Wallet Adapter.");
            if (!Uri.TryCreate(_config.mwaIdentityUri, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException("MWA identity URI must be a configured HTTPS URL owned by the game.");
            if (string.IsNullOrWhiteSpace(_config.mwaIdentityName))
                throw new InvalidOperationException("MWA identity name is not configured.");
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (_usingDesktopBridge)
            {
                DesktopWalletBridge.Instance?.Dispose();
                _usingDesktopBridge = false;
            }
#endif
        }
    }
}
