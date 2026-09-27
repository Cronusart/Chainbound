using UnityEngine;

/// <summary>
/// Camera profile ScriptableObject for storing camera configuration.
/// Supports multiple profiles (Explore, Battle, etc.) for easy switching.
/// 
/// GUARDRAIL: At runtime, only runtime copies (Instantiate) are modified during blends.
/// Original asset profiles are never modified. Use CameraProfileTemplateValidator in Editor
/// to validate/reset profiles against a template.
/// </summary>
[CreateAssetMenu(fileName = "CameraProfile", menuName = "Chainbound/Camera/Profiles/Camera Profile")]
public class CameraProfile : ScriptableObject
{
    [Header("Projection")]
    [Tooltip("Camera projection mode")]
    public bool isOrthographic = false; // Default to perspective for 3D top-down feel
    [Tooltip("Field of View for perspective mode (base FOV - zoom adjusts from this)")]
    [Range(30f, 120f)]
    public float fieldOfView = 60f; // Reduced from 90 to prevent warping
    [Tooltip("Near clipping plane distance (should be very small for top-down to prevent clipping)")]
    public float nearClipPlane = 0.01f;
    [Tooltip("Far clipping plane distance")]
    public float farClipPlane = 1000f;

    [Header("Zoom")]
    [Tooltip("Default zoom level (orthographic size or FOV multiplier)")]
    public float defaultZoom = 5f;
    [Tooltip("Minimum zoom level (zoomed in)")]
    public float minZoom = 1f;
    [Tooltip("Maximum zoom level (zoomed out)")]
    public float maxZoom = 15f; // Increased from 10 to allow more zoom out

    [Header("Angle")]
    [Tooltip("Pitch angle in degrees (45 = top-down)")]
    public float pitchAngle = 45f;

    [Header("Follow")]
    [Tooltip("Camera offset from target (Y = height, Z = distance back)")]
    public Vector3 followOffset = new Vector3(0, 25, -15); // Increased height and distance for better top-down view
    [Tooltip("Follow smoothing time (for SmoothDamp)")]
    public float followSmoothTime = 0.3f;
    [Tooltip("Follow speed (for Lerp, if not using SmoothDamp)")]
    public float followSpeed = 2f;
    [Tooltip("Use SmoothDamp instead of Lerp")]
    public bool useSmoothDamp = true;
    
    [Header("Y-Axis Following (Terrain Height)")]
    [Tooltip("If true, camera Y position follows target Y position (for heightmaps/terrain)")]
    public bool followYAxis = true;
    [Tooltip("Y offset above target (camera height relative to player)")]
    public float yAxisOffset = 25f;
    [Tooltip("Y-axis smoothing time (separate from XZ smoothing, can be faster/slower)")]
    public float yAxisSmoothTime = 0.2f;
    [Tooltip("Minimum Y position (prevents camera from going too low)")]
    public float minYPosition = -100f;
    [Tooltip("Maximum Y position (prevents camera from going too high)")]
    public float maxYPosition = 1000f;

    [Header("Rotation")]
    [Tooltip("Yaw rotation duration in seconds")]
    public float yawRotateTime = 0.2f;

    [Header("Ambient Motion (Optional)")]
    [Tooltip("If true, ambient bob/zoom motion only runs while follow position is paused (camera shots, anchored dialogue, etc.).")]
    public bool ambientMotionOnlyWhenFollowPaused = true;
    [Tooltip("Continuous local camera bob amplitude. Zero disables positional ambient motion.")]
    public Vector3 ambientBobAmplitude = Vector3.zero;
    [Tooltip("Continuous bob frequency in cycles per second.")]
    public float ambientBobFrequency = 0.12f;
    [Tooltip("Continuous zoom drift amplitude added on top of the current zoom value. Zero disables zoom motion.")]
    public float ambientZoomAmplitude = 0f;
    [Tooltip("Continuous zoom drift frequency in cycles per second.")]
    public float ambientZoomFrequency = 0.08f;

    [Header("Zoom Transitions")]
    [Tooltip("Zoom smoothing time")]
    public float zoomSmoothTime = 0.5f;
    [Tooltip("Idle zoom level (zoomed IN - closer view, lower value)")]
    public float idleZoom = 3f; // Increased from 1 for better baseline
    [Tooltip("Walk zoom level (baseline - medium view)")]
    public float walkZoom = 7f; // Increased from 5 for more zoom out
    [Tooltip("Sprint zoom level (zoomed OUT - wider view, higher value)")]
    public float sprintZoom = 12f; // Increased from 10 for more zoom out

    [Header("Movement Thresholds")]
    [Tooltip("Speed threshold for idle state")]
    public float idleThreshold = 0.1f;
    [Tooltip("Speed threshold for sprint state")]
    public float sprintThreshold = 2.5f;

    [Header("Lighting (Optional)")]
    [Tooltip("Use flat ambient lighting (no gradients)")]
    public bool useFlatAmbient = true;
    [Tooltip("Ambient intensity for flat lighting")]
    public float ambientIntensity = 1.0f;

    /// <summary>
    /// Get zoom level based on movement state.
    /// </summary>
    public float GetZoomForState(CameraMovementMode mode)
    {
        return mode switch
        {
            CameraMovementMode.Idle => idleZoom,
            CameraMovementMode.Walk => walkZoom,
            CameraMovementMode.Sprint => sprintZoom,
            _ => walkZoom
        };
    }

    /// <summary>
    /// Copy all values from another profile. Use for Editor template reset or syncing.
    /// </summary>
    public void CopyFrom(CameraProfile source)
    {
        if (source == null) return;

        isOrthographic = source.isOrthographic;
        fieldOfView = source.fieldOfView;
        nearClipPlane = source.nearClipPlane;
        farClipPlane = source.farClipPlane;
        defaultZoom = source.defaultZoom;
        minZoom = source.minZoom;
        maxZoom = source.maxZoom;
        pitchAngle = source.pitchAngle;
        followOffset = source.followOffset;
        followSmoothTime = source.followSmoothTime;
        followSpeed = source.followSpeed;
        useSmoothDamp = source.useSmoothDamp;
        followYAxis = source.followYAxis;
        yAxisOffset = source.yAxisOffset;
        yAxisSmoothTime = source.yAxisSmoothTime;
        minYPosition = source.minYPosition;
        maxYPosition = source.maxYPosition;
        yawRotateTime = source.yawRotateTime;
        ambientMotionOnlyWhenFollowPaused = source.ambientMotionOnlyWhenFollowPaused;
        ambientBobAmplitude = source.ambientBobAmplitude;
        ambientBobFrequency = source.ambientBobFrequency;
        ambientZoomAmplitude = source.ambientZoomAmplitude;
        ambientZoomFrequency = source.ambientZoomFrequency;
        zoomSmoothTime = source.zoomSmoothTime;
        idleZoom = source.idleZoom;
        walkZoom = source.walkZoom;
        sprintZoom = source.sprintZoom;
        idleThreshold = source.idleThreshold;
        sprintThreshold = source.sprintThreshold;
        useFlatAmbient = source.useFlatAmbient;
        ambientIntensity = source.ambientIntensity;
    }

    /// <summary>
    /// Compare this profile against another for equality (all fields). Returns diff summary.
    /// </summary>
    public string GetDiffSummary(CameraProfile other)
    {
        if (other == null) return "null (other)";

        var diffs = new System.Collections.Generic.List<string>();

        if (isOrthographic != other.isOrthographic) diffs.Add("isOrthographic");
        if (!Mathf.Approximately(fieldOfView, other.fieldOfView)) diffs.Add("fieldOfView");
        if (!Mathf.Approximately(nearClipPlane, other.nearClipPlane)) diffs.Add("nearClipPlane");
        if (!Mathf.Approximately(farClipPlane, other.farClipPlane)) diffs.Add("farClipPlane");
        if (!Mathf.Approximately(defaultZoom, other.defaultZoom)) diffs.Add("defaultZoom");
        if (!Mathf.Approximately(minZoom, other.minZoom)) diffs.Add("minZoom");
        if (!Mathf.Approximately(maxZoom, other.maxZoom)) diffs.Add("maxZoom");
        if (!Mathf.Approximately(pitchAngle, other.pitchAngle)) diffs.Add("pitchAngle");
        if (followOffset != other.followOffset) diffs.Add("followOffset");
        if (!Mathf.Approximately(followSmoothTime, other.followSmoothTime)) diffs.Add("followSmoothTime");
        if (!Mathf.Approximately(followSpeed, other.followSpeed)) diffs.Add("followSpeed");
        if (useSmoothDamp != other.useSmoothDamp) diffs.Add("useSmoothDamp");
        if (followYAxis != other.followYAxis) diffs.Add("followYAxis");
        if (!Mathf.Approximately(yAxisOffset, other.yAxisOffset)) diffs.Add("yAxisOffset");
        if (!Mathf.Approximately(yAxisSmoothTime, other.yAxisSmoothTime)) diffs.Add("yAxisSmoothTime");
        if (!Mathf.Approximately(minYPosition, other.minYPosition)) diffs.Add("minYPosition");
        if (!Mathf.Approximately(maxYPosition, other.maxYPosition)) diffs.Add("maxYPosition");
        if (!Mathf.Approximately(yawRotateTime, other.yawRotateTime)) diffs.Add("yawRotateTime");
        if (ambientMotionOnlyWhenFollowPaused != other.ambientMotionOnlyWhenFollowPaused) diffs.Add("ambientMotionOnlyWhenFollowPaused");
        if (ambientBobAmplitude != other.ambientBobAmplitude) diffs.Add("ambientBobAmplitude");
        if (!Mathf.Approximately(ambientBobFrequency, other.ambientBobFrequency)) diffs.Add("ambientBobFrequency");
        if (!Mathf.Approximately(ambientZoomAmplitude, other.ambientZoomAmplitude)) diffs.Add("ambientZoomAmplitude");
        if (!Mathf.Approximately(ambientZoomFrequency, other.ambientZoomFrequency)) diffs.Add("ambientZoomFrequency");
        if (!Mathf.Approximately(zoomSmoothTime, other.zoomSmoothTime)) diffs.Add("zoomSmoothTime");
        if (!Mathf.Approximately(idleZoom, other.idleZoom)) diffs.Add("idleZoom");
        if (!Mathf.Approximately(walkZoom, other.walkZoom)) diffs.Add("walkZoom");
        if (!Mathf.Approximately(sprintZoom, other.sprintZoom)) diffs.Add("sprintZoom");
        if (!Mathf.Approximately(idleThreshold, other.idleThreshold)) diffs.Add("idleThreshold");
        if (!Mathf.Approximately(sprintThreshold, other.sprintThreshold)) diffs.Add("sprintThreshold");
        if (useFlatAmbient != other.useFlatAmbient) diffs.Add("useFlatAmbient");
        if (!Mathf.Approximately(ambientIntensity, other.ambientIntensity)) diffs.Add("ambientIntensity");

        return diffs.Count == 0 ? "identical" : string.Join(", ", diffs);
    }
}

/// <summary>
/// Movement mode for camera zoom adaptation.
/// </summary>
public enum CameraMovementMode
{
    Idle,
    Walk,
    Sprint
}
