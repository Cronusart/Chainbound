using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class BattleIntroVfxProfileTests
    {
        [Test]
        public void ResolverUsesExactVariantBeforeSpeciesAndCategoryProfiles()
        {
            var exact = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var species = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var boss = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var entangled = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var pvp = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var standard = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var presentation = ScriptableObject.CreateInstance<BattleIntroPresentationSO>();

            presentation.defaultVfxProfile = standard;
            presentation.bossVfxProfile = boss;
            presentation.entangledVfxProfile = entangled;
            presentation.pvpVfxProfile = pvp;
            presentation.vfxProfileOverrides.Add(new BattleIntroVfxProfileOverride
            {
                speciesId = "sparkpup",
                profile = species,
            });
            presentation.vfxProfileOverrides.Add(new BattleIntroVfxProfileOverride
            {
                speciesId = "sparkpup",
                visualVariantId = "chrome",
                profile = exact,
            });

            var request = new BattleIntroRequest
            {
                isBoss = true,
                isPvP = true,
                hasEntangledOpponent = true,
                leadOpponentSpeciesId = " SparkPup ",
                leadOpponentVisualVariantId = "CHROME",
                opponentSpeciesIds = new[] { "sparkpup" },
            };

            Assert.AreSame(exact, presentation.ResolveVfxProfile(request));

            Object.DestroyImmediate(exact);
            Object.DestroyImmediate(species);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(entangled);
            Object.DestroyImmediate(pvp);
            Object.DestroyImmediate(standard);
            Object.DestroyImmediate(presentation);
        }

        [Test]
        public void ResolverUsesSpeciesOverrideBeforeCategoryProfiles()
        {
            var species = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var boss = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var standard = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var presentation = ScriptableObject.CreateInstance<BattleIntroPresentationSO>();

            presentation.defaultVfxProfile = standard;
            presentation.bossVfxProfile = boss;
            presentation.vfxProfileOverrides.Add(new BattleIntroVfxProfileOverride
            {
                speciesId = "bytebat",
                profile = species,
            });

            var request = new BattleIntroRequest
            {
                isBoss = true,
                leadOpponentSpeciesId = "other",
                opponentSpeciesIds = new[] { "bytebat" },
            };

            Assert.AreSame(species, presentation.ResolveVfxProfile(request));

            Object.DestroyImmediate(species);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(standard);
            Object.DestroyImmediate(presentation);
        }

        [Test]
        public void ResolverUsesBossEntangledPvpDefaultCategoryOrder()
        {
            var boss = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var entangled = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var pvp = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var standard = ScriptableObject.CreateInstance<BattleIntroVfxProfileSO>();
            var presentation = ScriptableObject.CreateInstance<BattleIntroPresentationSO>();

            presentation.defaultVfxProfile = standard;
            presentation.bossVfxProfile = boss;
            presentation.entangledVfxProfile = entangled;
            presentation.pvpVfxProfile = pvp;

            Assert.AreSame(boss, presentation.ResolveVfxProfile(new BattleIntroRequest
            {
                isBoss = true,
                isPvP = true,
                hasEntangledOpponent = true,
            }));
            Assert.AreSame(entangled, presentation.ResolveVfxProfile(new BattleIntroRequest
            {
                isPvP = true,
                hasEntangledOpponent = true,
            }));
            Assert.AreSame(pvp, presentation.ResolveVfxProfile(new BattleIntroRequest
            {
                isPvP = true,
            }));
            Assert.AreSame(standard, presentation.ResolveVfxProfile(new BattleIntroRequest()));

            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(entangled);
            Object.DestroyImmediate(pvp);
            Object.DestroyImmediate(standard);
            Object.DestroyImmediate(presentation);
        }

        [Test]
        public void RequestContextExtractsLeadSpeciesActiveSpeciesAndEntangledFlag()
        {
            var state = new CombatState();
            state.team1 = new TeamState(1, "Wild");
            state.team1.chainlings.Add(CreateChainling("sparkpup", "Spark Pup", ChainlingVisualIdentity.BaseVariantId));
            state.team1.chainlings.Add(CreateChainling("bytebat", "Byte Bat", ChainlingVisualIdentity.BaseVariantId, isEntangled: true));
            state.team1.chainlings.Add(CreateChainling("benchling", "Benchling", ChainlingVisualIdentity.BaseVariantId));
            state.team1.activeIndices.Add(0);
            state.team1.activeIndices.Add(1);

            var request = new BattleIntroRequest();
            BattleIntroRequestContextUtility.ApplyOpponentContext(ref request, state);

            Assert.AreEqual(2, request.opponentActiveCount);
            Assert.AreEqual("sparkpup", request.leadOpponentSpeciesId);
            Assert.AreEqual(ChainlingVisualIdentity.BaseVariantId, request.leadOpponentVisualVariantId);
            CollectionAssert.AreEqual(new[] { "sparkpup", "bytebat" }, request.opponentSpeciesIds);
            Assert.IsTrue(request.hasEntangledOpponent);
        }

        [Test]
        public void PresentationUsesSpawnAdvanceDelayAsPlayerFallback()
        {
            var presentation = ScriptableObject.CreateInstance<BattleIntroPresentationSO>();
            presentation.spawnAdvanceDelaySeconds = 0.35f;
            presentation.playerSpawnAdvanceDelaySeconds = -1f;

            Assert.AreEqual(0.35f, presentation.ResolvePlayerSpawnAdvanceDelay(), 0.001f);

            Object.DestroyImmediate(presentation);
        }

        [Test]
        public void PresentationAllowsPlayerSpawnAdvanceOverride()
        {
            var presentation = ScriptableObject.CreateInstance<BattleIntroPresentationSO>();
            presentation.spawnAdvanceDelaySeconds = 0.35f;
            presentation.playerSpawnAdvanceDelaySeconds = 0.12f;

            Assert.AreEqual(0.12f, presentation.ResolvePlayerSpawnAdvanceDelay(), 0.001f);

            Object.DestroyImmediate(presentation);
        }

        private static ChainlingInstance CreateChainling(
            string speciesId,
            string displayName,
            string visualVariantId,
            bool isEntangled = false)
        {
            return new ChainlingInstance
            {
                speciesId = speciesId,
                displayName = displayName,
                isAlive = true,
                isEntangled = isEntangled,
                entangledType = isEntangled ? ChainlingType.Burned : ChainlingType.None,
                visualIdentity = new ChainlingVisualIdentity
                {
                    visualVariantId = visualVariantId,
                },
            };
        }
    }
}
