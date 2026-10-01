using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Chainbound.Solana;

namespace Chainbound.Combat
{
    public sealed class WalletUserCancelledException : Exception
    {
        public WalletUserCancelledException(string message) : base(message) { }
    }

    internal sealed class MobileWalletAuthorization
    {
        public string PublicKey;
        public string AuthToken;
    }

    internal sealed class MobileWalletMessageSignature
    {
        public byte[] Signature;
        public string AuthToken;
    }

    internal sealed class MobileWalletTransactionSubmission
    {
        public string Signature;
        public string AuthToken;
    }

    internal interface IMobileWalletAdapterBridge
    {
        bool IsAvailable { get; }
        void CancelActiveRequest();
        Task<MobileWalletAuthorization> ConnectAsync(string cluster, string identityUri, string iconUri, string identityName, string authToken);
        Task DisconnectAsync(string cluster, string identityUri, string iconUri, string identityName, string authToken);
        Task<MobileWalletMessageSignature> SignMessageAsync(string cluster, string identityUri, string iconUri, string identityName, string authToken, byte[] message);
        Task<MobileWalletTransactionSubmission> SignAndSendTransactionAsync(string cluster, string identityUri, string iconUri, string identityName, string authToken, byte[] transaction);
    }

    /// <summary>
    /// Thin C# boundary over the official Android MWA client. The native plugin receives only
    /// public transaction/message bytes and never exposes key material to Unity.
    /// </summary>
    internal sealed class AndroidMobileWalletAdapterBridge : IMobileWalletAdapterBridge
    {
        private const string JavaBridgeClass = "com.signaljam.solana.mwa.SignalJamMwaBridge";
        private const int OperationTimeoutMs = 120000;
        private readonly SemaphoreSlim _operationLock = new SemaphoreSlim(1, 1);

        public bool IsAvailable
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using (var bridge = new AndroidJavaClass(JavaBridgeClass))
                        return bridge.CallStatic<bool>("isAvailable");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MWA] Native bridge unavailable: {ex.Message}");
                    return false;
                }
#else
                return false;
#endif
            }
        }

        public void CancelActiveRequest()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var bridge = new AndroidJavaClass(JavaBridgeClass))
                    bridge.CallStatic("cancelActiveRequest");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MWA] Could not cancel the active wallet request: {ex.Message}");
            }
#endif
        }

        public async Task<MobileWalletAuthorization> ConnectAsync(
            string cluster,
            string identityUri,
            string iconUri,
            string identityName,
            string authToken)
        {
            NativePayload payload = await InvokeAsync(
                "connect",
                cluster,
                identityUri,
                iconUri,
                identityName,
                authToken ?? string.Empty,
                null);

            byte[] publicKeyBytes = DecodeRequired(payload.publicKeyBase64, "wallet public key");
            if (publicKeyBytes.Length != 32)
                throw new InvalidOperationException("MWA returned a public key with an invalid length.");

            return new MobileWalletAuthorization
            {
                PublicKey = Base58.Encode(publicKeyBytes),
                AuthToken = payload.authToken ?? string.Empty,
            };
        }

        public async Task DisconnectAsync(
            string cluster,
            string identityUri,
            string iconUri,
            string identityName,
            string authToken)
        {
            await InvokeAsync(
                "disconnect",
                cluster,
                identityUri,
                iconUri,
                identityName,
                authToken ?? string.Empty,
                null);
        }

        public async Task<MobileWalletMessageSignature> SignMessageAsync(
            string cluster,
            string identityUri,
            string iconUri,
            string identityName,
            string authToken,
            byte[] message)
        {
            if (message == null || message.Length == 0)
                throw new ArgumentException("Message payload is required.", nameof(message));

            NativePayload payload = await InvokeAsync(
                "signMessage",
                cluster,
                identityUri,
                iconUri,
                identityName,
                authToken ?? string.Empty,
                Convert.ToBase64String(message));

            byte[] signature = DecodeRequired(payload.signatureBase64, "message signature");
            if (signature.Length != 64)
                throw new InvalidOperationException("MWA returned a message signature with an invalid length.");
            return new MobileWalletMessageSignature
            {
                Signature = signature,
                AuthToken = payload.authToken ?? string.Empty,
            };
        }

        public async Task<MobileWalletTransactionSubmission> SignAndSendTransactionAsync(
            string cluster,
            string identityUri,
            string iconUri,
            string identityName,
            string authToken,
            byte[] transaction)
        {
            if (transaction == null || transaction.Length == 0)
                throw new ArgumentException("Transaction payload is required.", nameof(transaction));

            NativePayload payload = await InvokeAsync(
                "signAndSendTransaction",
                cluster,
                identityUri,
                iconUri,
                identityName,
                authToken ?? string.Empty,
                Convert.ToBase64String(transaction));

            byte[] signature = DecodeRequired(payload.signatureBase64, "transaction signature");
            if (signature.Length != 64)
                throw new InvalidOperationException("MWA returned a transaction signature with an invalid length.");
            return new MobileWalletTransactionSubmission
            {
                Signature = Base58.Encode(signature),
                AuthToken = payload.authToken ?? string.Empty,
            };
        }

        private async Task<NativePayload> InvokeAsync(
            string method,
            string cluster,
            string identityUri,
            string iconUri,
            string identityName,
            string authToken,
            string payloadBase64)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            await _operationLock.WaitAsync();
            try
            {
                var completion = new TaskCompletionSource<NativePayload>(TaskCreationOptions.RunContinuationsAsynchronously);
                var callback = new NativeCallback(completion);

                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass(JavaBridgeClass))
                {
                    if (payloadBase64 == null)
                    {
                        bridge.CallStatic(
                            method,
                            activity,
                            cluster,
                            identityUri,
                            iconUri,
                            identityName,
                            authToken,
                            callback);
                    }
                    else
                    {
                        bridge.CallStatic(
                            method,
                            activity,
                            cluster,
                            identityUri,
                            iconUri,
                            identityName,
                            authToken,
                            payloadBase64,
                            callback);
                    }
                }

                Task completed = await Task.WhenAny(completion.Task, Task.Delay(OperationTimeoutMs));
                if (completed != completion.Task)
                {
                    CancelActiveRequest();
                    throw new WalletUserCancelledException("Wallet request timed out after two minutes.");
                }
                return await completion.Task;
            }
            finally
            {
                _operationLock.Release();
            }
#else
            await Task.Yield();
            throw new PlatformNotSupportedException("Mobile Wallet Adapter is available only on an Android player build.");
#endif
        }

        private static byte[] DecodeRequired(string base64, string label)
        {
            if (string.IsNullOrWhiteSpace(base64))
                throw new InvalidOperationException($"MWA returned an empty {label}.");
            try { return Convert.FromBase64String(base64); }
            catch (FormatException ex) { throw new InvalidOperationException($"MWA returned malformed {label} data.", ex); }
        }

        [Serializable]
        private sealed class NativePayload
        {
            public string publicKeyBase64;
            public string authToken;
            public string signatureBase64;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class NativeCallback : AndroidJavaProxy
        {
            private readonly TaskCompletionSource<NativePayload> _completion;

            public NativeCallback(TaskCompletionSource<NativePayload> completion)
                : base("com.signaljam.solana.mwa.SignalJamMwaCallback")
            {
                _completion = completion;
            }

            public void onSuccess(string payloadJson)
            {
                try
                {
                    NativePayload payload = JsonUtility.FromJson<NativePayload>(payloadJson ?? "{}");
                    _completion.TrySetResult(payload ?? new NativePayload());
                }
                catch (Exception ex)
                {
                    _completion.TrySetException(ex);
                }
            }

            public void onError(string code, string message)
            {
                string safeCode = string.IsNullOrWhiteSpace(code) ? "MWA_ERROR" : code.Trim();
                string safeMessage = string.IsNullOrWhiteSpace(message) ? "Wallet request failed." : message.Trim();
                string normalized = safeCode.ToUpperInvariant();
                if (normalized.Contains("CANCEL") || normalized.Contains("USER_REJECT") || normalized.Contains("DECLIN"))
                    _completion.TrySetException(new WalletUserCancelledException(safeMessage));
                else
                    _completion.TrySetException(new InvalidOperationException($"{safeCode}: {safeMessage}"));
            }
        }
#endif
    }
}
