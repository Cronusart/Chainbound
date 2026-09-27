using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalJam.UI.MenuNavigation.Editor.Tests
{
    [TestFixture]
    public sealed class MenuInputContextControllerTests
    {
        private GameObject eventSystemObject;
        private GameObject contextObject;
        private EventSystem eventSystem;
        private MenuInputContextController context;
        private Button button;

        [SetUp]
        public void SetUp()
        {
            ClearInputIntentGate();

            eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();

            contextObject = new GameObject("MenuContext");
            context = contextObject.AddComponent<MenuInputContextController>();
            SetPrivateField("eventSystem", eventSystem);

            GameObject buttonObject = new GameObject(
                "DefaultButton",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(contextObject.transform, false);
            button = buttonObject.GetComponent<Button>();
            context.SetContentNavigation(new[] { button }, button, contextObject.transform);
        }

        [TearDown]
        public void TearDown()
        {
            ClearInputIntentGate();
            Object.DestroyImmediate(contextObject);
            Object.DestroyImmediate(eventSystemObject);
        }

        [Test]
        public void MobileSubmit_RecoversLostFocusAndConfirmsInSamePress()
        {
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            eventSystem.SetSelectedGameObject(null);

            InvokePrivate("HandleMobileAPressed");

            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(button.gameObject));
            Assert.That(clicks, Is.EqualTo(1));
        }

        [Test]
        public void PhysicalSubmit_WithLostFocusRepairsWithoutConsumingMobileRetry()
        {
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            eventSystem.SetSelectedGameObject(null);

            InvokePrivate("SubmitSelected", false);
            Assert.That(clicks, Is.Zero);
            Assert.That(eventSystem.currentSelectedGameObject, Is.EqualTo(button.gameObject));

            InvokePrivate("HandleMobileAPressed");
            Assert.That(clicks, Is.EqualTo(1));
        }

        private void InvokePrivate(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(MenuInputContextController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing private method {methodName}.");
            method.Invoke(context, arguments);
        }

        private void SetPrivateField(string fieldName, object value)
        {
            FieldInfo field = typeof(MenuInputContextController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}.");
            field.SetValue(context, value);
        }

        private static void ClearInputIntentGate()
        {
            System.Type gateType = System.Type.GetType(
                "SignalJam.UI.MobileControls.MenuInputIntentGate, SignalJam.UI.MobileControls");
            gateType?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public)?.Invoke(null, null);
        }
    }
}
