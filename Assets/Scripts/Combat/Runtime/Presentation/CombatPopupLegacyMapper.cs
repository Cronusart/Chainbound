using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Maps legacy popup spawn flags to explicit categories.
    /// </summary>
    public static class CombatPopupLegacyMapper
    {
        public static CombatPopupCategory ResolveLegacyCategory(
            int amount,
            bool isHeal,
            bool isCrit,
            string impactText)
        {
            if (!string.IsNullOrEmpty(impactText))
                return ResolveImpactCategory(impactText);

            if (isHeal)
                return CombatPopupCategory.Heal;

            if (isCrit)
                return CombatPopupCategory.Crit;

            return CombatPopupCategory.Damage;
        }

        public static CombatPopupCategory ResolveImpactCategory(string impactText)
        {
            if (string.IsNullOrWhiteSpace(impactText))
                return CombatPopupCategory.GenericImpact;

            if (impactText.Equals("CRIT", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Crit;
            if (impactText.Equals("MISS", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Miss;
            if (impactText.Equals("DODGE", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Dodge;
            if (impactText.Equals("BLOCKED", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Blocked;
            if (impactText.Equals("NO AP", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.NoAp;
            if (impactText.Equals("KO", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Ko;
            if (impactText.Equals("WALL", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.WallRaised;
            if (impactText.Equals("BROKEN", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.WallBroken;
            if (impactText.Equals("PASSIVE", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Passive;
            if (impactText.Equals("COUNTER", StringComparison.OrdinalIgnoreCase))
                return CombatPopupCategory.Counter;
            if (impactText.IndexOf("AP", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (impactText.IndexOf("BLOCKED", StringComparison.OrdinalIgnoreCase) >= 0)
                    return CombatPopupCategory.ApBlocked;
                if (impactText.StartsWith("-", StringComparison.Ordinal))
                    return CombatPopupCategory.ApDrain;
                if (impactText.StartsWith("+", StringComparison.Ordinal))
                    return CombatPopupCategory.ApGain;
            }

            return CombatPopupCategory.GenericImpact;
        }
    }
}
