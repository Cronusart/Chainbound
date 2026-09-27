namespace Chainbound.Combat
{
    /// <summary>
    /// Status class determines how a status persists and resolves.
    /// Matches COMBAT_02A Section 1.
    /// </summary>
    public enum StatusClass
    {
        Duration,   // Persists N turns; reapply refreshes up to max
        Instant,    // Resolves immediately on application
        Counter,    // Increments counter; triggers at threshold
        Consumed,   // Persists until consumed OR expires
    }

    /// <summary>
    /// Origin type determines who/what may apply a status.
    /// Matches COMBAT_02A Section 5.
    /// </summary>
    public enum StatusOrigin
    {
        ChemistryOnly,  // Only produced by chemistry resolver on damaging hit
        General,        // Can be applied by inscriptions/passives
    }
}
