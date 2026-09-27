using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class CombatPopupVisualTests
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
        public void LegacyMapperMapsNumericAndImpactCategories()
        {
            Assert.AreEqual(CombatPopupCategory.Heal, CombatPopupLegacyMapper.ResolveLegacyCategory(12, true, false, null));
            Assert.AreEqual(CombatPopupCategory.Crit, CombatPopupLegacyMapper.ResolveLegacyCategory(40, false, true, null));
            Assert.AreEqual(CombatPopupCategory.Damage, CombatPopupLegacyMapper.ResolveLegacyCategory(18, false, false, null));
            Assert.AreEqual(CombatPopupCategory.Miss, CombatPopupLegacyMapper.ResolveImpactCategory("MISS"));
            Assert.AreEqual(CombatPopupCategory.ApGain, CombatPopupLegacyMapper.ResolveImpactCategory("+2 AP"));
            Assert.AreEqual(CombatPopupCategory.ApDrain, CombatPopupLegacyMapper.ResolveImpactCategory("-2 AP"));
            Assert.AreEqual(CombatPopupCategory.WallBroken, CombatPopupLegacyMapper.ResolveImpactCategory("BROKEN"));
        }

        [Test]
        public void VisualCatalogResolvesCategoryAndFallback()
        {
            var catalog = CreateAsset<CombatPopupVisualCatalogSO>();
            var damageProfile = CreateAsset<CombatPopupVisualProfileSO>();
            var fallbackProfile = CreateAsset<CombatPopupVisualProfileSO>();

            damageProfile.showText = true;
            damageProfile.showSprite = false;
            fallbackProfile.showSprite = true;

            SetCatalogEntries(catalog, new List<CombatPopupVisualCatalogEntry>
            {
                new CombatPopupVisualCatalogEntry
                {
                    category = CombatPopupCategory.Damage,
                    profile = damageProfile,
                },
            }, fallbackProfile);

            Assert.AreSame(damageProfile, catalog.ResolveProfile(CombatPopupCategory.Damage));
            Assert.AreSame(fallbackProfile, catalog.ResolveProfile(CombatPopupCategory.NoAp));
            Assert.AreSame(fallbackProfile, catalog.DefaultProfile);
        }

        [Test]
        public void PlayRequestFactoriesCaptureLabelAndAmountModes()
        {
            var labelRequest = CombatPopupPlayRequest.ForLabel(CombatPopupCategory.StatusDebuff, Vector3.zero, "BURN", -1);
            Assert.AreEqual(CombatPopupCategory.StatusDebuff, labelRequest.category);
            Assert.AreEqual("BURN", labelRequest.label);
            Assert.IsFalse(labelRequest.useNumericLabel);
            Assert.AreEqual(-1, labelRequest.facingSign);

            var amountRequest = CombatPopupPlayRequest.ForAmount(CombatPopupCategory.Heal, Vector3.up, 8, true, 1);
            Assert.AreEqual(CombatPopupCategory.Heal, amountRequest.category);
            Assert.AreEqual(8, amountRequest.amount);
            Assert.IsTrue(amountRequest.useNumericLabel);
            Assert.IsTrue(amountRequest.isHeal);
            Assert.AreEqual(1, amountRequest.facingSign);
        }

        [Test]
        public void MotionResolverMigratesLegacyMotionModeWhenRootLayersUnset()
        {
            var profile = CreateAsset<CombatPopupVisualProfileSO>();
            profile.rootMotionLayers = CombatPopupMotionLayers.None;
            profile.motionMode = CombatPopupMotionMode.Shake;

            Assert.AreEqual(CombatPopupMotionLayers.Shake, CombatPopupMotionResolver.ResolveRootLayers(profile));

            profile.motionMode = CombatPopupMotionMode.FloatUp;
            Assert.AreEqual(CombatPopupMotionLayers.Translate, CombatPopupMotionResolver.ResolveRootLayers(profile));
            Assert.AreEqual(CombatPopupTranslateMode.FloatUp, CombatPopupMotionResolver.ResolveTranslateMode(profile));

            profile.motionMode = CombatPopupMotionMode.StaticFade;
            Assert.AreEqual(CombatPopupMotionLayers.None, CombatPopupMotionResolver.ResolveRootLayers(profile));
        }

        [Test]
        public void MotionResolverPrefersExplicitRootLayersOverLegacyMotionMode()
        {
            var profile = CreateAsset<CombatPopupVisualProfileSO>();
            profile.rootMotionLayers = CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch;
            profile.translateMode = CombatPopupTranslateMode.FloatUp;
            profile.motionMode = CombatPopupMotionMode.Shake;

            Assert.AreEqual(
                CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch,
                CombatPopupMotionResolver.ResolveRootLayers(profile));
        }

        [Test]
        public void MotionResolverMirrorsChainlingRelativeOffsets()
        {
            var profile = CreateAsset<CombatPopupVisualProfileSO>();
            profile.moveOffset = new Vector3(0.5f, 0.75f, -0.2f);
            profile.arcControlOffset = new Vector3(0.35f, 0.2f, 0f);

            Vector3 mirroredMove = CombatPopupMotionResolver.ResolveMoveOffset(profile, -1, Vector3.zero);
            Vector3 mirroredArc = CombatPopupMotionResolver.ResolveArcControlOffset(profile, -1, Vector3.zero);

            Assert.AreEqual(-0.5f, mirroredMove.x, 0.001f);
            Assert.AreEqual(0.75f, mirroredMove.y, 0.001f);
            Assert.AreEqual(-0.2f, mirroredMove.z, 0.001f);
            Assert.AreEqual(-0.35f, mirroredArc.x, 0.001f);
        }

        [Test]
        public void PopupFacingSignFollowsChainlingFlipX()
        {
            var slotGo = new GameObject("Slot");
            _createdAssets.Add(slotGo);

            var visualRootGo = new GameObject("VisualRoot");
            visualRootGo.transform.SetParent(slotGo.transform, false);
            _createdAssets.Add(visualRootGo);

            var visual = visualRootGo.AddComponent<CombatChainlingVisual>();
            visual.SetFlipX(true);

            var view = slotGo.AddComponent<Combat2ChainlingView>();

            Assert.AreEqual(-1, view.ResolvePopupFacingSign());

            visual.SetFlipX(false);
            Assert.AreEqual(1, view.ResolvePopupFacingSign());
        }

        [Test]
        public void ProfileDefaultsUseComposableRootMotionLayers()
        {
            var damage = CreateAsset<CombatPopupVisualProfileSO>();
            var crit = CreateAsset<CombatPopupVisualProfileSO>();
            var blocked = CreateAsset<CombatPopupVisualProfileSO>();

            CombatPopupVisualProfileDefaults.ApplyDefaults(damage, CombatPopupCategory.Damage);
            CombatPopupVisualProfileDefaults.ApplyDefaults(crit, CombatPopupCategory.Crit);
            CombatPopupVisualProfileDefaults.ApplyDefaults(blocked, CombatPopupCategory.Blocked);

            Assert.AreEqual(
                CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Shake,
                damage.rootMotionLayers);
            Assert.AreEqual(
                CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Punch,
                crit.rootMotionLayers);
            Assert.AreEqual(
                CombatPopupMotionLayers.Translate | CombatPopupMotionLayers.Shake,
                blocked.rootMotionLayers);
        }

        [Test]
        public void ProfileDefaultsCoverWallMitigationFeedbackCategories()
        {
            var mitigated = CreateAsset<CombatPopupVisualProfileSO>();
            var reflect = CreateAsset<CombatPopupVisualProfileSO>();
            var debuff = CreateAsset<CombatPopupVisualProfileSO>();

            CombatPopupVisualProfileDefaults.ApplyDefaults(mitigated, CombatPopupCategory.WallMitigated);
            CombatPopupVisualProfileDefaults.ApplyDefaults(reflect, CombatPopupCategory.WallReflect);
            CombatPopupVisualProfileDefaults.ApplyDefaults(debuff, CombatPopupCategory.WallDebuff);

            Assert.IsTrue(mitigated.scaleByAmount);
            Assert.IsTrue(reflect.scaleByAmount);
            Assert.Greater(reflect.textColor.r, 0.9f);
            Assert.Greater(debuff.textColor.g, 0.4f);
            Assert.AreEqual(new Color(0.35f, 0.85f, 1f, 1f).g, mitigated.textColor.g, 0.05f);
        }

        [Test]
        public void AmountScaleUtilityInterpolatesBetweenConfiguredThresholds()
        {
            var profile = CreateAsset<CombatPopupVisualProfileSO>();
            profile.scaleByAmount = true;
            profile.minAmountForScale = 10f;
            profile.maxAmountForScale = 50f;
            profile.minAmountScaleMultiplier = 1f;
            profile.maxAmountScaleMultiplier = 1.5f;
            profile.scaleFontSizeByAmount = true;
            profile.minAmountFontSize = 2f;
            profile.maxAmountFontSize = 4f;
            profile.fontSize = 2.8f;

            Assert.AreEqual(1f, CombatPopupAmountScaleUtility.ResolveScaleFactor(profile, 10), 0.001f);
            Assert.AreEqual(1.5f, CombatPopupAmountScaleUtility.ResolveScaleFactor(profile, 50), 0.001f);
            Assert.AreEqual(1.25f, CombatPopupAmountScaleUtility.ResolveScaleFactor(profile, 30), 0.001f);
            Assert.AreEqual(2.8f, CombatPopupAmountScaleUtility.ResolveFontSize(profile, 10, profile.fontSize), 0.001f);
            Assert.AreEqual(4.2f, CombatPopupAmountScaleUtility.ResolveFontSize(profile, 50, profile.fontSize), 0.001f);
        }

        [Test]
        public void AmountScaleUtilityUsesProfileFontSizeWhenLegacyBoundsAreStale()
        {
            var profile = CreateAsset<CombatPopupVisualProfileSO>();
            profile.fontSize = 10f;
            profile.scaleByAmount = true;
            profile.scaleFontSizeByAmount = true;
            profile.minAmountForScale = 1f;
            profile.maxAmountForScale = 80f;
            profile.minAmountScaleMultiplier = 1f;
            profile.maxAmountScaleMultiplier = 1.35f;
            profile.minAmountFontSize = 2.4f;
            profile.maxAmountFontSize = 3.8f;

            // Amount curve matches root scale: baseFontSize * Lerp(min, max, InverseLerp(minAmt, maxAmt, amount)).
            Assert.AreEqual(10f, CombatPopupAmountScaleUtility.ResolveFontSize(profile, 1, profile.fontSize), 0.001f);
            Assert.AreEqual(
                10f * Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(1f, 80f, 12f)),
                CombatPopupAmountScaleUtility.ResolveFontSize(profile, 12, profile.fontSize),
                0.001f);
            Assert.AreEqual(13.5f, CombatPopupAmountScaleUtility.ResolveFontSize(profile, 80, profile.fontSize), 0.001f);
        }

        [Test]
        public void ProfileDefaultsEnableAmountScalingForDamageAndHeal()
        {
            var damage = CreateAsset<CombatPopupVisualProfileSO>();
            var heal = CreateAsset<CombatPopupVisualProfileSO>();
            var miss = CreateAsset<CombatPopupVisualProfileSO>();

            CombatPopupVisualProfileDefaults.ApplyDefaults(damage, CombatPopupCategory.Damage);
            CombatPopupVisualProfileDefaults.ApplyDefaults(heal, CombatPopupCategory.Heal);
            CombatPopupVisualProfileDefaults.ApplyDefaults(miss, CombatPopupCategory.Miss);

            Assert.IsTrue(damage.scaleByAmount);
            Assert.IsTrue(heal.scaleByAmount);
            Assert.IsFalse(miss.scaleByAmount);
            Assert.AreEqual(damage.textColor.r, 1f, 0.01f);
            Assert.Greater(heal.textColor.g, 0.9f);
        }

        /// <summary>
        /// Play Mode smoke checks (manual):
        /// text-only, sprite-only, and combined popup categories for damage, AP, status, passive, and wall feedback.
        /// </summary>
        [Test]
        public void PopupProfileDefaultsAreReadable()
        {
            var profile = CreateAsset<CombatPopupVisualProfileSO>();
            Assert.IsTrue(profile.showText);
            Assert.IsFalse(profile.showSprite);
            Assert.Greater(profile.duration, 0f);
            Assert.IsFalse(string.IsNullOrWhiteSpace(profile.spriteColorPropertyName));
        }

        [Test]
        public void PopupWorldPositionPrefersPopUpAnchorOverCameraFocus()
        {
            var slotGo = new GameObject("Slot");
            _createdAssets.Add(slotGo);

            var visualRootGo = new GameObject("VisualRoot");
            visualRootGo.transform.SetParent(slotGo.transform, false);
            _createdAssets.Add(visualRootGo);

            var cameraFocusGo = new GameObject("CameraFocus");
            cameraFocusGo.transform.SetParent(visualRootGo.transform, false);
            cameraFocusGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            _createdAssets.Add(cameraFocusGo);

            var popupAnchorGo = new GameObject("PopUpAnchor");
            popupAnchorGo.transform.SetParent(visualRootGo.transform, false);
            popupAnchorGo.transform.localPosition = new Vector3(0f, 3f, -0.2f);
            _createdAssets.Add(popupAnchorGo);

            var impactAnchorGo = new GameObject("ImpactAnchor");
            impactAnchorGo.transform.SetParent(visualRootGo.transform, false);
            impactAnchorGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            _createdAssets.Add(impactAnchorGo);

            var view = slotGo.AddComponent<Combat2ChainlingView>();

            Vector3 popupPosition = view.ResolvePopupWorldPosition();
            Vector3 cameraFocusPosition = view.CameraFocus.position;

            Assert.AreNotEqual(cameraFocusPosition, popupPosition);
            Assert.AreEqual(popupAnchorGo.transform.position, popupPosition);
        }

        [Test]
        public void PopupWorldPositionFallsBackToImpactThenSlotWithoutCameraFocus()
        {
            var slotGo = new GameObject("Slot");
            slotGo.transform.position = new Vector3(5f, 0f, 0f);
            _createdAssets.Add(slotGo);

            var visualRootGo = new GameObject("VisualRoot");
            visualRootGo.transform.SetParent(slotGo.transform, false);
            _createdAssets.Add(visualRootGo);

            var cameraFocusGo = new GameObject("CameraFocus");
            cameraFocusGo.transform.SetParent(visualRootGo.transform, false);
            cameraFocusGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            _createdAssets.Add(cameraFocusGo);

            var impactAnchorGo = new GameObject("ImpactAnchor");
            impactAnchorGo.transform.SetParent(visualRootGo.transform, false);
            impactAnchorGo.transform.localPosition = new Vector3(0f, 2f, 0f);
            _createdAssets.Add(impactAnchorGo);

            var view = slotGo.AddComponent<Combat2ChainlingView>();

            Assert.IsNull(view.PopupAnchor);
            Assert.AreEqual(impactAnchorGo.transform.position, view.ResolvePopupWorldPosition());
            Object.DestroyImmediate(impactAnchorGo);
            Assert.AreEqual(slotGo.transform.position, view.ResolvePopupWorldPosition());
        }

        private T CreateAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _createdAssets.Add(asset);
            return asset;
        }

        private static void SetCatalogEntries(
            CombatPopupVisualCatalogSO catalog,
            List<CombatPopupVisualCatalogEntry> entries,
            CombatPopupVisualProfileSO fallback)
        {
            var entriesField = typeof(CombatPopupVisualCatalogSO).GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic);
            var fallbackField = typeof(CombatPopupVisualCatalogSO).GetField("defaultProfile", BindingFlags.Instance | BindingFlags.NonPublic);
            entriesField?.SetValue(catalog, entries);
            fallbackField?.SetValue(catalog, fallback);
            catalog.BuildLookup();
        }
    }
}
