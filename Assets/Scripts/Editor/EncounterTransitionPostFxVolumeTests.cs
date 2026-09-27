using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class EncounterTransitionPostFxVolumeTests
{
    private readonly List<Object> cleanup = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [Test]
    public void IdleWeightIsZeroAndDoesNotTouchBaselineVolume()
    {
        Volume baseline = CreateBaselineVolume(out VolumeProfile baselineProfile, out ChromaticAberration baselineCa);
        baselineCa.intensity.Override(0.12f);

        EncounterTransitionPostFxVolume overlay = CreateOverlay();
        Volume owned = overlay.OwnedVolume;

        Assert.That(owned, Is.Not.SameAs(baseline));
        Assert.That(owned.weight, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(owned.sharedProfile, Is.Null);
        Assert.That(baseline.sharedProfile, Is.SameAs(baselineProfile));
        Assert.That(baseline.weight, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(baselineCa.intensity.value, Is.EqualTo(0.12f).Within(0.0001f));
    }

    [Test]
    public void OwnedVolumeIsForcedOntoVolumeLayer()
    {
        int volumeLayer = LayerMask.NameToLayer("Volume");
        Assert.That(volumeLayer, Is.GreaterThanOrEqualTo(0), "Project must define a 'Volume' layer.");

        EncounterTransitionPostFxVolume overlay = CreateOverlay();
        Assert.That(overlay.OwnedVolume.gameObject.layer, Is.EqualTo(volumeLayer));
    }

    [Test]
    public void AnimateChromaticWeight_DoesNotMutateBaselineProfile()
    {
        Volume baseline = CreateBaselineVolume(out VolumeProfile baselineProfile, out ChromaticAberration baselineCa);
        baselineCa.intensity.Override(0.12f);

        EncounterTransitionPostFxVolume overlay = CreateOverlay();
        IEnumerator anim = overlay.AnimateChromaticWeight(0.8f, 0f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        Drain(anim);

        Assert.That(overlay.CurrentWeight, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(overlay.OwnedVolume.sharedProfile, Is.Not.Null);
        Assert.That(overlay.OwnedVolume.sharedProfile, Is.Not.SameAs(baselineProfile));
        Assert.That(baseline.sharedProfile, Is.SameAs(baselineProfile));
        Assert.That(baselineCa.intensity.value, Is.EqualTo(0.12f).Within(0.0001f));
        Assert.That(baseline.weight, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void ForceClearZerosWeightWithoutTouchingBaselineVolume()
    {
        Volume baseline = CreateBaselineVolume(out VolumeProfile baselineProfile, out ChromaticAberration baselineCa);
        baselineCa.intensity.Override(0.25f);

        EncounterTransitionPostFxVolume overlay = CreateOverlay();
        Drain(overlay.AnimateChromaticWeight(0.8f, 0f, AnimationCurve.Linear(0f, 0f, 1f, 1f)));
        overlay.ForceClear();

        Assert.That(overlay.CurrentWeight, Is.EqualTo(0f).Within(0.0001f));
        Assert.That(overlay.OwnedVolume.sharedProfile, Is.Null);
        Assert.That(baseline.sharedProfile, Is.SameAs(baselineProfile));
        Assert.That(baselineCa.intensity.value, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(baseline.weight, Is.EqualTo(1f).Within(0.0001f));
    }

    private Volume CreateBaselineVolume(out VolumeProfile profile, out ChromaticAberration chromatic)
    {
        GameObject go = CreateGo("BaselineGlobalVolume");
        Volume volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 0f;
        volume.weight = 1f;

        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        cleanup.Add(profile);
        chromatic = profile.Add<ChromaticAberration>(true);
        volume.sharedProfile = profile;
        return volume;
    }

    private EncounterTransitionPostFxVolume CreateOverlay()
    {
        GameObject go = CreateGo("EncounterLeadIn");
        return go.AddComponent<EncounterTransitionPostFxVolume>();
    }

    private GameObject CreateGo(string name)
    {
        GameObject go = new GameObject(name);
        cleanup.Add(go);
        return go;
    }

    private static void Drain(IEnumerator enumerator)
    {
        while (enumerator.MoveNext())
        {
        }
    }
}
