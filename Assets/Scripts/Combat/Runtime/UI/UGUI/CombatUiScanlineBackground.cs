using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// CRT scanline overlay for combat UI panels. Preserves the host RectTransform authoring
    /// (unlike Player Menu background builder, which stretches/reorders its root).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CombatUiScanlineBackground : MonoBehaviour
    {
        private const string OverlayName = "ScanlineOverlay";

        [Header("Scanlines")]
        [SerializeField] private bool scanlinesEnabled = true;
        [SerializeField] private RawImage scanlineOverlay;
        [SerializeField] private Texture scanlineTexture;
        [SerializeField] private Color scanlineColor = new Color(1f, 1f, 1f, 0.45f);
        [SerializeField] private Vector2 scanlineTiling = new Vector2(1f, 1f);
        [SerializeField] private Vector2 scanlineScrollDirection = Vector2.down;
        [SerializeField, Min(0f)] private float scanlineUvSpeed = 0.05f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField, HideInInspector] private float tilingRestHeight;

        public RawImage ScanlineOverlay => scanlineOverlay;
        public bool ScanlinesEnabled => scanlinesEnabled;

        private void Awake()
        {
            EnsureOverlay();
            ApplyPresentation();
        }

        private void OnEnable()
        {
            ApplyPresentation();
        }

        private void OnValidate()
        {
            if (scanlineOverlay != null)
                ApplyPresentation();
        }

        private void Update()
        {
            if (!scanlinesEnabled || scanlineOverlay == null || scanlineOverlay.texture == null || scanlineUvSpeed <= 0f)
                return;

            Vector2 direction = scanlineScrollDirection.sqrMagnitude > 0.0001f
                ? scanlineScrollDirection.normalized
                : Vector2.down;
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            Rect uv = scanlineOverlay.uvRect;
            uv.position += direction * scanlineUvSpeed * deltaTime;
            uv.width = Mathf.Max(0.01f, scanlineTiling.x);
            uv.height = ResolveTiledHeight();
            scanlineOverlay.uvRect = uv;
        }

        public void Configure(
            bool enabled,
            Texture texture = null,
            Color? color = null,
            Vector2? tiling = null,
            Vector2? scrollDirection = null,
            float? uvSpeed = null)
        {
            scanlinesEnabled = enabled;
            if (texture != null)
                scanlineTexture = texture;
            if (color.HasValue)
                scanlineColor = color.Value;
            if (tiling.HasValue)
                scanlineTiling = tiling.Value;
            if (scrollDirection.HasValue)
                scanlineScrollDirection = scrollDirection.Value;
            if (uvSpeed.HasValue)
                scanlineUvSpeed = Mathf.Max(0f, uvSpeed.Value);

            EnsureOverlay();
            ApplyPresentation();
        }

        public void ApplyPresentation()
        {
            EnsureOverlay();
            if (scanlineOverlay == null)
                return;

            scanlineOverlay.texture = scanlineTexture;
            scanlineOverlay.color = scanlineColor;
            scanlineOverlay.raycastTarget = false;
            ApplyUvRect(resetPosition: true);
            scanlineOverlay.gameObject.SetActive(scanlinesEnabled && scanlineTexture != null);
        }

        /// <summary>
        /// Locks scanline thickness to <paramref name="restHeight"/>. Later plate resizes
        /// scale UV height so lines do not stretch with the rect.
        /// </summary>
        public void SetTilingRestHeight(float restHeight)
        {
            tilingRestHeight = Mathf.Max(0f, restHeight);
            SyncTilingToCurrentHeight();
        }

        public void SyncTilingToCurrentHeight()
        {
            if (scanlineOverlay != null)
                ApplyUvRect(resetPosition: false);
        }

        private void ApplyUvRect(bool resetPosition)
        {
            if (scanlineOverlay == null)
                return;

            Rect uv = scanlineOverlay.uvRect;
            if (resetPosition)
                uv.position = Vector2.zero;
            uv.width = Mathf.Max(0.01f, scanlineTiling.x);
            uv.height = ResolveTiledHeight();
            scanlineOverlay.uvRect = uv;
        }

        private float ResolveTiledHeight()
        {
            float baseHeight = Mathf.Max(0.01f, scanlineTiling.y);
            if (tilingRestHeight <= 0.01f)
                return baseHeight;

            float currentHeight = ((RectTransform)transform).rect.height;
            if (currentHeight <= 0.01f)
                return baseHeight;
            return Mathf.Max(0.01f, baseHeight * (currentHeight / tilingRestHeight));
        }

        private void EnsureOverlay()
        {
            if (scanlineOverlay == null)
            {
                Transform existing = transform.Find(OverlayName);
                if (existing != null)
                    scanlineOverlay = existing.GetComponent<RawImage>();
            }

            if (scanlineOverlay == null)
                return;

            var rect = scanlineOverlay.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();
        }
    }
}
