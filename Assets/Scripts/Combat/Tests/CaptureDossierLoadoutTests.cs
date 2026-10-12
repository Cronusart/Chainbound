using System.Collections.Generic;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CaptureDossierLoadoutTests
    {
        [Test]
        public void ApplyOwnedMove_UsesKnownMoveInsteadOfFightKit()
        {
            var source = new ChainlingSnapshot
            {
                speciesId = "kitsauto",
                knownMoveId = "baseline_jab",
                moveIds = new List<string> { "multi_thread_shot", "baseline_jab", "fox_feint" },
                modifier1SectionIds = new List<string> { "kit-a", "owned-a", "kit-c" },
                modifier2SectionIds = new List<string> { "kit-b", "owned-b", "kit-d" },
            };
            var dossier = new ChainlingSnapshot();

            CaptureDossierLoadout.ApplyOwnedMove(dossier, source);

            Assert.AreEqual("baseline_jab", dossier.knownMoveId);
            CollectionAssert.AreEqual(new[] { "baseline_jab" }, dossier.moveIds);
            CollectionAssert.AreEqual(new[] { "owned-a" }, dossier.modifier1SectionIds);
            CollectionAssert.AreEqual(new[] { "owned-b" }, dossier.modifier2SectionIds);
        }

        [Test]
        public void ApplyOwnedMove_LeavesMovesEmptyWhenKnownMoveIsMissing()
        {
            var source = new ChainlingSnapshot
            {
                knownMoveId = "  ",
                moveIds = new List<string> { "multi_thread_shot", "fox_feint", "blade_flurry" },
                modifier1SectionIds = new List<string> { "kit-a" },
            };
            var dossier = new ChainlingSnapshot
            {
                moveIds = new List<string> { "stale" },
            };

            CaptureDossierLoadout.ApplyOwnedMove(dossier, source);

            Assert.AreEqual(string.Empty, dossier.knownMoveId);
            Assert.AreEqual(0, dossier.moveIds.Count);
            Assert.AreEqual(0, dossier.modifier1SectionIds.Count);
        }

        [Test]
        public void ApplyOwnedMove_SkipsModifiersWhenKnownMoveIsNotInTheKit()
        {
            var source = new ChainlingSnapshot
            {
                knownMoveId = "baseline_jab",
                moveIds = new List<string> { "multi_thread_shot", "fox_feint" },
                modifier1SectionIds = new List<string> { "kit-a", "kit-c" },
            };
            var dossier = new ChainlingSnapshot();

            CaptureDossierLoadout.ApplyOwnedMove(dossier, source);

            CollectionAssert.AreEqual(new[] { "baseline_jab" }, dossier.moveIds);
            Assert.AreEqual(0, dossier.modifier1SectionIds.Count);
            Assert.AreEqual(0, dossier.modifier2SectionIds.Count);
        }
    }
}
