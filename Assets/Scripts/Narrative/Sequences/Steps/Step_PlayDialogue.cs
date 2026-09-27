using System.Collections;
using UnityEngine;

/// <summary>
/// Step that plays a dialogue sequence.
/// Waits for dialogue to complete before continuing.
/// </summary>
[CreateAssetMenu(fileName = "Step_PlayDialogue", menuName = "Chainbound/Narrative/Steps/Play Dialogue")]
public class Step_PlayDialogue : SequenceStep
{
    [Header("Dialogue")]
    [Tooltip("Dialogue sequence to play")]
    [SerializeField] private DialogueSequenceSO dialogueSequence;
    
    [Tooltip("Lock player input (movement, jump, interaction) while dialogue plays. " +
             "Set false for topical messages where player can roam and dialogue auto-confirms.")]
    [SerializeField] private bool lockInputDuringDialogue = true;

    [Header("Choice result (optional)")]
    [Tooltip("When this dialogue contains choices, store the chosen value in this persistent sequence variable.")]
    [SerializeField] private string choiceVariableKey = "";

    [Header("Speaker camera (optional)")]
    [Tooltip("When true, enables DialogueSpeakerCameraBridge on GameServices for this dialogue only " +
             "(component can stay disabled on the prefab). Disables the bridge after dialogue unless Persist is enabled.")]
    [SerializeField] private bool useSpeakerCameraBridge;

    [Tooltip("When true (and Use Speaker Camera Bridge), dialogue end does not restore the camera; the bridge stays enabled " +
             "until Step_ReleaseDialogueCamera. Use for one-off beats with steps between dialogues.")]
    [SerializeField] private bool persistSpeakerCameraAfterDialogue;
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        if (dialogueSequence == null)
        {
            Debug.LogError("[Step_PlayDialogue] Dialogue sequence is null!");
            onComplete?.Invoke(StepOutcome.Failed("Dialogue sequence is null"));
            yield break;
        }
        
        DialogueService dialogueService = context.DialogueService;
        if (dialogueService == null)
        {
            Debug.LogError("[Step_PlayDialogue] DialogueService not found!");
            onComplete?.Invoke(StepOutcome.Failed("DialogueService not found"));
            yield break;
        }

        DialogueSpeakerCameraBridge speakerBridge = null;
        if (useSpeakerCameraBridge)
        {
            speakerBridge = DialogueSpeakerCameraBridge.FindInstance();
            if (speakerBridge != null)
            {
                speakerBridge.enabled = true;
                speakerBridge.SetPersistAfterDialogueEnds(persistSpeakerCameraAfterDialogue);
                if (enableDebugLogs)
                    Debug.Log("[Step_PlayDialogue] DialogueSpeakerCameraBridge enabled for this dialogue.");
            }
            else if (enableDebugLogs)
            {
                Debug.LogWarning("[Step_PlayDialogue] useSpeakerCameraBridge is true but no DialogueSpeakerCameraBridge found in scene.");
            }
        }

        try
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_PlayDialogue] Playing dialogue: {dialogueSequence.name}");
            }
            
            // Play dialogue and wait for completion
            Coroutine dialogueCoroutine = dialogueService.PlayDialogue(dialogueSequence, lockInputDuringDialogue);
            if (dialogueCoroutine != null)
            {
                yield return dialogueCoroutine;
            }
            
            // Wait until dialogue is complete
            while (dialogueService.IsPlaying)
            {
                yield return null;
            }

            if (!string.IsNullOrWhiteSpace(choiceVariableKey))
            {
                context.Variables.Set(choiceVariableKey, dialogueService.LastChoiceValue ?? string.Empty, VariableScope.Global);
            }
            
            if (enableDebugLogs)
            {
                Debug.Log($"[Step_PlayDialogue] Dialogue complete: {dialogueSequence.name}");
            }
            
            onComplete?.Invoke(StepOutcome.Success());
        }
        finally
        {
            if (useSpeakerCameraBridge && speakerBridge != null && !persistSpeakerCameraAfterDialogue)
            {
                speakerBridge.enabled = false;
                if (enableDebugLogs)
                    Debug.Log("[Step_PlayDialogue] DialogueSpeakerCameraBridge disabled after dialogue.");
            }
        }
    }
    
    public override bool Validate()
    {
        if (dialogueSequence == null)
        {
            Debug.LogWarning("[Step_PlayDialogue] Dialogue sequence is not assigned!");
            return false;
        }
        return true;
    }
}

