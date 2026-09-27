using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class SequenceParallelTrackAuthoringTests
{
    private readonly List<Object> cleanup = new List<Object>();
    private readonly List<string> assetPathsToDelete = new List<string>();

    [TearDown]
    public void TearDown()
    {
        for (int i = assetPathsToDelete.Count - 1; i >= 0; i--)
        {
            if (AssetDatabase.DeleteAsset(assetPathsToDelete[i]))
                continue;
        }

        assetPathsToDelete.Clear();

        for (int i = cleanup.Count - 1; i >= 0; i--)
        {
            if (cleanup[i] != null)
                Object.DestroyImmediate(cleanup[i]);
        }

        cleanup.Clear();
    }

    [Test]
    public void CollectIssues_FindsDuplicateUnknownAndBlockingTrackProblems()
    {
        Step_RunTrack firstRunTrack = ScriptableObject.CreateInstance<Step_RunTrack>();
        Step_RunTrack duplicateRunTrack = ScriptableObject.CreateInstance<Step_RunTrack>();
        Step_RunTrack blockingRunTrack = ScriptableObject.CreateInstance<Step_RunTrack>();
        Step_WaitForTrack waitForTrack = ScriptableObject.CreateInstance<Step_WaitForTrack>();
        Step_StopTrack stopTrack = ScriptableObject.CreateInstance<Step_StopTrack>();
        cleanup.Add(firstRunTrack);
        cleanup.Add(duplicateRunTrack);
        cleanup.Add(blockingRunTrack);
        cleanup.Add(waitForTrack);
        cleanup.Add(stopTrack);

        NarrativeTestUtility.SetPrivateField(firstRunTrack, "trackId", "ambient_track");
        NarrativeTestUtility.SetPrivateField(duplicateRunTrack, "trackId", "ambient_track");
        NarrativeTestUtility.SetPrivateField(blockingRunTrack, "trackId", "blocking_track");
        NarrativeTestUtility.SetPrivateField(blockingRunTrack, "blockingMode", TrackBlockingMode.WaitForComplete);
        NarrativeTestUtility.SetPrivateField(waitForTrack, "trackId", "missing_track");
        NarrativeTestUtility.SetPrivateField(waitForTrack, "completionMode", TrackWaitCompletionMode.Completed);
        NarrativeTestUtility.SetPrivateField(stopTrack, "trackId", "missing_track");

        SequenceAsset sequence = ScriptableObject.CreateInstance<SequenceAsset>();
        cleanup.Add(sequence);
        NarrativeTestUtility.SetPrivateField(sequence, "steps", new List<SequenceStep>
        {
            firstRunTrack,
            duplicateRunTrack,
            blockingRunTrack,
            waitForTrack,
            stopTrack
        });

        List<SequenceParallelTrackValidation.Issue> issues = SequenceParallelTrackValidation.CollectIssues(sequence);

        Assert.That(issues.Exists(issue => issue.Severity == SequenceParallelTrackValidation.IssueSeverity.Error && issue.Message.Contains("Duplicate child track id")));
        Assert.That(issues.Exists(issue => issue.Severity == SequenceParallelTrackValidation.IssueSeverity.Error && issue.Message.Contains("Wait For Track references unknown track id")));
        Assert.That(issues.Exists(issue => issue.Severity == SequenceParallelTrackValidation.IssueSeverity.Error && issue.Message.Contains("Stop Track references unknown track id")));
        Assert.That(issues.Exists(issue => issue.Severity == SequenceParallelTrackValidation.IssueSeverity.Warning && issue.Message.Contains("configured as blocking")));
    }

    [Test]
    public void CreateStarterAssets_UsesExpectedDefaults()
    {
        string folderPath = $"Assets/TempNarrativeParallelTrackTests_{System.Guid.NewGuid():N}";
        AssetDatabase.CreateFolder("Assets", folderPath.Substring("Assets/".Length));
        assetPathsToDelete.Add(folderPath);

        NarrativeParallelTrackAuthoringEditor.StarterAssets assets =
            NarrativeParallelTrackAuthoringEditor.CreateStarterAssets(folderPath, "Lab Demo Parallel");

        assetPathsToDelete.Add(assets.SequencePath);
        assetPathsToDelete.Add(assets.RunTrackStepPath);

        Assert.That(assets.ChildSequence, Is.Not.Null);
        Assert.That(assets.RunTrackStep, Is.Not.Null);
        Assert.That(assets.TrackId, Is.EqualTo("lab_demo_parallel"));
        Assert.That(assets.RunTrackStep.TrackId, Is.EqualTo("lab_demo_parallel"));
        Assert.That(assets.RunTrackStep.BlockingMode, Is.EqualTo(TrackBlockingMode.NonBlocking));
        Assert.That(assets.RunTrackStep.CancelPolicy, Is.EqualTo(TrackCancelPolicy.StopOnParentEnd));
        Assert.That(assets.RunTrackStep.ContextMode, Is.EqualTo(TrackContextMode.Child));
        Assert.That(assets.RunTrackStep.Sequence, Is.EqualTo(assets.ChildSequence));
        Assert.That(assets.ChildSequence.SequenceId, Is.EqualTo("lab_demo_parallel_sequence"));
    }

    [Test]
    public void ValidateAllSequences_ReportsSequencesWithTrackIssues()
    {
        Step_RunTrack duplicateA = ScriptableObject.CreateInstance<Step_RunTrack>();
        Step_RunTrack duplicateB = ScriptableObject.CreateInstance<Step_RunTrack>();
        cleanup.Add(duplicateA);
        cleanup.Add(duplicateB);
        NarrativeTestUtility.SetPrivateField(duplicateA, "trackId", "duplicate");
        NarrativeTestUtility.SetPrivateField(duplicateB, "trackId", "duplicate");

        SequenceAsset sequence = ScriptableObject.CreateInstance<SequenceAsset>();
        cleanup.Add(sequence);
        NarrativeTestUtility.SetPrivateField(sequence, "sequenceId", "validation_sequence");
        NarrativeTestUtility.SetPrivateField(sequence, "steps", new List<SequenceStep> { duplicateA, duplicateB });

        string folderPath = $"Assets/TempNarrativeValidationTests_{System.Guid.NewGuid():N}";
        AssetDatabase.CreateFolder("Assets", folderPath.Substring("Assets/".Length));
        assetPathsToDelete.Add(folderPath);

        string sequencePath = $"{folderPath}/validation_sequence.asset";
        AssetDatabase.CreateAsset(sequence, sequencePath);
        assetPathsToDelete.Add(sequencePath);
        cleanup.Remove(sequence);

        NarrativeParallelTrackAuthoringEditor.ValidationReport report =
            NarrativeParallelTrackAuthoringEditor.ValidateAllSequences();

        Assert.That(report.TotalIssues, Is.GreaterThan(0));
        Assert.That(report.Entries.Count, Is.GreaterThan(0));
    }
}
