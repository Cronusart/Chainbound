namespace Chainbound.Combat
{
    /// <summary>
    /// Core stats per Chainling species.
    /// Matches COMBAT_02B Section 1.
    /// </summary>
    public enum CoreStat
    {
        MaxHP,
        MeleeAtk,
        MeleeDef,
        RangedAtk,
        RangedDef,
        Speed,
    }

    /// <summary>
    /// Derived stats computed from core stats, stages, and modifiers.
    /// Matches COMBAT_02B Section 2.
    /// </summary>
    public enum DerivedStat
    {
        Accuracy,
        Evasion,
        CritRate,
        CritMultiplier,
    }
}
