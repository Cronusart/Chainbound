using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class RuntimeVolumeProfileHandleTests
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
    public void Release_RestoresAuthoredSharedProfileAndDoesNotMutateAsset()
    {
        VolumeProfile authored = ScriptableObject.CreateInstance<VolumeProfile>();
        cleanup.Add(authored);
        ColorAdjustments authoredAdjustments = authored.Add<ColorAdjustments>();
        authoredAdjustments.postExposure.Override(1.25f);

        GameObject volumeObject = new GameObject("TOD Volume");
        cleanup.Add(volumeObject);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = authored;
        volume.weight = 0.8f;

        RuntimeVolumeProfileHandle first = RuntimeVolumeProfileHandle.Retain(volume);
        RuntimeVolumeProfileHandle second = RuntimeVolumeProfileHandle.Retain(volume);
        Assert.That(first, Is.SameAs(second));

        VolumeProfile working = first.AcquireWorkingProfile(authored);
        Assert.That(working, Is.Not.Null);
        Assert.That(working, Is.Not.SameAs(authored));
        Assert.That(volume.HasInstantiatedProfile(), Is.True);

        Assert.That(working.TryGet(out ColorAdjustments workingAdjustments), Is.True);
        workingAdjustments.postExposure.Override(9f);
        first.SetWeight(0.2f);

        Assert.That(authoredAdjustments.postExposure.value, Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(volume.weight, Is.EqualTo(0.2f).Within(0.0001f));

        first.Release();
        Assert.That(volume.HasInstantiatedProfile(), Is.True);
        Assert.That(authoredAdjustments.postExposure.value, Is.EqualTo(1.25f).Within(0.0001f));

        second.Release();
        Assert.That(volume.HasInstantiatedProfile(), Is.False);
        Assert.That(volume.sharedProfile, Is.SameAs(authored));
        Assert.That(volume.weight, Is.EqualTo(0.8f).Within(0.0001f));
        Assert.That(authoredAdjustments.postExposure.value, Is.EqualTo(1.25f).Within(0.0001f));
    }
}
