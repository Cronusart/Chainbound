using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatMoveInfoPanelUGUI : MonoBehaviour
    {
        public struct ChemistryRowData
        {
            public Sprite defenderIcon;
            public string defenderLabel;
            public string reactionSummary;
        }

        [Header("References")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image typeIconImage;
        [SerializeField] private TMP_Text typeLabelText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text chemistryHeaderText;
        [SerializeField] private RectTransform chemistryListRoot;
        [SerializeField] private TMP_Text noChemistryText;
        [SerializeField] private TMP_Text modifierDetailsText;
        [SerializeField] private TMP_Text footerPromptText;

        private readonly List<ChemistryRowView> _chemistryRows = new List<ChemistryRowView>(16);

        private sealed class ChemistryRowView
        {
            public RectTransform root;
            public Image defenderIcon;
            public TMP_Text defenderLabel;
            public TMP_Text reactionText;
        }

        private void Awake()
        {
            EnsureBuilt();
            SetVisible(false);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureBuilt();
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            if (root != null)
                root.gameObject.SetActive(visible);
        }

        public void SetContent(string title, string description, string modifierDetails, string footer = null)
        {
            EnsureBuilt();

            if (titleText != null)
                titleText.text = string.IsNullOrEmpty(title) ? "Move Info" : title;
            if (descriptionText != null)
                descriptionText.text = string.IsNullOrEmpty(description) ? "No description available for this move." : description;
            if (modifierDetailsText != null)
                modifierDetailsText.text = string.IsNullOrEmpty(modifierDetails) ? "No modifier details." : modifierDetails;
            if (footerPromptText != null)
                footerPromptText.text = string.IsNullOrEmpty(footer) ? "Cancel=Close" : footer;
        }

        public void SetTypeSummary(Sprite typeIcon, string typeLabel)
        {
            EnsureBuilt();
            if (typeIconImage != null)
            {
                typeIconImage.sprite = typeIcon;
                typeIconImage.enabled = typeIcon != null;
            }

            if (typeLabelText != null)
                typeLabelText.text = string.IsNullOrEmpty(typeLabel) ? "Type: --" : typeLabel;
        }

        public void SetChemistryRows(IReadOnlyList<ChemistryRowData> rows)
        {
            EnsureBuilt();

            int count = rows != null ? rows.Count : 0;
            EnsureChemistryRowPool(count);

            for (int i = 0; i < _chemistryRows.Count; i++)
            {
                bool show = i < count;
                var row = _chemistryRows[i];
                if (row?.root == null)
                    continue;

                row.root.gameObject.SetActive(show);
                if (!show)
                    continue;

                var data = rows[i];
                if (row.defenderIcon != null)
                {
                    row.defenderIcon.sprite = data.defenderIcon;
                    row.defenderIcon.enabled = data.defenderIcon != null;
                }

                if (row.defenderLabel != null)
                    row.defenderLabel.text = string.IsNullOrEmpty(data.defenderLabel) ? "--" : data.defenderLabel;
                if (row.reactionText != null)
                    row.reactionText.text = string.IsNullOrEmpty(data.reactionSummary) ? "-" : data.reactionSummary;
            }

            if (noChemistryText != null)
                noChemistryText.gameObject.SetActive(count == 0);
        }

        private void EnsureBuilt()
        {
            if (root == null)
                root = transform as RectTransform;
            if (root == null)
                return;

            if (GetComponent<CanvasRenderer>() == null)
                gameObject.AddComponent<CanvasRenderer>();

            var dimmer = GetComponent<Image>();
            bool createdDimmer = false;
            if (dimmer == null)
            {
                dimmer = gameObject.AddComponent<Image>();
                createdDimmer = true;
            }
            if (createdDimmer)
                dimmer.color = new Color(0f, 0f, 0f, 0.78f);
            dimmer.raycastTarget = false;

            var modal = EnsureRect("Modal", root, 0.12f, 0.08f, 0.88f, 0.92f);
            var modalImage = modal.GetComponent<Image>();
            bool createdModalImage = false;
            if (modalImage == null)
            {
                modalImage = modal.gameObject.AddComponent<Image>();
                createdModalImage = true;
            }
            if (createdModalImage)
                modalImage.color = new Color(0.08f, 0.11f, 0.15f, 0.98f);
            modalImage.raycastTarget = false;

            titleText = EnsureText("TitleText", modal, 0.03f, 0.90f, 0.97f, 0.98f, 28, TextAlignmentOptions.Left, Color.white, "Move Info");
            typeIconImage = EnsureImage("TypeIcon", modal, 0.03f, 0.82f, 0.10f, 0.89f, new Color(1f, 1f, 1f, 0f));
            if (typeIconImage != null)
                typeIconImage.preserveAspect = true;
            typeLabelText = EnsureText("TypeLabelText", modal, 0.11f, 0.82f, 0.97f, 0.89f, 20, TextAlignmentOptions.Left, new Color(0.82f, 0.93f, 0.98f, 1f), "Type: --");

            descriptionText = EnsureText("DescriptionText", modal, 0.03f, 0.56f, 0.97f, 0.80f, 20, TextAlignmentOptions.TopLeft, new Color(0.90f, 0.92f, 0.94f, 1f), "No description available for this move.");
            if (descriptionText != null)
                descriptionText.enableWordWrapping = true;

            chemistryHeaderText = EnsureText("ChemistryHeader", modal, 0.03f, 0.50f, 0.97f, 0.56f, 20, TextAlignmentOptions.Left, new Color(0.85f, 0.93f, 0.98f, 1f), "Type Reactions");
            chemistryListRoot = EnsureRect("ChemistryList", modal, 0.03f, 0.19f, 0.97f, 0.49f);
            EnsureVerticalListLayout(chemistryListRoot);
            noChemistryText = EnsureText("NoChemistryText", modal, 0.03f, 0.28f, 0.97f, 0.45f, 18, TextAlignmentOptions.Center, new Color(0.78f, 0.84f, 0.90f, 0.95f), "No special chemistry reactions");

            modifierDetailsText = EnsureText("ModifierDetailsText", modal, 0.03f, 0.11f, 0.97f, 0.18f, 16, TextAlignmentOptions.TopLeft, new Color(0.78f, 0.86f, 0.93f, 1f), "No modifier details.");
            if (modifierDetailsText != null)
                modifierDetailsText.enableWordWrapping = true;
            footerPromptText = EnsureText("FooterPrompt", modal, 0.03f, 0.03f, 0.97f, 0.10f, 18, TextAlignmentOptions.Center, new Color(0.84f, 0.92f, 0.96f, 1f), "Cancel=Close");
        }

        private void EnsureChemistryRowPool(int count)
        {
            while (_chemistryRows.Count < count)
            {
                var rowGo = new GameObject($"ChemRow_{_chemistryRows.Count:D2}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                var rowRect = rowGo.GetComponent<RectTransform>();
                rowRect.SetParent(chemistryListRoot, false);
                rowRect.localScale = Vector3.one;

                var rowBg = rowGo.GetComponent<Image>();
                rowBg.raycastTarget = false;
                rowBg.color = new Color(0.10f, 0.14f, 0.19f, 0.74f);

                var layout = rowGo.GetComponent<LayoutElement>();
                layout.minHeight = 30f;
                layout.preferredHeight = 30f;

                var icon = EnsureImage("DefenderTypeIcon", rowRect, 0.01f, 0.12f, 0.08f, 0.88f, new Color(1f, 1f, 1f, 0f));
                if (icon != null)
                    icon.preserveAspect = true;
                var defender = EnsureText("DefenderTypeLabel", rowRect, 0.10f, 0f, 0.29f, 1f, 16, TextAlignmentOptions.Left, Color.white, "--");
                var reaction = EnsureText("ReactionSummaryLabel", rowRect, 0.30f, 0f, 0.99f, 1f, 15, TextAlignmentOptions.Left, new Color(0.86f, 0.90f, 0.94f, 1f), "-");

                _chemistryRows.Add(new ChemistryRowView
                {
                    root = rowRect,
                    defenderIcon = icon,
                    defenderLabel = defender,
                    reactionText = reaction,
                });
            }
        }

        private static void EnsureVerticalListLayout(RectTransform rect)
        {
            if (rect == null)
                return;

            var vertical = rect.GetComponent<VerticalLayoutGroup>();
            bool createdVertical = false;
            if (vertical == null)
            {
                vertical = rect.gameObject.AddComponent<VerticalLayoutGroup>();
                createdVertical = true;
            }
            if (createdVertical)
            {
                vertical.childControlWidth = true;
                vertical.childControlHeight = false;
                vertical.childForceExpandWidth = true;
                vertical.childForceExpandHeight = false;
                vertical.spacing = 4f;
                vertical.padding = new RectOffset(0, 0, 0, 0);
            }

            var fitter = rect.GetComponent<ContentSizeFitter>();
            bool createdFitter = false;
            if (fitter == null)
            {
                fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
                createdFitter = true;
            }
            if (createdFitter)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private static RectTransform EnsureRect(string name, Transform parent, float minX, float minY, float maxX, float maxY)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null)
                return existing;

            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static TMP_Text EnsureText(string name, Transform parent, float minX, float minY, float maxX, float maxY, int size, TextAlignmentOptions align, Color color, string value)
        {
            var rect = EnsureRect(name, parent, minX, minY, maxX, maxY);
            var text = rect.GetComponent<TMP_Text>();
            bool createdText = false;
            if (text == null)
            {
                text = rect.gameObject.AddComponent<TextMeshProUGUI>();
                createdText = true;
            }
            if (createdText)
            {
                text.fontSize = size;
                text.alignment = align;
                text.color = color;
            }
            text.raycastTarget = false;
            if (string.IsNullOrEmpty(text.text))
                text.text = value;
            return text;
        }

        private static Image EnsureImage(string name, Transform parent, float minX, float minY, float maxX, float maxY, Color color)
        {
            var rect = EnsureRect(name, parent, minX, minY, maxX, maxY);
            var image = rect.GetComponent<Image>();
            bool createdImage = false;
            if (image == null)
            {
                image = rect.gameObject.AddComponent<Image>();
                createdImage = true;
            }
            if (createdImage)
                image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
