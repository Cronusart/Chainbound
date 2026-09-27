using UnityEngine;

/// <summary>
/// Binding component that exposes NPC to sequence system.
/// Handles claim tokens and routes commands to NPCController.
/// This is a thin wrapper around NPCController for sequence integration.
/// </summary>
[RequireComponent(typeof(NPCController))]
public class NPCSequencerBinding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NPCController npcController;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private void Awake()
    {
        if (npcController == null)
            npcController = GetComponent<NPCController>();
    }
    
    /// <summary>
    /// Get the NPC controller.
    /// </summary>
    public NPCController Controller => npcController;
    
    /// <summary>
    /// Get NPC ID.
    /// </summary>
    public string NPCId => npcController != null ? npcController.NPCId : "";
}

