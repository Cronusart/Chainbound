using System.Collections.Generic;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Prefab-authored Loadout look card. Play mode never Instantiates children —
    /// author under LoadoutView, then copy Column_1 → 2/3.
    /// Typography matches authored Stats lines (Pixeloid 18, UpperCase).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyLoadoutViewLayout : MonoBehaviour
    {
        [Header("Lines (authored)")]
        [SerializeField] private TMP_Text variantText;
        [SerializeField] private TMP_Text auraText;
        [SerializeField] private TMP_Text partyColumnText;
        [SerializeField] private TMP_Text idleText;
        [SerializeField] private TMP_Text combatFxText;

        [Header("Presentation")]
        [SerializeField] private Color labelColor = new Color(0.78f, 0.82f, 0.88f, 1f);
        [SerializeField] private Color valueColor = Color.white;
        [SerializeField] private Color mutedValueColor = PlayerMenuUiFactory.Muted;

        [Header("Bind Animation (Entry / Stats parity)")]
        [SerializeField] private bool animateOnBind = true;
        [SerializeField, Min(0f)] private float animationStartDelay = 0.12f;
        [SerializeField, Min(0f)] private float itemStaggerSeconds = 0.045f;
        [SerializeField, Min(0f)] private float lineFadeDuration = 0.16f;
        [SerializeField] private float lineStartYOffset = 10f;
        [SerializeField, Min(0f)] private float lineSettleDuration = 0.14f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool killTweensOnDisable = true;

        private Sequence presentationSequence;
        private readonly Dictionary<RectTransform, Vector2> basePositions = new Dictionary<RectTransform, Vector2>();

        public void Bind(ChainlingPartyColumnSummary summary)
        {
            KillPresentation(reset: true);

            if (summary == null || summary.isEmpty)
            {
                BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
                return;
            }

            SetLabeledLine(
                variantText,
                ChainlingPartyLoadoutDisplayUtility.VariantLabel,
                ResolveLineValue(summary, ChainlingPartyLoadoutDisplayUtility.VariantLabel),
                muted: false);
            SetLabeledLine(
                auraText,
                ChainlingPartyLoadoutDisplayUtility.AuraLabel,
                ResolveLineValue(summary, ChainlingPartyLoadoutDisplayUtility.AuraLabel),
                muted: false);
            SetLabeledLine(
                partyColumnText,
                ChainlingPartyLoadoutDisplayUtility.PartyColumnLabel,
                ResolveLineValue(summary, ChainlingPartyLoadoutDisplayUtility.PartyColumnLabel),
                muted: false);
            SetLabeledLine(
                idleText,
                ChainlingPartyLoadoutDisplayUtility.IdleLabel,
                ResolveLineValue(summary, ChainlingPartyLoadoutDisplayUtility.IdleLabel),
                muted: false);
            SetLabeledLine(
                combatFxText,
                ChainlingPartyLoadoutDisplayUtility.CombatFxLabel,
                ResolveLineValue(summary, ChainlingPartyLoadoutDisplayUtility.CombatFxLabel),
                muted: true);

            PlayPresentation();
        }

        public void BindEmpty(string message)
        {
            KillPresentation(reset: true);

            SetLabeledLine(variantText, ChainlingPartyLoadoutDisplayUtility.VariantLabel, "—", muted: false);
            SetLabeledLine(auraText, ChainlingPartyLoadoutDisplayUtility.AuraLabel, "—", muted: false);
            SetLabeledLine(partyColumnText, ChainlingPartyLoadoutDisplayUtility.PartyColumnLabel, "—", muted: false);
            SetLabeledLine(idleText, ChainlingPartyLoadoutDisplayUtility.IdleLabel, "—", muted: false);
            SetLabeledLine(
                combatFxText,
                ChainlingPartyLoadoutDisplayUtility.CombatFxLabel,
                string.IsNullOrWhiteSpace(message) ? "—" : message,
                muted: true);

            ResetPresentationImmediate(visible: true);
        }

        private void OnDisable()
        {
            if (killTweensOnDisable)
                KillPresentation(reset: true);
        }

        private void OnDestroy()
        {
            KillPresentation(reset: false);
        }

        private void PlayPresentation()
        {
            if (!animateOnBind || !Application.isPlaying)
            {
                ResetPresentationImmediate(visible: true);
                return;
            }

            presentationSequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetDelay(animationStartDelay)
                .SetTarget(this);

            int step = 0;
            step = AppendLineReveal(presentationSequence, variantText, step);
            step = AppendLineReveal(presentationSequence, auraText, step);
            step = AppendLineReveal(presentationSequence, partyColumnText, step);
            step = AppendLineReveal(presentationSequence, idleText, step);
            AppendLineReveal(presentationSequence, combatFxText, step);
        }

        private int AppendLineReveal(Sequence sequence, TMP_Text text, int step)
        {
            if (sequence == null || text == null)
                return step;

            RectTransform rect = text.rectTransform;
            CaptureBase(rect);
            Vector2 basePos = basePositions[rect];
            float at = step * itemStaggerSeconds;

            CanvasGroup group = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(text.gameObject);
            group.alpha = 0f;
            rect.anchoredPosition = basePos + new Vector2(0f, lineStartYOffset);

            sequence.Insert(at, group.DOFade(1f, lineFadeDuration).SetEase(Ease.OutSine).SetTarget(group));
            sequence.Insert(
                at,
                rect.DOAnchorPosY(basePos.y, lineSettleDuration).SetEase(Ease.OutSine).SetTarget(rect));
            return step + 1;
        }

        private static string ResolveLineValue(ChainlingPartyColumnSummary summary, string label)
        {
            if (summary?.lines == null)
                return "—";

            for (int i = 0; i < summary.lines.Count; i++)
            {
                if (summary.lines[i].Label == label)
                    return string.IsNullOrEmpty(summary.lines[i].Value) ? "—" : summary.lines[i].Value;
            }

            return "—";
        }

        private void CaptureBase(RectTransform rect)
        {
            if (rect == null)
                return;

            if (!basePositions.ContainsKey(rect))
                basePositions[rect] = rect.anchoredPosition;
        }

        private void ResetPresentationImmediate(bool visible)
        {
            ResetLineImmediate(variantText, visible);
            ResetLineImmediate(auraText, visible);
            ResetLineImmediate(partyColumnText, visible);
            ResetLineImmediate(idleText, visible);
            ResetLineImmediate(combatFxText, visible);
        }

        private void ResetLineImmediate(TMP_Text text, bool visible)
        {
            if (text == null)
                return;

            CanvasGroup group = text.GetComponent<CanvasGroup>();
            if (group != null)
                group.alpha = visible ? 1f : 0f;

            RectTransform rect = text.rectTransform;
            if (basePositions.TryGetValue(rect, out Vector2 pos))
                rect.anchoredPosition = pos;
        }

        private void KillPresentation(bool reset)
        {
            if (presentationSequence != null && presentationSequence.IsActive())
                presentationSequence.Kill(false);
            presentationSequence = null;

            if (reset)
                ResetPresentationImmediate(visible: true);
        }

        private void SetLabeledLine(TMP_Text text, string label, string value, bool muted)
        {
            if (text == null)
                return;

            text.richText = true;
            Color valueTint = muted ? mutedValueColor : valueColor;
            text.text = FormatLabeledLine(label, value, labelColor, valueTint);
        }

        private static string FormatLabeledLine(
            string label,
            string value,
            Color labelTint,
            Color valueTint)
        {
            string safeLabel = EscapeRichText(label ?? "");
            string safeValue = EscapeRichText(value ?? "—");
            return
                $"<color=#{ColorUtility.ToHtmlStringRGBA(labelTint)}>{safeLabel}:</color> " +
                $"<color=#{ColorUtility.ToHtmlStringRGBA(valueTint)}>{safeValue}</color>";
        }

        private static string EscapeRichText(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                return raw;

            return raw.Replace("<", string.Empty).Replace(">", string.Empty);
        }
    }
}
