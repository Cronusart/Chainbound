using System;
using System.Collections.Generic;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Aggregates local + server encounter modifiers and answers active-op queries.
    /// Discoverable as a MonoBehaviour (place under GameServices).
    /// </summary>
    public class EncounterModifierService : MonoBehaviour, IEncounterModifierSource
    {
        [Header("Local Modifiers")]
        [SerializeField] private List<LocalEncounterModifierSO> localModifiers = new List<LocalEncounterModifierSO>();

        [Header("Sources")]
        [SerializeField] private List<MonoBehaviour> additionalSources = new List<MonoBehaviour>();

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs;

        private static EncounterModifierService _instance;
        public static EncounterModifierService Instance => _instance != null
            ? _instance
            : FindFirstObjectByType<EncounterModifierService>(FindObjectsInactive.Include);

        private readonly List<IEncounterModifierSource> _runtimeSources = new List<IEncounterModifierSource>();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[EncounterModifierService] Duplicate found. Destroying.");
                Destroy(this);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        public void RegisterSource(IEncounterModifierSource source)
        {
            if (source == null || _runtimeSources.Contains(source))
                return;
            _runtimeSources.Add(source);
        }

        public void UnregisterSource(IEncounterModifierSource source)
        {
            _runtimeSources.Remove(source);
        }

        public IReadOnlyList<EncounterModifier> GetModifiers()
        {
            return CollectAll();
        }

        public List<EncounterModifier> GetActiveModifiers(
            string areaId,
            string zoneId,
            TimeSegment segment,
            EncounterDifficulty difficulty,
            long unixUtc = 0)
        {
            if (unixUtc <= 0)
                unixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var all = CollectAll();
            var active = new List<EncounterModifier>();
            for (int i = 0; i < all.Count; i++)
            {
                var mod = all[i];
                if (mod == null || !mod.IsActiveAt(unixUtc))
                    continue;
                // Species filter is applied later per entry/op; match area/zone/segment/difficulty here.
                if (!MatchesWithoutSpecies(mod, areaId, zoneId, segment, difficulty))
                    continue;
                active.Add(mod);
            }

            if (enableDebugLogs)
                Debug.Log($"[EncounterModifierService] Active modifiers: {active.Count} (area={areaId}, zone={zoneId}, seg={segment})");

            return active;
        }

        public List<EncounterModifierOp> GetMatchingOps(
            string areaId,
            string zoneId,
            string speciesId,
            TimeSegment segment,
            EncounterDifficulty difficulty,
            EncounterModifierOpType? filterType = null,
            long unixUtc = 0)
        {
            if (unixUtc <= 0)
                unixUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var all = CollectAll();
            var ops = new List<EncounterModifierOp>();
            for (int i = 0; i < all.Count; i++)
            {
                var mod = all[i];
                if (mod == null || !mod.IsActiveAt(unixUtc))
                    continue;

                // null speciesId = skip species filter (table/reward-level queries).
                bool scopeOk = speciesId == null
                    ? MatchesWithoutSpecies(mod, areaId, zoneId, segment, difficulty)
                    : mod.MatchesScope(areaId, zoneId, speciesId, segment, difficulty);
                if (!scopeOk)
                    continue;
                if (mod.ops == null)
                    continue;

                for (int o = 0; o < mod.ops.Count; o++)
                {
                    var op = mod.ops[o];
                    if (op == null)
                        continue;
                    if (filterType.HasValue && op.opType != filterType.Value)
                        continue;
                    ops.Add(op);
                }
            }
            return ops;
        }

        public float ApplyNumericOps(float baseValue, List<EncounterModifierOp> ops)
        {
            if (ops == null || ops.Count == 0)
                return baseValue;

            float value = baseValue;
            for (int i = 0; i < ops.Count; i++)
            {
                var op = ops[i];
                if (op == null)
                    continue;
                value = ApplyOp(value, op.mode, op.value);
            }
            return value;
        }

        public static float ApplyOp(float current, EncounterModifierOpMode mode, float opValue)
        {
            switch (mode)
            {
                case EncounterModifierOpMode.Add:
                    return current + opValue;
                case EncounterModifierOpMode.Set:
                    return opValue;
                case EncounterModifierOpMode.Multiply:
                default:
                    return current * opValue;
            }
        }

        private List<EncounterModifier> CollectAll()
        {
            var list = new List<EncounterModifier>();

            if (localModifiers != null)
            {
                for (int i = 0; i < localModifiers.Count; i++)
                {
                    if (localModifiers[i] != null && localModifiers[i].modifier != null)
                        list.Add(localModifiers[i].modifier);
                }
            }

            if (additionalSources != null)
            {
                for (int i = 0; i < additionalSources.Count; i++)
                {
                    if (additionalSources[i] is IEncounterModifierSource source)
                        AppendSource(list, source);
                }
            }

            for (int i = 0; i < _runtimeSources.Count; i++)
                AppendSource(list, _runtimeSources[i]);

            // Also pull the singleton server provider if present.
            var server = SupabaseEncounterEventsProvider.Instance;
            if (server != null && !_runtimeSources.Contains(server) &&
                (additionalSources == null || !additionalSources.Contains(server)))
            {
                AppendSource(list, server);
            }

            return list;
        }

        private static void AppendSource(List<EncounterModifier> list, IEncounterModifierSource source)
        {
            if (source == null)
                return;
            var mods = source.GetModifiers();
            if (mods == null)
                return;
            for (int i = 0; i < mods.Count; i++)
            {
                if (mods[i] != null)
                    list.Add(mods[i]);
            }
        }

        private static bool MatchesWithoutSpecies(
            EncounterModifier mod,
            string areaId,
            string zoneId,
            TimeSegment segment,
            EncounterDifficulty difficulty)
        {
            if (mod == null)
                return false;
            if (!MatchesList(mod.areaIds, areaId))
                return false;
            if (!MatchesList(mod.zoneIds, zoneId))
                return false;
            if (!TimeOfDayPredicateUtility.MatchesSegments(mod.segments, segment))
                return false;
            if (mod.filterByDifficulty && mod.difficulty != difficulty)
                return false;
            return true;
        }

        private static bool MatchesList(List<string> ids, string value)
        {
            if (ids == null || ids.Count == 0)
                return true;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string trimmed = value.Trim();
            for (int i = 0; i < ids.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(ids[i]) &&
                    string.Equals(ids[i].Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
