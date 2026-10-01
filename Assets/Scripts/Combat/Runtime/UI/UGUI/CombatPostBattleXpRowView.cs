using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Combat-safe copy of the Inventory Use popup target row: type icon, name,
    /// compact stars, cycle icon, HP + XP bars. Does not reference MenuNavigation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatPostBattleXpRowView : MonoBehaviour
    {
        public const float RowHeight = 200f;

        private static readonly Color PanelColor = new Color(0.216f, 0.306f, 0.290f, 1f);
        private static readonly Color TypePlaceholder = new Color(0.35f, 0.38f, 0.42f, 1f);
        private static readonly Color HpGreen = new Color(0.22f, 0.78f, 0.34f, 1f);
        private static readonly Color HpMissingRed = new Color(0.78f, 0.22f, 0.24f, 1f);
        private static readonly Color XpOrange = new Color(0.95f, 0.55f, 0.18f, 1f);
        private static readonly Color XpTrack = new Color(0.1f, 0.08f, 0.06f, 1f);

        [SerializeField] private Image panelBackground;
        [SerializeField] private Image typeIcon;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private CompactStarRatingDisplay starRatingDisplay;
        [SerializeField] private CycleIconDisplay cycleIconDisplay;
        [SerializeField] private Image hpBarBackground;
        [SerializeField] private Image hpFill;
        [SerializeField] private TextMeshProUGUI hpLabel;
        [SerializeField] private Image xpFill;
        [SerializeField] private TextMeshProUGUI xpLabel;
        [SerializeField] private TextMeshProUGUI folioChanceLabel;

        private float _xpFromFill;
        private float _xpToFill;
        private bool _hasStarUp;
        private int _previousStars;
        private int _starUpCount;
        private int _masteryCycleCount;
        private int _xpFromEarned;
        private int _xpFromSize;
        private int _xpToEarned;
        private int _xpToSize;

        public static CombatPostBattleXpRowView Create(
            Transform parent,
            Sprite rowBackground,
            Sprite barSprite,
            TMP_FontAsset font)
        {
            var go = new GameObject(
                "XpRow",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(CombatPostBattleXpRowView));
            go.transform.SetParent(parent, false);

            CombatPostBattleXpRowView row = go.GetComponent<CombatPostBattleXpRowView>();
            row.BuildLayout(rowBackground, barSprite, font);
            return row;
        }

        public void Bind(
            ChainlingXpReward reward,
            BattleParticipant participant,
            SpeciesDefinitionSO species,
            TypeIconPaletteSO typeIcons,
            CompactStarRatingArtSO starArt,
            CycleIconPaletteSO cyclePalette,
            ProgressionConfigSO progression)
        {
            string displayName = !string.IsNullOrWhiteSpace(reward?.displayName)
                ? reward.displayName
                : !string.IsNullOrWhiteSpace(reward?.speciesId)
                    ? reward.speciesId
                    : "Chainling";
            if (nameLabel != null)
            {
                nameLabel.text = displayName.ToUpperInvariant();
                nameLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            }

            if (typeIcon != null)
            {
                Sprite icon = null;
                if (typeIcons != null && species != null)
                    icon = typeIcons.GetIcon(species.primaryType);
                typeIcon.sprite = icon;
                typeIcon.enabled = true;
                typeIcon.preserveAspect = true;
                typeIcon.color = icon != null ? Color.white : TypePlaceholder;
            }

            _previousStars = reward != null ? Mathf.Max(0, reward.previousStars) : 0;
            int newStars = reward != null ? Mathf.Max(0, reward.newStars) : 0;
            _starUpCount = 0;
            if (reward != null && reward.starsCrossed != null && reward.starsCrossed.Count > 0)
                _starUpCount = reward.starsCrossed.Count;
            else if (newStars > _previousStars)
                _starUpCount = newStars - _previousStars;
            else if (reward != null && reward.gainedStar)
                _starUpCount = 1;
            _hasStarUp = _starUpCount > 0;
            _masteryCycleCount = reward?.masteryCyclesCompleted != null
                ? reward.masteryCyclesCompleted.Count
                : 0;
            if (starRatingDisplay != null)
            {
                starRatingDisplay.Configure(starArt);
                starRatingDisplay.SetRating(_previousStars, true, false);
            }

            int rebirth = reward != null ? reward.rebirthCount : 0;
            if (cycleIconDisplay != null)
            {
                cycleIconDisplay.Configure(cyclePalette);
                cycleIconDisplay.SetCycleFromRebirthCount(rebirth, true);
            }

            int hp = participant != null ? Mathf.Max(0, participant.endCurrentHp) : 0;
            int maxHp = participant != null ? Mathf.Max(0, participant.endMaxHp) : 0;
            if (reward != null && reward.wasKOd)
                hp = 0;
            float hpFill01 = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            ApplyHp(hpFill01, maxHp > 0 ? hp + " / " + maxHp : "— / —", hp <= 0);

            _xpFromFill = StarProgressionUtility.GetMilestoneFill01(
                reward != null ? reward.previousMasteryXp : 0,
                rebirth,
                progression);
            _xpToFill = StarProgressionUtility.GetMilestoneFill01(
                reward != null ? reward.newMasteryXp : 0,
                rebirth,
                progression);
            StarProgressionUtility.GetMilestoneSegment(
                reward != null ? reward.previousMasteryXp : 0,
                rebirth,
                progression,
                out _xpFromEarned,
                out _xpFromSize,
                out _);
            StarProgressionUtility.GetMilestoneSegment(
                reward != null ? reward.newMasteryXp : 0,
                rebirth,
                progression,
                out _xpToEarned,
                out _xpToSize,
                out _);
            SetBarFill(xpFill, _xpFromFill);
            SetXpLabel(_xpFromEarned, _xpFromSize);
            SetFolioChance(0, false, false);
            PrepareHidden();
        }

        public int StarUpCount => _starUpCount;

        public bool HasStarUp => _hasStarUp;

        public int MasteryCycleCount => _masteryCycleCount;

        public bool HasProgressionMilestone => _starUpCount > 0 || _masteryCycleCount > 0;

        public bool HasXpProgress
        {
            get
            {
                float target = ResolveXpShareTarget();
                return Mathf.Abs(target - _xpFromFill) > 0.001f;
            }
        }

        public void PrepareHidden()
        {
            transform.localScale = Vector3.zero;
        }

        public Tween PlayAppear()
        {
            Transform target = transform;
            target.DOKill();
            target.localScale = Vector3.zero;
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(target);
            sequence.Append(target.DOScale(Vector3.one * 1.12f, 0.18f).SetEase(Ease.OutCubic));
            sequence.Append(target.DOScale(Vector3.one, 0.1f).SetEase(Ease.OutQuad));
            return sequence;
        }

        public Tween PlayXpFill(float duration = 0.5f)
        {
            if (!HasXpProgress)
                return null;

            float fillDuration = Mathf.Max(0.05f, duration);
            SetBarFill(xpFill, _xpFromFill);
            SetXpLabel(_xpFromEarned, HasProgressionMilestone ? _xpFromSize : _xpToSize);
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(xpFill);
            sequence.Append(DOFill(xpFill, ResolveXpShareTarget(), fillDuration));
            int labelFrom = _xpFromEarned;
            int labelTo = HasProgressionMilestone ? Mathf.Max(_xpFromSize, _xpFromEarned) : _xpToEarned;
            int labelSize = HasProgressionMilestone ? _xpFromSize : _xpToSize;
            sequence.Join(DOVirtual.Float(labelFrom, labelTo, fillDuration, value =>
            {
                SetXpLabel(Mathf.RoundToInt(value), labelSize);
            }).SetEase(Ease.OutCubic).SetUpdate(true));
            return sequence;
        }

        public Tween PlayMasteryCycle(int stepIndex, float duration = 0.8f)
        {
            if (stepIndex < 0 || stepIndex >= _masteryCycleCount)
                return null;

            bool lastStep = stepIndex >= _masteryCycleCount - 1;
            if (lastStep)
            {
                SetBarFill(xpFill, _xpToFill);
                SetXpLabel(_xpToEarned, _xpToSize);
            }

            if (starRatingDisplay == null)
                return null;

            starRatingDisplay.SetRating(CombatConstants.STAR_MAX, true, false);
            return starRatingDisplay.PlayMasteryCycle(duration);
        }

        public void SetFolioChance(int percent, bool mastery, bool visible = true)
        {
            if (folioChanceLabel == null)
                return;
            folioChanceLabel.gameObject.SetActive(visible);
            if (visible)
                folioChanceLabel.text = $"Folio chance: {Mathf.Clamp(percent, 0, 100)}%";
        }

        public Tween PlayStarUp(int stepIndex, float duration = 0.5f)
        {
            if (stepIndex < 0 || stepIndex >= _starUpCount)
                return null;

            bool lastStep = stepIndex >= _starUpCount - 1;
            SetBarFill(xpFill, lastStep ? _xpToFill : 0f);
            SetXpLabel(lastStep ? _xpToEarned : 0, lastStep ? _xpToSize : _xpFromSize);

            int filledAfter = _previousStars + stepIndex + 1;
            Tween starGain = starRatingDisplay != null
                ? starRatingDisplay.PlayStarGain(filledAfter, Mathf.Max(0.05f, duration))
                : null;
            return starGain;
        }

        private float ResolveXpShareTarget()
        {
            return HasProgressionMilestone ? 1f : _xpToFill;
        }

        private void BuildLayout(Sprite rowBackground, Sprite barSprite, TMP_FontAsset font)
        {
            LayoutElement layout = GetComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.preferredHeight = RowHeight;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 0f;

            panelBackground = GetComponent<Image>();
            panelBackground.sprite = rowBackground;
            panelBackground.color = PanelColor;
            panelBackground.type = rowBackground != null ? Image.Type.Sliced : Image.Type.Simple;
            panelBackground.pixelsPerUnitMultiplier = 0.2f;
            panelBackground.raycastTarget = false;

            typeIcon = CreateImage("TypeIcon", transform, TypePlaceholder, null, 1f);
            RectTransform iconRect = typeIcon.rectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(32f, 0f);
            iconRect.sizeDelta = new Vector2(100f, 100f);
            typeIcon.preserveAspect = true;

            nameLabel = CreateText("Name", transform, font, 36f, TextAlignmentOptions.Left);
            RectTransform nameRect = nameLabel.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(-135.5f, -22f);
            nameRect.sizeDelta = new Vector2(-589f, 60f);

            RectTransform starRoot = CreateRect("StarRating", transform);
            starRoot.anchorMin = new Vector2(0f, 1f);
            starRoot.anchorMax = new Vector2(1f, 1f);
            starRoot.pivot = new Vector2(1f, 1f);
            starRoot.anchoredPosition = new Vector2(-300f, -34f);
            starRoot.sizeDelta = new Vector2(-884f, 28f);
            starRatingDisplay = starRoot.gameObject.AddComponent<CompactStarRatingDisplay>();

            RectTransform cycleRoot = CreateRect("CycleIcon", transform);
            cycleRoot.anchorMin = new Vector2(1f, 0.5f);
            cycleRoot.anchorMax = new Vector2(1f, 0.5f);
            cycleRoot.pivot = new Vector2(1f, 0.5f);
            cycleRoot.anchoredPosition = new Vector2(-38f, 0f);
            cycleRoot.sizeDelta = new Vector2(108f, 108f);
            cycleIconDisplay = cycleRoot.gameObject.AddComponent<CycleIconDisplay>();
            cycleIconDisplay.EnsureDefaultLayoutForEditor();

            hpBarBackground = CreateImage("HpBarBackground", transform, new Color(0.08f, 0.12f, 0.1f, 1f), barSprite, 0.2f);
            RectTransform hpBg = hpBarBackground.rectTransform;
            hpBg.anchorMin = new Vector2(0f, 0.5f);
            hpBg.anchorMax = new Vector2(1f, 0.5f);
            hpBg.pivot = new Vector2(0.5f, 0f);
            hpBg.anchoredPosition = new Vector2(-16f, -36f);
            hpBg.sizeDelta = new Vector2(-344f, 44f);

            hpFill = CreateImage("HpBarFill", hpBarBackground.transform, HpGreen, barSprite, 0.2f);
            Stretch(hpFill.rectTransform);
            ConfigureBarFill(hpFill);

            hpLabel = CreateText("HpLabel", hpBarBackground.transform, font, 18f, TextAlignmentOptions.Center);
            Stretch(hpLabel.rectTransform);
            hpLabel.raycastTarget = false;

            Image xpBg = CreateImage("XpBarBackground", transform, XpTrack, barSprite, 0.2f);
            RectTransform xpBgRect = xpBg.rectTransform;
            xpBgRect.anchorMin = new Vector2(0f, 0f);
            xpBgRect.anchorMax = new Vector2(1f, 0f);
            xpBgRect.pivot = new Vector2(0.5f, 0f);
            xpBgRect.anchoredPosition = new Vector2(-16f, 24f);
            xpBgRect.sizeDelta = new Vector2(-344f, 26f);

            xpFill = CreateImage("XpBarFill", xpBg.transform, XpOrange, barSprite, 0.2f);
            Stretch(xpFill.rectTransform);
            ConfigureBarFill(xpFill);

            xpLabel = CreateText("XpLabel", xpBg.transform, font, 16f, TextAlignmentOptions.Center);
            Stretch(xpLabel.rectTransform);
            xpLabel.raycastTarget = false;

            folioChanceLabel = CreateText("FolioChance", transform, font, 17f, TextAlignmentOptions.Right);
            RectTransform chanceRect = folioChanceLabel.rectTransform;
            chanceRect.anchorMin = new Vector2(0.45f, 0f);
            chanceRect.anchorMax = new Vector2(1f, 0f);
            chanceRect.pivot = new Vector2(1f, 0f);
            chanceRect.anchoredPosition = new Vector2(-170f, 54f);
            chanceRect.sizeDelta = new Vector2(-12f, 26f);
            folioChanceLabel.color = new Color(0.93f, 0.86f, 0.68f, 1f);
            folioChanceLabel.raycastTarget = false;
        }

        private void SetXpLabel(int earned, int required)
        {
            if (xpLabel == null)
                return;
            xpLabel.text = required > 0
                ? $"{Mathf.Max(0, earned)} / {required}"
                : "";
        }

        private void ApplyHp(float fill01, string label, bool fainted)
        {
            if (hpBarBackground != null)
                hpBarBackground.color = fainted ? HpMissingRed : new Color(0.08f, 0.12f, 0.1f, 1f);
            SetBarFill(hpFill, fill01);
            if (hpFill != null)
            {
                hpFill.color = fainted && fill01 <= 0.001f ? HpMissingRed : HpGreen;
                hpFill.enabled = fill01 > 0.001f;
            }

            if (hpLabel != null)
                hpLabel.text = label;
        }

        private static Tween DOFill(Image fill, float target, float duration)
        {
            float start = GetBarFill(fill);
            return DOVirtual.Float(start, Mathf.Clamp01(target), Mathf.Max(0.01f, duration), value =>
            {
                SetBarFill(fill, value);
            }).SetEase(Ease.OutCubic).SetTarget(fill);
        }

        private static float GetBarFill(Image fill)
        {
            if (fill == null)
                return 0f;
            return Mathf.Clamp01(fill.rectTransform.anchorMax.x);
        }

        private static void SetBarFill(Image fill, float fill01)
        {
            if (fill == null)
                return;
            ConfigureBarFill(fill);
            float clamped = Mathf.Clamp01(fill01);
            RectTransform rect = fill.rectTransform;
            rect.anchorMax = new Vector2(clamped, rect.anchorMax.y);
            fill.enabled = clamped > 0.001f;
        }

        private static void ConfigureBarFill(Image fill)
        {
            if (fill == null)
                return;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            bool sliced = fill.sprite != null && fill.sprite.border.sqrMagnitude > 0f;
            fill.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            RectTransform rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
        }

        private static Image CreateImage(string name, Transform parent, Color color, Sprite sprite, float ppu)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.sprite = sprite;
            image.type = sprite != null && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            image.pixelsPerUnitMultiplier = ppu;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            float size,
            TextAlignmentOptions alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            if (font != null)
                text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
