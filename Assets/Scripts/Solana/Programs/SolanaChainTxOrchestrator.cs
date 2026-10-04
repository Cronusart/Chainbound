using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Wallet-owned transaction submission with a durable, wallet/cluster-scoped recovery journal.
    /// A SignAndSend request is never followed by an RPC send and an uncertain wallet callback is
    /// recorded as UnknownOutcome rather than retried.
    /// </summary>
    public sealed class SolanaChainTxOrchestrator : IChainTxOrchestrator
    {
        private const string LegacyPrefsKey = "SolTxOrch_PendingTxJson";
        private const string JournalFileName = "chainbound_pending_transactions_v1.json";
        private const int ConfirmTimeoutMs = 45000;

        private readonly IWalletService wallet;
        private readonly IRpcService rpc;
        private readonly string configuredCluster;
        private readonly string journalPath;
        private readonly object journalGate = new object();
        private readonly Dictionary<string, TxResult> txCache = new Dictionary<string, TxResult>();
        private readonly Dictionary<string, PendingTxRecord> journal = new Dictionary<string, PendingTxRecord>();

        public event Action<string, TxStatus> OnTxStatusChanged;

        public SolanaChainTxOrchestrator(
            IWalletService wallet,
            IRpcService rpc,
            string journalPath = null,
            string configuredCluster = null)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            this.rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            this.configuredCluster = NormalizeCluster(configuredCluster);
            this.journalPath = string.IsNullOrWhiteSpace(journalPath)
                ? Path.Combine(Application.persistentDataPath, JournalFileName)
                : journalPath;
            LoadJournal();
            ImportLegacyPlayerPrefsOnce();
        }

        public async Task<RpcSimulationResult> SimulateAsync(ChainTxRequest request)
        {
            ValidateRequest(request);
            ValidateScopeMatchesWallet(request.scope);
            Notify(request.scope.Key, TxStatus.Simulating);
            return await rpc.SimulateTransactionAsync(request.transactionBytes);
        }

        public async Task<TxResult> SubmitAsync(ChainTxRequest request)
        {
            ValidateRequest(request);
            string key = request.scope.Key;
            ValidateScopeMatchesWallet(request.scope);

            PendingTxRecord existing = Query(request.scope);
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(existing.signature)) return await ResumeAsync(request.scope);
                if (existing.status == TxStatus.UnknownOutcome || existing.status == TxStatus.AwaitingWallet)
                    return CacheResult(key, FromRecord(existing));
                if (existing.status == TxStatus.Rejected || existing.status == TxStatus.Failed)
                    return CacheResult(key, FromRecord(existing));
            }

            Transaction transaction;
            try
            {
                transaction = Transaction.Deserialize(request.transactionBytes);
            }
            catch (Exception ex)
            {
                return FailBeforeSubmission(request.scope, $"Invalid unsigned Solana transaction: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(transaction.RecentBlockHash) || transaction.FeePayer == null ||
                transaction.Instructions == null || transaction.Instructions.Count == 0)
                return FailBeforeSubmission(request.scope, "Transaction has no blockhash, fee payer, or instructions.");

            if (!transaction.FeePayer.Equals(new PublicKey(request.scope.wallet)))
                return FailBeforeSubmission(request.scope, "The scoped wallet must match the transaction fee payer.");

            if (request.requiresSignAndSend && !wallet.SupportsSignAndSend)
                return FailBeforeSubmission(request.scope, "This checkout requires wallet SignAndSend support.");

            var record = NewRecord(request.scope, HashMessage(request.transactionBytes), TxStatus.Simulating);
            SaveRecord(record);
            RpcSimulationResult simulation = await rpc.SimulateTransactionAsync(request.transactionBytes);
            if (!simulation.success)
            {
                record.status = TxStatus.Failed;
                record.errorMessage = $"Transaction simulation failed: {simulation.errorMessage}";
                TouchAndSave(record);
                return CacheResult(key, FromRecord(record, simulation));
            }

            record.status = TxStatus.AwaitingWallet;
            record.errorMessage = null;
            TouchAndSave(record);

            string signature;
            if (request.requiresSignAndSend)
            {
                try
                {
                    // The wallet owns both signing and submission. Never send these bytes through RPC here.
                    signature = await wallet.SignAndSendTransactionAsync(request.transactionBytes);
                }
                catch (Exception ex)
                {
                    record.status = TxStatus.UnknownOutcome;
                    record.errorMessage = $"Wallet callback was not conclusive: {ClassifyError(ex)}";
                    TouchAndSave(record);
                    return CacheResult(key, FromRecord(record, simulation));
                }
            }
            else
            {
#if !UNITY_EDITOR && !UNITY_INCLUDE_TESTS
                return FailBeforeSubmission(request.scope, "Sign-only transaction submission is test-only.");
#else
                byte[] signedBytes;
                try
                {
                    signedBytes = await wallet.SignTransactionAsync(request.transactionBytes);
                }
                catch (Exception ex)
                {
                    record.status = TxStatus.Failed;
                    record.errorMessage = ClassifyError(ex);
                    TouchAndSave(record);
                    return CacheResult(key, FromRecord(record, simulation));
                }

                if (signedBytes == null)
                {
                    record.status = TxStatus.Rejected;
                    record.errorMessage = "User rejected the signing request.";
                    TouchAndSave(record);
                    return CacheResult(key, FromRecord(record, simulation));
                }

                try
                {
                    signature = await rpc.SendTransactionAsync(signedBytes);
                }
                catch (Exception ex)
                {
                    record.status = TxStatus.Failed;
                    record.errorMessage = ClassifyError(ex);
                    TouchAndSave(record);
                    return CacheResult(key, FromRecord(record, simulation));
                }
#endif
            }

            if (string.IsNullOrWhiteSpace(signature))
            {
                record.status = TxStatus.UnknownOutcome;
                record.errorMessage = "Wallet returned no transaction signature.";
                TouchAndSave(record);
                return CacheResult(key, FromRecord(record, simulation));
            }

            // Persist the evidence before the first confirmation request.
            record.signature = signature;
            record.status = TxStatus.Submitted;
            record.errorMessage = null;
            TouchAndSave(record);
            return await ConfirmRecord(record, simulation);
        }

        public async Task<TxResult> ResumeAsync(ChainTxScope scope)
        {
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            ValidateScopeMatchesWallet(scope);
            PendingTxRecord record = Query(scope);
            if (record == null) return null;
            if (string.IsNullOrWhiteSpace(record.signature)) return CacheResult(scope.Key, FromRecord(record));
            return await ConfirmRecord(record, null);
        }

        public PendingTxRecord Query(ChainTxScope scope)
        {
            if (scope == null) return null;
            lock (journalGate)
            {
                return journal.TryGetValue(scope.Key, out PendingTxRecord record) ? Clone(record) : null;
            }
        }

        public Task<TxResult> SubmitAsync(string idempotencyKey, byte[] transactionBytes)
        {
            var scope = new ChainTxScope("legacy-configured", wallet.PublicKey, idempotencyKey);
            return SubmitAsync(new ChainTxRequest(scope, transactionBytes, wallet.SupportsSignAndSend));
        }

        public async Task<TxResult> ResumeConfirmationAsync(string txSignature)
        {
            PendingTxRecord record;
            lock (journalGate)
                record = journal.Values.FirstOrDefault(item => item.signature == txSignature);
            if (record == null)
            {
                record = NewRecord(new ChainTxScope("legacy-configured", wallet.PublicKey, txSignature), string.Empty, TxStatus.Submitted);
                record.signature = txSignature;
                SaveRecord(record);
            }
            ValidateScopeMatchesWallet(new ChainTxScope(record.cluster, record.wallet, record.operationId, record.orderId));
            return await ConfirmRecord(record, null);
        }

        public bool HasPendingTx(string idempotencyKey)
        {
            lock (journalGate)
                return journal.Values.Any(record => record.operationId == idempotencyKey && IsRecoverable(record.status));
        }

        public TxResult GetCachedResult(string idempotencyKey)
        {
            txCache.TryGetValue(idempotencyKey, out TxResult result);
            if (result != null) return result;
            lock (journalGate)
            {
                PendingTxRecord record = journal.Values.FirstOrDefault(item => item.operationId == idempotencyKey);
                return record == null ? null : FromRecord(record);
            }
        }

        public async Task ResumeAllPendingAsync()
        {
            List<PendingTxRecord> records;
            lock (journalGate)
                records = journal.Values.Where(item => IsRecoverable(item.status) && !string.IsNullOrWhiteSpace(item.signature)).Select(Clone).ToList();
            foreach (PendingTxRecord record in records)
            {
                if (!string.Equals(record.wallet, wallet.PublicKey, StringComparison.Ordinal)) continue;
                await ConfirmRecord(record, null);
            }
        }

        private async Task<TxResult> ConfirmRecord(PendingTxRecord record, RpcSimulationResult simulation)
        {
            record.status = TxStatus.Confirming;
            TouchAndSave(record);
            try
            {
                RpcConfirmationStatus confirmation = await rpc.ConfirmTransactionAsync(record.signature, ConfirmTimeoutMs);
                if (confirmation == RpcConfirmationStatus.TimedOut)
                {
                    record.status = TxStatus.UnknownOutcome;
                    record.errorMessage = "Confirmation timed out; the submitted signature remains recoverable.";
                }
                else
                {
                    record.status = MapConfirmationStatus(confirmation);
                    record.errorMessage = null;
                }
            }
            catch (Exception ex)
            {
                record.status = TxStatus.UnknownOutcome;
                record.errorMessage = $"Confirmation was inconclusive: {ex.Message}";
            }
            TouchAndSave(record);
            return CacheResult(record.ScopeKey, FromRecord(record, simulation));
        }

        public static TxStatus MapConfirmationStatus(RpcConfirmationStatus confirmation)
        {
            return confirmation == RpcConfirmationStatus.Finalized
                ? TxStatus.Finalized
                : confirmation == RpcConfirmationStatus.Confirmed ? TxStatus.Confirmed : TxStatus.UnknownOutcome;
        }

        private void ValidateRequest(ChainTxRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (request.scope == null) throw new ArgumentException("Transaction scope is required.", nameof(request));
            if (request.transactionBytes == null || request.transactionBytes.Length == 0)
                throw new ArgumentException("Transaction bytes are required.", nameof(request));
            if (string.IsNullOrWhiteSpace(request.scope.cluster) || string.IsNullOrWhiteSpace(request.scope.wallet) ||
                (string.IsNullOrWhiteSpace(request.scope.operationId) && string.IsNullOrWhiteSpace(request.scope.orderId)))
                throw new ArgumentException("Cluster, wallet, and an operation/order ID are required.", nameof(request));
        }

        private void ValidateScopeMatchesWallet(ChainTxScope scope)
        {
            if (!wallet.IsConnected || string.IsNullOrWhiteSpace(wallet.PublicKey))
                throw new InvalidOperationException("A wallet must be connected.");
            if (!string.Equals(scope.wallet, wallet.PublicKey, StringComparison.Ordinal))
                throw new InvalidOperationException("Pending transactions cannot resume under a different wallet.");
            if (!string.IsNullOrWhiteSpace(configuredCluster) &&
                !string.Equals(NormalizeCluster(scope.cluster), configuredCluster, StringComparison.Ordinal))
                throw new InvalidOperationException("Pending transactions cannot resume under a different Solana cluster.");
        }

        private static string NormalizeCluster(string cluster)
        {
            string value = (cluster ?? string.Empty).Trim().ToLowerInvariant();
            return value == "mainnet" ? "mainnet-beta" : value;
        }

        private TxResult FailBeforeSubmission(ChainTxScope scope, string message)
        {
            var record = NewRecord(scope, string.Empty, TxStatus.Failed);
            record.errorMessage = message;
            TouchAndSave(record);
            return CacheResult(scope.Key, FromRecord(record));
        }

        private PendingTxRecord NewRecord(ChainTxScope scope, string messageHash, TxStatus status)
        {
            return new PendingTxRecord
            {
                cluster = scope.cluster,
                wallet = scope.wallet,
                operationId = scope.operationId,
                orderId = scope.orderId,
                messageHash = messageHash,
                status = status,
                lastUpdatedTicks = DateTime.UtcNow.Ticks,
            };
        }

        private TxResult FromRecord(PendingTxRecord record, RpcSimulationResult simulation = null)
        {
            return new TxResult
            {
                txSignature = record.signature,
                status = record.status,
                errorMessage = record.errorMessage,
                lastUpdatedTicks = record.lastUpdatedTicks,
                scope = new ChainTxScope(record.cluster, record.wallet, record.operationId, record.orderId),
                simulation = simulation,
            };
        }

        private TxResult CacheResult(string key, TxResult result)
        {
            txCache[key] = result;
            return result;
        }

        private void TouchAndSave(PendingTxRecord record)
        {
            record.lastUpdatedTicks = DateTime.UtcNow.Ticks;
            SaveRecord(record);
        }

        private void SaveRecord(PendingTxRecord record)
        {
            lock (journalGate)
            {
                journal[record.ScopeKey] = Clone(record);
                WriteJournalAtomically();
            }
            Notify(record.ScopeKey, record.status);
        }

        private void Notify(string key, TxStatus status)
        {
            OnTxStatusChanged?.Invoke(key, status);
        }

        private static bool IsRecoverable(TxStatus status)
        {
            return status == TxStatus.AwaitingWallet || status == TxStatus.Submitted ||
                   status == TxStatus.Confirming || status == TxStatus.UnknownOutcome || status == TxStatus.Confirmed;
        }

        private static string ClassifyError(Exception exception)
        {
            string message = exception.Message ?? string.Empty;
            if (message.IndexOf("blockhash", StringComparison.OrdinalIgnoreCase) >= 0) return "BlockhashExpired";
            if (message.Contains("429")) return "RpcRateLimit";
            if (message.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return "RpcTimeout";
            if (message.IndexOf("already processed", StringComparison.OrdinalIgnoreCase) >= 0) return "AlreadyProcessed";
            return $"SendFailed: {message}";
        }

        private static string HashMessage(byte[] serializedTransaction)
        {
            int offset = 0;
            int signatureCount = ReadShortVector(serializedTransaction, ref offset);
            offset = checked(offset + signatureCount * 64);
            if (offset < 0 || offset >= serializedTransaction.Length) throw new FormatException("Transaction message is missing.");
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(serializedTransaction, offset, serializedTransaction.Length - offset);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2"));
                return text.ToString();
            }
        }

        private static int ReadShortVector(byte[] bytes, ref int offset)
        {
            int value = 0;
            int shift = 0;
            while (true)
            {
                if (offset >= bytes.Length || shift > 28) throw new FormatException("Invalid short-vector length.");
                byte current = bytes[offset++];
                value |= (current & 0x7f) << shift;
                if ((current & 0x80) == 0) return value;
                shift += 7;
            }
        }

        [Serializable]
        private sealed class JournalEnvelope
        {
            public int version = 1;
            public List<PendingTxRecord> entries = new List<PendingTxRecord>();
        }

        [Serializable]
        private sealed class LegacyEntry
        {
            public string key;
            public string sig;
        }

        [Serializable]
        private sealed class LegacyList
        {
            public List<LegacyEntry> entries = new List<LegacyEntry>();
        }

        private void LoadJournal()
        {
            if (!File.Exists(journalPath)) return;
            try
            {
                JournalEnvelope envelope = JsonUtility.FromJson<JournalEnvelope>(File.ReadAllText(journalPath));
                if (envelope?.entries == null || envelope.version != 1) return;
                foreach (PendingTxRecord record in envelope.entries)
                {
                    if (record != null && !string.IsNullOrWhiteSpace(record.cluster) && !string.IsNullOrWhiteSpace(record.wallet))
                        journal[record.ScopeKey] = record;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SolanaChainTxOrchestrator] Journal load failed; evidence was left on disk: {ex.Message}");
            }
        }

        private void ImportLegacyPlayerPrefsOnce()
        {
            if (!PlayerPrefs.HasKey(LegacyPrefsKey)) return;
            try
            {
                LegacyList legacy = JsonUtility.FromJson<LegacyList>(PlayerPrefs.GetString(LegacyPrefsKey, string.Empty));
                foreach (LegacyEntry entry in legacy?.entries ?? new List<LegacyEntry>())
                {
                    if (string.IsNullOrWhiteSpace(entry.key) || string.IsNullOrWhiteSpace(entry.sig)) continue;
                    var record = NewRecord(new ChainTxScope("legacy-configured", wallet.PublicKey, entry.key), string.Empty, TxStatus.Submitted);
                    record.signature = entry.sig;
                    journal[record.ScopeKey] = record;
                }
                WriteJournalAtomically();
                PlayerPrefs.DeleteKey(LegacyPrefsKey);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SolanaChainTxOrchestrator] Legacy pending transaction import failed: {ex.Message}");
            }
        }

        private void WriteJournalAtomically()
        {
            string directory = Path.GetDirectoryName(journalPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporaryPath = journalPath + ".tmp";
            string backupPath = journalPath + ".bak";
            var envelope = new JournalEnvelope { entries = journal.Values.OrderBy(item => item.lastUpdatedTicks).ToList() };
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(envelope), Encoding.UTF8);
            if (File.Exists(journalPath))
            {
                File.Replace(temporaryPath, journalPath, backupPath, true);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            else
            {
                File.Move(temporaryPath, journalPath);
            }
        }

        private static PendingTxRecord Clone(PendingTxRecord source)
        {
            return new PendingTxRecord
            {
                version = source.version,
                cluster = source.cluster,
                wallet = source.wallet,
                operationId = source.operationId,
                orderId = source.orderId,
                signature = source.signature,
                messageHash = source.messageHash,
                status = source.status,
                errorMessage = source.errorMessage,
                lastUpdatedTicks = source.lastUpdatedTicks,
            };
        }
    }
}
