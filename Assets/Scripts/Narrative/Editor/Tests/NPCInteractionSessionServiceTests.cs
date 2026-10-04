using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class NPCInteractionSessionServiceTests
{
    private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                UnityEngine.Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [Test]
    public void BeginNormalInteraction_AcquiresConfiguredRouteOverrideAndFacesPlayer()
    {
        GameObject player = CreatePlayer(new Vector3(3f, 0f, 0f));
        NPCController npc = CreateNpc("npc_alpha");
        NPCInteractionSessionService service = CreateSessionService();

        service.BeginNormalInteraction(npc);

        Assert.That(service.HasActiveSession, Is.True);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.ForceHoldPosition));
        Assert.That(Vector3.Angle(npc.transform.forward, Vector3.right), Is.LessThan(1f));
    }

    [Test]
    public void BeginNormalInteraction_ReplacesPreviousSessionSafely()
    {
        CreatePlayer(new Vector3(3f, 0f, 0f));
        NPCController firstNpc = CreateNpc("npc_first");
        NPCController secondNpc = CreateNpc("npc_second");
        NPCInteractionSessionService service = CreateSessionService();

        service.BeginNormalInteraction(firstNpc);
        service.BeginNormalInteraction(secondNpc);

        Assert.That(firstNpc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.None));
        Assert.That(secondNpc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.ForceHoldPosition));
        Assert.That(service.HasActiveSession, Is.True);
    }

    [Test]
    public void PendingSession_ReleasesWhenGraceExpires()
    {
        CreatePlayer(new Vector3(3f, 0f, 0f));
        NPCController npc = CreateNpc("npc_pending");
        NPCInteractionSessionService service = CreateSessionService();
        NarrativeTestUtility.SetPrivateField(service, "downstreamClaimGraceSeconds", 0f);

        service.BeginNormalInteraction(npc);
        InvokePrivateMethod(service, "Update");

        Assert.That(service.HasActiveSession, Is.False);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.None));
    }

    [Test]
    public void NPCInteractableBinding_StartsNormalInteractionSessionBeforeUnityEvent()
    {
        CreatePlayer(new Vector3(3f, 0f, 0f));
        NPCController npc = CreateNpc("npc_binding");
        NPCInteractionSessionService service = CreateSessionService();

        GameObject bindingObject = new GameObject("NPCBinding");
        cleanup.Add(bindingObject);
        bindingObject.transform.SetParent(npc.transform, false);
        bindingObject.AddComponent<BoxCollider>();
        NPCInteractableBinding binding = bindingObject.AddComponent<NPCInteractableBinding>();
        NarrativeTestUtility.SetPrivateField(binding, "npcController", npc);
        NarrativeTestUtility.SetPrivateField(binding, "playerInRange", true);

        bool sessionWasActiveWhenInvoked = false;
        UnityEngine.Events.UnityEvent onInteracted = new UnityEngine.Events.UnityEvent();
        onInteracted.AddListener(() => sessionWasActiveWhenInvoked = service.HasActiveSession);
        NarrativeTestUtility.SetPrivateField(binding, "onInteracted", onInteracted);

        ((IInteractable)binding).Interact();

        Assert.That(sessionWasActiveWhenInvoked, Is.True);
        Assert.That(service.HasActiveSession, Is.True);
    }

    [UnityTest]
    public IEnumerator UnlockedDialogue_ClaimsPendingSessionTracksPlayerAndReleasesOnCompletion()
    {
        GameObject player = CreatePlayer(new Vector3(3f, 0f, 0f));
        DialogueService dialogueService = CreateDialogueService();
        NPCInteractionSessionService service = CreateSessionService();
        NPCController npc = CreateNpc("npc_unlocked");
        NPCMovementAgent movementAgent = npc.GetComponent<NPCMovementAgent>();

        npc.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        Vector2 facingBeforeTalk = new Vector2(npc.transform.forward.x, npc.transform.forward.z).normalized;

        service.BeginNormalInteraction(npc);
        dialogueService.PlayDialogue(CreateAutoAdvanceDialogue("unlocked_dialogue", 0.15f), false);

        yield return null;

        Assert.That(service.HasActiveSession, Is.True);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.ForceHoldPosition));
        Assert.That(GetPrivateField<Transform>(movementAgent, "facingTargetOverride"), Is.EqualTo(player.transform));

        yield return new WaitUntil(() => !dialogueService.IsPlaying);
        yield return null;

        Assert.That(service.HasActiveSession, Is.False);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.None));
        Assert.That(GetPrivateField<Transform>(movementAgent, "facingTargetOverride"), Is.Null);
        Vector2 facingAfterTalk = GetPrivateField<Vector2>(movementAgent, "lastStableWalkDirection");
        Assert.That(Vector2.Dot(facingAfterTalk.normalized, facingBeforeTalk), Is.GreaterThan(0.9f));
    }

    [UnityTest]
    public IEnumerator LockedDialogue_ClaimsPendingSessionWithoutTrackingPlayer()
    {
        CreatePlayer(new Vector3(3f, 0f, 0f));
        DialogueService dialogueService = CreateDialogueService();
        NPCInteractionSessionService service = CreateSessionService();
        NPCController npc = CreateNpc("npc_locked");
        NPCMovementAgent movementAgent = npc.GetComponent<NPCMovementAgent>();

        service.BeginNormalInteraction(npc);
        dialogueService.PlayDialogue(CreateAutoAdvanceDialogue("locked_dialogue", 0.15f), true);

        yield return null;

        Assert.That(service.HasActiveSession, Is.True);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.ForceHoldPosition));
        Assert.That(GetPrivateField<Transform>(movementAgent, "facingTargetOverride"), Is.Null);

        yield return new WaitUntil(() => !dialogueService.IsPlaying);
        yield return null;

        Assert.That(service.HasActiveSession, Is.False);
        Assert.That(npc.CurrentOverrideMode, Is.EqualTo(RouteOverrideMode.None));
    }

    [UnityTest]
    public IEnumerator DialogueWithoutPendingSession_DoesNotCreateInteractionSession()
    {
        DialogueService dialogueService = CreateDialogueService();
        NPCInteractionSessionService service = CreateSessionService();

        dialogueService.PlayDialogue(CreateAutoAdvanceDialogue("standalone_dialogue", 0.05f), false);

        yield return null;

        Assert.That(service.HasActiveSession, Is.False);

        yield return new WaitUntil(() => !dialogueService.IsPlaying);
        yield return null;

        Assert.That(service.HasActiveSession, Is.False);
    }

    [UnityTest]
    public IEnumerator ReplacingActiveDialogue_TerminatesPriorPlaybackWithoutCorruptingReplacement()
    {
        DialogueService dialogueService = CreateDialogueService();
        DialogueSequenceSO redirect = CreateAutoAdvanceDialogue("unlocked_redirect", 0.1f);
        DialogueSequenceSO npcDialogue = CreateAutoAdvanceDialogue("npc_dialogue", 0.5f);
        List<int> completedPlaybackIds = new List<int>();
        dialogueService.OnDialoguePlaybackComplete += playback => completedPlaybackIds.Add(playback.PlaybackId);

        dialogueService.PlayDialogue(redirect, false);
        yield return null;
        int redirectPlaybackId = dialogueService.CurrentPlayback.PlaybackId;

        dialogueService.PlayDialogue(npcDialogue, false);
        yield return null;
        int npcPlaybackId = dialogueService.CurrentPlayback.PlaybackId;

        Assert.That(npcPlaybackId, Is.Not.EqualTo(redirectPlaybackId));
        Assert.That(completedPlaybackIds, Is.EqualTo(new[] { redirectPlaybackId }));

        // The interrupted redirect's original delay expires during this window. Its
        // orphaned coroutine used to clear the replacement playback at this point.
        yield return new WaitForSecondsRealtime(0.2f);

        Assert.That(dialogueService.IsPlaying, Is.True);
        Assert.That(dialogueService.CurrentSequence, Is.SameAs(npcDialogue));
        Assert.That(dialogueService.CurrentPlayback.PlaybackId, Is.EqualTo(npcPlaybackId));
        Assert.That(completedPlaybackIds, Is.EqualTo(new[] { redirectPlaybackId }));

        dialogueService.StopDialogue();
        yield return null;

        Assert.That(dialogueService.IsPlaying, Is.False);
        Assert.That(completedPlaybackIds, Is.EqualTo(new[] { redirectPlaybackId, npcPlaybackId }));
    }

    [Test]
    public void LoopUntilStoppedPresentation_IsStoppedWhenPendingSessionExpires()
    {
        CreatePlayer(new Vector3(3f, 0f, 0f));
        NPCInteractionSessionService service = CreateSessionService();
        NarrativeTestUtility.SetPrivateField(service, "downstreamClaimGraceSeconds", 0f);

        NPCController npc = CreateNpcWithPresenter("npc_loop_pending", out ExplorationSpritePresentationPresenter presenter);
        ExplorationSpritePresentationClipSO clip = CreatePresentationClip(
            "pending_loop_clip",
            ExplorationPresentationPlaybackMode.LoopUntilStopped);

        service.BeginNormalInteraction(npc);
        Assert.That(presenter.PlayClip(clip), Is.True);
        Assert.That(presenter.IsPlaying, Is.True);

        InvokePrivateMethod(service, "Update");

        Assert.That(service.HasActiveSession, Is.False);
        Assert.That(presenter.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator LoopUntilStoppedPresentation_IsStoppedWhenClaimedDialogueCompletes()
    {
        CreatePlayer(new Vector3(3f, 0f, 0f));
        DialogueService dialogueService = CreateDialogueService();
        NPCInteractionSessionService service = CreateSessionService();
        NPCController npc = CreateNpcWithPresenter("npc_loop_dialogue", out ExplorationSpritePresentationPresenter presenter);
        ExplorationSpritePresentationClipSO clip = CreatePresentationClip(
            "dialogue_loop_clip",
            ExplorationPresentationPlaybackMode.LoopUntilStopped);

        service.BeginNormalInteraction(npc);
        Assert.That(presenter.PlayClip(clip), Is.True);

        dialogueService.PlayDialogue(CreateAutoAdvanceDialogue("loop_dialogue", 0.05f), false);

        yield return null;

        Assert.That(service.HasActiveSession, Is.True);
        Assert.That(presenter.IsPlaying, Is.True);

        yield return new WaitUntil(() => !dialogueService.IsPlaying);
        yield return null;

        Assert.That(service.HasActiveSession, Is.False);
        Assert.That(presenter.IsPlaying, Is.False);
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

    private NPCController CreateNpcWithPresenter(
        string npcId,
        out ExplorationSpritePresentationPresenter presenter)
    {
        NPCController npc = CreateNpc(npcId);

        GameObject visualObject = new GameObject("Visual");
        cleanup.Add(visualObject);
        visualObject.transform.SetParent(npc.transform, false);
        visualObject.AddComponent<MeshRendererNPCVisual>();

        presenter = npc.gameObject.AddComponent<ExplorationSpritePresentationPresenter>();
        return npc;
    }

    private ExplorationSpritePresentationClipSO CreatePresentationClip(
        string name,
        ExplorationPresentationPlaybackMode playbackMode)
    {
        ExplorationSpritePresentationClipSO clip = ScriptableObject.CreateInstance<ExplorationSpritePresentationClipSO>();
        clip.name = name;
        cleanup.Add(clip);

        NarrativeTestUtility.SetPrivateField(clip, "directionalSprites", CreateDirectionalSequences(frameCount: 3));
        NarrativeTestUtility.SetPrivateField(clip, "framesPerSecond", 12f);
        NarrativeTestUtility.SetPrivateField(clip, "defaultPlaybackMode", playbackMode);
        NarrativeTestUtility.SetPrivateField(clip, "loopDurationSeconds", 0.25f);
        NarrativeTestUtility.SetPrivateField(clip, "completionBehavior", ExplorationPresentationRestoreMode.ReleaseToGameplay);
        return clip;
    }

    private ExplorationDirectionalSpriteSequences CreateDirectionalSequences(int frameCount)
    {
        return new ExplorationDirectionalSpriteSequences
        {
            down = new SpriteAnimationSequence(CreateSprites(frameCount, Color.red)),
            left = new SpriteAnimationSequence(CreateSprites(frameCount, Color.green)),
            right = new SpriteAnimationSequence(CreateSprites(frameCount, Color.blue)),
            up = new SpriteAnimationSequence(CreateSprites(frameCount, Color.yellow))
        };
    }

    private Sprite[] CreateSprites(int frameCount, Color color)
    {
        Sprite[] sprites = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            cleanup.Add(texture);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            cleanup.Add(sprite);
            sprites[i] = sprite;
        }

        return sprites;
    }

    private GameObject CreatePlayer(Vector3 position)
    {
        GameObject player = new GameObject("Player");
        cleanup.Add(player);
        player.tag = "Player";
        player.transform.position = position;
        return player;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = FindField(target.GetType(), fieldName);
        return (T)field.GetValue(target);
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(target, null);
    }

    private static FieldInfo FindField(System.Type type, string fieldName)
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
