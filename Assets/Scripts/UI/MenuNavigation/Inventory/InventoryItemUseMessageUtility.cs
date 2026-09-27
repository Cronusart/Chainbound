using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Player-facing copy for Inventory Use results (never expose snake_case RPC codes).
    /// </summary>
    public static class InventoryItemUseMessageUtility
    {
        public static string ChooseHint() => "Choose a Chainling.";

        public static string FormatChooseTarget(string itemDisplayName)
        {
            string item = string.IsNullOrWhiteSpace(itemDisplayName) ? "this item" : itemDisplayName.Trim();
            return $"Choose a Chainling to use {item} on.";
        }

        public static string FormatConfirmPartyAoe(string itemDisplayName)
        {
            string item = string.IsNullOrWhiteSpace(itemDisplayName) ? "this item" : itemDisplayName.Trim();
            return $"Confirm {item} on the whole party.";
        }

        public static string FormatConfirmFavor(string itemDisplayName)
        {
            string item = string.IsNullOrWhiteSpace(itemDisplayName) ? "this item" : itemDisplayName.Trim();
            return $"Confirm {item}?";
        }

        public static string FormatInspectHint() => "Inspect item details. Cancel to return.";

        public static string FormatRecovered(string displayName, int healedAmount)
        {
            string name = string.IsNullOrWhiteSpace(displayName) ? "Your Chainling" : displayName.Trim();
            return $"{name} recovered {Mathf.Max(0, healedAmount)} HP!";
        }

        public static string FormatPartyRecovered(int healedTotal, int membersHealed)
        {
            if (membersHealed <= 0)
                return "The party didn't need that patch.";
            return $"Party recovered {Mathf.Max(0, healedTotal)} HP across {membersHealed} Chainling{(membersHealed == 1 ? "" : "s")}!";
        }

        public static string FormatFavorApplied(int favorScore, int battlesRemaining)
        {
            return $"Favor set to {Mathf.Max(0, favorScore)} for {Mathf.Max(0, battlesRemaining)} battles.";
        }

        public static string FormatInvalid(
            string errorCode,
            string displayName,
            PlayerItemEffectFamily family)
        {
            string name = string.IsNullOrWhiteSpace(displayName) ? "Your Chainling" : displayName.Trim();
            string code = (errorCode ?? "").Trim();

            switch (code)
            {
                case "already_full_hp":
                    return $"{name} is already at full health!";
                case "party_already_full_hp":
                    return "The party is already at full health!";
                case "target_fainted_use_revive":
                    return $"{name} needs a Chainling Reboot!";
                case "target_not_fainted":
                    return $"{name} doesn't need a Chainling Reboot.";
                case "target_required":
                    return "Choose a Chainling first.";
                case "party_empty":
                    return "No party Chainlings available.";
                case "item_not_in_inventory":
                    return "You don't have that item.";
                default:
                    if (family == PlayerItemEffectFamily.Revive)
                        return $"{name} doesn't need a Chainling Reboot.";
                    if (family == PlayerItemEffectFamily.HealSingle || family == PlayerItemEffectFamily.StatusClear)
                        return $"{name} can't use that right now.";
                    if (family == PlayerItemEffectFamily.HealParty)
                        return "The party can't use that right now.";
                    if (family == PlayerItemEffectFamily.FavorBoost)
                        return "That Favor Boost can't be used right now.";
                    return "That item can't be used right now.";
            }
        }
    }
}
