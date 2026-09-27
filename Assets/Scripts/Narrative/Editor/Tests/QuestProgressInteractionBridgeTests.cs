using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class QuestProgressInteractionBridgeTests
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
    public void OnInteract_StartsNpcInteractionSessionWhenProfileRequestsNpcDefaultBehavior()
    {
        CreatePlayer(new Vector3(2f, 0f, 0f));
        NPCInteractionSessionService service = CreateSessionService();
        NPCController npc = CreateNpc("quest_bridge_npc");
        QuestProgressInteractionBridge bridge = npc.gameObject.AddComponent<QuestProgressInteractionBridge>();

        QuestService questService = NarrativeTestUtility.CreateQuestService();
        cleanup.Add(questService.gameObject);

        QuestDefinitionSO definition = NarrativeTestUtility.CreateQuestDefinition("quest_bridge_behavior");
        cleanup.Add(definition);
        NarrativeTestUtility.SetQuestLifecycle(questService, definition, QuestLifecycleState.Active);

        QuestInteractionProfileSO profile = NarrativeTestUtility.CreateProfile(
            definition.QuestId,
            activeFallbackNpcBehavior: NarrativeTestUtility.CreateNpcBehaviorBinding(
                QuestInteractionNpcBehaviorMode.UseNpcDefault));
        cleanup.Add(profile);

        NarrativeTestUtility.SetPrivateField(bridge, "defaultProfile", profile);

        bridge.OnInteract();

        Assert.That(service.HasActiveSession, Is.True);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.ForceHoldPosition));
    }

    private NPCInteractionSessionService CreateSessionService()
    {
        GameObject serviceObject = new GameObject("NPCInteractionSessionService_Test");
        cleanup.Add(serviceObject);
        return serviceObject.AddComponent<NPCInteractionSessionService>();
    }

    private NPCController CreateNpc(string npcId)
    {
        GameObject npcObject = new GameObject($"NPC_{npcId}");
        cleanup.Add(npcObject);

        NPCController npc = npcObject.AddComponent<NPCController>();
        NarrativeTestUtility.SetPrivateField(npc, "npcId", npcId);

        NPCInteractionBehaviorSO behavior = ScriptableObject.CreateInstance<NPCInteractionBehaviorSO>();
        behavior.name = $"{npcId}_interaction_behavior";
        cleanup.Add(behavior);
        NarrativeTestUtility.SetPrivateField(npc, "interactionBehavior", behavior);

        return npc;
    }

    private void CreatePlayer(Vector3 position)
    {
        GameObject player = new GameObject("Player");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = position;
    }
}
