using System;
using UnityEngine;

/// <summary>
/// Shared exploration presentation clip for actors like NPCs and the player.
/// Stores the primary directional sprite sequence plus optional local companion cues.
/// </summary>
[CreateAssetMenu(
    fileName = "ExplorationSpritePresentationClip",
    menuName = "Chainbound/Presentation/Exploration/Sprite Presentation Clip")]
public class ExplorationSpritePresentationClipSO : ScriptableObject
{
    [Header("Primary Visual")]
    [Tooltip("Directional sprite sequence that temporarily takes over the actor's primary exploration visual.")]
    [SerializeField] private ExplorationDirectionalSpriteSequences directionalSprites = new ExplorationDirectionalSpriteSequences();

    [Tooltip("Frames per second for the primary presentation sequence.")]
    [SerializeField] private float framesPerSecond = 8f;

    [Tooltip("Default playback policy when this clip is started by trigger-driven interaction content.")]
    [SerializeField] private ExplorationPresentationPlaybackMode defaultPlaybackMode = ExplorationPresentationPlaybackMode.OneShot;

    [Tooltip("Duration in seconds when default playback mode is LoopForDuration.")]
    [SerializeField] private float loopDurationSeconds = 1f;

    [Tooltip("What happens after the programmed playback ends.")]
    [SerializeField] private ExplorationPresentationRestoreMode completionBehavior = ExplorationPresentationRestoreMode.ReleaseToGameplay;

    [Header("Companion Cues")]
    [Tooltip("Optional one-shot sound cues that resolve against presenter-local audio slots.")]
    [SerializeField] private ExplorationPresentationSoundCue[] soundCues = Array.Empty<ExplorationPresentationSoundCue>();

    [Tooltip("Optional one-shot local effect cues that resolve against presenter-local effect slots.")]
    [SerializeField] private ExplorationPresentationLocalEffectCue[] localEffectCues = Array.Empty<ExplorationPresentationLocalEffectCue>();

    public ExplorationDirectionalSpriteSequences DirectionalSprites => directionalSprites;
    public float FramesPerSecond => Mathf.Max(0.01f, framesPerSecond);
    public ExplorationPresentationPlaybackMode DefaultPlaybackMode => defaultPlaybackMode;
    public float LoopDurationSeconds => Mathf.Max(0f, loopDurationSeconds);
    public ExplorationPresentationRestoreMode CompletionBehavior => completionBehavior;
    public ExplorationPresentationSoundCue[] SoundCues => soundCues ?? Array.Empty<ExplorationPresentationSoundCue>();
    public ExplorationPresentationLocalEffectCue[] LocalEffectCues => localEffectCues ?? Array.Empty<ExplorationPresentationLocalEffectCue>();

    public bool HasDirectionalSprites => directionalSprites != null && directionalSprites.HasSprites();

    public float GetDuration(ExplorationFacingDirection direction)
    {
        if (!HasDirectionalSprites)
            return 0f;

        SpriteAnimationSequence sequence = directionalSprites.GetSequence(direction);
        int frameCount = sequence != null ? sequence.Length : 0;
        if (frameCount <= 1)
            return 0f;

        return (frameCount - 1) / FramesPerSecond;
    }

    public bool TryGetDefaultPlaybackRequest(out ExplorationPresentationPlaybackRequest request, out string validationMessage)
    {
        validationMessage = null;

        switch (defaultPlaybackMode)
        {
            case ExplorationPresentationPlaybackMode.OneShot:
                request = ExplorationPresentationPlaybackRequest.OneShot;
                return true;

            case ExplorationPresentationPlaybackMode.LoopForDuration:
                if (LoopDurationSeconds <= 0f)
                {
                    request = ExplorationPresentationPlaybackRequest.OneShot;
                    validationMessage = $"Clip '{name}' uses LoopForDuration but has no positive loop duration configured.";
                    return false;
                }

                request = ExplorationPresentationPlaybackRequest.LoopForDuration(LoopDurationSeconds);
                return true;

            case ExplorationPresentationPlaybackMode.LoopUntilStopped:
                request = ExplorationPresentationPlaybackRequest.LoopUntilStopped;
                return true;

            default:
                request = ExplorationPresentationPlaybackRequest.OneShot;
                validationMessage = $"Clip '{name}' has an unsupported playback mode '{defaultPlaybackMode}'.";
                return false;
        }
    }

    private void OnValidate()
    {
        framesPerSecond = Mathf.Max(0.01f, framesPerSecond);

        if (defaultPlaybackMode == ExplorationPresentationPlaybackMode.LoopForDuration && loopDurationSeconds <= 0f)
        {
            Debug.LogWarning(
                $"[ExplorationSpritePresentationClipSO] Clip '{name}' uses LoopForDuration and requires a positive loop duration. Clamping to 0.1 seconds.",
                this);
            loopDurationSeconds = 0.1f;
        }
        else if (loopDurationSeconds < 0f)
        {
            loopDurationSeconds = 0f;
        }
    }
}

public enum ExplorationPresentationPlaybackMode
{
    OneShot,
    LoopForDuration,
    LoopUntilStopped
}

public enum ExplorationPresentationRestoreMode
{
    ReleaseToGameplay,
    RestoreCapturedPose,
    HoldLastFrame
}

public enum ExplorationFacingDirection
{
    Down = 0,
    Up = 1,
    Left = 2,
    Right = 3
}

public readonly struct ExplorationPresentationPlaybackRequest
{
    public ExplorationPresentationPlaybackRequest(
        ExplorationPresentationPlaybackMode mode,
        float durationSeconds)
    {
        Mode = mode;
        DurationSeconds = Mathf.Max(0f, durationSeconds);
    }

    public ExplorationPresentationPlaybackMode Mode { get; }
    public float DurationSeconds { get; }
    public bool IsLooping => Mode != ExplorationPresentationPlaybackMode.OneShot;
    public bool HasFiniteDuration => Mode == ExplorationPresentationPlaybackMode.LoopForDuration;

    public static ExplorationPresentationPlaybackRequest OneShot =>
        new ExplorationPresentationPlaybackRequest(ExplorationPresentationPlaybackMode.OneShot, 0f);

    public static ExplorationPresentationPlaybackRequest LoopUntilStopped =>
        new ExplorationPresentationPlaybackRequest(ExplorationPresentationPlaybackMode.LoopUntilStopped, 0f);

    public static ExplorationPresentationPlaybackRequest LoopForDuration(float durationSeconds) =>
        new ExplorationPresentationPlaybackRequest(
            ExplorationPresentationPlaybackMode.LoopForDuration,
            Mathf.Max(0.01f, durationSeconds));
}

[Serializable]
public class ExplorationDirectionalSpriteSequences
{
    [Tooltip("Sprites for facing down / south.")]
    public SpriteAnimationSequence down = new SpriteAnimationSequence();

    [Tooltip("Sprites for facing left / west.")]
    public SpriteAnimationSequence left = new SpriteAnimationSequence();

    [Tooltip("Sprites for facing right / east.")]
    public SpriteAnimationSequence right = new SpriteAnimationSequence();

    [Tooltip("Sprites for facing up / north.")]
    public SpriteAnimationSequence up = new SpriteAnimationSequence();

    public SpriteAnimationSequence GetSequence(ExplorationFacingDirection direction)
    {
        switch (direction)
        {
            case ExplorationFacingDirection.Down:
                return down;
            case ExplorationFacingDirection.Up:
                return up;
            case ExplorationFacingDirection.Left:
                return left;
            case ExplorationFacingDirection.Right:
                return right;
            default:
                return down;
        }
    }

    public bool HasSprites()
    {
        return HasSequenceSprites(down) ||
               HasSequenceSprites(left) ||
               HasSequenceSprites(right) ||
               HasSequenceSprites(up);
    }

    private static bool HasSequenceSprites(SpriteAnimationSequence sequence)
    {
        return sequence != null && sequence.Length > 0;
    }
}

[Serializable]
public class ExplorationPresentationSoundCue
{
    [Tooltip("Presenter-local audio slot ID, such as 'body_audio'.")]
    [SerializeField] private string slotId = string.Empty;

    [Tooltip("Sound event to play when this presentation begins.")]
    [SerializeField] private SoundEventSO soundEvent;

    [Tooltip("If true, 3D playback follows the resolved audio slot transform.")]
    [SerializeField] private bool followSlotTransform = true;

    public string SlotId => slotId;
    public SoundEventSO SoundEvent => soundEvent;
    public bool FollowSlotTransform => followSlotTransform;
}

[Serializable]
public class ExplorationPresentationLocalEffectCue
{
    [Tooltip("Presenter-local effect slot ID, such as 'body_fx' or 'eyes_overlay'.")]
    [SerializeField] private string slotId = string.Empty;

    public string SlotId => slotId;
}
