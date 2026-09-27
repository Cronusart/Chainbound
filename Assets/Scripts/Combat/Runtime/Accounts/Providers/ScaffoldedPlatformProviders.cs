using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    public abstract class UnsupportedRecoverableSignInProvider : MonoBehaviour, IRecoverableSignInProvider
    {
        public abstract AuthIdentityType AuthIdentityType { get; }
        public abstract PlatformIdentityType PlatformIdentityType { get; }
        public virtual bool IsAvailable => false;
        public virtual bool CanRestore => false;

        public Task<IdentitySignInResult> SignInAsync(AccountSignInRequest request)
        {
            return Task.FromResult(IdentitySignInResult.Failed($"{GetType().Name} SDK wiring is not installed."));
        }

        public Task<IdentitySignInResult> RestoreAsync(AccountSignInRequest request)
        {
            return Task.FromResult(IdentitySignInResult.Failed($"{GetType().Name} SDK wiring is not installed."));
        }

        public Task SignOutAsync()
        {
            return Task.CompletedTask;
        }
    }

    public class GooglePlayIdentityProvider : UnsupportedRecoverableSignInProvider
    {
        public override AuthIdentityType AuthIdentityType => AuthIdentityType.GooglePlay;
        public override PlatformIdentityType PlatformIdentityType => PlatformIdentityType.GooglePlay;
    }

    public class AppleIdentityProvider : UnsupportedRecoverableSignInProvider
    {
        public override AuthIdentityType AuthIdentityType => AuthIdentityType.Apple;
        public override PlatformIdentityType PlatformIdentityType => PlatformIdentityType.Apple;
    }

    public abstract class UnsupportedWalletProvider : MonoBehaviour, IWalletProvider
    {
        public abstract WalletProviderType ProviderType { get; }
        public virtual bool IsAvailable => false;
        public bool IsConnected => false;
        public string PublicKey => "";

        public event Action<WalletConnectResult> OnWalletConnected;
        public event Action OnWalletDisconnected;

        public Task<WalletConnectResult> ConnectAsync()
        {
            var result = WalletConnectResult.Failed($"{GetType().Name} SDK wiring is not installed.");
            OnWalletConnected?.Invoke(result);
            return Task.FromResult(result);
        }

        public Task DisconnectAsync()
        {
            OnWalletDisconnected?.Invoke();
            return Task.CompletedTask;
        }

        public Task<byte[]> SignMessageAsync(byte[] messageBytes)
        {
            return Task.FromResult<byte[]>(null);
        }
    }

    public class AndroidWalletProvider : UnsupportedWalletProvider
    {
        [SerializeField] private WalletProviderType providerType = WalletProviderType.Other;
        public override WalletProviderType ProviderType => providerType;
    }

    public class IOSWalletProvider : UnsupportedWalletProvider
    {
        [SerializeField] private WalletProviderType providerType = WalletProviderType.Other;
        public override WalletProviderType ProviderType => providerType;
    }
}
