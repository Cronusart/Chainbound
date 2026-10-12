using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Chainbound.Combat;

namespace Chainbound.Combat.Tests
{
    public class CasualPvpSpineTests
    {
        private const string PinnedMatchmakingHash = "f7354f292abebd03a5a1fee545d2f967ae8945339cf11bb78ad330a535800a43";

        private readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                    Object.DestroyImmediate(created[i]);
            }

            created.Clear();
            CasualPvpLaunchContext.Clear();
        }

        [Test]
        public void EditorAndDeviceShareTheCommittedBuildStamp()
        {
            Assert.AreEqual(64, PvpBuildStamp.Value.Length);
            Assert.AreNotEqual("editor", PvpBuildStamp.Value);
            StringAssert.IsMatch("^[0-9a-f]{64}$", PvpBuildStamp.Value);
        }

        [Test]
        public void MatchmakingHashMatchesTheServerCanonicalString()
        {
            MatchmakingConfigSO config = Track(MatchmakingConfigSO.CreateRuntimeDefault());
            Assert.AreEqual(PinnedMatchmakingHash, config.ComputeConfigHash());

            config.casualEnabled = false;
            Assert.AreNotEqual(PinnedMatchmakingHash, config.ComputeConfigHash());
        }

        [Test]
        public void ServerOverrideIsRefusedBeforeTheBakedHashMatches()
        {
            MatchmakingConfigSO baked = Track(MatchmakingConfigSO.CreateRuntimeDefault());
            MatchmakingConfigSO probe = Track(MatchmakingConfigSO.CreateRuntimeDefault());
            string claimed = baked.ComputeConfigHash();
            Assert.IsTrue(MatchmakingConfigSO.AcceptsServerBoard(baked, probe, claimed));

            probe.casualEnabled = false;
            Assert.IsFalse(MatchmakingConfigSO.AcceptsServerBoard(baked, probe, claimed));
            Assert.IsTrue(baked.casualEnabled);
        }

        [Test]
        public void CasualSeatBWinIsLocal()
        {
            var result = new BattleResult
            {
                winnerTeamIndex = 0,
                viewerTeamIndex = 1,
                casualSpineMatch = true,
            };
            Assert.IsFalse(result.PlayerWon);

            result.viewerTeamIndex = 0;
            Assert.IsTrue(result.PlayerWon);

            result.winnerTeamIndex = -2;
            Assert.IsTrue(result.IsDraw);
            Assert.IsFalse(result.PlayerWon);
        }

        [Test]
        public void CasualSessionDoesNotRequireAWagerOrchestrator()
        {
            var host = new GameObject("casual-spine-test");
            created.Add(host);
            var controller = host.AddComponent<CombatBattleController>();
            controller.BeginCasualPvp(new CasualPvpLaunchPayload
            {
                MatchId = "match-test",
                Seed = 17,
                LocalTeamIndex = 1,
                Seat = "b",
            });

            Assert.IsTrue(controller.IsCasualPvpSession);
            Assert.IsFalse(controller.IsPvP);
            Assert.AreEqual(1, controller.LocalControlledTeamIndex);
        }

        [Test]
        public void MatchingSeedsAndActionsProduceTheSameForensicHash()
        {
            CombatResolver left = CreateResolver(4242);
            CombatResolver right = CreateResolver(4242);
            TeamState leftA = CreateTeam(0, CreateChainling("alpha", 3, 20, 80));
            TeamState leftB = CreateTeam(1, CreateChainling("beta", 3, 18, 80));
            TeamState rightA = CreateTeam(0, CreateChainling("alpha", 3, 20, 80));
            TeamState rightB = CreateTeam(1, CreateChainling("beta", 3, 18, 80));
            leftA.chainlings[0].moves.Add(CreateMove("jab"));
            leftB.chainlings[0].moves.Add(CreateMove("jab"));
            rightA.chainlings[0].moves.Add(CreateMove("jab"));
            rightB.chainlings[0].moves.Add(CreateMove("jab"));

            CombatState leftState = left.InitializeBattle(leftA, leftB);
            CombatState rightState = right.InitializeBattle(rightA, rightB);
            left.ExecuteTurn(leftState, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            right.ExecuteTurn(rightState, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            string leftHash = BattleStatsAggregator.ComputeLegacyEventHash(left.Log.Events, 4242);
            string rightHash = BattleStatsAggregator.ComputeLegacyEventHash(right.Log.Events, 4242);
            Assert.AreEqual(leftHash, rightHash);
            Assert.IsNotEmpty(leftHash);
        }

        [Test]
        public void ForfeitAndDisputeStatesDoNotInventAWinner()
        {
            Assert.AreEqual(1, PvpPartyMapper.WinnerForForfeit("forfeit_a"));
            Assert.AreEqual(0, PvpPartyMapper.WinnerForForfeit("forfeit_b"));
            Assert.AreEqual(-1, PvpPartyMapper.WinnerForForfeit("disputed"));
            Assert.AreEqual(-1, PvpPartyMapper.WinnerForForfeit("cancelled"));
            Assert.IsTrue(PvpPartyMapper.IsTerminal("disputed"));
            Assert.IsTrue(PvpPartyMapper.IsTerminal("forfeit_a"));
            Assert.IsFalse(PvpPartyMapper.IsTerminal("in_progress"));
        }

        private CombatResolver CreateResolver(int seed)
        {
            var matrix = Track(ScriptableObject.CreateInstance<TypeChemistryMatrixSO>());
            var catalog = Track(ScriptableObject.CreateInstance<StatusCatalogSO>());
            catalog.BuildLookup();
            return new CombatResolver(matrix, catalog, seed, chemistryStrictMode: true);
        }

        private T Track<T>(T asset) where T : Object
        {
            created.Add(asset);
            return asset;
        }

        private static ChainlingInstance CreateChainling(string id, int ap, int speed, int maxHp)
        {
            var chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = ap,
                baseMaxHP = maxHp,
                currentHP = maxHp,
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
                baseSpeed = speed,
                primaryType = ChainlingType.Node,
                maxSlots = 4,
                isAlive = true,
            };
            chainling.SyncDisplayVitals();
            return chainling;
        }

        private static TeamState CreateTeam(int teamIndex, ChainlingInstance chainling)
        {
            var team = new TeamState(teamIndex, "Team " + teamIndex);
            team.chainlings.Add(chainling);
            return team;
        }

        private static MoveInstance CreateMove(string id)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = MoveCategory.DamagingMelee,
                baseType = ChainlingType.Node,
                targeting = MoveTargeting.SingleEnemy,
                contactType = ContactType.Contact,
                basePower = 20,
                apCost = 1,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }
    }
}
