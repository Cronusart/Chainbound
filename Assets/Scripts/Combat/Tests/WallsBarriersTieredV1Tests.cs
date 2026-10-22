using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class WallsBarriersTieredV1Tests
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
        public void Wall_BlocksMeleeOnly_BarrierBlocksRangedOnly()
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_test"));
            Assert.That(holder.GetTotalMitigation(ContactType.Contact), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(holder.GetTotalMitigation(ContactType.Projectile), Is.Zero);

            holder.AddBlock(Block(0.3f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_test"), @override: true);
            Assert.That(holder.GetTotalMitigation(ContactType.Contact), Is.Zero);
            Assert.That(holder.GetTotalMitigation(ContactType.Projectile), Is.EqualTo(0.3f).Within(0.0001f));
        }

        [Test]
        public void Block_MitigatesTierPercent_OnOneHit_ThenConsumed()
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.6f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_blockade"));
            Assert.That(holder.mitigationSources.Single().blockTier, Is.EqualTo(BlockTier.Greater));
            Assert.That(holder.GetTotalMitigation(ContactType.Contact), Is.EqualTo(0.6f).Within(0.0001f));

            int removed = holder.ConsumeMitigationHitBudgets(ContactType.Contact, true, out MitigationSource consumed);

            Assert.That(removed, Is.EqualTo(1));
            Assert.That(consumed.stackKey, Is.EqualTo("wall_blockade"));
            Assert.That(holder.HasIntactBlock(), Is.False);
        }

        [Test]
        public void Block_Miss_DoesNotConsume()
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ember"));

            Assert.That(holder.HasIntactBlock(), Is.True, "A miss never calls hit-budget consumption.");
            Assert.That(holder.mitigationSources.Single().hitsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void Block_NotHitOnCastTurn_StillBlocksNextTurn()
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.4f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ice"));

            holder.TickMitigationSources();

            Assert.That(holder.HasIntactBlock(), Is.True);
            Assert.That(holder.mitigationSources.Single().turnsRemaining, Is.EqualTo(1));
        }

        [Test]
        public void Block_ExpiresEndOfNextTurn_WhenNotHit()
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.4f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ice"));

            holder.TickMitigationSources();
            Assert.That(holder.HasIntactBlock(), Is.True);
            holder.TickMitigationSources();
            Assert.That(holder.HasIntactBlock(), Is.False);
        }

        [Test]
        public void BlockTarget_HoldingIntactBlock_IsNotSelectable()
        {
            CombatState state = TwoAllyState(out ChainlingInstance actor, out ChainlingInstance target);
            MoveInstance move = BlockMove("ember_wall", UtilityContactFilter.Contact);
            actor.moves.Add(move);
            target.AddBlock(Block(0.25f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_buffer"));

            BlockTargetEvaluation result = new CombatActionEligibilityService()
                .EvaluateBlockTarget(actor, move, target, state);

            Assert.That(result.canTarget, Is.False);
            Assert.That(result.reason, Is.EqualTo("BlockAlreadyActive"));
        }

        [Test]
        public void BlockTarget_QueuedByTeammateThisTurn_IsNotSelectable()
        {
            CombatState state = TwoAllyState(out ChainlingInstance actor, out ChainlingInstance target);
            MoveInstance move = BlockMove("ember_wall", UtilityContactFilter.Contact);
            actor.moves.Add(move);
            ChainlingInstance teammate = state.team0.GetActiveInSlot(1);
            teammate.moves.Add(BlockMove("blockade", UtilityContactFilter.Contact));
            var queued = new Dictionary<int, CombatAction>
            {
                [1] = CombatAction.UseMove(0, 0, 0, 1),
            };

            BlockTargetEvaluation result = new CombatActionEligibilityService()
                .EvaluateBlockTarget(actor, move, target, state, queued, actorSlot: 0);

            Assert.That(result.canTarget, Is.False);
            Assert.That(result.reason, Is.EqualTo("BlockAlreadyQueued"));
        }

        [Test]
        public void BlockMove_AllTargetsBlocked_StaysUnavailable_NoApSpent()
        {
            CombatState state = TwoAllyState(out ChainlingInstance actor, out ChainlingInstance target);
            MoveInstance move = BlockMove("ember_wall", UtilityContactFilter.Contact);
            actor.moves.Add(move);
            actor.AddBlock(Block(0.25f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_self"));
            target.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_target"));

            MoveSelectionEvaluation result = new CombatActionEligibilityService()
                .EvaluateMoveSelection(actor, move, state);

            Assert.That(result.canSelect, Is.False);
            Assert.That(result.reason, Is.EqualTo("AllTargetsBlocked"));
            Assert.That(actor.currentAP, Is.EqualTo(5));
        }

        [Test]
        public void Block_ConsumedOrExpired_TargetSelectableAgain()
        {
            CombatState state = TwoAllyState(out ChainlingInstance actor, out ChainlingInstance target);
            MoveInstance move = BlockMove("ember_wall", UtilityContactFilter.Contact);
            actor.moves.Add(move);
            target.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ember"));
            target.ConsumeMitigationHitBudgets(ContactType.Contact);

            Assert.That(new CombatActionEligibilityService().EvaluateBlockTarget(actor, move, target, state).canTarget, Is.True);
        }

        [Test]
        public void Block_PassiveOrModifier_OverridesIntactBlock()
        {
            ChainlingInstance holder = Chainling("holder");
            Assert.That(holder.AddBlock(Block(0.25f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_buffer")), Is.True);
            Assert.That(holder.AddBlock(Block(0.6f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_blockade")), Is.False);
            Assert.That(holder.mitigationSources.Single().stackKey, Is.EqualTo("barrier_buffer"));

            Assert.That(holder.AddBlock(
                Block(0.6f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_blockade"), @override: true), Is.True);
            Assert.That(holder.mitigationSources.Single().stackKey, Is.EqualTo("wall_blockade"));
        }

        [TestCase(UtilityEffectExecutionOrigin.Passive)]
        [TestCase(UtilityEffectExecutionOrigin.Modifier)]
        public void Block_PassiveOrModifierExecution_OverridesIntactBlock(
            UtilityEffectExecutionOrigin origin)
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.25f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_buffer"));
            MoveInstance grantedBlock = BlockMove("blockade", UtilityContactFilter.Contact);

            new UtilityEffectExecutor(null).Execute(
                holder,
                holder,
                grantedBlock,
                turn: 1,
                shouldApplyConditionalEffects: true,
                context: new UtilityEffectExecutionContext { origin = origin });

            Assert.That(holder.mitigationSources, Has.Count.EqualTo(1));
            Assert.That(holder.mitigationSources.Single().stackKey, Is.EqualTo("wall_blockade"));
        }

        [Test]
        public void GuardPosture_IsArmor_StacksWithBlock_NotABlockTarget()
        {
            ChainlingInstance holder = Chainling("holder");
            holder.AddMitigationSource(0.25f, 2, 1, "guard_posture", "armor_guard_posture");
            Assert.That(holder.HasIntactBlock(), Is.False);
            holder.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ember"));

            Assert.That(holder.GetTotalMitigation(ContactType.Contact), Is.EqualTo(0.625f).Within(0.0001f));
        }

        [Test]
        public void WallTag_PrefixOnly_FirewallIsNotAWall()
        {
            Assert.That(CombatWallStateUtility.IsWallTag("wall_ember"), Is.True);
            Assert.That(CombatWallStateUtility.IsWallTag("barrier_buffer"), Is.True);
            Assert.That(CombatWallStateUtility.IsWallTag("Chemistry.Firewall"), Is.False);
            Assert.That(CombatWallStateUtility.IsWallTag("guard_posture"), Is.False);
        }

        [Test]
        public void Block_AuthoredRecharge2_ShowsRecharge1_UsableTurnAfterNext()
        {
            Assert.That(CombatResolver.FormatPlayerFacingRecharge("Ember Wall", 2), Does.Contain("1 turn"));
        }

        [Test]
        public void Blockade_IsAWallForPresentation()
        {
            MoveInstance blockade = BlockMove("blockade", UtilityContactFilter.Contact);
            blockade.tags.Add("PRIOR");
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.6f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_blockade"));

            Assert.That(CombatWallStateUtility.MovePlacesBlock(blockade), Is.True);
            Assert.That(CombatWallPopupLabelUtility.FormatDefenseCardBadge(blockade), Is.EqualTo("WALL · GREATER · PRIOR"));
            Assert.That(CombatWallPopupLabelUtility.FormatHolderChip(holder),
                Is.EqualTo("WALL 60% · MELEE · 1 hit · 2 turns"));
            holder.TickMitigationSources();
            Assert.That(CombatWallPopupLabelUtility.FormatHolderChip(holder),
                Is.EqualTo("WALL 60% · MELEE · 1 hit · 1 turn"));
        }

        [Test]
        public void CoreBreach_IgnoresArmorAndBlock_ConsumesNeither()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance attacker = Chainling("breacher");
            ChainlingInstance defender = Chainling("defender");
            defender.AddMitigationSource(0.25f, 2, 1, "guard_posture", "armor_guard_posture");
            defender.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ember"));
            MoveInstance move = DamageMove("core_breach");
            move.resolveProfile.damage.ignoreMitigation = true;
            attacker.moves.Add(move);
            CombatState state = resolver.InitializeBattle(Team(0, attacker), Team(1, defender));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.That(defender.currentHP, Is.LessThan(defender.EffectiveMaxHP));
            Assert.That(defender.mitigationSources.Any(x => x.stackKey == "armor_guard_posture" && x.hitsRemaining == 1), Is.True);
            Assert.That(defender.HasIntactBlock(), Is.True);
        }

        [Test]
        public void IceWall_GrantsAtMost1Ap_OnlyWhenHolderFaster()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance attacker = Chainling("attacker");
            ChainlingInstance holder = Chainling("holder");
            attacker.baseSpeed = 40;
            holder.baseSpeed = 60;
            holder.currentAP = 0;
            attacker.moves.Add(DamageMove("strike"));
            MitigationSource ice = Block(0.4f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ice");
            ice.apGainOnConsumeIfFaster = 1;
            holder.AddBlock(ice);
            CombatState state = resolver.InitializeBattle(Team(0, attacker), Team(1, holder));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.That(holder.currentAP, Is.EqualTo(3), "2 turn-start AP plus exactly 1 Ice Wall AP.");
            Assert.That(holder.HasIntactBlock(), Is.False);
        }

        [Test]
        public void CompoundBarrier_MeleeDefStacksOnRecast()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance caster = Chainling("caster");
            ChainlingInstance target = Chainling("target");
            ChainlingInstance foe = Chainling("foe");
            MoveInstance compound = BlockMove("compound_guard", UtilityContactFilter.Projectile);
            compound.stageChanges.Add(new MoveStageChange
            {
                stat = CoreStat.MeleeDef,
                stages = 1,
                chance = 1f,
                targetsSelf = false,
            });
            caster.moves.Add(compound);
            TeamState allies = Team(0, caster, target);
            allies.activeIndices.Add(0);
            allies.activeIndices.Add(1);
            TeamState enemies = Team(1, foe);
            enemies.activeIndices.Add(0);
            CombatState state = resolver.InitializeBattle(allies, enemies);

            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.UseMove(0, 0, 0, 1), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Pass(1) });
            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.Pass(0), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Pass(1) });
            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.UseMove(0, 0, 0, 1), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Pass(1) });

            Assert.That(target.stageMeleeDef, Is.EqualTo(2));
        }

        [Test]
        public void LatticeBarrier_Reflects20PercentOfPrevented()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance attacker = Chainling("attacker");
            ChainlingInstance holder = Chainling("holder");
            attacker.moves.Add(DamageMove("ranged_strike"));
            attacker.moves[0].category = MoveCategory.DamagingRanged;
            attacker.moves[0].contactType = ContactType.Projectile;
            MitigationSource lattice = Block(0.4f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_lattice");
            lattice.reflectPercent = 0.2f;
            holder.AddBlock(lattice);
            int attackerHpBefore = attacker.currentHP;
            CombatState state = resolver.InitializeBattle(Team(0, attacker), Team(1, holder));

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            CombatEvent damage = resolver.Log.Events.First(x => x.eventType == CombatEventType.DamageDealt);
            Assert.That(WallInterceptPayload.TryFromEvent(damage, out WallInterceptPayload payload), Is.True);
            Assert.That(payload.reflectedDamage, Is.GreaterThan(0));
            Assert.That(attackerHpBefore - attacker.currentHP, Is.EqualTo(payload.reflectedDamage));
            Assert.That(holder.HasIntactBlock(), Is.False);
        }

        [Test]
        public void Block_PriorityHitAfterBlock_IsMitigated()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance defender = Chainling("defender");
            ChainlingInstance attacker = Chainling("attacker");
            defender.baseSpeed = 10;
            attacker.baseSpeed = 90;
            MoveInstance wall = BlockMove("ember_wall", UtilityContactFilter.Contact);
            wall.tags.Add("PRIOR");
            defender.moves.Add(wall);
            attacker.moves.Add(DamageMove("strike"));
            CombatState state = resolver.InitializeBattle(Team(0, defender), Team(1, attacker));

            resolver.ExecuteTurn(
                state,
                CombatAction.UseMove(0, 0, 0, 0),
                CombatAction.UseMove(1, 0, 0, 0));

            CombatEvent damage = resolver.Log.Events.First(x => x.eventType == CombatEventType.DamageDealt);
            Assert.That(WallInterceptPayload.TryFromEvent(damage, out WallInterceptPayload payload), Is.True);
            Assert.That(payload.wallActiveBeforeHit, Is.True);
            Assert.That(payload.damageAbsorbed, Is.GreaterThan(0));
            Assert.That(defender.HasIntactBlock(), Is.False);
        }

        [Test]
        public void SwapOut_ClearsBlocks_BenchDoesNotTickThem()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance outgoing = Chainling("outgoing");
            ChainlingInstance partner = Chainling("partner");
            ChainlingInstance incoming = Chainling("incoming");
            ChainlingInstance untouchedBench = Chainling("untouched_bench");
            outgoing.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_outgoing"));
            untouchedBench.AddBlock(Block(0.3f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_bench"));
            CombatState state = resolver.InitializeBattle(
                Team(0, outgoing, partner, incoming, untouchedBench),
                Team(1, Chainling("foe")));

            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction> { CombatAction.Swap(0, 2), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.Pass(1) });

            Assert.That(outgoing.HasIntactBlock(), Is.False);
            Assert.That(untouchedBench.HasIntactBlock(), Is.True);
            Assert.That(untouchedBench.mitigationSources.Single().turnsRemaining, Is.EqualTo(2));
        }

        [Test]
        public void WallTimers_TickBeforeStatusTicks_AndDotIgnoresActiveBlock()
        {
            StatusCatalogSO statuses = Create<StatusCatalogSO>();
            StatusDefinitionSO rugged = Create<StatusDefinitionSO>();
            rugged.statusId = StatusId.Rugged;
            rugged.displayName = "Rugged";
            rugged.statusClass = StatusClass.Duration;
            rugged.origin = StatusOrigin.ChemistryOnly;
            rugged.defaultDuration = 3;
            rugged.hasEndOfTurnDamage = true;
            rugged.endOfTurnDamagePercent = 0.1f;
            statuses.definitions.Add(rugged);
            statuses.BuildLookup();
            CombatResolver resolver = Resolver(statuses);
            ChainlingInstance holder = Chainling("holder");
            holder.AddBlock(Block(0.5f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_ember"));
            holder.statuses.Add(new StatusInstance(
                StatusId.Rugged, StatusClass.Duration, StatusOrigin.ChemistryOnly, 3, "test"));
            int hpBefore = holder.currentHP;
            CombatState state = resolver.InitializeBattle(Team(0, holder), Team(1, Chainling("foe")));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));

            Assert.That(holder.currentHP, Is.LessThan(hpBefore));
            Assert.That(holder.HasIntactBlock(), Is.True);
            Assert.That(holder.mitigationSources.Single().turnsRemaining, Is.EqualTo(1));
            Assert.That(resolver.Log.Events.Any(x => x.eventType == CombatEventType.BlockIgnoredDot), Is.True);
        }

        [Test]
        public void Resolver_BlockedCastSafetyNet_PassNoApNoRechargeNoEffects()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance caster = Chainling("caster");
            ChainlingInstance protectedAlly = Chainling("protected");
            ChainlingInstance foe = Chainling("foe");
            caster.currentAP = 3;
            caster.moves.Add(BlockMove("ember_wall", UtilityContactFilter.Contact));
            protectedAlly.AddBlock(Block(0.25f, BlockKind.Barrier, UtilityContactFilter.Projectile, "barrier_buffer"));
            CombatState state = resolver.InitializeBattle(Team(0, caster, protectedAlly), Team(1, foe));

            resolver.ExecuteTurn(
                state,
                CombatAction.UseMove(0, 0, 0, 1),
                CombatAction.Pass(1));

            Assert.That(caster.currentAP, Is.EqualTo(5), "Turn-start AP remains; blocked cast spends none.");
            Assert.That(caster.statuses.Any(x => x.statusId == StatusId.Recharging), Is.False);
            Assert.That(protectedAlly.mitigationSources.Single().stackKey, Is.EqualTo("barrier_buffer"));
        }

        [Test]
        public void Resolver_BlockedCastSafetyNet_UsesTurnStartBlockStateAfterBlockBreaks()
        {
            CombatResolver resolver = Resolver();
            ChainlingInstance caster = Chainling("caster");
            ChainlingInstance protectedAlly = Chainling("protected");
            ChainlingInstance attacker = Chainling("attacker");
            caster.baseSpeed = 10;
            attacker.baseSpeed = 100;
            caster.currentAP = 3;
            caster.moves.Add(BlockMove("ember_wall", UtilityContactFilter.Contact));
            attacker.moves.Add(DamageMove("strike"));
            protectedAlly.AddBlock(Block(0.25f, BlockKind.Wall, UtilityContactFilter.Contact, "wall_existing"));
            CombatState state = resolver.InitializeBattle(
                Team(0, caster, protectedAlly),
                Team(1, attacker));

            resolver.ExecuteTurn2v2(
                state,
                new List<CombatAction>
                {
                    CombatAction.UseMove(0, 0, 0, 1),
                    CombatAction.Pass(0),
                },
                new List<CombatAction>
                {
                    CombatAction.UseMove(1, 0, 0, 1),
                });

            Assert.That(protectedAlly.HasIntactBlock(), Is.False, "The incoming hit should consume the original block.");
            Assert.That(caster.currentAP, Is.EqualTo(5), "The cast chosen against a turn-start holder must spend no AP.");
            Assert.That(caster.statuses.Any(x => x.statusId == StatusId.Recharging), Is.False);
            Assert.That(
                resolver.Log.Events.Any(x => x.eventType == CombatEventType.ValidationError &&
                    x.message.Contains("block already active or queued")),
                Is.True);
        }

        [Test]
        public void AI_NeverQueuesBlockedCast()
        {
            CombatState state = TwoAllyState(out ChainlingInstance first, out ChainlingInstance second);
            first.currentAP = second.currentAP = 5;
            first.moves.Add(BlockMove("ember_wall", UtilityContactFilter.Contact));
            second.moves.Add(BlockMove("blockade", UtilityContactFilter.Contact));
            ChainlingInstance foe = Chainling("foe");
            state.team1 = Team(1, foe);
            state.team1.activeIndices.Add(0);

            List<CombatAction> actions = new DumbAI().GenerateActions(state, 0, new CombatRng(19));

            Assert.That(actions.Count, Is.EqualTo(2));
            Assert.That(actions.All(x => x.actionType == CombatActionType.UseMove), Is.True);
            Assert.That(actions.Select(x => x.targetSlotIndex).Distinct().Count(), Is.EqualTo(2));
        }

        [Test]
        public void Resolver_BlockedCastSafetyNet_NeverFiresInScriptedBattles()
        {
            CombatResolver resolver = Resolver();
            CombatState state = TwoAllyState(out ChainlingInstance first, out ChainlingInstance second);
            first.currentAP = second.currentAP = 5;
            first.moves.Add(BlockMove("ember_wall", UtilityContactFilter.Contact));
            second.moves.Add(BlockMove("blockade", UtilityContactFilter.Contact));
            state.team1.activeIndices.Add(0);
            state = resolver.InitializeBattle(state.team0, state.team1);

            List<CombatAction> actions = new DumbAI().GenerateActions(state, 0, new CombatRng(31));
            resolver.ExecuteTurn2v2(state, actions, new List<CombatAction> { CombatAction.Pass(1) });

            Assert.That(
                resolver.Log.Events.Any(x => x.eventType == CombatEventType.ValidationError &&
                    x.message.Contains("block already active or queued")),
                Is.False);
        }

        [Test]
        public void AI_BlockValue_IsCategoryAware_AndBanksForGreaterThreat()
        {
            ChainlingInstance actor = Chainling("blocker");
            actor.currentAP = 2;
            MoveInstance wall = BlockMove("blockade", UtilityContactFilter.Contact);
            actor.moves.Add(wall);
            ChainlingInstance ally = Chainling("ally");
            ChainlingInstance enemy = Chainling("enemy");
            MoveInstance heavy = DamageMove("heavy_melee");
            heavy.basePower = 100;
            enemy.moves.Add(heavy);
            TeamState mine = Team(0, actor, ally);
            mine.activeIndices.Add(0);
            mine.activeIndices.Add(1);
            TeamState theirs = Team(1, enemy);
            theirs.activeIndices.Add(0);
            CombatState state = new CombatState { team0 = mine, team1 = theirs };

            Assert.That(AIHelpers.ScoreBlockValue(wall, ally, theirs), Is.GreaterThan(0f));
            MoveInstance rangedBarrier = BlockMove("lattice_guard", UtilityContactFilter.Projectile);
            Assert.That(AIHelpers.ScoreBlockValue(rangedBarrier, ally, theirs), Is.LessThan(0f));
            Assert.That(AIHelpers.ShouldBankForGreaterBlock(actor, mine, theirs, state, null, 0), Is.True);
        }

        [Test]
        public void MoveCatalog_RarityTypePoolsUnchanged()
        {
            AssertMoveAsset("ember_wall", "Ember Wall", 2, RarityTier.Legendary, ChainlingType.Burned, "wall_ember");
            AssertMoveAsset("blockade", "Blockade", 3, RarityTier.Uncommon, ChainlingType.Block, "wall_blockade");
            AssertMoveAsset("ice_wall", "Ice Wall", 2, RarityTier.Uncommon, ChainlingType.Frozen, "wall_ice");
            AssertMoveAsset("barrier_wall", "Buffer Barrier", 1, RarityTier.Uncommon, ChainlingType.None, "barrier_buffer");
            AssertMoveAsset("vapor_barrier", "Vapor Barrier", 1, RarityTier.Uncommon, ChainlingType.Gas, "barrier_vapor");
            AssertMoveAsset("compound_guard", "Compound Barrier", 2, RarityTier.Uncommon, ChainlingType.Staked, "barrier_compound");
            AssertMoveAsset("ash_guard", "Ash Barrier", 2, RarityTier.Uncommon, ChainlingType.Burned, "barrier_ash");
            AssertMoveAsset("lattice_guard", "Lattice Barrier", 2, RarityTier.Uncommon, ChainlingType.Shard, "barrier_lattice");

            MoveDefinitionSO armor = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(
                "Assets/Data/Combat/Moves/Move_guard_posture.asset");
            Assert.That(armor, Is.Not.Null);
            Assert.That(armor.tags, Contains.Item("ARMOR"));
            Assert.That(armor.utilityEffects.Single().stackKey, Is.EqualTo("armor_guard_posture"));
        }

        private CombatResolver Resolver(StatusCatalogSO statuses = null)
        {
            TypeChemistryMatrixSO matrix = Create<TypeChemistryMatrixSO>();
            if (statuses == null)
            {
                statuses = Create<StatusCatalogSO>();
                statuses.BuildLookup();
            }
            return new CombatResolver(matrix, statuses, 73, chemistryStrictMode: true);
        }

        private static void AssertMoveAsset(
            string id,
            string displayName,
            int apCost,
            RarityTier rarity,
            ChainlingType baseType,
            string stackKey)
        {
            MoveDefinitionSO move = AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(
                $"Assets/Data/Combat/Moves/Move_{id}.asset");
            Assert.That(move, Is.Not.Null, id);
            Assert.That(move.displayName, Is.EqualTo(displayName), id);
            Assert.That(move.apCost, Is.EqualTo(apCost), id);
            Assert.That(move.rechargeTurns, Is.EqualTo(2), id);
            Assert.That(move.rarity, Is.EqualTo(rarity), id);
            Assert.That(move.baseType, Is.EqualTo(baseType), id);
            Assert.That(move.targeting, Is.EqualTo(MoveTargeting.SingleAlly), id);
            Assert.That(move.tags, Contains.Item("PRIOR"), id);
            UtilityEffectPayload block = move.utilityEffects.Single(x => x.effectType == UtilityEffectType.BlockWindow);
            Assert.That(block.stackKey, Is.EqualTo(stackKey), id);
            Assert.That(block.hitBudget, Is.EqualTo(1), id);
            Assert.That(block.turnBudget, Is.EqualTo(2), id);
        }

        private T Create<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _created.Add(value);
            return value;
        }

        private static CombatState TwoAllyState(out ChainlingInstance first, out ChainlingInstance second)
        {
            first = Chainling("first");
            second = Chainling("second");
            TeamState team = Team(0, first, second);
            team.activeIndices.Add(0);
            team.activeIndices.Add(1);
            return new CombatState { team0 = team, team1 = Team(1, Chainling("foe")) };
        }

        private static TeamState Team(int index, params ChainlingInstance[] members)
        {
            TeamState team = new TeamState(index, $"Team {index}");
            team.chainlings.AddRange(members);
            return team;
        }

        private static ChainlingInstance Chainling(string id)
        {
            ChainlingInstance value = new ChainlingInstance
            {
                instanceId = id,
                speciesId = id,
                displayName = id,
                isAlive = true,
                currentHP = 100,
                baseMaxHP = 100,
                baseMeleeAtk = 50,
                baseMeleeDef = 50,
                baseRangedAtk = 50,
                baseRangedDef = 50,
                baseSpeed = 50,
                currentAP = 5,
                maxSlots = 4,
                primaryType = ChainlingType.Node,
            };
            value.SyncDisplayVitals();
            return value;
        }

        private static MitigationSource Block(
            float mitigation,
            BlockKind kind,
            UtilityContactFilter filter,
            string stackKey)
        {
            return new MitigationSource
            {
                mitigation = mitigation,
                turnsRemaining = 2,
                hitsRemaining = 1,
                stackKey = stackKey,
                sourceTag = stackKey,
                sourceMoveId = stackKey,
                contactFilter = filter,
                blockKind = kind,
            };
        }

        private static MoveInstance BlockMove(string id, UtilityContactFilter filter)
        {
            return new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = MoveCategory.Support,
                targeting = MoveTargeting.SingleAlly,
                contactType = ContactType.None,
                apCost = id == "blockade" ? 3 : 2,
                rechargeTurns = 2,
                hitCount = 1,
                hitCountMax = 1,
                baseAccuracy = 100,
                utilityEffects = new List<UtilityEffectPayload>
                {
                    new UtilityEffectPayload
                    {
                        effectType = UtilityEffectType.BlockWindow,
                        magnitude = id == "blockade" ? 0.6f : 0.5f,
                        turnBudget = 2,
                        hitBudget = 1,
                        stackKey = id == "blockade" ? "wall_blockade" : "wall_ember",
                        contactFilter = filter,
                        chance = 1f,
                    },
                },
            };
        }

        private static MoveInstance DamageMove(string id)
        {
            MoveInstance move = new MoveInstance
            {
                instanceId = id,
                definitionId = id,
                displayName = id,
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                contactType = ContactType.Contact,
                apCost = 0,
                hitCount = 1,
                hitCountMax = 1,
                basePower = 45,
                baseAccuracy = 100,
                resolveProfile = new MoveResolveProfile(),
            };
            move.resolveProfile.accuracy.forceHit = true;
            move.resolveProfile.damage.useCustomRandomRollRange = true;
            move.resolveProfile.damage.randomRollMin = 1f;
            move.resolveProfile.damage.randomRollMax = 1f;
            return move;
        }
    }
}
