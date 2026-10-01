using System;

namespace Chainbound.Combat
{
    /// <summary>
    /// Scenery ids for the shared combat scene. Values match overworld atmosphere
    /// catalog scene ids (loomville, link1, jungle) so a stage can reuse that profile.
    /// </summary>
    public static class CombatStageIds
    {
        public const string Loomville = "loomville";
        public const string Link1 = "link1";
        public const string Jungle = "jungle";
        public const string Default = Loomville;

        private static readonly string[] KnownIds = { Loomville, Link1, Jungle };

        public static string Normalize(string stageId)
        {
            if (string.IsNullOrWhiteSpace(stageId))
                return Default;

            string trimmed = stageId.Trim();
            for (int i = 0; i < KnownIds.Length; i++)
            {
                if (string.Equals(KnownIds[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    return KnownIds[i];
            }

            return trimmed;
        }

        public static bool TryMatchKnownName(string objectName, out string stageId)
        {
            stageId = null;
            if (string.IsNullOrWhiteSpace(objectName))
                return false;

            string trimmed = objectName.Trim();
            for (int i = 0; i < KnownIds.Length; i++)
            {
                if (!string.Equals(KnownIds[i], trimmed, StringComparison.OrdinalIgnoreCase))
                    continue;

                stageId = KnownIds[i];
                return true;
            }

            return false;
        }
    }
}
