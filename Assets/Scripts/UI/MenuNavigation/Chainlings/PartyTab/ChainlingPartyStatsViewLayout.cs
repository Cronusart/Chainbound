using System.Collections.Generic;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Prefab-authored Stats career card: summary lines + dealt/taken balance bar.
    /// Play mode never Instantiates children — author under StatsView, then copy Column_1 → 2/3.
    /// Bind motion matches Entry lines + Moves/Entry signature pop for the balance bar.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyStatsViewLayout : MonoBehaviour
    {
        [Header("Lines (authored)")]
        [SerializeField] private TMP_Text victoriesText;
        [SerializeField] private TMP_Text enemyKosText;
        [SerializeField] private TMP_Text damageDealtText;
        [SerializeField] private TMP_Text damageTakenText;
        [SerializeField] private TMP_Text xpEarnedText;
        [SerializeField] private TMP_Text timesKodText;

        [Header("Balance Bar (authored)")]
        [SerializeField] private RectTransform balanceBarRoot;
        [SerializeField] private Image balanceTrack;
        [SerializeField] private Image dealtFill;
        [SerializeField] private Image takenFill;
        [SerializeField] private TMP_Text dealtCapLabel;
        [SerializeField] private TMP_Text takenCapLabel;

        [Header("Presentation")]
        [SerializeField] private Color labelColor = new Color(0.78f, 0.82f, 0.88f, 1f);
        [SerializeField] private Color valueColor = Color.white;
        [SerializeField] private Color dealtFillColor = new Color(0.95f, 0.45f, 0.38f, 1f);
        [SerializeField] private Color takenFillColor = new Color(0.35f, 0.62f, 0.95f, 1f);
        [SerializeField] private Color trackColor = new Color(0.12f, 0.16f, 0.22f, 0.92f);

        [Header("Bind Animation (Entry / Moves parity)")]
        [SerializeField] private bool animateOnBind = true;
        [SerializeField, Min(0f)] private float animationStartDelay = 0.12f;
        [SerializeField, Min(0f)] private float itemStaggerSeconds = 0.045f;
        [SerializeField, Min(0f)] private float lineFadeDuration = 0.16f;
        [SerializeField] private float lineStartYOffset = 10f;
        [SerializeField, Min(0f)] private float lineSettleDuration = 0.14f;
        [SerializeField, Min(0f)] private float barFallDuration = 0.18f;
        [SerializeField, Min(0f)] private float barSettleDuration = 0.12f;
        [SerializeField] private float barStartYOffset = 14f;
        [SerializeField] private float barOvershootYOffset = -3f;
        [SerializeField] private float barScaleOvershootY = 1.04f;
        [SerializeField, Min(0f)] private float barFillDuration = 0.42f;
        [SerializeField] private Ease barFillEase = Ease.OutCubic;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool killTweensOnDisable = true;

        private Sequence presentationSequence;
        private Tween barTween;
        private readonly Dictionary<RectTransform, Vector2> basePositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> baseScales = new Dictionary<RectTransform, Vector3>();
        private float targetDealtRatio;
        private bool balanceBarEmpty;

        public void Bind(ChainlingPartyColumnSummary summary)
        {
            KillPresentation(reset: true);

            if (summary == null || summary.isEmpty)
            {
                BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
                return;
            }

            SetLabeledLine(
                victoriesText,
                ChainlingPartyStatsDisplayUtility.VictoriesLabel,
                ResolveLineValue(summary, ChainlingPartyStatsDisplayUtility.VictoriesLabel));
            SetLabeledLine(
                enemyKosText,
                ChainlingPartyStatsDisplayUtility.EnemyKosLabel,
                ResolveLineValue(summary, ChainlingPartyStatsDisplayUtility.EnemyKosLabel));
            SetLabeledLine(
                damageDealtText,
                ChainlingPartyStatsDisplayUtility.DamageDealtLabel,
                ChainlingPartyStatsDisplayUtility.FormatStatNumber(summary.damageDealt));
            SetLabeledLine(
                damageTakenText,
                ChainlingPartyStatsDisplayUtility.DamageTakenLabel,
                ChainlingPartyStatsDisplayUtility.FormatStatNumber(summary.damageTaken));
            SetLabeledLine(
                xpEarnedText,
                ChainlingPartyStatsDisplayUtility.XpEarnedLabel,
                ResolveLineValue(summary, ChainlingPartyStatsDisplayUtility.XpEarnedLabel));
            SetLabeledLine(
                timesKodText,
                ChainlingPartyStatsDisplayUtility.TimesKodLabel,
                ResolveLineValue(summary, ChainlingPartyStatsDisplayUtility.TimesKodLabel));

            targetDealtRatio = ComputeDealtRatio(summary.damageDealt, summary.damageTaken);
            balanceBarEmpty = summary.damageDealt <= 0 && summary.damageTaken <= 0;
            ApplyBalanceBarColors();
            SetCapLabelsVisible(!balanceBarEmpty);

            PlayPresentation();
        }

        public void BindEmpty(string message)
        {
            KillPresentation(reset: true);

            SetLabeledLine(victoriesText, ChainlingPartyStatsDisplayUtility.VictoriesLabel, "—");
            SetLabeledLine(enemyKosText, ChainlingPartyStatsDisplayUtility.EnemyKosLabel, "—");
            SetLabeledLine(damageDealtText, ChainlingPartyStatsDisplayUtility.DamageDealtLabel, "—");
            SetLabeledLine(damageTakenText, ChainlingPartyStatsDisplayUtility.DamageTakenLabel, "—");
            SetLabeledLine(xpEarnedText, ChainlingPartyStatsDisplayUtility.XpEarnedLabel, "—");
            SetLabeledLine(
                timesKodText,
                ChainlingPartyStatsDisplayUtility.TimesKodLabel,
                string.IsNullOrWhiteSpace(message) ? "—" : message);

            targetDealtRatio = 0f;
            balanceBarEmpty = true;
            ApplyBalanceBarImmediate(0f, empty: true);
            SetCapLabelsVisible(false);
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
                ApplyBalanceBarImmediate(targetDealtRatio, empty: balanceBarEmpty);
                ResetPresentationImmediate(visible: true);
                return;
            }

            ApplyBalanceBarImmediate(0f, empty: true);

            presentationSequence = DOTween.Sequence()
                .SetUpdate(useUnscaledTime)
                .SetDelay(animationStartDelay)
                .SetTarget(this);

            int step = 0;
            step = AppendLineReveal(presentationSequence, victoriesText, step);
            step = AppendLineReveal(presentationSequence, enemyKosText, step);
            step = AppendLineReveal(presentationSequence, damageDealtText, step);
            step = AppendLineReveal(presentationSequence, damageTakenText, step);
            step = AppendBarReveal(presentationSequence, step);
            step = AppendLineReveal(presentationSequence, xpEarnedText, step);
            AppendLineReveal(presentationSequence, timesKodText, step);
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

        private int AppendBarReveal(Sequence sequence, int step)
        {
            if (sequence == null || balanceBarRoot == null)
                return step;

            CaptureBase(balanceBarRoot);
            Vector2 basePos = basePositions[balanceBarRoot];
            Vector3 baseScale = baseScales.TryGetValue(balanceBarRoot, out Vector3 cachedScale)
                ? cachedScale
                : Vector3.one;
            if (baseScale.y <= 0.001f)
                baseScale = Vector3.one;

            float at = step * itemStaggerSeconds;
            float fall = Mathf.Max(0.01f, barFallDuration);
            float settle = Mathf.Max(0.01f, barSettleDuration);
            Vector3 startScale = new Vector3(baseScale.x, 0f, baseScale.z);
            Vector3 overshootScale = new Vector3(
                baseScale.x,
                baseScale.y * Mathf.Max(0f, barScaleOvershootY),
                baseScale.z);

            CanvasGroup group = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(balanceBarRoot.gameObject);
            group.alpha = 0f;
            balanceBarRoot.anchoredPosition = basePos + new Vector2(0f, barStartYOffset);
            balanceBarRoot.localScale = startScale;

            // Entry signature / Moves row pop, then Attributes-style fill.
            sequence.InsertCallback(at, () =>
            {
                if (group != null)
                    group.alpha = 1f;
            });
            sequence.Insert(at, balanceBarRoot.DOScale(overshootScale, fall).SetEase(Ease.OutSine).SetTarget(balanceBarRoot));
            sequence.Insert(
                at,
                balanceBarRoot.DOAnchorPosY(basePos.y + barOvershootYOffset, fall)
                    .SetEase(Ease.OutSine)
                    .SetTarget(balanceBarRoot));
            sequence.Insert(
                at + fall,
                balanceBarRoot.DOScale(baseScale, settle).SetEase(Ease.InOutSine).SetTarget(balanceBarRoot));
            sequence.Insert(
                at + fall,
                balanceBarRoot.DOAnchorPosY(basePos.y, settle).SetEase(Ease.InOutSine).SetTarget(balanceBarRoot));
            sequence.InsertCallback(at, () =>
            {
                if (barTween != null && barTween.IsActive())
                    barTween.Kill(false);

                if (balanceBarEmpty)
                {
                    ApplyBalanceBarImmediate(0f, empty: true);
                    return;
                }

                barTween = DOVirtual
                    .Float(0f, targetDealtRatio, Mathf.Max(0.01f, barFillDuration), value =>
                    {
                        ApplyBalanceBarImmediate(value, empty: false);
                    })
                    .SetEase(barFillEase)
                    .SetUpdate(useUnscaledTime)
                    .SetTarget(this);
            });

            return step + 1;
        }

        private void ApplyBalanceBarColors()
        {
            if (balanceTrack != null)
                balanceTrack.color = trackColor;
            if (dealtFill != null)
                dealtFill.color = dealtFillColor;
            if (takenFill != null)
                takenFill.color = takenFillColor;
            if (dealtCapLabel != null)
            {
                dealtCapLabel.text = "Dealt";
                dealtCapLabel.color = labelColor;
            }

            if (takenCapLabel != null)
            {
                takenCapLabel.text = "Taken";
                takenCapLabel.color = labelColor;
            }
        }

        private void ApplyBalanceBarImmediate(float dealtRatio, bool empty)
        {
            ApplyBalanceBarColors();

            float clamped = Mathf.Clamp01(dealtRatio);
            bool showFills = !empty;

            if (dealtFill != null)
            {
                RectTransform rect = dealtFill.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(showFills ? clamped : 0f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                dealtFill.enabled = showFills && clamped > 0.001f;
            }

            if (takenFill != null)
            {
                RectTransform rect = takenFill.rectTransform;
                rect.anchorMin = new Vector2(showFills ? clamped : 0f, 0f);
                rect.anchorMax = new Vector2(showFills ? 1f : 0f, 1f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                takenFill.enabled = showFills && clamped < 0.999f;
            }

            if (balanceTrack != null)
                balanceTrack.enabled = true;
        }

        private void SetCapLabelsVisible(bool visible)
        {
            if (dealtCapLabel != null)
                dealtCapLabel.gameObject.SetActive(visible);
            if (takenCapLabel != null)
                takenCapLabel.gameObject.SetActive(visible);
        }

        private static float ComputeDealtRatio(long dealt, long taken)
        {
            if (dealt <= 0 && taken <= 0)
                return 0f;

            double total = (double)dealt + taken;
            if (total <= 0d)
                return 0f;

            return (float)(dealt / total);
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
            if (!baseScales.ContainsKey(rect))
                baseScales[rect] = rect.localScale == Vector3.zero ? Vector3.one : rect.localScale;
        }

        private void ResetPresentationImmediate(bool visible)
        {
            ResetLineImmediate(victoriesText, visible);
            ResetLineImmediate(enemyKosText, visible);
            ResetLineImmediate(damageDealtText, visible);
            ResetLineImmediate(damageTakenText, visible);
            ResetLineImmediate(xpEarnedText, visible);
            ResetLineImmediate(timesKodText, visible);

            if (balanceBarRoot != null)
            {
                CanvasGroup group = balanceBarRoot.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = visible ? 1f : 0f;

                if (basePositions.TryGetValue(balanceBarRoot, out Vector2 pos))
                    balanceBarRoot.anchoredPosition = pos;
                if (baseScales.TryGetValue(balanceBarRoot, out Vector3 scale))
                    balanceBarRoot.localScale = scale;
                else if (balanceBarRoot.localScale == Vector3.zero)
                    balanceBarRoot.localScale = Vector3.one;
            }
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

            if (barTween != null && barTween.IsActive())
                barTween.Kill(false);
            barTween = null;

            if (reset)
            {
                ApplyBalanceBarImmediate(targetDealtRatio, empty: balanceBarEmpty);
                ResetPresentationImmediate(visible: true);
            }
        }

        private void SetLabeledLine(TMP_Text text, string label, string value)
        {
            if (text == null)
                return;

            text.richText = true;
            text.text = FormatLabeledLine(label, value, labelColor, valueColor);
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
