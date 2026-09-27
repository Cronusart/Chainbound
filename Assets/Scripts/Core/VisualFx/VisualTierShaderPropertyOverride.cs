using System;
using UnityEngine;

namespace SignalJam.VisualFx
{
    public enum VisualTierShaderPropertyType
    {
        Float = 0,
        Color = 1,
        Vector = 2,
    }

    [Serializable]
    public struct VisualTierShaderPropertyOverride
    {
        [Tooltip("Shader property name, e.g. _FaceColor, _BaseColor, _EmissionColor.")]
        public string propertyName;

        public VisualTierShaderPropertyType type;

        public float floatValue;

        public Color colorValue;

        public Vector4 vectorValue;

        public bool IsValid => !string.IsNullOrWhiteSpace(propertyName);

        public void ApplyTo(Material material)
        {
            if (material == null || !IsValid)
                return;

            switch (type)
            {
                case VisualTierShaderPropertyType.Float:
                    if (material.HasProperty(propertyName))
                        material.SetFloat(propertyName, floatValue);
                    break;
                case VisualTierShaderPropertyType.Color:
                    if (material.HasProperty(propertyName))
                        material.SetColor(propertyName, colorValue);
                    break;
                case VisualTierShaderPropertyType.Vector:
                    if (material.HasProperty(propertyName))
                        material.SetVector(propertyName, vectorValue);
                    break;
            }
        }

        public void ApplyTo(MaterialPropertyBlock block, Material referenceMaterial)
        {
            if (block == null || referenceMaterial == null || !IsValid)
                return;

            if (!referenceMaterial.HasProperty(propertyName))
                return;

            switch (type)
            {
                case VisualTierShaderPropertyType.Float:
                    block.SetFloat(propertyName, floatValue);
                    break;
                case VisualTierShaderPropertyType.Color:
                    block.SetColor(propertyName, colorValue);
                    break;
                case VisualTierShaderPropertyType.Vector:
                    block.SetVector(propertyName, vectorValue);
                    break;
            }
        }
    }
}
