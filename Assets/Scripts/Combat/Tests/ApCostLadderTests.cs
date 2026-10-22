using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class ApCostLadderTests
    {
        private readonly List<ScriptableObject> _createdAssets = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                if (_createdAssets[i] != null)
                    UnityEngine.Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void RegenOnlyCalendar_PaysFourOnTurnTwoAndFiveOnTurnThree()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance actor = Chainling("actor", 20);
            ChainlingInstance foe = Chainling("foe", 10);
            MoveInstance four = Move("four", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 4, 30);
            MoveInstance five = Move("five", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 5, 30);
            var eligibility = new CombatActionEligibilityService();
            CombatState state = resolver.InitializeBattle(Team(0, actor), Team(1, foe));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(2, actor.currentAP);
            Assert.IsFalse(eligibility.EvaluateMoveSelection(actor, four, state).canSelect);
            Assert.AreEqual("InsufficientAP", eligibility.EvaluateMoveSelection(actor, four, state).reason);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(4, actor.currentAP);
            Assert.IsTrue(eligibility.EvaluateMoveSelection(actor, four, state).canSelect);
            Assert.IsFalse(eligibility.EvaluateMoveSelection(actor, five, state).canSelect);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(6, actor.currentAP);
            Assert.IsTrue(eligibility.EvaluateMoveSelection(actor, five, state).canSelect);
        }

        [Test]
        public void TurnStartApPassive_OpensTurnOneAtThreeBeforeSelection()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance actor = Chainling("actor", 20);
            MoveInstance passive = Move("turn_start_ap_gain", MoveCategory.Passive, MoveTargeting.Self, 0, 0);
            passive.triggerCondition = PassiveTriggerCondition.TurnStart;
            passive.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.GainAP,
                apGain = 1,
                chance = 1f,
                targetOverride = MoveTargeting.Self,
            });
            actor.moves.Add(passive);

            CombatState state = resolver.InitializeBattle(Team(0, actor), Team(1, Chainling("foe", 10)));
            resolver.BeginTurn(state);

            Assert.AreEqual(TurnPhase.ActionSelection, state.currentPhase);
            Assert.AreEqual(3, actor.currentAP);
        }

        [Test]
        public void FasterAllyGift_PaysSameQueueAndSlowerGiftDoesNot()
        {
            int damaged = RunGiftBattle(giverSpeed: 40, recipientSpeed: 10);
            Assert.Greater(damaged, 0);

            int untouched = RunGiftBattle(giverSpeed: 10, recipientSpeed: 40);
            Assert.AreEqual(0, untouched);
        }

        [Test]
        public void MoveMenu_CountsOnlyAFasterAllyCommittedGift()
        {
            var eligibility = new CombatActionEligibilityService();
            ChainlingInstance giver = Chainling("giver", 40);
            ChainlingInstance recipient = Chainling("recipient", 10);
            giver.currentAP = 2;
            recipient.currentAP = 2;
            MoveInstance donation = Move("ap_donation", MoveCategory.Support, MoveTargeting.SingleAlly, 2, 0);
            MoveInstance heavy = Move("heavy", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 4, 40);
            giver.moves.Add(donation);
            recipient.moves.Add(heavy);
            TeamState team = Team(0, giver, recipient);
            team.activeIndices.Add(0);
            team.activeIndices.Add(1);

            var queued = new Dictionary<int, CombatAction>
            {
                { 0, CombatAction.UseMove(0, 0, 0, 1) },
            };

            int gift = eligibility.ResolveCommittedFasterGiftAp(team, recipient, 1, queued);
            Assert.AreEqual(2, gift);
            Assert.IsTrue(eligibility.EvaluateMoveSelection(recipient, heavy, new CombatState(), extraAvailableAp: gift).canSelect);
            Assert.IsFalse(eligibility.EvaluateMoveSelection(recipient, heavy, new CombatState()).canSelect);

            giver.baseSpeed = 10;
            recipient.baseSpeed = 10;
            Assert.AreEqual(0, eligibility.ResolveCommittedFasterGiftAp(team, recipient, 1, queued));

            giver.baseSpeed = 5;
            recipient.baseSpeed = 20;
            Assert.AreEqual(0, eligibility.ResolveCommittedFasterGiftAp(team, recipient, 1, queued));

            giver.baseSpeed = 40;
            recipient.baseSpeed = 10;
            queued[0] = CombatAction.UseMove(0, 0);
            Assert.AreEqual(2, eligibility.ResolveCommittedFasterGiftAp(team, recipient, 1, queued));

            ChainlingInstance extra = Chainling("extra", 8);
            team.chainlings.Add(extra);
            team.activeIndices.Add(2);
            Assert.AreEqual(0, eligibility.ResolveCommittedFasterGiftAp(team, recipient, 1, queued));
        }

        [Test]
        public void AllIn_SpendsRemainingApAndSkipsTheNextTurn()
        {
            int fromFour = RunAllIn(startingAp: 2, out int apAfterFour, out CombatResolver resolver, out CombatState state, out ChainlingInstance actor, out MoveInstance move);
            int fromSix = RunAllIn(startingAp: 4, out int apAfterSix, out _, out _, out _, out _);

            Assert.AreEqual(0, apAfterFour);
            Assert.AreEqual(0, apAfterSix);
            Assert.Greater(fromFour, 0);
            Assert.AreEqual(fromFour, fromSix);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.APSpent &&
                e.message == "all_in spent 4 AP."));

            resolver.BeginTurn(state);
            actor.currentAP = 4;
            MoveSelectionEvaluation nextTurn = new CombatActionEligibilityService().EvaluateMoveSelection(actor, move, state);
            Assert.IsFalse(nextTurn.canSelect);
            Assert.AreEqual("MoveRecharging", nextTurn.reason);
        }

        [Test]
        public void FeeCollector_DrainsOnlyTheFirstOpposingActorWhenAhead()
        {
            RunFeeCollector(holderStartAp: 1, firstStartAp: 3, secondStartAp: 3, firstSpeed: 40, secondSpeed: 10, out int firstAp, out int secondAp);
            Assert.AreEqual(4, firstAp);
            Assert.AreEqual(5, secondAp);

            RunFeeCollector(holderStartAp: 3, firstStartAp: 3, secondStartAp: 3, firstSpeed: 40, secondSpeed: 10, out firstAp, out secondAp);
            Assert.AreEqual(5, firstAp);
            Assert.AreEqual(5, secondAp);

            RunFeeCollector(holderStartAp: 4, firstStartAp: 0, secondStartAp: 3, firstSpeed: 40, secondSpeed: 10, out firstAp, out secondAp);
            Assert.AreEqual(2, firstAp);
            Assert.AreEqual(5, secondAp);
        }

        [Test]
        public void FeeCollectorLogsPassiveTriggerAndReadableDrain()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance holder = Chainling("holder", 20);
            holder.currentAP = 1;
            holder.moves.Add(Move("fee_collector", MoveCategory.Passive, MoveTargeting.Self, 0, 0));
            ChainlingInstance foe = Chainling("foe", 10);
            foe.currentAP = 3;
            CombatState state = resolver.InitializeBattle(
                Team(0, holder),
                Team(1, foe));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.PassiveTriggered &&
                e.message == "holder's Fee Collector drained 1 AP from foe."));
        }

        [Test]
        public void FeeCollector_PriorityAndSwapsCountAsTheFirstActor()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance holder = Chainling("holder", 20);
            holder.currentAP = 1;
            holder.moves.Add(Move("fee_collector", MoveCategory.Passive, MoveTargeting.Self, 0, 0));
            ChainlingInstance partner = Chainling("partner", 5);

            ChainlingInstance priorityFoe = Chainling("priority", 5);
            ChainlingInstance fastFoe = Chainling("fast", 40);
            priorityFoe.currentAP = 3;
            fastFoe.currentAP = 3;
            MoveInstance priorityMove = Move("priority_jab", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 10);
            priorityMove.tags.Add("PRIOR");
            priorityFoe.moves.Add(priorityMove);
            fastFoe.moves.Add(Move("fast_jab", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 10));

            CombatState state = resolver.InitializeBattle(
                Team(0, holder, partner),
                Team(1, priorityFoe, fastFoe));
            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.Pass(0), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.UseMove(1, 0), CombatAction.Pass(1) });

            Assert.AreEqual(4, priorityFoe.currentAP);
            Assert.AreEqual(5, fastFoe.currentAP);

            CombatResolver swapResolver = CreateResolver();
            ChainlingInstance swapHolder = Chainling("holder", 20);
            swapHolder.currentAP = 1;
            swapHolder.moves.Add(Move("fee_collector", MoveCategory.Passive, MoveTargeting.Self, 0, 0));
            ChainlingInstance swapper = Chainling("swapper", 5);
            ChainlingInstance quick = Chainling("quick", 40);
            ChainlingInstance bench = Chainling("bench", 8);
            swapper.currentAP = 3;
            quick.currentAP = 3;
            CombatState swapState = swapResolver.InitializeBattle(
                Team(0, swapHolder, Chainling("partner", 4)),
                Team(1, swapper, quick, bench));
            swapResolver.ExecuteTurn2v2(
                swapState,
                new List<CombatAction> { CombatAction.Pass(0), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Swap(1, 2), CombatAction.Pass(1) });

            Assert.AreEqual(4, swapper.currentAP);
            Assert.AreEqual(5, quick.currentAP);
        }

        [Test]
        public void ApDrain_HitsForSixteenCutsOneAndGainsOnlyOnTheHit()
        {
            bool sawGain = false;
            bool sawNoGain = false;
            for (int seed = 1; seed <= 80 && (!sawGain || !sawNoGain); seed++)
            {
                int attackerAp = RunApDrain(seed, forceMiss: false, out int foeAp, out int foeHp);
                Assert.Less(foeHp, 100);
                Assert.AreEqual(4, foeAp);
                if (attackerAp == 1)
                    sawGain = true;
                else if (attackerAp == 0)
                    sawNoGain = true;
                else
                    Assert.Fail("Unexpected attacker AP " + attackerAp);
            }

            Assert.IsTrue(sawGain, "25% self-gain did not trigger.");
            Assert.IsTrue(sawNoGain, "25% self-gain triggered on every seed.");

            int missedAttackerAp = RunApDrain(3, forceMiss: true, out int missedFoeAp, out int missedFoeHp);
            Assert.AreEqual(100, missedFoeHp);
            Assert.AreEqual(5, missedFoeAp);
            Assert.AreEqual(0, missedAttackerAp);
        }

        [Test]
        public void ThreadLock_BlocksTheNextRegenAndCannotBeReappliedImmediately()
        {
            StatusCatalogSO catalog = Catalog(
                Status("Thread Lock", StatusId.ThreadLock, true));
            CombatResolver resolver = CreateResolver(catalog);
            ChainlingInstance actor = Chainling("actor", 20);
            MoveInstance lockMove = Move("thread_lock", MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0);
            lockMove.statusEffects.Add(new MoveStatusEffect
            {
                statusId = StatusId.ThreadLock,
                duration = 1,
                chance = 1f,
                targetOverride = MoveTargeting.Self,
            });
            actor.moves.Add(lockMove);
            CombatState state = resolver.InitializeBattle(Team(0, actor), Team(1, Chainling("foe", 10)));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(2, actor.currentAP);
            Assert.IsFalse(HasThreadLock(actor));
            Assert.IsNotNull(actor.pendingTurnStartStatuses);
            Assert.AreEqual(1, actor.pendingTurnStartStatuses.Count);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(2, actor.currentAP);
            Assert.IsFalse(HasThreadLock(actor));
            Assert.AreEqual(0, actor.pendingTurnStartStatuses.Count);

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(4, actor.currentAP);
            Assert.IsFalse(HasThreadLock(actor));
            Assert.AreEqual(0, actor.pendingTurnStartStatuses.Count);

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(6, actor.currentAP);
            Assert.AreEqual(1, actor.pendingTurnStartStatuses.Count);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(6, actor.currentAP);
            Assert.IsFalse(HasThreadLock(actor));
        }

        [Test]
        public void ThreadLockInternalGateDoesNotLeakIntoANewBattle()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance actor = Chainling("actor", 20);
            actor.threadLockBlockedUntilTurn = 99;
            CombatState nextBattle = resolver.InitializeBattle(
                Team(0, actor),
                Team(1, Chainling("new_foe", 10)));

            Assert.That(actor.threadLockBlockedUntilTurn, Is.EqualTo(0));
            int before = actor.currentAP;
            resolver.ExecuteTurn(nextBattle, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.That(actor.currentAP, Is.GreaterThan(before));
        }

        private static bool HasThreadLock(ChainlingInstance actor)
        {
            return actor.statuses.Any(status => status.statusId == StatusId.ThreadLock && !status.IsExpired);
        }

        [Test]
        public void GasSpikeAndFud_RestoreOwnedStagesOnExpiry()
        {
            StatusDefinitionSO gas = Status("Gas Spike", StatusId.GasSpike, false);
            gas.stageEffects = new[]
            {
                new StatusStageEffect { stat = CoreStat.MeleeAtk, stages = 2, applyOnce = true },
                new StatusStageEffect { stat = CoreStat.MeleeDef, stages = -2, applyOnce = true },
            };
            StatusDefinitionSO fud = Status("FUD", StatusId.FUD, true);
            fud.stageEffects = new[]
            {
                new StatusStageEffect { stat = CoreStat.MeleeAtk, stages = -1, applyOnce = true },
                new StatusStageEffect { stat = CoreStat.MeleeDef, stages = -1, applyOnce = true },
                new StatusStageEffect { stat = CoreStat.Speed, stages = -1, applyOnce = true },
            };
            CombatResolver resolver = CreateResolver(Catalog(gas, fud));
            ChainlingInstance actor = Chainling("actor", 20);
            actor.SetStage(CoreStat.MeleeAtk, 5);
            MoveInstance gasMove = StatusMove("gas_spike", StatusId.GasSpike);
            actor.moves.Add(gasMove);
            CombatState state = resolver.InitializeBattle(Team(0, actor), Team(1, Chainling("foe", 10)));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(6, actor.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(-2, actor.GetStage(CoreStat.MeleeDef));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(5, actor.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(0, actor.GetStage(CoreStat.MeleeDef));
            Assert.IsFalse(actor.statuses.Any(status => status.statusId == StatusId.GasSpike));

            ChainlingInstance pressured = Chainling("pressured", 20);
            pressured.SetStage(CoreStat.Speed, -6);
            pressured.moves.Add(StatusMove("fud", StatusId.FUD));
            CombatState fudState = resolver.InitializeBattle(Team(0, pressured), Team(1, Chainling("foe2", 10)));
            resolver.ExecuteTurn(fudState, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(-1, pressured.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(-1, pressured.GetStage(CoreStat.MeleeDef));
            Assert.AreEqual(-6, pressured.GetStage(CoreStat.Speed));

            resolver.ExecuteTurn(fudState, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(0, pressured.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(0, pressured.GetStage(CoreStat.MeleeDef));
            Assert.AreEqual(-6, pressured.GetStage(CoreStat.Speed));
        }

        [Test]
        public void ImportedCatalog_MatchesTheCostLadderAndRaritySpread()
        {
            Assert.AreEqual(5, CombatConstants.AP_COST_DAMAGING_MAX);

            string movesPath = Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data/Moves.csv");
            string movesCsv = File.ReadAllText(movesPath);
            List<List<string>> rows = ReadCsv(movesCsv);
            List<string> header = rows[0];
            int idColumn = header.IndexOf("MoveId");
            int rarityColumn = header.IndexOf("Rarity");
            int costColumn = header.IndexOf("APCost");
            int tagsColumn = header.IndexOf("Tags");
            var csvRarity = new Dictionary<string, string>();
            var csvCost = new Dictionary<string, string>();
            var bossOnly = new HashSet<string>(StringComparer.Ordinal);
            var rarityCounts = new Dictionary<string, int>();
            for (int i = 1; i < rows.Count; i++)
            {
                string id = rows[i][idColumn];
                string rarity = rows[i][rarityColumn];
                csvRarity[id] = rarity;
                csvCost[id] = rows[i][costColumn];
                if (rows[i][tagsColumn].Split('|').Any(x => string.Equals(x.Trim(), "BOSS_ONLY", StringComparison.Ordinal)))
                {
                    bossOnly.Add(id);
                    continue;
                }
                rarityCounts.TryGetValue(rarity, out int count);
                rarityCounts[rarity] = count + 1;
            }

            Assert.AreEqual(211, csvRarity.Count);
            Assert.AreEqual(62, rarityCounts["Common"]);
            Assert.AreEqual(43, rarityCounts["Uncommon"]);
            Assert.AreEqual(43, rarityCounts["Rare"]);
            Assert.AreEqual(31, rarityCounts["Epic"]);
            Assert.AreEqual(21, rarityCounts["Legendary"]);
            Assert.AreEqual(10, rarityCounts["Mythic"]);

            Assert.AreEqual("Epic", csvRarity["all_in"]);
            Assert.AreEqual("Legendary", csvRarity["cryo_ray"]);
            Assert.AreEqual("Legendary", csvRarity["hammer_sync"]);
            Assert.AreEqual("Legendary", csvRarity["fork_split"]);
            Assert.AreEqual("Legendary", csvRarity["shard_scatter"]);
            Assert.AreEqual("Uncommon", csvRarity["recoil_strike"]);
            Assert.AreEqual("Uncommon", csvRarity["ember_shower"]);
            Assert.AreEqual("Uncommon", csvRarity["automation_burst"]);
            Assert.AreEqual("Uncommon", csvRarity["volatile_shot"]);
            Assert.AreEqual("Uncommon", csvRarity["deep_dig"]);
            Assert.AreEqual("Uncommon", csvRarity["civet_beam"]);
            Assert.AreEqual("Rare", csvRarity["fee_collector"]);
            Assert.AreEqual("Common", csvRarity["packet_skim"]);
            Assert.AreEqual("Uncommon", csvRarity["cipher_shot"]);
            Assert.AreEqual("Rare", csvRarity["coordinated_shot"]);
            Assert.AreEqual("Common", csvRarity["gas_bump"]);
            Assert.That(bossOnly, Does.Contain("grind_tax"));

            var expectedCosts = new Dictionary<string, string>
            {
                { "all_in", "4" },
                { "fork_split", "4" },
                { "shard_scatter", "4" },
                { "recoil_strike", "4" },
                { "ember_shower", "4" },
                { "automation_burst", "4" },
                { "volatile_shot", "4" },
                { "deep_dig", "4" },
                { "hammer_sync", "4" },
                { "cryo_ray", "4" },
                { "baseline_slam", "2" },
                { "baseline_multi_strike", "2" },
                { "baseline_snipe", "2" },
                { "guardbreaker_combo", "2" },
                { "frost_shards", "2" },
                { "chain_fork", "2" },
                { "stone_barrage", "2" },
                { "interest_strike", "2" },
                { "ap_drain", "2" },
                { "civet_beam", "2" },
                { "packet_skim", "1" },
                { "data_scramble", "1" },
            };
            foreach (KeyValuePair<string, string> expected in expectedCosts)
                Assert.AreEqual(expected.Value, csvCost[expected.Key], expected.Key);

            string pools = File.ReadAllText(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data/SpeciesMovePools.csv"));
            string kitsauto = pools.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .First(line => line.StartsWith("kitsauto,", StringComparison.Ordinal));
            string[] kitColumns = SplitCsv(kitsauto).ToArray();
            string[] learnable = kitColumns[1].Split('|');
            string[] exclusive = kitColumns[2].Split('|');
            CollectionAssert.AreEquivalent(
                new[] { "multi_thread_shot", "packet_skim", "debuff_script" },
                learnable);
            CollectionAssert.AreEquivalent(new[] { "fox_feint", "blade_flurry" }, exclusive);
            StringAssert.DoesNotContain("automated_pressure", kitsauto);
            StringAssert.DoesNotContain("mev_extract", kitsauto);
            StringAssert.DoesNotContain("thread_lock", kitsauto);

            string movesFolder = Path.Combine(Application.dataPath, "Data/Combat/Moves");
            var assetRarity = new Dictionary<string, int>();
            var assetCost = new Dictionary<string, int>();
            foreach (string assetPath in Directory.GetFiles(movesFolder, "Move_*.asset"))
            {
                string text = File.ReadAllText(assetPath);
                string moveId = FirstYaml(text, "moveId");
                if (string.IsNullOrEmpty(moveId) || assetRarity.ContainsKey(moveId))
                    continue;
                assetRarity[moveId] = int.Parse(FirstYaml(text, "rarity"));
                assetCost[moveId] = int.Parse(FirstYaml(text, "apCost"));
            }

            Assert.AreEqual(csvRarity.Count, assetRarity.Count);
            var assetCounts = new Dictionary<int, int>();
            foreach (KeyValuePair<string, string> row in csvRarity)
            {
                int rarity = (int)Enum.Parse(typeof(RarityTier), row.Value);
                Assert.AreEqual(rarity, assetRarity[row.Key], row.Key);
                if (!bossOnly.Contains(row.Key))
                {
                    assetCounts.TryGetValue(rarity, out int count);
                    assetCounts[rarity] = count + 1;
                }
                Assert.AreEqual(int.Parse(csvCost[row.Key]), assetCost[row.Key], row.Key);
            }

            Assert.AreEqual(62, assetCounts[(int)RarityTier.Common]);
            Assert.AreEqual(43, assetCounts[(int)RarityTier.Uncommon]);
            Assert.AreEqual(43, assetCounts[(int)RarityTier.Rare]);
            Assert.AreEqual(31, assetCounts[(int)RarityTier.Epic]);
            Assert.AreEqual(21, assetCounts[(int)RarityTier.Legendary]);
            Assert.AreEqual(10, assetCounts[(int)RarityTier.Mythic]);

            string allIn = File.ReadAllText(Path.Combine(movesFolder, "Move_all_in.asset"));
            StringAssert.Contains("SpendRemainingAp", allIn);
            Assert.AreEqual("2", FirstYaml(allIn, "rechargeTurns"));
            StringAssert.DoesNotContain("recoil", allIn);

            string skimGuid = GuidOf(Path.Combine(movesFolder, "Move_packet_skim.asset"));
            string shotGuid = GuidOf(Path.Combine(movesFolder, "Move_multi_thread_shot.asset"));
            string pressureGuid = GuidOf(Path.Combine(movesFolder, "Move_automated_pressure.asset"));
            string kitsautoAsset = File.ReadAllText(Path.Combine(Application.dataPath, "Data/Combat/ChainlingSpecies/Kitsauto.asset"));
            StringAssert.Contains(skimGuid, kitsautoAsset);
            StringAssert.Contains(shotGuid, kitsautoAsset);
            StringAssert.DoesNotContain(pressureGuid, kitsautoAsset);
        }

        [Test]
        public void UtilityAiScoresSpendAllAndSelfSurchargeAtTheirEffectiveCosts()
        {
            ChainlingInstance actor = Chainling("actor", 20);
            actor.currentAP = 9;
            MoveInstance allIn = Move("all_in", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 4, 98);
            allIn.tags.Add("SpendRemainingAp");
            MoveInstance gasWar = Move("volatile_shot", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 4, 72);
            gasWar.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.NextMoveAPSurcharge,
                magnitude = 1f,
                chance = 1f,
                targetOverride = MoveTargeting.Self,
            });

            MethodInfo method = typeof(UtilityStatusAI).GetMethod(
                "ResolveScoringCost",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            Assert.That((int)method.Invoke(null, new object[] { actor, allIn }), Is.EqualTo(9));
            Assert.That((int)method.Invoke(null, new object[] { actor, gasWar }), Is.EqualTo(5));
        }

        [Test]
        public void BossPayoffAiRequiresSixApAndFallsBackWhileRecharging()
        {
            ChainlingInstance player = Chainling("player", 10);
            ChainlingInstance boss = Chainling("cofi", 20);
            MoveInstance grindTax = Move("grind_tax", MoveCategory.DamagingMelee, MoveTargeting.AllEnemies, 6, 80);
            grindTax.tags.Add(MoveAvailability.BossOnlyTag);
            MoveInstance beanSip = Move("bean_sip", MoveCategory.Support, MoveTargeting.Self, 1, 0);
            boss.moves.Add(grindTax);
            boss.moves.Add(beanSip);
            CombatState state = CreateResolver().InitializeBattle(Team(0, player), Team(1, boss), true);
            var ai = new UtilityStatusAI();

            boss.currentAP = 5;
            Assert.That(ai.GenerateActions(state, 1, new CombatRng(71))[0].moveIndex, Is.EqualTo(1));

            boss.currentAP = 6;
            Assert.That(ai.GenerateActions(state, 1, new CombatRng(71))[0].moveIndex, Is.EqualTo(0));

            boss.statuses.Add(new StatusInstance(
                StatusId.Recharging, StatusClass.Consumed, StatusOrigin.General, 2, grindTax.instanceId)
            {
                rechargingMoveId = grindTax.instanceId,
            });
            Assert.That(ai.GenerateActions(state, 1, new CombatRng(71))[0].moveIndex, Is.EqualTo(1));
        }

        [Test]
        public void BossTellLogsChargingAndReadyOncePerCrossing()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance player = Chainling("player", 10);
            ChainlingInstance boss = Chainling("Cofi Lootwak", 20);
            MoveInstance grindTax = Move("grind_tax", MoveCategory.DamagingMelee, MoveTargeting.AllEnemies, 6, 80);
            grindTax.displayName = "Grind Tax";
            grindTax.tags.Add(MoveAvailability.BossOnlyTag);
            boss.moves.Add(grindTax);
            CombatState state = resolver.InitializeBattle(Team(0, player), Team(1, boss), true);
            boss.currentAP = 2;

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.UseMove(1, 0));

            CombatEvent[] tells = resolver.Log.Events
                .Where(e => e.eventType == CombatEventType.BossTell)
                .ToArray();
            Assert.That(tells.Length, Is.EqualTo(2));
            Assert.That(tells[0].message, Does.Contain("4/6 AP banked"));
            Assert.That(tells[1].message, Does.Contain("banked 6 AP"));
        }

        [Test]
        public void CofiHarnessFiresGrindTaxByTurnFiveAndDependsOnBeanSipGain()
        {
            int withGain = RunCofiPayoffHarness(beanSipGainsAp: true);
            int withoutGain = RunCofiPayoffHarness(beanSipGainsAp: false);

            Assert.That(withGain, Is.InRange(1, 5));
            Assert.That(withoutGain, Is.GreaterThan(withGain));
        }

        private int RunGiftBattle(int giverSpeed, int recipientSpeed)
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance giver = Chainling("giver", giverSpeed);
            ChainlingInstance recipient = Chainling("recipient", recipientSpeed);
            ChainlingInstance foe = Chainling("foe", 10, 120);
            giver.moves.Add(Move("ap_donation", MoveCategory.Support, MoveTargeting.SingleAlly, 2, 0));
            MoveInstance heavy = Move("heavy", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 4, 40);
            heavy.resolveProfile.accuracy.forceHit = true;
            recipient.moves.Add(heavy);
            CombatState state = resolver.InitializeBattle(Team(0, giver, recipient), Team(1, foe));
            int hpBefore = foe.currentHP;
            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction>
                {
                    CombatAction.UseMove(0, 0, 0, 1),
                    CombatAction.UseMove(0, 0),
                },
                new List<CombatAction> { CombatAction.Pass(1) });
            return hpBefore - foe.currentHP;
        }

        private int RunCofiPayoffHarness(bool beanSipGainsAp)
        {
            CombatResolver resolver = CreateResolver(91);
            ChainlingInstance player = Chainling("player", 10, 10000);
            ChainlingInstance boss = Chainling("cofi", 20, 1000);

            MoveInstance beanSip = Move("bean_sip", MoveCategory.Support, MoveTargeting.Self, 1, 0);
            beanSip.rechargeTurns = 2;
            beanSip.healPercentMaxHP = 8;
            if (beanSipGainsAp)
            {
                beanSip.utilityEffects.Add(new UtilityEffectPayload
                {
                    effectType = UtilityEffectType.GainAP,
                    apGain = 1,
                    chance = 1f,
                    targetOverride = MoveTargeting.Self,
                });
            }

            MoveInstance beanBeam = Move("civet_beam", MoveCategory.DamagingRanged, MoveTargeting.AllEnemies, 2, 12);
            beanBeam.resolveProfile.accuracy.forceHit = true;
            MoveInstance grindTax = Move("grind_tax", MoveCategory.DamagingRanged, MoveTargeting.AllEnemies, 6, 30);
            grindTax.hitCount = 3;
            grindTax.hitCountMax = 3;
            grindTax.rechargeTurns = 2;
            grindTax.tags.Add(MoveAvailability.BossOnlyTag);
            grindTax.resolveProfile.accuracy.forceHit = true;
            boss.moves.Add(beanSip);
            boss.moves.Add(beanBeam);
            boss.moves.Add(grindTax);

            CombatState state = resolver.InitializeBattle(Team(0, player), Team(1, boss), true);
            for (int turn = 1; turn <= 10 && state.isActive; turn++)
            {
                resolver.ExecuteTurnWithAI(state, new PassOnlyAi(), new UtilityStatusAI());
                CombatEvent payoff = resolver.Log.Events.LastOrDefault(e =>
                    e.eventType == CombatEventType.MoveUsed && e.moveId == "grind_tax");
                if (string.Equals(payoff.moveId, "grind_tax", StringComparison.Ordinal))
                    return payoff.turn;
            }

            return int.MaxValue;
        }

        private int RunAllIn(
            int startingAp,
            out int apAfter,
            out CombatResolver resolver,
            out CombatState state,
            out ChainlingInstance actor,
            out MoveInstance move)
        {
            resolver = CreateResolver();
            actor = Chainling("actor", 20);
            actor.currentAP = startingAp;
            ChainlingInstance foe = Chainling("foe", 10, 200);
            move = Move("all_in", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 4, 98);
            move.rechargeTurns = 2;
            move.tags.Add("SpendRemainingAp");
            move.resolveProfile.accuracy.forceHit = true;
            actor.moves.Add(move);
            state = resolver.InitializeBattle(Team(0, actor), Team(1, foe));
            int hpBefore = foe.currentHP;
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            apAfter = actor.currentAP;
            return hpBefore - foe.currentHP;
        }

        private void RunFeeCollector(
            int holderStartAp,
            int firstStartAp,
            int secondStartAp,
            int firstSpeed,
            int secondSpeed,
            out int firstAp,
            out int secondAp)
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance holder = Chainling("holder", 20);
            holder.currentAP = holderStartAp;
            holder.moves.Add(Move("fee_collector", MoveCategory.Passive, MoveTargeting.Self, 0, 0));
            ChainlingInstance first = Chainling("first", firstSpeed);
            ChainlingInstance second = Chainling("second", secondSpeed);
            first.currentAP = firstStartAp;
            second.currentAP = secondStartAp;
            CombatState state = resolver.InitializeBattle(
                Team(0, holder, Chainling("partner", 8)),
                Team(1, first, second));
            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.Pass(0), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Pass(1), CombatAction.Pass(1) });
            firstAp = first.currentAP;
            secondAp = second.currentAP;
        }

        private int RunApDrain(int seed, bool forceMiss, out int foeAp, out int foeHp)
        {
            CombatResolver resolver = CreateResolver(seed);
            ChainlingInstance attacker = Chainling("attacker", 20);
            ChainlingInstance foe = Chainling("foe", 10);
            foe.currentAP = 3;
            MoveInstance drain = Move("ap_drain", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 2, 16);
            drain.baseAccuracy = 95;
            drain.contactType = ContactType.Projectile;
            drain.resolveProfile.utilityExecutionTiming = MoveUtilityExecutionTiming.AfterEachConfirmedHit;
            drain.resolveProfile.accuracy.forceHit = !forceMiss;
            drain.resolveProfile.accuracy.forceMiss = forceMiss;
            drain.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.DrainAP,
                apGain = 1,
                chance = 1f,
                targetOverride = MoveTargeting.SingleEnemy,
            });
            drain.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.GainAP,
                apGain = 1,
                chance = 0.25f,
                targetOverride = MoveTargeting.Self,
            });
            attacker.moves.Add(drain);
            CombatState state = resolver.InitializeBattle(Team(0, attacker), Team(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            foeAp = foe.currentAP;
            foeHp = foe.currentHP;
            return attacker.currentAP;
        }

        private static MoveInstance StatusMove(string id, StatusId statusId)
        {
            MoveInstance move = Move(id, MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0);
            move.statusEffects.Add(new MoveStatusEffect
            {
                statusId = statusId,
                duration = 2,
                chance = 1f,
                targetOverride = MoveTargeting.Self,
            });
            return move;
        }

        private CombatResolver CreateResolver()
        {
            return CreateResolver(1234);
        }

        private CombatResolver CreateResolver(int seed)
        {
            return CreateResolver(Catalog(), seed);
        }

        private CombatResolver CreateResolver(StatusCatalogSO catalog)
        {
            return CreateResolver(catalog, 1234);
        }

        private CombatResolver CreateResolver(StatusCatalogSO catalog, int seed)
        {
            TypeChemistryMatrixSO matrix = Create<TypeChemistryMatrixSO>();
            return new CombatResolver(matrix, catalog, seed, true);
        }

        private StatusCatalogSO Catalog(params StatusDefinitionSO[] definitions)
        {
            StatusCatalogSO catalog = Create<StatusCatalogSO>();
            if (definitions != null)
                catalog.definitions.AddRange(definitions);
            catalog.BuildLookup();
            return catalog;
        }

        private StatusDefinitionSO Status(string name, StatusId id, bool debuff)
        {
            StatusDefinitionSO definition = Create<StatusDefinitionSO>();
            definition.statusId = id;
            definition.displayName = name;
            definition.statusClass = StatusClass.Duration;
            definition.origin = StatusOrigin.General;
            definition.defaultDuration = 2;
            definition.isDebuff = debuff;
            return definition;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static ChainlingInstance Chainling(string id, int speed, int maxHp = 100)
        {
            var chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = 0,
                baseMaxHP = maxHp,
                currentHP = maxHp,
                baseMeleeAtk = 80,
                baseMeleeDef = 40,
                baseRangedAtk = 80,
                baseRangedDef = 40,
                baseSpeed = speed,
                maxSlots = 4,
                isAlive = true,
            };
            chainling.SyncDisplayVitals();
            return chainling;
        }

        private static TeamState Team(int teamIndex, params ChainlingInstance[] chainlings)
        {
            var team = new TeamState(teamIndex, "Team " + teamIndex);
            team.chainlings.AddRange(chainlings);
            return team;
        }

        private static MoveInstance Move(string id, MoveCategory category, MoveTargeting targeting, int apCost, int power)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                targeting = targeting,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.None,
                basePower = power,
                apCost = apCost,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
            };
        }

        private sealed class PassOnlyAi : ICombatAI
        {
            public string PersonalityName => "Pass";

            public List<CombatAction> GenerateActions(CombatState state, int teamIndex, CombatRng rng)
            {
                return new List<CombatAction> { CombatAction.Pass(teamIndex) };
            }
        }

        private static List<List<string>> ReadCsv(string csv)
        {
            string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var rows = new List<List<string>>(lines.Length);
            for (int i = 0; i < lines.Length; i++)
                rows.Add(SplitCsv(lines[i]));
            return rows;
        }

        private static List<string> SplitCsv(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (c == ',' && !quoted)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        private static string FirstYaml(string text, string key)
        {
            string needle = key + ": ";
            int index = text.IndexOf(needle, StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;
            int start = index + needle.Length;
            int end = text.IndexOf('\n', start);
            if (end < 0)
                end = text.Length;
            return text.Substring(start, end - start).Trim();
        }

        private static string GuidOf(string assetPath)
        {
            string meta = File.ReadAllText(assetPath + ".meta");
            const string needle = "guid: ";
            int index = meta.IndexOf(needle, StringComparison.Ordinal);
            int start = index + needle.Length;
            int end = meta.IndexOf('\n', start);
            return meta.Substring(start, end - start).Trim();
        }
    }
}
