/// <summary>
/// How a child track shares state with the main sequence context.
/// </summary>
public enum TrackContextMode
{
    /// <summary>Same context instance — use sparingly.</summary>
    Shared,

    /// <summary>Own locals; reads globals from parent chain (default).</summary>
    Child,

    /// <summary>MVP: same as Child (dedicated fork can be added later).</summary>
    Snapshot
}
