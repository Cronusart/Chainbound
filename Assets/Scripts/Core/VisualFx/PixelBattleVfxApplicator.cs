using System;
using UnityEngine;

namespace SignalJam.VisualFx
{
    [DisallowMultipleComponent]
    public sealed class PixelBattleVfxApplicator : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int PixelCanvasSizeId = Shader.PropertyToID("_PixelCanvasSize");

        public enum SourceTextureAuthority
        {
            PresetOverride = 0,
            Renderer = 1
        }

        [Header("Preset")]
        [SerializeField] private PixelBattleVfxPresetSO preset;

        [Header("Targets")]
        [SerializeField] private Renderer[] targetRenderers = Array.Empty<Renderer>();
        [SerializeField] private bool includeChildRenderers = true;
        [SerializeField] private bool includeInactiveChildren = true;

        [Header("Material")]
        [SerializeField] private bool assignPresetMaterial = true;
        [SerializeField] private int materialSlotIndex;

        [Header("Source Texture")]
        [Tooltip("Preset Override uses the preset's optional source texture. Renderer preserves the target renderer's source texture. Animated source owners always retain their current frame.")]
        [SerializeField] private SourceTextureAuthority sourceTextureAuthority = SourceTextureAuthority.PresetOverride;

        [Header("Pixel Canvas")]
        [Tooltip("Optional source-frame canvas in pixels. Zero uses the selected source texture dimensions. Set this for texture-sheet particles or padded source frames.")]
        [SerializeField] private Vector2Int pixelCanvasSizeOverride;

        [Header("Playback")]
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool clearOnDisable = true;

        private MaterialPropertyBlock propertyBlock;
        private Renderer[] resolvedRenderers = Array.Empty<Renderer>();

        public PixelBattleVfxPresetSO Preset
        {
            get => preset;
            set => preset = value;
        }

        public bool OverridesSourceTexture =>
            sourceTextureAuthority == SourceTextureAuthority.PresetOverride &&
            preset != null &&
            preset.OverridesSourceTexture;

        private void OnEnable()
        {
            if (applyOnEnable)
                Apply();
        }

        private void OnDisable()
        {
            if (clearOnDisable)
                Clear();
        }

        public void Apply()
        {
            if (preset == null)
                return;

            EnsurePropertyBlock();
            ResolveRenderers();

            for (int i = 0; i < resolvedRenderers.Length; i++)
                ApplyToRenderer(resolvedRenderers[i]);
        }

        public void Clear()
        {
            ResolveRenderers();

            for (int i = 0; i < resolvedRenderers.Length; i++)
            {
                Renderer renderer = resolvedRenderers[i];
                if (renderer == null)
                    continue;

                renderer.SetPropertyBlock(null, Mathf.Max(0, materialSlotIndex));
            }
        }

        private void ApplyToRenderer(Renderer renderer)
        {
            if (renderer == null)
                return;

            int slot = Mathf.Max(0, materialSlotIndex);
            Material referenceMaterial = ResolveReferenceMaterial(renderer, slot);
            if (referenceMaterial == null)
                return;

            renderer.GetPropertyBlock(propertyBlock, slot);
            if (sourceTextureAuthority == SourceTextureAuthority.Renderer ||
                HasSourceTextureOwner(renderer, slot))
            {
                preset.ApplyStyleTo(propertyBlock, referenceMaterial);
            }
            else
            {
                preset.ApplyTo(propertyBlock, referenceMaterial);
            }

            if (!HasSourceTextureOwner(renderer, slot))
                ApplyPixelCanvasSize(propertyBlock, referenceMaterial);

            renderer.SetPropertyBlock(propertyBlock, slot);
        }

        private void ApplyPixelCanvasSize(MaterialPropertyBlock block, Material referenceMaterial)
        {
            if (block == null || referenceMaterial == null || !referenceMaterial.HasProperty(PixelCanvasSizeId))
                return;

            Vector2 canvasSize = pixelCanvasSizeOverride.x > 0 && pixelCanvasSizeOverride.y > 0
                ? new Vector2(pixelCanvasSizeOverride.x, pixelCanvasSizeOverride.y)
                : Vector2.zero;

            if (canvasSize == Vector2.zero)
            {
                Texture sourceTexture = null;
                if (sourceTextureAuthority == SourceTextureAuthority.PresetOverride &&
                    preset != null && preset.OverridesSourceTexture)
                {
                    sourceTexture = preset.SourceTextureOverride;
                }

                sourceTexture ??= block.GetTexture(MainTexId);
                sourceTexture ??= referenceMaterial.GetTexture(MainTexId);
                canvasSize = PixelBattleVfxSourceTextureUtility.ResolveTextureCanvasSize(sourceTexture);
            }

            if (canvasSize.x > 0f && canvasSize.y > 0f)
                block.SetVector(PixelCanvasSizeId, new Vector4(canvasSize.x, canvasSize.y, 0f, 0f));
        }

        private static bool HasSourceTextureOwner(Renderer renderer, int materialSlotIndex)
        {
            if (renderer == null)
                return false;

            MonoBehaviour[] components = renderer.GetComponentsInParent<MonoBehaviour>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IPixelBattleSourceTextureOwner sourceOwner &&
                    sourceOwner.OwnsPixelBattleSourceTexture(renderer, materialSlotIndex))
                {
                    return true;
                }
            }

            return false;
        }

        private Material ResolveReferenceMaterial(Renderer renderer, int slot)
        {
            Material presetMaterial = preset != null ? preset.Material : null;

            if (assignPresetMaterial && presetMaterial != null)
            {
                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials.Length <= slot)
                {
                    Material[] resized = new Material[slot + 1];
                    for (int i = 0; i < sharedMaterials.Length; i++)
                        resized[i] = sharedMaterials[i];
                    sharedMaterials = resized;
                }

                sharedMaterials[slot] = presetMaterial;
                renderer.sharedMaterials = sharedMaterials;
                return presetMaterial;
            }

            Material[] materials = renderer.sharedMaterials;
            if (materials.Length > slot && materials[slot] != null)
                return materials[slot];

            return renderer.sharedMaterial;
        }

        private void ResolveRenderers()
        {
            Renderer[] childRenderers = includeChildRenderers
                ? GetComponentsInChildren<Renderer>(includeInactiveChildren)
                : Array.Empty<Renderer>();

            int total = CountValidUnique(targetRenderers, childRenderers);
            if (resolvedRenderers.Length != total)
                resolvedRenderers = new Renderer[total];

            int index = 0;
            AppendUnique(targetRenderers, resolvedRenderers, ref index);
            AppendUnique(childRenderers, resolvedRenderers, ref index);
        }

        private static int CountValidUnique(Renderer[] first, Renderer[] second)
        {
            int count = 0;

            if (first != null)
            {
                for (int i = 0; i < first.Length; i++)
                {
                    Renderer renderer = first[i];
                    if (renderer != null && !Contains(first, i, renderer))
                        count++;
                }
            }

            if (second != null)
            {
                for (int i = 0; i < second.Length; i++)
                {
                    Renderer renderer = second[i];
                    if (renderer != null &&
                        !Contains(first, first != null ? first.Length : 0, renderer) &&
                        !Contains(second, i, renderer))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void AppendUnique(Renderer[] source, Renderer[] destination, ref int index)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Length; i++)
            {
                Renderer renderer = source[i];
                if (renderer == null || Contains(destination, index, renderer))
                    continue;

                destination[index] = renderer;
                index++;
            }
        }

        private static bool Contains(Renderer[] source, int exclusiveEnd, Renderer renderer)
        {
            if (source == null || renderer == null)
                return false;

            int end = Mathf.Min(exclusiveEnd, source.Length);
            for (int i = 0; i < end; i++)
            {
                if (source[i] == renderer)
                    return true;
            }

            return false;
        }

        private void EnsurePropertyBlock()
        {
            propertyBlock ??= new MaterialPropertyBlock();
        }

        private void OnValidate()
        {
            materialSlotIndex = Mathf.Max(0, materialSlotIndex);
            pixelCanvasSizeOverride.x = Mathf.Max(0, pixelCanvasSizeOverride.x);
            pixelCanvasSizeOverride.y = Mathf.Max(0, pixelCanvasSizeOverride.y);
        }
    }
}
