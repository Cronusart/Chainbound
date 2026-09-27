using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable quest-driven NPC interaction profile.
/// Stores dialogue/trigger outputs plus objective and NPC memory predicates
/// that can be assigned to a QuestProgressInteractionBridge host.
/// </summary>
[CreateAssetMenu(
    fileName = "QuestInteractionProfile",
    menuName = "Chainbound/Narrative/Interactions/Quest Interaction Profile")]
public class QuestInteractionProfileSO : ScriptableObject
{
    [Header("Quest")]
    [Tooltip("Optional quest definition asset. If assigned, its questId is used.")]
    [SerializeField] private QuestDefinitionSO questDefinition;

    [Tooltip("Manual quest ID fallback when no quest definition asset is assigned.")]
    [SerializeField] private string questId = "";

    [Header("Rules")]
    [Tooltip("Ordered rules. First matching rule wins while the quest is active.")]
    [SerializeField] private List<QuestInteractionRule> rules = new List<QuestInteractionRule>();

    [Header("When Quest Is Not Active")]
    [Tooltip("Dialogue to play when the quest is not active. This includes both not-started and completed states in v1.")]
    [SerializeField] private DialogueSequenceSO inactiveDialogue;

    [Tooltip("Trigger ID to fire when the quest is not active. Leave empty for no trigger.")]
    [SerializeField] private string inactiveTriggerId = "";

    [Tooltip("Optional NPC behavior to start before inactive outputs fire.")]
    [SerializeField] private QuestInteractionNpcBehaviorBinding inactiveNpcBehavior = new QuestInteractionNpcBehaviorBinding();

    [Header("When Quest Is Active But No Rule Matches")]
    [Tooltip("Dialogue to play when the quest is active but no ordered rule matches.")]
    [SerializeField] private DialogueSequenceSO activeFallbackDialogue;

    [Tooltip("Trigger ID to fire when the quest is active but no ordered rule matches. Leave empty for no trigger.")]
    [SerializeField] private string activeFallbackTriggerId = "";

    [Tooltip("Optional NPC behavior to start before active fallback outputs fire.")]
    [SerializeField] private QuestInteractionNpcBehaviorBinding activeFallbackNpcBehavior = new QuestInteractionNpcBehaviorBinding();

    [Header("Notes")]
    [TextArea(2, 5)]
    [Tooltip("Optional designer notes about where and when this profile should be used.")]
    [SerializeField] private string designerNotes = "";

    [Header("Activation")]
    [Tooltip("When the quest is Available (offered, not started), interacting starts it after inactive outputs fire. Use this to gate follow-up quests on an NPC talk.")]
    [SerializeField] private bool activateQuestOnInactiveInteract;

    [Tooltip("Optional objective to mark complete when Activate Quest On Inactive Interact starts the quest (e.g. talk_to_reynard).")]
    [SerializeField] private string completeObjectiveOnActivate = "";

    public QuestDefinitionSO QuestDefinition => questDefinition;
    public bool HasQuestId => !string.IsNullOrWhiteSpace(QuestId);

    public string QuestId
    {
        get
        {
            if (questDefinition != null && !string.IsNullOrWhiteSpace(questDefinition.QuestId))
                return questDefinition.QuestId;

            return questId;
        }
    }

    public IReadOnlyList<QuestInteractionRule> Rules => rules;
    public DialogueSequenceSO InactiveDialogue => inactiveDialogue;
    public string InactiveTriggerId => inactiveTriggerId;
    public QuestInteractionNpcBehaviorBinding InactiveNpcBehavior => inactiveNpcBehavior;
    public DialogueSequenceSO ActiveFallbackDialogue => activeFallbackDialogue;
    public string ActiveFallbackTriggerId => activeFallbackTriggerId;
    public QuestInteractionNpcBehaviorBinding ActiveFallbackNpcBehavior => activeFallbackNpcBehavior;
    public string DesignerNotes => designerNotes;
    public bool ActivateQuestOnInactiveInteract => activateQuestOnInactiveInteract;
    public string CompleteObjectiveOnActivate => completeObjectiveOnActivate;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (questDefinition == null && string.IsNullOrWhiteSpace(questId))
        {
            Debug.LogWarning(
                "[QuestInteractionProfileSO] Quest Definition or manual questId should be assigned for this profile.",
                this);
        }

        ValidateNpcBehaviorBinding(inactiveNpcBehavior, "inactive NPC behavior");
        ValidateNpcBehaviorBinding(activeFallbackNpcBehavior, "active fallback NPC behavior");

        if (rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            QuestInteractionRule rule = rules[i];
            if (rule == null)
                continue;

            string label = string.IsNullOrWhiteSpace(rule.label) ? $"rule {i}" : $"rule {i} ('{rule.label}')";
            ValidateNpcBehaviorBinding(rule.npcBehavior, label);
        }
    }

    private void ValidateNpcBehaviorBinding(QuestInteractionNpcBehaviorBinding binding, string label)
    {
        if (binding == null || binding.mode != QuestInteractionNpcBehaviorMode.Override || binding.behaviorOverride != null)
            return;

        Debug.LogWarning(
            $"[QuestInteractionProfileSO] {label} is set to Override, but no NPCInteractionBehaviorSO asset is assigned.",
            this);
    }
#endif
}
