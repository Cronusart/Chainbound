using UnityEngine;

/// <summary>
/// Default presentation behavior for normal NPC interactions.
/// This is used by <see cref="NPCInteractionSessionService"/> when an <see cref="NPCInteractableBinding"/>
/// starts a standard interaction, and is intentionally separate from quest routing or dialogue content.
/// </summary>
[CreateAssetMenu(
    fileName = "NPCInteractionBehavior",
    menuName = "Chainbound/NPC/Interaction Behavior")]
public class NPCInteractionBehaviorSO : ScriptableObject
{
    [Header("Route")]
    [Tooltip("When enabled, a normal interaction temporarily pauses the NPC's route autonomy.")]
    [SerializeField] private bool pauseRouteWhileInteracting = true;

    [Tooltip("How route autonomy should be paused while the normal interaction session is active.")]
    [SerializeField] private NPCInteractionRoutePauseMode routePauseMode = NPCInteractionRoutePauseMode.ForceHoldPosition;

    [Header("Facing")]
    [Tooltip("When enabled, the NPC immediately snaps to face the player when the interaction begins. Ending the interaction restores the facing captured before that snap. Route movement replaces it once the NPC walks again.")]
    [SerializeField] private bool facePlayerOnStart = true;

    [Tooltip("When enabled, unlocked dialogue keeps the NPC facing the player until dialogue completes. The pre-interaction facing is still restored when the session ends.")]
    [SerializeField] private bool trackPlayerWhileUnlockedDialogue = true;

    public bool PauseRouteWhileInteracting => pauseRouteWhileInteracting;
    public NPCInteractionRoutePauseMode RoutePauseMode => routePauseMode;
    public bool FacePlayerOnStart => facePlayerOnStart;
    public bool TrackPlayerWhileUnlockedDialogue => trackPlayerWhileUnlockedDialogue;
}

public enum NPCInteractionRoutePauseMode
{
    DisableSchedule,
    DisableAutonomy,
    ForceHoldPosition
}
