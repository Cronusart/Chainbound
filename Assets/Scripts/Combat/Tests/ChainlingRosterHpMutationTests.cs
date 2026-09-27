using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public class ChainlingRosterHpMutationTests
    {
        [Test]
        public void PatchRosterFromParticipants_UpdatesMatchingSnapshot()
        {
            var roster = new PlayerRosterSnapshot
            {
                chainlings =
                {
                    new PlayerRosterEntry
                    {
                        snapshot = new ChainlingSnapshot
                        {
                            backendChainlingId = "abc",
                            currentHp = ChainlingVitalsConstants.UnsyncedCurrentHp,
                        },
                    },
                },
            };

            int patched = ChainlingRosterHpMutation.PatchRosterFromParticipants(
                roster,
                new System.Collections.Generic.List<BattleParticipant>
                {
                    new BattleParticipant { backendChainlingId = "abc", endCurrentHp = 17 },
                });

            Assert.AreEqual(1, patched);
            Assert.AreEqual(17, roster.chainlings[0].snapshot.currentHp);
        }
    }
}
