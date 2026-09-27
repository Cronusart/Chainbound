using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    [CreateAssetMenu(fileName = "CombatVisualDatabase", menuName = "Chainbound/Combat/Presentation/Visual Profiles/Combat Visual Database")]
    public class CombatVisualDatabaseSO : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Species ID to match, such as 'validape' or 'test_validape'.")]
            public string speciesId;

            [Tooltip("Body-skin id (e.g. base, chrome). Empty means 'base'. Not a rarity value.")]
            public string visualVariantId = ChainlingVisualIdentity.BaseVariantId;

            [Tooltip("Entangled type for baked variant×type art. None = non-entangled skin profile.")]
            public ChainlingType entangledType = ChainlingType.None;

            [Tooltip("Visual profile asset for this species, variant, and optional entangled type.")]
            public CombatVisualProfileSO profile;
        }

        [Header("Entries (legacy flat lookup)")]
        [Tooltip("Legacy flat map of speciesId + visualVariantId + entangledType -> profile. Prefer Species Visual Sets for new skins. Resolve uses sets first.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Header("Species Visual Sets")]
        [Tooltip("Canonical catalog of which body skins exist per species. Resolve prefers these before flat entries.")]
        [SerializeField] private List<ChainlingSpeciesVisualSetSO> speciesVisualSets = new List<ChainlingSpeciesVisualSetSO>();

        [Header("Fallback")]
        [Tooltip("Profile returned when no entry matches. Prevents blank visuals for test or unknown chainlings.")]
        [SerializeField] private CombatVisualProfileSO fallbackProfile;

        private Dictionary<string, CombatVisualProfileSO> _lookup;
        private Dictionary<string, ChainlingSpeciesVisualSetSO> _setLookup;

        public IReadOnlyList<Entry> Entries => entries;
        public IReadOnlyList<ChainlingSpeciesVisualSetSO> SpeciesVisualSets => speciesVisualSets;

        public ChainlingSpeciesVisualSetSO GetVisualSet(string speciesId)
        {
            if (_setLookup == null)
                BuildSetLookup();

            string normalized = NormalizeSpeciesId(speciesId);
            if (string.IsNullOrEmpty(normalized))
                return null;

            return _setLookup.TryGetValue(normalized, out ChainlingSpeciesVisualSetSO set) ? set : null;
        }

        public bool TryGetCatalogRarity(string speciesId, string visualVariantId, out RarityTier rarity)
        {
            rarity = RarityTier.Common;
            ChainlingSpeciesVisualSetSO set = GetVisualSet(speciesId);
            if (set == null)
                return false;

            SpeciesVisualVariantEntry variant = set.FindVariant(visualVariantId);
            if (variant == null)
                return false;

            rarity = variant.rarity;
            return true;
        }

        public bool ContainsBodyVariant(string speciesId, string visualVariantId)
        {
            List<string> ids = CollectBodyVariantIds(speciesId);
            string normalized = ChainlingVisualIdentity.NormalizeBodySkinVariantId(visualVariantId);
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], normalized, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Canonical body-skin ids for a species. Visual-set ids win, then legacy flat entries.
        /// Always includes <see cref="ChainlingVisualIdentity.BaseVariantId"/>.
        /// </summary>
        public List<string> CollectBodyVariantIds(string speciesId)
        {
            var ids = new List<string>();
            AddUniqueBodyId(ids, ChainlingVisualIdentity.BaseVariantId);

            string normalizedSpecies = NormalizeSpeciesId(speciesId);
            if (string.IsNullOrEmpty(normalizedSpecies))
                return ids;

            ChainlingSpeciesVisualSetSO set = GetVisualSet(normalizedSpecies);
            if (set != null && set.variants != null)
            {
                for (int i = 0; i < set.variants.Count; i++)
                {
                    SpeciesVisualVariantEntry variant = set.variants[i];
                    if (variant == null)
                        continue;
                    AddUniqueBodyId(ids, variant.variantId);
                }
            }

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry entry = entries[i];
                    if (entry == null || entry.entangledType != ChainlingType.None)
                        continue;
                    if (!string.Equals(NormalizeSpeciesId(entry.speciesId), normalizedSpecies, StringComparison.Ordinal))
                        continue;
                    AddUniqueBodyId(ids, entry.visualVariantId);
                }
            }

            return ids;
        }

        private static void AddUniqueBodyId(List<string> ids, string visualVariantId)
        {
            if (!ChainlingVisualIdentity.IsBodySkinVariantId(visualVariantId))
                return;

            string normalized = ChainlingVisualIdentity.NormalizeBodySkinVariantId(visualVariantId);
            for (int i = 0; i < ids.Count; i++)
            {
                if (string.Equals(ids[i], normalized, StringComparison.Ordinal))
                    return;
            }

            ids.Add(normalized);
        }

        public CombatVisualProfileSO GetProfile(string speciesId)
        {
            return GetProfile(speciesId, ChainlingVisualIdentity.BaseVariantId, ChainlingType.None);
        }

        public CombatVisualProfileSO GetProfile(string speciesId, string visualVariantId)
        {
            return GetProfile(speciesId, visualVariantId, ChainlingType.None);
        }

        public CombatVisualProfileSO GetProfile(
            string speciesId,
            string visualVariantId,
            ChainlingType entangledType)
        {
            if (_lookup == null)
                BuildLookup();

            string normalizedSpecies = NormalizeSpeciesId(speciesId);
            string normalizedVariant = NormalizeVariantId(visualVariantId);
            ChainlingType type = entangledType;

            if (!string.IsNullOrEmpty(normalizedSpecies))
            {
                CombatVisualProfileSO fromSet = ResolveFromSpeciesSet(normalizedSpecies, normalizedVariant, type);
                if (fromSet != null)
                    return fromSet;

                if (type != ChainlingType.None &&
                    _lookup.TryGetValue(MakeKey(normalizedSpecies, normalizedVariant, type), out var typed))
                {
                    return typed;
                }

                if (_lookup.TryGetValue(MakeKey(normalizedSpecies, normalizedVariant, ChainlingType.None), out var variantBase))
                    return variantBase;

                if (normalizedVariant != ChainlingVisualIdentity.BaseVariantId &&
                    _lookup.TryGetValue(MakeKey(normalizedSpecies, ChainlingVisualIdentity.BaseVariantId, ChainlingType.None), out variantBase))
                {
                    return variantBase;
                }
            }

            return fallbackProfile;
        }

        public List<Entry> GetEntriesForSpecies(string speciesId)
        {
            var result = new List<Entry>();
            string normalizedSpecies = NormalizeSpeciesId(speciesId);
            if (string.IsNullOrEmpty(normalizedSpecies) || entries == null)
                return result;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry == null || entry.profile == null)
                    continue;

                if (string.Equals(NormalizeSpeciesId(entry.speciesId), normalizedSpecies, StringComparison.Ordinal))
                    result.Add(entry);
            }

            return result;
        }

        public CombatVisualProfileSO GetProfileExact(string speciesId)
        {
            return GetProfileExact(speciesId, ChainlingVisualIdentity.BaseVariantId, ChainlingType.None);
        }

        public CombatVisualProfileSO GetProfileExact(string speciesId, string visualVariantId)
        {
            return GetProfileExact(speciesId, visualVariantId, ChainlingType.None);
        }

        public CombatVisualProfileSO GetProfileExact(
            string speciesId,
            string visualVariantId,
            ChainlingType entangledType)
        {
            if (_lookup == null)
                BuildLookup();

            string normalizedSpecies = NormalizeSpeciesId(speciesId);
            if (string.IsNullOrEmpty(normalizedSpecies))
                return null;

            CombatVisualProfileSO fromSet = ResolveFromSpeciesSetExact(
                normalizedSpecies,
                NormalizeVariantId(visualVariantId),
                entangledType);
            if (fromSet != null)
                return fromSet;

            return _lookup.TryGetValue(
                    MakeKey(normalizedSpecies, NormalizeVariantId(visualVariantId), entangledType),
                    out var profile)
                ? profile
                : null;
        }

        public CombatVisualProfileSO FallbackProfile => fallbackProfile;

        private CombatVisualProfileSO ResolveFromSpeciesSet(
            string speciesId,
            string visualVariantId,
            ChainlingType entangledType)
        {
            if (_setLookup == null)
                BuildSetLookup();

            if (!_setLookup.TryGetValue(speciesId, out ChainlingSpeciesVisualSetSO set) || set == null)
                return null;

            CombatVisualProfileSO profile = set.ResolveProfile(visualVariantId, entangledType);
            if (profile != null)
                return profile;

            if (entangledType != ChainlingType.None)
                return set.ResolveProfile(visualVariantId, ChainlingType.None);

            return null;
        }

        private CombatVisualProfileSO ResolveFromSpeciesSetExact(
            string speciesId,
            string visualVariantId,
            ChainlingType entangledType)
        {
            if (_setLookup == null)
                BuildSetLookup();

            if (!_setLookup.TryGetValue(speciesId, out ChainlingSpeciesVisualSetSO set) || set == null)
                return null;

            SpeciesVisualVariantEntry variant = set.FindVariant(visualVariantId);
            if (variant == null)
                return null;

            if (entangledType != ChainlingType.None)
                return variant.GetEntangledProfile(entangledType);

            return variant.baseProfile;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, CombatVisualProfileSO>(
                entries != null ? entries.Count : 0,
                StringComparer.Ordinal);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.profile == null)
                    continue;

                string speciesId = NormalizeSpeciesId(entry.speciesId);
                if (string.IsNullOrEmpty(speciesId))
                    continue;

                string variantId = NormalizeVariantId(entry.visualVariantId);
                string key = MakeKey(speciesId, variantId, entry.entangledType);
                if (!_lookup.ContainsKey(key))
                {
                    _lookup[key] = entry.profile;
                    continue;
                }

                Debug.LogWarning(
                    $"[CombatVisualDatabase] Duplicate visual entry species='{speciesId}' variant='{variantId}' type='{entry.entangledType}' - keeping first entry.",
                    this);
            }
        }

        private void BuildSetLookup()
        {
            _setLookup = new Dictionary<string, ChainlingSpeciesVisualSetSO>(StringComparer.Ordinal);
            if (speciesVisualSets == null)
                return;

            for (int i = 0; i < speciesVisualSets.Count; i++)
            {
                ChainlingSpeciesVisualSetSO set = speciesVisualSets[i];
                if (set == null || string.IsNullOrWhiteSpace(set.speciesId))
                    continue;

                string key = NormalizeSpeciesId(set.speciesId);
                if (string.IsNullOrEmpty(key) || _setLookup.ContainsKey(key))
                    continue;

                _setLookup[key] = set;
            }
        }

        private static string NormalizeSpeciesId(string speciesId)
        {
            return string.IsNullOrWhiteSpace(speciesId) ? string.Empty : speciesId.Trim();
        }

        private static string NormalizeVariantId(string visualVariantId)
        {
            return ChainlingVisualIdentity.NormalizeBodySkinVariantId(visualVariantId);
        }

        private static string MakeKey(string speciesId, string visualVariantId, ChainlingType entangledType)
        {
            string typeKey = entangledType == ChainlingType.None ? "none" : entangledType.ToString();
            return $"{speciesId}::{visualVariantId}::{typeKey}";
        }

        private void OnEnable()
        {
            _lookup = null;
            _setLookup = null;
        }

        /// <summary>Rebuilds lookups after mutating <see cref="Entries"/> or species sets.</summary>
        public void RebuildLookup()
        {
            BuildLookup();
            BuildSetLookup();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _lookup = null;
            _setLookup = null;
        }
#endif

        /// <summary>Editor/test helper to register or replace a flat visual entry.</summary>
        public void EditorAddOrReplaceEntry(Entry entry)
        {
            if (entry == null || entry.profile == null)
                return;

            if (entries == null)
                entries = new List<Entry>();

            string species = NormalizeSpeciesId(entry.speciesId);
            string variant = NormalizeVariantId(entry.visualVariantId);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry existing = entries[i];
                if (existing == null)
                    continue;

                if (string.Equals(NormalizeSpeciesId(existing.speciesId), species, StringComparison.Ordinal) &&
                    string.Equals(NormalizeVariantId(existing.visualVariantId), variant, StringComparison.Ordinal) &&
                    existing.entangledType == entry.entangledType)
                {
                    existing.profile = entry.profile;
                    _lookup = null;
                    return;
                }
            }

            entries.Add(entry);
            _lookup = null;
        }

        /// <summary>Editor/test helper to register a species visual set as the canonical catalog.</summary>
        public void EditorAddOrReplaceVisualSet(ChainlingSpeciesVisualSetSO set)
        {
            if (set == null || string.IsNullOrWhiteSpace(set.speciesId))
                return;

            if (speciesVisualSets == null)
                speciesVisualSets = new List<ChainlingSpeciesVisualSetSO>();

            string species = NormalizeSpeciesId(set.speciesId);
            for (int i = 0; i < speciesVisualSets.Count; i++)
            {
                ChainlingSpeciesVisualSetSO existing = speciesVisualSets[i];
                if (existing == null)
                    continue;
                if (string.Equals(NormalizeSpeciesId(existing.speciesId), species, StringComparison.Ordinal))
                {
                    speciesVisualSets[i] = set;
                    _setLookup = null;
                    return;
                }
            }

            speciesVisualSets.Add(set);
            _setLookup = null;
        }
    }
}
