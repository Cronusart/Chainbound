using UnityEngine;

namespace SignalJam.UI.MobileControls
{
    /// <summary>
    /// Drives a <see cref="RectTransform"/>'s anchors from <see cref="Screen.safeArea"/>
    /// so the mobile HUD never clashes with notches, camera cutouts, or rounded corners.
    /// Attach to the root of the mobile HUD canvas (or a child panel that wraps all
    /// interactive controls). Recalculates on orientation / resolution change.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaAdapter : MonoBehaviour
    {
        [Tooltip("Extra inset in pixels applied on top of the system-reported safe area.")]
        [SerializeField] private RectOffset additionalInset = new RectOffset();

        private RectTransform panel;
        private Rect lastSafeArea = Rect.zero;
        private Vector2Int lastScreenSize = Vector2Int.zero;
        private ScreenOrientation lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != lastSafeArea
                || Screen.width != lastScreenSize.x
                || Screen.height != lastScreenSize.y
                || Screen.orientation != lastOrientation)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (panel == null) return;

            lastSafeArea = Screen.safeArea;
            lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            lastOrientation = Screen.orientation;

            Rect area = lastSafeArea;

            if (additionalInset != null)
            {
                area.xMin += additionalInset.left;
                area.yMin += additionalInset.bottom;
                area.xMax -= additionalInset.right;
                area.yMax -= additionalInset.top;
            }

            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen.x <= 0 || screen.y <= 0) return;

            Vector2 anchorMin = area.position / screen;
            Vector2 anchorMax = (area.position + area.size) / screen;

            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
            panel.anchoredPosition = Vector2.zero;
            panel.sizeDelta = Vector2.zero;
        }
    }
}
