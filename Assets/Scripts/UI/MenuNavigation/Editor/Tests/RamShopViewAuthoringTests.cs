using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor.Tests
{
    public sealed class RamShopViewAuthoringTests
    {
        [Test]
        public void PlayerMenuRoot_KeepsTheEmbeddedRamShopWithoutAMenuButton()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab");
            try
            {
                PlayerMenuController controller = root.GetComponent<PlayerMenuController>();
                Assert.NotNull(controller);
                Assert.AreEqual(5, controller.GetTabDefinitions().Length);
                Assert.AreEqual(5, controller.GetTabScreenCount());
                Assert.IsFalse(controller.GetTabDefinitions().Any(definition =>
                    definition != null && (definition.TabId == "ram_shop" || definition.TabId == "colosseum")));
                Assert.IsNull(root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(item => item.name == "RamShopTabButton" || item.name == "colosseumTabButton"));

                RamShopScreen shop = root.GetComponentsInChildren<RamShopScreen>(true).Single();
                Assert.AreEqual("ram_shop", shop.ScreenId);
                Assert.IsTrue(shop.HasEmbeddedLayout);
                Assert.IsEmpty(shop.GetComponentsInChildren<Canvas>(true), "Shop must reuse the Player Menu root Canvas.");

                RectTransform primary = shop.GetComponentsInChildren<RectTransform>(true).Single(item => item.name == "PrimaryCenter");
                Assert.AreEqual(new Vector2(0.206f, 0.014f), primary.anchorMin);
                Assert.AreEqual(new Vector2(0.794f, 0.951f), primary.anchorMax);

                Button[] buttons = shop.GetComponentsInChildren<Button>(true);
                Assert.IsTrue(buttons.Any(button => button.name == "Tender_SOL"));
                Assert.IsTrue(buttons.Any(button => button.name == "ConfirmButton"));
                Assert.IsTrue(buttons.Any(button => button.name == "CancelConfirmButton"));
                AssertBundleCards(shop.gameObject);
                foreach (Button button in buttons)
                {
                    LayoutElement layout = button.GetComponent<LayoutElement>();
                    if (layout != null && layout.preferredHeight >= 0f)
                        Assert.GreaterOrEqual(layout.preferredHeight, 60f, button.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssertBundleCards(GameObject shop)
        {
            RamBundleCardPresenter[] cards = shop.GetComponentsInChildren<RamBundleCardPresenter>(true);
            Assert.AreEqual(3, cards.Length);
            AssertCard(cards, "Bundle_5", 100, null);
            AssertCard(cards, "Bundle_20", 500, "20% OFF");
            AssertCard(cards, "Bundle_100", 3000, "33% OFF");
            Assert.IsNull(shop.GetComponentsInChildren<Transform>(true).FirstOrDefault(item => item.name == "BundleGrid"));
        }

        private static void AssertCard(RamBundleCardPresenter[] cards, string name, int ramAmount, string ribbon)
        {
            RamBundleCardPresenter card = cards.Single(item => item.name == name);
            SerializedObject serialized = new SerializedObject(card);
            Assert.AreEqual(ramAmount, serialized.FindProperty("ramAmount").intValue);
            Assert.AreEqual(1.03f, serialized.FindProperty("pulseScale").floatValue, 0.0001f);
            Assert.NotNull(serialized.FindProperty("revealRoot").objectReferenceValue, name);
            Assert.NotNull(serialized.FindProperty("revealGroup").objectReferenceValue, name);
            Assert.NotNull(serialized.FindProperty("selectionGlow").objectReferenceValue, name);
            TMPro.TMP_Text price = serialized.FindProperty("priceLabel").objectReferenceValue as TMPro.TMP_Text;
            Assert.NotNull(price, name);
            Assert.AreEqual("QUOTE IN SOL", price.text, name);
            TMPro.TMP_Text amount = card.GetComponentsInChildren<TMPro.TMP_Text>(true)
                .Single(item => item.name == "RamAmountLabel");
            StringAssert.Contains(ramAmount.ToString("N0"), amount.text, name);
            SerializedProperty frames = serialized.FindProperty("frames");
            Assert.AreEqual(8, frames.arraySize);
            for (int i = 0; i < frames.arraySize; i++)
            {
                Sprite sprite = frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                Assert.NotNull(sprite, name);
                TextureImporter importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite)) as TextureImporter;
                Assert.NotNull(importer);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
                Assert.AreEqual(FilterMode.Point, importer.filterMode);
            }

            Transform ribbonTransform = card.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(item => item.name == "DiscountRibbon");
            if (ribbon == null)
            {
                Assert.IsNull(ribbonTransform, name);
                return;
            }

            Assert.NotNull(ribbonTransform, name);
            Assert.AreEqual(ribbon, ribbonTransform.GetComponentInChildren<TMPro.TMP_Text>(true).text);
        }

        [Test]
        public void PlayerMenuRoot_RamIconTracksTheValueWidth()
        {
            GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/UI/PlayerMenu/PlayerMenuRoot.prefab");
            try
            {
                Transform value = root.GetComponentsInChildren<Transform>(true).Single(item => item.name == "RamValue");
                Transform icon = root.GetComponentsInChildren<Transform>(true).Single(item => item.name == "RamIcon");
                Assert.AreEqual(value.parent, icon.parent);
                Assert.Greater(icon.GetSiblingIndex(), value.GetSiblingIndex());

                HorizontalLayoutGroup row = value.parent.GetComponent<HorizontalLayoutGroup>();
                Assert.NotNull(row);
                Assert.IsTrue(row.childControlWidth);
                Assert.IsFalse(row.childForceExpandWidth);

                LayoutElement iconLayout = icon.GetComponent<LayoutElement>();
                Assert.NotNull(iconLayout);
                Assert.AreEqual(0f, iconLayout.flexibleWidth);
                Assert.Greater(iconLayout.preferredWidth, 0f);

                PlayerMenuOverviewPanel overview = root.GetComponentInChildren<PlayerMenuOverviewPanel>(true);
                SerializedObject serialized = new SerializedObject(overview);
                Assert.AreEqual(value.GetComponent<TMPro.TMP_Text>(), serialized.FindProperty("ramValueLabel").objectReferenceValue);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase("1", 9, "0.000000001")]
        [TestCase("1000000", 6, "1")]
        [TestCase("1234500", 6, "1.2345")]
        [TestCase("18446744073709551615", 9, "18446744073.709551615")]
        public void AtomicFormatter_PreservesExactDecimalStrings(string amount, int decimals, string expected)
        {
            Assert.AreEqual(expected, Chainbound.Combat.RamAtomicAmountFormatter.Format(amount, decimals));
        }
    }
}
