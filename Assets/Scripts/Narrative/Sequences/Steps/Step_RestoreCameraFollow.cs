using System.Collections;
using UnityEngine;

/// <summary>
/// Step that restores camera to normal follow behavior.
/// </summary>
[CreateAssetMenu(fileName = "Step_RestoreCameraFollow", menuName = "Chainbound/Narrative/Steps/Restore Camera Follow")]
public class Step_RestoreCameraFollow : SequenceStep
{
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        CameraShotController shotController = context.CameraShotController;
        if (shotController == null)
        {
            Debug.LogError("[Step_RestoreCameraFollow] CameraShotController not found!");
            onComplete?.Invoke(StepOutcome.Failed("CameraShotController not found"));
            yield break;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log("[Step_RestoreCameraFollow] Restoring camera follow");
        }
        
        yield return shotController.RestoreCameraFollowAndWait(0.3f);
        
        onComplete?.Invoke(StepOutcome.Success());
    }
}

