using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public class EditorMockWalletProvider : MonoBehaviour, IWalletProvider, IEditorOnlyAccountProvider
    {
        [SerializeField] private WalletProviderType providerType = WalletProviderType.Phantom;
        [SerializeField] private string mockPublicKey = "11111111111111111111111111111111";
        [SerializeField] private bool available = true;

        public WalletProviderType ProviderType => providerType;
        public bool IsAvailable => available && Application.isEditor;
        public bool IsConnected { get; private set; }
        public string PublicKey { get; private set; } = "";

        public event Action<WalletConnectResult> OnWalletConnected;
        public event Action OnWalletDisconnected;

        public Task<WalletConnectResult> ConnectAsync()
        {
            IsConnected = true;
            PublicKey = mockPublicKey;
            var result = new WalletConnectResult
            {
                success = true,
                providerType = providerType,
                walletPublicKey = PublicKey,
            };
            OnWalletConnected?.Invoke(result);
            return Task.FromResult(result);
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            PublicKey = "";
            OnWalletDisconnected?.Invoke();
            return Task.CompletedTask;
        }

        public Task<byte[]> SignMessageAsync(byte[] messageBytes)
        {
            string payload = Convert.ToBase64String(messageBytes ?? Array.Empty<byte>());
            return Task.FromResult(Encoding.UTF8.GetBytes("editor_mock_signature:" + payload));
        }
    }
}
