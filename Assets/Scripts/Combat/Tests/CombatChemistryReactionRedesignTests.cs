using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatChemistryReactionRedesignTests
    {
        private readonly List<UnityEngine.Object> _createdAssets = new List<UnityEngine.Object>();

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
        public void FirewallMitigatesProjectileButNotMeleeAndKeepsChemistryApplication()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Firewall, StatusClass.Duration, 2));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Burned, ChainlingType.Gas, StatusId.Firewall);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Burned);
            ChainlingInstance target = CreateChainling("target", ChainlingType.Gas);
            ChemistryResolver chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(7), true, config);

            chemistry.Resolve(ChainlingType.Burned, attacker, target, 1, new CombatRng(7), true, out _);

            Assert.IsTrue(HasStatus(attacker, StatusId.Firewall));
            Assert.AreEqual(1, attacker.mitigationSources.Count);

            ChainlingInstance projectileSource = CreateChainling("projectile_source", ChainlingType.Burned);
            MoveInstance projectile = CreateMove("projectile", MoveCategory.DamagingRanged, ContactType.Projectile, 40);
            MoveInstance melee = CreateMove("melee", MoveCategory.DamagingMelee, ContactType.Contact, 40);

            int projectileDamage = DamageCalculator.CalculateDamageDetailed(
                projectileSource, attacker, projectile, NeutralChemistry(), false, new CombatRng(99), null, config, out _);
            int meleeDamage = DamageCalculator.CalculateDamageDetailed(
                projectileSource, attacker, melee, NeutralChemistry(), false, new CombatRng(99), null, config, out _);
            ChainlingInstance baseline = CreateChainling("baseline", ChainlingType.Gas);
            int baselineProjectileDamage = DamageCalculator.CalculateDamageDetailed(
                projectileSource, baseline, projectile, NeutralChemistry(), false, new CombatRng(99), null, config, out _);

            Assert.Less(projectileDamage, baselineProjectileDamage);
            Assert.AreEqual(baselineProjectileDamage, meleeDamage);
            Assert.AreEqual(1, attacker.ConsumeMitigationHitBudgets(ContactType.Projectile));
            Assert.AreEqual(0f, attacker.GetTotalMitigation(ContactType.Projectile));
        }

        [Test]
        public void SlowedAppliesSpeedPenaltyAndDoesNotSkipActions()
        {
            CombatResolverConfigSO config = CreateConfig();
            config.slowedLoseActionChance = 1f;
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Slowed, StatusClass.Duration, 2));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Frozen, ChainlingType.Staked, StatusId.Slowed);
            ChainlingInstance first = CreateChainling("first", ChainlingType.Staked);
            ChainlingInstance second = CreateChainling("second", ChainlingType.Staked);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Frozen);
            ChemistryResolver chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(11), true, config);

            chemistry.Resolve(ChainlingType.Frozen, attacker, first, 1, new CombatRng(11), true, out _);
            chemistry.Resolve(ChainlingType.Frozen, attacker, second, 1, new CombatRng(11), true, out _);

            Assert.AreEqual(-2, first.stageSpeed);
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(11), resolverConfig: config);
            processor.ProcessStartOfTurn(first, 2, new CombatRng(123));
            processor.ProcessStartOfTurn(second, 2, new CombatRng(123));
            Assert.IsFalse(first.lostActionThisTurn);
            Assert.IsFalse(second.lostActionThisTurn);
            processor.ProcessEndOfTurn(first, 2);
            processor.ProcessEndOfTurn(first, 3);
            Assert.AreEqual(0, first.stageSpeed);

            ChainlingInstance capped = CreateChainling("capped", ChainlingType.Staked);
            capped.stageSpeed = -5;
            chemistry.Resolve(ChainlingType.Frozen, attacker, capped, 1, new CombatRng(11), true, out _);
            Assert.AreEqual(-6, capped.stageSpeed);
            processor.ProcessEndOfTurn(capped, 2);
            processor.ProcessEndOfTurn(capped, 3);
            Assert.AreEqual(-5, capped.stageSpeed);
        }

        [Test]
        public void ShatteredUsesCurrentHpChipMinimumOneAndDoesNotExecute()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Shattered, StatusClass.Instant, 0));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Mined, ChainlingType.Block, StatusId.Shattered);
            ChainlingInstance target = CreateChainling("target", ChainlingType.Block);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Mined);
            target.currentHP = 5;
            target.stageMeleeDef = 0;
            ChemistryResolver chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(13), true, config);

            chemistry.Resolve(ChainlingType.Mined, attacker, target, 1, new CombatRng(13), true, out _);

            Assert.AreEqual(4, target.currentHP);
            Assert.AreEqual(-1, target.stageMeleeDef);
            Assert.IsTrue(target.isAlive);
            Assert.IsFalse(HasStatus(target, StatusId.Shattered));

            ChainlingInstance oneHpTarget = CreateChainling("one_hp", ChainlingType.Block);
            oneHpTarget.currentHP = 1;
            chemistry.Resolve(ChainlingType.Mined, attacker, oneHpTarget, 1, new CombatRng(13), true, out _);
            Assert.AreEqual(1, oneHpTarget.currentHP);
            Assert.IsTrue(oneHpTarget.isAlive);
        }

        [Test]
        public void RuggedEscalatesAndCapsWhileDripAndFomoTickOnce()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(
                Status(StatusId.Rugged, StatusClass.Duration, 3, eotDamage: 0.0625f),
                Status(StatusId.Drip, StatusClass.Duration, 3),
                Status(StatusId.FOMO, StatusClass.Duration, 3, eotDamage: 0.0625f));
            CombatEventLog log = new CombatEventLog(17);
            StatusProcessor processor = new StatusProcessor(catalog, log, resolverConfig: config);
            ChainlingInstance rugged = CreateChainling("rugged", ChainlingType.Burned);
            rugged.currentHP = 100;
            rugged.statuses.Add(new StatusInstance(StatusId.Rugged, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 3, "test") { counterValue = 14 });

            processor.ProcessEndOfTurn(rugged, 1);
            Assert.AreEqual(13, rugged.currentHP);
            Assert.AreEqual(15, rugged.statuses[0].counterValue);
            ChainlingInstance cappedRugged = CreateChainling("capped_rugged", ChainlingType.Burned, 1000);
            cappedRugged.statuses.Add(new StatusInstance(StatusId.Rugged, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 3, "test") { counterValue = 15 });
            processor.ProcessEndOfTurn(cappedRugged, 2);
            Assert.AreEqual(15, cappedRugged.statuses[0].counterValue);

            ChainlingInstance drip = CreateChainling("drip", ChainlingType.Gas);
            drip.currentHP = 50;
            drip.statuses.Add(new StatusInstance(StatusId.Drip, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 3, "test"));
            processor.ProcessEndOfTurn(drip, 1);
            Assert.AreEqual(56, drip.currentHP);
            Assert.AreEqual(2, drip.statuses[0].turnsRemaining);

            ChainlingInstance fomo = CreateChainling("fomo", ChainlingType.Mined);
            fomo.currentHP = 100;
            processor.ApplyStatus(fomo, StatusId.FOMO, 3, "test", 1, bypassChemistryCheck: true);
            Assert.AreEqual(1, fomo.stageMeleeAtk);
            processor.ProcessEndOfTurn(fomo, 1);
            Assert.AreEqual(94, fomo.currentHP);
            processor.ProcessEndOfTurn(fomo, 2);
            processor.ProcessEndOfTurn(fomo, 3);
            Assert.AreEqual(0, fomo.stageMeleeAtk);
        }

        [Test]
        public void CascadeTriggersAtThreeAndForensicPayloadTracksCounterReset()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.CascadeFailure, StatusClass.Counter, 0, threshold: 3));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Glitched, ChainlingType.Block, StatusId.CascadeFailure);
            CombatEventLog log = new CombatEventLog(19);
            ChemistryResolver chemistry = new ChemistryResolver(matrix, catalog, log, true, config);
            ChainlingInstance target = CreateChainling("target", ChainlingType.Block);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Glitched);

            chemistry.Resolve(ChainlingType.Glitched, attacker, target, 1, new CombatRng(19), true, out _);
            chemistry.Resolve(ChainlingType.Glitched, attacker, target, 1, new CombatRng(19), true, out _);
            chemistry.Resolve(ChainlingType.Glitched, attacker, target, 1, new CombatRng(19), true, out ChemistryResolvedPayload payload);

            Assert.AreEqual(75, target.currentHP);
            Assert.IsTrue(target.isAlive);
            Assert.IsFalse(HasStatus(target, StatusId.CascadeFailure));
            Assert.AreEqual(2, payload.reactions[0].counterBefore);
            Assert.AreEqual(0, payload.reactions[0].counterAfter);
        }

        [Test]
        public void DualTypeChemistryCollapsesToFlatRelationAndPrimaryReaction()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(
                Status(StatusId.Dusted, StatusClass.Duration, 2),
                Status(StatusId.Jammed, StatusClass.Duration, 2));
            TypeChemistryMatrixSO matrix = CreateAsset<TypeChemistryMatrixSO>();
            matrix.reactions.Add(new ChemistryReaction
            {
                attackerType = ChainlingType.Shard,
                defenderType = ChainlingType.Node,
                output = new ChemistryOutput { statusId = StatusId.Dusted, recipient = ChemistryRecipient.Defender },
            });
            matrix.reactions.Add(new ChemistryReaction
            {
                attackerType = ChainlingType.Shard,
                defenderType = ChainlingType.Encrypted,
                output = new ChemistryOutput { statusId = StatusId.Jammed, recipient = ChemistryRecipient.Defender },
            });

            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Shard);
            ChainlingInstance defender = CreateChainling("defender", ChainlingType.Node);
            defender.secondaryType = ChainlingType.Encrypted;
            var chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(20), true, config);

            ChemistryResult result = chemistry.Resolve(
                ChainlingType.Shard, attacker, defender, 1, new CombatRng(20), true, out ChemistryResolvedPayload payload);

            Assert.AreEqual(ChemistryRelation.Advantage, result.relation);
            Assert.AreEqual(1.2f, result.DamageMultiplier, 0.0001f);
            Assert.IsTrue(HasStatus(defender, StatusId.Dusted));
            Assert.IsFalse(HasStatus(defender, StatusId.Jammed));
            Assert.AreEqual(StatusId.Dusted, payload.reactions.Single().statusId);
        }

        [Test]
        public void RuggedReapplicationRefreshesWithoutIncreasingSeverity()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Rugged, StatusClass.Duration, 3, eotDamage: 0.0625f));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Burned, ChainlingType.Gas, StatusId.Rugged);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Burned);
            ChainlingInstance defender = CreateChainling("defender", ChainlingType.Gas);
            var chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(21), true, config);

            chemistry.Resolve(ChainlingType.Burned, attacker, defender, 1, new CombatRng(21), true, out _);
            defender.statuses.Single(s => s.statusId == StatusId.Rugged).turnsRemaining = 1;
            chemistry.Resolve(ChainlingType.Burned, attacker, defender, 1, new CombatRng(21), true, out ChemistryResolvedPayload payload);

            StatusInstance rugged = defender.statuses.Single(s => s.statusId == StatusId.Rugged);
            Assert.AreEqual(1, rugged.counterValue);
            Assert.AreEqual(3, rugged.turnsRemaining);
            Assert.AreEqual(1, payload.reactions.Single().counterBefore);
            Assert.AreEqual(1, payload.reactions.Single().counterAfter);
        }

        [Test]
        public void DefueledRemovesOneHarmfulChemistryStatusFromSelf()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Defueled, StatusClass.Instant, 0));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Node, ChainlingType.Frozen, StatusId.Defueled);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Node);
            attacker.statuses.Add(new StatusInstance(StatusId.Rugged, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 2, "test") { counterValue = 4 });
            attacker.statuses.Add(new StatusInstance(StatusId.Pumped, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 2, "test"));
            ChainlingInstance defender = CreateChainling("defender", ChainlingType.Frozen);
            var chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(22), true, config);

            chemistry.Resolve(ChainlingType.Node, attacker, defender, 1, new CombatRng(22), true, out _);

            Assert.IsFalse(HasStatus(attacker, StatusId.Rugged));
            Assert.IsTrue(HasStatus(attacker, StatusId.Pumped));
        }

        [Test]
        public void SuppressedRepeatReactionStillReturnsDamageRelation()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Dusted, StatusClass.Duration, 3));
            TypeChemistryMatrixSO matrix = CreateMatrix(
                ChainlingType.Shard, ChainlingType.Node, StatusId.Dusted);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Shard);
            ChainlingInstance defender = CreateChainling("defender", ChainlingType.Node);
            var chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(23), true, config);

            chemistry.Resolve(ChainlingType.Shard, attacker, defender, 1, new CombatRng(23), true, out _);
            defender.statuses.Single(s => s.statusId == StatusId.Dusted).turnsRemaining = 1;
            ChemistryResult second = chemistry.Resolve(
                ChainlingType.Shard, attacker, defender, 1, new CombatRng(23), true,
                out ChemistryResolvedPayload payload, allowDefenderReaction: false);

            Assert.AreEqual(ChemistryRelation.Advantage, second.relation);
            Assert.AreEqual(1.2f, second.DamageMultiplier, 0.0001f);
            Assert.AreEqual(1, defender.statuses.Single(s => s.statusId == StatusId.Dusted).turnsRemaining);
            Assert.AreEqual("OncePerActionLimit", payload.reactions.Single().blockedReason);
        }

        [Test]
        public void SplinterSpreadsAtSixtyFivePercentAndIsConsumed()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Splinter, StatusClass.Consumed, 1));
            CombatResolver resolver = new CombatResolver(
                CreateAsset<TypeChemistryMatrixSO>(), catalog, 23, true, resolverConfig: config);
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Forked);
            attacker.statuses.Add(new StatusInstance(StatusId.Splinter, StatusClass.Consumed,
                StatusOrigin.ChemistryOnly, 1, "test"));
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, ContactType.Contact, 20));
            ChainlingInstance first = CreateChainling("first", ChainlingType.Gas);
            ChainlingInstance second = CreateChainling("second", ChainlingType.Gas);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, attacker), CreateTeam(1, first, second));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));

            Assert.Less(first.currentHP, first.EffectiveMaxHP);
            Assert.Less(second.currentHP, second.EffectiveMaxHP);
            Assert.IsFalse(HasStatus(attacker, StatusId.Splinter));

            CombatResolver baselineResolver = new CombatResolver(
                CreateAsset<TypeChemistryMatrixSO>(), catalog, 23, true, resolverConfig: CreateConfig());
            ChainlingInstance baselineAttacker = CreateChainling("baseline_attacker", ChainlingType.Forked);
            baselineAttacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, ContactType.Contact, 20));
            ChainlingInstance baselineTarget = CreateChainling("baseline_target", ChainlingType.Gas);
            CombatState baselineState = baselineResolver.InitializeBattle(
                CreateTeam(0, baselineAttacker), CreateTeam(1, baselineTarget));
            baselineResolver.ExecuteTurn(baselineState, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));

            int spreadDamage = first.EffectiveMaxHP - first.currentHP;
            int baselineDamage = baselineTarget.EffectiveMaxHP - baselineTarget.currentHP;
            int expectedSpreadDamage = (int)(baselineDamage * config.splinterDamageMultiplier);
            Assert.That(spreadDamage, Is.InRange(expectedSpreadDamage - 1, expectedSpreadDamage + 1));
        }

        [Test]
        public void SplinterSingleTargetFallbackDealsFifteenPercentMoreAndIsConsumed()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Splinter, StatusClass.Consumed, 1));
            ChainlingInstance attacker = CreateChainling("attacker", ChainlingType.Forked);
            attacker.statuses.Add(new StatusInstance(StatusId.Splinter, StatusClass.Consumed,
                StatusOrigin.ChemistryOnly, 1, "test"));
            attacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, ContactType.Contact, 20));
            ChainlingInstance target = CreateChainling("target", ChainlingType.Gas);
            CombatResolver resolver = new CombatResolver(
                CreateAsset<TypeChemistryMatrixSO>(), catalog, 31, true, resolverConfig: config);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));

            ChainlingInstance baselineAttacker = CreateChainling("baseline_attacker", ChainlingType.Forked);
            baselineAttacker.moves.Add(CreateMove("strike", MoveCategory.DamagingMelee, ContactType.Contact, 20));
            ChainlingInstance baselineTarget = CreateChainling("baseline_target", ChainlingType.Gas);
            CombatResolver baselineResolver = new CombatResolver(
                CreateAsset<TypeChemistryMatrixSO>(), catalog, 31, true, resolverConfig: CreateConfig());
            CombatState baselineState = baselineResolver.InitializeBattle(
                CreateTeam(0, baselineAttacker), CreateTeam(1, baselineTarget));
            baselineResolver.ExecuteTurn(baselineState, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));

            int splinterDamage = target.EffectiveMaxHP - target.currentHP;
            int baselineDamage = baselineTarget.EffectiveMaxHP - baselineTarget.currentHP;
            int expectedSplinterDamage = (int)(baselineDamage * config.splinterSingleTargetDamageMultiplier);
            Assert.That(splinterDamage, Is.InRange(expectedSplinterDamage - 1, expectedSplinterDamage + 1));
            Assert.IsFalse(HasStatus(attacker, StatusId.Splinter));
        }

        [Test]
        public void IsolatedRejectsAllySupportButLeavesTargetUnchanged()
        {
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Isolated, StatusClass.Duration, 2));
            CombatResolver resolver = new CombatResolver(
                CreateAsset<TypeChemistryMatrixSO>(), catalog, 29, true, resolverConfig: CreateConfig());
            ChainlingInstance donor = CreateChainling("donor", ChainlingType.Gas);
            ChainlingInstance partner = CreateChainling("partner", ChainlingType.Gas);
            partner.currentHP = 50;
            partner.statuses.Add(new StatusInstance(StatusId.Isolated, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 2, "test"));
            MoveInstance heal = CreateMove("ally_heal", MoveCategory.Support, ContactType.None, 0);
            heal.healPercentMaxHP = 25;
            donor.moves.Add(heal);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, donor, partner), CreateTeam(1, CreateChainling("enemy", ChainlingType.Block)));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 0, 1), CombatAction.Pass(1));

            Assert.AreEqual(50, partner.currentHP);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.StatusImmune && e.statusId == StatusId.Isolated));
        }

        [Test]
        public void IdenticalChemistrySeedsProduceIdenticalOutcomesAndForensicRolls()
        {
            CombatResolverConfigSO config = CreateConfig();
            StatusCatalogSO catalog = CreateCatalog(Status(StatusId.Slowed, StatusClass.Duration, 2));
            CombatEventLog firstLog = new CombatEventLog(41);
            CombatEventLog secondLog = new CombatEventLog(41);
            StatusProcessor firstProcessor = new StatusProcessor(catalog, firstLog, resolverConfig: config);
            StatusProcessor secondProcessor = new StatusProcessor(catalog, secondLog, resolverConfig: config);
            ChainlingInstance first = CreateChainling("first", ChainlingType.Staked);
            ChainlingInstance second = CreateChainling("second", ChainlingType.Staked);
            first.statuses.Add(new StatusInstance(StatusId.Slowed, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 2, "test"));
            second.statuses.Add(new StatusInstance(StatusId.Slowed, StatusClass.Duration,
                StatusOrigin.ChemistryOnly, 2, "test"));

            firstProcessor.ProcessStartOfTurn(first, 2, new CombatRng(41));
            secondProcessor.ProcessStartOfTurn(second, 2, new CombatRng(41));

            Assert.IsFalse(first.lostActionThisTurn);
            Assert.AreEqual(first.lostActionThisTurn, second.lostActionThisTurn);
            Assert.AreEqual(firstLog.ForensicEvents.Count, secondLog.ForensicEvents.Count);
            if (firstLog.ForensicEvents.Count > 0)
            {
                Assert.AreEqual(firstLog.ForensicEvents[0].eventType, secondLog.ForensicEvents[0].eventType);
                Assert.AreEqual(firstLog.ForensicEvents[0].rngSeed, secondLog.ForensicEvents[0].rngSeed);
                Assert.AreEqual(firstLog.ForensicEvents[0].rngStepIndex, secondLog.ForensicEvents[0].rngStepIndex);
                Assert.AreEqual(firstLog.ForensicEvents[0].payloadJson, secondLog.ForensicEvents[0].payloadJson);
            }
        }

        private CombatResolverConfigSO CreateConfig()
        {
            CombatResolverConfigSO config = CreateAsset<CombatResolverConfigSO>();
            config.ResetToDefaults();
            config.randomRollMin = 1f;
            config.randomRollMax = 1f;
            return config;
        }

        private StatusCatalogSO CreateCatalog(params StatusDefinitionSO[] definitions)
        {
            StatusCatalogSO catalog = CreateAsset<StatusCatalogSO>();
            catalog.definitions.AddRange(definitions);
            catalog.BuildLookup();
            return catalog;
        }

        private StatusDefinitionSO Status(StatusId id, StatusClass statusClass, int duration,
            int threshold = 0, float eotDamage = 0f)
        {
            StatusDefinitionSO definition = CreateAsset<StatusDefinitionSO>();
            definition.statusId = id;
            definition.displayName = id.ToString();
            definition.statusClass = statusClass;
            definition.origin = StatusOrigin.ChemistryOnly;
            definition.defaultDuration = duration;
            definition.counterThreshold = threshold;
            definition.hasEndOfTurnDamage = eotDamage > 0f;
            definition.endOfTurnDamagePercent = eotDamage;
            definition.isBuff = id == StatusId.Firewall || id == StatusId.Drip || id == StatusId.FOMO || id == StatusId.Splinter;
            definition.isDebuff = !definition.isBuff;
            if (id == StatusId.FOMO)
            {
                definition.stageEffects = new[]
                {
                    new StatusStageEffect { stat = CoreStat.MeleeAtk, stages = 1, applyOnce = true },
                };
            }
            return definition;
        }

        private TypeChemistryMatrixSO CreateMatrix(ChainlingType attacker, ChainlingType defender, StatusId status)
        {
            TypeChemistryMatrixSO matrix = CreateAsset<TypeChemistryMatrixSO>();
            ChemistryStatusPolarity polarity = ChemistryStatusRules.GetPolarity(status);
            matrix.reactions.Add(new ChemistryReaction
            {
                attackerType = attacker,
                defenderType = defender,
                output = new ChemistryOutput
                {
                    statusId = status,
                    recipient = polarity == ChemistryStatusPolarity.Harmful
                        ? ChemistryRecipient.Defender
                        : ChemistryRecipient.Attacker,
                },
            });
            return matrix;
        }

        private ChainlingInstance CreateChainling(string id, ChainlingType type, int maxHp = 100)
        {
            ChainlingInstance chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                primaryType = type,
                stars = 1,
                baseMaxHP = maxHp,
                currentHP = maxHp,
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
                baseSpeed = 20,
                maxSlots = 4,
                isAlive = true,
            };
            chainling.SyncDisplayVitals();
            return chainling;
        }

        private MoveInstance CreateMove(string id, MoveCategory category, ContactType contactType, int power)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = category,
                targeting = category == MoveCategory.Support ? MoveTargeting.SingleAlly : MoveTargeting.SingleEnemy,
                contactType = contactType,
                basePower = power,
                baseAccuracy = 100,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }

        private static TeamState CreateTeam(int index, params ChainlingInstance[] chainlings)
        {
            TeamState team = new TeamState(index, $"Team {index}");
            team.chainlings.AddRange(chainlings);
            return team;
        }

        private static bool HasStatus(ChainlingInstance chainling, StatusId statusId)
        {
            return chainling.statuses.Any(s => s.statusId == statusId && !s.IsExpired && !s.isConsumed);
        }

        private static ChemistryResult NeutralChemistry()
        {
            return new ChemistryResult
            {
                advantageFactor = 1.25f,
                disadvantageFactor = 0.8f,
            };
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }
    }
}
