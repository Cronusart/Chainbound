namespace Chainbound.Combat
{
    /// <summary>
    /// Shared interface that decouples the battlefield presenter from any
    /// specific battle driver (test harness, real controller, replay player, etc.).
    ///
    /// Both <see cref="Combat2TestHarness"/> and <see cref="CombatBattleController"/>
    /// implement this so the same presenter works in every context.
    /// </summary>
    public interface ICombatStateSource
    {
        /// <summary>Current combat state. Null before a battle starts.</summary>
        CombatState State { get; }

        /// <summary>Structured event log for the current battle. Null before a battle starts.</summary>
        CombatEventLog EventLog { get; }

        /// <summary>True once a battle has been initialized.</summary>
        bool BattleStarted { get; }
    }
}
