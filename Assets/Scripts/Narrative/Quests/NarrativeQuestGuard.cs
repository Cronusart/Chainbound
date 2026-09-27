using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lifecycle mask used by narrative quest guards and interaction routers.
/// </summary>
[Flags]
public enum NarrativeQuestLifecycleMask
{
    None = 0,
    Unknown = 1 << 0,
    Locked = 1 << 1,
    Available = 1 << 2,
    Active = 1 << 3,
    Completed = 1 << 4,
    Failed = 1 << 5,
    All = Unknown | Locked | Available | Active | Completed | Failed
}

/// <summary>
/// How required quest clauses should be combined.
/// </summary>
public enum NarrativeQuestGuardMatchMode
{
    Any,
    All
}

/// <summary>
/// One quest clause within a serialized narrative guard.
/// </summary>
[Serializable]
public class NarrativeQuestGuardClause
{
    [Tooltip("Optional quest definition asset. If assigned, its questId is used.")]
    [SerializeField] private QuestDefinitionSO questDefinition;

    [Tooltip("Manual quest ID fallback when no quest definition asset is assigned.")]
    [SerializeField] private string questId = "";

    [Tooltip("Which lifecycle states satisfy this clause.")]
    [SerializeField] private NarrativeQuestLifecycleMask allowedLifecycleMask = NarrativeQuestLifecycleMask.Active;

    public QuestDefinitionSO QuestDefinition => questDefinition;
    public NarrativeQuestLifecycleMask AllowedLifecycleMask => allowedLifecycleMask;

    public string QuestId
    {
        get
        {
            if (questDefinition != null && !string.IsNullOrWhiteSpace(questDefinition.QuestId))
                return questDefinition.QuestId;

            return questId;
        }
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(QuestId);
}

/// <summary>
/// Shared quest guard data used by interactables, redirect zones, and scene gates.
/// </summary>
[Serializable]
public class NarrativeQuestGuard
{
    [Tooltip("How required quest clauses should be combined. Excluded clauses always work as NOT conditions.")]
    [SerializeField] private NarrativeQuestGuardMatchMode requiredMatchMode = NarrativeQuestGuardMatchMode.All;

    [Tooltip("Required quest clauses. Leave empty when only exclusion checks are needed.")]
    [SerializeField] private List<NarrativeQuestGuardClause> requiredClauses = new List<NarrativeQuestGuardClause>();

    [Tooltip("Excluded quest clauses. If any excluded clause matches, the guard fails.")]
    [SerializeField] private List<NarrativeQuestGuardClause> excludedClauses = new List<NarrativeQuestGuardClause>();

    public NarrativeQuestGuardMatchMode RequiredMatchMode => requiredMatchMode;
    public IReadOnlyList<NarrativeQuestGuardClause> RequiredClauses => requiredClauses;
    public IReadOnlyList<NarrativeQuestGuardClause> ExcludedClauses => excludedClauses;
}

/// <summary>
/// Result of evaluating a narrative quest guard.
/// </summary>
public readonly struct NarrativeQuestGuardResult
{
    public NarrativeQuestGuardResult(bool isConfigured, bool passes, string matchedQuestId, QuestLifecycleState matchedLifecycleState)
    {
        IsConfigured = isConfigured;
        Passes = passes;
        MatchedQuestId = matchedQuestId ?? "";
        MatchedLifecycleState = matchedLifecycleState;
    }

    public bool IsConfigured { get; }
    public bool Passes { get; }
    public string MatchedQuestId { get; }
    public QuestLifecycleState MatchedLifecycleState { get; }
}

/// <summary>
/// Shared runtime helpers for quest guard and lifecycle-mask evaluation.
/// </summary>
public static class NarrativeQuestGuardUtility
{
    public static bool HasConfiguredClauses(NarrativeQuestGuard guard)
    {
        if (guard == null)
            return false;

        return HasConfiguredClauses(guard.RequiredClauses) || HasConfiguredClauses(guard.ExcludedClauses);
    }

    public static bool ReferencesQuestId(NarrativeQuestGuard guard, string questId)
    {
        if (guard == null || string.IsNullOrWhiteSpace(questId))
            return false;

        return ReferencesQuestId(guard.RequiredClauses, questId)
            || ReferencesQuestId(guard.ExcludedClauses, questId);
    }

    public static NarrativeQuestGuardResult Evaluate(
        QuestService questService,
        NarrativeQuestGuard guard,
        bool emptyGuardPasses = true)
    {
        if (HasConfiguredClauses(guard))
            return EvaluateConfiguredGuard(questService, guard);

        return new NarrativeQuestGuardResult(false, emptyGuardPasses, "", QuestLifecycleState.Unknown);
    }

    public static bool MatchesLifecycleMask(QuestLifecycleState state, NarrativeQuestLifecycleMask mask)
    {
        return (mask & ToLifecycleMask(state)) != 0;
    }

    public static NarrativeQuestLifecycleMask ToLifecycleMask(QuestLifecycleState state)
    {
        switch (state)
        {
            case QuestLifecycleState.Locked:
                return NarrativeQuestLifecycleMask.Locked;
            case QuestLifecycleState.Available:
                return NarrativeQuestLifecycleMask.Available;
            case QuestLifecycleState.Active:
                return NarrativeQuestLifecycleMask.Active;
            case QuestLifecycleState.Completed:
                return NarrativeQuestLifecycleMask.Completed;
            case QuestLifecycleState.Failed:
                return NarrativeQuestLifecycleMask.Failed;
            case QuestLifecycleState.Unknown:
            default:
                return NarrativeQuestLifecycleMask.Unknown;
        }
    }

    private static NarrativeQuestGuardResult EvaluateConfiguredGuard(QuestService questService, NarrativeQuestGuard guard)
    {
        if (questService == null || !questService.IsHydrated)
            return new NarrativeQuestGuardResult(true, false, "", QuestLifecycleState.Unknown);

        bool hasRequired = HasConfiguredClauses(guard.RequiredClauses);
        bool requiredPasses = !hasRequired;
        string matchedQuestId = "";
        QuestLifecycleState matchedLifecycleState = QuestLifecycleState.Unknown;

        if (hasRequired)
        {
            bool anyMatched = false;
            bool allMatched = true;

            for (int i = 0; i < guard.RequiredClauses.Count; i++)
            {
                NarrativeQuestGuardClause clause = guard.RequiredClauses[i];
                if (clause == null || !clause.IsConfigured)
                    continue;

                bool clauseMatches = EvaluateClause(questService, clause, out string clauseQuestId, out QuestLifecycleState clauseState);
                anyMatched |= clauseMatches;
                allMatched &= clauseMatches;

                if (clauseMatches && string.IsNullOrWhiteSpace(matchedQuestId))
                {
                    matchedQuestId = clauseQuestId;
                    matchedLifecycleState = clauseState;
                }
            }

            requiredPasses = guard.RequiredMatchMode == NarrativeQuestGuardMatchMode.Any
                ? anyMatched
                : allMatched;
        }

        if (!requiredPasses)
            return new NarrativeQuestGuardResult(true, false, matchedQuestId, matchedLifecycleState);

        for (int i = 0; i < guard.ExcludedClauses.Count; i++)
        {
            NarrativeQuestGuardClause clause = guard.ExcludedClauses[i];
            if (clause == null || !clause.IsConfigured)
                continue;

            if (EvaluateClause(questService, clause, out string excludedQuestId, out QuestLifecycleState excludedState))
                return new NarrativeQuestGuardResult(true, false, excludedQuestId, excludedState);
        }

        return new NarrativeQuestGuardResult(true, true, matchedQuestId, matchedLifecycleState);
    }

    private static bool EvaluateClause(
        QuestService questService,
        NarrativeQuestGuardClause clause,
        out string questId,
        out QuestLifecycleState lifecycleState)
    {
        questId = clause != null ? clause.QuestId : "";
        lifecycleState = QuestLifecycleState.Unknown;

        if (questService == null || clause == null || string.IsNullOrWhiteSpace(questId))
            return false;

        lifecycleState = questService.GetQuestLifecycleState(questId);
        return MatchesLifecycleMask(lifecycleState, clause.AllowedLifecycleMask);
    }

    private static bool HasConfiguredClauses(IReadOnlyList<NarrativeQuestGuardClause> clauses)
    {
        if (clauses == null)
            return false;

        for (int i = 0; i < clauses.Count; i++)
        {
            if (clauses[i] != null && clauses[i].IsConfigured)
                return true;
        }

        return false;
    }

    private static bool ReferencesQuestId(IReadOnlyList<NarrativeQuestGuardClause> clauses, string questId)
    {
        if (clauses == null)
            return false;

        for (int i = 0; i < clauses.Count; i++)
        {
            NarrativeQuestGuardClause clause = clauses[i];
            if (clause == null || !clause.IsConfigured)
                continue;

            if (string.Equals(clause.QuestId, questId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
