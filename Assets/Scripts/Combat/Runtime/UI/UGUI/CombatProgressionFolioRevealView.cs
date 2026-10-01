using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Post-battle folio overlay. An issued Known move prints onto the library card.
    /// Missed, pending, and interrupted grants keep the short paper notice.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatProgressionFolioRevealView : MonoBehaviour
    {
        const float EntranceOffsetY = 1040f;
        const float EntranceDuration = 0.64f;
        const float BeatGapSeconds = 1.7f;
        const float DissolveSeconds = 0.46f;
        const float OutlineFlashSeconds = 1f;

        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private RectTransform document;
        [SerializeField] private Image documentImage;
        [SerializeField] private TextMeshProUGUI milestoneLabel;
        [SerializeField] private TextMeshProUGUI outcomeLabel;
        [SerializeField] private Image raritySeal;
        [SerializeField] private TextMeshProUGUI rarityLabel;
        [SerializeField] private Image typeIcon;
        [SerializeField] private TextMeshProUGUI typeLabel;
        [SerializeField] private TextMeshProUGUI moveNameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI footerLabel;
        [SerializeField] private CanvasGroup rarityGroup;
        [SerializeField] private CanvasGroup typeGroup;
        [SerializeField] private CanvasGroup detailsGroup;
        [SerializeField] private CanvasGroup footerGroup;
        [SerializeField] private Button accelerationButton;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private Image cardFace;
        [SerializeField] private Image cardTypeIcon;
        [SerializeField] private Image moveNameStamp;
        [SerializeField] private Image statsStamp;
        [SerializeField] private Image moveDot;
        [SerializeField] private Image outlineFlash;
        [SerializeField] private Button continueButton;

        private readonly List<Sprite> spawnedStrips = new List<Sprite>();
        private Tween activeTween;
        private Tween outlinePulse;
        private float accelerationUnlockTime;
        private float fitScale = 1f;
        private bool sequenceCompleted;
        private MonoBehaviour cachedAudioBridgeBehaviour;
        private static Sprite circleSprite;

        public bool AwaitingContinue { get; private set; }
        public bool CanAccelerate => !AwaitingContinue && activeTween != null && activeTween.IsActive() && Time.unscaledTime >= accelerationUnlockTime;

        public static CombatProgressionFolioRevealView Create(Transform parent, TMP_FontAsset font)
        {
            GameObject root = new GameObject("ProgressionFolioOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(CombatProgressionFolioRevealView));
            root.transform.SetParent(parent, false);
            CombatProgressionFolioRevealView view = root.GetComponent<CombatProgressionFolioRevealView>();
            view.BuildLayout(font);
            root.SetActive(false);
            return view;
        }

        public void EnsureAuthoredLayout(TMP_FontAsset font)
        {
            if (rootGroup == null || document == null || milestoneLabel == null)
                BuildLayout(font);
            EnsureCard();
            EnsureContinue();
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                    audioSource = gameObject.AddComponent<AudioSource>();
            }
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            if (accelerationButton == null)
            {
                accelerationButton = GetComponent<Button>();
                if (accelerationButton == null)
                    accelerationButton = gameObject.AddComponent<Button>();
            }
            accelerationButton.transition = Selectable.Transition.None;
            WireAccelerationButton();
        }

        public Tween Play(
            ProgressionGrantResult grant,
            MoveDefinitionSO move,
            TypeIconPaletteSO typePalette,
            ProgressionFolioPresentationSO profile)
        {
            if (grant == null)
                return null;
            profile = profile != null ? profile : ProgressionFolioPresentationSO.CreateRuntimeDefault();
            EnsureAuthoredLayout(null);
            KillActive();
            AwaitingContinue = false;
            sequenceCompleted = false;
            SetContinueVisible(false);
            gameObject.SetActive(true);
            rootGroup.alpha = 0f;
            rootGroup.blocksRaycasts = true;
            if (accelerationButton != null)
                accelerationButton.interactable = true;

            bool issued = grant.outcome == ProgressionGrantOutcome.FolioIssued;
            if (document != null)
                document.gameObject.SetActive(!issued);
            if (cardRoot != null)
                cardRoot.gameObject.SetActive(issued);

            Sequence sequence = issued
                ? BuildIssuedSequence(grant, move, typePalette, profile)
                : BuildNoticeSequence(grant, move, typePalette, profile);
            sequence.OnComplete(ShowContinue);
            sequence.OnKill(HandleSequenceKilled);
            activeTween = sequence;
            accelerationUnlockTime = Time.unscaledTime + Mathf.Max(0f, profile.minimumUnskippableSeconds);
            sequence.Pause();
            return sequence;
        }

        public void Accelerate()
        {
            if (!CanAccelerate)
                return;
            activeTween.timeScale = Mathf.Max(activeTween.timeScale, 4f);
        }

        public void ConfirmContinue()
        {
            if (!AwaitingContinue)
                return;
            AwaitingContinue = false;
            HideAndRestore();
        }

        private Sequence BuildIssuedSequence(
            ProgressionGrantResult grant,
            MoveDefinitionSO move,
            TypeIconPaletteSO typePalette,
            ProgressionFolioPresentationSO profile)
        {
            BindCard(grant, move, typePalette, profile);
            FitCard();
            HidePrintedInk();
            PoseCardOffscreen();
            rootGroup.alpha = 1f;

            RarityTier rarity = move != null ? move.rarity : RarityTier.Common;
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            sequence.AppendCallback(() => PlaySound(profile.cardSlideSound));
            AppendEntrance(sequence);
            AppendDissolveBeat(sequence, FadeImages(DissolveSeconds, cardTypeIcon), profile, rarity);
            sequence.AppendInterval(BeatGapSeconds);
            sequence.AppendCallback(() => PlaySound(profile.cardRiserSound));
            AppendDissolveBeat(sequence, FadeImages(DissolveSeconds, moveDot), profile, rarity);
            sequence.AppendInterval(BeatGapSeconds);
            AppendDissolveBeat(sequence, FadeImages(DissolveSeconds, moveNameStamp, statsStamp), profile, rarity);
            return sequence;
        }

        private void AppendDissolveBeat(Sequence sequence, Tween dissolve, ProgressionFolioPresentationSO profile, RarityTier rarity)
        {
            sequence.AppendCallback(() =>
            {
                PlaySound(profile.cardPrintSound);
                FlashOutline(rarity);
            });
            if (dissolve != null)
                sequence.Append(dissolve);
        }

        private Sequence BuildNoticeSequence(
            ProgressionGrantResult grant,
            MoveDefinitionSO move,
            TypeIconPaletteSO typePalette,
            ProgressionFolioPresentationSO profile)
        {
            BindNotice(grant, move, typePalette, profile);
            document.localScale = Vector3.one * 0.92f;
            SetGroupAlpha(rarityGroup, 0f);
            SetGroupAlpha(typeGroup, 0f);
            SetGroupAlpha(detailsGroup, 0f);
            SetGroupAlpha(footerGroup, 0f);

            float materialize = Mathf.Max(0.1f, profile.materializeSeconds);
            Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
            sequence.Append(rootGroup.DOFade(1f, materialize * 0.45f).SetEase(Ease.OutSine));
            sequence.Join(document.DOScale(1f, materialize).SetEase(Ease.OutCubic));
            sequence.AppendCallback(() => PlayOutcomeAudio(grant.outcome, profile));
            sequence.Append(detailsGroup.DOFade(1f, 0.22f));
            sequence.Append(footerGroup.DOFade(1f, 0.16f));
            return sequence;
        }

        private void BindCard(ProgressionGrantResult grant, MoveDefinitionSO move, TypeIconPaletteSO typePalette, ProgressionFolioPresentationSO profile)
        {
            if (cardFace != null)
            {
                cardFace.sprite = profile.libraryCardFront;
                cardFace.color = Color.white;
                cardFace.preserveAspect = true;
            }

            if (outlineFlash != null)
            {
                outlineFlash.sprite = profile.libraryCardOutline;
                outlineFlash.preserveAspect = true;
                Color hidden = LibraryCardRarityColors.For(move != null ? move.rarity : RarityTier.Common);
                hidden.a = 0f;
                outlineFlash.color = hidden;
            }

            ChainlingType moveType = move != null ? move.baseType : ChainlingType.None;
            Sprite icon = typePalette != null ? typePalette.GetIcon(moveType) : null;
            if (cardTypeIcon != null)
            {
                cardTypeIcon.sprite = icon;
                cardTypeIcon.enabled = icon != null;
                cardTypeIcon.preserveAspect = true;
                cardTypeIcon.color = new Color(1f, 1f, 1f, 0f);
            }

            string moveName = move != null && !string.IsNullOrWhiteSpace(move.displayName)
                ? move.displayName
                : !string.IsNullOrWhiteSpace(grant.moveName)
                    ? grant.moveName
                    : grant.moveId;
            AssignStrip(
                moveNameStamp,
                string.IsNullOrWhiteSpace(moveName) ? "KNOWN MOVE" : moveName,
                LibraryCardLayout.NameFontSize,
                LibraryCardLayout.NameMaxWidth,
                11);

            string stats = move != null ? LibraryCardLayout.FormatStatsInk(move, move.description) : string.Empty;
            bool effectStats = LibraryCardLayout.IsEffectStatsInk(stats);
            AssignStrip(
                statsStamp,
                stats,
                effectStats ? LibraryCardLayout.EffectStatsFontSize : LibraryCardLayout.StatsFontSize,
                LibraryCardLayout.NameMaxWidth,
                23,
                effectStats ? LibraryCardLayout.EffectLineHeight : 0f);

            if (moveDot != null)
            {
                if (circleSprite == null)
                    circleSprite = CreateCircle();
                moveDot.sprite = circleSprite;
                Color color = LibraryCardRarityColors.For(move != null ? move.rarity : RarityTier.Common);
                color.a = 0f;
                moveDot.color = color;
            }
        }

        private void BindNotice(ProgressionGrantResult grant, MoveDefinitionSO move, TypeIconPaletteSO typePalette, ProgressionFolioPresentationSO profile)
        {
            string chainlingName = string.IsNullOrWhiteSpace(grant.chainlingDisplayName) ? "Chainling" : grant.chainlingDisplayName;
            milestoneLabel.text = grant.eventType == ProgressionGrantEventType.MasteryCycle
                ? profile.masteryRecorded
                : string.Format(profile.starRecordedTemplate, ToRoman(Mathf.Clamp(grant.starIndex, 1, 5)));

            bool interrupted = grant.outcome == ProgressionGrantOutcome.IssuanceError;
            bool pending = grant.outcome == ProgressionGrantOutcome.IssuancePending;
            outcomeLabel.text = interrupted
                ? profile.interruptedHeading
                : pending
                    ? profile.pendingHeading
                    : profile.noFolioHeading;
            outcomeLabel.color = interrupted ? profile.interruptedColor : profile.inkColor;

            if (documentImage != null)
                documentImage.color = profile.paperColor;
            ApplyInk(profile.inkColor);

            raritySeal.color = profile.neutralSealColor;
            rarityLabel.text = "ARCHIVE ENTRY";
            typeLabel.text = "";
            if (typeIcon != null)
            {
                typeIcon.sprite = null;
                typeIcon.enabled = false;
            }

            if (interrupted)
            {
                moveNameLabel.text = "THE FOLIO COULD NOT BE CATALOGUED.";
                descriptionLabel.text = "The issuance record is incomplete and has not been treated as a missed roll.";
                footerLabel.text = "Return to the archive when the connection is restored.";
            }
            else if (pending)
            {
                moveNameLabel.text = "CATALOGUING FOLIO…";
                descriptionLabel.text = "The archive is confirming this milestone’s issuance record.";
                footerLabel.text = "Please keep this page open.";
            }
            else
            {
                moveNameLabel.text = profile.noFolioBody;
                descriptionLabel.text = "";
                footerLabel.text = string.Format(profile.recordEnteredTemplate, chainlingName);
            }

            if (move != null && typePalette != null && typeIcon != null)
                typeIcon.sprite = typePalette.GetIcon(move.baseType);
        }

        private void AppendEntrance(Sequence sequence)
        {
            if (cardRoot == null)
                return;
            float startTilt = Random.Range(-18f, 18f);
            float settleTilt = Random.Range(-6f, 6f);
            cardRoot.anchoredPosition = new Vector2(0f, EntranceOffsetY);
            cardRoot.localRotation = Quaternion.Euler(0f, 0f, startTilt);
            cardRoot.localScale = Vector3.one * (fitScale * 0.98f);
            sequence.Append(cardRoot.DOAnchorPosY(0f, EntranceDuration).SetEase(Ease.OutCubic));
            sequence.Join(cardRoot.DOLocalRotate(new Vector3(0f, 0f, settleTilt), EntranceDuration).SetEase(Ease.OutQuad));
            sequence.Join(cardRoot.DOScale(fitScale * 1.02f, EntranceDuration * 0.72f).SetEase(Ease.OutCubic));
            sequence.Append(cardRoot.DOScale(fitScale, 0.12f).SetEase(Ease.OutQuad));
        }

        private Tween FadeImages(float duration, params Image[] images)
        {
            Sequence fade = DOTween.Sequence();
            int joins = 0;
            if (images == null)
                return fade;
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image == null || image.sprite == null)
                    continue;
                fade.Join(image.DOFade(1f, duration).SetEase(Ease.InOutSine));
                joins++;
            }

            return joins > 0 ? fade : null;
        }

        private void FlashOutline(RarityTier rarity)
        {
            if (outlineFlash == null)
                return;
            outlinePulse?.Kill(false);
            outlineFlash.gameObject.SetActive(true);
            Color color = LibraryCardRarityColors.For(rarity);
            color.a = 0.9f;
            outlineFlash.color = color;
            outlinePulse = outlineFlash.DOFade(0f, OutlineFlashSeconds)
                .SetEase(Ease.OutSine)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void ShowContinue()
        {
            sequenceCompleted = true;
            activeTween = null;
            AwaitingContinue = true;
            SetContinueVisible(true);
            if (accelerationButton != null)
            {
                accelerationButton.interactable = true;
                accelerationButton.navigation = new Navigation { mode = Navigation.Mode.None };
            }

            FocusContinueButton();
        }

        private void HandleSequenceKilled()
        {
            if (sequenceCompleted || AwaitingContinue)
                return;
            HideAndRestore();
        }

        private void PlaySound(SoundEventSO sound)
        {
            if (sound == null)
                return;
            ICombatAudioBridge bridge = CombatUiSfxResolver.ResolveAudioBridge(null, ref cachedAudioBridgeBehaviour);
            bridge?.PlayOneShot(sound);
        }

        private void ApplyInk(Color color)
        {
            TextMeshProUGUI[] labels = { milestoneLabel, rarityLabel, typeLabel, moveNameLabel, descriptionLabel, footerLabel };
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] != null)
                    labels[i].color = color;
        }

        private void HideAndRestore()
        {
            activeTween = null;
            outlinePulse?.Kill(false);
            outlinePulse = null;
            AwaitingContinue = false;
            sequenceCompleted = false;
            ReleaseStrips();
            SetContinueVisible(false);
            if (gameObject != null)
                gameObject.SetActive(false);
        }

        private void KillActive()
        {
            sequenceCompleted = false;
            AwaitingContinue = false;
            activeTween?.Kill(false);
            activeTween = null;
            outlinePulse?.Kill(false);
            outlinePulse = null;
            DOTween.Kill(this, false);
            ReleaseStrips();
        }

        private void OnDisable()
        {
            if (activeTween != null || outlinePulse != null)
                KillActive();
        }

        private void BuildLayout(TMP_FontAsset font)
        {
            if (font == null)
                font = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
            RectTransform rootRect = GetComponent<RectTransform>();
            Stretch(rootRect);
            rootGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            Image backdrop = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.035f, 0.04f, 0.045f, 0.92f);
            backdrop.raycastTarget = true;
            accelerationButton = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            accelerationButton.transition = Selectable.Transition.None;
            WireAccelerationButton();

            documentImage = CreateImage("FolioDocument", transform, new Color(0.91f, 0.84f, 0.68f, 1f));
            document = documentImage.rectTransform;
            document.anchorMin = new Vector2(0.5f, 0.5f);
            document.anchorMax = new Vector2(0.5f, 0.5f);
            document.pivot = new Vector2(0.5f, 0.5f);
            document.sizeDelta = new Vector2(860f, 580f);

            milestoneLabel = CreateText("Milestone", document, font, 24f, TextAlignmentOptions.Center, new Vector2(0f, 230f), new Vector2(760f, 48f));
            milestoneLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            outcomeLabel = CreateText("Outcome", document, font, 42f, TextAlignmentOptions.Center, new Vector2(0f, 170f), new Vector2(760f, 66f));
            outcomeLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;

            RectTransform rarityRoot = CreateGroup("RarityReveal", document, out rarityGroup, new Vector2(-245f, 55f), new Vector2(210f, 130f));
            raritySeal = CreateImage("RaritySeal", rarityRoot, new Color(0.39f, 0.32f, 0.22f, 1f));
            Stretch(raritySeal.rectTransform, 18f);
            rarityLabel = CreateText("Rarity", rarityRoot, font, 21f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(185f, 80f));
            rarityLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;

            RectTransform typeRoot = CreateGroup("TypeReveal", document, out typeGroup, new Vector2(245f, 55f), new Vector2(210f, 130f));
            typeIcon = CreateImage("TypeIcon", typeRoot, Color.white);
            RectTransform typeIconRect = typeIcon.rectTransform;
            typeIconRect.anchorMin = typeIconRect.anchorMax = new Vector2(0.5f, 1f);
            typeIconRect.pivot = new Vector2(0.5f, 1f);
            typeIconRect.anchoredPosition = new Vector2(0f, -8f);
            typeIconRect.sizeDelta = new Vector2(64f, 64f);
            typeIcon.preserveAspect = true;
            typeLabel = CreateText("MoveType", typeRoot, font, 20f, TextAlignmentOptions.Center, new Vector2(0f, -40f), new Vector2(200f, 40f));

            RectTransform detailsRoot = CreateGroup("DetailsReveal", document, out detailsGroup, new Vector2(0f, -95f), new Vector2(760f, 190f));
            moveNameLabel = CreateText("MoveName", detailsRoot, font, 34f, TextAlignmentOptions.Center, new Vector2(0f, 48f), new Vector2(720f, 58f));
            moveNameLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            descriptionLabel = CreateText("Description", detailsRoot, font, 22f, TextAlignmentOptions.Center, new Vector2(0f, -30f), new Vector2(690f, 90f));

            RectTransform footerRoot = CreateGroup("FooterReveal", document, out footerGroup, new Vector2(0f, -238f), new Vector2(760f, 64f));
            footerLabel = CreateText("Footer", footerRoot, font, 20f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(740f, 58f));
            EnsureCard();
            EnsureContinue();
        }

        private void EnsureCard()
        {
            if (cardRoot != null && cardFace != null && moveNameStamp != null && statsStamp != null && moveDot != null && outlineFlash != null && cardTypeIcon != null)
            {
                PlaceCardContents();
                return;
            }

            if (cardRoot == null)
            {
                GameObject root = new GameObject("LibraryCardRoot", typeof(RectTransform));
                root.transform.SetParent(transform, false);
                cardRoot = root.GetComponent<RectTransform>();
            }

            cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
            cardRoot.pivot = new Vector2(0.5f, 0.5f);
            cardRoot.sizeDelta = new Vector2(LibraryCardLayout.CardWidth, LibraryCardLayout.CardHeight);
            cardFace = EnsureChildImage(cardRoot, "CardFace", cardFace);
            Stretch(cardFace.rectTransform);
            cardTypeIcon = EnsureChildImage(cardRoot, "TypeWell", cardTypeIcon);
            moveNameStamp = EnsureChildImage(cardRoot, "MoveNameStamp", moveNameStamp);
            statsStamp = EnsureChildImage(cardRoot, "StatsStamp", statsStamp);
            moveDot = EnsureChildImage(cardRoot, "MoveDot", moveDot);
            outlineFlash = EnsureChildImage(cardRoot, "OutlineFlash", outlineFlash);
            Stretch(outlineFlash.rectTransform);
            PlaceCardContents();
            cardRoot.gameObject.SetActive(false);
        }

        private void PlaceCardContents()
        {
            LibraryCardLayout.PlaceTopLeft(moveNameStamp != null ? moveNameStamp.rectTransform : null, LibraryCardLayout.NameOrigin, LibraryCardLayout.NameTilt);
            LibraryCardLayout.PlaceTopLeft(statsStamp != null ? statsStamp.rectTransform : null, LibraryCardLayout.StatsOrigin, LibraryCardLayout.StatsTilt);
            LibraryCardLayout.PlaceCenter(moveDot != null ? moveDot.rectTransform : null, LibraryCardLayout.MoveWell, 0f, LibraryCardLayout.DotInset);
            if (cardTypeIcon != null)
            {
                RectTransform rect = cardTypeIcon.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = LibraryCardLayout.CenterOf(LibraryCardLayout.TypeInner);
                rect.sizeDelta = new Vector2(LibraryCardLayout.TypeInner.width, LibraryCardLayout.TypeInner.height);
                rect.localRotation = Quaternion.Euler(0f, 0f, LibraryCardLayout.TypeTilt);
                cardTypeIcon.preserveAspect = true;
                cardTypeIcon.raycastTarget = false;
            }
        }

        private void EnsureContinue()
        {
            if (continueButton != null)
                return;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("TextMesh Pro/Fonts/PixeloidSans SDF");
            Image image = CreateImage("ContinueButton", transform, new Color(0.91f, 0.84f, 0.68f, 1f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 36f);
            rect.sizeDelta = new Vector2(280f, 64f);
            image.raycastTarget = true;
            TextMeshProUGUI label = CreateText("Label", rect, font, 22f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(260f, 48f));
            label.text = "CONTINUE";
            label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            label.color = new Color(0.13f, 0.105f, 0.075f, 1f);
            continueButton = image.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = image;
            continueButton.transition = Selectable.Transition.None;
            continueButton.onClick.AddListener(ConfirmContinue);
            continueButton.navigation = new Navigation { mode = Navigation.Mode.None };
            image.gameObject.SetActive(false);
        }

        public void FocusContinueButton()
        {
            if (continueButton == null || !continueButton.gameObject.activeInHierarchy)
                return;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
                return;

            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(continueButton.gameObject);
        }

        private void SetContinueVisible(bool visible)
        {
            if (continueButton == null)
                return;
            if (visible)
                continueButton.transform.SetAsLastSibling();
            continueButton.gameObject.SetActive(visible);
        }

        private void PoseCardOffscreen()
        {
            if (cardRoot == null)
                return;
            cardRoot.anchoredPosition = new Vector2(0f, EntranceOffsetY);
            cardRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-18f, 18f));
            cardRoot.localScale = Vector3.one * fitScale;
        }

        private void FitCard()
        {
            fitScale = 1f;
            if (cardRoot == null || cardRoot.parent is not RectTransform parent)
                return;
            float width = parent.rect.width;
            if (width < 32f)
                return;
            fitScale = Mathf.Max(0.2f, Mathf.Min(1f, (width - 24f) / LibraryCardLayout.CardWidth));
            cardRoot.localScale = Vector3.one * fitScale;
        }

        private void HidePrintedInk()
        {
            SetImageAlpha(cardTypeIcon, 0f);
            SetImageAlpha(moveNameStamp, 0f);
            SetImageAlpha(statsStamp, 0f);
            SetImageAlpha(moveDot, 0f);
            SetImageAlpha(outlineFlash, 0f);
        }

        private void AssignStrip(Image image, string text, float fontSize, float maxWidth, int seed, float lineHeight = 0f)
        {
            if (image == null)
                return;
            Sprite sprite = LibraryCardStampStrip.Bake(
                text,
                fontSize,
                maxWidth,
                seed,
                null,
                lineHeight,
                LibraryCardFaces.LearnedInk,
                false);
            if (sprite != null)
                spawnedStrips.Add(sprite);
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = false;
            image.raycastTarget = false;
            if (sprite != null)
                image.rectTransform.sizeDelta = new Vector2(sprite.rect.width, sprite.rect.height);
            image.color = new Color(1f, 1f, 1f, 0f);
        }

        private void ReleaseStrips()
        {
            if (moveNameStamp != null)
                moveNameStamp.sprite = null;
            if (statsStamp != null)
                statsStamp.sprite = null;
            for (int i = 0; i < spawnedStrips.Count; i++)
                LibraryCardStampStrip.Release(spawnedStrips[i]);
            spawnedStrips.Clear();
        }

        private void WireAccelerationButton()
        {
            if (accelerationButton == null)
                return;
            accelerationButton.onClick.RemoveListener(OnOverlayPressed);
            accelerationButton.onClick.AddListener(OnOverlayPressed);
        }

        private void OnOverlayPressed()
        {
            if (AwaitingContinue)
                ConfirmContinue();
            else
                Accelerate();
        }

        private void PlayOutcomeAudio(ProgressionGrantOutcome outcome, ProgressionFolioPresentationSO profile)
        {
            if (audioSource == null || profile == null)
                return;
            AudioClip clip = outcome == ProgressionGrantOutcome.NoFolioIssued
                ? profile.noFolioClip
                : outcome == ProgressionGrantOutcome.IssuanceError
                    ? profile.issuanceInterruptedClip
                    : null;
            if (clip != null)
                audioSource.PlayOneShot(clip);
        }

        private static Image EnsureChildImage(RectTransform parent, string name, Image existing)
        {
            if (existing != null)
                return existing;
            Transform child = parent.Find(name);
            if (child != null)
            {
                Image found = child.GetComponent<Image>();
                if (found != null)
                    return found;
            }

            return CreateImage(name, parent, Color.white);
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static RectTransform CreateGroup(string name, Transform parent, out CanvasGroup group, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            group = go.GetComponent<CanvasGroup>();
            return rect;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            return text;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static Sprite CreateCircle()
        {
            const int size = 32;
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            Vector2 center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    byte alpha = distance <= radius ? (byte)255 : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
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

        private static void SetGroupAlpha(CanvasGroup group, float alpha)
        {
            if (group != null)
                group.alpha = alpha;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            if (rect == null)
                return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static string ToRoman(int star)
        {
            switch (star)
            {
                case 1: return "I";
                case 2: return "II";
                case 3: return "III";
                case 4: return "IV";
                default: return "V";
            }
        }
    }
}
