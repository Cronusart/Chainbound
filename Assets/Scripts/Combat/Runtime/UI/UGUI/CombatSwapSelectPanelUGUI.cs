using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Chainbound.Combat
{
    [DisallowMultipleComponent]
    public class CombatSwapSelectPanelUGUI : MonoBehaviour
    {
        public struct SwapRowData
        {
            public string label;
            public Sprite typeIcon;
            public bool isAvailable;
        }

        [Header("References")]
        [SerializeField] private RectTransform root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private TMP_Text footerPromptText;

        [Header("Reveal Effect")]
        [SerializeField] private ProjectedPanelReveal projectedReveal;
        [SerializeField] private TMPTypewriterReveal[] typewriters;
        [SerializeField] private float typewriterDelay = 0.25f;
        [SerializeField] private bool debugTypewriter = false;

        private readonly List<RowView> _rows = new List<RowView>(8);
        private Tween _typewriterDelayTween;
        private bool _isRevealing;

        private static readonly Color RowNormal = new Color(0.09f, 0.12f, 0.15f, 0.90f);
        private static readonly Color RowSelected = new Color(0.16f, 0.27f, 0.36f, 0.95f);
        private static readonly Color RowUnavailable = new Color(0.08f, 0.08f, 0.08f, 0.55f);
        private static readonly Color RowUnavailableSelected = new Color(0.10f, 0.10f, 0.10f, 0.70f);
        private static readonly Color LabelAvailable = Color.white;
        private static readonly Color LabelUnavailable = new Color(0.72f, 0.72f, 0.72f, 0.82f);
        private string _emptyStateMessage = "No chainlings available to swap";

        private sealed class RowView
        {
            public Image background;
            public Image icon;
            public TMP_Text label;
        }

        private void Awake()
        {
            EnsureBuilt();
            if (!_isRevealing)
                SetVisible(false);
        }

        private void OnDisable()
        {
            KillTypewriterDelay();
            KillTypewriters();
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                EnsureBuilt();
        }

        public void SetVisible(bool visible)
        {
            EnsureBuilt();
            if (root == null)
                return;

            var effect = projectedReveal != null ? projectedReveal : GetComponent<ProjectedPanelReveal>();

            if (visible)
            {
                _isRevealing = true;
                try
                {
                    if (effect != null)
                    {
                        effect.Reveal();
                        ScheduleTypewriters();
                    }
                    else
                    {
                        root.gameObject.SetActive(true);
                        ScheduleTypewriters();
                    }
                }
                finally
                {
                    _isRevealing = false;
                }
            }
            else
            {
                KillTypewriterDelay();
                KillTypewriters();
                if (effect != null)
                    effect.Hide();
                else
                    root.gameObject.SetActive(false);
            }
        }

        private void ScheduleTypewriters()
        {
            KillTypewriterDelay();
            KillTypewriters();

            var list = ResolveTypewriters();
            if (list == null || list.Count == 0)
            {
                if (debugTypewriter)
                    Debug.Log($"[CombatSwapSelectPanelUGUI] ScheduleTypewriters: no typewriters found (assigned={typewriters?.Length ?? 0}, auto-found=0).");
                return;
            }

            if (debugTypewriter)
                Debug.Log($"[CombatSwapSelectPanelUGUI] ScheduleTypewriters: scheduling {list.Count} typewriter(s) after {typewriterDelay}s delay.");

            foreach (var tw in list)
                if (tw != null) tw.ResetToHidden();

            _typewriterDelayTween = DOVirtual.DelayedCall(typewriterDelay, PlayTypewriters, true);
        }

        private List<TMPTypewriterReveal> ResolveTypewriters()
        {
            var list = new List<TMPTypewriterReveal>();
            if (typewriters != null)
            {
                foreach (var tw in typewriters)
                    if (tw != null) list.Add(tw);
            }
            if (list.Count == 0 && root != null)
            {
                var found = root.GetComponentsInChildren<TMPTypewriterReveal>(true);
                if (found != null && found.Length > 0)
                {
                    list.AddRange(found);
                    if (debugTypewriter)
                        Debug.Log($"[CombatSwapSelectPanelUGUI] ResolveTypewriters: auto-found {found.Length} TMPTypewriterReveal in children.");
                }
            }
            return list;
        }

        private void PlayTypewriters()
        {
            _typewriterDelayTween = null;
            var list = ResolveTypewriters();
            if (list == null || list.Count == 0) return;

            if (debugTypewriter)
                Debug.Log($"[CombatSwapSelectPanelUGUI] PlayTypewriters: playing {list.Count} typewriter(s).");

            foreach (var tw in list)
                if (tw != null) tw.Play();
        }

        private void KillTypewriterDelay()
        {
            if (_typewriterDelayTween != null)
            {
                _typewriterDelayTween.Kill();
                _typewriterDelayTween = null;
            }
        }

        private void KillTypewriters()
        {
            var list = ResolveTypewriters();
            if (list == null) return;
            foreach (var tw in list)
                if (tw != null) tw.Kill();
        }

        public void SetRows(IReadOnlyList<SwapRowData> rows, int selectedIndex)
        {
            EnsureBuilt();
            int count = rows != null ? rows.Count : 0;
            int availableCount = 0;
            EnsureRowPool(count);

            for (int i = 0; i < _rows.Count; i++)
            {
                bool show = i < count;
                var row = _rows[i];
                if (row?.background == null || row.label == null)
                    continue;

                row.background.gameObject.SetActive(show);
                if (!show)
                    continue;

                var item = rows[i];
                row.label.text = string.IsNullOrEmpty(item.label) ? "--" : item.label;
                bool available = item.isAvailable;
                bool selected = i == selectedIndex;
                if (available)
                {
                    availableCount++;
                    row.background.color = selected ? RowSelected : RowNormal;
                    row.label.color = LabelAvailable;
                }
                else
                {
                    row.background.color = selected ? RowUnavailableSelected : RowUnavailable;
                    row.label.color = LabelUnavailable;
                }
                if (row.icon != null)
                {
                    row.icon.sprite = item.typeIcon;
                    row.icon.enabled = item.typeIcon != null;
                }
            }

            if (emptyText != null)
            {
                bool noAvailable = count == 0 || availableCount == 0;
                emptyText.gameObject.SetActive(noAvailable);
                if (noAvailable)
                    emptyText.text = string.IsNullOrEmpty(_emptyStateMessage) ? "No chainlings available to swap" : _emptyStateMessage;
            }
        }

        public void SetFooterPrompt(string text)
        {
            EnsureBuilt();
            if (footerPromptText != null)
                footerPromptText.text = string.IsNullOrEmpty(text) ? "Confirm=Swap   Cancel=Back" : text;
        }

        public void SetEmptyStateMessage(string text)
        {
            _emptyStateMessage = string.IsNullOrEmpty(text) ? "No chainlings available to swap" : text;
        }

        private void EnsureBuilt()
        {
            if (root == null)
                root = transform as RectTransform;
            if (root == null)
                return;

            if (GetComponent<CanvasRenderer>() == null)
                gameObject.AddComponent<CanvasRenderer>();

            var bg = GetComponent<Image>();
            bool createdBackground = false;
            if (bg == null)
            {
                bg = gameObject.AddComponent<Image>();
                createdBackground = true;
            }
            if (createdBackground)
                bg.color = new Color(0.03f, 0.04f, 0.05f, 0.80f);
            bg.raycastTarget = false;

            titleText = EnsureText("TitleText", root, 0.02f, 0.82f, 0.98f, 0.98f, 26, TextAlignmentOptions.Left, Color.white, "Swap Select");
            listRoot = EnsureRect("SwapList", root, 0.02f, 0.18f, 0.98f, 0.80f);
            EnsureVerticalListLayout(listRoot);
            emptyText = EnsureText("EmptyText", root, 0.02f, 0.32f, 0.98f, 0.65f, 22, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.8f, 0.9f), "No swap candidates available");
            footerPromptText = EnsureText("FooterPrompt", root, 0.02f, 0.02f, 0.98f, 0.15f, 18, TextAlignmentOptions.Center, new Color(0.82f, 0.9f, 0.95f, 1f), "Confirm=Swap   Cancel=Back");
        }

        private void EnsureRowPool(int count)
        {
            while (_rows.Count < count)
            {
                var rowGo = new GameObject($"SwapRow_{_rows.Count:D2}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
                var rect = rowGo.GetComponent<RectTransform>();
                rect.SetParent(listRoot, false);
                rect.localScale = Vector3.one;

                var background = rowGo.GetComponent<Image>();
                background.raycastTarget = false;
                background.color = RowNormal;

                var layout = rowGo.GetComponent<LayoutElement>();
                layout.minHeight = 34f;
                layout.preferredHeight = 34f;

                var icon = EnsureImage("TypeIcon", rect, 0.01f, 0.1f, 0.08f, 0.9f, new Color(1f, 1f, 1f, 0f));
                icon.preserveAspect = true;
                var label = EnsureText("Label", rect, 0.10f, 0f, 0.98f, 1f, 20, TextAlignmentOptions.Left, Color.white, "--");

                _rows.Add(new RowView
                {
                    background = background,
                    icon = icon,
                    label = label,
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
                vertical.spacing = 6f;
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
            if (createdText)
                text.textWrappingMode = TextWrappingModes.NoWrap;
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
