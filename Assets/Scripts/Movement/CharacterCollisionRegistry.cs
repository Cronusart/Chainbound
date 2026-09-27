using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight runtime registry for solid player/NPC colliders that participate in
/// top-down character blocking. Triggers are intentionally excluded so interaction
/// volumes keep working even when shove collisions are ignored.
/// </summary>
public static class CharacterCollisionRegistry
{
    private static readonly HashSet<Collider> playerSolidColliders = new HashSet<Collider>();
    private static readonly HashSet<Collider> npcSolidColliders = new HashSet<Collider>();

    public static event Action<Collider> OnNpcColliderRegistered;
    public static event Action<Collider> OnNpcColliderUnregistered;

    public static IEnumerable<Collider> NpcSolidColliders
    {
        get
        {
            PruneInvalid(npcSolidColliders);
            return npcSolidColliders;
        }
    }

    public static void RegisterPlayerCollider(Collider collider)
    {
        if (!IsValidSolid(collider))
            return;

        playerSolidColliders.Add(collider);
    }

    public static void UnregisterPlayerCollider(Collider collider)
    {
        if (collider == null)
            return;

        playerSolidColliders.Remove(collider);
    }

    public static void RegisterNpcCollider(Collider collider)
    {
        if (!IsValidSolid(collider))
            return;

        if (npcSolidColliders.Add(collider))
            OnNpcColliderRegistered?.Invoke(collider);
    }

    public static void UnregisterNpcCollider(Collider collider)
    {
        if (collider == null)
            return;

        if (npcSolidColliders.Remove(collider))
            OnNpcColliderUnregistered?.Invoke(collider);
    }

    public static bool TryGetPreferredPlayerCollider(out Collider collider)
    {
        PruneInvalid(playerSolidColliders);

        foreach (Collider candidate in playerSolidColliders)
        {
            if (candidate != null && candidate.enabled && !candidate.isTrigger)
            {
                collider = candidate;
                return true;
            }
        }

        foreach (Collider candidate in playerSolidColliders)
        {
            if (candidate != null && !candidate.isTrigger)
            {
                collider = candidate;
                return true;
            }
        }

        collider = null;
        return false;
    }

    private static bool IsValidSolid(Collider collider)
    {
        return collider != null && !collider.isTrigger;
    }

    private static void PruneInvalid(HashSet<Collider> colliders)
    {
        if (colliders.Count == 0)
            return;

        colliders.RemoveWhere(collider => collider == null || collider.isTrigger);
    }
}
