using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Resolves equipped passive DUR MaxHP% modifiers into a flat MaxHP bonus.
    /// </summary>
    public static class PassiveMaxHpUtility
    {
        public static int ComputeFlatBonus(ChainlingInstance chainling, ModifierCatalogSO catalog)
        {
            return ComputeFlatBonus(
                chainling,
                catalog,
                BalanceConfigRuntime.ResolverConfig.maxPassiveMaxHpBonusPercent);
        }

        public static int ComputeFlatBonus(
            ChainlingInstance chainling,
            ModifierCatalogSO catalog,
            float maxBonusPercent)
        {
            if (chainling == null || catalog == null || chainling.baseMaxHP <= 0)
                return 0;

            float totalPercent = 0f;
            if (chainling.moves == null)
                return 0;

            for (int i = 0; i < chainling.moves.Count; i++)
            {
                MoveInstance move = chainling.moves[i];
                if (move == null)
                    continue;

                totalPercent += ResolveSectionPercent(move, catalog, move.modifier1SectionId);
                totalPercent += ResolveSectionPercent(move, catalog, move.modifier2SectionId);
            }

            float clamped = Math.Min(Math.Max(0f, totalPercent), Math.Max(0f, maxBonusPercent));
            return (int)Math.Round(chainling.baseMaxHP * clamped);
        }

        public static void ApplyToInstance(ChainlingInstance chainling, ModifierCatalogSO catalog)
        {
            if (chainling == null)
                return;

            bool wasFull = chainling.currentHP >= chainling.EffectiveMaxHP;
            chainling.passiveMaxHpBonus = ComputeFlatBonus(chainling, catalog);
            if (wasFull)
            {
                chainling.currentHP = chainling.EffectiveMaxHP;
                chainling.displayHP = chainling.currentHP;
            }
        }

        private static float ResolveSectionPercent(MoveInstance move, ModifierCatalogSO catalog, string sectionId)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(sectionId))
                return 0f;
            if (!sectionId.StartsWith("8.1-", StringComparison.OrdinalIgnoreCase))
                return 0f;

            ModifierDefinitionSO def = catalog.GetBySectionId(sectionId);
            if (def == null)
                return 0f;
            if (def.scope != ModifierScope.Passive && def.slotType != ModifierSlotType.Durability)
                return 0f;

            ModifierInstance runtime = FindRuntimeModifier(move, sectionId);
            float mid = runtime != null && runtime.effectValueLocked
                ? runtime.effectValue
                : def.effectMin;
            if ((runtime == null || !runtime.effectValueLocked) && def.effectMax > def.effectMin)
                mid = (def.effectMin + def.effectMax) * 0.5f;

            return mid > 1f ? mid / 100f : mid;
        }

        private static ModifierInstance FindRuntimeModifier(MoveInstance move, string sectionId)
        {
            if (move == null || move.modifiers == null)
                return null;
            for (int i = 0; i < move.modifiers.Count; i++)
            {
                ModifierInstance candidate = move.modifiers[i];
                if (candidate != null
                    && string.Equals(candidate.definitionSectionId, sectionId, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
            return null;
        }
    }
}
