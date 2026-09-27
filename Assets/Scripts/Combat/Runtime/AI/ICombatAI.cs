using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Interface for combat AI decision-making.
    /// Implementations produce actions for all active slots on a team given the full combat state.
    /// Called once per turn AFTER AP gain so the AI sees current AP values.
    ///
    /// Designed for per-species assignment: a Chainling species can reference an AI personality
    /// that determines how it behaves in CPU-controlled battles.
    /// </summary>
    public interface ICombatAI
    {
        /// <summary>
        /// Human-readable name for this AI personality (e.g., "Aggressive", "Defensive").
        /// Used in logs and Inspector display.
        /// </summary>
        string PersonalityName { get; }

        /// <summary>
        /// Generate actions for all active slots on a team.
        /// Returns one CombatAction per active slot (ordered by slot index).
        /// </summary>
        /// <param name="state">Full observable combat state.</param>
        /// <param name="teamIndex">Which team this AI controls (0 or 1).</param>
        /// <param name="rng">Seeded RNG for deterministic randomness. May be null for non-random AIs.</param>
        List<CombatAction> GenerateActions(CombatState state, int teamIndex, CombatRng rng);
    }
}
