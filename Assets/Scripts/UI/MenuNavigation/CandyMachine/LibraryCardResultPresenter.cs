using System.Collections.Generic;
using Chainbound.Combat;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation
{
    /// <summary>
    /// Live library-card reveal. Printed chrome is CardFace. Move, mod, and stats ink
    /// are stamp-strip sprites from LibraryCardStampStrip (ink.json distress), placed
    /// from LibraryCardLayout (layout.json). Known fills a blank card. Stamp slides in
    /// with the host already painted and only animates new mod ink.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LibraryCardResultPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private Image cardFace;
        [SerializeField] private Sprite learnedCardFace;
        [SerializeField] private Image typeIcon;
        [SerializeField] private Image moveNameStamp;
        [SerializeField] private Image statsStamp;
        [SerializeField] private Image mod1Stamp;
        [SerializeField] private Image mod2Stamp;
        [SerializeField] private Image effectStamp;
        [SerializeField] private Image moveDot;
        [SerializeField] private Image mod1Dot;
        [SerializeField] private Image mod2Dot;
        [SerializeField] private Image outlineFlash;
        [SerializeField] private TMP_FontAsset stampFont;

        [SerializeField] private float entranceOffsetY = 1040f;
        [SerializeField] private float entranceDuration = 0.64f;
        [SerializeField] private float entranceTiltMin = -18f;
        [SerializeField] private float entranceTiltMax = 18f;
        [SerializeField] private float settleTiltMin = -6f;
        [SerializeField] private float settleTiltMax = 6f;

        const float RevealLeadIn = 0.14f;
        const float PressDuration = 0.28f;
        const float InkDuration = 0.46f;
        const float FlashFadeDuration = 1.0f;
        const float StageTail = 0.24f;

        static Sprite circleSprite;

        public float ContentDelay { get; private set; }

        readonly List<Sprite> spawned = new List<Sprite>();
        Sequence stageSequence;
        Tween flashTween;
        CandyMachineRevealData bound;
        bool entrancePlayed;
        float fitScale = 1f;
        Sprite rolledCardFace;

        public void Bind(
            RectTransform root,
            Image face,
            Image type,
            Image moveName,
            Image stats,
            Image mod1,
            Image mod2,
            Image effect,
            Image moveRarity,
            Image mod1Rarity,
            Image mod2Rarity,
            Image flash,
            TMP_FontAsset font)
        {
            cardRoot = root;
            cardFace = face;
            typeIcon = type;
            moveNameStamp = moveName;
            statsStamp = stats;
            mod1Stamp = mod1;
            mod2Stamp = mod2;
            effectStamp = effect;
            moveDot = moveRarity;
            mod1Dot = mod1Rarity;
            mod2Dot = mod2Rarity;
            outlineFlash = flash;
            if (font != null)
                stampFont = font;
            ApplyLayout();
        }

        public void SetLearnedCardFace(Sprite learned)
        {
            learnedCardFace = learned;
        }

        public void ApplyLayout()
        {
            if (cardRoot != null)
            {
                cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
                cardRoot.pivot = new Vector2(0.5f, 0.5f);
                cardRoot.sizeDelta = new Vector2(LibraryCardLayout.CardWidth, LibraryCardLayout.CardHeight);
            }

            if (cardFace != null)
            {
                RectTransform face = cardFace.rectTransform;
                face.anchorMin = Vector2.zero;
                face.anchorMax = Vector2.one;
                face.offsetMin = Vector2.zero;
                face.offsetMax = Vector2.zero;
                cardFace.preserveAspect = true;
                cardFace.raycastTarget = false;
            }

            PlaceType(typeIcon);
            LibraryCardLayout.PlaceTopLeft(moveNameStamp != null ? moveNameStamp.rectTransform : null, LibraryCardLayout.NameOrigin, LibraryCardLayout.NameTilt);
            LibraryCardLayout.PlaceTopLeft(statsStamp != null ? statsStamp.rectTransform : null, LibraryCardLayout.StatsOrigin, LibraryCardLayout.StatsTilt);
            LibraryCardLayout.PlaceTopLeft(mod1Stamp != null ? mod1Stamp.rectTransform : null, LibraryCardLayout.Mod1Origin, LibraryCardLayout.Mod1Tilt);
            LibraryCardLayout.PlaceTopLeft(mod2Stamp != null ? mod2Stamp.rectTransform : null, LibraryCardLayout.Mod2Origin, LibraryCardLayout.Mod2Tilt);
            LibraryCardLayout.PlaceTopLeft(effectStamp != null ? effectStamp.rectTransform : null, LibraryCardLayout.EffectOrigin, LibraryCardLayout.Mod2Tilt);
            LibraryCardLayout.PlaceCenter(moveDot != null ? moveDot.rectTransform : null, LibraryCardLayout.MoveWell, 0f, LibraryCardLayout.DotInset);
            LibraryCardLayout.PlaceCenter(mod1Dot != null ? mod1Dot.rectTransform : null, LibraryCardLayout.Mod1Well, 0f, LibraryCardLayout.DotInset);
            LibraryCardLayout.PlaceCenter(mod2Dot != null ? mod2Dot.rectTransform : null, LibraryCardLayout.Mod2Well, 0f, LibraryCardLayout.DotInset);
            if (outlineFlash != null)
            {
                RectTransform flash = outlineFlash.rectTransform;
                flash.anchorMin = Vector2.zero;
                flash.anchorMax = Vector2.one;
                flash.offsetMin = Vector2.zero;
                flash.offsetMax = Vector2.zero;
                outlineFlash.raycastTarget = false;
            }
        }

        public void Prepare(CandyMachineRevealData data)
        {
            bound = data;
            entrancePlayed = false;
            ReleaseStrips();
            ApplyLayout();
            ApplyCardFace();
            FitCard();
            HideAllInk();
            if (data != null && data.IsStamp)
                PaintHost(1f);
            PoseOffscreen();
        }

        public float SetStage(int stage)
        {
            stageSequence?.Kill(false);
            stageSequence = null;
            if (stage <= 0)
            {
                HideFlash();
                return 0f;
            }

            stageSequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            float entrance = 0f;
            if (!entrancePlayed)
            {
                entrancePlayed = true;
                AppendEntrance(stageSequence);
                entrance = entranceDuration + 0.12f;
            }

            bool stampSlideOnly = bound != null && bound.IsStamp && stage == 1;
            ContentDelay = stampSlideOnly ? entrance : entrance + RevealLeadIn;
            if (!stampSlideOnly)
                stageSequence.AppendInterval(RevealLeadIn);
            if (!stampSlideOnly)
            {
                if (bound != null && bound.IsStamp)
                    AppendStampStage(stageSequence, stage);
                else
                    AppendKnownStage(stageSequence, stage);
            }

            stageSequence.AppendInterval(StageTail);
            float duration = stageSequence.Duration(false);
            stageSequence.OnComplete(() => stageSequence = null);
            return duration;
        }

        public void Clear()
        {
            DOTween.Kill(this);
            stageSequence = null;
            flashTween = null;
            bound = null;
            entrancePlayed = false;
            HideAllInk();
            ReleaseStrips();
            if (cardRoot != null)
            {
                cardRoot.anchoredPosition = Vector2.zero;
                cardRoot.localRotation = Quaternion.identity;
                cardRoot.localScale = Vector3.one;
            }
        }

        void AppendKnownStage(Sequence sequence, int stage)
        {
            RarityTier tier = bound != null ? bound.MoveRarity : RarityTier.Common;
            if (stage == 1)
            {
                ShowType(sequence, tier);
                return;
            }

            if (stage == 2)
            {
                ShowDot(sequence, moveDot, tier);
                return;
            }

            var beat = DOTween.Sequence();
            ShowStrip(beat, moveNameStamp, bound != null ? bound.MoveName : null, LibraryCardLayout.NameFontSize, LibraryCardLayout.NameMaxWidth, 11, tier, distress: !UsesLearnedFace());
            ShowStatsStrip(beat, bound != null ? bound.StatsInk : null, tier, parallel: true, distress: !UsesLearnedFace());
            sequence.Append(beat);
        }

        void AppendStampStage(Sequence sequence, int stage)
        {
            if (bound == null || stage <= 1)
                return;

            if (stage == 2)
            {
                var beat = DOTween.Sequence();
                if (bound.HasMod1Rarity)
                    ShowDot(beat, mod1Dot, bound.Mod1Rarity);
                if (bound.HasMod2Rarity)
                    ShowDot(beat, mod2Dot, bound.Mod2Rarity);
                sequence.Append(beat);
                return;
            }

            var names = DOTween.Sequence();
            if (bound.HasMod1Rarity)
            {
                ShowStrip(
                    names,
                    mod1Stamp,
                    LibraryCardLayout.FormatModStamp(bound.Mod1Name, bound.Mod1Effect),
                    LibraryCardLayout.ModFontSize,
                    LibraryCardLayout.ModMaxWidth,
                    41,
                    bound.Mod1Rarity,
                    distress: true);
            }

            if (bound.HasMod2Rarity)
            {
                ShowStrip(
                    names,
                    mod2Stamp,
                    LibraryCardLayout.FormatModStamp(bound.Mod2Name, bound.Mod2Effect),
                    LibraryCardLayout.ModFontSize,
                    LibraryCardLayout.ModMaxWidth,
                    42,
                    bound.Mod2Rarity,
                    parallel: bound.HasMod1Rarity,
                    distress: true);
            }

            sequence.Append(names);
        }

        void PaintHost(float alpha)
        {
            if (bound == null)
                return;
            bool hostDistress = !UsesLearnedFace();
            AssignStrip(moveNameStamp, bound.MoveName, LibraryCardLayout.NameFontSize, LibraryCardLayout.NameMaxWidth, 11, distress: hostDistress);
            AssignStatsStrip(bound.StatsInk, hostDistress);
            if (typeIcon != null)
            {
                typeIcon.sprite = bound.TypeIcon;
                typeIcon.enabled = bound.TypeIcon != null;
                typeIcon.preserveAspect = true;
                typeIcon.color = new Color(1f, 1f, 1f, alpha);
                SetVisible(typeIcon, bound.TypeIcon != null);
            }

            PaintDot(moveDot, bound.MoveRarity, alpha);
        }

        void ShowType(Sequence sequence, RarityTier flashTier)
        {
            if (typeIcon == null || bound == null || bound.TypeIcon == null)
                return;
            typeIcon.sprite = bound.TypeIcon;
            typeIcon.enabled = true;
            typeIcon.preserveAspect = true;
            SetVisible(typeIcon, true);
            Color color = typeIcon.color;
            color.a = 0f;
            typeIcon.color = color;
            RectTransform rect = typeIcon.rectTransform;
            rect.localScale = Vector3.one * 0.85f;
            sequence.Append(rect.DOScale(1.12f, PressDuration * 0.55f).SetEase(Ease.OutCubic));
            sequence.Append(rect.DOScale(1f, PressDuration * 0.45f).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => PlayImpact(flashTier));
            sequence.Append(typeIcon.DOFade(1f, InkDuration).SetEase(Ease.InOutSine));
        }

        void ShowDot(Sequence sequence, Image dot, RarityTier tier)
        {
            if (dot == null)
                return;
            PaintDot(dot, tier, 0f);
            RectTransform rect = dot.rectTransform;
            rect.localScale = Vector3.one * 0.85f;
            sequence.Append(rect.DOScale(1.15f, PressDuration * 0.55f).SetEase(Ease.OutCubic));
            sequence.Append(rect.DOScale(1f, PressDuration * 0.45f).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => PlayImpact(tier));
            sequence.Append(dot.DOFade(1f, InkDuration).SetEase(Ease.InOutSine));
        }

        void ShowStrip(Sequence sequence, Image target, string text, float fontSize, float maxWidth, int seed, RarityTier flashTier, bool parallel = false, float lineHeight = 0f, bool distress = true)
        {
            if (target == null || string.IsNullOrWhiteSpace(text))
                return;
            AssignStrip(target, text, fontSize, maxWidth, seed, lineHeight, distress);
            if (target.sprite == null)
                return;
            Color color = target.color;
            color.a = 0f;
            target.color = color;
            RectTransform rect = target.rectTransform;
            Vector3 rest = Vector3.one;
            rect.localScale = rest * 0.85f;
            if (parallel)
            {
                sequence.Join(rect.DOScale(rest * 1.08f, PressDuration * 0.55f).SetEase(Ease.OutCubic));
                sequence.Join(rect.DOScale(rest, PressDuration * 0.45f).SetEase(Ease.OutQuad).SetDelay(PressDuration * 0.55f));
                sequence.Join(target.DOFade(1f, InkDuration).SetEase(Ease.InOutSine).SetDelay(PressDuration));
                return;
            }

            sequence.Append(rect.DOScale(rest * 1.08f, PressDuration * 0.55f).SetEase(Ease.OutCubic));
            sequence.Append(rect.DOScale(rest, PressDuration * 0.45f).SetEase(Ease.OutQuad));
            sequence.AppendCallback(() => PlayImpact(flashTier));
            sequence.Append(target.DOFade(1f, InkDuration).SetEase(Ease.InOutSine));
        }

        void ShowStatsStrip(Sequence sequence, string text, RarityTier flashTier, bool parallel = false, bool distress = true)
        {
            if (LibraryCardLayout.IsEffectStatsInk(text))
            {
                ShowStrip(
                    sequence,
                    statsStamp,
                    text,
                    LibraryCardLayout.EffectStatsFontSize,
                    LibraryCardLayout.NameMaxWidth,
                    23,
                    flashTier,
                    parallel,
                    LibraryCardLayout.EffectLineHeight,
                    distress);
                return;
            }

            ShowStrip(sequence, statsStamp, text, LibraryCardLayout.StatsFontSize, LibraryCardLayout.NameMaxWidth, 23, flashTier, parallel, distress: distress);
        }

        void AssignStatsStrip(string text, bool distress = true)
        {
            if (LibraryCardLayout.IsEffectStatsInk(text))
            {
                AssignStrip(
                    statsStamp,
                    text,
                    LibraryCardLayout.EffectStatsFontSize,
                    LibraryCardLayout.NameMaxWidth,
                    23,
                    LibraryCardLayout.EffectLineHeight,
                    distress);
                return;
            }

            AssignStrip(statsStamp, text, LibraryCardLayout.StatsFontSize, LibraryCardLayout.NameMaxWidth, 23, distress: distress);
        }

        void AssignStrip(Image target, string text, float fontSize, float maxWidth, int seed, float lineHeight = 0f, bool distress = true)
        {
            if (target == null)
                return;
            Color32 ink = UsesLearnedFace() ? LibraryCardFaces.LearnedInk : LibraryCardFaces.SageInk;
            Sprite sprite = LibraryCardStampStrip.Bake(text, fontSize, maxWidth, seed, stampFont, lineHeight, ink, distress);
            if (sprite == null)
            {
                SetVisible(target, false);
                return;
            }

            spawned.Add(sprite);
            target.sprite = sprite;
            target.color = Color.white;
            target.preserveAspect = false;
            target.raycastTarget = false;
            target.rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            SetVisible(target, true);
        }

        void PaintDot(Image dot, RarityTier tier, float alpha)
        {
            if (dot == null)
                return;
            if (circleSprite == null)
                circleSprite = CreateCircle();
            dot.sprite = circleSprite;
            Color color = LibraryCardRarityColors.For(tier);
            color.a = alpha;
            dot.color = color;
            dot.raycastTarget = false;
            SetVisible(dot, true);
        }

        void AppendEntrance(Sequence sequence)
        {
            if (cardRoot == null)
                return;
            float startTilt = Random.Range(entranceTiltMin, entranceTiltMax);
            float settleTilt = Random.Range(settleTiltMin, settleTiltMax);
            cardRoot.anchoredPosition = new Vector2(0f, entranceOffsetY);
            cardRoot.localRotation = Quaternion.Euler(0f, 0f, startTilt);
            cardRoot.localScale = Vector3.one * (fitScale * 0.98f);
            sequence.Append(cardRoot.DOAnchorPosY(0f, entranceDuration).SetEase(Ease.OutCubic));
            sequence.Join(cardRoot.DOLocalRotate(new Vector3(0f, 0f, settleTilt), entranceDuration).SetEase(Ease.OutQuad));
            sequence.Join(cardRoot.DOScale(fitScale * 1.02f, entranceDuration * 0.72f).SetEase(Ease.OutCubic));
            sequence.Append(cardRoot.DOScale(fitScale, 0.12f).SetEase(Ease.OutQuad));
        }

        bool UsesLearnedFace()
        {
            return bound != null && bound.UsesLearnedFace;
        }

        void ApplyCardFace()
        {
            if (cardFace == null)
                return;
            if (rolledCardFace == null)
                rolledCardFace = cardFace.sprite;
            bool learned = UsesLearnedFace() && learnedCardFace != null;
            cardFace.sprite = learned ? learnedCardFace : rolledCardFace;
        }

        void PoseOffscreen()
        {
            if (cardRoot == null)
                return;
            cardRoot.anchoredPosition = new Vector2(0f, entranceOffsetY);
            cardRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(entranceTiltMin, entranceTiltMax));
        }

        void PlayImpact(RarityTier tier)
        {
            if (cardRoot != null)
                cardRoot.DOShakeAnchorPos(0.28f, new Vector2(10f, 6f), 16, 65f, false, true)
                    .SetUpdate(true)
                    .SetTarget(this);

            if (outlineFlash == null)
                return;
            flashTween?.Kill(false);
            outlineFlash.gameObject.SetActive(true);
            Color color = LibraryCardRarityColors.For(tier);
            color.a = 0.9f;
            outlineFlash.color = color;
            flashTween = outlineFlash.DOFade(0f, FlashFadeDuration)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .SetTarget(this)
                .OnComplete(() =>
                {
                    flashTween = null;
                    HideFlash();
                });
        }

        void HideFlash()
        {
            if (outlineFlash == null)
                return;
            Color color = outlineFlash.color;
            color.a = 0f;
            outlineFlash.color = color;
        }

        void HideAllInk()
        {
            HideImage(typeIcon);
            HideImage(moveNameStamp);
            HideImage(statsStamp);
            HideImage(mod1Stamp);
            HideImage(mod2Stamp);
            HideImage(effectStamp);
            HideImage(moveDot);
            HideImage(mod1Dot);
            HideImage(mod2Dot);
            HideFlash();
        }

        static void HideImage(Image image)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = 0f;
            image.color = color;
            image.enabled = false;
        }

        static void SetVisible(Image image, bool visible)
        {
            if (image == null)
                return;
            image.enabled = visible;
            image.gameObject.SetActive(true);
        }

        void FitCard()
        {
            if (cardRoot == null || cardRoot.parent is not RectTransform parent)
                return;
            float width = parent.rect.width;
            if (width < 32f)
                return;
            fitScale = Mathf.Max(0.2f, Mathf.Min(1f, (width - 24f) / LibraryCardLayout.CardWidth));
            cardRoot.localScale = Vector3.one * fitScale;
        }

        void PlaceType(Image icon)
        {
            if (icon == null)
                return;
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = LibraryCardLayout.CenterOf(LibraryCardLayout.TypeInner);
            rect.sizeDelta = new Vector2(LibraryCardLayout.TypeInner.width, LibraryCardLayout.TypeInner.height);
            rect.localRotation = Quaternion.Euler(0f, 0f, LibraryCardLayout.TypeTilt);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        void ReleaseStrips()
        {
            if (moveNameStamp != null) moveNameStamp.sprite = null;
            if (statsStamp != null) statsStamp.sprite = null;
            if (mod1Stamp != null) mod1Stamp.sprite = null;
            if (mod2Stamp != null) mod2Stamp.sprite = null;
            if (effectStamp != null) effectStamp.sprite = null;
            for (int i = 0; i < spawned.Count; i++)
                LibraryCardStampStrip.Release(spawned[i]);
            spawned.Clear();
        }

        static Sprite CreateCircle()
        {
            const int size = 32;
            var pixels = new Color32[size * size];
            float r = size * 0.5f - 1f;
            Vector2 c = new Vector2(r, r);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    byte a = d <= r ? (byte)255 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        void OnDisable()
        {
            DOTween.Kill(this);
            stageSequence = null;
            flashTween = null;
        }

        void OnDestroy()
        {
            ReleaseStrips();
        }
    }
}
