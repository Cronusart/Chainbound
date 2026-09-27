using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Inverse of <see cref="SafeAreaAdapter"/>: expands a <see cref="RectTransform"/>
    /// outward by the current <see cref="Screen.safeArea"/> inset so it covers the full
    /// display even though its parent is safe-area inset.
    ///
    /// Interactive controls belong inside the safe area, but decorative chrome (the
    /// gaming-pad frame) must be full-bleed or a strip of gameplay shows through on the
    /// camera-cutout side. Attaching this to the chrome keeps the safe area as the default
    /// for everything under the HUD root and makes escaping it an explicit opt-out, rather
    /// than depending on authors parenting new groups to the correct branch.
    ///
    /// Requires no <c>RectMask2D</c> / <c>Mask</c> on any ancestor, since the rect
    /// deliberately overflows its parent.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaOverscan : MonoBehaviour
    {
        [Tooltip("Extra expansion in canvas reference units, applied on top of the system-reported inset.")]
        [SerializeField] private RectOffset additionalOverscan = new RectOffset();

        private RectTransform panel;
        private Canvas parentCanvas;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastScreenSize = Vector2Int.zero;
        private float lastScaleFactor;
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            parentCanvas = GetComponentInParent<Canvas>();
            Apply();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            float scaleFactor = ResolveScaleFactor();

            if (Screen.safeArea != lastSafeArea
                || Screen.width != lastScreenSize.x
                || Screen.height != lastScreenSize.y
                || Screen.orientation != lastOrientation
                || !Mathf.Approximately(scaleFactor, lastScaleFactor))
            {
                Apply();
            }
        }

        private float ResolveScaleFactor()
        {
            if (parentCanvas == null)
                parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas == null)
                return 1f;

            float scaleFactor = parentCanvas.scaleFactor;
            return scaleFactor > 0f ? scaleFactor : 1f;
        }

        private void Apply()
        {
            if (panel == null) return;

            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0f || screen.y <= 0f) return;

            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;
            lastScaleFactor = ResolveScaleFactor();

            // Inset thickness on each edge, in physical pixels.
            Rect area = lastSafeArea;
            float insetLeft = Mathf.Max(0f, area.xMin);
            float insetBottom = Mathf.Max(0f, area.yMin);
            float insetRight = Mathf.Max(0f, screen.x - area.xMax);
            float insetTop = Mathf.Max(0f, screen.y - area.yMax);

            // The parent rect is expressed in canvas reference units, so physical pixels
            // must be divided by the CanvasScaler's factor before they can cancel the inset.
            float scale = lastScaleFactor;
            insetLeft /= scale;
            insetBottom /= scale;
            insetRight /= scale;
            insetTop /= scale;

            if (additionalOverscan != null)
            {
                insetLeft += additionalOverscan.left;
                insetBottom += additionalOverscan.bottom;
                insetRight += additionalOverscan.right;
                insetTop += additionalOverscan.top;
            }

            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = new Vector2(-insetLeft, -insetBottom);
            panel.offsetMax = new Vector2(insetRight, insetTop);
        }
    }
}
