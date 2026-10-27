using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public sealed class ChainlingDatabaseChrome
    {
        public Sprite idleSprite;
        public Sprite selectedSprite;
        public TMP_FontAsset font;
        public Texture scanlineTexture;
    }

    /// <summary>
    /// Profile B slots for the Chainling Database.
    /// Anchors match the handheld controller HUD: three upper columns and lower-center copy.
    /// Unregistered rows use a blank mark. The portrait slot stays empty until a species is known.
    /// </summary>
    public static class ChainlingDatabaseLayout
    {
        public const int AuthoredZoneRows = 6;
        public const int AuthoredSpeciesRows = 8;
        public const float RowHeight = 60f;

        public static readonly Vector2 TopLeftMin = new Vector2(0.019f, 0.438f);
        public static readonly Vector2 TopLeftMax = new Vector2(0.316f, 0.951f);
        public static readonly Vector2 TopCenterMin = new Vector2(0.334f, 0.438f);
        public static readonly Vector2 TopCenterMax = new Vector2(0.666f, 0.868f);
        public static readonly Vector2 TopRightMin = new Vector2(0.684f, 0.438f);
        public static readonly Vector2 TopRightMax = new Vector2(0.981f, 0.951f);
        public static readonly Vector2 DialogueMin = new Vector2(0.206f, 0.014f);
        public static readonly Vector2 DialogueMax = new Vector2(0.794f, 0.382f);
        public static readonly Vector2 SheetMin = new Vector2(0.206f, 0.014f);
        public static readonly Vector2 SheetMax = new Vector2(0.794f, 0.951f);

        public const string ZoneContentPath = "TopLeft/Viewport/Content";
        public const string SpeciesContentPath = "TopCenter/Viewport/Content";

        public static void Ensure(Transform root, ChainlingDatabaseChrome chrome)
        {
            if (root == null)
                return;

            if (root.Find("TopLeft") == null)
                Build(root, chrome);
            else
                ApplyChrome(root, chrome);
        }

        static void Build(Transform root, ChainlingDatabaseChrome chrome)
        {
            BuildBackground(root, chrome);
            RectTransform zones = CreateSlot(root, "TopLeft", TopLeftMin, TopLeftMax);
            RectTransform species = CreateSlot(root, "TopCenter", TopCenterMin, TopCenterMax);
            RectTransform file = CreateSlot(root, "TopRight", TopRightMin, TopRightMax);
            RectTransform dialogue = CreateSlot(root, "DialogueLowerCenter", DialogueMin, DialogueMax);
            BuildList(zones, "Zones", "ZoneRow_", AuthoredZoneRows, chrome);
            BuildSpeciesColumn(species, chrome);
            BuildFile(file, chrome);
            BuildDialogue(dialogue, chrome);
            BuildSheet(root, chrome);
        }

        static void ApplyChrome(Transform root, ChainlingDatabaseChrome chrome)
        {
            if (chrome == null)
                return;

            RawImage scanlines = root.Find("DatabaseBackground/ScanlineOverlay")?.GetComponent<RawImage>();
            if (scanlines != null)
            {
                scanlines.raycastTarget = false;
                if (scanlines.texture == null && chrome.scanlineTexture != null)
                {
                    scanlines.texture = chrome.scanlineTexture;
                    scanlines.gameObject.SetActive(true);
                }
            }

            ApplyButtonChrome(root.Find(ZoneContentPath), chrome);
            ApplyButtonChrome(root.Find(SpeciesContentPath), chrome);
            ApplyButtonChrome(root.Find("TopCenter/Header"), chrome);
            ApplyButtonChrome(root.Find("DatabaseSheet"), chrome);
            ApplyFont(root, chrome.font);
        }

        static void BuildBackground(Transform root, ChainlingDatabaseChrome chrome)
        {
            RectTransform background = PlayerMenuUiFactory.CreateRect("DatabaseBackground", root);
            PlayerMenuUiFactory.Stretch(background);
            background.SetAsFirstSibling();

            Image tint = PlayerMenuUiFactory.CreateImage("BaseTint", background, new Color(0.02f, 0.03f, 0.06f, 0.55f));
            PlayerMenuUiFactory.Stretch(tint.rectTransform);
            tint.raycastTarget = false;

            GameObject scanObject = new GameObject("ScanlineOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            scanObject.transform.SetParent(background, false);
            RawImage scanlines = scanObject.GetComponent<RawImage>();
            PlayerMenuUiFactory.Stretch(scanlines.rectTransform);
            scanlines.texture = chrome != null ? chrome.scanlineTexture : null;
            scanlines.color = new Color(1f, 1f, 1f, 0.12f);
            scanlines.raycastTarget = false;
            scanlines.gameObject.SetActive(scanlines.texture != null);
        }

        static RectTransform CreateSlot(Transform root, string name, Vector2 min, Vector2 max)
        {
            RectTransform slot = PlayerMenuUiFactory.CreateRect(name, root);
            slot.anchorMin = min;
            slot.anchorMax = max;
            slot.offsetMin = Vector2.zero;
            slot.offsetMax = Vector2.zero;
            slot.pivot = new Vector2(0.5f, 0.5f);
            Image plate = slot.gameObject.AddComponent<Image>();
            plate.color = PlayerMenuUiFactory.Panel;
            plate.raycastTarget = false;
            return slot;
        }

        static void BuildList(RectTransform slot, string title, string rowPrefix, int rowCount, ChainlingDatabaseChrome chrome)
        {
            TMP_Text header = CreateLabel(slot, "Header", title, 20f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Top(header.rectTransform, 36f, 12f, 12f, 8f);

            RectTransform viewport = CreateViewport(slot, 48f);
            RectTransform content = CreateContent(viewport);
            for (int i = 0; i < rowCount; i++)
                CreateRow(content, rowPrefix + i.ToString("00"), chrome);
        }

        static void BuildSpeciesColumn(RectTransform slot, ChainlingDatabaseChrome chrome)
        {
            RectTransform header = PlayerMenuUiFactory.CreateRect("Header", slot);
            PlayerMenuUiFactory.Top(header, 64f, 8f, 8f, 4f);
            TMP_Text title = CreateLabel(header, "Title", "Species", 20f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Stretch(title.rectTransform, 4f, 0f, 120f, 0f);
            Button filter = CreateRow(header, "FilterButton", chrome);
            RectTransform filterRect = filter.GetComponent<RectTransform>();
            filterRect.anchorMin = new Vector2(1f, 0.5f);
            filterRect.anchorMax = new Vector2(1f, 0.5f);
            filterRect.pivot = new Vector2(1f, 0.5f);
            filterRect.sizeDelta = new Vector2(112f, RowHeight);
            filterRect.anchoredPosition = Vector2.zero;
            TMP_Text filterLabel = filter.GetComponentInChildren<TMP_Text>();
            if (filterLabel != null)
                filterLabel.text = "All";

            TMP_Text empty = CreateLabel(slot, "EmptyLabel", "None logged", 18f, TextAlignmentOptions.Center, chrome);
            PlayerMenuUiFactory.Stretch(empty.rectTransform, 12f, 12f, 12f, 72f);
            empty.gameObject.SetActive(false);

            RectTransform viewport = CreateViewport(slot, 72f);
            RectTransform content = CreateContent(viewport);
            for (int i = 0; i < AuthoredSpeciesRows; i++)
                CreateRow(content, "SpeciesRow_" + i.ToString("00"), chrome);
        }

        static void BuildFile(RectTransform slot, ChainlingDatabaseChrome chrome)
        {
            Image mark = PlayerMenuUiFactory.CreateImage("UnknownMark", slot, new Color(0.42f, 0.46f, 0.5f, 1f));
            RectTransform markRect = mark.rectTransform;
            markRect.anchorMin = new Vector2(0.5f, 1f);
            markRect.anchorMax = new Vector2(0.5f, 1f);
            markRect.pivot = new Vector2(0.5f, 1f);
            markRect.sizeDelta = new Vector2(64f, 64f);
            markRect.anchoredPosition = new Vector2(0f, -16f);
            mark.raycastTarget = false;

            Image well = PlayerMenuUiFactory.CreateImage("PortraitWell", slot, new Color(0.04f, 0.05f, 0.07f, 1f));
            RectTransform wellRect = well.rectTransform;
            wellRect.anchorMin = new Vector2(0.5f, 1f);
            wellRect.anchorMax = new Vector2(0.5f, 1f);
            wellRect.pivot = new Vector2(0.5f, 1f);
            wellRect.sizeDelta = new Vector2(84f, 84f);
            wellRect.anchoredPosition = new Vector2(0f, -12f);
            well.raycastTarget = false;
            well.gameObject.SetActive(false);

            Image portrait = PlayerMenuUiFactory.CreateImage("Portrait", slot, Color.white);
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = wellRect.anchorMin;
            portraitRect.anchorMax = wellRect.anchorMax;
            portraitRect.pivot = wellRect.pivot;
            portraitRect.sizeDelta = new Vector2(72f, 72f);
            portraitRect.anchoredPosition = wellRect.anchoredPosition;
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.enabled = false;

            TMP_Text name = CreateLabel(slot, "Name", "Unregistered", 22f, TextAlignmentOptions.Center, chrome);
            PlayerMenuUiFactory.Top(name.rectTransform, 36f, 12f, 12f, 104f);
            TMP_Text fact1 = CreateLabel(slot, "Fact1", "", 18f, TextAlignmentOptions.Center, chrome);
            PlayerMenuUiFactory.Top(fact1.rectTransform, 28f, 12f, 12f, 144f);
            TMP_Text fact2 = CreateLabel(slot, "Fact2", "", 18f, TextAlignmentOptions.Center, chrome);
            PlayerMenuUiFactory.Top(fact2.rectTransform, 28f, 12f, 12f, 176f);
            TMP_Text fact3 = CreateLabel(slot, "Fact3", "", 18f, TextAlignmentOptions.Center, chrome);
            PlayerMenuUiFactory.Top(fact3.rectTransform, 28f, 12f, 12f, 208f);
        }

        static void BuildDialogue(RectTransform slot, ChainlingDatabaseChrome chrome)
        {
            TMP_Text title = CreateLabel(slot, "Title", "", 22f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Top(title.rectTransform, 32f, 16f, 16f, 12f);
            TMP_Text line1 = CreateLabel(slot, "Line1", "", 18f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Top(line1.rectTransform, 28f, 16f, 16f, 48f);
            TMP_Text line2 = CreateLabel(slot, "Line2", "", 18f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Top(line2.rectTransform, 48f, 16f, 16f, 80f);
            TMP_Text prompt = CreateLabel(slot, "Prompt", ChainlingDatabaseEntryBuilder.PromptBack, 18f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Bottom(prompt.rectTransform, 28f, 16f, 16f, 10f);
        }

        static void BuildSheet(Transform root, ChainlingDatabaseChrome chrome)
        {
            RectTransform sheet = CreateSlot(root, "DatabaseSheet", SheetMin, SheetMax);
            Image plate = sheet.GetComponent<Image>();
            if (plate != null)
            {
                plate.color = PlayerMenuUiFactory.PanelAlt;
                plate.raycastTarget = true;
            }

            TMP_Text title = CreateLabel(sheet, "Title", "", 24f, TextAlignmentOptions.Left, chrome);
            PlayerMenuUiFactory.Top(title.rectTransform, 36f, 20f, 20f, 16f);
            TMP_Text body = CreateLabel(sheet, "Body", "", 18f, TextAlignmentOptions.TopLeft, chrome);
            PlayerMenuUiFactory.Stretch(body.rectTransform, 20f, 84f, 20f, 60f);
            Button dossier = CreateRow(sheet, "DossierButton", chrome);
            RectTransform dossierRect = dossier.GetComponent<RectTransform>();
            dossierRect.anchorMin = new Vector2(0.5f, 0f);
            dossierRect.anchorMax = new Vector2(0.5f, 0f);
            dossierRect.pivot = new Vector2(0.5f, 0f);
            dossierRect.sizeDelta = new Vector2(220f, RowHeight);
            dossierRect.anchoredPosition = new Vector2(0f, 12f);
            TMP_Text dossierLabel = dossier.GetComponentInChildren<TMP_Text>();
            if (dossierLabel != null)
                dossierLabel.text = "Open dossier";
            sheet.gameObject.SetActive(false);
        }

        static RectTransform CreateViewport(RectTransform slot, float topInset)
        {
            RectTransform viewport = PlayerMenuUiFactory.CreateRect("Viewport", slot);
            PlayerMenuUiFactory.Stretch(viewport, 8f, 8f, 8f, topInset);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            ScrollRect scroll = slot.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            return viewport;
        }

        static RectTransform CreateContent(RectTransform viewport)
        {
            RectTransform content = PlayerMenuUiFactory.CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = PlayerMenuUiFactory.VerticalLayout(content.gameObject, 4f);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.GetComponentInParent<ScrollRect>();
            if (scroll != null)
                scroll.content = content;
            return content;
        }

        static Button CreateRow(Transform parent, string name, ChainlingDatabaseChrome chrome)
        {
            Button button = PlayerMenuUiFactory.CreateButton(name, parent, "", PlayerMenuUiFactory.PanelAlt);
            LayoutElement element = PlayerMenuUiFactory.LayoutElement(button.gameObject, preferredHeight: RowHeight);
            element.minHeight = RowHeight;
            element.flexibleWidth = 1f;
            Image image = button.targetGraphic as Image;
            ColorBlock colors = button.colors;
            colors.fadeDuration = 0.08f;
            colors.colorMultiplier = 1f;
            if (chrome != null && chrome.idleSprite != null && image != null)
            {
                image.sprite = chrome.idleSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.selectedColor = Color.white;
                colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            }
            else
            {
                colors.normalColor = new Color(0.45f, 0.48f, 0.52f, 1f);
                colors.highlightedColor = new Color(1f, 0.58f, 0.16f, 1f);
                colors.selectedColor = new Color(1f, 0.58f, 0.16f, 1f);
                colors.pressedColor = new Color(0.9f, 0.42f, 0.08f, 1f);
                if (image != null)
                    image.color = Color.white;
            }

            colors.disabledColor = new Color(0.3f, 0.32f, 0.35f, 0.6f);
            button.colors = colors;
            Navigation navigation = button.navigation;
            navigation.mode = Navigation.Mode.Explicit;
            button.navigation = navigation;
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.fontSize = 18f;
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(12f, 4f, 12f, 4f);
                label.overflowMode = TextOverflowModes.Ellipsis;
                if (chrome != null && chrome.font != null)
                    label.font = chrome.font;
            }

            if (button.GetComponent<ChainlingDatabaseRowSignal>() == null)
                button.gameObject.AddComponent<ChainlingDatabaseRowSignal>();
            return button;
        }

        static TMP_Text CreateLabel(
            Transform parent,
            string name,
            string value,
            float size,
            TextAlignmentOptions alignment,
            ChainlingDatabaseChrome chrome)
        {
            TMP_Text text = PlayerMenuUiFactory.CreateText(name, parent, value, size, alignment, true);
            text.color = PlayerMenuUiFactory.Text;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (chrome != null && chrome.font != null)
                text.font = chrome.font;
            return text;
        }

        static void ApplyButtonChrome(Transform root, ChainlingDatabaseChrome chrome)
        {
            if (root == null || chrome == null || chrome.idleSprite == null)
                return;

            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Image image = buttons[i].targetGraphic as Image;
                if (image == null || image.sprite != null)
                    continue;
                image.sprite = chrome.idleSprite;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
        }

        static void ApplyFont(Transform root, TMP_FontAsset font)
        {
            if (root == null || font == null)
                return;

            TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].font == null)
                    labels[i].font = font;
            }
        }
    }

    public sealed class ChainlingDatabaseRowSignal : MonoBehaviour, ISelectHandler
    {
        public int index;
        public UnityEngine.Events.UnityAction<int> selected;

        public void OnSelect(BaseEventData eventData)
        {
            selected?.Invoke(index);
        }
    }
}
