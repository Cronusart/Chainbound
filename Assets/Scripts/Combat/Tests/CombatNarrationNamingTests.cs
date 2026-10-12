using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatNarrationNamingTests
    {
        [Test]
        public void LocalPlayerTeam_UsesBareDisplayName()
        {
            var team = new TeamState(0, "Player");
            var chainling = new ChainlingInstance { displayName = "Validape" };

            Assert.AreEqual("Validape", CombatNarrationNaming.FormatChainlingName(team, chainling));
            Assert.IsNull(CombatNarrationNaming.ResolveTeamSubject("Player", 0));
        }

        [Test]
        public void WildOpponent_KeepsWildPrefix()
        {
            var team = new TeamState(1, "Wild");
            var chainling = new ChainlingInstance { displayName = "Kitsauto" };

            Assert.AreEqual("Wild Kitsauto", CombatNarrationNaming.FormatChainlingName(team, chainling));
        }

        [Test]
        public void EntangledOpponent_UsesEntangledPrefix()
        {
            var team = new TeamState(1, "Wild");
            var chainling = new ChainlingInstance
            {
                displayName = "Kitsauto",
                isEntangled = true,
                entangledType = ChainlingType.Burned,
            };

            Assert.AreEqual("Entangled Kitsauto", CombatNarrationNaming.FormatChainlingName(team, chainling));
        }

        [Test]
        public void BossOpponent_UsesBossPrefixInsteadOfRepeatingTheName()
        {
            var team = new TeamState(1, "Boss");
            var chainling = new ChainlingInstance { displayName = "Cofi Lootwak" };

            Assert.AreEqual("Boss Cofi Lootwak", CombatNarrationNaming.FormatChainlingName(team, chainling));
        }

        [Test]
        public void PvpOpponentNamedPlayer_UsesEnemyPrefix()
        {
            var team = new TeamState(1, "Player");
            var chainling = new ChainlingInstance { displayName = "Gilarime" };

            Assert.AreEqual("Enemy Gilarime", CombatNarrationNaming.FormatChainlingName(team, chainling));
            Assert.AreEqual("Enemy", CombatNarrationNaming.ResolveTeamSubject("Opponent", 1));
        }
    }
}
