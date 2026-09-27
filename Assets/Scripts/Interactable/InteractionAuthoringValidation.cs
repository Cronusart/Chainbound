using System.Collections.Generic;
using UnityEngine;

public enum InteractionAuthoringIssueSeverity
{
    Warning,
    Error
}

public readonly struct InteractionAuthoringIssue
{
    public InteractionAuthoringIssue(
        InteractionAuthoringIssueSeverity severity,
        string hierarchyPath,
        string message,
        Object context)
    {
        Severity = severity;
        HierarchyPath = hierarchyPath ?? "";
        Message = message ?? "";
        Context = context;
    }

    public InteractionAuthoringIssueSeverity Severity { get; }
    public string HierarchyPath { get; }
    public string Message { get; }
    public Object Context { get; }
}

/// <summary>
/// Shared validation for canonical interaction authoring.
/// Runtime components expose their migration state here so editor tooling and tests can
/// report stale legacy setup consistently.
/// </summary>
public static class InteractionAuthoringValidation
{
    public static List<InteractionAuthoringIssue> CollectIssues(GameObject root)
    {
        List<InteractionAuthoringIssue> issues = new List<InteractionAuthoringIssue>();
        AppendIssues(root, issues);
        return issues;
    }

    public static void AppendIssues(GameObject root, List<InteractionAuthoringIssue> issues)
    {
        if (root == null || issues == null)
            return;

        InteractionBridge[] legacyBridges = root.GetComponentsInChildren<InteractionBridge>(true);
        for (int i = 0; i < legacyBridges.Length; i++)
        {
            InteractionBridge legacyBridge = legacyBridges[i];
            if (legacyBridge == null)
                continue;

            issues.Add(BuildIssue(
                InteractionAuthoringIssueSeverity.Error,
                legacyBridge,
                "InteractionBridge is deprecated. Replace it with QuestProgressInteractionBridge plus QuestInteractionProfileSO assets."));
        }

        QuestProgressInteractionBridge[] canonicalBridges = root.GetComponentsInChildren<QuestProgressInteractionBridge>(true);
        for (int i = 0; i < canonicalBridges.Length; i++)
        {
            QuestProgressInteractionBridge bridge = canonicalBridges[i];
            if (bridge == null)
                continue;

            if (bridge.HasDeprecatedInlineAuthoringData)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Error,
                    bridge,
                    "QuestProgressInteractionBridge still has legacy inline interaction data serialized. Move that behavior into QuestInteractionProfileSO assets."));
            }

            if (!bridge.HasAuthoringProfileSource)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Warning,
                    bridge,
                    "QuestProgressInteractionBridge has no default profile or router assigned."));
            }

            if (!bridge.HasValidTimeOfDaySourceOverride)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Warning,
                    bridge,
                    "QuestProgressInteractionBridge has a time source override assigned that does not implement ITimeOfDayService."));
            }

            AppendTimeOfDayIssues(bridge, issues);
        }

        NPCInteractableBinding[] npcBindings = root.GetComponentsInChildren<NPCInteractableBinding>(true);
        for (int i = 0; i < npcBindings.Length; i++)
        {
            NPCInteractableBinding binding = npcBindings[i];
            if (binding != null && binding.HasDeprecatedGuardQuestId)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Error,
                    binding,
                    "NPCInteractableBinding still has legacy guardQuestId data serialized. Use NarrativeQuestGuard instead."));
            }
        }

        InteractableObject[] interactables = root.GetComponentsInChildren<InteractableObject>(true);
        for (int i = 0; i < interactables.Length; i++)
        {
            InteractableObject interactable = interactables[i];
            if (interactable != null && interactable.HasDeprecatedGuardQuestId)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Error,
                    interactable,
                    "InteractableObject still has legacy guardQuestId data serialized. Use NarrativeQuestGuard instead."));
            }
        }

        NarrativeRedirectZone[] redirectZones = root.GetComponentsInChildren<NarrativeRedirectZone>(true);
        for (int i = 0; i < redirectZones.Length; i++)
        {
            NarrativeRedirectZone zone = redirectZones[i];
            if (zone != null && zone.HasDeprecatedGuardQuestId)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Error,
                    zone,
                    "NarrativeRedirectZone still has legacy guardQuestId data serialized. Use NarrativeQuestGuard instead."));
            }
        }

        QuestStateGameObjectGate[] gates = root.GetComponentsInChildren<QuestStateGameObjectGate>(true);
        for (int i = 0; i < gates.Length; i++)
        {
            QuestStateGameObjectGate gate = gates[i];
            if (gate != null && gate.HasDeprecatedQuestId)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Error,
                    gate,
                    "QuestStateGameObjectGate still has legacy questId data serialized. Use NarrativeQuestGuard instead."));
            }
        }
    }

    private static InteractionAuthoringIssue BuildIssue(
        InteractionAuthoringIssueSeverity severity,
        Component component,
        string message)
    {
        return new InteractionAuthoringIssue(
            severity,
            BuildHierarchyPath(component != null ? component.transform : null),
            message,
            component);
    }

    private static string BuildHierarchyPath(Transform transform)
    {
        if (transform == null)
            return "";

        Stack<string> segments = new Stack<string>();
        Transform current = transform;
        while (current != null)
        {
            segments.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", segments);
    }

    private static void AppendTimeOfDayIssues(
        QuestProgressInteractionBridge bridge,
        List<InteractionAuthoringIssue> issues)
    {
        if (bridge == null || issues == null)
            return;

        HashSet<QuestInteractionProfileSO> visitedProfiles = new HashSet<QuestInteractionProfileSO>();
        bool hasTimeAwareRules = false;

        AppendProfileTimeOfDayIssues(bridge, bridge.DefaultProfile, visitedProfiles, issues, ref hasTimeAwareRules);

        QuestInteractionRouterSO router = bridge.InteractionRouter;
        if (router != null)
        {
            IReadOnlyList<QuestInteractionRouterSO.RouteSlot> slots = router.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                QuestInteractionRouterSO.RouteSlot slot = slots[i];
                if (slot?.Profile == null)
                    continue;

                AppendProfileTimeOfDayIssues(bridge, slot.Profile, visitedProfiles, issues, ref hasTimeAwareRules);
            }
        }

        if (hasTimeAwareRules && !bridge.HasExplicitTimeOfDaySourceOverride)
        {
            issues.Add(BuildIssue(
                InteractionAuthoringIssueSeverity.Warning,
                bridge,
                "QuestProgressInteractionBridge uses time-aware interaction rules and relies on auto-resolving a scene TimeOfDayManager because no explicit time source override is assigned."));
        }
    }

    private static void AppendProfileTimeOfDayIssues(
        QuestProgressInteractionBridge bridge,
        QuestInteractionProfileSO profile,
        HashSet<QuestInteractionProfileSO> visitedProfiles,
        List<InteractionAuthoringIssue> issues,
        ref bool hasTimeAwareRules)
    {
        if (bridge == null || profile == null || visitedProfiles == null || issues == null)
            return;

        if (!visitedProfiles.Add(profile))
            return;

        IReadOnlyList<QuestInteractionRule> rules = profile.Rules;
        if (rules == null)
            return;

        for (int i = 0; i < rules.Count; i++)
        {
            QuestInteractionRule rule = rules[i];
            if (rule == null || !TimeOfDayPredicateUtility.HasConfiguredConstraints(rule.timeOfDay))
                continue;

            hasTimeAwareRules = true;

            List<string> warnings = new List<string>();
            TimeOfDayPredicateUtility.AppendValidationWarnings(rule.timeOfDay, warnings, TimeSegmentThresholds.Default);
            for (int warningIndex = 0; warningIndex < warnings.Count; warningIndex++)
            {
                issues.Add(BuildIssue(
                    InteractionAuthoringIssueSeverity.Warning,
                    bridge,
                    $"Quest interaction profile '{profile.name}' {BuildRuleLabel(rule, i)}: {warnings[warningIndex]}"));
            }
        }
    }

    private static string BuildRuleLabel(QuestInteractionRule rule, int index)
    {
        if (rule != null && !string.IsNullOrWhiteSpace(rule.label))
            return $"rule {index} ('{rule.label}')";

        return $"rule {index}";
    }
}
