using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public static class PartyTabProgressBarUtility
    {
        public static float GetFill01(Image fill)
        {
            if (fill == null)
                return 0f;

            RectTransform rect = fill.rectTransform;
            return Mathf.Clamp01(rect.anchorMax.x);
        }

        public static void ConfigureRectFill(Image fill)
        {
            if (fill == null)
                return;

            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            fill.type = HasSlicedBorder(fill.sprite) ? Image.Type.Sliced : Image.Type.Simple;

            RectTransform rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
        }

        public static void SetFill01(Image fill, float fill01)
        {
            if (fill == null)
                return;

            ConfigureRectFill(fill);

            float clampedFill = Mathf.Clamp01(fill01);
            RectTransform rect = fill.rectTransform;
            rect.anchorMax = new Vector2(clampedFill, rect.anchorMax.y);
            fill.enabled = clampedFill > 0.001f;
        }

        public static Tween DOFill01(Image fill, float targetFill01, float duration)
        {
            if (fill == null)
                return null;

            float startFill = GetFill01(fill);
            return DOVirtual
                .Float(startFill, Mathf.Clamp01(targetFill01), Mathf.Max(0.01f, duration), value =>
                {
                    SetFill01(fill, value);
                })
                .SetTarget(fill);
        }

        private static bool HasSlicedBorder(Sprite sprite)
        {
            if (sprite == null)
                return false;

            Vector4 border = sprite.border;
            return border.x > 0f || border.y > 0f || border.z > 0f || border.w > 0f;
        }
    }
}
