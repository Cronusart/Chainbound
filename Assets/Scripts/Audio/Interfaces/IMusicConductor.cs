/// <summary>
/// Interface for the persistent soundtrack conductor service.
/// Handles scene soundtrack requests, cue playback, and seamless intro-to-loop scheduling.
/// </summary>
public readonly struct MusicDuckToken
{
    private readonly MusicConductor owner;
    private readonly int tokenId;

    internal MusicDuckToken(MusicConductor owner, int tokenId)
    {
        this.owner = owner;
        this.tokenId = tokenId;
    }

    public bool IsValid => owner != null && tokenId > 0;

    /// <summary>Idempotently release this request. Other active tokens continue ducking music.</summary>
    public void Release()
    {
        owner?.ReleaseDuckToken(tokenId);
    }
}

public readonly struct MusicEnvironmentToken
{
    private readonly MusicConductor owner;
    private readonly int tokenId;

    internal MusicEnvironmentToken(MusicConductor owner, int tokenId)
    {
        this.owner = owner;
        this.tokenId = tokenId;
    }

    public bool IsValid => owner != null && tokenId > 0;

    /// <summary>Idempotently release this environment request.</summary>
    public void Release()
    {
        owner?.ReleaseEnvironmentToken(tokenId);
    }
}

public interface IMusicConductor
{
    /// <summary>
    /// Play a soundtrack cue directly.
    /// </summary>
    void PlayCue(SoundtrackCueSO cue, float fadeSeconds = 0f, bool forceRestart = false);

    /// <summary>
    /// Resolve and execute soundtrack policy for a scene at a specific transition moment.
    /// </summary>
    void RequestSceneSoundtrack(
        string sceneName,
        SoundtrackTriggerMoment moment,
        SoundtrackCueSO overrideCue = null,
        bool forceRestart = false);

    /// <summary>Apply soundtrack policy after an overworld chunk becomes active.</summary>
    void RequestActiveOverworldChunkSoundtrack(string sceneName);

    /// <summary>
    /// Stop the currently playing soundtrack.
    /// </summary>
    void Stop(float fadeSeconds = 0f);

    /// <summary>
    /// Get the ID of the active or pending soundtrack cue.
    /// </summary>
    string GetCurrentCueId();

    /// <summary>
    /// Get the DSP time when the currently targeted cue started or is scheduled to start.
    /// </summary>
    double GetSongDspStartTime();

    /// <summary>
    /// Acquire a ref-counted, scene-music duck. The deepest active target wins;
    /// each caller releases only its own token.
    /// </summary>
    MusicDuckToken AcquireDuckToken(
        string reason,
        float targetMultiplier = 0.05f,
        float attackSeconds = 0.08f,
        float holdSeconds = 0f,
        float releaseSeconds = 0.5f);

    /// <summary>Acquire a prioritized, ref-counted soundtrack environment.</summary>
    MusicEnvironmentToken AcquireEnvironment(
        MusicEnvironmentProfileSO profile,
        string reason = "");
}
