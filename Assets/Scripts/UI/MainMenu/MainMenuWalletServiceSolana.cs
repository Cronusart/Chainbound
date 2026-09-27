using System;
using System.Threading.Tasks;
using UnityEngine;
using Chainbound.Combat;

/// <summary>
/// Live Solana wallet adapter for the main menu.
/// Bridges SolanaServiceBridge.IWalletService to MainMenuWalletServiceBase.
/// Requires Bootstrap to have [WalletController] and SolanaServices; persists via GameServices.
/// </summary>
public class MainMenuWalletServiceSolana : MainMenuWalletServiceBase
{
    [Header("Options")]
    [Tooltip("If true and wallet is already connected, invoke success immediately without opening adapter.")]
    [SerializeField] private bool reconnectIfAlreadyConnected = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private MainMenuWalletConnectionState _connectionState = MainMenuWalletConnectionState.Disconnected;
    private string _walletAddress = "";
    private bool _connectInProgress;

    public override MainMenuWalletConnectionState ConnectionState => _connectionState;
    public override string WalletAddress => _walletAddress;
    public override event Action<MainMenuWalletConnectionState> OnConnectionStateChanged;

    private void Start()
    {
        var bridge = SolanaServiceBridge.Instance;
        if (bridge?.WalletService == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[MainMenuWalletServiceSolana] SolanaServiceBridge or WalletService not available.");
            }
            return;
        }

        IWalletService wallet = bridge.WalletService;
        wallet.OnConnected += HandleWalletConnected;
        wallet.OnDisconnected += HandleWalletDisconnected;
        wallet.OnAccountChanged += HandleAccountChanged;

        if (wallet.IsConnected && !string.IsNullOrEmpty(wallet.PublicKey))
        {
            _walletAddress = wallet.PublicKey;
            SetState(MainMenuWalletConnectionState.Connected);
        }
    }

    private void OnDestroy()
    {
        var bridge = SolanaServiceBridge.Instance;
        if (bridge?.WalletService != null)
        {
            IWalletService wallet = bridge.WalletService;
            wallet.OnConnected -= HandleWalletConnected;
            wallet.OnDisconnected -= HandleWalletDisconnected;
            wallet.OnAccountChanged -= HandleAccountChanged;
        }
    }

    private void HandleWalletConnected(string pubkey)
    {
        _walletAddress = pubkey ?? "";
        _connectInProgress = false;
        SetState(MainMenuWalletConnectionState.Connected);
    }

    private void HandleWalletDisconnected()
    {
        _walletAddress = "";
        _connectInProgress = false;
        SetState(MainMenuWalletConnectionState.Disconnected);
    }

    private void HandleAccountChanged(string newPubkey)
    {
        _walletAddress = newPubkey ?? "";
        SetState(MainMenuWalletConnectionState.Connected);
    }

    public override void Connect(Action<MainMenuWalletConnectResult> onComplete)
    {
        var bridge = SolanaServiceBridge.Instance;
        if (bridge?.WalletService == null)
        {
            onComplete?.Invoke(new MainMenuWalletConnectResult
            {
                success = false,
                walletAddress = "",
                errorMessage = "Solana wallet service not available."
            });
            return;
        }

        IWalletService wallet = bridge.WalletService;

        if (reconnectIfAlreadyConnected && wallet.IsConnected && !string.IsNullOrEmpty(wallet.PublicKey))
        {
            _walletAddress = wallet.PublicKey;
            SetState(MainMenuWalletConnectionState.Connected);
            onComplete?.Invoke(new MainMenuWalletConnectResult
            {
                success = true,
                walletAddress = _walletAddress,
                errorMessage = ""
            });
            return;
        }

        if (_connectInProgress)
        {
            if (enableDebugLogs)
            {
                Debug.Log("[MainMenuWalletServiceSolana] Connect already in progress.");
            }
            return;
        }

        _connectInProgress = true;
        SetState(MainMenuWalletConnectionState.Connecting);

        RunConnectAsync(wallet, onComplete);
    }

    private async void RunConnectAsync(IWalletService wallet, Action<MainMenuWalletConnectResult> onComplete)
    {
        MainMenuWalletConnectResult result = new MainMenuWalletConnectResult();

        try
        {
            string pubkey = await wallet.ConnectAsync();
            _connectInProgress = false;

            if (!string.IsNullOrEmpty(pubkey))
            {
                _walletAddress = pubkey;
                SetState(MainMenuWalletConnectionState.Connected);
                result.success = true;
                result.walletAddress = pubkey;
                result.errorMessage = "";
            }
            else
            {
                SetState(MainMenuWalletConnectionState.Failed);
                result.success = false;
                result.walletAddress = "";
                result.errorMessage = "Wallet connection failed or was cancelled.";
            }
        }
        catch (Exception ex)
        {
            _connectInProgress = false;
            SetState(MainMenuWalletConnectionState.Failed);
            result.success = false;
            result.walletAddress = "";
            result.errorMessage = ex.Message ?? "Wallet connection failed.";
            if (enableDebugLogs)
            {
                Debug.LogWarning($"[MainMenuWalletServiceSolana] Connect error: {ex.Message}");
            }
        }

        onComplete?.Invoke(result);
    }

    public override void Disconnect(Action onComplete = null)
    {
        var bridge = SolanaServiceBridge.Instance;
        if (bridge?.WalletService == null)
        {
            _walletAddress = "";
            SetState(MainMenuWalletConnectionState.Disconnected);
            onComplete?.Invoke();
            return;
        }

        RunDisconnectAsync(bridge.WalletService, onComplete);
    }

    private async void RunDisconnectAsync(IWalletService wallet, Action onComplete)
    {
        try
        {
            await wallet.DisconnectAsync();
        }
        catch (Exception ex)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[MainMenuWalletServiceSolana] Disconnect error: {ex.Message}");
        }

        _walletAddress = "";
        SetState(MainMenuWalletConnectionState.Disconnected);
        onComplete?.Invoke();
    }

    private void SetState(MainMenuWalletConnectionState newState)
    {
        if (_connectionState == newState) return;
        _connectionState = newState;
        OnConnectionStateChanged?.Invoke(_connectionState);
    }
}
