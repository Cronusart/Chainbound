using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation;
using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    /// <summary>
    /// Prefab-authored Entry (Detail) species card: labeled catalog lines, blurb, signature move row, encounters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChainlingPartyEntryViewLayout : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TypeIconPaletteSO typeIconPalette;
        [SerializeField] private PartyTabMoveRarityPaletteSO rarityPalette;

        [Header("Catalog Lines")]
        [SerializeField] private TMP_Text speciesNameText;
        [SerializeField] private TMP_Text preferredTypeText;
        [SerializeField] private TMP_Text preferredRoleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text signatureLabelText;
        [SerializeField] private TMP_Text encountersText;

        [Header("Signature")]
        [SerializeField] private ChainlingPartyMoveRowDisplay signatureMoveRow;

        [Header("Presentation")]
        [SerializeField] private Color labelColor = new Color(0.78f, 0.82f, 0.88f, 1f);
        [SerializeField] private Color valueColor = Color.white;
        [SerializeField] private Color mutedTextColor = PlayerMenuUiFactory.Muted;

        [Header("Bind Animation")]
        [SerializeField] private bool animateOnBind = true;
        [SerializeField, Min(0f)] private float animationStartDelay = 0.12f;
        [SerializeField, Min(0f)] private float itemStaggerSeconds = 0.045f;
        [SerializeField, Min(0f)] private float lineFadeDuration = 0.16f;
        [SerializeField] private float lineStartYOffset = 10f;
        [SerializeField, Min(0f)] private float lineSettleDuration = 0.14f;
        [SerializeField, Min(0f)] private float signatureFallDuration = 0.18f;
        [SerializeField, Min(0f)] private float signatureSettleDuration = 0.12f;
        [SerializeField] private float signatureStartYOffset = 14f;
        [SerializeField] private float signatureOvershootYOffset = -3f;
        [SerializeField] private float signatureScaleOvershootY = 1.04f;
        [SerializeField] private bool useUnscaledTime = true;
        [SerializeField] private bool killTweensOnDisable = true;

        private Sequence presentationSequence;
        private readonly Dictionary<RectTransform, Vector2> basePositions = new Dictionary<RectTransform, Vector2>();
        private readonly Dictionary<RectTransform, Vector3> baseScales = new Dictionary<RectTransform, Vector3>();

        public TypeIconPaletteSO TypeIconPalette => typeIconPalette;
        public PartyTabMoveRarityPaletteSO RarityPalette => rarityPalette;
        public ChainlingPartyMoveRowDisplay SignatureMoveRow => signatureMoveRow;

        public void ConfigurePalettes(
            TypeIconPaletteSO icons,
            PartyTabMoveRarityPaletteSO rarity,
            ChainlingPartyTypeChromePaletteSO chrome)
        {
            if (icons != null)
                typeIconPalette = icons;
            if (rarity != null)
                rarityPalette = rarity;
        }

        public void Bind(ChainlingPartyColumnSummary summary)
        {
            KillPresentation(reset: true);

            if (summary == null || summary.isEmpty)
            {
                BindEmpty(summary != null ? summary.emptyMessage : "Assign at a PC.");
                return;
            }

            SetLabeledLine(
                speciesNameText,
                ChainlingPartyDetailDisplayUtility.SpeciesNameLabel,
                string.IsNullOrWhiteSpace(summary.speciesDisplayName) ? "—" : summary.speciesDisplayName);

            SetLabeledLine(
                preferredTypeText,
                ChainlingPartyDetailDisplayUtility.PreferredTypeLabel,
                ChainlingPartyDetailDisplayUtility.FormatPreferredTypeValue(
                    summary.primaryType,
                    summary.secondaryType));

            SetLabeledLine(
                preferredRoleText,
                ChainlingPartyDetailDisplayUtility.PreferredRoleLabel,
                string.IsNullOrWhiteSpace(summary.combatRoleLabel) ? "All-Rounder" : summary.combatRoleLabel);

            if (descriptionText != null)
                descriptionText.text = string.IsNullOrWhiteSpace(summary.descriptionHook)
                    ? "No lore entry authored yet."
                    : summary.descriptionHook;

            if (signatureLabelText != null)
                signatureLabelText.text = "Signature";

            if (signatureMoveRow != null)
            {
                ChainlingPartyMoveRowSummary row = summary.signatureMove
                    ?? ChainlingPartyMoveRowSummary.EmptySlot();
                signatureMoveRow.Bind(row, rarityPalette, typeIconPalette);
                signatureMoveRow.SetVisible(true);
            }

            if (encountersText != null)
            {
                string line = string.IsNullOrWhiteSpace(summary.encountersSummaryLine)
                    ? "Origins unlogged"
                    : summary.encountersSummaryLine;
                encountersText.text = line.StartsWith("Encounters")
                    ? line
                    : $"Encounters · {line}";
                encountersText.color = mutedTextColor;
            }

            PlayPresentation();
        }

        public void BindEmpty(string message)
        {
            KillPresentation(reset: true);

            SetLabeledLine(speciesNameText, ChainlingPartyDetailDisplayUtility.SpeciesNameLabel, "—");
            SetLabeledLine(preferredTypeText, ChainlingPartyDetailDisplayUtility.PreferredTypeLabel, "—");
            SetLabeledLine(preferredRoleText, ChainlingPartyDetailDisplayUtility.PreferredRoleLabel, "—");

            if (descriptionText != null)
                descriptionText.text = string.IsNullOrWhiteSpace(message) ? "—" : message;
            if (signatureLabelText != null)
                signatureLabelText.text = "Signature";

            if (signatureMoveRow != null)
            {
                signatureMoveRow.Bind(
                    new ChainlingPartyMoveRowSummary
                    {
                        isEmpty = true,
                        moveName = "No signature authored",
                        statLabel = "",
                    },
                    rarityPalette,
                    typeIconPalette);
            }

            if (encountersText != null)
                encountersText.text = "";

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
            step = AppendLineReveal(presentationSequence, speciesNameText, step);
            step = AppendLineReveal(presentationSequence, preferredTypeText, step);
            step = AppendLineReveal(presentationSequence, preferredRoleText, step);
            step = AppendLineReveal(presentationSequence, descriptionText, step);
            step = AppendLineReveal(presentationSequence, signatureLabelText, step);
            step = AppendSignatureReveal(presentationSequence, step);
            AppendLineReveal(presentationSequence, encountersText, step);
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

        private int AppendSignatureReveal(Sequence sequence, int step)
        {
            if (sequence == null || signatureMoveRow == null || !signatureMoveRow.gameObject.activeInHierarchy)
                return step;

            RectTransform rect = signatureMoveRow.transform as RectTransform;
            if (rect == null)
                return step;

            CaptureBase(rect);
            Vector2 basePos = basePositions[rect];
            Vector3 baseScale = baseScales[rect];
            if (baseScale.y <= 0.001f)
                baseScale.y = 1f;

            float at = step * itemStaggerSeconds;
            float fall = Mathf.Max(0.01f, signatureFallDuration);
            float settle = Mathf.Max(0.01f, signatureSettleDuration);
            Vector3 startScale = new Vector3(baseScale.x, 0f, baseScale.z);
            Vector3 overshootScale = new Vector3(
                baseScale.x,
                baseScale.y * Mathf.Max(0f, signatureScaleOvershootY),
                baseScale.z);

            CanvasGroup group = PlayerMenuUiFactory.GetOrAdd<CanvasGroup>(signatureMoveRow.gameObject);
            group.alpha = 0f;
            rect.anchoredPosition = basePos + new Vector2(0f, signatureStartYOffset);
            rect.localScale = startScale;

            sequence.InsertCallback(at, () =>
            {
                if (group != null)
                    group.alpha = 1f;
            });
            sequence.Insert(at, rect.DOScale(overshootScale, fall).SetEase(Ease.OutSine).SetTarget(rect));
            sequence.Insert(
                at,
                rect.DOAnchorPosY(basePos.y + signatureOvershootYOffset, fall).SetEase(Ease.OutSine).SetTarget(rect));
            sequence.Insert(at + fall, rect.DOScale(baseScale, settle).SetEase(Ease.InOutSine).SetTarget(rect));
            sequence.Insert(
                at + fall,
                rect.DOAnchorPosY(basePos.y, settle).SetEase(Ease.InOutSine).SetTarget(rect));

            return step + 1;
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
            ResetLineImmediate(speciesNameText, visible);
            ResetLineImmediate(preferredTypeText, visible);
            ResetLineImmediate(preferredRoleText, visible);
            ResetLineImmediate(descriptionText, visible);
            ResetLineImmediate(signatureLabelText, visible);
            ResetLineImmediate(encountersText, visible);

            if (signatureMoveRow != null)
            {
                RectTransform rect = signatureMoveRow.transform as RectTransform;
                CanvasGroup group = signatureMoveRow.GetComponent<CanvasGroup>();
                if (group != null)
                    group.alpha = visible ? 1f : 0f;
                if (rect != null)
                {
                    if (basePositions.TryGetValue(rect, out Vector2 pos))
                        rect.anchoredPosition = pos;
                    if (baseScales.TryGetValue(rect, out Vector3 scale))
                        rect.localScale = scale;
                    else if (rect.localScale == Vector3.zero)
                        rect.localScale = Vector3.one;
                }
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

            if (reset)
                ResetPresentationImmediate(visible: true);
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
