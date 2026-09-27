/// <summary>
/// High-level quest lifecycle for narrative gating (distinct from per-objective progress).
/// </summary>
public enum QuestLifecycleState
{
    /// <summary>Quest id has never been registered with QuestService.</summary>
    Unknown,

    /// <summary>Registered but not yet offered to the player.</summary>
    Locked,

    /// <summary>Discoverable / offerable but not yet accepted (not in active play).</summary>
    Available,

    /// <summary>Accepted and objectives may progress.</summary>
    Active,

    /// <summary>Finished successfully.</summary>
    Completed,

    /// <summary>Finished unsuccessfully.</summary>
    Failed
}
