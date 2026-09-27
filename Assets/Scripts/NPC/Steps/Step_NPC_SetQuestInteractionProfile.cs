using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that assigns or clears a quest interaction profile override on an NPC's
/// QuestProgressInteractionBridge host.
/// </summary>
[CreateAssetMenu(
    fileName = "Step_NPC_SetQuestInteractionProfile",
    menuName = "Chainbound/Narrative/Steps/NPC/Set Quest Interaction Profile")]
public class Step_NPC_SetQuestInteractionProfile : SequenceStep
{
    public enum ProfileAssignmentMode
    {
        AssignOverride,
        ClearOverride,
        RestoreDefault
    }

    [Header("NPC")]
    [Tooltip("NPC ID to update.")]
    [SerializeField] private string npcId = "";

    [Header("Assignment")]
    [Tooltip("How this step should update the NPC's quest interaction bridge.")]
    [SerializeField] private ProfileAssignmentMode mode = ProfileAssignmentMode.AssignOverride;

    [Tooltip("Profile to assign when mode is Assign Override.")]
    [SerializeField] private QuestInteractionProfileSO profile;

    [Tooltip("If true, this step fails when the NPC has no QuestProgressInteractionBridge host. If false, it logs and completes successfully.")]
    [SerializeField] private bool failIfBridgeMissing = true;

    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogError("[Step_NPC_SetQuestInteractionProfile] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }

        if (mode == ProfileAssignmentMode.AssignOverride && profile == null)
        {
            Debug.LogError("[Step_NPC_SetQuestInteractionProfile] Profile is required when mode is Assign Override.");
            onComplete?.Invoke(StepOutcome.Failed("Profile is required for Assign Override"));
            yield break;
        }

        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_SetQuestInteractionProfile] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }

        QuestProgressInteractionBridge bridge = npc.GetComponentInChildren<QuestProgressInteractionBridge>(true);
        if (bridge == null)
        {
            string message = $"NPC '{npcId}' has no QuestProgressInteractionBridge host.";
            if (failIfBridgeMissing)
            {
                Debug.LogError($"[Step_NPC_SetQuestInteractionProfile] {message}");
                onComplete?.Invoke(StepOutcome.Failed(message));
                yield break;
            }

            Debug.LogWarning($"[Step_NPC_SetQuestInteractionProfile] {message} Step skipped.");
            onComplete?.Invoke(StepOutcome.Success(message));
            yield break;
        }

        switch (mode)
        {
            case ProfileAssignmentMode.AssignOverride:
                bridge.SetRuntimeProfileOverride(profile);
                if (enableDebugLogs)
                {
                    Debug.Log($"[Step_NPC_SetQuestInteractionProfile] Assigned override profile '{profile.name}' to NPC '{npcId}'.");
                }
                break;

            case ProfileAssignmentMode.ClearOverride:
                bridge.ClearRuntimeProfileOverride();
                if (enableDebugLogs)
                {
                    Debug.Log($"[Step_NPC_SetQuestInteractionProfile] Cleared runtime profile override for NPC '{npcId}'.");
                }
                break;

            case ProfileAssignmentMode.RestoreDefault:
                bridge.RestoreDefaultProfile();
                if (enableDebugLogs)
                {
                    string fallback = bridge.DefaultProfile != null
                        ? $"restored default profile '{bridge.DefaultProfile.name}'"
                        : "restored default state with no default profile assigned";
                    Debug.Log($"[Step_NPC_SetQuestInteractionProfile] {fallback} for NPC '{npcId}'.");
                }
                break;

            default:
                Debug.LogError($"[Step_NPC_SetQuestInteractionProfile] Unsupported mode '{mode}'.");
                onComplete?.Invoke(StepOutcome.Failed($"Unsupported mode '{mode}'"));
                yield break;
        }

        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }

    public override bool Validate()
    {
        if (string.IsNullOrWhiteSpace(npcId))
        {
            Debug.LogWarning("[Step_NPC_SetQuestInteractionProfile] NPC ID is not set!");
            return false;
        }

        if (mode == ProfileAssignmentMode.AssignOverride && profile == null)
        {
            Debug.LogWarning("[Step_NPC_SetQuestInteractionProfile] Profile is not assigned for Assign Override mode!");
            return false;
        }

        return true;
    }
}
