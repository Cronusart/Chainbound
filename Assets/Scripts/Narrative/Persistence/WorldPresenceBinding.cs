using System;
using UnityEngine;

/// <summary>
/// Scene object presence keyed by a unique flag ID. Never disable the host;
/// assign a child or sibling as <see cref="target"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldPresenceBinding : MonoBehaviour
{
    [SerializeField] private string presenceId = "";
    [SerializeField] private GameObject target;
    [Tooltip("Once the flag exists: active when the flag is true, inactive when false.")]
    [SerializeField] private bool activeWhenTrue = true;
    [Tooltip("Unspecified uses the world-presence prefix board.")]
    [SerializeField] private WorldPresenceLifetime lifetimeOverride = WorldPresenceLifetime.Unspecified;

    private ProfileWorldFlagStateService stateService;
    private bool defaultActive;
    private bool hasCapturedDefault;

    public string PresenceId => presenceId != null ? presenceId.Trim() : "";
    public GameObject Target => target;
    public bool ActiveWhenTrue => activeWhenTrue;
    public WorldPresenceLifetime LifetimeOverride => lifetimeOverride;

    /// <summary>
    /// Stamps the presence id from a paired grant so scene instances author the id once.
    /// </summary>
    public void AuthorPresenceId(string id)
    {
        string trimmed = id != null ? id.Trim() : "";
        if (string.Equals(PresenceId, trimmed, StringComparison.Ordinal))
            return;

        presenceId = trimmed;
        RegisterLifetimeOverride();
        Apply();
    }

    private void Awake()
    {
        CaptureDefault();
        stateService = ProfileWorldFlagStateService.Instance;
        RegisterLifetimeOverride();
        Subscribe();
        Apply();
    }

    private void OnEnable()
    {
        if (stateService == null)
            stateService = ProfileWorldFlagStateService.Instance;
        RegisterLifetimeOverride();
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
        if (stateService != null && lifetimeOverride != WorldPresenceLifetime.Unspecified)
            stateService.ClearLifetimeOverride(PresenceId);
    }

    private void CaptureDefault()
    {
        if (hasCapturedDefault || target == null)
            return;

        defaultActive = target.activeSelf;
        hasCapturedDefault = true;
    }

    private void RegisterLifetimeOverride()
    {
        if (stateService == null || lifetimeOverride == WorldPresenceLifetime.Unspecified)
            return;
        if (string.IsNullOrWhiteSpace(PresenceId))
            return;
        stateService.RegisterLifetimeOverride(PresenceId, lifetimeOverride);
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
        if (string.Equals(changedKey, PresenceId, StringComparison.Ordinal))
            Apply();
    }

    private void Apply()
    {
        CaptureDefault();
        WorldPresenceApply.Apply(
            target,
            stateService,
            PresenceId,
            expectedFlagValue: true,
            activeWhenMatched: activeWhenTrue,
            defaultActive,
            hasCapturedDefault);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(PresenceId))
            Debug.LogWarning("[WorldPresenceBinding] Presence Id is empty.", this);

        if (target == null)
            Debug.LogWarning("[WorldPresenceBinding] Target is required.", this);
        else if (target == gameObject)
        {
            Debug.LogWarning(
                "[WorldPresenceBinding] Never disable the host. Assign a child or sibling as Target.",
                this);
        }

        WarnIfDuplicatePresenceId();
    }

    private void WarnIfDuplicatePresenceId()
    {
        if (string.IsNullOrWhiteSpace(PresenceId))
            return;

        WorldPresenceBinding[] bindings = FindObjectsByType<WorldPresenceBinding>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            WorldPresenceBinding other = bindings[i];
            if (other != null && string.Equals(other.PresenceId, PresenceId, StringComparison.Ordinal))
                count++;
        }

        if (count > 1)
        {
            Debug.LogWarning(
                $"[WorldPresenceBinding] Duplicate presenceId '{PresenceId}' among loaded scenes. " +
                "Explore copies need instance IDs (world.obstacle.link1.canopy_block); " +
                "shared-event copies may intentionally share one ID.",
                this);
        }
    }
#endif
}
