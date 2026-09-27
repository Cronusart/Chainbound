using UnityEngine;

public enum CameraShotYawMode
{
    KeepCurrent,
    Relative90,
    Absolute90
}

public enum CameraShotFollowMode
{
    UseSessionDefault,
    PauseFollowAndMoveRig,
    FollowShotTarget
}

/// <summary>
/// ScriptableObject defining a camera shot preset.
/// Uses a CameraProfile for zoom/pitch/etc (easy to configure and test in editor).
/// Adds target, timing, and movement (pan, look-at) on top.
/// </summary>
[CreateAssetMenu(fileName = "CameraShotPreset", menuName = "Chainbound/Camera/Shots/Camera Shot Preset")]
public class CameraShotPresetSO : ScriptableObject
{
    [Header("Camera Profile")]
    [Tooltip("Profile for shot (zoom, pitch, etc). When set, overrides targetZoom/targetPitch. Easier to configure in editor.")]
    [SerializeField] private CameraProfile shotProfile;

    [Header("Target")]
    [Tooltip("Target transform ID (looked up at runtime) or world position")]
    [SerializeField] private string targetTransformId = "";

    [Tooltip("World position target (used if transform ID is empty)")]
    [SerializeField] private Vector3 targetPosition = Vector3.zero;

    [Tooltip("Use world position instead of transform lookup")]
    [SerializeField] private bool useWorldPosition = false;

    [Header("Follow Behavior")]
    [Tooltip("Controls whether this shot uses the dialogue/session default, pauses follow and moves the rig manually, or keeps live follow on the shot target.")]
    [SerializeField] private CameraShotFollowMode followMode = CameraShotFollowMode.UseSessionDefault;

    [Header("Timing")]
    [Tooltip("Shot duration in seconds")]
    [Range(0.1f, 10f)]
    [SerializeField] private float duration = 2f;

    [Tooltip("Optional faster duration when this preset is used for dialogue line framing. <= 0 uses the bridge override or base shot duration.")]
    [SerializeField] private float dialogueLineDurationOverride = 0f;

    [Tooltip("Hold at target after reaching it (seconds)")]
    [Range(0f, 10f)]
    [SerializeField] private float holdTime = 0f;

    [Tooltip("If true, shot completes without restoring. Use Step_RestoreCameraFollow later to restore default profile.")]
    [SerializeField] private bool deferRestore = false;

    [Tooltip("Easing curve for movement")]
    [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Yaw Rotation (Optional)")]
    [Tooltip("Optional shot-authored yaw behavior. KeepCurrent leaves yaw unchanged unless a prior dialogue line shot temporarily overrode it.")]
    [SerializeField] private CameraShotYawMode yawMode = CameraShotYawMode.KeepCurrent;

    [Tooltip("Yaw rotation in 90-degree steps when Yaw Mode = Relative90.")]
    [Range(-3, 3)]
    [SerializeField] private int yawRotation90Steps = 0;

    [Tooltip("Absolute yaw index (0-3) when Yaw Mode = Absolute90. 0 = default/N, 1 = E, 2 = S, 3 = W.")]
    [Range(0, 3)]
    [SerializeField] private int absoluteYawIndex = 0;

    [Header("Cinematic Hold (Optional)")]
    [Tooltip("Additional world-space rig offset applied over Hold Time after the shot settles.")]
    [SerializeField] private Vector3 holdSettleOffset = Vector3.zero;

    [Tooltip("Normalized curve used to evaluate Hold Settle Offset over Hold Time.")]
    [SerializeField] private AnimationCurve holdSettleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Additional zoom offset applied over Hold Time after the shot settles.")]
    [SerializeField] private float holdZoomOffset = 0f;

    [Tooltip("Normalized curve used to evaluate Hold Zoom Offset over Hold Time.")]
    [SerializeField] private AnimationCurve holdZoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Additional world-space bob amplitude applied over Hold Time after the shot settles. Zero disables bobbing.")]
    [SerializeField] private Vector3 holdBobAmplitude = Vector3.zero;

    [Tooltip("Bob frequency in cycles per second during Hold Time.")]
    [SerializeField] private float holdBobFrequency = 0.2f;

    [Header("Camera Overrides (used when shotProfile is null)")]
    [Tooltip("Target zoom (-1 = don't change)")]
    [SerializeField] private float targetZoom = -1f;

    [Tooltip("Target pitch in degrees (-1 = don't change)")]
    [Range(-1f, 90f)]
    [SerializeField] private float targetPitch = -1f;

    [Tooltip("Camera offset from target")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    [Header("Look At")]
    [Tooltip("Look at shot target (the object/position being framed)")]
    [SerializeField] private bool lookAtTarget = true;

    [Tooltip("Smooth rotation to target")]
    [SerializeField] private bool smoothRotation = true;

    [Header("Occlusion Reveal (Optional)")]
    [Tooltip("Optional cinematic reveal profile. Null preserves existing camera shot behavior.")]
    [SerializeField] private CinematicOcclusionProfileSO occlusionRevealProfile;

    // ============================================
    // PUBLIC ACCESSORS
    // ============================================

    public CameraProfile ShotProfile => shotProfile;
    public string TargetTransformId => targetTransformId;
    public Vector3 TargetPosition => targetPosition;
    public bool UseWorldPosition => useWorldPosition;
    public CameraShotFollowMode FollowMode => followMode;
    public float Duration => duration;
    public float DialogueLineDurationOverride => dialogueLineDurationOverride;
    public float HoldTime => holdTime;
    public bool DeferRestore => deferRestore;
    public AnimationCurve EasingCurve => easingCurve;
    public CameraShotYawMode YawMode => yawMode;
    public int YawRotation90Steps => yawRotation90Steps;
    public int AbsoluteYawIndex => absoluteYawIndex;
    public Vector3 HoldSettleOffset => holdSettleOffset;
    public AnimationCurve HoldSettleCurve => holdSettleCurve;
    public float HoldZoomOffset => holdZoomOffset;
    public AnimationCurve HoldZoomCurve => holdZoomCurve;
    public Vector3 HoldBobAmplitude => holdBobAmplitude;
    public float HoldBobFrequency => holdBobFrequency;
    public float TargetZoom => targetZoom;
    public float TargetPitch => targetPitch;
    public Vector3 Offset => offset;
    public bool LookAtTarget => lookAtTarget;
    public bool SmoothRotation => smoothRotation;
    public CinematicOcclusionProfileSO OcclusionRevealProfile => occlusionRevealProfile;

    public bool HasYawOverride
    {
        get
        {
            return yawMode switch
            {
                CameraShotYawMode.Relative90 => yawRotation90Steps != 0,
                CameraShotYawMode.Absolute90 => true,
                _ => false,
            };
        }
    }
    
    private void OnValidate()
    {
        duration = Mathf.Max(0.1f, duration);
        dialogueLineDurationOverride = Mathf.Max(0f, dialogueLineDurationOverride);
        holdTime = Mathf.Max(0f, holdTime);
        holdBobFrequency = Mathf.Max(0f, holdBobFrequency);
        absoluteYawIndex = ((absoluteYawIndex % 4) + 4) % 4;
    }
}

