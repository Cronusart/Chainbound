using System;
using UnityEngine;

namespace SignalJam.VisualFx
{
    [Flags]
    public enum VisualTierChannelMask
    {
        None = 0,
        Typography = 1 << 0,
        Mesh = 1 << 1,
        Aura = 1 << 2,
        All = Typography | Mesh | Aura,
    }

    /// <summary>
    /// Bundles the three channel presets for one visual tier / variant identity.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualTierProfile",
        menuName = "Chainbound/VFX/Visual Tier Profile")]
    public sealed class VisualTierProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string tierId = "tier_0";
        [SerializeField] private string displayName = "Tier 0";

        [Header("Shared Palette")]
        [Tooltip("When set, overrides per-channel palette references for this profile.")]
        [SerializeField] private VisualTierPaletteSO sharedPalette;

        [Header("Channels")]
        [SerializeField] private VisualTierTypographyPresetSO typographyPreset;
        [SerializeField] private VisualTierMeshPresetSO meshPreset;
        [SerializeField] private VisualTierAuraPresetSO auraPreset;

        public string TierId => tierId;
        public string DisplayName => displayName;
        public VisualTierPaletteSO SharedPalette => sharedPalette;
        public VisualTierTypographyPresetSO TypographyPreset => typographyPreset;
        public VisualTierMeshPresetSO MeshPreset => meshPreset;
        public VisualTierAuraPresetSO AuraPreset => auraPreset;

        public VisualTierPaletteSO ResolvePaletteForTypography()
        {
            return sharedPalette != null ? sharedPalette : typographyPreset != null ? typographyPreset.Palette : null;
        }

        public VisualTierPaletteSO ResolvePaletteForMesh()
        {
            return sharedPalette != null ? sharedPalette : meshPreset != null ? meshPreset.Palette : null;
        }

        public VisualTierPaletteSO ResolvePaletteForAura()
        {
            return sharedPalette != null ? sharedPalette : auraPreset != null ? auraPreset.Palette : null;
        }
    }
}
