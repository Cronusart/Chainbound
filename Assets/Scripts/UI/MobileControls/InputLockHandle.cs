using System;

/// <summary>
/// Handle for an input lock request. Dispose or call Release() when done.
/// Prevents multiple systems from fighting over input control.
/// </summary>
public struct InputLockHandle : IDisposable
{
    private readonly int id;
    private readonly Action<int> releaseCallback;
    private bool isReleased;
    
    /// <summary>Unique identifier for this lock request.</summary>
    public int Id => id;
    
    /// <summary>Reason provided when lock was acquired.</summary>
    public string Reason { get; }
    
    /// <summary>Channel this lock affects.</summary>
    public InputLockChannel Channel { get; }
    
    /// <summary>True if this handle has been released.</summary>
    public bool IsReleased => isReleased;
    
    /// <summary>True if this is a valid handle (not default-constructed).</summary>
    public bool IsValid => releaseCallback != null;
    
    internal InputLockHandle(int id, string reason, InputLockChannel channel, Action<int> releaseCallback)
    {
        this.id = id;
        this.Reason = reason;
        this.Channel = channel;
        this.releaseCallback = releaseCallback;
        this.isReleased = false;
    }
    
    /// <summary>
    /// Release this lock. Input will resume if no other locks are active.
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
/// Channels for input locking. Allows granular control.
/// </summary>
public enum InputLockChannel
{
    /// <summary>Lock movement only (walk, sprint, jump)</summary>
    Movement,
    
    /// <summary>Lock interaction only (interact button)</summary>
    Interaction,
    
    /// <summary>Lock all gameplay input (movement + interaction + player menu)</summary>
    All,

    /// <summary>
    /// Lock the gameplay player menu without disabling movement or interaction.
    /// Existing <see cref="All"/> locks also cover this channel.
    /// </summary>
    PlayerMenu
}

