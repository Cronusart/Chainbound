using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class ClosedBetaFeaturePolicyTests
    {
        [TestCase("create_profile")]
        [TestCase("create_move_vault")]
        [TestCase("initialize_chainling_vault")]
        [TestCase("initialize_equipped_registry")]
        [TestCase("update_position")]
        public void ApprovedBootstrapAndPositionInstructionsAreAllowed(string instruction)
        {
            Assert.That(ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed(instruction), Is.True);
        }

        [TestCase("update_wins_losses")]
        [TestCase("add_ordinal")]
        [TestCase("add_chainling_from_capture")]
        [TestCase("create_chainling")]
        [TestCase("spend_ram")]
        [TestCase("burn_ordinal_for_ram")]
        [TestCase("set_equipped_move")]
        [TestCase("mint_move")]
        [TestCase("vending")]
        [TestCase("vrf")]
        [TestCase("ranked_wager")]
        [TestCase("")]
        [TestCase(null)]
        public void EconomicAndUnlistedInstructionsFailClosed(string instruction)
        {
            Assert.That(ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed(instruction), Is.False);
        }

        [Test]
        public void InstructionNamesAreCaseSensitive()
        {
            Assert.That(ClosedBetaFeaturePolicy.IsPlayerInstructionAllowed("Create_Profile"), Is.False);
        }
    }
}
