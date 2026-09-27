using Chainbound.Combat;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// World object representing a starter option. Registers itself with StarterSelectionService.
/// </summary>
public class StarterOption : MonoBehaviour
{
    [Header("Starter Info")]
    [Tooltip("Fallback species id used when no SpeciesDefinitionSO is assigned.")]
    [SerializeField] private string chainlingId = "";

    [Tooltip("Fallback display name used when no SpeciesDefinitionSO is assigned.")]
    [SerializeField] private string displayName = "";

    [Tooltip("Fallback description used when no SpeciesDefinitionSO is assigned.")]
    [TextArea(2, 4)]
    [SerializeField] private string description = "";

    [Tooltip("Primary species definition for this starter.")]
    [SerializeField] private SpeciesDefinitionSO speciesDefinition;

    [Header("Visual")]
    [Tooltip("Optional portrait/icon override.")]
    [SerializeField] private Sprite icon;

    [Tooltip("Optional combat visual profile used by the preview overlay.")]
    [SerializeField] private CombatVisualProfileSO visualProfile;

    [Header("Confirmed World State")]
    [Tooltip("Disable this starter's world object after the player confirms it.")]
    [SerializeField] private bool hideWorldObjectOnConfirmedSelection;

    [Tooltip("Stable key used to keep this starter hidden after scene reloads and continue-game. Prefer world.pickup.* so ProfileWorldFlagStateService can persist it.")]
    [SerializeField] private string confirmedHiddenStateId = "";

    [Tooltip("Optional object to disable. When empty, this StarterOption GameObject is used.")]
    [SerializeField] private GameObject confirmedStateTarget;

    [Header("Events")]
    [Tooltip("Invoked only when this starter has been fully confirmed.")]
    public UnityEvent OnSelected;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    public string ChainlingId =>
        !string.IsNullOrWhiteSpace(chainlingId)
            ? chainlingId
            : speciesDefinition != null
                ? speciesDefinition.speciesId
                : "";

    public string DisplayName =>
        speciesDefinition != null && !string.IsNullOrWhiteSpace(speciesDefinition.displayName)
            ? speciesDefinition.displayName
            : displayName;

    public string Description =>
        speciesDefinition != null && !string.IsNullOrWhiteSpace(speciesDefinition.description)
            ? speciesDefinition.description
            : description;

    public Sprite Icon =>
        icon != null
            ? icon
            : visualProfile != null
                ? visualProfile.portrait
                : null;

    public SpeciesDefinitionSO SpeciesDefinition => speciesDefinition;
    public CombatVisualProfileSO VisualProfile => visualProfile;
    public ChainlingType PrimaryType => speciesDefinition != null ? speciesDefinition.primaryType : ChainlingType.None;
    public ChainlingType SecondaryType => speciesDefinition != null ? speciesDefinition.secondaryType : ChainlingType.None;

    private ProfileWorldFlagStateService flagService;

    private void OnEnable()
    {
        SubscribeFlagService();
        if (ApplyPersistedHiddenState())
            return;

        StarterSelectionService.Instance?.RegisterOption(this);
    }

    private void OnDisable()
    {
        UnsubscribeFlagService();
        StarterSelectionService.Instance?.UnregisterOption(this);
    }

    /// <summary>
    /// Preview this starter option. The selection is only committed after the UI confirm action.
    /// </summary>
    public void Select()
    {
        StarterSelectionService service = StarterSelectionService.Instance;
        if (service == null)
            return;

        if (!service.IsSelecting)
        {
            if (enableDebugLogs)
                Debug.Log($"[StarterOption] Ignored preview for '{ChainlingId}' because starter selection is not active.");
            return;
        }

        service.PreviewStarter(this);
    }

    public void NotifyConfirmedSelection()
    {
        OnSelected?.Invoke();

        if (enableDebugLogs)
            Debug.Log($"[StarterOption] Confirmed starter '{ChainlingId}'.");
    }

    /// <summary>
    /// Applies the optional confirmed-selection world state after selection listeners have run.
    /// Session state covers same-runtime scene reloads. Profile world flags cover continue-game
    /// and post-quest lab visits.
    /// </summary>
    public void ApplyConfirmedWorldState()
    {
        if (!hideWorldObjectOnConfirmedSelection)
            return;

        string stateId = ResolveConfirmedStateId();
        NarrativeSceneObjectStateService stateService = NarrativeSceneObjectStateService.Instance
                                                       ?? FindFirstObjectByType<NarrativeSceneObjectStateService>(FindObjectsInactive.Include);
        if (stateService != null)
            stateService.RecordDesiredState(stateId, desiredActive: false);

        ProfileWorldFlagStateService flags = ResolveFlagService();
        if (flags != null)
            flags.SetFlag(stateId, true);

        HideConfirmedTarget();
    }

    private bool ApplyPersistedHiddenState()
    {
        if (!hideWorldObjectOnConfirmedSelection)
            return false;

        string stateId = ResolveConfirmedStateId();
        NarrativeSceneObjectStateService stateService = NarrativeSceneObjectStateService.Instance
                                                       ?? FindFirstObjectByType<NarrativeSceneObjectStateService>(FindObjectsInactive.Include);
        if (stateService != null &&
            stateService.TryGetDesiredState(stateId, out bool desiredActive) &&
            !desiredActive)
        {
            HideConfirmedTarget();
            return true;
        }

        ProfileWorldFlagStateService flags = ResolveFlagService();
        if (IsCommittedStarterForThisOption())
        {
            if (flags != null)
                flags.SetFlag(stateId, true);
            HideConfirmedTarget();
            return true;
        }

        if (flags != null && flags.IsHydrated && flags.GetFlag(stateId))
        {
            HideConfirmedTarget();
            return true;
        }

        return false;
    }

    private void SubscribeFlagService()
    {
        flagService = ResolveFlagService();
        if (flagService == null)
            return;

        flagService.OnReadyChanged -= HandleFlagReadyChanged;
        flagService.OnFlagChanged -= HandleFlagChanged;
        flagService.OnReadyChanged += HandleFlagReadyChanged;
        flagService.OnFlagChanged += HandleFlagChanged;
    }

    private void UnsubscribeFlagService()
    {
        if (flagService == null)
            return;

        flagService.OnReadyChanged -= HandleFlagReadyChanged;
        flagService.OnFlagChanged -= HandleFlagChanged;
        flagService = null;
    }

    private void HandleFlagReadyChanged(bool ready)
    {
        if (ready)
            ApplyPersistedHiddenState();
    }

    private void HandleFlagChanged(string changedKey, bool value)
    {
        if (!hideWorldObjectOnConfirmedSelection ||
            !value ||
            !string.Equals(changedKey, ResolveConfirmedStateId(), System.StringComparison.Ordinal))
        {
            return;
        }

        HideConfirmedTarget();
    }

    private void HideConfirmedTarget()
    {
        GameObject target = ResolveConfirmedStateTarget();
        if (target != null)
            target.SetActive(false);
    }

    private static ProfileWorldFlagStateService ResolveFlagService()
    {
        return FindFirstObjectByType<ProfileWorldFlagStateService>(FindObjectsInactive.Include);
    }

    private string ResolveConfirmedStateId()
    {
        if (!string.IsNullOrWhiteSpace(confirmedHiddenStateId))
            return confirmedHiddenStateId.Trim();

        return $"starter_orb.{ChainlingId}";
    }

    private bool IsCommittedStarterForThisOption()
    {
        string committed = GameSessionContext.Instance != null
            ? GameSessionContext.Instance.StarterSpeciesId
            : "";
        if (string.IsNullOrWhiteSpace(committed) || string.IsNullOrWhiteSpace(ChainlingId))
            return false;

        return string.Equals(committed.Trim(), ChainlingId.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private GameObject ResolveConfirmedStateTarget()
    {
        return confirmedStateTarget != null ? confirmedStateTarget : gameObject;
    }

    private void OnValidate()
    {
        confirmedHiddenStateId = confirmedHiddenStateId?.Trim() ?? "";
    }
}
