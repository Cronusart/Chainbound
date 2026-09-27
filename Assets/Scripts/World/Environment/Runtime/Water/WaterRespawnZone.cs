using System.Collections;
using UnityEngine;

/// <summary>
/// Trigger zone placed below water surface. When player falls in, respawns them to the
/// nearest spawn point (e.g. harbor points) and plays a splash sound.
///
/// After teleport, player motion and input stay locked while the visual mesh blinks,
/// then control is restored.
///
/// Setup:
/// 1. Add BoxCollider (trigger) below water surface covering the water area
/// 2. Set spawnIdPrefix (e.g. "harbor_") to filter to water-respawn spawn points
/// 3. Place SpawnPoint objects along the harbor with matching prefix
/// 4. Assign splash SoundEventSO (configure OutputBus in asset for audiomixer routing)
/// </summary>
[RequireComponent(typeof(Collider))]
public class WaterRespawnZone : MonoBehaviour
{
    [Header("Respawn Settings")]
    [Tooltip("Only consider spawn points whose ID starts with this prefix (e.g. 'harbor_'). Leave empty for all spawn points.")]
    [SerializeField] private string spawnIdPrefix = "harbor_";

    [Tooltip("Cooldown between respawns (seconds) to prevent rapid loops")]
    [SerializeField] private float respawnCooldown = 1.5f;

    [Header("Post-Respawn Hold")]
    [Tooltip("How long the player and input stay frozen after teleport.")]
    [SerializeField] private float freezeDuration = 2f;

    [Tooltip("How many times the player visual mesh blinks off then on during the freeze.")]
    [SerializeField] private int flashCount = 3;

    [Header("Audio")]
    [Tooltip("Splash sound when player falls in. Configure OutputBus in the SoundEventSO for audiomixer routing.")]
    [SerializeField] private SoundEventSO splashSound;

    [Header("Player Detection")]
    [Tooltip("Tag to detect (usually 'Player')")]
    [SerializeField] private string playerTag = "Player";

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private float lastRespawnTime = -999f;
    private Coroutine holdRoutine;
    private InputLockHandle inputLockHandle;
    private bool hasInputLock;
    private PlayerMovement3D frozenMovement;
    private bool appliedFreeze;
    private MeshRendererPlayerVisual flashedVisual;
    private MeshRenderer flashedRenderer;
    private static WaterRespawnZone activeHold;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
        }
    }

    private void OnDisable()
    {
        RestoreHoldState();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other)) return;

        if (activeHold != null)
        {
            if (enableDebugLogs)
                Debug.Log($"[WaterRespawnZone] Hold already active, ignoring trigger on {gameObject.name}");
            return;
        }

        float holdWindow = Mathf.Max(respawnCooldown, freezeDuration);
        if (Time.time - lastRespawnTime < holdWindow)
        {
            if (enableDebugLogs)
                Debug.Log($"[WaterRespawnZone] Cooldown active, ignoring trigger on {gameObject.name}");
            return;
        }

        RespawnPlayer(other.transform.position, ResolvePlayerRoot(other.transform));
    }

    private void RespawnPlayer(Vector3 fallPosition, Transform playerRoot)
    {
        SpawnService spawnService = SpawnService.Instance;
        if (spawnService == null)
        {
            Debug.LogError("[WaterRespawnZone] SpawnService not found!");
            return;
        }

        SpawnPoint nearest = spawnService.GetNearestSpawnPoint(
            fallPosition,
            string.IsNullOrEmpty(spawnIdPrefix) ? null : spawnIdPrefix);

        if (nearest == null)
        {
            Debug.LogWarning($"[WaterRespawnZone] No spawn point found (prefix='{spawnIdPrefix}'). Player not respawned.");
            return;
        }

        if (splashSound != null)
        {
            AudioService audioService = AudioService.Instance;
            if (audioService != null)
            {
                audioService.Play(splashSound, fallPosition);
            }
        }

        bool success = spawnService.SpawnPlayerAt(nearest.SpawnId);
        if (!success)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[WaterRespawnZone] SpawnPlayerAt failed for '{nearest.SpawnId}'");
            return;
        }

        lastRespawnTime = Time.time;
        if (enableDebugLogs)
            Debug.Log($"[WaterRespawnZone] Respawned player to '{nearest.SpawnId}' (nearest to fall position)");

        RestoreHoldState();
        holdRoutine = StartCoroutine(HoldAndFlashAfterRespawn(playerRoot));
    }

    private IEnumerator HoldAndFlashAfterRespawn(Transform playerRoot)
    {
        activeHold = this;

        PlayerMovement3D movement = ResolveMovement(playerRoot);
        flashedVisual = ResolvePlayerVisualComponent(playerRoot);
        flashedRenderer = flashedVisual != null
            ? flashedVisual.GetComponent<MeshRenderer>()
            : ResolvePlayerVisual(playerRoot);

        InputLockService lockService = InputLockService.Instance;
        if (lockService != null)
        {
            inputLockHandle = lockService.AcquireLock("WaterRespawn", InputLockChannel.All, this);
            hasInputLock = inputLockHandle.IsValid;
        }

        if (movement != null)
        {
            frozenMovement = movement;
            movement.SetFrozen(true, preservePhysics: false);
            appliedFreeze = true;
        }

        try
        {
            int blinks = Mathf.Max(0, flashCount);
            float holdSeconds = Mathf.Max(0f, freezeDuration);
            float slice = blinks > 0 ? holdSeconds / (blinks * 2f) : holdSeconds;

            if (blinks == 0)
            {
                if (holdSeconds > 0f)
                    yield return new WaitForSecondsRealtime(holdSeconds);
            }
            else
            {
                for (int i = 0; i < blinks; i++)
                {
                    SetVisualVisible(false);
                    if (slice > 0f)
                        yield return new WaitForSecondsRealtime(slice);
                    SetVisualVisible(true);
                    if (slice > 0f)
                        yield return new WaitForSecondsRealtime(slice);
                }
            }
        }
        finally
        {
            ReleaseHoldEffects();
            holdRoutine = null;
        }
    }

    private void RestoreHoldState()
    {
        if (holdRoutine != null)
        {
            StopCoroutine(holdRoutine);
            holdRoutine = null;
        }

        ReleaseHoldEffects();
    }

    private void ReleaseHoldEffects()
    {
        ForceVisualVisible();
        flashedVisual = null;
        flashedRenderer = null;

        if (appliedFreeze && frozenMovement != null)
        {
            frozenMovement.SetFrozen(false, preservePhysics: false);
            appliedFreeze = false;
        }

        frozenMovement = null;

        if (hasInputLock)
        {
            inputLockHandle.Release();
            hasInputLock = false;
        }

        if (activeHold == this)
            activeHold = null;
    }

    private void SetVisualVisible(bool visible)
    {
        if (flashedVisual != null)
        {
            flashedVisual.SetTemporarilyHidden(!visible);
            return;
        }

        if (flashedRenderer != null)
            flashedRenderer.enabled = visible;
    }

    private void ForceVisualVisible()
    {
        if (flashedVisual != null)
        {
            flashedVisual.ForceVisible();
            return;
        }

        if (flashedRenderer != null)
            flashedRenderer.enabled = true;
    }

    private PlayerMovement3D ResolveMovement(Transform playerRoot)
    {
        if (playerRoot == null)
            return FindFirstObjectByType<PlayerMovement3D>();

        PlayerMovement3D movement = playerRoot.GetComponent<PlayerMovement3D>();
        if (movement == null)
            movement = playerRoot.GetComponentInParent<PlayerMovement3D>();
        if (movement == null)
            movement = playerRoot.GetComponentInChildren<PlayerMovement3D>(true);
        return movement;
    }

    private static MeshRendererPlayerVisual ResolvePlayerVisualComponent(Transform playerRoot)
    {
        if (playerRoot == null)
            return FindFirstObjectByType<MeshRendererPlayerVisual>();

        MeshRendererPlayerVisual visual = playerRoot.GetComponent<MeshRendererPlayerVisual>();
        if (visual == null)
            visual = playerRoot.GetComponentInParent<MeshRendererPlayerVisual>();
        if (visual == null)
            visual = playerRoot.GetComponentInChildren<MeshRendererPlayerVisual>(true);
        return visual;
    }

    private static MeshRenderer ResolvePlayerVisual(Transform playerRoot)
    {
        if (playerRoot == null)
            return null;

        MeshRendererPlayerVisual visual = playerRoot.GetComponentInChildren<MeshRendererPlayerVisual>(true);
        if (visual != null)
            return visual.GetComponent<MeshRenderer>();

        return playerRoot.GetComponentInChildren<MeshRenderer>(true);
    }

    private Transform ResolvePlayerRoot(Transform candidate)
    {
        Transform current = candidate;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return current;
            current = current.parent;
        }
        return candidate;
    }

    private bool IsPlayerCollider(Collider col)
    {
        if (col == null) return false;
        if (col.CompareTag(playerTag)) return true;

        Transform parent = col.transform.parent;
        while (parent != null)
        {
            if (parent.CompareTag(playerTag)) return true;
            parent = parent.parent;
        }
        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
            if (col is BoxCollider box)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0f, 0.5f, 1f, 0.8f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
    }
#endif
}
