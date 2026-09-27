using System.Collections;
using UnityEngine;

/// <summary>
/// Ends an anchored dialogue camera session (restore to pre-beat snapshot) and disables
/// <see cref="DialogueSpeakerCameraBridge"/> so only explicit <see cref="Step_PlayDialogue"/> steps re-enable it.
/// Use after <see cref="Step_PlayDialogue"/> with persist speaker camera enabled.
/// </summary>
[CreateAssetMenu(fileName = "Step_ReleaseDialogueCamera", menuName = "Chainbound/Narrative/Steps/Release Dialogue Camera")]
public class Step_ReleaseDialogueCamera : SequenceStep
{
    [Tooltip("Blend duration when restoring follow / snapshot.")]
    [SerializeField] private float blendTime = 0.3f;

    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        CameraShotController shotController = context.CameraShotController;
        if (shotController == null)
        {
            Debug.LogError("[Step_ReleaseDialogueCamera] CameraShotController not found!");
            onComplete?.Invoke(StepOutcome.Failed("CameraShotController not found"));
            yield break;
        }

        if (enableDebugLogs)
            Debug.Log("[Step_ReleaseDialogueCamera] Ending dialogue camera session and disabling speaker bridge.");

        yield return shotController.EndDialogueCameraSessionAndWait(blendTime);

        DialogueSpeakerCameraBridge bridge = DialogueSpeakerCameraBridge.FindInstance();
        if (bridge != null)
            bridge.enabled = false;

        onComplete?.Invoke(StepOutcome.Success());
    }
}
