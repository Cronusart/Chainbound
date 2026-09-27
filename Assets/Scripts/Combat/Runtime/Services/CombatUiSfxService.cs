using UnityEngine;

namespace Chainbound.Combat
{
    public enum CombatUiSfxCue
    {
        Navigate,
        Confirm,
        Cancel,
        Invalid,
        TargetChanged,
    }

    public interface ICombatUiSfxPlayer
    {
        void PlayCue(CombatUiSfxCue cue);
    }

    public static class CombatUiSfxResolver
    {
        public static ICombatUiSfxPlayer Resolve(MonoBehaviour assigned, ref MonoBehaviour cached)
        {
            if (assigned != null && assigned is ICombatUiSfxPlayer assignedPlayer)
                return assignedPlayer;

            if (cached != null && cached is ICombatUiSfxPlayer cachedPlayer)
                return cachedPlayer;

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatUiSfxPlayer player)
                {
                    cached = behaviours[i];
                    return player;
                }
            }

            return null;
        }

        public static ICombatAudioBridge ResolveAudioBridge(MonoBehaviour assigned, ref MonoBehaviour cached)
        {
            if (assigned != null && assigned is ICombatAudioBridge assignedBridge)
                return assignedBridge;

            if (cached != null && cached is ICombatAudioBridge cachedBridge)
                return cachedBridge;

            MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ICombatAudioBridge bridge)
                {
                    cached = behaviours[i];
                    return bridge;
                }
            }

            return null;
        }
    }
}
