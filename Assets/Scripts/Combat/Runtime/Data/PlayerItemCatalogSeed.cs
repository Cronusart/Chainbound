using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Built-in seed matching Docs/Specs/Economy/PLAYER_ITEM_CATALOG_AND_INVENTORY_v1.md.
    /// Optional PlayerItemCatalogSO can override / extend later.
    /// </summary>
    public static class PlayerItemCatalogSeed
    {
        private static IReadOnlyList<PlayerItemDefinition> cached;

        public static IReadOnlyList<PlayerItemDefinition> All
        {
            get
            {
                if (cached != null)
                    return cached;
                cached = Build();
                return cached;
            }
        }

        public static bool TryGet(string itemId, out PlayerItemDefinition definition)
        {
            definition = null;
            string id = (itemId ?? "").Trim();
            if (id.Length == 0)
                return false;

            IReadOnlyList<PlayerItemDefinition> all = All;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerItemDefinition row = all[i];
                if (row != null && string.Equals(row.itemId, id, StringComparison.Ordinal))
                {
                    definition = row;
                    return true;
                }
            }

            return false;
        }

        public static MenuInventoryItemStack CreateStack(string itemId, int quantity, string coreAssetId = "")
        {
            if (!TryGet(itemId, out PlayerItemDefinition def) || def == null)
            {
                return new MenuInventoryItemStack
                {
                    itemId = itemId ?? "",
                    quantity = Math.Max(0, quantity),
                    coreAssetId = coreAssetId ?? "",
                };
            }

            return new MenuInventoryItemStack
            {
                itemId = def.itemId,
                quantity = Math.Max(0, quantity),
                coreAssetId = coreAssetId ?? "",
                ownershipKind = def.ownershipKind,
                categoryKey = PlayerItemCategoryKeys.ToKey(def.category),
                displayName = def.displayName,
                effectFamilyKey = PlayerItemCategoryKeys.ToKey(def.effectFamily),
            };
        }

        private static IReadOnlyList<PlayerItemDefinition> Build()
        {
            return new List<PlayerItemDefinition>
            {
                Def("chainling_patch", "Chainling Patch", "Applies a small HP patch to one Chainling.", PlayerItemCategory.Field, PlayerItemEffectFamily.HealSingle, heal: 30),
                Def("chainling_update", "Chainling Update", "Applies a moderate HP update to one Chainling.", PlayerItemCategory.Field, PlayerItemEffectFamily.HealSingle, heal: 70),
                Def("chainling_reformat", "Chainling Reformat", "Fully restores one Chainling's HP.", PlayerItemCategory.Field, PlayerItemEffectFamily.HealSingle, heal: 9999),
                Def("system_flush", "System Flush", "Heals and clears common statuses.", PlayerItemCategory.Field, PlayerItemEffectFamily.StatusClear, heal: 40),
                Def("chainling_reboot", "Chainling Reboot", "Reboots a fainted Chainling.", PlayerItemCategory.Field, PlayerItemEffectFamily.Revive),
                Def("party_patch", "Party Patch", "Applies a small HP patch to the whole party.", PlayerItemCategory.Field, PlayerItemEffectFamily.HealParty, heal: 15),

                Def("signal_orb", "Signal Orb", "Neutral capture band and lock profile.", PlayerItemCategory.Capture, PlayerItemEffectFamily.CaptureTool),
                Def("lattice_orb", "Lattice Orb", "Widens the capture band by 25%, but reduces Lock Rate by 15%.", PlayerItemCategory.Capture, PlayerItemEffectFamily.CaptureTool, bandWidthBp: 2500, lockRateBp: -1500),
                Def("entangle_orb", "Entangle Orb", "Narrows the band by 20% and capture time by 25%. A successful Lock assigns an entangled type.", PlayerItemCategory.Capture, PlayerItemEffectFamily.CaptureTool, bandWidthBp: -2000, captureTimeBp: -2500, entanglesOnCapture: true),
                Def("quick_orb", "Quick Orb", "Raises Lock Rate by 30%, but narrows the capture band by 20%.", PlayerItemCategory.Capture, PlayerItemEffectFamily.CaptureTool, bandWidthBp: -2000, lockRateBp: 3000),

                Def("signal_scanner_i", "Signal Scanner I", "Reveals the low-HP marker and one non-HP IV grade.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "head", scan: 1, maxStack: 1),
                Def("signal_scanner_iii", "Signal Scanner III", "Reveals the low-HP marker and three non-HP IV grades.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "head", scan: 3, maxStack: 1),
                Def("signal_scanner_v", "Signal Scanner V", "Reveals the low-HP marker and all five non-HP IV grades.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "head", scan: 5, maxStack: 1),
                Def("stability_visor", "Stability Visor", "Adds 15% Stability to reduce trace motion.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "head", stabilityBp: 1500, maxStack: 1),
                Def("wideband_rig", "Wideband Rig", "Widens the capture band by 15%.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "body", bandWidthBp: 1500, maxStack: 1),
                Def("lockdrive_rig", "Lockdrive Rig", "Raises Lock Rate by 15%.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "body", lockRateBp: 1500, maxStack: 1),
                Def("reserve_clock_rig", "Reserve Clock Rig", "Adds 15% capture time.", PlayerItemCategory.Utility, PlayerItemEffectFamily.UtilityGear, utilitySlot: "body", captureTimeBp: 1500, maxStack: 1),

                Def("band_expander_s", "Band Expander S", "Widens the capture band by 10% for 10 real-time minutes.", PlayerItemCategory.Utility, PlayerItemEffectFamily.CaptureBoost, bandWidthBp: 1000, boostSeconds: 600),
                Def("band_expander_m", "Band Expander M", "Widens the capture band by 20% for 20 real-time minutes.", PlayerItemCategory.Utility, PlayerItemEffectFamily.CaptureBoost, bandWidthBp: 2000, boostSeconds: 1200),
                Def("band_expander_l", "Band Expander L", "Widens the capture band by 30% for 30 real-time minutes.", PlayerItemCategory.Utility, PlayerItemEffectFamily.CaptureBoost, bandWidthBp: 3000, boostSeconds: 1800),
                Def("lock_accelerant_s", "Lock Accelerant S", "Raises Lock Rate by 10% for 10 real-time minutes.", PlayerItemCategory.Utility, PlayerItemEffectFamily.CaptureBoost, lockRateBp: 1000, boostSeconds: 600),
                Def("lock_accelerant_m", "Lock Accelerant M", "Raises Lock Rate by 20% for 20 real-time minutes.", PlayerItemCategory.Utility, PlayerItemEffectFamily.CaptureBoost, lockRateBp: 2000, boostSeconds: 1200),
                Def("lock_accelerant_l", "Lock Accelerant L", "Raises Lock Rate by 30% for 30 real-time minutes.", PlayerItemCategory.Utility, PlayerItemEffectFamily.CaptureBoost, lockRateBp: 3000, boostSeconds: 1800),

                Def("favor_boost_s", "Favor Boost", "Improves encounter, capture, find, and wild IV odds for a short stretch. Opens a little bandwidth on the road home.", PlayerItemCategory.Favor, PlayerItemEffectFamily.FavorBoost, favor: 100, favorBattles: 3),
                Def("favor_boost_m", "Strong Favor", "Improves encounter, capture, find, and wild IV odds for a medium stretch. More bandwidth, less latency on lucky finds.", PlayerItemCategory.Favor, PlayerItemEffectFamily.FavorBoost, favor: 100, favorBattles: 6),
                Def("favor_boost_l", "Peak Favor", "Improves encounter, capture, find, and wild IV odds for a long stretch. Peak bandwidth for the journey home.", PlayerItemCategory.Favor, PlayerItemEffectFamily.FavorBoost, favor: 110, favorBattles: 10),

                Def("stamp_solvent", "Stamp Solvent", "Erases a pressed stamp (Stamped → Known).", PlayerItemCategory.Bindery, PlayerItemEffectFamily.StampErase, binderyOnly: true, ram: 25),
                Def("seal_breaker", "Seal Breaker", "Rerolls a Stamped modifier in place.", PlayerItemCategory.Bindery, PlayerItemEffectFamily.ModifierReroll, binderyOnly: true, ram: 40),

                Def("case_binding_standard", "Case Binding", "Cover consumed at Archive Binding.", PlayerItemCategory.Archive, PlayerItemEffectFamily.CaseBinding, ownership: PlayerItemOwnershipKind.CoreCollectible, coreKind: "case_binding", binderyOnly: true),
                Def("binding_mutation_roll", "Binding Mutation Roll", "Display-only binding mutation.", PlayerItemCategory.Archive, PlayerItemEffectFamily.BindingMutationRoll, ownership: PlayerItemOwnershipKind.CoreCollectible, coreKind: "binding_mutation"),
                Def("archive_key", "Archive Key", "Detaches an item from a Chainling Archive.", PlayerItemCategory.Archive, PlayerItemEffectFamily.UnbindArchiveItem, ownership: PlayerItemOwnershipKind.CoreCollectible, coreKind: "archive_key", ram: 50),
                Def("signature_extract", "Signature Extract", "Removes an individual move from a bound encyclopedia.", PlayerItemCategory.Archive, PlayerItemEffectFamily.ExtractBookMove, ownership: PlayerItemOwnershipKind.CoreCollectible, coreKind: "signature_extract", ram: 75),
                Def("archive_severance", "Archive Severance", "Fully unbinds a bound encyclopedia back to a dossier.", PlayerItemCategory.Archive, PlayerItemEffectFamily.UnbindArchiveBook, ownership: PlayerItemOwnershipKind.CoreCollectible, coreKind: "archive_severance", ram: 200),
            };
        }

        private static PlayerItemDefinition Def(
            string id,
            string name,
            string description,
            PlayerItemCategory category,
            PlayerItemEffectFamily family,
            PlayerItemOwnershipKind ownership = PlayerItemOwnershipKind.ProfileStack,
            string coreKind = "",
            int heal = 0,
            float captureMult = 1f,
            int favor = 0,
            int favorBattles = 0,
            long ram = 0,
            bool binderyOnly = false,
            string utilitySlot = "",
            int scan = 0,
            int stabilityBp = 0,
            int bandWidthBp = 0,
            int lockRateBp = 0,
            int captureTimeBp = 0,
            int boostSeconds = 0,
            bool entanglesOnCapture = false,
            int maxStack = -1)
        {
            return new PlayerItemDefinition
            {
                itemId = id,
                displayName = name,
                description = description,
                category = category,
                effectFamily = family,
                ownershipKind = ownership,
                coreKindAttribute = coreKind ?? "",
                healAmount = heal,
                captureMultiplier = captureMult,
                utilitySlot = utilitySlot ?? "",
                captureScanRating = scan,
                captureStabilityBasisPoints = stabilityBp,
                captureBandWidthBasisPoints = bandWidthBp,
                captureLockRateBasisPoints = lockRateBp,
                captureTimeBasisPoints = captureTimeBp,
                captureBoostDurationSeconds = boostSeconds,
                entanglesOnCapture = entanglesOnCapture,
                favorPoints = favor,
                favorDurationBattles = favorBattles,
                useRamCost = ram,
                binderyOnly = binderyOnly,
                usableInMenu = IsMenuUsableFamily(family),
                usableInBattle = family == PlayerItemEffectFamily.CaptureTool,
                maxStack = maxStack > 0 ? maxStack : (ownership == PlayerItemOwnershipKind.ProfileStack ? 99 : 1),
            };
        }

        private static bool IsMenuUsableFamily(PlayerItemEffectFamily family)
        {
            switch (family)
            {
                case PlayerItemEffectFamily.HealSingle:
                case PlayerItemEffectFamily.HealParty:
                case PlayerItemEffectFamily.Revive:
                case PlayerItemEffectFamily.StatusClear:
                case PlayerItemEffectFamily.FavorBoost:
                case PlayerItemEffectFamily.UtilityGear:
                case PlayerItemEffectFamily.CaptureBoost:
                    return true;
                default:
                    return false;
            }
        }
    }
}
