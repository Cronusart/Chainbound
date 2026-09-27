using System;
using UnityEngine;

/// <summary>
/// Shared availability tuning for interactables after they fire.
/// Keeps interaction behavior simple to author while still allowing per-instance overrides.
/// </summary>
[Serializable]
public class InteractableAvailabilitySettings
{
    [Tooltip("How long to wait for downstream dialogue to claim this interaction before treating it as a no-dialogue flow.")]
    [Min(0f)]
    public float downstreamClaimGraceSeconds = 0.35f;

    [Tooltip("Short unscaled cooldown after the owned presentation window ends before this interactable can fire again.")]
    [Min(0f)]
    public float reinteractCooldownSeconds = 0.15f;

    [Tooltip("When enabled, the interact prompt stays hidden until the interactable is fully available again.")]
    public bool keepIndicatorHiddenUntilAvailable = true;

    public float DownstreamClaimGraceSeconds => Mathf.Max(0f, downstreamClaimGraceSeconds);
    public float ReinteractCooldownSeconds => Mathf.Max(0f, reinteractCooldownSeconds);
}
