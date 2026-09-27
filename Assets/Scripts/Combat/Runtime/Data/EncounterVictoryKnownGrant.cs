using System;
using UnityEngine;

namespace Chainbound.Combat
{
    [Serializable]
    public struct EncounterVictoryKnownGrant
    {
        public string moveId;

        [Tooltip("Optional. When empty, server/client derive boss:{encounterId}:{moveId}.")]
        public string idempotencyKey;

        public string ResolveIdempotencyKey(string narrativeEncounterId)
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
                return idempotencyKey.Trim();

            string encounter = string.IsNullOrWhiteSpace(narrativeEncounterId)
                ? "unknown_encounter"
                : narrativeEncounterId.Trim();
            string move = string.IsNullOrWhiteSpace(moveId) ? "unknown_move" : moveId.Trim();
            return $"boss:{encounter}:{move}";
        }
    }

    [Serializable]
    public sealed class EncounterVictoryKnownGrantResult
    {
        public string moveId;
        public string knownInstanceId;
        public string moveItemId;
        public bool success;
        public bool alreadyGranted;
        public string error;

        public bool AwardedKnown =>
            success && !string.IsNullOrWhiteSpace(moveId) &&
            (!string.IsNullOrWhiteSpace(knownInstanceId) || !string.IsNullOrWhiteSpace(moveItemId));
    }

    [Serializable]
    public sealed class EncounterVictoryKnownGrantRequest
    {
        public string playerProfileId;
        public string narrativeEncounterId;
        public string moveId;
        public string idempotencyKey;
        public int battleSeed;
    }

    public interface IEncounterVictoryKnownGrantPersistence
    {
        bool IsAvailable { get; }
        System.Threading.Tasks.Task<EncounterVictoryKnownGrantResult> GrantAsync(EncounterVictoryKnownGrantRequest request);
    }
}
