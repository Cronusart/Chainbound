using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatDecisionRoundTests
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
        public void PlayerFacingRechargeCountsUpcomingSkippedTurns()
        {
            Assert.AreEqual("Bean Sip can be used again next turn.",
                CombatResolver.FormatPlayerFacingRecharge("Bean Sip", 1));
            Assert.AreEqual("Bean Sip now recharging (1 turn)",
                CombatResolver.FormatPlayerFacingRecharge("Bean Sip", 2));
            Assert.AreEqual("AP Boost now recharging (2 turns)",
                CombatResolver.FormatPlayerFacingRecharge("AP Boost", 3));
            Assert.AreEqual("Fox Feint cannot be used again this battle.",
                CombatResolver.FormatPlayerFacingRecharge("Fox Feint", 999));
        }

        [Test]
        public void BeanSipLogsOneTurnAndGrantsOneAp()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance actor = CreateChainling("cofi", 3, 20);
            ChainlingInstance enemy = CreateChainling("enemy", 0, 10);
            MoveInstance sip = CreateMove("bean_sip", "Bean Sip", MoveCategory.StatusUtility, MoveTargeting.Self, 1, 0);
            sip.rechargeTurns = 2;
            sip.utilityEffects.Add(new UtilityEffectPayload
            {
                effectType = UtilityEffectType.GainAP,
                apGain = 1,
                chance = 1f,
                targetOverride = MoveTargeting.Self,
                stackKey = "bean_sip",
            });
            actor.moves.Add(sip);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, enemy));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            StatusInstance recharge = actor.statuses.Single(s => s.statusId == StatusId.Recharging);
            Assert.AreEqual(1, recharge.turnsRemaining);
            Assert.AreEqual(5, actor.currentAP);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.MoveRecharging &&
                e.message == "Bean Sip now recharging (1 turn)"));
        }

        [Test]
        public void ApBoostAndFoxFeintKeepDurationsAndLogSkippedTurns()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance actor = CreateChainling("actor", 0, 20);
            ChainlingInstance enemy = CreateChainling("enemy", 0, 10);
            MoveInstance boost = CreateMove("ap_boost", "AP Boost", MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0);
            boost.rechargeTurns = 3;
            actor.moves.Add(boost);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, enemy));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(2, actor.statuses.Single(s => s.statusId == StatusId.Recharging).turnsRemaining);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.message == "AP Boost now recharging (2 turns)"));

            CombatResolver feintResolver = CreateResolver();
            ChainlingInstance feinter = CreateChainling("feinter", 4, 20);
            MoveInstance feint = CreateMove("fox_feint", "Fox Feint", MoveCategory.StatusUtility, MoveTargeting.Self, 1, 0);
            feint.rechargeTurns = 999;
            feint.triggerOncePerBattle = true;
            feinter.moves.Add(feint);
            CombatState feintState = feintResolver.InitializeBattle(CreateTeam(0, feinter), CreateTeam(1, CreateChainling("foe", 0, 10)));
            feintResolver.ExecuteTurn(feintState, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(998, feinter.statuses.Single(s => s.statusId == StatusId.Recharging).turnsRemaining);
            Assert.IsTrue(feintResolver.Log.Events.Any(e =>
                e.message == "Fox Feint cannot be used again this battle."));
        }

        [Test]
        public void BenchRechargeTicksAndOtherStatusesPause()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance lead = CreateChainling("lead", 0, 20);
            ChainlingInstance partner = CreateChainling("partner", 0, 18);
            ChainlingInstance bench = CreateChainling("bench", 4, 12);
            TeamState allies = CreateTeam(0, lead, partner, bench);
            CombatState state = resolver.InitializeBattle(allies, CreateTeam(1, CreateChainling("enemy", 0, 10)));
            bench.statuses.Add(new StatusInstance(StatusId.Recharging, StatusClass.Consumed, StatusOrigin.General, 2, "bench_move")
            {
                rechargingMoveId = "bench_move",
            });
            bench.statuses.Add(new StatusInstance(StatusId.ThreadLock, StatusClass.Duration, StatusOrigin.General, 2, "lock"));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.AreEqual(1, bench.statuses.Single(s => s.statusId == StatusId.Recharging).turnsRemaining);
            Assert.AreEqual(2, bench.statuses.Single(s => s.statusId == StatusId.ThreadLock).turnsRemaining);
            Assert.AreEqual(4, bench.currentAP);

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.IsFalse(bench.statuses.Any(s => s.statusId == StatusId.Recharging));
            Assert.AreEqual(2, bench.statuses.Single(s => s.statusId == StatusId.ThreadLock).turnsRemaining);
        }

        [Test]
        public void ApplyOnceStagesRevertOnExpiryAndDoNotStackOnRefresh()
        {
            StatusCatalogSO catalog = CreateCatalog(
                StageStatus(StatusId.GasSpike, 2, false, false,
                    Stage(CoreStat.MeleeAtk, 2),
                    Stage(CoreStat.MeleeDef, -2)),
                StageStatus(StatusId.SearField, 1, true, false, Stage(CoreStat.MeleeAtk, -1)),
                StageStatus(StatusId.Immovable, 2, false, true, Stage(CoreStat.MeleeDef, 2)));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(30));
            ChainlingInstance gas = CreateChainling("gas", 0, 10);
            ChainlingInstance sear = CreateChainling("sear", 0, 10);
            ChainlingInstance wall = CreateChainling("wall", 0, 10);

            Assert.IsTrue(processor.ApplyStatus(gas, StatusId.GasSpike, 2, "fuel", 1));
            Assert.AreEqual(2, gas.stageMeleeAtk);
            Assert.AreEqual(-2, gas.stageMeleeDef);
            processor.ProcessEndOfTurn(gas, 1);
            Assert.AreEqual(1, gas.statuses.Single().turnsRemaining);

            Assert.IsTrue(processor.ApplyStatus(gas, StatusId.GasSpike, 2, "fuel", 2));
            Assert.AreEqual(2, gas.statuses.Single().turnsRemaining);
            Assert.AreEqual(2, gas.stageMeleeAtk);
            Assert.AreEqual(-2, gas.stageMeleeDef);
            Assert.IsTrue(processor.ApplyStatus(gas, StatusId.GasSpike, 1, "fuel", 2));
            Assert.AreEqual(2, gas.statuses.Single().turnsRemaining);
            processor.ProcessEndOfTurn(gas, 3);
            processor.ProcessEndOfTurn(gas, 4);
            Assert.AreEqual(0, gas.statuses.Count);
            Assert.AreEqual(0, gas.stageMeleeAtk);
            Assert.AreEqual(0, gas.stageMeleeDef);

            processor.ApplyStatus(sear, StatusId.SearField, 1, "sear_field", 1);
            Assert.AreEqual(-1, sear.stageMeleeAtk);
            processor.ProcessEndOfTurn(sear, 1);
            Assert.AreEqual(0, sear.statuses.Count);
            Assert.AreEqual(0, sear.stageMeleeAtk);

            processor.ApplyStatus(wall, StatusId.Immovable, 2, "immovable_stance", 1);
            Assert.AreEqual(2, wall.stageMeleeDef);
            processor.ProcessEndOfTurn(wall, 1);
            Assert.AreEqual(2, wall.stageMeleeDef);
            Assert.AreEqual(1, wall.statuses.Single().turnsRemaining);
            processor.ProcessEndOfTurn(wall, 2);
            Assert.AreEqual(0, wall.statuses.Count);
            Assert.AreEqual(0, wall.stageMeleeDef);
        }

        [Test]
        public void RepeatingStagesRevertWhenTheStatusExpires()
        {
            StatusCatalogSO catalog = CreateCatalog(
                StageStatus(StatusId.Corroded, 2, true, false, Stage(CoreStat.MeleeDef, -1, false)),
                StageStatus(StatusId.HODLed, 3, false, true, Stage(CoreStat.MeleeDef, 1, false)));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(31));
            ChainlingInstance corroded = CreateChainling("corroded", 0, 10);
            ChainlingInstance hodl = CreateChainling("hodl", 0, 10);

            processor.ApplyStatus(corroded, StatusId.Corroded, 2, "chem", 1, bypassChemistryCheck: true);
            Assert.AreEqual(0, corroded.stageMeleeDef);
            processor.ProcessEndOfTurn(corroded, 1);
            Assert.AreEqual(-1, corroded.stageMeleeDef);
            Assert.AreEqual(1, corroded.statuses.Single().turnsRemaining);
            processor.ProcessEndOfTurn(corroded, 2);
            Assert.AreEqual(0, corroded.statuses.Count);
            Assert.AreEqual(0, corroded.stageMeleeDef);

            processor.ApplyStatus(hodl, StatusId.HODLed, 3, "catalog", 1);
            Assert.AreEqual(0, hodl.stageMeleeDef);
            processor.ProcessEndOfTurn(hodl, 1);
            Assert.AreEqual(2, hodl.stageMeleeDef);
            processor.ProcessEndOfTurn(hodl, 2);
            Assert.AreEqual(3, hodl.stageMeleeDef);
            processor.ProcessEndOfTurn(hodl, 3);
            Assert.AreEqual(0, hodl.statuses.Count);
            Assert.AreEqual(0, hodl.stageMeleeDef);
        }

        [Test]
        public void CleanseRevertsOwnedDebuffStages()
        {
            StatusCatalogSO catalog = CreateCatalog(
                StageStatus(StatusId.FUD, 2, true, false,
                    Stage(CoreStat.MeleeAtk, -1),
                    Stage(CoreStat.MeleeDef, -1),
                    Stage(CoreStat.Speed, -1)));
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(33));
            ChainlingInstance target = CreateChainling("target", 0, 10);

            Assert.IsTrue(processor.ApplyStatus(target, StatusId.FUD, 2, "fud", 1));
            Assert.AreEqual(-1, target.stageMeleeAtk);
            Assert.AreEqual(-1, target.stageMeleeDef);
            Assert.AreEqual(-1, target.stageSpeed);

            Assert.AreEqual(1, processor.Cleanse(target, 1, 2));
            Assert.AreEqual(0, target.stageMeleeAtk);
            Assert.AreEqual(0, target.stageMeleeDef);
            Assert.AreEqual(0, target.stageSpeed);
            Assert.IsFalse(target.statuses.Any(s => s.statusId == StatusId.FUD));
        }

        [Test]
        public void CipherLockBlocksNewBuffsAndPositiveStagesWithoutRemovingExistingBuffs()
        {
            StatusDefinitionSO cipherLock = StageStatus(StatusId.CipherLocked, 2, true, false);
            StatusDefinitionSO existingBuff = StageStatus(
                StatusId.Immovable, 3, false, true, Stage(CoreStat.MeleeDef, 2));
            StatusDefinitionSO blockedBuff = StageStatus(
                StatusId.HODLed, 2, false, true, Stage(CoreStat.Speed, 1));
            StatusDefinitionSO allowedDebuff = StageStatus(
                StatusId.FUD, 2, true, false, Stage(CoreStat.MeleeAtk, -1));
            CombatEventLog log = new CombatEventLog(35);
            StatusProcessor processor = new StatusProcessor(
                CreateCatalog(cipherLock, existingBuff, blockedBuff, allowedDebuff), log);
            ChainlingInstance target = CreateChainling("target", 0, 10);

            Assert.IsTrue(processor.ApplyStatus(target, StatusId.Immovable, 3, "setup", 1));
            Assert.AreEqual(2, target.stageMeleeDef);
            Assert.IsTrue(processor.ApplyStatus(target, StatusId.CipherLocked, 2, "cipher_shot", 1));

            Assert.IsFalse(processor.ApplyStatus(target, StatusId.HODLed, 2, "self_buff", 1));
            Assert.AreEqual(0, target.stageSpeed);
            Assert.AreEqual(0, BuffGate.FilterPositiveStage(target, CoreStat.Speed, 1, "random_buff", 1));
            Assert.AreEqual(-1, BuffGate.FilterPositiveStage(target, CoreStat.Speed, -1, "debuff", 1));
            Assert.IsTrue(processor.ApplyStatus(target, StatusId.FUD, 2, "debuff", 1));
            Assert.AreEqual(-1, target.stageMeleeAtk);

            Assert.IsTrue(target.statuses.Any(s => s.statusId == StatusId.Immovable));
            Assert.AreEqual(2, target.stageMeleeDef);
            Assert.IsTrue(log.Events.Any(e => e.eventType == CombatEventType.BuffBlocked));
        }

        [Test]
        public void ChemistryRefreshResetsDurationWithoutStackingStages()
        {
            CombatResolverConfigSO config = CreateAsset<CombatResolverConfigSO>();
            config.ResetToDefaults();
            StatusDefinitionSO dusted = StageStatus(StatusId.Dusted, 2, true, false, Stage(CoreStat.Speed, -3));
            dusted.origin = StatusOrigin.ChemistryOnly;
            StatusCatalogSO catalog = CreateCatalog(dusted);
            TypeChemistryMatrixSO matrix = CreateAsset<TypeChemistryMatrixSO>();
            matrix.reactions.Add(new ChemistryReaction
            {
                attackerType = ChainlingType.Block,
                defenderType = ChainlingType.Gas,
                output = new ChemistryOutput
                {
                    statusId = StatusId.Dusted,
                    recipient = ChemistryRecipient.Defender,
                },
            });
            ChemistryResolver chemistry = new ChemistryResolver(matrix, catalog, new CombatEventLog(32), true, config);
            StatusProcessor processor = new StatusProcessor(catalog, new CombatEventLog(32), resolverConfig: config);
            ChainlingInstance attacker = CreateChainling("attacker", 0, 10);
            attacker.primaryType = ChainlingType.Block;
            ChainlingInstance target = CreateChainling("target", 0, 10);
            target.primaryType = ChainlingType.Gas;

            chemistry.Resolve(ChainlingType.Block, attacker, target, 1, new CombatRng(4), true, out _);
            Assert.AreEqual(-3, target.stageSpeed);
            Assert.AreEqual(2, target.statuses.Single().turnsRemaining);
            processor.ProcessEndOfTurn(target, 1);
            Assert.AreEqual(1, target.statuses.Single().turnsRemaining);

            chemistry.Resolve(ChainlingType.Block, attacker, target, 2, new CombatRng(4), true, out _);
            Assert.AreEqual(2, target.statuses.Single().turnsRemaining);
            Assert.AreEqual(-3, target.stageSpeed);
            processor.ProcessEndOfTurn(target, 3);
            processor.ProcessEndOfTurn(target, 4);
            Assert.AreEqual(0, target.statuses.Count);
            Assert.AreEqual(0, target.stageSpeed);
        }

        [Test]
        public void GroupHugGrantsOneApToActiveAlliesAndDoesNotCopyTheSupportMove()
        {
            CombatResolver resolver = CreateResolver();
            ChainlingInstance user = CreateChainling("user", 0, 20);
            ChainlingInstance partner = CreateChainling("partner", 0, 18);
            ChainlingInstance bench = CreateChainling("bench", 4, 12);
            user.currentHP = 80;
            partner.currentHP = 50;
            MoveInstance hug = CreateMove("potion", "Potion", MoveCategory.Support, MoveTargeting.SingleAlly, 0, 0);
            hug.healPercentMaxHP = 20;
            hug.modifier1SectionId = "4.5-a";
            hug.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = "4.5-a",
                category = ModifierCategory.AUG,
                scope = ModifierScope.Active,
            });
            user.moves.Add(hug);

            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, user, partner, bench),
                CreateTeam(1, CreateChainling("enemy", 0, 10)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 0, 1), CombatAction.Pass(1));

            Assert.AreEqual(80, user.currentHP);
            Assert.AreEqual(70, partner.currentHP);
            Assert.AreEqual(3, user.currentAP);
            Assert.AreEqual(3, partner.currentAP);
            Assert.AreEqual(4, bench.currentAP);
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.ModifierTriggered &&
                e.message != null &&
                e.message.Contains("+1 AP")));
        }

        private CombatResolver CreateResolver()
        {
            return new CombatResolver(CreateAsset<TypeChemistryMatrixSO>(), CreateCatalog(
                PlainStatus(StatusId.Recharging),
                PlainStatus(StatusId.ThreadLock)), 1234, true);
        }

        private StatusCatalogSO CreateCatalog(params StatusDefinitionSO[] definitions)
        {
            StatusCatalogSO catalog = CreateAsset<StatusCatalogSO>();
            catalog.definitions.AddRange(definitions);
            catalog.BuildLookup();
            return catalog;
        }

        private StatusDefinitionSO PlainStatus(StatusId id)
        {
            StatusDefinitionSO def = CreateAsset<StatusDefinitionSO>();
            def.statusId = id;
            def.displayName = id.ToString();
            def.statusClass = StatusClass.Duration;
            def.origin = StatusOrigin.General;
            def.defaultDuration = 1;
            return def;
        }

        private StatusDefinitionSO StageStatus(StatusId id, int duration, bool debuff, bool buff, params StatusStageEffect[] stages)
        {
            StatusDefinitionSO def = PlainStatus(id);
            def.defaultDuration = duration;
            def.isDebuff = debuff;
            def.isBuff = buff;
            def.stageEffects = stages;
            return def;
        }

        private static StatusStageEffect Stage(CoreStat stat, int stages, bool applyOnce = true)
        {
            return new StatusStageEffect { stat = stat, stages = stages, applyOnce = applyOnce };
        }

        private static ChainlingInstance CreateChainling(string id, int ap, int speed)
        {
            ChainlingInstance chainling = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                stars = 1,
                currentAP = ap,
                baseMaxHP = 100,
                currentHP = 100,
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

        private static TeamState CreateTeam(int teamIndex, params ChainlingInstance[] chainlings)
        {
            TeamState team = new TeamState(teamIndex, $"Team {teamIndex}");
            team.chainlings.AddRange(chainlings);
            return team;
        }

        private static MoveInstance CreateMove(
            string id,
            string displayName,
            MoveCategory category,
            MoveTargeting targeting,
            int apCost,
            int power)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = displayName,
                category = category,
                targeting = targeting,
                contactType = ContactType.None,
                basePower = power,
                apCost = apCost,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
                tier = MoveTier.Known,
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
