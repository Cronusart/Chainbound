using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable compound trigger volume that requests a soundtrack environment.
/// Occupancy is resampled after exits and scene placement so teleports and
/// overlapping child colliders do not leave stale audio state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MusicEnvironmentVolume : MonoBehaviour
{
    [SerializeField] private MusicEnvironmentProfileSO profile;
    [SerializeField] private string playerTag = "Player";
    [SerializeField, Min(0f)] private float exitHysteresisSeconds = 0.08f;
    [SerializeField] private bool enableDebugLogs;

    private readonly List<Collider> volumeColliders = new List<Collider>();
    private MusicEnvironmentToken token;
    private Coroutine exitRoutine;

    public MusicEnvironmentProfileSO Profile => profile;
    public bool IsOccupied => token.IsValid;

    private void Awake()
    {
        RefreshColliders(bindRelays: true);
    }

    private void OnEnable()
    {
        RefreshColliders(bindRelays: true);
        StartCoroutine(ResyncAfterPlacement());
    }

    private void OnDisable()
    {
        if (exitRoutine != null)
            StopCoroutine(exitRoutine);
        exitRoutine = null;
        ReleaseEnvironment();
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        NotifyPlayerExit(other);
    }

    public void NotifyPlayerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (exitRoutine != null)
        {
            StopCoroutine(exitRoutine);
            exitRoutine = null;
        }

        AcquireEnvironment();
    }

    public void NotifyPlayerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (exitRoutine != null)
            StopCoroutine(exitRoutine);
        exitRoutine = StartCoroutine(ConfirmExit());
    }

    public void ResyncOccupancy()
    {
        Physics.SyncTransforms();
        if (ContainsPlayer())
            AcquireEnvironment();
        else
            ReleaseEnvironment();
    }

    private IEnumerator ResyncAfterPlacement()
    {
        yield return null;
        yield return new WaitForFixedUpdate();
        ResyncOccupancy();
    }

    private IEnumerator ConfirmExit()
    {
        if (exitHysteresisSeconds > 0f)
            yield return new WaitForSecondsRealtime(exitHysteresisSeconds);
        else
            yield return new WaitForFixedUpdate();

        exitRoutine = null;
        ResyncOccupancy();
    }

    private void AcquireEnvironment()
    {
        if (token.IsValid || profile == null)
            return;

        MusicConductor conductor = MusicConductor.Instance;
        if (conductor == null)
            return;

        token = conductor.AcquireEnvironment(profile, gameObject.name);
        if (enableDebugLogs)
            Debug.Log($"[MusicEnvironmentVolume] Entered '{name}' -> '{profile.ProfileId}'.");
    }

    private void ReleaseEnvironment()
    {
        if (!token.IsValid)
            return;

        token.Release();
        token = default;
        if (enableDebugLogs)
            Debug.Log($"[MusicEnvironmentVolume] Exited '{name}'.");
    }

    private bool ContainsPlayer()
    {
        GameObject player = PlayerRootResolver.ResolveActivePlayer(playerTag);
        if (player == null)
            return false;

        RefreshColliders(bindRelays: false);
        Collider[] playerColliders = player.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < volumeColliders.Count; i++)
        {
            Collider volume = volumeColliders[i];
            if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy)
                continue;

            if (ContainsPoint(volume, player.transform.position))
                return true;

            Bounds bounds = volume.bounds;
            for (int j = 0; j < playerColliders.Length; j++)
            {
                Collider playerCollider = playerColliders[j];
                if (playerCollider != null && !playerCollider.isTrigger && playerCollider.enabled &&
                    bounds.Intersects(playerCollider.bounds))
                    return true;
            }
        }

        return false;
    }

    private bool IsPlayerCollider(Collider collider)
    {
        if (collider == null)
            return false;

        Transform current = collider.transform;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return true;
            current = current.parent;
        }

        return false;
    }

    private static bool ContainsPoint(Collider collider, Vector3 point)
    {
        if (collider is BoxCollider box)
        {
            Vector3 local = box.transform.InverseTransformPoint(point) - box.center;
            Vector3 half = box.size * 0.5f;
            return Mathf.Abs(local.x) <= Mathf.Abs(half.x) + 0.0001f &&
                   Mathf.Abs(local.y) <= Mathf.Abs(half.y) + 0.0001f &&
                   Mathf.Abs(local.z) <= Mathf.Abs(half.z) + 0.0001f;
        }

        return (collider.ClosestPoint(point) - point).sqrMagnitude <= 0.0001f;
    }

    private void RefreshColliders(bool bindRelays)
    {
        volumeColliders.Clear();
        GetComponents(volumeColliders);
        Collider[] descendants = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Collider collider = descendants[i];
            if (collider == null || volumeColliders.Contains(collider))
                continue;
            if (collider.GetComponentInParent<MusicEnvironmentVolume>() != this)
                continue;
            volumeColliders.Add(collider);
        }

        for (int i = 0; i < volumeColliders.Count; i++)
        {
            Collider collider = volumeColliders[i];
            if (collider == null)
                continue;
            collider.isTrigger = true;
            if (!bindRelays || collider.gameObject == gameObject)
                continue;
            MusicEnvironmentVolumeRelay relay = collider.GetComponent<MusicEnvironmentVolumeRelay>();
            if (relay == null)
                relay = collider.gameObject.AddComponent<MusicEnvironmentVolumeRelay>();
            relay.Bind(this);
        }
    }
}
