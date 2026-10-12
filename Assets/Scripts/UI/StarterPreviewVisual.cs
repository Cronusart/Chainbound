using System.Collections;
using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CombatSpriteAnimationSequence = Chainbound.Combat.SpriteAnimationSequence;

/// <summary>
/// UI-friendly wrapper for starter preview playback.
/// Reuses CombatVisualProfileSO sprite sequences but renders them through UGUI.
/// </summary>
public class StarterPreviewVisual : MonoBehaviour
{
    [SerializeField] private Image previewImage;
    [SerializeField] private Image flashOverlayImage;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private UIImageFrameAnimator frameAnimator;
    [Tooltip("Resolved default victory pose for starter celebration. Empty frame data safely falls back to swap-in.")]
    [SerializeField] private ChainlingVictoryPoseDefinitionSO defaultVictoryPose;
    [SerializeField, Min(0f)] private float revealDelaySeconds = 0.18f;
    [Tooltip("UI-compatible Signal Glitch material used across the combat swap-in frame sequence.")]
    [SerializeField] private Material signalGlitchMaterial;
    [SerializeField, Min(0.05f)] private float glitchInDuration = 0.8f;
    [Tooltip("Combat SignalGlitchEffect GlitchIn intensity curve (1 -> 0).")]
    [SerializeField] private AnimationCurve glitchInCurve = CreateCombatGlitchInCurve();
    [Tooltip("Combat GlitchIn scale curve remapped so UI previews settle at 1x instead of the battlefield 1.2x visual root.")]
    [SerializeField] private AnimationCurve glitchInScaleCurve = CreateUiGlitchInScaleCurve();
    [Tooltip("Optional explicit bridge. When empty, the shared combat audio bridge is discovered automatically.")]
    [SerializeField] private MonoBehaviour audioBridgeBehaviour;
    [SerializeField] private bool enableDebugLogs = false;

    private Coroutine playbackRoutine;
    private ICombatAudioBridge audioBridge;
    private Material runtimeGlitchMaterial;
    private Material originalPreviewMaterial;
    private bool originalMaterialCaptured;
    private Tween glitchTween;
    private CombatVisualProfileSO activeProfile;
    private Sprite activeFallbackSprite;

    /// <summary>
    /// True once the glitch-in and swap-in have finished and idle playback has started.
    /// </summary>
    public bool HasSettledReveal { get; private set; }

    private static readonly int GlitchIntensityId = Shader.PropertyToID("_GlitchIntensity");
    private static readonly int DissolveThresholdId = Shader.PropertyToID("_DissolveThreshold");
    private static readonly int MaterialColorId = Shader.PropertyToID("_Color");
    // The UI material is a runtime instance so the authored prefab material is never mutated.

    public void Initialize(
        Image previewImageReference,
        Image flashOverlayImageReference,
        RectTransform visualRootReference,
        UIImageFrameAnimator frameAnimatorReference)
    {
        previewImage = previewImageReference;
        flashOverlayImage = flashOverlayImageReference;
        visualRoot = visualRootReference;
        frameAnimator = frameAnimatorReference;
    }

    public void Play(CombatVisualProfileSO profile, Sprite fallbackSprite = null)
    {
        Clear();
        activeProfile = profile;
        activeFallbackSprite = fallbackSprite;
        HasSettledReveal = false;

        if (previewImage == null || visualRoot == null)
        {
            HasSettledReveal = true;
            return;
        }

        playbackRoutine = StartCoroutine(PlayRoutine(profile, fallbackSprite));
    }

    /// <summary>
    /// Plays the configured reusable victory pose when it has frames. Until
    /// authored pose frames arrive, starter previews use their normal swap-in
    /// flipbook and settle back to idle instead of failing silently.
    /// </summary>
    public float PlayCelebration()
    {
        if (previewImage == null || visualRoot == null)
            return 0f;

        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
        if (frameAnimator != null)
            frameAnimator.Stop();

        List<Sprite> victoryFrames = ToFrameList(defaultVictoryPose != null ? defaultVictoryPose.sequence : null);
        List<Sprite> fallbackFrames = ToFrameList(activeProfile != null ? activeProfile.swapInSequence : null);
        List<Sprite> celebrationFrames = victoryFrames.Count > 0 ? victoryFrames : fallbackFrames;
        float framesPerSecond = victoryFrames.Count > 0
            ? Mathf.Max(1f, defaultVictoryPose.fps)
            : activeProfile != null ? Mathf.Max(1f, activeProfile.swapInFPS) : 8f;
        float duration = celebrationFrames.Count > 0
            ? Mathf.Max(0.05f, (celebrationFrames.Count - 1) / framesPerSecond)
            : 0.2f;

        playbackRoutine = StartCoroutine(PlayCelebrationRoutine(celebrationFrames, framesPerSecond, duration));
        return duration;
    }

    public void Clear()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        if (frameAnimator != null)
            frameAnimator.Stop();

        StopSignalGlitch();
        HasSettledReveal = false;

        if (previewImage != null)
        {
            previewImage.sprite = null;
            previewImage.enabled = false;
            previewImage.color = new Color(1f, 1f, 1f, 0f);
        }

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = Vector3.one;
        }

        if (flashOverlayImage != null)
        {
            flashOverlayImage.DOKill();
            flashOverlayImage.color = new Color(0.55f, 1f, 0.95f, 0f);
            flashOverlayImage.sprite = null;
        }
    }

    private void OnDestroy()
    {
        StopSignalGlitch();

        if (runtimeGlitchMaterial != null)
            Destroy(runtimeGlitchMaterial);
    }

    private IEnumerator PlayRoutine(CombatVisualProfileSO profile, Sprite fallbackSprite)
    {
        Sprite fallback = ResolveFallbackSprite(profile, fallbackSprite);
        List<Sprite> swapInFrames = ToFrameList(profile != null ? profile.swapInSequence : null);
        List<Sprite> idleFrames = ToFrameList(profile != null ? profile.idleSequence : null);

        // Keep the frame empty while the panel settles, then start the same
        // swap-in/glitch flipbook used by combat. Use realtime so the intro
        // still presents correctly if a sequence has paused game time.
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, revealDelaySeconds));

        if (swapInFrames.Count > 0)
        {
            Sprite firstSwapFrame = swapInFrames[0] ?? fallback;
            // Combat's SignalGlitchEffect resolves the currently visible
            // chainling first. The profile's SwapIn flipbook starts only after
            // that effect completes, so do not feed a swap/KO frame into the
            // glitch material itself.
            Sprite glitchBaseFrame = idleFrames.Count > 0 ? idleFrames[0] : fallback;
            previewImage.sprite = glitchBaseFrame;
            previewImage.enabled = glitchBaseFrame != null;
            previewImage.color = new Color(1f, 1f, 1f, 0.98f);
            visualRoot.localScale = Vector3.zero;

            float swapDuration = profile != null
                ? ComputeSequenceDuration(profile.swapInSequence, profile.swapInFPS)
                : 0.24f;
            // Combat's SignalGlitchEffect is authored at 0.8 seconds on the
            // battlefield prefab. Keep that effect duration authoritative for
            // this UI presentation, then let the combat-authored swap frames
            // run to completion before settling into idle.
            bool glitchStarted = PlaySignalGlitch(glitchInDuration);
            if (glitchStarted)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, glitchInDuration));
                StopSignalGlitch();
            }
            else
            {
                visualRoot.localScale = Vector3.one;
            }

            // Combat's spawn cue is emitted once the glitch overlay has
            // resolved, immediately before the real swap-in presentation.
            PlaySpawnSound(profile);
            PlayFlash(firstSwapFrame);

            if (frameAnimator != null)
            {
                frameAnimator.SetFPS(profile != null ? Mathf.Max(1f, profile.swapInFPS) : 12f);
                frameAnimator.SetLoop(false);
                frameAnimator.Play(swapInFrames);
            }

            yield return new WaitForSecondsRealtime(Mathf.Max(0.2f, swapDuration));
        }
        else
        {
            previewImage.sprite = fallback;
            previewImage.enabled = fallback != null;
            previewImage.color = Color.white;
            visualRoot.localScale = Vector3.zero;
            if (fallback != null && PlaySignalGlitch(glitchInDuration))
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, glitchInDuration));
                StopSignalGlitch();
            }
            else
            {
                visualRoot.localScale = Vector3.one;
            }

            PlaySpawnSound(profile);
            PlayFlash(fallback);
        }

        if (idleFrames.Count > 0 && frameAnimator != null)
        {
            frameAnimator.SetFPS(profile != null ? Mathf.Max(1f, profile.idleFPS) : 4f);
            frameAnimator.SetLoop(true);
            frameAnimator.Play(idleFrames);
        }
        else
        {
            previewImage.sprite = fallback;
            previewImage.enabled = fallback != null;
            if (frameAnimator != null)
                frameAnimator.Stop();
        }

        HasSettledReveal = true;

        if (enableDebugLogs)
            Debug.Log($"[StarterPreviewVisual] Playing preview for '{profile?.speciesId ?? fallback?.name ?? "unknown"}'.");
    }

    private IEnumerator PlayCelebrationRoutine(List<Sprite> celebrationFrames, float framesPerSecond, float duration)
    {
        Sprite fallback = ResolveFallbackSprite(activeProfile, activeFallbackSprite);
        visualRoot.localScale = Vector3.one;
        previewImage.enabled = true;
        previewImage.color = Color.white;

        if (celebrationFrames.Count > 0)
        {
            previewImage.sprite = celebrationFrames[0];
            PlayFlash(celebrationFrames[0]);
            if (frameAnimator != null)
            {
                frameAnimator.SetFPS(framesPerSecond);
                frameAnimator.SetLoop(false);
                frameAnimator.Play(celebrationFrames);
            }
        }
        else
        {
            previewImage.sprite = fallback;
            PlayFlash(fallback);
        }

        yield return new WaitForSecondsRealtime(duration);

        List<Sprite> idleFrames = ToFrameList(activeProfile != null ? activeProfile.idleSequence : null);
        if (idleFrames.Count > 0 && frameAnimator != null)
        {
            frameAnimator.SetFPS(activeProfile != null ? Mathf.Max(1f, activeProfile.idleFPS) : 4f);
            frameAnimator.SetLoop(true);
            frameAnimator.Play(idleFrames);
        }
        else
        {
            if (frameAnimator != null)
                frameAnimator.Stop();
            previewImage.sprite = fallback;
            previewImage.enabled = fallback != null;
        }

        playbackRoutine = null;
    }

    private void PlaySpawnSound(CombatVisualProfileSO profile)
    {
        if (profile == null)
            return;

        UnityEngine.Object sound = profile.swapInSound != null
            ? profile.swapInSound
            : profile.spawnSound;
        if (sound == null)
            return;

        audioBridge = CombatAudioBridgeResolver.Resolve(
            audioBridgeBehaviour,
            autoDiscover: true,
            out _);
        audioBridge?.PlayOneShot(sound);
    }

    private void PlayFlash(Sprite sprite)
    {
        if (flashOverlayImage == null)
            return;

        flashOverlayImage.DOKill();
        flashOverlayImage.sprite = sprite;
        flashOverlayImage.color = new Color(0.55f, 1f, 0.95f, 0.65f);
        flashOverlayImage
            .DOFade(0f, 0.34f)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);
    }

    private bool PlaySignalGlitch(float duration)
    {
        if (previewImage == null)
            return false;

        CaptureOriginalPreviewMaterial();
        EnsureGlitchMaterial();
        if (runtimeGlitchMaterial == null)
            return false;

        StopSignalGlitch();
        previewImage.material = runtimeGlitchMaterial;
        SetGlitchMaterialValues(1f, 0f);

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = Vector3.zero;
        }

        float safeDuration = Mathf.Max(0.05f, duration);
        Tween intensityTween = DOTween.To(
                () => 0f,
                progress =>
                {
                    float intensity = glitchInCurve != null
                        ? glitchInCurve.Evaluate(progress)
                        : Mathf.Lerp(1f, 0f, progress);
                    // Combat GlitchIn never dissolves the source. Dissolve is
                    // reserved for GlitchOut and was making this UI preview
                    // look like it was fading out in reverse.
                    SetGlitchMaterialValues(intensity, 0f);
                },
                1f,
                safeDuration).SetEase(Ease.Linear);
        Tween scaleTween = DOTween.To(
                () => 0f,
                progress =>
                {
                    if (visualRoot == null)
                        return;

                    float scale = glitchInScaleCurve != null
                        ? glitchInScaleCurve.Evaluate(progress)
                        : progress;
                    visualRoot.localScale = Vector3.one * scale;
                },
                1f,
                safeDuration).SetEase(Ease.Linear);
        glitchTween = DOTween.Sequence()
            .Append(intensityTween)
            .Join(scaleTween)
            .SetUpdate(true)
            .OnComplete(StopSignalGlitch);
        return true;
    }

    private void StopSignalGlitch()
    {
        glitchTween?.Kill();
        glitchTween = null;

        if (runtimeGlitchMaterial != null)
            SetGlitchMaterialValues(0f, 0f);

        if (previewImage != null && originalMaterialCaptured)
            previewImage.material = originalPreviewMaterial;

        if (visualRoot != null)
        {
            visualRoot.DOKill();
            visualRoot.localScale = Vector3.one;
        }
    }

    private void CaptureOriginalPreviewMaterial()
    {
        if (originalMaterialCaptured || previewImage == null)
            return;

        originalPreviewMaterial = previewImage.material;
        originalMaterialCaptured = true;
    }

    private void EnsureGlitchMaterial()
    {
        if (runtimeGlitchMaterial != null)
            return;

        Material source = signalGlitchMaterial;
        if (source != null)
        {
            runtimeGlitchMaterial = new Material(source)
            {
                name = $"{source.name} (Starter Preview Runtime)"
            };
        }
        else
        {
            Shader shader = Shader.Find("SignalJam/UI/SignalGlitchOverlay");
            if (shader != null)
                runtimeGlitchMaterial = new Material(shader) { name = "Signal Glitch UI (Starter Preview Runtime)" };
        }

        if (runtimeGlitchMaterial != null && runtimeGlitchMaterial.HasProperty(MaterialColorId))
            runtimeGlitchMaterial.SetColor(MaterialColorId, Color.white);
    }

    private void SetGlitchMaterialValues(float intensity, float dissolve)
    {
        if (runtimeGlitchMaterial == null)
            return;

        if (runtimeGlitchMaterial.HasProperty(GlitchIntensityId))
            runtimeGlitchMaterial.SetFloat(GlitchIntensityId, Mathf.Clamp01(intensity));
        if (runtimeGlitchMaterial.HasProperty(DissolveThresholdId))
            runtimeGlitchMaterial.SetFloat(DissolveThresholdId, Mathf.Clamp01(dissolve));
    }

    private static AnimationCurve CreateCombatGlitchInCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.7172166f, 0.49675262f, -1.4436774f, -1.4436774f),
            new Keyframe(1f, 0f, -1f, -1f));
    }

    private static AnimationCurve CreateUiGlitchInScaleCurve()
    {
        return NormalizeCurveSettle(CreateCombatGlitchInScaleCurve(), 1f);
    }

    private static AnimationCurve NormalizeCurveSettle(AnimationCurve source, float settleValue)
    {
        if (source == null || source.length == 0)
            return AnimationCurve.Linear(0f, 0f, 1f, settleValue);

        Keyframe last = source.keys[source.length - 1];
        if (Mathf.Abs(last.value) < 0.0001f)
            return source;

        float factor = settleValue / last.value;
        Keyframe[] keys = source.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value *= factor;
            keys[i].inTangent *= factor;
            keys[i].outTangent *= factor;
        }

        return new AnimationCurve(keys);
    }

    private static AnimationCurve CreateCombatGlitchInScaleCurve()
    {
        return new AnimationCurve(
            new Keyframe(0.0038502524f, 0.0005296171f, 25.340723f, 25.340723f),
            new Keyframe(0.043183833f, 0.99727106f, 10.083976f, 10.083976f),
            new Keyframe(0.13192067f, 1.304922f, 0.026912455f, 0.026912455f),
            new Keyframe(0.3209595f, 1.1221685f, 1.843863f, 1.843863f),
            new Keyframe(0.5507654f, 1.2149935f, -0.8986799f, -0.8986799f),
            new Keyframe(0.81117636f, 1.2170436f, 0.14453983f, 0.14453983f),
            new Keyframe(0.99719346f, 1.2005126f, -0.088868126f, -0.088868126f));
    }

    private static float ComputeSequenceDuration(CombatSpriteAnimationSequence sequence, float fps)
    {
        int frameCount = sequence != null ? sequence.Length : 0;
        if (frameCount <= 1)
            return 0f;

        float safeFps = Mathf.Max(1f, fps);
        return (frameCount - 1) / safeFps;
    }

    private static List<Sprite> ToFrameList(CombatSpriteAnimationSequence sequence)
    {
        var frames = new List<Sprite>();
        if (sequence?.sprites == null)
            return frames;

        for (int i = 0; i < sequence.sprites.Length; i++)
        {
            if (sequence.sprites[i] != null)
                frames.Add(sequence.sprites[i]);
        }

        return frames;
    }

    private static Sprite ResolveFallbackSprite(CombatVisualProfileSO profile, Sprite fallbackSprite)
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        if (profile != null && profile.portrait != null)
            return profile.portrait;

        if (profile != null && profile.idleSequence != null && profile.idleSequence.Length > 0)
            return profile.idleSequence[0];

        if (profile != null && profile.swapInSequence != null && profile.swapInSequence.Length > 0)
            return profile.swapInSequence[0];

        return null;
    }
}
