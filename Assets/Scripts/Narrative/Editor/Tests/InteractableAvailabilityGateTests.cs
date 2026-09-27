using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

public class InteractableAvailabilityGateTests
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

    [UnityTest]
    public IEnumerator NPCInteractableBinding_RemainsBlockedForOwnedSessionAndCooldown()
    {
        GameObject player = CreatePlayer(Vector3.zero);
        CreateRegistry(player.transform);
        DialogueService dialogueService = CreateDialogueService();
        NPCInteractionSessionService sessionService = CreateSessionService();
        NPCController npc = CreateNpc("npc_owned_session");
        NPCInteractableBinding binding = CreateNpcBinding(
            npc,
            CreateAvailabilitySettings(0.05f, 0.1f, true),
            out InteractableIndicator indicator);

        DialogueSequenceSO dialogue = CreateAutoAdvanceDialogue("owned_session_dialogue", 0.02f);
        UnityEvent onInteracted = new UnityEvent();
        onInteracted.AddListener(() => dialogueService.PlayDialogue(dialogue, false));
        NarrativeTestUtility.SetPrivateField(binding, "onInteracted", onInteracted);

        ((IInteractable)binding).Interact();

        Assert.That(((IInteractable)binding).IsInteractable, Is.False);
        Assert.That(indicator.IsSuppressed, Is.True);
        Assert.That(sessionService.HasActiveSession, Is.True);

        yield return null;

        Assert.That(((IInteractable)binding).IsInteractable, Is.False);
        Assert.That(indicator.IsSuppressed, Is.True);

        yield return new WaitUntil(() => !dialogueService.IsPlaying);
        yield return null;

        Assert.That(((IInteractable)binding).IsInteractable, Is.False);
        Assert.That(indicator.IsSuppressed, Is.True);

        yield return WaitForUnscaledSeconds(0.12f);

        Assert.That(((IInteractable)binding).IsInteractable, Is.True);
        Assert.That(indicator.IsSuppressed, Is.False);
        Assert.That(sessionService.HasActiveSession, Is.False);
    }

    [UnityTest]
    public IEnumerator ReplacingNpcSession_ReleasesOldGateAndIndicatorSuppression()
    {
        GameObject player = CreatePlayer(Vector3.zero);
        CreateRegistry(player.transform);
        NPCInteractionSessionService sessionService = CreateSessionService();
        NarrativeTestUtility.SetPrivateField(sessionService, "downstreamClaimGraceSeconds", 0.25f);

        NPCController firstNpc = CreateNpc("npc_first_gate");
        NPCController secondNpc = CreateNpc("npc_second_gate");
        NPCInteractableBinding firstBinding = CreateNpcBinding(
            firstNpc,
            CreateAvailabilitySettings(0.05f, 0.05f, true),
            out InteractableIndicator firstIndicator);
        NPCInteractableBinding secondBinding = CreateNpcBinding(
            secondNpc,
            CreateAvailabilitySettings(0.05f, 0.05f, true),
            out InteractableIndicator secondIndicator);

        ((IInteractable)firstBinding).Interact();
        ((IInteractable)secondBinding).Interact();

        yield return WaitForUnscaledSeconds(0.08f);

        Assert.That(((IInteractable)firstBinding).IsInteractable, Is.True);
        Assert.That(firstIndicator.IsSuppressed, Is.False);
        Assert.That(((IInteractable)secondBinding).IsInteractable, Is.False);
        Assert.That(secondIndicator.IsSuppressed, Is.True);
        Assert.That(sessionService.HasActiveSession, Is.True);
    }

    [UnityTest]
    public IEnumerator InteractableObject_NoDialoguePath_RestoresAfterGraceAndCooldown()
    {
        CreateRegistry(null);
        InteractableObject interactable = CreateInteractableObject(
            "Monitor",
            Vector3.zero,
            CreateAvailabilitySettings(0.05f, 0.05f, true),
            out InteractableIndicator indicator);

        ((IInteractable)interactable).Interact();

        Assert.That(((IInteractable)interactable).IsInteractable, Is.False);
        Assert.That(indicator.IsSuppressed, Is.True);
        Assert.That(Object.FindFirstObjectByType<DialogueService>(), Is.Null);

        yield return WaitForUnscaledSeconds(0.12f);

        Assert.That(((IInteractable)interactable).IsInteractable, Is.True);
        Assert.That(indicator.IsSuppressed, Is.False);
    }

    [UnityTest]
    public IEnumerator InteractableRegistry_GetNearest_SkipsBlockedInteractableUntilAvailabilityReturns()
    {
        GameObject player = CreatePlayer(Vector3.zero);
        GameObject registryObject = new GameObject("InteractableRegistry_Test");
        cleanup.Add(registryObject);
        InteractableRegistry registry = registryObject.AddComponent<InteractableRegistry>();
        registry.SetPlayerTransform(player.transform);

        InteractableObject nearest = CreateInteractableObject(
            "Nearest",
            new Vector3(1f, 0f, 0f),
            CreateAvailabilitySettings(0.05f, 0.05f, true),
            out _);
        InteractableObject farther = CreateInteractableObject(
            "Farther",
            new Vector3(2f, 0f, 0f),
            CreateAvailabilitySettings(0.05f, 0.05f, true),
            out _);

        registry.Register(nearest);
        registry.Register(farther);

        Assert.That(registry.GetNearest(), Is.SameAs((IInteractable)nearest));

        ((IInteractable)nearest).Interact();

        Assert.That(registry.GetNearest(), Is.SameAs((IInteractable)farther));

        yield return WaitForUnscaledSeconds(0.12f);

        Assert.That(registry.GetNearest(), Is.SameAs((IInteractable)nearest));
    }

    [UnityTest]
    public IEnumerator InteractableIndicator_HidesForInteractionLockButNotMovementOnly()
    {
        CreateInputLockService();

        GameObject parent = new GameObject("IndicatorParent");
        cleanup.Add(parent);
        InteractableIndicator indicator = CreateIndicator(parent.transform, "LockAwareIndicator");
        CanvasGroup canvasGroup = indicator.GetComponent<CanvasGroup>();

        indicator.SetInRange(true);
        yield return WaitForUnscaledSeconds(0.2f);

        Assert.That(canvasGroup.alpha, Is.GreaterThan(0.9f));

        InputLockHandle movementLock = InputLockService.Instance.AcquireLock("MovementOnly", InputLockChannel.Movement);
        yield return WaitForUnscaledSeconds(0.2f);

        Assert.That(canvasGroup.alpha, Is.GreaterThan(0.9f));

        movementLock.Release();

        InputLockHandle interactionLock = InputLockService.Instance.AcquireLock("InteractionOnly", InputLockChannel.Interaction);
        yield return WaitForUnscaledSeconds(0.2f);

        Assert.That(canvasGroup.alpha, Is.LessThan(0.1f));

        interactionLock.Release();
        yield return WaitForUnscaledSeconds(0.2f);

        Assert.That(canvasGroup.alpha, Is.GreaterThan(0.9f));
    }

    private NPCInteractableBinding CreateNpcBinding(
        NPCController npc,
        InteractableAvailabilitySettings availabilitySettings,
        out InteractableIndicator indicator)
    {
        GameObject bindingObject = new GameObject($"{npc.name}_Binding");
        cleanup.Add(bindingObject);
        bindingObject.transform.SetParent(npc.transform, false);
        bindingObject.AddComponent<BoxCollider>();

        indicator = CreateIndicator(bindingObject.transform, "NPC_Indicator");

        NPCInteractableBinding binding = bindingObject.AddComponent<NPCInteractableBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "npcController", npc);
        NarrativeTestUtility.SetPrivateField(binding, "indicator", indicator);
        NarrativeTestUtility.SetPrivateField(binding, "availabilitySettings", availabilitySettings);
        NarrativeTestUtility.SetPrivateField(binding, "playerInRange", true);
        return binding;
    }

    private InteractableObject CreateInteractableObject(
        string objectName,
        Vector3 position,
        InteractableAvailabilitySettings availabilitySettings,
        out InteractableIndicator indicator)
    {
        GameObject interactableObject = new GameObject(objectName);
        cleanup.Add(interactableObject);
        interactableObject.transform.position = position;
        interactableObject.AddComponent<BoxCollider>();

        indicator = CreateIndicator(interactableObject.transform, $"{objectName}_Indicator");

        InteractableObject interactable = interactableObject.AddComponent<InteractableObject>();
        NarrativeTestUtility.SetPrivateField(interactable, "indicator", indicator);
        NarrativeTestUtility.SetPrivateField(interactable, "availabilitySettings", availabilitySettings);
        NarrativeTestUtility.SetPrivateField(interactable, "playerInRange", true);
        return interactable;
    }

    private InteractableIndicator CreateIndicator(Transform parent, string objectName)
    {
        GameObject indicatorObject = new GameObject(objectName);
        cleanup.Add(indicatorObject);
        indicatorObject.transform.SetParent(parent, false);
        indicatorObject.AddComponent<Canvas>();
        indicatorObject.AddComponent<CanvasGroup>();
        return indicatorObject.AddComponent<InteractableIndicator>();
    }

    private NPCInteractionSessionService CreateSessionService()
    {
        GameObject serviceObject = new GameObject("NPCInteractionSessionService_Test");
        cleanup.Add(serviceObject);
        return serviceObject.AddComponent<NPCInteractionSessionService>();
    }

    private DialogueService CreateDialogueService()
    {
        GameObject uiObject = new GameObject("DialogueUI_Test");
        cleanup.Add(uiObject);
        DialogueUIController uiController = uiObject.AddComponent<DialogueUIController>();

        GameObject serviceObject = new GameObject("DialogueService_Test");
        cleanup.Add(serviceObject);
        DialogueService dialogueService = serviceObject.AddComponent<DialogueService>();
        dialogueService.SetUIController(uiController);
        return dialogueService;
    }

    private InputLockService CreateInputLockService()
    {
        GameObject serviceObject = new GameObject("InputLockService_Test");
        cleanup.Add(serviceObject);
        return serviceObject.AddComponent<InputLockService>();
    }

    private InteractableRegistry CreateRegistry(Transform playerTransform)
    {
        GameObject registryObject = new GameObject("InteractableRegistry_Test");
        cleanup.Add(registryObject);
        InteractableRegistry registry = registryObject.AddComponent<InteractableRegistry>();
        registry.SetPlayerTransform(playerTransform);
        return registry;
    }

    private DialogueSequenceSO CreateAutoAdvanceDialogue(string name, float autoAdvanceDelay)
    {
        DialogueSequenceSO dialogue = NarrativeTestUtility.CreateDialogue(name);
        cleanup.Add(dialogue);

        NarrativeTestUtility.SetPrivateField(dialogue, "lines", new List<DialogueLine>
        {
            new DialogueLine
            {
                speakerName = "NPC",
                text = "Hello there.",
                typeSpeed = 0f
            }
        });
        NarrativeTestUtility.SetPrivateField(dialogue, "autoAdvance", true);
        NarrativeTestUtility.SetPrivateField(dialogue, "autoAdvanceDelay", autoAdvanceDelay);
        NarrativeTestUtility.SetPrivateField(dialogue, "allowSkip", false);
        return dialogue;
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

    private GameObject CreatePlayer(Vector3 position)
    {
        GameObject player = new GameObject("Player");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = position;
        return player;
    }

    private static InteractableAvailabilitySettings CreateAvailabilitySettings(
        float graceSeconds,
        float cooldownSeconds,
        bool keepIndicatorHiddenUntilAvailable)
    {
        return new InteractableAvailabilitySettings
        {
            downstreamClaimGraceSeconds = graceSeconds,
            reinteractCooldownSeconds = cooldownSeconds,
            keepIndicatorHiddenUntilAvailable = keepIndicatorHiddenUntilAvailable
        };
    }

    private static IEnumerator WaitForUnscaledSeconds(float seconds)
    {
        float deadline = Time.unscaledTime + seconds;
        while (Time.unscaledTime < deadline)
            yield return null;
    }
}
