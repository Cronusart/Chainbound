using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class EncounterFleePolicyTests
    {
        [Test]
        public void Evaluate_StandardEncounter_AllowsPlayerFlee()
        {
            var result = EncounterFleePolicyResolver.Evaluate(
                battleIsActive: true,
                fleeIsResolving: false,
                isPvP: false,
                new EncounterSnapshot { difficulty = EncounterDifficulty.Standard });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(EncounterFleePolicy.Allowed, result.Policy);
        }

        [Test]
        public void Evaluate_BossEncounter_LocksPlayerFleeByDefault()
        {
            var result = EncounterFleePolicyResolver.Evaluate(
                battleIsActive: true,
                fleeIsResolving: false,
                isPvP: false,
                new EncounterSnapshot { difficulty = EncounterDifficulty.Boss });

            Assert.AreEqual(FleeRequestStatus.LockedByEncounter, result.Status);
            Assert.AreEqual(EncounterFleePolicy.Locked, result.Policy);
        }

        [Test]
        public void Evaluate_ExplicitEncounterPolicy_OverridesDifficultyDefault()
        {
            var result = EncounterFleePolicyResolver.Evaluate(
                battleIsActive: true,
                fleeIsResolving: false,
                isPvP: false,
                new EncounterSnapshot
                {
                    difficulty = EncounterDifficulty.Boss,
                    fleePolicy = EncounterFleePolicy.Allowed,
                });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(EncounterFleePolicy.Allowed, result.Policy);
        }

        [Test]
        public void Evaluate_Pvp_AlwaysRejectsPlayerFlee()
        {
            var result = EncounterFleePolicyResolver.Evaluate(
                battleIsActive: true,
                fleeIsResolving: false,
                isPvP: true,
                new EncounterSnapshot { difficulty = EncounterDifficulty.Standard });

            Assert.AreEqual(FleeRequestStatus.UnavailableInPvP, result.Status);
        }
    }
}
