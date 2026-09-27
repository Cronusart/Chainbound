using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Optional override/extension catalog. When null or empty, runtime uses <see cref="PlayerItemCatalogSeed"/>.
    /// Place assets under Assets/Data/UI/ (Inventory catalog).
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerItemCatalog",
        menuName = "Chainbound/Economy/Player Item Catalog",
        order = 40)]
    public sealed class PlayerItemCatalogSO : ScriptableObject
    {
        [SerializeField] private List<PlayerItemDefinition> items = new List<PlayerItemDefinition>();
        [SerializeField] private bool includeBuiltInSeed = true;

        public IReadOnlyList<PlayerItemDefinition> ResolveDefinitions()
        {
            var merged = new List<PlayerItemDefinition>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            if (includeBuiltInSeed)
            {
                IReadOnlyList<PlayerItemDefinition> seed = PlayerItemCatalogSeed.All;
                for (int i = 0; i < seed.Count; i++)
                {
                    PlayerItemDefinition row = seed[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.itemId) || !seen.Add(row.itemId))
                        continue;
                    merged.Add(row);
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                PlayerItemDefinition row = items[i];
                if (row == null || string.IsNullOrWhiteSpace(row.itemId))
                    continue;

                if (!seen.Add(row.itemId))
                {
                    for (int j = 0; j < merged.Count; j++)
                    {
                        if (merged[j] != null && merged[j].itemId == row.itemId)
                        {
                            merged[j] = row;
                            break;
                        }
                    }
                    continue;
                }

                merged.Add(row);
            }

            return merged;
        }

        public bool TryGet(string itemId, out PlayerItemDefinition definition)
        {
            definition = null;
            string id = (itemId ?? "").Trim();
            if (id.Length == 0)
                return false;

            IReadOnlyList<PlayerItemDefinition> all = ResolveDefinitions();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].itemId == id)
                {
                    definition = all[i];
                    return true;
                }
            }

            return false;
        }
    }
}
