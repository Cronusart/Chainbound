using UnityEngine;

/// <summary>
/// Temporarily disables NPC "pause for player" blocking while narrative sequence steps
/// drive movement (<see cref="Step_NPC_MoveTo"/>, <see cref="Step_NPC_Follow"/>).
/// Designers toggle this per step via <c>suppressPauseForPlayerDuringStep</c> on those assets (sync steps only).
/// Routine schedule/route movement never enters this scope, so prefab
/// <see cref="NPCMovementAgent"/> respectPlayerBlocker stays the default for autonomy.
/// </summary>
public static class NPCNarrativePlayerBlockScope
{
    /// <summary>
    /// Increments suppress depth on this NPC's <see cref="NPCMovementAgent"/>.
    /// </summary>
    public static void Enter(NPCController npc)
    {
        if (npc == null)
            return;

        NPCMovementAgent agent = npc.GetComponent<NPCMovementAgent>();
        if (agent != null)
            agent.BeginNarrativePlayerBlockSuppress();
    }

    /// <summary>
    /// Decrements suppress depth. Call in <c>finally</c> paired with <see cref="Enter"/>.
    /// </summary>
    public static void Exit(NPCController npc)
    {
        if (npc == null)
            return;

        NPCMovementAgent agent = npc.GetComponent<NPCMovementAgent>();
        if (agent != null)
            agent.EndNarrativePlayerBlockSuppress();
    }
}
