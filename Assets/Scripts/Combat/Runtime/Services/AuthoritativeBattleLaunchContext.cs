using System;
using System.Collections.Generic;
using Chainbound.Combat.Core;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Process-local handoff from the overworld authority gate to the combat scene.
    /// The durable source remains the battle service and its resumable battle ID.
    /// </summary>
    public static class AuthoritativeBattleLaunchContext
    {
        private static CombatSessionEnvelope current;
        private static string gateMessage = string.Empty;

        public static CombatSessionEnvelope Current => current;
        public static bool HasSession => current != null && !string.IsNullOrWhiteSpace(current.BattleId);
        public static event Action<CombatSessionEnvelope> Changed;
        public static event Action<string> GateMessageChanged;

        public static void Publish(CombatSessionEnvelope envelope)
        {
            current = envelope;
            Changed?.Invoke(current);
        }

        public static void Clear()
        {
            current = null;
            Changed?.Invoke(null);
        }

        public static void SetGateMessage(string message)
        {
            string value = message ?? string.Empty;
            if (string.Equals(gateMessage, value, StringComparison.Ordinal))
                return;
            gateMessage = value;
            GateMessageChanged?.Invoke(value);
        }

        public static bool TryReadState(out AuthoritativeClientBattleState state)
        {
            state = null;
            if (!HasSession || string.IsNullOrWhiteSpace(current.StateJson))
                return false;
            try
            {
                state = JsonUtility.FromJson<AuthoritativeClientBattleState>(current.StateJson);
                return state != null;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    [Serializable]
    public sealed class AuthoritativeClientBattleState
    {
        public string phase;
        public uint seed;
        public int turnNumber;
        public bool hasActionCheckpoint;
        public int battleKind;
        public int capturePolicy;
        public bool encounterCaptureEnabled;
        public string zoneId;
        public int captureScannerRating;
        public int captureLowHpThresholdBasisPoints;
        public List<AuthoritativeClientBattleTarget> targets = new List<AuthoritativeClientBattleTarget>();
        public AuthoritativeClientCaptureChallenge captureChallenge;
        public string endReason;
        public List<AuthoritativeClientBattleVital> vitals = new List<AuthoritativeClientBattleVital>();
    }

    [Serializable]
    public sealed class AuthoritativeClientBattleTarget
    {
        public string targetId;
        public string speciesId;
        public string displayName;
        public int currentHp;
        public int maxHp;
        public bool isActive;
        public bool isRemoved;
        public bool captureEnabled;
        public bool isEntangled;
        public string visualVariantId;
        public string mutationId;
        public string entangledTypeId;
        public string difficultyId;
        public string knownMoveId;
        public int[] rawIvs;
        public int stars;
        public int level;
        public bool mutated;
    }

    [Serializable]
    public sealed class AuthoritativeClientBattleVital
    {
        public string entityId;
        public int teamIndex;
        public int rosterIndex;
        public int currentHp;
        public int currentAp;
        public bool isActive;
    }

    [Serializable]
    public sealed class AuthoritativeClientCaptureChallenge
    {
        public string attemptId;
    }
}
