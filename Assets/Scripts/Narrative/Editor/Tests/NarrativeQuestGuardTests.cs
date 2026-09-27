using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class NarrativeQuestGuardTests
{
    private readonly List<Object> cleanup = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [TestCase(QuestLifecycleState.Unknown, NarrativeQuestLifecycleMask.Unknown)]
    [TestCase(QuestLifecycleState.Locked, NarrativeQuestLifecycleMask.Locked)]
    [TestCase(QuestLifecycleState.Available, NarrativeQuestLifecycleMask.Available)]
    [TestCase(QuestLifecycleState.Active, NarrativeQuestLifecycleMask.Active)]
    [TestCase(QuestLifecycleState.Completed, NarrativeQuestLifecycleMask.Completed)]
    [TestCase(QuestLifecycleState.Failed, NarrativeQuestLifecycleMask.Failed)]
    public void Evaluate_SupportsEachQuestLifecycleState(QuestLifecycleState lifecycleState, NarrativeQuestLifecycleMask lifecycleMask)
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("quest_alpha");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, lifecycleState);

        NarrativeQuestGuard guard = NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(definition.QuestId, lifecycleMask) });

        NarrativeQuestGuardResult result = NarrativeQuestGuardUtility.Evaluate(service, guard, emptyGuardPasses: false);

        Assert.That(result.IsConfigured, Is.True);
        Assert.That(result.Passes, Is.True);
        Assert.That(result.MatchedLifecycleState, Is.EqualTo(lifecycleState));
    }

    [Test]
    public void Evaluate_SupportsAnyAndAllRequiredClauses()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO mainQuest = NarrativeTestUtility.CreateQuestDefinition("main_quest");
        QuestDefinitionSO sideQuest = NarrativeTestUtility.CreateQuestDefinition("side_quest");
        cleanup.Add(mainQuest);
        cleanup.Add(sideQuest);

        NarrativeTestUtility.SetQuestLifecycle(service, mainQuest, QuestLifecycleState.Active);
        NarrativeTestUtility.SetQuestLifecycle(service, sideQuest, QuestLifecycleState.Completed);

        NarrativeQuestGuard anyGuard = NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.Any,
            new[]
            {
                NarrativeTestUtility.CreateClause(mainQuest.QuestId, NarrativeQuestLifecycleMask.Active),
                NarrativeTestUtility.CreateClause("missing_quest", NarrativeQuestLifecycleMask.Active)
            });

        NarrativeQuestGuard allGuard = NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[]
            {
                NarrativeTestUtility.CreateClause(mainQuest.QuestId, NarrativeQuestLifecycleMask.Active),
                NarrativeTestUtility.CreateClause(sideQuest.QuestId, NarrativeQuestLifecycleMask.Completed)
            });

        Assert.That(NarrativeQuestGuardUtility.Evaluate(service, anyGuard, emptyGuardPasses: false).Passes, Is.True);
        Assert.That(NarrativeQuestGuardUtility.Evaluate(service, allGuard, emptyGuardPasses: false).Passes, Is.True);
    }

    [Test]
    public void Evaluate_FailsWhenExcludedClauseMatches()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO mainQuest = NarrativeTestUtility.CreateQuestDefinition("main_quest");
        QuestDefinitionSO sideQuest = NarrativeTestUtility.CreateQuestDefinition("side_quest");
        cleanup.Add(mainQuest);
        cleanup.Add(sideQuest);

        NarrativeTestUtility.SetQuestLifecycle(service, mainQuest, QuestLifecycleState.Active);
        NarrativeTestUtility.SetQuestLifecycle(service, sideQuest, QuestLifecycleState.Completed);

        NarrativeQuestGuard guard = NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(mainQuest.QuestId, NarrativeQuestLifecycleMask.Active) },
            new[] { NarrativeTestUtility.CreateClause(sideQuest.QuestId, NarrativeQuestLifecycleMask.Completed) });

        NarrativeQuestGuardResult result = NarrativeQuestGuardUtility.Evaluate(service, guard, emptyGuardPasses: false);

        Assert.That(result.IsConfigured, Is.True);
        Assert.That(result.Passes, Is.False);
        Assert.That(result.MatchedQuestId, Is.EqualTo(sideQuest.QuestId));
    }

    [Test]
    public void Evaluate_EmptyGuardCanBeConfiguredToPass()
    {
        NarrativeQuestGuardResult result = NarrativeQuestGuardUtility.Evaluate(
            null,
            new NarrativeQuestGuard(),
            emptyGuardPasses: true);

        Assert.That(result.IsConfigured, Is.False);
        Assert.That(result.Passes, Is.True);
        Assert.That(result.MatchedQuestId, Is.Empty);
    }
}
