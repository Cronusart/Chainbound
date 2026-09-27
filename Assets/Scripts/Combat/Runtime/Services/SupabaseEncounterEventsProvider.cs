using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Chainbound.Combat
{
    /// <summary>
    /// Fetches active server-wide encounter events from Supabase edge function
    /// <c>get_encounter_events</c> and exposes them as <see cref="IEncounterModifierSource"/>.
    /// Degrades gracefully offline (empty cache; local modifiers still apply).
    /// </summary>
    public class SupabaseEncounterEventsProvider : MonoBehaviour, IEncounterModifierSource
    {
        [SerializeField] private SupabaseConfigSO supabaseConfig;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float refreshIntervalSeconds = 300f;
        [SerializeField] private bool autoRegisterWithModifierService = true;

        [Tooltip("Fetch once on Start so events apply without waiting for the refresh interval.")]
        [SerializeField] private bool fetchOnStart = true;

        public static SupabaseEncounterEventsProvider Instance { get; private set; }

        public IReadOnlyList<EncounterModifier> CachedModifiers => _cached;
        public string LastError { get; private set; }
        public long LastFetchUnixUtc { get; private set; }

        private readonly List<EncounterModifier> _cached = new List<EncounterModifier>();
        private float _refreshTimer;
        private bool _fetchInFlight;

        public void Configure(SupabaseConfigSO config)
        {
            if (config != null)
                supabaseConfig = config;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SupabaseEncounterEventsProvider] Duplicate found. Destroying.");
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            if (autoRegisterWithModifierService && EncounterModifierService.Instance != null)
                EncounterModifierService.Instance.RegisterSource(this);
        }

        private void Start()
        {
            if (fetchOnStart)
                _ = FetchActiveEventsAsync();
        }

        private void OnDisable()
        {
            if (EncounterModifierService.Instance != null)
                EncounterModifierService.Instance.UnregisterSource(this);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (refreshIntervalSeconds <= 0f)
                return;
            _refreshTimer += Time.unscaledDeltaTime;
            if (_refreshTimer < refreshIntervalSeconds)
                return;
            _refreshTimer = 0f;
            _ = FetchActiveEventsAsync();
        }

        public IReadOnlyList<EncounterModifier> GetModifiers() => _cached;

        public async Task<IReadOnlyList<EncounterModifier>> FetchActiveEventsAsync()
        {
            if (_fetchInFlight)
                return _cached;

            LastError = "";
            if (supabaseConfig == null)
            {
                LastError = "SupabaseConfigSO was not assigned.";
                if (enableDebugLogs)
                    Debug.LogWarning($"[SupabaseEncounterEventsProvider] {LastError}");
                return _cached;
            }

            _fetchInFlight = true;
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = JsonUtility.ToJson(new EventsRequest
                {
                    now_unix = now,
                });

                SupabaseClient.Response response = await SupabaseClient.PostJsonAsync(
                    supabaseConfig,
                    "get_encounter_events",
                    json);

                if (!response.IsSuccess)
                {
                    LastError = $"get_encounter_events returned {response.statusCode}: {response.body}";
                    if (enableDebugLogs)
                        Debug.LogWarning($"[SupabaseEncounterEventsProvider] {LastError}");
                    return _cached;
                }

                EventsResponse payload = JsonUtility.FromJson<EventsResponse>(response.body);
                if (payload == null)
                {
                    LastError = "get_encounter_events returned an invalid payload.";
                    return _cached;
                }

                _cached.Clear();
                List<EventRow> rows = payload.events ?? new List<EventRow>();
                for (int i = 0; i < rows.Count; i++)
                {
                    EncounterModifier mod = MapRow(rows[i]);
                    if (mod != null)
                        _cached.Add(mod);
                }

                LastFetchUnixUtc = now;
                if (enableDebugLogs)
                    Debug.Log($"[SupabaseEncounterEventsProvider] Cached {_cached.Count} active event(s).");

                if (autoRegisterWithModifierService && EncounterModifierService.Instance != null)
                    EncounterModifierService.Instance.RegisterSource(this);

                return _cached;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                if (enableDebugLogs)
                    Debug.LogWarning($"[SupabaseEncounterEventsProvider] Exception: {ex.Message}");
                return _cached;
            }
            finally
            {
                _fetchInFlight = false;
            }
        }

        public static SupabaseEncounterEventsProvider ResolveOrCreate(SupabaseConfigSO config = null)
        {
            var existing = Instance ?? FindFirstObjectByType<SupabaseEncounterEventsProvider>(FindObjectsInactive.Include);
            if (existing == null)
            {
                var go = new GameObject("[SupabaseEncounterEventsProvider]");
                DontDestroyOnLoad(go);
                existing = go.AddComponent<SupabaseEncounterEventsProvider>();
            }
            if (config != null)
                existing.Configure(config);
            return existing;
        }

        private static EncounterModifier MapRow(EventRow row)
        {
            if (row == null)
                return null;

            // Prefer structured payload fields when present; fall back to nested EncounterModifierDto.
            EncounterModifier mod = row.payload != null ? MapDto(row.payload) : new EncounterModifier();
            if (string.IsNullOrWhiteSpace(mod.modifierId))
                mod.modifierId = !string.IsNullOrWhiteSpace(row.id) ? row.id : row.name;
            if (string.IsNullOrWhiteSpace(mod.displayName))
                mod.displayName = row.name ?? mod.modifierId;
            if (row.starts_at_unix > 0)
                mod.startUnixUtc = row.starts_at_unix;
            if (row.ends_at_unix > 0)
                mod.endUnixUtc = row.ends_at_unix;
            return mod;
        }

        private static EncounterModifier MapDto(EncounterModifierDto dto)
        {
            var mod = new EncounterModifier
            {
                modifierId = dto.modifier_id ?? "",
                displayName = dto.display_name ?? "",
                segments = (TimeSegmentMask)dto.segments,
                filterByDifficulty = dto.filter_by_difficulty,
                difficulty = (EncounterDifficulty)dto.difficulty,
                startUnixUtc = dto.start_unix_utc,
                endUnixUtc = dto.end_unix_utc,
            };

            if (dto.area_ids != null)
                mod.areaIds.AddRange(dto.area_ids);
            if (dto.zone_ids != null)
                mod.zoneIds.AddRange(dto.zone_ids);
            if (dto.species_ids != null)
                mod.speciesIds.AddRange(dto.species_ids);

            if (dto.ops != null)
            {
                for (int i = 0; i < dto.ops.Length; i++)
                {
                    var opDto = dto.ops[i];
                    if (opDto == null)
                        continue;
                    mod.ops.Add(new EncounterModifierOp
                    {
                        opType = (EncounterModifierOpType)opDto.op_type,
                        targetId = opDto.target_id ?? "",
                        mode = (EncounterModifierOpMode)opDto.mode,
                        value = opDto.value,
                        injectWeight = opDto.inject_weight,
                        injectCaptureRate = opDto.inject_capture_rate,
                        injectBaseStars = opDto.inject_base_stars,
                    });
                }
            }

            return mod;
        }

        [Serializable]
        private class EventsRequest
        {
            public long now_unix;
        }

        [Serializable]
        private class EventsResponse
        {
            public List<EventRow> events;
            public string error;
        }

        [Serializable]
        private class EventRow
        {
            public string id;
            public string name;
            public long starts_at_unix;
            public long ends_at_unix;
            public bool enabled;
            public EncounterModifierDto payload;
        }

        [Serializable]
        private class EncounterModifierDto
        {
            public string modifier_id;
            public string display_name;
            public string[] area_ids;
            public string[] zone_ids;
            public string[] species_ids;
            public int segments;
            public bool filter_by_difficulty;
            public int difficulty;
            public long start_unix_utc;
            public long end_unix_utc;
            public EncounterModifierOpDto[] ops;
        }

        [Serializable]
        private class EncounterModifierOpDto
        {
            public int op_type;
            public string target_id;
            public int mode;
            public float value;
            public int inject_weight;
            public int inject_capture_rate;
            public int inject_base_stars;
        }
    }
}
