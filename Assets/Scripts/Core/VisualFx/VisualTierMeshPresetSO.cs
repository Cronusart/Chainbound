using System;
using UnityEngine;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Mesh / renderer material preset for a visual tier or variant.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisualTierMeshPreset",
        menuName = "Chainbound/VFX/Mesh Preset")]
    public sealed class VisualTierMeshPresetSO : ScriptableObject
    {
        [Header("Palette")]
        [SerializeField] private VisualTierPaletteSO palette;

        [Header("Material")]
        [SerializeField] private Material material;
        [SerializeField] private int materialSlotIndex;
        [SerializeField] private VisualTierMaterialAssignmentMode materialAssignment =
            VisualTierMaterialAssignmentMode.PropertyBlockOnly;

        [Header("Tint")]
        [SerializeField] private bool applyBaseColorTint = true;
        [SerializeField] private string baseColorPropertyName = "_BaseColor";
        [SerializeField] private VisualTierColorSlot baseColorSlot = VisualTierColorSlot.Primary;
        [SerializeField] private Color customBaseColor = Color.white;
        [SerializeField] private Color baseColorTint = Color.white;

        [Header("Emissive")]
        [SerializeField] private bool applyEmissiveTint;
        [SerializeField] private string emissivePropertyName = "_EmissionColor";
        [SerializeField] private VisualTierColorSlot emissiveColorSlot = VisualTierColorSlot.Emissive;
        [SerializeField] private Color customEmissiveColor = Color.black;
        [Range(0f, 8f)]
        [SerializeField] private float emissiveStrength = 1f;

        [Header("Render")]
        [SerializeField] private int renderQueueOffset;
        [SerializeField] private bool castShadows = true;
        [SerializeField] private bool receiveShadows = true;

        [Header("Advanced Shader Overrides")]
        [SerializeField] private VisualTierShaderPropertyOverride[] materialPropertyOverrides =
            Array.Empty<VisualTierShaderPropertyOverride>();

        public VisualTierPaletteSO Palette => palette;
        public Material Material => material;
        public int MaterialSlotIndex => materialSlotIndex;
        public VisualTierMaterialAssignmentMode MaterialAssignment => materialAssignment;
        public bool ApplyBaseColorTint => applyBaseColorTint;
        public string BaseColorPropertyName => baseColorPropertyName;
        public VisualTierColorSlot BaseColorSlot => baseColorSlot;
        public Color CustomBaseColor => customBaseColor;
        public Color BaseColorTint => baseColorTint;
        public bool ApplyEmissiveTint => applyEmissiveTint;
        public string EmissivePropertyName => emissivePropertyName;
        public VisualTierColorSlot EmissiveColorSlot => emissiveColorSlot;
        public Color CustomEmissiveColor => customEmissiveColor;
        public float EmissiveStrength => emissiveStrength;
        public int RenderQueueOffset => renderQueueOffset;
        public bool CastShadows => castShadows;
        public bool ReceiveShadows => receiveShadows;
        public VisualTierShaderPropertyOverride[] MaterialPropertyOverrides => materialPropertyOverrides;

        private void OnValidate()
        {
            materialSlotIndex = Mathf.Max(0, materialSlotIndex);
            emissiveStrength = Mathf.Max(0f, emissiveStrength);
        }
    }
}
