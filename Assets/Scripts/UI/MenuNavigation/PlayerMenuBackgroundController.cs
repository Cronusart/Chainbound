using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class PlayerMenuBackgroundController : MonoBehaviour
    {
        [Header("Authored Presentation")]
        [Tooltip("When true, Play Mode keeps Image/RawImage colors, sprites, and textures authored on the prefab. Controller fields are synced FROM those visuals on Awake instead of overwriting them.")]
        [SerializeField] private bool respectAuthoredPresentation = true;

        [Header("Base Tint")]
        [SerializeField] private Image baseTintImage;
        [SerializeField] private Sprite baseTintSprite;
        [SerializeField] private Image.Type baseTintImageType = Image.Type.Simple;
        [SerializeField, Min(0.01f)] private float baseTintPixelsPerUnitMultiplier = 1f;
        [SerializeField] private Color baseTintColor = new Color(0.02f, 0.03f, 0.06f, 0.5f);
        [SerializeField] private bool baseTintBlocksRaycasts;

        [Header("Scanlines")]
        [SerializeField] private bool scanlinesEnabled;
        [SerializeField] private RawImage scanlineOverlay;
        [SerializeField] private Texture scanlineTexture;
        [SerializeField] private Color scanlineColor = new Color(1f, 1f, 1f, 0.12f);
        [SerializeField] private Vector2 scanlineTiling = new Vector2(1f, 8f);
        [SerializeField] private Vector2 scanlineScrollDirection = Vector2.down;
        [SerializeField, Min(0f)] private float scanlineUvSpeed = 0.05f;
        [SerializeField] private bool useUnscaledTime = true;

        private bool capturedAuthoredPresentation;

        public bool RespectAuthoredPresentation
        {
            get => respectAuthoredPresentation;
            set => respectAuthoredPresentation = value;
        }

        public Image BaseTintImage => baseTintImage;
        public RawImage ScanlineOverlay => scanlineOverlay;
        public bool ScanlinesEnabled => scanlinesEnabled;
        public Texture ScanlineTexture => scanlineTexture;

        private void Awake()
        {
            EnsureHierarchy();
            if (respectAuthoredPresentation)
                CaptureAuthoredPresentation();
            ApplyPresentation();
        }

        /// <summary>
        /// Wire scanline presentation for reuse on nested panels (e.g. inventory Use popup).
        /// Null / omitted optional args keep the existing serialized values.
        /// When <see cref="respectAuthoredPresentation"/> is true, pass <paramref name="force"/> to intentionally override prefab visuals.
        /// </summary>
        public void ConfigureScanlines(
            bool enabled,
            Texture texture = null,
            Color? color = null,
            Vector2? tiling = null,
            Vector2? scrollDirection = null,
            float? uvSpeed = null,
            bool force = false)
        {
            if (respectAuthoredPresentation && !force)
                return;

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

            EnsureHierarchy();
            ApplyPresentation();
        }

        public void ConfigureBaseTint(Color color, Sprite sprite = null, bool? blocksRaycasts = null, bool force = false)
        {
            if (respectAuthoredPresentation && !force)
                return;

            baseTintColor = color;
            if (sprite != null)
                baseTintSprite = sprite;
            if (blocksRaycasts.HasValue)
                baseTintBlocksRaycasts = blocksRaycasts.Value;

            EnsureHierarchy();
            ApplyPresentation();
        }

        public void SetBaseTintBlocksRaycasts(bool blocksRaycasts)
        {
            baseTintBlocksRaycasts = blocksRaycasts;
            if (baseTintImage != null)
                baseTintImage.raycastTarget = baseTintBlocksRaycasts;
        }

        private void OnEnable()
        {
            if (respectAuthoredPresentation && !capturedAuthoredPresentation)
                CaptureAuthoredPresentation();
            ApplyPresentation();
        }

        private void OnValidate()
        {
            // Editor: push inspector fields to visuals only when not protecting authored prefab look.
            if (!respectAuthoredPresentation)
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
            scanlineOverlay.uvRect = uv;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureHierarchy();
            if (!respectAuthoredPresentation)
                ApplyPresentation();
        }

        /// <summary>
        /// Pull live Image/RawImage values into serialized fields so the controller matches the prefab.
        /// </summary>
        public void CaptureAuthoredPresentation()
        {
            EnsureHierarchy();

            if (baseTintImage != null)
            {
                baseTintSprite = baseTintImage.sprite;
                baseTintImageType = baseTintImage.type;
                baseTintPixelsPerUnitMultiplier = Mathf.Max(0.01f, baseTintImage.pixelsPerUnitMultiplier);
                baseTintColor = baseTintImage.color;
                baseTintBlocksRaycasts = baseTintImage.raycastTarget;
            }

            if (scanlineOverlay != null)
            {
                scanlineTexture = scanlineOverlay.texture;
                scanlineColor = scanlineOverlay.color;
                Vector2 size = scanlineOverlay.uvRect.size;
                if (size.x > 0.001f && size.y > 0.001f)
                    scanlineTiling = size;
                scanlinesEnabled = scanlineOverlay.gameObject.activeSelf && scanlineTexture != null;
            }

            capturedAuthoredPresentation = true;
        }

        public void ApplyPresentation()
        {
            if (respectAuthoredPresentation)
            {
                // Keep authored look. Only enforce raycast flag used by overlay show/hide.
                if (baseTintImage != null)
                    baseTintImage.raycastTarget = baseTintBlocksRaycasts;
                return;
            }

            if (baseTintImage != null)
            {
                baseTintImage.sprite = baseTintSprite;
                baseTintImage.type = baseTintImageType;
                baseTintImage.pixelsPerUnitMultiplier = baseTintPixelsPerUnitMultiplier;
                baseTintImage.color = baseTintColor;
                baseTintImage.raycastTarget = baseTintBlocksRaycasts;
            }

            if (scanlineOverlay != null)
            {
                scanlineOverlay.texture = scanlineTexture;
                scanlineOverlay.color = scanlineColor;
                scanlineOverlay.raycastTarget = false;
                scanlineOverlay.uvRect = new Rect(Vector2.zero, ResolveScanlineTiling());
                scanlineOverlay.gameObject.SetActive(scanlinesEnabled && scanlineTexture != null);
            }
        }

        private void EnsureHierarchy()
        {
            if (transform is RectTransform rootRect)
                PlayerMenuUiFactory.Stretch(rootRect, 0f, 0f, 0f, 0f);

            transform.SetAsFirstSibling();

            if (baseTintImage == null)
                baseTintImage = transform.Find("BaseTint")?.GetComponent<Image>();

            if (baseTintImage == null)
            {
                baseTintImage = PlayerMenuUiFactory.CreateImage("BaseTint", transform, baseTintColor);
                PlayerMenuUiFactory.Stretch(baseTintImage.rectTransform, 0f, 0f, 0f, 0f);
            }

            baseTintImage.transform.SetAsFirstSibling();

            if (scanlineOverlay == null)
                scanlineOverlay = transform.Find("ScanlineOverlay")?.GetComponent<RawImage>();

            if (scanlineOverlay == null)
            {
                GameObject scanlines = new GameObject(
                    "ScanlineOverlay",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(RawImage));
                scanlines.transform.SetParent(transform, false);
                scanlineOverlay = scanlines.GetComponent<RawImage>();
                PlayerMenuUiFactory.Stretch(scanlineOverlay.rectTransform, 0f, 0f, 0f, 0f);
            }

            scanlineOverlay.transform.SetAsLastSibling();
        }

        private Vector2 ResolveScanlineTiling()
        {
            return new Vector2(
                Mathf.Max(0.01f, scanlineTiling.x),
                Mathf.Max(0.01f, scanlineTiling.y));
        }
    }
}
