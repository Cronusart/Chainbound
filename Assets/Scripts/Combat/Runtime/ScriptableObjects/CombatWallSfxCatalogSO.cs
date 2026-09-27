using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum CombatWallSfxCue
    {
        Raise,
        Absorb,
        Reflect,
        Break,
        Block,
    }

    [Serializable]
    public struct CombatWallSfxEntry
    {
        public string wallSfxId;
        public SoundEventSO raiseSound;
        public SoundEventSO absorbSound;
        public SoundEventSO reflectSound;
        public SoundEventSO breakSound;
        public SoundEventSO blockSound;

        public SoundEventSO Resolve(CombatWallSfxCue cue)
        {
            switch (cue)
            {
                case CombatWallSfxCue.Raise:
                    return raiseSound;
                case CombatWallSfxCue.Absorb:
                    return absorbSound;
                case CombatWallSfxCue.Reflect:
                    return reflectSound;
                case CombatWallSfxCue.Break:
                    return breakSound;
                case CombatWallSfxCue.Block:
                    return blockSound;
                default:
                    return null;
            }
        }
    }

    [CreateAssetMenu(
        fileName = "CombatWallSfxCatalog",
        menuName = "Chainbound/Combat/Audio/Wall SFX Catalog")]
    public sealed class CombatWallSfxCatalogSO : ScriptableObject
    {
        [SerializeField] private List<CombatWallSfxEntry> entries = new List<CombatWallSfxEntry>();

        private Dictionary<string, CombatWallSfxEntry> _lookup;

        public IReadOnlyList<CombatWallSfxEntry> Entries => entries;

        public bool TryResolve(string wallSfxId, out CombatWallSfxEntry entry)
        {
            BuildLookupIfNeeded();

            string normalized = NormalizeId(wallSfxId);
            if (!string.IsNullOrEmpty(normalized) &&
                _lookup.TryGetValue(normalized, out entry))
            {
                return true;
            }

            return _lookup.TryGetValue("default", out entry);
        }

        public bool TryResolveExact(string wallSfxId, out CombatWallSfxEntry entry)
        {
            BuildLookupIfNeeded();
            string normalized = NormalizeId(wallSfxId);
            if (string.IsNullOrEmpty(normalized))
            {
                entry = default;
                return false;
            }

            return _lookup.TryGetValue(normalized, out entry);
        }

        public SoundEventSO ResolveSound(string wallSfxId, CombatWallSfxCue cue)
        {
            // Prefer the material/move entry, but fall through to default when that cue slot
            // is unauthored. Empty wall_barrier (etc.) rows previously swallowed default SFX.
            if (TryResolveExact(wallSfxId, out CombatWallSfxEntry specific))
            {
                SoundEventSO authored = specific.Resolve(cue);
                if (authored != null)
                    return authored;
            }

            if (TryResolveExact("default", out CombatWallSfxEntry fallback))
                return fallback.Resolve(cue);

            return null;
        }

        public void BuildLookup()
        {
            _lookup = new Dictionary<string, CombatWallSfxEntry>(StringComparer.OrdinalIgnoreCase);
            if (entries == null)
                return;

            for (int i = 0; i < entries.Count; i++)
            {
                CombatWallSfxEntry entry = entries[i];
                string normalized = NormalizeId(entry.wallSfxId);
                if (string.IsNullOrEmpty(normalized) || _lookup.ContainsKey(normalized))
                    continue;

                entry.wallSfxId = normalized;
                _lookup.Add(normalized, entry);
            }
        }

        private void BuildLookupIfNeeded()
        {
            if (_lookup == null)
                BuildLookup();
        }

        private void OnEnable()
        {
            BuildLookup();
        }

        private void OnValidate()
        {
            BuildLookup();

            if (_lookup == null || !_lookup.ContainsKey("default"))
                Debug.LogWarning("[CombatWallSfxCatalog] A default wallSfxId entry is required.", this);

            if (entries == null)
                return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Count; i++)
            {
                string normalized = NormalizeId(entries[i].wallSfxId);
                if (string.IsNullOrEmpty(normalized))
                    continue;

                if (!seen.Add(normalized))
                    Debug.LogWarning($"[CombatWallSfxCatalog] Duplicate wallSfxId '{normalized}' at entry {i}.", this);
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
