using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Step that enables or disables colliders on a GameObject.
/// Useful for managing boundary colliders without affecting the GameObject itself.
/// </summary>
[CreateAssetMenu(fileName = "Step_SetColliderState", menuName = "Chainbound/Narrative/Steps/Set Collider State")]
public class Step_SetColliderState : SequenceStep
{
    [Header("GameObject")]
    [Tooltip("Name of GameObject with colliders")]
    [SerializeField] private string gameObjectName = "";
    
    [Tooltip("Enable or disable colliders")]
    [SerializeField] private bool enabled = true;
    
    [Tooltip("Affect colliders on children")]
    [SerializeField] private bool affectChildren = true;
    
    public override IEnumerator Execute(SequenceContext context, Action<StepOutcome> onComplete)
    {
        if (string.IsNullOrEmpty(gameObjectName))
        {
            Debug.LogError("[Step_SetColliderState] GameObject name is empty!");
            onComplete?.Invoke(StepOutcome.Failed("GameObject name is empty"));
            yield break;
        }
        
        GameObject obj = GameObject.Find(gameObjectName);
        
        if (obj == null)
        {
            Debug.LogWarning($"[Step_SetColliderState] GameObject '{gameObjectName}' not found!");
            onComplete?.Invoke(StepOutcome.Failed($"GameObject '{gameObjectName}' not found"));
            yield break;
        }
        
        Collider[] colliders;
        if (affectChildren)
        {
            colliders = obj.GetComponentsInChildren<Collider>();
        }
        else
        {
            colliders = obj.GetComponents<Collider>();
        }
        
        int affectedCount = 0;
        foreach (var col in colliders)
        {
            col.enabled = enabled;
            affectedCount++;
        }
        
        if (enableDebugLogs)
        {
            Debug.Log($"[Step_SetColliderState] {(enabled ? "Enabled" : "Disabled")} {affectedCount} collider(s) on: {gameObjectName}");
        }
        
        onComplete?.Invoke(StepOutcome.Success());
        yield return null;
    }
    
    public override bool Validate()
    {
        if (string.IsNullOrEmpty(gameObjectName))
        {
            Debug.LogWarning("[Step_SetColliderState] GameObject name is not set!");
            return false;
        }
        return true;
    }
}

