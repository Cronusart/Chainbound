using System;

/// <summary>
/// Represents distinct time-of-day segments for creature spawning, lighting, and gameplay logic.
/// </summary>
public enum TimeSegment
{
/// <summary>Night: CST [00:00..04:48) and [19:12..24:00) — [0.0..0.20) and [0.80..1.0)</summary>
    Night,
    
    /// <summary>Dawn: CST [04:48..07:12) — [0.20..0.30)</summary>
    Dawn,
    
    /// <summary>Day: CST [07:12..16:48) — [0.30..0.70)</summary>
    Day,
    
    /// <summary>Dusk: CST [16:48..19:12) — [0.70..0.80)</summary>
    Dusk
}

/// <summary>
/// Configurable segment thresholds for time-of-day boundaries.
/// </summary>
[Serializable]
public struct TimeSegmentThresholds
{
    /// <summary>Start of dawn (end of night). Default: 0.20</summary>
    public float dawnStart;
    
    /// <summary>Start of day (end of dawn). Default: 0.30</summary>
    public float dayStart;
    
    /// <summary>Start of dusk (end of day). Default: 0.70</summary>
    public float duskStart;
    
    /// <summary>Start of night (end of dusk). Default: 0.80</summary>
    public float nightStart;
    
    /// <summary>
    /// Creates default thresholds matching the specification.
    /// Night: [0.0..0.20) and [0.80..1.0)
    /// Dawn: [0.20..0.30)
    /// Day: [0.30..0.70)
    /// Dusk: [0.70..0.80)
    /// </summary>
    public static TimeSegmentThresholds Default => new TimeSegmentThresholds
    {
        dawnStart = 0.20f,
        dayStart = 0.30f,
        duskStart = 0.70f,
        nightStart = 0.80f
    };
    
    /// <summary>
    /// Determines which segment the given normalized time falls into.
    /// </summary>
    /// <param name="normalizedTime">Time in [0..1) range</param>
    public TimeSegment GetSegment(float normalizedTime)
    {
        // Wrap to [0..1) range
        normalizedTime = normalizedTime - UnityEngine.Mathf.Floor(normalizedTime);
        
        if (normalizedTime < dawnStart || normalizedTime >= nightStart)
            return TimeSegment.Night;
        if (normalizedTime < dayStart)
            return TimeSegment.Dawn;
        if (normalizedTime < duskStart)
            return TimeSegment.Day;
        return TimeSegment.Dusk;
    }
    
    /// <summary>
    /// Gets the normalized time range for a specific segment.
    /// Note: Night wraps around midnight (returns two ranges).
    /// </summary>
    public (float start, float end) GetSegmentRange(TimeSegment segment)
    {
        switch (segment)
        {
            case TimeSegment.Night:
                // Night spans [0..dawnStart) and [nightStart..1.0)
                // Return the primary range (nightStart..1.0 -> 0..dawnStart)
                return (nightStart, dawnStart);
            case TimeSegment.Dawn:
                return (dawnStart, dayStart);
            case TimeSegment.Day:
                return (dayStart, duskStart);
            case TimeSegment.Dusk:
                return (duskStart, nightStart);
            default:
                return (0f, 1f);
        }
    }
    
    /// <summary>
    /// Validates that thresholds are in proper order.
    /// </summary>
    public bool IsValid()
    {
        return dawnStart > 0f && 
               dawnStart < dayStart && 
               dayStart < duskStart && 
               duskStart < nightStart && 
               nightStart < 1f;
    }
}

