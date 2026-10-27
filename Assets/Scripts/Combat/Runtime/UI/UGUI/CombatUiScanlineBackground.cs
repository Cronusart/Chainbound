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
        [SerializeField, Tooltip("Clips the scanline RawImage to the interior of a sibling Frame. The Frame graphic stays in front.")]
        private bool clipInsideFrame;

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
            if (scanlineOverlay == null)
                return;

            scanlineOverlay.color = scanlineColor;
            ApplyUvRect(resetPosition: true);
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
            {
                RawImage[] images = GetComponentsInChildren<RawImage>(true);
                for (int i = 0; i < images.Length; i++)
                {
                    if (images[i] != null && images[i].name == OverlayName)
                    {
                        scanlineOverlay = images[i];
                        break;
                    }
                }
            }

            if (scanlineOverlay == null)
                return;

            if (clipInsideFrame && TryClipOverlayInsideFrame())
                return;

            StretchToParent(scanlineOverlay.rectTransform);
            scanlineOverlay.rectTransform.SetAsFirstSibling();
        }

        private bool TryClipOverlayInsideFrame()
        {
            Transform frameTransform = transform.Find("Frame");
            RectTransform frame = frameTransform as RectTransform;
            Image frameImage = frame != null ? frame.GetComponent<Image>() : null;
            if (frame == null)
                return false;

            RectTransform window = EnsureClipWindow();
            int insertIndex = scanlineOverlay.transform.parent == transform
                ? scanlineOverlay.transform.GetSiblingIndex()
                : window.GetSiblingIndex();

            if (scanlineOverlay.transform.parent != window)
                scanlineOverlay.transform.SetParent(window, false);

            StretchToParent(scanlineOverlay.rectTransform);
            FitWindowToFrameInterior(window, frameImage);
            if (window.parent != transform)
                window.SetParent(transform, false);
            window.SetSiblingIndex(Mathf.Clamp(insertIndex, 0, transform.childCount - 1));
            return true;
        }

        private RectTransform EnsureClipWindow()
        {
            Transform existing = transform.Find("ScanlineWindow");
            RectTransform window = existing as RectTransform;
            if (window == null)
            {
                var go = new GameObject("ScanlineWindow", typeof(RectTransform), typeof(RectMask2D));
                go.transform.SetParent(transform, false);
                window = go.GetComponent<RectTransform>();
            }

            if (window.GetComponent<RectMask2D>() == null)
                window.gameObject.AddComponent<RectMask2D>();
            return window;
        }

        private static void FitWindowToFrameInterior(RectTransform window, Image frameImage)
        {
            window.anchorMin = Vector2.zero;
            window.anchorMax = Vector2.one;
            window.pivot = new Vector2(0.5f, 0.5f);
            window.localScale = Vector3.one;
            window.anchoredPosition = Vector2.zero;

            Vector4 inset = FrameBorderInset(frameImage);
            window.offsetMin = new Vector2(inset.x, inset.y);
            window.offsetMax = new Vector2(-inset.z, -inset.w);
        }

        private static Vector4 FrameBorderInset(Image frameImage)
        {
            if (frameImage == null || frameImage.sprite == null)
                return Vector4.zero;

            float pixelsPerUnit = frameImage.pixelsPerUnit * Mathf.Max(0.01f, frameImage.pixelsPerUnitMultiplier);
            if (pixelsPerUnit <= 0.0001f)
                return Vector4.zero;

            return frameImage.sprite.border / pixelsPerUnit;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
    }
}
