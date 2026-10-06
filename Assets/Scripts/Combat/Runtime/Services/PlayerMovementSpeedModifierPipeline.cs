using System;
using System.Collections.Generic;

namespace Chainbound.Combat
{
    /// <summary>
    /// Who registered a movement-speed source. The kind is bookkeeping for wearables
    /// and party passives; stacking does not change by kind.
    /// </summary>
    public enum PlayerMovementSpeedModifierKind
    {
        Consumable = 0,
        Wearable = 1,
        PartyPassive = 2,
    }

    /// <summary>
    /// Snapshot of one registered speed source.
    /// </summary>
    public readonly struct PlayerMovementSpeedModifierState
    {
        public readonly string SourceId;
        public readonly PlayerMovementSpeedModifierKind Kind;
        public readonly float BonusFraction;
        public readonly bool Expires;
        public readonly float ExpiresAt;

        public PlayerMovementSpeedModifierState(
            string sourceId,
            PlayerMovementSpeedModifierKind kind,
            float bonusFraction,
            bool expires,
            float expiresAt)
        {
            SourceId = sourceId ?? "";
            Kind = kind;
            BonusFraction = bonusFraction;
            Expires = expires;
            ExpiresAt = expiresAt;
        }
    }

    /// <summary>
    /// Additive movement-speed sources that collapse to one multiplier.
    ///
    /// Stacking:
    /// speedMultiplier = max(0, 1 + sum(source.bonusFraction))
    ///
    /// PlayerMovement3D is the only writer of planar speed. It multiplies this value
    /// with every other IPlayerMovementModifier (terrain volumes) and the sprint
    /// multiplier. The serialized moveSpeed baseline is never written.
    ///
    /// Same sourceId refreshes in place: magnitude is replaced and a timed source's
    /// expiry is reset. It does not stack with itself. Distinct ids stack.
    /// durationSeconds &lt;= 0 stays until <see cref="RemoveSource"/> or <see cref="Clear"/>.
    /// durationSeconds &gt; 0 expires at now + durationSeconds. <see cref="Expire"/>
    /// drops due sources so a remaining wearable or surface modifier is unchanged.
    /// </summary>
    public sealed class PlayerMovementSpeedModifierPipeline
    {
        private sealed class Entry
        {
            public string SourceId;
            public PlayerMovementSpeedModifierKind Kind;
            public float BonusFraction;
            public bool Expires;
            public float ExpiresAt;
        }

        private readonly List<Entry> sources = new List<Entry>();

        public int SourceCount => sources.Count;

        public float SpeedMultiplier
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < sources.Count; i++)
                    sum += sources[i].BonusFraction;
                float multiplier = 1f + sum;
                return multiplier < 0f ? 0f : multiplier;
            }
        }

        public bool SetSource(
            string sourceId,
            PlayerMovementSpeedModifierKind kind,
            float bonusFraction,
            float durationSeconds,
            float now)
        {
            string id = (sourceId ?? "").Trim();
            if (id.Length == 0)
                return false;

            float fraction = SanitizeFraction(bonusFraction);
            bool expires = durationSeconds > 0f;
            float expiresAt = expires ? now + durationSeconds : 0f;

            for (int i = 0; i < sources.Count; i++)
            {
                Entry existing = sources[i];
                if (!string.Equals(existing.SourceId, id, StringComparison.Ordinal))
                    continue;

                existing.Kind = kind;
                existing.BonusFraction = fraction;
                existing.Expires = expires;
                existing.ExpiresAt = expiresAt;
                return true;
            }

            sources.Add(new Entry
            {
                SourceId = id,
                Kind = kind,
                BonusFraction = fraction,
                Expires = expires,
                ExpiresAt = expiresAt,
            });
            return true;
        }

        public bool RemoveSource(string sourceId)
        {
            string id = (sourceId ?? "").Trim();
            if (id.Length == 0)
                return false;

            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(sources[i].SourceId, id, StringComparison.Ordinal))
                    continue;
                sources.RemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Drops timed sources whose expiry is at or before <paramref name="now"/>.
        /// Permanent sources stay.
        /// </summary>
        public int Expire(float now)
        {
            int removed = 0;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                Entry entry = sources[i];
                if (!entry.Expires || entry.ExpiresAt > now)
                    continue;
                sources.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Drops every timed source immediately. Used when the timer is cancelled
        /// or the player/movement object is disabled so a boost cannot stick.
        /// </summary>
        public int RemoveExpiringSources()
        {
            int removed = 0;
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (!sources[i].Expires)
                    continue;
                sources.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        public void Clear()
        {
            sources.Clear();
        }

        public bool TryGetSource(string sourceId, out PlayerMovementSpeedModifierState state)
        {
            string id = (sourceId ?? "").Trim();
            for (int i = 0; i < sources.Count; i++)
            {
                Entry entry = sources[i];
                if (!string.Equals(entry.SourceId, id, StringComparison.Ordinal))
                    continue;

                state = new PlayerMovementSpeedModifierState(
                    entry.SourceId,
                    entry.Kind,
                    entry.BonusFraction,
                    entry.Expires,
                    entry.ExpiresAt);
                return true;
            }

            state = default;
            return false;
        }

        private static float SanitizeFraction(float bonusFraction)
        {
            if (float.IsNaN(bonusFraction) || float.IsInfinity(bonusFraction))
                return 0f;
            return bonusFraction;
        }
    }

    /// <summary>
    /// Player-facing sink for speed sources. Implemented by the component on PlayerRoot
    /// that registers itself with PlayerMovement3D.
    /// </summary>
    public interface IPlayerMovementSpeedModifierHost
    {
        void SetSource(
            string sourceId,
            PlayerMovementSpeedModifierKind kind,
            float bonusFraction,
            float durationSeconds);

        bool RemoveSource(string sourceId);

        void Clear();

        float SpeedMultiplier { get; }
    }

    /// <summary>
    /// Live hosts on enabled players. Item use and future wearables call this so they
    /// never write <c>PlayerMovement3D.moveSpeed</c>.
    ///
    /// Wearable / party passive hook (no expiry, removed by the owner):
    /// PlayerMovementSpeedModifierRuntime.TrySetSource(
    ///     "wearable.example_boots",
    ///     PlayerMovementSpeedModifierKind.Wearable,
    ///     0.10f,
    ///     0f,
    ///     out _);
    /// </summary>
    public static class PlayerMovementSpeedModifierRuntime
    {
        private static readonly List<IPlayerMovementSpeedModifierHost> hosts =
            new List<IPlayerMovementSpeedModifierHost>();

        public static int HostCount
        {
            get
            {
                Prune();
                return hosts.Count;
            }
        }

        public static void Register(IPlayerMovementSpeedModifierHost host)
        {
            if (host == null || Contains(host))
                return;
            hosts.Add(host);
        }

        public static void Unregister(IPlayerMovementSpeedModifierHost host)
        {
            if (host == null)
                return;

            for (int i = hosts.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(hosts[i], host))
                    hosts.RemoveAt(i);
            }
        }

        public static bool TryApplyConsumable(out string error)
        {
            return TrySetSource(
                PlayerMovementSpeedConsumable.SourceId,
                PlayerMovementSpeedModifierKind.Consumable,
                PlayerMovementSpeedConsumable.BonusFraction,
                PlayerMovementSpeedConsumable.DurationSeconds,
                out error);
        }

        public static bool TrySetSource(
            string sourceId,
            PlayerMovementSpeedModifierKind kind,
            float bonusFraction,
            float durationSeconds,
            out string error)
        {
            Prune();
            string id = (sourceId ?? "").Trim();
            if (id.Length == 0)
            {
                error = "movement_speed_source_id_required";
                return false;
            }

            if (hosts.Count == 0)
            {
                error = "movement_speed_host_missing";
                return false;
            }

            for (int i = 0; i < hosts.Count; i++)
                hosts[i].SetSource(id, kind, bonusFraction, durationSeconds);

            error = "";
            return true;
        }

        /// <summary>Test isolation. Does not clear pipelines on hosts that were registered.</summary>
        public static void ClearHosts()
        {
            hosts.Clear();
        }

        private static bool Contains(IPlayerMovementSpeedModifierHost host)
        {
            for (int i = 0; i < hosts.Count; i++)
            {
                if (ReferenceEquals(hosts[i], host))
                    return true;
            }

            return false;
        }

        private static void Prune()
        {
            for (int i = hosts.Count - 1; i >= 0; i--)
            {
                object host = hosts[i];
                if (host == null)
                {
                    hosts.RemoveAt(i);
                    continue;
                }

                if (host is UnityEngine.Object unityObject && unityObject == null)
                    hosts.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Nitro Brew tuning. Call sites read these constants; do not duplicate the numbers.
    /// </summary>
    public static class PlayerMovementSpeedConsumable
    {
        public const string ItemId = "nitro_brew";
        public const string SourceId = "consumable.nitro_brew";
        public const float BonusFraction = 0.15f;
        public const float DurationSeconds = 30f;

        public static bool TryApply(out string error)
        {
            return PlayerMovementSpeedModifierRuntime.TryApplyConsumable(out error);
        }
    }
}
