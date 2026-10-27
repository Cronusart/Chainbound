using Chainbound.Combat;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyMoveRowDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image rowBackground;
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private HorizontalLayoutGroup contentLayoutGroup;
        [SerializeField] private Image typeIcon;
        [SerializeField] private TMP_Text moveNameText;
        [SerializeField] private TMP_Text statText;

        [Header("Typography")]
        [SerializeField] private TMP_FontAsset moveNameFont;
        [SerializeField] private TMP_FontAsset statFont;
        [SerializeField] private float moveNameFontSize = 18f;
        [SerializeField] private float statFontSize = 18f;

        [Header("Colors")]
        [SerializeField] private Color moveNameColor = PlayerMenuUiFactory.Text;
        [SerializeField] private Color statColor = PlayerMenuUiFactory.Muted;
        [SerializeField] private Color emptyMoveNameColor = PlayerMenuUiFactory.Muted;

        [Header("Layout")]
        [SerializeField] private LayoutElement layoutElement;
        [SerializeField] private LayoutElement typeIconLayoutElement;
        [SerializeField] private LayoutElement moveNameLayoutElement;
        [SerializeField] private LayoutElement statLayoutElement;
        [SerializeField] private float preferredRowHeight = 30f;
        [SerializeField] private int contentPaddingLeft = 4;
        [SerializeField] private int contentPaddingRight = 4;
        [SerializeField] private int contentPaddingTop;
        [SerializeField] private int contentPaddingBottom;
        [SerializeField] private float contentSpacing = 6f;
        [SerializeField] private TextAnchor contentAlignment = TextAnchor.MiddleLeft;
        [SerializeField] private float typeIconPreferredWidth = 22f;
        [SerializeField] private float typeIconPreferredHeight = 22f;
        [SerializeField] private float moveNameMinWidth = 40f;
        [SerializeField] private float moveNameFlexibleWidth = 1f;
        [SerializeField] private float statPreferredWidth = 44f;
        [SerializeField] private float statMinWidth = 36f;
        [SerializeField] private TextAlignmentOptions moveNameAlignment = TextAlignmentOptions.MidlineLeft;
        [SerializeField] private TextAlignmentOptions statAlignment = TextAlignmentOptions.MidlineRight;

        public Image RowBackground => rowBackground;
        public RectTransform ContentRoot => contentRoot;
        public HorizontalLayoutGroup ContentLayoutGroup => contentLayoutGroup;
        public Image TypeIcon => typeIcon;
        public TMP_Text MoveNameText => moveNameText;
        public TMP_Text StatText => statText;
        public LayoutElement LayoutElement => layoutElement;

        public void ApplyEditorDefaults(TMP_FontAsset font, float rowHeight)
        {
            CaptureLayoutFromChildren();

            if (font != null)
            {
                moveNameFont = font;
                statFont = font;
            }

            preferredRowHeight = rowHeight;
            ApplyTypographyDefaults();
            ApplyLayoutDefaults();
        }

        public void CaptureLayoutFromChildren()
        {
            EnsureReferences();

            if (contentLayoutGroup != null)
            {
                RectOffset padding = contentLayoutGroup.padding;
                contentPaddingLeft = padding != null ? padding.left : 0;
                contentPaddingRight = padding != null ? padding.right : 0;
                contentPaddingTop = padding != null ? padding.top : 0;
                contentPaddingBottom = padding != null ? padding.bottom : 0;
                contentSpacing = contentLayoutGroup.spacing;
                contentAlignment = contentLayoutGroup.childAlignment;
            }

            if (layoutElement != null && layoutElement.preferredHeight > 0f)
                preferredRowHeight = layoutElement.preferredHeight;

            if (typeIconLayoutElement != null)
            {
                typeIconPreferredWidth = ResolvePositive(typeIconLayoutElement.preferredWidth, typeIconPreferredWidth);
                typeIconPreferredHeight = ResolvePositive(typeIconLayoutElement.preferredHeight, typeIconPreferredHeight);
            }

            if (moveNameLayoutElement != null)
            {
                moveNameMinWidth = ResolvePositive(moveNameLayoutElement.minWidth, moveNameMinWidth);
                moveNameFlexibleWidth = Mathf.Max(0f, moveNameLayoutElement.flexibleWidth);
            }

            if (statLayoutElement != null)
            {
                statPreferredWidth = ResolvePositive(statLayoutElement.preferredWidth, statPreferredWidth);
                statMinWidth = ResolvePositive(statLayoutElement.minWidth, statMinWidth);
            }

            if (moveNameText != null)
                moveNameAlignment = moveNameText.alignment;
            if (statText != null)
                statAlignment = statText.alignment;
        }

        public void CopyPresentationFrom(ChainlingPartyMoveRowDisplay source)
        {
            if (source == null || source == this)
                return;

            source.CaptureLayoutFromChildren();
            EnsureReferences();

            moveNameFont = source.moveNameFont;
            statFont = source.statFont;
            moveNameFontSize = source.moveNameFontSize;
            statFontSize = source.statFontSize;
            moveNameColor = source.moveNameColor;
            statColor = source.statColor;
            emptyMoveNameColor = source.emptyMoveNameColor;

            preferredRowHeight = source.preferredRowHeight;
            contentPaddingLeft = source.contentPaddingLeft;
            contentPaddingRight = source.contentPaddingRight;
            contentPaddingTop = source.contentPaddingTop;
            contentPaddingBottom = source.contentPaddingBottom;
            contentSpacing = source.contentSpacing;
            contentAlignment = source.contentAlignment;
            typeIconPreferredWidth = source.typeIconPreferredWidth;
            typeIconPreferredHeight = source.typeIconPreferredHeight;
            moveNameMinWidth = source.moveNameMinWidth;
            moveNameFlexibleWidth = source.moveNameFlexibleWidth;
            statPreferredWidth = source.statPreferredWidth;
            statMinWidth = source.statMinWidth;
            moveNameAlignment = source.moveNameAlignment;
            statAlignment = source.statAlignment;

            CopyImagePresentation(source.rowBackground, rowBackground);
            CopyImagePresentation(source.typeIcon, typeIcon);
            ApplyTypographyDefaults();
            ApplyLayoutDefaults();
        }

        public void ApplySerializedPresentation()
        {
            EnsureReferences();
            ApplyTypographyDefaults();
            ApplyLayoutDefaults();
        }

        public void Bind(
            ChainlingPartyMoveRowSummary row,
            PartyTabMoveRarityPaletteSO rarityPalette,
            TypeIconPaletteSO typeIconPalette)
        {
            EnsureReferences();
            ApplyTypographyDefaults();
            ApplyLayoutDefaults();

            bool empty = row == null || row.isEmpty;
            string moveName = empty
                ? !string.IsNullOrWhiteSpace(row?.moveName) ? row.moveName : "No moves equipped."
                : row.moveName;
            string statLabel = empty ? row?.statLabel ?? "" : row.statLabel;

            if (moveNameText != null)
            {
                moveNameText.text = moveName;
                moveNameText.color = empty ? emptyMoveNameColor : moveNameColor;
            }

            if (statText != null)
            {
                statText.text = statLabel;
                statText.color = empty ? emptyMoveNameColor : statColor;
            }

            if (typeIcon != null)
            {
                Sprite icon = !empty && typeIconPalette != null
                    ? typeIconPalette.GetIcon(row.moveType)
                    : null;
                typeIcon.sprite = icon;
                typeIcon.enabled = icon != null;
                typeIcon.color = Color.white;
            }

            if (rowBackground != null && rarityPalette != null)
            {
                if (rarityPalette.RowBackgroundSprite != null)
                    rowBackground.sprite = rarityPalette.RowBackgroundSprite;
                rowBackground.type = rarityPalette.RowBackgroundImageType;
                rowBackground.pixelsPerUnitMultiplier = rarityPalette.PixelsPerUnitMultiplier;
                rowBackground.color = rarityPalette.ResolveRarityColor(
                    row != null ? row.rarity : RarityTier.Common,
                    empty);
                rowBackground.enabled = rowBackground.sprite != null || empty;
            }

            gameObject.SetActive(true);
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void ApplyTypographyDefaults()
        {
            if (moveNameText != null)
            {
                if (moveNameFont != null)
                    moveNameText.font = moveNameFont;
                if (moveNameFontSize > 0f)
                    moveNameText.fontSize = moveNameFontSize;
                moveNameText.textWrappingMode = TextWrappingModes.NoWrap;
                moveNameText.overflowMode = TextOverflowModes.Ellipsis;
                moveNameText.alignment = moveNameAlignment;
            }

            if (statText != null)
            {
                if (statFont != null)
                    statText.font = statFont;
                if (statFontSize > 0f)
                    statText.fontSize = statFontSize;
                statText.textWrappingMode = TextWrappingModes.NoWrap;
                statText.overflowMode = TextOverflowModes.Ellipsis;
                statText.alignment = statAlignment;
            }
        }

        private void ApplyLayoutDefaults()
        {
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();
            if (layoutElement == null)
                return;

            if (preferredRowHeight > 0f)
            {
                layoutElement.preferredHeight = preferredRowHeight;
                layoutElement.minHeight = preferredRowHeight;
            }

            if (contentLayoutGroup != null)
            {
                contentLayoutGroup.padding = new RectOffset(
                    Mathf.Max(0, contentPaddingLeft),
                    Mathf.Max(0, contentPaddingRight),
                    Mathf.Max(0, contentPaddingTop),
                    Mathf.Max(0, contentPaddingBottom));
                contentLayoutGroup.spacing = Mathf.Max(0f, contentSpacing);
                contentLayoutGroup.childAlignment = contentAlignment;
                contentLayoutGroup.childControlWidth = true;
                contentLayoutGroup.childControlHeight = true;
                contentLayoutGroup.childForceExpandWidth = false;
                contentLayoutGroup.childForceExpandHeight = false;
            }

            ApplyIconLayout();
            ApplyMoveNameLayout();
            ApplyStatLayout();
        }

        private void EnsureReferences()
        {
            if (layoutElement == null)
                layoutElement = GetComponent<LayoutElement>();
            if (rowBackground == null)
                rowBackground = transform.Find("RowBackground")?.GetComponent<Image>();
            if (contentRoot == null)
                contentRoot = transform.Find("Content") as RectTransform;
            if (contentLayoutGroup == null && contentRoot != null)
                contentLayoutGroup = contentRoot.GetComponent<HorizontalLayoutGroup>();
            if (typeIcon == null)
                typeIcon = transform.Find("Content/TypeIcon")?.GetComponent<Image>()
                    ?? transform.Find("TypeIcon")?.GetComponent<Image>();
            if (moveNameText == null)
                moveNameText = transform.Find("Content/MoveName")?.GetComponent<TMP_Text>()
                    ?? transform.Find("MoveName")?.GetComponent<TMP_Text>();
            if (statText == null)
                statText = transform.Find("Content/StatText")?.GetComponent<TMP_Text>()
                    ?? transform.Find("StatText")?.GetComponent<TMP_Text>();
            if (typeIconLayoutElement == null && typeIcon != null)
                typeIconLayoutElement = typeIcon.GetComponent<LayoutElement>();
            if (moveNameLayoutElement == null && moveNameText != null)
                moveNameLayoutElement = moveNameText.GetComponent<LayoutElement>();
            if (statLayoutElement == null && statText != null)
                statLayoutElement = statText.GetComponent<LayoutElement>();
        }

        private void ApplyIconLayout()
        {
            if (typeIcon == null)
                return;
            if (typeIconLayoutElement == null)
                typeIconLayoutElement = typeIcon.GetComponent<LayoutElement>();
            if (typeIconLayoutElement == null)
                typeIconLayoutElement = typeIcon.gameObject.AddComponent<LayoutElement>();

            float width = Mathf.Max(1f, typeIconPreferredWidth);
            float height = Mathf.Max(1f, typeIconPreferredHeight);
            typeIconLayoutElement.preferredWidth = width;
            typeIconLayoutElement.preferredHeight = height;
            typeIconLayoutElement.minWidth = width;
            typeIconLayoutElement.minHeight = height;
            typeIconLayoutElement.flexibleWidth = 0f;
            typeIconLayoutElement.flexibleHeight = 0f;
            typeIcon.preserveAspect = true;
        }

        private void ApplyMoveNameLayout()
        {
            if (moveNameText == null)
                return;
            if (moveNameLayoutElement == null)
                moveNameLayoutElement = moveNameText.GetComponent<LayoutElement>();
            if (moveNameLayoutElement == null)
                moveNameLayoutElement = moveNameText.gameObject.AddComponent<LayoutElement>();

            moveNameLayoutElement.minWidth = Mathf.Max(1f, moveNameMinWidth);
            moveNameLayoutElement.flexibleWidth = Mathf.Max(0f, moveNameFlexibleWidth);
            moveNameLayoutElement.flexibleHeight = 0f;
        }

        private void ApplyStatLayout()
        {
            if (statText == null)
                return;
            if (statLayoutElement == null)
                statLayoutElement = statText.GetComponent<LayoutElement>();
            if (statLayoutElement == null)
                statLayoutElement = statText.gameObject.AddComponent<LayoutElement>();

            statLayoutElement.preferredWidth = Mathf.Max(1f, statPreferredWidth);
            statLayoutElement.minWidth = Mathf.Max(1f, statMinWidth);
            statLayoutElement.flexibleWidth = 0f;
            statLayoutElement.flexibleHeight = 0f;
        }

        private static float ResolvePositive(float value, float fallback)
        {
            return value > 0f ? value : fallback;
        }

        private static void CopyImagePresentation(Image source, Image target)
        {
            if (source == null || target == null)
                return;

            target.sprite = source.sprite;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
            target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            target.color = source.color;
            target.raycastTarget = source.raycastTarget;
        }
    }
}
