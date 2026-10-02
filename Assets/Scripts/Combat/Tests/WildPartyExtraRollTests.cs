using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class WildPartyExtraRollTests
    {
        [Test]
        public void ResolveChance_ScalesWithEquippedCount_AndCapsAtThree()
        {
            EncounterAreaSO area = ScriptableObject.CreateInstance<EncounterAreaSO>();
            area.extraWildChanceAtOne = 0.15f;
            area.extraWildChanceAtTwo = 0.45f;
            area.extraWildChanceAtThree = 0.70f;
            EncounterZoneSO zone = ScriptableObject.CreateInstance<EncounterZoneSO>();
            zone.area = area;
            zone.extraWildChanceAtOne = -1f;
            zone.extraWildChanceAtTwo = -1f;
            zone.extraWildChanceAtThree = -1f;

            Assert.AreEqual(0.15f, WildPartyExtraRoll.ResolveChance(zone, 0), 0.001f);
            Assert.AreEqual(0.15f, WildPartyExtraRoll.ResolveChance(zone, 1), 0.001f);
            Assert.AreEqual(0.45f, WildPartyExtraRoll.ResolveChance(zone, 2), 0.001f);
            Assert.AreEqual(0.70f, WildPartyExtraRoll.ResolveChance(zone, 3), 0.001f);
            Assert.AreEqual(0.70f, WildPartyExtraRoll.ResolveChance(zone, 9), 0.001f);
        }

        [Test]
        public void TryAddPartner_SkipsBossAndFullParty_WithoutConsumingRng()
        {
            EncounterZoneSO zone = ScriptableObject.CreateInstance<EncounterZoneSO>();
            zone.extraWildChanceAtThree = 1f;
            var untouched = new System.Random(4);
            int expected = untouched.Next();

            var bossRng = new System.Random(4);
            Assert.IsFalse(WildPartyExtraRoll.TryAddPartner(
                zone, EncounterDifficulty.Boss, false, 1, 3, bossRng));
            Assert.AreEqual(expected, bossRng.Next());

            var introRng = new System.Random(4);
            Assert.IsFalse(WildPartyExtraRoll.TryAddPartner(
                zone, EncounterDifficulty.Standard, true, 1, 3, introRng));
            Assert.AreEqual(expected, introRng.Next());

            var fullRng = new System.Random(4);
            Assert.IsFalse(WildPartyExtraRoll.TryAddPartner(
                zone, EncounterDifficulty.Standard, false, 2, 3, fullRng));
            Assert.AreEqual(expected, fullRng.Next());
        }

        [Test]
        public void TryAddPartner_AddsOnlyWhenThePartyChanceHits()
        {
            EncounterZoneSO zone = ScriptableObject.CreateInstance<EncounterZoneSO>();
            zone.extraWildChanceAtOne = 0f;
            zone.extraWildChanceAtThree = 1f;

            Assert.IsFalse(WildPartyExtraRoll.TryAddPartner(
                zone, EncounterDifficulty.Standard, false, 1, 1, new System.Random(1)));
            Assert.IsTrue(WildPartyExtraRoll.TryAddPartner(
                zone, EncounterDifficulty.Elite, false, 1, 3, new System.Random(1)));
        }
    }
}
