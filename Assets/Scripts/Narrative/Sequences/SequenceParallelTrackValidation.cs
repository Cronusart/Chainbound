using System.Collections.Generic;

/// <summary>
/// Validation helpers for authored child-track steps inside a sequence.
/// </summary>
public static class SequenceParallelTrackValidation
{
    public enum IssueSeverity
    {
        Warning,
        Error
    }

    public readonly struct Issue
    {
        public Issue(IssueSeverity severity, int stepIndex, string message)
        {
            Severity = severity;
            StepIndex = stepIndex;
            Message = message;
        }

        public IssueSeverity Severity { get; }
        public int StepIndex { get; }
        public string Message { get; }
    }

    public static List<Issue> CollectIssues(SequenceAsset sequence)
    {
        var issues = new List<Issue>();
        if (sequence == null)
            return issues;

        var declaredTrackIds = new HashSet<string>();

        for (int i = 0; i < sequence.StepCount; i++)
        {
            SequenceStep step = sequence.GetStep(i);
            if (step is Step_RunTrack runTrackStep)
            {
                string trackId = runTrackStep.TrackId;
                if (string.IsNullOrWhiteSpace(trackId))
                {
                    issues.Add(new Issue(IssueSeverity.Error, i, "Run Track step has an empty trackId."));
                    continue;
                }

                if (!declaredTrackIds.Add(trackId))
                {
                    issues.Add(new Issue(IssueSeverity.Error, i, $"Duplicate child track id '{trackId}'."));
                }

                if (runTrackStep.BlockingMode == TrackBlockingMode.WaitForComplete)
                {
                    issues.Add(new Issue(IssueSeverity.Warning, i, $"Track '{trackId}' is configured as blocking."));
                }
            }
        }

        for (int i = 0; i < sequence.StepCount; i++)
        {
            SequenceStep step = sequence.GetStep(i);
            if (step is Step_WaitForTrack waitForTrackStep
                && waitForTrackStep.CompletionMode == TrackWaitCompletionMode.Completed
                && !string.IsNullOrWhiteSpace(waitForTrackStep.TrackId)
                && !declaredTrackIds.Contains(waitForTrackStep.TrackId))
            {
                issues.Add(new Issue(
                    IssueSeverity.Error,
                    i,
                    $"Wait For Track references unknown track id '{waitForTrackStep.TrackId}'."));
            }

            if (step is Step_StopTrack stopTrackStep
                && !string.IsNullOrWhiteSpace(stopTrackStep.TrackId)
                && !declaredTrackIds.Contains(stopTrackStep.TrackId))
            {
                issues.Add(new Issue(
                    IssueSeverity.Error,
                    i,
                    $"Stop Track references unknown track id '{stopTrackStep.TrackId}'."));
            }
        }

        return issues;
    }
}
