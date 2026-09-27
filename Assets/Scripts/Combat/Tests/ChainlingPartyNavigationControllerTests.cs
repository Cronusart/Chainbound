using NUnit.Framework;
using SignalJam.UI.MenuNavigation.PartyTab;
using UnityEngine;

namespace Chainbound.Combat.Tests
{
    public class ChainlingPartyNavigationControllerTests
    {
        private GameObject go;
        private ChainlingPartyNavigationController controller;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("PartyNavControllerTest");
            controller = go.AddComponent<ChainlingPartyNavigationController>();
            controller.Configure(3);
            controller.ResetToViewSection(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void RightAndLeftSwapBetweenViewAndPartySections()
        {
            Assert.AreEqual(ChainlingPartyNavigationPhase.ViewSection, controller.Phase);

            Assert.IsTrue(controller.TryHandleMove(Vector2.right));
            Assert.AreEqual(ChainlingPartyNavigationPhase.PartySection, controller.Phase);

            Assert.IsTrue(controller.TryHandleMove(Vector2.left));
            Assert.AreEqual(ChainlingPartyNavigationPhase.ViewSection, controller.Phase);
        }

        [Test]
        public void SubmitGatesEditingPhases()
        {
            Assert.IsTrue(controller.TryHandleSubmit());
            Assert.AreEqual(ChainlingPartyNavigationPhase.ViewEditing, controller.Phase);

            controller.EnterPartySection();
            Assert.IsTrue(controller.TryHandleSubmit());
            Assert.AreEqual(ChainlingPartyNavigationPhase.ColumnEditing, controller.Phase);
        }

        [Test]
        public void CancelPeelsPhasesBeforeHub()
        {
            controller.EnterColumnEditing();
            Assert.IsTrue(controller.TryHandleCancel());
            Assert.AreEqual(ChainlingPartyNavigationPhase.PartySection, controller.Phase);

            Assert.IsFalse(controller.TryHandleCancel());
            Assert.AreEqual(ChainlingPartyNavigationPhase.PartySection, controller.Phase);

            controller.EnterViewSection();
            Assert.IsFalse(controller.TryHandleCancel());
        }

        [Test]
        public void CancelFromViewEditingReturnsToViewSection()
        {
            controller.EnterViewEditing();
            Assert.IsTrue(controller.TryHandleCancel());
            Assert.AreEqual(ChainlingPartyNavigationPhase.ViewSection, controller.Phase);
        }

        [Test]
        public void ViewEditingDelegatesMoveSubmitAndCancel()
        {
            int moveDelta = 0;
            bool submitRequested = false;
            bool cancelRequested = false;
            controller.ViewEditingMoveRequested = delta =>
            {
                moveDelta = delta;
                return true;
            };
            controller.ViewEditingSubmitRequested = () =>
            {
                submitRequested = true;
                return true;
            };
            controller.ViewEditingCancelRequested = () =>
            {
                cancelRequested = true;
                return true;
            };

            controller.EnterViewEditing();
            Assert.IsTrue(controller.TryHandleMove(Vector2.down));
            Assert.AreEqual(1, moveDelta);
            Assert.IsTrue(controller.TryHandleSubmit());
            Assert.IsTrue(submitRequested);
            Assert.IsTrue(controller.TryHandleCancel());
            Assert.IsTrue(cancelRequested);
            Assert.AreEqual(ChainlingPartyNavigationPhase.ViewEditing, controller.Phase);
        }

        [Test]
        public void ColumnEditingMovesSelectedSlot()
        {
            controller.EnterColumnEditing();
            Assert.AreEqual(0, controller.SelectedSlotIndex);

            controller.TryHandleMove(Vector2.right);
            Assert.AreEqual(1, controller.SelectedSlotIndex);

            controller.TryHandleMove(Vector2.left);
            Assert.AreEqual(0, controller.SelectedSlotIndex);
        }

        [Test]
        public void SubmitFromColumnEditingRequestsDetail()
        {
            bool detailRequested = false;
            controller.EnterColumnEditing();
            controller.DetailRequested += () => detailRequested = true;

            Assert.IsTrue(controller.TryHandleSubmit());
            Assert.IsTrue(detailRequested);
            Assert.AreEqual(ChainlingPartyNavigationPhase.ColumnEditing, controller.Phase);
        }
    }

    public class ChainlingPartyViewRailTests
    {
        private GameObject go;
        private ChainlingPartyViewRail rail;
        private ChainlingPartyViewMode previewedMode;

        [SetUp]
        public void SetUp()
        {
            go = new GameObject("PartyViewRailTest", typeof(RectTransform));
            rail = go.AddComponent<ChainlingPartyViewRail>();
            previewedMode = ChainlingPartyViewMode.General;
            rail.Configure(null, mode => previewedMode = mode, null, null);
            rail.BuildDefaultLayoutForEditor();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        [Test]
        public void DefaultsToGeneralAndOpensCarouselCentered()
        {
            Assert.AreEqual(ChainlingPartyViewMode.General, rail.ActiveMode);
            Assert.AreEqual(ChainlingPartyViewMode.General, rail.FocusedMode);
            Assert.IsFalse(rail.IsCarouselOpen);

            rail.OpenCarousel(false);

            Assert.IsTrue(rail.IsCarouselOpen);
            Assert.AreEqual(ChainlingPartyViewMode.General, rail.ActiveMode);
            Assert.AreEqual(ChainlingPartyViewMode.General, rail.FocusedMode);
        }

        [Test]
        public void CarouselMoveLiveUpdatesActiveMode()
        {
            rail.OpenCarousel(false);

            Assert.IsTrue(rail.MoveCarousel(1, false));

            Assert.AreEqual(ChainlingPartyViewMode.Attributes, rail.FocusedMode);
            Assert.AreEqual(ChainlingPartyViewMode.Attributes, rail.ActiveMode);
            Assert.AreEqual(ChainlingPartyViewMode.Attributes, previewedMode);
        }

        [Test]
        public void CarouselMoveWrapsVertically()
        {
            rail.OpenCarousel(false);

            Assert.IsTrue(rail.MoveCarousel(-1, false));

            Assert.AreEqual(ChainlingPartyViewMode.Stats, rail.FocusedMode);
            Assert.AreEqual(ChainlingPartyViewMode.Stats, rail.ActiveMode);
        }

        [Test]
        public void ConfirmCarouselClosesWithoutRevertingPreview()
        {
            rail.OpenCarousel(false);
            rail.MoveCarousel(1, false);

            Assert.IsTrue(rail.ConfirmCarousel(false));

            Assert.IsFalse(rail.IsCarouselOpen);
            Assert.AreEqual(ChainlingPartyViewMode.Attributes, rail.ActiveMode);
        }
    }
}
