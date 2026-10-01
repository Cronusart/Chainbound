using System;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// One-shot stage id published by the encounter transition before the combat
    /// scene loads. The combat stage director consumes it on enable.
    /// </summary>
    public static class CombatStageRequest
    {
        public static string PendingStageId { get; private set; }
        public static string ActiveStageId { get; private set; }

        public static event Action<string> ActiveStageChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            PendingStageId = null;
            ActiveStageId = null;
            ActiveStageChanged = null;
        }

        public static void SetPending(string stageId)
        {
            PendingStageId = CombatStageIds.Normalize(stageId);
        }

        public static bool TryConsumePending(out string stageId)
        {
            stageId = PendingStageId;
            PendingStageId = null;
            return !string.IsNullOrEmpty(stageId);
        }

        public static void PublishActive(string stageId)
        {
            string normalized = CombatStageIds.Normalize(stageId);
            ActiveStageId = normalized;
            ActiveStageChanged?.Invoke(normalized);
        }

        public static void ClearActive(string stageId)
        {
            if (string.IsNullOrEmpty(ActiveStageId))
                return;

            if (!string.IsNullOrEmpty(stageId) &&
                !string.Equals(ActiveStageId, stageId, StringComparison.OrdinalIgnoreCase))
                return;

            ActiveStageId = null;
            ActiveStageChanged?.Invoke(null);
        }
    }
}
