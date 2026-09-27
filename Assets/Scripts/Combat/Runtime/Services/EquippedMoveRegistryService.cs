using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Manages EquippedMoveRegistry PDAs (one per chainling instance).
    /// PDA seed: ["equipped_moves", owner_pubkey, chainling_instance_id_le_bytes]
    /// </summary>
    public class EquippedMoveRegistryService : IEquippedMoveRegistryService
    {
        private const string REGISTRY_SEED = "equipped_moves";

        private readonly IRpcService _rpc;
        private readonly IChainTxOrchestrator _txOrch;
        private readonly IWalletService _wallet;
        private readonly PublicKey _gameProgramPubkey;

        private readonly Dictionary<uint, EquippedMoveRegistryData> _cache = new Dictionary<uint, EquippedMoveRegistryData>();

        public EquippedMoveRegistryService(
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

        public string DeriveRegistryPda(string walletPubkey, uint chainlingInstanceId)
        {
            try
            {
                var walletPk = new PublicKey(walletPubkey);
                byte[] instanceIdBytes = BitConverter.GetBytes(chainlingInstanceId);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(instanceIdBytes);

                bool found = PublicKey.TryFindProgramAddress(
                    new[]
                    {
                        Encoding.UTF8.GetBytes(REGISTRY_SEED),
                        walletPk.KeyBytes,
                        instanceIdBytes,
                    },
                    _gameProgramPubkey,
                    out PublicKey pda,
                    out _);
                return found ? pda.Key : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquippedMoveRegistryService] PDA derivation failed: {ex.Message}");
                return null;
            }
        }

        // â”€â”€ Create â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VaultOperationResult> CreateRegistryAsync(string walletPubkey, uint chainlingInstanceId)
        {
            string registryPda = DeriveRegistryPda(walletPubkey, chainlingInstanceId);
            if (string.IsNullOrEmpty(registryPda))
                return new VaultOperationResult { success = false, errorMessage = "Failed to derive EquippedMoveRegistry PDA." };

            string idempotencyKey = $"create_emr_{walletPubkey}_{chainlingInstanceId}";
            Debug.Log($"[EquippedMoveRegistryService] Creating registry for instanceId={chainlingInstanceId}...");

            try
            {
                byte[] txBytes = await BuildCreateRegistryTx(walletPubkey, registryPda, chainlingInstanceId);
                var txResult = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

                if (txResult.status == TxStatus.Finalized || txResult.status == TxStatus.Confirmed)
                {
                    var registry = new EquippedMoveRegistryData
                    {
                        owner = walletPubkey,
                        chainlingInstanceId = chainlingInstanceId,
                        pdaAddress = registryPda,
                    };
                    _cache[chainlingInstanceId] = registry;
                    Debug.Log($"[EquippedMoveRegistryService] Registry created: {registryPda}");
                    return new VaultOperationResult { success = true, txResult = txResult };
                }

                return new VaultOperationResult
                {
                    success = false,
                    errorMessage = txResult.errorMessage ?? $"Create registry failed: {txResult.status}",
                    txResult = txResult,
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquippedMoveRegistryService] CreateRegistry failed: {ex.Message}");
                return new VaultOperationResult { success = false, errorMessage = ex.Message };
            }
        }

        // â”€â”€ Load â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<EquippedMoveRegistryData> LoadRegistryAsync(string walletPubkey, uint chainlingInstanceId)
        {
            string registryPda = DeriveRegistryPda(walletPubkey, chainlingInstanceId);
            if (string.IsNullOrEmpty(registryPda))
            {
                Debug.LogWarning("[EquippedMoveRegistryService] Cannot load: PDA derivation failed.");
                return null;
            }

            RpcAccountInfo account = await _rpc.GetAccountAsync(registryPda);
            var walletPk = new PublicKey(walletPubkey);
            byte[] instanceIdBytes = BitConverter.GetBytes(chainlingInstanceId);
            byte[] data = SolanaAccountValidation.RequireProgramPda(
                account, _gameProgramPubkey,
                new[] { Encoding.UTF8.GetBytes(REGISTRY_SEED), walletPk.KeyBytes, instanceIdBytes },
                ChainboundGameProgramContract.EquippedMoveRegistryAccountDiscriminator,
                765, walletPubkey, 8, 764);
            if (data == null)
            {
                Debug.Log($"[EquippedMoveRegistryService] No registry found for instanceId={chainlingInstanceId}.");
                return null;
            }

            var registry = DeserializeRegistry(data, walletPubkey, chainlingInstanceId, registryPda);
            _cache[chainlingInstanceId] = registry;
            Debug.Log($"[EquippedMoveRegistryService] Registry loaded for instanceId={chainlingInstanceId}: {registry.OccupiedSlotCount} slots occupied");
            return registry;
        }

        // â”€â”€ Set Slot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VaultOperationResult> SetSlotAsync(string walletPubkey, uint chainlingInstanceId, int slotIndex, EquippedMoveSlot slot)
        {
            if (!ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("set_equipped_move"))
                return new VaultOperationResult { success = false, errorMessage = ClosedBetaFeaturePolicy.DenialFor("set_equipped_move") };

            if (slotIndex < 0 || slotIndex >= CombatConstants.EQUIPPED_SLOTS_ON_CHAIN)
                return new VaultOperationResult { success = false, errorMessage = $"Invalid slot index: {slotIndex}." };

            string registryPda = DeriveRegistryPda(walletPubkey, chainlingInstanceId);
            if (string.IsNullOrEmpty(registryPda))
                return new VaultOperationResult { success = false, errorMessage = "Failed to derive registry PDA." };

            string idempotencyKey = $"set_slot_{chainlingInstanceId}_{slotIndex}_{DateTime.UtcNow.Ticks}";

            try
            {
                byte[] txBytes = await BuildSetSlotTx(walletPubkey, registryPda, chainlingInstanceId, slotIndex, slot);
                var txResult = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

                if (txResult.status == TxStatus.Finalized || txResult.status == TxStatus.Confirmed)
                {
                    if (_cache.TryGetValue(chainlingInstanceId, out var registry))
                        registry.slots[slotIndex] = slot;

                    return new VaultOperationResult { success = true, txResult = txResult };
                }

                return new VaultOperationResult
                {
                    success = false,
                    errorMessage = txResult.errorMessage ?? $"Set slot failed: {txResult.status}",
                    txResult = txResult,
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EquippedMoveRegistryService] SetSlot failed: {ex.Message}");
                return new VaultOperationResult { success = false, errorMessage = ex.Message };
            }
        }

        // â”€â”€ Clear Slot â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<VaultOperationResult> ClearSlotAsync(string walletPubkey, uint chainlingInstanceId, int slotIndex)
        {
            return await SetSlotAsync(walletPubkey, chainlingInstanceId, slotIndex, EquippedMoveSlot.Empty);
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        // â”€â”€ Transaction Builders â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<byte[]> BuildCreateRegistryTx(string walletPubkey, string registryPda, uint chainlingInstanceId)
        {
            var walletPk = new PublicKey(walletPubkey);
            var registryPk = new PublicKey(registryPda);
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.InitializeEquippedRegistryDiscriminator;

            byte[] ixData = new byte[discriminator.Length + 4];
            Buffer.BlockCopy(discriminator, 0, ixData, 0, discriminator.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(chainlingInstanceId), 0, ixData, discriminator.Length, 4);

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = ChainboundGameProgramContract.BuildAccounts(
                    ChainboundGameProgramContract.InitializeEquippedRegistryAccounts,
                    ChainboundGameProgramContract.InitializeEquippedRegistryWritable,
                    ChainboundGameProgramContract.InitializeEquippedRegistrySigner,
                    new Dictionary<string, PublicKey>
                    {
                        ["registry"] = registryPk, ["authority"] = walletPk, ["system_program"] = SystemProgram.ProgramIdKey,
                    }),
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private async Task<byte[]> BuildSetSlotTx(string walletPubkey, string registryPda, uint chainlingInstanceId, int slotIndex, EquippedMoveSlot slot)
        {
            var walletPk = new PublicKey(walletPubkey);
            var registryPk = new PublicKey(registryPda);
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.SetEquippedSlotDiscriminator;

            // Args: slotIndex(u8) + tier(u8) + sourceKind(u8) + knownIndex(u8) + assetId(32) + moveId(u16) + modCache(u64) = 46
            byte[] ixData = new byte[discriminator.Length + 46];
            int offset = 0;
            Buffer.BlockCopy(discriminator, 0, ixData, offset, discriminator.Length); offset += discriminator.Length;
            ixData[offset++] = (byte)slotIndex;
            ixData[offset++] = slot.tier;
            ixData[offset++] = slot.sourceKind;
            ixData[offset++] = slot.knownIndex;
            if (slot.assetId != null && slot.assetId.Length == 32)
                Buffer.BlockCopy(slot.assetId, 0, ixData, offset, 32);
            offset += 32;
            Buffer.BlockCopy(BitConverter.GetBytes(slot.moveId), 0, ixData, offset, 2); offset += 2;
            Buffer.BlockCopy(BitConverter.GetBytes(slot.modCache), 0, ixData, offset, 8);

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(registryPk, false),
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

        private EquippedMoveRegistryData DeserializeRegistry(byte[] data, string walletPubkey, uint chainlingInstanceId, string pda)
        {
            var registry = new EquippedMoveRegistryData
            {
                owner = walletPubkey,
                chainlingInstanceId = chainlingInstanceId,
                pdaAddress = pda,
            };

            try
            {
                int offset = 8; // Skip Anchor discriminator

                // owner: 32 bytes (skip)
                if (data.Length >= offset + 32) offset += 32;

                // chainling_instance_id: u32
                if (data.Length >= offset + 4)
                {
                    registry.chainlingInstanceId = BitConverter.ToUInt32(data, offset);
                    offset += 4;
                }

                // slots: [EquippedMoveSlot; EQUIPPED_SLOTS_ON_CHAIN]
                // Each slot: tier(1) + sourceKind(1) + knownIndex(1) + assetId(32) + moveId(2) + modCache(8) = 45 bytes
                const int slotSize = 45;
                for (int i = 0; i < CombatConstants.EQUIPPED_SLOTS_ON_CHAIN && offset + slotSize <= data.Length; i++)
                {
                    var slot = new EquippedMoveSlot
                    {
                        tier = data[offset],
                        sourceKind = data[offset + 1],
                        knownIndex = data[offset + 2],
                        assetId = new byte[32],
                        moveId = BitConverter.ToUInt16(data, offset + 35),
                        modCache = BitConverter.ToUInt64(data, offset + 37),
                    };
                    Buffer.BlockCopy(data, offset + 3, slot.assetId, 0, 32);
                    registry.slots[i] = slot;
                    offset += slotSize;
                }

                // bump: u8
                if (data.Length > offset)
                    registry.bump = data[offset];
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EquippedMoveRegistryService] Deserialization partial: {ex.Message}");
            }

            return registry;
        }
    }
}
