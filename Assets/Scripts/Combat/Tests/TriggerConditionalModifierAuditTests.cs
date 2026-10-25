using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    /// <summary>
    /// Fault sweep for trigger and conditional modifiers.
    /// Contract tests lock behavior that already matches the catalog.
    /// Known-fault tests assert the player-facing contract the resolver does
    /// not meet. Those failures are the handoff in
    /// Docs/Combat/Audits/trigger_conditional_modifier_audit.md.
    /// </summary>
    public class TriggerConditionalModifierContractTests : CombatModifierResolverTestBase
    {
        private sealed class Battle
        {
            public CombatResolver Resolver;
            public CombatState State;
            public ChainlingInstance User;
            public ChainlingInstance Foe;
            public ChainlingInstance Partner;
            public ChainlingInstance Second;
            public ChainlingInstance Lead;
            public ChainlingInstance Bench;
        }

        [Test]
        public void Leech_HealsOnALandedHit_AndDoesNotHealOnAMiss()
        {
            Battle landed = FightLeech(forceMiss: false, attackerHp: 40);
            Assert.Greater(landed.User.currentHP, 40);
            Assert.AreEqual(1, AppliedCount(landed, "6.1-a"));

            Battle missed = FightLeech(forceMiss: true, attackerHp: 40);
            Assert.AreEqual(40, missed.User.currentHP);
            Assert.AreEqual(0, AppliedCount(missed, "6.1-a"));
        }

        [Test]
        public void Leech_DoesNotHealWhenABarrierFullyBlocksTheHit_ButTheMoveStatusStillLands()
        {
            ModifierDefinitionSO leech = Modifier("6.1-a", ModifierCategory.TRG, "On hit: heal 10% of damage dealt", 10f, 10f, ModifierTriggerCondition.OnHit);
            Battle battle = Start("user", "foe", CreateModifierCatalog(leech), 4);
            battle.User.currentHP = 40;
            battle.Foe.AddBarrier(BarrierType.SingleHit, 1, 0, 2, "audit-wall");
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 30);
            strike.statusEffects.Add(new MoveStatusEffect
            {
                statusId = StatusId.Staggered,
                duration = 2,
                chance = 1f,
            });
            AddModifier(strike, "6.1-a", ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnHit);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(40, battle.User.currentHP, "A fully blocked hit currently skips on-hit leech.");
            Assert.AreEqual(0, AppliedCount(battle, "6.1-a"));
            Assert.IsTrue(battle.Foe.statuses.Exists(status => status.statusId == StatusId.Staggered && !status.IsExpired),
                "The same blocked hit still applies the move's status.");
        }

        [Test]
        public void MultiHitAndSpread_ProcOnHitOncePerLandedHit()
        {
            Battle multi = FightLeech(forceMiss: false, attackerHp: 20, hits: 2);
            Assert.AreEqual(2, AppliedCount(multi, "6.1-a"));

            ModifierDefinitionSO leech = Modifier("6.1-a", ModifierCategory.TRG, "On hit: heal 10% of damage dealt", 10f, 10f, ModifierTriggerCondition.OnHit);
            Battle spread = Start("user", "foe-a", CreateModifierCatalog(leech), 7);
            spread.Second = CreateChainling("foe-b", 1, 4);
            spread.State.team1.chainlings.Add(spread.Second);
            spread.User.currentHP = 20;
            MoveInstance wave = DamageMove("wave", MoveCategory.DamagingRanged, MoveTargeting.AllEnemies, 20);
            AddModifier(wave, "6.1-a", ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnHit);
            spread.User.moves.Add(wave);
            spread.State = spread.Resolver.InitializeBattle(spread.State.team0, spread.State.team1);
            spread.Resolver.ExecuteTurn(spread.State, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));
            Assert.AreEqual(2, AppliedCount(spread, "6.1-a"), "Each landed spread target is its own on-hit.");
        }

        [Test]
        public void ExecuteAndUnderdog_FollowCurrentHpThresholds()
        {
            int maxHp = CreateChainling("probe", 1, 1).EffectiveMaxHP;
            Assert.AreEqual(1, AppliedCount(FightExecute(maxHp / 2 - 1), "6.3-a"));
            Assert.AreEqual(0, AppliedCount(FightExecute(maxHp / 2), "6.3-a"));
            Assert.AreEqual(1, AppliedCount(FightUnderdog(30, 80), "6.3-b"));
            Assert.AreEqual(0, AppliedCount(FightUnderdog(80, 30), "6.3-b"));
        }

        [Test]
        public void GritGuard_ProtectsAfterTheCarrierActs_AndCoversTheFollowingTurn()
        {
            Battle fast = FightGrit(userSpeed: 80, foeSpeed: 5, withModifier: true);
            Battle fastBaseline = FightGrit(userSpeed: 80, foeSpeed: 5, withModifier: false);
            Assert.Greater(fast.User.currentHP, fastBaseline.User.currentHP,
                "A faster carrier should mitigate the later hit in the same turn.");

            Battle slow = FightGrit(userSpeed: 5, foeSpeed: 80, withModifier: true);
            Battle slowBaseline = FightGrit(userSpeed: 5, foeSpeed: 80, withModifier: false);
            Assert.AreEqual(1, AppliedCount(slow, "6.3-c"), "Grit still resolves after the carrier acts.");
            Assert.AreEqual(slowBaseline.User.currentHP, slow.User.currentHP,
                "The first hit landed before the slower carrier armed Grit Guard.");
            Assert.IsTrue(slow.User.mitigationSources.Exists(source => source != null && source.turnsRemaining > 0),
                "Grit Guard must survive into the following turn for a slower carrier.");

            int slowHpBefore = slow.User.currentHP;
            int baselineHpBefore = slowBaseline.User.currentHP;
            slow.Resolver.ExecuteTurn(slow.State, CombatAction.Pass(0), CombatAction.UseMove(1, 0));
            slowBaseline.Resolver.ExecuteTurn(slowBaseline.State, CombatAction.Pass(0), CombatAction.UseMove(1, 0));
            Assert.Less(slowHpBefore - slow.User.currentHP, baselineHpBefore - slowBaseline.User.currentHP,
                "The carried guard should mitigate the following turn's hit.");
            Assert.IsFalse(slow.User.mitigationSources.Exists(source => source != null && source.turnsRemaining > 0),
                "The row-specific two-turn budget expires after the following turn.");
        }

        [Test]
        public void PunishMiss_UsesLastTurnsMissLedger()
        {
            Assert.AreEqual(1, AppliedCount(FightPunishMiss(true), "6.3-e"));
            Assert.AreEqual(0, AppliedCount(FightPunishMiss(false), "6.3-e"));
        }

        [Test]
        public void RevengeSpark_BoostsAfterAnAllyFaints_NotAfterAnAllyScoresAKo()
        {
            Assert.AreEqual(1, AppliedCount(FightFallenAlly(), "6.3-d"));
            Assert.AreEqual(0, AppliedCount(FightScoredKo(), "6.3-d"));
        }

        [Test]
        public void CritWalletAndCritFeed_RequireACrit_AndShatterDropsTheContactedDefense()
        {
            Assert.AreEqual(1, AppliedCount(FightCrit("6.2-a", true), "6.2-a"));
            Assert.AreEqual(0, AppliedCount(FightCrit("6.2-a", false), "6.2-a"));

            Battle shatter = FightCrit("6.2-b", true);
            Assert.AreEqual(-2, shatter.Foe.GetStage(CoreStat.MeleeDef));
            Assert.AreEqual(0, shatter.Foe.GetStage(CoreStat.RangedDef));
            Assert.AreEqual("DEF -2", AppliedText(shatter, "6.2-b"));

            Battle feed = FightCrit("6.2-c", true);
            Assert.IsTrue(feed.User.statuses.Exists(status => status.statusId == StatusId.Surging && !status.IsExpired));
        }

        [Test]
        public void FinisherFeeAndMomentum_FireOnTheKillingMove()
        {
            Assert.AreEqual("KO AP", AppliedText(FightKo("6.5-a"), "6.5-a"));
            Battle momentum = FightKo("6.5-b");
            Assert.AreEqual(1, momentum.User.GetStage(CoreStat.Speed));
            Assert.AreEqual("SPEED +1", AppliedText(momentum, "6.5-b"));
        }

        [Test]
        public void PostCastBarrier_GrantsOnHit_SkipsAMiss_AndPreservesAnEqualBarrier()
        {
            Battle granted = FightBarrier(false, false);
            Assert.AreEqual(1, granted.User.barriers.Count);
            Assert.AreEqual("modifier.6.7-a", granted.User.barriers[0].sourceTag);

            Assert.AreEqual(0, FightBarrier(true, false).User.barriers.Count);

            Battle preserved = FightBarrier(false, true);
            Assert.AreEqual(1, preserved.User.barriers.Count);
            Assert.AreEqual("old-wall", preserved.User.barriers[0].sourceTag);
            Assert.AreEqual(0, AppliedCount(preserved, "6.7-a"));
        }

        [Test]
        public void Afterguard_StillBlocksOnTheFollowingTurn()
        {
            ModifierDefinitionSO guard = Modifier("6.7-a", ModifierCategory.TRG, "On hit: gain a 1-hit barrier for 2 turns", 1f, 1f, ModifierTriggerCondition.OnHit);
            Battle battle = Start("user", "foe", CreateModifierCatalog(guard), 11);
            battle.User.baseSpeed = 40;
            battle.Foe.baseSpeed = 10;
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(strike, "6.7-a", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnHit);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("jab", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 25));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            Assert.AreEqual(1, battle.User.barriers.Count, "A 2-turn barrier survives the granting turn's end tick.");
            int hp = battle.User.currentHP;
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.Pass(0), CombatAction.UseMove(1, 0));
            Assert.AreEqual(hp, battle.User.currentHP, "The carried barrier should absorb the next turn's hit.");
        }

        [Test]
        public void SwapInModifiers_HitTheIncomingMon_AndAllyModsRequireSupport()
        {
            Battle welcome = FightSwap("6.4-b", MoveCategory.DamagingMelee, ModifierTriggerCondition.OnSwapIn, "On swap in: +1 AP");
            Assert.AreEqual("+1 AP", AppliedText(welcome, "6.4-b"));

            Battle cover = FightSwap("6.4-d", MoveCategory.Support, ModifierTriggerCondition.OnSwapIn, "On swap in: ally gains mitigation");
            Assert.AreEqual(1, AppliedCount(cover, "6.4-d"));
            Assert.IsTrue(HadMitigationLog(cover, "partner"));

            Battle rejected = FightSwap("6.4-d", MoveCategory.DamagingMelee, ModifierTriggerCondition.OnSwapIn, "On swap in: ally gains mitigation");
            Assert.AreEqual(0, AppliedCount(rejected, "6.4-d"));
        }

        [Test]
        public void PartingShot_ChipsTheSwapper()
        {
            Battle battle = FightEscape("6.6-b", "On escape: deal chip damage equal to 10% MaxHP");
            Assert.Less(battle.Lead.currentHP, battle.Lead.EffectiveMaxHP);
            Assert.AreEqual(1, AppliedCount(battle, "6.6-b"));
        }

        [Test]
        public void CleanseAndSpotlight_ResolveOnUse()
        {
            Battle cleanse = FightOnUse("6.8-f", MoveCategory.StatusUtility, true);
            Assert.IsFalse(cleanse.User.statuses.Exists(status => status.statusId == StatusId.Staggered && !status.IsExpired));
            Assert.AreEqual(1, AppliedCount(cleanse, "6.8-f"));

            Battle spotlight = FightOnUse("6.8-g", MoveCategory.Support, false);
            Assert.AreEqual(1, AppliedCount(spotlight, "6.8-g"));
        }

        [Test]
        public void PreEmptive_CastsASupportHostAtBattleStart_AndDoesNotRepeatNextTurn()
        {
            Battle battle = FightBattleStart(MoveCategory.Support);
            Assert.AreEqual(1, battle.User.GetStage(CoreStat.MeleeAtk));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.Pass(0), CombatAction.Pass(1));
            Assert.AreEqual(1, battle.User.GetStage(CoreStat.MeleeAtk));
        }

        [Test]
        public void PreEmptive_RefusesADamagingHost()
        {
            Battle battle = FightBattleStart(MoveCategory.DamagingMelee);
            Assert.AreEqual(battle.Foe.EffectiveMaxHP, battle.Foe.currentHP);
            Assert.AreEqual(0, battle.User.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(0, AppliedCount(battle, "6.8-b"));
        }

        [Test]
        public void TagTeamAndPileOn_RequireAnEarlierActionThisTurn()
        {
            Assert.AreEqual(1, AppliedCount(FightFollowUp("7.2-a", true), "7.2-a"));
            Assert.AreEqual(0, AppliedCount(FightFollowUp("7.2-a", false), "7.2-a"));
            Assert.AreEqual(1, AppliedCount(FightFollowUp("7.2-b", true), "7.2-b"));
            Assert.AreEqual(0, AppliedCount(FightFollowUp("7.2-b", false), "7.2-b"));
        }

        private Battle FightLeech(bool forceMiss, int attackerHp, int hits = 1)
        {
            ModifierDefinitionSO leech = Modifier("6.1-a", ModifierCategory.TRG, "On hit: heal 10% of damage dealt", 10f, 10f, ModifierTriggerCondition.OnHit);
            Battle battle = Start("user", "foe", CreateModifierCatalog(leech), 3);
            battle.User.currentHP = attackerHp;
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 30);
            strike.hitCount = hits;
            strike.hitCountMax = hits;
            if (forceMiss)
                strike.resolveProfile.accuracy = new MoveAccuracyTuning { forceMiss = true };
            AddModifier(strike, "6.1-a", ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnHit);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightExecute(int targetHp)
        {
            ModifierDefinitionSO execute = Modifier("6.3-a", ModifierCategory.TRG, "If target HP < 50%: +35% power", 35f, 35f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(execute), 5);
            battle.Foe.currentHP = targetHp;
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(strike, "6.3-a", ModifierCategory.TRG, 35f, 35f);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightUnderdog(int attackerHp, int targetHp)
        {
            ModifierDefinitionSO underdog = Modifier("6.3-b", ModifierCategory.TRG, "If your HP < target HP: +20% power", 20f, 20f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(underdog), 5);
            battle.User.currentHP = attackerHp;
            battle.Foe.currentHP = targetHp;
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(strike, "6.3-b", ModifierCategory.TRG, 20f, 20f);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightGrit(int userSpeed, int foeSpeed, bool withModifier)
        {
            ModifierDefinitionSO grit = Modifier("6.3-c", ModifierCategory.TRG, "When HP < 30%: gain mitigation for the rest of this turn", 50f, 50f, ModifierTriggerCondition.OnLowHP);
            Battle battle = Start("user", "foe", CreateModifierCatalog(grit), 9);
            battle.User.baseMaxHP = 300;
            battle.User.currentHP = 80;
            battle.User.baseSpeed = userSpeed;
            battle.Foe.baseSpeed = foeSpeed;
            MoveInstance brace = CreateMove("brace", MoveCategory.StatusUtility, MoveTargeting.Self, 1, 0, 0);
            if (withModifier)
                AddModifier(brace, "6.3-c", ModifierCategory.TRG, 50f, 50f, ModifierTriggerCondition.OnLowHP);
            battle.User.moves.Add(brace);
            battle.Foe.moves.Add(DamageMove("jab", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 30));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.UseMove(1, 0));
            return battle;
        }

        private Battle FightPunishMiss(bool targetMissedLastTurn)
        {
            ModifierDefinitionSO punish = Modifier("6.3-e", ModifierCategory.TRG, "If target missed last turn: +20% accuracy and +15% power", 15f, 15f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(punish), 13);
            MoveInstance probe = DamageMove("probe", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(probe, "6.3-e", ModifierCategory.TRG, 15f, 15f);
            battle.User.moves.Add(probe);
            MoveInstance whiff = DamageMove("whiff", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 10);
            whiff.resolveProfile.accuracy = new MoveAccuracyTuning { forceMiss = targetMissedLastTurn };
            battle.Foe.moves.Add(whiff);
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.Pass(0), CombatAction.UseMove(1, 0));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightFallenAlly()
        {
            ModifierDefinitionSO spark = Modifier("6.3-d", ModifierCategory.TRG, "If an ally KO'd this turn: +25% power", 25f, 25f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(spark), 17);
            battle.User.baseSpeed = 5;
            battle.Partner = CreateChainling("partner", 3, 4);
            battle.Partner.currentHP = 1;
            battle.State.team0.chainlings.Add(battle.Partner);
            battle.Foe.baseSpeed = 40;
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(strike, "6.3-d", ModifierCategory.TRG, 25f, 25f);
            battle.User.moves.Add(strike);
            battle.Partner.moves.Add(DamageMove("poke", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 10));
            battle.Foe.moves.Add(DamageMove("crush", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 80));
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            battle.Resolver.ExecuteTurn2v2(battle.State,
                new List<CombatAction> { CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(0) },
                new List<CombatAction> { CombatAction.UseMove(1, 0, 0, 1) });
            return battle;
        }

        private Battle FightScoredKo()
        {
            ModifierDefinitionSO spark = Modifier("6.3-d", ModifierCategory.TRG, "If an ally KO'd this turn: +25% power", 25f, 25f);
            Battle battle = Start("user", "fodder", CreateModifierCatalog(spark), 18);
            battle.User.baseSpeed = 5;
            battle.Partner = CreateChainling("partner", 3, 40);
            battle.State.team0.chainlings.Add(battle.Partner);
            battle.Foe.currentHP = 1;
            battle.Second = CreateChainling("survivor", 1, 4);
            battle.State.team1.chainlings.Add(battle.Second);
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(strike, "6.3-d", ModifierCategory.TRG, 25f, 25f);
            battle.User.moves.Add(strike);
            battle.Partner.moves.Add(DamageMove("finish", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 40));
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Second.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            battle.Resolver.ExecuteTurn2v2(battle.State,
                new List<CombatAction>
                {
                    CombatAction.UseMove(0, 0, 1, 1),
                    CombatAction.UseMove(0, 0, 1, 0),
                },
                new List<CombatAction> { CombatAction.Pass(1), CombatAction.Pass(1) });
            return battle;
        }

        private Battle FightCrit(string section, bool forceCrit)
        {
            string description = section == "6.2-b"
                ? "On crit: target -2 Def stages"
                : section == "6.2-c"
                    ? "On crit: your next move deals +25% power"
                    : "On crit: gain 1 AP";
            ModifierDefinitionSO mod = Modifier(section, ModifierCategory.TRG, description, 1f, 1f, ModifierTriggerCondition.OnCrit);
            Battle battle = Start("user", "foe", CreateModifierCatalog(mod), 23);
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            strike.resolveProfile.crit = new MoveCritTuning { forceCrit = forceCrit, suppressCrits = !forceCrit };
            AddModifier(strike, section, ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnCrit);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightKo(string section)
        {
            string description = section == "6.5-b" ? "On KO gain +1 Speed stage" : "On KO restore 1 AP";
            ModifierDefinitionSO mod = Modifier(section, ModifierCategory.TRG, description, 1f, 1f, ModifierTriggerCondition.OnKO);
            Battle battle = Start("user", "foe", CreateModifierCatalog(mod), 29);
            battle.Foe.currentHP = 1;
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 40);
            AddModifier(strike, section, ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnKO);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightBarrier(bool forceMiss, bool preexisting)
        {
            ModifierDefinitionSO guard = Modifier("6.7-a", ModifierCategory.TRG, "On hit: gain a 1-hit barrier", 1f, 1f, ModifierTriggerCondition.OnHit);
            Battle battle = Start("user", "foe", CreateModifierCatalog(guard), 31);
            if (preexisting)
                battle.User.AddBarrier(BarrierType.SingleHit, 1, 0, 2, "old-wall");
            MoveInstance strike = DamageMove("strike", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            if (forceMiss)
                strike.resolveProfile.accuracy = new MoveAccuracyTuning { forceMiss = true };
            AddModifier(strike, "6.7-a", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnHit);
            battle.User.moves.Add(strike);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightSwap(string section, MoveCategory category, ModifierTriggerCondition trigger, string description)
        {
            ModifierDefinitionSO mod = Modifier(section, ModifierCategory.TRG, description, 20f, 20f, trigger);
            mod.allowedCategories = category == MoveCategory.Support
                ? new[] { MoveCategory.Support }
                : Array.Empty<MoveCategory>();
            Battle battle = Start("lead", "foe", CreateModifierCatalog(mod), 37);
            battle.Lead = battle.User;
            battle.Partner = CreateChainling("partner", 2, 10);
            battle.Bench = CreateChainling("bench", 2, 10);
            battle.State.team0.chainlings.Add(battle.Partner);
            battle.State.team0.chainlings.Add(battle.Bench);
            battle.Second = CreateChainling("foe-b", 1, 4);
            battle.State.team1.chainlings.Add(battle.Second);
            MoveInstance hosted = CreateMove("hosted", category, MoveTargeting.Self, 0, 0, 0);
            AddModifier(hosted, section, ModifierCategory.TRG, 20f, 20f, trigger);
            battle.Bench.moves.Add(hosted);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.Swap(0, 2), CombatAction.Pass(1));
            return battle;
        }

        private Battle FightEscape(string section, string description)
        {
            ModifierDefinitionSO mod = Modifier(section, ModifierCategory.TRG, description, 10f, 10f, ModifierTriggerCondition.OnEscape);
            Battle battle = Start("holder", "lead", CreateModifierCatalog(mod), 41);
            battle.Lead = battle.Foe;
            battle.Partner = CreateChainling("partner", 2, 8);
            battle.Bench = CreateChainling("bench", 2, 8);
            battle.State.team1.chainlings.Add(battle.Partner);
            battle.State.team1.chainlings.Add(battle.Bench);
            battle.Second = CreateChainling("holder-b", 2, 8);
            battle.State.team0.chainlings.Add(battle.Second);
            MoveInstance trap = DamageMove("trap", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1);
            AddModifier(trap, section, ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnEscape);
            battle.User.moves.Add(trap);
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            battle.Resolver.ExecuteTurn(battle.State, CombatAction.Pass(0), CombatAction.Swap(1, 2));
            return battle;
        }

        private Battle FightOnUse(string section, MoveCategory category, bool debuff)
        {
            string description = section == "6.8-f"
                ? "On use: chance to clear 1 self debuff"
                : "On use: ally gains accuracy";
            ModifierDefinitionSO mod = Modifier(section, ModifierCategory.TRG, description, 100f, 100f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(mod), 43);
            battle.Partner = CreateChainling("ally", 3, 10);
            battle.State.team0.chainlings.Add(battle.Partner);
            if (debuff)
            {
                battle.User.statuses.Add(new StatusInstance(
                    StatusId.Staggered, StatusClass.Duration, StatusOrigin.General, 2, "audit"));
            }
            MoveInstance move = CreateMove("use", category, category == MoveCategory.Support ? MoveTargeting.SingleAlly : MoveTargeting.Self, 1, 0, 0);
            AddModifier(move, section, ModifierCategory.TRG, 100f, 100f);
            battle.User.moves.Add(move);
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            CombatAction action = category == MoveCategory.Support
                ? CombatAction.UseMove(0, 0, 0, 1)
                : CombatAction.UseMove(0, 0);
            battle.Resolver.ExecuteTurn(battle.State, action, CombatAction.Pass(1));
            return battle;
        }

        private Battle FightBattleStart(MoveCategory category)
        {
            ModifierDefinitionSO mod = Modifier("6.8-b", ModifierCategory.TRG, "At battle start, chance to cast this move", 100f, 100f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(mod), 47);
            MoveInstance move = CreateMove("hosted", category, MoveTargeting.Self, 1, 10, 100);
            move.stageChanges.Add(new MoveStageChange
            {
                stat = CoreStat.MeleeAtk,
                stages = 1,
                chance = 1f,
                targetsSelf = true,
            });
            AddModifier(move, "6.8-b", ModifierCategory.TRG, 100f, 100f);
            battle.User.moves.Add(move);
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            return battle;
        }

        private Battle FightFollowUp(string section, bool earlierAction)
        {
            string description = section == "7.2-b"
                ? "If target was hit earlier this turn: +45% power"
                : "If an ally used a damaging move this turn: +20% power";
            ModifierDefinitionSO mod = Modifier(section, ModifierCategory.SYN, description, 20f, 20f);
            Battle battle = Start("user", "foe", CreateModifierCatalog(mod), 53);
            battle.User.baseSpeed = 5;
            battle.Partner = CreateChainling("partner", 3, 40);
            battle.State.team0.chainlings.Add(battle.Partner);
            battle.Second = CreateChainling("foe-b", 1, 4);
            battle.State.team1.chainlings.Add(battle.Second);
            MoveInstance finisher = DamageMove("finisher", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 20);
            AddModifier(finisher, section, ModifierCategory.SYN, 20f, 20f);
            battle.User.moves.Add(finisher);
            battle.Partner.moves.Add(DamageMove("setup", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 10));
            battle.Foe.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.Second.moves.Add(DamageMove("wait", MoveCategory.DamagingMelee, MoveTargeting.SingleEnemy, 1));
            battle.State = battle.Resolver.InitializeBattle(battle.State.team0, battle.State.team1);
            CombatAction partnerAction = earlierAction
                ? CombatAction.UseMove(0, 0, 1, 0)
                : CombatAction.Pass(0);
            battle.Resolver.ExecuteTurn2v2(battle.State,
                new List<CombatAction> { CombatAction.UseMove(0, 0, 1, 0), partnerAction },
                new List<CombatAction> { CombatAction.Pass(1), CombatAction.Pass(1) });
            return battle;
        }

        private Battle Start(string userId, string foeId, ModifierCatalogSO catalog, int seed)
        {
            CombatResolver resolver = CreateResolver(catalog, seed);
            ChainlingInstance user = CreateChainling(userId, 4, 20);
            ChainlingInstance foe = CreateChainling(foeId, 4, 8);
            return new Battle
            {
                Resolver = resolver,
                User = user,
                Foe = foe,
                State = resolver.InitializeBattle(CreateTeam(0, user), CreateTeam(1, foe)),
            };
        }

        private static MoveInstance DamageMove(string id, MoveCategory category, MoveTargeting targeting, int power)
        {
            MoveInstance move = CreateMove(id, category, targeting, 1, power, 0);
            move.resolveProfile.crit = new MoveCritTuning { suppressCrits = true };
            return move;
        }

        private static int AppliedCount(Battle battle, string section)
        {
            int count = 0;
            IReadOnlyList<CombatEvent> events = battle.Resolver.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                CombatEvent evt = events[i];
                if (evt.eventType != CombatEventType.ModifierTriggered || !evt.boolValue)
                    continue;
                if (ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload payload) &&
                    string.Equals(payload.sectionId, section, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private static string AppliedText(Battle battle, string section)
        {
            IReadOnlyList<CombatEvent> events = battle.Resolver.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                CombatEvent evt = events[i];
                if (evt.eventType != CombatEventType.ModifierTriggered || !evt.boolValue)
                    continue;
                if (ModifierOutcomePayload.TryFromEvent(evt, out ModifierOutcomePayload payload) &&
                    string.Equals(payload.sectionId, section, StringComparison.OrdinalIgnoreCase))
                    return payload.displayText;
            }

            Assert.Fail("No applied modifier outcome for " + section);
            return string.Empty;
        }

        private static bool HadMitigationLog(Battle battle, string name)
        {
            IReadOnlyList<CombatEvent> events = battle.Resolver.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                string message = events[i].message;
                if (message != null &&
                    message.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0 &&
                    message.IndexOf("mitigation", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }
    }

    public class TriggerConditionalModifierKnownFaultTests : CombatModifierResolverTestBase
    {
        [Test]
        public void Execute_PopupNamesTheConditional()
        {
            ModifierDefinitionSO execute = Modifier("6.3-a", ModifierCategory.TRG, "If target HP < 50%: +35% power", 35f, 35f);
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(execute), 5);
            ChainlingInstance user = CreateChainling("user", 4, 20);
            ChainlingInstance foe = CreateChainling("foe", 1, 5);
            foe.currentHP = Math.Max(1, foe.EffectiveMaxHP / 2 - 1);
            MoveInstance strike = new MoveInstance
            {
                instanceId = "strike",
                displayName = "strike",
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                basePower = 20,
                apCost = 1,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
            };
            strike.resolveProfile.crit = new MoveCritTuning { suppressCrits = true };
            AddModifier(strike, "6.3-a", ModifierCategory.TRG, 35f, 35f);
            user.moves.Add(strike);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, user), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual("EXECUTE", AppliedText(resolver, "6.3-a"));
        }

        [Test]
        public void Leech_AtFullHpDoesNotClaimAHeal()
        {
            ModifierDefinitionSO leech = Modifier("6.1-a", ModifierCategory.TRG, "On hit: heal 10% of damage dealt", 10f, 10f, ModifierTriggerCondition.OnHit);
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(leech), 3);
            ChainlingInstance user = CreateChainling("user", 4, 20);
            user.currentHP = user.EffectiveMaxHP;
            ChainlingInstance foe = CreateChainling("foe", 1, 5);
            MoveInstance strike = new MoveInstance
            {
                instanceId = "strike",
                displayName = "strike",
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                basePower = 20,
                apCost = 1,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
            };
            strike.resolveProfile.crit = new MoveCritTuning { suppressCrits = true };
            AddModifier(strike, "6.1-a", ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnHit);
            user.moves.Add(strike);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, user), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(user.EffectiveMaxHP, user.currentHP);
            Assert.AreEqual(0, AppliedCount(resolver, "6.1-a"), "A heal of 0 must not report that the stamp applied.");
        }

        [Test]
        public void ApSiphon_UsesTheStampedChanceRatherThanTheFlatTriggerChance()
        {
            ModifierDefinitionSO siphon = Modifier("6.1-b", ModifierCategory.TRG, "On hit: 15-25% chance to drain 1 AP", 100f, 100f, ModifierTriggerCondition.OnHit);
            siphon.triggerChance = 0.2f;
            ModifierCatalogSO catalog = CreateModifierCatalog(siphon);
            int procs = 0;
            for (int seed = 1; seed <= 24; seed++)
            {
                CombatResolver resolver = CreateResolver(catalog, seed);
                ChainlingInstance user = CreateChainling("user", 4, 20);
                ChainlingInstance foe = CreateChainling("foe", 4, 5);
                MoveInstance strike = new MoveInstance
                {
                    instanceId = "strike",
                    displayName = "strike",
                    category = MoveCategory.DamagingMelee,
                    targeting = MoveTargeting.SingleEnemy,
                    basePower = 20,
                    apCost = 1,
                    baseAccuracy = 0,
                    hitCount = 1,
                    hitCountMax = 1,
                };
                strike.resolveProfile.crit = new MoveCritTuning { suppressCrits = true };
                AddModifier(strike, "6.1-b", ModifierCategory.TRG, 100f, 100f, ModifierTriggerCondition.OnHit);
                strike.modifiers[0].effectValueLocked = true;
                user.moves.Add(strike);
                CombatState state = resolver.InitializeBattle(CreateTeam(0, user), CreateTeam(1, foe));
                resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));
                procs += AppliedCount(resolver, "6.1-b");
            }

            Assert.AreEqual(24, procs, "The locked 100% stamp was ignored. The proc used TriggerChance 0.2.");
        }

        [Test]
        public void CritShatter_AtTheStageFloorDoesNotClaimADrop()
        {
            ModifierDefinitionSO shatter = Modifier("6.2-b", ModifierCategory.TRG, "On crit: target -2 Def stages", 2f, 2f, ModifierTriggerCondition.OnCrit);
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(shatter), 23);
            ChainlingInstance user = CreateChainling("user", 4, 20);
            ChainlingInstance foe = CreateChainling("foe", 1, 5);
            foe.SetStage(CoreStat.MeleeDef, -6);
            MoveInstance strike = new MoveInstance
            {
                instanceId = "strike",
                displayName = "strike",
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                basePower = 20,
                apCost = 1,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
            };
            strike.resolveProfile.crit = new MoveCritTuning { forceCrit = true };
            AddModifier(strike, "6.2-b", ModifierCategory.TRG, 2f, 2f, ModifierTriggerCondition.OnCrit);
            user.moves.Add(strike);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, user), CreateTeam(1, foe));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(-6, foe.GetStage(CoreStat.MeleeDef));
            Assert.AreEqual(0, AppliedCount(resolver, "6.2-b"), "No stage changed, so the stamp must not report DEF -2.");
        }

        [Test]
        public void Execute_DoesNotLeakOntoAHealthySpreadTarget()
        {
            ModifierDefinitionSO execute = Modifier("6.3-a", ModifierCategory.TRG, "If target HP < 50%: +35% power", 35f, 35f);
            ModifierCatalogSO catalog = CreateModifierCatalog(execute);
            int woundedSpread = SpreadPartnerHp(catalog, primaryHpFraction: 0.25f);
            int healthySpread = SpreadPartnerHp(catalog, primaryHpFraction: 1f);
            Assert.AreEqual(healthySpread, woundedSpread,
                "Execute was resolved once for the selected target and then reused on every spread target.");
        }

        [Test]
        public void SharedBuff_RespectsCipherLock()
        {
            ModifierDefinitionSO share = Modifier("7.3-a", ModifierCategory.SYN, "Self-buffs also hit one ally", 0f, 0f);
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(share), 61);
            ChainlingInstance user = CreateChainling("user", 4, 20);
            ChainlingInstance ally = CreateChainling("ally", 4, 20);
            ally.statuses.Add(new StatusInstance(StatusId.CipherLocked, StatusClass.Duration, StatusOrigin.General, 3, "audit"));
            MoveInstance cheer = CreateMove("cheer", MoveCategory.Support, MoveTargeting.Self, 1, 0, 0);
            cheer.stageChanges.Add(new MoveStageChange
            {
                stat = CoreStat.MeleeAtk,
                stages = 1,
                chance = 1f,
                targetsSelf = true,
            });
            AddModifier(cheer, "7.3-a", ModifierCategory.SYN, 0f, 0f);
            user.moves.Add(cheer);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, user, ally), CreateTeam(1, CreateChainling("foe", 1, 4)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0), CombatAction.Pass(1));

            Assert.AreEqual(1, user.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(0, ally.GetStage(CoreStat.MeleeAtk), "Cipher Lock blocked the copied buff.");
        }

        [Test]
        public void HypePass_RespectsCipherLock()
        {
            ModifierDefinitionSO hype = Modifier("7.1-c", ModifierCategory.SYN, "Support move also grants +1 random stat stage", 1f, 1f);
            CombatResolver resolver = CreateResolver(CreateModifierCatalog(hype), 67);
            ChainlingInstance user = CreateChainling("user", 4, 20);
            ChainlingInstance ally = CreateChainling("ally", 4, 20);
            ally.statuses.Add(new StatusInstance(StatusId.CipherLocked, StatusClass.Duration, StatusOrigin.General, 3, "audit"));
            MoveInstance cheer = CreateMove("cheer", MoveCategory.Support, MoveTargeting.SingleAlly, 1, 0, 0);
            AddModifier(cheer, "7.1-c", ModifierCategory.SYN, 1f, 1f);
            user.moves.Add(cheer);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, user, ally), CreateTeam(1, CreateChainling("foe", 1, 4)));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 0, 1), CombatAction.Pass(1));

            Assert.AreEqual(0, ally.GetStage(CoreStat.MeleeAtk));
            Assert.AreEqual(0, ally.GetStage(CoreStat.MeleeDef));
            Assert.AreEqual(0, ally.GetStage(CoreStat.RangedAtk));
            Assert.AreEqual(0, ally.GetStage(CoreStat.RangedDef));
            Assert.AreEqual(0, ally.GetStage(CoreStat.Speed));
        }

        [Test]
        public void EscapeTriggers_OnSeparateMovesBothApply()
        {
            ModifierDefinitionSO slow = Modifier("6.6-a", ModifierCategory.TRG, "On escape: apply Slow", 1f, 1f, ModifierTriggerCondition.OnEscape);
            ModifierDefinitionSO chip = Modifier("6.6-b", ModifierCategory.TRG, "On escape: deal chip damage equal to 10% MaxHP", 10f, 10f, ModifierTriggerCondition.OnEscape);
            var status = CreateAsset<StatusDefinitionSO>();
            status.statusId = StatusId.Slowed;
            status.displayName = "Slowed";
            status.isDebuff = true;
            status.origin = StatusOrigin.General;
            var catalog = CreateAsset<StatusCatalogSO>();
            catalog.definitions.Add(status);
            catalog.BuildLookup();
            ModifierCatalogSO modifiers = CreateModifierCatalog(slow, chip);
            var matrix = CreateAsset<TypeChemistryMatrixSO>();
            CombatResolver resolver = new CombatResolver(matrix, catalog, 41, true, modifiers);

            ChainlingInstance holder = CreateChainling("holder", 2, 10);
            MoveInstance trip = new MoveInstance
            {
                instanceId = "trip",
                displayName = "trip",
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                basePower = 1,
                apCost = 0,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
            };
            AddModifier(trip, "6.6-a", ModifierCategory.TRG, 1f, 1f, ModifierTriggerCondition.OnEscape);
            MoveInstance shot = new MoveInstance
            {
                instanceId = "shot",
                displayName = "shot",
                category = MoveCategory.DamagingMelee,
                targeting = MoveTargeting.SingleEnemy,
                basePower = 1,
                apCost = 0,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
            };
            AddModifier(shot, "6.6-b", ModifierCategory.TRG, 10f, 10f, ModifierTriggerCondition.OnEscape);
            holder.moves.Add(trip);
            holder.moves.Add(shot);
            ChainlingInstance lead = CreateChainling("lead", 2, 8);
            int before = lead.currentHP;
            CombatState state = resolver.InitializeBattle(
                CreateTeam(0, holder, CreateChainling("holder-b", 2, 8)),
                CreateTeam(1, lead, CreateChainling("partner", 2, 8), CreateChainling("bench", 2, 8)));
            resolver.ExecuteTurn(state, CombatAction.Pass(0), CombatAction.Swap(1, 2));

            Assert.Less(lead.currentHP, before, "Parting Shot on the second move should still chip.");
            Assert.IsTrue(lead.statuses.Exists(statusInstance => statusInstance.statusId == StatusId.Slowed && !statusInstance.IsExpired),
                "Tripwire on the first move should still slow.");
        }

        private int SpreadPartnerHp(ModifierCatalogSO catalog, float primaryHpFraction)
        {
            CombatResolver resolver = CreateResolver(catalog, 71);
            ChainlingInstance user = CreateChainling("user", 4, 30);
            ChainlingInstance primary = CreateChainling("primary", 1, 4);
            ChainlingInstance partner = CreateChainling("partner", 1, 4);
            primary.baseMaxHP = 400;
            partner.baseMaxHP = 400;
            primary.currentHP = Math.Max(1, (int)(primary.EffectiveMaxHP * primaryHpFraction));
            partner.currentHP = partner.EffectiveMaxHP;
            MoveInstance wave = new MoveInstance
            {
                instanceId = "wave",
                displayName = "wave",
                category = MoveCategory.DamagingRanged,
                targeting = MoveTargeting.AllEnemies,
                basePower = 15,
                apCost = 1,
                baseAccuracy = 0,
                hitCount = 1,
                hitCountMax = 1,
            };
            wave.resolveProfile.crit = new MoveCritTuning { suppressCrits = true };
            AddModifier(wave, "6.3-a", ModifierCategory.TRG, 35f, 35f);
            user.moves.Add(wave);
            CombatState state = resolver.InitializeBattle(CreateTeam(0, user), CreateTeam(1, primary, partner));
            resolver.ExecuteTurn(state, CombatAction.UseMove(0, 0, 1, 0), CombatAction.Pass(1));
            Assert.IsTrue(partner.isAlive, "The healthy spread target has to survive so the damage can be compared.");
            if (primaryHpFraction < 0.5f)
                Assert.Greater(AppliedCount(resolver, "6.3-a"), 0, "Execute did not arm against the wounded primary.");
            return partner.currentHP;
        }

        private static int AppliedCount(CombatResolver resolver, string section)
        {
            int count = 0;
            IReadOnlyList<CombatEvent> events = resolver.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].eventType != CombatEventType.ModifierTriggered || !events[i].boolValue)
                    continue;
                if (ModifierOutcomePayload.TryFromEvent(events[i], out ModifierOutcomePayload payload) &&
                    string.Equals(payload.sectionId, section, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        private static string AppliedText(CombatResolver resolver, string section)
        {
            IReadOnlyList<CombatEvent> events = resolver.Log.Events;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].eventType != CombatEventType.ModifierTriggered || !events[i].boolValue)
                    continue;
                if (ModifierOutcomePayload.TryFromEvent(events[i], out ModifierOutcomePayload payload) &&
                    string.Equals(payload.sectionId, section, StringComparison.OrdinalIgnoreCase))
                    return payload.displayText;
            }

            Assert.Fail("No applied modifier outcome for " + section);
            return string.Empty;
        }
    }
}
