using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Chainbound.Solana;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Production ProfileService.
    /// Derives PlayerProfile PDA via PublicKey.TryFindProgramAddress,
    /// creates/reads/updates on-chain using real unsigned Solana transactions.
    /// Caches locally; refreshes on connect and after tx finalize.
    /// </summary>
    public class ProfileService : IProfileService
    {
        private const string PROFILE_SEED = "player_profile";
        private const string PREFS_PROFILE_CACHE = "ProfileSvc_CachedProfile";

        private readonly IRpcService _rpc;
        private readonly IChainTxOrchestrator _txOrch;
        private readonly PublicKey _gameProgramPubkey;

        private PlayerProfileData _cachedProfile;

        public bool IsProfileLoaded => _cachedProfile != null;
        public PlayerProfileData CachedProfile => _cachedProfile;

        public ProfileService(IRpcService rpc, IChainTxOrchestrator txOrch, PublicKey gameProgramId)
        {
            _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
            _txOrch = txOrch ?? throw new ArgumentNullException(nameof(txOrch));
            _gameProgramPubkey = gameProgramId ?? throw new ArgumentNullException(nameof(gameProgramId));
        }

        // â”€â”€ PDA Derivation (real) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public string DerivePdaAddress(string walletPubkey)
        {
            try
            {
                var walletPk = new PublicKey(walletPubkey);
                bool found = PublicKey.TryFindProgramAddress(
                    new[] { Encoding.UTF8.GetBytes(PROFILE_SEED), walletPk.KeyBytes },
                    _gameProgramPubkey,
                    out PublicKey pda,
                    out _);
                return found ? pda.Key : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProfileService] PDA derivation failed: {ex.Message}");
                return null;
            }
        }

        // â”€â”€ Read Operations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<bool> HasProfileAsync(string walletPubkey)
        {
            string pda = DerivePdaAddress(walletPubkey);
            if (string.IsNullOrEmpty(pda)) return false;

            RpcAccountInfo account = await _rpc.GetAccountAsync(pda);
            byte[] data = ValidateProfileAccount(account, walletPubkey);
            return data != null;
        }

        public async Task<PlayerProfileData> LoadProfileAsync(string walletPubkey)
        {
            string pda = DerivePdaAddress(walletPubkey);
            if (string.IsNullOrEmpty(pda))
            {
                Debug.LogWarning("[ProfileService] Cannot load: PDA derivation failed.");
                return null;
            }

            Debug.Log($"[ProfileService] Loading profile from {pda}...");

            RpcAccountInfo account = await _rpc.GetAccountAsync(pda);
            byte[] data = ValidateProfileAccount(account, walletPubkey);
            if (data == null)
            {
                Debug.Log("[ProfileService] No profile found on-chain.");
                _cachedProfile = null;
                return null;
            }

            _cachedProfile = DeserializeProfile(data, walletPubkey, pda);
            SaveProfileToLocalCache(_cachedProfile);

            Debug.Log($"[ProfileService] Profile loaded: wins={_cachedProfile.wins}, losses={_cachedProfile.losses}");
            return _cachedProfile;
        }

        // â”€â”€ Write Operations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<TxResult> CreateProfileAsync(string walletPubkey)
        {
            string idempotencyKey = $"create_profile_{walletPubkey}";

            Debug.Log($"[ProfileService] Creating profile for {walletPubkey}...");

            byte[] txBytes = await BuildUnsignedCreateProfileTx(walletPubkey);
            var result = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

            if (result.status == TxStatus.Finalized || result.status == TxStatus.Confirmed)
            {
                await LoadProfileAsync(walletPubkey);
                Debug.Log("[ProfileService] Profile created and loaded.");
            }

            return result;
        }

        public async Task<TxResult> UpdateWinsLossesAsync(bool isWin)
        {
            if (!ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("update_wins_losses"))
                return new TxResult { status = TxStatus.Failed, errorMessage = ClosedBetaFeaturePolicy.DenialFor("update_wins_losses") };

            if (_cachedProfile == null)
            {
                Debug.LogWarning("[ProfileService] Cannot update: no cached profile.");
                return new TxResult { status = TxStatus.Failed, errorMessage = "No profile loaded." };
            }

            string idempotencyKey = $"update_wl_{_cachedProfile.owner}_{DateTime.UtcNow.Ticks}";
            byte[] txBytes = await BuildUnsignedUpdateWinsLossesTx(_cachedProfile.owner, isWin);
            var result = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

            if (result.status == TxStatus.Finalized || result.status == TxStatus.Confirmed)
            {
                if (isWin) _cachedProfile.wins++;
                else _cachedProfile.losses++;
                SaveProfileToLocalCache(_cachedProfile);
            }

            return result;
        }

        public async Task<TxResult> UpdateLastPositionAsync(uint sceneId, float x, float y, float z)
        {
            if (_cachedProfile == null)
            {
                return new TxResult { status = TxStatus.Failed, errorMessage = "No profile loaded." };
            }

            string idempotencyKey = $"update_pos_{_cachedProfile.owner}_{DateTime.UtcNow.Ticks}";
            byte[] txBytes = await BuildUnsignedUpdatePositionTx(_cachedProfile.owner, sceneId, x, y, z);
            var result = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

            if (result.status == TxStatus.Finalized || result.status == TxStatus.Confirmed)
            {
                _cachedProfile.lastKnownSceneId = sceneId;
                _cachedProfile.SetQuantizedPosition(x, y, z);
                SaveProfileToLocalCache(_cachedProfile);
            }

            return result;
        }

        public void ClearCache()
        {
            _cachedProfile = null;
            PlayerPrefs.DeleteKey(PREFS_PROFILE_CACHE);
        }

        // â”€â”€ RAM Operations â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        public async Task<TxResult> SpendRamAsync(int amount, string reason)
        {
            if (!ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("spend_ram"))
                return new TxResult { status = TxStatus.Failed, errorMessage = ClosedBetaFeaturePolicy.DenialFor("spend_ram") };

            if (_cachedProfile == null)
                return new TxResult { status = TxStatus.Failed, errorMessage = "No profile loaded." };

            if (!_cachedProfile.CanAffordRam(amount))
                return new TxResult { status = TxStatus.Failed, errorMessage = $"Insufficient RAM: need {amount}, have {_cachedProfile.ramBalance}." };

            string idempotencyKey = $"spend_ram_{_cachedProfile.owner}_{reason}_{DateTime.UtcNow.Ticks}";
            byte[] txBytes = await BuildUnsignedSpendRamTx(_cachedProfile.owner, (uint)amount);
            var result = await _txOrch.SubmitAsync(idempotencyKey, txBytes);

            if (result.status == TxStatus.Finalized || result.status == TxStatus.Confirmed)
            {
                _cachedProfile.ramBalance -= (uint)amount;
                SaveProfileToLocalCache(_cachedProfile);
            }

            return result;
        }

        public Task<TxResult> CreditRamFromBurnAsync(int amount)
        {
            return Task.FromResult(new TxResult
            {
                status = TxStatus.Failed,
                errorMessage = "Standalone RAM credit is disabled. Burn an Known through MoveVaultService so the burn and reward are atomic.",
            });
        }

        public void CheckAndApplyDailyReset()
        {
            if (_cachedProfile == null) return;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_cachedProfile.NeedsDailyReset(now))
            {
                _cachedProfile.ResetDaily(now);
            }
        }

        // â”€â”€ Unsigned Transaction Builders â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private async Task<byte[]> BuildUnsignedCreateProfileTx(string walletPubkey)
        {
            var walletPk = new PublicKey(walletPubkey);
            var profilePda = new PublicKey(DerivePdaAddress(walletPubkey));
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] ixData = ChainboundGameProgramContract.CreateProfileDiscriminator;

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = ChainboundGameProgramContract.BuildAccounts(
                    ChainboundGameProgramContract.CreateProfileAccounts,
                    ChainboundGameProgramContract.CreateProfileWritable,
                    ChainboundGameProgramContract.CreateProfileSigner,
                    new Dictionary<string, PublicKey>
                    {
                        ["profile"] = profilePda,
                        ["authority"] = walletPk,
                        ["system_program"] = SystemProgram.ProgramIdKey,
                    }),
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private async Task<byte[]> BuildUnsignedUpdateWinsLossesTx(string walletPubkey, bool isWin)
        {
            var walletPk = new PublicKey(walletPubkey);
            var profilePda = new PublicKey(DerivePdaAddress(walletPubkey));
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.UpdateWinsLossesDiscriminator;
            byte[] ixData = new byte[discriminator.Length + 1];
            Buffer.BlockCopy(discriminator, 0, ixData, 0, discriminator.Length);
            ixData[discriminator.Length] = isWin ? (byte)1 : (byte)0;

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profilePda, false),
                    AccountMeta.Writable(walletPk, true),
                },
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private async Task<byte[]> BuildUnsignedUpdatePositionTx(string walletPubkey, uint sceneId, float x, float y, float z)
        {
            var walletPk = new PublicKey(walletPubkey);
            var profilePda = new PublicKey(DerivePdaAddress(walletPubkey));
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.UpdatePositionDiscriminator;
            byte[] ixData = new byte[discriminator.Length + 10]; // 4 (sceneId) + 2*3 (quantized xyz)
            Buffer.BlockCopy(discriminator, 0, ixData, 0, discriminator.Length);
            int offset = discriminator.Length;
            Buffer.BlockCopy(BitConverter.GetBytes(sceneId), 0, ixData, offset, 4);
            offset += 4;
            Buffer.BlockCopy(BitConverter.GetBytes((short)(x * 100)), 0, ixData, offset, 2);
            offset += 2;
            Buffer.BlockCopy(BitConverter.GetBytes((short)(y * 100)), 0, ixData, offset, 2);
            offset += 2;
            Buffer.BlockCopy(BitConverter.GetBytes((short)(z * 100)), 0, ixData, offset, 2);

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = ChainboundGameProgramContract.BuildAccounts(
                    ChainboundGameProgramContract.UpdatePositionAccounts,
                    ChainboundGameProgramContract.UpdatePositionWritable,
                    ChainboundGameProgramContract.UpdatePositionSigner,
                    new Dictionary<string, PublicKey> { ["profile"] = profilePda, ["authority"] = walletPk }),
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        private async Task<byte[]> BuildUnsignedSpendRamTx(string walletPubkey, uint amount)
        {
            var walletPk = new PublicKey(walletPubkey);
            var profilePda = new PublicKey(DerivePdaAddress(walletPubkey));
            string blockhash = await _rpc.GetLatestBlockhashAsync();

            byte[] discriminator = ChainboundGameProgramContract.SpendRamDiscriminator;
            byte[] ixData = new byte[discriminator.Length + 4];
            Buffer.BlockCopy(discriminator, 0, ixData, 0, discriminator.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(amount), 0, ixData, discriminator.Length, 4);

            var ix = new TransactionInstruction
            {
                ProgramId = _gameProgramPubkey.KeyBytes,
                Keys = new List<AccountMeta>
                {
                    AccountMeta.Writable(profilePda, false),
                    AccountMeta.Writable(walletPk, true),
                },
                Data = ixData,
            };

            return BuildUnsignedTx(blockhash, walletPk, ix);
        }

        // â”€â”€ Shared Helpers â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

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

        private PlayerProfileData DeserializeProfile(byte[] data, string walletPubkey, string pda)
        {
            var profile = new PlayerProfileData
            {
                owner = walletPubkey,
                pdaAddress = pda,
            };

            try
            {
                if (data.Length >= 28)
                {
                    int offset = 8 + 32; // Anchor discriminator + embedded owner
                    profile.schemaVersion = BitConverter.ToUInt16(data, offset); offset += 2;
                    profile.createdAtSlot = BitConverter.ToUInt64(data, offset); offset += 8;
                    profile.wins = BitConverter.ToUInt32(data, offset); offset += 4;
                    profile.losses = BitConverter.ToUInt32(data, offset); offset += 4;

                    if (data.Length >= offset + 10)
                    {
                        profile.lastKnownSceneId = BitConverter.ToUInt32(data, offset); offset += 4;
                        profile.lastKnownPosX = BitConverter.ToInt16(data, offset); offset += 2;
                        profile.lastKnownPosY = BitConverter.ToInt16(data, offset); offset += 2;
                        profile.lastKnownPosZ = BitConverter.ToInt16(data, offset); offset += 2;
                    }

                    // RAM fields (schema v2+)
                    if (data.Length >= offset + 23)
                    {
                        profile.ramBalance = BitConverter.ToUInt32(data, offset); offset += 4;
                        profile.dailyRamEarned = BitConverter.ToUInt16(data, offset); offset += 2;
                        profile.lastRamResetTimestamp = BitConverter.ToInt64(data, offset); offset += 8;
                        profile.ramLifetimeEarned = BitConverter.ToUInt64(data, offset); offset += 8;
                        profile.bump = data[offset]; offset += 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProfileService] Deserialization partial: {ex.Message}");
            }

            return profile;
        }

        private byte[] ValidateProfileAccount(RpcAccountInfo account, string walletPubkey)
        {
            var walletPk = new PublicKey(walletPubkey);
            return SolanaAccountValidation.RequireProgramPda(
                account,
                _gameProgramPubkey,
                new[] { Encoding.UTF8.GetBytes(PROFILE_SEED), walletPk.KeyBytes },
                ChainboundGameProgramContract.PlayerProfileAccountDiscriminator,
                91,
                walletPubkey,
                8,
                90);
        }

        // â”€â”€ Local Cache Persistence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

        private void SaveProfileToLocalCache(PlayerProfileData profile)
        {
            if (profile == null) return;
            string json = JsonUtility.ToJson(profile);
            PlayerPrefs.SetString(PREFS_PROFILE_CACHE, json);
            PlayerPrefs.Save();
        }

        public PlayerProfileData LoadFromLocalCache()
        {
            string json = PlayerPrefs.GetString(PREFS_PROFILE_CACHE, "");
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonUtility.FromJson<PlayerProfileData>(json);
            }
            catch
            {
                return null;
            }
        }
    }
}
