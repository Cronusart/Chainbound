using Chainbound.Combat;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class IntroOnboardingResumeTests
{
    private const string IntroSequencePath =
        "Assets/Scripts/Narrative/Quests/chainbound_intro/Sequence/chainbound_intro_sequence.asset";

    [Test]
    public void NamelessStartedProfile_PresentsNewGameAndReplaysIntro()
    {
        Assert.That(IntroResumePolicy.ShowContinue(true, true, ""), Is.False);
        Assert.That(IntroResumePolicy.ShowContinue(true, true, "   "), Is.False);
        Assert.That(IntroResumePolicy.ShowContinue(true, false, ""), Is.False);
        Assert.That(IntroResumePolicy.ShowContinue(false, false, ""), Is.False);

        Assert.That(IntroResumePolicy.ShouldRouteToIntro(true, true, true, ""), Is.True);
        Assert.That(IntroResumePolicy.ShouldReplayIntroFromBeginning(""), Is.True);
        Assert.That(IntroResumePolicy.ShouldReplayIntroFromBeginning("  "), Is.True);
        Assert.That(
            IntroResumePolicy.ShouldSuppressCompletedCinematic(true, true, ""),
            Is.False,
            "A monotonic cinematic flag must not hide the pre-name cinematic while the name is unset.");
    }

    [Test]
    public void NamedProfile_UsesContinueOnlyAfterTheIntroIsFinished()
    {
        Assert.That(IntroResumePolicy.ShowContinue(true, true, "Nova"), Is.True);
        Assert.That(IntroResumePolicy.ShowContinue(true, false, "Nova"), Is.False);

        Assert.That(IntroResumePolicy.ShouldRouteToIntro(true, false, false, "Nova"), Is.True);
        Assert.That(IntroResumePolicy.ShouldRouteToIntro(true, true, false, "Nova"), Is.True);
        Assert.That(IntroResumePolicy.ShouldRouteToIntro(true, true, true, "Nova"), Is.False);
        Assert.That(IntroResumePolicy.ShouldReplayIntroFromBeginning("Nova"), Is.False);
        Assert.That(IntroResumePolicy.ShouldSuppressCompletedCinematic(true, true, "Nova"), Is.True);
    }

    [Test]
    public void EmptyCheckpoint_RestartsAtStepZero_InsteadOfSafeLabel()
    {
        IntroCheckpointResume choice = IntroResumePolicy.Choose(
            checkpointId: "",
            checkpointIdFound: false,
            checkpointIdIndex: -1,
            resolvedCheckpointLabel: "",
            checkpointLabel: "",
            checkpointLabelFound: false,
            checkpointLabelIndex: -1,
            safeResumeLabel: "intro_player_room",
            safeLabelFound: true,
            safeLabelIndex: 9);

        Assert.That(choice.RestartAtBeginning, Is.True);
        Assert.That(choice.StepIndex, Is.EqualTo(0));
        Assert.That(choice.ResumeLabel, Is.Empty);
        Assert.That(IntroResumePolicy.ShouldPersistLabelProgress(""), Is.False);
        Assert.That(IntroResumePolicy.ShouldPersistLabelProgress("intro_player_room"), Is.True);
    }

    [Test]
    public void MissingCheckpointId_StillRecoversAtTheSafeLabel()
    {
        IntroCheckpointResume choice = IntroResumePolicy.Choose(
            checkpointId: "removed_checkpoint",
            checkpointIdFound: false,
            checkpointIdIndex: -1,
            resolvedCheckpointLabel: "",
            checkpointLabel: "",
            checkpointLabelFound: false,
            checkpointLabelIndex: -1,
            safeResumeLabel: "intro_player_room",
            safeLabelFound: true,
            safeLabelIndex: 9);

        Assert.That(choice.RestartAtBeginning, Is.False);
        Assert.That(choice.StepIndex, Is.EqualTo(9));
        Assert.That(choice.ResumeLabel, Is.EqualTo("intro_player_room"));
    }

    [Test]
    public void IntroSequence_CommitsThePlayerNameBeforeTheFirstCheckpoint()
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(IntroSequencePath);
        Assert.That(sequence, Is.Not.Null);

        int nameCommit = -1;
        int firstCheckpoint = -1;
        for (int i = 0; i < sequence.StepCount; i++)
        {
            SequenceStep step = sequence.GetStep(i);
            if (nameCommit < 0 && step is Step_CommitPlayerName)
                nameCommit = i;
            if (firstCheckpoint < 0 && step is Step_SetCheckpoint)
                firstCheckpoint = i;
        }

        Assert.That(nameCommit, Is.GreaterThanOrEqualTo(0));
        Assert.That(firstCheckpoint, Is.GreaterThan(nameCommit));
        Assert.That(sequence.GetStep(0), Is.InstanceOf<Step_Label>());
    }

    [Test]
    public void NameTaken_RePrompts_OtherCommitErrorsDoNot()
    {
        PlayerNameCommitResult taken = PlayerNameCommitFlow.FromEdgeBody("{\"success\":false,\"error\":\"name_taken\"}");
        PlayerNameCommitResult other = PlayerNameCommitResult.FromError("profile_set_display_name_failed", "profile_set_display_name_failed");
        PlayerNameCommitResult wrapped = PlayerNameCommitResult.FromError(
            "profile_set_display_name returned 409: {\"error\":\"name_taken\"}");

        Assert.That(taken.ErrorCode, Is.EqualTo(PlayerNameCommitFlow.NameTakenCode));
        Assert.That(taken.ErrorMessage, Is.EqualTo(PlayerNameCommitFlow.NameTakenMessage));
        Assert.That(PlayerNameCommitFlow.ShouldRePrompt(taken), Is.True);
        Assert.That(PlayerNameCommitFlow.ShouldRePrompt(wrapped), Is.True);
        Assert.That(PlayerNameCommitFlow.ShouldRePrompt(other), Is.False);
        Assert.That(PlayerNameCommitFlow.ShouldRePrompt(PlayerNameCommitResult.FromSuccess("Nova")), Is.False);
    }

    [Test]
    public void NamelessResume_DoesNotHonorAStepPastTheNameCommit()
    {
        Assert.That(IntroResumePolicy.ShouldHoldNamelessResume(
            IntroResumePolicy.IntroSequenceId, "", 4, 9), Is.True);
        Assert.That(IntroResumePolicy.ShouldHoldNamelessResume(
            IntroResumePolicy.IntroSequenceId, "   ", 4, 7), Is.True);
        Assert.That(IntroResumePolicy.ShouldHoldNamelessResume(
            IntroResumePolicy.IntroSequenceId, "Nova", 4, 9), Is.False);
        Assert.That(IntroResumePolicy.ShouldHoldNamelessResume(
            IntroResumePolicy.IntroSequenceId, "", 4, 4), Is.False);
        Assert.That(IntroResumePolicy.ShouldHoldNamelessResume(
            "some_other_sequence", "", 4, 9), Is.False);

        IntroCheckpointResume pastName = new IntroCheckpointResume(7, "intro_player_room", false, "");
        IntroCheckpointResume held = IntroResumePolicy.HoldNamelessPastNameCommit(
            pastName,
            IntroResumePolicy.IntroSequenceId,
            "",
            4,
            9);
        Assert.That(held.RestartAtBeginning, Is.True);
        Assert.That(held.StepIndex, Is.EqualTo(0));
        Assert.That(held.ResumeLabel, Is.Empty);

        IntroCheckpointResume named = IntroResumePolicy.HoldNamelessPastNameCommit(
            pastName,
            IntroResumePolicy.IntroSequenceId,
            "Nova",
            4,
            9);
        Assert.That(named.RestartAtBeginning, Is.False);
        Assert.That(named.ResumeLabel, Is.EqualTo("intro_player_room"));

        Assert.That(IntroResumePolicy.ShouldDiscardNamelessStepProgress(
            IntroResumePolicy.IntroSequenceId, "", 0), Is.True);
        Assert.That(IntroResumePolicy.ShouldDiscardNamelessStepProgress(
            IntroResumePolicy.IntroSequenceId, "   ", 0), Is.True);
        Assert.That(IntroResumePolicy.ShouldDiscardNamelessStepProgress(
            IntroResumePolicy.IntroSequenceId, "Nova", 0), Is.False);
        Assert.That(IntroResumePolicy.ShouldDiscardNamelessStepProgress(
            IntroResumePolicy.IntroSequenceId, "", 7), Is.False);
        Assert.That(IntroResumePolicy.ShouldDiscardNamelessStepProgress(
            "some_other_sequence", "", 0), Is.False);
    }

    [Test]
    public void AuthoredResumeScene_ReplacesAStaleBedroomSave()
    {
        IntroResumePolicy.PreferAuthoredResumePlacement(
            "player_room",
            "player_room_start",
            "loom_lab",
            "lab_from_loomville",
            out string sceneId,
            out string spawnId);

        Assert.That(sceneId, Is.EqualTo("loom_lab"));
        Assert.That(spawnId, Is.EqualTo("lab_from_loomville"));

        IntroResumePolicy.PreferAuthoredResumePlacement(
            "Loomville",
            "encounter_return",
            "",
            "",
            out sceneId,
            out spawnId);
        Assert.That(sceneId, Is.EqualTo("Loomville"));
        Assert.That(spawnId, Is.EqualTo("encounter_return"));
    }

    [Test]
    public void IntroResumeAuthoring_SplitsStarterLabelAndBattleCheckpoint()
    {
        SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(IntroSequencePath);
        Assert.That(sequence, Is.Not.Null);

        int nameCommit = -1;
        int encounter = -1;
        int escapeObjective = -1;
        int afterBattleCheckpoint = -1;
        int afterBattleLabel = -1;
        int starterCommit = -1;
        int starterCommittedLabel = -1;
        int beforeStarter = -1;
        int afterStarter = -1;
        int toby = -1;

        for (int i = 0; i < sequence.StepCount; i++)
        {
            SequenceStep step = sequence.GetStep(i);
            if (step is Step_CommitPlayerName)
            {
                nameCommit = i;
                Assert.That(step.GetFailurePolicy(), Is.EqualTo(StepFailurePolicy.AbortSequence));
            }

            if (step is Step_StartEncounter)
                encounter = i;

            if (step != null && step.name == "intro_setquestobjective_escapeencounter")
                escapeObjective = i;

            if (step is Step_SetCheckpoint leaving && leaving.CheckpointId == "intro_leaving_room")
            {
                Assert.That(leaving.ResumeSceneId, Is.EqualTo("Loomville"));
                Assert.That(leaving.ResumeSpawnId, Is.EqualTo("loomville_from_bedroom"));
            }

            if (step is Step_CommitStarterChainling)
                starterCommit = i;

            if (step is Step_Label label)
            {
                if (label.GetLabelName() == "after_first_creature")
                    afterBattleLabel = i;
                if (label.GetLabelName() == "starter_committed")
                    starterCommittedLabel = i;
            }

            if (step is Step_SetCheckpoint checkpoint)
            {
                if (checkpoint.CheckpointId == "intro_after_first_creature")
                {
                    afterBattleCheckpoint = i;
                    Assert.That(checkpoint.CheckpointLabel, Is.EqualTo("after_first_creature"));
                    Assert.That(checkpoint.ResumeSceneId, Is.EqualTo("Loomville"));
                    Assert.That(checkpoint.ResumeSpawnId, Is.EqualTo("loomville_from_combat"));
                }

                if (checkpoint.CheckpointId == "intro_before_choose_starter")
                {
                    beforeStarter = i;
                    Assert.That(checkpoint.CheckpointLabel, Is.EqualTo("choosestarter"));
                }

                if (checkpoint.CheckpointId == "intro_after_choose_starter")
                {
                    afterStarter = i;
                    Assert.That(checkpoint.CheckpointLabel, Is.EqualTo("starter_committed"));
                    Assert.That(checkpoint.ResumeSceneId, Is.EqualTo("loom_lab"));
                }

                if (checkpoint.CheckpointId == "intro_wait_for_toby")
                {
                    toby = i;
                    Assert.That(checkpoint.ResumeSceneId, Is.EqualTo("loom_lab"));
                    Assert.That(checkpoint.ResumeSpawnId, Is.EqualTo("lab_from_loomville"));
                    Assert.That(checkpoint.CheckpointLabel, Is.EqualTo("waitfortoby"));
                }
            }
        }

        Assert.That(nameCommit, Is.GreaterThanOrEqualTo(0));
        Assert.That(encounter, Is.GreaterThan(nameCommit));
        Assert.That(escapeObjective, Is.GreaterThan(encounter));
        Assert.That(afterBattleCheckpoint, Is.GreaterThan(escapeObjective));
        Assert.That(afterBattleLabel, Is.GreaterThan(afterBattleCheckpoint));
        Assert.That(starterCommit, Is.GreaterThan(afterBattleLabel));
        Assert.That(starterCommittedLabel, Is.GreaterThan(starterCommit));
        Assert.That(beforeStarter, Is.LessThan(starterCommit));
        Assert.That(afterStarter, Is.GreaterThan(starterCommittedLabel));
        Assert.That(toby, Is.GreaterThan(afterBattleLabel));
        Assert.That(toby, Is.LessThan(beforeStarter));
    }

    [Test]
    public void TakenNamePrompt_UsesRedAndRestoresTheAuthoredColor()
    {
        GameObject root = new GameObject("KeyboardPromptColor");
        try
        {
            OnScreenKeyboardUI keyboard = root.AddComponent<OnScreenKeyboardUI>();
            GameObject promptObject = new GameObject("PromptText");
            promptObject.transform.SetParent(root.transform, false);
            TextMeshProUGUI prompt = promptObject.AddComponent<TextMeshProUGUI>();
            Color authored = new Color(0.82f, 0.87f, 0.91f, 1f);
            prompt.color = authored;
            NarrativeTestUtility.SetPrivateField(keyboard, "promptText", prompt);

            keyboard.Refresh(
                new KeyboardSessionOptions
                {
                    Prompt = PlayerNameCommitFlow.NameTakenMessage,
                    PromptColor = NameInputUI.TakenNamePromptColor,
                },
                shiftEnabled: true,
                currentText: string.Empty,
                canSubmit: false);

            Assert.That(prompt.text, Is.EqualTo(PlayerNameCommitFlow.NameTakenMessage));
            Assert.That(prompt.color.r, Is.EqualTo(NameInputUI.TakenNamePromptColor.r).Within(0.001f));
            Assert.That(prompt.color.g, Is.EqualTo(NameInputUI.TakenNamePromptColor.g).Within(0.001f));
            Assert.That(prompt.color.b, Is.EqualTo(NameInputUI.TakenNamePromptColor.b).Within(0.001f));

            keyboard.Refresh(
                new KeyboardSessionOptions { Prompt = "What is your dog's name?" },
                shiftEnabled: true,
                currentText: string.Empty,
                canSubmit: false);

            Assert.That(prompt.color.r, Is.EqualTo(authored.r).Within(0.001f));
            Assert.That(prompt.color.g, Is.EqualTo(authored.g).Within(0.001f));
            Assert.That(prompt.color.b, Is.EqualTo(authored.b).Within(0.001f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NameEntryCopy_WarningDoesNotStickToTheNextPrompt()
    {
        GameObject root = new GameObject("NameEntryCopy");
        try
        {
            NameInputUI nameInput = root.AddComponent<NameInputUI>();
            nameInput.SetEntryCopy("Dog's Name", PlayerNameCommitFlow.NameTakenMessage, warning: true);
            nameInput.UseDefaultEntryCopy();

            Assert.That(
                NarrativeTestUtility.GetPrivateField<string>(nameInput, "keyboardPrompt"),
                Is.Not.EqualTo(PlayerNameCommitFlow.NameTakenMessage));
            Assert.That(
                NarrativeTestUtility.GetPrivateField<bool>(nameInput, "entryCopyIsWarning"),
                Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void UnconfirmedIntroBattle_RewindsToTheHouseExit()
    {
        Assert.That(IntroResumePolicy.ShouldRewindUnconfirmedIntroBattle(
            IntroResumePolicy.IntroSequenceId,
            IntroResumePolicy.AfterFirstCreatureCheckpointId,
            escapeObjectiveComplete: false), Is.True);
        Assert.That(IntroResumePolicy.EffectiveIntroCheckpointId(
            IntroResumePolicy.IntroSequenceId,
            IntroResumePolicy.AfterFirstCreatureCheckpointId,
            escapeObjectiveComplete: false), Is.EqualTo(IntroResumePolicy.LeavingRoomCheckpointId));
    }

    [Test]
    public void ConfirmedIntroBattle_KeepsTheGrassyCheckpoint()
    {
        Assert.That(IntroResumePolicy.ShouldRewindUnconfirmedIntroBattle(
            IntroResumePolicy.IntroSequenceId,
            IntroResumePolicy.AfterFirstCreatureCheckpointId,
            escapeObjectiveComplete: true), Is.False);
        Assert.That(IntroResumePolicy.EffectiveIntroCheckpointId(
            IntroResumePolicy.IntroSequenceId,
            IntroResumePolicy.AfterFirstCreatureCheckpointId,
            escapeObjectiveComplete: true), Is.EqualTo(IntroResumePolicy.AfterFirstCreatureCheckpointId));
        Assert.That(IntroResumePolicy.ShouldRewindUnconfirmedIntroBattle(
            "some_other_sequence",
            IntroResumePolicy.AfterFirstCreatureCheckpointId,
            escapeObjectiveComplete: false), Is.False);
    }
}
