using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public enum ChainlingPartySectionFocusTargetKind
    {
        ViewRail,
        Party,
    }

    [DisallowMultipleComponent]
    public sealed class ChainlingPartySectionChrome : MonoBehaviour
    {
        [SerializeField] private RectTransform viewRailRoot;
        [SerializeField] private RectTransform columnsRoot;
        [SerializeField] private Image viewRailSectionFrame;
        [SerializeField] private Image partySectionFrame;
        [SerializeField] private Button viewRailSectionButton;
        [SerializeField] private Button partySectionButton;
        [SerializeField] private Color activeFrameColor = new Color(0.2f, 0.85f, 0.75f, 0.22f);
        [SerializeField] private Color inactiveFrameColor = new Color(0.2f, 0.85f, 0.75f, 0f);
        [SerializeField] private float framePadding = 8f;
        [Tooltip("When enabled, runtime keeps prefab frame rects/sprites instead of copying ViewRail/ColumnsRoot bounds.")]
        [SerializeField] private bool preserveAuthoredFrameLayout = true;
        [Header("Focus Pulse")]
        [SerializeField] private bool enableFocusPulse = true;
        [SerializeField] private float focusPulseMinAlphaMultiplier = 0.5f;
        [SerializeField] private float focusPulseHalfDuration = 0.65f;

        private Tween viewRailPulseTween;
        private Tween partyPulseTween;
        private ChainlingPartyNavigationPhase currentPhase = ChainlingPartyNavigationPhase.PartySection;

        public event System.Action ViewRailSectionSelected;
        public event System.Action PartySectionSelected;

        public Button ViewRailSectionButton => viewRailSectionButton;
        public Button PartySectionButton => partySectionButton;
        public bool HasAuthoringFrames => viewRailSectionFrame != null && partySectionFrame != null;
        public bool UsesAuthoredFrameLayout => HasAuthoringFrames && preserveAuthoredFrameLayout;

        public void Configure(RectTransform viewRail, RectTransform columns)
        {
            if (viewRail != null)
                viewRailRoot = viewRail;
            if (columns != null)
                columnsRoot = columns;
        }

        private ChainlingPartySectionFramePalette ResolveFramePalette()
        {
            if (columnsRoot == null)
                return null;

            return columnsRoot.GetComponent<ChainlingPartySectionFramePalette>();
        }

        private Color ResolveActiveColor(ChainlingPartySectionFocusTargetKind target)
        {
            ChainlingPartySectionFramePalette palette = ResolveFramePalette();
            return palette != null ? palette.GetActiveColor(target) : activeFrameColor;
        }

        private Color ResolveInactiveColor(ChainlingPartySectionFocusTargetKind target)
        {
            ChainlingPartySectionFramePalette palette = ResolveFramePalette();
            return palette != null ? palette.GetInactiveColor(target) : inactiveFrameColor;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt();
            RefreshFrameRects(force: true);
            SetActivePhase(ChainlingPartyNavigationPhase.ViewSection);
        }

        /// <summary>
        /// Wires focus targets/buttons for prefab-authored frames without overwriting layout.
        /// </summary>
        public void PrepareAuthoredRuntime()
        {
            EnsureBuilt();
        }

        public void RefreshFrameRects(bool force = false)
        {
            if (!force && UsesAuthoredFrameLayout)
                return;

            CopyTargetRect(viewRailSectionFrame != null ? viewRailSectionFrame.rectTransform : null, viewRailRoot);
            CopyTargetRect(partySectionFrame != null ? partySectionFrame.rectTransform : null, columnsRoot);
        }

        public void SetActivePhase(ChainlingPartyNavigationPhase phase)
        {
            currentPhase = phase;
            EnsureBuilt();

            bool viewActive = phase == ChainlingPartyNavigationPhase.ViewSection || phase == ChainlingPartyNavigationPhase.ViewEditing;
            bool partyActive = phase == ChainlingPartyNavigationPhase.PartySection || phase == ChainlingPartyNavigationPhase.ColumnEditing;
            bool sectionInputActive = phase == ChainlingPartyNavigationPhase.ViewSection ||
                phase == ChainlingPartyNavigationPhase.PartySection;
            bool viewPulse = phase == ChainlingPartyNavigationPhase.ViewSection;
            bool partyPulse = phase == ChainlingPartyNavigationPhase.PartySection;

            SetFrameState(
                viewRailSectionFrame,
                viewRailSectionButton,
                ChainlingPartySectionFocusTargetKind.ViewRail,
                viewActive,
                sectionInputActive,
                viewPulse,
                ref viewRailPulseTween);
            SetFrameState(
                partySectionFrame,
                partySectionButton,
                ChainlingPartySectionFocusTargetKind.Party,
                partyActive,
                sectionInputActive,
                partyPulse,
                ref partyPulseTween);
        }

        private void OnDisable()
        {
            StopFramePulse(ref viewRailPulseTween, viewRailSectionFrame);
            StopFramePulse(ref partyPulseTween, partySectionFrame);
        }

        private void EnsureBuilt()
        {
            if (viewRailSectionFrame == null)
                viewRailSectionFrame = CreateFrame("ViewRailSectionFrame", out viewRailSectionButton);
            else if (viewRailSectionButton == null)
                viewRailSectionButton = viewRailSectionFrame.GetComponent<Button>();
            ConfigureFocusTarget(viewRailSectionFrame, ChainlingPartySectionFocusTargetKind.ViewRail);

            if (partySectionFrame == null)
                partySectionFrame = CreateFrame("PartySectionFrame", out partySectionButton);
            else if (partySectionButton == null)
                partySectionButton = partySectionFrame.GetComponent<Button>();
            ConfigureFocusTarget(partySectionFrame, ChainlingPartySectionFocusTargetKind.Party);
        }

        private Image CreateFrame(string frameName, out Button button)
        {
            Image frame = PlayerMenuUiFactory.CreateImage(frameName, transform, inactiveFrameColor);
            frame.raycastTarget = true;
            button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            return frame;
        }

        private void ConfigureFocusTarget(Image frame, ChainlingPartySectionFocusTargetKind target)
        {
            if (frame == null)
                return;

            ChainlingPartySectionFocusTarget focusTarget = frame.GetComponent<ChainlingPartySectionFocusTarget>();
            if (focusTarget == null)
                focusTarget = frame.gameObject.AddComponent<ChainlingPartySectionFocusTarget>();
            focusTarget.Configure(this, target);
        }

        public void NotifyFocusTargetSelected(ChainlingPartySectionFocusTargetKind target)
        {
            bool sectionInputActive = currentPhase == ChainlingPartyNavigationPhase.ViewSection ||
                currentPhase == ChainlingPartyNavigationPhase.PartySection;
            if (!sectionInputActive)
                return;

            if (target == ChainlingPartySectionFocusTargetKind.ViewRail)
                ViewRailSectionSelected?.Invoke();
            else
                PartySectionSelected?.Invoke();
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
            ChainlingPartySectionFocusTargetKind target,
            bool visible,
            bool inputActive,
            bool pulse,
            ref Tween pulseTween)
        {
            if (frame == null)
                return;

            Color activeColor = ResolveActiveColor(target);
            Color inactiveColor = ResolveInactiveColor(target);

            frame.enabled = true;
            StopFramePulse(ref pulseTween, frame);

            if (!visible)
            {
                frame.color = inactiveColor;
                frame.raycastTarget = false;
                if (button != null)
                    button.interactable = false;
                return;
            }

            frame.color = activeColor;
            frame.raycastTarget = inputActive;
            if (button != null)
                button.interactable = inputActive;

            if (pulse && enableFocusPulse)
                StartFramePulse(frame, ref pulseTween, activeColor);
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
