using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    public partial class Combat2BattlefieldPresenter
    {
        private const int CombatSfxOutcomeLookAheadLimit = 16;

        private readonly HashSet<int> _batchedOutcomeSfxEventTimestamps = new HashSet<int>();

        private void TryPlayOutcomeBatchAudio(CombatEvent evt)
        {
            if (!IsCombatSfxOutcomeEvent(evt))
                return;

            if (_batchedOutcomeSfxEventTimestamps.Remove(evt.timestamp))
                return;

            ICombatSfxOrchestrator sfx = CombatSfx;
            if (sfx == null)
                return;

            CombatSfxOutcomeBatch batch = BuildCombatSfxOutcomeBatch(evt);
            if (batch == null || batch.Count == 0)
                return;

            sfx.PlayOutcomeBatch(batch);
            MarkFutureOutcomeSfxEventsHandled(batch, evt.timestamp);
        }

        private void PlayCombatSfxOutcomeImmediate(CombatEvent evt)
        {
            if (!IsCombatSfxOutcomeEvent(evt))
                return;

            // Prevent the later sequence pass from double-playing this drained event.
            _batchedOutcomeSfxEventTimestamps.Add(evt.timestamp);

            ICombatSfxOrchestrator sfx = CombatSfx;
            if (sfx == null)
                return;

            string moveKey = ResolveCombatSfxOutcomeMoveKey(evt);
            MovePresentationContext moveContext = ResolveCombatSfxMoveContext(moveKey);
            string sourceId = ResolveCombatSfxBatchSourceId(evt, moveContext, moveKey);
            var batch = new CombatSfxOutcomeBatch(moveKey, sourceId, evt.turn);
            AddCombatSfxOutcomeContext(batch, evt, moveKey, moveContext);
            if (batch.Count == 0)
                return;

            sfx.PlayOutcomeBatch(batch);
        }

        private CombatSfxOutcomeBatch BuildCombatSfxOutcomeBatch(CombatEvent firstEvent)
        {
            string moveKey = ResolveCombatSfxOutcomeMoveKey(firstEvent);
            MovePresentationContext moveContext = ResolveCombatSfxMoveContext(moveKey);
            string sourceId = ResolveCombatSfxBatchSourceId(firstEvent, moveContext, moveKey);
            var batch = new CombatSfxOutcomeBatch(moveKey, sourceId, firstEvent.turn);

            AddCombatSfxOutcomeContext(batch, firstEvent, moveKey, moveContext);

            if (string.IsNullOrEmpty(moveKey))
                return batch;

            int scanned = 0;
            foreach (CombatEvent candidate in _eventQueue)
            {
                if (scanned++ >= CombatSfxOutcomeLookAheadLimit)
                    break;

                if (IsCombatSfxOutcomeInterstitial(candidate))
                    continue;

                if (!CanJoinCombatSfxOutcomeBatch(batch, firstEvent, moveKey, sourceId, candidate))
                    break;

                AddCombatSfxOutcomeContext(batch, candidate, ResolveCombatSfxOutcomeMoveKey(candidate), ResolveCombatSfxMoveContext(moveKey));
            }

            return batch;
        }

        private void AddCombatSfxOutcomeContext(
            CombatSfxOutcomeBatch batch,
            CombatEvent evt,
            string eventMoveKey,
            MovePresentationContext moveContext)
        {
            if (batch == null)
                return;

            MoveCategory category;
            MoveDefinitionSO moveDefinition = ResolveCombatSfxMoveDefinition(eventMoveKey, moveContext, out category);
            string sourceViewId = ResolveCombatSfxSourceViewId(evt, moveContext, eventMoveKey);
            WallInterceptPayload.TryFromEvent(evt, out var wallPayload);

            batch.Add(new CombatSfxOutcomeContext
            {
                evt = evt,
                sourceView = GetView(sourceViewId),
                targetView = GetView(evt.targetId),
                statusDefinition = IsCombatSfxStatusOutcome(evt.eventType) ? ResolveStatusDefinition(evt.statusId) : null,
                moveDefinition = moveDefinition,
                authoredImpactSound = moveContext != null && moveContext.vfxEntry != null
                    ? moveContext.vfxEntry.impactSound
                    : null,
                authoredMissSound = moveContext != null && moveContext.vfxEntry != null
                    ? moveContext.vfxEntry.missSound
                    : null,
                wallSourceTag = wallPayload != null ? wallPayload.wallSourceTag : string.Empty,
                wallSfxId = wallPayload != null ? ResolveWallSfxId(wallPayload.wallSourceTag) : string.Empty,
                moveCategory = category,
                isCrit = evt.boolValue,
                isKo = evt.eventType == CombatEventType.ChainlingKO,
                targetDelaySeconds = 0f,
                volumeScale = 1f,
            });
        }

        private bool CanJoinCombatSfxOutcomeBatch(
            CombatSfxOutcomeBatch batch,
            CombatEvent firstEvent,
            string moveKey,
            string sourceId,
            CombatEvent candidate)
        {
            if (!IsCombatSfxOutcomeEvent(candidate))
                return false;

            if (candidate.turn != firstEvent.turn)
                return false;

            if (candidate.eventType == CombatEventType.ChainlingKO)
                return BatchContainsTarget(batch, candidate.targetId);

            string candidateMoveKey = ResolveCombatSfxOutcomeMoveKey(candidate);
            if (!string.Equals(candidateMoveKey, moveKey, StringComparison.Ordinal))
                return false;

            if (string.IsNullOrEmpty(sourceId))
                return true;

            if (string.Equals(candidate.sourceId, moveKey, StringComparison.Ordinal))
                return true;

            return string.IsNullOrEmpty(candidate.sourceId) ||
                   string.Equals(candidate.sourceId, sourceId, StringComparison.Ordinal);
        }

        private void MarkFutureOutcomeSfxEventsHandled(CombatSfxOutcomeBatch batch, int currentTimestamp)
        {
            if (batch == null || batch.outcomes == null)
                return;

            for (int i = 0; i < batch.outcomes.Count; i++)
            {
                CombatSfxOutcomeContext context = batch.outcomes[i];
                if (context == null || context.evt.timestamp == currentTimestamp)
                    continue;

                _batchedOutcomeSfxEventTimestamps.Add(context.evt.timestamp);
            }
        }

        private MovePresentationContext ResolveCombatSfxMoveContext(string moveKey)
        {
            if (string.IsNullOrEmpty(moveKey))
                return null;

            MovePresentationContext moveContext;
            return _moveContextByMoveId.TryGetValue(moveKey, out moveContext) ? moveContext : null;
        }

        private MoveDefinitionSO ResolveCombatSfxMoveDefinition(
            string moveKey,
            MovePresentationContext moveContext,
            out MoveCategory category)
        {
            if (moveContext != null)
            {
                category = moveContext.category;
                return moveContext.moveDefinition;
            }

            category = MoveCategory.Support;
            var state = _stateSource != null ? _stateSource.State : null;
            if (state == null || string.IsNullOrEmpty(moveKey))
                return null;

            MoveInfo moveInfo = ResolveMoveInfo(state, moveKey);
            category = moveInfo.category;
            return moveInfo.definition;
        }

        private static string ResolveCombatSfxBatchSourceId(
            CombatEvent evt,
            MovePresentationContext moveContext,
            string moveKey)
        {
            if (moveContext != null && !string.IsNullOrEmpty(moveContext.sourceId))
                return moveContext.sourceId;

            if (!string.IsNullOrEmpty(evt.sourceId) &&
                !string.Equals(evt.sourceId, moveKey, StringComparison.Ordinal))
            {
                return evt.sourceId;
            }

            return string.Empty;
        }

        private static string ResolveCombatSfxSourceViewId(
            CombatEvent evt,
            MovePresentationContext moveContext,
            string moveKey)
        {
            if (!string.IsNullOrEmpty(evt.sourceId) &&
                !string.Equals(evt.sourceId, moveKey, StringComparison.Ordinal))
            {
                return evt.sourceId;
            }

            return moveContext != null ? moveContext.sourceId : string.Empty;
        }

        private static string ResolveCombatSfxOutcomeMoveKey(CombatEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.moveId))
                return evt.moveId;

            if (IsCombatSfxStatusOutcome(evt.eventType) && !string.IsNullOrEmpty(evt.sourceId))
                return evt.sourceId;

            return string.Empty;
        }

        private static bool BatchContainsTarget(CombatSfxOutcomeBatch batch, string targetId)
        {
            if (batch == null || batch.outcomes == null || string.IsNullOrEmpty(targetId))
                return false;

            for (int i = 0; i < batch.outcomes.Count; i++)
            {
                CombatSfxOutcomeContext context = batch.outcomes[i];
                if (context != null && string.Equals(context.evt.targetId, targetId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsCombatSfxOutcomeEvent(CombatEvent evt)
        {
            return IsCombatSfxDamageOutcome(evt.eventType) ||
                   evt.eventType == CombatEventType.HealApplied ||
                   evt.eventType == CombatEventType.Miss ||
                   evt.eventType == CombatEventType.Dodge ||
                   evt.eventType == CombatEventType.BarrierBlocked ||
                   evt.eventType == CombatEventType.ChainlingKO ||
                   IsCombatSfxStatusOutcome(evt.eventType);
        }

        private static bool IsCombatSfxDamageOutcome(CombatEventType eventType)
        {
            return eventType == CombatEventType.DamageDealt ||
                   eventType == CombatEventType.HPDotDamage ||
                   eventType == CombatEventType.HPRecoilDamage ||
                   eventType == CombatEventType.PassiveDamageDealt;
        }

        private static bool IsCombatSfxStatusOutcome(CombatEventType eventType)
        {
            return eventType == CombatEventType.StatusApplied ||
                   eventType == CombatEventType.ChemistryStatusApplied ||
                   eventType == CombatEventType.StatusRefreshed ||
                   eventType == CombatEventType.StatusImmune ||
                   eventType == CombatEventType.StatusCleansed;
        }

        private static bool IsCombatSfxOutcomeInterstitial(CombatEvent evt)
        {
            return evt.eventType == CombatEventType.StatBreakdown;
        }
    }
}
