using System;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation.PartyTab
{
    public enum ChainlingPartyNavigationPhase
    {
        ViewSection,
        ViewEditing,
        PartySection,
        ColumnEditing,
    }

    [DisallowMultipleComponent]
    public sealed class ChainlingPartyNavigationController : MonoBehaviour, IMenuContentInputHandler, IMenuAdvancedInputHandler
    {
        [SerializeField] private ChainlingPartyNavigationPhase phase = ChainlingPartyNavigationPhase.PartySection;
        [SerializeField] private int selectedSlotIndex;

        private int slotCount = 1;

        public ChainlingPartyNavigationPhase Phase => phase;
        public int SelectedSlotIndex => selectedSlotIndex;

        public event Action<ChainlingPartyNavigationPhase, ChainlingPartyNavigationPhase> PhaseChanged;
        public event Action<int, bool> SlotFocusRequested;
        public event Action DetailRequested;
        public Func<int, bool> ViewEditingMoveRequested { get; set; }
        public Func<bool> ViewEditingSubmitRequested { get; set; }
        public Func<bool> ViewEditingCancelRequested { get; set; }

        public void Configure(int partySlotCount)
        {
            slotCount = Mathf.Max(1, partySlotCount);
            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, slotCount - 1);
        }

        public void ResetToViewSection(bool notify = true)
        {
            EnterPhase(ChainlingPartyNavigationPhase.ViewSection, notify);
        }

        public void ResetToPartySection(bool notify = true)
        {
            EnterPhase(ChainlingPartyNavigationPhase.PartySection, notify);
        }

        public void EnterViewEditing()
        {
            EnterPhase(ChainlingPartyNavigationPhase.ViewEditing, true);
        }

        public void EnterViewSection()
        {
            EnterPhase(ChainlingPartyNavigationPhase.ViewSection, true);
        }

        public void EnterPartySection()
        {
            EnterPhase(ChainlingPartyNavigationPhase.PartySection, true);
        }

        public void EnterColumnEditing()
        {
            EnterPhase(ChainlingPartyNavigationPhase.ColumnEditing, true);
            SlotFocusRequested?.Invoke(selectedSlotIndex, true);
        }

        public void SetSelectedSlotIndex(int slotIndex, bool notify)
        {
            int clamped = Mathf.Clamp(slotIndex, 0, slotCount - 1);
            if (selectedSlotIndex == clamped)
            {
                if (notify && phase == ChainlingPartyNavigationPhase.ColumnEditing)
                    SlotFocusRequested?.Invoke(selectedSlotIndex, true);
                return;
            }

            selectedSlotIndex = clamped;
            if (notify && phase == ChainlingPartyNavigationPhase.ColumnEditing)
                SlotFocusRequested?.Invoke(selectedSlotIndex, true);
        }

        public bool TryHandleCancel()
        {
            switch (phase)
            {
                case ChainlingPartyNavigationPhase.ViewEditing:
                    if (ViewEditingCancelRequested != null && ViewEditingCancelRequested())
                        return true;

                    EnterViewSection();
                    return true;
                case ChainlingPartyNavigationPhase.ColumnEditing:
                    EnterPartySection();
                    return true;
                case ChainlingPartyNavigationPhase.PartySection:
                case ChainlingPartyNavigationPhase.ViewSection:
                    return false;
                default:
                    return false;
            }
        }

        public bool TryHandleMove(Vector2 move)
        {
            if (move.sqrMagnitude < 0.01f)
                return false;

            if (phase == ChainlingPartyNavigationPhase.ColumnEditing)
            {
                if (Mathf.Abs(move.x) >= Mathf.Abs(move.y))
                {
                    if (move.x > 0f)
                        TryHandleRight();
                    else
                        TryHandleLeft();
                }

                return true;
            }

            if (phase == ChainlingPartyNavigationPhase.ViewEditing)
            {
                if (Mathf.Abs(move.x) >= Mathf.Abs(move.y))
                    return true;

                int delta = move.y > 0f ? -1 : 1;
                if (ViewEditingMoveRequested != null)
                    return ViewEditingMoveRequested(delta);

                return true;
            }

            if (Mathf.Abs(move.x) < Mathf.Abs(move.y))
                return true;

            if (move.x > 0f)
                return TryHandleRight();

            return TryHandleLeft();
        }

        public bool TryHandleSubmit()
        {
            switch (phase)
            {
                case ChainlingPartyNavigationPhase.ViewSection:
                    EnterViewEditing();
                    return true;
                case ChainlingPartyNavigationPhase.PartySection:
                    EnterColumnEditing();
                    return true;
                case ChainlingPartyNavigationPhase.ViewEditing:
                    return ViewEditingSubmitRequested != null && ViewEditingSubmitRequested();
                case ChainlingPartyNavigationPhase.ColumnEditing:
                    DetailRequested?.Invoke();
                    return true;
                default:
                    return false;
            }
        }

        public bool TryHandleDetailedView()
        {
            if (phase != ChainlingPartyNavigationPhase.ColumnEditing &&
                phase != ChainlingPartyNavigationPhase.PartySection)
            {
                return false;
            }

            if (phase == ChainlingPartyNavigationPhase.PartySection)
                EnterColumnEditing();

            DetailRequested?.Invoke();
            return true;
        }

        public bool TryHandleCloseMenu()
        {
            return false;
        }

        public bool TryHandleSpecialInteraction()
        {
            return false;
        }

        public bool TryHandleGuide()
        {
            return false;
        }

        private bool TryHandleRight()
        {
            switch (phase)
            {
                case ChainlingPartyNavigationPhase.ViewSection:
                    EnterPartySection();
                    return true;
                case ChainlingPartyNavigationPhase.ColumnEditing:
                    MoveColumn(1);
                    return true;
                case ChainlingPartyNavigationPhase.PartySection:
                case ChainlingPartyNavigationPhase.ViewEditing:
                    return true;
                default:
                    return false;
            }
        }

        private bool TryHandleLeft()
        {
            switch (phase)
            {
                case ChainlingPartyNavigationPhase.PartySection:
                    EnterViewSection();
                    return true;
                case ChainlingPartyNavigationPhase.ColumnEditing:
                    MoveColumn(-1);
                    return true;
                case ChainlingPartyNavigationPhase.ViewSection:
                case ChainlingPartyNavigationPhase.ViewEditing:
                    return true;
                default:
                    return false;
            }
        }

        private void MoveColumn(int delta)
        {
            SetSelectedSlotIndex(selectedSlotIndex + delta, true);
        }

        private void EnterPhase(ChainlingPartyNavigationPhase nextPhase, bool notify)
        {
            ChainlingPartyNavigationPhase previous = phase;
            if (previous == nextPhase)
                return;

            phase = nextPhase;

            if (notify)
                PhaseChanged?.Invoke(previous, phase);
        }
    }
}
