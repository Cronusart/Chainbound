using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum QuestResolutionContextBindingSource
{
    Literal,
    PersistentSequenceVariable
}

[Serializable]
public sealed class QuestResolutionContextBinding
{
    [Tooltip("Stable context key authored by the quest resolution definition.")]
    public string contextKey = "";

    public QuestResolutionContextBindingSource source = QuestResolutionContextBindingSource.PersistentSequenceVariable;

    [Tooltip("Literal value or persistent sequence variable key, depending on Source.")]
    public string value = "";

    [Tooltip("Fail the step if the resolved value is missing or empty.")]
    public bool required = true;
}

/// <summary>
/// Completes an active quest with a stable resolution and context gathered from
/// persistent sequence variables. The QuestService mutation is accepted by the
/// profile coordinator's local outbox before sequence execution advances.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_CompleteQuestWithResolution",
    menuName = "Chainbound/Narrative/Steps/Quests/Complete Quest With Resolution")]
public sealed class Step_CompleteQuestWithResolution : SequenceStep
{
    [SerializeField] private QuestDefinitionSO questDefinition;
    [SerializeField] private string questId = "";
    [SerializeField] private string resolutionId = "standard";
    [SerializeField] private List<QuestResolutionContextBinding> contextBindings =
        new List<QuestResolutionContextBinding>();

    private string ResolvedQuestId => questDefinition != null && !string.IsNullOrWhiteSpace(questDefinition.QuestId)
        ? questDefinition.QuestId
        : questId;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        QuestService service = QuestService.Instance;
        if (service == null || !service.IsHydrated)
        {
            onComplete?.Invoke(StepOutcome.Failed("Quest profile state is not hydrated."));
            yield break;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < contextBindings.Count; i++)
        {
            QuestResolutionContextBinding binding = contextBindings[i];
            if (binding == null || string.IsNullOrWhiteSpace(binding.contextKey))
                continue;

            string resolvedValue = binding.source == QuestResolutionContextBindingSource.Literal
                ? binding.value ?? ""
                : context.Variables.Get<string>(binding.value, "");
            if (binding.required && string.IsNullOrWhiteSpace(resolvedValue))
            {
                onComplete?.Invoke(StepOutcome.Failed(
                    $"Required quest resolution context '{binding.contextKey}' was missing from '{binding.value}'."));
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(resolvedValue))
                values[binding.contextKey.Trim()] = resolvedValue.Trim();
        }

        if (service.IsQuestComplete(ResolvedQuestId))
        {
            if (service.TryGetResolution(ResolvedQuestId, out QuestResolutionRecord existing) &&
                ResolutionMatches(existing, resolutionId, values))
            {
                onComplete?.Invoke(StepOutcome.Success(
                    $"Quest '{ResolvedQuestId}' was already completed with the requested resolution."));
                yield break;
            }

            onComplete?.Invoke(StepOutcome.Failed(
                $"Quest '{ResolvedQuestId}' is already completed with a different immutable resolution."));
            yield break;
        }

        if (!service.CompleteQuest(ResolvedQuestId, resolutionId, values))
        {
            onComplete?.Invoke(StepOutcome.Failed(
                $"Quest '{ResolvedQuestId}' could not complete with resolution '{resolutionId}'."));
            yield break;
        }

        onComplete?.Invoke(StepOutcome.Success(
            $"Quest '{ResolvedQuestId}' completed with resolution '{resolutionId}'."));
        yield return null;
    }

    private static bool ResolutionMatches(
        QuestResolutionRecord existing,
        string expectedResolutionId,
        IReadOnlyDictionary<string, string> expectedContext)
    {
        if (existing == null || !string.Equals(
                existing.resolutionId,
                expectedResolutionId,
                StringComparison.Ordinal))
            return false;

        if (expectedContext == null)
            return true;
        foreach (KeyValuePair<string, string> pair in expectedContext)
        {
            if (!existing.TryGetContextValue(pair.Key, out string value) ||
                !string.Equals(value, pair.Value, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public override bool Validate()
    {
        bool valid = base.Validate();
        if (string.IsNullOrWhiteSpace(ResolvedQuestId) || string.IsNullOrWhiteSpace(resolutionId))
            return false;

        if (questDefinition != null)
        {
            var keys = new List<string>();
            for (int i = 0; i < contextBindings.Count; i++)
            {
                if (contextBindings[i] != null && !string.IsNullOrWhiteSpace(contextBindings[i].contextKey))
                    keys.Add(contextBindings[i].contextKey);
            }

            if (!questDefinition.ValidateResolution(resolutionId, keys, out string error))
            {
                Debug.LogWarning($"[Step_CompleteQuestWithResolution] {error}", this);
                valid = false;
            }
        }

        return valid;
    }
}
