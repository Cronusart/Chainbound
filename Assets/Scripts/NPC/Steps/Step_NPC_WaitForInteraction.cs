using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that waits for player interaction with an NPC.
/// Supports proximity-only or button press modes.
/// </summary>
[CreateAssetMenu(fileName = "Step_NPC_WaitForInteraction", menuName = "Chainbound/Narrative/Steps/NPC/Wait For Interaction")]
public class Step_NPC_WaitForInteraction : SequenceStep
{
    [Header("NPC")]
    [Tooltip("NPC ID to wait for interaction")]
    [SerializeField] private string npcId = "";
    
    [Header("Interaction Mode")]
    [Tooltip("Proximity-only: triggers automatically when player enters radius. Button press: requires interact button")]
    [SerializeField] private InteractionMode mode = InteractionMode.ProximityOnly;
    
    [Header("Settings")]
    [Tooltip("Interaction radius (world units)")]
    [SerializeField] private float interactionRadius = 2f;
    
    [Tooltip("Player tag")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Interact button name (if mode is ButtonPress)")]
    [SerializeField] private string interactButtonName = "Interact";
    
    public enum InteractionMode
    {
        ProximityOnly,
        ButtonPress
    }
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogError("[Step_NPC_WaitForInteraction] NPC ID is empty!");
            onComplete?.Invoke(StepOutcome.Failed("NPC ID is empty"));
            yield break;
        }
        
        NPCController npc = NPCRegistry.Instance.GetNPC(npcId);
        if (npc == null)
        {
            Debug.LogError($"[Step_NPC_WaitForInteraction] NPC '{npcId}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"NPC '{npcId}' not found"));
            yield break;
        }
        
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogError("[Step_NPC_WaitForInteraction] Player not found!");
            onComplete?.Invoke(StepOutcome.Failed("Player not found"));
            yield break;
        }
        
        Vector3 npcPos = npc.transform.position;
        bool interactionTriggered = false;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPC_WaitForInteraction] Waiting for interaction with NPC '{npcId}' (mode: {mode})");
        }
        
        while (!interactionTriggered)
        {
            // Respect global interaction locks (dialogue, on-screen keyboard, etc.).
            InputLockService inputLock = InputLockService.Instance;
            bool interactionLocked = inputLock != null && inputLock.IsInteractionLocked;

            float distance = Vector3.Distance(player.transform.position, npcPos);
            
            if (!interactionLocked && distance <= interactionRadius)
            {
                if (mode == InteractionMode.ProximityOnly)
                {
                    // Proximity-only: trigger immediately
                    interactionTriggered = true;
                }
                else if (mode == InteractionMode.ButtonPress)
                {
                    // Button press: check for input
                    if (UnityEngine.Input.GetButtonDown(interactButtonName))
                    {
                        interactionTriggered = true;
                    }
                }
            }
            
            yield return null;
        }
        
        // Record interaction in NPC memory
        NPCMemoryComponent memory = npc.GetComponent<NPCMemoryComponent>();
        if (memory != null)
        {
            memory.RecordInteraction();
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_NPC_WaitForInteraction] Interaction triggered with NPC '{npcId}'");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(npcId))
        {
            Debug.LogWarning("[Step_NPC_WaitForInteraction] NPC ID is not set!");
            return false;
        }
        return true;
    }
}

