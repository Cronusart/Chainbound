/// <summary>
/// When a child track should stop relative to the main track.
/// </summary>
public enum TrackCancelPolicy
{
    StopOnParentEnd,
    PersistUntilComplete,
    ManualOnly
}
