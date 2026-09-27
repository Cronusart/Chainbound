using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class InteractionAuthoringValidationTests
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
    public void CollectIssues_FlagsDeprecatedInteractionBridge()
    {
        GameObject root = new GameObject("LegacyRoot");
        cleanup.Add(root);
        root.AddComponent<InteractionBridge>();

        List<InteractionAuthoringIssue> issues = InteractionAuthoringValidation.CollectIssues(root);

        Assert.That(issues.Count, Is.EqualTo(1));
        Assert.That(issues[0].Severity, Is.EqualTo(InteractionAuthoringIssueSeverity.Error));
        Assert.That(issues[0].Message, Does.Contain("InteractionBridge"));
    }

    [Test]
    public void CollectIssues_FlagsDeprecatedFallbackFields()
    {
        GameObject root = new GameObject("Root");
        cleanup.Add(root);

        QuestProgressInteractionBridge bridge = root.AddComponent<QuestProgressInteractionBridge>();
        NarrativeTestUtility.SetPrivateField(bridge, "deprecatedQuestId", "chainbound_intro");

        NPCInteractableBinding binding = root.AddComponent<NPCInteractableBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "deprecatedGuardQuestId", "chainbound_intro");

        List<InteractionAuthoringIssue> issues = InteractionAuthoringValidation.CollectIssues(root);

        Assert.That(issues.Count, Is.EqualTo(3));
        Assert.That(issues.Exists(issue => issue.Message.Contains("legacy inline interaction data")), Is.True);
        Assert.That(issues.Exists(issue => issue.Message.Contains("legacy guardQuestId")), Is.True);
        Assert.That(issues.Exists(issue => issue.Message.Contains("no default profile or router")), Is.True);
    }

    [Test]
    public void CollectIssues_WarnsWhenTimeAwareProfilesRelyOnAutoResolvedTimeSource()
    {
        GameObject root = new GameObject("Root");
        cleanup.Add(root);

        QuestProgressInteractionBridge bridge = root.AddComponent<QuestProgressInteractionBridge>();

        QuestInteractionRule timeAwareRule = new QuestInteractionRule
        {
            label = "Morning greeting",
            timeOfDay = new TimeOfDayPredicate
            {
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.20f,
                maxNormalizedTime = 0.35f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            "time_quest",
            new[] { timeAwareRule });
        cleanup.Add(profile);
        NarrativeTestUtility.SetPrivateField(bridge, "defaultProfile", profile);

        List<InteractionAuthoringIssue> issues = InteractionAuthoringValidation.CollectIssues(root);

        Assert.That(issues.Exists(issue => issue.Message.Contains("auto-resolving a scene TimeOfDayManager")), Is.True);
    }

    [Test]
    public void CollectIssues_WarnsWhenTimePredicateLooksContradictory()
    {
        GameObject root = new GameObject("Root");
        cleanup.Add(root);

        QuestProgressInteractionBridge bridge = root.AddComponent<QuestProgressInteractionBridge>();

        QuestInteractionRule contradictoryRule = new QuestInteractionRule
        {
            label = "Contradictory time rule",
            timeOfDay = new TimeOfDayPredicate
            {
                allowedSegments = TimeSegmentMask.Day,
                useNormalizedTimeRange = true,
                minNormalizedTime = 0.85f,
                maxNormalizedTime = 0.95f
            }
        };

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            "contradiction_quest",
            new[] { contradictoryRule });
        cleanup.Add(profile);
        NarrativeTestUtility.SetPrivateField(bridge, "defaultProfile", profile);

        List<InteractionAuthoringIssue> issues = InteractionAuthoringValidation.CollectIssues(root);

        Assert.That(issues.Exists(issue => issue.Message.Contains("do not overlap")), Is.True);
    }
}
