using Chainbound.Combat;
using DG.Tweening;
using SignalJam.UI.MenuNavigation.PartyTab;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Horizontal party-target row for Inventory Use: type icon, name, compact stars, cycle icon, HP + XP.
    /// Selection chrome (TargetSlotFrame) is owned by the popup; this row only scales itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryItemUseTargetSlot : MonoBehaviour
    {
        private static readonly Color PanelIdleDefault = new Color(0.16f, 0.18f, 0.2f, 1f);
        private static readonly Color HpGreen = new Color(0.22f, 0.78f, 0.34f, 1f);
        private static readonly Color HpMissingRed = new Color(0.78f, 0.22f, 0.24f, 1f);
        private static readonly Color XpOrange = new Color(0.95f, 0.55f, 0.18f, 1f);

        [SerializeField] private Button button;
        [SerializeField] private Image panelBackground;
        [SerializeField] private Image typeIcon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private CompactStarRatingDisplay starRatingDisplay;
        [SerializeField] private CycleIconDisplay cycleIconDisplay;
        [SerializeField] private Image hpBarBackground;
        [SerializeField] private Image hpFill;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private Image xpFill;
        [SerializeField] private TMP_Text xpLabel;

        [Header("Selection Motion")]
        [SerializeField] private float selectedScale = 1.04f;
        [SerializeField] private float unselectedScale = 1f;
        [SerializeField] private float selectionTweenDuration = 0.16f;

        private Tween scaleTween;
        private Tween hpFillTween;
        private Tween xpFillTween;
        private Tween xpLabelTween;
        private float hpTargetFill01;
        private float xpTargetFill01;
        private string hpTargetLabel = "— / —";
        private int xpTargetEarned;
        private int xpTargetSize;

        public Button Button => button;
        public Image HpFill => hpFill;
        public TMP_Text HpLabel => hpLabel;
        public RectTransform RectTransform => transform as RectTransform;
        public float SelectedScale => selectedScale;
        public float UnselectedScale => unselectedScale;
        public float SelectionTweenDuration => selectionTweenDuration;

        public void ConfigurePresentation(
            TypeIconPaletteSO typeIcons,
            CompactStarRatingArtSO starArt,
            CycleIconPaletteSO cyclePalette)
        {
            if (starRatingDisplay != null && starArt != null)
                starRatingDisplay.Configure(starArt);
            if (cycleIconDisplay != null && cyclePalette != null)
                cycleIconDisplay.Configure(cyclePalette);
            _ = typeIcons;
        }

        public void BuildDefaultLayoutForEditor()
        {
            EnsureMissingLayoutForEditor(forceLayoutElementDefaults: true);
        }

        public void EnsureMissingLayoutForEditor()
        {
            EnsureMissingLayoutForEditor(forceLayoutElementDefaults: false);
        }

        private void EnsureMissingLayoutForEditor(bool forceLayoutElementDefaults)
        {
            RectTransform root = transform as RectTransform;
            if (root == null)
                return;

            // Strip legacy outline selection chrome if still present.
            Outline legacyOutline = GetComponent<Outline>();
            if (legacyOutline != null)
            {
                if (Application.isPlaying)
                    Destroy(legacyOutline);
                else
                    DestroyImmediate(legacyOutline);
            }

            Transform legacyFrame = root.Find("FocusFrame");
            if (legacyFrame != null)
                legacyFrame.gameObject.SetActive(false);

            LayoutElement layout = GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = gameObject.AddComponent<LayoutElement>();
                forceLayoutElementDefaults = true;
            }

            if (forceLayoutElementDefaults)
            {
                layout.minHeight = 128f;
                layout.preferredHeight = 136f;
                layout.flexibleWidth = 1f;
                layout.flexibleHeight = 0f;
            }

            if (button == null)
                button = PlayerMenuUiFactory.GetOrAdd<Button>(gameObject);

            if (panelBackground == null)
            {
                panelBackground = GetComponent<Image>();
                if (panelBackground == null)
                {
                    panelBackground = gameObject.AddComponent<Image>();
                    panelBackground.color = PanelIdleDefault;
                }
            }

            if (button != null)
            {
                button.targetGraphic = panelBackground;
                button.transition = Selectable.Transition.None;
            }

            if (typeIcon == null)
            {
                Transform existing = root.Find("TypeIcon");
                if (existing != null)
                    typeIcon = existing.GetComponent<Image>();
            }

            if (typeIcon == null)
            {
                typeIcon = PlayerMenuUiFactory.CreateImage("TypeIcon", root, new Color(0.35f, 0.38f, 0.42f, 1f));
                RectTransform iconRect = typeIcon.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(18f, 8f);
                iconRect.sizeDelta = new Vector2(64f, 64f);
                typeIcon.preserveAspect = true;
                typeIcon.raycastTarget = false;
            }

            if (nameLabel == null)
            {
                Transform existing = root.Find("Name");
                if (existing != null)
                    nameLabel = existing.GetComponent<TMP_Text>();
            }

            if (nameLabel == null)
            {
                nameLabel = PlayerMenuUiFactory.CreateText("Name", root, "CHAINLING", 30f, TextAlignmentOptions.Left, true);
                RectTransform nameRect = nameLabel.rectTransform;
                nameRect.anchorMin = new Vector2(0f, 0.5f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.offsetMin = new Vector2(96f, 52f);
                nameRect.offsetMax = new Vector2(-160f, -12f);
            }

            if (starRatingDisplay == null)
            {
                Transform existing = root.Find("StarRating");
                if (existing != null)
                {
                    PartyTabStarRatingDisplay legacyStars = existing.GetComponent<PartyTabStarRatingDisplay>();
                    if (legacyStars != null)
                    {
                        if (Application.isPlaying)
                            Destroy(legacyStars);
                        else
                            DestroyImmediate(legacyStars);
                    }

                    starRatingDisplay = existing.GetComponent<CompactStarRatingDisplay>()
                        ?? existing.gameObject.AddComponent<CompactStarRatingDisplay>();
                }
            }

            if (starRatingDisplay == null)
            {
                RectTransform starRoot = PlayerMenuUiFactory.CreateRect("StarRating", root);
                starRoot.anchorMin = new Vector2(1f, 1f);
                starRoot.anchorMax = new Vector2(1f, 1f);
                starRoot.pivot = new Vector2(1f, 1f);
                starRoot.anchoredPosition = new Vector2(-16f, -10f);
                starRoot.sizeDelta = new Vector2(120f, 22f);
                starRatingDisplay = starRoot.gameObject.AddComponent<CompactStarRatingDisplay>();
            }

            starRatingDisplay.BuildDefaultLayoutForEditor();

            // Prefer CycleIcon; keep legacy Cycle text hidden if present.
            Transform cycleText = root.Find("Cycle");
            if (cycleIconDisplay == null)
            {
                Transform existingIcon = root.Find("CycleIcon");
                if (existingIcon != null)
                    cycleIconDisplay = existingIcon.GetComponent<CycleIconDisplay>()
                        ?? existingIcon.gameObject.AddComponent<CycleIconDisplay>();
            }

            if (cycleIconDisplay == null)
            {
                RectTransform cycleRoot = PlayerMenuUiFactory.CreateRect("CycleIcon", root);
                cycleRoot.anchorMin = new Vector2(1f, 1f);
                cycleRoot.anchorMax = new Vector2(1f, 1f);
                cycleRoot.pivot = new Vector2(1f, 1f);
                cycleRoot.anchoredPosition = new Vector2(-16f, -40f);
                cycleRoot.sizeDelta = new Vector2(36f, 28f);
                cycleIconDisplay = cycleRoot.gameObject.AddComponent<CycleIconDisplay>();
            }

            cycleIconDisplay.EnsureDefaultLayoutForEditor();
            if (cycleText != null)
                cycleText.gameObject.SetActive(false);

            // Red track + green fill (no separate missing layer — rounded sprites seam badly mid-bar).
            if (hpBarBackground == null)
            {
                Transform existingBg = root.Find("HpBarBackground");
                if (existingBg != null)
                    hpBarBackground = existingBg.GetComponent<Image>();
            }

            if (hpBarBackground == null)
            {
                hpBarBackground = PlayerMenuUiFactory.CreateImage("HpBarBackground", root, HpMissingRed);
                RectTransform hpBgRect = hpBarBackground.rectTransform;
                hpBgRect.anchorMin = new Vector2(0f, 0f);
                hpBgRect.anchorMax = new Vector2(1f, 0f);
                hpBgRect.pivot = new Vector2(0.5f, 0f);
                hpBgRect.anchoredPosition = new Vector2(0f, 36f);
                hpBgRect.sizeDelta = new Vector2(-32f, 28f);
            }

            RectTransform hpBg = hpBarBackground.rectTransform;
            Transform legacyMissing = hpBg.Find("HpBarMissing");
            if (legacyMissing != null)
                legacyMissing.gameObject.SetActive(false);

            if (hpFill == null)
            {
                Transform existing = hpBg.Find("HpBarFill");
                if (existing != null)
                    hpFill = existing.GetComponent<Image>();
            }

            if (hpFill == null)
            {
                hpFill = PlayerMenuUiFactory.CreateImage("HpBarFill", hpBg, HpGreen);
                PlayerMenuUiFactory.Stretch(hpFill.rectTransform);
                PartyTabProgressBarUtility.ConfigureRectFill(hpFill);
            }

            if (hpLabel == null)
            {
                Transform existing = hpBg.Find("HpLabel");
                if (existing != null)
                    hpLabel = existing.GetComponent<TMP_Text>();
            }

            if (hpLabel == null)
            {
                Transform existing = root.Find("HpLabel");
                if (existing != null)
                    hpLabel = existing.GetComponent<TMP_Text>();
            }

            if (hpLabel == null)
            {
                hpLabel = PlayerMenuUiFactory.CreateText("HpLabel", hpBg, "— / —", 18f, TextAlignmentOptions.Center, false);
                PlayerMenuUiFactory.Stretch(hpLabel.rectTransform);
                hpLabel.raycastTarget = false;
            }
            else if (hpBarBackground != null && hpLabel.transform.parent != hpBarBackground.transform)
            {
                hpLabel.transform.SetParent(hpBarBackground.transform, false);
                PlayerMenuUiFactory.Stretch(hpLabel.rectTransform);
                hpLabel.raycastTarget = false;
            }

            if (nameLabel != null && hpLabel != null && nameLabel.font != null)
                hpLabel.font = nameLabel.font;

            RectTransform xpBg = root.Find("XpBarBackground") as RectTransform;
            if (xpBg == null)
            {
                Image bg = PlayerMenuUiFactory.CreateImage("XpBarBackground", root, new Color(0.1f, 0.08f, 0.06f, 1f));
                xpBg = bg.rectTransform;
                xpBg.anchorMin = new Vector2(0f, 0f);
                xpBg.anchorMax = new Vector2(1f, 0f);
                xpBg.pivot = new Vector2(0.5f, 0f);
                xpBg.anchoredPosition = new Vector2(0f, 14f);
                xpBg.sizeDelta = new Vector2(-32f, 12f);
            }

            if (xpFill == null)
            {
                Transform existing = xpBg.Find("XpBarFill");
                if (existing != null)
                    xpFill = existing.GetComponent<Image>();
            }

            if (xpFill == null)
            {
                xpFill = PlayerMenuUiFactory.CreateImage("XpBarFill", xpBg, XpOrange);
                PlayerMenuUiFactory.Stretch(xpFill.rectTransform);
                PartyTabProgressBarUtility.ConfigureRectFill(xpFill);
            }

            if (xpLabel == null)
            {
                Transform existing = xpBg.Find("XpLabel");
                if (existing != null)
                    xpLabel = existing.GetComponent<TMP_Text>();
            }

            if (xpLabel == null)
            {
                xpLabel = PlayerMenuUiFactory.CreateText("XpLabel", xpBg, "", 16f, TextAlignmentOptions.Center, false);
                PlayerMenuUiFactory.Stretch(xpLabel.rectTransform);
                xpLabel.raycastTarget = false;
            }

            if (nameLabel != null && xpLabel != null && nameLabel.font != null)
                xpLabel.font = nameLabel.font;

            if (typeIcon != null)
                typeIcon.rectTransform.SetSiblingIndex(Mathf.Min(1, root.childCount - 1));
        }

        public void Bind(
            PlayerRosterEntry entry,
            SpeciesDefinitionSO species,
            TypeIconPaletteSO typeIconPalette,
            CompactStarRatingArtSO starArt,
            CycleIconPaletteSO cyclePalette,
            PartyTabMotionProfileSO motionProfile,
            string caption = null)
        {
            EnsureMissingLayoutForEditor(forceLayoutElementDefaults: false);
            ConfigurePresentation(typeIconPalette, starArt, cyclePalette);

            ChainlingSnapshot snapshot = entry != null ? entry.snapshot : null;
            string displayName = "Chainling";
            if (snapshot != null)
            {
                if (!string.IsNullOrWhiteSpace(snapshot.displayName))
                    displayName = snapshot.displayName.Trim();
                else if (species != null && !string.IsNullOrWhiteSpace(species.displayName))
                    displayName = species.displayName;
                else if (!string.IsNullOrWhiteSpace(snapshot.speciesId))
                    displayName = snapshot.speciesId;
            }

            if (nameLabel != null)
            {
                nameLabel.text = string.IsNullOrWhiteSpace(caption)
                    ? displayName.ToUpperInvariant()
                    : caption.Trim().ToUpperInvariant();
            }

            if (typeIcon != null)
            {
                Sprite icon = null;
                if (typeIconPalette != null)
                {
                    if (species != null)
                        icon = typeIconPalette.GetIcon(species.primaryType);
                    if (icon == null)
                        icon = typeIconPalette.FallbackIcon;
                }
                typeIcon.sprite = icon;
                typeIcon.enabled = icon != null;
                typeIcon.preserveAspect = true;
                typeIcon.color = icon != null ? Color.white : new Color(0.35f, 0.38f, 0.42f, 1f);
                typeIcon.rectTransform.SetSiblingIndex(1);
            }

            int stars = snapshot != null ? ChainlingProgressionUtility.NormalizeStars(snapshot.stars) : 0;
            if (starRatingDisplay != null)
                starRatingDisplay.SetRating(stars, true, Application.isPlaying);

            int rebirth = snapshot != null ? snapshot.rebirthCount : 0;
            if (cycleIconDisplay != null)
                cycleIconDisplay.SetCycleFromRebirthCount(rebirth, true);

            ChainlingVitalsDisplay vitals = ChainlingVitalsUtility.BuildDisplay(snapshot, species);
            hpTargetFill01 = vitals.Fill01;
            hpTargetLabel = vitals.Label;
            ApplyHpVisual(0f, hpTargetLabel, vitals.IsFainted && hpTargetFill01 <= 0.001f);

            ResolveXpSegment(entry, out xpTargetEarned, out xpTargetSize, out xpTargetFill01);
            PartyTabProgressBarUtility.SetFill01(xpFill, 0f);
            SetXpLabel(0, xpTargetSize);

            if (motionProfile != null)
            {
                selectedScale = Mathf.Max(1.01f, motionProfile.ButtonFocusScale);
                unselectedScale = 1f;
            }
        }

        public void PlayProgressReveal(float duration = 0.46f)
        {
            KillProgressTweens();
            float fillDuration = Mathf.Max(0.05f, duration);

            ApplyHpVisual(0f, hpTargetLabel);
            if (Application.isPlaying)
            {
                hpFillTween = PartyTabProgressBarUtility
                    .DOFill01(hpFill, hpTargetFill01, fillDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
            }
            else
            {
                ApplyHpVisual(hpTargetFill01, hpTargetLabel);
            }

            PartyTabProgressBarUtility.SetFill01(xpFill, 0f);
            SetXpLabel(0, xpTargetSize);
            if (Application.isPlaying)
            {
                xpFillTween = PartyTabProgressBarUtility
                    .DOFill01(xpFill, xpTargetFill01, fillDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true);
                if (xpTargetSize > 0)
                {
                    xpLabelTween = DOVirtual
                        .Float(0f, xpTargetEarned, fillDuration, value =>
                        {
                            SetXpLabel(Mathf.RoundToInt(value), xpTargetSize);
                        })
                        .SetEase(Ease.OutCubic)
                        .SetUpdate(true)
                        .OnComplete(() => SetXpLabel(xpTargetEarned, xpTargetSize));
                }
            }
            else
            {
                PartyTabProgressBarUtility.SetFill01(xpFill, xpTargetFill01);
                SetXpLabel(xpTargetEarned, xpTargetSize);
            }
        }

        public void ApplyHpVisual(float fill01, string label, bool fainted = false)
        {
            if (hpFillTween != null && hpFillTween.IsActive())
                hpFillTween.Kill();
            hpFillTween = null;
            float clamped = Mathf.Clamp01(fill01);

            // Red track shows through wherever green fill has drained.
            if (hpBarBackground != null)
                hpBarBackground.color = HpMissingRed;

            PartyTabProgressBarUtility.SetFill01(hpFill, clamped);
            if (hpFill != null)
            {
                hpFill.color = fainted && clamped <= 0.001f ? HpMissingRed : HpGreen;
                hpFill.enabled = clamped > 0.001f;
            }

            if (hpLabel != null)
                hpLabel.text = string.IsNullOrWhiteSpace(label) ? "— / —" : label;
        }

        public void PlaySelectPulse()
        {
            if (scaleTween != null && scaleTween.IsActive())
                scaleTween.Kill();

            if (!Application.isPlaying)
                return;

            transform.localScale = Vector3.one * selectedScale;
            scaleTween = transform
                .DOPunchScale(Vector3.one * 0.06f, 0.16f, 1, 0f)
                .SetUpdate(true);
        }

        public void KillMotion()
        {
            if (scaleTween != null && scaleTween.IsActive())
                scaleTween.Kill();
            scaleTween = null;
            KillProgressTweens();
        }

        public void SetSelected(bool isSelected, bool animate)
        {
            float targetScale = isSelected ? selectedScale : unselectedScale;
            if (scaleTween != null && scaleTween.IsActive())
                scaleTween.Kill();

            if (!animate || !Application.isPlaying)
            {
                transform.localScale = Vector3.one * targetScale;
                return;
            }

            scaleTween = transform
                .DOScale(targetScale, selectionTweenDuration)
                .SetEase(isSelected ? Ease.OutBack : Ease.OutSine)
                .SetUpdate(true);
        }

        private static void ResolveXpSegment(
            PlayerRosterEntry entry,
            out int earned,
            out int required,
            out float fill01)
        {
            earned = 0;
            required = 0;
            fill01 = 0f;
            if (entry?.snapshot == null)
                return;

            int masteryXp = Mathf.Max(0, entry.masteryXp);
            int rebirth = ChainlingProgressionUtility.NormalizeRebirthCount(entry.snapshot.rebirthCount);
            StarProgressionUtility.GetMilestoneSegment(
                masteryXp,
                rebirth,
                BalanceConfigRuntime.Progression,
                out earned,
                out required,
                out fill01);
        }

        private void SetXpLabel(int earned, int required)
        {
            if (xpLabel == null)
                return;

            xpLabel.text = required > 0
                ? $"{Mathf.Max(0, earned)} / {required}"
                : earned > 0 ? earned.ToString() : "";
        }

        private void KillProgressTweens()
        {
            if (hpFillTween != null && hpFillTween.IsActive())
                hpFillTween.Kill();
            hpFillTween = null;
            if (xpFillTween != null && xpFillTween.IsActive())
                xpFillTween.Kill();
            xpFillTween = null;
            if (xpLabelTween != null && xpLabelTween.IsActive())
                xpLabelTween.Kill();
            xpLabelTween = null;
        }

        private void OnDestroy()
        {
            if (scaleTween != null && scaleTween.IsActive())
                scaleTween.Kill();
            scaleTween = null;
            KillProgressTweens();
        }
    }
}
