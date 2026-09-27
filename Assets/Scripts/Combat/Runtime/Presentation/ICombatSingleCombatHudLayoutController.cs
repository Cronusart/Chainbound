namespace Chainbound.Combat
{
    /// <summary>
    /// Presentation contract for placing a settled single-combat survivor in Slot B
    /// while preserving the logical slot mapping.
    /// </summary>
    public interface ICombatSingleCombatHudLayoutController
    {
        void SetSingleCombatLayoutActive(bool active);
    }
}
