using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatPostFxVolumeControllerTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void IdleWeightIsZeroAndDoesNotTouchBaselineVolume()
        {
            var baselineGo = CreateGo("BaselineVolume");
            var baseline = baselineGo.AddComponent<Volume>();
            baseline.isGlobal = true;
            baseline.priority = 0f;
            baseline.weight = 1f;
            var baselineProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _created.Add(baselineProfile);
            baseline.sharedProfile = baselineProfile;

            var controllerGo = CreateGo("CombatPostFx");
            var controller = controllerGo.AddComponent<CombatPostFxVolumeController>();
            Volume owned = controller.OwnedVolume;

            Assert.AreNotSame(baseline, owned);
            Assert.AreEqual(0f, owned.weight, 0.0001f);
            Assert.IsNull(owned.sharedProfile);
            Assert.AreSame(baselineProfile, baseline.sharedProfile);
            Assert.AreEqual(1f, baseline.weight, 0.0001f);
        }

        [Test]
        public void OwnedVolumeIsForcedOntoVolumeLayer()
        {
            int volumeLayer = LayerMask.NameToLayer("Volume");
            Assert.GreaterOrEqual(volumeLayer, 0, "Project must define a 'Volume' layer.");

            var controllerGo = CreateGo("CombatPostFx");
            var controller = controllerGo.AddComponent<CombatPostFxVolumeController>();
            Volume owned = controller.OwnedVolume;

            Assert.AreEqual(volumeLayer, owned.gameObject.layer);
        }

        [UnityTest]
        public IEnumerator PlayRaisesWeightThenReturnsToZero()
        {
            var controllerGo = CreateGo("CombatPostFx");
            var controller = controllerGo.AddComponent<CombatPostFxVolumeController>();
            CombatPostFxCueSO cue = CreateCue(fadeIn: 0.05f, hold: 0.05f, fadeOut: 0.05f);

            controller.Play(cue);
            Assert.IsTrue(controller.HasActivePlayback);

            float maxWeight = 0f;
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                maxWeight = Mathf.Max(maxWeight, controller.CurrentWeight);
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.Greater(maxWeight, 0.5f);
            Assert.AreEqual(0f, controller.CurrentWeight, 0.02f);
            Assert.IsFalse(controller.HasActivePlayback);
            Assert.IsNull(controller.OwnedVolume.sharedProfile);
        }

        [UnityTest]
        public IEnumerator ReplaceLatestCueEndsAtZeroWeight()
        {
            var controllerGo = CreateGo("CombatPostFx");
            var controller = controllerGo.AddComponent<CombatPostFxVolumeController>();
            CombatPostFxCueSO cueA = CreateCue(fadeIn: 0.02f, hold: 1f, fadeOut: 0.05f);
            CombatPostFxCueSO cueB = CreateCue(fadeIn: 0.02f, hold: 0.05f, fadeOut: 0.05f);

            controller.Play(cueA);
            yield return new WaitForSeconds(0.05f);
            Assert.Greater(controller.CurrentWeight, 0.1f);

            controller.Play(cueB);

            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            Assert.AreEqual(0f, controller.CurrentWeight, 0.02f);
            Assert.IsFalse(controller.HasActivePlayback);
        }

        [Test]
        public void ForceClearZerosWeightWithoutTouchingBaselineVolume()
        {
            var controllerGo = CreateGo("CombatPostFx");
            var controller = controllerGo.AddComponent<CombatPostFxVolumeController>();
            Volume owned = controller.OwnedVolume;
            var activeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _created.Add(activeProfile);
            owned.sharedProfile = activeProfile;
            owned.weight = 0.8f;

            Volume baseline = CreateGo("Baseline").AddComponent<Volume>();
            baseline.weight = 0.75f;
            var baselineProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _created.Add(baselineProfile);
            baseline.sharedProfile = baselineProfile;

            controller.ForceClear();

            Assert.AreEqual(0f, controller.CurrentWeight, 0.0001f);
            Assert.IsNull(owned.sharedProfile);
            Assert.AreEqual(0.75f, baseline.weight, 0.0001f);
            Assert.AreSame(baselineProfile, baseline.sharedProfile);
        }

        [Test]
        public void PhaseConfig_IsUsableWithPostFxCueOnly()
        {
            CombatPostFxCueSO cue = CreateCue(fadeIn: 0f, hold: 0f, fadeOut: 0f);
            var entry = new CombatVFXLibrary.VFXEntry
            {
                castVfx = new CombatVFXLibrary.VFXPhaseConfig
                {
                    enabled = true,
                    postFxCue = cue
                }
            };

            Assert.IsTrue(entry.TryGetPhaseConfig(CombatVFXLibrary.VFXPhase.Cast, out _));
        }

        private CombatPostFxCueSO CreateCue(float fadeIn, float hold, float fadeOut)
        {
            var cue = ScriptableObject.CreateInstance<CombatPostFxCueSO>();
            _created.Add(cue);
            cue.profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _created.Add(cue.profile);
            cue.peakWeight = 1f;
            cue.fadeInSeconds = fadeIn;
            cue.holdSeconds = hold;
            cue.fadeOutSeconds = fadeOut;
            return cue;
        }

        private GameObject CreateGo(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }
    }
}
