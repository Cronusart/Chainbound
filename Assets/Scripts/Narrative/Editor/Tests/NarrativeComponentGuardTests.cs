using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class NarrativeComponentGuardTests
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
    public void NPCInteractableBinding_UsesNarrativeQuestGuard()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("guarded_npc");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Completed);

        GameObject npc = new GameObject("NPC");
        cleanup.Add(npc);
        npc.AddComponent<BoxCollider>();
        NPCInteractableBinding binding = npc.AddComponent<NPCInteractableBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "questGuard", NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(definition.QuestId, NarrativeQuestLifecycleMask.Completed) }));
        NarrativeTestUtility.SetPrivateField(binding, "playerInRange", true);

        Assert.That(((IInteractable)binding).IsInteractable, Is.True);
    }

    [Test]
    public void InteractableObject_UsesNarrativeQuestGuard()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("guarded_interactable");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Available);

        GameObject interactableGameObject = new GameObject("Monitor");
        cleanup.Add(interactableGameObject);
        interactableGameObject.AddComponent<BoxCollider>();
        InteractableObject interactable = interactableGameObject.AddComponent<InteractableObject>();
        NarrativeTestUtility.SetPrivateField(interactable, "questGuard", NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(definition.QuestId, NarrativeQuestLifecycleMask.Available) }));
        NarrativeTestUtility.SetPrivateField(interactable, "playerInRange", true);

        Assert.That(((IInteractable)interactable).IsInteractable, Is.True);
    }

    [Test]
    public void NarrativeRedirectZone_KeepsGuardedZonesArmedWhenGuardFails()
    {
        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("redirect_quest");
        cleanup.Add(definition);

        GameObject player = new GameObject("Player");
        cleanup.Add(player);
        player.tag = "Player";
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();

        DialogueSequenceSO redirectDialogue = NarrativeTestUtility.CreateDialogue("redirect_dialogue");
        cleanup.Add(redirectDialogue);

        GameObject guardedZoneObject = new GameObject("GuardedZone");
        cleanup.Add(guardedZoneObject);
        guardedZoneObject.AddComponent<BoxCollider>();
        NarrativeRedirectZone guardedZone = guardedZoneObject.AddComponent<NarrativeRedirectZone>();
        NarrativeTestUtility.SetPrivateField(guardedZone, "redirectDialogue", redirectDialogue);
        NarrativeTestUtility.SetPrivateField(guardedZone, "questGuard", NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(definition.QuestId, NarrativeQuestLifecycleMask.Active) }));
        guardedZone.Arm();

        InvokeOnTriggerEnter(guardedZone, playerCollider);

        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(guardedZone, "_armed"), Is.True);
    }

    [Test]
    public void QuestStateGameObjectGate_EvaluatesGuardPass()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO completedQuest = NarrativeTestUtility.CreateQuestDefinition("completed_quest");
        cleanup.Add(completedQuest);

        NarrativeTestUtility.SetQuestLifecycle(service, completedQuest, QuestLifecycleState.Completed);

        GameObject guardedGateObject = new GameObject("GuardedGate");
        cleanup.Add(guardedGateObject);
        QuestStateGameObjectGate guardedGate = guardedGateObject.AddComponent<QuestStateGameObjectGate>();
        GameObject guardedActiveTarget = new GameObject("GuardedActiveTarget");
        GameObject guardedInactiveTarget = new GameObject("GuardedInactiveTarget");
        cleanup.Add(guardedActiveTarget);
        cleanup.Add(guardedInactiveTarget);
        NarrativeTestUtility.SetPrivateField(guardedGate, "questService", service);
        NarrativeTestUtility.SetPrivateField(guardedGate, "questGuard", NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(completedQuest.QuestId, NarrativeQuestLifecycleMask.Completed) }));
        NarrativeTestUtility.SetPrivateField(guardedGate, "questActiveTargets", new[] { guardedActiveTarget });
        NarrativeTestUtility.SetPrivateField(guardedGate, "questInactiveTargets", new[] { guardedInactiveTarget });
        guardedGate.RefreshState();

        Assert.That(guardedActiveTarget.activeSelf, Is.True);
        Assert.That(guardedInactiveTarget.activeSelf, Is.False);
    }

    [Test]
    public void EncounterTriggerZone_EmptyQuestGuardPreservesLegacyAvailability()
    {
        GameObject zoneObject = new GameObject("LegacyEncounterZone");
        cleanup.Add(zoneObject);
        zoneObject.AddComponent<BoxCollider>();

        EncounterTriggerZone zone = zoneObject.AddComponent<EncounterTriggerZone>();

        Assert.That(zone.IsQuestAvailable, Is.True);
    }

    [Test]
    public void EncounterTriggerZone_ConfiguredGuardRequiresCompletedQuest()
    {
        QuestService service = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(service.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("chainbound_intro");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(service, definition, QuestLifecycleState.Active);

        GameObject zoneObject = new GameObject("QuestGatedEncounterZone");
        cleanup.Add(zoneObject);
        zoneObject.AddComponent<BoxCollider>();
        EncounterTriggerZone zone = zoneObject.AddComponent<EncounterTriggerZone>();
        NarrativeTestUtility.SetPrivateField(zone, "questService", service);
        NarrativeTestUtility.SetPrivateField(zone, "questGuard", NarrativeTestUtility.CreateGuard(
            NarrativeQuestGuardMatchMode.All,
            new[] { NarrativeTestUtility.CreateClause(definition.QuestId, NarrativeQuestLifecycleMask.Completed) }));

        Assert.That(zone.IsQuestAvailable, Is.False);

        service.CompleteQuest(definition.QuestId);

        Assert.That(zone.IsQuestAvailable, Is.True);
    }

    private static void InvokeOnTriggerEnter(NarrativeRedirectZone zone, Collider collider)
    {
        MethodInfo method = typeof(NarrativeRedirectZone).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(zone, new object[] { collider });
    }
}
