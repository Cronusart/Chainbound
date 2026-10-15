using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Comparison applied to quest objective progress inside a quest interaction rule.
/// </summary>
[MovedFrom(true, sourceClassName: "QuestProgressInteractionBridge")]
public enum QuestInteractionObjectiveComparison
{
    None,
    Complete,
    NotComplete,
    AtLeast,
    AtMost,
    Exactly
}

/// <summary>
/// Comparison applied to a label in the currently running narrative sequence.
/// The label step itself is considered not completed until the runner advances
/// to a later step.
/// </summary>
public enum QuestInteractionSequenceProgressComparison
{
    BeforeLabel,
    AtOrAfterLabel
}

/// <summary>
/// Existing rules remain Active-only. Opt in to an explicit lifecycle mask for
/// post-completion, available, failed, or locked-state interactions.
/// </summary>
public enum QuestInteractionRuleLifecycleScope
{
    ActiveOnly,
    UseLifecycleMask
}

public enum QuestInteractionResolutionComparison
{
    Exists,
    Missing,
    ResolutionIdEquals,
    ContextValueEquals,
    ContextKeyMissing
}

[Serializable]
public class QuestInteractionResolutionPredicate
{
    [Tooltip("Optional quest definition. Leave both quest fields empty to inspect the profile's quest.")]
    public QuestDefinitionSO questDefinition;

    [Tooltip("Manual quest ID fallback. Leave empty to inspect the profile's quest.")]
    public string questId = "";

    public QuestInteractionResolutionComparison comparison = QuestInteractionResolutionComparison.Exists;

    [Tooltip("Stable resolution ID used by ResolutionIdEquals.")]
    public string resolutionId = "";

    [Tooltip("Stable context key used by context comparisons.")]
    public string contextKey = "";

    [Tooltip("Stable context value used by ContextValueEquals.")]
    public string contextValue = "";

    public string ResolveQuestId(string profileQuestId)
    {
        if (questDefinition != null && !string.IsNullOrWhiteSpace(questDefinition.QuestId))
            return questDefinition.QuestId;
        return !string.IsNullOrWhiteSpace(questId) ? questId : profileQuestId;
    }
}

/// <summary>
/// Reusable narrative sequence progress predicate for quest interaction rules.
/// Uses stable sequence and label IDs rather than scene object references.
/// </summary>
[Serializable]
public class QuestInteractionSequenceProgressPredicate
{
    [Tooltip("Sequence ID to evaluate, e.g. chainbound_intro_sequence.")]
    public string sequenceId = "";

    [Tooltip("Label within the sequence to evaluate, e.g. choosestarter.")]
    public string label = "";

    [Tooltip("Whether the rule matches before the label or at/after the label completes.")]
    public QuestInteractionSequenceProgressComparison comparison = QuestInteractionSequenceProgressComparison.BeforeLabel;
}

/// <summary>
/// Comparison applied to NPC memory flags inside a quest interaction rule.
/// </summary>
[MovedFrom(true, sourceClassName: "QuestProgressInteractionBridge")]
public enum QuestInteractionMemoryFlagComparison
{
    Exists,
    Missing,
    IsTrue,
    IsFalse
}

/// <summary>
/// Comparison applied to NPC memory counters inside a quest interaction rule.
/// </summary>
[MovedFrom(true, sourceClassName: "QuestProgressInteractionBridge")]
public enum QuestInteractionMemoryCounterComparison
{
    Exists,
    Missing,
    AtLeast,
    AtMost,
    Exactly
}

/// <summary>
/// One NPC memory flag predicate within a quest interaction rule.
/// </summary>
[Serializable]
[MovedFrom(true, sourceClassName: "QuestProgressInteractionBridge")]
public class QuestInteractionMemoryFlagPredicate
{
    [Tooltip("NPC memory flag key to evaluate.")]
    public string key = "";

    [Tooltip("How the flag should be evaluated.")]
    public QuestInteractionMemoryFlagComparison comparison = QuestInteractionMemoryFlagComparison.Exists;
}

/// <summary>
/// One profile world-flag predicate within a quest interaction rule.
/// These flags are shared across NPCs and scenes. They are not NPC memory.
/// </summary>
[Serializable]
public class QuestInteractionWorldFlagPredicate
{
    [Tooltip("Profile world flag key, e.g. world.coffee.zeb_departed.")]
    public string key = "";

    [Tooltip("IsTrue requires the flag to exist and be true. IsFalse matches when it is absent or false.")]
    public QuestInteractionMemoryFlagComparison comparison = QuestInteractionMemoryFlagComparison.IsTrue;
}

/// <summary>
/// One profile-bag quantity predicate within a quest interaction rule.
/// An unread bag fails closed so a reward line cannot fire before inventory is known.
/// </summary>
[Serializable]
public class QuestInteractionBagItemPredicate
{
    [Tooltip("Profile bag item id, e.g. lucky_crayon.")]
    public string itemId = "";

    [Tooltip("Rule matches when the cached bag quantity is at least this value.")]
    public int minimumQuantity = 1;
}

/// <summary>
/// One NPC memory counter predicate within a quest interaction rule.
/// </summary>
[Serializable]
[MovedFrom(true, sourceClassName: "QuestProgressInteractionBridge")]
public class QuestInteractionMemoryCounterPredicate
{
    [Tooltip("NPC memory counter key to evaluate.")]
    public string key = "";

    [Tooltip("How the counter should be evaluated.")]
    public QuestInteractionMemoryCounterComparison comparison = QuestInteractionMemoryCounterComparison.Exists;

    [Tooltip("Threshold used by AtLeast, AtMost, and Exactly.")]
    public int value = 0;
}

/// <summary>
/// Controls whether a quest-resolved interaction should also start an NPC interaction session.
/// </summary>
public enum QuestInteractionNpcBehaviorMode
{
    Disabled,
    UseNpcDefault,
    Override
}

/// <summary>
/// Optional NPC interaction behavior binding for quest-routed interactions.
/// </summary>
[Serializable]
public class QuestInteractionNpcBehaviorBinding
{
    [Tooltip("Disabled skips NPC interaction behavior. Use NPC Default uses the NPCController default behavior. Override uses the explicit behavior asset below.")]
    public QuestInteractionNpcBehaviorMode mode = QuestInteractionNpcBehaviorMode.Disabled;

    [Tooltip("Behavior asset used when Mode is Override.")]
    public NPCInteractionBehaviorSO behaviorOverride;

    public NPCInteractionBehaviorSO Resolve(NPCInteractionBehaviorSO defaultBehavior, out string warning)
    {
        warning = "";

        switch (mode)
        {
            case QuestInteractionNpcBehaviorMode.Disabled:
                return null;

            case QuestInteractionNpcBehaviorMode.UseNpcDefault:
                if (defaultBehavior != null)
                    return defaultBehavior;

                warning = "Behavior mode is UseNpcDefault, but the NPC has no default interaction behavior assigned.";
                return null;

            case QuestInteractionNpcBehaviorMode.Override:
                if (behaviorOverride != null)
                    return behaviorOverride;

                warning = "Behavior mode is Override, but no override behavior asset is assigned.";
                return null;

            default:
                return null;
        }
    }
}

/// <summary>
/// Ordered dialogue/trigger rule used while a quest is active.
/// </summary>
[Serializable]
[MovedFrom(true, sourceClassName: "QuestProgressInteractionBridge")]
public class QuestInteractionRule
{
    [Tooltip("Optional label for designer readability only.")]
    public string label = "";

    [Tooltip("ActiveOnly preserves legacy behavior. UseLifecycleMask enables rules for other quest states.")]
    public QuestInteractionRuleLifecycleScope lifecycleScope = QuestInteractionRuleLifecycleScope.ActiveOnly;

    [Tooltip("Lifecycle states used only when Lifecycle Scope is UseLifecycleMask.")]
    public NarrativeQuestLifecycleMask lifecycleMask = NarrativeQuestLifecycleMask.Active;

    [Tooltip("Objective ID to evaluate while the quest is active. Leave empty when this rule should not check objective progress.")]
    public string objectiveId = "";

    [Tooltip("How to evaluate the objective. None skips objective checks for this rule.")]
    public QuestInteractionObjectiveComparison objectiveComparison = QuestInteractionObjectiveComparison.None;

    [Tooltip("Threshold used by AtLeast, AtMost, and Exactly.")]
    public int objectiveValue = 0;

    [Tooltip("All sequence progress predicates must pass for this rule to match.")]
    public List<QuestInteractionSequenceProgressPredicate> sequenceProgressPredicates =
        new List<QuestInteractionSequenceProgressPredicate>();

    [Tooltip("All durable quest-resolution predicates must pass for this rule to match.")]
    public List<QuestInteractionResolutionPredicate> resolutionPredicates =
        new List<QuestInteractionResolutionPredicate>();

    [Tooltip("All flag predicates must pass for this rule to match.")]
    public List<QuestInteractionMemoryFlagPredicate> flagPredicates = new List<QuestInteractionMemoryFlagPredicate>();

    [Tooltip("All profile world-flag predicates must pass. IsTrue requires the flag to exist and be true. IsFalse matches when the flag is absent or false.")]
    public List<QuestInteractionWorldFlagPredicate> worldFlagPredicates = new List<QuestInteractionWorldFlagPredicate>();

    [Tooltip("All bag-item predicates must pass. A missing or unread inventory fails the rule.")]
    public List<QuestInteractionBagItemPredicate> bagItemPredicates = new List<QuestInteractionBagItemPredicate>();

    [Tooltip("All counter predicates must pass for this rule to match.")]
    public List<QuestInteractionMemoryCounterPredicate> counterPredicates = new List<QuestInteractionMemoryCounterPredicate>();

    [Tooltip("Optional time-of-day predicate. Leave empty to ignore time. When configured, the rule only matches if the current time also passes.")]
    public TimeOfDayPredicate timeOfDay = new TimeOfDayPredicate();

    [Tooltip("Dialogue to play when this rule matches. Leave empty for no dialogue.")]
    public DialogueSequenceSO dialogue;

    [Tooltip("Temporarily enable DialogueSpeakerCameraBridge while this rule's dialogue plays. Use explicit per-line camera directives on the dialogue asset for target framing.")]
    public bool useDialogueCamera;

    [Tooltip("Trigger ID to fire when this rule matches. Leave empty for no trigger.")]
    public string triggerId = "";

    [Tooltip("Optional NPC interaction behavior to start before this rule fires its outputs.")]
    public QuestInteractionNpcBehaviorBinding npcBehavior = new QuestInteractionNpcBehaviorBinding();
}
