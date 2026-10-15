using System;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Reads a profile bag quantity from the last published or cached snapshot.
    /// An unread bag is unknown, not empty, so interaction rules can fail closed.
    /// </summary>
    public static class ProfileBagQuantity
    {
        private static bool useTestSnapshot;
        private static InventorySnapshot testSnapshot;

        /// <summary>
        /// Edit-mode tests inject a snapshot without touching the live session cache.
        /// Pass enabled false to return to the runtime readers.
        /// </summary>
        public static void SetTestSnapshot(InventorySnapshot snapshot, bool enabled)
        {
            useTestSnapshot = enabled;
            testSnapshot = snapshot;
        }

        public static bool TryGetQuantity(string itemId, out int quantity)
        {
            quantity = 0;
            string id = (itemId ?? "").Trim();
            if (id.Length == 0)
                return false;

            if (useTestSnapshot)
            {
                if (testSnapshot == null)
                    return false;

                quantity = Sum(testSnapshot, id);
                return true;
            }

            if (TryReadPublishedInventory(id, out quantity))
                return true;

            CoreBagItemInventoryService bag = UnityEngine.Object.FindFirstObjectByType<CoreBagItemInventoryService>(
                FindObjectsInactive.Include);
            return bag != null && bag.TryGetCachedQuantity(id, out quantity);
        }

        private static bool TryReadPublishedInventory(string itemId, out int quantity)
        {
            quantity = 0;
            GameSessionContext session = GameSessionContext.Instance;
            if (session == null)
                return false;

            PlayerProfileReadStore store = UnityEngine.Object.FindFirstObjectByType<PlayerProfileReadStore>(
                FindObjectsInactive.Include);
            if (store == null ||
                !store.TryGetInventory(session.PlayerProfileId, session.WalletAddress, out InventorySnapshot snapshot) ||
                snapshot == null)
            {
                return false;
            }

            quantity = Sum(snapshot, itemId);
            return true;
        }

        private static int Sum(InventorySnapshot snapshot, string itemId)
        {
            if (snapshot?.stacks == null)
                return 0;

            int total = 0;
            for (int i = 0; i < snapshot.stacks.Count; i++)
            {
                MenuInventoryItemStack stack = snapshot.stacks[i];
                if (stack == null || !string.Equals(stack.itemId, itemId, StringComparison.Ordinal))
                    continue;

                total += Mathf.Max(0, stack.quantity);
            }

            return total;
        }
    }
}
