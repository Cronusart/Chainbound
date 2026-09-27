#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Minimal editor helpers for authoring and validating child narrative tracks.
/// </summary>
public static class NarrativeParallelTrackAuthoringEditor
{
    private const string CreateStarterMenuPath = "Assets/Create/Chainbound/Narrative/Tracks/Parallel Track Starter";
    private const string ValidateTracksMenuPath = "Tools/Chainbound/Narrative/Validate Parallel Tracks";

    public readonly struct StarterAssets
    {
        public StarterAssets(SequenceAsset childSequence, Step_RunTrack runTrackStep, string trackId, string sequencePath, string runTrackStepPath)
        {
            ChildSequence = childSequence;
            RunTrackStep = runTrackStep;
            TrackId = trackId ?? "";
            SequencePath = sequencePath ?? "";
            RunTrackStepPath = runTrackStepPath ?? "";
        }

        public SequenceAsset ChildSequence { get; }
        public Step_RunTrack RunTrackStep { get; }
        public string TrackId { get; }
        public string SequencePath { get; }
        public string RunTrackStepPath { get; }
    }

    public readonly struct ValidationEntry
    {
        public ValidationEntry(SequenceAsset sequence, string assetPath, List<SequenceParallelTrackValidation.Issue> issues)
        {
            Sequence = sequence;
            AssetPath = assetPath ?? "";
            Issues = issues ?? new List<SequenceParallelTrackValidation.Issue>();
        }

        public SequenceAsset Sequence { get; }
        public string AssetPath { get; }
        public IReadOnlyList<SequenceParallelTrackValidation.Issue> Issues { get; }
    }

    public readonly struct ValidationReport
    {
        public ValidationReport(List<ValidationEntry> entries)
        {
            Entries = entries ?? new List<ValidationEntry>();

            int totalIssues = 0;
            for (int i = 0; i < Entries.Count; i++)
                totalIssues += Entries[i].Issues.Count;

            TotalIssues = totalIssues;
        }

        public IReadOnlyList<ValidationEntry> Entries { get; }
        public int TotalIssues { get; }
    }

    [MenuItem(CreateStarterMenuPath, false, 220)]
    public static void CreateParallelTrackStarterFromMenu()
    {
        string folderPath = GetSelectedFolderPath();
        string baseName = GetSuggestedBaseName();
        StarterAssets assets = CreateStarterAssets(folderPath, baseName);

        Selection.objects = new Object[] { assets.RunTrackStep, assets.ChildSequence };
        EditorGUIUtility.PingObject(assets.RunTrackStep);
        Debug.Log(
            $"[NarrativeParallelTrackAuthoringEditor] Created child track starter '{assets.TrackId}' at '{folderPath}'.",
            assets.RunTrackStep);
    }

    [MenuItem(CreateStarterMenuPath, true)]
    private static bool ValidateCreateParallelTrackStarterFromMenu()
    {
        return !string.IsNullOrWhiteSpace(GetSelectedFolderPath());
    }

    [MenuItem(ValidateTracksMenuPath, false, 310)]
    public static void ValidateParallelTracksFromMenu()
    {
        ValidationReport report = ValidateAllSequences();
        if (report.TotalIssues == 0)
        {
            Debug.Log("[NarrativeParallelTrackAuthoringEditor] No parallel-track issues found.");
            return;
        }

        Debug.LogWarning(BuildReportMessage(report));
    }

    public static StarterAssets CreateStarterAssets(string folderPath, string baseName)
    {
        string resolvedFolderPath = ResolveFolderPath(folderPath);
        string slug = Slugify(baseName);
        if (string.IsNullOrWhiteSpace(slug))
            slug = "parallel_track";

        string sequencePath = AssetDatabase.GenerateUniqueAssetPath($"{resolvedFolderPath}/{slug}_sequence.asset");
        SequenceAsset childSequence = ScriptableObject.CreateInstance<SequenceAsset>();
        AssetDatabase.CreateAsset(childSequence, sequencePath);

        SerializedObject childSequenceSerialized = new SerializedObject(childSequence);
        SerializedProperty sequenceIdProp = childSequenceSerialized.FindProperty("sequenceId");
        if (sequenceIdProp != null)
            sequenceIdProp.stringValue = $"{slug}_sequence";
        childSequenceSerialized.ApplyModifiedPropertiesWithoutUndo();

        string stepPath = AssetDatabase.GenerateUniqueAssetPath($"{resolvedFolderPath}/Step_RunTrack_{slug}.asset");
        Step_RunTrack runTrackStep = ScriptableObject.CreateInstance<Step_RunTrack>();
        AssetDatabase.CreateAsset(runTrackStep, stepPath);

        SerializedObject runTrackSerialized = new SerializedObject(runTrackStep);
        SetString(runTrackSerialized, "stepName", $"Run {ObjectNames.NicifyVariableName(slug)}");
        SetString(runTrackSerialized, "trackId", slug);
        SetObjectReference(runTrackSerialized, "sequence", childSequence);
        SetEnumIndex(runTrackSerialized, "trackType", (int)SequenceTrackType.Ambient);
        SetEnumIndex(runTrackSerialized, "contextMode", (int)TrackContextMode.Child);
        SetEnumIndex(runTrackSerialized, "cancelPolicy", (int)TrackCancelPolicy.StopOnParentEnd);
        SetEnumIndex(runTrackSerialized, "blockingMode", (int)TrackBlockingMode.NonBlocking);
        runTrackSerialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(childSequence);
        EditorUtility.SetDirty(runTrackStep);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new StarterAssets(childSequence, runTrackStep, slug, sequencePath, stepPath);
    }

    public static ValidationReport ValidateAllSequences()
    {
        string[] guids = AssetDatabase.FindAssets("t:SequenceAsset");
        var entries = new List<ValidationEntry>();

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            SequenceAsset sequence = AssetDatabase.LoadAssetAtPath<SequenceAsset>(assetPath);
            if (sequence == null)
                continue;

            List<SequenceParallelTrackValidation.Issue> issues = SequenceParallelTrackValidation.CollectIssues(sequence);
            if (issues.Count > 0)
                entries.Add(new ValidationEntry(sequence, assetPath, issues));
        }

        return new ValidationReport(entries);
    }

    public static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "parallel_track";

        var builder = new StringBuilder(value.Length);
        bool lastWasSeparator = false;

        for (int i = 0; i < value.Length; i++)
        {
            char character = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        string slug = builder.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? "parallel_track" : slug;
    }

    private static string BuildReportMessage(ValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"[NarrativeParallelTrackAuthoringEditor] Found {report.TotalIssues} parallel-track issue(s) across {report.Entries.Count} sequence(s):");

        for (int i = 0; i < report.Entries.Count; i++)
        {
            ValidationEntry entry = report.Entries[i];
            builder.AppendLine($"- {entry.AssetPath}");

            for (int j = 0; j < entry.Issues.Count; j++)
            {
                SequenceParallelTrackValidation.Issue issue = entry.Issues[j];
                builder.AppendLine($"  [{issue.Severity}] Step {issue.StepIndex}: {issue.Message}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string GetSelectedFolderPath()
    {
        if (Selection.activeObject == null)
            return "Assets";

        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrWhiteSpace(selectedPath))
            return "Assets";

        if (AssetDatabase.IsValidFolder(selectedPath))
            return selectedPath;

        string directory = Path.GetDirectoryName(selectedPath);
        if (string.IsNullOrWhiteSpace(directory))
            return "Assets";

        return directory.Replace("\\", "/");
    }

    private static string GetSuggestedBaseName()
    {
        if (Selection.activeObject == null)
            return "Parallel Track";

        return Selection.activeObject.name;
    }

    private static string ResolveFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return "Assets";

        return folderPath.Replace("\\", "/");
    }

    private static void SetEnumIndex(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.enumValueIndex = value;
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }
}
#endif
