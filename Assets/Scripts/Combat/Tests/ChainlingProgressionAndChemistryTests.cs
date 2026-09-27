using NUnit.Framework;

namespace Chainbound.Combat.Tests
{
    public sealed class ChainlingProgressionAndChemistryTests
    {
        [Test]
        public void StatScaleMultiplier_IsAlwaysOne_UnderCycleFlats()
        {
            Assert.AreEqual(1f, ChainlingProgressionUtility.GetStatScaleMultiplier(0, 0), 0.0001f);
            Assert.AreEqual(1f, ChainlingProgressionUtility.GetStatScaleMultiplier(3, 0), 0.0001f);
            Assert.AreEqual(1f, ChainlingProgressionUtility.GetStatScaleMultiplier(5, 3), 0.0001f);
        }

        [Test]
        public void CycleAutoFlat_AddsOneCombatAndFivePercentMaxHpPerRebirth()
        {
            Assert.AreEqual(0, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MeleeAtk, 0));
            Assert.AreEqual(1, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MeleeAtk, 1));
            Assert.AreEqual(3, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.Speed, 3));
            Assert.AreEqual(5, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, 1, 100));
            Assert.AreEqual(15, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, 3, 100));
            Assert.AreEqual(18, ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, 3, 120));
        }

        [Test]
        public void CyclePickFlat_AppliesOneCombatOrFivePercentMaxHpPerAllocationPoint()
        {
            int[] bonuses = { 1, 0, 2, 0, 0, 0 };
            Assert.AreEqual(5, ChainlingProgressionUtility.GetCyclePickFlat(CoreStat.MaxHP, bonuses, 100));
            Assert.AreEqual(0, ChainlingProgressionUtility.GetCyclePickFlat(CoreStat.MeleeAtk, bonuses));
            Assert.AreEqual(2, ChainlingProgressionUtility.GetCyclePickFlat(CoreStat.MeleeDef, bonuses));
        }

        [Test]
        public void ComputeProgressedStat_Cycle1_AutoPlusMaxHpPick()
        {
            // Cycle 1 auto: +5% of 100; pick MaxHP: +5% of 100; base 100 + IV 0 => 110
            int[] pickMaxHp = { 1, 0, 0, 0, 0, 0 };
            int hp = ChainlingProgressionUtility.ComputeProgressedStat(
                100, 0, CoreStat.MaxHP, rebirthCount: 1, pickMaxHp);
            Assert.AreEqual(110, hp);

            // Combat pick MATK: auto +1, pick +1
            int[] pickMatk = { 0, 1, 0, 0, 0, 0 };
            int matk = ChainlingProgressionUtility.ComputeProgressedStat(
                50, 2, CoreStat.MeleeAtk, rebirthCount: 1, pickMatk);
            Assert.AreEqual(54, matk);
        }

        [Test]
        public void ChainlingInstance_UsesCycleFlats_NotStarScale()
        {
            var instance = CombatFactory.CreateTestChainling("Test", ChainlingType.Burned, stars: 5, rebirthCount: 1, hp: 100, mAtk: 50);
            instance.rebirthIvBonuses = new[] { 0, 1, 0, 0, 0, 0 };
            instance.ivMeleeAtk = 0;

            // Stars 5 must not multiply: 50 + 0 + auto1 + pick1 = 52
            Assert.AreEqual(52, instance.GetBaseStat(CoreStat.MeleeAtk));
            Assert.AreEqual(105, instance.EffectiveMaxHP); // 100 + 5% auto
            Assert.AreEqual(1f, instance.StatScaleMultiplier, 0.0001f);
        }

        [Test]
        public void ResolveSnapshotProgression_MigratesLegacyLevelWhenStarsUnset()
        {
            ChainlingProgressionUtility.ResolveSnapshotProgression(0, 0, 45, out int stars, out int rebirth);
            Assert.AreEqual(2, stars);
            Assert.AreEqual(0, rebirth);
        }

        [Test]
        public void ResolveSnapshotProgression_PrefersExplicitStars()
        {
            ChainlingProgressionUtility.ResolveSnapshotProgression(4, 1, 99, out int stars, out int rebirth);
            Assert.AreEqual(4, stars);
            Assert.AreEqual(1, rebirth);
        }

        [Test]
        public void ProgressionDamageFactor_IsFlatOneByDefault()
        {
            Assert.AreEqual(1f, DamageCalculator.ResolveProgressionDamageFactor(null), 0.0001f);
            Assert.AreEqual(1f, DamageCalculator.ResolveProgressionDamageFactor(new MoveDamageFormulaTuning()), 0.0001f);
        }

        [Test]
        public void ProgressionDamageFactor_HonorsMoveOverride()
        {
            var tuning = new MoveDamageFormulaTuning
            {
                useCustomProgressionDamageFactor = true,
                customProgressionDamageFactor = 1.25f,
            };

            Assert.AreEqual(1.25f, DamageCalculator.ResolveProgressionDamageFactor(tuning), 0.0001f);
        }

        [Test]
        public void ChemistryDamageMultiplier_UsesOnePointTwoAndZeroPointEight()
        {
            Assert.AreEqual(1f, Multiplier(ChemistryRelation.Neutral), 0.0001f);
            Assert.AreEqual(1.2f, Multiplier(ChemistryRelation.Advantage), 0.0001f);
            Assert.AreEqual(0.8f, Multiplier(ChemistryRelation.Resisted), 0.0001f);
        }

        [Test]
        public void ProgressionTieBreakScore_FavorsStarsThenRebirth()
        {
            int low = ChainlingProgressionUtility.GetProgressionTieBreakScore(1, 0);
            int highStars = ChainlingProgressionUtility.GetProgressionTieBreakScore(2, 0);
            int highRebirth = ChainlingProgressionUtility.GetProgressionTieBreakScore(1, 1);

            Assert.Greater(highStars, low);
            Assert.Greater(highRebirth, low);
        }

        private static float Multiplier(ChemistryRelation relation)
        {
            var result = new ChemistryResult
            {
                relation = relation,
            };
            return result.DamageMultiplier;
        }
    }
}
