using System;
using UnityEngine;

namespace SignalJam.VisualFx
{
    [Serializable]
    public struct VisualVariantCatalogEntry
    {
        [Tooltip("Stable id used by gameplay/data (e.g. cycle_3, binding_holo).")]
        public string tierId;

        [Tooltip("Optional sort index for tier ladders (0 = lowest).")]
        public int tierIndex;

        public VisualTierProfileSO profile;
    }

    /// <summary>
    /// Catalog of visual tier profiles keyed by id or ladder index.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualVariantCatalog",
        menuName = "Chainbound/VFX/Visual Variant Catalog")]
    public sealed class VisualVariantCatalogSO : ScriptableObject
    {
        [SerializeField] private VisualVariantCatalogEntry[] entries = Array.Empty<VisualVariantCatalogEntry>();

        public VisualVariantCatalogEntry[] Entries => entries;

        public bool TryResolveById(string tierId, out VisualTierProfileSO profile)
        {
            profile = null;
            if (string.IsNullOrWhiteSpace(tierId) || entries == null)
                return false;

            for (int i = 0; i < entries.Length; i++)
            {
                VisualVariantCatalogEntry entry = entries[i];
                if (entry.profile == null || string.IsNullOrWhiteSpace(entry.tierId))
                    continue;

                if (string.Equals(entry.tierId, tierId, StringComparison.OrdinalIgnoreCase))
                {
                    profile = entry.profile;
                    return true;
                }
            }

            return false;
        }

        public VisualTierProfileSO ResolveByIndex(int tierIndex)
        {
            if (entries == null || entries.Length == 0)
                return null;

            VisualTierProfileSO exact = null;
            VisualTierProfileSO bestLower = null;
            int bestLowerIndex = int.MinValue;

            for (int i = 0; i < entries.Length; i++)
            {
                VisualVariantCatalogEntry entry = entries[i];
                if (entry.profile == null)
                    continue;

                if (entry.tierIndex == tierIndex)
                    exact = entry.profile;
                else if (entry.tierIndex <= tierIndex && entry.tierIndex > bestLowerIndex)
                {
                    bestLowerIndex = entry.tierIndex;
                    bestLower = entry.profile;
                }
            }

            if (exact != null)
                return exact;

            if (bestLower != null)
                return bestLower;

            return entries[Mathf.Clamp(tierIndex, 0, entries.Length - 1)].profile;
        }
    }
}
