using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Inventory bag categories — keys match PLAYER_ITEM_CATALOG_AND_INVENTORY_v1.md.
    /// </summary>
    public enum PlayerItemCategory
    {
        Field = 0,
        Capture = 1,
        Favor = 2,
        Bindery = 3,
        Archive = 4,
        Cosmetic = 5,
        Quest = 6,
        Utility = 7,
    }

    /// <summary>
    /// Resolver families for menu / Bindery item use.
    /// </summary>
    public enum PlayerItemEffectFamily
    {
        None = 0,
        HealSingle = 1,
        HealParty = 2,
        Revive = 3,
        StatusClear = 4,
        CaptureTool = 5,
        FavorBoost = 6,
        StampErase = 8,
        ModifierReroll = 9,
        CaseBinding = 10,
        BindingMutationRoll = 11,
        UnbindArchiveItem = 12,
        ExtractBookMove = 13,
        UnbindArchiveBook = 14,
        CosmeticOwned = 15,
        UtilityGear = 16,
        CaptureBoost = 17,
        MovementSpeedBoost = 18,
    }

    /// <summary>
    /// How the stack is backed for ownership / consume authority.
    /// </summary>
    public enum PlayerItemOwnershipKind
    {
        /// <summary>Profile-backed stack owned by the game authority.</summary>
        ProfileStack = 0,
        /// <summary>Metaplex Core NFT under a trusted collection; consume requires burn + server finalize.</summary>
        CoreCollectible = 1,
        /// <summary>Cosmetic Core asset shown in Style tab; Inventory Style category is ownership viewer.</summary>
        CoreCosmetic = 2,
    }

    [Serializable]
    public class PlayerItemDefinition
    {
        public string itemId;
        public string displayName;
        [TextArea(2, 4)] public string description;
        public PlayerItemCategory category = PlayerItemCategory.Field;
        public PlayerItemEffectFamily effectFamily = PlayerItemEffectFamily.None;
        public PlayerItemOwnershipKind ownershipKind = PlayerItemOwnershipKind.ProfileStack;
        public string rarity = "common";
        public int maxStack = 99;
        public bool usableInMenu = true;
        public bool usableInBattle;
        public bool binderyOnly;
        public int favorPoints;
        public int favorDurationBattles;
        public float captureMultiplier = 1f;
        [Tooltip("Capture utility slot for UtilityGear: head or body.")]
        public string utilitySlot = "";
        [Tooltip("Scanner grade (0-5). A non-zero scanner also reveals the low-HP marker.")]
        [Range(0, 5)] public int captureScanRating;
        public int captureStabilityBasisPoints;
        public int captureBandWidthBasisPoints;
        public int captureLockRateBasisPoints;
        public int captureTimeBasisPoints;
        [Tooltip("Real-time booster duration. Zero for gear and Orbs.")]
        public int captureBoostDurationSeconds;
        public bool entanglesOnCapture;
        public int healAmount;
        public long useRamCost;
        /// <summary>Metaplex Core cb.kind value when ownershipKind is Core*.</summary>
        public string coreKindAttribute = "";
    }

    [Serializable]
    public class MenuInventoryItemStack
    {
        public string itemId;
        public int quantity = 1;
        /// <summary>Optional Core asset pubkey when this stack is 1:1 with a collectible.</summary>
        public string coreAssetId = "";
        public PlayerItemOwnershipKind ownershipKind = PlayerItemOwnershipKind.ProfileStack;
        public string categoryKey = "field";
        public string displayName = "";
        public string effectFamilyKey = "";
    }

    public static class PlayerItemCategoryKeys
    {
        public const string Field = "field";
        public const string Capture = "capture";
        public const string Favor = "favor";
        public const string Bindery = "bindery";
        public const string Archive = "archive";
        public const string Cosmetic = "cosmetic";
        public const string Quest = "quest";
        public const string Utility = "utility";

        public static string ToKey(PlayerItemCategory category)
        {
            switch (category)
            {
                case PlayerItemCategory.Capture: return Capture;
                case PlayerItemCategory.Favor: return Favor;
                case PlayerItemCategory.Bindery: return Bindery;
                case PlayerItemCategory.Archive: return Archive;
                case PlayerItemCategory.Cosmetic: return Cosmetic;
                case PlayerItemCategory.Quest: return Quest;
                case PlayerItemCategory.Utility: return Utility;
                default: return Field;
            }
        }

        public static bool TryParse(string key, out PlayerItemCategory category)
        {
            switch ((key ?? "").Trim().ToLowerInvariant())
            {
                case Capture: category = PlayerItemCategory.Capture; return true;
                case Favor:
                case "clarity": // legacy bag key
                    category = PlayerItemCategory.Favor;
                    return true;
                case Bindery: category = PlayerItemCategory.Bindery; return true;
                case Archive: category = PlayerItemCategory.Archive; return true;
                case Cosmetic: category = PlayerItemCategory.Cosmetic; return true;
                case Quest: category = PlayerItemCategory.Quest; return true;
                case Utility: category = PlayerItemCategory.Utility; return true;
                case Field: category = PlayerItemCategory.Field; return true;
                default:
                    category = PlayerItemCategory.Field;
                    return false;
            }
        }

        public static string ToKey(PlayerItemEffectFamily family)
        {
            switch (family)
            {
                case PlayerItemEffectFamily.HealSingle: return "heal_single";
                case PlayerItemEffectFamily.HealParty: return "heal_party";
                case PlayerItemEffectFamily.Revive: return "revive";
                case PlayerItemEffectFamily.StatusClear: return "status_clear";
                case PlayerItemEffectFamily.CaptureTool: return "capture_tool";
                case PlayerItemEffectFamily.FavorBoost: return "favor_boost";
                case PlayerItemEffectFamily.StampErase: return "stamp_erase";
                case PlayerItemEffectFamily.ModifierReroll: return "modifier_reroll";
                case PlayerItemEffectFamily.CaseBinding: return "case_binding";
                case PlayerItemEffectFamily.BindingMutationRoll: return "binding_mutation_roll";
                case PlayerItemEffectFamily.UnbindArchiveItem: return "unbind_archive_item";
                case PlayerItemEffectFamily.ExtractBookMove: return "extract_book_move";
                case PlayerItemEffectFamily.UnbindArchiveBook: return "unbind_archive_book";
                case PlayerItemEffectFamily.CosmeticOwned: return "cosmetic_owned";
                case PlayerItemEffectFamily.UtilityGear: return "utility_gear";
                case PlayerItemEffectFamily.CaptureBoost: return "capture_boost";
                case PlayerItemEffectFamily.MovementSpeedBoost: return "movement_speed_boost";
                default: return "none";
            }
        }
    }
}
