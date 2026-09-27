using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public class SpeciesEntangledVisualProfileEntry
    {
        public ChainlingType type = ChainlingType.Node;
        public CombatVisualProfileSO profile;
    }

    [Serializable]
    public class SpeciesVisualVariantEntry
    {
        [Tooltip("Cosmetic skin id. Use 'base' for the default species look.")]
        public string variantId = ChainlingVisualIdentity.BaseVariantId;

        public string displayName;

        [Tooltip("Catalog rarity tier for this skin (label only). Spawn odds live on wild variant tables as weight.")]
        public RarityTier rarity = RarityTier.Common;

        [Tooltip("Non-entangled body profile for this skin.")]
        public CombatVisualProfileSO baseProfile;

        [Tooltip("Fully baked entangled body profiles keyed by type.")]
        public List<SpeciesEntangledVisualProfileEntry> entangledProfiles =
            new List<SpeciesEntangledVisualProfileEntry>();

        public CombatVisualProfileSO GetEntangledProfile(ChainlingType type)
        {
            if (entangledProfiles == null || type == ChainlingType.None)
                return null;

            for (int i = 0; i < entangledProfiles.Count; i++)
            {
                SpeciesEntangledVisualProfileEntry entry = entangledProfiles[i];
                if (entry != null && entry.type == type && entry.profile != null)
                    return entry.profile;
            }

            return null;
        }
    }

    /// <summary>
    /// Per-species catalog of cosmetic variants and baked entangled type profiles.
    /// Hierarchy: Species -> Variants (including required base) -> Entangled type art.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SpeciesVisualSet_",
        menuName = "Chainbound/Combat/Presentation/Visual Profiles/Species Visual Set")]
    public class ChainlingSpeciesVisualSetSO : ScriptableObject
    {
        public string speciesId;

        public List<SpeciesVisualVariantEntry> variants = new List<SpeciesVisualVariantEntry>();

        public SpeciesVisualVariantEntry FindVariant(string variantId)
        {
            string normalized = ChainlingVisualIdentity.NormalizeId(
                variantId,
                ChainlingVisualIdentity.BaseVariantId);
            if (variants == null)
                return null;

            for (int i = 0; i < variants.Count; i++)
            {
                SpeciesVisualVariantEntry entry = variants[i];
                if (entry == null)
                    continue;

                string id = ChainlingVisualIdentity.NormalizeId(
                    entry.variantId,
                    ChainlingVisualIdentity.BaseVariantId);
                if (string.Equals(id, normalized, StringComparison.Ordinal))
                    return entry;
            }

            return null;
        }

        public CombatVisualProfileSO ResolveProfile(string variantId, ChainlingType entangledType)
        {
            SpeciesVisualVariantEntry variant = FindVariant(variantId);
            if (variant == null &&
                !string.Equals(
                    ChainlingVisualIdentity.NormalizeId(variantId, ChainlingVisualIdentity.BaseVariantId),
                    ChainlingVisualIdentity.BaseVariantId,
                    StringComparison.Ordinal))
            {
                variant = FindVariant(ChainlingVisualIdentity.BaseVariantId);
            }

            if (variant == null)
                return null;

            if (entangledType != ChainlingType.None)
            {
                CombatVisualProfileSO entangled = variant.GetEntangledProfile(entangledType);
                if (entangled != null)
                    return entangled;
            }

            return variant.baseProfile;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (variants == null)
                variants = new List<SpeciesVisualVariantEntry>();

            bool hasBase = false;
            for (int i = 0; i < variants.Count; i++)
            {
                SpeciesVisualVariantEntry entry = variants[i];
                if (entry == null)
                    continue;

                entry.variantId = ChainlingVisualIdentity.NormalizeId(
                    entry.variantId,
                    ChainlingVisualIdentity.BaseVariantId);
                if (string.Equals(entry.variantId, ChainlingVisualIdentity.BaseVariantId, StringComparison.Ordinal))
                    hasBase = true;
            }

            if (!hasBase)
            {
                Debug.LogWarning(
                    $"[ChainlingSpeciesVisualSet] '{name}' (speciesId='{speciesId}') is missing a required 'base' variant.",
                    this);
            }
        }
#endif
    }
}
