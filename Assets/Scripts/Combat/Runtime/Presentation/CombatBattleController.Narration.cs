using System.Collections;
using UnityEngine;

namespace Chainbound.Combat
{
    public partial class CombatBattleController
    {
        [Header("Combat Narration")]
        [Tooltip("Optional copy and pacing policy. When unset, built-in defaults are used.")]
        [SerializeField] private CombatNarrationProfileSO narrationProfile;

        public CombatNarrationProfileSO NarrationPolicy => narrationProfile;

        private bool ComposeAndQueueNarration(
            int fromEventIndex,
            int toEventExclusive,
            int turnIndex,
            int actionIndex)
        {
            if (_resolver?.Log == null || toEventExclusive <= fromEventIndex)
                return false;

            CombatUiPresentationMode uiMode = CurrentEncounter?.EffectiveUiMode ?? CombatUiPresentationMode.Full;
            bool liveEnabled = uiMode != CombatUiPresentationMode.Hidden;
            CombatNarrationProfileSO effectiveProfile = narrationProfile != null
                ? narrationProfile
                : presenter != null ? presenter.NarrationProfile : null;
            presenter?.SetNarrationProfile(effectiveProfile);
            CombatNarrationActionPlan plan = CombatNarrationComposer.ComposeAction(
                _resolver.Log,
                fromEventIndex,
                toEventExclusive,
                effectiveProfile,
                turnIndex,
                actionIndex,
                liveEnabled);
            if (plan == null)
                return false;

            _replayData?.AppendNarration(plan.transcriptEntries);
            return presenter != null && presenter.QueueActionNarration(plan);
        }

        private void AppendAndQueueBattleResultNarration(BattleResult result)
        {
            if (result == null)
                return;

            CombatNarrationProfileSO effectiveProfile = narrationProfile != null
                ? narrationProfile
                : presenter != null ? presenter.NarrationProfile : null;
            presenter?.SetNarrationProfile(effectiveProfile);
            int sequenceIndex = _replayData?.narrationTranscript?.Count ?? 0;
            CombatNarrationTranscriptEntry entry = CombatNarrationComposer.ComposeBattleResult(
                result,
                effectiveProfile,
                sequenceIndex);
            if (entry == null)
                return;

            _replayData?.AppendNarration(new[] { entry });
            CombatUiPresentationMode mode = CurrentEncounter?.EffectiveUiMode ?? CombatUiPresentationMode.Full;
            if (mode == CombatUiPresentationMode.Hidden || presenter == null)
                return;

            presenter.QueueBattleResultNarration(new CombatNarrationPage
            {
                kind = CombatNarrationPageKind.BattleResult,
                speaker = entry.speaker,
                text = entry.text,
                minimumDisplaySeconds = effectiveProfile != null
                    ? effectiveProfile.PrimaryResolutionSeconds
                    : 0.70f,
            });
        }

        private void RouteRewardsAfterPostBattlePresentation(BattleResult result)
        {
            if (!(rewardsRouterOverride is ICombatRewardsRouter router))
                return;
            CombatPostBattleReportController report = FindFirstObjectByType<CombatPostBattleReportController>(FindObjectsInactive.Include);
            if (report == null || !report.WillPresentBattleReport)
            {
                router.RouteToRewards(result);
                return;
            }
            StartCoroutine(RouteRewardsAfterPostBattlePresentationRoutine(router, result, report));
        }

        private void PrepareRewardsBeforePostBattlePresentation(BattleResult result)
        {
            if (rewardsRouterOverride is ICombatRewardPreparation preparation)
                preparation.PrepareRewards(result);
        }

        private static IEnumerator RouteRewardsAfterPostBattlePresentationRoutine(
            ICombatRewardsRouter router,
            BattleResult result,
            CombatPostBattleReportController report)
        {
            yield return report.WaitForCompletion();
            router.RouteToRewards(result);
        }
    }
}
