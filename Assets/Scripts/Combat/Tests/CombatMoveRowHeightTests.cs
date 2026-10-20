using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatMoveRowHeightTests
    {
        [Test]
        public void SetData_CopiesAuthoredPreferredHeightOntoAZeroRect()
        {
            GameObject rowObject = CreateRow();
            RectTransform rect = rowObject.GetComponent<RectTransform>();
            LayoutElement layout = rowObject.GetComponent<LayoutElement>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            layout.preferredHeight = 58f;
            layout.minHeight = 58f;

            try
            {
                rowObject.GetComponent<CombatMoveRowItemUGUI>().SetData("Signal Jab", null, true, false);
                Assert.That(rect.sizeDelta.y, Is.EqualTo(58f).Within(0.01f));
                Assert.That(layout.preferredHeight, Is.EqualTo(58f).Within(0.01f));
                Assert.That(layout.minHeight, Is.EqualTo(58f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(rowObject);
            }
        }

        [Test]
        public void SetData_LeavesAPositiveAuthoredRectHeightAlone()
        {
            GameObject rowObject = CreateRow();
            RectTransform rect = rowObject.GetComponent<RectTransform>();
            LayoutElement layout = rowObject.GetComponent<LayoutElement>();
            rect.sizeDelta = new Vector2(12f, 72f);
            layout.preferredHeight = 72f;
            layout.minHeight = 72f;

            try
            {
                rowObject.GetComponent<CombatMoveRowItemUGUI>().SetData("Signal Jab", null, true, false);
                Assert.That(rect.sizeDelta.y, Is.EqualTo(72f).Within(0.01f));
                Assert.That(layout.preferredHeight, Is.EqualTo(72f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(rowObject);
            }
        }

        [Test]
        public void SetData_DoesNotWriteTheRowHeightOntoAnotherLayoutElement()
        {
            GameObject rowObject = CreateRow();
            var listObject = new GameObject("MoveList", typeof(RectTransform), typeof(LayoutElement));
            LayoutElement listLayout = listObject.GetComponent<LayoutElement>();
            listLayout.preferredHeight = 0f;
            listLayout.minHeight = 0f;

            FieldInfo field = typeof(CombatMoveRowItemUGUI).GetField(
                "layoutElement",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            field.SetValue(rowObject.GetComponent<CombatMoveRowItemUGUI>(), listLayout);

            try
            {
                rowObject.GetComponent<CombatMoveRowItemUGUI>().SetData("Signal Jab", null, true, false);
                Assert.That(listLayout.preferredHeight, Is.EqualTo(0f).Within(0.01f));
                Assert.That(rowObject.GetComponent<LayoutElement>().preferredHeight, Is.EqualTo(58f).Within(0.01f));
            }
            finally
            {
                Object.DestroyImmediate(rowObject);
                Object.DestroyImmediate(listObject);
            }
        }

        static GameObject CreateRow()
        {
            return new GameObject(
                "MoveRow",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(CombatMoveRowItemUGUI));
        }
    }
}
