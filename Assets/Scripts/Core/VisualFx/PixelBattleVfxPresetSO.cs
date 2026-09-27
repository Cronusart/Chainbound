using UnityEngine;

namespace SignalJam.VisualFx
{
    [CreateAssetMenu(
        fileName = "PixelBattleVfxPreset",
        menuName = "Chainbound/VFX/Pixel Battle VFX Preset")]
    public sealed class PixelBattleVfxPresetSO : ScriptableObject
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int PixelCellsId = Shader.PropertyToID("_PixelCells");
        private static readonly int PosterizeStepsId = Shader.PropertyToID("_PosterizeSteps");
        private static readonly int AlphaCutoffId = Shader.PropertyToID("_AlphaCutoff");
        private static readonly int DissolveAmountId = Shader.PropertyToID("_DissolveAmount");
        private static readonly int DissolveFeatherId = Shader.PropertyToID("_DissolveFeather");
        private static readonly int DissolveNoiseScaleId = Shader.PropertyToID("_DissolveNoiseScale");
        private static readonly int DissolveNoiseSpeedId = Shader.PropertyToID("_DissolveNoiseSpeed");
        private static readonly int EmissionStrengthId = Shader.PropertyToID("_EmissionStrength");

        [Header("Material")]
        [SerializeField] private Material material;
        [SerializeField] private bool overrideSourceTexture;
        [SerializeField] private Texture sourceTextureOverride;

        [Header("Pixel Style")]
        [SerializeField] private PixelBattleVfxStyleSO style;
        [Min(0.125f)]
        [SerializeField] private float pixelCellMultiplier = 1f;
        [Min(0f)]
        [SerializeField] private float posterizeSteps;

        [Header("Color")]
        [ColorUsage(true, true)]
        [SerializeField] private Color tint = Color.white;
        [Min(0f)]
        [SerializeField] private float emissionStrength = 1f;

        [Header("Alpha")]
        [Range(0f, 1f)]
        [SerializeField] private float alphaCutoff = 0.01f;
        [Range(0f, 1f)]
        [SerializeField] private float dissolveAmount;
        [Range(0.001f, 1f)]
        [SerializeField] private float dissolveFeather = 0.08f;
        [SerializeField] private Vector2 dissolveNoiseScale = new Vector2(16f, 16f);
        [SerializeField] private float dissolveNoiseSpeed;

        public Material Material => material;
        public bool OverridesSourceTexture => overrideSourceTexture && sourceTextureOverride != null;
        public Texture SourceTextureOverride => sourceTextureOverride;
        public PixelBattleVfxStyleSO Style => style;
        public float PixelCellMultiplier => pixelCellMultiplier;
        public Color Tint => tint;
        public float EmissionStrength => emissionStrength;

        public void ApplyTo(MaterialPropertyBlock block, Material referenceMaterial)
        {
            ApplyTo(block, referenceMaterial, includeSourceTextureOverride: true);
        }

        /// <summary>
        /// Applies the visual style without replacing a renderer-owned source texture or atlas transform.
        /// </summary>
        public void ApplyStyleTo(MaterialPropertyBlock block, Material referenceMaterial)
        {
            ApplyTo(block, referenceMaterial, includeSourceTextureOverride: false);
        }

        private void ApplyTo(MaterialPropertyBlock block, Material referenceMaterial, bool includeSourceTextureOverride)
        {
            if (block == null)
                return;

            if (includeSourceTextureOverride && OverridesSourceTexture && HasProperty(referenceMaterial, MainTexId))
                block.SetTexture(MainTexId, sourceTextureOverride);

            ApplyColor(block, referenceMaterial, ColorId, tint);
            ApplyFloat(block, referenceMaterial, PixelCellsId, ResolvePixelCells());
            ApplyFloat(block, referenceMaterial, PosterizeStepsId, posterizeSteps);
            ApplyFloat(block, referenceMaterial, AlphaCutoffId, alphaCutoff);
            ApplyFloat(block, referenceMaterial, DissolveAmountId, dissolveAmount);
            ApplyFloat(block, referenceMaterial, DissolveFeatherId, dissolveFeather);
            ApplyVector(block, referenceMaterial, DissolveNoiseScaleId, new Vector4(dissolveNoiseScale.x, dissolveNoiseScale.y, 0f, 0f));
            ApplyFloat(block, referenceMaterial, DissolveNoiseSpeedId, dissolveNoiseSpeed);
            ApplyFloat(block, referenceMaterial, EmissionStrengthId, emissionStrength);
        }

        public void ApplyTo(Material targetMaterial)
        {
            if (targetMaterial == null)
                return;

            if (OverridesSourceTexture && targetMaterial.HasProperty(MainTexId))
                targetMaterial.SetTexture(MainTexId, sourceTextureOverride);
            if (targetMaterial.HasProperty(ColorId))
                targetMaterial.SetColor(ColorId, tint);
            if (targetMaterial.HasProperty(PixelCellsId))
                targetMaterial.SetFloat(PixelCellsId, ResolvePixelCells());
            if (targetMaterial.HasProperty(PosterizeStepsId))
                targetMaterial.SetFloat(PosterizeStepsId, posterizeSteps);
            if (targetMaterial.HasProperty(AlphaCutoffId))
                targetMaterial.SetFloat(AlphaCutoffId, alphaCutoff);
            if (targetMaterial.HasProperty(DissolveAmountId))
                targetMaterial.SetFloat(DissolveAmountId, dissolveAmount);
            if (targetMaterial.HasProperty(DissolveFeatherId))
                targetMaterial.SetFloat(DissolveFeatherId, dissolveFeather);
            if (targetMaterial.HasProperty(DissolveNoiseScaleId))
                targetMaterial.SetVector(DissolveNoiseScaleId, new Vector4(dissolveNoiseScale.x, dissolveNoiseScale.y, 0f, 0f));
            if (targetMaterial.HasProperty(DissolveNoiseSpeedId))
                targetMaterial.SetFloat(DissolveNoiseSpeedId, dissolveNoiseSpeed);
            if (targetMaterial.HasProperty(EmissionStrengthId))
                targetMaterial.SetFloat(EmissionStrengthId, emissionStrength);
        }

        private float ResolvePixelCells()
        {
            float baseCells = style != null ? style.BasePixelCells : 16f;
            return Mathf.Max(1f, baseCells * pixelCellMultiplier);
        }

        private static void ApplyColor(MaterialPropertyBlock block, Material referenceMaterial, int propertyId, Color value)
        {
            if (HasProperty(referenceMaterial, propertyId))
                block.SetColor(propertyId, value);
        }

        private static void ApplyVector(MaterialPropertyBlock block, Material referenceMaterial, int propertyId, Vector4 value)
        {
            if (HasProperty(referenceMaterial, propertyId))
                block.SetVector(propertyId, value);
        }

        private static void ApplyFloat(MaterialPropertyBlock block, Material referenceMaterial, int propertyId, float value)
        {
            if (HasProperty(referenceMaterial, propertyId))
                block.SetFloat(propertyId, value);
        }

        private static bool HasProperty(Material material, int propertyId)
        {
            return material == null || material.HasProperty(propertyId);
        }

        private void OnValidate()
        {
            pixelCellMultiplier = Mathf.Max(0.125f, pixelCellMultiplier);
            posterizeSteps = Mathf.Max(0f, posterizeSteps);
            alphaCutoff = Mathf.Clamp01(alphaCutoff);
            dissolveAmount = Mathf.Clamp01(dissolveAmount);
            dissolveFeather = Mathf.Max(0.001f, dissolveFeather);
            dissolveNoiseScale.x = Mathf.Max(0.001f, dissolveNoiseScale.x);
            dissolveNoiseScale.y = Mathf.Max(0.001f, dissolveNoiseScale.y);
            emissionStrength = Mathf.Max(0f, emissionStrength);
        }
    }
}
