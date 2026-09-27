using UnityEngine;
using UnityEngine.Rendering;

namespace SignalJam.VisualFx
{
    /// <summary>
    /// Applies mesh channel presets to renderers via shared materials, instances, or property blocks.
    /// </summary>
    public static class VisualTierMeshApplication
    {
        public static void Apply(Renderer renderer, VisualTierProfileSO profile, MaterialPropertyBlock propertyBlock = null)
        {
            if (renderer == null || profile == null)
                return;

            VisualTierMeshPresetSO preset = profile.MeshPreset;
            if (preset == null)
                return;

            VisualTierPaletteSO palette = profile.ResolvePaletteForMesh();
            MaterialPropertyBlock block = propertyBlock ?? new MaterialPropertyBlock();
            int slot = Mathf.Clamp(preset.MaterialSlotIndex, 0, Mathf.Max(0, renderer.sharedMaterials.Length - 1));

            renderer.shadowCastingMode = preset.CastShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            renderer.receiveShadows = preset.ReceiveShadows;

            Material referenceMaterial = ResolveReferenceMaterial(renderer, preset, slot);
            if (referenceMaterial == null)
                referenceMaterial = renderer.sharedMaterial;

            block.Clear();

            if (preset.ApplyBaseColorTint && !string.IsNullOrWhiteSpace(preset.BaseColorPropertyName))
            {
                Color baseColor = VisualTierPaletteUtility.ResolveTintedSlot(
                    palette,
                    preset.BaseColorSlot,
                    preset.CustomBaseColor,
                    preset.BaseColorTint,
                    Color.white);
                block.SetColor(preset.BaseColorPropertyName, baseColor);
            }

            if (preset.ApplyEmissiveTint && !string.IsNullOrWhiteSpace(preset.EmissivePropertyName))
            {
                Color emissive = VisualTierPaletteUtility.ResolveSlot(
                    palette,
                    preset.EmissiveColorSlot,
                    preset.CustomEmissiveColor,
                    Color.black) * preset.EmissiveStrength;
                block.SetColor(preset.EmissivePropertyName, emissive);
            }

            VisualTierShaderPropertyOverride[] overrides = preset.MaterialPropertyOverrides;
            if (overrides != null)
            {
                for (int i = 0; i < overrides.Length; i++)
                    overrides[i].ApplyTo(block, referenceMaterial);
            }

            renderer.SetPropertyBlock(block, slot);

            if (preset.RenderQueueOffset != 0 && referenceMaterial != null)
            {
                Material target = preset.MaterialAssignment == VisualTierMaterialAssignmentMode.SharedMaterial
                    ? renderer.sharedMaterials[slot]
                    : renderer.materials[slot];

                if (target != null)
                    target.renderQueue = referenceMaterial.renderQueue + preset.RenderQueueOffset;
            }
        }

        public static void ClearPropertyBlock(Renderer renderer, int materialSlotIndex = 0)
        {
            if (renderer == null)
                return;

            renderer.SetPropertyBlock(null, materialSlotIndex);
        }

        private static Material ResolveReferenceMaterial(
            Renderer renderer,
            VisualTierMeshPresetSO preset,
            int slot)
        {
            if (preset.Material == null)
                return renderer.sharedMaterials.Length > slot ? renderer.sharedMaterials[slot] : renderer.sharedMaterial;

            switch (preset.MaterialAssignment)
            {
                case VisualTierMaterialAssignmentMode.SharedMaterial:
                {
                    Material[] shared = renderer.sharedMaterials;
                    if (shared.Length <= slot)
                    {
                        Material[] resized = new Material[slot + 1];
                        for (int i = 0; i < shared.Length; i++)
                            resized[i] = shared[i];
                        shared = resized;
                    }

                    shared[slot] = preset.Material;
                    renderer.sharedMaterials = shared;
                    return preset.Material;
                }

                case VisualTierMaterialAssignmentMode.MaterialInstance:
                {
                    Material[] materials = renderer.materials;
                    if (materials.Length <= slot)
                    {
                        Material[] resized = new Material[slot + 1];
                        for (int i = 0; i < materials.Length; i++)
                            resized[i] = materials[i];
                        materials = resized;
                    }

                    materials[slot] = preset.Material;
                    renderer.materials = materials;
                    return materials[slot];
                }

                default:
                    return preset.Material != null ? preset.Material : renderer.sharedMaterial;
            }
        }
    }
}
