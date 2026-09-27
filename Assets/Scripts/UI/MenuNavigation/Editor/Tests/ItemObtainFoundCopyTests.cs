using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

public sealed class ItemObtainFoundCopyTests
{
    [Test]
    public void FormatTitle_UsesPromptFormAndFieldColor()
    {
        string hex = ColorUtility.ToHtmlStringRGB(PlayerItemCategoryPalette.Field);
        string title = ItemObtainFoundCopy.FormatTitle("Chainling Patch", PlayerItemCategory.Field);
        Assert.AreEqual($"You've found a <color=#{hex}>chainling patch</color>!", title);
    }

    [Test]
    public void FormatTitle_UsesAnForVowelNames()
    {
        string hex = ColorUtility.ToHtmlStringRGB(PlayerItemCategoryPalette.Archive);
        string title = ItemObtainFoundCopy.FormatTitle("Archive Tome", PlayerItemCategory.Archive);
        Assert.AreEqual($"You've found an <color=#{hex}>archive tome</color>!", title);
    }

    [Test]
    public void FormatTitle_EscapesRichTextInName()
    {
        string hex = ColorUtility.ToHtmlStringRGB(PlayerItemCategoryPalette.Quest);
        string title = ItemObtainFoundCopy.FormatTitle("Quest <flag>", PlayerItemCategory.Quest);
        Assert.AreEqual($"You've found a <color=#{hex}>quest &lt;flag&gt;</color>!", title);
    }

    [Test]
    public void FormatTitle_MultipleUsesQtyThenItemName()
    {
        string hex = ColorUtility.ToHtmlStringRGB(PlayerItemCategoryPalette.Field);
        string title = ItemObtainFoundCopy.FormatTitle("Chainling Patch", PlayerItemCategory.Field, 3);
        Assert.AreEqual($"You've found x3 <color=#{hex}>Chainling Patch</color>!", title);
    }

    [Test]
    public void Resolve_MatchesInventoryTabDefaults()
    {
        Assert.AreEqual(new Color(0.2f, 0.85f, 0.75f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Field));
        Assert.AreEqual(new Color(0.95f, 0.52f, 0.22f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Capture));
        Assert.AreEqual(new Color(0.52f, 0.78f, 1f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Favor));
        Assert.AreEqual(new Color(0.8f, 0.62f, 1f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Bindery));
        Assert.AreEqual(new Color(1f, 0.78f, 0.28f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Archive));
        Assert.AreEqual(new Color(1f, 0.48f, 0.7f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Cosmetic));
        Assert.AreEqual(new Color(0.7f, 0.9f, 0.42f, 1f), PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Quest));
        Assert.AreEqual(PlayerItemCategoryPalette.Field, PlayerItemCategoryPalette.Resolve(PlayerItemCategory.Utility));
    }
}
