using System;
using UnityEngine;
using Chainbound.Combat;

/// <summary>
/// Zone-based encounter trigger. Place on GameObjects with a Collider (isTrigger = true).
/// Uses <see cref="EncounterRollService"/> for local seeded rolls with full TimeOfDay segments.
/// Enter also arms the nearest sanctuary portal for wipe/revive.
/// Lives under World/Environment (Assembly-CSharp) so it can reference <see cref="EncounterTransitionBridge"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public class EncounterTriggerZone : MonoBehaviour
{
    [Header("Zone Config")]
    [SerializeField] private EncounterZoneSO zone;

    [Tooltip("Fallback wild trait config when area/zone do not supply one.")]
    [SerializeField] private WildRollConfig wildConfig = new WildRollConfig();

    [Header("Quest Availability (Optional)")]
    [Tooltip("When configured, encounter rolls fail closed until this narrative quest guard passes. Empty guards preserve legacy behavior.")]
    [SerializeField] private NarrativeQuestGuard questGuard = new NarrativeQuestGuard();

    [Tooltip("Optional scene reference. The active QuestService is resolved automatically when left empty.")]
    [SerializeField] private QuestService questService;

    [Header("Scene")]
    [SerializeField] private string battleSceneName = "Combat2_TestHarness";

    [Header("Player Detection")]
    [SerializeField] private LayerMask playerLayer = ~0;

    [Header("Cooldown")]
    [SerializeField] private float cooldownSeconds = 10f;
    [SerializeField] private float minDistanceBetweenEncounters = 5f;

    [Header("Debug")]
    [SerializeField] private bool debugForceEncounter;
    [SerializeField] private bool debugIgnoreCadence;
    [SerializeField] private bool enableDebugLogs;

    private static float _lastEncounterTime = -999f;
    private static Vector3 _lastEncounterPos = Vector3.zero;
    private bool _isProcessing;
    private float _cadenceTimer;
    private int _attemptCounter;
    private bool _playerInside;
    private Transform _playerTransform;
    private EncounterRollService _rollService;

    /// <summary>
    /// Whether this zone's optional quest guard currently permits encounters.
    /// Empty guards remain open for backward compatibility; configured guards fail closed
    /// while narrative profile state is unavailable or still hydrating.
    /// </summary>
    public bool IsQuestAvailable => PassesQuestGuard();

    private void Start()
    {
        var col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            if (enableDebugLogs)
                Debug.LogWarning($"[EncounterTriggerZone] Collider on {gameObject.name} was not a trigger. Auto-fixed.");
        }
    }

    private void Update()
    {
        if (!_playerInside || _isProcessing || _playerTransform == null) return;
        if (zone == null || zone.cadence != EncounterCadence.PerSecond) return;

        _cadenceTimer += Time.deltaTime;
        float interval = debugIgnoreCadence ? 0f : zone.cadenceIntervalSeconds;
        if (_cadenceTimer < interval) return;

        _cadenceTimer = 0f;
        TryRoll(_playerTransform);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isProcessing) return;
        if (!IsPlayer(other)) return;

        _playerInside = true;
        _playerTransform = other.transform;
        _cadenceTimer = 0f;

        SanctuaryLatchZone.TryLatchNearestPortal(other.transform.position);

        if (zone != null && zone.cadence == EncounterCadence.OnEnter)
            TryRoll(other.transform);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        _playerInside = false;
        _playerTransform = null;
    }

    private void TryRoll(Transform player)
    {
        if (_isProcessing) return;
        if (InputLockService.BlocksWorldGameplay)
        {
            if (enableDebugLogs)
                Debug.Log("[EncounterTriggerZone] Skipped roll because world gameplay is frozen.");
            return;
        }
        if (zone == null)
        {
            if (enableDebugLogs) Debug.LogWarning("[EncounterTriggerZone] No EncounterZoneSO assigned.");
            return;
        }

        if (!PassesQuestGuard())
            return;

        if (!debugIgnoreCadence && !PassesCooldown(player))
            return;

        EnsureServices();

        if (_rollService == null)
        {
            Debug.LogError("[EncounterTriggerZone] EncounterRollService not available.");
            return;
        }

        var ctx = BuildContext(player);
        _attemptCounter++;
        ctx.attemptCounter = _attemptCounter;

        ZoneEncounterResult result;
        bool hit;
        MoveDatabaseSO moveDb = SolanaServiceBridge.Instance != null
            ? SolanaServiceBridge.Instance.MoveDatabase
            : null;

        if (debugForceEncounter)
        {
            hit = ForceRoll(ctx, moveDb, out result);
        }
        else
        {
            hit = _rollService.TryRollEncounter(
                zone,
                ctx,
                wildConfig,
                out result,
                moveDatabase: moveDb,
                modifierService: EncounterModifierService.Instance);
        }

        if (!hit) return;

        _isProcessing = true;
        _lastEncounterTime = Time.time;
        _lastEncounterPos = player.position;

        StartCoroutine(TransitionToBattle(player, result));
    }

    private bool ForceRoll(EncounterContext ctx, MoveDatabaseSO moveDb, out ZoneEncounterResult result)
    {
        // Reuse the real roll path with chance forced via a temporary resolve + always-hit.
        // Deterministic: run TryRollEncounter against a cloned chance of 1 via modifier-less resolve,
        // then if the chance gate would fail, still build from first available entry.
        EffectiveEncounterTable table = EncounterTableResolver.Resolve(
            zone, wildConfig, ctx.segment, EncounterModifierService.Instance);
        table.encounterChance = 1f;

        var rng = new System.Random(ctx.attemptCounter);
        if (table.entries == null || table.entries.Count == 0)
        {
            result = null;
            return false;
        }

        int partySize = Mathf.Clamp(
            table.minPartySize == table.maxPartySize
                ? table.minPartySize
                : table.minPartySize + rng.Next(table.maxPartySize - table.minPartySize + 1),
            1, 2);

        var slots = new System.Collections.Generic.List<ZoneEncounterSlot>();
        for (int i = 0; i < partySize; i++)
        {
            var entry = table.entries[i % table.entries.Count];
            var traits = WildRoller.RollTraits(table.wildConfig, rng);
            var moveRoll = WildMoveRoller.Roll(
                entry.movePool, entry.battleKitSizeMin, entry.battleKitSizeMax, rng, moveDb, entry.moveWeightMultipliers);
            var visual = WildVariantRoller.Roll(entry.variantPool, rng, traits.isEntangled, entry.variantWeightMultipliers);
            int stars = Mathf.Clamp(entry.baseStars, 0, CombatConstants.STAR_MAX);
            string knownMoveId = moveRoll.knownMoveId;
            if (zone != null && zone.speciesDatabase != null)
            {
                knownMoveId = ProgressionKnownMoveRoll.TryRoll(
                    zone.speciesDatabase.GetById(entry.speciesId),
                    stars,
                    traits.isEntangled,
                    rng,
                    zone.progressionRewardTables,
                    zone.progressionConfig);
            }

            slots.Add(new ZoneEncounterSlot
            {
                speciesId = entry.speciesId,
                traits = traits,
                stars = stars,
                level = 0,
                baseCaptureRate = entry.baseCaptureRate,
                moveIds = moveRoll.battleMoveIds,
                knownMoveId = knownMoveId,
                visualIdentity = visual,
            });
        }

        result = new ZoneEncounterResult
        {
            slots = slots,
            zoneId = table.zoneId,
            areaId = table.areaId,
            segment = table.segment,
            seed = ctx.attemptCounter,
            difficulty = table.difficulty,
            bossIntroDialogueAsset = zone.bossIntroDialogueAsset,
            debugLogLine = "[FORCED] " + table.debugResolvedSummary,
            activeModifierIds = new System.Collections.Generic.List<string>(table.activeModifierIds),
        };
        return true;
    }

    private System.Collections.IEnumerator TransitionToBattle(Transform player, ZoneEncounterResult result)
    {
        EncounterTransitionBridge transitionBridge = EncounterTransitionBridge.Instance;
        if (transitionBridge == null)
        {
            Debug.LogError("[EncounterTriggerZone] EncounterTransitionBridge is unavailable. Cannot load battle scene.");
            _isProcessing = false;
            yield break;
        }

        yield return transitionBridge.BeginEncounterTransition(new EncounterTransitionBridge.EncounterTransitionRequest
        {
            playerTransform = player,
            battleSceneName = battleSceneName,
            battleSpawnId = zone != null ? zone.ResolveCombatStageId() : CombatStageIds.Default,
            launchSource = EncounterTransitionBridge.EncounterLaunchSource.Zone,
            authoritativeZoneId = result != null ? result.zoneId : "",
            onReadyToLoad = () => SetActivePayload(result),
        });

        _isProcessing = false;
    }

    private bool IsPlayer(Collider other)
    {
        if (playerLayer != ~0 && ((1 << other.gameObject.layer) & playerLayer) == 0)
            return false;
        return other.CompareTag("Player");
    }

    private bool PassesCooldown(Transform player)
    {
        if (Time.time - _lastEncounterTime < cooldownSeconds)
        {
            if (enableDebugLogs)
                Debug.Log($"[EncounterTriggerZone] Cooldown ({cooldownSeconds - (Time.time - _lastEncounterTime):F1}s left).");
            return false;
        }

        float dist = Vector3.Distance(player.position, _lastEncounterPos);
        if (_lastEncounterPos != Vector3.zero && dist < minDistanceBetweenEncounters)
        {
            if (enableDebugLogs)
                Debug.Log($"[EncounterTriggerZone] Too close ({dist:F1}m < {minDistanceBetweenEncounters}m).");
            return false;
        }
        return true;
    }

    private EncounterContext BuildContext(Transform player)
    {
        var bridge = SolanaServiceBridge.Instance;
        bool isNight = false;
        TimeSegment segment = TimeSegment.Day;
        int dayNumber = 0;
        string wallet = null;

        if (TimeOfDayManager.Instance != null)
        {
            isNight = TimeOfDayManager.Instance.IsNight;
            segment = TimeOfDayManager.Instance.CurrentSegment;
        }

        if (bridge?.TimeService != null)
        {
            long ts = bridge.TimeService.ClusterUnixTimestamp;
            dayNumber = (int)(ts / 86400);
        }
        else
        {
            dayNumber = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 86400);
        }

        if (bridge?.WalletService != null)
            wallet = bridge.WalletService.PublicKey;

        return new EncounterContext
        {
            position = player.position,
            isNight = isNight,
            segment = segment,
            dayNumber = dayNumber,
            walletPubkey = wallet,
            attemptCounter = _attemptCounter,
        };
    }

    private void EnsureServices()
    {
        if (_rollService != null) return;

        var bridge = SolanaServiceBridge.Instance;
        if (bridge != null)
            _rollService = bridge.EncounterRollService;

        if (_rollService == null)
        {
            _rollService = new EncounterRollService();
            if (enableDebugLogs)
                Debug.Log("[EncounterTriggerZone] Created standalone EncounterRollService (no bridge).");
        }
    }

    private bool PassesQuestGuard()
    {
        if (!NarrativeQuestGuardUtility.HasConfiguredClauses(questGuard))
            return true;

        if (questService == null)
            questService = FindFirstObjectByType<QuestService>();

        NarrativeQuestGuardResult result = NarrativeQuestGuardUtility.Evaluate(
            questService,
            questGuard,
            emptyGuardPasses: false);
        return result.IsConfigured && result.Passes;
    }

    private void SetActivePayload(ZoneEncounterResult result)
    {
        var bridge = SolanaServiceBridge.Instance;
        if (bridge != null)
        {
            var zoneProvider = bridge.ZoneEncounterProvider;
            if (zoneProvider != null)
            {
                zoneProvider.ActiveResult = result;
                return;
            }
        }

        if (enableDebugLogs)
            Debug.LogWarning("[EncounterTriggerZone] No ZoneEncounterProvider found on SolanaServiceBridge.");
    }
}
