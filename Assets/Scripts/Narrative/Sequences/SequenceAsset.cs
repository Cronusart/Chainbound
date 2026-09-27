using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// ScriptableObject containing a sequence of steps.
/// This is the authored content that drives scripted sequences.
/// </summary>
[CreateAssetMenu(fileName = "Sequence", menuName = "Chainbound/Narrative/Sequences/Sequence")]
public class SequenceAsset : ScriptableObject
{
    [Header("Sequence Info")]
    [Tooltip("Unique sequence ID")]
    [SerializeField] private string sequenceId = "";
    
    [Tooltip("Start checkpoint ID (for resume)")]
    [SerializeField] private string startCheckpointId = "";

    [Tooltip("Safe label used when a persisted checkpoint no longer exists after a content revision.")]
    [SerializeField] private string safeResumeLabel = "";

    [Tooltip("Increment when checkpoint/label semantics change.")]
    [Min(1)]
    [SerializeField] private int contentVersion = 1;

    [Header("Auto Start")]
    [Tooltip("When true, SequenceCatalogAutoStart may play this sequence if no sequence is running.")]
    [SerializeField] private bool autoStartWhenNoSequenceRunning;

    [Tooltip("Quest ID that must match Auto Start Lifecycle Mask. Leave empty to ignore quest state.")]
    [SerializeField] private string autoStartQuestId = "";

    [Tooltip("Quest lifecycle states that allow auto-start.")]
    [SerializeField] private NarrativeQuestLifecycleMask autoStartLifecycleMask = NarrativeQuestLifecycleMask.None;

    [Tooltip("If this profile world flag is true, auto-start is skipped (e.g. world.loomville_mart.open).")]
    [SerializeField] private string autoStartSkipWorldFlagKey = "";

    [Tooltip("If set, auto-start requires this profile world flag to be true (e.g. world.loomville_mart.open).")]
    [SerializeField] private string autoStartRequiredWorldFlagKey = "";
    
    [Header("Steps")]
    [Tooltip("Ordered list of steps to execute")]
    [SerializeField] private List<SequenceStep> steps = new List<SequenceStep>();
    
    /// <summary>Sequence ID.</summary>
    public string SequenceId => sequenceId;
    
    /// <summary>Start checkpoint ID.</summary>
    public string StartCheckpointId => startCheckpointId;

    public string SafeResumeLabel => safeResumeLabel;

    public int ContentVersion => Mathf.Max(1, contentVersion);

    public bool AutoStartWhenNoSequenceRunning => autoStartWhenNoSequenceRunning;
    public string AutoStartQuestId => autoStartQuestId != null ? autoStartQuestId.Trim() : "";
    public NarrativeQuestLifecycleMask AutoStartLifecycleMask => autoStartLifecycleMask;
    public string AutoStartSkipWorldFlagKey =>
        autoStartSkipWorldFlagKey != null ? autoStartSkipWorldFlagKey.Trim() : "";

    public string AutoStartRequiredWorldFlagKey =>
        autoStartRequiredWorldFlagKey != null ? autoStartRequiredWorldFlagKey.Trim() : "";

    public bool ShouldAutoStart(QuestService questService, ProfileWorldFlagStateService flags)
    {
        if (!autoStartWhenNoSequenceRunning)
            return false;

        if (!string.IsNullOrWhiteSpace(AutoStartRequiredWorldFlagKey) &&
            (flags == null || !flags.IsHydrated || !flags.GetFlag(AutoStartRequiredWorldFlagKey)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(AutoStartSkipWorldFlagKey) &&
            flags != null &&
            flags.IsHydrated &&
            flags.GetFlag(AutoStartSkipWorldFlagKey))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(AutoStartQuestId) ||
            autoStartLifecycleMask == NarrativeQuestLifecycleMask.None)
        {
            return true;
        }

        if (questService == null || !questService.IsHydrated)
            return false;

        QuestLifecycleState lifecycle = questService.GetQuestLifecycleState(AutoStartQuestId);
        return NarrativeQuestGuardUtility.MatchesLifecycleMask(lifecycle, autoStartLifecycleMask);
    }
    
    /// <summary>List of steps.</summary>
    public IReadOnlyList<SequenceStep> Steps => steps;
    
    /// <summary>Number of steps.</summary>
    public int StepCount => steps.Count;
    
    /// <summary>Get a specific step by index.</summary>
    public SequenceStep GetStep(int index)
    {
        if (index < 0 || index >= steps.Count) return null;
        return steps[index];
    }
    
    private void OnValidate()
    {
        contentVersion = Mathf.Max(1, contentVersion);
        var validationMessages = new List<string>();
        
        // Validate all steps
        foreach (var step in steps)
        {
            if (step != null && !step.Validate())
            {
                validationMessages.Add($"[Error] Step '{step.GetStepName()}' failed validation");
            }
        }
        
        // Build label index for validation
        var labels = new HashSet<string>();
        for (int i = 0; i < steps.Count; i++)
        {
            if (steps[i] is Step_Label labelStep)
            {
                string labelName = labelStep.GetLabelName();
                if (!string.IsNullOrEmpty(labelName))
                {
                    if (labels.Contains(labelName))
                    {
                        validationMessages.Add($"[Error] Duplicate label '{labelName}' at step {i}");
                    }
                    else
                    {
                        labels.Add(labelName);
                    }
                }
            }
        }
        
        // Validate jump targets
        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            if (step == null) continue;
            
            // Check JumpToLabel steps
            if (step is Step_JumpToLabel jumpStep)
            {
                // Use reflection or add public method to get target label
                var field = typeof(Step_JumpToLabel).GetField("targetLabel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    string targetLabel = field.GetValue(jumpStep) as string;
                    if (!string.IsNullOrEmpty(targetLabel) && !labels.Contains(targetLabel))
                    {
                        validationMessages.Add($"[Error] Step {i}: JumpToLabel targets missing label '{targetLabel}'");
                    }
                }
            }
            
            // Check If steps
            if (step is Step_If ifStep)
            {
                var trueField = typeof(Step_If).GetField("trueLabel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var falseField = typeof(Step_If).GetField("falseLabel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (trueField != null)
                {
                    string trueLabel = trueField.GetValue(ifStep) as string;
                    if (!string.IsNullOrEmpty(trueLabel) && !labels.Contains(trueLabel))
                    {
                        validationMessages.Add($"[Error] Step {i}: If step targets missing label '{trueLabel}'");
                    }
                }
                
                if (falseField != null)
                {
                    string falseLabel = falseField.GetValue(ifStep) as string;
                    if (!string.IsNullOrEmpty(falseLabel) && !labels.Contains(falseLabel))
                    {
                        validationMessages.Add($"[Error] Step {i}: If step targets missing label '{falseLabel}'");
                    }
                }
            }
            
            // Check Branch steps
            if (step is Step_Branch branchStep)
            {
                var casesField = typeof(Step_Branch).GetField("cases", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (casesField != null)
                {
                    var cases = casesField.GetValue(branchStep) as System.Collections.IList;
                    if (cases != null)
                    {
                        foreach (var branchCase in cases)
                        {
                            var labelField = branchCase.GetType().GetField("label");
                            if (labelField != null)
                            {
                                string caseLabel = labelField.GetValue(branchCase) as string;
                                if (!string.IsNullOrEmpty(caseLabel) && !labels.Contains(caseLabel))
                                {
                                    validationMessages.Add($"[Error] Step {i}: Branch step targets missing label '{caseLabel}'");
                                }
                            }
                        }
                    }
                }
            }
            
            // Check Switch steps
            if (step is Step_Switch switchStep)
            {
                var casesField = typeof(Step_Switch).GetField("cases", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var defaultField = typeof(Step_Switch).GetField("defaultLabel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (casesField != null)
                {
                    var cases = casesField.GetValue(switchStep) as System.Collections.IList;
                    if (cases != null)
                    {
                        foreach (var switchCase in cases)
                        {
                            var labelField = switchCase.GetType().GetField("label");
                            if (labelField != null)
                            {
                                string caseLabel = labelField.GetValue(switchCase) as string;
                                if (!string.IsNullOrEmpty(caseLabel) && !labels.Contains(caseLabel))
                                {
                                    validationMessages.Add($"[Error] Step {i}: Switch step targets missing label '{caseLabel}'");
                                }
                            }
                        }
                    }
                }
                
                if (defaultField != null)
                {
                    string defaultLabel = defaultField.GetValue(switchStep) as string;
                    if (!string.IsNullOrEmpty(defaultLabel) && !labels.Contains(defaultLabel))
                    {
                        validationMessages.Add($"[Error] Step {i}: Switch step targets missing default label '{defaultLabel}'");
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(safeResumeLabel) && !labels.Contains(safeResumeLabel))
            validationMessages.Add($"[Error] Safe resume label '{safeResumeLabel}' does not exist");

        List<SequenceParallelTrackValidation.Issue> trackIssues = SequenceParallelTrackValidation.CollectIssues(this);
        for (int i = 0; i < trackIssues.Count; i++)
        {
            SequenceParallelTrackValidation.Issue issue = trackIssues[i];
            validationMessages.Add($"[{issue.Severity}] Step {issue.StepIndex}: {issue.Message}");
        }
        
        // Log errors
        if (validationMessages.Count > 0)
        {
            Debug.LogWarning($"[SequenceAsset] '{name}' validation found {validationMessages.Count} issue(s):\n" +
                string.Join("\n", validationMessages.Select((message, idx) => $"  {idx + 1}. {message}")));
        }
    }
}

