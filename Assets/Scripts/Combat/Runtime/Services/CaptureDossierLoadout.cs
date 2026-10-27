using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// The capture dossier shows the one move the chainling owns after capture.
    /// The wild fight kit stays on the encounter and is not copied onto that page.
    /// </summary>
    public static class CaptureDossierLoadout
    {
        public static void ApplyOwnedMove(
            ChainlingSnapshot dossier,
            ChainlingSnapshot encounterSource,
            MoveDatabaseSO moveDatabase = null)
        {
            if (dossier == null)
                return;

            string known = encounterSource != null && !string.IsNullOrWhiteSpace(encounterSource.knownMoveId)
                ? encounterSource.knownMoveId.Trim()
                : string.Empty;
            if (MoveAvailability.IsBossOnly(known, moveDatabase))
                known = string.Empty;
            dossier.knownMoveId = known;
            dossier.moveIds = new List<string>();
            dossier.moveAcquisitions = new List<string>();
            dossier.modifier1SectionIds = new List<string>();
            dossier.modifier2SectionIds = new List<string>();
            if (known.Length == 0)
                return;

            dossier.moveIds.Add(known);
            int index = FindMoveIndex(encounterSource.moveIds, known);
            dossier.moveAcquisitions.Add(index >= 0 ? SectionAt(encounterSource.moveAcquisitions, index) : string.Empty);
            if (index < 0)
                return;

            dossier.modifier1SectionIds.Add(SectionAt(encounterSource.modifier1SectionIds, index));
            dossier.modifier2SectionIds.Add(SectionAt(encounterSource.modifier2SectionIds, index));
        }

        private static int FindMoveIndex(List<string> moveIds, string known)
        {
            if (moveIds == null)
                return -1;

            for (int i = 0; i < moveIds.Count; i++)
            {
                string moveId = moveIds[i];
                if (string.Equals(moveId != null ? moveId.Trim() : null, known, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static string SectionAt(List<string> sectionIds, int index)
        {
            if (sectionIds == null || index < 0 || index >= sectionIds.Count || string.IsNullOrWhiteSpace(sectionIds[index]))
                return string.Empty;
            return sectionIds[index].Trim();
        }
    }
}
