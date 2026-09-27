using System.Collections;
using UnityEngine;

/// <summary>
/// Step that waits until player is a certain distance from a point.
/// </summary>
[CreateAssetMenu(fileName = "Step_WaitForDistance", menuName = "Chainbound/Narrative/Steps/Wait For Distance")]
public class Step_WaitForDistance : SequenceStep
{
    [Header("Distance Check")]
    [Tooltip("World position to check distance from")]
    [SerializeField] private Vector3 checkPosition = Vector3.zero;
    
    [Tooltip("Use transform ID instead of world position")]
    [SerializeField] private bool useTransformId = false;
    
    [Tooltip("Transform ID to check distance from")]
    [SerializeField] private string transformId = "";
    
    [Tooltip("Distance threshold")]
    [SerializeField] private float distanceThreshold = 5f;
    
    [Tooltip("Check if player is within distance (true) or beyond distance (false)")]
    [SerializeField] private bool withinDistance = true;
    
    [Header("Player")]
    [Tooltip("Player tag")]
    [SerializeField] private string playerTag = "Player";
    
    public override IEnumerator Execute(SequenceContext context, System.Action<StepOutcome> onComplete)
    {
        // Find player
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player == null)
        {
            Debug.LogError("[Step_WaitForDistance] Player not found!");
            onComplete?.Invoke(StepOutcome.Failed("Player not found"));
            yield break;
        }
        
        // Get check position
        Vector3 targetPos = checkPosition;
        if (useTransformId && !string.IsNullOrEmpty(transformId))
        {
            GameObject found = GameObject.Find(transformId);
            if (found != null)
            {
                targetPos = found.transform.position;
            }
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_WaitForDistance] Waiting for player distance from {targetPos} (threshold: {distanceThreshold})");
        }
        
        // Wait for distance condition
        while (true)
        {
            float distance = Vector3.Distance(player.transform.position, targetPos);
            bool conditionMet = withinDistance ? (distance <= distanceThreshold) : (distance >= distanceThreshold);
            
            if (conditionMet)
            {
                break;
            }
            
            yield return null;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_WaitForDistance] Distance condition met");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
    }
    
    public override bool Validate()
    {
        if (!useTransformId && checkPosition == Vector3.zero)
        {
            Debug.LogWarning("[Step_WaitForDistance] Check position is zero and not using transform ID!");
            return false;
        }
        if (useTransformId && string.IsNullOrEmpty(transformId))
        {
            Debug.LogWarning("[Step_WaitForDistance] Transform ID is empty!");
            return false;
        }
        return true;
    }
}

