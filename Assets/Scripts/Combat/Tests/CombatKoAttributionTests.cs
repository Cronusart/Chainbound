using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatKoAttributionTests
    {
        private readonly List<Object> _createdAssets = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void DirectAttackKoIncludesSourceAndMoveIds()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20, maxHp: 100);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 12);
            attacker.moves.Add(CreateMove("finisher", MoveCategory.DamagingMelee, 500));

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent ko = resolver.Log.Events.First(e => e.eventType == CombatEventType.ChainlingKO);
            Assert.AreEqual("attacker", ko.sourceId);
            Assert.AreEqual("target", ko.targetId);
            Assert.AreEqual("finisher", ko.moveId);
            Assert.AreEqual(0, ko.intValue);
            Assert.AreEqual(1, ko.intValue2);
        }

        [Test]
        public void DirectAttackKoWithBenchAutoSwapIncludesIncomingIds()
        {
            var resolver = CreateResolver();
            var attacker = CreateChainling("attacker", ap: 3, speed: 20, maxHp: 100);
            var target = CreateChainling("target", ap: 0, speed: 10, maxHp: 12);
            // ACTIVE_PER_TEAM is 2, so a true bench requires a 3rd roster member.
            var partner = CreateChainling("partner", ap: 0, speed: 5, maxHp: 80);
            var bench = CreateChainling("bench", ap: 2, speed: 8, maxHp: 80);
            attacker.moves.Add(CreateMove("finisher", MoveCategory.DamagingMelee, 500));

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target, partner, bench));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent autoSwap = resolver.Log.Events.First(e => e.eventType == CombatEventType.AutoSwapIn);
            Assert.AreEqual("target", autoSwap.sourceId);
            Assert.AreEqual("bench", autoSwap.targetId);
            Assert.AreEqual(0, autoSwap.intValue);
            Assert.AreEqual(1, autoSwap.intValue2);
        }

        [Test]
        public void LogKoWithoutSourceLeavesAttributionEmpty()
        {
            var log = new CombatEventLog(seed: 1);

            log.LogKO(turn: 3, targetId: "target");

            CombatEvent ko = log.Events[0];
            Assert.AreEqual(CombatEventType.ChainlingKO, ko.eventType);
            Assert.AreEqual("target", ko.targetId);
            Assert.IsEmpty(ko.sourceId);
            Assert.IsEmpty(ko.moveId);
        }

        private CombatResolver CreateResolver()
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            var catalog = CreateAsset<StatusCatalogSO>();
            catalog.BuildLookup();
            return new CombatResolver(matrix, catalog, seed: 1234, chemistryStrictMode: true);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
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

        private static TeamState CreateTeam(int teamIndex, params ChainlingInstance[] chainlings)
        {
            var team = new TeamState(teamIndex, $"Team {teamIndex}");
            team.chainlings.AddRange(chainlings);
            return team;
        }

        private static MoveInstance CreateMove(string id, MoveCategory category, int power)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                baseType = ChainlingType.Node,
                targeting = MoveTargeting.SingleEnemy,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.None,
                basePower = power,
                apCost = 1,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }
    }
}
