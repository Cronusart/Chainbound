using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Binds a UI Toolkit RenderTexture to a mesh renderer material slot using a MaterialPropertyBlock.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WalletScreenSurface : MonoBehaviour
    {
        [SerializeField] private MeshRenderer screenRenderer;
        [SerializeField] private int materialIndex = 0;
        [SerializeField] private RenderTexture targetRT;
        [SerializeField] private string textureProperty = "_BaseMap";
        [SerializeField] private string fallbackTextureProperty = "_MainTex";

        private MaterialPropertyBlock _propertyBlock;
        private int _texturePropertyId;
        private int _fallbackTexturePropertyId;

        private void Reset()
        {
            if (screenRenderer == null)
                screenRenderer = GetComponent<MeshRenderer>();
        }

        private void OnEnable()
        {
            ApplyRenderTexture();
        }

        private void OnValidate()
        {
            materialIndex = Mathf.Max(0, materialIndex);
            ApplyRenderTexture();
        }

        [ContextMenu("Apply Render Texture")]
        public void ApplyRenderTexture()
        {
            if (screenRenderer == null || targetRT == null)
                return;

            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            var sharedMaterials = screenRenderer.sharedMaterials;
            if (sharedMaterials == null || materialIndex >= sharedMaterials.Length)
                return;

            int propertyId = ResolveTexturePropertyId(sharedMaterials[materialIndex]);
            if (propertyId == -1)
                return;

            screenRenderer.GetPropertyBlock(_propertyBlock, materialIndex);
            _propertyBlock.SetTexture(propertyId, targetRT);
            screenRenderer.SetPropertyBlock(_propertyBlock, materialIndex);
        }

        private int ResolveTexturePropertyId(Material material)
        {
            if (material == null)
                return -1;

            if (_texturePropertyId == 0)
                _texturePropertyId = Shader.PropertyToID(textureProperty);
            if (_fallbackTexturePropertyId == 0)
                _fallbackTexturePropertyId = Shader.PropertyToID(fallbackTextureProperty);

            if (!string.IsNullOrEmpty(textureProperty) && material.HasProperty(_texturePropertyId))
                return _texturePropertyId;

            if (!string.IsNullOrEmpty(fallbackTextureProperty) && material.HasProperty(_fallbackTexturePropertyId))
                return _fallbackTexturePropertyId;

            return -1;
        }
    }
}
