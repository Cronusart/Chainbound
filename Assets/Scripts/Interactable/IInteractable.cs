using UnityEngine;

/// <summary>
/// Interface for objects the player can interact with (NPCs, static objects, etc.).
/// Used by InteractableRegistry and PlayerInteractor to resolve the nearest valid target.
/// </summary>
public interface IInteractable
{
    /// <summary>Transform for position/distance calculations. Use for indicator pivot.</summary>
    Transform Transform { get; }

    /// <summary>Whether this interactable is currently valid (armed, in range, etc.).</summary>
    bool IsInteractable { get; }

    /// <summary>Invoked when the player triggers interaction (button press or auto-enter).</summary>
    void Interact();
}
