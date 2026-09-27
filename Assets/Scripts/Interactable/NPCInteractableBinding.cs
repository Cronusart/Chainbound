using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

/// <summary>
/// Binds an NPC to the InteractableRegistry so it receives Interact input like static objects.
/// Add to NPC GameObject (or child). Requires a trigger Collider for range detection.
/// Supports RequireInteract (button press) or AutoOnEnter (proximity-only).
/// Wire OnInteracted to dialogue, sequence steps, etc.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NPCInteractableBinding : MonoBehaviour, IInteractable
{
    public enum TriggerMode { RequireInteract, AutoOnEnter }

    [Header("Trigger")]
    [Tooltip("RequireInteract: player must press Interact. AutoOnEnter: fires on trigger enter.")]
    [SerializeField] private TriggerMode triggerMode = TriggerMode.RequireInteract;

    [Tooltip("Player tag")]
    [SerializeField] private string playerTag = "Player";

    [Header("Output")]
    [Tooltip("Invoked when interaction fires. Wire to dialogue, SequenceDirector, etc.")]
    [SerializeField] private UnityEvent onInteracted;

    [Header("Quest Guard (optional)")]
    [Tooltip("Lifecycle-aware quest guard. Leave empty for always available.")]
    [SerializeField] private NarrativeQuestGuard questGuard = new NarrativeQuestGuard();

    [Header("Indicator")]
    [Tooltip("Optional indicator to show when player in range.")]
    [SerializeField] private InteractableIndicator indicator;

    [Header("Availability")]
    [SerializeField] private InteractableAvailabilitySettings availabilitySettings = new InteractableAvailabilitySettings();

    [Header("NPC Memory (optional)")]
    [Tooltip("If set, RecordInteraction() is called on Interact.")]
    [SerializeField] private NPCMemoryComponent npcMemory;

    [Tooltip("NPC controller used for normal interaction behavior sessions. Auto-finds in parents if left empty.")]
    [SerializeField] private NPCController npcController;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [SerializeField, HideInInspector, FormerlySerializedAs("guardQuestId")] private string deprecatedGuardQuestId = "";

    private Collider triggerCollider;
    private bool playerInRange;
    private InteractableAvailabilityGate availabilityGate;

    Transform IInteractable.Transform => transform;
    bool IInteractable.IsInteractable => IsArmed && playerInRange && !IsAvailabilityBlocked;
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

    private bool CanInteract => IsArmed && playerInRange && !IsAvailabilityBlocked;

    private void OnValidate()
    {
        if (HasDeprecatedGuardQuestId)
        {
            Debug.LogWarning(
                "[NPCInteractableBinding] Legacy guardQuestId data is still serialized on this component. Author availability through NarrativeQuestGuard instead.",
                this);
        }
    }

    private void Awake()
    {
        EnsureAvailabilitySettings();
        EnsureAvailabilityGate();
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null) triggerCollider.isTrigger = true;

        if (npcMemory == null)
            npcMemory = GetComponentInParent<NPCMemoryComponent>();

        if (npcController == null)
            npcController = GetComponentInParent<NPCController>();
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
            Debug.Log($"[NPCInteractableBinding] Player entered range: {gameObject.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        playerInRange = false;
        InteractableRegistry.Instance?.Unregister(this);
        indicator?.SetInRange(false);

        if (enableDebugLogs)
            Debug.Log($"[NPCInteractableBinding] Player exited range: {gameObject.name}");
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
            Debug.Log($"[NPCInteractableBinding] Fired: {gameObject.name}");

        int baselinePlaybackId = InteractableAvailabilityGate.CaptureCurrentDialoguePlaybackId();
        NPCInteractionSessionService sessionService = null;
        int sessionId = 0;

        if (npcController != null && npcController.InteractionBehavior != null)
        {
            sessionService = NPCInteractionSessionService.Instance;
            if (sessionService != null)
            {
                sessionService.BeginNormalInteraction(npcController);
                sessionService.TryGetActiveSessionId(npcController, out sessionId);
            }
        }

        availabilityGate.BeginForNpcSession(availabilitySettings, sessionService, sessionId, baselinePlaybackId);

        npcMemory?.RecordInteraction();
        onInteracted?.Invoke();
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
