using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public static class PlayerMenuUiFactory
    {
        public static readonly Color Panel = new Color(0.07f, 0.095f, 0.14f, 0.86f);
        public static readonly Color PanelAlt = new Color(0.095f, 0.125f, 0.18f, 0.94f);
        public static readonly Color Accent = new Color(0.2f, 0.85f, 0.75f, 1f);
        public static readonly Color Text = new Color(0.92f, 0.96f, 1f, 1f);
        public static readonly Color Muted = new Color(0.64f, 0.72f, 0.82f, 1f);

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            var text = CreateText("Label", go.transform, label, 24f, TextAlignmentOptions.Center, false);
            Stretch(text.rectTransform, 8f, 6f, 8f, 6f);
            return button;
        }

        public static TMP_Text CreateText(string name, Transform parent, string value, float size, TextAlignmentOptions alignment, bool wrap = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Text;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        public static HorizontalLayoutGroup HorizontalLayout(GameObject go, float spacing = 8f)
        {
            var layout = GetOrAdd<HorizontalLayoutGroup>(go);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static VerticalLayoutGroup VerticalLayout(GameObject go, float spacing = 8f)
        {
            var layout = GetOrAdd<VerticalLayoutGroup>(go);
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static LayoutElement LayoutElement(
            GameObject go,
            float preferredWidth = -1f,
            float preferredHeight = -1f,
            float flexibleWidth = 0f,
            float flexibleHeight = -1f)
        {
            var element = GetOrAdd<LayoutElement>(go);
            if (preferredWidth >= 0f)
                element.preferredWidth = preferredWidth;
            if (preferredHeight >= 0f)
                element.preferredHeight = preferredHeight;
            element.flexibleWidth = flexibleWidth;
            if (flexibleHeight >= 0f)
                element.flexibleHeight = flexibleHeight;
            return element;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static void Top(RectTransform rt, float height, float left = 0f, float right = 0f, float y = 0f)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -height - y);
            rt.offsetMax = new Vector2(-right, -y);
        }

        public static void Bottom(RectTransform rt, float height, float left = 0f, float right = 0f, float y = 0f)
        {
            if (rt == null) return;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, y);
            rt.offsetMax = new Vector2(-right, y + height);
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            if (component == null)
                component = go.AddComponent<T>();
            return component;
        }
    }
}
