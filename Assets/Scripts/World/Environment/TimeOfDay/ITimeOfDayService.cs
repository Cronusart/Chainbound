using System;

/// <summary>
/// Service interface for time-of-day queries.
/// Used by spawners, AI, and other systems that need time awareness.
/// </summary>
public interface ITimeOfDayService
{
    /// <summary>
    /// Current normalized time in [0..1) range.
    /// 0 = midnight, 0.25 = sunrise, 0.5 = noon, 0.75 = sunset
    /// </summary>
    float NormalizedTime { get; }
    
    /// <summary>
    /// Current time segment (Night/Dawn/Day/Dusk).
    /// </summary>
    TimeSegment CurrentSegment { get; }
    
    /// <summary>
    /// Convenience: true if current segment is Night.
    /// </summary>
    bool IsNight { get; }
    
    /// <summary>
    /// True if time is currently paused (no advancement).
    /// </summary>
    bool IsPaused { get; }
    
    /// <summary>
    /// True if time is currently held at a specific value/segment.
    /// </summary>
    bool IsHeld { get; }
    
    /// <summary>
    /// Reason for current hold (empty if not held).
    /// </summary>
    string HoldReason { get; }
    
    /// <summary>
    /// Fired when normalized time changes (throttled 4-10 Hz for performance).
    /// Parameter: new normalized time value.
    /// </summary>
    event Action<float> OnTimeNormalizedChanged;
    
    /// <summary>
    /// Fired when segment changes.
    /// Parameters: (previous segment, new segment)
    /// </summary>
    event Action<TimeSegment, TimeSegment> OnSegmentChanged;
    
    /// <summary>
    /// Convenience event: fired when transitioning to/from night.
    /// Parameter: true if now night, false if now day/dawn/dusk.
    /// </summary>
    event Action<bool> OnDayNightChanged;
    
    /// <summary>
    /// Fired when pause state changes.
    /// Parameter: true if now paused.
    /// </summary>
    event Action<bool> OnPausedChanged;
    
    /// <summary>
    /// Fired when hold state changes.
    /// Parameters: (isHeld, reason)
    /// </summary>
    event Action<bool, string> OnHoldChanged;
}

/// <summary>
/// Extended interface for systems that need to control time.
/// </summary>
public interface ITimeOfDayController : ITimeOfDayService
{
    /// <summary>
    /// Acquire a pause handle. Time will not advance while any pause is active.
    /// </summary>
    /// <param name="reason">Reason for pausing (for debugging)</param>
    /// <returns>Handle to release when done</returns>
    PauseHandle AcquirePause(string reason);
    
    /// <summary>
    /// Acquire a hold handle. Time is forced to a specific value/segment.
    /// </summary>
    /// <param name="reason">Reason for hold (for debugging/UI)</param>
    /// <param name="forcedTimeNormalized">Optional: force to specific time value</param>
    /// <param name="forcedSegment">Optional: force to specific segment (uses segment midpoint if no time specified)</param>
    /// <returns>Handle to release when done</returns>
    HoldHandle AcquireHold(string reason, float? forcedTimeNormalized = null, TimeSegment? forcedSegment = null);
    
    /// <summary>
    /// Manually set time (works even when paused, for scene staging).
    /// </summary>
    void SetTime(float normalizedTime);
}

