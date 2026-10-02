using UnityEngine;

/// <summary>
/// Forwards trigger occupancy from a child volume collider to the owning
/// <see cref="CameraZoneTrigger"/> or <see cref="CameraZoneBuffer"/>.
/// Extra boxes can live on child objects without becoming a second camera zone.
/// </summary>
[DisallowMultipleComponent]
public sealed class CameraZoneVolumeRelay : MonoBehaviour
{
    [SerializeField] private CameraZoneTrigger trigger;
    [SerializeField] private CameraZoneBuffer buffer;

    public void Bind(MonoBehaviour owner)
    {
        trigger = owner as CameraZoneTrigger;
        buffer = owner as CameraZoneBuffer;
    }

    private void Awake()
    {
        ResolveOwners();
    }

    private void OnValidate()
    {
        ResolveOwners();
    }

    private void ResolveOwners()
    {
        // Buffer pieces are children of the trigger, so a parent search hits the
        // trigger too. A relay anywhere under a buffer is buffer occupancy.
        // An inner compound piece is not under the buffer, so it stays on the trigger.
        CameraZoneBuffer owningBuffer = GetComponent<CameraZoneBuffer>();
        if (owningBuffer == null)
            owningBuffer = GetComponentInParent<CameraZoneBuffer>();
        if (owningBuffer != null)
        {
            buffer = owningBuffer;
            trigger = null;
            return;
        }

        buffer = null;
        if (trigger == null)
            trigger = GetComponentInParent<CameraZoneTrigger>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (buffer != null)
            buffer.NotifyPlayerEnter(other);
        else
            trigger?.NotifyPlayerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (buffer != null)
            buffer.NotifyPlayerExit(other);
        else
            trigger?.NotifyPlayerExit(other);
    }
}
