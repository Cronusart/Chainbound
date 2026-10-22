using System.Reflection;
using Chainbound.Combat.Core;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class AuthoritativeFleeCommandTests
    {
        [Test]
        public void FleeBuildsTerminalAuthorityCommandWithoutClaimingDefeat()
        {
            var state = new CombatState
            {
                team0 = TeamWith("player-chainling", 24, 3),
                team1 = TeamWith("wild-chainling", 18, 0),
            };
            var envelope = new CombatSessionEnvelope
            {
                BattleId = "battle-123",
                Version = 7,
                IsResumable = true,
            };
            MethodInfo method = typeof(CombatBattleController).GetMethod(
                "BuildAuthoritativeFleeCommand",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(method);
            var command = (CombatTeamCommand)method.Invoke(null, new object[] { envelope, state, 4 });

            Assert.AreEqual("battle-123:7:flee", command.CommandId);
            Assert.AreEqual(7, command.ExpectedVersion);
            Assert.AreEqual("action_step", command.Kind);
            Assert.AreEqual("flee", command.ActionId);
            Assert.IsFalse(command.BattleActive);
            Assert.AreEqual(-1, command.WinnerTeamIndex);
            Assert.AreEqual("Flee", command.EndReason);
            Assert.AreEqual(2, command.Vitals.Count);
        }

        private static TeamState TeamWith(string id, int hp, int ap)
        {
            var team = new TeamState();
            team.chainlings.Add(new ChainlingInstance
            {
                instanceId = id,
                currentHP = hp,
                currentAP = ap,
                isAlive = hp > 0,
            });
            team.activeIndices.Add(0);
            return team;
        }
    }
}
