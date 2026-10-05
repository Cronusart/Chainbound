using NUnit.Framework;
using UnityEditor;

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
}
