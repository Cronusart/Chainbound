using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class NarrativeTestUtility
{
    public static QuestService CreateQuestService(string objectName = "QuestService_Test")
    {
        GameObject gameObject = new GameObject(objectName);
        return gameObject.AddComponent<QuestService>();
    }

    public static QuestDefinitionSO CreateQuestDefinition(string questId, params QuestObjective[] objectives)
    {
        QuestDefinitionSO definition = ScriptableObject.CreateInstance<QuestDefinitionSO>();
        definition.name = $"{questId}_definition";
        SetPrivateField(definition, "questId", questId);
        SetPrivateField(definition, "questName", ObjectNames.NicifyVariableName(questId));
        SetPrivateField(definition, "objectives", new List<QuestObjective>(objectives ?? Array.Empty<QuestObjective>()));
        return definition;
    }

    public static QuestObjective CreateObjective(string objectiveId, int targetAmount = 1, int currentProgress = 0)
    {
        return new QuestObjective
        {
            objectiveId = objectiveId,
            targetAmount = targetAmount,
            currentProgress = currentProgress
        };
    }

    public static DialogueSequenceSO CreateDialogue(string assetName)
    {
        DialogueSequenceSO dialogue = ScriptableObject.CreateInstance<DialogueSequenceSO>();
        dialogue.name = assetName;
        return dialogue;
    }

    public static NarrativeQuestGuardClause CreateClause(string questId, NarrativeQuestLifecycleMask lifecycleMask)
    {
        NarrativeQuestGuardClause clause = new NarrativeQuestGuardClause();
        SetPrivateField(clause, "questId", questId);
        SetPrivateField(clause, "allowedLifecycleMask", lifecycleMask);
        return clause;
    }

    public static NarrativeQuestGuard CreateGuard(
        NarrativeQuestGuardMatchMode matchMode,
        IEnumerable<NarrativeQuestGuardClause> requiredClauses = null,
        IEnumerable<NarrativeQuestGuardClause> excludedClauses = null)
    {
        NarrativeQuestGuard guard = new NarrativeQuestGuard();
        SetPrivateField(guard, "requiredMatchMode", matchMode);
        SetPrivateField(guard, "requiredClauses", new List<NarrativeQuestGuardClause>(requiredClauses ?? Array.Empty<NarrativeQuestGuardClause>()));
        SetPrivateField(guard, "excludedClauses", new List<NarrativeQuestGuardClause>(excludedClauses ?? Array.Empty<NarrativeQuestGuardClause>()));
        return guard;
    }

    public static QuestInteractionProfileSO CreateProfile(
        string questId,
        IEnumerable<QuestInteractionRule> rules = null,
        DialogueSequenceSO inactiveDialogue = null,
        string inactiveTriggerId = "",
        DialogueSequenceSO activeFallbackDialogue = null,
        string activeFallbackTriggerId = "",
        QuestInteractionNpcBehaviorBinding inactiveNpcBehavior = null,
        QuestInteractionNpcBehaviorBinding activeFallbackNpcBehavior = null)
    {
        QuestInteractionProfileSO profile = ScriptableObject.CreateInstance<QuestInteractionProfileSO>();
        profile.name = $"{questId}_profile";
        SetPrivateField(profile, "questId", questId);
        SetPrivateField(profile, "rules", new List<QuestInteractionRule>(rules ?? Array.Empty<QuestInteractionRule>()));
        SetPrivateField(profile, "inactiveDialogue", inactiveDialogue);
        SetPrivateField(profile, "inactiveTriggerId", inactiveTriggerId);
        SetPrivateField(profile, "activeFallbackDialogue", activeFallbackDialogue);
        SetPrivateField(profile, "activeFallbackTriggerId", activeFallbackTriggerId);
        SetPrivateField(profile, "inactiveNpcBehavior", inactiveNpcBehavior ?? new QuestInteractionNpcBehaviorBinding());
        SetPrivateField(profile, "activeFallbackNpcBehavior", activeFallbackNpcBehavior ?? new QuestInteractionNpcBehaviorBinding());
        return profile;
    }

    public static QuestInteractionNpcBehaviorBinding CreateNpcBehaviorBinding(
        QuestInteractionNpcBehaviorMode mode,
        NPCInteractionBehaviorSO behaviorOverride = null)
    {
        return new QuestInteractionNpcBehaviorBinding
        {
            mode = mode,
            behaviorOverride = behaviorOverride
        };
    }

    public static QuestInteractionRouterSO.RouteSlot CreateRouteSlot(
        QuestInteractionProfileSO profile,
        NarrativeQuestLifecycleMask lifecycleMask,
        string label = "")
    {
        QuestInteractionRouterSO.RouteSlot slot = new QuestInteractionRouterSO.RouteSlot();
        SetPrivateField(slot, "label", label);
        SetPrivateField(slot, "profile", profile);
        SetPrivateField(slot, "allowedLifecycleMask", lifecycleMask);
        return slot;
    }

    public static QuestInteractionRouterSO CreateRouter(params QuestInteractionRouterSO.RouteSlot[] slots)
    {
        QuestInteractionRouterSO router = ScriptableObject.CreateInstance<QuestInteractionRouterSO>();
        router.name = "test_router";
        SetPrivateField(router, "slots", new List<QuestInteractionRouterSO.RouteSlot>(slots ?? Array.Empty<QuestInteractionRouterSO.RouteSlot>()));
        return router;
    }

    public static void SetQuestLifecycle(QuestService service, QuestDefinitionSO definition, QuestLifecycleState state)
    {
        string questId = definition.QuestId;
        switch (state)
        {
            case QuestLifecycleState.Locked:
                service.SetQuestLocked(questId, definition);
                break;
            case QuestLifecycleState.Available:
                service.SetQuestAvailable(questId, definition);
                break;
            case QuestLifecycleState.Active:
                service.StartQuest(questId, definition);
                break;
            case QuestLifecycleState.Completed:
                service.StartQuest(questId, definition);
                service.CompleteQuest(questId);
                break;
            case QuestLifecycleState.Failed:
                service.StartQuest(questId, definition);
                service.FailQuest(questId);
                break;
            case QuestLifecycleState.Unknown:
            default:
                break;
        }
    }

    public static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        field.SetValue(target, value);
    }

    public static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        return (T)field.GetValue(target);
    }

    private static FieldInfo FindField(Type type, string fieldName)
    {
        while (type != null)
        {
            FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null)
                return field;

            type = type.BaseType;
        }

        throw new MissingFieldException($"Field '{fieldName}' was not found.");
    }
}
