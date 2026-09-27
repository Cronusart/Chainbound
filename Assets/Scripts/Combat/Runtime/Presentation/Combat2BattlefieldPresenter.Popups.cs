using UnityEngine;

namespace Chainbound.Combat
{
    public partial class Combat2BattlefieldPresenter
    {
        private void SpawnCategoryPopup(
            Combat2ChainlingView view,
            CombatPopupCategory category,
            string label,
            bool preferBarrierAnchor = false)
        {
            if (view == null || string.IsNullOrEmpty(label))
                return;

            ResolveUxFallbacks();
            if (damagePopupSpawner == null)
                return;

            Vector3 anchor = ResolvePopupAnchorPosition(view, preferBarrierAnchor);
            int facingSign = view.ResolvePopupFacingSign();
            var request = CombatPopupPlayRequest.ForLabel(category, anchor, label, facingSign);
            damagePopupSpawner.Spawn(request);
        }

        private void SpawnCategoryAmount(
            Combat2ChainlingView view,
            CombatPopupCategory category,
            int amount,
            bool isHeal = false,
            bool preferBarrierAnchor = false)
        {
            if (view == null || amount <= 0)
                return;

            ResolveUxFallbacks();
            if (damagePopupSpawner == null)
                return;

            Vector3 anchor = ResolvePopupAnchorPosition(view, preferBarrierAnchor);
            int facingSign = view.ResolvePopupFacingSign();
            var request = CombatPopupPlayRequest.ForAmount(category, anchor, amount, isHeal, facingSign);
            damagePopupSpawner.Spawn(request);
        }

        private void SpawnWallMitigatedPopup(Combat2ChainlingView targetView, WallInterceptPayload payload)
        {
            if (targetView == null || payload == null || payload.damageAbsorbed <= 0)
                return;
            if (!payload.RoutesAttackToWall())
                return;

            SpawnCategoryAmount(
                targetView,
                CombatPopupCategory.WallMitigated,
                payload.damageAbsorbed,
                preferBarrierAnchor: true);
        }

        private void SpawnWallAttackerHitFeedback(string attackerId, WallInterceptPayload payload)
        {
            if (payload == null || !payload.HasWallOnHitFeedback())
                return;

            var attackerView = GetView(attackerId);
            if (attackerView == null)
                return;

            if (payload.reflectedDamage > 0)
            {
                SpawnCategoryAmount(attackerView, CombatPopupCategory.WallReflect, payload.reflectedDamage);
            }
            else if (payload.wallReflectPercent > 0f && payload.RoutesAttackToWall())
            {
                string reflectLabel = CombatWallPopupLabelUtility.FormatReflectPercent(payload.wallReflectPercent);
                if (!string.IsNullOrWhiteSpace(reflectLabel))
                    SpawnCategoryPopup(attackerView, CombatPopupCategory.WallReflect, reflectLabel);
            }

            if (payload.attackerApDrained > 0)
            {
                string debuffLabel = CombatWallPopupLabelUtility.FormatApDebuff(payload.attackerApDrained);
                if (!string.IsNullOrWhiteSpace(debuffLabel))
                    SpawnCategoryPopup(attackerView, CombatPopupCategory.WallDebuff, debuffLabel);
            }

            SyncDisplayVitalsForChainling(attackerId);
        }

        private CombatPopupCategory ResolvePassiveTriggerCategory(CombatEvent evt)
        {
            string label = ResolvePassiveTriggerLabel(evt);
            if (label == "COUNTER")
                return CombatPopupCategory.Counter;
            return CombatPopupCategory.Passive;
        }

        private CombatPopupCategory ResolveStatusPopupCategory(CombatEvent evt)
        {
            if (evt.eventType == CombatEventType.StatusImmune)
                return CombatPopupCategory.StatusImmune;
            if (evt.eventType == CombatEventType.StatusCleansed)
                return CombatPopupCategory.StatusCleanse;

            StatusDefinitionSO def = ResolveStatusDefinition(evt.statusId);
            if (def != null)
            {
                if (def.isBuff)
                    return CombatPopupCategory.StatusBuff;
                if (def.isDebuff)
                    return CombatPopupCategory.StatusDebuff;
            }

            return CombatPopupCategory.StatusNeutral;
        }

        private CombatPopupCategory ResolveApPopupCategory(CombatEvent evt)
        {
            if (evt.eventType == CombatEventType.APGainBlocked)
                return CombatPopupCategory.ApBlocked;

            if (evt.eventType == CombatEventType.APSpent ||
                (evt.eventType == CombatEventType.APStolen && evt.intValue < 0))
            {
                return CombatPopupCategory.ApDrain;
            }

            return CombatPopupCategory.ApGain;
        }
    }
}
