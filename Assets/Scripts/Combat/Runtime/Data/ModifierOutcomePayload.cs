using System;
using UnityEngine;

namespace Chainbound.Combat
{
    public enum ModifierPopupMoment
    {
        Unknown = 0,
        PreResolutionApplied = 1,
        OnHitApplied = 2,
        OnCritApplied = 3,
        OnProcSucceeded = 4,
        OnPriorityApplied = 5,
        OnSwapTrigger = 6,
        OnKoTrigger = 7,
        OnEscapeTrigger = 8,
        OnSynergyMet = 9,
        OnStatusApplied = 10
    }

    [Serializable]
    public sealed class ModifierOutcomePayload
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string sectionId;
        public string category;
        public string displayText;
        public string anchorChainlingId;
        public ModifierPopupMoment moment;
        public string momentName;
        public bool gameplayApplied;
        public string reason;
        public string sourceChainlingId;
        public string targetChainlingId;
        public string moveId;

        public string ToJson()
        {
            momentName = moment.ToString();
            return JsonUtility.ToJson(this);
        }

        public static ModifierOutcomePayload Create(
            string sectionId,
            ModifierCategory category,
            string displayText,
            string anchorChainlingId,
            ModifierPopupMoment moment,
            bool gameplayApplied,
            string reason,
            string sourceChainlingId,
            string targetChainlingId,
            string moveId)
        {
            return new ModifierOutcomePayload
            {
                schemaVersion = CurrentSchemaVersion,
                sectionId = sectionId ?? string.Empty,
                category = category.ToString(),
                displayText = string.IsNullOrWhiteSpace(displayText) ? "MOD" : displayText.Trim().ToUpperInvariant(),
                anchorChainlingId = anchorChainlingId ?? string.Empty,
                moment = moment,
                momentName = moment.ToString(),
                gameplayApplied = gameplayApplied,
                reason = reason ?? string.Empty,
                sourceChainlingId = sourceChainlingId ?? string.Empty,
                targetChainlingId = targetChainlingId ?? string.Empty,
                moveId = moveId ?? string.Empty,
            };
        }

        public static CombatEvent CreateEvent(
            int turn,
            string sectionId,
            ModifierCategory category,
            string displayText,
            string anchorChainlingId,
            ModifierPopupMoment moment,
            bool gameplayApplied,
            string reason,
            string sourceChainlingId,
            string targetChainlingId,
            string moveId)
        {
            ModifierOutcomePayload payload = Create(
                sectionId,
                category,
                displayText,
                anchorChainlingId,
                moment,
                gameplayApplied,
                reason,
                sourceChainlingId,
                targetChainlingId,
                moveId);

            return new CombatEvent
            {
                eventType = CombatEventType.ModifierTriggered,
                turn = turn,
                sourceId = sourceChainlingId ?? string.Empty,
                targetId = targetChainlingId ?? string.Empty,
                moveId = moveId ?? string.Empty,
                boolValue = gameplayApplied,
                message = $"[Modifier {payload.sectionId}] {(gameplayApplied ? "applied" : "skipped")}: {payload.reason}",
                metadataJson = payload.ToJson(),
            };
        }

        public static bool TryParse(string json, out ModifierOutcomePayload payload)
        {
            payload = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                payload = JsonUtility.FromJson<ModifierOutcomePayload>(json);
            }
            catch
            {
                payload = null;
            }

            return payload != null && payload.schemaVersion == CurrentSchemaVersion;
        }

        public static bool TryFromEvent(CombatEvent evt, out ModifierOutcomePayload payload)
        {
            payload = null;
            if (evt.eventType != CombatEventType.ModifierTriggered)
                return false;

            return TryParse(evt.metadataJson, out payload);
        }
    }
}
