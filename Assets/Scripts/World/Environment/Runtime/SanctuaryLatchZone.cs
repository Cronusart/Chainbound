using System;
using UnityEngine;

/// <summary>
/// Enter-latch volume for sanctuary continue/wipe. Hub updates lastLatch only.
/// Portal updates lastLatch and lastPortal. The portal mesh stays hidden until
/// SanctuarySpawnDirector presents a respawn.
/// Keep portal boxes tight around the structure (authoring default ~14m).
/// Encounter volumes arm wipe by calling <see cref="TryLatchNearestPortal"/> —
/// do not grow this collider to cover grass or fight areas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SanctuaryLatchZone : MonoBehaviour
{
    public const string DefaultPortalUnlockFlag = "world.sanctuary.unlocked";

    [Header("Latch")]
    [SerializeField] private SanctuaryLatchKind kind = SanctuaryLatchKind.Hub;
    [Tooltip("Scene id stored on latch. Empty uses the active scene name at enter.")]
    [SerializeField] private string sceneId = "";
    [Tooltip("Overworld scenes that must be resident on sanctuary continue/wipe. Author every chunk the player can walk onto from this portal (e.g. Jungle + Link1). Not a comma list. Leave empty on hub volumes.")]
    [SerializeField] private string[] residentChunkSceneIds = Array.Empty<string>();
    [SerializeField, HideInInspector] private string overworldChunkSceneId = "";
    [Tooltip("SpawnPoint id the player returns to.")]
    [SerializeField] private string spawnId = "";
    [Tooltip("Optional profile world flag required before this volume can latch or ping. Empty = always armed. Outdoor portals use world.sanctuary.unlocked (or a more specific world.sanctuary.* key) so quests can gate them.")]
    [SerializeField] private string requiredFlagKey = "";

    [Header("Wake Dialogue")]
    [Tooltip("When enabled, sanctuary arrival uses the Dog fallback lines for both continue and party wipe instead of the Sun catalogs.")]
    [SerializeField] private bool useDogWakeFallback;

    [Header("Ping")]
    [Tooltip("One-shot sound on enter. Not the portal mesh.")]
    [SerializeField] private SoundEventSO pingSound;
    [Tooltip("Optional short ring/vfx, separate from the hidden portal mesh.")]
    [SerializeField] private GameObject pingVfx;
    [SerializeField] private float pingVfxDuration = 0.6f;
    [SerializeField] private float pingCooldown = 4f;

    [Header("Portal Visual")]
    [Tooltip("Hidden until wipe/continue presentation. Leave empty for hub volumes.")]
    [SerializeField] private GameObject portalVisual;

    [Header("Player Detection")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs;

    private float lastPingTime = -999f;
    private Coroutine pingVfxRoutine;

    public SanctuaryLatchKind Kind => kind;
    public string SceneId => string.IsNullOrWhiteSpace(sceneId)
        ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        : sceneId.Trim();
    public string[] ResidentChunkSceneIds => NormalizeResidentChunkSceneIds(
        residentChunkSceneIds,
        overworldChunkSceneId,
        kind == SanctuaryLatchKind.Portal ? SceneId : "");
    public string SpawnId => spawnId != null ? spawnId.Trim() : "";
    public GameObject PortalVisual => portalVisual;
    public string RequiredFlagKey => requiredFlagKey != null ? requiredFlagKey.Trim() : "";
    public bool UseDogWakeFallback => useDogWakeFallback;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;

        MigrateLegacyChunkField();
        HidePortalVisual();
        if (pingVfx != null)
            pingVfx.SetActive(false);

        int volumeLayer = LayerMask.NameToLayer("Volume");
        if (volumeLayer >= 0 && gameObject.layer == 0)
            gameObject.layer = volumeLayer;

        int emissiveLayer = LayerMask.NameToLayer("Emissive");
        if (pingVfx != null && emissiveLayer >= 0)
            SetLayerRecursively(pingVfx, emissiveLayer);
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null || layer < 0)
            return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            SetLayerRecursively(t.GetChild(i).gameObject, layer);
    }

    private void OnDisable()
    {
        if (pingVfxRoutine != null)
        {
            StopCoroutine(pingVfxRoutine);
            pingVfxRoutine = null;
        }

        if (pingVfx != null)
            pingVfx.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        TryApplyLatch();
    }

    /// <summary>
    /// Arm the nearest loaded portal from an encounter volume or combat launch.
    /// Uses world distance so grass/fight colliders do not need scene references
    /// to a specific sanctuary, and so streamed chunks pick the local gate.
    /// </summary>
    public static bool TryLatchNearestPortal(Vector3 worldPosition)
    {
        SanctuaryLatchZone nearest = FindNearestPortal(worldPosition);
        if (nearest == null)
            return false;

        return nearest.TryApplyLatch();
    }

    public static SanctuaryLatchZone FindNearestPortal(Vector3 worldPosition)
    {
        SanctuaryLatchZone[] zones = FindObjectsByType<SanctuaryLatchZone>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        SanctuaryLatchZone nearest = null;
        float nearestSq = float.PositiveInfinity;
        for (int i = 0; i < zones.Length; i++)
        {
            SanctuaryLatchZone zone = zones[i];
            if (zone == null || zone.kind != SanctuaryLatchKind.Portal)
                continue;
            if (!zone.CanLatch())
                continue;

            float sq = (zone.transform.position - worldPosition).sqrMagnitude;
            if (sq >= nearestSq)
                continue;

            nearestSq = sq;
            nearest = zone;
        }

        return nearest;
    }

    public bool TryApplyLatch()
    {
        if (EncounterReturnPlacement.SuppressWorldVolumeEnter)
            return false;

        return CommitLatch(playPing: !SanctuarySpawnDirector.ShouldSuppressLatchPing);
    }

    /// <summary>
    /// Writes this volume's authored residency even when arrival presentation
    /// suppresses the enter ping. Wipe/continue otherwise keep a stale snapshot
    /// because the player is already standing inside the trigger.
    /// </summary>
    public bool CommitLatch(bool playPing)
    {
        if (string.IsNullOrWhiteSpace(SpawnId))
        {
            Debug.LogWarning($"[SanctuaryLatchZone] '{name}' is missing spawnId.", this);
            return false;
        }

        if (!IsUnlockFlagSatisfied(RequiredFlagKey, treatEmptyAsDefaultPortalFlag: false))
            return false;

        SanctuaryStateService state = SanctuaryStateService.Instance;
        if (state == null)
            return false;

        string resolvedScene = SceneId;
        string resolvedSpawn = SpawnId;
        bool alreadyLatched = state.MatchesLatch(kind, resolvedScene, resolvedSpawn);
        if (kind == SanctuaryLatchKind.Portal)
        {
            string[] chunks = ResidentChunkSceneIds;
            if (residentChunkSceneIds == null || residentChunkSceneIds.Length == 0)
            {
                Debug.LogWarning(
                    $"[SanctuaryLatchZone] '{name}' is missing residentChunkSceneIds. " +
                    "Assign every overworld chunk the player can walk onto from this portal.",
                    this);
            }

            state.LatchPortal(resolvedScene, resolvedSpawn, RequiredFlagKey, chunks);
        }
        else
            state.LatchHub(resolvedScene, resolvedSpawn);

        if (playPing && !alreadyLatched)
            TryPlayPing();

        return true;
    }

    /// <summary>
    /// After the destination overworld scene is loaded, the live portal's
    /// authored list is source of truth. Saved snapshots can still be Jungle-only
    /// from before Link1 was authored, or from a presentation that skipped latch.
    /// </summary>
    public static string[] ResolvePreferredResidentChunkSceneIds(
        string sceneId,
        string spawnId,
        string[] savedChunkSceneIds)
    {
        SanctuaryLatchZone zone = FindArrivalZone(sceneId, spawnId);
        if (zone != null && zone.Kind == SanctuaryLatchKind.Portal)
        {
            string[] live = zone.ResidentChunkSceneIds;
            if (live != null && live.Length > 0)
                return live;
        }

        return NormalizeResidentChunkSceneIds(savedChunkSceneIds, "", "");
    }

    private bool CanLatch()
    {
        return !string.IsNullOrWhiteSpace(SpawnId) &&
               IsUnlockFlagSatisfied(RequiredFlagKey, treatEmptyAsDefaultPortalFlag: false);
    }

    public void HidePortalVisual()
    {
        if (portalVisual != null)
            portalVisual.SetActive(false);
    }

    public void ShowPortalVisual()
    {
        if (portalVisual != null)
            portalVisual.SetActive(true);
    }

    public static SanctuaryLatchZone FindForSpawn(string sceneId, string spawnId)
    {
        string normalizedScene = sceneId != null ? sceneId.Trim() : "";
        string normalizedSpawn = spawnId != null ? spawnId.Trim() : "";
        if (string.IsNullOrEmpty(normalizedSpawn))
            return null;

        SanctuaryLatchZone[] zones = FindObjectsByType<SanctuaryLatchZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            SanctuaryLatchZone zone = zones[i];
            if (zone == null)
                continue;
            if (!string.Equals(zone.SpawnId, normalizedSpawn, System.StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrEmpty(normalizedScene) &&
                !string.Equals(zone.SceneId, normalizedScene, System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return zone;
        }

        return null;
    }

    /// <summary>
    /// Arrival spawn used by Continue/wipe/restart. Portal spawn ids map to their
    /// latch volume. Hub wipe uses <c>home_bed</c>, which shares the bedroom hub.
    /// Door spawns such as <c>bedroom_from_loomville</c> return null.
    /// </summary>
    public static SanctuaryLatchZone FindArrivalZone(string sceneId, string spawnId)
    {
        SanctuaryLatchZone exact = FindForSpawn(sceneId, spawnId);
        if (exact != null)
            return exact;

        string normalizedScene = sceneId != null ? sceneId.Trim() : "";
        string normalizedSpawn = spawnId != null ? spawnId.Trim() : "";
        if (string.IsNullOrEmpty(normalizedSpawn))
            return null;

        bool isHubWipeBed =
            string.Equals(normalizedSpawn, SanctuaryStateService.DefaultWipeSpawnId, System.StringComparison.Ordinal) &&
            (string.IsNullOrEmpty(normalizedScene) ||
             string.Equals(normalizedScene, SanctuaryStateService.DefaultWipeSceneId, System.StringComparison.OrdinalIgnoreCase));
        if (!isHubWipeBed)
            return null;

        SanctuaryLatchZone[] zones = FindObjectsByType<SanctuaryLatchZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            SanctuaryLatchZone zone = zones[i];
            if (zone == null || zone.kind != SanctuaryLatchKind.Hub)
                continue;
            if (!string.IsNullOrEmpty(normalizedScene) &&
                !string.Equals(zone.SceneId, normalizedScene, System.StringComparison.OrdinalIgnoreCase))
                continue;
            return zone;
        }

        return null;
    }

    /// <summary>
    /// Outdoor portals stay inert until their flag is true. Empty keys stay armed
    /// on scene volumes (hubs). Saved portal destinations with an empty key still
    /// require <see cref="DefaultPortalUnlockFlag"/> so intro contamination cannot
    /// wipe/continue to a gated gate.
    /// </summary>
    public static bool IsUnlockFlagSatisfied(string requiredFlagKey, bool treatEmptyAsDefaultPortalFlag)
    {
        string key = requiredFlagKey != null ? requiredFlagKey.Trim() : "";
        if (string.IsNullOrEmpty(key))
        {
            if (!treatEmptyAsDefaultPortalFlag)
                return true;
            key = DefaultPortalUnlockFlag;
        }

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance;
        if (flags == null || !flags.IsHydrated)
            return false;
        return flags.GetFlag(key);
    }

    private void TryPlayPing()
    {
        if (Time.time - lastPingTime < pingCooldown)
            return;

        lastPingTime = Time.time;

        if (pingSound != null)
        {
            AudioService audioService = AudioService.Instance;
            if (audioService != null)
                audioService.Play(pingSound, transform.position);
        }

        if (pingVfx == null)
            return;

        pingVfx.SetActive(true);
        if (pingVfxRoutine != null)
            StopCoroutine(pingVfxRoutine);
        pingVfxRoutine = StartCoroutine(HidePingVfxAfterDelay());
    }

    private System.Collections.IEnumerator HidePingVfxAfterDelay()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.05f, pingVfxDuration));
        if (pingVfx != null)
            pingVfx.SetActive(false);
        pingVfxRoutine = null;
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null)
            return false;
        if (col.CompareTag(playerTag))
            return true;

        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(playerTag))
                return true;
            parent = parent.parent;
        }

        return false;
    }

    internal static string[] NormalizeResidentChunkSceneIds(
        string[] authored,
        string legacySingleId,
        string portalSceneFallback)
    {
        var ids = new System.Collections.Generic.List<string>();
        AddUniqueChunkIds(ids, authored);
        AddUniqueChunkId(ids, legacySingleId);
        if (ids.Count == 0)
            AddUniqueChunkId(ids, portalSceneFallback);
        return ids.ToArray();
    }

    private void MigrateLegacyChunkField()
    {
        if (residentChunkSceneIds != null && residentChunkSceneIds.Length > 0)
            return;
        if (string.IsNullOrWhiteSpace(overworldChunkSceneId))
            return;

        residentChunkSceneIds = new[] { overworldChunkSceneId.Trim() };
        overworldChunkSceneId = "";
    }

    private static void AddUniqueChunkIds(
        System.Collections.Generic.List<string> destination,
        string[] source)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Length; i++)
            AddUniqueChunkId(destination, source[i]);
    }

    private static void AddUniqueChunkId(
        System.Collections.Generic.List<string> destination,
        string sceneId)
    {
        string normalized = sceneId != null ? sceneId.Trim() : "";
        if (string.IsNullOrEmpty(normalized))
            return;
        for (int i = 0; i < destination.Count; i++)
        {
            if (string.Equals(destination[i], normalized, StringComparison.OrdinalIgnoreCase))
                return;
        }

        destination.Add(normalized);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        MigrateLegacyChunkField();
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col is BoxCollider box)
        {
            Gizmos.color = kind == SanctuaryLatchKind.Portal
                ? new Color(0.4f, 0.85f, 1f, 0.28f)
                : new Color(0.95f, 0.75f, 0.2f, 0.28f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = kind == SanctuaryLatchKind.Portal
                ? new Color(0.4f, 0.85f, 1f, 0.85f)
                : new Color(0.95f, 0.75f, 0.2f, 0.85f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
#endif
}
