/// <summary>
/// How long a profile world-presence flag should survive continue-game rewind.
/// Durable flags are never restored from a checkpoint rail snapshot.
/// </summary>
public enum WorldPresenceLifetime
{
    Unspecified = 0,
    Durable = 1,
    Rail = 2
}
