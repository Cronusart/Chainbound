using UnityEngine;

/// <summary>
/// Scene-authored climate/fog overlay for one streamed region. It delegates to
/// the persistent TimeOfDayVisualApplier and never owns a separate TOD clock.
/// </summary>
[RequireComponent(typeof(Collider))]
public sealed class TimeOfDayRegionVolume : MonoBehaviour
{
    [SerializeField, Min(0f)] private float fogDensityMultiplier = 1f;
    [SerializeField] private Color fogTint = new Color(1f, 1f, 1f, 0f);
    [SerializeField] private int priority;
    [SerializeField] private string playerTag = "Player";

    private int playerOverlapCount;

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
            trigger.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerOverlapCount++;
        if (playerOverlapCount == 1)
            TimeOfDayVisualApplier.ActiveInstance?.SetRegionFogOverlay(this, fogDensityMultiplier, fogTint, priority);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other))
            return;

        playerOverlapCount = Mathf.Max(0, playerOverlapCount - 1);
        if (playerOverlapCount == 0)
            TimeOfDayVisualApplier.ActiveInstance?.ClearRegionFogOverlay(this);
    }

    private void OnDisable()
    {
        TimeOfDayVisualApplier.ActiveInstance?.ClearRegionFogOverlay(this);
        playerOverlapCount = 0;
    }

    private bool IsPlayer(Component candidate)
    {
        Transform current = candidate != null ? candidate.transform : null;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
                return true;
            current = current.parent;
        }

        return false;
    }
}
