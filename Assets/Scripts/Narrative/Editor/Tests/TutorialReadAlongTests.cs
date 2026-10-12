using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SignalJam.Narrative.Combat;
using SignalJam.UI.MenuNavigation;
using SignalJam.UI.MobileControls;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

[TestFixture]
public sealed class TutorialReadAlongTests
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
    public void Snippet_RequiresIdAndBody()
    {
        TutorialReadAlongSnippetSO snippet = ScriptableObject.CreateInstance<TutorialReadAlongSnippetSO>();
        cleanup.Add(snippet);
        Assert.That(snippet.IsValid, Is.False);

        NarrativeTestUtility.SetPrivateField(snippet, "snippetId", "tutorial.move");
        NarrativeTestUtility.SetPrivateField(snippet, "body", "Use the D-pad or joystick to move around.");
        Assert.That(snippet.IsValid, Is.True);
    }

    [Test]
    public void Step_FailsWhenSnippetMissing()
    {
        Step_ShowTutorialReadAlong step = ScriptableObject.CreateInstance<Step_ShowTutorialReadAlong>();
        cleanup.Add(step);
        Assert.That(step.Validate(), Is.False);

        StepOutcome outcome = default;
        bool completed = false;
        var enumerator = step.Execute(null, result =>
        {
            outcome = result;
            completed = true;
        });
        while (enumerator.MoveNext())
        {
        }

        Assert.That(completed, Is.True);
        Assert.That(outcome.Result, Is.EqualTo(StepResult.Failed));
    }

    [Test]
    public void Presenter_ShowFreezesMenusAndWorldGameplay()
    {
        GameObject lockObject = new GameObject("InputLockService_ReadAlong");
        GameObject gateObject = new GameObject("GameplayMenu_ReadAlong");
        GameObject presenterObject = new GameObject("TutorialReadAlongPresenter_Test", typeof(RectTransform));
        cleanup.Add(lockObject);
        cleanup.Add(gateObject);
        cleanup.Add(presenterObject);

        InputLockService inputLocks = lockObject.AddComponent<InputLockService>();
        GameplayMenuAvailabilityService gate = gateObject.AddComponent<GameplayMenuAvailabilityService>();
        NarrativeTestUtility.SetPrivateField(gate, "requireIntroComplete", false);
        TutorialReadAlongPresenter presenter = presenterObject.AddComponent<TutorialReadAlongPresenter>();

        Assert.That(gate.CanOpen, Is.True);
        Assert.That(inputLocks.IsWorldGameplayFrozen, Is.False);

        TutorialReadAlongSnippetSO snippet = ScriptableObject.CreateInstance<TutorialReadAlongSnippetSO>();
        cleanup.Add(snippet);
        NarrativeTestUtility.SetPrivateField(snippet, "snippetId", "tutorial.move");
        NarrativeTestUtility.SetPrivateField(snippet, "body", "Use the D-pad or joystick to move around.");

        Assert.That(presenter.Show(snippet), Is.True);
        Assert.That(presenter.IsShowing, Is.True);
        Assert.That(inputLocks.IsWorldGameplayFrozen, Is.True);
        Assert.That(gate.CanOpen, Is.False);

        presenter.Hide();
        Assert.That(presenter.IsShowing, Is.False);
        Assert.That(inputLocks.IsWorldGameplayFrozen, Is.False);
        Assert.That(gate.CanOpen, Is.True);
    }

    [Test]
    public void Presenter_ArmsDismissOnlyAfterRevealAndReadDelay()
    {
        GameObject presenterObject = new GameObject("TutorialReadAlongPresenter_Arm", typeof(RectTransform));
        cleanup.Add(presenterObject);
        TutorialReadAlongPresenter presenter = presenterObject.AddComponent<TutorialReadAlongPresenter>();

        TutorialReadAlongSnippetSO snippet = ScriptableObject.CreateInstance<TutorialReadAlongSnippetSO>();
        cleanup.Add(snippet);
        NarrativeTestUtility.SetPrivateField(snippet, "snippetId", "tutorial.move");
        NarrativeTestUtility.SetPrivateField(snippet, "body", "Use the D-pad or joystick to move around.");

        Assert.That(presenter.Show(snippet), Is.True);
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(presenter, "dismissArmed"), Is.False);
        Assert.That(
            NarrativeTestUtility.GetPrivateField<float>(presenter, "dismissDelayAfterRevealSeconds"),
            Is.EqualTo(TutorialReadAlongPresenter.DefaultDismissDelayAfterRevealSeconds));

        ItemObtainPanelReveal reveal = presenter.GetComponent<ItemObtainPanelReveal>();
        Assert.That(reveal, Is.Not.Null);
        reveal.HideImmediate();
        NarrativeTestUtility.SetPrivateField(reveal, "revealPlaying", true);
        NarrativeTestUtility.SetPrivateField(presenter, "dismissDelayAfterRevealSeconds", 100f);

        System.Collections.IEnumerator arm = InvokePrivateEnumerator(
            presenter,
            "ArmDismissAfterReveal",
            MobileHudEmphasisTarget.None);
        Assert.That(arm.MoveNext(), Is.True);
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(presenter, "dismissArmed"), Is.False);

        NarrativeTestUtility.SetPrivateField(reveal, "revealPlaying", false);
        Assert.That(arm.MoveNext(), Is.True);
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(presenter, "dismissArmed"), Is.False);

        NarrativeTestUtility.SetPrivateField(presenter, "dismissDelayAfterRevealSeconds", 0f);
        arm.MoveNext();
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(presenter, "dismissArmed"), Is.True);

        presenter.Hide();
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(presenter, "dismissArmed"), Is.False);
    }

    [Test]
    public void EncounterTriggerZone_SkipsRollWhileWorldGameplayFrozen()
    {
        GameObject lockObject = new GameObject("InputLockService_EncounterFreeze");
        GameObject zoneObject = new GameObject("EncounterZone_Freeze");
        GameObject player = new GameObject("Player");
        cleanup.Add(lockObject);
        cleanup.Add(zoneObject);
        cleanup.Add(player);

        InputLockService inputLocks = lockObject.AddComponent<InputLockService>();
        zoneObject.AddComponent<BoxCollider>().isTrigger = true;
        EncounterTriggerZone zone = zoneObject.AddComponent<EncounterTriggerZone>();
        InputLockHandle freeze = inputLocks.AcquireLock("test", InputLockChannel.All);

        InvokePrivate(zone, "TryRoll", player.transform);
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(zone, "_isProcessing"), Is.False);
        freeze.Release();
    }

    [Test]
    public void NpcEncounterTriggerZone_SkipsLaunchWhileWorldGameplayFrozen()
    {
        GameObject lockObject = new GameObject("InputLockService_NpcEncounterFreeze");
        GameObject zoneObject = new GameObject("NpcEncounter_Freeze");
        GameObject player = new GameObject("Player");
        cleanup.Add(lockObject);
        cleanup.Add(zoneObject);
        cleanup.Add(player);

        player.tag = "Player";
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();
        InputLockService inputLocks = lockObject.AddComponent<InputLockService>();
        zoneObject.AddComponent<BoxCollider>().isTrigger = true;
        NpcEncounterTriggerZone zone = zoneObject.AddComponent<NpcEncounterTriggerZone>();
        InputLockHandle freeze = inputLocks.AcquireLock("test", InputLockChannel.All);

        InvokePrivate(zone, "OnTriggerEnter", playerCollider);
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(zone, "_hasTriggered"), Is.False);
        Assert.That(NarrativeTestUtility.GetPrivateField<bool>(zone, "_isProcessing"), Is.False);
        freeze.Release();
    }

    [Test]
    public void InteractableObject_AutoOnEnterDoesNotFireWhileInteractionLocked()
    {
        GameObject lockObject = new GameObject("InputLockService_InteractFreeze");
        GameObject interactableObject = new GameObject("AutoInteract_Freeze");
        GameObject player = new GameObject("Player");
        cleanup.Add(lockObject);
        cleanup.Add(interactableObject);
        cleanup.Add(player);

        player.tag = "Player";
        BoxCollider playerCollider = player.AddComponent<BoxCollider>();
        InputLockService inputLocks = lockObject.AddComponent<InputLockService>();
        interactableObject.AddComponent<BoxCollider>().isTrigger = true;
        InteractableObject interactable = interactableObject.AddComponent<InteractableObject>();
        NarrativeTestUtility.SetPrivateField(interactable, "triggerMode", InteractableObject.TriggerMode.AutoOnEnter);

        int fired = 0;
        UnityEvent onInteracted = NarrativeTestUtility.GetPrivateField<UnityEvent>(interactable, "onInteracted");
        if (onInteracted == null)
        {
            onInteracted = new UnityEvent();
            NarrativeTestUtility.SetPrivateField(interactable, "onInteracted", onInteracted);
        }

        onInteracted.AddListener(() => fired++);
        InputLockHandle freeze = inputLocks.AcquireLock("test", InputLockChannel.All);
        InvokePrivate(interactable, "OnTriggerEnter", playerCollider);

        Assert.That(fired, Is.EqualTo(0));
        Assert.That(((IInteractable)interactable).IsInteractable, Is.False);
        freeze.Release();
    }

    [Test]
    public void AuthoredAssets_IntroSequenceInsertsReadAlongBeats()
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(
            "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_sequence.asset");
        Assert.That(sequence, Is.Not.Null);

        Step_ShowTutorialReadAlong move = AssetDatabase.LoadAssetAtPath<Step_ShowTutorialReadAlong>(
            TutorialReadAlongAuthoring.MoveStepPath);
        Step_ShowTutorialReadAlong interact = AssetDatabase.LoadAssetAtPath<Step_ShowTutorialReadAlong>(
            TutorialReadAlongAuthoring.InteractStepPath);
        Step_ShowTutorialReadAlong sprint = AssetDatabase.LoadAssetAtPath<Step_ShowTutorialReadAlong>(
            TutorialReadAlongAuthoring.SprintStepPath);
        Step_ShowTutorialReadAlong jump = AssetDatabase.LoadAssetAtPath<Step_ShowTutorialReadAlong>(
            TutorialReadAlongAuthoring.JumpStepPath);
        SequenceStep restore = AssetDatabase.LoadAssetAtPath<SequenceStep>(
            "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_Steps/intro_restorecamerafollow.asset");
        SequenceStep encounter = AssetDatabase.LoadAssetAtPath<SequenceStep>(
            "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_Steps/intro_startencounter_firstcreature.asset");
        SequenceStep ledge = AssetDatabase.LoadAssetAtPath<SequenceStep>(
            "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_Steps/intro_playdial_ledge.asset");

        Assert.That(move, Is.Not.Null);
        Assert.That(interact, Is.Not.Null);
        Assert.That(sprint, Is.Not.Null);
        Assert.That(jump, Is.Not.Null);
        Assert.That(move.Snippet, Is.Not.Null);
        Assert.That(move.Snippet.IsValid, Is.True);
        Assert.That(move.Snippet.Emphasis, Is.EqualTo(MobileHudEmphasisTarget.MovePad));
        Assert.That(move.Snippet.KeepsHudVisible, Is.True);
        Assert.That(interact.Snippet.Body, Does.Contain("Press A").IgnoreCase);
        Assert.That(interact.Snippet.Emphasis, Is.EqualTo(MobileHudEmphasisTarget.InteractA));
        Assert.That(sprint.Snippet.Body, Does.Contain("Double-tap").IgnoreCase);
        Assert.That(sprint.Snippet.Emphasis, Is.EqualTo(MobileHudEmphasisTarget.SprintX));
        Assert.That(sprint.Snippet.KeepsHudVisible, Is.True);
        Assert.That(jump.Snippet.Body, Does.Contain("jump").IgnoreCase);
        Assert.That(jump.Snippet.Emphasis, Is.EqualTo(MobileHudEmphasisTarget.JumpB));
        Assert.That(jump.Snippet.KeepsHudVisible, Is.True);

        Assert.That(IndexOf(sequence, restore) + 1, Is.EqualTo(IndexOf(sequence, move)));
        Assert.That(IndexOf(sequence, move) + 1, Is.EqualTo(IndexOf(sequence, interact)));
        Assert.That(IndexOf(sequence, encounter) + 1, Is.EqualTo(IndexOf(sequence, sprint)));
        Assert.That(IndexOf(sequence, ledge) + 1, Is.EqualTo(IndexOf(sequence, jump)));
    }

    [Test]
    public void SprintLock_DoubleTapInsideWindowLocksWithoutClearingOnThatPress()
    {
        MobileSprintLock sprintLock = new MobileSprintLock { WindowSeconds = 0.32f };
        sprintLock.RegisterDown(1f);
        Assert.That(sprintLock.IsLocked, Is.False);
        sprintLock.RegisterDown(1.2f);
        Assert.That(sprintLock.IsLocked, Is.True);
    }

    [Test]
    public void SprintLock_TapOutsideWindowStartsANewWindow()
    {
        MobileSprintLock sprintLock = new MobileSprintLock { WindowSeconds = 0.32f };
        sprintLock.RegisterDown(1f);
        sprintLock.RegisterDown(1.4f);
        Assert.That(sprintLock.IsLocked, Is.False);
        sprintLock.RegisterDown(1.6f);
        Assert.That(sprintLock.IsLocked, Is.True);
    }

    [Test]
    public void SprintLock_LaterTapClearsAndDoesNotRelock()
    {
        MobileSprintLock sprintLock = new MobileSprintLock { WindowSeconds = 0.32f };
        sprintLock.RegisterDown(1f);
        sprintLock.RegisterDown(1.2f);
        sprintLock.RegisterDown(2f);
        Assert.That(sprintLock.IsLocked, Is.False);
        sprintLock.RegisterDown(2.1f);
        Assert.That(sprintLock.IsLocked, Is.False);
    }

    [Test]
    public void MobileHud_HasEmphasisOutlinesAndAutoSprintLabel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Controls/MobileServices.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<MobileHudControlEmphasis>(true), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<AutoSprintIndicator>(true), Is.Not.Null);
        Assert.That(CountNamed(prefab.transform, "ControlEmphasisFrame"), Is.EqualTo(5));
        MobileHudControlEmphasis emphasis = prefab.GetComponentInChildren<MobileHudControlEmphasis>(true);
        SerializedObject emphasisSerialized = new SerializedObject(emphasis);
        Assert.That(emphasisSerialized.FindProperty("sprintButton").objectReferenceValue, Is.Not.Null);
        Assert.That(emphasisSerialized.FindProperty("sprintOutline").objectReferenceValue, Is.Not.Null);
        Assert.That(emphasisSerialized.FindProperty("jumpButton").objectReferenceValue, Is.Not.Null);
        Assert.That(emphasisSerialized.FindProperty("jumpOutline").objectReferenceValue, Is.Not.Null);
        TutorialReadAlongSnippetSO menu = AssetDatabase.LoadAssetAtPath<TutorialReadAlongSnippetSO>(
            TutorialReadAlongAuthoring.MenuSnippetPath);
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.Body, Is.EqualTo(TutorialReadAlongAuthoring.MenuSnippetBody));
        Assert.That(menu.Emphasis, Is.EqualTo(MobileHudEmphasisTarget.MenuY));
        Assert.That(menu.KeepsHudVisible, Is.True);

        Transform label = FindChild(prefab.transform, "Label");
        Assert.That(label, Is.Not.Null);
        TMPro.TMP_Text text = label.GetComponent<TMPro.TMP_Text>();
        Assert.That(text, Is.Not.Null);
        Assert.That(text.text, Is.EqualTo(AutoSprintIndicator.DefaultLabel));
        Assert.That(label.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void Prefab_HasPresenterAndNoLabeledButtonsOnCard()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/TutorialReadAlongPanel.prefab");
        Assert.That(prefab, Is.Not.Null);
        TutorialReadAlongPresenter presenter = prefab.GetComponent<TutorialReadAlongPresenter>();
        Assert.That(presenter, Is.Not.Null);

        Transform card = FindChild(prefab.transform, "Card");
        Assert.That(card, Is.Not.Null);
        UnityEngine.UI.Button[] cardButtons = card.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        Assert.That(cardButtons, Is.Empty);

        Transform dismiss = FindChild(prefab.transform, "Dismiss");
        Assert.That(dismiss, Is.Not.Null);
        Assert.That(dismiss.GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "Title"), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "Body"), Is.Not.Null);
        Assert.That(FindChild(prefab.transform, "Hint"), Is.Not.Null);

        GameObject services = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/GameServices/GameServices.prefab");
        Assert.That(services, Is.Not.Null);
        Assert.That(
            services.GetComponentInChildren<TutorialReadAlongPresenter>(true),
            Is.Not.Null);
    }

    [Test]
    public void Prefab_ShowKeepsAuthoredLayoutAndDimmer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/UI/TutorialReadAlongPanel.prefab");
        GameObject instance = Object.Instantiate(prefab);
        cleanup.Add(instance);

        Assert.That(FindChild(instance.transform, "ScanlineOverlay"), Is.Null);
        Assert.That(FindChild(instance.transform, "CardFrame"), Is.Null);
        Transform safeArea = FindChild(instance.transform, "SafeArea");
        Assert.That(safeArea, Is.Not.Null);
        Assert.That(safeArea.GetComponent<SafeAreaAdapter>(), Is.Null);

        TutorialReadAlongSnippetSO snippet = ScriptableObject.CreateInstance<TutorialReadAlongSnippetSO>();
        cleanup.Add(snippet);
        NarrativeTestUtility.SetPrivateField(snippet, "snippetId", "tutorial.move");
        NarrativeTestUtility.SetPrivateField(snippet, "body", "Use the D-pad or joystick to move around.");

        TutorialReadAlongPresenter presenter = instance.GetComponent<TutorialReadAlongPresenter>();
        Assert.That(presenter.Show(snippet), Is.True);
        Assert.That(FindChild(instance.transform, "ScanlineOverlay"), Is.Null);
        Assert.That(FindChild(instance.transform, "CardFrame"), Is.Null);
        Assert.That(safeArea.GetComponent<SafeAreaAdapter>(), Is.Null);

        CanvasGroup dimmer = FindChild(instance.transform, "Dismiss").GetComponent<CanvasGroup>();
        Assert.That(dimmer.alpha, Is.EqualTo(1f).Within(0.001f));
        Assert.That(instance.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.001f));
    }

    private static int IndexOf(SequenceAsset sequence, SequenceStep step)
    {
        for (int i = 0; i < sequence.StepCount; i++)
        {
            if (sequence.GetStep(i) == step)
                return i;
        }

        return -1;
    }

    private static int CountNamed(Transform root, string name)
    {
        int count = root.name == name ? 1 : 0;
        for (int i = 0; i < root.childCount; i++)
            count += CountNamed(root.GetChild(i), name);
        return count;
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root.name == name)
            return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), name);
            if (found != null)
                return found;
        }

        return null;
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        method.Invoke(target, args);
    }

    private static System.Collections.IEnumerator InvokePrivateEnumerator(
        object target,
        string methodName,
        params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, methodName);
        return (System.Collections.IEnumerator)method.Invoke(target, args);
    }
}
