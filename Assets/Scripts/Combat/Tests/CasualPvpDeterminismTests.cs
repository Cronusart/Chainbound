using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CasualPvpDeterminismTests
    {
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
        }

        [Test]
        public void MirrorMatchWithSpeedTieProducesTheSameLogAndMatchHash()
        {
            const string matchId = "match-determinism";
            const int seed = 4242;
            SimulatedSeat left = Prepare(seed);
            SimulatedSeat right = Prepare(seed);

            Assert.AreEqual("t0r0", left.Team0.chainlings[0].instanceId);
            Assert.AreEqual("t0r1", left.Team0.chainlings[1].instanceId);
            Assert.AreEqual("t1r0", left.Team1.chainlings[0].instanceId);
            Assert.AreEqual("t0r0m0", left.Team0.chainlings[0].moves[0].instanceId);
            Assert.AreEqual("t1r1m0", left.Team1.chainlings[1].moves[0].instanceId);
            Assert.Greater(left.Team0.chainlings[0].currentHP, 3);
            Assert.AreEqual(left.Team0.chainlings[0].EffectiveMaxHP, left.Team0.chainlings[0].currentHP);

            RunMirrorTurn(left);
            RunMirrorTurn(right);
            Assert.AreNotEqual(left.Resolver.Log.BattleId, right.Resolver.Log.BattleId);
            Assert.Greater(left.Resolver.Log.Events.Count, 0);
            Assert.AreEqual(Fingerprint(left.Resolver), Fingerprint(right.Resolver));
            Assert.IsTrue(HasTurnDigest(left.Resolver.Log.Events, 1));

            string leftHash = BattleStatsAggregator.ComputePvpForensicHash(
                matchId,
                BattleStatsAggregator.ComputeLegacyEventHash(left.Resolver.Log.Events, seed));
            string rightHash = BattleStatsAggregator.ComputePvpForensicHash(
                matchId,
                BattleStatsAggregator.ComputeLegacyEventHash(right.Resolver.Log.Events, seed));
            Assert.AreEqual(leftHash, rightHash);
            Assert.AreEqual(64, leftHash.Length);
            Assert.IsFalse(leftHash.Contains(left.Resolver.Log.BattleId));
        }

        private SimulatedSeat Prepare(int seed)
        {
            SpeciesDatabaseSO speciesDb = Track(ScriptableObject.CreateInstance<SpeciesDatabaseSO>());
            MoveDatabaseSO moveDb = Track(ScriptableObject.CreateInstance<MoveDatabaseSO>());
            SpeciesDefinitionSO species = Track(ScriptableObject.CreateInstance<SpeciesDefinitionSO>());
            MoveDefinitionSO move = Track(ScriptableObject.CreateInstance<MoveDefinitionSO>());
            species.speciesId = "mirrorling";
            species.displayName = "Mirrorling";
            species.baseMaxHP = 40;
            species.baseMeleeAtk = 12;
            species.baseMeleeDef = 12;
            species.baseRangedAtk = 12;
            species.baseRangedDef = 12;
            species.baseSpeed = 12;
            species.initialMoves = new List<MoveDefinitionSO> { move };
            move.moveId = "baseline_jab";
            move.displayName = "Quick Jab";
            move.category = MoveCategory.DamagingMelee;
            move.targeting = MoveTargeting.SingleEnemy;
            move.contactType = ContactType.Contact;
            move.power = 16;
            move.apCost = 1;
            move.accuracy = 100;
            move.hitCountMin = 1;
            move.hitCountMax = 1;
            speciesDb.species = new List<SpeciesDefinitionSO> { species };
            speciesDb.BuildLookup();
            moveDb.moves = new List<MoveDefinitionSO> { move };
            moveDb.BuildLookup();

            PartySnapshot party = MirrorParty();
            var rng = new CombatRng(seed);
            TeamState team0 = RosterSlotUtility.BuildTeamFromParty(0, party, speciesDb, moveDb, seed, rng);
            TeamState team1 = RosterSlotUtility.BuildTeamFromParty(1, MirrorParty(), speciesDb, moveDb, seed, rng);
            RosterSlotUtility.StampStablePvpIds(team0);
            RosterSlotUtility.StampStablePvpIds(team1);
            RosterSlotUtility.ApplyFullHeal(team0);
            RosterSlotUtility.ApplyFullHeal(team1);

            TypeChemistryMatrixSO matrix = Track(ScriptableObject.CreateInstance<TypeChemistryMatrixSO>());
            StatusCatalogSO catalog = Track(ScriptableObject.CreateInstance<StatusCatalogSO>());
            catalog.BuildLookup();
            var resolver = new CombatResolver(matrix, catalog, seed, chemistryStrictMode: true);
            CombatState state = resolver.InitializeBattle(team0, team1);
            return new SimulatedSeat(resolver, state, team0, team1);
        }

        private static void RunMirrorTurn(SimulatedSeat seat)
        {
            var actions0 = new List<CombatAction> { CombatAction.UseMove(0, 0), CombatAction.Pass(0) };
            var actions1 = new List<CombatAction> { CombatAction.UseMove(1, 0), CombatAction.Pass(1) };
            seat.Resolver.ExecuteTurn2v2(seat.State, actions0, actions1);
        }

        private static PartySnapshot MirrorParty()
        {
            return new PartySnapshot
            {
                teamName = "Mirror",
                chainlings = new List<ChainlingSnapshot>
                {
                    Chainling("lead"),
                    Chainling("partner"),
                },
            };
        }

        private static ChainlingSnapshot Chainling(string name)
        {
            return new ChainlingSnapshot
            {
                speciesId = "mirrorling",
                displayName = name,
                stars = 1,
                rebirthCount = 0,
                currentHp = 3,
                ivs = new[] { 0, 0, 0, 0, 0, 0 },
                moveIds = new List<string> { "baseline_jab" },
            };
        }

        private static string Fingerprint(CombatResolver resolver)
        {
            var builder = new StringBuilder();
            IReadOnlyList<CombatEvent> events = resolver.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                CombatEvent evt = events[i];
                builder.Append(evt.timestamp).Append(':')
                    .Append(evt.turn).Append(':')
                    .Append(evt.eventType).Append(':')
                    .Append(evt.sourceId).Append(':')
                    .Append(evt.targetId).Append(':')
                    .Append(evt.moveId).Append(':')
                    .Append(evt.intValue).Append(':')
                    .Append(evt.intValue2).Append(':')
                    .Append(evt.metadataJson).Append('|');
            }

            return builder.ToString();
        }

        private static bool HasTurnDigest(IReadOnlyList<CombatEvent> events, int turn)
        {
            string marker = "turn=" + turn + "|";
            for (int i = 0; i < events.Count; i++)
            {
                if (!string.IsNullOrEmpty(events[i].metadataJson) && events[i].metadataJson.StartsWith(marker))
                    return true;
            }

            return false;
        }

        private T Track<T>(T asset) where T : Object
        {
            created.Add(asset);
            return asset;
        }

        private sealed class SimulatedSeat
        {
            public SimulatedSeat(CombatResolver resolver, CombatState state, TeamState team0, TeamState team1)
            {
                Resolver = resolver;
                State = state;
                Team0 = team0;
                Team1 = team1;
            }

            public CombatResolver Resolver { get; }
            public CombatState State { get; }
            public TeamState Team0 { get; }
            public TeamState Team1 { get; }
        }
    }
}
