using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Canonical quest interaction bridge.
/// Availability gating lives outside this component; this bridge only routes the successful
/// interaction to a runtime override profile, router-selected profile, or default profile.
/// </summary>
[DisallowMultipleComponent]
public class QuestProgressInteractionBridge : MonoBehaviour
{
    [Header("Services (Optional Scene References)")]
    [Tooltip("QuestService in the scene. Auto-finds if left empty.")]
    [SerializeField] private QuestService questService;

    [Tooltip("DialogueService in the scene. Auto-finds if left empty.")]
    [SerializeField] private DialogueService dialogueService;

    [Tooltip("TriggerRegistry in the scene. Auto-finds if left empty.")]
    [SerializeField] private TriggerRegistry triggerRegistry;

    [Tooltip("NPC memory source used by memory predicates. Auto-finds in parents if left empty.")]
    [SerializeField] private NPCMemoryComponent npcMemory;

    [Tooltip("Optional time-of-day source used by time-aware interaction rules. Must implement ITimeOfDayService. Auto-resolves TimeOfDayManager when left empty.")]
    [SerializeField] private MonoBehaviour timeOfDaySourceOverride;

    [Tooltip("Optional NPC controller used when quest-routed interactions should start NPC interaction behavior. Auto-finds in parents if left empty.")]
    [SerializeField] private NPCController npcController;

    [Header("Profiles")]
    [Tooltip("Default quest interaction profile used by this bridge.")]
    [SerializeField] private QuestInteractionProfileSO defaultProfile;

    [Tooltip("Optional ordered router. If assigned, its first matching slot wins before the default profile is considered.")]
    [SerializeField] private QuestInteractionRouterSO interactionRouter;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    [SerializeField, HideInInspector, FormerlySerializedAs("questId")] private string deprecatedQuestId = "";
    [SerializeField, HideInInspector, FormerlySerializedAs("rules")] private List<QuestInteractionRule> deprecatedRules = new List<QuestInteractionRule>();
    [SerializeField, HideInInspector, FormerlySerializedAs("inactiveDialogue")] private DialogueSequenceSO deprecatedInactiveDialogue;
    [SerializeField, HideInInspector, FormerlySerializedAs("inactiveTriggerId")] private string deprecatedInactiveTriggerId = "";
    [SerializeField, HideInInspector, FormerlySerializedAs("activeFallbackDialogue")] private DialogueSequenceSO deprecatedActiveFallbackDialogue;
    [SerializeField, HideInInspector, FormerlySerializedAs("activeFallbackTriggerId")] private string deprecatedActiveFallbackTriggerId = "";

    private QuestInteractionProfileSO runtimeOverrideProfile;
    private DialogueSpeakerCameraBridge ownedDialogueCameraBridge;
    private DialogueSequenceSO ownedCameraDialogue;

    public QuestInteractionProfileSO DefaultProfile => defaultProfile;
    public QuestInteractionRouterSO InteractionRouter => interactionRouter;
    public MonoBehaviour TimeOfDaySourceOverride => timeOfDaySourceOverride;
    public bool HasAuthoringProfileSource => defaultProfile != null || interactionRouter != null;
    public bool HasExplicitTimeOfDaySourceOverride => timeOfDaySourceOverride != null;
    public bool HasValidTimeOfDaySourceOverride => timeOfDaySourceOverride == null || timeOfDaySourceOverride is ITimeOfDayService;
    public bool HasDeprecatedInlineAuthoringData =>
        !string.IsNullOrWhiteSpace(deprecatedQuestId) ||
        (deprecatedRules != null && deprecatedRules.Count > 0) ||
        deprecatedInactiveDialogue != null ||
        !string.IsNullOrWhiteSpace(deprecatedInactiveTriggerId) ||
        deprecatedActiveFallbackDialogue != null ||
        !string.IsNullOrWhiteSpace(deprecatedActiveFallbackTriggerId);

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ReleaseOwnedDialogueCameraBridge();
    }

    private void OnValidate()
    {
        if (!HasAuthoringProfileSource)
        {
            Debug.LogWarning(
                "[QuestProgressInteractionBridge] Assign a default profile or interaction router. Runtime overrides are additive and should not be the only authored source in normal content.",
                this);
        }

        if (HasDeprecatedInlineAuthoringData)
        {
            Debug.LogWarning(
                "[QuestProgressInteractionBridge] Legacy inline interaction fields are still serialized on this component. Remove the old data and author behavior through QuestInteractionProfileSO assets instead.",
                this);
        }

        if (!HasValidTimeOfDaySourceOverride)
        {
            Debug.LogWarning(
                "[QuestProgressInteractionBridge] Time Of Day Source Override must implement ITimeOfDayService. Leave it empty to auto-resolve TimeOfDayManager instead.",
                this);
        }
    }

    /// <summary>
    /// Parameterless entry point for UnityEvent wiring from NPCInteractableBinding or InteractableObject.
    /// </summary>
    public void OnInteract()
    {
        ResolveReferences();
        ITimeOfDayService timeOfDayService = ResolveTimeOfDayService();

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            questService,
            npcMemory,
            timeOfDayService,
            runtimeOverrideProfile,
            interactionRouter,
            defaultProfile,
            npcController != null ? npcController.InteractionBehavior : null);

        if (!string.IsNullOrWhiteSpace(resolution.Warning))
            Debug.LogWarning($"[QuestProgressInteractionBridge] {resolution.Warning}", this);

        if (!resolution.IsValid)
            return;

        TryStartNpcInteractionSession(resolution);
        ExecuteOutputs(resolution);
        TryActivateAvailableQuest(resolution);
    }

    public void SetRuntimeProfileOverride(QuestInteractionProfileSO profile)
    {
        runtimeOverrideProfile = profile;
        LogDebug(profile == null
            ? "Runtime profile override cleared by SetRuntimeProfileOverride(null)."
            : $"Runtime profile override set to '{profile.name}'.");
    }

    public void ClearRuntimeProfileOverride()
    {
        runtimeOverrideProfile = null;
        LogDebug("Runtime profile override cleared.");
    }

    public void RestoreDefaultProfile()
    {
        runtimeOverrideProfile = null;
        LogDebug(defaultProfile != null
            ? $"Restored default profile '{defaultProfile.name}'."
            : interactionRouter != null
                ? $"Restored router-driven interaction source '{interactionRouter.name}'."
                : "Restored authoring-time interaction source state with no default profile or router assigned.");
    }

    private void ResolveReferences()
    {
        if (questService == null)
            questService = FindFirstObjectByType<QuestService>();

        if (dialogueService == null)
            dialogueService = FindFirstObjectByType<DialogueService>();

        if (triggerRegistry == null)
            triggerRegistry = FindFirstObjectByType<TriggerRegistry>();

        if (npcMemory == null)
            npcMemory = GetComponentInParent<NPCMemoryComponent>();

        if (npcController == null)
            npcController = GetComponentInParent<NPCController>();
    }

    private ITimeOfDayService ResolveTimeOfDayService()
    {
        if (timeOfDaySourceOverride != null)
            return timeOfDaySourceOverride as ITimeOfDayService;

        return TimeOfDayManager.Instance;
    }

    private void ExecuteOutputs(QuestInteractionResolver.QuestInteractionResolution resolution)
    {
        DialogueSequenceSO dialogue = resolution.Dialogue;
        string triggerId = resolution.TriggerId;
        string context = resolution.ContextLabel;
        bool hasDialogue = dialogue != null;
        bool hasTrigger = !string.IsNullOrWhiteSpace(triggerId);

        if (!hasDialogue && !hasTrigger)
        {
            LogDebug($"No outputs configured for {context}.");
            return;
        }

        if (hasDialogue)
        {
            if (dialogueService == null)
            {
                Debug.LogWarning($"[QuestProgressInteractionBridge] DialogueService not found for {context}; dialogue '{dialogue.name}' was not played.", this);
            }
            else
            {
                if (resolution.UseDialogueCamera)
                    AcquireDialogueCameraBridge(dialogue, context);

                Coroutine playback = dialogueService.PlayDialogue(dialogue);
                if (playback == null && ownedCameraDialogue == dialogue)
                    ReleaseOwnedDialogueCameraBridge();
                LogDebug($"Played dialogue '{dialogue.name}' for {context}.");
            }
        }

        if (hasTrigger)
        {
            if (triggerRegistry == null)
            {
                Debug.LogWarning($"[QuestProgressInteractionBridge] TriggerRegistry not found for {context}; trigger '{triggerId}' was not fired.", this);
            }
            else
            {
                triggerRegistry.FireTrigger(triggerId);
                LogDebug($"Fired trigger '{triggerId}' for {context}.");
            }
        }
    }

    private void AcquireDialogueCameraBridge(DialogueSequenceSO dialogue, string context)
    {
        ReleaseOwnedDialogueCameraBridge();

        DialogueSpeakerCameraBridge bridge = DialogueSpeakerCameraBridge.FindInstance();
        if (bridge == null)
        {
            Debug.LogWarning(
                $"[QuestProgressInteractionBridge] {context} requested dialogue camera playback, but DialogueSpeakerCameraBridge was not found.",
                this);
            return;
        }

        // An already-enabled bridge is owned by another narrative flow. It will receive the
        // dialogue events without this interaction taking responsibility for disabling it.
        if (bridge.enabled)
            return;

        ownedDialogueCameraBridge = bridge;
        ownedCameraDialogue = dialogue;
        dialogueService.OnDialogueComplete += OnOwnedCameraDialogueComplete;
        bridge.enabled = true;
    }

    private void OnOwnedCameraDialogueComplete(DialogueSequenceSO dialogue)
    {
        if (dialogue != ownedCameraDialogue)
            return;

        ReleaseOwnedDialogueCameraBridge();
    }

    private void ReleaseOwnedDialogueCameraBridge()
    {
        if (dialogueService != null)
            dialogueService.OnDialogueComplete -= OnOwnedCameraDialogueComplete;

        DialogueSpeakerCameraBridge bridge = ownedDialogueCameraBridge;
        ownedDialogueCameraBridge = null;
        ownedCameraDialogue = null;
        if (bridge != null)
            bridge.enabled = false;
    }

    private void TryStartNpcInteractionSession(QuestInteractionResolver.QuestInteractionResolution resolution)
    {
        if (resolution.InteractionBehavior == null)
            return;

        if (npcController == null)
        {
            Debug.LogWarning(
                $"[QuestProgressInteractionBridge] {resolution.ContextLabel} requested NPC interaction behavior, but no NPCController was found.",
                this);
            return;
        }

        NPCInteractionSessionService sessionService = NPCInteractionSessionService.Instance;
        if (sessionService == null)
        {
            Debug.LogWarning(
                $"[QuestProgressInteractionBridge] {resolution.ContextLabel} requested NPC interaction behavior, but no NPCInteractionSessionService was found.",
                this);
            return;
        }

        sessionService.BeginInteraction(npcController, resolution.InteractionBehavior, resolution.ContextLabel);
    }

    private void TryActivateAvailableQuest(QuestInteractionResolver.QuestInteractionResolution resolution)
    {
        QuestInteractionProfileSO profile = resolution.SelectedProfile;
        if (profile == null || !profile.ActivateQuestOnInactiveInteract)
            return;

        if (resolution.LifecycleState != QuestLifecycleState.Available)
            return;

        QuestDefinitionSO definition = profile.QuestDefinition;
        if (definition == null || string.IsNullOrWhiteSpace(profile.QuestId))
        {
            Debug.LogWarning(
                $"[QuestProgressInteractionBridge] {resolution.ContextLabel} requested quest activation, but the profile has no quest definition.",
                this);
            return;
        }

        if (questService == null)
        {
            Debug.LogWarning(
                $"[QuestProgressInteractionBridge] {resolution.ContextLabel} requested quest activation, but QuestService was not found.",
                this);
            return;
        }

        questService.StartQuest(profile.QuestId, definition);
        if (!string.IsNullOrWhiteSpace(profile.CompleteObjectiveOnActivate)
            && questService.TryGetObjective(profile.QuestId, profile.CompleteObjectiveOnActivate, out QuestObjective objective)
            && objective != null)
        {
            questService.SetObjectiveProgress(
                profile.QuestId,
                profile.CompleteObjectiveOnActivate,
                Mathf.Max(1, objective.targetAmount));
        }
        LogDebug($"Started available quest '{profile.QuestId}' from {resolution.ContextLabel}.");
    }

    private void LogDebug(string message)
    {
        if (!enableDebugLogs)
            return;

        Debug.Log($"[QuestProgressInteractionBridge] {message}", this);
    }
}
