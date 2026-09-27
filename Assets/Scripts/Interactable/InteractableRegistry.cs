using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of interactables currently in range of the player.
/// InteractableObject (and NPC bindings) register on trigger enter, unregister on exit.
/// PlayerInteractor queries GetNearest() to resolve which object receives the Interact input.
/// </summary>
public class InteractableRegistry : MonoBehaviour
{
    private static InteractableRegistry instance;

    public static InteractableRegistry Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InteractableRegistry>();
                if (instance == null)
                {
                    var go = new GameObject("[InteractableRegistry]");
                    instance = go.AddComponent<InteractableRegistry>();
                }
            }
            return instance;
        }
    }

    [Header("Player Reference")]
    [Tooltip("Player transform for distance calculation. Auto-finds by tag if null.")]
    [SerializeField] private Transform playerTransform;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private readonly List<IInteractable> inRange = new List<IInteractable>();

    private void Awake()
    {
        if (playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }

    /// <summary>Register an interactable as in range.</summary>
    public void Register(IInteractable interactable)
    {
        if (interactable == null) return;
        if (inRange.Contains(interactable)) return;

        inRange.Add(interactable);
        if (enableDebugLogs)
            Debug.Log($"[InteractableRegistry] Registered {interactable.Transform?.name ?? "?"} (count={inRange.Count})");
    }

    /// <summary>Unregister an interactable.</summary>
    public void Unregister(IInteractable interactable)
    {
        if (interactable == null) return;
        inRange.Remove(interactable);
        if (enableDebugLogs)
            Debug.Log($"[InteractableRegistry] Unregistered {interactable.Transform?.name ?? "?"} (count={inRange.Count})");
    }

    /// <summary>Get the nearest valid interactable to the player, or null.</summary>
    public IInteractable GetNearest()
    {
        if (playerTransform == null) return null;

        IInteractable nearest = null;
        float nearestSq = float.MaxValue;

        for (int i = inRange.Count - 1; i >= 0; i--)
        {
            var item = inRange[i];
            if (item == null || item.Transform == null)
            {
                inRange.RemoveAt(i);
                continue;
            }

            if (!item.IsInteractable)
                continue;

            float sqDist = (item.Transform.position - playerTransform.position).sqrMagnitude;
            if (sqDist < nearestSq)
            {
                nearestSq = sqDist;
                nearest = item;
            }
        }

        return nearest;
    }

    /// <summary>Check if any interactable is in range (for indicator logic).</summary>
    public bool HasAnyInRange => inRange.Count > 0;

    /// <summary>Set player transform (e.g. when player spawns).</summary>
    public void SetPlayerTransform(Transform t) => playerTransform = t;
}
