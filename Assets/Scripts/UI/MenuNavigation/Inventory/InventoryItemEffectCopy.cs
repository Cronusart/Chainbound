using System.Text;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Short effect / inspect copy derived from catalog fields (not display-name branches).
    /// </summary>
    public static class InventoryItemEffectCopy
    {
        public static string FormatEffectLabel(PlayerItemDefinition definition)
        {
            if (definition == null)
                return "";

            switch (definition.effectFamily)
            {
                case PlayerItemEffectFamily.HealSingle:
                    if (definition.healAmount >= 9999)
                        return "Fully restores one Chainling's HP.";
                    return $"Restores {Mathf.Max(0, definition.healAmount)} HP to one Chainling.";
                case PlayerItemEffectFamily.StatusClear:
                    return definition.healAmount > 0
                        ? $"Restores {definition.healAmount} HP and clears common statuses."
                        : "Clears common statuses.";
                case PlayerItemEffectFamily.Revive:
                    return "Reboots a fainted Chainling.";
                case PlayerItemEffectFamily.HealParty:
                    return $"Patches the whole party for +{Mathf.Max(0, definition.healAmount)} HP.";
                case PlayerItemEffectFamily.FavorBoost:
                    return $"Sets Favor Boost to {Mathf.Max(0, definition.favorPoints)} for {Mathf.Max(0, definition.favorDurationBattles)} battles.";
                case PlayerItemEffectFamily.MovementSpeedBoost:
                    return FormatMovementSpeedLabel();
                case PlayerItemEffectFamily.CaptureTool:
                    return FormatCaptureEffects(definition) + " Select it during Record in battle.";
                case PlayerItemEffectFamily.UtilityGear:
                    return $"{Capitalize(definition.utilitySlot)} utility gear. {FormatCaptureEffects(definition)}";
                case PlayerItemEffectFamily.CaptureBoost:
                    return $"{FormatCaptureEffects(definition)} for {Mathf.Max(0, definition.captureBoostDurationSeconds / 60)} minutes.";
                case PlayerItemEffectFamily.StampErase:
                    return FormatRamLine("Erases a pressed stamp (Stamped → Known).", definition.useRamCost);
                case PlayerItemEffectFamily.ModifierReroll:
                    return FormatRamLine("Rerolls a Stamped modifier in place.", definition.useRamCost);
                case PlayerItemEffectFamily.CaseBinding:
                    return "Cover consumed at Archive Binding.";
                case PlayerItemEffectFamily.BindingMutationRoll:
                    return "Display-only binding mutation.";
                case PlayerItemEffectFamily.UnbindArchiveItem:
                    return FormatRamLine("Detaches an item from a Chainling Archive.", definition.useRamCost);
                case PlayerItemEffectFamily.ExtractBookMove:
                    return FormatRamLine("Removes a move from a bound encyclopedia.", definition.useRamCost);
                case PlayerItemEffectFamily.UnbindArchiveBook:
                    return FormatRamLine("Fully unbinds a bound encyclopedia.", definition.useRamCost);
                case PlayerItemEffectFamily.CosmeticOwned:
                    return "Cosmetic ownership — equip from Style / Loadout.";
                default:
                    return string.IsNullOrWhiteSpace(definition.description) ? "" : definition.description.Trim();
            }
        }

        public static string FormatInspectBody(
            PlayerItemDefinition definition,
            MenuInventoryItemStack stack,
            InventoryItemPresentationEntry presentation)
        {
            if (definition == null && stack == null)
                return "No item details.";

            var sb = new StringBuilder(512);
            string name = !string.IsNullOrWhiteSpace(presentation?.displayNameOverride)
                ? presentation.displayNameOverride.Trim()
                : definition != null && !string.IsNullOrWhiteSpace(definition.displayName)
                    ? definition.displayName.Trim()
                    : !string.IsNullOrWhiteSpace(stack?.displayName)
                        ? stack.displayName.Trim()
                        : stack?.itemId ?? "Item";

            sb.AppendLine(name);
            if (definition != null)
            {
                sb.AppendLine($"Category: {PlayerItemCategoryKeys.ToKey(definition.category)}");
                sb.AppendLine($"Effect: {PlayerItemCategoryKeys.ToKey(definition.effectFamily)}");
                sb.AppendLine($"Ownership: {definition.ownershipKind}");
                if (!string.IsNullOrWhiteSpace(definition.rarity))
                    sb.AppendLine($"Rarity: {definition.rarity}");
            }

            if (stack != null)
            {
                sb.AppendLine($"Quantity: {Mathf.Max(0, stack.quantity)}");
                if (!string.IsNullOrWhiteSpace(stack.coreAssetId))
                    sb.AppendLine($"Core id: {stack.coreAssetId.Trim()}");
            }

            if (definition != null && !string.IsNullOrWhiteSpace(definition.description))
            {
                sb.AppendLine();
                sb.AppendLine(definition.description.Trim());
            }

            sb.AppendLine();
            sb.AppendLine(FormatEffectLabel(definition));

            if (definition != null)
            {
                switch (definition.effectFamily)
                {
                    case PlayerItemEffectFamily.CaptureTool:
                        sb.AppendLine("Fixed Orb profile; a full Lock captures with no secondary roll.");
                        sb.AppendLine("Select during Record in a capture-enabled wild battle.");
                        break;
                    case PlayerItemEffectFamily.UtilityGear:
                        sb.AppendLine($"Slot: {Capitalize(definition.utilitySlot)}");
                        sb.AppendLine("Equip from Inventory or the Profile utility slot.");
                        break;
                    case PlayerItemEffectFamily.CaptureBoost:
                        sb.AppendLine($"Real-time duration: {Mathf.Max(0, definition.captureBoostDurationSeconds / 60)} minutes.");
                        sb.AppendLine("Reusing this same tier extends its expiry; its magnitude does not stack.");
                        break;
                    case PlayerItemEffectFamily.StampErase:
                    case PlayerItemEffectFamily.ModifierReroll:
                        if (definition.useRamCost > 0)
                            sb.AppendLine($"RAM cost: {definition.useRamCost}");
                        sb.AppendLine("Open Bindery to use this item.");
                        break;
                    case PlayerItemEffectFamily.CaseBinding:
                    case PlayerItemEffectFamily.BindingMutationRoll:
                    case PlayerItemEffectFamily.UnbindArchiveItem:
                    case PlayerItemEffectFamily.ExtractBookMove:
                    case PlayerItemEffectFamily.UnbindArchiveBook:
                        if (!string.IsNullOrWhiteSpace(definition.coreKindAttribute))
                            sb.AppendLine($"Core kind: {definition.coreKindAttribute}");
                        if (definition.useRamCost > 0)
                            sb.AppendLine($"RAM cost: {definition.useRamCost}");
                        sb.AppendLine("Complete this rite in Archive / Bindery.");
                        break;
                    case PlayerItemEffectFamily.CosmeticOwned:
                        sb.AppendLine("Equip from Style / Loadout. Inventory is a viewer only.");
                        break;
                    case PlayerItemEffectFamily.HealSingle:
                    case PlayerItemEffectFamily.HealParty:
                    case PlayerItemEffectFamily.StatusClear:
                        if (definition.healAmount > 0 && definition.healAmount < 9999)
                            sb.AppendLine($"Heal amount: {definition.healAmount}");
                        else if (definition.healAmount >= 9999)
                            sb.AppendLine("Heal amount: full HP");
                        sb.AppendLine(definition.usableInMenu ? "Usable from Inventory." : "Not usable from Inventory.");
                        break;
                    case PlayerItemEffectFamily.FavorBoost:
                        sb.AppendLine($"Favor points: {definition.favorPoints}");
                        sb.AppendLine($"Duration: {definition.favorDurationBattles} battles (refresh, does not stack).");
                        break;
                    case PlayerItemEffectFamily.MovementSpeedBoost:
                        sb.AppendLine(FormatMovementSpeedApplyBlock());
                        sb.AppendLine("Using it again refreshes the timer. Wearables, party passives, and terrain keep their own modifiers.");
                        break;
                    case PlayerItemEffectFamily.Revive:
                        sb.AppendLine("Usable from Inventory on fainted party members.");
                        break;
                }

                if (definition.usableInBattle)
                    sb.AppendLine("Usable in battle.");
                if (definition.binderyOnly)
                    sb.AppendLine("Bindery-gated.");
                if (definition.maxStack > 1)
                    sb.AppendLine($"Max stack: {definition.maxStack}");
            }

            return sb.ToString().TrimEnd();
        }

        public static string FormatFavorStatusBlock(
            int totalScore,
            float luckStrength01,
            int ampoulePoints,
            int ampouleBattles,
            int partyPoints)
        {
            int luckPct = Mathf.RoundToInt(Mathf.Clamp01(luckStrength01) * 100f);
            var sb = new StringBuilder(256);
            sb.AppendLine($"Current Favor: {Mathf.Max(0, totalScore)}  ·  Luck strength: {luckPct}%");
            sb.AppendLine("Sources:");
            if (ampoulePoints > 0 && ampouleBattles > 0)
                sb.AppendLine($"  Active Boost: {ampoulePoints} pts · {ampouleBattles} battles left");
            else
                sb.AppendLine("  Active Boost: none");
            sb.AppendLine(partyPoints > 0
                ? $"  Party grants: {partyPoints}"
                : "  Party grants: 0");
            sb.AppendLine("  Accessories: none (coming soon)");
            return sb.ToString().TrimEnd();
        }

        public static string FormatMovementSpeedLabel()
        {
            int percent = Mathf.RoundToInt(PlayerMovementSpeedConsumable.BonusFraction * 100f);
            int seconds = Mathf.RoundToInt(PlayerMovementSpeedConsumable.DurationSeconds);
            return $"Raises movement speed by {percent}% for {seconds} seconds.";
        }

        public static string FormatMovementSpeedApplyBlock()
        {
            int percent = Mathf.RoundToInt(PlayerMovementSpeedConsumable.BonusFraction * 100f);
            int seconds = Mathf.RoundToInt(PlayerMovementSpeedConsumable.DurationSeconds);
            return
                $"Apply: +{percent}% movement speed for {seconds} seconds\n" +
                "(refreshes this brew; other speed sources stay)";
        }

        public static string FormatFavorApplyBlock(
            int applyPoints,
            int applyBattles,
            int projectedTotal,
            float projectedLuck01)
        {
            int luckPct = Mathf.RoundToInt(Mathf.Clamp01(projectedLuck01) * 100f);
            return
                $"Apply: {Mathf.Max(0, applyPoints)} Favor for {Mathf.Max(0, applyBattles)} battles\n" +
                $"(replaces active boost; party grants unchanged)\n" +
                $"After: {Mathf.Max(0, projectedTotal)} Favor · Luck strength {luckPct}%";
        }

        private static string FormatRamLine(string body, long ram)
        {
            if (ram <= 0)
                return body;
            return $"{body} RAM {ram}.";
        }

        private static string FormatCaptureEffects(PlayerItemDefinition definition)
        {
            if (definition == null) return "No capture effect.";
            var parts = new System.Collections.Generic.List<string>(5);
            if (definition.captureScanRating > 0) parts.Add($"Scan {definition.captureScanRating}");
            AddPercent(parts, "Stability", definition.captureStabilityBasisPoints);
            AddPercent(parts, "Band Width", definition.captureBandWidthBasisPoints);
            AddPercent(parts, "Lock Rate", definition.captureLockRateBasisPoints);
            AddPercent(parts, "Capture Time", definition.captureTimeBasisPoints);
            return parts.Count > 0 ? string.Join(", ", parts) + "." : "Neutral capture profile.";
        }

        private static void AddPercent(System.Collections.Generic.List<string> parts, string label, int basisPoints)
        {
            if (basisPoints == 0) return;
            parts.Add($"{label} {(basisPoints > 0 ? "+" : "")}{basisPoints / 100f:0.#}%");
        }

        private static string Capitalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "Utility";
            string clean = value.Trim();
            return char.ToUpperInvariant(clean[0]) + clean.Substring(1).ToLowerInvariant();
        }
    }
}
