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
    public void FormatCount_ShowsZeroAndClampsNegative()
    {
        Assert.AreEqual("0", ProfileCardFormatter.FormatCount(0));
        Assert.AreEqual("0", ProfileCardFormatter.FormatCount(-4));
        Assert.AreEqual(1250.ToString("N0"), ProfileCardFormatter.FormatCount(1250));
    }

    [Test]
    public void FormatRam_ShowsZeroAndClampsNegative()
    {
        Assert.AreEqual("0", ProfileCardFormatter.FormatRam(0L));
        Assert.AreEqual("0", ProfileCardFormatter.FormatRam(-8L));
        Assert.AreEqual(12500L.ToString("N0"), ProfileCardFormatter.FormatRam(12500L));
    }

    [Test]
    public void CareerLabels_DoNotMentionInscriptionsOrStamps()
    {
        string joined = string.Join(" ", ProfileCardFormatter.CareerLabels).ToLowerInvariant();
        Assert.IsFalse(joined.Contains("inscribed"));
        Assert.IsFalse(joined.Contains("stamped"));
        Assert.Contains("UNIQUE SPECIES", ProfileCardFormatter.CareerLabels);
        Assert.Contains("RAM EARNED", ProfileCardFormatter.CareerLabels);
        Assert.Contains("KO'D", ProfileCardFormatter.CareerLabels);
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
}
