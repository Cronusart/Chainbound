using System.Collections;
using UnityEngine;
using UnityEngine.Video;

/// <summary>
/// Step that plays a video cutscene.
/// Supports skipping and completion callback.
/// </summary>
[CreateAssetMenu(fileName = "Step_PlayVideoCutscene", menuName = "Chainbound/Narrative/Steps/Play Video Cutscene")]
public class Step_PlayVideoCutscene : SequenceStep
{
    [Header("Video")]
    [Tooltip("Video clip to play")]
    [SerializeField] private VideoClip videoClip;
    
    [Tooltip("Video file path (alternative to clip)")]
    [SerializeField] private string videoPath = "";
    
    [Header("Settings")]
    [Tooltip("Allow skipping")]
    [SerializeField] private bool allowSkip = true;
    
    [Tooltip("Skip action name")]
    [SerializeField] private string skipActionName = "Interact";
    
    [Header("UI")]
    [Tooltip("Video player. Auto-creates if null.")]
    [SerializeField] private VideoPlayer videoPlayer;
    
    [Tooltip("Skip prompt UI (optional)")]
    [SerializeField] private GameObject skipPromptUI;
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        // Get or create video player
        if (videoPlayer == null)
        {
            GameObject videoObj = new GameObject("VideoPlayer");
            videoPlayer = videoObj.AddComponent<VideoPlayer>();
            videoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
            videoPlayer.targetCamera = SceneCameraResolver.ResolveGameplayCamera();
        }
        
        // Set video clip or path
        if (videoClip != null)
        {
            videoPlayer.clip = videoClip;
        }
        else if (!string.IsNullOrEmpty(videoPath))
        {
            videoPlayer.url = videoPath;
        }
        else
        {
            Debug.LogError("[Step_PlayVideoCutscene] No video clip or path specified!");
            onComplete?.Invoke(StepOutcome.Failed("No video clip or path specified"));
            yield break;
        }
        
        // Show skip prompt if available
        if (skipPromptUI != null && allowSkip)
        {
            skipPromptUI.SetActive(true);
        }
        
        // Play video
        videoPlayer.Play();
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_PlayVideoCutscene] Playing video: {(videoClip != null ? videoClip.name : videoPath)}");
        }
        
        // Wait for completion or skip
        bool skipped = false;
        
        while (videoPlayer.isPlaying)
        {
            // Check for skip input
            if (allowSkip && CheckSkipInput())
            {
                videoPlayer.Stop();
                skipped = true;
                break;
            }
            
            yield return null;
        }
        
        // Hide skip prompt
        if (skipPromptUI != null)
        {
            skipPromptUI.SetActive(false);
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_PlayVideoCutscene] Video {(skipped ? "skipped" : "completed")}");
        }
        
        onComplete?.Invoke(StepOutcome.Success(skipped ? "Video skipped" : "Video completed"));
    }
    
    private bool CheckSkipInput()
    {
        // Check for any input
        return Input.anyKeyDown;
    }
    
    public override bool Validate()
    {
        if (videoClip == null && string.IsNullOrEmpty(videoPath))
        {
            Debug.LogWarning("[Step_PlayVideoCutscene] No video clip or path specified!");
            return false;
        }
        return true;
    }
}

