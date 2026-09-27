using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production VRF service. Requests randomness by submitting a real
    /// "request_encounter" instruction to our game program (which CPIs into
    /// MagicBlock VRF). Polls the EncounterRequest PDA for fulfillment.
    /// </summary>
    public class VrfService : IVrfService
    {
        public const string ExpectedVrfProgramId = "Vrf1RNUjXmQGjmQrQLvJHs9SNkvDJEsRVFPkfSQUwGz";
        public const string ExpectedOracleQueueId = "Cuj97ggrhhidhbu39TijNVqE74xvKJ69gDervRUXAxGh";
        private const string SlotHashesSysvarId = "SysvarS1otHashes111111111111111111111111111";
        private const string PROGRAM_IDENTITY_SEED = "identity";
        private const string PREFS_PENDING_VRF = "VrfSvc_PendingRequestId";
        private const string ENCOUNTER_REQUEST_SEED = "encounter_request";

        private readonly IRpcService _rpc;
        private readonly IChainTxOrchestrator _txOrch;
        private readonly IWalletService _wallet;
        private readonly PublicKey _gameProgramPubkey;
        private readonly PublicKey _vrfProgramPubkey;
        private readonly PublicKey _oracleQueuePubkey;

        private string _pendingRequestId;

        public bool HasPendingRequest => !string.IsNullOrEmpty(_pendingRequestId);
        public string PendingRequestId => _pendingRequestId;

        public VrfService(
            IRpcService rpc,
            IChainTxOrchestrator txOrch,
            IWalletService wallet,
            PublicKey gameProgramId,
            PublicKey vrfProgramId,
            PublicKey oracleQueueId)
        {
            _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            _txOrch = txOrch ?? throw new ArgumentNullException(nameof(txOrch));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gameProgramPubkey = gameProgramId ?? throw new ArgumentNullException(nameof(gameProgramId));
            _vrfProgramPubkey = vrfProgramId ?? throw new ArgumentNullException(nameof(vrfProgramId));
            _oracleQueuePubkey = oracleQueueId ?? throw new ArgumentNullException(nameof(oracleQueueId));
            if (_vrfProgramPubkey.Key != ExpectedVrfProgramId || _oracleQueuePubkey.Key != ExpectedOracleQueueId)
                throw new ArgumentException("MagicBlock VRF program/queue do not match reviewed base-layer pins.");

            _pendingRequestId = PlayerPrefs.GetString(PREFS_PENDING_VRF, null);
        }

        // â”€â”€ PDA Derivation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Derive the EncounterRequest PDA deterministically:
        /// seeds = ["encounter_request", walletPubkeyBytes, sha256(seedContext)]
        /// </summary>
        public static string DeriveEncounterRequestPda(
            PublicKey gameProgramId, string walletPubkey, byte[] seedContext)
        {
            var walletPk = new PublicKey(walletPubkey);
            var seeds = new List<byte[]>
            {
                Encoding.UTF8.GetBytes(ENCOUNTER_REQUEST_SEED),
                walletPk.KeyBytes,
            };
            seeds.Add(HashSeedContext(seedContext));

            bool found = PublicKey.TryFindProgramAddress(
                seeds,
                gameProgramId,
                out PublicKey pda,
                out _);
            return found ? pda.Key : null;
        }

        private static byte[] HashSeedContext(byte[] seedContext)
        {
            using SHA256 sha = SHA256.Create();
            return sha.ComputeHash(seedContext ?? Array.Empty<byte>());
        }

        // â”€â”€ Request Randomness â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VrfRequestResult> RequestRandomnessAsync(byte[] seedContext)
        {
            if (!_wallet.IsConnected || string.IsNullOrEmpty(_wallet.PublicKey))
            {
                return new VrfRequestResult
                {
                    status = VrfRequestStatus.Failed,
                    errorMessage = "Wallet not connected.",
                };
            }

            Debug.Log("[VrfService] Requesting randomness via game program...");

            // Derive the EncounterRequest PDA before sending so we know what account to poll
            string encounterRequestPda = DeriveEncounterRequestPda(
                _gameProgramPubkey, _wallet.PublicKey, seedContext);

            if (string.IsNullOrEmpty(encounterRequestPda))
            {
                return new VrfRequestResult
                {
                    status = VrfRequestStatus.Failed,
                    errorMessage = "Failed to derive EncounterRequest PDA.",
                };
            }

            string idempotencyKey = $"vrf_request_{DateTime.UtcNow.Ticks}";
            byte[] requestSeed = HashSeedContext(seedContext);
            byte[] txBytes = await BuildUnsignedRequestEncounterTx(
                _wallet.PublicKey, encounterRequestPda, requestSeed);

            var txResult = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

            if (txResult.status == TxStatus.Finalized || txResult.status == TxStatus.Confirmed)
            {
                _pendingRequestId = encounterRequestPda;
                PlayerPrefs.SetString(PREFS_PENDING_VRF, encounterRequestPda);
                PlayerPrefs.Save();

                Debug.Log($"[VrfService] Randomness requested. EncounterRequest PDA: {encounterRequestPda}");

                return new VrfRequestResult
                {
                    requestId = encounterRequestPda,
                    txSignature = txResult.txSignature,
                    status = VrfRequestStatus.Pending,
                };
            }

            return new VrfRequestResult
            {
                status = VrfRequestStatus.Failed,
                errorMessage = txResult.errorMessage ?? $"VRF request failed: {txResult.status}",
            };
        }

        // â”€â”€ Poll â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VrfRequestResult> PollRandomnessAsync(
            string requestId,
            int pollIntervalMs = 2000,
            int timeoutMs = 30000)
        {
            Debug.Log($"[VrfService] Polling EncounterRequest PDA {requestId}...");

            float startTime = Time.realtimeSinceStartup;
            float timeoutSec = timeoutMs / 1000f;

            while (Time.realtimeSinceStartup - startTime < timeoutSec)
            {
                try
                {
                    RpcAccountInfo account = await _rpc.GetAccountAsync(requestId);
                    if (account != null && !string.Equals(account.owner, _gameProgramPubkey.Key, StringComparison.Ordinal))
                        throw new InvalidOperationException("EncounterRequest is not owned by the Chainbound program.");
                    byte[] accountData = account?.data;

                    if (accountData != null && accountData.Length > 0)
                    {
                        var parsed = ParseEncounterRequestAccount(accountData);
                        parsed.requestId = requestId;

                        if (parsed.status == VrfRequestStatus.Fulfilled)
                        {
                            ClearPending();
                            Debug.Log($"[VrfService] Randomness fulfilled for {requestId}.");
                            return parsed;
                        }

                        if (parsed.status == VrfRequestStatus.Failed)
                        {
                            ClearPending();
                            return parsed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VrfService] Poll error: {ex.Message}");
                }

                await Task.Delay(pollIntervalMs);
            }

            Debug.LogWarning($"[VrfService] Polling timed out for {requestId}.");
            return new VrfRequestResult
            {
                requestId = requestId,
                status = VrfRequestStatus.Failed,
                errorMessage = "VRF polling timed out.",
            };
        }

        public async Task<VrfRequestResult> ConsumeRandomnessAsync(string requestId)
        {
            if (!_wallet.IsConnected || string.IsNullOrWhiteSpace(_wallet.PublicKey))
                return Failed(requestId, "Wallet not connected.");
            if (string.IsNullOrWhiteSpace(requestId))
                return Failed(requestId, "EncounterRequest PDA is required.");

            byte[] txBytes = await BuildUnsignedConsumeEncounterTx(_wallet.PublicKey, requestId);
            TxResult txResult = await _txOrch.SubmitAsync($"vrf_consume_{requestId}", txBytes);
            if (txResult.status != TxStatus.Confirmed && txResult.status != TxStatus.Finalized)
                return Failed(requestId, txResult.errorMessage ?? "VRF consume transaction failed.");

            ClearPending();
            return new VrfRequestResult
            {
                requestId = requestId,
                txSignature = txResult.txSignature,
                status = VrfRequestStatus.Consumed,
            };
        }

        public async Task<VrfRequestResult> ResumePendingAsync()
        {
            if (!HasPendingRequest)
            {
                return new VrfRequestResult
                {
                    status = VrfRequestStatus.Failed,
                    errorMessage = "No pending VRF request.",
                };
            }

            Debug.Log($"[VrfService] Resuming pending VRF request: {_pendingRequestId}");
            return await PollRandomnessAsync(_pendingRequestId);
        }

        // â”€â”€ Transaction Builder â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<byte[]> BuildUnsignedRequestEncounterTx(
            string walletPubkey, string encounterRequestPda, byte[] requestSeed)
        {
            var walletPk = new PublicKey(walletPubkey);
            var encounterPda = new PublicKey(encounterRequestPda);
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            // Anchor discriminator for "request_encounter"
            byte[] discriminator = ChainboundGameProgramContract.RequestEncounterDiscriminator;

            if (requestSeed == null || requestSeed.Length != 32)
                throw new ArgumentException("VRF request seed must be 32 bytes.", nameof(requestSeed));
            byte[] ixData = new byte[discriminator.Length + 32];
            Buffer.BlockCopy(discriminator, 0, ixData, 0, discriminator.Length);
            Buffer.BlockCopy(requestSeed, 0, ixData, discriminator.Length, 32);

            PublicKey programIdentity = DerivePda(
                new[] { Encoding.UTF8.GetBytes(PROGRAM_IDENTITY_SEED) }, _gameProgramPubkey);

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(encounterPda, false),     // EncounterRequest PDA (init)
                    AccountMeta.Writable(walletPk, true),           // payer + signer
                    AccountMeta.Writable(_oracleQueuePubkey, false),
                    AccountMeta.ReadOnly(programIdentity, false),
                    AccountMeta.ReadOnly(_vrfProgramPubkey, false),
                    AccountMeta.ReadOnly(new PublicKey(SlotHashesSysvarId), false),
                    AccountMeta.ReadOnly(SystemProgram.ProgramIdKey, false),
                },
                Data = ixData,
            };

            var tx = new Transaction
            {
                RecentBlockHash = blockhash,
                FeePayer = walletPk,
                Instructions = new List<TransactionInstruction> { ix },
                Signatures = new List<SignaturePubKeyPair>(),
            };
            return tx.Serialize();
        }

        private async Task<byte[]> BuildUnsignedConsumeEncounterTx(string walletPubkey, string requestId)
        {
            var walletPk = new PublicKey(walletPubkey);
            var requestPk = new PublicKey(requestId);
            string blockhash = await _rpc.GetLatestBlockhashAsync();
            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(requestPk, false),
                    AccountMeta.ReadOnly(walletPk, true),
                },
                Data = ChainboundGameProgramContract.ConsumeEncounterRandomnessDiscriminator,
            };
            return new Transaction
            {
                RecentBlockHash = blockhash,
                FeePayer = walletPk,
                Instructions = new List<TransactionInstruction> { ix },
                Signatures = new List<SignaturePubKeyPair>(),
            }.Serialize();
        }

        private static PublicKey DerivePda(IEnumerable<byte[]> seeds, PublicKey programId)
        {
            if (!PublicKey.TryFindProgramAddress(seeds, programId, out PublicKey pda, out _))
                throw new InvalidOperationException("Failed to derive required VRF PDA.");
            return pda;
        }

        // â”€â”€ Account Parsing â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        /// <summary>
        /// Parse EncounterRequest account data.
        /// Layout: [8 discriminator][1 status][32 randomness]
        /// Status: 0=Pending, 1=Fulfilled, 2=Consumed
        /// </summary>
        private VrfRequestResult ParseEncounterRequestAccount(byte[] data)
        {
            var result = new VrfRequestResult();

            try
            {
                if (data.Length >= 41)
                {
                    byte[] expected = ChainboundGameProgramContract.EncounterRequestAccountDiscriminator;
                    for (int i = 0; i < expected.Length; i++)
                        if (data[i] != expected[i]) return Failed(null, "Invalid EncounterRequest discriminator.");
                    byte statusByte = data[8];
                    switch (statusByte)
                    {
                        case 0:
                            result.status = VrfRequestStatus.Pending;
                            break;
                        case 1:
                            result.status = VrfRequestStatus.Fulfilled;
                            result.randomness = new byte[32];
                            Buffer.BlockCopy(data, 9, result.randomness, 0, 32);
                            break;
                        case 2:
                            result.status = VrfRequestStatus.Consumed;
                            break;
                        default:
                            result.status = VrfRequestStatus.Failed;
                            result.errorMessage = $"Unknown VRF status byte: {statusByte}";
                            break;
                    }
                }
                else
                {
                    result.status = VrfRequestStatus.Pending;
                }
            }
            catch (Exception ex)
            {
                result.status = VrfRequestStatus.Failed;
                result.errorMessage = $"Parse error: {ex.Message}";
            }

            return result;
        }

        private static VrfRequestResult Failed(string requestId, string message) => new VrfRequestResult
        {
            requestId = requestId,
            status = VrfRequestStatus.Failed,
            errorMessage = message,
        };

        private void ClearPending()
        {
            _pendingRequestId = null;
            PlayerPrefs.DeleteKey(PREFS_PENDING_VRF);
            PlayerPrefs.Save();
        }
    }
}
