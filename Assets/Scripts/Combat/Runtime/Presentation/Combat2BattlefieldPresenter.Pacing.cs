using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    public partial class Combat2BattlefieldPresenter
    {
        private CombatBattlePacingProfileSO ResolvePacingProfile()
        {
            if (pacingProfile != null)
                return pacingProfile;
            if (_battleController != null && _battleController.PacingProfile != null)
                return _battleController.PacingProfile;
            return null;
        }

        private StatusCatalogSO ResolveStatusCatalog()
        {
            if (_battleController != null && _battleController.StatusCatalog != null)
                return _battleController.StatusCatalog;
            return null;
        }

        private float ResolveMoveAnnounceHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.moveAnnounceHold : 0f;
        }

        private float ResolveUtilityCastHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.utilityCastHold : 0.3f;
        }

        private float ResolveDamageHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.damageHold : impactHoldDuration;
        }

        private float ResolveHealHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.healHold : impactHoldDuration;
        }

        private float ResolveMissDodgeHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.missDodgeHold : impactHoldDuration;
        }

        private float ResolveApFeedbackHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.apFeedbackHold : 0.28f;
        }

        private float ResolveTurnStartApGrantHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.turnStartApGrantHold : 0.35f;
        }

        private float ResolveTurnStartApOutlineFlashHold()
        {
            var profile = ResolvePacingProfile();
            if (profile != null)
                return Mathf.Max(0f, profile.apFeedbackHold * profile.turnStartApOutlineFlashHoldScale);
            return ResolveApFeedbackHold() * 0.35f;
        }

        private bool ShouldBatchTurnStartApHudRefresh()
        {
            var profile = ResolvePacingProfile();
            return profile == null || profile.batchTurnStartApHudRefresh;
        }

        private float ResolveStatusLabelHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.statusLabelHold : 0.32f;
        }

        private float ResolveStageLabelHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.stageLabelHold : 0.28f;
        }

        private float ResolvePassiveLabelHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.passiveLabelHold : 0.22f;
        }

        private float ResolveModifierLabelHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.modifierLabelHold : 0.22f;
        }

        private float ResolveNoApHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.noApHold : 0.28f;
        }

        private float ResolveBarrierBlockedHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.barrierBlockedHold : 0.4f;
        }

        private float ResolveBarrierRaisedHold()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.barrierRaisedHold : 0.32f;
        }

        private float ResolveWallFollowUpLabelStagger()
        {
            var profile = ResolvePacingProfile();
            if (profile != null)
                return Mathf.Max(0.08f, profile.apFeedbackHold * 0.45f);
            return ResolveApFeedbackHold() * 0.45f;
        }

        private float ResolveSwapSettleDelay()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.swapSettleDelay : swapSettleDelay;
        }

        private float ResolveFaintPostImpactDelay()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.faintPostImpactDelay : faintPostImpactDelay;
        }

        private bool ShouldShowMoveCostApFeedback()
        {
            var profile = ResolvePacingProfile();
            return profile != null && profile.showMoveCostApFeedback;
        }

        private IEnumerator HandleStatusFeedback(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            if (targetView == null)
                yield break;

            FocusRecipientTarget(targetView);

            string label = ResolveStatusLabel(evt);
            SpawnCategoryPopup(targetView, ResolveStatusPopupCategory(evt), label);

            Color? outlineColor = ResolveStatusOutlineColor(evt);
            float flashDuration = 0f;
            if (outlineColor.HasValue)
                flashDuration = PlayOutlineFlash(targetView, outlineColor.Value);

            float hold = Mathf.Max(ResolveStatusLabelHold(), flashDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        private IEnumerator HandleStageChanged(CombatEvent evt)
        {
            var targetView = GetView(evt.targetId);
            if (targetView == null)
                yield break;

            FocusRecipientTarget(targetView);

            string label = ResolveStageLabel(evt);
            var stageCategory = evt.intValue >= 0 ? CombatPopupCategory.StageBuff : CombatPopupCategory.StageDebuff;
            SpawnCategoryPopup(targetView, stageCategory, label);

            Color outlineColor = evt.intValue >= 0
                ? ResolveBuffColor()
                : ResolveDebuffColor();
            float flashDuration = PlayOutlineFlash(targetView, outlineColor);

            float hold = Mathf.Max(ResolveStageLabelHold(), flashDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        private IEnumerator HandleApFeedback(CombatEvent evt)
        {
            if (ShouldSkipApFeedback(evt))
                yield break;

            if (IsTurnRegenApEvent(evt))
                yield break;

            var targetView = GetView(evt.targetId);
            if (targetView == null)
                yield break;

            FocusRecipientTarget(targetView);
            CombatSfx?.PlayApFeedback(evt, targetView);

            string label = ResolveApLabel(evt);
            SpawnCategoryPopup(targetView, ResolveApPopupCategory(evt), label);

            Color outlineColor = ResolveApOutlineColor(evt);
            float flashDuration = PlayOutlineFlash(targetView, outlineColor);

            SyncDisplayVitalsForChainling(evt.targetId);
            if (hudController != null)
                hudController.RefreshAllHud();

            float hold = Mathf.Max(ResolveApFeedbackHold(), flashDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        private IEnumerator HandleTurnStartBeat()
        {
            FocusDefault(true);

            List<CombatEvent> batch = CollectTurnStartApEventsFromQueue();
            if (batch.Count == 0)
                yield break;

            yield return HandleTurnStartApGrantBatch(batch);
        }

        private bool TryBuildTurnStartApBatch(CombatEvent evt, out List<CombatEvent> batch)
        {
            batch = null;
            if (!IsTurnStartApBatchEvent(evt))
                return false;

            batch = new List<CombatEvent> { evt };
            while (_eventQueue.Count > 0 && IsTurnStartApBatchEvent(_eventQueue.Peek()))
                batch.Add(_eventQueue.Dequeue());

            return true;
        }

        private List<CombatEvent> CollectTurnStartApEventsFromQueue()
        {
            var batch = new List<CombatEvent>(4);
            while (_eventQueue.Count > 0)
            {
                CombatEvent peek = _eventQueue.Peek();
                if (IsTurnStartApBatchEvent(peek))
                {
                    batch.Add(_eventQueue.Dequeue());
                    continue;
                }

                if (IsTurnStartApInterstitial(peek))
                {
                    _eventQueue.Dequeue();
                    continue;
                }

                break;
            }

            return batch;
        }

        private static bool IsTurnStartApInterstitial(CombatEvent evt)
        {
            return evt.eventType == CombatEventType.SystemMessage;
        }

        private IEnumerator HandleTurnStartApGrantBatch(List<CombatEvent> events)
        {
            if (events == null || events.Count == 0)
                yield break;

            FocusDefault(true);
            CombatSfx?.PlayTurnStartApGrant(events);

            float maxFlashDuration = 0f;
            float outlineHold = ResolveTurnStartApOutlineFlashHold();
            bool refreshHud = ShouldBatchTurnStartApHudRefresh();

            for (int i = 0; i < events.Count; i++)
            {
                CombatEvent evt = events[i];
                if (ShouldSkipApFeedback(evt))
                    continue;

                var targetView = GetView(evt.targetId);
                if (targetView == null)
                    continue;

                string label = ResolveApLabel(evt);
                SpawnCategoryPopup(targetView, ResolveApPopupCategory(evt), label);

                Color outlineColor = ResolveApOutlineColor(evt);
                maxFlashDuration = Mathf.Max(maxFlashDuration, PlayOutlineFlash(targetView, outlineColor, outlineHold));

                SyncDisplayVitalsForChainling(evt.targetId);
                if (!refreshHud && hudController != null)
                    hudController.RefreshAllHud();
            }

            if (refreshHud && hudController != null)
                hudController.RefreshAllHud();

            float hold = Mathf.Max(ResolveTurnStartApGrantHold(), maxFlashDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        private static bool IsTurnRegenApEvent(CombatEvent evt)
        {
            return string.Equals(evt.apMutationSource, CombatApMutationSource.TurnRegen, StringComparison.Ordinal);
        }

        private static bool IsTurnStartApBatchEvent(CombatEvent evt)
        {
            if (!IsTurnRegenApEvent(evt))
                return false;

            return evt.eventType == CombatEventType.APGained ||
                   evt.eventType == CombatEventType.APGainBlocked ||
                   evt.eventType == CombatEventType.APCapped;
        }

        private IEnumerator HandleModifierTriggered(CombatEvent evt)
        {
            if (!evt.boolValue)
                yield break;

            ModifierOutcomePayload payload;
            bool hasPayload = ModifierOutcomePayload.TryFromEvent(evt, out payload);
            if (hasPayload && !payload.gameplayApplied)
                yield break;

            string anchorId = hasPayload && !string.IsNullOrWhiteSpace(payload.anchorChainlingId)
                ? payload.anchorChainlingId
                : evt.targetId;

            var targetView = GetView(anchorId);
            if (targetView == null)
                targetView = GetView(evt.targetId);
            if (targetView == null)
                targetView = GetView(evt.sourceId);
            if (targetView == null)
                yield break;

            FocusRecipientTarget(targetView);

            string label = hasPayload && !string.IsNullOrWhiteSpace(payload.displayText)
                ? payload.displayText
                : ResolveModifierLabel(evt.message);
            SpawnCategoryPopup(targetView, CombatPopupCategory.Modifier, label);

            float hold = ResolveModifierLabelHold();
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        private bool ShouldSkipApFeedback(CombatEvent evt)
        {
            if (evt.eventType == CombatEventType.APSpent &&
                string.Equals(evt.apMutationSource, CombatApMutationSource.MoveCost, StringComparison.Ordinal))
            {
                return !ShouldShowMoveCostApFeedback();
            }

            if (evt.eventType == CombatEventType.APCapped && evt.intValue <= 0)
                return true;

            return false;
        }

        private string ResolveApLabel(CombatEvent evt)
        {
            if (evt.eventType == CombatEventType.APGainBlocked)
                return "AP BLOCKED";

            int amount = evt.intValue;
            if (amount == 0)
                return "AP";

            string sign = amount > 0 ? "+" : string.Empty;
            return $"{sign}{amount} AP";
        }

        private Color ResolveApOutlineColor(CombatEvent evt)
        {
            var profile = ResolvePacingProfile();

            if (evt.eventType == CombatEventType.APGainBlocked)
                return profile != null ? profile.apBlockedColor : new Color(0.55f, 0.55f, 0.65f, 0.95f);

            if (evt.eventType == CombatEventType.APSpent ||
                (evt.eventType == CombatEventType.APStolen && evt.intValue < 0))
            {
                return profile != null ? profile.apDrainColor : new Color(1f, 0.33f, 0.28f, 0.95f);
            }

            return profile != null ? profile.apGainColor : new Color(0.26f, 1f, 0.56f, 0.95f);
        }

        private Color? ResolveStatusOutlineColor(CombatEvent evt)
        {
            var profile = ResolvePacingProfile();

            if (evt.eventType == CombatEventType.StatusImmune)
                return profile != null ? profile.immuneColor : new Color(0.75f, 0.75f, 0.8f, 0.95f);

            if (evt.eventType == CombatEventType.StatusCleansed)
                return profile != null ? profile.cleanseColor : new Color(0.55f, 0.85f, 1f, 0.95f);

            StatusDefinitionSO def = ResolveStatusDefinition(evt.statusId);
            if (def != null)
            {
                if (def.isBuff)
                    return profile != null ? profile.buffColor : new Color(0.26f, 1f, 0.56f, 0.95f);
                if (def.isDebuff)
                    return profile != null ? profile.debuffColor : new Color(1f, 0.33f, 0.28f, 0.95f);
            }

            return null;
        }

        private string ResolveStatusLabel(CombatEvent evt)
        {
            StatusDefinitionSO def = ResolveStatusDefinition(evt.statusId);
            if (def != null && !string.IsNullOrWhiteSpace(def.displayName))
                return ChemistryStatusPresentation.FormatCombatLabel(def, evt.statusId);

            if (evt.statusId != StatusId.None)
                return ChemistryStatusPresentation.FormatCombatLabel(null, evt.statusId);

            return evt.eventType == CombatEventType.StatusCleansed ? "CLEANSED"
                : evt.eventType == CombatEventType.StatusImmune ? "IMMUNE"
                : "STATUS";
        }

        private string ResolveStageLabel(CombatEvent evt)
        {
            string stat = evt.coreStat.ToString().ToUpperInvariant();
            int delta = evt.intValue;
            if (delta == 0)
                return stat;

            string sign = delta > 0 ? "+" : string.Empty;
            return $"{stat} {sign}{delta}";
        }

        private StatusDefinitionSO ResolveStatusDefinition(StatusId statusId)
        {
            if (statusId == StatusId.None)
                return null;

            var catalog = ResolveStatusCatalog();
            return catalog != null ? catalog.Get(statusId) : null;
        }

        private string ResolvePassiveTriggerLabel(CombatEvent evt)
        {
            MoveInstance move = FindMoveInState(evt.moveId);
            if (move != null && !string.IsNullOrWhiteSpace(move.displayName))
                return move.displayName.ToUpperInvariant();

            if (!string.IsNullOrEmpty(evt.moveId) &&
                evt.moveId.IndexOf("counter", StringComparison.OrdinalIgnoreCase) >= 0)
                return "COUNTER";

            return "PASSIVE";
        }

        private string ResolveModifierLabel(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return "MOD";

            const string prefix = "[Modifier ";
            int start = message.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "MOD";

            start += prefix.Length;
            int end = message.IndexOf(']', start);
            if (end <= start)
                return "MOD";

            return message.Substring(start, end - start).Trim().ToUpperInvariant();
        }

        private CombatWallRaiseLabels ResolveWallRaiseLabels(ChainlingInstance chainling)
        {
            if (chainling == null)
                return default;

            string sourceTag = ResolveBarrierSourceTag(chainling);
            MoveDefinitionSO moveDef = ResolveMoveDefinitionAsset(sourceTag);
            CombatWallRaiseLabels labels = CombatWallPopupLabelUtility.ResolveRaiseLabels(chainling, moveDef);
            if (labels.HasMitigationLabel)
                return labels;

            if (moveDef != null && !string.IsNullOrWhiteSpace(moveDef.displayName))
                return new CombatWallRaiseLabels(moveDef.displayName.ToUpperInvariant(), labels.reflectLabel, labels.debuffLabel);

            if (!string.IsNullOrWhiteSpace(sourceTag))
                return new CombatWallRaiseLabels(sourceTag.ToUpperInvariant(), labels.reflectLabel, labels.debuffLabel);

            return new CombatWallRaiseLabels("WALL", labels.reflectLabel, labels.debuffLabel);
        }

        private MoveInstance FindMoveInState(string moveInstanceId)
        {
            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null || string.IsNullOrEmpty(moveInstanceId))
                return null;

            MoveInstance move = FindMove(state.team0, moveInstanceId);
            if (move != null)
                return move;

            return FindMove(state.team1, moveInstanceId);
        }

        private void QueueBarrierFeedback(
            Combat2ChainlingView view,
            ChainlingInstance chainling,
            bool preferBarrierAnchor,
            bool isBroken)
        {
            if (view == null)
                return;

            IEnumerator routine = isBroken
                ? PlayBarrierBrokenFeedbackRoutine(view, preferBarrierAnchor)
                : PlayBarrierRaiseFeedbackRoutine(view, chainling, preferBarrierAnchor);
            if (_sequenceRoutine != null)
            {
                _feedbackQueue.Enqueue(routine);
                return;
            }

            StartCoroutine(routine);
        }

        private IEnumerator PlayBarrierRaiseFeedbackRoutine(
            Combat2ChainlingView view,
            ChainlingInstance chainling,
            bool preferBarrierAnchor)
        {
            CombatWallRaiseLabels labels = ResolveWallRaiseLabels(chainling);
            string mitigationLabel = labels.HasMitigationLabel ? labels.mitigationLabel : "WALL";

            SpawnCategoryPopup(view, CombatPopupCategory.WallRaised, mitigationLabel, preferBarrierAnchor);

            var profile = ResolvePacingProfile();
            Color outlineColor = profile != null
                ? profile.wallColor
                : new Color(0.35f, 0.75f, 1f, 0.95f);
            float flashDuration = PlayOutlineFlash(view, outlineColor);

            float hold = Mathf.Max(ResolveBarrierRaisedHold(), flashDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);

            float stagger = ResolveWallFollowUpLabelStagger();
            if (labels.HasReflectLabel)
            {
                if (stagger > 0f)
                    yield return new WaitForSeconds(stagger);
                SpawnCategoryPopup(view, CombatPopupCategory.WallReflect, labels.reflectLabel, preferBarrierAnchor);
            }

            if (labels.HasDebuffLabel)
            {
                if (stagger > 0f)
                    yield return new WaitForSeconds(stagger);
                SpawnCategoryPopup(view, CombatPopupCategory.WallDebuff, labels.debuffLabel, preferBarrierAnchor);
            }
        }

        private IEnumerator PlayBarrierBrokenFeedbackRoutine(Combat2ChainlingView view, bool preferBarrierAnchor)
        {
            SpawnCategoryPopup(view, CombatPopupCategory.WallBroken, "BROKEN", preferBarrierAnchor);

            var profile = ResolvePacingProfile();
            Color outlineColor = profile != null
                ? profile.wallBrokenColor
                : new Color(1f, 0.55f, 0.25f, 0.95f);
            float flashDuration = PlayOutlineFlash(view, outlineColor);

            float hold = Mathf.Max(ResolveBarrierRaisedHold(), flashDuration);
            if (hold > 0f)
                yield return new WaitForSeconds(hold);
        }

        private float PlayOutlineFlash(Combat2ChainlingView view, Color color, float holdSecondsOverride = -1f)
        {
            if (view == null)
                return 0f;

            var profile = ResolvePacingProfile();
            float width = profile != null ? profile.outlineFlashWidthPixels : 5f;
            float softness = profile != null ? profile.outlineFlashSoftness : 0.45f;
            float fadeIn = profile != null ? profile.outlineFlashFadeInSeconds : 0.06f;
            float fadeOut = profile != null ? profile.outlineFlashFadeOutSeconds : 0.14f;
            float hold = holdSecondsOverride >= 0f
                ? holdSecondsOverride
                : ResolveApFeedbackHold() * 0.35f;

            return view.PlayOutlineFlash(color, width, softness, fadeIn, fadeOut, hold);
        }

        private Color ResolveBuffColor()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.buffColor : new Color(0.26f, 1f, 0.56f, 0.95f);
        }

        private Color ResolveDebuffColor()
        {
            var profile = ResolvePacingProfile();
            return profile != null ? profile.debuffColor : new Color(1f, 0.33f, 0.28f, 0.95f);
        }
    }
}
