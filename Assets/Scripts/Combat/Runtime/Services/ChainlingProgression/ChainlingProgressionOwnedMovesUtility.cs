using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public static class ChainlingProgressionOwnedMovesUtility
    {
        public static HashSet<string> CollectFromSnapshot(ChainlingSnapshot snapshot)
        {
            var owned = new HashSet<string>(StringComparer.Ordinal);
            if (snapshot?.moveIds == null)
                return owned;

            for (int i = 0; i < snapshot.moveIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(snapshot.moveIds[i]))
                    owned.Add(snapshot.moveIds[i].Trim());
            }

            return owned;
        }

        public static HashSet<string> CollectFromSnapshotAndInventory(
            ChainlingSnapshot snapshot,
            IReadOnlyList<MenuMoveItem> moveInventory)
        {
            HashSet<string> owned = CollectFromSnapshot(snapshot);
            if (moveInventory == null)
                return owned;

            for (int i = 0; i < moveInventory.Count; i++)
            {
                MenuMoveItem item = moveInventory[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.moveId))
                    owned.Add(item.moveId.Trim());
            }

            return owned;
        }

        public static Dictionary<string, int> CountFromSnapshotAndInventory(
            ChainlingSnapshot snapshot,
            IReadOnlyList<MenuMoveItem> moveInventory)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);

            if (moveInventory != null)
            {
                for (int i = 0; i < moveInventory.Count; i++)
                {
                    MenuMoveItem item = moveInventory[i];
                    if (item == null || string.IsNullOrWhiteSpace(item.moveId))
                        continue;

                    string moveId = item.moveId.Trim();
                    counts.TryGetValue(moveId, out int count);
                    counts[moveId] = count + 1;
                }
            }

            // Snapshot move ids are equipped definitions, not additional inventory copies.
            // Only seed a count when inventory has not already represented that move.
            if (snapshot?.moveIds != null)
            {
                for (int i = 0; i < snapshot.moveIds.Count; i++)
                {
                    string moveId = snapshot.moveIds[i] != null ? snapshot.moveIds[i].Trim() : "";
                    if (!string.IsNullOrWhiteSpace(moveId) && !counts.ContainsKey(moveId))
                        counts[moveId] = 1;
                }
            }

            return counts;
        }

        public static List<string> ToList(IEnumerable<string> moveIds)
        {
            var list = new List<string>();
            if (moveIds == null)
                return list;

            foreach (string moveId in moveIds)
            {
                if (!string.IsNullOrWhiteSpace(moveId))
                    list.Add(moveId.Trim());
            }

            return list;
        }
    }
}
