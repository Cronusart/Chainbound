using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

namespace SignalJam.UI.MenuNavigation
{
    public struct GameplayMenuAvailabilityToken : IDisposable
    {
        private readonly int id;
        private readonly Action<int> releaseCallback;
        private bool isReleased;

        public int Id => id;
        public string Reason { get; }
        public bool IsReleased => isReleased;
        public bool IsValid => releaseCallback != null;

        internal GameplayMenuAvailabilityToken(int id, string reason, Action<int> releaseCallback)
        {
            this.id = id;
            Reason = reason;
            this.releaseCallback = releaseCallback;
            isReleased = false;
        }

        public void Release()
        {
            if (isReleased || releaseCallback == null) return;
            isReleased = true;
            releaseCallback(id);
        }

        public void Dispose() => Release();
    }

    /// <summary>
    /// Gate for the gameplay menu. Narrative and tutorial systems acquire
    /// suppression tokens while they need the menu unavailable.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayMenuAvailabilityService : MonoBehaviour
    {
        private static GameplayMenuAvailabilityService instance;

        public static GameplayMenuAvailabilityService Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GameplayMenuAvailabilityService>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("GameplayMenuAvailabilityService");
                        instance = go.AddComponent<GameplayMenuAvailabilityService>();
                        DontDestroyOnLoad(go);
                    }
                }

                return instance;
            }
        }

        [Header("Progression Gate")]
        [SerializeField] private bool requireIntroComplete = true;
        [SerializeField] private string introCompletePrefsKey = "SignalJam_IntroQuestComplete";

        [Header("Debug Overrides")]
        [SerializeField] private bool forceAvailable = false;
        [SerializeField] private bool forceUnavailable = false;
        [SerializeField] private bool enableDebugLogs = false;

        private int nextTokenId = 1;
        private readonly Dictionary<int, string> activeSuppressions = new Dictionary<int, string>();

        public bool HasSuppression => activeSuppressions.Count > 0;
        public int ActiveSuppressionCount => activeSuppressions.Count;

        public bool CanOpen
        {
            get
            {
                if (forceUnavailable) return false;
                if (forceAvailable) return true;
                if (HasSuppression) return false;

                InputLockService inputLock = InputLockService.Instance;
                if (inputLock != null && inputLock.IsPlayerMenuLocked)
                    return false;

                OnScreenKeyboardService keyboard = OnScreenKeyboardService.Instance;
                if (keyboard != null && keyboard.IsOpen)
                    return false;

                return !requireIntroComplete || IsIntroComplete();
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public GameplayMenuAvailabilityToken AcquireSuppression(string reason)
        {
            int id = nextTokenId++;
            string effectiveReason = string.IsNullOrWhiteSpace(reason) ? "gameplay_menu_suppressed" : reason.Trim();
            activeSuppressions[id] = effectiveReason;

            if (enableDebugLogs)
                Debug.Log($"[GameplayMenuAvailabilityService] Suppressed: {effectiveReason} id={id}");

            return new GameplayMenuAvailabilityToken(id, effectiveReason, ReleaseSuppression);
        }

        public IReadOnlyList<string> GetActiveReasons()
        {
            var reasons = new List<string>(activeSuppressions.Count);
            foreach (string reason in activeSuppressions.Values)
                reasons.Add(reason);
            return reasons;
        }

        public void ClearAllSuppressions()
        {
            if (activeSuppressions.Count == 0) return;

            if (enableDebugLogs)
                Debug.Log($"[GameplayMenuAvailabilityService] Clearing {activeSuppressions.Count} suppressions.");

            activeSuppressions.Clear();
        }

        private void ReleaseSuppression(int id)
        {
            if (!activeSuppressions.TryGetValue(id, out string reason)) return;
            activeSuppressions.Remove(id);

            if (enableDebugLogs)
                Debug.Log($"[GameplayMenuAvailabilityService] Released: {reason} id={id}");
        }

        private bool IsIntroComplete()
        {
            GameSessionContext session = GameSessionContext.Instance;
            if (session != null && session.IntroComplete)
                return true;

            return !string.IsNullOrWhiteSpace(introCompletePrefsKey) &&
                PlayerPrefs.GetInt(introCompletePrefsKey, 0) == 1;
        }
    }
}
