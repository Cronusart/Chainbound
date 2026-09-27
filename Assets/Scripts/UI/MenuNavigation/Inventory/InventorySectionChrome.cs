using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    public enum InventorySectionFocusTarget
    {
        Category = 0,
        List = 1,
        Actions = 2,
    }

    [DisallowMultipleComponent]
    public sealed class InventorySectionChrome : MonoBehaviour
    {
        private static readonly Vector2 LeftPanelAnchor = new Vector2(0f, 0.5f);
        private static readonly Vector2 CenterPanelAnchor = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 RightPanelAnchor = new Vector2(1f, 0.5f);
        private static readonly Vector2 SideFrameSize = new Vector2(350f, 510f);
        private static readonly Vector2 CenterFrameSize = new Vector2(1640f, 1000f);

        [SerializeField] private RectTransform categoryRoot;
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private RectTransform actionsRoot;
        [SerializeField] private Image categorySectionFrame;
        [SerializeField] private Image listSectionFrame;
        [SerializeField] private Image actionsSectionFrame;
        [SerializeField] private Button categorySectionButton;
        [SerializeField] private Button listSectionButton;
        [SerializeField] private Button actionsSectionButton;
        [SerializeField] private Color activeFrameColor = new Color(0.69803923f, 0.7294118f, 0.5647059f, 1f);
        [SerializeField] private Color inactiveFrameColor = new Color(0.69803923f, 0.7294118f, 0.5647059f, 0.48f);
        [SerializeField] private float framePadding = 8f;
        [SerializeField] private bool preserveAuthoredFrameLayout = true;

        [Header("Focus Pulse")]
        [Tooltip("Pulse only while browsing section frames (not while confirmed inside a panel).")]
        [SerializeField] private bool enableFocusPulse = true;
        [SerializeField] private float focusPulseMinAlphaMultiplier = 0.5f;
        [SerializeField] private float focusPulseHalfDuration = 0.65f;

        private Tween categoryPulseTween;
        private Tween listPulseTween;
        private Tween actionsPulseTween;
        private UnityAction categoryClickHandler;
        private UnityAction listClickHandler;
        private UnityAction actionsClickHandler;
        private InventorySectionFocusTarget activeSection = InventorySectionFocusTarget.Category;

        public event System.Action<InventorySectionFocusTarget> SectionSelected;

        public Button CategorySectionButton => categorySectionButton;
        public Button ListSectionButton => listSectionButton;
        public Button ActionsSectionButton => actionsSectionButton;
        public bool HasAuthoringFrames => categorySectionFrame != null && listSectionFrame != null && actionsSectionFrame != null;
        public bool UsesAuthoredFrameLayout => HasAuthoringFrames && preserveAuthoredFrameLayout;

        public void Configure(RectTransform category, RectTransform list, RectTransform actions)
        {
            if (category != null)
                categoryRoot = category;
            if (list != null)
                listRoot = list;
            if (actions != null)
                actionsRoot = actions;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            ApplyDefaultAuthoredFrameLayoutForEditor();
            SetActiveSection(activeSection, true);
        }

        public void ApplyDefaultAuthoredFrameLayoutForEditor()
        {
            EnsureBuilt();
            preserveAuthoredFrameLayout = true;

            ApplyFrameRect(
                categorySectionFrame != null ? categorySectionFrame.rectTransform : null,
                LeftPanelAnchor,
                new Vector2(255f, 166f),
                SideFrameSize);
            ApplyFrameRect(
                listSectionFrame != null ? listSectionFrame.rectTransform : null,
                CenterPanelAnchor,
                new Vector2(0f, -50f),
                CenterFrameSize);
            ApplyFrameRect(
                actionsSectionFrame != null ? actionsSectionFrame.rectTransform : null,
                RightPanelAnchor,
                new Vector2(-255f, 166f),
                SideFrameSize);

            categorySectionFrame?.transform.SetAsLastSibling();
            listSectionFrame?.transform.SetAsLastSibling();
            actionsSectionFrame?.transform.SetAsLastSibling();
        }

        public void PrepareAuthoredRuntime()
        {
            EnsureBuilt();
        }

        public void RefreshFrameRects(bool force = false)
        {
            if (!force && UsesAuthoredFrameLayout)
                return;

            CopyTargetRect(categorySectionFrame != null ? categorySectionFrame.rectTransform : null, categoryRoot);
            CopyTargetRect(listSectionFrame != null ? listSectionFrame.rectTransform : null, listRoot);
            CopyTargetRect(actionsSectionFrame != null ? actionsSectionFrame.rectTransform : null, actionsRoot);
        }

        /// <param name="sectionInputActive">
        /// True while hopping Category↔List frames (buttons receive input + optional pulse).
        /// False while confirmed inside a panel — active frame stays full opacity, no pulse.
        /// </param>
        public void SetActiveSection(InventorySectionFocusTarget section, bool sectionInputActive)
        {
            activeSection = section;
            EnsureBuilt();

            bool pulse = sectionInputActive && enableFocusPulse;
            SetFrameState(
                categorySectionFrame,
                categorySectionButton,
                section == InventorySectionFocusTarget.Category,
                sectionInputActive,
                pulse,
                ref categoryPulseTween);
            SetFrameState(
                listSectionFrame,
                listSectionButton,
                section == InventorySectionFocusTarget.List,
                sectionInputActive,
                pulse,
                ref listPulseTween);
            SetFrameState(
                actionsSectionFrame,
                actionsSectionButton,
                section == InventorySectionFocusTarget.Actions,
                sectionInputActive,
                pulse,
                ref actionsPulseTween);
        }

        private void OnEnable()
        {
            EnsureBuilt();
            WireButtons(true);
        }

        private void OnDisable()
        {
            WireButtons(false);
            StopFramePulse(ref categoryPulseTween, categorySectionFrame);
            StopFramePulse(ref listPulseTween, listSectionFrame);
            StopFramePulse(ref actionsPulseTween, actionsSectionFrame);
        }

        private void WireButtons(bool wire)
        {
            categoryClickHandler ??= () => SectionSelected?.Invoke(InventorySectionFocusTarget.Category);
            listClickHandler ??= () => SectionSelected?.Invoke(InventorySectionFocusTarget.List);
            actionsClickHandler ??= () => SectionSelected?.Invoke(InventorySectionFocusTarget.Actions);

            WireButton(categorySectionButton, categoryClickHandler, wire);
            WireButton(listSectionButton, listClickHandler, wire);
            WireButton(actionsSectionButton, actionsClickHandler, wire);
        }

        private static void WireButton(Button button, UnityAction handler, bool wire)
        {
            if (button == null || handler == null)
                return;

            button.onClick.RemoveListener(handler);
            if (wire)
                button.onClick.AddListener(handler);
        }

        private void EnsureBuilt()
        {
            if (categorySectionFrame == null)
                categorySectionFrame = CreateFrame("CategorySectionFrame", out categorySectionButton);
            else if (categorySectionButton == null)
                categorySectionButton = categorySectionFrame.GetComponent<Button>();
            ConfigureFrame(categorySectionFrame, ref categorySectionButton);

            if (listSectionFrame == null)
                listSectionFrame = CreateFrame("ListSectionFrame", out listSectionButton);
            else if (listSectionButton == null)
                listSectionButton = listSectionFrame.GetComponent<Button>();
            ConfigureFrame(listSectionFrame, ref listSectionButton);

            if (actionsSectionFrame == null)
                actionsSectionFrame = CreateFrame("ActionsSectionFrame", out actionsSectionButton);
            else if (actionsSectionButton == null)
                actionsSectionButton = actionsSectionFrame.GetComponent<Button>();
            ConfigureFrame(actionsSectionFrame, ref actionsSectionButton);
        }

        private Image CreateFrame(string frameName, out Button button)
        {
            Image frame = PlayerMenuUiFactory.CreateImage(frameName, transform, inactiveFrameColor);
            frame.raycastTarget = true;
            button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            return frame;
        }

        private void ConfigureFrame(Image frame, ref Button button)
        {
            if (frame == null)
                return;

            frame.raycastTarget = true;
            if (button == null)
                button = frame.GetComponent<Button>();
            if (button == null)
                button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            // ColorTint Disabled multiplies the Image and dimmed confirmed frames — own color fully.
            button.transition = Selectable.Transition.None;
        }

        private static void ApplyFrameRect(RectTransform frame, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (frame == null)
                return;

            frame.anchorMin = anchor;
            frame.anchorMax = anchor;
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = position;
            frame.sizeDelta = size;
            frame.localScale = Vector3.one;
            frame.localRotation = Quaternion.identity;
        }

        private void CopyTargetRect(RectTransform frame, RectTransform target)
        {
            if (frame == null || target == null)
                return;

            frame.anchorMin = target.anchorMin;
            frame.anchorMax = target.anchorMax;
            frame.pivot = target.pivot;
            frame.anchoredPosition = target.anchoredPosition;
            frame.sizeDelta = target.sizeDelta + new Vector2(framePadding * 2f, framePadding * 2f);
        }

        private void SetFrameState(
            Image frame,
            Button button,
            bool active,
            bool sectionInputActive,
            bool pulse,
            ref Tween pulseTween)
        {
            if (frame == null)
                return;

            StopFramePulse(ref pulseTween, frame);
            frame.enabled = true;
            // Confirmed-in: active frame solid at authored active alpha; others stay dim (not invisible).
            frame.color = active ? activeFrameColor : inactiveFrameColor;
            frame.raycastTarget = sectionInputActive;
            if (button != null)
                button.interactable = sectionInputActive;

            // Pulse only while browsing panels — never while confirmed inside one.
            if (active && pulse)
                StartFramePulse(frame, ref pulseTween, activeFrameColor);
        }

        private void StartFramePulse(Image frame, ref Tween pulseTween, Color baseColor)
        {
            StopFramePulse(ref pulseTween, frame);
            if (frame == null)
                return;

            float minAlpha = baseColor.a * Mathf.Clamp01(focusPulseMinAlphaMultiplier);
            Color minColor = new Color(baseColor.r, baseColor.g, baseColor.b, minAlpha);
            frame.color = baseColor;

            pulseTween = frame
                .DOColor(minColor, Mathf.Max(0.05f, focusPulseHalfDuration))
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetUpdate(true);
        }

        private static void StopFramePulse(ref Tween pulseTween, Image frame)
        {
            if (pulseTween != null && pulseTween.IsActive())
                pulseTween.Kill();

            pulseTween = null;

            if (frame != null)
                frame.DOKill();
        }
    }
}
