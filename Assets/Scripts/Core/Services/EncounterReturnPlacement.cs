using System.Collections;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Applies post-battle encounter return placement (spawn id or snapshot teleport).
/// Lives on a DontDestroyOnLoad object so <see cref="SceneLoader"/> <c>onBeforeReveal</c> can start coroutines
/// after the battle scene (and scene-only handlers) unload.
/// </summary>
public sealed class EncounterReturnPlacement : MonoBehaviour
{
    private static EncounterReturnPlacement instance;
    private static int suppressWorldVolumeEnterUntilFrame = -1;

    /// <summary>
    /// True for a few frames after an encounter teleport so world enter volumes
    /// (sanctuary latches, etc.) do not treat spawn-in-overlap as a real enter.
    /// </summary>
    public static bool SuppressWorldVolumeEnter =>
        Time.frameCount <= suppressWorldVolumeEnterUntilFrame;

    public static EncounterReturnPlacement Instance
    {
        get
        {
            EnsureCreated();
            return instance;
        }
    }

    /// <summary>Ensures a persistent placement host exists before starting an async scene load that uses <c>onBeforeReveal</c>.</summary>
    public static void EnsureCreated()
    {
        if (instance != null)
            return;

        instance = FindObjectOfType<EncounterReturnPlacement>(true);
        if (instance != null)
            return;

        GameServicesContainer servicesContainer = GameServicesContainer.Instance != null
            ? GameServicesContainer.Instance
            : FindObjectOfType<GameServicesContainer>(true);

        GameObject go = new GameObject(nameof(EncounterReturnPlacement));
        if (servicesContainer != null)
            go.transform.SetParent(servicesContainer.transform, false);

        instance = go.AddComponent<EncounterReturnPlacement>();

        if (servicesContainer == null)
            DontDestroyOnLoad(go);
    }

    [Header("Snapshot placement safety")]
    [Tooltip("When not using returnSpawnId, raycast down from this height above the snapshot to snap feet to ground.")]
    [SerializeField] private float groundSnapRaycastUp = 4f;

    [Tooltip("Max raycast distance below the origin.")]
    [SerializeField] private float groundSnapRaycastMaxDown = 120f;

    [Tooltip("Place the root this far above the hit point after a successful ground snap.")]
    [SerializeField] private float groundSnapSkinOffset = 0.08f;

    [Tooltip("If no ground hit, add this to snapshot Y as a small bump.")]
    [SerializeField] private float snapshotFallbackYOffset = 0.2f;

    [SerializeField] private LayerMask groundSnapLayers = ~0;

    [SerializeField] private bool enablePlacementDebugLogs = false;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning($"[{nameof(EncounterReturnPlacement)}] Duplicate instance. Destroying {gameObject.name}.");
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void ArmWorldVolumeEnterSuppress(int extraFrames = 8)
    {
        suppressWorldVolumeEnterUntilFrame = Time.frameCount + Mathf.Max(1, extraFrames);
        _ = Instance;
    }

    /// <summary>
    /// Teleport the player to the exact snapshot position with ground snap + one-frame physics hold.
    /// The scene-authored <c>encounter_return</c> spawn point reverts to its default transform on reload,
    /// so we always use the snapshot position stored in PlayerPrefs rather than <c>SpawnPlayerAt</c>.
    /// </summary>
    public void ApplyEncounterReturnPlacement(OverworldReturnInfo info)
    {
        SpawnService spawnService = SpawnService.Instance;
        GameObject player = spawnService != null ? spawnService.GetResolvedPlayerRoot() : null;
        if (player == null)
            player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            Debug.LogWarning($"[{nameof(EncounterReturnPlacement)}] No player for encounter return placement.");
            return;
        }

        if (enablePlacementDebugLogs)
        {
            Debug.Log($"[{nameof(EncounterReturnPlacement)}] Using snapshot teleport to ({info.position.x:F2}, {info.position.y:F2}, {info.position.z:F2}). " +
                      $"returnSpawnId='{info.returnSpawnId}' (not used for placement — spawn point resets on scene reload).");
        }

        ArmWorldVolumeEnterSuppress();
        TeleportPlayerSnapshotSafe(player, info.position, info.rotation);

        // Clear leftover lead-in overlay weight on the persistent bridge.
        EncounterTransitionBridge bridge = EncounterTransitionBridge.Instance;
        if (bridge != null)
            bridge.ResetChromaticAberration();
    }

    private void TeleportPlayerSnapshotSafe(GameObject player, Vector3 snapshotPosition, Quaternion rotation)
    {
        Rigidbody rb = player.GetComponent<Rigidbody>();
        CharacterController cc = player.GetComponent<CharacterController>();

        bool hadCc = cc != null && cc.enabled;
        if (cc != null)
            cc.enabled = false;

        bool restoreKinematic = rb != null && rb.isKinematic;
        bool restoreGravity = rb != null && rb.useGravity;
        if (rb != null)
        {
            // Zero velocity only while dynamic; kinematic bodies reject velocity writes (Unity 6+).
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Vector3 target = snapshotPosition;
        if (TryGroundSnap(snapshotPosition, out Vector3 snapped))
        {
            target = snapped;
            if (enablePlacementDebugLogs)
                Debug.Log($"[{nameof(EncounterReturnPlacement)}] Ground snap applied: {snapshotPosition} -> {target}");
        }
        else
        {
            target = snapshotPosition + Vector3.up * snapshotFallbackYOffset;
            if (enablePlacementDebugLogs)
                Debug.Log($"[{nameof(EncounterReturnPlacement)}] No ground hit; using snapshot + fallback Y offset: {target}");
        }

        player.transform.SetPositionAndRotation(target, rotation);
        Physics.SyncTransforms();

        SpawnService.Instance?.ClearStoredSpawnId();

        if (enablePlacementDebugLogs)
            Debug.Log($"[{nameof(EncounterReturnPlacement)}] Snapshot teleport (safe) to {target}.");

        StartCoroutine(RestorePhysicsAfterPlacementCoroutine(player, hadCc, restoreKinematic, restoreGravity));
    }

    private bool TryGroundSnap(Vector3 snapshotPosition, out Vector3 worldPosition)
    {
        worldPosition = snapshotPosition;
        Vector3 origin = snapshotPosition + Vector3.up * groundSnapRaycastUp;
        if (Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                groundSnapRaycastUp + groundSnapRaycastMaxDown,
                groundSnapLayers,
                QueryTriggerInteraction.Ignore))
        {
            worldPosition = hit.point + Vector3.up * groundSnapSkinOffset;
            return true;
        }

        return false;
    }

    private IEnumerator RestorePhysicsAfterPlacementCoroutine(
        GameObject player,
        bool restoreCcState = true,
        bool restoreKinematic = false,
        bool restoreUseGravity = true)
    {
        yield return null;

        if (player == null)
            yield break;

        Rigidbody rb = player.GetComponent<Rigidbody>();
        CharacterController cc = player.GetComponent<CharacterController>();

        if (rb != null)
        {
            rb.isKinematic = restoreKinematic;
            rb.useGravity = restoreUseGravity;
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        if (cc != null && restoreCcState)
            cc.enabled = true;
    }
}
