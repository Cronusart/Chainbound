using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shared occupancy helpers for camera zone inner and buffer volumes.
/// Multiple colliders on one trigger are one area: presence is the union.
/// </summary>
public static class CameraZoneOccupancy
{
    public static void CollectInnerColliders(
        CameraZoneTrigger trigger,
        List<Collider> results,
        bool markAsTrigger = true)
    {
        results.Clear();
        if (trigger == null)
            return;

        var found = new List<Collider>();
        trigger.GetComponents(found);
        Collider[] descendants = trigger.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Collider descendant = descendants[i];
            if (descendant != null && !found.Contains(descendant))
                found.Add(descendant);
        }

        for (int i = 0; i < found.Count; i++)
        {
            Collider collider = found[i];
            if (collider == null)
                continue;
            if (collider.GetComponentInParent<CameraZoneBuffer>() != null)
                continue;

            CameraZoneTrigger owningTrigger = collider.gameObject == trigger.gameObject
                ? trigger
                : collider.GetComponentInParent<CameraZoneTrigger>();
            if (owningTrigger == null && collider.transform.IsChildOf(trigger.transform))
                owningTrigger = trigger;
            if (owningTrigger != trigger)
                continue;

            if (markAsTrigger)
                collider.isTrigger = true;
            results.Add(collider);
        }
    }

    public static void CollectBufferColliders(
        CameraZoneBuffer buffer,
        List<Collider> results,
        bool markAsTrigger = true)
    {
        results.Clear();
        if (buffer == null)
            return;

        Collider[] found = buffer.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < found.Length; i++)
        {
            Collider collider = found[i];
            if (collider == null)
                continue;

            if (markAsTrigger)
                collider.isTrigger = true;
            results.Add(collider);
        }
    }

    /// <summary>
    /// True while a covered scene transition still owns spawn/camera finalization.
    /// Streaming chunk IO must not sample occupancy before the player is placed.
    /// </summary>
    public static bool IsSceneTransitionOccupancyDeferred()
    {
        SceneLoader loader = SceneLoader.Instance;
        return loader != null && loader.IsTransitioning;
    }

    /// <summary>
    /// Spawn/load occupancy is sampled from position, not trigger enter.
    /// Compound child volumes and additive chunks can exist a frame after the
    /// player is placed, so wait briefly, then resync every live zone.
    /// </summary>
    public static IEnumerator WaitThenResyncOccupancy(
        System.Func<Vector3> positionSource,
        float timeoutSeconds = 2f,
        int maxFrames = 120)
    {
        if (positionSource == null)
        {
            ResyncAllLiveZones();
            yield break;
        }

        timeoutSeconds = Mathf.Max(0f, timeoutSeconds);
        maxFrames = Mathf.Max(1, maxFrames);
        float startedAt = Time.unscaledTime;
        int frames = 0;

        while (true)
        {
            Physics.SyncTransforms();
            CameraZoneTrigger zone = FindTriggerContaining(positionSource());
            if (zone != null)
            {
                zone.ForceActivateAtPosition(positionSource(), reassertPresentation: true);
                break;
            }

            bool timedOut = frames > 0 && Time.unscaledTime - startedAt >= timeoutSeconds;
            if (timedOut || frames >= maxFrames)
                break;

            yield return null;
            frames++;
        }

        ResyncAllLiveZones();
    }

    /// <summary>
    /// Reconciles every live camera zone with the player's actual position.
    /// Additive chunk IO can fire false trigger enter/exit; occupancy must not
    /// depend on those events. A zone that no longer contains the player must
    /// not restore yaw while another loaded zone still does.
    /// </summary>
    public static void ResyncAllLiveZones()
    {
        Physics.SyncTransforms();

        CameraZoneTrigger[] zones = Object.FindObjectsByType<CameraZoneTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (zones == null || zones.Length == 0)
            return;

        GameObject player = PlayerRootResolver.ResolveActivePlayer("Player");
        Vector3 playerPosition = player != null ? player.transform.position : Vector3.zero;
        bool hasPlayerPosition = player != null;

        bool playerInsideAny = false;
        bool[] containsPlayer = new bool[zones.Length];
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            // Spawn/stream resync is point occupancy. AABB overlap of a nested
            // inner volume must not keep that zone alive while the player root
            // is only in the outer compound.
            bool inside = hasPlayerPosition
                ? zone.OccupiesPosition(playerPosition)
                : ContainsPlayer(zone);
            containsPlayer[i] = inside;
            if (inside)
                playerInsideAny = true;
        }

        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            if (containsPlayer[i])
                zone.ForceActivateIfPlayerInside();
            else if (zone.IsActivated)
                zone.ReleaseOccupancy(restorePresentation: !playerInsideAny);
        }

        if (playerInsideAny)
            ReassertWinningZone();
    }

    /// <summary>
    /// Present the smallest live zone that still contains the player.
    /// Overlapping independent zones keep occupancy, but only the winner's
    /// yaw/profile should remain on the camera after a smaller zone dismisses.
    /// </summary>
    public static void ReassertWinningZone(CameraZoneTrigger except = null)
    {
        GameObject player = PlayerRootResolver.ResolveActivePlayer("Player");
        if (player == null)
            return;

        CameraZoneTrigger winner = FindTriggerContaining(player.transform.position);
        if (winner == null || winner == except || !winner.isActiveAndEnabled)
            return;

        winner.ForceActivateAtPosition(player.transform.position, reassertPresentation: true);
    }

    /// <summary>
    /// A nested presentation zone owns the camera exclusively while its inner
    /// footprint is inside an outer zone's compound inner union. Vertical
    /// extents are deliberately ignored: camera-zone boxes are commonly made
    /// tall for traversal tolerance and do not need matching Y bounds.
    /// </summary>
    public static bool IsNestedPresentationPair(
        CameraZoneTrigger inner,
        CameraZoneTrigger outer)
    {
        if (inner == null || outer == null || inner == outer)
            return false;

        var innerColliders = new List<Collider>();
        var outerColliders = new List<Collider>();
        CollectInnerColliders(inner, innerColliders, markAsTrigger: false);
        CollectInnerColliders(outer, outerColliders, markAsTrigger: false);
        if (innerColliders.Count == 0 || outerColliders.Count == 0)
            return false;

        for (int i = 0; i < innerColliders.Count; i++)
        {
            Collider collider = innerColliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            Bounds bounds = collider.bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.center.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.center.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.center.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.center.y, bounds.max.z)
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                if (!ContainsPlanarPosition(outerColliders, corners[cornerIndex]))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// True when an already-active zone still has the player in its buffer ring
    /// (outside that zone's inner volume). Nested rooms are not latched: they
    /// take the camera as soon as their inner volume is entered.
    /// </summary>
    public static bool ActivatedBufferHoldsTransition(CameraZoneTrigger incoming, Vector3 position)
    {
        if (incoming == null)
            return false;

        CameraZoneTrigger[] zones = Object.FindObjectsByType<CameraZoneTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger held = zones[i];
            if (held == null || held == incoming || !held.isActiveAndEnabled || !held.IsActivated)
                continue;
            if (held.ContainsPosition(position))
                continue;
            if (!BufferContainsPosition(held, position))
                continue;
            if (IsNestedPresentationPair(incoming, held))
                continue;
            return true;
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="profile"/> is the authored profile of a zone
    /// nested inside <paramref name="outer"/>.
    /// </summary>
    public static bool IsProfileFromNestedInner(CameraZoneTrigger outer, CameraProfile profile)
    {
        if (outer == null || profile == null)
            return false;

        CameraZoneTrigger[] zones = Object.FindObjectsByType<CameraZoneTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger candidate = zones[i];
            if (candidate == null || candidate == outer || !candidate.isActiveAndEnabled)
                continue;

            CameraZoneBehavior behavior = candidate.ZoneBehavior;
            if (behavior == null || behavior.ZoneProfile != profile)
                continue;
            if (IsNestedPresentationPair(candidate, outer))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Drops only true containing outer zones. Independent overlaps keep their
    /// normal occupancy, which preserves Academy stair-zone behavior.
    /// </summary>
    public static void DismissContainingOuterZones(
        CameraZoneTrigger winner,
        Vector3 position)
    {
        if (winner == null)
            return;

        CameraZoneTrigger[] zones = Object.FindObjectsByType<CameraZoneTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger candidate = zones[i];
            if (candidate == null || candidate == winner || !candidate.isActiveAndEnabled)
                continue;
            if (!candidate.OccupiesPosition(position))
                continue;
            if (!IsNestedPresentationPair(winner, candidate))
                continue;

            candidate.ReleaseOccupancy(restorePresentation: false);
        }
    }

    private static bool ContainsPlanarPosition(IList<Collider> colliders, Vector3 position)
    {
        for (int i = 0; i < colliders.Count; i++)
        {
            Collider collider = colliders[i];
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;

            if (collider is BoxCollider box)
            {
                Vector3 local = box.transform.InverseTransformPoint(position) - box.center;
                Vector3 half = box.size * 0.5f;
                if (Mathf.Abs(local.x) <= Mathf.Abs(half.x) + 0.0001f &&
                    Mathf.Abs(local.z) <= Mathf.Abs(half.z) + 0.0001f)
                    return true;
                continue;
            }

            Bounds bounds = collider.bounds;
            if (position.x >= bounds.min.x - 0.0001f &&
                position.x <= bounds.max.x + 0.0001f &&
                position.z >= bounds.min.z - 0.0001f &&
                position.z <= bounds.max.z + 0.0001f)
                return true;
        }

        return false;
    }

    public static bool ContainsPlayer(CameraZoneTrigger trigger)
    {
        return trigger != null &&
               (trigger.IsPlayerPhysicallyInsideInner() || trigger.IsPlayerPhysicallyInsideBuffer());
    }

    public static bool AnyLiveZoneContainsPlayer(CameraZoneTrigger except = null)
    {
        CameraZoneTrigger[] zones = Object.FindObjectsByType<CameraZoneTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger zone = zones[i];
            if (zone == null || zone == except || !zone.isActiveAndEnabled)
                continue;
            if (ContainsPlayer(zone))
                return true;
        }

        return false;
    }

    public static bool ContainsPosition(IList<Collider> colliders, Vector3 position)
    {
        if (colliders == null)
            return false;

        for (int i = 0; i < colliders.Count; i++)
        {
            if (ColliderContainsPoint(colliders[i], position))
                return true;
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="position"/> is inside the collider's actual
    /// volume (oriented box / closest-point), not just its world AABB.
    /// Box size components are abs'd so authored negative extents still count.
    /// </summary>
    public static bool ColliderContainsPoint(Collider collider, Vector3 position)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
            return false;

        if (collider is BoxCollider box)
            return BoxColliderContainsPoint(box, position);

        Vector3 closest = collider.ClosestPoint(position);
        return (closest - position).sqrMagnitude <= 0.0001f;
    }

    public static bool BoxColliderContainsPoint(BoxCollider box, Vector3 position)
    {
        if (box == null)
            return false;

        Vector3 local = box.transform.InverseTransformPoint(position) - box.center;
        Vector3 half = box.size * 0.5f;
        half.x = Mathf.Abs(half.x);
        half.y = Mathf.Abs(half.y);
        half.z = Mathf.Abs(half.z);
        return Mathf.Abs(local.x) <= half.x + 0.0001f &&
               Mathf.Abs(local.y) <= half.y + 0.0001f &&
               Mathf.Abs(local.z) <= half.z + 0.0001f;
    }

    /// <summary>
    /// Live camera zone that contains <paramref name="position"/>.
    /// Nested independent zones beat a wrapping compound/buffer: inner
    /// containment wins first, then the smallest containing inner piece.
    /// Buffer is only used when no inner volume contains the point.
    /// </summary>
    public static CameraZoneTrigger FindTriggerContaining(Vector3 position)
    {
        CameraZoneTrigger[] zones = Object.FindObjectsByType<CameraZoneTrigger>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        CameraZoneTrigger bestInner = null;
        float bestInnerVolume = float.PositiveInfinity;
        CameraZoneTrigger bestBuffer = null;
        float bestBufferVolume = float.PositiveInfinity;
        for (int i = 0; i < zones.Length; i++)
        {
            CameraZoneTrigger zone = zones[i];
            if (zone == null || !zone.isActiveAndEnabled)
                continue;

            if (zone.ContainsPosition(position))
            {
                float volume = ApproximateContainingVolume(zone, position);
                if (volume >= bestInnerVolume)
                    continue;
                bestInnerVolume = volume;
                bestInner = zone;
                continue;
            }

            if (!BufferContainsPosition(zone, position))
                continue;

            float bufferVolume = ApproximateInnerVolume(zone);
            if (bufferVolume >= bestBufferVolume)
                continue;
            bestBufferVolume = bufferVolume;
            bestBuffer = zone;
        }

        return bestInner != null ? bestInner : bestBuffer;
    }

    public static bool BufferContainsPosition(CameraZoneTrigger trigger, Vector3 position)
    {
        if (trigger == null)
            return false;

        CameraZoneBuffer[] buffers = trigger.GetComponentsInChildren<CameraZoneBuffer>(true);
        for (int i = 0; i < buffers.Length; i++)
        {
            CameraZoneBuffer buffer = buffers[i];
            if (buffer == null)
                continue;
            var colliders = new List<Collider>();
            CollectBufferColliders(buffer, colliders, markAsTrigger: false);
            if (ContainsPosition(colliders, position))
                return true;
        }

        return false;
    }

    public static float ApproximateInnerVolume(CameraZoneTrigger trigger)
    {
        if (trigger == null)
            return float.PositiveInfinity;

        var inner = new List<Collider>();
        CollectInnerColliders(trigger, inner, markAsTrigger: false);
        float volume = 0f;
        for (int i = 0; i < inner.Count; i++)
        {
            Collider collider = inner[i];
            if (collider == null)
                continue;
            Vector3 size = collider.bounds.size;
            volume += Mathf.Abs(size.x) * Mathf.Abs(size.y) * Mathf.Abs(size.z);
        }

        return volume > 0f ? volume : float.PositiveInfinity;
    }

    /// <summary>
    /// Volume of inner pieces that actually contain <paramref name="position"/>.
    /// A huge compound wrapper is judged by the piece you are in, not by a
    /// distant tiny root box or the sum of unused pieces.
    /// </summary>
    public static float ApproximateContainingVolume(CameraZoneTrigger trigger, Vector3 position)
    {
        if (trigger == null)
            return float.PositiveInfinity;

        var inner = new List<Collider>();
        CollectInnerColliders(trigger, inner, markAsTrigger: false);
        float volume = 0f;
        for (int i = 0; i < inner.Count; i++)
        {
            Collider collider = inner[i];
            if (collider == null || !ColliderContainsPoint(collider, position))
                continue;
            volume += ColliderVolume(collider);
        }

        return volume > 0f ? volume : float.PositiveInfinity;
    }

    private static float ColliderVolume(Collider collider)
    {
        if (collider is BoxCollider box)
        {
            Vector3 size = box.size;
            Vector3 scale = box.transform.lossyScale;
            return Mathf.Abs(size.x * scale.x) * Mathf.Abs(size.y * scale.y) * Mathf.Abs(size.z * scale.z);
        }

        Vector3 boundsSize = collider.bounds.size;
        return Mathf.Abs(boundsSize.x) * Mathf.Abs(boundsSize.y) * Mathf.Abs(boundsSize.z);
    }

    public static bool IsPlayerPhysicallyInside(IList<Collider> zoneColliders, string playerTag)
    {
        if (zoneColliders == null || zoneColliders.Count == 0)
            return false;

        GameObject playerObj = PlayerRootResolver.ResolveActivePlayer(playerTag);
        if (playerObj == null)
            return false;

        for (int i = 0; i < zoneColliders.Count; i++)
        {
            if (IsPlayerPhysicallyInside(zoneColliders[i], playerObj))
                return true;
        }

        return false;
    }

    public static bool IsPlayerPhysicallyInside(Collider zoneCol, GameObject playerObj)
    {
        if (zoneCol == null || playerObj == null)
            return false;

        Bounds zoneBounds = zoneCol.bounds;
        if (ColliderContainsPoint(zoneCol, playerObj.transform.position))
            return true;

        Collider[] playerColliders = playerObj.GetComponentsInChildren<Collider>(true);
        if (playerColliders == null || playerColliders.Length == 0)
            return false;

        for (int i = 0; i < playerColliders.Length; i++)
        {
            Collider playerCol = playerColliders[i];
            if (playerCol == null || playerCol.isTrigger)
                continue;
            if (zoneBounds.Intersects(playerCol.bounds))
                return true;
        }

        return false;
    }

    public static void CopyExtendedCollider(Collider inner, Transform destination, float extension)
    {
        if (inner == null || destination == null)
            return;

        if (inner is BoxCollider innerBox)
        {
            BoxCollider bufferBox = destination.gameObject.AddComponent<BoxCollider>();
            bufferBox.center = innerBox.center;
            bufferBox.size = innerBox.size + Vector3.one * extension * 2f;
            bufferBox.isTrigger = true;
        }
        else if (inner is SphereCollider innerSphere)
        {
            SphereCollider bufferSphere = destination.gameObject.AddComponent<SphereCollider>();
            bufferSphere.center = innerSphere.center;
            bufferSphere.radius = innerSphere.radius + extension;
            bufferSphere.isTrigger = true;
        }
        else if (inner is CapsuleCollider innerCapsule)
        {
            CapsuleCollider bufferCapsule = destination.gameObject.AddComponent<CapsuleCollider>();
            bufferCapsule.center = innerCapsule.center;
            bufferCapsule.radius = innerCapsule.radius + extension;
            bufferCapsule.height = innerCapsule.height + extension * 2f;
            bufferCapsule.direction = innerCapsule.direction;
            bufferCapsule.isTrigger = true;
        }
    }

    public static Transform CreateMatchingBufferHost(Collider inner, Transform triggerRoot, Transform bufferRoot)
    {
        if (inner == null || triggerRoot == null || bufferRoot == null)
            return bufferRoot;

        if (inner.transform == triggerRoot)
        {
            GameObject host = new GameObject("InnerVolume_Buffer");
            host.transform.SetParent(bufferRoot, false);
            host.transform.localPosition = Vector3.zero;
            host.transform.localRotation = Quaternion.identity;
            host.transform.localScale = Vector3.one;
            return host.transform;
        }

        GameObject childHost = new GameObject(inner.gameObject.name + "_Buffer");
        childHost.transform.SetParent(bufferRoot, false);
        childHost.transform.localPosition = triggerRoot.InverseTransformPoint(inner.transform.position);
        childHost.transform.localRotation = Quaternion.Inverse(triggerRoot.rotation) * inner.transform.rotation;
        childHost.transform.localScale = Vector3.one;
        return childHost.transform;
    }

    public static void EnsureRelay(Collider collider, MonoBehaviour owner)
    {
        if (collider == null || owner == null)
            return;
        if (collider.gameObject == owner.gameObject)
            return;

        CameraZoneVolumeRelay relay = collider.GetComponent<CameraZoneVolumeRelay>();
        if (relay == null)
            relay = collider.gameObject.AddComponent<CameraZoneVolumeRelay>();
        relay.Bind(owner);
    }

    public static void DrawColliderGizmo(Collider col, bool wireframe)
    {
        if (col == null)
            return;

        Gizmos.matrix = col.transform.localToWorldMatrix;
        if (col is BoxCollider box)
        {
            if (wireframe)
                Gizmos.DrawWireCube(box.center, box.size);
            else
                Gizmos.DrawCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 center = col.transform.TransformPoint(sphere.center);
            if (wireframe)
                Gizmos.DrawWireSphere(center, sphere.radius);
            else
                Gizmos.DrawSphere(center, sphere.radius);
        }
        else if (col is CapsuleCollider capsule)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(col.transform.TransformPoint(capsule.center), capsule.radius);
        }

        Gizmos.matrix = Matrix4x4.identity;
    }

    public static void DrawBufferPreviewGizmo(Collider inner, float extension)
    {
        if (inner is BoxCollider box)
        {
            Gizmos.matrix = inner.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size + Vector3.one * extension * 2f);
        }
        else if (inner is SphereCollider sphere)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireSphere(
                inner.transform.TransformPoint(sphere.center),
                sphere.radius + extension);
        }
    }
}
