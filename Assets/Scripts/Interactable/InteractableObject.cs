using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Static-object interactable (monitor, vending machine, etc.).
/// Supports RequireInteract (button press via PlayerInteractor) or AutoOnEnter (trigger on enter).
/// Registers with InteractableRegistry when player in range; PlayerInteractor routes Interact to nearest.
/// Wire OnInteracted to TriggerRegistry, SequenceDirector, DialogueService, etc.
/// </summary>
[RequireComponent(typeof(Collider))]
public class InteractableObject : MonoBehaviour, IInteractable
{
    public enum TriggerMode { RequireInteract, AutoOnEnter }

    [Header("Trigger")]
    [Tooltip("RequireInteract: player must press Interact while in range (via PlayerInteractor). AutoOnEnter: fires on trigger enter.")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.RequireInteract;

    [Tooltip("Player tag")]
    [SerializeField] private string playerTag = "Player";

    [Header("Output")]
    [Tooltip("Invoked when interaction fires. Wire to TriggerRegistry.FireTrigger, SequenceDirector, etc.")]
    [SerializeField] private UnityEvent onInteracted;

    [Header("Quest Guard (optional)")]
    [Tooltip("Lifecycle-aware quest guard. Leave empty for always available.")]
    [SerializeField] private NarrativeQuestGuard questGuard = new NarrativeQuestGuard();

    [Header("Indicator")]
    [Tooltip("Optional indicator to show/hide when player in range.")]
    [SerializeField] private InteractableIndicator indicator;

    [Header("Availability")]
    [SerializeField] private InteractableAvailabilitySettings availabilitySettings = new InteractableAvailabilitySettings();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [SerializeField, HideInInspector, FormerlySerializedAs("guardQuestId")] private string deprecatedGuardQuestId = "";

    private Collider triggerCollider;
    private bool playerInRange;
    private InteractableAvailabilityGate availabilityGate;

    Transform IInteractable.Transform => transform;
    bool IInteractable.IsInteractable => CanInteract;
    public bool HasDeprecatedGuardQuestId => !string.IsNullOrWhiteSpace(deprecatedGuardQuestId);

    private bool IsArmed
    {
        get
        {
            return NarrativeQuestGuardUtility.Evaluate(
                QuestService.Instance,
                questGuard,
                emptyGuardPasses: true).Passes;
        }
    }

    private bool IsAvailabilityBlocked => availabilityGate != null && availabilityGate.IsBlocked;

    private bool IsInputLocked
    {
        get
        {
            InputLockService inputLock = InputLockService.Instance;
            return inputLock != null && inputLock.IsInteractionLocked;
        }
    }

    private bool CanInteract => IsArmed && playerInRange && !IsAvailabilityBlocked && !IsInputLocked;

    private void OnValidate()
    {
        EnsureIndicatorReference();
        if (HasDeprecatedGuardQuestId)
        {
            Debug.LogWarning(
                "[InteractableObject] Legacy guardQuestId data is still serialized on this component. Author availability through NarrativeQuestGuard instead.",
                this);
        }
    }

    private void Awake()
    {
        EnsureAvailabilitySettings();
        EnsureIndicatorReference();
        EnsureAvailabilityGate();
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;
    }

    private void OnDisable()
    {
        EnsureAvailabilityGate();
        availabilityGate?.ResetState();

        if (playerInRange)
        {
            playerInRange = false;
            InteractableRegistry.Instance?.Unregister(this);
            indicator?.SetInRange(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = true;
        InteractableRegistry.Instance?.Register(this);
        indicator?.SetInRange(true);

        if (triggerMode == TriggerMode.AutoOnEnter && CanInteract)
        {
            FireInteraction();
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"[InteractableObject] Player entered range: {gameObject.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = false;
        InteractableRegistry.Instance?.Unregister(this);
        indicator?.SetInRange(false);

        if (enableDebugLogs)
            Debug.Log($"[InteractableObject] Player exited range: {gameObject.name}");
    }

    void IInteractable.Interact()
    {
        if (!CanInteract) return;
        FireInteraction();
    }

    private void FireInteraction()
    {
        EnsureAvailabilitySettings();
        EnsureAvailabilityGate();

        if (enableDebugLogs)
            Debug.Log($"[InteractableObject] Fired: {gameObject.name}");

        int baselinePlaybackId = InteractableAvailabilityGate.CaptureCurrentDialoguePlaybackId();
        availabilityGate.BeginForDialogueWindow(availabilitySettings, baselinePlaybackId);
        onInteracted?.Invoke();
    }

    /// <summary>Arm/disarm at runtime (e.g. via Step_EnableGameObject on parent).</summary>
    public void SetEnabled(bool enabled) => gameObject.SetActive(enabled);

    private void EnsureIndicatorReference()
    {
        if (indicator != null)
            return;

        indicator = GetComponentInChildren<InteractableIndicator>(true);
    }

    private void EnsureAvailabilitySettings()
    {
        if (availabilitySettings == null)
            availabilitySettings = new InteractableAvailabilitySettings();
    }

    private void EnsureAvailabilityGate()
    {
        if (availabilityGate != null && availabilityGate.Indicator == indicator)
            return;

        availabilityGate?.ResetState();
        availabilityGate = new InteractableAvailabilityGate(this, indicator, enableDebugLogs);
    }
}
