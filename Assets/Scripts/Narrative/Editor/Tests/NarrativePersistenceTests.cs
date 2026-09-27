using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class NarrativePersistenceTests
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

    [Test]
    public void QuestSnapshot_RoundTripPreservesObjectivesAndExplicitResolutionContext()
    {
        QuestDefinitionSO definition = CreateResolvedQuestDefinition();
        QuestCatalogSO catalog = ScriptableObject.CreateInstance<QuestCatalogSO>();
        NarrativeTestUtility.SetPrivateField(catalog, "quests", new List<QuestDefinitionSO> { definition });
        cleanup.Add(definition);
        cleanup.Add(catalog);

        QuestService source = NarrativeTestUtility.CreateQuestService("QuestService_PersistenceSource");
        cleanup.Add(source.gameObject);
        source.StartQuest(definition.QuestId, definition);
        source.SetObjectiveProgress(definition.QuestId, "choose_starter", 1);

        Assert.That(source.CompleteQuest(
            definition.QuestId,
            "chainling_bound",
            new Dictionary<string, string> { ["starter_species_id"] = "kitsauto" }), Is.True);

        NarrativeStateSnapshot snapshot = source.ExportSnapshot("profile-a");
        Assert.That(snapshot.questStates, Has.Count.EqualTo(1));
        Assert.That(snapshot.questStates[0].objectiveProgress[0].currentProgress, Is.EqualTo(1));
        Assert.That(snapshot.questStates[0].resolution.resolutionId, Is.EqualTo("chainling_bound"));

        Object.DestroyImmediate(source.gameObject);

        QuestService restored = NarrativeTestUtility.CreateQuestService("QuestService_PersistenceRestored");
        cleanup.Add(restored.gameObject);
        restored.BeginProfileHydration();
        restored.RestoreSnapshot(snapshot.questStates, catalog);

        Assert.That(restored.IsHydrated, Is.True);
        Assert.That(restored.GetQuestLifecycleState(definition.QuestId), Is.EqualTo(QuestLifecycleState.Completed));
        Assert.That(restored.GetQuestState(definition.QuestId).objectives["choose_starter"].currentProgress, Is.EqualTo(1));
        Assert.That(restored.TryGetResolution(definition.QuestId, out QuestResolutionRecord resolution), Is.True);
        Assert.That(resolution.TryGetContextValue("starter_species_id", out string starter), Is.True);
        Assert.That(starter, Is.EqualTo("kitsauto"));
    }

    [Test]
    public void InMemoryRepository_IsIdempotentRevisionedTerminalAndProfileIsolated()
    {
        var repository = new InMemoryNarrativeStateRepository();
        var profileA = new NarrativeProfileIdentity { playerProfileId = "profile-a" };
        var profileB = new NarrativeProfileIdentity { playerProfileId = "profile-b" };
        var completed = new QuestStateRecord
        {
            questId = "chainbound_intro",
            lifecycle = QuestLifecycleState.Completed,
            resolution = QuestResolutionRecord.Create(
                "chainling_bound",
                new Dictionary<string, string> { ["starter_species_id"] = "validape" })
        };
        var mutation = new NarrativeStateMutation
        {
            mutationId = "mutation-complete-001",
            kind = NarrativeMutationKind.QuestState,
            expectedRevision = 0,
            questState = completed
        };

        NarrativeRepositoryResult first = repository.ApplyQuestStateAsync(profileA, mutation).GetAwaiter().GetResult();
        NarrativeRepositoryResult duplicate = repository.ApplyQuestStateAsync(profileA, mutation).GetAwaiter().GetResult();
        Assert.That(first.Success, Is.True);
        Assert.That(first.QuestState.revision, Is.EqualTo(1));
        Assert.That(duplicate.Success, Is.True);
        Assert.That(duplicate.QuestState.revision, Is.EqualTo(1));

        var regression = new NarrativeStateMutation
        {
            mutationId = "mutation-regress-002",
            expectedRevision = 1,
            kind = NarrativeMutationKind.QuestState,
            questState = new QuestStateRecord
            {
                questId = "chainbound_intro",
                lifecycle = QuestLifecycleState.Active
            }
        };
        NarrativeRepositoryResult rejected = repository.ApplyQuestStateAsync(profileA, regression).GetAwaiter().GetResult();
        Assert.That(rejected.Success, Is.False);
        Assert.That(rejected.Conflict, Is.True);

        NarrativeStateSnapshot snapshotA = repository.LoadSnapshotAsync(profileA).GetAwaiter().GetResult();
        NarrativeStateSnapshot snapshotB = repository.LoadSnapshotAsync(profileB).GetAwaiter().GetResult();
        Assert.That(snapshotA.questStates, Has.Count.EqualTo(1));
        Assert.That(snapshotB.questStates, Is.Empty);
    }

    [Test]
    public void SequenceCheckpoint_RoundTripPreservesResumeAndExecuteOnceState()
    {
        var repository = new InMemoryNarrativeStateRepository();
        var identity = new NarrativeProfileIdentity { playerProfileId = "profile-sequence" };
        var checkpoint = new SequenceCheckpointRecord
        {
            sequenceId = "chainbound_intro_sequence",
            status = SequenceCheckpointStatus.Active,
            checkpointId = "intro_after_choose_starter",
            checkpointLabel = "choosestarter",
            lastCompletedLabel = "choosestarter",
            checkpointStepIndex = 81,
            sceneId = "loom_lab",
            spawnId = "loom_lab_entry",
            sequenceVersion = 3,
            persistentVariables = new List<NarrativeStringValue>
            {
                new NarrativeStringValue("starter_species_id", "gilarime")
            },
            railFlagSnapshot = new List<NarrativeFlagValue>
            {
                new NarrativeFlagValue("intro.guardrail.house", false)
            },
            completedStepGuids = new List<string> { "step-guid-a", "step-guid-b" },
            playedGateKeys = new List<string> { "intro_gate" }
        };
        var mutation = new NarrativeStateMutation
        {
            mutationId = "checkpoint-roundtrip-001",
            kind = NarrativeMutationKind.SequenceCheckpoint,
            expectedRevision = 0,
            sequenceCheckpoint = checkpoint
        };

        NarrativeRepositoryResult result = repository.ApplySequenceCheckpointAsync(identity, mutation).GetAwaiter().GetResult();
        NarrativeStateSnapshot snapshot = repository.LoadSnapshotAsync(identity).GetAwaiter().GetResult();

        Assert.That(result.Success, Is.True);
        Assert.That(snapshot.sequenceCheckpoints, Has.Count.EqualTo(1));
        SequenceCheckpointRecord restored = snapshot.sequenceCheckpoints[0];
        Assert.That(restored.checkpointId, Is.EqualTo("intro_after_choose_starter"));
        Assert.That(restored.lastCompletedLabel, Is.EqualTo("choosestarter"));
        Assert.That(restored.sequenceVersion, Is.EqualTo(3));
        Assert.That(restored.persistentVariables[0].value, Is.EqualTo("gilarime"));
        Assert.That(restored.railFlagSnapshot, Has.Count.EqualTo(1));
        Assert.That(restored.railFlagSnapshot[0].key, Is.EqualTo("intro.guardrail.house"));
        Assert.That(restored.railFlagSnapshot[0].value, Is.False);
        Assert.That(restored.completedStepGuids, Is.EquivalentTo(new[] { "step-guid-a", "step-guid-b" }));
        Assert.That(restored.playedGateKeys, Is.EquivalentTo(new[] { "intro_gate" }));
    }

    [Test]
    public void ActiveQuest_RoundTripPreservesProgressWithoutReplayingOffer()
    {
        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition(
            "coffee_and_beer",
            NarrativeTestUtility.CreateObjective("fetch_coffee", 2));
        QuestCatalogSO catalog = ScriptableObject.CreateInstance<QuestCatalogSO>();
        NarrativeTestUtility.SetPrivateField(catalog, "quests", new List<QuestDefinitionSO> { definition });
        cleanup.Add(definition);
        cleanup.Add(catalog);

        QuestService source = NarrativeTestUtility.CreateQuestService("QuestService_ActiveSource");
        cleanup.Add(source.gameObject);
        source.SetQuestAvailable(definition.QuestId, definition);
        source.StartQuest(definition.QuestId, definition);
        source.SetObjectiveProgress(definition.QuestId, "fetch_coffee", 1);
        NarrativeStateSnapshot snapshot = source.ExportSnapshot("profile-active");
        Object.DestroyImmediate(source.gameObject);

        QuestService restored = NarrativeTestUtility.CreateQuestService("QuestService_ActiveRestored");
        cleanup.Add(restored.gameObject);
        restored.BeginProfileHydration();
        restored.RestoreSnapshot(snapshot.questStates, catalog);

        Assert.That(restored.GetQuestLifecycleState(definition.QuestId), Is.EqualTo(QuestLifecycleState.Active));
        Assert.That(restored.GetQuestState(definition.QuestId).objectives["fetch_coffee"].currentProgress, Is.EqualTo(1));
    }

    [Test]
    public void Resolver_CompletedLifecycleCanBranchOnPersistedResolutionContext()
    {
        QuestDefinitionSO definition = CreateResolvedQuestDefinition();
        QuestService service = NarrativeTestUtility.CreateQuestService("QuestService_ResolutionRule");
        DialogueSequenceSO dialogue = NarrativeTestUtility.CreateDialogue("kitsauto_resolution_dialogue");
        cleanup.Add(definition);
        cleanup.Add(service.gameObject);
        cleanup.Add(dialogue);

        service.StartQuest(definition.QuestId, definition);
        Assert.That(service.CompleteQuest(
            definition.QuestId,
            "chainling_bound",
            new Dictionary<string, string> { ["starter_species_id"] = "kitsauto" }), Is.True);

        var rule = new QuestInteractionRule
        {
            lifecycleScope = QuestInteractionRuleLifecycleScope.UseLifecycleMask,
            lifecycleMask = NarrativeQuestLifecycleMask.Completed,
            dialogue = dialogue,
            triggerId = "kitsauto_branch",
            resolutionPredicates = new List<QuestInteractionResolutionPredicate>
            {
                new QuestInteractionResolutionPredicate
                {
                    comparison = QuestInteractionResolutionComparison.ContextValueEquals,
                    contextKey = "starter_species_id",
                    contextValue = "kitsauto"
                }
            }
        };
        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(definition.QuestId, new[] { rule });
        cleanup.Add(profile);

        QuestInteractionResolver.QuestInteractionResolution result = QuestInteractionResolver.Resolve(
            service, null, null, null, null, profile);
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Dialogue, Is.SameAs(dialogue));
        Assert.That(result.TriggerId, Is.EqualTo("kitsauto_branch"));
    }

    [Test]
    public void Resolver_FailsClosedWhileQuestServiceIsHydrating()
    {
        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("hydration_guard_quest");
        QuestService service = NarrativeTestUtility.CreateQuestService("QuestService_HydrationGuard");
        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(definition.QuestId);
        cleanup.Add(definition);
        cleanup.Add(service.gameObject);
        cleanup.Add(profile);

        service.StartQuest(definition.QuestId, definition);
        service.BeginProfileHydration();
        QuestInteractionResolver.QuestInteractionResolution result = QuestInteractionResolver.Resolve(
            service, null, null, null, null, profile);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Warning, Does.Contain("hydrating"));
    }

    private static QuestDefinitionSO CreateResolvedQuestDefinition()
    {
        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition(
            "chainbound_intro",
            NarrativeTestUtility.CreateObjective("choose_starter"));
        NarrativeTestUtility.SetPrivateField(definition, "autoCompleteWhenObjectivesComplete", false);
        NarrativeTestUtility.SetPrivateField(definition, "defaultResolutionId", "chainling_bound");
        NarrativeTestUtility.SetPrivateField(definition, "resolutions", new List<QuestResolutionDefinition>
        {
            new QuestResolutionDefinition
            {
                resolutionId = "chainling_bound",
                allowedContextKeys = new List<string> { "starter_species_id" }
            }
        });
        return definition;
    }
}
