using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Reusable ScriptableObject mapping each <see cref="ChainlingType"/> to an icon sprite.
    /// Create multiple instances for different visual contexts:
    ///   - "Battle HUD" palette (compact icons next to chainling name)
    ///   - "Universal" palette (used next to combat moves, tooltips, etc.)
    ///
    /// Right-click in Project > Create > Combat > Type Icon Palette.
    /// </summary>
    [CreateAssetMenu(menuName = "Chainbound/Combat/Types/Type Icon Palette", order = 40)]
    public class TypeIconPaletteSO : ScriptableObject
    {
        [Header("Typeless")]
        [Tooltip("Icon used for ChainlingType.None, the canonical typeless value.")]
        [SerializeField] private Sprite typelessIcon;

        [Header("Type -> Icon Mappings")]
        [Tooltip("One entry per concrete type. ChainlingType.None uses the Typeless Icon above.")]
        [SerializeField] private List<TypeIconEntry> entries = new List<TypeIconEntry>();

        [Header("Fallback")]
        [Tooltip("Icon used when a concrete type has no entry in the list.")]
        [SerializeField] private Sprite fallbackIcon;

        // Runtime lookup (built lazily on first access)
        private Dictionary<ChainlingType, Sprite> _lookup;

        /// <summary>Fallback icon for missing concrete type mappings.</summary>
        public Sprite FallbackIcon => fallbackIcon;

        /// <summary>Icon for ChainlingType.None / typeless moves and Chainlings.</summary>
        public Sprite TypelessIcon
        {
            get
            {
                EnsureLookup();
                return _lookup.TryGetValue(ChainlingType.None, out var icon) && icon != null
                    ? icon
                    : fallbackIcon;
            }
        }

        /// <summary>
        /// Get the icon for the given type. Returns the fallback if no entry exists.
        /// Returns null only if both the entry and fallback are unassigned.
        /// </summary>
        public Sprite GetIcon(ChainlingType type)
        {
            EnsureLookup();
            if (_lookup.TryGetValue(type, out var icon) && icon != null)
                return icon;
            return fallbackIcon;
        }

        public void WarmPresentation()
        {
            EnsureLookup();
            TouchSprite(fallbackIcon);
            TouchSprite(typelessIcon);
            if (_lookup == null)
                return;

            foreach (KeyValuePair<ChainlingType, Sprite> pair in _lookup)
                TouchSprite(pair.Value);
        }

        private static void TouchSprite(Sprite sprite)
        {
            if (sprite == null)
                return;
            Texture2D texture = sprite.texture;
            if (texture != null)
                _ = texture.width;
        }

        /// <summary>
        /// Try to get the icon for the given type without falling back.
        /// Returns true if an explicit entry exists, including the typeless icon field.
        /// </summary>
        public bool TryGetIcon(ChainlingType type, out Sprite icon)
        {
            EnsureLookup();
            return _lookup.TryGetValue(type, out icon) && icon != null;
        }

        /// <summary>
        /// Build the full lookup dictionary. Useful for callers that need
        /// to iterate or cache the mapping themselves.
        /// </summary>
        public Dictionary<ChainlingType, Sprite> BuildLookup()
        {
            var dict = new Dictionary<ChainlingType, Sprite>();

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.icon != null)
                        dict[e.type] = e.icon;
                }
            }

            if (typelessIcon != null)
                dict[ChainlingType.None] = typelessIcon;

            return dict;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = BuildLookup();
        }

        // Reset lookup when asset is modified in editor
        private void OnValidate()
        {
            _lookup = null;
        }

        [Serializable]
        public struct TypeIconEntry
        {
            public ChainlingType type;
            public Sprite icon;
        }
    }
}
