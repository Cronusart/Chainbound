using System.Reflection;
using Chainbound.Combat;
using NUnit.Framework;
using SignalJam.UI.MenuNavigation;

public sealed class ChainlingCaptureDossierFallbackTests
{
    [Test]
    public void ProvisionalEntry_RetainsCommittedIdentityAndPartyPlacement()
    {
        var request = new CaptureDossierRequest
        {
            chainlingId = "captured-chainling-id",
            fallbackDisplayName = "Rattlestake",
            addedToParty = true,
            fallbackSnapshot = new ChainlingSnapshot
            {
                speciesId = "rattlestake",
            },
        };
        var roster = new PlayerRosterSnapshot { partyCount = 1 };
        roster.chainlings.Add(new PlayerRosterEntry
        {
            snapshot = new ChainlingSnapshot { speciesId = "existing-party-member" },
            isPartyMember = true,
            partySlot = 0,
        });

        MethodInfo method = typeof(ChainlingCaptureDossierPresenter).GetMethod(
            "BuildProvisionalEntry",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var entry = method.Invoke(null, new object[] { request, roster }) as PlayerRosterEntry;

        Assert.NotNull(entry);
        Assert.AreEqual("captured-chainling-id", entry.snapshot.backendChainlingId);
        Assert.AreEqual("Rattlestake", entry.snapshot.displayName);
        Assert.IsTrue(entry.isPartyMember);
        Assert.AreEqual(1, entry.partySlot);
    }
}
