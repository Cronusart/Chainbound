using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [CreateAssetMenu(
        fileName = "ChainlingPartyTypeChromePalette",
        menuName = "Chainbound/UI/Party Tab Type Chrome Palette")]
    public sealed class ChainlingPartyTypeChromePaletteSO : ScriptableObject
    {
        [Serializable]
        public struct TypeChromeEntry
        {
            public ChainlingType type;
            public Color backgroundColor;
            public Color pedestalColor;
            [Tooltip("Optional Attributes radar stroke override. Clear alpha (a=0) to use lighten-from-background.")]
            public Color radarStrokeColor;
            [Tooltip("Optional Attributes radar fill override. Clear alpha (a=0) to derive from stroke.")]
            public Color radarFillColor;
        }

        [Header("Panel")]
        [SerializeField] private Sprite backgroundSprite;
        [SerializeField] private Color emptyBackgroundColor = new Color(0.07f, 0.095f, 0.14f, 1f);
        [SerializeField] private Color emptyPedestalColor = new Color(0.50f, 0.44f, 0.54f, 1f);

        [Header("Type Colors")]
        [SerializeField] private List<TypeChromeEntry> entries = new List<TypeChromeEntry>();

        [Header("Entangled Overlay")]
        [SerializeField] private bool useEntangledBackgroundTint;
        [SerializeField] private Color entangledBackgroundTint = new Color(1f, 0.58f, 0.82f, 1f);
        [SerializeField, Range(0f, 1f)] private float entangledBackgroundBlend = 0.22f;

        public Sprite BackgroundSprite => backgroundSprite;
        public Color EmptyBackgroundColor => emptyBackgroundColor;
        public Color EmptyPedestalColor => emptyPedestalColor;

        public Color ResolveBackgroundColor(ChainlingType type)
        {
            return TryGetEntry(type, out TypeChromeEntry entry)
                ? entry.backgroundColor
                : ChainlingTypeDisplaySO.CreateFallback(type).badgeColor;
        }

        public Color ResolvePedestalColor(ChainlingType type)
        {
            return TryGetEntry(type, out TypeChromeEntry entry)
                ? entry.pedestalColor
                : ChainlingTypeDisplaySO.CreateFallback(type).badgeColor;
        }

        public Color ResolveEntangledBackgroundColor(Color baseColor)
        {
            if (!useEntangledBackgroundTint)
                return baseColor;

            Color blended = Color.Lerp(baseColor, entangledBackgroundTint, entangledBackgroundBlend);
            blended.a = baseColor.a;
            return blended;
        }

        /// <summary>
        /// Radar stroke: optional per-type override, else lighten type background toward white.
        /// </summary>
        public Color ResolveRadarStrokeColor(ChainlingType type, float lighten01, bool entangled)
        {
            Color background = ResolveBackgroundColor(type);
            if (entangled)
                background = ResolveEntangledBackgroundColor(background);

            if (TryGetEntry(type, out TypeChromeEntry entry) && entry.radarStrokeColor.a > 0.001f)
            {
                Color stroke = entry.radarStrokeColor;
                if (entangled)
                    stroke = ResolveEntangledBackgroundColor(stroke);
                return stroke;
            }

            Color derived = Color.Lerp(background, Color.white, Mathf.Clamp01(lighten01));
            derived.a = 1f;
            return derived;
        }

        public bool TryResolveRadarFillColor(ChainlingType type, out Color fillColor)
        {
            if (TryGetEntry(type, out TypeChromeEntry entry) && entry.radarFillColor.a > 0.001f)
            {
                fillColor = entry.radarFillColor;
                return true;
            }

            fillColor = default;
            return false;
        }

        public bool TryGetEntry(ChainlingType type, out TypeChromeEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i].type == type)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }
    }
}
