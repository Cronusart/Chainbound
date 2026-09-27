using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Owns a high-priority overlay URP Volume for encounter lead-in chromatic aberration.
/// Isolation: never finds, reads, or writes the GameServices / TimeOfDay Global Volume.
/// Animates owned <see cref="Volume.weight"/> only; the runtime chromatic profile is HideAndDontSave.
/// </summary>
[DisallowMultipleComponent]
public sealed class EncounterTransitionPostFxVolume : MonoBehaviour
{
    private const float DefaultOverlayPriority = 50f;
    private const string VolumeLayerName = "Volume";
    private const string ChildVolumeName = "EncounterLeadInVolume";

    [Header("Owned Volume")]
    [Tooltip("Encounter-only overlay Volume. Auto-created as a child when unset. Never assign the scene/GameServices Global Volume.")]
    [SerializeField] private Volume ownedVolume;

    [Tooltip("URP global priority for the overlay. Scene / TOD volumes typically use 0; combat post-FX uses 100.")]
    [SerializeField] private float overlayPriority = DefaultOverlayPriority;

    private VolumeProfile runtimeProfile;
    private ChromaticAberration chromatic;

    public Volume OwnedVolume
    {
        get
        {
            EnsureOwnedVolume();
            return ownedVolume;
        }
    }

    public float CurrentWeight => ownedVolume != null ? ownedVolume.weight : 0f;

    private void Awake()
    {
        EnsureOwnedVolume();
        ApplyIdleState();
    }

    private void OnDisable()
    {
        ForceClear();
    }

    private void OnDestroy()
    {
        ForceClear();
        ReleaseRuntimeProfile();
    }

    private void OnValidate()
    {
        overlayPriority = Mathf.Max(0f, overlayPriority);
        if (ownedVolume != null)
            ConfigureOwnedVolume(ownedVolume);
    }

    /// <summary>
    /// Arms the overlay chromatic profile and fades weight 0 → 1 over <paramref name="durationSeconds"/>.
    /// Caller must <see cref="ForceClear"/> in finally / disable so a mid-transition stop cannot leak.
    /// </summary>
    public IEnumerator AnimateChromaticWeight(
        float peakIntensity,
        float durationSeconds,
        AnimationCurve weightCurve)
    {
        EnsureOwnedVolume();
        ArmRuntimeProfile(peakIntensity);
        ownedVolume.weight = 0f;

        if (durationSeconds <= 0f)
        {
            ownedVolume.weight = 1f;
            yield break;
        }

        AnimationCurve curve = weightCurve != null && weightCurve.length > 0
            ? weightCurve
            : AnimationCurve.Linear(0f, 0f, 1f, 1f);

        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / durationSeconds);
            ownedVolume.weight = Mathf.Clamp01(curve.Evaluate(normalized));
            yield return null;
        }

        ownedVolume.weight = 1f;
    }

    /// <summary>
    /// Immediately stops overlay contribution. Safe for aborted transitions, scene load, and Play Mode exit.
    /// </summary>
    public void ForceClear()
    {
        ApplyIdleState();
    }

    private void ArmRuntimeProfile(float peakIntensity)
    {
        if (runtimeProfile == null)
        {
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = "EncounterLeadInChromatic (Runtime)";
            runtimeProfile.hideFlags = HideFlags.HideAndDontSave;
            chromatic = runtimeProfile.Add<ChromaticAberration>(true);
        }
        else if (chromatic == null)
        {
            runtimeProfile.TryGet(out chromatic);
        }

        if (chromatic != null)
            chromatic.intensity.Override(Mathf.Clamp01(peakIntensity));

        ownedVolume.sharedProfile = runtimeProfile;
    }

    private void ApplyIdleState()
    {
        if (ownedVolume == null)
            return;

        ownedVolume.weight = 0f;
        ownedVolume.sharedProfile = null;
        ConfigureOwnedVolume(ownedVolume);
    }

    private void EnsureOwnedVolume()
    {
        if (ownedVolume != null)
        {
            ConfigureOwnedVolume(ownedVolume);
            return;
        }

        Transform existing = transform.Find(ChildVolumeName);
        if (existing != null)
            ownedVolume = existing.GetComponent<Volume>();

        if (ownedVolume == null)
        {
            GameObject go = new GameObject(ChildVolumeName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            ownedVolume = go.AddComponent<Volume>();
        }

        ConfigureOwnedVolume(ownedVolume);
        ApplyIdleState();
    }

    private void ConfigureOwnedVolume(Volume volume)
    {
        if (volume == null)
            return;

        int volumeLayer = LayerMask.NameToLayer(VolumeLayerName);
        if (volumeLayer >= 0 && volume.gameObject.layer != volumeLayer)
            volume.gameObject.layer = volumeLayer;

        volume.isGlobal = true;
        volume.priority = overlayPriority;
        if (!volume.enabled)
            volume.enabled = true;
    }

    private void ReleaseRuntimeProfile()
    {
        chromatic = null;
        if (runtimeProfile == null)
            return;

        if (Application.isPlaying)
            Destroy(runtimeProfile);
        else
            DestroyImmediate(runtimeProfile);

        runtimeProfile = null;
    }
}
