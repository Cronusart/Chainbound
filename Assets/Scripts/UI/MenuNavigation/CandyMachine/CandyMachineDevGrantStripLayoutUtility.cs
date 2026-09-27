using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Shared DetailBody dev-grant strip sizing for prefab authoring and runtime rescue.
    /// </summary>
    public static class CandyMachineDevGrantStripLayoutUtility
    {
        public const float StripHeight = 72f;

        public static void ApplyStripRoot(RectTransform strip)
        {
            if (strip == null)
                return;

            strip.anchorMin = new Vector2(0f, 1f);
            strip.anchorMax = new Vector2(1f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = Vector2.zero;
            strip.sizeDelta = new Vector2(0f, StripHeight);

            LayoutElement layout = strip.GetComponent<LayoutElement>();
            if (layout == null)
                layout = strip.gameObject.AddComponent<LayoutElement>();
            layout.ignoreLayout = false;
            layout.minHeight = 64f;
            layout.preferredHeight = StripHeight;
            layout.flexibleHeight = 0f;
        }

        public static void LayoutControl(Component control, float minX, float maxX)
        {
            if (control == null)
                return;

            RectTransform rect = control.transform as RectTransform;
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(minX, 0.12f);
            rect.anchorMax = new Vector2(maxX, 0.88f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        public static void LayoutControl(TMP_Text label, float minX, float maxX) =>
            LayoutControl(label != null ? label.rectTransform : null, minX, maxX);
    }
}
