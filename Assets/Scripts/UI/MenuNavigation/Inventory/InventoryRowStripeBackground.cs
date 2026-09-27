using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LayoutElement))]
    public sealed class InventoryRowStripeBackground : MonoBehaviour
    {
        [SerializeField, Min(1)] private int minimumStripeCount = 12;
        [SerializeField] private bool autoExpandToViewportHeight = true;
        [SerializeField, Min(1f)] private float rowHeight = 72f;
        [SerializeField, Min(0f)] private float rowSpacing = 8f;
        [SerializeField, Min(0f)] private float leftInset = 10f;
        [SerializeField, Min(0f)] private float rightInset = 10f;
        [SerializeField, Min(0f)] private float topInset = 8f;
        [SerializeField, Min(0f)] private float bottomInset = 8f;
        [SerializeField] private Sprite stripeSprite;
        [SerializeField] private Image.Type stripeImageType = Image.Type.Simple;
        [SerializeField] private Color firstStripeColor = new Color(0.5f, 0.64f, 0.46f, 0.72f);
        [SerializeField] private Color secondStripeColor = new Color(0.26f, 0.44f, 0.34f, 0.72f);

        private readonly List<Image> stripes = new List<Image>(16);

        public void ConfigureFromRows(RectTransform rowsRoot, LayoutElement rowTemplate, VerticalLayoutGroup rowLayout)
        {
            if (rowsRoot != null)
            {
                bool livesInsideRowsRoot = transform.parent == rowsRoot;
                leftInset = livesInsideRowsRoot ? 0f : Mathf.Max(0f, rowsRoot.offsetMin.x);
                rightInset = livesInsideRowsRoot ? 0f : Mathf.Max(0f, -rowsRoot.offsetMax.x);
            }

            if (rowTemplate != null && rowTemplate.preferredHeight > 0f)
                rowHeight = rowTemplate.preferredHeight;

            if (rowLayout != null)
            {
                rowSpacing = Mathf.Max(0f, rowLayout.spacing);
                topInset = Mathf.Max(0f, rowLayout.padding.top);
                bottomInset = Mathf.Max(0f, rowLayout.padding.bottom);
            }

            Rebuild();
        }

        public void Rebuild()
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            EnsureIgnoredByLayout();

            CacheStripes();
            int targetCount = ResolveStripeCount(root);
            EnsureStripeCount(targetCount);

            for (int i = 0; i < stripes.Count; i++)
            {
                Image stripe = stripes[i];
                if (stripe == null)
                    continue;

                bool visible = i < targetCount;
                stripe.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                stripe.name = $"Stripe_{i + 1:00}";
                stripe.raycastTarget = false;
                stripe.sprite = stripeSprite;
                stripe.type = stripeImageType;
                stripe.color = i % 2 == 0 ? firstStripeColor : secondStripeColor;
                ApplyStripeRect(stripe.rectTransform, i);
                stripe.transform.SetSiblingIndex(i);
            }
        }

        private void OnEnable()
        {
            EnsureIgnoredByLayout();
            Rebuild();
        }

        private void Awake()
        {
            EnsureIgnoredByLayout();
        }

        private void OnValidate()
        {
            minimumStripeCount = Mathf.Max(1, minimumStripeCount);
            rowHeight = Mathf.Max(1f, rowHeight);
            rowSpacing = Mathf.Max(0f, rowSpacing);
            leftInset = Mathf.Max(0f, leftInset);
            rightInset = Mathf.Max(0f, rightInset);
            topInset = Mathf.Max(0f, topInset);
            bottomInset = Mathf.Max(0f, bottomInset);

            if (isActiveAndEnabled)
                Rebuild();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled && autoExpandToViewportHeight)
                Rebuild();
        }

        private void EnsureIgnoredByLayout()
        {
            LayoutElement layoutElement = GetComponent<LayoutElement>();
            if (layoutElement != null)
                layoutElement.ignoreLayout = true;
        }

        private int ResolveStripeCount(RectTransform root)
        {
            int count = Mathf.Max(1, minimumStripeCount);
            if (!autoExpandToViewportHeight)
                return count;

            float referenceHeight = root.rect.height;
            if (referenceHeight <= 1f && transform.parent is RectTransform parentRect)
                referenceHeight = parentRect.rect.height;

            float availableHeight = referenceHeight - topInset - bottomInset;
            if (availableHeight <= 1f)
                return count;

            float stride = Mathf.Max(1f, rowHeight + rowSpacing);
            int visibleCount = Mathf.CeilToInt((availableHeight + rowSpacing) / stride);
            return Mathf.Max(count, visibleCount);
        }

        private void EnsureStripeCount(int targetCount)
        {
            for (int i = stripes.Count; i < targetCount; i++)
            {
                Image stripe = PlayerMenuUiFactory.CreateImage($"Stripe_{i + 1:00}", transform, Color.white);
                stripe.raycastTarget = false;
                stripes.Add(stripe);
            }
        }

        private void CacheStripes()
        {
            stripes.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null || !child.name.StartsWith("Stripe_", System.StringComparison.Ordinal))
                    continue;

                Image stripe = child.GetComponent<Image>();
                if (stripe != null)
                    stripes.Add(stripe);
            }
        }

        private void ApplyStripeRect(RectTransform rect, int index)
        {
            if (rect == null)
                return;

            float y = topInset + index * (rowHeight + rowSpacing);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(leftInset, -y - rowHeight);
            rect.offsetMax = new Vector2(-rightInset, -y);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }
    }
}
