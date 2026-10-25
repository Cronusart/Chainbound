using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Chainbound.Combat
{
    /// <summary>
    /// Settled library card for the move-select details experiment.
    /// Stamp ink, rarity dots, and up to two mods follow the shared card layout.
    /// Active state is owned by the prefab so Details1 and Details2 can be toggled by hand.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatMoveLibraryCardView : UIBehaviour
    {
        public struct Presentation
        {
            public bool hasMove;
            public bool learnedFace;
            public string moveName;
            public string statsInk;
            public Sprite typeIcon;
            public RarityTier moveRarity;
            public bool hasMod1;
            public string mod1Name;
            public string mod1Effect;
            public RarityTier mod1Rarity;
            public bool hasMod2;
            public string mod2Name;
            public string mod2Effect;
            public RarityTier mod2Rarity;
            public int apCost;
            public int currentAp;
            public bool isPassive;
            public string roleLabel;
            public int direction;

            public static Presentation Blank()
            {
                return new Presentation();
            }
        }

        public readonly struct CarouselPose
        {
            public readonly Vector2 exitAnchored;
            public readonly Vector2 enterAnchored;
            public readonly Vector3 exitEuler;
            public readonly Vector3 enterEuler;
            public readonly Vector3 landingEuler;

            public CarouselPose(Vector2 exitAnchored, Vector2 enterAnchored, Vector3 exitEuler, Vector3 enterEuler, Vector3 landingEuler)
            {
                this.exitAnchored = exitAnchored;
                this.enterAnchored = enterAnchored;
                this.exitEuler = exitEuler;
                this.enterEuler = enterEuler;
                this.landingEuler = landingEuler;
            }
        }

        public static readonly Color32 ShortApColor = new Color32(0xAE, 0x23, 0x34, 0xFF);

        public static int ResolvePipCount(int apCost)
        {
            return Mathf.Max(0, apCost);
        }

        public static int ResolveFilledPipCount(int apCost, int currentAp)
        {
            if (apCost <= 0)
                return 0;
            return Mathf.Clamp(currentAp, 0, apCost);
        }

        public static bool IsApShort(int apCost, int currentAp)
        {
            return apCost > 0 && currentAp < apCost;
        }

        public static CarouselPose BuildCarouselPose(
            int direction,
            Vector2 rest,
            float travel,
            float landingZ,
            float tiltDegrees)
        {
            bool downward = direction > 0;
            float exitSign = downward ? -1f : 1f;
            float tilt = Mathf.Abs(tiltDegrees);
            return new CarouselPose(
                new Vector2(rest.x, rest.y + (exitSign * travel)),
                new Vector2(rest.x, rest.y - (exitSign * travel)),
                new Vector3(downward ? tilt : -tilt, 0f, 0f),
                new Vector3(downward ? -tilt : tilt, 0f, 0f),
                new Vector3(0f, 0f, landingZ));
        }

        [SerializeField] private RectTransform cardRoot;
        [SerializeField] private Image cardFace;
        [SerializeField] private Sprite rolledCardFace;
        [SerializeField] private Sprite learnedCardFace;
        [SerializeField] private Image typeIcon;
        [SerializeField] private Image moveNameStamp;
        [SerializeField] private Image statsStamp;
        [SerializeField] private Image mod1Stamp;
        [SerializeField] private Image mod2Stamp;
        [SerializeField] private Image moveDot;
        [SerializeField] private Image mod1Dot;
        [SerializeField] private Image mod2Dot;
        [SerializeField] private TMP_FontAsset stampFont;
        [SerializeField] private Sprite filledApPip;
        [SerializeField] private Sprite emptyApPip;
        [SerializeField] private float cardExitDuration = 0.12f;
        [SerializeField] private float cardEnterDuration = 0.15f;
        [SerializeField] private float exitTiltDegrees = 35f;
        [SerializeField] private float landingZDegrees = 4f;
        [SerializeField] private float pipStagger = 0.03f;
        [SerializeField] private float pipPopDuration = 0.09f;
        [SerializeField] private Color passiveHeaderMutedColor = new Color(0.74f, 0.74f, 0.74f, 0.85f);

        readonly List<Sprite> spawned = new List<Sprite>();
        readonly List<Image> pipPool = new List<Image>();
        readonly List<Tween> pipTweens = new List<Tween>();
        readonly Dictionary<EntityId, List<Sprite>> departingSprites = new Dictionary<EntityId, List<Sprite>>();
        static Sprite circleSprite;
        TMP_Text apCostLabel;
        TMP_Text roleLabel;
        RectTransform apPipRoot;
        Image apPipTemplate;
        Sequence carousel;
        int carouselVersion;
        bool carouselSettling;
        Vector2 restAnchored;
        Vector3 restScale;
        float landedZ;
        Color authoredApColor = Color.white;
        Color authoredRoleColor = Color.white;
        bool headerResolved;
        bool restCached;
        bool apColorCached;
        bool clipFittedToPanel;

        public void Bind(
            RectTransform root,
            Image face,
            Sprite rolledFace,
            Sprite learnedFace,
            Image type,
            Image moveName,
            Image stats,
            Image mod1,
            Image mod2,
            Image moveRarity,
            Image mod1Rarity,
            Image mod2Rarity,
            TMP_FontAsset font)
        {
            cardRoot = root;
            cardFace = face;
            rolledCardFace = rolledFace;
            learnedCardFace = learnedFace;
            typeIcon = type;
            moveNameStamp = moveName;
            statsStamp = stats;
            mod1Stamp = mod1;
            mod2Stamp = mod2;
            moveDot = moveRarity;
            mod1Dot = mod1Rarity;
            mod2Dot = mod2Rarity;
            if (font != null)
                stampFont = font;
        }

        public void AssignApPipSprites(Sprite filled, Sprite empty)
        {
            filledApPip = filled;
            emptyApPip = empty;
        }

        public void Show(Presentation presentation)
        {
            EnsureClip();
            CacheRest();
            ResolveHeader();
            ApplyHeader(presentation);
            if (presentation.direction != 0 && Application.isPlaying && cardRoot != null)
            {
                PlayCarousel(presentation);
                return;
            }

            KillCarousel();
            SnapCardToRest();
            Paint(presentation);
        }

        protected override void OnDisable()
        {
            SettleAnimations();
            base.OnDisable();
        }

        void Paint(Presentation presentation)
        {
            ReleaseStrips();

            Sprite face = presentation.learnedFace && learnedCardFace != null
                ? learnedCardFace
                : rolledCardFace;
            if (cardFace != null)
            {
                cardFace.sprite = face;
                cardFace.color = Color.white;
                cardFace.preserveAspect = true;
                cardFace.raycastTarget = false;
                SetVisible(cardFace, face != null);
            }

            if (!presentation.hasMove)
            {
                HideInk();
                return;
            }

            bool distress = !presentation.learnedFace;
            Color32 ink = presentation.learnedFace ? LibraryCardFaces.LearnedInk : LibraryCardFaces.SageInk;
            AssignStrip(
                moveNameStamp,
                presentation.moveName,
                LibraryCardLayout.NameFontSize,
                LibraryCardLayout.NameMaxWidth,
                11,
                0f,
                ink,
                distress);
            AssignStats(presentation.statsInk, ink, distress);
            ShowType(presentation.typeIcon);
            PaintDot(moveDot, presentation.moveRarity);
            ShowMod(
                presentation.hasMod1,
                mod1Stamp,
                mod1Dot,
                LibraryCardLayout.FormatModStamp(presentation.mod1Name, presentation.mod1Effect),
                41,
                presentation.mod1Rarity,
                ink,
                distress);
            ShowMod(
                presentation.hasMod2,
                mod2Stamp,
                mod2Dot,
                LibraryCardLayout.FormatModStamp(presentation.mod2Name, presentation.mod2Effect),
                42,
                presentation.mod2Rarity,
                ink,
                distress);
        }

        protected override void OnDestroy()
        {
            SettleAnimations();
            ReleaseStrips();
            ReleaseAllDeparting();
            base.OnDestroy();
        }

        void SettleAnimations()
        {
            KillCarousel();
            SnapCardToRest();
            KillPipTweens();
            SnapPipsToRest();
            RevealLabels();
        }

        void PlayCarousel(Presentation presentation)
        {
            KillCarousel();
            if (cardRoot == null)
            {
                Paint(presentation);
                return;
            }

            GameObject departing = Instantiate(cardRoot.gameObject, cardRoot.parent);
            departing.name = "LibraryCardDeparting";
            RectTransform departingRect = departing.GetComponent<RectTransform>();
            HoldDepartingSprites(departing);
            Paint(presentation);

            float travel = ResolveTravel();
            float tilt = exitTiltDegrees > 0.01f ? exitTiltDegrees : 35f;
            float zRange = landingZDegrees > 0.01f ? landingZDegrees : 4f;
            float landing = Random.Range(-zRange, zRange);
            landedZ = landing;
            CarouselPose pose = BuildCarouselPose(presentation.direction, restAnchored, travel, landing, tilt);
            cardRoot.anchoredPosition = pose.enterAnchored;
            cardRoot.localRotation = Quaternion.Euler(pose.enterEuler);
            cardRoot.localScale = restScale;

            EntityId departingId = departing.GetEntityId();
            int version = ++carouselVersion;
            float exitDuration = cardExitDuration > 0.01f ? cardExitDuration : 0.12f;
            float enterDuration = cardEnterDuration > 0.01f ? cardEnterDuration : 0.15f;
            carousel = DOTween.Sequence().SetUpdate(true).SetAutoKill(false);
            carousel.Join(departingRect.DOAnchorPos(pose.exitAnchored, exitDuration).SetEase(Ease.OutCubic));
            carousel.Join(departingRect.DOLocalRotate(pose.exitEuler, exitDuration, RotateMode.Fast).SetEase(Ease.OutQuad));
            carousel.Join(cardRoot.DOAnchorPos(restAnchored, enterDuration).SetEase(Ease.OutCubic));
            carousel.Join(cardRoot.DOLocalRotate(pose.landingEuler, enterDuration, RotateMode.Fast).SetEase(Ease.OutQuad));
            carousel.OnComplete(() => SettleCarousel(version, departingId, departing, snapLiveCard: true));
            carousel.OnKill(() => SettleCarousel(version, departingId, departing, snapLiveCard: false));
        }

        void SettleCarousel(int version, EntityId departingId, GameObject departing, bool snapLiveCard)
        {
            if (carouselSettling || version != carouselVersion)
                return;

            carouselSettling = true;
            carouselVersion++;
            Sequence active = carousel;
            carousel = null;
            if (active != null && active.IsActive())
                active.Kill(false);
            if (departing != null)
                departing.transform.DOKill(false);
            if (cardRoot != null)
                cardRoot.DOKill(false);
            ReleaseDeparting(departingId, departing);
            if (snapLiveCard)
                SnapCardToRest();
            carouselSettling = false;
        }

        void KillCarousel()
        {
            if (carousel == null)
                return;

            Sequence active = carousel;
            carousel = null;
            if (active.IsActive())
                active.Kill(false);
        }

        void SnapCardToRest()
        {
            if (!restCached || cardRoot == null)
                return;

            cardRoot.anchoredPosition = restAnchored;
            cardRoot.localScale = restScale;
            cardRoot.localRotation = Quaternion.Euler(0f, 0f, landedZ);
        }

        void CacheRest()
        {
            if (restCached || cardRoot == null)
                return;

            restAnchored = cardRoot.anchoredPosition;
            restScale = cardRoot.localScale;
            landedZ = cardRoot.localEulerAngles.z;
            if (landedZ > 180f)
                landedZ -= 360f;
            restCached = true;
        }

        float ResolveTravel()
        {
            float height = 0f;
            if (cardRoot != null && cardRoot.parent is RectTransform parent)
                height = parent.rect.height;
            if (height < 8f && cardRoot != null)
                height = cardRoot.rect.height;
            if (height < 8f)
                height = 640f;
            return height * 0.85f;
        }

        void EnsureClip()
        {
            if (cardRoot == null || cardRoot.parent == null)
                return;

            if (cardRoot.parent.name != "CardClip")
            {
                int sibling = cardRoot.GetSiblingIndex();
                Transform parent = cardRoot.parent;
                var clipObject = new GameObject("CardClip", typeof(RectTransform), typeof(RectMask2D));
                RectTransform clip = clipObject.GetComponent<RectTransform>();
                clip.SetParent(parent, false);
                clip.SetSiblingIndex(sibling);
                clipFittedToPanel = FitClipToMoveSelectPanel(clip);
                if (!clipFittedToPanel)
                    StretchClipToParent(clip);

                // World pose stays on the authored card. Only the runtime mask rect changes.
                cardRoot.SetParent(clip, true);

                Transform header = transform.Find("MoveHeader");
                if (header != null)
                    header.SetAsLastSibling();
                return;
            }

            if (clipFittedToPanel || carousel != null)
                return;

            RectTransform existing = cardRoot.parent as RectTransform;
            if (existing == null)
                return;

            Vector3 worldPosition = cardRoot.position;
            Quaternion worldRotation = cardRoot.rotation;
            Vector3 localScale = cardRoot.localScale;
            if (!FitClipToMoveSelectPanel(existing))
                return;

            cardRoot.position = worldPosition;
            cardRoot.rotation = worldRotation;
            cardRoot.localScale = localScale;
            clipFittedToPanel = true;
            restCached = false;
        }

        bool FitClipToMoveSelectPanel(RectTransform clip)
        {
            RectTransform panel = ResolveMoveSelectPanel();
            RectTransform parent = clip.parent as RectTransform;
            if (panel == null || parent == null)
                return false;
            if (panel.rect.width < 8f || panel.rect.height < 8f)
                return false;

            Vector3[] corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector3 bottomLeft = parent.InverseTransformPoint(corners[0]);
            Vector3 topRight = parent.InverseTransformPoint(corners[2]);
            Vector2 min = new Vector2(
                Mathf.Min(bottomLeft.x, topRight.x),
                Mathf.Min(bottomLeft.y, topRight.y));
            Vector2 max = new Vector2(
                Mathf.Max(bottomLeft.x, topRight.x),
                Mathf.Max(bottomLeft.y, topRight.y));
            Vector2 size = max - min;
            if (size.x < 8f || size.y < 8f)
                return false;

            clip.anchorMin = new Vector2(0.5f, 0.5f);
            clip.anchorMax = new Vector2(0.5f, 0.5f);
            clip.pivot = new Vector2(0.5f, 0.5f);
            clip.sizeDelta = size;
            clip.anchoredPosition = (min + max) * 0.5f - (Vector2)parent.rect.center;
            return true;
        }

        static void StretchClipToParent(RectTransform clip)
        {
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = Vector2.one;
            clip.pivot = new Vector2(0.5f, 0.5f);
            clip.offsetMin = Vector2.zero;
            clip.offsetMax = Vector2.zero;
        }

        RectTransform ResolveMoveSelectPanel()
        {
            CombatMoveSelectPanelUGUI panel = GetComponentInParent<CombatMoveSelectPanelUGUI>(true);
            if (panel != null)
                return panel.transform as RectTransform;

            Transform cursor = transform.parent;
            while (cursor != null)
            {
                if (cursor.name == "MoveSelectPanel")
                    return cursor as RectTransform;
                cursor = cursor.parent;
            }

            return null;
        }

        void ResolveHeader()
        {
            if (headerResolved)
                return;

            apCostLabel = FindLabel("APCost");
            roleLabel = FindLabel("MoveRole");
            Transform pip = FindNamed("APPiP");
            apPipRoot = pip as RectTransform;
            if (apCostLabel != null)
            {
                authoredApColor = apCostLabel.color;
                apColorCached = true;
            }
            if (roleLabel != null)
                authoredRoleColor = roleLabel.color;

            headerResolved = apCostLabel != null || roleLabel != null || apPipRoot != null;
        }

        void ApplyHeader(Presentation presentation)
        {
            if (apCostLabel != null)
            {
                apCostLabel.text = "AP:";
                apCostLabel.color = presentation.isPassive
                    ? passiveHeaderMutedColor
                    : IsApShort(presentation.apCost, presentation.currentAp)
                    ? ShortApColor
                    : authoredApColor;
                PlayTypewriter(apCostLabel);
            }

            if (roleLabel != null)
            {
                roleLabel.text = string.IsNullOrWhiteSpace(presentation.roleLabel)
                    ? string.Empty
                    : presentation.roleLabel.Trim();
                roleLabel.color = presentation.isPassive ? passiveHeaderMutedColor : authoredRoleColor;
                PlayTypewriter(roleLabel);
            }

            RebuildPips(presentation);
        }

        void PlayTypewriter(TMP_Text label)
        {
            if (label == null)
                return;

            TMPTypewriterReveal typewriter = label.GetComponent<TMPTypewriterReveal>();
            if (typewriter == null && Application.isPlaying)
                typewriter = label.gameObject.AddComponent<TMPTypewriterReveal>();
            if (typewriter != null && Application.isPlaying)
            {
                typewriter.Play();
                return;
            }

            label.maxVisibleCharacters = int.MaxValue;
        }

        void RebuildPips(Presentation presentation)
        {
            KillPipTweens();
            if (apPipRoot == null)
                return;

            EnsurePipTemplate();
            int count = ResolvePipCount(presentation.apCost);
            int filled = ResolveFilledPipCount(presentation.apCost, presentation.currentAp);
            ResolvePipSprites(out Sprite filledSprite, out Sprite emptySprite);
            float height = apPipRoot.rect.height;
            if (height < 4f)
                height = apPipRoot.sizeDelta.y;
            if (height < 4f)
                height = 80f;
            height *= 0.9f;
            float width = PipWidth(filledSprite != null ? filledSprite : emptySprite, height);
            const float gap = 6f;

            for (int i = 0; i < count; i++)
            {
                Image pip = RentPip(i);
                bool isFilled = i < filled;
                pip.sprite = isFilled ? filledSprite : emptySprite;
                pip.color = Color.white;
                pip.preserveAspect = true;
                pip.raycastTarget = false;
                pip.enabled = pip.sprite != null;
                RectTransform rect = pip.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(width, height);
                rect.anchoredPosition = new Vector2(i * (width + gap), 0f);
                rect.localRotation = Quaternion.identity;
                pip.gameObject.SetActive(true);
                if (Application.isPlaying)
                {
                    float popDuration = pipPopDuration > 0.01f ? pipPopDuration : 0.09f;
                    float stagger = pipStagger > 0f ? pipStagger : 0.03f;
                    rect.localScale = Vector3.one * 0.65f;
                    RectTransform pipRect = rect;
                    Tween tween = pipRect
                        .DOScale(1f, popDuration)
                        .SetDelay(i * stagger)
                        .SetEase(Ease.OutBack)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            if (pipRect != null)
                                pipRect.localScale = Vector3.one;
                        })
                        .OnKill(() =>
                        {
                            if (pipRect != null)
                                pipRect.localScale = Vector3.one;
                        });
                    pipTweens.Add(tween);
                }
                else
                {
                    rect.localScale = Vector3.one;
                }
            }

            for (int i = count; i < pipPool.Count; i++)
            {
                if (pipPool[i] != null)
                    pipPool[i].gameObject.SetActive(false);
            }
        }

        void EnsurePipTemplate()
        {
            if (apPipTemplate != null || apPipRoot == null)
                return;

            Transform existing = apPipRoot.Find("APPiPTemplate");
            if (existing == null)
            {
                var templateObject = new GameObject("APPiPTemplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                templateObject.transform.SetParent(apPipRoot, false);
                existing = templateObject.transform;
            }

            apPipTemplate = existing.GetComponent<Image>();
            if (apPipTemplate == null)
                apPipTemplate = existing.gameObject.AddComponent<Image>();
            apPipTemplate.raycastTarget = false;
            apPipTemplate.gameObject.SetActive(false);
        }

        Image RentPip(int index)
        {
            while (pipPool.Count <= index)
            {
                var pipObject = Instantiate(apPipTemplate.gameObject, apPipRoot);
                pipObject.name = "APPiPSlot";
                Image image = pipObject.GetComponent<Image>();
                pipPool.Add(image);
            }

            return pipPool[index];
        }

        void ResolvePipSprites(out Sprite filledSprite, out Sprite emptySprite)
        {
            filledSprite = filledApPip;
            emptySprite = emptyApPip;
            if (filledSprite != null && emptySprite != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return;

            HudCardView[] huds = canvas.GetComponentsInChildren<HudCardView>(true);
            for (int i = 0; i < huds.Length; i++)
            {
                if (filledSprite == null)
                    filledSprite = huds[i].ActiveApPipSprite;
                if (emptySprite == null)
                    emptySprite = huds[i].InactiveApPipSprite;
            }
        }

        static float PipWidth(Sprite sprite, float height)
        {
            if (sprite == null || sprite.rect.height <= 0.01f)
                return height * 0.28f;
            return height * (sprite.rect.width / sprite.rect.height);
        }

        void KillPipTweens()
        {
            for (int i = 0; i < pipTweens.Count; i++)
            {
                if (pipTweens[i] != null && pipTweens[i].IsActive())
                    pipTweens[i].Kill(false);
            }

            pipTweens.Clear();
            SnapPipsToRest();
        }

        void SnapPipsToRest()
        {
            for (int i = 0; i < pipPool.Count; i++)
            {
                if (pipPool[i] != null)
                    pipPool[i].rectTransform.localScale = Vector3.one;
            }
        }

        void RevealLabels()
        {
            RevealLabel(apCostLabel);
            RevealLabel(roleLabel);
        }

        static void RevealLabel(TMP_Text label)
        {
            if (label == null)
                return;

            TMPTypewriterReveal typewriter = label.GetComponent<TMPTypewriterReveal>();
            if (typewriter != null)
                typewriter.Kill();
            label.maxVisibleCharacters = int.MaxValue;
            label.alpha = 1f;
        }

        TMP_Text FindLabel(string childName)
        {
            Transform found = FindNamed(childName);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        Transform FindNamed(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                    return children[i];
            }

            return null;
        }

        void HoldDepartingSprites(GameObject departing)
        {
            if (departing == null)
                return;

            var held = new List<Sprite>(spawned);
            spawned.Clear();
            departingSprites[departing.GetEntityId()] = held;
        }

        void ReleaseDeparting(EntityId id, GameObject departing)
        {
            if (!departingSprites.TryGetValue(id, out List<Sprite> held))
            {
                DestroyRuntimeObject(departing);
                return;
            }

            departingSprites.Remove(id);
            for (int i = 0; i < held.Count; i++)
                LibraryCardStampStrip.Release(held[i]);
            DestroyRuntimeObject(departing);
        }

        void ReleaseAllDeparting()
        {
            if (departingSprites.Count == 0)
                return;

            var ids = new List<EntityId>(departingSprites.Keys);
            for (int i = 0; i < ids.Count; i++)
                ReleaseDeparting(ids[i], null);
        }

        static void DestroyRuntimeObject(GameObject target)
        {
            if (target == null)
                return;
            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        public void ApplyLayout()
        {
            if (cardRoot != null)
            {
                cardRoot.anchorMin = cardRoot.anchorMax = new Vector2(0.5f, 0.5f);
                cardRoot.pivot = new Vector2(0.5f, 0.5f);
                cardRoot.anchoredPosition = Vector2.zero;
                cardRoot.localRotation = Quaternion.identity;
                cardRoot.sizeDelta = new Vector2(LibraryCardLayout.CardWidth, LibraryCardLayout.CardHeight);
            }

            if (cardFace != null)
            {
                RectTransform face = cardFace.rectTransform;
                face.anchorMin = Vector2.zero;
                face.anchorMax = Vector2.one;
                face.offsetMin = Vector2.zero;
                face.offsetMax = Vector2.zero;
            }

            PlaceTypeRect(typeIcon);
            LibraryCardLayout.PlaceTopLeft(moveNameStamp != null ? moveNameStamp.rectTransform : null, LibraryCardLayout.NameOrigin, LibraryCardLayout.NameTilt);
            LibraryCardLayout.PlaceTopLeft(statsStamp != null ? statsStamp.rectTransform : null, LibraryCardLayout.StatsOrigin, LibraryCardLayout.StatsTilt);
            LibraryCardLayout.PlaceTopLeft(mod1Stamp != null ? mod1Stamp.rectTransform : null, LibraryCardLayout.Mod1Origin, LibraryCardLayout.Mod1Tilt);
            LibraryCardLayout.PlaceTopLeft(mod2Stamp != null ? mod2Stamp.rectTransform : null, LibraryCardLayout.Mod2Origin, LibraryCardLayout.Mod2Tilt);
            LibraryCardLayout.PlaceCenter(moveDot != null ? moveDot.rectTransform : null, LibraryCardLayout.MoveWell, 0f, LibraryCardLayout.DotInset);
            LibraryCardLayout.PlaceCenter(mod1Dot != null ? mod1Dot.rectTransform : null, LibraryCardLayout.Mod1Well, 0f, LibraryCardLayout.DotInset);
            LibraryCardLayout.PlaceCenter(mod2Dot != null ? mod2Dot.rectTransform : null, LibraryCardLayout.Mod2Well, 0f, LibraryCardLayout.DotInset);
        }

        void ShowType(Sprite icon)
        {
            if (typeIcon == null)
                return;

            typeIcon.sprite = icon;
            typeIcon.preserveAspect = true;
            typeIcon.color = Color.white;
            typeIcon.raycastTarget = false;
            SetVisible(typeIcon, icon != null);
        }

        void ShowMod(bool hasMod, Image stamp, Image dot, string text, int seed, RarityTier rarity, Color32 ink, bool distress)
        {
            if (!hasMod || string.IsNullOrWhiteSpace(text))
            {
                SetVisible(stamp, false);
                SetVisible(dot, false);
                return;
            }

            AssignStrip(
                stamp,
                text,
                LibraryCardLayout.ModFontSize,
                LibraryCardLayout.ModMaxWidth,
                seed,
                0f,
                ink,
                distress);
            PaintDot(dot, rarity);
        }

        void AssignStats(string text, Color32 ink, bool distress)
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
                    ink,
                    distress);
                return;
            }

            AssignStrip(
                statsStamp,
                text,
                LibraryCardLayout.StatsFontSize,
                LibraryCardLayout.NameMaxWidth,
                23,
                0f,
                ink,
                distress);
        }

        void AssignStrip(
            Image target,
            string text,
            float fontSize,
            float maxWidth,
            int seed,
            float lineHeight,
            Color32 ink,
            bool distress)
        {
            if (target == null)
                return;
            if (string.IsNullOrWhiteSpace(text))
            {
                SetVisible(target, false);
                return;
            }

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

        void PaintDot(Image dot, RarityTier tier)
        {
            if (dot == null)
                return;

            if (circleSprite == null)
                circleSprite = CreateCircle();
            dot.sprite = circleSprite;
            dot.color = LibraryCardRarityColors.For(tier);
            dot.raycastTarget = false;
            SetVisible(dot, true);
        }

        void HideInk()
        {
            SetVisible(typeIcon, false);
            SetVisible(moveNameStamp, false);
            SetVisible(statsStamp, false);
            SetVisible(mod1Stamp, false);
            SetVisible(mod2Stamp, false);
            SetVisible(moveDot, false);
            SetVisible(mod1Dot, false);
            SetVisible(mod2Dot, false);
        }

        void ReleaseStrips()
        {
            if (moveNameStamp != null) moveNameStamp.sprite = null;
            if (statsStamp != null) statsStamp.sprite = null;
            if (mod1Stamp != null) mod1Stamp.sprite = null;
            if (mod2Stamp != null) mod2Stamp.sprite = null;
            for (int i = 0; i < spawned.Count; i++)
                LibraryCardStampStrip.Release(spawned[i]);
            spawned.Clear();
        }

        static void PlaceTypeRect(Image icon)
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

        static void SetVisible(Image image, bool visible)
        {
            if (image == null)
                return;
            image.enabled = visible;
        }

        static Sprite CreateCircle()
        {
            const int size = 32;
            var pixels = new Color32[size * size];
            float radius = size * 0.5f - 1f;
            var center = new Vector2(radius, radius);
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
    }
}
