using UnityEngine;

/// <summary>
/// Suspends schedule/route autonomy while narrative code drives the NPC via
/// <see cref="NPCMovementAgent"/> (MoveToPosition, follow, etc.).
/// Prevents <see cref="NPCController.OnTimeSegmentChanged"/> and other callers of
/// <see cref="NPCController"/> routing from restarting the route coroutine mid-step.
/// </summary>
public static class NPCNarrativeAutonomyScope
{
    /// <summary>
    /// Snapshot current autonomy and disable schedule-driven routes for this NPC.
    /// </summary>
    /// <param name="restoreAutonomyEnabled">Pass to <see cref="Exit"/> to restore previous state.</param>
    public static void Enter(NPCController npc, out bool restoreAutonomyEnabled)
    {
        restoreAutonomyEnabled = npc != null && npc.AutonomyEnabled;
        if (npc != null)
            npc.SetAutonomyEnabled(false);
    }

    /// <summary>
    /// Restore autonomy to the value captured in <see cref="Enter"/>.
    /// </summary>
    public static void Exit(NPCController npc, bool restoreAutonomyEnabled)
    {
        if (npc == null)
            return;
        npc.SetAutonomyEnabled(restoreAutonomyEnabled);
    }
}
