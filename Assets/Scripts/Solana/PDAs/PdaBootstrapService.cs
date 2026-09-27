using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Called on wallet connect to ensure all player PDAs exist and are loaded.
    /// Flow:
    ///   1. Derive + load PlayerProfile; init if missing
    ///   2. Derive + load MoveVault; init if missing
    ///   3. Derive + load ChainlingVault; init if missing
    ///   4. For each ChainlingEntry: derive + load EquippedMoveRegistry; init if missing
    ///   5. Populate PlayerStateCache
    /// </summary>
    public class PdaBootstrapService
    {
        private readonly IProfileService _profileService;
        private readonly IMoveVaultService _moveVaultService;
        private readonly IChainlingVaultService _chainlingVaultService;
        private readonly IEquippedMoveRegistryService _registryService;
        private readonly PlayerStateCache _cache;

        public PdaBootstrapService(
            IProfileService profileService,
            IMoveVaultService moveVaultService,
            IChainlingVaultService chainlingVaultService,
            IEquippedMoveRegistryService registryService,
            PlayerStateCache cache)
        {
            _profileService = profileService ?? throw new ArgumentNullException(nameof(profileService));
            _moveVaultService = moveVaultService ?? throw new ArgumentNullException(nameof(moveVaultService));
            _chainlingVaultService = chainlingVaultService ?? throw new ArgumentNullException(nameof(chainlingVaultService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Bootstrap all PDAs for the given wallet. Creates any that are missing.
        /// </summary>
        public async Task BootstrapAsync(string walletPubkey)
        {
            Debug.Log($"[PdaBootstrapService] Starting bootstrap for {walletPubkey}...");
            _cache.Clear();

            try
            {
                // 1. PlayerProfile
                var profile = await EnsureProfileAsync(walletPubkey);
                _cache.Profile = profile;

                // 2. MoveVault
                var moveVault = await EnsureMoveVaultAsync(walletPubkey);
                _cache.MoveVault = moveVault;

                // 3. ChainlingVault
                var chainlingVault = await EnsureChainlingVaultAsync(walletPubkey);
                _cache.ChainlingVault = chainlingVault;

                // 4. EquippedMoveRegistry for each captured chainling
                if (chainlingVault != null && chainlingVault.chainlings != null)
                {
                    foreach (var entry in chainlingVault.chainlings)
                    {
                        var registry = await EnsureEquippedRegistryAsync(walletPubkey, entry.instanceId);
                        if (registry != null)
                            _cache.EquippedRegistries[entry.instanceId] = registry;
                    }
                }

                // Apply daily reset if needed
                _profileService.CheckAndApplyDailyReset();

                Debug.Log($"[PdaBootstrapService] Bootstrap complete. " +
                          $"Profile={profile != null}, MoveVault={moveVault != null}, " +
                          $"ChainlingVault={chainlingVault?.ChainlingCount ?? 0} chainlings, " +
                          $"Registries={_cache.EquippedRegistries.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PdaBootstrapService] Bootstrap failed: {ex.Message}");
            }
        }

        private async Task<PlayerProfileData> EnsureProfileAsync(string walletPubkey)
        {
            bool exists = await _profileService.HasProfileAsync(walletPubkey);
            if (!exists)
            {
                Debug.Log("[PdaBootstrapService] Profile not found; creating...");
                var createResult = await _profileService.CreateProfileAsync(walletPubkey);
                if (createResult.status != TxStatus.Finalized && createResult.status != TxStatus.Confirmed)
                {
                    Debug.LogError($"[PdaBootstrapService] Profile creation failed: {createResult.errorMessage}");
                    return null;
                }
            }
            return await _profileService.LoadProfileAsync(walletPubkey);
        }

        private async Task<PlayerMoveVault> EnsureMoveVaultAsync(string walletPubkey)
        {
            var vault = await _moveVaultService.LoadVaultAsync(walletPubkey);
            if (vault == null)
            {
                Debug.Log("[PdaBootstrapService] MoveVault not found; creating...");
                var createResult = await _moveVaultService.CreateVaultAsync(walletPubkey);
                if (createResult.success)
                    vault = await _moveVaultService.LoadVaultAsync(walletPubkey);
                else
                    Debug.LogError($"[PdaBootstrapService] MoveVault creation failed: {createResult.errorMessage}");
            }
            return vault;
        }

        private async Task<ChainlingVaultData> EnsureChainlingVaultAsync(string walletPubkey)
        {
            var vault = await _chainlingVaultService.LoadVaultAsync(walletPubkey);
            if (vault == null)
            {
                Debug.Log("[PdaBootstrapService] ChainlingVault not found; creating...");
                var createResult = await _chainlingVaultService.CreateVaultAsync(walletPubkey);
                if (createResult.success)
                    vault = await _chainlingVaultService.LoadVaultAsync(walletPubkey);
                else
                    Debug.LogError($"[PdaBootstrapService] ChainlingVault creation failed: {createResult.errorMessage}");
            }
            return vault;
        }

        private async Task<EquippedMoveRegistryData> EnsureEquippedRegistryAsync(string walletPubkey, uint instanceId)
        {
            var registry = await _registryService.LoadRegistryAsync(walletPubkey, instanceId);
            if (registry == null)
            {
                Debug.Log($"[PdaBootstrapService] EquippedMoveRegistry not found for instanceId={instanceId}; creating...");
                var createResult = await _registryService.CreateRegistryAsync(walletPubkey, instanceId);
                if (createResult.success)
                    registry = await _registryService.LoadRegistryAsync(walletPubkey, instanceId);
                else
                    Debug.LogError($"[PdaBootstrapService] EquippedMoveRegistry creation failed for {instanceId}: {createResult.errorMessage}");
            }
            return registry;
        }
    }
}
