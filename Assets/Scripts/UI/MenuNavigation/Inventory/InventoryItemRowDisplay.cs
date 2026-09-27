using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    [DisallowMultipleComponent]
    public sealed class InventoryItemRowDisplay : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image background;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField] private TMP_Text ownershipLabel;
        [SerializeField] private CanvasGroup coreBadgeGroup;
        [SerializeField] private Image selectionFrame;
        [SerializeField] private Color normalColor = new Color(0.095f, 0.125f, 0.18f, 0.94f);
        [SerializeField] private Color selectedColor = new Color(0.2f, 0.85f, 0.75f, 0.26f);
        [Header("Text Colors")]
        [SerializeField] private Color normalPrimaryTextColor = new Color(0.92f, 0.96f, 1f, 1f);
        [SerializeField] private Color selectedPrimaryTextColor = new Color(0.075f, 0.11f, 0.125f, 1f);
        [SerializeField] private Color normalOwnershipTextColor = new Color(0.64f, 0.72f, 0.82f, 1f);
        [SerializeField] private Color selectedOwnershipTextColor = new Color(0.16f, 0.25f, 0.28f, 1f);
        [SerializeField] private Color coreBadgeColor = new Color(1f, 0.68f, 0.2f, 0.92f);

        public Button Button => button;
        public CanvasGroup CanvasGroup => canvasGroup;
        public MenuInventoryItemStack Stack { get; private set; }
        public Vector2 RestAnchoredPosition { get; private set; }
        private bool isBuilding;

        private void Awake()
        {
            EnsureBuilt();
            CaptureRestPosition();
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            CaptureRestPosition();
        }

        public void Bind(
            MenuInventoryItemStack stack,
            PlayerItemDefinition definition,
            Color categoryColor,
            InventoryItemPresentationEntry presentation = null,
            Sprite fallbackIcon = null)
        {
            EnsureBuilt();
            Stack = stack;

            string displayName = presentation != null && !string.IsNullOrWhiteSpace(presentation.displayNameOverride)
                ? presentation.displayNameOverride
                : !string.IsNullOrWhiteSpace(stack?.displayName)
                ? stack.displayName
                : definition != null
                    ? definition.displayName
                    : stack != null
                        ? stack.itemId
                        : "Unknown Item";

            if (nameLabel != null)
                nameLabel.text = displayName;
            if (quantityLabel != null)
                quantityLabel.text = stack != null ? $"x{Mathf.Max(0, stack.quantity)}" : "x0";
            if (ownershipLabel != null)
                ownershipLabel.text = ResolveOwnershipLabel(stack, definition);
            if (iconImage != null)
            {
                Sprite sprite = presentation != null && presentation.rowIcon != null
                    ? presentation.rowIcon
                    : fallbackIcon;
                iconImage.sprite = sprite;
                iconImage.preserveAspect = sprite != null;
                iconImage.color = sprite != null
                    ? Color.white
                    : new Color(categoryColor.r, categoryColor.g, categoryColor.b, 0.92f);
            }

            bool coreBacked = stack != null &&
                (stack.ownershipKind == PlayerItemOwnershipKind.CoreCollectible ||
                 stack.ownershipKind == PlayerItemOwnershipKind.CoreCosmetic ||
                 !string.IsNullOrWhiteSpace(stack.coreAssetId));
            if (coreBadgeGroup != null)
            {
                coreBadgeGroup.alpha = coreBacked ? 1f : 0f;
                coreBadgeGroup.interactable = false;
                coreBadgeGroup.blocksRaycasts = false;
            }
        }

        public void SetSelected(bool selected, bool animate)
        {
            if (!isBuilding)
                EnsureBuilt();

            ApplySelectedState(selected, animate);
        }

        private void ApplySelectedState(bool selected, bool animate)
        {
            if (background != null)
                background.color = selected ? selectedColor : normalColor;

            ApplyTextColors(selected, animate);

            if (selectionFrame != null)
            {
                selectionFrame.enabled = true;
                selectionFrame.DOKill();
                Color target = selectionFrame.color;
                target.a = selected ? 0.5f : 0f;
                if (animate && Application.isPlaying)
                {
                    selectionFrame.DOColor(target, 0.12f)
                        .SetEase(Ease.OutSine)
                        .SetUpdate(true);
                }
                else
                {
                    selectionFrame.color = target;
                }
            }
        }

        private void ApplyTextColors(bool selected, bool animate)
        {
            Color primaryColor = selected ? selectedPrimaryTextColor : normalPrimaryTextColor;
            Color ownershipColor = selected ? selectedOwnershipTextColor : normalOwnershipTextColor;
            ApplyTextColor(nameLabel, primaryColor, animate);
            ApplyTextColor(quantityLabel, primaryColor, animate);
            ApplyTextColor(ownershipLabel, ownershipColor, animate);
        }

        private static void ApplyTextColor(TMP_Text label, Color color, bool animate)
        {
            if (label == null)
                return;

            label.DOKill();
            if (animate && Application.isPlaying)
            {
                label.DOColor(color, 0.12f)
                    .SetEase(Ease.OutSine)
                    .SetUpdate(true);
            }
            else
            {
                label.color = color;
            }
        }

        public void CaptureRestPosition()
        {
            RectTransform rect = transform as RectTransform;
            RestAnchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
        }

        private void EnsureBuilt()
        {
            if (isBuilding)
                return;

            isBuilding = true;
            try
            {
                if (button == null)
                    button = GetComponent<Button>();
                if (button == null)
                    button = gameObject.AddComponent<Button>();

                if (canvasGroup == null)
                    canvasGroup = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(gameObject);

                if (background == null)
                    background = GetComponent<Image>();
                if (background == null)
                    background = gameObject.AddComponent<Image>();
                background.color = normalColor;
                button.targetGraphic = background;

                RectTransform rect = transform as RectTransform;
                if (rect != null && rect.sizeDelta == Vector2.zero)
                    rect.sizeDelta = new Vector2(720f, 72f);

                if (selectionFrame == null)
                {
                    Transform existing = transform.Find("SelectionFrame");
                    selectionFrame = existing != null ? existing.GetComponent<Image>() : null;
                }

                if (selectionFrame == null)
                {
                    selectionFrame = PlayerMenuUiFactory.CreateImage("SelectionFrame", transform, selectedColor);
                    PlayerMenuUiFactory.Stretch(selectionFrame.rectTransform);
                    selectionFrame.raycastTarget = false;
                }

                EnsureIcon();
                EnsureLabels();
                EnsureCoreBadge();
                ApplySelectedState(false, false);
            }
            finally
            {
                isBuilding = false;
            }
        }

        private void EnsureIcon()
        {
            bool created = false;
            if (iconImage == null)
            {
                Transform existing = transform.Find("TypeIcon");
                if (existing != null && existing != transform)
                    iconImage = existing.GetComponent<Image>();
            }

            if (iconImage == null)
            {
                iconImage = PlayerMenuUiFactory.CreateImage("TypeIcon", transform, new Color(0.2f, 0.85f, 0.75f, 0.92f));
                created = true;
            }

            RectTransform rect = iconImage.rectTransform;
            if (created || rect.sizeDelta == Vector2.zero)
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(38f, 0f);
                rect.sizeDelta = new Vector2(38f, 38f);
            }

            iconImage.raycastTarget = false;
        }

        private void EnsureLabels()
        {
            bool createdName = nameLabel == null && transform.Find("NameLabel") == null;
            bool createdQuantity = quantityLabel == null && transform.Find("QuantityLabel") == null;
            bool createdOwnership = ownershipLabel == null && transform.Find("OwnershipLabel") == null;

            if (nameLabel == null)
                nameLabel = FindOrCreateLabel("NameLabel", 24f, TextAlignmentOptions.MidlineLeft);
            if (quantityLabel == null)
                quantityLabel = FindOrCreateLabel("QuantityLabel", 23f, TextAlignmentOptions.MidlineRight);
            if (ownershipLabel == null)
                ownershipLabel = FindOrCreateLabel("OwnershipLabel", 16f, TextAlignmentOptions.MidlineLeft);

            if (createdName)
            {
                RectTransform nameRect = nameLabel.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 0.45f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.offsetMin = new Vector2(78f, 0f);
                nameRect.offsetMax = new Vector2(-110f, -4f);
            }

            if (createdQuantity)
            {
                RectTransform qtyRect = quantityLabel.rectTransform;
                qtyRect.anchorMin = new Vector2(1f, 0f);
                qtyRect.anchorMax = new Vector2(1f, 1f);
                qtyRect.pivot = new Vector2(1f, 0.5f);
                qtyRect.anchoredPosition = new Vector2(-18f, 0f);
                qtyRect.sizeDelta = new Vector2(90f, 0f);
            }

            if (createdOwnership)
            {
                RectTransform ownRect = ownershipLabel.rectTransform;
                ownRect.anchorMin = new Vector2(0f, 0f);
                ownRect.anchorMax = new Vector2(1f, 0.52f);
                ownRect.offsetMin = new Vector2(78f, 4f);
                ownRect.offsetMax = new Vector2(-150f, 0f);
            }

            ApplyTextColors(false, false);
        }

        private TMP_Text FindOrCreateLabel(string childName, float size, TextAlignmentOptions alignment)
        {
            Transform existing = transform.Find(childName);
            TMP_Text label = existing != null ? existing.GetComponent<TMP_Text>() : null;
            if (label == null)
                label = PlayerMenuUiFactory.CreateText(childName, transform, "", size, alignment, false);

            label.raycastTarget = false;
            return label;
        }

        private void EnsureCoreBadge()
        {
            if (coreBadgeGroup == null)
            {
                Transform existing = transform.Find("CoreBadge");
                coreBadgeGroup = existing != null ? existing.GetComponent<CanvasGroup>() : null;
            }

            if (coreBadgeGroup == null)
            {
                Image badge = PlayerMenuUiFactory.CreateImage("CoreBadge", transform, coreBadgeColor);
                RectTransform rect = badge.rectTransform;
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-112f, 0f);
                rect.sizeDelta = new Vector2(58f, 28f);
                TMP_Text text = PlayerMenuUiFactory.CreateText("Label", badge.transform, "Core", 15f, TextAlignmentOptions.Center, false);
                PlayerMenuUiFactory.Stretch(text.rectTransform, 3f, 1f, 3f, 1f);
                coreBadgeGroup = badge.gameObject.AddComponent<CanvasGroup>();
            }
        }

        private static string ResolveOwnershipLabel(MenuInventoryItemStack stack, PlayerItemDefinition definition)
        {
            PlayerItemOwnershipKind ownership = stack != null ? stack.ownershipKind : definition != null ? definition.ownershipKind : PlayerItemOwnershipKind.ProfileStack;
            switch (ownership)
            {
                case PlayerItemOwnershipKind.CoreCollectible:
                    return "Core collectible";
                case PlayerItemOwnershipKind.CoreCosmetic:
                    return "Core cosmetic";
                default:
                    return "Profile stack";
            }
        }
    }
}
