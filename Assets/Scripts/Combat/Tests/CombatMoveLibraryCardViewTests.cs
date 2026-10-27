using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatMoveLibraryCardViewTests
    {
        [Test]
        public void Show_PaintsMoveNameStatsAndBothModStamps()
        {
            var host = new GameObject("Details2", typeof(RectTransform));
            var cardObject = new GameObject("LibraryCardRoot", typeof(RectTransform));
            cardObject.transform.SetParent(host.transform, false);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            var view = host.AddComponent<CombatMoveLibraryCardView>();
            Image moveName = CreateImage("MoveNameStamp", cardRect);
            Image stats = CreateImage("StatsStamp", cardRect);
            Image mod1 = CreateImage("Mod1Stamp", cardRect);
            Image mod2 = CreateImage("Mod2Stamp", cardRect);
            Image moveDot = CreateImage("MoveDot", cardRect);
            Image mod1Dot = CreateImage("Mod1Dot", cardRect);
            Image mod2Dot = CreateImage("Mod2Dot", cardRect);
            view.Bind(
                cardRect,
                CreateImage("CardFace", cardRect),
                null,
                null,
                CreateImage("TypeWell", cardRect),
                moveName,
                stats,
                mod1,
                mod2,
                moveDot,
                mod1Dot,
                mod2Dot,
                null);

            try
            {
                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    learnedFace = false,
                    moveName = "Signal Jab",
                    statsInk = "POWER: 40      ACCURACY: 100",
                    moveRarity = RarityTier.Rare,
                    hasMod1 = true,
                    mod1Name = "Overclock",
                    mod1Effect = "+7% base power",
                    mod1Rarity = RarityTier.Uncommon,
                    hasMod2 = true,
                    mod2Name = "Pierce",
                    mod2Effect = "Ignore 10% defense",
                    mod2Rarity = RarityTier.Epic,
                });

                Assert.IsNotNull(moveName.sprite);
                Assert.IsNotNull(stats.sprite);
                Assert.IsNotNull(mod1.sprite);
                Assert.IsNotNull(mod2.sprite);
                Assert.IsTrue(moveDot.enabled);
                Assert.IsTrue(mod1Dot.enabled);
                Assert.IsTrue(mod2Dot.enabled);
                Assert.AreEqual(LibraryCardRarityColors.For(RarityTier.Rare), moveDot.color);
                Assert.AreEqual(LibraryCardRarityColors.For(RarityTier.Uncommon), mod1Dot.color);
                Assert.AreEqual(LibraryCardRarityColors.For(RarityTier.Epic), mod2Dot.color);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Show_LearnedFaceUsesLearnedCardAndCleanModInk()
        {
            AssertFaceAndModInk(learnedFace: true, LibraryCardFaces.LearnedInk, LibraryCardFaces.SageInk);
        }

        [Test]
        public void Show_RolledFaceUsesRolledCardAndDistressedSageInk()
        {
            AssertFaceAndModInk(learnedFace: false, LibraryCardFaces.SageInk, LibraryCardFaces.LearnedInk);
        }

        static void AssertFaceAndModInk(bool learnedFace, Color32 expectedInk, Color32 absentInk)
        {
            var host = new GameObject("Details2", typeof(RectTransform));
            var cardObject = new GameObject("LibraryCardRoot", typeof(RectTransform));
            cardObject.transform.SetParent(host.transform, false);
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            var view = host.AddComponent<CombatMoveLibraryCardView>();
            Image cardFace = CreateImage("CardFace", cardRect);
            Image mod1 = CreateImage("Mod1Stamp", cardRect);
            Sprite rolled = CreateSprite(2, 2, new Color(0.2f, 0.4f, 0.3f, 1f));
            Sprite learned = CreateSprite(2, 2, new Color(0.6f, 0.2f, 0.15f, 1f));
            view.Bind(
                cardRect,
                cardFace,
                rolled,
                learned,
                CreateImage("TypeWell", cardRect),
                CreateImage("MoveNameStamp", cardRect),
                CreateImage("StatsStamp", cardRect),
                mod1,
                CreateImage("Mod2Stamp", cardRect),
                CreateImage("MoveDot", cardRect),
                CreateImage("Mod1Dot", cardRect),
                CreateImage("Mod2Dot", cardRect),
                null);

            try
            {
                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    learnedFace = learnedFace,
                    moveName = "Signal Jab",
                    statsInk = "POWER: 40      ACCURACY: 100",
                    moveRarity = RarityTier.Common,
                    hasMod1 = true,
                    mod1Name = "Guard",
                    mod1Effect = "+4 defense",
                    mod1Rarity = RarityTier.Common,
                });

                Assert.AreSame(learnedFace ? learned : rolled, cardFace.sprite);
                Assert.Greater(CountInk(mod1.sprite, expectedInk), 0);
                Assert.AreEqual(0, CountInk(mod1.sprite, absentInk));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        static int CountInk(Sprite sprite, Color32 ink)
        {
            if (sprite == null || sprite.texture == null)
                return 0;

            Color32[] pixels = sprite.texture.GetPixels32();
            int count = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 pixel = pixels[i];
                if (pixel.a > 8 && pixel.r == ink.r && pixel.g == ink.g && pixel.b == ink.b)
                    count++;
            }

            return count;
        }

        [Test]
        public void Show_BlankCardHidesModStamps()
        {
            var host = new GameObject("Details2", typeof(RectTransform));
            var cardObject = new GameObject("LibraryCardRoot", typeof(RectTransform));
            cardObject.transform.SetParent(host.transform, false);
            var view = host.AddComponent<CombatMoveLibraryCardView>();
            Image mod1 = CreateImage("Mod1Stamp", cardObject.transform);
            Image mod2 = CreateImage("Mod2Stamp", cardObject.transform);
            view.Bind(
                cardObject.GetComponent<RectTransform>(),
                null,
                null,
                null,
                null,
                CreateImage("MoveNameStamp", cardObject.transform),
                null,
                mod1,
                mod2,
                null,
                CreateImage("Mod1Dot", cardObject.transform),
                CreateImage("Mod2Dot", cardObject.transform),
                null);

            try
            {
                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    moveName = "Bare",
                    moveRarity = RarityTier.Common,
                });

                Assert.IsFalse(mod1.enabled);
                Assert.IsFalse(mod2.enabled);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CarouselPose_DownExitsThroughTheBottomAndEntersFromAbove()
        {
            CombatMoveLibraryCardView.CarouselPose pose = CombatMoveLibraryCardView.BuildCarouselPose(
                1,
                new Vector2(0f, -40f),
                500f,
                2.5f,
                35f);

            Assert.Less(pose.exitAnchored.y, -40f);
            Assert.Greater(pose.enterAnchored.y, -40f);
            Assert.AreEqual(35f, pose.exitEuler.x);
            Assert.AreEqual(-35f, pose.enterEuler.x);
            Assert.AreEqual(0f, pose.landingEuler.x);
            Assert.AreEqual(2.5f, pose.landingEuler.z);
        }

        [Test]
        public void CarouselPose_UpExitsThroughTheTopAndEntersFromBelow()
        {
            CombatMoveLibraryCardView.CarouselPose pose = CombatMoveLibraryCardView.BuildCarouselPose(
                -1,
                new Vector2(0f, -40f),
                500f,
                -3f,
                35f);

            Assert.Greater(pose.exitAnchored.y, -40f);
            Assert.Less(pose.enterAnchored.y, -40f);
            Assert.AreEqual(-35f, pose.exitEuler.x);
            Assert.AreEqual(35f, pose.enterEuler.x);
            Assert.AreEqual(-3f, pose.landingEuler.z);
        }

        [Test]
        public void PipMath_ShowsCostSlotsAndFillsOnlyAffordableAp()
        {
            Assert.AreEqual(3, CombatMoveLibraryCardView.ResolvePipCount(3));
            Assert.AreEqual(2, CombatMoveLibraryCardView.ResolveFilledPipCount(3, 2));
            Assert.AreEqual(0, CombatMoveLibraryCardView.ResolvePipCount(0));
            Assert.AreEqual(0, CombatMoveLibraryCardView.ResolveFilledPipCount(0, 4));
            Assert.AreEqual(1, CombatMoveLibraryCardView.ResolveFilledPipCount(1, 6));
            Assert.IsTrue(CombatMoveLibraryCardView.IsApShort(3, 2));
            Assert.IsFalse(CombatMoveLibraryCardView.IsApShort(2, 2));
            Assert.IsFalse(CombatMoveLibraryCardView.IsApShort(0, 0));
        }

        [Test]
        public void TypewriterKill_RevealsTheFullLabel()
        {
            var host = new GameObject("APCost", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(TMPTypewriterReveal));
            TMP_Text label = host.GetComponent<TMP_Text>();
            label.text = "AP:";
            label.maxVisibleCharacters = 0;
            TMPTypewriterReveal typewriter = host.GetComponent<TMPTypewriterReveal>();

            try
            {
                typewriter.Kill();
                Assert.AreEqual(int.MaxValue, label.maxVisibleCharacters);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Show_HeaderShowsRolePipsAndRedCostWhenShort()
        {
            var host = new GameObject("Details2", typeof(RectTransform));
            var cardObject = new GameObject("LibraryCardRoot", typeof(RectTransform));
            cardObject.transform.SetParent(host.transform, false);
            var view = host.AddComponent<CombatMoveLibraryCardView>();
            TMP_Text apCost = CreateLabel("APCost", host.transform, "AP:");
            apCost.color = Color.white;
            RectTransform pipRoot = CreateRect("APPiP", host.transform, new Vector2(300f, 80f));
            TMP_Text role = CreateLabel("MoveRole", host.transform, "DAMAGING MELEE");
            Sprite filled = CreateSprite(8, 32, Color.cyan);
            Sprite empty = CreateSprite(8, 32, Color.black);
            view.AssignApPipSprites(filled, empty);
            view.Bind(
                cardObject.GetComponent<RectTransform>(),
                null,
                null,
                null,
                null,
                CreateImage("MoveNameStamp", cardObject.transform),
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            try
            {
                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    moveName = "Signal Jab",
                    apCost = 3,
                    currentAp = 2,
                    roleLabel = "Damaging Melee",
                });

                Assert.AreEqual("AP:", apCost.text);
                Assert.AreEqual("Damaging Melee", role.text);
                Assert.AreEqual(CombatMoveLibraryCardView.ShortApColor, (Color32)apCost.color);
                List<Image> slots = ActiveSlots(pipRoot);
                Assert.AreEqual(3, slots.Count);
                Assert.AreEqual(filled, slots[0].sprite);
                Assert.AreEqual(filled, slots[1].sprite);
                Assert.AreEqual(empty, slots[2].sprite);

                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    moveName = "Signal Jab",
                    apCost = 1,
                    currentAp = 3,
                    roleLabel = "Support",
                });

                Assert.AreEqual("Support", role.text);
                Assert.AreEqual(new Color32(255, 255, 255, 255), (Color32)apCost.color);
                slots = ActiveSlots(pipRoot);
                Assert.AreEqual(1, slots.Count);
                Assert.AreEqual(filled, slots[0].sprite);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(filled);
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void Show_ClipsTheLibraryCardToTheMoveSelectPanel()
        {
            var panel = new GameObject("MoveSelectPanel", typeof(RectTransform));
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(1000f, 750f);

            var host = new GameObject("Details2", typeof(RectTransform));
            RectTransform hostRect = host.GetComponent<RectTransform>();
            hostRect.SetParent(panelRect, false);
            hostRect.anchorMin = new Vector2(0.5f, 0.5f);
            hostRect.anchorMax = new Vector2(0.5f, 0.5f);
            hostRect.sizeDelta = new Vector2(400f, 500f);

            var header = new GameObject("MoveHeader", typeof(RectTransform));
            header.transform.SetParent(hostRect, false);

            var cardObject = new GameObject("LibraryCardRoot", typeof(RectTransform));
            RectTransform cardRect = cardObject.GetComponent<RectTransform>();
            cardRect.SetParent(hostRect, false);
            cardRect.anchoredPosition = new Vector2(0f, -40f);
            cardRect.sizeDelta = new Vector2(200f, 100f);
            Vector3 authoredPosition = cardRect.position;

            var view = host.AddComponent<CombatMoveLibraryCardView>();
            view.Bind(
                cardRect,
                null,
                null,
                null,
                null,
                CreateImage("MoveNameStamp", cardRect),
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            try
            {
                view.Show(new CombatMoveLibraryCardView.Presentation
                {
                    hasMove = true,
                    moveName = "Signal Jab",
                });

                RectTransform clip = cardRect.parent as RectTransform;
                Assert.IsNotNull(clip);
                Assert.AreEqual("CardClip", clip.name);
                Assert.AreEqual(hostRect, clip.parent);
                Assert.AreEqual(hostRect, header.transform.parent);
                Assert.That(clip.rect.width, Is.EqualTo(panelRect.rect.width).Within(1f));
                Assert.That(clip.rect.height, Is.EqualTo(panelRect.rect.height).Within(1f));
                Assert.Greater(clip.rect.width, hostRect.rect.width + 10f);
                Assert.That(cardRect.position.x, Is.EqualTo(authoredPosition.x).Within(0.05f));
                Assert.That(cardRect.position.y, Is.EqualTo(authoredPosition.y).Within(0.05f));
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }

        static TMP_Text CreateLabel(string name, Transform parent, string text)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 size)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);
            RectTransform rect = rectObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            return rect;
        }

        static Sprite CreateSprite(int width, int height, Color color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }

        static List<Image> ActiveSlots(RectTransform pipRoot)
        {
            var slots = new List<Image>();
            for (int i = 0; i < pipRoot.childCount; i++)
            {
                Transform child = pipRoot.GetChild(i);
                if (!child.gameObject.activeSelf || child.name != "APPiPSlot")
                    continue;
                slots.Add(child.GetComponent<Image>());
            }

            return slots;
        }

        static Image CreateImage(string name, Transform parent)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
