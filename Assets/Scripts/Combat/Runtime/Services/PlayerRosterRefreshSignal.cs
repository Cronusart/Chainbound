using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Persistent, profile-scoped invalidation signal for roster readers.
    /// It carries no mutable roster data; screens rehydrate through profile_get_chainlings.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerRosterRefreshSignal : MonoBehaviour
    {
        public readonly struct Notice
        {
            public Notice(long sequence, string playerProfileId, string chainlingId)
            {
                Sequence = sequence;
                PlayerProfileId = playerProfileId ?? string.Empty;
                ChainlingId = chainlingId ?? string.Empty;
            }

            public long Sequence { get; }
            public string PlayerProfileId { get; }
            public string ChainlingId { get; }
        }

        private static PlayerRosterRefreshSignal instance;
        private readonly Dictionary<string, Notice> latestByProfile = new Dictionary<string, Notice>(StringComparer.Ordinal);
        private long nextSequence;

        public event Action<Notice> Changed;

        public static PlayerRosterRefreshSignal ResolveOrCreate()
        {
            if (instance != null)
                return instance;

            instance = FindFirstObjectByType<PlayerRosterRefreshSignal>(FindObjectsInactive.Include);
            if (instance != null)
                return instance;

            var serviceObject = new GameObject("[PlayerRosterRefreshSignal]");
            DontDestroyOnLoad(serviceObject);
            instance = serviceObject.AddComponent<PlayerRosterRefreshSignal>();
            return instance;
        }

        public static void Publish(string playerProfileId, string chainlingId)
        {
            if (string.IsNullOrWhiteSpace(playerProfileId))
                return;

            ResolveOrCreate().PublishInternal(playerProfileId.Trim(), chainlingId);
        }

        public static bool TryGetLatest(string playerProfileId, out Notice notice)
        {
            notice = default;
            return !string.IsNullOrWhiteSpace(playerProfileId) &&
                   ResolveOrCreate().latestByProfile.TryGetValue(playerProfileId.Trim(), out notice);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void PublishInternal(string playerProfileId, string chainlingId)
        {
            var notice = new Notice(++nextSequence, playerProfileId, chainlingId);
            latestByProfile[playerProfileId] = notice;
            Changed?.Invoke(notice);
        }
    }
}
