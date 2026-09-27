using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation
{
    public enum InventoryItemCeremonyId
    {
        None = 0,
        PartyTarget = 1,
        PartyAoeConfirm = 2,
        SelfConfirm = 3,
        ItemInspect = 4,
        UtilityGearEquip = 5,
        CaptureBoostConfirm = 6,
    }

    /// <summary>
    /// Maps catalog effect families to Inventory Use/Inspect ceremony IDs.
    /// </summary>
    public static class InventoryItemCeremonyResolver
    {
        public static InventoryItemCeremonyId Resolve(PlayerItemDefinition definition)
        {
            if (definition == null)
                return InventoryItemCeremonyId.None;

            switch (definition.effectFamily)
            {
                case PlayerItemEffectFamily.HealSingle:
                case PlayerItemEffectFamily.StatusClear:
                case PlayerItemEffectFamily.Revive:
                    return InventoryItemCeremonyId.PartyTarget;
                case PlayerItemEffectFamily.HealParty:
                    return InventoryItemCeremonyId.PartyAoeConfirm;
                case PlayerItemEffectFamily.FavorBoost:
                    return InventoryItemCeremonyId.SelfConfirm;
                case PlayerItemEffectFamily.UtilityGear:
                    return InventoryItemCeremonyId.UtilityGearEquip;
                case PlayerItemEffectFamily.CaptureBoost:
                    return InventoryItemCeremonyId.CaptureBoostConfirm;
                case PlayerItemEffectFamily.CaptureTool:
                case PlayerItemEffectFamily.StampErase:
                case PlayerItemEffectFamily.ModifierReroll:
                case PlayerItemEffectFamily.CaseBinding:
                case PlayerItemEffectFamily.BindingMutationRoll:
                case PlayerItemEffectFamily.UnbindArchiveItem:
                case PlayerItemEffectFamily.ExtractBookMove:
                case PlayerItemEffectFamily.UnbindArchiveBook:
                case PlayerItemEffectFamily.CosmeticOwned:
                    return InventoryItemCeremonyId.ItemInspect;
                default:
                    // Non-menu-usable or unknown → inspect when possible.
                    if (!definition.usableInMenu)
                        return InventoryItemCeremonyId.ItemInspect;
                    return InventoryItemCeremonyId.None;
            }
        }

        public static bool IsConsumableCeremony(InventoryItemCeremonyId ceremony)
        {
            return ceremony == InventoryItemCeremonyId.PartyTarget
                || ceremony == InventoryItemCeremonyId.PartyAoeConfirm
                || ceremony == InventoryItemCeremonyId.SelfConfirm
                || ceremony == InventoryItemCeremonyId.CaptureBoostConfirm;
        }

        public static bool NeedsPartySlots(InventoryItemCeremonyId ceremony)
        {
            return ceremony == InventoryItemCeremonyId.PartyTarget
                || ceremony == InventoryItemCeremonyId.PartyAoeConfirm;
        }

        public static PlayerMenuSfxCue ResolveApplyTickCue(PlayerItemEffectFamily family)
        {
            switch (family)
            {
                case PlayerItemEffectFamily.Revive:
                    return PlayerMenuSfxCue.HealTick;
                case PlayerItemEffectFamily.FavorBoost:
                    return PlayerMenuSfxCue.FavorActivate;
                case PlayerItemEffectFamily.HealParty:
                    return PlayerMenuSfxCue.HealTick;
                default:
                    return PlayerMenuSfxCue.HealTick;
            }
        }

        public static PlayerMenuSfxCue ResolveApplyCompleteCue(PlayerItemEffectFamily family)
        {
            switch (family)
            {
                case PlayerItemEffectFamily.Revive:
                    return PlayerMenuSfxCue.ReviveComplete;
                case PlayerItemEffectFamily.HealParty:
                    return PlayerMenuSfxCue.PartyHealComplete;
                case PlayerItemEffectFamily.FavorBoost:
                    return PlayerMenuSfxCue.FavorActivate;
                default:
                    return PlayerMenuSfxCue.HealComplete;
            }
        }
    }
}
