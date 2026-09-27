using System;
using System.Collections;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public partial class Combat2BattlefieldPresenter
    {
        private readonly Queue<CombatNarrationActionPlan> _narrationQueue = new Queue<CombatNarrationActionPlan>();
        private CombatNarrationActionPlan _activeNarrationPlan;
        private CombatNarrationBoxController _narrationController;

        private bool HasPendingNarration => _activeNarrationPlan != null || _narrationQueue.Count > 0;
        public CombatNarrationProfileSO NarrationProfile => _narrationController != null ? _narrationController.Profile : null;
        public bool IsLiveNarrationEnabled
        {
            get
            {
                CombatUiPresentationMode mode = _battleController?.CurrentEncounter?.EffectiveUiMode ?? CombatUiPresentationMode.Full;
                return mode != CombatUiPresentationMode.Hidden && _narrationController != null && _narrationController.IsConfigured;
            }
        }

        public bool QueueActionNarration(CombatNarrationActionPlan plan)
        {
            if (plan == null || !plan.HasLivePages || !IsLiveNarrationEnabled)
                return false;

            _narrationQueue.Enqueue(plan);
            return true;
        }

        public void SetNarrationProfile(CombatNarrationProfileSO profile)
        {
            ResolveNarrationController();
            _narrationController?.SetProfile(profile);
        }

        public bool QueueBattleResultNarration(CombatNarrationPage page)
        {
            if (page == null || !page.IsReadable || !IsLiveNarrationEnabled)
                return false;
            _feedbackQueue.Enqueue(_narrationController.PresentPage(page));
            if (_sequenceRoutine == null)
                _sequenceRoutine = StartCoroutine(PlayEventSequence());
            return true;
        }

        private void ResolveNarrationController()
        {
            if (_narrationController == null)
                _narrationController = CombatNarrationBoxController.FindOrCreate(this);
            if (_battleController != null)
                _narrationController.SetProfile(_battleController.NarrationPolicy);
            CombatPostBattleReportController.FindOrCreate(this, _battleController);
        }

        private bool TryActivateNextNarrationPlan()
        {
            if (_activeNarrationPlan != null || _narrationQueue.Count == 0)
                return false;
            _activeNarrationPlan = _narrationQueue.Dequeue();
            return true;
        }

        private IEnumerator PresentActiveNarrationAction()
        {
            if (_activeNarrationPlan?.actionPage != null && _narrationController != null)
                yield return _narrationController.PresentPage(_activeNarrationPlan.actionPage);
        }

        private IEnumerator CompleteNarrationAfterEvent(CombatEvent evt)
        {
            int presentedThrough = Math.Max(evt.timestamp, _lastPresentedCombatEventTimestamp);
            if (_activeNarrationPlan == null || presentedThrough < _activeNarrationPlan.lastEventTimestamp)
                yield break;

            if (_narrationController != null)
            {
                foreach (CombatNarrationPage page in _activeNarrationPlan.EnumerateResolutionPages())
                    yield return _narrationController.PresentPage(page);
            }
            _activeNarrationPlan = null;
        }

        private IEnumerator CompleteActiveNarrationWithoutEvent()
        {
            if (_activeNarrationPlan == null)
                yield break;

            if (_narrationController != null)
            {
                foreach (CombatNarrationPage page in _activeNarrationPlan.EnumerateResolutionPages())
                    yield return _narrationController.PresentPage(page);
            }
            _activeNarrationPlan = null;
        }

        private void HideNarrationBox()
        {
            _narrationController?.HideForSelection();
        }

        private void ClearNarrationQueue()
        {
            _narrationQueue.Clear();
            _activeNarrationPlan = null;
            HideNarrationBox();
        }
    }
}
