using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class LeverBoardSystemTests
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
        public void ResolverConfigHash_IsDeterministic_AndChangesWhenFieldChanges()
        {
            var a = CreateAsset<CombatResolverConfigSO>();
            a.ResetToDefaults();
            var b = CreateAsset<CombatResolverConfigSO>();
            b.ResetToDefaults();

            string hashA = a.ComputeConfigHash();
            string hashB = b.ComputeConfigHash();
            Assert.AreEqual(hashA, hashB);
            Assert.IsFalse(string.IsNullOrEmpty(hashA));

            a.apPerTurn = a.apPerTurn + 1;
            Assert.AreNotEqual(hashA, a.ComputeConfigHash());
        }

        [Test]
        public void ProgressionCycleGrants_UsePercentAndFlatFromConfig()
        {
            var config = CreateAsset<ProgressionConfigSO>();
            config.ResetToDefaults();
            config.cycleAutoHpPercent = 0.05f;
            config.cycleAutoHpFlat = 0;
            config.cycleAutoStatFlat = 1;
            config.cycleAutoStatPercent = 0f;

            Assert.AreEqual(
                10,
                ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MaxHP, rebirthCount: 2, speciesBase: 100, config));

            Assert.AreEqual(
                2,
                ChainlingProgressionUtility.GetCycleAutoFlat(CoreStat.MeleeAtk, rebirthCount: 2, speciesBase: 100, config));
        }

        [Test]
        public void VendingRarityThresholds_Economy01_EndAtBasisPoints_AndCommonIsLargest()
        {
            var config = CreateAsset<VendingConfigSO>();
            config.ResetToDefaults();

            int[] thresholds = config.BuildCumulativeThresholds();
            Assert.AreEqual(6, thresholds.Length);
            Assert.AreEqual(VendingConfigSO.RollBasisPoints, thresholds[5]);

            int largestSlice = 0;
            int largestIndex = -1;
            int previous = 0;
            for (int i = 0; i < thresholds.Length; i++)
            {
                int slice = thresholds[i] - previous;
                if (slice > largestSlice)
                {
                    largestSlice = slice;
                    largestIndex = i;
                }

                previous = thresholds[i];
            }

            Assert.AreEqual(0, largestIndex); // Common
            Assert.Greater(largestSlice, 0);
        }

        [Test]
        public void ChainlingRecovery_Defaults_UseBoundedTenPercentTicks_AndHashAllLevers()
        {
            var config = ChainlingRecoveryConfigSO.CreateRuntimeDefault();
            _createdAssets.Add(config);

            Assert.IsTrue(config.enabled);
            Assert.AreEqual(1800, config.intervalSeconds);
            Assert.AreEqual(1000, config.HealBasisPoints);
            Assert.AreEqual(48, config.maxCatchupIntervals);
            Assert.IsTrue(config.storageOnly);
            Assert.AreEqual(11, config.ResolveHealPerInterval(101)); // ceil(10.1)

            string hash = config.ComputeConfigHash();
            config.maxCatchupIntervals = 24;
            Assert.AreNotEqual(hash, config.ComputeConfigHash());
        }

        [Test]
        public void ChainlingRecovery_DisabledOrZeroRate_DoesNotProduceHealing()
        {
            var config = ChainlingRecoveryConfigSO.CreateRuntimeDefault();
            _createdAssets.Add(config);

            config.enabled = false;
            Assert.AreEqual(0, config.ResolveHealPerInterval(100));

            config.enabled = true;
            config.maxHpFractionPerInterval = 0f;
            Assert.AreEqual(0, config.ResolveHealPerInterval(100));
        }

        [Test]
        public void ModifierBalanceOverlay_ResolveMethods_OverrideEffectRangeAndWeight()
        {
            var definition = CreateAsset<ModifierDefinitionSO>();
            definition.sectionId = "2.1-a";
            definition.effectMin = 5f;
            definition.effectMax = 8f;
            definition.weight = 1;

            var overlay = CreateAsset<ModifierBalanceOverlaySO>();
            overlay.entries.Add(new ModifierBalanceOverlayEntry
            {
                sectionId = "2.1-a",
                overrideEffectRange = true,
                effectMin = 12f,
                effectMax = 18f,
                overrideWeight = true,
                weight = 7,
            });

            Assert.AreEqual(12f, overlay.ResolveEffectMin(definition), 0.0001f);
            Assert.AreEqual(18f, overlay.ResolveEffectMax(definition), 0.0001f);
            Assert.AreEqual(7, overlay.ResolveWeight(definition));
        }

        [Test]
        public void RulesetEligibility_BannedMoveRejects_AndAllowedTypeFilterWorks()
        {
            var ruleset = CreateAsset<CombatRulesetSO>();
            ruleset.bannedMoveIds = new List<string> { "banned_jab" };
            ruleset.allowedTypes = new List<ChainlingType> { ChainlingType.Node };

            Assert.IsFalse(ruleset.IsChainlingEligible(
                ChainlingType.Node,
                ChainlingType.None,
                new[] { "banned_jab" },
                out string bannedReason));
            Assert.IsTrue(bannedReason.StartsWith("banned_move:"));

            Assert.IsFalse(ruleset.IsChainlingEligible(
                ChainlingType.Burned,
                ChainlingType.None,
                new[] { "safe_move" },
                out string typeReason));
            Assert.AreEqual("type_filter", typeReason);

            Assert.IsTrue(ruleset.IsChainlingEligible(
                ChainlingType.Node,
                ChainlingType.None,
                new[] { "safe_move" },
                out string okReason));
            Assert.IsNull(okReason);
        }

        [Test]
        public void PostBattleRewards_CreateRuntimeDefault_DisablesCapturedAndDefeatRewards()
        {
            var rewards = PostBattleRewardsConfigSO.CreateRuntimeDefault();
            _createdAssets.Add(rewards);

            Assert.IsFalse(rewards.capturedReceivesXp);
            Assert.IsFalse(rewards.defeatGrantsRewards);
            Assert.IsTrue(rewards.defeatRevivesKoChainlings);
            Assert.AreEqual(0.5f, rewards.defeatKoRespawnHpFraction);
            Assert.AreEqual(25, rewards.ramBoss);
            Assert.AreEqual(0, rewards.koXpBoss);
            Assert.AreEqual(100, rewards.completionBonusBoss);
        }

        [Test]
        public void RewardCalculator_BossVictory_PaysFlatRamAndXp()
        {
            PostBattleRewardsConfigSO rewards = PostBattleRewardsConfigSO.CreateRuntimeDefault();
            ProgressionConfigSO progression = ProgressionConfigSO.CreateRuntimeDefault();
            _createdAssets.Add(rewards);
            _createdAssets.Add(progression);
            var calculator = new StandardBattleRewardCalculator(rewards, progression);
            var result = new BattleResult
            {
                winnerTeamIndex = 0,
                totalEnemyKOs = 2,
                encounterDifficulty = EncounterDifficulty.Boss,
                battleType = BattleType.PvE,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant
                    {
                        instanceId = "c1",
                        backendChainlingId = "chainling-1",
                        speciesId = "validape",
                        wasActive = true,
                    },
                },
            };

            BattleRewardSummary summary = calculator.Calculate(result);

            Assert.AreEqual(25, summary.ramEarned);
            Assert.AreEqual(1, summary.chainlingRewards.Count);
            Assert.AreEqual(100, summary.chainlingRewards[0].xpEarned);
        }

        [Test]
        public void BattleIntroCopy_CreateRuntimeDefault_UsesStackedWildTemplate()
        {
            var config = BattleIntroCopyConfigSO.CreateRuntimeDefault();
            _createdAssets.Add(config);

            Assert.IsTrue(config.wildSingleTemplate.Contains("{subject}"));
            Assert.AreEqual(4, config.traitOrder.Count);
            Assert.AreEqual(BattleIntroTraitSlot.Mutated, config.traitOrder[0]);
            Assert.AreEqual(BattleIntroTraitSlot.Variant, config.traitOrder[3]);
            Assert.AreEqual("mutated", config.mutatedStyle.label);
            Assert.AreEqual("variant", config.variantStyle.label);
        }

        [Test]
        public void ResolverConfig_CreateRuntimeDefault_MatchesCombatConstants()
        {
            var config = CombatResolverConfigSO.CreateRuntimeDefault();
            _createdAssets.Add(config);

            Assert.AreEqual(CombatConstants.AP_PER_TURN, config.apPerTurn);
            Assert.AreEqual(CombatConstants.AP_CAP, config.apCap);
            Assert.AreEqual(CombatConstants.RANDOM_ROLL_MIN, config.randomRollMin, 0.0001f);
            Assert.AreEqual(CombatConstants.RANDOM_ROLL_MAX, config.randomRollMax, 0.0001f);
            Assert.AreEqual(CombatConstants.CRIT_MULTIPLIER_DEFAULT, config.critMultiplierDefault, 0.0001f);
            Assert.AreEqual(CombatConstants.PROGRESSION_DAMAGE_FACTOR, config.progressionDamageFactor, 0.0001f);
            Assert.AreEqual(CombatConstants.CHEMISTRY_ADVANTAGE_FACTOR, config.chemistryAdvantageFactor, 0.0001f);
            Assert.AreEqual(CombatConstants.CHEMISTRY_DISADVANTAGE_FACTOR, config.chemistryDisadvantageFactor, 0.0001f);
            Assert.AreEqual(CombatConstants.STAB_MULTIPLIER, config.stabMultiplier, 0.0001f);
            Assert.AreEqual(CombatConstants.STAGE_MIN, config.stageMin);
            Assert.AreEqual(CombatConstants.STAGE_MAX, config.stageMax);
            Assert.AreEqual(CombatConstants.STAGE_ACTIVE_CHANGE_CAP, config.stageActiveChangeCap);
            Assert.AreEqual(CombatConstants.MITIGATION_SINGLE_SOURCE_CAP, config.mitigationSingleSourceCap, 0.0001f);
            Assert.AreEqual(CombatConstants.MITIGATION_FLOOR, config.mitigationFloor, 0.0001f);
            Assert.AreEqual(CombatConstants.EXCLUSIVE_BARRIERS_AND_WALLS, config.exclusiveBarriersAndWalls);
            Assert.AreEqual(CombatConstants.MAX_PASSIVE_MAX_HP_BONUS_PERCENT, config.maxPassiveMaxHpBonusPercent, 0.0001f);
            Assert.AreEqual(CombatConstants.REKT_LOSE_ACTION_CHANCE, config.rektLoseActionChance, 0.0001f);
            Assert.AreEqual(CombatConstants.GLITCHED_MIND_MISACT_CHANCE, config.glitchedMindMisactChance, 0.0001f);
            Assert.IsFalse(config.enableStab);
            Assert.AreEqual(0f, config.turnActionTimeLimitSeconds, 0.0001f);
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }
    }
}
