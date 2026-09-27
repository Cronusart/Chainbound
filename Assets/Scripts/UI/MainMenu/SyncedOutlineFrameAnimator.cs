using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Advances a menu object's body and outline from one frame clock so the two
/// layers can never drift. A one-frame outline list is treated as a static
/// outline for an animated body.
/// </summary>
[DisallowMultipleComponent]
public sealed class SyncedOutlineFrameAnimator : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Image bodyImage;
    [SerializeField] private Image outlineImage;

    [Header("Frames")]
    [SerializeField] private List<Sprite> bodyFrames = new List<Sprite>();
    [SerializeField] private List<Sprite> outlineFrames = new List<Sprite>();

    [Header("Playback")]
    [SerializeField, Range(1f, 60f)] private float fps = 12f;
    [SerializeField] private bool loop = true;
    [SerializeField] private bool autoPlayOnStart;
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine playbackCoroutine;
    private bool isPlaying;

    public bool IsPlaying => isPlaying;
    public int SerializedBodyFrameCount => bodyFrames != null ? bodyFrames.Count : 0;
    public int SerializedOutlineFrameCount => outlineFrames != null ? outlineFrames.Count : 0;

    private void Awake()
    {
        ApplyFrame(0);
    }

    private void Start()
    {
        if (autoPlayOnStart)
            Play();
    }

    public void Play()
    {
        if (bodyFrames == null || bodyFrames.Count == 0)
            return;

        Stop();
        ApplyFrame(0);

        if (bodyFrames.Count == 1)
            return;

        isPlaying = true;
        playbackCoroutine = StartCoroutine(PlaybackCoroutine());
    }

    public void Stop()
    {
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        isPlaying = false;
    }

    /// <summary>Stops playback and restores both layers to their authored first frame.</summary>
    public void ResetToFirstFrame()
    {
        Stop();
        ApplyFrame(0);
    }

    public void SetFPS(float newFps)
    {
        fps = Mathf.Max(1f, newFps);
    }

    public void SetLoop(bool shouldLoop)
    {
        loop = shouldLoop;
    }

    private IEnumerator PlaybackCoroutine()
    {
        int frameIndex = 0;

        while (isPlaying)
        {
            ApplyFrame(frameIndex);

            float duration = 1f / Mathf.Max(1f, fps);
            if (useUnscaledTime)
                yield return new WaitForSecondsRealtime(duration);
            else
                yield return new WaitForSeconds(duration);

            frameIndex++;
            if (frameIndex < bodyFrames.Count)
                continue;

            if (loop)
                frameIndex = 0;
            else
                break;
        }

        isPlaying = false;
        playbackCoroutine = null;
    }

    private void ApplyFrame(int bodyFrameIndex)
    {
        if (bodyImage != null && bodyFrames != null && bodyFrames.Count > 0)
        {
            int safeBodyIndex = Mathf.Clamp(bodyFrameIndex, 0, bodyFrames.Count - 1);
            bodyImage.sprite = bodyFrames[safeBodyIndex];
        }

        if (outlineImage == null || outlineFrames == null || outlineFrames.Count == 0)
            return;

        int outlineIndex = outlineFrames.Count == 1
            ? 0
            : Mathf.Clamp(bodyFrameIndex, 0, outlineFrames.Count - 1);
        outlineImage.sprite = outlineFrames[outlineIndex];
    }

    private void OnDisable()
    {
        Stop();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (bodyImage == null)
            bodyImage = GetComponentInChildren<Image>(true);

        if (outlineFrames != null && outlineFrames.Count > 1 &&
            bodyFrames != null && outlineFrames.Count != bodyFrames.Count)
        {
            Debug.LogWarning(
                $"[{nameof(SyncedOutlineFrameAnimator)}] {name} has {bodyFrames.Count} body frames " +
                $"but {outlineFrames.Count} outline frames.", this);
        }

        ApplyFrame(0);
    }
#endif
}
