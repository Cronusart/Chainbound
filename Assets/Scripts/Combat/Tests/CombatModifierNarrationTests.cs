using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatModifierNarrationTests : CombatModifierResolverTestBase
    {
        [Test]
        public void ModifierOutcome_CarriesDefinitionDisplayName()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.1-b",
                ModifierCategory.TRG,
                "On hit: steal 1 AP",
                100f,
                100f,
                ModifierTriggerCondition.OnHit);
            definition.displayName = "AP Siphon";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 101);
            ChainlingInstance actor = CreateChainling("actor", 0, 30);
            ChainlingInstance target = CreateChainling("target", 3, 5);
            MoveInstance move = DamagingMove("siphon_strike");
            AddModifier(move, "6.1-b", ModifierCategory.TRG, 100f, 100f, ModifierTriggerCondition.OnHit);
            actor.moves.Add(move);

            TeamState opponent = CreateTeam(1, target);
            opponent.teamName = "Opponent";
            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), opponent);
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            ModifierOutcomePayload payload = AppliedPayload(resolver, "6.1-b");
            Assert.AreEqual("AP Siphon", payload.modifierName);
        }

        [Test]
        public void ModifierOutcome_LeavesNameEmptyWhenDefinitionHasNone()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.1-b",
                ModifierCategory.TRG,
                "On hit: steal 1 AP",
                100f,
                100f,
                ModifierTriggerCondition.OnHit);
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 103);
            ChainlingInstance actor = CreateChainling("actor", 0, 30);
            ChainlingInstance target = CreateChainling("target", 3, 5);
            MoveInstance move = DamagingMove("siphon_strike");
            AddModifier(move, "6.1-b", ModifierCategory.TRG, 100f, 100f, ModifierTriggerCondition.OnHit);
            actor.moves.Add(move);

            TeamState opponent = CreateTeam(1, target);
            opponent.teamName = "Opponent";
            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), opponent);
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            ModifierOutcomePayload payload = AppliedPayload(resolver, "6.1-b");
            Assert.IsTrue(string.IsNullOrEmpty(payload.modifierName));
        }

        [Test]
        public void PostCastBarrier_RegistersDisplayNameForWallNarration()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.7-a",
                ModifierCategory.TRG,
                "On hit: gain a barrier that blocks 1 hit",
                1f,
                1f,
                ModifierTriggerCondition.OnHit);
            definition.displayName = "Afterguard";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 107);
            ChainlingInstance actor = CreateChainling("actor", 0, 30);
            ChainlingInstance target = CreateChainling("target", 0, 5);
            MoveInstance move = DamagingMove("guard_strike");
            AddModifier(move, "6.7-a", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnHit);
            actor.moves.Add(move);

            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, target));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual("Afterguard", resolver.Log.Name("modifier.6.7-a"));
        }

        [Test]
        public void Execute_NotMet_NarratesNoLiveModifierLine()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.3-a",
                ModifierCategory.TRG,
                "If target HP is below 50%: increase power",
                35f,
                35f);
            definition.displayName = "Execute";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 109);
            ChainlingInstance actor = CreateChainling("actor", 0, 30);
            ChainlingInstance target = CreateChainling("target", 0, 5);
            MoveInstance move = DamagingMove("execution_test");
            AddModifier(move, "6.3-a", ModifierCategory.TRG, 35f, 35f);
            actor.moves.Add(move);
            TeamState opponent = CreateTeam(1, target);
            opponent.teamName = "Opponent";
            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), opponent);
            int from = resolver.Log.EventCount;

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(
                resolver.Log, from, resolver.Log.EventCount, null, 1, 0);

            Assert.That(LiveText(plan), Does.Not.Contain("Execute"));
            Assert.IsTrue(plan.transcriptEntries.Any(entry =>
                entry.kind == CombatNarrationPageKind.System &&
                entry.text == "Execute did not trigger: Enemy target was not below half HP."));
        }

        [Test]
        public void Execute_Met_PrefixesTheDamageLine()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.3-a",
                ModifierCategory.TRG,
                "If target HP is below 50%: increase power",
                35f,
                35f);
            definition.displayName = "Execute";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 113);
            ChainlingInstance actor = CreateChainling("actor", 0, 30);
            ChainlingInstance target = CreateChainling("target", 0, 5);
            target.currentHP = 40;
            MoveInstance move = DamagingMove("execution_test");
            AddModifier(move, "6.3-a", ModifierCategory.TRG, 35f, 35f);
            actor.moves.Add(move);
            TeamState opponent = CreateTeam(1, target);
            opponent.teamName = "Opponent";
            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), opponent);
            int from = resolver.Log.EventCount;

            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(
                resolver.Log, from, resolver.Log.EventCount, null, 1, 0);

            Assert.NotNull(plan.resolutionPage);
            StringAssert.StartsWith("Execute! Enemy target took", plan.resolutionPage.text);
        }

        [Test]
        public void PartingShot_NarratesWithOwnerAndRegisteredNames()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.6-b",
                ModifierCategory.TRG,
                "On escape: deal chip damage equal to 10% MaxHP",
                10f,
                10f,
                ModifierTriggerCondition.OnEscape);
            definition.displayName = "Parting Shot";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 127);

            ChainlingInstance lead = CreateChainling("lead", 2, 12);
            ChainlingInstance partner = CreateChainling("partner", 2, 10);
            ChainlingInstance bench = CreateChainling("bench", 2, 8);
            ChainlingInstance holder = CreateChainling("holder", 2, 15);
            ChainlingInstance holderPartner = CreateChainling("holder_partner", 2, 9);
            MoveInstance shot = DamagingMove("warning_shot", 1);
            shot.displayName = "Warning Shot";
            AddModifier(shot, "6.6-b", ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnEscape);
            holder.moves.Add(shot);

            TeamState opponent = CreateTeam(1, holder, holderPartner);
            opponent.teamName = "Opponent";
            CombatState state = resolver.InitializeBattle(CreateTeam(0, lead, partner, bench), opponent);
            int from = resolver.Log.EventCount;
            resolver.ExecuteTurn(state, CombatAction.Swap(0, 2), CombatAction.Pass(1));
            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(
                resolver.Log, from, resolver.Log.EventCount, null, 1, 0);

            Assert.NotNull(plan.resolutionPage);
            StringAssert.Contains("Enemy holder's Parting Shot dealt 10 damage to lead.", plan.resolutionPage.text);
            Assert.That(LiveText(plan), Does.Not.Contain("OnEscape").IgnoreCase);
        }

        [Test]
        public void CipherLockSwapBlock_UsesRegisteredNameAndReadableStat()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.4-c",
                ModifierCategory.TRG,
                "On swap in: gain +1 random stat stage",
                1f,
                1f,
                ModifierTriggerCondition.OnSwapIn);
            definition.displayName = "Flex In";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 131);
            ChainlingInstance lead = CreateChainling("lead", 2, 12);
            ChainlingInstance partner = CreateChainling("partner", 2, 10);
            ChainlingInstance bench = CreateChainling("bench", 2, 8);
            bench.statuses.Add(new StatusInstance(
                StatusId.CipherLocked,
                StatusClass.Duration,
                StatusOrigin.General,
                3,
                "test"));
            MoveInstance hosted = CreateMove("flex_host", MoveCategory.StatusUtility, MoveTargeting.Self, 0, 0, 100);
            AddModifier(hosted, "6.4-c", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnSwapIn);
            bench.moves.Add(hosted);
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, lead, partner, bench),
                CreateTeam(1, CreateChainling("foe", 0, 4)));

            resolver.ExecuteTurn(state, CombatAction.Swap(0, 2), CombatAction.Pass(1));

            CombatEvent blocked = resolver.Log.Events.Single(evt => evt.eventType == CombatEventType.BuffBlocked);
            StringAssert.Contains("bench", blocked.message);
            Assert.That(blocked.message, Does.Not.Contain("MeleeAtk"));
            Assert.That(blocked.message, Does.Not.Contain("MeleeDef"));
            Assert.That(blocked.message, Does.Not.Contain("RangedAtk"));
            Assert.That(blocked.message, Does.Not.Contain("RangedDef"));
        }

        [Test]
        public void PreEmptive_BattleStartOutcomeIsNarratedOnce()
        {
            ModifierDefinitionSO definition = Modifier(
                "6.8-b",
                ModifierCategory.TRG,
                "At battle start, chance to cast this move",
                100f,
                100f);
            definition.displayName = "Pre-emptive";
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(definition), 137);
            ChainlingInstance actor = CreateChainling("user", 3, 20);
            ChainlingInstance target = CreateChainling("foe", 0, 5);
            MoveInstance move = CreateMove("signal_cast", MoveCategory.Support, MoveTargeting.Self, 1, 0, 100);
            move.displayName = "Signal Cast";
            move.stageChanges.Add(new MoveStageChange
            {
                stat = CoreStat.MeleeAtk,
                stages = 1,
                chance = 1f,
                targetsSelf = true,
            });
            AddModifier(move, "6.8-b", ModifierCategory.TRG, 100f, 100f);
            actor.moves.Add(move);

            int beforeBattleStart = resolver.Log.EventCount;
            CombatState state = resolver.InitializeBattle(CreateTeam(0, actor), CreateTeam(1, target));
            int afterBattleStart = resolver.Log.EventCount;
            CombatNarrationActionPlan battleStart = CombatNarrationComposer.ComposeAction(
                resolver.Log, beforeBattleStart, afterBattleStart, null, 0, -1);

            StringAssert.Contains("user's Pre-emptive cast Signal Cast!", LiveText(battleStart));

            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Pass(1));
            CombatNarrationActionPlan firstTurn = CombatNarrationComposer.ComposeAction(
                resolver.Log, afterBattleStart, resolver.Log.EventCount, null, 1, 0);

            Assert.That(AllText(firstTurn), Does.Not.Contain("Pre-emptive"));
        }

        [Test]
        public void ThreeHitLeech_RepeatsSkipHoldInsideActiveNarrationRange()
        {
            var presenterObject = new GameObject("modifier-popup-repeat-test");
            try
            {
                Combat2BattlefieldPresenter presenter = presenterObject.AddComponent<Combat2BattlefieldPresenter>();
                var plan = new CombatNarrationActionPlan
                {
                    firstEventTimestamp = 10,
                    lastEventTimestamp = 12,
                };
                SetPrivateField(presenter, "_activeNarrationPlan", plan);
                MethodInfo method = typeof(Combat2BattlefieldPresenter).GetMethod(
                    "IsRepeatedModifierPopupInActiveNarration",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(method);
                ModifierOutcomePayload payload = ModifierOutcomePayload.Create(
                    "6.1-a",
                    ModifierCategory.TRG,
                    "HEAL",
                    "actor",
                    ModifierPopupMoment.OnHitApplied,
                    true,
                    "On-hit heal applied",
                    "actor",
                    "target",
                    "move",
                    "Leech",
                    3);

                bool firstRepeats = (bool)method.Invoke(presenter, new object[] { new CombatEvent { timestamp = 10 }, payload });
                bool secondRepeats = (bool)method.Invoke(presenter, new object[] { new CombatEvent { timestamp = 11 }, payload });
                bool thirdRepeats = (bool)method.Invoke(presenter, new object[] { new CombatEvent { timestamp = 12 }, payload });

                Assert.IsFalse(firstRepeats, "The first hit owns the modifier popup hold.");
                Assert.IsTrue(secondRepeats, "The second hit must not add another hold.");
                Assert.IsTrue(thirdRepeats, "The third hit must not add another hold.");
            }
            finally
            {
                Object.DestroyImmediate(presenterObject);
            }
        }

        private static MoveInstance DamagingMove(string id, int power = 20)
        {
            MoveInstance move = CreateMove(id, MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 0, power, 100);
            move.resolveProfile.crit.suppressCrits = true;
            return move;
        }

        private static ModifierOutcomePayload AppliedPayload(CombatResolver resolver, string sectionId)
        {
            CombatEvent evt = resolver.Log.Events.First(combatEvent =>
                combatEvent.eventType == CombatEventType.ModifierTriggered &&
                combatEvent.boolValue &&
                ModifierOutcomePayload.TryFromEvent(combatEvent, out ModifierOutcomePayload payload) &&
                payload.sectionId == sectionId);
            Assert.IsTrue(ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload result));
            return result;
        }

        private static string LiveText(CombatNarrationActionPlan plan)
        {
            if (plan == null)
                return string.Empty;
            return string.Join(" ", new[]
            {
                plan.actionPage != null ? plan.actionPage.text : string.Empty,
                plan.resolutionPage != null ? plan.resolutionPage.text : string.Empty,
                plan.consequencePage != null ? plan.consequencePage.text : string.Empty,
            });
        }

        private static string AllText(CombatNarrationActionPlan plan)
        {
            string transcript = plan == null
                ? string.Empty
                : string.Join(" ", plan.transcriptEntries.Select(entry => entry.text));
            return LiveText(plan) + " " + transcript;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
