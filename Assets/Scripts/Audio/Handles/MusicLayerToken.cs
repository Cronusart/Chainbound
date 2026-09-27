using System;

/// <summary>
/// Handle for a music layer token request. Dispose or call Release() when done.
/// Prevents multiple systems from fighting over layer control.
/// 
/// Usage:
/// var token = MusicConductor.Instance.AcquireLayerToken("Combat", 0.5f);
/// // ... combat active ...
/// token.Release();
/// </summary>
public struct MusicLayerToken : IDisposable
{
    private readonly int id;
    private readonly Action<int> releaseCallback;
    private bool isReleased;
    
    /// <summary>Unique identifier for this token request.</summary>
    public int Id => id;
    
    /// <summary>Reason provided when token was acquired.</summary>
    public string Reason { get; }
    
    /// <summary>True if this token has been released.</summary>
    public bool IsReleased => isReleased;
    
    /// <summary>True if this is a valid token (not default-constructed).</summary>
    public bool IsValid => releaseCallback != null;
    
    internal MusicLayerToken(int id, string reason, Action<int> releaseCallback)
    {
        this.id = id;
        this.Reason = reason;
        this.releaseCallback = releaseCallback;
        this.isReleased = false;
    }
    
    /// <summary>
    /// Release this token. Layer will fade out if no other tokens are active.
    /// Uses the music cue's default crossfade duration.
    /// </summary>
    public void Release()
    {
        if (isReleased || releaseCallback == null) return;
        
        // Release via conductor (handles token removal and volume update)
        if (MusicConductor.Instance != null)
        {
            MusicConductor.Instance.ReleaseLayerToken(this, 0f);
        }
        
        isReleased = true;
    }
    
    public void Dispose() => Release();
}
