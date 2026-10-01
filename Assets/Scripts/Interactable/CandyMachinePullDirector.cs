using System;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using UnityEngine;

/// <summary>
/// Scene-local Candy_Machine sequence: Idle -> Lever -> GlowShake -> Door -> RevealReady.
/// The UI owns transaction/reveal state; this component only animates authored world references.
/// </summary>
[DisallowMultipleComponent]
public sealed class CandyMachinePullDirector : MonoBehaviour, ICandyMachinePullDirector
{
    [Header("Authored machine references")]
    [SerializeField] private Transform leverRoot;
    [SerializeField] private Transform spheresRoot;
    [SerializeField] private Renderer emissiveRenderer;
    [SerializeField] private Transform candyMachineDoor;

    [Header("Lever")]
    [SerializeField] private Vector3 leverLocalAxis = Vector3.forward;
    [SerializeField, Min(0.5f)] private float leverDuration = 2.05f;
    [SerializeField, Range(0.1f, 1f)] private float leverSpringDuration = 0.72f;
    [SerializeField, Range(1f, 60f)] private float leverOvershootDegrees = 32f;
    [SerializeField, Range(0.5f, 3f)] private float leverElasticAmplitude = 1.85f;
    [SerializeField, Range(0.08f, 0.8f)] private float leverElasticPeriod = 0.36f;
    [SerializeField] private AnimationCurve leverCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0.8f, 0.8f),
        new Keyframe(0.45f, 0.47f, 0.3f, 0.3f),
        new Keyframe(0.55f, 0.53f, 0.3f, 0.3f),
        new Keyframe(1f, 1f, 1.2f, 0f));

    [Header("Glow charge")]
    [SerializeField, Min(0f)] private float glowShakeStart = 1.1f;
    [SerializeField, Min(0.1f)] private float glowRampUpDuration = 2.2f;
    [SerializeField, Min(0f)] private float glowHoldDuration = 0.6f;
    [SerializeField, Min(0.1f)] private float glowRampDownDuration = 2.2f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.06f;
    [SerializeField] private Vector2 shakeFrequencyRange = new Vector2(8f, 24f);
    [SerializeField, Min(0f)] private float maximumEmission = 3.5f;

    [Header("Door")]
    [SerializeField] private float doorLocalXDegrees = 115f;
    [SerializeField, Min(0.1f)] private float doorDuration = 0.5f;
    [SerializeField, Min(0f)] private float doorHold = 0.15f;

    [Header("Camera")]
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private CameraShotPresetSO pullShot;

    [Header("Audio")]
    [SerializeField] private SoundEventSO leverSound;
    [SerializeField] private SoundEventSO glowSound;
    [SerializeField] private SoundEventSO doorSound;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Sequence sequence;
    private MaterialPropertyBlock propertyBlock;
    private Quaternion leverAuthoredRotation;
    private Quaternion doorAuthoredRotation;
    private Vector3 spheresAuthoredPosition;
    private Color authoredBaseColor = Color.white;
    private Color authoredEmissionColor = Color.white;
    private AudioHandle leverHandle;
    private AudioHandle glowHandle;
    private AudioHandle doorHandle;
    private Action revealReady;
    private bool cameraShotStarted;
    private bool capturedPose;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        CaptureAuthoredPose();
        PrepareEmission();
    }

    public void BeginPull(Action onRevealReady)
    {
        if (IsPlaying)
            return;

        CaptureAuthoredPose();
        ResetMachineVisuals();
        revealReady = onRevealReady;
        IsPlaying = true;

        CameraShotController shots = CameraShotController.Instance;
        if (shots != null && pullShot != null)
        {
            shots.PlayShot(pullShot, cameraTarget != null ? cameraTarget : transform);
            cameraShotStarted = true;
        }

        PlayLeverAudio();

        float glowRampDownStart = glowShakeStart + glowRampUpDuration + glowHoldDuration;
        float glowEnd = glowRampDownStart + glowRampDownDuration;
        float doorStart = Mathf.Max(leverDuration, glowEnd);
        float springDuration = Mathf.Min(leverSpringDuration, leverDuration * 0.45f);
        float turnDuration = Mathf.Max(0.5f, leverDuration - springDuration);
        float overshootDuration = Mathf.Max(0.05f, springDuration * 0.2f);
        float settleDuration = Mathf.Max(0.12f, springDuration - overshootDuration);
        float phase = 0f;
        Vector3 axis = leverLocalAxis.sqrMagnitude > 0.0001f ? leverLocalAxis.normalized : Vector3.forward;

        sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        if (leverRoot != null)
        {
            sequence.Insert(0f, DOTween.To(
                    () => 0f,
                    angle => leverRoot.localRotation = leverAuthoredRotation * Quaternion.AngleAxis(angle, axis),
                    -360f,
                    turnDuration)
                .SetEase(leverCurve));
            sequence.Insert(turnDuration, DOTween.To(
                    () => -360f,
                    angle => leverRoot.localRotation = leverAuthoredRotation * Quaternion.AngleAxis(angle, axis),
                    -360f - leverOvershootDegrees,
                    overshootDuration)
                .SetEase(Ease.OutQuad));
            sequence.Insert(turnDuration + overshootDuration, DOTween.To(
                    () => -360f - leverOvershootDegrees,
                    angle => leverRoot.localRotation = leverAuthoredRotation * Quaternion.AngleAxis(angle, axis),
                    -360f,
                    settleDuration)
                .SetEase(Ease.OutElastic, leverElasticAmplitude, leverElasticPeriod));
        }

        sequence.InsertCallback(glowShakeStart, PlayGlowAudio);
        sequence.Insert(glowShakeStart, DOTween.To(
                () => 0f,
                progress =>
                {
                    float eased = progress * progress * (3f - 2f * progress);
                    phase += Mathf.Lerp(shakeFrequencyRange.x, shakeFrequencyRange.y, progress)
                        * Mathf.PI * 2f * Time.unscaledDeltaTime;
                    if (spheresRoot != null)
                        spheresRoot.localPosition = spheresAuthoredPosition + Vector3.right * (Mathf.Sin(phase) * shakeAmplitude * eased);
                    ApplyEmission(eased);
                },
                1f,
                glowRampUpDuration)
            .SetEase(Ease.InOutSine));

        sequence.InsertCallback(glowRampDownStart, BeginGlowAudioFadeOut);
        sequence.Insert(glowRampDownStart, DOTween.To(
                () => 1f,
                progress =>
                {
                    float eased = progress * progress * (3f - 2f * progress);
                    phase += Mathf.Lerp(shakeFrequencyRange.x, shakeFrequencyRange.y, progress)
                        * Mathf.PI * 2f * Time.unscaledDeltaTime;
                    if (spheresRoot != null)
                        spheresRoot.localPosition = spheresAuthoredPosition + Vector3.right * (Mathf.Sin(phase) * shakeAmplitude * eased);
                    ApplyEmission(eased);
                },
                0f,
                glowRampDownDuration)
            .SetEase(Ease.InOutSine));

        sequence.InsertCallback(doorStart, PlayDoorAudio);
        if (candyMachineDoor != null)
        {
            sequence.Insert(doorStart, DOTween.To(
                    () => 0f,
                    angle => candyMachineDoor.localRotation = doorAuthoredRotation * Quaternion.Euler(angle, 0f, 0f),
                    doorLocalXDegrees,
                    doorDuration)
                .SetEase(Ease.OutCubic));
        }

        sequence.AppendInterval(doorHold);
        sequence.OnComplete(CompletePull);
    }

    public void RestoreAfterReveal()
    {
        StopSequenceAndRestore(0.3f);
    }

    public void CancelAndRestore()
    {
        StopSequenceAndRestore(0f);
    }

    private void CompletePull()
    {
        sequence = null;
        IsPlaying = false;
        StopPresentationAudio();
        if (spheresRoot != null)
            spheresRoot.localPosition = spheresAuthoredPosition;
        Action callback = revealReady;
        revealReady = null;
        callback?.Invoke();
    }

    private void StopSequenceAndRestore(float cameraBlend)
    {
        sequence?.Kill(false);
        sequence = null;
        revealReady = null;
        IsPlaying = false;
        StopPresentationAudio();
        if (doorHandle.IsValid)
            doorHandle.Stop();
        doorHandle = default;
        ResetMachineVisuals();

        if (cameraShotStarted)
        {
            CameraShotController.Instance?.RestoreCameraFollow(cameraBlend);
            cameraShotStarted = false;
        }
    }

    private void CaptureAuthoredPose()
    {
        if (capturedPose)
            return;
        if (leverRoot != null) leverAuthoredRotation = leverRoot.localRotation;
        if (candyMachineDoor != null) doorAuthoredRotation = candyMachineDoor.localRotation;
        if (spheresRoot != null) spheresAuthoredPosition = spheresRoot.localPosition;
        capturedPose = true;
    }

    private void PrepareEmission()
    {
        propertyBlock ??= new MaterialPropertyBlock();
        if (emissiveRenderer == null)
            return;
        emissiveRenderer.gameObject.SetActive(false);
        if (emissiveRenderer.sharedMaterial == null)
            return;
        Material material = emissiveRenderer.sharedMaterial;
        if (material.HasProperty(BaseColorId)) authoredBaseColor = material.GetColor(BaseColorId);
        if (material.HasProperty(EmissionColorId)) authoredEmissionColor = material.GetColor(EmissionColorId);
    }

    private void ApplyEmission(float progress)
    {
        if (emissiveRenderer == null)
            return;

        // DOTween can evaluate an inserted tween once at its zero value while
        // the sequence is being constructed or killed. Do not let that zero
        // sample reactivate the authored emissive object after cancellation.
        if (progress <= 0.0001f)
        {
            emissiveRenderer.SetPropertyBlock(null);
            emissiveRenderer.gameObject.SetActive(false);
            return;
        }

        if (!emissiveRenderer.gameObject.activeSelf)
            emissiveRenderer.gameObject.SetActive(true);

        propertyBlock ??= new MaterialPropertyBlock();
        emissiveRenderer.GetPropertyBlock(propertyBlock);
        Color baseColor = authoredBaseColor;
        baseColor.a = Mathf.Clamp01(progress);
        propertyBlock.SetColor(BaseColorId, baseColor);
        propertyBlock.SetColor(EmissionColorId, authoredEmissionColor * (maximumEmission * progress));
        emissiveRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ResetMachineVisuals()
    {
        if (leverRoot != null) leverRoot.localRotation = leverAuthoredRotation;
        if (candyMachineDoor != null) candyMachineDoor.localRotation = doorAuthoredRotation;
        if (spheresRoot != null) spheresRoot.localPosition = spheresAuthoredPosition;
        if (emissiveRenderer != null)
        {
            emissiveRenderer.SetPropertyBlock(null);
            emissiveRenderer.gameObject.SetActive(false);
        }
    }

    private void PlayLeverAudio()
    {
        if (leverSound == null)
            return;
        AudioService audio = AudioService.Instance;
        if (audio != null)
            leverHandle = audio.Play(leverSound);
    }

    private void PlayGlowAudio()
    {
        if (glowSound == null)
            return;
        AudioService audio = AudioService.Instance;
        if (audio != null)
            glowHandle = audio.Play(glowSound);
    }

    private void StopGlowAudio(float fadeOutSeconds = 0f)
    {
        if (glowHandle.IsValid)
            glowHandle.Stop(fadeOutSeconds);
        glowHandle = default;
    }

    private void BeginGlowAudioFadeOut()
    {
        if (glowHandle.IsValid)
            glowHandle.Stop(glowRampDownDuration);
    }

    private void PlayDoorAudio()
    {
        if (doorSound == null)
            return;
        AudioService audio = AudioService.Instance;
        if (audio != null)
            doorHandle = audio.Play(doorSound);
    }

    private void StopPresentationAudio()
    {
        if (leverHandle.IsValid)
            leverHandle.Stop(0.08f);
        leverHandle = default;
        StopGlowAudio(0.08f);
    }

    private void OnDisable() => CancelAndRestore();

    private void OnDestroy() => CancelAndRestore();
}
