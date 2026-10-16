using NUnit.Framework;
using Chainbound.Combat;

public sealed class StarterSelectionCopyTests
{
    [Test]
    public void FormatSpecies_PrefixesLabel()
    {
        Assert.AreEqual("Species: Validape", StarterSelectionCopy.FormatSpecies("Validape"));
    }

    [Test]
    public void FormatPortraitBanner_PutsTheSpeciesUnderTheCallout()
    {
        Assert.AreEqual("CAPTURED!\nHopoch", StarterSelectionCopy.FormatPortraitBanner("CAPTURED!\n{0}", "Hopoch"));
        Assert.AreEqual("Chainling", StarterSelectionCopy.FormatPortraitBanner("CAPTURED!", "  "));
    }

    [Test]
    public void FormatType_PrefixesLabel()
    {
        Assert.AreEqual("Type: Node", StarterSelectionCopy.FormatType("Node"));
    }

    [Test]
    public void FormatRole_PrefixesResolvedLabel()
    {
        Assert.AreEqual("Role: Support Warrior", StarterSelectionCopy.FormatRole(SpeciesCombatRole.Support));
        Assert.AreEqual("Role: Ranged Guardian", StarterSelectionCopy.FormatRole(SpeciesCombatRole.RangedGuard));
        Assert.AreEqual("Role: Speedy Striker", StarterSelectionCopy.FormatRole(SpeciesCombatRole.Speed));
    }

    [Test]
    public void FormatLore_StaysUnlabeledAndFallsBack()
    {
        Assert.AreEqual(
            "It lives in noisy troops and never travels far from its kin.",
            StarterSelectionCopy.FormatLore("It lives in noisy troops and never travels far from its kin."));
        Assert.AreEqual(
            StarterSelectionCopy.MissingLore,
            StarterSelectionCopy.FormatLore("  "));
    }
}
