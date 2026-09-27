using System;

/// <summary>
/// Handle for a time pause request. Dispose or call Release() when done.
/// </summary>
public struct PauseHandle : IDisposable
{
    private readonly int id;
    private readonly Action<int> releaseCallback;
    private bool isReleased;
    
    /// <summary>Unique identifier for this pause request.</summary>
    public int Id => id;
    
    /// <summary>Reason provided when pause was acquired.</summary>
    public string Reason { get; }
    
    /// <summary>True if this handle has been released.</summary>
    public bool IsReleased => isReleased;
    
    /// <summary>True if this is a valid handle (not default-constructed).</summary>
    public bool IsValid => releaseCallback != null;
    
    internal PauseHandle(int id, string reason, Action<int> releaseCallback)
    {
        this.id = id;
        this.Reason = reason;
        this.releaseCallback = releaseCallback;
        this.isReleased = false;
    }
    
    /// <summary>
    /// Release this pause. Time will resume if no other pauses are active.
    /// </summary>
    public void Release()
    {
        if (isReleased || releaseCallback == null) return;
        isReleased = true;
        releaseCallback(id);
    }
    
    public void Dispose() => Release();
}

/// <summary>
/// Handle for a time hold request. Dispose or call Release() when done.
/// </summary>
public struct HoldHandle : IDisposable
{
    private readonly int id;
    private readonly Action<int> releaseCallback;
    private bool isReleased;
    
    /// <summary>Unique identifier for this hold request.</summary>
    public int Id => id;
    
    /// <summary>Reason provided when hold was acquired.</summary>
    public string Reason { get; }
    
    /// <summary>Forced normalized time value, if specified.</summary>
    public float? ForcedTime { get; }
    
    /// <summary>Forced segment, if specified.</summary>
    public TimeSegment? ForcedSegment { get; }
    
    /// <summary>True if this handle has been released.</summary>
    public bool IsReleased => isReleased;
    
    /// <summary>True if this is a valid handle (not default-constructed).</summary>
    public bool IsValid => releaseCallback != null;
    
    internal HoldHandle(int id, string reason, float? forcedTime, TimeSegment? forcedSegment, Action<int> releaseCallback)
    {
        this.id = id;
        this.Reason = reason;
        this.ForcedTime = forcedTime;
        this.ForcedSegment = forcedSegment;
        this.releaseCallback = releaseCallback;
        this.isReleased = false;
    }
    
    /// <summary>
    /// Release this hold. Time will resume normal cycling if no other holds are active.
    /// </summary>
    public void Release()
    {
        if (isReleased || releaseCallback == null) return;
        isReleased = true;
        releaseCallback(id);
    }
    
    public void Dispose() => Release();
}

