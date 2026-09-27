using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;

public sealed class InventoryItemUseMessageUtilityTests
{
    [Test]
    public void FormatChooseTarget_IncludesItemName()
    {
        string message = InventoryItemUseMessageUtility.FormatChooseTarget("Chainling Update");
        Assert.AreEqual("Choose a Chainling to use Chainling Update on.", message);
    }

    [Test]
    public void FormatRecovered_UsesActualHealedAmount()
    {
        string message = InventoryItemUseMessageUtility.FormatRecovered("Voltbit", 18);
        Assert.AreEqual("Voltbit recovered 18 HP!", message);
    }

    [Test]
    public void FormatInvalid_MapsAlreadyFullHp()
    {
        string message = InventoryItemUseMessageUtility.FormatInvalid(
            "already_full_hp",
            "Voltbit",
            PlayerItemEffectFamily.HealSingle);
        Assert.AreEqual("Voltbit is already at full health!", message);
    }

    [Test]
    public void FormatInvalid_MapsReviveMismatch()
    {
        string message = InventoryItemUseMessageUtility.FormatInvalid(
            "target_not_fainted",
            "Voltbit",
            PlayerItemEffectFamily.Revive);
        Assert.AreEqual("Voltbit doesn't need a Chainling Reboot.", message);
    }

    [Test]
    public void FormatInvalid_MapsFaintedNeedsRevive()
    {
        string message = InventoryItemUseMessageUtility.FormatInvalid(
            "target_fainted_use_revive",
            "Voltbit",
            PlayerItemEffectFamily.HealSingle);
        Assert.AreEqual("Voltbit needs a Chainling Reboot!", message);
    }

    [Test]
    public void FormatPartyRecovered_IncludesTotals()
    {
        string message = InventoryItemUseMessageUtility.FormatPartyRecovered(30, 2);
        Assert.AreEqual("Party recovered 30 HP across 2 Chainlings!", message);
    }

    [Test]
    public void FormatFavorApplied_IncludesScoreAndDuration()
    {
        string message = InventoryItemUseMessageUtility.FormatFavorApplied(100, 6);
        Assert.AreEqual("Favor set to 100 for 6 battles.", message);
    }

    [Test]
    public void FormatInvalid_MapsPartyAlreadyFull()
    {
        string message = InventoryItemUseMessageUtility.FormatInvalid(
            "party_already_full_hp",
            "Party",
            PlayerItemEffectFamily.HealParty);
        Assert.AreEqual("The party is already at full health!", message);
    }

    [Test]
    public void FormatEffectLabel_PatchUsesHealAmount()
    {
        Assert.IsTrue(PlayerItemCatalogSeed.TryGet("chainling_update", out PlayerItemDefinition def));
        string label = InventoryItemEffectCopy.FormatEffectLabel(def);
        Assert.AreEqual("Restores 70 HP to one Chainling.", label);
    }
}
