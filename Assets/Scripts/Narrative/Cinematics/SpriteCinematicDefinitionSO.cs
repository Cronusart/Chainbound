using System;
using System.Collections.Generic;
using UnityEngine;

public enum SpriteCinematicComposition
{
    /// <summary>
    /// Plays shot frames on a scene-authored middle UI layer while existing
    /// background/foreground frame animators keep looping. Requires a
    /// <see cref="SpriteCinematicStageBinder"/> in the active scene.
    /// </summary>
    Layered,
    /// <summary>Full-screen flipbook on the cinematic overlay canvas.</summary>
    FullScreen,
}

[Serializable]
public class SpriteCinematicFrameAudioCue
{
    [Tooltip("0-based flipbook frame index that fires this one-shot.")]
    [Min(0)] public int frameIndex;
    public SoundEventSO soundEvent;
    [Tooltip("0-based frame that stops this cue. -1 = play to the natural clip end.")]
    public int stopFrameIndex = -1;
    [Tooltip("Optional fade when stopFrameIndex is hit. 0 = hard stop.")]
    [Min(0)] public float stopFadeSeconds;
}

[Serializable]
public class SpriteCinematicShot
{
    public string shotId = "";
    public SpriteCinematicComposition composition = SpriteCinematicComposition.FullScreen;

    [Tooltip("Legacy fields — unused when a SpriteCinematicStageBinder supplies scene BG/FG.")]
    public Sprite background;
    [Tooltip("Legacy fields — unused when a SpriteCinematicStageBinder supplies scene BG/FG.")]
    public Sprite foreground;

    [Tooltip("Flipbook frames. Layered → middle panel; FullScreen → overlay.")]
    public List<Sprite> frames = new List<Sprite>();
    [Min(0.01f)] public float framesPerSecond = 12f;
    [Min(0)] public float holdLastFrameSeconds;
    [Min(0)] public float fadeToBlackSeconds;
    [Min(0)] public float blackHoldSeconds;
    [Tooltip("Keep the screen black after this shot. Use for a final shot before a scene bootstrap.")]
    public bool remainBlackAfterShot;

    [Header("Presentation Scale")]
    [Tooltip("Scale at the start of the flipbook (e.g. 1.25 for a zoomed-in overhead).")]
    [Min(0.01f)] public float startScale = 1.1f;
    [Tooltip("Scale at the end of the flipbook / static display scale (default 1.1).")]
    [Min(0.01f)] public float endScale = 1.1f;

    [Header("Name Input Handoff")]
    [Tooltip("Leave the last fullscreen frame visible after this shot ends (for coin-name OSK over the monitor).")]
    public bool remainLastFrameVisible;
    [Tooltip("When remaining visible, show the coin-name TMP overlay prefab (authored on the definition).")]
    public bool enableCoinNameMirror;
    [Tooltip("Quick zoom applied to the held frame before the OSK opens.")]
    [Min(0.01f)] public float nameHandoffScale = 1.6f;
    [Tooltip("Stretch offset Y in canvas points. Positive shifts the frame up (Inspector Bottom +Y / Top -Y).")]
    public float nameHandoffOffsetY = 100f;
    [Tooltip("Duration of the zoom/offset tween into and out of name entry.")]
    [Min(0.01f)] public float nameHandoffDurationSeconds = 0.35f;

    [Tooltip("When true, freeze companion stage animators (BG/FG/middle) as the flash begins.")]
    public bool cameraFlash;
    [Range(0, 1)] public float flashRiseSeconds = 0.08f;
    [Range(0, 1)] public float flashHoldSeconds = 0.12f;
    [Range(0, 2)] public float flashFadeSeconds = 0.35f;
    [Tooltip("0-based frame index; -1 disables the marker.")]
    public int markerFrame = -1;
    [Range(0, 1)] public float chromaticAberrationPeak = 0.8f;
    [Min(0)] public float markerEffectRampSeconds = 0.8f;

    [Header("Creep Effects")]
    [Tooltip("Ramp scanline/glitch overlay + chromatic aberration across the full shot (dog-bed sleep beat).")]
    public bool enableCreepEffects;
    [Tooltip("Peak chromatic aberration intensity when creep effects are enabled.")]
    [Range(0, 1)] public float creepChromaticPeak = 0.9f;

    [Header("Audio")]
    [Tooltip("Optional one-shot played when this shot begins (before frame 0).")]
    public SoundEventSO audioCue;
    [Tooltip("One-shot SoundEvents fired when the flipbook reaches the listed 0-based frame index. Sounds play for their full length (no loop).")]
    public List<SpriteCinematicFrameAudioCue> frameAudioCues = new List<SpriteCinematicFrameAudioCue>();
    [Tooltip("If > 0, fades the currently playing main-menu music when this shot starts. Leave 0 to keep menu music running. Creep shots use the flipbook + hold duration instead of this number.")]
    [Min(0)] public float fadeMenuMusicSeconds;
}

/// <summary>Data-only sequence of composable sprite cinematic shots.</summary>
[CreateAssetMenu(fileName = "SpriteCinematic", menuName = "Chainbound/Narrative/Sprite Cinematic Definition")]
public class SpriteCinematicDefinitionSO : ScriptableObject
{
    public List<SpriteCinematicShot> shots = new List<SpriteCinematicShot>();

    [Tooltip("When false, hold-to-skip is disabled for this cinematic.")]
    public bool allowSkip = true;

    [Tooltip("When true, marks GameSessionContext intro cinematic complete after this definition finishes.")]
    public bool markIntroCinematicComplete;

    [Tooltip("If a previous cinematic left its last frame held (name-entry handoff), continue from that 1.1-scale frame instead of wiping to black.")]
    public bool continueFromHeldFrame;

    [Header("Name Field Overlay")]
    [Tooltip("CoinNameFieldOverlay prefab (font/position authored on the prefab). Used when a shot enables the coin-name mirror.")]
    public CoinNameFieldOverlay coinNameFieldPrefab;

    [Min(0)] public float skipAvailableAfterSeconds = 1.5f;
    [Min(0.1f)] public float holdToSkipSeconds = 0.75f;
    [Min(0)] public float skipFadeToBlackSeconds = 0.25f;
}
