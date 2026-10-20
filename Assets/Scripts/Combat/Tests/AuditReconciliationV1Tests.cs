using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class AuditReconciliationV1Tests
    {
        private readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object asset in _created)
                if (asset != null)
                    UnityEngine.Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void StripBuffs_RemovesUpToNGeneralBuffsAndIgnoresDebuffs()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.Yield, buff: true, duration: 3),
                Status(StatusId.Relay, buff: true, duration: 1),
                Status(StatusId.ThreadLock, debuff: true, duration: 2));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(101));
            ChainlingInstance target = Chainling("target");
            processor.ApplyStatus(target, StatusId.Yield, 3, "setup", 1);
            processor.ApplyStatus(target, StatusId.Relay, 1, "setup", 1);
            processor.ApplyStatus(target, StatusId.ThreadLock, 2, "setup", 1);

            ExecuteUtility(processor, Chainling("source"), target,
                Effect(UtilityEffectType.StripBuffs, magnitude: 2, target: MoveTargeting.SingleEnemy));

            Assert.That(target.statuses.Select(x => x.statusId), Is.EquivalentTo(new[] { StatusId.ThreadLock }));
        }

        [Test]
        public void StripAllStatuses_ClearsBuffsAndDebuffs()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.Yield, buff: true),
                Status(StatusId.ThreadLock, debuff: true),
                Status(StatusId.Anchored, origin: StatusOrigin.ChemistryOnly, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(102));
            ChainlingInstance target = Chainling("target");
            processor.ApplyStatus(target, StatusId.Yield, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.ThreadLock, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.Anchored, 1, "setup", 1, true);

            ExecuteUtility(processor, Chainling("source"), target,
                Effect(UtilityEffectType.StripAllStatuses, target: MoveTargeting.SingleEnemy));

            Assert.That(target.statuses.Select(x => x.statusId), Is.EquivalentTo(new[] { StatusId.Anchored }));
        }

        [Test]
        public void StripBuffsThenStage_GrantsAtkEqualToBuffsRemoved()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.Yield, buff: true),
                Status(StatusId.Relay, buff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(103));
            ChainlingInstance source = Chainling("source");
            ChainlingInstance target = Chainling("target");
            processor.ApplyStatus(target, StatusId.Yield, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.Relay, 2, "setup", 1);
            UtilityEffectPayload effect = Effect(
                UtilityEffectType.StripBuffsThenStage,
                magnitude: 2,
                target: MoveTargeting.SingleEnemy);
            effect.stageStat = CombatStageStat.MeleeAtk;
            effect.stageAmount = 1;

            ExecuteUtility(processor, source, target, effect);

            Assert.That(source.stageMeleeAtk, Is.EqualTo(2));
            Assert.That(target.statuses, Is.Empty);
        }

        [Test]
        public void IsolationTool_AppliesAnchoredOnCrackettAndMarrowhead()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.Anchored, origin: StatusOrigin.ChemistryOnly, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(104));
            UtilityEffectPayload effect = Effect(
                UtilityEffectType.ApplyChemistryStatus,
                target: MoveTargeting.SingleEnemy);
            effect.statusId = StatusId.Anchored;
            effect.statusDuration = 1;

            foreach (string id in new[] { "crackett-foe", "marrowhead-foe" })
            {
                ChainlingInstance foe = Chainling(id);
                ExecuteUtility(processor, Chainling("source-" + id), foe, effect);
                Assert.That(foe.statuses.Single().statusId, Is.EqualTo(StatusId.Anchored));
                Assert.That(foe.statuses.Single().turnsRemaining, Is.EqualTo(1));
            }
        }

        [Test]
        public void CrystalExtraction_HealsSelfFivePercentPerFoeDebuffThenCleansesFoe()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.ThreadLock, debuff: true),
                Status(StatusId.Staggered, debuff: true),
                Status(StatusId.CipherLocked, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(105));
            ChainlingInstance source = Chainling("source");
            source.currentHP = 40;
            ChainlingInstance foe = Chainling("foe");
            foe.currentHP = 50;
            processor.ApplyStatus(foe, StatusId.ThreadLock, 2, "setup", 1);
            processor.ApplyStatus(foe, StatusId.Staggered, 2, "setup", 1);
            processor.ApplyStatus(foe, StatusId.CipherLocked, 2, "setup", 1);

            MoveInstance move = Move("crystal_extraction",
                Effect(UtilityEffectType.HealSelfPerTargetDebuff, magnitude: 0.05f),
                Cleanse(99, MoveTargeting.SingleEnemy));
            Executor(processor).Execute(source, foe, move, 1, true);

            Assert.That(source.currentHP, Is.EqualTo(55));
            Assert.That(foe.currentHP, Is.EqualTo(50));
            Assert.That(foe.statuses, Is.Empty);
        }

        [Test]
        public void CrystalExtraction_DoesNotHealTheFoe()
        {
            MoveDefinitionSO definition = LoadMove("crystal_extraction");
            Assert.That(definition.healPercentMaxHP, Is.Zero);
            Assert.That(definition.utilityEffects.Select(x => x.effectType),
                Is.EqualTo(new[] { UtilityEffectType.HealSelfPerTargetDebuff, UtilityEffectType.Cleanse }));
        }

        [Test]
        public void Walls_MitigateForTheirTurnWindowWithUnlimitedHits()
        {
            foreach ((string id, float amount, int turns) in new[]
            {
                ("ash_guard", 0.20f, 2),
                ("compound_guard", 0.20f, 2),
                ("ice_wall", 0.25f, 1),
                ("blockade", 0.25f, 2),
            })
            {
                MoveDefinitionSO definition = LoadMove(id);
                MoveInstance move = CombatFactory.CreateMove(definition);
                ChainlingInstance ally = Chainling(id + "-ally");
                Executor(null).Execute(Chainling(id + "-source"), ally, move, 1, true);
                Assert.That(ally.GetTotalMitigation(), Is.EqualTo(amount).Within(0.0001f), id);
                ally.ConsumeMitigationHitBudgets();
                ally.ConsumeMitigationHitBudgets();
                Assert.That(ally.GetTotalMitigation(), Is.EqualTo(amount).Within(0.0001f), id + " after two hits");
                for (int i = 0; i < turns; i++)
                    ally.TickMitigationSources();
                Assert.That(ally.GetTotalMitigation(), Is.Zero, id + " after duration");
            }

            Assert.That(LoadMove("compound_guard").stageChanges.Any(x => x.stat == CoreStat.MeleeDef && x.stages == 1), Is.True);
            Assert.That(LoadMove("ice_wall").utilityEffects.Any(x => x.effectType == UtilityEffectType.GainApOnDamageIfSpeedHigher), Is.True);
        }

        [Test]
        public void Jammed_AppliesOwnedAccuracyMinusOneAndReverts()
        {
            StatusDefinitionSO jammed = Status(StatusId.Jammed, origin: StatusOrigin.ChemistryOnly, debuff: true, duration: 2);
            jammed.stageEffects = new[]
            {
                new StatusStageEffect
                {
                    usesCombatStage = true,
                    combatStage = CombatStageStat.Accuracy,
                    stages = -1,
                    applyOnce = true,
                },
            };
            StatusProcessor processor = new StatusProcessor(Catalog(jammed), new CombatEventLog(106));
            ChainlingInstance target = Chainling("target");

            processor.ApplyStatus(target, StatusId.Jammed, 2, "chem", 1, true);
            Assert.That(target.GetCombatStage(CombatStageStat.Accuracy), Is.EqualTo(-1));
            processor.ApplyStatus(target, StatusId.Jammed, 2, "chem", 1, true);
            Assert.That(target.GetCombatStage(CombatStageStat.Accuracy), Is.EqualTo(-1));
            processor.ProcessEndOfTurn(target, 1);
            processor.ProcessEndOfTurn(target, 2);
            Assert.That(target.GetCombatStage(CombatStageStat.Accuracy), Is.Zero);
        }

        [Test]
        public void EndOfTurnPassives_RunBeforeStatusDurationTick()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Scripts/Combat/Runtime/Engine/CombatResolver.cs"));
            int method = source.IndexOf("private void ProcessEndOfTurnForTeam", StringComparison.Ordinal);
            int nextMethod = source.IndexOf("private void TickBenchedRecharge", method, StringComparison.Ordinal);
            string body = source.Substring(method, nextMethod - method);
            Assert.That(body.IndexOf("PassiveTriggerPoint.EndOfTurn", StringComparison.Ordinal),
                Is.LessThan(body.IndexOf("_statusProcessor.ProcessEndOfTurn", StringComparison.Ordinal)));
        }

        [Test]
        public void ChargedRelay_PaysAfterAnAllySpread()
        {
            CombatEventLog log = new CombatEventLog(107);
            ChainlingInstance source = Chainling("relay-user");
            TeamState team = new TeamState(0, "allies");
            team.chainlings.Add(source);
            TurnActionLedger ledger = new TurnActionLedger();
            ledger.BeginTurn(1);
            ledger.RecordHit(0, "ally", "foe-a");
            ledger.RecordHit(0, "ally", "foe-b");
            MoveInstance move = Move("charged_relay", new UtilityEffectPayload
            {
                effectType = UtilityEffectType.GainApIfDistinctTargetsHitAtLeast,
                threshold = 2,
                apGain = 2,
                targetOverride = MoveTargeting.Self,
                chance = 1f,
            });
            var context = new UtilityEffectExecutionContext { ledger = ledger, sourceTeam = team };

            new UtilityEffectExecutor(log, new CombatApService(log)).Execute(
                source, source, move, 1, true, context: context);

            Assert.That(ledger.CountDistinctTargetsHitThisTurn(source.instanceId), Is.Zero);
            Assert.That(source.currentAP, Is.EqualTo(2));
        }

        [Test]
        public void PartialRiderAssets_MatchApprovedPayloads()
        {
            MoveDefinitionSO streamlined = LoadMove("streamlined_flow");
            Assert.That(streamlined.stageChanges, Is.Empty);
            Assert.That(streamlined.utilityEffects.Count(x => x.effectType == UtilityEffectType.ChangeStage), Is.EqualTo(1));
            Assert.That(LoadMove("compression_burst").resolveProfile.moveEffectsOncePerTarget, Is.True);
            AssertStatus(LoadMove("rally_cry"), StatusId.Yield, 1, MoveTargeting.Self);
            AssertStatus(LoadMove("refinery"), StatusId.Yield, 2, MoveTargeting.Self);
        }

        [Test]
        public void BugFix_CleansesOneDebuffAndAppliesStaggered()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.ThreadLock, debuff: true),
                Status(StatusId.CipherLocked, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(109));
            ChainlingInstance source = Chainling("bug-fix-user");
            processor.ApplyStatus(source, StatusId.ThreadLock, 2, "setup", 1);
            processor.ApplyStatus(source, StatusId.CipherLocked, 2, "setup", 1);

            MoveDefinitionSO definition = LoadMove("bug_fix");
            Executor(processor).Execute(source, source, CombatFactory.CreateMove(definition), 1, true);

            Assert.That(source.statuses.Count, Is.EqualTo(1));
            Assert.That(definition.healPercentMaxHP, Is.EqualTo(10));
            AssertStatus(definition, StatusId.Staggered, 1, MoveTargeting.Self);
        }

        [Test]
        public void CrystalBash_DealsDefScaledDamageAndDoublesBelow30()
        {
            MoveDefinitionSO definition = LoadMove("crystal_bash");
            MoveInstance move = CombatFactory.CreateMove(definition);
            move.resolveProfile.damage.useCustomRandomRollRange = true;
            move.resolveProfile.damage.randomRollMin = 1f;
            move.resolveProfile.damage.randomRollMax = 1f;
            ChainlingInstance defender = Chainling("crystal-bash-target");
            ChainlingInstance highDef = Chainling("high-def");
            ChainlingInstance lowDef = Chainling("low-def");
            highDef.baseMeleeAtk = lowDef.baseMeleeAtk = 5;
            highDef.baseMeleeDef = 100;
            lowDef.baseMeleeDef = 20;

            int highDefDamage = DamageCalculator.CalculateDamage(
                highDef, defender, move, new ChemistryResult(), false, new CombatRng(110));
            int lowDefDamage = DamageCalculator.CalculateDamage(
                lowDef, defender, move, new ChemistryResult(), false, new CombatRng(110));

            Assert.That(highDefDamage, Is.GreaterThan(lowDefDamage));
            Assert.That(highDefDamage, Is.GreaterThan(0));
            Assert.That(move.resolveProfile.damage.attackStatOverride, Is.EqualTo(CoreStat.MeleeDef));
            Assert.That(move.resolveProfile.conditionalDamage.multiplier, Is.EqualTo(2f));
            MoveResolveCondition condition = move.resolveProfile.conditionalDamage.conditions.Single();
            Assert.That(condition.subject, Is.EqualTo(MoveResolveConditionSubject.Target));
            Assert.That(condition.conditionType, Is.EqualTo(MoveResolveConditionType.HPBelowPercent));
            Assert.That(condition.threshold, Is.EqualTo(30f));
        }

        [Test]
        public void SystemReset_StripsAllGeneralStatusesAndRechargesOneTurn()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.Yield, buff: true),
                Status(StatusId.ThreadLock, debuff: true),
                Status(StatusId.Anchored, origin: StatusOrigin.ChemistryOnly, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(111));
            ChainlingInstance target = Chainling("system-reset-target");
            processor.ApplyStatus(target, StatusId.Yield, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.ThreadLock, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.Anchored, 1, "setup", 1, true);
            MoveDefinitionSO definition = LoadMove("system_reset");

            Executor(processor).Execute(
                Chainling("system-reset-user"), target, CombatFactory.CreateMove(definition), 1, true);

            Assert.That(target.statuses.Select(x => x.statusId), Is.EqualTo(new[] { StatusId.Anchored }));
            Assert.That(definition.rechargeTurns, Is.EqualTo(2),
                "RechargeTurns 2 blocks the next action turn under the existing recharge contract.");
        }

        [Test]
        public void HypeBurst_StripsUpToTwoBuffsAndGainsAtkPerRemoved()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.Yield, buff: true),
                Status(StatusId.Relay, buff: true),
                Status(StatusId.ThreadLock, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(112));
            ChainlingInstance source = Chainling("hype-burst-user");
            ChainlingInstance target = Chainling("hype-burst-target");
            processor.ApplyStatus(target, StatusId.Yield, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.Relay, 2, "setup", 1);
            processor.ApplyStatus(target, StatusId.ThreadLock, 2, "setup", 1);

            Executor(processor).Execute(
                source, target, CombatFactory.CreateMove(LoadMove("hype_burst")), 1, true);

            Assert.That(source.stageMeleeAtk, Is.EqualTo(2));
            Assert.That(target.statuses.Select(x => x.statusId), Is.EqualTo(new[] { StatusId.ThreadLock }));
        }

        [Test]
        public void PurgeProtocol_CleansesAllAllyDebuffsAndGrantsDefPlusOne()
        {
            StatusCatalogSO catalog = Catalog(
                Status(StatusId.ThreadLock, debuff: true),
                Status(StatusId.CipherLocked, debuff: true));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(113));
            ChainlingInstance ally = Chainling("purge-protocol-ally");
            processor.ApplyStatus(ally, StatusId.ThreadLock, 2, "setup", 1);
            processor.ApplyStatus(ally, StatusId.CipherLocked, 2, "setup", 1);
            MoveDefinitionSO definition = LoadMove("purge_protocol");

            Executor(processor).Execute(
                Chainling("purge-protocol-user"), ally, CombatFactory.CreateMove(definition), 1, true);

            Assert.That(ally.statuses, Is.Empty);
            MoveStageChangeDef stage = definition.stageChanges.Single();
            Assert.That(stage.stat, Is.EqualTo(CoreStat.MeleeDef));
            Assert.That(stage.stages, Is.EqualTo(1));
            Assert.That(stage.targetsSelf, Is.False);
        }

        [Test]
        public void StreamlinedFlow_DropsDefOnceAbove70()
        {
            MoveDefinitionSO definition = LoadMove("streamlined_flow");
            UtilityEffectPayload drop = definition.utilityEffects.Single(
                x => x.effectType == UtilityEffectType.ChangeStage);
            Assert.That(drop.stageStat, Is.EqualTo(CombatStageStat.MeleeDef));
            Assert.That(drop.stageAmount, Is.EqualTo(-1));
            Assert.That(drop.stageCap, Is.EqualTo(-6));
            Assert.That(drop.targetOverride, Is.EqualTo(MoveTargeting.Self));
            Assert.That(definition.resolveProfile.resolveConditions.Single().conditionType,
                Is.EqualTo(MoveResolveConditionType.HPAbovePercent));
            Assert.That(definition.resolveProfile.resolveConditions.Single().threshold, Is.EqualTo(70f));
        }

        [Test]
        public void CompressionBurst_DropsDefOnTheFirstHitOnly()
        {
            MoveDefinitionSO definition = LoadMove("compression_burst");
            Assert.That(definition.hitCountMin, Is.EqualTo(2));
            Assert.That(definition.hitCountMax, Is.EqualTo(2));
            Assert.That(definition.resolveProfile.moveEffectsOncePerTarget, Is.True);
            MoveStageChangeDef drop = definition.stageChanges.Single();
            Assert.That(drop.stat, Is.EqualTo(CoreStat.MeleeDef));
            Assert.That(drop.stages, Is.EqualTo(-1));
            Assert.That(drop.targetsSelf, Is.False);
        }

        [Test]
        public void RallyCry_GrantsOneApToTheUserOnly()
        {
            MoveDefinitionSO definition = LoadMove("rally_cry");
            Assert.That(definition.targeting, Is.EqualTo(MoveTargeting.AllAllies));
            AssertStatus(definition, StatusId.Yield, 1, MoveTargeting.Self);
        }

        [Test]
        public void Refinery_GrantsApForTwoEndSteps()
        {
            MoveDefinitionSO definition = LoadMove("refinery");
            AssertStatus(definition, StatusId.Yield, 2, MoveTargeting.Self);
        }

        [Test]
        public void TextBatch_DescriptionsMatchPayload()
        {
            string moves = File.ReadAllText(DataPath("Moves.csv"));
            string statuses = File.ReadAllText(DataPath("Statuses.csv"));
            StringAssert.Contains("Apply MEV Extracted; drain 1 AP per turn for 2 turns.", moves);
            StringAssert.Contains("Heal an ally for 20% MaxHP.", moves);
            StringAssert.Contains("Below 50% HP, heal 20% now.", moves);
            StringAssert.Contains("Lower target Atk by 1.", moves);
            StringAssert.Contains("Passive - end turn 60% +1 AP; 40% -1 AP.", moves);
            StringAssert.Contains("Token. No contact damage.", statuses);
            StringAssert.Contains("Drain 1 AP per turn", statuses);
            string statusIds = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Combat/Runtime/Data/StatusId.cs"));
            StringAssert.Contains("Atk/Def/Spd -1", statusIds);
        }

        [Test]
        public void DeprecatedStatuses_HaveNoProducer()
        {
            string statuses = File.ReadAllText(DataPath("Statuses.csv"));
            StringAssert.Contains("Rekt [Deprecated]", statuses);
            StringAssert.Contains("Offline [Deprecated]", statuses);
            StringAssert.Contains("Relay [Deprecated]", statuses);
            Assert.That(LoadMove("status_resistance").description, Is.EqualTo("Retired. No effect."));

            foreach (string status in new[] { "Rekt", "Offline", "Relay" })
            {
                Assert.That(StatusPayloadTokens("Moves.csv"), Does.Not.Contain(status));
                Assert.That(File.ReadAllText(DataPath("Modifiers.csv")), Does.Not.Contain(status + ":"));
            }

            StatusCatalogSO catalog = AssetDatabase.LoadAssetAtPath<StatusCatalogSO>(
                "Assets/Data/Combat/StatusDef/Catalog/StatusCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            foreach (StatusId removed in new[]
            {
                StatusId.DustStorm,
                StatusId.ForkedChain,
                StatusId.CompressedData,
                StatusId.MemeSurge,
                StatusId.NodeRelay,
                StatusId.BridgedOut,
                StatusId.Reinforced,
            })
                Assert.That(catalog.Get(removed), Is.Null, removed + " should stay out of the live catalog");
        }

        [Test]
        public void CanonicalStatusCatalog_IsSceneReferencedAndContainsKindled()
        {
            const string canonicalPath = "Assets/Data/Combat/StatusDef/Catalog/StatusCatalog.asset";
            StatusCatalogSO catalog = AssetDatabase.LoadAssetAtPath<StatusCatalogSO>(canonicalPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Get(StatusId.Kindled), Is.Not.Null);

            string guid = AssetDatabase.AssetPathToGUID(canonicalPath);
            string scene = File.ReadAllText(Path.Combine(Application.dataPath, "Scenes/Combat2_TestHarness.unity"));
            StringAssert.Contains("statusCatalog: {fileID: 11400000, guid: " + guid, scene);
        }

        [Test]
        public void ReconciledMoveAssets_MatchApprovedNumbersAndPayloads()
        {
            Assert.That(LoadMove("crystal_bash").power, Is.EqualTo(44));
            Assert.That(LoadMove("data_scramble").apCost, Is.EqualTo(1));
            Assert.That(LoadMove("baseline_shot").power, Is.EqualTo(26));
            Assert.That(LoadMove("yield_spike").tags, Is.EqualTo(new[] { "DMG" }));
            Assert.That(LoadMove("system_reset").rechargeTurns, Is.EqualTo(2));
            Assert.That(LoadMove("system_reset").utilityEffects.Single().effectType, Is.EqualTo(UtilityEffectType.StripAllStatuses));
            Assert.That(LoadMove("hype_burst").utilityEffects.Single().effectType, Is.EqualTo(UtilityEffectType.StripBuffsThenStage));
            Assert.That(LoadMove("purge_protocol").utilityEffects.Single().effectType, Is.EqualTo(UtilityEffectType.Cleanse));
        }

        [Test]
        public void LivePools_NoEmptyOrPartialPayloads()
        {
            List<List<string>> poolRows = ParseCsv(File.ReadAllText(DataPath("SpeciesMovePools.csv")));
            Dictionary<string, int> poolColumns = HeaderColumns(poolRows[0]);
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (List<string> row in poolRows.Skip(1).Where(x => x.Count == poolRows[0].Count && !x[0].StartsWith("#")))
            {
                foreach (string column in new[] { "LearnableMoves", "ExclusiveMoves", "InitialMoves", "OffTypeMoves" })
                    AddPipeValues(live, row[poolColumns[column]]);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:WildMovePoolSO", new[] { "Assets/Data/Combat/Encounters" }))
            {
                WildMovePoolSO pool = AssetDatabase.LoadAssetAtPath<WildMovePoolSO>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (WeightedMoveEntry entry in pool.entries)
                    if (!string.IsNullOrWhiteSpace(entry.moveId)) live.Add(entry.moveId);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EncounterAreaSO", new[] { "Assets/Data/Combat/Encounters" }))
            {
                EncounterAreaSO area = AssetDatabase.LoadAssetAtPath<EncounterAreaSO>(AssetDatabase.GUIDToAssetPath(guid));
                AddInlineMoveIds(live, area.entries);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:EncounterZoneSO", new[] { "Assets/Data/Combat/Encounters" }))
            {
                EncounterZoneSO zone = AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(AssetDatabase.GUIDToAssetPath(guid));
                AddInlineMoveIds(live, zone.entries);
            }

            List<List<string>> moveRows = ParseCsv(File.ReadAllText(DataPath("Moves.csv")));
            Dictionary<string, int> moveColumns = HeaderColumns(moveRows[0]);
            Dictionary<string, List<string>> moves = moveRows.Skip(1)
                .Where(x => x.Count == moveRows[0].Count && !string.IsNullOrWhiteSpace(x[moveColumns["MoveId"]]))
                .ToDictionary(x => x[moveColumns["MoveId"]], StringComparer.Ordinal);

            List<List<string>> reviewRows = ParseCsv(File.ReadAllText(DataPath("LivePoolPayloadReview.csv")));
            Dictionary<string, int> reviewColumns = HeaderColumns(reviewRows[0]);
            Dictionary<string, List<string>> reviews = reviewRows.Skip(1)
                .Where(x => x.Count == reviewRows[0].Count)
                .ToDictionary(x => x[reviewColumns["MoveId"]], StringComparer.Ordinal);
            Assert.That(reviews.Keys, Is.EquivalentTo(live), "Review rows must exactly match the live move set.");

            var emptyPassiveAllowlist = new HashSet<string>(StringComparer.Ordinal)
            {
                "immutable_ledger", "kindling", "uplink",
            };
            string resolverSource = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/Combat/Runtime/Engine/CombatResolver.cs"));
            foreach (string moveId in live)
            {
                Assert.That(moves.ContainsKey(moveId), Is.True, moveId + " is live but missing from Moves.csv");
                List<string> row = moves[moveId];
                string category = row[moveColumns["Category"]];
                int power = int.TryParse(row[moveColumns["Power"]], out int parsedPower) ? parsedPower : 0;
                string statuses = row[moveColumns["StatusEffects"]];
                string stages = row[moveColumns["StageChanges"]];
                string utilities = row[moveColumns["UtilityEffects"]];
                int heal = int.TryParse(row[moveColumns["HealPercentMaxHP"]], out int parsedHeal) ? parsedHeal : 0;
                bool hasPayload = !string.IsNullOrWhiteSpace(statuses) || !string.IsNullOrWhiteSpace(stages) ||
                                  !string.IsNullOrWhiteSpace(utilities) || heal > 0;

                if (category.StartsWith("Damaging", StringComparison.Ordinal))
                    Assert.That(power, Is.GreaterThan(0), moveId + " is a live damaging move with zero power");
                else if (category == "StatusUtility" || category == "Support")
                    Assert.That(hasPayload, Is.True, moveId + " is a live utility/support move with no payload");
                else if (category == "Passive" && !hasPayload)
                {
                    Assert.That(emptyPassiveAllowlist.Contains(moveId), Is.True, moveId + " needs a reviewed passive handler");
                    StringAssert.Contains(moveId, resolverSource, moveId + " has no resolver handler evidence");
                }

                foreach (string effect in utilities.Split('|'))
                {
                    if (!effect.StartsWith("MitigationWindow:", StringComparison.Ordinal)) continue;
                    string[] parts = effect.Split(':');
                    Assert.That(parts.Length, Is.GreaterThanOrEqualTo(4), moveId);
                    Assert.That(parts[3], Is.Not.EqualTo("0"), moveId + " has a zero-hit mitigation window");
                }

                List<string> review = reviews[moveId];
                Assert.That(review[reviewColumns["TextOnly"]], Is.EqualTo("false").IgnoreCase, moveId);
                string payload = string.Join("|", new[] { statuses, stages, utilities });
                if (power > 0) payload += "|Damage";
                if (heal > 0) payload += "|Heal";
                if (int.TryParse(row[moveColumns["RechargeTurns"]], out int recharge) && recharge > 0) payload += "|Recharge";
                if (emptyPassiveAllowlist.Contains(moveId)) payload += "|PassiveHandler";
                if (utilities.Contains("StripBuffsThenStage")) payload += "|StripBuffs|MeleeAtk";
                foreach (string token in review[reviewColumns["ExpectedTokens"]].Split('|'))
                    StringAssert.Contains(token, payload, moveId + " is missing reviewed token " + token);
            }

            foreach (string ungranted in new[] { "crystal_bash", "accuracy_disrupt", "power_drain", "ore_restoration" })
                Assert.That(live, Does.Not.Contain(ungranted));
        }

        [Test]
        public void WildEncounterTables_ExcludeKitsautoAndHumpacked()
        {
            SpeciesDatabaseSO database = AssetDatabase.LoadAssetAtPath<SpeciesDatabaseSO>(
                "Assets/Data/Combat/ChainlingSpecies/Database/SpeciesDatabase.asset");
            Assert.That(database, Is.Not.Null);
            var entries = new List<ZoneEncounterEntry>();
            foreach (string guid in AssetDatabase.FindAssets("t:EncounterAreaSO", new[] { "Assets/Data/Combat/Encounters" }))
                entries.AddRange(AssetDatabase.LoadAssetAtPath<EncounterAreaSO>(AssetDatabase.GUIDToAssetPath(guid)).entries);
            foreach (string guid in AssetDatabase.FindAssets("t:EncounterZoneSO", new[] { "Assets/Data/Combat/Encounters" }))
                entries.AddRange(AssetDatabase.LoadAssetAtPath<EncounterZoneSO>(AssetDatabase.GUIDToAssetPath(guid)).entries);

            foreach (ZoneEncounterEntry entry in entries)
            {
                Assert.That(database.GetById(entry.speciesId), Is.Not.Null, entry.speciesId + " is absent from SpeciesDatabase");
                Assert.That(entry.speciesId, Is.Not.EqualTo("kitsauto").And.Not.EqualTo("humpacked"),
                    "Kitsauto is starter-only; Humpacked waits for the river zone (decision 2026-10-18).");
            }
        }

        [Test]
        public void BalloomPool_MatchesAcceptedFive()
        {
            List<List<string>> rows = ParseCsv(File.ReadAllText(DataPath("SpeciesMovePools.csv")));
            Dictionary<string, int> columns = HeaderColumns(rows[0]);
            List<string> balloom = rows.Single(x => x.Count == rows[0].Count && x[columns["SpeciesId"]] == "balloom");
            Assert.That(balloom[columns["LearnableMoves"]].Split('|'), Is.EqualTo(new[]
            {
                "fuel_surge", "volatile_shot", "gas_bump", "slipstream", "baseline_slam",
            }));
            Assert.That(balloom[columns["FlagshipMove"]], Is.EqualTo("fuel_surge"));
            Assert.That(balloom[columns["InitialMoves"]], Is.EqualTo("gas_bump"));
            Assert.That(balloom[columns["OffTypeMoves"]], Is.EqualTo("recoil_strike"));
            Assert.That(string.Join("|", balloom), Does.Not.Contain("all_in"));
        }

        private static Dictionary<string, int> HeaderColumns(List<string> header)
        {
            return header.Select((name, index) => new { name, index })
                .ToDictionary(x => x.name, x => x.index, StringComparer.Ordinal);
        }

        private static void AddPipeValues(HashSet<string> target, string value)
        {
            foreach (string item in (value ?? string.Empty).Split('|'))
                if (!string.IsNullOrWhiteSpace(item)) target.Add(item.Trim());
        }

        private static void AddInlineMoveIds(HashSet<string> target, List<ZoneEncounterEntry> entries)
        {
            if (entries == null) return;
            foreach (ZoneEncounterEntry entry in entries)
            {
                if (entry == null || entry.inlineMovePool == null) continue;
                foreach (WeightedMoveEntry move in entry.inlineMovePool)
                    if (!string.IsNullOrWhiteSpace(move.moveId)) target.Add(move.moveId);
            }
        }

        private UtilityEffectExecutor Executor(StatusProcessor processor)
        {
            CombatEventLog log = new CombatEventLog(108);
            return new UtilityEffectExecutor(log, new CombatApService(log), processor);
        }

        private void ExecuteUtility(StatusProcessor processor, ChainlingInstance source, ChainlingInstance target, UtilityEffectPayload effect)
        {
            Executor(processor).Execute(source, target, Move("test", effect), 1, true);
        }

        private static UtilityEffectPayload Effect(
            UtilityEffectType type,
            float magnitude = 0f,
            MoveTargeting target = MoveTargeting.SingleEnemy)
        {
            return new UtilityEffectPayload
            {
                effectType = type,
                magnitude = magnitude,
                chance = 1f,
                targetOverride = target,
            };
        }

        private static UtilityEffectPayload Cleanse(int count, MoveTargeting target)
        {
            UtilityEffectPayload effect = Effect(UtilityEffectType.Cleanse, count, target);
            effect.apGain = count;
            return effect;
        }

        private static MoveInstance Move(string id, params UtilityEffectPayload[] effects)
        {
            MoveInstance move = new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                targeting = MoveTargeting.SingleEnemy,
            };
            move.utilityEffects.AddRange(effects);
            return move;
        }

        private StatusDefinitionSO Status(
            StatusId id,
            StatusOrigin origin = StatusOrigin.General,
            bool debuff = false,
            bool buff = false,
            int duration = 2)
        {
            StatusDefinitionSO def = Create<StatusDefinitionSO>();
            def.statusId = id;
            def.displayName = id.ToString();
            def.origin = origin;
            def.statusClass = StatusClass.Duration;
            def.defaultDuration = duration;
            def.isDebuff = debuff;
            def.isBuff = buff;
            return def;
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
            _created.Add(asset);
            return asset;
        }

        private static ChainlingInstance Chainling(string id)
        {
            ChainlingInstance value = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                baseMaxHP = 100,
                currentHP = 100,
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
                baseSpeed = 20,
                isAlive = true,
                maxSlots = 4,
            };
            value.SyncDisplayVitals();
            return value;
        }

        private static MoveDefinitionSO LoadMove(string id)
        {
            MoveDefinitionSO move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>($"Assets/Data/Combat/Moves/Move_{id}.asset");
            Assert.That(move, Is.Not.Null, id);
            return move;
        }

        private static void AssertStatus(MoveDefinitionSO move, StatusId statusId, int duration, MoveTargeting target)
        {
            MoveStatusEffectDef status = move.statusEffects.Single(x => x.statusId == statusId);
            Assert.That(status.duration, Is.EqualTo(duration));
            Assert.That(status.overrideTargeting ? status.targetOverride : move.targeting, Is.EqualTo(target));
        }

        private static string DataPath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../Docs/Specs/Combat/Data", fileName));
        }

        private static string StatusPayloadTokens(string fileName)
        {
            List<List<string>> rows = ParseCsv(File.ReadAllText(DataPath(fileName)));
            int status = rows[0].IndexOf("StatusEffects");
            int utility = rows[0].IndexOf("UtilityEffects");
            return string.Join("|", rows.Skip(1).Where(row => row.Count == rows[0].Count)
                .Select(row => row[status] + "|" + row[utility]));
        }

        private static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var field = new System.Text.StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else quoted = !quoted;
                }
                else if (c == ',' && !quoted)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if ((c == '\n' || c == '\r') && !quoted)
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Any(x => x.Length > 0)) rows.Add(row);
                    row = new List<string>();
                }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row);
            }
            return rows;
        }
    }
}
