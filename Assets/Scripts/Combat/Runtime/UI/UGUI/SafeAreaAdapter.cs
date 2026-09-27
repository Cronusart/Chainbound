using UnityEngine;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class SafeAreaAdapter : MonoBehaviour
    {
        [SerializeField] private bool applyInUpdate = true;

        private RectTransform _panel;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreenSize;

        private void Awake()
        {
            _panel = transform as RectTransform;
            ApplySafeArea(force: true);
        }

        private void OnEnable()
        {
            ApplySafeArea(force: true);
        }

        private void Update()
        {
            if (applyInUpdate)
                ApplySafeArea(force: false);
        }

        public void ApplySafeArea(bool force)
        {
            if (_panel == null)
                _panel = transform as RectTransform;
            if (_panel == null)
                return;

            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize.x <= 0 || screenSize.y <= 0)
                return;

            if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize)
                return;

            _lastSafeArea = safeArea;
            _lastScreenSize = screenSize;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= screenSize.x;
            anchorMin.y /= screenSize.y;
            anchorMax.x /= screenSize.x;
            anchorMax.y /= screenSize.y;

            _panel.anchorMin = anchorMin;
            _panel.anchorMax = anchorMax;
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
        }
    }
}
