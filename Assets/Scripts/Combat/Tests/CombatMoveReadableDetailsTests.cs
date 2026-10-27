using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatMoveReadableDetailsTests
    {
        const string CanvasPath = "Assets/Prefabs/Combat/CombatBattleCanvas.prefab";
        const string StampFontPath = "Assets/Resources/TextMesh Pro/Fonts/PixeloidSans SDF.asset";

        [Test]
        public void ReadableStats_ShowTypePowerAndAccuracyOnlyWhenTheyApply()
        {
            MoveSelectDisplayUtility.ReadableStatValues damaging =
                MoveSelectDisplayUtility.FormatReadableStats(
                    new MoveInstance
                    {
                        category = MoveCategory.DamagingMelee,
                        baseType = ChainlingType.Compressed,
                        basePower = 75,
                        baseAccuracy = 75,
                    },
                    ChainlingType.Compressed);

            Assert.That(damaging.typeValue, Is.EqualTo("Compressed"));
            Assert.That(damaging.powerValue, Is.EqualTo("75"));
            Assert.That(damaging.accuracyValue, Is.EqualTo("75%"));

            MoveSelectDisplayUtility.ReadableStatValues utility =
                MoveSelectDisplayUtility.FormatReadableStats(
                    new MoveInstance
                    {
                        category = MoveCategory.StatusUtility,
                        baseType = ChainlingType.None,
                        basePower = 0,
                        baseAccuracy = 0,
                    },
                    ChainlingType.Frozen);

            Assert.That(utility.typeValue, Is.EqualTo("Typeless (Frozen)"));
            Assert.That(utility.powerValue, Is.EqualTo(string.Empty));
            Assert.That(utility.accuracyValue, Is.EqualTo(string.Empty));

            Assert.That(
                MoveSelectDisplayUtility.FormatReadablePowerValue(MoveCategory.Support, 40),
                Is.EqualTo(string.Empty));
            Assert.That(
                MoveSelectDisplayUtility.FormatReadablePowerValue(MoveCategory.DamagingRanged, 0),
                Is.EqualTo("0"));
            Assert.That(
                MoveSelectDisplayUtility.FormatReadableAccuracyValue(0),
                Is.EqualTo(string.Empty));
        }

        [Test]
        public void SetSelectedDetails_FillsReadableValuesWithoutTouchingAuthoredStyle()
        {
            var panelObject = new GameObject("MoveSelectPanel", typeof(RectTransform), typeof(CombatMoveSelectPanelUGUI));
            try
            {
                Transform details = CreateRect("Details", panelObject.transform);
                Transform details2 = CreateRect("Details2", details);
                TMP_Text typeValue = CreateLabeledValue(details2, "MoveStats/MoveType", "TYPE:", "COMPRESSED");
                TMP_Text powerValue = CreateLabeledValue(details2, "MoveStats/MovePower", "POWER:", "75");
                TMP_Text accuracyValue = CreateLabeledValue(details2, "MoveStats/MoveAccuracy", "ACCURACY:", "75%");
                TMP_Text description = CreateDescription(details2);
                AssignAuthoringRefs(panelObject.GetComponent<CombatMoveSelectPanelUGUI>());

                FontStyles typeStyle = typeValue.fontStyle;
                float typeSize = typeValue.fontSize;
                Color typeColor = typeValue.color;
                Vector2 typePosition = typeValue.rectTransform.anchoredPosition;
                TMP_FontAsset typeFont = typeValue.font;
                string typeLabel = details2.Find("MoveStats/MoveType/Label").GetComponent<TMP_Text>().text;

                CombatMoveSelectPanelUGUI panel = panelObject.GetComponent<CombatMoveSelectPanelUGUI>();
                panel.SetSelectedDetails(new CombatMoveSelectPanelUGUI.MoveDetailData
                {
                    moveName = "Scorch Shot",
                    apText = "AP Cost 2",
                    powerText = "Power 40",
                    accuracyText = "Accuracy Auto",
                    typeRoleLabel = "Burned | Damaging Ranged",
                    shortDescription = "A short readable description.",
                    readableTypeValue = "Burned",
                    readablePowerValue = "40",
                    readableAccuracyValue = string.Empty,
                    isUsable = true,
                });

                Assert.That(typeValue.text, Is.EqualTo("Burned"));
                Assert.That(powerValue.text, Is.EqualTo("40"));
                Assert.That(accuracyValue.text, Is.EqualTo(string.Empty));
                Assert.That(description.text, Is.EqualTo("A short readable description."));
                Assert.That(typeValue.fontStyle, Is.EqualTo(typeStyle));
                Assert.That(typeValue.fontSize, Is.EqualTo(typeSize).Within(0.01f));
                Assert.That(typeValue.color, Is.EqualTo(typeColor));
                Assert.That(typeValue.rectTransform.anchoredPosition, Is.EqualTo(typePosition));
                Assert.That(typeValue.font, Is.SameAs(typeFont));
                Assert.That(details2.Find("MoveStats/MoveType/Label").GetComponent<TMP_Text>().text, Is.EqualTo(typeLabel));
                Assert.That(description.fontSize, Is.EqualTo(20f).Within(0.01f));
                Assert.That(description.fontStyle, Is.EqualTo(FontStyles.UpperCase));

                CombatMoveSelectPanelUGUI.MoveDetailData rest = panel.CreateRestDetailData();
                panel.SetSelectedDetails(rest);
                Assert.That(typeValue.text, Is.EqualTo(string.Empty));
                Assert.That(powerValue.text, Is.EqualTo(string.Empty));
                Assert.That(accuracyValue.text, Is.EqualTo(string.Empty));
                Assert.That(description.text, Is.EqualTo(rest.shortDescription));
                Assert.That(description.fontStyle, Is.EqualTo(FontStyles.UpperCase));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
            }
        }

        [Test]
        public void BattleCanvas_MoveStatsAndDescriptionKeepAuthoredFontsAndLayout()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPath);
            Assert.That(prefab, Is.Not.Null);
            CombatMoveSelectPanelUGUI panel = prefab.GetComponentInChildren<CombatMoveSelectPanelUGUI>(true);
            Assert.That(panel, Is.Not.Null);

            SerializedObject serialized = new SerializedObject(panel);
            var details = serialized.FindProperty("detailsRoot").objectReferenceValue as RectTransform;
            Assert.That(details, Is.Not.Null);
            Transform details2 = details.Find("Details2");
            Assert.That(details2, Is.Not.Null);
            Assert.That(ChildNames(details2), Is.EqualTo(new[] { "MoveHeader", "LibraryCardRoot", "MoveStats", "MoveDetails" }));

            RectTransform card = details2.Find("LibraryCardRoot") as RectTransform;
            Assert.That(card.localScale.x, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(card.localScale.y, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(card.anchoredPosition.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(card.anchoredPosition.y, Is.EqualTo(-40f).Within(0.01f));
            Assert.That(card.sizeDelta.x, Is.EqualTo(1354f).Within(0.01f));
            Assert.That(card.sizeDelta.y, Is.EqualTo(640f).Within(0.01f));

            AssertBar(details2.Find("MoveStats") as RectTransform, new Vector2(0f, -165f), new Vector2(-50f, 130f), true);
            AssertBar(details2.Find("MoveDetails") as RectTransform, new Vector2(0f, 90f), new Vector2(-50f, 130f), false);

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StampFontPath);
            Assert.That(font, Is.Not.Null);
            AssertStatColumn(details2.Find("MoveStats/MoveType"), "TYPE:", font, typeValue: true);
            AssertStatColumn(details2.Find("MoveStats/MovePower"), "POWER:", font, typeValue: false);
            AssertStatColumn(details2.Find("MoveStats/MoveAccuracy"), "ACCURACY:", font, typeValue: false);

            Transform descriptionRoot = details2.Find("MoveDetails/MoveDescription");
            RectTransform descriptionRect = descriptionRoot as RectTransform;
            Assert.That(descriptionRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(descriptionRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(descriptionRect.sizeDelta.x, Is.EqualTo(-32f).Within(0.01f));
            Assert.That(descriptionRect.sizeDelta.y, Is.EqualTo(-24f).Within(0.01f));
            Assert.That(descriptionRoot.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            AssertImage(descriptionRoot.GetComponent<Image>(), new Color(0.64705884f, 0.6666667f, 0.54509807f, 1f), 0.2f);

            TMP_Text description = details2.Find("MoveDetails/MoveDescription/Description").GetComponent<TMP_Text>();
            RectTransform descriptionTextRect = description.rectTransform;
            Assert.That(descriptionTextRect.sizeDelta.x, Is.EqualTo(-48f).Within(0.01f));
            Assert.That(descriptionTextRect.sizeDelta.y, Is.EqualTo(-32f).Within(0.01f));
            AssertReadableFont(description, font, 20f, autosize: false);
            Assert.That(description.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            Assert.That(description.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Left));
            Assert.That(description.verticalAlignment, Is.EqualTo(VerticalAlignmentOptions.Top));
        }

        static void AssignAuthoringRefs(CombatMoveSelectPanelUGUI panel)
        {
            SerializedObject serialized = new SerializedObject(panel);
            serialized.FindProperty("root").objectReferenceValue = panel.GetComponent<RectTransform>();
            serialized.FindProperty("moveListRoot").objectReferenceValue = CreateRect("MoveList", panel.transform);
            serialized.FindProperty("selectedNameText").objectReferenceValue = CreateText(panel.transform, "SelectedName");
            serialized.FindProperty("selectedRoleText").objectReferenceValue = CreateText(panel.transform, "SelectedRole");
            serialized.FindProperty("selectedApText").objectReferenceValue = CreateText(panel.transform, "SelectedAP");
            serialized.FindProperty("selectedDescriptionText").objectReferenceValue = CreateText(panel.transform, "SelectedDescription");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static TMP_Text CreateLabeledValue(Transform details2, string columnPath, string label, string value)
        {
            Transform column = CreateRect(columnPath, details2);
            TMP_Text labelText = CreateText(column, "Label");
            labelText.text = label;
            TMP_Text valueText = CreateText(column, "Value");
            valueText.text = value;
            valueText.fontSize = 36f;
            valueText.fontStyle = FontStyles.UpperCase;
            valueText.color = Color.white;
            valueText.rectTransform.anchoredPosition = new Vector2(0f, 15f);
            return valueText;
        }

        static TMP_Text CreateDescription(Transform details2)
        {
            Transform root = CreateRect("MoveDetails/MoveDescription", details2);
            TMP_Text text = CreateText(root, "Description");
            text.fontSize = 20f;
            text.fontStyle = FontStyles.UpperCase;
            text.color = Color.white;
            return text;
        }

        static Transform CreateRect(string path, Transform parent)
        {
            Transform current = parent;
            string[] parts = path.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                Transform child = current.Find(parts[i]);
                if (child == null)
                {
                    var go = new GameObject(parts[i], typeof(RectTransform));
                    go.transform.SetParent(current, false);
                    child = go.transform;
                }

                current = child;
            }

            return current;
        }

        static TMP_Text CreateText(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing.GetComponent<TMP_Text>();

            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<TextMeshProUGUI>();
        }

        static void AssertBar(RectTransform rect, Vector2 position, Vector2 size, bool topAnchored)
        {
            Assert.That(rect.anchorMin.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(rect.anchorMax.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rect.anchorMin.y, Is.EqualTo(topAnchored ? 1f : 0f).Within(0.0001f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(topAnchored ? 1f : 0f).Within(0.0001f));
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(position.x).Within(0.01f));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(position.y).Within(0.01f));
            Assert.That(rect.sizeDelta.x, Is.EqualTo(size.x).Within(0.01f));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(size.y).Within(0.01f));

            HorizontalLayoutGroup layout = rect.GetComponent<HorizontalLayoutGroup>();
            Assert.That(layout.padding.left, Is.EqualTo(64));
            Assert.That(layout.padding.right, Is.EqualTo(64));
            Assert.That(layout.spacing, Is.EqualTo(0f).Within(0.01f));
            Assert.That(layout.childAlignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(layout.childControlWidth, Is.False);
            Assert.That(layout.childControlHeight, Is.False);
            Assert.That(layout.childForceExpandWidth, Is.True);
            AssertImage(rect.GetComponent<Image>(), new Color(0.21568629f, 0.29803923f, 0.28235295f, 0.6862745f), 0.2f);
        }

        static void AssertStatColumn(Transform column, string label, TMP_FontAsset font, bool typeValue)
        {
            Assert.That(ChildNames(column), Is.EqualTo(new[] { "Label", "Value", "Frame" }));
            RectTransform rect = column as RectTransform;
            Assert.That(rect.sizeDelta.x, Is.EqualTo(200f).Within(0.01f));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(130f).Within(0.01f));

            TMP_Text labelText = column.Find("Label").GetComponent<TMP_Text>();
            Assert.That(labelText.text, Is.EqualTo(label));
            AssertReadableFont(labelText, font, 24f, autosize: false);
            Assert.That(labelText.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Center));
            Assert.That(labelText.verticalAlignment, Is.EqualTo(VerticalAlignmentOptions.Bottom));

            TMP_Text value = column.Find("Value").GetComponent<TMP_Text>();
            RectTransform valueRect = value.rectTransform;
            if (typeValue)
            {
                Assert.That(valueRect.anchoredPosition.y, Is.EqualTo(28f).Within(0.01f));
                Assert.That(valueRect.sizeDelta.x, Is.EqualTo(-56f).Within(0.01f));
                Assert.That(valueRect.sizeDelta.y, Is.EqualTo(-90f).Within(0.01f));
                Assert.That(SerializedFloat(value, "m_fontSizeBase"), Is.EqualTo(42f).Within(0.01f));
                AssertReadableFont(value, font, value.fontSize, autosize: true);
                Assert.That(value.verticalAlignment, Is.EqualTo(VerticalAlignmentOptions.Middle));
            }
            else
            {
                Assert.That(valueRect.anchoredPosition.y, Is.EqualTo(15f).Within(0.01f));
                Assert.That(valueRect.sizeDelta.y, Is.EqualTo(60f).Within(0.01f));
                AssertReadableFont(value, font, 36f, autosize: false);
                Assert.That(value.verticalAlignment, Is.EqualTo(VerticalAlignmentOptions.Top));
            }

            Assert.That(value.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Center));
            RectTransform frame = column.Find("Frame") as RectTransform;
            Assert.That(frame.sizeDelta.x, Is.EqualTo(-30f).Within(0.01f));
            Assert.That(frame.sizeDelta.y, Is.EqualTo(-40f).Within(0.01f));
            AssertImage(frame.GetComponent<Image>(), new Color(0.69803923f, 0.7294118f, 0.5647059f, 1f), 0.3f);
        }

        static void AssertReadableFont(TMP_Text text, TMP_FontAsset font, float size, bool autosize)
        {
            Assert.That(text.font, Is.SameAs(font));
            Assert.That(text.fontStyle, Is.EqualTo(FontStyles.UpperCase));
            Assert.That(text.enableAutoSizing, Is.EqualTo(autosize));
            if (!autosize)
                Assert.That(text.fontSize, Is.EqualTo(size).Within(0.01f));
            Assert.That(text.color.r, Is.EqualTo(1f).Within(0.01f));
            Assert.That(text.color.g, Is.EqualTo(1f).Within(0.01f));
            Assert.That(text.color.b, Is.EqualTo(1f).Within(0.01f));
            Assert.That(text.color.a, Is.EqualTo(1f).Within(0.01f));
        }

        static float SerializedFloat(Object target, string propertyName)
        {
            SerializedObject serialized = new SerializedObject(target);
            return serialized.FindProperty(propertyName).floatValue;
        }

        static void AssertImage(Image image, Color color, float pixelsPerUnit)
        {
            Assert.That(image, Is.Not.Null);
            Assert.That(image.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(pixelsPerUnit).Within(0.001f));
            Assert.That(image.color.r, Is.EqualTo(color.r).Within(0.002f));
            Assert.That(image.color.g, Is.EqualTo(color.g).Within(0.002f));
            Assert.That(image.color.b, Is.EqualTo(color.b).Within(0.002f));
            Assert.That(image.color.a, Is.EqualTo(color.a).Within(0.002f));
            Assert.That(image.sprite, Is.Not.Null);
        }

        static string[] ChildNames(Transform parent)
        {
            var names = new string[parent.childCount];
            for (int i = 0; i < parent.childCount; i++)
                names[i] = parent.GetChild(i).name;
            return names;
        }
    }
}
