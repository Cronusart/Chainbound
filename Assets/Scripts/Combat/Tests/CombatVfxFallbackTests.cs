using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatVfxFallbackTests
    {
        private readonly List<UnityEngine.Object> _cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _cleanup.Count - 1; i >= 0; i--)
            {
                if (_cleanup[i] != null)
                    UnityEngine.Object.DestroyImmediate(_cleanup[i]);
            }
            _cleanup.Clear();
        }

        [Test]
        public void Resolve_ExplicitEntry_BeatsAllFallbacks()
        {
            var exact = UsableEntry("exact");
            exact.moveDefinitionId = "move";
            var fallback = NewFallbacks();
            fallback.SetTyped(ChainlingType.Node, MoveCategory.DamagingMelee, UsableEntry("typed"));

            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("move", MoveCategory.DamagingMelee, ChainlingType.Node, MoveVfxRole.Damage),
                new[] { exact },
                fallback);
            Assert.That(result.entry, Is.SameAs(exact));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Explicit));
        }

        [Test]
        public void Resolve_LegacyCategoryFallback_StillHonoured()
        {
            var legacy = UsableEntry("legacy");
            legacy.isCategoryFallback = true;
            legacy.fallbackCategory = MoveCategory.Support;
            var fallback = NewFallbacks();
            fallback.SetRole(MoveVfxRole.Buff, UsableEntry("buff"));

            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("utility", MoveCategory.Support, ChainlingType.None, MoveVfxRole.Buff),
                new[] { legacy }, fallback);

            Assert.That(result.entry, Is.SameAs(legacy));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.LegacyCategory));
        }

        [TestCase(MoveCategory.DamagingMelee)]
        [TestCase(MoveCategory.DamagingRanged)]
        public void Resolve_TypedMelee_And_TypedRanged(MoveCategory category)
        {
            var fallback = NewFallbacks();
            var typed = UsableEntry("typed");
            fallback.SetTyped(ChainlingType.Gas, category, typed);

            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("damage", category, ChainlingType.Gas, MoveVfxRole.Damage),
                Array.Empty<CombatVFXLibrary.VFXEntry>(), fallback);

            Assert.That(result.entry, Is.SameAs(typed));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Typed));
        }

        [Test]
        public void Resolve_TypedEmpty_FallsToTypeless()
        {
            var fallback = NewFallbacks();
            fallback.SetTyped(ChainlingType.Node, MoveCategory.DamagingMelee, new CombatVFXLibrary.VFXEntry());
            var typeless = UsableEntry("typeless");
            fallback.SetTyped(ChainlingType.None, MoveCategory.DamagingMelee, typeless);

            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("damage", MoveCategory.DamagingMelee, ChainlingType.Node, MoveVfxRole.Damage),
                null, fallback);

            Assert.That(result.entry, Is.SameAs(typeless));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Typeless));
            StringAssert.Contains("Typed:Node/Melee empty", result.debugTrace);
        }

        [Test]
        public void Resolve_TypelessEmpty_FallsToGlobal()
        {
            var fallback = NewFallbacks();
            var global = UsableEntry("global");
            fallback.SetGlobal(MoveCategory.DamagingRanged, global);

            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("damage", MoveCategory.DamagingRanged, ChainlingType.Bot, MoveVfxRole.Damage),
                null, fallback);

            Assert.That(result.entry, Is.SameAs(global));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Global));
        }

        [Test]
        public void Resolve_NothingAuthored_ReturnsNoneTier()
        {
            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("missing", MoveCategory.Support, ChainlingType.None, MoveVfxRole.Buff),
                null, NewFallbacks());

            Assert.That(result.entry, Is.Null);
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.None));
        }

        [TestCase(MoveVfxRole.Wall, MoveVfxRole.Buff)]
        [TestCase(MoveVfxRole.ApDrain, MoveVfxRole.Debuff)]
        public void Resolve_SpecificRoleEmpty_FallsToParent(MoveVfxRole specific, MoveVfxRole parent)
        {
            var fallback = NewFallbacks();
            var expected = UsableEntry("parent");
            fallback.SetRole(parent, expected);

            VfxResolution result = CombatVfxResolver.Resolve(
                new VfxMoveKey("utility", MoveCategory.Support, ChainlingType.None, specific),
                null, fallback);

            Assert.That(result.entry, Is.SameAs(expected));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.RoleParent));
        }

        [Test]
        public void Resolve_DamageWithDebuff_ResolvesAsDamage()
        {
            var move = ScriptableObject.CreateInstance<MoveDefinitionSO>();
            _cleanup.Add(move);
            move.category = MoveCategory.DamagingRanged;
            move.targeting = MoveTargeting.SingleEnemy;
            move.statusEffects.Add(new MoveStatusEffectDef());

            Assert.That(MoveVfxRoleClassifier.Classify(move), Is.EqualTo(MoveVfxRole.Damage));
        }

        [Test]
        public void Classifier_PinnedEdgeCases()
        {
            AssertRole("position_swap", MoveVfxRole.Swap);
            AssertRole("guard_posture", MoveVfxRole.Armor);
            AssertRole("transaction_tax", MoveVfxRole.ApDrain);
            AssertRole("ap_boost", MoveVfxRole.ApGain);
            AssertRole("stasis_field", MoveVfxRole.Debuff);
            AssertRole("field_split", MoveVfxRole.Debuff);
            AssertRole("slipstream", ExpectedByTarget("slipstream"));
            AssertRole("risk_spike", ExpectedByTarget("risk_spike"));
            AssertRole("compression_trade", ExpectedByTarget("compression_trade"));
            AssertRole("zip_step", ExpectedByTarget("zip_step"));
        }

        [Test]
        public void Classifier_BucketsMatchCatalog()
        {
            List<MoveDefinitionSO> moves = LoadMoves();
            Assert.That(moves.Count, Is.EqualTo(211));
            Assert.That(moves.Count(m => m.category == MoveCategory.Passive), Is.EqualTo(31));
            Assert.That(moves.Count(m => m.category != MoveCategory.Passive), Is.EqualTo(180));
            Assert.That(moves.Where(m => m.category != MoveCategory.Passive)
                .All(m => MoveVfxRoleClassifier.Classify(m) != MoveVfxRole.Passive), Is.True);

            Dictionary<MoveVfxRole, int> counts = moves
                .GroupBy(MoveVfxRoleClassifier.Classify)
                .ToDictionary(group => group.Key, group => group.Count());
            Assert.That(counts[MoveVfxRole.Damage], Is.EqualTo(75));
            Assert.That(counts[MoveVfxRole.Passive], Is.EqualTo(31));
            Assert.That(counts[MoveVfxRole.Heal], Is.EqualTo(18));
            Assert.That(counts[MoveVfxRole.Buff], Is.EqualTo(40));
            Assert.That(counts[MoveVfxRole.Debuff], Is.EqualTo(29));
            Assert.That(counts[MoveVfxRole.Wall], Is.EqualTo(3));
            Assert.That(counts[MoveVfxRole.Barrier], Is.EqualTo(5));
            Assert.That(counts[MoveVfxRole.Armor], Is.EqualTo(1));
            Assert.That(counts[MoveVfxRole.ApGain], Is.EqualTo(3));
            Assert.That(counts[MoveVfxRole.ApDrain], Is.EqualTo(5));
            Assert.That(counts[MoveVfxRole.Swap], Is.EqualTo(1));
        }

        [Test]
        public void Preview_And_Runtime_ResolveSameEntry()
        {
            MoveDefinitionSO definition = FindMove("cipher_shot");
            Assert.That(definition, Is.Not.Null);
            var instance = new MoveInstance
            {
                definitionId = definition.moveId,
                category = definition.category,
                baseType = definition.baseType,
                targeting = definition.targeting,
                contactType = definition.contactType,
                tags = new List<string>(definition.tags),
                utilityEffects = new List<UtilityEffectPayload>(definition.utilityEffects),
                healPercentMaxHP = definition.healPercentMaxHP
            };

            var fallback = NewFallbacks();
            var expected = UsableEntry("typed");
            fallback.SetTyped(definition.baseType, definition.category, expected);

            VfxResolution preview = CombatVfxResolver.Resolve(VfxMoveKey.From(definition), null, fallback);
            VfxResolution runtime = CombatVfxResolver.Resolve(VfxMoveKey.From(instance), null, fallback);
            Assert.That(runtime.entry, Is.SameAs(preview.entry));
            Assert.That(runtime.tier, Is.EqualTo(preview.tier));
        }

        [Test]
        public void FallbackAsset_Has15UniqueTypedRows()
        {
            CombatVFXFallbacks asset = LoadFallbackAsset();
            Assert.That(asset, Is.Not.Null);
            Assert.That(asset.TypedRows.Count, Is.EqualTo(Enum.GetValues(typeof(ChainlingType)).Length));
            Assert.That(asset.TypedRows.Select(row => row.type).Distinct().Count(), Is.EqualTo(15));
        }

        [Test]
        public void FallbackAsset_SeededSlotsHaveRequiredPhasesAndUnsetImpactDelay()
        {
            CombatVFXFallbacks asset = LoadFallbackAsset();
            CombatVFXLibrary.VFXEntry melee = asset.GetTyped(ChainlingType.None, MoveCategory.DamagingMelee);
            CombatVFXLibrary.VFXEntry ranged = asset.GetTyped(ChainlingType.None, MoveCategory.DamagingRanged);

            Assert.That(melee, Is.Not.Null);
            Assert.That(ranged, Is.Not.Null);
            Assert.That(melee.impactDelay, Is.LessThanOrEqualTo(0f));
            Assert.That(ranged.impactDelay, Is.LessThanOrEqualTo(0f));
            Assert.That(melee.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Strike, out _) ||
                        melee.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Impact, out _), Is.True);
            Assert.That(ranged.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Projectile, out _), Is.True);
        }

        [Test]
        public void FallbackAsset_AllAuthoredEntriesPassValidation()
        {
            var warnings = new List<string>();
            LoadFallbackAsset().CollectValidationWarnings(warnings);
            Assert.That(warnings, Is.Empty);
        }

        [Test]
        public void VfxLibrary_IsOutsideIgnoredLibraryFolderAndWired()
        {
            const string path = "Assets/Data/Combat/VFX/Registry/CombatVFXLibrary.asset";
            CombatVFXLibrary library = AssetDatabase.LoadAssetAtPath<CombatVFXLibrary>(path);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Fallbacks, Is.Not.Null);
            Assert.That(File.Exists(path), Is.True);
            StringAssert.DoesNotContain("/Library/", path);
        }

        [Test]
        public void Catalog_AllUsableMovesResolve_WhenBetaSlotsFilled()
        {
            var fallback = NewFallbacks();
            var entry = UsableEntry("all");
            foreach (ChainlingType type in Enum.GetValues(typeof(ChainlingType)))
            {
                fallback.SetTyped(type, MoveCategory.DamagingMelee, entry);
                fallback.SetTyped(type, MoveCategory.DamagingRanged, entry);
            }
            fallback.SetRole(MoveVfxRole.Heal, entry);
            fallback.SetRole(MoveVfxRole.Buff, entry);
            fallback.SetRole(MoveVfxRole.Debuff, entry);

            List<MoveDefinitionSO> usable = LoadMoves().Where(m => m.category != MoveCategory.Passive).ToList();
            List<string> unresolved = usable
                .Where(move => CombatVfxResolver.Resolve(VfxMoveKey.From(move), null, fallback).tier == VfxResolveTier.None)
                .Select(move => move.moveId)
                .ToList();

            Assert.That(unresolved, Is.Empty, string.Join(", ", unresolved));
        }

        private CombatVFXFallbacks NewFallbacks()
        {
            var fallback = ScriptableObject.CreateInstance<CombatVFXFallbacks>();
            fallback.NormalizeTypedRows();
            _cleanup.Add(fallback);
            return fallback;
        }

        private CombatVFXLibrary.VFXEntry UsableEntry(string id)
        {
            var prefab = new GameObject("VfxTest_" + id);
            _cleanup.Add(prefab);
            return new CombatVFXLibrary.VFXEntry
            {
                moveDefinitionId = id,
                impactVfx = new CombatVFXLibrary.VFXPhaseConfig
                {
                    enabled = true,
                    vfxPrefab = prefab
                }
            };
        }

        private static List<MoveDefinitionSO> LoadMoves()
        {
            return AssetDatabase.FindAssets("t:MoveDefinitionSO", new[] { "Assets/Data/Combat/Moves" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<MoveDefinitionSO>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(move => move != null)
                .ToList();
        }

        private static MoveDefinitionSO FindMove(string id)
        {
            return LoadMoves().FirstOrDefault(move => move.moveId == id);
        }

        private static CombatVFXFallbacks LoadFallbackAsset()
        {
            return AssetDatabase.LoadAssetAtPath<CombatVFXFallbacks>(
                "Assets/Data/Combat/VFX/Registry/CombatVFXFallbacks.asset");
        }

        private static MoveVfxRole ExpectedByTarget(string id)
        {
            MoveDefinitionSO move = FindMove(id);
            Assert.That(move, Is.Not.Null, id);
            switch (move.targeting)
            {
                case MoveTargeting.SingleEnemy:
                case MoveTargeting.AllEnemies:
                case MoveTargeting.RandomEnemy:
                case MoveTargeting.Field:
                    return MoveVfxRole.Debuff;
                default:
                    return MoveVfxRole.Buff;
            }
        }

        private static void AssertRole(string id, MoveVfxRole expected)
        {
            MoveDefinitionSO move = FindMove(id);
            Assert.That(move, Is.Not.Null, id);
            Assert.That(MoveVfxRoleClassifier.Classify(move), Is.EqualTo(expected), id);
        }
    }
}
