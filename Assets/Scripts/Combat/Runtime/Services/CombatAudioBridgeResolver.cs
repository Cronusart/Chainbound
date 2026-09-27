using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Shared scene/service resolution for combat presentation audio.
    /// Callers cache the returned behaviour; this helper only owns the lookup policy.
    /// </summary>
    public static class CombatAudioBridgeResolver
    {
        public static ICombatAudioBridge Resolve(
            MonoBehaviour configuredBehaviour,
            bool autoDiscover,
            out MonoBehaviour resolvedBehaviour)
        {
            if (configuredBehaviour is ICombatAudioBridge configuredBridge)
            {
                resolvedBehaviour = configuredBehaviour;
                return configuredBridge;
            }

            resolvedBehaviour = null;
            if (!autoDiscover)
                return null;

            MonoBehaviour[] behaviours = Object.FindObjectsOfType<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour is ICombatAudioBridge bridge)
                {
                    resolvedBehaviour = behaviour;
                    return bridge;
                }
            }

            return null;
        }
    }
}
