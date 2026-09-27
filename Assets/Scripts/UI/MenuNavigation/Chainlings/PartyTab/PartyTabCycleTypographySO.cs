using System;
using SignalJam.VisualFx;
using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [CreateAssetMenu(
        fileName = "PartyTabCycleTypography",
        menuName = "Chainbound/UI/Party Tab Cycle Typography")]
    public sealed class PartyTabCycleTypographySO : ScriptableObject
    {
        [Serializable]
        public struct CycleTierStyle
        {
            public Color labelColor;
            public Color filledPipColor;
            public Color emptyPipColor;
            public float labelScale;
            public float pipScale;
            public TMP_FontAsset font;
            public Material fontMaterial;
        }

        [Header("Visual Tier Delegation")]
        [Tooltip("Primary source for Cycle I-V typography FX (Max Cycle reuses Cycle V). Run Chainbound/VFX/Create Cycle Progress Visual Assets to author.")]
        [SerializeField] private VisualVariantCatalogSO variantCatalog;

        [Header("Legacy Fallback (used when variantCatalog is unset)")]
        [SerializeField] private CycleTierStyle[] tierStyles =
        {
            new CycleTierStyle
            {
                labelColor = new Color(0.86f, 0.93f, 1f, 1f),
                filledPipColor = new Color(0.66f, 0.82f, 1f, 1f),
                emptyPipColor = new Color(0.33f, 0.40f, 0.50f, 0.72f),
                labelScale = 1f,
                pipScale = 1f,
            },
            new CycleTierStyle
            {
                labelColor = new Color(0.74f, 0.98f, 0.90f, 1f),
                filledPipColor = new Color(0.38f, 0.98f, 0.78f, 1f),
                emptyPipColor = new Color(0.30f, 0.42f, 0.44f, 0.72f),
                labelScale = 1.02f,
                pipScale = 1.02f,
            },
            new CycleTierStyle
            {
                labelColor = new Color(1f, 0.92f, 0.60f, 1f),
                filledPipColor = new Color(1f, 0.72f, 0.28f, 1f),
                emptyPipColor = new Color(0.46f, 0.39f, 0.27f, 0.72f),
                labelScale = 1.05f,
                pipScale = 1.05f,
            },
            new CycleTierStyle
            {
                labelColor = new Color(1f, 0.70f, 0.86f, 1f),
                filledPipColor = new Color(1f, 0.42f, 0.68f, 1f),
                emptyPipColor = new Color(0.46f, 0.30f, 0.39f, 0.72f),
                labelScale = 1.08f,
                pipScale = 1.07f,
            },
            new CycleTierStyle
            {
                labelColor = new Color(0.92f, 0.86f, 1f, 1f),
                filledPipColor = new Color(0.72f, 0.52f, 1f, 1f),
                emptyPipColor = new Color(0.38f, 0.32f, 0.48f, 0.72f),
                labelScale = 1.12f,
                pipScale = 1.10f,
            },
        };

        public VisualVariantCatalogSO VariantCatalog => variantCatalog;

        public VisualTierProfileSO ResolveProfile(int tier)
        {
            if (variantCatalog == null)
                return null;

            return variantCatalog.ResolveByIndex(Mathf.Clamp(tier, 0, 4));
        }

        public CycleTierStyle Resolve(int tier)
        {
            int clampedTier = Mathf.Clamp(tier, 0, 4);
            VisualTierProfileSO profile = ResolveProfile(clampedTier);
            if (profile != null)
                return ResolveFromProfile(profile, clampedTier);

            if (tierStyles == null || tierStyles.Length == 0)
                return CreateFallback(clampedTier);

            return tierStyles[Mathf.Clamp(clampedTier, 0, tierStyles.Length - 1)];
        }

        public static CycleTierStyle CreateFallback(int tier)
        {
            Color label = Color.Lerp(
                new Color(0.86f, 0.93f, 1f, 1f),
                new Color(1f, 0.70f, 0.34f, 1f),
                Mathf.Clamp01(tier / 4f));

            return new CycleTierStyle
            {
                labelColor = label,
                filledPipColor = label,
                emptyPipColor = new Color(0.34f, 0.39f, 0.48f, 0.72f),
                labelScale = 1f + Mathf.Clamp(tier, 0, 4) * 0.03f,
                pipScale = 1f + Mathf.Clamp(tier, 0, 4) * 0.025f,
            };
        }

        private static CycleTierStyle ResolveFromProfile(VisualTierProfileSO profile, int tier)
        {
            VisualTierPaletteSO palette = profile.ResolvePaletteForTypography();
            VisualTierTypographyPresetSO typography = profile.TypographyPreset;

            Color primary = palette != null
                ? palette.ResolveColor(VisualTierColorSlot.Primary, Color.white)
                : CreateFallback(tier).labelColor;
            Color secondary = palette != null
                ? palette.ResolveColor(VisualTierColorSlot.Secondary, primary)
                : primary;
            Color empty = palette != null
                ? palette.ResolveColor(VisualTierColorSlot.Shadow, new Color(0.34f, 0.39f, 0.48f, 0.72f))
                : new Color(0.34f, 0.39f, 0.48f, 0.72f);

            float scale = typography != null ? typography.TransformScaleMultiplier : 1f + tier * 0.03f;

            return new CycleTierStyle
            {
                labelColor = primary,
                filledPipColor = secondary,
                emptyPipColor = empty,
                labelScale = scale,
                pipScale = scale,
                font = typography != null ? typography.FontAsset : null,
                fontMaterial = typography != null ? typography.MaterialPreset : null,
            };
        }
    }
}
