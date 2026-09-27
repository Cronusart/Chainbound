namespace Chainbound.Combat
{
    /// <summary>
    /// Generates an opponent party for a battle encounter.
    /// Implementations may use zone tables, VRF seeds, scripted boss fights,
    /// or inspector-configured fallbacks.
    /// Pure interface â€” no Unity dependencies.
    /// </summary>
    public interface IEncounterProvider
    {
        /// <summary>
        /// Generate an encounter snapshot for the given seed.
        /// The seed enables deterministic encounters when combined with VRF.
        /// </summary>
        EncounterSnapshot GenerateEncounter(int seed);
    }
}
