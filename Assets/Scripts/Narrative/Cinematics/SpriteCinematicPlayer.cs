using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Chainbound.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public enum SpriteCinematicResultKind { Completed, Skipped, Failed }

public readonly struct SpriteCinematicResult
{
    public SpriteCinematicResult(SpriteCinematicResultKind kind, string message = "") { Kind = kind; Message = message ?? ""; }
    public SpriteCinematicResultKind Kind { get; }
    public string Message { get; }
}

/// <summary>
/// Interprets <see cref="SpriteCinematicDefinitionSO"/> shots.
/// Layered shots use a scene <see cref="SpriteCinematicStageBinder"/> (middle
/// flipbook between looping BG/FG). FullScreen shots use an overlay canvas.
/// </summary>
public sealed class SpriteCinematicPlayer : MonoBehaviour
{
    private const int SortingOrderPlayback = 30000;
    /// <summary>
    /// Below NameInputCanvasRoot (420) and KeyboardCanvasRoot (1500) so OSK + name UI
    /// remain visible over the held monitor frame.
    /// </summary>
    private const int SortingOrderNameHandoff = 300;
    private const float MissingFrameDuration = 0.25f;
    private const float DefaultDisplayScale = 1.1f;
    private const float BlackRevealSeconds = 0.2f;
    private const float DefaultNameHandoffDuration = 0.35f;

    public static SpriteCinematicPlayer Instance { get; private set; }

    private GameObject root;
    private Canvas rootCanvas;
    private Image fullScreenImage;
    private RectTransform fullScreenImageRect;
    private Image flashImage;
    private Image blackImage;
    private GameObject skipPrompt;
    private CoinNameFieldOverlay coinNameOverlayInstance;
    private bool playing;
    private bool touchSkipHeld;
    private float heldSkipSeconds;
    private ChromaticAberration chromatic;
    private float originalChromatic;
    private bool capturedChromatic;
    private SpriteCinematicStageBinder stage;
    private bool holdingLastFrame;
    private bool coinNameMirrorEnabled;
    private bool nameHandoffZoomActive;
    private TaskCompletionSource<SpriteCinematicResult> activePlaybackSource;
    private float nameHandoffRestScale = DefaultDisplayScale;
    private float nameHandoffZoomScale = 1.6f;
    private float nameHandoffOffsetY = 100f;
    private float nameHandoffDurationSeconds = DefaultNameHandoffDuration;
    private float currentPresentationOffsetY;
    private readonly List<ActiveFrameAudioCue> activeFrameAudioCues = new List<ActiveFrameAudioCue>();

    private struct ActiveFrameAudioCue
    {
        public SpriteCinematicFrameAudioCue Cue;
        public AudioHandle Handle;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDisable()
    {
        if (!holdingLastFrame)
            CleanupVisualState(forceHide: true);
    }

    private void OnDestroy()
    {
        CleanupVisualState(forceHide: true);
        if (Instance == this) Instance = null;
    }

    public bool IsCoinNameMirrorActive =>
        coinNameMirrorEnabled &&
        coinNameOverlayInstance != null &&
        coinNameOverlayInstance.gameObject.activeInHierarchy;

    /// <summary>
    /// Brings the held monitor name field back after a refused name.
    /// The overlay stays the input surface for every following attempt.
    /// </summary>
    public void ResumeCoinNameMirror()
    {
        if (coinNameOverlayInstance == null)
            return;

        coinNameMirrorEnabled = true;
        coinNameOverlayInstance.Show();
    }

    /// <summary>Updates the diegetic coin-name field overlay while a last frame is held for name entry.</summary>
    public void SetCoinNameMirrorText(string text)
    {
        if (coinNameOverlayInstance == null || !coinNameMirrorEnabled)
            return;

        string trimmed = text != null ? text.TrimStart() : string.Empty;
        coinNameOverlayInstance.SetText(trimmed);
        coinNameOverlayInstance.Show();
    }

    public void ClearCoinNameMirror()
    {
        coinNameMirrorEnabled = false;
        if (coinNameOverlayInstance == null)
            return;

        coinNameOverlayInstance.Hide();
    }

    /// <summary>
    /// Tweens the held name-entry frame from the zoomed handoff pose back to rest scale/offset
    /// before the post-name cinematic continues.
    /// </summary>
    public Task AnimateNameHandoffZoomOut()
    {
        var source = new TaskCompletionSource<bool>();
        if (!holdingLastFrame || !nameHandoffZoomActive || root == null || !root.activeInHierarchy)
        {
            ClearCoinNameMirror();
            ApplyPresentationScale(nameHandoffRestScale > 0.01f ? nameHandoffRestScale : DefaultDisplayScale);
            ApplyPresentationOffset(0f);
            nameHandoffZoomActive = false;
            source.TrySetResult(false);
            return source.Task;
        }

        StartCoroutine(AnimateNameHandoffZoomOutRoutine(source));
        return source.Task;
    }

    public Task<SpriteCinematicResult> Play(SpriteCinematicDefinitionSO definition)
    {
        var source = new TaskCompletionSource<SpriteCinematicResult>();
        if (playing)
        {
            source.TrySetResult(new SpriteCinematicResult(SpriteCinematicResultKind.Failed, "A sprite cinematic is already playing."));
            return source.Task;
        }

        bool continueFromHeldFrame = definition != null && definition.continueFromHeldFrame && holdingLastFrame;
        if (continueFromHeldFrame)
            PrepareContinueFromHeldFrame();
        else
            ReleaseHeldLastFrame();

        activePlaybackSource = source;
        StartCoroutine(PlayRoutine(definition, source, continueFromHeldFrame));
        return source.Task;
    }

    /// <summary>
    /// Hard-stop playback and hide overlays. Used by debug skips so name-handoff
    /// cinematics cannot keep running across a scene load.
    /// </summary>
    public void AbortPlayback()
    {
        StopAllCoroutines();
        playing = false;
        AbortLayeredStage();
        CleanupVisualState(forceHide: true, keepGlitch: false);
        if (activePlaybackSource != null)
        {
            activePlaybackSource.TrySetResult(
                new SpriteCinematicResult(SpriteCinematicResultKind.Skipped, "Aborted for debug skip."));
            activePlaybackSource = null;
        }
    }

    private IEnumerator PlayRoutine(
        SpriteCinematicDefinitionSO definition,
        TaskCompletionSource<SpriteCinematicResult> source,
        bool continueFromHeldFrame)
    {
        playing = true;
        holdingLastFrame = false;
        stage = SpriteCinematicStageBinder.Active != null
            ? SpriteCinematicStageBinder.Active
            : FindObjectOfType<SpriteCinematicStageBinder>();

        EnsureCanvas();
        SetCanvasSortingOrder(SortingOrderPlayback);
        root.SetActive(true);
        if (continueFromHeldFrame)
        {
            SetColorAlpha(blackImage, 0f);
            SetColorAlpha(flashImage, 0f);
            SetVisible(fullScreenImage, true);
        }
        else
        {
            // Layered shots composite into the scene (middle between BG/FG). Covering
            // them with BlackFade would flash a full-screen plate before playback.
            // Full-screen shots still start covered so a previous frame cannot peek.
            bool coverUntilFirstFrame = NeedsOpeningBlackCover(definition, stage);
            if (coverUntilFirstFrame)
                SetColorAlpha(blackImage, 1f);
            else
                ClearBlackCoverImmediate();
            SetColorAlpha(flashImage, 0f);
            SetVisible(fullScreenImage, false);
        }
        ClearCoinNameMirror();
        skipPrompt.SetActive(false);
        bool skipped = false;
        float timelineElapsed = 0f;
        bool keepLastFrame = false;
        bool remainCoveredForLoad = false;

        if (definition == null)
        {
            yield return Fade(blackImage, 1f, 0f, 0.12f);
            yield return Fade(blackImage, 0f, 1f, 0.12f);
            Finish(source, new SpriteCinematicResult(SpriteCinematicResultKind.Failed, "Sprite cinematic definition is missing."), keepPresentation: false);
            yield break;
        }

        foreach (SpriteCinematicShot shot in definition.shots)
        {
            if (shot == null) continue;
            if (shot.audioCue != null && AudioService.Instance != null)
                AudioService.Instance.Play(shot.audioCue);
            if (shot.fadeMenuMusicSeconds > 0f && !shot.enableCreepEffects)
                FadeMenuMusic(shot, shot.fadeMenuMusicSeconds);
            bool shotSkipped = false;
            yield return PlayShot(shot, definition, () => timelineElapsed, value => timelineElapsed = value, value => shotSkipped = value);
            StopActiveFrameAudioCues();
            if (shotSkipped) { skipped = true; break; }
            keepLastFrame = shot.remainLastFrameVisible && shot.composition == SpriteCinematicComposition.FullScreen;
            remainCoveredForLoad = shot.remainBlackAfterShot;
            if (keepLastFrame)
            {
                yield return AnimateNameHandoffZoomIn(shot);
                if (shot.enableCoinNameMirror)
                    EnableCoinNameMirror(definition);
            }
        }

        if (skipped)
            yield return Fade(blackImage, blackImage.color.a, 1f, definition.skipFadeToBlackSeconds);
        else if (!keepLastFrame && blackImage != null && blackImage.color.a < 0.99f)
            yield return Fade(blackImage, blackImage.color.a, 1f, 0.01f);

        stage?.StopCompanionAnimations();
        stage?.HideMiddleImmediate();
        stage?.HideStageVisuals();

        yield return PersistCompletionCheckpoint(definition);
        Finish(
            source,
            new SpriteCinematicResult(skipped ? SpriteCinematicResultKind.Skipped : SpriteCinematicResultKind.Completed),
            keepPresentation: (keepLastFrame || remainCoveredForLoad) && !skipped,
            keepLoadCover: remainCoveredForLoad && !keepLastFrame && !skipped);
    }

    private IEnumerator PlayShot(
        SpriteCinematicShot shot,
        SpriteCinematicDefinitionSO definition,
        Func<float> getElapsed,
        Action<float> setElapsed,
        Action<bool> setSkipped)
    {
        bool layered = shot.composition == SpriteCinematicComposition.Layered;
        bool useStage = layered && stage != null && stage.HasMiddleTarget;

        if (layered && !useStage)
        {
            Debug.LogWarning(
                "[SpriteCinematicPlayer] Layered shot requires a SpriteCinematicStageBinder with a middle animator. " +
                "Falling back to full-screen for this shot.");
            layered = false;
        }

        ApplyShotComposition(layered);
        ApplyPresentationScale(ResolveStartScale(shot));

        if (layered)
            yield return PlayLayeredShot(shot, definition, getElapsed, setElapsed, setSkipped);
        else
            yield return PlayFullScreenShot(shot, definition, getElapsed, setElapsed, setSkipped);
    }

    private IEnumerator PlayLayeredShot(
        SpriteCinematicShot shot,
        SpriteCinematicDefinitionSO definition,
        Func<float> getElapsed,
        Action<float> setElapsed,
        Action<bool> setSkipped)
    {
        List<Sprite> frames = shot.frames;
        int frameCount = frames != null ? frames.Count : 0;
        bool skippedLocal = false;
        Action<bool> markSkip = value =>
        {
            if (!value) return;
            skippedLocal = true;
            setSkipped(true);
        };

        float startScale = ResolveStartScale(shot);
        float endScale = ResolveEndScale(shot);
        float fps = Mathf.Max(0.01f, shot.framesPerSecond);
        float flipbookDuration = frameCount > 0
            ? frameCount / fps
            : MissingFrameDuration;
        float zoomElapsed = 0f;
        int lastAudioFrame = -1;

        stage.BeginLayeredPresentation();
        ApplyStageMiddleScale(startScale);
        stage.PrepareMiddleFrames(frames, shot.framesPerSecond);
        ClearBlackCoverImmediate();
        stage.PlayMiddleFrames(frames, shot.framesPerSecond);

        if (frameCount == 0)
        {
            ProcessFrameAudio(shot, 0);
            yield return WaitWithSkip(MissingFrameDuration, definition, getElapsed, setElapsed, markSkip);
            ApplyStageMiddleScale(endScale);
        }
        else
        {
            ProcessFrameAudio(shot, 0);
            lastAudioFrame = 0;

            while (stage.IsMiddlePlaying)
            {
                if (ShouldSkip(definition, getElapsed()))
                {
                    markSkip(true);
                    break;
                }

                float dt = Time.unscaledDeltaTime;
                zoomElapsed += dt;
                int frameIndex = Mathf.Clamp(Mathf.FloorToInt(zoomElapsed * fps), 0, frameCount - 1);
                if (frameIndex != lastAudioFrame)
                {
                    ProcessFrameAudio(shot, frameIndex);
                    lastAudioFrame = frameIndex;
                }

                float t = flipbookDuration <= 0f ? 1f : Mathf.Clamp01(zoomElapsed / flipbookDuration);
                ApplyStageMiddleScale(Mathf.Lerp(startScale, endScale, t));
                setElapsed(getElapsed() + dt);
                yield return null;
            }

            ApplyStageMiddleScale(endScale);
        }

        if (skippedLocal)
        {
            AbortLayeredStage();
            yield break;
        }

        float hold = shot.holdLastFrameSeconds;
        while (hold > 0f)
        {
            if (ShouldSkip(definition, getElapsed()))
            {
                markSkip(true);
                AbortLayeredStage();
                yield break;
            }

            float dt = Time.unscaledDeltaTime;
            hold -= dt;
            setElapsed(getElapsed() + dt);
            yield return null;
        }

        stage.StopCompanionAnimations();
        yield return PlayFlashAndBlack(shot, definition, getElapsed, setElapsed, markSkip);
        stage.HideMiddleImmediate();
        ApplyStageMiddleScale(DefaultDisplayScale);
    }

    private void AbortLayeredStage()
    {
        StopActiveFrameAudioCues();
        stage?.StopCompanionAnimations();
        stage?.HideMiddleImmediate();
        ApplyStageMiddleScale(DefaultDisplayScale);
    }

    private IEnumerator PlayFullScreenShot(
        SpriteCinematicShot shot,
        SpriteCinematicDefinitionSO definition,
        Func<float> getElapsed,
        Action<float> setElapsed,
        Action<bool> setSkipped)
    {
        stage?.BeginFullScreenPresentation();

        int frameCount = shot.frames != null ? shot.frames.Count : 0;
        int framesToPlay = Mathf.Max(1, frameCount);
        float frameDuration = 1f / Mathf.Max(0.01f, shot.framesPerSecond);
        float flipbookDuration = framesToPlay * (frameCount > 0 ? frameDuration : MissingFrameDuration);
        float zoomElapsed = 0f;
        bool markerTriggered = false;

        ApplyPresentationScale(ResolveStartScale(shot));
        ApplyPresentationOffset(0f);
        SetVisible(fullScreenImage, true);
        fullScreenImage.sprite = frameCount > 0 ? shot.frames[0] : null;
        yield return RevealFromBlackIfNeeded();

        float creepDuration = flipbookDuration + Mathf.Max(0f, shot.holdLastFrameSeconds);
        if (shot.enableCreepEffects)
        {
            StartCoroutine(PlayCreepEffects(shot, creepDuration));
            if (shot.fadeMenuMusicSeconds > 0f)
                FadeMenuMusic(shot, creepDuration);
        }

        for (int index = 0; index < framesToPlay; index++)
        {
            fullScreenImage.sprite = frameCount > 0 ? shot.frames[index] : null;
            ProcessFrameAudio(shot, index);

            if (!markerTriggered && !shot.enableCreepEffects && shot.markerFrame == index)
            {
                markerTriggered = true;
                StartCoroutine(PlayMarkerEffects(shot));
            }

            float elapsed = 0f;
            float stepDuration = frameCount > 0 ? frameDuration : MissingFrameDuration;
            while (elapsed < stepDuration)
            {
                if (ShouldSkip(definition, getElapsed()))
                {
                    setSkipped(true);
                    StopActiveFrameAudioCues();
                    yield break;
                }
                float dt = Time.unscaledDeltaTime;
                elapsed += dt;
                zoomElapsed += dt;
                float t = flipbookDuration <= 0f ? 1f : Mathf.Clamp01(zoomElapsed / flipbookDuration);
                ApplyPresentationScale(Mathf.Lerp(ResolveStartScale(shot), ResolveEndScale(shot), t));
                setElapsed(getElapsed() + dt);
                yield return null;
            }
        }

        ApplyPresentationScale(ResolveEndScale(shot));

        float hold = shot.holdLastFrameSeconds;
        while (hold > 0f)
        {
            if (ShouldSkip(definition, getElapsed()))
            {
                setSkipped(true);
                StopActiveFrameAudioCues();
                yield break;
            }
            float dt = Time.unscaledDeltaTime;
            hold -= dt;
            setElapsed(getElapsed() + dt);
            yield return null;
        }

        if (shot.remainLastFrameVisible)
        {
            // Keep the monitor frame up for name entry; skip fade-to-black handoff.
            SetColorAlpha(blackImage, 0f);
            yield break;
        }

        yield return PlayFlashAndBlack(shot, definition, getElapsed, setElapsed, setSkipped);
    }

    /// <summary>
    /// Fade to black / flash, then keep black opaque and clear the previous frame
    /// underneath so the next shot can paint before reveal.
    /// </summary>
    private IEnumerator PlayFlashAndBlack(
        SpriteCinematicShot shot,
        SpriteCinematicDefinitionSO definition,
        Func<float> getElapsed,
        Action<float> setElapsed,
        Action<bool> setSkipped)
    {
        if (shot.cameraFlash)
        {
            yield return Fade(flashImage, 0f, 1f, shot.flashRiseSeconds);
            yield return WaitWithSkip(shot.flashHoldSeconds, definition, getElapsed, setElapsed, setSkipped);
            yield return Fade(flashImage, 1f, 0f, shot.flashFadeSeconds);
        }

        if (shot.fadeToBlackSeconds > 0f)
            yield return Fade(blackImage, blackImage.color.a, 1f, shot.fadeToBlackSeconds);
        if (shot.blackHoldSeconds > 0f)
            yield return WaitWithSkip(shot.blackHoldSeconds, definition, getElapsed, setElapsed, setSkipped);

        // Intentionally do NOT fade black out here. Reveal happens at the start of the next shot
        // after its first frame is applied. Clearing the prior frame prevents a one-frame flash.
        if (blackImage != null && blackImage.color.a >= 0.99f)
            ClearPresentationUnderBlack();
    }

    private void ClearPresentationUnderBlack()
    {
        if (fullScreenImage != null)
            fullScreenImage.sprite = null;
    }

    private IEnumerator RevealFromBlackIfNeeded()
    {
        if (blackImage == null || blackImage.color.a <= 0.01f)
            yield break;

        yield return Fade(blackImage, blackImage.color.a, 0f, BlackRevealSeconds);
    }

    private void ClearBlackCoverImmediate()
    {
        if (blackImage == null)
            return;

        SetColorAlpha(blackImage, 0f);
        blackImage.gameObject.SetActive(false);
    }

    private static bool NeedsOpeningBlackCover(
        SpriteCinematicDefinitionSO definition,
        SpriteCinematicStageBinder stageBinder)
    {
        if (definition == null || definition.shots == null)
            return true;

        for (int i = 0; i < definition.shots.Count; i++)
        {
            SpriteCinematicShot shot = definition.shots[i];
            if (shot == null)
                continue;

            bool layered = shot.composition == SpriteCinematicComposition.Layered;
            bool useStage = layered && stageBinder != null && stageBinder.HasMiddleTarget;
            return !useStage;
        }

        return true;
    }

    private void ProcessFrameAudio(SpriteCinematicShot shot, int frameIndex)
    {
        for (int i = activeFrameAudioCues.Count - 1; i >= 0; i--)
        {
            ActiveFrameAudioCue active = activeFrameAudioCues[i];
            if (active.Cue == null || active.Cue.stopFrameIndex < 0)
                continue;
            if (frameIndex < active.Cue.stopFrameIndex)
                continue;

            if (active.Handle.IsValid)
                active.Handle.Stop(Mathf.Max(0f, active.Cue.stopFadeSeconds));
            activeFrameAudioCues.RemoveAt(i);
        }

        if (shot?.frameAudioCues == null || AudioService.Instance == null)
            return;

        for (int i = 0; i < shot.frameAudioCues.Count; i++)
        {
            SpriteCinematicFrameAudioCue cue = shot.frameAudioCues[i];
            if (cue == null || cue.soundEvent == null)
                continue;
            if (cue.frameIndex != frameIndex)
                continue;
            if (cue.stopFrameIndex >= 0 && cue.stopFrameIndex <= cue.frameIndex)
                continue;

            AudioHandle handle = AudioService.Instance.Play(cue.soundEvent);
            if (!handle.IsValid)
                continue;

            // Only track cues that may need a later stop; fire-and-forget otherwise.
            if (cue.stopFrameIndex >= 0)
            {
                activeFrameAudioCues.Add(new ActiveFrameAudioCue
                {
                    Cue = cue,
                    Handle = handle,
                });
            }
        }
    }

    private void StopActiveFrameAudioCues()
    {
        for (int i = 0; i < activeFrameAudioCues.Count; i++)
        {
            ActiveFrameAudioCue active = activeFrameAudioCues[i];
            if (active.Handle.IsValid)
                active.Handle.Stop(0f);
        }

        activeFrameAudioCues.Clear();
    }

    private static void FadeMenuMusic(SpriteCinematicShot shot, float durationSeconds)
    {
        if (shot == null || durationSeconds <= 0f)
            return;

        MainMenuSequenceController2 menu = FindFirstObjectByType<MainMenuSequenceController2>(FindObjectsInactive.Include);
        if (menu == null)
            return;

        if (shot.enableCreepEffects)
            menu.FadeOutMenuMusicWithCreep(durationSeconds);
        else
            menu.FadeOutMenuMusic(durationSeconds);
    }

    private IEnumerator WaitWithSkip(float duration, SpriteCinematicDefinitionSO definition, Func<float> getElapsed, Action<float> setElapsed, Action<bool> setSkipped)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (ShouldSkip(definition, getElapsed())) { setSkipped(true); yield break; }
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            setElapsed(getElapsed() + dt);
            yield return null;
        }
    }

    private IEnumerator PlayMarkerEffects(SpriteCinematicShot shot)
    {
        CaptureChromatic();
        EncounterTransitionGlitchOverlay.Instance?.PlayForDuration(
            Mathf.Max(0.01f, shot.markerEffectRampSeconds),
            SortingOrderPlayback + 10);
        float elapsed = 0f;
        while (elapsed < shot.markerEffectRampSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            if (chromatic != null)
                chromatic.intensity.Override(Mathf.Lerp(originalChromatic, shot.chromaticAberrationPeak, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, shot.markerEffectRampSeconds))));
            yield return null;
        }
    }

    private IEnumerator PlayCreepEffects(SpriteCinematicShot shot, float durationSeconds)
    {
        float duration = Mathf.Max(0.01f, durationSeconds);
        CaptureChromatic();
        EncounterTransitionGlitchOverlay.Instance?.PlayCreepRamp(duration, SortingOrderPlayback + 10);

        float peak = shot.creepChromaticPeak > 0f ? shot.creepChromaticPeak : shot.chromaticAberrationPeak;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Ease-in so scanline/chromatic creep intensifies toward the end.
            float eased = t * t;
            if (chromatic != null)
                chromatic.intensity.Override(Mathf.Lerp(originalChromatic, peak, eased));
            yield return null;
        }

        if (chromatic != null)
            chromatic.intensity.Override(peak);
    }

    private IEnumerator AnimateNameHandoffZoomIn(SpriteCinematicShot shot)
    {
        nameHandoffRestScale = ResolveEndScale(shot);
        nameHandoffZoomScale = shot.nameHandoffScale > 0.01f ? shot.nameHandoffScale : 1.6f;
        nameHandoffOffsetY = shot.nameHandoffOffsetY;
        nameHandoffDurationSeconds = shot.nameHandoffDurationSeconds > 0.01f
            ? shot.nameHandoffDurationSeconds
            : DefaultNameHandoffDuration;

        yield return AnimatePresentationTransform(
            nameHandoffRestScale,
            nameHandoffZoomScale,
            currentPresentationOffsetY,
            nameHandoffOffsetY,
            nameHandoffDurationSeconds);

        nameHandoffZoomActive = true;
    }

    private IEnumerator AnimateNameHandoffZoomOutRoutine(TaskCompletionSource<bool> source)
    {
        ClearCoinNameMirror();
        float fromScale = nameHandoffZoomScale;
        float toScale = nameHandoffRestScale > 0.01f ? nameHandoffRestScale : DefaultDisplayScale;
        yield return AnimatePresentationTransform(
            fromScale,
            toScale,
            currentPresentationOffsetY,
            0f,
            nameHandoffDurationSeconds);

        nameHandoffZoomActive = false;
        source.TrySetResult(true);
    }

    private IEnumerator AnimatePresentationTransform(
        float fromScale,
        float toScale,
        float fromOffsetY,
        float toOffsetY,
        float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float smoothed = t * t * (3f - 2f * t);
            ApplyPresentationScale(Mathf.Lerp(fromScale, toScale, smoothed));
            ApplyPresentationOffset(Mathf.Lerp(fromOffsetY, toOffsetY, smoothed));
            yield return null;
        }

        ApplyPresentationScale(toScale);
        ApplyPresentationOffset(toOffsetY);
    }

    private void ApplyPresentationOffset(float yOffset)
    {
        currentPresentationOffsetY = yOffset;
        if (fullScreenImageRect == null)
            return;

        // Stretch Inspector: Bottom = offsetMin.y, Top = -offsetMax.y.
        // +100 bottom / -100 top means both offsets are +yOffset (rect shifts up).
        fullScreenImageRect.offsetMin = new Vector2(0f, yOffset);
        fullScreenImageRect.offsetMax = new Vector2(0f, yOffset);
    }

    private bool ShouldSkip(SpriteCinematicDefinitionSO definition, float timelineElapsed)
    {
        if (definition == null || !definition.allowSkip)
        {
            if (skipPrompt != null)
                skipPrompt.SetActive(false);
            heldSkipSeconds = 0f;
            return false;
        }

        bool canSkip = timelineElapsed >= definition.skipAvailableAfterSeconds;
        if (skipPrompt != null) skipPrompt.SetActive(canSkip);
        bool held = touchSkipHeld || (Keyboard.current != null && Keyboard.current.escapeKey.isPressed) ||
                    (Gamepad.current != null && Gamepad.current.buttonEast.isPressed);
        heldSkipSeconds = held ? heldSkipSeconds + Time.unscaledDeltaTime : 0f;
        return canSkip && heldSkipSeconds >= definition.holdToSkipSeconds;
    }

    private IEnumerator Fade(Image image, float from, float to, float duration)
    {
        if (image == null) yield break;
        SetColorAlpha(image, from);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetColorAlpha(image, Mathf.Lerp(from, to, duration <= 0f ? 1f : elapsed / duration));
            yield return null;
        }
        SetColorAlpha(image, to);
    }

    private void ApplyShotComposition(bool layered)
    {
        SetVisible(fullScreenImage, !layered);
        if (!layered)
            stage?.BeginFullScreenPresentation();
    }

    private static float ResolveStartScale(SpriteCinematicShot shot)
    {
        if (shot == null)
            return DefaultDisplayScale;
        return shot.startScale > 0.01f ? shot.startScale : DefaultDisplayScale;
    }

    private static float ResolveEndScale(SpriteCinematicShot shot)
    {
        if (shot == null)
            return DefaultDisplayScale;
        return shot.endScale > 0.01f ? shot.endScale : DefaultDisplayScale;
    }

    private void ApplyPresentationScale(float scale)
    {
        if (fullScreenImageRect == null)
            return;

        float clamped = Mathf.Max(0.01f, scale);
        fullScreenImageRect.localScale = new Vector3(clamped, clamped, 1f);
    }

    private void ApplyStageMiddleScale(float scale)
    {
        if (stage == null)
            return;

        stage.SetMiddleScale(Mathf.Max(0.01f, scale));
    }

    private void EnableCoinNameMirror(SpriteCinematicDefinitionSO definition)
    {
        EnsureCanvas();
        EnsureCoinNameOverlay(definition);
        if (coinNameOverlayInstance == null)
            return;

        coinNameOverlayInstance.SetText(string.Empty);
        coinNameOverlayInstance.Show();
        coinNameMirrorEnabled = true;
    }

    private void EnsureCoinNameOverlay(SpriteCinematicDefinitionSO definition)
    {
        if (coinNameOverlayInstance != null)
            return;

        CoinNameFieldOverlay prefab = definition != null ? definition.coinNameFieldPrefab : null;
        if (prefab == null)
        {
            Debug.LogWarning(
                "[SpriteCinematicPlayer] CoinNameFieldOverlay prefab missing. " +
                "Assign Assets/Prefabs/UI/MainMenu/CoinNameFieldOverlay on the cinematic definition.");
            return;
        }

        coinNameOverlayInstance = Instantiate(prefab, fullScreenImageRect != null ? fullScreenImageRect : root.transform);
        coinNameOverlayInstance.name = "CoinNameFieldOverlay";
        coinNameOverlayInstance.Hide();
    }

    private void EnsureCanvas()
    {
        if (root != null) return;
        root = new GameObject("[SpriteCinematicCanvas]", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        rootCanvas = root.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = SortingOrderPlayback;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1335f, 600f);
        scaler.matchWidthOrHeight = 0.5f;

        fullScreenImage = CreateLayer("FullScreenAnimation", root.transform, Color.white);
        fullScreenImageRect = fullScreenImage.rectTransform;
        flashImage = CreateLayer("WhiteFlash", root.transform, Color.white);
        blackImage = CreateLayer("BlackFade", root.transform, Color.black);
        skipPrompt = CreateSkipPrompt(root.transform);
        root.SetActive(false);
    }

    private void SetCanvasSortingOrder(int order)
    {
        if (rootCanvas == null && root != null)
            rootCanvas = root.GetComponent<Canvas>();
        if (rootCanvas != null)
            rootCanvas.sortingOrder = order;
    }

    private static Image CreateLayer(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.preserveAspect = true;
        image.color = color;
        image.raycastTarget = false;
        Stretch(image.rectTransform);
        return image;
    }

    private GameObject CreateSkipPrompt(Transform parent)
    {
        GameObject go = new GameObject("HoldToSkip", typeof(RectTransform), typeof(Image), typeof(HoldPointerControl));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f); rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f); rect.sizeDelta = new Vector2(210f, 60f); rect.anchoredPosition = new Vector2(-24f, 24f);
        go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        HoldPointerControl hold = go.GetComponent<HoldPointerControl>();
        hold.OnHeldChanged = held => touchSkipHeld = held;
        TextMeshProUGUI text = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        text.transform.SetParent(go.transform, false); text.font = TMP_Settings.defaultFontAsset; text.text = "Hold B to Skip"; text.fontSize = 18; text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return go;
    }

    private IEnumerator PersistCompletionCheckpoint(SpriteCinematicDefinitionSO definition)
    {
        if (definition == null || !definition.markIntroCinematicComplete)
            yield break;

        GameSessionContext.Instance?.MarkIntroCinematicComplete();
        AccountSessionService account = FindObjectOfType<AccountSessionService>();
        if (account == null || string.IsNullOrWhiteSpace(account.CurrentSession.PlayerProfileId)) yield break;
        Task<PlayerAccountSession> task = account.SetIntroCinematicCompleteAsync();
        while (!task.IsCompleted) yield return null;
    }

    private void CaptureChromatic()
    {
        if (capturedChromatic) return;
        Volume[] volumes = FindObjectsOfType<Volume>(true);
        foreach (Volume volume in volumes)
        {
            if (!volume.isGlobal || volume.profile == null) continue;
            if (volume.profile.TryGet(out ChromaticAberration result))
            {
                chromatic = result; originalChromatic = result.intensity.value; capturedChromatic = true; return;
            }
        }
    }

    private void PrepareContinueFromHeldFrame()
    {
        ClearCoinNameMirror();
        nameHandoffZoomActive = false;
        SetCanvasSortingOrder(SortingOrderPlayback);
        ApplyPresentationScale(nameHandoffRestScale > 0.01f ? nameHandoffRestScale : DefaultDisplayScale);
        ApplyPresentationOffset(0f);
        if (root != null)
            root.SetActive(true);
        SetVisible(fullScreenImage, true);
        SetColorAlpha(blackImage, 0f);
        SetColorAlpha(flashImage, 0f);
    }

    private void ReleaseHeldLastFrame()
    {
        holdingLastFrame = false;
        nameHandoffZoomActive = false;
        ClearCoinNameMirror();
        SetCanvasSortingOrder(SortingOrderPlayback);
        if (root != null && !playing)
            root.SetActive(false);
        ApplyPresentationScale(DefaultDisplayScale);
        ApplyPresentationOffset(0f);
    }

    /// <summary>
    /// Drops a load-cover hold (black + scanlines) once the loading screen is on top.
    /// </summary>
    public void ReleaseLoadCover()
    {
        if (!holdingLastFrame && (root == null || !root.activeSelf))
            return;

        holdingLastFrame = false;
        CleanupVisualState(forceHide: true, keepGlitch: false);
    }

    private void CleanupVisualState(bool forceHide, bool keepGlitch = false)
    {
        if (!keepGlitch)
        {
            if (chromatic != null && capturedChromatic)
                chromatic.intensity.Override(originalChromatic);
            capturedChromatic = false;
            chromatic = null;
            EncounterTransitionGlitchOverlay.Instance?.HideImmediate();
        }

        heldSkipSeconds = 0f;
        touchSkipHeld = false;

        if (forceHide || !holdingLastFrame)
        {
            StopActiveFrameAudioCues();
            ClearCoinNameMirror();
            nameHandoffZoomActive = false;
            SetCanvasSortingOrder(SortingOrderPlayback);
            if (root != null)
                root.SetActive(false);
            holdingLastFrame = false;
            ApplyPresentationScale(DefaultDisplayScale);
            ApplyPresentationOffset(0f);
        }

        playing = false;
    }

    private void Finish(
        TaskCompletionSource<SpriteCinematicResult> source,
        SpriteCinematicResult result,
        bool keepPresentation,
        bool keepLoadCover = false)
    {
        holdingLastFrame = keepPresentation;
        CleanupVisualState(forceHide: !keepPresentation, keepGlitch: keepLoadCover);
        if (keepPresentation && root != null)
        {
            root.SetActive(true);
            SetVisible(fullScreenImage, true);
            SetColorAlpha(flashImage, 0f);
            if (skipPrompt != null)
                skipPrompt.SetActive(false);

            if (keepLoadCover)
            {
                SetColorAlpha(blackImage, 1f);
                SetCanvasSortingOrder(SortingOrderPlayback);
            }
            else
            {
                SetColorAlpha(blackImage, 0f);
                SetCanvasSortingOrder(SortingOrderNameHandoff);
            }
        }

        if (activePlaybackSource == source)
            activePlaybackSource = null;
        source.TrySetResult(result);
    }

    private static void SetVisible(Image image, bool visible) { if (image != null) image.gameObject.SetActive(visible); }
    private static void SetColorAlpha(Image image, float alpha) { if (image != null) { Color color = image.color; color.a = Mathf.Clamp01(alpha); image.color = color; image.gameObject.SetActive(true); } }
    private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
}

public sealed class HoldPointerControl : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    public Action<bool> OnHeldChanged;
    public void OnPointerDown(PointerEventData eventData) => OnHeldChanged?.Invoke(true);
    public void OnPointerUp(PointerEventData eventData) => OnHeldChanged?.Invoke(false);
    public void OnPointerExit(PointerEventData eventData) => OnHeldChanged?.Invoke(false);
}
