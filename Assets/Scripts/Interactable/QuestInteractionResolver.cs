using System;
using System.Collections.Generic;
using Chainbound.Combat;
using UnityEngine;

/// <summary>
/// Shared resolver for quest-driven interaction outputs.
/// Keeps runtime override, router, and default-profile evaluation consistent.
/// </summary>
public static class QuestInteractionResolver
{
    public readonly struct QuestInteractionResolution
    {
        public QuestInteractionResolution(
            bool isValid,
            DialogueSequenceSO dialogue,
            string triggerId,
            string contextLabel,
            string warning,
            NPCInteractionBehaviorSO interactionBehavior,
            QuestInteractionProfileSO selectedProfile,
            string questId,
            QuestLifecycleState lifecycleState,
            bool useDialogueCamera = false)
        {
            IsValid = isValid;
            Dialogue = dialogue;
            TriggerId = triggerId ?? "";
            ContextLabel = contextLabel ?? "";
            Warning = warning ?? "";
            InteractionBehavior = interactionBehavior;
            SelectedProfile = selectedProfile;
            QuestId = questId ?? "";
            LifecycleState = lifecycleState;
            UseDialogueCamera = useDialogueCamera;
        }

        public bool IsValid { get; }
        public DialogueSequenceSO Dialogue { get; }
        public string TriggerId { get; }
        public string ContextLabel { get; }
        public string Warning { get; }
        public NPCInteractionBehaviorSO InteractionBehavior { get; }
        public QuestInteractionProfileSO SelectedProfile { get; }
        public string QuestId { get; }
        public QuestLifecycleState LifecycleState { get; }
        public bool UseDialogueCamera { get; }
    }

    public static QuestInteractionResolution Resolve(
        QuestService questService,
        NPCMemoryComponent npcMemory,
        ITimeOfDayService timeOfDayService,
        QuestInteractionProfileSO runtimeOverrideProfile,
        QuestInteractionRouterSO interactionRouter,
        QuestInteractionProfileSO defaultProfile,
        NPCInteractionBehaviorSO defaultNpcInteractionBehavior = null)
    {
        if (questService == null)
            return Invalid("QuestService was not available while resolving a quest interaction.");

        if (!questService.IsHydrated)
            return Invalid("Narrative profile state is still hydrating; interaction deferred.");

        if (runtimeOverrideProfile != null)
        {
            return ResolveProfile(
                questService,
                npcMemory,
                timeOfDayService,
                runtimeOverrideProfile,
                $"runtime override profile '{runtimeOverrideProfile.name}'",
                defaultNpcInteractionBehavior);
        }

        if (TryResolveRouterProfile(questService, interactionRouter, out QuestInteractionProfileSO routedProfile, out string routerContext))
            return ResolveProfile(questService, npcMemory, timeOfDayService, routedProfile, routerContext, defaultNpcInteractionBehavior);

        if (defaultProfile != null)
        {
            return ResolveProfile(
                questService,
                npcMemory,
                timeOfDayService,
                defaultProfile,
                $"default profile '{defaultProfile.name}'",
                defaultNpcInteractionBehavior);
        }

        return Invalid("No runtime override, router match, or default profile is configured.");
    }

    private static QuestInteractionResolution ResolveProfile(
        QuestService questService,
        NPCMemoryComponent npcMemory,
        ITimeOfDayService timeOfDayService,
        QuestInteractionProfileSO profile,
        string contextLabel,
        NPCInteractionBehaviorSO defaultNpcInteractionBehavior)
    {
        return ResolveProfileSource(
            questService,
            npcMemory,
            timeOfDayService,
            defaultNpcInteractionBehavior,
            profile != null ? profile.QuestId : "",
            profile != null ? profile.Rules : null,
            profile != null ? profile.InactiveDialogue : null,
            profile != null ? profile.InactiveTriggerId : "",
            profile != null ? profile.InactiveNpcBehavior : null,
            profile != null ? profile.ActiveFallbackDialogue : null,
            profile != null ? profile.ActiveFallbackTriggerId : "",
            profile != null ? profile.ActiveFallbackNpcBehavior : null,
            contextLabel,
            profile);
    }

    private static QuestInteractionResolution ResolveProfileSource(
        QuestService questService,
        NPCMemoryComponent npcMemory,
        ITimeOfDayService timeOfDayService,
        NPCInteractionBehaviorSO defaultNpcInteractionBehavior,
        string resolvedQuestId,
        IReadOnlyList<QuestInteractionRule> rules,
        DialogueSequenceSO inactiveDialogue,
        string inactiveTriggerId,
        QuestInteractionNpcBehaviorBinding inactiveNpcBehavior,
        DialogueSequenceSO activeFallbackDialogue,
        string activeFallbackTriggerId,
        QuestInteractionNpcBehaviorBinding activeFallbackNpcBehavior,
        string contextLabel,
        QuestInteractionProfileSO profile)
    {
        if (string.IsNullOrWhiteSpace(resolvedQuestId))
            return Invalid($"{contextLabel} has no questId configured.");

        if (questService == null)
            return Invalid($"QuestService not found while evaluating {contextLabel}.");

        QuestLifecycleState lifecycleState = questService.GetQuestLifecycleState(resolvedQuestId);
        string warning = "";
        if (rules != null)
        {
            for (int i = 0; i < rules.Count; i++)
            {
                QuestInteractionRule rule = rules[i];
                if (RuleMatches(
                        rule,
                        resolvedQuestId,
                        lifecycleState,
                        questService,
                        npcMemory,
                        timeOfDayService,
                        contextLabel,
                        i,
                        out string ruleWarning))
                {
                    string resolutionWarning = string.IsNullOrWhiteSpace(warning) ? ruleWarning : warning;
                    NPCInteractionBehaviorSO interactionBehavior = ResolveNpcBehavior(
                        rule.npcBehavior,
                        defaultNpcInteractionBehavior,
                        BuildRuleLabel(rule, i, contextLabel),
                        ref resolutionWarning);

                    return new QuestInteractionResolution(
                        true,
                        rule.dialogue,
                        rule.triggerId,
                        BuildRuleLabel(rule, i, contextLabel),
                        resolutionWarning,
                        interactionBehavior,
                        profile,
                        resolvedQuestId,
                        lifecycleState,
                        rule.useDialogueCamera);
                }

                if (string.IsNullOrWhiteSpace(warning) && !string.IsNullOrWhiteSpace(ruleWarning))
                    warning = ruleWarning;
            }
        }

        if (lifecycleState != QuestLifecycleState.Active)
        {
            string inactiveWarning = warning;
            NPCInteractionBehaviorSO interactionBehavior = ResolveNpcBehavior(
                inactiveNpcBehavior,
                defaultNpcInteractionBehavior,
                $"{contextLabel} quest {lifecycleState}",
                ref inactiveWarning);

            return new QuestInteractionResolution(
                true,
                inactiveDialogue,
                inactiveTriggerId,
                $"{contextLabel} quest {lifecycleState}",
                inactiveWarning,
                interactionBehavior,
                profile,
                resolvedQuestId,
                lifecycleState);
        }

        NPCInteractionBehaviorSO activeFallbackBehavior = ResolveNpcBehavior(
            activeFallbackNpcBehavior,
            defaultNpcInteractionBehavior,
            $"{contextLabel} active fallback",
            ref warning);

        return new QuestInteractionResolution(
            true,
            activeFallbackDialogue,
            activeFallbackTriggerId,
            $"{contextLabel} active fallback",
            warning,
            activeFallbackBehavior,
            profile,
            resolvedQuestId,
            lifecycleState);
    }

    private static bool TryResolveRouterProfile(
        QuestService questService,
        QuestInteractionRouterSO interactionRouter,
        out QuestInteractionProfileSO profile,
        out string contextLabel)
    {
        profile = null;
        contextLabel = "";

        if (questService == null || interactionRouter == null)
            return false;

        IReadOnlyList<QuestInteractionRouterSO.RouteSlot> slots = interactionRouter.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            QuestInteractionRouterSO.RouteSlot slot = slots[i];
            if (slot == null || slot.Profile == null || string.IsNullOrWhiteSpace(slot.Profile.QuestId))
                continue;

            QuestLifecycleState lifecycleState = questService.GetQuestLifecycleState(slot.Profile.QuestId);
            if (!NarrativeQuestGuardUtility.MatchesLifecycleMask(lifecycleState, slot.AllowedLifecycleMask))
                continue;

            profile = slot.Profile;
            contextLabel = string.IsNullOrWhiteSpace(slot.Label)
                ? $"router '{interactionRouter.name}' slot {i} profile '{slot.Profile.name}'"
                : $"router '{interactionRouter.name}' slot {i} ('{slot.Label}') profile '{slot.Profile.name}'";
            return true;
        }

        return false;
    }

    private static bool RuleMatches(
        QuestInteractionRule rule,
        string resolvedQuestId,
        QuestLifecycleState lifecycleState,
        QuestService questService,
        NPCMemoryComponent npcMemory,
        ITimeOfDayService timeOfDayService,
        string contextLabel,
        int ruleIndex,
        out string warning)
    {
        warning = "";

        if (rule == null)
            return false;

        if (!RuleLifecycleMatches(rule, lifecycleState))
            return false;

        if (!ObjectiveMatches(rule, resolvedQuestId, questService))
            return false;

        if (!ResolutionPredicatesMatch(rule.resolutionPredicates, resolvedQuestId, questService))
            return false;

        if (!SequenceProgressPredicatesMatch(
                rule.sequenceProgressPredicates,
                contextLabel,
                ruleIndex,
                out warning))
        {
            return false;
        }

        return FlagPredicatesMatch(rule.flagPredicates, npcMemory)
            && WorldFlagPredicatesMatch(rule.worldFlagPredicates)
            && BagItemPredicatesMatch(rule.bagItemPredicates)
            && CounterPredicatesMatch(rule.counterPredicates, npcMemory)
            && TimePredicateMatches(rule, timeOfDayService, contextLabel, ruleIndex, out warning);
    }

    private static bool RuleLifecycleMatches(QuestInteractionRule rule, QuestLifecycleState lifecycleState)
    {
        if (rule == null)
            return false;
        if (rule.lifecycleScope == QuestInteractionRuleLifecycleScope.ActiveOnly)
            return lifecycleState == QuestLifecycleState.Active;
        return NarrativeQuestGuardUtility.MatchesLifecycleMask(lifecycleState, rule.lifecycleMask);
    }

    private static bool ResolutionPredicatesMatch(
        IReadOnlyList<QuestInteractionResolutionPredicate> predicates,
        string profileQuestId,
        QuestService questService)
    {
        if (predicates == null || predicates.Count == 0)
            return true;
        if (questService == null || !questService.IsHydrated)
            return false;

        for (int i = 0; i < predicates.Count; i++)
        {
            QuestInteractionResolutionPredicate predicate = predicates[i];
            if (predicate == null)
                continue;

            string questId = predicate.ResolveQuestId(profileQuestId);
            QuestResolutionRecord resolution = null;
            bool hasResolution = !string.IsNullOrWhiteSpace(questId) &&
                questService.TryGetResolution(questId, out resolution);

            switch (predicate.comparison)
            {
                case QuestInteractionResolutionComparison.Exists:
                    if (!hasResolution) return false;
                    break;
                case QuestInteractionResolutionComparison.Missing:
                    if (hasResolution) return false;
                    break;
                case QuestInteractionResolutionComparison.ResolutionIdEquals:
                    if (!hasResolution || !string.Equals(
                            resolution.resolutionId,
                            predicate.resolutionId,
                            StringComparison.Ordinal))
                        return false;
                    break;
                case QuestInteractionResolutionComparison.ContextValueEquals:
                    if (!hasResolution || !resolution.TryGetContextValue(predicate.contextKey, out string value) ||
                        !string.Equals(value, predicate.contextValue, StringComparison.Ordinal))
                        return false;
                    break;
                case QuestInteractionResolutionComparison.ContextKeyMissing:
                    if (!hasResolution || resolution.TryGetContextValue(predicate.contextKey, out _))
                        return false;
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool SequenceProgressPredicatesMatch(
        IReadOnlyList<QuestInteractionSequenceProgressPredicate> predicates,
        string contextLabel,
        int ruleIndex,
        out string warning)
    {
        warning = "";

        if (predicates == null || predicates.Count == 0)
            return true;

        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null || !runner.IsRunning || runner.CurrentSequence == null)
        {
            warning = $"No running SequenceRunner was available while evaluating {contextLabel} rule {ruleIndex}. The sequence-aware rule failed closed.";
            return false;
        }

        for (int i = 0; i < predicates.Count; i++)
        {
            QuestInteractionSequenceProgressPredicate predicate = predicates[i];
            if (QuestInteractionSequenceProgressUtility.Matches(
                    runner.CurrentSequence,
                    runner.CurrentStepIndex,
                    predicate,
                    out string predicateWarning))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(warning) && !string.IsNullOrWhiteSpace(predicateWarning))
                warning = $"{contextLabel} rule {ruleIndex}: {predicateWarning}";

            return false;
        }

        return true;
    }

    private static bool ObjectiveMatches(
        QuestInteractionRule rule,
        string resolvedQuestId,
        QuestService questService)
    {
        if (rule.objectiveComparison == QuestInteractionObjectiveComparison.None)
            return true;

        if (string.IsNullOrWhiteSpace(rule.objectiveId))
            return false;

        if (questService == null || !questService.TryGetObjective(resolvedQuestId, rule.objectiveId, out QuestObjective objective) || objective == null)
            return false;

        switch (rule.objectiveComparison)
        {
            case QuestInteractionObjectiveComparison.Complete:
                return objective.IsComplete;
            case QuestInteractionObjectiveComparison.NotComplete:
                return !objective.IsComplete;
            case QuestInteractionObjectiveComparison.AtLeast:
                return objective.currentProgress >= rule.objectiveValue;
            case QuestInteractionObjectiveComparison.AtMost:
                return objective.currentProgress <= rule.objectiveValue;
            case QuestInteractionObjectiveComparison.Exactly:
                return objective.currentProgress == rule.objectiveValue;
            default:
                return true;
        }
    }

    private static bool FlagPredicatesMatch(
        IReadOnlyList<QuestInteractionMemoryFlagPredicate> predicates,
        NPCMemoryComponent npcMemory)
    {
        if (predicates == null || predicates.Count == 0)
            return true;

        if (npcMemory == null)
            return false;

        for (int i = 0; i < predicates.Count; i++)
        {
            if (!FlagPredicateMatches(predicates[i], npcMemory))
                return false;
        }

        return true;
    }

    private static bool FlagPredicateMatches(
        QuestInteractionMemoryFlagPredicate predicate,
        NPCMemoryComponent npcMemory)
    {
        if (predicate == null)
            return true;

        if (string.IsNullOrWhiteSpace(predicate.key))
            return false;

        bool exists = npcMemory.HasFlag(predicate.key);
        switch (predicate.comparison)
        {
            case QuestInteractionMemoryFlagComparison.Exists:
                return exists;
            case QuestInteractionMemoryFlagComparison.Missing:
                return !exists;
            case QuestInteractionMemoryFlagComparison.IsTrue:
                return exists && npcMemory.GetFlag(predicate.key);
            case QuestInteractionMemoryFlagComparison.IsFalse:
                return exists && !npcMemory.GetFlag(predicate.key);
            default:
                return false;
        }
    }

    private static bool WorldFlagPredicatesMatch(IReadOnlyList<QuestInteractionWorldFlagPredicate> predicates)
    {
        if (predicates == null || predicates.Count == 0)
            return true;

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance;
        if (flags == null)
            return false;

        for (int i = 0; i < predicates.Count; i++)
        {
            if (!WorldFlagPredicateMatches(predicates[i], flags))
                return false;
        }

        return true;
    }

    private static bool WorldFlagPredicateMatches(
        QuestInteractionWorldFlagPredicate predicate,
        ProfileWorldFlagStateService flags)
    {
        if (predicate == null)
            return true;

        if (string.IsNullOrWhiteSpace(predicate.key) || flags == null)
            return false;

        bool exists = flags.HasFlag(predicate.key);
        bool value = exists && flags.GetFlag(predicate.key);
        switch (predicate.comparison)
        {
            case QuestInteractionMemoryFlagComparison.Exists:
                return exists;
            case QuestInteractionMemoryFlagComparison.Missing:
                return !exists;
            case QuestInteractionMemoryFlagComparison.IsTrue:
                return value;
            case QuestInteractionMemoryFlagComparison.IsFalse:
                return !value;
            default:
                return false;
        }
    }

    private static bool BagItemPredicatesMatch(IReadOnlyList<QuestInteractionBagItemPredicate> predicates)
    {
        if (predicates == null || predicates.Count == 0)
            return true;

        for (int i = 0; i < predicates.Count; i++)
        {
            if (!BagItemPredicateMatches(predicates[i]))
                return false;
        }

        return true;
    }

    private static bool BagItemPredicateMatches(QuestInteractionBagItemPredicate predicate)
    {
        if (predicate == null)
            return true;

        if (string.IsNullOrWhiteSpace(predicate.itemId))
            return false;

        if (!ProfileBagQuantity.TryGetQuantity(predicate.itemId, out int quantity))
            return false;

        return quantity >= Mathf.Max(1, predicate.minimumQuantity);
    }

    private static bool CounterPredicatesMatch(
        IReadOnlyList<QuestInteractionMemoryCounterPredicate> predicates,
        NPCMemoryComponent npcMemory)
    {
        if (predicates == null || predicates.Count == 0)
            return true;

        if (npcMemory == null)
            return false;

        for (int i = 0; i < predicates.Count; i++)
        {
            if (!CounterPredicateMatches(predicates[i], npcMemory))
                return false;
        }

        return true;
    }

    private static bool CounterPredicateMatches(
        QuestInteractionMemoryCounterPredicate predicate,
        NPCMemoryComponent npcMemory)
    {
        if (predicate == null)
            return true;

        if (string.IsNullOrWhiteSpace(predicate.key))
            return false;

        bool exists = npcMemory.HasCounter(predicate.key);
        switch (predicate.comparison)
        {
            case QuestInteractionMemoryCounterComparison.Exists:
                return exists;
            case QuestInteractionMemoryCounterComparison.Missing:
                return !exists;
        }

        if (!exists)
            return false;

        int value = npcMemory.GetCounter(predicate.key);
        switch (predicate.comparison)
        {
            case QuestInteractionMemoryCounterComparison.AtLeast:
                return value >= predicate.value;
            case QuestInteractionMemoryCounterComparison.AtMost:
                return value <= predicate.value;
            case QuestInteractionMemoryCounterComparison.Exactly:
                return value == predicate.value;
            default:
                return false;
        }
    }

    private static bool TimePredicateMatches(
        QuestInteractionRule rule,
        ITimeOfDayService timeOfDayService,
        string contextLabel,
        int ruleIndex,
        out string warning)
    {
        warning = "";

        if (rule == null || !TimeOfDayPredicateUtility.HasConfiguredConstraints(rule.timeOfDay))
            return true;

        if (timeOfDayService == null)
        {
            warning = $"No ITimeOfDayService was available while evaluating {BuildRuleLabel(rule, ruleIndex, contextLabel)}. The time-aware rule failed closed.";
            return false;
        }

        return TimeOfDayPredicateUtility.Matches(rule.timeOfDay, timeOfDayService);
    }

    private static string BuildRuleLabel(
        QuestInteractionRule rule,
        int index,
        string contextLabel)
    {
        if (!string.IsNullOrWhiteSpace(rule.label))
            return $"{contextLabel} rule {index} ('{rule.label}')";

        if (!string.IsNullOrWhiteSpace(rule.objectiveId))
            return $"{contextLabel} rule {index} (objective '{rule.objectiveId}')";

        if (rule.sequenceProgressPredicates != null && rule.sequenceProgressPredicates.Count > 0)
        {
            QuestInteractionSequenceProgressPredicate predicate = rule.sequenceProgressPredicates[0];
            if (predicate != null && !string.IsNullOrWhiteSpace(predicate.label))
                return $"{contextLabel} rule {index} (sequence label '{predicate.label}')";
        }

        return $"{contextLabel} rule {index}";
    }

    private static NPCInteractionBehaviorSO ResolveNpcBehavior(
        QuestInteractionNpcBehaviorBinding binding,
        NPCInteractionBehaviorSO defaultNpcInteractionBehavior,
        string contextLabel,
        ref string warning)
    {
        if (binding == null)
            return null;

        NPCInteractionBehaviorSO interactionBehavior = binding.Resolve(defaultNpcInteractionBehavior, out string behaviorWarning);
        if (string.IsNullOrWhiteSpace(warning) && !string.IsNullOrWhiteSpace(behaviorWarning))
            warning = $"{contextLabel}: {behaviorWarning}";

        return interactionBehavior;
    }

    private static QuestInteractionResolution Invalid(string warning)
    {
        return new QuestInteractionResolution(
            false,
            null,
            "",
            "",
            warning,
            null,
            null,
            "",
            QuestLifecycleState.Unknown);
    }
}

/// <summary>
/// Evaluates sequence-label progress without coupling interaction content to
/// the runner's internal label-index dictionary.
/// </summary>
public static class QuestInteractionSequenceProgressUtility
{
    public static bool Matches(
        SequenceAsset sequence,
        int currentStepIndex,
        QuestInteractionSequenceProgressPredicate predicate,
        out string warning)
    {
        warning = "";

        if (predicate == null)
            return true;

        if (string.IsNullOrWhiteSpace(predicate.sequenceId))
        {
            warning = "Sequence progress predicate has no sequenceId.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(predicate.label))
        {
            warning = "Sequence progress predicate has no label.";
            return false;
        }

        if (sequence == null)
        {
            warning = $"Sequence '{predicate.sequenceId}' is not loaded.";
            return false;
        }

        if (!string.Equals(sequence.SequenceId, predicate.sequenceId, StringComparison.Ordinal))
            return false;

        int labelIndex = FindLabelIndex(sequence, predicate.label);
        if (labelIndex < 0)
        {
            warning = $"Sequence '{predicate.sequenceId}' does not contain label '{predicate.label}'.";
            return false;
        }

        bool labelCompleted = currentStepIndex > labelIndex;
        return predicate.comparison == QuestInteractionSequenceProgressComparison.BeforeLabel
            ? !labelCompleted
            : labelCompleted;
    }

    private static int FindLabelIndex(SequenceAsset sequence, string label)
    {
        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) is Step_Label labelStep
                && string.Equals(labelStep.GetLabelName(), label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
