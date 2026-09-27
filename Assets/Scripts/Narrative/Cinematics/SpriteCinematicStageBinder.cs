using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-authored stage for layered sprite cinematics. Background and foreground
/// keep their own looping frame animators; the middle target plays the shot's
/// flipbook between them in hierarchy order. Full-screen shots do not use this stage.
/// </summary>
public sealed class SpriteCinematicStageBinder : MonoBehaviour
{
    public static SpriteCinematicStageBinder Active { get; private set; }

    [Header("Companion Layers (scene-authored)")]
    [SerializeField] private UIImageFrameAnimator backgroundAnimator;
    [SerializeField] private UIImageFrameAnimator foregroundAnimator;

    [Header("Middle Photo Layer")]
    [Tooltip("UIImageFrameAnimator on the panel between Background and Foreground.")]
    [SerializeField] private UIImageFrameAnimator middleAnimator;
    [SerializeField] private CanvasGroup middleGroup;

    [Header("Object Navigation Stage")]
    [Tooltip("Animated menu objects that must freeze at the photo snap.")]
    [SerializeField] private SyncedOutlineFrameAnimator[] stageAnimatorsToStop;
    [Tooltip("Object, vinyl, bubble, and HUD roots hidden for the full-screen handoff.")]
    [SerializeField] private GameObject[] stageRootsToHide;

    [Header("Chrome To Hide During Cinematic")]
    [Tooltip("Logo / buttons / overlays that should not sit over the intro photo.")]
    [SerializeField] private CanvasGroup[] chromeGroupsToHide;

    private readonly List<float> _cachedChromeAlphas = new List<float>();
    private bool _chromeHidden;

    public bool HasMiddleTarget => middleAnimator != null;
    public bool IsMiddlePlaying => middleAnimator != null && middleAnimator.IsPlaying;

    public void SetMiddleScale(float scale)
    {
        if (middleAnimator == null)
            return;

        float clamped = Mathf.Max(0.01f, scale);
        middleAnimator.transform.localScale = new Vector3(clamped, clamped, 1f);
    }

    private void OnEnable()
    {
        Active = this;
        HideMiddleImmediate();
    }

    private void OnDisable()
    {
        if (Active == this)
            Active = null;
    }

    /// <summary>Show middle layer; keep BG/FG animators running; hide menu chrome.</summary>
    public void BeginLayeredPresentation()
    {
        HideChrome();
        if (middleGroup != null)
        {
            middleGroup.gameObject.SetActive(true);
            middleGroup.alpha = 1f;
            middleGroup.interactable = false;
            middleGroup.blocksRaycasts = false;
        }

        EnsurePlaying(backgroundAnimator);
        EnsurePlaying(foregroundAnimator);
    }

    /// <summary>Shows frame 0 on the middle layer without starting playback (for black-reveal handoff).</summary>
    public void PrepareMiddleFrames(List<Sprite> frames, float fps)
    {
        if (middleAnimator == null)
            return;

        middleAnimator.Stop();
        middleAnimator.SetFPS(Mathf.Max(0.01f, fps));
        middleAnimator.SetLoop(false);

        if (frames != null && frames.Count > 0)
            middleAnimator.SetFrames(frames);
    }

    public void PlayMiddleFrames(List<Sprite> frames, float fps)
    {
        if (middleAnimator == null)
            return;

        middleAnimator.SetFPS(Mathf.Max(0.01f, fps));
        middleAnimator.SetLoop(false);

        if (frames != null && frames.Count > 0)
            middleAnimator.Play(frames);
        else
            middleAnimator.Stop();
    }

    /// <summary>Stops background, foreground, and middle animators (e.g. at camera flash).</summary>
    public void StopCompanionAnimations()
    {
        backgroundAnimator?.Stop();
        foregroundAnimator?.Stop();
        middleAnimator?.Stop();

        if (stageAnimatorsToStop == null)
            return;

        for (int i = 0; i < stageAnimatorsToStop.Length; i++)
            stageAnimatorsToStop[i]?.Stop();
    }

    /// <summary>Handoff into a full-screen overlay shot: freeze stage art and hide middle.</summary>
    public void BeginFullScreenPresentation()
    {
        HideChrome();
        StopCompanionAnimations();
        HideMiddleImmediate();

        if (stageRootsToHide == null)
            return;

        for (int i = 0; i < stageRootsToHide.Length; i++)
        {
            if (stageRootsToHide[i] != null)
                stageRootsToHide[i].SetActive(false);
        }
    }

    /// <summary>
    /// Deactivates menu stage art (background/foreground/middle and authored hide roots)
    /// so it cannot flash under a cinematic-to-loading handoff.
    /// </summary>
    public void HideStageVisuals()
    {
        HideChrome();
        StopCompanionAnimations();
        HideMiddleImmediate();

        SetInactive(backgroundAnimator != null ? backgroundAnimator.gameObject : null);
        SetInactive(foregroundAnimator != null ? foregroundAnimator.gameObject : null);
        SetInactive(middleAnimator != null ? middleAnimator.gameObject : null);

        if (stageRootsToHide == null)
            return;

        for (int i = 0; i < stageRootsToHide.Length; i++)
            SetInactive(stageRootsToHide[i]);
    }

    private static void SetInactive(GameObject target)
    {
        if (target != null && target.activeSelf)
            target.SetActive(false);
    }

    public void HideMiddleImmediate()
    {
        if (middleGroup == null)
            return;

        middleGroup.alpha = 0f;
        middleGroup.gameObject.SetActive(false);
    }

    private static void EnsurePlaying(UIImageFrameAnimator animator)
    {
        if (animator != null && !animator.IsPlaying)
            animator.Play();
    }

    private void HideChrome()
    {
        if (_chromeHidden || chromeGroupsToHide == null)
            return;

        _cachedChromeAlphas.Clear();
        for (int i = 0; i < chromeGroupsToHide.Length; i++)
        {
            CanvasGroup group = chromeGroupsToHide[i];
            if (group == null)
            {
                _cachedChromeAlphas.Add(0f);
                continue;
            }

            _cachedChromeAlphas.Add(group.alpha);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        _chromeHidden = true;
    }
}
