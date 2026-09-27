using UnityEngine;

/// <summary>
/// Stores complete camera state for save/restore operations.
/// Used by camera zones to properly restore camera after exiting.
/// </summary>
[System.Serializable]
public struct CameraStateSnapshot
{
    /// <summary>
    /// Active camera profile at time of snapshot.
    /// </summary>
    public CameraProfile profile;
    
    /// <summary>
    /// Current zoom target (not necessarily profile default).
    /// </summary>
    public float zoomTarget;
    
    /// <summary>
    /// Yaw index in 90° increments (0-3 representing N/E/S/W).
    /// 0 = default (no rotation)
    /// 1 = 90° clockwise
    /// 2 = 180°
    /// 3 = 270° (or -90°)
    /// </summary>
    public int yawIndex90;
    
    /// <summary>
    /// Current pitch angle override (or profile pitch if no override).
    /// </summary>
    public float pitchAngle;
    
    /// <summary>
    /// Whether pitch was overridden (vs using profile pitch).
    /// </summary>
    public bool hasPitchOverride;
    
    /// <summary>
    /// Whether this snapshot is valid (has been populated).
    /// </summary>
    public bool isValid;
    
    /// <summary>
    /// Create a snapshot from current camera controller state.
    /// </summary>
    public static CameraStateSnapshot Create(
        CameraProfile profile,
        float zoomTarget,
        int yawIndex90,
        float pitchAngle,
        bool hasPitchOverride = false)
    {
        return new CameraStateSnapshot
        {
            profile = profile,
            zoomTarget = zoomTarget,
            yawIndex90 = yawIndex90,
            pitchAngle = pitchAngle,
            hasPitchOverride = hasPitchOverride,
            isValid = true
        };
    }
    
    /// <summary>
    /// Get yaw angle in degrees from yawIndex90.
    /// </summary>
    public float YawAngle => yawIndex90 * 90f;
    
    /// <summary>
    /// Returns an invalid/empty snapshot.
    /// </summary>
    public static CameraStateSnapshot Invalid => new CameraStateSnapshot { isValid = false };
}

