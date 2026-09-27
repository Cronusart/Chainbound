using System;
using UnityEngine;

/// <summary>
/// Applies an authored active/inactive mapping once the flag exists.
/// Does not hide the target while flags are still hydrating. Place this
/// component on a separate host when the target GameObject itself may be disabled.
/// Prefer <see cref="WorldPresenceBinding"/> for new overworld content.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProfileWorldFlagObjectGate : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private string flagKey = "";
    [SerializeField] private bool expectedFlagValue = true;
    [SerializeField] private bool activeWhenMatched;

    private ProfileWorldFlagStateService stateService;
    private bool defaultActive;
    private bool hasCapturedDefault;

    private void Awake()
    {
        CaptureDefault();
        stateService = ProfileWorldFlagStateService.Instance;
        Subscribe();
        Apply();
    }

    private void OnEnable()
    {
        if (stateService == null)
            stateService = ProfileWorldFlagStateService.Instance;
        CaptureDefault();
        Subscribe();
        Apply();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void CaptureDefault()
    {
        if (hasCapturedDefault || target == null)
            return;

        defaultActive = target.activeSelf;
        hasCapturedDefault = true;
    }

    private void Subscribe()
    {
        if (stateService == null)
            return;
        stateService.OnReadyChanged -= HandleReadyChanged;
        stateService.OnFlagChanged -= HandleFlagChanged;
        stateService.OnReadyChanged += HandleReadyChanged;
        stateService.OnFlagChanged += HandleFlagChanged;
    }

    private void Unsubscribe()
    {
        if (stateService == null)
            return;
        stateService.OnReadyChanged -= HandleReadyChanged;
        stateService.OnFlagChanged -= HandleFlagChanged;
    }

    private void HandleReadyChanged(bool ready)
    {
        Apply();
    }

    private void HandleFlagChanged(string changedKey, bool value)
    {
        if (string.Equals(changedKey, flagKey, StringComparison.Ordinal))
            Apply();
    }

    private void Apply()
    {
        CaptureDefault();
        WorldPresenceApply.Apply(
            target,
            stateService,
            flagKey,
            expectedFlagValue,
            activeWhenMatched,
            defaultActive,
            hasCapturedDefault);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (target == gameObject)
        {
            Debug.LogWarning(
                "[ProfileWorldFlagObjectGate] Put the gate on a separate host when disabling its target.",
                this);
        }

        if (string.IsNullOrWhiteSpace(flagKey))
            Debug.LogWarning("[ProfileWorldFlagObjectGate] Flag Key is empty.", this);
    }
#endif
}
