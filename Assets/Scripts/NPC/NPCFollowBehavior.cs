using System.Collections;
using UnityEngine;

/// <summary>
/// Follow behavior for NPCs (reusable for Chainlings later).
/// Follows a target (player) within a distance band with low-frequency repath.
/// </summary>
public class NPCFollowBehavior : MonoBehaviour
{
    [Header("Follow Settings")]
    [Tooltip("Target to follow (null = follow player)")]
    [SerializeField] private Transform target;
    
    [Tooltip("Minimum follow distance (NPC stops when closer)")]
    [SerializeField] private float minFollowDistance = 2f;
    
    [Tooltip("Maximum follow distance — when farther than this, NPC paths to a standoff point on this radius (not to the target's pivot), so they do not walk into the player.")]
    [SerializeField] private float maxFollowDistance = 5f;
    
    [Tooltip("How often to recalculate path (seconds)")]
    [SerializeField] private float repathInterval = 0.5f;
    
    [Tooltip("Maximum duration to follow (0 = no limit)")]
    [SerializeField] private float maxDuration = 0f;
    
    [Tooltip("Stop following when target is reached")]
    [SerializeField] private bool stopOnArrival = true;
    
    [Header("References")]
    [SerializeField] private NPCMovementAgent movementAgent;
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    
    private bool isFollowing = false;
    private float followStartTime = 0f;
    private float lastRepathTime = 0f;
    private Coroutine followCoroutine;
    
    private void Awake()
    {
        if (movementAgent == null)
            movementAgent = GetComponent<NPCMovementAgent>();
    }
    
    /// <summary>
    /// Start following target (async).
    /// </summary>
    /// <param name="completeWhenInFollowBand">
    /// If true, exits as soon as horizontal distance is within [minDist, maxDist] (one-shot catch-up).
    /// If false, keeps following until duration, too-close + stopOnReach, or external <see cref="StopFollowing"/>.
    /// </param>
    public IEnumerator FollowTarget(
        Transform followTarget,
        float minDist,
        float maxDist,
        float duration = 0f,
        bool stopOnReach = true,
        bool completeWhenInFollowBand = false)
    {
        target = followTarget;
        minFollowDistance = minDist;
        maxFollowDistance = maxDist;
        maxDuration = duration;
        stopOnArrival = stopOnReach;
        
        isFollowing = true;
        followStartTime = Time.time;
        lastRepathTime = Time.time;
        
        if (enableDebugLogs)
        {
            Debug.Log($"[NPCFollowBehavior] {gameObject.name} started following {(target != null ? target.name : "Player")}");
        }

        UpdateMovementFacingTarget();

        try
        {
            while (isFollowing)
            {
                // Check timeout
                if (maxDuration > 0f && Time.time - followStartTime >= maxDuration)
                {
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[NPCFollowBehavior] {gameObject.name} follow duration expired");
                    }
                    break;
                }

                // Check if target is valid
                if (target == null)
                {
                    // Try to find player
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        target = player.transform;
                        UpdateMovementFacingTarget();
                    }
                    else
                    {
                        if (enableDebugLogs)
                        {
                            Debug.LogWarning($"[NPCFollowBehavior] {gameObject.name} follow target is null!");
                        }
                        break;
                    }
                }

                // Horizontal distance (follow band is on XZ; ignore Y for thresholds)
                float distance = Vector3.Distance(FlatXZ(transform.position), FlatXZ(target.position));

                // One-shot: NPC reached acceptable standoff band — complete follow (sequence step can advance)
                if (completeWhenInFollowBand && distance >= minFollowDistance && distance <= maxFollowDistance)
                {
                    if (movementAgent != null)
                        movementAgent.StopMovement();

                    if (enableDebugLogs)
                    {
                        Debug.Log(
                            $"[NPCFollowBehavior] {gameObject.name} in follow band (d={distance:F2}), one-shot complete.");
                    }

                    break;
                }

                // Repath check
                if (Time.time - lastRepathTime >= repathInterval)
                {
                    lastRepathTime = Time.time;

                    if (distance > maxFollowDistance)
                    {
                        // Path to a standoff point on the ring at maxFollowDistance, not to the target pivot
                        Vector3 standoff = GetStandoffDestination(transform.position, target.position, maxFollowDistance);
                        yield return movementAgent.MoveToPosition(standoff, 10f);
                    }
                    else if (distance < minFollowDistance && stopOnArrival)
                    {
                        // Too close, stop
                        movementAgent.StopMovement();
                        if (enableDebugLogs)
                        {
                            Debug.Log($"[NPCFollowBehavior] {gameObject.name} reached follow target, stopping");
                        }
                        break;
                    }
                    else
                    {
                        // In range, maintain position
                        movementAgent.StopMovement();
                    }
                }

                yield return null;
            }
        }
        finally
        {
            isFollowing = false;
            ClearMovementFacingTarget();

            if (movementAgent != null)
            {
                movementAgent.StopMovement();
            }

            if (enableDebugLogs)
            {
                Debug.Log($"[NPCFollowBehavior] {gameObject.name} stopped following");
            }
        }
    }
    
    /// <summary>
    /// Stop following.
    /// </summary>
    public void StopFollowing()
    {
        isFollowing = false;
        if (movementAgent != null)
        {
            movementAgent.StopMovement();
        }
    }
    
    /// <summary>
    /// Check if currently following.
    /// </summary>
    public bool IsFollowing => isFollowing;

    private void UpdateMovementFacingTarget()
    {
        if (movementAgent != null)
        {
            movementAgent.SetFollowFacingTarget(target);
        }
    }

    private void ClearMovementFacingTarget()
    {
        if (movementAgent != null)
        {
            movementAgent.SetFollowFacingTarget(null);
        }
    }

    private static Vector3 FlatXZ(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    /// <summary>
    /// World position on the XZ circle around <paramref name="targetWorld"/> at <paramref name="radius"/>,
    /// on the ray from target toward follower (natural approach direction).
    /// </summary>
    private static Vector3 GetStandoffDestination(Vector3 followerWorld, Vector3 targetWorld, float radius)
    {
        Vector3 delta = FlatXZ(followerWorld) - FlatXZ(targetWorld);
        if (delta.sqrMagnitude < 0.0001f)
            delta = Vector3.forward * 0.01f;

        delta.Normalize();
        Vector3 dest = FlatXZ(targetWorld) + delta * Mathf.Max(0.05f, radius);
        dest.y = targetWorld.y;
        return dest;
    }
}

