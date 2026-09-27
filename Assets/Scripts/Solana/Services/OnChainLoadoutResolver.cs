using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Resolves move loadouts from on-chain MoveVault + EquippedMoveRegistry data.
    /// Current phase resolves Known moves fully and uses cached move hints for NFT-backed slots.
    /// </summary>
    public class OnChainLoadoutResolver : MonoBehaviour
    {
        [SerializeField] private MoveDatabaseSO moveDatabase;
        [SerializeField] private bool replaceExistingLoadout = true;
        [SerializeField] private bool usePlayerStateCache = true;
        [SerializeField] private bool useCachedNftMoveHints = true;
        [SerializeField] private bool logDiagnostics = true;

        public bool IsReady { get; private set; }
        public string LastError { get; private set; }

        public void Configure(MoveDatabaseSO database)
        {
            moveDatabase = database;
        }

        public async Task ResolveLoadoutsAsync(
            PartySnapshot party,
            string walletPubkey,
            IMoveVaultService moveVaultService,
            IEquippedMoveRegistryService registryService,
            PlayerStateCache playerState = null)
        {
            IsReady = false;
            LastError = null;

            if (party == null || party.chainlings == null || party.chainlings.Count == 0)
            {
                IsReady = true;
                return;
            }

            if (moveDatabase == null)
            {
                LastError = "MoveDatabaseSO is required to resolve on-chain move hashes.";
                if (logDiagnostics)
                    Debug.LogWarning($"[OnChainLoadoutResolver] {LastError}");
                IsReady = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(walletPubkey))
            {
                LastError = "Wallet pubkey was empty. Skipping on-chain loadout resolution.";
                if (logDiagnostics)
                    Debug.LogWarning($"[OnChainLoadoutResolver] {LastError}");
                IsReady = true;
                return;
            }

            try
            {
                PlayerMoveVault vault = await ResolveMoveVaultAsync(walletPubkey, moveVaultService, playerState);
                List<EquippedMoveRegistryData> registries = await ResolveRegistriesAsync(walletPubkey, registryService, playerState, party.Count);

                for (int i = 0; i < party.chainlings.Count; i++)
                {
                    ChainlingSnapshot snapshot = party.chainlings[i];
                    if (snapshot == null)
                        continue;

                    if (replaceExistingLoadout)
                    {
                        snapshot.moveIds = new List<string>();
                        snapshot.modifier1SectionIds = new List<string>();
                        snapshot.modifier2SectionIds = new List<string>();
                    }

                    EquippedMoveRegistryData registry = i < registries.Count ? registries[i] : null;
                    if (registry == null)
                    {
                        if (logDiagnostics)
                            Debug.LogWarning($"[OnChainLoadoutResolver] No equipped registry resolved for party slot {i}. Existing/default loadout will be used.");
                        continue;
                    }

                    ApplyRegistryToSnapshot(snapshot, registry, vault);
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogWarning($"[OnChainLoadoutResolver] ResolveLoadoutsAsync failed: {ex.Message}");
            }

            IsReady = true;
        }

        private async Task<PlayerMoveVault> ResolveMoveVaultAsync(string walletPubkey, IMoveVaultService moveVaultService, PlayerStateCache playerState)
        {
            if (usePlayerStateCache && playerState?.MoveVault != null)
                return playerState.MoveVault;

            if (moveVaultService == null)
                return null;

            return moveVaultService.CachedVault ?? await moveVaultService.LoadVaultAsync(walletPubkey);
        }

        private async Task<List<EquippedMoveRegistryData>> ResolveRegistriesAsync(
            string walletPubkey,
            IEquippedMoveRegistryService registryService,
            PlayerStateCache playerState,
            int desiredCount)
        {
            var results = new List<EquippedMoveRegistryData>(desiredCount);

            if (usePlayerStateCache && playerState?.ChainlingVault != null && playerState.ChainlingVault.chainlings != null)
            {
                int count = Math.Min(desiredCount, playerState.ChainlingVault.chainlings.Count);
                for (int i = 0; i < count; i++)
                {
                    uint instanceId = playerState.ChainlingVault.chainlings[i].instanceId;
                    EquippedMoveRegistryData registry = null;
                    if (playerState.EquippedRegistries != null)
                        playerState.EquippedRegistries.TryGetValue(instanceId, out registry);

                    if (registry == null && registryService != null)
                        registry = await registryService.LoadRegistryAsync(walletPubkey, instanceId);

                    results.Add(registry);
                }

                return results;
            }

            if (usePlayerStateCache && playerState?.EquippedRegistries != null && playerState.EquippedRegistries.Count > 0)
            {
                var ordered = new List<uint>(playerState.EquippedRegistries.Keys);
                ordered.Sort();
                for (int i = 0; i < ordered.Count && results.Count < desiredCount; i++)
                    results.Add(playerState.EquippedRegistries[ordered[i]]);
            }

            return results;
        }

        private void ApplyRegistryToSnapshot(ChainlingSnapshot snapshot, EquippedMoveRegistryData registry, PlayerMoveVault vault)
        {
            if (registry?.slots == null || snapshot == null)
                return;

            int maxSlots = MoveLoadoutSlotRules.ResolveMaxMoveSlots(snapshot.rebirthCount);
            int slotLimit = Math.Min(registry.slots.Length, maxSlots);
            for (int i = 0; i < slotLimit; i++)
            {
                EquippedMoveSlot slot = registry.slots[i];
                if (slot.IsEmpty)
                    continue;

                ushort hashedMoveId = ResolveHashedMoveId(slot, vault);
                if (hashedMoveId == 0)
                    continue;

                MoveDefinitionSO def = moveDatabase.GetByHashedId(hashedMoveId);
                if (def == null)
                {
                    if (logDiagnostics)
                        Debug.LogWarning($"[OnChainLoadoutResolver] Could not map on-chain move hash {hashedMoveId} to a MoveDefinitionSO.");
                    continue;
                }

                snapshot.moveIds.Add(def.moveId);
                snapshot.modifier1SectionIds.Add(string.Empty);
                snapshot.modifier2SectionIds.Add(string.Empty);
            }
        }

        private ushort ResolveHashedMoveId(EquippedMoveSlot slot, PlayerMoveVault vault)
        {
            MoveTier tier = (MoveTier)slot.tier;

            if (tier == MoveTier.Known)
            {
                if (vault != null && vault.knownMoves != null && slot.knownIndex < vault.knownMoves.Count)
                    return vault.knownMoves[slot.knownIndex].moveId;

                return slot.moveId;
            }

            if (useCachedNftMoveHints)
                return slot.moveId;

            return 0;
        }
    }
}
