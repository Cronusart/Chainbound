using NUnit.Framework;
using SignalJam.UI.MenuNavigation;

public sealed class ProfileCardFormatterTests
{
    [Test]
    public void FormatDisplayName_PrefixesDollar()
    {
        Assert.AreEqual("$BONK", ProfileCardFormatter.FormatDisplayName("BONK"));
    }

    [Test]
    public void FormatDisplayName_KeepsExistingPrefix()
    {
        Assert.AreEqual("$BONK", ProfileCardFormatter.FormatDisplayName("$BONK", "Other"));
    }

    [Test]
    public void FormatDisplayName_PrefersSessionThenStatsThenFallback()
    {
        Assert.AreEqual("$FromStats", ProfileCardFormatter.FormatDisplayName("", "FromStats"));
        Assert.AreEqual("$PLAYER", ProfileCardFormatter.FormatDisplayName(" ", null));
    }

    [Test]
    public void FormatLinkChip_GuestVersusSecured()
    {
        Assert.AreEqual("GUEST", ProfileCardFormatter.FormatLinkChip(true));
        Assert.AreEqual("SECURED", ProfileCardFormatter.FormatLinkChip(false));
    }

    [Test]
    public void FormatRecord_HidesZeroZero()
    {
        Assert.AreEqual("", ProfileCardFormatter.FormatRecord(0, 0));
        Assert.AreEqual("1W-0L", ProfileCardFormatter.FormatRecord(1, 0));
        Assert.AreEqual("0W-2L", ProfileCardFormatter.FormatRecord(0, 2));
    }

    [Test]
    public void FormatShareCopy_SubstitutesName()
    {
        string copy = ProfileCardFormatter.FormatShareCopy("just checked $NAME's chainbound id", "BONK");
        Assert.AreEqual("just checked $BONK's chainbound id", copy);
    }

    [Test]
    public void FormatShareCopy_UsesDefaultTemplateWhenEmpty()
    {
        string copy = ProfileCardFormatter.FormatShareCopy("  ", "Ada");
        Assert.AreEqual("just checked $Ada's chainbound id", copy);
    }

    [Test]
    public void FormatStatLines_ClampNegativeAndAlwaysShow()
    {
        Assert.AreEqual("3 species", ProfileCardFormatter.FormatUniqueSpecies(3));
        Assert.AreEqual("0 species", ProfileCardFormatter.FormatUniqueSpecies(-2));
        Assert.AreEqual("4 captured   1 entangled", ProfileCardFormatter.FormatCaptures(4, 1));
        Assert.AreEqual("2 stamped", ProfileCardFormatter.FormatStampedMoves(2));
    }
}
