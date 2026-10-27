using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class BossNoDrawTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void BossMutualWipe_PlayerFallsSecond_PlayerWins()
        {
            CombatState state = CreateMutualWipe(isBoss: true, firstKoTeam: 1, secondKoTeam: 0, out CombatResolver resolver);

            Assert.That(CheckBattleEnd(resolver, state), Is.True);
            Assert.That(state.winnerTeamIndex, Is.EqualTo(0));
            Assert.That(state.winnerTeamIndex, Is.Not.EqualTo(-2));
        }

        [Test]
        public void BossMutualWipe_PlayerFallsFirst_PlayerLoses()
        {
            CombatState state = CreateMutualWipe(isBoss: true, firstKoTeam: 0, secondKoTeam: 1, out CombatResolver resolver);

            Assert.That(CheckBattleEnd(resolver, state), Is.True);
            Assert.That(state.winnerTeamIndex, Is.EqualTo(1));
            Assert.That(state.winnerTeamIndex, Is.Not.EqualTo(-2));
        }

        [Test]
        public void BossMutualWipe_WithoutKoOrder_PlayerLoses()
        {
            CombatState state = CreateMutualWipe(isBoss: true, firstKoTeam: -1, secondKoTeam: -1, out CombatResolver resolver);

            Assert.That(CheckBattleEnd(resolver, state), Is.True);
            Assert.That(state.winnerTeamIndex, Is.EqualTo(1));
        }

        [Test]
        public void NonBossMutualWipe_RemainsDraw()
        {
            CombatState state = CreateMutualWipe(isBoss: false, firstKoTeam: 0, secondKoTeam: 1, out CombatResolver resolver);

            Assert.That(CheckBattleEnd(resolver, state), Is.True);
            Assert.That(state.winnerTeamIndex, Is.EqualTo(-2));
        }

        private CombatState CreateMutualWipe(
            bool isBoss,
            int firstKoTeam,
            int secondKoTeam,
            out CombatResolver resolver)
        {
            TypeChemistryMatrixSO matrix = CreateAsset<TypeChemistryMatrixSO>();
            StatusCatalogSO catalog = CreateAsset<StatusCatalogSO>();
            catalog.BuildLookup();
            resolver = new CombatResolver(matrix, catalog, 41);

            ChainlingInstance player = CreateChainling("player");
            ChainlingInstance opponent = CreateChainling("boss");
            var playerTeam = new TeamState(0, "Player");
            var bossTeam = new TeamState(1, "Boss");
            playerTeam.chainlings.Add(player);
            bossTeam.chainlings.Add(opponent);
            CombatState state = resolver.InitializeBattle(playerTeam, bossTeam, isBoss);

            if (firstKoTeam >= 0)
                state.turnActionLedger.RecordKo(firstKoTeam == 0 ? player.instanceId : opponent.instanceId, firstKoTeam);
            if (secondKoTeam >= 0)
                state.turnActionLedger.RecordKo(secondKoTeam == 0 ? player.instanceId : opponent.instanceId, secondKoTeam);
            player.isAlive = false;
            player.currentHP = 0;
            opponent.isAlive = false;
            opponent.currentHP = 0;
            return state;
        }

        private static bool CheckBattleEnd(CombatResolver resolver, CombatState state)
        {
            MethodInfo method = typeof(CombatResolver).GetMethod("CheckBattleEnd", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(resolver, new object[] { state });
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        private static ChainlingInstance CreateChainling(string id) => new ChainlingInstance
        {
            instanceId = id,
            speciesId = id,
            displayName = id,
            currentHP = 10,
            baseMaxHP = 10,
            isAlive = true,
        };
    }
}
