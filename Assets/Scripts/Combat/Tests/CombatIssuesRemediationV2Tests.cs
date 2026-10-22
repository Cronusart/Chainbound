using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatIssuesRemediationV2Tests
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
        public void SlowedLabel_DoesNotPromiseAnActionSkip()
        {
            string label = ChemistryStatusPresentation.FormatCombatLabel(null, StatusId.Slowed);
            StringAssert.DoesNotContain("25%", label);
            StringAssert.Contains("SPD -2", label);

            string resolver = File.ReadAllText(Path.Combine(
                Application.dataPath, "Scripts/Combat/Runtime/Engine/CombatResolver.cs"));
            StringAssert.DoesNotContain("slowedLoseActionChance", resolver);
            StringAssert.DoesNotContain("SLOWED_LOSE_ACTION_CHANCE", resolver);
        }

        [Test]
        public void KindledPresentation_UsesItsOwnIconAndBurnLabel()
        {
            Assert.AreEqual("status_kindled", ChemistryStatusPresentation.GetIconKey(StatusId.Kindled));
            string label = ChemistryStatusPresentation.FormatCombatLabel(null, StatusId.Kindled);
            StringAssert.Contains("6% BURN", label);
        }

        [Test]
        public void Dusted_OwnsClampedEvasionAndDoesNotStackOnRefresh()
        {
            StatusProcessor processor = CreateProcessor(DustedDefinition());
            ChainlingInstance target = Chainling("dusted", 100);
            target.SetCombatStage(CombatStageStat.Evasion, -4);
            target.SetStage(CoreStat.Speed, -5);

            Assert.IsTrue(processor.ApplyStatus(target, StatusId.Dusted, 2, "chem", 1, true));
            Assert.AreEqual(-6, target.GetCombatStage(CombatStageStat.Evasion));
            Assert.AreEqual(-6, target.GetStage(CoreStat.Speed));

            Assert.IsTrue(processor.ApplyStatus(target, StatusId.Dusted, 2, "chem", 1, true));
            Assert.AreEqual(-6, target.GetCombatStage(CombatStageStat.Evasion));
            Assert.AreEqual(-6, target.GetStage(CoreStat.Speed));

            StatusInstance dusted = target.statuses.Find(status => status.statusId == StatusId.Dusted);
            Assert.AreEqual(1, dusted.ownedCombatStageDeltas.Count);
            Assert.AreEqual(-2, dusted.ownedCombatStageDeltas[0].delta);
            Assert.AreEqual(-1, dusted.ownedStageDeltas[0].delta);

            MoveInstance strike = Move("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1, 40);
            strike.baseAccuracy = 20;
            float open = DamageCalculator.ComputeHitChance(Chainling("attacker", 100), Chainling("open", 100), strike, out _, out float openEvasion);
            float dusty = DamageCalculator.ComputeHitChance(Chainling("attacker", 100), target, strike, out _, out float dustyEvasion);
            Assert.Less(dustyEvasion, openEvasion);
            Assert.Greater(dusty, open, $"open={open} dusty={dusty} acc={strike.baseAccuracy} eva={target.stageEvasion}");

            dusted.turnsRemaining = 1;
            processor.ProcessEndOfTurn(target, 1);
            Assert.AreEqual(-4, target.GetCombatStage(CombatStageStat.Evasion));
            Assert.AreEqual(-5, target.GetStage(CoreStat.Speed));
        }

        [Test]
        public void Tripwire_AppliesSlowedForOneTurnAndRefreshDoesNotStack()
        {
            CombatResolver resolver = CreateResolver(SlowedCatalog());
            ChainlingInstance holder = Chainling("holder", 80, 30);
            MoveInstance trap = Move("trap", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 1);
            trap.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = "6.6-a",
                triggerCondition = ModifierTriggerCondition.OnEscape,
            });
            holder.moves.Add(trap);

            ChainlingInstance lead = Chainling("lead", 80, 10);
            ChainlingInstance partner = Chainling("partner", 80, 8);
            ChainlingInstance bench = Chainling("bench", 80, 6);
            CombatState state = resolver.InitializeBattle(Team(0, holder), Team(1, lead, partner, bench));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Swap(1, 2));

            Assert.AreEqual(-2, lead.GetStage(CoreStat.Speed));
            Assert.AreEqual(1, lead.statuses.Find(status => status.statusId == StatusId.Slowed).turnsRemaining);
        }

        [Test]
        public void Tripwire_RefreshDoesNotStackSpeed()
        {
            CombatResolver resolver = CreateResolver(SlowedCatalog());
            ChainlingInstance holder = Chainling("holder", 80, 30);
            MoveInstance trap = Move("trap", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 1);
            trap.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = "6.6-a",
                triggerCondition = ModifierTriggerCondition.OnEscape,
            });
            holder.moves.Add(trap);
            ChainlingInstance lead = Chainling("lead", 80, 10);
            new StatusProcessor(SlowedCatalog(), resolver.Log).ApplySlowedForTurns(lead, 1, "setup", 1);
            CombatState state = resolver.InitializeBattle(
                Team(0, holder),
                Team(1, lead, Chainling("partner", 80), Chainling("bench", 80)));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Swap(1, 2));
            Assert.AreEqual(-2, lead.GetStage(CoreStat.Speed));
            Assert.AreEqual(1, lead.statuses.Count(status => status.statusId == StatusId.Slowed && !status.IsExpired));
        }

        [Test]
        public void Tripwire_FrozenAssetsAndOtherSectionsDoNotSlow()
        {
            CombatResolver resolver = CreateResolver(SlowedCatalog());
            ChainlingInstance holder = Chainling("holder", 80, 30);
            MoveInstance trap = Move("trap", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 1);
            trap.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = "6.6-b",
                triggerCondition = ModifierTriggerCondition.OnEscape,
            });
            holder.moves.Add(trap);
            ChainlingInstance lead = Chainling("lead", 80, 10);
            lead.statuses.Add(new StatusInstance(StatusId.FrozenAssets, StatusClass.Duration, StatusOrigin.General, 2, "test"));
            CombatState state = resolver.InitializeBattle(
                Team(0, holder),
                Team(1, lead, Chainling("partner", 80), Chainling("bench", 80)));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Swap(1, 2));
            Assert.AreEqual(0, lead.GetStage(CoreStat.Speed));
            Assert.IsFalse(lead.statuses.Exists(status => status.statusId == StatusId.Slowed));
            Assert.AreEqual(0, state.team1.activeIndices[0]);
        }

        [Test]
        public void Uplink_PaysEachChangedAllyOnceAndSkipsExtendAndCap()
        {
            CombatResolver resolver = CreateResolver(BuffCatalog());
            ChainlingInstance user = Chainling("user", 100, 20);
            ChainlingInstance ally = Chainling("ally", 100, 18);
            user.moves.Add(Passive("uplink"));
            MoveInstance sync = Move("network_sync", MoveCategory.StatusUtility, MoveTargeting.AllAllies, 2, 0);
            sync.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.RaiseLowestStage,
                affectAllAllies = true,
                stackKey = "network_sync",
            });
            user.moves.Add(sync);

            CombatState state = resolver.InitializeBattle(Team(0, user, ally), Team(1, Chainling("foe", 100)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 1), CombatAction.Pass(1));

            Assert.AreEqual(1, user.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(1, ally.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(0, user.currentAP);
            Assert.AreEqual(3, ally.currentAP);
            StringAssert.Contains("Uplink gives ally 1 AP.", Messages(resolver));

            ally.currentAP = 10;
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 1), CombatAction.Pass(1));
            Assert.AreEqual(10, ally.currentAP);
            Assert.GreaterOrEqual(Messages(resolver).Split(new[] { "Uplink's point for ally is lost." }, StringSplitOptions.None).Length - 1, 1);
        }

        [Test]
        public void ExtendProtocol_ExtendsTheShortestBuffAndDoesNotPayUplink()
        {
            StatusCatalogSO catalog = BuffCatalog();
            CombatResolver resolver = CreateResolver(catalog);
            ChainlingInstance user = Chainling("user", 100, 20);
            ChainlingInstance ally = Chainling("ally", 100, 18);
            user.moves.Add(Passive("uplink"));
            MoveInstance extend = Move("extend_protocol", MoveCategory.StatusUtility, MoveTargeting.SingleAlly, 1, 0);
            extend.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.ExtendBuff,
                targetOverride = MoveTargeting.SingleAlly,
                stackKey = "extend_protocol",
            });
            user.moves.Add(extend);
            StatusProcessor processor = new StatusProcessor(catalog, resolver.Log);
            processor.ApplyStatus(ally, StatusId.Anchored, 1, "setup", 0);
            processor.ApplyStatus(ally, StatusId.Pumped, 3, "setup", 0);

            CombatState state = resolver.InitializeBattle(Team(0, user, ally), Team(1, Chainling("foe", 100)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 1, 0, 1), CombatAction.Pass(1));

            Assert.AreEqual(1, ally.statuses.Find(status => status.statusId == StatusId.Anchored).turnsRemaining);
            Assert.AreEqual(2, ally.statuses.Find(status => status.statusId == StatusId.Pumped).turnsRemaining);
            Assert.AreEqual(2, ally.currentAP);
            StringAssert.DoesNotContain("Uplink gives", Messages(resolver));
        }

        [Test]
        public void ExtendProtocol_LogsWhenNothingQualifies()
        {
            CombatResolver resolver = CreateResolver(BuffCatalog());
            ChainlingInstance user = Chainling("user", 100);
            ChainlingInstance ally = Chainling("ally", 100);
            MoveInstance extend = Move("extend_protocol", MoveCategory.StatusUtility, MoveTargeting.SingleAlly, 1, 0);
            extend.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.ExtendBuff,
                targetOverride = MoveTargeting.SingleAlly,
            });
            user.moves.Add(extend);
            CombatState state = resolver.InitializeBattle(Team(0, user, ally), Team(1, Chainling("foe", 100)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 0, 1), CombatAction.Pass(1));
            StringAssert.Contains("has no buff to extend", Messages(resolver));
        }

        [Test]
        public void RaiseLowestStage_RespectsTiesAndCap()
        {
            CombatResolver resolver = CreateResolver(BuffCatalog());
            ChainlingInstance user = Chainling("user", 100);
            user.SetStage(CoreStat.MeleeAtk, 1);
            MoveInstance sync = Move("network_sync", MoveCategory.StatusUtility, MoveTargeting.AllAllies, 2, 0);
            sync.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.RaiseLowestStage,
                affectAllAllies = true,
            });
            user.moves.Add(sync);
            CombatState state = resolver.InitializeBattle(Team(0, user), Team(1, Chainling("foe", 100)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(1, user.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(1, user.GetStage(CoreStat.MeleeDef));

            user.SetStage(CoreStat.MeleeAtk, 6);
            user.SetStage(CoreStat.MeleeDef, 6);
            user.SetStage(CoreStat.RangedAtk, 6);
            user.SetStage(CoreStat.RangedDef, 6);
            user.SetStage(CoreStat.Speed, 6);
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(6, user.GetStage(CoreStat.Speed));
            StringAssert.Contains("already at +6", Messages(resolver));
        }

        [Test]
        public void Kindling_MarksOnceThenBurnsBeforeRugged()
        {
            StatusCatalogSO catalog = BurnCatalog();
            CombatResolver resolver = CreateResolver(catalog);
            ChainlingInstance attacker = Chainling("attacker", 132, 30);
            attacker.moves.Add(Passive("kindling"));
            MoveInstance shots = Move("cinder", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 2, 20);
            shots.hitCount = 2;
            shots.hitCountMax = 2;
            attacker.moves.Add(shots);
            ChainlingInstance foe = Chainling("foe", 132, 10);
            CombatState state = resolver.InitializeBattle(Team(0, attacker), Team(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 1), CombatAction.Pass(1));

            Assert.AreEqual(1, resolver.Log.Events.Count(evt =>
                evt.eventType == CombatEventType.StatusApplied && evt.statusId == StatusId.Kindled));
            string text = Messages(resolver);
            int kindled = text.IndexOf("Kindled burns foe for 7", StringComparison.Ordinal);
            Assert.GreaterOrEqual(kindled, 0);
            Assert.IsFalse(foe.statuses.Exists(status => status.statusId == StatusId.Kindled && !status.IsExpired));

            ChainlingInstance burned = Chainling("burned", 100);
            CombatEventLog tickLog = new CombatEventLog(1);
            StatusProcessor processor = new StatusProcessor(catalog, tickLog);
            processor.ApplyStatus(burned, StatusId.Kindled, 1, "kindling", 1);
            processor.ApplyStatus(burned, StatusId.Rugged, 2, "chem", 1, true);
            processor.ApplyStatus(burned, StatusId.Drip, 3, "chem", 1, true);
            processor.ProcessEndOfTurn(burned, 1);
            string ticks = string.Join("\n", tickLog.Events.Select(evt => evt.message));
            int expectedBurn = Math.Max(1, (int)(burned.EffectiveMaxHP * 0.06f));
            int burnAt = ticks.IndexOf($"Kindled burns burned for {expectedBurn}", StringComparison.Ordinal);
            if (burnAt < 0)
                Assert.Fail($"expected {expectedBurn} in:\n{ticks}");
            int ruggedAt = ticks.IndexOf("Rugged scorches burned for", StringComparison.Ordinal);
            int dripAt = ticks.IndexOf("healed", StringComparison.Ordinal);
            Assert.GreaterOrEqual(burnAt, 0);
            Assert.Greater(ruggedAt, burnAt);
            Assert.Greater(dripAt, ruggedAt);
        }

        [Test]
        public void Kindling_MissAndKoDoNotMark()
        {
            CombatResolver resolver = CreateResolver(BurnCatalog());
            ChainlingInstance attacker = Chainling("attacker", 100, 30);
            attacker.moves.Add(Passive("kindling"));
            MoveInstance miss = Move("miss", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 40);
            miss.resolveProfile.accuracy.forceMiss = true;
            attacker.moves.Add(miss);
            ChainlingInstance foe = Chainling("foe", 100, 10);
            CombatState state = resolver.InitializeBattle(Team(0, attacker), Team(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 1), CombatAction.Pass(1));
            Assert.IsFalse(foe.statuses.Exists(status => status.statusId == StatusId.Kindled));

            MoveInstance finisher = Move("finisher", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 400);
            finisher.resolveProfile.accuracy.forceHit = true;
            attacker.moves.Add(finisher);
            foe.currentHP = 10;
            foe.isAlive = true;
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 2), CombatAction.Pass(1));
            Assert.IsFalse(foe.isAlive);
            Assert.IsFalse(foe.statuses.Exists(status => status.statusId == StatusId.Kindled));
        }

        [Test]
        public void Leaked_TaxesOnlyActorsOnThatSide()
        {
            CombatResolver resolver = CreateResolver(BurnCatalog());
            ChainlingInstance actor = Chainling("actor", 100, 20);
            ChainlingInstance partner = Chainling("partner", 100, 18);
            ChainlingInstance bench = Chainling("bench", 100, 8);
            ChainlingInstance foe = Chainling("foe", 100, 10);
            actor.statuses.Add(new StatusInstance(StatusId.Leaked, StatusClass.Duration, StatusOrigin.ChemistryOnly, 3, "chem"));
            bench.statuses.Add(new StatusInstance(StatusId.Leaked, StatusClass.Duration, StatusOrigin.ChemistryOnly, 3, "chem"));
            CombatState state = resolver.InitializeBattle(Team(0, actor, partner, bench), Team(1, foe));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.AreEqual(90, actor.currentHP);
            Assert.AreEqual(90, partner.currentHP);
            Assert.AreEqual(100, bench.currentHP);
            Assert.AreEqual(100, foe.currentHP);
            StringAssert.Contains("Leaked taxes actor for 10", Messages(resolver));
            Assert.AreEqual(2, actor.statuses.Find(status => status.statusId == StatusId.Leaked).turnsRemaining);
        }

        [Test]
        public void AuthoredRows_MatchCsv()
        {
            string moves = File.ReadAllText(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data/Moves.csv"));
            StringAssert.Contains("ChangeStage:Accuracy:2:100%:Self:6:false:accuracy_focus", moves);
            StringAssert.Contains("ChangeStage:Evasion:2:100%:Self:6:false:evasion_maneuver", moves);
            StringAssert.Contains("DrainAP:1:100%:SingleEnemy:ap_drain|GainAP:1:25%:Self:ap_drain_steal", moves);
            StringAssert.Contains("StealAP:1:100%:SingleEnemy:transaction_tax", moves);
            StringAssert.Contains("ExtendBuff:extend_protocol", moves);
            StringAssert.Contains("RaiseLowestStage:network_sync", moves);
            StringAssert.Contains("uplink,Uplink,Passive,Node,Uncommon", moves);
            StringAssert.Contains("kindling,Kindling,Passive,Burned,Uncommon", moves);
            Assert.AreEqual("44", CsvField(moves, "crystal_bash", "Power"));
            Assert.AreEqual("true", CsvField(moves, "crystal_bash", "DamageOverrideAttackStat"));
            Assert.AreEqual("MeleeDef", CsvField(moves, "crystal_bash", "DamageAttackStat"));
            Assert.AreEqual("MitigationWindow:25%:2:-1:blockade", CsvField(moves, "blockade", "UtilityEffects"));
            Assert.AreEqual("", CsvField(moves, "precision_strike", "UtilityEffects"));
            Assert.AreEqual("", CsvField(moves, "shard_scatter", "UtilityEffects"));

            string statuses = File.ReadAllText(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data/Statuses.csv"));
            StringAssert.Contains("Dusted,Dusted,ChemistryOnly,Duration,2,0,Eva -3 and Spd -3,Speed:-3|Evasion:-3", statuses);
            StringAssert.Contains("Kindled,Kindled,General,Duration,1,0,6% MaxHP at end of turn", statuses);
            StringAssert.DoesNotContain("DustStorm,", statuses);
            StringAssert.DoesNotContain("NodeRelay,", statuses);
            StringAssert.DoesNotContain("Reinforced,", statuses);

            string pools = File.ReadAllText(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data/SpeciesMovePools.csv"));
            string validape = pools.Split('\n').First(line => line.StartsWith("validape,", StringComparison.Ordinal));
            string cinislug = pools.Split('\n').First(line => line.StartsWith("cinislug,", StringComparison.Ordinal));
            StringAssert.Contains("uplink", validape);
            StringAssert.DoesNotContain("ap_router", validape);
            StringAssert.Contains("hammer_sync", validape);
            StringAssert.DoesNotContain("coordinated_shot", validape);
            StringAssert.Contains("kindling", cinislug);
            StringAssert.Contains("cinder_burst", cinislug);
            StringAssert.Contains("ember_wall", cinislug);
            StringAssert.DoesNotContain("scorch_shot", cinislug);
            StringAssert.DoesNotContain("deflation_mark", cinislug);
            Assert.AreEqual(5, validape.Split(',')[1].Split('|').Length + validape.Split(',')[2].Split('|').Length);
            Assert.AreEqual(5, cinislug.Split(',')[1].Split('|').Length);

            string extendAsset = File.ReadAllText(Path.Combine(
                Application.dataPath, "Data/Combat/Moves/Move_extend_protocol.asset"));
            StringAssert.Contains("effectType: 24", extendAsset);
            StringAssert.Contains("rechargeTurns: 2", extendAsset);
            string syncAsset = File.ReadAllText(Path.Combine(
                Application.dataPath, "Data/Combat/Moves/Move_network_sync.asset"));
            StringAssert.Contains("effectType: 25", syncAsset);
        }

        private static string Messages(CombatResolver resolver)
        {
            return string.Join("\n", resolver.Log.Events.Select(evt => evt.message));
        }

        private static string CsvField(string csv, string moveId, string column)
        {
            string[] lines = csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string[] header = lines[0].Split(',');
            int columnIndex = Array.IndexOf(header, column);
            string line = lines.First(row => row.StartsWith(moveId + ",", StringComparison.Ordinal));
            return line.Split(',')[columnIndex];
        }

        private CombatResolver CreateResolver(StatusCatalogSO catalog)
        {
            TypeChemistryMatrixSO matrix = Create<TypeChemistryMatrixSO>();
            return new CombatResolver(matrix, catalog, 1234, true);
        }

        private StatusProcessor CreateProcessor(params StatusDefinitionSO[] definitions)
        {
            StatusCatalogSO catalog = Create<StatusCatalogSO>();
            catalog.definitions.AddRange(definitions);
            catalog.BuildLookup();
            return new StatusProcessor(catalog, new CombatEventLog(1));
        }

        private StatusCatalogSO SlowedCatalog()
        {
            StatusDefinitionSO slowed = Create<StatusDefinitionSO>();
            slowed.statusId = StatusId.Slowed;
            slowed.displayName = "Slowed";
            slowed.statusClass = StatusClass.Duration;
            slowed.origin = StatusOrigin.ChemistryOnly;
            slowed.defaultDuration = 1;
            slowed.isDebuff = true;
            return Catalog(slowed);
        }

        private StatusCatalogSO BuffCatalog()
        {
            return Catalog(
                Status(StatusId.Anchored, true, false),
                Status(StatusId.Pumped, true, false),
                Status(StatusId.ThreadLock, false, true));
        }

        private StatusCatalogSO BurnCatalog()
        {
            StatusDefinitionSO kindled = Status(StatusId.Kindled, false, true);
            kindled.defaultDuration = 1;
            kindled.origin = StatusOrigin.General;
            kindled.hasEndOfTurnDamage = true;
            kindled.endOfTurnDamagePercent = 0.06f;
            StatusDefinitionSO rugged = Status(StatusId.Rugged, false, true);
            rugged.origin = StatusOrigin.ChemistryOnly;
            rugged.hasEndOfTurnDamage = true;
            rugged.endOfTurnDamagePercent = 0.08f;
            StatusDefinitionSO drip = Status(StatusId.Drip, true, false);
            drip.origin = StatusOrigin.ChemistryOnly;
            return Catalog(kindled, rugged, drip, Status(StatusId.Leaked, false, true));
        }

        private StatusDefinitionSO DustedDefinition()
        {
            StatusDefinitionSO dusted = Status(StatusId.Dusted, false, true);
            dusted.origin = StatusOrigin.ChemistryOnly;
            dusted.defaultDuration = 2;
            dusted.stageEffects = new[]
            {
                new StatusStageEffect { stat = CoreStat.Speed, stages = -3, applyOnce = true },
                new StatusStageEffect
                {
                    usesCombatStage = true,
                    combatStage = CombatStageStat.Evasion,
                    stages = -3,
                    applyOnce = true,
                },
            };
            return dusted;
        }

        private StatusDefinitionSO Status(StatusId id, bool isBuff, bool isDebuff)
        {
            StatusDefinitionSO definition = Create<StatusDefinitionSO>();
            definition.statusId = id;
            definition.displayName = id.ToString();
            definition.statusClass = StatusClass.Duration;
            definition.origin = StatusOrigin.General;
            definition.defaultDuration = 2;
            definition.isBuff = isBuff;
            definition.isDebuff = isDebuff;
            return definition;
        }

        private StatusCatalogSO Catalog(params StatusDefinitionSO[] definitions)
        {
            StatusCatalogSO catalog = Create<StatusCatalogSO>();
            catalog.definitions.AddRange(definitions);
            catalog.BuildLookup();
            return catalog;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static ChainlingInstance Chainling(string id, int maxHp, int speed = 20)
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
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
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
                contactType = ContactType.None,
                basePower = power,
                apCost = apCost,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
            };
        }

        private static MoveInstance Passive(string id)
        {
            MoveInstance move = Move(id, MoveCategory.Passive, MoveTargeting.Self, 0, 0);
            move.triggerCondition = PassiveTriggerCondition.OnAction;
            return move;
        }
    }
}
