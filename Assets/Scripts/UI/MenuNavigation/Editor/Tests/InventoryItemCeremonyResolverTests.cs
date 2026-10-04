using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;

public sealed class InventoryItemCeremonyResolverTests
{
    [Test]
    public void Resolve_MapsHealFamiliesToPartyTarget()
    {
        Assert.AreEqual(
            InventoryItemCeremonyId.PartyTarget,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.HealSingle)));
        Assert.AreEqual(
            InventoryItemCeremonyId.PartyTarget,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.StatusClear)));
        Assert.AreEqual(
            InventoryItemCeremonyId.PartyTarget,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.Revive)));
    }

    [Test]
    public void Resolve_MapsHealPartyToAoe()
    {
        Assert.AreEqual(
            InventoryItemCeremonyId.PartyAoeConfirm,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.HealParty)));
    }

    [Test]
    public void Resolve_MapsFavorToSelfConfirm()
    {
        Assert.AreEqual(
            InventoryItemCeremonyId.SelfConfirm,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.FavorBoost)));
    }

    [Test]
    public void Resolve_MapsMovementSpeedBoostToSelfConfirm()
    {
        Assert.AreEqual(
            InventoryItemCeremonyId.SelfConfirm,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.MovementSpeedBoost)));
        Assert.IsTrue(InventoryItemCeremonyResolver.IsConsumableCeremony(InventoryItemCeremonyId.SelfConfirm));
    }

    [Test]
    public void Resolve_MapsCaptureAndBinderyToInspect()
    {
        Assert.AreEqual(
            InventoryItemCeremonyId.ItemInspect,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.CaptureTool)));
        Assert.AreEqual(
            InventoryItemCeremonyId.ItemInspect,
            InventoryItemCeremonyResolver.Resolve(Def(PlayerItemEffectFamily.UnbindArchiveItem)));
    }

    [Test]
    public void Seed_CaptureNotMenuUsable_FieldIs()
    {
        Assert.IsTrue(PlayerItemCatalogSeed.TryGet("chainling_update", out PlayerItemDefinition patch));
        Assert.IsTrue(patch.usableInMenu);
        Assert.IsTrue(PlayerItemCatalogSeed.TryGet("signal_orb", out PlayerItemDefinition orb));
        Assert.IsFalse(orb.usableInMenu);
        Assert.IsTrue(orb.usableInBattle);
    }

    private static PlayerItemDefinition Def(PlayerItemEffectFamily family)
    {
        return new PlayerItemDefinition
        {
            itemId = "test",
            effectFamily = family,
        };
    }
}
