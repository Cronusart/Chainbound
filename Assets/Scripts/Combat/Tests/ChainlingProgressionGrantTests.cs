using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class ChainlingProgressionGrantTests
    {
        [Test]
        public void MythicSignature_FloorsAtOneBeforeFiveStars_AndUsesStarFiveWeight()
        {
            ProgressionConfigSO config = ScriptableObject.CreateInstance<ProgressionConfigSO>();
            config.moveLearnRarityWeights = new[] { 1f, 0.72f, 0.48f, 0.28f, 0.14f, 0.06f };

            MoveDefinitionSO signature = CreateMove("mythic_sig", "Mythic Sig");
            signature.rarity = RarityTier.Mythic;
            MoveDefinitionSO common = CreateMove("baseline_jab", "Baseline Jab");
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "balloom";
            species.progressionMovePool.Add(new SpeciesProgressionMoveEntry
            {
                move = signature,
                weight = SpeciesProgressionPoolWeights.Signature,
                weightAtStarFive = SpeciesProgressionPoolWeights.SignatureAtStarFive,
                useWeightAtStarFive = true,
                isSignature = true,
                poolTag = ProgressionPoolTag.Learnable,
                maxStar = CombatConstants.STAR_MAX,
            });
            species.progressionMovePool.Add(new SpeciesProgressionMoveEntry
            {
                move = common,
                weight = SpeciesProgressionPoolWeights.Standard,
                poolTag = ProgressionPoolTag.Learnable,
                maxStar = CombatConstants.STAR_MAX,
            });

            ChainlingProgressionResolvedPool beforeFive = ChainlingProgressionPoolResolver.Resolve(
                species,
                4,
                false,
                null,
                null,
                config);
            ChainlingProgressionResolvedPool atFive = ChainlingProgressionPoolResolver.Resolve(
                species,
                CombatConstants.STAR_MAX,
                false,
                null,
                null,
                config);

            Assert.NotNull(Find(beforeFive, "mythic_sig"));
            Assert.AreEqual(1, Find(beforeFive, "mythic_sig").effectiveWeight);
            Assert.AreEqual(SpeciesProgressionPoolWeights.Standard, Find(beforeFive, "baseline_jab").effectiveWeight);
            Assert.AreEqual(1, Find(atFive, "mythic_sig").effectiveWeight);

            config.moveLearnRarityWeights[5] = 0.2f;
            ChainlingProgressionResolvedPool earlyPath = ChainlingProgressionPoolResolver.Resolve(
                species,
                4,
                false,
                null,
                null,
                config);
            ChainlingProgressionResolvedPool starFivePath = ChainlingProgressionPoolResolver.Resolve(
                species,
                CombatConstants.STAR_MAX,
                false,
                null,
                null,
                config);

            Assert.AreEqual(2, Find(earlyPath, "mythic_sig").effectiveWeight);
            Assert.AreEqual(5, Find(starFivePath, "mythic_sig").effectiveWeight);
        }

        [Test]
        public void PoolResolver_AppliesEventMultiplierAndGateBonus()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            MoveDefinitionSO shot = CreateMove("baseline_shot", "Baseline Shot");
            SpeciesDefinitionSO species = CreateSpecies(jab, shot);
            ChainlingProgressionEventCatalogSO catalog = ScriptableObject.CreateInstance<ChainlingProgressionEventCatalogSO>();
            ChainlingProgressionEventSO progressionEvent = ScriptableObject.CreateInstance<ChainlingProgressionEventSO>();
            progressionEvent.enabled = true;
            progressionEvent.eventId = "test_event";
            progressionEvent.patches.Add(new SpeciesProgressionEventPatch
            {
                speciesId = "validape",
                starGateBonusPercent = 7,
                weightMultipliers = new List<WeightMultiplierPatch>
                {
                    new WeightMultiplierPatch { moveId = "baseline_shot", multiplier = 3f },
                },
            });
            catalog.events.Add(progressionEvent);

            ChainlingProgressionResolvedPool pool = ChainlingProgressionPoolResolver.Resolve(
                species,
                2,
                false,
                null,
                catalog);

            Assert.AreEqual(7, pool.starGateBonusPercent);
            Assert.AreEqual(40, pool.TotalWeight);
            Assert.AreEqual(30, Find(pool, "baseline_shot").effectiveWeight);
        }

        [Test]
        public void PreviewUtility_UsesGateTimesNormalizedWeight()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            MoveDefinitionSO shot = CreateMove("baseline_shot", "Baseline Shot");
            SpeciesDefinitionSO species = CreateSpecies(jab, shot);
            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };

            float percent = MoveLearnPreviewUtility.GetMoveLearnPercent(
                species,
                0,
                "baseline_jab",
                false,
                tables,
                null);

            Assert.AreEqual(50f, percent, 0.001f);
        }

        [Test]
        public void PreviewUtility_UsesMasteryGateAtMaxStar()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            MoveDefinitionSO shot = CreateMove("baseline_shot", "Baseline Shot");
            SpeciesDefinitionSO species = CreateSpecies(jab, shot);
            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.masteryKnownMoveChanceStandard = 40;
            tables.starKnownMoveChanceStandard = new[] { 10, 20, 30, 40, 50 };

            int masteryGate = MoveLearnPreviewUtility.GetMasteryGatePercent(species, false, tables, null);
            List<MoveLearnPreviewRow> rows = MoveLearnPreviewUtility.BuildPreviewRows(
                species,
                CombatConstants.STAR_MAX,
                false,
                tables,
                null);

            Assert.AreEqual(40, masteryGate);
            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(20f, rows[0].learnPercent, 0.001f);
        }

        [Test]
        public void BuildSummaryPreview_UsesMasterySuffixAtMaxStar()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            SpeciesDefinitionSO species = CreateSpecies(jab);
            MoveLearnSummaryPreview summary = MoveLearnPreviewUtility.BuildSummaryPreview(
                species,
                CombatConstants.STAR_MAX,
                false,
                ChainlingProgressionRewardTablesSO.CreateRuntimeDefault(),
                null);

            Assert.IsTrue(summary.isMastery);
            Assert.AreEqual("to mastery", summary.xpMilestoneSuffix);
            Assert.AreEqual("Mastery", summary.gateLabel);
        }

        [Test]
        public async Task GrantService_ExcludesOwnedMovesWhenConfigured()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            MoveDefinitionSO shot = CreateMove("baseline_shot", "Baseline Shot");
            SpeciesDefinitionSO species = CreateSpecies(jab, shot);
            species.progressionMovePool[0].excludeIfOwned = true;
            species.progressionMovePool[1].excludeIfOwned = true;
            SpeciesDatabaseSO speciesDatabase = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            speciesDatabase.species.Add(species);
            speciesDatabase.BuildLookup();

            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };
            var persistence = new FakeProgressionGrantPersistence();
            var service = new StarProgressionGrantService(speciesDatabase, tables, null, persistence);
            var result = new BattleResult { battleType = BattleType.PvE, seed = 999 };
            var summary = new BattleRewardSummary
            {
                chainlingRewards = new List<ChainlingXpReward>
                {
                    new ChainlingXpReward
                    {
                        instanceId = "c1",
                        backendChainlingId = "chainling-1",
                        speciesId = "validape",
                        xpEarned = 100,
                        starsCrossed = new List<int> { 1 },
                        knownMoveIds = new List<string> { "baseline_jab" },
                    },
                },
            };

            List<ProgressionGrantResult> grants = await service.ApplyGrantsAsync(result, summary, "profile", "wallet", "auth");

            Assert.AreEqual(1, grants.Count);
            Assert.IsTrue(grants[0].AwardedMove);
            Assert.AreEqual("baseline_shot", grants[0].moveId);
        }

        [Test]
        public void PreviewUtility_OwnedMovesRemainEligibleAndReportCopyCount()
        {
            MoveDefinitionSO exclusive = CreateMove("starter_exclusive", "Starter Exclusive");
            SpeciesDefinitionSO species = CreateSpecies(exclusive);
            species.progressionMovePool[0].poolTag = ProgressionPoolTag.Exclusive;
            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };
            var owned = new List<string> { exclusive.moveId };
            var counts = new Dictionary<string, int> { { exclusive.moveId, 2 } };

            List<MoveLearnPreviewRow> rows = MoveLearnPreviewUtility.BuildPreviewRows(
                species, 0, false, tables, null, owned, counts);

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(100f, rows[0].learnPercent, 0.001f);
            Assert.IsTrue(rows[0].learned);
            Assert.AreEqual(2, rows[0].ownedCopyCount);
            Assert.AreEqual(ProgressionPoolTag.Exclusive, rows[0].poolTag);
        }

        [Test]
        public async Task GrantService_DistinctMilestonesCanAwardDuplicateInventoryCopies()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            SpeciesDefinitionSO species = CreateSpecies(jab);
            SpeciesDatabaseSO speciesDatabase = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            speciesDatabase.species.Add(species);
            speciesDatabase.BuildLookup();
            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };
            var persistence = new FakeProgressionGrantPersistence();
            var service = new StarProgressionGrantService(speciesDatabase, tables, null, persistence);
            var result = new BattleResult { battleType = BattleType.PvE, seed = 444 };
            var summary = new BattleRewardSummary
            {
                chainlingRewards = new List<ChainlingXpReward>
                {
                    new ChainlingXpReward
                    {
                        instanceId = "c1",
                        backendChainlingId = "chainling-1",
                        speciesId = "validape",
                        xpEarned = 100,
                        starsCrossed = new List<int> { 1, 2 },
                        knownMoveIds = new List<string> { jab.moveId },
                    },
                },
            };

            List<ProgressionGrantResult> grants = await service.ApplyGrantsAsync(result, summary, "profile", "wallet", "auth");

            Assert.AreEqual(2, grants.Count);
            Assert.AreEqual("move-item-1", grants[0].moveItemId);
            Assert.AreEqual("move-item-2", grants[1].moveItemId);
            Assert.AreEqual(1, grants[0].ownedCopyCount);
            Assert.AreEqual(2, grants[1].ownedCopyCount);
            Assert.AreNotEqual(grants[0].idempotencyKey, grants[1].idempotencyKey);
        }

        [Test]
        public async Task GrantService_ZeroPercentGateRecordsNoFolioWithoutPersistence()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            SpeciesDefinitionSO species = CreateSpecies(jab);
            SpeciesDatabaseSO speciesDatabase = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            speciesDatabase.species.Add(species);
            speciesDatabase.BuildLookup();
            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.starKnownMoveChanceStandard = new[] { 0, 0, 0, 0, 0 };
            var persistence = new FakeProgressionGrantPersistence();
            var service = new StarProgressionGrantService(speciesDatabase, tables, null, persistence);
            var result = new BattleResult { battleType = BattleType.PvE, seed = 77 };
            var summary = new BattleRewardSummary
            {
                chainlingRewards = new List<ChainlingXpReward>
                {
                    new ChainlingXpReward
                    {
                        backendChainlingId = "chainling-1",
                        speciesId = "validape",
                        xpEarned = 1,
                        starsCrossed = new List<int> { 1 },
                    },
                },
            };

            List<ProgressionGrantResult> grants = await service.ApplyGrantsAsync(result, summary, "profile", "wallet", "auth");

            Assert.AreEqual(ProgressionGrantOutcome.NoFolioIssued, grants[0].outcome);
            Assert.IsFalse(grants[0].AwardedMove);
            Assert.AreEqual(0, persistence.Requests.Count);
        }

        [Test]
        public void RewardCalculator_DerivesStarsCrossedFromMasteryXp()
        {
            PostBattleRewardsConfigSO rewards = PostBattleRewardsConfigSO.CreateRuntimeDefault();
            ProgressionConfigSO progression = ProgressionConfigSO.CreateRuntimeDefault();
            var calculator = new StandardBattleRewardCalculator(rewards, progression);
            var result = new BattleResult
            {
                winnerTeamIndex = 0,
                totalEnemyKOs = 39,
                encounterDifficulty = EncounterDifficulty.Standard,
                battleType = BattleType.PvE,
                playerParticipants = new List<BattleParticipant>
                {
                    new BattleParticipant
                    {
                        instanceId = "c1",
                        backendChainlingId = "chainling-1",
                        speciesId = "validape",
                        masteryXp = 440,
                        stars = 0,
                        rebirthCount = 0,
                        wasActive = true,
                    },
                },
            };

            BattleRewardSummary summary = calculator.Calculate(result);

            Assert.AreEqual(1, summary.chainlingRewards.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, summary.chainlingRewards[0].starsCrossed);
            Assert.AreEqual(0, summary.chainlingRewards[0].previousStars);
            Assert.AreEqual(2, summary.chainlingRewards[0].newStars);
        }

        [Test]
        public async Task GrantService_PersistsSuccessfulStarRolls()
        {
            MoveDefinitionSO jab = CreateMove("baseline_jab", "Baseline Jab");
            SpeciesDefinitionSO species = CreateSpecies(jab);
            SpeciesDatabaseSO speciesDatabase = ScriptableObject.CreateInstance<SpeciesDatabaseSO>();
            speciesDatabase.species.Add(species);
            speciesDatabase.BuildLookup();

            ChainlingProgressionRewardTablesSO tables = ChainlingProgressionRewardTablesSO.CreateRuntimeDefault();
            tables.starKnownMoveChanceStandard = new[] { 100, 100, 100, 100, 100 };
            var persistence = new FakeProgressionGrantPersistence();
            var service = new StarProgressionGrantService(speciesDatabase, tables, null, persistence);
            var result = new BattleResult { battleType = BattleType.PvE, seed = 123 };
            var summary = new BattleRewardSummary
            {
                chainlingRewards = new List<ChainlingXpReward>
                {
                    new ChainlingXpReward
                    {
                        instanceId = "c1",
                        backendChainlingId = "chainling-1",
                        speciesId = "validape",
                        xpEarned = 100,
                        starsCrossed = new List<int> { 1, 2 },
                    },
                },
            };

            List<ProgressionGrantResult> grants = await service.ApplyGrantsAsync(result, summary, "profile", "wallet", "auth");

            Assert.AreEqual(2, grants.Count);
            Assert.AreEqual(2, persistence.Requests.Count);
            Assert.IsTrue(grants[0].AwardedMove);
            Assert.AreEqual("baseline_jab", grants[0].moveId);
        }

        private static SpeciesDefinitionSO CreateSpecies(params MoveDefinitionSO[] moves)
        {
            SpeciesDefinitionSO species = ScriptableObject.CreateInstance<SpeciesDefinitionSO>();
            species.speciesId = "validape";
            species.displayName = "Validape";
            species.progressionMovePool.Clear();
            for (int i = 0; i < moves.Length; i++)
            {
                species.progressionMovePool.Add(new SpeciesProgressionMoveEntry
                {
                    move = moves[i],
                    weight = 10,
                    poolTag = ProgressionPoolTag.Learnable,
                    maxStar = CombatConstants.STAR_MAX,
                    excludeIfOwned = false,
                });
            }

            return species;
        }

        private static MoveDefinitionSO CreateMove(string moveId, string displayName)
        {
            MoveDefinitionSO move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            move.moveId = moveId;
            move.displayName = displayName;
            move.baseType = ChainlingType.Meme;
            move.rarity = RarityTier.Common;
            return move;
        }

        private static ResolvedProgressionMoveEntry Find(ChainlingProgressionResolvedPool pool, string moveId)
        {
            for (int i = 0; i < pool.entries.Count; i++)
            {
                if (pool.entries[i].moveId == moveId)
                    return pool.entries[i];
            }

            return null;
        }

        private sealed class FakeProgressionGrantPersistence : IProgressionGrantPersistence
        {
            public readonly List<ProgressionGrantRequest> Requests = new List<ProgressionGrantRequest>();
            public bool IsAvailable => true;

            public Task<ProgressionGrantPersistenceResult> GrantProgressionKnownMoveAsync(ProgressionGrantRequest request)
            {
                Requests.Add(request);
                return Task.FromResult(new ProgressionGrantPersistenceResult
                {
                    success = true,
                    moveItemId = $"move-item-{Requests.Count}",
                    moveId = request.moveId,
                    ownedCopyCount = Requests.Count,
                });
            }
        }
    }
}
