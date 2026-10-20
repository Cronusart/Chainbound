using System;

namespace Chainbound.Combat
{
    /// <summary>Shared player-acquisition policy for authored encounter-only moves.</summary>
    public static class MoveAvailability
    {
        public const string BossOnlyTag = "BOSS_ONLY";

        public static bool IsBossOnly(MoveDefinitionSO move)
        {
            return move != null && HasTag(move.tags, BossOnlyTag);
        }

        public static bool IsBossOnly(MoveInstance move)
        {
            return move != null && HasTag(move.tags, BossOnlyTag);
        }

        public static bool IsBossOnly(string moveId, MoveDatabaseSO database)
        {
            return database != null && IsBossOnly(database.GetById(moveId));
        }

        public static bool IsPlayerObtainable(MoveDefinitionSO move) => move != null && !IsBossOnly(move);
        public static bool IsPlayerObtainable(MoveInstance move) => move != null && !IsBossOnly(move);

        public static bool IsPlayerObtainable(string moveId, MoveDatabaseSO database)
        {
            MoveDefinitionSO move = database != null ? database.GetById(moveId) : null;
            return IsPlayerObtainable(move);
        }

        private static bool HasTag(System.Collections.Generic.IReadOnlyList<string> tags, string expected)
        {
            if (tags == null)
                return false;
            for (int i = 0; i < tags.Count; i++)
            {
                if (string.Equals(tags[i], expected, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
