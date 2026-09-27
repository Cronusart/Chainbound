using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Harness / offline item use only. Production Inventory uses
    /// <see cref="SupabasePlayerItemUseService"/>. Profile stacks decrement locally;
    /// Core-backed items route through <see cref="ICoreAssetConsumeService"/> when present.
    /// Field heals patch roster snapshots in-place for menu refresh.
    /// </summary>
    public class StubPlayerItemUseService : MonoBehaviour, IPlayerItemUseService
    {
        [SerializeField] private MonoBehaviour inventoryServiceBehaviour;
        [SerializeField] private MonoBehaviour favorServiceBehaviour;
        [SerializeField] private MonoBehaviour coreConsumeServiceBehaviour;
        [SerializeField] private MonoBehaviour rosterProviderBehaviour;
        [SerializeField] private SpeciesDatabaseSO speciesDatabase;
        [SerializeField] private FieldItemConfigSO fieldItemConfig;
        [SerializeField] private bool succeedInDev = true;

        private InventorySnapshot working;

        public void Configure(
            IInventoryService inventory,
            IFavorService favor = null,
            ICoreAssetConsumeService consume = null,
            IPlayerRosterProvider roster = null,
            SpeciesDatabaseSO speciesDb = null,
            FieldItemConfigSO fieldConfig = null)
        {
            inventoryServiceBehaviour = inventory as MonoBehaviour;
            favorServiceBehaviour = favor as MonoBehaviour;
            coreConsumeServiceBehaviour = consume as MonoBehaviour;
            rosterProviderBehaviour = roster as MonoBehaviour;
            if (speciesDb != null)
                speciesDatabase = speciesDb;
            if (fieldConfig != null)
                fieldItemConfig = fieldConfig;
        }

        public async Task<PlayerItemUseResult> UseItemAsync(
            string playerProfileId,
            string walletAddress,
            PlayerItemUseRequest request)
        {
            if (!succeedInDev)
                return PlayerItemUseResult.Failed("item_use_stub_rejected");
            if (request == null || string.IsNullOrWhiteSpace(request.itemId))
                return PlayerItemUseResult.Failed("item_id_required");

            if (!PlayerItemCatalogSeed.TryGet(request.itemId, out PlayerItemDefinition def) || def == null)
                return PlayerItemUseResult.Failed($"unknown_item:{request.itemId}");

            IInventoryService inventoryService = inventoryServiceBehaviour as IInventoryService;
            if (working == null && inventoryService != null)
                working = await inventoryService.FetchInventoryAsync(playerProfileId, walletAddress);
            if (working == null)
                working = new InventorySnapshot { stacks = new List<MenuInventoryItemStack>() };

            MenuInventoryItemStack stack = FindStack(working, request.itemId, request.coreAssetId);
            if (stack == null || stack.quantity <= 0)
                return PlayerItemUseResult.Failed("item_not_in_inventory");

            if (def.ownershipKind == PlayerItemOwnershipKind.CoreCollectible)
            {
                ICoreAssetConsumeService consume = coreConsumeServiceBehaviour as ICoreAssetConsumeService;
                if (consume == null)
                    return PlayerItemUseResult.Failed("core_consume_service_missing");

                string assetId = !string.IsNullOrWhiteSpace(request.coreAssetId)
                    ? request.coreAssetId
                    : stack.coreAssetId;
                CoreItemConsumePrepareResult prepared = await consume.PrepareConsumeAsync(
                    playerProfileId,
                    walletAddress,
                    def.itemId,
                    assetId,
                    def.effectFamily.ToString());
                if (!prepared.success)
                    return PlayerItemUseResult.Failed(prepared.error);

                CoreItemConsumeFinalizeResult finalized = await consume.FinalizeConsumeAsync(
                    playerProfileId,
                    walletAddress,
                    prepared.consumeIntentId,
                    string.IsNullOrWhiteSpace(prepared.unsignedTxBase64) ? "stub-signature" : "");
                if (!finalized.success)
                    return PlayerItemUseResult.Failed(finalized.error);

                DecrementStack(working, stack);
                return PlayerItemUseResult.Ok(working, consumeReceiptId: finalized.consumeIntentId);
            }

            if (working.ramBalance < def.useRamCost)
                return PlayerItemUseResult.Failed("insufficient_ram");

            List<FieldItemHpPatch> hpPatches = null;
            int favorScore = 0;

            switch (def.effectFamily)
            {
                case PlayerItemEffectFamily.HealSingle:
                case PlayerItemEffectFamily.StatusClear:
                {
                    if (!TryResolveTarget(request, out ChainlingSnapshot target, out SpeciesDefinitionSO species, out string targetError))
                        return PlayerItemUseResult.Failed(targetError);
                    if (!FieldItemHealUtility.TryApplySingleHeal(target, species, def.healAmount, out FieldItemHpPatch patch, out string healError))
                        return PlayerItemUseResult.Failed(healError);
                    hpPatches = new List<FieldItemHpPatch> { patch };
                    // StatusClear: heal now; status wipe TODO when menu status data exists.
                    break;
                }
                case PlayerItemEffectFamily.Revive:
                {
                    if (!TryResolveTarget(request, out ChainlingSnapshot target, out SpeciesDefinitionSO species, out string targetError))
                        return PlayerItemUseResult.Failed(targetError);
                    FieldItemConfigSO config = fieldItemConfig != null
                        ? fieldItemConfig
                        : FieldItemConfigSO.CreateRuntimeDefault();
                    if (!FieldItemHealUtility.TryApplyRevive(target, species, config, out FieldItemHpPatch patch, out string reviveError))
                        return PlayerItemUseResult.Failed(reviveError);
                    hpPatches = new List<FieldItemHpPatch> { patch };
                    break;
                }
                case PlayerItemEffectFamily.HealParty:
                {
                    if (!TryResolveParty(out List<ChainlingSnapshot> party, out string partyError))
                        return PlayerItemUseResult.Failed(partyError);
                    if (!FieldItemHealUtility.TryApplyPartyHeal(party, ResolveSpecies, def.healAmount, out hpPatches, out string partyHealError))
                        return PlayerItemUseResult.Failed(partyHealError);
                    break;
                }
                case PlayerItemEffectFamily.FavorBoost:
                {
                    IFavorService favorService = ResolveFavorService();
                    favorService.SetAmpouleFavor(def.favorPoints, def.favorDurationBattles);
                    favorScore = favorService.CurrentFavorScore;
                    break;
                }
            }

            working.ramBalance -= def.useRamCost;
            DecrementStack(working, stack);

            if (hpPatches != null)
                PatchCachedRosters(hpPatches);

            Debug.Log(
                $"[StubPlayerItemUseService] Used {def.itemId} family={def.effectFamily} remaining={stack.quantity}",
                this);
            return PlayerItemUseResult.Ok(working, favorScore, hpPatches: hpPatches);
        }

        private IFavorService ResolveFavorService()
        {
            IFavorService favor = favorServiceBehaviour as IFavorService
                ?? GetComponent<FavorService>()
                ?? FavorRuntime.Active;
            if (favor != null)
                return favor;

            FavorService created = gameObject.AddComponent<FavorService>();
            favorServiceBehaviour = created;
            return created;
        }

        private bool TryResolveTarget(
            PlayerItemUseRequest request,
            out ChainlingSnapshot target,
            out SpeciesDefinitionSO species,
            out string error)
        {
            target = null;
            species = null;
            error = "";

            string targetId = request != null ? request.targetChainlingId : "";
            if (string.IsNullOrWhiteSpace(targetId))
            {
                error = "target_required";
                return false;
            }

            PlayerRosterSnapshot roster = ResolveRosterSnapshot();
            if (roster?.chainlings == null)
            {
                error = "roster_unavailable";
                return false;
            }

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry?.snapshot == null)
                    continue;
                if (!string.Equals(entry.snapshot.backendChainlingId, targetId, StringComparison.Ordinal))
                    continue;

                target = entry.snapshot;
                species = ResolveSpecies(target);
                if (species == null)
                {
                    error = "species_missing";
                    return false;
                }

                return true;
            }

            error = "target_not_in_roster";
            return false;
        }

        private bool TryResolveParty(out List<ChainlingSnapshot> party, out string error)
        {
            party = new List<ChainlingSnapshot>();
            error = "";
            PlayerRosterSnapshot roster = ResolveRosterSnapshot();
            if (roster?.chainlings == null)
            {
                error = "roster_unavailable";
                return false;
            }

            for (int i = 0; i < roster.chainlings.Count; i++)
            {
                PlayerRosterEntry entry = roster.chainlings[i];
                if (entry?.snapshot == null || !entry.isPartyMember)
                    continue;
                if (string.IsNullOrWhiteSpace(entry.snapshot.backendChainlingId))
                    continue;
                party.Add(entry.snapshot);
            }

            if (party.Count == 0)
            {
                // Harness / offline: treat whole roster as party if no flags set.
                for (int i = 0; i < roster.chainlings.Count; i++)
                {
                    PlayerRosterEntry entry = roster.chainlings[i];
                    if (entry?.snapshot == null || string.IsNullOrWhiteSpace(entry.snapshot.backendChainlingId))
                        continue;
                    party.Add(entry.snapshot);
                    if (party.Count >= CombatConstants.ROSTER_SIZE)
                        break;
                }
            }

            if (party.Count == 0)
            {
                error = "party_empty";
                return false;
            }

            return true;
        }

        private PlayerRosterSnapshot ResolveRosterSnapshot()
        {
            if (rosterProviderBehaviour is ChainlingsMenuTestHarness assignedHarness)
            {
                if (speciesDatabase == null)
                    speciesDatabase = assignedHarness.SpeciesDatabase;
                return assignedHarness.CachedRoster;
            }

            if (rosterProviderBehaviour is SupabasePlayerRosterProvider assignedSupabase)
                return assignedSupabase.CachedRoster;

            ChainlingsMenuTestHarness harness = GetComponentInParent<ChainlingsMenuTestHarness>(true)
                ?? FindObjectOfType<ChainlingsMenuTestHarness>(true);
            if (harness != null)
            {
                rosterProviderBehaviour = harness;
                if (speciesDatabase == null)
                    speciesDatabase = harness.SpeciesDatabase;
                return harness.CachedRoster;
            }

            SupabasePlayerRosterProvider supabase = FindObjectOfType<SupabasePlayerRosterProvider>(true);
            if (supabase != null)
            {
                rosterProviderBehaviour = supabase;
                return supabase.CachedRoster;
            }

            return null;
        }

        private SpeciesDefinitionSO ResolveSpecies(ChainlingSnapshot snapshot)
        {
            if (snapshot == null || speciesDatabase == null)
                return null;
            return speciesDatabase.GetById(snapshot.speciesId);
        }

        private void PatchCachedRosters(List<FieldItemHpPatch> patches)
        {
            if (patches == null)
                return;

            PlayerRosterSnapshot roster = ResolveRosterSnapshot();
            for (int i = 0; i < patches.Count; i++)
            {
                FieldItemHpPatch patch = patches[i];
                if (patch == null || string.IsNullOrWhiteSpace(patch.chainlingId))
                    continue;
                ChainlingRosterHpMutation.PatchEntryHp(roster, patch.chainlingId, patch.currentHp);
            }
        }

        private static MenuInventoryItemStack FindStack(
            InventorySnapshot snapshot,
            string itemId,
            string coreAssetId)
        {
            if (snapshot?.stacks == null)
                return null;

            for (int i = 0; i < snapshot.stacks.Count; i++)
            {
                MenuInventoryItemStack stack = snapshot.stacks[i];
                if (stack == null || stack.itemId != itemId)
                    continue;
                if (!string.IsNullOrWhiteSpace(coreAssetId) && stack.coreAssetId != coreAssetId)
                    continue;
                return stack;
            }

            return null;
        }

        private static void DecrementStack(InventorySnapshot snapshot, MenuInventoryItemStack stack)
        {
            stack.quantity = Mathf.Max(0, stack.quantity - 1);
            if (stack.quantity > 0 || snapshot?.stacks == null)
                return;

            snapshot.stacks.Remove(stack);
            if (snapshot.consumableIds != null)
                snapshot.consumableIds.Remove(stack.itemId);
        }
    }
}
