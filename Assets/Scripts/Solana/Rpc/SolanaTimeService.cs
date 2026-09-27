using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Provides Solana-authoritative cluster time with CST (UTC-6) helpers.
    /// Periodically syncs via getBlockTime RPC and interpolates locally between fetches.
    /// Falls back to DateTimeOffset.UtcNow if RPC is unavailable.
    /// </summary>
    public class SolanaTimeService
    {
        private const float SYNC_INTERVAL_SECONDS = 60f;
        private const int CST_OFFSET_HOURS = TimeOfDayClock.CstOffsetHours;

        private readonly IRpcService _rpc;

        private long _lastSyncedUnixTimestamp;
        private float _lastSyncRealtimeSince;
        private bool _hasSynced;
        private bool _hasClusterSync;
        private bool _syncInProgress;

        public SolanaTimeService(IRpcService rpc)
        {
            _rpc = rpc ?? throw new ArgumentNullException(nameof(rpc));
        }

        /// <summary>
        /// Best-estimate of the current cluster unix timestamp (seconds).
        /// Between RPC syncs, this drifts forward using local Time.realtimeSinceStartup.
        /// </summary>
        public long ClusterUnixTimestamp
        {
            get
            {
                if (!_hasSynced) return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                float elapsed = Time.realtimeSinceStartup - _lastSyncRealtimeSince;
                return _lastSyncedUnixTimestamp + (long)elapsed;
            }
        }

        /// <summary>
        /// Seconds remaining until the next CST midnight (UTC-6 00:00:00).
        /// </summary>
        public float SecondsUntilCstMidnight
        {
            get
            {
                var cstNow = DateTimeOffset.FromUnixTimeSeconds(ClusterUnixTimestamp)
                    .ToOffset(TimeSpan.FromHours(CST_OFFSET_HOURS));
                var nextMidnight = cstNow.Date.AddDays(1);
                double secs = (nextMidnight - cstNow.DateTime).TotalSeconds;
                return Mathf.Max(0f, (float)secs);
            }
        }

        /// <summary>
        /// Normalized time-of-day in CST (0 = midnight, 0.5 = noon, ~1 = 23:59).
        /// </summary>
        public float NormalizedCstTimeOfDay
        {
            get
            {
                return TimeOfDayClock.FromUnixSeconds(ClusterUnixTimestamp);
            }
        }

        /// <summary>Whether at least one successful RPC sync has completed.</summary>
        public bool HasSynced => _hasSynced;

        /// <summary>
        /// Whether the current estimate was seeded by an actual Solana block-time response.
        /// Unlike <see cref="HasSynced"/>, this is false for the client-clock fallback.
        /// </summary>
        public bool HasClusterSync => _hasClusterSync;

        /// <summary>
        /// Trigger an initial sync. Should be called once after construction (e.g. in Awake/Start).
        /// </summary>
        public async Task InitialSyncAsync()
        {
            await SyncClusterTimeAsync();
        }

        /// <summary>
        /// Call from a MonoBehaviour Update loop to periodically re-sync.
        /// </summary>
        public void Tick()
        {
            if (_syncInProgress) return;
            float elapsed = Time.realtimeSinceStartup - _lastSyncRealtimeSince;
            if (!_hasSynced || elapsed >= SYNC_INTERVAL_SECONDS)
            {
                _ = SyncClusterTimeAsync();
            }
        }

        private async Task SyncClusterTimeAsync()
        {
            if (_syncInProgress) return;
            _syncInProgress = true;

            try
            {
                long? blockTime = await _rpc.GetBlockTimeAsync();
                if (blockTime.HasValue)
                {
                    _lastSyncedUnixTimestamp = blockTime.Value;
                    _lastSyncRealtimeSince = Time.realtimeSinceStartup;
                    _hasSynced = true;
                    _hasClusterSync = true;
                    Debug.Log($"[SolanaTimeService] Synced cluster time: {blockTime.Value} (CST: {DateTimeOffset.FromUnixTimeSeconds(blockTime.Value).ToOffset(TimeSpan.FromHours(CST_OFFSET_HOURS)):HH:mm:ss})");
                }
                else
                {
                    if (!_hasSynced)
                    {
                        _lastSyncedUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        _lastSyncRealtimeSince = Time.realtimeSinceStartup;
                        _hasSynced = true;
                        Debug.LogWarning("[SolanaTimeService] RPC block time unavailable; falling back to client UTC.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SolanaTimeService] Sync failed: {ex.Message}");
                if (!_hasSynced)
                {
                    _lastSyncedUnixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    _lastSyncRealtimeSince = Time.realtimeSinceStartup;
                    _hasSynced = true;
                }
            }
            finally
            {
                _syncInProgress = false;
            }
        }
    }
}
