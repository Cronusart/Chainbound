using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Manages the ChainlingVault PDA: captured chainling storage (max MAX_CHAINLINGS).
    /// PDA seed: ["chainling_vault", owner_pubkey]
    /// </summary>
    public class ChainlingVaultService : IChainlingVaultService
    {
        private const string VAULT_SEED = "chainling_vault";

        private readonly IRpcService _rpc;
        private readonly IChainTxOrchestrator _txOrch;
        private readonly IWalletService _wallet;
        private readonly PublicKey _gameProgramPubkey;

        private ChainlingVaultData _cachedVault;

        public ChainlingVaultData CachedVault => _cachedVault;
        public bool IsVaultLoaded => _cachedVault != null;

        public ChainlingVaultService(
            IRpcService rpc,
            IChainTxOrchestrator txOrch,
            IWalletService wallet,
            PublicKey gameProgramId)
        {
            _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            _txOrch = txOrch ?? throw new ArgumentNullException(nameof(txOrch));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _gameProgramPubkey = gameProgramId ?? throw new ArgumentNullException(nameof(gameProgramId));
        }

        // â”€â”€ PDA Derivation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public string DeriveVaultPda(string walletPubkey)
        {
            try
            {
                var walletPk = new PublicKey(walletPubkey);
                bool found = PublicKey.TryFindProgramAddress(
                    new[]
                    {
                        Encoding.UTF8.GetBytes(VAULT_SEED),
                        walletPk.KeyBytes,
                    },
                    _gameProgramPubkey,
                    out PublicKey pda,
                    out _);
                return found ? pda.Key : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChainlingVaultService] PDA derivation failed: {ex.Message}");
                return null;
            }
        }

        // â”€â”€ Create â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VaultOperationResult> CreateVaultAsync(string walletPubkey)
        {
            string vaultPda = DeriveVaultPda(walletPubkey);
            if (string.IsNullOrEmpty(vaultPda))
                return new VaultOperationResult { success = false, errorMessage = "Failed to derive ChainlingVault PDA." };

            string idempotencyKey = $"create_chainling_vault_{walletPubkey}";
            Debug.Log($"[ChainlingVaultService] Creating vault for {walletPubkey}...");

            try
            {
                byte[] txBytes = await BuildCreateVaultTx(walletPubkey, vaultPda);
                var txResult = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

                if (txResult.status == TxStatus.Finalized || txResult.status == TxStatus.Confirmed)
                {
                    _cachedVault = new ChainlingVaultData
                    {
                        owner = walletPubkey,
                        pdaAddress = vaultPda,
                    };
                    Debug.Log($"[ChainlingVaultService] Vault created: {vaultPda}");
                    return new VaultOperationResult { success = true, txResult = txResult };
                }

                return new VaultOperationResult
                {
                    success = false,
                    errorMessage = txResult.errorMessage ?? $"Create chainling vault failed: {txResult.status}",
                    txResult = txResult,
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChainlingVaultService] CreateVault failed: {ex.Message}");
                return new VaultOperationResult { success = false, errorMessage = ex.Message };
            }
        }

        // â”€â”€ Load â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<ChainlingVaultData> LoadVaultAsync(string walletPubkey)
        {
            string vaultPda = DeriveVaultPda(walletPubkey);
            if (string.IsNullOrEmpty(vaultPda))
            {
                Debug.LogWarning("[ChainlingVaultService] Cannot load: PDA derivation failed.");
                return null;
            }

            RpcAccountInfo account = await _rpc.GetAccountAsync(vaultPda);
            var walletPk = new PublicKey(walletPubkey);
            byte[] data = SolanaAccountValidation.RequireProgramPda(
                account, _gameProgramPubkey,
                new[] { Encoding.UTF8.GetBytes(VAULT_SEED), walletPk.KeyBytes },
                ChainboundGameProgramContract.ChainlingVaultAccountDiscriminator,
                847, walletPubkey, 8, 846);
            if (data == null)
            {
                Debug.Log("[ChainlingVaultService] No vault found on-chain.");
                _cachedVault = null;
                return null;
            }

            _cachedVault = DeserializeVault(data, walletPubkey, vaultPda);
            Debug.Log($"[ChainlingVaultService] Vault loaded: {_cachedVault.ChainlingCount} chainlings");
            return _cachedVault;
        }

        // â”€â”€ Add Chainling â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<ChainlingCaptureResult> AddChainlingFromCaptureAsync(ushort chainlingId, ushort level, ulong ivsSeed)
        {
            if (!ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("add_chainling_from_capture"))
                return new ChainlingCaptureResult { success = false, errorMessage = ClosedBetaFeaturePolicy.DenialFor("add_chainling_from_capture") };

            if (!_wallet.IsConnected || string.IsNullOrEmpty(_wallet.PublicKey))
                return new ChainlingCaptureResult { success = false, errorMessage = "Wallet not connected." };
            if (_cachedVault == null)
                return new ChainlingCaptureResult { success = false, errorMessage = "ChainlingVault not loaded." };
            if (!_cachedVault.CanAddChainling)
                return new ChainlingCaptureResult { success = false, errorMessage = $"Chainling vault full ({_cachedVault.chainlingCapacity} max)." };

            var entry = new ChainlingEntry
            {
                chainlingId = chainlingId,
                instanceId = _cachedVault.nextInstanceId,
                level = level,
                xp = 0,
                ivsSeed = ivsSeed,
                flags = 0,
            };

            string idempotencyKey = $"capture_chainling_{chainlingId}_{DateTime.UtcNow.Ticks}";
            Debug.Log($"[ChainlingVaultService] Capturing chainling: id={chainlingId}, instanceId={entry.instanceId}");

            try
            {
                byte[] txBytes = await BuildAddChainlingTx(_wallet.PublicKey, _cachedVault.pdaAddress, entry);
                var txResult = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

                if (txResult.status == TxStatus.Finalized || txResult.status == TxStatus.Confirmed)
                {
                    _cachedVault.chainlings.Add(entry);
                    _cachedVault.chainlingCount = (byte)_cachedVault.chainlings.Count;
                    _cachedVault.nextInstanceId++;

                    return new ChainlingCaptureResult { success = true, entry = entry, txResult = txResult };
                }

                return new ChainlingCaptureResult
                {
                    success = false,
                    errorMessage = txResult.errorMessage ?? $"Add chainling failed: {txResult.status}",
                    txResult = txResult,
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChainlingVaultService] AddChainling failed: {ex.Message}");
                return new ChainlingCaptureResult { success = false, errorMessage = ex.Message };
            }
        }

        // â”€â”€ Remove Chainling (swap-remove) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VaultOperationResult> RemoveChainlingAsync(int index)
        {
            if (!ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("remove_chainling"))
                return new VaultOperationResult { success = false, errorMessage = ClosedBetaFeaturePolicy.DenialFor("remove_chainling") };

            if (!_wallet.IsConnected || string.IsNullOrEmpty(_wallet.PublicKey))
                return new VaultOperationResult { success = false, errorMessage = "Wallet not connected." };
            if (_cachedVault == null)
                return new VaultOperationResult { success = false, errorMessage = "ChainlingVault not loaded." };
            if (index < 0 || index >= _cachedVault.ChainlingCount)
                return new VaultOperationResult { success = false, errorMessage = $"Invalid index: {index}." };

            string idempotencyKey = $"remove_chainling_{index}_{DateTime.UtcNow.Ticks}";

            try
            {
                byte[] txBytes = await BuildRemoveChainlingTx(_wallet.PublicKey, _cachedVault.pdaAddress, index);
                var txResult = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

                if (txResult.status == TxStatus.Finalized || txResult.status == TxStatus.Confirmed)
                {
                    int last = _cachedVault.chainlings.Count - 1;
                    if (index != last)
                        _cachedVault.chainlings[index] = _cachedVault.chainlings[last];
                    _cachedVault.chainlings.RemoveAt(last);
                    _cachedVault.chainlingCount = (byte)_cachedVault.chainlings.Count;

                    return new VaultOperationResult { success = true, txResult = txResult };
                }

                return new VaultOperationResult
                {
                    success = false,
                    errorMessage = txResult.errorMessage ?? $"Remove chainling failed: {txResult.status}",
                    txResult = txResult,
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ChainlingVaultService] RemoveChainling failed: {ex.Message}");
                return new VaultOperationResult { success = false, errorMessage = ex.Message };
            }
        }

        public void ClearCache()
        {
            _cachedVault = null;
        }

        // â”€â”€ Transaction Builders â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<byte[]> BuildCreateVaultTx(string walletPubkey, string vaultPda)
        {
            var walletPk = new PublicKey(walletPubkey);
            var vaultPk = new PublicKey(vaultPda);
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.InitializeChainlingVaultDiscriminator;

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = ChainboundGameProgramContract.BuildAccounts(
                    ChainboundGameProgramContract.InitializeChainlingVaultAccounts,
                    ChainboundGameProgramContract.InitializeChainlingVaultWritable,
                    ChainboundGameProgramContract.InitializeChainlingVaultSigner,
                    new Dictionary<string, PublicKey>
                    {
                        ["vault"] = vaultPk, ["authority"] = walletPk, ["system_program"] = SystemProgram.ProgramIdKey,
                    }),
                Data = discriminator,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private async Task<byte[]> BuildAddChainlingTx(string walletPubkey, string vaultPda, ChainlingEntry entry)
        {
            var walletPk = new PublicKey(walletPubkey);
            var vaultPk = new PublicKey(vaultPda);
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.AddChainlingFromCaptureDiscriminator;

            // Args: chainlingId(u16) + level(u16) + ivsSeed(u64)
            byte[] ixData = new byte[discriminator.Length + 12];
            int offset = 0;
            Buffer.BlockCopy(discriminator, 0, ixData, offset, discriminator.Length); offset += discriminator.Length;
            Buffer.BlockCopy(BitConverter.GetBytes(entry.chainlingId), 0, ixData, offset, 2); offset += 2;
            Buffer.BlockCopy(BitConverter.GetBytes(entry.level), 0, ixData, offset, 2); offset += 2;
            Buffer.BlockCopy(BitConverter.GetBytes(entry.ivsSeed), 0, ixData, offset, 8);

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(vaultPk, false),
                    AccountMeta.Writable(walletPk, true),
                },
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private async Task<byte[]> BuildRemoveChainlingTx(string walletPubkey, string vaultPda, int index)
        {
            var walletPk = new PublicKey(walletPubkey);
            var vaultPk = new PublicKey(vaultPda);
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.RemoveChainlingDiscriminator;

            byte[] ixData = new byte[discriminator.Length + 1];
            Buffer.BlockCopy(discriminator, 0, ixData, 0, discriminator.Length);
            ixData[discriminator.Length] = (byte)index;

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(vaultPk, false),
                    AccountMeta.Writable(walletPk, true),
                },
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private static byte[] BuildUnsignedTx(string blockhash, PublicKey feePayer, TransactionInstruction ix)
        {
            var tx = new Transaction
            {
                RecentBlockHash = blockhash,
                FeePayer = feePayer,
                Instructions = new List<TransactionInstruction> { ix },
                Signatures = new List<SignaturePubKeyPair>(),
            };
            return tx.Serialize();
        }

        // â”€â”€ Deserialization â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private ChainlingVaultData DeserializeVault(byte[] data, string walletPubkey, string pda)
        {
            var vault = new ChainlingVaultData
            {
                owner = walletPubkey,
                pdaAddress = pda,
            };

            try
            {
                int offset = 8; // Skip Anchor discriminator

                // owner: 32 bytes (skip)
                if (data.Length >= offset + 32) offset += 32;

                // chainling_capacity: u8, chainling_count: u8
                if (data.Length >= offset + 2)
                {
                    vault.chainlingCapacity = data[offset++];
                    vault.chainlingCount = data[offset++];
                }

                // ChainlingEntry: chainlingId(u16) + instanceId(u32) + level(u16) + xp(u32)
                //   + ivsSeed(u64) + flags(u16) + pendingLevelReward(u8) + pendingRewardSeed(u64)
                //   + claimedRewardBits(u8) = 32 bytes
                const int entrySize = 32;
                int count = Math.Min(vault.chainlingCount, vault.chainlingCapacity);
                for (int i = 0; i < count && offset + entrySize <= data.Length; i++)
                {
                    var entry = new ChainlingEntry
                    {
                        chainlingId = BitConverter.ToUInt16(data, offset),
                        instanceId = BitConverter.ToUInt32(data, offset + 2),
                        level = BitConverter.ToUInt16(data, offset + 6),
                        xp = BitConverter.ToUInt32(data, offset + 8),
                        ivsSeed = BitConverter.ToUInt64(data, offset + 12),
                        flags = BitConverter.ToUInt16(data, offset + 20),
                        pendingLevelReward = data[offset + 22],
                        pendingRewardSeed = BitConverter.ToUInt64(data, offset + 23),
                        claimedRewardBits = data[offset + 31],
                    };
                    vault.chainlings.Add(entry);
                    offset += entrySize;
                }

                // Skip remaining empty entries in fixed array
                int remaining = vault.chainlingCapacity - count;
                offset += remaining * entrySize;

                // next_instance_id: u32
                if (data.Length >= offset + 4)
                {
                    vault.nextInstanceId = BitConverter.ToUInt32(data, offset);
                    offset += 4;
                }

                // bump: u8
                if (data.Length > offset)
                    vault.bump = data[offset];
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ChainlingVaultService] Deserialization partial: {ex.Message}");
            }

            return vault;
        }
    }
}
