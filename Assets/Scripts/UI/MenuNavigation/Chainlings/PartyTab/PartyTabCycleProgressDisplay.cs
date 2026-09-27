using Chainbound.Combat;
using SignalJam.VisualFx;
using TMPro;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    [DisallowMultipleComponent]
    public sealed class PartyTabCycleProgressDisplay : MonoBehaviour
    {
        [SerializeField] private PartyTabCycleTypographySO typography;
        [SerializeField] private TMP_Text cycleLabel;
        [SerializeField] private float labelFontSize = 20f;

        public TMP_Text CycleLabel => cycleLabel;

        public void Configure(PartyTabCycleTypographySO cycleTypography)
        {
            if (cycleTypography != null)
                typography = cycleTypography;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureBuilt(createIfMissing: true);
        }

        public void SetCycle(string label, int tier, bool visible)
        {
            EnsureBuilt(createIfMissing: false);

            gameObject.SetActive(visible);
            if (!visible || cycleLabel == null)
                return;

            // Max Cycle (tier 5) reuses Cycle V typography; label still comes from player terms.
            int visualTier = ChainlingPlayerTerms.ResolveCycleVisualTier(tier);
            VisualTierProfileSO profile = typography != null ? typography.ResolveProfile(visualTier) : null;
            PartyTabCycleTypographySO.CycleTierStyle legacyStyle = typography != null
                ? typography.Resolve(visualTier)
                : PartyTabCycleTypographySO.CreateFallback(visualTier);

            cycleLabel.text = string.IsNullOrWhiteSpace(label)
                ? ChainlingPlayerTerms.FormatCycleLabel(tier)
                : label;
            cycleLabel.fontSize = labelFontSize;
            ApplyLabelLayout();

            if (profile != null)
            {
                // Apply restores a live shared material before any TMP mesh/material access.
                VisualTierTypographyApplication.Apply(cycleLabel, profile, labelFontSize, 1f);
            }
            else
            {
                cycleLabel.color = legacyStyle.labelColor;
                cycleLabel.transform.localScale = Vector3.one * Mathf.Max(0.1f, legacyStyle.labelScale);
                ApplyLegacyFontStyle(cycleLabel, legacyStyle);
            }
        }

        private void ApplyLabelLayout()
        {
            if (cycleLabel == null)
                return;

            RectTransform labelRect = cycleLabel.rectTransform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private void EnsureBuilt(bool createIfMissing)
        {
            if (cycleLabel == null)
                cycleLabel = transform.Find("CycleLabel")?.GetComponent<TMP_Text>();

            if (cycleLabel == null && createIfMissing)
            {
                cycleLabel = PlayerMenuUiFactory.CreateText(
                    "CycleLabel",
                    transform,
                    "Cycle I",
                    labelFontSize,
                    TextAlignmentOptions.MidlineLeft,
                    false);
            }

            ApplyLabelLayout();
        }

        private static void ApplyLegacyFontStyle(TMP_Text text, PartyTabCycleTypographySO.CycleTierStyle style)
        {
            if (text == null)
                return;

            if (style.font != null)
                text.font = style.font;
            if (style.fontMaterial != null)
                VisualTierTypographyApplication.ClearMaterialInstance(text, style.fontMaterial);
        }

#if UNITY_EDITOR
        [SerializeField] private bool applyEditorPreview = true;
        [SerializeField] private int editorPreviewTier = 4;
        private bool editorPreviewQueued;

        private void OnValidate()
        {
            if (!applyEditorPreview || Application.isPlaying || typography == null || typography.VariantCatalog == null)
                return;

            if (!ShouldPreviewInEditor())
                return;

            if (editorPreviewQueued)
                return;

            editorPreviewQueued = true;
            UnityEditor.EditorApplication.delayCall += ApplyEditorPreviewDelayed;
        }

        private void ApplyEditorPreviewDelayed()
        {
            editorPreviewQueued = false;

            if (this == null || Application.isPlaying || typography == null)
                return;

            int tier = Mathf.Clamp(editorPreviewTier, 0, CombatConstants.REBIRTH_MAX);
            SetCycle(ChainlingPlayerTerms.FormatCycleLabel(tier), tier, true);
        }

        private bool ShouldPreviewInEditor()
        {
            ChainlingPartyInfoViewLayout layout = GetComponentInParent<ChainlingPartyInfoViewLayout>(true);
            if (layout == null)
                return true;

            UnityEditor.SerializedObject layoutObject = new UnityEditor.SerializedObject(layout);
            return layoutObject.FindProperty("showCycleProgress").boolValue;
        }
#endif
    }
}
