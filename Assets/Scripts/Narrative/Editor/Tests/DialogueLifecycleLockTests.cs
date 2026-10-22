using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DialogueLifecycleLockTests
{
    private readonly List<Object> cleanup = new List<Object>();

    [SetUp]
    public void SetUp()
    {
        DestroyLingering<DialogueService>();
        DestroyLingering<InputLockService>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
        DestroyLingering<DialogueService>();
        DestroyLingering<InputLockService>();
    }

    [UnityTest]
    public IEnumerator DisablingDialoguePresentation_ReleasesDialogueInputLock()
    {
        GameObject inputLockObject = new GameObject("InputLockService_LifecycleTest");
        cleanup.Add(inputLockObject);
        InputLockService inputLocks = inputLockObject.AddComponent<InputLockService>();

        GameObject uiObject = new GameObject("DialogueUI_LifecycleTest");
        cleanup.Add(uiObject);
        DialogueUIController ui = uiObject.AddComponent<DialogueUIController>();

        GameObject serviceObject = new GameObject("DialogueService_LifecycleTest");
        cleanup.Add(serviceObject);
        DialogueService service = serviceObject.AddComponent<DialogueService>();
        service.SetUIController(ui);

        DialogueSequenceSO dialogue = ScriptableObject.CreateInstance<DialogueSequenceSO>();
        cleanup.Add(dialogue);
        NarrativeTestUtility.SetPrivateField(dialogue, "lines", new List<DialogueLine>
        {
            new DialogueLine { speakerName = "Toby", text = "Still here.", typeSpeed = 0f }
        });
        NarrativeTestUtility.SetPrivateField(dialogue, "autoAdvance", false);

        service.PlayDialogue(dialogue, true);
        yield return null;

        Assert.That(service.IsPlaying, Is.True);
        Assert.That(inputLocks.IsInteractionLocked, Is.True);

        ui.CancelDialogueForPresentationLoss();
        yield return null;

        Assert.That(service.IsPlaying, Is.False);
        Assert.That(inputLocks.IsInteractionLocked, Is.False);
    }

    private static void DestroyLingering<T>() where T : Component
    {
        T[] instances = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < instances.Length; i++)
        {
            if (instances[i] != null)
                Object.DestroyImmediate(instances[i].gameObject);
        }
    }
}
