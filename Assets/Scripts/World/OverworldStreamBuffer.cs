using UnityEngine;

/// <summary>
/// Child hysteresis collider for <see cref="OverworldStreamVolume"/>.
/// Size this larger than the inner volume and extend it toward the previous
/// chunk (Loomville). Reverse streaming fires on buffer exit, not inner exit.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Signal Jam/World/Overworld Stream Buffer")]
public sealed class OverworldStreamBuffer : MonoBehaviour
{
    [SerializeField] private OverworldStreamVolume parentVolume;
    [SerializeField] private bool enableDebugLogs;

    private int playerOverlapCount;

    public void Initialize(OverworldStreamVolume parent)
    {
        parentVolume = parent;
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
        parentVolume = GetComponentInParent<OverworldStreamVolume>();
    }

    private void Awake()
    {
        if (parentVolume == null)
            parentVolume = GetComponentInParent<OverworldStreamVolume>();

        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (parentVolume == null || !parentVolume.IsPlayer(other))
            return;

        playerOverlapCount++;
        if (playerOverlapCount != 1)
            return;

        if (enableDebugLogs)
            Debug.Log($"[{nameof(OverworldStreamBuffer)}] Enter '{name}'.");

        parentVolume.NotifyBufferEntered();
    }

    private void OnTriggerExit(Collider other)
    {
        if (parentVolume == null || !parentVolume.IsPlayer(other))
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount != 0)
            return;

        if (enableDebugLogs)
            Debug.Log($"[{nameof(OverworldStreamBuffer)}] Exit '{name}'.");

        parentVolume.NotifyBufferExited();
    }

    private void OnDrawGizmos()
    {
        Collider buffer = GetComponent<Collider>();
        if (buffer is BoxCollider box)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.45f, 0.75f, 0.12f);
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.45f, 0.75f, 0.7f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
