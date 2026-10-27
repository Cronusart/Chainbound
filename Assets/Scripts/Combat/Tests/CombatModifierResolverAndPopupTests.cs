using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class TurnActionLedgerTests
    {
        [Test]
        public void BeginTurnRollsMissesAndClearsCurrentTurnState()
        {
            var ledger = new TurnActionLedger();
            ledger.BeginTurn(1);
            ledger.RecordMiss("target_a");
            ledger.RecordHit("target_b");
            ledger.RecordHpDamage("target_b", 12);
            ledger.RecordDamagingMove(0, "ally_a");

            ledger.BeginTurn(2);

            Assert.IsTrue(ledger.DidTargetMissLastTurn("target_a"));
            Assert.IsFalse(ledger.WasTargetHitThisTurn("target_b"));
            Assert.IsFalse(ledger.DidTargetTakeDamageThisTurn("target_b"));
            Assert.IsFalse(ledger.DidTeamUseDamagingMoveThisTurn(0));
        }

        [Test]
        public void TracksAllyDamageAndAllyKoByTeam()
        {
            var ledger = new TurnActionLedger();
            ledger.BeginTurn(3);

            ledger.RecordDamagingMove(0, "lead");
            ledger.RecordKo("fallen_partner", 0);

            Assert.IsTrue(ledger.DidTeamUseDamagingMoveThisTurn(0));
            Assert.IsTrue(ledger.DidAllyUseDamagingMoveThisTurn(0, "partner"));
            Assert.IsFalse(ledger.DidAllyUseDamagingMoveThisTurn(0, "lead"));
            Assert.IsTrue(ledger.DidAllyKoOccurThisTurn(0));
            Assert.IsFalse(ledger.DidAllyKoOccurThisTurn(1));
        }

        [Test]
        public void SetupFeedAppliesOnlyToTheNextAllyPowerAction()
        {
            var ledger = new TurnActionLedger();
            ledger.BeginTurn(1);
            ledger.RecordActorAction("feeder");
            ledger.ArmSetupFeed(0, "feeder", 0.12f);

            ledger.BeginTurn(2);
            Assert.IsTrue(ledger.HasPendingSetupFeed(0, "feeder"));

            ledger.RecordActorAction("feeder");
            var ignored = new List<float>();
            Assert.IsFalse(ledger.TryConsumeSetupFeeds(0, "feeder", true, ignored));
            Assert.IsTrue(ledger.HasPendingSetupFeed(0, "feeder"));

            ledger.RecordActorAction("ally");
            var wasted = new List<float>();
            Assert.IsTrue(ledger.TryConsumeSetupFeeds(0, "ally", false, wasted));
            Assert.AreEqual(1, wasted.Count);
            Assert.AreEqual(0.12f, wasted[0]);
            Assert.IsFalse(ledger.TryPeekActiveSetupFeed("ally", out _));
            Assert.IsFalse(ledger.HasPendingSetupFeed(0, "feeder"));
        }

        [Test]
        public void SetupFeedPowerActionStoresTheExactFractionUntilCleared()
        {
            var ledger = new TurnActionLedger();
            ledger.RecordActorAction("feeder");
            ledger.ArmSetupFeed(0, "feeder", 0.12f);
            ledger.RecordActorAction("ally");

            var applied = new List<float>();
            Assert.IsTrue(ledger.TryConsumeSetupFeeds(0, "ally", true, applied));
            Assert.IsTrue(ledger.TryPeekActiveSetupFeed("ally", out float fraction));
            Assert.AreEqual(0.12f, fraction);
            Assert.IsFalse(ledger.TryConsumeSetupFeeds(1, "enemy", true, applied));

            ledger.ClearActiveSetupFeed();
            Assert.IsFalse(ledger.TryPeekActiveSetupFeed("ally", out _));
        }
    }

    public class ModifierOutcomePayloadTests
    {
        [Test]
        public void EventRoundTripsStructuredPayload()
        {
            CombatEvent evt = ModifierOutcomePayload.CreateEvent(
                turn: 4,
                sectionId: "6.5-a",
                category: ModifierCategory.TRG,
                displayText: "KO AP",
                anchorChainlingId: "attacker",
                moment: ModifierPopupMoment.OnKoTrigger,
                gameplayApplied: true,
                reason: "OnKO effect applied",
                sourceChainlingId: "attacker",
                targetChainlingId: "target",
                moveId: "move");

            Assert.IsTrue(evt.boolValue);
            Assert.IsTrue(ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload payload));
            Assert.AreEqual("6.5-a", payload.sectionId);
            Assert.AreEqual("KO AP", payload.displayText);
            Assert.AreEqual("attacker", payload.anchorChainlingId);
            Assert.AreEqual(ModifierPopupMoment.OnKoTrigger, payload.moment);
            Assert.IsTrue(payload.gameplayApplied);
        }

        [Test]
        public void ModifierOutcomePayload_RoundTripsNameAndAmount_AndParsesOldJson()
        {
            ModifierOutcomePayload authored = ModifierOutcomePayload.Create(
                "6.1-a",
                ModifierCategory.TRG,
                "HEAL",
                "attacker",
                ModifierPopupMoment.OnHitApplied,
                true,
                "On-hit heal applied",
                "attacker",
                "target",
                "move",
                "Leech",
                7);

            Assert.IsTrue(ModifierOutcomePayload.TryParse(authored.ToJson(), out ModifierOutcomePayload roundTrip));
            Assert.AreEqual("Leech", roundTrip.modifierName);
            Assert.AreEqual(7, roundTrip.amount);

            const string oldJson = "{\"schemaVersion\":1,\"sectionId\":\"6.1-a\",\"category\":\"TRG\",\"gameplayApplied\":true}";
            Assert.IsTrue(ModifierOutcomePayload.TryParse(oldJson, out ModifierOutcomePayload oldPayload));
            Assert.IsTrue(string.IsNullOrEmpty(oldPayload.modifierName));
            Assert.AreEqual(0, oldPayload.amount);
        }
    }

    public class PotencyModifierMomentTests : CombatModifierResolverTestBase
    {
        [Test]
        public void PowerModifierEmitsPayloadAfterDamageProof()
        {
            var catalog = CreateModifierCatalog(Modifier("2.1-a", ModifierCategory.POT, "Power bonus", 50f, 50f));
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("attacker", 0, 40);
            var defender = CreateChainling("defender", 0, 10);
            var move = CreateMove("powered_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 40, 100);
            AddModifier(move, "2.1-a", ModifierCategory.POT, 50f, 50f);
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            List<CombatEvent> events = resolver.Log.Events.ToList();
            int damageIndex = events.FindIndex(e => e.eventType == CombatEventType.DamageDealt);
            int modifierIndex = events.FindIndex(e => e.eventType == CombatEventType.ModifierTriggered && e.boolValue);

            Assert.GreaterOrEqual(damageIndex, 0);
            Assert.Greater(modifierIndex, damageIndex);
            AssertPayload(events[modifierIndex], "2.1-a", "POWER", attacker.instanceId, ModifierPopupMoment.OnHitApplied);
        }
    }

    public class EfficiencyRefundModifierTests : CombatModifierResolverTestBase
    {
        [Test]
        public void GuaranteedRefundEmitsRefundPayloadOnlyWhenRefundApplies()
        {
            var catalog = CreateModifierCatalog(Modifier("3.1-a", ModifierCategory.EFF, "100% chance to refund AP", 100f, 100f));
            var resolver = CreateResolver(catalog);
            var actor = CreateChainling("refunder", 0, 40);
            var target = CreateChainling("target", 0, 10);
            var move = CreateMove("efficient_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 2, 20, 100);
            AddModifier(move, "3.1-a", ModifierCategory.EFF, 100f, 100f);
            actor.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            // LogAP anchors the chainling on targetId (presentation HandleApFeedback uses targetId).
            Assert.IsTrue(resolver.Log.Events.Any(e => e.eventType == CombatEventType.APRefunded && e.targetId == actor.instanceId));
            CombatEvent evt = resolver.Log.Events.First(e => e.eventType == CombatEventType.ModifierTriggered && e.boolValue);
            AssertPayload(evt, "3.1-a", "REFUND", actor.instanceId, ModifierPopupMoment.OnProcSucceeded);
        }
    }

    public class SwapAndKoModifierOutcomeTests : CombatModifierResolverTestBase
    {
        [Test]
        public void OnKoModifierEmitsKoPayload()
        {
            var catalog = CreateModifierCatalog(Modifier("6.5-a", ModifierCategory.TRG, "On KO restore 1 AP", 1f, 1f, ModifierTriggerCondition.OnKO));
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("ko_attacker", 0, 40);
            var defender = CreateChainling("ko_target", 0, 10);
            defender.currentHP = 15;
            var move = CreateMove("ko_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 200, 100);
            AddModifier(move, "6.5-a", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnKO);
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent evt = resolver.Log.Events.First(e => e.eventType == CombatEventType.ModifierTriggered && e.boolValue);
            AssertPayload(evt, "6.5-a", "KO AP", attacker.instanceId, ModifierPopupMoment.OnKoTrigger);
        }

        [Test]
        public void SwapInModifierEmitsSwapPayloadAnchoredToIncomingTarget()
        {
            var log = new CombatEventLog(77);
            var rng = new CombatRng(77);
            var ap = new CombatApService(log);
            var outgoing = CreateChainling("outgoing", 0, 10);
            var incoming = CreateChainling("incoming", 0, 10);
            var team = CreateTeam(0, outgoing, incoming);
            team.activeIndices.Clear();
            team.activeIndices.Add(0);

            var catalog = CreateModifierCatalog(Modifier("6.4-b", ModifierCategory.TRG, "Self gains +1 AP on swap in", 1f, 1f, ModifierTriggerCondition.OnSwapIn));
            var passive = CreateMove("swap_passive", MoveCategory.Passive, MoveTargeting.Self, 0, 0, 0);
            AddModifier(passive, "6.4-b", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnSwapIn);
            incoming.moves.Add(passive);

            SwapModifierResolver.ProcessSwapModifiers(new CombatState(), team, outgoing, incoming, 1, log, rng, catalog, ap);

            CombatEvent evt = log.Events.First(e => e.eventType == CombatEventType.ModifierTriggered && e.boolValue);
            AssertPayload(evt, "6.4-b", "+1 AP", incoming.instanceId, ModifierPopupMoment.OnSwapTrigger);
        }
    }

    public class SynergyModifierResolverTests : CombatModifierResolverTestBase
    {
        [Test]
        public void AllyFirstDamagingMoveEnablesComboPowerPayload()
        {
            var catalog = CreateModifierCatalog(Modifier("7.2-a", ModifierCategory.SYN, "If ally attacked this turn: power", 100f, 100f));
            var resolver = CreateResolver(catalog);
            var fastAlly = CreateChainling("fast_ally", 0, 100);
            var comboUser = CreateChainling("combo_user", 0, 10);
            var target = CreateChainling("target", 0, 1);
            target.baseMaxHP = 500;
            target.currentHP = 500;

            fastAlly.moves.Add(CreateMove("setup_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 10, 100));
            var comboMove = CreateMove("combo_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 20, 100);
            AddModifier(comboMove, "7.2-a", ModifierCategory.SYN, 100f, 100f);
            comboUser.moves.Add(comboMove);

            var state = resolver.InitializeBattle(CreateTeam(0, fastAlly, comboUser), CreateTeam(1, target));
            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.UseMove(0, 0), CombatAction.UseMove(0, 0) },
                new List<CombatAction> { CombatAction.Pass(1) });

            CombatEvent evt = resolver.Log.Events.First(e => e.eventType == CombatEventType.ModifierTriggered && e.boolValue);
            AssertPayload(evt, "7.2-a", "TAG", comboUser.instanceId, ModifierPopupMoment.OnSynergyMet);
        }
    }

    public class TransmutationStatSubTests : CombatModifierResolverTestBase
    {
        [Test]
        public void SpeedSubstitutionUsesSpeedAndEmitsStatPayload()
        {
            var catalog = CreateModifierCatalog(Modifier("5.2-e", ModifierCategory.TRX, "Use Speed instead of MeleeAtk when Speed is higher", 0f, 0f));
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("speed_user", 0, 80);
            attacker.baseMeleeAtk = 10;
            var defender = CreateChainling("target", 0, 10);
            var move = CreateMove("speed_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 40, 100);
            AddModifier(move, "5.2-e", ModifierCategory.TRX, 0f, 0f);
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.StatBreakdown &&
                e.message.Contains("Speed")));
            CombatEvent evt = resolver.Log.Events.First(e => e.eventType == CombatEventType.ModifierTriggered && e.boolValue);
            AssertPayload(evt, "5.2-e", "SPEED", attacker.instanceId, ModifierPopupMoment.OnHitApplied);
        }

        [Test]
        public void CoverStrike_UsesRangedDefenseOnMeleeDamage()
        {
            var definition = Modifier("5.2-h", ModifierCategory.TRX, "Damage uses Ranged Defense instead of Melee Attack", 0f, 0f);
            definition.substitutionStat = CoreStat.RangedDef;
            var catalog = CreateModifierCatalog(definition);
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("cover", 0, 20);
            attacker.baseMeleeAtk = 15;
            attacker.baseRangedDef = 120;
            var defender = CreateChainling("target", 0, 10);
            var move = CreateMove("cover_hit", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, 40, 100);
            AddModifier(move, "5.2-h", ModifierCategory.TRX, 0f, 0f);
            move.modifiers[0].substitutionStat = CoreStat.RangedDef;
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.StatBreakdown &&
                e.message.Contains("RangedDef")));
        }

        [Test]
        public void HeavySignal_UsesMeleeAttackOnRangedDamage()
        {
            var definition = Modifier("5.2-i", ModifierCategory.TRX, "Damage uses Melee Attack instead of Ranged Attack", 0f, 0f);
            definition.substitutionStat = CoreStat.MeleeAtk;
            var catalog = CreateModifierCatalog(definition);
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("signal", 0, 20);
            attacker.baseMeleeAtk = 90;
            attacker.baseRangedAtk = 12;
            var defender = CreateChainling("target", 0, 10);
            var move = CreateMove("signal_hit", MoveCategory.DamagingRanged, MoveTargeting.SingleEnemy, 0, 40, 100);
            AddModifier(move, "5.2-i", ModifierCategory.TRX, 0f, 0f);
            move.modifiers[0].substitutionStat = CoreStat.MeleeAtk;
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent breakdown = resolver.Log.Events.First(e => e.eventType == CombatEventType.StatBreakdown);
            StringAssert.Contains("MeleeAtk", breakdown.message);
            StringAssert.DoesNotContain("RangedAtk", breakdown.message);
        }

        [Test]
        public void CoverStrike_DoesNotSwapStatsOnUtilityMoves()
        {
            var definition = Modifier("5.2-h", ModifierCategory.TRX, "Damage uses Ranged Defense instead of Melee Attack", 0f, 0f);
            definition.substitutionStat = CoreStat.RangedDef;
            var catalog = CreateModifierCatalog(definition);
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("cover", 2, 20);
            attacker.baseRangedDef = 120;
            var defender = CreateChainling("target", 0, 10);
            var move = CreateMove("cover_utility", MoveCategory.StatusUtility, MoveTargeting.SingleEnemy, 0, 0, 100);
            AddModifier(move, "5.2-h", ModifierCategory.TRX, 0f, 0f);
            move.modifiers[0].substitutionStat = CoreStat.RangedDef;
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.IsFalse(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.StatBreakdown &&
                e.message != null &&
                e.message.Contains(": RangedDef")));
        }
    }

    public abstract class CombatModifierResolverTestBase
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

        protected CombatResolver CreateResolver(ModifierCatalogSO modifierCatalog = null, int seed = 1234)
        {
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            var statusCatalog = CreateAsset<StatusCatalogSO>();
            statusCatalog.definitions.Add(Status(StatusId.Recharging, "Recharging", false, false));
            statusCatalog.definitions.Add(Status(StatusId.Staggered, "Staggered", false, true));
            statusCatalog.definitions.Add(Status(StatusId.Surging, "Surging", true, false));
            statusCatalog.BuildLookup();
            return new CombatResolver(matrix, statusCatalog, seed, true, modifierCatalog);
        }

        protected ModifierCatalogSO CreateModifierCatalog(params ModifierDefinitionSO[] definitions)
        {
            var catalog = CreateAsset<ModifierCatalogSO>();
            catalog.modifiers.AddRange(definitions);
            catalog.BuildLookup();
            return catalog;
        }

        protected ModifierDefinitionSO Modifier(
            string sectionId,
            ModifierCategory category,
            string description,
            float effectMin,
            float effectMax,
            ModifierTriggerCondition trigger = ModifierTriggerCondition.None)
        {
            var def = CreateAsset<ModifierDefinitionSO>();
            def.sectionId = sectionId;
            def.category = category;
            def.scope = ModifierScope.Active;
            def.minimumRarity = RarityTier.Common;
            def.effectDescription = description;
            def.effectMin = effectMin;
            def.effectMax = effectMax;
            def.triggerCondition = trigger;
            def.triggerChance = 1f;
            if (sectionId.StartsWith("5.2"))
            {
                def.isStatSubstitution = true;
                def.substitutionStat = CoreStat.Speed;
            }
            return def;
        }

        protected T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        protected static ChainlingInstance CreateChainling(string id, int ap, int speed)
        {
            var chainling = new ChainlingInstance
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

        protected static TeamState CreateTeam(int teamIndex, params ChainlingInstance[] chainlings)
        {
            var team = new TeamState(teamIndex, $"Team {teamIndex}");
            team.chainlings.AddRange(chainlings);
            return team;
        }

        protected static MoveInstance CreateMove(
            string id,
            MoveCategory category,
            MoveTargeting targeting,
            int apCost,
            int power,
            int accuracy)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                tier = MoveTier.Stamped,
                category = category,
                targeting = targeting,
                contactType = category == MoveCategory.DamagingMelee ? ContactType.Contact : ContactType.None,
                baseType = ChainlingType.None,
                basePower = power,
                apCost = apCost,
                baseAccuracy = accuracy,
                hitCount = 1,
                hitCountMax = 1,
                rarity = RarityTier.Common,
            };
        }

        protected static void AddModifier(
            MoveInstance move,
            string sectionId,
            ModifierCategory category,
            float effectValue,
            float effectValueMax,
            ModifierTriggerCondition trigger = ModifierTriggerCondition.None)
        {
            move.modifier1SectionId = sectionId;
            move.modifiers.Add(new ModifierInstance
            {
                definitionSectionId = sectionId,
                category = category,
                scope = ModifierScope.Active,
                rarity = RarityTier.Common,
                effectValue = effectValue,
                effectValueMax = effectValueMax,
                triggerCondition = trigger,
                substitutionStat = sectionId.StartsWith("5.2") ? CoreStat.Speed : CoreStat.MeleeAtk,
            });
        }

        protected static void AssertPayload(
            CombatEvent evt,
            string sectionId,
            string displayText,
            string anchorId,
            ModifierPopupMoment moment)
        {
            Assert.AreEqual(CombatEventType.ModifierTriggered, evt.eventType);
            Assert.IsTrue(evt.boolValue);
            Assert.IsTrue(ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload payload), evt.metadataJson);
            Assert.AreEqual(sectionId, payload.sectionId);
            Assert.AreEqual(displayText, payload.displayText);
            Assert.AreEqual(anchorId, payload.anchorChainlingId);
            Assert.AreEqual(moment, payload.moment);
            Assert.IsTrue(payload.gameplayApplied);
        }

        private StatusDefinitionSO Status(StatusId id, string displayName, bool buff, bool debuff)
        {
            var def = CreateAsset<StatusDefinitionSO>();
            def.statusId = id;
            def.displayName = displayName;
            def.statusClass = StatusClass.Duration;
            def.origin = StatusOrigin.General;
            def.defaultDuration = 1;
            def.isBuff = buff;
            def.isDebuff = debuff;
            return def;
        }
    }

    public class PassAModifierResolverTests : CombatModifierResolverTestBase
    {
        [Test]
        public void EmptyBenchSlots_CountPartyBenchNotMoveSlots()
        {
            var resolver = CreateResolver();
            var lead = CreateChainling("lead", 2, 10);
            var partner = CreateChainling("partner", 2, 10);
            var state = resolver.InitializeBattle(CreateTeam(0, lead, partner), CreateTeam(1, CreateChainling("foe", 2, 10)));

            Assert.AreEqual(1, state.team0.CountEmptyBenchSlots());
            Assert.AreEqual(0, state.team0.CountOccupiedBenchSlots());
        }

        [Test]
        public void PatchKit_HealsSelfOnUse()
        {
            var catalog = CreateModifierCatalog(Modifier("6.8-a", ModifierCategory.TRG, "On use: heal {n}% Max HP", 10f, 10f));
            var resolver = CreateResolver(catalog);
            var actor = CreateChainling("medic", 2, 10);
            actor.currentHP = 50;
            var foe = CreateChainling("foe", 0, 5);
            var move = CreateMove("patch", MoveCategory.StatusUtility, MoveTargeting.Self, 1, 0, 0);
            AddModifier(move, "6.8-a", ModifierCategory.TRG, 10f, 10f);
            actor.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(60, actor.currentHP);
            Assert.IsTrue(resolver.Log.Events.Any(e => e.eventType == CombatEventType.HealApplied && e.intValue == 10));
        }

        [Test]
        public void IgnoreWalls_BypassesFirstHitBarrier()
        {
            var catalog = CreateModifierCatalog(Modifier("4.8-a", ModifierCategory.AUG, "First hit ignores barriers", 0f, 0f));
            var resolver = CreateResolver(catalog);
            var attacker = CreateChainling("breaker", 2, 40);
            var defender = CreateChainling("walled", 0, 10);
            defender.AddMitigationSource(0.25f, 2, 1, "guard_posture", "armor_guard_posture");
            defender.AddBlock(new MitigationSource
            {
                mitigation = 0.5f,
                turnsRemaining = 2,
                hitsRemaining = 1,
                sourceTag = "ember_wall",
                sourceMoveId = "ember_wall",
                stackKey = "wall_ember",
                contactFilter = UtilityContactFilter.Contact,
                blockKind = BlockKind.Wall,
            });
            var move = CreateMove("smash", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1, 40, 100);
            AddModifier(move, "4.8-a", ModifierCategory.AUG, 0f, 0f);
            attacker.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, attacker), CreateTeam(1, defender));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.Less(defender.currentHP, defender.EffectiveMaxHP);
            Assert.IsTrue(defender.HasIntactBlock(), "Ignore Walls skips the block without consuming it.");
            Assert.IsFalse(defender.mitigationSources.Any(x => x.stackKey == "armor_guard_posture"),
                "Armor still applies and spends its hit budget on the bypassed hit.");
            Assert.IsTrue(resolver.Log.Events.Any(e =>
                e.eventType == CombatEventType.ModifierTriggered &&
                e.boolValue &&
                ModifierOutcomePayload.TryFromEvent(e, out var payload) &&
                payload.sectionId == "4.8-a"));
        }

        [Test]
        public void FirstDibs_GrantsPriorityChance()
        {
            var catalog = CreateModifierCatalog(Modifier("1.3-a", ModifierCategory.INIT, "{n}% chance to act first within speed tier", 100f, 100f));
            var resolver = CreateResolver(catalog);
            var actor = CreateChainling("slow", 2, 1);
            var foe = CreateChainling("fast", 2, 50);
            var move = CreateMove("nudge", MoveCategory.StatusUtility, MoveTargeting.Self, 1, 0, 0);
            AddModifier(move, "1.3-a", ModifierCategory.INIT, 100f, 100f);
            actor.moves.Add(move);

            var state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent evt = resolver.Log.Events.First(e =>
                e.eventType == CombatEventType.ModifierTriggered && e.boolValue);
            AssertPayload(evt, "1.3-a", "PRIORITY", actor.instanceId, ModifierPopupMoment.OnPriorityApplied);
        }
    }
}
