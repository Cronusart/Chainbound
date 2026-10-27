using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatVfxPhaseFillTests
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
        public void ExplicitProjectile_FillsImpactFromTyped_AndKeepsExplicitTier()
        {
            GameObject projectilePrefab = NewPrefab("projectile");
            GameObject impactPrefab = NewPrefab("impact");
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: projectilePrefab);
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, Entry("typed", impact: impactPrefab));

            VfxResolution result = Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);

            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Explicit));
            Assert.That(result.isComposed, Is.True);
            Assert.That(result.entry, Is.Not.SameAs(explicitEntry));
            Assert.That(result.entry.projectileVfx.vfxPrefab, Is.SameAs(projectilePrefab));
            Assert.That(result.entry.impactVfx.vfxPrefab, Is.SameAs(impactPrefab));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Projectile), Is.EqualTo(VfxResolveTier.Explicit));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Impact), Is.EqualTo(VfxResolveTier.Typed));
            StringAssert.Contains("Projectile=Explicit:scorch_shot", result.debugTrace);
            StringAssert.Contains("Impact=Typed:Burned/Ranged", result.debugTrace);
        }

        [Test]
        public void ExplicitProjectile_EmptyTyped_FillsImpactFromTypeless()
        {
            GameObject projectilePrefab = NewPrefab("projectile");
            GameObject impactPrefab = NewPrefab("impact");
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: projectilePrefab);
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, new CombatVFXLibrary.VFXEntry());
            fallbacks.SetTyped(ChainlingType.None, MoveCategory.DamagingRanged, Entry("typeless", impact: impactPrefab));

            VfxResolution result = Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);

            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Projectile), Is.EqualTo(VfxResolveTier.Explicit));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Impact), Is.EqualTo(VfxResolveTier.Typeless));
            Assert.That(result.entry.impactVfx.vfxPrefab, Is.SameAs(impactPrefab));
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Explicit));
        }

        [Test]
        public void TypedImpact_FillsCastAndProjectileFromTypeless()
        {
            GameObject castPrefab = NewPrefab("cast");
            GameObject projectilePrefab = NewPrefab("projectile");
            GameObject impactPrefab = NewPrefab("impact");
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, Entry("typed", impact: impactPrefab));
            fallbacks.SetTyped(
                ChainlingType.None,
                MoveCategory.DamagingRanged,
                Entry("typeless", cast: castPrefab, projectile: projectilePrefab));

            VfxResolution result = Resolve(
                "shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                null,
                fallbacks);

            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Typed));
            Assert.That(result.isComposed, Is.True);
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Cast), Is.EqualTo(VfxResolveTier.Typeless));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Projectile), Is.EqualTo(VfxResolveTier.Typeless));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Impact), Is.EqualTo(VfxResolveTier.Typed));
            Assert.That(result.entry.castVfx.vfxPrefab, Is.SameAs(castPrefab));
            Assert.That(result.entry.projectileVfx.vfxPrefab, Is.SameAs(projectilePrefab));
            Assert.That(result.entry.impactVfx.vfxPrefab, Is.SameAs(impactPrefab));
        }

        [Test]
        public void FullyAuthoredExplicit_ReturnsTheSameInstance()
        {
            GameObject projectilePrefab = NewPrefab("projectile");
            GameObject impactPrefab = NewPrefab("impact");
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: projectilePrefab, impact: impactPrefab);
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, Entry("typed", impact: NewPrefab("other-impact")));

            VfxResolution result = Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);

            Assert.That(result.entry, Is.SameAs(explicitEntry));
            Assert.That(result.isComposed, Is.False);
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Explicit));
        }

        [Test]
        public void SuppressPhaseFill_LeavesTheMissingImpactMissing()
        {
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: NewPrefab("projectile"));
            explicitEntry.suppressPhaseFill = true;
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, Entry("typed", impact: NewPrefab("impact")));

            VfxResolution result = Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);

            Assert.That(result.entry, Is.SameAs(explicitEntry));
            Assert.That(result.isComposed, Is.False);
            Assert.That(result.entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Impact, out _), Is.False);
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Impact), Is.EqualTo(VfxResolveTier.None));
        }

        [Test]
        public void LegacyStrike_FillsImpactFromTyped()
        {
            GameObject strikePrefab = NewPrefab("strike");
            GameObject impactPrefab = NewPrefab("impact");
            var legacy = Entry("legacy", strike: strikePrefab);
            legacy.isCategoryFallback = true;
            legacy.fallbackCategory = MoveCategory.DamagingMelee;
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Node, MoveCategory.DamagingMelee, Entry("typed", impact: impactPrefab));

            VfxResolution result = Resolve(
                "jab",
                MoveCategory.DamagingMelee,
                ChainlingType.Node,
                new[] { legacy },
                fallbacks);

            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.LegacyCategory));
            Assert.That(result.isComposed, Is.True);
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Strike), Is.EqualTo(VfxResolveTier.LegacyCategory));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Impact), Is.EqualTo(VfxResolveTier.Typed));
            Assert.That(result.entry.strikeVfx.vfxPrefab, Is.SameAs(strikePrefab));
            Assert.That(result.entry.impactVfx.vfxPrefab, Is.SameAs(impactPrefab));
        }

        [Test]
        public void UtilityCast_FillsImpactFromGlobal()
        {
            GameObject roleCast = NewPrefab("role-cast");
            GameObject globalCast = NewPrefab("global-cast");
            GameObject globalImpact = NewPrefab("global-impact");
            var fallbacks = NewFallbacks();
            fallbacks.SetRole(MoveVfxRole.Heal, Entry("heal", cast: roleCast));
            fallbacks.SetGlobal(MoveCategory.Support, Entry("utility", cast: globalCast, impact: globalImpact));

            VfxResolution result = Resolve(
                "mend",
                MoveCategory.Support,
                ChainlingType.None,
                null,
                fallbacks,
                MoveVfxRole.Heal);

            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.Role));
            Assert.That(result.isComposed, Is.True);
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Cast), Is.EqualTo(VfxResolveTier.Role));
            Assert.That(result.PhaseTier(CombatVFXLibrary.VFXPhase.Impact), Is.EqualTo(VfxResolveTier.Global));
            Assert.That(result.entry.castVfx.vfxPrefab, Is.SameAs(roleCast));
            Assert.That(result.entry.impactVfx.vfxPrefab, Is.SameAs(globalImpact));
        }

        [Test]
        public void PassiveKey_ReturnsNone()
        {
            var fallbacks = NewFallbacks();
            fallbacks.SetRole(MoveVfxRole.Heal, Entry("heal", cast: NewPrefab("cast")));

            VfxResolution result = Resolve(
                "aura",
                MoveCategory.Passive,
                ChainlingType.None,
                null,
                fallbacks,
                MoveVfxRole.Passive);

            Assert.That(result.entry, Is.Null);
            Assert.That(result.tier, Is.EqualTo(VfxResolveTier.None));
        }

        [Test]
        public void NullFallbacks_FillsOnlyAcrossExplicitAndLegacy()
        {
            GameObject projectilePrefab = NewPrefab("projectile");
            GameObject strikePrefab = NewPrefab("strike");
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("jab", projectile: projectilePrefab);
            var legacy = Entry("legacy", strike: strikePrefab);
            legacy.isCategoryFallback = true;
            legacy.fallbackCategory = MoveCategory.DamagingMelee;

            VfxResolution merged = Resolve(
                "jab",
                MoveCategory.DamagingMelee,
                ChainlingType.Node,
                new[] { explicitEntry, legacy },
                null);
            Assert.That(merged.isComposed, Is.True);
            Assert.That(merged.tier, Is.EqualTo(VfxResolveTier.Explicit));
            Assert.That(merged.PhaseTier(CombatVFXLibrary.VFXPhase.Projectile), Is.EqualTo(VfxResolveTier.Explicit));
            Assert.That(merged.PhaseTier(CombatVFXLibrary.VFXPhase.Strike), Is.EqualTo(VfxResolveTier.LegacyCategory));
            Assert.That(merged.entry.projectileVfx.vfxPrefab, Is.SameAs(projectilePrefab));
            Assert.That(merged.entry.strikeVfx.vfxPrefab, Is.SameAs(strikePrefab));

            VfxResolution alone = Resolve(
                "jab",
                MoveCategory.DamagingMelee,
                ChainlingType.Node,
                new[] { explicitEntry },
                null);
            Assert.That(alone.entry, Is.SameAs(explicitEntry));
            Assert.That(alone.isComposed, Is.False);
        }

        [Test]
        public void Resolve_DoesNotMutateSourceEntries()
        {
            GameObject projectilePrefab = NewPrefab("projectile");
            GameObject impactPrefab = NewPrefab("impact");
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: projectilePrefab);
            explicitEntry.projectileVfx.scaleMultiplier = 1.75f;
            AnimationCurve curve = explicitEntry.projectileVfx.phaseScaleCurve;
            CombatVFXLibrary.VFXEntry typed = Entry("typed", impact: impactPrefab);
            typed.impactVfx.scaleMultiplier = 2.25f;
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, typed);

            Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);

            Assert.That(explicitEntry.projectileVfx.enabled, Is.True);
            Assert.That(explicitEntry.projectileVfx.vfxPrefab, Is.SameAs(projectilePrefab));
            Assert.That(explicitEntry.projectileVfx.scaleMultiplier, Is.EqualTo(1.75f));
            Assert.That(explicitEntry.projectileVfx.phaseScaleCurve, Is.SameAs(curve));
            Assert.That(explicitEntry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Impact, out _), Is.False);
            Assert.That(typed.impactVfx.enabled, Is.True);
            Assert.That(typed.impactVfx.vfxPrefab, Is.SameAs(impactPrefab));
            Assert.That(typed.impactVfx.scaleMultiplier, Is.EqualTo(2.25f));
        }

        [Test]
        public void NonPhaseFields_PreferBaseThenImpactSupplier()
        {
            var baseSound = ScriptableObject.CreateInstance<SoundEventSO>();
            var impactSound = ScriptableObject.CreateInstance<SoundEventSO>();
            _cleanup.Add(baseSound);
            _cleanup.Add(impactSound);

            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: NewPrefab("projectile"));
            explicitEntry.impactDelay = 0.4f;
            explicitEntry.impactSound = baseSound;
            CombatVFXLibrary.VFXEntry typed = Entry("typed", impact: NewPrefab("impact"));
            typed.impactDelay = 0.2f;
            typed.impactSound = impactSound;
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, typed);

            VfxResolution preferred = Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);
            Assert.That(preferred.entry.impactDelay, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(preferred.entry.impactSound, Is.SameAs(baseSound));

            explicitEntry.impactDelay = -1f;
            explicitEntry.impactSound = null;
            VfxResolution inherited = Resolve(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                new[] { explicitEntry },
                fallbacks);
            Assert.That(inherited.entry.impactDelay, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(inherited.entry.impactSound, Is.SameAs(impactSound));
        }

        [Test]
        public void LibraryCache_ReusesResolution_UntilFallbacksVersionChanges()
        {
            GameObject firstImpact = NewPrefab("impact-a");
            GameObject secondImpact = NewPrefab("impact-b");
            CombatVFXLibrary.VFXEntry explicitEntry = Entry("scorch_shot", projectile: NewPrefab("projectile"));
            CombatVFXLibrary.VFXEntry typed = Entry("typed", impact: firstImpact);
            var fallbacks = NewFallbacks();
            fallbacks.SetTyped(ChainlingType.Burned, MoveCategory.DamagingRanged, typed);

            var library = ScriptableObject.CreateInstance<CombatVFXLibrary>();
            _cleanup.Add(library);
            SetPrivate(library, "entries", new[] { explicitEntry });
            SetPrivate(library, "fallbacks", fallbacks);

            VfxMoveKey key = new VfxMoveKey(
                "scorch_shot",
                MoveCategory.DamagingRanged,
                ChainlingType.Burned,
                MoveVfxRole.Damage);
            VfxResolution first = library.Resolve(key);
            VfxResolution repeat = library.Resolve(key);
            Assert.That(repeat.entry, Is.SameAs(first.entry));
            Assert.That(repeat.entry.impactVfx.vfxPrefab, Is.SameAs(firstImpact));
            Assert.That(repeat.isComposed, Is.EqualTo(first.isComposed));

            typed.impactVfx.vfxPrefab = secondImpact;
            fallbacks.MarkChanged();
            VfxResolution refreshed = library.Resolve(key);
            Assert.That(refreshed.entry, Is.Not.SameAs(first.entry));
            Assert.That(refreshed.entry.impactVfx.vfxPrefab, Is.SameAs(secondImpact));
        }

        private VfxResolution Resolve(
            string id,
            MoveCategory category,
            ChainlingType type,
            CombatVFXLibrary.VFXEntry[] entries,
            CombatVFXFallbacks fallbacks,
            MoveVfxRole role = MoveVfxRole.Damage)
        {
            return CombatVfxResolver.Resolve(new VfxMoveKey(id, category, type, role), entries, fallbacks);
        }

        private CombatVFXFallbacks NewFallbacks()
        {
            var fallbacks = ScriptableObject.CreateInstance<CombatVFXFallbacks>();
            fallbacks.NormalizeTypedRows();
            _cleanup.Add(fallbacks);
            return fallbacks;
        }

        private GameObject NewPrefab(string name)
        {
            var prefab = new GameObject("VfxPhaseFill_" + name);
            _cleanup.Add(prefab);
            return prefab;
        }

        private static CombatVFXLibrary.VFXEntry Entry(
            string id,
            GameObject cast = null,
            GameObject strike = null,
            GameObject projectile = null,
            GameObject impact = null)
        {
            return new CombatVFXLibrary.VFXEntry
            {
                moveDefinitionId = id,
                castVfx = Phase(cast),
                strikeVfx = Phase(strike),
                projectileVfx = Phase(projectile),
                impactVfx = Phase(impact)
            };
        }

        private static CombatVFXLibrary.VFXPhaseConfig Phase(GameObject prefab)
        {
            return new CombatVFXLibrary.VFXPhaseConfig
            {
                enabled = prefab != null,
                vfxPrefab = prefab
            };
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}

