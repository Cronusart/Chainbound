using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that defines a label (jump target).
/// This step does nothing during execution - it's just a marker.
/// </summary>
[CreateAssetMenu(fileName = "Step_Label", menuName = "Chainbound/Narrative/Steps/Control Flow/Label")]
public class Step_Label : SequenceStep
{
    [Header("Label")]
    [Tooltip("Label name (must be unique within sequence)")]
    [SerializeField] private string labelName = "";
    
    /// <summary>
    /// Get the label name.
    /// </summary>
    public string GetLabelName() => labelName;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_Label] Label marker: {labelName}");
        }
        
        // Labels don't do anything - they're just markers
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(labelName))
        {
            Debug.LogWarning("[Step_Label] Label name is not set!");
            return false;
        }
        return true;
    }
}

