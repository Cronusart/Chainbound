using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Reusable quest-state gate that toggles target GameObjects based on whether a narrative quest guard passes.
/// Content stays in scene/prefab references; this component only interprets quest state.
/// </summary>
[DisallowMultipleComponent]
public class QuestStateGameObjectGate : MonoBehaviour
{
    [Header("Quest Guard")]
    [Tooltip("Lifecycle-aware guard. When it passes, 'Quest Active Targets' are enabled and 'Quest Inactive Targets' are disabled.")]
    [SerializeField] private NarrativeQuestGuard questGuard = new NarrativeQuestGuard();

    [Tooltip("Optional scene reference. Auto-finds QuestService if left empty.")]
    [SerializeField] private QuestService questService;

    [Tooltip("If true, missing QuestService falls back to the quest-inactive state instead of leaving targets unchanged.")]
    [SerializeField] private bool treatMissingQuestServiceAsInactive = true;

    [Header("Targets")]
    [Tooltip("Objects enabled while the quest is active.")]
    [SerializeField] private GameObject[] questActiveTargets = Array.Empty<GameObject>();

    [Tooltip("Objects enabled while the quest is not active. This includes both not-started and completed states in the current quest runtime.")]
    [SerializeField] private GameObject[] questInactiveTargets = Array.Empty<GameObject>();

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private bool subscribedToQuestEvents;
    private bool hasWarnedAboutMissingQuestConfiguration;
    private bool hasWarnedAboutMissingQuestService;

    [SerializeField, HideInInspector, FormerlySerializedAs("questId")] private string deprecatedQuestId = "";

    public bool HasDeprecatedQuestId => !string.IsNullOrWhiteSpace(deprecatedQuestId);

    private void Awake()
    {
        ResolveQuestService();
    }

    private void OnEnable()
    {
        ResolveQuestService();
        SubscribeToQuestEvents();
        RefreshState();
    }

    private void Start()
    {
        ResolveQuestService();
        SubscribeToQuestEvents();
        RefreshState();
    }

    private void OnDisable()
    {
        UnsubscribeFromQuestEvents();
    }

    private void OnValidate()
    {
        if (!NarrativeQuestGuardUtility.HasConfiguredClauses(questGuard))
        {
            Debug.LogWarning("[QuestStateGameObjectGate] Quest guard is empty. Assign a Narrative Quest Guard so the gate can evaluate state.", this);
        }

        if (HasDeprecatedQuestId)
        {
            Debug.LogWarning(
                "[QuestStateGameObjectGate] Legacy questId data is still serialized on this component. Author gate state through NarrativeQuestGuard instead.",
                this);
        }

        if ((questActiveTargets == null || questActiveTargets.Length == 0)
            && (questInactiveTargets == null || questInactiveTargets.Length == 0))
        {
            Debug.LogWarning("[QuestStateGameObjectGate] No target GameObjects are assigned. Configure at least one quest-active or quest-inactive target.", this);
        }
    }

    /// <summary>
    /// Re-evaluate the configured quest and apply target active states.
    /// </summary>
    public void RefreshState()
    {
        if (!TryGetGuardPassState(out bool guardPasses))
            return;

        ApplyTargetStates(guardPasses);
    }

    private void HandleQuestLifecycleStateChanged(string changedQuestId, QuestLifecycleState _, QuestLifecycleState __)
    {
        if (!ShouldRefreshForQuest(changedQuestId))
            return;

        RefreshState();
    }

    private void ResolveQuestService()
    {
        if (questService == null)
        {
            questService = FindFirstObjectByType<QuestService>();
        }
    }

    private void SubscribeToQuestEvents()
    {
        if (subscribedToQuestEvents || questService == null)
            return;

        questService.OnQuestLifecycleStateChanged += HandleQuestLifecycleStateChanged;
        subscribedToQuestEvents = true;
    }

    private void UnsubscribeFromQuestEvents()
    {
        if (!subscribedToQuestEvents || questService == null)
            return;

        questService.OnQuestLifecycleStateChanged -= HandleQuestLifecycleStateChanged;
        subscribedToQuestEvents = false;
    }

    private bool TryGetGuardPassState(out bool guardPasses)
    {
        guardPasses = false;

        if (!NarrativeQuestGuardUtility.HasConfiguredClauses(questGuard))
        {
            if (!hasWarnedAboutMissingQuestConfiguration)
            {
                Debug.LogWarning("[QuestStateGameObjectGate] Cannot evaluate quest state because no quest guard is configured.", this);
                hasWarnedAboutMissingQuestConfiguration = true;
            }
            return false;
        }

        hasWarnedAboutMissingQuestConfiguration = false;

        ResolveQuestService();
        SubscribeToQuestEvents();

        if (questService == null)
        {
            if (!hasWarnedAboutMissingQuestService)
            {
                Debug.LogWarning(
                    "[QuestStateGameObjectGate] QuestService was not found. Falling back to the configured quest-inactive state.",
                    this);
                hasWarnedAboutMissingQuestService = true;
            }

            if (!treatMissingQuestServiceAsInactive)
                return false;

            guardPasses = false;
            return true;
        }

        hasWarnedAboutMissingQuestService = false;
        NarrativeQuestGuardResult guardResult = NarrativeQuestGuardUtility.Evaluate(
            questService,
            questGuard,
            emptyGuardPasses: false);

        if (!guardResult.IsConfigured)
            return false;

        guardPasses = guardResult.Passes;
        return true;
    }

    private bool ShouldRefreshForQuest(string changedQuestId)
    {
        if (string.IsNullOrWhiteSpace(changedQuestId))
            return false;

        return NarrativeQuestGuardUtility.ReferencesQuestId(questGuard, changedQuestId);
    }

    private void ApplyTargetStates(bool guardPasses)
    {
        SetTargetsActive(questActiveTargets, guardPasses);
        SetTargetsActive(questInactiveTargets, !guardPasses);

        if (enableDebugLogs)
        {
            Debug.Log(
                $"[QuestStateGameObjectGate] Applied guard state on {name}: guardPasses={guardPasses}",
                this);
        }
    }

    private static void SetTargetsActive(GameObject[] targets, bool isActive)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            GameObject target = targets[i];
            if (target == null)
                continue;

            if (target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }
    }
}
