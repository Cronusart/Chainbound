using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>Validated read/create client for zero-modifier Known MoveVault records.</summary>
    public sealed class MoveVaultService : IMoveVaultService
    {
        private const string MoveVaultSeed = "move_vault";
        private const int KnownEntrySize = 49;
        private readonly IRpcService rpc;
        private readonly IChainTxOrchestrator transactions;
        private readonly PublicKey gameProgram;
        private PlayerMoveVault cachedVault;

        public MoveVaultService(
            IRpcService rpc,
            IChainTxOrchestrator transactions,
            IWalletService wallet,
            IProfileService profile,
            PublicKey gameProgram)
        {
            this.rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            this.transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
            _ = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _ = profile ?? throw new ArgumentNullException(nameof(profile));
            this.gameProgram = gameProgram ?? throw new ArgumentNullException(nameof(gameProgram));
        }

        public PlayerMoveVault CachedVault => cachedVault;
        public bool IsVaultLoaded => cachedVault != null;

        public string DeriveVaultPda(string walletAddress)
        {
            var wallet = new PublicKey(walletAddress);
            return PublicKey.TryFindProgramAddress(
                new[] { Encoding.UTF8.GetBytes(MoveVaultSeed), wallet.KeyBytes },
                gameProgram,
                out PublicKey pda,
                out _) ? pda.Key : null;
        }

        public async Task<VaultOperationResult> CreateVaultAsync(string walletAddress)
        {
            var wallet = new PublicKey(walletAddress);
            var vault = new PublicKey(DeriveVaultPda(walletAddress));
            string blockhash = await rpc.GetLatestBlockhashAsync();
            var instruction = new TransactionInstruction
            {
                ProgramId = gameProgram.KeyBytes,
                Keys = ChainboundGameProgramContract.BuildAccounts(
                    ChainboundGameProgramContract.CreateMoveVaultAccounts,
                    ChainboundGameProgramContract.CreateMoveVaultWritable,
                    ChainboundGameProgramContract.CreateMoveVaultSigner,
                    new Dictionary<string, PublicKey>
                    {
                        ["vault"] = vault,
                        ["authority"] = wallet,
                        ["system_program"] = SystemProgram.ProgramIdKey,
                    }),
                Data = ChainboundGameProgramContract.CreateMoveVaultDiscriminator,
            };
            var transaction = new Transaction
            {
                RecentBlockHash = blockhash,
                FeePayer = wallet,
                Instructions = new List<TransactionInstruction> { instruction },
                Signatures = new List<SignaturePubKeyPair>(),
            };
            TxResult result = await transactions.SubmitAsync("create_known_vault_" + walletAddress, transaction.Serialize());
            if (result.status != TxStatus.Confirmed && result.status != TxStatus.Finalized)
                return new VaultOperationResult { errorMessage = result.errorMessage, txResult = result };

            cachedVault = new PlayerMoveVault { owner = walletAddress, pdaAddress = vault.Key };
            return new VaultOperationResult { success = true, txResult = result, updatedVault = cachedVault };
        }

        public async Task<PlayerMoveVault> LoadVaultAsync(string walletAddress)
        {
            string pda = DeriveVaultPda(walletAddress);
            RpcAccountInfo account = await rpc.GetAccountAsync(pda);
            var wallet = new PublicKey(walletAddress);
            byte[] data = SolanaAccountValidation.RequireProgramPda(
                account,
                gameProgram,
                new[] { Encoding.UTF8.GetBytes(MoveVaultSeed), wallet.KeyBytes },
                ChainboundGameProgramContract.MoveVaultAccountDiscriminator,
                55,
                walletAddress,
                8);
            if (data == null) return cachedVault = null;
            cachedVault = Deserialize(data, walletAddress, pda);
            return cachedVault;
        }

        public void ClearCache() => cachedVault = null;

        private static PlayerMoveVault Deserialize(byte[] data, string owner, string pda)
        {
            int offset = 8 + 32;
            var vault = new PlayerMoveVault
            {
                owner = owner,
                pdaAddress = pda,
                knownCapacity = data[offset++],
                knownCount = data[offset++],
            };
            uint vectorCount = BitConverter.ToUInt32(data, offset);
            offset += 4;
            if (vectorCount != vault.knownCount || vectorCount > vault.knownCapacity)
                throw new InvalidOperationException("MoveVault Known vector count is invalid.");
            if (data.Length < offset + (vectorCount * KnownEntrySize) + 9)
                throw new InvalidOperationException("MoveVault Known vector is truncated.");

            for (int i = 0; i < vectorCount; i++)
            {
                ulong instanceId = BitConverter.ToUInt64(data, offset); offset += 8;
                ushort moveId = BitConverter.ToUInt16(data, offset); offset += 2;
                byte element = data[offset++];
                ushort basePower = BitConverter.ToUInt16(data, offset); offset += 2;
                uint generatorVersion = BitConverter.ToUInt32(data, offset); offset += 4;
                var poolHash = new byte[32];
                Buffer.BlockCopy(data, offset, poolHash, 0, poolHash.Length); offset += poolHash.Length;
                vault.knownMoves.Add(new KnownMoveVaultEntry(
                    instanceId, moveId, element, basePower, generatorVersion, poolHash));
            }

            vault.nextInstanceId = BitConverter.ToUInt64(data, offset); offset += 8;
            vault.bump = data[offset];
            return vault;
        }
    }
}
