using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Component that animates a UI Image by swapping sprites at a fixed FPS.
/// Supports looping and unscaled time for animations during pause.
/// </summary>
[RequireComponent(typeof(Image))]
public class UIImageFrameAnimator : MonoBehaviour
{
    [Header("Frames")]
    [Tooltip("Sprites to cycle through in order. Assign your frame set here.")]
    [SerializeField] private List<Sprite> frames = new List<Sprite>();

    [Header("Animation Settings")]
    [Tooltip("Target image to animate (auto-assigned if null)")]
    [SerializeField] private Image targetImage;
    
    [Tooltip("Animation frames per second")]
    [Range(1f, 60f)]
    [SerializeField] private float fps = 12f;
    
    [Tooltip("Loop the animation continuously.")]
    [SerializeField] private bool loop = true;

    [Tooltip("Automatically begin playing when the object starts. If false, call Play() manually.")]
    [SerializeField] private bool autoPlayOnStart = false;
    
    [Tooltip("Use unscaled time (animates during Time.timeScale = 0)")]
    [SerializeField] private bool useUnscaledTime = true;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    // Current animation state
    private List<Sprite> currentFrames = new List<Sprite>();
    private Coroutine animationCoroutine;
    private bool isPlaying = false;
    
    private void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();

        // Load the Inspector-assigned frame set if no frames have been set programmatically yet.
        if (frames != null && frames.Count > 0)
            SetFrames(frames);
    }

    private void Start()
    {
        if (autoPlayOnStart)
            Play();
    }
    
    /// <summary>
    /// Play animation with optional frame override.
    /// </summary>
    public void Play(List<Sprite> framesOverride = null)
    {
        if (framesOverride != null && framesOverride.Count > 0)
        {
            SetFrames(framesOverride);
        }
        
        if (currentFrames == null || currentFrames.Count == 0)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[UIImageFrameAnimator] No frames to animate!");
            }
            return;
        }
        
        Stop();
        isPlaying = true;
        animationCoroutine = StartCoroutine(AnimateCoroutine());
    }
    
    /// <summary>
    /// Stop the animation.
    /// </summary>
    public void Stop()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
        isPlaying = false;
    }
    
    /// <summary>
    /// Set the frames to animate.
    /// </summary>
    public void SetFrames(List<Sprite> frames)
    {
        if (frames == null)
        {
            currentFrames = new List<Sprite>();
            return;
        }
        
        // Create a copy to avoid external modifications
        currentFrames = new List<Sprite>(frames);
        
        // Set initial frame
        if (targetImage != null && currentFrames.Count > 0)
        {
            targetImage.sprite = currentFrames[0];
        }
    }
    
    /// <summary>
    /// Set the FPS for animation.
    /// </summary>
    public void SetFPS(float newFps)
    {
        fps = Mathf.Max(1f, newFps);
    }

    /// <summary>
    /// Set whether the animation loops. When false, animation runs once then stops (IsPlaying becomes false).
    /// </summary>
    public void SetLoop(bool shouldLoop)
    {
        loop = shouldLoop;
    }
    
    /// <summary>
    /// Check if animation is currently playing.
    /// </summary>
    public bool IsPlaying => isPlaying;

    /// <summary>
    /// Number of sprites assigned in the Inspector (does not reflect runtime overrides from <see cref="Play"/>).
    /// </summary>
    public int SerializedAuthoringFrameCount => frames != null ? frames.Count : 0;
    
    private IEnumerator AnimateCoroutine()
    {
        if (currentFrames == null || currentFrames.Count == 0)
        {
            yield break;
        }
        
        if (currentFrames.Count == 1)
        {
            // Single frame - just set it and exit
            if (targetImage != null)
            {
                targetImage.sprite = currentFrames[0];
            }
            isPlaying = false;
            animationCoroutine = null;
            yield break;
        }
        
        float frameDuration = 1f / fps;
        int currentFrameIndex = 0;
        
        while (isPlaying)
        {
            if (targetImage != null && currentFrames.Count > 0)
            {
                targetImage.sprite = currentFrames[currentFrameIndex];
            }
            
            // Wait for next frame
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(frameDuration);
            }
            else
            {
                yield return new WaitForSeconds(frameDuration);
            }
            
            // Advance frame
            currentFrameIndex++;
            
            if (currentFrameIndex >= currentFrames.Count)
            {
                if (loop)
                {
                    currentFrameIndex = 0;
                }
                else
                {
                    // Animation complete
                    break;
                }
            }
        }
        
        isPlaying = false;
        animationCoroutine = null;
    }
    
    private void OnDisable()
    {
        Stop();
    }
}
