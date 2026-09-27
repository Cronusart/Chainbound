using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Chainbound.Combat.Tests
{
    public sealed class CombatNarrationPresentationTests
    {
        private GameObject _testRoot;

        [TearDown]
        public void TearDown()
        {
            if (_testRoot != null)
                UnityEngine.Object.DestroyImmediate(_testRoot);
        }

        [Test]
        public void Typewriter_RevealsMultipleCharactersWhenFrameIsSlowerThanGlyphDelay()
        {
            float leftover = 0f;
            int visible = DialogueBoxView.AdvanceVisibleCharacterCount(
                0,
                75,
                75f,
                1f / 30f,
                ref leftover);

            Assert.AreEqual(2, visible);
            Assert.Greater(leftover, 0f);
            Assert.Less(leftover, 1f / 75f);
        }

        [Test]
        public void Typewriter_DoesNotAdvanceWhenFrameIsFasterThanGlyphDelay()
        {
            float leftover = 0f;
            int visible = DialogueBoxView.AdvanceVisibleCharacterCount(
                0,
                75,
                75f,
                1f / 200f,
                ref leftover);

            Assert.AreEqual(0, visible);
            Assert.Greater(leftover, 0f);

            int frames = 1;
            while (visible < 1 && frames < 8)
            {
                visible = DialogueBoxView.AdvanceVisibleCharacterCount(
                    visible,
                    75,
                    75f,
                    1f / 200f,
                    ref leftover);
                frames++;
            }

            Assert.AreEqual(1, visible);
            Assert.Greater(frames, 2);
        }

        [Test]
        public void Typewriter_CompletesImmediatelyWhenSpeedIsInstant()
        {
            float leftover = 0f;
            int visible = DialogueBoxView.AdvanceVisibleCharacterCount(
                0,
                12,
                0f,
                0.016f,
                ref leftover);

            Assert.AreEqual(12, visible);
        }

        [Test]
        public void DialogueView_ConfirmCompletesRevealBeforeAdvancing()
        {
            DialogueBoxView view = CreateDialogueView();
            view.InitializeHidden();
            view.Show();

            IEnumerator reveal = view.PresentLine("BATTLE LINK", "Resolution text", 1f);
            Assert.IsTrue(reveal.MoveNext());
            Assert.IsTrue(view.IsRevealing);
            Assert.AreEqual(DialogueBoxAdvanceResult.RevealCompleted, view.RequestAdvance());

            while (reveal.MoveNext())
            {
            }

            view.AllowImmediateAdvance();
            Assert.AreEqual(DialogueBoxAdvanceResult.Advanced, view.RequestAdvance());
        }

        [Test]
        public void DialogueView_SelectedChoiceSubmit_CommitsSelectedIndex()
        {
            DialogueBoxView view = CreateDialogueView();
            _testRoot.AddComponent<EventSystem>();
            view.InitializeHidden();
            view.Show();

            int picked = -1;
            view.ShowChoices(new[] { "Yes", "No" }, index => picked = index);
            view.SelectChoiceRelative(1);
            view.SubmitSelectedChoice();

            Assert.AreEqual(1, picked);
            Assert.IsFalse(view.IsWaitingForChoice);
        }

        [Test]
        public void DialogueView_PointerButtonClick_CommitsChoice()
        {
            DialogueBoxView view = CreateDialogueView();
            _testRoot.AddComponent<EventSystem>();
            view.InitializeHidden();
            view.Show();

            int picked = -1;
            view.ShowChoices(new[] { "Yes", "No" }, index => picked = index);
            Button[] buttons = view.GetComponentsInChildren<Button>(true)
                .Where(button => button != null && button.gameObject.activeInHierarchy)
                .ToArray();
            Button noButton = buttons.Length > 1 ? buttons[1] : null;

            Assert.NotNull(noButton);
            noButton.onClick.Invoke();

            Assert.AreEqual(1, picked);
            Assert.IsFalse(view.IsWaitingForChoice);
        }

        [Test]
        public void DialogueView_EventSystemPointerClick_CommitsChoice()
        {
            DialogueBoxView view = CreateDialogueView();
            EventSystem eventSystem = _testRoot.AddComponent<EventSystem>();
            view.InitializeHidden();
            view.Show();

            int picked = -1;
            view.ShowChoices(new[] { "Yes", "No" }, index => picked = index);
            Button yesButton = view.GetComponentsInChildren<Button>(true)
                .First(button => button != null && button.gameObject.activeInHierarchy);
            var pointer = new PointerEventData(eventSystem) { button = PointerEventData.InputButton.Left };

            ExecuteEvents.Execute<IPointerClickHandler>(yesButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);

            Assert.AreEqual(0, picked);
            Assert.IsFalse(view.IsWaitingForChoice);
        }

        [UnityTest]
        public IEnumerator NarrationBox_AutomaticallyCompletesZeroHoldPage()
        {
            _testRoot = new GameObject("CombatBattleCanvas", typeof(RectTransform), typeof(Canvas));
            CombatNarrationBoxController controller = _testRoot.AddComponent<CombatNarrationBoxController>();
            var page = new CombatNarrationPage
            {
                kind = CombatNarrationPageKind.Action,
                speaker = "BATTLE LINK",
                text = "A",
                minimumDisplaySeconds = 0f,
            };

            yield return controller.PresentPage(page);

            Assert.IsTrue(controller.IsVisible);
            controller.HideForSelection();
            Assert.IsFalse(controller.IsVisible);
        }

        private DialogueBoxView CreateDialogueView()
        {
            _testRoot = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("DialoguePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
            panel.transform.SetParent(_testRoot.transform, false);
            var bodyObject = new GameObject("DialogText", typeof(RectTransform), typeof(CanvasRenderer));
            bodyObject.transform.SetParent(panel.transform, false);

            Type textType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("TMPro.TextMeshProUGUI"))
                .FirstOrDefault(type => type != null);
            Assert.NotNull(textType, "TextMeshProUGUI is required by the shared dialogue view.");
            Component body = bodyObject.AddComponent(textType);

            DialogueBoxView view = panel.AddComponent<DialogueBoxView>();
            MethodInfo configure = typeof(DialogueBoxView).GetMethod(nameof(DialogueBoxView.Configure));
            Assert.NotNull(configure);
            configure.Invoke(view, new object[]
            {
                panel,
                body,
                null,
                null,
                null,
                false,
                0f,
                0,
                0.01f,
                0.35f,
            });
            return view;
        }
    }
}
