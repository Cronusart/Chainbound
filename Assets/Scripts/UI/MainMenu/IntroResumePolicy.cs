using System;

/// <summary>
/// Routing and resume rules for the opening sequence.
/// A profile with no committed display name is a fresh start on the existing
/// profile row: the menu offers New Game, and any intro checkpoint is replayed
/// from the beginning instead of a later safe label.
/// </summary>
public static class IntroResumePolicy
{
    public const string IntroSequenceId = "chainbound_intro_sequence";

    public static bool HasCommittedDisplayName(string displayName)
    {
        return !string.IsNullOrWhiteSpace(displayName);
    }

    /// <summary>
    /// Continue is only for a profile that has started and has a committed name.
    /// A nameless row stays on the New Game presentation so the player cannot
    /// resume past name entry, and New Game updates that same profile.
    /// </summary>
    public static bool ShowContinue(bool hasProfile, bool hasStartedGame, string displayName)
    {
        return hasProfile && hasStartedGame && HasCommittedDisplayName(displayName);
    }

    public static bool ShouldRouteToIntro(
        bool hasStartedGame,
        bool introCinematicComplete,
        bool introQuestComplete,
        string displayName)
    {
        if (!HasCommittedDisplayName(displayName))
            return true;

        return !hasStartedGame || !introCinematicComplete || !introQuestComplete;
    }

    public static bool ShouldReplayIntroFromBeginning(string displayName)
    {
        return !HasCommittedDisplayName(displayName);
    }

    /// <summary>
    /// The pre-name cinematic must still play when the name was never committed.
    /// The server cinematic flag is monotonic and is left alone; this only
    /// decides whether the client skips the shot.
    /// </summary>
    public static bool ShouldSuppressCompletedCinematic(
        bool suppressWhenProfileComplete,
        bool introCinematicComplete,
        string displayName)
    {
        return suppressWhenProfileComplete &&
               introCinematicComplete &&
               HasCommittedDisplayName(displayName);
    }

    /// <summary>
    /// Label completions before the first authored checkpoint must not be saved.
    /// An empty checkpoint id is what previously resumed at the safe label.
    /// </summary>
    public static bool ShouldPersistLabelProgress(string checkpointId)
    {
        return !string.IsNullOrWhiteSpace(checkpointId);
    }

    /// <summary>
    /// A blank display name must not honor a resume point after the name commit.
    /// Applies to every resume entry, not only the main menu.
    /// </summary>
    public static bool ShouldHoldNamelessResume(
        string sequenceId,
        string displayName,
        int nameCommitStepIndex,
        int resumeStepIndex)
    {
        if (!string.Equals(sequenceId, IntroSequenceId, StringComparison.Ordinal))
            return false;
        if (HasCommittedDisplayName(displayName))
            return false;
        if (nameCommitStepIndex < 0 || resumeStepIndex < 0)
            return false;
        return resumeStepIndex > nameCommitStepIndex;
    }

    public static IntroCheckpointResume HoldNamelessPastNameCommit(
        IntroCheckpointResume choice,
        string sequenceId,
        string displayName,
        int nameCommitStepIndex,
        int resumeLabelStepIndex)
    {
        int effectiveIndex = choice.StepIndex;
        if (!choice.RestartAtBeginning && resumeLabelStepIndex > effectiveIndex)
            effectiveIndex = resumeLabelStepIndex;

        if (!ShouldHoldNamelessResume(sequenceId, displayName, nameCommitStepIndex, effectiveIndex))
            return choice;

        return new IntroCheckpointResume(
            0,
            "",
            true,
            "Display name is unset; not resuming past the name commit.");
    }

    /// <summary>
    /// A nameless intro that is about to run from step 0 must forget execute-once
    /// progress in memory. Otherwise the name prompt, which is execute-once, is
    /// skipped and the player never gets a chance to commit a name.
    /// </summary>
    public static bool ShouldDiscardNamelessStepProgress(
        string sequenceId,
        string displayName,
        int resumeStepIndex)
    {
        return string.Equals(sequenceId, IntroSequenceId, StringComparison.Ordinal) &&
               !HasCommittedDisplayName(displayName) &&
               resumeStepIndex <= 0;
    }

    /// <summary>
    /// The authored resume scene wins over a scene id already stored on the
    /// checkpoint row, so a content fix applies to saves written earlier.
    /// </summary>
    public static void PreferAuthoredResumePlacement(
        string savedSceneId,
        string savedSpawnId,
        string authoredSceneId,
        string authoredSpawnId,
        out string sceneId,
        out string spawnId)
    {
        sceneId = !string.IsNullOrWhiteSpace(authoredSceneId)
            ? authoredSceneId.Trim()
            : (savedSceneId ?? "").Trim();
        spawnId = !string.IsNullOrWhiteSpace(authoredSpawnId)
            ? authoredSpawnId.Trim()
            : (savedSpawnId ?? "").Trim();
    }

    public static IntroCheckpointResume Choose(
        string checkpointId,
        bool checkpointIdFound,
        int checkpointIdIndex,
        string resolvedCheckpointLabel,
        string checkpointLabel,
        bool checkpointLabelFound,
        int checkpointLabelIndex,
        string safeResumeLabel,
        bool safeLabelFound,
        int safeLabelIndex)
    {
        if (!string.IsNullOrWhiteSpace(checkpointId) && checkpointIdFound)
        {
            return new IntroCheckpointResume(
                checkpointIdIndex,
                resolvedCheckpointLabel ?? "",
                false,
                "");
        }

        if (!string.IsNullOrWhiteSpace(checkpointLabel) && checkpointLabelFound)
        {
            string reason = !string.IsNullOrWhiteSpace(checkpointId)
                ? $"Checkpoint '{checkpointId}' no longer exists; recovered with label '{checkpointLabel}'."
                : "";
            return new IntroCheckpointResume(checkpointLabelIndex, checkpointLabel, false, reason);
        }

        // No authored checkpoint was ever recorded. Do not jump to the safe label.
        if (string.IsNullOrWhiteSpace(checkpointId) && string.IsNullOrWhiteSpace(checkpointLabel))
        {
            return new IntroCheckpointResume(
                0,
                "",
                true,
                "No authored checkpoint yet; restarting at sequence beginning.");
        }

        if (!string.IsNullOrWhiteSpace(safeResumeLabel) && safeLabelFound)
        {
            return new IntroCheckpointResume(
                safeLabelIndex,
                safeResumeLabel,
                false,
                $"Checkpoint '{checkpointId}' could not be resolved; restarting at safe label '{safeResumeLabel}'.");
        }

        return new IntroCheckpointResume(
            0,
            "",
            true,
            $"Checkpoint '{checkpointId}' could not be resolved; restarting at sequence beginning.");
    }
}

public readonly struct IntroCheckpointResume
{
    public IntroCheckpointResume(int stepIndex, string resumeLabel, bool restartAtBeginning, string recoveryReason)
    {
        StepIndex = stepIndex;
        ResumeLabel = resumeLabel ?? "";
        RestartAtBeginning = restartAtBeginning;
        RecoveryReason = recoveryReason ?? "";
    }

    public int StepIndex { get; }
    public string ResumeLabel { get; }
    public bool RestartAtBeginning { get; }
    public string RecoveryReason { get; }
}
