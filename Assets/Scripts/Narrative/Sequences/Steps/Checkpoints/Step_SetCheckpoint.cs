using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Step that sets a checkpoint for resume.
/// </summary>
[CreateAssetMenu(fileName = "Step_SetCheckpoint", menuName = "Chainbound/Narrative/Steps/Checkpoints/Set Checkpoint")]
public class Step_SetCheckpoint : SequenceStep
{
    [Header("Checkpoint")]
    [Tooltip("Checkpoint ID (optional - uses label if empty)")]
    [SerializeField] private string checkpointId = "";
    
    [Tooltip("Checkpoint label (alternative to checkpointId)")]
    [SerializeField] private string checkpointLabel = "";
    
    [Tooltip("Save checkpoint to persistent state")]
    [SerializeField] private bool saveToState = true;

    [Header("Resume Placement")]
    [Tooltip("Gameplay scene to load on continue. Used when local currentSceneId was cleared.")]
    [SerializeField] private string resumeSceneId = "";

    [Tooltip("Spawn id to use with resumeSceneId.")]
    [SerializeField] private string resumeSpawnId = "";

    [Header("Resume World Flags")]
    [Tooltip("Legacy fallback only. Continue restores rail flags from the checkpoint row snapshot. Leave this empty on new checkpoints.")]
    [SerializeField] private List<ResumeProfileWorldFlag> resumeProfileWorldFlags =
        new List<ResumeProfileWorldFlag>();

    [Serializable]
    public sealed class ResumeProfileWorldFlag
    {
        public string flagKey = "";
        public bool flagValue;
    }

    public string CheckpointId => checkpointId ?? "";
    public string CheckpointLabel => checkpointLabel ?? "";
    public string ResumeSceneId => resumeSceneId != null ? resumeSceneId.Trim() : "";
    public string ResumeSpawnId => resumeSpawnId != null ? resumeSpawnId.Trim() : "";
    public IReadOnlyList<ResumeProfileWorldFlag> ResumeProfileWorldFlags => resumeProfileWorldFlags;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        SequenceRunner runner = SequenceRunner.Instance;
        if (runner == null)
        {
            Debug.LogError("[Step_SetCheckpoint] SequenceRunner not found!");
            onComplete?.Invoke(StepOutcome.Failed("SequenceRunner not found"));
            yield break;
        }
        
        // Get current step index
        int currentIndex = runner.CurrentStepIndex;
        
        string sceneId = ResumeSceneId;
        string spawnId = ResumeSpawnId;

        if (string.IsNullOrWhiteSpace(sceneId) &&
            context.Variables.TryGet<string>("currentSceneId", out string sceneVar))
        {
            sceneId = sceneVar;
        }
        if (string.IsNullOrWhiteSpace(spawnId) &&
            context.Variables.TryGet<string>("currentSpawnId", out string spawnVar))
        {
            spawnId = spawnVar;
        }
        
        // Set checkpoint in persistent state
        if (saveToState)
        {
            runner.SetCheckpoint(checkpointId, checkpointLabel, sceneId, spawnId);
            
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_SetCheckpoint] Checkpoint set: id={checkpointId}, label={checkpointLabel}, step={currentIndex}");
            }
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(checkpointId) && string.IsNullOrEmpty(checkpointLabel))
        {
            Debug.LogWarning("[Step_SetCheckpoint] Both checkpoint ID and label are empty!");
            return false;
        }
        return true;
    }
}

