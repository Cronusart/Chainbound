using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public sealed class StarterOrbInteractionTests
{
    private const string DialogueFolder =
        "Assets/Scripts/Narrative/Quests/chainbound_intro/DialogueSO/";
    private const string ProfileFolder =
        "Assets/Scripts/NPC/Interaction/Intro/";

    [Test]
    public void PreSelectionRedirect_HasNoSpeakerAndExplicitlyFramesToby()
    {
        DialogueSequenceSO dialogue = AssetDatabase.LoadAssetAtPath<DialogueSequenceSO>(
            DialogueFolder + "Dial_StarterOrb_PreSelection.asset");

        Assert.That(dialogue, Is.Not.Null);
        Assert.That(dialogue.LineCount, Is.EqualTo(1));
        DialogueLine line = dialogue.GetLine(0);
        Assert.That(line.speakerName, Is.Empty);
        Assert.That(line.text, Does.StartWith("HEY!"));
        Assert.That(line.camera.targetSource, Is.EqualTo(DialogueCameraTargetSource.NpcId));
        Assert.That(line.camera.npcId, Is.EqualTo("prof_toby"));
        Assert.That(line.camera.shotPreset, Is.Not.Null);
    }

    [TestCase("chainbound_intro_starter_orb_profile.asset", "Validape")]
    [TestCase("chainbound_intro_starter_orb_kitsauto_profile.asset", "Kitsauto")]
    [TestCase("chainbound_intro_starter_orb_gilarime_profile.asset", "Gilarime")]
    public void OrbProfile_UsesCameraRedirectAndCompletedInternalMonologue(
        string profileFile,
        string speciesName)
    {
        QuestInteractionProfileSO profile = AssetDatabase.LoadAssetAtPath<QuestInteractionProfileSO>(
            ProfileFolder + profileFile);

        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.Rules, Has.Count.EqualTo(2));
        Assert.That(profile.Rules[0].useDialogueCamera, Is.True);
        Assert.That(profile.Rules[1].lifecycleScope, Is.EqualTo(QuestInteractionRuleLifecycleScope.UseLifecycleMask));
        Assert.That(profile.Rules[1].lifecycleMask, Is.EqualTo(NarrativeQuestLifecycleMask.Completed));
        Assert.That(profile.Rules[1].dialogue, Is.Not.Null);
        Assert.That(profile.Rules[1].dialogue.GetLine(0).speakerName, Is.Empty);
        Assert.That(profile.Rules[1].dialogue.GetLine(0).text, Does.Contain(speciesName));
    }

    [Test]
    public void ConfirmedStarterWorldState_DisablesObjectAndRecordsSessionState()
    {
        var stateRoot = new GameObject("NarrativeSceneObjectStateService_Test");
        var optionRoot = new GameObject("StarterOrb_Test");

        try
        {
            NarrativeSceneObjectStateService stateService =
                stateRoot.AddComponent<NarrativeSceneObjectStateService>();
            StarterOption option = optionRoot.AddComponent<StarterOption>();
            var serializedOption = new SerializedObject(option);
            serializedOption.FindProperty("hideWorldObjectOnConfirmedSelection").boolValue = true;
            serializedOption.FindProperty("confirmedHiddenStateId").stringValue = "test.starter_orb";
            serializedOption.ApplyModifiedPropertiesWithoutUndo();

            option.ApplyConfirmedWorldState();

            Assert.That(optionRoot.activeSelf, Is.False);
            Assert.That(stateService.TryGetDesiredState("test.starter_orb", out bool desiredActive), Is.True);
            Assert.That(desiredActive, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(optionRoot);
            Object.DestroyImmediate(stateRoot);
        }
    }

    [Test]
    public void ConfirmedStarterWorldState_WritesProfileWorldFlagWhenServiceExists()
    {
        var flagRoot = new GameObject("ProfileWorldFlagStateService_Test");
        var optionRoot = new GameObject("StarterOrb_Flag_Test");

        try
        {
            ProfileWorldFlagStateService flagService =
                flagRoot.AddComponent<ProfileWorldFlagStateService>();
            StarterOption option = optionRoot.AddComponent<StarterOption>();
            var serializedOption = new SerializedObject(option);
            serializedOption.FindProperty("hideWorldObjectOnConfirmedSelection").boolValue = true;
            serializedOption.FindProperty("confirmedHiddenStateId").stringValue =
                "world.pickup.loom_lab.starter_orb.validape";
            serializedOption.ApplyModifiedPropertiesWithoutUndo();

            option.ApplyConfirmedWorldState();

            Assert.That(optionRoot.activeSelf, Is.False);
            Assert.That(flagService.GetFlag("world.pickup.loom_lab.starter_orb.validape"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(optionRoot);
            Object.DestroyImmediate(flagRoot);
        }
    }

    [Test]
    public void SetProfileWorldFlag_ResolvesStarterToken()
    {
        Step_SetProfileWorldFlag step = ScriptableObject.CreateInstance<Step_SetProfileWorldFlag>();
        try
        {
            var serialized = new SerializedObject(step);
            serialized.FindProperty("flagKey").stringValue = "world.pickup.loom_lab.starter_orb.{Starter}";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var context = new SequenceContext();
            context.StarterSelected = "Validape";

            Assert.That(
                step.ResolveFlagKey(context),
                Is.EqualTo("world.pickup.loom_lab.starter_orb.validape"));
        }
        finally
        {
            Object.DestroyImmediate(step);
        }
    }

    [Test]
    public void UnregisteringConfirmedOption_DoesNotEraseSelectionBeforeSequenceReadsIt()
    {
        var serviceRoot = new GameObject("StarterSelectionService_Test");
        var optionRoot = new GameObject("StarterOption_Test");

        try
        {
            StarterSelectionService service = serviceRoot.AddComponent<StarterSelectionService>();
            StarterOption option = optionRoot.AddComponent<StarterOption>();
            service.RegisterOption(option);

            FieldInfo selectedField = typeof(StarterSelectionService).GetField(
                "selectedStarter",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(selectedField, Is.Not.Null);
            selectedField.SetValue(service, option);

            service.UnregisterOption(option);

            Assert.That(service.SelectedStarter, Is.SameAs(option));
            Assert.That(service.HasSelection(), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(optionRoot);
            Object.DestroyImmediate(serviceRoot);
        }
    }
}
