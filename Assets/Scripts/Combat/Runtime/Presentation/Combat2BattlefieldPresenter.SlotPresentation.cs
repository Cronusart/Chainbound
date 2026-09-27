using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public partial class Combat2BattlefieldPresenter
    {
        private readonly Dictionary<int, string> _slotPresentationLockByKey = new Dictionary<int, string>();

        private static int SlotPresentationKey(int teamIndex, int slotIndex) => teamIndex * 2 + slotIndex;

        private void ClearPresentationLocks()
        {
            _slotPresentationLockByKey.Clear();
        }

        private void LockSlotPresentation(int teamIndex, int slotIndex, string instanceId)
        {
            if (teamIndex < 0 || slotIndex < 0 || string.IsNullOrEmpty(instanceId))
                return;

            _slotPresentationLockByKey[SlotPresentationKey(teamIndex, slotIndex)] = instanceId;
        }

        private void UnlockSlotPresentation(int teamIndex, int slotIndex)
        {
            if (teamIndex < 0 || slotIndex < 0)
                return;

            _slotPresentationLockByKey.Remove(SlotPresentationKey(teamIndex, slotIndex));
        }

        private bool TryGetSlotPresentationLock(int teamIndex, int slotIndex, out string instanceId)
        {
            instanceId = string.Empty;
            if (teamIndex < 0 || slotIndex < 0)
                return false;

            return _slotPresentationLockByKey.TryGetValue(SlotPresentationKey(teamIndex, slotIndex), out instanceId)
                && !string.IsNullOrEmpty(instanceId);
        }

        private void ApplyPresentationLocksForNewEvents(IReadOnlyList<CombatEvent> events, int startIndex)
        {
            bool appliedAny = false;
            for (int i = startIndex; i < events.Count; i++)
            {
                if (TryApplyPresentationLockForEvent(events, i))
                    appliedAny = true;
            }

            if (appliedAny)
                RefreshSlotBindings();
        }

        private bool TryApplyPresentationLockForEvent(IReadOnlyList<CombatEvent> events, int index)
        {
            CombatEvent evt = events[index];
            switch (evt.eventType)
            {
                case CombatEventType.ChainlingKO:
                    return TryApplyPresentationLockForKo(events, index, evt);
                case CombatEventType.SwapPerformed:
                    return TryApplyPresentationLockForSwap(evt);
                default:
                    return false;
            }
        }

        private bool TryApplyPresentationLockForKo(IReadOnlyList<CombatEvent> events, int index, CombatEvent koEvt)
        {
            if (string.IsNullOrEmpty(koEvt.targetId))
                return false;

            if (TryResolveEventSlot(koEvt.intValue2, koEvt.intValue, out int teamIndex, out int slotIndex))
            {
                LockSlotPresentation(teamIndex, slotIndex, koEvt.targetId);
                return true;
            }

            for (int i = index + 1; i < events.Count; i++)
            {
                CombatEvent next = events[i];
                if (next.eventType == CombatEventType.MoveUsed)
                    break;

                if (next.eventType != CombatEventType.AutoSwapIn)
                    continue;

                if (!string.Equals(next.sourceId, koEvt.targetId, StringComparison.Ordinal))
                    continue;

                if (!TryResolveEventSlot(next.intValue2, next.intValue, out teamIndex, out slotIndex))
                    return false;

                LockSlotPresentation(teamIndex, slotIndex, koEvt.targetId);
                return true;
            }

            return TryApplyPresentationLockForInstance(koEvt.targetId);
        }

        private bool TryApplyPresentationLockForSwap(CombatEvent evt)
        {
            if (string.IsNullOrEmpty(evt.sourceId))
                return false;

            if (TryResolveEventSlot(evt.intValue2, evt.intValue, out int teamIndex, out int slotIndex))
            {
                LockSlotPresentation(teamIndex, slotIndex, evt.sourceId);
                return true;
            }

            return TryApplyPresentationLockForInstance(evt.sourceId);
        }

        private bool TryApplyPresentationLockForInstance(string instanceId)
        {
            if (!TryFindSlotForInstance(instanceId, out int teamIndex, out int slotIndex))
                return false;

            LockSlotPresentation(teamIndex, slotIndex, instanceId);
            return true;
        }

        private static bool TryResolveEventSlot(int teamIndex, int slotIndex, out int resolvedTeam, out int resolvedSlot)
        {
            resolvedTeam = teamIndex;
            resolvedSlot = slotIndex;
            return teamIndex >= 0 && slotIndex >= 0;
        }

        private bool TryFindSlotForInstance(string instanceId, out int teamIndex, out int slotIndex)
        {
            teamIndex = -1;
            slotIndex = -1;
            if (string.IsNullOrEmpty(instanceId))
                return false;

            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null)
                return false;

            if (TryFindSlotOnTeam(state.team0, 0, instanceId, out slotIndex))
            {
                teamIndex = 0;
                return true;
            }

            if (TryFindSlotOnTeam(state.team1, 1, instanceId, out slotIndex))
            {
                teamIndex = 1;
                return true;
            }

            return false;
        }

        private static bool TryFindSlotOnTeam(TeamState team, int teamIndex, string instanceId, out int slotIndex)
        {
            slotIndex = -1;
            if (team == null || team.chainlings == null || team.activeIndices == null)
                return false;

            for (int rosterIndex = 0; rosterIndex < team.chainlings.Count; rosterIndex++)
            {
                ChainlingInstance chainling = team.chainlings[rosterIndex];
                if (chainling == null || !string.Equals(chainling.instanceId, instanceId, StringComparison.Ordinal))
                    continue;

                for (int slot = 0; slot < team.activeIndices.Count; slot++)
                {
                    if (team.activeIndices[slot] == rosterIndex)
                    {
                        slotIndex = slot;
                        return true;
                    }
                }

                return false;
            }

            return false;
        }

        private ChainlingInstance FindChainlingInState(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return null;

            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null)
                return null;

            ChainlingInstance found = FindChainlingOnTeam(state.team0, instanceId);
            if (found != null)
                return found;

            return FindChainlingOnTeam(state.team1, instanceId);
        }

        private static ChainlingInstance FindChainlingOnTeam(TeamState team, string instanceId)
        {
            if (team == null || team.chainlings == null)
                return null;

            for (int i = 0; i < team.chainlings.Count; i++)
            {
                ChainlingInstance chainling = team.chainlings[i];
                if (chainling != null && string.Equals(chainling.instanceId, instanceId, StringComparison.Ordinal))
                    return chainling;
            }

            return null;
        }

        private void AssignSlotRespectingLock(Combat2ChainlingView view, ChainlingInstance stateChainling, int teamIndex, int slotIndex)
        {
            if (view == null)
                return;

            if (TryGetSlotPresentationLock(teamIndex, slotIndex, out string lockedInstanceId))
            {
                if (string.Equals(view.InstanceId, lockedInstanceId, StringComparison.Ordinal))
                    return;

                ChainlingInstance outgoing = FindChainlingInState(lockedInstanceId);
                if (outgoing != null)
                    AssignSlot(view, outgoing);

                return;
            }

            AssignSlot(view, stateChainling);
        }

        private ChainlingInstance ResolveWallChainlingForSlot(ChainlingInstance stateChainling, int teamIndex, int slotIndex)
        {
            if (TryGetSlotPresentationLock(teamIndex, slotIndex, out string lockedInstanceId))
            {
                ChainlingInstance outgoing = FindChainlingInState(lockedInstanceId);
                if (outgoing != null)
                    return outgoing;
            }

            return stateChainling;
        }

        private Combat2ChainlingView ResolveLockedSlotView(int teamIndex, int slotIndex, string instanceId)
        {
            if (TryGetSlotPresentationLock(teamIndex, slotIndex, out string lockedInstanceId)
                && string.Equals(lockedInstanceId, instanceId, StringComparison.Ordinal))
            {
                return GetViewBySlot(teamIndex, slotIndex);
            }

            return null;
        }

        private Combat2ChainlingView ResolvePresentationView(string instanceId, int teamIndex, int slotIndex)
        {
            if (!string.IsNullOrEmpty(instanceId))
            {
                if (_viewById.TryGetValue(instanceId, out Combat2ChainlingView mapped))
                    return mapped;

                Combat2ChainlingView lockedView = ResolveLockedSlotView(teamIndex, slotIndex, instanceId);
                if (lockedView != null)
                    return lockedView;
            }

            return GetView(instanceId);
        }

        private bool HasQueuedAutoSwapForKo(CombatEvent koEvt)
        {
            if (string.IsNullOrEmpty(koEvt.targetId))
                return false;

            foreach (CombatEvent queued in _eventQueue)
            {
                if (queued.eventType != CombatEventType.AutoSwapIn)
                    continue;

                if (string.Equals(queued.sourceId, koEvt.targetId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private void UnlockKoPresentationIfNeeded(CombatEvent koEvt)
        {
            if (HasQueuedAutoSwapForKo(koEvt))
                return;

            if (TryResolveEventSlot(koEvt.intValue2, koEvt.intValue, out int teamIndex, out int slotIndex))
            {
                UnlockSlotPresentation(teamIndex, slotIndex);
                RefreshSlotBindings();
                return;
            }

            if (TryFindSlotForInstance(koEvt.targetId, out teamIndex, out slotIndex))
            {
                UnlockSlotPresentation(teamIndex, slotIndex);
                RefreshSlotBindings();
            }
        }
    }
}
