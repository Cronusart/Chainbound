using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Chainbound.Solana;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production transaction orchestrator: Build -> Sign -> Send -> Confirm -> Finalize.
    /// Persists pending tx signatures to PlayerPrefs for crash/resume recovery.
    /// Classifies errors for clear UX feedback.
    /// </summary>
    public class SolanaChainTxOrchestrator : IChainTxOrchestrator
    {
        private const string PREFS_PENDING_TX_KEY = "SolTxOrch_PendingTxJson";
        private const int DEFAULT_CONFIRM_TIMEOUT_MS = 45000;

        private readonly IWalletService _wallet;
        private readonly IRpcService _rpc;

        // In-memory cache of tx results keyed by idempotency key
        private readonly Dictionary<string, TxResult> _txCache = new Dictionary<string, TxResult>();

        // Persisted pending signatures (idempotencyKey -> txSignature)
        private readonly Dictionary<string, string> _pendingSignatures = new Dictionary<string, string>();

        /// <summary>
        /// Fired when a tx status changes. (idempotencyKey, newStatus)
        /// </summary>
        public event Action<string, TxStatus> OnTxStatusChanged;

        public SolanaChainTxOrchestrator(IWalletService wallet, IRpcService rpc)
        {
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            LoadPendingFromDisk();
        }

        public async Task<TxResult> SubmitAsync(string idempotencyKey, byte[] transactionBytes)
        {
            // Idempotency check
            if (_txCache.TryGetValue(idempotencyKey, out var existing))
            {
                if (existing.status == TxStatus.Confirmed
                    || existing.status == TxStatus.Finalized
                    || existing.status == TxStatus.Rejected)
                {
                    return existing;
                }

                // If submitted but not confirmed, resume confirmation
                if (existing.status == TxStatus.Submitted && !string.IsNullOrEmpty(existing.txSignature))
                {
                    return await ResumeConfirmationAsync(existing.txSignature);
                }
            }

            // â”€â”€ Validate tx bytes are a real serialized Solana transaction â”€â”€
            var result = new TxResult
            {
                lastUpdatedTicks = DateTime.UtcNow.Ticks
            };

            Transaction txCheck;
            try
            {
                txCheck = Transaction.Deserialize(transactionBytes);
            }
            catch (Exception ex)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = $"Expected unsigned tx bytes, got invalid payload: {ex.Message}";
                _txCache[idempotencyKey] = result;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                return result;
            }

            if (string.IsNullOrEmpty(txCheck.RecentBlockHash)
                || txCheck.FeePayer == null
                || txCheck.Instructions == null
                || txCheck.Instructions.Count == 0)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = "Transaction has no blockhash or instructions. Build a real Transaction.";
                _txCache[idempotencyKey] = result;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                return result;
            }

            PublicKey walletPublicKey;
            try
            {
                walletPublicKey = string.IsNullOrWhiteSpace(_wallet.PublicKey)
                    ? null
                    : new PublicKey(_wallet.PublicKey);
            }
            catch (Exception)
            {
                walletPublicKey = null;
            }
            if (walletPublicKey == null || !txCheck.FeePayer.Equals(walletPublicKey))
            {
                result.status = TxStatus.Failed;
                result.errorMessage = "The connected wallet must match the transaction fee payer.";
                _txCache[idempotencyKey] = result;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                return result;
            }

            var simulation = await _rpc.SimulateTransactionAsync(transactionBytes);
            if (!simulation.success)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = $"Transaction simulation failed: {simulation.errorMessage}";
                _txCache[idempotencyKey] = result;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                return result;
            }

            result.status = TxStatus.Signing;
            _txCache[idempotencyKey] = result;
            OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Signing);

            // â”€â”€ Sign â”€â”€
            byte[] signedBytes;
            try
            {
                signedBytes = _wallet.SupportsSignAndSend
                    ? transactionBytes
                    : await _wallet.SignTransactionAsync(transactionBytes);
            }
            catch (Exception ex)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = $"Signing error: {ex.Message}";
                result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                return result;
            }

            if (signedBytes == null)
            {
                result.status = TxStatus.Rejected;
                result.errorMessage = "User rejected the signing prompt.";
                result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Rejected);
                return result;
            }

            // â”€â”€ Send â”€â”€
            string txSignature;
            try
            {
                txSignature = _wallet.SupportsSignAndSend
                    ? await _wallet.SignAndSendTransactionAsync(transactionBytes)
                    : await _rpc.SendTransactionAsync(signedBytes);
            }
            catch (Exception ex)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = ClassifyError(ex);
                result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                return result;
            }

            result.txSignature = txSignature;
            result.status = TxStatus.Submitted;
            result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
            OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Submitted);

            // Persist for crash recovery
            _pendingSignatures[idempotencyKey] = txSignature;
            SavePendingToDisk();

            // â”€â”€ Confirm â”€â”€
            try
            {
                RpcConfirmationStatus confirmation = await _rpc.ConfirmTransactionAsync(txSignature, DEFAULT_CONFIRM_TIMEOUT_MS);
                if (confirmation != RpcConfirmationStatus.TimedOut)
                {
                    result.status = MapConfirmationStatus(confirmation);
                    result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
                    OnTxStatusChanged?.Invoke(idempotencyKey, result.status);
                }
                else
                {
                    result.status = TxStatus.Failed;
                    result.errorMessage = "Confirmation timed out. Transaction may still confirm.";
                    result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
                    OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
                }
            }
            catch (Exception ex)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = $"Confirmation error: {ex.Message}";
                result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
                OnTxStatusChanged?.Invoke(idempotencyKey, TxStatus.Failed);
            }

            // Remove from pending
            _pendingSignatures.Remove(idempotencyKey);
            SavePendingToDisk();

            return result;
        }

        public async Task<TxResult> ResumeConfirmationAsync(string txSignature)
        {
            Debug.Log($"[SolanaChainTxOrchestrator] Resuming confirmation for {txSignature}...");

            var result = new TxResult
            {
                txSignature = txSignature,
                status = TxStatus.Submitted,
                lastUpdatedTicks = DateTime.UtcNow.Ticks
            };

            try
            {
                RpcConfirmationStatus confirmation = await _rpc.ConfirmTransactionAsync(txSignature, DEFAULT_CONFIRM_TIMEOUT_MS);
                result.status = MapConfirmationStatus(confirmation);
                result.errorMessage = confirmation == RpcConfirmationStatus.TimedOut ? "Confirmation timed out on resume." : null;
                result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
            }
            catch (Exception ex)
            {
                result.status = TxStatus.Failed;
                result.errorMessage = $"Resume confirmation error: {ex.Message}";
                result.lastUpdatedTicks = DateTime.UtcNow.Ticks;
            }

            return result;
        }

        /// <summary>Preserves the RPC commitment level without promoting confirmed transactions to finalized.</summary>
        public static TxStatus MapConfirmationStatus(RpcConfirmationStatus confirmation)
        {
            return confirmation == RpcConfirmationStatus.Finalized
                ? TxStatus.Finalized
                : confirmation == RpcConfirmationStatus.Confirmed
                    ? TxStatus.Confirmed
                    : TxStatus.Failed;
        }

        public bool HasPendingTx(string idempotencyKey)
        {
            if (_txCache.TryGetValue(idempotencyKey, out var r))
            {
                return r.status == TxStatus.Signing || r.status == TxStatus.Submitted;
            }
            return _pendingSignatures.ContainsKey(idempotencyKey);
        }

        public TxResult GetCachedResult(string idempotencyKey)
        {
            _txCache.TryGetValue(idempotencyKey, out var result);
            return result;
        }

        /// <summary>
        /// Resume all pending transactions from a previous session.
        /// Call this on app resume / focus regained.
        /// </summary>
        public async Task ResumeAllPendingAsync()
        {
            if (_pendingSignatures.Count == 0) return;

            Debug.Log($"[SolanaChainTxOrchestrator] Resuming {_pendingSignatures.Count} pending transactions...");

            // Copy to avoid modification during iteration
            var pending = new Dictionary<string, string>(_pendingSignatures);

            foreach (var kvp in pending)
            {
                var resumeResult = await ResumeConfirmationAsync(kvp.Value);
                _txCache[kvp.Key] = resumeResult;

                if (resumeResult.status == TxStatus.Finalized || resumeResult.status == TxStatus.Failed)
                {
                    _pendingSignatures.Remove(kvp.Key);
                }
            }

            SavePendingToDisk();
        }

        // â”€â”€ Error Classification â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private string ClassifyError(Exception ex)
        {
            string msg = ex.Message ?? "";

            if (msg.Contains("blockhash") || msg.Contains("Blockhash"))
                return "BlockhashExpired: Rebuild the transaction and retry.";

            if (msg.Contains("rate limit") || msg.Contains("429"))
                return "RpcRateLimit: Too many requests. Wait and retry.";

            if (msg.Contains("timeout") || msg.Contains("Timeout"))
                return "RpcTimeout: Network timeout. Check connection.";

            if (msg.Contains("already processed") || msg.Contains("AlreadyProcessed"))
                return "AlreadyProcessed: Transaction was already submitted.";

            if (msg.Contains("simulation") || msg.Contains("Simulation"))
                return $"SimulationFailed: {msg}";

            return $"SendFailed: {msg}";
        }

        // â”€â”€ Persistence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        [Serializable]
        private class PendingTxEntry
        {
            public string key;
            public string sig;
        }

        [Serializable]
        private class PendingTxList
        {
            public List<PendingTxEntry> entries = new List<PendingTxEntry>();
        }

        private void SavePendingToDisk()
        {
            var list = new PendingTxList();
            foreach (var kvp in _pendingSignatures)
            {
                list.entries.Add(new PendingTxEntry { key = kvp.Key, sig = kvp.Value });
            }
            string json = JsonUtility.ToJson(list);
            PlayerPrefs.SetString(PREFS_PENDING_TX_KEY, json);
            PlayerPrefs.Save();
        }

        private void LoadPendingFromDisk()
        {
            string json = PlayerPrefs.GetString(PREFS_PENDING_TX_KEY, "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var list = JsonUtility.FromJson<PendingTxList>(json);
                if (list?.entries != null)
                {
                    foreach (var entry in list.entries)
                    {
                        if (!string.IsNullOrEmpty(entry.key) && !string.IsNullOrEmpty(entry.sig))
                        {
                            _pendingSignatures[entry.key] = entry.sig;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SolanaChainTxOrchestrator] Failed to load pending tx data: {ex.Message}");
            }
        }
    }
}
