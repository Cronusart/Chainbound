using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Example integration showing how to hold time at night until a tutorial/dialogue completes.
/// 
/// Use Case:
/// - New game starts at night
/// - Time is held until introductory dialogue finishes
/// - After dialogue, normal day/night cycling begins
/// 
/// Setup:
/// 1. Attach to a manager object
/// 2. Set startHeldAtNight = true
/// 3. Call ReleaseTutorialNightHold() when dialogue completes
///    (or connect OnDialogueComplete event to your dialogue system)
/// </summary>
public class TutorialNightHoldExample : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TimeOfDayManager reference. Uses singleton if null.")]
    [SerializeField] private TimeOfDayManager timeManager;
    
    [Header("Settings")]
    [Tooltip("Start with time held at night")]
    [SerializeField] private bool startHeldAtNight = true;
    
    [Tooltip("Specific normalized time for night (0.1 = deep night)")]
    [Range(0f, 0.2f)]
    [SerializeField] private float nightHoldTime = 0.1f;
    
    [Tooltip("Reason shown in debug/UI")]
    [SerializeField] private string holdReason = "Tutorial Dialogue";
    
    [Header("Events")]
    [Tooltip("Fired when hold is acquired")]
    public UnityEvent OnHoldAcquired;
    
    [Tooltip("Fired when hold is released and time resumes")]
    public UnityEvent OnHoldReleased;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDebugUI = true;
    
    // State
    private HoldHandle activeHold;
    private bool isHolding = false;
    
    /// <summary>True if currently holding time at night.</summary>
    public bool IsHolding => isHolding;
    
    private void Start()
    {
        // Auto-find time manager
        if (timeManager == null)
        {
            timeManager = TimeOfDayManager.Instance;
        }
        
        if (timeManager == null)
        {
            Debug.LogError("[TutorialNightHoldExample] No TimeOfDayManager found!");
            return;
        }
        
        // Start holding at night if configured
        if (startHeldAtNight)
        {
            AcquireNightHold();
        }
    }
    
    /// <summary>
    /// Acquire a hold to keep time at night.
    /// Call this at game start or when entering a night-required sequence.
    /// </summary>
    public void AcquireNightHold()
    {
        if (isHolding)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[TutorialNightHoldExample] Already holding night!");
            }
            return;
        }
        
        if (timeManager == null)
        {
            timeManager = TimeOfDayManager.Instance;
            if (timeManager == null)
            {
                Debug.LogError("[TutorialNightHoldExample] Cannot acquire hold: no TimeOfDayManager!");
                return;
            }
        }
        
        // Acquire hold at specified night time
        activeHold = timeManager.AcquireHold(holdReason, nightHoldTime, TimeSegment.Night);
        isHolding = true;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[TutorialNightHoldExample] Night hold acquired: t={nightHoldTime:F2}, reason='{holdReason}'");
        }
        
        OnHoldAcquired?.Invoke();
    }
    
    /// <summary>
    /// Release the night hold and allow normal time cycling.
    /// Call this when the tutorial/dialogue completes.
    /// </summary>
    public void ReleaseTutorialNightHold()
    {
        if (!isHolding)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning("[TutorialNightHoldExample] Not currently holding!");
            }
            return;
        }
        
        if (activeHold.IsValid && !activeHold.IsReleased)
        {
            activeHold.Release();
        }
        
        isHolding = false;
        
        if (enableDebugLogs)
        {
            Debug.Log("[TutorialNightHoldExample] Night hold released - time cycling resumes");
        }
        
        OnHoldReleased?.Invoke();
    }
    
    /// <summary>
    /// Release hold and immediately set time to dawn (new day beginning).
    /// Nice transition effect after tutorial.
    /// </summary>
    public void ReleaseAndSkipToDawn()
    {
        ReleaseTutorialNightHold();
        
        if (timeManager != null)
        {
            timeManager.SetTimeToSegment(TimeSegment.Dawn);
            
            if (enableDebugLogs)
            {
                Debug.Log("[TutorialNightHoldExample] Skipped to dawn - new day begins!");
            }
        }
    }
    
    private void OnDestroy()
    {
        // Clean up hold on destroy
        if (isHolding && activeHold.IsValid)
        {
            activeHold.Release();
        }
    }
    
    // ============================================
    // DEBUG UI
    // ============================================
    
    private void OnGUI()
    {
        if (!showDebugUI) return;
        
        GUILayout.BeginArea(new Rect(320, 10, 250, 120));
        GUILayout.BeginVertical("box");
        
        GUILayout.Label("Tutorial Night Hold");
        GUILayout.Label($"Holding: {isHolding}");
        GUILayout.Label($"Reason: {holdReason}");
        
        GUILayout.Space(5);
        
        GUI.enabled = !isHolding;
        if (GUILayout.Button("Acquire Night Hold"))
        {
            AcquireNightHold();
        }
        
        GUI.enabled = isHolding;
        if (GUILayout.Button("Release Hold"))
        {
            ReleaseTutorialNightHold();
        }
        
        if (GUILayout.Button("Release & Skip to Dawn"))
        {
            ReleaseAndSkipToDawn();
        }
        
        GUI.enabled = true;
        
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}

