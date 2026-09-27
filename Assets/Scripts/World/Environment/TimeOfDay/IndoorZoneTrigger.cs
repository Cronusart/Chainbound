using UnityEngine;

/// <summary>
/// Trigger component that marks indoor zones.
/// When the player is physically inside, switches TimeOfDayVisualApplier to indoor
/// mode and binds the scene's IndoorDirectionalLight / indoor visual profile.
/// Extra Player-tagged colliders cannot flip outdoor lighting while the root
/// is still inside the volume.
///
/// Setup:
/// 1. Add Collider component (set isTrigger = true)
/// 2. Attach this component
/// 3. Assign TimeOfDayVisualApplier reference (or auto-finds)
/// 4. Set player tag if different from "Player"
/// </summary>
[RequireComponent(typeof(Collider))]
public class IndoorZoneTrigger : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Visual applier override (optional). Leave empty to use static ActiveInstance. " +
             "Recommended: Leave empty for automatic resolution.")]
    [SerializeField] private TimeOfDayVisualApplier visualApplierOverride;

    [Header("Settings")]
    [Tooltip("Tag of player GameObject")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("Allow nested zones (uses reference counting)")]
    [SerializeField] private bool allowNestedZones = false;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private Collider zoneCollider;
    private bool playerInside;
    private int playerCount;
    private TimeOfDayVisualApplier resolvedApplier;

    private void Awake()
    {
        zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        ResolveVisualApplier();
    }

    private void OnEnable()
    {
        if (resolvedApplier == null)
            ResolveVisualApplier();

        RefreshIndoorState();
    }

    private void OnDisable()
    {
        if (playerInside)
            ApplyIndoorState(false);
        playerInside = false;
        playerCount = 0;
    }

    private void ResolveVisualApplier()
    {
        resolvedApplier = TimeOfDayVisualApplier.ActiveInstance;

        if (resolvedApplier != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[IndoorZoneTrigger] {gameObject.name} using ActiveInstance: {resolvedApplier.name}");
            return;
        }

        if (visualApplierOverride != null)
        {
            resolvedApplier = visualApplierOverride;
            if (enableDebugLogs)
                Debug.Log($"[IndoorZoneTrigger] {gameObject.name} using serialized override: {resolvedApplier.name}");
            return;
        }

        resolvedApplier = FindFirstObjectByType<TimeOfDayVisualApplier>();

        if (resolvedApplier != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[IndoorZoneTrigger] {gameObject.name} found via FindFirstObjectByType: {resolvedApplier.name}");
        }
        else
        {
            Debug.LogWarning($"[IndoorZoneTrigger] {gameObject.name}: No TimeOfDayVisualApplier found! Indoor zone will not function.");
        }
    }

    private TimeOfDayVisualApplier GetVisualApplier()
    {
        if (resolvedApplier == null)
            ResolveVisualApplier();

        return resolvedApplier;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (allowNestedZones)
        {
            playerCount++;
            if (playerCount == 1)
                ApplyIndoorState(true);
            return;
        }

        RefreshIndoorState();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        if (allowNestedZones)
        {
            playerCount--;
            if (playerCount <= 0)
            {
                playerCount = 0;
                ApplyIndoorState(false);
            }
            return;
        }

        RefreshIndoorState();
    }

    private void RefreshIndoorState()
    {
        bool inside = IsPlayerPhysicallyInside();
        if (inside == playerInside)
            return;

        ApplyIndoorState(inside);
    }

    private bool IsPlayerPhysicallyInside()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<Collider>();
        if (zoneCollider == null)
            return false;

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
            return false;

        Bounds zoneBounds = zoneCollider.bounds;
        if (zoneBounds.Contains(player.transform.position))
            return true;

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider playerCol = playerColliders[i];
            if (playerCol == null || playerCol.isTrigger)
                continue;
            if (zoneBounds.Intersects(playerCol.bounds))
                return true;
        }

        return false;
    }

    private bool IsPlayer(Collider col)
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

    private void ApplyIndoorState(bool indoors)
    {
        playerInside = indoors;

        var applier = GetVisualApplier();
        if (applier == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[IndoorZoneTrigger] {gameObject.name}: Cannot set indoors - no visual applier found!");
            return;
        }

        if (indoors)
        {
            IndoorDirectionalLight indoorLight = IndoorDirectionalLight.FindForScene(gameObject.scene);
            if (indoorLight != null)
                applier.ConfigureIndoorLight(indoorLight);
        }

        applier.SetIndoors(indoors);

        if (enableDebugLogs)
            Debug.Log($"[IndoorZoneTrigger] {gameObject.name}: SetIndoors({indoors}) on {applier.name}");
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
            return;

        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            Vector3 center = capsule.center;
            Gizmos.DrawSphere(center + Vector3.up * (capsule.height * 0.5f - capsule.radius), capsule.radius);
            Gizmos.DrawSphere(center - Vector3.up * (capsule.height * 0.5f - capsule.radius), capsule.radius);
        }
    }
}
