using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Writes a per-profile world flag. Scene objects listen via
/// <see cref="WorldPresenceBinding"/>, <see cref="ProfileWorldFlagObjectGate"/>,
/// or the name catalog. Unique pickups use the same step plus an item grant.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_SetProfileWorldFlag",
    menuName = "Chainbound/Narrative/Steps/Set Profile World Flag")]
public class Step_SetProfileWorldFlag : SequenceStep
{
    [Header("Flag")]
    [Tooltip("Stable key, e.g. intro.guardrail.house or world.pickup.loom_lab.starter_orb.{Starter}.")]
    [SerializeField] private string flagKey = "";

    [SerializeField] private bool flagValue = true;

    [Tooltip("Unspecified uses the world-presence prefix board.")]
    [SerializeField] private WorldPresenceLifetime lifetimeOverride = WorldPresenceLifetime.Unspecified;

    public string FlagKey => flagKey != null ? flagKey.Trim() : "";
    public bool FlagValue => flagValue;
    public WorldPresenceLifetime LifetimeOverride => lifetimeOverride;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        string starter = context != null ? context.StarterSelected : "";
        starter = starter != null ? starter.Trim() : "";
        if (FlagKey.IndexOf("{Starter}", StringComparison.Ordinal) >= 0 &&
            string.IsNullOrWhiteSpace(starter))
        {
            Debug.LogError("[Step_SetProfileWorldFlag] {Starter} could not be resolved.");
            onComplete?.Invoke(StepOutcome.Failed("Starter species id was empty"));
            yield break;
        }

        string resolvedKey = ResolveFlagKey(context);
        if (string.IsNullOrWhiteSpace(resolvedKey))
        {
            Debug.LogError("[Step_SetProfileWorldFlag] Flag key is empty!");
            onComplete?.Invoke(StepOutcome.Failed("Flag key is empty"));
            yield break;
        }

        ProfileWorldFlagStateService service = ProfileWorldFlagStateService.Instance;
        if (service == null)
        {
            Debug.LogError("[Step_SetProfileWorldFlag] ProfileWorldFlagStateService not found!");
            onComplete?.Invoke(StepOutcome.Failed("ProfileWorldFlagStateService not found"));
            yield break;
        }

        service.SetFlag(resolvedKey, flagValue, lifetimeOverride);
        if (enableDebugLogs)
            Debug.Log($"[Step_SetProfileWorldFlag] {resolvedKey} = {flagValue}");

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public string ResolveFlagKey(SequenceContext context)
    {
        string resolved = FlagKey;
        if (string.IsNullOrWhiteSpace(resolved))
            return "";

        string starter = context != null ? context.StarterSelected : "";
        starter = starter != null ? starter.Trim().ToLowerInvariant() : "";
        return resolved.Replace("{Starter}", starter);
    }

    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(FlagKey))
        {
            Debug.LogWarning("[Step_SetProfileWorldFlag] Flag key is empty!");
            return false;
        }

        return true;
    }
}
