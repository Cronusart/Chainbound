using SignalJam.VisualFx;
using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SpriteFlipbookVfxPlayer : MonoBehaviour
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int MainTexStId = Shader.PropertyToID("_MainTex_ST");
        private static readonly int PixelCanvasSizeId = Shader.PropertyToID("_PixelCanvasSize");

        private static Mesh s_sharedQuadMesh;
        private static Material s_fallbackMaterial;

        private SpriteFlipbookVfxDefinitionSO _definition;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private float _elapsed;
        private int _currentFrame = -1;

        public static SpriteFlipbookVfxPlayer Spawn(SpriteFlipbookVfxDefinitionSO definition, Vector3 position, float scale, Transform parent = null)
        {
            if (definition == null)
                return null;

            var root = new GameObject(string.IsNullOrWhiteSpace(definition.vfxId) ? "SpriteFlipbookVfx" : definition.vfxId);
            if (parent != null)
                root.transform.SetParent(parent, true);

            root.transform.position = position;

            var player = root.AddComponent<SpriteFlipbookVfxPlayer>();
            player.Initialize(definition, scale);
            return player;
        }

        private void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Initialize(SpriteFlipbookVfxDefinitionSO definition, float scale)
        {
            _definition = definition;
            _elapsed = 0f;
            _currentFrame = -1;

            if (_meshFilter == null)
                _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null)
                _meshRenderer = GetComponent<MeshRenderer>();
            if (_propertyBlock == null)
                _propertyBlock = new MaterialPropertyBlock();

            _meshFilter.sharedMesh = GetSharedQuadMesh();
            _meshRenderer.sharedMaterial = ResolveMaterial(definition);

            if (definition.pixelBattlePreset != null)
            {
                _meshRenderer.GetPropertyBlock(_propertyBlock);
                definition.pixelBattlePreset.ApplyTo(_propertyBlock, _meshRenderer.sharedMaterial);
                _meshRenderer.SetPropertyBlock(_propertyBlock);
            }

            Vector2 size = definition.spriteSize == Vector2.zero ? Vector2.one : definition.spriteSize;
            transform.localScale = new Vector3(size.x * scale, size.y * scale, 1f);

            ApplyFrame(0);
        }

        private void Update()
        {
            StepPlayback(Time.deltaTime);
        }

        public void StepPlayback(float deltaTime)
        {
            if (_definition == null || _definition.sprites == null || _definition.sprites.Length == 0)
            {
                DestroySelf();
                return;
            }

            _elapsed += deltaTime;

            int nextFrame = _definition.fps > 0f ? Mathf.FloorToInt(_elapsed * _definition.fps) : 0;
            nextFrame = Mathf.Clamp(nextFrame, 0, _definition.sprites.Length - 1);

            if (nextFrame != _currentFrame)
                ApplyFrame(nextFrame);

            if (_elapsed >= _definition.GetDurationSeconds())
                DestroySelf();
        }

        private void DestroySelf()
        {
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }

        private void ApplyFrame(int frameIndex)
        {
            if (_definition == null || _definition.sprites == null || _definition.sprites.Length == 0)
                return;

            frameIndex = Mathf.Clamp(frameIndex, 0, _definition.sprites.Length - 1);
            if (_currentFrame == frameIndex && _currentFrame >= 0)
                return;

            _currentFrame = frameIndex;
            Sprite sprite = _definition.sprites[frameIndex];
            if (sprite == null || sprite.texture == null)
                return;

            _meshRenderer.GetPropertyBlock(_propertyBlock);
            if (!UsesSourceTextureOverride())
            {
                _propertyBlock.SetTexture(MainTexId, sprite.texture);
                _propertyBlock.SetVector(
                    MainTexStId,
                    PixelBattleVfxSourceTextureUtility.ResolveTextureSt(sprite, _definition.spriteCanvasSize));
            }

            Vector2 canvasSize = UsesSourceTextureOverride()
                ? PixelBattleVfxSourceTextureUtility.ResolveTextureCanvasSize(_definition.pixelBattlePreset.SourceTextureOverride)
                : PixelBattleVfxSourceTextureUtility.ResolveCanvasSize(sprite, _definition.spriteCanvasSize);
            _propertyBlock.SetVector(PixelCanvasSizeId, new Vector4(canvasSize.x, canvasSize.y, 0f, 0f));

            _propertyBlock.SetColor("_Color", _definition.pixelBattlePreset != null
                ? _definition.pixelBattlePreset.Tint
                : Color.white);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
        }

        private Material ResolveMaterial(SpriteFlipbookVfxDefinitionSO definition)
        {
            if (definition != null &&
                definition.pixelBattlePreset != null &&
                definition.pixelBattlePreset.Material != null)
            {
                return definition.pixelBattlePreset.Material;
            }

            return definition != null && definition.material != null
                ? definition.material
                : GetFallbackMaterial();
        }

        private bool UsesSourceTextureOverride()
        {
            return _definition != null &&
                   _definition.pixelBattlePreset != null &&
                   _definition.pixelBattlePreset.OverridesSourceTexture;
        }

        private static Mesh GetSharedQuadMesh()
        {
            if (s_sharedQuadMesh != null)
                return s_sharedQuadMesh;

            s_sharedQuadMesh = new Mesh { name = "SpriteFlipbookVfxQuad" };
            s_sharedQuadMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            s_sharedQuadMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            s_sharedQuadMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            s_sharedQuadMesh.RecalculateBounds();
            s_sharedQuadMesh.RecalculateNormals();
            return s_sharedQuadMesh;
        }

        private static Material GetFallbackMaterial()
        {
            if (s_fallbackMaterial != null)
                return s_fallbackMaterial;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            s_fallbackMaterial = new Material(shader)
            {
                name = "SpriteFlipbookVfxFallback"
            };
            s_fallbackMaterial.hideFlags = HideFlags.HideAndDontSave;
            return s_fallbackMaterial;
        }
    }
}
