using Chainbound.Combat;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Move stat labels for party tab rows (mirrors combat move-select formatting for definitions).
    /// </summary>
    public static class PartyTabMoveDisplayUtility
    {
        public static string FormatStatLabel(MoveDefinitionSO definition)
        {
            if (definition == null)
                return "—";

            switch (definition.category)
            {
                case MoveCategory.DamagingMelee:
                case MoveCategory.DamagingRanged:
                    return definition.power > 0 ? definition.power.ToString() : "—";

                case MoveCategory.Support:
                    if (definition.healPercentMaxHP > 0)
                        return "Heal";
                    if (HasPositiveStageChanges(definition))
                        return "Buff";
                    return "Sup";

                case MoveCategory.StatusUtility:
                    return "Util";

                case MoveCategory.Passive:
                    return "Pas";

                default:
                    return "—";
            }
        }

        private static bool HasPositiveStageChanges(MoveDefinitionSO definition)
        {
            if (definition.stageChanges == null || definition.stageChanges.Count == 0)
                return false;

            for (int i = 0; i < definition.stageChanges.Count; i++)
            {
                if (definition.stageChanges[i].stages > 0)
                    return true;
            }

            return false;
        }
    }
}
