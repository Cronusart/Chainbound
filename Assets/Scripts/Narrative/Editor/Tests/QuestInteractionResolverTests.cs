using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class QuestInteractionResolverTests
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
    public void Resolve_RuntimeOverrideProfileHasHighestPriority()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO overrideQuest = NarrativeTestUtility.CreateQuestDefinition("override_quest");
        QuestDefinitionSO routedQuest = NarrativeTestUtility.CreateQuestDefinition("routed_quest");
        QuestDefinitionSO defaultQuest = NarrativeTestUtility.CreateQuestDefinition("default_quest");
        cleanup.Add(overrideQuest);
        cleanup.Add(routedQuest);
        cleanup.Add(defaultQuest);

        NarrativeTestUtility.SetQuestLifecycle(service, overrideQuest, QuestLifecycleState.Completed);
        NarrativeTestUtility.SetQuestLifecycle(service, routedQuest, QuestLifecycleState.Active);
        NarrativeTestUtility.SetQuestLifecycle(service, defaultQuest, QuestLifecycleState.Active);

        DialogueSequenceSO overrideDialogue = NarrativeTestUtility.CreateDialogue("override_inactive");
        cleanup.Add(overrideDialogue);

        QuestInteractionProfileSO overrideProfile = NarrativeTestUtility.CreateProfile(
            overrideQuest.QuestId,
            inactiveDialogue: overrideDialogue,
            inactiveTriggerId: "override_inactive");
        QuestInteractionProfileSO routedProfile = NarrativeTestUtility.CreateProfile(
            routedQuest.QuestId,
            activeFallbackTriggerId: "router_active");
        QuestInteractionProfileSO defaultProfile = NarrativeTestUtility.CreateProfile(
            defaultQuest.QuestId,
            activeFallbackTriggerId: "default_active");
        cleanup.Add(overrideProfile);
        cleanup.Add(routedProfile);
        cleanup.Add(defaultProfile);

        QuestInteractionRouterSO router = NarrativeTestUtility.CreateRouter(
            NarrativeTestUtility.CreateRouteSlot(routedProfile, NarrativeQuestLifecycleMask.Active, "routed"));
        cleanup.Add(router);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            overrideProfile,
            router,
            defaultProfile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("override_inactive"));
        Assert.That(resolution.ContextLabel, Does.Contain("runtime override"));
    }

    [Test]
    public void Resolve_UsesFirstMatchingRouterSlot()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO firstQuest = NarrativeTestUtility.CreateQuestDefinition("first_quest");
        QuestDefinitionSO secondQuest = NarrativeTestUtility.CreateQuestDefinition("second_quest");
        cleanup.Add(firstQuest);
        cleanup.Add(secondQuest);

        NarrativeTestUtility.SetQuestLifecycle(service, firstQuest, QuestLifecycleState.Active);
        NarrativeTestUtility.SetQuestLifecycle(service, secondQuest, QuestLifecycleState.Active);

        QuestInteractionProfileSO firstProfile = NarrativeTestUtility.CreateProfile(firstQuest.QuestId, activeFallbackTriggerId: "first");
        QuestInteractionProfileSO secondProfile = NarrativeTestUtility.CreateProfile(secondQuest.QuestId, activeFallbackTriggerId: "second");
        cleanup.Add(firstProfile);
        cleanup.Add(secondProfile);

        QuestInteractionRouterSO router = NarrativeTestUtility.CreateRouter(
            NarrativeTestUtility.CreateRouteSlot(firstProfile, NarrativeQuestLifecycleMask.Active, "first"),
            NarrativeTestUtility.CreateRouteSlot(secondProfile, NarrativeQuestLifecycleMask.Active, "second"));
        cleanup.Add(router);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            router,
            null);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("first"));
        Assert.That(resolution.ContextLabel, Does.Contain("slot 0"));
    }

    [Test]
    public void Resolve_UsesOrderedRulesWhenQuestIsActive()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition(
            "objective_quest",
            NarrativeTestUtility.CreateObjective("talk_to_npc", targetAmount: 3, currentProgress: 0));
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);
        service.SetObjectiveProgress(definition.QuestId, "talk_to_npc", 1);

        DialogueSequenceSO ruleDialogue = NarrativeTestUtility.CreateDialogue("rule_dialogue");
        cleanup.Add(ruleDialogue);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Objective progress",
            objectiveId = "talk_to_npc",
            objectiveComparison = QuestInteractionObjectiveComparison.AtLeast,
            objectiveValue = 1,
            dialogue = ruleDialogue,
            triggerId = "rule_trigger"
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule },
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.Dialogue, Is.EqualTo(ruleDialogue));
        Assert.That(resolution.TriggerId, Is.EqualTo("rule_trigger"));
        Assert.That(resolution.ContextLabel, Does.Contain("rule"));
    }

    [Test]
    public void Resolve_UsesInactiveOutputWhenQuestIsNotActive()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("inactive_quest");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Completed);

        DialogueSequenceSO inactiveDialogue = NarrativeTestUtility.CreateDialogue("inactive_dialogue");
        cleanup.Add(inactiveDialogue);

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            inactiveDialogue: inactiveDialogue,
            inactiveTriggerId: "inactive_trigger",
            activeFallbackTriggerId: "active_trigger");
        cleanup.Add(profile);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.Dialogue, Is.EqualTo(inactiveDialogue));
        Assert.That(resolution.TriggerId, Is.EqualTo("inactive_trigger"));
        Assert.That(resolution.LifecycleState, Is.EqualTo(QuestLifecycleState.Completed));
    }

    [Test]
    public void Resolve_UsesNpcDefaultBehaviorWhenRuleRequestsIt()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("npc_behavior_default");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        NPCInteractionBehaviorSO defaultBehavior = ScriptableObject.CreateInstance<NPCInteractionBehaviorSO>();
        cleanup.Add(defaultBehavior);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Use NPC default",
            triggerId = "behavior_trigger",
            npcBehavior = NarrativeTestUtility.CreateNpcBehaviorBinding(QuestInteractionNpcBehaviorMode.UseNpcDefault)
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule });
        cleanup.Add(profile);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            null,
            profile,
            defaultBehavior);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.InteractionBehavior, Is.EqualTo(defaultBehavior));
    }

    [Test]
    public void Resolve_UsesOverrideBehaviorWhenInactiveOutputConfiguresIt()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("npc_behavior_override");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Completed);

        NPCInteractionBehaviorSO overrideBehavior = ScriptableObject.CreateInstance<NPCInteractionBehaviorSO>();
        cleanup.Add(overrideBehavior);

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            inactiveTriggerId: "inactive_trigger",
            inactiveNpcBehavior: NarrativeTestUtility.CreateNpcBehaviorBinding(
                QuestInteractionNpcBehaviorMode.Override,
                overrideBehavior));
        cleanup.Add(profile);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("inactive_trigger"));
        Assert.That(resolution.InteractionBehavior, Is.EqualTo(overrideBehavior));
    }

    [Test]
    public void Resolve_FallsBackToDefaultProfileWhenRouterHasNoMatch()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO routerQuest = NarrativeTestUtility.CreateQuestDefinition("router_quest");
        QuestDefinitionSO defaultQuest = NarrativeTestUtility.CreateQuestDefinition("default_quest");
        cleanup.Add(routerQuest);
        cleanup.Add(defaultQuest);

        NarrativeTestUtility.SetQuestLifecycle(service, routerQuest, QuestLifecycleState.Completed);
        NarrativeTestUtility.SetQuestLifecycle(service, defaultQuest, QuestLifecycleState.Active);

        QuestInteractionProfileSO routerProfile = NarrativeTestUtility.CreateProfile(routerQuest.QuestId, activeFallbackTriggerId: "router_active");
        QuestInteractionProfileSO defaultProfile = NarrativeTestUtility.CreateProfile(defaultQuest.QuestId, activeFallbackTriggerId: "default_active");
        cleanup.Add(routerProfile);
        cleanup.Add(defaultProfile);

        QuestInteractionRouterSO router = NarrativeTestUtility.CreateRouter(
            NarrativeTestUtility.CreateRouteSlot(routerProfile, NarrativeQuestLifecycleMask.Active, "active only"));
        cleanup.Add(router);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            router,
            defaultProfile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("default_active"));
        Assert.That(resolution.ContextLabel, Does.Contain("default profile"));
    }

    [Test]
    public void Resolve_TimeSegmentPredicateMatchesCurrentSegment()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("segment_quest");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Day greeting",
            triggerId = "day_trigger",
            timeOfDay = new TimeOfDayPredicate
            {
                allowedSegments = TimeSegmentMask.Day
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule },
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        TestTimeOfDayService timeService = CreateTimeService(0.5f, TimeSegment.Day);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            timeService,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("day_trigger"));
    }

    [Test]
    public void Resolve_TimeRangePredicateMatchesNormalizedWindow()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("range_quest");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Morning greeting",
            triggerId = "morning_trigger",
            timeOfDay = new TimeOfDayPredicate
            {
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.20f,
                maxNormalizedTime = 0.35f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule },
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        TestTimeOfDayService timeService = CreateTimeService(0.25f, TimeSegment.Dawn);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            timeService,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("morning_trigger"));
    }

    [Test]
    public void Resolve_TimeSegmentAndRangeUseConjunction()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("conjunction_quest");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        QuestInteractionRule impossibleDayWindow = new QuestInteractionRule
        {
            label = "Wrong day window",
            triggerId = "wrong_window",
            timeOfDay = new TimeOfDayPredicate
            {
                allowedSegments = TimeSegmentMask.Day,
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.80f,
                maxNormalizedTime = 0.90f
            }
        };

        QuestInteractionRule matchingDayWindow = new QuestInteractionRule
        {
            label = "Right day window",
            triggerId = "right_window",
            timeOfDay = new TimeOfDayPredicate
            {
                allowedSegments = TimeSegmentMask.Day,
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.45f,
                maxNormalizedTime = 0.55f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { impossibleDayWindow, matchingDayWindow },
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        TestTimeOfDayService timeService = CreateTimeService(0.5f, TimeSegment.Day);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            timeService,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("right_window"));
        Assert.That(resolution.ContextLabel, Does.Contain("Right day window"));
    }

    [Test]
    public void Resolve_TimeRangeSupportsMidnightWrapAround()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("wrap_quest");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Late night",
            triggerId = "late_night_trigger",
            timeOfDay = new TimeOfDayPredicate
            {
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.85f,
                maxNormalizedTime = 0.10f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule },
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        TestTimeOfDayService timeService = CreateTimeService(0.95f, TimeSegment.Night);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            timeService,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("late_night_trigger"));
    }

    [Test]
    public void Resolve_TimeAwareRuleFailsClosedWhenTimeServiceIsMissing()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("missing_time_quest");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Morning greeting",
            triggerId = "morning_trigger",
            timeOfDay = new TimeOfDayPredicate
            {
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.20f,
                maxNormalizedTime = 0.35f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule },
            activeFallbackTriggerId: "neutral_trigger");
        cleanup.Add(profile);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            null,
            null,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("neutral_trigger"));
        Assert.That(resolution.Warning, Does.Contain("No ITimeOfDayService"));
    }

    [Test]
    public void Resolve_TimePredicateComposesWithObjectiveAndMemoryPredicates()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        GameObject npcObject = new GameObject("NPCMemory_Test");
        cleanup.Add(npcObject);
        NPCMemoryComponent npcMemory = npcObject.AddComponent<NPCMemoryComponent>();
        npcMemory.SetFlag("met_player", true);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition(
            "composed_quest",
            NarrativeTestUtility.CreateObjective("talk_to_npc", targetAmount: 3, currentProgress: 0));
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);
        service.SetObjectiveProgress(definition.QuestId, "talk_to_npc", 2);

        QuestInteractionRule rule = new QuestInteractionRule
        {
            label = "Composed rule",
            objectiveId = "talk_to_npc",
            objectiveComparison = QuestInteractionObjectiveComparison.AtLeast,
            objectiveValue = 2,
            flagPredicates = new List<QuestInteractionMemoryFlagPredicate>
            {
                new QuestInteractionMemoryFlagPredicate
                {
                    key = "met_player",
                    comparison = QuestInteractionMemoryFlagComparison.IsTrue
                }
            },
            triggerId = "composed_trigger",
            timeOfDay = new TimeOfDayPredicate
            {
                allowedSegments = TimeSegmentMask.Day,
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.45f,
                maxNormalizedTime = 0.55f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { rule },
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        TestTimeOfDayService timeService = CreateTimeService(0.5f, TimeSegment.Day);

        QuestInteractionResolver.QuestInteractionResolution resolution = QuestInteractionResolver.Resolve(
            service,
            npcMemory,
            timeService,
            null,
            null,
            profile);

        Assert.That(resolution.IsValid, Is.True);
        Assert.That(resolution.TriggerId, Is.EqualTo("composed_trigger"));
    }

    [Test]
    public void Resolve_WorldFlagIsFalseWhenAbsentAndIsTrueWhenSet()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition(
            "coffee_flags",
            NarrativeTestUtility.CreateObjective("talk", targetAmount: 1, currentProgress: 0));
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        DialogueSequenceSO beerDialogue = NarrativeTestUtility.CreateDialogue("beer");
        DialogueSequenceSO fallbackDialogue = NarrativeTestUtility.CreateDialogue("fallback");
        cleanup.Add(beerDialogue);
        cleanup.Add(fallbackDialogue);

        QuestInteractionRule beerRule = new QuestInteractionRule
        {
            label = "beer",
            dialogue = beerDialogue,
            triggerId = "trigger_beer",
            worldFlagPredicates = new List<QuestInteractionWorldFlagPredicate>
            {
                new QuestInteractionWorldFlagPredicate
                {
                    key = "world.coffee.zeb_departed",
                    comparison = QuestInteractionMemoryFlagComparison.IsTrue
                },
                new QuestInteractionWorldFlagPredicate
                {
                    key = "world.coffee.cold_beer_granted",
                    comparison = QuestInteractionMemoryFlagComparison.IsFalse
                }
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            new[] { beerRule },
            activeFallbackDialogue: fallbackDialogue,
            activeFallbackTriggerId: "fallback_trigger");
        cleanup.Add(profile);

        ProfileWorldFlagStateService flags = ProfileWorldFlagStateService.Instance;
        cleanup.Add(flags.gameObject);

        QuestInteractionResolver.QuestInteractionResolution before = QuestInteractionResolver.Resolve(
            service, null, null, null, null, profile);
        Assert.That(before.TriggerId, Is.EqualTo("fallback_trigger"));

        flags.SetFlag("world.coffee.zeb_departed", true);
        QuestInteractionResolver.QuestInteractionResolution ready = QuestInteractionResolver.Resolve(
            service, null, null, null, null, profile);
        Assert.That(ready.TriggerId, Is.EqualTo("trigger_beer"));

        flags.SetFlag("world.coffee.cold_beer_granted", true);
        QuestInteractionResolver.QuestInteractionResolution after = QuestInteractionResolver.Resolve(
            service, null, null, null, null, profile);
        Assert.That(after.TriggerId, Is.EqualTo("fallback_trigger"));
    }

    [Test]
    public void SequenceProgressPredicate_UsesLabelCompletionBoundary()
    {
        SequenceAsset sequence = ScriptableObject.CreateInstance<SequenceAsset>();
        Step_Label label = ScriptableObject.CreateInstance<Step_Label>();
        cleanup.Add(sequence);
        cleanup.Add(label);

        NarrativeTestUtility.SetPrivateField(sequence, "sequenceId", "test_sequence");
        NarrativeTestUtility.SetPrivateField(sequence, "steps", new List<SequenceStep> { label });
        NarrativeTestUtility.SetPrivateField(label, "labelName", "choosestarter");

        QuestInteractionSequenceProgressPredicate beforeLabel = new QuestInteractionSequenceProgressPredicate
        {
            sequenceId = "test_sequence",
            label = "choosestarter",
            comparison = QuestInteractionSequenceProgressComparison.BeforeLabel
        };
        QuestInteractionSequenceProgressPredicate afterLabel = new QuestInteractionSequenceProgressPredicate
        {
            sequenceId = "test_sequence",
            label = "choosestarter",
            comparison = QuestInteractionSequenceProgressComparison.AtOrAfterLabel
        };

        Assert.That(
            QuestInteractionSequenceProgressUtility.Matches(sequence, 0, beforeLabel, out string beforeWarning),
            Is.True,
            beforeWarning);
        Assert.That(
            QuestInteractionSequenceProgressUtility.Matches(sequence, 0, afterLabel, out string atLabelWarning),
            Is.False,
            atLabelWarning);
        Assert.That(
            QuestInteractionSequenceProgressUtility.Matches(sequence, 1, beforeLabel, out string pastLabelWarning),
            Is.False,
            pastLabelWarning);
        Assert.That(
            QuestInteractionSequenceProgressUtility.Matches(sequence, 1, afterLabel, out string afterWarning),
            Is.True,
            afterWarning);
    }

    private TestTimeOfDayService CreateTimeService(float normalizedTime, TimeSegment segment)
    {
        GameObject timeObject = new GameObject("TimeService_Test");
        cleanup.Add(timeObject);

        TestTimeOfDayService timeService = timeObject.AddComponent<TestTimeOfDayService>();
        timeService.SetState(normalizedTime, segment);
        return timeService;
    }

    private sealed class TestTimeOfDayService : MonoBehaviour, ITimeOfDayService
    {
        public float NormalizedTime { get; private set; }
        public TimeSegment CurrentSegment { get; private set; }
        public bool IsNight => CurrentSegment == TimeSegment.Night;
        public bool IsPaused => false;
        public bool IsHeld => false;
        public string HoldReason => string.Empty;

        public event System.Action<float> OnTimeNormalizedChanged;
        public event System.Action<TimeSegment, TimeSegment> OnSegmentChanged;
        public event System.Action<bool> OnDayNightChanged;
        public event System.Action<bool> OnPausedChanged;
        public event System.Action<bool, string> OnHoldChanged;

        public void SetState(float normalizedTime, TimeSegment segment)
        {
            NormalizedTime = normalizedTime;
            CurrentSegment = segment;
        }
    }
}
