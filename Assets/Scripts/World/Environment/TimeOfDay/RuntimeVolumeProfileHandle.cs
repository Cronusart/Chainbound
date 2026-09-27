using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Owns a Play Mode clone of a URP Volume's authored <see cref="Volume.sharedProfile"/>.
/// Time-of-day writers retain the same handle per volume, drive the clone, and restore
/// the inspector baseline when the last retainer releases (Play Mode exit / disable).
/// </summary>
public sealed class RuntimeVolumeProfileHandle
{
    private static readonly Dictionary<int, RuntimeVolumeProfileHandle> handlesByVolumeId =
        new Dictionary<int, RuntimeVolumeProfileHandle>();

    private Volume volume;
    private VolumeProfile authoredSharedProfile;
    private float authoredWeight = 1f;
    private VolumeProfile runtimeClone;
    private VolumeProfile cloneSource;
    private int retainCount;

    public bool IsBound => volume != null;
    public VolumeProfile WorkingProfile => runtimeClone;

    /// <summary>
    /// Retain the runtime handle for this volume. Call <see cref="Release"/> from OnDisable.
    /// </summary>
    public static RuntimeVolumeProfileHandle Retain(Volume volume)
    {
        if (volume == null)
            return null;

        int id = volume.GetInstanceID();
        if (!handlesByVolumeId.TryGetValue(id, out RuntimeVolumeProfileHandle handle))
        {
            handle = new RuntimeVolumeProfileHandle();
            handle.Bind(volume);
            handlesByVolumeId[id] = handle;
        }

        handle.retainCount++;
        return handle;
    }

    public VolumeProfile AcquireWorkingProfile(VolumeProfile source)
    {
        if (!IsBound)
            return null;

        VolumeProfile desired = source != null ? source : authoredSharedProfile;
        if (desired == null)
            return null;

        if (runtimeClone != null && cloneSource == desired)
            return runtimeClone;

        ReleaseClone();
        runtimeClone = Object.Instantiate(desired);
        runtimeClone.name = desired.name + " (TOD Runtime)";
        runtimeClone.hideFlags = HideFlags.HideAndDontSave;
        cloneSource = desired;
        volume.profile = runtimeClone;
        return runtimeClone;
    }

    public void SetWeight(float weight)
    {
        if (volume != null)
            volume.weight = weight;
    }

    public void Release()
    {
        if (retainCount <= 0)
            return;

        retainCount--;
        if (retainCount > 0)
            return;

        int id = volume != null ? volume.GetInstanceID() : 0;
        Restore();
        if (id != 0)
            handlesByVolumeId.Remove(id);
    }

    private void Bind(Volume target)
    {
        volume = target;
        authoredSharedProfile = target != null ? target.sharedProfile : null;
        authoredWeight = target != null ? target.weight : 1f;
        retainCount = 0;
    }

    private void Restore()
    {
        if (volume != null)
        {
            volume.weight = authoredWeight;
            volume.sharedProfile = authoredSharedProfile;
            volume.profile = null;
        }

        ReleaseClone();
        volume = null;
        authoredSharedProfile = null;
        cloneSource = null;
        retainCount = 0;
    }

    private void ReleaseClone()
    {
        if (runtimeClone == null)
            return;

        if (Application.isPlaying)
            Object.Destroy(runtimeClone);
        else
            Object.DestroyImmediate(runtimeClone);

        runtimeClone = null;
        cloneSource = null;
    }
}
